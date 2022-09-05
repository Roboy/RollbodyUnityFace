// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using UnityEngine;
using System.Runtime.InteropServices;

public static class iOSSpeaker
{
#if UNITY_IOS && !UNITY_EDITOR
	[DllImport("__Internal")]
	private static extern void _RouteForPlayback();
	[DllImport("__Internal")]
	private static extern void _RouteForRecording();
#endif

	public static void RouteForPlayback()
    {
#if UNITY_IOS && !UNITY_EDITOR
        _RouteForPlayback();
#endif
	}

	public static void RouteForRecording()
    {
#if UNITY_IOS && !UNITY_EDITOR
        _RouteForRecording();
#endif
	}
}
