// 测试共用夹具:构造一个「真」掉落写者(2026-10-27 · R17 闭合批)
//
// PlayMode 侧的副本 —— 本树是独立程序集(`PlayMode.asmdef`),够不着 EditMode 的同名件。
// 两份**刻意重复**而不是抽公共装配:抽公共件要新立一个被两树同时引用的测试 asmdef,
// 而那会把「测试夹具」升格成生产装配的一部分(与 ADR-025 §① 的装配清单口径冲突)。
//
// ⚠️ **不是 Fake**:给的是**真** `DropEventWriter`,只把它的依赖(内存池 / 计数器 / sink)
//   换成测试替身。

using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>构造真 <see cref="DropEventWriter"/> 的最小测试夹具。</summary>
    public static class TestDropWriter
    {
        /// <summary>`DEMOLISH_REFUND_RATIO` 的 P0 值(Q16.16 raw = 1.0,全额返还)。</summary>
        /// <remarks>刻意是测试侧字面量,不引用生产常量 —— 详见 EditMode 侧同名件的理由。</remarks>
        public const int P0RefundRatio = 65536;

        /// <summary>造一个写者 + 它独占的内存池,把掉落事件记进 <paramref name="sink"/>。</summary>
        public static DropEventWriter Create(IEventSink sink, out InMemoryBlobPool pool,
                                             out RecordingIdAuthority ids)
        {
            pool = new InMemoryBlobPool();
            ids = new RecordingIdAuthority();
            return new DropEventWriter(sink, new PayloadEncoder(pool), ids);
        }

        /// <summary>同上,但不关心池与发号器。</summary>
        public static DropEventWriter Create(IEventSink sink) => Create(sink, out _, out _);

        /// <summary>记录型发号器 —— 物品实例号从 <paramref name="start"/> 起逐个递增。</summary>
        public sealed class RecordingIdAuthority : IIdAuthority
        {
            private int _nextItem;

            /// <summary>构造。<paramref name="start"/> = 首个将被发放的物品实例号。</summary>
            public RecordingIdAuthority(int start = 1) => _nextItem = start;

            /// <summary>已发放的物品实例号个数。</summary>
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
