// 测试共用夹具:构造一个「真」掉落写者(2026-10-27 · R17 闭合批)
//
// 为什么需要它:`StructureWriter` 的 `dropWriter` 形参是**必填 + 构造期 fail-closed**
// (N-r1 同口径)。若各测试就地 new 一个,`IEventSink` / `IPayloadEncoder` / `IIdAuthority`
// 三件套会在 18 处重复抄写 —— 抄错一处就是「返还事件跑到另一个 sink 里」的静默失败。
//
// ⚠️ **不是 Fake**:这里给的是**真** `DropEventWriter`,只是依赖用测试替身
//   (内存池 / 递增计数器 / 调用方给的 sink)。真实现在 `Sim/World/DropEventWriter.cs`,
//   测试**不替换被测行为**,只替换它够不着的宿主。
//
// ⚠️ **`_ids` 每个用例独立**:`IIdAuthority` 的计数器是全局状态,跨用例复用会让
//   「本次铸造了哪些号」不可断言(`test_*_mintsExactlyOneId` 一类夹具就没法写)。

using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Tests
{
    /// <summary>构造真 <see cref="DropEventWriter"/> 的最小测试夹具。</summary>
    public static class TestDropWriter
    {
        /// <summary>
        /// `DEMOLISH_REFUND_RATIO` 的 P0 值(Q16.16 raw = **1.0**,全额返还)。
        /// <para>⚠️ **刻意是测试侧字面量,不是引用生产常量**:生产侧今日**没有**运行期载体
        /// (`BuildModulesBinder.DemolishRefundRatioRaw` 住编辑期程序集,测试引用它 =
        /// 让被测行为与判据同源)。夹具写成字面量 65536 才能在「生产把 R 改成别的值」时转红。</para>
        /// <para>规则九:`P0 R = 1`;P1a 才 `R &lt; 1`。</para>
        /// </summary>
        public const int P0RefundRatio = 65536;

        /// <summary>造一个写者 + 它独占的内存池,把掉落事件记进 <paramref name="sink"/>。</summary>
        /// <param name="sink">掉落事件的去处(通常与 `StructureWriter` 的 sink **同一个**,
        /// 这样一条流里能同时看到 `StructureRemoved` 与 `DropSpawned`)。</param>
        /// <param name="pool">载荷池(调用方要回读载荷时需持有它,故作为 out 交出)。</param>
        /// <param name="ids">发号器(调用方要断「铸造了几个号」时需持有它)。</param>
        public static DropEventWriter Create(IEventSink sink, out InMemoryBlobPool pool,
                                             out RecordingIdAuthority ids)
        {
            pool = new InMemoryBlobPool();
            ids = new RecordingIdAuthority();
            return new DropEventWriter(sink, new PayloadEncoder(pool), ids);
        }

        /// <summary>同上,但不关心池与发号器(大多数夹具只需要「事件真的发出去了」)。</summary>
        public static DropEventWriter Create(IEventSink sink) => Create(sink, out _, out _);

        /// <summary>
        /// 用**调用方给的池**造写者 —— 调用方要回读载荷(如本文件的 `PayloadOf`)时**必须**用这个重载。
        /// </summary>
        /// <remarks>
        /// ⚠️ **这一条是 2026-10-27 实测踩出来的**:首版 `Create(sink)` 内部自建池并丢弃,
        /// 而夹具用自己的 `_pool` 回读 ⇒ `PayloadCodec.TryGetPayload` 在**错误的池**里
        /// 找那个 offset,取回的是**零值载荷**(`Qty = 0`),断言「refund 应为 1」转红。
        /// 症状极具误导性:事件存在、种类对、只有字段值全 0 —— 看起来像「写者没传值」,
        /// 实际是**两个池**。⇒ 凡要读载荷的夹具,池必须与写者同源。
        /// </remarks>
        public static DropEventWriter Create(IEventSink sink, InMemoryBlobPool pool)
            => new DropEventWriter(sink, new PayloadEncoder(pool), new RecordingIdAuthority());

        /// <summary>记录型发号器 —— 物品实例号从 <paramref name="start"/> 起逐个递增。</summary>
        /// <remarks>
        /// ⚠️ 病人号恒 <c>1</c>:本夹具服务的测试都不铸病人号(建造/拆除不产生病人)。
        /// 真被调用说明有人越界 —— 不会静默,`NextPatientId` 的返回值会立刻让
        /// `Patient = PatientId.None` 的断言失效。
        /// </remarks>
        public sealed class RecordingIdAuthority : IIdAuthority
        {
            private int _nextItem;

            /// <summary>构造。<paramref name="start"/> = 首个将被发放的物品实例号。</summary>
            public RecordingIdAuthority(int start = 1) => _nextItem = start;

            /// <summary>已发放的物品实例号个数(断「铸造点恰一处」用)。</summary>
            public int ItemIdsIssued { get; private set; }

            /// <inheritdoc />
            public PatientId NextPatientId() => new PatientId(1);

            /// <inheritdoc />
            public ItemInstanceId NextItemInstanceId()
            {
                ItemIdsIssued++;
                return new ItemInstanceId(_nextItem++);
            }
        }
    }
}
