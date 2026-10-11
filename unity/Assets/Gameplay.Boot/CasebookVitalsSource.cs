// sprint-05 T2.1 + sprint-06 G1.4 · 42 侧只读数源的**生产实装**(2026-10-11 更新)
//
// 权威来源:
//   ADR-013 §9 C3 —— 42 只渲染不持状态;**本类是 42 与装配层之间的装配桥**,同样零缓存
//   ADR-025 §① —— `Gameplay.Boot` = 装配层(组合根案 A)
//   story-024 边界①② —— 42 零自定义映射(通道/词/极性全来自 8 的表)
//   G1 对撞裁决乙案(2026-10-11) —— revealed = **派生态,不进三流**;
//     「查了没有」+ 快照住 39 分册(<see cref="ExaminationLedger"/>)
//
// ⚠️ G1.4 语义切换(2026-10-11):revealed 真源从「F2 signs tripwire(恒空)」改为
//   **查体分册快照**。查体后(ExamineDriver 落快照)→ 42 值区出现词条;未查 ⇒ 空集
//   (未查 = 空行,`:329`,禁假数据)。空词表接线期 = 恒空行,词表装载后自动放行。
//
// ⚠️ 设计说明:
//   · 住 `Gameplay.Boot`(**不**住 `Gameplay.UI`):42 装配不可反向引用装配层
//     (ADR-025 §① Gameplay.UI 引用集 = {Sim.Contracts, Gameplay.Presentation};
//      而 `ICasebookDtoSource` 住 Gameplay.UI)⇒ 实现必须在能同时看见
//     `Gameplay.UI` + `Sim` + `Sim.Contracts` 的装配 = 本装配(与案 A 同理由)。
//   · 每次读都穿过分册(零缓存字段 —— 缓存 = 第二份真相,破 C3);
//     分册快照 = 冻结值(AC-8-25),row 引用来自 8 词表(ExamineDriver 求值时传入)。

using Array_ = System.Array;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 脉案只读数源的生产实装 —— 把 42 接到**查体分册快照**(sprint-05 T2.1 + G1.4)。
    /// </summary>
    public sealed class CasebookVitalsSource : ICasebookDtoSource
    {
        private readonly ExaminationLedger _ledger;
        private readonly IReadOnlyList<SimEvent> _caseStream;
        private readonly ITickProvider _tick;
        private PatientId _patient = PatientId.None;

        /// <param name="ledger">查体分册(「查了没有」+ 快照 —— revealed 真源)。</param>
        /// <param name="caseStreamEvents">病例流事件视图(墨龄 = 最近 Judgment 的 Tick —— G2.2)。</param>
        /// <param name="tick">tick 源(墨龄的「当前 tick」;AC-42-G6:差值在装配层算,42 零计时器)。</param>
        public CasebookVitalsSource(ExaminationLedger ledger,
                                    IReadOnlyList<SimEvent> caseStreamEvents,
                                    ITickProvider tick)
        {
            _ledger = ledger ?? throw new System.ArgumentNullException(nameof(ledger));
            _caseStream = caseStreamEvents ?? throw new System.ArgumentNullException(nameof(caseStreamEvents));
            _tick = tick ?? throw new System.ArgumentNullException(nameof(tick));
        }

        /// <summary>当前绑定病人。</summary>
        public PatientId BoundPatient => _patient;

        /// <summary>换绑定病人(不持旧病人的任何量 —— 42 下次读即是新病人)。</summary>
        public void ChangePatient(PatientId patient) => _patient = patient;

        /// <inheritdoc/>
        /// <remarks>已查 ⇒ 该次快照词条数;未查 ⇒ 0(空行)。</remarks>
        public int ActiveSignCount
        {
            get
            {
                if (_ledger.TryGetSnapshot(_patient.Value, out var snap))
                    return snap.Set.SignCount;
                return 0;
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// **真源 = 查体分册快照**(G1 · 乙案):已查 ⇒ 该次查体的 8 词表词条(冻结);
        /// 未查 ⇒ 空集(未查 = 空行,`:329`,禁假数据)。
        /// <para>不再恒空集 —— sprint-05 的接线期占位已被 G1 的 Ledger 快照替下。
        /// 「读出的词」住 8 词表;Ledger 只存**收录了哪几条**(引用,不经 42 复制)。</para>
        /// </remarks>
        public IReadOnlyList<SignLexemeRow> RevealedRows
        {
            get
            {
                if (!_ledger.TryGetSnapshot(_patient.Value, out var snap))
                    return Array_.Empty<SignLexemeRow>();
                return snap.Set.Rows ?? Array_.Empty<SignLexemeRow>();
            }
        }

        /// <inheritdoc/>
        /// <remarks>精度档 = 该次查体的档(快照内;未查恒粗档 0)。</remarks>
        public int PrecisionSlot
        {
            get
            {
                if (!_ledger.TryGetSnapshot(_patient.Value, out var snap))
                    return 0;
                return snap.Set.PrecisionSlot;
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// **墨龄(G2.2 · 派生态)** = `当前 tick(ITickProvider) − 该病人最近 JudgmentRecorded
        /// 事件的 Tick`。差值在装配层算(42 零计时器,AC-42-G6)。
        /// <para>未落笔 ⇒ 0 = 刚落笔(墨龄零 = 湿墨形态)。载荷 freehandText P0 不做,
        /// 只认事件头 Tick —— <see cref="CaseStreamQuery.LastJudgmentOf"/> 按 case 过滤,
        /// 此处取该病人最近一条 Judgment(含 Revised,README 按全序最后)。</para>
        /// </remarks>
        public long InkAgeTicks
        {
            get
            {
                CaseId? openCase = CaseStreamQuery.OpenCaseOf(_caseStream, _patient.Value);
                if (!openCase.HasValue) return 0L;

                SimEvent? last = CaseStreamQuery.LastJudgmentOf(_caseStream, openCase.Value);
                if (!last.HasValue) return 0L;

                long current = _tick.CurrentTick;
                long age = current - last.Value.Tick;
                return age < 0 ? 0L : age;
            }
        }
    }
}