// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

// Directivity texture visualization from Resonance Audio for Unity
// Copyright 2017 Google Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using AudioStream;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AudioStreamEditor
{
    [CustomEditor(typeof(AudioStreamBase), true)]
    [CanEditMultipleObjects]
    public class AudioStreamEditor : Editor
    {
        /// <summary>
        /// Resonance plugin
        /// </summary>
        Texture2D directivityTexture = null;

        void SetFieldCondition()
        {
            // . custom inspector is sometimes buggily invoked for different base class what
            if (target == null)
                return;

            // AudioStreamBase
            // the reflection system cares only about the final enum member name
            this.enumFieldConditions.Add(ConditionalFields.ShowOnEnum("streamType", "RAW", "RAWSoundFormat", target));
            this.enumFieldConditions.Add(ConditionalFields.ShowOnEnum("streamType", "RAW", "RAWFrequency", target));
            this.enumFieldConditions.Add(ConditionalFields.ShowOnEnum("streamType", "RAW", "RAWChannels", target));
            this.enumFieldConditions.Add(ConditionalFields.ShowOnEnum("speakerMode", "RAW", "numOfRawSpeakers", target));

            // AudioStreamMemory
            // this.boolFieldConditions.Add(ConditionalFields.ShowOnBool("useDiskCache", true, "slowClipCreation", target));

            // AudioStreamDownload
            this.boolFieldConditions.Add(ConditionalFields.ShowOnBool("realTimeDecoding", true, "playWhileDownloading", target, new System.Type[] { typeof(AudioStreamDownload) }));
            this.boolFieldConditions.Add(ConditionalFields.ShowOnBool("realTimeDecoding", true, "audioSourceToPlayWhileDownloading", target, new System.Type[] { typeof(AudioStreamDownload) }));
        }

        List<EnumFieldCondition> enumFieldConditions;
        List<BoolFieldCondition> boolFieldConditions;
        public void OnEnable()
        {
            enumFieldConditions = new List<EnumFieldCondition>();
            boolFieldConditions = new List<BoolFieldCondition>();
            SetFieldCondition();

            this.directivityTexture = Texture2D.blackTexture;
        }

        public override void OnInspectorGUI()
        {
            // Update the serializedProperty - always do this in the beginning of OnInspectorGUI.
            serializedObject.Update();

            var obj = serializedObject.GetIterator();

            if (obj.NextVisible(true))
            {
                // Resonance plugin
                float? directivity = null;
                float? directivitySharpness = null;

                // Loops through all visible fields
                do
                {
                    bool shouldBeVisible = true;
                    // Tests if the field is a field that should be hidden/shown due to the enum value
                    foreach (var fieldCondition in enumFieldConditions)
                    {
                        //If the fieldcondition isn't valid, display an error msg.
                        if (!fieldCondition.p_isValid)
                        {
                            Debug.LogError(fieldCondition.p_errorMsg);
                        }
                        else if (fieldCondition.p_fieldName == obj.name)
                        {
                            FieldInfo enumField = target.GetType().GetField(fieldCondition.p_enumFieldName);
                            var currentEnumValue = enumField.GetValue(target);

                            //If the enum value isn't equal to the wanted value the field will be set not to show
                            if (currentEnumValue.ToString() != fieldCondition.p_enumValue)
                            {
                                shouldBeVisible = false;
                                break;
                            }
                        }
                    }

                    // if not precessed
                    if (shouldBeVisible)
                    {
                        // Tests if the field is a field that should be hidden/shown due to the bool value
                        foreach (var fieldCondition in boolFieldConditions)
                        {
                            //If the fieldcondition isn't valid, display an error msg.
                            if (!fieldCondition.isValid)
                            {
                                Debug.LogError(fieldCondition.errorMsg);
                            }
                            else if (fieldCondition.fieldName == obj.name)
                            {
                                FieldInfo enumField = target.GetType().GetField(fieldCondition.boolFieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                var currentBoolValue = (bool)enumField.GetValue(target);

                                //If the bool value isn't equal to the wanted value the field will be set not to show
                                if (currentBoolValue != fieldCondition.boolFieldValue)
                                {
                                    shouldBeVisible = false;
                                    break;
                                }
                            }
                        }
                    }

                    if (shouldBeVisible)
                        EditorGUILayout.PropertyField(obj, true);

                    // Resonance plugin
                    // (these should be always visible...)
                    if (serializedObject.targetObject.GetType() == typeof(ResonanceSource))
                    {
                        if (obj.name == "directivity")
                            directivity = obj.floatValue;

                        if (obj.name == "directivitySharpness")
                            directivitySharpness = obj.floatValue;

                        if (directivity.HasValue && directivitySharpness.HasValue)
                        {
                            GUI.skin.label.wordWrap = true;

                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Approximate spatial spread strength of this audio source:");
                            DirectivityPattern.DrawDirectivityPattern(directivityTexture, directivity.Value, directivitySharpness.Value,
                                                   (int)(3.0f * EditorGUIUtility.singleLineHeight));
                            GUILayout.EndHorizontal();

                            directivity = null;
                            directivitySharpness = null;
                        }
                    }

                } while (obj.NextVisible(false));
            }

            // Apply changes to the serializedProperty - always do this in the end of OnInspectorGUI.
            serializedObject.ApplyModifiedProperties();
        }
    }
}