// 9 的可干预性真值 `handle` —— 派生实现(2026-10-23 立,结清 Binder 文件尾 §未实现登记 ①)。
//
// 权威来源:
//   GDD disease-simulation.md `:294` —— `handle ∈ {causal, symptomatic_only, care, none}` 的**语义**
//   GDD disease-simulation.md `:329` —— **派生规则逐字**:
//       `causal` ⇔ ∃ 对因处置 ∈ `treatable_by`;
//       否则若 ∈ {伤寒, 痢疾, 心衰} ⇒ `care`;
//       若 = `DIS_TETANUS` ⇒ `none`;
//       其余无对因病 ⇒ `symptomatic_only`
//   GDD disease-simulation.md `:609` / `:680-698` —— 8 只读 `handle`,不读 `disease_id`(铁律四)
//   GDD disease-simulation.md `:1551` `D-9-K` —— 极性以**轴**为准(故本类的 `hasCausal` 输入
//     必须来自轴,不是载荷)
//
// ── 为什么是「派生」而不是「手填」────────────────────────────────────────────
//   GDD `:329` 的原话:「**派生而非手填,防「表里写 causal、实际没有对因药」的静默撒谎**」。
//   手填 = 作者可以写一个与 `treatable_by` 矛盾的 handle,而没有任何东西会红。
//
// ── 为什么住 `Sim` 而不是烘焙器 ──────────────────────────────────────────────
//   ① 它是 **9 的**语义(规则九:注册表派生,不是代码 —— 但「怎么派生」是 9 的知识);
//   ② 住 `Sim` ⇒ 烘焙器(`Editor.Tools.Bake`)与测试共用**同一份**实现,
//      不会出现「烘焙器算一套、测试验另一套」;
//   ③ `Sim` 的引用集恰 = {BCL, Sim.Contracts} ⇒ 本类**只**吃 `(string, bool)`,
//      不碰轴产物、不碰 `Gameplay.*`(门 A 不破)。**「hasCausal 从哪来」是调用方的事** ——
//      烘焙期由 `DiseaseRegistryBinder` 从 `disease_action_axis.json` 算,
//      这与 `ITreatableByLookup` 的「查表面住装配层」是同一条边界。
//
// ── ⚠️ 与 `CareLeverApplicability` 的关系 ───────────────────────────────────
//   `care` 分支的适用集**不在这里另存一份** —— 直接调 `CareLeverApplicability.AppliesToKey`,
//   保证「handle == care」与「照护杠杆适用」**恒同源**(GDD `:294` 的充要条件,
//   由 `CareLeverApplicability.CareApplicableConsistent` 断言)。

using System;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// `handle` 的四个取值 + 派生规则(GDD <c>:294</c> / <c>:329</c>)—— **唯一实现**。
    /// </summary>
    /// <remarks>
    /// <para><b>为什么用 <c>string</c> 常量而不是枚举</b>:<see cref="DiseaseRegistryEntry.Handle"/>
    /// 是 <c>string</c>,且它要进 cooked 产物与 8 的接口面(跨程序集、跨进程)。
    /// 造枚举 = 在类型系统里加一层与产物字符串并行的表示,两者可分叉
    /// (与 <c>DiseaseKind</c> 不同:后者是 <c>Sim</c> 内部枚举,产物里本来就按 int 编码)。</para>
    ///
    /// <para>⚠️ <b>取值集合是闭集</b>:GDD <c>:294</c> 列了四个,没有第五个。
    /// <see cref="IsValid"/> 是那条闭集的机械表达 —— 任何写进产物的值都要过它。</para>
    /// </remarks>
    public static class DiseaseHandle
    {
        /// <summary>有对因处置,可改 <c>Progress</c>。</summary>
        public const string Causal = "causal";

        /// <summary>仅对症,不逆转病程。</summary>
        public const string SymptomaticOnly = "symptomatic_only";

        /// <summary>无对因药,但照护暂停适用 —— 仅 {伤寒 / 痢疾 / 心衰}。</summary>
        public const string Care = "care";

        /// <summary>发病后无解 —— 仅 <c>DIS_TETANUS</c>。</summary>
        public const string None = "none";

        /// <summary>闭集四值(顺序 = GDD <c>:294</c> 的书写序;供诊断与测试遍历)。</summary>
        public static readonly string[] All = { Causal, SymptomaticOnly, Care, None };

        /// <summary>`none` 的唯一归属病种(GDD <c>:294</c> / <c>:435</c> 点名)。</summary>
        public const string TetanusDiseaseKey = "DIS_TETANUS";

        /// <summary>取值是否在闭集内。</summary>
        public static bool IsValid(string handle)
        {
            for (int i = 0; i < All.Length; i++)
                if (string.Equals(All[i], handle, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// 按 GDD <c>:329</c> 的派生规则算 <c>handle</c>。
        /// </summary>
        /// <param name="diseaseKey">注册表 <c>key</c>(如 <c>"DIS_TYPHOID"</c>)。</param>
        /// <param name="hasCausalTreatment">
        /// 该病种在**轴**里是否至少有一条 <c>polarity = causal</c> 的 <c>treatable_by</c> 行。
        /// <para>⚠️ 这个 bool 的**来源必须是轴**(<c>D-9-K</c>),不是载荷极性、不是内联数组。
        /// 本方法刻意收 bool 而不是收行集:收行集就得把轴的行类型引进 <c>Sim</c>,
        /// 而轴的行类型住 <c>Gameplay.Presentation</c> —— 门 A 不许。</para>
        /// </param>
        /// <returns>闭集四值之一。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="diseaseKey"/> 为 null。</exception>
        /// <remarks>
        /// <para><b>判定顺序不可调换</b>:<c>causal</c> 优先于 <c>care</c>。若某天给伤寒加了
        /// 对因药,它应变成 <c>causal</c> 而不是继续 <c>care</c> —— GDD 的「否则若」就是这个意思。
        /// 反过来把 <c>care</c> 排在前面,会让「伤寒有了对因药」这件事**静默不生效**。</para>
        ///
        /// <para><b><c>none</c> 的位置</b>:GDD 把 <c>DIS_TETANUS ⇒ none</c> 写在 <c>care</c> 之后、
        /// 「其余」之前。破伤风不在照护适用集内(K8),故两者的先后**当前不可观测**;
        /// 但保持 GDD 的书写序,免得将来有人给破伤风加进照护集时行为静默改变。</para>
        /// </remarks>
        public static string Derive(string diseaseKey, bool hasCausalTreatment)
        {
            if (diseaseKey == null) throw new ArgumentNullException(nameof(diseaseKey));

            if (hasCausalTreatment) return Causal;
            if (CareLeverApplicability.AppliesToKey(diseaseKey)) return Care;
            if (string.Equals(diseaseKey, TetanusDiseaseKey, StringComparison.Ordinal)) return None;
            return SymptomaticOnly;
        }
    }
}
