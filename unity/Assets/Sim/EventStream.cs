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
//   - 去重（条件键:已发号 = 四元组 (Kind, Patient, Tick, Seq);未发号补 PayloadRef 身份 —— O-4）
//   - 有界性（PATIENT_APPEARANCE_CAP = 24;跳过 PatientId.None —— O-5）
//
// ⚠️ 次序(承既有,测试面 F-4 登记):CAP 检查在去重**之前** ⇒ 病人已离场且 CAP 满时,
//    重发事件先抛而非走去重短路。45 重传路径的重试语义须知悉(归 45 轮)。

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
            // AC-15: 有界性检查(O-5 修复 · 2026-10-09:跳过 PatientId.None)
            // 失效模式(O-5):世界事件(StructurePlaced / PoiStateChanged / ActorCellEntered /
            // 急救两支 …)一律带 PatientId.None,IsPresent(None) 恒 false ⇒ 在场满 CAP 时
            // 被误判「新病人」拒收 —— 玩家立刻不能盖房、POI 状态机与跨格事件全抛异常。
            // CAP 守的是**同场被模拟病人数**(ADR-008 §六 有界性),None 不是病人 ⇒ 不进本分支。
            if (e.Patient != PatientId.None && !_presenceQuery.IsPresent(e.Patient))
            {
                if (_presenceQuery.PresentCount >= PATIENT_APPEARANCE_CAP)
                {
                    throw new InvalidOperationException(
                        $"AC-15: 在场病人数已达上限 {PATIENT_APPEARANCE_CAP}，拒收新病人");
                }
            }

            // AC-15: 去重(O-4 修复 · 2026-10-09:未发号键补载荷身份)
            // 失效模式(O-4):入流 Seq 对未发号事件恒为 -1,只按 (Kind, Patient, Tick, Seq)
            // 取键会让同 tick 同 Kind 的**两条不同事件**坍缩 —— 第二条静默丢弃,而其业务
            // 结果(如 structureId 已返回调用方)已生效 ⇒ 重建 / 回放时该结果消失。
            // 现实触发:同一 tick 放置两个结构 ⇒ 第二条 StructurePlaced 被吞。
            // 口径:
            //   · 未发号(Seq < 0):键补 PayloadRef 身份 (BlobId, Offset, Length) ——
            //     未发号事件的唯一区分只能来自载荷;同一 SimEvent 重发 ⇒ 同 PayloadRef ⇒
            //     同键 ⇒ 幂等拒收(既有重发语义保留)。
            //   · 已发号 / 显式 Seq:维持四元组 —— Seq 已是事件身份(重传带原 Seq 命中同键),
            //     且不受载荷重编码影响(重建 / 传输路径不产生假阴性去重)。
            // ⚠️ Sim 门 A 只见 Sim.Contracts ⇒ 只能取 PayloadRef 三字段,读不了 payload
            //    内容(那归 Sim.Codec)—— 故身份 = ref 本身,不是首字段语义值。
            string dedupKey = e.Seq < 0
                ? $"{e.Kind}_{e.Patient.Value}_{e.Tick}_{e.Seq}_{e.Payload.BlobId}_{e.Payload.Offset}_{e.Payload.Length}"
                : $"{e.Kind}_{e.Patient.Value}_{e.Tick}_{e.Seq}";
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

            // 如果事件没有 Seq,则发号
            // O-1 修复(2026-10-09):哨兵由 `Seq == 0` 改为 `Seq < 0` ——
            // 0 是合法首号值,原哨兵与 _currentSeq 首值冲突,
            // 「发了 0 号」与「没发号」不可区分。调用方待发事件一律传 -1。
            if (e.Seq < 0)
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
