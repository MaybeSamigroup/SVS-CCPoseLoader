using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using HarmonyLib;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using CharacterCreation;
using CharacterCreation.ListInfo;
#if Aicomi
using MotionIK = ILLGAMES.Rigging.MotionIK;
#else
using MotionIK = ILLGames.Rigging.MotionIK;
#endif
using CoastalSmell;
using PoseBuff = Il2CppSystem.Collections.Generic.List<CharacterCreation.ListInfo.PoseInfoData>;
using PoseDefs = Il2CppSystem.Collections.Generic.IReadOnlyList<CharacterCreation.ListInfo.PoseInfoData>;
using PoseSet = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string[]>>;

namespace CCPoseLoader
{
    public static class Event
    {
        public static List<string> PoseIds = new ();
        public static string QualifiedName(this PoseInfoData info) =>
            $"{info.Bundle}:{info.Asset}:{info.State}";
        internal static ConfigEntry<string> MalePoses { get; set; }
        internal static ConfigEntry<string> FemalePoses { get; set; } 
        static string ConfigPath(this string value) => Path.Combine(Paths.GameRootPath, "UserData", "plugins", Plugin.Name, value);
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
        static Action<PoseInfoData> EntryPoseId = info => PoseIds.Add(info.QualifiedName());
        static PoseBuff EntryPoses(this PoseBuff dst, IEnumerable<PoseInfoData> src) =>
            dst.With(() => src.ForEach(EntryPoseId + dst.Add));
        static bool ReadyPoses() =>
            HumanCustom.Instance.PoseList != null; 
        static void ApplyPoses() =>
            HumanCustom.Instance.PoseList = new PoseDefs(new PoseBuff()
                .EntryPoses(HumanCustom.Instance.PoseList.Yield())
                .EntryPoses(CurrentPoses(HumanCustom.Instance)).Pointer);
        internal static void Initialize() =>
            Util<HumanCustom>.Hook(OnEnterCustom, OnLeaveCustom);
        static void FixBinder(MotionIKDataBinder binder) =>
            (binder != null && binder.Data != null && binder.MotionIK.Data == null)
                .Maybe(() => binder.MotionIK.SetData(binder.Data));
        static void FixBinder() =>
            FixBinder(HumanCustom.Instance.Human?.gameObject?.GetComponent<MotionIKDataBinder>());
        static void OnEnterCustom()
        {
            Hooks.MotionIKLoadData += FixBinder;
            Util.DoOnCondition(ReadyPoses, ApplyPoses);
        }
        static void OnLeaveCustom()
        {
            Hooks.MotionIKLoadData -= FixBinder;
            PoseIds.Clear();
        }
    }
    internal static class Hooks
    {
        internal static event Action MotionIKLoadData = delegate { };

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StateMiniSelection), nameof(StateMiniSelection.InitPosePack))]
        static void InitPosePackPostfix(StateMiniSelection.PosePack pack) =>
            pack._posePtn.input.characterLimit = 5;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MotionIK), nameof(MotionIK.LoadData))]
        static void MotionIKLoadDataPostfix() => MotionIKLoadData();
    }

    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CoastalSmell.Plugin.Guid)]
    public partial class Plugin : BasePlugin
    {
        internal static Plugin Instance;
        public const string Name = "CCPoseLoader";
        public const string Guid = $"{Process}.{Name}";
        public const string Version = "2.1.1";
        private Harmony Patch;
        public override void Load()
        {
            Patch = Harmony.CreateAndPatchAll(typeof(Hooks), $"{Name}.Hooks");
            Instance = this;
            Event.MalePoses = Config.Bind(new ConfigDefinition("General", "MalePoseFile"), "male/poses.json");
            Event.FemalePoses = Config.Bind(new ConfigDefinition("General", "FemalePoseFile"), "female/poses.json");
            Event.Initialize();
        }
        public override bool Unload() => true.With(Patch.UnpatchSelf) && base.Unload();
    }
}