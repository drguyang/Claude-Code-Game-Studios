// 42 侧「病人 cue 可见对应物」的判据(`AC-13-F1` 的 42 侧达成面 · 2026-10-29 · 缺口 D)。
//
// ── 权威来源 ────────────────────────────────────────────────────────────────
//   · `design/gdd/patient-ai.md` **AC-13-F1**(`:1088`):「咳嗽 / 呻吟 / 呼吸三类 cue
//     均有一个可见对应物……且该对应物在**默认设置**下亦可见(**不需额外开关**)」
//   · 同处 2026-09-25 二轮回填:「对应物须**非色相可辨**(**形状 / 明度 / 姿态**承载)」
//   · `design/gdd/audio-system.md` AC-44-12 ①(视觉替代 = 引用 AC-13-F1)
//   · `design/gdd/skeuomorphic-ui.md` §Visual 二(每形状一语义;新增语义须**新增形状**)
//
// ── 判据面 ──────────────────────────────────────────────────────────────────
// ① **三状态各有一个可见物**(不是「枚举存在」);
// ② **非色相**:三态的字形两两互异(形状通道)+ USS 类两两互异(明度通道),
//    且**不靠颜色**——判据 = 渲染文本本身就不同,故「只改颜色」的退化会直接转红;
// ③ **默认可见、无开关**:构造即可见,且类型上不存在 Enabled / Toggle 类成员;
// ④ **42 不判时机**:反射断言不含 Timer / Duration / AutoHide 类成员;
// ⑤ **形状所有权**:三形与 `MarkRegistry` 的六形**不相交**(新增族,不是借用)。

using System;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.SkeuomorphicUI
{
    /// <summary>`PatientCueMarkView` / `PatientCueMarks` 的元件级判据。</summary>
    public sealed class PatientCueMarkViewTest
    {
        private static readonly PatientCueVisual[] AllKinds =
        {
            PatientCueVisual.Cough, PatientCueVisual.Groan, PatientCueVisual.Breath,
        };

        /// <summary>**三态各有形状标记且两两互异**(形状通道)——「非色相可辨」的第一半。</summary>
        [Test]
        public void test_threeKinds_haveDistinctShapes()
        {
            Assert.That(PatientCueMarks.KindCount, Is.EqualTo(3),
                "AC-13-F1 逐字三类(咳嗽 / 呻吟 / 呼吸)—— 少一类即该类的可见物缺失");

            string[] shapes = AllKinds.Select(PatientCueMarks.ShapeOf).ToArray();
            foreach (string g in shapes)
                Assert.That(string.IsNullOrEmpty(g), Is.False, "字形不得为空");
            CollectionAssert.AllItemsAreUnique(shapes,
                $"三态形状标记须两两互异(形状通道,非色相)—— 实得 {string.Join(" / ", shapes)}");
        }

        /// <summary>**三态各有 USS 类且两两互异**(字重 / 明度通道)—— 第二半。</summary>
        [Test]
        public void test_threeKinds_haveDistinctUssClasses()
        {
            string[] classes = AllKinds.Select(PatientCueMarks.UssClassOf).ToArray();
            CollectionAssert.AllItemsAreUnique(classes,
                $"三态 USS 类须两两互异(明度通道)—— 实得 {string.Join(" / ", classes)}");
        }

        /// <summary>**形状所有权**:三形与 `MarkRegistry` 的六记号**不相交**。
        /// <para>⚠️ `skeuomorphic-ui.md` §Visual 二:「每个形状只有一个拥有者、一个语义;
        /// 新增语义**须新增形状,不得借用**」。本测钉住「这是新增族」。</para></summary>
        [Test]
        public void test_shapes_disjointFromMarkRegistry()
        {
            var registered = new System.Collections.Generic.List<string>();
            for (int i = 0; i < MarkRegistry.ShapeCount; i++)
                registered.Add(MarkRegistry.GetUssClass((MarkShape)i));

            foreach (PatientCueVisual kind in AllKinds)
            {
                string cls = PatientCueMarks.UssClassOf(kind);
                Assert.That(registered.Contains(cls), Is.False,
                    $"「{cls}」借用了记号登记表的形状 —— 新增语义须新增形状(§Visual 二)");
            }
        }

        /// <summary>**三个可见物真的画出来、且文本两两不同** —— 这一条同时钉住「非色相」:
        /// 若日后有人把区分退化成「同一字形换颜色」,三态文本会变得相同 ⇒ 本测转红。
        /// <para>⚠️ 判据面 = `Label.text`,不是「我调过 Mark」。</para></summary>
        [Test]
        public void test_renderedTexts_differPerKind()
        {
            var texts = new System.Collections.Generic.List<string>();
            foreach (PatientCueVisual kind in AllKinds)
            {
                var view = new PatientCueMarkView();
                view.Mark(new PatientCueMarkData(patientId: 1, kind: kind));

                Label label = view.Q<Label>();
                Assert.That(label, Is.Not.Null, $"「{kind}」Mark 后须有 Label(否则只是内部字段)");
                texts.Add(label.text);
            }

            CollectionAssert.AllItemsAreUnique(texts,
                $"三态的**渲染文本**须两两不同(非色相的机械判据)—— 实得 {string.Join(" / ", texts)}");
        }

        /// <summary>**默认可见、无开关** —— 构造即可见,且**不需要**任何设置项打开。</summary>
        [Test]
        public void test_defaultVisible_withoutAnyToggle()
        {
            var view = new PatientCueMarkView();

            Assert.That(view.IsShowing, Is.True,
                "构造后即须可见 —— AC-13-F1:「该对应物在**默认设置**下亦可见(不需额外开关)」");
            Assert.That(view.MarkCount, Is.Zero, "初始无可见物(空态,不是隐藏)");
        }

        /// <summary>**类型上不存在开关** —— 反射断言。
        /// <para>⚠️ 若日后有人加一个 `VisualRedundancyEnabled` 并默认 false,
        /// AC-13-F1 的「不需额外开关」即被推翻 ⇒ 本测转红。</para></summary>
        [Test]
        public void test_noToggleMembers_reflection()
        {
            const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic
                                   | BindingFlags.Instance | BindingFlags.Static
                                   | BindingFlags.DeclaredOnly;

            string[] forbidden = { "Enabled", "Toggle", "Option", "Switch", "Redundancy", "ShowMarks" };

            foreach (MemberInfo m in typeof(PatientCueMarkView).GetMembers(All))
                foreach (string bad in forbidden)
                    Assert.That(m.Name.IndexOf(bad, StringComparison.OrdinalIgnoreCase), Is.LessThan(0),
                        $"可见物不得有「{bad}」类成员(`{m.Name}`)—— AC-13-F1:不需额外开关");
        }

        /// <summary>**42 不判时机** —— 反射断言(与 `SubtitleLineView` 同款纪律)。</summary>
        [Test]
        public void test_noTimingMembers_reflection()
        {
            const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic
                                   | BindingFlags.Instance | BindingFlags.Static
                                   | BindingFlags.DeclaredOnly;

            string[] forbidden = { "Timer", "Duration", "AutoHide", "Timeout", "Expiry", "Seconds" };

            foreach (MemberInfo m in typeof(PatientCueMarkView).GetMembers(All))
                foreach (string bad in forbidden)
                    Assert.That(m.Name.IndexOf(bad, StringComparison.OrdinalIgnoreCase), Is.LessThan(0),
                        $"可见物不得含「{bad}」类成员(`{m.Name}`)—— 时机归上游,42 只渲染");
        }

        /// <summary>同病人同类**只留一条**(刷新而非堆叠)。</summary>
        [Test]
        public void test_samePatientSameKind_refreshesInsteadOfDuplicating()
        {
            var view = new PatientCueMarkView();
            view.Mark(new PatientCueMarkData(1, PatientCueVisual.Cough));
            view.Mark(new PatientCueMarkData(1, PatientCueVisual.Cough));

            Assert.That(view.MarkCount, Is.EqualTo(1), "同病人同类重复投递不得堆成两条");
        }

        /// <summary>容量有界:超出 <see cref="PatientCueMarkView.MaxMarks"/> 挤掉最早的一条。</summary>
        [Test]
        public void test_capacity_boundedAtMaxMarks()
        {
            var view = new PatientCueMarkView();
            for (int id = 1; id <= PatientCueMarkView.MaxMarks + 1; id++)
                view.Mark(new PatientCueMarkData(id, PatientCueVisual.Cough));

            Assert.That(view.MarkCount, Is.EqualTo(PatientCueMarkView.MaxMarks),
                "一次性可见物无「结束」信号 ⇒ 容器须有界(否则屏上堆满字形)");
        }

        /// <summary>`ClearPatient` 只摘该病人,别家的留着。</summary>
        [Test]
        public void test_clearPatient_removesOnlyThatPatient()
        {
            var view = new PatientCueMarkView();
            view.Mark(new PatientCueMarkData(1, PatientCueVisual.Cough));
            view.Mark(new PatientCueMarkData(2, PatientCueVisual.Cough));

            view.ClearPatient(1);

            Assert.That(view.MarkCount, Is.EqualTo(1), "只摘该病人的可见物");
        }

        /// <summary>`ClearAll` 清空(场景卸载 / 读档前)。</summary>
        [Test]
        public void test_clearAll_empties()
        {
            var view = new PatientCueMarkView();
            view.Mark(new PatientCueMarkData(1, PatientCueVisual.Cough));
            view.Mark(new PatientCueMarkData(2, PatientCueVisual.Breath));

            view.ClearAll();

            Assert.That(view.MarkCount, Is.Zero, "ClearAll 后不得留残影");
            Assert.That(view.childCount, Is.Zero, "子件须一并清空(内部表清空但子件留着 = 屏上残影)");
        }

        /// <summary>越界枚举值 ⇒ 具名抛出(不得静默取到某个字形)。</summary>
        [Test]
        public void test_outOfRangeKind_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PatientCueMarks.ShapeOf((PatientCueVisual)99));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PatientCueMarks.UssClassOf((PatientCueVisual)99));
        }
    }
}
