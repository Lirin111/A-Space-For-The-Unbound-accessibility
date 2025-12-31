using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace AsftuAccessibilityMod.Patches
{
    [HarmonyPatch(typeof(JugglingMinigameManager))]
    public static class JugglingMinigamePatches
    {
        private static int lastAnnouncedScore = 0;
        private static bool hasPlayedBeepForCurrentWindow = false;
        private static float lastBeepTime = 0f;

        [HarmonyPatch("StartMinigameOverride")]
        [HarmonyPostfix]
        public static void StartMinigameOverride_Postfix(JugglingMinigameManager __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                lastAnnouncedScore = 0;
                hasPlayedBeepForCurrentWindow = false;
                lastBeepTime = 0f;
                
                MelonLogger.Msg("[JugglingMinigame] Minigame started, reset beep flags");
                TTSManager.AnnounceUI("Kick-up challenge started. Press the confirm button when you hear the beep. Good luck!", true);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[JugglingMinigame] StartMinigameOverride error: {ex.Message}");
            }
        }

        [HarmonyPatch("StartJuggling")]
        [HarmonyPostfix]
        public static void StartJuggling_Postfix(JugglingMinigameManager __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                TTSManager.AnnounceUI("Start!", true);
                
                // Play early beep for the FIRST kick
                hasPlayedBeepForCurrentWindow = false;
                lastBeepTime = Time.unscaledTime;
                MinigameAudioManager.Instance.PlayOneShot(MinigameAudioManager.TIMING_BAR_TONE, 0.8f);
                MelonLogger.Msg($"[JugglingMinigame] StartJuggling - Early beep for first kick at time {lastBeepTime}");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[JugglingMinigame] StartJuggling error: {ex.Message}");
            }
        }

        [HarmonyPatch("EnableJuggle")]
        [HarmonyPostfix]
        public static void EnableJuggle_Postfix(JugglingMinigameManager __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                float currentTime = Time.unscaledTime;
                
                // Only play beep for the FIRST kick (when no beep has been played yet)
                // For subsequent kicks, the beep was already played in CheckResult after success
                if (!hasPlayedBeepForCurrentWindow || (currentTime - lastBeepTime) > 0.5f)
                {
                    hasPlayedBeepForCurrentWindow = true;
                    lastBeepTime = currentTime;
                    
                    MelonLogger.Msg($"[JugglingMinigame] EnableJuggle - Playing first kick beep at time {currentTime}");
                    MinigameAudioManager.Instance.PlayOneShot(MinigameAudioManager.TIMING_BAR_TONE, 0.8f);
                }
                else
                {
                    MelonLogger.Msg($"[JugglingMinigame] EnableJuggle - Beep already played in advance");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[JugglingMinigame] EnableJuggle error: {ex.Message}");
            }
        }

        [HarmonyPatch("DisableJuggle")]
        [HarmonyPostfix]
        public static void DisableJuggle_Postfix(JugglingMinigameManager __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                // Reset flag so next EnableJuggle can play a beep
                hasPlayedBeepForCurrentWindow = false;
                MelonLogger.Msg("[JugglingMinigame] DisableJuggle - Reset beep flag");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[JugglingMinigame] DisableJuggle error: {ex.Message}");
            }
        }

        [HarmonyPatch("CheckResult")]
        [HarmonyPrefix]
        public static void CheckResult_Prefix(JugglingMinigameManager __instance, out bool __state)
        {
            // Capture the current score before CheckResult modifies it
            __state = false;
            
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                // Access private fields using Harmony's AccessTools
                bool didJuggle = (bool)AccessTools.Field(typeof(JugglingMinigameManager), "didJuggle").GetValue(__instance);
                __state = didJuggle;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[JugglingMinigame] CheckResult_Prefix error: {ex.Message}");
            }
        }

        [HarmonyPatch("CheckResult")]
        [HarmonyPostfix]
        public static void CheckResult_Postfix(JugglingMinigameManager __instance, bool __state)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                bool didJuggle = __state;
                
                if (didJuggle)
                {
                    // Success - announce new score AND play early beep for next kick
                    var flowchartField = AccessTools.Field(typeof(JugglingMinigameManager), "flowchart");
                    var scoreVarField = AccessTools.Field(typeof(JugglingMinigameManager), "scoreVarName");
                    
                    if (flowchartField != null && scoreVarField != null)
                    {
                        var flowchart = flowchartField.GetValue(__instance) as Fungus.Flowchart;
                        var scoreVarName = scoreVarField.GetValue(__instance) as string;
                        
                        if (flowchart != null && !string.IsNullOrEmpty(scoreVarName))
                        {
                            int currentScore = flowchart.GetIntegerVariable(scoreVarName);
                            
                            if (currentScore != lastAnnouncedScore)
                            {
                                lastAnnouncedScore = currentScore;
                                TTSManager.AnnounceUI($"Good! Score: {currentScore}", false);
                            }
                        }
                    }
                    
                    // Play beep NOW as early warning for the next kick coming up
                    // Reset the flag so the beep can play
                    hasPlayedBeepForCurrentWindow = false;
                    lastBeepTime = Time.unscaledTime;
                    MinigameAudioManager.Instance.PlayOneShot(MinigameAudioManager.TIMING_BAR_TONE, 0.8f);
                    MelonLogger.Msg($"[JugglingMinigame] Early beep played after success at time {lastBeepTime}");
                }
                else
                {
                    // Failure - game over
                    var flowchartField = AccessTools.Field(typeof(JugglingMinigameManager), "flowchart");
                    var scoreVarField = AccessTools.Field(typeof(JugglingMinigameManager), "scoreVarName");
                    
                    if (flowchartField != null && scoreVarField != null)
                    {
                        var flowchart = flowchartField.GetValue(__instance) as Fungus.Flowchart;
                        var scoreVarName = scoreVarField.GetValue(__instance) as string;
                        
                        if (flowchart != null && !string.IsNullOrEmpty(scoreVarName))
                        {
                            int finalScore = flowchart.GetIntegerVariable(scoreVarName);
                            TTSManager.AnnounceUI($"Ball dropped! Final score: {finalScore}", true);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[JugglingMinigame] CheckResult_Postfix error: {ex.Message}");
            }
        }

        [HarmonyPatch("StopMinigameOverride")]
        [HarmonyPostfix]
        public static void StopMinigameOverride_Postfix(JugglingMinigameManager __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                lastAnnouncedScore = 0;
                hasPlayedBeepForCurrentWindow = false;
                lastBeepTime = 0f;
                MelonLogger.Msg("[JugglingMinigame] Minigame stopped, cleanup complete");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[JugglingMinigame] StopMinigameOverride_Postfix error: {ex.Message}");
            }
        }
    }
}
