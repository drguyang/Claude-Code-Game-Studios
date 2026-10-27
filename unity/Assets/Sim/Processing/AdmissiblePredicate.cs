// S20-4 · processing story-001 —— F-18.2 准入谓词 `Admissible`(两道门,整数比较)
//
// 权威来源:
//   design/gdd/processing.md
//     · F-18.2(:437-456)Admissible ⟺ QueryLevel(player, CRAFT_SKILL) ≥ recipe.skill_gate
//                                 ∧ min(quality of consumedInstances) ≥ recipe.min_quality
//     · F-18.2 BL-9 补注①(:449)`min` 的对象是**被点名的实例集**,不是 `inputs[]` 行
//       —— 20 规则六允许跨栈凑量点名,行级 min 会静默放行「凑出来的差料」
//     · F-18.2 BL-9 补注②(:451)裁决权威 = 主机终裁;客户端预答是**呈现门**,
//       **两处必须同一份实现,禁两份**
//     · 规则三(:167)`skill_gate` 是**门槛非系数**(否则与 SkillMod 双重计数);
//       `min_quality` 是**准入闸不改幅值**(品级出口只有 F5)
//   ADR-011 Amendment B —— 判定输入进流、主机终裁;本地判定降级为预表现
//   ADR-025 §① —— `Sim` 引用集恰 = {BCL, Sim.Contracts}
//
// ─────────────────────────────────────────────────────────────────────────────
// 本类**不做**什么(边界,必须与「它做了什么」同读)
// ─────────────────────────────────────────────────────────────────────────────
// ① **不筛子集** —— `owner == process` 归 `ProcessingCandidateSet`(AC-18-10 第一条件)。
//    本谓词只答「这个配方这个玩家够不够格」,不答「哪些配方该出现在面板上」。
// ② **不判单炉** —— 归 `ISingleFurnaceQuery`(AC-18-10 第三条件)。理由见该接口注释:
//    谓词签名若含事件流,预答路径物理上不可用,AC-18-20 的同源要求随之落空。
// ③ **不扣料、不写事件** —— 本类零 `IEventSink` / 零 `IPayloadEncoder` 依赖。
//    不持写通道 ⇒ 想写也写不出来(与 `IgnitionAdmission` 同一纪律)。
// ④ **不读 `Recipe.Inputs` / `Recipe.Outputs`** —— 只看 `SkillGate` / `MinQuality`。
//    碰产出侧就是幅值调制路径,直接违 AC-18-05。
//
// ─────────────────────────────────────────────────────────────────────────────
// 两个调用点为什么是**两个具名方法**而不是一个方法调两次
// ─────────────────────────────────────────────────────────────────────────────
// AC-18-20 的判据是「**同一函数、零第二实现**」,不是「行为相同的两份」。要让这条可机械断言,
// 两个调用点必须**都能在 IL 里看到 `call ...::Admissible` 指令** —— 若预答写
// `return TerminalVerdict(...)`(转调),IL 里就只有一条 `call`,第二条路径变成
// 「间接同源」,夹具只能靠人工读代码确认。
//
// ⇒ `PreviewVerdict` 与 `TerminalVerdict` **各自直接调 `Admissible`**,且**除该调用外零判据逻辑**。
//   这样夹具可以分别断言:两条路径的 IL 里 `call` 的**方法引用逐位相同**
//   (`Admissible(Recipe, int, IReadOnlyList<int>)`),且两个包装方法体内**没有第二处比较指令**。
//
// ⚠️ 两条路径**语义不同**、**判据相同** —— 这是刻意的:
//   预答只决定面板显隐(呈现门),终裁决定是否落流(权威门);两者对同一输入的答案必须一致,
//   但「不一致时以谁为准」在 GDD 里已有答案(终裁),本类不重述。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;

namespace DaYiJingCheng.Sim.Processing
{
    /// <summary>准入判定的可诊断结果 —— 拒绝时点名**哪道门**没过。</summary>
    /// <remarks>
    /// <para>⚠️ <b>为什么不是裸 <c>bool</c></b>:两道门在玩家侧是不同处境 ——
    /// 技能不够 = 「练到 X 级再来」,品级不够 = 「换好料」。合成一个 <c>false</c>
    /// 会让面板无法给出可行动的反馈(42 的呈现半边要读它)。</para>
    /// <para>⚠️ 本类型**不承载**任何幅值信息 —— 它只说「过没过、哪道门」,
    /// 不说「差多少」。后者会诱使调用方拿去做调制,正是 AC-18-05 要防的形态。</para>
    /// </remarks>
    public enum AdmissionRejection
    {
        /// <summary>两道门都过。</summary>
        None = 0,

        /// <summary>技能门槛未达(`QueryLevel &lt; skill_gate`)。</summary>
        SkillGate = 1,

        /// <summary>品级准入闸未达(`min(quality) &lt; min_quality`)。</summary>
        MinQuality = 2,

        /// <summary>被点名的实例集为空 —— <c>min</c> over ∅ 无定义,按不通过处理。</summary>
        /// <remarks>
        /// ⚠️ 单独成值而不是并进 <see cref="MinQuality"/>:空集是**调用方给错了输入**
        /// (配方要求有投入,却一个实例都没点名),不是「料太差」。混在一起会让
        /// 「玩家料不够好」与「上层没传实例」在日志里不可区分。
        /// </remarks>
        NoConsumedInstances = 3,
    }

    /// <summary>
    /// F-18.2 准入谓词 —— 两道门的**全仓唯一定义点**(AC-18-20)。
    /// <para><b>纯函数</b>:零静态可变状态、零 I/O、零墙钟、零事件写入。
    /// 同输入任意次求值逐位同。</para>
    /// </summary>
    public static class AdmissiblePredicate
    {
        /// <summary>炮制技能 id —— `SkillId.炮制 = 7`(GDD 的 `CRAFT_SKILL` 符号名)。
        /// <para>⚠️ 取自 <see cref="SkillId"/> 而非字面量 7:技能号是 30 的所有物,
        /// 本系统不拥有该常量。</para></summary>
        public const int CraftSkillId = (int)SkillId.炮制;

        /// <summary>准入判定(带具名拒绝理由)。</summary>
        /// <param name="recipe">被求值的配方。</param>
        /// <param name="actorId">玩家 id(与 `InventoryOf(player)` 归因同一号空间)。</param>
        /// <param name="consumedQualities">**被点名实例**的品级集(F-18.2 补注①:
        /// 不是 `inputs[]` 行 —— 跨栈凑量时行级 min 会静默放行差料)。
        /// <b>不得为 <c>null</c></b>(fail-closed:见下)。</param>
        /// <param name="skillLevels">30 的只读等级查询。</param>
        /// <returns>两道门的裁决;全部通过 ⇒ <see cref="AdmissionRejection.None"/>。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="consumedQualities"/> 或
        /// <paramref name="skillLevels"/> 为 null。</exception>
        /// <remarks>
        /// <b>为什么 null 必须抛</b>(承 `IgnitionAdmission.CanAdmit` 的 2026-10-26 评审 B1 同款):
        /// 把 `null` 当成空集处理 = 「不知道点了哪些料」与「一个料都没点」被合并成同一件事,
        /// 而本方法是 AC-18-05 / AC-18-10(BLOCKING)唯一的准入门。fail-open 捷径在
        /// 唯一拒绝门上是结构性的静默失效。
        /// <para><b>空集(非 null)返回 <see cref="AdmissionRejection.NoConsumedInstances"/></b>,
        /// 不是抛 —— 它是合法的可表达输入(上层可以构造出来),只是不该通过。</para>
        /// </remarks>
        public static AdmissionRejection Judge(
            in Recipe recipe,
            int actorId,
            IReadOnlyList<int> consumedQualities,
            ISkillLevelQuery skillLevels)
        {
            if (consumedQualities == null)
                throw new ArgumentNullException(nameof(consumedQualities),
                    "未传入被点名实例的品级集 —— 拒绝当作「空集」放行(那是 AC-18-10 的静默失效)");
            if (skillLevels == null)
                throw new ArgumentNullException(nameof(skillLevels),
                    "未传入技能等级查询 —— 拒绝当作「零级」继续判(那是 AC-18-05 的静默失效)");

            if (skillLevels.QueryLevel(actorId, CraftSkillId) < recipe.SkillGate)
                return AdmissionRejection.SkillGate;

            if (consumedQualities.Count == 0)
                return AdmissionRejection.NoConsumedInstances;

            // min over 被点名实例(F-18.2 补注①)。手写循环而非 LINQ:
            // 门 A 内避免闭包分配,且 `Min()` 在空集上抛的是无上下文异常。
            int minQuality = int.MaxValue;
            for (int i = 0; i < consumedQualities.Count; i++)
            {
                int q = consumedQualities[i];
                if (q < minQuality) minQuality = q;
            }

            if (minQuality < recipe.MinQuality)
                return AdmissionRejection.MinQuality;

            return AdmissionRejection.None;
        }

        /// <summary>「准入通过」的**唯一定义点** —— <c>AdmissionRejection.None</c> 的唯一比较处。
        /// <para>⚠️ 为什么单独抽出来:两个调用点若各自写 `== AdmissionRejection.None`,
        /// 那个比较就变成**两处**判据。抽成一处后,两个包装方法体在 IL 面上
        /// 恰是「转调 <see cref="Judge"/> → 转调本方法 → 返回」,**零比较指令** ——
        /// 这才是 AC-18-20「零第二实现」可被机械断言的形式。</para>
        /// </summary>
        /// <param name="rejection">两道门的裁决。</param>
        /// <returns>通过 ⇒ <c>true</c>。</returns>
        public static bool IsAdmitted(AdmissionRejection rejection)
            => rejection == AdmissionRejection.None;

        /// <summary>**调用点 ①:客户端预答**(呈现门 —— 只决定面板显隐)。
        /// <para>⚠️ 本方法**除转调 <see cref="Judge"/> 与 <see cref="IsAdmitted"/> 外零判据逻辑**。
        /// 这不是「省略」,是 AC-18-20 的结构性保证:夹具在 IL 面断言本方法体
        /// 恰含一条 <c>call ...AdmissiblePredicate::Judge</c>、**零比较指令**。</para>
        /// </summary>
        /// <returns>该配方是否应当出现在本玩家的候选面板上。</returns>
        public static bool PreviewVerdict(
            in Recipe recipe,
            int actorId,
            IReadOnlyList<int> consumedQualities,
            ISkillLevelQuery skillLevels)
            => IsAdmitted(Judge(in recipe, actorId, consumedQualities, skillLevels));

        /// <summary>**调用点 ②:主机终裁**(权威门 —— 决定是否落 `Craft` 事件)。
        /// <para>⚠️ 与 <see cref="PreviewVerdict"/> 同款约束:零判据逻辑,只转调
        /// <see cref="Judge"/> 与 <see cref="IsAdmitted"/>。两方法的 IL 里对这两者的
        /// **方法引用逐位相同** —— 这才是「同一函数」的可机械断言形
        /// (而非「两份行为相同的实现」)。</para>
        /// </summary>
        /// <returns>是否准予落流(拒绝 ⇒ 调用方零事件,ADR-005)。</returns>
        public static bool TerminalVerdict(
            in Recipe recipe,
            int actorId,
            IReadOnlyList<int> consumedQualities,
            ISkillLevelQuery skillLevels)
            => IsAdmitted(Judge(in recipe, actorId, consumedQualities, skillLevels));
    }
}
