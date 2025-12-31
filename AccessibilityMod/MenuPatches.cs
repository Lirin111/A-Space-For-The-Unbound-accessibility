using System;
using HarmonyLib;
using MelonLoader;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace AsftuAccessibilityMod.Patches
{
    /// <summary>
    /// Patches for menu navigation to announce menu items and selections
    /// </summary>
    [HarmonyPatch]
    public static class MenuPatches
    {
        // Patch MainMenuManager.MoveSelectedButton for main menu navigation
        [HarmonyPatch(typeof(MainMenuManager), "MoveSelectedButton")]
        [HarmonyPostfix]
        public static void MainMenuManager_MoveSelectedButton_Postfix(MainMenuManager __instance)
        {
            MelonLogger.Msg($"[DEBUG] MainMenuManager_MoveSelectedButton called!");
            
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                // Use reflection to get private fields
                var buttonsField = typeof(MainMenuManager).GetField("buttons", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var selectedIndexField = typeof(MainMenuManager).GetField("selectedButtonIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (buttonsField != null && selectedIndexField != null)
                {
                    var buttons = buttonsField.GetValue(__instance) as System.Collections.IList;
                    int selectedIndex = (int)selectedIndexField.GetValue(__instance);
                    
                    MelonLogger.Msg($"[DEBUG] Selected index: {selectedIndex}, Total buttons: {buttons?.Count}");
                    
                    if (buttons != null && selectedIndex >= 0 && selectedIndex < buttons.Count)
                    {
                        var buttonHolder = buttons[selectedIndex];
                        var textField = buttonHolder.GetType().GetField("text");
                        
                        if (textField != null)
                        {
                            var localizedText = textField.GetValue(buttonHolder) as Component;
                            if (localizedText != null)
                            {
                                // Try to get TextMeshProUGUI component first
                                var tmpUGUI = localizedText.GetComponent<TextMeshProUGUI>();
                                if (tmpUGUI != null && !string.IsNullOrWhiteSpace(tmpUGUI.text))
                                {
                                    TTSManager.AnnounceUI(tmpUGUI.text, interrupt: true);
                                    MelonLogger.Msg($"[MainMenu] Announced UGUI: {tmpUGUI.text}");
                                    return;
                                }
                                
                                // Try to get TextMeshPro component
                                var tmp = localizedText.GetComponent<TextMeshPro>();
                                if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text))
                                {
                                    TTSManager.AnnounceUI(tmp.text, interrupt: true);
                                    MelonLogger.Msg($"[MainMenu] Announced TMP: {tmp.text}");
                                    return;
                                }
                            }
                        }
                    }
                }
                
                MelonLogger.Warning("[MainMenu] Could not get button text via reflection");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in MainMenuManager_MoveSelectedButton_Postfix: {ex.Message}");
            }
        }

        // Patch PauseMenuChildSettings.UpdateOption for settings menu navigation
        [HarmonyPatch(typeof(PauseMenuChildSettings), "UpdateOption")]
        [HarmonyPostfix]
        public static void PauseMenuChildSettings_UpdateOption_Postfix(PauseMenuChildSettings __instance)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                var currentOptionField = typeof(PauseMenuChildVerticalList).GetField("currentOption", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (currentOptionField != null)
                {
                    var currentOption = currentOptionField.GetValue(__instance) as PauseMenuButtonUI;
                    if (currentOption != null)
                    {
                        string optionName = currentOption.Text?.text ?? "";
                        string optionValue = "";

                        // Get the value for each setting
                        if (currentOption == __instance.languageOption)
                        {
                            optionValue = __instance.languageValueOption?.text ?? "";
                        }
                        else if (currentOption == __instance.resolutionOption)
                        {
                            optionValue = __instance.resolutionValueOption?.text ?? "";
                        }
                        else if (currentOption == __instance.displayOption)
                        {
                            optionValue = __instance.displayValueOption?.text ?? "";
                        }
                        else if (currentOption == __instance.soundOption)
                        {
                            float volume = AsftuSaveManager.Instance.mainData.masterSoundVolume;
                            optionValue = $"{Mathf.RoundToInt(volume * 100)}%";
                        }
                        else if (currentOption == __instance.musicOption)
                        {
                            float volume = AsftuSaveManager.Instance.mainData.masterMusicVolume;
                            optionValue = $"{Mathf.RoundToInt(volume * 100)}%";
                        }
                        else if (currentOption == __instance.controlConfigButton)
                        {
                            optionValue = ""; // No value, just a button
                        }

                        string announcement = string.IsNullOrWhiteSpace(optionValue) ? optionName : $"{optionName}, {optionValue}";
                        TTSManager.AnnounceUI(announcement, interrupt: true);
                        MelonLogger.Msg($"[Settings] Announced: {announcement}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PauseMenuChildSettings_UpdateOption_Postfix: {ex.Message}");
            }
        }

        // Patch PauseMenuChildVerticalList.UpdateOption for general pause menu navigation
        [HarmonyPatch(typeof(PauseMenuChildVerticalList), "UpdateOption")]
        [HarmonyPostfix]
        public static void PauseMenuChildVerticalList_UpdateOption_Postfix(PauseMenuChildVerticalList __instance)
        {
            // Skip if this is PauseMenuChildSettings (it has its own patch above)
            if (__instance is PauseMenuChildSettings)
                return;

            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                var currentOptionField = typeof(PauseMenuChildVerticalList).GetField("currentOption", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (currentOptionField != null)
                {
                    var currentOption = currentOptionField.GetValue(__instance) as PauseMenuButtonUI;
                    if (currentOption != null && currentOption.Text != null)
                    {
                        string optionText = currentOption.Text.text;
                        if (!string.IsNullOrWhiteSpace(optionText))
                        {
                            TTSManager.AnnounceUI(optionText, interrupt: true);
                            MelonLogger.Msg($"[PauseMenu] Announced: {optionText}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PauseMenuChildVerticalList_UpdateOption_Postfix: {ex.Message}");
            }
        }

        // Patch settings value changes to announce new values
        [HarmonyPatch(typeof(PauseMenuChildSettings), "ChangeSFXVolume")]
        [HarmonyPostfix]
        public static void PauseMenuChildSettings_ChangeSFXVolume_Postfix(PauseMenuChildSettings __instance)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                float volume = AsftuSaveManager.Instance.mainData.masterSoundVolume;
                TTSManager.AnnounceUI($"{Mathf.RoundToInt(volume * 100)}%", interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ChangeSFXVolume_Postfix: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(PauseMenuChildSettings), "ChangeMusicVolume")]
        [HarmonyPostfix]
        public static void PauseMenuChildSettings_ChangeMusicVolume_Postfix(PauseMenuChildSettings __instance)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                float volume = AsftuSaveManager.Instance.mainData.masterMusicVolume;
                TTSManager.AnnounceUI($"{Mathf.RoundToInt(volume * 100)}%", interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ChangeMusicVolume_Postfix: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(PauseMenuChildSettings), "RefreshLanguageValue")]
        [HarmonyPostfix]
        public static void PauseMenuChildSettings_RefreshLanguageValue_Postfix(PauseMenuChildSettings __instance)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                string language = __instance.languageValueOption?.text ?? "";
                if (!string.IsNullOrWhiteSpace(language))
                {
                    TTSManager.AnnounceUI(language, interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in RefreshLanguageValue_Postfix: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(PauseMenuChildSettings), "RefreshDisplayValue")]
        [HarmonyPostfix]
        public static void PauseMenuChildSettings_RefreshDisplayValue_Postfix(PauseMenuChildSettings __instance)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                string displayMode = __instance.displayValueOption?.text ?? "";
                if (!string.IsNullOrWhiteSpace(displayMode))
                {
                    TTSManager.AnnounceUI(displayMode, interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in RefreshDisplayValue_Postfix: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(PauseMenuChildSettings), "RefreshResolutionValue")]
        [HarmonyPostfix]
        public static void PauseMenuChildSettings_RefreshResolutionValue_Postfix(PauseMenuChildSettings __instance)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                string resolution = __instance.resolutionValueOption?.text ?? "";
                if (!string.IsNullOrWhiteSpace(resolution))
                {
                    TTSManager.AnnounceUI(resolution, interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in RefreshResolutionValue_Postfix: {ex.Message}");
            }
        }

        // Patch ListView.SelectEntry to announce list navigation
        [HarmonyPatch(typeof(ListView), "SelectEntry", new Type[] { typeof(int), typeof(bool) })]
        [HarmonyPostfix]
        public static void ListView_SelectEntry_Postfix(ListView __instance, int index)
        {
            MelonLogger.Msg($"[DEBUG] ListView_SelectEntry called! Index: {index}, IsEnabled: {AccessibilityMod.IsEnabled}, AnnounceMenus: {AccessibilityMod.AnnounceMenus}");
            
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                if (index >= 0 && index < __instance.entries.Count)
                {
                    ListViewEntryInterface entry = __instance.entries[index];
                    MelonLogger.Msg($"[DEBUG] Entry found, checking for text...");
                    
                    if (entry != null)
                    {
                        GameObject entryObj = entry.GetGameObject();
                        if (entryObj != null)
                        {
                            MelonLogger.Msg($"[DEBUG] GameObject name: {entryObj.name}");
                            
                            // Skip SaveSlot items - they have their own dedicated patch
                            if (entry is PauseMenuChildSaveSlot)
                            {
                                MelonLogger.Msg("[ListView] Skipping SaveSlot - has dedicated patch");
                                return;
                            }
                            
                            // Skip control config entries - they have their own dedicated patch
                            if (__instance.GetComponentInParent<PauseMenuChildControlConfig>() != null)
                            {
                                MelonLogger.Msg("[ListView] Skipping ControlConfig entry - has dedicated patch");
                                return;
                            }
                            
                            // Try to get text from TextMeshProUGUI component
                            TextMeshProUGUI textComp = entryObj.GetComponent<TextMeshProUGUI>();
                            if (textComp != null && !string.IsNullOrWhiteSpace(textComp.text))
                            {
                                TTSManager.AnnounceUI(textComp.text, interrupt: true);
                                MelonLogger.Msg($"[ListView] Announced: {textComp.text}");
                                return;
                            }

                            // Try to get PauseMenuButtonUI
                            PauseMenuButtonUI buttonUI = entryObj.GetComponent<PauseMenuButtonUI>();
                            if (buttonUI != null && buttonUI.Text != null && !string.IsNullOrWhiteSpace(buttonUI.Text.text))
                            {
                                TTSManager.AnnounceUI(buttonUI.Text.text, interrupt: true);
                                MelonLogger.Msg($"[ListView] Announced button: {buttonUI.Text.text}");
                                return;
                            }

                            // Try to find any TextMeshProUGUI in children
                            TextMeshProUGUI childText = entryObj.GetComponentInChildren<TextMeshProUGUI>();
                            if (childText != null && !string.IsNullOrWhiteSpace(childText.text))
                            {
                                TTSManager.AnnounceUI(childText.text, interrupt: true);
                                MelonLogger.Msg($"[ListView] Announced from child: {childText.text}");
                                return;
                            }

                            // Fallback: announce object name
                            TTSManager.AnnounceUI($"Item {index + 1} of {__instance.entries.Count}", interrupt: true);
                            MelonLogger.Msg($"[ListView] Announced fallback: Item {index + 1}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ListView_SelectEntry_Postfix: {ex.Message}");
            }
        }

        // Patch MenuButton.OnPointerDown to announce button hover
        [HarmonyPatch(typeof(MenuButton), "OnPointerDown")]
        [HarmonyPostfix]
        public static void MenuButton_OnPointerDown_Postfix(MenuButton __instance)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                // Try PauseMenuButtonUI
                if (__instance is PauseMenuButtonUI buttonUI && buttonUI.Text != null)
                {
                    TTSManager.AnnounceUI(buttonUI.Text.text, interrupt: true);
                    MelonLogger.Msg($"[MenuButton] Announced: {buttonUI.Text.text}");
                    return;
                }

                // Try to get text from TextMeshProUGUI
                TextMeshProUGUI text = __instance.GetComponent<TextMeshProUGUI>();
                if (text != null && !string.IsNullOrWhiteSpace(text.text))
                {
                    TTSManager.AnnounceUI(text.text, interrupt: true);
                    MelonLogger.Msg($"[MenuButton] Announced from component: {text.text}");
                    return;
                }

                // Try children
                text = __instance.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null && !string.IsNullOrWhiteSpace(text.text))
                {
                    TTSManager.AnnounceUI(text.text, interrupt: true);
                    MelonLogger.Msg($"[MenuButton] Announced from child: {text.text}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in MenuButton_OnPointerDown_Postfix: {ex.Message}");
            }
        }

        // Patch BookMenuUI.Open to announce book menu opening
        [HarmonyPatch(typeof(BookMenuUI), "Open", new Type[] { typeof(bool) })]
        [HarmonyPostfix]
        public static void BookMenuUI_Open_Postfix(BookMenuUI __instance)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                TTSManager.AnnounceUI("Book menu opened", interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in BookMenuUI_Open_Postfix: {ex.Message}");
            }
        }

        // Patch BookMenuUI.Close to announce book menu closing
        [HarmonyPatch(typeof(BookMenuUI), nameof(BookMenuUI.Close))]
        [HarmonyPostfix]
        public static void BookMenuUI_Close_Postfix(BookMenuUI __instance)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                TTSManager.AnnounceUI("Book menu closed", interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in BookMenuUI_Close_Postfix: {ex.Message}");
            }
        }

        // Patch BookMenuUI.SetSectionTitle to announce section changes
        [HarmonyPatch(typeof(BookMenuUI), nameof(BookMenuUI.SetSectionTitle))]
        [HarmonyPostfix]
        public static void BookMenuUI_SetSectionTitle_Postfix(BookMenuUI __instance, LocalizationGlobalKey textKey)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                string sectionName = LocalizationManager.GetLocalizedGlobalValue(textKey);
                if (!string.IsNullOrWhiteSpace(sectionName))
                {
                    TTSManager.AnnounceUI($"Section: {sectionName}", interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in BookMenuUI_SetSectionTitle_Postfix: {ex.Message}");
            }
        }

        // Patch BookMenuSectionCollectible.SelectItem to announce collectible selection
        [HarmonyPatch(typeof(BookMenuSectionCollectible), nameof(BookMenuSectionCollectible.SelectItem))]
        [HarmonyPostfix]
        public static void BookMenuSectionCollectible_SelectItem_Postfix(BookMenuSectionCollectible __instance, int indexItem)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                if (__instance.nameText != null && __instance.descText != null)
                {
                    string name = __instance.nameText.text;
                    string desc = __instance.descText.text;
                    
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        string fullText = string.IsNullOrWhiteSpace(desc) 
                            ? name 
                            : $"{name}. {desc}";
                        
                        TTSManager.AnnounceUI(fullText, interrupt: true);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in BookMenuSectionCollectible_SelectItem_Postfix: {ex.Message}");
            }
        }

        // Patch BookMenuSectionInventory.SelectItem to announce inventory selection
        [HarmonyPatch(typeof(BookMenuSectionInventory), nameof(BookMenuSectionInventory.SelectItem))]
        [HarmonyPostfix]
        public static void BookMenuSectionInventory_SelectItem_Postfix(BookMenuSectionInventory __instance, int indexItem)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                if (__instance.nameText != null && __instance.descText != null)
                {
                    string name = __instance.nameText.text;
                    string desc = __instance.descText.text;
                    
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        string fullText = string.IsNullOrWhiteSpace(desc) 
                            ? name 
                            : $"{name}. {desc}";
                        
                        TTSManager.AnnounceUI(fullText, interrupt: true);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in BookMenuSectionInventory_SelectItem_Postfix: {ex.Message}");
            }
        }

        // Patch BookMenuSectionMap.SelectArea to announce map area selection
        [HarmonyPatch(typeof(BookMenuSectionMap), nameof(BookMenuSectionMap.SelectArea))]
        [HarmonyPostfix]
        public static void BookMenuSectionMap_SelectArea_Postfix(BookMenuSectionMap __instance, BookMenuSectionMapNavigation area)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                if (__instance.label != null)
                {
                    string areaName = __instance.label.text;
                    if (!string.IsNullOrWhiteSpace(areaName))
                    {
                        TTSManager.AnnounceUI($"Map location: {areaName}", interrupt: true);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in BookMenuSectionMap_SelectArea_Postfix: {ex.Message}");
            }
        }

        private static int _lastInteractionMenuIndex = -1;

        // Patch InteractionMenu.Update to announce interaction options when navigating
        [HarmonyPatch(typeof(InteractionMenu), "Update")]
        [HarmonyPostfix]
        public static void InteractionMenu_Update_Postfix(InteractionMenu __instance)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                if (!__instance.IsOpen)
                {
                    _lastInteractionMenuIndex = -1;
                    return;
                }

                int currentIndex = __instance.CurrentIndex;
                
                // Only announce if index changed
                if (currentIndex != _lastInteractionMenuIndex)
                {
                    _lastInteractionMenuIndex = currentIndex;
                    
                    var currentInteraction = __instance.CurrentInteraction;
                    if (currentInteraction != null)
                    {
                        var nameField = currentInteraction.GetType().GetField("name");
                        if (nameField != null)
                        {
                            string optionName = nameField.GetValue(currentInteraction) as string;
                            if (!string.IsNullOrWhiteSpace(optionName) && optionName != "None")
                            {
                                TTSManager.AnnounceUI(optionName, interrupt: true);
                                MelonLogger.Msg($"[InteractionMenu] Announced: {optionName}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in InteractionMenu_Update_Postfix: {ex.Message}");
            }
        }

        // Patch PauseMenuChildSaveSlot.SetSelected to announce save slot selection
        // DISABLED: This was causing duplicate announcements with RefreshSlot patch
        // [HarmonyPatch(typeof(PauseMenuChildSaveSlot), nameof(PauseMenuChildSaveSlot.SetSelected))]
        // [HarmonyPostfix]
        public static void PauseMenuChildSaveSlot_SetSelected_Postfix_DISABLED(PauseMenuChildSaveSlot __instance, bool selected)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus || !selected)
                return;

            try
            {
                if (__instance.indexText != null)
                {
                    string saveInfo = $"Save slot {__instance.indexText.text}";
                    
                    if (__instance.chapterText != null && !string.IsNullOrWhiteSpace(__instance.chapterText.text))
                    {
                        saveInfo += $", {__instance.chapterText.text}";
                    }
                    
                    if (__instance.playTimeText != null && !string.IsNullOrWhiteSpace(__instance.playTimeText.text))
                    {
                        saveInfo += $", Play time: {__instance.playTimeText.text}";
                    }

                    TTSManager.AnnounceUI(saveInfo, interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PauseMenuChildSaveSlot_SetSelected_Postfix: {ex.Message}");
            }
        }

        // Track last announced items for SaveLoad and ControlConfig to prevent duplicate announcements
        private static object _lastAnnouncedSaveLoadItem = null;
        private static object _lastAnnouncedControlItem = null;

        // Patch PauseMenuChildSaveLoad to announce save slots properly (Load Game menu)
        [HarmonyPatch(typeof(PauseMenuChildSaveLoad), "RefreshSlot")]
        [HarmonyPostfix]
        public static void PauseMenuChildSaveLoad_RefreshSlot_Postfix(PauseMenuChildSaveLoad __instance, int index)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                // Only announce if this menu is currently active
                if (!__instance.gameObject.activeInHierarchy)
                    return;

                // Get the ListView
                var listViewField = typeof(PauseMenuChildSaveLoad).GetField("listView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (listViewField != null)
                {
                    var listView = listViewField.GetValue(__instance) as ListView;
                    if (listView != null && listView.SelectedEntryIndex >= 0 && listView.SelectedEntryIndex < listView.entries.Count)
                    {
                        var selectedEntry = listView.entries[listView.SelectedEntryIndex];
                        
                        // Prevent duplicate announcement
                        if (_lastAnnouncedSaveLoadItem == selectedEntry)
                            return;
                        
                        _lastAnnouncedSaveLoadItem = selectedEntry;

                        var saveSlot = selectedEntry as PauseMenuChildSaveSlot;
                        if (saveSlot != null && saveSlot.indexText != null)
                        {
                            string saveInfo = $"Slot {saveSlot.indexText.text}";
                            
                            if (saveSlot.chapterText != null && !string.IsNullOrWhiteSpace(saveSlot.chapterText.text))
                            {
                                saveInfo += $", {saveSlot.chapterText.text}";
                            }
                            
                            if (saveSlot.playTimeText != null && !string.IsNullOrWhiteSpace(saveSlot.playTimeText.text))
                            {
                                saveInfo += $", {saveSlot.playTimeText.text}";
                            }

                            TTSManager.AnnounceUI(saveInfo, interrupt: true);
                            MelonLogger.Msg($"[SaveLoad] Announced: {saveInfo}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PauseMenuChildSaveLoad_RefreshSlot_Postfix: {ex.Message}");
            }
        }

        // Patch PauseMenuChildControlConfig to announce control bindings properly (Controls menu)
        [HarmonyPatch(typeof(PauseMenuChildControlConfig), "ListView_onSelectEntry")]
        [HarmonyPostfix]
        public static void PauseMenuChildControlConfig_ListViewSelect_Postfix(PauseMenuChildControlConfig __instance, ListViewEntryInterface newEntry, ListViewEntryInterface previousEntry)
        {
            MelonLogger.Msg("[ControlConfig] ListView_onSelectEntry called!");
            
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
            {
                MelonLogger.Msg("[ControlConfig] Disabled or menus not announced");
                return;
            }

            try
            {
                if (newEntry == null)
                {
                    MelonLogger.Msg("[ControlConfig] newEntry is null");
                    return;
                }

                // Prevent duplicate announcement
                if (_lastAnnouncedControlItem == newEntry)
                {
                    MelonLogger.Msg("[ControlConfig] Duplicate entry, skipping");
                    return;
                }
                
                _lastAnnouncedControlItem = newEntry;

                var gameObject = newEntry.GetGameObject();
                MelonLogger.Msg($"[ControlConfig] GameObject: {gameObject?.name}");
                
                if (gameObject == null)
                {
                    MelonLogger.Msg("[ControlConfig] GameObject is null");
                    return;
                }
                
                // Get action name from the ActionLabel text, not GameObject name
                var actionLabelTransform = gameObject.transform.Find("ActionLabel");
                string actionName = gameObject.name; // Fallback to GameObject name
                
                if (actionLabelTransform != null)
                {
                    var actionLabelText = actionLabelTransform.GetComponent<TMPro.TextMeshProUGUI>();
                    if (actionLabelText != null && !string.IsNullOrWhiteSpace(actionLabelText.text))
                    {
                        actionName = actionLabelText.text;
                    }
                }
                
                MelonLogger.Msg($"[ControlConfig] Action name: {actionName}");
                
                // Get the current key binding
                string keyBinding = GetCurrentKeyBinding(__instance, newEntry);
                MelonLogger.Msg($"[ControlConfig] Key binding: '{keyBinding}'");
                
                string announcement = !string.IsNullOrWhiteSpace(keyBinding)
                    ? $"{actionName}, {keyBinding}"
                    : actionName;

                TTSManager.AnnounceUI(announcement, interrupt: true);
                MelonLogger.Msg($"[ControlConfig] Announced: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ControlConfig] Error in PauseMenuChildControlConfig_ListViewSelect_Postfix: {ex.Message}");
                MelonLogger.Error($"[ControlConfig] Stack: {ex.StackTrace}");
            }
        }

        // Helper to get current key binding text for a control
        private static string GetCurrentKeyBinding(PauseMenuChildControlConfig instance, ListViewEntryInterface entry)
        {
            try
            {
                var gameObject = entry.GetGameObject();
                MelonLogger.Msg($"[ControlConfig] GetCurrentKeyBinding for: {gameObject?.name}");
                
                if (gameObject == null)
                {
                    MelonLogger.Msg("[ControlConfig] GameObject is null");
                    return "";
                }
                
                var configButtonUI = gameObject.GetComponent<PauseMenuButtonControlConfigUI>();
                if (configButtonUI == null)
                {
                    MelonLogger.Msg("[ControlConfig] PauseMenuButtonControlConfigUI not found");
                    return "";
                }
                
                MelonLogger.Msg($"[ControlConfig] buttonLegend: {configButtonUI.buttonLegend != null}");
                
                if (configButtonUI.buttonLegend != null)
                {
                    var buttonLegend = configButtonUI.buttonLegend;
                    var keyType = buttonLegend.Key;
                    MelonLogger.Msg($"[ControlConfig] ButtonLegend Key: {keyType}");
                    
                    // Refresh the button legend to ensure current binding is displayed
                    buttonLegend.Refresh();
                    
                    // Look specifically for a GameObject named "ButtonLabel" which contains the key text (for keyboard)
                    Transform buttonLabelTransform = buttonLegend.transform.Find("ButtonLabel");
                    if (buttonLabelTransform != null)
                    {
                        var buttonLabelText = buttonLabelTransform.GetComponent<TMPro.TextMeshProUGUI>();
                        if (buttonLabelText != null && !string.IsNullOrWhiteSpace(buttonLabelText.text))
                        {
                            MelonLogger.Msg($"[ControlConfig] Found ButtonLabel text: {buttonLabelText.text}");
                            return buttonLabelText.text;
                        }
                    }
                    
                    // For controller or when ButtonLabel doesn't exist, generate button name from binding
                    MelonLogger.Msg("[ControlConfig] No ButtonLabel found, getting button name from binding");
                    string buttonName = GetButtonNameForControlConfig(keyType);
                    if (!string.IsNullOrWhiteSpace(buttonName))
                    {
                        MelonLogger.Msg($"[ControlConfig] Got button name from binding: {buttonName}");
                        return buttonName;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ControlConfig] Error: {ex.Message}");
                MelonLogger.Warning($"[ControlConfig] Stack: {ex.StackTrace}");
            }
            
            MelonLogger.Msg("[ControlConfig] No button text found");
            return "";
        }

        // Get user-friendly button name for control config (same logic as InteractablePatches)
        private static string GetButtonNameForControlConfig(PlayerKeybinding.KeyBindingType keyType)
        {
            try
            {
                var playerKeybind = InputManager.Instance?.playerkeybind;
                if (playerKeybind == null)
                    return "";

                var action = playerKeybind.GetKeyBinding(keyType);
                if (action == null)
                    return "";

                // Get the device class
                var deviceClass = playerKeybind.LastDeviceClass;
                if (deviceClass == InControl.InputDeviceClass.Mouse)
                    deviceClass = InControl.InputDeviceClass.Keyboard;

                // Find the binding for current device
                foreach (var binding in action.UnfilteredBindings)
                {
                    if (binding.DeviceClass == deviceClass)
                    {
                        if (deviceClass == InControl.InputDeviceClass.Keyboard)
                        {
                            var keyBinding = binding as InControl.KeyBindingSource;
                            if (keyBinding != null)
                            {
                                var key = keyBinding.Control.GetInclude(0);
                                // Use localized keyboard key name
                                string localizedKey = LocalizationManager.GetLocalizedValue(LocalizationCategory.Keyboard, key.ToString());
                                return !string.IsNullOrWhiteSpace(localizedKey) ? localizedKey : GetFriendlyKeyNameForControlConfig(key);
                            }
                        }
                        else
                        {
                            // Controller button - get friendly name
                            var deviceBinding = binding as InControl.DeviceBindingSource;
                            if (deviceBinding != null)
                            {
                                return GetFriendlyControllerButtonNameForControlConfig(deviceBinding.Control);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting button name for control config: {ex.Message}");
            }

            return "";
        }

        // Convert key enum to friendly name for control config
        private static string GetFriendlyKeyNameForControlConfig(InControl.Key key)
        {
            switch (key)
            {
                case InControl.Key.UpArrow: return "Up Arrow";
                case InControl.Key.DownArrow: return "Down Arrow";
                case InControl.Key.LeftArrow: return "Left Arrow";
                case InControl.Key.RightArrow: return "Right Arrow";
                case InControl.Key.Space: return "Space";
                case InControl.Key.Return: return "Enter";
                case InControl.Key.Escape: return "Escape";
                case InControl.Key.LeftShift: return "Left Shift";
                case InControl.Key.RightShift: return "Right Shift";
                case InControl.Key.LeftControl: return "Left Control";
                case InControl.Key.RightControl: return "Right Control";
                case InControl.Key.Tab: return "Tab";
                default:
                    // For letters and numbers, just return the key name
                    string keyName = key.ToString();
                    if (keyName.StartsWith("Key"))
                        keyName = keyName.Substring(3);
                    return keyName;
            }
        }

        // Convert controller button to friendly name for control config
        private static string GetFriendlyControllerButtonNameForControlConfig(InControl.InputControlType control)
        {
            switch (control)
            {
                case InControl.InputControlType.Action1: return "A button";
                case InControl.InputControlType.Action2: return "B button";
                case InControl.InputControlType.Action3: return "X button";
                case InControl.InputControlType.Action4: return "Y button";
                case InControl.InputControlType.DPadUp: return "D-pad Up";
                case InControl.InputControlType.DPadDown: return "D-pad Down";
                case InControl.InputControlType.DPadLeft: return "D-pad Left";
                case InControl.InputControlType.DPadRight: return "D-pad Right";
                case InControl.InputControlType.LeftBumper: return "Left Bumper";
                case InControl.InputControlType.RightBumper: return "Right Bumper";
                case InControl.InputControlType.LeftTrigger: return "Left Trigger";
                case InControl.InputControlType.RightTrigger: return "Right Trigger";
                case InControl.InputControlType.LeftStickButton: return "Left Stick";
                case InControl.InputControlType.RightStickButton: return "Right Stick";
                default:
                    return control.ToString().Replace("Action", "Button ");
            }
        }

        // Clear tracking when menus close
        [HarmonyPatch(typeof(PauseMenuChildSaveLoad), "Close")]
        [HarmonyPostfix]
        public static void PauseMenuChildSaveLoad_Close_Postfix()
        {
            _lastAnnouncedSaveLoadItem = null;
        }

        [HarmonyPatch(typeof(PauseMenuChildControlConfig), "Close")]
        [HarmonyPostfix]
        public static void PauseMenuChildControlConfig_Close_Postfix()
        {
            _lastAnnouncedControlItem = null;
        }

        // Patch PopupUIController.Show to announce the popup message and buttons
        [HarmonyPatch(typeof(PopupUIController), nameof(PopupUIController.Show))]
        [HarmonyPostfix]
        public static void PopupUIController_Show_Postfix(PopupUIController __instance)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                // Announce the message text
                if (__instance.messageText != null && !string.IsNullOrWhiteSpace(__instance.messageText.text))
                {
                    TTSManager.Speak(__instance.messageText.text, interrupt: true, priority: 10);
                }
                
                // Announce the selected button after a short delay
                UnityEngine.MonoBehaviour coroutineHost = __instance;
                coroutineHost.StartCoroutine(AnnounceSelectedButtonAfterDelay(__instance, 0.3f));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PopupUIController_Show_Postfix: {ex.Message}");
            }
        }

        // Patch PopupUIController.SetSelectedButton to announce button changes
        [HarmonyPatch(typeof(PopupUIController), "SetSelectedButton")]
        [HarmonyPostfix]
        public static void PopupUIController_SetSelectedButton_Postfix(PopupUIController __instance)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                if (__instance.selectedButton != null)
                {
                    var buttonText = __instance.selectedButton.GetComponentInChildren<TextMeshProUGUI>();
                    if (buttonText != null && !string.IsNullOrWhiteSpace(buttonText.text))
                    {
                        TTSManager.AnnounceUI(buttonText.text, interrupt: true);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PopupUIController_SetSelectedButton_Postfix: {ex.Message}");
            }
        }

        // Helper coroutine to announce the selected button after popup appears
        private static System.Collections.IEnumerator AnnounceSelectedButtonAfterDelay(PopupUIController popup, float delay)
        {
            yield return new UnityEngine.WaitForSeconds(delay);
            
            if (popup != null && popup.selectedButton != null)
            {
                var buttonText = popup.selectedButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null && !string.IsNullOrWhiteSpace(buttonText.text))
                {
                    TTSManager.AnnounceUI(buttonText.text, interrupt: false);
                }
            }
        }

        // Patch BookMenuSectionQuest.SelectScrollbar to announce which list is selected
        [HarmonyPatch(typeof(BookMenuSectionQuest), "SelectScrollbar")]
        [HarmonyPostfix]
        public static void BookMenuSectionQuest_SelectScrollbar_Postfix(BookMenuSectionQuest __instance, UnityEngine.UI.Scrollbar scrollbar)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                // Determine which list was selected
                string listName = "";
                if (scrollbar == __instance.todoListVerticalScroll)
                {
                    listName = "To-Do List";
                }
                else if (scrollbar == __instance.bucketListVerticalScroll)
                {
                    listName = "Atma and Raya's Epic Bucket List";
                }

                if (!string.IsNullOrWhiteSpace(listName))
                {
                    TTSManager.AnnounceUI(listName, interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in BookMenuSectionQuest_SelectScrollbar_Postfix: {ex.Message}");
            }
        }

        // Track scroll position to announce items
        private static float _lastBucketListScrollPosition = -1f;
        private static float _lastTodoListScrollPosition = -1f;

        // Patch BookMenuSectionQuest.UpdateDefault to announce visible quest items
        [HarmonyPatch(typeof(BookMenuSectionQuest), nameof(BookMenuSectionQuest.UpdateDefault))]
        [HarmonyPostfix]
        public static void BookMenuSectionQuest_UpdateDefault_Postfix(BookMenuSectionQuest __instance)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                // Get the selected scrollbar via reflection
                var selectedScrollbarField = typeof(BookMenuSectionQuest).GetField("selectedScrollbar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (selectedScrollbarField == null)
                    return;

                var selectedScrollbar = selectedScrollbarField.GetValue(__instance) as UnityEngine.UI.Scrollbar;
                if (selectedScrollbar == null)
                    return;

                // Check which list is selected and if scroll position changed significantly
                RectTransform container = null;
                float currentScrollPos = 0f;
                float lastScrollPos = 0f;
                
                if (selectedScrollbar == __instance.todoListVerticalScroll)
                {
                    container = __instance.todoListContainer;
                    currentScrollPos = container.anchoredPosition.y;
                    lastScrollPos = _lastTodoListScrollPosition;
                }
                else if (selectedScrollbar == __instance.bucketListVerticalScroll)
                {
                    container = __instance.bucketListContainer;
                    currentScrollPos = container.anchoredPosition.y;
                    lastScrollPos = _lastBucketListScrollPosition;
                }

                if (container == null)
                    return;

                // Check if scroll position changed enough (at least 50 units)
                if (Mathf.Abs(currentScrollPos - lastScrollPos) > 50f)
                {
                    // Update last position
                    if (selectedScrollbar == __instance.todoListVerticalScroll)
                    {
                        _lastTodoListScrollPosition = currentScrollPos;
                    }
                    else
                    {
                        _lastBucketListScrollPosition = currentScrollPos;
                    }

                    // Find the most visible quest item
                    AnnounceVisibleQuestItem(container);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in BookMenuSectionQuest_UpdateDefault_Postfix: {ex.Message}");
            }
        }

        // Helper method to announce the most visible quest item
        private static void AnnounceVisibleQuestItem(RectTransform container)
        {
            try
            {
                var questItems = container.GetComponentsInChildren<BookMenuSectionQuest_TodoListText>();
                if (questItems == null || questItems.Length == 0)
                    return;

                // Find the item closest to the center of the viewport
                BookMenuSectionQuest_TodoListText mostVisibleItem = null;
                float minDistance = float.MaxValue;

                foreach (var item in questItems)
                {
                    if (item.gameObject.activeInHierarchy)
                    {
                        float itemY = item.transform.position.y;
                        float screenCenterY = UnityEngine.Screen.height / 2f;
                        float distance = Mathf.Abs(itemY - screenCenterY);

                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            mostVisibleItem = item;
                        }
                    }
                }

                if (mostVisibleItem != null && mostVisibleItem.questSaveData != null)
                {
                    var questData = mostVisibleItem.questSaveData.GetQuestData();
                    if (questData != null)
                    {
                        string questText = questData.GetText();
                        if (!string.IsNullOrWhiteSpace(questText))
                        {
                            // Check if completed
                            bool isCompleted = questData.CheckComplete(mostVisibleItem.questSaveData.progress);
                            string announcement = isCompleted ? $"{questText}, completed" : questText;
                            
                            TTSManager.AnnounceUI(announcement, interrupt: true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in AnnounceVisibleQuestItem: {ex.Message}");
            }
        }

        // Track last selected menu button for Fungus MenuDialog
        private static GameObject _lastSelectedMenuButton = null;

        // Patch EventSystem.SetSelectedGameObject to detect menu option navigation
        [HarmonyPatch(typeof(EventSystem), nameof(EventSystem.SetSelectedGameObject), new System.Type[] { typeof(GameObject) })]
        [HarmonyPostfix]
        public static void EventSystem_SetSelectedGameObject_Postfix(EventSystem __instance, GameObject selected)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                if (selected == null || selected == _lastSelectedMenuButton)
                    return;

                _lastSelectedMenuButton = selected;

                // Check if this is a MenuDialog button
                var button = selected.GetComponent<Button>();
                if (button != null)
                {
                    // Check if parent or grandparent has MenuDialog component
                    var menuDialog = button.GetComponentInParent<Fungus.MenuDialog>();
                    if (menuDialog != null && menuDialog.IsActive())
                    {
                        // Get button text
                        var buttonText = button.GetComponentInChildren<Text>();
                        if (buttonText != null && !string.IsNullOrWhiteSpace(buttonText.text))
                        {
                            TTSManager.AnnounceUI(buttonText.text, interrupt: true);
                            MelonLogger.Msg($"[MenuDialog] Selected option: {buttonText.text}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in EventSystem_SetSelectedGameObject_Postfix: {ex.Message}");
            }
        }

        // Patch MenuDialog.AddOption to announce when first option is shown
        [HarmonyPatch(typeof(Fungus.MenuDialog), "AddOption", new System.Type[] { typeof(string), typeof(bool), typeof(bool), typeof(UnityEngine.Events.UnityAction) })]
        [HarmonyPostfix]
        public static void MenuDialog_AddOption_Postfix(Fungus.MenuDialog __instance, string text, bool interactable, bool hideOption, bool __result)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                // Only announce the first option when menu becomes active
                if (__result && __instance.DisplayedOptionsCount == 1 && !hideOption && interactable)
                {
                    TTSManager.AnnounceUI("Menu options available", interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in MenuDialog_AddOption_Postfix: {ex.Message}");
            }
        }

        // Patch SpaceDialogMenuBox.RefreshOption to announce menu option changes
        [HarmonyPatch(typeof(SpaceDialogMenuBox), "RefreshOption")]
        [HarmonyPostfix]
        public static void SpaceDialogMenuBox_RefreshOption_Postfix(SpaceDialogMenuBox __instance)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceMenus)
                return;

            try
            {
                // Get the currently selected option text
                var optionDisplayTextListField = typeof(SpaceDialogMenuBox).GetField("optionDisplayTextList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var indexSelectedField = typeof(SpaceDialogMenuBox).GetField("indexSelected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (optionDisplayTextListField != null && indexSelectedField != null)
                {
                    var optionList = optionDisplayTextListField.GetValue(__instance) as System.Collections.Generic.List<string>;
                    int index = (int)indexSelectedField.GetValue(__instance);

                    if (optionList != null && index >= 0 && index < optionList.Count)
                    {
                        string optionText = optionList[index];
                        if (!string.IsNullOrWhiteSpace(optionText))
                        {
                            TTSManager.AnnounceUI(optionText, interrupt: true);
                            MelonLogger.Msg($"[SpaceDialogMenuBox] Announced option {index + 1} of {optionList.Count}: {optionText}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in SpaceDialogMenuBox_RefreshOption_Postfix: {ex.Message}");
            }
        }

        // ========== PATCHES TO STOP INTERACTABLE AUDIO WHEN MENUS/DIALOGS OPEN ==========

        // Stop audio when pause menu opens
        [HarmonyPatch(typeof(PauseMenu), "Open")]
        [HarmonyPostfix]
        public static void PauseMenu_Open_Postfix()
        {
            try
            {
                InteractableAudioManager.Instance?.StopInteractableAudio();
                MelonLogger.Msg("[PauseMenu] Stopped interactable audio");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PauseMenu_Open_Postfix: {ex.Message}");
            }
        }

        // Stop audio when book menu opens
        [HarmonyPatch(typeof(BookMenuUI), "Open", new Type[] { typeof(bool) })]
        [HarmonyPostfix]
        public static void BookMenuUI_Open_Postfix()
        {
            try
            {
                InteractableAudioManager.Instance?.StopInteractableAudio();
                MelonLogger.Msg("[BookMenuUI] Stopped interactable audio");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in BookMenuUI_Open_Postfix: {ex.Message}");
            }
        }

        // Stop audio when interaction menu opens
        [HarmonyPatch(typeof(InteractionMenu), "Open")]
        [HarmonyPostfix]
        public static void InteractionMenu_Open_Postfix()
        {
            try
            {
                InteractableAudioManager.Instance?.StopInteractableAudio();
                MelonLogger.Msg("[InteractionMenu] Stopped interactable audio");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in InteractionMenu_Open_Postfix: {ex.Message}");
            }
        }

        // Stop audio when dialogue starts
        [HarmonyPatch(typeof(Fungus.SayDialog), "SetActive")]
        [HarmonyPostfix]
        public static void SayDialog_SetActive_Postfix(bool state)
        {
            try
            {
                if (state) // Only stop when dialogue becomes active
                {
                    InteractableAudioManager.Instance?.StopInteractableAudio();
                    MelonLogger.Msg("[SayDialog] Stopped interactable audio");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in SayDialog_SetActive_Postfix: {ex.Message}");
            }
        }
    }
}
