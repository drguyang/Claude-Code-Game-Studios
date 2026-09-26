// Story 007 · 急救直读通道与输入更新相位 —— EditMode 真身(B1a 逻辑面 + B2① 结构门 + B2② 编译门)
//
// 权威来源:production/epics/input-system/story-007-direct-read-channel.md(判据权威,AC 原文)
//   · AC-3-B1a(BLOCKING · P0)零硬件合成注入 → 读数 EmergencyReading 可见性 ≤1 帧且同帧可见
//       (断言对象 = 读数,非「输入→判定」—— 判定归 10)
//   · AC-3-B2①(BLOCKING)直读程序集 asmdef 不引用 UI Toolkit / UnityEngine.UI / EventSystem
//       (构建失败;传递闭包门 —— InputBoundaryGates.CheckUiStackReferenceClosure)
//   · AC-3-B2②(BLOCKING)Roslyn 编译期拒 UI 事件符号(非 grep;诊断 DY0002 指向该引用)
//   · AC-3-B2③(相位对偶)真身在 PlayMode(direct_read_channel_phase_test.cs)——
//       EditMode 不跑渲染帧步进,B2③ 的「帧 +1 ⇒ 采样 +1」须真实 player loop;
//       本文件只落 B1a 逻辑面 + B2① + B2②
//   · QA Test Cases(逐条 Given/When/Then/Edge/Negative)—— 本文件的测试即按其组织
//   · GDD input-system.md 规则七(直读在 onAfterUpdate 回调,非轮询)· 规则八(全整数读数)
//   · ADR-011 §二(方差不是算术 / 直读独立于 42 UI 栈)· §Risks-A S1(三道门同属「不穿 42」)
//
// 落点注记:故事 Test Evidence 登记口径 = tests/integration/input_system/direct_read_channel_test.cs;
//   Unity 只编译 unity/Assets/ 树 ⇒ 真身落本路径(承 Story 001/005/006 同一先例)。
//
// 纪律:NUnit · test_* 命名 · 确定性(无随机 / 无时间依赖)· 子进程 60s 超时兜底 ·
//   扫描键一律引用 InputBoundaryGates 常量(单一来源,不复写字面量 —— 承 Story 006 评审纪律)·
//   测试乘子为**结构性合法常量**(仅证形状;真实值归 OQ-10-7 数值轮,本文件不拍值)。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Input;
using DaYiJingCheng.Gameplay.Input.Intents;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace DaYiJingCheng.Tests.Unit.InputSystem
{
    /// <summary>Story 007 直读通道的 EditMode 测试(B1a 逻辑面 / B2① asmdef 闭包门 / B2② Roslyn 符号门)。
    /// B2③ 相位对偶在 PlayMode(见 direct_read_channel_phase_test.cs)。</summary>
    [TestFixture]
    internal sealed class DirectReadChannelTest
    {
        // ── 扫描键 / 落点(单一来源;分析器 DLL 路径承 RoslynAnalyzerLabel.UiEventSymbolAnalyzerAssetPath 语义)──
        private const string UiAnalyzerAssetPath = "Assets/Editor.Tools.Analyzers/UiEventSymbolAnalyzer.dll";
        private const string ActionAssetPath = "Assets/InputSystem_Actions.inputactions";
        private const string RoslynLabel = "RoslynAnalyzer";

        private static readonly string RepoRoot = ComputeRepoRoot();
        private static readonly string UiAnalyzerDllOnDisk =
            Path.Combine(RepoRoot, "unity", "Assets", "Editor.Tools.Analyzers", "UiEventSymbolAnalyzer.dll");

        // ── 测试乘子(结构性合法常量:F-10.1 形状 AXIAL_SCALE ≥ MAG_MAX · DZ_MAG ≥ 0)──
        // ⚠️ 数值归 OQ-10-7 数值轮 —— 本处只取「1.0 轴 → 65536 满幅」的可读值证形状,不代表裁定值。
        private const int TestAxialScale = 65536;
        private const int TestDzMag = 0;
        private const int TestMagMax = 65536;

        private static EmergencyDirectReadChannel NewLogicChannel()
            => new EmergencyDirectReadChannel(null, TestAxialScale, TestDzMag, TestMagMax);

        private static string ComputeRepoRoot([CallerFilePath] string callerPath = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerPath), "..", "..", "..", "..", ".."));

        private static string Sha256Hex(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        // ══════════ AC-3-B1a · 零硬件合成注入 → 读数可见性 ≤1 帧且同帧可见(逻辑面)══════════

        /// <summary>AC-3-B1a 正例:注入帧内同帧读到非默认 EmergencyReading(边缘标志/幅度至少一项变化),
        /// 帧计数可见性延迟 ≤1 帧。同帧可见由 IsReading 在注入点立即观察保证 ——
        /// 轮询且晚一帧更新的通道在注入点必 false(负例形态),此处即红。</summary>
        [Test]
        public void test_direct_read_channel_b1a_injected_reading_visible_same_frame()
        {
            // Arrange
            var channel = NewLogicChannel();
            channel.Arm(7);

            // Act:一次注入(press 沿 + 满幅轴)—— 等价接线侧回调该帧的直读结果
            int frameAtInject = Time.frameCount;
            int n = channel.FeedForTest(1.0f, newlyPressed: true);
            int frameAfter = Time.frameCount;

            // Assert —— 同帧可见性(注入点立即可观察)
            Assert.That(n, Is.EqualTo(1), "首次注入必须产生第 1 个样本");
            Assert.That(channel.SampleCount, Is.EqualTo(1));
            Assert.That(channel.IsReading, Is.True,
                "注入帧内必须同帧可见(AC-3-B1a;负例:通道改轮询且晚一帧 ⇒ 此处 false ⇒ 红)");

            // Assert —— 可见性延迟 ≤1 帧(AC 原文上界)
            Assert.That(frameAfter - frameAtInject, Is.LessThanOrEqualTo(1),
                "输入 → 读数可见性必须 ≤1 帧(AC-3-B1a)");

            // Assert —— 非默认读数(边缘标志/幅度变化;判定归 10,本处只断读数形状)
            var result = channel.EndAction();
            Assert.That(result, Is.Not.Null, "Armed 期有过样本 ⇒ EndAction 必须聚合出一条");
            var agg = result.Value;
            Assert.That(agg.Action, Is.EqualTo(7), "读数动作身份 = Arm 时预约的 ordinal");
            Assert.That(agg.Edges, Is.EqualTo(1), "默认值 Edges=0;一次注入 ⇒ 非默认(边缘标志变化)");
            Assert.That(agg.EdgeTicks, Is.EqualTo(new[] { 0 }), "首沿 tick=0(动作内首样本)");
            Assert.That(agg.MagPeak, Is.EqualTo(TestMagMax), "满幅轴 1.0 → 定点 65536(非默认幅度变化)");
            Assert.That(agg.HoldTicks, Is.EqualTo(0), "单沿持时 = lastEdge − firstEdge = 0(F-10.5)");
            Assert.That(channel.State, Is.EqualTo(DirectChannelState.Idle), "EndAction 后回 Idle");

            channel.AbortAction(); // Idle 态空操作,保险收尾
        }

        /// <summary>AC-3-B1a 边界「连续两帧注入(两次都同帧可见)」:两次注入各自在注入帧可观察,
        /// 聚合结果两条沿俱在(Edges=2 · EdgeTicks=[0,1])。</summary>
        [Test]
        public void test_direct_read_channel_b1a_consecutive_injections_both_visible_same_frame()
        {
            // Arrange
            var channel = NewLogicChannel();
            channel.Arm(3);

            // Act —— 注入 1(满幅沿)
            int frame1 = Time.frameCount;
            int n1 = channel.FeedForTest(1.0f, newlyPressed: true);

            // Assert —— 注入 1 同帧可见
            Assert.That(n1, Is.EqualTo(1));
            Assert.That(channel.IsReading, Is.True, "注入 1 必须在注入帧可见");
            Assert.That(Time.frameCount - frame1, Is.LessThanOrEqualTo(1), "注入 1 可见性 ≤1 帧");

            // Act —— 持有帧(无新沿)+ 注入 2(半幅沿)
            channel.FeedForTest(0.5f, newlyPressed: false);
            int frame2 = Time.frameCount;
            int n3 = channel.FeedForTest(0.5f, newlyPressed: true);

            // Assert —— 注入 2 同帧可见
            Assert.That(n3, Is.EqualTo(3), "连续注入 ⇒ 累计 3 个样本");
            Assert.That(channel.IsReading, Is.True, "注入 2 必须在注入帧可见(两次都同帧可见)");
            Assert.That(Time.frameCount - frame2, Is.LessThanOrEqualTo(1), "注入 2 可见性 ≤1 帧");

            // Act —— 尾帧持有 + 收束
            channel.FeedForTest(0.5f, newlyPressed: false);
            var result = channel.EndAction();

            // Assert —— 聚合把两次注入都收进来了
            Assert.That(channel.SampleCount, Is.EqualTo(4), "四帧各恰一样本");
            Assert.That(result, Is.Not.Null);
            var agg = result.Value;
            Assert.That(agg.Action, Is.EqualTo(3));
            Assert.That(agg.Edges, Is.EqualTo(2), "两次注入 ⇒ 两条沿");
            Assert.That(agg.EdgeTicks, Is.EqualTo(new[] { 0, 1 }), "沿 tick 单调 = 动作内第几个样本");
            Assert.That(agg.MagPeak, Is.EqualTo(TestMagMax), "峰值取注入 1 的满幅");
            Assert.That(agg.MagLast, Is.EqualTo(32768), "末帧半幅 0.5 → 32768(0.5×65536)");
            Assert.That(agg.HoldTicks, Is.EqualTo(1), "持时 = lastEdge(1) − firstEdge(0)");
        }

        /// <summary>AC-3-B1a 负面 / 默认值面:未 Arm(默认态)时 Feed 拒收 ⇒ 零样本,
        /// EndAction 返回 null(零样本签约)—— 「非默认读数」的对照组。</summary>
        [Test]
        public void test_direct_read_channel_b1a_idle_rejects_feed_default_reading()
        {
            // Arrange
            var channel = NewLogicChannel();
            var defaultReading = new EmergencyReading(0, 0, 0, Array.Empty<int>(), 0);

            // Act + Assert —— Feed 拒收(Idle 零采样)
            Assert.That(channel.Feed(defaultReading), Is.False, "Idle 态 Feed 必须拒收");
            Assert.That(channel.SampleCount, Is.EqualTo(0), "默认态零采样");
            Assert.That(channel.FeedForTest(1.0f, newlyPressed: true), Is.EqualTo(0),
                "测试面同样拒收(计数不前进)");
            Assert.That(channel.SampleCount, Is.EqualTo(0));
            Assert.That(channel.IsReading, Is.False, "未采样 ⇒ 不可观察为 Reading");

            // Act + Assert —— 零样本签约:Idle 态 EndAction = null
            Assert.That(channel.EndAction(), Is.Null, "Idle 态 EndAction 返回 null(零样本 ⇒ 0 条上行)");
            Assert.That(channel.State, Is.EqualTo(DirectChannelState.Idle));
        }

        // ══════════ 状态机构(构造校验 / Arm 预约 / Attach 接线)══════════

        /// <summary>构造校验:F-10.1 结构性要求 AXIAL_SCALE ≥ MAG_MAX 与 DZ_MAG ≥ 0,
        /// 违例必须抛 ArgumentException(否则幅度门退化 / 负死区静默吃样本)。</summary>
        [Test]
        public void test_direct_read_channel_ctor_rejects_malformed_multipliers()
        {
            Assert.Throws<ArgumentException>(
                () => new EmergencyDirectReadChannel(null, 100, 0, 200),
                "axialScale < magMax 必须拒(F-10.1:否则幅度域被压扁)");
            Assert.Throws<ArgumentException>(
                () => new EmergencyDirectReadChannel(null, TestAxialScale, -1, TestMagMax),
                "dzMag < 0 必须拒(死区非负)");
        }

        /// <summary>Arm 纪律:非 Idle 态二次预约 = 编程错误(10 须先 End/Abort)⇒ InvalidOperationException。</summary>
        [Test]
        public void test_direct_read_channel_arm_twice_throws()
        {
            var channel = NewLogicChannel();
            channel.Arm(1);

            Assert.Throws<InvalidOperationException>(() => channel.Arm(2),
                "动作进行中不可二次预约(状态机守卫)");
            Assert.That(channel.State, Is.EqualTo(DirectChannelState.Armed), "违例预约不得改变状态");
        }

        /// <summary>Arm 纪律:actionOrdinal 非负(OQ-10-6 表 / 10 侧烘焙数据)⇒ ArgumentException。</summary>
        [Test]
        public void test_direct_read_channel_arm_negative_ordinal_throws()
        {
            var channel = NewLogicChannel();
            Assert.Throws<ArgumentException>(() => channel.Arm(-1));
            Assert.That(channel.State, Is.EqualTo(DirectChannelState.Idle), "违例预约不得进入 Armed");
        }

        /// <summary>EndAction 幂等面:Armed 期收束出一条后回 Idle,再次 End = null(不重复聚合)。</summary>
        [Test]
        public void test_direct_read_channel_end_twice_returns_null()
        {
            var channel = NewLogicChannel();
            channel.Arm(2);
            channel.FeedForTest(1.0f, newlyPressed: true);

            var first = channel.EndAction();
            Assert.That(first, Is.Not.Null);

            Assert.That(channel.EndAction(), Is.Null, "Idle 态再 End 返回 null(零样本签约)");
            Assert.That(channel.State, Is.EqualTo(DirectChannelState.Idle));
        }

        /// <summary>AbortAction(F-10.5 附注):丢弃累计、回 Idle、不发 —— End 返回 null;
        /// SampleCount 是累计仪表(跨周期不清零),拒收后不前进。</summary>
        [Test]
        public void test_direct_read_channel_abort_discards_and_rejects()
        {
            var channel = NewLogicChannel();
            channel.Arm(4);
            channel.FeedForTest(1.0f, newlyPressed: true);
            Assert.That(channel.SampleCount, Is.EqualTo(1));

            channel.AbortAction();

            Assert.That(channel.State, Is.EqualTo(DirectChannelState.Idle));
            Assert.That(channel.EndAction(), Is.Null, "已 Abort ⇒ 无记录可聚合(丢弃语义)");
            Assert.That(channel.FeedForTest(0.5f, true), Is.EqualTo(1), "Idle 拒收 ⇒ 计数保持累计值");
            Assert.That(channel.SampleCount, Is.EqualTo(1), "SampleCount 是累计仪表(设计口径,不清零)");
        }

        /// <summary>Attach 纪律(逻辑层模式):构造传 null 动作资产不能接线 —— InvalidOperationException。</summary>
        [Test]
        public void test_direct_read_channel_attach_logic_layer_mode_throws()
        {
            var channel = NewLogicChannel();
            Assert.That(channel.IsAttached, Is.False);
            Assert.Throws<InvalidOperationException>(() => channel.Attach(),
                "逻辑层模式(无动作资产)必须拒接线");
            Assert.That(channel.IsAttached, Is.False);
        }

        /// <summary>Attach 纪律(接线模式):真动作资产(规则一唯一 .inputactions,含 Emergency 动作)
        /// 恰挂一次 onAfterUpdate;二次挂抛;Detach 幂等。</summary>
        [Test]
        public void test_direct_read_channel_attach_real_asset_detach_idempotent()
        {
            // Arrange
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionAssetPath);
            Assert.That(asset, Is.Not.Null, "动作资产未找到:" + ActionAssetPath);
            var channel = new EmergencyDirectReadChannel(asset, TestAxialScale, TestDzMag, TestMagMax);

            try
            {
                // Act + Assert —— 恰一次
                channel.Attach();
                Assert.That(channel.IsAttached, Is.True);
                Assert.Throws<InvalidOperationException>(() => channel.Attach(), "重复挂接必须抛(恰一次)");

                // Act + Assert —— Detach 幂等
                channel.Detach();
                Assert.That(channel.IsAttached, Is.False);
                channel.Detach();
                Assert.That(channel.IsAttached, Is.False, "Detach 幂等(未挂时无操作)");
            }
            finally
            {
                channel.Detach();
            }
        }

        // ══════════ AC-3-B2① · 直读程序集 UI 栈引用闭包(asmdef 结构门)══════════

        /// <summary>AC-3-B2① 端到端:真工程装配图上 Gameplay.Input 的传递闭包 ∩ UI 栈禁引集 = ∅。</summary>
        [Test]
        public void test_direct_read_gate_b2_1_real_graph_has_no_ui_stack_closure()
        {
            var graph = InputBoundaryGates.BuildProjectReferenceGraph();
            Assert.That(graph.ContainsKey(InputBoundaryGates.InputAssemblyName), Is.True,
                "真图必须含直读程序集根(否则闭包空转 = 假绿)");

            var errs = InputBoundaryGates.CheckUiStackReferenceClosure();

            Assert.That(errs, Is.Empty, () => "AC-3-B2① 直读程序集不得可达 UI 栈程序集:\n" +
                                              string.Join("\n", errs));
        }

        /// <summary>AC-3-B2① 正例夹具:直读程序集直引任一 UI 栈程序集 ⇒ 一条 [B2①] 红并点名违例。</summary>
        [Test]
        public void test_direct_read_gate_b2_1_direct_reference_reports_violation()
        {
            string forbidden = InputBoundaryGates.UiStackForbiddenAssemblyNames[0];
            var graph = new Dictionary<string, List<string>>(StringComparer.Ordinal)
            {
                [InputBoundaryGates.InputAssemblyName] = new List<string> { forbidden },
            };

            var errs = InputBoundaryGates.UiStackReferenceClosureViolations(
                graph, InputBoundaryGates.InputAssemblyName);

            Assert.That(errs, Has.Count.EqualTo(1), "直引必须恰报一条:\n" + string.Join("\n", errs));
            Assert.That(errs[0], Does.Contain("[B2①]"), "红行须带门标记");
            Assert.That(errs[0], Does.Contain(forbidden), "红行须点名违例程序集");
        }

        /// <summary>AC-3-B2① 边界「经输入程序集间接可达 ⇒ 传递闭包收紧」:一层 shim 中转同样红。</summary>
        [Test]
        public void test_direct_read_gate_b2_1_shim_indirection_reports_violation()
        {
            string forbidden = InputBoundaryGates.UiStackForbiddenAssemblyNames[
                InputBoundaryGates.UiStackForbiddenAssemblyNames.Length - 1];
            var graph = new Dictionary<string, List<string>>(StringComparer.Ordinal)
            {
                [InputBoundaryGates.InputAssemblyName] = new List<string> { "Some.Ui.Shim" },
                ["Some.Ui.Shim"] = new List<string> { forbidden },
            };

            var errs = InputBoundaryGates.UiStackReferenceClosureViolations(
                graph, InputBoundaryGates.InputAssemblyName);

            Assert.That(errs, Has.Count.EqualTo(1), () => "shim 间接可达必须红:\n" + string.Join("\n", errs));
            Assert.That(errs[0], Does.Contain("[B2①]"));
            Assert.That(errs[0], Does.Contain(forbidden), "红行须点名闭包末端的违例程序集");
        }

        /// <summary>AC-3-B2① 引擎叶非误报:直读程序集引 Unity.InputSystem(引擎装配,图外 = 叶)
        /// 合法 —— 其引擎侧对 UGUI 的依赖不建边、不带出禁引名(否则任何合法输入引用都误红)。</summary>
        [Test]
        public void test_direct_read_gate_b2_1_engine_leaf_not_false_positive()
        {
            var graph = new Dictionary<string, List<string>>(StringComparer.Ordinal)
            {
                [InputBoundaryGates.InputAssemblyName] = new List<string> { "Unity.InputSystem" },
            };

            var errs = InputBoundaryGates.UiStackReferenceClosureViolations(
                graph, InputBoundaryGates.InputAssemblyName);

            Assert.That(errs, Is.Empty, () => "引擎叶不得误报:\n" + string.Join("\n", errs));
        }

        /// <summary>AC-3-B2① 门接线:CheckUiStackReferenceClosure 必须挂进 InputBoundaryGates.RunAll
        /// (门存在但没接进总入口 = 无人执行,构建期静默失效)。</summary>
        [Test]
        public void test_direct_read_gate_b2_1_wired_into_run_all()
        {
            string gateSrcPath = Path.Combine(RepoRoot, "unity", "Assets",
                "Editor.Tools.Gates", "InputBoundaryGates.cs");
            Assert.That(File.Exists(gateSrcPath), Is.True, "门源文件缺失:" + gateSrcPath);
            string src = File.ReadAllText(gateSrcPath);

            Assert.That(src, Does.Contain("errs.AddRange(CheckUiStackReferenceClosure());"),
                "B2① 必须挂进 RunAll —— 门存在但未接线 = 构建期无人执行(Story 007 AC-3-B2①)");
        }

        // ══════════ AC-3-B2② · Roslyn 拒 UI 事件符号(编译期,非 grep)══════════

        /// <summary>AC-3-B2② 前置:分析器 DLL 已产出,且带 RoslynAnalyzer label(label 缺失 ⇒ 门静默失效)。</summary>
        [Test]
        public void test_direct_read_gate_b2_2_dll_present_and_labelled()
        {
            Assert.That(File.Exists(UiAnalyzerDllOnDisk), Is.True,
                "分析器 DLL 不存在 —— 先跑 tools/analyzers/build.sh:" + UiAnalyzerDllOnDisk);

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(UiAnalyzerAssetPath);
            Assert.That(asset, Is.Not.Null, "AssetDatabase 未加载到分析器 DLL:" + UiAnalyzerAssetPath);
            string[] labels = AssetDatabase.GetLabels(asset);

            Assert.That(labels, Does.Contain(RoslynLabel),
                $"缺少 {RoslynLabel} label ⇒ Unity 不会把它当分析器,AC-3-B2② 静默失效" +
                "(先 batch 执行 DaYiJingCheng.EditorTools.Gates.RoslynAnalyzerLabel.SetLabel)");
        }

        /// <summary>平台位全关回归(承 Story 001 W2/B 实验):Any=off 且 Editor=off ——
        /// 任一位翻回即红(player 构建炸 / 域重载插件装载噪声)。</summary>
        [Test]
        public void test_direct_read_gate_b2_2_platform_all_off()
        {
            var importer = AssetImporter.GetAtPath(UiAnalyzerAssetPath) as PluginImporter;
            Assert.That(importer, Is.Not.Null, "PluginImporter 获取失败:" + UiAnalyzerAssetPath);

            Assert.That(importer.GetCompatibleWithAnyPlatform(), Is.False,
                "分析器 DLL 不得 Any=on(W2:UnityLinker AssemblyResolutionException):" + UiAnalyzerAssetPath);
            Assert.That(importer.GetCompatibleWithEditor(), Is.False,
                "分析器 DLL 不得 Editor=on(B 实验:域重载插件装载噪声):" + UiAnalyzerAssetPath);
        }

        /// <summary>AC-3-B2② 新鲜度:DLL ↔ 分析器源码经 build.sh sidecar(双 hash)绑定 ——
        /// 改源不重跑 build.sh ⇒ 红(防门静默跑旧逻辑)。</summary>
        [Test]
        public void test_direct_read_gate_b2_2_dll_matches_source_freshness_sidecar()
        {
            string srcPath = Path.Combine(RepoRoot, "tools", "analyzers",
                "UiEventSymbolAnalyzer", "UiEventSymbolAnalyzer.cs");
            string sidecarPath = Path.Combine(RepoRoot, "tools", "analyzers", "UiEventSymbolAnalyzer.dll.sha256");
            Assert.That(File.Exists(srcPath), Is.True, "分析器源码缺失:" + srcPath);
            Assert.That(File.Exists(sidecarPath), Is.True,
                "新鲜度 sidecar 缺失 —— 重跑 bash tools/analyzers/build.sh 产出:" + sidecarPath);
            Assert.That(File.Exists(UiAnalyzerDllOnDisk), Is.True,
                "分析器 DLL 不存在 —— 先跑 tools/analyzers/build.sh:" + UiAnalyzerDllOnDisk);

            string[] parts = File.ReadAllText(sidecarPath)
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.That(parts.Length, Is.EqualTo(2),
                "sidecar 格式应为「<源 sha256> <DLL sha256>」:" + sidecarPath);

            Assert.That(Sha256Hex(srcPath), Is.EqualTo(parts[0]),
                "分析器源码已改动但 sidecar 未更新 ⇒ 改了源没重跑 build.sh:" + srcPath);
            Assert.That(Sha256Hex(UiAnalyzerDllOnDisk), Is.EqualTo(parts[1]),
                "DLL 与 sidecar 记录不符 ⇒ 重跑 build.sh 以刷新:" + UiAnalyzerDllOnDisk);
        }

        /// <summary>AC-3-B2② 正例(using 短名 + 成员访问):样例在直读作用域内引用 EventSystem.current
        /// ⇒ 子进程 csc 编译失败、诊断 DY0002 且带行列指向该引用。</summary>
        [Test]
        public void test_direct_read_gate_b2_2_rejects_eventsystem_property_reference()
        {
            const string sample =
                "using UnityEngine.EventSystems;\n" +
                "public static class BadEventSystem\n" +
                "{\n" +
                "    public static EventSystem Run() => EventSystem.current;\n" +
                "}\n";

            var (exitCode, output) = RunCscWithAnalyzer(
                "Assets/Gameplay.Input/bad_eventsystem.cs", sample);

            Assert.That(exitCode, Is.Not.EqualTo(0), "直读作用域内 UI 事件符号引用必须编译失败:\n" + output);
            Assert.That(output, Does.Contain("DY0002"), "失败必须由 DY0002 报出:\n" + output);
            Assert.That(output, Does.Match(@"bad_eventsystem\.cs\(\d+,\d+\): error DY0002"),
                "诊断必须带行列位置、指向该引用(而非无位置的裸诊断):\n" + output);
        }

        /// <summary>AC-3-B2② 边界「全限定名」:typeof(UnityEngine.UIElements.NavigationMoveEvent)
        /// —— 无 using、全限定形态同样拒(符号面判定,非文本前缀)。</summary>
        [Test]
        public void test_direct_read_gate_b2_2_rejects_fully_qualified_typeof()
        {
            const string sample =
                "using System;\n" +
                "public static class BadTypeOfNav\n" +
                "{\n" +
                "    public static Type T() => typeof(UnityEngine.UIElements.NavigationMoveEvent);\n" +
                "}\n";

            var (exitCode, output) = RunCscWithAnalyzer(
                "Assets/Gameplay.Input/bad_typeof_nav.cs", sample);

            Assert.That(exitCode, Is.Not.EqualTo(0), "全限定 typeof UI 事件类型必须编译失败:\n" + output);
            Assert.That(output, Does.Contain("DY0002"), "失败必须由 DY0002 报出:\n" + output);
            Assert.That(output, Does.Match(@"bad_typeof_nav\.cs\(\d+,\d+\): error DY0002"),
                "诊断必须指向该引用:\n" + output);
        }

        /// <summary>AC-3-B2② 边界「using 短名 + 局部变量」:类型只有名字没有成员的形态
        /// (声明 / 转换 / 局部引用)同样拒。</summary>
        [Test]
        public void test_direct_read_gate_b2_2_rejects_using_short_name_local()
        {
            const string sample =
                "using UnityEngine.UIElements;\n" +
                "public static class BadUsingLocal\n" +
                "{\n" +
                "    public static NavigationMoveEvent Run()\n" +
                "    {\n" +
                "        NavigationMoveEvent e = null;\n" +
                "        return e;\n" +
                "    }\n" +
                "}\n";

            var (exitCode, output) = RunCscWithAnalyzer(
                "Assets/Gameplay.Input/bad_using_local.cs", sample);

            Assert.That(exitCode, Is.Not.EqualTo(0), "using 短名局部变量形态必须编译失败:\n" + output);
            Assert.That(output, Does.Contain("DY0002"), "失败必须由 DY0002 报出:\n" + output);
            Assert.That(output, Does.Match(@"bad_using_local\.cs\(\d+,\d+\): error DY0002"),
                "诊断必须指向该引用:\n" + output);
        }

        /// <summary>AC-3-B2② 负面夹具「只 using 不触符号 ⇒ 不误报」:两条 using 指令但零符号使用
        /// ⇒ 编译通过、零 DY0002(证明门判符号不判文本)。</summary>
        [Test]
        public void test_direct_read_gate_b2_2_allows_using_only_negative_fixture()
        {
            const string sample =
                "using UnityEngine.EventSystems;\n" +
                "using UnityEngine.UIElements;\n" +
                "public static class GoodUsingOnly\n" +
                "{\n" +
                "    public static int Run() => 42;\n" +
                "}\n";

            var (exitCode, output) = RunCscWithAnalyzer(
                "Assets/Gameplay.Input/good_using_only.cs", sample);

            Assert.That(exitCode, Is.EqualTo(0), "只 using 不触符号不得误报:\n" + output);
            Assert.That(output, Does.Not.Contain("DY0002"), "负面夹具不得出现 DY0002:\n" + output);
        }

        /// <summary>AC-3-B2② 正向放行「合法 Input System 写法」:InputAction.ReadValue 在直读作用域内
        /// 必须通过(门禁的是 UI 事件栈,不是输入系统本身)。</summary>
        [Test]
        public void test_direct_read_gate_b2_2_allows_input_action_usage()
        {
            const string sample =
                "using UnityEngine.InputSystem;\n" +
                "public static class GoodInputActionUsage\n" +
                "{\n" +
                "    public static float Run(InputAction action) => action.ReadValue<float>();\n" +
                "}\n";

            var (exitCode, output) = RunCscWithAnalyzer(
                "Assets/Gameplay.Input/good_input_action.cs", sample);

            Assert.That(exitCode, Is.EqualTo(0), "合法 Input System 写法不得被误报:\n" + output);
            Assert.That(output, Does.Not.Contain("DY0002"), "合法样例不得出现 DY0002:\n" + output);
        }

        /// <summary>AC-3-B2② 作用域边界:同一样例(含 EventSystem 引用)落在直读程序集**目录之外**
        /// ⇒ 不受本门约束(42 UI 栈 / Spike 按设计合法引用 UI 符号)⇒ 编译通过、零 DY0002。</summary>
        [Test]
        public void test_direct_read_gate_b2_2_out_of_scope_file_not_gated()
        {
            const string sample =
                "using UnityEngine.EventSystems;\n" +
                "public static class OutOfScopeEventSystem\n" +
                "{\n" +
                "    public static EventSystem Run() => EventSystem.current;\n" +
                "}\n";

            // 裸文件名 ⇒ 落 workDir 根(路径不含 Assets/Gameplay.Input/ = 作用域外)
            var (exitCode, output) = RunCscWithAnalyzer("out_of_scope_eventsystem.cs", sample);

            Assert.That(exitCode, Is.EqualTo(0),
                "直读作用域外的 UI 代码不受本门约束(作用域级门):\n" + output);
            Assert.That(output, Does.Not.Contain("DY0002"), "作用域外不得出现 DY0002:\n" + output);
        }

        // ══════════ 子进程编译装置(AC-3-B2② 的执行体;不破坏工程编译)══════════

        /// <summary>桩程序集源:自备 UnityEngine.EventSystems.EventSystem /
        /// UnityEngine.UIElements.NavigationMoveEvent / UnityEngine.InputSystem.InputAction ——
        /// 样例编译无需真 UnityEngine;分析器按「类型所属命名空间」判定(语义),
        /// 桩与真类型同名同命名空间即等价生效。</summary>
        private const string StubSource =
            "namespace UnityEngine.EventSystems\n" +
            "{\n" +
            "    public class EventSystem\n" +
            "    {\n" +
            "        public static EventSystem current { get { return null; } }\n" +
            "    }\n" +
            "}\n" +
            "namespace UnityEngine.UIElements\n" +
            "{\n" +
            "    public class NavigationMoveEvent { }\n" +
            "}\n" +
            "namespace UnityEngine.InputSystem\n" +
            "{\n" +
            "    public class InputAction\n" +
            "    {\n" +
            "        public TValue ReadValue<TValue>() { return default(TValue); }\n" +
            "    }\n" +
            "}\n";

        /// <summary>从 EditorApplication.applicationPath 上溯定位捆绑 dotnet 与 csc
        /// (逐级试 Data/、Tools/ 两候选 —— 与 Story 001 装置同款,跨机器可移植)。</summary>
        private static (string dotnet, string cscDll, string editorData) LocateUnityToolchain()
        {
            string dir = Path.GetDirectoryName(EditorApplication.applicationPath);
            while (!string.IsNullOrEmpty(dir))
            {
                foreach (string sub in new[] { "Data", "Tools" })
                {
                    string data = Path.Combine(dir, sub);
                    string csc = Path.Combine(data, "DotNetSdkRoslyn", "csc.dll");
                    if (!File.Exists(csc))
                        continue;
                    string dotnetUnix = Path.Combine(data, "NetCoreRuntime", "dotnet");
                    string dotnetWin = dotnetUnix + ".exe";
                    string dotnet = File.Exists(dotnetUnix) ? dotnetUnix
                        : File.Exists(dotnetWin) ? dotnetWin : null;
                    if (dotnet != null)
                        return (dotnet, csc, data);
                }
                dir = Path.GetDirectoryName(dir);
            }
            Assert.Fail("找不到捆绑工具链(以 applicationPath 上溯,试 Data/ 与 DotNetSdkRoslyn/):" +
                         EditorApplication.applicationPath);
            return default;
        }

        /// <summary>用捆绑 csc 编译样例(引用 NetCoreRuntime 框架程序集 + -analyzer 挂 UiEventSymbolAnalyzer)。
        /// <paramref name="sampleRelPath"/> 含 <c>Assets/Gameplay.Input/</c> 前缀 = 作用域内(建子目录);
        /// 裸文件名 = 作用域外(落根)。工作目录固定于临时目录,每测先清后建(隔离 + 自清理)。</summary>
        private static (int exitCode, string output) RunCscWithAnalyzer(string sampleRelPath, string sampleSource)
        {
            var (dotnet, cscDll, editorData) = LocateUnityToolchain();

            // 隔离:测试全名 + 进程 id(常量目录在并行/多实例下互删;测试名保日志可复现)
            string safeName = TestContext.CurrentContext.Test.FullName;
            foreach (char c in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(c, '_');
            string workDir = Path.Combine(Path.GetTempPath(),
                "dyc_story007_analyzer_" + Process.GetCurrentProcess().Id + "_" + safeName);
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, true);
            Directory.CreateDirectory(workDir);
            try
            {
                // 框架引用集:NetCoreRuntime/shared/Microsoft.NETCore.App/<version>/*.dll(取最高版,同 build.sh)
                string sharedRoot = Path.Combine(editorData, "NetCoreRuntime", "shared", "Microsoft.NETCore.App");
                Assert.That(Directory.Exists(sharedRoot), Is.True, "缺 shared/Microsoft.NETCore.App:" + sharedRoot);
                string frameworkDir = Directory.GetDirectories(sharedRoot)
                    .OrderBy(d => System.Version.TryParse(
                            Path.GetFileName(d.TrimEnd(Path.DirectorySeparatorChar, '/')), out var v)
                        ? v : new Version(0, 0))
                    .Last();
                string[] frameworkRefs = Directory.GetFiles(frameworkDir, "*.dll", SearchOption.TopDirectoryOnly);

                string rsp = Path.Combine(workDir, "refs.rsp");
                var rspLines = new List<string>();
                foreach (string dll in frameworkRefs)
                    rspLines.Add("-r:\"" + dll + "\"");
                File.WriteAllLines(rsp, rspLines);

                string stubPath = Path.Combine(workDir, "stub.cs");
                File.WriteAllText(stubPath, StubSource);

                // 样例落点:含作用域键 ⇒ 建子目录(路径决定分析器作用域判定);裸名 ⇒ 根
                string samplePath = Path.Combine(workDir, sampleRelPath);
                string sampleDir = Path.GetDirectoryName(samplePath);
                if (!string.IsNullOrEmpty(sampleDir))
                    Directory.CreateDirectory(sampleDir);
                File.WriteAllText(samplePath, sampleSource);

                string outDll = Path.Combine(workDir, "sample_out.dll");

                var psi = new ProcessStartInfo
                {
                    FileName = dotnet,
                    Arguments = "exec \"" + cscDll + "\" -nologo -target:library -nostdlib+" +
                                " -out:\"" + outDll + "\"" +
                                " -analyzer:\"" + UiAnalyzerDllOnDisk + "\"" +
                                " @\"" + rsp + "\" \"" + stubPath + "\" \"" + samplePath + "\"",
                    WorkingDirectory = workDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using (var proc = Process.Start(psi))
                {
                    Assert.That(proc, Is.Not.Null, "Process.Start 失败:" + dotnet);
                    var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                    var stderrTask = proc.StandardError.ReadToEndAsync();
                    bool exited = proc.WaitForExit(60_000);
                    if (!exited)
                    {
                        try { proc.Kill(); } catch { /* 已退出则忽略 */ }
                        Assert.Fail("csc 子进程超时(60s):" + psi.Arguments);
                    }
                    string output = stdoutTask.GetAwaiter().GetResult() + stderrTask.GetAwaiter().GetResult();
                    return (proc.ExitCode, output);
                }
            }
            finally
            {
                try { Directory.Delete(workDir, true); } catch { /* 清理失败不掩盖断言 */ }
            }
        }
    }
}
