// ADR-029 §② —— blob 池的**写面**(与 IBlobPool 的读面配对)。
//
// 权威来源:
//   ADR-029 §② —— 乙案要求编码器能**写入**池,而 IBlobPool 只有读面 ⇒ 补此签名
//   ADR-006 Amendment G-2 —— 载荷字节住流持有的**不可变** blob 池;落盘 = 传输 = 同一段字节
//   IBlobPool.cs 头注 —— 「池的分配 / 释放 / 重映射不在本批;生命周期归流实现(7a / 45 侧)」
//
// ⚠️ 归属说明(ADR-029 §②):
//   IBlobSink 刻意住 **Sim.Codec 而非 Sim.Contracts** —— Sim 的写者**不该**直接摸池,
//   它只调 IPayloadEncoder。池是 codec 与流实现之间的事;暴露给 Sim 会开第二条旁路。
//   ⇒ Sim 引用集白名单({BCL, Sim.Contracts})天然挡住这条边。

using System;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>
    /// 不可变 blob 池的**写入**契约。实现者 = 池的所有者(7a 持久化 / 45 网络 —— 承
    /// <see cref="IBlobPool"/> 头注「生命周期归流实现」);消费者 = <c>PayloadEncoder</c>。
    /// </summary>
    /// <remarks>
    /// <para><b>不可变性(ADR-006 G-2 硬义务)</b>:给定 <c>BlobId</c> 后其字节内容**不得再改**。
    /// 违反 ⇒ 已发出的 <see cref="DaYiJingCheng.Sim.Contracts.PayloadRef"/> 静默变义 ⇒
    /// **回放分叉**。本接口无法在编译期强制,登记为实现期断言面(7a 轮)。</para>
    /// <para>本接口只定**签名** —— 分配 / 释放 / 去重 / 并发 / 容量归各实现方 ADR 轮。</para>
    /// </remarks>
    public interface IBlobSink
    {
        /// <summary>
        /// 存入一段不可变字节,返回其 <c>BlobId</c>(供 <see cref="DaYiJingCheng.Sim.Contracts.PayloadRef"/> 寻址)。
        /// </summary>
        /// <param name="blob">待存字节。实现须**复制**(调用方可能复用其缓冲)。</param>
        /// <returns>BlobId。同内容重复存入**不要求**返回同一 id(去重是实现细节,非契约)。</returns>
        int Store(ReadOnlySpan<byte> blob);
    }
}
