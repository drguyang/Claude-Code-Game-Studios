// 模态开启器(2026-10-31)—— 42 侧运行期换屏的判据。
//
// 权威:ADR-013 §十(`ModalId` 闭集 + `IModalState`;42 是模态真源)·
//       design/ux/save-slots-7b.md:189(正式入口 = 暂停壳,今日未成屏 ⇒ 本轮的临时入口)
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
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.Casebook),
                "夹具前提:Awake 建的是 Casebook(序列化默认屏)");

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
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.Casebook), "失败的开关不得改变当前模态");
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

        /// <summary>开合面的夹具实现(不碰引擎对象)。</summary>
        private sealed class FakeOpener : ModalOpener
        {
            public int OpenCalls;
            public int CloseCalls;
            public bool IsSaveSlotsOpen { get; private set; }

            public void OpenSaveSlots() { OpenCalls++; IsSaveSlotsOpen = true; }

            public void Close() { CloseCalls++; IsSaveSlotsOpen = false; }
        }
    }
}
