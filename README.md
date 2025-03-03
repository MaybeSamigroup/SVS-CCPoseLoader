# SVS-CCPoseLoader

Additional pose loader for SamabakeScramble Character creation

# Prerequisites

 * [BepInEx](https://github.com/BepInEx/BepInEx)
   * v6.0.0 be 725 or later
 * [ByteFiddler](https://github.com/BepInEx/BepInEx)
   * v1.0 or later and suitable configuration
 * [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager)
   * v18.3 or later

Confirmed working under SVS 1.1.4 + [SVS-HF Patch](https://github.com/ManlyMarco/SVS-HF_Patch) 1.6 environment.

# Installation

If you have 1.0.0 version, remove configuretion file at:

 * (game root)/BepInEx/config/SamabakeScramble.CCPoseLoader.cfg

and pose definition jsons at:

 * (game root)/BepInEx/config/CCPoseLoader

Extract the release to game root.

# How to use

Start character creation then you'll see addtional poses.
 
Poses to load are defined in .json format, you can made your own in relative path from:

```(game root)/BepInEx/config/CCPoseLoader```.

There are predefined (and not well tested) list packaged in release.

# Configuration

 * MalePoseFile

     Pose definition file for male creation mode.

 * FemalePoseFile

     Pose definition file for female creation mode.

# Pose definition format

 * Depth 1 Key
   Asset bundle path relative from ```(game root)```/abdata
   * Depth 2 Key
     AnimatorController asset name to load from parent
     * Values
       AnimationClip Names to play in parent 
