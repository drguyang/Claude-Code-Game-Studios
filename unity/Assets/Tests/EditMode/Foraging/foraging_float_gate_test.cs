// story-005 · AC-17-10 —— 17 求值路径**零浮点**门的判别力夹具。
//
// 权威: design/gdd/foraging.md AC-17-10(BLOCKING)· ADR-006 · ADR-014 §三(FixParse 只在烘焙期)
// story production/epics/foraging/story-005-action-atomicity-failure-convergence.md(Implementation Note 5)
//
// ── 为什么用纯函数驱动而不是往仓库里写违规文件 ────────────────────────────
// 写盘会撞 `FsWriteBoundaryGate`(S16-4:测试不得写仓库),而且违规文件一旦漏删就变成
// 常驻的假红。⇒ 判据落在 `ForagingFloatGate.ScanSource` 这个**纯函数**上:
// 它与 `RunAll` 走同一条管线,直驱它即可证明「门会红」。
//
// ⚠️ 本件**不**主张「Roslyn 语法树级」的覆盖 —— 门是文本级。残余缺口(IL 层 `conv.r4`)
//    在门头注里如实登记,不在本件兜底。

using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Foraging
{
    /// <summary>AC-17-10 零浮点门:先证它会红,再证现存树是绿的。</summary>
    [TestFixture]
    public sealed class ForagingFloatGateTest
    {
        // ══════════ 判别力:门必须真的会红 ══════════

        [TestCase("float qty = 1.0f;", TestName = "float_token")]
        [TestCase("double d = 0;", TestName = "double_token")]
        [TestCase("Fix g = 0.5f;", TestName = "float_literal_with_suffix")]
        [TestCase("Fix g = new Fix(1.5);", TestName = "float_literal_plain")]
        public void test_ac1710_floatForms_areRejected(string source)
        {
            var errs = ForagingFloatGate.ScanSource(source);
            Assert.That(errs, Is.Not.Empty,
                "含 float/double 的源文本必须被拒 —— 否则门对该形态失明:" + source);
        }

        [Test]
        public void test_ac1710_cleanFixedPointSource_isAccepted()
        {
            // 负控必须与正例**独立**:一段只用 Fix 整数域的正常代码不得被判红。
            const string clean =
                "Fix mul = Fix.One + Fix.FromRational(level, 30);\n" +
                "return mul.Raw > constants.GatherMulCap.Raw ? constants.GatherMulCap : mul;";
            Assert.That(ForagingFloatGate.ScanSource(clean), Is.Empty,
                "合法的定点写法被误判 ⇒ 门过宽,会把正确实现判成红");
        }

        [Test]
        public void test_ac1710_documentReferenceInString_isNotALiteral()
        {
            // "F-17.2" / "ADR-006.3" 是文档引用,不是 C# 浮点字面量。
            // 若门不剥字符串,它自己的错误消息也会被正则判红(自触门)。
            const string source = "// 见 GDD F-17-2 与 ADR-006.3\n" +
                                  "throw new System.Exception(\"F-17.2 的表值不符\");";
            Assert.That(ForagingFloatGate.ScanSource(source), Is.Empty,
                "字符串 / 注释里的文档编号不得被当作浮点字面量");
        }

        // ══════════ 现状:真实树是绿的 ══════════

        [Test]
        public void test_ac1710_realForagingTree_isFloatFree()
        {
            // 工程根 = Unity 进程的当前目录(`MedConsFloatGate` 的既有口径同为 ".")。
            var errs = ForagingFloatGate.RunAll(".");
            Assert.That(errs, Is.Empty,
                "17 的求值路径出现浮点 —— AC-17-10 是 BLOCKING:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_ac1710_scanDirectoryExists()
        {
            // 防「目录被改名 ⇒ RunAll 扫到空 ⇒ 恒绿」的失效形态(门自身的前置)。
            Assert.That(ForagingFloatGate.RunAll("."), Is.Not.Null);
            Assert.That(System.IO.Directory.Exists(ForagingFloatGate.ForagingDirRelative), Is.True,
                $"扫描目录 `{ForagingFloatGate.ForagingDirRelative}` 不存在 —— 门会静默变绿");
        }
    }
}
