// S25-1 · tutorial-002 —— 48 的**只读投影输入面**(边界层)。
//
// 权威来源:
//   · `design/gdd/tutorial-and-onboarding.md` 规则四 / 规则七 / F-48.1
//       —— `Proj(p)` = **边界层只读投影查询**(承 `AC-51-A2` 同型);48 不写三流,
//          交付物不引 `SimEvent` / `PatientState` / `Fix`(AC-48-04),程序集引用白名单
//          不含声明 `IEventSink` / `IEventAuthority` 的程序集(AC-48-11)。
//   · `docs/architecture/adr-019` §三 —— 51 同构先例:边界层只读消费者,零 `Append`。
//   · `docs/architecture/adr-025` §① —— 装配清单(本文件住 `Gameplay.Presentation`;48 的
//       投影模块住 `Gameplay.UI.Tutorial`,两者分装配)。
//
// ═══ 为什么本文件住 `Gameplay.Presentation`,而 48 的投影住 `Gameplay.UI.Tutorial` ═══
// 本文件**必须看得见** `SimEvent` / `EventKind` / 三个带作者轴的载荷 —— 它把事件解成
// 「Kind 名 + 作者玩家 id」;而 48 的投影**必须看不见**它们(AC-48-11 的结构性表达:
// 没有引用就**无法写出那个类型名**,C# 要求直接引用才能命名类型)。
// ⇒ 解码留在边界层,48 只吃 <see cref="TutorialEventFact"/>(纯整数 + Kind 名字符串)。
//
// ⚠️ 顺序轴 = `(Tick, Patient, Seq)` 的入流序(不自排):本类的输出**原样保序**,
//    重排即第二真源(承 `TutorialContentTable` 「行序是契约的一部分」同一条纪律)。
// ⚠️ 本类**不缓存**:`Facts` 每次读取现算(纯函数,同流前缀同输出)。调用方须在**边沿**
//    之外不重复调用(48 侧闩锁归 Story 003,`F-48.1` 的摊销 O(1) 由它给)。

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Tutorial
{
    /// <summary>
    /// 一条事件的**只读事实**(48 能看见的最小形状)。
    /// <para>刻意**不含**患者 id / 载荷 / 流别 —— 六步谓词一个字都用不上它们,
    /// 多带一个字段就多一个 48 侧的语义面。</para>
    /// </summary>
    public readonly struct TutorialEventFact
    {
        /// <summary>「本事件没有作者轴」的哨兵(= −1;与 `PatientId.None` 同值,**语义不同**)。</summary>
        public const int NoAuthor = -1;

        /// <summary>入流 tick(`SimEvent.Tick`;仅作观测,不作为判据)。</summary>
        public readonly long Tick;

        /// <summary>`EventKind` 的成员名(如 <c>CaseOpened</c>)。**唯一与内容表对照的键** ——
        /// 内容表的 `steps[].predicateKind` 存的就是这个名字(规则八 · 校验规则 6)。</summary>
        public readonly string KindName;

        /// <summary>造成本事件的玩家 id;世界级事件 = <see cref="NoAuthor"/>。</summary>
        public readonly int AuthorPlayerId;

        /// <summary>创建一条事实。</summary>
        public TutorialEventFact(long tick, string kindName, int authorPlayerId)
        {
            Tick = tick;
            KindName = kindName;
            AuthorPlayerId = authorPlayerId;
        }

        /// <summary>本事件是否带作者轴。</summary>
        public bool HasAuthor => AuthorPlayerId != NoAuthor;
    }

    /// <summary>
    /// 48 的只读投影输入面(`ITutorialSource` 同族;51 的 `ITelemetrySource` 是其先例)。
    /// <para>⚠️ <b>零写面</b>:本接口没有任何 `Append` / `Write` / `Save` 成员 ——
    /// 48 不是第四个 `IEventSink` 写入者(ADR-019 §三)。</para>
    /// </summary>
    public interface ITutorialProjectionFeed
    {
        /// <summary>三流合并视图的只读事实序(入流序,原样保序)。</summary>
        IReadOnlyList<TutorialEventFact> Facts { get; }

        /// <summary>7a 回执:本会话(或读档后)是否**发生过一次** `Checkpoint` 写入。
        /// <para>F-48.1 的 ⑤ `册子` 步判据 = 「首个 Checkpoint 被写入」—— 它**不是**流事件
        /// (存档头不落流,ADR-024 白名单无该 Kind),故只能走这条旁路只读口。</para>
        /// <para>⚠️ <b>今日零生产实现</b>(2026-11-06 实测:`Sim.Codec` / 装配层**没有**任何
        /// 「最近 checkpoint 回执」的读面)。本成员 = **接口需求登记**,按 story-002 的
        /// Implementation Notes 2 处理:夹具可置 true 证明谓词连通,**生产上恒 false**,
        /// 不得据此声称 `archive` 步已端到端可用。承载方 = 7a 的实现轮,不得由 48 私开第二条通道。</para>
        /// </summary>
        bool HasCheckpointWritten { get; }
    }

    /// <summary>
    /// 把三流事件解成 <see cref="TutorialEventFact"/> 序的**唯一解码点**。
    /// <para>⚠️ 作者轴只在三个 Kind 上存在(逐条对齐载荷契约):
    /// <c>JudgmentRecorded</c> → `AuthorPlayerId`(ADR-008 §三 2026-09-19 补);
    /// <c>EmergencyAttempt</c> → `ActorId`(ADR-009 Amendment I);
    /// <c>DrugTreatmentApplied</c> → `ActorId`(ADR-009 Amendment I)。
    /// 其余 Kind 一律 <see cref="TutorialEventFact.NoAuthor"/> —— **不加猜测性作者轴**。</para>
    /// </summary>
    public static class TutorialSnapshotBuilder
    {
        private static readonly ReadOnlyCollection<TutorialEventFact> Empty =
            Array.AsReadOnly(Array.Empty<TutorialEventFact>());

        /// <summary>逐条解码。<paramref name="events"/> 的次序原样保留。</summary>
        /// <param name="events">三流合并视图(通常 = `EventStream.Events`)。</param>
        /// <param name="decoder">载荷解码器(ADR-031;`Sim` 侧够不着它,故由装配层注入)。</param>
        /// <exception cref="ArgumentNullException">任一入参为 null(拒绝以空集冒充绿)。</exception>
        public static IReadOnlyList<TutorialEventFact> Build(
            IReadOnlyList<SimEvent> events, IPayloadDecoder decoder)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (decoder == null) throw new ArgumentNullException(nameof(decoder),
                "未注入载荷解码器 —— 届时每个带作者轴的事件都只能报 NoAuthor," +
                "「别人落笔也算我学会」将被静默放过(AC-48-09 的反面)");
            if (events.Count == 0) return Empty;

            var facts = new TutorialEventFact[events.Count];
            for (int i = 0; i < events.Count; i++)
            {
                // ⚠️ 先落局部再 `in`:`in events[i]`(索引器返回的右值)不被 C# 接受
                //    (CS8156,2026-11-06 实测)—— 不是风格问题,是编译错误。
                SimEvent e = events[i];
                facts[i] = FactOf(in e, decoder);
            }
            return Array.AsReadOnly(facts);
        }

        private static TutorialEventFact FactOf(in SimEvent e, IPayloadDecoder decoder)
        {
            int author = TutorialEventFact.NoAuthor;
            switch (e.Kind)
            {
                case EventKind.JudgmentRecorded:
                    author = decoder.Decode<JudgmentRecordedPayload>(e.Kind, in e.Payload).AuthorPlayerId;
                    break;
                case EventKind.EmergencyAttempt:
                    author = decoder.Decode<EmergencyAttemptPayload>(e.Kind, in e.Payload).ActorId;
                    break;
                case EventKind.DrugTreatmentApplied:
                    author = decoder.Decode<DrugTreatmentAppliedPayload>(e.Kind, in e.Payload).ActorId;
                    break;
            }

            // KindName 取枚举成员名 —— 内容表的 predicateKind 由烘焙期的 Kind 白名单校验
            // (规则八 校验规则 6),两侧因此是**同一个字符串来源**(`EventKind` 的成员名),
            // 不存在第二张词表。
            return new TutorialEventFact(e.Tick, e.Kind.ToString(), author);
        }
    }

    /// <summary>
    /// 生产形态的输入面适配器:事件来源与回执来源都由装配层给。
    /// <para>⚠️ <b>刻意零缓存</b> —— 缓存 = 48 侧持有可变状态(规则四「零持久化」的反面)。
    /// 摊销由消费方的边沿闩锁负责(Story 003)。</para>
    /// </summary>
    public sealed class TutorialStreamFeed : ITutorialProjectionFeed
    {
        private readonly Func<IReadOnlyList<SimEvent>> _events;
        private readonly IPayloadDecoder _decoder;
        private readonly Func<bool> _checkpointWritten;

        /// <summary>构造。</summary>
        /// <param name="events">三流合并视图的取值委托(如 <c>() =&gt; stream.Events</c>)。</param>
        /// <param name="decoder">载荷解码器。</param>
        /// <param name="checkpointWritten">7a 回执读取委托。
        /// <para>⚠️ <b>缺省 = 恒 false</b>,仅在**回执口尚不存在**时允许省略(今日实况);
        /// 一旦 7a 交出读面,装配层必须补上 —— 省略会让 `archive` 步永不完结,
        /// 而那是**静默**的(与「玩家还没存过档」不可区分)。</para></param>
        public TutorialStreamFeed(
            Func<IReadOnlyList<SimEvent>> events,
            IPayloadDecoder decoder,
            Func<bool> checkpointWritten = null)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _checkpointWritten = checkpointWritten;
        }

        /// <inheritdoc/>
        public IReadOnlyList<TutorialEventFact> Facts
            => TutorialSnapshotBuilder.Build(_events(), _decoder);

        /// <inheritdoc/>
        public bool HasCheckpointWritten => _checkpointWritten != null && _checkpointWritten();
    }
}
