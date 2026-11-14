// 全屏遮罩 · 标题卡(42 侧原语 V-12 · 2026-11-14)夹具
//
// 权威:
//   `design/gdd/skeuomorphic-ui.md` §一 V-12
//   `design/levels/prologue-beat-script.md` P1-06(黑底字幕)/ P3-05(黑底 + 标题卡 → 淡出)
//
// ═══ 它钉的五件事 ═════════════════════════════════════════════════════════
//   ① 初始关(display none、透明度 0、`IsUp == false`);
//   ② 淡入 / 淡出的**逐帧推进**与完成判据(`IsFullyOpaque` / `IsFullyTransparent`);
//   ③ 动效缩放 = 0 ⇒ 瞬切,且 `Step` 不崩(AC-42-G4 后半);
//   ④ 重复 Show 不闪回全透明;淡出中途 Show 只反向目标;
//   ⑤ 遮罩铺满且 `pickingMode = Ignore`(不是交互面)、Show 后压在最上层。
//
// ⚠️ **未覆盖**:真实渲染(颜色 / 字号 / 居中的观感)= 真包走查,不借绿。

using System;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.SkeuomorphicUI
{
    [TestFixture]
    public sealed class full_screen_mask_test
    {
        private VisualElement _host;
        private FullScreenMask _mask;

        [SetUp]
        public void Setup()
        {
            _host = new VisualElement();
            _mask = new FullScreenMask(_host);
        }

        private DisplayStyle Display => _mask.Root.style.display.value;
        private float StyleOpacity => _mask.Root.style.opacity.value;

        // ── ① 初始关 ────────────────────────────────────────────────────────

        [Test]
        public void test_initialState_hidden()
        {
            Assert.IsFalse(_mask.IsUp);
            Assert.IsTrue(_mask.IsFullyTransparent);
            Assert.IsFalse(_mask.IsFullyOpaque, "关着的遮罩不是「全黑」");
            Assert.AreEqual(DisplayStyle.None, Display);
            Assert.AreEqual(string.Empty, _mask.Title);
        }

        [Test]
        public void test_mask_coversHost_andIgnoresPicking()
        {
            Assert.AreSame(_host, _mask.Root.parent, "创建即挂上宿主");
            Assert.AreEqual(PickingMode.Ignore, _mask.Root.pickingMode,
                "铺满后不吞指针(与 .modal-host 同一条理由)");
            Assert.AreEqual(PickingMode.Ignore, _mask.Root.Q("full-screen-mask-title").pickingMode);
        }

        // ── ② 淡入 / 淡出 ──────────────────────────────────────────────────

        [Test]
        public void test_show_startsTransparent_thenFadesIn()
        {
            _mask.Show("那一夜");

            Assert.IsTrue(_mask.IsUp);
            Assert.AreEqual(DisplayStyle.Flex, Display);
            Assert.AreEqual(0f, _mask.Opacity, 1e-5f, "淡入从全透明起步(不在过渡前闪一帧全黑)");
            Assert.AreEqual("那一夜", _mask.Title);

            _mask.Step(FullScreenMask.BaseFadeSeconds / 2f);
            Assert.AreEqual(0.5f, StyleOpacity, 1e-4f);
            Assert.IsFalse(_mask.IsFullyOpaque);

            _mask.Step(FullScreenMask.BaseFadeSeconds / 2f);
            Assert.AreEqual(1f, _mask.Opacity, 1e-5f);
            Assert.IsTrue(_mask.IsFullyOpaque);
        }

        [Test]
        public void test_hide_fadesOut_thenDismisses()
        {
            _mask.Snap(true);
            _mask.Hide();

            Assert.IsTrue(_mask.IsUp, "淡出中仍在屏上(遮罩还看不见整个世界)");
            _mask.Step(FullScreenMask.BaseFadeSeconds / 2f);
            Assert.AreEqual(0.5f, _mask.Opacity, 1e-4f);

            _mask.Step(FullScreenMask.BaseFadeSeconds / 2f);
            Assert.AreEqual(0f, _mask.Opacity, 1e-5f);
            Assert.IsFalse(_mask.IsUp, "淡出完成 ⇒ P3-05 的完成判据成立");
            Assert.IsTrue(_mask.IsFullyTransparent);
            Assert.AreEqual(DisplayStyle.None, Display);
        }

        [Test]
        public void test_opacity_clampsAtBothEnds()
        {
            _mask.Show(string.Empty);
            _mask.Step(100f);
            Assert.AreEqual(1f, _mask.Opacity, 1e-5f, "超步不得越过 1");

            _mask.Hide();
            _mask.Step(100f);
            Assert.AreEqual(0f, _mask.Opacity, 1e-5f, "超步不得越过 0");
        }

        // ── ③ 动效缩放 = 0 ─────────────────────────────────────────────────

        [Test]
        public void test_motionScaleZero_snapsBothWays()
        {
            _mask.Motion.Scale = 0f;

            _mask.Show("序章 · 神父走了");
            Assert.AreEqual(1f, _mask.Opacity, 1e-5f, "无动效 ⇒ 瞬切到全黑");
            Assert.IsTrue(_mask.IsFullyOpaque);

            _mask.Hide();
            Assert.AreEqual(0f, _mask.Opacity, 1e-5f, "无动效 ⇒ 瞬切退场");
            Assert.IsFalse(_mask.IsUp);
            Assert.AreEqual(DisplayStyle.None, Display);

            Assert.DoesNotThrow(() => _mask.Step(0.1f), "退场后 Step 不得崩(界面仍可用)");
        }

        [Test]
        public void test_motionScale_halvesSpeed() =>
            // 缩放在 `EvaluateDuration` 里吸收一次(不是再乘一次)。
            Assert.AreEqual(FullScreenMask.BaseFadeSeconds / 2f,
                new MotionScaleHook { Scale = 0.5f }.EvaluateDuration(FullScreenMask.BaseFadeSeconds),
                1e-5f);

        // ── ④ 重入 ─────────────────────────────────────────────────────────

        [Test]
        public void test_showWhileUp_keepsOpacity_changesTitle()
        {
            _mask.Snap(true);
            _mask.Show("第二行");

            Assert.AreEqual(1f, _mask.Opacity, 1e-5f, "重复 Show 不得闪回全透明");
            Assert.AreEqual("第二行", _mask.Title);
        }

        [Test]
        public void test_showDuringFadeOut_reversesTarget()
        {
            _mask.Snap(true);
            _mask.Hide();
            _mask.Step(FullScreenMask.BaseFadeSeconds / 2f);
            Assert.AreEqual(0.5f, _mask.Opacity, 1e-4f);

            _mask.Show("回来");
            Assert.AreEqual(0.5f, _mask.Opacity, 1e-4f, "反向不重置不透明度");
            Assert.IsTrue(_mask.IsUp);

            _mask.Step(FullScreenMask.BaseFadeSeconds / 2f);
            Assert.AreEqual(1f, _mask.Opacity, 1e-5f);
            Assert.IsTrue(_mask.IsFullyOpaque);
        }

        [Test]
        public void test_stepWhenHidden_isNoOp()
        {
            Assert.DoesNotThrow(() => _mask.Step(0.5f));
            Assert.AreEqual(0f, _mask.Opacity, 1e-5f);
        }

        [Test]
        public void test_hideWhenHidden_isNoOp()
        {
            Assert.DoesNotThrow(() => _mask.Hide());
            Assert.IsFalse(_mask.IsUp);
        }

        [Test]
        public void test_negativeStep_throws()
        {
            _mask.Show(string.Empty);
            Assert.Throws<ArgumentOutOfRangeException>(() => _mask.Step(-0.1f));
        }

        // ── ⑤ 层级 ─────────────────────────────────────────────────────────

        [Test]
        public void test_show_movesMaskAboveExistingScreens()
        {
            var later = new VisualElement();
            _host.Add(later);

            _mask.Show(string.Empty);

            Assert.AreSame(_mask.Root, _host[_host.childCount - 1],
                "遮罩必须是最上层(压住此前已摊开的屏)");
        }

        [Test]
        public void test_show_emptyTitle_hidesTitleLabel()
        {
            _mask.Show("有字");
            Assert.AreEqual(DisplayStyle.Flex, _mask.Root.Q("full-screen-mask-title").style.display.value);

            _mask.Show(string.Empty);
            Assert.AreEqual(DisplayStyle.None, _mask.Root.Q("full-screen-mask-title").style.display.value,
                "空题签 = 纯黑场(P1-06)");
        }
    }
}
