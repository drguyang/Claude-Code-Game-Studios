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
    /// 内存 blob 池 —— 同时实现读面(<see cref="IBlobPool"/>)与写面(<see cref="IBlobSink"/>)。
    /// 测试与接线期用;生产实现归 7a / 45。
    /// </summary>
    /// <remarks>
    /// <para><b>不可变性</b>:<see cref="Store"/> 复制入参字节并只追加,已发出的 BlobId 内容永不改写
    /// ⇒ 满足 ADR-006 G-2 的硬义务。</para>
    /// <para><b>BlobId 从 0 起</b>(与 <c>PayloadRef.BlobId</c> 的 int 值域一致)。</para>
    /// <para><b>非线程安全</b> —— 与 sim 的单线程 tick 模型一致(ADR-005 主机唯一 Step)。</para>
    /// </remarks>
    public sealed class InMemoryBlobPool : IBlobPool, IBlobSink
    {
        private readonly List<byte[]> _blobs = new List<byte[]>();

        /// <summary>已存 blob 数。</summary>
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
            if (blobId < 0 || blobId >= _blobs.Count)
            {
                blob = default;
                return false;   // 宽容:未知 / 未驻留 ⇒ false,不以异常表达「查无此 blob」
            }
            blob = _blobs[blobId];
            return true;
        }
    }
}
