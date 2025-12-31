using System;
using HarmonyLib;
using MelonLoader;
using TMPro;
using Fungus;

namespace AsftuAccessibilityMod.Patches
{
    /// <summary>
    /// Patches for the dialogue system to announce character dialogue and story text
    /// </summary>
    [HarmonyPatch]
    public static class DialoguePatches
    {
        private static string _lastDialogueText = "";
        private static float _lastDialogueTime = 0f;
        private static readonly float _dialogueThreshold = 0.5f;

        // Remove SetCharacterName patch - we don't need to announce character names

        // Patch Writer.Write to announce dialogue text as it's written
        [HarmonyPatch(typeof(Writer), nameof(Writer.Write))]
        [HarmonyPrefix]
        public static void Writer_Write_Prefix(Writer __instance, string content)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceDialogue)
                return;

            try
            {
                if (!string.IsNullOrWhiteSpace(content))
                {
                    // Check if this is a duplicate dialogue
                    float currentTime = UnityEngine.Time.unscaledTime;
                    if (content == _lastDialogueText && (currentTime - _lastDialogueTime) < _dialogueThreshold)
                    {
                        MelonLogger.Msg($"[Dialogue] Skipping duplicate: {content}");
                        return;
                    }

                    _lastDialogueText = content;
                    _lastDialogueTime = currentTime;

                    // Get character name if available
                    string characterName = "";
                    SayDialog sayDialog = SayDialog.ActiveSayDialog;
                    if (sayDialog != null && sayDialog.NameText != null)
                    {
                        characterName = sayDialog.NameText.text;
                    }

                    // Announce character name first, then dialogue
                    if (!string.IsNullOrWhiteSpace(characterName))
                    {
                        TTSManager.Speak(characterName, interrupt: true, priority: 10);
                        TTSManager.Speak(content, interrupt: false, priority: 10);
                        MelonLogger.Msg($"[Dialogue] Announced: {characterName} - {content}");
                    }
                    else
                    {
                        TTSManager.Speak(content, interrupt: true, priority: 10);
                        MelonLogger.Msg($"[Dialogue] Announced: {content}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in Writer_Write_Prefix: {ex.Message}");
            }
        }

        // Patch BaseSpaceSay.StartSay for custom dialogue system
        [HarmonyPatch(typeof(BaseSpaceSay), nameof(BaseSpaceSay.StartSay))]
        [HarmonyPrefix]
        public static void BaseSpaceSay_StartSay_Prefix(BaseSpaceSay __instance, AsftuCharacterName characterName)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceDialogue)
                return;

            try
            {
                string dialogueText = __instance.storyText;

                if (!string.IsNullOrWhiteSpace(dialogueText))
                {
                    // Check if this is a duplicate dialogue
                    float currentTime = UnityEngine.Time.unscaledTime;
                    if (dialogueText == _lastDialogueText && (currentTime - _lastDialogueTime) < _dialogueThreshold)
                    {
                        MelonLogger.Msg($"[BaseSpaceSay] Skipping duplicate: {dialogueText}");
                        return;
                    }

                    _lastDialogueText = dialogueText;
                    _lastDialogueTime = currentTime;

                    // Get clean character name
                    string charName = GetCleanCharacterName(characterName);

                    // Announce character name first, then dialogue
                    if (!string.IsNullOrWhiteSpace(charName))
                    {
                        TTSManager.Speak(charName, interrupt: true, priority: 10);
                        TTSManager.Speak(dialogueText, interrupt: false, priority: 10);
                        MelonLogger.Msg($"[BaseSpaceSay] Announced: {charName} - {dialogueText}");
                    }
                    else
                    {
                        TTSManager.Speak(dialogueText, interrupt: true, priority: 10);
                        MelonLogger.Msg($"[BaseSpaceSay] Announced: {dialogueText}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in BaseSpaceSay_StartSay_Prefix: {ex.Message}");
            }
        }

        // Helper to get clean character name from AsftuCharacterName
        private static string GetCleanCharacterName(AsftuCharacterName characterName)
        {
            if (characterName == null)
                return "";

            try
            {
                // Use the built-in GetName method which handles localization
                string name = characterName.GetName();
                if (!string.IsNullOrWhiteSpace(name))
                    return name;

                // Fallback to base name
                return characterName.name;
            }
            catch
            {
                return "";
            }
        }

        // Patch LogDialogBoxUI.Show to announce dialogue log entries
        [HarmonyPatch(typeof(LogDialogBoxUI), nameof(LogDialogBoxUI.Show))]
        [HarmonyPostfix]
        public static void LogDialogBoxUI_Show_Postfix(LogDialogBoxUI __instance)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceDialogue)
                return;

            try
            {
                if (__instance.dialogText != null)
                {
                    string dialogue = __instance.dialogText.text;
                    
                    if (!string.IsNullOrWhiteSpace(dialogue))
                    {
                        // Announce only the dialogue text, without character name
                        TTSManager.Speak(dialogue, interrupt: false, priority: 10);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in LogDialogBoxUI_Show_Postfix: {ex.Message}");
            }
        }

        // Patch ShowMinigameTitleText to announce minigame titles
        [HarmonyPatch(typeof(ShowMinigameTitleText), "ShowMinigameTitleTextCoroutine")]
        [HarmonyPostfix]
        public static void ShowMinigameTitleText_Postfix(ShowMinigameTitleText __instance)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceDialogue)
                return;

            try
            {
                if (__instance.textKey != null)
                {
                    string text = LocalizationManager.GetLocalizedValue(__instance.textKey);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        TTSManager.Speak($"Minigame: {text}", interrupt: true, priority: 10);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ShowMinigameTitleText_Postfix: {ex.Message}");
            }
        }
    }
}
