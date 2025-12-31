using System;
using System.Collections;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace AsftuAccessibilityMod.Patches
{
    /// <summary>
    /// Patches for various minigames to make them accessible
    /// </summary>
    [HarmonyPatch]
    public static class MinigamePatches
    {
        // Timing Bar state tracking
        private static bool _timingBarInSafeZone = false;
        private static bool _timingBarInitialized = false;
        
        // Throw minigame state tracking
        private static int _lastThrowState = 0; // Using int instead of enum since State is protected
        private static float _lastThrowAngle = -1f;
        private static float _lastThrowPower = -1f;
        private static float _throwAngleBeepTimer = 0f;
        private const float THROW_ANGLE_BEEP_INTERVAL = 0.1f; // Faster beeps for quicker feedback
        
        // Eavesdrop state tracking
        private static float _lastEavesdropLevel = -1f;
        private static bool _lastEavesdropIsListening = false;

        // ============================================
        // TIMING BAR MINIGAME PATCHES
        // ============================================

        // Patch MinigameTimingBarBase.UpdateDefault to provide audio feedback
        [HarmonyPatch(typeof(MinigameTimingBarBase), "UpdateDefault")]
        [HarmonyPostfix]
        public static void TimingBarUpdate_Postfix(MinigameTimingBarBase __instance, float deltaTime)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                // Access fields using reflection (safeAreas is protected, cursor and mainBar are public)
                var safeAreasField = AccessTools.Field(typeof(MinigameTimingBarBase), "safeAreas");
                var safeAreas = safeAreasField?.GetValue(__instance) as System.Collections.IList;
                
                var cursor = __instance.cursor;
                var mainBar = __instance.mainBar;

                if (cursor == null || safeAreas == null || mainBar == null || safeAreas.Count == 0)
                    return;

                // Get cursor position (0 to 1)
                float mainBarWidth = mainBar.rectTransform.sizeDelta.x - 4f;
                float cursorPos = cursor.rectTransform.anchoredPosition.x / mainBarWidth;

                // Check if cursor is in any safe zone
                bool inSafeZone = false;
                float closestSafeZoneStart = -1f;
                float closestDistance = float.MaxValue;

                foreach (var safeAreaObj in safeAreas)
                {
                    if (safeAreaObj == null) continue;
                    
                    var startField = safeAreaObj.GetType().GetField("start");
                    var percentageField = safeAreaObj.GetType().GetField("percentage");
                    
                    if (startField != null && percentageField != null)
                    {
                        float start = (float)startField.GetValue(safeAreaObj);
                        float percentage = (float)percentageField.GetValue(safeAreaObj);
                        float end = start + percentage;

                        // Check if in safe zone
                        if (cursorPos >= start && cursorPos <= end)
                        {
                            inSafeZone = true;
                            
                            // Play success sound when entering safe zone
                            if (!_timingBarInSafeZone)
                            {
                                MinigameAudioManager.Instance.StopLoopingTone(MinigameAudioManager.TIMING_BAR_TONE);
                                MinigameAudioManager.Instance.PlayOneShot(MinigameAudioManager.TIMING_BAR_SUCCESS);
                            }
                            
                            _timingBarInSafeZone = true;
                            break;
                        }
                        
                        // Track closest safe zone
                        float distance = Mathf.Abs(cursorPos - start);
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestSafeZoneStart = start;
                        }
                    }
                }

                // Play continuous pitch that gets higher as you approach safe zone
                if (!inSafeZone && closestSafeZoneStart >= 0)
                {
                    _timingBarInSafeZone = false;
                    
                    // Calculate pitch based on distance (closer = higher pitch)
                    float distanceToSafeZone = Mathf.Abs(cursorPos - closestSafeZoneStart);
                    
                    // Map distance to pitch: far = 0.5 (low), very close = 2.5 (high)
                    float normalizedDistance = Mathf.Clamp01(1f - (distanceToSafeZone / 0.5f));
                    float pitch = 0.5f + (normalizedDistance * 2.0f);
                    
                    MinigameAudioManager.Instance.PlayLoopingTone(MinigameAudioManager.TIMING_BAR_TONE, pitch);
                }
                else if (!inSafeZone)
                {
                    _timingBarInSafeZone = false;
                    MinigameAudioManager.Instance.StopLoopingTone(MinigameAudioManager.TIMING_BAR_TONE);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in TimingBarUpdate_Postfix: {ex.Message}");
            }
        }

        // Announce when timing bar starts
        [HarmonyPatch(typeof(MinigameTimingBarBase), "StartMinigameOverride")]
        [HarmonyPostfix]
        public static void TimingBarStart_Postfix(MinigameTimingBarBase __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                _timingBarInSafeZone = false;
                _timingBarInitialized = true;
                MelonLogger.Msg("[TimingBar] Minigame started and initialized");
                TTSManager.AnnounceUI("Timing challenge started. Press button when pitch is highest.", interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in TimingBarStart_Postfix: {ex.Message}");
            }
        }

        // Reset when timing bar stops
        [HarmonyPatch(typeof(MinigameTimingBarBase), "StopMinigameOverride")]
        [HarmonyPostfix]
        public static void TimingBarStop_Postfix()
        {
            _timingBarInitialized = false;
            MinigameAudioManager.Instance.StopAll();
        }

        [HarmonyPatch(typeof(MinigameTimingBarBase), "CheckInput")]
        [HarmonyPostfix]
        public static void TimingBarCheckInput_Postfix()
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                if (_timingBarInSafeZone)
                {
                    TTSManager.AnnounceUI("Hit!", interrupt: false);
                }
                else
                {
                    TTSManager.AnnounceUI("Missed!", interrupt: false);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in TimingBarCheckInput_Postfix: {ex.Message}");
            }
        }

        // ============================================
        // THROW STAR MINIGAME PATCHES
        // ============================================

        // Patch MinigameThrow.UpdateDefault to provide directional audio
        [HarmonyPatch(typeof(MinigameThrow), "UpdateDefault")]
        [HarmonyPostfix]
        public static void ThrowUpdate_Postfix(MinigameThrow __instance, float deltaTime)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                // Access private state field
                var stateField = AccessTools.Field(typeof(MinigameThrow), "state");
                var stateValue = stateField.GetValue(__instance);
                int state = Convert.ToInt32(stateValue); // Convert enum to int (0=Preparing, 1=WaitingForInputDirection, 2=WaitingForInputPower, 3=Throw, 4=Fail)

                // State change announcements
                if (state != _lastThrowState)
                {
                    _lastThrowState = state;
                    
                    switch (state)
                    {
                        case 1: // WaitingForInputDirection
                            TTSManager.AnnounceUI("Aim direction. Listen for the angle. Press to confirm.", interrupt: true);
                            _throwAngleBeepTimer = 0f;
                            break;
                        case 2: // WaitingForInputPower
                            TTSManager.AnnounceUI("Set power. Listen for the meter. Release button to throw.", interrupt: true);
                            break;
                        case 3: // Throw
                            TTSManager.AnnounceUI("Throwing!", interrupt: false);
                            break;
                    }
                }

                // Direction audio feedback - continuous pitch that changes with angle
                if (state == 1) // WaitingForInputDirection
                {
                    var arrowField = AccessTools.Field(typeof(MinigameThrow), "arrowDirection");
                    var arrow = arrowField?.GetValue(__instance) as Transform;
                    
                    if (arrow != null)
                    {
                        float currentAngle = arrow.eulerAngles.z;
                        
                        // Normalize angle to 0-360
                        if (currentAngle < 0) currentAngle += 360f;
                        
                        // For throw minigame, provide audio based on angle
                        // Map angle to pitch for continuous feedback
                        // Target angle varies per game, so we provide consistent audio cues
                        // Lower angles (30-60°) are typically the target range
                        
                        // Calculate how far from ideal throwing angle (45°)
                        float idealAngle = 45f;
                        float angleDiff = Mathf.Abs(currentAngle - idealAngle);
                        if (angleDiff > 180f) angleDiff = 360f - angleDiff;
                        
                        // Play continuous pitch based on angle
                        // Close to 45° = higher pitch, far = lower pitch
                        float normalizedDiff = Mathf.Clamp01(angleDiff / 90f);
                        float pitch = 2.5f - (normalizedDiff * 2.0f); // Range: 0.5 to 2.5
                        
                        // When very close to ideal angle (within 15°), play success sound
                        if (angleDiff < 15f && _lastThrowAngle > 15f)
                        {
                            MinigameAudioManager.Instance.PlayOneShot(MinigameAudioManager.THROW_SUCCESS);
                        }
                        
                        MinigameAudioManager.Instance.PlayLoopingTone(MinigameAudioManager.THROW_TONE, pitch);
                        _lastThrowAngle = angleDiff;
                    }
                }
                else
                {
                    // Stop audio when not in direction input state
                    MinigameAudioManager.Instance.StopLoopingTone(MinigameAudioManager.THROW_TONE);
                }

                // Power audio feedback
                if (state == 2) // WaitingForInputPower
                {
                    // Don't play audio for power - game already has audio feedback for this phase
                    /*
                    var powerBarField = AccessTools.Field(typeof(MinigameThrow), "powerBar");
                    var powerBar = powerBarField?.GetValue(__instance) as UnityEngine.UI.Image;
                    
                    if (powerBar != null)
                    {
                        float power = powerBar.fillAmount;
                        
                        // Play continuous tone that rises with power (shorter for rapid updates)
                        int frequency = 200 + (int)(power * 800f); // 200Hz to 1000Hz
                        TTSManager.PlayTone(frequency, 20); // Very short beeps for smooth continuous tone
                        
                        _lastThrowPower = power;
                    }
                    */
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ThrowUpdate_Postfix: {ex.Message}");
            }
        }

        // Announce throw result
        [HarmonyPatch(typeof(MinigameThrowStar), "ThrowCoroutine")]
        [HarmonyPrefix]
        public static void ThrowStarCoroutine_Prefix(MinigameThrowStar __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                // Get calculated speed to determine if it's a good throw
                var method = AccessTools.Method(typeof(MinigameThrow), "CalculateSpeed");
                if (method != null)
                {
                    float speed = (float)method.Invoke(__instance, null);
                    float powerLevel = (speed - 0.2f) / 3.9f; // Reverse the calculation
                    
                    string powerDescription;
                    if (powerLevel < 0.3f)
                        powerDescription = "weak";
                    else if (powerLevel < 0.7f)
                        powerDescription = "medium";
                    else
                        powerDescription = "strong";
                    
                    MelonLogger.Msg($"[Throw] Power level: {powerLevel:F2} ({powerDescription})");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ThrowStarCoroutine_Prefix: {ex.Message}");
            }
        }

        // ============================================
        // EAVESDROP MINIGAME PATCHES
        // ============================================

        // Patch MinigameEavesdrop.UpdateDefault to provide listening audio feedback
        [HarmonyPatch(typeof(MinigameEavesdrop), "UpdateDefault")]
        [HarmonyPostfix]
        public static void EavesdropUpdate_Postfix(MinigameEavesdrop __instance, float deltaTime)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                // Access private fields
                var eavesdropSenseField = AccessTools.Field(typeof(MinigameEavesdrop), "eavesdropSense");
                var stateField = AccessTools.Field(typeof(MinigameEavesdrop), "state");
                
                if (eavesdropSenseField == null || stateField == null)
                    return;

                float eavesdropLevel = (float)eavesdropSenseField.GetValue(__instance);
                var stateValue = stateField.GetValue(__instance);
                int state = Convert.ToInt32(stateValue); // 0=Eavesdrop, 1=Pretend

                // Check if listening state changed
                bool isListening = InputManager.Instance.CheckIfInputInteractionIsPressed();
                
                if (isListening != _lastEavesdropIsListening)
                {
                    _lastEavesdropIsListening = isListening;
                    
                    if (isListening)
                    {
                        TTSManager.PlayTone(600, 80);
                    }
                    else
                    {
                        TTSManager.PlayTone(300, 80);
                    }
                }

                // Play continuous tone that represents eavesdrop level
                if (isListening && state == 0) // Eavesdrop state
                {
                    // Frequency increases as eavesdrop level increases
                    int frequency = 300 + (int)(eavesdropLevel * 500f);
                    int duration = 50;
                    TTSManager.PlayTone(frequency, duration);
                }

                // Announce when fully listening (level reaches 100%)
                if (eavesdropLevel >= 1f && _lastEavesdropLevel < 1f)
                {
                    TTSManager.PlayTone(1000, 150); // Success tone
                }

                // Warn when in Pretend mode and timer is running out
                if (state == 1 && !isListening) // Pretend state
                {
                    var pretendTimerField = AccessTools.Field(typeof(MinigameEavesdrop), "currentPretendTimer");
                    var pretendTimerTargetField = AccessTools.Field(typeof(MinigameEavesdrop), "pretendTimerTarget");
                    
                    if (pretendTimerField != null && pretendTimerTargetField != null)
                    {
                        float currentTimer = (float)pretendTimerField.GetValue(__instance);
                        float targetTimer = (float)pretendTimerTargetField.GetValue(__instance);
                        
                        float timeLeft = targetTimer - currentTimer;
                        
                        // Play warning beeps when time is running out
                        if (timeLeft < 2f && timeLeft > 0f)
                        {
                            int frequency = 400 + (int)((2f - timeLeft) * 300f);
                            TTSManager.PlayTone(frequency, 50);
                        }
                    }
                }

                _lastEavesdropLevel = eavesdropLevel;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in EavesdropUpdate_Postfix: {ex.Message}");
            }
        }

        // Announce when eavesdrop starts
        [HarmonyPatch(typeof(MinigameEavesdrop), "StartMinigameOverride")]
        [HarmonyPostfix]
        public static void EavesdropStart_Postfix(MinigameEavesdrop __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                _lastEavesdropLevel = -1f;
                _lastEavesdropIsListening = false;
                TTSManager.AnnounceUI("Eavesdropping. Hold button to listen. Rising tone means better hearing.", interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in EavesdropStart_Postfix: {ex.Message}");
            }
        }

        // Announce when switching to Pretend mode
        [HarmonyPatch(typeof(MinigameEavesdrop), "SetToPretend")]
        [HarmonyPostfix]
        public static void EavesdropSetToPretend_Postfix(MinigameEavesdrop __instance, float timer)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                TTSManager.AnnounceUI($"Pretend mode. Don't listen for {timer:F0} seconds or you'll be caught.", interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in EavesdropSetToPretend_Postfix: {ex.Message}");
            }
        }

        // ============================================
        // CINEMA MINIGAME PATCHES
        // ============================================

        private static bool _cinemaAnnouncedThisSession = false;
        private static bool _fallingDebrisAnnouncedThisSession = false;

        // Patch the base Minigame.StartMinigame to detect when ANY minigame starts
        [HarmonyPatch(typeof(Minigame), "StartMinigame")]
        [HarmonyPostfix]
        public static void Minigame_StartMinigame_Postfix(Minigame __instance)
        {
            try
            {
                string minigameType = __instance.GetType().Name;
                MelonLogger.Msg($"[Minigame] StartMinigame called for: {minigameType}");
                
                if (!AccessibilityMod.IsEnabled)
                {
                    MelonLogger.Msg($"[Minigame] Accessibility mod disabled");
                    return;
                }
                
                // Check if it's the cinema minigame
                if (minigameType == "MinigameCinema")
                {
                    MelonLogger.Msg("[Cinema] Cinema minigame detected!");
                    
                    // Only announce once per session
                    if (_cinemaAnnouncedThisSession)
                    {
                        MelonLogger.Msg("[Cinema] Already announced this session");
                        return;
                    }

                    _cinemaAnnouncedThisSession = true;
                    
                    // Get the button name for interact
                    string buttonName = GetButtonName(PlayerKeybinding.KeyBindingType.Interact);
                    MelonLogger.Msg($"[Cinema] Button name: {buttonName}");
                    
                    string announcement = $"Press {buttonName} to eat popcorn.";
                    TTSManager.AnnounceUI(announcement, interrupt: true);
                    MelonLogger.Msg($"[Cinema] Announced: {announcement}");
                }
                // Check if it's a falling debris minigame (includes variants)
                else if (minigameType.Contains("FallingDebris"))
                {
                    MelonLogger.Msg("[FallingDebris] Falling debris minigame detected!");
                    
                    // Only announce once per session
                    if (_fallingDebrisAnnouncedThisSession)
                    {
                        MelonLogger.Msg("[FallingDebris] Already announced this session");
                        return;
                    }

                    _fallingDebrisAnnouncedThisSession = true;
                    
                    string announcement = "Falling debris. Move left and right to avoid obstacles. Audio cues warn of danger.";
                    TTSManager.AnnounceUI(announcement, interrupt: true);
                    MelonLogger.Msg($"[FallingDebris] Announced: {announcement}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Minigame] Error in Minigame_StartMinigame_Postfix: {ex.Message}");
                MelonLogger.Error($"[Minigame] Stack: {ex.StackTrace}");
            }
        }
        
        // Reset the flags when minigames stop
        [HarmonyPatch(typeof(Minigame), "StopMinigame")]
        [HarmonyPrefix]
        public static void Minigame_StopMinigame_Prefix(Minigame __instance)
        {
            try
            {
                string minigameType = __instance.GetType().Name;
                MelonLogger.Msg($"[Minigame] StopMinigame called for: {minigameType}");
                
                if (minigameType == "MinigameCinema")
                {
                    _cinemaAnnouncedThisSession = false;
                    MelonLogger.Msg("[Cinema] Cinema stopped, reset announcement flag");
                }
                else if (minigameType.Contains("FallingDebris"))
                {
                    _fallingDebrisAnnouncedThisSession = false;
                    MelonLogger.Msg("[FallingDebris] Falling debris stopped, reset announcement flag");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Minigame] Error in Minigame_StopMinigame_Prefix: {ex.Message}");
            }
        }

        // Helper method to get button name for a keybinding type
        private static string GetButtonName(PlayerKeybinding.KeyBindingType bindingType)
        {
            try
            {
                var keybinding = InputManager.Instance?.playerkeybind;
                if (keybinding == null)
                    return "interact button";

                // Try to get the current control for this binding
                var controlField = keybinding.GetType().GetField(bindingType.ToString());
                if (controlField != null)
                {
                    var control = controlField.GetValue(keybinding);
                    if (control != null)
                    {
                        // Get display string
                        var displayStringMethod = control.GetType().GetProperty("displayString");
                        if (displayStringMethod != null)
                        {
                            string displayString = displayStringMethod.GetValue(control) as string;
                            if (!string.IsNullOrEmpty(displayString))
                            {
                                return displayString;
                            }
                        }
                    }
                }
                
                // Fallback to generic name
                return bindingType.ToString().ToLower() + " button";
            }
            catch
            {
                return "interact button";
            }
        }

        // ============================================
        // BUTTON SEQUENCE MINIGAME PATCHES (Rhythm/QTE)
        // ============================================

        private static int _lastButtonSequenceIndex = -1;
        private static float _buttonSequenceAnnouncementDelay = 0f;
        private const float BUTTON_SEQUENCE_DELAY = 0.3f;
        private static bool _isBattleMinigame = false;

        [HarmonyPatch(typeof(MinigameButtonSequence), "UpdateDefault")]
        [HarmonyPostfix]
        public static void ButtonSequenceUpdate_Postfix(MinigameButtonSequence __instance, float deltaTime)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                var currentSequenceField = AccessTools.Field(typeof(MinigameButtonSequence), "currentSequence");
                var currentSequenceIndexField = AccessTools.Field(typeof(MinigameButtonSequence), "currentSequenceIndex");
                var gameStateField = AccessTools.Field(typeof(MinigameButtonSequence), "gameState");
                
                var currentSequence = currentSequenceField?.GetValue(__instance) as System.Collections.IList;
                int currentIndex = (int)(currentSequenceIndexField?.GetValue(__instance) ?? -1);
                var gameState = gameStateField?.GetValue(__instance);
                
                if (currentSequence == null || gameState == null)
                    return;

                // Only announce when waiting for input
                string stateName = gameState.ToString();
                
                // Debug logging every few frames
                if (UnityEngine.Random.value < 0.01f) // Log 1% of the time to avoid spam
                {
                    MelonLogger.Msg($"[ButtonSequence] State={stateName}, Index={currentIndex}/{currentSequence.Count}, LastIndex={_lastButtonSequenceIndex}");
                }
                
                if (stateName != "waitingForInput")
                    return;

                // Announce new button when index changes
                if (currentIndex != _lastButtonSequenceIndex && currentIndex >= 0 && currentIndex < currentSequence.Count)
                {
                    // Only set delay if not currently waiting to announce
                    if (_buttonSequenceAnnouncementDelay <= 0)
                    {
                        _buttonSequenceAnnouncementDelay = BUTTON_SEQUENCE_DELAY;
                        _lastButtonSequenceIndex = currentIndex;
                        MelonLogger.Msg($"[ButtonSequence] New button at index {currentIndex}, delay set");
                    }
                }

                // Play directional audio after short delay
                if (_buttonSequenceAnnouncementDelay > 0)
                {
                    _buttonSequenceAnnouncementDelay -= deltaTime;
                    if (_buttonSequenceAnnouncementDelay <= 0 && currentIndex >= 0 && currentIndex < currentSequence.Count)
                    {
                        var inputObject = currentSequence[currentIndex];
                        var dataField = inputObject.GetType().GetProperty("Data");
                        if (dataField != null)
                        {
                            var data = dataField.GetValue(inputObject);
                            var keyListField = data.GetType().GetField("keyList");
                            if (keyListField != null)
                            {
                                var keyList = keyListField.GetValue(data) as System.Collections.IList;
                                if (keyList != null && keyList.Count > 0)
                                {
                                    AnnounceButtonDirection(keyList);
                                    _buttonSequenceAnnouncementDelay = -1f; // Prevent re-announcing
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ButtonSequenceUpdate_Postfix: {ex.Message}");
            }
        }

        // Patch for battle-specific update (since it overrides UpdateDefault)
        [HarmonyPatch(typeof(MinigameButtonSequence_Battle), "UpdateDefault")]
        [HarmonyPostfix]
        public static void ButtonSequenceBattleUpdate_Postfix(MinigameButtonSequence_Battle __instance, float deltaTime)
        {
            // Don't do anything - we announce all buttons upfront in StartRound
            // Keep this patch to prevent the base class patch from interfering
        }

        [HarmonyPatch(typeof(MinigameButtonSequence), "StartMinigameOverride")]
        [HarmonyPostfix]
        public static void ButtonSequenceStart_Postfix(MinigameButtonSequence __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                _lastButtonSequenceIndex = -1;

                // Check if this is a battle minigame
                _isBattleMinigame = __instance.GetType().Name.Contains("Battle");

                // Only increase the overall timeout for NON-battle sequences (finishing phase)
                // Battle sequences don't need it because all inputs are announced upfront
                if (!_isBattleMinigame)
                {
                    // Increase the overall sequence timeout (default is 4 seconds total)
                    // Set it to 20 seconds for finishing phase to give players enough time
                    var timerInputMaxField = AccessTools.Field(typeof(MinigameButtonSequence), "timerInputMax");
                    var timerInputField = AccessTools.Field(typeof(MinigameButtonSequence), "timerInput");

                    if (timerInputMaxField != null && timerInputField != null)
                    {
                        timerInputMaxField.SetValue(__instance, 20.0f);
                        timerInputField.SetValue(__instance, 20.0f);  // Also update the current timer value
                        MelonLogger.Msg($"[ButtonSequence] Increased overall sequence timeout to 20 seconds for finishing phase");
                    }
                }

                // Don't announce anything here - let the UpdateDefault patch announce each button as needed
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ButtonSequenceStart_Postfix: {ex.Message}");
            }
        }

        private static int _lastAnnouncedRound = -1;
        private static bool _inDefensePhase = false;
        private static float _lastAnnouncementTime = 0f;
        
        // Battle-specific start announcement - called when round first starts
        [HarmonyPatch(typeof(MinigameButtonSequence_Battle), "StartRound")]
        [HarmonyPostfix]
        public static void BattleStartRound_Postfix(MinigameButtonSequence_Battle __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                _lastButtonSequenceIndex = -1;
                
                // Get the round number for debugging
                var roundField = AccessTools.Field(typeof(MinigameButtonSequence_Battle), "round");
                int round = (int)(roundField?.GetValue(__instance) ?? -1);
                
                MelonLogger.Msg($"[ButtonSequence] StartRound called for round {round}");
                
                // Reset tracking when new round starts via AdvanceRound
                if (round != _lastAnnouncedRound)
                {
                    _inDefensePhase = false;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in BattleStartRound_Postfix: {ex.Message}");
            }
        }
        
        // Announce attack phase when state changes to waitingForInput
        [HarmonyPatch(typeof(MinigameButtonSequence), "ChangeGameState")]
        [HarmonyPostfix]
        public static void ButtonSequenceChangeGameState_Postfix(MinigameButtonSequence __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                // Only handle battle minigames
                if (!(__instance is MinigameButtonSequence_Battle))
                    return;
                    
                var gameStateField = AccessTools.Field(typeof(MinigameButtonSequence), "gameState");
                var gameState = gameStateField?.GetValue(__instance);
                
                if (gameState != null && gameState.ToString() == "waitingForInput")
                {
                    // Returning to attack phase after defense
                    if (_inDefensePhase)
                    {
                        _inDefensePhase = false;
                        _lastAnnouncementTime = 0f; // Reset cooldown to force announcement
                        // Add small delay to ensure sequence is generated
                        __instance.StartCoroutine(DelayedAnnounceAttackPhase(__instance as MinigameButtonSequence_Battle, 0.1f));
                    }
                    // First time entering attack phase for this round
                    else
                    {
                        // Add small delay to ensure sequence is generated
                        __instance.StartCoroutine(DelayedAnnounceAttackPhase(__instance as MinigameButtonSequence_Battle, 0.15f));
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ButtonSequenceChangeGameState_Postfix: {ex.Message}");
            }
        }
        
        private static System.Collections.IEnumerator DelayedAnnounceAttackPhase(MinigameButtonSequence_Battle instance, float delay)
        {
            yield return new UnityEngine.WaitForSeconds(delay);
            AnnounceAttackPhase(instance);
        }
        
        private static void AnnounceAttackPhase(MinigameButtonSequence_Battle __instance)
        {
            try
            {
                // Get the round number
                var roundField = AccessTools.Field(typeof(MinigameButtonSequence_Battle), "round");
                int round = (int)(roundField?.GetValue(__instance) ?? -1);
                
                // Prevent rapid duplicate announcements (within 0.5 seconds)
                float currentTime = UnityEngine.Time.time;
                if (round == _lastAnnouncedRound && (currentTime - _lastAnnouncementTime) < 0.5f)
                {
                    MelonLogger.Msg($"[ButtonSequence] Skipping announcement for round {round} - too soon (cooldown)");
                    return;
                }
                
                _lastAnnouncedRound = round;
                _lastAnnouncementTime = currentTime;
                MelonLogger.Msg($"[ButtonSequence] Announcing attack phase for round {round}");
                
                // Get the button sequence
                var currentSequenceField = AccessTools.Field(typeof(MinigameButtonSequence), "currentSequence");
                var currentSequence = currentSequenceField?.GetValue(__instance) as System.Collections.IList;
                
                if (currentSequence != null && currentSequence.Count > 0)
                {
                    // Build the sequence string
                    System.Collections.Generic.List<string> buttonNames = new System.Collections.Generic.List<string>();

                    // Read the sequence in the correct order (0, 1, 2, 3...)
                    System.Text.StringBuilder sequenceDebug = new System.Text.StringBuilder();
                    for (int i = 0; i < currentSequence.Count; i++)
                    {
                        var inputObject = currentSequence[i];

                        // Check if this button has HP (needs to be pressed multiple times)
                        var hpField = inputObject.GetType().GetProperty("Hp");
                        var maxHPField = inputObject.GetType().GetProperty("MaxHP");
                        int hp = 1;
                        int maxHP = 1;
                        if (hpField != null && maxHPField != null)
                        {
                            hp = (int)hpField.GetValue(inputObject);
                            maxHP = (int)maxHPField.GetValue(inputObject);
                        }

                        var dataField = inputObject.GetType().GetProperty("Data");
                        if (dataField != null)
                        {
                            var data = dataField.GetValue(inputObject);
                            var keyListField = data.GetType().GetField("keyList");
                            if (keyListField != null)
                            {
                                var keyList = keyListField.GetValue(data) as System.Collections.IList;
                                if (keyList != null && keyList.Count > 0)
                                {
                                    string keyName;

                                    // Check if this is a diagonal input (2 keys)
                                    if (keyList.Count == 2)
                                    {
                                        // Combine the two keys for diagonal inputs
                                        string key1 = keyList[0].ToString();
                                        string key2 = keyList[1].ToString();
                                        keyName = key1 + key2; // e.g., "UpLeft" or "DownRight"
                                    }
                                    else
                                    {
                                        // Single key input
                                        keyName = keyList[0].ToString();
                                    }

                                    string buttonName = GetButtonNameForTTS(keyName);

                                    // If HP > 1, this button needs to be pressed multiple times!
                                    // Add it to the announcement multiple times
                                    for (int j = 0; j < maxHP; j++)
                                    {
                                        buttonNames.Add(buttonName);
                                    }

                                    // Build debug string showing index -> button mapping with HP
                                    if (sequenceDebug.Length > 0) sequenceDebug.Append(", ");
                                    sequenceDebug.Append($"[{i}]={keyName}");
                                    if (maxHP > 1)
                                    {
                                        sequenceDebug.Append($"(x{maxHP})");
                                    }
                                }
                            }
                        }
                    }

                    // Log the sequence we extracted
                    MelonLogger.Msg($"[ButtonSequence DEBUG] Raw sequence (array order): {sequenceDebug}");

                    // Announce the sequence AS-IS without any reversal
                    // The sequence in the array is already in the correct order for pressing
                    if (buttonNames.Count > 0)
                    {
                        string sequence = string.Join(", ", buttonNames.ToArray());
                        TTSManager.AnnounceUI(sequence, interrupt: true);
                        MelonLogger.Msg($"[ButtonSequence] Announced sequence for round {round}: {sequence}");
                    }
                    else
                    {
                        TTSManager.AnnounceUI("Press arrow keys as shown.", interrupt: true);
                        MelonLogger.Msg($"[ButtonSequence] Announced generic attack phase for round {round}");
                    }
                }
                else
                {
                    TTSManager.AnnounceUI("Press arrow keys as shown.", interrupt: true);
                    MelonLogger.Msg($"[ButtonSequence] No sequence found for round {round}, announced generic");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in AnnounceAttackPhase: {ex.Message}");
            }
        }
        
        private static string GetButtonNameForTTS(string keyName)
        {
            switch (keyName)
            {
                case "Up":
                    return "up";
                case "Down":
                    return "down";
                case "Left":
                    return "left";
                case "Right":
                    return "right";
                case "UpLeft":
                    return "U L";
                case "UpRight":
                    return "U R";
                case "DownLeft":
                    return "D L";
                case "DownRight":
                    return "D R";
                case "Interact":
                    return "attack";
                case "Cancel":
                    return "cancel";
                case "Submit":
                case "Jump":
                case "Action":
                    return "action button";
                default:
                    return "button";
            }
        }
        
        // Non-battle button sequences - announce with TTS
        private static void AnnounceButtonDirection(System.Collections.IList keyList)
        {
            try
            {
                // Log the full keyList for debugging
                System.Text.StringBuilder keyListDebug = new System.Text.StringBuilder();
                for (int i = 0; i < keyList.Count; i++)
                {
                    if (i > 0) keyListDebug.Append(", ");
                    keyListDebug.Append($"[{i}]={keyList[i]}");
                }
                MelonLogger.Msg($"[ButtonSequence] AnnounceButtonDirection called with keyList: {keyListDebug}");

                string keyName;

                // Check if this is a diagonal input (2 keys)
                if (keyList.Count == 2)
                {
                    // Combine the two keys for diagonal inputs
                    string key1 = keyList[0].ToString();
                    string key2 = keyList[1].ToString();
                    keyName = key1 + key2; // e.g., "UpLeft" or "DownRight"
                }
                else if (keyList.Count == 1)
                {
                    // Single key input
                    keyName = keyList[0].ToString();
                }
                else
                {
                    // Unknown input
                    MelonLogger.Msg($"[ButtonSequence] Skipping announcement - keyList has {keyList.Count} keys");
                    return;
                }

                string buttonName = GetButtonNameForTTS(keyName);
                TTSManager.AnnounceUI(buttonName, interrupt: true);
                MelonLogger.Msg($"[ButtonSequence] Announced button: {buttonName} (keyName: {keyName})");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in AnnounceButtonDirection: {ex.Message}");
            }
        }
        
        // Battle timing bar specific announcement
        [HarmonyPatch(typeof(MinigameTimingBarBattle), "StartMinigameOverride")]
        [HarmonyPostfix]
        public static void TimingBarBattleStart_Postfix(MinigameTimingBarBattle __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                _inDefensePhase = true;
                TTSManager.AnnounceUI("Defense phase. Press button when pitch is highest.", interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in TimingBarBattleStart_Postfix: {ex.Message}");
            }
        }

        // ============================================
        // BUTTON MASHING MINIGAME PATCHES
        // ============================================

        private static float _lastMashingProgress = 0f;
        private static float _mashingProgressAnnouncementTimer = 0f;
        private const float MASHING_ANNOUNCEMENT_INTERVAL = 1f;

        [HarmonyPatch(typeof(MinigameButtonMashing), "UpdateDefault")]
        [HarmonyPostfix]
        public static void ButtonMashingUpdate_Postfix(MinigameButtonMashing __instance, float deltaTime)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                var counterField = AccessTools.Field(typeof(MinigameButtonMashing), "counter");
                var targetPressField = AccessTools.Field(typeof(MinigameButtonMashing), "targetPress");
                
                float counter = (float)(counterField?.GetValue(__instance) ?? 0f);
                int targetPress = (int)(targetPressField?.GetValue(__instance) ?? 1);
                
                float progress = counter / targetPress;
                
                // Announce progress periodically
                _mashingProgressAnnouncementTimer += deltaTime;
                if (_mashingProgressAnnouncementTimer >= MASHING_ANNOUNCEMENT_INTERVAL)
                {
                    _mashingProgressAnnouncementTimer = 0f;
                    int percentage = Mathf.RoundToInt(progress * 100f);
                    
                    // Play rising tone based on progress
                    float frequency = 200f + (progress * 800f); // 200Hz to 1000Hz
                    MinigameAudioManager.Instance?.PlayProgressTone(progress);
                    
                    if (percentage % 20 == 0) // Announce every 20%
                    {
                        TTSManager.AnnounceUI($"{percentage} percent", interrupt: false);
                    }
                }
                
                _lastMashingProgress = progress;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ButtonMashingUpdate_Postfix: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(MinigameButtonMashing), "StartMinigameOverride")]
        [HarmonyPostfix]
        public static void ButtonMashingStart_Postfix()
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                _lastMashingProgress = 0f;
                _mashingProgressAnnouncementTimer = 0f;
                string buttonName = GetButtonName(PlayerKeybinding.KeyBindingType.Interact);
                TTSManager.AnnounceUI($"Button mashing. Repeatedly press {buttonName}. Rising tone shows progress.", interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ButtonMashingStart_Postfix: {ex.Message}");
            }
        }

        // ============================================
        // FALLING DEBRIS MINIGAME PATCHES
        // ============================================

        // Track audio channels for each obstacle so we can stop them when destroyed
        private static System.Collections.Generic.Dictionary<int, string> _debrisAudioChannels = new System.Collections.Generic.Dictionary<int, string>();

        // Track obstacle positions for dynamic audio updates
        private static System.Collections.Generic.Dictionary<int, MinigameFallingDebris_DebrisController> _activeDebris = new System.Collections.Generic.Dictionary<int, MinigameFallingDebris_DebrisController>();
        private static System.Collections.Generic.Dictionary<int, MinigameFallingDebris_ObstacleThornController> _activeThorns = new System.Collections.Generic.Dictionary<int, MinigameFallingDebris_ObstacleThornController>();

        // Track if falling debris minigame is active
        private static bool _fallingDebrisActive = false;
        private static float _audioUpdateTimer = 0f;
        private const float AUDIO_UPDATE_INTERVAL = 0.1f; // Update audio every 100ms

        // Slow down debris falling speed for accessibility
        private const float DEBRIS_SPEED_MULTIPLIER = 0.4f; // 40% speed - gives 2.5x more time to react
        private const float THORN_DURATION_MULTIPLIER = 2.0f; // 2x longer anticipation time

        // Patch the spawning logic to prevent spawning when at limit
        [HarmonyPatch(typeof(MinigameFallingDebrisRandom), "UpdateMinigameFallingDebris")]
        [HarmonyPrefix]
        public static bool UpdateMinigameFallingDebris_Prefix(MinigameFallingDebrisRandom __instance, float deltaTime)
        {
            if (!AccessibilityMod.IsEnabled)
                return true;

            try
            {
                // Check if we're at the limit - block spawning completely
                int activeCount = _activeDebris.Count + _activeThorns.Count;
                if (activeCount >= MAX_DEBRIS_ON_SCREEN)
                {
                    // Don't allow any spawning when at limit - return false to skip original method
                    return false;
                }

                return true; // Allow spawning
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in UpdateMinigameFallingDebris_Prefix: {ex.Message}");
                return true; // Allow on error
            }
        }

        // Patch debris initialization to slow it down
        [HarmonyPatch(typeof(MinigameFallingDebris_DebrisController), "Init")]
        [HarmonyPrefix]
        public static bool DebrisInit_Prefix(ref float speed)
        {
            if (!AccessibilityMod.IsEnabled)
                return true;

            try
            {
                // Slow down the debris falling speed
                speed *= DEBRIS_SPEED_MULTIPLIER;
                MelonLogger.Msg($"[FallingDebris] Debris speed reduced to {DEBRIS_SPEED_MULTIPLIER * 100}% ({speed:F2})");
                return true; // Allow Init to proceed
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in DebrisInit_Prefix: {ex.Message}");
                return true; // Allow on error
            }
        }

        // Add looping audio after debris Init
        [HarmonyPatch(typeof(MinigameFallingDebris_DebrisController), "Init")]
        [HarmonyPostfix]
        public static void DebrisInit_Postfix(MinigameFallingDebris_DebrisController __instance, Vector3 endPos)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                // Get player position for directional audio
                var playerPos = com.mojiken.asftu.PlayerController.Instance.transform.position;
                float debrisX = endPos.x;
                float playerX = playerPos.x;

                // Calculate direction: left, right, or center
                float distance = debrisX - playerX;

                // Use instance ID as unique identifier
                int instanceId = __instance.GetInstanceID();
                string soundFile;

                // Play directional looping audio cue based on where the debris will land
                if (Mathf.Abs(distance) < 0.3f)
                {
                    // Debris landing very close - center/danger (use up sound for danger)
                    soundFile = "button_up.wav";
                    MelonLogger.Msg($"[FallingDebris] Debris spawned at CENTER (player: {playerX:F2}, debris: {debrisX:F2})");
                }
                else if (distance < 0)
                {
                    // Debris landing to the left
                    soundFile = "button_left.wav";
                    MelonLogger.Msg($"[FallingDebris] Debris spawned on LEFT (player: {playerX:F2}, debris: {debrisX:F2}, distance: {distance:F2})");
                }
                else
                {
                    // Debris landing to the right
                    soundFile = "button_right.wav";
                    MelonLogger.Msg($"[FallingDebris] Debris spawned on RIGHT (player: {playerX:F2}, debris: {debrisX:F2}, distance: {distance:F2})");
                }

                // Start looping directional sound
                string channelKey = MinigameAudioManager.Instance?.PlayDirectionalLoopingSound(soundFile, debrisX, playerPos.y, instanceId.ToString());
                if (!string.IsNullOrEmpty(channelKey))
                {
                    _debrisAudioChannels[instanceId] = channelKey;
                    _activeDebris[instanceId] = __instance; // Track for dynamic updates
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in DebrisInit_Postfix: {ex.Message}");
            }
        }

        // Clean up when debris starts being destroyed (hits ground)
        [HarmonyPatch(typeof(MinigameFallingDebris_DebrisController), "StartDestroy")]
        [HarmonyPrefix]
        public static void DebrisStartDestroy_Prefix(MinigameFallingDebris_DebrisController __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                int instanceId = __instance.GetInstanceID();
                if (_debrisAudioChannels.ContainsKey(instanceId))
                {
                    MinigameAudioManager.Instance?.StopDirectionalSound(_debrisAudioChannels[instanceId]);
                    _debrisAudioChannels.Remove(instanceId);
                    MelonLogger.Msg($"[FallingDebris] Stopped debris sound on StartDestroy (id={instanceId})");
                }
                // Remove from active tracking immediately so new debris can spawn
                _activeDebris.Remove(instanceId);
                MelonLogger.Msg($"[FallingDebris] Removed debris from tracking (id={instanceId}), active count now: {_activeDebris.Count + _activeThorns.Count}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in DebrisStartDestroy_Prefix: {ex.Message}");
            }
        }

        // Final cleanup when debris object is completely destroyed
        [HarmonyPatch(typeof(MinigameFallingDebris_DebrisController), "DestroyObjectOverride")]
        [HarmonyPrefix]
        public static void DebrisDestroy_Prefix(MinigameFallingDebris_DebrisController __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                int instanceId = __instance.GetInstanceID();
                // Double-check cleanup (in case StartDestroy wasn't called)
                if (_debrisAudioChannels.ContainsKey(instanceId))
                {
                    MinigameAudioManager.Instance?.StopDirectionalSound(_debrisAudioChannels[instanceId]);
                    _debrisAudioChannels.Remove(instanceId);
                }
                _activeDebris.Remove(instanceId);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in DebrisDestroy_Prefix: {ex.Message}");
            }
        }

        // Patch thorn/stalagmite initialization to provide warning with looping sound
        [HarmonyPatch(typeof(MinigameFallingDebris_ObstacleThornController), "Init")]
        [HarmonyPostfix]
        public static void ThornInit_Postfix(MinigameFallingDebris_ObstacleThornController __instance, Vector3 position)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                // Get player position for directional audio
                var playerPos = com.mojiken.asftu.PlayerController.Instance.transform.position;
                float thornX = position.x;
                float playerX = playerPos.x;

                // Calculate direction
                float distance = thornX - playerX;

                // Use instance ID as unique identifier
                int instanceId = __instance.GetInstanceID();
                string soundFile = "button_down.wav"; // Always use down sound for thorns

                // Play directional looping audio cue based on where the thorn will emerge
                if (Mathf.Abs(distance) < 0.3f)
                {
                    MelonLogger.Msg($"[FallingDebris] Thorn spawned at CENTER (player: {playerX:F2}, thorn: {thornX:F2})");
                }
                else if (distance < 0)
                {
                    MelonLogger.Msg($"[FallingDebris] Thorn spawned on LEFT (player: {playerX:F2}, thorn: {thornX:F2}, distance: {distance:F2})");
                }
                else
                {
                    MelonLogger.Msg($"[FallingDebris] Thorn spawned on RIGHT (player: {playerX:F2}, thorn: {thornX:F2}, distance: {distance:F2})");
                }

                // Start looping directional sound
                string channelKey = MinigameAudioManager.Instance?.PlayDirectionalLoopingSound(soundFile, thornX, playerPos.y, instanceId.ToString());
                if (!string.IsNullOrEmpty(channelKey))
                {
                    _debrisAudioChannels[instanceId] = channelKey;
                    _activeThorns[instanceId] = __instance; // Track for dynamic updates
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ThornInit_Postfix: {ex.Message}");
            }
        }

        // Clean up when thorn starts being destroyed
        [HarmonyPatch(typeof(MinigameFallingDebris_ObstacleThornController), "StartDestroy")]
        [HarmonyPrefix]
        public static void ThornStartDestroy_Prefix(MinigameFallingDebris_ObstacleThornController __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                int instanceId = __instance.GetInstanceID();
                if (_debrisAudioChannels.ContainsKey(instanceId))
                {
                    MinigameAudioManager.Instance?.StopDirectionalSound(_debrisAudioChannels[instanceId]);
                    _debrisAudioChannels.Remove(instanceId);
                    MelonLogger.Msg($"[FallingDebris] Stopped thorn sound on StartDestroy (id={instanceId})");
                }
                // Remove from active tracking immediately
                _activeThorns.Remove(instanceId);
                MelonLogger.Msg($"[FallingDebris] Removed thorn from tracking (id={instanceId}), active count now: {_activeDebris.Count + _activeThorns.Count}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ThornStartDestroy_Prefix: {ex.Message}");
            }
        }

        // Final cleanup when thorn is destroyed
        [HarmonyPatch(typeof(MinigameFallingDebris_ObstacleThornController), "DestroyObjectOverride")]
        [HarmonyPrefix]
        public static void ThornDestroy_Prefix(MinigameFallingDebris_ObstacleThornController __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                int instanceId = __instance.GetInstanceID();
                // Double-check cleanup
                if (_debrisAudioChannels.ContainsKey(instanceId))
                {
                    MinigameAudioManager.Instance?.StopDirectionalSound(_debrisAudioChannels[instanceId]);
                    _debrisAudioChannels.Remove(instanceId);
                }
                _activeThorns.Remove(instanceId);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ThornDestroy_Prefix: {ex.Message}");
            }
        }

        // Update audio panning/volume dynamically as player moves
        [HarmonyPatch(typeof(MinigameFallingDebris), "UpdateMinigameFallingDebris")]
        [HarmonyPostfix]
        public static void FallingDebrisUpdate_Postfix(float deltaTime)
        {
            if (!AccessibilityMod.IsEnabled || !_fallingDebrisActive)
                return;

            try
            {
                _audioUpdateTimer += deltaTime;

                if (_audioUpdateTimer >= AUDIO_UPDATE_INTERVAL)
                {
                    _audioUpdateTimer = 0f;
                    UpdateObstacleAudio();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in FallingDebrisUpdate_Postfix: {ex.Message}");
            }
        }

        private static void UpdateObstacleAudio()
        {
            try
            {
                var player = com.mojiken.asftu.PlayerController.Instance;
                if (player == null) return;

                float playerX = player.transform.position.x;
                float playerY = player.transform.position.y;

                // Update debris audio
                foreach (var kvp in _activeDebris)
                {
                    if (kvp.Value == null) continue;

                    int instanceId = kvp.Key;
                    var debris = kvp.Value;

                    // Get shadow position (where it will land)
                    var shadow = debris.GetShadow();
                    if (shadow != null)
                    {
                        float debrisX = shadow.transform.position.x;
                        MinigameAudioManager.Instance?.UpdateDirectionalSound(_debrisAudioChannels[instanceId], debrisX, playerX, playerY);
                    }
                }

                // Update thorn audio
                foreach (var kvp in _activeThorns)
                {
                    if (kvp.Value == null) continue;

                    int instanceId = kvp.Key;
                    var thorn = kvp.Value;
                    float thornX = thorn.transform.position.x;

                    MinigameAudioManager.Instance?.UpdateDirectionalSound(_debrisAudioChannels[instanceId], thornX, playerX, playerY);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in UpdateObstacleAudio: {ex.Message}");
            }
        }

        // Limit maximum debris on screen for accessibility
        private const int MAX_DEBRIS_ON_SCREEN = 2;

        // ============================================
        // CLIMB TREE MINIGAME PATCHES
        // ============================================

        private static float _climbProgressTimer = 0f;
        private const float CLIMB_PROGRESS_INTERVAL = 2f;

        [HarmonyPatch(typeof(MiniGameClimbTree), "UpdateDefault")]
        [HarmonyPostfix]
        public static void ClimbTreeUpdate_Postfix(MiniGameClimbTree __instance, float deltaTime)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                var atmaAnimatorField = AccessTools.Field(typeof(MiniGameClimbTree), "atmaAnimator");
                var targetXField = AccessTools.Field(typeof(MiniGameClimbTree), "targetX");
                
                var atmaAnimator = atmaAnimatorField?.GetValue(__instance) as UnityEngine.Component;
                float targetX = (float)(targetXField?.GetValue(__instance) ?? 0f);
                
                if (atmaAnimator != null)
                {
                    float currentX = atmaAnimator.transform.position.x;
                    float progress = 1f - ((currentX - targetX) / currentX);
                    
                    _climbProgressTimer += deltaTime;
                    if (_climbProgressTimer >= CLIMB_PROGRESS_INTERVAL)
                    {
                        _climbProgressTimer = 0f;
                        int percentage = Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f);
                        MinigameAudioManager.Instance?.PlayProgressTone(progress);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ClimbTreeUpdate_Postfix: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(MiniGameClimbTree), "StartMinigameOverride")]
        [HarmonyPostfix]
        public static void ClimbTreeStart_Postfix()
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                _climbProgressTimer = 0f;
                TTSManager.AnnounceUI("Climb tree. Hold left arrow to crawl. Rising tone shows progress.", interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ClimbTreeStart_Postfix: {ex.Message}");
            }
        }

        // ============================================
        // FISHING NET ROD MINIGAME PATCHES  
        // ============================================

        // Track fishing state
        private static bool _hasFishingMinigameStarted = false;
        private static float _fishingBeepTimer = 0f;
        private const float FISHING_BEEP_INTERVAL = 0.15f; // Fast beeps when in safe zone

        [HarmonyPatch(typeof(MinigameFishingNetRod), "StartMinigameOverride")]
        [HarmonyPostfix]
        public static void FishingMinigameStart_Postfix()
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                // Only announce the first time the minigame starts, not on retries
                if (!_hasFishingMinigameStarted)
                {
                    _hasFishingMinigameStarted = true;
                    string buttonName = GetButtonName(PlayerKeybinding.KeyBindingType.Interact);
                    TTSManager.AnnounceUI($"Fishing. Press {buttonName} when cursor is in safe zone. Listen for audio cues.", interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in FishingMinigameStart_Postfix: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(MinigameFishingNetRod), "StopMinigameOverride")]
        [HarmonyPrefix]
        public static void FishingMinigameStop_Prefix()
        {
            _hasFishingMinigameStarted = false;
        }

        // Reuse timing bar audio system for fishing (similar mechanic)
        [HarmonyPatch(typeof(MinigameFishingNetRod), "LateUpdate")]
        [HarmonyPostfix]
        public static void FishingUpdate_Postfix(MinigameFishingNetRod __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                var cursorField = AccessTools.Field(typeof(MinigameFishingNetRod), "cursor");
                var mainBarField = AccessTools.Field(typeof(MinigameFishingNetRod), "mainBar");
                var safeAreaField = AccessTools.Field(typeof(MinigameFishingNetRod), "safeArea");

                var cursor = cursorField?.GetValue(__instance) as UnityEngine.UI.Image;
                var mainBar = mainBarField?.GetValue(__instance) as UnityEngine.UI.Image;
                var safeArea = safeAreaField?.GetValue(__instance) as UnityEngine.UI.Image;

                if (cursor != null && mainBar != null && safeArea != null)
                {
                    float cursorX = cursor.rectTransform.anchoredPosition.x;
                    float barWidth = mainBar.rectTransform.sizeDelta.x;
                    float safeWidth = safeArea.rectTransform.sizeDelta.x;
                    float safeStart = (barWidth - safeWidth) * 0.5f;
                    float safeEnd = safeStart + safeWidth;

                    bool inSafeZone = cursorX >= safeStart && cursorX <= safeEnd;

                    // Update beep timer
                    _fishingBeepTimer -= UnityEngine.Time.deltaTime;

                    if (inSafeZone)
                    {
                        // Play rapid beeps when in safe zone
                        if (_fishingBeepTimer <= 0f)
                        {
                            MinigameAudioManager.Instance?.PlayOneShot(MinigameAudioManager.TIMING_BAR_SUCCESS, 1.0f);
                            _fishingBeepTimer = FISHING_BEEP_INTERVAL;
                        }
                    }
                    else
                    {
                        // Outside safe zone - play continuous tone with pitch based on distance
                        float distance = Mathf.Min(Mathf.Abs(cursorX - safeStart), Mathf.Abs(cursorX - safeEnd));
                        float proximity = 1f - Mathf.Clamp01(distance / (barWidth * 0.5f));
                        float pitch = 0.5f + (proximity * 1.0f); // Range: 0.5 to 1.5
                        MinigameAudioManager.Instance?.PlayLoopingTone(MinigameAudioManager.TIMING_BAR_TONE, pitch);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in FishingUpdate_Postfix: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(MinigameFishingNetRod), "StartNewRound")]
        [HarmonyPrefix]
        public static void FishingStartRound_Prefix()
        {
            // Reset fishing state
            _fishingBeepTimer = 0f;
        }

        // ============================================
        // TREE SHAKING (CHERRY CATCHING) MINIGAME PATCHES
        // ============================================

        private static int _lastCherryCount = 0;
        private static int _lastAtmaPosition = 1;
        private static System.Collections.Generic.HashSet<int> _announcedObjects = new System.Collections.Generic.HashSet<int>();

        [HarmonyPatch(typeof(MinigameGrandpaKersen), "SpawnDroppedObject")]
        [HarmonyPostfix]
        public static void CherrySpawn_Postfix(MinigameGrandpaKersen __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                var droppedObjectListField = AccessTools.Field(typeof(MinigameGrandpaKersen), "droppedObjectList");
                var droppedObjectList = droppedObjectListField?.GetValue(__instance) as System.Collections.IList;
                
                if (droppedObjectList != null && droppedObjectList.Count > 0)
                {
                    // Get the last spawned object
                    var lastObject = droppedObjectList[droppedObjectList.Count - 1] as UnityEngine.MonoBehaviour;
                    
                    if (lastObject != null)
                    {
                        int instanceId = lastObject.GetInstanceID();
                        
                        // Check if already announced
                        if (_announcedObjects.Contains(instanceId))
                            return;
                        
                        _announcedObjects.Add(instanceId);
                        
                        var transform = lastObject.transform;
                        float x = transform.localPosition.x;
                        
                        // Determine position (left, center, or right) based on X coordinate
                        string position;
                        float pan;
                        
                        if (x < -0.5f)
                        {
                            position = "left";
                            pan = -1f;
                        }
                        else if (x > 0.5f)
                        {
                            position = "right";
                            pan = 1f;
                        }
                        else
                        {
                            position = "center";
                            pan = 0f;
                        }
                        
                        // Check if it's a cherry or bad object using reflection on private field
                        var isKersenField = AccessTools.Field(lastObject.GetType(), "isKersen");
                        bool isCherry = (bool)(isKersenField?.GetValue(lastObject) ?? true);
                        
                        // Play spatial audio cue
                        float pitch = isCherry ? 1.3f : 0.5f;
                        MinigameAudioManager.Instance?.PlayButtonSequenceCue("right", pan, pitch);
                        
                        MelonLogger.Msg($"[Cherry] Object spawned at {position} (x={x:F2}, cherry={isCherry}, id={instanceId})");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CherrySpawn_Postfix: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(MinigameGrandpaKersen), "UpdateDefault")]
        [HarmonyPostfix]
        public static void CherryUpdate_Postfix(MinigameGrandpaKersen __instance, float deltaTime)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                var indexCurrentAtmaField = AccessTools.Field(typeof(MinigameGrandpaKersen), "indexCurrentAtma");
                int currentPosition = (int)(indexCurrentAtmaField?.GetValue(__instance) ?? 1);
                
                // Announce position change
                if (currentPosition != _lastAtmaPosition)
                {
                    string positionName = currentPosition == 0 ? "left" : (currentPosition == 2 ? "right" : "center");
                    TTSManager.AnnounceUI(positionName, interrupt: false);
                    _lastAtmaPosition = currentPosition;
                }
                
                // Announce cherry count updates
                var kersenCounterField = AccessTools.Field(typeof(MinigameGrandpaKersen), "kersenCounter");
                var kersenTargetField = AccessTools.Field(typeof(MinigameGrandpaKersen), "kersenCounterTarget");
                
                int currentCount = (int)(kersenCounterField?.GetValue(__instance) ?? 0);
                int targetCount = (int)(kersenTargetField?.GetValue(__instance) ?? 10);
                
                if (currentCount != _lastCherryCount)
                {
                    int remaining = targetCount - currentCount;
                    TTSManager.AnnounceUI($"{remaining} cherries remaining", interrupt: true);
                    _lastCherryCount = currentCount;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CherryUpdate_Postfix: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(MinigameGrandpaKersen), "StartMinigameOverride")]
        [HarmonyPostfix]
        public static void CherryStart_Postfix(MinigameGrandpaKersen __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                var kersenTargetField = AccessTools.Field(typeof(MinigameGrandpaKersen), "kersenCounterTarget");
                int targetCount = (int)(kersenTargetField?.GetValue(__instance) ?? 10);
                
                _lastCherryCount = 0;
                _lastAtmaPosition = 1;
                _announcedObjects.Clear();
                
                TTSManager.AnnounceUI($"Cherry catching. Use left and right arrows to move. Catch {targetCount} cherries. Avoid bad objects. High pitch sound is cherry, low pitch is bad.", interrupt: true);
                MelonLogger.Msg($"[Cherry] Minigame started, target: {targetCount}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CherryStart_Postfix: {ex.Message}");
            }
        }

        // ============================================
        // ZOOM/EVIDENCE MINIGAME PATCHES
        // ============================================

        [HarmonyPatch(typeof(MinigameZoom), "UpdateDefault")]
        [HarmonyPostfix]
        public static void ZoomUpdate_Postfix(MinigameZoom __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                // This minigame shows a menu with points of interest
                // Menu navigation is already handled by SpaceDialogMenuBox patches
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ZoomUpdate_Postfix: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(MinigameZoom), "StartMinigameOverride")]
        [HarmonyPostfix]
        public static void ZoomStart_Postfix()
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                TTSManager.AnnounceUI("Examine evidence. Navigate menu to select points of interest.", interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ZoomStart_Postfix: {ex.Message}");
            }
        }
    }
}
