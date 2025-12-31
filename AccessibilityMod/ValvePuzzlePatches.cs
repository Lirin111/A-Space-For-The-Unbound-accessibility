using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using com.mojiken.asftu;
using UnityEngine;

namespace AsftuAccessibilityMod.Patches
{
    /// <summary>
    /// Patches for the valve/pipe wheel puzzle in Chapter 3
    /// </summary>
    [HarmonyPatch]
    public static class ValvePuzzlePatches
    {
        // Track valve positions and assign numbers
        private static Dictionary<GameObject, int> _valveNumbers = new Dictionary<GameObject, int>();
        private static Dictionary<int, bool> _valveStates = new Dictionary<int, bool>(); // true = has pipe wheel
        private static bool _puzzleActive = false;
        private static float _lastValveCheckTime = 0f;

        // Puzzle progress tracking
        private static int _puzzleStep = 0; // 0 = not started, 1 = need to fill well, 2 = need to extinguish fire
        private static bool _hasAnnouncedObjective = false;
        private static float _lastHintTime = 0f;
        private static float _hintCooldown = 30f; // 30 seconds between auto-hints

        // Track last announced valve to prevent repeating
        private static GameObject _lastAnnouncedValve = null;
        private static float _lastValveAnnounceTime = 0f;

        // Patch to detect when entering the valve puzzle area and announce valve numbers
        [HarmonyPatch(typeof(CharacterInteract), nameof(CharacterInteract.setInteract))]
        [HarmonyPostfix]
        public static void CharacterInteract_ValvePuzzle_Postfix(CharacterInteract __instance, InteractableObject theObject)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceInteractables)
                return;

            try
            {
                if (theObject == null) return;

                string objName = theObject.gameObject.name.ToLower();

                // Check if we're interacting with a numbered valve (not ValveA, ValveB, etc.)
                if (objName.Contains("valve") && System.Text.RegularExpressions.Regex.IsMatch(theObject.gameObject.name, @"^Valve\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    MelonLogger.Msg($"[Valve Puzzle] Approaching valve object: GameObject='{theObject.gameObject.name}', ObjName='{objName}'");
                    ActivatePuzzleIfNeeded();

                    // Assign number to this valve if not already assigned
                    if (_valveNumbers.Count == 0)
                    {
                        AssignValveNumbers();
                    }

                    // Get valve number
                    int valveNumber = _valveNumbers.ContainsKey(theObject.gameObject)
                        ? _valveNumbers[theObject.gameObject]
                        : -1;

                    if (valveNumber > 0)
                    {
                        float currentTime = UnityEngine.Time.unscaledTime;

                        // Only announce if this is a different valve or enough time has passed (3 seconds)
                        if (_lastAnnouncedValve != theObject.gameObject ||
                            currentTime - _lastValveAnnounceTime > 3.0f)
                        {
                            // Check if valve has a pipe wheel installed
                            bool hasWheel = CheckValveHasWheel(valveNumber, theObject);
                            string wheelStatus = hasWheel ? "has pipe wheel installed" : "empty, no pipe wheel";

                            // Announce immediately when approaching the valve
                            string announcement = $"Valve {valveNumber}. {wheelStatus}";
                            TTSManager.Speak(announcement, interrupt: true, priority: 9);
                            MelonLogger.Msg($"[Valve Puzzle] Announced on approach: {announcement}");

                            _lastAnnouncedValve = theObject.gameObject;
                            _lastValveAnnounceTime = currentTime;
                        }
                    }
                }
                // Check if interacting with a pipe wheel item
                else if (objName.Contains("pipewheel") || objName.Contains("pipe wheel") || objName.Contains("handle"))
                {
                    ActivatePuzzleIfNeeded();
                    MelonLogger.Msg("[Valve Puzzle] Detected pipe wheel item");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CharacterInteract_ValvePuzzle_Postfix: {ex.Message}");
            }
        }

        private static void ActivatePuzzleIfNeeded()
        {
            if (!_puzzleActive)
            {
                _puzzleActive = true;
                _valveNumbers.Clear();
                _valveStates.Clear();
                _puzzleStep = 1; // Start at step 1
                _hasAnnouncedObjective = false;
                _lastAnnouncedValve = null;
                _lastValveAnnounceTime = 0f;
                AssignValveNumbers();
                MelonLogger.Msg("[Valve Puzzle] Puzzle activated");

                // Announce puzzle introduction and first objective
                // Use interrupt: true to ensure it speaks immediately and doesn't get lost in queue
                string intro = "Valve puzzle detected. There are numbered valves around you. " +
                              "Collect loose pipe wheel items from the area, then use the interaction menu to install or remove wheels on specific valves. " +
                              "Goal: Install pipe wheels on Valve 2 and Valve 11 first to fill the well.";
                TTSManager.Speak(intro, interrupt: true, priority: 10);
                _hasAnnouncedObjective = true;
                _lastHintTime = UnityEngine.Time.unscaledTime;
            }
        }

        private static void AssignValveNumbers()
        {
            try
            {
                // Find all valve objects in the scene
                var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                List<GameObject> valves = new List<GameObject>();

                foreach (var obj in allObjects)
                {
                    if (obj.name.ToLower().Contains("valve") && obj.activeInHierarchy)
                    {
                        // Exclude ValveA, ValveB, etc. - these are pickup items, not puzzle valves
                        // Only include Valve0, Valve1, Valve2, etc. (numbered valves)
                        if (System.Text.RegularExpressions.Regex.IsMatch(obj.name, @"^Valve\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        {
                            // Check if it has an InteractableObject component
                            if (obj.GetComponent<InteractableObject>() != null ||
                                obj.GetComponentInParent<InteractableObject>() != null ||
                                obj.GetComponentInChildren<InteractableObject>() != null)
                            {
                                // Log the actual object name for debugging
                                MelonLogger.Msg($"[Valve Puzzle] Found valve object: '{obj.name}' at ({obj.transform.position.x:F2}, {obj.transform.position.y:F2})");
                                valves.Add(obj);
                            }
                        }
                        else
                        {
                            MelonLogger.Msg($"[Valve Puzzle] Skipping non-numbered valve: '{obj.name}' (item, not puzzle valve)");
                        }
                    }
                }

                if (valves.Count == 0)
                {
                    MelonLogger.Warning("[Valve Puzzle] No valves found in scene");
                    return;
                }

                // Extract the number from GameObject name and add +1 for player-facing numbers
                // GameObjects are named Valve0-Valve10 (internal 0-based numbering)
                // But players/walkthroughs expect Valve 1-11 (1-based numbering)
                // So GameObject "Valve0" -> announce as "Valve 1", "Valve10" -> announce as "Valve 11"
                foreach (var valve in valves)
                {
                    // Extract the number from the GameObject name (e.g., "Valve2" -> 2)
                    var match = System.Text.RegularExpressions.Regex.Match(valve.name, @"Valve(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        int internalNum = int.Parse(match.Groups[1].Value); // 0-10
                        int playerNum = internalNum + 1; // 1-11 for announcements
                        _valveNumbers[valve] = playerNum;
                        _valveStates[playerNum] = false; // Initially empty
                        MelonLogger.Msg($"[Valve Puzzle] Valve '{valve.name}' (internal {internalNum}) will be announced as Valve {playerNum} at position ({valve.transform.position.x:F2}, {valve.transform.position.y:F2})");
                    }
                }

                MelonLogger.Msg($"[Valve Puzzle] Assigned numbers to {valves.Count} valves");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in AssignValveNumbers: {ex.Message}");
            }
        }

        private static bool CheckValveHasWheel(int playerValveNumber, InteractableObject valve)
        {
            try
            {
                // playerValveNumber is 1-11 (what we announce to players)
                // Need to convert to internal number 0-10 to match Fungus variable names
                // Fungus variables are likely named "valve1" through "valve10" for internal Valve0-Valve9
                // OR they might be "valve0" through "valve10" matching GameObject names exactly

                // Strategy 1: Check Fungus variables
                var flowcharts = UnityEngine.Object.FindObjectsOfType<Fungus.Flowchart>();
                foreach (var flowchart in flowcharts)
                {
                    if (flowchart == null) continue;

                    var varNames = flowchart.GetVariableNames();
                    if (varNames != null)
                    {
                        // Try multiple naming patterns to find the correct Fungus variable
                        // Pattern 1: valve{playerNum} - e.g., "valve1" through "valve11"
                        // Pattern 2: valve{internalNum} - e.g., "valve0" through "valve10"
                        int internalNum = playerValveNumber - 1; // Convert back to 0-based

                        string[] possibleNames = new string[]
                        {
                            $"valve{playerValveNumber}",      // e.g., "valve1" for player "Valve 1"
                            $"valve{internalNum}",            // e.g., "valve0" for GameObject "Valve0"
                            $"valve_{playerValveNumber}",     // With underscore
                            $"valve_{internalNum}",
                            $"valve {playerValveNumber}",     // With space
                            $"valve {internalNum}"
                        };

                        foreach (var vName in varNames)
                        {
                            string vNameLower = vName.ToLower();
                            foreach (var possibleName in possibleNames)
                            {
                                if (vNameLower == possibleName.ToLower())
                                {
                                    var boolVar = flowchart.GetVariable<Fungus.BooleanVariable>(vName);
                                    if (boolVar != null)
                                    {
                                        bool fungusValue = boolVar.Value;
                                        MelonLogger.Msg($"[Valve Puzzle DEBUG] Player Valve {playerValveNumber} (GameObject Valve{internalNum}): Fungus '{vName}' = {fungusValue}");

                                        // Check child objects for visual confirmation
                                        bool hasVisibleWheel = false;
                                        if (valve != null && valve.gameObject != null)
                                        {
                                            var children = valve.GetComponentsInChildren<Transform>(true);
                                            foreach (var child in children)
                                            {
                                                if ((child.name.ToLower().Contains("wheel") ||
                                                     child.name.ToLower().Contains("handle") ||
                                                     child.name.ToLower().Contains("pipe")) &&
                                                    child.gameObject.activeInHierarchy)
                                                {
                                                    hasVisibleWheel = true;
                                                    MelonLogger.Msg($"[Valve Puzzle DEBUG] Found active child: {child.name}");
                                                    break;
                                                }
                                            }
                                        }

                                        MelonLogger.Msg($"[Valve Puzzle DEBUG] Visual check: hasVisibleWheel = {hasVisibleWheel}");

                                        // Use Fungus value as truth for now
                                        bool hasWheel = fungusValue;
                                        _valveStates[playerValveNumber] = hasWheel;
                                        return hasWheel;
                                    }
                                }
                            }
                        }
                    }
                }

                // Strategy 2: Check for child objects (pipe wheel model attached to valve)
                if (valve != null && valve.gameObject != null)
                {
                    var children = valve.GetComponentsInChildren<Transform>(true);
                    foreach (var child in children)
                    {
                        if (child.name.ToLower().Contains("wheel") ||
                            child.name.ToLower().Contains("handle") ||
                            child.name.ToLower().Contains("pipe"))
                        {
                            bool isActive = child.gameObject.activeInHierarchy;
                            _valveStates[playerValveNumber] = isActive;
                            MelonLogger.Msg($"[Valve Puzzle] Player Valve {playerValveNumber} child object state: {isActive}");
                            return isActive;
                        }
                    }
                }

                // Return cached state if available
                if (_valveStates.ContainsKey(playerValveNumber))
                {
                    return _valveStates[playerValveNumber];
                }

                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CheckValveHasWheel: {ex.Message}");
                return false;
            }
        }

        // NOTE: Removed InteractionMenu.Open patch - valve numbers are now announced
        // when approaching the valve (in setInteract patch) instead of when opening
        // the interaction menu, to avoid interrupting the interaction menu announcement

        // Monitor valve state changes and provide hints
        [HarmonyPatch(typeof(CharacterInteract), "Update")]
        [HarmonyPostfix]
        public static void MonitorValveStates_Postfix()
        {
            if (!AccessibilityMod.IsEnabled || !_puzzleActive)
                return;

            try
            {
                float currentTime = UnityEngine.Time.unscaledTime;

                // Check valve states every 1 second when puzzle is active
                if (currentTime - _lastValveCheckTime > 1.0f)
                {
                    _lastValveCheckTime = currentTime;
                    CheckForValveChanges();
                    CheckPuzzleProgress();
                }

                // Provide hints every 30 seconds if player seems stuck
                if (currentTime - _lastHintTime > _hintCooldown)
                {
                    _lastHintTime = currentTime;
                    ProvideHint();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in MonitorValveStates_Postfix: {ex.Message}");
            }
        }

        private static void CheckPuzzleProgress()
        {
            try
            {
                // Check if Step 1 is complete (Valve 2 and 11 have wheels)
                if (_puzzleStep == 1)
                {
                    bool valve2HasWheel = _valveStates.ContainsKey(2) && _valveStates[2];
                    bool valve11HasWheel = _valveStates.ContainsKey(11) && _valveStates[11];

                    if (valve2HasWheel && valve11HasWheel)
                    {
                        _puzzleStep = 2;
                        _hasAnnouncedObjective = false;
                        _lastHintTime = UnityEngine.Time.unscaledTime;

                        string announcement = "Step 1 complete! The well should be filling. Get the wet key from the well and use it on the locked box to get a third pipe wheel. " +
                                            "Step 2: Remove pipe wheels from Valve 2 and Valve 11, then install them on Valve 3, Valve 8, and Valve 10 to put out the fire.";
                        TTSManager.Speak(announcement, interrupt: false, priority: 9);
                        _hasAnnouncedObjective = true;
                        MelonLogger.Msg("[Valve Puzzle] Advanced to Step 2");
                    }
                }
                // Check if Step 2 is complete (Valve 3, 8, and 10 have wheels)
                else if (_puzzleStep == 2)
                {
                    bool valve3HasWheel = _valveStates.ContainsKey(3) && _valveStates[3];
                    bool valve8HasWheel = _valveStates.ContainsKey(8) && _valveStates[8];
                    bool valve10HasWheel = _valveStates.ContainsKey(10) && _valveStates[10];

                    if (valve3HasWheel && valve8HasWheel && valve10HasWheel)
                    {
                        _puzzleStep = 3; // Completed
                        string announcement = "Puzzle complete! The fire should be extinguished. Well done!";
                        TTSManager.Speak(announcement, interrupt: false, priority: 9);
                        MelonLogger.Msg("[Valve Puzzle] Puzzle completed!");

                        // Deactivate puzzle tracking after a delay
                        _puzzleActive = false;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CheckPuzzleProgress: {ex.Message}");
            }
        }

        private static void ProvideHint()
        {
            try
            {
                if (_puzzleStep == 1)
                {
                    // Check current state
                    bool valve2HasWheel = _valveStates.ContainsKey(2) && _valveStates[2];
                    bool valve11HasWheel = _valveStates.ContainsKey(11) && _valveStates[11];

                    if (!valve2HasWheel && !valve11HasWheel)
                    {
                        TTSManager.Speak("Hint: Find and collect loose pipe wheel items, then install them on Valve 2 and Valve 11 using the interaction menu.", interrupt: false, priority: 7);
                    }
                    else if (valve2HasWheel && !valve11HasWheel)
                    {
                        TTSManager.Speak("Hint: Valve 2 is done. Now install a pipe wheel on Valve 11.", interrupt: false, priority: 7);
                    }
                    else if (!valve2HasWheel && valve11HasWheel)
                    {
                        TTSManager.Speak("Hint: Valve 11 is done. Now install a pipe wheel on Valve 2.", interrupt: false, priority: 7);
                    }
                }
                else if (_puzzleStep == 2)
                {
                    // Count how many of the target valves have wheels
                    int correctValves = 0;
                    if (_valveStates.ContainsKey(3) && _valveStates[3]) correctValves++;
                    if (_valveStates.ContainsKey(8) && _valveStates[8]) correctValves++;
                    if (_valveStates.ContainsKey(10) && _valveStates[10]) correctValves++;

                    // Check if any wrong valves still have wheels
                    bool wrongValves = false;
                    if (_valveStates.ContainsKey(2) && _valveStates[2]) wrongValves = true;
                    if (_valveStates.ContainsKey(11) && _valveStates[11]) wrongValves = true;

                    if (wrongValves)
                    {
                        TTSManager.Speak("Hint: Remove the pipe wheels from Valve 2 and Valve 11 first, then install them on Valve 3, Valve 8, and Valve 10.", interrupt: false, priority: 7);
                    }
                    else if (correctValves == 0)
                    {
                        TTSManager.Speak("Hint: Install pipe wheels on Valve 3, Valve 8, and Valve 10. You should have 3 pipe wheels total.", interrupt: false, priority: 7);
                    }
                    else if (correctValves < 3)
                    {
                        TTSManager.Speak($"Hint: You have {correctValves} out of 3 valves correct. Install pipe wheels on Valve 3, Valve 8, and Valve 10.", interrupt: false, priority: 7);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ProvideHint: {ex.Message}");
            }
        }

        private static void CheckForValveChanges()
        {
            try
            {
                Dictionary<int, bool> newStates = new Dictionary<int, bool>();

                foreach (var kvp in _valveNumbers)
                {
                    int valveNum = kvp.Value;
                    var interactable = kvp.Key.GetComponent<InteractableObject>();
                    if (interactable == null)
                        interactable = kvp.Key.GetComponentInParent<InteractableObject>();

                    bool currentState = CheckValveHasWheel(valveNum, interactable);
                    newStates[valveNum] = currentState;

                    // Check if state changed
                    if (_valveStates.ContainsKey(valveNum) && _valveStates[valveNum] != currentState)
                    {
                        string announcement = currentState
                            ? $"Pipe wheel installed on Valve {valveNum}"
                            : $"Pipe wheel removed from Valve {valveNum}";

                        TTSManager.Speak(announcement, interrupt: false, priority: 8);
                        MelonLogger.Msg($"[Valve Puzzle] State change: {announcement}");
                    }
                }

                // Update cached states
                foreach (var kvp in newStates)
                {
                    _valveStates[kvp.Key] = kvp.Value;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CheckForValveChanges: {ex.Message}");
            }
        }
    }
}
