// ADR-031 —— 载荷解码器(第八个 P0 抽象点)。
//
// 权威来源:
//   ADR-031 §① —— 第八抽象点,住 Sim.Contracts;实现 PayloadDecoder 住 Sim.Codec
//   ADR-031 §② —— 形状 = 抛形 `T Decode<T>(EventKind, in PayloadRef) where T : struct`
//   ADR-031 §③ —— 池依赖在**实现的构造函数**,不在本签名(承 ADR-029 §② 模式)
//   ADR-031 §④ —— **刻意不含 freehand 路径**(ADR-006 G-3:禁 sim 读自由文本)
//   ADR-006 Amendment G-2 —— 载荷 header/blob 形态(本接口补其**读访问面**)
//   ADR-005:257-270 —— 抽象点集合(本件为第八个)
//   ADR-025 §①:111 —— Sim 引用集 = {BCL, Sim.Contracts};本接口使 Sim 无需见 Sim.Codec
//
// 存在理由(ADR-031 §Summary):
//   ADR-029 立了**写侧**抽象点 IPayloadEncoder,理由是「Sim 的写者够不着编码面」。
//   **读侧有完全同构的问题,却从未被裁决** —— PayloadCodec.TryGetPayload 同样住
//   Sim.Codec,Sim 同样够不着。实测三次同型绕行(CaseCloseDecider / ConsequenceInputAggregator /
//   PatternDetector),**无一处是「解决」**。S12-9 的勘察证明第四个撞墙者(53 Judge)绕不开 ——
//   Judge 的语义就建立在载荷内容上(GDD medical-consequences.md:263)。
//   本接口是 Sim 侧**唯一合法**的载荷读取路径。

using System;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 载荷解码器 —— 从 <see cref="PayloadRef"/> 寻址并解码为强类型载荷。
    /// 实现者 = <c>Sim.Codec</c> 的 <c>PayloadDecoder</c>(它看得见 <c>PayloadCodec</c>);
    /// 消费者 = <c>Sim</c> 的全部判定方(9 / 37 / 53 / 6 …)。
    /// </summary>
    /// <remarks>
    /// <para><b>为何收 <see cref="PayloadRef"/> 而非 <c>SimEvent</c></b>:既有
    /// <c>PayloadCodec.TryGetPayload&lt;T&gt;</c> 收整个 <c>in SimEvent</c>,而它实际只用
    /// <c>e.Kind</c> + <c>e.Payload</c> 两个字段 ⇒ 接口面承担了不必要的耦合。
    /// 本接口收**最小充分集**。</para>
    /// <para><b>池依赖在实现构造函数,不在本签名</b> —— 逐字承 ADR-029 §② 的模式
    /// (<c>PayloadEncoder(IBlobSink)</c> 同理):<c>IBlobPool</c> 住 <c>Sim.Codec</c>,
    /// 若出现在本签名则 <c>Sim.Contracts</c> 须引用 <c>Sim.Codec</c> ⇒ 造出非法边。
    /// <c>Sim</c> 侧的消费者经构造注入拿实例(与拿 <c>IPayloadEncoder</c> 同款)。</para>
    /// <para><b>刻意不含 freehand 路径</b>(ADR-006 G-3):写侧 <c>IPayloadEncoder</c> 有
    /// 两个 Judgment 具名重载,读侧**没有** —— 该不对称是设计。G-3 禁 sim 读自由文本
    /// ⇒ 接口面不含 = **编译期不可表达**,不靠纪律守。</para>
    /// <para><b>与 <c>TryGetPayload</c> 的分工</b>:后者是**宽容**形(bool 承载「查无」),
    /// 服务呈现侧等消费者,**本件不改它**。本接口把「未驻留」也显式化为异常 ⇒
    /// 「查无此数据」与「数据坏了」在**类型上可分</b>」。</para>
    /// </remarks>
    public interface IPayloadDecoder
    {
        /// <summary>
        /// 从 blob 池寻址并解码为强类型载荷。
        /// </summary>
        /// <typeparam name="T">per-Kind 载荷 struct(须与 <paramref name="kind"/> 匹配)。</typeparam>
        /// <param name="kind">事件种类 —— 决定 codec 分派与 schema 校验。</param>
        /// <param name="payloadRef">载荷寻址引用(blob 池内偏移段)。</param>
        /// <returns>解码后的强类型载荷。</returns>
        /// <exception cref="InvalidOperationException">
        /// 池未驻留该 blob / 引用越界(含负 Offset / 负 Length)—— **查无此数据**层。
        /// 调用方决定是否降级;本接口不替调用方决定。
        /// </exception>
        /// <exception cref="InvalidDataException">
        /// 字节在但违反该 Kind 的 schema(**损坏**,非「查无」);或请求的 <typeparamref name="T"/>
        /// 与该 <paramref name="kind"/> 的载荷类型不匹配(编程错误)。
        /// </exception>
        T Decode<T>(EventKind kind, in PayloadRef payloadRef) where T : struct;
    }
}
