// ============================================================================
// 死亡掉级 —— Level_new(s) = ( Level(s) × 19 ) / 20(C# int 截断除法)
// 权威来源: design/gdd/death-and-respawn.md F-29-1(2026-09-19 修订 · 承首轮阻断 B2)
//   · ADR-026 §Decision 四(调参表 Fix 字段) + Implementation Guidelines
//   · ADR-005 确定性 sim(纯函数,无随机)
//   · production/epics/skill-system/story-005-death-penalty.md(30 侧承载)
//   · production/epics/death-respawn/story-003(29 消费面,S23-3)
// ============================================================================
// ⚠️ **为什么是整数截断除法,不是定点乘法**(F-29-1 的定点陷阱,勿「顺手定点化」):
//   `FixParse("19/20") = 62259`(Q16.16,62259.2 量化丢低位),则
//   `20 × 62259 >> 16 = 18`,而表值要求 **19**(40→38、60→57 同理各差一)。
//   `floor_Fix(Mul)` 先量化再 floor,与 `(L×19)/20` **不等价**;
//   `Fix.Div` 的 ROUND_HALF_AWAY_FROM_ZERO 会把 10→9.5 舍成 10,反向错误。
//   ⇒ `Level` 是 int,直接走整数截断除法,**不进入定点乘法域**。
//   (原 2026-10 之前的 Fix 路径实现 = GDD 已登记的「同源同错」,2026-11-01 订正;
//   `skill-system.md` §4.3 公式行文本订正仍挂 30 下一轮 —— 见其 §4.3 同日注。)
// ============================================================================
// ⚠️ **19/20 是整数比,不是 Fix 域量** —— 与 `SkillTuningTable.Default.DeathLoss`
//   (= FixParse("19/20"),同一比值的 Q16.16 近似形)**同源对应**:数值轮改其一
//   必须同批改另一(DeathLoss 是近似形,本常量对是精确形;精确形才是求值依据)。
// SkillGrown 事件触发由调用方负责(本文件只定义「掉级后触发」的语义,
// 具体批量/逐 tick 由 29 死亡检测编排层裁定 —— S23-3 落地于 DeathSkillDownSettlement)。
// ============================================================================

using System;

namespace DaYiJingCheng.Sim.Contracts.SkillSystem
{
    /// <summary>
    /// 死亡掉级纯函数(F-29-1 权威式:整数截断除法)。
    /// <para>公式: <c>Level_new(s) = (Level(s) × 19) / 20</c> —— C# <c>int /</c> 向零截断
    /// (等级非负 ⇒ 等价 floor)。</para>
    /// <para>允许掉到 0(不设下限保护)。0 级技能仍可执行(无加成但不禁止)。</para>
    /// </summary>
    public static class DeathPenalty
    {
        /// <summary>掉级保留比的分子(19/20 = 0.95 的精确整数比;F-29-1)。
        /// ⚠️ 与 <see cref="SkillTuningTable"/>.Default.DeathLoss 同源,改须同批。</summary>
        public const int LossNum = 19;

        /// <summary>掉级保留比的分母(见 <see cref="LossNum"/>)。</summary>
        public const int LossDen = 20;

        /// <summary>应用死亡掉级惩罚,返回新等级(int)。
        /// 调用方负责在掉级后触发 SkillGrown 事件(Story 007 实现机制)。</summary>
        /// <param name="currentLevel">当前技能等级(0–60)。</param>
        /// <returns>掉级后的新等级(int,整数截断,允许为 0)。</returns>
        /// <exception cref="ArgumentOutOfRangeException">currentLevel 为负时抛出。</exception>
        public static int Apply(int currentLevel)
        {
            if (currentLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(currentLevel),
                    $"currentLevel={currentLevel} 不能为负");

            // F-29-1:int 截断除法(等级非负 ⇒ 向零截断 = floor)。禁定点路径 —— 见文件头。
            return currentLevel * LossNum / LossDen;
        }
    }
}
