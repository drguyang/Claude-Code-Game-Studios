// 9 的 `treatable_by` 门 —— 运行期查表抽象点(2026-10-23 立)。
//
// 权威来源:
//   GDD disease-simulation.md F1 取舍 4(`:896`)—— 「本门的成员与极性**同查轴行 —— 轴为准**」
//   GDD disease-simulation.md `:1551` `D-9-K`(2026-10-10 已裁:极性双源优先级 = 轴为准)
//   GDD disease-simulation.md `:1555` `D-9-P`(treatable_by 门结构性缺位;装载面接通后
//     `IsInTreatableBy` 改为按 `(treatment, disease)` 查轴,**成员 + 极性同行**)
//   GDD disease-simulation.md R1.3(`:289`)—— `treatable_by[]` 每项带
//     `polarity ∈ {causal, symptomatic}`
//
// ── 为什么是抽象点而不是让 Sim 直接读轴产物 ──────────────────────────────────
//   轴产物(`disease_action_axis.cooked.bytes`)的解码器住
//   `Gameplay.Presentation/DiseaseActionAxisCookedCodec`,而 `Sim` 的引用集
//   **恰 = {BCL, Sim.Contracts}**(ADR-025 §① / b2 门 / 门 A `noEngineReferences`)
//   ⇒ `Sim` **物理上够不着**它。本接口与 `IPayloadEncoder` / `IPayloadDecoder`
//   (ADR-029 / ADR-031)同法:契约住 `Sim.Contracts`,实现与装载归装配层。
//
// ── 为什么**不**把 treatable_by 烘进病种注册表产物(2026-10-23 用户裁定)──────
//   GDD R1.3 把 `treatable_by[]` 定义在病种条目内,但 story-007 起已拆到
//   `disease_action_axis.json` 作**单一真源**。若再烘一份进注册表产物,轴与产物
//   就构成第二真源,二者可分叉 —— 而这正是 `D-9-K` 要消灭的形态。
//   ⇒ 产物**不携带** `treatable_by`;运行期由装配层持有一个本接口的实现。
//
// ── 空实现(`null`)的语义 ───────────────────────────────────────────────────
//   `null` = **未接线**,不是「没有任何处置可用」。门在 null 下**不拦**
//   (拦则生产治疗贡献恒 0,最小可证链不可证 —— 承 `D-9-P` 的登记临时口径)。
//   这与「查到了但不在集内 ⇒ 拦」是两种不同状态,调用方须分清。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// `treatable_by` 关系的只读查询面 —— **轴为准**(GDD <c>D-9-K</c>)。
    ///
    /// <para><b>实现方</b> = 装配层(`CompositionRoot` 由已加载的
    /// <c>DiseaseActionAxisDataSet</c> 造一个);<b>消费方</b> = <c>Sim</c> 的
    /// <c>ProgressionEvaluator</c>(F1 离牌门 + F4 有效对因处置判据)。</para>
    ///
    /// <para><b>为什么返回极性而不只返回 bool</b>:GDD <c>F1 取舍 4</c> 明写
    /// 「成员与极性**同查轴行**」。若只查成员、极性仍取载荷,就退回到
    /// <c>D-9-K</c> 判定前的「双源」形态 —— 同一味药对两个病种极性不同时,
    /// 载荷那一个值必然对其中一边是错的。</para>
    /// </summary>
    public interface ITreatableByLookup
    {
        /// <summary>
        /// 查 <c>(病种, 处置)</c> 这一对是否在轴里声明过,以及声明的极性。
        /// </summary>
        /// <param name="diseaseKey">病种 key(注册表 <c>key</c> 字段,如 <c>"DIS_MALARIA"</c>)。</param>
        /// <param name="actionId">处置 id(处置轴闭集成员)。</param>
        /// <param name="polarity">命中时写出极性序数
        /// (全案唯一契约:<c>0 = 对症 · 1 = 对因</c>,见 GDD <c>D-9-L</c>);
        /// 未命中时写 <c>-1</c>(哨兵,不是合法极性)。</param>
        /// <returns>
        /// <c>true</c> ⇔ 轴里存在该 <c>(病种, 处置)</c> 关系行。
        /// <para>⚠️ 病种**不在轴里**(如全部 <c>INJ_*</c> 伤情行)⇒ 返回 <c>false</c>,
        /// 含义是「该病种没有任何处置被声明为可用」—— 与 <c>null</c> 查表面
        /// (「未接线,无从判断」)语义不同。</para>
        /// </returns>
        bool TryGetPolarity(string diseaseKey, int actionId, out int polarity);
    }
}
