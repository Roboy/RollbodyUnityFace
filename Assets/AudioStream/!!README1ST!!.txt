
Hi, welcome and thanks for your interest in AudioStream !


Please read carefully before anything else!
===========================================

--------------------------------------------------
>> For new users: <<
--------------------------------------------------

AudioStream uses FMOD Studio functionality, which redistribution is not allowed for 3rd party SDKs ( such as this one ).
Therefore, when you first import AudioStream into a new project two kinds of compile errors will occur:
For AudioStream C# scripts: error CS0246: 'The type or namespace name `FMOD' could not be found. Are you missing a using directive or an assembly reference?'
and
native mixer plugin will fail to load with warning/error (depending on Unity version):
Plugins: Failed to load 'Assets/AudioStream/Plugins/x86_64/AudioPluginAudioStreamOutputDevice.dll' with error 'The specified module could not be found.'.
, followed by the error:
Effect AudioStream OutputDevice could not be found. Check that the project contains the correct native audio plugin libraries and that the importer settings are set up correctly.


This is normal and will be resolved once you manually import the FMOD Studio Unity package and/or add native dll for mixer effect, if you choose to use it.

>:: 
>:: Depending on needed functionality and platform you can choose to use only the mixer plugin for routing audio to any system's output on x86/x64 Windows and macOS from the Unity AudioMixers, use only 'normal' (non mixer effect) AudioStream, or both
>::
See each option install and usage instructions below

NOTE: Native mixer plugin effect 'AudioStream OutputDevice'
- the AudioPluginAudioStreamOutputDevice.dll in Plugins/x86_64, Plugins/x86 and AudioPluginAudioStreamOutputDevice.bundle in Plugins - 
is currently available on 32 and 64-bit Windows and macOS *ONLY*.
> For other platforms/non mixer usage please still use AudioSourceOutputDevice component of AudioStream.


--------------------------------------------------
>> When doing an upgrade of a existing project: <<
--------------------------------------------------

When doing an upgrade of the package to a newer version please be aware that since it uses several native plugins the safest way of upgrading is to:

1] import new Asset Store package into a new separate Unity project
2] copy/overwrite all files manually outside of Unity (i.e. via filesystem) from the above temporary project into upgraded project's 'AudioStream' folder (possibly deleting target files first)
3] delete '_audiostream_demo_assets_prepared' in 'Assets\StreamingAssets\AudioStream' (or the whole directory) in order to copy any new/changed demo assets if you want to run demo scenes



================================================================================================================
FMOD packages installation and download:
================================================================================================================

Required Unity package is available either on FMOD's download site: https://www.fmod.com/download
or on the Asset Store: https://assetstore.unity.com/packages/tools/audio/fmod-for-unity-161631

There is no difference between them as far as I can tell, you can pick one or the other 


==== 
NOTE: you have to create an account and agree to the FMOD EULA before downloading and you are bound by it by using this asset (their licensing policy is very friendly for indies though).


For AudioStream, please follow ** 1] **, for Unity Audio Mixer native plugin follow ** 2] **


================================================================================================================
** 1] ** For AudioStream you need
		'Unity Integration' package (2nd tab on the FMOD downloads page)
		- OR -
		import 'FMOD for Unity' from the Asset Store
================================================================================================================

For FMOD website:
	You will need "FMOD Studio for Unity" from the page, Version at least 2.00.xx - all later versions _should_ work too.
	Warning: Versions prior to 2.00.00 are no longer supported.

> AudioStream uses only low level API of FMOD Studio and only really requires a small part of the "Plugins" folder from the FMOD package.
The Plugins folder contains C# wrapper for FMOD and all necessary platform specific libraries, the rest of the package enables usage of FMOD Studio projects and objects directly in Unity, live editing of FMOD project and access to other FMOD Studio project capabilities.

In general you need only native platforms plugins, and low level FMOD C# wrapper.

When importing the Integration Unity package it's safe to select only:

* 'Plugins/FMOD/lib' folder (+ you can choose only plugins relevant for your intended platform)
* 'Plugins/FMOD/src/Runtime/wrapper' folder
* you probably want to include 'Plugins/FMOD/platform_ios.mm' on iOS
* you should probably include 'Plugins/FMOD/LICENSE.TXT', too

Everything else - does not need - to be imported for this plugin to work.

You project structure just with AudioStream and needed FMOD parts should look like this afterwards:

-------------------                                   
| Assets          | AudioStream                         lib                                                     fmod.cs 
--------------------------------------------------------------------------------------------------------------- fmod_android.cs
| ProjectSettings | Plugins         | FMOD            | src             | Runtime          | wrapper          | fmod_dsp.cs
                  --------------------------------------------------------------------------------------------- fmod_errors.cs
													    LICENSE.TXT                                             fmod_studio.cs

			  
The above is for v 2.00 of the plugin 



Once the FMOD Studio Unity package is successfully imported and setup, AudioStream is ready to use.

You can move AudioStream folder freely anywhere in the project, for example into Plugins to reduce user scripts compile times.

Furthermore, if you don't intend to use native mixer plugin, you can delete :
- AudioStream/Demo/OutputDevice/UnityMixer folder with demo scene
- AudioPluginAudioStreamOutputDevice.bundle mac OS plugin from Plugins
- AudioPluginAudioStreamOutputDevice.dll Windows plugin from Plugins/x86_64/ and Plugins/x86



================================================================================================================
** 2] ** For native audio mixer plugin you need 'FMOD Studio API' Windows/Mac (1st tab on the downloads page )
================================================================================================================

*** remark - the two below mentioned dynamic libraries are included in the asset store package since Asset Store Upload Tools upload them despite not being selected for upload anyway -
- although probably not legally entirely allowed this is huge convenience for the user so I leave them in for the time being -
- but they *should* be downloaded and installed as described below by user to keep everything perfectly legal -


Download and install on Windows/open .dmg on macOS respective installer. You will need just one file from it -

On 64-bit Windows - copy 'fmod.dll' from C:\Program Files (x86)\FMOD SoundSystem\FMOD Studio API Windows\api\core\lib\x64 (default install location) to AudioStream/Plugins/x86_64/
On 32-bit Windows - similarly as above
- the dll *must* be placed alongside AudioPluginAudioStreamOutputDevice.dll

On macOS - copy 'libfmod.dylib' from FMOD Programmers API/api/core/lib to AudioStream/Plugins/
- alongside AudioPluginAudioStreamOutputDevice.bundle


Note that both plugins are compiled against specific version of FMOD at any given time - but should be binary compatible with earlier/future versions


If you don't need other AudioStream functionality you can delete 'everything else', meaning:
- all demo scenes, theirs scripts and resources ( possibly except AudioStream/Demo/OutputDevice/UnityMixer and 'OutputDevice/262447__xinematix__action-percussion-ensemble-fast-4-170-bpm' audio to test the plugin )
- AudioStream/Editor folder
- AudioStream/Plugins/iOS folder
- whole AudioStream/Scripts folder
(the mixer plugin does not use any c# scripts)

You might want to restart Unity once native plugins are in place.

AudioStream/Demo/OutputDevice/UnityMixer/OutputDeviceUnityMixerDemo scene should be now working playing looped AudioClip on system outputs 1 and 2 (with fallback to output 0 if either is not available)


=================================================================================================================================
** !! IMPORTANT !! for AudioStream 2.0 and above and FMOD <= 2.00.05 (around Fall 2019) ** 
=================================================================================================================================

If you get error CS0122: `FMOD.DSP_STATE_FUNCTIONS.getuserdata' is inaccessible due to its protection level after importing FMOD Integration, please fix this manually:
go to the definition of DSP_STATE_FUNCTIONS in fmod_dsp.cs - your IDE should help you navigate there - and make the field public so it then reads:

	...
	public DSP_GETUSERDATA_FUNC getuserdata;
}

- this was ( FMOD 2.00.05 ) not properly exposed in C#, so manual fix was needed, but is fixed in later FMOD releases.




-------------------------------------------

There should be no errors or warnings afterwards.

Please see Documentation.txt for usage notes for each component and few concepts guides.

Package is submitted with Unity 2017.4.1f LTS version.

It's possible to use it in earlier versions ( Unity 5.3.5 and later for mobile applications is recommended in general since previous versions had plethora of varying issues starting with building and ending with ~strike~dragons/~strike~ runtime crashes for unknown reasons.
Standalone should be fine for runtime from 5.0 except with various quality of life issues in the Editor ).

===========================================

In case of any questions / suggestion feel free to ask on support forum. Often things change without notice, especially things like setting up and building to all various/mobile platforms.
And, if AudioStream served you well you might consider leaving a rating and/or review on the Asset Store page - that helps a lot!
(note you should be able to rate directly from Unity's Editor Asset Store Download/Purchases page, without even bothering with a review)

== forum link == :	https://forum.unity.com/threads/audiostream-an-audio-streaming-solution-for-all-and-everywhere.412029/
== email == :		[ not displayed on new asset store page ] - mcv618 at gmail com.
== twitter == :		DMs are open at twitter https://twitter.com/r618
== discord == :		there's also 'audiostream_asset' Discord server: https://discord.gg/5ZyPqeA

Thanks!

Martin
