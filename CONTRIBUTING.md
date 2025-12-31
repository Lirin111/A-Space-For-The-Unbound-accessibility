# Contributing to A Space for the Unbound - Accessibility Mod

Thank you for considering contributing to this accessibility mod! This project aims to make "A Space for the Unbound" playable for blind and visually impaired players.

## How Can I Contribute?

### Reporting Bugs

If you find a bug, please create an issue with:

- **Clear title** - Describe the problem briefly
- **Steps to reproduce** - What did you do to encounter the bug?
- **Expected behavior** - What should have happened?
- **Actual behavior** - What actually happened?
- **MelonLoader log** - Attach relevant portions from `MelonLoader/Latest.log`
- **System info** - OS version, screen reader used, game version

### Suggesting Enhancements

We welcome suggestions for new accessibility features! Please include:

- **Use case** - What accessibility need does this address?
- **Proposed solution** - How should it work?
- **Alternatives considered** - Other ways to solve the problem
- **Impact** - How many players would benefit?

### Providing Feedback

As a blind or visually impaired player, your feedback is invaluable:

- What works well?
- What's confusing or difficult?
- What features are missing?
- How can the audio cues be improved?

### Contributing Code

1. **Fork the repository**
2. **Create a feature branch** - `git checkout -b feature/your-feature-name`
3. **Make your changes** - Follow the coding style below
4. **Test thoroughly** - Make sure it works in-game
5. **Commit your changes** - Use clear commit messages
6. **Push to your fork** - `git push origin feature/your-feature-name`
7. **Create a Pull Request** - Describe your changes

## Coding Guidelines

### C# Style
- Use **4 spaces** for indentation (no tabs)
- **Descriptive names** for variables and methods
- **XML comments** for public methods
- **Try-catch blocks** around all patched methods to prevent crashes
- **MelonLogger** for all logging, with appropriate prefixes

### Harmony Patches
- Always include **try-catch** in patch methods
- Use **clear patch names** - `[MethodName]_Prefix/Postfix`
- **Log important events** for debugging
- **Return true** on errors in Prefix patches (allow original to run)

### Audio Implementation
- **Test with headphones** - Verify spatial positioning works
- **Volume levels** - Keep consistent with game audio
- **File formats** - Use WAV format for compatibility
- **File naming** - Use descriptive lowercase names with underscores

### Example Patch
```csharp
[HarmonyPatch(typeof(GameClass), "MethodName")]
[HarmonyPrefix]
public static bool MethodName_Prefix(GameClass __instance)
{
    if (!AccessibilityMod.IsEnabled)
        return true;

    try
    {
        // Your accessibility code here
        MelonLogger.Msg("[Feature] Doing something accessible");
        return true;
    }
    catch (Exception ex)
    {
        MelonLogger.Error($"Error in MethodName_Prefix: {ex.Message}");
        return true; // Allow original method to run
    }
}
```

## Testing

Before submitting a PR:

1. **Build succeeds** - No compilation errors
2. **Game launches** - Mod loads without errors
3. **Feature works** - Test in actual gameplay
4. **No regressions** - Existing features still work
5. **Screen reader tested** - Verify announcements work
6. **Audio tested** - Check spatial positioning

## Audio File Guidelines

When adding new audio cues:

1. **Format**: WAV (uncompressed PCM)
2. **Sample Rate**: 44100 Hz
3. **Bit Depth**: 16-bit
4. **Channels**: Mono (spatial positioning handled by code)
5. **Duration**: Keep short (0.5-2 seconds for cues)
6. **Volume**: Normalize to -3dB to prevent clipping

## Documentation

When adding features:

- Update **README.md** with new features
- Add comments in code for complex logic
- Update **CHANGELOG** section in README
- Include examples in documentation

## Questions?

Feel free to:
- Open an issue for discussion
- Start a thread in GitHub Discussions
- Ask questions in pull request comments

Thank you for helping make games more accessible!
