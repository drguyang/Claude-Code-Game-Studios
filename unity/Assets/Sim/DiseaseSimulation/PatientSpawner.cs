// ADR-030 —— 9 的 DiseaseOnset 写者。
//
// 权威来源:
//   ADR-030 §③ —— 写者 = 9
//   ADR-030 §④ —— 落病史流
//   ADR-005 —— IEventSink / IIdAuthority / IPresenceQuery 抽象点
//   ADR-029 —— IPayloadEncoder 第七抽象点
//   disease-simulation.md 规则六 :164 —— patient_seed = hash(world_seed, patient_id)
//
// 核心机制:
//   - NextPatientId() 分配 patient_id
//   - SplitMix64.Hash(worldSeed, patientId) 派生 patient_seed(禁 Random.Range)
//   - IPayloadEncoder.Encode 编码载荷
//   - IEventSink.Append 写入病史流(Seq 由 EventStream 发放)

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.DiseaseSimulation
{
    /// <summary>
    /// 病人创建器 —— 9 的 DiseaseOnset 写者(ADR-030 §③)。
    /// </summary>
    public sealed class PatientSpawner
    {
        private readonly IIdAuthority _idAuthority;
        private readonly IEventSink _sink;
        private readonly IPayloadEncoder _encoder;
        private readonly ulong _worldSeed;

        /// <summary>
        /// 创建病人并写入 DiseaseOnset 事件。
        /// </summary>
        /// <param name="idAuthority">ID 发号权威(分配 patient_id)</param>
        /// <param name="sink">事件写入通道(病史流)</param>
        /// <param name="encoder">载荷编码器(第七抽象点)</param>
        /// <param name="worldSeed">世界种子(存档头,派生 patient_seed)</param>
        public PatientSpawner(
            IIdAuthority idAuthority,
            IEventSink sink,
            IPayloadEncoder encoder,
            ulong worldSeed)
        {
            _idAuthority = idAuthority ?? throw new ArgumentNullException(nameof(idAuthority));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _worldSeed = worldSeed;
        }

        /// <summary>
        /// 创建病人并写入 DiseaseOnset 事件。
        /// </summary>
        /// <param name="diseaseId">病种枚举(P0 · 8 项)</param>
        /// <param name="tick">onset tick</param>
        /// <returns>新创建的 patient_id</returns>
        public PatientId SpawnNext(int diseaseId, long tick)
        {
            if (diseaseId < 0) throw new ArgumentOutOfRangeException(nameof(diseaseId));
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));

            var patientId = _idAuthority.NextPatientId();
            // registry payload_schema 声明 patient_seed: i64 ⇒ 哈希 64 位原样按位承载
            // (不截断、不取模;codec 侧 WriteFieldInt64 与之对位)。
            var patientSeed = unchecked((long)SplitMix64.Hash((long)_worldSeed, patientId.Value));

            var payload = new DiseaseOnsetPayload(
                onsetTick: tick,
                diseaseId: diseaseId,
                patientId: patientId.Value,
                patientSeed: patientSeed,
                seq: 0); // 载荷 Seq = 占位 0;header Seq 由主机 Append 时发号(承 10 的同一现状)

            var encoded = _encoder.Encode(EventKind.DiseaseOnset, payload);
            _sink.Append(new SimEvent(tick, patientId, 0, EventKind.DiseaseOnset, encoded));

            // A1(评审 ADVISORY): 若 Append 抛异常(如 CAP 满),ID 已分配但事件未写入 ⇒ ID 空洞。
            // 生产路径 PresentCount 恒 0(IPresenceQuery 无写面),CAP 永不触发 ⇒ 当前不可达。
            // 且 IdAuthority.NextPatientId = _nextPatient++(不扫流),空洞不可自愈。
            // 真正修法 = 发号时机后移(Append 成功后才 NextPatientId),需改 IIdAuthority 契约,超本轮。
            // 登记为观察项 O-3,待 9 实现轮或 IIdAuthority 契约修订轮处理。
            return patientId;
        }
    }
}
