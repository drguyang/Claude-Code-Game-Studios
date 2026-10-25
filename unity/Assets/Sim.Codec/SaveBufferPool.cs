// GDD `persistence-service.md` 规则五 —— **GC 纪律:缓冲池复用**。
//
// 权威来源:
//   GDD `:123` —— 「**GC 纪律**:大 `byte[]` 分配会触发主线程 GC 停顿 ⇒ **缓冲池复用**」
//   GDD `:282-283` —— 「体积上界论证 / SHA256 覆盖范围 / **缓冲池旋钮**等 **不立编号**
//                     —— 它们要么是定义、要么是恒等式论证、要么是实现纪律」
//   AC-7a-07 —— BLOCKING 判面是「写盘 IO 与 SHA256 在非主线程」;**缓冲池不在该 AC 的字面内**
//                (它来自规则五的 GC 纪律),本类因此**不冒充** AC-7a-07 的兑现物。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 为什么**不用** `System.Buffers.ArrayPool<byte>.Shared`
// ─────────────────────────────────────────────────────────────────────────────
// `ArrayPool.Rent(n)` 返回的数组 **`Length ≥ n`**(实现上按 2 的幂分桶)⇒ 若直接把它当
// 「有效字节」用,`SaveService.AddChecksum` 会把**池里上一轮的残留尾部**一起算进 SHA256
// ⇒ 写出的存档校验和与读回时的重算不一致 ⇒ **静默坏档**(而且只在「租到的数组恰好更大」时
// 出现,是最难复现的一类)。
//
// 三条出路:
//   ① 全链路改成 `(byte[] buf, int len)` 二元组 —— 会把 `compose` 委托、
//      `AddChecksum` / `StripChecksum` / `ValidateChecksum`、15 处 `Compose` 调用点
//      与 `SaveService` 的写路径**全部**改签名。收益是复用率高,代价是一次横跨整个
//      存档路径的契约变更 —— 而存档体今日是 KB 级,GC 压力**从未被测到过**。
//   ② 池**按精确长度**复用 —— 契约一字不改(`byte[].Length` 仍然就是有效长度),
//      代价是「长度变了就复用不上」。
//   ③ 不做 —— 但用户已裁定本轮含缓冲池。
//
// ⇒ **取 ②**。复用率不高(同一世界的存档长度会随事件数变化),但**零正确性风险**,
//    且「池真的在复用」是可断言的(同一长度两次 Rent 拿到同一实例)。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 本类覆盖的范围(如实登记,不借绿)
// ─────────────────────────────────────────────────────────────────────────────
// **覆盖**:`SaveComposer.Compose` 的 `combined` 合成缓冲、`SaveService.AddChecksum`
//          的产出缓冲 —— 即存档路径上**最大的两块**。
// **不覆盖**:`CodecWriter` 的内部增长缓冲(它是指数扩容的 `struct`,`ToArray()` 每次
//          新分配)与 `SimEventListCodec` 的中间产物。**理由**:那是编码器自身的分配纪律,
//          改它要动 `CodecWriter` 的 struct 语义与 4 个 codec 文件;且它的产物长度天然
//          逐次不同(事件数在变),精确长度池的命中率会更低。
//          ⇒ **登记为剩余缺口**,不声称「缓冲池已全面落地」。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>
    /// 存档路径的**精确长度**缓冲池。
    /// <para><b>契约不变</b>:<see cref="Rent"/> 返回的数组 `Length` **恰等于**请求长度
    /// (不像 `ArrayPool` 那样可能更大)⇒ 调用方可以继续把 `byte[].Length` 当作有效长度用。</para>
    /// <para><b>线程安全</b>:是(内部锁)。生产上主线程(组版)与 worker 线程(校验和)
    /// 会同时用到它。</para>
    /// </summary>
    public sealed class SaveBufferPool
    {
        /// <summary>
        /// 单块缓冲的保留上限(字节)。超过它的一律不回收 —— 防一次超大存档把池撑住不放。
        /// <para>取 8 MiB:远大于今日任何存档体(KB 级),同时把「池自身成为内存泄漏源」
        /// 这条风险钉住。**该值归实现纪律,不立旋钮**(GDD `:282-283`)。</para>
        /// </summary>
        public const int DefaultMaxRetainedBufferBytes = 8 * 1024 * 1024;

        /// <summary>池内保留的总字节上限。超过即停止回收(不驱逐已有块 —— 驱逐会让行为不可预期)。</summary>
        public const int DefaultMaxRetainedTotalBytes = 32 * 1024 * 1024;

        private readonly int _maxBufferBytes;
        private readonly int _maxTotalBytes;
        private readonly object _gate = new object();

        /// <summary>按**精确长度**分桶的空闲块。</summary>
        private readonly Dictionary<int, Stack<byte[]>> _free = new Dictionary<int, Stack<byte[]>>();

        private int _retainedBytes;

        /// <summary>构造。</summary>
        /// <param name="maxBufferBytes">单块保留上限(字节)。</param>
        /// <param name="maxTotalBytes">池内总保留上限(字节)。</param>
        public SaveBufferPool(
            int maxBufferBytes = DefaultMaxRetainedBufferBytes,
            int maxTotalBytes = DefaultMaxRetainedTotalBytes)
        {
            _maxBufferBytes = maxBufferBytes;
            _maxTotalBytes = maxTotalBytes;
        }

        /// <summary>池内当前保留的总字节数(诊断 / 夹具用)。</summary>
        public int RetainedBytes
        {
            get { lock (_gate) { return _retainedBytes; } }
        }

        /// <summary>
        /// 借一块长度**恰为** <paramref name="length"/> 的缓冲。
        /// <para>池内有同长度的空闲块 ⇒ 复用(内容**不保证清零** —— 调用方须整块覆写,
        /// 这与 `new byte[]` 的语义不同,是复用的必要代价)。</para>
        /// </summary>
        /// <param name="length">请求长度(≥ 0)。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> 为负。</exception>
        public byte[] Rent(int length)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));

            lock (_gate)
            {
                Stack<byte[]> bucket;
                if (_free.TryGetValue(length, out bucket) && bucket.Count > 0)
                {
                    _retainedBytes -= length;
                    return bucket.Pop();
                }
            }

            return new byte[length];
        }

        /// <summary>
        /// 归还一块缓冲。
        /// <para>⚠️ <b>调用方须保证此后不再持有该数组</b> —— 复用意味着内容会被覆写。</para>
        /// <para>超过保留上限的块**直接丢弃**(交给 GC),不入池。</para>
        /// </summary>
        /// <param name="buffer">要归还的数组;null 静默忽略。</param>
        public void Return(byte[] buffer)
        {
            if (buffer == null) return;

            int length = buffer.Length;
            if (length == 0 || length > _maxBufferBytes) return;

            lock (_gate)
            {
                if (_retainedBytes + length > _maxTotalBytes) return;

                Stack<byte[]> bucket;
                if (!_free.TryGetValue(length, out bucket))
                {
                    bucket = new Stack<byte[]>();
                    _free[length] = bucket;
                }
                bucket.Push(buffer);
                _retainedBytes += length;
            }
        }
    }
}
