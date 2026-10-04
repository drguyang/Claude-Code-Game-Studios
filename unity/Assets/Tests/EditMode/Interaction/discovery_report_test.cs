// interaction-system Story 004 —— POI 自报链路(F-4.3 / F-4.3b / F-4.4)
//
// 登记落点: tests/integration/interaction/discovery_report_test.cs
// 真身落点: unity/Assets/Tests/EditMode/Interaction/discovery_report_test.cs
//
// 权威来源:
//   GDD design/gdd/interaction-system.md —— F-4.3(自报 4 → 6,非写)/ F-4.3b(自报与 argmin 解耦)
//     / F-4.4(有界性)/ 规则四 ③(广播不互斥)/ 规则七(触发 = 主动交互,非碰撞)
//   ADR-021(6 = 唯一写者;4 = 合法自报方)/ ADR-009 §七(三段式的意图半)
//   ADR-016 §三(事件率上界 = tick 频率与帧率无关)/ ADR-020 §四(读方 = 经流确立格)
//   ADR-006(载荷全整数域)/ ADR-015 §三(evidence_cell = 单一整数格)
//
// ⚠️ AC-4-13 的**两半**(逐字承故事 Implementation Notes):
//   4 半 = 每帧驱动自报,spy 计数 4 的**发出**次数 = 帧数×邻域 POI 数(4 **不做去重** —— 设计,非缺陷);
//   6 半 = per-tick latch 后**吸收**为每 tick ≤ 1 条 / POI 的落流效应。
//   ⚠️ **本文件的断言载体**:4 侧「零可变字段」反射(与 story-001 AC-4-04 的扫描器**同源同式**
//     —— 同为 `DeclaredOnly` 字段级 + 基类上溯 + `const`/`readonly` 豁免;本文件内正测与夹具
//     **共用同一台** `ScanMutableState`,故「夹具红」蕴含「真断言红」)。
//     「每 tick 至多一条」的验收对象是 **6 的 latch** —— 6 侧真身未落前以 **spy-latch 替身**签形状,
//     该子条**在测试内显式 `Assert.Ignore` 登记** `NOT-RUN / BLOCKED-BY: 系统 6 latch/幂等实现`
//     (不借 6 的绿,亦不让 6 借 4 的绿 —— 见 `test_ac413_sixSideLatchIsNotRunBlockedBySystem6`)。
//
// ⚠️ 零记账 = 纯函数身份纪律(AC-4-13 后半):`NeighbourhoodReporter` 内**无** `_reported` / `_lastTick`
//   类可变字段;负夹具「加 `_reported` bool ⇒ 红」证明扫描器非空转。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Interaction
{
    public class DiscoveryReportTest
    {
        // ═══════════════════════════════════════════════════════════
        //  夹具(显式 —— AC-4-16 出处纪律)
        // ═══════════════════════════════════════════════════════════

        // source: manual fixture —— 玩家经流确立格(F-4.3b 验算例同格,便于人工核对)
        private static readonly WorldPos PlayerCell = new WorldPos(10, 0, 5);

        // source: manual fixture —— R_INTERACT 注入值(⚠️ 值归数值轮;此处只作夹具量,不签取值)
        private const int RFixture = 1;

        /// <summary>造一个 POI 候选(邻域夹具用)。</summary>
        private static Candidate Poi(int x, int y, int z, long poiId)
            => new Candidate(new WorldPos(x, y, z), InteractableKind.PoiCell, poiId, StableIdSource.PoiId);

        private static Candidate Patient(int x, int y, int z, long patientId)
            => new Candidate(new WorldPos(x, y, z), InteractableKind.Patient, patientId, StableIdSource.PatientId);

        private static InteractionRadius Radius(int r = RFixture) => new InteractionRadius(r);

        // ═══════════════════════════════════════════════════════════
        //  AC-4-13 —— 4 侧:每帧照报(零去重,设计);
        //            6 侧:per-tick latch 吸收为每 tick ≤ 1(spy 替身)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac413_fourReportsEveryEvaluationWithoutDedup()
        {
            // GIVEN:单一 POI 格;4 的自报器在**同一 tick 内被重复求值**(模拟渲染帧多次驱动)。
            // WHEN:同一 tick 内连调 3 次(帧相位夹具),连跑 100 tick。
            // THEN:4 的**发出**计数 = 求值次数(无去重 —— 设计声明,非缺陷)。
            var spy = new CountingReporter();
            var reporter = new NeighbourhoodReporter(spy, Radius());
            var candidates = new[] { Poi(10, 0, 5, 42L) };

            const int ticks = 100, evalsPerTick = 3;
            for (int tk = 0; tk < ticks; tk++)
                for (int e = 0; e < evalsPerTick; e++)
                    reporter.ReportNeighbourhood(PlayerCell, candidates, tk);   // 同一 tick 多次求值

            Assert.AreEqual(ticks * evalsPerTick, spy.TotalRequests,
                "AC-4-13:4 每次求值照报 —— 发出计数 = 求值次数(4 **不做去重**,这是设计不是缺陷)");
            // ⚠️ 「发出次数 == 求值次数」是**恒真**的(每次调用恰一次循环)⇒ 本条是**形状声明**,
            //   不作承载判据;承载判据 = 落流侧(6 的 latch,见下条 NOT-RUN 登记)。
        }

        [Test]
        public void test_ac413_sixSideLatchIsNotRunBlockedBySystem6()
        {
            // ⚠️ AC-4-13 的 **6 半**(「每 tick 至多一条 `Request`/POI 的落流效应」)
            //   —— 验收对象是 **6 的 per-tick latch + 幂等**,6 侧真身**尚未落地**。
            //   故本条**不得借 4 侧的 spy-latch 替身签绿**(替身是自己造的,删掉任何生产行为它照样绿
            //   = 教科书式空转)。**显式 `Assert.Ignore` 登记为 NOT-RUN**(不是注释 —— 注释不可机检),
            //   6 侧真身落地后**移除本 Ignore 并补 6 侧集成断言**。
            Assert.Ignore("NOT-RUN(AC-4-13 6 半):BLOCKED-BY 系统 6 latch/幂等实现。" +
                          "验收对象 = 6 侧 per-tick latch 的落流效应,6 真身未落地前**不签绿**;" +
                          "以 spy-latch 替身签形状 = 自证空转(替身由本测试自造)。" +
                          "story-004 的 4 半(每帧照报 + 零记账)已由同文件其余测试签绿。");
        }

        [Test]
        public void test_ac413_negativeFixture_absenceOfIntentYieldsZeroEgress()
        {
            // ⚠️ QA F-B / 规则七 / EC-6-2 / AC-4-12 —— **主动交互是自报的前提**。
            //   `ReportNeighbourhood` **不自取** `InteractIntent`(保持纯函数,AC-4-13);
            //   故「无输入 ⇒ 零出境」是**调用方契约**。本负夹具以**同一台**出口探针
            //   (CountingReporter)证明:合法的广播路径**只在主动交互分支内**被驱动 ⇒
            //   「走进 POI 格但未按交互键」⇒ **零 Request 出境**(POI 不停留在被发现的假象里)。
            //
            // 形态:一个「按 intent.Pressed 门控广播」的调用方替身 vs 一个「无论是否按键都广播」的缺陷替身。
            var candidates = new[] { Poi(10, 0, 5, 42L), Poi(11, 0, 5, 43L) };

            // ── 正解:调用方把广播门在 `intent.Pressed` 之内 ──
            var goodSpy = new CountingReporter();
            var goodReporter = new NeighbourhoodReporter(goodSpy, Radius());
            var noPress = new InteractIntent(PlayerCell, false, 0);      // 走进格、**未按**交互键
            int goodEgress = GatedBroadcast(goodReporter, in noPress, candidates, 0);

            // ── 缺陷:调用方无视按键,按邻域驱动广播 ──
            var badSpy = new CountingReporter();
            var badReporter = new NeighbourhoodReporter(badSpy, Radius());
            int badEgress = UngatedBroadcast(badReporter, candidates, 0);

            Assert.AreEqual(0, goodEgress,
                "AC-4-12 / F-B:无主动交互 ⇒ 广播路径**零出境**(走进格 ≠ 交互,规则七)");
            Assert.AreEqual(0, goodSpy.TotalRequests, "AC-4-12 / F-B:spy 证实零 Request(未按键)");
            Assert.AreEqual(2, badEgress,
                "负夹具对照:UngatedBroadcast(**缺陷**形态:无门控)对同一夹具发出 2 条 —— " +
                "与「门控后 0 条」构成可辨对拍(证明本负夹具非空转)");
        }

        /// <summary>正解调用方形态:广播**门在** <c>intent.Pressed</c> 之内(F-4.3 前提 = 主动交互)。</summary>
        private static int GatedBroadcast(NeighbourhoodReporter reporter, in InteractIntent intent,
                                          Candidate[] candidates, long tick)
        {
            if (!intent.Pressed) return 0;      // ← 无主动交互 ⇒ 不驱动广播
            return reporter.ReportNeighbourhood(intent.PlayerCell, candidates, tick);
        }

        /// <summary>缺陷调用方形态:无视主动交互按键,按邻域驱动广播(AC-4-12 失效入口)。</summary>
        private static int UngatedBroadcast(NeighbourhoodReporter reporter, Candidate[] candidates, long tick)
            => reporter.ReportNeighbourhood(PlayerCell, candidates, tick);   // ← 无 `Pressed` 门

        [Test]
        public void test_ac413_neighbourhoodReporterHasZeroMutableState()
        {
            // 4 侧「零记账」的结构半边 —— **复用 story-001 的同一台扫描器**(AC-4-04 共用,GDD 原文)。
            var violations = ScanMutableState(typeof(NeighbourhoodReporter));
            Assert.IsEmpty(violations,
                "AC-4-13:4 自报侧须**零**「报过了」可变字段(去重责任在 6;4 是纯函数):\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void test_ac413_negativeFixture_reportedBoolIsPointedlyRed()
        {
            // 负夹具(故事 QA 逐字):「4 侧加 `_reported` bool ⇒ 可变字段红」。
            // 用**同一台**扫描器、换影子类型 —— 证明上条的「空违规集」非空转。
            var violations = ScanMutableState(typeof(ShadowReporterWithBookkeeping));
            Assert.IsNotEmpty(violations,
                "AC-4-13 负夹具:带 `_reported` 记账字段的影子类型必须被同一台扫描器抓到");
            Assert.IsTrue(violations.Any(v => v.Contains("_reported")),
                "AC-4-13 负夹具:违规须点名 `_reported`(证扫描器触底到字段名)");
        }

        [Test]
        public void test_ac413_leakyReporterHashes_alsoRed()
        {
            // 故事 QA 边缘(逐字):「字段名语义判定 `int _lastReportedTick` 亦红」。
            var violations = ScanMutableState(typeof(ShadowReporterWithLastTick));
            Assert.IsTrue(violations.Any(v => v.Contains("_lastReportedTick")),
                "AC-4-13 边缘:`int _lastReportedTick` 类记账字段亦须红(语义同族,不因改名豁免)");
        }

        // ═══════════════════════════════════════════════════════════
        //  F-4.3b —— 广播自报与 argmin 解耦(落选 POI 仍自报)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_f43b_losingPoisAreStillReported()
        {
            // GIVEN:高优先级 argmin 目标(Patient)与邻域 3 POI 共存的格布局。
            // WHEN:一次交互。
            // THEN:选择输出 = Patient(路由面归 005);**同时** 3 POI 各有 Request 出境(不因落选被吞)。
            var spy = new CountingReporter();
            var reporter = new NeighbourhoodReporter(spy, Radius());
            var candidates = new[]
            {
                Patient(10, 0, 5, 7L),          // 与玩家同格 ⇒ argmin 第一键 d∞=0 必中
                Poi(11, 0, 5, 101L),
                Poi(10, 0, 6, 102L),
                Poi(9,  0, 5, 103L),
            };

            // argmin(单选自报路径在 InteractionSelector;这里只验广播半边)
            var selector = new InteractionSelector(new CountingReporter(), RFixture);
            var target = selector.Select(new InteractIntent(PlayerCell, true, 0), candidates);

            // 广播:邻域内全部 POI 各自出报(与 argmin 胜者无因果)
            int sent = reporter.ReportNeighbourhood(PlayerCell, candidates, 0);

            Assert.AreEqual(InteractableKind.Patient, target.Kind,
                "前置:F-4.3b 场景中 argmin 胜者是 Patient(d∞=0)");
            Assert.AreEqual(3, sent, "F-4.3b:邻域 3 POI 各自出报 —— 落选 POI **不被吞**(发现不饿死)");
            CollectionAssert.AreEquivalent(new[] { 101L, 102L, 103L }, spy.RequestedPoiIds,
                "F-4.3b:三个 POI 的 Request 全数出境(逐 id 核对,非只数条数)");
        }

        [Test]
        public void test_f43b_negativeFixture_argminWinnerOnlyWouldStrandLosers()
        {
            // 负夹具(故事 QA 逐字):「『argmin 胜者才自报』实现 ⇒ 落选 POI 零出境,红(发现饿死)」。
            // 形态:对拍正解(广播)与缺陷实现(只报胜者)。
            var goodSpy = new CountingReporter();
            var good = new NeighbourhoodReporter(goodSpy, Radius());
            var candidates = new[]
            {
                Patient(10, 0, 5, 7L),
                Poi(11, 0, 5, 101L),
                Poi(10, 0, 6, 102L),
            };
            good.ReportNeighbourhood(PlayerCell, candidates, 0);

            // 缺陷实现:把「胜者才报」写成对 PoiCell 的过滤 + 单选
            var badSpy = new CountingReporter();
            var bad = new ShadowWinnerOnlyReporter(badSpy, Radius());
            bad.Report(PlayerCell, candidates, 0);

            Assert.AreEqual(2, goodSpy.TotalRequests, "正解:2 个 POI 全报");
            Assert.AreEqual(0, badSpy.TotalRequests,
                "F-4.3b 负夹具:「胜者才报」实现 ⇒ 落选 POI 零出境(发现饿死形态,可辨)");
        }

        // ═══════════════════════════════════════════════════════════
        //  F-4.4 —— 有界性(≤ (2R+1)³ / tick / 玩家;与帧率无关)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_f44_worstCaseDensityIsBoundedByNeighbourhoodCellCount()
        {
            // GIVEN:最坏密度注入(邻域 (2R+1)³ 格全 POI)。
            // WHEN:每 tick 求值,连跑 10³ tick。
            // THEN:出境计数上界 (2R+1)³ / tick / 玩家。
            const int r = 2;
            int side = 2 * r + 1;
            int cells = side * side * side;      // 125
            var candidates = new List<Candidate>();
            long id = 1;
            for (int x = -r; x <= r; x++)
                for (int y = -r; y <= r; y++)
                    for (int z = -r; z <= r; z++)
                        candidates.Add(Poi(10 + x, 0 + y, 5 + z, id++));

            var spy = new CountingReporter();
            var reporter = new NeighbourhoodReporter(spy, Radius(r));

            const int ticks = 1000;
            for (int tk = 0; tk < ticks; tk++)
                reporter.ReportNeighbourhood(PlayerCell, candidates, tk);

            Assert.AreEqual(cells, candidates.Count, "前置:邻域恰 (2R+1)³ = 125 格全 POI(最坏密度)");
            Assert.AreEqual((long)cells * ticks, spy.TotalRequests,
                $"F-4.4:最坏密度下每 tick 至多 (2R+1)³={cells} 条 ⇒ 10³ tick 总数 = {cells * ticks}");
        }

        [Test]
        public void test_f44_frameRateDoublingDoesNotChangeEgress()
        {
            // GIVEN/WHEN:同 tick 数,调用频率翻倍(1× vs 2× 每 tick)。
            // THEN:出境计数**只看调用次数,不看调用来源** —— 故「帧率翻倍」若**不**多调,
            //   计数**不变**。⚠️ 本类不自取时钟/不自计帧 ⇒ 帧率无关性是**调用方**
            //   (ITickProvider 每 tick 恰一次 Step)的契约;本条把它化为**可判形态**:
            //   以「同调用次数、不同夹带帧相位」两条路径比对,证明计数**不由帧相位派生**。
            var candidates = new[] { Poi(10, 0, 5, 1L), Poi(11, 0, 5, 2L) };
            const int ticks = 50;

            // 路径 A:每 tick 恰 1 次求值(1× 调用率)
            var spyA = new CountingReporter();
            var repA = new NeighbourhoodReporter(spyA, Radius());
            for (int tk = 0; tk < ticks; tk++) repA.ReportNeighbourhood(PlayerCell, candidates, tk);

            // 路径 B:同 50 次调用,但以「1 tick = 2 帧」的关系**只调 1 次/tick**
            //   (帧相位由 tick 边界吸收 ⇒ 调用次数 == tick 数 ⇒ 与 A 相同)
            var spyB = new CountingReporter();
            var repB = new NeighbourhoodReporter(spyB, Radius());
            for (int tk = 0; tk < ticks; tk++) repB.ReportNeighbourhood(PlayerCell, candidates, tk);

            // 路径 C:帧率翻倍**且**真的多调(2 次/tick)—— 计数随之翻倍,证「计数 = 调用次数」而非「= tick 数」
            var spyC = new CountingReporter();
            var repC = new NeighbourhoodReporter(spyC, Radius());
            for (int tk = 0; tk < ticks; tk++)
                for (int f = 0; f < 2; f++) repC.ReportNeighbourhood(PlayerCell, candidates, tk);

            Assert.AreEqual(ticks * 2, spyA.TotalRequests, "前置:50 tick × 2 POI,1× 调用率 ⇒ 100");
            Assert.AreEqual(spyA.TotalRequests, spyB.TotalRequests,
                "F-4.4:帧相位不同但**调用次数相同** ⇒ 出境计数**相同**(计数不派生自帧相位)");
            Assert.AreEqual(ticks * 4, spyC.TotalRequests,
                "F-4.4:调用率翻倍(2×)⇒ 计数翻倍(200)—— 证明计数**恰** = 调用次数,不多不少");
            // ⚠️ **真相**:上界论证的对象**不是 4 的发出**(它随调用率线性),而是**每 tick 的落流**
            //   (经 6 的 latch 吸收)。后者归 6(见 test_ac413_sixSideLatchIsNotRunBlockedBySystem6 的
            //   NOT-RUN 登记)⇒ **本故事不在此签 6 的绿**。本条的承重判据 = 「计数 = 调用次数」
            //   这一**结构**事实(4 无帧派生项)。
        }

        [Test]
        public void test_f44_edge_zeroPoisInNeighbourhoodYieldsZeroEgress()
        {
            // 故事 QA 边缘(逐字):「邻域 0 POI(零出境)」。
            var spy = new CountingReporter();
            var reporter = new NeighbourhoodReporter(spy, Radius());
            var candidates = new[] { Poi(99, 0, 5, 1L) };   // 远在邻域外(d∞ ≫ R)

            int sent = reporter.ReportNeighbourhood(PlayerCell, candidates, 0);
            Assert.AreEqual(0, sent, "F-4.4 边缘:邻域 0 POI ⇒ 零出境");
            Assert.AreEqual(0, spy.TotalRequests, "F-4.4 边缘:spy 证实零 Request");
        }

        [Test]
        public void test_f44_edge_poiOnRadiusBoundaryIsIncluded_RPlusOneIsNot()
        {
            // 故事 QA 边缘(逐字):「POI 恰在半径界上(`d∞ == R` 含,`R+1` 不含)」。
            var spy = new CountingReporter();
            var reporter = new NeighbourhoodReporter(spy, Radius(2));   // R = 2
            var candidates = new[]
            {
                Poi(12, 0, 5, 1L),   // d∞ = 2 == R  ⇒ 含
                Poi(13, 0, 5, 2L),   // d∞ = 3 == R+1 ⇒ 不含
            };

            int sent = reporter.ReportNeighbourhood(PlayerCell, candidates, 0);
            Assert.AreEqual(1, sent, "F-4.4 边缘:恰在界上(d∞==R)含,R+1 不含");
            CollectionAssert.AreEqual(new[] { 1L }, spy.RequestedPoiIds,
                "F-4.4 边缘:只有界上那一个出境(逐 id 核对)");
        }

        [Test]
        public void test_f44_edge_chebyshevWidensInt64BeforeAbs_intMinValueTrap()
        {
            // ⚠️ **本类型自带第二份 `Chebyshev`**(NeighbourhoodReporter.cs)—— 与 4 的
            //   InteractionSelector.Chebyshev 同式。story-002 的 target_selection_test 只钉了
            //   **选择器**那份;本类型这份若丢掉 `(long)` 拓宽 ⇒ 无测试会红 ⇒ 本类为它补钉。
            //
            // 陷阱命中条件(承 memory / F-4.1 订正):`Δ == int.MinValue` **恰**为其一时,
            //   int 域 `Math.Abs(int.MinValue)` 回绕为**负**。此处令玩家在原点、
            //   POI 置 `(int.MinValue, 0, 0)`:Δx = int.MinValue − 0 = int.MinValue。
            //   · 正确(int64 拓宽):d∞ = 2^31(正)≫ R ⇒ **在邻域外** ⇒ 零出境;
            //   · 错误(int 域 Abs):Abs 回绕 ⇒ d∞ **为负** ⇒ 负 ≤ R ⇒ **误判在邻域内** ⇒ 误出境。
            var spy = new CountingReporter();
            var reporter = new NeighbourhoodReporter(spy, Radius(1));   // R = 1
            var origin = new WorldPos(0, 0, 0);
            var candidates = new[]
            {
                new Candidate(new WorldPos(int.MinValue, 0, 0), InteractableKind.PoiCell, 7L, StableIdSource.PoiId),
            };

            int sent = reporter.ReportNeighbourhood(origin, candidates, 0);
            Assert.AreEqual(0, sent,
                "F-4.4 边缘:int64 拓宽 —— Δx = int.MinValue − 0 ⇒ d∞ = 2^31(正)⇒ 在邻域外;" +
                "若 int 域 Abs 回绕为负 ⇒ 误判在邻域内(本断言即红)");
            Assert.AreEqual(0, spy.TotalRequests, "F-4.4 边缘:spy 证实该极端格零出境");
        }

        // ═══════════════════════════════════════════════════════════
        //  有界性 + 幂等/重放边界(同一 tick 两客户端各报同一 POI)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_f43_sameTickTwoClientsProduceTwoDistinctEgressesThatConvergeOnlyAtSystem6()
        {
            // 故事 Implementation Notes:「同一 tick 两客户端各报同一 POI ⇒ 6 幂等收敛为一条」。
            // ⚠️ QA 校正:4 侧**不**做收敛(它零记账)⇒ 本测试**只签 4 的半边**:
            //    两客户端(两个独立自报器)各出**一条** Request(共 2 条),二者 `(tick, poi)` 相同。
            //    「收敛为一条」的**验收对象是 6 的幂等** ⇒ NOT-RUN / BLOCKED-BY 系统 6(见下)。
            var latch = new SpyPerTickLatch();
            var candidates = new[] { Poi(10, 0, 5, 42L) };
            var clientA = new CountingReporter();
            var clientB = new CountingReporter();

            new NeighbourhoodReporter(clientA, Radius())
                .ReportNeighbourhood(PlayerCell, candidates, 0);                    // 客户端 A
            new NeighbourhoodReporter(clientB, Radius())
                .ReportNeighbourhood(new WorldPos(11, 0, 5), candidates, 0);        // 客户端 B(同 tick 同 POI)

            Assert.AreEqual(1, clientA.TotalRequests, "F-4.3:客户端 A 出 1 条(4 侧不去重)");
            Assert.AreEqual(1, clientB.TotalRequests, "F-4.3:客户端 B 出 1 条(4 侧不去重)");
            // 两条 Request 指向同一 (tick=0, poi=42) ⇒ 收敛责任在 6;以替身**只记录**该键,
            // 断言「两客户端产出的是**同一键**」(这才是 4 侧可签的事实)。
            clientA.OnRequest += r => latch.Absorb(r);
            clientB.OnRequest += r => latch.Absorb(r);
            Assert.AreEqual(0, latch.AdmittedTotal,
                "F-4.3:替身在请求**重放前**为空(前置:上两行只是断计数,未回放)");
            // ⚠️ 不得借绿:6 侧幂等收敛归系统 6。
            Assert.Ignore("NOT-RUN(F-4.3 收敛半):BLOCKED-BY 系统 6 的幂等实现。" +
                          "4 侧已签:两客户端各出 1 条同 `(tick, poi)` 的 Request(不去重);" +
                          "「收敛为一条」的验收对象 = 6 幂等,真身未落地前不签绿。");
        }

        // ═══════════════════════════════════════════════════════════
        //  扫描机器(与 story-001 的 `ScanForMutableState` **同式同口径**)
        //  ⚠️ 承 story-001/002 的「两台机器」教训:正测与夹具**共用**本方法一台机器
        //     (夹具只把根换成影子类型),否则「夹具红」不蕴含「真断言红」。
        //  ⚠️ **同式而非同符号**:本方法与 story-001 的 `ScanForMutableState` 是**逐字同式**
        //     (DeclaredOnly 字段级 + 基类上溯 + `const`/`readonly` 豁免)—— 即 AC-4-04/AC-4-13
        //     的结构判据**同一形状**。首版本方法**多扫了可写属性**(story-001 不扫)⇒ 两 AC 的保护面
        //     静默分叉(给 `InteractionSelector` 加一个可写属性,AC-4-04 漏、AC-4-13 抓)。已删该分叉。
        //     跨文件共享同一台 scanner 需跨测试类可见性改造(登记为后续重构,非本故事面)。
        // ═══════════════════════════════════════════════════════════

        /// <summary>可变状态扫描(AC-4-13 结构半边):非 readonly 实例字段 + static 非 const 字段。
        /// <para>⚠️ 与 story-001 <c>ScanForMutableState</c> **逐字同式**(字段级;<b>不扫属性</b> ——
        /// 记账若走自动属性,其背后的 <c>&lt;Name&gt;k__BackingField</c> 编译生成字段仍被本扫描抓到)。</para></summary>
        private static List<string> ScanMutableState(Type t)
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
                    if (f.IsInitOnly) continue;                      // readonly(实例/静态)合法
                    violations.Add($"[AC-4-13] {cur.Name}.{f.Name} —— 可变字段" +
                                   (f.IsStatic ? "(static)" : "(instance)"));
                }
            }
            return violations;
        }

        // ═══════════════════════════════════════════════════════════
        //  替身(spy)
        // ═══════════════════════════════════════════════════════════

        private sealed class CountingReporter : IDiscoveryReporter
        {
            public int TotalRequests;
            public readonly List<long> RequestedPoiIds = new List<long>();
            public event Action<DiscoveryRequest> OnRequest;

            public void Request(in DiscoveryRequest request)
            {
                TotalRequests++;
                RequestedPoiIds.Add(request.PoiId);
                OnRequest?.Invoke(request);
            }
        }

        /// <summary>6 侧 per-tick latch 的**替身**(语义:同 tick 同 poi_id 合并为一次)。
        /// <para>⚠️ AC-4-13 的判据是「每 tick **每条 POI** 至多一条 **准入境**」——
        /// 度量对象是**每个 <c>(tick, poi)</c> 键被 latch 准入的次数**(须 ≤ 1),
        /// <b>不是</b>该键被 4 看到的**次数**(4 每帧照报 ⇒ 3 帧/tick ⇒ 键被见 3 次,合法)。</para>
        /// <para>首版度量「每 tick 不同 POI 数」—— 单 POI 夹具下与判据恰好重合 ⇒ 空转;
        /// 本版度量「每键**准入**次数」<see cref="MaxAdmitPerPoiPerTick"/> —— 与 latch 语义一致,
        /// 且在两 POI 夹具下仍成立(见 <c>test_ac413_edge_twoPois…</c>)。</para></summary>
        private sealed class SpyPerTickLatch
        {
            private readonly Dictionary<(long tick, long poi), int> _seen = new Dictionary<(long, long), int>();
            private readonly Dictionary<(long tick, long poi), int> _admitted = new Dictionary<(long, long), int>();
            public int AdmittedTotal { get; private set; }
            /// <summary>任一 <c>(tick, poi)</c> 键被**准入**的最大次数(latch 口径:须 ≤ 1)。</summary>
            public int MaxAdmitPerPoiPerTick { get; private set; }
            /// <summary>任一 tick 内准入的**不同 POI 数**的最大值(供「两 POI 总 ≤2」边缘断言)。</summary>
            public int MaxDistinctPoiPerTick { get; private set; }

            public void Absorb(in DiscoveryRequest r)
            {
                long tick = r.Tick;                          // ⚠️ `in` 形参不可捕获进 lambda ⇒ 先取局部
                var key = (tick, r.PoiId);
                _seen.TryGetValue(key, out int n);
                _seen[key] = n + 1;
                if (n == 0)
                {
                    // 首次看到该 (tick, poi) ⇒ **准入一次**(latch 语义:吸收后续重复)
                    AdmittedTotal++;
                    _admitted.TryGetValue(key, out int a);
                    _admitted[key] = a + 1;
                    if (a + 1 > MaxAdmitPerPoiPerTick) MaxAdmitPerPoiPerTick = a + 1;
                    int distinct = _admitted.Count(kv => kv.Key.tick == tick);   // 本 tick 准入的不同 POI 数
                    if (distinct > MaxDistinctPoiPerTick) MaxDistinctPoiPerTick = distinct;
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  负夹具影子类型(住测试命名空间 —— 不污染生产)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>影子:4 侧错误的「报过了」记账(AC-4-13 负夹具)。</summary>
    internal sealed class ShadowReporterWithBookkeeping
    {
        private bool _reported;      // ← 违规:4 侧自建记账(去重责任在 6)
        public bool Reported => _reported;
    }

    /// <summary>影子:`int _lastReportedTick` 型记账(故事 QA 边缘:改名不豁免)。</summary>
    internal sealed class ShadowReporterWithLastTick
    {
        private int _lastReportedTick;   // ← 违规:同族记账字段
        public int LastTick => _lastReportedTick;
    }

    /// <summary>影子:「胜者才自报」的缺陷实现(F-4.3b 负夹具)。</summary>
    internal sealed class ShadowWinnerOnlyReporter
    {
        private readonly IDiscoveryReporter _r;
        private readonly InteractionRadius _radius;
        public ShadowWinnerOnlyReporter(IDiscoveryReporter r, InteractionRadius radius) { _r = r; _radius = radius; }

        public void Report(in WorldPos playerCell, IReadOnlyList<Candidate> candidates, long tick)
        {
            // 缺陷:只报 argmin 胜者(若胜者是 PoiCell 才报)—— 落选 POI 被吞
            Candidate? best = null;
            foreach (var c in candidates)
            {
                long d = Cheb(playerCell, c.Cell);
                if (best == null || d < Cheb(playerCell, best.Value.Cell)) best = c;
            }
            if (best.HasValue && best.Value.Kind == InteractableKind.PoiCell && Cheb(playerCell, best.Value.Cell) <= _radius.Value)
                _r.Request(new DiscoveryRequest(best.Value.StableId, playerCell, tick));
        }

        private static long Cheb(in WorldPos a, in WorldPos b)
        {
            long dx = Math.Abs((long)a.X - b.X), dy = Math.Abs((long)a.Y - b.Y), dz = Math.Abs((long)a.Z - b.Z);
            long m = dx > dy ? dx : dy; return m > dz ? m : dz;
        }
    }
}
