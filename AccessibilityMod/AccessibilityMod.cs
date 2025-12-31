using System;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(AsftuAccessibilityMod.AccessibilityMod), "A Space for the Unbound Accessibility Mod", "1.0.0", "Accessibility Team")]
[assembly: MelonGame("Mojiken", "A Space for the Unbound")]

namespace AsftuAccessibilityMod
{
    public class AccessibilityMod : MelonMod
    {
        private static MelonPreferences_Category _prefCategory;
        private static MelonPreferences_Entry<bool> _announceDialoguePref;
        private static MelonPreferences_Entry<bool> _announceMenusPref;
        private static MelonPreferences_Entry<bool> _announceInteractablesPref;
        private static MelonPreferences_Entry<bool> _announceNotificationsPref;

        public static bool IsEnabled => true; // Always enabled
        public static bool AnnounceDialogue { get; private set; } = true;
        public static bool AnnounceMenus { get; private set; } = true;
        public static bool AnnounceInteractables { get; private set; } = true;
        public static bool AnnounceNotifications { get; private set; } = true;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Initializing A Space for the Unbound Accessibility Mod...");

            // Create preferences
            _prefCategory = MelonPreferences.CreateCategory("AccessibilityMod");
            _announceDialoguePref = _prefCategory.CreateEntry("AnnounceDialogue", true, "Announce Dialogue");
            _announceMenusPref = _prefCategory.CreateEntry("AnnounceMenus", true, "Announce Menu Navigation");
            _announceInteractablesPref = _prefCategory.CreateEntry("AnnounceInteractables", true, "Announce Interactable Objects");
            _announceNotificationsPref = _prefCategory.CreateEntry("AnnounceNotifications", true, "Announce Notifications");

            LoadPreferences();

            // Initialize TOLK
            if (!TolkWrapper.Initialize())
            {
                LoggerInstance.Warning("TOLK initialization failed. Screen reader support may not work.");
                LoggerInstance.Warning("Make sure Tolk.dll, nvdaControllerClient64.dll, and nvdaControllerClient32.dll are in the game directory.");
            }
            else
            {
                LoggerInstance.Msg($"TOLK initialized successfully. Screen reader detected: {TolkWrapper.DetectScreenReader()}");
                LoggerInstance.Msg($"Speech available: {TolkWrapper.HasSpeech()}");
                LoggerInstance.Msg($"Braille available: {TolkWrapper.HasBraille()}");
                
                // Initialize TTS Manager
                TTSManager.Initialize();
                
                // Welcome message
                TTSManager.Speak("A Space for the Unbound Accessibility Mod loaded successfully", interrupt: true, priority: 10);
            }

            // Apply Harmony patches
            try
            {
                var harmony = new HarmonyLib.Harmony("com.accessibilityteam.asftuaccessibility");
                harmony.PatchAll();
                LoggerInstance.Msg("Harmony patches applied successfully!");
                
                // Pre-initialize audio managers to ensure they're ready
                var stealthAudio = StealthAudioManager.Instance;
                LoggerInstance.Msg($"StealthAudioManager pre-initialized: {stealthAudio != null}");
                
                var interactableAudio = InteractableAudioManager.Instance;
                LoggerInstance.Msg($"InteractableAudioManager pre-initialized: {interactableAudio != null}");
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"Failed to apply Harmony patches: {ex.Message}");
                LoggerInstance.Error(ex.StackTrace);
            }

            LoggerInstance.Msg("Accessibility Mod initialized successfully!");
        }

        public override void OnUpdate()
        {
            if (!TolkWrapper.IsInitialized)
                return;

            // Update TTS Manager
            TTSManager.Update();

            // Silence speech with Ctrl+S
            if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftControl) && UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.S))
            {
                TTSManager.Silence();
                LoggerInstance.Msg("Speech silenced");
            }
        }

        public override void OnApplicationQuit()
        {
            LoggerInstance.Msg("Shutting down Accessibility Mod...");
            TolkWrapper.Shutdown();
        }

        private void LoadPreferences()
        {
            AnnounceDialogue = _announceDialoguePref.Value;
            AnnounceMenus = _announceMenusPref.Value;
            AnnounceInteractables = _announceInteractablesPref.Value;
            AnnounceNotifications = _announceNotificationsPref.Value;
        }
    }
}
