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
using System.Linq;
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
        private readonly IReadOnlyList<DiseaseRegistryEntry> _registry;

        /// <summary>构造(组合根调用点)。</summary>
        /// <param name="stream">事件流(只读其 <see cref="EventStream.Events"/> 快照;不持写面)。</param>
        /// <param name="decoder">载荷解码器(实现在 `Sim.Codec`;组合根引得到,51 的装配引不到 ⇒ 委托注入)。</param>
        /// <param name="registry">病种注册表(真表到位后替换 Fallback;DiseaseId → ordinal,
        /// 与 CaseClosed 位掩码 bit 位置对齐,用于把 truth/judged 还原成同一字符串空间)。</param>
        public TelemetryReporter(EventStream stream, IPayloadDecoder decoder,
                                 IReadOnlyList<DiseaseRegistryEntry> registry)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
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

        // ────────────────────────────────────────────────────────────────

        /// <summary>事件流 → <see cref="JudgmentRecord"/> 序列(解码委托的**生产实现**)。
        /// <para>⚠️ 与 EditMode 夹具的替身分工:那边注入假集,这边解**真载荷**。</para></summary>
        private IReadOnlyList<JudgmentRecord> Reduce(out int skipped)
            => JudgmentStreamReducer.Reduce(_stream.Events, DecodeJudgmentRecord, out skipped);

        /// <summary>解码委托(ADR-031 抛形 ⇒ try-catch 转「跳过并计数」)。</summary>
        private bool DecodeJudgmentRecord(PayloadRef payloadRef, EventKind kind,
                                    out JudgmentStreamReducer.DecodedJudgment decoded)
        {
            try
            {
                switch (kind)
                {
                    case EventKind.CaseClosed:
                        var closed = _decoder.Decode<CaseClosedPayload>(kind, payloadRef);
                        // 位掩码 → ordinal 升序(ordinal 0-based → DiseaseId 1-based,即 bit+1)。
                        // 与 `PatternDetector.DiseaseOrdinalsOf` 定序一致：bit 位置 i 对应
                        // 注册表第 i 个条目，DiseaseId = i + 1(ADR-014 append-only ordinal)。
                        ulong bits = closed.DiseaseSet.Bits;
                        var diseases = new List<int>();
                        for (int bit = 0; bit < 64; bit++)
                            if ((bits & (1UL << bit)) != 0UL) diseases.Add(bit + 1);
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

        // ────────────────────────────────────────────────────────────────
        // F3–F7 接线(2026-10-19 · TD-epic-01 第六片)
        //
        // ⚠️ 还原口径(逐条登记,禁自行发明):
        //   · F3:动作实例**只由 `EmergencyAttempt` 还原**(10 GDD 规则十二逐字:
        //         「`Played ⇔ Manual` · `Skipped ⇔ Skip`」;`NotOffered` = 无意图事件,
        //         **恒不在流上** ⇒ 还原面里它计数恒为 0)。`cause` 住**结算**事件
        //         `EmergencyTreatmentApplied`(`{玩家选择, 降级}` 整数枚举,枚举名归 10 的 GDD);
        //         **关联已落(第八片)**:经**结算全序**把意图贴到其结算(见 `ReduceActions` 注);
        //         贴不上的跳过 = 无 cause(计「未关联」,不猜)。
        //   · F4:只认 `SkillGrown`;`Level` 取载荷 `SkillGrownPayload.Level`,
        //         `LevelNotGrown = -1` **不携带** ⇒ 映射为 null(「不可得」≠ 0,AC-51-D2)。
        //   · F5/F6/F7:输入 = 三流合流的 (流别, Tick)。
        //   · ⚠️ `W` / `S` / `t_0` / `GAP_THRESHOLD` / `GROWTH_WINDOW` 全部 `*待定*`(用户调,
        //         GDD Tuning Knobs)⇒ 本类**不设默认值**,一律由调用方传入;未传的一次性
        //         查询须由调用方显式给数(禁硬编码「看着合理」的数 —— 那是替用户调参)。

        /// <summary>F3 跳过率的本地报告文本(整数对;Opportunity = 0 ⇒ 分母 0)。
        /// <para>还原面 = 病史流的 `EmergencyAttempt` 族;`cause` 经结算全序关联(见 `ReduceActions`)。</para>
        /// <para>AC-51-B8「两个独立分子」:SkipByChoice 与降级跳过**分开报**,不合并成单一 SkipRate。</para></summary>
        /// <param name="skipped">解码失败被跳过的条数(可观测)。</param>
        /// <param name="unattributed">跳过中**无 cause** 的条数(结算缺失 / 关联歧义 / 枚举越界)。
        /// 可观测面,**不进任何分母** —— 它大于 0 说明关联面有洞,而不是说「这些人选了玩家选择」。</param>
        public string SkipRateReport(out int skipped, out int unattributed)
        {
            IReadOnlyList<SkipRateMetrics.ActionInstance> instances = ReduceActions(out skipped, out unattributed);
            var (skipNum, skipDen, choiceNum, choiceDen, denominatorZero) =
                SkipRateMetrics.Compute(instances);
            int degradedNum = SkipRateMetrics.CountDegradedSkips(instances);
            return MetricsReport.Render("F3 跳过率(本地会话)", new[]
            {
                new MetricLine("F3 跳过率", skipNum, skipDen,
                    denominatorZero ? "无动作机会" : "Skipped / Opportunity(Opportunity 不含 NotOffered)"),
                new MetricLine("F3 跳过·玩家选择", choiceNum, choiceDen,
                    denominatorZero ? "无动作机会" : "Skipped ∧ cause = 玩家选择(与降级强制分开)"),
                new MetricLine("F3 跳过·降级", degradedNum, choiceDen,
                    denominatorZero ? "无动作机会" : "Skipped ∧ cause = 降级(AC-10-21 的无障碍核心读数)"),
                new MetricLine("F3 跳过·未关联", unattributed, skipNum,
                    "Skipped 中无 cause(结算缺失 / 关联歧义)—— 关联面有洞的可观测面,不计入任一 cause"),
            });
        }

        /// <summary>病史流 → 动作实例(解码委托的**生产实现**;口径见 F3 注)。
        /// <para><b>意图 ↔ 结算的关联(TD-epic-01 第八片)</b>:两条事件**无共享键**,按 GDD 定义
        /// 的**结算全序**贴 —— 「`EmergencyAttempt` 在流中先于 `EmergencyTreatmentApplied`」
        /// (`adr-009` Amendment I:意图是判定输入,结算在其后;两者同 tick 时写入序恒为
        /// 意图 → 结算,见 `HostEmergencyProcessor.Process`)。每一笔结算贴给**最早一笔尚未
        /// 结算的意图**(不是最近一笔 —— 见下方实现处的定序论证)。</para>
        /// <para>关联证据 = **事件头 `Patient`** + **载荷 `ActorId`**(施予者)+「未结算的最早者」。
        /// 刻意**不**拿 tick 作判据:GDD 未定义该比较,而主机写路径上载荷 tick 与事件头 tick
        /// 恒同源(`HostEmergencyProcessor.Process` 同一 tick 写两条),加它只是自造规则。</para>
        /// <para>贴不上的跳过(结算缺失 / cause 序数越界)保持无 cause,计「未关联」——
        /// **不猜**成「玩家选择」(那会把关联面的洞伪装成玩家行为)。</para></summary>
        private IReadOnlyList<SkipRateMetrics.ActionInstance> ReduceActions(out int skipped, out int unattributed)
        {
            var list = new List<SkipRateMetrics.ActionInstance>();
            var pending = new List<PendingIntent>();   // 已入列表、尚未结算的意图(保留插入序)
            int bad = 0;
            int untied = 0;
            IReadOnlyList<SimEvent> events = _stream.Events;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind == EventKind.EmergencyAttempt)
                {
                    if (!DecodeActionRecord(e.Payload, e.Kind, out int method, out int intentActor))
                    {
                        bad++;
                        continue;
                    }
                    var instance = new SkipRateMetrics.ActionInstance
                    {
                        InstanceId = list.Count + 1,
                        // 枚举名归 10 的 GDD(10 GDD 规则十二:Played ⇔ Manual · Skipped ⇔ Skip)
                        Resolution = method == EmergencyMethodManual ? Played : Skipped,
                        Cause = null,   // 待其结算来贴;贴不上即保持 null(不猜)
                    };
                    list.Add(instance);
                    pending.Add(new PendingIntent(e.Tick, e.Patient.Value, intentActor, instance));
                    continue;
                }

                if (e.Kind != EventKind.EmergencyTreatmentApplied) continue;
                if (!DecodeSettlementRecord(e, out EmergencyTreatmentAppliedPayload applied))
                {
                    bad++;
                    continue;
                }

                // 结算全序 ⇒ 该结算属于「病人 + 施予者」都对得上、**尚未结算**的**最早**一笔意图
                // (同键多笔入队时,最早者 = 唯一与「意图先于结算」相符的候选;取最近者会让
                // 「两笔跳过意图、一笔结算」把后一笔错贴成已结算)。
                int match = -1;
                for (int k = 0; k < pending.Count; k++)
                {
                    PendingIntent p = pending[k];
                    if (p.IsSettled) continue;   // 已被前面某笔结算贴上(不得再收第二笔)
                    if (p.Patient == e.Patient.Value && p.Actor == applied.ActorId)
                    {
                        match = k;
                        break;
                    }
                }

                // 无对得上的意图(乱序 / 伪造 / 意图载荷解码失败 / 同键意图都已被前笔结算贴上)
                // ⇒ **不贴**,继续等下笔 —— 把「A 的结算」贴给更晚的意图 = 把 A 的 cause 记成 B 的。
                // (事件头 tick 不作附加判据:GDD 未定义该比较,主机写路径两 tick 恒同源。)
                if (match < 0) continue;

                PendingIntent intent = pending[match];
                // 只取 cause 序数的闭集映射;序数越界 ⇒ null(计「未关联」),**不猜名字**。
                // ⚠️ 刻意**不**拿载荷 tick / 头 tick 作附加判据:GDD 未定义该比较,而主机写路径
                //    两者恒同源(`HostEmergencyProcessor.Process` 同一 tick 写两事件)⇒ 加它只是
                //    自造规则。关联证据 = (病人, 施予者) + 结算全序,够用且可证伪。
                intent.Cause = SkipRateMetrics.CauseNameOf(applied.Cause);
                intent.IsSettled = true;
            }

            // 跳过却无 cause = 关联面有洞(结算缺失 / 歧义 / 枚举越界)—— 计「未关联」,不猜成玩家选择。
            for (int k = 0; k < pending.Count; k++)
            {
                SkipRateMetrics.ActionInstance inst = pending[k].Instance;
                if (inst.Resolution == Skipped && inst.Cause == null) untied++;
            }
            skipped = bad;
            unattributed = untied;
            return list;
        }

        /// <summary>关联期的一笔意图(事件头 + 已分配的实例)。证据面 = (病人, 施予者) 与 tick。</summary>
        private sealed class PendingIntent
        {
            public readonly long Tick;
            public readonly int Patient;
            public readonly int Actor;
            public readonly SkipRateMetrics.ActionInstance Instance;

            public PendingIntent(long tick, int patient, int actor, SkipRateMetrics.ActionInstance instance)
            {
                Tick = tick;
                Patient = patient;
                Actor = actor;
                Instance = instance;
            }

            /// <summary>关联位是否已被消费(被贴过 / 被判歧义)。</summary>
            public bool IsSettled;

            /// <summary>写入 cause(贴时置;歧义置 null)。</summary>
            public string Cause
            {
                get => Instance.Cause;
                set => Instance.Cause = value;
            }
        }

        /// <summary>动作 method 枚举的「Manual」值(10 GDD 规则十二:`Played ⇔ Manual`)。</summary>
        private const int EmergencyMethodManual = 0;

        private const string Played = "Played";
        private const string Skipped = "Skipped";

        /// <summary>`EmergencyAttempt` 解码委托(ADR-031 抛形 ⇒ try-catch 转「跳过并计数」)。</summary>
        private bool DecodeActionRecord(PayloadRef payloadRef, EventKind kind, out int method, out int actorId)
        {
            try
            {
                EmergencyAttemptPayload p = _decoder.Decode<EmergencyAttemptPayload>(kind, payloadRef);
                method = p.Method;
                actorId = p.ActorId;
                return true;
            }
            catch (Exception)
            {
                method = 0;
                actorId = -1;
                return false;
            }
        }

        /// <summary>`EmergencyTreatmentApplied` 解码(同上;`ActorId` = 施予者,与意图载荷 `ActorId` 同空间)。</summary>
        private bool DecodeSettlementRecord(SimEvent e, out EmergencyTreatmentAppliedPayload applied)
        {
            try
            {
                applied = _decoder.Decode<EmergencyTreatmentAppliedPayload>(e.Kind, e.Payload);
                return true;
            }
            catch (Exception)
            {
                applied = default;
                return false;
            }
        }

        /// <summary>F4 熟练度成长的本地报告文本(新颖度三分 + ΔLevel 可得分)。
        /// <para>还原面 = 病史流的 `SkillGrown`;`skill` 由调用方指名(技能表 ordinal 或名)。</para></summary>
        /// <param name="skill">要统计的技能(与 <see cref="SkillGrowthMetrics"/> 的筛键同空间)。</param>
        /// <param name="growthTicks">成长统计窗口的 tick 闭区间;null ⇒ 全流(窗口 `*待定*`,见段首注)。</param>
        /// <param name="skipped">解码失败被跳过的条数(可观测)。</param>
        public string SkillGrowthReport(string skill, (long from, long to)? growthTicks, out int skipped)
        {
            IReadOnlyList<SkillGrowthMetrics.GrowthEvent> events = ReduceGrowth(growthTicks, out skipped);
            var (practice, nNew, nRepeat, nStale) = SkillGrowthMetrics.Compute(events, skill);
            var (deltaOk, delta) = SkillGrowthMetrics.TryComputeDeltaLevel(events, skill);

            var sb = new StringBuilder();
            sb.Append("# F4 熟练度成长(本地会话)\n");
            sb.AppendFormat("F4 Practice = {0}  # 窗口内练了多少次\n", practice);
            sb.AppendFormat("F4 N_new = {0}  # 首次\n", nNew);
            sb.AppendFormat("F4 N_repeat = {0}  # 冷却内\n", nRepeat);
            sb.AppendFormat("F4 N_stale = {0}  # 其它\n", nStale);
            sb.AppendFormat("F4 NoveltyConserves = {0}  # N_new + N_repeat + N_stale = Practice\n",
                SkillGrowthMetrics.NoveltyConserves(practice, nNew, nRepeat, nStale) ? 1 : 0);
            sb.Append(deltaOk
                ? string.Format("F4 ΔLevel = {0}\n", delta.Value)
                : "F4 ΔLevel = 不可得  # 窗口内有成长事件未携带 Level\n");
            return sb.ToString();
        }

        private IReadOnlyList<SkillGrowthMetrics.GrowthEvent> ReduceGrowth(
            (long from, long to)? growthTicks, out int skipped)
        {
            var list = new List<SkillGrowthMetrics.GrowthEvent>();
            int bad = 0;
            IReadOnlyList<SimEvent> events = _stream.Events;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.SkillGrown) continue;
                if (growthTicks.HasValue && (e.Tick < growthTicks.Value.from || e.Tick > growthTicks.Value.to))
                    continue;
                if (!DecodeGrowthRecord(e.Payload, e.Kind, out SkillGrownPayload p))
                {
                    bad++;
                    continue;
                }
                list.Add(new SkillGrowthMetrics.GrowthEvent
                {
                    Skill = p.SkillId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    NoveltyClass = NoveltyClassOf(p.NoveltyClass),
                    Level = p.Level == SkillGrownPayload.LevelNotGrown ? (long?)null : p.Level,
                });
            }
            skipped = bad;
            return list;
        }

        /// <summary>新颖度整数枚举 → 谓词面的三值名(枚举名归 30 的 GDD;形状见 `SkillGrownPayload`)。</summary>
        private static string NoveltyClassOf(int noveltyClass)
        {
            switch (noveltyClass)
            {
                case 0: return "new";      // 首次
                case 1: return "repeat";   // 冷却内
                default: return "stale";   // 其它
            }
        }

        private bool DecodeGrowthRecord(PayloadRef payloadRef, EventKind kind, out SkillGrownPayload p)
        {
            try
            {
                p = _decoder.Decode<SkillGrownPayload>(kind, payloadRef);
                return true;
            }
            catch (Exception)
            {
                p = default;
                return false;
            }
        }

        /// <summary>F5 难度曲线的本地报告文本(三流各一条密度序列 + 覆盖记账)。
        /// <para>⚠️ `W` / `S` / `t_0` 全部 `*待定*`(GDD Tuning Knobs,用户调)⇒ **无默认值**,
        /// 一律由调用方传入;`W < 1` / `S < 1` 由谓词面**硬失败**(AC-51-B14)。</para></summary>
        /// <param name="firstWindowTick">首窗起点 t_0。</param>
        /// <param name="step">步长 S。</param>
        /// <param name="window">窗宽 W(半开 `[t_k, t_k + W)`)。</param>
        /// <param name="skipped">恒为 0(F5 只读事件头,不解码载荷 —— 保留形参以统一签名)。</param>
        public string DifficultyCurveReport(long firstWindowTick, int step, int window, out int skipped)
        {
            skipped = 0;
            IReadOnlyList<DifficultyCurveMetrics.SimEvent> events = FlattenByStream();
            var sb = new StringBuilder();
            sb.Append("# F5 难度曲线(本地会话;三流分列,不合并)\n");
            if (events.Count == 0)
            {
                sb.Append("F5 空流:三流密度全 0,WindowCoverage = (0, 0)\n");
                return sb.ToString();
            }
            long tickMin = long.MaxValue, tickMax = long.MinValue;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Tick < tickMin) tickMin = events[i].Tick;
                if (events[i].Tick > tickMax) tickMax = events[i].Tick;
            }
            foreach (StreamId s in new[] { StreamId.History, StreamId.Case, StreamId.World })
            {
                var (densities, covered, span) =
                    DifficultyCurveMetrics.ComputeSeries(events, s, firstWindowTick, step, window, tickMax);
                sb.AppendFormat("F5 {0} 密度序列 = [{1}]\n", s.ToString(),
                    string.Join(",", densities));
                // ⚠️ 覆盖记账按流分列(AC-51-B13 的 WindowCoverage 是**每流对**);同参数下三流相同,
                //    这里逐流打印以便「未覆盖 ≠ 测得 0」在报告里直接可读。
                sb.AppendFormat("F5 {0} WindowCoverage = ({1}, {2})\n", s.ToString(), covered, span);
            }
            return sb.ToString();
        }

        /// <summary>F6/F7 会话节律的本地报告文本(SessionSpan + 上中位数 / MAD / GapCount)。</summary>
        /// <param name="gapThreshold">F7「静默段」阈值 `*待定*`(GDD Tuning Knobs,用户调)⇒ 无默认值。</param>
        /// <param name="skipped">恒为 0(F6/F7 只读事件头 —— 保留形参以统一签名)。</param>
        public string SessionRhythmReport(int gapThreshold, out int skipped)
        {
            skipped = 0;
            IReadOnlyList<SessionRhythmMetrics.SimEvent> events = FlattenTicks();
            var (tickMin, tickMax, span, count) = SessionRhythmMetrics.ComputeSessionSpan(events);
            var (median, mad, dMin, dMax, gapCount, defined) =
                SessionRhythmMetrics.ComputeRhythm(events, gapThreshold);

            var sb = new StringBuilder();
            sb.Append("# F6/F7 会话节律(本地会话)\n");
            sb.AppendFormat("F6 SessionSpan = {0}  # Tick_max − Tick_min ∈ [{1}, {2}],事件 {3} 条\n",
                span, tickMin, tickMax, count);
            if (!defined)
            {
                sb.Append("F7 Δ 节律 = 不可定义  # 事件 < 2 ⇒ 无 Δ 序列(不是 0)\n");
                return sb.ToString();
            }
            sb.AppendFormat("F7 Δ_median = {0}  # 上中位数(禁插值)\n", median);
            sb.AppendFormat("F7 Δ_MAD = {0}\n", mad);
            sb.AppendFormat("F7 Δ_min = {0}  Δ_max = {1}\n", dMin, dMax);
            sb.AppendFormat("F7 GapCount = {0}\n", gapCount);
            return sb.ToString();
        }

        /// <summary>三流合流 → (流别, Tick)。流别按 **Kind 路由表**判定(承 ADR-024 / StreamRouting),
        /// 不在本类另抄一张 Kind→流 白名单。</summary>
        private IReadOnlyList<DifficultyCurveMetrics.SimEvent> FlattenByStream()
        {
            IReadOnlyList<SimEvent> events = _stream.Events;
            var list = new List<DifficultyCurveMetrics.SimEvent>(events.Count);
            for (int i = 0; i < events.Count; i++)
            {
                list.Add(new DifficultyCurveMetrics.SimEvent
                {
                    Stream = StreamOf(events[i].Kind),
                    Tick = events[i].Tick,
                });
            }
            return list;
        }

        /// <summary>Kind → 流派生自 registry 生成的路由表(ADR-024 §①,单一真源)。</summary>
        private static StreamId StreamOf(EventKind kind) => StreamRouting.Of(kind);

        private IReadOnlyList<SessionRhythmMetrics.SimEvent> FlattenTicks()
        {
            IReadOnlyList<SimEvent> events = _stream.Events;
            var list = new List<SessionRhythmMetrics.SimEvent>(events.Count);
            for (int i = 0; i < events.Count; i++)
                list.Add(new SessionRhythmMetrics.SimEvent { Tick = events[i].Tick });
            return list;
        }

        // ────────────────────────────────────────────────────────────────
        // F2 误诊分布接线(下一条 story 轮会把 F3–F7 按同款模板补上)

        /// <summary>F2 误诊分布的本地报告文本(混淆矩阵 + 三桶显式计数)。
        /// <para>truth = CaseClosed 位掩码的 ordinal(bit 位置)字符串集;</para>
        /// <para>judged = JudgmentRecorded/Revised 的 LexiconId 字符串集。</para>
        /// <para>两者同一字符串空间 ⇒ `diseaseEnum` = 注册表的 `DiseaseId`(字符串);
        /// 位掩码 bit 位置 == DiseaseId(承 `DiseaseIdSet` bitmask 语义),既注册表病种
        /// 又出现在矩阵行/列上。</para></summary>
        /// <param name="skipped">解码失败 / 载荷不可用被跳过的条数(可观测)。</param>
        public string MisdiagnosisReport(out int skipped)
        {
            IReadOnlyList<MisdiagnosisMatrixMetrics.CaseRecord> records =
                JudgmentStreamF2Reducer.Reduce(_stream.Events, DecodeMisdiagnosisRecord, out skipped);

            var diseaseEnum = new HashSet<string>(
                _registry.Select(e => e.DiseaseId.ToString(System.Globalization.CultureInfo.InvariantCulture)));

            var (matrix, nComorbid, nOpen, nUnmappable, nMatchedComorbid) =
                MisdiagnosisMatrixMetrics.Compute(records, diseaseEnum);

            // ⚠️ 矩阵格/三桶都是整数计数,不是率 ⇒ 不走 MetricLine(Den=0 会渲染
            // 为「分母 0」,丢失计数);直接拼纯文本,顺序=字典序(确定性)。
            var sb = new StringBuilder();
            sb.Append("# F2 误诊分布(本地会话)\n");
            foreach (var kv in matrix.OrderBy(kv => kv.Key.Item1).ThenBy(kv => kv.Key.Item2))
                sb.AppendFormat("F2 M[{0}][{1}] = {2}\n", kv.Key.Item1, kv.Key.Item2, kv.Value);
            sb.AppendFormat("F2 N_comorbid = {0}  # |D| ≥ 2 ∨ |J| ≥ 2(共病)\n", nComorbid);
            sb.AppendFormat("F2 N_open = {0}  # 无 CaseClosed(未结案)\n", nOpen);
            sb.AppendFormat("F2 N_unmappable = {0}  # judgment 未携带病种枚举\n", nUnmappable);
            sb.AppendFormat("F2 N_matched_comorbid = {0}  # 共病且 D == J ⇒ 判对,进 F1a 第三项\n", nMatchedComorbid);
            return sb.ToString();
        }

        /// <summary>F2 的解码委托(ADR-031 抛形 ⇒ try-catch 转「跳过并计数」)。</summary>
        private bool DecodeMisdiagnosisRecord(PayloadRef payloadRef, EventKind kind,
                            out JudgmentStreamF2Reducer.DecodedJudgment decoded)
        {
            try
            {
                switch (kind)
                {
                    case EventKind.CaseClosed:
                        var closed = _decoder.Decode<CaseClosedPayload>(kind, payloadRef);
                        // 位掩码 → ordinal 升序(ordinal 0-based → DiseaseId 1-based,即 bit+1)。
                        // 与 `PatternDetector.DiseaseOrdinalsOf` 定序一致：bit 位置 i 对应
                        // 注册表第 i 个条目，DiseaseId = i + 1(ADR-014 append-only ordinal)。
                        ulong bits = closed.DiseaseSet.Bits;
                        var diseases = new List<int>();
                        for (int bit = 0; bit < 64; bit++)
                            if ((bits & (1UL << bit)) != 0UL) diseases.Add(bit + 1);
                        decoded = new JudgmentStreamF2Reducer.DecodedJudgment(
                            checked((int)closed.CaseId.Seq), diseases, false);
                        return true;

                    case EventKind.JudgmentRecorded:
                    case EventKind.JudgmentRevised:
                        var rec = _decoder.Decode<JudgmentRecordedPayload>(kind, payloadRef);
                        var judged = new List<int>(1) { rec.LexiconId };
                        decoded = new JudgmentStreamF2Reducer.DecodedJudgment(
                            checked((int)rec.CaseId.Seq), judged, kind == EventKind.JudgmentRevised);
                        return true;

                    default:
                        decoded = default;
                        return false;
                }
            }
            catch (Exception)
            {
                decoded = default;
                return false;
            }
        }

        /// <summary>全部已接线的指标汇总(调试视图 / 本地导出的正文)**在事件流上可算的部分**。
        /// <para>⚠️ 不含需要**调用方参数**的四项:F4(技能名 + 成长窗口)/ F5(`W`/`S`/`t_0`)/
        /// F6·F7(`GAP_THRESHOLD`)—— 这些参数全部 `*待定*`(用户调),本类**不设默认值**
        /// (禁替用户调参);调用方拿到对应 `*Report(...)` 方法自行传数。</para>
        /// <para>F3 的 cause 关联已于第八片落地(结算全序贴);跳过里贴不上的条数在 F3 段
        /// 以「跳过·未关联」行可观测 —— 它大于 0 说明关联面有洞,报告**不**把它并进任一 cause。</para></summary>
        public string AllMetricsReport(out int skipped)
        {
            string body = JudgmentReport(out int f1Skipped);
            body += '\n' + MisdiagnosisReport(out int f2Skipped);
            body += '\n' + SkipRateReport(out int f3Skipped, out int _);
            body += "\n# ⚠️ F4 / F5 / F6·F7 须调用方传参(参数全部待定 —— 用户调,见各类 Report 方法)\n";
            skipped = f1Skipped + f2Skipped + f3Skipped;

            // 空白与退化(AC-51-D1/D8):标注放在报告**头部** —— 读报告者先看到口径再读数,
            // 不会把「三流皆空」读成「测得 0」、也不会把「部分流」当完整流(禁静默出数)。
            var sb = new StringBuilder();
            sb.Append(CompletenessBanner(f1Faulted: false));
            sb.Append(body);
            return sb.ToString();
        }

        /// <summary>AC-51-D1 / D8 的口径标注行:流空 ⇒ 显式「空流(零值合法)」而非静默零;
        /// 流非空但某条指标无可打分样本 ⇒ 显式「部分指标」(禁止当完整流静默出数)。</summary>
        /// <param name="f1Faulted">F1 折叠是否检出流异常(本类当前恒 false —— 异常检定归 story-009 轮)。</param>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>开发者调试视图的可执行入口(AC-51-D4)。**只在 Editor / Development Build 编译**
        /// —— Release 构建里本方法及其调用的预览渲染均不存在(编译期剔除,不是「有按钮但点了没用」)。</summary>
        /// <param name="request">预览口径(参数未给的用「未给」显示 —— 本类不替用户填默认值)。</param>
        /// <param name="sink">输出面(控制台 / 本地文件;ADR-019 §五)。</param>
        /// <returns>写入 sink 的正文(便于调用方 / 夹具复算)。</returns>
        public string RenderDeveloperPreview(in TelemetryPreviewRequest request, ITelemetrySink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            // `TelemetryDeveloperView.Render` 已自带完整性头(空流 / 部分指标);此处**不再前置**
            // 一次,否则头部重复 —— 且第 2 份与视图的「未给」口径会脱节。
            string text = TelemetryDeveloperView.Render(this, request);
            sink.LogDeveloperSummary(text);
            return text;
        }

        /// <summary>开发期默认预览 —— **只收 sink**(不替用户填旋钮):参数全 `*待定*` ⇒ 只出
        /// F1–F3 + 「未给(未算)」逐条点名。供 DevBuild 里挂一个键位 / 控制台命令直接调;
        /// 生产装配**尚不挂调用点**(键位选型未裁,见 TD 账)—— 有调用点之后这条才会被
        /// 真实链路驱动,在那之前它只由夹具证明。
        /// <para>同样在 Release 编译期剔除。</para></summary>
        public string RenderDeveloperSummary(ITelemetrySink sink) =>
            RenderDeveloperPreview(new TelemetryPreviewRequest(null, null, null, null, null, null, null), sink);
#endif

        public string CompletenessBanner(bool f1Faulted)
        {
            bool empty = _stream.Events.Count == 0;
            string streamLine = empty
                ? "# ⚠️ 空流:三流零事件 —— 以下数值为「空输入零值」(Computed,非 Faulted)\n"
                : string.Format("# 流事件 {0} 条(三流合流)\n", _stream.Events.Count);
            // F1 无可打分病例 ⇒ 其后 F2/F3 的分母口径随之不可定义 ⇒ 报告是「部分指标」
            bool partial = !empty && ScorableCount == 0;
            string partialLine = partial
                ? "# ⚠️ 部分指标:无 Scorable 病例 ⇒ F1/F2 不可定义(非测得 0)\n"
                : string.Empty;
            string faultedLine = f1Faulted ? "# ⚠️ Faulted:流异常,不得出数\n" : string.Empty;
            return faultedLine + streamLine + partialLine;
        }
    }
}
