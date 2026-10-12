// sprint-08 S1.1 件 F2/G · 病名册指认 → 落笔**全链**判据(PlayMode · 2026-10-12)
//
// 判据(可证伪 · 承 sprint-08 §3 的 `test_lexiconBook_pointIdentify_*`):
//   ① 42 屏(真 CasebookScreen)病名栏落焦 + 提交 ⇒ 摊开 ⇒ 条目行落焦 + 提交
//      ⇒ **病例流新增 JudgmentRecorded**,载荷 lexiconId = 词表反查出的**显式 id**、
//      confidence = 疑似(首落笔);
//   ② 档位经 DTO 回读(PendingConfidence)随落笔推进 —— 42 不持档位;
//   ③ 墨龄 > 0(落笔后 `InkAgeTicks` 从 0 起算,AC-42-G6);
//   ④ 三态循环全链:同栏再提交 ×2 ⇒ 流里依次出现 疑似 → 确定 → 擦除(lexicon_id = 0);
//   ⑤ **变异敏感**:不注入命令面 ⇒ 零判断事件(证明本链不是「恒真」的空转)。
//
// ⚠️ 与 `casebook_lexicon_book_test` 的分工(不重复):
//   · 那边 = 42 屏**单元**(语义 + 接线,fixture 假源假汇);
//   · 这边 = **跨层全链**(真组合根 + 真病人 + 真开案 + 真词表 + 真 JudgmentWriter),
//     两端的假件只有「已见集」的构造 —— 故 ⑤ 的变异断言能真正证伪「接线存在」。
//
// ⚠️ 派发口径(2026-10-12 实测,同 casebook_lexicon_book_test ⑦):
//   `NavigationSubmitEvent` 的目标由 `FocusController.focusedElement` 决定 ⇒ 须先 Focus。

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>病名册指认 → 落笔全链判据(sprint-08 S1.1 件 F2/G)。</summary>
    internal sealed class casebook_point_identify_chain_test
    {
        private const int PlayerId = 9001;
        private const int DiseaseId = 1;          // 内联注册表的病种 1(词表反查有 id)

        private GameObject _go;
        private UIDocument _doc;
        private PanelSettings _panelSettings;
        private CompositionRootServices _bag;
        private PatientId _patient;

        [TearDown]
        public void TearDown()
        {
            if (_doc != null) Object.Destroy(_doc);
            if (_panelSettings != null) Object.Destroy(_panelSettings);
            if (_go != null) Object.Destroy(_go);
            _doc = null; _panelSettings = null; _go = null;
            _bag = null;
        }

        /// <summary>真组合根 + 真病人 + 真开案 + 作者 + 观看者(落笔前置齐备)。</summary>
        private void AssembleChain()
        {
            _bag = CompositionRoot.Assemble();
            _patient = _bag.PatientSpawner.SpawnNext(diseaseId: DiseaseId, tick: 0);
            _bag.Presence.AddPresent(_patient, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);
            _bag.TickDriver.Advance(0.05d);                                  // → tick 1
            _bag.CaseOpenedDriver.OnTickEdge(_bag.TickDriver.CurrentTick);    // 开案

            _bag.PrescriptionPage.SetAuthor(PlayerId);
            _bag.CasebookSource.SetViewer(PlayerId);
            _bag.CasebookSource.ChangePatient(_patient);   // 含命令路由换绑
        }

        /// <summary>
        /// 让「已见集」非空 —— 走**真链**:查体 ⇒ SeenDiseaseProjector 写 SkillGrown
        /// ⇒ NoveltyTracker 回填 ⇒ 病名册反查出 lexicon_id。不直接塞 tracker。
        /// </summary>
        private void ExaminePatient()
        {
            _bag.TickDriver.Advance(0.05d);
            _bag.SeenDisease.OnExamined(_patient, _bag.TickDriver.CurrentTick, PlayerId);
        }

        private IEnumerator WaitForRoot(UIDocument doc)
        {
            float t0 = Time.realtimeSinceStartup;
            while (doc.rootVisualElement == null)
            {
                if (Time.realtimeSinceStartup - t0 > 2f)
                {
                    Assert.Fail("[夹具红] UIDocument.rootVisualElement 2s 未就绪(panelSettings 未生效)。");
                    yield break;
                }
                yield return null;
            }
        }

        /// <summary>建真 panel 宿主(root 就绪后由调用方建屏 —— 须 await <see cref="WaitForRoot"/>)。</summary>
        private void BuildHost()
        {
            _go = new GameObject("CasebookPointIdentify_UnderTest");
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _doc = _go.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;
        }

        /// <summary>落焦 + 真派发提交事件(引擎语义:提交落在当前焦点元素)。</summary>
        private static void Submit(VisualElement target)
        {
            target.Focus();
            using (var ev = NavigationSubmitEvent.GetPooled(EventModifiers.None))
                target.SendEvent(ev);
        }

        /// <summary>病例流内判断事件(全序;载荷解码)。</summary>
        private List<(EventKind kind, int lexiconId, byte confidence)> Judgments()
        {
            var list = new List<(EventKind, int, byte)>();
            foreach (var e in _bag.Stream.Events)
            {
                if (e.Kind == EventKind.JudgmentRecorded)
                {
                    Assert.IsTrue(PayloadCodec.TryGetPayload(in e, _bag.BlobPool,
                        out JudgmentRecordedPayload p), "Recorded 载荷可解码");
                    list.Add((e.Kind, p.LexiconId, p.Confidence));
                }
                else if (e.Kind == EventKind.JudgmentRevised)
                {
                    Assert.IsTrue(PayloadCodec.TryGetPayload(in e, _bag.BlobPool,
                        out JudgmentRevisedPayload p), "Revised 载荷可解码(F0 编码通道)");
                    list.Add((e.Kind, p.LexiconId, p.Confidence));
                }
            }
            return list;
        }

        // ══════════════════════════════════════════════════════════════════════
        // ① 全链:翻册 → 指认 ⇒ JudgmentRecorded(显式 lexicon_id + 疑似)
        // ══════════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_lexiconBook_pointIdentify_writesJudgmentRecordedWithLexiconId()
        {
            AssembleChain();
            BuildHost();
            yield return WaitForRoot(_doc);
            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            ExaminePatient();                       // 真链:查体 ⇒ 已见集非空

            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            screen.BindCommandSink(_bag.CasebookCommands);
            screen.ApplyData(_bag.CasebookSource);

            IReadOnlyList<int> pointable = _bag.CasebookSource.SeenLexiconIds;
            Assert.Greater(pointable.Count, 0,
                "查体后病名册非空(真链:SkillGrown ⇒ 已见集 ⇒ 词表反查)");
            int expectedLexiconId = pointable[0];

            Assert.AreEqual(0, Judgments().Count, "前置:落笔前病例流零判断事件");

            Submit(root.Q<VisualElement>("channel-disease-name"));   // 摊开
            Assert.IsTrue(screen.IsLexiconBookOpen, "病名栏提交 ⇒ 摊开");

            VisualElement entry = EntryRows(root).First();
            Submit(entry);                          // 指认

            var judgments = Judgments();
            Assert.AreEqual(1, judgments.Count, "指认 ⇒ 病例流恰一条判断事件");
            Assert.AreEqual(EventKind.JudgmentRecorded, judgments[0].kind, "首落笔 = Recorded");
            Assert.AreEqual(expectedLexiconId, judgments[0].lexiconId,
                "载荷 lexiconId = 词表反查出的**显式 id**(非册内显示索引)");
            Assert.AreEqual((byte)JudgmentState.Suspected, judgments[0].confidence,
                "首落 = 疑似(OQ-CB-5 甲)");

            // ② 档位经 DTO 回读(42 不持档位)
            Assert.AreEqual((byte)JudgmentState.Suspected, _bag.CasebookSource.PendingConfidence,
                "档位经 DTO 回读 = 疑似(真源住装配层)");

            // ③ 墨龄 > 0(落笔后从 0 起算)
            _bag.TickDriver.Advance(0.05d);
            Assert.Greater(_bag.CasebookSource.InkAgeTicks, 0L,
                "落笔后墨龄 > 0(AC-42-G6:差值在装配层算,42 零计时器)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ④ 三态循环全链:疑似 → 确定 → 擦除(lexicon_id = 0)
        // ══════════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_lexiconBook_submitCycle_threeStates()
        {
            AssembleChain();
            BuildHost();
            yield return WaitForRoot(_doc);
            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            ExaminePatient();
            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            screen.BindCommandSink(_bag.CasebookCommands);
            screen.ApplyData(_bag.CasebookSource);

            var bar = root.Q<VisualElement>("channel-disease-name");
            Submit(bar);                                            // 摊开
            Submit(EntryRows(root).First());                        // ① 疑似
            Submit(bar);                                            // ② 确定
            Submit(bar);                                            // ③ 擦除

            var judgments = Judgments();
            Assert.AreEqual(3, judgments.Count, "恰三笔(擦除 = 写空判断,非删事件)");
            Assert.AreEqual(EventKind.JudgmentRecorded, judgments[0].kind);
            Assert.AreEqual((byte)JudgmentState.Suspected, judgments[0].confidence, "① 疑似");
            Assert.AreEqual(EventKind.JudgmentRevised, judgments[1].kind);
            Assert.AreEqual((byte)JudgmentState.Confirmed, judgments[1].confidence, "② 确定");
            Assert.AreEqual(EventKind.JudgmentRevised, judgments[2].kind);
            Assert.AreEqual(0, judgments[2].lexiconId, "③ 擦除 lexicon_id = 0(case-system.md:241)");
            Assert.AreEqual((byte)JudgmentState.Empty, judgments[2].confidence, "③ 回空");
            Assert.AreEqual((byte)JudgmentState.Empty, _bag.CasebookSource.PendingConfidence,
                "档位经 DTO 回读 = 空");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑤ 变异敏感:不注入命令面 ⇒ 零判断事件(证伪「恒真空转」)
        // ══════════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_noCommandSink_zeroJudgments_mutationSensitive()
        {
            AssembleChain();
            BuildHost();
            yield return WaitForRoot(_doc);
            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            ExaminePatient();
            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            // ⚠️ 刻意不 BindCommandSink(等价于删掉 BootRoot 第 6.1 步的接线)
            screen.ApplyData(_bag.CasebookSource);

            Submit(root.Q<VisualElement>("channel-disease-name"));
            foreach (var e in EntryRows(root)) Submit(e);

            Assert.AreEqual(0, Judgments().Count,
                "缺写通道 ⇒ 零判断事件(本条与①成对:删接线则①恰红,证①非恒真)");
        }

        // ── helpers ─────────────────────────────────────────────────────────

        /// <summary>病名册条目行(匿名容器内,userData 为 int)。</summary>
        /// <summary>病名册条目行(限「病名栏 anchor 的父容器」子树;userData = 显式 lexicon_id)。</summary>
        /// <remarks>⚠️ 2026-10-13 C3 lead-programmer A2:原为全树 `userData is int` 宽匹配 ——
        /// UI 新增别处 int userData 即假阳收集。生产侧病名册容器保持**匿名**(守护测
        /// `test_bookContainer_isAnonymous_notInNamedSequence` 扫运行期树明断无 name)⇒
        /// 收集方靠 anchor 亲属限定:条目必经「病名栏行 → 父容器(右栏)」内新建容器。</remarks>
        private static List<VisualElement> EntryRows(VisualElement root)
        {
            var rows = new List<VisualElement>();
            VisualElement anchor = root.Q<VisualElement>("channel-disease-name");
            VisualElement scope = anchor?.parent ?? root;   // 右栏子树(条目容器挂在其中)
            Walk(scope);
            return rows;

            void Walk(VisualElement e)
            {
                if (e.userData is int) rows.Add(e);
                foreach (var c in e.Children()) Walk(c);
            }
        }
    }
}
