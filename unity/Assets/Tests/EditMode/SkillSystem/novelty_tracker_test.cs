// ============================================================================
// 新颖度追踪 EditMode 单测 —— Story 006 验收
// 权威来源: production/epics/skill-system/story-006-novelty-tracking.md
//   · AC-1 ~ AC-6(首次/冷却内/冷却外/actor 独立/联机不共享/对象粒度)
//   · ADR-026 §Decision 四(调参表 NOVELTY_COOLDOWN) + Implementation Guidelines
//   · ADR-005 确定性 sim(纯函数查询,无随机)
//   · ADR-009 §四(派生态重建三源不变量 —— lastSeenTick 来自 tick 序列,非墙钟)
// ============================================================================
// 测试策略:全部用 int 断言 tick / NoveltyClass;零随机种子、零时间依赖、零外部 I/O。
// 对象粒度由调用方传入 object_id 决定(Story 006 AC-6),30 不定义对象语义。
// ============================================================================

using System;
using NUnit.Framework;
using DaYiJingCheng.Sim.Contracts.SkillSystem;

namespace DaYiJingCheng.Tests.Unit.SkillSystem
{
    /// <summary>Story 006 新颖度追踪的 EditMode 单测。</summary>
    [TestFixture]
    internal sealed class NoveltyTrackerTest
    {
        private const int NOVELTY_COOLDOWN = 24000; // 20 min @ 20 Hz(Story 006 默认值)

        // ═══════════════════════════════════════════════════════════════════
        // AC-1: 首次遇见 → First(×3.0),记录 lastSeenTick
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-1 主例: 字典未命中 → First,lastSeenTick = currentTick。</summary>
        [Test]
        public void test_ac1_firstEncounter_returnsFirst_andRecordsTick()
        {
            var tracker = new NoveltyTracker(NOVELTY_COOLDOWN);
            NoveltyClass result = tracker.Query(actorId: 1, skillId: 0, objectId: 1, currentTick: 100);

            Assert.That(result, Is.EqualTo(NoveltyClass.First),
                "首次遇见 → First(×3.0)");
            // 再次同一查询 → 不再是 First
            Assert.That(tracker.Query(1, 0, 1, 101), Is.EqualTo(NoveltyClass.Stale),
                "同一 actor 同一对象第二次 → Stale(冷却内)");
        }

        /// <summary>AC-1 边缘: 同一 actor 不同 objectId → 各自独立 First。</summary>
        [Test]
        public void test_ac1_differentObjectId_independentFirst()
        {
            var tracker = new NoveltyTracker(NOVELTY_COOLDOWN);
            Assert.That(tracker.Query(1, 0, 1, 100), Is.EqualTo(NoveltyClass.First),
                "objectId=1 首次 → First");
            Assert.That(tracker.Query(1, 0, 2, 100), Is.EqualTo(NoveltyClass.First),
                "objectId=2 首次 → First(独立)");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-2: 冷却期内再次遇见 → Stale(×0.2),lastSeenTick 不更新
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-2 主例: lastSeenTick=100, currentTick=101,冷却内 → Stale。</summary>
        [Test]
        public void test_ac2_withinCooldown_returnsStale_andLocksTick()
        {
            var tracker = new NoveltyTracker(NOVELTY_COOLDOWN);
            tracker.Query(1, 0, 1, 100); // First

            Assert.That(tracker.Query(1, 0, 1, 101), Is.EqualTo(NoveltyClass.Stale),
                "冷却期内再次遇见 → Stale(×0.2)");
            // 冷却锁定:lastSeenTick 仍 = 100
            Assert.That(tracker.Query(1, 0, 1, 100 + NOVELTY_COOLDOWN - 1), Is.EqualTo(NoveltyClass.Stale),
                "冷却锁定:lastSeenTick 不更新,仍为 Stale");
        }

        /// <summary>AC-2 边界: 恰好等于冷却期 → Normal(≥ 语义)。</summary>
        [Test]
        public void test_ac2_exactCooldownBoundary_returnsNormal()
        {
            var tracker = new NoveltyTracker(NOVELTY_COOLDOWN);
            tracker.Query(1, 0, 1, 100); // First

            Assert.That(tracker.Query(1, 0, 1, 100 + NOVELTY_COOLDOWN), Is.EqualTo(NoveltyClass.Normal),
                "currentTick - lastSeenTick = NOVELTY_COOLDOWN → Normal(≥ 语义)");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-3: 冷却期外再次遇见 → Normal(×1.0),更新 lastSeenTick
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-3 主例: lastSeenTick=100, currentTick=24100,冷却外 → Normal。</summary>
        [Test]
        public void test_ac3_afterCooldown_returnsNormal_andUpdatesTick()
        {
            var tracker = new NoveltyTracker(NOVELTY_COOLDOWN);
            tracker.Query(1, 0, 1, 100); // First

            Assert.That(tracker.Query(1, 0, 1, 24100), Is.EqualTo(NoveltyClass.Normal),
                "冷却期外再次遇见 → Normal(×1.0)");
            // 更新后 lastSeenTick = 24100,再查 24101 → Stale
            Assert.That(tracker.Query(1, 0, 1, 24101), Is.EqualTo(NoveltyClass.Stale),
                "Normal 后 lastSeenTick 更新为 24100,24101 → Stale");
        }

        /// <summary>AC-3 边缘: 仍在冷却内 → Stale,lastSeenTick 不更新。</summary>
        [Test]
        public void test_ac3_stillWithinCooldown_returnsStale()
        {
            var tracker = new NoveltyTracker(NOVELTY_COOLDOWN);
            tracker.Query(1, 0, 1, 100);

            Assert.That(tracker.Query(1, 0, 1, 24099), Is.EqualTo(NoveltyClass.Stale),
                "currentTick=24099 < 100+24000=24100 → Stale");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-4: 按 actor_id 独立(同一 skill/object,不同 actor 各自追踪)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-4 主例: actorId=1 Stale, actorId=2 First,同一 tick 同一对象。</summary>
        [Test]
        public void test_ac4_differentActorId_independentTracking()
        {
            var tracker = new NoveltyTracker(NOVELTY_COOLDOWN);
            tracker.Query(actorId: 1, skillId: 0, objectId: 1, currentTick: 100); // First

            Assert.That(tracker.Query(1, 0, 1, 101), Is.EqualTo(NoveltyClass.Stale),
                "actorId=1,冷却内 → Stale");
            Assert.That(tracker.Query(2, 0, 1, 101), Is.EqualTo(NoveltyClass.First),
                "actorId=2 未见过同一对象 → First(独立追踪)");
        }

        /// <summary>AC-4 边缘: 同一 actor 不同 skill → 独立追踪。</summary>
        [Test]
        public void test_ac4_differentSkillId_independentTracking()
        {
            var tracker = new NoveltyTracker(NOVELTY_COOLDOWN);
            tracker.Query(1, 0, 1, 100); // 诊断+大叶性肺炎 → First

            Assert.That(tracker.Query(1, 1, 1, 101), Is.EqualTo(NoveltyClass.First),
                "同一 actor,不同 skillId → First(独立)");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-5: 联机不共享(队友行为不影响本地新颖度字典)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-5: actorId=1(本地)未见过的对象,队友见过 → 本地仍为 First。</summary>
        [Test]
        public void test_ac5_teammateSeen_localStillFirst()
        {
            var localTracker = new NoveltyTracker(NOVELTY_COOLDOWN);
            var teammateTracker = new NoveltyTracker(NOVELTY_COOLDOWN);

            // 队友见过 objectId=1
            teammateTracker.Query(actorId: 2, skillId: 0, objectId: 1, currentTick: 100);

            // 本地玩家未见 → First(队友的字典不影响本地)
            Assert.That(localTracker.Query(1, 0, 1, 100), Is.EqualTo(NoveltyClass.First),
                "队友见过 ≠ 本地见过 → 本地仍为 First");
        }

        /// <summary>AC-5 边缘: 两个独立 tracker 实例互不影响(联机各玩家各自维护)。</summary>
        [Test]
        public void test_ac5_independentTrackers_noCrossContamination()
        {
            var trackerA = new NoveltyTracker(NOVELTY_COOLDOWN);
            var trackerB = new NoveltyTracker(NOVELTY_COOLDOWN);

            trackerA.Query(1, 0, 1, 100); // A 见过
            Assert.That(trackerB.Query(2, 0, 1, 101), Is.EqualTo(NoveltyClass.First),
                "trackerB 不受 trackerA 影响 → First");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-6: 格斗技能对象 = 对手类型(对象粒度由调用方 objectId 决定)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-6: objectId=兵痞类型(1) → 按类型判定,同一类型第二次 → Stale。</summary>
        [Test]
        public void test_ac6_combatObjectType_sameType_stale()
        {
            var tracker = new NoveltyTracker(NOVELTY_COOLDOWN);
            // 格斗:objectId=1 = 兵痞类型
            tracker.Query(actorId: 1, skillId: (int)WeaponLine.徒手, objectId: 1, currentTick: 100);

            Assert.That(tracker.Query(1, (int)WeaponLine.徒手, 1, 101), Is.EqualTo(NoveltyClass.Stale),
                "同一对手类型第二次 → Stale(冷却内)");
        }

        /// <summary>AC-6 边缘: objectId=野兽类型(2) → 独立于兵痞(1)。</summary>
        [Test]
        public void test_ac6_combatObjectType_differentType_independent()
        {
            var tracker = new NoveltyTracker(NOVELTY_COOLDOWN);
            tracker.Query(1, (int)WeaponLine.徒手, objectId: 1, currentTick: 100); // 兵痞

            Assert.That(tracker.Query(1, (int)WeaponLine.徒手, objectId: 2, 101), Is.EqualTo(NoveltyClass.First),
                "不同对手类型(野兽) → First(独立)");
        }

        // ═══════════════════════════════════════════════════════════════════
        // 字典操作与边界
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Peek(只读查询)不修改字典。</summary>
        [Test]
        public void test_peek_readOnly_doesNotModify()
        {
            var tracker = new NoveltyTracker(NOVELTY_COOLDOWN);
            tracker.Query(1, 0, 1, 100); // First,记录 lastSeenTick=100

            // 同一 tick Peek → Stale,不更新 lastSeenTick
            Assert.That(tracker.Peek(1, 0, 1, 100), Is.EqualTo(NoveltyClass.Stale),
                "Peek 同一 tick 返回 Stale,不更新字典");
            // 因为 Peek 未更新 lastSeenTick,Query 在同一 tick 仍为 Stale(非 Normal)
            Assert.That(tracker.Query(1, 0, 1, 100), Is.EqualTo(NoveltyClass.Stale),
                "Peek 后同 tick Query 仍 Stale(lastSeenTick 未变)");
        }

        /// <summary>Peek 未见过键 → First。</summary>
        [Test]
        public void test_peek_unseenKey_returnsFirst()
        {
            var tracker = new NoveltyTracker(NOVELTY_COOLDOWN);
            Assert.That(tracker.Peek(1, 0, 1, 100), Is.EqualTo(NoveltyClass.First),
                "Peek 未见过 → First");
        }

        /// <summary>Peek 冷却内 → Stale。</summary>
        [Test]
        public void test_peek_withinCooldown_returnsStale()
        {
            var tracker = new NoveltyTracker(NOVELTY_COOLDOWN);
            tracker.Query(1, 0, 1, 100); // First

            Assert.That(tracker.Peek(1, 0, 1, 101), Is.EqualTo(NoveltyClass.Stale),
                "Peek 冷却内 → Stale");
        }

        /// <summary>Reset 强制重置 lastSeenTick。</summary>
        [Test]
        public void test_reset_overwritesLastSeenTick()
        {
            var tracker = new NoveltyTracker(NOVELTY_COOLDOWN);
            tracker.Query(1, 0, 1, 100); // First

            tracker.Reset(1, 0, 1, 50000); // 重置到远期
            Assert.That(tracker.Query(1, 0, 1, 50001), Is.EqualTo(NoveltyClass.Stale),
                "Reset 后 lastSeenTick=50000,50001 → Stale(冷却内)");
            Assert.That(tracker.Query(1, 0, 1, 74000), Is.EqualTo(NoveltyClass.Normal),
                "Reset 后 74000 - 50000 = 24000 ≥ cooldown → Normal");
        }

        /// <summary>Remove 移除条目,之后重新查询 → First。</summary>
        [Test]
        public void test_remove_thenQuery_returnsFirst()
        {
            var tracker = new NoveltyTracker(NOVELTY_COOLDOWN);
            tracker.Query(1, 0, 1, 100); // First

            bool removed = tracker.Remove(1, 0, 1);
            Assert.That(removed, Is.True, "Remove 成功");

            Assert.That(tracker.Query(1, 0, 1, 200), Is.EqualTo(NoveltyClass.First),
                "Remove 后重新查询 → First");
        }

        /// <summary>Remove 不存在的键 → false。</summary>
        [Test]
        public void test_remove_nonexistentKey_returnsFalse()
        {
            var tracker = new NoveltyTracker(NOVELTY_COOLDOWN);
            bool removed = tracker.Remove(1, 0, 999);
            Assert.That(removed, Is.False, "不存在的键 → Remove 返回 false");
        }

        /// <summary>Clear 清空全部记录。</summary>
        [Test]
        public void test_clear_emptiesAllEntries()
        {
            var tracker = new NoveltyTracker(NOVELTY_COOLDOWN);
            tracker.Query(1, 0, 1, 100);
            tracker.Query(2, 0, 1, 100);
            Assert.That(tracker.Count, Is.EqualTo(2), "两条记录");

            tracker.Clear();
            Assert.That(tracker.Count, Is.EqualTo(0), "Clear 后 Count = 0");
            Assert.That(tracker.Query(1, 0, 1, 200), Is.EqualTo(NoveltyClass.First),
                "Clear 后重新查询 → First");
        }

        // ═══════════════════════════════════════════════════════════════════
        // 异常输入验证
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>负 actorId → ArgumentOutOfRangeException。</summary>
        [Test]
        public void test_negativeActorId_throws()
        {
            var tracker = new NoveltyTracker();
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                tracker.Query(-1, 0, 1, 100);
            }, "actorId 为负须抛 ArgumentOutOfRangeException");
        }

        /// <summary>零冷却期 → 任何再次遇见均为 Normal。</summary>
        [Test]
        public void test_zeroCooldown_alwaysNormalAfterFirst()
        {
            var tracker = new NoveltyTracker(cooldownTicks: 0);
            tracker.Query(1, 0, 1, 100); // First

            Assert.That(tracker.Query(1, 0, 1, 100), Is.EqualTo(NoveltyClass.Normal),
                "cooldown=0,同一 tick → Normal");
            Assert.That(tracker.Query(1, 0, 1, 101), Is.EqualTo(NoveltyClass.Normal),
                "cooldown=0,下一 tick → Normal");
        }

        /// <summary>时间回退(currentTick < lastSeenTick) → ArgumentOutOfRangeException。</summary>
        [Test]
        public void test_backwardTimeTravel_throws()
        {
            var tracker = new NoveltyTracker(NOVELTY_COOLDOWN);
            tracker.Query(1, 0, 1, 100); // First,lastSeenTick=100

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                tracker.Query(1, 0, 1, 50); // 回退到 50 < 100
            }, "currentTick < lastSeenTick 须抛 ArgumentOutOfRangeException");
        }
    }
}
