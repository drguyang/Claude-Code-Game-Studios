// 「结案」的**装配层驱动** —— T19 甲(2026-11-10)
//
// 权威:
//   `design/gdd/case-system.md` 规则六(结案唯一出口 · 已处置前置 · 单例 · 不可撤销 · 幂等拒收)
//   ADR-008 §四(处置证据窗口化 + 快照)· ADR-006 Amendment A(`CaseId` 三元组)
//   `CaseCloseWriter`(判据与写入的唯一落点)· `CaseStreamQuery`(开案与病种集的共用读法)
//   C3-a 的 `HookCaseReferDriver` —— 同一形状(T19 把它的反查提到 `CaseStreamQuery`)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本件补的是什么断点
// ─────────────────────────────────────────────────────────────────────────────
//   `CaseCloseWriter.TryClose` **生产调用点 = 0** —— 写者自己的头注就写着
//   「生产触发点(结案 UI『已处置』勾选 + 确认 → 本写者)尚未接线」,
//   `PatternDetector.cs:479` 也重复了这句。
//
//   ⇒ 后果不是「少一个按钮」:
//     ① **三条 Case 源解锁规则全部不可达**(技法 2 清创 / 3 蛇伤 / 10 柳树皮);
//     ② 病例生命周期停在**立案**,永不收口;
//     ③ `Folded(p)` 里「无未结案病例」那一半永远假(ADR-008 §四)→ 7a 折叠永不发生。
//
//   本件把「按键 → 哪一案 → 结案」这条链接上。判据一行不复制:`该不该结` 全在
//   `CaseCloseWriter` 里(它自己复核四条前置),本件只负责**把参数凑齐**。
//
// ─────────────────────────────────────────────────────────────────────────────
// `isChecked = true` 是什么意思(别读成「跳过校验」)
// ─────────────────────────────────────────────────────────────────────────────
//   GDD 规则六-4 要求「处置证据(流的事实)**且**玩家自述已处置(checkbox)」两件同时成立。
//   按键就是玩家的自述 —— 按下「结案」= 他声明「这一例我处置完了」。
//   ⇒ 本件传 `isChecked: true`,而**证据那半边仍由写者从流里查**
//     (`HasTreatmentInWindow`);窗口内没有处置事件 ⇒ 写者照拒(`RejectNoTreatment`)。
//   两半是**与**的关系,本件只提供其中一半。
//
// ⚠️ **本件不判**:病种集该不该是这些(那是流的事实)、这一例该不该结(37 写者)、
//    玩家此刻在哪 / 面朝谁(按键路由由 `InteractionInputDriver` 给「最近在场病人」)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>把一次「结案键」翻译成一条 `CaseClosed`(病例流)。</summary>
    public sealed class CaseCloseDriver
    {
        private readonly CaseCloseWriter _writer;
        private readonly Func<IReadOnlyList<SimEvent>> _events;
        private readonly IPayloadDecoder _decoder;
        private readonly Func<long> _tick;

        /// <param name="writer">37 的结案写者(唯一 Append 落点;四条前置在它内部复核)。</param>
        /// <param name="events">事件流只读视图(反查开着的病例 / 该病人的病种集)。</param>
        /// <param name="decoder">载荷解码器(ADR-031)。</param>
        /// <param name="tick">当前 tick(`ITickProvider.CurrentTick`)—— 同时是处置证据窗口的上界。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public CaseCloseDriver(CaseCloseWriter writer,
                               Func<IReadOnlyList<SimEvent>> events,
                               IPayloadDecoder decoder,
                               Func<long> tick)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _tick = tick ?? throw new ArgumentNullException(nameof(tick));
        }

        /// <summary>
        /// 把该病人**当前开着的那一例**结掉。
        /// </summary>
        /// <param name="patient">病人(按键时的最近在场者;None ⇒ 直接拒)。</param>
        /// <returns>写者的结果码(零写入分支亦具名)。</returns>
        /// <remarks>
        /// <para>没有开着的病例 ⇒ <see cref="CaseCloseWriteResult.RejectNotOpen"/>
        /// (与写者「未开」同码 —— 两种情形对调用方是同一件事:按下去什么也没发生,
        /// 但**原因可归因**)。</para>
        /// <para>病种集 = `CaseStreamQuery.DiseaseSetOf`(该病人全部 `DiseaseOnset`,
        /// 位序 `DiseaseId − 1`)。**不用**开案快照 —— 它在生产上恒为空集
        /// (`CaseOpenedDriver.TryOpen` 传 `default`),见 `CaseStreamQuery` 头注。</para>
        /// </remarks>
        public CaseCloseWriteResult TryClose(PatientId patient)
        {
            if (patient.IsNone) return CaseCloseWriteResult.RejectNotOpen;

            // 开着的病例 = 既有读法 `CaseStreamQuery.OpenCaseOf`(不在这里再写一份扫描 ——
            // 那份判据已被 CaseOpenWriter / JudgmentWriter / FoldedPredicate 共用)。
            // 「这一案到底能不能结」由写者对着真流复核,本件不预判。
            CaseId? openCase = CaseStreamQuery.OpenCaseOf(_events(), patient.Value);
            if (!openCase.HasValue) return CaseCloseWriteResult.RejectNotOpen;
            CaseId caseId = openCase.Value;

            DiseaseIdSet diseaseSet = CaseStreamQuery.DiseaseSetOf(_events(), _decoder, patient);

            // isChecked = 玩家按下「结案」即自述已处置;证据那半边仍由写者查流(见文件头)。
            return _writer.TryClose(_tick(), caseId, diseaseSet, isChecked: true);
        }
    }
}
