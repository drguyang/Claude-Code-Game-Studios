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

            try
            {
                var encoded = _encoder.Encode(EventKind.DiseaseOnset, payload);
                // header Seq = -1(未发号哨兵,O-1)—— 主机 Append 时发号。
                _sink.Append(new SimEvent(tick, patientId, -1, EventKind.DiseaseOnset, encoded));
            }
            catch
            {
                // O-3 修复(2026-10-09):写入失败(如 AC-15 CAP 满)⇒ 回滚刚发放的号,
                // 防 ID 空洞(NextPatientId = _nextPatient++,不扫流,空洞不可自愈)。
                // 仅最后号可回滚;若回滚被拒(他处已续发),按原行为留空洞(不可挽回)。
                if (_idAuthority is IRollbackableIdAuthority rb)
                    rb.TryRollbackLastPatientId(patientId);
                throw;
            }

            return patientId;
        }
    }
}
