// 病人 cue 的**非色相可见对应物**(`AC-13-F1` 的 42 侧达成面 · 2026-10-29 · 缺口 D)。
//
// ── 权威来源 ────────────────────────────────────────────────────────────────
//   · `design/gdd/patient-ai.md` **AC-13-F1**(`:1088`):「咳嗽 / 呻吟 / 呼吸三类 cue
//     均有一个可见对应物……且该对应物在**默认设置**下亦可见(不需额外开关)」
//   · `patient-ai.md:1088` 的 **2026-09-25 二轮 44 无障碍回填**:
//     「对应物须**非色相可辨**(**形状 / 明度 / 姿态**承载)」——
//     原句的「面色变化」是纯色通道(WCAG 1.4.1),色觉盲玩家读不出
//   · `design/gdd/audio-system.md` **AC-44-12** ①:「视觉替代 = 引用 `AC-13-F1`」
//   · `design/accessibility-requirements.md:95`「色-only 铁律」(非颜色差异须同时存在)
//
// ── 为什么是 42 ────────────────────────────────────────────────────────────
// 13 侧只判**结构前提**(cue 语义是独立枚举 id / 呼吸层默认在),
// 其夹具自陈「达成面在 42 的冗余通道」(见 `presentation_projection_test.cs:921` 一节的注)。
// 42 = 呈现层(ADR-013 §9 C3「只渲染,永不持有游戏状态」)⇒ 可见物由 42 落。
//
// ── 形状 + 明度的双承载(不是色相)───────────────────────────────────────────
// 三类各占一个**互异字形**(形状通道),并有各自的 USS 类(字重 = 明度通道)。
// 两者都不含颜色语义 ⇒ 色觉盲玩家凭形状即可分辨。
//
// ⚠️ **形状所有权**:`skeuomorphic-ui.md` §Visual 二的记号登记表规定
//    「每个形状只有一个拥有者、一个语义;**新增语义须新增形状,不得借用**」。
//    故本类的三形与 `MarkRegistry` 的六形**互不相交**(由夹具钉住,见
//    `patient_cue_mark_view_test.cs` 的 `test_shapes_disjointFromMarkRegistry`)——
//    这是**新增族**,不是借用既有记号。
//
// ⚠️ **本类不含时机**:无 `Timer` / `duration` / `autoHide` 类成员 —— 何时出现、
//    何时消失由上游发射点(`PatientCueDriver`)决定,与 `SubtitleLine` 同款纪律。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>病人 cue 的三类可见对应物(`AC-13-F1` 逐字的三类)。</summary>
    public enum PatientCueVisual
    {
        /// <summary>咳嗽(一次性)。</summary>
        Cough = 0,

        /// <summary>呻吟(一次性)。</summary>
        Groan = 1,

        /// <summary>呼吸(持续态;起 / 停由上游驱动)。</summary>
        Breath = 2,
    }

    /// <summary>一条 cue 可见物的载荷。
    /// <para>⚠️ 全整数域、**不含 `disease_id`** —— 承 ADR-013 §9 C3 与
    /// `PresentationDtoGuard` 的递归扫描(AC-37-15:病种不得进呈现层)。</para></summary>
    public readonly struct PatientCueMarkData
    {
        /// <summary>病人 id(呈现层只用于「同一条的刷新 / 摘除」,不作业务判断)。</summary>
        public readonly int PatientId;

        /// <summary>三态之一。</summary>
        public readonly PatientCueVisual Kind;

        /// <summary>构造。</summary>
        public PatientCueMarkData(int patientId, PatientCueVisual kind)
        {
            PatientId = patientId;
            Kind = kind;
        }
    }

    /// <summary>42 侧的 cue 可见物接收口(上游发射点投递)。
    /// <para>⚠️ **无「要不要显示」的反问** —— 42 只渲染,判断归上游。</para></summary>
    public interface IPatientCueMarkSink
    {
        /// <summary>出现 / 刷新一条可见物(同病人同类的既有条目移至末位,不新增重复)。</summary>
        void Mark(in PatientCueMarkData mark);

        /// <summary>摘除该病人的**全部**可见物(离场 / 终止)。
        /// <para>⚠️ 不取名 `Clear(...)` —— 与 `VisualElement.Clear()` 并存会把调用点读成两件事。</para></summary>
        void ClearPatient(int patientId);

        /// <summary>摘除全部可见物(场景卸载 / 读档前)。
        /// <para>⚠️ 不取名 `Clear()` —— 那会遮住 `VisualElement.Clear()`,把重建路径指错。</para></summary>
        void ClearAll();
    }

    /// <summary>三类可见物的**形状单一出处**(形状标记 + USS 类名)。
    /// <para>⚠️ 形状标记是**形状通道**,USS 类承载**字重(明度)通道**;两者都不表达颜色。</para></summary>
    public static class PatientCueMarks
    {
        /// <summary>登记的三态数(恒 3 —— `AC-13-F1` 逐字的三类)。</summary>
        public const int KindCount = 3;

        // 形状标记:爆点(突发)/ 波形(绵长)/ 环(周而复始)。
        // ⚠️ 选型判据 = **两两在低分辨率下仍可辨**,且不含颜色语义。
        private static readonly string[] ShapeByKind = { "✳", "〰", "◎" };

        // 明度通道:粗 / 常规 / 细 —— 与形状同向叠加,不互相替代。
        private static readonly string[] UssByKind =
        {
            "cue-mark-cough", "cue-mark-groan", "cue-mark-breath",
        };

        /// <summary>取形状标记(形状通道)。</summary>
        public static string ShapeOf(PatientCueVisual kind)
        {
            int i = (int)kind;
            if (i < 0 || i >= ShapeByKind.Length)
                throw new System.ArgumentOutOfRangeException(nameof(kind), kind,
                    "[PatientCueMarks] cue 可见物超出登记范围(三类:Cough / Groan / Breath)。");
            return ShapeByKind[i];
        }

        /// <summary>取 USS 类名(字重 / 明度通道)。</summary>
        public static string UssClassOf(PatientCueVisual kind)
        {
            int i = (int)kind;
            if (i < 0 || i >= UssByKind.Length)
                throw new System.ArgumentOutOfRangeException(nameof(kind), kind,
                    "[PatientCueMarks] cue 可见物超出登记范围(三类:Cough / Groan / Breath)。");
            return UssByKind[i];
        }
    }
}
