// 模态开启器(2026-10-31)—— 42 侧运行期换屏的判据。
//
// 权威:ADR-013 §十(`ModalId` 闭集 + `IModalState`;42 是模态真源)·
//       design/ux/save-slots-7b.md:189(正式入口 = 暂停壳)
// 🔁 **2026-10-31 晚(ADR-013 §十-C Amendment C)**:暂停壳入闭集 ⇒ 第七节新增五条判据,
//    开合对象由存档位改为暂停壳。条文见各节头注,原「临时入口」口径已作废。
//
// ── 本件钉的四件事 ─────────────────────────────────────────────
// ① 开 → 真的有那个屏 + `CurrentModal` 如实;② 关 → 屏没了、`CurrentModal = None`;
// ③ 换屏 → 同一时刻只有一个容器(焦点单栈的结构前提);④ 键沿 → 一次按下恰好一次动作
//    (持键不重复触发)。
//
// ⚠️ **本件不判**「屏画得对不对」「焦点落对了没」「输入冻结生效没」——
//    那些要真 panel + 焦点桥 + 交互门,分别归 AC-42-F1 走查与 4 的门接线(今日均 NOT-RUN)。

using System;
using System.Collections.Generic;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Interaction;      // ModalGate / IArmedState / InteractIntent
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>模态开启器:开 / 关 / 换屏 + 键沿驱动。</summary>
    [TestFixture]
    public sealed class ModalOpenerTest
    {
        private GameObject _go;
        private SkeuoRuntimeDriver _driver;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("modal-opener-test");
            _driver = _go.AddComponent<SkeuoRuntimeDriver>();
            // EditMode 下 AddComponent 不跑 Awake ⇒ 显式调一次(它只做 EnsureDocument + 建屏)。
            Invoke(_driver, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        /// <summary>写私有序列化字段(构造不同启动形态的驱动用)。</summary>
        private static void SetPrivateField(object target, string field, object value)
        {
            var f = target.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(f, Is.Not.Null, $"夹具前提:{field} 不存在(驱动形态变了?)");
            f.SetValue(target, value);
        }

        private static void Invoke(object target, string method)
        {
            MethodInfo m = target.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(m, Is.Not.Null, $"夹具前提:{method} 不存在(装配形态变了?)");
            m.Invoke(target, null);
        }

        // ══════════════ ① 开 ══════════════

        [Test]
        public void test_openSaveSlots_createsScreenAndReportsCurrentModal()
        {
            // 🔁 2026-11-02:默认屏由 `"Casebook"` 改为**空**(无常驻屏)—— 依据
            // `design/ux/casebook-39.md` §入口「立案即开册」:脉案是按需打开的模态,
            // 不该在启动时摊着。本条前提随之由 Casebook 改为 None。
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.None),
                "夹具前提:启动无常驻屏(`_screenName` 默认为空)");
            Assert.That(_driver.Screen, Is.Null, "夹具前提:启动不建屏");

            bool opened = _driver.OpenModal(ModalId.SaveSlots);

            Assert.That(opened, Is.True, "闭集内的模态必须能开");
            Assert.That(_driver.Screen, Is.TypeOf<SaveSlotsScreen>(), "打开的必须是存档位屏");
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.SaveSlots), "当前模态须如实");
            Assert.That(_driver.IsModalOpen, Is.True);
        }

        [Test]
        public void test_openModal_noneOrUnknown_returnsFalseWithoutThrowing()
        {
            Assert.That(_driver.OpenModal(ModalId.None), Is.False, "None 不是可开的模态");

            // 闭集外的 id:不抛,但**留 LogError**(「拒绝静默无操作」)—— 故先声明期望。
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("没有对应的屏"));
            Assert.IsFalse(_driver.OpenModal((ModalId)99),
                "闭集外的 id 不得抛(它可能来自旧数据);但也不得静默当成开了");
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.None), "失败的开关不得改变当前模态");
        }

        // ══════════════ ② 关 ══════════════

        [Test]
        public void test_closeModal_clearsScreenAndCurrentModal()
        {
            _driver.OpenModal(ModalId.SaveSlots);

            _driver.CloseModal();

            Assert.That(_driver.Screen, Is.Null, "关掉后不得留着屏引用");
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.None));
            Assert.That(_driver.IsModalOpen, Is.False);
        }

        [Test]
        public void test_closeModal_whenNothingOpen_isNoOp()
        {
            _driver.CloseModal();
            Assert.DoesNotThrow(() => _driver.CloseModal(), "重复关 = 空操作,不崩");
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.None));
        }

        // ══════════════ ③ 换屏:同一时刻只有一个容器 ══════════════

        [Test]
        public void test_switchingModal_leavesExactlyOneModalContainer()
        {
            _driver.OpenModal(ModalId.InventoryContainer);
            _driver.OpenModal(ModalId.SaveSlots);

            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.SaveSlots));
            Assert.That(_driver.Screen, Is.TypeOf<SaveSlotsScreen>());

            int containers = 0;
            foreach (VisualElement child in _driver.GetUiRoot().Children())
                if (child.name != null && child.name.StartsWith("modal-")) containers++;

            Assert.That(containers, Is.EqualTo(1),
                "换屏后只允许剩一个模态容器 —— 多一个 = 两个屏同时摊开,「焦点单栈」的结构前提破了");
        }

        // ══════════════ ④ 键沿:一次按下恰好一次动作 ══════════════

        [Test]
        public void test_toggleDriver_pressOpens_thenPressCloses_holdingDoesNotRepeat()
        {
            var opener = new FakeOpener();
            bool held = false;
            var driver = new ModalInputDriver(() => held, opener);

            held = true;
            driver.OnFrame();
            Assert.That(opener.OpenCalls, Is.EqualTo(1), "第一次按下 ⇒ 开");
            Assert.That(opener.CloseCalls, Is.EqualTo(0));

            driver.OnFrame();                       // 持键
            driver.OnFrame();
            Assert.That(opener.OpenCalls, Is.EqualTo(1), "持键不得重复触发(边沿化)");

            held = false;
            driver.OnFrame();                       // 松开
            Assert.That(opener.CloseCalls, Is.EqualTo(0), "松开沿不是动作沿");

            held = true;
            driver.OnFrame();
            Assert.That(opener.CloseCalls, Is.EqualTo(1), "再按一次 ⇒ 关(开合同键)");
            Assert.That(driver.CloseCount, Is.EqualTo(1));
        }

        [Test]
        public void test_toggleDriver_nullDependencies_throwAtConstruction()
        {
            Assert.Throws<ArgumentNullException>(() => new ModalInputDriver(null, new FakeOpener()));
            Assert.Throws<ArgumentNullException>(() => new ModalInputDriver(() => false, null));
        }

        // ══════════════ ⑤ 焦点可达(游戏没有指针,焦点就是入口)══════════════

        [Test]
        public void test_saveSlotRows_areFocusable_inRowOrder()
        {
            _driver.OpenModal(ModalId.SaveSlots);
            var screen = (SaveSlotsScreen)_driver.Screen;
            screen.SetSlots(new uint[] { 1u, 2u, 3u });

            for (int i = 0; i < screen.SlotRowCount; i++)
            {
                SaveSlotItem row = screen.RowAt(i);
                Assert.That(row.focusable, Is.True,
                    $"第 {i} 行不可获焦 —— 手柄玩家按不动它(焦点桥走不到)");
                Assert.That(row.tabIndex, Is.EqualTo(i),
                    "焦点顺序必须 = 行序(AC-7b-04:slot_seq 升序,不可重排)");
            }

            Assert.That(screen.FirstFocusable, Is.SameAs(screen.RowAt(0)),
                "开屏后要落焦的那一行 = 第一行");
        }

        [Test]
        public void test_saveSlotRows_haveRowHeightRule_inUss()
        {
            // ⚠️ 只读外部夹具的具名声明:读 `SkeuoPaper.uss`(拟物样式表)**必须**读真表而非内联 ——
            //    本判据要证的就是「真样式表里有这条规则」,内联一份等于自证自。
            //    只读、零写入(不违 test-standards 对可变外部状态的那一条)。
            string uss = System.IO.File.ReadAllText(
                System.IO.Path.Combine(UnityEngine.Application.dataPath,
                    "Gameplay.UI", "Skeuomorphic", "SkeuoPaper.uss"));

            // ⚠️ 判据必须落在**选择器行**上,不能全文搜串 —— 注释里出现同名类
            //    会顶替真规则(证伪轮实测:变异注释里带了类名 ⇒ 全文搜仍命中 ⇒ 假绿)。
            var selector = new System.Text.RegularExpressions.Regex(
                @"^\s*\.save-slot-item\s*[,{]", System.Text.RegularExpressions.RegexOptions.Multiline);
            System.Text.RegularExpressions.Match m = selector.Match(uss);
            Assert.That(m.Success, Is.True,
                "样式表里没有 `.save-slot-item` 的**选择器行** —— 有字行零高度 ⇒ 点不中也获不了焦的落点");

            int ruleAt = m.Index;
            int blockEnd = uss.IndexOf('}', ruleAt);
            string block = uss.Substring(ruleAt, blockEnd - ruleAt);
            Assert.That(block, Does.Contain("min-height"),
                "有字行必须与空行同 min-height(AB-3 的 44 下限)—— 否则行塌陷、点不中");
        }

        [Test]
        public void test_openSaveSlots_withoutPanel_doesNotThrow()
        {
            // EditMode 下 rootVisualElement 无 panel(合法形态)⇒ 落焦要跳过而不是抛。
            Assert.DoesNotThrow(() => _driver.OpenModal(ModalId.SaveSlots));
            var screen = (SaveSlotsScreen)_driver.Screen;
            screen.SetSlots(new uint[] { 1u });
            Assert.That(screen.FirstFocusable, Is.Not.Null, "行在,焦点面就该存在");
        }

        // ══════════════ ⑥ 规则七方向②:模态门接线(2026-10-31)══════════════

        [Test]
        public void test_modalGateState_restingScreen_doesNotBlockPlay()
        {
            // 常驻屏(启动即建的屏)是「有屏」但**不是**「玩家摊开的模态」—— 若把它算作
            // 模态开,规则七的门会常闭,E 查体当场失效。
            //
            // 🔁 2026-11-02:默认启动形态已改为**无常驻屏**(`_screenName` 默认空,依据
            // `casebook-39.md §入口`)。但「常驻屏」这条**代码路径仍在**(配置了非空屏名就走),
            // 属性也必须继续被守 ⇒ 本条改为**显式构造**一个带常驻屏的驱动,
            // 而不是依赖默认形态。删掉本条会留下一个无人守的分支。
            var go = new GameObject("modal-opener-test-resting");
            var driver = go.AddComponent<SkeuoRuntimeDriver>();
            SetPrivateField(driver, "_screenName", "Casebook");
            Invoke(driver, "Awake");

            try
            {
                var adapter = new ModalGateStateAdapter(driver);

                Assert.That(driver.IsModalOpen, Is.True,
                    "前提:常驻屏在(`IsModalOpen` 答「有没有屏」)");
                Assert.That(driver.CurrentModal, Is.EqualTo(ModalId.Casebook),
                    "前提:常驻屏真的是 Casebook");
                Assert.That(adapter.IsOpen, Is.False,
                    "常驻屏不得让闸门常闭 —— 否则启动即不能查体");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void test_modalGateState_openedModal_blocksPlay_andCloseRestores()
        {
            var adapter = new ModalGateStateAdapter(_driver);

            _driver.OpenModal(ModalId.SaveSlots);
            Assert.That(adapter.IsOpen, Is.True, "玩家摊开的模态必须挡路");

            _driver.CloseModal();
            Assert.That(adapter.IsOpen, Is.False, "关掉后立刻恢复");
        }

        [Test]
        public void test_modalGate_rejectsInteraction_onlyWhileModalOpenOrArmed()
        {
            var armed = new FakeArmed();
            var gate = new ModalGate(armed, new ModalGateStateAdapter(_driver));

            Assert.That(gate.Accept(InteractIntent.None), Is.True, "常驻屏 + 未 Armed ⇒ 放行(E 查体可用)");

            _driver.OpenModal(ModalId.SaveSlots);
            Assert.That(gate.Accept(InteractIntent.None), Is.False, "模态开 ⇒ 丢弃(F-4.2)");

            _driver.CloseModal();
            armed.IsArmed = true;
            Assert.That(gate.Accept(InteractIntent.None), Is.False, "Armed ⇒ 丢弃(方向①)");
        }

        [Test]
        public void test_modalGateState_nullProvider_doesNotBlock()
        {
            var adapter = new ModalGateStateAdapter((SkeuoRuntimeDriver)null);
            Assert.That(adapter.IsOpen, Is.False, "无 UI ⇒ 无模态(诚实降级,不是假装开着)");
        }

        // ══════════════ ⑦ 暂停壳:存档位的正式入口(2026-10-31 Amendment C)══════════════

        [Test]
        public void test_openPause_createsPauseScreen_andBlocksPlay()
        {
            Assert.IsTrue(_driver.OpenModal(ModalId.Pause), "闭集第八员必须能开");

            Assert.That(_driver.Screen, Is.TypeOf<PauseScreen>(), "打开的必须是暂停壳");
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.Pause));
            Assert.That(_driver.BlocksPlay, Is.True,
                "暂停壳必须挡路 —— 它存在的意义就是冻住世界意图(规则七方向②)");
        }

        [Test]
        public void test_pauseSaveSlotsRow_reachesSaveSlotsScreen_throughAssemblySink()
        {
            // 本件是**整条入口链的合取**:暂停壳的行 → 装配层 sink → 42 换屏。
            // 缺任一环,7b 就又回到「无正式入口」。
            _driver.PauseSink = new DriverPauseCommandSink(_driver);
            _driver.OpenModal(ModalId.Pause);
            var pause = (PauseScreen)_driver.Screen;

            Assert.That(pause.RowCount, Is.EqualTo(5),
                "壳 = 继续 / 保存 / 存档位 / 调治 / 退出游戏 五行" +
                "(2026-11-05 参照英灵神殿第二层增「保存」;同日早些增第四行,2026-11-04 增第三行)");

            pause.Activate(2);   // 「存档位」(1 = 保存,2026-11-05 增)

            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.SaveSlots),
                "暂停壳的「存档位」行必须真的换到 7b 的屏 —— 否则正式入口是假的");
            Assert.That(_driver.Screen, Is.TypeOf<SaveSlotsScreen>());
        }

        [Test]
        public void test_pauseSettingsRow_reachesSettingsShell_throughAssemblySink()
        {
            // 🔁 2026-11-04(TD-skeuo-04):`ModalId.SettingsShell` 此前**全仓零开屏点** ——
            //    屏载体与目录登记都在,玩家却永远打不开。本件是整条入口链的合取:
            //    暂停壳第三行 → 装配层 sink → 42 换屏。
            _driver.PauseSink = new DriverPauseCommandSink(_driver);
            _driver.OpenModal(ModalId.Pause);
            var pause = (PauseScreen)_driver.Screen;

            pause.Activate(3);   // 「调治」(2026-11-05 起行序后移一位)

            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.SettingsShell),
                "第三行必须真的换到设置壳 —— 否则设置壳仍是零开屏点");
            Assert.That(_driver.Screen, Is.TypeOf<SettingsScreen>(),
                "换到的必须是 SettingsScreen 屏载体");
        }

        [Test]
        public void test_pauseSettingsRow_isTheOnlyProductionEntry()
        {
            // ⚠️ 本件是**副作用判据**,不是「屏能开」的重复断言:
            //    它断的是「设置壳的开屏点**只在暂停壳第三行**这一处」。
            //    主菜单被 main-menu-42.md:85-86 明确排除(不提供设置),故不得经主菜单开。
            _driver.PauseSink = new DriverPauseCommandSink(_driver);
            _driver.OpenModal(ModalId.Pause);
            var pause = (PauseScreen)_driver.Screen;

            // 🔁 2026-11-05:行序后移一位(第二行插了「保存」)⇒ 「调治」由 2 → 3。
            Assert.That(pause.RowAt(3), Is.Not.Null, "「调治」行必须存在");
            Assert.That(pause.RowAt(3).name, Is.EqualTo("pause-settings"),
                "该行的元件名是夹具定位锚点 —— 改名会让真挂载面与夹具脱钩");

            var label = pause.RowAt(3).Q<Label>();
            Assert.That(label, Is.Not.Null, "行必须有可见文字(空行 = 可点但玩家看不见,2026-10-31 实拍缺陷)");
            Assert.That(label.text, Is.EqualTo("调治"), "文案权威 = pause-shell-42.md §5");
        }

        [Test]
        public void test_pauseResumeRow_closesTheShell()
        {
            _driver.PauseSink = new DriverPauseCommandSink(_driver);
            _driver.OpenModal(ModalId.Pause);
            var pause = (PauseScreen)_driver.Screen;

            pause.Activate(0);   // 「继续」

            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.None), "继续 = 合上暂停壳");
            Assert.That(_driver.BlocksPlay, Is.False, "合上后世界意图立刻恢复");
        }

        [Test]
        public void test_pauseScreen_withoutSink_doesNotThrow()
        {
            // 未注入 sink = 未接线的诚实形态:行照画、按了留空(不崩、不造动作)。
            _driver.OpenModal(ModalId.Pause);
            var pause = (PauseScreen)_driver.Screen;

            Assert.DoesNotThrow(() => pause.Activate(0));
            Assert.DoesNotThrow(() => pause.Activate(1));   // 「保存」未接线形态
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.Pause), "无出口时不得自己乱换屏");
        }

        [Test]
        public void test_openModal_focusScope_disablesOutside_andRestoresOnClose()
        {
            // 🔁 2026-10-31 走查实测缺陷的回归钉:模态打开后,方向键把焦点**移出了模态**
            //    (落到同一面板上的急救候选「跳过」行)⇒ ADR-013 §五 的「焦点单栈门」
            //    在运行期不成立。UI Toolkit 没有内建焦点陷阱 ⇒ 由 driver 显式施加。
            VisualElement panel = _driver.GetUiRoot();
            var outsider = new VisualElement { name = "outside-focusable" };
            outsider.focusable = true;
            panel.Add(outsider);

            _driver.OpenModal(ModalId.Pause);
            var pause = (PauseScreen)_driver.Screen;

            Assert.That(outsider.focusable, Is.False,
                "模态开 ⇒ 面板上**模态外**的可聚焦元素必须被摘 —— 否则方向键会逃出模态");
            Assert.That(pause.RowAt(0).focusable, Is.True,
                "模态自己的行必须仍可聚焦(否则锁过头,玩家一步也走不了)");

            _driver.CloseModal();
            Assert.That(outsider.focusable, Is.True,
                "关屏 ⇒ 必须还原 —— 不得把原本可聚焦的元素永久废掉");
        }

        [Test]
        public void test_openModal_focusScope_doesNotTouchNeverFocusableElements()
        {
            // ⚠️ 还原判据必须按**记录回放**,不能"统统设成 true" —— 那会凭空造出
            //    本来不可聚焦的元素(它们会进入焦点序,玩家会停在空洞上)。
            VisualElement panel = _driver.GetUiRoot();
            var plain = new VisualElement { name = "never-focusable" };   // focusable 默认 false
            panel.Add(plain);

            _driver.OpenModal(ModalId.Pause);
            _driver.CloseModal();

            Assert.That(plain.focusable, Is.False,
                "原本不可聚焦的元素,关屏后仍须不可聚焦(还原按记录,不按「应该能聚焦」)");
        }

        [Test]
        public void test_pauseRows_haveVisibleText()
        {
            // 🔁 2026-10-31 走查实测缺陷的回归钉:首版两行只有 `.ruled`,**零 Label**
            //    ⇒ 行在、可点、可获焦,但玩家**什么都看不见**(三张走查截图里一次都没看见暂停壳)。
            //    本判据只问「每行有没有可读文字」,不问排版/字体(那归美术轮)。
            _driver.OpenModal(ModalId.Pause);
            var pause = (PauseScreen)_driver.Screen;

            // 🔁 2026-11-05:增「保存」与「退出游戏」—— 陈旧期望 3 → 5。
            string[] expected = { "继续", "保存", "存档位", "调治", "退出游戏" };
            for (int i = 0; i < pause.RowCount; i++)
            {
                Label text = pause.RowAt(i).Q<Label>();
                Assert.That(text, Is.Not.Null, $"第 {i} 行没有 Label —— 玩家看不见这一行");
                Assert.That(text.text, Is.EqualTo(expected[i]),
                    "行文案归 `pause-shell-42.md` §5,不得在别处改写");
            }
        }

        [Test]
        public void test_pauseRows_areFocusable_inRowOrder()
        {
            _driver.OpenModal(ModalId.Pause);
            var pause = (PauseScreen)_driver.Screen;

            for (int i = 0; i < pause.RowCount; i++)
            {
                VisualElement row = pause.RowAt(i);
                Assert.That(row.focusable, Is.True, $"第 {i} 行不可获焦 —— 手柄走不到");
                Assert.That(row.tabIndex, Is.EqualTo(i), "焦点顺序 = 行序");
            }

            Assert.That(pause.FirstFocusable, Is.SameAs(pause.RowAt(0)), "开屏落焦 = 第一行(继续)");
        }

        private sealed class FakeArmed : IArmedState
        {
            public bool IsArmed { get; set; }
        }

        /// <summary>开合面的夹具实现(不碰引擎对象)。</summary>
        /// <remarks>🔁 2026-10-31(Amendment C):开合对象由存档位改为**暂停壳**,
        /// 成员随之改名 —— 名字继续写 `SaveSlots` 会骗人。</remarks>
        private sealed class FakeOpener : ModalOpener
        {
            public int OpenCalls;
            public int CloseCalls;

            /// <summary>当前模态身份。🔁 2026-11-04(`TD-skeuo-03`):两个逐员布尔 → 本项。
            /// 夹具可直接把它置成**任意**一员来模拟「玩家正摊着某屏」。</summary>
            public ModalId CurrentModal { get; set; } = ModalId.None;

            public void OpenPause() { OpenCalls++; CurrentModal = ModalId.Pause; }

            public void Close() { CloseCalls++; CurrentModal = ModalId.None; }
        }
    }
}
