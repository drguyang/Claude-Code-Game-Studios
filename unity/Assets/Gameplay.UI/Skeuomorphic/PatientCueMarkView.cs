// 病人 cue 可见物的**呈现元件**(`AC-13-F1` 的 42 侧达成面 · 2026-10-29 · 缺口 D)。
//
// ── 设计边界 ────────────────────────────────────────────────────────────────
// ① **默认可见、无开关** —— `AC-13-F1` 逐字:「该对应物在**默认设置**下亦可见
//    (**不需额外开关**)」。故本类**没有**任何 `Enabled` / `Toggle` / `Option` 类成员;
//    构造即可见(容器始终 `display: Flex`,只是可能没有子件)。
// ② **不判时机** —— 无 `Timer` / `duration` / `autoHide` 类成员。何时出现 / 消失
//    由上游发射点(`PatientCueDriver`)经 `IPatientCueMarkSink` 决定。
// ③ **不持游戏状态** —— 只保留**渲染所需**的末 N 条的键(`(病人, 类)` 二元组),
//    不读体征、不推病情、不解 `disease_id`(承 ADR-013 §9 C3 · AC-37-15)。
//
// ── 为什么留一个**有界**的末 N 条 ──────────────────────────────────────────
// 一次性 cue(咳嗽 / 呻吟)没有「结束」信号 —— 上游发一次就完,42 又不能自己计时。
// 若无限累积,屏上会堆满字形。故容器只保留**末 `MaxMarks` 条**(挤掉最早的),
// 这是**渲染容量**,不是业务保留期(与 `SubtitleLineView.MaxLines` 同性质)。

using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>病人 cue 的可见物容器(三类字形;形状 + 字重双承载,非色相)。</summary>
    /// <remarks>⚠️ 本类**实现** <see cref="IPatientCueMarkSink"/>,是 42 侧唯一接收口的落地件。</remarks>
    public sealed class PatientCueMarkView : VisualElement, IPatientCueMarkSink
    {
        /// <summary>USS 类名(主题变量挂点;颜色由主题给,分辨**不依赖**它)。</summary>
        public const string UssClass = "cue-mark-row";

        /// <summary>同时保留的可见物上限(渲染容量,非业务保留期)。</summary>
        public const int MaxMarks = 3;

        // (病人, 类)二元组,末位 = 最近。容量 ≤ MaxMarks。
        private readonly List<PatientCueMarkData> _marks = new List<PatientCueMarkData>(MaxMarks);

        /// <summary>构造元件(可见但空;不需任何开关)。</summary>
        public PatientCueMarkView()
        {
            AddToClassList(UssClass);
            style.display = DisplayStyle.Flex;   // 默认可见 —— 不是「等开关」
        }

        /// <summary>当前保留的条数(≤ <see cref="MaxMarks"/>)。</summary>
        public int MarkCount => _marks.Count;

        /// <summary>当前是否可见(渲染态,**不是**业务判断)。</summary>
        public bool IsShowing => resolvedStyle.display != DisplayStyle.None;

        /// <inheritdoc/>
        public void Mark(in PatientCueMarkData mark)
        {
            for (int i = 0; i < _marks.Count; i++)
            {
                if (_marks[i].PatientId == mark.PatientId && _marks[i].Kind == mark.Kind)
                {
                    _marks.RemoveAt(i);
                    break;
                }
            }

            _marks.Add(mark);
            if (_marks.Count > MaxMarks) _marks.RemoveAt(0);

            Rebuild();
        }

        /// <inheritdoc/>
        public void ClearPatient(int patientId)
        {
            bool removed = false;
            for (int i = _marks.Count - 1; i >= 0; i--)
            {
                if (_marks[i].PatientId == patientId)
                {
                    _marks.RemoveAt(i);
                    removed = true;
                }
            }
            if (removed) Rebuild();
        }

        /// <inheritdoc/>
        public void ClearAll()
        {
            if (_marks.Count == 0) return;
            _marks.Clear();
            Rebuild();
        }

        // 子件整体重建(N ≤ 3,代价可忽略;换来「视图 = 键序列的纯函数」)。
        private void Rebuild()
        {
            Clear();
            for (int i = 0; i < _marks.Count; i++)
            {
                var label = new Label(PatientCueMarks.ShapeOf(_marks[i].Kind));
                label.AddToClassList(PatientCueMarks.UssClassOf(_marks[i].Kind));
                Add(label);
            }
        }
    }
}
