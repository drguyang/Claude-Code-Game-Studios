// `slopeLimit` / `stepOffset` 的**整数量化约定** —— 关卡工具与运行期读侧共用的唯一出处。
//
// 权威来源:
//   ADR-022 §四 **C2**(`|engineParam − Dequantize(bakedInt)| ≤ 1 量子`)· ADR-015 §一之补
//     (`slopeLimit` / `stepOffset` 的**真源 = 关卡工具**,系统 1 只是执行者)
//   `production/epics/level-tool/story-002-world-geometry-and-terrain.md` §Implementation Notes
//     (「量化函数本身必须在本 story 定义 —— 否则 S6 无对象可比。它归 `Sim.Contracts`(整数域)
//      还是工具侧(编辑期)?本 story 须明确并登记」)
//   ADR-015 §三(逻辑层只有整数格)· ADR-006 §Decision 一(边界上禁浮点)
//
// ═══════════════════════════════════════════════════════════════════════════
// 本 story 的裁定:量化约定住 `Sim.Contracts`,但**只定整数侧的量子**
// ═══════════════════════════════════════════════════════════════════════════
// 故事卡把「归 `Sim.Contracts` 还是归工具侧」留给了实现轮。本轮的取法与理由:
//
//   · **必须两侧都看得见** ⇒ 只能住 `Sim.Contracts`:`Sim` 的引用集恰 = {BCL, `Sim.Contracts`}
//     (ADR-025 §① · b2 门),而 `Gameplay.Presentation` 的 codec 也要读它。工具侧
//     (`Editor.Tools.Level`)在**编辑期**需要它做作者期量程校验,而它**不进构建**(ADR-022 §①)。
//   · **只定整数侧,不提供 float 反量化函数**。理由不是偷懒:C2 的比对(`|engine − int/量子| ≤ 1 量子`)
//     发生在 **S6 的编辑期一致性检查**里,那里 float 合法;而 sim 侧**永远**只持整数
//     (ADR-015 §三)。在 `Sim.Contracts` 里放一个 `ToDegrees(int) => float` 等于给整数域开一个
//     浮点出口,而它的**唯一**消费者今天不存在(S6 尚未实现)。
//     ⚠️ 本仓的既有教训(2026-10-28 评审 A-21 与 story-001 实现轮 ④)逐字适用:
//     「『S6 将来要用』不是现在留下一个零消费方 API 的理由」。
//     ⇒ 单位关系用**整数常量**表达(`1 度 = 100 单位`),S6 自己写那一行除法即可,
//       并且它必须引用这些常量而不是另抄字面量。
//
//   ⚠️ **数值不在这里。** `slopeLimit` 取 45° 还是 30°、`stepOffset` 取 0.3 m 还是 0.5 m,
//      都是**用户数值轮**的事(ADR-022 §Out of Scope)。本类只把「一个整数单位等于多少度 / 米」
//      钉死 —— 那是**量纲**约定,不是设计取值。

using System;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>格点参数(`slopeLimit` / `stepOffset`)的整数量化约定。</summary>
    /// <remarks>
    /// <para>两个参数在引擎侧都是 `CharacterController` 的 <c>float</c> 属性(AC-0 spike 实测:
    /// <c>slopeLimit</c> 默认 45,单位**度**;<c>stepOffset</c> 默认 0.3,单位**米**)。
    /// 逻辑层只有整数 ⇒ 必须先定一个量子,否则 C2 的比对没有对象。</para>
    /// <para>量子取法:<c>slopeLimit</c> = **0.01 度**(45.00° ⇒ 4500)· <c>stepOffset</c> = **1 毫米**
    /// (0.300 m ⇒ 300)。后者与 `LATTICE_SIZE` 同量子(ADR-015 §三:格边长以**整数毫米**表达)——
    /// 「台阶高度」与「格边长」在同一个长度量纲上比较,同量子才不会引入第二种长度单位。</para>
    /// </remarks>
    public static class LatticeParamQuantization
    {
        /// <summary>1 度 = 多少个 `slopeLimit` 量化单位(量子 = 0.01 度)。</summary>
        public const int SlopeLimitUnitsPerDegree = 100;

        /// <summary>1 米 = 多少个 `stepOffset` 量化单位(量子 = 1 毫米)。</summary>
        public const int StepOffsetUnitsPerMeter = 1000;

        /// <summary>`slopeLimit` 的合法上界(量化单位;含 90°)。</summary>
        /// <remarks>斜率 ≤ 90° 是几何事实;超过它不是「更陡」而是**无意义**。
        /// 下界 = 1 单位(0° 表示「完全不能上坡」,与 `walkable = false` 是两件事,但仍须显式声明)。</remarks>
        public const int SlopeLimitMaxUnits = 90 * SlopeLimitUnitsPerDegree;

        /// <summary>`stepOffset` 的合法上界(毫米;10 m)。</summary>
        /// <remarks>⚠️ 这个上界**不是设计值**,只是「量级离谱」的守卫:超过 10 m 的台阶高度
        /// 已不是台阶而是地形落差,写进来必然是单位写错(如按厘米填了 30)。
        /// 真实取值归用户数值轮。</remarks>
        public const int StepOffsetMaxUnits = 10 * StepOffsetUnitsPerMeter;

        /// <summary>`slopeLimit` 量化值是否在合法域 `(0, 9000]` 内。</summary>
        /// <param name="units">量化单位(0.01 度)。</param>
        public static bool IsValidSlopeLimit(int units)
            => units > 0 && units <= SlopeLimitMaxUnits;

        /// <summary>`stepOffset` 量化值是否在合法域 `[0, 10000]` 内。</summary>
        /// <param name="units">量化单位(毫米)。</param>
        /// <remarks>下界含 0:「无台阶容差」是合法的控制器配置。</remarks>
        public static bool IsValidStepOffset(int units)
            => units >= 0 && units <= StepOffsetMaxUnits;

        /// <summary>
        /// `|engineParam − Dequantize(bakedInt)| ≤ 1 量子` 的**整数形式**判据(C2 的语义核心)。
        /// </summary>
        /// <param name="engineUnits">引擎侧参数**换算到同一量子后的**值。</param>
        /// <param name="bakedUnits">烘焙产物里的整数。</param>
        /// <returns>偏差是否不超过 1 个量子。</returns>
        /// <remarks>
        /// ⚠️ 本方法**不收 float**(那会把浮点带进 `Sim.Contracts`)。调用方(S6)负责把引擎侧
        /// `slopeLimit` / `stepOffset` 乘上 <see cref="SlopeLimitUnitsPerDegree"/> /
        /// <see cref="StepOffsetUnitsPerMeter"/> 并**就近取整**成整数,再调本方法 ——
        /// 取整方式属 S6 的判据细节,不在本条约定内。
        /// <para>⚠️ 本方法今天**零生产消费者**(S6 未实现)。它是**约定的执行形**,
        /// 由本 story 的夹具钉住(S6 落地时直接引用,不另抄一份偏差公式)。
        /// ⇒ **如实登记**:它不是死 API —— 它是「约定」这个抽象的唯一可执行表述,
        /// 而约定本身是 AC-2 / C2 的前置。若 S6 落地时发现取整口径不同,改这里一处。</para>
        /// </remarks>
        public static bool IsWithinOneQuantum(int engineUnits, int bakedUnits)
            => Math.Abs((long)engineUnits - bakedUnits) <= 1L;
    }
}
