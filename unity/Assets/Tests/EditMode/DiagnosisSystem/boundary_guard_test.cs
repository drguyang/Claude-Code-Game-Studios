// diagnosis-system Story 001 —— 边界守门夹具(required evidence)。
//
// 权威来源:
//   Story: production/epics/diagnosis-system/story-001-boundary-vitalsdto-float-guardrails.md
//     · AC-8-1(I)零写入:全流程脚本化 ⇒ Append 计数 0;流哈希 ≡ 不经 8 的对照跑;
//       静态守门 = 零 Publish 调用点
//     · AC-8-2(L)引用面:Cecil 扫 8 程序集 TypeRef ⇒ 零 sim 内部类型;浮点入口仅 ToFloat
//     · AC-8-3(L/I)双进程逐字:纯函数入口 + 独立实例(Implementation Note 3,
//       不引多进程框架)⇒ 输出逐字相同;字段反射遍历 ⇒ 无跨调用累积量 / 零 tick 持久字段
//     · AC-8-4 8 侧:零 8→11 数据边(11 侧反射半边归 prescription story-003,括注)
//     · 铁律③/④:零持久化 API 调用点;EmitGrowth 唯一出口形状(IL 调用点恰 = 1)
//     · AC-8-6:零 libm 超越函数(IL + 源双层);内部标量一律 System.Single(无 double 混算)
//     · TR-diag-019/020 前置:PresentationDtoGuard 挂入 8 的全部呈现 DTO
//   GDD diagnosis-system.md 边界五条铁律 · F-8.6 · control-manifest 本层 Guardrail
//
// 分工(承 audio story-001 先例):Cecil + 源文本面的编排在 `DiagnosisBoundaryGates`
//   (菜单 / 构建前门共用);反射面(字段累积量 / 位宽 / DtoGuard 枚举 / 出口形状)在本文件。
//
// ⚠️ 诚实边界(不冒充):
//   · 「查体 + 落笔 + 2 改写」完整脚本随 story-005/006 落 —— 本 story 可执行面 =
//     门面读数 + 成长出口,夹具形状不变,直接扩展;
//   · 「流哈希 ≡ 对照跑」的跨 epic 空流基线(disease story 002)归 CI 层
//     (Implementation Note 4);本处以同进程对照落地结构;
//   · AC-8-6「定表命中断言」(同输入两次查表)随 story 003/004 落 —— 本 story 只锁
//     「运行期不现算浮点幂」的形状(零 libm 调用点);
//   · AC-8-4 的 11 输入契约反射半边:prescription-medication epic 未开工 ⇒ NOT-RUN。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;

namespace DaYiJingCheng.Tests.DiagnosisSystem
{
    [TestFixture]
    internal sealed class BoundaryGuardTest
    {
        private const string Prefix = DiagnosisBoundaryGates.DiagnosisModuleNamespacePrefix;

        // ════════════════════ 全门(生产树绿)════════════════════

        /// <summary>全门:IL 层 + 源文本层 + 逃逸谓词对生产树一次跑完 ⇒ 零红。
        /// 覆盖 AC-8-1/8-2 静态半 · AC-8-4 8 侧 · 铁律③④ · AC-8-6 · G-2 · 门面收口。</summary>
        [Test]
        public void test_fullGate_currentTree_zeroErrors()
        {
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False,
                "编译失败 ⇒ 一切扫描不可判(假绿防护前提)");
            var errs = DiagnosisBoundaryGates.RunAll(out var warns);
            foreach (var w in warns) TestContext.WriteLine("[warn] " + w);
            Assert.That(errs, Is.Empty, "边界门红行:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_gate_missingDll_red()
        {
            var errs = DiagnosisBoundaryGates.CheckDiagnosisIl(
                "/nonexistent/nope.dll", Prefix, out _, out _);
            Assert.That(errs.Any(e => e.Contains("[D-0]")), Is.True,
                "产物缺失须红 —— 拒以空集冒充绿");
        }

        // ════════════════════ AC-8-1(I)零写入 ═════════════════════

        /// <summary>8 现存可执行面(门面读 ×4 + 成长出口 ×2)跑完:Append 计数 0、
        /// 流哈希 ≡ 不经 8 的对照跑。⚠️ sink 从未交给 8(IL 层已证 8 连 IEventSink 类型
        /// 都不引用)—— 计数器是可执行面的第二道保险,不是唯一防线。
        /// ⚠️ **结构占位(2026-10-05 评审登记)**:两 sink 恒空 ⇒ 哈希对照段当前**不可失败**;
        /// story-005/006 落「查体+落笔+2改写」全流程脚本后才获得鉴别力 —— 收口描述
        /// 不得引作「AC-8-1[I] 哈希判据已生效」。</summary>
        [Test]
        public void test_ac81_runtime_zeroWrites_hashEqualsControl()
        {
            var (aCount, aHash) = RunEightSurface();
            var (cCount, cHash) = RunControl();
            Assert.That(aCount, Is.EqualTo(0), "8 可执行面 Append 计数须 0(AC-8-1)");
            Assert.That(cCount, Is.EqualTo(0), "对照跑自身零写入(基线干净)");
            Assert.That(aHash, Is.EqualTo(cHash),
                "8 跑过的流哈希须 ≡ 不经 8 的对照跑(AC-8-1;跨 epic 空流基线归 CI)");
        }

        [Test]
        public void test_ac81_ilNegative_sinkAndPublish_red()
        {
            var errs = DiagnosisBoundaryGates.CheckDiagnosisIl(
                TestDll, Prefix, out var matched, out _);
            Assert.That(matched, Is.GreaterThanOrEqualTo(1),
                "负例夹具须在扫描面内(空集 = 扫描键失配)");
            Assert.That(errs.Any(e => e.Contains("[D-TREF]") && e.Contains("IEventSink")),
                Is.True, "IEventSink 类型面引用须红(铁律② / AC-8-1)");
            Assert.That(errs.Any(e => e.Contains("[D-PUB]")), Is.True,
                "Publish 调用点须红(AC-8-1 静态守门)");
        }

        [Test]
        public void test_ac81_sourceNegative_usingSimPublishReflect_red_commentGreen()
        {
            var lines = new[]
            {
                /* 1*/ "using DaYiJingCheng.Sim.Contracts;",          // 合法,不许误伤
                /* 2*/ "using DaYiJingCheng.Sim;",                    // sim 内部命名空间 → 红
                /* 3*/ "namespace DaYiJingCheng.Gameplay.Presentation.Diagnosis {",
                /* 4*/ "  class S { void M(T t) { t.PublishLatest(default); } }", // Publish 调用点 → 红
                /* 5*/ "  object R() => Type.GetType(\"DaYiJingCheng.Sim.Foo\");", // 反射字符串 → 红(字符串保留)
                /* 6*/ "}",
                /* 7*/ "// IEventSink 禁引;Math.Pow 禁;PlayerPrefs 禁 —— 注释剥净不计",
                /* 8*/ "  class W { void F(IEventSink s) { } }",  // 非注释 IEventSink → 红(源层规则正例)
                /* 9*/ "  object P() => FixParse.RoundHalfAwayFromZero(1, 1);", // FixParse → 红(D-FIX 源层正例)
            };
            var errs = DiagnosisBoundaryGates.CheckSourceText("neg.cs", lines);
            Assert.That(errs.Any(e => e.Contains("neg.cs:2") && e.Contains("[D-TREF]")), Is.True,
                "using Sim 内部命名空间须红(故事 QA「加 using ⇒ 必红」)");
            Assert.That(errs.Any(e => e.Contains("neg.cs:4") && e.Contains("[D-PUB]")), Is.True,
                "Publish 调用点须红(源层)");
            Assert.That(errs.Any(e => e.Contains("neg.cs:5") && e.Contains("[D-TREF]")), Is.True,
                "反射字符串须红(字符串保留 —— IL 漏报面的兜底)");
            Assert.That(errs.Any(e => e.Contains("neg.cs:8") && e.Contains("[D-TREF]") &&
                                      e.Contains("IEventSink")), Is.True,
                "非注释 IEventSink 词面须红(2026-10-05 评审补:源层规则正例,原缺)");
            Assert.That(errs.Any(e => e.Contains("neg.cs:9") && e.Contains("[D-FIX]")), Is.True,
                "FixParse 词面须红(2026-10-05 评审补:D-FIX 源层正例,原缺)");
            Assert.That(errs.Any(e => e.Contains("neg.cs:1")), Is.False,
                "Sim.Contracts 是 8 的合法依赖,不许误伤");
            Assert.That(errs.Any(e => e.Contains("neg.cs:7")), Is.False,
                "注释提及不算(剥注释 —— 承 audio 先例)");
        }

        // ════════════════════ AC-8-2(L)引用面与浮点入口 ═════════════════════

        [Test]
        public void test_ac82_ilNegative_simInternalFixMember_red_toFloatGreen()
        {
            var errs = DiagnosisBoundaryGates.CheckDiagnosisIl(TestDll, Prefix, out _, out _);
            Assert.That(errs.Any(e => e.Contains("[D-TREF]") && e.Contains("RecipeDataSet")),
                Is.True, "sim 内部类型须红(QA「影子负夹具」:PatientState 全库不存在," +
                         "以 RecipeDataSet 承同一判据)");
            Assert.That(errs.Any(e => e.Contains("[D-FIX]") && e.Contains("「get_Raw」")),
                Is.True, "Fix.Raw 访问须红(浮点入口仅 ToFloat)");
            Assert.That(errs.Any(e => e.Contains("[D-FIX]") && e.Contains("「One」")),
                Is.True, "Fix.One 字段访问须红(2026-10-05 评审补:VerdictField 字段分支正例)");
            Assert.That(errs.Any(e => e.Contains("「ToFloat」")), Is.False,
                "Fix.ToFloat 是 8 的唯一合法浮点入口,不许误伤");
        }

        /// <summary>门面形状:住前缀、零字段、唯一公开方法 Read、签名对齐、null 硬失败。</summary>
        [Test]
        public void test_ac82_facade_shapeAndNullGuard()
        {
            var types = ProductionDiagnosisTypes();
            var facade = types.Single(t => t.Name == "DiagnosisVitalsFacade");
            Assert.That(facade.Namespace, Is.EqualTo(Prefix), "门面须住 8 前缀(扫描键)");
            Assert.That(ZeroFields(facade), Is.Empty, "门面零字段(AC-8-3 零累积量形状前提)");
            var read = facade.GetMethod("Read",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly,
                null, new[] { typeof(IVitalsQuery), typeof(PatientId) }, null);
            Assert.That(read, Is.Not.Null, "唯一取数方法 Read(IVitalsQuery, PatientId)");
            Assert.That(read.ReturnType, Is.EqualTo(typeof(VitalsDto)));
            Assert.Throws<ArgumentNullException>(
                () => DiagnosisVitalsFacade.Read(null, new PatientId(1)),
                "null query 须 ArgumentNullException 显式失败,不静默");

            // 成员集锁(2026-10-05 评审补,与 EmitGrowth 锁同构):若 9 侧扩
            // GetVitalsV2 等成员,8 可绕门面直调而 D-FACADE 谓词(按名匹配)静默绿
            // —— 扩员须同步扩谓词,本断言是扩员的报警器。
            var qMethods = typeof(IVitalsQuery).GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.That(qMethods.Select(m => m.Name).Distinct().ToArray(),
                Is.EqualTo(new[] { "GetVitals" }),
                "IVitalsQuery 成员集 = {GetVitals}(扩员须同步扩 [D-FACADE] 谓词)");
        }

        [Test]
        public void test_ac82_facadeNegative_getVitalsOutsideFacade_red()
        {
            var errs = DiagnosisBoundaryGates.CheckDiagnosisIl(TestDll, Prefix, out _, out _);
            Assert.That(errs.Any(e => e.Contains("[D-FACADE]")), Is.True,
                "门面之外调用 IVitalsQuery.GetVitals 须红(取数唯一入口收口在调用点)");
        }

        // ════════════════════ AC-8-3(L/I)零累积量 + 双进程逐字 ═════════════════════

        /// <summary>字段反射遍历生产 8 类型 ⇒ 零跨调用累积量语义字段、零 tick 类型持久字段。</summary>
        [Test]
        public void test_ac83_stateScan_production_clean()
        {
            var types = ProductionDiagnosisTypes();
            Assert.That(types, Is.Not.Empty,
                "扫描面自证(2026-10-05 评审补:空集 ⇒ 扫描键失配,单跑假绿面)");
            var errs = ScanStateFields(types);
            Assert.That(errs, Is.Empty, "8 生产字段零累积量语义:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_ac83_stateScan_negative_tokensAndTick_red()
        {
            var errs = ScanStateFields(new[] { typeof(StateFieldFixture) });
            Assert.That(errs.Any(e => e.Contains("_confidence")), Is.True, "置信度字段须红");
            Assert.That(errs.Any(e => e.Contains("_lastRecheckTick")), Is.True, "重查 tick 字段须红");
            Assert.That(errs.Any(e => e.Contains("_rewriteCount")), Is.True, "改写次数字段须红");
            Assert.That(errs.Any(e => e.Contains("_readings")), Is.True, "读数记录字段须红");
            Assert.That(errs.Any(e => e.Contains("ITickProvider")), Is.True,
                "tick 类型字段须红(零 tick 类型持久字段)");
            Assert.That(errs.Any(e => e.Contains("_frameCache")), Is.False,
                "非累积量语义的当帧缓存不许误伤(control-manifest Guardrail)");
        }

        /// <summary>Implementation Note 3:纯函数入口 + 两独立实例 =「两进程等价物」。
        /// ⚠️ 四输出(display_词 / 把握度 / 四态)随 story 002/003 落 —— 同一夹具形状扩展。</summary>
        [Test]
        public void test_ac83_twoIndependentInstances_byteIdentical()
        {
            var pid = new PatientId(1);
            var dto = new VitalsDto(0.42f, 0.07f, 0b000101, 2);
            var q1 = new FixedVitalsQuery(dto);
            var q2 = new FixedVitalsQuery(dto);
            var v1 = DiagnosisVitalsFacade.Read(q1, pid);
            var v2 = DiagnosisVitalsFacade.Read(q2, pid);
            Assert.That(BitsOf(v1.Position), Is.EqualTo(BitsOf(v2.Position)), "Position 逐位相同");
            Assert.That(BitsOf(v1.Trend), Is.EqualTo(BitsOf(v2.Trend)), "Trend 逐位相同");
            Assert.That(v1.SignChannelMask, Is.EqualTo(v2.SignChannelMask));
            Assert.That(v1.SignCount, Is.EqualTo(v2.SignCount));

            var p1 = DiagnosisGrowthExit.EmitGrowth(0, 2, 3, NoveltyClass.First, 12, 100, pid);
            var p2 = DiagnosisGrowthExit.EmitGrowth(0, 2, 3, NoveltyClass.First, 12, 100, pid);
            Assert.That(p1.ActorId, Is.EqualTo(p2.ActorId));
            Assert.That(p1.PatientId, Is.EqualTo(p2.PatientId));
            Assert.That(p1.SkillId, Is.EqualTo(p2.SkillId));
            Assert.That(p1.ObjectId, Is.EqualTo(p2.ObjectId));
            Assert.That(p1.NoveltyClass, Is.EqualTo(p2.NoveltyClass));
            Assert.That(p1.Level, Is.EqualTo(p2.Level), "成长载荷逐字相同");
        }

        // ════════════════════ AC-8-4 8 侧:零 8→11 数据边 ═════════════════════

        [Test]
        public void test_ac84_sourceProduction_zeroPrescriptionEdge()
        {
            // 扫描面自证:类型存在 ⇒ 其源文件必声明前缀 ⇒ CheckSourceFiles 的
            // declCount > 0(类型从声明该前缀的源文件编译而来,桥接成立)。
            Assert.That(ProductionDiagnosisTypes(), Is.Not.Empty,
                "扫描面自证(2026-10-05 评审补:空集 ⇒ 单跑假绿面)");
            var errs = DiagnosisBoundaryGates.CheckSourceFiles();
            Assert.That(errs.Any(e => e.Contains("[D-11]")), Is.False,
                "8 源零 8→11 数据边(AC-8-4 本 story 断 8 侧;铁律① 病名不给 11)");
            TestContext.WriteLine(
                "AC-8-4 的 11 输入契约反射半边 NOT-RUN —— prescription-medication epic " +
                "未开工(全库零 prescription 类型),归该 epic story-003 成对断言。");
        }

        [Test]
        public void test_ac84_sourceNegative_prescription_red()
        {
            var errs = DiagnosisBoundaryGates.CheckSourceText("p11.cs", new[]
            {
                "namespace DaYiJingCheng.Gameplay.Presentation.Diagnosis {",
                "  class X { object prescriptionCache; }",
                "}",
            });
            Assert.That(errs.Any(e => e.Contains("[D-11]") && e.Contains("p11.cs:2")), Is.True,
                "prescri* 词面须红(8→11 数据边,8 侧)");
        }

        // ════════════════════ 铁律③:零持久化 API ═════════════════════

        [Test]
        public void test_rule3_persistence_layers_negative()
        {
            var ilErrs = DiagnosisBoundaryGates.CheckDiagnosisIl(TestDll, Prefix, out _, out _);
            Assert.That(ilErrs.Any(e => e.Contains("[D-PERSIST]") && e.Contains("PlayerPrefs")),
                Is.True, "IL 层:PlayerPrefs 调用点须红(铁律③「8 什么都没存」)");

            var srcErrs = DiagnosisBoundaryGates.CheckSourceText("persist.cs", new[]
            {
                "class A { void M() { UnityEngine.PlayerPrefs.SetInt(\"k\", 1); } }",
                "class B { object N() => UnityEngine.JsonUtility.ToJson(null); }",
                "class C { void O() => System.IO.File.WriteAllText(\"a\", \"b\"); }",
            });
            Assert.That(srcErrs, Has.Count.EqualTo(3), "源层三行各命中一次(全为 D-PERSIST)");
            Assert.That(srcErrs.All(e => e.Contains("[D-PERSIST]")), Is.True);
        }

        // ════════════════════ 铁律④:EmitGrowth 唯一出口形状 ═════════════════════

        /// <summary>契约侧唯一对外入口:方法名集 = {EmitGrowth},返回载荷、参数不含 sink。</summary>
        [Test]
        public void test_rule4_contract_uniqueEntryPoint()
        {
            var methods = typeof(SkillGrownEmitter).GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Assert.That(methods.Length, Is.GreaterThanOrEqualTo(2),
                "契约两重载(patientId 显式 / 缺省)须都在");
            Assert.That(methods.Select(m => m.Name).Distinct().ToArray(),
                Is.EqualTo(new[] { "EmitGrowth" }), "契约唯一入口名 = EmitGrowth");
            foreach (var m in methods)
            {
                Assert.That(m.ReturnType, Is.EqualTo(typeof(SkillGrownPayload)),
                    $"{m.Name} 返回载荷 —— 不 Append(编码 / 构造 / Append 归主机)");
                foreach (var p in m.GetParameters())
                    Assert.That(p.ParameterType, Is.Not.EqualTo(typeof(IEventSink)),
                        $"{m.Name}({p.Name}) 不得要求 sink 入参");
            }
        }

        /// <summary>出口形状:类型住前缀、零字段、唯一公开方法签名对齐、IL 调用点恰 = 1。</summary>
        [Test]
        public void test_rule4_exitShape_ilCallsiteExactlyOne()
        {
            var types = ProductionDiagnosisTypes();
            var exit = types.Single(t => t.Name == "DiagnosisGrowthExit");
            Assert.That(exit.Namespace, Is.EqualTo(Prefix), "出口须住 8 前缀(扫描键)");
            Assert.That(ZeroFields(exit), Is.Empty, "出口零字段(不持时钟 / 不持状态)");
            var pub = exit.GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Assert.That(pub.Length, Is.EqualTo(1), "唯一公开方法(唯一出口形状)");
            Assert.That(pub[0].Name, Is.EqualTo("EmitGrowth"));
            Assert.That(pub[0].ReturnType, Is.EqualTo(typeof(SkillGrownPayload)));
            Assert.That(pub[0].GetParameters().Select(p => p.ParameterType).ToArray(),
                Is.EqualTo(new[]
                {
                    typeof(int), typeof(int), typeof(int), typeof(NoveltyClass),
                    typeof(int?), typeof(long), typeof(PatientId),
                }), "签名与契约 7 参逐参对齐(纯转发)");

            var errs = DiagnosisBoundaryGates.CheckDiagnosisIl(
                ProdDll, Prefix, out var matched, out var emitCalls);
            Assert.That(matched, Is.GreaterThanOrEqualTo(2), "门面 + 出口须在扫描面");
            Assert.That(errs, Is.Empty, "生产 IL 层零红:\n" + string.Join("\n", errs));
            Assert.That(emitCalls, Is.EqualTo(1),
                "8 内 SkillGrownEmitter.EmitGrowth 调用点恰 = 1(铁律④ IL 半边)");
        }

        [Test]
        public void test_rule4_ilNegative_directCallOutsideExit_red()
        {
            var errs = DiagnosisBoundaryGates.CheckDiagnosisIl(TestDll, Prefix, out _, out _);
            Assert.That(errs.Any(e => e.Contains("[D-EXIT]") && e.Contains("DiagnosisGrowthExit")),
                Is.True, "出口之外直调 EmitGrowth 须红(8 内唯一调用点在 DiagnosisGrowthExit)");
        }

        // ════════════════════ AC-8-6:libm 超越函数与位宽 ═════════════════════

        [Test]
        public void test_ac86a_ilNegative_libm_red()
        {
            var errs = DiagnosisBoundaryGates.CheckDiagnosisIl(TestDll, Prefix, out _, out _);
            Assert.That(errs.Any(e => e.Contains("[D-G1]") && e.Contains("Math::Pow")), Is.True,
                "IL 层:Math.Pow 须红(AC-8-6a)");
            Assert.That(errs.Any(e => e.Contains("[D-G1]") && e.Contains("Math::Sin")), Is.True,
                "IL 层:Math.Sin 须红(G-1「等超越函数」)");
            Assert.That(errs.Any(e => e.Contains("[D-G1]") && e.Contains("Mathf::Sqrt")), Is.True,
                "IL 层:Mathf.Sqrt 须红(2026-10-05 评审补 —— G-4 邀请式绕行面)");
        }

        [Test]
        public void test_ac86a_sourceNegative_libm_red_nonLibmGreen()
        {
            var errs = DiagnosisBoundaryGates.CheckSourceText("libm.cs", new[]
            {
                /* 1*/ "class A { double M() => Math.Pow(2.0, 1.4); }",
                /* 2*/ "class B { float N(float x) => (float)MathF.Sqrt(x); }",
                /* 3*/ "class C { double P() => Math.Max(1, 2); }",   // 非超越函数,不许误伤
                /* 4*/ "// Math.Pow 注释不算",
                /* 5*/ "class D { float M() => Mathf.Pow(1f, 1.4f); }", // Mathf → 红(评审补)
            });
            Assert.That(errs.Any(e => e.Contains("[D-G1]") && e.Contains("libm.cs:1")), Is.True);
            Assert.That(errs.Any(e => e.Contains("[D-G1]") && e.Contains("libm.cs:2")), Is.True);
            Assert.That(errs.Any(e => e.Contains("[D-G1]") && e.Contains("libm.cs:5")), Is.True,
                "Mathf.Pow 须红(2026-10-05 评审补:原正则漏 Mathf)");
            Assert.That(errs.Any(e => e.Contains("libm.cs:3")), Is.False,
                "Math.Max 非超越函数,不在禁列");
            Assert.That(errs.Any(e => e.Contains("libm.cs:4")), Is.False, "注释不算");
        }

        [Test]
        public void test_ac86c_widthProduction_noDouble()
        {
            var types = ProductionDiagnosisTypes();
            Assert.That(types, Is.Not.Empty,
                "扫描面自证(2026-10-05 评审补:空集 ⇒ 扫描键失配,单跑假绿面)");
            var errs = ScanScalarWidth(types);
            Assert.That(errs, Is.Empty, "8 生产标量一律 System.Single(AC-8-6c / G-4):\n" +
                                         string.Join("\n", errs));
        }

        [Test]
        public void test_ac86c_widthNegative_double_red_singleGreen()
        {
            var errs = ScanScalarWidth(new[] { typeof(DoubleScalarFixture) });
            Assert.That(errs.Any(e => e.Contains("_mix")), Is.True, "double 字段须红");
            Assert.That(errs.Any(e => e.Contains("Gain") && e.Contains("ret")), Is.True,
                "double 返回类型须红");
            Assert.That(errs.Any(e => e.Contains("amount")), Is.True, "double 参数须红");
            Assert.That(errs.Any(e => e.Contains("_single")), Is.False,
                "float 字段不许误伤(单精度合法)");
        }

        // ════════════════════ G-2:时钟 / RNG ═════════════════════

        [Test]
        public void test_clk_layers_negative()
        {
            var ilErrs = DiagnosisBoundaryGates.CheckDiagnosisIl(TestDll, Prefix, out _, out _);
            Assert.That(ilErrs.Any(e => e.Contains("[D-CLK]")), Is.True,
                "IL 层:UnityEngine.Time 时钟访问须红(G-2 / AC-8-3 四类合法可变输入)");
            Assert.That(ilErrs.Any(e => e.Contains("[D-CLK]") && e.Contains("DateTimeOffset")),
                Is.True, "IL 层:DateTimeOffset 墙钟须红(2026-10-05 评审补)");

            var srcErrs = DiagnosisBoundaryGates.CheckSourceText("clock.cs", new[]
            {
                /* 1*/ "class A { float M() => UnityEngine.Time.deltaTime; }",
                /* 2*/ "class B { object N() => UnityEngine.Random.value; }",
                /* 3*/ "class C { object P() => DateTime.UtcNow; }",
                /* 4*/ "class D { object Q() => DateTimeOffset.Now; }",          // 评审补
                /* 5*/ "class E { object S() => Stopwatch.StartNew(); }",        // 评审补
                /* 6*/ "class F { int T() => Environment.TickCount; }",          // 评审补
            });
            Assert.That(srcErrs, Has.Count.EqualTo(6), "源层时钟 / RNG 六行各须命中(各一行一规则)");
            Assert.That(srcErrs.All(e => e.Contains("[D-CLK]")), Is.True);
        }

        // ════════════════════ TR-diag-019/020:PresentationDtoGuard 挂入 ═════════════════════

        /// <summary>8 的全部呈现 DTO(前缀下 *Dto 类型)递归扫描 ⇒ 零诊断语义。
        /// ⚠️ story-006 前为 0 个 ⇒ 钩子空转(空集不豁免负例,红路径由下一条坐实)。</summary>
        [Test]
        public void test_dtoGuard_diagnosisDtos_hooked_clean()
        {
            var dtos = ProductionDiagnosisTypes()
                .Where(t => t.Name.EndsWith("Dto", StringComparison.Ordinal))
                .ToList();
            foreach (var d in dtos)
                Assert.That(PresentationDtoGuard.Scan(d), Is.Empty,
                    $"8 的呈现 DTO「{d.Name}」零 disease / 诊断语义(TR-diag-019/020)");
            TestContext.WriteLine(
                $"8 的 *Dto 类型数 = {dtos.Count}(story-006 前为 0 ⇒ 钩子空转;" +
                "红路径由 test_dtoGuard_negative_* 坐实)");
        }

        [Test]
        public void test_dtoGuard_negative_diseaseFieldAndTypeName_red()
        {
            var fieldErrs = PresentationDtoGuard.Scan(typeof(GuardNegativeDiseaseField));
            Assert.That(fieldErrs.Any(e => e.Contains("disease_id")), Is.True,
                "disease_id 字段须红(AC-37-15 / TR-diag-019)");
            var nameErrs = PresentationDtoGuard.Scan(typeof(GuardNegativeDiagnosisName));
            Assert.That(nameErrs.Any(e => e.Contains("名携带 disease 语义")), Is.True,
                "类型名携带 diagnos* 词面须红(类型名规则)");
            Assert.Throws<InvalidOperationException>(
                () => PresentationDtoGuard.AssertNoDiseaseId(typeof(GuardNegativeDiseaseField)),
                "AssertNoDiseaseId 草图签名:有违例即 throw");
        }

        // ════════════════════ 逃逸谓词(四层同键兜底)════════════════════

        [Test]
        public void test_sourceEscape_tokenWithoutPrefix_red_benignGreen()
        {
            var red = DiagnosisBoundaryGates.CheckSourceEscape(
                "esc.cs", "class C { DiagnosisVitalsFacade f; }");
            Assert.That(red.Any(e => e.Contains("[D-ESC]")), Is.True,
                "含 8 类型 token 却不声明前缀 ⇒ 逃逸红");

            Assert.That(DiagnosisBoundaryGates.CheckSourceEscape(
                "decl.cs",
                "namespace DaYiJingCheng.Gameplay.Presentation.Diagnosis { class C { } }"),
                Is.Empty, "声明前缀 ⇒ 归源文本层,逃逸段放行");
            Assert.That(DiagnosisBoundaryGates.CheckSourceEscape(
                "cmt.cs", "// DiagnosisFoo 提及仅在注释"), Is.Empty,
                "注释提及不算(剥注释)");
            Assert.That(DiagnosisBoundaryGates.CheckSourceEscape(
                "ok.cs", "class L4 { int x; }"), Is.Empty, "无 token 良性文件放行");
        }

        // ════════════════════ helpers ════════════════════

        private static string ProdDll => ScriptDll(AssemblyGates.PresentationAssemblyName);
        private static string TestDll => ScriptDll("Sim.Contracts.Tests");
        private static string ScriptDll(string name) => AssemblyGates.ScriptAssemblyPath(name);

        private static List<Type> ProductionDiagnosisTypes()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Gameplay.Presentation");
            Assert.That(asm, Is.Not.Null, "Gameplay.Presentation 装配须已加载");
            Type[] all;
            try
            {
                all = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                all = ex.Types.Where(t => t != null).ToArray();
            }
            return all.Where(InDiagnosisPrefix).ToList();
        }

        private static bool InDiagnosisPrefix(Type t)
        {
            while (t != null && string.IsNullOrEmpty(t.Namespace)) t = t.DeclaringType;
            if (t == null) return false;
            var ns = t.Namespace;
            return ns == Prefix || ns.StartsWith(Prefix + ".", StringComparison.Ordinal);
        }

        private static FieldInfo[] ZeroFields(Type t) => t.GetFields(
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

        // ── AC-8-3 反射面:跨调用累积量字段扫描(名字语义 + tick 类型;非全禁可变)──
        private static readonly string[] StateNameTokens =
        {
            "reading", "record", "checked", "confidence", "revis", "rewrite",
            "disease", "tick", "snapshot", "recheck",
        };

        private static List<string> ScanStateFields(IEnumerable<Type> types)
        {
            var errs = new List<string>();
            const BindingFlags fb = BindingFlags.DeclaredOnly | BindingFlags.Public |
                                    BindingFlags.NonPublic | BindingFlags.Instance |
                                    BindingFlags.Static;
            foreach (var t in types)
                foreach (var f in t.GetFields(fb))
                {
                    var norm = NormalizeMemberName(f.Name);
                    foreach (var tok in StateNameTokens)
                        if (norm.Contains(tok))
                            errs.Add($"[AC-8-3] {t.FullName}.{f.Name} 字段名携带累积量语义" +
                                     $"「{tok}」—— 读数 / 已查记录 / 病名 / 置信度 / 改写次数" +
                                     "等跨调用累积量禁持(当帧缓存按语义豁免)。");
                    foreach (var leaf in Flatten(f.FieldType))
                        if (leaf.Name.IndexOf("tick", StringComparison.OrdinalIgnoreCase) >= 0)
                            errs.Add($"[AC-8-3] {t.FullName}.{f.Name} 字段类型「{leaf.Name}」" +
                                     "携带 tick —— 上次重查 tick / 快照 tick 由调用方传参" +
                                     "(39 查询取得),8 不持久持有。");
                }
            return errs;
        }

        private static string NormalizeMemberName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var a = name.IndexOf('<');
            var b = name.IndexOf('>');
            if (a >= 0 && b > a) name = name.Substring(a + 1, b - a - 1);
            return name.Replace("_", "").Replace("-", "").ToLowerInvariant();
        }

        // ── AC-8-6c 反射面:double 混算扫描(字段 / 参数 / 返回,泛型与数组展开)──
        private static List<string> ScanScalarWidth(IEnumerable<Type> types)
        {
            var errs = new List<string>();
            const BindingFlags fb = BindingFlags.DeclaredOnly | BindingFlags.Public |
                                    BindingFlags.NonPublic | BindingFlags.Instance |
                                    BindingFlags.Static;
            foreach (var t in types)
            {
                foreach (var f in t.GetFields(fb))
                    if (Flatten(f.FieldType).Any(x => x == typeof(double)))
                        errs.Add($"[G-4c] {t.FullName}.{f.Name} —— 字段类型含 double," +
                                 "8 内部标量一律 System.Single(AC-8-6c)。");
                foreach (var m in t.GetMethods(fb))
                {
                    if (Flatten(m.ReturnType).Any(x => x == typeof(double)))
                        errs.Add($"[G-4c] {t.FullName}.{m.Name}:ret —— 返回类型含 double(AC-8-6c)。");
                    foreach (var p in m.GetParameters())
                        if (Flatten(p.ParameterType).Any(x => x == typeof(double)))
                            errs.Add($"[G-4c] {t.FullName}.{m.Name}({p.Name}) —— 参数类型含 double" +
                                     "(AC-8-6c)。");
                }
            }
            return errs;
        }

        private static IEnumerable<Type> Flatten(Type t)
        {
            if (t == null) yield break;
            if (t.IsArray || t.IsByRef || t.IsPointer)
            {
                foreach (var e in Flatten(t.GetElementType())) yield return e;
                yield break;
            }
            if (t.IsGenericType)
                foreach (var a in t.GetGenericArguments())
                    foreach (var e in Flatten(a))
                        yield return e;
            yield return t;
        }

        // ── AC-8-1 运行面:计数 sink + 纯函数脚本 + 对照跑 ─────────────────────
        private sealed class CountingSink : IEventSink
        {
            private readonly List<SimEvent> _events = new List<SimEvent>();
            public int AppendCount { get; private set; }
            public IReadOnlyList<SimEvent> Events => _events;
            public void Append(in SimEvent e)
            {
                AppendCount++;
                _events.Add(e);
            }
        }

        private sealed class FixedVitalsQuery : IVitalsQuery
        {
            private readonly VitalsDto _dto;
            public FixedVitalsQuery(in VitalsDto dto) { _dto = dto; }
            public VitalsDto GetVitals(PatientId p) => _dto;
        }

        private static (int appendCount, ulong hash) RunEightSurface()
        {
            var sink = new CountingSink();
            var pid = new PatientId(1);
            var query = new FixedVitalsQuery(new VitalsDto(0.42f, 0.07f, 0b000101, 2));
            // 查体形状:门面读 ×4(可执行面;完整「查体 + 落笔 + 2 改写」脚本归 005/006,
            // 夹具形状不变 —— story QA「零写入」)
            for (var i = 0; i < 4; i++)
            {
                var v = DiagnosisVitalsFacade.Read(query, pid);
                Assert.That(v.SignCount, Is.EqualTo(2), "门面纯转发(不夹取)");
            }
            // 成长出口 ×2:首次升级 + 改写后 novelty 再发(未升级 ⇒ 哨兵)
            var p1 = DiagnosisGrowthExit.EmitGrowth(0, 2, 3, NoveltyClass.First, 12, 100, pid);
            var p2 = DiagnosisGrowthExit.EmitGrowth(0, 2, 3, NoveltyClass.Normal, null, 150, pid);
            Assert.That(p1.Level, Is.EqualTo(12), "出口转发:升级 = 新等级");
            Assert.That(p2.Level, Is.EqualTo(SkillGrownPayload.LevelNotGrown),
                "出口转发:未升级 = 哨兵 -1");
            Assert.That(p1.PatientId, Is.EqualTo(1), "出口转发:patientId 透传");
            return (sink.AppendCount, StreamHash(sink));
        }

        private static (int appendCount, ulong hash) RunControl()
        {
            // 对照跑:同输入构造、零 8 调用(跨 epic 空流基线归 CI —— Implementation Note 4;
            // 本处以同进程对照落地结构,不冒充跨 epic 判据)
            var sink = new CountingSink();
            _ = new VitalsDto(0.42f, 0.07f, 0b000101, 2);
            return (sink.AppendCount, StreamHash(sink));
        }

        private static ulong StreamHash(CountingSink sink)
        {
            unchecked
            {
                var h = 14695981039346656037UL;   // FNV-1a offset basis
                foreach (var e in sink.Events)
                {
                    h = (h ^ (ulong)e.Tick) * 1099511628211UL;
                    h = (h ^ (ulong)e.Seq) * 1099511628211UL;
                }
                return h;
            }
        }

        private static int BitsOf(float f) => BitConverter.ToInt32(BitConverter.GetBytes(f), 0);

        // ════════════════════ 反射面负例夹具(不经 8 前缀;以显式类型清单入参)════════════════════

        private sealed class StateFieldFixture
        {
            private float _confidence = 0.5f;                       // 置信度 → 红
            private long _lastRecheckTick = 0;                      // 重查 tick → 红
            private ITickProvider _tick = null;                     // tick 类型 → 红
            private int _rewriteCount = 0;                          // 改写次数 → 红
            private readonly List<int> _readings = new List<int>(); // 读数记录 → 红
            private readonly int _frameCache = 42;                  // 当帧缓存(非累积语义)→ 绿
        }

        private sealed class DoubleScalarFixture
        {
            private double _mix = 1.0;                 // double 字段 → 红
            private float _single = 0.5f;              // single 合法 → 绿
            private double Gain(float x) => x * (float)_mix;   // double 返回 → 红
            private void Feed(double amount) { _mix = amount; } // double 参数 → 红
        }

        private struct GuardNegativeDiseaseField { public int disease_id; }

        private struct GuardNegativeDiagnosisName { public int Ok; }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// 真 IL 负例夹具:住 8 前缀命名空间、但在**测试装配**内 —— 生产树零污染。
// ⚠️ 禁止挪入 Assets/Gameplay.Presentation:本类是故意违例的聚合体,
//    DiagnosisBoundaryGates 对测试装配跑同一谓词 ⇒ 每条违例必须红(负例端到端注入)。
// ⚠️ QA 原文的「PatientState 内部类型」全库不存在(C# 侧无此类型)—— 以 Sim 真实
//    内部类型 RecipeDataSet 承同一判据(装配黑名单),故事测试案例括注。
// ═══════════════════════════════════════════════════════════════════════════════
namespace DaYiJingCheng.Gameplay.Presentation.Diagnosis
{
    internal sealed class DiagnosisIlScanNegativeFixture
    {
        // [D-TREF] IEventSink:写通道契约类型面(铁律② / AC-8-1)
        internal void Emit(IEventSink sink) { }

        // [D-TREF] Sim 装配:零 sim 内部类型(AC-8-2)
        internal DaYiJingCheng.Sim.RecipeDataSet Load() => default;

        // [D-PUB]:方法名 Publish 的调用点(AC-8-1 静态守门)
        internal void Publish(int x) { }
        internal void TriggerPublish() => Publish(1);

        // [D-G1]:libm 超越函数(AC-8-6a / G-1;Sin 同禁)
        internal double LibmPow() => Math.Pow(2.0, 1.4);
        internal double LibmSin() => Math.Sin(0.5);

        // [D-G1]:Mathf 同族(2026-10-05 评审补 —— G-4 禁 double ⇒ 作者有动机选
        // float 版,漏它 = G-1 被邀请式绕行击穿)
        internal float MathfSqrt() => UnityEngine.Mathf.Sqrt(2f);

        // [D-PERSIST]:持久化 API 调用点(铁律③;方法体永不执行,只供 IL 扫描)
        internal void Persist() => UnityEngine.PlayerPrefs.SetInt("diag_negative", 1);

        // [D-FIX] / 绿对照:Raw = 红;ToFloat = 绿(AC-8-2 浮点入口唯一)
        internal long RawOf(Fix f) => f.Raw;
        internal float OkToFloat(Fix f) => f.ToFloat();

        // [D-FIX] **字段分支**(2026-10-05 评审补测:原夹具 Raw 走属性 / 方法分支,
        // VerdictField 的 Fix 族字段面零负例):Fix.One 是 static readonly 字段 ⇒ ldsfld
        internal Fix FixOne() => Fix.One;

        // [D-CLK]:帧时钟(G-2 / AC-8-3 四类合法可变输入)
        internal float FrameClock() => UnityEngine.Time.deltaTime;

        // [D-CLK]:DateTimeOffset 墙钟(2026-10-05 评审补 —— 原名单漏,全门绿违 D-CLK)
        internal DateTimeOffset OffsetNow() => DateTimeOffset.Now;

        // [D-FACADE]:GetVitals 调用在门面之外(control-manifest「输入只进门面」)
        internal VitalsDto Bypass(IVitalsQuery q) => q.GetVitals(PatientId.None);

        // [D-EXIT]:直调契约入口 —— 8 内唯一调用点须在 DiagnosisGrowthExit(铁律④)
        internal SkillGrownPayload DirectEmit() => SkillGrownEmitter.EmitGrowth(
            0, 0, 0, NoveltyClass.First, 1, 0, PatientId.None);
    }
}
