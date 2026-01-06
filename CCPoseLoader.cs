using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Disposables;
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
using PoseSet = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string[]>>;

namespace CCPoseLoader
{
    public static class Event
    {
        static ConfigEntry<string> MalePoses = 
            Plugin.Instance.Config.Bind(new ConfigDefinition("General", "MalePoseFile"), "male/poses.json");

        internal static ConfigEntry<string> FemalePoses =
            Plugin.Instance.Config.Bind(new ConfigDefinition("General", "FemalePoseFile"), "female/poses.json");

        static string ConfigPath(this string value) => Path.Combine(Util.UserDataPath, "plugins", Plugin.Name, value);

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

        static bool CheckPoses(HumanCustom scene) => scene.PoseList != null;

        static void ApplyPoses(HumanCustom scene) =>
            scene.PoseList = new(scene.PoseList.Yield().Concat(CurrentPoses(scene)).AsIl2Cpp().Pointer);

        static void FixBinder(MotionIKDataBinder binder) =>
            (binder != null && binder.Data != null && binder.MotionIK.Data == null)
                .Maybe(() => binder.MotionIK.SetData(binder.Data));

        static CompositeDisposable Subscriptions;

        internal static void Subscribe(HumanCustom scene) => Subscriptions = [
            scene.OnUpdateAsObservable()
                .Select(_ => scene)
                .FirstAsync(CheckPoses)
                .Subscribe(ApplyPoses),
            Hooks.OnMotionIKLoadData
                .Select(_ => scene?.Human?.gameObject?.GetComponent<MotionIKDataBinder>())
                .Subscribe(FixBinder)
        ];

        internal static IDisposable[] Initialize() => [
            SingletonInitializerExtension<HumanCustom>.OnStartup.Subscribe(Subscribe),
            SingletonInitializerExtension<HumanCustom>.OnDestroy.Subscribe(_ => Subscriptions.Dispose()),
        ];
                
    }
    internal static class Hooks
    {
        static Subject<Unit> MotionIKLoadData = new();
        internal static IObservable<Unit> OnMotionIKLoadData => MotionIKLoadData.AsObservable();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StateMiniSelection), nameof(StateMiniSelection.InitPosePack))]
        static void InitPosePackPostfix(StateMiniSelection.PosePack pack) =>
            pack._posePtn.input.characterLimit = 5;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MotionIK), nameof(MotionIK.LoadData))]
        static void MotionIKLoadDataPostfix() => MotionIKLoadData.OnNext(Unit.Default);

        internal static IDisposable Initialize() =>
            Disposable.Create(Harmony.CreateAndPatchAll(typeof(Hooks), $"{Plugin.Name}.Hooks").UnpatchSelf);
    }

    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(CoastalSmell.Plugin.Guid)]
    public partial class Plugin : BasePlugin
    {
        internal static Plugin Instance;
        public const string Name = "CCPoseLoader";
        public const string Guid = $"{Process}.{Name}";
        public const string Version = "2.2.0";
        CompositeDisposable Subscriptions;
        public Plugin() : base() => Instance = this;
        public override void Load() => Subscriptions = [Hooks.Initialize(), ..Event.Initialize()];
        public override bool Unload() =>
            true.With(Subscriptions.Dispose) && base.Unload();
    }
}