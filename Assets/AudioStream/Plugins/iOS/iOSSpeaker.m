//
//	iOSSpeaker.m
//	part of Unity 'AudioStream' asset
//
//	Copyright © 2016-2019 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
//
//	Native calls to force input to earspeaker/output to speaker on pre 2017.1 Unity which didn't have 'Force iOS Speakers when Recording' session option
// 

#import "iOSSpeaker.h"
#import <AVFoundation/AVFoundation.h>

void _RouteForPlayback()
{
    // if the headset is connected set default routing, i.e. output to headset
    // otherwise 'fix' the route by routing to speaker ( otherwise earspeaker would be used for PlayAndRecord category by default )
    
    OSStatus error;
    UInt32 audioRouteOverride = _externalDeviceConnected() ? kAudioSessionOverrideAudioRoute_None : kAudioSessionOverrideAudioRoute_Speaker;
    
    error = AudioSessionSetProperty(kAudioSessionProperty_OverrideAudioRoute,
                                    sizeof(audioRouteOverride),
                                    &audioRouteOverride);
    
    if (error)
        NSLog(@"AudioSessionSetProperty, code: %d", (int)error);
    else
        NSLog(@"Forcing audio to speaker");
}

void _RouteForRecording()
{
    // normal route -   if headset not connected    - speaker + mic
    //                  if headset connected        - heaset speaker + headset mic
    OSStatus error;
    UInt32 audioRouteOverride = kAudioSessionOverrideAudioRoute_None;
    
    error = AudioSessionSetProperty(kAudioSessionProperty_OverrideAudioRoute,
                                    sizeof(audioRouteOverride),
                                    &audioRouteOverride);
    
    if (error)
        NSLog(@"AudioSessionSetProperty, code: %d", (int)error);
    else
        NSLog(@"Forcing audio to earspeaker/default");
}

bool _externalDeviceConnected()
{
    UInt32 routeSize = sizeof(CFStringRef);
    CFStringRef route = NULL;
    OSStatus error = AudioSessionGetProperty(kAudioSessionProperty_AudioRoute, &routeSize, &route);
    
    if (!error &&
        (route != NULL)&&
        ([(__bridge NSString*)route rangeOfString:@"Head"].location != NSNotFound))
    {
        /*  don't think this is needed
         see "the get rule":
         https://developer.apple.com/library/mac/#documentation/CoreFoundation/Conceptual/CFMemoryMgmt/Concepts/Ownership.html#//apple_ref/doc/uid/20001148-CJBEJBHH
         */
        //CFRelease(route);
        
        NSLog(@"Headset connected");
        return true;
    }
    
    return false;
}
