// ADR-031 §①/§③ —— IPayloadDecoder 的实现(住 Sim.Codec)。
//
// 权威来源:
//   ADR-031 §① —— 第八抽象点 IPayloadDecoder 的兑现件
//   ADR-031 §② —— 抛形:未驻留 / 越界 ⇒ InvalidOperationException;损坏 ⇒ 由 codec 抛
//   ADR-031 §③ —— 池在**构造函数**(IBlobPool 住 Sim.Codec,不进接口签名)
//   ADR-031 §④ —— 本实现**不**暴露 freehand 路径(接口面本就不含)
//   ADR-006 Amendment G-2 —— 载荷字节住不可变 blob 池;PayloadRef 即其寻址
//   ADR-029 §② —— 对称先例(PayloadEncoder(IBlobSink) 同款)
//
// ⚠️ 本类**不重写**任何 codec 逻辑 —— 它只补「够得着」这一层:
//   ① 池寻址(把 PayloadRef 变成字节段);② 委托既有 PayloadCodec.Decode<T> 做分派 + schema 校验。
//   与 ADR-029 对 PayloadEncoder 的处置逐字同款(镜像既有 DecodeBoxed,不改 35 个 struct)。

using System;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>
    /// 载荷解码器 —— 池寻址 + 委托 <see cref="PayloadCodec"/> 解码。
    /// </summary>
    /// <remarks>
    /// <para><b>池在构造函数(ADR-031 §③)</b> —— 逐字承 ADR-029 §② 对
    /// <c>PayloadEncoder(IBlobSink)</c> 的处置:<c>IBlobPool</c> 刻意住 <c>Sim.Codec</c>
    /// 而非 <c>Sim.Contracts</c>,接口签名里零池引用 ⇒ <c>Sim.Contracts → Sim.Codec</c>
    /// 的非法边**不会产生**。</para>
    /// <para><b>「查无」与「损坏」分两层(ADR-031 §②)</b> —— 本实现把池未命中 / 引用越界
    /// 统一为 <see cref="InvalidOperationException"/>;字节在但 schema 违例由
    /// <see cref="PayloadCodec.Decode{T}"/> 抛 <see cref="InvalidDataException"/>。
    /// (既有宽容形 <c>PayloadCodec.TryGetPayload</c> 保留不动 —— 它服务呈现侧。)</para>
    /// </remarks>
    public sealed class PayloadDecoder : IPayloadDecoder
    {
        private readonly IBlobPool _blobPool;

        /// <summary>构造解码器。</summary>
        /// <param name="blobPool">blob 池读面(实现者 = 流实现:7a / 45)。</param>
        public PayloadDecoder(IBlobPool blobPool)
        {
            _blobPool = blobPool ?? throw new ArgumentNullException(nameof(blobPool));
        }

        /// <inheritdoc />
        public T Decode<T>(EventKind kind, in PayloadRef payloadRef) where T : struct
        {
            ReadOnlySpan<byte> span = ResolveSpan(in payloadRef);
            return PayloadCodec.Decode<T>(kind, span);
        }

        /// <summary>
        /// 池寻址 —— 把 <see cref="PayloadRef"/> 解析为字节段。
        /// </summary>
        /// <remarks>
        /// 失败语义与 <c>PayloadCodec.TryGetPayloadSpan</c> 的**边界判据逐字一致**
        /// (负 Offset / 负 Length / 未驻留 / 越界),差别只在**表达**:彼处返回 false,
        /// 此处抛 —— 承 ADR-031 §②「把『未驻留』显式化」。
        /// </remarks>
        /// <exception cref="InvalidOperationException">查无此数据(未驻留 / 越界)。</exception>
        private ReadOnlySpan<byte> ResolveSpan(in PayloadRef r)
        {
            if (r.Offset < 0 || r.Length < 0)
                throw new InvalidOperationException(
                    $"PayloadRef 非法:Offset={r.Offset}, Length={r.Length}(不得为负)—— 查无此数据。");

            if (!_blobPool.TryGetBlob(r.BlobId, out var blob))
                throw new InvalidOperationException(
                    $"blob 池未驻留 BlobId={r.BlobId}(事件可能引用尚未装载的 blob)—— 查无此数据。");

            if ((long)r.Offset + r.Length > blob.Length)
                throw new InvalidOperationException(
                    $"PayloadRef 越界:BlobId={r.BlobId} 段 [{r.Offset}, {r.Offset + r.Length}) " +
                    $"超出 blob 长度 {blob.Length} —— 查无此数据。");

            return blob.Span.Slice(r.Offset, r.Length);
        }
    }
}
