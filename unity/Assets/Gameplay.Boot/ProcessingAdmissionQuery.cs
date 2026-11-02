// `IProcessingAdmissionQuery` 的**生产实现** —— 18 → 42 的只读准入接缝(`O-18-R6` 接口半边)
//
// ── 判据真源(不许有第二个)────────────────────────────────────────────────
//   `ProcessingCandidateSet.IsCandidate(recipe, CandidateQuery)`
//     = AC-18-10 的三条件合取 `Admissible ∧ owner=process ∧ 单炉空闲`(iff 双向)。
//
// ⚠️ **本类零判据、零状态**:只做「拼 `CandidateQuery` + 转发」。
//    任何「等级够不够 / 品质够不够 / 炉忙不忙」写在这里 = AC-18-20 明令禁止的第二实现
//    (而且 42 屏会成为第二个求值面 ⇒ 预答与终裁分叉)。
//
// ── 为什么这个类住 `Gameplay.Boot`
//   `Gameplay.UI` 的引用集 = `{Sim.Contracts, Gameplay.Presentation}` —— **看不见 `Sim`**,
//   而 `ProcessingCandidateSet` 住 `Sim`。`Gameplay.Boot` 同时引 `Sim` + `Sim.Contracts` + `Gameplay.UI`
//   ⇒ 它是唯一能「看见两侧」并做转接的程序集。与 `HarvestWorldQuery` / `VitalsQueryOrNone`
//   同一落点先例。
//
// ── 依赖为什么是 `Func` 而不是值
//   炉忙不忙是**派生态**(从 `Craft` 事件折叠),随流每次变化。持一份 `bool` 快照 =
//   炉早已空出来,面板却还判「忙」。
//   品级集同理:玩家点了别的料,品级集就变了。
//   ⇒ 两者都走回调,每次求值重取。
//
// ⚠️ 不得读表现态位置:本类不碰玩家格 / 器具格(那是 4 的交互面)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Processing;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// <see cref="IProcessingAdmissionQuery"/> 的生产实现:转发到 <see cref="ProcessingCandidateSet"/>。
    /// </summary>
    /// <remarks>
    /// <para><b>与 <see cref="ISingleFurnaceQuery"/> 的关系</b>:后者的生产实现
    /// (若有)答「单炉空闲」;本类把它的结果作为 `CandidateQuery.Furnace` 传进去。
    /// 两者不是替代关系 —— 本类是**合取面**,后者是**其中一个条件**。</para>
    /// </remarks>
    public sealed class ProcessingAdmissionQuery : IProcessingAdmissionQuery
    {
        private readonly ISkillLevelQuery _skillLevels;
        private readonly ISingleFurnaceQuery _furnace;
        private readonly Func<int, IReadOnlyList<int>> _consumedQualitiesOf;

        /// <summary>构造查询面。</summary>
        /// <param name="skillLevels">30 的只读等级查询(AC-18-05 的门)。</param>
        /// <param name="furnace">单炉空闲查询(AC-18-10 的第三条件)。</param>
        /// <param name="consumedQualitiesOf">**取当前品级集**的回调(按 `actorId`;
        /// 每次调用重取,不缓存 —— 玩家点别的料它就变了)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public ProcessingAdmissionQuery(ISkillLevelQuery skillLevels,
                                        ISingleFurnaceQuery furnace,
                                        Func<int, IReadOnlyList<int>> consumedQualitiesOf)
        {
            _skillLevels = skillLevels ?? throw new ArgumentNullException(nameof(skillLevels),
                "未注入技能等级查询 —— 拒绝当作「零级」继续判(AC-18-05 的静默失效)");
            _furnace = furnace ?? throw new ArgumentNullException(nameof(furnace),
                "未注入单炉查询 —— 拒绝当作「炉永远空闲」继续判(AC-18-10 第三条件的静默失效)");
            _consumedQualitiesOf = consumedQualitiesOf ?? throw new ArgumentNullException(
                nameof(consumedQualitiesOf),
                "未注入品级集回调 —— 拒绝当作「空集」放行(AC-18-10 的静默失效)");
        }

        /// <inheritdoc/>
        /// <remarks>
        /// ⚠️ <b>`consumedQualities` 形参被<b>忽略</b></b> —— 真源是构造期注入的
        /// <c>_consumedQualitiesOf</c>。理由:42 屏不知道玩家此刻点了哪些料(那是 20 的
        /// 选中态),而本类能按 `actorId` 取到。若两者都收,就会出现「调用方传一份、
        /// 回调取一份」的双来源 —— 传错时不报错,只是判错。
        /// <para>⇒ 该形参在接口上保留(它是判据的**语义位**,删掉会让「判据需要品级集」
        /// 这件事在类型面消失),但实现方以回调为准。这条**不对称是设计**,已登记。</para>
        /// </remarks>
        public bool IsAdmissible(in Recipe recipe, int actorId,
                                 IReadOnlyList<int> consumedQualities)
        {
            // fail-closed:接口契约要求 null ⇒ 抛。即使本实现不用这个形参,
            // 也不能让「传 null 却不报错」成为可表达状态(否则调用方会以为它生效了)。
            if (consumedQualities == null)
                throw new ArgumentNullException(nameof(consumedQualities),
                    "未传入品级集 —— 拒绝当作「空集」放行(AC-18-10 的静默失效)");

            IReadOnlyList<int> qualities = _consumedQualitiesOf(actorId);
            if (qualities == null)
                throw new InvalidOperationException(
                    $"品级集回调对 actorId={actorId} 返回 null —— 拒绝当作「空集」放行");

            var query = new CandidateQuery(actorId, qualities, _skillLevels, _furnace);
            return ProcessingCandidateSet.IsCandidate(in recipe, in query);
        }
    }
}
