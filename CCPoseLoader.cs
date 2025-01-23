using HarmonyLib;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using CharacterCreation;
using CharacterCreation.ListInfo;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using PoseBuff = Il2CppSystem.Collections.Generic.List<CharacterCreation.ListInfo.PoseInfoData>;
using PoseDefs = Il2CppSystem.Collections.Generic.IReadOnlyList<CharacterCreation.ListInfo.PoseInfoData>;
using PoseSet = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string[]>>;
namespace CCPoseLoader
{
    public static class Hooks
    {
        // hook to replace pose list
        public static event Action<string> OnPoseUpdate = (input) => {};
        
        internal static void RaisePoseUpdate(string poseId) => OnPoseUpdate(poseId);
    }
    internal static class Extension
    {
        internal static List<string> PoseIds = new ();
        // hook to listen pose change
        internal static event Func<PoseDefs, PoseDefs> OnPosesLoad;
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HumanCustom), nameof(HumanCustom.LoadList))]
        internal static void HumanCustomLoadListPostfix(HumanCustom __instance) =>
            __instance.PoseList = OnPosesLoad(__instance.PoseList);
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HumanCustom), nameof(HumanCustom.LoadAnimation), typeof(string), typeof(string))]
        internal static void HumanCustomLoadAnimationPostfix(HumanCustom __instance)
        {
            // tweaks for not character creation made animation
            var binder = __instance?.Human?.gameObject?.GetComponent<MotionIKDataBinder>();
            if (binder != null && binder.Data != null && binder.MotionIK.Data == null)
            {
                binder.MotionIK.SetData(binder.Data);
            }
            // raise event
            Hooks.RaisePoseUpdate(PoseIds[HumanCustom.Instance?.NowPose ?? 0]);
        }
        internal static string QualifiedName(this PoseInfoData info) =>
            $"{info.Bundle}:{info.Asset}:{info.State}";
        internal static string ConfigPath(this string value) =>
            Path.Combine(Paths.ConfigPath, Plugin.Name, value);
    }
    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BasePlugin
    {
        public const string Process = "SamabakeScramble";
        public const string Name = "CCPoseLoader";
        public const string Guid = $"{Process}.{Name}";
        public const string Version = "2.0.0";
        private Harmony[] Patches;
        private ConfigEntry<string> MalePoses { get; set; }
        private ConfigEntry<string> FemalePoses { get; set; }
        public override void Load()
        {
            MalePoses = Config.Bind(new ConfigDefinition("General", "MalePoseFile"), "male/poses.json");
            FemalePoses = Config.Bind(new ConfigDefinition("General", "FemalePoseFile"), "female/poses.json");
            Patches = new[]
            {
                Harmony.CreateAndPatchAll(typeof(Extension), $"{Name}.Hooks")
            };
            Extension.OnPosesLoad += (input) =>
            {
                Extension.PoseIds.Clear();
                var output = new PoseBuff();
                var index = 0;
                while (input.TryGet(index++, out PoseInfoData info))
                {
                    output.Add(info);
                    Extension.PoseIds.Add(info.QualifiedName());
                }
                var poses = index == 10 ? MalePoses : FemalePoses;
                JsonSerializer.Deserialize<PoseSet>(File.OpenRead(poses.Value.ConfigPath()))
                    .SelectMany(bundle2assets => bundle2assets.Value
                    .SelectMany(asset2states => asset2states.Value
                    .Select(state => new PoseInfoData() { Bundle = bundle2assets.Key, Asset = asset2states.Key, State = state })))
                    .Do(pose => { output.Add(pose); Extension.PoseIds.Add(pose.QualifiedName()); });
                return new PoseDefs(output.Pointer);
            };
        }
        public override bool Unload()
        {
            Patches.Do(patch => patch.UnpatchSelf());
            return base.Unload();
        }
    }
}