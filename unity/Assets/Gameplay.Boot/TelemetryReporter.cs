// 权威来源:ADR-019 §一(回放即数据记录)· §五(指标重算器 + 调试视图 + 导出)·
//   ADR-031(解码 = 抛形 `Decode<T>`,「未驻留」与「损坏」在类型上可分)。
//
// ═══════════════════════════════════════════════════════════════════════════════
// ⚠️ 本文件是 TD-epic-01 的**接线片**(2026-10-19):把「事件流 → 指标 → 本地报告」在生产里接通。
//
//   接线前状态(如实):`JudgmentStreamReducer` / `JudgmentMetrics` / `MetricsReport` 只有
//   **EditMode 夹具驱动**,生产**零调用** ⇒ F1 在生产里算不出来。
//   本类是装配层拿到的那一个对象:`CompositionRoot.Assemble` 构造它(引得到 `Sim.Codec` 的
//   decoder),之后任何人问「F1 是多少」只调 <see cref="JudgmentReport"/>。
//
// ⚠️ **51 仍只读**:本类**不持** `IEventSink`、不写三流、不写文件(导出内容交 `ITelemetrySink`)。
// ⚠️ **解码走抛形**(ADR-031 ②):`Decode<T>` 对「未驻留 / 损坏」都抛 ⇒ 本类用 try-catch
//    把「该条不可用」转成「跳过并计数」,与 <see cref="JudgmentStreamReducer"/> 的
//    `skipped` 语义对齐(**不**把损坏静默当成 0 —— 那正是普查抓的「空跑」形态)。
// ═══════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Text;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Telemetry;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>51 遥测的**装配层入口**(ADR-019 §五 的 P0 切面:重算器 + 导出)。
    /// <para>组合根构造它(它是唯一引得到 <see cref="IPayloadDecoder"/> 实现的一方),</para>
    /// <para>业务侧只调 <see cref="JudgmentReport"/> / <see cref="AllMetricsReport"/>。</para></summary>
    public sealed class TelemetryReporter
    {
        private readonly EventStream _stream;
        private readonly IPayloadDecoder _decoder;

        /// <summary>构造(组合根调用点)。</summary>
        /// <param name="stream">事件流(只读其 <see cref="EventStream.Events"/> 快照;不持写面)。</param>
        /// <param name="decoder">载荷解码器(实现在 `Sim.Codec`;组合根引得到,51 的装配引不到 ⇒ 委托注入)。</param>
        public TelemetryReporter(EventStream stream, IPayloadDecoder decoder)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        }

        /// <summary>F1 指标的本地报告文本(整数对;分母 0 报「分母 0」)。
        /// <para>**同流同值**(GDD R8 纯函数):不读墙钟 / 执行次数。</para></summary>
        /// <param name="skipped">解码失败 / 载荷不可用被跳过的条数(可观测;非静默吞)。</param>
        public string JudgmentReport(out int skipped)
        {
            IReadOnlyList<JudgmentRecord> records = Reduce(out skipped);
            JudgmentMetrics.Result r = JudgmentMetrics.Compute(records);
            return MetricsReport.Render("F1 判断层指标(本地会话)", new[]
            {
                new MetricLine("F1a 判断准确率", r.F1aNum, r.F1aDen,
                    r.DenominatorZero ? "无 Scorable 病例" : "J(c) == D(c) 的比例"),
                new MetricLine("F1b 改写率", r.F1bNum, r.F1bDen,
                    r.DenominatorZero ? "无 Scorable 病例" : "存在改写史的比例"),
            });
        }

        /// <summary>当前 Scorable 病例数(可观测面;0 ⇒ 分母 0 的意思)。</summary>
        public int ScorableCount
        {
            get { IReadOnlyList<JudgmentRecord> r = Reduce(out int _); return r.Count; }
        }

        /// <summary>全部已落指标的汇总(调试视图 / 本地导出的正文)。
        /// <para>⚠️ **只含 F1**(本轮唯一接线的);F2–F7 的谓词面虽已在 `Telemetry` 装配,
        /// **还原器接线归各自 story 轮** —— 本类不假装已覆盖(禁借绿);扩面时逐条加并各自附判据。</para></summary>
        public string AllMetricsReport(out int skipped)
        {
            var sb = new StringBuilder();
            sb.Append(JudgmentReport(out skipped));
            sb.Append("\n# ⚠️ F2–F7 未接线(谓词面已在 Telemetry 装配,还原器接线归各自 story 轮)\n");
            return sb.ToString();
        }

        // ────────────────────────────────────────────────────────────────

        /// <summary>事件流 → <see cref="JudgmentRecord"/> 序列(解码委托的**生产实现**)。
        /// <para>⚠️ 与 EditMode 夹具的替身分工:那边注入假集,这边解**真载荷**。</para></summary>
        private IReadOnlyList<JudgmentRecord> Reduce(out int skipped)
            => JudgmentStreamReducer.Reduce(_stream.Events, DecodeJudgment, out skipped);

        /// <summary>解码委托(ADR-031 抛形 ⇒ try-catch 转「跳过并计数」)。</summary>
        private bool DecodeJudgment(PayloadRef payloadRef, EventKind kind,
                                    out JudgmentStreamReducer.DecodedJudgment decoded)
        {
            try
            {
                switch (kind)
                {
                    case EventKind.CaseClosed:
                        var closed = _decoder.Decode<CaseClosedPayload>(kind, payloadRef);
                        // 位掩码 → ordinal 升序(承 PatternDetector.DiseaseOrdinalsOf 的定序纪律)
                        ulong bits = closed.DiseaseSet.Bits;
                        var diseases = new List<int>();
                        for (int bit = 0; bit < 64; bit++)
                            if ((bits & (1UL << bit)) != 0UL) diseases.Add(bit);
                        decoded = new JudgmentStreamReducer.DecodedJudgment(
                            checked((int)closed.CaseId.Seq), diseases, false);
                        return true;

                    case EventKind.JudgmentRecorded:
                    case EventKind.JudgmentRevised:
                        var rec = _decoder.Decode<JudgmentRecordedPayload>(kind, payloadRef);
                        // ⚠️ P0 落笔 = 单值病种(LexiconId)⇒ 集合 = {LexiconId};
                        //    多病种落笔的载荷形状归 8 的词表轮(若有),届时这里随动。
                        var judged = new List<int>(1) { rec.LexiconId };
                        decoded = new JudgmentStreamReducer.DecodedJudgment(
                            checked((int)rec.CaseId.Seq), judged, kind == EventKind.JudgmentRevised);
                        return true;

                    default:
                        // F1 不用的 Kind —— **不是失败**,由 reducer 的 default 分支跳过
                        decoded = default;
                        return false;
                }
            }
            catch (Exception)
            {
                // 解码失败(未驻留 / 载荷损坏)⇒ 跳过并计数,**不**当 0
                decoded = default;
                return false;
            }
        }
    }
}
