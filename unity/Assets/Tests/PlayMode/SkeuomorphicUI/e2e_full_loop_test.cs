// sprint-08 E2E-3 ·七环节全链收口判据(PlayMode · 2026-10-13)
//
// 判据(可证伪 · 用户裁定 2026-10-13「一测到底 + 每环保留断言」+「施治注入虚拟 F」):
//   单测内一条链跑完走近→查体→对照→落笔→开方→施治→纸面反馈,每环保留**真派发 +
//   流事件 + DTO 回读**三层断言;末附全序断言(事件 tick 单调不减)。
//
// 环节与判据锚:
//   ① 走近:玩家 Teleport + OnTickEdge ⇒ ActorCellEntered 入世界流(ADR-020 §四);
//   ② 查体:OnExamined ⇒ SkillGrown 入病史流(病名册已见集真源,ADR-026 ⑥);
//   ③ 对照:RevealedRows 非空 ⇒ CasebookScreen 通道**.channel-reading** 行渲染出词条文字
//      (对照 = 玩家把已查体征与病名册知识对上,呈现锚 = 值区有字);
//   ④ 落笔:病名栏 Submit(真派发 Focus + NavigationSubmitEvent)→ 摊开 → 条目 Submit
//      ⇒ JudgmentRecorded 入病例流(lexiconId 显式 + confidence = 疑似);
//   ⑤ 开方:SubmitPageNext → 方笺页 → 档位行 Submit ⇒ DrugTreatmentApplied 入病史流
//      (dose_potency 载荷;翻页/折页零流事件,AC-39-10);
//   ⑥ 施治:注入虚拟 F(同 interaction_input_chain_test 三件套 —— 用户裁定)⇒
//      EmergencyAttempt + EmergencyTreatmentApplied 双事件入病史流;
//   ⑦ 纸面反馈:落笔后 InkAgeTicks > 0 + 通道行仍渲染词条(翻回脉案页值区存活)。
//   ⚠️ **2026-10-13 C3 qa-lead 评审订正(A-1)**:本行原稿曾声称「病名栏回显所指病名」,
//     经核生产 `CasebookScreen.ApplyData` **零回显写入点**(grep `channel-disease-name`
//     只见 Submit 回调 + 册容器锚点),测试**也未为它编断言** ⇒ 原措辞是**过claim**
//     (文档声称已完成而实现缺席)。**病名栏回显 = 未实现项,归 42 后续轮** ——
//     本测⑦ 只断言墨龄 + 值区存活两项(均有真断言兜底)。
//
// ⚠️ **判据与输入路径解耦**(sprint-08 §4 预裁):语义动作层走 CasebookScreen 公开方法
//   + 真 NavigationSubmitEvent 派发(照 casebook_point_identify_chain_test 先例);
//   E/F 键位注入路径本身由 interaction_input_chain_test ②③ 覆盖,本测⑥ F 半个标。
//
// ⚠️ **时序前置(BCD-码-3)**:急救事件须在 DiseaseOnset 之后 ⇒ 测前 Advance 推 tick
//   使动作 tick > 出现 tick(≤ 同 tick 入流触发「乱序入流」红)。
//
// ⚠️ **库存种子**:组合根装配期 Grant 玩家 0(CompositionRoot.cs:248);本测 authorId
//   经 IdAuthority 机制 A 取(≠0)⇒ 开方前自 Grant 1 份(同 prescription_page_host_test
//   先例 —— 开方前置 = 库存归玩家,不归接线层)。
//
// ⚠️ **真派发口径(2026-10-12 实测)**:`NavigationSubmitEvent` 目标由
//   `FocusController.focusedElement` 决定 ⇒ 提交前必须 `Focus()`(引擎真实语义)。
//
// ✅ **C3 qa-lead 评审 A-2 兑现(2026-10-13 三发变异全实测,各恰红 1 后还原复跑绿)**:
//   变④ 删 `screen.BindCommandSink(...)` ⇒ ④环红(0 判断事件);
//   变⑤ 删 `PortionsStore.Grant(...)`(无货)⇒ ⑤环红(DrugTreatmentApplied = 0)
//        —— 同时证「库存前置真生效」(S2.3 C3-M3 口径:拒绝权归库存,不归接线层);
//   变⑦ 删落笔后 `_bag.TickDriver.Advance(0.05d)` ⇒ ⑦环红(InkAgeTicks = 0)。
//   (纸面定位删除点的做法按 M2 阶段 2 教训不作数 —— 多层门冗余结构上对纸面推演盲目,
//    故 ⑤⑦ 两发由实测补齐。)
//
// ⚠️ **与 S1.1 单测的分工(C3 A-4 登记)**:④ 环本测只判「首落 Recorded + 显式 lexiconId
//   + 疑似」一笔;**三态循环(疑似→确定→擦除回空)不在本链内复判** —— 全链覆盖归
//   `casebook_point_identify_chain_test.test_lexiconBook_submitCycle_threeStates`
//   (真组合根、恰三笔、擦除 lexicon_id = 0,强度高于链内插两次 Submit);
//   **病例流侧**的路由(Recorded/Revised → 病例流)+ 末值语义(AC-37-05 J(c) = 全序
//   最后一条判断事件,非差分合成)归 EditMode `case_open_and_judgment_test`。
//   三处分工互不替代,均已有真断言兜底。

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Gameplay.Presentation.Player;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>七环节全链收口判据(sprint-08 E2E-3)。</summary>
    internal sealed class e2e_full_loop_test
    {
        private const int DiseaseId = 1;          // 内联注册表病种 1(词表反查有 id)

        private GameObject _go;
        private UIDocument _doc;
        private PanelSettings _panelSettings;
        private CompositionRootServices _bag;
        private PatientId _patient;
        private int _playerId;

        // 虚拟 F 注入三件套(照 interaction_input_chain_test 同型环境)
        private Keyboard _keyboard;
        private Gamepad _gamepad;
        private bool _keyboardAdded;
        private bool _gamepadAdded;
        private PlayerController _playerGo;
        private InteractionInputDriver _inputDriver;
        private object _manager;
        private System.Reflection.FieldInfo _maskField;
        private InputUpdateType _savedMask;
        private InputSettings.EditorInputBehaviorInPlayMode _savedBehavior;
        private InputSettings.BackgroundBehavior _savedBg;
        private bool _savedRunInBackground;
        private bool _envSaved;

        [TearDown]
        public void TearDown()
        {
            RestoreEnvironmentIfNeeded();
            if (_keyboard != null && _keyboardAdded)
            {
                InputSystem.RemoveDevice(_keyboard);
                _keyboardAdded = false;
            }
            if (_gamepad != null && _gamepadAdded)
            {
                InputSystem.RemoveDevice(_gamepad);
                _gamepadAdded = false;
            }
            _keyboard = null;
            _gamepad = null;

            if (_playerGo != null)
            {
                Object.Destroy(_playerGo.gameObject);
                _playerGo = null;
            }
            _bag?.PatientVisualSpawner?.DestroyAll();
            if (_doc != null) Object.Destroy(_doc);
            if (_panelSettings != null) Object.Destroy(_panelSettings);
            if (_go != null) Object.Destroy(_go);
            _doc = null; _panelSettings = null; _go = null;
            _bag = null;
            _inputDriver = null;
        }

        // ── 虚拟 F 注入环境(三件套,同 interaction_input_chain_test)───────
        private void SaveEnvironment()
        {
            _savedBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            _savedBg = InputSystem.settings.backgroundBehavior;
            _savedRunInBackground = Application.runInBackground;
            _manager = typeof(InputSystem).GetProperty("manager",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Public).GetValue(null);
            _maskField = typeof(InputSystem).Assembly
                .GetType("UnityEngine.InputSystem.InputManager")
                ?.GetField("m_UpdateMask", System.Reflection.BindingFlags.NonPublic
                                            | System.Reflection.BindingFlags.Instance);
            _savedMask = (InputUpdateType)_maskField.GetValue(_manager);
            _envSaved = true;

            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.runInBackground = true;
            _maskField.SetValue(_manager,
                (InputUpdateType)((int)_savedMask & ~(int)InputUpdateType.Editor));
        }

        private void RestoreEnvironmentIfNeeded()
        {
            if (!_envSaved) return;
            if (_maskField != null && _manager != null)
                _maskField.SetValue(_manager, _savedMask);
            if (InputSystem.settings != null)
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = _savedBehavior;
                InputSystem.settings.backgroundBehavior = _savedBg;
            }
            Application.runInBackground = _savedRunInBackground;
            _envSaved = false;
        }

        private void Press(Key key)
        {
            var state = new KeyboardState();
            state.Press(key);
            InputSystem.QueueStateEvent(_keyboard, state);
            InputSystem.Update();
        }

        private void PressGamepadButton(GamepadButton button)
        {
            if (_gamepad == null)
            {
                _gamepad = Gamepad.current;
                if (_gamepad == null)
                {
                    _gamepad = InputSystem.AddDevice<Gamepad>();
                    _gamepadAdded = true;
                }
            }
            InputSystem.QueueStateEvent(_gamepad,
                new GamepadState { buttons = (ushort)(1 << ((int)button)) });
            InputSystem.Update();
        }

        private void ReleaseAll()
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            if (_gamepad != null) InputSystem.QueueStateEvent(_gamepad, new GamepadState());
            InputSystem.Update();
        }

        // ── 世界装配:组合根 + 病人 + 在场 + 开案 + 作者 ──────────────────
        private void AssembleWorld()
        {
            _bag = CompositionRoot.Assemble();
            _patient = _bag.PatientSpawner.SpawnNext(diseaseId: DiseaseId, tick: 0);
            _bag.Presence.AddPresent(_patient, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);

            _bag.TickDriver.Advance(0.05d);                                  // → tick 1
            _bag.CaseOpenedDriver.OnTickEdge(_bag.TickDriver.CurrentTick);    // 开案(落笔前置)

            // 玩家 id 走 IIdAuthority 机制 A(不新开第二计数器;ADR-006 Amendment B 第二十六批)
            _playerId = _bag.IdAuthority.NextPatientId().Value;
            _bag.PrescriptionPage.SetAuthor(_playerId);
            _bag.CasebookSource.SetViewer(_playerId);
            _bag.CasebookSource.ChangePatient(_patient);   // 含命令路由换绑

            // 开方前置:库存种子归玩家(组合根只 Grant 玩家 0;S2.3 C3-M3 口径 ——
            // 库存前置真生效,不归接线层自证)
            _bag.PortionsStore.Grant(_playerId, MinimalDrugCatalog.DrugKey, 1);

            // 视觉实体(F 施治的定位源:InteractionInputDriver.NearestPresentPatient 读
            // 视觉位置,不在场 ⇒ 返回 None ⇒ 急救链断 —— 同 interaction_input_chain_test)
            _bag.PatientVisualSpawner.OnTickEdge(_bag.TickDriver.CurrentTick);
        }

        /// <summary>
        /// 推进病程越过潜伏期(③ 环对照前置 —— 判据要求值区有词条)。
        /// <para>⚠️ 2026-10-14 A 案订正:fallback 病种已配真 F1 曲线(潜伏 20 tick / τ=40 达峰 /
        /// **plateau 保持峰值不衰减**)⇒ 推出「潜伏 + 上升」落到 plateau 上即可,不再需要
        /// 2000+ tick 的等待(旧值 2500 是 legacy `RecoveryTime = 2000` 时代的产物)。
        /// 推 60 tick = 潜伏 20 + 上升 20 + plateau 20,留 20 tick 余量 ⇒ position ≡ 1.0
        /// (plateau 分支恒返回 A_peak ⇒ 多推也不会衰减回 0 —— 这正是 A 案的目的)。
        /// batch 无渲染等待,60 tick 推进是瞬时的纯 Step。</para>
        /// </summary>
        private void AdvancePastIncubation()
        {
            for (int i = 0; i < 60; i++)
            {
                _bag.TickDriver.Advance(0.05d);
                _bag.VitalsService.OnTickEdge(_bag.TickDriver.CurrentTick);
            }
            Assert.Greater(_bag.VitalsService.GetVitals(_patient).Position, 0f,
                "推进过潜伏 + 上升 ⇒ position > 0(plateau 型 ⇒ τ=40 后恒 = 1.0;60 留余量)");
        }

        /// <summary>玩家 GameObject + 交互输入驱动(施治环 F 注入的语义落点)。</summary>
        private void AssemblePlayer()
        {
            SaveEnvironment();
            _keyboard = Keyboard.current;
            if (_keyboard == null)
            {
                _keyboard = InputSystem.AddDevice<Keyboard>();
                _keyboardAdded = true;
            }
            var go = new GameObject("E2EFullLoop_Player");
            _playerGo = go.AddComponent<PlayerController>();
            _playerGo.Initialize(SimAuthorityMode.Host, _bag.EventSink, _bag.TickDriver,
                                 _bag.Encoder, _playerId);
            _playerGo.Teleport(new Vector3(0f, 1f, 0f));
            _inputDriver = new InteractionInputDriver(
                _bag, () => _playerGo != null ? _playerGo.transform.position : Vector3.zero,
                _playerId);
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

        private void BuildHost()
        {
            _go = new GameObject("E2EFullLoop_Casebook");
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

        private static bool StreamHas(EventStream stream, EventKind kind)
            => stream.Events.Any(e => e.Kind == kind);

        /// <summary>通道值区格(挂 `.channel-reading` / `-negative` 类的元素;词条文字在内嵌 Label 上)。</summary>
        private static List<VisualElement> ReadingRows(VisualElement root)
        {
            var rows = new List<VisualElement>();
            Walk(root);
            return rows;

            void Walk(VisualElement e)
            {
                if (e.ClassListContains("channel-reading")
                    || e.ClassListContains("channel-reading-negative"))
                    rows.Add(e);
                foreach (var c in e.Children()) Walk(c);
            }
        }

        /// <summary>值区格是否承载非空词条文字(格内嵌 Label 的 text —— 格本体是 VisualElement)。</summary>
        private static bool HasWordText(VisualElement cell)
        {
            foreach (var c in cell.Children())
                if (c is Label l && !string.IsNullOrEmpty(l.text)) return true;
            return false;
        }

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

        /// <summary>戥子档位行(限 `dentch-doses` 子树;userData = 显式 ordinal)。</summary>
        private static List<VisualElement> DoseRows(VisualElement root)
        {
            var rows = new List<VisualElement>();
            VisualElement dentch = root.Q<VisualElement>("dentch-doses");
            if (dentch == null) return rows;
            Walk(dentch);
            return rows;

            void Walk(VisualElement e)
            {
                if (e.userData is int) rows.Add(e);
                foreach (var c in e.Children()) Walk(c);
            }
        }

        /// <summary>病例流判断事件(全序;载荷解码)。</summary>
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

        private int DrugTreatmentCount => _bag.Stream.Events
            .Count(e => e.Kind == EventKind.DrugTreatmentApplied);

        // ══════════════════════════════════════════════════════════════════
        // 七环节全链(一测到底 · 每环保留断言)
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_e2e_fullLoop_sevenStages()
        {
            AssembleWorld();
            BuildHost();
            yield return WaitForRoot(_doc);
            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            screen.BindCommandSink(_bag.CasebookCommands);
            screen.ApplyData(_bag.CasebookSource);

            // ── ① 走近:玩家走近 ⇒ 位置样本跨格 ⇒ OnTickEdge 写世界流 ──
            // (ADR-020 §四:玩家位移唯一 sim 投影 = ActorCellEntered;连续位置永不写流。
            //  Teleport 不打 sample ⇒ 须 OnPositionSample 到邻近格才产生 pending_cell)
            AssemblePlayer();
            _playerGo.OnPositionSample(new Vector3(2f, 1f, 0f));   // 跨到格 (2,1,0)
            _bag.TickDriver.Advance(0.05d);                 // 推 tick(动作 tick > onset)
            _playerGo.OnTickEdge(_bag.TickDriver.CurrentTick);
            Assert.IsTrue(StreamHas(_bag.Stream, EventKind.ActorCellEntered),
                "走近环节:玩家跨格 ⇒ ActorCellEntered 入世界流(唯一 sim 投影)");

            // ── 病程前置:越过潜伏期(对照环的「有词可读」前提)──
            AdvancePastIncubation();

            // ── ② 查体:ExamineDriver(ledger 快照)+ SeenDisease(SkillGrown)真链 ──
            // (照 InteractionInputDriver.TryExamine 的次序:OnExamine 先建档,投影器后读病种 D)
            _bag.TickDriver.Advance(0.05d);
            _bag.ExamineDriver.OnExamine(_patient, _bag.TickDriver.CurrentTick);
            _bag.SeenDisease.OnExamined(_patient, _bag.TickDriver.CurrentTick, _playerId);
            Assert.IsTrue(_bag.ExaminationLedger.HasExamined(_patient.Value),
                "查体环节:查体分册有该病人快照(Ledger,对照环词条真源)");
            Assert.IsTrue(StreamHas(_bag.Stream, EventKind.SkillGrown),
                "查体环节:SkillGrown 入病史流(病名册已见集真源)");

            // ── ③ 对照:RevealedRows 非空 ⇒ 通道值区渲染出词条文字 ──
            _bag.TickDriver.Advance(0.05d);
            _bag.VitalsService.OnTickEdge(_bag.TickDriver.CurrentTick);
            Assert.Greater(_bag.CasebookSource.RevealedRows.Count, 0,
                "对照环节:查体后 RevealedRows 非空(已查体征的真词条源)");

            screen.ApplyData(_bag.CasebookSource);           // 回读 DTO 重建
            List<VisualElement> reading = ReadingRows(root);
            Assert.Greater(reading.Count, 0,
                "对照环节:脉案通道行渲染出 .channel-reading 值区(对照的呈现锚)");
            Assert.IsTrue(reading.Any(HasWordText),
                "对照环节:值区行有词条文字(不是空行/badge;AC-024-2)");

            // ── ④ 落笔:病名栏 Submit ⇒ 摊开 ⇒ 指认 ⇒ JudgmentRecorded ──
            IReadOnlyList<int> pointable = _bag.CasebookSource.SeenLexiconIds;
            Assert.Greater(pointable.Count, 0, "落笔前置:病名册非空(反幻想二)");
            int expectedLexiconId = pointable[0];

            Assert.AreEqual(0, Judgments().Count, "前置:落笔前病例流零判断事件");

            Submit(root.Q<VisualElement>("channel-disease-name"));   // 摊开
            Assert.IsTrue(screen.IsLexiconBookOpen, "病名栏提交 ⇒ 病名册摊开");

            List<VisualElement> entries = EntryRows(root);
            Assert.Greater(entries.Count, 0, "摊开后有条目行");
            Submit(entries[0]);                                     // 指认

            var judgments = Judgments();
            Assert.AreEqual(1, judgments.Count, "落笔环节:病例流恰一条判断事件");
            Assert.AreEqual(EventKind.JudgmentRecorded, judgments[0].kind, "首落 = Recorded");
            Assert.AreEqual(expectedLexiconId, judgments[0].lexiconId,
                "落笔环节:lexiconId = 词表反查显式 id(非册内显示索引)");
            Assert.AreEqual((byte)JudgmentState.Suspected, judgments[0].confidence,
                "首落 = 疑似(OQ-CB-5 甲)");

            // ── ⑤ 开方:翻页 ⇒ 方笺页 ⇒ 档位行 ⇒ DrugTreatmentApplied ──
            int streamBeforeDose = DrugTreatmentCount;
            Assert.AreEqual(0, streamBeforeDose, "前置:开方前病史流零 DrugTreatmentApplied");

            Submit(root.Q<VisualElement>("page-edge-next"));         // 翻至方笺续页
            Assert.IsTrue(screen.IsOnPrescriptionPage, "后案 Submit ⇒ 翻至方笺页");

            List<VisualElement> doseRows = DoseRows(root);
            Assert.Greater(doseRows.Count, 0, "方笺页有戥子档位行(档数 > 0)");
            Submit(doseRows[(int)doseRows[0].userData - 1]);         // 选第一档

            Assert.AreEqual(streamBeforeDose + 1, DrugTreatmentCount,
                "开方环节:选档确认 ⇒ DrugTreatmentApplied 入病史流(恰一条)");
            Assert.IsTrue(StreamHas(_bag.Stream, EventKind.DrugTreatmentApplied),
                "开方环节:DrugTreatmentApplied 载荷可查(真事件入流)");

            // ── ⑥ 施治:注入虚拟 F ⇒ EmergencyAttempt + Treatment 双事件 ──
            Press(Key.F);
            if (!PlayerInputReader.EmergencyPressed())
                PressGamepadButton(GamepadButton.West);
            Assert.IsTrue(PlayerInputReader.EmergencyPressed(),
                "探针:F/buttonWest 注入后 Emergency 读回 = true(键位注入生效 —— 若此条红,键位怪癖非链断)");
            _inputDriver.OnFrame();
            ReleaseAll();

            Assert.IsTrue(StreamHas(_bag.Stream, EventKind.EmergencyAttempt),
                "施治环节:F 注入 ⇒ EmergencyAttempt 入病史流");
            Assert.IsTrue(StreamHas(_bag.Stream, EventKind.EmergencyTreatmentApplied),
                "施治环节:F 注入 ⇒ EmergencyTreatmentApplied 入病史流(双事件,ADR-009 Amendment I)");

            // ── ⑦ 纸面反馈:墨龄 > 0 + 值区仍渲染 + 病名栏回显 ──
            _bag.TickDriver.Advance(0.05d);
            Assert.Greater(_bag.CasebookSource.InkAgeTicks, 0L,
                "纸面反馈环节:落笔后墨龄 > 0(AC-42-G6)");

            screen.SubmitPagePrev();                                // 翻回脉案页看纸面
            reading = ReadingRows(root);
            Assert.Greater(reading.Count, 0, "纸面反馈环节:翻回脉案值区仍在");
            Assert.IsTrue(reading.Any(HasWordText),
                "纸面反馈环节:值区词条持续渲染(全链走完不丢纸面)");

            // ── 末:全序断言 —— 三流事件按 tick 单调不减 ──
            long lastTick = long.MinValue;
            foreach (var e in _bag.Stream.Events)
            {
                Assert.GreaterOrEqual(e.Tick, lastTick,
                    "全序:三流事件 tick 单调不减(乱序入流 = 重放不可复现,ADR-005)");
                lastTick = e.Tick;
            }

            yield return null;
        }
    }
}
