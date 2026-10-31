// story-004 —— F-17-3 节点余量前缀 + 采前闸的夹具(EditMode,真编码 / 真解码,禁 Fake)。
//
// 权威:
//   design/gdd/foraging.md F-17-3 `:341-360` · §边界探针 `:398-410` · §Edge Cases `:412-427`
//   story production/epics/foraging/story-004-node-remaining-prefix.md
//     AC-17-06(采前闸)· AC-17-17(YieldDecay 三性质)· AC-17-13(形状半边)· 前缀一致性
//   ADR-009 §四(派生态)· ADR-005(主机唯一)· ADR-006(唯一舍入 ROUND_HALF_AWAY_FROM_ZERO)
//
// ── 本文件覆盖什么 / 不覆盖什么(如实登记)────────────────────────────────
// 覆盖:余量公式、采前闸谓词、扫描前缀语义(条数 / Σ_all / Σ_window 一次遍历)、
//       YieldDecay 三性质、越界与非法入参的 fail-loud。
// **不覆盖**「余量不足 ⇒ 流里零事件」的完整动作链 —— 那一跳的**调用方**是
// story-005 的动作路径(harvest_orchestrator + 三类失败收敛)。本件只提供**谓词**;
// 在这里自造一个「先判后发」的调用方再断言零事件,断言的是测试自己写的谓词(F1/F4),
// 因此**不做**,留给 story-005 的真装配件。
//
// ── 数值全部取自「离线复算过的字面量」,不引用被测常量 ────────────────────
// `Fix` 是 Q16.16(raw = 值 × 65536)。凡出现在断言里的 raw 值都是先离线算好再写进来的
// 字面量 —— 若断言写成「等于 `Fix.One * x`」,变异常量时两边一起动,零判别力(F3)。

using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Foraging;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Foraging
{
    /// <summary>F-17-3 余量前缀与采前闸。</summary>
    [TestFixture]
    public sealed class NodeRemainingPrefixTest
    {
        private const int SeasonsPerYear = 5;
        private const long RegrowWindow = 1200;
        private const int BaseCapacity = 5;
        private const int MaxQuality = 5;

        private CompositionRootServices _bag;

        [SetUp]
        public void Setup() => _bag = CompositionRoot.Assemble();

        // ══════════ 构造器辅助 ══════════

        private static int[] FlatCapTable()
        {
            var caps = new int[61];
            for (int i = 0; i < caps.Length; i++) caps[i] = MaxQuality;
            return caps;
        }

        /// <summary>季节乘子表:`[1, 1/2, 1, 1, 1]` —— 下标 1 取半,用于造「余量带小数」的边界。</summary>
        private static Fix[] HalfSeasonMult()
            => new[] { Fix.One, Fix.FromRational(1, 2), Fix.One, Fix.One, Fix.One };

        private static Fix[] FlatSeasonMult()
            => new[] { Fix.One, Fix.One, Fix.One, Fix.One, Fix.One };

        private static ForagingConstants Constants(
            Fix[] seasonMult = null, int baseCapacity = BaseCapacity, long regrowWindow = RegrowWindow)
            => new ForagingConstants(
                FlatCapTable(), seasonMult ?? FlatSeasonMult(), SeasonsPerYear, regrowWindow,
                baseCapacity, Fix.FromRational(1, 40), Fix.FromRational(1, 2),
                Fix.FromRational(3, 1), MaxQuality);

        /// <summary>往世界里真写一条 `ResourceHarvested`(真编码 + 真 Append)。</summary>
        private void Harvest(int nodeId, int qty, long tick, int gatherSeq = 0)
        {
            var payload = new ResourceHarvestedPayload(
                instanceId: 0L, nodeId: nodeId, gatherSeq: gatherSeq, qty: qty, outQuality: 1);
            PayloadRef pref = _bag.Encoder.Encode(EventKind.ResourceHarvested, payload);
            _bag.EventSink.Append(new SimEvent(tick, PatientId.None, -1, EventKind.ResourceHarvested, pref));
        }

        // ══════════ AC-17-17 · YieldDecay 三性质 ══════════

        [Test]
        public void test_ac1717_yieldDecay_zeroIsOne_monotoneNonIncreasing_boundedBelow()
        {
            ForagingConstants c = Constants();

            // ① D(0) == MUL_ONE(字面 65536,不引 Fix.One)
            Assert.That(NodeRemaining.YieldDecay(c, 0).Raw, Is.EqualTo(65536L),
                "YieldDecay(0) 须恰为 1.0 —— 未采过不该有惩罚");

            // ② 单调不增:全遍历 n ∈ [0, 200]
            long prev = NodeRemaining.YieldDecay(c, 0).Raw;
            for (int n = 1; n <= 200; n++)
            {
                long cur = NodeRemaining.YieldDecay(c, n).Raw;
                Assert.That(cur, Is.LessThanOrEqualTo(prev),
                    $"YieldDecay 须单调不增 —— n={n} 处 ({cur}) 反而大于 n={n - 1} ({prev})");
                prev = cur;
            }

            // ③ 有下限:大 n 处恰为 DECAY_FLOOR = 1/2 ⇒ raw 32768
            Assert.That(NodeRemaining.YieldDecay(c, 10000).Raw, Is.EqualTo(32768L),
                "YieldDecay 须夹在 DECAY_FLOOR = 1/2 上,不得归零(节点若归零 = 永久枯死)");
        }

        [Test]
        public void test_ac1717_yieldDecay_floorBoundary_isAt21Units()
        {
            // DECAY_RATE = 1/40(raw 1638)· DECAY_FLOOR = 1/2(raw 32768)
            // 线性项 1 − n×1638/65536 在 n = 20 处 = 32776(仍高于下限),n = 21 处跌破 ⇒ 夹住。
            // ⚠️ 与 GDD `:352` 的「累计 40 份触底」字样**不一致** —— 按表内数值算,触底在 **21**。
            //    本条只钉「实现与表值一致」;GDD 措辞与数值的出入归**数值轮**(值归用户,本件不改)。
            ForagingConstants c = Constants();
            Assert.That(NodeRemaining.YieldDecay(c, 20).Raw, Is.EqualTo(32776L),
                "n=20:1 − 20×1638/65536 = 32776/65536,尚未触底");
            Assert.That(NodeRemaining.YieldDecay(c, 21).Raw, Is.EqualTo(32768L),
                "n=21:线性项已低于 1/2 ⇒ 夹在下限上");
            Assert.That(NodeRemaining.YieldDecay(c, 40).Raw, Is.EqualTo(32768L),
                "n=40 仍在下限上(触底后恒等)");
        }

        // ══════════ EffCapacity 的运算次序 ══════════

        [Test]
        public void test_effCapacity_matchesGddFormula_atSelectedPoints()
        {
            // 值全部离线复算(不引用被测常量):
            //   5 × 1/2        = 2.5   ⇒ raw 163840
            //   decay(20)      = 32776 ⇒ 2.5 × 32776/65536 = 81940
            // ⚠️ 本夹具**不区分**「先乘季节再乘衰减」与「先合并两个乘子」——
            //    在 Base=5(整数)且季节乘子为 1/2(精确二进制)时两条次序同值。
            //    GDD 字面次序已写进实现注释;要区分需一个非精确乘子,本轮不作此主张。
            ForagingConstants c = Constants(HalfSeasonMult());
            Assert.That(NodeRemaining.EffCapacity(c, 1, 0).Raw, Is.EqualTo(163840L),
                "季节点:5 × 1/2 = 2.5(raw 163840)");
            Assert.That(NodeRemaining.EffCapacity(c, 1, 20).Raw, Is.EqualTo(81940L),
                "累计 20 份:decay(20) = 32776 ⇒ 2.5 × 32776/65536 = 81940");
        }

        [Test]
        public void test_effCapacity_seasonIndexOutOfRange_throws()
        {
            ForagingConstants c = Constants();
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => NodeRemaining.EffCapacity(c, 5, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => NodeRemaining.EffCapacity(c, -1, 0));
        }

        // ══════════ AC-17-06 · 采前闸(含 GDD 边界探针的「采穿」行)══════════

        [Test]
        public void test_ac1706_fractionalRemaining_rejectsQtyOne_noHarvestThrough()
        {
            // 关键边界(GDD `:404`):remaining ∈ (0,1) 而 qty = 1 ⇒ 必须拒。
            // 造法(离线复算):Base 5 × SEASON[1]=1/2 ⇒ 名义容量 2.5;采过 2 份 ⇒ decay(2)=62260
            //   ⇒ EffCapacity = 2.5 × 62260/65536 = 155650(raw);减窗口 2.0 ⇒ remaining = 24578(≈0.375)。
            ForagingConstants c = Constants(HalfSeasonMult());
            Harvest(nodeId: 1, qty: 2, tick: 10);

            var scan = NodeRemaining.ScanNode(_bag.Stream.Events, _bag.Decoder, 1, tick: 10, RegrowWindow);

            Assert.That(NodeRemaining.Remaining(c, 1, in scan).Raw, Is.EqualTo(24578L),
                "前置:remaining 须落在 (0,1) 内(raw 24578 ≈ 0.375)—— 否则本条测的不是「采穿」边界");
            Assert.That(NodeRemaining.CanHarvest(c, 1, in scan, qty: 1), Is.False,
                "remaining = 0.5 < qty = 1 ⇒ 必须拒。若放行,节点被采穿 —— GDD 边界探针表 :404 的原缺陷");
        }

        [Test]
        public void test_ac1706_exactEquality_isAllowed_andLeavesZero()
        {
            // 取等 = 刚好采完(合法)—— 这是**唯一能区分 `≥` 与 `>`** 的夹具:
            //   Base 8、累计 21 份(已触 DECAY_FLOOR)⇒ EffCapacity = 8 × 1/2 = 4.0(raw 262144)
            //   窗口占用 3 ⇒ remaining = 1.0(raw 65536)⇒ 恰等,须放行
            //   窗口占用 4 ⇒ remaining = 0.0 ⇒ 须拒
            // ⚠️ 用直接构造的 `PrefixScan`(而非真流):窗口/累计语义已由 ScanNode 的用例单独钉住,
            //    这里只钉**闸门算术**。若走真流,Σ_all 会同时被本次采集抬高,边界无法精确命中。
            ForagingConstants c = Constants(baseCapacity: 8);

            var exact = new NodeRemaining.PrefixScan(gatherCount: 21, sumAllQty: 21, sumWindowQty: 3);
            Assert.That(NodeRemaining.Remaining(c, 0, in exact).Raw, Is.EqualTo(65536L),
                "前置:remaining 须恰为 1.0 —— 否则本条测不到取等");
            Assert.That(NodeRemaining.CanHarvest(c, 0, in exact, qty: 1), Is.True,
                "remaining == qty ⇒ 放行。若实现在这里用 `>`,本断言转红");

            var dry = new NodeRemaining.PrefixScan(gatherCount: 21, sumAllQty: 21, sumWindowQty: 4);
            Assert.That(NodeRemaining.Remaining(c, 0, in dry).Raw, Is.EqualTo(0L), "再采完 ⇒ 余量归 0");
            Assert.That(NodeRemaining.CanHarvest(c, 0, in dry, qty: 1), Is.False, "枯节点 ⇒ 拒");
        }

        [Test]
        public void test_ac1706_qtyBelowOne_throws()
        {
            ForagingConstants c = Constants();
            var scan = new NodeRemaining.PrefixScan(0, 0, 0);
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => NodeRemaining.CanHarvest(c, 0, in scan, qty: 0));
        }

        // ══════════ 前缀语义(扫描口径)══════════

        [Test]
        public void test_scanNode_countsAndSums_onlyThisNode()
        {
            Harvest(nodeId: 7, qty: 2, tick: 5);
            Harvest(nodeId: 8, qty: 3, tick: 5);   // 别的节点,不得计入
            Harvest(nodeId: 7, qty: 1, tick: 6);

            var scan = NodeRemaining.ScanNode(_bag.Stream.Events, _bag.Decoder, 7, 6, RegrowWindow);
            Assert.That(scan.GatherCount, Is.EqualTo(2), "只数本节点");
            Assert.That(scan.SumAllQty, Is.EqualTo(3), "2 + 1 = 3");
            Assert.That(scan.SumWindowQty, Is.EqualTo(3), "都在窗口内");
        }

        [Test]
        public void test_scanNode_windowBoundary_isStrictlyLessThan()
        {
            // t − Tick(e) = 1200 恰等于 RegrowWindow ⇒ **已在窗外**(GDD 逐字 `<`)
            Harvest(nodeId: 3, qty: 2, tick: 0);

            var atBoundary = NodeRemaining.ScanNode(_bag.Stream.Events, _bag.Decoder, 3, 1200, RegrowWindow);
            Assert.That(atBoundary.SumWindowQty, Is.EqualTo(0),
                "恰等于窗口的事件须已出窗(取 <= 会让再生慢一拍,与 GDD 不符)");
            Assert.That(atBoundary.SumAllQty, Is.EqualTo(2),
                "但 Σ_all 永不回退 —— 衰减项仍然记得它");

            var justInside = NodeRemaining.ScanNode(_bag.Stream.Events, _bag.Decoder, 3, 1199, RegrowWindow);
            Assert.That(justInside.SumWindowQty, Is.EqualTo(2), "1199 < 1200 ⇒ 仍在窗内");
        }

        [Test]
        public void test_scanNode_excludesFutureEvents_prefixSemantics()
        {
            Harvest(nodeId: 4, qty: 2, tick: 100);

            var scan = NodeRemaining.ScanNode(_bag.Stream.Events, _bag.Decoder, 4, tick: 50, RegrowWindow);
            Assert.That(scan.GatherCount, Is.EqualTo(0),
                "Tick(e) > tick 的事件不在已确立前缀内 —— 否则同一 tick 的两次求值会不同");
            Assert.That(scan.SumAllQty, Is.EqualTo(0));
        }

        [Test]
        public void test_scanNode_samePrefixTwice_isBitIdentical()
        {
            for (int i = 0; i < 20; i++) Harvest(nodeId: 9, qty: 1, tick: i);

            var a = NodeRemaining.ScanNode(_bag.Stream.Events, _bag.Decoder, 9, 19, RegrowWindow);
            var b = NodeRemaining.ScanNode(_bag.Stream.Events, _bag.Decoder, 9, 19, RegrowWindow);
            Assert.That(b.GatherCount, Is.EqualTo(a.GatherCount));
            Assert.That(b.SumAllQty, Is.EqualTo(a.SumAllQty));
            Assert.That(b.SumWindowQty, Is.EqualTo(a.SumWindowQty));
        }

        [Test]
        public void test_scanNode_beyondWindow_stillCountsTowardDecay_notTowardRegrowth()
        {
            // 采满 40 份后彻底出窗:再生项归零(余量回升),但衰减项保留(容量已减半)
            Harvest(nodeId: 5, qty: 40, tick: 0);
            var scan = NodeRemaining.ScanNode(_bag.Stream.Events, _bag.Decoder, 5, 5000, RegrowWindow);

            Assert.That(scan.SumWindowQty, Is.EqualTo(0), "出窗 ⇒ 不再占用窗口额度");
            Assert.That(scan.SumAllQty, Is.EqualTo(40), "但全时段累计仍是 40");

            ForagingConstants c = Constants();
            // 容量 5 × 衰减 1/2 = 2.5(raw 163840);余量 = 2.5 − 0 = 2.5
            Assert.That(NodeRemaining.Remaining(c, 0, in scan).Raw, Is.EqualTo(163840L),
                "窗口恢复只回到「衰减后的容量」,不会回到 5 —— 这正是深水线的形状");
        }

        [Test]
        public void test_scanNode_emptyOrNull_yieldsZeroScan()
        {
            var nullScan = NodeRemaining.ScanNode(null, _bag.Decoder, 1, 0, RegrowWindow);
            Assert.That(nullScan.GatherCount, Is.EqualTo(0));

            var emptyScan = NodeRemaining.ScanNode(_bag.Stream.Events, _bag.Decoder, 99, 0, RegrowWindow);
            Assert.That(emptyScan.SumAllQty, Is.EqualTo(0));
            Assert.That(NodeRemaining.Remaining(Constants(), 0, in emptyScan).Raw, Is.EqualTo(327680L),
                "从未采过的节点 = 满容量 5.0(raw 327680)");
        }

        // ══════════ 非法入参 fail-loud ══════════

        [Test]
        public void test_scanNode_rejectsBadArguments()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => NodeRemaining.ScanNode(_bag.Stream.Events, null, 1, 0, RegrowWindow));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => NodeRemaining.ScanNode(_bag.Stream.Events, _bag.Decoder, -1, 0, RegrowWindow));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => NodeRemaining.ScanNode(_bag.Stream.Events, _bag.Decoder, 1, 0, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => NodeRemaining.ScanNode(_bag.Stream.Events, _bag.Decoder, 1, 0, -5));
        }

        [Test]
        public void test_prefixScan_rejectsNegativeAggregates()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new NodeRemaining.PrefixScan(-1, 0, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new NodeRemaining.PrefixScan(0, -1, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new NodeRemaining.PrefixScan(0, 0, -1));
        }
    }
}
