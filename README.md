# SVS-CCPoseLoader

Additional pose loader for SamabakeScramble Character creation

# Prerequests

 * BepInEx v6
 
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
