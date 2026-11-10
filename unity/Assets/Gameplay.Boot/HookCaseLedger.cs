// 钩子病例的**事件流账本**(T5a · 2026-11-09)—— 「这一章已经放过几例」
//
// 权威来源:
//   design/gdd/reviews/technique-unlock-map-2026-11-09.md §5.2 规则 1(每章 1–3 例)
//   ADR-016 §一 三源不变量 —— 决策的全部输入 ∈ {事件流, 版本化烘焙数据, 二者的纯函数}
//   ADR-043 §D7 —— 解锁状态 = 病史流折叠(本类复用同一判据,只是加了**时间**维)
//
// ── 为什么不做成内存计数器 ─────────────────────────────────────────────────
//   内存计数读档后不可重建(存档体里没有它),重放会得到不同的钩子配额 ——
//   与 `ADR-039 §D2` 删掉两个内存字段的理由逐字相同。
//
// ── 判据:一个病人是「钩子病例」当且仅当 ──────────────────────────────────
//   他的 `DiseaseOnset` 发生的那一刻,该病种在轴里声明的技法**一门都没解锁**。
//   这正是钩子病例的定义(「技法未解锁时出现的病例」),且**全部可从流重建**:
//   前向扫一遍,维护「截至此刻该 actor 已解锁的技法集」,遇到 onset 时比对。
//   ⚠️ 后解锁不追溯 —— 一个在后来的奇遇里解锁的技法,不会把**先前**那一例变成普通病例,
//      也不会把**之后**的同病种病人变成钩子。前向扫一遍天然给出这个语义。
//
// ── 为什么不用「此刻是否解锁」判 ──────────────────────────────────────────
//   用当前状态反推历史 = 把「当时未解锁」与「现在仍未解锁」混为一谈;一旦玩家中途解锁,
//   先前那例钩子就会被数丢,配额凭空多出一格。
//
// ⚠️ 「章」在运行期不存在 ⇒ 本类只给**全局**计数,章切分由调用方的事后处理承担
//    (今日 `chapterOf() ≡ 0`,见 `HookCasePolicy` 头注的具名缺口)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>病史流 → 钩子病例计数的只读账本(每次查询 O(n),与既有投影同口径)。</summary>
    public sealed class HookCaseLedger
    {
        private readonly Func<IReadOnlyList<SimEvent>> _events;
        private readonly IPayloadDecoder _decoder;
        private readonly TechniqueDrawIndex _index;
        private readonly IReadOnlyDictionary<int, DiseaseRegistryEntry> _registryById;

        /// <param name="events">事件流只读视图(每次查询时取 —— 装配早于流填充)。</param>
        /// <param name="decoder">载荷解码器(ADR-031)。</param>
        /// <param name="index">`treatable_by` 反向索引(与投放门**同一份**)。</param>
        /// <param name="registryById">病种 id → 注册表行(onset 载荷只带 id)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public HookCaseLedger(
            Func<IReadOnlyList<SimEvent>> events,
            IPayloadDecoder decoder,
            TechniqueDrawIndex index,
            IReadOnlyDictionary<int, DiseaseRegistryEntry> registryById)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _index = index ?? throw new ArgumentNullException(nameof(index));
            _registryById = registryById ?? throw new ArgumentNullException(nameof(registryById));
        }

        /// <summary>
        /// 计数主实现。<paramref name="targetChapter"/> &lt; 0 ⇒ 全局;否则只数该章内的。
        /// </summary>
        /// <param name="actorId">玩家 actor id(解锁事件按 actor 区分)。</param>
        /// <param name="targetChapter">目标章号(&lt;0 = 全局口径)。</param>
        /// <returns>钩子病例条数(0 = 一例都没有)。</returns>
        private int CountHookCasesCore(int actorId, int targetChapter)
        {
            IReadOnlyList<SimEvent> events = _events();
            if (events == null) return 0;

            var unlocked = new HashSet<int>();
            int hooks = 0;
            int chapter = 0;   // 0 = 从未推进(B1-b;无 ChapterAdvanced 事件时恒 0)

            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];

                if (e.Kind == EventKind.ChapterAdvanced)
                {
                    chapter = _decoder.Decode<ChapterAdvancedPayload>(e.Kind, e.Payload).Chapter;
                    continue;
                }

                if (e.Kind == EventKind.TechniqueUnlocked)
                {
                    TechniqueUnlockedPayload p =
                        _decoder.Decode<TechniqueUnlockedPayload>(e.Kind, e.Payload);
                    if (p.ActorId == actorId) unlocked.Add(p.TechniqueId);
                    continue;
                }

                if (e.Kind != EventKind.DiseaseOnset) continue;

                // 章内口径:只数落在目标章里的 onset(全局口径 targetChapter < 0 ⇒ 恒真)。
                if (targetChapter >= 0 && chapter != targetChapter) continue;

                DiseaseOnsetPayload onset = _decoder.Decode<DiseaseOnsetPayload>(e.Kind, e.Payload);
                if (!_registryById.TryGetValue(onset.DiseaseId, out DiseaseRegistryEntry row)) continue;
                if (row == null) continue;

                // 未声明的病种不算钩子(它是「数据未覆盖」,走 Undeclared 分支,不是「缺技法」)。
                IReadOnlyList<int> techniques = _index.TechniquesOf(row.DiseaseKey);
                if (techniques.Count == 0) continue;

                bool anyUnlocked = false;
                for (int k = 0; k < techniques.Count; k++)
                {
                    if (unlocked.Contains(techniques[k])) { anyUnlocked = true; break; }
                }
                // ⚠️ 用「onset 那一刻的已解锁集」判 —— 此处的 `unlocked` 只含**本行之前**的解锁。
                if (!anyUnlocked) hooks++;
            }

            return hooks;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  章切分(B1-b · 2026-11-10)
        // ══════════════════════════════════════════════════════════════════════
        // 🔁 2026-11-10:上一条 `CountHookCases` 是**全局**计数。此前「每章 1 例」退化成
        //    「整局 1 例」,正是因为调用方恒传 `chapterOf() == 0`(见 `HookCasePolicy` 头注)。
        //    本批立了 `ChapterAdvanced` 事件 ⇒ 章可以从流重建,下面两条是那段读法。
        // ⚠️ **两条并存是刻意的**:`CountHookCases(actor)` 保留为「全局」口径(既有夹具与
        //    「玩家到底见过几个钩子」的诊断都用它),章内配额走新方法。
        //    把它改写成章内口径会**静默**改掉既有语义 —— 那才是真正的坑。

        /// <summary>
        /// 当前章号(<c>0</c> = 从未推进)。
        /// <para>判据 = 流里**最后一条** <see cref="EventKind.ChapterAdvanced"/> 的
        /// <c>Chapter</c>(绝对量,故取最后一条即当前值);空流 / 无此事件 ⇒ 0。</para>
        /// </summary>
        public int CurrentChapter()
        {
            IReadOnlyList<SimEvent> events = _events();
            if (events == null) return 0;

            int chapter = 0;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.ChapterAdvanced) continue;
                chapter = _decoder.Decode<ChapterAdvancedPayload>(e.Kind, e.Payload).Chapter;
            }
            return chapter;
        }

        /// <summary>
        /// 只数**当前章**内的钩子病例 —— 这是 `HookCasePolicy.MaxPerChapter` 要的那个数。
        /// <para>前向扫一遍,维护「此刻在第几章」;onset 落在当前章才计。
        /// **后推进不追溯**:先前章里注入的钩子不会被算进本章(否则每推进一次配额就被历史吃掉)。</para>
        /// <para>⚠️ 今日无生产者写 `ChapterAdvanced` ⇒ `CurrentChapter() == 0`,本章 = 整局
        /// (与 `CountHookCases(actor)` 逐值相同)。这是**载体就位、生产者未接**的如实状态,
        /// 不是本方法的缺陷。</para>
        /// </summary>
        /// <param name="actorId">玩家 actor id(解锁事件按 actor 区分)。</param>
        /// <returns>当前章内已注入的钩子病例数。</returns>
        public int CountHookCasesInCurrentChapter(int actorId)
            => CountHookCasesCore(actorId, targetChapter: CurrentChapter());

        /// <summary>
        /// 截至当前流尾,**全局**已注入的钩子病例数(不分章)。
        /// <para>⚠️ 章内配额请用 <see cref="CountHookCasesInCurrentChapter"/>。本方法保留
        /// 全局口径是**刻意**的:既有夹具与「玩家一共见过几个钩子」的诊断都读它。
        /// 把它改写成章内口径会**静默**改掉既有语义。</para>
        /// </summary>
        /// <param name="actorId">玩家 actor id(解锁事件按 actor 区分)。</param>
        /// <returns>钩子病例条数(0 = 一例都没有)。</returns>
        public int CountHookCases(int actorId) => CountHookCasesCore(actorId, targetChapter: -1);
    }
}
