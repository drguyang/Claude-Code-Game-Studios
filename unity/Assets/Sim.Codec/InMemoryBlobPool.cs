// ADR-029 §Implementation Guidelines —— 测试用内存 blob 池(读面 + 写面同一实现)。
//
// 权威来源:
//   ADR-029 §Implementation Guidelines —— 「本 story 提供一个测试用内存池(InMemoryBlobSink)
//     供单测与接线支使用」;池的生产实现归 7a 存档侧 / 45 网络侧
//   ADR-006 Amendment G-2 —— 不可变 blob 池
//
// ⚠️ 本类是**测试与接线期**载体,不是生产池:
//   · 无容量上限、无落盘、无跨进程重映射;
//   · 生产池(7a 存档 / 45 网络)须各自实现 IBlobPool + IBlobSink,并满足
//     「同 BlobId 内容此后不再变」的硬义务。
//   刻意做成**追加式且从不改写**(append-only),使「不可变」在测试载体上天然成立。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>
    /// 内存 blob 池 —— 同时实现读面(<see cref="IBlobPool"/>)与写面(<see cref="IBlobSink"/>)，
    /// 以及读档专用的按号装载面(<see cref="IBlobRestoreSink"/>，ADR-034 §⑤)。
    /// 测试与接线期用;生产实现归 7a / 45。
    /// </summary>
    /// <remarks>
    /// <para><b>不可变性</b>:<see cref="Store"/> 复制入参字节并只追加,已发出的 BlobId 内容永不改写
    /// ⇒ 满足 ADR-006 G-2 的硬义务。</para>
    /// <para><b>BlobId 从 0 起</b>(与 <c>PayloadRef.BlobId</c> 的 int 值域一致)。</para>
    /// <para><b>非线程安全</b> —— 与 sim 的单线程 tick 模型一致(ADR-005 主机唯一 Step)。</para>
    /// </remarks>
    public sealed class InMemoryBlobPool : IBlobPool, IBlobSink, IBlobRestoreSink
    {
        // ⚠️ 元素可空(ADR-034 §⑤「允许留空」):读档按号装载时,存档里只含
        //    **存活引用**的 blob ⇒ 号必然不连续 ⇒ 空洞用 null 占位。
        //    空洞上 TryGetBlob 返回 false(「未驻留」),与「越界」同一处置。
        private readonly List<byte[]> _blobs = new List<byte[]>();

        /// <summary>已占用的号上界 + 1(含空洞)。**不是**「已装载条目数」。</summary>
        public int Count => _blobs.Count;

        /// <inheritdoc />
        public int Store(ReadOnlySpan<byte> blob)
        {
            // 复制:调用方可能复用其缓冲(接口契约明写「实现须复制」)。
            _blobs.Add(blob.ToArray());
            return _blobs.Count - 1;
        }

        /// <inheritdoc />
        public bool TryGetBlob(int blobId, out ReadOnlyMemory<byte> blob)
        {
            if (blobId < 0 || blobId >= _blobs.Count || _blobs[blobId] == null)
            {
                blob = default;
                return false;   // 宽容:未知 / 未驻留 ⇒ false,不以异常表达「查无此 blob」
            }
            blob = _blobs[blobId];
            return true;
        }

        /// <inheritdoc />
        /// <remarks>
        /// ADR-034 §⑤ 的按号装载。三条纪律:
        /// <list type="number">
        /// <item>号可为任意非负值;落在已有序号之上时**中间留空洞**(null),不补零字节
        /// —— 补零会让「未驻留」与「空载荷」不可分。</item>
        /// <item>同号装载:字节相同 ⇒ 幂等 no-op;字节不同 ⇒ 抛(池不可变,ADR-006 G-2)。</item>
        /// <item>装载**不推进** <see cref="Store"/> 的发号水位之外的东西 —— <see cref="Store"/>
        /// 直接取 <c>_blobs.Count</c>,装载后天然接在最高号之后。</item>
        /// </list>
        /// </remarks>
        public void Restore(int blobId, ReadOnlySpan<byte> blob)
        {
            if (blobId < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(blobId), blobId,
                    "BlobId 须非负(存档里的 PayloadRef.BlobId 值域)");

            while (_blobs.Count <= blobId) _blobs.Add(null);

            if (_blobs[blobId] != null)
            {
                // 同号 **且内容相同** ⇒ 幂等 no-op(2026-10-28 实跑修正):
                // 生产装配根把**同一个**池同时用作编码写面与读档装载面
                // (`CompositionRoot` 的 encoder / decoder / blobRestoreSink 共用一个实例),
                // 于是「本会话写过 → 本会话读回」这一最普通的情形会命中同号。
                // 内容一致 ⇒ 什么都没改变,不违背「池不可变」。
                if (blob.SequenceEqual(_blobs[blobId])) return;

                // 同号 **内容不同** ⇒ 真冲突:要么档损坏,要么把另一世界的档读进了本池。
                // 静默覆盖会让已发出的 PayloadRef 变义 ⇒ 回放分叉,故必须抛。
                throw new InvalidOperationException(
                    $"blobId={blobId} 已装载**且内容不同** —— 池不可变(ADR-006 G-2)," +
                    "覆盖会让已发出的 PayloadRef 静默变义 ⇒ 回放分叉" +
                    "(同号同内容则是幂等 no-op,不会走到这里)");
            }

            _blobs[blobId] = blob.ToArray();
        }
    }
}
