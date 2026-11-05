// 暂停壳第四行 + 「世界冻结时存档册仍须重探」—— 2026-11-05 两条用户实测缺陷的判据。
//
// 权威:
//   · `production/qa/smoke-2026-11-05.md`(Batch 1 / Batch 2 的用户逐字报告)
//   · ADR-013 §十-C Amendment D(挡路模态摊开 ⇒ 真停 sim tick,2026-11-01)
//   · `design/ux/pause-shell-42.md` §5(行集);本轮用户裁定新增第四行「退出游戏」
//
// ── 本件断的两件事 ─────────────────────────────────────────────
// ① 暂停壳是**四行**,第四行文案 =「退出游戏」,`Activate(3)` 走 `IPauseCommandSink.Quit`;
//    且未注入出口时**不崩**(诚实留空)。
// ② 存档槽位的重探住在 `OnFrameEdge()`(每帧),**不在** `OnTickEdge()`(tick 边沿)——
//    因为存档册是挡路模态,世界在它摊开期间真停 tick ⇒ tick 边沿**永不发生**。
//
// ⚠️ **本件不证**「真启动序里 42 会调 OnFrameEdge」:那在 `BootRoot.Update` 的装配行,
//    与既有 `OnTickEdge` 同款(装配层无 EditMode 夹具的既有边界,如实登记)。
// ⚠️ 本件不碰真磁盘(槽位视图由夹具注入)。

using System;
using System.Collections.Generic;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>暂停壳第四行 + 冻结下仍重探(两条 smoke-2026-11-05 缺陷)。</summary>
    [TestFixture]
    public sealed class PauseShellAndFrozenRefreshTest
    {
        private GameObject _go;
        private SkeuoRuntimeDriver _driver;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("pause-and-refresh-test");
            _driver = _go.AddComponent<SkeuoRuntimeDriver>();
            // EditMode 下 AddComponent 不跑 Awake ⇒ 显式调一次(与 `modal_opener_test` 同法)。
            Invoke(_driver, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        private static void Invoke(object target, string method)
        {
            MethodInfo m = target.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(m, Is.Not.Null, $"夹具前提:{method} 不存在(装配形态变了?)");
            m.Invoke(target, null);
        }

        private sealed class RecordingPauseSink : IPauseCommandSink
        {
            public int ResumeCount;
            public int SaveSlotsCount;
            public int SettingsCount;
            public int QuitCount;
            public int RequestSaveCount;

            /// <summary>夹具可设:落笔是否被门拒。</summary>
            public bool RequestSaveAccepted = true;

            public void Resume() => ResumeCount++;
            public void OpenSaveSlots() => SaveSlotsCount++;
            public void OpenSettings() => SettingsCount++;
            public void Quit() => QuitCount++;
            public bool RequestSave() { RequestSaveCount++; return RequestSaveAccepted; }
        }

        // ══════════════ ① 暂停壳 = 四行 ══════════════

        [Test]
        public void test_pauseShell_hasFiveRows_andLastIsQuit()
        {
            Assert.That(_driver.OpenModal(ModalId.Pause), Is.True, "夹具前提:暂停壳开得出来");
            var pause = _driver.Screen as PauseScreen;
            Assert.That(pause, Is.Not.Null, "夹具前提:打开的是暂停壳");

            // 字面量 4 —— 不与 `RowCount` 自指(自指判据:改实现时两边一起动 = 恒真)。
            Assert.That(pause.RowCount, Is.EqualTo(5),
                "暂停壳必须是五行(继续 / 保存 / 存档位 / 调治 / 退出游戏)。" +
                "2026-11-05 参照英灵神殿第二层增「保存」;同日早些增「退出游戏」。");

            // 末行文案必须真的画出来(首版教训:只挂 `.ruled` 没有 Label ⇒ 行在、玩家看不见)。
            var label = pause.RowAt(4)?.Q<Label>();
            Assert.That(label, Is.Not.Null, "末行必须有 Label(空壳行 = 玩家看不见的行)");
            Assert.That(label.text, Is.EqualTo("退出游戏"), "文案权威 = pause-shell-42.md §5");
        }

        [Test]
        public void test_pauseShell_rowThree_activatesQuit()
        {
            _driver.OpenModal(ModalId.Pause);
            var pause = (PauseScreen)_driver.Screen;
            var sink = new RecordingPauseSink();
            pause.BindCommandSink(sink);

            pause.Activate(4);

            Assert.That(sink.QuitCount, Is.EqualTo(1), "末行必须走 IPauseCommandSink.Quit");
            Assert.That(sink.ResumeCount + sink.SaveSlotsCount + sink.SettingsCount, Is.EqualTo(0),
                "按退出**不得**顺带触发其它三行(行序错位的指纹)");
        }

        [Test]
        public void test_pauseShell_noSink_doesNotThrow()
        {
            _driver.OpenModal(ModalId.Pause);
            var pause = (PauseScreen)_driver.Screen;
            // 未注入出口 = 未接线的诚实形态(行照画、按了留空),不是崩溃。
            Assert.DoesNotThrow(() => pause.Activate(4), "未注入出口时按末行不得抛");
            Assert.DoesNotThrow(() => pause.Activate(1), "未注入出口时按「保存」不得抛");
        }

        [Test]
        public void test_driverPauseSink_quit_delegatesToInjectedAction()
        {
            int quit = 0;
            var sink = new DriverPauseCommandSink(_driver, quit: () => quit++);
            sink.Quit();
            Assert.That(quit, Is.EqualTo(1), "退出动作必须转给装配层注入的委托(42 侧不持 UnityEngine 退出)");

            // null 委托 = 未接线:不崩、不造动作。
            var unbound = new DriverPauseCommandSink(_driver);
            Assert.DoesNotThrow(() => unbound.Quit(), "未注入退出动作时不得抛");
        }

        // ══════════════ ①-b 「保存」行(2026-11-05 参照英灵神殿第二层)══════════════

        [Test]
        public void test_pauseShell_secondRow_isRequestSave_withAgeHint()
        {
            _driver.OpenModal(ModalId.Pause);
            var pause = (PauseScreen)_driver.Screen;

            var label = pause.RowAt(1)?.Q<Label>();
            Assert.That(label, Is.Not.Null, "「保存」行必须有 Label");
            Assert.That(label.text, Is.EqualTo("保存"), "文案权威 = pause-shell-42.md §5");

            var sink = new RecordingPauseSink();
            pause.BindCommandSink(sink);
            pause.Activate(1);

            Assert.That(sink.RequestSaveCount, Is.EqualTo(1), "第二行必须走 IPauseCommandSink.RequestSave");
            Assert.That(sink.ResumeCount + sink.SaveSlotsCount + sink.SettingsCount + sink.QuitCount,
                Is.EqualTo(0), "按「保存」不得顺带触发别的行(行序错位的指纹)");
            Assert.That(pause.SavePending, Is.True, "已受理 ⇒ 置待落定,由驱动在写队列排空后重读年龄");
        }

        [Test]
        public void test_pauseShell_requestSaveRejected_isReportedOnScreen()
        {
            _driver.OpenModal(ModalId.Pause);
            var pause = (PauseScreen)_driver.Screen;
            var sink = new RecordingPauseSink { RequestSaveAccepted = false };
            pause.BindCommandSink(sink);

            pause.Activate(1);

            Assert.That(pause.SavePending, Is.False, "被拒 ⇒ 没有待落定");
            Assert.That(pause.SaveHintText, Does.Contain("未能落笔"),
                "门拒必须**说出来** —— 否则与「按了没反应」不可区分(本轮 smoke 抓的正是这个形态)");
        }

        [Test]
        public void test_pauseShell_saveHint_threeStates()
        {
            _driver.OpenModal(ModalId.Pause);
            var pause = (PauseScreen)_driver.Screen;

            pause.LastSaveAgeMinutes = null;
            pause.RefreshSaveHint();
            Assert.That(pause.SaveHintText, Is.EqualTo("尚未保存"), "来源缺失 ⇒ 如实说不知道,不编");

            pause.LastSaveAgeMinutes = () => 0;
            pause.RefreshSaveHint();
            Assert.That(pause.SaveHintText, Is.EqualTo("刚刚保存"), "0 分钟 ⇒「刚刚」,不写「0 分钟前」");

            pause.LastSaveAgeMinutes = () => 19;
            pause.RefreshSaveHint();
            Assert.That(pause.SaveHintText, Is.EqualTo("上次保存：19 分钟前"),
                "对照英灵神殿「上次保持:19分钟前」的读数形态");
        }

        // ══════════════ ② 冻结下仍重探 ══════════════

        private static SaveSlotView Filled(uint seq)
            => new SaveSlotView(seq, known: true, hasSave: true, tick: 100L);

        private static SaveSlotView Blank(uint seq)
            => new SaveSlotView(seq, known: true, hasSave: false, tick: 0L);

        /// <summary>让驱动按当前 <c>SaveSlotViewProvider</c> 重画一次(等价于开册那一次)。</summary>
        private static void SetRows(SkeuoRuntimeDriver driver)
        {
            MethodInfo m = typeof(SkeuoRuntimeDriver).GetMethod("SetSaveSlotRows",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(m, Is.Not.Null, "夹具前提:SetSaveSlotRows 不存在(重探路径改名了?)");
            m.Invoke(driver, new object[] { null });
        }

        [Test]
        public void test_saveSlotRefresh_happensOnFrameEdge_notOnlyOnTickEdge()
        {
            Assert.That(_driver.OpenModal(ModalId.SaveSlots), Is.True, "夹具前提:存档册开得出来");
            Assert.That(_driver.Screen, Is.TypeOf<SaveSlotsScreen>(), "夹具前提:打开的是存档册");

            // 开册时的第一次探面:三槽全空。
            _driver.SaveSlotViewProvider = () => new[]
            {
                Blank(3), Blank(4), Blank(5),
            };
            _driver.SaveInFlight = () => false;
            SetRows(_driver);
            var book = (SaveSlotsScreen)_driver.Screen;
            Assert.That(book.SlotRowCount, Is.EqualTo(3), "夹具前提:三行");

            // 落笔成功 ⇒ 装配层置脏(生产 = `SaveSlotCommandRouter` 的 onSaveAccepted)。
            _driver.SaveSlotViewProvider = () => new[]
            {
                Filled(3), Blank(4), Blank(5),
            };
            _driver.MarkSaveSlotRowsDirty();

            // 世界冻结 ⇒ tick 边沿**永不发生**。`OnTickEdge` 若消费脏标记,就是把刷新
            // 押在一个不会到来的事件上 —— 那正是用户那次「点了空档还是空的」。
            _driver.OnTickEdge();
            Assert.That(book.RowAt(0).MarkText, Is.Empty,
                "重探**不得**再挂在 OnTickEdge 上(冻结时它永远不跑)");

            _driver.OnFrameEdge();
            Assert.That(book.RowAt(0).MarkText, Is.Not.Empty,
                "每帧边沿必须把已落笔的槽画出来 —— 否则玩家点了空档、盘上写了、屏上还是空的");
        }

        [Test]
        public void test_saveSlotRefresh_waitsWhileWriteInFlight()
        {
            _driver.OpenModal(ModalId.SaveSlots);
            _driver.SaveSlotViewProvider = () => new[]
            {
                Blank(3), Blank(4), Blank(5),
            };
            _driver.SaveInFlight = () => false;
            SetRows(_driver);
            var book = (SaveSlotsScreen)_driver.Screen;

            _driver.SaveSlotViewProvider = () => new[]
            {
                Filled(3), Blank(4), Blank(5),
            };
            _driver.MarkSaveSlotRowsDirty();

            // 写还在队列里 ⇒ 此刻探到的磁盘是**写之前**的,重画会画出「没存上」的假象。
            _driver.SaveInFlight = () => true;
            _driver.OnFrameEdge();
            Assert.That(book.RowAt(0).MarkText, Is.Empty,
                "写未落定时不得重探(否则画出的是写之前的磁盘状态)");

            _driver.SaveInFlight = () => false;
            _driver.OnFrameEdge();
            Assert.That(book.RowAt(0).MarkText, Is.Not.Empty, "写落定后必须补上这次重探");
        }
    }
}
