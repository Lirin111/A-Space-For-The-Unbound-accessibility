using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MelonLoader;
using FMODUnity;
using FMOD;
using FMOD.Studio;
using com.mojiken.asftu;

namespace AsftuAccessibilityMod
{
    /// <summary>
    /// Manages audio cues for interactable objects using FMOD
    /// </summary>
    public class InteractableAudioManager : MonoBehaviour
    {
        private static InteractableAudioManager _instance;
        private Dictionary<string, FMOD.Sound> _fmodSounds = new Dictionary<string, FMOD.Sound>();
        private Dictionary<string, FMOD.Channel> _activeChannels = new Dictionary<string, FMOD.Channel>();
        private FMOD.System _fmodSystem;
        private string _audioFolder;

        // Track currently active interactable
        private InteractableObject _currentInteractable = null;
        private InteractableType _currentType = InteractableType.Item;

        public static InteractableAudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("InteractableAudioManager");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<InteractableAudioManager>();
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        // Audio file names
        public const string DOOR_STAIRS = "door_stairs.wav";
        public const string CHARACTER = "character.wav";
        public const string ITEM = "item.wav";

        private void Initialize()
        {
            if (_audioFolder != null)
                return;

            try
            {
                MelonLogger.Msg("[InteractableAudioManager] Initializing...");

                // Get FMOD system
                _fmodSystem = RuntimeManager.CoreSystem;
                if (_fmodSystem.handle == IntPtr.Zero)
                {
                    MelonLogger.Error("[InteractableAudioManager] Failed to get FMOD system");
                    return;
                }

                // Use the embedded audio resources
                _audioFolder = AudioResourceExtractor.AudioFolder;

                LoadAudioFiles();
                MelonLogger.Msg("[InteractableAudioManager] ✓ Initialized with FMOD");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[InteractableAudioManager] Init error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void LoadAudioFiles()
        {
            try
            {
                if (!Directory.Exists(_audioFolder))
                {
                    Directory.CreateDirectory(_audioFolder);
                    MelonLogger.Warning($"[InteractableAudioManager] Created audio folder: {_audioFolder}");
                    MelonLogger.Warning($"[InteractableAudioManager] Please add these WAV files:");
                    MelonLogger.Warning($"  - {DOOR_STAIRS}");
                    MelonLogger.Warning($"  - {CHARACTER}");
                    MelonLogger.Warning($"  - {ITEM}");
                    return;
                }

                // Load audio files using FMOD
                LoadFMODSound(DOOR_STAIRS);
                LoadFMODSound(CHARACTER);
                LoadFMODSound(ITEM);

                MelonLogger.Msg($"[InteractableAudioManager] ✓ Loaded {_fmodSounds.Count} audio files");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[InteractableAudioManager] Load error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void LoadFMODSound(string fileName)
        {
            string filePath = Path.Combine(_audioFolder, fileName);
            if (!File.Exists(filePath))
            {
                MelonLogger.Warning($"[InteractableAudioManager] Missing: {fileName}");
                return;
            }

            try
            {
                FMOD.Sound sound;
                RESULT result = _fmodSystem.createSound(filePath, MODE.LOOP_NORMAL | MODE.DEFAULT, out sound);
                
                if (result == RESULT.OK)
                {
                    _fmodSounds[fileName] = sound;
                    MelonLogger.Msg($"[InteractableAudioManager] ✓ Loaded: {fileName}");
                }
                else
                {
                    MelonLogger.Error($"[InteractableAudioManager] Failed to load {fileName}: {result}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[InteractableAudioManager] Error loading {fileName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Start playing looped audio for an interactable with spatial positioning
        /// </summary>
        public void StartInteractableAudio(InteractableObject interactable, InteractableType type)
        {
            try
            {
                if (interactable == null)
                {
                    StopInteractableAudio();
                    return;
                }

                // If same object, just update position
                if (_currentInteractable == interactable)
                {
                    UpdateSpatialAudio(interactable);
                    return;
                }

                // Stop previous audio
                StopInteractableAudio();

                // Start new audio
                _currentInteractable = interactable;
                _currentType = type;

                string clipName = GetClipForType(type);
                if (!_fmodSounds.ContainsKey(clipName))
                    return;

                FMOD.Sound sound = _fmodSounds[clipName];
                FMOD.Channel channel;
                FMOD.ChannelGroup channelGroup = default(FMOD.ChannelGroup);
                RESULT result = _fmodSystem.playSound(sound, channelGroup, false, out channel);
                
                if (result == RESULT.OK)
                {
                    _activeChannels[clipName] = channel;
                    UpdateSpatialAudio(interactable);
                }
                else
                {
                    MelonLogger.Error($"[InteractableAudioManager] Failed to play {clipName}: {result}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[InteractableAudioManager] Error in StartInteractableAudio: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop playing interactable audio
        /// </summary>
        public void StopInteractableAudio()
        {
            try
            {
                foreach (var kvp in _activeChannels)
                {
                    kvp.Value.stop();
                }
                _activeChannels.Clear();
                _currentInteractable = null;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[InteractableAudioManager] Error in StopInteractableAudio: {ex.Message}");
            }
        }

        private void Update()
        {
            // Update spatial audio position every frame
            if (_currentInteractable != null)
            {
                UpdateSpatialAudio(_currentInteractable);
            }
        }

        /// <summary>
        /// Update spatial audio based on player position relative to interactable
        /// </summary>
        private void UpdateSpatialAudio(InteractableObject interactable)
        {
            try
            {
                if (interactable == null)
                    return;

                string clipName = GetClipForType(_currentType);
                if (!_activeChannels.ContainsKey(clipName))
                    return;

                FMOD.Channel channel = _activeChannels[clipName];

                // Get player position through CharacterInteract component
                var characterInteract = GameObject.FindObjectOfType<CharacterInteract>();
                if (characterInteract == null)
                    return;

                Vector3 playerPos = characterInteract.transform.position;
                Vector3 objectPos = interactable.transform.position;

                // Calculate relative position
                float distance = Vector3.Distance(playerPos, objectPos);
                float horizontalDiff = objectPos.x - playerPos.x;

                // Calculate pan based on horizontal position (-1 = left, 0 = center, 1 = right)
                // Reduced from 5 to 2 units for more pronounced stereo positioning
                float pan = Mathf.Clamp(horizontalDiff / 2f, -1f, 1f); // 2 units = full pan

                // Calculate volume based on distance (louder when closer)
                // Extended from 10 to 20 units so you hear it before being close enough to interact
                float volume = Mathf.Clamp01(1f - (distance / 20f)); // Full volume at 0, silent at 20 units
                volume = Mathf.Max(volume, 0.05f); // Lower minimum volume for better directional awareness

                // Apply pan and volume
                channel.setPan(pan);
                channel.setVolume(volume);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[InteractableAudioManager] Error updating spatial audio: {ex.Message}");
            }
        }

        /// <summary>
        /// Play audio cue for interactable based on type (legacy - now using looping audio)
        /// </summary>
        public void PlayInteractableCue(InteractableType type, float volume = 0.8f)
        {
            // This method is now deprecated in favor of StartInteractableAudio
            // Kept for backward compatibility
        }

        private string GetClipForType(InteractableType type)
        {
            switch (type)
            {
                case InteractableType.DoorStairs:
                    return DOOR_STAIRS;
                case InteractableType.Character:
                    return CHARACTER;
                case InteractableType.Item:
                    return ITEM;
                default:
                    return ITEM;
            }
        }

        public bool HasClip(string clipName)
        {
            return _fmodSounds.ContainsKey(clipName);
        }

        private void OnDestroy()
        {
            // Clean up FMOD sounds
            try
            {
                StopInteractableAudio();
                
                foreach (var sound in _fmodSounds.Values)
                {
                    sound.release();
                }
                _fmodSounds.Clear();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[InteractableAudioManager] Error in OnDestroy: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Types of interactable objects
    /// </summary>
    public enum InteractableType
    {
        DoorStairs,
        Character,
        Item
    }
}
