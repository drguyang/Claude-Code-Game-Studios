// sprint-06 G2.1 · 判断记录(落笔)写者 —— 37 的 JudgmentRecorded 生产者(2026-10-11)
//
// 权威来源:
//   ADR-008 §三(JudgmentRecorded 落病例流;判断 = 玩家落笔记录)·
//   GDD case-system.md(判断记录;J(c) 存新值最后一条胜,CaseStreamQuery.LastJudgmentOf)·
//   sprint-06 G2 规划:写者住 Gameplay.Boot(8/42 禁持 IEventSink,ADR-013 §9 C3 +
//     ADR-025 装配方向 ⇒ 落笔判据(呈现层量)的写者必须住装配层;
//     登记「37 写者落点 = 装配层」归 ADR-008 Amendment / ADR-031)
//   CaseOpenWriter(37 既有写者先例:同构的 Append 形态)
//
// ⚠️ **编码例外路径(ADR-029 §① 修正 ④)**:JudgmentRecorded / JudgmentRevised 两支
//   的载荷多收 `freehandText`(ADR-006 G-3 的 blob 变长段),该值**不在 T 里** ⇒
//   <c>IPayloadEncoder.Encode&lt;T&gt;</c> 对这两支**显式抛 NotSupportedException**;
//   唯一合法路径 = <c>PayloadCodec.Encode(in payload, string freehandText)</c> 具名重载
//   + <see cref="IBlobSink.Store"/> 入池 + 本写者构造 <c>PayloadRef</c>。
//   P0 不做自由书写 ⇒ freehandText 传空字符串(空 = 无自由文本;G-3 sim 消费面不持有该值)。
//
// ⚠️ 判据 = 病人有开案(病例流查询,sim 量)→ 写;无开案 ⇒ 具名返回码(不写)。
//   「落笔」本身是玩家交互(呈现层意图);本写者只消费该意图 + 开案查询,零呈现层持有。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>落笔结果码:仅 <see cref="Recorded"/> = 真的写入一条。</summary>
    public enum JudgmentWriteResult
    {
        /// <summary>写入成功(JudgmentRecorded 已入病例流)。</summary>
        Recorded = 0,

        /// <summary>病人无开案(病例流查询无 CaseOpened 或已 CaseClosed)—— 未落笔。</summary>
        NoOpenCase = 1,
    }

    /// <summary>
    /// 判断记录(落笔)写者 —— 37 的 <see cref="EventKind.JudgmentRecorded"/> 生产者。
    /// <para>判据 = 病人当前有开案(病例流前缀查询,sim 量);通过则编码 + Append 落病例流。</para>
    /// <para>⚠️ 住 <c>Gameplay.Boot</c>(非 Sim):落笔判据虽含 sim 量查询,但「落笔」的
    /// 意图源是呈现层(玩家在脉案上提判断),装配方向 = 呈现层可看 Sim、Sim 不可看呈现层
    /// ⇒ 写者住装配层(ADR-025 §① 案 A 同理由)。登记:37 写者落点由 ADR-008 Amendment 锚定。</para>
    /// </summary>
    public sealed class JudgmentWriter
    {
        private readonly IEventSink _eventSink;
        private readonly IPayloadEncoder _encoder;
        private readonly IReadOnlyList<SimEvent> _caseStream;
        private int _authorPlayerId;

        /// <param name="eventSink">事件写入通道(主机唯一 Append;ADR-005)。</param>
        /// <param name="encoder">载荷编码器 —— **JudgmentRecorded 走其具名路径**
        /// (<see cref="IPayloadEncoder.EncodeJudgmentRecorded"/>,ADR-029 §① 修正④:
        /// freehandText 例外 ⇒ <c>Encode&lt;T&gt;</c> 不承载,唯此入口合法)。</param>
        /// <param name="caseStreamEvents">病例流事件视图(纯函数查询用 —— OpenCaseOf)。</param>
        /// <param name="authorPlayerId">落笔作者(玩家)id —— 机制 A 的 <c>player_id</c>
        /// (ADR-006 第二十六批:玩家 id 复用受伤实体计数空间;单机 = 装配时给的 actorId)。</param>
        public JudgmentWriter(IEventSink eventSink, IPayloadEncoder encoder,
                              IReadOnlyList<SimEvent> caseStreamEvents,
                              int authorPlayerId)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _caseStream = caseStreamEvents ?? throw new ArgumentNullException(nameof(caseStreamEvents));
            _authorPlayerId = authorPlayerId;
        }

        /// <summary>
        /// 设定落笔作者(玩家)id —— 装配期初始 -1(未定),玩家生成后由装配层调用;
        /// 未定作者动作 ⇒ <see cref="InvalidOperationException"/>(fail-loud)。
        /// </summary>
        /// <param name="authorPlayerId">机制 A 的 <c>player_id</c>(ADR-006 第二十六批)。</param>
        /// <exception cref="ArgumentOutOfRangeException">id 为负(未定态不得接动作)。</exception>
        public void SetAuthor(int authorPlayerId)
        {
            if (authorPlayerId < 0)
                throw new ArgumentOutOfRangeException(nameof(authorPlayerId), authorPlayerId,
                    "落笔作者 id 不得为负(机制 A 计数器 id 空间;未定态须先 SetAuthor)");
            _authorPlayerId = authorPlayerId;
        }

        /// <summary>
        /// 落笔一次判断记录(前置通过则写一条 <see cref="EventKind.JudgmentRecorded"/>)。
        /// </summary>
        /// <param name="tick">落笔完成 tick(玩家在脉案上确认判断的那一 tick;
        /// 本事件的 <c>SimEvent.Tick</c> —— **墨龄 = current − 本 tick**,G2.2)。</param>
        /// <param name="patient">被判断的病人。</param>
        /// <param name="lexiconId">判断词条 id(u16 值域;case_judgment_lexicon 版本化 ordinal)。</param>
        /// <param name="confidence">把握度(u8 档位;OQ-CB-5 甲:落笔循环)。</param>
        /// <returns>结果码;仅 <see cref="JudgmentWriteResult.Recorded"/> = 写入。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> &lt; 0。</exception>
        public JudgmentWriteResult TryRecord(long tick, PatientId patient,
                                             int lexiconId, byte confidence)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");
            if (_authorPlayerId < 0)
                throw new InvalidOperationException(
                    "JudgmentWriter 作者 id 未定 —— 须先 SetAuthor(玩家生成后)," +
                    "未定态拒绝落笔(作者缺失 = 事件作者无主)");

            // 前置:病人须有开案(病例流前缀查询;无开案无从落笔 —— 病例都没开)
            CaseId? openCase = CaseStreamQuery.OpenCaseOf(_caseStream, patient.Value);
            if (!openCase.HasValue)
                return JudgmentWriteResult.NoOpenCase;

            // 编码 = Judgment 具名路径(ADR-029 §① 修正④:经 IPayloadEncoder.EncodeJudgmentRecorded,
            // 编码 + 入池一体,写者零手搓 PayloadRef —— b6 门;P0 零自由文本 ⇒ 空串)
            var payload = new JudgmentRecordedPayload(
                patient.Value, openCase.Value, _authorPlayerId, lexiconId, confidence);
            PayloadRef payloadRef = _encoder.EncodeJudgmentRecorded(in payload, freehandText: string.Empty);

            _eventSink.Append(new SimEvent(tick, patient, -1, EventKind.JudgmentRecorded, payloadRef));

            return JudgmentWriteResult.Recorded;
        }
    }
}