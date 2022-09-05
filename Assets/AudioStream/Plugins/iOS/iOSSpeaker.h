//
//	iOSSpeaker.h
//	part of Unity 'AudioStream' asset
//
//	Copyright © 2016-2019 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
//
//	Native calls to force input to earspeaker/output to speaker on pre 2017.1 Unity which didn't have 'Force iOS Speakers when Recording' session option
// 

#import <Foundation/Foundation.h>

void _RouteForPlayback();
void _RouteForRecording();
bool _externalDeviceConnected();
