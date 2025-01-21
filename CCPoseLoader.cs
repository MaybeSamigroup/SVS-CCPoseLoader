using HarmonyLib;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime;
using CharacterCreation;
using CharacterCreation.ListInfo;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PoseBuff = Il2CppSystem.Collections.Generic.List<CharacterCreation.ListInfo.PoseInfoData>;
using PoseDefs = Il2CppSystem.Collections.Generic.IReadOnlyList<CharacterCreation.ListInfo.PoseInfoData>;
using PoseSet = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string[]>>>;

namespace CCPoseLoader
{
    // gather reference UI component from clothes edit panel.
    // abdata/ui.unity3d should be loaded before.
    internal class UIRefs
    {
        // pose control object
        internal GameObject PoseControl;
        // window background
        internal Image Window;
        // outlined text component
        internal TextMeshProUGUI Text;
        // button bacground image
        internal Image Image;
        // per state button image
        internal Button Button;

        internal UIRefs(AssetBundle bundle, GameObject poseControl) :
            this(new GameObject(bundle.LoadAsset_Internal("assets/illgames/assetbundle/custom/ui/03_clothes/00_clothes.prefab", Il2CppType.From(typeof(GameObject))).Pointer))
        {
            PoseControl = poseControl;
        }
        internal UIRefs(GameObject refui) : this(refui.transform.Find("BaseView"))
        { }
        internal UIRefs(Transform refui) :
            this(refui.Find("BG").GetComponent<Image>(), refui.Find("Contents"))
        { }
        internal UIRefs(Image image, Transform refui) :
            this(refui.Find("Label").Find("T02-1").GetComponent<TextMeshProUGUI>(),
                 refui.Find("objOptionSliders").Find("InputSliderButton(2Line)").Find("Interface").Find("btnDefault"))
        {
            Window = image;
        }
        internal UIRefs(TextMeshProUGUI text, Transform refui) : this(refui.GetComponent<Image>(), refui.GetComponent<Button>())
        {
            Text = text;
        }
        internal UIRefs(Image image, Button button)
        {
            Image = image;
            Button = button;
        }
        // copy reference properties to target
        internal Image RefWindow(Image dest) => dest.RefUI(Window);
        // copy reference properties to target
        internal TextMeshProUGUI RefText(TextMeshProUGUI dest) => dest.RefUI(Text);
        // copy reference properties to target
        internal Image RefButton(Image dest) => dest.RefUI(Image);
        // copy reference properties to target
        internal Button RefButton(Button dest) => dest.RefUI(Button);
    }
    // abstraction of labeled (categorized) status control
    internal class StateControl<TLabels, TStatus>
    {
        internal string Name;
        // internal label value to display representation
        internal Func<TLabels, string> TranslateLabel;
        // internal status value to display representation
        internal Func<TStatus, string> TranslateState;
        // next label status action
        internal Action<TLabels> Toggle;
        // current label status function
        internal Func<TLabels, TStatus> Get;
        // full list of labels
        internal IEnumerable<TLabels> Labels;
        // compose toggle button action
        // perform next label action then
        // apply current status to text component.
        internal Action ComposeAction(TLabels label, TextMeshProUGUI ui) => () =>
        {
            Toggle(label);
            ui.SetText(TranslateState(Get(label)));
        };
        internal GameObject ComposeControl(GameObject go, UIRefs refs)
        {
            go = go.Wrap(new GameObject(Name)).HorizontalLayout();
            ComposeControl(
                VerticalLayout(go.Wrap(new GameObject("Labels"))),
                VerticalLayout(go.Wrap(new GameObject("Buttons"))),
                VerticalLayout(go.Wrap(new GameObject("Status"))), refs);
            return go;
        }
        internal GameObject VerticalLayout(GameObject go)
        {
            go.AddComponent<RectTransform>();
            go.AddComponent<LayoutElement>().preferredHeight = 25 * Labels.Count();
            go.AddComponent<VerticalLayoutGroup>().With(ui =>
            {
                ui.spacing = 1;
                ui.childAlignment = TextAnchor.LowerCenter;
                ui.childControlWidth = true;
                ui.childControlHeight = true;
            });
            go.AddComponent<ContentSizeFitter>().With(ui =>
            {
                ui.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                ui.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            });
            return go;
        }
        // compose each label to {labels, buttons, status} control
        internal void ComposeControl(GameObject labels, GameObject buttons, GameObject status, UIRefs refs) =>
            Labels.Do(label =>
            {
                labels.Wrap(new GameObject("Label")).Label(refs, TranslateLabel(label));
                buttons.Wrap(new GameObject("Button")).ToggleButton(refs)
                    .GetComponent<Button>().onClick.AddListener(
                        ComposeAction(label, status.Wrap(new GameObject("Label"))
                            .State(refs, TranslateState(Get(label))).GetComponent<TextMeshProUGUI>()));
            });
        internal void UpdateState()
        {
            if (HumanCustom.Instance.transform.Find("UI").Find("Root").Find(Plugin.Name) != null)
            {
                UpdateState(HumanCustom.Instance.transform.Find("UI")
                    .Find("Root").Find(Plugin.Name).Find("Window").Find(Name).Find("Status")
                    .GetComponentsInChildren<TextMeshProUGUI>());
            }
        }
        internal void UpdateState(TextMeshProUGUI[] status)
        {
            for (int index = Labels.Count(); index >= 0; --index)
            {
                Enumerable.Range(0, Labels.Count()).Zip(Labels)
                    .Do(tuple => status[tuple.Item1].SetText(TranslateState(Get(tuple.Item2))));
            }
        }
    }
    // UI generation helpers
    internal static class UIFactory
    {
        internal static ConfigEntry<bool> InitialVisibility { get; set; }
        // tweaks to make local namespace clean
        internal static void With<T>(this T item, Action<T> action) => action(item);
        internal static void With<T, S>(this T item, Func<T, S> action) => action(item);
        // tweaks to link game object hierarchy first
        internal static GameObject Wrap(this GameObject outer, GameObject inner)
        {
            inner.transform.SetParent(outer.transform);
            return inner;
        }
        // copy componet property from reference
        internal static Image RefUI(this Image dest, Image refs)
        {
            dest.type = refs.type;
            dest.fillMethod = refs.fillMethod;
            dest.fillOrigin = refs.fillOrigin;
            dest.fillAmount = refs.fillAmount;
            dest.fillCenter = refs.fillCenter;
            dest.fillClockwise = refs.fillClockwise;
            dest.sprite = refs.sprite;
            dest.overrideSprite = refs.overrideSprite;
            return dest;
        }
        // copy componet property from reference
        internal static TextMeshProUGUI RefUI(this TextMeshProUGUI dest, TextMeshProUGUI refs)
        {
            dest.autoSizeTextContainer = refs.autoSizeTextContainer;
            dest.fontSharedMaterial = refs.fontSharedMaterial;
            dest.font = refs.font;
            dest.alignment = refs.alignment;
            dest.overflowMode = refs.overflowMode;
            dest.enableWordWrapping = refs.enableWordWrapping;
            dest.fontSize = refs.fontSize;
            dest.faceColor = refs.faceColor;
            dest.fontWeight = refs.fontWeight;
            return dest;
        }
        // copy componet property from reference
        internal static Button RefUI(this Button dest, Button refs)
        {
            dest.interactable = refs.interactable;
            dest.transition = Selectable.Transition.SpriteSwap;
            dest.spriteState = refs.spriteState;
            return dest;
        }
        internal static GameObject HorizontalLayout(this GameObject go) {
            go.AddComponent<RectTransform>();
            go.AddComponent<LayoutElement>();
            go.AddComponent<HorizontalLayoutGroup>().With(ui =>
            {
                ui.padding = new(0, 0, 10, 10);
                ui.spacing = 5;
                ui.childAlignment = TextAnchor.MiddleCenter;
                ui.childControlWidth = true;
                ui.childControlHeight = true;
            });
            go.AddComponent<ContentSizeFitter>().With(ui =>
            {
                ui.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                ui.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            });
            return go;
        }
        // add button to open clothes and accessory status window
        internal static void UI(this GameObject go, UIRefs refs)
        {
            go.AddComponent<RectTransform>();
            go.AddComponent<LayoutElement>().With(ui =>
            {
                ui.minWidth = 64;
                ui.minHeight = 64;
            });
            refs.RefButton(go.AddComponent<Button>()).targetGraphic = refs.RefButton(go.AddComponent<Image>());
            go.GetComponent<Button>()
                .onClick.AddListener(new GameObject(Plugin.Name).Canvas(refs));
        }
        // create clothes and accessory status canvas and return show/hide action
        internal static Action Canvas(this GameObject go, UIRefs refs)
        {
            go.transform.SetParent(HumanCustom.Instance.transform.Find("UI").Find("Root"));
            go.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>().With(ui =>
            {
                ui.referenceResolution = new(1920, 1080);
                ui.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                ui.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            });
            go.AddComponent<GraphicRaycaster>();
            go.GetComponent<RectTransform>();
            go.Wrap(new GameObject("Window")).Window(refs);
            go.SetActive(InitialVisibility.Value);
            return () => { go.SetActive(!go.active); };
        }
        // internal pose index to display representation
        internal static Func<int, string> TranslatePose;
        // create draggable window
        internal static GameObject Window(this GameObject go, UIRefs refs)
        {
            go.AddComponent<RectTransform>().With(ui =>
            {
                ui.anchorMin = new(0.0f, 1.0f);
                ui.anchorMax = new(0.0f, 1.0f);
                ui.pivot = new(0.0f, 1.0f);
                ui.sizeDelta = new(180 / 1920, 900 / 1080);
                ui.anchoredPosition = new(1600, -120);
            });
            go.AddComponent<VerticalLayoutGroup>().With(ui =>
            {
                ui.padding = new(20, 20, 10, 10);
                ui.childControlWidth = true;
                ui.childControlHeight = true;
            });
            go.AddComponent<ContentSizeFitter>().With(ui =>
            {
                ui.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                ui.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            });
            refs.RefWindow(go.AddComponent<Image>());
            go.Wrap(new GameObject("Title")).With(title =>
            {
                title.AddComponent<CanvasRenderer>();
                title.AddComponent<RectTransform>();
                title.AddComponent<LayoutElement>().minHeight = 30;
                title.AddComponent<UI_DragWindow>().With(ui =>
                {
                    ui.rtMove = go.GetComponent<RectTransform>();
                });
                refs.RefText(title.AddComponent<TextMeshProUGUI>()).With(ui =>
                {
                    ui.alignment = TextAlignmentOptions.Center;
                    ui.overflowMode = TextOverflowModes.Overflow;
                    ui.SetText(TranslatePose(HumanCustom.Instance?.NowPose ?? 0));
                });
            });
            go.Wrap(refs.PoseControl);
            ClothesControl.ComposeControl(go, refs);
            AccessoryControl.ComposeControl(go, refs);
            return go;
        }
        // clothes status control
        internal static StateControl<ChaFileDefine.ClothesKind, ChaFileDefine.ClothesState> ClothesControl;
        // accessory status control
        internal static StateControl<int, bool> AccessoryControl;
        // create label component
        internal static GameObject Label(this GameObject go, UIRefs refs, string label)
        {
            go.AddComponent<RectTransform>();
            go.AddComponent<CanvasRenderer>();
            go.AddComponent<LayoutElement>();
            refs.RefText(go.AddComponent<TextMeshProUGUI>()).With(ui =>
            {
                ui.fontSize = 15;
                ui.alignment = TextAlignmentOptions.Right;
                ui.SetText(label);
            });
            return go;
        }
        // create button component
        internal static GameObject ToggleButton(this GameObject go, UIRefs refs)
        {
            go.AddComponent<RectTransform>();
            go.AddComponent<CanvasRenderer>();
            go.AddComponent<LayoutElement>().With(ui =>
            {
                ui.preferredWidth = 24;
                ui.preferredHeight = 24;
            });
            refs.RefButton(go.AddComponent<Button>()).targetGraphic = refs.RefButton(go.AddComponent<Image>());
            return go;
        }
        // create state comoponent
        internal static GameObject State(this GameObject go, UIRefs refs, string state)
        {
            go.AddComponent<RectTransform>();
            go.AddComponent<CanvasRenderer>();
            go.AddComponent<LayoutElement>();
            refs.RefText(go.AddComponent<TextMeshProUGUI>()).With(ui =>
            {
                ui.fontSize = 15;
                ui.alignment = TextAlignmentOptions.Center;
                ui.SetText(state);
            });
            return go;
        }
        internal static GameObject PoseControl(this Transform layout, GameObject go) {
            go.HorizontalLayout();
            go.Wrap(layout.Find("ptnSelect").Find("btnPrev").gameObject.MirrorButton());
            go.Wrap(layout.Find("ptnSelect").Find("btnNext").gameObject.MirrorButton());
            go.Wrap( layout.Find("tglPose").gameObject.MirrorToggle());
            return go;
        }
        internal static GameObject MirrorButton(this GameObject go) {
            var mirror = UnityEngine.Object.Instantiate(go);
            mirror.GetComponent<Button>()
                .onClick.AddListener(go.GetComponent<Button>().MirrorAction());
            return mirror;
        }
        internal static GameObject MirrorToggle(this GameObject go) {
            var mirror = UnityEngine.Object.Instantiate(go);
            mirror.GetComponent<Toggle>()
                .onValueChanged.AddListener(go.GetComponent<Toggle>().MirrorAction());
            return mirror;
        }
        internal static Action MirrorAction(this Button ui) =>
            () => ui.onClick.Invoke();
        internal static Action<bool> MirrorAction(this Toggle ui) =>
            (value) => ui.onValueChanged.Invoke(value);

    }
    internal static class Extension
    {
        // hook to replace pose list
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
            // update clothes and accessesory status window label
            if (__instance.transform.Find("UI").Find("Root").Find(Plugin.Name) != null)
            {
                __instance.transform.Find("UI").Find("Root").Find(Plugin.Name)
                    .GetComponentInChildren<TextMeshProUGUI>()
                        .SetText(UIFactory.TranslatePose(HumanCustom.Instance?.NowPose ?? 0));
            }
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HumanCustom), nameof(HumanCustom.UpdateClothesState))]
        [HarmonyPatch(typeof(HumanCustom), nameof(HumanCustom.SetClothesState), typeof(HumanCustom.ClothStateSeter.State))]
        internal static void UpdateClothesStatePostfix() =>
            UIFactory.ClothesControl.UpdateState();
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HumanCustom), nameof(HumanCustom.UpdateAccessoryState))]
        internal static void UpdateAccessoryStatePostfix() =>
            UIFactory.AccessoryControl.UpdateState();
        // UI should be made after abdata/ui.unity3d loaded
        // so there would be better timing ...
        [HarmonyPostfix]
        [HarmonyPatch(typeof(StateMiniSelection), nameof(StateMiniSelection.InitPosePack))]
        internal static void StateMiniSelectionInitPosePackPostfix(Component disposableComponent)
        {
            foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles_Native())
            {
                if (Path.GetFileName(bundle.name).Equals("ui.unity3d"))
                {
                    disposableComponent.transform.Find("Window").Find("StateWindow")
                        .Find("Pose").Find("PosePtn").Find("Layout").With(layout =>{
                        // tweaks to mini state window
                        layout.Find("ptnSelect").Find("InputField_Integer").With(input =>
                        {
                            input.GetComponent<RectTransform>().sizeDelta = new(80, 26);
                            input.GetComponent<TMP_InputField>().characterLimit = 5;
                        });
                        layout.gameObject.Wrap(new GameObject("ShowHide"))
                            .UI(new UIRefs(bundle, layout.PoseControl(new GameObject("PoseControl"))));
                    });
                    return;
                }
            }
        }
        internal static string QualifiedName(this PoseInfoData info) =>
            $"{info.Bundle}:{info.Asset}:{info.State}";
        internal static string ConfigPath(this string value) =>
            Path.Combine(Paths.ConfigPath, Plugin.Name, value);
        internal static Func<string, string> GuiTranslation(this Dictionary<string, string> names) =>
            input => names.GetValueOrDefault(input) ?? input;
        internal static Func<string, string> PoseTranslation(this Dictionary<string, string> names) =>
            input => names.GetValueOrDefault(input) ?? input.Split(":")[^1];
    }

    [BepInProcess(Process)]
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BasePlugin
    {
        public const string Process = "SamabakeScramble";
        public const string Name = "CCPoseLoader";
        public const string Guid = $"{Process}.{Name}";
        public const string Version = "1.0.0";
        private Harmony[] Patches;
        private ConfigEntry<string> Poses { get; set; }
        private ConfigEntry<string> Translations { get; set; }
        private Func<string, string> GuiTranslation;
        private Func<string, string> PoseTranslation;
        public override void Load()
        {
            UIFactory.InitialVisibility = Config.Bind(new ConfigDefinition("General", "InitialVisibility"), true);
            Poses = Config.Bind(new ConfigDefinition("General", "PoseFile"), "poses.json");
            Translations = Config.Bind(new ConfigDefinition("General", "TranslationDir"), "ja_JP");

            GuiTranslation = JsonSerializer
                .Deserialize<Dictionary<string, string>>
                    (File.OpenRead(Path.Combine(Translations.Value, "gui.json").ConfigPath().ConfigPath())).PoseTranslation();
            PoseTranslation = JsonSerializer
                .Deserialize<Dictionary<string, string>>
                    (File.OpenRead(Path.Combine(Translations.Value, "names.json").ConfigPath().ConfigPath())).PoseTranslation();

            Patches = new[]
            {
                Harmony.CreateAndPatchAll(typeof(Extension), $"{Name}.Hooks")
            };
            UIFactory.ClothesControl = new StateControl<ChaFileDefine.ClothesKind, ChaFileDefine.ClothesState>
            {
                Name = "ClothesCOntrol",
                TranslateLabel = (input) => GuiTranslation(Enum.GetName(input)),
                TranslateState = (input) => GuiTranslation(Enum.GetName(input)),
                Toggle = (input) => HumanCustom.Instance?.Human?.cloth?.SetClothesStateNext(input),
                Get = (input) => HumanCustom.Instance?.Human?.cloth?.GetClothesStateType(input) ?? ChaFileDefine.ClothesState.Naked,
                Labels = Enum.GetValues<ChaFileDefine.ClothesKind>()
            };
            UIFactory.AccessoryControl = new StateControl<int, bool>()
            {
                Name = "AccessoryControl",
                TranslateLabel = (input) => $"{GuiTranslation("Slot")}{input + 1}",
                TranslateState = (input) => GuiTranslation(input ? "ON" : "OFF"),
                Toggle = (input) => HumanCustom.Instance?.Human?.acs?.SetAccessoryState(input, !HumanCustom.Instance?.Human?.acs?.IsAccessory(input) ?? false),
                Get = (input) => HumanCustom.Instance?.Human?.acs?.IsAccessory(input) ?? false,
                Labels = Enumerable.Range(0, 20)
            };
            Extension.OnPosesLoad += (input) =>
            {
                var status = new List<string>();
                var output = new PoseBuff();
                var index = 0;
                while (input.TryGet(index++, out PoseInfoData info))
                {
                    output.Add(info);
                    status.Add(info.QualifiedName());
                }
                var gender = index == 10 ? "male" : "female";
                JsonSerializer.Deserialize<PoseSet>(File.OpenRead(Poses.Value.ConfigPath())).Where(item => gender.Equals(item.Key))
                    .SelectMany(item => item.Value
                    .SelectMany(bundle2assets => bundle2assets.Value
                    .SelectMany(asset2states => asset2states.Value
                    .Select(state => new PoseInfoData() { Bundle = bundle2assets.Key, Asset = asset2states.Key, State = state }))))
                    .Do(pose => { output.Add(pose); status.Add(pose.QualifiedName()); });
                UIFactory.TranslatePose = (input) => PoseTranslation(status[input]);
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