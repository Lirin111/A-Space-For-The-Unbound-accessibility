using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MelonLoader;
using FMODUnity;
using FMOD;
using FMOD.Studio;

namespace AsftuAccessibilityMod
{
    /// <summary>
    /// Manages spatial audio for stealth sections using FMOD
    /// </summary>
    public class StealthAudioManager : MonoBehaviour
    {
        private static StealthAudioManager _instance;
        private Dictionary<string, FMOD.Sound> _fmodSounds = new Dictionary<string, FMOD.Sound>();
        private Dictionary<string, FMOD.Channel> _activeChannels = new Dictionary<string, FMOD.Channel>();
        private FMOD.System _fmodSystem;
        private string _audioFolder;

        public static StealthAudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("StealthAudioManager");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<StealthAudioManager>();
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

        public const string ENTERING_GUARD_AREA = "entering_guard_area.wav";
        public const string EXITING_GUARD_AREA = "exiting_guard_area.wav";
        public const string GUARD_SEARCHING = "guard_searching.wav";
        public const string GUARD_ALERT = "guard_alert.wav";
        public const string NOW_CROUCHING = "now_crouching.wav";
        public const string NOW_WALKING = "now_walking.wav";
        public const string SAFE_AREA = "safe_area.wav";
        public const string SAFE_ZONE_LOOP = "safe_zone_loop.wav";
        public const string DANGER_EXPOSED = "danger_exposed.wav";
        public const string PROXIMITY_BEEP_NEAR = "proximity_beep_near.wav";
        public const string PROXIMITY_BEEP_MEDIUM = "proximity_beep_medium.wav";
        public const string PROXIMITY_BEEP_FAR = "proximity_beep_far.wav";
        public const string GUARD_LOOKING_AT_YOU = "guard_looking_at_you.wav";
        public const string GUARD_NOT_LOOKING = "guard_not_looking.wav";

        private float _lastProximityBeepTime = 0f;
        private const float PROXIMITY_BEEP_INTERVAL = 1.5f;

        private void Initialize()
        {
            if (_audioFolder != null)
                return;

            try
            {
                MelonLogger.Msg("[StealthAudioManager] Initializing...");

                // Get FMOD system
                _fmodSystem = RuntimeManager.CoreSystem;
                if (_fmodSystem.handle == IntPtr.Zero)
                {
                    MelonLogger.Error("[StealthAudioManager] Failed to get FMOD system");
                    return;
                }

                // Use the embedded audio resources
                _audioFolder = AudioResourceExtractor.AudioFolder;

                LoadAudioFiles();
                MelonLogger.Msg("[StealthAudioManager] ✓ Initialized with FMOD");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[StealthAudioManager] Init error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void LoadAudioFiles()
        {
            try
            {
                if (!Directory.Exists(_audioFolder))
                {
                    Directory.CreateDirectory(_audioFolder);
                    MelonLogger.Warning($"[StealthAudioManager] Created audio folder. Add WAV files!");
                    return;
                }

                string[] wavFiles = Directory.GetFiles(_audioFolder, "*.wav");
                MelonLogger.Msg($"[StealthAudioManager] Found {wavFiles.Length} WAV files");

                foreach (string filePath in wavFiles)
                {
                    string fileName = Path.GetFileName(filePath);
                    LoadFMODSound(fileName, filePath);
                }

                MelonLogger.Msg($"[StealthAudioManager] ✓ Loaded {_fmodSounds.Count} sounds");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[StealthAudioManager] Load error: {ex.Message}");
            }
        }

        private void LoadFMODSound(string fileName, string filePath)
        {
            try
            {
                // Check if this should be a looping sound
                MODE mode = MODE.DEFAULT;
                if (fileName == SAFE_ZONE_LOOP)
                {
                    mode = MODE.LOOP_NORMAL; // Loop the safe zone sound
                }
                else
                {
                    mode = MODE.LOOP_OFF; // One-shot sound
                }
                
                FMOD.Sound sound;
                RESULT result = _fmodSystem.createSound(filePath, mode, out sound);
                
                if (result == RESULT.OK)
                {
                    _fmodSounds[fileName] = sound;
                    MelonLogger.Msg($"[StealthAudioManager] ✓ Loaded: {fileName} (mode: {mode})");
                }
                else
                {
                    MelonLogger.Error($"[StealthAudioManager] Failed to load {fileName}: {result}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[StealthAudioManager] Error loading {fileName}: {ex.Message}");
            }
        }

        public void PlaySpatial(string clipName, float direction, float volume = 1f)
        {
            PlayAudio(clipName, direction, volume);
        }

        public void PlayCentered(string clipName, float volume = 1f)
        {
            PlayAudio(clipName, 0f, volume);
        }

        public void PlayProximityBeep(float distance, float direction)
        {
            if (Time.unscaledTime - _lastProximityBeepTime < PROXIMITY_BEEP_INTERVAL)
                return;

            _lastProximityBeepTime = Time.unscaledTime;

            string clipName;
            if (distance < 2f)
                clipName = PROXIMITY_BEEP_NEAR;
            else if (distance < 5f)
                clipName = PROXIMITY_BEEP_MEDIUM;
            else
                clipName = PROXIMITY_BEEP_FAR;

            float volume = Mathf.Clamp01(1f - (distance / 10f));
            PlayAudio(clipName, direction, volume);
        }

        private void PlayAudio(string clipName, float pan, float volume)
        {
            try
            {
                if (_fmodSounds.Count == 0 && _audioFolder != null)
                    LoadAudioFiles();

                if (!_fmodSounds.ContainsKey(clipName))
                {
                    MelonLogger.Warning($"[StealthAudioManager] Not found: {clipName}");
                    return;
                }

                FMOD.Sound sound = _fmodSounds[clipName];
                FMOD.Channel channel;
                FMOD.ChannelGroup channelGroup = default(FMOD.ChannelGroup);
                
                RESULT result = _fmodSystem.playSound(sound, channelGroup, false, out channel);
                
                if (result == RESULT.OK)
                {
                    // Apply stereo panning (-1 = left, 0 = center, 1 = right)
                    channel.setPan(Mathf.Clamp(pan, -1f, 1f));
                    
                    // Apply volume
                    channel.setVolume(Mathf.Clamp01(volume));
                    
                    // Track channel with unique key
                    string channelKey = $"{clipName}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                    _activeChannels[channelKey] = channel;
                    
                    MelonLogger.Msg($"[StealthAudioManager] ✓ Playing: {clipName} (pan: {pan:F2}, vol: {volume:F2})");
                }
                else
                {
                    MelonLogger.Error($"[StealthAudioManager] Failed to play {clipName}: {result}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[StealthAudioManager] Play error for {clipName}: {ex.Message}");
            }
        }

        public void StopAll()
        {
            try
            {
                foreach (var kvp in _activeChannels)
                {
                    kvp.Value.stop();
                }
                _activeChannels.Clear();
                MelonLogger.Msg("[StealthAudioManager] Stopped all sounds");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[StealthAudioManager] Error stopping all: {ex.Message}");
            }
        }

        /// <summary>
        /// Play or update a looping spatial sound (like safe zone loop)
        /// </summary>
        public void PlayOrUpdateLoopingSpatial(string clipName, float pan, float volume)
        {
            try
            {
                if (!_fmodSounds.ContainsKey(clipName))
                {
                    MelonLogger.Warning($"[StealthAudioManager] Not found: {clipName}");
                    return;
                }

                // Check if this sound is already playing
                if (_activeChannels.ContainsKey(clipName))
                {
                    FMOD.Channel channel = _activeChannels[clipName];
                    
                    // Check if channel is still playing
                    bool isPlaying = false;
                    channel.isPlaying(out isPlaying);
                    
                    if (isPlaying)
                    {
                        // Update pan and volume
                        channel.setPan(Mathf.Clamp(pan, -1f, 1f));
                        channel.setVolume(Mathf.Clamp01(volume));
                        return;
                    }
                    else
                    {
                        // Channel stopped, remove it
                        _activeChannels.Remove(clipName);
                    }
                }

                // Start playing the looping sound
                FMOD.Sound sound = _fmodSounds[clipName];
                FMOD.Channel channel2;
                FMOD.ChannelGroup channelGroup = default(FMOD.ChannelGroup);
                
                RESULT result = _fmodSystem.playSound(sound, channelGroup, false, out channel2);
                
                if (result == RESULT.OK)
                {
                    channel2.setPan(Mathf.Clamp(pan, -1f, 1f));
                    channel2.setVolume(Mathf.Clamp01(volume));
                    _activeChannels[clipName] = channel2;
                    MelonLogger.Msg($"[StealthAudioManager] ✓ Started looping: {clipName} (pan: {pan:F2}, vol: {volume:F2})");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[StealthAudioManager] Error with looping audio {clipName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop a specific looping sound
        /// </summary>
        public void StopLooping(string clipName)
        {
            try
            {
                if (_activeChannels.ContainsKey(clipName))
                {
                    _activeChannels[clipName].stop();
                    _activeChannels.Remove(clipName);
                    MelonLogger.Msg($"[StealthAudioManager] Stopped looping: {clipName}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[StealthAudioManager] Error stopping {clipName}: {ex.Message}");
            }
        }

        public bool HasClip(string clipName)
        {
            return _fmodSounds.ContainsKey(clipName);
        }

        private void OnDestroy()
        {
            StopAll();
        }
    }
}