// `MedConsRepoPaths` 的判据 —— 2026-10-30 · `TD-medcons-09` 结案件的一部分。
//
// 权威:
//   `TD-medcons-09` —— 「`LoadTable` 走 `dataPath/../../` 解析仓根,在 CI 检出布局变化时即红」
//   本 helper 的文件头「本条还掉什么 / 没还掉什么」
//
// ⚠️ 本文件不测「EditMode 能不能做 file I/O」(那是既成事实,登记为例外),只测:
//   ① 仓根**解析对**(锚点判据真的成立,而不是碰巧上跳两层);
//   ② 四个派生路径**都指向仓内真位置**;
//   ③ **找不到锚点时抛**,且报文点名锚点(负路径 —— 靠 `ResolveFrom` 注入起点才可测)。

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.MedConsequences
{
    /// <summary>`MedConsRepoPaths` 的解析判据(含负路径)。</summary>
    [TestFixture]
    public sealed class MedConsRepoPathsTest
    {
        // ══════════════════ 正向:解析对 ══════════════════

        [Test]
        public void test_repoRoot_actuallyContainsTheAnchor()
        {
            // 判据 = 「仓根下确有锚点文件」——不是「上跳了 N 层」。
            // 若解析实现被改成 `dataPath/../..` 之类的固定层数,该目录一旦不是仓根,本条转红。
            string root = MedConsRepoPaths.RepoRoot;
            Assert.That(File.Exists(Path.Combine(root, "assets", "data", "disease_registry.json")), Is.True,
                $"RepoRoot«{root}»下没有 assets/data/disease_registry.json —— 解析判据未成立");
        }

        [Test]
        public void test_repoRoot_isStableAcrossCalls()
        {
            Assert.That(MedConsRepoPaths.RepoRoot, Is.EqualTo(MedConsRepoPaths.RepoRoot),
                "两次调用须同值(缓存或确定性解析,不得随工作目录漂移)");
        }

        [Test]
        public void test_projectRoot_isTheParentOfDataPath()
        {
            string dataPath = UnityEngine.Application.dataPath;
            Assert.That(MedConsRepoPaths.ProjectRoot, Is.EqualTo(Path.GetDirectoryName(dataPath)),
                "工程根 = Application.dataPath 的父(`…/unity`)");
        }

        [Test]
        public void test_projectRoot_isInsideRepoRoot()
        {
            // 工程根必须是仓根的子目录 —— 否则两者不可能都成立(布局变了)。
            string rel = Path.GetRelativePath(MedConsRepoPaths.RepoRoot, MedConsRepoPaths.ProjectRoot);
            Assert.That(rel.StartsWith("..", StringComparison.Ordinal), Is.False,
                $"工程根«{MedConsRepoPaths.ProjectRoot}»不在仓根«{MedConsRepoPaths.RepoRoot}»之下");
        }

        [Test]
        public void test_dataFile_pointsAtRealAuthoringSource()
        {
            string p = MedConsRepoPaths.DataFile("disease_registry.json");
            Assert.That(p, Is.EqualTo(Path.Combine(MedConsRepoPaths.RepoRoot, "assets", "data", "disease_registry.json")));
            Assert.That(File.Exists(p), Is.True, "DataFile 须指向真实存在的作者态源");
        }

        [Test]
        public void test_cookedFile_pointsInsideAssetsDataCooked()
        {
            string p = MedConsRepoPaths.CookedFile("53_cost_table.cooked.bytes");
            Assert.That(p, Is.EqualTo(Path.Combine(MedConsRepoPaths.DataCookedDir, "53_cost_table.cooked.bytes")));
            Assert.That(p.StartsWith(UnityEngine.Application.dataPath, StringComparison.Ordinal), Is.True,
                "cooked 产物住 Assets/ 下(不是仓根)");
        }

        // ══════════════════ 负路径:找不到锚点 ⇒ 抛 ══════════════════

        [Test]
        public void test_resolveFrom_emptyStarts_throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => MedConsRepoPaths.ResolveFrom(Array.Empty<string>()));
            Assert.That(ex.Message, Does.Contain("assets/data/disease_registry.json"),
                "报文须点名锚点,否则读者不知道该补什么文件");
            Assert.That(ex.Message, Does.Contain("一个搜索起点都没有"),
                "空起点须与「搜过但没找到」在报文上可区分");
        }

        [Test]
        public void test_resolveFrom_directoryWithoutAnchor_throwsAndListsTried()
        {
            // ⚠️ 不写盘:用一个**不存在**的绝对路径。`DirectoryInfo` 不校验存在性,
            //    但 `.Parent` 会一路向上到根 —— 根上不会有 `assets/data/disease_registry.json`
            //    (仓不在文件系统根)。⇒ 必抛,且 tried 非空。
            string bogus = Path.Combine(Path.GetTempPath(), "medcons-no-such-anchor-" + Guid.NewGuid().ToString("N"));
            var ex = Assert.Throws<InvalidOperationException>(
                () => MedConsRepoPaths.ResolveFrom(new[] { bogus }));
            Assert.That(ex.Message, Does.Contain("已向上搜索"),
                "搜过但没找到 ⇒ 报文须列出已试目录(与空起点的报文区分)");
            Assert.That(ex.Message, Does.Not.Contain("一个搜索起点都没有"));
        }

        [Test]
        public void test_resolveFrom_allStartsInvalid_throwsNotArgumentException()
        {
            // 非法路径(含 NUL)⇒ **跳过**而不是把 `ArgumentException` 漏给调用方;
            // 全部起点都非法 ⇒ 退化为空 tried ⇒ 仍抛 `InvalidOperationException`(单一异常类型)。
            var ex = Assert.Throws<InvalidOperationException>(
                () => MedConsRepoPaths.ResolveFrom(new[] { "bad\0path" }));
            Assert.That(ex.Message, Does.Contain("一个搜索起点都没有"));
        }

        // ══════════════════ 反向:锚点必须真的是「内容特征」 ══════════════════

        [Test]
        public void test_repoRoot_isNotSimplyTwoLevelsUp()
        {
            // 钉住「解析靠锚点、不靠层数」:显式断言仓根与 `dataPath/../../` 的关系
            // **可以**相等(当前布局如此),但判据是上一条的锚点存在性 ——
            // 本条只记录当前布局,若将来布局变了本条可改,而锚点判据不用改。
            string twoUp = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", ".."));
            Assert.That(MedConsRepoPaths.RepoRoot, Is.EqualTo(twoUp),
                "当前布局下仓根 == dataPath/../../ —— 本条是**当前布局的记录**,不是解析判据;" +
                "解析判据见 test_repoRoot_actuallyContainsTheAnchor");
        }
    }
}
