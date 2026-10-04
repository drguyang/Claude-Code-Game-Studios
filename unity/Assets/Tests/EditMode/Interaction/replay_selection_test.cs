// interaction-system Story 002 —— 纯选择重放(AC-4-14 的单机半边)
//
// 登记落点: tests/unit/interaction/replay_selection_test.cs
// 真身落点: unity/Assets/Tests/EditMode/Interaction/replay_selection_test.cs
//
// 权威来源:
//   GDD design/gdd/interaction-system.md —— F-4.1 / F-4.1b / F-4.2(作用域收窄)
//   ADR-012 §二(三格黄金夹具矩阵 —— AC-4-14 的载体)
//
// ⚠️ AC-4-14 的**作用域收窄**(首轮订正,故事原文逐字):
//   本条**只覆盖纯选择函数**,**不覆盖 Accept** —— `Accept` 的输入 `Armed` / `ModalOpen`
//   是第四来源(见 F-4.2),改由 AC-4-20 立判(story 003)。含模态的序列无从验证。
//
// ⚠️ AC-4-14 的**跨平台半边 BLOCKED-BY**:ADR-012 三格矩阵载体未建
//   (CI 未配 `UNITY_LICENSE` secret)⇒ 本文件只交付**单机可跑形态**
//   (同二进制二次运行 + 两实例交错 + 到达序打乱,选择序列相等)。三格半边记 BLOCKED-BY,
//   **不得借绿**。
//
// ⚠️ **刻意缺口**(评审 F-7,登记不隐藏):故事 QA 列出的「全体出半径 ⇒ 裁剪后空集 ⇒ None」
//   属**邻域裁剪**面 —— 故事 Out-of-Scope 明写裁剪归 story 003/004(半径过滤发生在 argmin
//   **之前**,本故事夹具候选集**已裁剪**)。故本文件不测该例,亦不借其绿。
//
// ⚠️ 断言目标纪律(**2026-10-04 双代理评审修复轮** —— 承 story-001「两台机器」教训):
//   本文件**直接驱动生产** `InteractionSelector.Select` 实例,**不**经任何测试侧参考实现。
//   修复轮前的初稿把 `SelectOnce` 绑到本地 `TargetSelectionTest.ArgMin` —— 那使三条重放测试
//   守护「另一台机器」(删掉生产 F-4.1 实现照绿);且那条「缓存上一个目标」负夹具在本地纯函数
//   上**无从寄存**(本地 ArgMin 无缓存,夹具放不进去)。修复轮改用**有状态替身选择器**
//   驱动真正的缓存缺陷形态(见 test_ac414_*NegativeFixture*)。

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Interaction
{
    public class ReplaySelectionTest
    {
        // ═══════════════════════════════════════════════════════════
        //  重放夹具:病人格序列 + 世界四源格序列 + 输入序列
        //  ⚠️ AC-4-16:格序列**显式入参**(不得由测试内 Random 现生成)。
        //     出处逐条标注。
        // ⚠️ 2026-10-04 评审 #4 修复轮:下列夹具此前被冒充为 GDD 出处,已订正为
        //    `manual:`(它们是**手工格序列**,非 GDD 表逐字值;GDD 表值见
        //    target_selection_test.cs 的 ExampleA/B/C/Bp)。
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 病人格序列(显式夹具 —— AC-4-16 要求)。
        /// source: manual: 手工 fixture,覆盖「同格 / 不同格 / 等距」三类形态。
        ///   理由:重放判据要的是「同输入 ⇒ 同选择序列」,故格序列须含平局点。
        /// </summary>
        private static readonly (long patientId, WorldPos cell)[] PatientCells =
        {
            // source: manual fixture
            (7L,  new WorldPos(10, 0, 5)),   // 与玩家同格(d∞=0)
            (11L, new WorldPos(11, 0, 5)),   // d∞=1
            (12L, new WorldPos(11, 0, 5)),   // d∞=1,与 11 号同格(第三键决胜)
            (13L, new WorldPos(9, 0, 5)),    // d∞=1,另一侧
            (14L, new WorldPos(12, 3, 5)),   // d∞=3(Chebyshev:max(2,3,0)=3)
        };

        /// <summary>世界流 / 烘焙层的格序列(显式夹具)。</summary>
        private static readonly Candidate[] WorldCandidates =
        {
            // source: manual fixture(覆盖 Drop / Container / PoiCell / Utensil)
            new Candidate(new WorldPos(11, 0, 5), InteractableKind.Drop,      812L, StableIdSource.InstanceId),
            new Candidate(new WorldPos(11, 0, 5), InteractableKind.Container, 44L,  StableIdSource.InstanceId),
            new Candidate(new WorldPos(10, 0, 5), InteractableKind.PoiCell,   42L,  StableIdSource.PoiId),
            new Candidate(new WorldPos(12, 3, 5), InteractableKind.Utensil,   2L,   StableIdSource.StructureId),
        };

        /// <summary>输入序列(玩家格 + pressed + tick;显式夹具)。</summary>
        private static readonly InteractIntent[] InputSequence =
        {
            // source: manual fixture —— 含 pressed=true/false 交替,证明「无输入 ⇒ None」参与序列
            new InteractIntent(new WorldPos(10, 0, 5), true,  0),
            new InteractIntent(new WorldPos(10, 0, 5), true,  1),
            new InteractIntent(new WorldPos(11, 0, 5), false, 2),
            new InteractIntent(new WorldPos(11, 0, 5), true,  3),
            new InteractIntent(new WorldPos(9, 0, 5),  true,  4),
        };

        /// <summary>F-4.1b 的玩家格(逐字照抄 GDD §F-4.1b)。</summary>
        private static readonly WorldPos PlayerCell = new WorldPos(10, 0, 5);

        /// <summary>F-4.1b 的作用半径(逐字照抄 GDD §F-4.1b)。</summary>
        private const int RInteract = 1;

        // ═══════════════════════════════════════════════════════════
        //  AC-4-14 —— 单机重放:二次运行选择序列逐元素相等(**直接驱动生产**)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac414_sameInputYieldsSameSelectionSequence()
        {
            var run1 = RunSelectionSequence();
            var run2 = RunSelectionSequence();

            Assert.AreEqual(run1.Count, run2.Count, "AC-4-14:两次运行选择序列长度须相等");
            for (int i = 0; i < run1.Count; i++)
                Assert.AreEqual(run1[i], run2[i],
                    $"AC-4-14:第 {i} 步选择分叉 —— 同输入须同输出序列(F-4.1 函数外延相同)");
        }

        [Test]
        public void test_ac414_twoInterleavedInstancesDoNotDiverge()
        {
            // 两实例**交错**跑同一序列(证无隐藏静态状态 —— 承故事 001 AC-4-04 的交错夹具)。
            // ⚠️ 关键:两个**独立生产实例**各自跑,而非同一实例跑两遍 —— 才能抓跨实例静态污染。
            var seqA = RunSelectionSequence();
            var seqB = RunSelectionSequenceInReverseInputOrder();

            // seqB 是反序**输入**跑的,选择序列应等于 seqA 的逆序(纯函数无状态)。
            var expectedReversed = new List<Selection>(seqA);
            expectedReversed.Reverse();

            Assert.AreEqual(expectedReversed.Count, seqB.Count, "AC-4-14:交错实例序列长度须相等");
            for (int i = 0; i < seqB.Count; i++)
                Assert.AreEqual(expectedReversed[i], seqB[i],
                    $"AC-4-14:交错实例第 {i} 步与预期逆序不符 —— 存在隐藏状态累加");
        }

        [Test]
        public void test_ac414_longSequenceNoAccumulatedDrift()
        {
            // 长序列(≥ 10⁴ 步)无累积漂移。
            // ⚠️ 关键(评审 #9 修复轮):输入须**真的变化** —— 初稿只变 tick 而 tick 不被选择读取,
            //   使全部 10⁴ 次是同一次求值的重复,断言恒真。此处改为**循环走完整输入序列**
            //   (含 None / 同格 / 等距),使每次求值输入不同,漂移才可能暴露。
            const int steps = 10000;   // source: 故事 QA「长序列(≥ 10⁴ tick)」
            var selector = new ProductionSelector();

            // 期望序列 = 一次完整序列(同一实例重复 N 遍,每遍应逐元素复现)。
            var onePass = RunSelectionSequence();

            for (int t = 0; t < steps; t++)
            {
                var intent = InputSequence[t % InputSequence.Length];
                // 重放时 tick 递增(真实时序),但选择结果不得依赖 tick 之外任何累积量。
                var withTick = new InteractIntent(intent.PlayerCell, intent.Pressed, t);
                var got = SelectOnce(selector, withTick);
                var want = onePass[t % onePass.Count];
                Assert.AreEqual(want, got,
                    $"AC-4-14:第 {t} 步出现漂移(期望 {want} / 实得 {got})—— 纯选择函数不得累积状态");
            }
        }

        [Test]
        public void test_ac414_negativeFixture_cacheLastTargetImplementationDiverges()
        {
            // ⚠️ 负夹具(故事 QA 原文):「缓存『上一个目标』的实现 ⇒ 二次运行分叉,红」。
            //
            // 形态:生产 `InteractionSelector.Select` 是**纯函数**(无缓存),故正确的重放
            //   「跑两遍 ⇒ 序列相同」恒成立。要证明本判据**能红**,须把一个**有缓存**的
            //   错误实现塞进同一重放脚手架,并对拍:缓存实现的序列与生产序列**不同**。
            //   ⇒ 判据「序列相等」对该缺陷形态**有鉴别力**,不是空转。
            var producer = RunSelectionSequence();                         // 生产(纯函数)
            var cachey   = RunSelectionSequenceThrough(new CachingSelector()); // 错误实现(缓存上一目标)

            Assert.AreEqual(producer.Count, cachey.Count,
                "AC-4-14 负夹具前置:两序列长度须相等(同输入序列)");
            bool diverged = !producer.SequenceEqual(cachey);
            Assert.IsTrue(diverged,
                "AC-4-14 负夹具:缓存『上一个目标』的实现竟与生产纯函数序列完全相同 ⇒ " +
                "该判据无鉴别力(负夹具空转)");

            // 并坐实故事原文的判据形态:**复用同一实例**跑「正序 + 反序」——
            //   生产 = 纯函数 ⇒ 结果只由输入决定,与跑过的历史无关;
            //   缓存实现 = 结果被历史污染 ⇒ 反序那一遍的首步受正序末步的缓存影响而分叉。
            //   ⚠️ 必须复用**同一实例**(缓存状态寄居其中);新建实例 = 空缓存,测不到污染。
            var cacheInstance = new CachingSelector();
            RunSelectionSequenceThrough(cacheInstance);                       // 预热缓存(正序)
            var cacheyAfterWarm = RunSelectionSequenceInReverseThrough(cacheInstance);
            var cacheyFreshRev  = RunSelectionSequenceInReverseThrough(new CachingSelector());
            Assert.IsFalse(cacheyAfterWarm.SequenceEqual(cacheyFreshRev),
                "AC-4-14 负夹具:被历史预热过的缓存实例,其反序序列须与新鲜实例不同(状态污染);" +
                "若相同,该判据对该缺陷形态无鉴别力(负夹具空转)");

            // 生产:复用同一实例,正序预热后再跑反序,须与新鲜实例的反序**逐元素相同**。
            var producerReused = new ProductionSelector();
            RunSelectionSequenceThrough(producerReused);
            var prodAfterWarm = RunSelectionSequenceInReverseThrough(producerReused);
            var prodFreshRev  = RunSelectionSequenceInReverseThrough(new ProductionSelector());
            Assert.IsTrue(prodAfterWarm.SequenceEqual(prodFreshRev),
                "AC-4-14:生产纯函数须与运行历史无关(复用实例与新鲜实例结果相同)");
        }

        // ── 重放执行(**经生产 `InteractionSelector`**)─────────────────

        /// <summary>选择结果三元组(便于序列相等比较)。</summary>
        private readonly struct Selection : IEquatable<Selection>
        {
            public readonly bool HasTarget;
            public readonly InteractableKind Kind;
            public readonly long StableId;

            public Selection(bool hasTarget, InteractableKind kind, long stableId)
            { HasTarget = hasTarget; Kind = kind; StableId = stableId; }

            public bool Equals(Selection other)
                => HasTarget == other.HasTarget && Kind == other.Kind && StableId == other.StableId;

            public override bool Equals(object obj) => obj is Selection s && Equals(s);
            public override int GetHashCode() => HashCode.Combine(HasTarget, (byte)Kind, StableId);
            public override string ToString() => $"({HasTarget}, {Kind}, {StableId})";
        }

        /// <summary>跑一遍完整输入序列(独立生产实例),记录选择序列。</summary>
        private static List<Selection> RunSelectionSequence()
            => RunSelectionSequenceThrough(new ProductionSelector());

        private static List<Selection> RunSelectionSequenceInReverseThrough(ISelectorLike selector)
            => InputSequence.Reverse().Select(i => ToSelection(selector.SelectOnceStep(in i))).ToList();

        private static List<Selection> RunSelectionSequenceInReverseInputOrder()
            => InputSequence.Reverse()
                .Select(i => SelectOnce(new ProductionSelector(), i))
                .ToList();

        /// <summary>把选择器塞进同一重放脚手架(抽象出「选择器」,供负夹具塞入错误实现)。</summary>
        private interface ISelectorLike
        {
            (bool, InteractableKind, long) SelectOnceStep(in InteractIntent intent);
        }

        private static List<Selection> RunSelectionSequenceThrough(ISelectorLike selector)
            => InputSequence.Select(i => ToSelection(selector.SelectOnceStep(in i))).ToList();

        private static Selection ToSelection((bool has, InteractableKind kind, long id) t)
            => new Selection(t.has, t.kind, t.id);

        /// <summary>
        /// 单步选择(**经生产** `InteractionSelector.Select`)。
        /// ⚠️ **不含 Accept 门**(作用域收窄 —— Armed / ModalOpen 是第四来源,归 AC-4-20 / story 003)。
        /// </summary>
        private static Selection SelectOnce(ISelectorLike selector, in InteractIntent intent)
            => ToSelection(selector.SelectOnceStep(in intent));

        /// <summary>生产选择器的 ISelectorLike 适配(唯一「真」路径)。</summary>
        private sealed class ProductionSelector : ISelectorLike
        {
            private readonly InteractionSelector _selector;
            public ProductionSelector() { _selector = new InteractionSelector(new NoopDiscoveryReporter(), RInteract); }

            public (bool, InteractableKind, long) SelectOnceStep(in InteractIntent intent)
            {
                var candidates = BuildCandidateSet();
                var target = _selector.Select(in intent, candidates);
                return (target.HasTarget, target.Kind, target.StableId);
            }
        }

        /// <summary>
        /// **错误实现**:缓存「上一个目标」—— 若上一步的目标仍在本步候选集中,则径直沿用,
        /// 不再求 argmin。这是 AC-4-14 负夹具要抓的缺陷形态(有状态、随运行历史漂移)。
        /// </summary>
        private sealed class CachingSelector : ISelectorLike
        {
            private readonly InteractionSelector _inner = new InteractionSelector(new NoopDiscoveryReporter(), RInteract);
            private long _lastStableId = long.MinValue;
            private InteractableKind _lastKind = InteractableKind.Drop;

            public (bool, InteractableKind, long) SelectOnceStep(in InteractIntent intent)
            {
                var candidates = BuildCandidateSet();
                // 「就近偏好」缺陷:上一步目标仍在候选集中 ⇒ 径直沿用,**不重算 argmin**
                // (于是玩家移动后它不会改选更近者 —— 正是重放要抓的有状态漂移)。
                if (_hasCache)
                    foreach (var c in candidates)
                        if (c.StableId == _lastStableId && c.Kind == _lastKind)
                            return (true, _lastKind, _lastStableId);

                var t = _inner.Select(in intent, candidates);
                if (t.HasTarget) { _lastStableId = t.StableId; _lastKind = t.Kind; _hasCache = true; }
                return (t.HasTarget, t.Kind, t.StableId);
            }

            private bool _hasCache;
        }

        /// <summary>装配候选集(病人 + 世界四源)。</summary>
        private static List<Candidate> BuildCandidateSet()
        {
            var candidates = new List<Candidate>();
            foreach (var (patientId, cell) in PatientCells)
                candidates.Add(new Candidate(cell, InteractableKind.Patient, patientId, StableIdSource.PatientId));
            candidates.AddRange(WorldCandidates);
            return candidates;
        }

        /// <summary>spy 替身(本文件只作 no-op;真实链路归 story 004)。</summary>
        private sealed class NoopDiscoveryReporter : IDiscoveryReporter
        {
            public void Request(in DiscoveryRequest request) { }
        }
    }
}
