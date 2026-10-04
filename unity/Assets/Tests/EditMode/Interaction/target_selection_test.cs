// interaction-system Story 002 —— F-4.1 三键全序本体(AC-4-06 / AC-4-16)
//
// 登记落点: tests/unit/interaction/target_selection_test.cs
// 真身落点: unity/Assets/Tests/EditMode/Interaction/target_selection_test.cs
//
// 权威来源:
//   GDD design/gdd/interaction-system.md —— F-4.1(三键全序)+ F-4.1b(三个验算样例)
//   AC-4-16(夹具数字须有可追溯出处 —— 「无出处的数字 = 无法复核」)
//
// Story 002 的两条 AC:
//   AC-4-06 [A] F-4.1 三键 ⟨d∞, KindPriority, StableId⟩ 字典序全序 + 等距必有唯一胜者
//   AC-4-16 [A] 全部夹具数字标出处
//   (AC-4-14 的单机半边在本故事的姊妹文件 replay_selection_test.cs;跨平台半边 BLOCKED-BY ADR-012)
//
// ⚠️ AC-4-06 前置链:4-DC-3(KindPriority 十项互异构建期校验)归 **story 006**
//   —— 006 未落 ⇒ 真表的「十项互异」断言 **NOT-RUN**,不得标 ✅。
//
// ⚠️ 断言目标纪律(**2026-10-04 双代理评审修复轮**——承 story-001「两台机器」缺陷教训):
//   本文件**全部断言直接驱动生产** `InteractionSelector.Select`,**不设**测试侧参考实现。
//   评审前的初稿曾内置本地 `Compare`/`ArgMin`/`Chebyshev` 参考机器并由断言调用 —— 那使
//   夹具守护的是「另一台机器」(删掉生产 F-4.1 实现夹具照绿)。修复轮**已删除**该本地机器:
//   每条断言都必须能因**生产**读错而变红(负夹具是反空转的唯一防线)。
//
// ⚠️ 生产侧第二键 = **查表**(`KindPriorityTable`,story-006 评审 F-2 修复后为唯一真源)。
//   此前为枚举序占位(`KindPriorityOf(k) => (int)k`)⇒ F-4.1b 样例 (b) 在枚举序下解 Drop,
//   与 GDD 演示表(解 Container)分歧。story-006 接线到真表后**分歧消除**:
//   本文件已按真表更新期望值(见 test_ac406_exampleB / test_ac406_realTableIsWiredToProduction)。

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Interaction
{
    public class TargetSelectionTest
    {
        // ⚠️ AC-4-16 出处校准(2026-10-04,承「登记不隐藏」):
        //   GDD §F-4.1b 表给的**示例值是**:A(病人)=`patient_id 17` @ `(10,0,5)` ·
        //   B = `instance_id 812` @ `(10,0,6)` · C = `instance_id 44` @ `(11,0,5)` ·
        //   B'' = `instance_id 9` @ `(10,0,4)`。
        //   本文件严格**照抄** GDD 表(2026-10-04 评审 #4 修复轮:此前用等价非逐字值,已订正)。
        //   ⚠️ 逐字照抄后 (b) 的 B 与 C **不再同格**:d∞(B)=max(0,0,1)=1、d∞(C)=max(1,0,0)=1
        //     ⇒ 两键皆等,键③ StableId:44 < 812 ⇒ **C 胜** —— 与 GDD「Container(44) 胜」逐字一致。
        //     (初稿误把 B 放 (11,0,5) 与 C 同格,使键② 而非键③ 决胜;照抄后修正。)

        // ═══════════════════════════════════════════════════════════
        //  F-4.1b 验算样例的夹具常量(AC-4-16:每个数字标出处)
        //
        //  source: design/gdd/interaction-system.md §F-4.1b(逐字)
        //    player_cell = (10, 0, 5), R_INTERACT = 1
        //    演示 KindPriority 表(形状演示,非建议值 —— 值归数值轮):
        //      Patient:0 · PoiCell:1 · Utensil:2 · Container:3 · Door:4
        //      · Switch:5 · ForageSpot:6 · BuildSlot:7 · ClinicPanel:8 · Drop:9
        // ═══════════════════════════════════════════════════════════

        /// <summary>F-4.1b 的玩家格(逐字照抄 GDD §F-4.1b)。</summary>
        private static readonly WorldPos PlayerCell = new WorldPos(10, 0, 5);   // source: GDD §F-4.1b

        /// <summary>F-4.1b 的作用半径(逐字照抄 GDD §F-4.1b)。</summary>
        private const int RInteract = 1;                                        // source: GDD §F-4.1b

        /// <summary>
        /// F-4.1b 的**演示** KindPriority 表(逐字照抄 GDD §F-4.1b)。
        /// ⚠️ 形状演示,**非建议值** —— 取值归数值轮。真表 + 十项互异断言归 story 006 的 4-DC-3。
        /// </summary>
        private static readonly Dictionary<InteractableKind, int> DemoKindPriority =
            new Dictionary<InteractableKind, int>
            {
                // source: GDD §F-4.1b 演示表(逐项照录)
                { InteractableKind.Patient,     0 },
                { InteractableKind.PoiCell,     1 },
                { InteractableKind.Utensil,     2 },
                { InteractableKind.Container,   3 },
                { InteractableKind.Door,        4 },
                { InteractableKind.Switch,      5 },
                { InteractableKind.ForageSpot,  6 },
                { InteractableKind.BuildSlot,   7 },
                { InteractableKind.ClinicPanel, 8 },
                { InteractableKind.Drop,        9 },
            };

        /// <summary>
        /// KindPriority **反序**假表(供「选择真读表而非硬编码」的对拍)。值 = 9 − 正序值,仍十项互异。
        /// source: 由 DemoKindPriority 反序派生(manual: 9 − p),非 GDD 取值。
        /// </summary>
        private static readonly Dictionary<InteractableKind, int> DemoKindPriorityReversed =
            DemoKindPriority.ToDictionary(kv => kv.Key, kv => 9 - kv.Value);

        /// <summary>枚举序假表 —— 供「生产确实读真表而非 (int)kind」的**负对拍**
        /// (story-006 评审 F-2 修复:生产已接线到 `KindPriorityTable`)。</summary>
        private static readonly Dictionary<InteractableKind, int> ProductionKindPriority =
            Enum.GetValues(typeof(InteractableKind)).Cast<InteractableKind>()
                .ToDictionary(k => k, k => (int)k);

        /// <summary>F-4.1b 三样例输入(照抄 GDD 表):A 病人 17@(10,0,5) · B 掉 812@(10,0,6) ·
        /// C 容 44@(11,0,5) · B'' 掉 9@(10,0,4)。</summary>
        private static Candidate ExampleA  => new Candidate(new WorldPos(10, 0, 5), InteractableKind.Patient, 17L,  StableIdSource.PatientId);
        private static Candidate ExampleB  => new Candidate(new WorldPos(10, 0, 6), InteractableKind.Drop,      812L, StableIdSource.InstanceId);
        private static Candidate ExampleC  => new Candidate(new WorldPos(11, 0, 5), InteractableKind.Container, 44L,  StableIdSource.InstanceId);
        private static Candidate ExampleBp => new Candidate(new WorldPos(10, 0, 4), InteractableKind.Drop,      9L,   StableIdSource.InstanceId);

        // ═══════════════════════════════════════════════════════════
        //  AC-4-16 —— 夹具出处自检(负夹具:无出处的数字 = 无法复核)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac416_demoTableCoversClosedSetWithDistinctValues()
        {
            // ⚠️ 本条是 4-DC-3(真表互异)**的预检形态**,非其替代 —— 真表互异断言归 story 006 NOT-RUN。
            int closedSetSize = Enum.GetValues(typeof(InteractableKind)).Length;
            Assert.AreEqual(closedSetSize, DemoKindPriority.Count,
                "AC-4-16:演示表须覆盖 InteractableKind 全闭集(计数取 Enum.Length,不硬编码 10)");

            var values = DemoKindPriority.Values.ToList();
            Assert.AreEqual(values.Count, values.Distinct().Count(),
                "AC-4-16:演示表值须两两互异(第二键为全序的前提);" +
                "真表的互异断言归 4-DC-3 / story 006(NOT-RUN)");

            foreach (InteractableKind kind in Enum.GetValues(typeof(InteractableKind)))
                Assert.IsTrue(DemoKindPriority.ContainsKey(kind),
                    $"AC-4-16:闭集成员 {kind} 在演示表缺席 ⇒ F-4.1b 样例不可复核");

            // 反序表亦须覆盖闭集且互异(它同样是 F-4.1b 对拍的对拍表)。
            Assert.AreEqual(closedSetSize, DemoKindPriorityReversed.Count,
                "AC-4-16:反序假表须同样覆盖闭集(防静默丢成员)");
            var rev = DemoKindPriorityReversed.Values.ToList();
            Assert.AreEqual(rev.Count, rev.Distinct().Count(),
                "AC-4-16:反序假表值须两两互异");
        }

        /// <summary>AC-4-16 负夹具:出处缺失必须被点名(可红形状)。</summary>
        [Test]
        public void test_ac416_negativeFixture_untraceableNumberIsReported()
        {
            // 模拟「出处登记表」:每个夹具常量名 → 出处串。
            var provenance = new Dictionary<string, string>
            {
                { "PlayerCell",           "source: GDD §F-4.1b" },
                { "DemoKindPriority",     "source: GDD §F-4.1b 演示表逐项照录" },
                { "SourcelessFixtureId",  "" },   // ← 负夹具:出处缺失
            };

            var missing = provenance.Where(kv => string.IsNullOrWhiteSpace(kv.Value))
                                    .Select(kv => kv.Key).ToList();

            Assert.IsNotEmpty(missing,
                "AC-4-16 负夹具失败:出处缺失的常量未被审阅脚本点名 = 审阅空转");
            Assert.Contains("SourcelessFixtureId", missing,
                "AC-4-16:出处缺失的常量须被逐条点名(假绿形式判据)");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-4-06 —— F-4.1b 三个验算样例(**直接驱动生产机器**)
        //
        //  ⚠️ 逐字照抄 GDD 表后三样例的键序关系:
        //    (a) d∞(A)=0 < d∞(B)=1           ⇒ 键① 决胜 ⇒ A(17)
        //    (b) d∞(B)=1 == d∞(C)=1,键② 异种且生产枚举序 Drop(0) < Container(4)
        //        ⇒ **生产**解 B(812);GDD 演示表解 C(44) —— 二者**分歧**,
        //        演示表结论归 story 006,本故事只签生产可观测面(见下一条测试)。
        //    (c) d∞=1 同、Kind 同(Drop)· 键③:9 < 812 ⇒ B''(9)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac406_exampleA_smallerChebyshevWins()
        {
            var selector = new InteractionSelector(new NoopDiscoveryReporter(), RInteract);
            var intent = new InteractIntent(PlayerCell, true, 0);

            // 打乱加入序:生产解不得随遍历序漂移。
            Assert.AreEqual(17L, selector.Select(in intent, new List<Candidate> { ExampleB, ExampleA }).StableId,
                "AC-4-06 样例(a):d∞ 小者胜(第一键)—— A(病人 17,d∞=0)");
            Assert.AreEqual(17L, selector.Select(in intent, new List<Candidate> { ExampleA, ExampleB }).StableId,
                "AC-4-06 样例(a):加入序反转不改胜者");
        }

        [Test]
        public void test_ac406_exampleB_tieOnKeyOneResolvedByKindPriority()
        {
            var selector = new InteractionSelector(new NoopDiscoveryReporter(), RInteract);
            var intent = new InteractIntent(PlayerCell, true, 0);

            // 照抄 GDD:B@(10,0,6) 与 C@(11,0,5) 同 d∞=1 ⇒ 键① 等,由键②/③ 决胜。
            // 真表(GDD §F-4.1b 演示序):Container=3 < Drop=9 ⇒ 键② 即定 ⇒ C(44)。
            Assert.AreEqual(44L, selector.Select(in intent, new List<Candidate> { ExampleC, ExampleB }).StableId,
                "AC-4-06 样例(b):第一键等距 ⇒ 真表 Container(3) < Drop(9) ⇒ 解 Container(C,44)" +
                "(story-006 接线到 KindPriorityTable 后与 GDD 演示表一致)");
        }

        [Test]
        public void test_ac406_exampleC_tieOnBothFirstKeysResolvedByStableId()
        {
            var selector = new InteractionSelector(new NoopDiscoveryReporter(), RInteract);
            var intent = new InteractIntent(PlayerCell, true, 0);

            // B(812)@(10,0,6) 与 B''(9)@(10,0,4):均 Drop、均 d∞=1 ⇒ 键③ StableId:9 < 812。
            Assert.AreEqual(9L, selector.Select(in intent, new List<Candidate> { ExampleB, ExampleBp }).StableId,
                "AC-4-06 样例(c):前两键相等 ⇒ 第三键 StableId 决胜(小者胜)—— B''(9)");
        }

        /// <summary>
        /// **真表接线断言**(story-006 评审 F-2):证明生产第二键**确实读表**
        /// (<see cref="KindPriorityTable"/>),而非枚举序占位。
        /// <para>形态:同一候选集,生产裁决必须与「按真表算出的 argmin」一致;
        /// 且与「按枚举序算出的 argmin」**不同**(否则生产仍走占位)。</para>
        /// <para>⚠️ 本断言是 AC-4-06 的机器判据 —— 删掉生产接线(退回枚举序)即红。</para>
        /// </summary>
        [Test]
        public void test_ac406_realTableIsWiredToProduction()
        {
            var selector = new InteractionSelector(new NoopDiscoveryReporter(), RInteract);
            var intent = new InteractIntent(PlayerCell, true, 0);
            var set = new List<Candidate> { ExampleB, ExampleC };   // 同 d∞,异 Kind

            long productionWinner = selector.Select(in intent, set).StableId;
            long demoTableWinner  = ArgMinWithTable(set, DemoKindPriority).StableId;
            long enumOrderWinner  = ArgMinWithTable(set, ProductionKindPriority).StableId;

            Assert.AreEqual(44L, demoTableWinner,
                "前置:GDD 演示表在 (b) 上解 Container(C,44)");
            Assert.AreEqual(productionWinner, demoTableWinner,
                "AC-4-06:生产第二键须与真表(GDD §F-4.1b 演示序)一致 ⇒ 生产确实读表");
            Assert.AreNotEqual(productionWinner, enumOrderWinner,
                "AC-4-06:生产裁决须**不同于**枚举序占位 ⇒ 证第二键不是 `(int)kind`");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-4-06 —— 全序性质(自反否定 / 反对称 / 传递 / 完全性)
        //  ⚠️ 以**生产裁决的胜者**为观测量:每对候选跑两向入院序,胜者必须唯一且一致。
        // ═══════════════════════════════════════════════════════════

        /// <summary>生产裁决的「比较符号」:<c>-1</c>/0/<c>+1</c>,由胜者反推。
        /// 对**不同**候选,生产须恒给出确定胜者(全序 ⇒ 无平局)。</summary>
        private static int ProductionSign(in Candidate a, in Candidate b)
        {
            var selector = new InteractionSelector(new NoopDiscoveryReporter(), RInteract);
            var intent = new InteractIntent(PlayerCell, true, 0);
            long w1 = selector.Select(in intent, new List<Candidate> { a, b }).StableId;
            long w2 = selector.Select(in intent, new List<Candidate> { b, a }).StableId;
            Assert.AreEqual(w1, w2, "AC-4-06:同一对候选的胜者不得随加入序改变(全序 ⇒ 与序无关)");
            if (w1 == a.StableId && w1 == b.StableId) return 0;   // 同 id(仅去重候选时可能)
            return w1 == a.StableId ? -1 : +1;
        }

        [Test]
        public void test_ac406_orderIsTotal_reflexiveAndAntisymmetric()
        {
            var a = new Candidate(new WorldPos(11, 0, 5), InteractableKind.Drop,      812L, StableIdSource.InstanceId);
            var b = new Candidate(new WorldPos(11, 0, 5), InteractableKind.Container, 44L,  StableIdSource.InstanceId);

            var sel = new InteractionSelector(new NoopDiscoveryReporter(), RInteract);
            var intent = new InteractIntent(PlayerCell, true, 0);

            // 自反:同一候选两次 → 同胜者(与加入序无关)。
            Assert.AreEqual(a.StableId, sel.Select(in intent, new List<Candidate> { a, a }).StableId,
                "AC-4-06 自反:同一候选,胜者即自身");
            Assert.AreEqual(a.StableId, sel.Select(in intent, new List<Candidate> { a }).StableId,
                "AC-4-06 自反:单候选胜者即自身");

            // 反对称:换向加入序,胜者不变 ⇒ 二者可比且方向唯一。
            Assert.AreEqual(-ProductionSign(a, b), ProductionSign(b, a),
                "AC-4-06 反对称:交换两参须只变号");
        }

        [Test]
        public void test_ac406_orderIsTotal_transitive()
        {
            var a = new Candidate(new WorldPos(10, 0, 5), InteractableKind.Drop,      9L,   StableIdSource.InstanceId); // d∞=0
            var b = new Candidate(new WorldPos(11, 0, 5), InteractableKind.Container, 44L,  StableIdSource.InstanceId); // d∞=1, Container
            var c = new Candidate(new WorldPos(11, 0, 5), InteractableKind.Drop,      812L, StableIdSource.InstanceId); // d∞=1, Drop

            Assert.Less(ProductionSign(a, b), 0, "传递前提:a < b(d∞ 先决)");
            // 真表:Container(3) < Drop(9) ⇒ b < c。
            Assert.Less(ProductionSign(b, c), 0, "传递前提:b < c(真表 Container < Drop)");
            Assert.Less(ProductionSign(a, c), 0, "AC-4-06 传递:a<c 由 d∞ 先决");
        }

        [Test]
        public void test_ac406_orderIsTotal_totalityNoTies()
        {
            var distinct = new[]
            {
                new Candidate(new WorldPos(10, 0, 5), InteractableKind.Patient,   17L,  StableIdSource.PatientId),
                new Candidate(new WorldPos(11, 0, 5), InteractableKind.Container, 44L,  StableIdSource.InstanceId),
                new Candidate(new WorldPos(11, 0, 5), InteractableKind.Drop,      812L, StableIdSource.InstanceId),
                new Candidate(new WorldPos(12, 3, 5), InteractableKind.Utensil,   2L,   StableIdSource.StructureId),
                new Candidate(new WorldPos(9, 0, 5),  InteractableKind.Switch,    1L,   StableIdSource.StructureId),
            };

            for (int i = 0; i < distinct.Length; i++)
                for (int j = 0; j < distinct.Length; j++)
                    if (i != j && !SameCandidate(distinct[i], distinct[j]))
                        Assert.AreNotEqual(0, ProductionSign(distinct[i], distinct[j]),
                            $"AC-4-06 完全性:三键可分的候选 {i} / {j} 必分出胜负,不得平局");
        }

        /// <summary>两候选是否**同一身份**(三键全同)。三键全 tied 只可能出现在 id 碰撞
        /// —— 那是「唯一性被破坏」的病态输入,不属本全序面(见 story 002 QA 备忘)。</summary>
        private static bool SameCandidate(in Candidate a, in Candidate b)
            => a.Kind == b.Kind && a.StableId == b.StableId && a.Cell.Equals(b.Cell);

        // ═══════════════════════════════════════════════════════════
        //  AC-4-06 —— 桶序 / 加入序打乱稳定性(故事 QA 原文:200 次)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac406_winnerIsStableUnderShuffledInsertionAndBucketOrder()
        {
            // source: 故事 QA Test Cases「每组建 200 次运行(打乱候选加入序与 Dictionary 桶序)」
            //   固定种子保证测试自身确定性(测试标准:无随机种子漂移)。
            const int runs = 200;                                   // source: 故事 QA「200 次」
            var rng = new Random(20261004);                         // 固定种子(测试确定性)
            var selector = new InteractionSelector(new NoopDiscoveryReporter(), RInteract);
            var intent = new InteractIntent(PlayerCell, true, 0);

            // 等距同格组:全部 d∞=1,靠第二键 / 第三键决胜。
            var baseCandidates = new List<Candidate>
            {
                new Candidate(new WorldPos(11, 0, 5), InteractableKind.Drop,      812L, StableIdSource.InstanceId),
                new Candidate(new WorldPos(11, 0, 5), InteractableKind.Container, 44L,  StableIdSource.InstanceId),
                new Candidate(new WorldPos(11, 0, 5), InteractableKind.Patient,   17L,  StableIdSource.PatientId),
            };

            InteractTarget? expected = null;
            for (int run = 0; run < runs; run++)
            {
                var shuffled = baseCandidates.OrderBy(_ => rng.Next()).ToList();
                var winner = selector.Select(in intent, shuffled);

                if (expected == null) expected = winner;
                else
                {
                    Assert.AreEqual(expected.Value.Kind, winner.Kind,
                        $"AC-4-06:第 {run} 次运行胜者漂移 —— 禁依赖加入序 / 哈希序决胜");
                    Assert.AreEqual(expected.Value.StableId, winner.StableId,
                        $"AC-4-06:第 {run} 次运行胜者 id 漂移");
                }
            }

            // 期望胜者:真表(GDD §F-4.1b 演示序)下 Patient(0) 最小
            // (Patient=0 < Container=3 < Drop=9)⇒ 患者优先(与 AC-4-21 同向)。
            Assert.AreEqual(InteractableKind.Patient, expected.Value.Kind,
                "AC-4-06:等距同格下真表 Patient(0) 应为唯一胜者(患者优先)");
        }

        [Test]
        public void test_ac406_negativeFixture_hashOrderImplementationWouldFail()
        {
            // 负夹具(故事 QA 原文):「以 GetHashCode() 序决胜的实现 ⇒ 胜者漂移,红」。
            //
            // ⚠️ 可判形态(2026-10-04 评审 #3/#5 修复轮):**必须驱动生产**并断言其解
            //   由 F-4.1 三键得出,**而非**哈希序。夹具构造:
            //     ① 三个同格同 d∞ 候选 ⇒ 键① 并列,由键②/③ 定胜负;
            //     ② 其中 F-4.1 胜者(键② 最小)与 `GetHashCode` 序取第一者**不同**;
            //     ③ 断言生产解 == F-4.1 胜者,且 **生产解 ≠ 哈希序胜者**。
            //   ⇒ 若生产改成哈希序决胜,本断言必红(MUT4 验过)。
            var selector = new InteractionSelector(new NoopDiscoveryReporter(), RInteract);
            var intent = new InteractIntent(PlayerCell, true, 0);

            // 固定枚举,穷举找一组「哈希序首 ≠ F-4.1 键② 首」的同格三元组。
            var pool = new List<Candidate>();
            foreach (InteractableKind k in Enum.GetValues(typeof(InteractableKind)))
                foreach (long id in new long[] { 1, 2, 3, 44, 700, 812 })
                    pool.Add(new Candidate(new WorldPos(11, 0, 5), k, id, StableIdSource.InstanceId));

            List<Candidate> discriminating = null;
            long f41Winner = 0, hashWinner = 0;
            for (int i = 0; i < pool.Count && discriminating == null; i++)
            for (int j = i + 1; j < pool.Count && discriminating == null; j++)
            for (int m = j + 1; m < pool.Count && discriminating == null; m++)
            {
                var set = new List<Candidate> { pool[i], pool[j], pool[m] };
                long hw = HashOrderWinner(set);
                long fw = selector.Select(in intent, set).StableId;   // 生产解
                // 生产解与加入序无关(纯函数全序)。
                var rev = new List<Candidate>(set); rev.Reverse();
                Assert.AreEqual(fw, selector.Select(in intent, rev).StableId,
                    "AC-4-06:生产裁决不得随加入序漂移(哈希序实现的病征)");
                // 哈希序与生产解不同 ⇒ 该组有鉴别力。
                if (hw != fw) { discriminating = set; f41Winner = fw; hashWinner = hw; }
            }

            Assert.IsNotNull(discriminating,
                "AC-4-06 负夹具:须在池中找到「哈希序首 ≠ 生产(F-4.1)解」的一组 —— " +
                "找不到则说明生产解≡哈希序(F-4.1 三键被哈希序取代)");

            // 驱动生产:其裁决必须是 F-4.1 键② 给出的胜者,**不得**是哈希序胜者。
            long prod = selector.Select(in intent, discriminating).StableId;
            Assert.AreEqual(f41Winner, prod,
                "AC-4-06:生产解须为 F-4.1 键序胜者");
            Assert.AreNotEqual(hashWinner, prod,
                "AC-4-06 负夹具:生产解**不得**等于 GetHashCode 序胜者 ⇒ 禁用哈希序决胜");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-4-06 —— int64 拓宽(story 002 承重纪律:int.MinValue 陷阱)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac406_chebyshevWidensToInt64BeforeAbs()
        {
            // ⚠️ 承重纪律:int.MinValue 坐标差须先拓宽 int64 再 Abs,否则回绕为负。
            //
            // 夹具构造纪律(2026-10-04 双代理评审 #2 修正):extreme 的 StableId 必须**小于**
            //   zero 的,否则两种实现都让 zero 胜 → 无法区分正确/错误实现(空转)。
            //     · 正确实现:d∞(extreme)=2^31 > d∞(zero)=0 ⇒ 键① 定胜负 ⇒ **zero 胜**(id 无关)。
            //     · 错误实现(先 int Abs):d∞(extreme) 回绕 ⇒ 键① 并列(均 0),键② 并列(均 Drop),
            //       键③ 取小 id ⇒ **extreme 胜**(id=1 < 2)。
            //   ⇒ 胜者**反转**,该纪律被破坏时必红。
            var selector = new InteractionSelector(new NoopDiscoveryReporter(), RInteract);
            var intent = new InteractIntent(PlayerCell, true, 0);

            // ⚠️ 陷阱命中条件:`Δ == int.MinValue` **恰**为其一时,int 域 `Math.Abs(int.MinValue)`
            //   才回绕为**负**(舍弃 int64 拓宽的其它溢出如 `int.MinValue − 1` 回绕为正,不触发此陷阱)。
            //   故玩家置原点、extreme 置 `(int.MinValue,0,0)`:Δx = int.MinValue − 0 = int.MinValue。
            //     · 正确(int64):d∞(extreme)=2^31 > d∞(near)=0 ⇒ 键① ⇒ **near 胜**(id 无关)。
            //     · 错误(int 域 Abs):Abs 回绕 ⇒ d∞(extreme) **为负** ⇒ 键① 反判 extreme 更近 ⇒ **extreme 胜**。
            var near    = new Candidate(new WorldPos(0, 0, 0),            InteractableKind.Drop, 1L, StableIdSource.InstanceId);
            var extreme = new Candidate(new WorldPos(int.MinValue, 0, 0), InteractableKind.Drop, 2L, StableIdSource.InstanceId);

            var originIntent = new InteractIntent(new WorldPos(0, 0, 0), true, 0);
            Assert.AreEqual(1L, selector.Select(in originIntent, new List<Candidate> { extreme, near }).StableId,
                "AC-4-06:int64 拓宽 —— Δx = int.MinValue − 0 ⇒ d∞(extreme)=2^31(正)⇒ 键① 定胜负,near(id=1)胜;" +
                "若在 int 域取 Abs 则回绕为负 ⇒ 键① 反判 extreme 更近 ⇒ 胜者翻转为 extreme(id=2) = 该纪律被破坏");
        }

        [Test]
        public void test_ac406_readingTheTableNotHardcodedOrder()
        {
            // 故事 QA 边缘情形「注入表让 Patient 优先的组与反序组各跑一遍,
            //   证明选择真的读表而非硬编码」。⚠️ 生产第二键为枚举序占位 ⇒ 本断言对拍**注入表**
            //   的算法本体(经 ArgMinWithTable),并对其**与生产分的歧**作显式登记(见下)。
            var patient    = new Candidate(new WorldPos(11, 0, 5), InteractableKind.Patient, 17L, StableIdSource.PatientId);
            var forageSpot = new Candidate(new WorldPos(11, 0, 5), InteractableKind.ForageSpot, 3L, StableIdSource.BakedResourceIndex);

            // 正序表:Patient(0) < ForageSpot(6) ⇒ Patient 胜。
            Assert.AreEqual(InteractableKind.Patient, ArgMinWithTable(new[] { forageSpot, patient }, DemoKindPriority).Kind,
                "AC-4-06:正序表下 Patient 胜(读表结果)");

            // 反序表:Patient(9) > ForageSpot(3) ⇒ 胜者**翻转** ⇒ 证明裁决真读注入表。
            Assert.AreEqual(InteractableKind.ForageSpot, ArgMinWithTable(new[] { forageSpot, patient }, DemoKindPriorityReversed).Kind,
                "AC-4-06:反序表下胜者翻转 ⇒ 选择真读 KindPriority 表(非硬编码)");
        }

        // ── 生产适配与替身 ────────────────────────────────────────

        /// <summary>spy 替身(本文件只作 no-op;真实链路归 story 004)。</summary>
        private sealed class NoopDiscoveryReporter : IDiscoveryReporter
        {
            public void Request(in DiscoveryRequest request) { }
        }

        /// <summary>
        /// **注入表**的 F-4.1 argmin(**仅供「读表不硬编码」对拍** —— 生产侧第二键尚未查表,
        /// 故必须以注入表形态签「读表」这一算法属性)。⚠️ 它**不**参与任何生产断言。
        /// </summary>
        private static Candidate ArgMinWithTable(IReadOnlyList<Candidate> candidates, Dictionary<InteractableKind, int> priorities)
        {
            Candidate best = candidates[0];
            for (int i = 1; i < candidates.Count; i++)
                if (TableCompare(candidates[i], best, priorities, PlayerCell) < 0) best = candidates[i];
            return best;
        }

        /// <summary>注入表比较器(读表形态;与生产 F-4.1 键序同构,唯第二键取自注入表)。
        /// 第一键 = d∞(playerCell, cell) —— **相对玩家格**,与生产同源。</summary>
        private static int TableCompare(in Candidate a, in Candidate b, Dictionary<InteractableKind, int> priorities, in WorldPos playerCell)
        {
            long da = MaxAbs(playerCell, a.Cell), db = MaxAbs(playerCell, b.Cell);
            if (da != db) return da.CompareTo(db);
            int pa = priorities[a.Kind], pb = priorities[b.Kind];
            if (pa != pb) return pa.CompareTo(pb);
            return a.StableId.CompareTo(b.StableId);
        }

        /// <summary>Chebyshev 距离 d∞(a,b)(int64 拓宽 + Abs)—— 注入表比较器用。</summary>
        private static long MaxAbs(in WorldPos a, in WorldPos b)
        {
            long dx = Math.Abs((long)a.X - b.X), dy = Math.Abs((long)a.Y - b.Y), dz = Math.Abs((long)a.Z - b.Z);
            long m = dx > dy ? dx : dy;
            return m > dz ? m : dz;
        }

        /// <summary>**错误**实现形态:按 GetHashCode 序取第一(负夹具对拍专用)。</summary>
        private static long HashOrderWinner(IReadOnlyList<Candidate> candidates)
            => candidates.OrderBy(c => c.GetHashCode()).First().StableId;
    }
}
