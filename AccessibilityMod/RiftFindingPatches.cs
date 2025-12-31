using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using com.mojiken.asftu;

namespace AsftuAccessibilityMod.Patches
{
    /// <summary>
    /// Patches for the Rift Finding minigame to make it accessible
    /// </summary>
    [HarmonyPatch]
    public static class RiftFindingPatches
    {
        private static bool _isInRiftFindingMode = false;
        private static MinigameRiftFinding_Rift _activeRift = null;
        private static Coroutine _riftAudioCoroutine = null;

        // Track button sequence announcement for rift entry
        private static bool _hasAnnouncedCurrentSequence = false;

        // Track wand prompt announcement
        private static bool _hasAnnouncedWandPrompt = false;
        private static float _wandPromptCooldown = 0f;
        private const float WAND_PROMPT_COOLDOWN_TIME = 5f; // Announce every 5 seconds while in rift finding mode

        // ============================================
        // RIFT FINDING MINIGAME PATCHES
        // ============================================

        /// <summary>
        /// Patch to detect when rift finding minigame starts
        /// </summary>
        [HarmonyPatch(typeof(MinigameRiftFinding), "StartMinigameOverride")]
        [HarmonyPostfix]
        public static void RiftFindingStart_Postfix(MinigameRiftFinding __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                _isInRiftFindingMode = true;
                _activeRift = null;
                _hasAnnouncedCurrentSequence = false;
                _hasAnnouncedWandPrompt = false;
                _wandPromptCooldown = 0f;

                TTSManager.AnnounceUI("Rift finding mode activated. Use Magic Wand to detect rifts.", interrupt: true);
                MelonLogger.Msg("[RiftFinding] Rift finding minigame started");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in RiftFindingStart_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch to detect when rift finding minigame stops (using OnDisable)
        /// </summary>
        [HarmonyPatch(typeof(MinigameRiftFinding), "OnDisable")]
        [HarmonyPrefix]
        public static void RiftFindingOnDisable_Prefix(MinigameRiftFinding __instance)
        {
            if (!_isInRiftFindingMode)
                return;

            try
            {
                _isInRiftFindingMode = false;
                _activeRift = null;
                _hasAnnouncedCurrentSequence = false;
                _hasAnnouncedWandPrompt = false;

                // Stop any active rift audio
                if (_riftAudioCoroutine != null)
                {
                    __instance.StopCoroutine(_riftAudioCoroutine);
                    _riftAudioCoroutine = null;
                }

                MelonLogger.Msg("[RiftFinding] Rift finding minigame stopped");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in RiftFindingOnDisable_Prefix: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch to detect when a rift is activated (detected by wand)
        /// </summary>
        [HarmonyPatch(typeof(MinigameRiftFinding_Rift), "Activate")]
        [HarmonyPostfix]
        public static void RiftActivate_Postfix(MinigameRiftFinding_Rift __instance)
        {
            if (!AccessibilityMod.IsEnabled || !_isInRiftFindingMode)
                return;

            try
            {
                _activeRift = __instance;

                // Calculate direction relative to player
                Vector3 playerPos = PlayerController.Instance.transform.position;
                Vector3 riftPos = __instance.transform.position;
                string direction = GetDirectionString(playerPos, riftPos);

                TTSManager.AnnounceUI($"Rift detected on your {direction}. Move closer to enter.", interrupt: true);

                // Find the MinigameRiftFinding instance to start coroutine
                var riftMinigame = UnityEngine.Object.FindObjectOfType<MinigameRiftFinding>();
                if (riftMinigame != null)
                {
                    // Stop previous coroutine if running
                    if (_riftAudioCoroutine != null)
                    {
                        riftMinigame.StopCoroutine(_riftAudioCoroutine);
                    }

                    // Start playing directional audio
                    _riftAudioCoroutine = riftMinigame.StartCoroutine(PlayRiftDirectionalAudio(__instance));
                }

                MelonLogger.Msg($"[RiftFinding] Rift activated, direction: {direction}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in RiftActivate_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch to detect when rift deactivates
        /// </summary>
        [HarmonyPatch(typeof(MinigameRiftFinding_Rift), "Deactivate")]
        [HarmonyPostfix]
        public static void RiftDeactivate_Postfix(MinigameRiftFinding_Rift __instance)
        {
            try
            {
                if (_activeRift == __instance)
                {
                    _activeRift = null;

                    // Stop audio coroutine
                    if (_riftAudioCoroutine != null)
                    {
                        var riftMinigame = UnityEngine.Object.FindObjectOfType<MinigameRiftFinding>();
                        if (riftMinigame != null)
                        {
                            riftMinigame.StopCoroutine(_riftAudioCoroutine);
                            _riftAudioCoroutine = null;
                        }
                    }

                    MelonLogger.Msg("[RiftFinding] Active rift deactivated");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in RiftDeactivate_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Coroutine to play continuous directional audio for active rift
        /// </summary>
        private static IEnumerator PlayRiftDirectionalAudio(MinigameRiftFinding_Rift rift)
        {
            MelonLogger.Msg("[RiftFinding] Starting rift directional audio");

            while (rift != null && !rift.activatedVariable.RuntimeValue && _isInRiftFindingMode && _activeRift == rift)
            {
                try
                {
                    // Don't play audio during dialogue
                    bool isDialogueActive = Fungus.SayDialog.ActiveSayDialog != null && Fungus.SayDialog.ActiveSayDialog.gameObject.activeSelf;

                    if (!isDialogueActive && !AsftuMainManager.NeedToPauseGame)
                    {
                        Vector3 playerPos = PlayerController.Instance.transform.position;
                        Vector3 riftPos = rift.transform.position;

                        // Calculate horizontal difference for stereo panning
                        float horizontalDiff = riftPos.x - playerPos.x;
                        float distance = Vector3.Distance(playerPos, riftPos);

                        // Calculate pan (-1 = left, 0 = center, 1 = right)
                        // Reduced divisor for stronger stereo effect
                        float pan = Mathf.Clamp(horizontalDiff / 2f, -1f, 1f);

                        // Calculate pitch based on distance (closer = higher pitch)
                        float normalizedDistance = Mathf.Clamp01(1f - (distance / 10f));
                        float pitch = 0.8f + (normalizedDistance * 0.7f); // Range: 0.8 to 1.5

                        // Play directional audio cue
                        MinigameAudioManager.Instance?.PlayButtonSequenceCue("right", pan, pitch);

                        MelonLogger.Msg($"[RiftFinding] Rift audio - pan: {pan:F2}, pitch: {pitch:F2}, dist: {distance:F2}");
                    }
                    else
                    {
                        MelonLogger.Msg($"[RiftFinding] Audio blocked - dialogue: {isDialogueActive}, paused: {AsftuMainManager.NeedToPauseGame}");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in PlayRiftDirectionalAudio: {ex.Message}");
                }

                yield return new WaitForSeconds(0.5f);
            }

            MelonLogger.Msg("[RiftFinding] Stopped rift directional audio");
            _riftAudioCoroutine = null;
        }

        /// <summary>
        /// Patch UpdateDefault to announce wand prompt periodically
        /// </summary>
        [HarmonyPatch(typeof(MinigameRiftFinding), "UpdateDefault")]
        [HarmonyPostfix]
        public static void RiftFindingUpdate_Postfix(MinigameRiftFinding __instance, float deltaTime)
        {
            if (!AccessibilityMod.IsEnabled || !_isInRiftFindingMode)
                return;

            try
            {
                // Get the state using reflection
                var stateField = AccessTools.Field(typeof(MinigameRiftFinding), "state");
                var stateValue = stateField?.GetValue(__instance);

                if (stateValue != null && stateValue.ToString() == "FindingRift")
                {
                    // Update cooldown timer
                    if (_wandPromptCooldown > 0f)
                    {
                        _wandPromptCooldown -= deltaTime;
                    }

                    // Announce wand prompt if player can interact and no active rift
                    if (PlayerController.Instance.IsReadyToInteract && _activeRift == null && _wandPromptCooldown <= 0f)
                    {
                        if (!_hasAnnouncedWandPrompt)
                        {
                            string buttonName = GetSpecialButtonName();
                            TTSManager.AnnounceUI($"Press {buttonName} to activate Magic Wand", interrupt: false);
                            _hasAnnouncedWandPrompt = true;
                            _wandPromptCooldown = WAND_PROMPT_COOLDOWN_TIME;
                            MelonLogger.Msg($"[RiftFinding] Announced wand prompt: Press {buttonName}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in RiftFindingUpdate_Postfix: {ex.Message}");
            }
        }

        // ============================================
        // RIFT BUTTON SEQUENCE PATCHES
        // ============================================

        /// <summary>
        /// Patch to announce when entering rift button sequence
        /// </summary>
        [HarmonyPatch(typeof(MinigameRiftFinding), "StartMinigameButtonSequence")]
        [HarmonyPostfix]
        public static void StartRiftButtonSequence_Postfix(MinigameRiftFinding __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                _hasAnnouncedCurrentSequence = false;

                // Stop directional audio when entering sequence mode
                if (_riftAudioCoroutine != null)
                {
                    __instance.StopCoroutine(_riftAudioCoroutine);
                    _riftAudioCoroutine = null;
                }

                // Delay the announcement slightly to ensure sequence is generated
                __instance.StartCoroutine(AnnounceRiftSequenceDelayed(__instance));

                MelonLogger.Msg("[RiftFinding] Starting rift button sequence");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in StartRiftButtonSequence_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Delayed announcement of rift button sequence
        /// </summary>
        private static IEnumerator AnnounceRiftSequenceDelayed(MinigameRiftFinding instance)
        {
            yield return new WaitForSeconds(0.25f);

            if (_hasAnnouncedCurrentSequence)
                yield break;

            _hasAnnouncedCurrentSequence = true;

            try
            {
                // Get the current sequence using reflection
                var currentSequenceField = AccessTools.Field(typeof(MinigameButtonSequence), "currentSequence");
                var currentSequence = currentSequenceField?.GetValue(instance) as System.Collections.IList;

                if (currentSequence != null && currentSequence.Count > 0)
                {
                    // Build the sequence string
                    List<string> buttonNames = new List<string>();

                    for (int i = 0; i < currentSequence.Count; i++)
                    {
                        var inputObject = currentSequence[i];
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
                                    string keyName = keyList[0].ToString();
                                    string buttonName = GetButtonNameForRift(keyName);
                                    buttonNames.Add(buttonName);
                                }
                            }
                        }
                    }

                    if (buttonNames.Count > 0)
                    {
                        string sequence = string.Join(", ", buttonNames.ToArray());
                        TTSManager.AnnounceUI($"Enter rift. Sequence: {sequence}", interrupt: true);
                        MelonLogger.Msg($"[RiftFinding] Announced rift sequence: {sequence}");
                    }
                    else
                    {
                        TTSManager.AnnounceUI("Enter rift. Press arrow keys.", interrupt: true);
                    }
                }
                else
                {
                    TTSManager.AnnounceUI("Enter rift. Press arrow keys.", interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in AnnounceRiftSequenceDelayed: {ex.Message}");
                TTSManager.AnnounceUI("Enter rift. Press arrow keys.", interrupt: true);
            }
        }

        /// <summary>
        /// Announce when rift sequence succeeds
        /// </summary>
        [HarmonyPatch(typeof(MinigameRiftFinding), "SequenceSuccess")]
        [HarmonyPrefix]
        public static void RiftSequenceSuccess_Prefix(MinigameRiftFinding __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                // Get current rift level
                var currentTargetRiftField = AccessTools.Field(typeof(MinigameRiftFinding), "currentTargetRift");
                var currentRift = currentTargetRiftField?.GetValue(__instance) as MinigameRiftFinding_Rift;

                if (currentRift != null)
                {
                    int level = currentRift.level;
                    int maxLevel = currentRift.levelMax;

                    if (level + 1 >= maxLevel)
                    {
                        TTSManager.AnnounceUI("Rift opening successful!", interrupt: true);
                    }
                    else
                    {
                        int remaining = maxLevel - (level + 1);
                        TTSManager.AnnounceUI($"{remaining} more to open.", interrupt: true);
                        _hasAnnouncedCurrentSequence = false; // Reset for next sequence
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in RiftSequenceSuccess_Prefix: {ex.Message}");
            }
        }

        /// <summary>
        /// Announce when rift sequence fails (patch parent class method but only act on RiftFinding)
        /// </summary>
        [HarmonyPatch(typeof(MinigameButtonSequence), "MinigameFail")]
        [HarmonyPostfix]
        public static void ButtonSequenceMinigameFail_Postfix(MinigameButtonSequence __instance)
        {
            if (!AccessibilityMod.IsEnabled || !_isInRiftFindingMode)
                return;

            // Only handle if this is a MinigameRiftFinding instance
            if (!(__instance is MinigameRiftFinding))
                return;

            try
            {
                TTSManager.AnnounceUI("Failed! Rift closed. Find it again.", interrupt: true);
                _activeRift = null;
                _hasAnnouncedCurrentSequence = false;
                MelonLogger.Msg("[RiftFinding] Rift sequence failed");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ButtonSequenceMinigameFail_Postfix: {ex.Message}");
            }
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        /// <summary>
        /// Get the Special button name (for Magic Wand activation)
        /// </summary>
        private static string GetSpecialButtonName()
        {
            try
            {
                var keybinding = InputManager.Instance?.playerkeybind;
                if (keybinding == null)
                    return "special button";

                var action = keybinding.GetKeyBinding(PlayerKeybinding.KeyBindingType.Special);
                if (action == null)
                    return "special button";

                // Get the device class
                var deviceClass = keybinding.LastDeviceClass;
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
                                return GetFriendlyKeyName(key);
                            }
                        }
                        else
                        {
                            // Controller button
                            return binding.Name;
                        }
                    }
                }

                return "special button";
            }
            catch
            {
                return "special button";
            }
        }

        /// <summary>
        /// Get friendly key name for keyboard keys
        /// </summary>
        private static string GetFriendlyKeyName(InControl.Key key)
        {
            // Convert enum to string and make it more readable
            string keyName = key.ToString();

            // Handle special cases
            switch (keyName)
            {
                case "LeftShift": return "Left Shift";
                case "RightShift": return "Right Shift";
                case "LeftControl": return "Left Control";
                case "RightControl": return "Right Control";
                case "LeftAlt": return "Left Alt";
                case "RightAlt": return "Right Alt";
                case "Space": return "Spacebar";
                default: return keyName;
            }
        }

        /// <summary>
        /// Get direction string relative to player
        /// </summary>
        private static string GetDirectionString(Vector3 playerPos, Vector3 targetPos)
        {
            float horizontalDiff = targetPos.x - playerPos.x;

            if (Mathf.Abs(horizontalDiff) < 0.5f)
                return "center";
            else if (horizontalDiff > 0)
                return "right";
            else
                return "left";
        }

        /// <summary>
        /// Get button name for TTS (rift-specific)
        /// </summary>
        private static string GetButtonNameForRift(string keyName)
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
                default:
                    return keyName.ToLower();
            }
        }
    }
}
