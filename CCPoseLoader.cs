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
    public static class FunctionalExtension {
        public static T With<T>(this T input, Action sideEffect) {
            sideEffect();
            return input;
        }
        public static T With<T>(this T input, Action<T> sideEffect) => input.With(() => sideEffect(input));
    }
    public static class Event
    {
        // hook to be notified pose name
        public static event Action<string> OnPoseUpdate = (input) => {};
        public static List<string> PoseIds = new ();
        public static string QualifiedName(this PoseInfoData info) =>
            $"{info.Bundle}:{info.Asset}:{info.State}";
        internal static void NotifyPoseUpdate() => OnPoseUpdate(PoseIds[HumanCustom.Instance?.NowPose ?? 0]);
    }
    internal static class Hooks
    {
        internal static ConfigEntry<string> MalePoses { get; set; }
        internal static ConfigEntry<string> FemalePoses { get; set; } 
        static string ConfigPath(this string value) => Path.Combine(Paths.ConfigPath, Plugin.Name, value);
        static string CurrentConfig(HumanCustom custom) => ConfigPath(custom.IsMale() ? MalePoses.Value : FemalePoses.Value);
        static IEnumerable<PoseInfoData> CurrentPoses(HumanCustom custom) =>
            JsonSerializer.Deserialize<PoseSet>(File.OpenRead(CurrentConfig(custom)))
                .SelectMany(bundle2assets => bundle2assets.Value
                .SelectMany(asset2states => asset2states.Value
                .Select(state => new PoseInfoData() {
                    Bundle = bundle2assets.Key,
                    Asset = asset2states.Key,
                    State = state
                })));
        static Action<PoseInfoData> EntryPoseId = info => Event.PoseIds.Add(info.QualifiedName());

        static Func<int, Tuple<bool, PoseInfoData>> TryGet(this PoseDefs src) =>
            index => new (src.TryGet(index, out PoseInfoData data), data);

        static IEnumerable<PoseInfoData> ToList(PoseDefs src) =>
            Enumerable.Range(0, int.MaxValue).Select(TryGet(src))
                .TakeWhile(tuple => tuple.Item1).Select(tuple => tuple.Item2);

        static PoseBuff EntryPoses(this PoseBuff dst, IEnumerable<PoseInfoData> src) =>
            dst.With(() => src.Do(EntryPoseId + dst.Add));

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HumanCustom), nameof(HumanCustom.LoadList))]
        static void HumanCustomLoadListPostfix(HumanCustom __instance) =>
            __instance.PoseList = new PoseDefs(new PoseBuff().With(Event.PoseIds.Clear)
                .EntryPoses(ToList(__instance.PoseList)).EntryPoses(CurrentPoses(__instance)).Pointer); 

        // tweaks to not for character creation made poses works fine.
        static bool Fix(this MotionIKDataBinder binder) =>
            binder != null && binder.Data != null && binder.MotionIK.Data == null
                 && true.With(() => binder.MotionIK.SetData(binder.Data));

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HumanCustom), nameof(HumanCustom.LoadAnimation), typeof(string), typeof(string))]
        static void HumanCustomLoadAnimationPostfix(HumanCustom __instance) =>
            __instance.With(Event.NotifyPoseUpdate).Human?.gameObject?.GetComponent<MotionIKDataBinder>().Fix();
    }
    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BasePlugin
    {
        public const string Process = "SamabakeScramble";
        public const string Name = "CCPoseLoader";
        public const string Guid = $"{Process}.{Name}";
        public const string Version = "2.0.1";
        private Harmony Patch;
        public override void Load() =>
            Patch = Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks")
                .With(ConfigMalePoses).With(ConfigFemalePoses);
        public void ConfigMalePoses() =>
            Hooks.MalePoses = Config.Bind(new ConfigDefinition("General", "MalePoseFile"), "male/poses.json");
        public void ConfigFemalePoses() =>
            Hooks.FemalePoses = Config.Bind(new ConfigDefinition("General", "FemalePoseFile"), "female/poses.json");
        public override bool Unload() => true.With(Patch.UnpatchSelf) && base.Unload();
    }
}