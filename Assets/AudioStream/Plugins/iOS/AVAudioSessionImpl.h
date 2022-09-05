//
//  AVAudioSessionImpl.h
//	part of Unity 'AudioStream' asset
//
//	Copyright © 2016-2019 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
//
//	Thin wrapper around shared AVAudioSession used by Unity/FMOD
// 

#import <Foundation/Foundation.h>
#import <AVFoundation/AVFoundation.h>

NS_ASSUME_NONNULL_BEGIN

@interface AVAudioSessionImpl : NSObject

// currently detected inputs and outputs
// with poor's man ( meaning crap ) caching flag which needs to be set from outside
// TODO: move to properties get/setters / delegate ?
@property (readonly) NSArray<AVAudioSessionPortDescription *> *availableInputs;
@property (readonly) NSArray<AVAudioSessionPortDescription *> *availableOutputs;
@property (readwrite) BOOL availableInputsChanged;
@property (readwrite) BOOL availableOutputsChanged;
@property (readonly) BOOL isSessionReady;

// detected format
// channels for pcmData
@property (readonly) uint channels;
@property (readonly) double samplerate;
// PCM data per channel
@property (readonly) float *_Nullable *_Nullable pcmData;
@property (readwrite) BOOL pcmDataWasUpdated;
// lenght of one frame
@property (readonly) uint pcmDataSamples;
@property (readonly) uint pcmDataBytesPerSample;


+(instancetype)sharedInstance;
-(void)UpdateAVAudioSession:(AVAudioSessionCategoryOptions)withOptions;

-(void)SetPreferredInput:(AVAudioSessionPortDescription*)input;

-(void)StartRecording;
-(void)StopRecording;
-(BOOL)IsRecording;

@end

NS_ASSUME_NONNULL_END
