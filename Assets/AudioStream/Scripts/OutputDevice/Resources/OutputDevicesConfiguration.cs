// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System.Collections.Generic;
using UnityEngine;

namespace AudioStream
{
    // [CreateAssetMenu]
    public class OutputDevicesConfiguration : ScriptableObject
    {
        // TODO: custom editor for enum in list/array element
        [System.Serializable]
        public class OutputDeviceConfiguration
        {
            [Tooltip("Speaker mode for redirected signal\r\n\r\nNote: Other than default is rather advanced setup and usually better left untouched / at default /.\r\nWhen raw speaker mode is selected that should default to 2 speakers ( stereo ), unless changed by user.")]
            public FMOD.SPEAKERMODE SPEAKERMODE;
            [Tooltip("No. of speakers for RAW speaker mode. You might want also provide mix matrix for custom setups,\r\nsee remarks at https://www.fmod.com/docs/api/content/generated/FMOD_SPEAKERMODE.html, \r\nand https://www.fmod.com/docs/api/content/generated/FMOD_Channel_SetMixMatrix.html about how to setup the matrix.")]
            public int NumOfRawSpeakers = 2;
            // TODO: custom DSP buffer per device
        }

        [Tooltip("User override settings for an output device - usually not needed.\r\nEach list element corresponds to output device with that ID\r\nAn example is added in store package which is considered when creating a system for output 0, but since it is set to defaults, defaults of the output are used as well.")]
        public List<OutputDeviceConfiguration> outputDevicesConfiguration = new List<OutputDeviceConfiguration>();

        static OutputDevicesConfiguration _instance;
        public static OutputDevicesConfiguration Instance
        {
            get
            {
                if (OutputDevicesConfiguration._instance == null )
                {
                    OutputDevicesConfiguration._instance = Resources.Load<OutputDevicesConfiguration>("OutputDevicesConfiguration");
                }

                return OutputDevicesConfiguration._instance;
            }
        }
    }
}