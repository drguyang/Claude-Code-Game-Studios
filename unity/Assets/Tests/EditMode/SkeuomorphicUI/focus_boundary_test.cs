namespace DaYiJingCheng.Tests.Unit.SkeuomorphicUI
{
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
    using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using UnityEngine.EventSystems;

    /// <summary>
    /// 焦点导航边界断言 + 焦点悬空回退 单元测试。
    /// <para>覆盖 AC-42-B1 / AC-42-B7 / 同键双触发禁令(AC-3-C2) / AC-42-B2。</para>
    /// </summary>
    [TestFixture]
    public class focus_boundary_test
    {
        // ── AC-42-B1: rank 数据满射 ──

        [Test]
        public void test_rank_data_surjective_all_controls_have_rank()
        {
            // Arrange: 全部控件 FocusRank >= 1
            var controls = new IFocusable[]
            {
                new MockFocusable(rank: 1, enabled: true),
                new MockFocusable(rank: 2, enabled: true),
                new MockFocusable(rank: 3, enabled: true)
            };

            // Act + Assert: 满射断言不应抛异常
            Assert.DoesNotThrow(() =>
                FocusBoundaryAssertions.AssertRankDataSurjective(controls));
        }

        [Test]
        public void test_missing_rank_throws()
        {
            // Arrange: 控件集合包含 null 引用(模拟装载遗漏)
            var controls = new IFocusable[]
            {
                new MockFocusable(rank: 1, enabled: true),
                null,
                new MockFocusable(rank: 3, enabled: true)
            };

            // Act + Assert: 满射断言应抛异常
            var ex = Assert.Throws<InvalidOperationException>(() =>
                FocusBoundaryAssertions.AssertRankDataSurjective(controls));

            StringAssert.Contains("null 引用", ex.Message);
        }

        // ── AC-42-B1: rank 数据单射 ──

        [Test]
        public void test_rank_data_injective_ranks_are_unique()
        {
            // Arrange: 全部控件 rank 唯一
            var controls = new IFocusable[]
            {
                new MockFocusable(rank: 1, enabled: true),
                new MockFocusable(rank: 2, enabled: true),
                new MockFocusable(rank: 3, enabled: true)
            };

            // Act + Assert: 单射断言不应抛异常
            Assert.DoesNotThrow(() =>
                FocusBoundaryAssertions.AssertRankDataInjective(controls));
        }

        [Test]
        public void test_duplicate_rank_throws()
        {
            // Arrange: 两个控件共享 rank=2
            var controls = new IFocusable[]
            {
                new MockFocusable(rank: 1, enabled: true),
                new MockFocusable(rank: 2, enabled: true),
                new MockFocusable(rank: 2, enabled: true)
            };

            // Act + Assert: 单射断言应抛异常并列出冲突 rank
            var ex = Assert.Throws<InvalidOperationException>(() =>
                FocusBoundaryAssertions.AssertRankDataInjective(controls));

            StringAssert.Contains("rank=2", ex.Message);
            StringAssert.Contains("单射性被破坏", ex.Message);
        }

        // ── 同键双触发禁令(AC-3-C2) ──

        [Test]
        public void test_no_dual_binding_safe_controls_pass()
        {
            // Arrange: 仅官方 Navigate(无自建焦点动作)
            Assert.DoesNotThrow(() =>
                FocusBoundaryAssertions.ValidateNoDualBinding(
                    controlName: "btn-menu",
                    hasOfficialNavigate: true,
                    hasCustomFocusAction: false));

            // Arrange: 仅自建焦点动作(无官方 Navigate)
            Assert.DoesNotThrow(() =>
                FocusBoundaryAssertions.ValidateNoDualBinding(
                    controlName: "btn-custom",
                    hasOfficialNavigate: false,
                    hasCustomFocusAction: true));

            // Arrange: 两者均无
            Assert.DoesNotThrow(() =>
                FocusBoundaryAssertions.ValidateNoDualBinding(
                    controlName: "btn-none",
                    hasOfficialNavigate: false,
                    hasCustomFocusAction: false));
        }

        [Test]
        public void test_no_dual_binding_dual_bound_throws()
        {
            // Arrange + Act + Assert: 同键双触发应抛异常
            var ex = Assert.Throws<InvalidOperationException>(() =>
                FocusBoundaryAssertions.ValidateNoDualBinding(
                    controlName: "btn-conflict",
                    hasOfficialNavigate: true,
                    hasCustomFocusAction: true));

            StringAssert.Contains("同键双触发", ex.Message);
            StringAssert.Contains("btn-conflict", ex.Message);
        }

        // ── AC-42-B2: K=0 焦点门保持关闭 ──
        // ⚠️ K=0 的「界面仍渲染 + 门不开」由 UI 层呈现逻辑负责,不归 FocusNavigationBridge。
        //   本桥接线只做「两栈之间焦点门切换」;K=0 时 UI 层自行保持门关,
        //   桥接线不感知 K≠0/K=0 差异,因此不在本测试夹具覆盖。
        //   AC-42-B2 的渲染层验证 = ADVISORY,待桌面 PlayMode / 手动走查。

        // ── AC-42-B7: 焦点悬空回退 ──

        [Test]
        public void test_focus_orphan_fallback_to_nearest_valid()
        {
            // Arrange: 当前焦点 rank=5 的控件失效;rank=3 和 rank=8 仍有效
            var controls = new IFocusable[]
            {
                new MockFocusable(rank: 1, enabled: true),
                new MockFocusable(rank: 3, enabled: true),   // 最近有效(|3-5|=2)
                new MockFocusable(rank: 5, enabled: false),  // 当前焦点已失效
                new MockFocusable(rank: 8, enabled: true)    // |8-5|=3
            };

            // Act
            int fallback = FocusOrphanHandler.HandleOrphan(controls, currentRank: 5);

            // Assert: 应回退到 rank=3(距离最近)
            Assert.AreEqual(3, fallback);
        }

        [Test]
        public void test_focus_orphan_all_invalid_returns_minus_one()
        {
            // Arrange: 全部控件均禁用
            var controls = new IFocusable[]
            {
                new MockFocusable(rank: 1, enabled: false),
                new MockFocusable(rank: 2, enabled: false),
                new MockFocusable(rank: 3, enabled: false)
            };

            // Act
            int fallback = FocusOrphanHandler.HandleOrphan(controls, currentRank: 2);

            // Assert: 无有效回退目标,返回 -1
            Assert.AreEqual(-1, fallback);
        }

        [Test]
        public void test_focus_orphan_skips_disabled_controls()
        {
            // Arrange: rank=5 失效;rank=4 禁用(跳过);rank=6 有效
            var controls = new IFocusable[]
            {
                new MockFocusable(rank: 2, enabled: true),
                new MockFocusable(rank: 4, enabled: false), // 禁用,跳过
                new MockFocusable(rank: 5, enabled: false), // 当前焦点失效
                new MockFocusable(rank: 6, enabled: true)   // 有效(|6-5|=1)
            };

            // Act
            int fallback = FocusOrphanHandler.HandleOrphan(controls, currentRank: 5);

            // Assert: 应选 rank=6(rank=4 禁用被跳过)
            Assert.AreEqual(6, fallback);
        }

        // ── Mock 工具 ──

        /// <summary>模拟 IFocusable(测试用)。</summary>
        private sealed class MockFocusable : IFocusable
        {
            public int FocusRank { get; }
            public bool IsFocusEnabled { get; }

            public MockFocusable(int rank, bool enabled)
            {
                FocusRank = rank;
                IsFocusEnabled = enabled;
            }
        }

        /// <summary>模拟 IPresentationRoot(测试用)。</summary>
        private sealed class MockPresentationRoot : IPresentationRoot
        {
            public bool Active { get; private set; }

            public void SetFocusGate(bool active)
            {
                Active = active;
            }
        }

        /// <summary>模拟 EventSystem(测试用)。</summary>
        private sealed class MockEventSystem : EventSystem
        {
        }
    }
}
