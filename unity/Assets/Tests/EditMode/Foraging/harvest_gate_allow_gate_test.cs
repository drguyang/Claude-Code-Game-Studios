// S22-9(2026-11-01)· `HarvestGateAllowGate` 的自检夹具
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里
// ─────────────────────────────────────────────────────────────────────────────
// 读:`unity/Assets/**` 全部生产 `.cs`(经 `HarvestGateAllowGate.RunAll(".")`)——
//   必须读**真树**,否则这条测的只是「扫一个我造的目录」,证不了生产码清洁。
// 写:**只写 `Path.GetTempPath()`**(合成目录,用于证扫描器真的会红)。
//
// ── 为什么需要这三条 ──────────────────────────────────────────────────────
// 一条门若从没红过,「它有门」就只是自述(假绿形态 F6)。这里用**合成目录**让它必须红,
// 从而把「扫描器工作」这件事变成可复算的判据;真树那条则钉「今天生产码是干净的」。

using System.IO;
using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Foraging
{
    /// <summary>`HarvestGateAllowGate`:真树清洁 + 扫描器会红 + 注释不误报。</summary>
    [TestFixture]
    public sealed class HarvestGateAllowGateTest
    {
        [Test]
        public void test_productionTree_hasNoHarvestGateAllow()
        {
            var errs = HarvestGateAllowGate.RunAll(".");
            Assert.That(errs, Is.Empty,
                "生产码出现 `HarvestGate.Allow` —— 语义是跳过整道采前闸。\n" +
                string.Join("\n", errs));
        }

        [Test]
        public void test_syntheticProductionFile_withAllow_isReported()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "harvest-gate-probe-" + System.Guid.NewGuid().ToString("N"));
            string dir = Path.Combine(root, "Assets");
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "Bad.cs"),
                    "class Bad { void M() { var g = HarvestGate.Allow; } }");

                var errs = HarvestGateAllowGate.RunAll(root);

                Assert.That(errs, Is.Not.Empty,
                    "合成生产码里有 `HarvestGate.Allow` 却没报 —— 扫描器是死的");
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch (IOException) { }
            }
        }

        [Test]
        public void test_commentMention_isNotReported()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "harvest-gate-probe-" + System.Guid.NewGuid().ToString("N"));
            string dir = Path.Combine(root, "Assets");
            Directory.CreateDirectory(dir);
            try
            {
                // 注释里解释「为什么禁止 HarvestGate.Allow」不算违规 —— 否则本门的
                // 文档会把本门弄红(自己把自己判死)。
                File.WriteAllText(Path.Combine(dir, "Doc.cs"),
                    "// 生产不得传 HarvestGate.Allow\nclass Doc { }");

                var errs = HarvestGateAllowGate.RunAll(root);

                Assert.That(errs, Is.Empty,
                    "注释里的提及被当成违规 —— 剥离没生效,本门会被自己的文档弄红");
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch (IOException) { }
            }
        }

        /// <summary>测试目录**豁免**:夹具需要构造全放行来测编排器自身分支。</summary>
        [Test]
        public void test_testDirectory_isExempt()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "harvest-gate-probe-" + System.Guid.NewGuid().ToString("N"));
            string dir = Path.Combine(root, "Assets", "Tests", "EditMode");
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "Fixture.cs"),
                    "class Fixture { void M() { var g = HarvestGate.Allow; } }");

                var errs = HarvestGateAllowGate.RunAll(root);

                Assert.That(errs, Is.Empty,
                    "`/Tests/` 被扫了 —— 夹具构造全放行是合法的,不该红");
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch (IOException) { }
            }
        }
    }
}
