// 「结案页」的**装配层驱动** —— T20 第二批(2026-11-10 用户裁定:丙 = 独立结案页 + 纸面墨勾)
//
// 权威:
//   `design/ux/casebook-39.md:370` 的 `OQ-CB-7`(形态归 39 / 落地归 42)
//   `design/ux/case-close-42.md`(本屏 spec,同批落盘)
//   `design/gdd/case-system.md` 规则八 —— 可结案 = 开 ∧ 已处置 ∧ 玩家勾选
//   `CaseCloseDriver`(判定的唯一落点;本件只把「屏上的墨勾」翻成它的 `isChecked`)
//
// ─────────────────────────────────────────────────────────────────────────────
// 与 T19 的关系(改了什么)
// ─────────────────────────────────────────────────────────────────────────────
//   T19 把 `C` 直接接到 `CaseCloseDriver.TryClose(patient, isChecked: true)` —— 一次按键即结案,
//   「已处置」是**隐式自述**。GDD 规则八要的是「显式勾选」,且 37 的 `AC-37-04`(BLOCKING)
//   要求那个人动作**存在且可判**。
//   ⇒ 本件把 `C` 改成**开结案页**:墨勾 = 显式自述(可勾可不勾),「结案」行才真写。
//   驱动的判据一字未改:窗口内无处置照拒,只是拒绝现在**在屏上有话说**。
//
// ⚠️ 本件不判:有没有开着的病例(`CaseStreamQuery.OpenCaseOf`,与脉案 / 转诊同一读法)、
//   这一例该不该结(37 的写者)、屏长什么样(42 的 `CaseCloseScreen`)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>结案页的意图出口实现(开屏 / 墨勾 → 提交 / 合屏)。</summary>
    public sealed class CaseClosePresenter : ICaseCloseCommandSink
    {
        private readonly CaseCloseDriver _driver;
        private readonly SkeuoRuntimeDriver _ui;
        private readonly Func<IReadOnlyList<SimEvent>> _events;

        private PatientId _patient = PatientId.None;
        private CaseCloseScreen _screen;

        /// <param name="driver">37 的结案驱动(唯一判定落点)。</param>
        /// <param name="ui">42 的模态驱动(开 / 合本屏)。</param>
        /// <param name="events">事件流只读视图(判「他有没有开着的病例」)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public CaseClosePresenter(CaseCloseDriver driver, SkeuoRuntimeDriver ui,
                                  Func<IReadOnlyList<SimEvent>> events)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _events = events ?? throw new ArgumentNullException(nameof(events));
        }

        /// <summary>当前结案页正在处理哪个病人(<see cref="PatientId.None"/> = 无)。</summary>
        public PatientId Patient => _patient;

        /// <summary>
        /// 为 <paramref name="patient"/> 打开结案页。
        /// </summary>
        /// <returns><c>false</c> = 没开(病人无开着的病例 / 已有别的模态 / 哨兵)。</returns>
        /// <remarks>「没有开着的病例」时**不开空页** —— 空页会让玩家以为有东西可结。</remarks>
        public bool TryOpen(PatientId patient)
        {
            if (patient.IsNone) return false;
            if (!CaseStreamQuery.OpenCaseOf(_events(), patient.Value).HasValue) return false;
            if (!_ui.OpenModal(ModalId.CaseClose)) return false;

            _screen = _ui.Screen as CaseCloseScreen;
            if (_screen == null)
            {
                // 闭集表 / 工厂 / 屏类型三处不一致 —— fail-loud 而不是留一张空模态挡着世界。
                _ui.CloseModal();
                throw new InvalidOperationException(
                    "ModalId.CaseClose 开出来的屏不是 CaseCloseScreen —— 闭集三处不一致");
            }

            _patient = patient;
            _screen.Bind(this);
            _screen.ResetForOpen();
            return true;
        }

        /// <inheritdoc/>
        /// <remarks>墨勾态由屏自己持(纯呈现);本件在提交时**读**它 —— 单一真源在屏上。</remarks>
        public void ToggleInk()
        {
        }

        /// <inheritdoc/>
        public bool SubmitClose()
        {
            if (_screen == null || _patient.IsNone) return false;

            CaseCloseWriteResult result = _driver.TryClose(_patient, _screen.IsInkChecked);
            if (result == CaseCloseWriteResult.Closed)
            {
                _ui.CloseModal();
                Clear();
                return true;
            }

            _screen.ShowReject(ReasonOf(result));
            return false;
        }

        /// <inheritdoc/>
        public void Cancel()
        {
            _ui.CloseModal();
            Clear();
        }

        private void Clear()
        {
            _screen = null;
            _patient = PatientId.None;
        }

        /// <summary>
        /// 结果码 → 淡墨行文案。
        /// <para>⚠️ 文案**只说明「为何没生效」**,不透露任何病种 / 数值 / 进度 ——
        /// 承 37「系统沉默」纪律与 `AC-42-F3` 的禁词表。</para>
        /// </summary>
        private static string ReasonOf(CaseCloseWriteResult result)
        {
            switch (result)
            {
                case CaseCloseWriteResult.RejectNoTreatment: return "病未处置";
                case CaseCloseWriteResult.RejectNotChecked:  return "未勾「已处置」";
                case CaseCloseWriteResult.RejectAlreadyClosed: return "此案已结";
                default: return "此案已合";
            }
        }
    }
}
