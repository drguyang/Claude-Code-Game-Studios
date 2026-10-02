// ADR-029 —— 载荷编码器(第七个 P0 抽象点)。
//
// 权威来源:
//   ADR-029 §① —— 第七抽象点,住 Sim.Contracts;乙案:编码 + 入池一体,返回 PayloadRef
//   ADR-006 Amendment G-2 —— 载荷 header/blob 形态(本接口补其**写形**)
//   ADR-005:257-270 —— 抽象点集合(本件为第七个)
//   ADR-025 §①:111 —— Sim 引用集 = {BCL, Sim.Contracts};本接口使 Sim 无需见 Sim.Codec
//
// 存在理由(ADR-029 §Summary):
//   Sim 的写者要产出 PayloadRef,却够不着编码面 —— PayloadCodec 住 Sim.Codec,
//   不可变 blob 池住 7a/45。实测后果:全库每个写者都在手搓 PayloadRef,
//   且零字节真的进池。本接口是 Sim 侧**唯一合法**的编码路径。

using System;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 载荷编码器 —— 把强类型载荷编码进不可变 blob 池,返回其寻址引用。
    /// 实现者 = <c>Sim.Codec</c> 的 <c>PayloadEncoder</c>(它看得见 <c>PayloadCodec</c>);
    /// 消费者 = <c>Sim</c> 的全部事件写者(6 / 9 / 25 / 29 / 37 / 52 …)。
    /// </summary>
    /// <remarks>
    /// <para><b>乙案(ADR-029 §①)</b>:编码与入池**一体** —— 返回 <see cref="PayloadRef"/> 而非
    /// <c>byte[]</c>,使 <c>Sim</c> 侧只持**一个**依赖;裸字节半途形态不得暴露给 <c>Sim</c>。</para>
    /// <para><b>同载荷多次编码不保证返回同一 BlobId</b> —— 池去重是实现细节,非契约。</para>
    /// <para><b>约束 = <c>where T : struct</c></b>,与既有 <c>PayloadCodec.Decode&lt;T&gt;</c> 逐字对称;
    /// 刻意**不**引入标记接口(ADR-029 §① 的评审修正 ②)。</para>
    /// <para><b>须收 <paramref name="kind"/></b> —— <c>T</c> 无法反推 <c>EventKind</c>
    /// (与 <c>Decode&lt;T&gt;</c> 同因);写者本就知道自己发的 Kind,不构成负担。</para>
    /// <para><b>例外(ADR-029 §① 修正 ④)</b>:<c>JudgmentRecorded</c> / <c>JudgmentRevised</c>
    /// 两支的载荷需额外 <c>freehandText</c>(ADR-006 G-3 的变长段,不在 <c>T</c> 里),
    /// 本接口**不承载** —— 实现须对其抛 <see cref="NotSupportedException"/>。</para>
    /// </remarks>
    public interface IPayloadEncoder
    {
        /// <summary>
        /// 编码 + 入池,返回 <see cref="PayloadRef"/>。
        /// </summary>
        /// <typeparam name="T">per-Kind 载荷 struct(须与 <paramref name="kind"/> 匹配)。</typeparam>
        /// <param name="kind">事件种类 —— 决定 codec 分派与 schema。</param>
        /// <param name="payload">强类型载荷。</param>
        /// <returns>指向池中不可变字节的寻址引用。</returns>
        /// <exception cref="NotSupportedException">
        /// <paramref name="kind"/> 为 <c>JudgmentRecorded</c> / <c>JudgmentRevised</c> ——
        /// 这两支须经具名重载 <c>PayloadCodec.Encode(in JudgmentXxxPayload, string freehandText)</c>。
        /// </exception>
        PayloadRef Encode<T>(EventKind kind, in T payload) where T : struct;
    }
}
