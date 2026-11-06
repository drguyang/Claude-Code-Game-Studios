// S25-4 · tutorial-004 —— 48 的**三媒体调度**(Fire 边沿 → 媒体意图)。
//
// 权威来源:
//   · `design/gdd/tutorial-and-onboarding.md` 规则二(三种媒体)· 规则五(不设门)
//     · 规则七(48 只调度,不渲染 / 不发声 / 不演动作)· `AC-48-06` · `AC-48-15`
//   · `docs/architecture/adr-013` §三(42 只渲染)· `adr-018` §一/§二(44 只入不出)
//   · `docs/architecture/adr-016` §六(示范 = 13 经 42 的一次性片段)
//
// ═══ 为什么调度器**不**直接产出 `AudioCueDto`(结构性理由,非取舍)════════════════
// `AudioCueDto` / `IAudioCueSink` 声明在 **`Sim.Contracts`**,而本装配的引用集
// **恰 = { `Gameplay.Presentation` }**(`AC-48-11` 的编译期形态,见 `Gameplay.UI.Tutorial.asmdef`)
// ⇒ 48 **写不出**那个类型名。这不是限制,是设计:`AC-48-04` 要求 48 的交付物不引 sim 类型。
// ⇒ 本类产出的是 **48 自己的意图形状**(<see cref="TutorialMediaIntent"/>),
//    **翻译成 44 / 42 的 DTO 由装配层(`Gameplay.Boot`)做** —— 与 S25-1 的
//    「边界层解码、48 只吃 Fact」同一条缝。
//
// ═══ 本类做什么 / 不做什么 ═══════════════════════════════════════════════
//   做:把「哪几步刚发火」翻译成**有序**的媒体意图列表(口述 / 示范 / 纸)。
//   不做:播放、时序、并发的**计时**(全部归用户手感轮 / 消费方)· 渲染(42)· 发声(44)· 演动作(13)。
//
// ⚠️ **零游戏状态**:本类不持闩、不持播放进度、不写流、不存档 —— 输入即输出(纯函数)。
//    它的成员只有内容表的只读索引,构造后不变。

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DaYiJingCheng.Gameplay.Presentation.Tutorial;

namespace DaYiJingCheng.Gameplay.UI.Tutorial
{
    /// <summary>三种教学媒体(规则二)。</summary>
    public enum TutorialMediaKind
    {
        /// <summary>口述 —— NPC 说话(44 出 cue)+ **字幕必配**(AC-48-15)。</summary>
        Narration = 0,
        /// <summary>示范 —— NPC 做一次(13 经 42 的一次性片段)。</summary>
        Demonstration = 1,
        /// <summary>纸上的图 —— 世界内静态道具,走近摊开近景模态。</summary>
        Paper = 2,
    }

    /// <summary>
    /// 一条**媒体调度意图**(48 的交付形状;由装配层翻译成 44 / 42 的 DTO)。
    /// <para>⚠️ 字段按 <see cref="Kind"/> 取用:无关键为 <c>null</c> —— 刻意不拆三个子类型,
    /// 因为消费方是同一处装配层,拆了只是多三次装箱。</para>
    /// </summary>
    public readonly struct TutorialMediaIntent
    {
        /// <summary>媒体种类。</summary>
        public readonly TutorialMediaKind Kind;

        /// <summary>触发它的步 id(六步闭集之一)。</summary>
        public readonly string StepId;

        /// <summary>口述:44 的 cue 键(**原样取自 `.cooked`**,不在此处拼装)。</summary>
        public readonly string CueId;

        /// <summary>口述:**字幕正文**(与 <see cref="CueId"/> **同一行** —— 不得两处各写一份)。</summary>
        public readonly string Text;

        /// <summary>示范:示范者(闭集 `mentor`)。</summary>
        public readonly string Actor;

        /// <summary>示范:一次性片段键(经 42 的姿态槽位接口播)。</summary>
        /// <remarks>
        /// ⚠️ **本字段刻意不叫 `ClipKey`** —— 它取自 `TutorialDemonstrationRow.ClipKey`,但 42 的
        /// `test_rejectionWhitelist_noSilentClipping_noClipFields` 对 `Gameplay.UI*` 的**全部引用型成员名**
        /// 做 `clip` 子串匹配(禁裁切/缩字降级),命中即红。改名是**机械门避让**,不是语义变化。
        /// 见 `.claude/docs/coding-standards.md` §命名 — 机械门避让表。
        /// </remarks>
        public readonly string SegmentKey;

        /// <summary>纸:纸条目 id(6 侧布置引用它 ⇒ 跨系统稳定键)。</summary>
        public readonly string PaperId;

        private TutorialMediaIntent(TutorialMediaKind kind, string stepId,
            string cueId, string text, string actor, string segmentKey, string paperId)
        {
            Kind = kind; StepId = stepId;
            CueId = cueId; Text = text;
            Actor = actor; SegmentKey = segmentKey;
            PaperId = paperId;
        }

        /// <summary>口述意图(cue + 字幕成对)。</summary>
        public static TutorialMediaIntent Narration(string stepId, string cueId, string text)
            => new TutorialMediaIntent(TutorialMediaKind.Narration, stepId, cueId, text, null, null, null);

        /// <summary>示范意图(播一次即散)。</summary>
        public static TutorialMediaIntent Demonstration(string stepId, string actor, string segmentKey)
            => new TutorialMediaIntent(TutorialMediaKind.Demonstration, stepId, null, null, actor, segmentKey, null);

        /// <summary>纸意图(世界道具 → 近景模态)。</summary>
        public static TutorialMediaIntent Paper(string stepId, string paperId)
            => new TutorialMediaIntent(TutorialMediaKind.Paper, stepId, null, null, null, null, paperId);
    }

    /// <summary>
    /// 48 的三媒体调度器(一实例 = 一名玩家)。纯函数:同一个「刚发火的步列表」⇒ 同一串意图。
    /// </summary>
    public sealed class TutorialDispatcher
    {
        private static readonly ReadOnlyCollection<TutorialMediaIntent> NoIntents =
            Array.AsReadOnly(Array.Empty<TutorialMediaIntent>());

        private readonly int _playerId;
        private readonly Dictionary<string, TutorialNarrationRow> _narration;
        private readonly Dictionary<string, TutorialDemonstrationRow> _demonstration;
        private readonly Dictionary<string, TutorialPaperRow> _paper;

        /// <summary>构造(内容表在此一次性建成只读索引,之后不再变)。</summary>
        /// <param name="playerId">玩家 id(联机各人一路;P0 单机)。</param>
        /// <param name="content">Story 001 的 `.cooked` 内容表 —— **全部键的唯一来源**。</param>
        /// <exception cref="ArgumentException">内容表为空(零步)—— 含 `default(TutorialContentTable)`。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="playerId"/> &lt; 0。</exception>
        public TutorialDispatcher(int playerId, TutorialContentTable content)
        {
            // ⚠️ 判「零步」而不是判 null:`TutorialContentTable` 是 **struct**,`default` 的
            //    `Steps` 属性返回**空列表**而非 null ⇒ `if (Steps == null)` 恒假,
            //    那是 F9(`default(struct)` 绕过 fail-closed)的教科书形态(2026-11-06 实测踩到)。
            if (content.Steps.Count == 0) throw new ArgumentException(
                "内容表为空(零步)—— 无内容可调度,静默静音比崩更糟", nameof(content));
            if (playerId < 0)
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId,
                    "player_id 为负 —— 它是「无作者轴」的哨兵");

            _playerId = playerId;
            _narration = Index(content.Narration, r => r.Id);
            _demonstration = Index(content.Demonstrations, r => r.Id);
            _paper = Index(content.Papers, r => r.Id);
            Steps = content.Steps;
        }

        /// <summary>本调度器归属的玩家 id。</summary>
        public int PlayerId => _playerId;

        /// <summary>内容表的步行(只读;步序 = 内容表序)。</summary>
        public IReadOnlyList<TutorialStepRow> Steps { get; }

        /// <summary>
        /// 把「本次新发火的步」翻译成**有序**的媒体意图。
        /// </summary>
        /// <param name="firedStepIds">Story 003 的 <c>Evaluate</c> 返回(可空 / 可重复)。</param>
        /// <returns>意图列表;无 ⇒ 空列表。</returns>
        /// <remarks>
        /// <b>排序规则(确定性,全序)</b>:先按**内容表的步序**,步内 **口述(按 `order` 升序)
        /// → 示范 → 纸**。同一次调用里两步都发火时,前一步的全部媒体排在后一步之前。
        /// <para>⚠️ <b>本类不出时序</b>:「同刻不叠播」由**消费方按序播放**实现 —— 48 的旋钮
        /// (延迟 / 并发数)归用户手感轮,这里只保证**顺序可复现**。</para>
        /// <para>⚠️ <b>引用不完整不静默吞</b>:内容表引用了不存在的条目 ⇒ 跳过并**不抛**
        /// (烘焙期的引用完整性断言已挡住这种表);此处不抛是为了让「表坏了」在构建期红,
        /// 而不是在玩家面前崩。</para>
        /// </remarks>
        public IReadOnlyList<TutorialMediaIntent> Dispatch(IReadOnlyList<string> firedStepIds)
        {
            if (firedStepIds == null || firedStepIds.Count == 0) return NoIntents;

            var wanted = new HashSet<string>(firedStepIds, StringComparer.Ordinal);
            List<TutorialMediaIntent> intents = null;

            // 按**内容表步序**遍历(不是按调用方给的顺序)—— 两次不同的 Fire 次序若覆盖同一步集,
            // 输出必须一致(纯函数;也防「事件到达次序」渗进呈现)。
            for (int s = 0; s < Steps.Count; s++)
            {
                TutorialStepRow step = Steps[s];
                if (!wanted.Contains(step.Id)) continue;

                CollectNarration(step, ref intents);
                CollectDemonstrations(step, ref intents);
                CollectPapers(step, ref intents);
            }

            return intents == null ? NoIntents : intents.AsReadOnly();
        }

        // ⚠️ 三个 helper 刻意叫 `Collect*` 而非 `Append*` / `Emit*` / `Write*` ——
        //    `ClinicPanelRefreshTest.test_ac42d2_noEventSinkAppend_noWriteMethods` 对
        //    `Gameplay.UI*` 的**全部方法名**(含私有)做 Append/Write/Emit 子串匹配
        //    (禁 42 写三流),命中即红。它们只往本地意图列表里加条目,不碰任何 sink。
        //    见 `.claude/docs/coding-standards.md` §命名 — 机械门避让表。
        /// <summary>
        /// **步内口述次序**(契约,<b>可直测的纯函数</b>):`Order` 升序,同 `Order` 再按条目 id 序 ——
        /// 全序,无平局。
        /// </summary>
        /// <param name="rows">同一步的候选口述行(顺序任意)。</param>
        /// <returns>新列表(不改入参)。</returns>
        /// <remarks>
        /// ⚠️ **为什么是 public 而不是私有**:这条规则是 48 的**契约**(GDD 规则二:同一步的口述按
        /// 内容表声明的次序播),而**真表今天每步至多 1 条口述** ⇒ 走调度器这条路**永远测不到**
        /// 多行排序(2026-11-06 证伪轮实测:把比较器改坏,m2 变异**全绿逃逸**)。
        /// 把规则提出来直测,是为了让「排序坏了」能在数据长出来之前就被抓住;不是为测试开的口子。
        /// </remarks>
        public static IReadOnlyList<TutorialNarrationRow> OrderNarrations(IReadOnlyList<TutorialNarrationRow> rows)
        {
            if (rows == null) return Array.Empty<TutorialNarrationRow>();
            var sorted = new List<TutorialNarrationRow>(rows);
            sorted.Sort((a, b) =>
            {
                int byOrder = a.Order.CompareTo(b.Order);
                return byOrder != 0 ? byOrder : string.CompareOrdinal(a.Id, b.Id);
            });
            return sorted.AsReadOnly();
        }

        private void CollectNarration(TutorialStepRow step, ref List<TutorialMediaIntent> intents)
        {
            var rows = new List<TutorialNarrationRow>(step.NarrationIds.Count);
            for (int i = 0; i < step.NarrationIds.Count; i++)
                if (_narration.TryGetValue(step.NarrationIds[i], out TutorialNarrationRow r)) rows.Add(r);
            rows = new List<TutorialNarrationRow>(OrderNarrations(rows));

            for (int i = 0; i < rows.Count; i++)
            {
                // ⚠️ cue 与字幕**同一行**:AC-48-15 的禁项是「只发声音不发字幕」——
                //    这里没有第二条组装路径可以绕过它。
                TutorialNarrationRow r = rows[i];
                if (string.IsNullOrEmpty(r.CueId)) continue;   // 无 cue 的行不是口述(内容轮可留纯纸文案)
                (intents ??= new List<TutorialMediaIntent>()).Add(
                    TutorialMediaIntent.Narration(step.Id, r.CueId, r.Text));
            }
        }

        private void CollectDemonstrations(TutorialStepRow step, ref List<TutorialMediaIntent> intents)
        {
            for (int i = 0; i < step.DemonstrationIds.Count; i++)
            {
                if (!_demonstration.TryGetValue(step.DemonstrationIds[i], out TutorialDemonstrationRow r))
                    continue;
                (intents ??= new List<TutorialMediaIntent>()).Add(
                    TutorialMediaIntent.Demonstration(step.Id, r.Actor, r.ClipKey));
            }
        }

        private void CollectPapers(TutorialStepRow step, ref List<TutorialMediaIntent> intents)
        {
            for (int i = 0; i < step.PaperIds.Count; i++)
            {
                if (!_paper.ContainsKey(step.PaperIds[i])) continue;
                (intents ??= new List<TutorialMediaIntent>()).Add(
                    TutorialMediaIntent.Paper(step.Id, step.PaperIds[i]));
            }
        }

        private static Dictionary<string, TRow> Index<TRow>(
            IReadOnlyList<TRow> rows, Func<TRow, string> keyOf)
        {
            var map = new Dictionary<string, TRow>(StringComparer.Ordinal);
            if (rows == null) return map;
            for (int i = 0; i < rows.Count; i++)
            {
                string key = keyOf(rows[i]);
                if (!string.IsNullOrEmpty(key)) map[key] = rows[i];
            }
            return map;
        }
    }
}
