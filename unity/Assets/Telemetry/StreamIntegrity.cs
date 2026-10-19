// 权威来源:`design/gdd/telemetry-analytics.md`
//   · §States and Transitions(Idle → Reading → Computed → Emitted → Idle;Reading → Faulted;
//     **Faulted 不可自愈 —— 须重新发起 Compute(),由 Idle 重入**)
//   · §Edge Cases 组 B —— 流完整性(异常,Faulted):
//       E-4 `Tick` 回退(后读到的 Tick **小于**前一条)        → Faulted
//       E-5 同一 (Tick, StreamPriority, Patient) 内 Seq 不连续   → Faulted
//       E-6 病例流引用不在病史流中的 Patient                   → Faulted
//   · `AC-51-D3`(BLOCKING):Faulted 且**中止** —— ITelemetrySink 零调用 ·
//     本地文件未生成 / 未覆写(旧文件字节保持)· 无部分指标 · Faulted 不可自愈
// ADR-019 §三(51 只读消费者,不写三流)· ADR-008 §跨流全序键 (Tick, StreamPriority, Patient, Seq)
//
// ═══════════════════════════════════════════════════════════════════════════════
// ⚠️ **TD-epic-01 D3 还债件(2026-10-19)**:普查实测 `AC-51-D3` 的九个测试 story 全标 `Complete`,
//    而生产侧**没有**任何 Faulted 状态机 / 流完整性校验 / sink 门 —— AC 语义层由测试自持谓词顶替
//    (上一版 `blank_and_degraded_test.cs` 的 `ComputeFaulted()` 是手搓常量)。本件把它上移为生产谓词。
//
// ⚠️ **为什么校验「实时流」看似多余**:`EventStream.Append` 自己会拒收 `Tick` 回退
//    (`EventStream.cs` 的 `BCD-码-3`)。但 51 的输入面(GDD R9 ②)**离线完整重算**读的是
//    「一次回放的完整三流」 —— 那份数据来自 7a 存档 / 45 拉流 / 跨流合并,**不经过** `EventStream.Append`。
//    ⇒ E-4 / E-5 只有在这一侧才有意义,本校验器正是为此而存在。
//
// ⚠️ **E-5 的起点口径(用户裁定 2026-10-19)**:每 `(Tick, Patient)` 组**首条 Seq 必须是 0**,
//    之后逐条 `+1`。依据 = `EventStream` 的发号器行为(`_currentSeq = 0` 在组切换时复位)。
//    ⚠️ 已知缺口(如实登记):`EventStream` 的组键是 `(Tick, Patient)` —— **不含 StreamId**;
//    而 GDD E-5 的组键含 `StreamPriority`。二者只在「同 tick 同病人跨流」时分歧,
//    且 `Append` 的 Seq 在跨流时会被复用 —— 该分歧归流层(ADR-008),不在本件扩大。
// ═══════════════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Telemetry
{
    /// <summary>一次重算的生命周期状态(GDD §States and Transitions)。
    /// <para>正式状态共五个;**51 内部只物化与指标可信度相关的三个** ——
    /// `Emitted` 是落盘动作的瞬时态(由调用方顺序体现),`Reading` 是折叠期间的瞬时态
    /// (`Compute` 在单次调用内完成),两者不构成可观测的对外状态 ⇒ 不设 enum 成员。</para></summary>
    public enum EvalState
    {
        /// <summary>已挂载,未重算(或上一次达标重算已结束)。</summary>
        Idle = 0,

        /// <summary>三流读完,**校验通过** ⇒ 指标可信。</summary>
        Computed = 1,

        /// <summary>三流校验失败 ⇒ **中止,不产出指标**。⚠️ 不可自愈(见 <see cref="ComputeSession"/>)。</summary>
        Faulted = 2,
    }

    /// <summary>流完整性违规的种类(与 GDD E-4 / E-5 / E-6 一一对应)。</summary>
    public enum IntegrityViolation
    {
        /// <summary>无违规。</summary>
        None = 0,

        /// <summary>E-4:`Tick` 回退(后读到的 tick 小于同流前一条)。</summary>
        TickRegression = 1,

        /// <summary>E-5:同一 `(Tick, Patient)` 内 `Seq` 不连续(须 0,1,2…)。</summary>
        SeqGap = 2,

        /// <summary>E-6:病例流引用了一个不在病史流中的 `Patient`。</summary>
        CasePatientNotInHistory = 3,
    }

    /// <summary>完整性校验结果(值类型;无违规 ⇒ <see cref="Ok"/> 为真)。</summary>
    public readonly struct IntegrityResult
    {
        /// <summary>违规种类;<see cref="IntegrityViolation.None"/> ⇒ 通过。</summary>
        public readonly IntegrityViolation Violation;

        /// <summary>违规所在流(违规为 None 时无意义)。</summary>
        public readonly StreamId Stream;

        /// <summary>该流内的下标(违规为 None 时无意义)。</summary>
        public readonly int Index;

        /// <summary>违规涉及的病人(<see cref="PatientId.None"/> = 不适用 / 世界级)。</summary>
        public readonly PatientId Patient;

        /// <summary>违规处的 tick(便于定位)。</summary>
        public readonly long Tick;

        public IntegrityResult(IntegrityViolation violation, StreamId stream, int index,
                               PatientId patient, long tick)
        {
            Violation = violation;
            Stream = stream;
            Index = index;
            Patient = patient;
            Tick = tick;
        }

        /// <summary>校验通过。</summary>
        public bool Ok => Violation == IntegrityViolation.None;

        /// <summary>违规的可读描述(用于报告 / 断言失败信息)。</summary>
        public override string ToString() => Ok
            ? "OK"
            : $"{Violation} @ {Stream}[{Index}] tick={Tick} patient={Patient.Value}";
    }

    /// <summary>三流完整性校验器(GDD 组 B · `AC-51-D3`)—— **纯函数,无状态**。
    /// <para>⚠️ 只读:不排序、不去重、不改任何一条事件 —— 违规即报,不「顺手修好」。</para></summary>
    public static class StreamIntegrity
    {
        /// <summary>按 E-4 → E-5 → E-6 的次序校验三条流,返回**第一条**违规。
        /// <para>次序是刻意的:先查单流内的结构(E-4 / E-5),再查跨流引用(E-6) ——
        /// 流本身已坏时,跨流引用检查的结论没有意义。</para></summary>
        /// <param name="history">病史流(只读)。</param>
        /// <param name="caseStream">病例流(只读)。</param>
        /// <param name="world">世界流(只读)。</param>
        public static IntegrityResult Validate(IReadOnlyList<SimEvent> history,
                                               IReadOnlyList<SimEvent> caseStream,
                                               IReadOnlyList<SimEvent> world)
        {
            IntegrityResult r = CheckStream(history, StreamId.History);
            if (!r.Ok) return r;

            r = CheckStream(caseStream, StreamId.Case);
            if (!r.Ok) return r;

            r = CheckStream(world, StreamId.World);
            if (!r.Ok) return r;

            return CheckCasePatientsInHistory(history, caseStream);
        }

        /// <summary>单流内校验:E-4(有序)+ E-5(Seq 连续)。</summary>
        private static IntegrityResult CheckStream(IReadOnlyList<SimEvent> stream, StreamId id)
        {
            if (stream.Count == 0) return default; // Ok

            long lastTick = stream[0].Tick;
            // 组键 = (Tick, Patient);值 = 该组上一条的 Seq。
            long groupTick = stream[0].Tick;
            PatientId groupPatient = stream[0].Patient;
            long lastSeq = stream[0].Seq;

            // 组首条:Seq 必须是 0(E-5 起点口径,用户裁定 2026-10-19)。
            if (lastSeq != 0)
                return Fault(IntegrityViolation.SeqGap, id, 0, groupPatient, groupTick);

            for (int i = 1; i < stream.Count; i++)
            {
                SimEvent e = stream[i];

                // E-4:Tick 回退(GDD 原文判据 = 「小于前一条」,同 tick 合法)。
                if (e.Tick < lastTick)
                    return Fault(IntegrityViolation.TickRegression, id, i, e.Patient, e.Tick);
                lastTick = e.Tick;

                // E-5:同组 Seq 必须前条 +1;跨组则必须从 0 起。
                bool sameGroup = e.Tick == groupTick && e.Patient == groupPatient;
                if (sameGroup)
                {
                    if (e.Seq != lastSeq + 1)
                        return Fault(IntegrityViolation.SeqGap, id, i, e.Patient, e.Tick);
                }
                else
                {
                    if (e.Seq != 0)
                        return Fault(IntegrityViolation.SeqGap, id, i, e.Patient, e.Tick);
                    groupTick = e.Tick;
                    groupPatient = e.Patient;
                }
                lastSeq = e.Seq;
            }

            return default; // Ok
        }

        /// <summary>E-6:病例流中带真实病人的事件,其 `Patient` 必须出现在病史流中。
        /// <para>⚠️ `PatientId.None` 的病例事件不参与本检查 —— 它不是「引用了谁」,
        /// 而是「没有引用任何病人」(哨兵语义,ADR-007 §四)。</para></summary>
        private static IntegrityResult CheckCasePatientsInHistory(IReadOnlyList<SimEvent> history,
                                                                  IReadOnlyList<SimEvent> caseStream)
        {
            if (caseStream.Count == 0) return default;

            var known = new HashSet<int>();
            for (int i = 0; i < history.Count; i++)
                if (!history[i].Patient.IsNone) known.Add(history[i].Patient.Value);

            for (int i = 0; i < caseStream.Count; i++)
            {
                SimEvent e = caseStream[i];
                if (e.Patient.IsNone) continue;
                if (!known.Contains(e.Patient.Value))
                    return Fault(IntegrityViolation.CasePatientNotInHistory, StreamId.Case, i, e.Patient, e.Tick);
            }

            return default; // Ok
        }

        private static IntegrityResult Fault(IntegrityViolation v, StreamId id, int index,
                                             PatientId patient, long tick)
            => new IntegrityResult(v, id, index, patient, tick);
    }

    /// <summary>一次重算的**生命周期**(GDD §States and Transitions · `AC-51-D3` 的「不可自愈」)。
    /// <para>⚠️ 刻意**不是**静态 / 单例:每次重算 = 一个新会话对象 ⇒ 不存在「上一局的 Faulted
    /// 污染下一局」的跨调用状态(51 不持有游戏状态,R8 纯函数)。</para>
    /// <para>「不可自愈」的落点:<see cref="Compute"/> 一旦返回 `Faulted`,本会话**永远**返回
    /// `Faulted` —— 不提供 reset,也不因下一次调用而回到 `Idle`。要重算须**新建**一个会话
    /// (GDD 原文:「须重新发起 Compute(),由 Idle 重入」)。</para></summary>
    public sealed class ComputeSession
    {
        private EvalState _state = EvalState.Idle;

        /// <summary>当前状态。初始 = <see cref="EvalState.Idle"/>。</summary>
        public EvalState State => _state;

        /// <summary>上一次 <see cref="Compute"/> 的违规明细(未重算 / 通过时 = <c>default</c> 的 Ok 值)。</summary>
        public IntegrityResult LastIntegrity { get; private set; }

        /// <summary>执行一次重算:读三流 → 校验 → 状态落在 <see cref="EvalState.Computed"/>
        /// 或 <see cref="EvalState.Faulted"/>。
        /// <para>⚠️ 已 Faulted 的会话再次调用 ⇒ **直接返回 Faulted,不重新校验**
        /// (不可自愈;也保证「坏流不会因为第二次多读了什么就变好」)。</para></summary>
        public EvalState Compute(IReadOnlyList<SimEvent> history,
                                 IReadOnlyList<SimEvent> caseStream,
                                 IReadOnlyList<SimEvent> world)
        {
            if (_state == EvalState.Faulted) return EvalState.Faulted; // 不可自愈

            IntegrityResult r = StreamIntegrity.Validate(history, caseStream, world);
            LastIntegrity = r;
            _state = r.Ok ? EvalState.Computed : EvalState.Faulted;
            return _state;
        }
    }
}
