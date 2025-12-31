using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using MelonLoader;
using FMODUnity;
using FMOD;
using FMOD.Studio;

namespace AsftuAccessibilityMod
{
    /// <summary>
    /// Manages audio for minigames using FMOD
    /// </summary>
    public class MinigameAudioManager : MonoBehaviour
    {
        private static MinigameAudioManager _instance;
        private Dictionary<string, FMOD.Sound> _fmodSounds = new Dictionary<string, FMOD.Sound>();
        private Dictionary<string, FMOD.Channel> _activeChannels = new Dictionary<string, FMOD.Channel>();
        private string _audioFolder;
        private FMOD.System _fmodSystem;

        // Sound file names
        public const string TIMING_BAR_TONE = "timing_bar_tone.wav";
        public const string TIMING_BAR_SUCCESS = "timing_bar_success.wav";
        public const string THROW_TONE = "throw_tone.wav";
        public const string THROW_SUCCESS = "throw_success.wav";
        
        // New sound files for additional minigames
        public const string BUTTON_UP = "button_up.wav";
        public const string BUTTON_DOWN = "button_down.wav";
        public const string BUTTON_LEFT = "button_left.wav";
        public const string BUTTON_RIGHT = "button_right.wav";
        public const string DANGER_SOUND = "danger.wav";
        public const string PROGRESS_TONE = "progress_tone.wav";

        public static MinigameAudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("MinigameAudioManager");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<MinigameAudioManager>();
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

        private void Initialize()
        {
            if (_audioFolder != null)
                return;

            try
            {
                MelonLogger.Msg("[MinigameAudioManager] Initializing...");

                // Get FMOD system
                _fmodSystem = RuntimeManager.CoreSystem;
                MelonLogger.Msg($"[MinigameAudioManager] FMOD System obtained");

                // Path to Mods/AccessibilityAudio folder (same as stealth audio)
                // Use the embedded audio resources
                _audioFolder = AudioResourceExtractor.AudioFolder;

                MelonLogger.Msg($"[MinigameAudioManager] Audio folder: {_audioFolder}");

                LoadAudioFiles();
                MelonLogger.Msg("[MinigameAudioManager] ✓ Initialized");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[MinigameAudioManager] Init error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void LoadAudioFiles()
        {
            try
            {
                if (!Directory.Exists(_audioFolder))
                {
                    Directory.CreateDirectory(_audioFolder);
                    MelonLogger.Warning($"[MinigameAudioManager] Created audio folder: {_audioFolder}");
                    MelonLogger.Warning($"[MinigameAudioManager] Please add these WAV files:");
                    MelonLogger.Warning($"  - {TIMING_BAR_TONE}");
                    MelonLogger.Warning($"  - {TIMING_BAR_SUCCESS}");
                    MelonLogger.Warning($"  - {THROW_TONE}");
                    MelonLogger.Warning($"  - {THROW_SUCCESS}");
                    MelonLogger.Warning($"  - {BUTTON_UP}");
                    MelonLogger.Warning($"  - {BUTTON_DOWN}");
                    MelonLogger.Warning($"  - {BUTTON_LEFT}");
                    MelonLogger.Warning($"  - {BUTTON_RIGHT}");
                    MelonLogger.Warning($"  - {DANGER_SOUND}");
                    MelonLogger.Warning($"  - {PROGRESS_TONE}");
                    return;
                }

                // Load audio files using FMOD
                LoadFMODSound(TIMING_BAR_TONE, MODE.LOOP_OFF);  // Changed from LOOP_NORMAL - this should be a one-shot beep!
                LoadFMODSound(TIMING_BAR_SUCCESS, MODE.LOOP_OFF);
                LoadFMODSound(THROW_TONE, MODE.LOOP_NORMAL);
                LoadFMODSound(THROW_SUCCESS, MODE.LOOP_OFF);
                
                // Load new minigame sounds
                LoadFMODSound(BUTTON_UP, MODE.LOOP_OFF);
                LoadFMODSound(BUTTON_DOWN, MODE.LOOP_OFF);
                LoadFMODSound(BUTTON_LEFT, MODE.LOOP_OFF);
                LoadFMODSound(BUTTON_RIGHT, MODE.LOOP_OFF);
                LoadFMODSound(DANGER_SOUND, MODE.LOOP_OFF);
                LoadFMODSound(PROGRESS_TONE, MODE.LOOP_NORMAL);

                MelonLogger.Msg($"[MinigameAudioManager] ✓ Loaded {_fmodSounds.Count} audio files");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[MinigameAudioManager] Load error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void LoadFMODSound(string fileName, MODE mode)
        {
            string filePath = Path.Combine(_audioFolder, fileName);
            if (!File.Exists(filePath))
            {
                MelonLogger.Warning($"[MinigameAudioManager] Missing: {fileName}");
                return;
            }

            try
            {
                FMOD.Sound sound;
                RESULT result = _fmodSystem.createSound(filePath, mode | MODE.DEFAULT, out sound);
                
                if (result == RESULT.OK)
                {
                    _fmodSounds[fileName] = sound;
                    MelonLogger.Msg($"[MinigameAudioManager] ✓ Loaded: {fileName}");
                }
                else
                {
                    MelonLogger.Error($"[MinigameAudioManager] Failed to load {fileName}: {result}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[MinigameAudioManager] Error loading {fileName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Play a looping tone with pitch shifting (0.5 = half speed, 2.0 = double speed)
        /// </summary>
        public void PlayLoopingTone(string clipName, float pitch)
        {
            if (!_fmodSounds.ContainsKey(clipName))
            {
                MelonLogger.Warning($"[MinigameAudioManager] Sound not loaded: {clipName}");
                return;
            }

            try
            {
                // Stop existing channel if already playing
                if (_activeChannels.ContainsKey(clipName))
                {
                    FMOD.Channel existingChannel = _activeChannels[clipName];
                    bool isPlaying;
                    existingChannel.isPlaying(out isPlaying);
                    if (isPlaying)
                    {
                        // Just update pitch
                        existingChannel.setPitch(Mathf.Clamp(pitch, 0.5f, 3.0f));
                        return;
                    }
                    else
                    {
                        _activeChannels.Remove(clipName);
                    }
                }

                // Play sound with FMOD
                FMOD.Sound sound = _fmodSounds[clipName];
                FMOD.Channel channel;
                FMOD.ChannelGroup channelGroup = default(FMOD.ChannelGroup);
                RESULT result = _fmodSystem.playSound(sound, channelGroup, false, out channel);
                
                if (result == RESULT.OK)
                {
                    channel.setPitch(Mathf.Clamp(pitch, 0.5f, 3.0f));
                    _activeChannels[clipName] = channel;
                    
                    bool isPlaying;
                    channel.isPlaying(out isPlaying);
                    MelonLogger.Msg($"[MinigameAudioManager] Playing {clipName} at pitch {pitch:F2}, isPlaying={isPlaying}");
                }
                else
                {
                    MelonLogger.Error($"[MinigameAudioManager] Failed to play {clipName}: {result}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[MinigameAudioManager] Error playing looping tone: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Play a one-shot sound effect
        /// </summary>
        public void PlayOneShot(string clipName, float volume = 1f)
        {
            if (!_fmodSounds.ContainsKey(clipName))
            {
                MelonLogger.Warning($"[MinigameAudioManager] Sound not found: {clipName}");
                return;
            }

            try
            {
                FMOD.Sound sound = _fmodSounds[clipName];
                FMOD.Channel channel;
                FMOD.ChannelGroup channelGroup = default(FMOD.ChannelGroup);
                RESULT result = _fmodSystem.playSound(sound, channelGroup, false, out channel);
                
                if (result == RESULT.OK)
                {
                    channel.setVolume(volume * 0.7f);
                    MelonLogger.Msg($"[MinigameAudioManager] Playing one-shot: {clipName}");
                }
                else
                {
                    MelonLogger.Error($"[MinigameAudioManager] Failed to play one-shot {clipName}: {result}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[MinigameAudioManager] Error playing one-shot: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop a specific looping sound
        /// </summary>
        public void StopLoopingTone(string clipName)
        {
            if (_activeChannels.ContainsKey(clipName))
            {
                try
                {
                    FMOD.Channel channel = _activeChannels[clipName];
                    channel.stop();
                    _activeChannels.Remove(clipName);
                    MelonLogger.Msg($"[MinigameAudioManager] Stopped: {clipName}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[MinigameAudioManager] Error stopping tone: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Stop all active sounds
        /// </summary>
        public void StopAll()
        {
            try
            {
                foreach (var kvp in _activeChannels)
                {
                    kvp.Value.stop();
                }
                _activeChannels.Clear();
                MelonLogger.Msg($"[MinigameAudioManager] Stopped all sounds");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[MinigameAudioManager] Error stopping all: {ex.Message}");
            }
        }

        /// <summary>
        /// Play button sequence directional cue with stereo panning and pitch
        /// </summary>
        public void PlayButtonSequenceCue(string direction, float pan, float pitch)
        {
            try
            {
                string soundFile = null;
                
                if (direction.Contains("up"))
                    soundFile = BUTTON_UP;
                else if (direction.Contains("down"))
                    soundFile = BUTTON_DOWN;
                else if (direction.Contains("left"))
                    soundFile = BUTTON_LEFT;
                else if (direction.Contains("right"))
                    soundFile = BUTTON_RIGHT;
                
                if (soundFile != null && _fmodSounds.ContainsKey(soundFile))
                {
                    FMOD.Sound sound = _fmodSounds[soundFile];
                    FMOD.Channel channel;
                    FMOD.ChannelGroup channelGroup = default(FMOD.ChannelGroup);
                    RESULT result = _fmodSystem.playSound(sound, channelGroup, false, out channel);
                    
                    if (result == RESULT.OK)
                    {
                        channel.setPan(pan);
                        channel.setPitch(pitch);
                        channel.setVolume(0.8f);
                        MelonLogger.Msg($"[MinigameAudioManager] Playing button cue: {direction} (pan={pan}, pitch={pitch})");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[MinigameAudioManager] Error playing button cue: {ex.Message}");
            }
        }

        /// <summary>
        /// Play progress tone with variable pitch based on progress (0-1)
        /// </summary>
        public void PlayProgressTone(float progress)
        {
            try
            {
                if (!_fmodSounds.ContainsKey(PROGRESS_TONE))
                    return;

                // Stop existing progress tone
                if (_activeChannels.ContainsKey(PROGRESS_TONE))
                {
                    _activeChannels[PROGRESS_TONE].stop();
                    _activeChannels.Remove(PROGRESS_TONE);
                }

                FMOD.Sound sound = _fmodSounds[PROGRESS_TONE];
                FMOD.Channel channel;
                FMOD.ChannelGroup channelGroup = default(FMOD.ChannelGroup);
                RESULT result = _fmodSystem.playSound(sound, channelGroup, false, out channel);
                
                if (result == RESULT.OK)
                {
                    // Map progress to pitch: 0.5x at 0% to 2.0x at 100%
                    float pitch = 0.5f + (progress * 1.5f);
                    channel.setPitch(pitch);
                    channel.setVolume(0.6f);
                    channel.setMode(MODE.LOOP_OFF);
                    
                    MelonLogger.Msg($"[MinigameAudioManager] Playing progress tone: {(progress * 100):F0}% (pitch={pitch:F2})");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[MinigameAudioManager] Error playing progress tone: {ex.Message}");
            }
        }

        /// <summary>
        /// Play danger/warning sound
        /// </summary>
        public void PlayDangerSound()
        {
            try
            {
                if (_fmodSounds.ContainsKey(DANGER_SOUND))
                {
                    FMOD.Sound sound = _fmodSounds[DANGER_SOUND];
                    FMOD.Channel channel;
                    FMOD.ChannelGroup channelGroup = default(FMOD.ChannelGroup);
                    RESULT result = _fmodSystem.playSound(sound, channelGroup, false, out channel);

                    if (result == RESULT.OK)
                    {
                        channel.setVolume(0.9f);
                        MelonLogger.Msg($"[MinigameAudioManager] Playing danger sound");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[MinigameAudioManager] Error playing danger sound: {ex.Message}");
            }
        }

        /// <summary>
        /// Play a looping directional sound with stereo panning based on position
        /// Returns a unique channel key for stopping this specific sound later
        /// </summary>
        /// <param name="soundFile">Name of the sound file to play</param>
        /// <param name="worldX">X position in world space</param>
        /// <param name="worldY">Y position in world space</param>
        /// <param name="uniqueId">Unique identifier for this obstacle (to track multiple sounds)</param>
        public string PlayDirectionalLoopingSound(string soundFile, float worldX, float worldY, string uniqueId)
        {
            try
            {
                if (!_fmodSounds.ContainsKey(soundFile))
                {
                    MelonLogger.Warning($"[MinigameAudioManager] Sound not found for directional play: {soundFile}");
                    return null;
                }

                // Get player position for panning calculation
                var player = com.mojiken.asftu.PlayerController.Instance;
                if (player == null)
                {
                    MelonLogger.Warning($"[MinigameAudioManager] PlayerController not found for directional audio");
                    return null;
                }

                float playerX = player.transform.position.x;
                float distance = worldX - playerX;

                // Calculate stereo pan: -1 (left) to +1 (right)
                // Clamp to reasonable range for game world
                float pan = Mathf.Clamp(distance / 2f, -1f, 1f);

                FMOD.Sound sound = _fmodSounds[soundFile];
                FMOD.Channel channel;
                FMOD.ChannelGroup channelGroup = default(FMOD.ChannelGroup);
                RESULT result = _fmodSystem.playSound(sound, channelGroup, false, out channel);

                if (result == RESULT.OK)
                {
                    channel.setPan(pan);
                    channel.setVolume(0.8f);
                    channel.setMode(MODE.LOOP_NORMAL); // Enable looping

                    // Store channel with unique ID for later stopping
                    string channelKey = $"{soundFile}_{uniqueId}";
                    if (_activeChannels.ContainsKey(channelKey))
                    {
                        _activeChannels[channelKey].stop();
                    }
                    _activeChannels[channelKey] = channel;

                    MelonLogger.Msg($"[MinigameAudioManager] Playing looping directional {soundFile}: pan={pan:F2} (distance={distance:F2}, id={uniqueId})");
                    return channelKey;
                }
                else
                {
                    MelonLogger.Error($"[MinigameAudioManager] Failed to play directional {soundFile}: {result}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[MinigameAudioManager] Error playing directional sound: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Stop a specific directional looping sound by its channel key
        /// </summary>
        public void StopDirectionalSound(string channelKey)
        {
            if (string.IsNullOrEmpty(channelKey))
                return;

            try
            {
                if (_activeChannels.ContainsKey(channelKey))
                {
                    _activeChannels[channelKey].stop();
                    _activeChannels.Remove(channelKey);
                    MelonLogger.Msg($"[MinigameAudioManager] Stopped directional sound: {channelKey}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[MinigameAudioManager] Error stopping directional sound: {ex.Message}");
            }
        }

        /// <summary>
        /// Update panning and volume for an existing directional sound as player moves
        /// </summary>
        /// <param name="channelKey">The channel key returned from PlayDirectionalLoopingSound</param>
        /// <param name="obstacleX">X position of the obstacle</param>
        /// <param name="playerX">X position of the player</param>
        /// <param name="playerY">Y position of the player</param>
        public void UpdateDirectionalSound(string channelKey, float obstacleX, float playerX, float playerY)
        {
            if (string.IsNullOrEmpty(channelKey))
                return;

            try
            {
                if (_activeChannels.ContainsKey(channelKey))
                {
                    FMOD.Channel channel = _activeChannels[channelKey];

                    // Check if channel is still playing
                    bool isPlaying;
                    channel.isPlaying(out isPlaying);
                    if (!isPlaying)
                    {
                        _activeChannels.Remove(channelKey);
                        return;
                    }

                    // Calculate new pan based on current player position
                    float distance = obstacleX - playerX;
                    float pan = Mathf.Clamp(distance / 2f, -1f, 1f);

                    // Calculate volume based on distance (closer = louder)
                    float absDistance = Mathf.Abs(distance);
                    float volume = Mathf.Clamp(1.0f - (absDistance / 3f), 0.3f, 0.9f);

                    // Update pan and volume
                    channel.setPan(pan);
                    channel.setVolume(volume);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[MinigameAudioManager] Error updating directional sound: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            StopAll();
        }
    }
}
