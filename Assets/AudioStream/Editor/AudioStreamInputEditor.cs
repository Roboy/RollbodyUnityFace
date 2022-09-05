// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AudioStreamEditor
{
    [CustomEditor(typeof(AudioStreamInputBase), true)]
    [CanEditMultipleObjects]
    public class AudioStreamInputEditor : Editor
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

            // the reflection system cares only about the final enum member name
            this.boolFieldConditions.Add(ConditionalFields.ShowOnBool("useAutomaticDSPBufferSize", false, "dspBufferLength_Custom", target, null));
            this.boolFieldConditions.Add(ConditionalFields.ShowOnBool("useAutomaticDSPBufferSize", false, "dspBufferCount_Custom", target, null));
            this.boolFieldConditions.Add(ConditionalFields.ShowOnBool("resampleInput", true, "useUnityToResampleAndMapChannels", target, null));
        }

        List<BoolFieldCondition> boolFieldConditions;
        public void OnEnable()
        {
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

                    if (shouldBeVisible)
                        EditorGUILayout.PropertyField(obj, true);

                    // Resonance plugin
                    // (these should be always visible...)
                    if (serializedObject.targetObject.GetType() == typeof(ResonanceInput))
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