using System;
using System.IO;
using System.Reflection;
using MelonLoader;

namespace AsftuAccessibilityMod
{
    /// <summary>
    /// Extracts embedded audio resources from the DLL to a temporary folder
    /// </summary>
    public static class AudioResourceExtractor
    {
        private static string _audioFolder = null;

        /// <summary>
        /// Gets the path to the folder containing extracted audio files
        /// </summary>
        public static string AudioFolder
        {
            get
            {
                if (_audioFolder == null)
                {
                    ExtractAudioResources();
                }
                return _audioFolder;
            }
        }

        /// <summary>
        /// Extracts all embedded .wav files from the DLL to a temporary folder
        /// </summary>
        private static void ExtractAudioResources()
        {
            try
            {
                // Create a folder for audio files in the mod's directory
                string modFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                _audioFolder = Path.Combine(modFolder, "AccessibilityAudio");

                // Create the folder if it doesn't exist
                if (!Directory.Exists(_audioFolder))
                {
                    Directory.CreateDirectory(_audioFolder);
                }

                // Get all embedded resources
                Assembly assembly = Assembly.GetExecutingAssembly();
                string[] resourceNames = assembly.GetManifestResourceNames();

                int extractedCount = 0;
                foreach (string resourceName in resourceNames)
                {
                    // Only process .wav files from AudioFiles folder
                    if (resourceName.Contains(".AudioFiles.") && resourceName.EndsWith(".wav"))
                    {
                        // Extract the filename (e.g., "button_up.wav" from "AsftuAccessibilityMod.AudioFiles.button_up.wav")
                        string fileName = resourceName.Substring(resourceName.LastIndexOf('.', resourceName.Length - 5) + 1);
                        string outputPath = Path.Combine(_audioFolder, fileName);

                        // Only extract if file doesn't exist or is older than the DLL
                        bool shouldExtract = !File.Exists(outputPath);
                        if (!shouldExtract)
                        {
                            DateTime dllTime = File.GetLastWriteTime(assembly.Location);
                            DateTime fileTime = File.GetLastWriteTime(outputPath);
                            shouldExtract = dllTime > fileTime;
                        }

                        if (shouldExtract)
                        {
                            // Extract the resource
                            using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
                            using (FileStream fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                            {
                                resourceStream.CopyTo(fileStream);
                            }
                            extractedCount++;
                            MelonLogger.Msg($"[AudioResourceExtractor] Extracted: {fileName}");
                        }
                    }
                }

                if (extractedCount > 0)
                {
                    MelonLogger.Msg($"[AudioResourceExtractor] Extracted {extractedCount} audio files to: {_audioFolder}");
                }
                else
                {
                    MelonLogger.Msg($"[AudioResourceExtractor] Using existing audio files in: {_audioFolder}");
                }

                // List all files that are now available
                int totalFiles = Directory.GetFiles(_audioFolder, "*.wav").Length;
                MelonLogger.Msg($"[AudioResourceExtractor] Total audio files available: {totalFiles}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[AudioResourceExtractor] Failed to extract audio resources: {ex.Message}");
                MelonLogger.Error($"[AudioResourceExtractor] Stack trace: {ex.StackTrace}");

                // Fallback to Mods/AccessibilityAudio if extraction fails
                _audioFolder = Path.Combine(
                    Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)),
                    "AccessibilityAudio"
                );
                MelonLogger.Warning($"[AudioResourceExtractor] Using fallback folder: {_audioFolder}");
            }
        }
    }
}
