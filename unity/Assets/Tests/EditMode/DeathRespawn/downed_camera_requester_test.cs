// story-007 —— 29 的档位请求载体(`DownedCameraRequester`)夹具。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里
// ─────────────────────────────────────────────────────────────────────────────
// 读:① `unity/Assets/**` 全部生产 `.cs`(经 `CameraSetModeRequesterGate.RunAll(".")` —— S29-05);
//     ② `docs/architecture/tr-registry.yaml` 与 `traceability-index.md`(S29-08 的登记面判据)。
// 写:**零写盘**。其余各测全在内存里跑(间谍 `ICameraRig`)。
//
// ── 本文件替代了什么 ──────────────────────────────────────────────────────────
// 相机请求此前是 `BootRoot` 里的**内联 lambda**(`a5f766f6`),**没有任何东西能指向它** ——
// 改坏它没有一条 AC 会红(缺口 A)。story-007 把它提取成具名类,**本文件就是那条 AC 的载体**。
//
// ── 本文件测什么 / 不测什么 ────────────────────────────────────────────────────
// ✅ 测:发没发、发的哪个档、requesterId 是不是 29、VR / 机位缺席时是否**不发**。
// ❌ 不测:**边沿不重复**(那条在呈现器的 `_downedRequested`,归 L2 PlayMode)
//    与**生产启动序真的接上了**(L2)。
//
// 权威来源:
//   `design/ux/death-transition-29.md` §9(`:188` / `:127` / `:129` / `:133`)
//   `design/gdd/camera-and-viewpoint.md` `R-2-5`(29 → Downed / 29 → Explore)
//   `docs/architecture/adr-020-player-controller-and-camera.md` §五 / Amendment C ①②
//   `production/epics/death-respawn/story-007-downed-camera-mode-request.md`

using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Presentation.Camera;
using DaYiJingCheng.Gameplay.Presentation.DeathTransition;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.DeathRespawn
{
    /// <summary>story-007 L1:29 的档位意图请求序列。</summary>
    [TestFixture]
    internal sealed class DownedCameraRequesterTest
    {
        private SpyCameraRig _rig;
        private DownedCameraRequester _requester;

        [SetUp]
        public void SetUp()
        {
            _rig = new SpyCameraRig();
            _requester = new DownedCameraRequester(() => _rig);
        }

        // ══════════════ S29-01 / S29-02 两个方向 ══════════════

        [Test]
        public void test_s29_01_downed_requestsDownedWithRequester29()
        {
            _requester.Request(downed: true);

            Assert.That(_rig.Describe(), Is.EqualTo("Downed:29"),
                "倒下 ⇒ 恰一条 (Downed, requesterId: 29)(spec §9 Φ1 · R-2-5)");
        }

        [Test]
        public void test_s29_02_notDowned_requestsExploreWithRequester29()
        {
            _requester.Request(downed: false);

            Assert.That(_rig.Describe(), Is.EqualTo("Explore:29"),
                "退出倒下 ⇒ 恰一条 (Explore, 29) —— 否则相机会**卡在倒下档**(甲案「同一请求方可降档」)");
        }

        [Test]
        public void test_s29_02_bothDirections_useSameRequester()
        {
            _requester.Request(downed: true);
            _requester.Request(downed: false);

            Assert.That(_rig.Describe(), Is.EqualTo("Downed:29,Explore:29"),
                "进 / 出两个方向的 requesterId **必须同源** —— 不是 29 发的 Explore 降不了 29 开的档");
        }

        // ══════════════ S29-03 VR 全禁 ══════════════

        [Test]
        public void test_s29_03_firstPersonInPlace_zeroRequests_bothDirections()
        {
            _rig.ModeValue = CameraMode.FirstPerson;

            _requester.Request(downed: true);
            _requester.Request(downed: false);

            Assert.That(_rig.Describe(), Is.Empty,
                "`FirstPerson` 在位 ⇒ **零请求**(spec §9「平面两段式相机运动不得平移到 VR」)。\n" +
                "⚠️ 注意:这里**不是**「换成别的档」,是**什么都不发**");
        }

        [Test]
        public void test_s29_03_exploreInPlace_stillRequests()
        {
            // 负控的反面:非 VR 档时不得被早退吞掉(否则 S29-03 可能是「恒不发」的假绿)。
            _rig.ModeValue = CameraMode.Explore;
            _requester.Request(downed: true);
            Assert.That(_rig.Describe(), Is.EqualTo("Downed:29"), "非 VR 档 ⇒ 照常发");
        }

        // ══════════════ S29-04 机位缺席 ══════════════

        [Test]
        public void test_s29_04_nullRig_silentNoThrow()
        {
            var absent = new DownedCameraRequester(() => null);

            Assert.DoesNotThrow(() => absent.Request(downed: true),
                "机位缺席(拆卸段 / 启动序早期)⇒ 静默,不抛");
            Assert.DoesNotThrow(() => absent.Request(downed: false));
            Assert.That(_rig.Describe(), Is.Empty, "不存在的机位不会收到任何请求");
            Assert.IsTrue(absent.HasCameraRig, "访问器本身仍在 ⇒ HasCameraRig = true(是机位没了,不是没绑)");
        }

        [Test]
        public void test_s29_04_nullAccessor_rejectedAtConstruction()
        {
            Assert.Throws<System.ArgumentNullException>(() => new DownedCameraRequester(null),
                "访问器为 null = 装配遗漏 ⇒ 构造期 fail-loud");
        }

        // ══════════════ S29-05 系统号单一出处 + 机械门 ══════════════

        [Test]
        public void test_s29_05_requesterId_isTwentyNine_andGateClean()
        {
            Assert.AreEqual(29, DownedCameraRequester.RequesterId,
                "29 = 死亡与复活的系统号(systems-index.md);改它等于改身份,须同步 ADR-020 Amd C ②");

            var errs = CameraSetModeRequesterGate.RunAll(".");
            Assert.That(errs, Is.Empty,
                "生产码出现单参 `SetMode` 或 `requesterId: -1` ⇒ 甲案退出语义静默失效。\n" +
                string.Join("\n", errs));
        }

        // ══════════════ S29-08 登记面 ══════════════

        /// <summary>
        /// `TR-camera-007` 在两份登记文件里**各至少一处**。
        /// <para>⚠️ 这条守的是「要求存在但 ID 不存在」:GDD 的 `R-2-5` 2026-11-01 就补了 29 两行,
        /// 而注册表当时没有任何 TR 覆盖它 —— 无 ID ⇒ 没有 story 能引用 ⇒ 无登记。</para>
        /// </summary>
        [Test]
        public void test_s29_08_trCamera007_registeredInRegistryAndIndex()
        {
            string root = FindRepoRoot();
            Assert.IsNotNull(root, "未能定位仓库根(向上找 docs/architecture/tr-registry.yaml)");

            string registry = Path.Combine(root, "docs", "architecture", "tr-registry.yaml");
            string index = Path.Combine(root, "docs", "architecture", "traceability-index.md");

            Assert.IsTrue(File.Exists(registry), $"注册表不存在:{registry}");
            Assert.IsTrue(File.Exists(index), $"追溯索引不存在:{index}");

            string regText = File.ReadAllText(registry);
            Assert.That(regText, Does.Contain("TR-camera-007"),
                "`tr-registry.yaml` 缺 TR-camera-007 —— 29 的档位请求要求存在但 ID 未登记(R-7.4)");

            string idxText = File.ReadAllText(index);
            Assert.That(idxText, Does.Contain("TR-camera-007"),
                "`traceability-index.md` 缺 TR-camera-007 行(R-7.4)");
        }

        // ══════════════ 辅助 ══════════════

        /// <summary>向上找含 `docs/architecture/tr-registry.yaml` 的目录(EditMode 的 CWD 是工程根 `unity/`)。</summary>
        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "docs", "architecture", "tr-registry.yaml")))
                    return dir.FullName;
            }
            return null;
        }

        /// <summary>
        /// 间谍 `ICameraRig` —— 只记录请求序列。
        /// <para>⚠️ `ModeValue` 可写但**不随 `SetMode` 变化**:本文件里「相机认不认」写不出来,
        /// 避免拿间谍的状态当证据(L2 才断真机 `Mode`)。</para>
        /// </summary>
        private sealed class SpyCameraRig : ICameraRig
        {
            private readonly List<(CameraMode Mode, int Requester)> _requests =
                new List<(CameraMode, int)>();

            /// <summary>被读取的当前档(只用于 `FirstPerson` 早退判据)。**不随请求变化**。</summary>
            public CameraMode ModeValue = CameraMode.Explore;

            public YawBasis YawBasis => new YawBasis(UnityEngine.Vector3.forward, UnityEngine.Vector3.right);
            public float Yaw => 0f;
            public float Pitch => 0f;
            public CameraMode Mode => ModeValue;

            /// <summary>单参重载 —— 生产**不得**走这里(门判红);记哨兵值以便断言意外调用。</summary>
            public void SetMode(CameraMode mode) => _requests.Add((mode, int.MinValue));

            public void SetMode(CameraMode mode, int requesterId) => _requests.Add((mode, requesterId));

            public void Tick(float deltaTime) { }

            public UnityEngine.Camera Camera => null;

            public string Describe() =>
                string.Join(",", _requests.Select(r =>
                    r.Requester == int.MinValue ? $"{r.Mode}:SINGLE_ARG" : $"{r.Mode}:{r.Requester}"));
        }
    }
}
