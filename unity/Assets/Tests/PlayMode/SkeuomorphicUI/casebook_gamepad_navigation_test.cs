// 病名册的**手柄路径**判据(PlayMode · 2026-11-07)
//
// 承「验手柄路径,用之前的虚拟手柄」。
// 前置缺口:`TR-concept-008` / ADR-013 §6.6 —— 拟物 UI **无指针**,脉案 / 病名册的全部交互
// 都须有手柄焦点导航路径。此前 `NOT-RUN` 的正是「病名册这一段能不能用手柄走完」。
//
// ▍为什么用虚拟手柄(而不是直接 `SendEvent`)
//   `casebook_point_identify_chain_test` 的 `Submit(el)` 是 `el.Focus()` + `el.SendEvent(NavigationSubmitEvent)` ——
//   它**绕过了输入层**:既不必经过 EventSystem,也不必经过 `PanelEventHandler`,
//   于是「手柄的 Navigation 动作有没有接到这条路上」完全不在判据面内。
//   本文件的整链是(承 `U1Assump6GamepadChainTest` 已有实证):
//
//     虚拟 Gamepad → InputSystemUIInputModule.Process → PanelEventHandler.OnMove/OnSubmit
//       → panel.SendEvent(NavigationMove/NavigationSubmit) → FocusController(默认动作阶段)
//       → 42 屏的回调
//
//   ⇒ 判据覆盖「手柄输入 → 焦点移动 → 提交 → 落笔」的**整条代码链**。
//
// ▍判据
//   ① 方向键逐行移动、每次按键**恰好一次**焦点移动(无双触发 / 无跳格);
//   ② 从首行(面色)按 Down **能走到**病名行(= 可达性,不是「给它 Focus 之后能用」);
//   ③ 手柄 Submit 落在病名行 ⇒ 病名册摊开,且焦点**落在条目**上;
//   ④ 再按一次 Submit ⇒ 病例流出现 `JudgmentRecorded`,载荷 `lexiconId` = 词表显式 id。
//
// ▍边界(禁借绿,先声明)
//   · ❌ 不是真实手柄硬件(轴死区 / 平台映射不在面内);
//   · ❌ 不是真实显示(仍 `-nographics`,焦点高亮**看得见看不见**不在面内);
//   · ❌ 不是「移动跟不跟手」的主观判断;
//   · 本件只覆盖**脉案 → 病名册 → 指认**这一段手柄路径,
//     **不含**世界空间的走近 / 就诊 / 急救档位。

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Tests.PlayMode;   // PlayModeTempSaveRoot(父命名空间不会自动进作用域)
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using InputApi = UnityEngine.InputSystem.InputSystem;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>病名册的手柄焦点导航判据(虚拟手柄 → 生产 EventSystem 装配)。</summary>
    [TestFixture]
    internal sealed class casebook_gamepad_navigation_test
    {
        private const int PlayerId = 9001;

        /// <summary>真注册表 `INJ_STAB` 刺伤 = 词表第 4 行(生产默认病人的病种)。</summary>
        private const int DiseaseId = 2;

        /// <summary>夹具给的显式行高(理由见 <see cref="ChannelRowHeightNote"/>)。</summary>
        private const float ChannelRowHeight = 80f;

        /// <summary>行高为什么由夹具给:42 的自动高度在夹具环境下算成 0(实测 1356×0)。</summary>
        private const string ChannelRowHeightNote = "夹具显式行高,不改相邻关系";

        private const float RootWaitTimeoutSeconds = 2f;
        private const float CarrierWaitTimeoutSeconds = 3f;

        /// <summary>脉案六行 + 病名行的焦点序(与 42 树序一致)。</summary>
        private static readonly string[] ChannelOrder =
        {
            "channel-complexion", "channel-voice", "channel-posture",
            "channel-breathing", "channel-palpation", "channel-inquiry",
            "channel-disease-name",
        };

        private GameObject _go;
        private UIDocument _doc;
        private PanelSettings _panelSettings;
        private GameObject _bootGo;
        private EventSystem _es;
        private InputSystemUIInputModule _module;
        private PanelEventHandler _handler;
        private Gamepad _pad;

        private CompositionRootServices _bag;
        private PatientId _patient;
        private VisualElement _root;
        /// <summary>显式尺寸的宿主容器(屏建在它里面;见 <see cref="WaitForRoot"/> 的尺寸注)。</summary>
        private VisualElement _host;
        private CasebookScreen _screen;

        private int _navEvents;

        [SetUp]
        public void SetUp()
        {
            _navEvents = 0;
            _go = new GameObject("CasebookGamepad_UnderTest");
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _doc = _go.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 虚拟手柄自造自清(承 device_and_presentation_test 纪律)
            if (_pad != null && _pad.deviceId != InputDevice.InvalidDeviceId &&
                InputApi.GetDevice<Gamepad>() != null)
                InputApi.RemoveDevice(_pad);
            _pad = null;

            if (_root != null)
                _root.UnregisterCallback<NavigationMoveEvent>(OnNavMove, TrickleDown.TrickleDown);
            _root = null;
            _handler = null;
            _module = null;
            _es = null;

            if (_doc != null) Object.Destroy(_doc);
            if (_panelSettings != null) Object.Destroy(_panelSettings);
            if (_go != null) Object.Destroy(_go);
            if (_bootGo != null) Object.Destroy(_bootGo);
            _doc = null; _panelSettings = null; _go = null; _bootGo = null;
            _bag = null; _screen = null;
            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 主判据:一条测到底(可达性 → 摊开 → 落焦 → 指认)
        // ══════════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_gamepad_dpadReachesDiseaseName_submitOpensBook_andIdentifyRecordsJudgment()
        {
            AssembleCasebookChain();
            yield return WaitForRoot(_doc);
            Assert.IsNotNull(_root, "[夹具红] root 未就绪");
            Assert.IsNotNull(_root.panel, "[夹具红] panel 为 null");

            _screen = new CasebookScreen(_host, new SkeuoElementLibrary());
            _screen.BindCommandSink(_bag.CasebookCommands);
            _screen.ApplyData(_bag.CasebookSource);

            IReadOnlyList<int> pointable = _bag.CasebookSource.SeenLexiconIds;
            Assert.Greater(pointable.Count, 0,
                "前置:查体后病名册非空(真链 SkillGrown ⇒ 已见集 ⇒ 词表反查)");
            int expectedLexiconId = pointable[0];
            Assert.AreEqual(0, Judgments().Count, "前置:落笔前病例流零判断事件");

            yield return AssembleProductionEventSystem();

            // 起点 = 模态摊开时 42 给的初焦(首个通道行)
            // ⚠️ 夹具措施(**不是**产品改动):42 的通道行在夹具环境下自动高度算成 **0**
            //    (实测 `worldBound = 1356×0`,带不带图形一样),而几何导航需要真实高差。
            //    七行给**显式行高** —— 只让高度"可量",不改**相邻关系**(仍是竖列)、
            //    不改 focusable / 回调 / 数据。既有两条手柄导航夹具(u1 / pause)同样用显式尺寸。
            for (int i = 0; i < ChannelOrder.Length; i++)
            {
                VisualElement row = _host.Q<VisualElement>(ChannelOrder[i]);
                Assert.IsNotNull(row, $"[夹具红] 行 {ChannelOrder[i]} 不在树上");
                row.style.height = ChannelRowHeight;
                row.style.flexGrow = 0f;
            }
            yield return null;
            yield return null;

            VisualElement first = _root.Q<VisualElement>(ChannelOrder[0]);
            Assert.IsNotNull(first, "[夹具红] 首个通道行不在树上 —— 42 树序变了");
            first.Focus();
            yield return null;
            Assert.AreEqual(ChannelOrder[0], CurrentFocusName(), "[夹具红] 起点焦点未落在首行");

            // 夹具红闸:几何未计算时手柄判据不可测(承 `U1Assump6GamepadChainTest` 同纪律)
            Assert.Greater(first.worldBound.height, 0f,
                $"[夹具红] 通道行高为 0({first.worldBound})—— 布局未计算,几何序判据不可测;不得改判据迁就实现。");

            // ── ① 左列竖走:面色 → 语声 → 姿态 → 呼吸 → 触感 → 问诊(5 次 Down)──
            var trail = new List<string> { CurrentFocusName() };
            for (int i = 1; i <= 5; i++)
            {
                _navEvents = 0;
                yield return PressStickDown();
                trail.Add(CurrentFocusName());

                Assert.AreEqual(1, _navEvents,
                    $"第 {i} 次 Down 应产生**恰好一次**焦点移动(双触发会让手柄玩家跳行)");
                Assert.AreEqual(ChannelOrder[i], CurrentFocusName(),
                    $"第 {i} 次 Down 应落到 {ChannelOrder[i]};轨迹={string.Join("→", trail)}");
            }

            // ── ② 回到列首(5 次 Up)—— 同样是逐行、无双触发 ──
            for (int i = 4; i >= 0; i--)
            {
                _navEvents = 0;
                yield return PressStickUp();
                trail.Add(CurrentFocusName());
                Assert.AreEqual(1, _navEvents, "Up 应产生恰好一次焦点移动");
                Assert.AreEqual(ChannelOrder[i], CurrentFocusName(),
                    $"Up 应逐行上移;轨迹={string.Join("→", trail)}");
            }

            // ── ③ 横走到病名行 ──
            // ⚠️ **几何事实**(本轮实测):右栏只有两行(病名 / 折),而左栏有六行 ⇒
            //    只有与右栏**竖直波段重叠**的左栏行(面色 / 语声)能右走成功;
            //    从问诊(第 6 行)按 Right **无候选、焦点不动**(实测)。
            //    ⇒ 手柄玩家的实际路径 = 在列首右走一次,不是「一路 Down 再右」。
            _navEvents = 0;
            yield return PressStickRight();
            trail.Add(CurrentFocusName());
            Assert.AreEqual(1, _navEvents, "Right 应产生恰好一次焦点移动");
            Assert.AreEqual("channel-disease-name", CurrentFocusName(),
                "列首右走应落到病名行;轨迹=" + string.Join("→", trail));

            // ── ③ 手柄 Submit ⇒ 摊开 + 焦点落条目 ──
            yield return PressSouth();
            Assert.IsTrue(_screen.IsLexiconBookOpen, "手柄 Submit 落在病名行 ⇒ 病名册摊开");

            var openedEntries = EntryRows(_host);
            VisualElement focused = CurrentFocus();
            Assert.IsNotNull(focused,
                $"[手柄断点] 摊开后焦点为空;条目数={openedEntries.Count}," +
                $" SeenLexiconIds={_bag.CasebookSource.SeenLexiconIds.Count}" +
                $" bookOpen={_screen.IsLexiconBookOpen}");
            Assert.IsTrue(focused.userData is int,
                $"摊开后焦点应落在**条目行**上(实得 {CurrentFocusName()})—— " +
                "否则手柄玩家到此为止(键鼠还能点,手柄不能)");

            // ── ④ 再按一次 Submit ⇒ 指认落笔 ──
            yield return PressSouth();
            var judgments = Judgments();
            Assert.AreEqual(1, judgments.Count, "手柄指认 ⇒ 病例流恰一条判断事件");
            Assert.AreEqual(EventKind.JudgmentRecorded, judgments[0].kind, "首落笔 = Recorded");
            Assert.AreEqual(expectedLexiconId, judgments[0].lexiconId,
                "载荷 lexiconId = 词表反查出的**显式 id**(不是册内显示序)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // 装配:真组合根 + 真注册表 + 真默认病人
        // ══════════════════════════════════════════════════════════════════════
        private void AssembleCasebookChain()
        {
            // 真注册表 ⇒ 病种 2 = INJ_STAB 刺伤(生产默认病人的病种,词表第 4 行有外键)。
            _bag = CompositionRoot.Assemble(
                registry: DaYiJingCheng.Gameplay.Presentation.DataCookedDisk.LoadDiseaseRegistry().Rows);
            _patient = _bag.PatientSpawner.SpawnNext(diseaseId: DiseaseId, tick: 0);
            _bag.Presence.AddPresent(_patient, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);
            _bag.TickDriver.Advance(0.05d);
            _bag.CaseOpenedDriver.TryOpen(_patient, _bag.TickDriver.CurrentTick);

            _bag.PrescriptionPage.SetAuthor(PlayerId);
            _bag.CasebookSource.SetViewer(PlayerId);
            _bag.CasebookSource.ChangePatient(_patient);

            // 查体(走真链:SkillGrown ⇒ NoveltyTracker ⇒ 词表反查)
            _bag.TickDriver.Advance(0.05d);
            _bag.SeenDisease.OnExamined(_patient, _bag.TickDriver.CurrentTick, PlayerId);
        }

        private IEnumerator WaitForRoot(UIDocument doc)
        {
            float t0 = Time.realtimeSinceStartup;
            while (doc.rootVisualElement == null)
            {
                if (Time.realtimeSinceStartup - t0 > RootWaitTimeoutSeconds)
                {
                    Assert.Fail("[夹具红] UIDocument.rootVisualElement 2s 未就绪(panelSettings 未生效)。");
                    yield break;
                }
                yield return null;
            }
            _root = doc.rootVisualElement;

            // ⚠️ 面板必须**有真实尺寸**,否则行高恒 0、几何邻居找不到 ⇒ 手柄「移动」看起来像无候选。
            //    `-nographics` 批处理下运行时面板的高度算成 **0**(实测 `root bound = 640×0`),
            //    而 UIDocument 会每帧把根的尺寸刷回面板尺寸 ⇒ **在根上设 style 会被覆盖**
            //    (实测设 1600×900 后宽度生效 1356、高度仍 0)。
            //    ⇒ 插一层**显式尺寸**的宿主容器,屏建在它里面。生产里这块就是全屏面板,
            //      不是夹具造出来的能力。
            _host = new VisualElement { name = "test-host" };
            _host.style.width = 1600f;
            _host.style.height = 900f;
            _host.style.flexGrow = 0f;
            _host.style.flexShrink = 0f;
            _root.Add(_host);

            yield return null;
            yield return null;   // 让 layout 算出 worldBound(几何序依赖它)
        }

        /// <summary>生产同款 EventSystem 装配(承 `U1Assump6GamepadChainTest` 的既有实证形态)。</summary>
        private IEnumerator AssembleProductionEventSystem()
        {
            _root.RegisterCallback<NavigationMoveEvent>(OnNavMove, TrickleDown.TrickleDown);
            yield return null;

            _bootGo = new GameObject("BootRoot_CasebookGamepad");
            var boot = _bootGo.AddComponent<BootRoot>();
            boot.SaveRootOverride = PlayModeTempSaveRoot.New("casebook_gamepad_navigation_test");
            boot.enabled = false;
            _es = InvokePrivate(boot, "EnsureEventSystem") as EventSystem;
            Assert.IsNotNull(_es, "[夹具红] EnsureEventSystem 返回 null");

            GameObject carrier = null;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < CarrierWaitTimeoutSeconds)
            {
                carrier = (_root.panel as IRuntimePanel)?.selectableGameObject;
                if (carrier != null) break;
                yield return null;
            }
            Assert.IsNotNull(carrier,
                "[夹具红] 面板载体 selectableGameObject 3s 未就绪 —— interop bridge 未建面板 GO。");
            _handler = carrier.GetComponent<PanelEventHandler>();
            Assert.IsNotNull(_handler,
                "[夹具红] 面板载体上没有 PanelEventHandler —— 手柄导航事件无处落地。");

            _es.SetSelectedGameObject(carrier);
            _module = _es.currentInputModule as InputSystemUIInputModule;
            Assert.IsNotNull(_module,
                "[夹具红] currentInputModule 不是 InputSystemUIInputModule —— EventSystem 未 tick 过。");
            yield return null;

            _pad = InputApi.AddDevice<Gamepad>();
            Assert.IsNotNull(_pad, "[夹具红] 虚拟 Gamepad 未建成。");
            yield return null;
            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 注入
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>左摇杆向下 —— 状态事件全量替换,故松开必须显式归零。</summary>
        private IEnumerator PressStickDown() => PressStick(new Vector2(0f, -1f));

        /// <summary>左摇杆向上。</summary>
        private IEnumerator PressStickUp() => PressStick(new Vector2(0f, 1f));

        /// <summary>左摇杆向右。</summary>
        private IEnumerator PressStickRight() => PressStick(new Vector2(1f, 0f));

        private IEnumerator PressStick(Vector2 v)
        {
            InputApi.QueueStateEvent(_pad, new GamepadState { leftStick = v });
            yield return null;
            yield return null;   // 首帧处理输入 + 派发,次帧让焦点稳定

            InputApi.QueueStateEvent(_pad, new GamepadState { leftStick = Vector2.zero });
            yield return null;
            yield return null;
        }

        /// <summary>面按钮南 = 提交(UI/Submit 的手柄绑定)。
        /// <para>按下/松开 = 全量状态事件:置位写 one-hot 位,松开写**空状态**。
        /// 形态照抄既有用法(`pause_focus_navigation_test` / `main_menu_wiring_test`),
        /// 不另造一套按钮注入。</para></summary>
        private IEnumerator PressSouth() => PressButton(GamepadButton.South);

        private IEnumerator PressButton(GamepadButton button)
        {
            InputApi.QueueStateEvent(_pad, new GamepadState { buttons = (ushort)(1 << (int)button) });
            yield return null;
            yield return null;

            InputApi.QueueStateEvent(_pad, new GamepadState());
            yield return null;
            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 读面
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>病名册条目行(userData 是 lexicon_id 的整数)。</summary>
        private static List<VisualElement> EntryRows(VisualElement scope)
        {
            var rows = new List<VisualElement>();
            Walk(scope);
            return rows;

            void Walk(VisualElement e)
            {
                if (e.userData is int) rows.Add(e);
                foreach (var c in e.Children()) Walk(c);
            }
        }

        private VisualElement CurrentFocus()
            => _root?.panel?.focusController?.focusedElement as VisualElement;

        private string CurrentFocusName()
        {
            VisualElement fe = CurrentFocus();
            if (fe == null) return "<none>";
            return string.IsNullOrEmpty(fe.name) ? "<unnamed>" : fe.name;
        }

        private void OnNavMove(NavigationMoveEvent e) => _navEvents++;

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
                        out JudgmentRevisedPayload p), "Revised 载荷可解码");
                    list.Add((e.Kind, p.LexiconId, p.Confidence));
                }
            }
            return list;
        }

        private static object InvokePrivate(object target, string method)
        {
            var mi = target.GetType().GetMethod(method,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(mi, $"[夹具红] {target.GetType().Name}.{method} 未找到(私有方法名漂移)");
            return mi.Invoke(target, null);
        }
    }
}
