// ADR-035 §⑥ —— 53 的**过量支**结算:把 9 的 `PeakDrugLoad` 越档结算成一条 `TreatmentHarmOnset`。
//
// 权威来源:
//   ADR-035 §①(四层所有权)· §④(载荷形状)· §⑥(解除条件 = `TD-sim-02` + `TD-prescription-09` 均结案)
//   用户 2026-10-30 两项裁定:9 侧走独立状态量 / 「过量」按同一味药累积
//   `OverdoseEvaluator`(定档纯函数)· `CostTable53`(后果外键)
//
// ── 与 `ContraindicationSettler` 的分工 ────────────────────────────────────
//   两者写**同一个** Kind `TreatmentHarmOnset`,但触发源不同:
//     · 禁忌:11 判出的 `ContraindicationBand`(随用药事件**离散**发生)
//     · 过量:9 的 `PeakDrugLoad` 越过 53 的阈值(**连续累积**的结果)
//   ⇒ 不能合并成一个结算器:前者的驱动是「一条用药事件」,后者是「一个 tick 的体内药量」。
//   合并会把「事件驱动」和「状态驱动」两种游标语义塞进同一个 `Step`,出错时无法归因。
//
// ⚠️ **本文件不自算药量**:`PeakDrugLoad` 由 9 求值并经 `IVitalsQuery` 读出
//   (`VitalsDto.PeakDrugLoad`)。53 若自己从事件流累加 `Σ|potency|×Decay`,
//   就是在算 9 的毒性模型 —— 违 `AC-53-01` 与 `TD-sim-02` 的「材料在 9 侧」。
//
// ⚠️ **只升不降**:`PeakDrugLoad` 会随半衰期衰减,但**已造成的伤害不可逆**
//   (`TreatmentHarmOnset` 是事件,没有撤销路径)。故按病人记「已发出的最高档」,
//   只在新档**严格高于**它时再发;衰减回落不产生任何写入。
//
// ⚠️ **确定性**:病人按 id **升序**处理(`SortedSet`),同一 tick 内的事件写入顺序
//   与集合迭代序无关 —— 否则 `Seq` 发号会随 `HashSet` 的内部布局漂移。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Consequences
{
    /// <summary>过量代价的结算器 —— 读 `VitalsDto.PeakDrugLoad`,写 `TreatmentHarmOnset`。</summary>
    public sealed class OverdoseSettler
    {
        private readonly IReadOnlyList<SimEvent> _stream;
        private readonly IPayloadDecoder _decoder;
        private readonly IVitalsQuery _vitals;
        private readonly CostTable53.Table _table;
        private readonly IReadOnlyList<Fix> _bandThresholds;
        private readonly IPayloadEncoder _encoder;
        private readonly IEventSink _sink;

        /// <summary>去重键 = 与禁忌支同形的四元组(`harm_kind` 不同 ⇒ 两者不会互撞)。</summary>
        private readonly HashSet<(long Tick, int Patient, int InjuryId, int HarmKind)> _alreadyEmitted;

        /// <summary>每病人「已发出的最高过量档」。初值从**已有流**重建(读档 / 重放)。</summary>
        private readonly Dictionary<int, int> _highestBand = new Dictionary<int, int>();

        /// <summary>本 tick 内出现过用药事件的病人(下一个 `Step` 只检查这些)。</summary>
        private readonly SortedSet<int> _dirty = new SortedSet<int>();

        private int _cursor;

        /// <summary>构造结算器。</summary>
        /// <param name="stream">病史流(**只读**;与 <paramref name="sink"/> 须是同一条 —— 同禁忌支的理由)。</param>
        /// <param name="decoder">载荷解码(ADR-031)。</param>
        /// <param name="vitals">9 的体征查询门面(`PeakDrugLoad` 的唯一来源)。</param>
        /// <param name="table">代价表(空表 = 惰性:任何档都 miss)。</param>
        /// <param name="bandThresholds">各档下界(升序;来自 53 的代价表)。空 ⇒ 过量支未启用。</param>
        /// <param name="encoder">载荷编码(ADR-029)。</param>
        /// <param name="sink">写入面(主机唯一,ADR-005)。</param>
        public OverdoseSettler(
            IReadOnlyList<SimEvent> stream, IPayloadDecoder decoder, IVitalsQuery vitals,
            CostTable53.Table table, IReadOnlyList<Fix> bandThresholds,
            IPayloadEncoder encoder, IEventSink sink)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _vitals = vitals ?? throw new ArgumentNullException(nameof(vitals));
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _bandThresholds = bandThresholds ?? Array.Empty<Fix>();
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));

            // 幂等重建:已存在的过量伤害不得重发;同时恢复「已发出的最高档」。
            _alreadyEmitted = new HashSet<(long, int, int, int)>();
            for (int i = 0; i < _stream.Count; i++)
            {
                SimEvent e = _stream[i];
                if (e.Kind != EventKind.TreatmentHarmOnset) continue;
                TreatmentHarmOnsetPayload p = _decoder.Decode<TreatmentHarmOnsetPayload>(
                    EventKind.TreatmentHarmOnset, in e.Payload);
                if (p.HarmKind != (int)CostHarmKind.Overdose) continue;

                _alreadyEmitted.Add((p.Tick, p.PatientId, p.InjuryId, p.HarmKind));

                int band = BandOfInjury(p.InjuryId, p.SeverityBand);
                if (band > PeekHighest(p.PatientId)) _highestBand[p.PatientId] = band;
            }
        }

        /// <summary>本次运行累计发出的伤害事件条数(幂等跳过的不计)。</summary>
        public int EmittedCount { get; private set; }

        /// <summary>
        /// 把游标推进到 <paramref name="tick"/>(含),对该窗口内**有过用药**的病人结算过量。
        /// </summary>
        /// <returns>本次调用**新发出**的伤害事件条数。</returns>
        /// <remarks>
        /// ⚠️ 调用次序:`DiseaseVitalsService.OnTickEdge(tick)` 必须**先**跑 ——
        /// 本结算器读的是 9 在该 tick 上求出的 `PeakDrugLoad`。
        /// </remarks>
        public int Step(long tick)
        {
            while (_cursor < _stream.Count)
            {
                SimEvent e = _stream[_cursor];
                if (e.Tick > tick) break;     // 未来事件留到它自己的边沿
                if (e.Kind == EventKind.DrugTreatmentApplied) _dirty.Add(e.Patient.Value);
                _cursor++;
            }

            if (_dirty.Count == 0) return 0;

            int emitted = 0;
            foreach (int pid in _dirty)
            {
                if (TrySettle(pid, tick)) emitted++;
            }
            _dirty.Clear();

            EmittedCount += emitted;
            return emitted;
        }

        /// <summary>对单个病人在 <paramref name="tick"/> 上的药量结算;返回是否写了事件。</summary>
        private bool TrySettle(int patientId, long tick)
        {
            VitalsDto v = _vitals.GetVitals(new PatientId(patientId));
            int band = OverdoseEvaluator.Evaluate(v.PeakDrugLoad, _bandThresholds);

            if (band == OverdoseEvaluator.NoOverdose) return false;      // 未达最低阈值
            if (band <= PeekHighest(patientId)) return false;           // 该档已发过(只升不降)

            int harmKind = (int)CostHarmKind.Overdose;
            if (!_table.TryLookup(harmKind, band, out int injuryId, out int severityBand))
                return false;   // 合法 miss:该档在表里没有代价行

            _highestBand[patientId] = band;

            if (!_alreadyEmitted.Add((tick, patientId, injuryId, harmKind)))
                return false;   // 幂等:同 tick 同伤情已在流里

            var payload = new TreatmentHarmOnsetPayload(
                tick: tick,
                patientId: patientId,
                injuryId: injuryId,
                harmKind: harmKind,
                severityBand: severityBand,
                seq: 0);            // 同禁忌支:载荷 Seq 占位,header Seq 由主机发号

            _sink.Append(new SimEvent(tick, new PatientId(patientId), -1,
                EventKind.TreatmentHarmOnset, _encoder.Encode(EventKind.TreatmentHarmOnset, payload)));
            return true;
        }

        /// <summary>病人已发出的最高档;从未发过 = 哨兵。</summary>
        private int PeekHighest(int patientId)
            => _highestBand.TryGetValue(patientId, out int b) ? b : OverdoseEvaluator.NoOverdose;

        /// <summary>
        /// 由已有的伤害事件反查 53 的档序数(重建 `_highestBand` 用;查不到 = 哨兵)。
        /// </summary>
        /// <remarks>
        /// <para>⚠️ <b>必须按 `(injury_id, severity_band)` 双键匹配,不能只看 `injury_id`</b>:
        /// 真实表里过量两档**都指向 `INJ_DRUG_HARM`**(severity 3 / 4)⇒ 单键会同时匹配到两档。</para>
        /// <para>⚠️ <b>歧义时取最小档**(首个匹配即返回):取最大档会让「已发档 0」被读成「已发档 1」
        /// ⇒ 真正的档 1 永远发不出来(**漏伤害**,比重复发严重);取最小档最坏是多一次
        /// `TryLookup` + 被 `_alreadyEmitted` 挡掉,不会漏。</para>
        /// </remarks>
        private int BandOfInjury(int injuryId, int severityBand)
        {
            for (int b = 0; b < _bandThresholds.Count; b++)
            {
                if (_table.TryLookup((int)CostHarmKind.Overdose, b, out int inj, out int sev)
                    && inj == injuryId && sev == severityBand)
                    return b;   // 升序首匹配 = 最小档
            }
            return OverdoseEvaluator.NoOverdose;
        }
    }
}
