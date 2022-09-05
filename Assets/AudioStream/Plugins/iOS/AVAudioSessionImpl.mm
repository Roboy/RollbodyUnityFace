//
//  AVAudioSessionImpl.mm
//	part of Unity 'AudioStream' asset
//
//	Copyright © 2016-2019 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
//
//	Thin wrapper around shared AVAudioSession used by Unity/FMOD
// 

// TODO: should handle return error codes from AVAudioSession calls

#import "AVAudioSessionImpl.h"
#include "UnityInterface.h"

@interface AVAudioSessionImpl ()

// readwrite properties for class internal
@property (readwrite) NSArray<AVAudioSessionPortDescription *> *availableInputs;
@property (readwrite) NSArray<AVAudioSessionPortDescription *> *availableOutputs;
@property (readwrite) BOOL isSessionReady;
@property (readwrite) uint channels;
@property (readwrite) double samplerate;
@property (readwrite) float *_Nullable *_Nullable pcmData;
@property (readwrite) uint pcmDataSamples;
@property (readwrite) uint pcmDataBytesPerSample;

@end

@implementation AVAudioSessionImpl
{
    // recording input tap
    // size of the tap buffer & pcm waveform
    uint bSize;
    
    // currently running session parameters
    AVAudioSessionCategory category;
    AVAudioSessionCategoryOptions options;
    
    // input override
    AVAudioSessionPortDescription *preferredInput;
    
    // recording buffer
    AVAudioEngine *rec_engine;
}

+(instancetype)sharedInstance
{
    static dispatch_once_t once;
    static id instance;
    dispatch_once(&once, ^{
        instance = [[self alloc] init];
    });
    return instance;
}

-(id)init
{
    if (self = [super init])
    {
        self->bSize = 1024;
        self->rec_engine = nil;
        self.availableInputsChanged = NO;
        self.availableOutputsChanged = NO;
        self.isSessionReady = NO;
        self.pcmDataWasUpdated = NO;
        
        // subscribe to route change notification
        [[NSNotificationCenter defaultCenter] addObserver:self selector:@selector(routeChanged:) name:AVAudioSessionRouteChangeNotification object:nil];
    }
    
    return self;
}

-(void)dealloc
{
    // remove notification
    [[NSNotificationCenter defaultCenter] removeObserver:self];
}

// Updates Unity's FMOD's AVAudioSession to include whatever passed options (typically e.g. bluettoth input devices and/or default speaker override)
// currently this hack first triggers unity audio session activation - to be notified about route change - and sets session options immediately after the call
// currently ~~ iOS 12., Unity 2019.
// ( FMOD/OAFR won't run without original session active )
-(void)UpdateAVAudioSession:(AVAudioSessionCategoryOptions)withOptions
{
    self.isSessionReady = NO;
    
    // deactivate session first - or don't if not needed, which doesn't seem to be
    // UnitySetAudioSessionActive(0);
    
    // update current (Unity) category with user requested options
    AVAudioSessionCategory          currentCategory = [[AVAudioSession sharedInstance] category];
    
    self->category = currentCategory;
    self->options = withOptions;
    
    UnitySetAudioSessionActive(1);
    
    [[AVAudioSession sharedInstance] setCategory:self->category withOptions:self->options error:nil];
    
    // invoke preffered input after category activation to correctly tie input/output (to e.g. iPhone microphone/Receiver) - otherwise with e.g. connected BT device
    // output will stay on BT and setup recording session will not start due to immediate route change with newDeviceAvailable reason
    [self SetPreferredInput:[[[AVAudioSession sharedInstance] availableInputs] firstObject]];
}

-(void)routeChanged:(NSNotification*)notification
{
    // crash catch for some reason
    if (!notification)
        return;
    
    // notification reason -
    if (![[notification userInfo] valueForKey:AVAudioSessionRouteChangeReasonKey])
        return;
    
    AVAudioSessionRouteChangeReason reason = (AVAudioSessionRouteChangeReason)[[[notification userInfo] valueForKey:AVAudioSessionRouteChangeReasonKey] integerValue];
    
    NSLog(@"");
    NSLog(@"===================== route change, reason: %@", [self AudioSessionReasonDescription:reason]);
    NSLog(@"");
    
    // reflect ports change
    
    // trigger another route change by querying conected input if there's device change such as dis/connecting BT device
    if (reason == AVAudioSessionRouteChangeReasonNewDeviceAvailable
        || reason == AVAudioSessionRouteChangeReasonOldDeviceUnavailable
        )
    {
        // retrigger session after device dis/appearance
        // - on the main thread because of Unity SoundManager... -
        dispatch_async(dispatch_get_main_queue(), ^{
            [self UpdateAVAudioSession:self->options];
        });
    }
    else
    {
        // work around .playback category reporting available inputs ( connect in AVAudioEngine then fails.. )
        // in general check input and output node for categories for which they make sense
        
        if (self->category == AVAudioSessionCategoryRecord)
        {
            // no outputs
            self.availableInputs = [[AVAudioSession sharedInstance] availableInputs];
            self.availableOutputs = nil;
        }
        else if (self->category == AVAudioSessionCategoryPlayback
                 || self->category == AVAudioSessionCategoryAmbient
                 || self->category == AVAudioSessionCategorySoloAmbient)
        {
            self.availableInputs = nil;
            self.availableOutputs = [[[AVAudioSession sharedInstance] currentRoute] outputs];
        }
        else
        {
            self.availableInputs = [[AVAudioSession sharedInstance] availableInputs];
            self.availableOutputs = [[[AVAudioSession sharedInstance] currentRoute] outputs];
        }
        
        self.availableInputsChanged = YES;
        self.availableOutputsChanged = YES;
        self.isSessionReady = YES;
        
        NSLog(@"");
        NSLog(@"===================== inputs : %lu", (unsigned long)self.availableInputs.count);
        NSLog(@"%@", self.availableInputs);
        NSLog(@"");
        NSLog(@"===================== outputs: %lu", (unsigned long)self.availableOutputs.count);
        NSLog(@"%@", self.availableOutputs);
        NSLog(@"");
    }
}

-(void)SetPreferredInput:(AVAudioSessionPortDescription*)input
{
    [[AVAudioSession sharedInstance] setPreferredInput:input error:nil];
    self->preferredInput = input;
}

-(void)StartRecording
{
    [self StopRecording];
    
    // setup engine and input buffer tap
    
    self->rec_engine = [[AVAudioEngine alloc] init];
    
    AVAudioMixerNode *rec_mixer = [self->rec_engine mainMixerNode];
    AVAudioInputNode *rec_input = [self->rec_engine inputNode];
    
    if (rec_mixer && rec_input)
    {
        AVAudioFormat *format = [rec_input inputFormatForBus:0];
        NSLog(@"Input Node In  : %@", format);
        
        [self->rec_engine connect:rec_input to:rec_mixer format:format];
        
        self.channels = format.channelCount;
        self.samplerate = format.sampleRate;
        
        // allocate pcm buffer on first tap
        __block BOOL formatDetected = NO;
        
        [rec_input installTapOnBus:0 bufferSize:self->bSize format:format block:^(AVAudioPCMBuffer * _Nonnull tapBuffer, AVAudioTime * _Nonnull when)
         {
             if (!tapBuffer.floatChannelData)
             {
                 NSLog(@"Unsupported tap buffer format [floatChannelData: %p, int16ChannelData: %p, int32ChannelData: %p]. Please contact AudioStream support.", tapBuffer.floatChannelData, tapBuffer.int16ChannelData, tapBuffer.int32ChannelData);
                 return;
             }
             
             // prepare extraction buffer/s on start, or when tap is changed (can it change?)
             if (!formatDetected
                 || tapBuffer.frameLength != self.pcmDataSamples
                 || tapBuffer.format.streamDescription->mBytesPerFrame != self.pcmDataBytesPerSample
                 )
             {
                 formatDetected = YES;
                 NSLog(@"Tap buffer     : %@", tapBuffer.format);
                 
                 // deallocate / update previous run
                 if (self.pcmData)
                 {
                     for (int ch = 0; ch < self.channels; ++ch)
                         free(self.pcmData[ch]);
                     
                     free(self.pcmData);
                 }
                 
                 self.pcmData = (float * _Nullable * _Nullable)malloc(self.channels * sizeof(float*));
                 self.pcmDataSamples = tapBuffer.frameLength;
                 self.pcmDataBytesPerSample = tapBuffer.format.streamDescription->mBytesPerFrame;
                 
                 for (int ch = 0; ch < self.channels; ++ch)
                     self.pcmData[ch] = (float * _Nullable)malloc(self.pcmDataSamples * self.pcmDataBytesPerSample);
             }
             
             // (iPhone 7 mic)
             // NSLog(@"%@ %d, %d, %d, %lu", tapBuffer.format, tapBuffer.frameCapacity, tapBuffer.frameLength, tapBuffer.format.interleaved, (unsigned long)tapBuffer.stride);
             // <AVAudioFormat 0x2826bbd40:  1 ch,  44100 Hz, Float32> 4410, 4410, 0, 1
             
             for (int ch = 0; ch < self.channels; ++ch)
             {
                 // The returned pointer is to format.channelCount pointers to float. Each of these pointers
                 // is to "frameLength" valid samples, which are spaced by "stride" samples.
                 
                 // If format.interleaved is false (as with the standard deinterleaved float format), then
                 // the pointers will be to separate chunks of memory. "stride" is 1.
                 //
                 // If format.interleaved is true, then the pointers will refer into the same chunk of interleaved
                 // samples, each offset by 1 frame. "stride" is the number of interleaved channels.
                 
                 if (!tapBuffer.format.interleaved)
                 {
                     memcpy(self.pcmData[ch], tapBuffer.floatChannelData[ch], self.pcmDataSamples * self.pcmDataBytesPerSample);
                 }
                 else
                 {
                     for (int i = 0; i < self.pcmDataSamples; ++i)
                     {
                         memcpy(
                                &self.pcmData[ch][i * self.pcmDataBytesPerSample]
                                ,&tapBuffer.floatChannelData[ch][(i * self.channels) + (ch * self.pcmDataBytesPerSample)]
                                ,self.pcmDataBytesPerSample
                                );
                     }
                 }
             }
             
             self.pcmDataWasUpdated = YES;
         }];
    }
    
    [self->rec_engine prepare];
    
    NSError *error;
    [self->rec_engine startAndReturnError:&error];
    
    // without this call after the engine has started, the recorder will record from (for some reason) automatically triggered route change with newDeviceAvailable where e.g. AirPods are default input
    // despite previous setPreferredInput call by user which requested default mic
    // this ensures that preferred input switches back immediately and it seems to work
    // it might be a bug in iOS 12 (.1.4 at the time) - esp. since other bugreports seemed to be submitted - e.g. https://github.com/CraigLn/ios12-airpods-routing-bugreport - dealing with similar inconsistency on outputs -
    // inputs and outpus are tighly coupled for BT devices: see e.g. https://developer.apple.com/library/archive/qa/qa1799/_index.html
    if (self->preferredInput != nil) {
        [self SetPreferredInput:self->preferredInput];
    }
}

-(void)StopRecording
{
    [self->rec_engine stop];
    self->rec_engine = nil;
}

-(BOOL)IsRecording
{
    return self->rec_engine.isRunning;
}

#pragma -
-(NSString*)AudioSessionReasonDescription:(AVAudioSessionRouteChangeReason)reason
{
    switch (reason)
    {
        case AVAudioSessionRouteChangeReasonUnknown:
            return @"Unknown";
        case AVAudioSessionRouteChangeReasonNewDeviceAvailable:
            return @"NewDeviceAvailable";
        case AVAudioSessionRouteChangeReasonOldDeviceUnavailable:
            return @"OldDeviceUnavailable";
        case AVAudioSessionRouteChangeReasonCategoryChange:
            return @"CategoryChange";
        case AVAudioSessionRouteChangeReasonOverride:
            return @"Override";
        case AVAudioSessionRouteChangeReasonWakeFromSleep:
            return @"WakeFromSleep";
        case AVAudioSessionRouteChangeReasonNoSuitableRouteForCategory:
            return @"NoSuitableRouteForCategory";
        case AVAudioSessionRouteChangeReasonRouteConfigurationChange:
            return @"RouteConfigurationChange";
        default:
            return @"";
    }
}

@end
