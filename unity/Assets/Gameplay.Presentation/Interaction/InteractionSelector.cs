// ADR 权威来源:
//   GDD design/gdd/interaction-system.md —— 规则一(纯目标选择器)/ 规则七(触发 = 主动交互)
//     / F-4.2(模态门)/ 4-DC-1(R_INTERACT 区间)
//   ADR-005(输入是意图源,不直接驱动模拟 —— 规则一在选择层的落实)
//   ADR-013 §九 C3 / ADR-018 §一 / ADR-020 §五(呈现层三件套同构:只读不持状态)
//
// ⚠️ 本故事(story-001)**只签边界形状**,不签机器数学:
//   · 全序三键体(F-4.1 的 d∞ 比较 + KindPriority + StableId)归 **story 002**(AC-4-06);
//   · 四源候选集装配(世界流 / 6 烘焙层 / 13 视图 / 23 BakedInitial)归 **story 003**;
//   · 模态门(ImodalState)与 Armed 压制归 **story 005**(AC-4-09/10);
//   · 真实发现链路归 **story 004**(AC-4-13/17/18)。
//
// 本类承载的是 001 六条 AC 共同指向的**形状**:
//   AC-4-01 零结算类型可达 · AC-4-02 零 IEventSink · AC-4-04 无状态纯函数
//   AC-4-05 零玩法数值 · AC-4-11 零相机档位 · AC-4-12 走进 ≠ 交互
//
// ⚠️ AC-4-02 的「零 Append ≠ 零上行」注释义务**就在本文件**:
//   本类**只经 `IDiscoveryReporter.Request` 出境**,永不持 `IEventSink`。
//   反射闭包(AC-4-02 结构半边)会断言本类及 4 命名空间闭包内
//   **无 `IEventSink` 字段 / 构造参 / 方法形参**。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Interaction
{
    /// <summary>
    /// 4 的目标选择器(F-4.2 模态门 + 主动交互触发门)。
    /// <para><b>规则一</b>:纯选择器 —— 输入 <c>(玩家格, 候选集, 意图)</c>,输出
    /// <see cref="InteractTarget"/>。只选目标,永不结算(结算归 20/17/37/8/10/11/6/23/18/24)。</para>
    /// <para><b>规则三</b>:候选集是**派生态**(四源皆为整数格),本类**不写三流**——
    /// 唯一出境面 = <see cref="IDiscoveryReporter.Request"/>(请求 ≠ 写入)。</para>
    /// <para><b>规则七</b>:触发条件 = <b>主动交互输入</b>,不是碰撞 ——
    /// 玩家走进 POI 格但无输入 ⇒ 零 <c>Request</c>(AC-4-12)。</para>
    /// </summary>
    /// <remarks>
    /// <para><b>无状态(AC-4-04)</b>:本类只有 <c>readonly</c> 实例字段,无 static 可变状态。
    /// 清空重建后同输入 ⇒ 同输出序列(F-4.1 的函数外延相同)。</para>
    /// <para><b>零玩法数值(AC-4-05)</b>:本类及 <see cref="Candidate"/> 家族不引
    /// <c>VitalsDto</c> / 病种名 / 档位 / 药性 / 环境修饰符类型(规则十/十二)。</para>
    /// <para><b>零相机档位(AC-4-11)</b>:本类不引 <c>ICameraRig</c> 或任何档位枚举(规则六)。</para>
    /// </remarks>
    public sealed class InteractionSelector
    {
        // ⚠️ AC-4-02 结构半边的落点:这里刻意只有 IDiscoveryReporter,**没有 IEventSink**。
        //   若有人加一个 IEventSink 字段 / 构造参 / 方法形参 ⇒ 反射闭包断言即红。
        private readonly IDiscoveryReporter _discoveryReporter;

        /// <summary>全局交互半径(整数格,4-DC-1:1 ≤ R_INTERACT ≤ min(W,H,D) − 1)。
        /// ⚠️ <b>数值归数值轮</b> —— 本故事只登记形状,不签取值。</summary>
        private readonly int _rInteract;

        /// <summary>构造选择器。</summary>
        /// <param name="discoveryReporter">发现上报面(4 的唯一出境通道;<b>不是</b> <c>IEventSink</c>)。</param>
        /// <param name="rInteract">交互半径(整数格)。</param>
        public InteractionSelector(IDiscoveryReporter discoveryReporter, int rInteract)
        {
            _discoveryReporter = discoveryReporter ?? throw new ArgumentNullException(nameof(discoveryReporter));
            if (rInteract < 1) throw new ArgumentOutOfRangeException(nameof(rInteract), "4-DC-1:r_interact ≥ 1");
            _rInteract = rInteract;
        }

        /// <summary>
        /// 选择目标(F-4.2)。
        /// <para><b>门序</b>:① 无主动交互输入 ⇒ <see cref="InteractTarget.None"/>;
        /// ② 无候选 ⇒ None;③ 取全序最小者(F-4.1,归 story 002)。</para>
        /// </summary>
        /// <param name="intent">玩家交互意图。</param>
        /// <param name="candidates">候选集(派生态,来自四源;调用方装配 —— story 003)。</param>
        /// <returns>选中目标;无则 <see cref="InteractTarget.None"/>。</returns>
        public InteractTarget Select(in InteractIntent intent, IReadOnlyList<Candidate> candidates)
        {
            // 规则七:无主动交互输入 ⇒ 不选择。「走进格」本身**不是**触发。
            if (!intent.Pressed) return InteractTarget.None;
            if (candidates == null || candidates.Count == 0) return InteractTarget.None;

            // F-4.1 三键全序(取 argmin)—— 机器数学归 story 002。
            // 本故事只交付「形状成立」:一次线性扫描取最小者,不含 KindPriority 表
            // (该表及其十项互异断言归 story 006 的 4-DC-3 构建期校验)。
            Candidate best = candidates[0];
            for (int i = 1; i < candidates.Count; i++)
            {
                if (IsBetter(candidates[i], best)) best = candidates[i];
            }

            // 主动交互了 POI ⇒ 请 6 记账(**请求**,不是写入 —— AC-4-02 注释义务)。
            // 「走进但无输入」永远到不了这里 ⇒ POI 无 Request(AC-4-12)。
            if (best.Kind == InteractableKind.PoiCell)
            {
                _discoveryReporter.Request(best.StableId, intent.Tick);
            }

            return InteractTarget.Of(best.Kind, best.StableId, best.Cell);
        }

        /// <summary>
        /// 两候选的全序比较(F-4.1 三键)。<b>本故事只签形态</b> —— 三键的完整体
        /// (Chebyshev `d∞` int64 → `KindPriority` 十项互异 → `StableId` int64 标量)
        /// 归 story 002 的 AC-4-06。
        /// </summary>
        private bool IsBetter(in Candidate a, in Candidate b)
        {
            // 第一键:d∞(Chebyshev 整数距离),小者胜。
            long da = Chebyshev(a.Cell);
            long db = Chebyshev(b.Cell);
            if (da != db) return da < db;

            // 第二键:种类序(本故事用枚举序占位;真表 + 十项互异断言归 002/006)。
            if (a.Kind != b.Kind) return a.Kind < b.Kind;

            // 第三键:稳定 id,单一 int64 标量,小者胜(F-4.1:禁析取)。
            return a.StableId < b.StableId;
        }

        /// <summary>Chebyshev 距离的整数平方(全整数,无 float —— AC-4-05 / ADR-006 定点纪律面外但同纪律)。</summary>
        private static long Chebyshev(in WorldPos c)
        {
            long ax = Math.Abs((long)c.X), ay = Math.Abs((long)c.Y), az = Math.Abs((long)c.Z);
            long m = ax > ay ? ax : ay;
            return m > az ? m : az;
        }
    }
}
