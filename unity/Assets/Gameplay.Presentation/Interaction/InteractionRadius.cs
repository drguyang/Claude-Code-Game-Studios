// ADR 权威来源:
//   GDD design/gdd/interaction-system.md —— F-4.4 / 4-DC-1(r_interact 区间)/ OQ-4-8(单值裁定)
//   ADR-014 §二/§五(r_interact 内联于 interaction_kinds.json,一次装载,内容哈希派生 ConfigVersion)
//   ADR-015 §三(半径 = 整数格数)
//
// ⚠️ AC-4-17 的**唯一落点**:`R_INTERACT` 是**单一装载常量** ——
//     4 的选择半径与 6 的「已发现」触发半径是**同一个字段**(`interaction_kinds.json` 的
//     `r_interact`),**两处消费同一内存值**。本类型是该值的**唯一持有者**;
//     任何第二处 `const` / `[SerializeField]` 声明 = 红(两处各填一个数会**静默脱钩**)。
//
// ⚠️ 本类型**不带数值** —— 值归用户(OQ-4-8 已裁单值,取值归数值轮)。
//   本类型只签发「存在唯一装载常量 + 区间校验」的形状(4-DC-1 的区间校验本体归 story 006)。
//
// ⚠️ 无状态:r_interact 是 readonly 实例字段(装载后不变),无 static 可变状态。

using System;

namespace DaYiJingCheng.Gameplay.Interaction
{
    /// <summary>
    /// 交互半径 <c>R_INTERACT</c> 的**单一装载常量**持有者(AC-4-17 / F-4.4)。
    /// <para>4 的选择(邻域裁剪)与 6 的「已发现」触发**共用本值** —— 单源,禁二次声明。</para>
    /// <para>⚠️ <b>值归用户</b>(<c>OQ-4-8</c> 已裁单值);本类型只登记「唯一来源 + 区间形状」。</para>
    /// </summary>
    /// <remarks>
    /// <para><b>装载</b>:经 ADR-014 <c>IDataProvider</c> 从 <c>interaction_kinds.json</c> 一次读入
    /// (<c>r_interact</c> 字段)。本故事以注入值驱动测试;真装载归 story 006。</para>
    /// <para><b>4-DC-1</b>:<c>1 ≤ r_interact ≤ min(W,H,D) − 1</c> —— 下界在本类型构造期硬失败,
    /// 上界须 <c>W/H/D</c> 在场(校验本体归 story 006)。</para>
    /// <para><b>无状态</b>(AC-4-04 同族):本值装载后不变 —— <b>不是</b>「每帧重读的旋钮」。</para>
    /// </remarks>
    public sealed class InteractionRadius
    {
        /// <summary>半径的整数值(整数格数;<c>4-DC-1</c> 下界已守)。</summary>
        public int Value { get; }

        /// <summary>构造半径持有者。<b>下界硬失败</b>(4-DC-1:恒不可交互的 <c>r_interact &lt; 1</c> 装载期即拒)。</summary>
        /// <param name="rInteract">来自烘焙字段 <c>r_interact</c> 的整数值。</param>
        public InteractionRadius(int rInteract)
        {
            // ⚠️ 4-DC-1 下界:原稿只查 ≥ 1 —— 本类型在**构造期**即拒,不等运行期。
            if (rInteract < 1)
                throw new ArgumentOutOfRangeException(nameof(rInteract),
                    "4-DC-1:r_interact ≥ 1(恒不可交互的半径装载期硬失败)");
            Value = rInteract;
        }
    }
}
