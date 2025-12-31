using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace AsftuAccessibilityMod.Patches
{
    [HarmonyPatch]
    public static class PasswordDialogPatches
    {
        private static int lastIndexSelected = -1;
        private static int lastDigitValue = -1;
        private static bool dialogActive = false;

        [HarmonyPatch(typeof(PasswordDialogBox), "Show")]
        [HarmonyPostfix]
        public static void Show_Postfix(int password, PasswordDialogBox __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                dialogActive = true;
                lastIndexSelected = -1;
                lastDigitValue = -1;
                
                string passwordStr = password.ToString();
                int digitCount = passwordStr.Length;
                string message = "Password entry. " + digitCount + " digits. Use left and right to move between digits, up and down to change values, confirm to submit, cancel to close.";
                TTSManager.AnnounceUI(message, true);
                
                __instance.StartCoroutine(AnnounceInitialDigit(__instance));
            }
            catch (Exception ex)
            {
                MelonLogger.Error("Error in PasswordDialogBox.Show_Postfix: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(PasswordDialogBox), "Hide")]
        [HarmonyPostfix]
        public static void Hide_Postfix()
        {
            dialogActive = false;
            lastIndexSelected = -1;
            lastDigitValue = -1;
        }

        [HarmonyPatch(typeof(PasswordDialogBox), "Update")]
        [HarmonyPostfix]
        public static void Update_Postfix(PasswordDialogBox __instance)
        {
            if (!AccessibilityMod.IsEnabled || !dialogActive)
                return;

            try
            {
                FieldInfo indexField = typeof(PasswordDialogBox).GetField("indexSelected", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo digitsField = typeof(PasswordDialogBox).GetField("digits", BindingFlags.NonPublic | BindingFlags.Instance);

                if (indexField == null || digitsField == null)
                    return;

                int currentIndex = (int)indexField.GetValue(__instance);
                List<int> digits = digitsField.GetValue(__instance) as List<int>;

                if (digits == null || currentIndex < 0 || currentIndex >= digits.Count)
                    return;

                int currentDigit = digits[currentIndex];

                if (currentIndex != lastIndexSelected)
                {
                    lastIndexSelected = currentIndex;
                    lastDigitValue = currentDigit;
                    string message = "Digit " + (currentIndex + 1) + ", value " + currentDigit;
                    TTSManager.Speak(message, true);
                }
                else if (currentDigit != lastDigitValue)
                {
                    lastDigitValue = currentDigit;
                    string message = currentDigit.ToString();
                    TTSManager.Speak(message, true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error("Error in PasswordDialogBox.Update_Postfix: " + ex.Message);
            }
        }

        private static IEnumerator AnnounceInitialDigit(PasswordDialogBox instance)
        {
            yield return new WaitForSecondsRealtime(0.4f);

            if (AccessibilityMod.IsEnabled && dialogActive)
            {
                try
                {
                    FieldInfo indexField = typeof(PasswordDialogBox).GetField("indexSelected", BindingFlags.NonPublic | BindingFlags.Instance);
                    FieldInfo digitsField = typeof(PasswordDialogBox).GetField("digits", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (indexField != null && digitsField != null)
                    {
                        int currentIndex = (int)indexField.GetValue(instance);
                        List<int> digits = digitsField.GetValue(instance) as List<int>;

                        if (digits != null && currentIndex >= 0 && currentIndex < digits.Count)
                        {
                            int currentDigit = digits[currentIndex];
                            lastIndexSelected = currentIndex;
                            lastDigitValue = currentDigit;
                            string message = "Digit " + (currentIndex + 1) + ", value " + currentDigit;
                            TTSManager.Speak(message, false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error("Error in AnnounceInitialDigit: " + ex.Message);
                }
            }
        }
    }
}
