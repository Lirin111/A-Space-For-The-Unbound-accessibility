using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using com.mojiken.asftu;
using UnityEngine;
using Fungus;

namespace AsftuAccessibilityMod.Patches
{
    /// <summary>
    /// Patches for the Sensei Spacedive pose/symbol puzzle in Chapter 4
    /// Three switches that cycle through different poses - must be set to correct sequence
    /// </summary>
    [HarmonyPatch]
    public static class PosePuzzlePatches
    {
        // Track puzzle state
        private static bool _puzzleActive = false;
        private static Dictionary<string, int> _lastKnownPoseIndices = new Dictionary<string, int>();
        private static float _lastCheckTime = 0f;
        private static GameObject _lastAnnouncedSwitch = null;
        private static float _lastSwitchAnnounceTime = 0f;

        // Pose names for announcements (based on karate poses)
        // Index 0-4 representing different poses - adjust based on actual game
        private static readonly string[] poseNames = new string[]
        {
            "Stance 1", "Stance 2", "Stance 3", "Stance 4", "Stance 5"
        };

        // Specific variable names for the Chapter 4 puzzle
        private const string VAR_BOARD0 = "ch4_4_SpacediveCivilWorker_board0";
        private const string VAR_BOARD1 = "ch4_4_SpacediveCivilWorker_board1";
        private const string VAR_BOARD2 = "ch4_4_SpacediveCivilWorker_board2";

        /// <summary>
        /// Detect when interacting with switches in the Spacedive area
        /// </summary>
        [HarmonyPatch(typeof(CharacterInteract), nameof(CharacterInteract.setInteract))]
        [HarmonyPostfix]
        public static void DetectSwitchInteraction_Postfix(CharacterInteract __instance, InteractableObject theObject)
        {
            if (!AccessibilityMod.IsEnabled || theObject == null)
                return;

            try
            {
                string objName = theObject.gameObject.name.ToLower();
                string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.ToLower();

                // Activate puzzle when in Spacedive and interacting with switch-like objects
                if (sceneName.Contains("spacedive") &&
                    (objName.Contains("switch") || objName.Contains("lever") ||
                     objName.Contains("dial") || objName.Contains("pose") ||
                     objName.Contains("symbol")))
                {
                    MelonLogger.Msg($"[Pose Puzzle] Detected switch interaction: {theObject.gameObject.name} in scene {sceneName}");
                    ActivatePuzzle();

                    // Announce which switch this is
                    string switchPosition = DetermineSwitchPosition(theObject);

                    float currentTime = Time.unscaledTime;
                    if (_lastAnnouncedSwitch != theObject.gameObject ||
                        currentTime - _lastSwitchAnnounceTime > 2.0f)
                    {
                        _lastAnnouncedSwitch = theObject.gameObject;
                        _lastSwitchAnnounceTime = currentTime;

                        // Get current pose for this switch
                        string currentPose = GetCurrentPoseForSwitch(theObject, switchPosition);

                        // Get target pose for this switch
                        string targetInfo = "";
                        if (switchPosition.Contains("Left"))
                            targetInfo = " Target: pose 4.";
                        else if (switchPosition.Contains("Middle"))
                            targetInfo = " Target: pose 2.";
                        else if (switchPosition.Contains("Right"))
                            targetInfo = " Target: pose 0. Do not press.";

                        string announcement = $"{switchPosition} switch. {currentPose}{targetInfo}";
                        TTSManager.Speak(announcement, interrupt: true, priority: 9);
                        MelonLogger.Msg($"[Pose Puzzle] Announced: {announcement}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in DetectSwitchInteraction_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Monitor for changes in pose states
        /// </summary>
        [HarmonyPatch(typeof(CharacterInteract), "Update")]
        [HarmonyPostfix]
        public static void MonitorPoseChanges_Postfix()
        {
            if (!AccessibilityMod.IsEnabled || !_puzzleActive)
                return;

            try
            {
                float currentTime = Time.unscaledTime;

                // Check pose states every 0.5 seconds
                if (currentTime - _lastCheckTime > 0.5f)
                {
                    _lastCheckTime = currentTime;
                    CheckPoseChanges();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in MonitorPoseChanges_Postfix: {ex.Message}");
            }
        }

        private static void ActivatePuzzle()
        {
            if (!_puzzleActive)
            {
                _puzzleActive = true;
                _lastKnownPoseIndices.Clear();
                MelonLogger.Msg("[Pose Puzzle] Puzzle activated");

                // Give introduction
                string intro = "Pose puzzle detected. Three switches control a pose display board. " +
                              "Each switch cycles through 5 different poses numbered 0 through 4. " +
                              "When you interact with a switch, it advances to the next pose. " +
                              "The puzzle starts with all switches at pose 0. " +
                              "Solution: Set left switch to pose 4 by pressing it 4 times. " +
                              "Set middle switch to pose 2 by pressing it 2 times. " +
                              "Leave right switch at pose 0, do not press it.";
                TTSManager.Speak(intro, interrupt: false, priority: 8);
            }
        }

        private static string DetermineSwitchPosition(InteractableObject switchObj)
        {
            string objName = switchObj.gameObject.name.ToLower();
            float xPos = switchObj.transform.position.x;

            MelonLogger.Msg($"[Pose Puzzle] Determining position for '{switchObj.gameObject.name}' at X={xPos:F2}");

            // Try to determine position from object name first
            if (objName.Contains("left"))
                return "Left";
            if (objName.Contains("middle") || objName.Contains("center"))
                return "Middle";
            if (objName.Contains("right"))
                return "Right";

            // Find all similar switches to compare positions
            var allObjects = UnityEngine.Object.FindObjectsOfType<InteractableObject>();
            List<GameObject> switches = new List<GameObject>();

            foreach (var obj in allObjects)
            {
                string name = obj.gameObject.name.ToLower();
                if (name.Contains("switch") || name.Contains("lever") ||
                    name.Contains("dial") || name.Contains("pose") || name.Contains("symbol"))
                {
                    switches.Add(obj.gameObject);
                    MelonLogger.Msg($"[Pose Puzzle] Found switch: '{obj.gameObject.name}' at X={obj.transform.position.x:F2}");
                }
            }

            // Sort by X position to determine left/middle/right
            switches.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

            if (switches.Count >= 3)
            {
                int index = switches.FindIndex(s => s == switchObj.gameObject);
                MelonLogger.Msg($"[Pose Puzzle] Switch index in sorted list: {index} out of {switches.Count}");

                if (index == 0) return "Left";
                if (index == 1) return "Middle";
                if (index == 2) return "Right";
            }

            // Check for numeric suffix (0, 1, 2)
            if (objName.EndsWith("0")) return "Left";
            if (objName.EndsWith("1")) return "Middle";
            if (objName.EndsWith("2")) return "Right";

            // Fallback - log for debugging
            MelonLogger.Warning($"[Pose Puzzle] Could not determine position for '{switchObj.gameObject.name}'");
            return $"Switch ({switchObj.gameObject.name})";
        }

        private static string GetCurrentPoseForSwitch(InteractableObject switchObj, string switchPosition)
        {
            try
            {
                // Determine which variable to check based on switch position
                string variableName = null;
                if (switchPosition.Contains("Left"))
                    variableName = VAR_BOARD0;
                else if (switchPosition.Contains("Middle"))
                    variableName = VAR_BOARD1;
                else if (switchPosition.Contains("Right"))
                    variableName = VAR_BOARD2;

                if (variableName == null)
                {
                    MelonLogger.Warning($"[Pose Puzzle] Unknown switch position: {switchPosition}");
                    return "Current pose: Unknown";
                }

                // Get the GlobalFlowchart
                var flowchart = GlobalFlowchart.GetFlowchart();
                if (flowchart == null)
                {
                    MelonLogger.Error("[Pose Puzzle] Could not find GlobalFlowchart");
                    return "Current pose: Unknown";
                }

                // Get the variable value
                var intVar = flowchart.GetVariable<IntegerVariable>(variableName);
                if (intVar != null)
                {
                    int poseIndex = intVar.Value;
                    MelonLogger.Msg($"[Pose Puzzle] {switchPosition} switch - Variable '{variableName}' = {poseIndex}");

                    // Track this for change detection
                    _lastKnownPoseIndices[variableName] = poseIndex;

                    // Return pose name
                    return $"Current pose: {poseIndex}";
                }
                else
                {
                    MelonLogger.Warning($"[Pose Puzzle] Variable '{variableName}' not found in GlobalFlowchart");
                    return "Current pose: Unknown";
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in GetCurrentPoseForSwitch: {ex.Message}");
                return "Error reading pose";
            }
        }

        private static void CheckPoseChanges()
        {
            try
            {
                var flowchart = GlobalFlowchart.GetFlowchart();
                if (flowchart == null) return;

                // Check each of the three board variables
                CheckSingleVariable(flowchart, VAR_BOARD0, "Left");
                CheckSingleVariable(flowchart, VAR_BOARD1, "Middle");
                CheckSingleVariable(flowchart, VAR_BOARD2, "Right");

                // Check if puzzle is solved
                CheckPuzzleSolution(flowchart);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CheckPoseChanges: {ex.Message}");
            }
        }

        private static void CheckSingleVariable(Flowchart flowchart, string varName, string switchName)
        {
            var intVar = flowchart.GetVariable<IntegerVariable>(varName);
            if (intVar == null) return;

            int currentValue = intVar.Value;

            // Check if value changed
            if (_lastKnownPoseIndices.ContainsKey(varName))
            {
                int previousValue = _lastKnownPoseIndices[varName];
                if (currentValue != previousValue)
                {
                    _lastKnownPoseIndices[varName] = currentValue;

                    // Announce the change
                    string announcement = $"{switchName} switch changed to pose {currentValue}";
                    TTSManager.Speak(announcement, interrupt: false, priority: 8);
                    MelonLogger.Msg($"[Pose Puzzle] {announcement}");
                }
            }
            else
            {
                // First time seeing this variable
                _lastKnownPoseIndices[varName] = currentValue;
            }
        }

        private static void CheckPuzzleSolution(Flowchart flowchart)
        {
            try
            {
                // Solution: board0=4, board1=2, board2=0
                var board0 = flowchart.GetVariable<IntegerVariable>(VAR_BOARD0);
                var board1 = flowchart.GetVariable<IntegerVariable>(VAR_BOARD1);
                var board2 = flowchart.GetVariable<IntegerVariable>(VAR_BOARD2);

                if (board0 != null && board1 != null && board2 != null)
                {
                    if (board0.Value == 4 && board1.Value == 2 && board2.Value == 0)
                    {
                        TTSManager.Speak("Puzzle solved! All switches are at the correct poses.", interrupt: false, priority: 9);
                        MelonLogger.Msg("[Pose Puzzle] Puzzle completed!");
                        _puzzleActive = false; // Deactivate puzzle tracking
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in CheckPuzzleSolution: {ex.Message}");
            }
        }
    }
}
