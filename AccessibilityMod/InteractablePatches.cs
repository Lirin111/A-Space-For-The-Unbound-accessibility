using System;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using com.mojiken.asftu;
using InControl;

namespace AsftuAccessibilityMod.Patches
{
    /// <summary>
    /// Patches for interactable objects to announce when player is near them
    /// </summary>
    [HarmonyPatch]
    public static class InteractablePatches
    {
        private static InteractableObject _lastAnnouncedInteractable = null;
        private static float _lastInteractableAnnounceTime = 0f;
        private static float _interactableAnnounceDelay = 10.0f; // 10 seconds delay between announcements

        // Track last announced spacedive NPCs
        private static NPCController _lastAnnouncedSpacediveNPC = null;
        private static float _lastSpacediveAnnounceTime = 0f;

        // Track door states for the puzzle
        private static System.Collections.Generic.Dictionary<string, bool> _doorStates = new System.Collections.Generic.Dictionary<string, bool>();
        private static float _lastDoorCheckTime = 0f;
        private static bool _isDoorMonitoringActive = false;

        // Patch CharacterInteract.setInteract to announce when interactable becomes available
        [HarmonyPatch(typeof(CharacterInteract), nameof(CharacterInteract.setInteract))]
        [HarmonyPostfix]
        public static void CharacterInteract_setInteract_Postfix(CharacterInteract __instance, InteractableObject theObject)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceInteractables)
                return;

            try
            {
                if (theObject == null)
                {
                    _lastAnnouncedInteractable = null; // Clear when no object
                    return;
                }

                float currentTime = UnityEngine.Time.unscaledTime;

                // Don't re-announce the same object too quickly
                if (_lastAnnouncedInteractable == theObject && 
                    (currentTime - _lastInteractableAnnounceTime) < _interactableAnnounceDelay)
                {
                    return;
                }

                string objectName = GetInteractableObjectName(theObject);

                // Skip numbered valves (Valve0-10) - they are handled by ValvePuzzlePatches
                // But allow ValveA, ValveB, etc. (items) to be announced normally
                if (!string.IsNullOrEmpty(objectName) &&
                    System.Text.RegularExpressions.Regex.IsMatch(theObject.gameObject.name, @"^Valve\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    return; // Let ValvePuzzlePatches handle numbered valve announcements
                }

                string buttonPrompt = GetButtonPrompt(theObject);
                InteractableType audioType = GetInteractableAudioType(theObject);

                // Start looping spatial audio for interactable
                InteractableAudioManager.Instance.StartInteractableAudio(theObject, audioType);

                // Try to get switch/lever/fuse/door/gate/cage status
                string statusAnnouncement = null;
                if (!string.IsNullOrEmpty(objectName) &&
                    (objectName.ToLower().Contains("lever") ||
                     objectName.ToLower().Contains("switch") ||
                     objectName.ToLower().Contains("fuse") ||
                     objectName.ToLower().Contains("door") ||
                     objectName.ToLower().Contains("gate") ||
                     objectName.ToLower().Contains("cage")))
                {
                    string status = GetSwitchLeverStatus(theObject, objectName);
                    statusAnnouncement = $"Status: {status}. ";
                }

                // Format announcement: "ObjectName, status, press A"
                string announcement = !string.IsNullOrWhiteSpace(objectName)
                    ? $"{objectName}. {(statusAnnouncement ?? "")} {buttonPrompt}"
                    : buttonPrompt;

                TTSManager.Speak(announcement, interrupt: false, priority: 7);
                _lastAnnouncedInteractable = theObject;
                _lastInteractableAnnounceTime = currentTime;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CharacterInteract_setInteract_Postfix: {ex.Message}");
            }
        }

        // Patch CharacterInteract.clearInteract to announce when leaving interactable
        [HarmonyPatch(typeof(CharacterInteract), nameof(CharacterInteract.clearInteract))]
        [HarmonyPostfix]
        public static void CharacterInteract_clearInteract_Postfix(CharacterInteract __instance)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceInteractables)
                return;

            try
            {
                // Clear the last announced interactable when leaving
                _lastAnnouncedInteractable = null;
                
                // Stop spatial audio
                InteractableAudioManager.Instance.StopInteractableAudio();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CharacterInteract_clearInteract_Postfix: {ex.Message}");
            }
        }

        // NOTE: Removed Interact patches for InteractableObject_OpenInteractionMenu and InteractableObject_FungusMessage
        // to prevent double announcements. The setInteract patch already announces when the player gets near
        // the object, so we don't need to announce again when they interact with it.

        // Patch CharacterInteract Update to monitor door status changes when near levers
        [HarmonyPatch(typeof(CharacterInteract), "Update")]
        [HarmonyPostfix]
        public static void CharacterInteract_Update_Postfix(CharacterInteract __instance)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceInteractables)
                return;

            try
            {
                float currentTime = UnityEngine.Time.unscaledTime;

                // Check if we're near a lever
                if (__instance.ToInteract != null)
                {
                    string objectName = GetInteractableObjectName(__instance.ToInteract);
                    if (!string.IsNullOrEmpty(objectName) && objectName.ToLower().Contains("lever"))
                    {
                        // Activate door monitoring
                        if (!_isDoorMonitoringActive)
                        {
                            _isDoorMonitoringActive = true;
                            UpdateDoorStates(); // Initialize current states
                            MelonLogger.Msg("[Door Monitor] Activated - near a lever");
                        }

                        // Check door states every 0.3 seconds when near a lever
                        if (currentTime - _lastDoorCheckTime > 0.3f)
                        {
                            _lastDoorCheckTime = currentTime;
                            CheckAndAnnounceDoorChanges();
                        }
                    }
                }
                else if (_isDoorMonitoringActive)
                {
                    // Deactivate when not near any lever
                    _isDoorMonitoringActive = false;
                    _doorStates.Clear();
                    MelonLogger.Msg("[Door Monitor] Deactivated - left lever area");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CharacterInteract_Update_Postfix: {ex.Message}");
            }
        }

        private static void UpdateDoorStates()
        {
            try
            {
                var flowcharts = UnityEngine.Object.FindObjectsOfType<Fungus.Flowchart>();

                foreach (var flowchart in flowcharts)
                {
                    if (flowchart == null) continue;

                    var varNames = flowchart.GetVariableNames();
                    if (varNames != null)
                    {
                        foreach (var vName in varNames)
                        {
                            if (vName.Contains("cage") && vName.Contains("Fuse") && vName.Contains("Open"))
                            {
                                var boolVar = flowchart.GetVariable<Fungus.BooleanVariable>(vName);
                                if (boolVar != null)
                                {
                                    _doorStates[vName] = boolVar.Value;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in UpdateDoorStates: {ex.Message}");
            }
        }

        private static void CheckAndAnnounceDoorChanges()
        {
            try
            {
                var flowcharts = UnityEngine.Object.FindObjectsOfType<Fungus.Flowchart>();
                System.Collections.Generic.List<string> changes = new System.Collections.Generic.List<string>();

                foreach (var flowchart in flowcharts)
                {
                    if (flowchart == null) continue;

                    var varNames = flowchart.GetVariableNames();
                    if (varNames != null)
                    {
                        foreach (var vName in varNames)
                        {
                            if (vName.Contains("cage") && vName.Contains("Fuse") && vName.Contains("Open"))
                            {
                                var boolVar = flowchart.GetVariable<Fungus.BooleanVariable>(vName);
                                if (boolVar != null)
                                {
                                    bool currentState = boolVar.Value;
                                    bool hadPreviousState = _doorStates.ContainsKey(vName);
                                    bool previousState = hadPreviousState ? _doorStates[vName] : false;

                                    // Check if state changed
                                    if (!hadPreviousState || currentState != previousState)
                                    {
                                        _doorStates[vName] = currentState;

                                        // Extract fuse letter
                                        string fuseLetter = System.Text.RegularExpressions.Regex.Match(vName, @"Fuse([A-E])").Groups[1].Value;
                                        if (!string.IsNullOrEmpty(fuseLetter))
                                        {
                                            if (currentState && (!hadPreviousState || !previousState))
                                            {
                                                changes.Add($"Fuse {fuseLetter} door opened");
                                                MelonLogger.Msg($"[Door Monitor] {vName} opened (was: {previousState}, now: {currentState})");
                                            }
                                            else if (!currentState && hadPreviousState && previousState)
                                            {
                                                changes.Add($"Fuse {fuseLetter} door closed");
                                                MelonLogger.Msg($"[Door Monitor] {vName} closed (was: {previousState}, now: {currentState})");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Announce all changes
                if (changes.Count > 0)
                {
                    string announcement = string.Join(". ", changes);
                    TTSManager.Speak(announcement, interrupt: false, priority: 8);
                    MelonLogger.Msg($"[Door Changes] {announcement}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CheckAndAnnounceDoorChanges: {ex.Message}");
            }
        }

        // Helper method to get switch/lever/fuse status
        private static string GetSwitchLeverStatus(InteractableObject obj, string objectName)
        {
            if (obj == null || string.IsNullOrEmpty(objectName))
                return "status unknown";

            try
            {
                // Strategy 1: Try to find Fungus flowchart variables based on object name
                // Common patterns: "Lever A" -> "LeverA", "Fuse D" -> "FuseD", etc.
                string variableName = objectName.Replace(" ", "").Replace("_", "");

                // Find all active flowcharts in the scene
                var flowcharts = UnityEngine.Object.FindObjectsOfType<Fungus.Flowchart>();

                MelonLogger.Msg($"[Switch Status] Looking for status of '{objectName}' (variable name: '{variableName}')");
                MelonLogger.Msg($"[Switch Status] Found {flowcharts.Length} flowcharts in scene");

                foreach (var flowchart in flowcharts)
                {
                    if (flowchart == null) continue;

                    // Get all available variables in this flowchart
                    var varNames = flowchart.GetVariableNames();
                    if (varNames != null && varNames.Length > 0)
                    {
                        // Look for variables that contain our variable name
                        // Pattern: ch3_TeacherAgung_LeverA, ch3_TeacherAgung_LeverB, etc.
                        // Also: cageItemFuseDOpen, cageFuseSlotDOpen, etc.
                        foreach (var vName in varNames)
                        {
                            // Check for exact match or suffix match
                            bool isMatch = vName.EndsWith(variableName, StringComparison.OrdinalIgnoreCase);

                            // Also check for cage/door patterns (e.g., "Fuse D" -> cageItemFuseDOpen)
                            if (!isMatch && (objectName.ToLower().Contains("fuse") ||
                                            objectName.ToLower().Contains("cage") ||
                                            objectName.ToLower().Contains("door")))
                            {
                                // Extract letter/number from object name (e.g., "D" from "Fuse D")
                                string letter = System.Text.RegularExpressions.Regex.Match(objectName, @"[A-E]").Value;
                                if (!string.IsNullOrEmpty(letter))
                                {
                                    isMatch = vName.Contains("cage") && vName.Contains("Fuse" + letter) && vName.Contains("Open");
                                }
                            }

                            if (isMatch)
                            {
                                // Try boolean variable first
                                var boolVar = flowchart.GetVariable<Fungus.BooleanVariable>(vName);
                                if (boolVar != null)
                                {
                                    MelonLogger.Msg($"[Switch Status] Found bool variable '{vName}' = {boolVar.Value}");

                                    // For levers: inverted logic (False = on, True = off)
                                    // For doors/cages: normal logic (True = open, False = closed)
                                    if (vName.ToLower().Contains("lever"))
                                    {
                                        return boolVar.Value ? "off (red)" : "on (green)";
                                    }
                                    else if (vName.ToLower().Contains("open") || vName.ToLower().Contains("cage"))
                                    {
                                        return boolVar.Value ? "open (unlocked)" : "locked (closed)";
                                    }
                                    else
                                    {
                                        return boolVar.Value ? "on (green)" : "off (red)";
                                    }
                                }

                                // Try integer variable
                                var intVar = flowchart.GetVariable<Fungus.IntegerVariable>(vName);
                                if (intVar != null)
                                {
                                    MelonLogger.Msg($"[Switch Status] Found int variable '{vName}' = {intVar.Value}");

                                    // For levers: inverted logic
                                    if (vName.ToLower().Contains("lever"))
                                    {
                                        return intVar.Value > 0 ? "off (red)" : "on (green)";
                                    }
                                    else
                                    {
                                        return intVar.Value > 0 ? "on (green)" : "off (red)";
                                    }
                                }
                            }
                        }
                    }

                    // Try exact match as fallback
                    var boolVarExact = flowchart.GetVariable<Fungus.BooleanVariable>(variableName);
                    if (boolVarExact != null)
                    {
                        MelonLogger.Msg($"[Switch Status] Found bool variable '{variableName}' = {boolVarExact.Value}");
                        // Note: The variable logic is inverted - False = on (green), True = off (red)
                        return boolVarExact.Value ? "off (red)" : "on (green)";
                    }

                    // Try integer variable as fallback
                    var intVarExact = flowchart.GetVariable<Fungus.IntegerVariable>(variableName);
                    if (intVarExact != null)
                    {
                        MelonLogger.Msg($"[Switch Status] Found int variable '{variableName}' = {intVarExact.Value}");
                        // Note: The variable logic is inverted - 0 = on (green), >0 = off (red)
                        return intVarExact.Value > 0 ? "off (red)" : "on (green)";
                    }
                }

                // Strategy 2: Check properties on the object itself
                var type = obj.GetType();
                var isOnProp = type.GetProperty("isOn");
                if (isOnProp != null)
                {
                    try
                    {
                        bool isOn = (bool)isOnProp.GetValue(obj, null);
                        return isOn ? "on (green)" : "off (red)";
                    }
                    catch { }
                }

                // Strategy 3: Check for SpriteRenderer color indicators
                var srs = obj.GetComponentsInChildren<UnityEngine.SpriteRenderer>(true);
                foreach (var sr in srs)
                {
                    // Skip if sprite name doesn't suggest it's an indicator
                    if (sr.name.ToLower().Contains("indicator") ||
                        sr.name.ToLower().Contains("light") ||
                        sr.name.ToLower().Contains("status"))
                    {
                        var color = sr.color;
                        // Check if it's strongly green or red
                        if (color.g > 0.6f && color.g > color.r + 0.2f && color.g > color.b + 0.2f)
                        {
                            return "on (green)";
                        }
                        if (color.r > 0.6f && color.r > color.g + 0.2f && color.r > color.b + 0.2f)
                        {
                            return "off (red)";
                        }
                    }
                }

                // Fallback: Check all sprite renderers (less reliable)
                foreach (var sr in srs)
                {
                    var color = sr.color;
                    if (color.g > 0.7f && color.g > color.r + 0.3f && color.g > color.b + 0.3f)
                    {
                        return "on (green)";
                    }
                    if (color.r > 0.7f && color.r > color.g + 0.3f && color.r > color.b + 0.3f)
                    {
                        return "off (red)";
                    }
                }

                return "status unknown";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting switch/lever status for {objectName}: {ex.Message}");
                return "status unknown";
            }
        }

        // Helper method to get interactable object name
        private static string GetInteractableObjectName(InteractableObject obj)
        {
            if (obj == null)
                return "";

            try
            {
                // For MoveScene objects (doors/stairs), return empty string to avoid announcing area names
                if (obj is InteractableObject_MoveScene)
                {
                    return "";
                }

                // Try to get from game object name
                string name = obj.gameObject.name;
                
                // Filter out area transition objects by name patterns
                if (name.Contains("KeyArea") || name.Contains("Key Area") || 
                    name.Contains("Enter Area") || name.Contains("Exit Area") ||
                    name.StartsWith("Area") || name.Contains("Transition"))
                {
                    return "";
                }
                
                // Special handling for reliefs - check BEFORE cleaning up the name
                // Reliefs have numbers that we need to preserve (ArcaLulu4, ArcaLulu5, etc.)
                if (name.ToLower().Contains("relief") || name.ToLower().Contains("arca"))
                {
                    // Remove "(Clone)" but keep the numbers for reliefs
                    string reliefName = System.Text.RegularExpressions.Regex.Replace(name, @"\(Clone\)", "").Trim();
                    return GetReliefName(obj, reliefName);
                }

                // Clean up Unity naming (remove Clone, numbers, etc.)
                name = System.Text.RegularExpressions.Regex.Replace(name, @"\(Clone\)", "");
                name = System.Text.RegularExpressions.Regex.Replace(name, @"\d+$", "");
                name = name.Trim();

                // Special handling for lamps - check if lit or unlit
                if (name.ToLower().Contains("lamp") || name.ToLower().Contains("lampu"))
                {
                    string lampState = GetLampState(obj);
                    return lampState; // Returns "lamp lit" or "lamp unlit"
                }

                // Try to get character name if it's a character
                if (obj is InteractableObject_OpenInteractionMenu interactionMenu)
                {
                    var character = obj.GetComponent<AsftuCharacter>();
                    if (character != null && character.characterName != null)
                    {
                        // Use the built-in GetName method which handles localization
                        string characterNameText = character.characterName.GetName();
                        if (!string.IsNullOrWhiteSpace(characterNameText))
                            return characterNameText;
                        
                        // Fallback to base name
                        return character.characterName.name;
                    }
                }

                // Format the name to be more readable
                name = FormatObjectName(name);
                
                return name;
            }
            catch
            {
                return "Object";
            }
        }

        // Helper method to get just the button prompt (shorter format: "press A")
        private static string GetButtonPrompt(InteractableObject obj)
        {
            if (obj == null)
                return "press interact button";

            try
            {
                string buttonName = "";
                string iconValue = "";
                
                // Check if it's a InteractableObject_MoveScene (doors/stairs)
                if (obj is InteractableObject_MoveScene moveScene)
                {
                    iconValue = moveScene.signDirection.ToString();
                }
                else
                {
                    iconValue = obj.icon.ToString();
                }
                
                // Also check inputKeyType for Special button (Magic Wand)
                if (obj.inputKeyType == InputKeyType.Special)
                {
                    buttonName = GetButtonName(PlayerKeybinding.KeyBindingType.Special);
                    return $"press {buttonName} to activate Magic Wand";
                }

                switch (iconValue)
                {
                    case "signUp":
                        buttonName = GetButtonName(PlayerKeybinding.KeyBindingType.Up);
                        return $"press {buttonName}";
                    case "signDown":
                        buttonName = GetButtonName(PlayerKeybinding.KeyBindingType.Down);
                        return $"press {buttonName}";
                    case "signLeft":
                        buttonName = GetButtonName(PlayerKeybinding.KeyBindingType.Left);
                        return $"press {buttonName}";
                    case "signRight":
                        buttonName = GetButtonName(PlayerKeybinding.KeyBindingType.Right);
                        return $"press {buttonName}";
                    case "signInteract":
                    case "signCompanion":
                    default:
                        buttonName = GetButtonName(PlayerKeybinding.KeyBindingType.Interact);
                        return $"press {buttonName}";
                }
            }
            catch
            {
                return "press interact button";
            }
        }

        // Helper method to get interaction type based on icon (kept for backward compatibility)
        private static string GetInteractionType(InteractableObject obj)
        {
            if (obj == null)
                return "Interact";

            try
            {
                // Get the actual button name for the action
                string buttonName = "";
                string iconValue = "";
                
                // Check if it's a InteractableObject_MoveScene (doors/stairs)
                if (obj is InteractableObject_MoveScene moveScene)
                {
                    iconValue = moveScene.signDirection.ToString();
                    MelonLogger.Msg($"[Interactable] MoveScene SignDirection: {iconValue}");
                }
                else
                {
                    // Otherwise use the icon field
                    iconValue = obj.icon.ToString();
                    MelonLogger.Msg($"[Interactable] Icon type: {iconValue}");
                }
                
                switch (iconValue)
                {
                    case "signUp":
                        buttonName = GetButtonName(PlayerKeybinding.KeyBindingType.Up);
                        return $"Press {buttonName} to enter";
                    case "signDown":
                        buttonName = GetButtonName(PlayerKeybinding.KeyBindingType.Down);
                        return $"Press {buttonName} to exit";
                    case "signLeft":
                        buttonName = GetButtonName(PlayerKeybinding.KeyBindingType.Left);
                        return $"Press {buttonName}";
                    case "signRight":
                        buttonName = GetButtonName(PlayerKeybinding.KeyBindingType.Right);
                        return $"Press {buttonName}";
                    case "signInteract":
                        buttonName = GetButtonName(PlayerKeybinding.KeyBindingType.Interact);
                        return $"Press {buttonName} to interact with";
                    case "signCompanion":
                        buttonName = GetButtonName(PlayerKeybinding.KeyBindingType.Interact);
                        return $"Press {buttonName} to talk to";
                    default:
                        buttonName = GetButtonName(PlayerKeybinding.KeyBindingType.Interact);
                        return $"Press {buttonName} to interact with";
                }
            }
            catch
            {
                return "Interact with";
            }
        }

        // Helper method to get user-friendly button name
        private static string GetButtonName(PlayerKeybinding.KeyBindingType keyType)
        {
            try
            {
                var playerKeybind = InputManager.Instance?.playerkeybind;
                if (playerKeybind == null)
                    return "the button";

                var action = playerKeybind.GetKeyBinding(keyType);
                if (action == null)
                    return "the button";

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
                                return GetFriendlyKeyName(key);
                            }
                        }
                        else
                        {
                            // Controller button
                            var deviceBinding = binding as InControl.DeviceBindingSource;
                            if (deviceBinding != null)
                            {
                                return GetFriendlyControllerButtonName(deviceBinding.Control);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting button name: {ex.Message}");
            }

            return "the button";
        }

        // Convert key enum to friendly name
        private static string GetFriendlyKeyName(InControl.Key key)
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

        // Convert controller button to friendly name
        private static string GetFriendlyControllerButtonName(InControl.InputControlType control)
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

        // Helper method to format object names for speech
        private static string FormatObjectName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Object";

            // Replace underscores with spaces
            name = name.Replace("_", " ");
            
            // Add spaces before capital letters in camelCase
            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
            
            // Handle special cases
            if (name.ToLower().Contains("key area") || name.ToLower().Contains("keyarea"))
            {
                return ""; // For area transitions, don't announce a name
            }
            
            return name.Trim();
        }

        // Patch NPCSpacediveFlowerController.SetVisibility to announce when spacedive opportunity appears
        [HarmonyPatch(typeof(NPCSpacediveFlowerController), nameof(NPCSpacediveFlowerController.SetVisibility))]
        [HarmonyPostfix]
        public static void NPCSpacediveFlowerController_SetVisibility_Postfix(NPCSpacediveFlowerController __instance, bool visible)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceInteractables)
                return;

            try
            {
                // Only announce when flower becomes visible (spacedive opportunity appears)
                if (!visible)
                    return;

                // Find the NPC that owns this flower
                NPCController npc = UnityEngine.Object.FindObjectsOfType<NPCController>().FirstOrDefault(n => n.spacediveFlower == __instance);
                
                if (npc != null)
                {
                    // Check if enough time has passed since last announcement
                    float currentTime = UnityEngine.Time.unscaledTime;
                    if (npc == _lastAnnouncedSpacediveNPC && currentTime - _lastSpacediveAnnounceTime < _interactableAnnounceDelay)
                    {
                        return;
                    }

                    _lastAnnouncedSpacediveNPC = npc;
                    _lastSpacediveAnnounceTime = currentTime;

                    // Get character name if available
                    string characterName = "";
                    var asftuCharacter = npc.GetComponent<AsftuCharacter>();
                    if (asftuCharacter != null && asftuCharacter.characterName != null)
                    {
                        characterName = asftuCharacter.characterName.GetName();
                    }
                    else
                    {
                        characterName = npc.name;
                    }

                    // Get the book button name
                    string bookButton = GetButtonName(PlayerKeybinding.KeyBindingType.BookMenu);
                    
                    string announcement = !string.IsNullOrWhiteSpace(characterName)
                        ? $"{characterName} can be dived into. Press {bookButton} to use spacedive"
                        : $"Character can be dived into. Press {bookButton} to use spacedive";

                    TTSManager.Speak(announcement, interrupt: false, priority: 7);
                    MelonLogger.Msg($"[Spacedive] Announced: {announcement}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in NPCSpacediveFlowerController_SetVisibility_Postfix: {ex.Message}");
            }
        }

        // Helper method to determine audio type for interactable
        private static InteractableType GetInteractableAudioType(InteractableObject obj)
        {
            if (obj == null)
                return InteractableType.Item;

            try
            {
                // Check if it's a door/stairs (MoveScene)
                if (obj is InteractableObject_MoveScene)
                {
                    return InteractableType.DoorStairs;
                }

                // Check if it's a character
                var character = obj.GetComponent<AsftuCharacter>();
                if (character != null)
                {
                    return InteractableType.Character;
                }

                // Check icon type for additional character detection
                string iconValue = obj.icon.ToString();
                if (iconValue == "signCompanion")
                {
                    return InteractableType.Character;
                }

                // Default to item
                return InteractableType.Item;
            }
            catch
            {
                return InteractableType.Item;
            }
        }

        // Helper method to get lamp state (lit or unlit)
        private static string GetLampState(InteractableObject obj)
        {
            if (obj == null)
                return "lamp";

            try
            {
                // Strategy 1: Check for SpriteRenderer to see if the lamp is lit
                var spriteRenderers = obj.GetComponentsInChildren<UnityEngine.SpriteRenderer>(true);
                foreach (var sr in spriteRenderers)
                {
                    // Check if the sprite renderer is active and has high alpha (visible)
                    if (sr.gameObject.activeSelf && sr.color.a > 0.5f)
                    {
                        // Check if it's a light/glow sprite (typically brighter colors)
                        var color = sr.color;
                        float brightness = (color.r + color.g + color.b) / 3f;

                        // If sprite name suggests it's a light indicator
                        if (sr.name.ToLower().Contains("light") ||
                            sr.name.ToLower().Contains("glow") ||
                            sr.name.ToLower().Contains("on"))
                        {
                            if (brightness > 0.6f)
                            {
                                MelonLogger.Msg($"[Lamp] Found lit lamp via sprite: {sr.name}, brightness: {brightness}");
                                return "lamp lit";
                            }
                            else
                            {
                                MelonLogger.Msg($"[Lamp] Found unlit lamp via sprite: {sr.name}, brightness: {brightness}");
                                return "lamp unlit";
                            }
                        }
                    }
                }

                // Strategy 2: Check for Light2D component (Unity 2D lighting)
                var lights = obj.GetComponentsInChildren<UnityEngine.Light>(true);
                foreach (var light in lights)
                {
                    if (light.enabled && light.intensity > 0.1f)
                    {
                        MelonLogger.Msg($"[Lamp] Found lit lamp via Light component, intensity: {light.intensity}");
                        return "lamp lit";
                    }
                }

                // Strategy 3: Check Fungus variables for lamp state
                var flowcharts = UnityEngine.Object.FindObjectsOfType<Fungus.Flowchart>();
                string lampIdentifier = obj.gameObject.name.Replace(" ", "").Replace("_", "");

                foreach (var flowchart in flowcharts)
                {
                    if (flowchart == null) continue;

                    var varNames = flowchart.GetVariableNames();
                    if (varNames != null)
                    {
                        foreach (var vName in varNames)
                        {
                            // Look for variables like "Lamp1", "Lamp2", etc.
                            if (vName.ToLower().Contains("lamp") && vName.Contains(lampIdentifier))
                            {
                                var boolVar = flowchart.GetVariable<Fungus.BooleanVariable>(vName);
                                if (boolVar != null)
                                {
                                    MelonLogger.Msg($"[Lamp] Found Fungus variable '{vName}' = {boolVar.Value}");
                                    return boolVar.Value ? "lamp lit" : "lamp unlit";
                                }
                            }
                        }
                    }
                }

                // Default: assume unlit if we can't determine state
                MelonLogger.Msg($"[Lamp] Could not determine lamp state, defaulting to unlit");
                return "lamp unlit";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting lamp state: {ex.Message}");
                return "lamp";
            }
        }

        // Helper method to get relief name
        private static string GetReliefName(InteractableObject obj, string rawName)
        {
            if (obj == null)
                return "relief";

            try
            {
                // Extract the number at the end (e.g., "4" from "ArcaLulu4")
                var numberMatch = System.Text.RegularExpressions.Regex.Match(rawName, @"(\d+)$");

                if (numberMatch.Success)
                {
                    // Convert 0-based to 1-based numbering (0->1, 1->2, etc.)
                    int zeroBasedNumber = int.Parse(numberMatch.Groups[1].Value);
                    int oneBasedNumber = zeroBasedNumber + 1;

                    return $"Relief {oneBasedNumber}";
                }

                return "relief";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting relief name: {ex.Message}");
                return "relief";
            }
        }
    }
}
