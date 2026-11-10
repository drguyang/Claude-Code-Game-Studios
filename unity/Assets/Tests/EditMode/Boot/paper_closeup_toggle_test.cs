// 手册开合驱动(`M` / 手柄 `select`)的判据夹具 —— T18 甲(2026-11-10)
//
// 权威:`PaperCloseupToggleDriver` 头注的三条判据(只认无模态时开 / 手册开着则关 /
//       只有上升沿算一次),以及「开屏被拒不计入 OpenCount」。
//
// ⚠️ 本件**不碰设备**:键读面与 42 的模态面全部注入假面 —— 测的是**判据**,
//    不是 `PlayerInputReader` 的按键表(那一条只能在真包 / 手柄走查里验)。
// ⚠️ 零写盘、零 I/O。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>手册近景开合驱动的判据(边沿 / 模态互斥 / 拒绝计数)。</summary>
    [TestFixture]
    public sealed class PaperCloseupToggleTest
    {
        /// <summary>可编排的假面(键 / 模态 / 开 / 关)。</summary>
        private sealed class Stage
        {
            public bool Pressed;
            public ModalId Current = ModalId.None;
            public bool PresentSucceeds = true;
            public int PresentCalls;
            public int CloseCalls;

            public PaperCloseupToggleDriver Driver() => new PaperCloseupToggleDriver(
                () => Pressed,
                () => Current,
                () =>
                {
                    PresentCalls++;
                    if (!PresentSucceeds) return false;
                    Current = ModalId.PaperCloseup48;   // 开成功后 42 的当前模态随之变
                    return true;
                },
                () =>
                {
                    CloseCalls++;
                    Current = ModalId.None;
                });

            /// <summary>按一次(按下 → 抬起),返回驱动。</summary>
            public void Tap(PaperCloseupToggleDriver driver)
            {
                Pressed = true; driver.OnFrame();
                Pressed = false; driver.OnFrame();
            }
        }

        // ══════════════ ① 上升沿 ══════════════

        [Test]
        public void test_holdingTheKey_firesOnce()
        {
            var stage = new Stage();
            PaperCloseupToggleDriver driver = stage.Driver();

            stage.Pressed = true;
            for (int frame = 0; frame < 10; frame++) driver.OnFrame();

            Assert.That(stage.PresentCalls, Is.EqualTo(1),
                "按住十帧只该开一次 —— 不持「上一帧」的写法会让手册每秒重开几十次");
            Assert.That(driver.OpenCount, Is.EqualTo(1));
        }

        [Test]
        public void test_releaseAndPressAgain_firesAgain()
        {
            var stage = new Stage();
            PaperCloseupToggleDriver driver = stage.Driver();

            stage.Tap(driver);          // 开
            stage.Tap(driver);          // 此时手册开着 ⇒ 关
            stage.Tap(driver);          // 再开

            Assert.That(driver.OpenCount, Is.EqualTo(2));
            Assert.That(driver.CloseCount, Is.EqualTo(1));
        }

        // ══════════════ ② 手册开着 ⇒ 第二次按 = 关 ══════════════

        [Test]
        public void test_whenManualOpen_secondPressCloses()
        {
            var stage = new Stage { Current = ModalId.PaperCloseup48 };
            PaperCloseupToggleDriver driver = stage.Driver();

            stage.Tap(driver);

            Assert.That(stage.CloseCalls, Is.EqualTo(1), "手册开着时按 M 应当收回去");
            Assert.That(stage.PresentCalls, Is.EqualTo(0), "不应再开一次(那是原地打转)");
            Assert.That(driver.CloseCount, Is.EqualTo(1));
            Assert.That(driver.OpenCount, Is.EqualTo(0));
        }

        // ══════════════ ③ 别的模态摊着 ⇒ 不抢 ══════════════

        [Test]
        public void test_whenAnotherModalIsOpen_doesNothing()
        {
            var stage = new Stage { Current = ModalId.Pause };
            PaperCloseupToggleDriver driver = stage.Driver();

            stage.Tap(driver);

            Assert.That(stage.PresentCalls, Is.EqualTo(0),
                "别的模态摊着时按 M 不该开手册(那会把玩家正看着的屏换掉)");
            Assert.That(stage.CloseCalls, Is.EqualTo(0),
                "也不该顺手关掉别人的屏 —— 「关任何模态」是 ESC 的判据,不是 M 的");
            Assert.That(stage.Current, Is.EqualTo(ModalId.Pause), "当前模态不变");
        }

        // ══════════════ ④ 开屏被拒 ⇒ 不计入 ══════════════

        [Test]
        public void test_whenPresentIsRefused_notCountedAsOpen()
        {
            var stage = new Stage { PresentSucceeds = false };
            PaperCloseupToggleDriver driver = stage.Driver();

            stage.Tap(driver);

            Assert.That(stage.PresentCalls, Is.EqualTo(1), "确实尝试过");
            Assert.That(driver.OpenCount, Is.EqualTo(0),
                "OpenCount 是「真的开了几次」,不是「按了几次」");
        }

        // ══════════════ ⑤ 构造期 fail-closed ══════════════

        [Test]
        public void test_constructor_rejectsNulls()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => new PaperCloseupToggleDriver(null, () => ModalId.None, () => true, () => { }));
            Assert.Throws<System.ArgumentNullException>(
                () => new PaperCloseupToggleDriver(() => false, null, () => true, () => { }));
            Assert.Throws<System.ArgumentNullException>(
                () => new PaperCloseupToggleDriver(() => false, () => ModalId.None, null, () => { }));
            Assert.Throws<System.ArgumentNullException>(
                () => new PaperCloseupToggleDriver(() => false, () => ModalId.None, () => true, null));
        }
    }
}
