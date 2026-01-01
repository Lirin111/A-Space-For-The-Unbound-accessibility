# A Space for the Unbound - Accessibility Mod

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Version](https://img.shields.io/badge/version-1.2.0-blue.svg)](https://github.com/Lirin111/A-Space-For-The-Unbound-accessibility/releases)

This mod makes "A Space for the Unbound" fully accessible for blind and visually impaired players through comprehensive audio cues and screen reader support.

## 🎮 Features

### Screen Reader Support
- **Full Compatibility** with NVDA, JAWS, and SAPI screen readers
- **Menu Navigation** - All menus announced as you navigate
- **Dialogue Reading** - Story text and conversations read aloud
- **Interaction Text** - Object descriptions and prompts spoken
- **Game State Notifications** - Important events announced

### Spatial Audio Cues System
All audio cues use 3D positioning - sounds play from the left or right speaker based on object locations relative to your character.

#### Interactable Objects
- **Doors & Stairs** - Directional audio when nearby
- **Characters & NPCs** - Distinct sounds for people
- **Items** - Audio cues to locate collectibles

#### Stealth Sections
- **Proximity Alerts** - Distance-based beeping (near/medium/far)
- **Guard Tracking** - Directional audio shows guard positions
- **State Announcements** - Know when guards are searching, alert, or calm
- **Movement Feedback** - Confirms crouch/walk state
- **Zone Indicators** - Safe area and danger zone sounds

#### Accessible Minigames
- **Fighting Minigame**
  - Audio cues for button sequences
  - Diagonal input support (e.g., up-left, down-right)
  - Timing feedback for attacks and finishing moves
  - Extended timeout (20 seconds) for finishing phase

- **Timing Bar Minigame**
  - Continuous beeping that increases in pitch as you approach the target
  - Success/failure audio feedback

- **Throwing Minigame**
  - Beeps indicate throwing angle
  - Audio feedback for successful hits

- **Falling Debris Minigame**
  - Directional looping sounds (left/right/center) for each falling object
  - Debris speed reduced to 40% for more reaction time
  - Maximum 2 obstacles on screen at once
  - Dynamic audio that updates as you move

- **Comic Book Reading**
  - Automatic panel navigation
  - Audio feedback when turning pages

#### Puzzle Assistance
- **Valve Puzzles** - Current position and target announcements
- **Interactive Elements** - Descriptions of puzzle components

## 📥 Installation

### Requirements
- **Game**: A Space for the Unbound (Steam version)
- **Mod Loader**: [MelonLoader](https://melonwiki.xyz/)
- **Screen Reader**: NVDA, JAWS, or other compatible screen readers

### Installation Steps

1. **Install MelonLoader**
   - Download MelonLoader from [melonwiki.xyz](https://melonwiki.xyz/)
   - Run the installer and select "A Space for the Unbound"

2. **Download the Mod**
   - Go to [Releases](https://github.com/Lirin111/A-Space-For-The-Unbound-accessibility/releases)
   - Download the latest `AsftuAccessibilityMod-Release.zip`

3. **Install the Mod**
   - Extract all files from the ZIP
   - Copy all 4 DLL files to: `[Game Directory]\Mods\`
     - `AsftuAccessibilityMod.dll`
     - `Tolk.dll`
     - `nvdaControllerClient64.dll`
     - `nvdaControllerClient32.dll`

4. **Launch the Game**
   - Start the game normally
   - Audio files will automatically extract on first run
   - You should hear "Accessibility Mod Loaded" announcement

**Default Game Directory**: `C:\Program Files (x86)\Steam\steamapps\common\A Space for the Unbound\`

## 🎯 How to Play

### General Navigation
- Use **directional audio** to locate interactable objects
- Listen for **distance-based volume** - closer objects are louder
- **Screen reader** announces all menus and dialogue automatically

### Stealth Sections
- Listen for **proximity beeps** - faster beeps mean guards are closer
- **Guard sounds** come from their direction (left/right/center)
- Stay in **safe zones** (you'll hear a calm ambient sound)
- Avoid **danger zones** (urgent warning sound)
- Press crouch to hear "now crouching" confirmation

### Minigames
- **Fighting**: Listen for button sequence cues, press buttons in order
- **Timing Bar**: Press when the beeping reaches highest pitch
- **Throwing**: Adjust angle until you hear the target tone, then throw
- **Falling Debris**: Use directional audio to dodge left/right, only 2 objects fall at a time

## 🔧 Technical Details

### Architecture
- **MelonLoader Mod** - Uses Harmony for runtime patching
- **FMOD Integration** - Leverages game's audio engine for spatial sound
- **Tolk Library** - Universal screen reader interface

### Audio System
- **Spatial Positioning** - Pan values from -1 (left) to +1 (right)
- **Distance-based Volume** - Dynamic volume calculation based on proximity
- **Looping Sounds** - Continuous audio for persistent obstacles
- **Dynamic Updates** - Audio repositions as you move (100ms update rate)

## 🐛 Troubleshooting

### Audio Cues Not Playing
- Check `[Game Directory]\MelonLoader\Latest.log` for errors
- Verify all 4 DLL files are in the Mods folder
- Ensure `AccessibilityAudio` folder was created next to the DLL

### Screen Reader Not Working
- Make sure your screen reader is running BEFORE launching the game
- Try restarting your screen reader software
- Check MelonLoader log for "TOLK initialized" message
- Supported: NVDA, JAWS, System Access, Window-Eyes, SuperNova, ZoomText

### Mod Not Loading
- Verify MelonLoader is installed correctly
- Check for error messages in MelonLoader console window
- Make sure you're using the Steam version of the game

### Performance Issues
- The mod is optimized and should not impact performance
- If you experience issues, check MelonLoader log for exceptions

## 🛠️ For Developers

### Building from Source

1. **Clone the repository**
   ```bash
   git clone https://github.com/Lirin111/A-Space-For-The-Unbound-accessibility.git
   cd A-Space-For-The-Unbound-accessibility
   ```

2. **Requirements**
   - .NET SDK 7.0 or higher
   - Visual Studio 2022 or Rider
   - Game installed with MelonLoader

3. **Setup**
   - Update DLL reference paths in `AccessibilityMod/AsftuAccessibilityMod.csproj`
   - Place required game DLLs in reference locations
   - Copy audio files to `AccessibilityMod/AudioFiles/`

4. **Build**
   ```bash
   dotnet build AccessibilityMod/AsftuAccessibilityMod.csproj --configuration Release
   ```

5. **Output**
   - Built DLL: `AccessibilityMod/bin/Release/net472/AsftuAccessibilityMod.dll`
   - Copy to `[Game Directory]\Mods\` for testing

### Project Structure
```
A-Space-For-The-Unbound-accessibility/
├── AccessibilityMod/               # Main mod source code
│   ├── AudioFiles/                 # Audio resources (*.wav)
│   ├── AudioResourceExtractor.cs   # Extracts audio resources
│   ├── StealthAudioManager.cs      # Stealth section audio
│   ├── MinigameAudioManager.cs     # Minigame audio
│   ├── InteractableAudioManager.cs # Object interaction audio
│   ├── MinigamePatches.cs          # Minigame accessibility patches
│   ├── StealthPatches.cs           # Stealth section patches
│   ├── ComicBookPatches.cs         # Comic reading patches
│   ├── ValvePuzzlePatches.cs       # Puzzle assistance
│   ├── InteractionPatches.cs       # Object interaction patches
│   └── AsftuAccessibilityMod.csproj
├── Tolk.dll                        # Screen reader library
├── nvdaControllerClient64.dll      # NVDA support (64-bit)
├── nvdaControllerClient32.dll      # NVDA support (32-bit)
├── README.md                       # This file
└── LICENSE                         # MIT License
```

### Key Components

- **Harmony Patches** - Runtime method interception for game modification
- **FMOD Audio** - Spatial audio using game's built-in engine
- **Tolk Integration** - Cross-platform screen reader support

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Credits

**Developer**: lirin's workspace

**Libraries Used**:
- [MelonLoader](https://melonwiki.xyz/) - Mod loading framework
- [Harmony](https://github.com/pardeike/Harmony) - Runtime patching
- [Tolk](https://github.com/dkager/tolk) - Screen reader interface

**Special Thanks**:
- The blind gaming community for feedback and testing
- MelonLoader developers for the modding framework
- Mojiken Studio for creating "A Space for the Unbound"

## 📧 Support

- **Issues**: [GitHub Issues](https://github.com/Lirin111/A-Space-For-The-Unbound-accessibility/issues)
- **Discussions**: [GitHub Discussions](https://github.com/Lirin111/A-Space-For-The-Unbound-accessibility/discussions)

## 🗺️ Roadmap

Future improvements being considered:
- Additional minigame accessibility enhancements
- More detailed environment audio descriptions
- Customizable audio cue volumes
- Key rebinding support
- Multi-language screen reader support

## 📜 Changelog

### v1.2.0 (2025-01-01)
- Updated user documentation
- Refined installation guide

### v1.1.0 (2025-01-01)
- Updated documentation
- Improved stability

### v1.0.0 (2025-01-01)
- Initial release
- Full screen reader support
- Comprehensive audio cue system
- Accessible stealth sections
- Accessible minigames (fighting, timing bar, throwing, falling debris, comic books)
- Puzzle assistance
