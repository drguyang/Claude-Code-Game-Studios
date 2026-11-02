// 2026-11-02 · `TD-net-01` 收口 —— `IReplayPipe` / `ReorderBuffer` 的 P0 预埋落点判据。
//
// 债:`ADR-001` 把 pipe 抽象的**落地级别定为 P0**(`:10` / `:59` / `:131` / `:167` / `:448`),
//   而 `IReplayPipe` 全树**零命中**、`ReorderBuffer` 生产**零命中** ⇒
//   「八个抽象点落 7 个,`IReplayPipe` 是唯一零落点」。
//
// 判据(逐条可证伪):
//   ① **抽象点存在**:`IReplayPipe` 在 `Sim.Contracts`(被 `Sim` 看得见)·
//      `Publish` / `Subscribe` 两个方法(ADR-001 `:137-141` 逐字)。
//   ② **重排缓冲 = 全序键吸收**:乱序注入 ⇒ `Drain` 输出**按全序键升序**、
//      **无丢**(入队数 = 输出数)、**无重放**(每条至多输出一次)。
//   ③ **有界 + 超界丢弃 + 告警**:`MaxLag` 满后 `TryEnqueue` 返回 `false`、
//      `Dropped` 递增、告警串点名「超界丢弃」与「重连拉流」。
//   ④ **`MaxLag = 0` 构造期即拒**:0 会让缓冲退化为「按到达序直通」= 放弃全序吸收。
//   ⑤ **本地占位真的能发**:`LocalReplayPipe.Publish` ⇒ 订阅者收到**同一条**事件;
//      `Subscribe(null)` ⇒ 抛(不静默接受)。
//   ⑥ **多订阅 = 追加**:两个订阅者都收到(不是「后者替换前者」)。
//
// ⚠️ **本件不测网络** —— 本地占位零网络(见 `LocalReplayPipe` 类注的语义边界)。
//   它证明的是「契约形状可被实现」,不是「网络层能工作」。
//
// ⚠️ **本件不测「乱序在本地路径上出现」** —— 本地 `Publish` 按调用序入队,
//   路径上**永远不乱序**。乱序面只在 `ReorderBuffer` 的直接夹具里造(判据 ② 就是这样做的)。
//
// 零随机 / 零时间依赖 / 零写盘。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Networking
{
    /// <summary>ADR-001 §一 的 pipe 契约(`TD-net-01`)。</summary>
    [TestFixture]
    public sealed class ReplayPipeContractTest
    {
        private PayloadEncoder _encoder;

        [SetUp]
        public void Setup()
        {
            _encoder = new PayloadEncoder(new InMemoryBlobPool());
        }

        // ══════════════════════════════════════════════════════════════════
        // ① 抽象点存在
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// `IReplayPipe` 在 `Sim.Contracts`,且恰有 ADR-001 `:137-141` 那两个方法。
        /// <para><b>判别力</b>:删掉 `Publish` 或 `Subscribe` ⇒ 本条红;把接口搬到别的
        /// 程序集(如 `Sim`)⇒ 本条红(那会破坏「`Sim` 看得见」的装配面)。</para>
        /// </summary>
        [Test]
        public void test_tdnet01_interface_livesInContracts_withTwoMethods()
        {
            Type pipe = typeof(IReplayPipe);

            Assert.That(pipe.Namespace, Is.EqualTo("DaYiJingCheng.Sim.Contracts"),
                "`IReplayPipe` 须住 `Sim.Contracts` —— 只有那里 `Sim` 才看得见" +
                "(ADR-025 §① 引用集恰 = {BCL, Sim.Contracts}),且不开新程序集边");

            var methods = new List<string>();
            foreach (var m in pipe.GetMethods())
                methods.Add(m.Name);
            CollectionAssert.AreEquivalent(new[] { "Publish", "Subscribe" }, methods,
                "ADR-001 `:137-141` 逐字定义两个方法 —— 多一个/少一个都是契约漂移");
        }

        /// <summary>`ReorderBuffer` 在 `Sim.Contracts`(与接口同程序集 —— 它是纯逻辑,零依赖)。</summary>
        [Test]
        public void test_tdnet01_reorderBuffer_livesInContracts()
        {
            Assert.That(typeof(ReorderBuffer).Namespace, Is.EqualTo("DaYiJingCheng.Sim.Contracts"),
                "`ReorderBuffer` 是纯 C# 逻辑(ADR-001 `:167`)⇒ 与接口同程序集,零引擎引用");
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 全序键吸收:乱序注入 ⇒ 升序输出、无丢、无重放
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 乱序注入 ⇒ `Drain` 按全序键升序输出,入队数 = 输出数(无丢),
        /// 每条至多输出一次(无重放)。
        /// <para><b>判别力</b>:把 `TryEnqueue` 里的插入排序改成 `Add`(直接追加)⇒ 本条红。</para>
        /// <para>⚠️ 乱序**必须在这里造** —— 本地 `LocalReplayPipe.Publish` 按调用序入队,
        /// 路径上永远不乱序(见类注)。</para>
        /// </summary>
        [Test]
        public void test_tdnet01_reorderBuffer_outOfOrderInput_drainsInTotalOrder()
        {
            var buffer = new ReorderBuffer(maxLag: 8);
            var warnings = new List<string>();
            var b2 = new ReorderBuffer(maxLag: 8, warnings.Add);

            // 刻意乱序:Seq 5, 2, 9, 1, 7
            long[] seqs = { 5, 2, 9, 1, 7 };
            foreach (long seq in seqs)
            {
                SimEvent e = MakeEvent(tick: 100, seq: seq);
                Assert.That(b2.TryEnqueue(in e, KeyOf(e)), Is.True,
                    $"前置:缓冲未满(8 ≥ 5)⇒ 第 seq={seq} 条须入队");
            }

            IReadOnlyList<SimEvent> drained = b2.Drain();

            Assert.That(drained.Count, Is.EqualTo(seqs.Length),
                "无丢:入队 5 条 ⇒ 输出 5 条");
            Assert.That(warnings, Is.Empty, "未超界 ⇒ 零告警");

            for (int i = 1; i < drained.Count; i++)
            {
                Assert.That(EventOrder.Compare(drained[i - 1], drained[i]), Is.LessThan(0),
                    $"输出须按全序键升序 —— 第 {i - 1} 条(seq={drained[i - 1].Seq})" +
                    $"不得 ≥ 第 {i} 条(seq={drained[i].Seq})");
            }

            // 无重放:每条 seq 恰出现一次
            var seen = new HashSet<long>();
            foreach (SimEvent e in drained)
                Assert.That(seen.Add(e.Seq), Is.True, $"seq={e.Seq} 被重复输出(重放)");
            Assert.That(seen.Count, Is.EqualTo(seqs.Length), "输出集合须恰等于输入集合");

            // Drain 后缓冲清空
            Assert.That(b2.Count, Is.Zero, "Drain 后缓冲须清空");
            Assert.That(b2.Drain(), Is.Empty, "再次 Drain ⇒ 空(不重放)");

            // 第一条 buffer 未用(仅用于前置对照)
            Assert.That(buffer.Count, Is.Zero);
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 有界 + 超界丢弃 + 告警
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 缓冲满后 `TryEnqueue` 返回 `false`、`Dropped` 递增、告警点名「超界丢弃」与
        /// 「重连拉流」。
        /// <para><b>判别力</b>:把超界分支从「丢弃 + 告警」改成「抛异常」⇒ 本条红
        /// (ADR-001 要求丢弃而非崩溃:客户端重连拉流兜底)。</para>
        /// </summary>
        [Test]
        public void test_tdnet01_reorderBuffer_overflow_dropsAndWarns()
        {
            var warnings = new List<string>();
            var buffer = new ReorderBuffer(maxLag: 2, warnings.Add);

            for (long seq = 1; seq <= 2; seq++)
            {
                SimEvent e = MakeEvent(tick: 100, seq: seq);
                Assert.That(buffer.TryEnqueue(in e, KeyOf(e)), Is.True,
                    $"前置:第 {seq} 条须入队(缓冲容量 2)");
            }
            Assert.That(buffer.Count, Is.EqualTo(2), "前置:缓冲已满");

            SimEvent third = MakeEvent(tick: 100, seq: 3);
            Assert.That(buffer.TryEnqueue(in third, KeyOf(third)), Is.False,
                "缓冲满 ⇒ 第 3 条须被丢弃(返回 false),**不得**抛");

            Assert.That(buffer.Dropped, Is.EqualTo(1), "丢弃计数须递增");
            Assert.That(buffer.Enqueued, Is.EqualTo(3), "入队计数含被丢弃那条");

            Assert.That(warnings.Count, Is.EqualTo(1), "超界 ⇒ 恰一条告警");
            StringAssert.Contains("超界丢弃", warnings[0], "告警须点名「超界丢弃」");
            StringAssert.Contains("重连", warnings[0],
                "告警须指出兜底路径(重连 = 拉流重放)—— 否则运维看不出怎么恢复");
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ MaxLag = 0 构造期即拒
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// `MaxLag = 0` ⇒ 构造期抛。
        /// <para><b>判别力</b>:把 `maxLag &lt; 1` 那条守卫删掉 ⇒ 本条红。
        /// 0 会让缓冲退化为「按到达序直通」= 放弃 ADR-001「全序键吸收」的全部意义。</para>
        /// </summary>
        [Test]
        public void test_tdnet01_reorderBuffer_zeroMaxLag_throwsAtConstruction()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => new ReorderBuffer(maxLag: 0),
                "MaxLag = 0 须构造期拒 —— 它让「全序键吸收」退化为「按到达序直通」");
            StringAssert.Contains("MaxLag", ex.Message, "报文须点名 MaxLag");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑤ 本地占位真的能发
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// `LocalReplayPipe.Publish` ⇒ 订阅者收到**同一条**事件(同一 `Tick` / `Seq` / `Kind`)。
        /// <para><b>判别力</b>:把 `Publish` 的回调段删掉 ⇒ 本条红。</para>
        /// </summary>
        [Test]
        public void test_tdnet01_localPipe_publish_reachesSubscriber()
        {
            var pipe = new LocalReplayPipe(maxLag: 8);
            var got = new List<SimEvent>();
            pipe.Subscribe(e => got.Add(e));

            SimEvent sent = MakeEvent(tick: 100, seq: 1);
            pipe.Publish(in sent);

            Assert.That(got.Count, Is.EqualTo(1), "订阅者须恰收到 1 条");
            Assert.That(got[0].Tick, Is.EqualTo(sent.Tick));
            Assert.That(got[0].Seq, Is.EqualTo(sent.Seq));
            Assert.That(got[0].Kind, Is.EqualTo(sent.Kind));
        }

        /// <summary>`Subscribe(null)` ⇒ 抛(不静默接受)。</summary>
        [Test]
        public void test_tdnet01_localPipe_subscribeNull_throws()
        {
            var pipe = new LocalReplayPipe();
            Assert.Throws<ArgumentNullException>(() => pipe.Subscribe(null),
                "订阅 null 回调须抛 —— 静默接受会让「订阅了」与「没订阅」在外部不可分");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑥ 多订阅 = 追加
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 两个订阅者 ⇒ **都**收到(不是「后者替换前者」)。
        /// <para><b>判别力</b>:把 `_subscribers.Add` 改成 `_subscribers.Clear(); Add` ⇒ 本条红。</para>
        /// <para>⚠️ ADR-001 原文**未定**多订阅语义(见接口注);本实现取「追加」,
        /// 本条钉的是「**本实现**的行为与其文档一致」,不宣称它是 ADR 的裁决。</para>
        /// </summary>
        [Test]
        public void test_tdnet01_localPipe_secondSubscribe_appends()
        {
            var pipe = new LocalReplayPipe(maxLag: 8);
            int first = 0, second = 0;
            pipe.Subscribe(_ => first++);
            pipe.Subscribe(_ => second++);

            Assert.That(pipe.SubscriberCount, Is.EqualTo(2), "前置:两个订阅者");

            SimEvent sent = MakeEvent(tick: 100, seq: 1);
            pipe.Publish(in sent);

            Assert.That(first, Is.EqualTo(1), "第一个订阅者须收到");
            Assert.That(second, Is.EqualTo(1), "第二个订阅者也须收到(追加,不是替换)");
        }

        // ══════════════════════════════════════════════════════════════════
        // 辅助
        // ══════════════════════════════════════════════════════════════════

        /// <summary>造一条 `DiseaseOnset`(任何在白名单里的 Kind 都可 —— 本件只关心键)。</summary>
        private SimEvent MakeEvent(long tick, long seq)
            => new SimEvent(tick, new PatientId(1), seq, EventKind.DiseaseOnset,
                _encoder.Encode(EventKind.DiseaseOnset,
                    new DiseaseOnsetPayload(tick, 3, 1, 7L, seq)));

        private static EventOrderKey KeyOf(in SimEvent e) => EventOrder.KeyOf(in e);
    }
}
