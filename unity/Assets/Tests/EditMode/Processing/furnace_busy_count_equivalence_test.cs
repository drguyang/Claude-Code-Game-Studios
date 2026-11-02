// 2026-11-02 · `TD-processing-04` 收口 —— `BusyActorCount` 单遍扫描的**等价性**判据。
//
// 债:`BusyActorCount` 旧实现 = 「扫一遍收 actor 集合 + 逐 actor 重扫全流」⇒
//   `O(A×E)` 且每条 `Craft` 被解码 `A` 次。修法 = 单遍扫描,但**硬要求**是
//   「`IsBusy` 仍是唯一判据」—— 单遍只**产出**中间量,判据仍走同一个核心谓词。
//
// 本件钉的就是那条硬要求:**单遍结果 ≡ 逐 actor 问 `IsBusy` 的结果**。
//   ⇒ 若将来有人把判据抄第二份(两处各写一份 `tick < completeTick`),
//     本件会在边界输入上分叉并转红。
//
// 判据(逐条可证伪):
//   ① **等价性**(主判据):对一批**含边界**的流 × 一批 tick,`BusyActorCount` 必须逐点等于
//      「所有 actor 里 `IsBusy` 为真的个数」。边界 = `tick == CompleteTick`(完成当拍不忙)。
//   ② **时长域先验同位置**:`duration_ticks = 0` 的 `Craft` **即使落在前缀之外**也须抛
//      (与 `TryGetLatest` 同语义;若单遍扫描把它挪到「前缀内才验」,两条路径就分叉了)。
//   ③ **非法流定序同源**:同一 actor 两条同 tick `Craft`(不同 `Seq`)⇒ 取**全序键更大**的那条,
//      与 `TryGetLatest` 一致(不靠列表顺序)。
//   ④ **全流混入非 Craft**:计数只认 `Craft`(域外 Kind 不得被解码)。
//
// ⚠️ 本件**不测性能** —— 债册原文自陈「分配量为推算,未用 Profiler 实测」。
//   本件钉的是**等价性**(修法的正确性前提),不是加速幅度。
//   「单遍确实少解码」由证伪轮的**结构**保证(删掉单遍 ⇒ 等价性仍绿但 ①②③④ 都还是绿,
//   故须另有一条变异打「判据被抄第二份」)。见证据件 §证伪轮。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Processing;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Processing
{
    /// <summary>`BusyActorCount` 与 `IsBusy` 的等价性(`TD-processing-04`)。</summary>
    [TestFixture]
    public sealed class FurnaceBusyCountEquivalenceTest
    {
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;

        [SetUp]
        public void Setup()
        {
            var pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(pool);
            _decoder = new PayloadDecoder(pool);
        }

        // ══════════════════════════════════════════════════════════════════
        // ① 等价性(主判据)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 一批含边界的流 × 一批 tick ⇒ `BusyActorCount` 逐点等于「逐 actor 问 `IsBusy`」。
        /// <para><b>判别力</b>:把单遍扫描里的 `IsBusyCore` 换成自己写的一份
        /// `tick &lt; completeTick`(即「判据被抄第二份」)⇒ 只要两处有任一字符差异
        /// (例如 `<` vs `&lt;=`、`Exists` 守卫遗漏)本条即红。</para>
        /// </summary>
        [Test]
        public void test_tdp04_busyCount_equalsPerActorIsBusy_atEveryTick()
        {
            List<SimEvent> stream = BuildBoundaryStream();
            int[] actors = { 1, 2, 3, 4 };   // 4 = 只在前缀外出现(见 BuildBoundaryStream)

            // 覆盖:点火前 / 点火当拍 / 烧中 / **完成当拍** / 完成后。
            long[] ticks = { 0, 99, 100, 120, 140, 139, 150, 190, 200, 300 };

            foreach (long tick in ticks)
            {
                int expected = 0;
                foreach (int actor in actors)
                    if (FurnaceState.IsBusy(stream, _decoder, actor, tick)) expected++;

                int actual = FurnaceState.BusyActorCount(stream, _decoder, tick);

                Assert.AreEqual(expected, actual,
                    $"tick {tick}: 单遍计数({actual})≠ 逐 actor 问 IsBusy({expected})—— " +
                    "两条路径分叉(判据被抄了第二份)");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 时长域先验同位置(前缀外也须抛)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// `duration_ticks = 0` 的 `Craft` 即使落在**前缀之外**(`e.Tick &gt; tick`)也须抛。
        /// <para><b>判别力</b>:把时长校验挪到「`e.Tick &gt; tick` 之后」⇒ 本条红。
        /// 两条路径(单遍 / `TryGetLatest`)必须**同位置同语义**,否则坏数据在一条路径上被吞。</para>
        /// </summary>
        [Test]
        public void test_tdp04_zeroDuration_beyondPrefix_stillFailsLoud()
        {
            var stream = new List<SimEvent>
            {
                Craft(actorId: 1, startTick: 100, durationTicks: 50, envelopeTick: 100, seq: 0),
                // 前缀外:信封 tick = 200,但时长坏
                Craft(actorId: 2, startTick: 190, durationTicks: 0, envelopeTick: 200, seq: 1),
            };

            var ex = Assert.Throws<InvalidOperationException>(
                () => FurnaceState.BusyActorCount(stream, _decoder, tick: 100),
                "前缀外的坏时长也须抛 —— 坏数据早报,不因询问 tick 靠前而漏");
            StringAssert.Contains("duration_ticks", ex.Message,
                "报文须点名 duration_ticks(否则排障看不出坏的是哪个字段)");

            // 同一条流,**逐 actor 路径**也必须抛同一类 —— 两条路径同语义。
            Assert.Throws<InvalidOperationException>(
                () => FurnaceState.IsBusy(stream, _decoder, 2, tick: 100),
                "IsBusy 对同一条流须抛同类型异常(两路径语义必须一致)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 非法流定序同源(不靠列表顺序)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 同一 actor 两条同 tick `Craft`(不同 `Seq`)⇒ 单遍扫描须取**全序键更大**的那条,
        /// 与 `TryGetLatest` 一致。
        /// <para><b>判别力</b>:把单遍里的「取全序键最大」改成「取最后遇到者」⇒ 本条红
        /// (把两条按 Seq 倒序排列,「最后遇到」会取到 Seq 较小的那条)。</para>
        /// <para>⚠️ 这是**非法流**(合法流里主机唯一发号 ⇒ 同键不可能)——
        /// 但它钉的是「两条路径在同一份输入上给同一答案」,而非法流恰是分叉最容易暴露的地方。</para>
        /// </summary>
        [Test]
        public void test_tdp04_sameTickTwoCrafts_bothPathsPickLargestOrderKey()
        {
            // 列表顺序刻意与 Seq 顺序**相反**:Seq 大的在前,Seq 小的在后。
            // 「取最后遇到者」会取到 Seq=0 那条(时长 10 ⇒ 到 110 就完),
            // 「取全序键最大」会取到 Seq=1 那条(时长 90 ⇒ 到 190 才完)。
            var stream = new List<SimEvent>
            {
                Craft(actorId: 1, startTick: 100, durationTicks: 90, envelopeTick: 100, seq: 1),
                Craft(actorId: 1, startTick: 100, durationTicks: 10, envelopeTick: 100, seq: 0),
            };

            // 前置:两条路径都得先给出「最后一炉」—— 用 IsBusy 探出它取了哪条。
            bool isBusyAt150 = FurnaceState.IsBusy(stream, _decoder, 1, tick: 150);
            Assert.IsTrue(isBusyAt150,
                "前置:IsBusy 取的是**全序键更大**那条(时长 90 ⇒ 190 才完);" +
                "若此处 false,说明 IsBusy 自身已退化为「取最后遇到者」");

            // 判据:单遍计数必须与 IsBusy 同答案。
            Assert.AreEqual(1, FurnaceState.BusyActorCount(stream, _decoder, tick: 150),
                "tick 150:单遍扫描须与 IsBusy 取同一条(全序键最大),不得取列表里最后那条");
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 全流混入非 Craft
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 流里混有非 `Craft` 事件 ⇒ 计数只认 `Craft`(域外 Kind 不得被解码)。
        /// <para><b>判别力</b>:把单遍里的 `if (e.Kind != EventKind.Craft) continue;` 删掉 ⇒
        /// 解码 `DropSpawned` 时抛(codec 会拒绝错误的载荷类型)⇒ 本条红。</para>
        /// </summary>
        [Test]
        public void test_tdp04_nonCraftEvents_areIgnoredByBusyCount()
        {
            var stream = new List<SimEvent>
            {
                new SimEvent(50L, PatientId.None, -1, EventKind.DropSpawned,
                    _encoder.Encode(EventKind.DropSpawned,
                        new DropSpawnedPayload(900L, new WorldPos(1, 1, 1), 0, 1))),
                Craft(actorId: 1, startTick: 100, durationTicks: 50, envelopeTick: 100, seq: 0),
            };

            Assert.AreEqual(1, FurnaceState.BusyActorCount(stream, _decoder, tick: 120),
                "tick 120:只有 actor 1 在烧 ⇒ 计数 1(非 Craft 事件不得被当成炉)");
            Assert.AreEqual(0, FurnaceState.BusyActorCount(stream, _decoder, tick: 200),
                "tick 200:炉已完 ⇒ 计数 0");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑤ 同键口径(防御分支,合法流上不可达 —— TD-processing-03 甲案)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// **同键两条 `Craft`** ⇒ 取**先入者**(`TryGetLatest` 与 `BusyActorCount` 同口径)。
        /// <para>⚠️ <b>本条钉的是防御分支,不是合法输入</b> —— 同键在合法流上**结构上不可达**:
        /// ① `Seq` 发号器每 `(Tick, Patient)` 组复位并递增(`EventStream.cs:262-276`);
        /// ② `EventStream` 按四元组 `(Kind, Patient, Tick, Seq)` 去重(`:242-244`)。
        /// ⇒ 两条同键 `Craft` 只可能来自**流损坏**(手搓 / 档损坏)。本条直接构造该形态
        /// (不经 `EventStream`),钉住消费方的口径,使「取先入者」**有据可查**而非隐式默认。</para>
        /// <para><b>判别力</b>:把 `<= 0` 改成 `< 0`(同键改取后入者)⇒ 本条的
        /// `IsBusy` 断言在「先入者已完、后入者仍在烧」的 tick 上翻转 ⇒ 红。</para>
        /// </summary>
        [Test]
        public void test_tdp03_sameOrderKey_twoCrafts_pickFirstEncountered()
        {
            // 两条同键:同 envelope tick / 同 Patient(None)/ 同 Seq ⇒ 全序键逐位相同。
            // 载荷刻意让两者在不同 tick 上「忙」:先入者 [100,140)、后入者 [100,220)。
            SimEvent first = Craft(actorId: 1, startTick: 100, durationTicks: 40,
                                   envelopeTick: 100, seq: 0);
            SimEvent second = Craft(actorId: 1, startTick: 100, durationTicks: 120,
                                    envelopeTick: 100, seq: 0);

            // 前置:两条的键确实相同(否则本条测的不是同键分支)。
            Assert.That(EventOrder.KeyOf(in first).CompareTo(EventOrder.KeyOf(in second)),
                Is.EqualTo(0), "前置:两条 Craft 的全序键须逐位相同 —— 否则本测打不到同键分支");

            var stream = new List<SimEvent> { first, second };

            // 取先入者 ⇒ 完成于 140;故 140 不忙、130 忙。
            Assert.That(FurnaceState.IsBusy(stream, _decoder, actorId: 1, tick: 130L), Is.True,
                "tick 130:先入者 [100,140) 仍在烧 ⇒ 忙");
            Assert.That(FurnaceState.IsBusy(stream, _decoder, actorId: 1, tick: 140L), Is.False,
                "tick 140:先入者已完成(完成当拍不忙)⇒ 不忙 —— " +
                "若同键取后入者([100,220))则此处仍忙 ⇒ 本条红");

            // 单遍扫描须与 IsBusy 同口径(否则两条路径在同键上分叉)。
            Assert.That(FurnaceState.BusyActorCount(stream, _decoder, tick: 140L), Is.EqualTo(0),
                "单遍扫描须与 IsBusy 同口径:同键取先入者 ⇒ tick 140 计数 0");
        }

        // ══════════════════════════════════════════════════════════════════
        // 辅助:造边界流
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 四条 actor,覆盖「点火前 / 烧中 / 完成当拍 / 完成后」全部边界:
        /// · actor 1:`[100, 140)`
        /// · actor 2:`[100, 190)`
        /// · actor 3:两炉 —— `[100, 110)` 与 `[150, 170)`(测「最后一炉」语义)
        /// · actor 4:**只在前缀外出现**(信封 300)⇒ 任何 ≤ 200 的 tick 都看不见它
        ///   (钉住「actor 集合也不得从未来事件里收」)
        /// </summary>
        private List<SimEvent> BuildBoundaryStream()
        {
            return new List<SimEvent>
            {
                Craft(actorId: 1, startTick: 100, durationTicks: 40, envelopeTick: 100, seq: 0),
                Craft(actorId: 2, startTick: 100, durationTicks: 90, envelopeTick: 100, seq: 0),
                Craft(actorId: 3, startTick: 100, durationTicks: 10, envelopeTick: 100, seq: 0),
                Craft(actorId: 3, startTick: 150, durationTicks: 20, envelopeTick: 150, seq: 0),
                Craft(actorId: 4, startTick: 290, durationTicks: 20, envelopeTick: 300, seq: 0),
            };
        }

        private SimEvent Craft(int actorId, long startTick, long durationTicks,
                               long envelopeTick, long seq)
        {
            var payload = new CraftPayload(
                actorId, recipeId: 1, startTick, durationTicks,
                inputInstanceIds: new long[] { 101 },
                actualConsumed: new int[] { 1 },
                outputQty: new int[] { 1 },
                outputQuality: new int[] { 3 },
                outputInstanceIds: new long[] { 201 },
                toolCell: new WorldPos(4, 0, 7));
            return new SimEvent(envelopeTick, PatientId.None, seq, EventKind.Craft,
                                _encoder.Encode(EventKind.Craft, payload));
        }
    }
}
