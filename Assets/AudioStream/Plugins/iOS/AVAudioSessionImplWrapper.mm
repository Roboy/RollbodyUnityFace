//
//  AVAudioSessionImplWrapper.m
//	part of Unity 'AudioStream' asset
//
//	Copyright © 2016-2019 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
//
//	C interface for custom AVAudioSession implementation for access from Unity user scripts
// 

#import "AVAudioSessionImpl.h"

// 'members'
char** _availableInputs;
uint _availableInputsCount;
char** _availableOutputs;
uint _availableOutputsCount;
char* _preferredInput;

// Converts C style string to NSString
NSString* CreateNSString (const char* string)
{
    if (string)
        return [NSString stringWithUTF8String: string];
    else
        return [NSString stringWithUTF8String: ""];
}

// Create C string copy
char* MakeStringCopy (const char* string)
{
    if (string == NULL)
        return NULL;
    
    char* res = (char*)malloc(strlen(string) + 1);
    strcpy(res, string);
    return res;
}

// native code plugin functions implemented in .mm / .cpp file must conform to C function naming rules
extern "C" {
    
    void _UpdateAVAudioSession (bool bluetoothRecording, bool defaultToSpeaker)
    {
        // always allow bluetooth output..
        AVAudioSessionCategoryOptions options = AVAudioSessionCategoryOptionAllowBluetoothA2DP;
        
        if (bluetoothRecording)
            options |= AVAudioSessionCategoryOptionAllowBluetooth;
        else
            options &= ~AVAudioSessionCategoryOptionAllowBluetooth;
        
        if (defaultToSpeaker)
            options |= AVAudioSessionCategoryOptionDefaultToSpeaker;
        else
            options &= ~AVAudioSessionCategoryOptionDefaultToSpeaker;
        
        [[AVAudioSessionImpl sharedInstance] UpdateAVAudioSession:options];
    }
    
    bool _IsSessionReady()
    {
        return [[AVAudioSessionImpl sharedInstance] isSessionReady];
    }
    
    void* _AvailableInputs(int* icount)
    {
        if ([AVAudioSessionImpl sharedInstance].availableInputsChanged)
        {
            if (_availableInputs)
            {
                for (int i = 0; i < _availableInputsCount; i++)
                    free(_availableInputs[i]);
                free(_availableInputs);
            }
            
            uint cnt = (uint)[[[AVAudioSessionImpl sharedInstance] availableInputs] count];
            _availableInputs = (char**)malloc(sizeof(char*) * cnt);
            
            for (int i = 0; i < cnt; i++)
                _availableInputs[i] = MakeStringCopy([[[[AVAudioSessionImpl sharedInstance] availableInputs] objectAtIndex:i].portName UTF8String]);
            
            _availableInputsCount = cnt;
            
            [AVAudioSessionImpl sharedInstance].availableInputsChanged = NO;
        }
        
        *icount = _availableInputsCount;
        return (void*)_availableInputs;
    }
    
    void* _AvailableOutputs(int *ocount)
    {
        if ([AVAudioSessionImpl sharedInstance].availableOutputsChanged)
        {
            if (_availableOutputs)
            {
                for (int i = 0; i < _availableOutputsCount; i++)
                    free(_availableOutputs[i]);
                free(_availableOutputs);
            }
            
            uint cnt = (uint)[[[AVAudioSessionImpl sharedInstance] availableOutputs] count];
            _availableOutputs = (char**)malloc(sizeof(char*) * cnt);
            
            for (int i = 0; i < cnt; i++)
                _availableOutputs[i] = MakeStringCopy([[[[AVAudioSessionImpl sharedInstance] availableOutputs] objectAtIndex:i].portName UTF8String]);
            
            _availableOutputsCount = cnt;
            
            [AVAudioSessionImpl sharedInstance].availableOutputsChanged = NO;
        }
        
        *ocount = _availableOutputsCount;
        return (void*)_availableOutputs;
    }
    
    void _SetPreferredInput(const int input)
    {
        AVAudioSessionPortDescription *port = [[[AVAudioSessionImpl sharedInstance] availableInputs] objectAtIndex:input];
        [[AVAudioSessionImpl sharedInstance] SetPreferredInput:port];
    }
    
    void _StartRecording()
    {
        [[AVAudioSessionImpl sharedInstance] StartRecording];
    }
    
    void _StopRecording()
    {
        [[AVAudioSessionImpl sharedInstance] StopRecording];
    }
    
    BOOL _IsRecording()
    {
        return [[AVAudioSessionImpl sharedInstance] IsRecording];
    }
    
    uint _Channels()
    {
        return [[AVAudioSessionImpl sharedInstance] channels];
    }
    
    double _Samplerate()
    {
        return [[AVAudioSessionImpl sharedInstance] samplerate];
    }
    
    void _PcmData(float** pcmDataPtr)
    {
        if ([[AVAudioSessionImpl sharedInstance] pcmData] && [[AVAudioSessionImpl sharedInstance] pcmDataWasUpdated])
        {
            *pcmDataPtr = *([[AVAudioSessionImpl sharedInstance] pcmData]);
            [AVAudioSessionImpl sharedInstance].pcmDataWasUpdated = NO;
        }
        else
        {
            *pcmDataPtr = NULL;
        }
    }
    
    uint _PcmDataSamples()
    {
        return [[AVAudioSessionImpl sharedInstance] pcmDataSamples];
    }
    
    uint _PcmDataBytesPerSample()
    {
        return [[AVAudioSessionImpl sharedInstance] pcmDataBytesPerSample];
    }
}
