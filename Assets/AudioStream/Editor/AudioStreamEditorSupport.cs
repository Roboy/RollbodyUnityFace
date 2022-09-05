// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

// custom editor for conditional displaying of fields in the editor by Mr.Jwolf ( thank you Mr.Jwolf whoever you are )
// https://forum.unity3d.com/threads/inspector-enum-dropdown-box-hide-show-variables.83054/#post-951401

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

using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Helper classes for AudioStream's common custom editor tasks
/// </summary>
namespace AudioStreamEditor
{
    /// <summary>
    /// Polar directivity pattern for spatial sources
    /// </summary>
    public static class DirectivityPattern
    {
        /// Source directivity GUI color.
        static readonly Color ResonanceAudio_sourceDirectivityColor = 0.65f * Color.blue;

        public static void DrawDirectivityPattern(Texture2D directivityTexture, float alpha, float sharpness, int size)
        {
            directivityTexture.Reinitialize(size, size);
            // Draw the axes.
            Color axisColor = ResonanceAudio_sourceDirectivityColor.a * Color.black;
            for (int i = 0; i < size; ++i)
            {
                directivityTexture.SetPixel(i, size / 2, axisColor);
                directivityTexture.SetPixel(size / 2, i, axisColor);
            }
            // Draw the 2D polar directivity pattern.
            float offset = 0.5f * size;
            float cardioidSize = 0.45f * size;
            Vector2[] vertices = ResonanceAudio_Generate2dPolarPattern(alpha, sharpness, 180);
            for (int i = 0; i < vertices.Length; ++i)
            {
                directivityTexture.SetPixel((int)(offset + cardioidSize * vertices[i].x),
                                            (int)(offset + cardioidSize * vertices[i].y), ResonanceAudio_sourceDirectivityColor);
            }
            directivityTexture.Apply();
            // Show the texture.
            GUILayout.Box(directivityTexture);
        }

        /// Generates a set of points to draw a 2D polar pattern.
        static Vector2[] ResonanceAudio_Generate2dPolarPattern(float alpha, float order, int resolution)
        {
            Vector2[] points = new Vector2[resolution];
            float interval = 2.0f * Mathf.PI / resolution;
            for (int i = 0; i < resolution; ++i)
            {
                float theta = i * interval;
                // Magnitude |r| for |theta| in radians.
                float r = Mathf.Pow(Mathf.Abs((1 - alpha) + alpha * Mathf.Cos(theta)), order);
                points[i] = new Vector2(r * Mathf.Sin(theta), r * Mathf.Cos(theta));
            }
            return points;
        }
    }

    public class BoolFieldCondition
    {
        public string boolFieldName { get; set; }
        public bool boolFieldValue { get; set; }
        public string fieldName { get; set; }
        public bool isValid { get; set; }
        public string errorMsg { get; set; }

        /// <summary>
        /// type (descendants) filter, applied if != null
        /// </summary>
        public System.Type[] applicableForTypes { get; set; }

        public string ToStringFunction()
        {
            return "'" + boolFieldName + "', '" + boolFieldValue + "', '" + fieldName + "'.";
        }
    }

    public class EnumFieldCondition
    {
        public string p_enumFieldName { get; set; }
        public string p_enumValue { get; set; }
        public string p_fieldName { get; set; }
        public bool p_isValid { get; set; }
        public string p_errorMsg { get; set; }

        public string ToStringFunction()
        {
            return "'" + p_enumFieldName + "', '" + p_enumValue + "', '" + p_fieldName + "'.";
        }
    }

    /// <summary>
    /// Conditional displaying of fields based on value of other field
    /// </summary>
    public static class ConditionalFields
    {
        public static BoolFieldCondition ShowOnBool(string _boolFieldName, bool _boolFieldValue, string _fieldName, UnityEngine.Object forTarget, System.Type[] forTypes)
        {
            BoolFieldCondition newFieldCondition = new BoolFieldCondition()
            {
                boolFieldName = _boolFieldName,
                boolFieldValue = _boolFieldValue,
                fieldName = _fieldName,
                isValid = true,
                applicableForTypes = forTypes
            };

            //Valildating the "boolFieldName"
            newFieldCondition.errorMsg = "";
            if (forTypes == null
                || forTypes.Contains(forTarget.GetType()))
            {
                // all instance members to include [SerializeField] protected too
                FieldInfo enumField = forTarget.GetType().GetField(newFieldCondition.boolFieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (enumField == null)
                {
                    newFieldCondition.isValid = false;
                    newFieldCondition.errorMsg = "Could not find a bool-field named: '" + _boolFieldName + "' in '" + forTarget + "'. Make sure you have spelled the field name for the enum correct in the script '" + forTarget.ToString() + "'";
                }
            }

            //Valildating the "fieldName"
            if (newFieldCondition.isValid)
            {
                if (forTypes == null
                    || forTypes.Contains(forTarget.GetType()))
                {
                    // all instance members to include [SerializeField] protected too
                    FieldInfo fieldWithCondition = forTarget.GetType().GetField(_fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fieldWithCondition == null)
                    {
                        newFieldCondition.isValid = false;
                        newFieldCondition.errorMsg = "Could not find the field: '" + _fieldName + "' in '" + forTarget + "'. Make sure you have spelled the field name correct in the script '" + forTarget.ToString() + "'";
                    }
                }
            }

            if (!newFieldCondition.isValid)
            {
                newFieldCondition.errorMsg += "\nYour error is within the Custom Editor Script to show/hide fields in the inspector depending on the an Enum." +
                        "\n\n" + forTarget.ToString() + ": " + newFieldCondition.ToStringFunction() + "\n";
            }

            return newFieldCondition;
        }

        /// <summary>
        /// Use this function to set when witch fields should be visible.
        /// </summary>
        /// <param name='enumFieldName'>
        /// The name of the Enum field.
        /// </param>
        /// <param name='enumValue'>
        /// When the Enum value is this in the editor, the field is visible.
        /// </param>
        /// <param name='fieldName'>
        /// The Field name that should only be visible when the chosen enum value is set.
        /// </param>
        public static EnumFieldCondition ShowOnEnum(string enumFieldName, string enumValue, string fieldName, UnityEngine.Object forTarget)
        {
            EnumFieldCondition newFieldCondition = new EnumFieldCondition()
            {
                p_enumFieldName = enumFieldName,
                p_enumValue = enumValue,
                p_fieldName = fieldName,
                p_isValid = true
            };

            //Valildating the "enumFieldName"
            newFieldCondition.p_errorMsg = "";
            FieldInfo enumField = forTarget.GetType().GetField(newFieldCondition.p_enumFieldName);
            if (enumField == null)
            {
                newFieldCondition.p_isValid = false;
                newFieldCondition.p_errorMsg = "Could not find a enum-field named: '" + enumFieldName + "' in '" + forTarget + "'. Make sure you have spelled the field name for the enum correct in the script '" + forTarget.ToString() + "'";
            }

            //Valildating the "enumValue"
            if (newFieldCondition.p_isValid)
            {
                var currentEnumValue = enumField.GetValue(forTarget);
                var enumNames = currentEnumValue.GetType().GetFields();
                //var enumNames =currentEnumValue.GetType().GetEnumNames();
                bool found = false;
                foreach (FieldInfo enumName in enumNames)
                {
                    if (enumName.Name == enumValue)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    newFieldCondition.p_isValid = false;
                    newFieldCondition.p_errorMsg = "Could not find the enum value: '" + enumValue + "' in the enum '" + currentEnumValue.GetType().ToString() + "'. Make sure you have spelled the value name correct in the script '" + forTarget.ToString() + "'";
                }
            }

            //Valildating the "fieldName"
            if (newFieldCondition.p_isValid)
            {
                FieldInfo fieldWithCondition = forTarget.GetType().GetField(fieldName);
                if (fieldWithCondition == null)
                {
                    newFieldCondition.p_isValid = false;
                    newFieldCondition.p_errorMsg = "Could not find the field: '" + fieldName + "' in '" + forTarget + "'. Make sure you have spelled the field name correct in the script '" + forTarget.ToString() + "'";
                }
            }

            if (!newFieldCondition.p_isValid)
            {
                newFieldCondition.p_errorMsg += "\nYour error is within the Custom Editor Script to show/hide fields in the inspector depending on the an Enum." +
                        "\n\n" + forTarget.ToString() + ": " + newFieldCondition.ToStringFunction() + "\n";
            }

            return newFieldCondition;
        }
    }
}