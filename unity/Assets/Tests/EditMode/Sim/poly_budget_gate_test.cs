// 3D 面数门判据(§8.5 + §8.10.6.3 `budget_tier`)· 2026-11-12 立
//
// 本件只测**纯逻辑**(解析 + 判决),不读仓库文件、不写盘 —— 三角面查找走注入委托,
// 故不需要真资产在场。真实资产侧的「测量」在这一轮仍是 NOT-RUN(两件 spec 均 `asset: pending`)。

using System.Collections.Generic;
using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    [TestFixture]
    public class PolyBudgetGateTest
    {
        private static PolyBudgetGates.Declaration Decl(
            string family, string tier = "normal", string asset = PolyBudgetGates.PendingAsset, string scene = null)
        {
            return new PolyBudgetGates.Declaration
            {
                SpecRelPath = "design/assets/specs/x.md",
                Line = 1,
                Family = family,
                Tier = tier,
                Asset = asset,
                Scene = scene,
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
    }
}
