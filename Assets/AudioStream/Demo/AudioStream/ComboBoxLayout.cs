// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// GUILayout embeddable combobox
    /// </summary>
    public static class ComboBoxLayout
    {
        /// <summary>
        /// Private container with current/runtime single combobox properties
        /// </summary>
        class ComboBox
        {
            public int id;
            public string[] items;
            public int selectedItemIndex;
            public int dropDownItemsCount;
            public GUIStyle style;
            public GUILayoutOption[] options;

            public Vector2 scroll;

            public Rect rectValue;
            public Rect rectDropDownButton;
            public Rect rectComboBox;

            public bool expanded;

            public ComboBox(int _id, string[] _items, int _selectedItemIndex, int _dropDownItemsCount, GUIStyle _style, params GUILayoutOption[] _options)
            {
                this.id = _id;
                this.items = _items;
                this.selectedItemIndex = _selectedItemIndex;
                this.dropDownItemsCount = _dropDownItemsCount + 1;
                this.style = _style;
                this.options = _options;
                this.scroll = Vector2.zero;
                this.rectValue = Rect.zero;
                this.rectDropDownButton = Rect.zero;
                this.rectComboBox = Rect.zero;
                this.expanded = false;
            }
        }
        /// <summary>
        /// All OnGUI used comboboxes
        /// </summary>
        readonly static List<ComboBox> comboboxes = new List<ComboBox>();
        /// <summary>
        /// Display value box (currently readonly button) next to a dropdown button which toggles dropdown populated with supplied items on click
        /// </summary>
        /// <param name="id">Unique id - use to pair with EndLayout if used</param>
        /// <param name="items">Dropdown content</param>
        /// <param name="selectedItemIndex">Item with this index is selected/populated as combobox value</param>
        /// <param name="dropDownItemsCount"></param>
        /// <param name="style"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static int BeginLayout(int id, string[] items, int selectedItemIndex, int dropDownItemsCount, GUIStyle style, params GUILayoutOption[] options)
        {
            var combobox = ComboBoxLayout.comboboxes.FirstOrDefault(cb => cb.id == id);
            if (combobox == null)
            {
                combobox = new ComboBox(id, items, selectedItemIndex, dropDownItemsCount, style, options);
                ComboBoxLayout.comboboxes.Add(combobox);
            }

            using (new GUILayout.HorizontalScope(combobox.options))
            {
                // combobox value
                GUILayout.Button(combobox.items[selectedItemIndex], combobox.style, combobox.options);
                // - measure displayed value layout 
                if (Event.current.type == EventType.Repaint)
                    combobox.rectValue = GUILayoutUtility.GetLastRect();

                // combobox dropdown button
                bool dropDownButtonClicked = GUILayout.Button("v", combobox.style, GUILayout.Width(25));
                // - measure displayed dropdown button layout before event is consumed by click
                if (Event.current.type == EventType.Repaint)
                    combobox.rectDropDownButton = GUILayoutUtility.GetLastRect();

                // dropdown clicked && we have sane layout values
                // - compute actual dropdown width and toggle dropdown
                if (dropDownButtonClicked && combobox.rectValue != Rect.zero && combobox.rectDropDownButton != Rect.zero)
                {
                    combobox.rectComboBox = combobox.rectValue;
                    combobox.rectComboBox.width = combobox.rectValue.width + combobox.rectDropDownButton.width;

                    combobox.expanded = !combobox.expanded;
                }

                ComboBoxLayout.DisplayLayout(combobox);
            }

            return combobox.selectedItemIndex;
        }

        static void DisplayLayout(ComboBox combobox)
        {
            if (combobox.expanded)
            {
                var itemRect = combobox.rectComboBox;
                var dropDownRect = combobox.rectComboBox;
                var scrollRect = combobox.rectComboBox;

                // dropdown rect based on displayed items
                dropDownRect.y += itemRect.height;
                dropDownRect.height = itemRect.height * (combobox.items.Length > combobox.dropDownItemsCount ? combobox.dropDownItemsCount : combobox.items.Length);

                // scroll rect based on all itesm
                scrollRect.y += itemRect.height;
                scrollRect.height = itemRect.height * combobox.items.Length;

                // TODO: dropdown automatic down/up direction if it overflows bottom of the screen
                /*
                int direction = 1;
                if (dropDownRect.yMax > Screen.height)
                {
                    // update rects

                    direction = -1;
                }
                */

                // dropdown background
                GUI.Box(dropDownRect, "", combobox.style);
                GUI.Box(dropDownRect, "", combobox.style);

                // finally scroll view with items buttons
                combobox.scroll = GUI.BeginScrollView(dropDownRect, combobox.scroll, scrollRect, false, false);

                for (var i = 0; i < combobox.items.Length; ++i)
                {
                    itemRect.y += itemRect.height;
                    if (GUI.Button(itemRect, combobox.items[i], combobox.style))
                    {
                        combobox.selectedItemIndex = i;
                        combobox.expanded = false;
                    }
                }

                GUI.EndScrollView(true);
            }
        }
        /// <summary>
        /// Call to finish single combobox layout - all OnGUI UI elements following this call will be drawn on top of this combobox+dropdown
        /// </summary>
        /// <param name="id">Unique id paired to BeginLayout call</param>
        public static void EndLayout(int id)
        {
            var combobox = ComboBoxLayout.comboboxes.FirstOrDefault(cb => cb.id == id);
            if (combobox == null)
                return;

            ComboBoxLayout.DisplayLayout(combobox);
        }
        /// <summary>
        /// Recommended to call at the end of OnGUI for all comboboxes+dropdowns to be drawn on top of all other UI elements
        /// </summary>
        public static void EndAllLayouts()
        {
            for (var i = 0; i < ComboBoxLayout.comboboxes.Count; ++i)
                ComboBoxLayout.EndLayout(ComboBoxLayout.comboboxes[i].id);
        }
    }
}