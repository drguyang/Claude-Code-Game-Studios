// ADR-035 §Decision ⑤ —— 53 的**唯一**病史流写者:把「本剂命中了禁忌档」结算成一条
// `TreatmentHarmOnset`。这是 `TreatmentHarmOnset` 的**生产写入点**(AC-11-22 同族的写者存在性判据)。
//
// 权威来源:
//   ADR-035 §①B(触发条件面归 11)· ②(表的键 = `(harm_kind, band)`)· ④(载荷形状)
//   §⑤ 写者面 = 世界流 + 病史流**恰一支**(不得再扩第三支,扩 = 须另开 ADR)
//   ADR-031 §②(`IPayloadDecoder` 是 `Sim` 读载荷内容的唯一路径)· ADR-029 §①(`IPayloadEncoder`)
//
// ⚠️ **本文件的判据边界**:
//   · 它**不判定**「有没有命中」—— 那是 11 的 `ContraindicationBand`(ADR-035 §①B);
//     本文件只消费它。档为 `-1`(`NoContraindication`)⇒ 不写任何事件。
//   · 它**不持有**任何伤害属性 —— 伤情 id 与程度档全部来自 `CostTable53.Table`(外键)。
//     `AC-53-01`:53 零后果数值字段。
//   · 表里查不到 `(Contraindication, band)` ⇒ **合法 miss,静默跳过**(不抛)。
//     抛会把「作者少填一行」变成运行期崩溃,而 miss 的语义就是「该档没有代价」。
//
// ⚠️ **幂等**:同一条用药事件重放(读档后重建)不得重复发伤害。判据 = 流里**已存在**
//   同 `(tick, patient, injuryId, harmKind)` 的 `TreatmentHarmOnset` ⇒ 跳过。
//   承 `ADR-035 §④ 有界性`:≤ 每完成一次用药至多 1 条。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Consequences
{
    /// <summary>禁忌代价的结算器 —— 读 `DrugTreatmentApplied` 的 `ContraindicationBand`,写 `TreatmentHarmOnset`。</summary>
    /// <remarks>
    /// <para><b>为什么是游标式而不是事件订阅</b>:与 9 的 <c>DiseaseVitalsService.ApplyNewEvents</c> 同形 ——
    /// 游标 + 单调推进 ⇒ 重放 / 读档重建走**同一条**路径,不需要第二套「首次加载」分支。
    /// 「已发出」的去重键在构造期从**已有流**里扫描得到,故重放不会重复发。</para>
    /// </remarks>
    public sealed class ContraindicationSettler
    {
        private readonly IReadOnlyList<SimEvent> _stream;
        private readonly IPayloadDecoder _decoder;
        private readonly CostTable53.Table _table;
        private readonly IPayloadEncoder _encoder;
        private readonly IEventSink _sink;
        private readonly HashSet<(long Tick, int Patient, int InjuryId, int HarmKind)> _alreadyEmitted;

        private int _cursor;

        /// <summary>构造结算器。</summary>
        /// <param name="stream">病史流(**只读**;与 <paramref name="sink"/> 须是同一条 —— 见 <see cref="Step"/> 的注释)。</param>
        /// <param name="decoder">载荷解码(ADR-031;`Sim` 读载荷内容的唯一路径)。</param>
        /// <param name="table">代价表(空表 = 惰性形态:任何档都 miss)。</param>
        /// <param name="encoder">载荷编码(ADR-029)。</param>
        /// <param name="sink">写入面(主机唯一,ADR-005)。</param>
        public ContraindicationSettler(
            IReadOnlyList<SimEvent> stream, IPayloadDecoder decoder, CostTable53.Table table,
            IPayloadEncoder encoder, IEventSink sink)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));

            // 去重键从**已有流**里取:重放 / 读档后重建不得重复发同一条伤害。
            _alreadyEmitted = new HashSet<(long, int, int, int)>();
            for (int i = 0; i < _stream.Count; i++)
            {
                SimEvent e = _stream[i];
                if (e.Kind != EventKind.TreatmentHarmOnset) continue;
                TreatmentHarmOnsetPayload p = _decoder.Decode<TreatmentHarmOnsetPayload>(
                    EventKind.TreatmentHarmOnset, in e.Payload);
                _alreadyEmitted.Add((p.Tick, p.PatientId, p.InjuryId, p.HarmKind));
            }
        }

        /// <summary>本次运行累计发出的伤害事件条数(幂等跳过的不计)。</summary>
        public int EmittedCount { get; private set; }

        /// <summary>
        /// 把游标推进到 <paramref name="tick"/>(含),对窗口内的 `DrugTreatmentApplied` 结算。
        /// </summary>
        /// <returns>本次调用**新发出**的伤害事件条数。</returns>
        /// <remarks>
        /// ⚠️ `stream` 与 `sink` 须是**同一条流**:游标按 `stream` 推进、新事件写进 `sink`。
        /// 两者不同时,新事件进不了游标所在的那条流 ⇒ 下一步看不到自己写的行
        /// (症状 = 重复发;去重键能挡住,但那是兜底不是设计)。装配层负责一致。
        /// </remarks>
        public int Step(long tick)
        {
            int emitted = 0;
            while (_cursor < _stream.Count)
            {
                SimEvent e = _stream[_cursor];
                if (e.Tick > tick) break;     // 未来事件留到它自己的边沿(入流序不越界)
                if (e.Kind == EventKind.DrugTreatmentApplied && TrySettle(in e))
                    emitted++;
                _cursor++;
            }
            EmittedCount += emitted;
            return emitted;
        }

        /// <summary>对一条用药事件结算;返回是否写了伤害事件。</summary>
        private bool TrySettle(in SimEvent e)
        {
            DrugTreatmentAppliedPayload drug = _decoder.Decode<DrugTreatmentAppliedPayload>(
                EventKind.DrugTreatmentApplied, in e.Payload);

            // 档 = -1 ⇒ 未命中任何禁忌(11 的判定结论)。**不是**「表里没有」。
            if (drug.ContraindicationBand == DrugTreatmentAppliedPayload.NoContraindication)
                return false;

            int harmKind = (int)CostHarmKind.Contraindication;
            if (!_table.TryLookup(harmKind, drug.ContraindicationBand, out int injuryId, out int severityBand))
                return false;   // 合法 miss:该档在表里没有代价行(空表 = 全部 miss)

            int patient = e.Patient.Value;
            if (!_alreadyEmitted.Add((drug.Tick, patient, injuryId, harmKind)))
                return false;   // 幂等:同一条用药事件的伤害已在流里

            var payload = new TreatmentHarmOnsetPayload(
                tick: drug.Tick,
                patientId: patient,
                injuryId: injuryId,
                harmKind: harmKind,
                severityBand: severityBand,
                seq: 0);            // 载荷 Seq = 占位;header Seq 由主机 Append 时发号(承 10 / 11 的同一现状)

            _sink.Append(new SimEvent(drug.Tick, e.Patient, -1 /* 未发号哨兵 */,
                EventKind.TreatmentHarmOnset, _encoder.Encode(EventKind.TreatmentHarmOnset, payload)));
            return true;
        }
    }
}
