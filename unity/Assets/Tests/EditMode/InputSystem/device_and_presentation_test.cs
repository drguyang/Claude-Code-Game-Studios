// Story 008 · 设备态与呈现契约 —— EditMode 真身(D1 / D2 / D3 / D4 + IHaptics Verify)
//
// 权威来源:production/epics/input-system/story-008-device-and-presentation-contract.md
//   · AC-3-D1(BLOCKING)QueryBinding → {device, bindingPath, iconKey};未绑定 = 明确枚举
//   · AC-3-D2(BLOCKING)无素材无时机 —— 构建报告断言(TypeRef 面,非 grep)+ 类型字段白名单
//   · AC-3-D3(BLOCKING)设备移除 → 合成 release,全部 IsPressed 转 false(同帧,无卡键)
//   · AC-3-D4(BLOCKING)Mixed 迟滞三条件(径向 ‖Δ‖,禁逐分量;两列各表;漂移不抖切)
//   · 附注(TR-input-015 无 AC)IHaptics 接口存在(OpenXRInput spike 结果见 Completion Notes)
// GDD input-system.md:§States 一 · §Tuning Knobs 一之二(Ⓑ 径向 / Ⓑ.1 两列)·
//   §Visual/Audio 二(通道非语义)/ 三(键名非素材)· AC-3-D1…D4 原文。
//
// ⚠️ 落点:故事登记口径 tests/integration/input_system/;Unity 只编译 unity/Assets/ 树 ⇒
//    真身 = 本文件(承 Story 001/005/006/007 同一先例)。
// ⚠️ 负向夹具全部住本装配顶层(internal struct,不嵌套 —— Cecil 嵌套名带 '/' 分隔,
//    顶层名的 Ordinal 比较与 reflection FullName 一致);不污染生产扫描面、不新增 asmdef。
// ⚠️ 确定性:零随机 / 零墙钟;设备 = AddDevice/RemoveDevice 自造自清(try/finally 复位);
//    动作资产 = FromJson 合成夹具(不读真 .inputactions —— D 组判据与内容解耦)。
// ⚠️ 测试旋钮值为**结构性合法常量**(证形状;真实值归用户数值轮,住
//    assets/data/input_device_switch.json 种子,本文件不拍值 —— 承 Story 007 测试乘子纪律)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Input;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using InputApi = UnityEngine.InputSystem.InputSystem;

namespace DaYiJingCheng.Tests.Unit.InputSystem
{
    // ═════════ D2 负例夹具(AC-3-D2;顶层 internal,IL 面按全名点射)═════════

    /// <summary>QA 负例「返回类型加一个 float fadeDuration ⇒ 类型断言红」的真身:
    /// 合法字段 + FadeDuration(float)混装 —— reflection 面与 IL 面各红一次(时机字段 + 浮点)。</summary>
    internal struct BindingResultFadeDurationFixture
    {
        public string IconKey;          // 合法字段(证明红行只点 FadeDuration,不是一片糊)
        public float FadeDuration;      // 违例:时机字段 + 浮点叶子
    }

    /// <summary>可见性字段的第二种形态(story 点名 "visible" 类):bool 也落名字白名单外。</summary>
    internal struct BindingResultVisibleFixture
    {
        public string IconKey;
        public bool Visible;            // 违例:可见性字段(名字面红)
    }

    /// <summary>素材字段的真身夹具:字段类型 UnityEngine.Sprite / Texture2D / Font ⇒ 测试装配
    /// 产物 TypeRef 表出现三族 —— D2 面 1(素材 TypeRef)与面 1b(点射)的负例驱动源
    /// (承音频 IlScanNegativeFixture 先例;三支各一字段,黑名单拼写破坏不可测 qa-008-5)。</summary>
    internal struct SpriteBearingFixture
    {
        public UnityEngine.Sprite Icon;       // 违例:素材类型进交付物依赖面(AC-3-D2 明禁)
        public UnityEngine.Texture2D Atlas;   // 违例:Texture 族第二支(黑名单拼写自证)
        public UnityEngine.Font LabelFont;    // 违例:字体族(黑名单拼写自证)
    }

    /// <summary>常量字符串引用素材路径(D2 QA Edge「按资产引用扫,不按源码字面扫」的自证):
    /// 本类型的字符串常量含 .png 路径 —— TypeRef 面**必须**对它无反应(键名/常量不约束,GDD §Visual/Audio 三)。</summary>
    internal static class AssetPathInStringConstFixture
    {
        public const string LooksLikeAssetPath = "Assets/Art/Icons/kbd_e.png";
    }

    [TestFixture]
    internal sealed class DeviceAndPresentationTest
    {
        // ── 结构性合法测试旋钮(两列;真实值归数值轮)──
        // 轴列:threshold 1/2 = 32768 · tolerance 1/8 = 8192 · dwell 4 tick(20 Hz ⇒ 200 ms)
        // 指针列:threshold 4px = 262144 · tolerance 1px = 65536 · dwell 4 tick
        private const int AxisThreshold = 32768;
        private const int AxisTolerance = 8192;
        private const int AxisDwell = 4;
        private const int PointerThreshold = 262144;
        private const int PointerTolerance = 65536;
        private const int PointerDwell = 4;

        // ── EditMode 按住前置的环境接线(仅 D3 三个按下前置用)──
        // EditMode(非 play)下 InputSystem.Update() 的 defaultUpdateType = Editor
        // (InputManager.cs defaultUpdateType:!m_RunPlayerUpdatesInEditMode ⇒ Editor),
        // 而 InputActionState.NotifyControlStateChanged 在 currentUpdateType == Editor 时
        // early-return(InputActionState.cs:1503)⇒ 动作 press 闩从不更新 ⇒ IsPressed()
        // 恒 false(设备状态本身已写入,仅动作侧不感知)。置该 feature flag ⇒
        // gameIsPlaying = true(InputManager.cs gameIsPlaying 的 || m_RunPlayerUpdatesInEditMode)
        // ⇒ defaultUpdateType 转 Dynamic ⇒ 监视器生效。**测试环境接线,不改产品逻辑**;
        // finally 恢复 false,防跨测泄漏(其余测试仍走 Editor update)。
        private const string RunPlayerUpdatesInEditModeFlag = "RUN_PLAYER_UPDATES_IN_EDIT_MODE";

        /// <summary>进入「按住前置」前调用:打开 RUN_PLAYER_UPDATES_IN_EDIT_MODE 使
        /// 手动 InputSystem.Update() 按 Dynamic update 处理(见字段注释)。</summary>
        private static void BeginPressedPrecondition()
            => InputApi.settings.SetInternalFeatureFlag(RunPlayerUpdatesInEditModeFlag, true);

        /// <summary>恢复 feature flag(必须在 finally 调用 —— 与 <see cref="BeginPressedPrecondition"/> 成对)。</summary>
        private static void EndPressedPrecondition()
            => InputApi.settings.SetInternalFeatureFlag(RunPlayerUpdatesInEditModeFlag, false);

        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot([CallerFilePath] string callerPath = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerPath), "..", "..", "..", "..", ".."));

        /// <summary>D1/D3 合成动作资产夹具(FromJson,不读真资产 —— Story 001 规则一约束
        /// 的是**出货**唯一资产,测试夹具从 JSON 文本构造不违规)。绑重面覆盖:
        /// Kbd 简单绑 / Pad 简单绑(buttonNorth,iconKey 剥 button 前缀)/ Pad 复合本体(2DVector)
        /// + part 路径(leftStick/up、leftStick/down)/ Xr tag 路径({PrimaryAction})/
        /// 未知布局路径(<TestDevice>/q,只入资产不查询)/ D3 用的 buttonSouth、buttonEast。</summary>
        private const string FixtureAssetJson = @"{
  ""name"": ""Story008Fixture"",
  ""maps"": [
    {
      ""name"": ""Gameplay"",
      ""id"": ""3f1f2a6c-0b1e-4f4f-9b7a-8c2d5e6f7a01"",
      ""actions"": [
        { ""name"": ""Interact"", ""type"": ""Button"", ""id"": ""aaaaaaaa-0000-0000-0000-000000000001"" },
        { ""name"": ""Move2D"", ""type"": ""Value"", ""id"": ""aaaaaaaa-0000-0000-0000-000000000002"" },
        { ""name"": ""Emergency"", ""type"": ""Button"", ""id"": ""aaaaaaaa-0000-0000-0000-000000000003"" },
        { ""name"": ""XrProbe"", ""type"": ""Button"", ""id"": ""aaaaaaaa-0000-0000-0000-000000000004"" }
      ],
      ""bindings"": [
        { ""name"": """", ""path"": ""<Keyboard>/e"", ""interactions"": """", ""processors"": """", ""groups"": ""Keyboard&Mouse"", ""action"": ""Interact"", ""isComposite"": false, ""isPartOfComposite"": false },
        { ""name"": """", ""path"": ""<Gamepad>/buttonNorth"", ""interactions"": """", ""processors"": """", ""groups"": ""Gamepad"", ""action"": ""Interact"", ""isComposite"": false, ""isPartOfComposite"": false },
        { ""name"": ""Dpad"", ""path"": ""2DVector"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""Move2D"", ""isComposite"": true, ""isPartOfComposite"": false },
        { ""name"": ""up"", ""path"": ""<Gamepad>/leftStick/up"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""Move2D"", ""isComposite"": false, ""isPartOfComposite"": true },
        { ""name"": ""down"", ""path"": ""<Gamepad>/leftStick/down"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""Move2D"", ""isComposite"": false, ""isPartOfComposite"": true },
        { ""name"": """", ""path"": ""<Gamepad>/buttonSouth"", ""interactions"": """", ""processors"": """", ""groups"": ""Gamepad"", ""action"": ""Emergency"", ""isComposite"": false, ""isPartOfComposite"": false },
        { ""name"": """", ""path"": ""<Gamepad>/buttonEast"", ""interactions"": """", ""processors"": """", ""groups"": ""Gamepad"", ""action"": ""Emergency"", ""isComposite"": false, ""isPartOfComposite"": false },
        { ""name"": """", ""path"": ""<TestDevice>/q"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""Interact"", ""isComposite"": false, ""isPartOfComposite"": false }
      ]
    },
    {
      ""name"": ""Xr"",
      ""id"": ""3f1f2a6c-0b1e-4f4f-9b7a-8c2d5e6f7a02"",
      ""actions"": [
        { ""name"": ""XrProbe"", ""type"": ""Button"", ""id"": ""bbbbbbbb-0000-0000-0000-000000000004"" }
      ],
      ""bindings"": [
        { ""name"": """", ""path"": ""<XRController>/{PrimaryAction}"", ""interactions"": """", ""processors"": """", ""groups"": ""XR"", ""action"": ""XrProbe"", ""isComposite"": false, ""isPartOfComposite"": false }
      ]
    }
  ],
  ""controlSchemes"": []
}";

        private static InputActionAsset NewFixtureAsset() => InputActionAsset.FromJson(FixtureAssetJson);

        private static DeviceSwitchHysteresis NewHysteresis()
            => new DeviceSwitchHysteresis(AxisThreshold, AxisTolerance, AxisDwell,
                                          PointerThreshold, PointerTolerance, PointerDwell);

        // ═══════════════════ AC-3-D1 · QueryBinding 形状与未绑定枚举 ═══════════════════

        /// <summary>D1 正例(已绑定):Kbd 与 Pad 各一条 —— 三字段 {device, bindingPath, iconKey}
        /// 齐且 iconKey = 键名非素材("Kbd.E" / "Pad.North",GDD §Visual/Audio 三 表格例)。</summary>
        [Test]
        public void test_queryBinding_bound_returnsThreeFieldsAndKeyNames()
        {
            var actions = NewFixtureAsset();
            try
            {
                var kbd = BindingQuery.QueryBinding(BindingDeviceClass.Kbd, "<Keyboard>/e", actions);
                Assert.That(kbd.Status, Is.EqualTo(BindingQueryStatus.Bound));
                Assert.That(kbd.Device, Is.EqualTo(BindingDeviceClass.Kbd));
                Assert.That(kbd.BindingPath, Is.EqualTo("<Keyboard>/e"));
                Assert.That(kbd.IconKey, Is.EqualTo("Kbd.E"), "键名口径 = GDD 表例 Kbd.E");

                var pad = BindingQuery.QueryBinding(BindingDeviceClass.Pad, "<Gamepad>/buttonNorth", actions);
                Assert.That(pad.Status, Is.EqualTo(BindingQueryStatus.Bound));
                Assert.That(pad.IconKey, Is.EqualTo("Pad.North"),
                    "button 前缀剥除(承 GDD 例 \"Pad.South\" 的同型派生)");
            }
            finally { UnityEngine.Object.DestroyImmediate(actions); }
        }

        /// <summary>D1 主判据:未绑定返回**明确枚举**——非 null(值类型形状下物理不可能)、
        /// 不抛异常。负例「实现用 null 表未绑定 ⇒ 红 / 用异常 ⇒ 红」的机械对照面:
        /// 返回类型是 struct(IsValueType)⇒ 不可能为 null;调用不抛 ⇒ Assert.DoesNotThrow 守。</summary>
        [Test]
        public void test_queryBinding_unbound_returnsExplicitEnum_notNullNotThrow()
        {
            Assert.That(typeof(BindingQueryResult).IsValueType, Is.True,
                "返回类型 = 值类型 ⇒ 「null 表未绑定」结构性不可能(QA 负例形态一被形状封死)");
            var actions = NewFixtureAsset();
            try
            {
                Assert.DoesNotThrow(() =>
                {
                    var r = BindingQuery.QueryBinding(BindingDeviceClass.Kbd, "<Keyboard>/q", actions);
                    Assert.That(r.Status, Is.EqualTo(BindingQueryStatus.NotBound), "未绑定必须走枚举分支");
                    Assert.That(r.IconKey, Is.Empty, "非 Bound 时字段退化为空值(消费方按 Status 分支)");
                }, "未绑定不得抛异常(QA 负例形态二)");
            }
            finally { UnityEngine.Object.DestroyImmediate(actions); }
        }

        /// <summary>D1 Edge「device 未知枚举值」⇒ UnknownDevice(仍非 null 非抛)。</summary>
        [Test]
        public void test_queryBinding_unknownDeviceEnumValue_reportsUnknownDevice()
        {
            var actions = NewFixtureAsset();
            try
            {
                var r = BindingQuery.QueryBinding((BindingDeviceClass)99, "<Keyboard>/e", actions);
                Assert.That(r.Status, Is.EqualTo(BindingQueryStatus.UnknownDevice));
                Assert.That(r.Device, Is.EqualTo(BindingDeviceClass.Unknown));
            }
            finally { UnityEngine.Object.DestroyImmediate(actions); }
        }

        /// <summary>D1 Edge「bindingPath 空串」⇒ NotBound(空串不是「全等匹配」的通配)。</summary>
        [Test]
        public void test_queryBinding_emptyBindingPath_reportsNotBound()
        {
            var actions = NewFixtureAsset();
            try
            {
                Assert.That(BindingQuery.QueryBinding(BindingDeviceClass.Kbd, "", actions).Status,
                    Is.EqualTo(BindingQueryStatus.NotBound), "空串");
                Assert.That(BindingQuery.QueryBinding(BindingDeviceClass.Kbd, null, actions).Status,
                    Is.EqualTo(BindingQueryStatus.NotBound), "null 入参同样走枚举(非 ArgumentNullException)");
            }
            finally { UnityEngine.Object.DestroyImmediate(actions); }
        }

        /// <summary>D1 Edge「复合绑重的 part 路径」:part 路径自身可查得(iconKey 多段 "_" 连接);
        /// 复合本体路径("2DVector",无设备前缀)不归属任何设备类 ⇒ NotBound。</summary>
        [Test]
        public void test_queryBinding_compositePartPath_resolvesAsPart()
        {
            var actions = NewFixtureAsset();
            try
            {
                var part = BindingQuery.QueryBinding(BindingDeviceClass.Pad, "<Gamepad>/leftStick/up", actions);
                Assert.That(part.Status, Is.EqualTo(BindingQueryStatus.Bound));
                Assert.That(part.IconKey, Is.EqualTo("Pad.LeftStick_Up"),
                    "part 路径键名 = 设备内各段首字母大写以 _ 连接");

                var composite = BindingQuery.QueryBinding(BindingDeviceClass.Pad, "2DVector", actions);
                Assert.That(composite.Status, Is.EqualTo(BindingQueryStatus.NotBound),
                    "复合本体无设备前缀 ⇒ 不属 Pad 类(查询按类全等,不猜)");
            }
            finally { UnityEngine.Object.DestroyImmediate(actions); }
        }

        /// <summary>D1 Xr 列:tag 路径 {PrimaryAction} 命中 ⇒ device=Xr、iconKey="Xr.PrimaryAction"
        /// (GDD 设备列闭集三值 Kbd/Pad/Xr 的第三值;真机绑重承 InputSystem_Actions 的 XR 组形态)。</summary>
        [Test]
        public void test_queryBinding_xrTagPath_resolvesXrClass()
        {
            var actions = NewFixtureAsset();
            try
            {
                var r = BindingQuery.QueryBinding(BindingDeviceClass.Xr, "<XRController>/{PrimaryAction}", actions);
                Assert.That(r.Status, Is.EqualTo(BindingQueryStatus.Bound));
                Assert.That(r.Device, Is.EqualTo(BindingDeviceClass.Xr));
                Assert.That(r.IconKey, Is.EqualTo("Xr.PrimaryAction"));
            }
            finally { UnityEngine.Object.DestroyImmediate(actions); }
        }

        // ═══════════════════ AC-3-D2 · 无素材、无时机(构建报告 + 类型断言)═══════════════════

        /// <summary>D2 面 1(构建报告断言 · 真产物):Gameplay.Input.dll 的 TypeRef 表
        /// 无 Sprite/Texture/Font 族 —— **编译产物元数据**判据,非 grep 源码(AC 原文)。
        /// 面 1b 点射(qa-008-6):同谓词跑 Presentation 四交付物类型全名 ——
        /// 证明扫描面覆盖住 Gameplay.Presentation 程序集(素材面曾只扫 Input dll 的洞)。</summary>
        [Test]
        public void test_assetTypeRefsIl_realProduct_zeroErrors()
        {
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False,
                "跑 D2 前提:编译成功(读上一版 DLL = 假绿)");
            var dll = AssemblyGates.ScriptAssemblyPath(InputBoundaryGates.InputAssemblyName);
            var errs = InputBoundaryGates.CheckAssetTypeRefsIl(dll, out var refs);
            Assert.That(errs, Is.Empty, () => "D2 素材面红行:\n" + string.Join("\n", errs));
            Assert.That(refs, Is.GreaterThan(0), "TypeRef 表非空(空表 = 扫描面丢失)");
            Assert.That(refs, Is.GreaterThan(10), "Gameplay.Input 对引擎类型的真实依赖数应远超 10(自证扫描面在跑)");

            // 面 1b 点射正例:Presentation 四类型真产物零红 + 四名全命中
            var presDll = AssemblyGates.ScriptAssemblyPath(InputBoundaryGates.PresentationAssemblyName);
            var e1b = InputBoundaryGates.CheckNamedTypeAssetRefsIl(
                presDll, InputBoundaryGates.PresentationDeliveredTypeNames, out var matched1b);
            Assert.That(e1b, Is.Empty, () => "D2 点射面(1b)红行:\n" + string.Join("\n", e1b));
            Assert.That(matched1b, Is.EqualTo(InputBoundaryGates.PresentationDeliveredTypeNames.Length),
                "点射名单四类型必须全在 Presentation 产物中找到(落空 = 名字漂移假绿)");
        }

        /// <summary>D2 面 1 负例自证:对**本测试装配**产物跑同一谓词 ——
        /// SpriteBearingFixture 让 TypeRef 表真出现 UnityEngine.Sprite/Texture2D/Font ⇒ 必红
        /// (承音频 IlScanNegativeFixture 先例:负例走真 IL,不走合成文本)。
        /// 同产物里 AssetPathInStringConstFixture 的 "Assets/.../kbd_e.png" 常量字符串
        /// **不得**触发红(QA Edge「按资产引用扫,不按源码字面扫」—— 字符串值不是 TypeRef)。
        /// 面 1b 点射负例(qa-008-6):同谓词按类型全名点射夹具 ⇒ 三族各点名 + 名字落空显红。</summary>
        [Test]
        public void test_assetTypeRefsIl_spriteBearingTestProduct_reportsRed()
        {
            var dll = AssemblyGates.ScriptAssemblyPath("Sim.Contracts.Tests");
            Assert.That(File.Exists(dll), Is.True, $"测试装配产物缺失:{dll}");

            var errs = InputBoundaryGates.CheckAssetTypeRefsIl(dll, out _);

            Assert.That(errs.Any(e => e.Contains("UnityEngine.Sprite")), Is.True,
                "Sprite 字段必红(真 IL TypeRef,AC-3-D2)");
            Assert.That(errs.Any(e => e.Contains("UnityEngine.Texture2D")), Is.True,
                "Texture2D 字段必红(黑名单拼写自证,qa-008-5)");
            Assert.That(errs.Any(e => e.Contains("UnityEngine.Font")), Is.True,
                "Font 字段必红(黑名单拼写自证,qa-008-5)");
            Assert.That(errs.Any(e => e.Contains("kbd_e.png")), Is.False,
                "字符串常量里的素材路径不得触发 TypeRef 面(D2 QA Edge:键名/常量不约束)");

            // 面 1b 点射负例:对夹具类型按全名点射(点射函数自身三族抓取面自证)
            var e1b = InputBoundaryGates.CheckNamedTypeAssetRefsIl(
                dll, new[] { typeof(SpriteBearingFixture).FullName }, out var matched1b);
            Assert.That(matched1b, Is.EqualTo(1), "点射名单类型必须命中(落空 = 假绿面)");
            Assert.That(e1b.Any(e => e.Contains("UnityEngine.Sprite")), Is.True, "点射面抓 Sprite");
            Assert.That(e1b.Any(e => e.Contains("UnityEngine.Texture2D")), Is.True, "点射面抓 Texture2D");
            Assert.That(e1b.Any(e => e.Contains("UnityEngine.Font")), Is.True, "点射面抓 Font");

            // 点射面假绿护栏:Presentation 产物里点射夹具名(不在)⇒ 扫描面丢失显红
            var eMiss = InputBoundaryGates.CheckNamedTypeAssetRefsIl(
                AssemblyGates.ScriptAssemblyPath(InputBoundaryGates.PresentationAssemblyName),
                new[] { typeof(SpriteBearingFixture).FullName }, out var mMiss);
            Assert.That(mMiss, Is.Zero);
            Assert.That(eMiss.Any(e => e.Contains("扫描面丢失")), Is.True,
                "点射名字落空必须显红(类型被改名/移除 = 假绿面)");
        }

        /// <summary>D2 面 2(类型断言 · reflection 面):真交付物 BindingQueryResult 实例字段集
        /// 恰 = {Status, Device, BindingPath, IconKey},类型全在白名单内 ⇒ 零红。</summary>
        [Test]
        public void test_bindingResultFields_realType_zeroErrors()
        {
            var errs = InputBoundaryGates.CheckBindingResultFields(typeof(BindingQueryResult));
            Assert.That(errs, Is.Empty, () => "D2 字段面红行:\n" + string.Join("\n", errs));

            // 字段集**等于**白名单(双向:不多不少 —— 少字段也是漂移)
            var actual = typeof(BindingQueryResult)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal);
            Assert.That(actual, Is.EqualTo(InputBoundaryGates.BindingResultAllowedFieldNames
                       .OrderBy(n => n, StringComparer.Ordinal)));
        }

        /// <summary>D2 负例(QA 点名「返回类型加一个 float fadeDuration ⇒ 类型断言红」):
        /// 夹具类型喂 reflection 面 ⇒ 红行点名字段 + 浮点。</summary>
        [Test]
        public void test_bindingResultFields_fadeDurationFixture_reportsRed()
        {
            var errs = InputBoundaryGates.CheckBindingResultFields(typeof(BindingResultFadeDurationFixture));
            Assert.That(errs, Is.Not.Empty, "float FadeDuration 必红(时机字段禁入交付物)");
            Assert.That(errs.Any(e => e.Contains("FadeDuration")), Is.True, "红行须点名违例字段");
            Assert.That(errs.Any(e => e.Contains("Single") || e.Contains("float")), Is.True,
                "浮点面须可读出(别名同拒,B3 纪律)");
            Assert.That(errs.Any(e => e.Contains("IconKey")), Is.False,
                "合法字段不得连坐(红行可定位 = 让人照着改字段)");
        }

        /// <summary>D2 负例第二形态:可见性字段 bool Visible ⇒ 名字白名单外即红
        /// (AC-D2「无 visible/showAt/duration 类」逐类点名中的 visible 类)。</summary>
        [Test]
        public void test_bindingResultFields_visibleFixture_reportsRed()
        {
            var errs = InputBoundaryGates.CheckBindingResultFields(typeof(BindingResultVisibleFixture));
            Assert.That(errs.Any(e => e.Contains("Visible")), Is.True, "可见性字段必红");
        }

        /// <summary>D2 面 2 的 IL 负例(真产物红,不走合成):对本装配产物按全名点射夹具类型 ⇒
        /// 门在**编译产物元数据**上抓到 FadeDuration(构建期强制点成立的自证)。</summary>
        [Test]
        public void test_bindingResultFieldsIl_fadeDurationFixture_reportsRealIlRed()
        {
            var dll = AssemblyGates.ScriptAssemblyPath("Sim.Contracts.Tests");
            var errs = InputBoundaryGates.CheckBindingResultFieldsIl(
                dll, new[] { "DaYiJingCheng.Tests.Unit.InputSystem.BindingResultFadeDurationFixture" },
                out var matched);
            Assert.That(matched, Is.EqualTo(1), "夹具类型必须在产物中找到(落空 = 假绿面)");
            Assert.That(errs, Is.Not.Empty);
            Assert.That(errs.Any(e => e.Contains("FadeDuration")), Is.True, "IL 面红行点名 FadeDuration");
        }

        /// <summary>D2 两面共用假绿护栏:产物缺失 ⇒ 红 / 目标集空 ⇒ 红 / 名字全对不上 ⇒ matched=0 红。</summary>
        [Test]
        public void test_bindingResultGates_missingOrEmptyFaces_reportRed()
        {
            var e1 = InputBoundaryGates.CheckBindingResultFieldsIl(
                "/tmp/no-such-assembly-008.dll", new[] { InputBoundaryGates.BindingResultTypeName }, out var m1);
            Assert.That(e1, Has.Count.EqualTo(1));
            Assert.That(e1[0], Does.Contain("产物缺失"));
            Assert.That(m1, Is.Zero);

            var e2 = InputBoundaryGates.CheckBindingResultFieldsIl(
                AssemblyGates.ScriptAssemblyPath(InputBoundaryGates.InputAssemblyName),
                new string[0], out _);
            Assert.That(e2, Has.Count.EqualTo(1), "空目标集 = 拒以空集冒充绿");

            var e3 = InputBoundaryGates.CheckBindingResultFieldsIl(
                AssemblyGates.ScriptAssemblyPath(InputBoundaryGates.InputAssemblyName),
                new[] { "DaYiJingCheng.Gameplay.Input.NoSuchType008" }, out var m3);
            Assert.That(m3, Is.Zero);
            Assert.That(e3.Any(e => e.Contains("未找到")), Is.True, "扫描面丢失必须显红");

            var e4 = InputBoundaryGates.CheckAssetTypeRefsIl("/tmp/no-such-assembly-008.dll", out _);
            Assert.That(e4[0], Does.Contain("拒扫"));

            // 点射面(面 1b)的同族假绿护栏:缺产物拒扫 / 空名单拒以空集冒充绿
            var e5 = InputBoundaryGates.CheckNamedTypeAssetRefsIl(
                "/tmp/no-such-assembly-008.dll", new[] { "X.Y" }, out _);
            Assert.That(e5, Has.Count.EqualTo(1));
            Assert.That(e5[0], Does.Contain("点射面拒扫"));
            var e6 = InputBoundaryGates.CheckNamedTypeAssetRefsIl(
                AssemblyGates.ScriptAssemblyPath(InputBoundaryGates.InputAssemblyName), new string[0], out _);
            Assert.That(e6, Has.Count.EqualTo(1), "点射空名单 = 拒以空集冒充绿(qa-008-6)");

            Assert.That(InputBoundaryGates.CheckBindingResultFields(null)[0], Does.Contain("null"));
        }

        /// <summary>D2 端到端:RunAll(构建前门实走路径)含 D2 两面且真树绿
        /// (此断言 = AC 字面「构建报告断言」在本地构建路径上的预跑;构建失败强制点见
        /// AssemblyGates.BuildGate,本测只断判据当前树全绿)。</summary>
        [Test]
        public void test_runAll_currentTree_d2FacesZeroErrors()
        {
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False);
            var errs = InputBoundaryGates.RunAll(out var roots);
            Assert.That(errs, Is.Empty, () => "RunAll 红行:\n" + string.Join("\n", errs));
            Assert.That(roots, Is.GreaterThan(0));
        }

        // ═══════════════════ AC-3-D3 · 设备移除 → 合成 release ═══════════════════

        /// <summary>引擎事实钉(**前提实测**,批跑 #5 证伪原「闩残留卡键」假设):**不挂守卫**
        /// 时断连,press 闩由引擎自清 ⇒ IsPressed 转 false。机制:InputManager.RemoveDevice 先跑
        /// 引擎 InputActionState.OnDeviceChange(Removed)(:1603)→ 重解绑重时对 active control
        /// 失效的动作 ResetActionState(ResolveBindings else 分支);守卫 listener 后跑(:1629)。
        /// ⇒ 原「引擎不合成 release ⇒ 卡键」前提为假;守卫的保留理由改为「引擎自清非文档化
        /// 契约 + GDD/故事明文要求游戏侧合成 release」。若本测红 = 引擎不再自清 = 守卫真实
        /// 上岗(预期中的红,提醒重评,不是脆)。</summary>
        [Test]
        public void test_deviceRemoved_withoutGuard_engineSelfClearsPress_measured()
        {
            var actions = NewFixtureAsset();
            var gp = InputApi.AddDevice<Gamepad>();
            BeginPressedPrecondition();
            try
            {
                var map = actions.FindActionMap("Gameplay");
                map.Enable();
                var emergency = actions.FindAction("Emergency");
                // 队状态事件驱动按键(InputState.Change 对按钮位域控件抛
                // "Cannot change state of bitfield control" —— 实测限制)
                InputApi.QueueStateEvent(gp, new GamepadState().WithButton(GamepadButton.South));
                InputApi.Update();
                Assert.That(emergency.IsPressed(), Is.True, "前置:按住成立");

                InputApi.RemoveDevice(gp);

                Assert.That(emergency.IsPressed(), Is.False,
                    "引擎在 Removed 路径自清 press(批跑 #5 实测钉;原 engine-gap 前提证伪)—— 若红 = 引擎行为变了,守卫接管");
            }
            finally
            {
                EndPressedPrecondition();
                if (gp != null && gp.deviceId != InputDevice.InvalidDeviceId &&
                    InputApi.GetDevice<Gamepad>() != null)
                    InputApi.RemoveDevice(gp);
                UnityEngine.Object.DestroyImmediate(actions);
            }
        }

        /// <summary>D3 主路径(两段)。**① 守卫机制真身**:按住两键(两动作)后直驱公开接缝
        /// <see cref="DeviceReleaseGuard.ReleaseDeviceNow"/>(设备仍连)⇒ 同帧全 false +
        /// SyntheticReleaseCount 精确 = 2 —— layout 归属 / Reset / 仪表三合一被真正驱动
        /// (Removed 事件路径上引擎先于 listener 自清,守卫见不到 pressed,故机制只能这样驱)。
        /// **② AC 可观测面**:全量状态事件 0→1 重新按住后断连 ⇒ 同帧全部 IsPressed 转 false、
        /// 后续 Update 不复跳(引擎先清或守卫先跑两种次序都满足 AC —— 次序的实测钉在
        /// withoutGuard 测,故本段不强断言计数)。</summary>
        [Test]
        public void test_deviceRemoved_guardAttached_allPressedReleasedSameFrame()
        {
            var actions = NewFixtureAsset();
            var gp = InputApi.AddDevice<Gamepad>();
            var guard = new DeviceReleaseGuard();
            BeginPressedPrecondition();
            try
            {
                var map = actions.FindActionMap("Gameplay");
                map.Enable();
                var emergency = actions.FindAction("Emergency");
                var interact = actions.FindAction("Interact");
                guard.Attach();

                // 同一状态事件压两键(状态事件是全量替换 —— 拆两条会把前一键清掉)
                InputApi.QueueStateEvent(gp, new GamepadState()
                    .WithButton(GamepadButton.South).WithButton(GamepadButton.North));
                InputApi.Update();
                Assert.That(emergency.IsPressed(), Is.True, "前置:buttonSouth 按住");
                Assert.That(interact.IsPressed(), Is.True, "前置:buttonNorth 按住");

                // ① 守卫机制真身(设备仍连,脱离事件流直驱)
                int released = guard.ReleaseDeviceNow(gp);
                Assert.That(released, Is.EqualTo(2), "直驱:两动作皆归属该设备布局且按住 ⇒ 各 release 一次");
                Assert.That(guard.SyntheticReleaseCount, Is.EqualTo(2), "仪表:计数 = 真实 release 次数(非引擎顺手清)");
                Assert.That(emergency.IsPressed(), Is.False, "直驱后同帧读侧 false");
                Assert.That(interact.IsPressed(), Is.False, "「全部」不是「首个」");

                // ② 重新按住(全量替换:先归零使控件 0→1 重新闩住 —— 状态事件只在变化时触发监视器)
                InputApi.QueueStateEvent(gp, new GamepadState());
                InputApi.Update();
                InputApi.QueueStateEvent(gp, new GamepadState()
                    .WithButton(GamepadButton.South).WithButton(GamepadButton.North));
                InputApi.Update();
                Assert.That(emergency.IsPressed(), Is.True, "重按前置:buttonSouth 按住");
                Assert.That(interact.IsPressed(), Is.True, "重按前置:buttonNorth 按住");

                InputApi.RemoveDevice(gp);   // Removed 回调内同步(引擎 :1603 / 守卫 :1629)

                Assert.That(emergency.IsPressed(), Is.False, "断连同帧:全部 IsPressed 转 false(AC-3-D3)");
                Assert.That(interact.IsPressed(), Is.False, "多动作逐个释放(「全部」不是「首个」)");

                InputApi.Update();
                InputApi.Update();
                Assert.That(emergency.IsPressed(), Is.False, "后续帧不复跳 true(无卡键回声)");
                Assert.That(interact.IsPressed(), Is.False);
                gp = null;   // 已移除,finally 不再重复
            }
            finally
            {
                EndPressedPrecondition();
                guard.Detach();
                if (gp != null) InputApi.RemoveDevice(gp);
                UnityEngine.Object.DestroyImmediate(actions);
            }
        }

        /// <summary>D3 Edge「断连时正处于 composite 按住(一半)」:2DVector 复合只按 up 一半 ⇒
        /// 动作 pressed;**守卫机制段**直驱 <see cref="DeviceReleaseGuard.ReleaseDeviceNow"/>
        /// ⇒ 同帧释放 + 计数 1(复合 part 路径的 layout 归属在此被真正驱动 —— Removed 事件
        /// 路径上引擎先自清,守卫见不到 pressed);**断连段**复验读侧保持 false、无回声。</summary>
        [Test]
        public void test_deviceRemoved_compositeHalfPressed_released()
        {
            var actions = NewFixtureAsset();
            var gp = InputApi.AddDevice<Gamepad>();
            var guard = new DeviceReleaseGuard();
            BeginPressedPrecondition();
            try
            {
                var map = actions.FindActionMap("Gameplay");
                map.Enable();
                var move = actions.FindAction("Move2D");
                guard.Attach();

                // 复合只吃进一半:仅 up 轴满偏(down 归零)—— 队状态事件(位域 Change 不可用)
                InputApi.QueueStateEvent(gp, new GamepadState { leftStick = new UnityEngine.Vector2(0f, 1f) });
                InputApi.Update();
                Assert.That(move.IsPressed(), Is.True, "前置:半按即 pressed(EvaluateMagnitude 满幅)");

                // 守卫机制段(设备仍连):复合 part 路径归属命中 ⇒ release 一次
                int released = guard.ReleaseDeviceNow(gp);
                Assert.That(released, Is.EqualTo(1), "直驱:composite 半按同样被守卫释放(QA Edge)");
                Assert.That(guard.SyntheticReleaseCount, Is.EqualTo(1));
                Assert.That(move.IsPressed(), Is.False, "直驱后同帧读侧 false");

                // 断连段:读侧保持 false、无回声、计数不再涨(引擎先清,守卫幂等)
                InputApi.RemoveDevice(gp);
                Assert.That(move.IsPressed(), Is.False, "断连后仍 false(AC-3-D3 可观测面)");
                InputApi.Update();
                InputApi.Update();
                Assert.That(move.IsPressed(), Is.False, "后续帧不复跳");
                Assert.That(guard.SyntheticReleaseCount, Is.EqualTo(1), "Removed 路径引擎先自清 ⇒ 守卫不再加分(次序钉)");
                gp = null;
            }
            finally
            {
                EndPressedPrecondition();
                guard.Detach();
                if (gp != null) InputApi.RemoveDevice(gp);
                UnityEngine.Object.DestroyImmediate(actions);
            }
        }

        /// <summary>D3 Edge「无按住键的设备断连」= 无操作、不抛、仪表不动。</summary>
        [Test]
        public void test_deviceRemoved_nothingPressed_noOpNoThrow()
        {
            var actions = NewFixtureAsset();
            var gp = InputApi.AddDevice<Gamepad>();
            var guard = new DeviceReleaseGuard();
            try
            {
                actions.FindActionMap("Gameplay").Enable();
                guard.Attach();

                Assert.DoesNotThrow(() => InputApi.RemoveDevice(gp));
                Assert.That(guard.SyntheticReleaseCount, Is.Zero, "没按任何键 ⇒ 一条 release 都不发");
                gp = null;
            }
            finally
            {
                guard.Detach();
                if (gp != null) InputApi.RemoveDevice(gp);
                UnityEngine.Object.DestroyImmediate(actions);
            }
        }

        /// <summary>D3 机制面(qa-008-1):真实 Removed 事件下守卫与引擎都跑、post-fix 结构性
        /// 不可分辨 ⇒ 用反射直驱私有 <c>OnDeviceChange</c>,钉 handler 接线与逐 change 语义:
        /// Added 忽略(热插拔新增不动 release)/ Disconnected 与 Removed 驱动合成 release +
        /// IsAttached 在场。若本测红 = handler 改名/改签名/丢接线 —— 守卫对真实事件的
        /// 订阅面在此被直接盯住,不再依赖引擎次序的间接推断。</summary>
        [Test]
        public void test_deviceGuard_onDeviceChange_directDriven_semanticsPerChange()
        {
            var actions = NewFixtureAsset();
            var gp = InputApi.AddDevice<Gamepad>();
            var guard = new DeviceReleaseGuard();
            BeginPressedPrecondition();
            try
            {
                var map = actions.FindActionMap("Gameplay");
                map.Enable();
                var emergency = actions.FindAction("Emergency");
                var interact = actions.FindAction("Interact");

                var handler = typeof(DeviceReleaseGuard).GetMethod(
                    "OnDeviceChange", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(handler, Is.Not.Null, "OnDeviceChange 必须存在(改名/改签名 = 订阅面丢)");

                guard.Attach();
                Assert.That(guard.IsAttached, Is.True, "Attach 后 IsAttached 为真(qa-008-1)");

                InputApi.QueueStateEvent(gp, new GamepadState()
                    .WithButton(GamepadButton.South).WithButton(GamepadButton.North));
                InputApi.Update();
                Assert.That(emergency.IsPressed(), Is.True, "前置:按住");

                // Added:热插拔新增路径不得触 release
                handler.Invoke(guard, new object[] { gp, InputDeviceChange.Added });
                Assert.That(emergency.IsPressed(), Is.True, "Added 不释放(设备刚接入,没人按它时也无副作用)");
                Assert.That(guard.SyntheticReleaseCount, Is.Zero, "Added 路径计数不动");

                // Disconnected:终态 change 驱动合成 release
                handler.Invoke(guard, new object[] { gp, InputDeviceChange.Disconnected });
                Assert.That(emergency.IsPressed(), Is.False, "Disconnected 驱动释放");
                Assert.That(interact.IsPressed(), Is.False, "同帧全部(非首个)");
                Assert.That(guard.SyntheticReleaseCount, Is.EqualTo(2), "两动作各一次");

                // 重按后再驱 Removed:同一 handler 第二终态同样释放
                InputApi.QueueStateEvent(gp, new GamepadState());
                InputApi.Update();
                InputApi.QueueStateEvent(gp, new GamepadState()
                    .WithButton(GamepadButton.South).WithButton(GamepadButton.North));
                InputApi.Update();
                Assert.That(emergency.IsPressed(), Is.True, "重按前置");

                handler.Invoke(guard, new object[] { gp, InputDeviceChange.Removed });
                Assert.That(emergency.IsPressed(), Is.False, "Removed 驱动释放(与 Disconnected 同族终态)");
                Assert.That(guard.SyntheticReleaseCount, Is.EqualTo(4), "四次 = 两次按住 × 两终态(直驱,不经引擎次序)");
            }
            finally
            {
                EndPressedPrecondition();
                guard.Detach();
                if (gp != null && gp.deviceId != InputDevice.InvalidDeviceId)
                    InputApi.RemoveDevice(gp);
                UnityEngine.Object.DestroyImmediate(actions);
            }
        }

        /// <summary>us-008-1 回归钉(跨设备过释放):**键盘按住 Interact + 移除闲置手柄** ⇒
        /// 守卫计数 0、动作未被 cancel、activeControl 仍是键盘(三面排除「守卫越权清」)。
        /// 两级判据的运行期面 —— 静态面 ActionBoundTo 对 &lt;Keyboard&gt;/e
        /// 命中(Interact 双绑),但 activeControl 归属 keyboard ⇒ 不越权清。
        /// 若本测红 = activeControl 判据丢了(守卫退回静态面 ⇒ 掉按净回归)。
        /// ⚠️ 引擎事实(源码实锤 + 批跑取证,com.unity.inputsystem@7a4e1a2a8194):
        /// 设备增删触发重解绑 ⇒ actionStates 由 InputBindingResolver 重建(isPressed 默认 false),
        /// RestoreActionStatesAfterReResolvingBindings 回写清单**不含 isPressed**,
        /// OnBeforeInitialUpdate 又跳过 IsActiveControl ⇒ 任意在跑动作的 IsPressed() 闩
        /// 在重解绑瞬间丢失且不可自愈(phase / activeControl 保留)。
        /// 故存活判据 = phase + activeControl + 守卫计数三面,**不断言 RemoveDevice 后的 IsPressed**。</summary>
        [Test]
        public void test_deviceRemoved_otherDeviceHeld_pressSurvives()
        {
            var actions = NewFixtureAsset();
            bool addedKbd = false;
            var kbd = Keyboard.current;
            if (kbd == null) { kbd = InputApi.AddDevice<Keyboard>(); addedKbd = true; }
            var gp = InputApi.AddDevice<Gamepad>();   // 闲置手柄(全程不按)
            var guard = new DeviceReleaseGuard();
            BeginPressedPrecondition();
            try
            {
                var map = actions.FindActionMap("Gameplay");
                map.Enable();
                var interact = actions.FindAction("Interact");

                // 键盘按住 E(KeyboardState 全量状态事件;按钮位域 Change 不可用的同族姿势)
                InputApi.QueueStateEvent(kbd, new KeyboardState(Key.E));
                InputApi.Update();
                Assert.That(interact.IsPressed(), Is.True, "前置:键盘按住成立");

                guard.Attach();

                // ① 直驱:静态面命中(gp 在 Interact 的绑定里)但按住来源 = keyboard ⇒ 0 释放
                int released = guard.ReleaseDeviceNow(gp);
                Assert.That(released, Is.Zero, "直驱:按住来自键盘 ⇒ 手柄移除不越权清(us-008-1)");
                Assert.That(guard.SyntheticReleaseCount, Is.Zero, "计数不动(真掉按的回归面)");
                Assert.That(interact.IsPressed(), Is.True, "直驱后按住存活");

                // ② 真实 Removed 事件:守卫判据的运行期面 —— 键盘按住不被守卫所清
                InputApi.RemoveDevice(gp);
                var acAfter = interact.activeControl;
                var diag = $"phase={interact.phase}, activeControl={(acAfter == null ? "null" : acAfter.name + "@" + acAfter.device?.layout)}, guardReleased={guard.SyntheticReleaseCount}";
                // us-008-1 核心面(三面排除「守卫越权清」):计数 0 + 动作未被 cancel/reset
                // (守卫 Reset 会把 phase 打回 Waiting、controlIndex 打成 invalid)+ activeControl 仍是键盘。
                Assert.That(guard.SyntheticReleaseCount, Is.Zero, "Released 计数仍 0(不是他设备的 release); " + diag);
                Assert.That(interact.phase, Is.EqualTo(InputActionPhase.Performed), "动作未被 cancel/reset(守卫没 Reset 它); " + diag);
                Assert.That(acAfter, Is.Not.Null, "activeControl 保留(按住来源可归属); " + diag);
                Assert.That(acAfter.device, Is.SameAs(kbd), "activeControl 仍是键盘(AC-D3「全部」按设备归属); " + diag);
                // 后续两帧不回声掉落(phase / activeControl 面)。
                // ⚠️ 引擎事实(源码 + 批跑 #7 实测,com.unity.inputsystem@7a4e1a2a8194):
                // 设备增删触发重解绑 ⇒ actionStates 由 InputBindingResolver 重建(isPressed 默认 false),
                // 而 RestoreActionStatesAfterReResolvingBindings 回写字段清单**不含 isPressed**
                // ⇒ 任意在跑动作的 IsPressed() 闩在重解绑瞬间丢失(phase / activeControl 保留),
                // 且 OnBeforeInitialUpdate 跳过 IsActiveControl ⇒ 不会自行重建,须重新按下才恢复。
                // 这是引擎行为、非守卫所为(上面三面已证);**本测不断言 RemoveDevice 后的 IsPressed**。
                InputApi.Update();
                InputApi.Update();
                var acLater = interact.activeControl;
                Assert.That(interact.phase, Is.EqualTo(InputActionPhase.Performed), "后续帧 phase 不回声掉落; " + diag);
                Assert.That(acLater, Is.Not.Null, "后续帧 activeControl 不回声掉落; " + diag);
                Assert.That(acLater.device, Is.SameAs(kbd), "后续帧 activeControl 归属仍键盘; " + diag);
                gp = null;
            }
            finally
            {
                EndPressedPrecondition();
                guard.Detach();
                // 键盘复位(清 E,防按住闩跨测泄漏)
                InputApi.QueueStateEvent(kbd, new KeyboardState());
                InputApi.Update();
                if (gp != null && gp.deviceId != InputDevice.InvalidDeviceId) InputApi.RemoveDevice(gp);
                if (addedKbd) InputApi.RemoveDevice(kbd);
                UnityEngine.Object.DestroyImmediate(actions);
            }
        }

        /// <summary>状态侧热插拔(qa-008-3):Attach **之后**新增的设备只能经 Added handler 进
        /// 在场集(Attach 快照不含它)⇒ 加 XRController 后 State 必须转 XrActive(XR 优先,
        /// 与环境有无手柄无关),摘除后回基线。若 Added 路径零执行(本测的原发现)⇒
        /// State 停留基线、XrActive 不出现,本测红。</summary>
        [Test]
        public void test_deviceStateManager_hotplugAdded_afterAttach_recalculates()
        {
            var mgr = NewManagerAttached(out var addedKbd, out var addedMouse);
            var baseline = mgr.State;
            InputDevice xr = null;
            try
            {
                Assert.That(mgr.IsAttached, Is.True, "前置:已挂接");

                xr = InputApi.AddDevice("XRController");   // Attach 之后新增 ⇒ 只有 handler 能看到

                Assert.That(mgr.State, Is.EqualTo(DeviceState.XrActive),
                    "热插拔 Added 必须重算在场集(qa-008-3:该路径曾零执行)");

                InputApi.RemoveDevice(xr);
                xr = null;
                Assert.That(mgr.State, Is.EqualTo(baseline), "摘除后回基线(Removed 路径复验)");
            }
            finally
            {
                mgr.Detach();
                if (xr != null) InputApi.RemoveDevice(xr);
                CleanupAdded(addedKbd, addedMouse);
            }
        }

        /// <summary>D3 Edge(状态侧)「断连的设备是当前 Mixed 源」⇒ 来源迁移到存活家族
        /// (迁移不经迟滞 —— 见 DeviceStateManager 类头;与 pressed 释放互补)。
        /// 环境前置(qa-008-5):断言「只剩键鼠」要求环境无其他手柄 —— 有则显式跳过(假红改
        /// 自解释失配);超算批跑环境无物理手柄,本测照常执行。</summary>
        [Test]
        public void test_mixedActiveSourceDisconnected_stateMigratesToSurvivor()
        {
            if (InputApi.GetDevice<Gamepad>() != null)
                Assert.Ignore("环境已有物理手柄 ⇒ 「移除现源后只剩键鼠」前提失配(qa-008-5:显式跳过,不假红)");
            var gp = InputApi.AddDevice<Gamepad>();
            var mgr = NewManagerAttached(out var addedKbd, out var addedMouse);
            try
            {
                Assert.That(mgr.State, Is.EqualTo(DeviceState.Mixed), "键鼠+手柄在联 = Mixed");
                Assert.That(mgr.ObserveDigital(DeviceSourceKind.Kbm), Is.True, "无 incumbent:有效输入即成源");
                Assert.That(mgr.ActiveSource, Is.EqualTo(DeviceSourceKind.Kbm));

                // Pad 夺源(三条件全满足)→ incumbent = Pad
                ForceSwitchToPad(mgr);
                Assert.That(mgr.ActiveSource, Is.EqualTo(DeviceSourceKind.Pad), "前置:Pad 已成源");

                InputApi.RemoveDevice(gp);   // 断连的正是现源

                Assert.That(mgr.ActiveSource, Is.EqualTo(DeviceSourceKind.Kbm),
                    "现源离场 ⇒ 迁移到存活家族(QA Edge)");
                Assert.That(mgr.State, Is.EqualTo(DeviceState.KbmOnly));
                gp = null;
            }
            finally
            {
                mgr.Detach();
                if (gp != null) InputApi.RemoveDevice(gp);
                CleanupAdded(addedKbd, addedMouse);
            }
        }

        // ── 设备在场保障辅助(EditMode 基线 = 编辑器自带键盘/鼠标,不假设其存在也不假设其 absence)──

        /// <summary>确保键鼠家族在场(编辑器环境若天然有则复用,无则补加并在 finally 摘掉),
        /// 再挂 DeviceStateManager(Attach 快照 InputSystem.devices)。</summary>
        private static DeviceStateManager NewManagerAttached(out Keyboard addedKbd, out Mouse addedMouse)
        {
            addedKbd = Keyboard.current == null ? InputApi.AddDevice<Keyboard>() : null;
            addedMouse = Mouse.current == null ? InputApi.AddDevice<Mouse>() : null;
            var mgr = new DeviceStateManager(AxisThreshold, AxisTolerance, AxisDwell,
                                             PointerThreshold, PointerTolerance, PointerDwell);
            mgr.Attach();
            return mgr;
        }

        private static void CleanupAdded(Keyboard kbd, Mouse mouse)
        {
            if (kbd != null) InputApi.RemoveDevice(kbd);
            if (mouse != null) InputApi.RemoveDevice(mouse);
        }

        /// <summary>把 Pad 三条件打满(轴列:满幅 > threshold/tolerance,持续 > dwell tick)。</summary>
        private static void ForceSwitchToPad(DeviceStateManager mgr)
        {
            for (int i = 0; i <= AxisDwell + 1; i++)
                if (mgr.ObserveAxis(DeviceSourceKind.Pad, 50000, 0)) return;   // 50000/65536 ≈ 0.76 > 1/2
            Assert.Fail("ForceSwitchToPad:未夺源(前置失配)");
        }

        // ═══════════════════ §States 一 · 设备态五值与最近有效输入 ═══════════════════

        /// <summary>五态推导(纯函数 DeriveState 全 8 组合枚举一遍 —— 表驱动自证无空格)。</summary>
        [Test]
        public void test_deriveState_allCombinations_matchStatesTable()
        {
            Assert.That(DeviceStateManager.DeriveState(false, false, false), Is.EqualTo(DeviceState.NoDevice));
            Assert.That(DeviceStateManager.DeriveState(true, false, false), Is.EqualTo(DeviceState.KbmOnly));
            Assert.That(DeviceStateManager.DeriveState(false, true, false), Is.EqualTo(DeviceState.PadOnly));
            Assert.That(DeviceStateManager.DeriveState(true, true, false), Is.EqualTo(DeviceState.Mixed));
            Assert.That(DeviceStateManager.DeriveState(false, false, true), Is.EqualTo(DeviceState.XrActive));
            Assert.That(DeviceStateManager.DeriveState(true, true, true), Is.EqualTo(DeviceState.XrActive),
                "XR 优先(§States 一 XrActive 行语义;登记注记见类头)");
            Assert.That(DeviceStateManager.DeriveState(false, true, true), Is.EqualTo(DeviceState.XrActive));
            Assert.That(DeviceStateManager.DeriveState(true, false, true), Is.EqualTo(DeviceState.XrActive));
        }

        /// <summary>家族映射:Keyboard/Mouse→Kbm · Gamepad→Pad · XRController 布局→Xr ·
        /// 未知布局→None(不计入在场)。</summary>
        [Test]
        public void test_familyOf_deviceMapping_fiveStatesInputColumns()
        {
            // 自造设备必须自清 —— 此处漏摘 gamepad 会让后续 Mixed 测试在 `_present`
            // 里看到幽灵 Pad(跨测试污染,曾致 source 迁移用例红)。
            var kbd = Keyboard.current;
            bool addedKbd = false;
            if (kbd == null) { kbd = InputApi.AddDevice<Keyboard>(); addedKbd = true; }
            var gp = InputApi.GetDevice<Gamepad>();
            bool addedGp = false;
            if (gp == null) { gp = InputApi.AddDevice<Gamepad>(); addedGp = true; }
            try
            {
                Assert.That(DeviceStateManager.FamilyOf(kbd), Is.EqualTo(DeviceSourceKind.Kbm));
                Assert.That(DeviceStateManager.FamilyOf(gp), Is.EqualTo(DeviceSourceKind.Pad));
                var xr = InputApi.AddDevice("XRController");
                try
                {
                    Assert.That(DeviceStateManager.FamilyOf(xr), Is.EqualTo(DeviceSourceKind.Xr),
                        "XRController 布局在联 = Xr 家族(P0 近似,真 session 接线归 VR 轮)");
                }
                finally { InputApi.RemoveDevice(xr); }
                Assert.That(DeviceStateManager.FamilyOf(null), Is.EqualTo(DeviceSourceKind.None));
            }
            finally
            {
                if (addedGp) InputApi.RemoveDevice(gp);
                if (addedKbd) InputApi.RemoveDevice(kbd);
            }
        }

        // ═══════════════════ AC-3-D4 · Mixed 迟滞三条件 ═══════════════════

        /// <summary>D4 黄金例(AC 点名 EditMode 用例):来源 = Kbm,鼠标静止(指针列 0)+
        /// 摇杆漂移在 DRIFT_TOLERANCE 内,持续远超 DWELL ⇒ 来源**保持** Kbm。
        /// 口径订正(qa-008-2):mag=7000 同时低于 threshold(32768)⇒ 条件① 已挡,
        /// 单删条件②(容差)本测也不会切 —— 原写「删迟滞本测即红」不成立。
        /// 本测钉黄金场景行为面;条件② 的**独立执法**由反旋钮测(阈 < 容)承担。</summary>
        [Test]
        public void test_mixedGolden_mouseStill_plusStickDriftWithinTolerance_staysKbm()
        {
            var gp = InputApi.AddDevice<Gamepad>();
            var mgr = NewManagerAttached(out var addedKbd, out var addedMouse);
            try
            {
                Assert.That(mgr.State, Is.EqualTo(DeviceState.Mixed));
                Assert.That(mgr.ObserveDigital(DeviceSourceKind.Kbm), Is.True);

                // 漂移向量幅值 = ‖(0, 7000)‖ = 7000 < AxisTolerance(8192),喂 50 tick(≫ dwell 4)
                for (int i = 0; i < 50; i++)
                {
                    mgr.ObservePointer(0, 0);                    // 鼠标完全静止
                    mgr.ObserveAxis(DeviceSourceKind.Pad, 0, 7000);
                }

                Assert.That(mgr.ActiveSource, Is.EqualTo(DeviceSourceKind.Kbm),
                    "漂移不夺源(AC-3-D4 黄金例;来源保持 Kbm,不抖切到 Pad)");
            }
            finally
            {
                mgr.Detach();
                InputApi.RemoveDevice(gp);
                CleanupAdded(addedKbd, addedMouse);
            }
        }

        /// <summary>D4 条件② 反旋钮独立执法(qa-008-2):构造**阈 < 容**(反相关旋钮 ——
        /// 区间断言拒收的形态,只活在测试构造里)使条件① 满足而条件②不满足 ——
        /// mag=16384:① 16384 &gt; 8192 ✓ ② 16384 &lt; 32768 ✗ ⇒ 喂满 dwell+2 仍不切
        /// (证明条件② 各表其责,不依附条件①)。对照半:同旋钮喂 40000(①②全满足)
        /// ⇒ 恰在 dwell+1 切 —— 自证 harness 能看见切(否则「不切」可能只是 harness 坏)。</summary>
        [Test]
        public void test_hysteresis_toleranceAboveThreshold_condition2EnforcedIndependently()
        {
            var h = new DeviceSwitchHysteresis(
                axisThresholdQ16: 8192, axisDriftToleranceQ16: 32768, axisDwellTicks: AxisDwell,
                pointerThresholdQ16: 8192, pointerDriftToleranceQ16: 32768, pointerDwellTicks: PointerDwell);

            // 反旋钮半:① 满足、② 不满足 ⇒ 恒不切(条件② 独立挡下)
            for (int i = 0; i < AxisDwell + 2; i++)
                Assert.That(h.Observe(DeviceSourceKind.Kbm, DeviceSourceKind.Pad, 16384, 0, axisColumn: true),
                    Is.False, $"第 {i} tick:①✓②✗ ⇒ 条件② 独立执法,不得切(qa-008-2)");

            // 对照半:同旋钮 40000 > 32768 ⇒ ①② 全满足,恰在 dwell+1 切(harness 自证)
            for (int i = 1; i <= AxisDwell; i++)
                Assert.That(h.Observe(DeviceSourceKind.Kbm, DeviceSourceKind.Pad, 40000, 0, axisColumn: true),
                    Is.False, $"对照半第 {i}/{AxisDwell} tick:持满前不切");
            Assert.That(h.Observe(DeviceSourceKind.Kbm, DeviceSourceKind.Pad, 40000, 0, axisColumn: true),
                Is.True, "对照半 dwell+1:①②③ 全满足 ⇒ 切(harness 可见切面)");
        }

        /// <summary>D4 条件①单独执法:幅值过容差但**不过** DEVICE_SWITCH_THRESHOLD ⇒ 不切
        /// (‖Δ‖=20000:8192 < 20000 < 32768;逐 tick 喂满 dwell+1 仍不切)。</summary>
        [Test]
        public void test_hysteresis_aboveToleranceBelowThreshold_noSwitch()
        {
            var h = NewHysteresis();
            for (int i = 0; i < AxisDwell + 2; i++)
                Assert.That(h.Observe(DeviceSourceKind.Kbm, DeviceSourceKind.Pad, 20000, 0, axisColumn: true),
                    Is.False, $"第 {i} tick:条件① 未满足即不得切");
        }

        /// <summary>D4 Edge「漂移幅值恰 = DRIFT_TOLERANCE(边界,应不切)」:三条件全用**严格大于**
        /// (口径 = story AC 原文「> / 超过」,与旋钮表一致 —— 此断言把开闭侧钉死)。</summary>
        [Test]
        public void test_hysteresis_magExactlyEqualsTolerance_noSwitch()
        {
            var h = NewHysteresis();
            // ‖(0, 8192)‖ = 恰 = DRIFT_TOLERANCE;且 8192 < threshold ⇒ 双不满足
            for (int i = 0; i < AxisDwell + 2; i++)
                Assert.That(h.Observe(DeviceSourceKind.Kbm, DeviceSourceKind.Pad, 0, AxisTolerance, axisColumn: true),
                    Is.False, "恰等容差 = 不超出(严格大于)");

            // 边界另一侧:恰 = THRESHOLD 且超容差(‖(0,32768)‖=恰 threshold)⇒ 条件① 严格大于仍不满足
            for (int i = 0; i < AxisDwell + 2; i++)
                Assert.That(h.Observe(DeviceSourceKind.Kbm, DeviceSourceKind.Pad, 0, AxisThreshold, axisColumn: true),
                    Is.False, "恰等夺源阈 = 不超过(严格大于)");
        }

        /// <summary>D4 Edge「持续时长恰 = DWELL_DURATION」:恰好 dwell 个达标 tick ⇒ **不切**
        /// (「持续 … 以上」= 严格大于,与条件①②同侧开闭 —— 测试内明写口径,承 story QA 要求);
        /// 第 dwell+1 个 ⇒ 切。</summary>
        [Test]
        public void test_hysteresis_durationExactlyEqualsDwell_noSwitch_untilOnePast()
        {
            var h = NewHysteresis();
            for (int i = 1; i <= AxisDwell; i++)
                Assert.That(h.Observe(DeviceSourceKind.Kbm, DeviceSourceKind.Pad, 50000, 0, axisColumn: true),
                    Is.False, $"第 {i}/{AxisDwell} 个达标 tick:恰 = DWELL 仍不切(严格大于口径)");
            Assert.That(h.Observe(DeviceSourceKind.Kbm, DeviceSourceKind.Pad, 50000, 0, axisColumn: true),
                Is.True, "dwell+1 tick:三条件同刻全满足 ⇒ 夺源");
        }

        /// <summary>D4 迟滞的「中断即重来」:达标 3 tick 后断一拍(条件②失守),再续 dwell 拍 ⇒
        /// 恰在断点后的 dwell+1 才切(漏桶/积分器实现会提前切 ⇒ 此处红)。</summary>
        [Test]
        public void test_hysteresis_streakInterrupted_restartsFromZero()
        {
            var h = NewHysteresis();
            Assert.That(h.Observe(DeviceSourceKind.Kbm, DeviceSourceKind.Pad, 50000, 0, axisColumn: true), Is.False);
            Assert.That(h.Observe(DeviceSourceKind.Kbm, DeviceSourceKind.Pad, 50000, 0, axisColumn: true), Is.False);
            Assert.That(h.Observe(DeviceSourceKind.Kbm, DeviceSourceKind.Pad, 1000, 0, axisColumn: true), Is.False); // 断
            for (int i = 1; i <= AxisDwell; i++)
                Assert.That(h.Observe(DeviceSourceKind.Kbm, DeviceSourceKind.Pad, 50000, 0, axisColumn: true),
                    Is.False, $"断点后第 {i} 拍恰 = 旧计数,须从起重计");
            Assert.That(h.Observe(DeviceSourceKind.Kbm, DeviceSourceKind.Pad, 50000, 0, axisColumn: true), Is.True);
        }

        /// <summary>D4 负例自证(QA「DWELL 关掉 ⇒ 漂移一到就切 ⇒ 抖切 ⇒ 红」):DwellTicks=0
        /// 的失效配置下,交替输入(达标/归零)使来源逐拍翻转 = 抖切;同输入在裁定口径(DWELL=4)
        /// 下零翻转。两半并置:抖切可证伪,且只有 DWELL 条件在挡它。</summary>
        [Test]
        public void test_hysteresis_dwellDisabled_flappingProof()
        {
            // 失效配置:轴列 dwell=0(第一拍达标即切)
            var broken = new DeviceSwitchHysteresis(AxisThreshold, AxisTolerance, 0,
                                                    PointerThreshold, PointerTolerance, 0);
            int flaps = 0;
            var src = DeviceSourceKind.Kbm;
            for (int i = 0; i < 10; i++)
            {
                bool switched;
                if (src == DeviceSourceKind.Kbm)
                    switched = broken.Observe(src, DeviceSourceKind.Pad, 50000, 0, axisColumn: true);
                else
                    switched = broken.Observe(src, DeviceSourceKind.Kbm, PointerThreshold * 2, 0, axisColumn: false);
                if (switched) { src = src == DeviceSourceKind.Kbm ? DeviceSourceKind.Pad : DeviceSourceKind.Kbm; flaps++; }
                broken.ResetStreaks();   // 模拟「一到就切再被拨回来」的最坏抖动
            }
            Assert.That(flaps, Is.GreaterThan(1), "DWELL 关闭 ⇒ 来源 Kbm→Pad 反复翻(失效形态可复现 = 判据可证伪)");

            // 裁定配置(DWELL=4)下同样的逐拍拨动:不足持时 ⇒ 一次都不切
            var ok = NewHysteresis();
            for (int i = 0; i < 10; i++)
            {
                Assert.That(ok.Observe(DeviceSourceKind.Kbm, DeviceSourceKind.Pad, 50000, 0, axisColumn: true),
                    Is.False, "单拍拨动不得夺源(DWELL 在场)");
                ok.Observe(DeviceSourceKind.Kbm, DeviceSourceKind.Pad, 0, 0, axisColumn: true); // 断流,重来
            }
        }

        /// <summary>D4 Ⓑ「径向 vs 逐分量」负例自证:对角漂移 ‖(30000, 30000)‖≈42426 > threshold(32768)
        /// 但**每个分量都 < threshold** ⇒ 径向实现必须切(持满 dwell 后),逐分量实现永远漏切。
        /// (story:「若实现逐分量则漏切(负例自证)」)</summary>
        [Test]
        public void test_hysteresis_diagonalAboveThreshold_radialMustSwitch()
        {
            var h = NewHysteresis();
            Assert.That(30000 < AxisThreshold, Is.True, "构造自证:两分量各自在阈下");
            bool switched = false;
            for (int i = 0; i <= AxisDwell && !switched; i++)
                switched = h.Observe(DeviceSourceKind.Kbm, DeviceSourceKind.Pad, 30000, 30000, axisColumn: true);
            Assert.That(switched, Is.True,
                "径向 ‖Δ‖=42426 越阈 ⇒ 必切;此断言红 = 实现退化为逐分量(Ⓑ 明禁)");
        }

        /// <summary>D4 Ⓑ.1 两列各自成表(禁共用一行):轴列阈 1/2 与指针列阈 4px(262144)量纲不同 ——
        /// 同一 Q16 值 100000 在轴列越阈(>32768)、在指针列不越阈(<262144):喂同值两列,
        /// 轴列满 dwell 夺源、指针列满 dwell 零反应。若两列共用一行,第二半必红。</summary>
        [Test]
        public void test_hysteresis_twoColumns_neverShareARow()
        {
            var h = NewHysteresis();
            bool axisSwitched = false;
            for (int i = 0; i <= AxisDwell && !axisSwitched; i++)
                axisSwitched = h.Observe(DeviceSourceKind.Kbm, DeviceSourceKind.Pad, 100000, 0, axisColumn: true);
            Assert.That(axisSwitched, Is.True, "轴列:100000 > 32768 ⇒ 切");

            // 指针列:候选换成 Kbm→Pad?指针列只服务 Kbm 侧的夺源(鼠标自身)。用 incumbent=Pad 测指针列:
            var h2 = NewHysteresis();
            bool pointerSwitched = false;
            for (int i = 0; i <= PointerDwell; i++)
                pointerSwitched |= h2.Observe(DeviceSourceKind.Pad, DeviceSourceKind.Kbm, 100000, 0, axisColumn: false);
            Assert.That(pointerSwitched, Is.False, "指针列:100000 < 262144 ⇒ 不切(同值不同判 = 两列存在)");
        }

        /// <summary>D4 全条件经**管理器**(机制+装配并证):Mixed/Kbm 现任,Pad 满幅持满 ⇒ 切到 Pad,
        /// ActiveSource 与 State 同时可观察(State 仍 Mixed —— 在场未变,变的只是来源)。</summary>
        [Test]
        public void test_deviceStateManager_allConditionsMet_switchesSource()
        {
            var gp = InputApi.AddDevice<Gamepad>();
            var mgr = NewManagerAttached(out var addedKbd, out var addedMouse);
            try
            {
                mgr.ObserveDigital(DeviceSourceKind.Kbm);   // 立 incumbent = Kbm
                for (int i = 0; i <= AxisDwell; i++)
                    mgr.ObserveAxis(DeviceSourceKind.Pad, 50000, 0);
                Assert.That(mgr.ActiveSource, Is.EqualTo(DeviceSourceKind.Pad), "三条件全满足 ⇒ 切到 Pad");
                Assert.That(mgr.State, Is.EqualTo(DeviceState.Mixed), "在场集未变,State 保持 Mixed");
            }
            finally
            {
                mgr.Detach();
                InputApi.RemoveDevice(gp);
                CleanupAdded(addedKbd, addedMouse);
            }
        }

        /// <summary>候选家族不在场 ⇒ 永不夺源(连接簿记执法,迟滞机制不被绕过)。
        /// 环境前置(qa-008-4):断言 KbmOnly 要求环境无物理手柄 —— 有则显式跳过
        /// (假红改自解释失配);超算批跑环境无手柄,本测照常执行。</summary>
        [Test]
        public void test_deviceStateManager_absentCandidate_neverSwitches()
        {
            if (InputApi.GetDevice<Gamepad>() != null)
                Assert.Ignore("环境已有物理手柄 ⇒ 「仅键鼠」夹具前提失配(qa-008-4:显式跳过,不假红)");
            var mgr = NewManagerAttached(out var addedKbd, out var addedMouse);
            try
            {
                Assert.That(mgr.State, Is.EqualTo(DeviceState.KbmOnly), "本夹具环境 = 仅键鼠");
                for (int i = 0; i <= AxisDwell + 2; i++)
                    Assert.That(mgr.ObserveAxis(DeviceSourceKind.Pad, 65536, 0), Is.False,
                        "手柄不在场:满幅也切不过去(先有在场,再谈迟滞)");
            }
            finally
            {
                mgr.Detach();
                CleanupAdded(addedKbd, addedMouse);
            }
        }

        // ═══════════════════ §Tuning Knobs 一之二 · 区间断言与常量表装载 ═══════════════════

        /// <summary>安全范围断言(轴列):threshold 两端不可取(0 与 ≥1)/ tolerance > 0 /
        /// dwell ≥ 0 —— 逐条违例各红一行,GDD 表「破了会怎样」的机械面。</summary>
        [Test]
        public void test_tuningValidate_axisColumnRangeAssertions_eachViolationRed()
        {
            var okAxis = new DeviceSwitchColumn(new Fix(Fix.OneRaw / 2), new Fix(Fix.OneRaw / 8), 4);
            Assert.That(okAxis.Validate("axis", checkAxisUpperBound: true), Is.Empty);

            Assert.That(new DeviceSwitchColumn(new Fix(0), new Fix(Fix.OneRaw / 8), 4).Validate("a", true),
                Has.Some.Contains("DEVICE_SWITCH_THRESHOLD"), "= 0 迟滞消失 ⇒ 红");
            Assert.That(new DeviceSwitchColumn(new Fix(Fix.OneRaw), new Fix(Fix.OneRaw / 8), 4).Validate("a", true),
                Has.Some.Contains("< 1"), "≥ 1 永锁首设备 ⇒ 红");
            Assert.That(new DeviceSwitchColumn(new Fix(Fix.OneRaw), new Fix(Fix.OneRaw / 8), 4).Validate("p", false),
                Is.Empty, "指针列无 1 上界语义(量纲=像素位移)—— 轴列专属断言不误伤");
            Assert.That(new DeviceSwitchColumn(new Fix(Fix.OneRaw / 2), new Fix(0), 4).Validate("a", true),
                Has.Some.Contains("DRIFT_TOLERANCE"));
            Assert.That(new DeviceSwitchColumn(new Fix(Fix.OneRaw / 2), new Fix(Fix.OneRaw / 8), -1).Validate("a", true),
                Has.Some.Contains("DWELL_DURATION"));
        }

        /// <summary>种子常量表(assets/data/input_device_switch.json)经 ADR-014 绑定器全绿装载,
        /// 且**两列独立可辨**(指针列阈值 ≠ 轴列阈值 —— Ⓑ.1 结构在数据层的自证)。
        /// 数值 = 临时值(待数值轮),本测只证形状与区间。</summary>
        [Test]
        public void test_deviceSwitchSeedTable_bindsAndColumnsDistinct()
        {
            var path = Path.Combine(RepoRoot, "assets", "data", "input_device_switch.json");
            Assert.That(File.Exists(path), Is.True, $"种子表缺失:{path}");

            var tuning = InputDeviceSwitchBinder.BindFromSourceText(File.ReadAllText(path));

            Assert.That(tuning.Validate(), Is.Empty, () => string.Join("\n", tuning.Validate()));
            Assert.That(tuning.Pointer.DeviceSwitchThreshold.Raw,
                Is.Not.EqualTo(tuning.Axis.DeviceSwitchThreshold.Raw),
                "两列同值 = 共用一行的影子(Ⓑ.1 要求各自成表;量纲不同不该巧合相等)");
            Assert.That(tuning.Axis.DwellTicks, Is.GreaterThanOrEqualTo(0));
        }

        /// <summary>装载期硬失败(负例):Fix 字段用数值 token / 缺 axis 列 / dwell 用字符串 /
        /// 阈值越界 —— 四种违例各聚合红(ADR-014 §四;AC-3-D4「本故事只交机制与区间断言」的执法面)。</summary>
        [Test]
        public void test_deviceSwitchBinder_violations_hardFail()
        {
            AssertNumericTokenFix();
            AssertMissingColumn();
            AssertDwellAsFixString();
            AssertOutOfRangeThreshold();
        }

        private static void AssertNumericTokenFix()
        {
            var ex = Assert.Throws<BakeValidationException>(() => InputDeviceSwitchBinder.BindFromSourceText(
                @"{ ""schema_version"": 1,
                    ""pointer"": { ""device_switch_threshold"": 4, ""drift_tolerance"": 1, ""dwell_ticks"": 4 },
                    ""axis"":   { ""device_switch_threshold"": ""1/2"", ""drift_tolerance"": ""1/8"", ""dwell_ticks"": 4 } }"));
            Assert.That(ex.Errors.Any(e => e.Contains("device_switch_threshold") && e.Contains("字符串")),
                Is.True, () => "数值 token 承载 Fix 必红(ADR-014 §四);实际:" + string.Join("\n", ex.Errors));
        }

        private static void AssertMissingColumn()
        {
            var ex = Assert.Throws<BakeValidationException>(() => InputDeviceSwitchBinder.BindFromSourceText(
                @"{ ""schema_version"": 1,
                    ""pointer"": { ""device_switch_threshold"": ""4"", ""drift_tolerance"": ""1"", ""dwell_ticks"": 4 } }"));
            Assert.That(ex.Errors.Any(e => e.Contains("axis")), Is.True,
                "缺列 = 共用一行的温床(Ⓑ.1);实际:" + string.Join("\n", ex.Errors));
        }

        private static void AssertDwellAsFixString()
        {
            var ex = Assert.Throws<BakeValidationException>(() => InputDeviceSwitchBinder.BindFromSourceText(
                @"{ ""schema_version"": 1,
                    ""pointer"": { ""device_switch_threshold"": ""4"", ""drift_tolerance"": ""1"", ""dwell_ticks"": ""1/5"" },
                    ""axis"":   { ""device_switch_threshold"": ""1/2"", ""drift_tolerance"": ""1/8"", ""dwell_ticks"": 4 } }"));
            Assert.That(ex.Errors.Any(e => e.Contains("dwell_ticks") && e.Contains("整数")), Is.True,
                "tick 计数 = int token(D-21-17);实际:" + string.Join("\n", ex.Errors));
        }

        private static void AssertOutOfRangeThreshold()
        {
            var ex = Assert.Throws<BakeValidationException>(() => InputDeviceSwitchBinder.BindFromSourceText(
                @"{ ""schema_version"": 1,
                    ""pointer"": { ""device_switch_threshold"": ""4"", ""drift_tolerance"": ""1"", ""dwell_ticks"": 4 },
                    ""axis"":   { ""device_switch_threshold"": ""1"", ""drift_tolerance"": ""1/8"", ""dwell_ticks"": 4 } }"));
            Assert.That(ex.Errors.Any(e => e.Contains("DEVICE_SWITCH_THRESHOLD") && e.Contains("< 1")), Is.True,
                "轴列阈 = 1 ⇒ 永锁首设备(装载期硬失败);实际:" + string.Join("\n", ex.Errors));
        }

        // ═══════════════════ 附注 TR-input-015 · IHaptics Verify(无 AC)═══════════════════

        /// <summary>Verify 项:IHaptics 接口存在,签名 = 通道枚举 + int 强度(Q16.16),
        /// 零波形/零时长参数(§Visual/Audio 二「通道非语义」+ story 附注「数值与波形归用户与后续轮」)。
        /// OpenXRInput spike 判不可用 ⇒ 实现挂账 P1b(接口保留,见 Completion Notes)。</summary>
        [Test]
        public void test_iHaptics_interfaceExists_channelAndIntensityOnly()
        {
            var iface = typeof(IHaptics);
            Assert.That(iface.IsInterface, Is.True, "P0 交付 = 通道接口本体");
            Assert.That(iface.IsDefined(typeof(System.ComponentModel.EditorBrowsableAttribute), false),
                Is.False, "(形状自证:接口无 EditorBrowsable 属性 —— qa-008-7 订正:原传 EditorBrowsableState 枚举类型,IsDefined 恒 false 恒真 = 占位不执法)");

            var methods = iface.GetMethods();
            Assert.That(methods, Has.Length.EqualTo(1), "P0 只有一条 Pulse(零回执/零波形方法)");
            var pulse = methods[0];
            Assert.That(pulse.Name, Is.EqualTo("Pulse"));
            var ps = pulse.GetParameters();
            Assert.That(ps.Select(p => p.ParameterType),
                Is.EqualTo(new[] { typeof(HapticsChannel), typeof(int) }),
                "参数 = 通道枚举 + int 强度(Q16.16;无 float,承整数域纪律)");

            Assert.That(Enum.GetNames(typeof(HapticsChannel)),
                Is.EquivalentTo(new[] { "GamepadRumble", "XrController" }),
                "通道闭集两值;XR 通道 = P0 占位不实现(增员须回写 GDD)");
        }
    }
}
