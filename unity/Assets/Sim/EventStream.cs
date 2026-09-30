// ADR-005 / ADR-008 —— 病史事件流实现。
//
// 权威来源:
//   ADR-005 §Key Interfaces —— 事件流唯一真源
//   ADR-008 §一 —— 病例流路由口径
//   ADR-024 §① —— Kind 白名单路由
//   GDD disease-simulation.md —— AC-15 有界性, AC-36 id 机制
//
// 核心机制:
//   - Append 纯函数路由（Kind → StreamId）
//   - Seq 发号器（每 tick 复位）
//   - 去重（五元组键）
//   - 有界性（PATIENT_APPEARANCE_CAP = 24）

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 病史事件流 —— 唯一真源。
    /// </summary>
    public sealed class EventStream : IEventSink
    {
        // AC-15: 同场被模拟病人数硬上限
        public const int PATIENT_APPEARANCE_CAP = 24;

        private readonly List<SimEvent> _events = new List<SimEvent>();
        private readonly IIdAuthority _idAuthority;
        private readonly IPresenceQuery _presenceQuery;
        private readonly HashSet<string> _dedupKeys = new HashSet<string>();

        // Seq 发号器状态
        private long _lastSeqTick = -1;
        private PatientId _lastSeqPatient = PatientId.None;
        private long _currentSeq = 0;

        public EventStream(IIdAuthority idAuthority, IPresenceQuery presenceQuery)
        {
            _idAuthority = idAuthority ?? throw new ArgumentNullException(nameof(idAuthority));
            _presenceQuery = presenceQuery ?? throw new ArgumentNullException(nameof(presenceQuery));
        }

        /// <summary>
        /// 事件流中的事件数。
        /// </summary>
        public int Count => _events.Count;

        /// <summary>
        /// 只读事件列表。
        /// </summary>
        public IReadOnlyList<SimEvent> Events => _events;

        /// <inheritdoc />
        public void Append(in SimEvent e)
        {
            // AC-15: 有界性检查
            if (!_presenceQuery.IsPresent(e.Patient))
            {
                if (_presenceQuery.PresentCount >= PATIENT_APPEARANCE_CAP)
                {
                    throw new InvalidOperationException(
                        $"AC-15: 在场病人数已达上限 {PATIENT_APPEARANCE_CAP}，拒收新病人");
                }
            }

            // AC-15: 去重（五元组键）
            string dedupKey = $"{e.Kind}_{e.Patient.Value}_{e.Tick}_{e.Seq}";
            if (_dedupKeys.Contains(dedupKey))
            {
                return; // 重发拒收
            }
            _dedupKeys.Add(dedupKey);

            // AC-36: Seq 发号（每 tick 复位）
            SimEvent eventWithSeq = e;
            if (e.Tick != _lastSeqTick || e.Patient != _lastSeqPatient)
            {
                _currentSeq = 0;
                _lastSeqTick = e.Tick;
                _lastSeqPatient = e.Patient;
            }
            else
            {
                _currentSeq++;
            }

            // 如果事件没有 Seq，则发号
            if (e.Seq == 0)
            {
                eventWithSeq = new SimEvent(e.Tick, e.Patient, _currentSeq, e.Kind, e.Payload);
            }

            _events.Add(eventWithSeq);
        }

        /// <summary>
        /// 获取下一个 patient_id（从事件流重构）。
        /// </summary>
        public PatientId GetNextPatientId()
        {
            int max = -1;
            foreach (var e in _events)
            {
                if (e.Patient != PatientId.None && e.Patient.Value > max)
                {
                    max = e.Patient.Value;
                }
            }
            return new PatientId(max + 1);
        }

        /// <summary>
        /// 清空事件流（测试用）。
        /// </summary>
        public void Clear()
        {
            _events.Clear();
            _dedupKeys.Clear();
            _lastSeqTick = -1;
            _lastSeqPatient = PatientId.None;
            _currentSeq = 0;
        }
    }
}
