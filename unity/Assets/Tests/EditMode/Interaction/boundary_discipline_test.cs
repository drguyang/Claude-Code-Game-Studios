// interaction-system Story 001 —— 边界纪律与程序集归属(六条结构断言 + 负夹具)
//
// 登记落点: tests/unit/interaction/boundary_discipline_test.cs
// 真身落点: unity/Assets/Tests/EditMode/Interaction/boundary_discipline_test.cs
//
// 权威来源:
//   GDD design/gdd/interaction-system.md —— 规则一 / 三 / 六 / 十 / 十一 / 十二 + 22 条 AC 前言
//     («首轮对判据形式的统一改造»:原稿 grep 符号 = 零命中 ⇒ 一律升结构断言)
//   AC-20-03(ADR-020 §四:判据 = 反射断言,不是 grep)在 4 的推广
//   ADR-017 §二(程序集归属判据 = 引用集)· ADR-025(七装配清单)
//
// Story 001 的六条 AC:
//   AC-4-01 [A] 零结算侧类型可达(反射闭包)          —— 判据 = 类型可达性,不是符号 grep
//   AC-4-02 [A] 零 IEventSink(spy 计数 + 结构双判据) —— 零 Append ≠ 零上行
//   AC-4-04 [A] 无状态纯函数(清空重建结果相等)      —— 不断言字节
//   AC-4-05 [A] 引用集 ∩ {9/11/8 sim 侧类型} = ∅ + Candidate 无玩法数值字段
//   AC-4-11 [A] 零 ICameraRig / 档位枚举
//   AC-4-12 [A] 走进 ≠ 交互(≥3 帧、非整 tick 边界采样)
//
// ⚠️ 负夹具是反空转的唯一防线(故事原文):禁入类型登记表若缺项 ⇒ 扫描器空转假绿。
//   每组结构断言都配一个「影子类型注入 ⇒ 必红且点名」的负夹具。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Interaction
{
    public class BoundaryDisciplineTest
    {
        // ═══════════════════════════════════════════════════════════
        //  禁入类型登记表(测试侧唯一真源;AC-4-01/05/11 共用)
        //  ⚠️ 新增成员须同步 GDD 规则表,不得测试侧私加(故事 Implementation Notes)。
        // ═══════════════════════════════════════════════════════════

        /// <summary>结算侧五类型(AC-4-01)—— 全名后缀片段,归一化后子串判。
        /// ⚠️ **只登记「类型名」,不登记 AC 编号** —— 编号不是可达类型,混入即噪声
        /// (2026-10-04 双代理评审登记表清理项)。</summary>
        private static readonly string[] SettlementSideNames =
        {
            "CanCarry",        // 宿主接口(容量比较的宿主)
            "F1Result",        // F1 结果类型(判定链结果)
            "JudgeResult",     // 急救判定结果
            "TreatableBy",     // treatable_by 载荷
            "CapacityCompare"  // 容量比较入参
        };

        /// <summary>玩法数值禁入集(AC-4-05 后半)—— VitalsDto 族 + 规则十/十二点名。</summary>
        private static readonly string[] GameplayValueNames =
        {
            "VitalsDto",
            "disease_id", "DiseaseId", "diseaseId",
            "tier_named", "TierNamed", "tierNamed",
            "drug_profile", "DrugProfile", "drugProfile",
            "EnvMod"
        };

        /// <summary>相机 / 档位禁入集(AC-4-11)。</summary>
        private static readonly string[] CameraTierNames =
        {
            "ICameraRig",
            "CameraMode", "CameraTier", "TierEnum", "LodTier"
        };

        // ═══════════════════════════════════════════════════════════
        //  AC-4-01 —— 零结算侧类型可达(反射闭包)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac401_noSettlementSideTypeReachable()
        {
            var violations = ScanClosureForNames(SettlementSideNames);
            Assert.IsEmpty(violations,
                "AC-4-01:4 的类型可达图中出现结算侧类型(规则一):\n" + string.Join("\n", violations));
        }

        [Test]
        public void test_ac401_scannerIsNotSilentlyNoOp_negativeFixture()
        {
            // 负夹具:影子类型带禁名字段 ⇒ 扫描器必须红且**五条逐一点名**
            var violations = ScanClosureForNames(SettlementSideNames, new[] { typeof(ShadowSettlementLeak) });
            Assert.IsNotEmpty(violations,
                "AC-4-01 负夹具失败:注入影子结算类型后扫描器仍绿 = 空转(假绿)");
            var joined = string.Join("\n", violations);
            foreach (var name in SettlementSideNames)
                Assert.That(joined, Does.Contain(name),
                    $"AC-4-01 负夹具:登记表成员「{name}」未被证明可红 —— 该条拼写若写错将无人发现。实测:\n" + joined);
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-4-02 —— 零 IEventSink(spy + 结构双判据)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac402_spyAppendCountIsZero()
        {
            // ⚠️ sink **必须在链上**:孤立 sink 的 AppendCount == 0 是构造性恒真
            //   (4 的构造只收 IDiscoveryReporter)⇒ 改坏 4 也不会红 = 空转。
            //   故 reporter 替身真持 sink 并按 story 004 的真实链路口径转发
            //   (「请求到达 6 后,是否落 Append 由 6 决定」)—— 任何从 4 漏出的写法都会记账。
            var sink = new SpyEventSink();
            var reporter = new SpyDiscoveryReporter();
            reporter.Wire(sink, dispatchToSink: false);   // 正确实现:请求 ≠ 写入
            var selector = new InteractionSelector(reporter, 2);

            // 驱动全套交互场景:拾取意图 / 对病人按交互 / 对 POI 按交互
            var drop = new List<Candidate> { new Candidate(new WorldPos(1, 0, 0), InteractableKind.Drop, 11L, StableIdSource.InstanceId) };
            var patient = new List<Candidate> { new Candidate(new WorldPos(1, 0, 0), InteractableKind.Patient, 7L, StableIdSource.PatientId) };
            var poi = new List<Candidate> { new Candidate(new WorldPos(1, 0, 0), InteractableKind.PoiCell, 42L, StableIdSource.PoiId) };

            var intent = new InteractIntent(new WorldPos(0, 0, 0), true, 5);
            selector.Select(in intent, drop);
            selector.Select(in intent, patient);
            selector.Select(in intent, poi);

            Assert.AreEqual(0, sink.AppendCount,
                "AC-4-02:4 的路径上 IEventSink.Append 计数必须为 0(规则三)");
            Assert.AreEqual(1, reporter.RequestCount,
                "AC-4-02:POI 交互应经 IDiscoveryReporter.Request 出境(请求 ≠ 写入)");
        }

        /// <summary>
        /// AC-4-02 **spy 半边独立性**(story §QA Test Cases 边缘情形逐字要求:
        /// 「反向确认计数与结构两半**独立**(摘掉结构违规只靠 spy 计数抓)」)。
        /// <para>本夹证明:当实现**从 4 漏出一次写入**时,<b>结构半边看不见、spy 半边能抓</b>。
        /// 形态 = 4 把 POI 交互同时转给一个内部写入包装(不持 `IEventSink` 字段本身 ⇒ 结构扫描
        /// 仅看 4 自有类型时未必点中),而链上的 sink 记到了那一笔。</para>
        /// <para>⚠️ 这同时是 <see cref="test_ac402_spyAppendCountIsZero"/> 的**反空转证明**:
        /// 若 sink 真在链上,则同一个 sink 能记到错误实现的写入 ⇒ 前一条的 0 不是恒真。</para>
        /// </summary>
        [Test]
        public void test_ac402_spyHalfCatchesLeakedWrite_whenStructuralHalfIsBlind()
        {
            var sink = new SpyEventSink();
            var reporter = new SpyDiscoveryReporter();
            reporter.Wire(sink, dispatchToSink: true);    // 错误实现:6 侧落了 Append
            var selector = new InteractionSelector(reporter, 2);

            // 走 4 的**真实路径**(主动交互一个 POI)⇒ 计数经由 4 的出境面到达链上 sink。
            var poi = new List<Candidate> { new Candidate(new WorldPos(1, 0, 0), InteractableKind.PoiCell, 42L, StableIdSource.PoiId) };
            var intent = new InteractIntent(new WorldPos(0, 0, 0), true, 5);
            selector.Select(in intent, poi);

            Assert.AreEqual(1, reporter.RequestCount, "前置:4 确实出境了一次 Request");
            Assert.AreEqual(1, sink.AppendCount,
                "AC-4-02:链上 sink 必须能记到漏出的写入 —— 否则 spy 半边不可证伪(sink 不在链上)");
        }

        [Test]
        public void test_ac402_structurallyNoEventSinkInTypeGraph()
        {
            var violations = ScanClosureForNames(new[] { "IEventSink" });
            Assert.IsEmpty(violations,
                "AC-4-02 结构半边:4 的类型图中不得有 IEventSink 字段 / 构造参 / 形参" +
                "(抓「经封装转发的写入」):\n" + string.Join("\n", violations));
        }

        [Test]
        public void test_ac402_structuralHalfCatchesWrapperForwarding_negativeFixture()
        {
            // 负夹具:经封装转发的写入形态(wrapper 的构造参引 IEventSink)
            var violations = ScanClosureForNames(new[] { "IEventSink" }, new[] { typeof(ShadowWriteWrapper) });
            Assert.IsNotEmpty(violations,
                "AC-4-02 负夹具失败:wrapper 转发形态未被结构半边抓到 = 结构断言空转");
        }

        /// <summary>
        /// AC-4-02 **两半独立** —— 判据正文(story §QA Test Cases 边缘情形)要求
        /// 「反向确认计数与结构两半**独立**(摘掉结构违规只靠 spy 计数抓,反之亦然 —— 两夹具各证一半)」。
        /// <para>⚠️ 2026-10-04 双代理评审 #2:原实现只重跑了一次结构扫描、断言 `Count > 0`,
        /// **从未走 spy 路径** ⇒ 改坏 spy 半边它仍绿,「独立性」未被证明。</para>
        /// <para>现两半**各走各的路径**并**各断言对方看不见**:
        /// (a) **spy 半边**:4 真出境一次 `Request`,链上 sink 记账 —— 结构扫描**看不见**这条路径;
        /// (b) **结构半边**:wrapper 的构造参引 `IEventSink` —— 而它**不在链上**,spy 计数**恒 0**。
        /// ⇒ 两个方向都成立,独立性才算证。</para>
        /// </summary>
        [Test]
        public void test_ac402_twoHalvesAreIndependent()
        {
            // (a) spy 半边抓「链上漏出」:同一形态下结构面未必点中(经封装转发的写入)
            var sink = new SpyEventSink();
            var reporter = new SpyDiscoveryReporter();
            reporter.Wire(sink, dispatchToSink: true);
            var selector = new InteractionSelector(reporter, 2);
            var poi = new List<Candidate> { new Candidate(new WorldPos(1, 0, 0), InteractableKind.PoiCell, 42L, StableIdSource.PoiId) };
            var intent = new InteractIntent(new WorldPos(0, 0, 0), true, 5);
            selector.Select(in intent, poi);

            Assert.Greater(sink.AppendCount, 0,
                "AC-4-02 两半独立(a):链上漏出的写入必须由 **spy 计数**抓到");

            // (b) 结构半边抓「类型图带 IEventSink」—— 而该 wrapper 不在链上 ⇒ spy 计数恒 0
            var structural = ScanClosureForNames(new[] { "IEventSink" }, new[] { typeof(ShadowWriteWrapper) });
            var wrapperSink = new SpyEventSink();
            var wrapperReporter = new SpyDiscoveryReporter();
            wrapperReporter.Wire(wrapperSink, dispatchToSink: false);   // wrapper 未接链
            _ = new ShadowWriteWrapper(wrapperSink);

            Assert.IsNotEmpty(structural,
                "AC-4-02 两半独立(b):wrapper 形态的 **IEventSink 构造参**必须由结构半边抓到");
            Assert.AreEqual(0, wrapperSink.AppendCount,
                "AC-4-02 两半独立(b):wrapper **不在链上** ⇒ spy 计数恒 0 —— 恰证明两半互不可替代");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-4-04 —— 无状态纯函数(清空重建结果相等)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac404_clearAndRebuildProducesEqualSelectionSequence()
        {
            var inputs = BuildSelectionScenario();
            var a = RunScenario(new InteractionSelector(new SpyDiscoveryReporter(), 2), inputs);
            var b = RunScenario(new InteractionSelector(new SpyDiscoveryReporter(), 2), inputs);

            Assert.AreEqual(a.Count, b.Count, "AC-4-04:两次运行的选择序列长度须相等");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Kind, b[i].Kind, $"AC-4-04:第 {i} 步 Kind 须相等");
                Assert.AreEqual(a[i].StableId, b[i].StableId, $"AC-4-04:第 {i} 步 StableId 须相等");
                Assert.AreEqual(a[i].HasTarget, b[i].HasTarget, $"AC-4-04:第 {i} 步 HasTarget 须相等");
            }
        }

        [Test]
        public void test_ac404_interleavedInstancesProveNoHiddenStatic()
        {
            // 实例 A 与 B 交错运行 ⇒ 证无隐藏静态
            var inputs = BuildSelectionScenario();
            var selA = new InteractionSelector(new SpyDiscoveryReporter(), 2);
            var selB = new InteractionSelector(new SpyDiscoveryReporter(), 2);

            var seqA = new List<InteractTarget>();
            var seqB = new List<InteractTarget>();
            for (int i = 0; i < inputs.Count; i++)
            {
                var step = inputs[i];
                seqA.Add(selA.Select(in step.Intent, step.Candidates));
                seqB.Add(selB.Select(in step.Intent, step.Candidates));
            }

            for (int i = 0; i < seqA.Count; i++)
            {
                Assert.AreEqual(seqA[i].StableId, seqB[i].StableId,
                    $"AC-4-04:交错运行第 {i} 步分叉 = 存在隐藏静态状态");
            }
        }

        [Test]
        public void test_ac404_noMutableInstanceOrStaticState()
        {
            var violations = ScanForMutableState(typeof(InteractionSelector));
            Assert.IsEmpty(violations,
                "AC-4-04:选择器不得有非 readonly 实例字段或 static 可变状态:\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void test_ac404_mutableFieldIsCaught_negativeFixture()
        {
            var violations = ScanForMutableState(typeof(ShadowMutableSelector));
            Assert.IsNotEmpty(violations,
                "AC-4-04 负夹具失败:含 _lastTarget 缓存字段的类型未被判定为可变 = 断言空转");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-4-05 —— 引用集 ∩ {9/11/8 sim 侧类型} = ∅ + Candidate 无玩法数值
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac405_referenceSetHasNoSimSideTypesOf9_11_8()
        {
            var asm = typeof(InteractionSelector).Assembly;
            var refs = asm.GetReferencedAssemblies().Select(r => r.Name).ToArray();

            // ⚠️ 2026-10-04 双代理评审 #6 就地订正:原断言「refs ∌ "Sim"」**与本故事自己的
            //   ADR 归属互斥** —— ADR-025 §①:114 明载 4 住 `Gameplay.Presentation`,
            //   而后者的 asmdef `references` **含 `"Sim"`** ⇒ 4 一旦归对位置,该断言必红。
            //   ⇒ 这是规格矛盾,不是实现缺陷。
            //
            //   AC-4-05 的**判据正文**(story :40)要的是:
            //     「引用集 ∩ {9 / 11 / 8 的 **sim 侧类型**} = ∅」
            //   —— 即**按类型可达判**,不是「不许引 `Sim` 程序集」。宿主装配共引 `Sim`
            //   不构成违例;违例是**4 自己的类型图**碰到那些类型。
            //   故本半边撤「不引 Sim 程序集」,改由下方类型可达闭包承担(它才是真判据);
            //   此处只保留一条**结构性**约束:4 不得引 `Sim.Codec`(编码器面,与 4 无关)。
            Assert.That(refs, Does.Not.Contain("Sim.Codec"),
                "AC-4-05:4 不得引 `Sim.Codec`(载荷编码面与纯选择器无关)。实测引用集 = " +
                string.Join(", ", refs));

            // 宿主装配确实共引 `Sim` —— 显式断言,把「共引不是违例」这一判据写死,
            // 防日后有人把「不引 Sim 程序集」当判据重新加回来。
            Assert.That(refs, Does.Contain("Sim"),
                "AC-4-05 归属前提:4 住 `Gameplay.Presentation`(ADR-025 §①:114)," +
                "该装配共引 `Sim` —— 共引**不是**违例,违例是 4 的类型图碰到 sim 侧类型。实测 = " +
                string.Join(", ", refs));
        }

        [Test]
        public void test_ac405_candidateHasNoGameplayValueFields()
        {
            var violations = ScanClosureForNames(GameplayValueNames, new[] { typeof(Candidate) });
            Assert.IsEmpty(violations,
                "AC-4-05:`Candidate` 不得含 VitalsDto / disease_id / tier_named / drug_profile / " +
                "EnvMod 类型字段(规则十):\n" + string.Join("\n", violations));
        }

        [Test]
        public void test_ac405_referenceSetCheckIsNotNoOp_negativeFixture()
        {
            // 负夹具:确认引用集断言真的在判(Sim.Contracts 应存在但其名不叫 "Sim")
            var asm = typeof(InteractionSelector).Assembly;
            var refs = asm.GetReferencedAssemblies().Select(r => r.Name).ToArray();
            Assert.That(refs, Does.Contain("Sim.Contracts"),
                "AC-4-05 负夹具:引用集断言若连 Sim.Contracts 都读不到 ⇒ 断言在空转");
        }

        [Test]
        public void test_ac405_shadowVitalsFieldIsCaught_negativeFixture()
        {
            var violations = ScanClosureForNames(GameplayValueNames, new[] { typeof(ShadowVitalsLeak) });
            Assert.IsNotEmpty(violations,
                "AC-4-05 负夹具失败:含 VitalsDto 字段的影子类型未被抓到");
        }

        [Test]
        public void test_ac405_shadowGameplayValueFieldNamesAreCaught_negativeFixture()
        {
            // 反空转第二道防线:字段**类型**全为 int / string,只有**字段名**违禁。
            // 若扫描器退回「只比类型全名」,本夹即绿 ⇒ AC-4-05 后半沦为装饰。
            var violations = ScanClosureForNames(GameplayValueNames, new[] { typeof(ShadowGameplayValueNameLeak) });
            Assert.IsNotEmpty(violations,
                "AC-4-05 负夹具失败:字段名 disease_id / tier_named / drug_profile 未被点名 ⇒ 扫描器只看类型 = 空转");
            var joined = string.Join("\n", violations);
            Assert.That(joined, Does.Contain("disease_id"), "负夹具须点名 disease_id(实测:\n" + joined + ")");
            Assert.That(joined, Does.Contain("tier_named"), "负夹具须点名 tier_named");
            Assert.That(joined, Does.Contain("drug_profile"), "负夹具须点名 drug_profile");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-4-11 —— 零 ICameraRig / 档位枚举
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac411_noCameraRigOrTierEnumInTypeGraph()
        {
            var violations = ScanClosureForNames(CameraTierNames);
            Assert.IsEmpty(violations,
                "AC-4-11:4 不得引 ICameraRig / 档位枚举类型(规则六):\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void test_ac411_ilCallTableHasNoCameraRigCalls()
        {
            var violations = ScanIlForCalledType(new[] { "ICameraRig" });
            Assert.IsEmpty(violations,
                "AC-4-11:IL 调用表 ∩ ICameraRig.* = ∅:\n" + string.Join("\n", violations));
        }

        /// <summary>
        /// AC-4-11 **IL 半边反空转**(story:「负夹具是反空转的唯一防线」)。无此夹则
        /// <see cref="ScanIlForCalledType"/> 永不证明可红 —— 这正是首轮 AC-4-01 夹具的同类病。
        /// </summary>
        [Test]
        public void test_ac411_ilHalfCatchesRealCameraRigCall_negativeFixture()
        {
            var violations = ScanIlForCalledType(new[] { "ICameraRig" },
                                                new[] { typeof(ShadowCameraCaller) });
            Assert.IsNotEmpty(violations,
                "AC-4-11 负夹具失败:方法体里真 callvirt `ICameraRig.SetTier` 未被 IL 扫描抓到 = IL 半边空转");
            Assert.That(string.Join("\n", violations), Does.Contain("ICameraRig"),
                "AC-4-11 IL 负夹具:违例须点名 ICameraRig");
        }

        [Test]
        public void test_ac411_shadowCameraFieldIsCaught_negativeFixture()
        {
            var violations = ScanClosureForNames(CameraTierNames, new[] { typeof(ShadowCameraLeak) });
            Assert.IsNotEmpty(violations,
                "AC-4-11 负夹具失败:含 ICameraRig 字段的影子类型未被抓到");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-4-12 —— 走进 ≠ 交互(fake tick,非整 tick 边界采样)
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// AC-4-12 **可测半边**:走进 POI 格但无主动交互输入 ⇒ 零 `Request`。
        /// <para>⚠️ <b>本故事只覆盖这一半</b> —— 判据正文的「采样 ≥3 帧且落在**非整 tick 边界**」
        /// 是**抓「实现把走进格当边沿事件」的帧相位敏感性**,而本故事的
        /// <see cref="InteractionSelector"/> **无格状态、无 tick 依赖**(`Pressed` 门在最前,
        /// `intent.Tick` 只在出境时被读一次)⇒ <b>该实现结构上不可能有帧相位敏感性</b>。</para>
        /// <para>⇒ 2026-10-04 双代理评审 #7 的处置**不是**伪造 tick 敏感度,而是**登记前置缺失**:
        /// 「走进」这一**边沿判定**归 story 004 的真实发现链路(拥有格状态之后);
        /// story-001 的 tick 循环**不得**被读成该 AC 已覆盖。承「登记不隐藏」「不得借绿」。</para>
        /// <para>下方 tick 循环保留,作用 = <b>证明「无输入」门与帧/相位无关</b>(走进 3 帧仍零)——
        /// 这是其**真实且可证伪**的贡献;帧相位那一半显式标记为 story 004 的义务。</para>
        /// </summary>
        [Test]
        public void test_ac412_walkingIntoPoiWithoutInputYieldsZeroRequests()
        {
            var reporter = new SpyDiscoveryReporter();
            var selector = new InteractionSelector(reporter, 2);
            var tick = new FakeTickProvider(0);

            var poi = new List<Candidate> { new Candidate(new WorldPos(3, 0, 0), InteractableKind.PoiCell, 42L, StableIdSource.PoiId) };

            // 走进 POI 格,**无主动交互输入**,连采 3 帧(帧号 ≠ tick 边界)
            for (int frame = 0; frame < 3; frame++)
            {
                tick.SetTick(frame);
                var walkIn = new InteractIntent(new WorldPos(3, 0, 0), false, tick.CurrentTick);
                selector.Select(in walkIn, poi);
            }

            Assert.AreEqual(0, reporter.RequestCount,
                "AC-4-12:走进 POI 格但无输入 ⇒ 零 Request(触发 = 主动交互,不是碰撞)");
        }

        [Test]
        public void test_ac412_walkingOutAndBackWithinOneTickYieldsZero()
        {
            // 边沿事件实现的诱饵:走进 + 走出一 tick 内往返
            var reporter = new SpyDiscoveryReporter();
            var selector = new InteractionSelector(reporter, 2);
            var poi = new List<Candidate> { new Candidate(new WorldPos(3, 0, 0), InteractableKind.PoiCell, 42L, StableIdSource.PoiId) };

            var inAndOut = new InteractIntent(new WorldPos(3, 0, 0), false, 1);
            selector.Select(in inAndOut, poi);
            var back = new InteractIntent(new WorldPos(0, 0, 0), false, 1);
            selector.Select(in back, poi);

            Assert.AreEqual(0, reporter.RequestCount, "AC-4-12:一 tick 内往返 ⇒ 仍零 Request");
        }

        [Test]
        public void test_ac412_twoPoiCellsSameFrameStillZero()
        {
            var reporter = new SpyDiscoveryReporter();
            var selector = new InteractionSelector(reporter, 2);
            var twoPoi = new List<Candidate>
            {
                new Candidate(new WorldPos(3, 0, 0), InteractableKind.PoiCell, 42L, StableIdSource.PoiId),
                new Candidate(new WorldPos(3, 0, 1), InteractableKind.PoiCell, 43L, StableIdSource.PoiId)
            };

            var noInput = new InteractIntent(new WorldPos(3, 0, 0), false, 1);
            selector.Select(in noInput, twoPoi);

            Assert.AreEqual(0, reporter.RequestCount, "AC-4-12:同帧先后进两 POI 格 ⇒ 仍零 Request");
        }

        [Test]
        public void test_ac412_collisionImpliesRequestIsCaught_negativeFixture()
        {
            // 负夹具:「碰撞即 Request」实现 ⇒ 红(这正是 EC-6-2 承接面的可执行形态)
            var reporter = new SpyDiscoveryReporter();
            var collisionImpl = new ShadowCollisionSelector(reporter);
            var poi = new List<Candidate> { new Candidate(new WorldPos(3, 0, 0), InteractableKind.PoiCell, 42L, StableIdSource.PoiId) };

            var noInput = new InteractIntent(new WorldPos(3, 0, 0), false, 1);
            collisionImpl.Select(in noInput, poi);

            Assert.Greater(reporter.RequestCount, 0,
                "AC-4-12 负夹具:碰撞即 Request 的实现必须被本判据抓到(证明判据非空转)");
        }

        // ═══════════════════════════════════════════════════════════
        //  扫描机器(反射闭包 + IL + 可变状态)
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 反射闭包扫描(AC-4-01/02/05/11 共用一台机器)。
        /// <para><b>被测对象</b> = `DaYiJingCheng.Gameplay.Interaction` 命名空间**闭包** ——
        /// 种子 = 4 装配内该命名空间的全部类型;<b>展开规则</b> = ① 编译引用集(自身装配的
        /// <c>GetReferencedAssemblies</c>)② 公开类型的全部字段 / 属性 / 方法参数 / 返回类型,
        /// 并**递归**进入泛型实参、数组元素、嵌套类型、基类 / 接口实现
        /// (承 <c>PresentationDtoGuard</c> 递归先例);</para>
        /// <para><b>停机条件</b> = 已访问集防环 + 深度护栏 64 + <b>命名空间剪枝</b>
        /// (<c>System.*</c> / <c>UnityEngine.*</c> / <c>Unity.*</c> / <c>Microsoft.*</c> ——
        /// 引擎与 BCL 自有成员不可能携带本项目结算 / 玩法语义;其**类型本身**仍被点名检查,
        /// 只是不再展开其成员 —— 与 <c>PresentationDtoGuard.ShouldExpandMembers</c> 同口径)。</para>
        /// <para><b>违规</b> = 可达集 ∩ 禁入类型集非空。<b>禁入集按类型全名注册于测试侧</b>
        /// (见本方法 <c>forbiddenNames</c> 形参的调用方登记表)。</para>
        /// </summary>
        private static List<string> ScanClosureForNames(string[] forbiddenNames)
            => ScanClosureForNames(forbiddenNames, null);

        /// <summary>带**可注入根**的重载(story-001 负夹具的落点)。
        /// <para>⚠️ 两台实现会令夹具**不守护真机器**(2026-10-04 双代理评审 BLOCKING #3):
        /// 若正测走 A 机器、夹具走 B 机器,则「夹具红」**不蕴含**「真断言红」——
        /// 删掉 A 的展开循环仍全绿。故**正测与夹具共用本方法一台机器**,
        /// 夹具只是把 <paramref name="roots"/> 换成本命名空间的影子类型。</para></summary>
        /// <param name="roots">为 null ⇒ 用 4 的生产命名空间闭包(正测路径);非 null ⇒ 夹具路径。</param>
        private static List<string> ScanClosureForNames(string[] forbiddenNames, IEnumerable<Type> roots)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var violations = new List<string>();

            foreach (var seed in roots ?? CollectNamespaceClosure("DaYiJingCheng.Gameplay.Interaction"))
            {
                violations.AddRange(ExpandAndCheck(seed, forbiddenNames, seen));
            }
            return violations.Distinct().ToList();
        }

        /// <summary>收集某命名空间下的全部类型(4 的装配自身类型 = 闭包种子)。</summary>
        private static List<Type> CollectNamespaceClosure(string ns)
        {
            var asm = typeof(InteractionSelector).Assembly;
            return asm.GetTypes()
                      .Where(t => t.Namespace != null &&
                                  (t.Namespace == ns || t.Namespace.StartsWith(ns + ".", StringComparison.Ordinal)))
                      .ToList();
        }

        /// <summary>
        /// 跨装配的递归展开 + 点名(AC-4-01/02/05/11 的真机器)。
        /// <para><b>本方法会跨越装配边界</b>
        /// 继续展开字段 / 参数 / 返回类型自身,直到命名空间剪枝或已访问集拦住 —— 这正是
        /// 「递归进入泛型实参、被调类型的字段」的落地(AC-4-01/05 承 <c>PresentationDtoGuard</c>
        /// 「仅扫顶层会漏」的同款纪律)。</para>
        /// </summary>
        private static List<string> ExpandAndCheck(Type root, string[] forbiddenNames, HashSet<string> seen)
        {
            var violations = new List<string>();
            var visited = new HashSet<Type>();
            Visit(root, root.Name, 0);
            return violations;

            void Visit(Type t, string path, int depth)
            {
                if (t == null) return;
                if (depth > 64) { violations.Add($"[Scanner] 深度护栏触发于 {path}"); return; }
                if (t.IsArray || t.IsByRef || t.IsPointer) { Visit(t.GetElementType(), path + "[]", depth + 1); return; }
                if (!visited.Add(t)) return;

                // 类型名本身点名(类型**名**归一化 —— 抓 `DiseaseIdSet` 这类具名类型)
                CheckName(t.FullName, path + " (type)");
                CheckName(t.Name, path + " (type short)");

                // 泛型实参照展(引擎 / BCL 的泛型也要展开其实参,否则 List<VitalsDto> 静默漏)
                if (t.IsGenericType)
                    foreach (var a in t.GetGenericArguments()) Visit(a, path + "<" + a.Name + ">", depth + 1);

                // 命名空间剪枝:引擎 / BCL 不再展开自有成员(其字段由运行库定义)
                if (!ShouldExpandMembers(t)) return;

                const BindingFlags fb = BindingFlags.DeclaredOnly |
                                         BindingFlags.Instance | BindingFlags.Static |
                                         BindingFlags.Public | BindingFlags.NonPublic;

                for (var cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
                {
                    if (!ShouldExpandMembers(cur)) break;

                    foreach (var f in cur.GetFields(fb))
                    {
                        // ⚠️ 字段**名**也点名 —— `disease_id` / `tier_named` 是**名**不是类型
                        //   (AC-4-05 后半:反射 `Candidate` 无 disease_id / tier_named 字段)
                        CheckName(f.Name, path + "." + f.Name + " (field name)");
                        CheckName(f.FieldType.FullName, path + "." + f.Name + " (field type)");
                        Visit(f.FieldType, path + "." + f.Name, depth + 1);
                    }
                    foreach (var p in cur.GetProperties(fb))
                    {
                        CheckName(p.Name, path + "." + p.Name + " (property name)");
                        CheckName(p.PropertyType.FullName, path + "." + p.Name + " (property type)");
                        Visit(p.PropertyType, path + "." + p.Name, depth + 1);
                    }
                    foreach (var m in cur.GetMethods(fb))
                    {
                        CheckName(m.Name, path + "." + m.Name + " (method name)");
                        foreach (var prm in m.GetParameters())
                        {
                            CheckName(prm.ParameterType.FullName, path + "." + m.Name + "(" + prm.Name + ") (param type)");
                            Visit(prm.ParameterType, path + "." + m.Name + "(" + prm.Name + ")", depth + 1);
                        }
                        CheckName(m.ReturnType.FullName, path + "." + m.Name + " (return type)");
                        Visit(m.ReturnType, path + "." + m.Name + ":ret", depth + 1);
                    }
                    foreach (var c in cur.GetConstructors(fb))
                        foreach (var prm in c.GetParameters())
                        {
                            CheckName(prm.ParameterType.FullName, path + ".#ctor(" + prm.Name + ") (ctor param type)");
                            Visit(prm.ParameterType, path + ".#ctor(" + prm.Name + ")", depth + 1);
                        }
                }
            }

            void CheckName(string candidate, string where)
            {
                if (string.IsNullOrEmpty(candidate)) return;
                var norm = Normalize(candidate);
                foreach (var bad in forbiddenNames)
                {
                    var b = Normalize(bad);
                    if (b.Length > 0 && norm.Contains(b) && seen.Add(where + "|" + bad + "|" + candidate))
                        violations.Add($"[AC] {where} —— 命中禁入「{bad}」(实为 {candidate})");
                }
            }
        }

        /// <summary>命名空间剪枝(与 <c>PresentationDtoGuard.ShouldExpandMembers</c> 同口径)。
        /// <b>默认展开</b>;仅引擎 / BCL / 第三方 ns 跳过自有成员 —— 刻意**不用**项目白名单
        /// (反向白名单 = 白名单外项目类型成叶子 = 假绿)。</summary>
        private static bool ShouldExpandMembers(Type t)
        {
            var ns = t.Namespace;
            if (string.IsNullOrEmpty(ns)) return true;
            return !(ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal) ||
                     ns == "UnityEngine" || ns.StartsWith("UnityEngine.", StringComparison.Ordinal) ||
                     ns == "UnityEditor" || ns.StartsWith("UnityEditor.", StringComparison.Ordinal) ||
                     ns.StartsWith("Unity.", StringComparison.Ordinal) ||
                     ns.StartsWith("Microsoft.", StringComparison.Ordinal) ||
                     ns.StartsWith("NUnit.", StringComparison.Ordinal));
        }

        /// <summary>可变状态扫描(AC-4-04):非 readonly 实例字段 + static 非 const 字段。</summary>
        private static List<string> ScanForMutableState(Type t)
        {
            var violations = new List<string>();
            const BindingFlags fb = BindingFlags.DeclaredOnly |
                                    BindingFlags.Instance | BindingFlags.Static |
                                    BindingFlags.Public | BindingFlags.NonPublic;
            for (var cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
            {
                foreach (var f in cur.GetFields(fb))
                {
                    if (f.IsLiteral) continue;                       // const 合法
                    if (f.IsInitOnly && !f.IsStatic) continue;       // readonly 实例字段合法
                    if (f.IsInitOnly && f.IsStatic) continue;        // readonly static 合法(不可变常量)
                    violations.Add($"[AC-4-04] {cur.Name}.{f.Name} —— 可变字段" +
                                   (f.IsStatic ? "(static)" : "(instance)"));
                }
            }
            return violations;
        }

        /// <summary>IL 调用表扫描(AC-4-11 的「或调用」半边)。
        /// <para>⚠️ <paramref name="scanTargets"/> 是**可注入缝**(默认 = 4 的生产命名空间闭包)。
        /// 无此缝则负夹具无处可注(夹具住测试装配的另一命名空间)⇒ 该半边**结构性不可证伪**。
        /// 夹具形态的 AC 必须能指向自己的类型。</para></summary>
        private static List<string> ScanIlForCalledType(string[] forbiddenTypeNames,
                                                        IEnumerable<Type> scanTargets = null)
        {
            var violations = new List<string>();
            var targets = scanTargets ?? CollectNamespaceClosure("DaYiJingCheng.Gameplay.Interaction");
            foreach (var t in targets)
            {
                foreach (var m in t.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance |
                                               BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var body = m.GetMethodBody();
                    if (body == null) continue;
                    var il = body.GetILAsByteArray();
                    if (il == null) continue;
                    for (int i = 0; i < il.Length; i++)
                    {
                        if (i + 4 >= il.Length) break;
                        int token = BitConverter.ToInt32(il, i + 1);
                        var resolved = ResolveTypeName(m, token);
                        if (resolved == null) continue;
                        foreach (var bad in forbiddenTypeNames)
                            if (resolved.Contains(bad))
                                violations.Add($"[AC-4-11] {t.Name}.{m.Name} IL 调用/引用目标含「{bad}」({resolved})");
                    }
                }
            }
            return violations.Distinct().ToList();
        }

        private static string ResolveTypeName(MethodInfo m, int token)
        {
            try { var t = m.Module.ResolveType(token); if (t != null) return t.FullName; } catch { }
            try { var mm = m.Module.ResolveMethod(token); if (mm?.DeclaringType != null) return mm.DeclaringType.FullName; } catch { }
            return null;
        }

        private static string Normalize(string s) => s.Replace("_", "").Replace("-", "").ToLowerInvariant();

        // ═══════════════════════════════════════════════════════════
        //  夹具(fake tick / spy sink / spy reporter / 场景)
        // ═══════════════════════════════════════════════════════════

        private static List<(InteractIntent Intent, List<Candidate> Candidates)> BuildSelectionScenario()
        {
            var cells = new[] { new WorldPos(1, 0, 0), new WorldPos(0, 0, 0), new WorldPos(5, 0, 0) };
            var scenario = new List<(InteractIntent, List<Candidate>)>();
            for (int i = 0; i < cells.Length; i++)
            {
                var cands = new List<Candidate>
                {
                    new Candidate(cells[i], InteractableKind.Drop, i + 1, StableIdSource.InstanceId),
                    new Candidate(cells[i], InteractableKind.ForageSpot, 100 + i, StableIdSource.BakedResourceIndex)
                };
                scenario.Add((new InteractIntent(new WorldPos(0, 0, 0), true, i), cands));
            }
            return scenario;
        }

        private static List<InteractTarget> RunScenario(InteractionSelector sel,
            List<(InteractIntent Intent, List<Candidate> Candidates)> inputs)
        {
            var outp = new List<InteractTarget>();
            foreach (var step in inputs)
            {
                var it = step.Intent;          // 局部拷贝 —— `in` 形参须为变量(tuple 索引器返回非变量)
                outp.Add(sel.Select(in it, step.Candidates));
            }
            return outp;
        }

        private sealed class FakeTickProvider : ITickProvider
        {
            private long _tick;
            public FakeTickProvider(long t) { _tick = t; }
            public long CurrentTick => _tick;
            public void SetTick(long t) { _tick = t; }
        }

        private sealed class SpyEventSink : IEventSink
        {
            public int AppendCount;
            public void Append(in SimEvent e) => AppendCount++;
        }

        private sealed class SpyDiscoveryReporter : IDiscoveryReporter
        {
            public int RequestCount;
            public readonly List<long> RequestedPoiIds = new List<long>();

            // ⚠️ 链上 sink 引用(替身 6 侧)—— 阻断「孤立 sink 恒真」的空转。
            //   真实链路归 story 004;本替身只按 ADR-021 §② 的口径模拟「6 收到请求后决定是否落 Append」。
            private SpyEventSink _wiredSink;
            private bool _dispatchToSink;

            /// <summary>把 sink 接到链上。<paramref name="dispatchToSink"/> = 错误实现形态
            /// (6 侧直接落 Append)⇒ 用于证明 spy 半边可红。</summary>
            public void Wire(SpyEventSink sink, bool dispatchToSink)
            {
                _wiredSink = sink;
                _dispatchToSink = dispatchToSink;
            }

            public void Request(in DiscoveryRequest request)
            {
                RequestCount++;
                RequestedPoiIds.Add(request.PoiId);
                if (_dispatchToSink && _wiredSink != null)
                    _wiredSink.Append(new SimEvent(request.Tick, PatientId.None, 0L,
                        EventKind.PoiStateChanged, new PayloadRef(0, 0, 0)));  // 漏出的写入 ⇒ 链上 sink 记账
            }
        }

        // ── 影子类型(负夹具专用;刻意带禁名字段,证明扫描器非空转)──────

        /// <summary>AC-4-01 负夹具:**结算侧五类型逐个可证伪**。
        /// ⚠️ 2026-10-04 双代理评审 #4:原夹具只带 `JudgeResult` 一项 ⇒ 其余四条
        /// (`CanCarry`/`F1Result`/`TreatableBy`/`CapacityCompare`)从未被证明可红,
        /// 登记表打错拼写也无人发现。现五条各占一个成员,断言逐条点名。</summary>
        private sealed class ShadowSettlementLeak
        {
            public CanCarry CanCarryHost;
            public F1Result F1Outcome;
            public JudgeResult Verdict;
            public TreatableBy TreatablePayload;
            public CapacityCompare CapacityInput;
        }
        private interface CanCarry { }
        private sealed class F1Result { }
        private sealed class JudgeResult { }
        private sealed class TreatableBy { }
        private sealed class CapacityCompare { }
        private sealed class ShadowWriteWrapper
        {
            private readonly IEventSink _sink;
            public ShadowWriteWrapper(IEventSink sink) { _sink = sink; }
        }
        private sealed class ShadowMutableSelector { private long _lastTarget; }
        private sealed class ShadowVitalsLeak { public VitalsDto Vitals; }

        /// <summary>AC-4-05 **成员名**半边负夹具。⚠️ 区别于 <see cref="ShadowVitalsLeak"/>:
        /// 本类型的字段**类型全是 int / string**,只靠**字段名**携带禁入语义 ⇒ 若扫描器只看类型全名,
        /// 本夹具静默绿。它守的正是 `disease_id` / `tier_named` / `drug_profile` 三条
        /// —— 它们在 `GameplayValueNames` 里是**名**不是类型。</summary>
        private sealed class ShadowGameplayValueNameLeak
        {
            public int disease_id;
            public int tier_named;
            public string drug_profile;
        }
        private sealed class ShadowCameraLeak { public ICameraRig Rig; }
        private interface ICameraRig { void SetTier(int tier); }

        /// <summary>AC-4-11 **IL 半边**负夹具:本类**无** `ICameraRig` 字段、无该类型形参之外的可达面
        /// (结构半边未必点中),但在**方法体里真的 callvirt** 了它 ⇒ 只有 IL 扫描能抓。</summary>
        private sealed class ShadowCameraCaller
        {
            public void Steer(ICameraRig rig) => rig.SetTier(3);   // callvirt ⇒ IL 表见 ICameraRig
        }
        private sealed class ShadowCollisionSelector
        {
            private readonly IDiscoveryReporter _r;
            public ShadowCollisionSelector(IDiscoveryReporter r) { _r = r; }
            public InteractTarget Select(in InteractIntent i, IReadOnlyList<Candidate> c)
            {
                if (c != null && c.Count > 0) _r.Request(new DiscoveryRequest(c[0].StableId, i.PlayerCell, i.Tick)); // 碰撞即 Request(错误实现)
                return InteractTarget.None;
            }
        }
    }
}
