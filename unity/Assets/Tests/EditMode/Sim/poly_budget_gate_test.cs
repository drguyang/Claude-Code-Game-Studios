// 3D 面数门判据(§8.5 + §8.10.6.3 `budget_tier`)· 2026-11-12 立
//
// 本件只测**纯逻辑**(解析 + 判决),不读仓库文件、不写盘 —— 三角面查找走注入委托,
// 故不需要真资产在场。真实资产侧的「测量」由构建日志的 Notes 行承载
// (2026-11-12 起两件试件已接真 FBX,见 impl-poly-budget-gate 证据件 §11)。

using System.Collections.Generic;
using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    [TestFixture]
    public class PolyBudgetGateTest
    {
        private static PolyBudgetGates.Declaration Decl(
            string family, string tier = "normal", string asset = PolyBudgetGates.PendingAsset, string scene = null,
            string assetHome = null)
        {
            return new PolyBudgetGates.Declaration
            {
                SpecRelPath = "design/assets/specs/x.md",
                Line = 1,
                Family = family,
                Tier = tier,
                Asset = asset,
                Scene = scene,
                AssetHome = assetHome,
            };
        }

        private static PolyBudgetGates.Report Eval(int tris, params PolyBudgetGates.Declaration[] decls)
        {
            return PolyBudgetGates.Evaluate(decls, _ => tris);
        }

        // ── P5 上界 ──

        [Test]
        public void test_polyBudget_normalCeiling_exactPasses()
        {
            var r = Eval(8000, Decl("building-module", asset: "Assets/Art/x.fbx"));
            Assert.That(r.Errors, Is.Empty, string.Join("\n", r.Errors));
            Assert.That(r.Measured, Is.EqualTo(1));
        }

        [Test]
        public void test_polyBudget_normalCeiling_oneOverFails()
        {
            var r = Eval(8001, Decl("building-module", asset: "Assets/Art/x.fbx"));
            Assert.That(r.Errors.Count, Is.EqualTo(1));
            // 报文用 N0 千分位 ⇒ "8,001";按字面量断言(不引用被测常量,免自指)
            StringAssert.Contains("8,001", r.Errors[0]);
            StringAssert.Contains("8,000", r.Errors[0]);
        }

        [Test]
        public void test_polyBudget_heroCeiling_exactPasses()
        {
            var r = Eval(20000, Decl("building-module", "hero", "Assets/Art/x.fbx", "s1"));
            Assert.That(r.Errors, Is.Empty, string.Join("\n", r.Errors));
        }

        [Test]
        public void test_polyBudget_heroCeiling_oneOverFails()
        {
            var r = Eval(20001, Decl("building-module", "hero", "Assets/Art/x.fbx", "s1"));
            Assert.That(r.Errors.Count, Is.EqualTo(1));
        }

        // ── 实测读数(Notes):只记录、不判决 ──

        [Test]
        public void test_polyBudget_measuredDeclaration_emitsNote()
        {
            // 没有这一行,「实测 2 / 绿」就只剩「都 ≤ 上界」,报不出数字 ⇒ 数字不可复核
            var r = Eval(20000, Decl("building-module", "hero", "Assets/Art/x.fbx", "s1"));
            Assert.That(r.Notes.Count, Is.EqualTo(1), string.Join("\n", r.Notes));
            StringAssert.Contains("20,000", r.Notes[0]);
            StringAssert.Contains("100.0%", r.Notes[0]);
        }

        [Test]
        public void test_polyBudget_pendingDeclaration_emitsNoNote()
        {
            // Notes 的语义是「量到了」,不是「声明了」—— pending 走了 Warning,不得混进 Notes
            var r = Eval(0, Decl("building-module"));
            Assert.That(r.Notes, Is.Empty);
            Assert.That(r.Warnings.Count, Is.EqualTo(1));
        }

        // ── P9/P10 第三态:按策略不入库 ──

        [Test]
        public void test_polyBudget_localOnly_missing_isWarningNotError()
        {
            var r = PolyBudgetGates.Evaluate(
                new[] { Decl("building-module", "hero", "Assets/Art/x.fbx", "s1", "local-only") }, _ => null);
            Assert.That(r.Errors, Is.Empty, string.Join("\n", r.Errors));
            Assert.That(r.LocalAbsent, Is.EqualTo(1));
            Assert.That(r.Measured, Is.EqualTo(0));
            // 必须说清「未测 ≠ 通过」,否则这条会被读成「绿了」
            StringAssert.Contains("未测", r.Warnings[0]);
            StringAssert.Contains("不是通过", r.Warnings[0]);
        }

        [Test]
        public void test_polyBudget_localOnly_presentOverCeiling_isError()
        {
            // 这条是第三态的**反免检**判据:资产在盘上时,local-only 不减免任何判决
            var r = PolyBudgetGates.Evaluate(
                new[] { Decl("building-module", "hero", "Assets/Art/x.fbx", "s1", "local-only") }, _ => 20001);
            Assert.That(r.Errors.Count, Is.EqualTo(1));
            Assert.That(r.LocalAbsent, Is.EqualTo(0));
            Assert.That(r.Measured, Is.EqualTo(1));
        }

        [Test]
        public void test_polyBudget_localOnly_withPending_isError()
        {
            // 两个「缺席」口径叠在一件资产上 ⇒ 谁也说不清它到底量没量过
            var r = PolyBudgetGates.Evaluate(
                new[] { Decl("building-module", asset: PolyBudgetGates.PendingAsset, assetHome: "local-only") }, _ => 0);
            Assert.That(r.Errors.Count, Is.EqualTo(1));
            Assert.That(r.Pending, Is.EqualTo(0));
            Assert.That(r.LocalAbsent, Is.EqualTo(0));
        }

        [Test]
        public void test_polyBudget_repoHome_missing_isStillError()
        {
            // 缺省 = repo:缺席必须仍然 fail-closed。这条防「加了第三态把 P6 弄松了」
            var r = PolyBudgetGates.Evaluate(
                new[] { Decl("building-module", asset: "Assets/Art/x.fbx") }, _ => null);
            Assert.That(r.Errors.Count, Is.EqualTo(1));
            Assert.That(r.LocalAbsent, Is.EqualTo(0));
        }

        // ── P3 hero 只对建筑模块 ──

        [Test]
        public void test_polyBudget_heroOnInteractiveProp_isError()
        {
            var r = Eval(6000, Decl("prop-interactive", "hero", "Assets/Art/x.fbx", "s1"));
            Assert.That(r.Errors.Count, Is.EqualTo(1));
            StringAssert.Contains("hero", r.Errors[0]);
        }

        // ── P4 名额 ──

        [Test]
        public void test_polyBudget_heroQuota_fourthInSameSceneFails()
        {
            var r = PolyBudgetGates.Evaluate(new[]
            {
                Decl("building-module", "hero", "Assets/Art/a.fbx", "s1"),
                Decl("building-module", "hero", "Assets/Art/b.fbx", "s1"),
                Decl("building-module", "hero", "Assets/Art/c.fbx", "s1"),
                Decl("building-module", "hero", "Assets/Art/d.fbx", "s1"),
            }, _ => 1000);

            Assert.That(r.Errors.Count, Is.EqualTo(1));
            StringAssert.Contains("hero 件数 4", r.Errors[0]);
        }

        [Test]
        public void test_polyBudget_heroQuota_threePerSceneIsFine()
        {
            var r = PolyBudgetGates.Evaluate(new[]
            {
                Decl("building-module", "hero", "Assets/Art/a.fbx", "s1"),
                Decl("building-module", "hero", "Assets/Art/b.fbx", "s1"),
                Decl("building-module", "hero", "Assets/Art/c.fbx", "s1"),
                Decl("building-module", "hero", "Assets/Art/d.fbx", "s2"),
            }, _ => 1000);

            Assert.That(r.Errors, Is.Empty, string.Join("\n", r.Errors));
        }

        [Test]
        public void test_polyBudget_heroWithoutScene_isError()
        {
            var r = Eval(1000, Decl("building-module", "hero", "Assets/Art/x.fbx", null));
            Assert.That(r.Errors.Count, Is.EqualTo(1));
            StringAssert.Contains("scene", r.Errors[0]);
        }

        // ── P5 / P6 资产在场性 ──

        [Test]
        public void test_polyBudget_pendingAsset_isWarningNotError()
        {
            var r = PolyBudgetGates.Evaluate(new[] { Decl("building-module", "hero", scene: "s1") }, _ => 999999);

            Assert.That(r.Errors, Is.Empty, string.Join("\n", r.Errors));
            Assert.That(r.Pending, Is.EqualTo(1));
            Assert.That(r.Measured, Is.EqualTo(0), "pending 不许被算成实测");
            Assert.That(r.Warnings.Count, Is.EqualTo(1));
            StringAssert.Contains("未测", r.Warnings[0]);
        }

        [Test]
        public void test_polyBudget_missingAsset_isError()
        {
            var r = PolyBudgetGates.Evaluate(new[] { Decl("building-module", asset: "Assets/Art/gone.fbx") }, _ => null);

            Assert.That(r.Errors.Count, Is.EqualTo(1));
            StringAssert.Contains("fail-closed", r.Errors[0]);
        }

        // ── P7 / P8 ──

        [Test]
        public void test_polyBudget_duplicateAsset_isError()
        {
            var r = PolyBudgetGates.Evaluate(new[]
            {
                Decl("building-module", asset: "Assets/Art/x.fbx"),
                Decl("building-module", asset: "Assets/Art/x.fbx"),
            }, _ => 100);

            Assert.That(r.Errors.Count, Is.EqualTo(1));
            StringAssert.Contains("重复声明", r.Errors[0]);
        }

        [Test]
        public void test_polyBudget_emptyInput_isError()
        {
            var r = PolyBudgetGates.Evaluate(new PolyBudgetGates.Declaration[0], _ => 0);
            Assert.That(r.Errors.Count, Is.EqualTo(1));
            StringAssert.Contains("扫描面为 0", r.Errors[0]);
        }

        // ── 解析 ──

        [Test]
        public void test_polyBudget_parser_readsBudgetBlockOnly()
        {
            const string md = @"# Spec

散文里写 budget: 8000 不算数据。

```budget
# 注释
asset: pending
family: building-module
tier: hero
scene: city-ruins
```

```csharp
asset: not-a-budget-block
```
";
            var errs = new List<string>();
            var list = PolyBudgetGates.ParseSpec("specs/x.md", md, errs);

            Assert.That(errs, Is.Empty, string.Join("\n", errs));
            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0].Family, Is.EqualTo("building-module"));
            Assert.That(list[0].Tier, Is.EqualTo("hero"));
            Assert.That(list[0].Scene, Is.EqualTo("city-ruins"));
            Assert.That(list[0].Asset, Is.EqualTo("pending"));
        }

        [Test]
        public void test_polyBudget_parser_unknownKey_isError()
        {
            const string md = "```budget\nasset: pending\nfamly: building-module\n```\n";
            var errs = new List<string>();
            PolyBudgetGates.ParseSpec("specs/x.md", md, errs);

            Assert.That(errs.Count, Is.EqualTo(1));
            StringAssert.Contains("未知键", errs[0]);
        }

        [Test]
        public void test_polyBudget_parser_defaultsTierToNormal()
        {
            const string md = "```budget\nasset: pending\nfamily: prop-decor\n```\n";
            var errs = new List<string>();
            var list = PolyBudgetGates.ParseSpec("specs/x.md", md, errs);

            Assert.That(errs, Is.Empty);
            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0].Tier, Is.EqualTo("normal"));
        }

        [Test]
        public void test_polyBudget_parser_defaultsAssetHomeToRepo()
        {
            const string md = "```budget\nasset: pending\nfamily: prop-decor\n```\n";
            var errs = new List<string>();
            var list = PolyBudgetGates.ParseSpec("specs/x.md", md, errs);

            Assert.That(errs, Is.Empty);
            Assert.That(list[0].AssetHome, Is.EqualTo("repo"));
            Assert.That(list[0].IsLocalOnly, Is.False);
        }

        [Test]
        public void test_polyBudget_parser_unknownAssetHome_isError()
        {
            // 拼错的 `local_only` 若被静默当成 repo,新机器上会把「按策略缺席」报成「文件丢失」
            const string md = "```budget\nasset: Assets/Art/x.fbx\nfamily: prop-decor\nasset_home: local_only\n```\n";
            var errs = new List<string>();
            var list = PolyBudgetGates.ParseSpec("specs/x.md", md, errs);

            Assert.That(errs.Count, Is.EqualTo(1));
            StringAssert.Contains("asset_home", errs[0]);
            Assert.That(list, Is.Empty);
        }

        [Test]
        public void test_polyBudget_parser_unknownAssetHome_terminatesAndAdvances()
        {
            // 回归锁(2026-11-12):早期实现里未知 asset_home 的分支用了 `continue`,
            // 它跳过 `i = j + 1` ⇒ 同一 ```budget 块被无限重解析,每轮 errors.Add 一个新字符串
            // ⇒ 本机 23 条全跑时内存 ~2 GB/分钟爆炸、单测 0 执行(v12…v21 卡死根因)。
            // 本夹具用「错误块在前、合法块在后」的形态:若 i 不前进,合法块永不可达 ⇒ list.Count ≠ 1 当场红;
            // 若有人把 `goto Advance` 改回 `continue`,本夹具会以**死循环超时**形式红(不是绿)。
            const string md =
                "```budget\n" +
                "asset: Assets/Art/x.fbx\n" +
                "family: prop-decor\n" +
                "asset_home: local_only\n" +   // 未知值(故意的)
                "```\n" +
                "\n" +
                "```budget\n" +
                "asset: pending\n" +
                "family: building-module\n" +
                "```\n";
            var errs = new List<string>();
            var list = PolyBudgetGates.ParseSpec("specs/x.md", md, errs);

            Assert.That(errs.Count, Is.EqualTo(1), string.Join("\n", errs));
            StringAssert.Contains("asset_home", errs[0]);
            // 关键判据:第二个块必须被解析到 —— 它证明 i 前进了
            Assert.That(list.Count, Is.EqualTo(1), "错误块之后的合法块必须仍被解析(i 必须前进)");
            Assert.That(list[0].Family, Is.EqualTo("building-module"));
        }
    }
}
