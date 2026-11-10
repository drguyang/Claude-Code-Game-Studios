// 技法解锁的**触发规则** —— 奇遇 / Boss / 病例 → 技法(2026-11-09 · T9)
//
// 权威:`design/gdd/reviews/technique-unlock-map-2026-11-09.md` §5.2(奇遇 / boss 关卡事件解锁;
//       钩子病例「缺哪门技法」驱动探索)· ADR-043 §D2(写者 = 30,奇遇宿主只是**调用方**)·
//       ADR-043 §D5(技法 id = 处置轴 action id)· ADR-009 §一(三问判据:真源 / 权威 / 可感知)
//
// ── 本件补的是什么断点 ────────────────────────────────────────────────────
// T3 交付 `TechniqueUnlockWriter` 时,全仓**唯一**调用点 = 起步集写入器(`{0, 1}`)。
// 🔁 2026-11-10(A3-b):该集合已收成 `{0, 1}` → **`{0}`**(节奏型通气移出起步集),
//    故今日唯一调用点写入的是 **1 门**技法。下文「17 行 / 6 行」是 T3 时点的实测,
//    原样保留作历史;内容态以 `draw_surface_audit_test` 的 `[T11]` 行为准。
// ⇒ 其余技法在 P0 里**永远锁着**:17 行注册表只有 6 行可投放(4 件伤情 + 两个 Undeclared),
//    11 行永久被 T4 的判定拒之门外。本件是那条「奇遇 → 解锁」通路的**机制**半边。
//
// ── 为什么规则表住代码常量,而不是 baked artifact ───────────────────────────
// ① **今日零行,且不是我没填** —— 三个候选源在数据侧都没有可映射的语义:
//    `world_poi.json` 只有 `{poi_id, type}`(**没有**状态词表)· `52_event_pool.json`
//    只有数字 key + 权重 · 病例侧**没有**「病种 → 技法」映射。⇒ 规则行是**内容**,
//    归内容轮 —— 与 `TechniqueUnlockBootstrap.StartingTechniques`(`:36`)逐字同格。
//    🔁 **2026-11-10(A5)**:① 的一半已消解 —— 事件的语义登记进了
//    `52_event_pool.json` 的 `_note`,本表因此有了三条 Event 源规则;
//    POI 状态词表与「病种 → 技法」映射仍未裁。
// ② **同族先例**:本子系统的内容本来就住代码常量(起步集)。同一件事两种载体 = 第二套口径。
// ③ **零行为的管线是负资产**:今日若造 baked artifact,要动六处(源 JSON / binder /
//    cooked 写器 / 运行时 codec / Addressables 组 + meta / 漂移门哨兵)去运一张**空表**,
//    而「表是空的」与「表没装上」在运行期不可区分。
// ⇒ 载体 = `TechniqueUnlockRules.Default`。**升级路径是一个缝**:驱动只认
//    `IReadOnlyList<TechniqueUnlockRule>`,将来外部化只需换来源,驱动零改动。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>触发源种类 —— 三类事件的**闭集**(各有一个唯一写者)。</summary>
    /// <remarks>
    /// <para>每一员都对应一条**已经在流里**的事件,不新增 Kind、不改 registry:
    /// <see cref="Poi"/> → <c>PoiStateChanged</c>(ADR-021,写者 6)·
    /// <see cref="Event"/> → <c>EventArrived</c>(ADR-007,写者 52)·
    /// <see cref="Case"/> → <c>CaseClosed</c>(ADR-008,写者 37)。</para>
    /// <para>⚠️ 值是**载荷字段的值域**,不是枚举序号 —— 改值等于改语义,故显式写死。</para>
    /// </remarks>
    public enum TechniqueUnlockSourceKind
    {
        /// <summary>世界流 <c>PoiStateChanged</c>:POI 到达某状态(6 写)。</summary>
        Poi = 0,

        /// <summary>病史流 <c>EventArrived</c>:52 的奇遇 / 威胁降临。</summary>
        Event = 1,

        /// <summary>病例流 <c>CaseClosed</c>:一次**已处置**的结案(37 写)。</summary>
        Case = 2,
    }

    /// <summary>
    /// 一条「什么事实 ⇒ 解锁哪门技法」的规则。
    /// </summary>
    /// <remarks>
    /// <para><b>为什么 `RequiredState` / `MinTier` 挤在同一个 struct 里</b>:三个来源各自带一个
    /// 匹配约束,但它们**互斥**(按 <see cref="SourceKind"/> 取用)。拆成三个 struct 会让
    /// 调用方必须写三趟同构代码;合成一个则要求**构造入口收敛到三个具名工厂** ——
    /// 直接 <c>new</c> 会让「Poi 行带了 MinTier」这类错配编译通过、运行期静默。
    /// 故构造函数 <c>private</c>,只经 <see cref="ForPoi"/> / <see cref="ForEvent"/> /
    /// <see cref="ForCase"/> 造。</para>
    /// <para><b>`SourceId` 同时是发出去的 <c>sourceId</c></b>:ADR-043 §D4 的载荷第三字段就是
    /// 「来源 id」。分两个字段会让「记在流里的来源」和「匹配用的来源」可分叉。</para>
    /// </remarks>
    public readonly struct TechniqueUnlockRule
    {
        /// <summary>触发源种类(决定读哪个流、用哪个约束字段)。</summary>
        public readonly TechniqueUnlockSourceKind SourceKind;

        /// <summary>源标识 —— Poi: `poi_id`;Event: `event_key`;Case: `disease_id`。</summary>
        public readonly int SourceId;

        /// <summary>被解锁的技法 id(= 处置轴 action id,ADR-043 §D5)。</summary>
        public readonly int TechniqueId;

        /// <summary>仅 <see cref="TechniqueUnlockSourceKind.Poi"/> 有意义:要求的 <c>new_state</c>。</summary>
        public readonly int RequiredState;

        /// <summary>仅 <see cref="TechniqueUnlockSourceKind.Event"/> 有意义:最低档(<c>-1</c> = 任意档)。</summary>
        public readonly int MinTier;

        private TechniqueUnlockRule(TechniqueUnlockSourceKind sourceKind, int sourceId,
                                    int techniqueId, int requiredState, int minTier)
        {
            SourceKind = sourceKind;
            SourceId = sourceId;
            TechniqueId = techniqueId;
            RequiredState = requiredState;
            MinTier = minTier;
        }

        /// <summary>POI 到达 <paramref name="requiredState"/> ⇒ 解锁。</summary>
        /// <param name="poiId">POI 的稳定 id(<c>world_poi.json</c> 的 <c>poi_id</c>)。</param>
        /// <param name="requiredState">必须匹配的 <c>new_state</c> —— **不写「任意状态」**:
        /// POI 状态有机转换(未清 → 已清 等),只按 id 匹配会让「进去转一圈」也算解锁。</param>
        /// <param name="techniqueId">技法 id。</param>
        /// <exception cref="ArgumentOutOfRangeException">任一 id 为负。</exception>
        public static TechniqueUnlockRule ForPoi(int poiId, int requiredState, int techniqueId)
        {
            RequireNonNegative(poiId, nameof(poiId));
            RequireNonNegative(requiredState, nameof(requiredState));
            RequireNonNegative(techniqueId, nameof(techniqueId));
            return new TechniqueUnlockRule(TechniqueUnlockSourceKind.Poi, poiId, techniqueId,
                                           requiredState, minTier: -1);
        }

        /// <summary>奇遇 / 威胁降临(<c>tier ≥ minTier</c>)⇒ 解锁。</summary>
        /// <param name="eventKey">52 的事件键。</param>
        /// <param name="minTier">最低档;<c>-1</c> = 任意档。用「≥」而非「=」:档是威胁强度,
        /// 更强的同一奇遇不该反而解锁不了。</param>
        /// <param name="techniqueId">技法 id。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="eventKey"/> /
        /// <paramref name="techniqueId"/> 为负,或 <paramref name="minTier"/> &lt; -1。</exception>
        public static TechniqueUnlockRule ForEvent(int eventKey, int minTier, int techniqueId)
        {
            RequireNonNegative(eventKey, nameof(eventKey));
            RequireNonNegative(techniqueId, nameof(techniqueId));
            if (minTier < -1)
                throw new ArgumentOutOfRangeException(nameof(minTier), minTier,
                    "minTier 只能取 -1(任意档)或非负档位");
            return new TechniqueUnlockRule(TechniqueUnlockSourceKind.Event, eventKey, techniqueId,
                                           requiredState: -1, minTier);
        }

        /// <summary>一次**已处置**的结案里含该病种 ⇒ 解锁。</summary>
        /// <param name="diseaseId">病种 id(<c>disease_registry.json</c>)。</param>
        /// <param name="techniqueId">技法 id。</param>
        /// <exception cref="ArgumentOutOfRangeException">任一 id 为负。</exception>
        /// <remarks>
        /// ⚠️ **未处置的结案不触发**,且这不是可选行为:`CaseClosedPayload.Treated == false`
        /// 意味着病人是被放着或转走的,「从没治过的病例里学会技术」没有语义。该约束**写死在
        /// 驱动里**而不是做成规则字段 —— 做成字段就有「谁能把它设成 false」的分支。
        /// </remarks>
        public static TechniqueUnlockRule ForCase(int diseaseId, int techniqueId)
        {
            RequireNonNegative(diseaseId, nameof(diseaseId));
            RequireNonNegative(techniqueId, nameof(techniqueId));
            return new TechniqueUnlockRule(TechniqueUnlockSourceKind.Case, diseaseId, techniqueId,
                                           requiredState: -1, minTier: -1);
        }

        private static void RequireNonNegative(int value, string name)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(name, value, "id 不得为负(负值会变成流里的死数据)");
        }
    }

    /// <summary>
    /// 生产规则表 —— **今日零行**。
    /// </summary>
    /// <remarks>
    /// <para>🔁 <b>2026-11-10(A5)订正</b>:上句「今日零行」已不成立 —— 本表自本批起有
    /// **三条 Event 源规则**(见 <see cref="TechniqueUnlockRules.Default"/>)。下文是
    /// **T9 起草时**的口径,原样保留作历史;「为什么可以是空的」那段的答案(
    /// 交付的是机制不是内容)仍然成立,只是内容这半边已经补上了。</para>
    /// <para>🔁 **2026-11-10 另一处订正**:下段「值域今日无机械门」也已被 T12 推翻 ——
    /// `TechniqueUnlockRuleValidator` + `technique_side_gap_gate_test` ⑤ 现在会逐条校验
    /// 三类源 id 是否落在各自的真表里。</para>
    /// <para><b>为什么可以是空的</b>:本件交付的是**机制**,不是内容。空表 ⇒ 驱动零扫描、
    /// 零写入、零开销;补一行就活。这不是「未实现」,是「内容未裁定」——
    /// 两者在代码上同形,在报告里必须分开写。</para>
    /// <para><b>怎么写第一行</b>(唯一需要改的地方):</para>
    /// <code>
    /// public static readonly TechniqueUnlockRule[] Default =
    /// {
    ///     // 清创缝合(技法 2)只能从破伤风钩子病例的处置里学到
    ///     TechniqueUnlockRule.ForCase(diseaseId: 4, techniqueId: 2),
    /// };
    /// </code>
    /// <para>⚠️ <c>techniqueId</c> 必须 ∈ 处置轴 <c>actions[].id</c>(今日 `{0,1,2,3,10,11,12}`)——
    /// 写错会让 `TechniqueUnlockWriter` 在**运行期**抛(那是 fail-loud,不是静默),
    /// 但更早的发现点是 `technique_unlock_trigger_test` 的闭集判据。</para>
    /// <para>⚠️ <c>diseaseId</c> / <c>poi_id</c> / <c>event_key</c> 的值域**今日无机械门** ——
    /// 它们各自的词表归 9 / 6 / 52 的内容轮。这如实登记在 T9 的证据件「未做」表里。</para>
    /// </remarks>
    public static class TechniqueUnlockRules
    {
        /// <summary>生产规则集(2026-11-10 A5 起三条;此前为空)。</summary>
        /// <remarks>
        /// <para>🔁 <b>2026-11-10(A5,用户裁定「照准」)</b>:本表由空表变成三条 **Event 源**规则。
        /// 事件 key 的语义登记在 <c>assets/data/52_event_pool.json</c> 的 <c>_note</c>
        /// (池本身只存数字 key,语义原先全库零处)。</para>
        /// <para><c>minTier = -1</c> = 任意档 —— 池里的 <c>tier</c>(threat / opportunity / reaction)
        /// 到 <c>EventArrivedPayload.Tier</c> 的 ordinal 映射今日没有权威件,
        /// 设下限会造出「配置看着对、却永远不触发」的静默失效。</para>
        /// <para>⚠️ <b>三条今日都不会触发</b>:52 的运行期链路被
        /// <c>TD-randomevents-05</c>(上下文门硬编码 false ⇒ 恒 <c>NoContext</c>)与
        /// ADR-039 §R3(生产候选池为空 ⇒ 恒 <c>NoPool</c>)挡着。本表落地的是
        /// 「规则有内容」,**不是**「技能够解锁了」—— 与 `ChapterAdvanced` 同一口径。</para>
        /// </remarks>
        public static readonly IReadOnlyList<TechniqueUnlockRule> Default =
            new[]
            {
                // ── Event 源(三条)—— 语义登记在 `52_event_pool.json` 的 `_note` ──
                // 「打摆子」(第三章)—— 疟疾病人上门 ⇒ 学会金鸡纳皮
                TechniqueUnlockRule.ForEvent(2002, -1, 12),
                // 「热性惊厥」(第三章)—— 抽搐的孩子 ⇒ 学会节奏型通气
                TechniqueUnlockRule.ForEvent(2003, -1, 1),
                // 「溃兵军医」(第四章)—— 心衰病人 + 军医药箱 ⇒ 学会毛地黄
                TechniqueUnlockRule.ForEvent(2004, -1, 11),

                // ── Case 源(两条)—— ⚠️ 这两条是**今天唯一真的会触发**的规则 ──
                //   `CaseClosed` 是三条源里唯一活着的事件源:`EventArrived` 压在 52 的死链后面
                //   (`TD-randomevents-05` / ADR-039 §R3),`PoiStateChanged` 缺状态词表。
                //   ⇒ 把「处置完一个现在还治得动的病」接成成长链,是 P0 里唯一能让玩家
                //     **真的学会一门技法**的路径。
                //   链:止血(起步集)处置刀伤 → 学会清创 → 咬伤/钝击/破伤风可投放
                //       → 清创处置咬伤 → 学会蛇伤处置 → 蛇毒可投放
                //   ⚠️ 两条都**不循环**:被触发的那门技法,处置它自己那条病种时不需要它
                //      (刀伤由技法 0 处置;咬伤由技法 2 处置)。
                TechniqueUnlockRule.ForCase(2, 2),   // INJ_STAB(刀伤)→ 清创缝合
                TechniqueUnlockRule.ForCase(6, 3),   // INJ_BITE(咬伤)→ 蛇伤处置
            };
    }
}
