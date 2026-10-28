// ADR-034 §Decision ⑤ —— blob 池的「按号装载」契约(读档路径专用)。
//
// 权威来源:docs/architecture/adr-034-save-v2-payload-segment.md
//
// 为什么不复用 IBlobSink:它的 `Store(ReadOnlySpan<byte>) -> int` **只发号**,
// 无法把一段字节装到**调用方指定**的 BlobId 上。而读档必须保号 —— 落盘的
// `PayloadRef.BlobId` 一旦重编号,存档里每一条引用同时失效。
//
// ⚠️ 刻意住 Sim.Codec 而非 Sim.Contracts(与 IBlobSink / IBlobPool 同侧):
//    进 Contracts 会把池的**写面**暴露给 Sim,正是 ADR-029 §② 挡掉的那条旁路
//    (ADR-034 §⑤ / C5)。

using System;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>
    /// 不可变 blob 池的**按号装载**契约 —— 读档路径专用(<c>SaveService.Load</c>)。
    /// <para>与 <see cref="IBlobSink"/> 的分工:<see cref="IBlobSink.Store"/> 由池**发号**
    /// (写流用);本接口由**调用方给号**(读档用,号来自存档里的 <c>PayloadRef.BlobId</c>)。</para>
    /// </summary>
    /// <remarks>
    /// <para><b>不可变性(ADR-006 G-2 硬义务)</b>:同号装载**不得改变**已装载的内容 ——
    /// ① 同号 **且字节相同** ⇒ **幂等 no-op**(2026-10-28 实跑修正:生产装配根把同一个池
    /// 同时用作编码写面与装载面,「本会话写过 → 本会话读回」是最普通的情形,
    /// 一律抛会让它必炸);② 同号 **而字节不同** ⇒ <b>抛</b>,不得静默覆盖 ——
    /// 那要么是档损坏、要么是把另一世界的档读进了本池,覆盖会让已发出的
    /// <c>PayloadRef</c> 静默变义 ⇒ 回放分叉。</para>
    /// <para><b>实现须复制入参</b>(调用方可能复用其缓冲)。</para>
    /// <para><b>允许留空</b>:号可以不连续 —— 未被任何存活事件引用的 blob 不进存档
    /// (ADR-034 §② 引用驱动),故装载后池内可能存在空洞。空洞上的
    /// <see cref="IBlobPool.TryGetBlob"/> 返回 <c>false</c>(「未驻留」,不是「损坏」)。</para>
    /// </remarks>
    public interface IBlobRestoreSink
    {
        /// <summary>
        /// 把一段字节装到调用方指定的 <paramref name="blobId"/> 上。
        /// </summary>
        /// <param name="blobId">目标号(≥ 0;来自存档里的 <c>PayloadRef.BlobId</c>)。</param>
        /// <param name="blob">载荷字节。实现须复制。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="blobId"/> 为负。</exception>
        /// <exception cref="InvalidOperationException">
        /// 该号已装载且**字节不同**(池不可变);同号同字节是幂等 no-op,不抛。
        /// </exception>
        void Restore(int blobId, ReadOnlySpan<byte> blob);
    }
}
