using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using MelonLoader;
using UnityEngine;

namespace AsftuAccessibilityMod
{
    /// <summary>
    /// Central manager for all text-to-speech functionality
    /// Handles speech queuing, text cleanup, and rate limiting
    /// </summary>
    public static class TTSManager
    {
        // Windows Beep API for tone generation
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Beep(uint frequency, uint duration);

        private static Queue<TTSMessage> _messageQueue = new Queue<TTSMessage>();
        private static TTSMessage _currentMessage;
        private static float _lastSpeechTime = 0f;
        private static float _minSpeechInterval = 0.1f; // Minimum time between speech events
        private static string _lastSpokenText = "";
        private static float _repeatThreshold = 0.5f; // Don't repeat same text within this time

        private class TTSMessage
        {
            public string Text { get; set; }
            public bool Interrupt { get; set; }
            public float Timestamp { get; set; }
            public int Priority { get; set; } // Higher = more important
        }

        public static void Initialize()
        {
            _messageQueue.Clear();
            _currentMessage = null;
            _lastSpeechTime = 0f;
            _lastSpokenText = "";
            MelonLogger.Msg("TTSManager initialized");
        }

        public static void Update()
        {
            float currentTime = Time.unscaledTime;

            // Process queue if enough time has passed
            if (_messageQueue.Count > 0 && (currentTime - _lastSpeechTime) >= _minSpeechInterval)
            {
                ProcessNextMessage();
            }
        }

        /// <summary>
        /// Speak text immediately, optionally interrupting current speech
        /// </summary>
        public static void Speak(string text, bool interrupt = false, int priority = 0)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            string cleanedText = CleanText(text);
            if (string.IsNullOrWhiteSpace(cleanedText))
                return;

            float currentTime = Time.unscaledTime;

            // Check if we're repeating the same text too quickly
            if (cleanedText == _lastSpokenText && (currentTime - _lastSpeechTime) < _repeatThreshold)
            {
                return;
            }

            var message = new TTSMessage
            {
                Text = cleanedText,
                Interrupt = interrupt,
                Timestamp = currentTime,
                Priority = priority
            };

            if (interrupt)
            {
                // Clear queue and speak immediately
                _messageQueue.Clear();
                TolkWrapper.Silence();
                SpeakMessage(message);
            }
            else
            {
                // Add to queue with priority
                EnqueueMessage(message);
            }
        }

        /// <summary>
        /// Announce UI element (button, menu item, etc.)
        /// </summary>
        public static void AnnounceUI(string text, bool interrupt = false)
        {
            Speak(text, interrupt, priority: 5);
        }

        /// <summary>
        /// Announce dialogue text
        /// </summary>
        public static void AnnounceDialogue(string characterName, string dialogueText, bool interrupt = true)
        {
            string fullText = string.IsNullOrEmpty(characterName) 
                ? dialogueText 
                : $"{characterName} says: {dialogueText}";
            
            Speak(fullText, interrupt, priority: 10);
        }

        /// <summary>
        /// Announce interactable object
        /// </summary>
        public static void AnnounceInteractable(string objectName, string actionType = "Interact")
        {
            string text;
            if (string.IsNullOrWhiteSpace(objectName))
            {
                // If no object name, just announce the action
                text = actionType;
            }
            else
            {
                // Check if action already has "to interact with" or "to talk to" at the end
                if (actionType.EndsWith("to interact with") || actionType.EndsWith("to talk to"))
                {
                    text = $"{actionType} {objectName}";
                }
                // If action already contains "with", don't add it again
                else if (actionType.Contains("with") || actionType.Contains("to"))
                {
                    text = $"{actionType} {objectName}";
                }
                else
                {
                    text = $"{actionType} with {objectName}";
                }
            }
            Speak(text, interrupt: false, priority: 3);
        }

        /// <summary>
        /// Announce notification/tutorial
        /// </summary>
        public static void AnnounceNotification(string text, bool interrupt = false)
        {
            Speak($"Notification: {text}", interrupt, priority: 7);
        }

        /// <summary>
        /// Silence current speech
        /// </summary>
        public static void Silence()
        {
            _messageQueue.Clear();
            TolkWrapper.Silence();
        }

        private static void EnqueueMessage(TTSMessage message)
        {
            // Insert by priority
            if (_messageQueue.Count == 0)
            {
                _messageQueue.Enqueue(message);
                return;
            }

            var tempQueue = new Queue<TTSMessage>();
            bool inserted = false;

            while (_messageQueue.Count > 0)
            {
                var existing = _messageQueue.Dequeue();
                if (!inserted && message.Priority > existing.Priority)
                {
                    tempQueue.Enqueue(message);
                    inserted = true;
                }
                tempQueue.Enqueue(existing);
            }

            if (!inserted)
            {
                tempQueue.Enqueue(message);
            }

            _messageQueue = tempQueue;
        }

        private static void ProcessNextMessage()
        {
            if (_messageQueue.Count == 0)
                return;

            var message = _messageQueue.Dequeue();
            SpeakMessage(message);
        }

        private static void SpeakMessage(TTSMessage message)
        {
            if (TolkWrapper.Speak(message.Text, message.Interrupt))
            {
                _lastSpokenText = message.Text;
                _lastSpeechTime = message.Timestamp;
                _currentMessage = message;
                
                // Log for debugging
                MelonLogger.Msg($"[TTS] {message.Text}");
            }
        }

        /// <summary>
        /// Clean text for speech - remove rich text tags, fix formatting, etc.
        /// </summary>
        private static string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            StringBuilder sb = new StringBuilder(text);

            // Remove Unity rich text tags like <color>, <size>, etc.
            sb = new StringBuilder(Regex.Replace(sb.ToString(), @"<[^>]+>", ""));
            
            // Remove TextMeshPro curly brace tags like {b}, {/b}, {i}, {/i}
            sb = new StringBuilder(Regex.Replace(sb.ToString(), @"\{[^}]+\}", ""));
            
            // Remove extra whitespace
            sb = new StringBuilder(Regex.Replace(sb.ToString(), @"\s+", " "));
            
            // Replace common symbols
            string result = sb.ToString()
                .Replace("...", ", ")
                .Replace("…", ", ")
                .Replace("—", ", ")
                .Replace("–", ", ")
                .Trim();

            return result;
        }

        /// <summary>
        /// Set minimum interval between speech events
        /// </summary>
        public static void SetMinSpeechInterval(float interval)
        {
            _minSpeechInterval = Mathf.Max(0.05f, interval);
        }

        /// <summary>
        /// Set repeat threshold - don't repeat same text within this time
        /// </summary>
        public static void SetRepeatThreshold(float threshold)
        {
            _repeatThreshold = Mathf.Max(0.1f, threshold);
        }

        /// <summary>
        /// Play a tone at the specified frequency and duration (for audio cues)
        /// </summary>
        /// <param name="frequency">Frequency in Hz (37-32767)</param>
        /// <param name="duration">Duration in milliseconds</param>
        public static void PlayTone(int frequency, int duration)
        {
            try
            {
                // Clamp values to valid ranges
                uint freq = (uint)Mathf.Clamp(frequency, 37, 32767);
                uint dur = (uint)Mathf.Clamp(duration, 10, 5000);
                
                // Play tone asynchronously to avoid blocking
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        Beep(freq, dur);
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"Error playing tone: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in PlayTone: {ex.Message}");
            }
        }
    }
}
