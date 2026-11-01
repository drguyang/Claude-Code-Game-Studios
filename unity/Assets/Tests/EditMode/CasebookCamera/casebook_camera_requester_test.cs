// casebook/story-006 —— 39 的档位请求载体(`CasebookCameraRequester`)夹具。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读:① `unity/Assets/**` 生产 `.cs`(`CameraSetModeRequesterGate.RunAll(".")` —— S39-05;
//     另有一处**源码归属扫描** —— S39-07b,见下)
//     ② `docs/architecture/tr-registry.yaml`(**零读** —— 本卡不新增 TR)
// 写:**零写盘**。
//
// ── 为什么住在 `Tests/EditMode/CasebookCamera/` 而不是 `Tests/EditMode/Casebook/` ──
// `Tests/EditMode/Casebook/` 自带 `Casebook.Tests.asmdef`,其引用集只有
// `{GUID, Gameplay.UI}` —— 够不着 `Gameplay.Presentation`(`CasebookCameraRequester` 的家)
// 与 `EditorTools.Gates`。为了不为了一个夹具去改那个 asmdef 的引用面,
// 本件落在 `Tests/EditMode/` 根(`EditMode.asmdef`,引用集已含两者)。
// ⚠️ 目录名刻意与 `Casebook/` 错开 —— 子目录会继承**最近的** asmdef,同名会误落进旧的那个。
//
// ── 测什么 / 不测什么 ────────────────────────────────────────────────────────
// ✅ 测:发没发、发的哪个档、requesterId 是不是 39、VR / 机位缺席时是否**不发**、
//    `Casebook` 档的生产请求点是否**只有本载体一处**。
// ❌ 不测:**模态边沿**(在 `SkeuoRuntimeDriver.NotifyCasebookCamera`)与
//    **生产启动序真的接上了** —— 两者归 L2 PlayMode。
//
// 权威来源:
//   `design/gdd/casebook.md` 规则五「摊开与相机»
//   `design/gdd/camera-and-viewpoint.md` `R-2-5`(39 → `Casebook` / 39 → `Explore`)
//   `docs/architecture/adr-020-player-controller-and-camera.md` §五 / Amendment C ①②
//   `production/epics/casebook/story-006-camera-mode-intent.md`

using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Presentation.Camera;
using DaYiJingCheng.Gameplay.Presentation.Casebook;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.CasebookCamera
{
    /// <summary>casebook/story-006 L1:39 的档位意图请求序列与归属。</summary>
    [TestFixture]
    internal sealed class CasebookCameraRequesterTest
    {
        private SpyCameraRig _rig;
        private CasebookCameraRequester _requester;

        [SetUp]
        public void SetUp()
        {
            _rig = new SpyCameraRig();
            _requester = new CasebookCameraRequester(() => _rig);
        }

        // ══════════════ S39-01 / S39-02 两个方向 ══════════════

        [Test]
        public void test_s39_01_open_requestsCasebookWithRequester39()
        {
            _requester.Request(open: true);

            Assert.That(_rig.Describe(), Is.EqualTo("Casebook:39"),
                "开册 ⇒ 恰一条 (Casebook, requesterId: 39)(R-2-5 · AC-39-07)");
        }

        [Test]
        public void test_s39_02_close_requestsExploreWithRequester39()
        {
            _requester.Request(open: false);

            Assert.That(_rig.Describe(), Is.EqualTo("Explore:39"),
                "合册 ⇒ 恰一条 (Explore, 39) —— 否则相机会**卡在脉案档**(甲案「同一请求方可降档」)");
        }

        [Test]
        public void test_s39_02_bothDirections_useSameRequester()
        {
            _requester.Request(open: true);
            _requester.Request(open: false);

            Assert.That(_rig.Describe(), Is.EqualTo("Casebook:39,Explore:39"),
                "开 / 合两个方向的 requesterId **必须同源** —— 不是 39 发的 Explore 降不了 39 开的档");
        }

        // ══════════════ S39-03 VR 全禁 ══════════════

        [Test]
        public void test_s39_03_firstPersonInPlace_zeroRequests_bothDirections()
        {
            _rig.ModeValue = CameraMode.FirstPerson;

            _requester.Request(open: true);
            _requester.Request(open: false);

            Assert.That(_rig.Describe(), Is.Empty,
                "`FirstPerson` 在位 ⇒ **零请求**(头显驱动、不经本链;平面近景档不得平移到 VR)\n" +
                "⚠️ 这不是「换成别的档」,是**什么都不发**");
        }

        [Test]
        public void test_s39_03_exploreInPlace_stillRequests()
        {
            // 负控的反面:非 VR 档时不得被早退吞掉(否则 S39-03 可能是「恒不发」的假绿)。
            _rig.ModeValue = CameraMode.Explore;
            _requester.Request(open: true);
            Assert.That(_rig.Describe(), Is.EqualTo("Casebook:39"), "非 VR 档 ⇒ 照常发");
        }

        // ══════════════ S39-04 机位缺席 ══════════════

        [Test]
        public void test_s39_04_nullRig_silentNoThrow()
        {
            var absent = new CasebookCameraRequester(() => null);

            Assert.DoesNotThrow(() => absent.Request(open: true),
                "机位缺席(拆卸段 / 启动序早期)⇒ 静默,不抛");
            Assert.DoesNotThrow(() => absent.Request(open: false));
            Assert.That(_rig.Describe(), Is.Empty, "不存在的机位不会收到任何请求");
            Assert.IsTrue(absent.HasCameraRig, "访问器本身仍在 ⇒ HasCameraRig = true(是机位没了,不是没绑)");
        }

        [Test]
        public void test_s39_04_nullAccessor_rejectedAtConstruction()
        {
            Assert.Throws<System.ArgumentNullException>(() => new CasebookCameraRequester(null),
                "访问器为 null = 装配遗漏 ⇒ 构造期 fail-loud");
        }

        // ══════════════ S39-05 系统号单一出处 + 机械门 ══════════════

        [Test]
        public void test_s39_05_requesterId_isThirtyNine_andGateClean()
        {
            Assert.AreEqual(39, CasebookCameraRequester.RequesterId,
                "39 = 脉案的系统号(systems-index.md);改它等于改身份,须同步 ADR-020 Amd C ②");

            var errs = CameraSetModeRequesterGate.RunAll(".");
            Assert.That(errs, Is.Empty,
                "生产码出现单参 `SetMode` 或 `requesterId: -1` ⇒ 甲案退出语义静默失效。\n" +
                string.Join("\n", errs));
        }

        // ══════════════ S39-07b 归属:Casebook 档的生产请求点只有本载体 ══════════════

        /// <summary>
        /// `CameraMode.Casebook` 作为 **`SetMode` 的实参**出现,生产侧**只允许**在
        /// `CasebookCameraRequester.cs` 一处。
        /// <para>⚠️ 这条守的是 GDD 的「39 = `Casebook` 档唯一请求方」:相机的优先级表、
        /// 冻结判据里也会出现 `CameraMode.Casebook`,所以判据必须**同时**要求它与 `.SetMode(`
        /// 同行,否则会把那些引用误判成请求点。</para>
        /// <para>⚠️ 原文的「39 是**两档**(Casebook/Explore)唯一请求方」**已作废** ——
        /// `Explore` 由 10 / 29 / 39 三方各发一条(`R-2-5` 现况)。本判据只认 `Casebook`。</para>
        /// </summary>
        [Test]
        public void test_s39_07b_casebookModeRequestedOnlyByThisRequester()
        {
            string root = FindRepoRoot();
            Assert.IsNotNull(root, "未能定位仓库根(向上找 docs/architecture/tr-registry.yaml)");

            string assets = Path.Combine(root, "unity", "Assets");
            Assert.IsTrue(Directory.Exists(assets), $"生产源码树不存在:{assets}");

            var offenders = new List<string>();
            int hits = 0;

            foreach (string path in Directory.EnumerateFiles(assets, "*.cs", SearchOption.AllDirectories))
            {
                string norm = path.Replace('\\', '/');
                if (norm.Contains("/Tests/")) continue;

                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (!line.Contains(".SetMode(")) continue;
                    if (!line.Contains("CameraMode.Casebook")) continue;

                    hits++;
                    if (!norm.EndsWith("/CasebookCameraRequester.cs"))
                        offenders.Add($"{norm}:{i + 1}: {line.Trim()}");
                }
            }

            Assert.That(offenders, Is.Empty,
                "`Casebook` 档的生产请求点**只允许**在 `CasebookCameraRequester.cs`(39 = 唯一请求方)。\n" +
                "⚠️ 若这是有意新增的第二个请求方,须先改 GDD 的 `R-2-5` 表 —— 不得在代码里静默扩大。\n" +
                string.Join("\n", offenders));

            Assert.That(hits, Is.EqualTo(1),
                "本判据若无命中即为**空洞正控**(恒绿)—— 预期恰 1 处(本载体)。\n" +
                "⚠️ 命中 0 说明扫描面失效(路径/关键字改了),不是「没有违例」。");
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
