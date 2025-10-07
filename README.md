# SVS-CCPoseLoader

Additional pose loader for Aicomi and SamabakeScramble Character creation

## Prerequisites (Aicomi)

- [AC-HF_Patch](https://github.com/ManlyMarco/AC-HF_Patch)
  - Message Center
  - BepInEx.ConfigurationManager
  - SVS_BepisPlugins
- [CoastalSmell](https://github.com/MaybeSamigroup/SVS-Fishbone)
  - 1.1.9 or later

Confirmed working under Aicomi 1.0.1

## Prerequisites (SamabakeScramble)

- [SVS-HF_Patch](https://github.com/ManlyMarco/SVS-HF_Patch)
  - Message Center
  - BepInEx.ConfigurationManager
  - SVS_BepisPlugins
- [CoastalSmell](https://github.com/MaybeSamigroup/SVS-Fishbone)
  - 1.1.9 or later

Confirmed working under SamabakeScramble 1.1.6

## Installation

Extract the [latest release](https://github.com/MaybeSamigroup/SVS-CCPoseLoader/releases/latest) to your game install directory.

## Migration from older release

remove directory at:

- (game root)/BepInEx/config/CCPoseLoader

remove files at:

- (game root)/BepInEx/config/SamabakeScramble.CCPoseLoader.cfg
- (game root)/BepInEx/plugin/CCPoseLoader.dll

## How to use

Start character creation then you'll see addtional poses.

## Configuration

- MalePoseFile

  Pose definition file for male creation mode.

- FemalePoseFile

  Pose definition file for female creation mode.

Poses to load are defined in .json format, you can made your own in relative path from:

```(game root)/UserData/plugins/CCPoseLoader```.

There are predefined (and not well tested) list packaged in release.

## Pose definition format

- Depth 1 Key
  Asset bundle path relative from ```(game root)```/abdata
  - Depth 2 Key
    AnimatorController asset name to load from parent
    - Values
      AnimationClip Names to play in parent

Provided AnimatorController extracted files are partially handwork, so there can be typo or other human errors.
