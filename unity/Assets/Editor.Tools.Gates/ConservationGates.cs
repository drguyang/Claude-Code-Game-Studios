// 权威来源:Story 005(production/epics/item-database/story-005-conservation-law-gates.md)
//   · AC-21a-8  —— 聚合守恒上界:QTY_MULT_MAX × Σ(w × 产出基数) > EFF_MAX × Σ(w × 输入基数) ⇒ 拒;
//                  「EFF_MAX > 1 亦拒」在本条文本内(交叉 AC-21a-40)⇒ 本文件**委托**并合并其错误列表
//   · AC-21a-40 —— EFF_MAX > 1 的任何常量表 ⇒ 构建期硬失败
//   · AC-21a-56 —— EFF_MIN ≤ 0 或 EFF_MIN > EFF_MAX ⇒ 构建期硬失败(EFF 是 F1 除数,下端此前无人守)
//   · AC-21a-65 —— 逐条同形极值式(唯一硬门;D-21-32)
//   GDD:design/gdd/item-database.md §Core Rules 规则八(:202-228)· §Formulas F1 守恒块(:530-552)
//        · §Tuning Knobs(:897-957)—— EFF_* / QTY_MULT_* 行即本条两条范围门的出处
//   ADR-006 §Decision 四(守恒律整数域内求值)· §Decision 三(单一舍入 ROUND_HALF_AWAY_FROM_ZERO)
//   ADR-014 §三(逐 schema 已知键白名单 / 硬失败归烘焙管线 Story 008 —— 本文件只供可复用纯函数)
//   ADR-025 §①(Editor.Tools 族 = L6,UnityEditor 自由,不进构建)
//
// ⚠️ 落点(unity-specialist 约束①,2026-09-23;本文件承 Story 004 的 DrugProfileGates 先例):
//    构建期校验纯函数住 Editor.Tools.Gates,**不**住 Sim —— 编辑期职责(008 烘焙期校验),
//    不进玩家构建。运行期算术原语住 Sim(ConservationSolver)。
// ⚠️ 执法体统一形态 = **错误列表(空 = 通过)**(与同目录 DrugProfileGates / ItemDbValidation 同形);
//    「硬失败 throw」由调用方(008 烘焙管线)聚合非空列表后执行 —— 本文件是纯函数,无 I/O、无静态可变态。
// ⚠️ **单一实现(本文件的承重纪律)**:四条门**不复制任何算术** —— 判据一律由
//    ConservationSolver 的谓词裁定(逐条极值式更直接消费运行期的 OutputQty / ActualConsumed),
//    本文件只在谓词为 false 时枚举违规以**具名诊断**。故「构建期门与运行期不同形」在结构上不可能。
// ⚠️ **零数值**:EFF_* / QTY_MULT_* 一律经 RecipeSettlementConstants 的 **PascalCase 属性**读取
//    (AC-21a-48)—— 本文件不含任何旋钮字面量,亦不声明任何 const / static readonly 数值。
// ⚠️ 范围锁:本文件**不**做容器闭包(Story 010)、**不**做 QTY_MULT 下界与 cap 和(AC-21a-9,Story 006)、
//    **不**做烘焙管线接线(Story 008)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>规则八守恒律的构建期校验纯函数(AC-21a-8 / 40 / 56 / 65 的执行体)。
    /// <para>全部方法:纯函数 · 无 I/O · 无静态可变态 · 返回错误列表(空 = 通过)——
    /// 供 Story 008 两阶段烘焙管线复用(聚合非空列表 ⇒ 构建失败,ADR-014 §三)。</para>
    /// <para>⚠️ 判据**全部委托** <see cref="ConservationSolver"/> —— 本文件不复制公式体。
    /// 尤其 <see cref="ValidatePerLineExtremeConservation"/> 的通过/失败与运行期结算
    /// **逐位同形**(消费同一份 <c>OutputQty</c> / <c>ActualConsumed</c>)。</para>
    /// </summary>
    public static class ConservationGates
    {
        // ══════════ AC-21a-40:EFF_MAX ≤ 1 ══════════

        /// <summary>校验常量表 <c>EFF_MAX ≤ 1</c>(AC-21a-40 · GDD §Tuning Knobs <c>EFF_MAX</c> 行)。
        /// <para><c>EFF_MAX &gt; 1</c> 是「凭空造物」的边界,**不是平衡旋钮** —— 它是守恒律的承重前提,
        /// 不是可调项(ADR-006 §Decision 四明文「构建期硬失败」)。</para>
        /// <para>恰 = 1(零损耗档)与 &lt; 1 均**过**;恰 1 是满技能取等号的唯一合法上端。</para>
        /// </summary>
        /// <param name="constants">待校验常量表(值经 PascalCase 属性读;本文件不承载数值)。</param>
        /// <param name="recordLabel">诊断标签(如常量表名;可省)。</param>
        /// <returns>错误列表;空 = <c>EFF_MAX ≤ 1</c>。</returns>
        /// <example><c>ValidateEffMaxRange(one)</c> ⇒ 空(EFF_MAX = 1);
        /// <c>ValidateEffMaxRange(overOne)</c> ⇒ 1 条错误(点名 raw 值)。</example>
        public static IReadOnlyList<string> ValidateEffMaxRange(
            in RecipeSettlementConstants constants, string recordLabel = "")
        {
            if (constants.EffMax.Raw <= Fix.OneRaw)
                return Array.Empty<string>();

            return new[]
            {
                $"{RecordPrefix(recordLabel)} 转化效率上限 raw = {constants.EffMax.Raw} > 1.0 raw = " +
                $"{Fix.OneRaw} —— 上限 > 1 是「凭空造物」的边界,不是平衡旋钮,构建期硬失败(AC-21a-40 / " +
                "ADR-006 §Decision 四);它同时使守恒律的右端收缩前提失效",
            };
        }

        // ══════════ AC-21a-56:EFF_MIN 的下端与序关系 ══════════

        /// <summary>校验常量表 <c>EFF_MIN</c> 的下端与序关系(<c>0 &lt; EFF_MIN ≤ EFF_MAX</c>,AC-21a-56)。
        /// <para>两种违反,各具名一条(<b>可返回 2 条</b>):</para>
        /// <list type="number">
        /// <item><c>EFF_MIN ≤ 0</c> —— EFF 是 F1 的除数,0 ⇒ 运行期除零,负 ⇒ 实耗为负(凭空造料);</item>
        /// <item><c>EFF_MIN &gt; EFF_MAX</c> —— 区间反向,插值曲线出域。</item>
        /// </list>
        /// <para>⚠️ <b>不擅自收紧</b>:GDD 现文只拒 <c>&gt;</c> ⇒ <c>EFF_MIN = EFF_MAX &gt; 0</c>
        /// 退化为点集**按现文过**(与 AC-21a-38b 只断言 <c>&gt; 0</c> 同一纪律 —— 收紧 = 改机制)。</para>
        /// </summary>
        /// <param name="constants">待校验常量表。</param>
        /// <param name="recordLabel">诊断标签(可省)。</param>
        /// <returns>错误列表;空 = <c>0 &lt; EFF_MIN ≤ EFF_MAX</c>。</returns>
        /// <example><c>ValidateEffMinRange(zero)</c> ⇒ 1 条;<c>ValidateEffMinRange(bothBad)</c> ⇒ 2 条。</example>
        public static IReadOnlyList<string> ValidateEffMinRange(
            in RecipeSettlementConstants constants, string recordLabel = "")
        {
            var errors = new List<string>();

            if (constants.EffMin.Raw <= 0L)
            {
                errors.Add(
                    $"{RecordPrefix(recordLabel)} 转化效率下限 raw = {constants.EffMin.Raw} ≤ 0 —— " +
                    "EFF 是 F1 的除数,0 除零、负值 ⇒ 实耗为负(凭空造料);构建期硬失败" +
                    "(AC-21a-56 / GDD §Tuning Knobs:EFF_MIN 下界不含 0)");
            }

            if (constants.EffMin.Raw > constants.EffMax.Raw)
            {
                errors.Add(
                    $"{RecordPrefix(recordLabel)} 转化效率下限 raw = {constants.EffMin.Raw} > 上限 raw = " +
                    $"{constants.EffMax.Raw} —— 区间反向,插值曲线出域;构建期硬失败(AC-21a-56)");
            }

            return errors;
        }

        // ══════════ AC-21a-8:聚合守恒上界 ══════════

        /// <summary>构建期**聚合**守恒上界校验(AC-21a-8 · GDD §Formulas F1 :530-533)。
        /// <code>
        /// QTY_MULT_MAX × Σ( weight × outputs_i.qty )  ≤  EFF_MAX × Σ( weight × inputs_j.qty )
        /// </code>
        /// <para>⚠️ AC-8 文本含「<b>EFF_MAX &gt; 1 亦拒</b>」⇒ 本方法**委托**
        /// <see cref="ValidateEffMaxRange"/> 并把其错误列表**合并**进返回值(不重复实现该判据)。</para>
        /// <para>⚠️ 本式是**必要非充分**(D-21-32)⇒ 违反必拒,但通过**不等于**安全;
        /// 唯一硬门是 <see cref="ValidatePerLineExtremeConservation"/>。</para>
        /// <para>判据本体由 <see cref="ConservationSolver.AggregateUpperBoundHolds"/> 单一实现裁定
        /// (整数域先乘后比;溢出 ⇒ 前提未证 ⇒ 拒,不回绕)。</para>
        /// </summary>
        /// <param name="outputs">产出项(基数;非 null、非空)。</param>
        /// <param name="inputs">投入项(基数;非 null、非空)。</param>
        /// <param name="itemWeight">权重查表**纯函数**(int ≥ 1;非 null)。</param>
        /// <param name="constants">常量表(用其乘子属性)。</param>
        /// <param name="recordLabel">诊断标签(如 recipe_id;可省)。</param>
        /// <returns>错误列表;空 = 聚合式成立且上限合法。</returns>
        /// <example>GDD :535 反例一 ⇒ 含一条点名 18 &gt; 10 的错误。</example>
        public static IReadOnlyList<string> ValidateAggregateConservation(
            RecipeEntry[] outputs,
            RecipeEntry[] inputs,
            Func<ItemKey, int> itemWeight,
            in RecipeSettlementConstants constants,
            string recordLabel = "")
        {
            // AC-8 文本含「EFF_MAX > 1 亦拒」—— 委托交叉门并合并其列表(单一实现,不重写)。
            var errors = new List<string>(ValidateEffMaxRange(constants, recordLabel));

            if (ConservationSolver.AggregateUpperBoundHolds(
                    outputs, inputs, itemWeight, constants.EffMax, constants.QtyMultMax))
                return errors;

            errors.Add(
                $"{RecordPrefix(recordLabel)} 聚合守恒上界被违反 —— " +
                $"QTY_MULT_MAX raw = {constants.QtyMultMax.Raw} × Σ(w × 产出基数) > " +
                $"EFF_MAX raw = {constants.EffMax.Raw} × Σ(w × 输入基数)" +
                "(同量纲「重量」求值,整数域先乘后比);后果 = 运行期凭空造物(AC-21a-8 / D-21-21)。" +
                "⚠️ 本式必要非充分 —— 通过不等于安全,唯一硬门是逐条同形极值式(AC-21a-65 / D-21-32)");
            return errors;
        }

        // ══════════ AC-21a-65:逐条同形极值式(唯一硬门)══════════

        /// <summary><b>唯一硬门</b> —— 构建期**逐条同形极值式**校验(AC-21a-65 · D-21-32)。
        /// <code>
        /// Σ( weight_out × max(1, Round(outputs_i.qty × QTY_MULT_MAX)) )
        ///     ≤ EFF_MAX × Σ( weight_in × Ceil(inputs_j.qty / EFF_MAX) )
        /// </code>
        /// <para>⚠️ <b>同形 = 同一实现</b>:判据由
        /// <see cref="ConservationSolver.PerLineExtremeBoundHolds"/> 裁定,而它**直接调用**
        /// 运行期结算的 <c>OutputQty</c> / <c>ActualConsumed</c> —— 缝(在 <c>Round</c> 不在
        /// <c>max(1,·)</c>,GDD :543)由结构消除,不靠「两处抄一致」。</para>
        /// <para>⚠️ 前提不可用时**不进入**算术:常量表 <c>EFF_MAX ≤ 0</c> 会使投入侧上取整除零
        /// (运行期函数对 EFF ≤ 0 显式抛)⇒ 本方法改为**具名前提错误**返回
        /// (EFF ≤ 0 的域归属 = AC-21a-40 / 56,不在此重复判定)。</para>
        /// </summary>
        /// <param name="outputs">产出项(基数)。</param>
        /// <param name="inputs">投入项(基数)。</param>
        /// <param name="itemWeight">权重查表**纯函数**(int ≥ 1;非 null)。</param>
        /// <param name="constants">常量表(用其乘子属性)。</param>
        /// <param name="recordLabel">诊断标签(如 recipe_id;可省)。</param>
        /// <returns>错误列表;空 = 极值式成立且前提可用。</returns>
        /// <example>GDD :540 反例二(聚合式过、极值式击穿)⇒ 恰一条点名 <c>Round(1 × 1.5) = 2</c> 的错误。</example>
        public static IReadOnlyList<string> ValidatePerLineExtremeConservation(
            RecipeEntry[] outputs,
            RecipeEntry[] inputs,
            Func<ItemKey, int> itemWeight,
            in RecipeSettlementConstants constants,
            string recordLabel = "")
        {
            // 前提不可用(域外)⇒ 具名返回,不把异常/误诊漏出去。
            if (constants.EffMax.Raw <= 0L)
            {
                return new[]
                {
                    $"{RecordPrefix(recordLabel)} 逐条极值式前提不可用:转化效率上限 raw = " +
                    $"{constants.EffMax.Raw} ≤ 0 —— 投入侧上取整的除数非正;" +
                    "该域归属 AC-21a-40 / AC-21a-56,本门不代判,亦不进入算术(AC-21a-65)",
                };
            }

            if (constants.QtyMultMax.Raw <= 0L)
            {
                return new[]
                {
                    $"{RecordPrefix(recordLabel)} 逐条极值式前提不可用:产出乘子上限 raw = " +
                    $"{constants.QtyMultMax.Raw} ≤ 0 —— 产出侧取整无上界可言;" +
                    "该域归属 Story 006 的 QTY_MULT 范围门(AC-21a-9),本门不代判(AC-21a-65)",
                };
            }

            if (ConservationSolver.PerLineExtremeBoundHolds(
                    outputs, inputs, itemWeight, constants.EffMax, constants.QtyMultMax))
                return Array.Empty<string>();

            return new[]
            {
                $"{RecordPrefix(recordLabel)} 逐条同形极值式被击穿 —— " +
                $"Σ(w_out × max(1, Round(qty × QTY_MULT_MAX raw = {constants.QtyMultMax.Raw}))) > " +
                $"EFF_MAX raw = {constants.EffMax.Raw} × Σ(w_in × Ceil(qty / EFF_MAX));" +
                "这是**唯一硬门**(D-21-32):聚合式可能与运行期逐条取整不同形而给出假绿," +
                "本式与运行期同一实现,故违反即硬失败(AC-21a-65)",
            };
        }

        // ══════════ 共用小件 ══════════

        /// <summary>诊断前缀:无标签时用「记录」,有标签时用「记录(标签)」(与 DrugProfileGates 同形)。</summary>
        private static string RecordPrefix(string recordLabel)
            => string.IsNullOrEmpty(recordLabel) ? "记录" : $"记录({recordLabel})";
    }
}
