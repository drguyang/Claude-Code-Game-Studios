// 权威来源:
//   `.claude/rules/test-standards.md` 第 3 条(Unit tests must not depend on external state)
//   · `retro-sprint-14-2026-10-18.md` §Action Items #4(「测试/工具不得写仓库」立机械门)
//   · 被测件 `DaYiJingCheng.EditorTools.Gates.FsWriteBoundaryGate`(S16-4,2026-10-20)
//
// ⚠️ **本文件验的是「门有没有判别力」,不是「门有没有写出来」** ——
//   承 TD-epic-02 的最贵教训:**「改坏生产码 ⇒ 断言转红」**,不是「删断言 ⇒ 变绿」。
//   故本文件三面齐备:
//     ① 正例面:R1 / R2 各能报红(喂合成源码,不碰文件系统);
//     ② 判别力面:分别停用 `IsStringLiteral` / `FindRepoAnchors` / 声明查找,
//        对应断言**必须转红**的形态由 `production/qa/evidence/impl-s16-4-*.md` 的
//        证伪轮 F1/F2/F3 记录 —— 本文件只钉**当下的绿**,判别力由变异轮证明;
//     ③ 真树面:`RunAll(".")` 对真测试树零红(修完 u1 / story007 的 `Assets/` 候选后)。
//
// ⚠️ **纯函数优先**:除真树那一条外,全部断言喂字符串源码 ——
//   本门存在的理由就是「测试别碰文件系统」,它的测试更不该碰。

using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Unit.DataPipeline
{
    [TestFixture]
    public class FsWriteBoundaryGateTest
    {
        // ══════════ R1:写目标 = 相对字面量 ══════════

        [Test]
        public void test_r1_writeTargetIsRelativeLiteral_isRed()
        {
            const string src = "class T { void M() { File.WriteAllText(\"docs/x.md\", \"y\"); } }";
            List<string> errs = FsWriteBoundaryGate.Scan("Assets/Tests/EditMode/Fake.cs", src);
            Assert.That(errs.Exists(e => e.Contains("R1")), Is.True,
                "相对字面量目标必须报 R1 —— 相对路径按 cwd 解析 = 仓库落点");
        }

        [Test]
        public void test_r1_verbatimLiteralTarget_isRed()
        {
            const string src = "class T { void M() { File.WriteAllText(@\"Assets/x.txt\", \"y\"); } }";
            List<string> errs = FsWriteBoundaryGate.Scan("Assets/Tests/EditMode/Fake.cs", src);
            Assert.That(errs.Exists(e => e.Contains("R1")), Is.True, "`@\"...\"` 同属字面量");
        }

        [Test]
        public void test_r1_combineExpression_isNotLiteral_noR1()
        {
            const string src = "class T { void M() { File.WriteAllText(Path.Combine(p, \"x\"), \"y\"); } }";
            List<string> errs = FsWriteBoundaryGate.Scan("Assets/Tests/EditMode/Fake.cs", src);
            Assert.That(errs.Exists(e => e.Contains("R1")), Is.False,
                "表达式目标不是字面量 —— R1 的判据面是「直接就是字面量」,不得外扩");
        }

        // ══════════ R2:锚点 × 写盘 ⇒ 须声明 ══════════

        [Test]
        public void test_r2_anchorPlusWrite_withoutDeclaration_isRed()
        {
            const string src =
                "class T { void M() { var p = Path.Combine(Application.dataPath, \"x\");" +
                " File.WriteAllText(p, \"y\"); } }";
            List<string> errs = FsWriteBoundaryGate.Scan("Assets/Tests/EditMode/Unlisted.cs", src);
            Assert.That(errs.Exists(e => e.Contains("R2")), Is.True,
                "含仓库锚点又含写盘 ⇒ 必须具名声明,无声明 = 红");
        }

        [Test]
        public void test_r2_anchorPlusWrite_withDeclaration_isGreen()
        {
            string suffix = FsWriteBoundaryGate.Declarations[0].FileSuffix;
            const string src =
                "class T { void M() { var p = Path.Combine(Application.dataPath, \"..\");" +
                " File.WriteAllText(\"z\", \"y\"); } }";
            List<string> errs = FsWriteBoundaryGate.Scan("Assets/Tests/" + suffix, src);
            Assert.That(errs.Exists(e => e.Contains("R2")), Is.False,
                "已具名声明的文件不再报 R2(声明是**审查结论**,不是自动豁免的挡箭牌)");
        }

        [Test]
        public void test_r2_anchorWithNoWrite_isGreen()
        {
            const string src = "class T { string M() => Application.dataPath; }";
            List<string> errs = FsWriteBoundaryGate.Scan("Assets/Tests/EditMode/Unlisted.cs", src);
            Assert.That(errs, Is.Empty, "只读锚点不构成写盘风险 —— R2 是共存判据,不是锚点禁令");
        }

        [Test]
        public void test_r2_writeWithNoAnchor_isGreen()
        {
            const string src =
                "class T { void M() { var p = Path.Combine(Path.GetTempPath(), \"x\");" +
                " File.WriteAllText(p, \"y\"); } }";
            List<string> errs = FsWriteBoundaryGate.Scan("Assets/Tests/EditMode/Unlisted.cs", src);
            Assert.That(errs, Is.Empty, "写 temp 是合法形态,门不得误伤 —— 误报会逼出整目录白名单");
        }

        [Test]
        public void test_r2_allThreeAnchors_areRecognized()
        {
            foreach (string anchor in FsWriteBoundaryGate.RepoCodeAnchors)
            {
                string src = "class T { string M() => " + anchor + "; }";
                string[] found = FsWriteBoundaryGate.FindRepoAnchors(src);
                Assert.That(found, Does.Contain(anchor), $"锚点 `{anchor}` 未被识别");
            }
        }

        // ══════════ 注释 / 字符串字面量:两处已知误报源 ══════════

        [Test]
        public void test_comment_mentioningApi_notCounted()
        {
            const string src =
                "// 历史注:曾用 Directory.CreateDirectory(repoRoot/assets/audio) 与 File.WriteAllText\n" +
                "class T { }";
            Assert.That(FsWriteBoundaryGate.FindWriteSites(src), Is.Empty,
                "注释里的 API 名不是调用点 —— S14-6 的历史记载正住在 pipeline_gate_test.cs 的注释里");
        }

        [Test]
        public void test_stringLiteralFixture_invokingApi_notCounted()
        {
            const string src =
                "class T { const string F = \"class X { void M() { File.WriteAllText(\\\"a\\\", \\\"b\\\"); } }\"; }";
            Assert.That(FsWriteBoundaryGate.FindWriteSites(src), Is.Empty,
                "夹具字符串里的调用不是真调用 —— overrides_sidecar_test 的 WriteSurface 负向夹具即此形态");
        }

        [Test]
        public void test_allDeclaredApiNames_areMatched()
        {
            foreach (string api in FsWriteBoundaryGate.WriteApiNames)
            {
                string src = "class T { void M() { " + api + "(a, b); } }";
                List<FsWriteBoundaryGate.WriteSite> sites = FsWriteBoundaryGate.FindWriteSites(src);
                Assert.That(sites.Count, Is.EqualTo(1), $"API `{api}` 未被识别(名单与正则脱节)");
                Assert.That(sites[0].Api, Is.EqualTo(api), "报文须点名具体 API");
            }
        }

        [Test]
        public void test_reportedLine_matchesSourceLine()
        {
            const string src =
                "class T\n{\n    void M()\n    {\n        File.WriteAllText(\"a\", \"b\");\n    }\n}";
            var sites = FsWriteBoundaryGate.FindWriteSites(src);
            Assert.That(sites, Has.Count.EqualTo(1));
            Assert.That(sites[0].Line, Is.EqualTo(5), "报错须点名**真实行号**,否则无法定位");
        }

        // ══════════ 声明的反向判据(防白名单腐烂)══════════

        [Test]
        public void test_declaration_pointingAtMissingFile_isRed()
        {
            var files = new Dictionary<string, string>();
            List<string> errs = FsWriteBoundaryGate.ValidateDeclarations(files);
            Assert.That(errs.Exists(e => e.Contains("悬空")), Is.True,
                "声明指向不存在的文件 = 白名单腐烂,必须红");
        }

        [Test]
        public void test_declaration_whoseFileLostAnchor_isRed()
        {
            var files = new Dictionary<string, string>();
            foreach (var d in FsWriteBoundaryGate.Declarations)
                files["Assets/Tests/" + d.FileSuffix] = "class T { }";   // 无锚点 = 已过期
            List<string> errs = FsWriteBoundaryGate.ValidateDeclarations(files);
            Assert.That(errs.Exists(e => e.Contains("过期")), Is.True,
                "文件已无锚点而声明仍在 = 过期声明;过期声明会让门对该文件永久失明");
        }

        [Test]
        public void test_declarations_allCarryReasons()
        {
            Assert.That(FsWriteBoundaryGate.Declarations.Length, Is.GreaterThan(0),
                "声明表为空时本断言无意义 —— 先确认表非空");
            foreach (var d in FsWriteBoundaryGate.Declarations)
            {
                Assert.That(d.FileSuffix, Is.Not.Empty, "声明须点名文件");
                Assert.That(d.Reason, Is.Not.Null, $"`{d.FileSuffix}` 的声明缺理由");
                Assert.That(d.Reason.Trim(), Is.Not.Empty,
                    $"`{d.FileSuffix}` 的声明理由为空白 —— 空理由等于自我豁免");
            }
        }

        // ══════════ 真树 ══════════

        [Test]
        public void test_realTestTree_zeroRed()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            List<string> errs = FsWriteBoundaryGate.RunAll(root);
            Assert.That(errs, Is.Empty,
                "真测试树须零红。若转红:① 新增了写仓库的测试 ⇒ 修测试;② 新增了合法锚点 ⇒ " +
                "在 Declarations 里具名声明并写清理由。**不得**靠删本断言变绿");
        }

        [Test]
        public void test_scanDirExists_andIsNonEmpty()
        {
            string dir = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", FsWriteBoundaryGate.ScanDirRelative));
            Assert.That(Directory.Exists(dir), Is.True, "扫描目录不存在 ⇒ 门会静默变绿(空集)");
            Assert.That(Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories).Length,
                Is.GreaterThan(0), "扫不到任何文件 ⇒ 门是装饰");
        }
    }
}
