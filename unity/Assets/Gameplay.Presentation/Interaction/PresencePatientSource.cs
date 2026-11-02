// ADR 权威来源:
//   GDD design/gdd/interaction-system.md —— 规则二(**第三源** = 13 的 `IPresentPatients` 只读在场视图)
//   ADR-016 §三(候选集只出整数格,禁读表现态位置)· §六(37 读 13,13 不引用 37;4 读 13 的在场视图)
//   ADR-009 §四(候选集 = 派生态:每次重建,不进流、不存档)
//
// ⚠️ 本类之前 `IPatientSource` **零生产实装**(`CandidateSources.Patients` 在
//   `CompositionRoot` 里传的是 `null`)⇒ 4 的候选集里从来没有病人,
//   而 E 键的「打在谁身上」由 `InteractionInputDriver.NearestPresentPatient` **另算**:
//   它读的是 `PatientVisualSpawner` 的**视觉位置**(float)、并与玩家 `transform.position`
//   比平方距离 —— 既违 ADR-016 §三(禁读表现态位置),也是「4 是唯一目标选择器」的第二处例外。
//   `TD-interaction-01` 的消解判据 ① 即本类。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.PatientAI;   // PresenceRegistry(13 的只读在场视图)
using DaYiJingCheng.Sim.Contracts;        // PatientId / WorldPos

namespace DaYiJingCheng.Gameplay.Interaction
{
    /// <summary>
    /// 第三源的生产实装:把 13 的 <see cref="PresenceRegistry"/> 投影成 4 的候选行。
    /// <para><b>只出「格 + patient_id」</b>(`PatientDto` 的全部字段)—— `VitalsDto`
    /// 物理上进不了本类的返回类型(AC-4-05)。</para>
    /// <para><b>无缓存</b>:`Rows` 每次现算 —— 候选集是派生态(ADR-009 §四),
    /// 在场集会变,缓存即第二真源。</para>
    /// </summary>
    public sealed class PresencePatientSource : IPatientSource
    {
        private readonly PresenceRegistry _presence;

        /// <param name="presence">13 的在场视图(生产 = 组合根持有的同一个 `PresenceRegistry`)。</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="presence"/> 为 null。</exception>
        public PresencePatientSource(PresenceRegistry presence)
        {
            _presence = presence ?? throw new System.ArgumentNullException(nameof(presence));
        }

        /// <inheritdoc/>
        /// <remarks>顺序**不承载语义** —— 全序由 F-4.1 三键定(见 `ICandidateSource` 契约)。
        /// 格取不到的在场病人**跳过**(不编造坐标)。</remarks>
        public IReadOnlyList<PatientDto> Rows
        {
            get
            {
                IReadOnlyCollection<int> ids = _presence.PresentPatientIds();
                var rows = new List<PatientDto>(ids.Count);
                foreach (int pid in ids)
                {
                    if (_presence.TryGetCell(new PatientId(pid), out WorldPos cell))
                        rows.Add(new PatientDto(cell, pid));
                }
                return rows;
            }
        }
    }
}
