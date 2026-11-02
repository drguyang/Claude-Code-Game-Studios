// `O-18-R6` 的**接口半边** —— 18 → 42 的只读准入接缝(`AC-18-10b` 的数据面)。
//
// 权威来源(逐字):
//   design/gdd/processing.md
//     · `:403`  「**回 42/18 接缝的义务 `O-18-R6`**(新立「可准入清单」只读 DTO;
//                归 42 屏清单轮,与 `OQ-42-*` 同批)」
//     · `:664`  UI-18.3「P0 **无独立「炮制面板」screen** —— 选料复用 **20 出诊箱容器**;
//                呈现落点 = **42 的屏闭集须补入炮制交互**(O-18-R6 接缝,与 11 方笺并批)」
//     · `:732`  AC-18-10b「GIVEN 不可发起的配方,WHEN 走查面板,THEN **不出现(非灰置)**」
//     · `:517`  「42 拟物 UI … **准入谓词的只读接口 `O-18-R6`**」
//   design/gdd/skeuomorphic-ui.md `:1269` —— 42 侧认领该缺口的同一行。
//   ADR-025 §① —— 住 `Sim.Contracts`,引用集恰 = BCL。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么需要这个接口(而不是让 42 直接调 `AdmissiblePredicate`)
// ─────────────────────────────────────────────────────────────────────────────
// `AdmissiblePredicate` / `ProcessingCandidateSet` 住 **`Sim`** 程序集,
// 而 `Gameplay.UI` 的引用集 = `{Sim.Contracts, Gameplay.Presentation}`
// (`Gameplay.UI.asmdef`)—— **它物理上看不见 `Sim`**。
// ⇒ 「18 提供只读准入谓词」在类型面上**今天不存在**(这正是 `O-18-R6` 未回写的实质,
//    而不只是「42 没认领」)。
//
// ⚠️ **本接口不是第二判据**:实现方(住装配层)只把
// `ProcessingCandidateSet.IsCandidate` / `AdmissiblePredicate.PreviewVerdict` **包一层**。
// 接口里没有任何新算术 —— 一旦实现方自己写 `skillLevel >= skill_gate`,
// 就成了 AC-18-20 明令禁止的第二实现。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么**不**在这里放「配方表」
// ─────────────────────────────────────────────────────────────────────────────
// 配方表(`Recipe[]`)的装载归 21a 的 cooked 数据集,经 `IDataProvider`。
// 本接口只回答「**这一个**配方此刻对**这个**玩家可不可发起」——
// 把配方表也塞进来会让「谁拥有配方数据」出现第二个答案(ADR-014 的装载面已定)。
//
// ⚠️ 本接口**不声明**「可发起」的判据内容(那是 `Admissible ∧ owner=process ∧ 单炉空闲`,
//    AC-18-10 逐字)—— 实现方照抄 `ProcessingCandidateSet.IsCandidate` 即可,
//    本接口不重述、不覆盖(承 `ISingleFurnaceQuery` 的同款纪律)。
//
// ⚠️ **零引擎引用**:本文件只依赖 BCL + `Sim.Contracts` 自身的 `Recipe` / `ISkillLevelQuery`。

using System.Collections.Generic;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 炮制准入的**只读查询接缝**(`O-18-R6` · `AC-18-10b` 的数据面)。
    /// <para><b>语义</b>:给定一个配方与一个玩家,回答「此刻可不可以发起这一炉」。
    /// 判据 = AC-18-10 的三条件合取(`Admissible ∧ owner=process ∧ 单炉空闲`),
    /// 但**判据本体不在这里** —— 见实现方的转调义务。</para>
    /// <para><b>纯查询</b>:实现方不得在本方法内推进任何状态(含缓存时间戳)。
    /// 42 每次开屏 / 每 tick 刷新都会问,带副作用的实现会让「同输入不同答案」变得可表达。</para>
    /// </summary>
    /// <remarks>
    /// <para>⚠️ <b>与 <see cref="ISingleFurnaceQuery"/> 的分工</b>:后者只答「单炉空闲」
    /// 这一位(AC-18-10 的**第三**条件);本接口答**三条件合取**。
    /// 42 屏需要的是后者(它要决定「这一行出不出」),不是逐条自己合取 ——
    /// 那样就把 AC-18-20 的「零第二实现」在呈现层重新破一次。</para>
    /// <para>⚠️ <b>为什么收 <c>IReadOnlyList&lt;int&gt;</c> 而不是配方内的 <c>inputs[]</c></b>:
    /// F-18.2 补注① 逐字「不是 `inputs[]` 行 —— 跨栈凑量时行级 min 会静默放行差料」。
    /// 本接口的形参名沿用该口径,实现方不得把它换成 `recipe.Inputs`。</para>
    /// </remarks>
    public interface IProcessingAdmissionQuery
    {
        /// <summary>该配方此刻对该玩家是否可发起(可进候选面板)。</summary>
        /// <param name="recipe">被求值的配方(21a 表的一行)。</param>
        /// <param name="actorId">玩家 id(与 <c>InventoryOf(player)</c> 的归因同一号空间)。</param>
        /// <param name="consumedQualities">**被点名实例**的品级集(F-18.2 补注①)。
        /// <b>不得为 <c>null</c></b> —— 实现方须抛 `ArgumentNullException`(fail-closed,
        /// 承 `AdmissiblePredicate.Judge` 的同款理由:null 当成空集会静默放行)。
        /// <b>空集(非 null)是合法输入</b>,结果 = 不可发起。</param>
        /// <returns>可发起 ⇒ <c>true</c>(该配方应当出现在面板上)。</returns>
        bool IsAdmissible(in Recipe recipe, int actorId, IReadOnlyList<int> consumedQualities);
    }
}
