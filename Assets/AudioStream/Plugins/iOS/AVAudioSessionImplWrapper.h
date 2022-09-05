//
//  AVAudioSessionImplWrapper.h
//	part of Unity 'AudioStream' asset
//
//	Copyright © 2016-2019 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
//
//	C interface for custom AVAudioSession implementation for access from Unity user scripts
// 

#import <Foundation/Foundation.h>
#import <AVFoundation/AVFoundation.h>

#ifndef AVAudioSessionImplWrapper_h
#define AVAudioSessionImplWrapper_h

void _UpdateAVAudioSession(bool bluetoothRecording, bool defaultToSpeaker);
bool _IsSessionReady();
void* _AvailableInputs(int *icount);
void* _AvailableOutputs(int *ocount);

void _SetPreferredInput(const int input);

uint _Channels();
double _Samplerate();
void _PcmData(float** pcmDataPtr);
uint _PcmDataSamples();
uint _PcmDataBytesPerSample();


void _StartRecording();
void _StopRecording();
BOOL _IsRecording();

#endif /* AVAudioSessionImplWrapper_h */
