// camera-viewpoint/story-007 —— 档位表静态层(表 ↔ 代码)与记录器纪律的夹具。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读:① `docs/architecture/camera-mode-requests.yaml`(档位表真源)
//     ② `unity/Assets/**` 生产 `.cs`(记录器纪律判据 S7-07)
// 写:**零写盘**。
//
// ── 这层抓什么 / 抓不到什么(不许读成更强)─────────────────────────────────
// ✅ 抓:表里某行的请求方**不存在** / 没声明 `const int RequesterId` / 源码里连
//    `CameraMode.<档>` 都没出现 / evidence 文件不存在或没点名该行;
//    记录器被生产码**读**(纪律①被破坏)。
// ❌ 抓不到:**「声明了但真的不发」** —— `EmergencyChannelDriver` 的
//    `rig.SetMode(mode, RequesterId)` 里 mode 是形参,静态扫描无从判断实参。
//    那一半归 `Tests/PlayMode/Camera/camera_mode_request_matrix_test.cs`。
//
// 权威:`production/epics/camera-viewpoint/story-007-mode-request-coverage-matrix.md` R-7.2 / R-7.4

using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Camera
{
    /// <summary>story-007 L1:档位表覆盖(静态)+ 记录器纪律。</summary>
    [TestFixture]
    internal sealed class CameraModeTableGateTest
    {
        // ══════════════ S7-01 表存在且形状对 ══════════════

        [Test]
        public void test_s7_01_tableExists_andParsesToSixActiveRowsPlusExclusion()
        {
            string root = FindRepoRoot();
            Assert.IsNotNull(root, "未能定位仓库根(向上找 docs/architecture/camera-mode-requests.yaml)");

            string path = Path.Combine(root, "docs", "architecture", "camera-mode-requests.yaml");
            Assert.IsTrue(File.Exists(path),
                "档位表真源不存在 —— 表没了,「该发没发」就没有宾语(变量见 story-007 R-7.1)");

            string text = File.ReadAllText(path);
            Assert.That(text, Does.Contain("CMR-EX-011"),
                "11 的**具名除外行**必须留在表内(承 R-2-5 的 2026-09-21 裁定)。\n" +
                "抹掉它会让后来人读成「遗漏」并自行补行 —— 那等于把已裁的裁定静默改回未裁");
        }

        // ══════════════ S7-02 / 03 B1 + B2 + B3 ══════════════

        [Test]
        public void test_s7_02_03_gateReportsNoModeTableViolations()
        {
            var errs = CameraSetModeRequesterGate.RunModeRequestTable(".");
            Assert.That(errs, Is.Empty,
                "档位表 ↔ 代码的覆盖判据不通过:\n" + string.Join("\n", errs));
        }

        /// <summary>
        /// **空洞正控防护**:上面的判据若因为「找不到仓库根」而恒返回空,它会永远绿。
        /// 本条断言门**确实读到了表**(用一条必然不存在的目录路径反证它会给错)。
        /// </summary>
        [Test]
        public void test_s7_02_03_gateIsNotVacuouslyGreen()
        {
            var errs = CameraSetModeRequesterGate.RunModeRequestTable("/nonexistent-root-for-probe");
            Assert.That(errs, Is.Not.Empty,
                "门在找不到表时必须**报错**,而不是静默返回空 —— 否则上一条恒绿(空洞正控)");
        }

        // ══════════════ S7-07 记录器不参与判定 ══════════════

        /// <summary>
        /// 记录器只许有**一个写入点**(`CameraModeMachine.SetMode`),且生产码**零读点**。
        /// <para>⚠️ 读点若出现在生产码,记录器就从「采集面」变成了「判定输入」—— 那是第二真源。</para>
        /// </summary>
        [Test]
        public void test_s7_07_recorderIsWriteOnlyInProduction()
        {
            string root = FindRepoRoot();
            Assert.IsNotNull(root, "未能定位仓库根");

            string assets = Path.Combine(root, "unity", "Assets");
            Assert.IsTrue(Directory.Exists(assets), $"生产源码树不存在:{assets}");

            var writers = new List<string>();
            var readers = new List<string>();

            foreach (string path in Directory.EnumerateFiles(assets, "*.cs", SearchOption.AllDirectories))
            {
                string norm = path.Replace('\\', '/');
                if (norm.Contains("/Tests/")) continue;
                if (norm.EndsWith("/CameraModeRequestRecorder.cs")) continue;   // 定义处

                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (!line.Contains("CameraModeRequestRecorder.")) continue;

                    if (line.Contains(".Record(")) writers.Add($"{norm}:{i + 1}");
                    else if (line.Contains(".Snapshot(") || line.Contains("TotalRecorded") ||
                             line.Contains(".Clear("))
                        readers.Add($"{norm}:{i + 1}: {line.Trim()}");
                }
            }

            Assert.That(readers, Is.Empty,
                "生产码**读**了记录器 —— 违反 R-7.4 纪律①(记录器不得参与任何判定)。\n" +
                string.Join("\n", readers));

            Assert.That(writers.Count, Is.EqualTo(1),
                "记录器写入点应**恰 1 处**(`CameraModeMachine.SetMode`)。\n" +
                "⚠️ 0 处说明记录器根本没接上(矩阵夹具会因永远空而全红);" +
                ">1 处说明多了一条采集入口,须先确认它不构成第二真源。\n" +
                $"实测:{string.Join(", ", writers)}");
        }

        /// <summary>
        /// 记录器整类必须由条件编译包裹(**不进发布构建**)。
        /// ⚠️ 这是**源面**判据:它证明「有 `#if`」,不证明「Release 下真的编不出来」——
        /// 后者须变体编译实测,本卡未做(已在卡面「抓不到什么」之外,记入证伪轮说明)。
        /// </summary>
        [Test]
        public void test_s7_06_recorderIsBehindConditionalCompilation()
        {
            string root = FindRepoRoot();
            string path = Path.Combine(root, "unity", "Assets", "Gameplay.Presentation",
                "Diagnostics", "CameraModeRequestRecorder.cs");
            Assert.IsTrue(File.Exists(path), $"记录器不存在:{path}");

            string text = File.ReadAllText(path);
            Assert.That(text, Does.Contain("#if UNITY_EDITOR || DEVELOPMENT_BUILD"),
                "记录器必须由 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 包裹 —— 否则它会进发布构建");
            Assert.That(text, Does.Contain("#endif"), "条件编译块未闭合");
        }

        // ══════════════ 辅助 ══════════════

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "docs", "architecture",
                        "camera-mode-requests.yaml")))
                    return dir.FullName;
            }
            return null;
        }
    }
}
