// R-1(U0-b 残留)· PayloadRef 的 blob 池契约(b1b 拍板点 0-2 的兑现件)。
//
// 权威来源:
//   b1b 设计卡 · 拍板点 0-2(2026-09-22 用户裁定甲案随附)—— 「PayloadRef 的 blob
//     **生命周期归流实现(7a / 45 侧)**,contracts 只定义 PayloadRef struct +
//     34 支 payload struct + **codec 访问签名**」。故:
//     · 本文件只定**读取面契约**(IBlobPool)+ 访问签名(TryGet* —— 见 PayloadCodec);
//     · 池的分配 / 释放 / 重映射(跨进程传输归 45,ADR-001 轮)**不在本批**。
//   ADR-006 Amendment G-2 —— 载荷字节住流持有的**不可变** blob 池;
//     落盘 = 传输 = 同一段字节(PayloadRef 即其寻址)。
//   ADR-009 §七 同构 —— 身份进流 / 位置表现;此处为「内容进池 / 引用进头」。
//
// 不可变性口径:池在给定 blobId 后其字节内容**不得再改**(append-only 流的自然推论)。
//   违反 ⇒ 已发出的 PayloadRef 静默变义 ⇒ 回放分叉 —— 属 7a/45 实现侧的硬义务,
//   本接口无法在编译期强制,登记为实现期断言面(7a 轮)。

using System;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>
    /// 不可变 blob 池的只读寻址契约。实现者 = 流实现(7a 持久化 / 45 网络);
    /// 消费者 = <see cref="PayloadCodec"/> 的 TryGet* 访问签名。
    /// </summary>
    public interface IBlobPool
    {
        /// <summary>
        /// 按 BlobId 取整段不可变字节。未知 / 未装载 ⇒ false(宽容 —— 事件可能引用
        /// 尚未驻留的 blob,由调用方决定降级);**不**以异常表达「查无此 blob」。
        /// </summary>
        bool TryGetBlob(int blobId, out ReadOnlyMemory<byte> blob);
    }
}
