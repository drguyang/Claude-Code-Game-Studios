// 帐篷搭建演出(N3 调用方)+ 过场机位夹具(2026-11-14)。
//
// 权威:`design/levels/prologue-beat-script.md` P3-02b(逐件生长 → 镜头绕半圈 → 解冻)
//       §2 相机行(独立机位,不得借 `CameraMode` 五档)
//
// ═══ 它钉的六件事 ═════════════════════════════════════════════════════════
//   ① **五件**:视觉体块 = 5 组(帐篷五件材料),出现顺序 = 材料清单顺序。
//   ② **逐件**:拍没到,组是零尺寸;拍到了,组从零长到全尺寸(底边贴地)。
//   ③ **机位**:开播前相机是关的;末拍进入后是开的;播完 / 中断都关(成对)。
//   ④ **圆弧**:相机与焦点的水平距离 = 半径,且朝焦点看(不是随手放一个位置)。
//   ⑤ **只演一次**:第二座帐篷的放置不重演(演出是 P3-02b 这一拍的,不是建造系统的通用反馈)。
//   ⑥ **不重叠**:已经有一段演出在播 ⇒ `TryPlay` 静默返回 false(不抛、不替换)。
//
// ⚠️ 「演出期冻结输入」的接线归 `cutscene_player_test` 的 `ModalInputFreeze` 一条
//    (冻结的唯一判定点在装配层,本件不重复证明)。

using System;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Cutscene;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>搭建演出:五件 / 逐件 / 机位 / 只演一次 / 不重叠。</summary>
    [TestFixture]
    public class tent_build_cutscene_test
    {
        private Material _mat;

        [SetUp]
        public void SetUp()
        {
            var shader = Shader.Find("Unlit/Color");
            Assert.IsNotNull(shader, "测试机缺 `Unlit/Color` —— 占位材质无从构造(fail-loud,不静默跳过)");
            _mat = new Material(shader);
        }

        [TearDown]
        public void TearDown()
        {
            if (_mat != null) Object.DestroyImmediate(_mat);
            _mat = null;
        }

        // ── ① 五件 ─────────────────────────────────────────────────────────

        [Test]
        public void test_fivePieces_built()
        {
            var visual = new TentAssemblyVisual(Vector3.zero, _mat);
            try
            {
                Assert.AreEqual(TentAssemblyVisual.PieceCount, 5, "帐篷五件材料 ⇒ 五组体块");
                Assert.AreEqual(0, visual.RevealedCount, "刚建好的帐篷一件都不该出现");
                Assert.IsFalse(visual.AllGrown);
                Assert.IsNotNull(visual.Root);
                // 四根柱子 = 房梁组有 4 个方块;组数 5、方块数 4+1+1+1+4 = 11
                int blocks = 0;
                for (int i = 0; i < visual.Root.childCount; i++) blocks++;
                Assert.AreEqual(11, blocks, "房梁 4 + 门板 1 + 布 1 + 油布 1 + 钉子 4");
            }
            finally { visual.Dispose(); }
        }

        // ── ② 逐件 ─────────────────────────────────────────────────────────

        [Test]
        public void test_reveal_isSequential_andIdempotent()
        {
            var visual = new TentAssemblyVisual(Vector3.zero, _mat);
            try
            {
                visual.Reveal(0);
                Assert.AreEqual(1, visual.RevealedCount);
                visual.Reveal(0);   // 幂等:不重置生长
                visual.Step(0.45f);
                Assert.AreEqual(1f, visual.ProgressOf(0), 1e-4f);

                Assert.AreEqual(0, visual.RevealedCount - 1, "其余四件仍未出现");
                Assert.IsFalse(visual.AllGrown);

                for (int i = 1; i < TentAssemblyVisual.PieceCount; i++) visual.Reveal(i);
                visual.Step(0.45f);
                Assert.IsTrue(visual.AllGrown, "五件全拍过 + 各长 0.45 s ⇒ 全部长完");
            }
            finally { visual.Dispose(); }
        }

        [Test]
        public void test_growth_smoothstepAndBottomAnchored()
        {
            var visual = new TentAssemblyVisual(Vector3.zero, _mat);
            try
            {
                visual.Reveal(2);   // 布(单方块组)
                visual.Step(TentAssemblyVisual.GrowSeconds * 0.5f);

                // 组内序 = 建组顺序 ⇒ 房梁 4 个方块(0…3)+ 门板(4)⇒ 布是第 6 个方块(0 起 5)
                Transform block = visual.Root.GetChild(5);
                Assert.Greater(block.localScale.x, 0.01f, "半程必须已有尺寸");
                Assert.Less(block.localScale.x, 2.40f, "半程不得已到全尺寸");

                // 底边贴地:中心 y = 全尺寸中心 y − 未长出的高度 × 0.5 ⇒ 半程低于 2.04
                Assert.Less(block.localPosition.y, 2.04f, "生长中的体块中心必须下压(从地里立起来)");

                visual.Step(TentAssemblyVisual.GrowSeconds);
                Assert.AreEqual(2.04f, block.localPosition.y, 1e-3f, "长满后中心回到全尺寸位置");
                Assert.AreEqual(2.40f, block.localScale.x, 1e-3f);
            }
            finally { visual.Dispose(); }
        }

        // ── ③ 机位开合 ─────────────────────────────────────────────────────

        [Test]
        public void test_camera_offUntilOrbit_andOffAfterEnd()
        {
            var player = new CutscenePlayer();
            var camera = new CutsceneCameraRig();
            var cutscene = new TentBuildCutscene(player, camera, _mat, () => 0L);
            try
            {
                Assert.IsTrue(cutscene.TryPlay(new WorldPos(108, 0, 16)));
                Assert.IsTrue(player.IsPlaying);
                Assert.AreEqual(TentAssemblyVisual.PieceCount + 1, player.BeatCount, "五件 + 运镜 = 六拍");
                Assert.AreEqual("tent-piece-0", player.CurrentBeatId);
                Assert.IsFalse(camera.IsPlaying, "第一拍是生长,不是运镜 —— 相机必须还关着");

                // 走完五件 = 5 × 24 tick
                player.OnTickEdge(TentBuildCutscene.TicksPerPiece * TentAssemblyVisual.PieceCount);
                Assert.AreEqual("tent-orbit", player.CurrentBeatId);
                Assert.IsTrue(camera.IsPlaying, "末拍进入 ⇒ 机位开播");

                // 运镜走完(按墙钟补间,3 s)
                cutscene.OnFrame(TentBuildCutscene.OrbitSeconds + 0.01f);
                Assert.IsFalse(camera.IsPlaying, "运镜播完 ⇒ 相机关");

                // 再推 1 tick 进入结束:播放器收尾(OnExit 再 End 一次,幂等)
                player.OnTickEdge(TentBuildCutscene.TicksPerPiece * TentAssemblyVisual.PieceCount
                                  + TentBuildCutscene.OrbitTicks);
                Assert.IsFalse(player.IsPlaying, "六拍走完 ⇒ 演出结束(解冻)");
                Assert.IsTrue(cutscene.Visual.AllGrown, "播完五件必须全尺寸(视觉保留)");
            }
            finally { cutscene.Dispose(); }
        }

        [Test]
        public void test_camera_abort_alsoEndsCamera()
        {
            var player = new CutscenePlayer();
            var camera = new CutsceneCameraRig();
            var cutscene = new TentBuildCutscene(player, camera, _mat, () => 0L);
            try
            {
                cutscene.TryPlay(new WorldPos(0, 0, 0));
                player.OnTickEdge(TentBuildCutscene.TicksPerPiece * TentAssemblyVisual.PieceCount);
                Assert.IsTrue(camera.IsPlaying);

                player.OnTickEdge(0L);   // 读档回拨 ⇒ 中断
                Assert.IsFalse(player.IsPlaying);
                Assert.IsFalse(camera.IsPlaying, "中断也必须关相机(OnExit 与开播成对)");
                Assert.IsFalse(camera.Camera.enabled, "且是真关(组件 enabled = false),不只是标志位");
            }
            finally { cutscene.Dispose(); }
        }

        // ── ④ 圆弧 ────────────────────────────────────────────────────────

        [Test]
        public void test_camera_orbitsAtRadius_facingFocus()
        {
            var camera = new CutsceneCameraRig(radius: 10f, height: 6f);
            try
            {
                var focus = new Vector3(3f, 1f, 4f);
                camera.Begin(focus, 35f, 120f, 3f);

                Vector3 p0 = camera.Camera.transform.position;
                Assert.AreEqual(10f, new Vector2(p0.x - focus.x, p0.z - focus.z).magnitude, 1e-3f,
                    "水平距离 = 半径");
                Assert.AreEqual(focus.y + 6f, p0.y, 1e-3f, "高度 = 焦点高度 + 构造值");
                Assert.Greater(Vector3.Dot(camera.Camera.transform.forward, focus - p0), 0f,
                    "相机必须朝焦点看(forward 与「指向焦点」同向:dot > 0)");

                camera.Step(1.5f);
                Vector3 pMid = camera.Camera.transform.position;
                Assert.AreEqual(10f, new Vector2(pMid.x - focus.x, pMid.z - focus.z).magnitude, 1e-3f);
                Assert.Greater(Vector3.Distance(p0, pMid), 0.01f, "半程必须已经动了");

                camera.Step(1.6f);
                Assert.IsFalse(camera.IsPlaying, "超过时长 ⇒ 自动收尾");
                Assert.IsFalse(camera.Camera.enabled);
            }
            finally { camera.Dispose(); }
        }

        [Test]
        public void test_camera_beginTwice_throws()
        {
            var camera = new CutsceneCameraRig();
            try
            {
                camera.Begin(Vector3.zero, 0f, 90f, 1f);
                Assert.Throws<InvalidOperationException>(() => camera.Begin(Vector3.zero, 0f, 90f, 1f));
            }
            finally { camera.Dispose(); }
        }

        // ── ⑤ 只演一次 ─────────────────────────────────────────────────────

        [Test]
        public void test_secondTent_doesNotReplay()
        {
            var player = new CutscenePlayer();
            var camera = new CutsceneCameraRig();
            var cutscene = new TentBuildCutscene(player, camera, _mat, () => 0L);
            try
            {
                Assert.IsTrue(cutscene.TryPlay(new WorldPos(108, 0, 16)));
                player.OnTickEdge(TentBuildCutscene.TicksPerPiece * TentAssemblyVisual.PieceCount
                                  + TentBuildCutscene.OrbitTicks);
                Assert.IsFalse(player.IsPlaying);

                Assert.IsFalse(cutscene.TryPlay(new WorldPos(0, 0, 0)),
                    "第二座帐篷的放置不重演搭建演出(P3-02b 只有一次)");
                Assert.IsFalse(player.IsPlaying);
            }
            finally { cutscene.Dispose(); }
        }

        // ── ⑥ 不重叠 ───────────────────────────────────────────────────────

        [Test]
        public void test_playWhileOtherCutscene_returnsFalse()
        {
            var player = new CutscenePlayer();
            var camera = new CutsceneCameraRig();
            var cutscene = new TentBuildCutscene(player, camera, _mat, () => 0L);
            try
            {
                // 别的演出(例如开场运镜)在播
                player.Play(new[] { new CutsceneBeat("other", 10) }, 0L);
                Assert.IsFalse(cutscene.TryPlay(new WorldPos(1, 0, 1)), "不重叠:静默跳过");
                Assert.AreEqual("other", player.CurrentBeatId, "别人的演出不得被替换");
            }
            finally { cutscene.Dispose(); }
        }

        // ── 生命周期 ───────────────────────────────────────────────────────

        [Test]
        public void test_dispose_idempotent_andDestroysVisuals()
        {
            var camera = new CutsceneCameraRig();
            var cutscene = new TentBuildCutscene(new CutscenePlayer(), camera, _mat, () => 0L);
            cutscene.TryPlay(new WorldPos(0, 0, 0));
            var visualRoot = cutscene.Visual.Root;

            cutscene.Dispose();
            cutscene.Dispose();   // 幂等

            Assert.IsTrue(visualRoot == null, "销毁后视觉根节点必须消失(Unity 伪空)");
            Assert.IsNull(cutscene.Visual);
        }
    }
}
