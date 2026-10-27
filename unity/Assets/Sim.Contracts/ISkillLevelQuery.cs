// S20-4 · processing story-001 —— 技能等级**只读**查询接缝(AC-18-05 / AC-18-10 的等级输入)
//
// 权威来源:
//   design/gdd/processing.md F-18.2(:440)`QueryLevel(player, CRAFT_SKILL) ≥ recipe.skill_gate`
//   ADR-026 §⑤ —— 档位 / 解锁判定走**整数等级比较**(G-3)
//   ADR-025 §① —— 住 `Sim.Contracts`,引用集恰 = BCL
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么不直接复用 `ISkillGrowthPort`
// ─────────────────────────────────────────────────────────────────────────────
// `ISkillGrowthPort`(`Sim/Prescription/PrescribeFlow.cs:99`)除 `QueryLevel` 外还有
// **成长入口** `EmitGrowth(...)`。准入谓词只需要读,给它一个能写成长的句柄,会让
// 「求值谓词」变成「可能推进技能」—— 而谓词每 tick 可能被求值多次,
// 带副作用的实现使「同输入不同答案」成为可表达状态(破 ADR-016 §一 三源不变量)。
//
// ⇒ 按**接口隔离**拆一个只读面。
//
// ⚠️ **2026-10-27 评审 AR2 订正 —— 本接口不是「另立一份 QueryLevel」,而是唯一声明处**:
//   初版写法是「另立接口 + 将来在 Boot 写一个适配器把 `ISkillGrowthPort` 包进来」。
//   那样 `QueryLevel` 会有**两处逐字相同的声明**(本接口 + `ISkillGrowthPort`),适配器成为
//   第三个必须与之保持一致的东西 —— 隔离成立了,但代价是三条可各自漂移的读路径。
//   现改为 **`ISkillGrowthPort : ISkillLevelQuery`**(`Sim/Prescription/PrescribeFlow.cs`):
//   ① 本接口 = `QueryLevel` 的**唯一声明处**;
//   ② 任何 `ISkillGrowthPort` 实现**自动**满足 18 的只读需求 ⇒ **适配器不需要写**;
//   ③ 「等级只有一个读面」从纪律升为**编译期事实**。
//   隔离方向未变:谓词只收本接口,拿不到 `EmitGrowth`。
//
// ⚠️ 本接口**不定义**等级值域 —— `SKILL_CAP` 是 30 的所有物,本系统不重述。
//    越界值由 30 侧保证不出现;本接口照实返回。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>技能等级只读查询(30 的 <c>QueryLevel</c> 的隔离读面)。</summary>
    /// <remarks>
    /// <para><b>纯查询</b>:实现方不得在本方法内推进任何状态(含惰性缓存的时间戳)。
    /// 与 <see cref="ISingleFurnaceQuery"/> 同一纪律。</para>
    /// </remarks>
    public interface ISkillLevelQuery
    {
        /// <summary>查某玩家某技能的当前等级。</summary>
        /// <param name="actorId">玩家 id。</param>
        /// <param name="skillId">技能 id(取 <c>SkillId</c> 枚举值)。</param>
        /// <returns>等级(整数;P0 只读)。</returns>
        int QueryLevel(int actorId, int skillId);
    }
}
