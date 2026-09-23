// 权威来源:design/gdd/item-database.md §Formulas 节首「权威边界(Q1 裁决)」:
//          「17 采集 / 18 炮制 / 19 制作只产出输入参数,不得自建结算逻辑」
//          · AC-21a-5 [I](18 与 19 走**同一个函数** —— 反射断言两入口收敛到同一 MethodInfo)
//          · AC-21a-6 [I](17/18/19 不含任何重复结算实现)
//          · ADR-005(纯函数、可重放)· ADR-025 §①(`Sim` 装配)
//
// ⚠️ **本文件是本故事的「入口接缝」**(18 / 19 的系统程序集尚未落地):
//    两个入口类**只有构造参数 + 委托调用**,零公式体 —— 它们的存在使 AC-21a-5 的
//    「两入口收敛到同一 MethodInfo」在 18/19 程序集出现**之前**就可被断言。
//    18/19 落地时**只允许**改成「从各自数据源取输入 → 调本入口」,
//    绝不允许把 `RecipeSettlementSolver` 的任何具名量复制进各自程序集。
//
// ⚠️ 本文件**故意不含** QtyMultiplier / Retain( / Ceil / EnvMod_total / OutputQty /
//    ActualConsumed / ΣM 的任何形态 —— AC-21a-6 的静态扫描把它当 18/19 侧对待
//    (扫描器只白名单 `RecipeSettlementSolver.cs` 一个文件)。改本文件时若引入公式体,
//    `recipe_settlement_solver_integration_test.cs` 的 AC-21a-6 扫描立刻变红。

namespace DaYiJingCheng.Sim
{
    /// <summary>18 炮制侧的结算**入口接缝**。
    /// <para>职责 = 只把该系统的输入装配成 <see cref="RecipeSettlementRequest"/> 并调用
    /// <see cref="RecipeSettlementSolver"/>。配方子集划分(<c>owner = process</c>,D-21-30)
    /// 由 18 在**构造参数之前**完成 —— 公式体不随子集分叉。</para>
    /// <para><b>零公式体</b>:本类不得出现任何 F1/F2 具名量的实现(AC-21a-6)。</para>
    /// </summary>
    public static class ProcessingSettlementEntry
    {
        /// <summary>本入口委托的唯一求解方法 —— 与
        /// <see cref="CraftingSettlementEntry.Solver"/> **指向同一 MethodInfo**(AC-21a-5 断言面)。</summary>
        /// <example><c>ProcessingSettlementEntry.Solver.Method
        /// == typeof(RecipeSettlementSolver).GetMethod("Solve")</c> ⇒ <c>true</c>。</example>
        public static readonly RecipeSettlementFn Solver = RecipeSettlementSolver.Solve;

        /// <summary>18 侧结算入口 —— 只转调唯一求解器。</summary>
        /// <param name="request">结算输入(纯数据)。</param>
        /// <param name="constants">常量表(纯数据)。</param>
        /// <returns>结算出参(与 19 侧同参数集**逐位相同**)。</returns>
        /// <example><c>ProcessingSettlementEntry.Solve(request, constants)</c></example>
        public static RecipeSettlementResult Solve(
            in RecipeSettlementRequest request, in RecipeSettlementConstants constants)
            => RecipeSettlementSolver.Solve(request, constants);
    }

    /// <summary>19 制作侧的结算**入口接缝**(与 <see cref="ProcessingSettlementEntry"/> 同构)。
    /// <para>职责 = 只把该系统的输入装配成 <see cref="RecipeSettlementRequest"/> 并调用
    /// <see cref="RecipeSettlementSolver"/>。配方子集划分(<c>owner = craft</c>,D-21-30)
    /// 由 19 在**构造参数之前**完成 —— 公式体不随子集分叉。</para>
    /// <para><b>零公式体</b>:本类不得出现任何 F1/F2 具名量的实现(AC-21a-6)。</para>
    /// </summary>
    public static class CraftingSettlementEntry
    {
        /// <summary>本入口委托的唯一求解方法 —— 与
        /// <see cref="ProcessingSettlementEntry.Solver"/> **指向同一 MethodInfo**(AC-21a-5 断言面)。</summary>
        /// <example><c>CraftingSettlementEntry.Solver.Method
        /// == typeof(RecipeSettlementSolver).GetMethod("Solve")</c> ⇒ <c>true</c>。</example>
        public static readonly RecipeSettlementFn Solver = RecipeSettlementSolver.Solve;

        /// <summary>19 侧结算入口 —— 只转调唯一求解器。</summary>
        /// <param name="request">结算输入(纯数据)。</param>
        /// <param name="constants">常量表(纯数据)。</param>
        /// <returns>结算出参(与 18 侧同参数集**逐位相同**)。</returns>
        /// <example><c>CraftingSettlementEntry.Solve(request, constants)</c></example>
        public static RecipeSettlementResult Solve(
            in RecipeSettlementRequest request, in RecipeSettlementConstants constants)
            => RecipeSettlementSolver.Solve(request, constants);
    }
}
