// S20-4 · processing story-001 —— 候选集三条件合取(AC-18-10 的 18 侧半边)
//
// 权威来源:
//   design/gdd/processing.md
//     · AC-18-10 配方进入候选集 ⟺ `Admissible ∧ owner=process ∧ 单炉空闲`(**iff 双向**)
//     · 规则一 / D-21-30 —— 子集划分唯一判据 = `Recipe.Owner` 显式字段,
//       **不靠 `processing_state` 约定**(降级为单向校验断言)
//     · 规则八 —— per-player 单炉(OQ-18-3 裁 [甲]),无排队
//   ADR-011 Amendment B —— 预答是呈现门,本候选集服务它;权威求值仍在终裁
//   ADR-025 §① —— `Sim` 引用集恰 = {BCL, Sim.Contracts}
//
// ─────────────────────────────────────────────────────────────────────────────
// 「iff 双向」在这里意味着什么
// ─────────────────────────────────────────────────────────────────────────────
// 单向(`⟸`)是「三条件都成立 ⇒ 必进候选集」;反向(`⟹`)是
// 「进了候选集 ⇒ 三条件都成立」,即**不许有第四种放行路径**。
//
// 反向是最容易被实现悄悄破坏的那一半 —— 一个「特殊配方豁免」「空实例集放行」
// 「单炉判定放在循环外先短路」都能让反向不成立而正向仍绿。
// ⇒ 夹具用**逐配方 × 逐组合**的 iff 矩阵断双向,不用「总数相等」这种弱形态。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么本类不重写判据,只做**合取**
// ─────────────────────────────────────────────────────────────────────────────
// `owner == process` 与两道门在别处各有唯一定义点(`RecipeOwner` 枚举 / `AdmissiblePredicate`)。
// 本类若自己再判一次 `skill_gate`,就造出 AC-18-20 明令禁止的第二实现。
// ⇒ 本类体内**只允许**三件事:读 `Owner`、调 `AdmissiblePredicate.Judge` + `IsAdmitted`、
//   调 `ISingleFurnaceQuery.IsBusy`。
//
// ⚠️ **2026-10-27 自查订正**:初版此处写的是
//   `AdmissiblePredicate.Judge(...) == AdmissionRejection.None` ——
//   与 `AdmissiblePredicate.IsAdmitted` 的头注「`AdmissionRejection.None` 的**唯一**比较处」
//   **直接矛盾**。这不是笔误,是**同一份判据的第二个定义点**:改 `IsAdmitted` 的口径
//   (例如将来要「`NoConsumedInstances` 也算过」)时,本类会**不跟随**而静默分叉。
//   已改为经 `IsAdmitted`;夹具 `test_ac1810_*` 的 8 组合矩阵是这次分叉的发现者
//   (它在 M5/M6 变异下仍全绿,却掩盖不了这条**读代码**才看得见的重复)。
//   教训:「唯一定义点」写进注释时,**须当场 grep 该判据的全部出现点** —— 注释不是门。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Processing
{
    /// <summary>候选集求值所需的一次性入参 —— 把「玩家侧上下文」与「配方表」分开。</summary>
    /// <remarks>
    /// <para>⚠️ <b>为什么单独成类型而不是五个形参</b>:AC-18-10 的 iff 矩阵要枚举
    /// 「配方 × 玩家状态」两个维度的组合,把玩家侧三件(等级查询 / 实例品级集 / 炉查询)
    /// 打包后,夹具可以用同一个上下文对象喂不同配方,减少「构造差异混进判定差异」的风险。</para>
    /// <para>⚠️ <b>2026-10-27 评审 AR1 订正 —— 构造器的三道 null 检查不是 fail-closed 的全部</b>:
    /// 本类型是 <c>readonly struct</c>,编译器**隐式提供无参构造器**,故
    /// <c>default(CandidateQuery)</c> 合法且三个字段全 null,**完全绕过**下面三道检查。
    /// 初版注释称「任一引用参数为 null ⇒ fail-closed」,那句话**只对本构造器成立**,对
    /// <c>default</c> 不成立 —— 自述比实现强,是「注释不是门」的同族形态。
    /// 补法在**消费侧**:<see cref="ProcessingCandidateSet.IsCandidate"/> 入口再验一次
    /// (见该方法的 Remarks)。之所以不在本类型内解决:<c>struct</c> 无法禁止 <c>default</c>,
    /// 改成 <c>class</c> 会给每 tick 求值引入堆分配(AC-18-10 的候选集是预答热路径)。</para>
    /// </remarks>
    public readonly struct CandidateQuery
    {
        /// <summary>玩家 id(与 `InventoryOf(player)` 归因同一号空间)。</summary>
        public readonly int ActorId;

        /// <summary>**被点名实例**的品级集(F-18.2 补注①:不是 `inputs[]` 行)。</summary>
        public readonly IReadOnlyList<int> ConsumedQualities;

        /// <summary>30 的只读等级查询。</summary>
        public readonly ISkillLevelQuery SkillLevels;

        /// <summary>单炉空闲查询(AC-18-10 第三条件)。</summary>
        public readonly ISingleFurnaceQuery Furnace;

        /// <summary>构造。</summary>
        /// <exception cref="ArgumentNullException">任一引用参数为 null。
        /// ⚠️ 本检查**不覆盖** <c>default(CandidateQuery)</c> —— 见类型 Remarks。</exception>
        public CandidateQuery(int actorId,
                              IReadOnlyList<int> consumedQualities,
                              ISkillLevelQuery skillLevels,
                              ISingleFurnaceQuery furnace)
        {
            if (consumedQualities == null)
                throw new ArgumentNullException(nameof(consumedQualities));
            if (skillLevels == null)
                throw new ArgumentNullException(nameof(skillLevels));
            if (furnace == null)
                throw new ArgumentNullException(nameof(furnace));

            ActorId = actorId;
            ConsumedQualities = consumedQualities;
            SkillLevels = skillLevels;
            Furnace = furnace;
        }
    }

    /// <summary>
    /// 炮制候选集 —— `Admissible ∧ owner=process ∧ 单炉空闲` 的三条件合取(AC-18-10)。
    /// <para><b>纯函数</b>:零静态可变状态、零 I/O、零墙钟、零事件写入。</para>
    /// </summary>
    /// <remarks>
    /// ⚠️ **「纯」的边界(2026-10-27 评审 AR5)**:纯是对**本类**说的 —— 同
    /// <c>(recipe, query)</c> 任意次求值逐位同。但 <c>query.Furnace.IsBusy(actorId)</c> 的
    /// 实现必然闭包「当下 tick」,故 <c>IsCandidate</c> 作为<b>整体</b>不是时间的纯函数。
    /// 这**不违反** ADR-016 §一 三源不变量:tick 来自 <c>ITickProvider</c>、busy 由事件流
    /// 折叠而来,两者都在三源内。此处如实写明,避免把「本类无状态」读成「答案与时序无关」。
    /// </remarks>
    public static class ProcessingCandidateSet
    {
        /// <summary>某配方是否进入该玩家的候选集(**iff 双向**的唯一判据)。</summary>
        /// <param name="recipe">被求值的配方。</param>
        /// <param name="query">玩家侧上下文。</param>
        /// <returns>进入候选集 ⇒ <c>true</c>。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="query"/> 的三个引用字段
        /// 任一为 null —— 含 <c>default(CandidateQuery)</c> 这一路径。</exception>
        /// <remarks>
        /// <b>三条件的次序是有意的</b>:`Owner` 先判(最便宜,且是子集划分的**唯一**判据),
        /// 单炉次之(与配方无关,一次查询),两道门最后(要查技能与实例)。
        /// 次序**不影响结果**(三者是纯合取,无短路语义差异),只影响开销;
        /// 但夹具**不得**依赖次序 —— 断言写成 iff 矩阵而非「第几步返回」。
        /// <para>⚠️ <b>入口 null 校验为什么在这里而不只在构造器</b>(2026-10-27 评审 AR1):
        /// <see cref="CandidateQuery"/> 是 <c>readonly struct</c>,<c>default(CandidateQuery)</c>
        /// 绕过其构造器。若此处不验,<c>default</c> 会落到
        /// <c>query.Furnace.IsBusy(...)</c> 抛 <c>NullReferenceException</c> ——
        /// 而不是契约承诺的 <c>ArgumentNullException</c>。
        /// 两者的差别不是异常类型洁癖:**NRE 意味着「解引用了一个本不该为 null 的东西」**
        /// (指向实现 bug),而 <c>ArgumentNullException</c> 意味着「调用方漏传」
        /// (指向接线 bug)。合并会让现场归因错向。</para>
        /// <para>校验放在 <see cref="IsCandidate"/> 而非另立入口,是因为它同时是
        /// <see cref="Evaluate"/> 的每一条路径 —— 一处校验覆盖两条批量 / 逐条路径。</para>
        /// </remarks>
        public static bool IsCandidate(in Recipe recipe, in CandidateQuery query)
        {
            ValidateQuery(in query);

            // ① 子集划分(D-21-30):唯一判据是显式字段,不靠 processing_state 约定。
            if (recipe.Owner != RecipeOwner.Process)
                return false;

            // ② 单炉空闲(AC-18-10 第三条件)。per-player,故键是 actorId 而非炉/格。
            if (query.Furnace.IsBusy(query.ActorId))
                return false;

            // ③ 两道门 —— **只经** AdmissiblePredicate(AC-18-20 零第二实现)。
            //    ⚠️ 「算不算过」也必须经 `IsAdmitted`,不得在此自写 `== AdmissionRejection.None`:
            //    那会造出「通过」的**第二个定义点**,与 AC-18-20 要防的形态同族。
            return AdmissiblePredicate.IsAdmitted(
                AdmissiblePredicate.Judge(
                    recipe, query.ActorId, query.ConsumedQualities, query.SkillLevels));
        }

        /// <summary>求候选集(按输入顺序保序 —— 面板序由 42 决定,本类不排序)。</summary>
        /// <param name="recipes">配方表(可含任意 owner;非 process 的会被滤掉)。</param>
        /// <param name="query">玩家侧上下文。</param>
        /// <returns>进入候选集的配方(保序;无则空列表而非 null)。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="recipes"/> 为 null,
        /// 或 <paramref name="query"/> 的三个引用字段任一为 null。</exception>
        /// <remarks>
        /// ⚠️ <b>保序而不排序</b>:候选集的**呈现顺序**属 42 的裁量(拟物 UI 的
        /// 配方册排序规则未定,见 O-18-R6)。本类若自作主张排序,会把一个未裁的
        /// 呈现决策固化进 sim。
        /// </remarks>
        public static List<Recipe> Evaluate(IReadOnlyList<Recipe> recipes, in CandidateQuery query)
        {
            if (recipes == null)
                throw new ArgumentNullException(nameof(recipes),
                    "未传入配方表 —— 拒绝当作「零候选」放行(空面板与无配方在玩家侧不同)");

            // ⚠️ query 校验必须在循环**之前**:空配方表时循环不执行,
            //    若把校验留在 `IsCandidate` 里就漏检 —— 而「空表 + null query」
            //    恰是最容易出现的接线错误(新玩家还没有任何配方)。
            ValidateQuery(in query);

            var result = new List<Recipe>();
            for (int i = 0; i < recipes.Count; i++)
            {
                Recipe r = recipes[i];
                if (IsCandidate(in r, in query))
                    result.Add(r);
            }
            return result;
        }

        /// <summary>候选集入参完整性 —— 覆盖 <c>default(CandidateQuery)</c>(绕构造器)与直接构造两条路径。</summary>
        /// <remarks>
        /// <para>与 <c>AdmissiblePredicate.Judge</c> 的 fail-closed 同款:唯一拒绝门上不放 fail-open 捷径。
        /// 把 <c>null</c> 当空集 / 零级 / 不忙处理,会把「调用方漏传」伪装成「玩家条件不满足」。</para>
        /// <para>⚠️ 单独立方法而非在 <see cref="IsCandidate"/> 里内联:<see cref="Evaluate"/> 须在
        /// **循环之前**校验一次,否则空配方表时校验根本不执行。</para>
        /// </remarks>
        private static void ValidateQuery(in CandidateQuery query)
        {
            if (query.ConsumedQualities == null)
                throw new ArgumentNullException(nameof(query),
                    "CandidateQuery.ConsumedQualities 为 null —— 拒绝当作「空集」判(false 会把「上层没传」伪装成「料不够」)");
            if (query.SkillLevels == null)
                throw new ArgumentNullException(nameof(query),
                    "CandidateQuery.SkillLevels 为 null —— 拒绝当作「零级」继续判(那是 AC-18-05 的静默失效)");
            if (query.Furnace == null)
                throw new ArgumentNullException(nameof(query),
                    "CandidateQuery.Furnace 为 null —— 单炉条件是 AC-18-10 三条件之一,缺它即无判据");
        }
    }
}
