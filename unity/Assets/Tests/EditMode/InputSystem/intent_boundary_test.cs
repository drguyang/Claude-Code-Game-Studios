// 权威来源:
//   Story: production/epics/input-system/story-006-intent-boundary.md
//     · AC-3-A6(BLOCKING)—— 零 SimEvent:输入程序集 asmdef 引用集白名单(不引用声明
//         IEventSink / SimEvent 的程序集)+ 交出物闭集断言
//     · AC-3-A7(BLOCKING)—— SimEvent 载荷可达类型闭包**递归**零 float/double
//     · AC-3-B3(BLOCKING)—— 判定结果全整数(int/long/bool/枚举/Fix)+ 3 侧无
//         FixParse.Parse(string)(SimEvent 由 10 直接构造)
//     · AC-3-B4(BLOCKING)—— 联机 C 路:客户端聚合**一条**全整数上行;本地判定仅预表现
//   TR: docs/architecture/tr-registry.yaml TR-input-007 + TR-input-008
//   ADR: ADR-011 §二 / Amendment A / Amendment B(主)· ADR-006 · ADR-009 §七 · ADR-025 §①
//
// ⚠️ 落点:故事头账本路径 = tests/unit/input_system/intent_boundary_test.cs;
//    **Unity 只编译 unity/Assets/ 树** ⇒ 真身 = 本文件(承 Story 001/005 同一先例)。
//
// ⚠️ B3 的 EmergencyReading 字段断言**不在门内**跑:Editor.Tools.Gates.asmdef 不引用
//    Gameplay.Input(A6 自己禁的正是这条边)⇒ `typeof(EmergencyReading)` 在门程序集内不可
//    编译。本测试装配引用 Gameplay.Input,把类型喂给门里的**纯函数**
//    InputBoundaryGates.CheckReadingFieldLeaves(Type) —— 门保判据,测试供类型。
//
// ⚠️ 负向夹具落位:全住本测试装配(不污染生产扫描面、不新增 asmdef —— 新增 asmdef 会被
//    b3 Manifest 封闭性拒)。私有嵌套类型即负例源;合成源文本喂 CheckInputSourceText
//    的等价谓词(免写坏生产 .cs)。
// ⚠️ 零 UnityEngine 副作用 / 零随机 / 零时间依赖;确定性 = 反射枚举序 + Ordinal 判定。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Input;
using DaYiJingCheng.Gameplay.Input.Intents;

namespace DaYiJingCheng.Tests.Unit.InputSystem
{
    [TestFixture]
    internal sealed class IntentBoundaryTest
    {
        // ═══════════════ AC-3-A6 —— 引用集白名单 ═══════════════

        [Test]
        public void test_refSet_forbiddenSimFamily_reportsRed()
        {
            // AC-3-A6 正文:「不引用任何声明 IEventSink / SimEvent 的程序集(构建失败)」。
            // 集合按**声明面**取 = {Sim.Contracts}(SimEvent.cs / Abstractions.cs 现住此);
            // Sim / Sim.Codec 同时禁(3 引用 Sim = 写能力传播面,见门内注记)。
            foreach (var forbidden in InputBoundaryGates.InputForbiddenAssemblies)
            {
                var errs = InputBoundaryGates.CheckInputReferenceSet(new[] { forbidden });

                Assert.That(errs, Has.Count.EqualTo(1), $"{forbidden} 必红");
                Assert.That(errs[0], Does.Contain("[A6]"));
                Assert.That(errs[0], Does.Contain(forbidden), "红行须点名违例装配");
                Assert.That(errs[0], Does.Contain("构建失败"), "红行须写明后果(AC-3-A6 原文)");
            }
        }

        [Test]
        public void test_refSet_engineAndBcl_pass()
        {
            // 引擎白名单(Unity.InputSystem 等)+ BCL 两面放行(引擎侧输入栈必需)
            var errs = InputBoundaryGates.CheckInputReferenceSet(new[]
            {
                "Unity.InputSystem", "UnityEngine", "UnityEngine.InputLegacyModule",
                "System", "System.Runtime", "netstandard", "mscorlib", "Mono",
            });

            Assert.That(errs, Is.Empty, () => string.Join("\n", errs));
        }

        [Test]
        public void test_refSet_unregisteredDrift_reportsRedWithThreeSegments()
        {
            // 漂移红:既非禁引、也非引擎/BCL、也不在 ADR-025 登记集 —— 须先过装配轮。
            // 混入 Sim.Contracts 另有黑名单红 ⇒ **两条**(黑名单与漂移互不吞并,逐条报)。
            var errs = InputBoundaryGates.CheckInputReferenceSet(
                new[] { "Sim.Contracts", "Some.New.Gameplay.Assembly" });

            Assert.That(errs, Has.Count.EqualTo(2),
                "Sim.Contracts(黑名单)+ Some.New.Gameplay.Assembly(漂移)= 两条独立红");

            var drift = errs.Single(e => e.Contains("漂移"));
            Assert.That(drift, Does.Contain("[A6]"));
            Assert.That(drift, Does.Contain("Some.New.Gameplay.Assembly"));
            Assert.That(drift, Does.Contain("实际集"), "三段并列:实际集");
            Assert.That(drift, Does.Contain("ADR-025"), "三段并列:登记集出处");

            // 按性质而非按子串取行(每行都带「实际集」全列,含 Sim.Contracts ⇒ 按子串
            // 取行会两条都命中)。
            var black = errs.Single(e => e.Contains("构建失败"));
            Assert.That(black, Does.Contain("Sim.Contracts"), "黑名单红须点名违例装配");
        }

        [Test]
        public void test_refSet_emptyAndNull_areGreen()
        {
            Assert.That(InputBoundaryGates.CheckInputReferenceSet(new string[0]), Is.Empty);
            Assert.That(InputBoundaryGates.CheckInputReferenceSet(null), Is.Empty,
                "null 引用集 = 零引用,平凡成立(不是「解析失败」—— 后者由端到端面独立报红)");
        }

        [Test]
        public void test_inputAssemblyReferences_currentTree_zeroErrors()
        {
            // 端到端(真 asmdef + 真编译产物引用表并集):当前树必须绿。
            // scriptCompilationFailed ⇒ 红(拒扫陈旧产物 = 假绿防护),非绿非红皆不可接受。
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False,
                "跑 A6 端到端前提:编译成功(读上一版 DLL = 假绿)");

            var errs = InputBoundaryGates.CheckInputAssemblyReferences();

            Assert.That(errs, Is.Empty, () => "A6 引用集红行:\n" + string.Join("\n", errs));
        }

        // ── A6 传递闭包(间接引用;G3)──

        [Test]
        public void test_referenceClosure_oneShimLayer_reportsRed()
        {
            // 故事 QA 边界例原文:「加一层 shim 间接引用 ⇒ 必红」。
            // 合成图:Gameplay.Input → Some.Shim → Sim.Contracts(直接面全绿,仅闭包面红)。
            var graph = new Dictionary<string, List<string>>
            {
                [InputBoundaryGates.InputAssemblyName] = new List<string> { "Some.Shim" },
                ["Some.Shim"] = new List<string> { "Sim.Contracts" },
            };

            var errs = InputBoundaryGates.ReferenceClosureViolations(
                graph, InputBoundaryGates.InputAssemblyName);

            Assert.That(errs, Has.Count.EqualTo(1),
                () => "一层 shim 必须红:\n" + string.Join("\n", errs));
            Assert.That(errs[0], Does.Contain("[A6]"));
            Assert.That(errs[0], Does.Contain("间接"), "红行须标出是间接面而非直接面");
            Assert.That(errs[0], Does.Contain("Sim.Contracts"), "红行须点名被传递穿透的装配");
            Assert.That(errs[0], Does.Contain("Some.Shim"), "红行须显示闭包证据(shim 名字)");
        }

        [Test]
        public void test_referenceClosure_directEngineRef_only_isGreen()
        {
            // 对照:引擎 / BCL 是引用图的叶(图里无节点 ⇒ 游走不展开)= 闭包绿。
            var graph = new Dictionary<string, List<string>>
            {
                [InputBoundaryGates.InputAssemblyName] = new List<string>
                    { "Unity.InputSystem", "UnityEngine", "netstandard" },
            };

            var errs = InputBoundaryGates.ReferenceClosureViolations(
                graph, InputBoundaryGates.InputAssemblyName);

            Assert.That(errs, Is.Empty, () => string.Join("\n", errs));
        }

        [Test]
        public void test_referenceClosure_missingRoot_returnsEmpty()
        {
            // 假绿防护:图里查无此装配 = 零边(不是「已扫描且无违例」)—— 空集平凡成立,
            // 端到端面另有 asmdef 缺失报红,此处只钉纯函数语义。
            var closure = InputBoundaryGates.ReferenceClosure(
                new Dictionary<string, List<string>> { ["A"] = new List<string> { "B" } }, "ZZZ");

            Assert.That(closure, Is.Empty, "查无的根 ⇒ 零边");
            var errs = InputBoundaryGates.ReferenceClosureViolations(null, "ZZZ");
            Assert.That(errs, Is.Empty, "null 图 = 零边(纯函数不吞异常,交由端到端面报红)");
        }

        [Test]
        public void test_referenceClosure_realTree_zeroErrors()
        {
            // 端到端:真工程装配图。当前树 Gameplay.Input 只经引擎面出边 ⇒ 绿。
            var graph = InputBoundaryGates.BuildProjectReferenceGraph();
            Assert.That(graph.ContainsKey(InputBoundaryGates.InputAssemblyName), Is.True,
                "工程图必须含扫描目标装配(缺 = 扫描面丢失,假绿)");

            var errs = InputBoundaryGates.CheckInputReferenceClosure();

            Assert.That(errs, Is.Empty, () => "A6 传递闭包红行:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_deliveredIntentClosure_currentTree_closedSet()
        {
            // 交出物闭集:Intents 子命名空间公开类型 ⊆ 登记集,且登记集全在(缺一 = 闭集对
            // 空集断言 = 假绿)。A6 四件 + B4 一件(上行聚合形状)。
            var errs = InputBoundaryGates.CheckDeliveredIntentClosure();

            Assert.That(errs, Is.Empty, () => string.Join("\n", errs));
            Assert.That(InputBoundaryGates.DeliveredIntentTypes, Has.Length.EqualTo(5),
                "交出物 = A6 四件(InteractIntent/EmergencyIntent/FocusNavigationIntent/" +
                "EmergencyReading)+ B4 一件(AggregatedEmergency 上行形状)");
        }

        [Test]
        public void test_deliveredIntentClosure_aggregatedEmergencyRegistered()
        {
            // B4 的上行产物必须留在闭集面内 —— 漏登 = 上行整数形状成第二真源。
            Assert.That(InputBoundaryGates.DeliveredIntentTypes,
                Does.Contain(typeof(AggregatedEmergency).FullName),
                "AggregatedEmergency(B4 上行形状)必须在交出物闭集内登记");
        }

        [Test]
        public void test_deliveredIntentClosure_realIntentsNamespace_equalsRegisteredSet()
        {
            // 真树集合相等(比「无红行」更强:逐条点名,将来有人误删/误加会直接看到差集)。
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == InputBoundaryGates.InputAssemblyName);
            Assert.That(asm, Is.Not.Null, "Gameplay.Input 必须已加载(EditMode.asmdef 引用它)");

            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }

            var intentsNs = typeof(EmergencyReading).Namespace;
            var inIntents = types
                .Where(t => (t.IsPublic || t.IsNestedPublic) && t.Namespace == intentsNs)
                .Select(t => t.FullName)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            var registered = InputBoundaryGates.DeliveredIntentTypes
                .OrderBy(n => n, StringComparer.Ordinal).ToList();

            Assert.That(inIntents, Is.EqualTo(registered),
                "Intents 子命名空间公开类型集与登记集必须逐条相等(多 = 白名单外新增;少 = 假绿)");
        }

        [Test]
        public void test_deliveredIntentClosure_extraTypeInIntentsNamespace_reportsRed()
        {
            // 负例**纯谓词**面(2026-09-26 评审 G4):原测试只对当前真树断言「不多不少」,
            // 从未把「多一个」喂进门 —— 两条红路径(白名单外新增 / 登记件缺失)零覆盖。
            // 这里的候选 = 真树四件 + 一个登记集外的影子类型 ⇒ 白名单外新增红。
            var shadow = typeof(ClosureExtraTypeFixture);
            var candidates = new[] { typeof(InteractIntent), typeof(EmergencyIntent),
                                      typeof(FocusNavigationIntent), typeof(EmergencyReading),
                                      typeof(AggregatedEmergency), shadow };

            var errs = InputBoundaryGates.DeliveredIntentClosureViolations(candidates);

            var added = errs.Where(e => e.Contains("新增公开类型")).ToList();
            Assert.That(added, Has.Count.EqualTo(1), () => string.Join("\n", errs));
            Assert.That(added[0], Does.Contain(shadow.FullName), "红行须点名影子类型");
            Assert.That(errs, Has.None.Contains("不存在"), "登记五件齐备 ⇒ 不得报缺失");
        }

        [Test]
        public void test_deliveredIntentClosure_missingRegisteredType_reportsRed()
        {
            // 缺失分支 = 「闭集对空集断言 = 假绿」的唯一护栏。候选少一件 ⇒ 红,
            // 且红行须点名**缺的那一件**(不是泛泛说闭集不完整)。
            var candidates = new[] { typeof(InteractIntent), typeof(EmergencyIntent),
                                      typeof(FocusNavigationIntent) };   // 缺 Reading + Aggregated

            var errs = InputBoundaryGates.DeliveredIntentClosureViolations(candidates);

            var missing = errs.Where(e => e.Contains("不存在")).ToList();
            Assert.That(missing, Has.Count.EqualTo(2),
                () => "缺两件 = 两条红:\n" + string.Join("\n", errs));
            Assert.That(missing[0], Does.Contain("EmergencyReading"));
            Assert.That(missing[1], Does.Contain("AggregatedEmergency"));
            Assert.That(missing[0], Does.Contain("假绿"), "红行须写明后果(防人当噪声忽略)");
        }

        [Test]
        public void test_deliveredIntentClosure_nullCandidates_onlyReportsMissing()
        {
            // null 候选集 = 零公开类型 ⇒ 五件全报缺失(不静默绿)。
            var errs = InputBoundaryGates.DeliveredIntentClosureViolations(null);

            Assert.That(errs, Has.Count.EqualTo(InputBoundaryGates.DeliveredIntentTypes.Length));
        }

        [Test]
        public void test_inputSourceText_currentTree_zeroSimEventNoParse()
        {
            // 端到端源文本面:Gameplay.Input 源树零 `SimEvent` token(注释剥离后)、
            // 零 `FixParse.Parse(`。
            var errs = InputBoundaryGates.CheckInputSourceText();

            Assert.That(errs, Is.Empty, () => "B3 源文本红行:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_inputSourceText_iEventSinkAppendCall_reportsRed()
        {
            // 故事 QA 负例:「夹具源码出现 IEventSink.Append 调用 ⇒ 红」。
            // 兑现形态:合成源文本**直接驱动门自己的纯谓词** SourceTextViolations
            // (2026-09-26 评审 G2 修复:原写法把剥离 + 匹配抄进测试,再断言抄的那份命中
            //  = 门若改坏,测试照绿 —— 门与测试不共用判定代码,就等于没测门)。
            const string fixture =
                "using DaYiJingCheng.Sim.Contracts;\n" +
                "class X { void M(IEventSink s) { s.Append(new SimEvent()); } }";

            var errs = InputBoundaryGates.SourceTextViolations(fixture, "<G2-IEventSinkFixture>");

            // 两个 token 各自成行:IEventSink(写入通道)+ SimEvent(事件本体)
            Assert.That(errs, Has.Count.EqualTo(2),
                () => "本负例只测事件面,应恰 2 条(IEventSink + SimEvent):\n" + string.Join("\n", errs));
            Assert.That(errs[0], Does.Contain("[B3]"));
            Assert.That(errs[0], Does.Contain("IEventSink"), "红行须点名违例 token");
            Assert.That(errs[1], Does.Contain("SimEvent"));
        }

        [Test]
        public void test_inputSourceText_sinkAppendWithoutSimEventLiteral_reportsRed()
        {
            // G2 的核心失效场景:`s.Append(SomePayload.Make())` 这类**不出现 SimEvent 字面**
            // 的写法(只经 IEventSink 写事件)在补 token 前全绿通过。补 IEventSink 后必红。
            const string fixture =
                "class W { void M(IEventSink s) { s.Append(SomePayload.Make()); } }";

            var errs = InputBoundaryGates.SourceTextViolations(fixture, "<G2-AppendOnlyFixture>");

            Assert.That(errs, Has.Count.EqualTo(1),
                () => "不携 SimEvent 字面也应被 IEventSink token 抓住:\n" + string.Join("\n", errs));
            Assert.That(errs[0], Does.Contain("IEventSink"));
        }

        [Test]
        public void test_inputSourceText_fixParseCall_reportsRed()
        {
            // 故事 QA:AC-3-B3「运行期不经 FixParse.Parse(string)」。负例源文本命中扫描键。
            const string fixture = "class Y { void M() { var v = FixParse.Parse(\"3/4\"); } }";

            var errs = InputBoundaryGates.SourceTextViolations(fixture, "<B3-FixParseFixture>");

            Assert.That(errs, Has.Count.EqualTo(1),
                () => "本负例只测 FixParse 面:\n" + string.Join("\n", errs));
            Assert.That(errs[0], Does.Contain("[B3]"));
            Assert.That(errs[0], Does.Contain(InputBoundaryGates.FixParseParseMarker));
            Assert.That(errs[0], Does.Contain("<B3-FixParseFixture>"), "红行须点名违例文件");
        }

        [Test]
        public void test_inputSourceText_commentMention_notFlagged()
        {
            // 注释里的 SimEvent / FixParse.Parse( 不算(注释剥离先例,同 b5)。
            const string fixture =
                "// 3 零 SimEvent;判定结果不经 FixParse.Parse( —— 见 AC-3-A6 / B3\n" +
                "/* 块注释 SimEvent 同禁 */\n" +
                "class Z { int a; }";

            var errs = InputBoundaryGates.SourceTextViolations(fixture, "<CommentFixture>");

            Assert.That(errs, Is.Empty,
                () => "注释剥离后零红行,实测:\n" + string.Join("\n", errs));
        }

        // ═══════════════ AC-3-A7 —— 载荷可达闭包递归零浮点 ═══════════════

        [Test]
        public void test_payloadClosure_allRoots_currentTree_zeroFloats()
        {
            // 端到端:Sim.Contracts 全部 *Payload struct(ADR-024 真源 34 支)+ Intents
            // 交出物五件,逐根递归扫。零 float/double。
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False,
                "跑 A7 端到端前提:编译成功");

            var errs = InputBoundaryGates.CheckAllPayloadClosures(out var roots);

            Assert.That(errs, Is.Empty, () => "A7 红行:\n" + string.Join("\n", errs));
            Assert.That(roots, Is.GreaterThanOrEqualTo(30),
                $"A7 扫描根数 = {roots},应覆盖 Sim.Contracts 载荷族(30+ 支)+ Intents 五件");
        }

        [Test]
        public void test_payloadClosure_allRoots_zeroHits_isNotTrivialEmpty()
        {
            // 假绿防护:根数 0(扫描面丢失)必须可观测 —— 门以红错报「装配未加载」,
            // 这里补一条:当前树根数恒 ≥ 阈值,防止未来扫描键失效仍报绿。
            InputBoundaryGates.CheckAllPayloadClosures(out var roots);
            Assert.That(roots, Is.GreaterThan(0), "扫描根为 0 = 扫描面丢失(假绿)");
        }

        [Test]
        public void test_payloadClosure_nestedStructFloat_reportsRed()
        {
            // 故事 QA 负例(自证):`struct Outer { Inner i; } struct Inner { float x; }` ⇒ 红。
            // 顶层扫描看不见 Inner —— 这正是 AC-3-A7「必须递归」的存在理由。
            var errs = InputBoundaryGates.CheckPayloadClosure(typeof(NestedFloatOuter));

            Assert.That(errs, Is.Not.Empty, "内嵌 struct 的 float 必红(A7 递归)");
            Assert.That(errs[0], Does.Contain("[A7]"));
            Assert.That(errs[0], Does.Contain("System.Single"), "别名面须点名(别名同拒)");
            Assert.That(errs[0], Does.Contain("NestedFloatOuter.Inner.X"),
                "须可定位到具体叶子路径(根.嵌套字段.叶字段)");
        }

        [Test]
        public void test_payloadClosure_arrayElementFloat_reportsRed()
        {
            var errs = InputBoundaryGates.CheckPayloadClosure(typeof(ArrayFloatFixture));

            Assert.That(errs, Is.Not.Empty, "数组元素类型里的 float 必红(元素类型须展开)");
            Assert.That(errs[0], Does.Contain("System.Single"));
        }

        [Test]
        public void test_payloadClosure_listElementFloat_reportsRed()
        {
            // 故事 QA 边界例:List<T> 元素类型。泛型实参照走 = 不展开则漏报。
            var errs = InputBoundaryGates.CheckPayloadClosure(typeof(ListFloatFixture));

            Assert.That(errs, Is.Not.Empty, "List<T> 元素里的 float 必红(泛型实参须展开)");
            Assert.That(errs[0], Does.Contain("System.Single"));
        }

        [Test]
        public void test_payloadClosure_baseClassPrivateFloat_reportsRed()
        {
            // 故事 QA 边界例:继承链基类字段(DeclaredOnly 链逐层展开,含 private)。
            var errs = InputBoundaryGates.CheckPayloadClosure(typeof(DerivedFloatFixture));

            Assert.That(errs, Is.Not.Empty, "基类 private float 字段必红");
            Assert.That(errs[0], Does.Contain("System.Single"));
        }

        [Test]
        public void test_payloadClosure_fixField_withToFloatMethod_isGreen()
        {
            // 2026-09-26 评审修复(门面收窄的**正向**兑现):载荷持有 `Fix` 字段,而 `Fix` 类型
            // 上挂着 `ToFloat():float` —— ADR-006 §五 钉死的**唯一**浮点出口。
            // 门只扫**数据面**(字段/属性),故此项绿;若有人把方法返回值加回扫描面,
            // 本测红(真树恒红 = 判据不可修 ⇒ 是设计错误的信号,不是待修的缺陷)。
            var errs = InputBoundaryGates.CheckPayloadClosure(typeof(FixFieldPayloadFixture));

            Assert.That(errs, Is.Empty, () => string.Join("\n", errs));
            Assert.That(typeof(DaYiJingCheng.Sim.Contracts.Fix)
                .GetMethod("ToFloat", BindingFlags.Public | BindingFlags.Instance),
                Is.Not.Null, "前提:Fix 确有 ToFloat() 方法(ADR-006 §五 唯一浮点出口)");
        }

        [Test]
        public void test_payloadClosure_pureIntegerTree_isGreen()
        {
            // 正向对照:全整数嵌套 + 数组 + 泛型 ⇒ 绿(证明谓词不误杀)。
            var errs = InputBoundaryGates.CheckPayloadClosure(typeof(PureIntegerFixture));

            Assert.That(errs, Is.Empty, () => string.Join("\n", errs));
        }

        [Test]
        public void test_payloadClosure_nullRoot_reportsRed()
        {
            var errs = InputBoundaryGates.CheckPayloadClosure(null);
            Assert.That(errs, Has.Count.EqualTo(1), "null 根 = 红(拒以空集冒充绿)");
            Assert.That(errs[0], Does.Contain("null"));
        }

        [Test]
        public void test_isFloatLeaf_primitivesAndFixedPoint()
        {
            // 判据面自证。**别名面**:`System.Single` / `System.Double` 在 C# 里是
            // `float` / `double` 的**同一 Type 对象**(编译器直接归一)⇒ 写成别名
            // `System.Single` 的字段与写 `float` 的字段拿到同一个 Type,判据必然命中 ——
            // 「别名同拒」是**结构性成立**,不是靠字符串匹配补救。
            Assert.That(typeof(System.Single), Is.EqualTo(typeof(float)),
                "System.Single 与 float 是同一 Type(别名不可能绕开判据)");
            Assert.That(typeof(System.Double), Is.EqualTo(typeof(double)),
                "System.Double 与 double 是同一 Type");

            Assert.That(InputBoundaryGates.IsFloatLeaf(typeof(float)), Is.True);
            Assert.That(InputBoundaryGates.IsFloatLeaf(typeof(double)), Is.True);
            Assert.That(InputBoundaryGates.IsFloatLeaf(typeof(int)), Is.False);
            Assert.That(InputBoundaryGates.IsFloatLeaf(typeof(long)), Is.False);
            Assert.That(InputBoundaryGates.IsFloatLeaf(typeof(bool)), Is.False);
            Assert.That(InputBoundaryGates.IsFloatLeaf(typeof(DaYiJingCheng.Sim.Contracts.Fix)),
                Is.False, "Fix 是 Q16.16 定点整数表示,非浮点(ADR-006)");
        }

        [Test]
        public void test_floatAliasField_reportsRed()
        {
            // 别名写法(`System.Single` 而非 `float`)的字段面负例:必红。
            var errs = InputBoundaryGates.CheckPayloadClosure(typeof(FloatAliasFixture));

            Assert.That(errs, Is.Not.Empty, "System.Single 别名字段必红");
            Assert.That(errs[0], Does.Contain("System.Single"), "红行点名归一后的类型名");
        }

        [Test]
        public void test_payloadClosure_interfaceField_isGreen_byDeclaredScope()
        {
            // 故事 QA 边界例:「接口字段 / 多态载荷」—— **刻意判绿**,A7 只问「闭包里
            // 有没有浮点」,接口字段类型本身非 float ⇒ 绿(与 PresentationDtoGuard 同当)。
            // 本测的作用不是证绿,而是把**这个选择**钉死:接口字段里藏一个 float
            // 实现类,门看不见 —— 日后若有人把扫描面扩为实现枚举,本测会红,提醒
            // 同步改门内注释与本决策记录,而不是悄悄换判据。
            var errs = InputBoundaryGates.CheckPayloadClosure(typeof(InterfaceFieldFixture));

            Assert.That(errs, Is.Empty,
                () => "A7 声明面 = 不经接口实现枚举展开,故绿:\n" + string.Join("\n", errs));
            Assert.That(typeof(PolymorphicFloatImpl).GetField("X", BindingFlags.Public | BindingFlags.Instance)
                                             .FieldType, Is.EqualTo(typeof(float)),
                "前提:实现类里确实藏着 float(否则本测就是为真而真,夹具落空)");
        }

        [Test]
        public void test_payloadClosure_concretePolymorphicField_reportsRed()
        {
            // 对照面:字段类型写成**具体**类(不经接口)⇒ 递归抓得到 ⇒ 红。
            // 与上一测并置 = 门确实只按声明面断,不是整体失灵。
            var errs = InputBoundaryGates.CheckPayloadClosure(typeof(ConcreteFieldFixture));

            Assert.That(errs, Is.Not.Empty, "具体多态字段必红");
            Assert.That(errs[0], Does.Contain("System.Single"));
        }

        // ═══════════════ AC-3-B3 —— 判定结果全整数 ═══════════════

        [Test]
        public void test_readingFields_emergencyReading_withinIntegerAllowlist()
        {
            // 门不引用 Gameplay.Input ⇒ 类型由本测试装配供给(见文件头注)。
            var errs = InputBoundaryGates.CheckReadingFieldLeaves(typeof(EmergencyReading));

            Assert.That(errs, Is.Empty, () => "B3 红行:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_readingFields_aggregatedEmergency_withinIntegerAllowlist()
        {
            // B4 的上行整数记录同样受 B3 约束(它是 3 交出的判定输入形状)。
            var errs = InputBoundaryGates.CheckReadingFieldLeaves(typeof(AggregatedEmergency));

            Assert.That(errs, Is.Empty, () => string.Join("\n", errs));
        }

        [Test]
        public void test_readingFields_emergencyIntent_withinIntegerAllowlist()
        {
            var errs = InputBoundaryGates.CheckReadingFieldLeaves(typeof(EmergencyIntent));

            Assert.That(errs, Is.Empty, () => string.Join("\n", errs));
        }

        [Test]
        public void test_readingFields_floatMagnitudeFixture_reportsRed()
        {
            // 故事 QA 负例:夹具字段 `float magnitude` ⇒ 红。
            var errs = InputBoundaryGates.CheckReadingFieldLeaves(typeof(FloatMagnitudeReadingFixture));

            Assert.That(errs, Is.Not.Empty, "float magnitude 必红(AC-3-B3)");
            Assert.That(errs[0], Does.Contain("[B3]"));
            Assert.That(errs[0], Does.Contain(".Magnitude"), "须点名违例字段路径(根.字段)");
            Assert.That(errs[0], Does.Contain("System.Single"), "别名面须点名");
        }

        [Test]
        public void test_readingFields_nestedStructFloat_reportsRed()
        {
            // 故事 QA:「EmergencyReading 内嵌结构体字段同样递归(承 A7 纪律)」。
            var errs = InputBoundaryGates.CheckReadingFieldLeaves(typeof(NestedReadingFixture));

            Assert.That(errs, Is.Not.Empty, "内嵌 struct 的 float 必红(B3 递归)");
            Assert.That(errs[0], Does.Contain("System.Single"));
        }

        [Test]
        public void test_readingFields_doubleField_reportsRed()
        {
            var errs = InputBoundaryGates.CheckReadingFieldLeaves(typeof(DoubleFieldReadingFixture));

            Assert.That(errs, Is.Not.Empty, "double 字段必红");
            Assert.That(errs[0], Does.Contain("System.Double"));
        }

        [Test]
        public void test_readingFields_stringField_reportsRed()
        {
            // G5 的核心失效场景:白名单自己说 string 不合格,但门原先把 string 展开成
            // char[] 逐项判 —— char/int 全绿 ⇒ **零红行** = 自相矛盾。收窄后必红。
            var errs = InputBoundaryGates.CheckReadingFieldLeaves(typeof(StringFieldReadingFixture));

            Assert.That(errs, Is.Not.Empty, "string 字段必红(B3 白名单闭合)");
            var hit = errs.First(e => e.Contains("System.String"));
            Assert.That(hit, Does.Contain("[B3]"));
            Assert.That(hit, Does.Contain(".Name"), "须点名违例字段路径");
            Assert.That(hit, Does.Contain("闭合"), "红行须写明白名单是闭合的(不是建议)");
        }

        [Test]
        public void test_readingFields_objectField_reportsRed()
        {
            // 同 G5 的第二个形态:`object` 字段 = 任意装箱载荷的逃逸口。
            var errs = InputBoundaryGates.CheckReadingFieldLeaves(typeof(ObjectFieldReadingFixture));

            Assert.That(errs, Is.Not.Empty, "object 字段必红");
            Assert.That(errs[0], Does.Contain("System.Object"));
        }

        [Test]
        public void test_readingFields_emergencyReading_intArrayField_recursesToElement()
        {
            // 正向对照:int[] 元素 = int ⇒ 绿(证明「数组元素递归」没有把合法面误杀)。
            var errs = InputBoundaryGates.CheckReadingFieldLeaves(typeof(EmergencyReading));

            Assert.That(errs, Is.Empty, () => string.Join("\n", errs));
        }

        [Test]
        public void test_readingFields_nullRoot_reportsRed()
        {
            var errs = InputBoundaryGates.CheckReadingFieldLeaves(null);
            Assert.That(errs, Has.Count.EqualTo(1));
            Assert.That(errs[0], Does.Contain("null"));
        }

        [Test]
        public void test_allowedReadingLeaf_coversIntLongBoolFixAndEnum()
        {
            // 白名单面自证:int / long / bool / Fix / 枚举 = 合格叶子;float / double / string 红。
            Assert.That(InputBoundaryGates.IsAllowedReadingLeaf(typeof(int)), Is.True);
            Assert.That(InputBoundaryGates.IsAllowedReadingLeaf(typeof(long)), Is.True);
            Assert.That(InputBoundaryGates.IsAllowedReadingLeaf(typeof(bool)), Is.True);
            Assert.That(InputBoundaryGates.IsAllowedReadingLeaf(typeof(DaYiJingCheng.Sim.Contracts.Fix)),
                Is.True, "Fix = Q16.16 整数定点,合法");
            Assert.That(InputBoundaryGates.IsAllowedReadingLeaf(typeof(OrdinalEnumFixture)), Is.True,
                "枚举 = 序数语义,合法");
            Assert.That(InputBoundaryGates.IsAllowedReadingLeaf(typeof(float)), Is.False);
            Assert.That(InputBoundaryGates.IsAllowedReadingLeaf(typeof(double)), Is.False);
            Assert.That(InputBoundaryGates.IsAllowedReadingLeaf(typeof(string)), Is.False,
                "string 不是整数域叶子");
        }

        [Test]
        public void test_emergencyReading_ctor_assertsEdgeCountMatchesArray()
        {
            // 规则一:edges == edge_ticks.Length(构造期断言,不等即抛)。
            Assert.Throws<ArgumentException>(() =>
                new EmergencyReading(0, 0, 2, new[] { 1 }, 0));
            Assert.DoesNotThrow(() =>
                new EmergencyReading(0, 0, 1, new[] { 1 }, 0));
        }

        [Test]
        public void test_emergencyReading_ctor_rejectsNullEdgeTicks()
        {
            Assert.Throws<ArgumentException>(() => new EmergencyReading(0, 0, 0, null, 0));
        }

        [Test]
        public void test_inputAssembly_declaresNoEventContractType()
        {
            // A6 的「零 SimEvent」在**类型面**的正向兑现:Gameplay.Input 装配内不存在
            // 任何 Sim 契约类型(IEventSink / SimEvent / *Payload)作为字段或参数。
            // 引用集面已由 CheckInputAssemblyReferences 断;本条断「类型面零契约类型」。
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == InputBoundaryGates.InputAssemblyName);
            Assert.That(asm, Is.Not.Null);

            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }

            const BindingFlags fb = BindingFlags.DeclaredOnly | BindingFlags.Instance |
                                     BindingFlags.Static | BindingFlags.Public |
                                     BindingFlags.NonPublic;
            var hits = new List<string>();
            foreach (var t in types)
            {
                foreach (var f in t.GetFields(fb))
                    if (IsSimContractType(f.FieldType)) hits.Add($"{t.FullName}.{f.Name}");
                foreach (var m in t.GetMethods(fb))
                {
                    if (IsSimContractType(m.ReturnType)) hits.Add($"{t.FullName}.{m.Name}():ret");
                    foreach (var p in m.GetParameters())
                        if (IsSimContractType(p.ParameterType))
                            hits.Add($"{t.FullName}.{m.Name}({p.Name})");
                }
            }
            Assert.That(hits, Is.Empty, () => "3 持有 sim 契约类型:\n" + string.Join("\n", hits));
        }

        private static bool IsSimContractType(Type t)
        {
            if (t == null) return false;
            if (t.IsArray || t.IsByRef || t.IsPointer) return IsSimContractType(t.GetElementType());
            if (t.IsGenericType)
                return t.GetGenericArguments().Any(IsSimContractType);
            var ns = t.Namespace;
            return ns != null && ns.StartsWith("DaYiJingCheng.Sim", StringComparison.Ordinal);
        }

        // ═══════════════ AC-3-B4 —— C 路聚合:恰一条 / 零条 / 中止不发 ═══════════════

        [Test]
        public void test_aggregator_noActionInPeriod_zeroUplink()
        {
            // QA 边界例:「单周期 0 次动作 ⇒ 0 条上行(不发空帧)」。
            var agg = new EmergencyAggregator();

            var result = agg.EndAttempt();

            Assert.That(result.HasValue, Is.False, "零样本周期不得产出任何上行(0 条)");
        }

        [Test]
        public void test_aggregator_oneAttemptManyFrames_exactlyOneUplink()
        {
            // QA 主路径 + 负例:「实现改成逐帧上行 ⇒ 恰一条红」。
            // 逐帧 5 个样本(Sample 5 次)→ EndAttempt 恰一条;再 EndAttempt 归零。
            var agg = new EmergencyAggregator();
            agg.Sample(Reading(action: 1, hold: 2, new[] { 10 }, mag: 100));
            agg.Sample(Reading(action: 1, hold: 4, new[] { 10 }, mag: 220));
            agg.Sample(Reading(action: 1, hold: 6, new[] { 10, 14 }, mag: 180));
            agg.Sample(Reading(action: 1, hold: 8, new[] { 10, 14 }, mag: 260));
            agg.Sample(Reading(action: 1, hold: 10, new[] { 10, 14, 18 }, mag: 240));

            var first = agg.EndAttempt();
            var second = agg.EndAttempt();

            Assert.That(first.HasValue, Is.True, "有样本 ⇒ 恰一条");
            Assert.That(second.HasValue, Is.False, "EndAttempt 后归零(第二次 = 0 条,不得重复发)");
        }

        [Test]
        public void test_aggregator_holdTicks_isLastEdgeMinusFirstEdge()
        {
            // F-10.5:hold_ticks = 最后一个边沿 tick − 第一个边沿 tick。
            var agg = new EmergencyAggregator();
            agg.Sample(Reading(action: 2, hold: 0, new[] { 7 }, mag: 10));
            agg.Sample(Reading(action: 2, hold: 9, new[] { 7, 19 }, mag: 30));

            var aggOut = agg.EndAttempt();

            Assert.That(aggOut.HasValue, Is.True);
            Assert.That(aggOut.Value.HoldTicks, Is.EqualTo(12), "19 − 7 = 12");
            Assert.That(aggOut.Value.Edges, Is.EqualTo(2));
            Assert.That(aggOut.Value.EdgeTicks, Is.EqualTo(new[] { 7, 19 }));
        }

        [Test]
        public void test_aggregator_singleEdge_holdIsZero()
        {
            var agg = new EmergencyAggregator();
            agg.Sample(Reading(action: 0, hold: 5, new[] { 42 }, mag: 50));

            var out0 = agg.EndAttempt();

            Assert.That(out0.HasValue, Is.True);
            Assert.That(out0.Value.HoldTicks, Is.EqualTo(0), "单沿 ⇒ 末沿 = 首沿 ⇒ 持时 0");
        }

        [Test]
        public void test_aggregator_magPeak_isMax_magLast_isFinal()
        {
            // F-10.5:mag_peak = max(magnitude[t]) → 幅度门;mag_last = magnitude[t_end]
            // → 预表现定格,**不进任何门**。
            var agg = new EmergencyAggregator();
            agg.Sample(Reading(action: 1, hold: 1, new[] { 3 }, mag: 120));
            agg.Sample(Reading(action: 1, hold: 2, new[] { 3 }, mag: 300));
            agg.Sample(Reading(action: 1, hold: 3, new[] { 3 }, mag: 90));

            var out0 = agg.EndAttempt();

            Assert.That(out0.Value.MagPeak, Is.EqualTo(300), "峰值 = max 算子");
            Assert.That(out0.Value.MagLast, Is.EqualTo(90), "末值 = 结束瞬时幅度(≠ peak)");
        }

        [Test]
        public void test_aggregator_failedAction_stillAggregates()
        {
            // QA 边界例:「失败动作(判定前中止)⇒ 仍聚合(判定输入语义,主机才有权判失败)」
            // ⇒ 幅度低 / 沿少 / 持时短 = 表现差,但 3 侧**照样**发一条;优劣由主机判。
            var agg = new EmergencyAggregator();
            agg.Sample(Reading(action: 3, hold: 0, new[] { 1 }, mag: 1));
            agg.Sample(Reading(action: 3, hold: 1, new[] { 1 }, mag: 2));

            var out0 = agg.EndAttempt();

            Assert.That(out0.HasValue, Is.True, "表现糟糕也发一条(3 无判定权)");
            Assert.That(out0.Value.MagPeak, Is.EqualTo(2), "劣化幅度照样上行 —— 由主机判失败");
        }

        [Test]
        public void test_aggregator_systemAbort_emitsNothing()
        {
            // 规则六之甲(系统级中止,如 Armed 内持续无边沿达 ABORT_IDLE_TICKS):
            // 丢弃累计、**不发**任何上行(中止 ≠ 失败动作)。
            var agg = new EmergencyAggregator();
            agg.Sample(Reading(action: 1, hold: 3, new[] { 5 }, mag: 200));
            agg.AbortAttempt();

            var out0 = agg.EndAttempt();

            Assert.That(out0.HasValue, Is.False, "中止后不得产出上行(0 条)");
        }

        [Test]
        public void test_aggregator_abortThenNewAttempt_startsClean()
        {
            // 中止后重开新动作:累计态必须清干净(不串前一次动作的沿/幅度)。
            var agg = new EmergencyAggregator();
            agg.Sample(Reading(action: 1, hold: 5, new[] { 100, 200 }, mag: 500));
            agg.AbortAttempt();

            agg.Sample(Reading(action: 4, hold: 2, new[] { 7 }, mag: 60));
            var out0 = agg.EndAttempt();

            Assert.That(out0.Value.Action, Is.EqualTo(4), "新动作身份");
            Assert.That(out0.Value.Edges, Is.EqualTo(1), "沿不得串前一次");
            Assert.That(out0.Value.MagPeak, Is.EqualTo(60), "幅度不得串前一次");
            Assert.That(out0.Value.HoldTicks, Is.EqualTo(0));
        }

        [Test]
        public void test_aggregator_nonMonotonicEdgeTick_throws()
        {
            // F-10.5 附带口径 + AC-3-B3:edge_ticks 单调非递减是 **3 侧的运行时断言义务**,
            // 不静默吸收。
            var agg = new EmergencyAggregator();
            agg.Sample(Reading(action: 1, hold: 1, new[] { 30 }, mag: 10));

            var ex = Assert.Throws<ArgumentException>(() =>
                agg.Sample(Reading(action: 1, hold: 2, new[] { 30, 20 }, mag: 10)));

            Assert.That(ex!.Message, Does.Contain("单调"));
        }

        [Test]
        public void test_aggregator_actionChangeMidAttempt_throws()
        {
            // 一个 Armed 期只武装一个动作;换动作须先 EndAttempt / AbortAttempt。
            var agg = new EmergencyAggregator();
            agg.Sample(Reading(action: 1, hold: 1, new[] { 5 }, mag: 10));

            var ex = Assert.Throws<ArgumentException>(() =>
                agg.Sample(Reading(action: 2, hold: 2, new[] { 5 }, mag: 10)));

            Assert.That(ex!.Message, Does.Contain("动作身份"));
        }

        [Test]
        public void test_aggregator_sampleArrayIsCumulativeView_notDelta()
        {
            // 直读通道每帧交**累计视图**数组(实现侧可复用同一数组)。聚合器按差值追加,
            // 不依赖同一实例 ⇒ 换实例喂同样的累计序列,结果必须一致。
            var aggA = new EmergencyAggregator();
            aggA.Sample(Reading(action: 1, hold: 1, new[] { 10 }, mag: 5));
            aggA.Sample(Reading(action: 1, hold: 2, new[] { 10, 11 }, mag: 6));
            var outA = aggA.EndAttempt();

            var aggB = new EmergencyAggregator();
            var shared = new[] { 10 };
            aggB.Sample(Reading(action: 1, hold: 1, shared, mag: 5));
            Array.Resize(ref shared, 2);
            shared[1] = 11;
            aggB.Sample(Reading(action: 1, hold: 2, shared, mag: 6));
            var outB = aggB.EndAttempt();

            Assert.That(outB.Value.Edges, Is.EqualTo(outA.Value.Edges));
            Assert.That(outB.Value.EdgeTicks, Is.EqualTo(outA.Value.EdgeTicks));
            Assert.That(outB.Value.HoldTicks, Is.EqualTo(outA.Value.HoldTicks));
        }

        [Test]
        public void test_aggregator_result_isSnapshot_notLiveView()
        {
            // 聚合产物是**快照**:EndAttempt 后再喂新动作,旧产物的沿数组不得被改动
            // (否则已上行的整数形状会被后续动作污染 = 流内容失真)。
            var agg = new EmergencyAggregator();
            agg.Sample(Reading(action: 1, hold: 1, new[] { 5 }, mag: 10));
            var out0 = agg.EndAttempt();

            agg.Sample(Reading(action: 1, hold: 2, new[] { 9 }, mag: 20));
            agg.EndAttempt();

            Assert.That(out0.Value.EdgeTicks, Is.EqualTo(new[] { 5 }),
                "旧产物是被冻结的快照");
            Assert.That(out0.Value.MagPeak, Is.EqualTo(10));
        }

        [Test]
        public void test_aggregator_neverTouchesSimAssembly()
        {
            // B4 负例:「本地 Judge 写流 ⇒ 无 Append 可达红」。兑现形态 = 聚合器与
            // 交出物类型**不含任何 Sim 契约类型**(已在 A6 类型面断言过),此处断
            // 聚合器自身的方法签名 / 字段面。
            const BindingFlags fb = BindingFlags.DeclaredOnly | BindingFlags.Instance |
                                     BindingFlags.Static | BindingFlags.Public |
                                     BindingFlags.NonPublic;
            foreach (var m in typeof(EmergencyAggregator).GetMethods(fb))
            {
                Assert.That(IsSimContractType(m.ReturnType), Is.False,
                    $"{m.Name} 返回 sim 契约类型 = 写流能力");
                foreach (var p in m.GetParameters())
                    Assert.That(IsSimContractType(p.ParameterType), Is.False,
                        $"{m.Name}({p.Name}) 吃进 sim 契约类型 = 写流能力");
            }
            foreach (var f in typeof(EmergencyAggregator).GetFields(fb))
                Assert.That(IsSimContractType(f.FieldType), Is.False,
                    $"聚合器持有 sim 契约类型 {f.Name}");
        }

        [Test]
        public void test_aggregator_uplinkMatchesSimContractsPayloadShape()
        {
            // B4 形状对齐:聚合产物的六个整数/数组面与 Sim.Contracts 的
            // EmergencyAttemptPayload 同形(10 在其边界补 Method / ActorId 后即可 Append)。
            // 本断言只做**结构对齐**(名与量纲),不构造载荷 —— 载荷构造归 10。
            var fields = typeof(AggregatedEmergency)
                .GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public |
                           BindingFlags.Instance)
                .Where(f => !f.IsStatic)
                .ToDictionary(f => f.Name, f => f.FieldType, StringComparer.Ordinal);

            Assert.That(fields.ContainsKey("Action"), Is.True, "action 面");
            Assert.That(fields.ContainsKey("HoldTicks"), Is.True, "hold_ticks 面");
            Assert.That(fields.ContainsKey("Edges"), Is.True, "edges 面");
            Assert.That(fields.ContainsKey("EdgeTicks"), Is.True, "edge_ticks[] 面");
            Assert.That(fields.ContainsKey("MagPeak"), Is.True, "mag_peak 面(幅度门)");
            Assert.That(fields.ContainsKey("MagLast"), Is.True, "mag_last 面(预表现定格)");

            foreach (var kv in fields)
                Assert.That(InputBoundaryGates.IsAllowedReadingLeaf(kv.Value) ||
                            kv.Value == typeof(int[]),
                    Is.True, $"{kv.Key} 类型 {kv.Value.FullName} 须在全整数域");
        }

        // ── 读数构造助手(单帧样本;edges 必 == edgeTicks.Length)──
        private static EmergencyReading Reading(int action, int hold, int[] edgeTicks, int mag)
            => new EmergencyReading(action, hold, edgeTicks.Length, edgeTicks, mag);

        // ═══════════════ 负向夹具(住本测试装配,不污染生产扫描面)═══════════════

        private enum OrdinalEnumFixture
        {
            Zero = 0,
            One = 1,
        }

        private struct FloatAliasFixture
        {
            // 显式别名 System.Single —— 与 float 同一 Type 对象(编译器归一),
            // 故判据命中是结构性成立,不是靠字符串匹配补救。
            public System.Single X;
        }

        // A7 负例组
        private struct NestedFloatInner
        {
            public float X;
        }

        private struct NestedFloatOuter
        {
            public NestedFloatInner Inner;
        }

        private struct ArrayFloatFixture
        {
            public NestedFloatInner[] Items;
        }

        private sealed class ListFloatFixture
        {
            public List<NestedFloatInner> Items = new List<NestedFloatInner>();
        }

        // 载荷持 `Fix` 字段的真形副本(形如 DrugTreatmentAppliedPayload.DrugPotency):
        // Fix 上挂着 ToFloat():float,数据面绿 —— 门面收窄的正向兑现面。
        private struct FixFieldPayloadFixture
        {
            public DaYiJingCheng.Sim.Contracts.Fix Potency;
        }

        private class BaseFloatFixture
        {
            private float _hidden;
        }

        private sealed class DerivedFloatFixture : BaseFloatFixture
        {
            public int Ok;
        }

        private struct PureIntegerInner
        {
            public int A;
            public long B;
            public DaYiJingCheng.Sim.Contracts.Fix C;
        }

        private sealed class PureIntegerFixture
        {
            public PureIntegerInner One;
            public PureIntegerInner[] Many;
            public List<PureIntegerInner> Listed = new List<PureIntegerInner>();
            public int Ticks;
        }

        // B3 负例组
        private struct FloatMagnitudeReadingFixture
        {
            public int Action;
            public float Magnitude;
        }

        private struct NestedReadingInner
        {
            public float Duration;
        }

        private struct NestedReadingFixture
        {
            public int Action;
            public NestedReadingInner Nested;
        }

        private struct DoubleFieldReadingFixture
        {
            public double Score;
        }

        // G5 负例:白名单闭合面(string / object 都不得作为读数字段)
        private struct StringFieldReadingFixture
        {
            public int Action;
            public string Name;
        }

        private struct ObjectFieldReadingFixture
        {
            public object Boxed;
        }

        // G4 负例:闭集谓词面的「白名单外新增」影子类型(不属 Intents 子命名空间,
        // 只作为候选喂纯函数 —— 不会污染真树闭集断言)
        private struct ClosureExtraTypeFixture
        {
            public int A;
        }

        // G1 组:接口字段 vs 具体多态字段(声明面差异的可观测对照)
        private interface IPolymorphicField
        {
        }

        private sealed class PolymorphicFloatImpl : IPolymorphicField
        {
            public float X;
        }

        private struct InterfaceFieldFixture
        {
            public IPolymorphicField Field;
        }

        private struct ConcreteFieldFixture
        {
            public PolymorphicFloatImpl Field;
        }
    }
}
