// 权威来源:design/gdd/casebook.md —— 规则二(每玩家一本,分册键 = player_id)
//          · **F-39.2**(Page / Traces / J(c,p))· 规则四 · **AC-39-13**(改写痕 = 病例流读时投影,
//          零独立存储)· AC-39-04 ①② / ⑥ · ADR-008 §三(JudgmentRecorded / JudgmentRevised)
//          · ADR-031(读侧解码抽象点 IPayloadDecoder)· ADR-010 §三 义务 13(分册态段**不存**痕)
//          · production/epics/casebook/story-003-volume-key-and-revision-trace-projection.md
//
// 三条纪律(改本文件前先读):
//   ① **痕不落盘**:`Traces` / `Stale` 只存在于本文件的**运行期返回值**里,不进任何可序列化类型、
//      不进 `CaseClosed` 载荷(AC-39-13)。分册态段(S26-1)刻意**不**存这些 —— 两边别接回去。
//   ② **`player_id` 是方法签名级约束**:没有「全部作者」重载(AC-39-04 ① 的能力面)。
//      忘传 = 编译不过;这不是运行时 if —— 允许「全量」的那一天,间接泄漏就成立了。
//   ③ **纯函数**:入参 → 新对象,零静态可变字段、零缓存(同 `CaseListOrdering` 的纪律)。
//      同一事件流 + 同一 player_id 两次求值必须逐位相同。
//
// 边界(本文件刻意不做):
//   · 不读 `IEventSink` / 不做流加载 —— 入参就是「已加载病例流的只读事件列表」。
//   · 不判「已处置」、不数「未结」、不跨病例比较(规则四三条「绝不」;那是 story-004 的守卫面)。
//   · 不做跨机 `player_id` 稳定性 —— 那是 45 的铸造契约(`AC-39-04 ③`,BLOCKED-BY-45)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.UI.Casebook
{
    /// <summary>
    /// 一笔判断在**本作者分册**里的相对位置(F-39.2 的「旧」态)。
    /// <para>⚠️ <b>整数态值,不是颜色</b> —— 42 的铅笔对勾靠它区分「当前笔 / 旧笔」,
    /// 区分通道不得只有 hue(GDD 规则七 / AC-39-12)。</para>
    /// <para>⚠️ <b>本枚举不落盘</b>:它是读时算出来的(AC-39-13)。分册态段里没有它。</para>
    /// </summary>
    public enum JudgmentTraceState
    {
        /// <summary>当前笔 = 该作者在该病例上的**最后一笔**判断。</summary>
        Current = 0,

        /// <summary>旧笔 = 被后续笔覆盖过。</summary>
        Stale = 1,
    }

    /// <summary>
    /// 一笔判断(病例流里的一条 `JudgmentRecorded` / `JudgmentRevised` 的作者轴投影)。
    /// <para>值类型、全整数、无引用字段 —— 与 <see cref="CaseEntry"/> 同款,
    /// 反射面天然没有可落盘的东西。</para>
    /// </summary>
    public readonly struct JudgmentTrace
    {
        /// <summary>所属病例。</summary>
        public readonly CaseId Case;

        /// <summary>该判断事件自身的 `SimEvent.Tick`(全序第一分量)。</summary>
        public readonly long Tick;

        /// <summary>该判断事件的主机发号 `SimEvent.Seq`(全序第三分量,同 tick 内定序)。</summary>
        public readonly long Seq;

        /// <summary>病名册词条 id(`case_judgment_lexicon` 的版本化 ordinal)。</summary>
        public readonly int LexiconId;

        /// <summary>置信档位(u8 值域;档位语义归 8)。</summary>
        public readonly byte Confidence;

        /// <summary>当前笔 / 旧笔(见 <see cref="JudgmentTraceState"/>)。</summary>
        public readonly JudgmentTraceState State;

        internal JudgmentTrace(CaseId c, long tick, long seq, int lexiconId, byte confidence,
                               JudgmentTraceState state)
        {
            Case = c; Tick = tick; Seq = seq;
            LexiconId = lexiconId; Confidence = confidence; State = state;
        }

        /// <summary>旧的写在前面(升序),便于「翻改写史」按序显示。</summary>
        public int CompareTo(JudgmentTrace other)
        {
            // ⚠️ 禁 `a - b`:`Tick` 是 long,极值处回绕会静默反转(同 `CaseSortKey` 的纪律)
            int c = Tick.CompareTo(other.Tick);
            return c != 0 ? c : Seq.CompareTo(other.Seq);
        }
    }

    /// <summary>
    /// 一页 = 「一个病例」在该玩家分册里的样子(F-39.2 的 `Page(p, c)`)。
    /// <para>⚠️ <b>零落笔的病例也有页</b>(`Traces` 为空 ⇒ `IsBlank`),因为「页面在、痕无」
    /// 与「这个病人不在我的册子里」是两件不同的事(GDD 规则七:空行可聚焦)。</para>
    /// </summary>
    public readonly struct VolumePage
    {
        /// <summary>该页对应病例。</summary>
        public readonly CaseId Case;

        /// <summary>本作者在该病例上的全部判断,按 <c>(Tick, Seq)</c> 升序;最后一笔 = <see cref="Current"/>。</summary>
        public readonly IReadOnlyList<JudgmentTrace> Traces;

        /// <summary>构造一页。</summary>
        public VolumePage(CaseId c, IReadOnlyList<JudgmentTrace> traces)
        {
            Case = c;
            Traces = traces ?? Array.Empty<JudgmentTrace>();
        }

        /// <summary>本作者在该病例上的笔数(0 ⇒ 空白页)。</summary>
        public int TraceCount => Traces.Count;

        /// <summary>空白页 = 本作者对该病例零落笔。**不是**「页不存在」。</summary>
        public bool IsBlank => Traces.Count == 0;

        /// <summary>当前笔(最后一笔);空白页 ⇒ `null`。</summary>
        public JudgmentTrace? Current => Traces.Count == 0 ? (JudgmentTrace?)null : Traces[Traces.Count - 1];
    }

    /// <summary>
    /// 一本分册 = 某玩家的全部页(F-39.2 的 <c>VolumeKey(p)</c> 之下的整体)。
    /// <para>页序 = <see cref="CaseListOrdering"/> 的全序(串接组连续),**本类不二次排序**。</para>
    /// </summary>
    public sealed class ProjectedVolume
    {
        /// <summary>分册键 = `player_id`(规则二)。</summary>
        public readonly int PlayerId;

        /// <summary>按病例全序排列的页(含空白页)。</summary>
        public readonly IReadOnlyList<VolumePage> Pages;

        /// <summary>构造一本分册。</summary>
        public ProjectedVolume(int playerId, IReadOnlyList<VolumePage> pages)
        {
            PlayerId = playerId;
            Pages = pages ?? Array.Empty<VolumePage>();
        }

        /// <summary>页数。</summary>
        public int Count => Pages.Count;

        /// <summary>全册总笔数(派生,不另存 —— 与 <see cref="Pages"/> 不会分叉)。</summary>
        public int TotalTraceCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Pages.Count; i++) n += Pages[i].TraceCount;
                return n;
            }
        }
    }

    /// <summary>
    /// 分册投影(F-39.2 的实现体):**纯函数 · 零落盘 · 零可变状态**。
    /// <para>输入 = 已加载病例流的只读事件列表 + 分册键 + 载荷解码器;输出 = 新对象。</para>
    /// <para><b>为什么需要 decoder</b>:判断的作者 / 词条 / 置信档位都在**载荷**里(ADR-008 §三),
    /// 而 `SimEvent` 头只带 `PayloadRef`。读侧唯一合法解码路径是 <see cref="IPayloadDecoder"/>
    /// (ADR-031)—— 本类**不**自己碰 blob 池。</para>
    /// </summary>
    public static class CasebookVolumeProjection
    {
        /// <summary>
        /// 求一本分册。
        /// </summary>
        /// <param name="events">病例流事件(顺序任意 —— 相邻顺序不影响结果)。</param>
        /// <param name="playerId"><b>必选</b>分册键。⚠️ 刻意**没有**缺省值、没有「全部作者」重载 ——
        /// 那是能力面约束(AC-39-04 ①),不是风格问题。</param>
        /// <param name="decoder">载荷解码器(ADR-031)。</param>
        /// <returns>页序 = 病例全序;每页只含 <paramref name="playerId"/> 自己的判断。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="decoder"/> 为 null。</exception>
        public static ProjectedVolume ProjectVolume(
            IReadOnlyList<SimEvent> events, int playerId, IPayloadDecoder decoder)
        {
            if (decoder == null)
                throw new ArgumentNullException(
                    nameof(decoder), "分册投影必须能读判断载荷(作者 / 词条 / 置信都在载荷里)");

            // 页序 = 病例全序(含空白页所需的「全部病例」),复用 story-002 的纯函数,不另写一份
            CaseEntry[] ordered = CaseListOrdering.OrderedList(events);

            // 一次扫描:只解该作者的判断事件,按病例归并
            var byCase = new Dictionary<CaseId, List<JudgmentTrace>>();
            int count = events?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                SimEvent e = events[i];
                CaseId caseId;
                int author;
                int lexiconId;
                byte confidence;

                switch (e.Kind)
                {
                    case EventKind.JudgmentRecorded:
                    {
                        JudgmentRecordedPayload p =
                            decoder.Decode<JudgmentRecordedPayload>(EventKind.JudgmentRecorded, e.Payload);
                        caseId = p.CaseId;
                        author = p.AuthorPlayerId; lexiconId = p.LexiconId; confidence = p.Confidence;
                        break;
                    }
                    case EventKind.JudgmentRevised:
                    {
                        JudgmentRevisedPayload p =
                            decoder.Decode<JudgmentRevisedPayload>(EventKind.JudgmentRevised, e.Payload);
                        caseId = p.CaseId;
                        author = p.AuthorPlayerId; lexiconId = p.LexiconId; confidence = p.Confidence;
                        break;
                    }
                    default:
                        continue;   // 病例流里还有 CaseOpened / CaseClosed 等,它们不是判断
                }

                // ⚠️ 作者过滤在**解码之后**(作者住在载荷里)。这是能力面判据的**数据侧**落实:
                //    别人的笔从不进入本册的容器,后面就没有「漏筛」的机会。
                if (author != playerId) continue;

                if (!byCase.TryGetValue(caseId, out List<JudgmentTrace> list))
                {
                    list = new List<JudgmentTrace>();
                    byCase[caseId] = list;
                }
                // State 先全填 Stale,归并结束后把末笔改成 Current —— 一次定标,不在插入时猜
                list.Add(new JudgmentTrace(caseId, e.Tick, e.Seq, lexiconId, confidence, JudgmentTraceState.Stale));
            }

            var pages = new VolumePage[ordered.Length];
            for (int i = 0; i < ordered.Length; i++)
            {
                CaseEntry entry = ordered[i];
                if (!byCase.TryGetValue(entry.Case, out List<JudgmentTrace> traces))
                {
                    pages[i] = new VolumePage(entry.Case, null);   // 空白页:页在、痕无
                    continue;
                }

                traces.Sort((a, b) => a.CompareTo(b));   // (Tick, Seq) 升序
                var finalized = new JudgmentTrace[traces.Count];
                for (int k = 0; k < traces.Count; k++)
                {
                    finalized[k] = new JudgmentTrace(
                        traces[k].Case, traces[k].Tick, traces[k].Seq,
                        traces[k].LexiconId, traces[k].Confidence,
                        k == traces.Count - 1 ? JudgmentTraceState.Current : JudgmentTraceState.Stale);
                }
                pages[i] = new VolumePage(entry.Case, finalized);
            }

            return new ProjectedVolume(playerId, pages);
        }
    }
}
