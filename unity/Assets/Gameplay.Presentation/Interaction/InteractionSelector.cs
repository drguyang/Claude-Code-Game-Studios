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

        /// <summary>全局交互半径的**单源持有者**(AC-4-17)。
        /// ⚠️ <b>本类不持半径数值</b> —— 只持 <see cref="InteractionRadius"/> 引用(与 6 的
        /// 触发半径共享同一内存值)。第二处 `int` 数值 = 静默脱钩(radius_single_source_test 守)。
        /// ⚠️ <b>数值归数值轮</b> —— 本故事只登记形状,不签取值。</summary>
        private readonly InteractionRadius _radius;

        /// <summary>构造选择器(<b>单源半径</b>形参,AC-4-17)。
        /// <para>半径经 <see cref="InteractionRadius"/> 注入 —— 与 6 的触发半径同一实例,
        /// 结构上不可能出现「第二处填数」。</para></summary>
        /// <param name="discoveryReporter">发现上报面(4 的唯一出境通道;<b>不是</b> <c>IEventSink</c>)。</param>
        /// <param name="radius">交互半径的**单源持有者**(与 6 共享)。</param>
        public InteractionSelector(IDiscoveryReporter discoveryReporter, InteractionRadius radius)
        {
            _discoveryReporter = discoveryReporter ?? throw new ArgumentNullException(nameof(discoveryReporter));
            _radius = radius ?? throw new ArgumentNullException(nameof(radius));
        }

        /// <summary>便捷重载:以裸 <c>int</c> 构造 —— 内部包成 <see cref="InteractionRadius"/>
        /// (下界 <c>4-DC-1</c> 校验仍生效)。
        /// <para>⚠️ 本重载**不构成第二声明**:它不存自己的数,只是转发给单源持有者。</para></summary>
        public InteractionSelector(IDiscoveryReporter discoveryReporter, int rInteract)
            : this(discoveryReporter, new InteractionRadius(rInteract))
        {
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
            // ⚠️ 第一键是 d∞(**player_cell**, cell)—— **相对玩家格**,不是到原点(story 002)。
            //    裁剪(≤ R_INTERACT)发生在 argmin **之前**,归 story 003/004(本故事夹具候选集已裁剪)。
            Candidate best = candidates[0];
            for (int i = 1; i < candidates.Count; i++)
            {
                if (IsBetter(candidates[i], best, intent.PlayerCell)) best = candidates[i];
            }

            // 主动交互了 POI ⇒ 请 6 记账(**请求**,不是写入 —— AC-4-02 注释义务)。
            // 「走进但无输入」永远到不了这里 ⇒ POI 无 Request(AC-4-12)。
            //
            // ⚠️ 这是**单选权**的自报路径 —— 只报 argmin 胜者。
            //   **广播式自报**(邻域内**全部** POI 各自出报,与 argmin 无因果)归 story 004 的
            //   `NeighbourhoodReporter`,**不**在本方法内(否则「落选 POI 被吞」= 发现饿死)。
            //   本方法保留单选自报 = F-4.3b 的「交互意图打给谁」半边;广播半边在邻域装载后另发。
            if (best.Kind == InteractableKind.PoiCell)
            {
                _discoveryReporter.Request(new DiscoveryRequest(best.StableId, intent.PlayerCell, intent.Tick));
            }

            return InteractTarget.Of(best.Kind, best.StableId, best.Cell);
        }

        /// <summary>
        /// 两候选的全序比较(F-4.1 三键,story 002 / AC-4-06)。
        /// <para><b>字典序</b>:⟨<c>d∞</c>, <c>KindPriority[kind]</c>, <c>StableId</c>⟩ ——
        /// 逐键比较,首个不等的键定胜负,小者胜(F-4.1 的三键全序)。</para>
        /// <para>⚠️ 第一键是 <c>d∞(playerCell, cell)</c> —— <b>相对玩家格</b>(F-4.1 判定式),
        /// <b>不是</b>到原点。原点距只在玩家恰在原点时相等(F-4.1b 例即常踩此线)。</para>
        /// <para>⚠️ 第二键用 <see cref="KindPriorityOf"/> 查表,<b>不是</b>枚举序 ——
        /// 枚举声明序只是登记序,优先级数值归数值轮(F-4.1 明写)。</para>
        /// </summary>
        private static bool IsBetter(in Candidate a, in Candidate b, in WorldPos playerCell)
        {
            // 第一键:d∞(player_cell, cell) —— Chebyshev 整数距离(相对玩家格),小者胜。
            long da = Chebyshev(playerCell, a.Cell);
            long db = Chebyshev(playerCell, b.Cell);
            if (da != db) return da < db;

            // 第二键:KindPriority 查表(F-4.1 中间键)。十项互异保证本键对
            // (Kind 不同 ⇒ 优先级不同)是全序,等距决胜归于此 —— 4-DC-3 构建期校验(006)守之。
            int pa = KindPriorityOf(a.Kind);
            int pb = KindPriorityOf(b.Kind);
            if (pa != pb) return pa < pb;

            // 第三键:稳定 id,单一 int64 标量,小者胜(F-4.1:禁析取 —— 折成单标量)。
            return a.StableId < b.StableId;
        }

        /// <summary>
        /// F-4.1 第二键 <c>KindPriority</c> 的查表。
        /// <para>⚠️ <b>数值归数值轮,接线不归</b> —— 表本体 = <see cref="KindPriorityTable"/>
        /// (story-006 评审 F-2 修复后的**唯一**真源):选择器与 4-DC-3 校验器读**同一张表**,
        /// 物理上不可能是两张。当前取值 = GDD §F-4.1b 演示序,真表待数值轮落
        /// <c>interaction_kinds.json</c>(ADR-014 烘焙,装载归 story 007)。</para>
        /// </summary>
        private static int KindPriorityOf(InteractableKind kind) => KindPriorityTable.PriorityOf(kind);

        /// <summary>
        /// 两格之间的 Chebyshev 距离 <c>d∞(a,b) := max(|Δx|,|Δy|,|Δz|)</c>(F-4.1 第一键)。
        /// <para>⚠️ <b>先拓宽到 int64 再取绝对值</b>(story 002 承重纪律):
        /// <c>(long)a.X - b.X</c> 必须在 <c>Math.Abs</c> 之前 —— 否则两格分量相减
        /// (如 <c>int.MinValue - 1</c>)在 int 域溢出,末位错号 ⇒ 全序第一键崩。
        /// 原稿「int 距离」已就此改判(见 GDD F-4.1 订正)。</para>
        /// <para>全整数,无 float —— AC-4-05 / ADR-006 定点纪律同款。</para>
        /// </summary>
        private static long Chebyshev(in WorldPos a, in WorldPos b)
        {
            long dx = Math.Abs((long)a.X - b.X);
            long dy = Math.Abs((long)a.Y - b.Y);
            long dz = Math.Abs((long)a.Z - b.Z);
            long m = dx > dy ? dx : dy;
            return m > dz ? m : dz;
        }
    }
}
