using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using MelonLoader;

namespace AsftuAccessibilityMod.Patches
{
    /// <summary>
    /// Patches for stealth minigame accessibility
    /// Provides state announcements and directional audio cues
    /// </summary>
    [HarmonyPatch]
    public static class StealthPatches
    {
        // Track player state changes
        private static MinigameStealthAtmaController.State _lastPlayerState = MinigameStealthAtmaController.State.Walk;
        private static bool _lastInPatrollerArea = false;
        private static bool _lastExposedState = false;
        private static MinigameStealthPatroller _lastPatroller = null;
        
        // Track patroller states
        private static Dictionary<MinigameStealthPatroller, object> _patrollerStates = new Dictionary<MinigameStealthPatroller, object>();
        
        // Update timing
        private static float _lastProximityUpdateTime = 0f;
        private const float PROXIMITY_UPDATE_INTERVAL = 2f; // Announce proximity every 2 seconds
        
        // Guard looking direction monitoring
        private static float _lastGuardLookingCheckTime = 0f;
        private const float GUARD_LOOKING_CHECK_INTERVAL = 1.5f; // Check guard direction every 1.5 seconds
        private static bool _lastGuardWasLookingAtPlayer = false;

        // Safe zone tracking
        private static List<Collider2D> _safeZones = new List<Collider2D>();
        private static List<Vector3> _safeZonePositions = new List<Vector3>(); // Calculated safe positions
        private static bool _safeZonesInitialized = false;
        private const float SAFE_ZONE_DETECTION_RANGE = 7f; // 7 meters as requested
        private static int _safeZoneAudioFrameCounter = 0; // For periodic logging

        #region Player State Changes

        /// <summary>
        /// Patch: Announce when player changes to crouch state
        /// </summary>
        [HarmonyPatch(typeof(MinigameStealthAtmaController), nameof(MinigameStealthAtmaController.ChangeStateToCrouch))]
        [HarmonyPostfix]
        public static void ChangeStateToCrouch_Postfix(MinigameStealthAtmaController __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                if (_lastPlayerState != MinigameStealthAtmaController.State.Crouch)
                {
                    _lastPlayerState = MinigameStealthAtmaController.State.Crouch;
                    
                    // TTS announcement
                    // // TTSManager.AnnounceUI("Crouching, hidden", interrupt: true);
                    
                    // Play audio cue if available
                    if (StealthAudioManager.Instance.HasClip(StealthAudioManager.NOW_CROUCHING))
                    {
                        StealthAudioManager.Instance.PlayCentered(StealthAudioManager.NOW_CROUCHING);
                    }
                    
                    MelonLogger.Msg("[Stealth] Player crouched");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[StealthPatches] Error in ChangeStateToCrouch: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch: Announce when player changes to walk state
        /// </summary>
        [HarmonyPatch(typeof(MinigameStealthAtmaController), nameof(MinigameStealthAtmaController.ChangeStateToWalk))]
        [HarmonyPostfix]
        public static void ChangeStateToWalk_Postfix(MinigameStealthAtmaController __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                if (_lastPlayerState != MinigameStealthAtmaController.State.Walk)
                {
                    _lastPlayerState = MinigameStealthAtmaController.State.Walk;
                    
                    // Warn if walking in patroller area
                    string message = __instance.inPatrollerArea != null 
                        ? "Walking, exposed to guard" 
                        : "Walking";
                    
                    // // TTSManager.AnnounceUI(message, interrupt: true);
                    
                    // Play audio cue if available
                    if (StealthAudioManager.Instance.HasClip(StealthAudioManager.NOW_WALKING))
                    {
                        StealthAudioManager.Instance.PlayCentered(StealthAudioManager.NOW_WALKING);
                    }
                    
                    MelonLogger.Msg($"[Stealth] Player walking (in patrol area: {__instance.inPatrollerArea != null})");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[StealthPatches] Error in ChangeStateToWalk: {ex.Message}");
            }
        }

        #endregion

        #region Patroller Area Detection

        /// <summary>
        /// Patch: Announce when player enters a patroller's detection area
        /// </summary>
        [HarmonyPatch(typeof(MinigameStealthPatroller), nameof(MinigameStealthPatroller.OnTriggerStay2D))]
        [HarmonyPostfix]
        public static void OnTriggerStay2D_Postfix(MinigameStealthPatroller __instance, Collider2D collider)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                var atmaController = collider.GetComponent<MinigameStealthAtmaController>();
                if (atmaController == null)
                    return;

                // Only announce when first entering
                if (!_lastInPatrollerArea || _lastPatroller != __instance)
                {
                    _lastInPatrollerArea = true;
                    _lastPatroller = __instance;

                    // Calculate direction to patroller
                    float direction = GetDirectionToPatroller(atmaController, __instance);
                    string directionText = GetDirectionText(direction);

                    // Announce with direction
                    string stateText = __instance.isAware ? "alert, looking at you" : "searching";
                    string message = $"Entering guard area from {directionText}. Guard is {stateText}. Crouch to hide";
                    
                    // // TTSManager.AnnounceUI(message, interrupt: true);

                    // Play spatial audio cue
                    if (StealthAudioManager.Instance.HasClip(StealthAudioManager.ENTERING_GUARD_AREA))
                    {
                        StealthAudioManager.Instance.PlaySpatial(StealthAudioManager.ENTERING_GUARD_AREA, direction);
                    }

                    MelonLogger.Msg($"[Stealth] Entered patroller area (direction: {directionText}, aware: {__instance.isAware})");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[StealthPatches] Error in OnTriggerStay2D: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch: Announce when player exits a patroller's detection area
        /// </summary>
        [HarmonyPatch(typeof(MinigameStealthPatroller), nameof(MinigameStealthPatroller.OnTriggerExit2D))]
        [HarmonyPostfix]
        public static void OnTriggerExit2D_Postfix(MinigameStealthPatroller __instance, Collider2D collider)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                var atmaController = collider.GetComponent<MinigameStealthAtmaController>();
                if (atmaController == null)
                    return;

                if (_lastInPatrollerArea && _lastPatroller == __instance)
                {
                    _lastInPatrollerArea = false;
                    _lastPatroller = null;
                    
                    // // TTSManager.AnnounceUI("Left guard area, safe to walk", interrupt: true);                    // Play spatial audio cue
                    if (StealthAudioManager.Instance.HasClip(StealthAudioManager.EXITING_GUARD_AREA))
                    {
                        StealthAudioManager.Instance.PlayCentered(StealthAudioManager.EXITING_GUARD_AREA);
                    }

                    MelonLogger.Msg("[Stealth] Exited patroller area");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[StealthPatches] Error in OnTriggerExit2D: {ex.Message}");
            }
        }

        #endregion

        #region Patroller State Changes

        /// <summary>
        /// Patch: Announce when patroller becomes aware (looking at player)
        /// </summary>
        [HarmonyPatch(typeof(MinigameStealthPatroller), nameof(MinigameStealthPatroller.SetAware))]
        [HarmonyPostfix]
        public static void SetAware_Postfix(MinigameStealthPatroller __instance, bool isAware)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                MelonLogger.Msg($"[Stealth] SetAware called: {isAware} (patroller: {__instance.GetHashCode()})");
                
                // Check if state actually changed
                bool stateChanged = false;
                if (!_patrollerStates.ContainsKey(__instance))
                {
                    _patrollerStates[__instance] = isAware;
                    stateChanged = true; // First time seeing this patroller
                }
                else
                {
                    bool wasAware = (bool)_patrollerStates[__instance];
                    if (wasAware != isAware)
                    {
                        _patrollerStates[__instance] = isAware;
                        stateChanged = true;
                    }
                }

                if (stateChanged)
                {

                    // Only announce if this patroller affects the player
                    var atma = UnityEngine.Object.FindObjectOfType<MinigameStealthAtmaController>();
                    if (atma != null && atma.inPatrollerArea == __instance)
                    {
                        float direction = GetDirectionToPatroller(atma, __instance);
                        
                        if (isAware)
                        {
                            // // TTSManager.AnnounceUI("Guard alert! Looking your way, crouch now!", interrupt: true);
                            
                            if (StealthAudioManager.Instance.HasClip(StealthAudioManager.GUARD_ALERT))
                            {
                                StealthAudioManager.Instance.PlaySpatial(StealthAudioManager.GUARD_ALERT, direction, volume: 1.2f);
                            }
                        }
                        else
                        {
                            // // TTSManager.AnnounceUI("Guard searching, not looking", interrupt: false);
                            
                            if (StealthAudioManager.Instance.HasClip(StealthAudioManager.GUARD_SEARCHING))
                            {
                                StealthAudioManager.Instance.PlaySpatial(StealthAudioManager.GUARD_SEARCHING, direction);
                            }
                        }

                        MelonLogger.Msg($"[Stealth] Patroller aware state changed: {isAware}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[StealthPatches] Error in SetAware: {ex.Message}");
            }
        }

        #endregion

        #region Exposure Detection

        /// <summary>
        /// Patch: Monitor exposure state and announce danger
        /// </summary>
        [HarmonyPatch(typeof(MinigameStealthAtmaController), nameof(MinigameStealthAtmaController.UpdateExposedState))]
        [HarmonyPostfix]
        public static void UpdateExposedState_Postfix(MinigameStealthAtmaController __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                // Announce when exposure state changes
                if (_lastExposedState != __instance.IsExposed)
                {
                    _lastExposedState = __instance.IsExposed;

                    if (__instance.IsExposed && __instance.inPatrollerArea != null && __instance.inPatrollerArea.isAware)
                    {
                        // // TTSManager.AnnounceUI("EXPOSED! Guard sees you! Crouch immediately!", interrupt: true);
                        
                        if (StealthAudioManager.Instance.HasClip(StealthAudioManager.DANGER_EXPOSED))
                        {
                            StealthAudioManager.Instance.PlayCentered(StealthAudioManager.DANGER_EXPOSED, volume: 1.5f);
                        }

                        MelonLogger.Msg("[Stealth] PLAYER EXPOSED!");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[StealthPatches] Error in UpdateExposedState: {ex.Message}");
            }
        }

        #endregion

        #region Continuous Updates

        private static bool _updateLogged = false;

        /// <summary>
        /// Patch: Provide periodic proximity updates during stealth minigame
        /// Note: We patch the UpdateDefault method which is called by Update in base Minigame class
        /// </summary>
        [HarmonyPatch(typeof(MinigameStealth), "UpdateDefault", new Type[] { typeof(float) })]
        [HarmonyPostfix]
        public static void UpdateDefault_Postfix(MinigameStealth __instance, float deltaTime)
        {
            if (!_updateLogged)
            {
                MelonLogger.Msg("========================================");
                MelonLogger.Msg("[Stealth] MinigameStealth.Update PATCHED AND RUNNING!");
                MelonLogger.Msg("========================================");
                _updateLogged = true;
            }

            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                var atma = __instance.atma;
                if (atma == null || __instance.patrollerList == null || __instance.patrollerList.Length == 0)
                    return;

                // Check if minigame is still in Playing state using reflection
                var stateField = typeof(MinigameStealth).GetField("state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (stateField != null)
                {
                    var state = stateField.GetValue(__instance);
                    // If state is not Playing (0), stop all audio and return
                    if ((int)state != 0)
                    {
                        if (StealthAudioManager.Instance != null)
                        {
                            StealthAudioManager.Instance.StopLooping(StealthAudioManager.SAFE_ZONE_LOOP);
                        }
                        return;
                    }
                }

                // Check guard looking direction periodically
                if (Time.unscaledTime - _lastGuardLookingCheckTime >= GUARD_LOOKING_CHECK_INTERVAL)
                {
                    _lastGuardLookingCheckTime = Time.unscaledTime;
                    CheckGuardLookingDirection(__instance, atma);
                }

                // Initialize safe zones if not done yet
                if (!_safeZonesInitialized)
                {
                    InitializeSafeZones(__instance);
                }

                // Update safe zone spatial audio EVERY frame for smooth panning
                UpdateSafeZoneSpatialAudio(atma);

                // Rate limit proximity beep announcements
                if (Time.unscaledTime - _lastProximityUpdateTime < PROXIMITY_UPDATE_INTERVAL)
                    return;

                _lastProximityUpdateTime = Time.unscaledTime;

                // Find nearest patroller for proximity beeps
                MinigameStealthPatroller nearestPatroller = null;
                float nearestDistance = float.MaxValue;

                foreach (var patroller in __instance.patrollerList)
                {
                    if (patroller == null)
                        continue;

                    float distance = Vector3.Distance(atma.transform.position, patroller.transform.position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestPatroller = patroller;
                    }
                }

                if (nearestPatroller != null)
                {
                    float direction = GetDirectionToPatroller(atma, nearestPatroller);
                    
                    // Play proximity beep (directional)
                    StealthAudioManager.Instance.PlayProximityBeep(nearestDistance, direction);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[StealthPatches] Error in Update: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if nearby guards are looking at the player and announce
        /// </summary>
        private static void CheckGuardLookingDirection(MinigameStealth minigame, MinigameStealthAtmaController atma)
        {
            // Check guards within very extended range - much further than danger zone
            MinigameStealthPatroller nearbyPatroller = null;
            float nearbyDistance = float.MaxValue;
            const float CHECK_RANGE = 20f; // Much further range to hear well before entering danger zone

            foreach (var patroller in minigame.patrollerList)
            {
                if (patroller == null)
                    continue;

                float distance = Vector3.Distance(atma.transform.position, patroller.transform.position);
                if (distance < CHECK_RANGE && distance < nearbyDistance)
                {
                    nearbyDistance = distance;
                    nearbyPatroller = patroller;
                }
            }

            if (nearbyPatroller != null)
            {
                bool guardLookingAtPlayer = nearbyPatroller.isAware;
                
                MelonLogger.Msg($"[Stealth] Nearby guard found - distance: {nearbyDistance:F1}, isAware: {guardLookingAtPlayer}, last state: {_lastGuardWasLookingAtPlayer}");
                
                // Only announce when state changes
                if (guardLookingAtPlayer != _lastGuardWasLookingAtPlayer)
                {
                    _lastGuardWasLookingAtPlayer = guardLookingAtPlayer;
                    
                    float direction = GetDirectionToPatroller(atma, nearbyPatroller);
                    
                    if (guardLookingAtPlayer)
                    {
                        // Guard is looking at your direction - don't move!
                        MelonLogger.Msg($"[Stealth] *** PLAYING GUARD LOOKING AT YOU SOUND ***");
                        if (StealthAudioManager.Instance.HasClip(StealthAudioManager.GUARD_LOOKING_AT_YOU))
                        {
                            StealthAudioManager.Instance.PlaySpatial(StealthAudioManager.GUARD_LOOKING_AT_YOU, direction, volume: 1.0f);
                        }
                        else
                        {
                            MelonLogger.Warning("[Stealth] guard_looking_at_you.wav not found!");
                        }
                    }
                    else
                    {
                        // Guard not looking - safe to pass!
                        MelonLogger.Msg($"[Stealth] *** PLAYING GUARD NOT LOOKING SOUND ***");
                        if (StealthAudioManager.Instance.HasClip(StealthAudioManager.GUARD_NOT_LOOKING))
                        {
                            StealthAudioManager.Instance.PlaySpatial(StealthAudioManager.GUARD_NOT_LOOKING, direction, volume: 1.0f);
                        }
                        else
                        {
                            MelonLogger.Warning("[Stealth] guard_not_looking.wav not found!");
                        }
                    }
                }
            }
            else
            {
                // No nearby guards, reset state
                if (_lastGuardWasLookingAtPlayer != false)
                {
                    MelonLogger.Msg("[Stealth] No guards nearby, resetting state");
                    _lastGuardWasLookingAtPlayer = false;
                }
            }
        }

        #endregion

        #region Minigame Lifecycle

        /// <summary>
        /// Patch: Announce stealth minigame start via Minigame.StartMinigame (base class)
        /// </summary>
        [HarmonyPatch(typeof(Minigame), nameof(Minigame.StartMinigame))]
        [HarmonyPostfix]
        public static void Minigame_StartMinigame_Postfix(Minigame __instance)
        {
            // Only handle if this is a stealth minigame
            if (!(__instance is MinigameStealth))
                return;

            try
            {
                MelonLogger.Msg("========================================");
                MelonLogger.Msg("[Stealth] STEALTH MINIGAME STARTED!");
                MelonLogger.Msg($"[Stealth] Accessibility enabled: {AccessibilityMod.IsEnabled}");
                MelonLogger.Msg("========================================");

                if (!AccessibilityMod.IsEnabled)
                    return;

                // Reset state tracking
                _lastInPatrollerArea = false;
                _lastExposedState = false;
                _lastPlayerState = MinigameStealthAtmaController.State.Walk;
                _lastPatroller = null;
                _patrollerStates.Clear();
                
                // Reset safe zone tracking for new minigame
                _safeZonesInitialized = false;
                _safeZones.Clear();

                // Initialize audio manager
                var audioMgr = StealthAudioManager.Instance;
                MelonLogger.Msg($"[Stealth] Audio manager initialized: {audioMgr != null}");

                TTSManager.AnnounceUI("Stealth section started. Move left and right. Hold still to crouch and hide from guards", interrupt: true);
                MelonLogger.Msg("[Stealth] Start announcement sent");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[StealthPatches] Error in StartMinigame: {ex.Message}");
                MelonLogger.Error($"[StealthPatches] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Patch: Announce when stealth section is lost (caught)
        /// </summary>
        [HarmonyPatch(typeof(MinigameStealthPatroller), nameof(MinigameStealthPatroller.PlayCatchAnimation))]
        [HarmonyPrefix]
        public static void PlayCatchAnimation_Prefix(MinigameStealthPatroller __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                // // TTSManager.AnnounceUI("Caught by guard! Restarting stealth section", interrupt: true);
                
                // Stop all audio when caught
                if (StealthAudioManager.Instance != null)
                {
                    StealthAudioManager.Instance.StopLooping(StealthAudioManager.SAFE_ZONE_LOOP);
                    StealthAudioManager.Instance.StopAll();
                }
                
                // Reset safe zones so they reinitialize on restart
                _safeZonesInitialized = false;
                _safeZones.Clear();
                _safeZonePositions.Clear();
                
                MelonLogger.Msg("[Stealth] Player caught - audio stopped and zones reset");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[StealthPatches] Error in PlayCatchAnimation: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Calculate direction from player to patroller (-1 = left, 0 = center, 1 = right)
        /// </summary>
        private static float GetDirectionToPatroller(MinigameStealthAtmaController player, MinigameStealthPatroller patroller)
        {
            if (player == null || patroller == null)
                return 0f;

            float diff = patroller.transform.position.x - player.transform.position.x;
            
            // Normalize to -1 to 1 range
            return Mathf.Clamp(diff / 5f, -1f, 1f);
        }

        /// <summary>
        /// Convert direction value to text
        /// </summary>
        private static string GetDirectionText(float direction)
        {
            if (direction < -0.3f)
                return "left";
            else if (direction > 0.3f)
                return "right";
            else
                return "center";
        }
        /// <summary>
        /// Initialize safe zone positions based on gaps between exposed areas
        /// </summary>
        private static void InitializeSafeZones(MinigameStealth stealthInstance)
        {
            try
            {
                _safeZones.Clear();
                _safeZonePositions.Clear();

                MelonLogger.Msg("[Stealth] ====== INITIALIZING SAFE ZONES ======");

                // Find all EXPOSED areas (dangerous zones)
                MinigameStealthExposedArea[] exposedAreas = UnityEngine.Object.FindObjectsOfType<MinigameStealthExposedArea>();
                MelonLogger.Msg($"[Stealth] Found {exposedAreas.Length} exposed/dangerous areas");

                if (exposedAreas.Length == 0)
                {
                    MelonLogger.Warning("[Stealth] No exposed areas found - entire area is safe!");
                    _safeZonesInitialized = true;
                    return;
                }

                // Get bounds of all exposed areas and sort by X position
                List<(float minX, float maxX, float y)> exposedBounds = new List<(float, float, float)>();
                foreach (var exposed in exposedAreas)
                {
                    Collider2D collider = exposed.GetComponent<Collider2D>();
                    if (collider != null)
                    {
                        _safeZones.Add(collider);
                        float minX = collider.bounds.min.x;
                        float maxX = collider.bounds.max.x;
                        float y = collider.bounds.center.y;
                        exposedBounds.Add((minX, maxX, y));
                        MelonLogger.Msg($"[Stealth]   Exposed area: X={minX:F1} to {maxX:F1}, Y={y:F1}");
                    }
                }

                // Sort by minX
                exposedBounds.Sort((a, b) => a.minX.CompareTo(b.minX));

                // Calculate safe zone positions as midpoints between exposed areas
                // And add positions before first and after last exposed area
                if (exposedBounds.Count > 0)
                {
                    // Safe zone before first exposed area
                    float firstExposedStart = exposedBounds[0].minX;
                    float safeX = firstExposedStart - 2f; // 2 units before
                    _safeZonePositions.Add(new Vector3(safeX, exposedBounds[0].y, 0));
                    MelonLogger.Msg($"[Stealth]   Safe zone 1: X={safeX:F1} (before first exposed)");

                    // Safe zones between exposed areas
                    for (int i = 0; i < exposedBounds.Count - 1; i++)
                    {
                        float gap = exposedBounds[i + 1].minX - exposedBounds[i].maxX;
                        if (gap > 0.5f) // Only if there's a meaningful gap
                        {
                            float midX = (exposedBounds[i].maxX + exposedBounds[i + 1].minX) / 2f;
                            float avgY = (exposedBounds[i].y + exposedBounds[i + 1].y) / 2f;
                            _safeZonePositions.Add(new Vector3(midX, avgY, 0));
                            MelonLogger.Msg($"[Stealth]   Safe zone {_safeZonePositions.Count}: X={midX:F1} (between exposed areas, gap={gap:F1})");
                        }
                    }

                    // Safe zone after last exposed area
                    float lastExposedEnd = exposedBounds[exposedBounds.Count - 1].maxX;
                    safeX = lastExposedEnd + 2f; // 2 units after
                    _safeZonePositions.Add(new Vector3(safeX, exposedBounds[exposedBounds.Count - 1].y, 0));
                    MelonLogger.Msg($"[Stealth]   Safe zone {_safeZonePositions.Count}: X={safeX:F1} (after last exposed)");
                }

                _safeZonesInitialized = true;
                MelonLogger.Msg($"[Stealth] ====== CALCULATED {_safeZonePositions.Count} SAFE ZONE POSITIONS ======");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Stealth] Error initializing safe zones: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Update spatial audio to guide player to nearest safe zone
        /// </summary>
        private static void UpdateSafeZoneSpatialAudio(MinigameStealthAtmaController player)
        {
            // Only log every 60 frames to avoid spam
            bool shouldLog = (_safeZoneAudioFrameCounter++ % 60 == 0);

            try
            {
                if (!StealthAudioManager.Instance.HasClip(StealthAudioManager.SAFE_ZONE_LOOP))
                {
                    if (shouldLog)
                        MelonLogger.Warning($"[Stealth] Safe zone audio file not found: {StealthAudioManager.SAFE_ZONE_LOOP}");
                    return;
                }

                if (_safeZonePositions.Count == 0)
                {
                    if (shouldLog)
                        MelonLogger.Msg("[Stealth] No safe zones calculated - entire area may be safe");
                    return;
                }

                Vector3 playerPos = player.transform.position;
                
                // Find nearest safe zone position
                Vector3 nearestSafeZone = _safeZonePositions[0];
                float nearestDistance = float.MaxValue;
                
                foreach (Vector3 safePos in _safeZonePositions)
                {
                    float dist = Vector2.Distance(new Vector2(playerPos.x, playerPos.y), new Vector2(safePos.x, safePos.y));
                    if (dist < nearestDistance)
                    {
                        nearestDistance = dist;
                        nearestSafeZone = safePos;
                    }
                }

                // Calculate direction to nearest safe zone
                float directionX = nearestSafeZone.x - playerPos.x;
                float pan = Mathf.Clamp(directionX / 2f, -1f, 1f);
                
                // Volume based on distance - louder when far, quieter when close
                float normalizedDistance = Mathf.Clamp01(nearestDistance / SAFE_ZONE_DETECTION_RANGE);
                float volume = 0.8f - (normalizedDistance * 0.3f); // 0.5 to 0.8 range
                
                // When very close (within 0.5 units), play centered and quiet as confirmation
                if (nearestDistance < 0.5f)
                {
                    pan = 0f;
                    volume = 0.3f;
                    if (shouldLog)
                        MelonLogger.Msg($"[Stealth] AT SAFE ZONE! X={nearestSafeZone.x:F1} - STOP AND CROUCH!");
                }
                else if (shouldLog)
                {
                    MelonLogger.Msg($"[Stealth] Player X={playerPos.x:F1}, nearest safe zone X={nearestSafeZone.x:F1}, dirX={directionX:F2}, dist={nearestDistance:F2}, pan={pan:F2}, vol={volume:F2}");
                }

                StealthAudioManager.Instance.PlayOrUpdateLoopingSpatial(
                    StealthAudioManager.SAFE_ZONE_LOOP,
                    pan,
                    volume
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Stealth] Error in safe zone audio: {ex.Message}\n{ex.StackTrace}");
            }
        }
        #endregion
    }
}
