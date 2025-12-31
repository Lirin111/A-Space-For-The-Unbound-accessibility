using System;
using HarmonyLib;
using MelonLoader;

namespace AsftuAccessibilityMod.Patches
{
    /// <summary>
    /// Patches for notifications, tutorials, and game messages
    /// </summary>
    [HarmonyPatch]
    public static class NotificationPatches
    {
        // Track last announcement to prevent duplicates
        private static string _lastAnnouncedNotification = "";
        private static float _lastAnnouncementTime = 0f;
        private const float DUPLICATE_PREVENTION_WINDOW = 0.5f; // 0.5 seconds

        // Patch NotificationManager.AddNotificationTodoList
        [HarmonyPatch(typeof(NotificationManager), nameof(NotificationManager.AddNotificationTodoList))]
        [HarmonyPostfix]
        public static void AddNotificationTodoList_Postfix()
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceNotifications)
                return;

            try
            {
                string text = LocalizationManager.GetLocalizedGlobalValue(LocalizationGlobalKey.newObjective);
                AnnounceIfNotDuplicate(text);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in AddNotificationTodoList_Postfix: {ex.Message}");
            }
        }

        // Patch NotificationManager.AddNotificationTodoListComplete
        [HarmonyPatch(typeof(NotificationManager), nameof(NotificationManager.AddNotificationTodoListComplete))]
        [HarmonyPostfix]
        public static void AddNotificationTodoListComplete_Postfix()
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceNotifications)
                return;

            try
            {
                string text = LocalizationManager.GetLocalizedGlobalValue(LocalizationGlobalKey.objectiveCompleted);
                AnnounceIfNotDuplicate(text);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in AddNotificationTodoListComplete_Postfix: {ex.Message}");
            }
        }

        // Patch NotificationManager.AddNotificationBucketListComplete
        [HarmonyPatch(typeof(NotificationManager), nameof(NotificationManager.AddNotificationBucketListComplete))]
        [HarmonyPostfix]
        public static void AddNotificationBucketListComplete_Postfix()
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceNotifications)
                return;

            try
            {
                string text = LocalizationManager.GetLocalizedGlobalValue(LocalizationGlobalKey.bucketListCompleted);
                TTSManager.AnnounceNotification(text, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in AddNotificationBucketListComplete_Postfix: {ex.Message}");
            }
        }

        // Patch NotificationManager.AddNotificationCollectible
        [HarmonyPatch(typeof(NotificationManager), nameof(NotificationManager.AddNotificationCollectible))]
        [HarmonyPostfix]
        public static void AddNotificationCollectible_Postfix(string label)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceNotifications)
                return;

            try
            {
                TTSManager.AnnounceNotification($"Collectible found: {label}", interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in AddNotificationCollectible_Postfix: {ex.Message}");
            }
        }

        // Patch NotificationManager.AddNotificationInventory
        [HarmonyPatch(typeof(NotificationManager), nameof(NotificationManager.AddNotificationInventory))]
        [HarmonyPostfix]
        public static void AddNotificationInventory_Postfix(string label)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceNotifications)
                return;

            try
            {
                AnnounceIfNotDuplicate($"Item received: {label}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in AddNotificationInventory_Postfix: {ex.Message}");
            }
        }

        // Patch NotificationManager.AddNotificationFairyTale
        [HarmonyPatch(typeof(NotificationManager), nameof(NotificationManager.AddNotificationFairyTale))]
        [HarmonyPostfix]
        public static void AddNotificationFairyTale_Postfix()
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceNotifications)
                return;

            try
            {
                string text = LocalizationManager.GetLocalizedGlobalValue(LocalizationGlobalKey.newFairyTale);
                TTSManager.AnnounceNotification(text, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in AddNotificationFairyTale_Postfix: {ex.Message}");
            }
        }

        // Patch QuestManager methods to announce quest updates
        [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.AddQuestIntoTodoList))]
        [HarmonyPostfix]
        public static void AddQuestIntoTodoList_Postfix(QuestData target)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceNotifications)
                return;

            try
            {
                if (target != null)
                {
                    string questName = target.name;
                    // Remove chapter prefix (e.g., "Ch1_", "Ch2_", etc.)
                    questName = System.Text.RegularExpressions.Regex.Replace(questName, @"^Ch\d+_", "");
                    AnnounceIfNotDuplicate($"New quest: {questName}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in AddQuestIntoTodoList_Postfix: {ex.Message}");
            }
        }

        // Patch SceneLoad.MoveScene to announce location changes
        [HarmonyPatch(typeof(com.mojiken.asftu.SceneLoad), nameof(com.mojiken.asftu.SceneLoad.MoveScene))]
        [HarmonyPrefix]
        public static void MoveScene_Prefix(SceneLoadSetting sceneLoadSetting)
        {
            if (!AccessibilityMod.IsEnabled || !AccessibilityMod.AnnounceNotifications)
                return;

            try
            {
                // Get the target scene name from the setting
                string sceneName = sceneLoadSetting.GetTargetSceneName();
                
                if (!string.IsNullOrEmpty(sceneName))
                {
                    // Format and announce the scene name
                    string formattedName = FormatSceneName(sceneName);
                    AnnounceIfNotDuplicate(formattedName, useUIAnnouncement: true);
                    MelonLogger.Msg($"[Scene] Announcing scene: {formattedName}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in MoveScene_Prefix: {ex.Message}");
            }
        }

        // Helper to prevent duplicate announcements
        private static void AnnounceIfNotDuplicate(string text, bool useUIAnnouncement = false)
        {
            float currentTime = UnityEngine.Time.unscaledTime;
            
            // Check if this is the same announcement within the prevention window
            if (text == _lastAnnouncedNotification && 
                (currentTime - _lastAnnouncementTime) < DUPLICATE_PREVENTION_WINDOW)
            {
                MelonLogger.Msg($"[Notifications] Prevented duplicate: {text}");
                return;
            }
            
            _lastAnnouncedNotification = text;
            _lastAnnouncementTime = currentTime;
            
            if (useUIAnnouncement)
            {
                TTSManager.AnnounceUI(text, interrupt: false);
            }
            else
            {
                TTSManager.AnnounceNotification(text, interrupt: false);
            }
        }

        // Helper to format scene names
        private static string FormatSceneName(string sceneName)
        {
            // Add spaces before capital letters
            sceneName = System.Text.RegularExpressions.Regex.Replace(sceneName, "([a-z])([A-Z])", "$1 $2");
            // Replace underscores with spaces
            sceneName = sceneName.Replace("_", " ");
            return sceneName.Trim();
        }

        // Track announced splash screens to prevent duplicates
        private static System.Collections.Generic.HashSet<string> _announcedSplashScreens = new System.Collections.Generic.HashSet<string>();

        // Patch SplashScreen.DoSplash to announce warning screens
        [HarmonyPatch(typeof(SplashScreen), "DoSplash")]
        [HarmonyPostfix]
        public static void SplashScreen_DoSplash_Postfix(SplashScreen __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            try
            {
                __instance.StartCoroutine(MonitorSplashScreens(__instance));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in SplashScreen_DoSplash_Postfix: {ex.Message}");
            }
        }

        private static System.Collections.IEnumerator MonitorSplashScreens(SplashScreen splashScreen)
        {
            _announcedSplashScreens.Clear();
            
            // Get all splash objects using reflection
            var triggerWarningThemeField = typeof(SplashScreen).GetField("triggerWarningTheme", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var triggerWarningFlashField = typeof(SplashScreen).GetField("triggerWarningFlash", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var themeWarning = triggerWarningThemeField?.GetValue(splashScreen) as SplashObject;
            var flashWarning = triggerWarningFlashField?.GetValue(splashScreen) as SplashObject;
            
            while (splashScreen != null && splashScreen.gameObject.activeSelf)
            {
                // Check trigger warning theme
                if (themeWarning != null && themeWarning.gameObject.activeSelf && !_announcedSplashScreens.Contains("theme"))
                {
                    _announcedSplashScreens.Add("theme");
                    string announcement = LocalizationManager.GetLocalizedGlobalValue(LocalizationGlobalKey.triggerWarningTheme);
                    TTSManager.AnnounceUI(announcement, interrupt: true);
                    MelonLogger.Msg($"[SplashScreen] Announced theme warning");
                    
                    // Wait a bit then announce "press any button"
                    yield return new UnityEngine.WaitForSeconds(2f);
                    if (themeWarning.skipable && themeWarning.gameObject.activeSelf)
                    {
                        string pressButton = LocalizationManager.GetLocalizedGlobalValue(LocalizationGlobalKey.pressAnyButton);
                        TTSManager.AnnounceUI(pressButton, interrupt: false);
                    }
                }
                
                // Check trigger warning flash
                if (flashWarning != null && flashWarning.gameObject.activeSelf && !_announcedSplashScreens.Contains("flash"))
                {
                    _announcedSplashScreens.Add("flash");
                    string announcement = LocalizationManager.GetLocalizedGlobalValue(LocalizationGlobalKey.triggerWarningFlash);
                    TTSManager.AnnounceUI(announcement, interrupt: true);
                    MelonLogger.Msg($"[SplashScreen] Announced flash warning");
                    
                    // Wait a bit then announce "press any button"
                    yield return new UnityEngine.WaitForSeconds(2f);
                    if (flashWarning.skipable && flashWarning.gameObject.activeSelf)
                    {
                        string pressButton = LocalizationManager.GetLocalizedGlobalValue(LocalizationGlobalKey.pressAnyButton);
                        TTSManager.AnnounceUI(pressButton, interrupt: false);
                    }
                }
                
                yield return new UnityEngine.WaitForSeconds(0.2f); // Check every 0.2 seconds
            }
        }
    }
}
