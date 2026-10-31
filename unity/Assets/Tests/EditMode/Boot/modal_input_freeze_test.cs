// 模态期输入冻结(2026-10-31)—— 规则七遗漏的**移动轴**那半边。
//
// 权威:design/gdd/interaction-system.md 规则七 · ADR-013 §十 / §十-C(42 是模态真源)
//
// ── 本件钉什么 ─────────────────────────────────────────────────────
// ① 无模态 ⇒ 移动轴原样透传(**正控**:否则「挡路就折零」在下述用例里恒真而不证明任何事);
// ② 有挡路模态 ⇒ 轴折零(暂停壳摊开时玩家不该还能走);
// ③ 提供者为 null ⇒ 不冻结(「无 UI ⇒ 无模态」的诚实降级);
// ④ **实时读,零缓存** —— 同一帧内两个方向各问一次,必须各自如实。
//
// ⚠️ **本件不判**:`BootRoot.Update` 里那一行接线是否真的被调用(headless 没有帧泵);
//    也不判 `BuildInputDriver` 的挡路分支 —— 那条要真 `BuildController` 才能构造驱动,
//    本件不复制那套装配。两者均按 `NOT-RUN` 登记,见 `BootRoot` 与 `BuildInputDriver` 的注释。

using DaYiJingCheng.Gameplay.Boot;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>模态期移动轴的冻结面。</summary>
    [TestFixture]
    public sealed class ModalInputFreezeTest
    {
        private static readonly Vector2 Axis = new Vector2(0.7f, -0.7f);

        [Test]
        public void test_freeze_notBlocked_passesMoveAxisThrough()
        {
            var freeze = new ModalInputFreeze(() => false);

            Assert.That(freeze.IsBlocked, Is.False);
            Assert.That(freeze.FilterMovement(Axis), Is.EqualTo(Axis),
                "无模态时不得改轴 —— 否则「冻结生效」这条判据在下一条里毫无判别力");
        }

        [Test]
        public void test_freeze_blocked_zeroesMoveAxis()
        {
            var freeze = new ModalInputFreeze(() => true);

            Assert.That(freeze.IsBlocked, Is.True);
            Assert.That(freeze.FilterMovement(Axis), Is.EqualTo(Vector2.zero),
                "暂停壳摊开时玩家不该还能走(规则七漏掉的移动轴)");
        }

        [Test]
        public void test_freeze_nullPredicate_neverBlocks()
        {
            // 诚实降级:没有 UI ⇒ 没有模态 ⇒ 不冻结。
            // ⚠️ 与「假装有一条模态开着」相对 —— 后者会让玩家在世界里彻底动不了。
            var freeze = new ModalInputFreeze(null);

            Assert.That(freeze.IsBlocked, Is.False);
            Assert.That(freeze.FilterMovement(Axis), Is.EqualTo(Axis));
        }

        [Test]
        public void test_freeze_readsLive_doesNotCache()
        {
            // 缓存 = 第二真源:模态在帧内开合时读到的是上一帧的答案。
            bool blocked = false;
            var freeze = new ModalInputFreeze(() => blocked);

            Assert.That(freeze.FilterMovement(Axis), Is.EqualTo(Axis), "先问一次(未挡)");

            blocked = true;
            Assert.That(freeze.FilterMovement(Axis), Is.EqualTo(Vector2.zero),
                "同一实例,状态下一次调用就必须变 —— 缓存过的实现会在这里给旧答案");

            blocked = false;
            Assert.That(freeze.FilterMovement(Axis), Is.EqualTo(Axis), "关屏立刻恢复");
        }
    }
}
