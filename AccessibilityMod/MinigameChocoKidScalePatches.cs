using HarmonyLib;
using MelonLoader;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AsftuAccessibilityMod.Patches
{
    [HarmonyPatch(typeof(MinigameChocoKidScale))]
    public static class MinigameChocoKidScalePatches
    {
        private static float lastAnnouncedWeight = -1f;
        private static float currentTargetWeight = 0f;
        private static Dictionary<int, string> foodIndexToName = new Dictionary<int, string>();
        private static bool hasAnnouncedFoodList = false;

        [HarmonyPatch("StartMinigameOverride")]
        [HarmonyPrefix]
        public static void StartMinigameOverride_Prefix(MinigameChocoKidScale __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            // Reset state for new minigame
            lastAnnouncedWeight = -1f;
            hasAnnouncedFoodList = false;
            
            // Get target weight from the private field
            currentTargetWeight = AccessTools.Field(typeof(MinigameChocoKidScale), "targetWeight").GetValue(__instance) as float? ?? 0f;
            
            // Build food name mapping from sprite names
            foodIndexToName.Clear();
            var foodSpriteList = __instance.foodSpriteList;
            var foodWeightList = __instance.foodWeightList;
            
            for (int i = 0; i < foodSpriteList.Count; i++)
            {
                if (foodSpriteList[i] != null)
                {
                    string foodName = CleanFoodName(foodSpriteList[i].name);
                    foodIndexToName[i] = foodName;
                }
            }
            
            // Announce minigame start with target weight
            string announcement = $"Chocolate scale minigame started. Target weight: {currentTargetWeight} points.";
            TTSManager.AnnounceUI(announcement, true);
            
            // Announce available foods and their weights
            if (foodIndexToName.Count > 0)
            {
                System.Text.StringBuilder foodList = new System.Text.StringBuilder();
                foodList.AppendLine("Available food items:");
                
                for (int i = 0; i < foodSpriteList.Count; i++)
                {
                    if (foodIndexToName.ContainsKey(i))
                    {
                        int weight = __instance.GetFoodWeight(i);
                        foodList.AppendLine($"{foodIndexToName[i]}: {weight} points");
                    }
                }
                
                TTSManager.AnnounceUI(foodList.ToString(), true);
                hasAnnouncedFoodList = true;
            }
        }

        [HarmonyPatch("SetFood0")]
        [HarmonyPostfix]
        public static void SetFood0_Postfix(MinigameChocoKidScale __instance, Sprite sprite)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            AnnounceFoodChange(__instance, "first", sprite);
        }

        [HarmonyPatch("SetFood1")]
        [HarmonyPostfix]
        public static void SetFood1_Postfix(MinigameChocoKidScale __instance, Sprite sprite)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            AnnounceFoodChange(__instance, "second", sprite);
        }

        [HarmonyPatch("UpdateWeight")]
        [HarmonyPostfix]
        public static void UpdateWeight_Postfix(MinigameChocoKidScale __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            // This is called after weight calculations, announce the result
            AnnounceCurrentWeight(__instance);
        }

        [HarmonyPatch("UpdateSprite")]
        [HarmonyPostfix]
        public static void UpdateSprite_Postfix(MinigameChocoKidScale __instance)
        {
            if (!AccessibilityMod.IsEnabled)
                return;

            // Check if balanced
            var food0Variable = __instance.food0Variable;
            var food1Variable = __instance.food1Variable;
            
            int food0Index = GlobalFlowchart.GetFlowchart().GetIntegerVariable(food0Variable.Key);
            int food1Index = GlobalFlowchart.GetFlowchart().GetIntegerVariable(food1Variable.Key);
            
            int currentWeight = __instance.GetFoodWeight(food0Index) + __instance.GetFoodWeight(food1Index);
            
            if ((float)currentWeight == currentTargetWeight)
            {
                TTSManager.AnnounceUI($"Perfect balance! The scale is balanced at {currentWeight} points. Round complete!", true);
                lastAnnouncedWeight = -1f; // Reset for next round
            }
        }

        private static void AnnounceFoodChange(MinigameChocoKidScale instance, string slotName, Sprite sprite)
        {
            string foodName;
            int weight = 0;
            
            if (sprite == null)
            {
                foodName = "Empty";
                TTSManager.AnnounceUI($"{slotName} slot cleared.", true);
            }
            else
            {
                int spriteIndex = instance.foodSpriteList.IndexOf(sprite);
                if (spriteIndex >= 0 && foodIndexToName.ContainsKey(spriteIndex))
                {
                    foodName = foodIndexToName[spriteIndex];
                    weight = instance.GetFoodWeight(spriteIndex);
                    TTSManager.AnnounceUI($"{foodName} placed in {slotName} slot. Weight: {weight} points.", true);
                }
                else
                {
                    foodName = CleanFoodName(sprite.name);
                    TTSManager.AnnounceUI($"{foodName} placed in {slotName} slot.", true);
                }
            }
        }

        private static void AnnounceCurrentWeight(MinigameChocoKidScale instance)
        {
            var food0Variable = instance.food0Variable;
            var food1Variable = instance.food1Variable;
            
            int food0Index = GlobalFlowchart.GetFlowchart().GetIntegerVariable(food0Variable.Key);
            int food1Index = GlobalFlowchart.GetFlowchart().GetIntegerVariable(food1Variable.Key);
            
            int currentWeight = instance.GetFoodWeight(food0Index) + instance.GetFoodWeight(food1Index);
            
            // Only announce if weight changed
            if (currentWeight != lastAnnouncedWeight)
            {
                lastAnnouncedWeight = currentWeight;
                
                float difference = currentTargetWeight - currentWeight;
                
                System.Text.StringBuilder announcement = new System.Text.StringBuilder();
                
                // List current items
                List<string> items = new List<string>();
                if (food0Index >= 0 && foodIndexToName.ContainsKey(food0Index))
                {
                    items.Add($"{foodIndexToName[food0Index]} ({instance.GetFoodWeight(food0Index)} points)");
                }
                if (food1Index >= 0 && foodIndexToName.ContainsKey(food1Index))
                {
                    items.Add($"{foodIndexToName[food1Index]} ({instance.GetFoodWeight(food1Index)} points)");
                }
                
                if (items.Count > 0)
                {
                    announcement.Append("Current items: ");
                    announcement.Append(string.Join(" and ", items));
                    announcement.Append(". ");
                }
                else
                {
                    announcement.Append("No items on scale. ");
                }
                
                announcement.Append($"Total weight: {currentWeight} points. ");
                announcement.Append($"Target weight: {currentTargetWeight} points. ");
                
                if (difference > 0)
                {
                    announcement.Append($"Need {difference} more points. Too light.");
                }
                else if (difference < 0)
                {
                    announcement.Append($"{Mathf.Abs(difference)} points too heavy.");
                }
                else
                {
                    announcement.Append("Perfect balance!");
                }
                
                TTSManager.AnnounceUI(announcement.ToString(), true);
            }
        }

        private static string CleanFoodName(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName))
                return "Unknown Food";
            
            // Remove common sprite suffixes and clean up the name
            string cleaned = spriteName
                .Replace("_sprite", "")
                .Replace("_Sprite", "")
                .Replace("_icon", "")
                .Replace("_Icon", "")
                .Replace("_", " ")
                .Trim();
            
            // Capitalize first letter of each word
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleaned.ToLower());
        }
    }
}
