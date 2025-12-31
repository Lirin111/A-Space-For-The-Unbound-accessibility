using System;
using System.Runtime.InteropServices;
using MelonLoader;

namespace AsftuAccessibilityMod
{
    /// <summary>
    /// Wrapper for the TOLK library to provide text-to-speech functionality
    /// </summary>
    public static class TolkWrapper
    {
        private const string DllName = "Tolk.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_Load();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_Unload();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Tolk_IsLoaded();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern IntPtr Tolk_DetectScreenReader();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Tolk_HasSpeech();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Tolk_HasBraille();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Tolk_Output([MarshalAs(UnmanagedType.LPWStr)] string text, [MarshalAs(UnmanagedType.Bool)] bool interrupt);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Tolk_Speak([MarshalAs(UnmanagedType.LPWStr)] string text, [MarshalAs(UnmanagedType.Bool)] bool interrupt);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Tolk_Silence();

        private static bool _isInitialized = false;

        public static bool Initialize()
        {
            try
            {
                Tolk_Load();
                _isInitialized = Tolk_IsLoaded();
                
                if (_isInitialized)
                {
                    string screenReader = DetectScreenReader();
                    MelonLogger.Msg($"TOLK initialized successfully. Screen reader detected: {screenReader}");
                }
                else
                {
                    MelonLogger.Warning("TOLK loaded but no screen reader detected");
                }
                
                return _isInitialized;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to initialize TOLK: {ex.Message}");
                return false;
            }
        }

        public static void Shutdown()
        {
            if (_isInitialized)
            {
                try
                {
                    Tolk_Unload();
                    _isInitialized = false;
                    MelonLogger.Msg("TOLK shutdown successfully");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Failed to shutdown TOLK: {ex.Message}");
                }
            }
        }

        public static bool IsInitialized => _isInitialized;

        public static string DetectScreenReader()
        {
            if (!_isInitialized) return "None";
            
            try
            {
                IntPtr ptr = Tolk_DetectScreenReader();
                return ptr != IntPtr.Zero ? Marshal.PtrToStringUni(ptr) : "Unknown";
            }
            catch
            {
                return "Error";
            }
        }

        public static bool HasSpeech()
        {
            return _isInitialized && Tolk_HasSpeech();
        }

        public static bool HasBraille()
        {
            return _isInitialized && Tolk_HasBraille();
        }

        public static bool Speak(string text, bool interrupt = false)
        {
            if (!_isInitialized || string.IsNullOrWhiteSpace(text))
                return false;

            try
            {
                return Tolk_Speak(text, interrupt);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to speak text: {ex.Message}");
                return false;
            }
        }

        public static bool Output(string text, bool interrupt = false)
        {
            if (!_isInitialized || string.IsNullOrWhiteSpace(text))
                return false;

            try
            {
                return Tolk_Output(text, interrupt);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to output text: {ex.Message}");
                return false;
            }
        }

        public static bool Silence()
        {
            if (!_isInitialized)
                return false;

            try
            {
                return Tolk_Silence();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to silence speech: {ex.Message}");
                return false;
            }
        }
    }
}
