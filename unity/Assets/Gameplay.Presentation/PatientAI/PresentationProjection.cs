// patient-ai Story 003 —— 呈现投影主链:`ViewState` 优先级 · `IPresentPatients` 视图 · `Material(p)`。
//
// 权威来源:
//   GDD design/gdd/patient-ai.md §Formulas F-13.6(ViewState 优先级)· F-13.8(signs[] → MaterialTable)
//     · §Core Rules 十(IPresentPatients 载荷 = PatientId + WorldPos 格 + 粗状态枚举,无 disease_id)
//     · §Core Rules 三-bis(signs[] 只映射材质,不推断病种)
//   ADR-016 §六(13 ↔ 37 单向无环)· ADR-013 §9 C3(呈现层只渲染不持状态;disease_id 不进呈现层)
//   TR-patient-011/012/020 · AC-13-C1/C3/C4/C5 · AC-13-A5 · AC-13-D1
//
// ⚠️ **投影链单向**(Implementation Notes 1):
//   `(BehaviorState, SessionState, LogiPose, VitalsDto) → {ViewState, PresentPatient, Material, cue}`
//   **任何环节零回写 sim / 零写流**(承 Story 001 的零写门,ADR-027)。
//
// ⚠️ **`signs[]` 的消费面是白名单**(AC-13-A5)—— `signs[]`(此处 = `SignChannelMask` / `SignCount`)
//   **只出现在 `Material(p)` 的调用点**;`ViewState()` / `Snapshot()` 的输入集**恰 ⊆ {position, trend}**。
//   本文件用编译期结构把这条钉死:视图投影函数**签名里不出现 VitalsDto**。
//
// ⚠️ **13 不引用 37**(AC-13-C2)—— 本文件与整个 `PatientAI` 目录零 `DaYiJingCheng.*.Case*` 引用;
//   反向由 37 读 `IPresentPatients`(ADR-016 §六)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.PatientAI
{
    /// <summary>表现材质(zero-cost 粗粒度)——
    /// `Material(p) = MaterialTable[ signs(p) ]`(GDD F-13.8)。
    /// <para>⚠️ **纯表现层**:不含病种、不含九态;输出只是「用哪套音色 / 哪组姿态细节 / 哪个面色通道」。
    /// **永不进 `IPresentPatients`**(AC-13-C1)。</para></summary>
    public readonly struct PresentMaterial
    {
        /// <summary>音色变体 id(44 侧解析素材;13 只给 id)。</summary>
        public readonly int ToneVariant;
        /// <summary>姿态细节位(整数位域 —— 禁 float,承 TR-patient-010)。</summary>
        public readonly int PostureBits;
        /// <summary>面色通道位(与 9 的 `channel_mask` 对齐,规则三-bis)。</summary>
        public readonly int ComplexionBits;

        public PresentMaterial(int toneVariant, int postureBits, int complexionBits)
        {
            ToneVariant = toneVariant; PostureBits = postureBits; ComplexionBits = complexionBits;
        }

        /// <summary>中性材质 —— `signs[]` 空集(或仅「客观族」词条)时的输出(GDD §Edge Cases 一)。
        /// <para>⚠️ **不做特判、不报错** —— 空集是 9 的 R3.6 豁免类合法形态。</para></summary>
        public static PresentMaterial Neutral => new PresentMaterial(0, 0, 0);

        /// <summary>是否等于中性(测试与去重判据;`Neutral` 是值等判定,非引用等)。</summary>
        public bool IsNeutral => ToneVariant == 0 && PostureBits == 0 && ComplexionBits == 0;
    }

    /// <summary>`MaterialTable`(F-13.8)—— `signs[]` 词条 → 表现材质。
    /// <para>⚠️ **表内容走 ADR-014 烘焙**(`assets/data/ai_patient.json`,AC-13-D1 `[A]`);
    /// 本类型是**运行期查表器**,不持表内容。表未装载时退化为中性 —— 但那**不是**合法产品形态,
    /// 由 AC-13-D1 的签署记录归口(本 story 只保证「查表器存在且行为正确」)。</para>
    /// <para>⚠️ **输入是词条 id(整数位),不是病种** —— 表把词条位映射到表现通道;
    /// 任何「按词条推断病种」的用法都违规则三-bis。</para></summary>
    public static class MaterialTable
    {
        /// <summary>`Material(p)`(GDD F-13.8 逐字)。
        /// <para>⚠️ **本方法是 `signs[]` 的唯一合法消费点**(AC-13-A5 白名单)。</para></summary>
        public static PresentMaterial Material(in VitalsDto vitals)
        {
            // 空集(无主观词条)⇒ 中性 —— 合法形态(GDD §Edge Cases 一)。
            if (vitals.SignCount <= 0 || vitals.SignChannelMask == 0)
                return PresentMaterial.Neutral;

            // 词条位 → 表现通道:直通映射(位即通道)。表内容的**语义**归 ADR-014 烘焙件,
            // 此处只保证「位到位、无病种推断、无九态重建」。
            return new PresentMaterial(
                toneVariant: ToneFromMask(vitals.SignChannelMask),
                postureBits: vitals.SignChannelMask,
                complexionBits: vitals.SignChannelMask);
        }

        /// <summary>��色变体 = 词条位的稳定折叠(纯函数,无 PRNG)。
        /// <para>⚠️ 只做**稳定折叠**,不做「按词条选病种音色」—— 后者是第二真源。</para></summary>
        private static int ToneFromMask(int mask)
        {
            unchecked
            {
                // 稳定折叠到 [0, 15]:与 44 的 ToneVariant 基数对齐(读表由 44 侧完成)。
                int folded = mask & 0xF;
                return folded == 0 ? 1 : folded;   // 非空词条集不得折叠成「中性音色」
            }
        }
    }

    /// <summary>一行的**投影输入** —— 13 内部形状,非 DTO。
    /// <para>⚠️ 含 `VitalsDto` ⇒ **不得**出现在 `IPresentPatients` 沿途任何 DTO 里;
    /// 它是投影的**起点**,不是终点(AC-13-C1)。</para></summary>
    public readonly struct PatientProjectionInput
    {
        /// <summary>病人 id。</summary>
        public readonly PatientId Id;
        /// <summary>行为状态(Story 001 `Map()` 产出)。</summary>
        public readonly BehaviorState Behavior;
        /// <summary>会诊态(边界层赐予,13 不推断 —— AC-13-B5)。</summary>
        public readonly SessionState Session;
        /// <summary>求医相(Story 002 `Phase`)。</summary>
        public readonly SeekingPhase Phase;
        /// <summary>逻辑格(Story 002 `LogiPose.Cell`)。</summary>
        public readonly WorldPos Cell;
        /// <summary>体征(9 的唯一浮点出口)—— **只用于 `Material(p)`,不进视图**。</summary>
        public readonly VitalsDto Vitals;

        public PatientProjectionInput(PatientId id, BehaviorState behavior, SessionState session,
                                      SeekingPhase phase, WorldPos cell, in VitalsDto vitals)
        {
            Id = id; Behavior = behavior; Session = session; Phase = phase; Cell = cell; Vitals = vitals;
        }
    }

    /// <summary>呈现投影(Story 003)—— 把 13 的派生态折成**视图 + 材质**。
    /// <para>⚠️ **不是 `IPresentPatients` 的实现**(那由在场集驱动,见 <see cref="PresentPatientsView"/>)——
    /// 本类型是**纯函数层**,输入一行、输出一行。</para></summary>
    public static class PresentationProjection
    {
        /// <summary>`ViewState(p)`(F-13.6)—— 委托 <see cref="ViewStateMap.ViewState"/>,**不自建第二真源**。
        /// <para>⚠️ **签名里不出现 `VitalsDto`** —— `signs[]` 不得参与状态判定(AC-13-A5)。</para></summary>
        public static PresentPatientState ViewState(in PatientProjectionInput input)
            => ViewStateMap.ViewState(input.Behavior, input.Session, input.Phase);

        /// <summary>投影一行的视图载荷(`PresentPatient`)。
        /// <para>⚠️ **输出恰 = {Id, Cell, State}** —— 无 `disease_id` / 无 `position` / 无 `trend` /
        /// 无 `signs[]`(AC-13-C1)。</para></summary>
        public static PresentPatient ToPresentPatient(in PatientProjectionInput input)
            => new PresentPatient(input.Id, input.Cell, ViewState(input));

        /// <summary>投影一行的表现材质(F-13.8)。
        /// <para>⚠️ **这是 `VitalsDto` 在本文件内的两个消费点之一** —— 另一处是
        /// <see cref="MaterialTable.Material(in VitalsDto)"/>(本方法只是它的转发)。
        /// **视图路径**(<see cref="ViewState"/>/<see cref="ToPresentPatient"/>)读不到 `VitalsDto`
        /// ⇒ `signs[]` 不参与状态判定(AC-13-A5)。</para></summary>
        public static PresentMaterial Material(in PatientProjectionInput input)
            => MaterialTable.Material(input.Vitals);

        // story-004 评审修复(S6,2026-10-07):此处原有 `IsVisible(bool present) => present`
        // —— **恒真死代码**(零生产调用方,唯一提及处是 spatial_behavior_test 的禁词数组字符串)
        // ⇒ 已删除。「场外者不进视图」的判据本就由 `IPresentPatients` 的在场登记承载,
        // 不需要一个恒真谓词。
    }

    /// <summary>`IPresentPatients` 的实装(Story 003)—— 在场登记 → 只读视图快照。
    ///
    /// <para>⚠️ **在场由「进入 / 离开」事件维护,不从查询拉** —— 与
    /// <see cref="PatientSpatialDirector"/> **同型**(该件以 `OnPresentEntered` / `OnPresentLeft`
    /// 维护自身派生态字典)。理由:`IPresenceQuery` 只有 `IsPresent` / `PresentCount`,
    /// **不暴露在场 id 集**;13 也**不枚举全库病人**(那是第二真源 + 违反零 spawn 纪律)。
    /// ⇒ 在场集是 13 从 9 的进出事件**收集**来的,不是它**推导**出来的(AC-13-C3/C4)。</para>
    ///
    /// <para>⚠️ **pull 语义**(Implementation Notes 4):37 在自己的 tick 里读
    /// <see cref="Snapshot"/>;13 **不 push**。`Rebuild()` 只重算**本 tick 的投影结果** ——
    /// 它不是缓存层,是求值产物(下一 tick 覆盖,离场者立即消失)。</para>
    ///
    /// <para>⚠️ **13 零 spawn/despawn**(AC-13-C4):本类型无任何病人实体创建 / 销毁调用,
    /// 只在册子上加减行。</para></summary>
    public sealed class PresentPatientsView : IPresentPatients
    {
        private readonly Func<PatientId, PatientProjectionInput> _project;

        // 在场册(升序 id —— 求值序钉死,承 Story 002 同型纪律)。**这是 13 的派生态,不落盘不写流。**
        private readonly SortedSet<int> _presentIds = new SortedSet<int>();
        private readonly Dictionary<int, PatientId> _ids = new Dictionary<int, PatientId>();

        // 本 tick 快照。
        private readonly List<PresentPatient> _snapshot = new List<PresentPatient>();

        /// <summary>`_project` 调用计数(测试接缝 —— 「场外者不被投影」的可观测点,AC-13-C3)。</summary>
        private int _projectionCalls;

        /// <summary>构造。</summary>
        /// <param name="project">单行投影器(测试可注入;生产侧由在场循环装配)。</param>
        public PresentPatientsView(Func<PatientId, PatientProjectionInput> project)
        {
            _project = project ?? throw new ArgumentNullException(nameof(project));
        }

        /// <summary>本 tick 的投影调用次数(测试接缝 —— 证「场外者零投影」)。</summary>
        public int ProjectionCalls => _projectionCalls;

        /// <summary>当前在册病人数(测试接缝 / 37 侧可达)。</summary>
        public int PresentCount => _presentIds.Count;

        /// <summary>病人进入在场范围(9 给;13 只登记 —— TR-patient-006/013)。
        /// <para>⚠️ 幂等:重复进入不叠加(同 id 覆盖)。</para></summary>
        public void OnPresentEntered(PatientId id)
        {
            _presentIds.Add(id.Value);
            _ids[id.Value] = id;
        }

        /// <summary>病人离开在场范围 —— 13 仅出册(**不删除病人实体**,AC-13-C4)。</summary>
        public void OnPresentLeft(PatientId id)
        {
            _presentIds.Remove(id.Value);
            _ids.Remove(id.Value);
            // 立即从快照移除 —— 离场者不得残留(Rebuild 前也成立)。
            for (int i = _snapshot.Count - 1; i >= 0; i--)
                if (_snapshot[i].Id.Value == id.Value) _snapshot.RemoveAt(i);
        }

        /// <summary>加载后重置(承 AC-13-B4 同型:派生态重新播种,不沿用)。
        /// <para>⚠️ 在场册由 9 侧重新灌入 —— 13 不自行推导。</para></summary>
        public void ResetForLoad()
        {
            _presentIds.Clear();
            _ids.Clear();
            _snapshot.Clear();
        }

        /// <summary>重建本 tick 快照(幂等:同一 tick 内重复调用结果相同)。
        /// <para>⚠️ **只有在册者被投影** —— 离场者**不在册** ⇒ 不进视图(AC-13-C3)
        /// 且**不被调用**(由 <see cref="ProjectionCalls"/> 可证伪)。</para></summary>
        public void Rebuild()
        {
            _snapshot.Clear();
            _projectionCalls = 0;

            // SortedSet 升序枚举 —— 快照序确定(与插入序无关)。
            foreach (int key in _presentIds)
            {
                var input = _project(_ids[key]);
                _projectionCalls++;
                _snapshot.Add(PresentationProjection.ToPresentPatient(input));
            }
        }

        /// <summary>当前在场病人视图快照(离场者不在其中 —— AC-13-C3)。</summary>
        public IReadOnlyList<PresentPatient> Snapshot() => _snapshot;
    }
}
