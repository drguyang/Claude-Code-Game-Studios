// `TD-skeuo-03` 的夹具(2026-11-04):**闭集每一员都能被同一个 toggle 关掉**。
//
// 权威:
//   · `ADR-013 §十`(`ModalId` 闭集;42 是模态真源)
//   · `inventory-container-20.md:56` / `clinic-panel-24.md:81` / `casebook-39.md:225`
//     —— 三份 Approved spec 的出口都是「同一 action toggle 关闭」
//   · `save-slots-7b.md:198`「关闭册 | 同一 toggle | 回进入前状态栈」
//
// ▍病因(本文件为什么存在)
//   旧判据是**逐员 if**:只认 `Casebook` / `Pause` 两个身份,其余 7 员按同一个 toggle
//   落进第三支 `OpenPause()` —— 也就是**换屏而不是关闭**。玩家以为关掉了,实际被弹到暂停壳,
//   再按一次又回世界,来回打转。
//
// ▍本文件钉的三件事
//   ① **判据是枚举驱动的**,不是逐员列举 —— 闭集增员时本文件**零改动**,且新员自动进判据。
//      这也是「收敛成一个可比较的量」的可证伪形态:逐员 if 写法下,新员不在分支里就会红。
//   ② **「关闭」与「换屏」必须可分** —— `OpenCalls` 必须为 0。只断「屏没了」不够:
//      换到暂停壳同样会让旧屏消失,那是假绿。
//   ③ **栈语义** —— 模态套模态时关的是上面那层,回到上一层;栈空才回世界。
//
// ⚠️ 本文件**不测**:那 3 个生产上打不开的屏(TD-skeuo-04)、焦点回进入前载体(P-02,
//    须真 panel ⇒ 归 `Tests/PlayMode/modal_focus_wiring_test.cs`)。

using System;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    [TestFixture]
    public sealed class ModalToggleClosureTest
    {
        /// <summary>只记动作、不碰引擎的开合面。</summary>
        private sealed class RecordingOpener : ModalOpener
        {
            public int OpenCalls;
            public int CloseCalls;
            public ModalId CurrentModal { get; set; } = ModalId.None;
            public void OpenPause() { OpenCalls++; CurrentModal = ModalId.Pause; }
            public void Close() { CloseCalls++; CurrentModal = ModalId.None; }
        }

        /// <summary>按一次 toggle(按下 → 松开)。上升沿只在「上一帧没按住」时产生,故必须成对调用。</summary>
        private static void Press(RecordingOpener opener)
        {
            bool held = false;
            var driver = new ModalInputDriver(() => held, opener);
            held = true;
            driver.OnFrame();     // 上升沿 ⇒ 动作
            held = false;
            driver.OnFrame();     // 松开(清掉驱动内部的按住态)
        }

        // ── ① 判据 = 闭集全体(枚举驱动,不是逐员列举) ──────────────────────────

        [Test]
        public void test_toggle_closesEveryMemberOfTheClosedSet()
        {
            ModalId[] closedSet = (ModalId[])Enum.GetValues(typeof(ModalId));
            int checkedCount = 0;

            foreach (ModalId modal in closedSet)
            {
                if (modal == ModalId.None) continue;      // None 不是模态

                var opener = new RecordingOpener { CurrentModal = modal };
                Press(opener);
                checkedCount++;

                Assert.That(opener.CloseCalls, Is.EqualTo(1),
                    $"{modal} 开着时按 toggle ⇒ 必须**关**（闭集每一员同一条规则，不逐员分支）");
                Assert.That(opener.OpenCalls, Is.EqualTo(0),
                    $"{modal} 开着时按 toggle ⇒ 不得**换屏到暂停壳**（换屏会让旧屏消失，" +
                    "只断「屏没了」会把它误判成关闭）");
                Assert.That(opener.CurrentModal, Is.EqualTo(ModalId.None));
            }

            // ⚠️ 空集守卫:闭集若被改空,上面的循环一次也不跑 ⇒ 整条恒绿。
            Assert.That(checkedCount, Is.GreaterThanOrEqualTo(8),
                "闭集至少 8 员(Casebook / SaveSlots / InventoryContainer / SettingsShell / " +
                "Tutorial / ClinicPanel / PaperCloseup48 / Processing / Pause)");
        }

        [Test]
        public void test_toggle_whenNoModalOpen_opensPauseShell()
        {
            var opener = new RecordingOpener { CurrentModal = ModalId.None };
            Press(opener);

            Assert.That(opener.OpenCalls, Is.EqualTo(1), "没有模态 ⇒ toggle 是「开暂停壳」");
            Assert.That(opener.CloseCalls, Is.EqualTo(0));
            Assert.That(opener.CurrentModal, Is.EqualTo(ModalId.Pause));
        }

        // ── ② 真驱动器上的栈语义(不经 FakeOpener) ──────────────────────────────

        [Test]
        public void test_realDriver_toggleOnSaveSlots_returnsToPauseShell_notWorld()
        {
            using (var rig = new DriverRig())
            {
                rig.Driver.OpenModal(ModalId.Pause);
                rig.Driver.OpenModal(ModalId.SaveSlots);
                Assert.That(rig.Driver.CurrentModal, Is.EqualTo(ModalId.SaveSlots), "夹具前提");

                rig.Press();

                Assert.That(rig.Driver.CurrentModal, Is.EqualTo(ModalId.Pause),
                    "存档位是从暂停壳进去的 ⇒ 关它要回**暂停壳**（回进入前状态栈），不是回世界");
            }
        }

        [Test]
        public void test_realDriver_toggleOnPause_returnsToWorld()
        {
            using (var rig = new DriverRig())
            {
                rig.Driver.OpenModal(ModalId.Pause);
                rig.Press();

                Assert.That(rig.Driver.CurrentModal, Is.EqualTo(ModalId.None),
                    "暂停壳是从世界进去的 ⇒ 关它回世界(栈空)");
            }
        }

        [Test]
        public void test_realDriver_toggleOnCasebook_closesBook()
        {
            using (var rig = new DriverRig())
            {
                rig.Driver.OpenModal(ModalId.Casebook);
                rig.Press();

                Assert.That(rig.Driver.CurrentModal, Is.EqualTo(ModalId.None),
                    "合册（39 §7「关册 | toggle」）");
            }
        }

        [Test]
        public void test_realDriver_toggleOnInventory_closesInsteadOfSwitchingToPause()
        {
            using (var rig = new DriverRig())
            {
                rig.Driver.OpenModal(ModalId.InventoryContainer);
                rig.Press();

                Assert.That(rig.Driver.CurrentModal, Is.EqualTo(ModalId.None),
                    "库存容器按 toggle ⇒ 关闭。旧写法的第三支会把它换成暂停壳 —— " +
                    "玩家看到的仍是「有个屏在」，却换了一个屏");
            }
        }

        /// <summary>真驱动 + `SkeuoRuntimeDriver` 的开合面(走生产装配那条路)。</summary>
        private sealed class DriverRig : IDisposable
        {
            private readonly UnityEngine.GameObject _go;
            private bool _held;

            public SkeuoRuntimeDriver Driver { get; }
            public ModalInputDriver Input { get; }

            public DriverRig()
            {
                _go = new UnityEngine.GameObject("modal-toggle-closure-test");
                Driver = _go.AddComponent<SkeuoRuntimeDriver>();
                // EditMode 下 AddComponent 不跑 Awake ⇒ 显式调一次(它只做 EnsureDocument + 建屏)。
                var awake = typeof(SkeuoRuntimeDriver).GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public);
                Assert.That(awake, Is.Not.Null, "夹具前提:`Awake` 不存在(驱动形态变了?)");
                awake.Invoke(Driver, null);

                // 输入读面是「按住为 true」⇒ 用一个可变的字段喂它,`Press()` 成对模拟按下/松开。
                _held = false;
                Input = new ModalInputDriver(() => _held, new DriverModalOpener(Driver));
            }

            /// <summary>按一次 toggle —— 走**真** `ModalInputDriver` 的上升沿,不直接调 driver。</summary>
            public void Press()
            {
                _held = true;
                Input.OnFrame();
                _held = false;
                Input.OnFrame();
            }

            public void Dispose()
            {
                if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            }
        }
    }
}
