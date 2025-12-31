using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using Fungus;
using com.mojiken.asftu;

namespace AsftuAccessibilityMod.Patches
{
    /// <summary>
    /// Patches for the Karate Boy comic book reading in Chapter 4
    /// Announces page content and counts attacks for the passcode puzzle
    /// </summary>
    [HarmonyPatch]
    public static class ComicBookPatches
    {
        // Track comic state
        private static bool _isComicOpen = false;
        private static int _currentPage = -1;
        private static float _lastPageAnnounceTime = 0f;
        private static Dictionary<string, int> _loggedVariables = new Dictionary<string, int>();

        // TOTAL attack counts across all pages of Karate Boy Chapter 1043
        // Note: Karate chops do NOT count!
        // Source: https://www.gamepur.com/guides/how-to-solve-the-senseis-dojo-and-karate-boy-puzzle-in-a-space-for-the-unbound
        private const int TOTAL_ELBOWS = 3;
        private const int TOTAL_KICKS = 5;
        private const int TOTAL_PUNCHES = 7;
        // Passcode: 3-5-7 (Elbows-Kicks-Punches)

        /// <summary>
        /// Detect when UI elements are enabled/disabled to track comic opening/closing
        /// </summary>
        [HarmonyPatch(typeof(GameObject), "SetActive")]
        [HarmonyPostfix]
        public static void GameObjectSetActive_Postfix(GameObject __instance, bool value)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                string objName = __instance.name;

                // Look specifically for the Karate Boy comic minigame UI
                if (objName == "MinigameZoomComic")
                {
                    if (value && !_isComicOpen)
                    {
                        // Comic opened
                        _isComicOpen = true;
                        _currentPage = 0; // Start at page 0 (title page)
                        MelonLogger.Msg($"[Comic Book] Comic UI opened: {__instance.name}");
                        AnnounceComicOpened();
                    }
                    else if (!value && _isComicOpen)
                    {
                        // Comic closed
                        _isComicOpen = false;
                        _currentPage = -1;
                        MelonLogger.Msg($"[Comic Book] Comic UI closed");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in GameObjectSetActive_Postfix: {ex.Message}");
            }
        }


        /// <summary>
        /// Monitor for UP/DOWN key presses to announce attack totals
        /// </summary>
        [HarmonyPatch(typeof(InputManager), "Update")]
        [HarmonyPostfix]
        public static void MonitorComicInput_Postfix()
        {
            if (!AccessibilityMod.IsEnabled || !_isComicOpen)
                return;

            try
            {
                // Press UP or DOWN to hear the total attack counts
                if (InputManager.Instance.CheckIfInputUpWasPressed() ||
                    InputManager.Instance.CheckIfInputDownWasPressed())
                {
                    MelonLogger.Msg($"[Comic Book] Up/Down pressed, announcing attack totals");
                    AnnounceTotals();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in MonitorComicInput_Postfix: {ex.Message}");
            }
        }

        private static void AnnounceComicOpened()
        {
            string intro = "Karate Boy Chapter 1043 comic opened. " +
                          "Press up or down to hear the total attack counts. " +
                          "These numbers form the passcode for the puzzle.";
            TTSManager.Speak(intro, interrupt: false, priority: 10);
            _lastPageAnnounceTime = Time.unscaledTime;
        }


        private static void AnnounceTotals()
        {
            string totals = $"Total attacks across all pages: " +
                           $"{TOTAL_ELBOWS} Elbows, {TOTAL_KICKS} Kicks, {TOTAL_PUNCHES} Punches.";

            TTSManager.Speak(totals, interrupt: false, priority: 9);
            MelonLogger.Msg($"[Comic Book] {totals}");
        }

        /// <summary>
        /// Alternative approach: Monitor Fungus variables for page tracking
        /// </summary>
        private static void CheckFungusVariables()
        {
            try
            {
                var flowchart = GlobalFlowchart.GetFlowchart();
                if (flowchart == null) return;

                // Look for variables that might track comic page
                var varNames = flowchart.GetVariableNames();
                foreach (var varName in varNames)
                {
                    string varNameLower = varName.ToLower();
                    if (varNameLower.Contains("comic") || varNameLower.Contains("page") ||
                        varNameLower.Contains("karate") || varNameLower.Contains("1043"))
                    {
                        var intVar = flowchart.GetVariable<IntegerVariable>(varName);
                        if (intVar != null)
                        {
                            MelonLogger.Msg($"[Comic Book DEBUG] Variable '{varName}' = {intVar.Value}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CheckFungusVariables: {ex.Message}");
            }
        }
    }
}
