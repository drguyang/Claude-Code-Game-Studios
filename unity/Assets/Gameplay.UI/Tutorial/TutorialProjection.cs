// S25-1 · tutorial-002 —— 48 的**只读投影 Proj**(纯函数,零持久化)。
//
// 权威来源:
//   · `design/gdd/tutorial-and-onboarding.md` 规则四(F-48.1 `Completion_s(p) ⟺ Predicate_s(Proj(p))`)
//       · 规则五(零设门)· 规则七(只读投影,不写流)· 规则八(内容表的 `predicateKind`)
//   · `docs/architecture/adr-009` §一 Q1 判据 —— 引导进度 = **派生态**,不进流、不落盘
//   · `docs/architecture/adr-019` §三 —— 51 同构先例(边界层只读消费者)
//   · `docs/architecture/adr-025` §① —— 装配清单(本装配 `Gameplay.UI.Tutorial`)
//
// ═══ 装配落点与 TR-tutorial-003(partial)═══════════════════════════════════
// `TR-tutorial-003` 的原问题是「48 的 `Proj` 落哪个 asmdef」,ADR-025 §① 未点名(故记 partial)。
// 本装配 = **就近解释**:`Gameplay.UI.Tutorial`,引用集**恰 = { `Gameplay.Presentation` }**。
// 理由 = AC-48-11 的结构性表达:`IEventSink` / `IEventAuthority` 声明在 `Sim.Contracts`,
// 而 `Gameplay.UI` 引用了 `Sim.Contracts` ⇒ 48 若住 `Gameplay.UI`,该 AC 在**编译后**就不可能成立。
// ⇒ **架构回写前不得把本 story 记成 TR closure**(承 §④「新 asmdef 须追加进表」:
//   `ADR-025 §①` 已同批追加本行;`AssemblyGates.Manifest` 已同批登记,否则 b3 封闭性门转红)。
//
// ═══ 本类做什么 / 不做什么 ═══════════════════════════════════════════════
// 做:把「内容表的六步」与「事件的只读事实序」合成**六个布尔**(`Completion_s`)。
// 不做:闩锁 / 边沿 / 调度(Story 003)· 媒体时序(Story 004)· 任何写路径(规则七)。
//
// ⚠️ **本装配看不见** `SimEvent` / `EventKind` / `Fix` / `IEventSink` —— 它们分别住
//    `Sim.Contracts` 与 `Sim`,而本装配**零引用**这两支(AC-48-04 / AC-48-11)。
//    这不是自觉,是编译期事实:没有引用就写不出那个类型名。
// ⚠️ **数值冻结**:本类零旋钮(全机制);媒体时序类旋钮归 Story 004。

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using DaYiJingCheng.Gameplay.Presentation.Tutorial;

namespace DaYiJingCheng.Gameplay.UI.Tutorial
{
    /// <summary>一步的完成判据(只读形;`Completion_s` 的单步结果)。</summary>
    public readonly struct TutorialStepCompletion
    {
        /// <summary>步 id ∈ 六步闭集(<c>observe</c> / <c>judge</c> / <c>treat</c> / <c>close</c> /
        /// <c>archive</c> / <c>manage</c>;取自内容表,不在此处另立枚举)。</summary>
        public readonly string StepId;

        /// <summary>该步的判据 Kind 名(内容表原值;`archive` 步为字面 <c>Checkpoint</c>)。</summary>
        public readonly string PredicateKind;

        /// <summary>该步的内容是否**已被做过**(单调:一旦为真,永不再假)。</summary>
        public readonly bool Completed;

        /// <summary>创建一条单步结果。</summary>
        public TutorialStepCompletion(string stepId, string predicateKind, bool completed)
        {
            StepId = stepId;
            PredicateKind = predicateKind;
            Completed = completed;
        }
    }

    /// <summary>
    /// 一名玩家的教学进度**读时投影**(`Proj(p)` 的输出)。
    /// <para>⚠️ <b>不可变</b>:全部字段只读,集合经 <see cref="ReadOnlyCollection{T}"/> 承载;
    /// 无 `Add` / `Sort` / `Clear`。它**不是**进度存储 —— 每次读取现算(规则四)。</para>
    /// </summary>
    public readonly struct TutorialProgressView
    {
        private static readonly ReadOnlyCollection<TutorialStepCompletion> NoSteps =
            Array.AsReadOnly(Array.Empty<TutorialStepCompletion>());

        private readonly ReadOnlyCollection<TutorialStepCompletion> _steps;
        private readonly int _playerId;

        /// <summary>构造(供 <see cref="TutorialProjection.Project"/> 用)。</summary>
        public TutorialProgressView(int playerId, IReadOnlyList<TutorialStepCompletion> steps)
        {
            _playerId = playerId;
            if (steps == null || steps.Count == 0)
            {
                _steps = NoSteps;
                return;
            }

            var copy = new TutorialStepCompletion[steps.Count];
            for (int i = 0; i < steps.Count; i++) copy[i] = steps[i];
            _steps = Array.AsReadOnly(copy);
        }

        /// <summary>本视图是**谁的**进度。</summary>
        public int PlayerId => _playerId;

        /// <summary>六步(内容表行序;原样保序)。</summary>
        public IReadOnlyList<TutorialStepCompletion> Steps =>
            (IReadOnlyList<TutorialStepCompletion>)_steps ?? NoSteps;

        /// <summary>步数(内容表为空 ⇒ 0)。</summary>
        public int StepCount => Steps.Count;

        /// <summary>已完成步数。</summary>
        public int CompletedCount
        {
            get
            {
                var steps = Steps;
                int n = 0;
                for (int i = 0; i < steps.Count; i++) if (steps[i].Completed) n++;
                return n;
            }
        }

        /// <summary>
        /// 六步是否**全部**完成。
        /// <para>⚠️ 空步列表 ⇒ **false**,不取「真空为真」—— 文件缺失 / 解码失败时
        /// 「全部完成」会静默把整条教学链判成已走完(空集冒充绿的经典形态)。</para>
        /// </summary>
        public bool AllCompleted
        {
            get
            {
                var steps = Steps;
                return steps.Count > 0 && CompletedCount == steps.Count;
            }
        }

        /// <summary>某一步是否完成(未知名 ⇒ false,不抛 —— 查询面不该因拼错而崩)。</summary>
        public bool IsCompleted(string stepId)
        {
            if (string.IsNullOrEmpty(stepId)) return false;
            var steps = Steps;
            for (int i = 0; i < steps.Count; i++)
                if (string.Equals(steps[i].StepId, stepId, StringComparison.Ordinal))
                    return steps[i].Completed;
            return false;
        }

        /// <summary>
        /// 规范串(判据用):同一输入两次求值 ⇒ 逐字一致。
        /// <para>格式稳定,仅供**相等性**比较,**不是**存档格式(规则四:48 永不落盘)。</para>
        /// </summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            sb.Append(PlayerId).Append('|');
            var steps = Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                sb.Append(steps[i].StepId).Append(':')
                  .Append(steps[i].Completed ? '1' : '0').Append(';');
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 48 的只读投影(`Proj(p)`)。六步各自独立求值,不存在蕴含链(F-48.2)。
    /// </summary>
    public static class TutorialProjection
    {
        /// <summary>`archive` 步的判据字面(GDD 规则八:该步取字面 <c>Checkpoint</c>,不是 `Kind`)。</summary>
        public const string CheckpointPredicateKind = "Checkpoint";

        /// <summary>
        /// 判据**析取组**:组内任意 Kind 出现即满足该步。
        /// <para>⚠️ 只有一组,且它**不是**发明出来的:F-48.1 第 ③ 步逐字写
        /// 「`EmergencyAttempt` / `DrugTreatmentApplied`(initiator=p)」—— 是**析取**。
        /// 而内容表 schema 的 `predicateKind` 是**单值**(规则八 · 校验规则 6),
        /// 种子数据取 `DrugTreatmentApplied` ⇒ 另一支只能在本表登记。
        /// <b>登记缺口(如实)</b>:内容表无法表达析取 ⇒ 若该步内容日后只想认其中一支,
        /// 数据侧无从区分;修法 = 内容 schema 支持多 Kind,归 48 的数据轮(不是本 story)。</para>
        /// </summary>
        private static readonly string[][] PredicateGroups =
        {
            new[] { "EmergencyAttempt", "DrugTreatmentApplied" },
        };

        /// <summary>
        /// 计算玩家 <paramref name="playerId"/> 的六步完成视图。
        /// </summary>
        /// <param name="feed">只读事实序 + 7a 回执(边界层注入)。</param>
        /// <param name="steps">内容表的步行(<c>TutorialContentTable.Steps</c>)。</param>
        /// <param name="playerId">玩家 id(联机各人一份进度,承 39 分册同构)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="feed"/> 或 <paramref name="steps"/> 为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="playerId"/> &lt; 0
        /// (负值 = 无作者哨兵,拿它求值会让**所有**带作者轴的事件都算命中 —— 必须 fail-loud)。</exception>
        /// <remarks>
        /// <b>作者轴过滤</b>:带作者轴的事件的作者 ≠ 本玩家 ⇒ **不计**;
        /// 无作者轴的事件(世界级:立案 / 结案 / 建造)对**每名**玩家都成立 ——
        /// 那是 F-48.1 的逐字口径(`CaseOpened(patient=p)` / `StructurePlaced(23)` 都没有作者轴),
        /// 不是遗漏。AC-48-09 只要求「各有作者轴的谓词」各自独立。
        /// </remarks>
        public static TutorialProgressView Project(
            ITutorialProjectionFeed feed, IReadOnlyList<TutorialStepRow> steps, int playerId)
        {
            if (feed == null) throw new ArgumentNullException(nameof(feed));
            if (steps == null) throw new ArgumentNullException(nameof(steps),
                "未给出内容表步行 —— 空内容 ⇒ 六步全缺 ⇒ 「全部完成」将永远不成立(静默失效)");
            if (playerId < 0)
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId,
                    "player_id 为负 —— 它是「无作者轴」的哨兵(TutorialEventFact.NoAuthor)," +
                    "拿它求值会让每一次落笔 / 处置都算命中(AC-48-09 的反面)");

            IReadOnlyList<TutorialEventFact> facts = feed.Facts;
            var results = new TutorialStepCompletion[steps.Count];
            for (int i = 0; i < steps.Count; i++)
            {
                TutorialStepRow step = steps[i];
                results[i] = new TutorialStepCompletion(
                    step.Id, step.PredicateKind,
                    IsSatisfied(step.PredicateKind, playerId, facts, feed.HasCheckpointWritten));
            }

            return new TutorialProgressView(playerId, results);
        }

        private static bool IsSatisfied(
            string predicateKind, int playerId, IReadOnlyList<TutorialEventFact> facts,
            bool checkpointWritten)
        {
            if (string.IsNullOrEmpty(predicateKind)) return false;   // 数据缺键 ⇒ 不成立(不抛:内容轮负责修)

            // ⑤ 册子:判据不是流事件(存档头不落流,ADR-024 白名单无该 Kind)⇒ 走 7a 回执。
            if (string.Equals(predicateKind, CheckpointPredicateKind, StringComparison.Ordinal))
                return checkpointWritten;

            int n = facts.Count;
            for (int i = 0; i < n; i++)
            {
                TutorialEventFact fact = facts[i];
                if (!KindMatches(fact.KindName, predicateKind)) continue;
                // 无作者轴(世界级)⇒ 对所有人都成立;有作者轴 ⇒ 只算作者本人。
                if (fact.AuthorPlayerId == TutorialEventFact.NoAuthor ||
                    fact.AuthorPlayerId == playerId)
                    return true;
            }
            return false;
        }

        private static bool KindMatches(string factKind, string predicateKind)
        {
            if (string.Equals(factKind, predicateKind, StringComparison.Ordinal)) return true;
            string[] group = GroupOf(predicateKind);
            if (group == null) return false;
            for (int i = 0; i < group.Length; i++)
                if (string.Equals(group[i], factKind, StringComparison.Ordinal)) return true;
            return false;
        }

        private static string[] GroupOf(string predicateKind)
        {
            for (int g = 0; g < PredicateGroups.Length; g++)
            {
                string[] group = PredicateGroups[g];
                for (int i = 0; i < group.Length; i++)
                    if (string.Equals(group[i], predicateKind, StringComparison.Ordinal)) return group;
            }
            return null;
        }
    }
}
