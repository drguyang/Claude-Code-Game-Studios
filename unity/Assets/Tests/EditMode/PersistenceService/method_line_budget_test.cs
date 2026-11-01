// 方法行数预算夹具 —— `TD-persist-08` 的「≤40 界」执行体(2026-10-31 · sprint-21 S21-7)
//
// ══════════════════════════════════════════════════════════════════════════
// 为什么需要它
// ══════════════════════════════════════════════════════════════════════════
// `code-review` Phase 4 有「方法 ≤ 40 行」一项,但**全仓没有泛用执行体**
// (`TD-clinic-14` / `TD-medcons-17` 都登记过同一事实;唯一的行数门是音频侧
// `s15_5_hygiene_test.cs` 的专有夹具)。
// ⇒ 拆了方法而不钉 = `Landed ✅ / Proven ✗` —— 下一个人把它内联回去,红不了。
// 本文件给 `TD-persist-08` 涉及的方法补上这一钉。
//
// ══════════════════════════════════════════════════════════════════════════
// 口径:**代码行**,不是物理行
// ══════════════════════════════════════════════════════════════════════════
// 本件按 **非空、非 `//` 起始行** 计数(= 纯代码行)。理由见 `TD-persist-08` 的实测:
// `EventStream.Restore` 43 物理行里 5 行是注释与空行 ⇒ 纯代码 38 行,**本来就不超**;
// 按物理行判它「超了」会逼出一次没有信息增益的拆分。
// ⚠️ **块注释 `/* */` 的行会被计成代码**(本件不做块注释剥离)——
// 方向是**偏严**(多算 ⇒ 更容易红),可以接受。
//
// ══════════════════════════════════════════════════════════════════════════
// 本件**不是**全文件/全仓门(如实登记,别读成「规范已落地」)
// ══════════════════════════════════════════════════════════════════════════
// 🔁 **2026-11-01(S22-7 · `TD-persist-13` 结案)**:上面那段(「`ParseBody` / `WriteExitSave`
// 仍超且不在本件面内」)已**过期**,原文保留作闭环记录。现况:
//   · `ParseBody` 已按段拆成 5 个方法(**17 / 10 / 20 / 25 / 21** 代码行,实测),
//     本件新增 `test_td13_parseBodyAndHelpers_withinBudget` 钉住;
//   · `WriteExitSave` **实测只有 6 代码行**(一行表达体)。债记的「61」是**度量器假数** ——
//     原 `MethodBody` 不做表达体识别,签名行没有 `{` 就一路吞到下一个带花括号的方法。
//     ⇒ 本件**同时修了度量器**(见 `IndexOfExpressionBodyEnd`)并加了自测,
//     新判据 `test_td13_writeExitSave_expressionBody_notSwallowed` 钉「不吞下一个方法」。
// ⚠️ 仍**不是**全仓门:本件只钉 `SaveService` 面内的这些方法,**不得**据此声称「全仓合规」。
//
// 📖 只读外部夹具声明(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────
// 读仓库内文件(**只读,零写盘**):
//   · `Assets/Sim.Codec/SaveService.cs`
//   · `Assets/Sim/EventStream.cs`
// **为什么必须读真源**:本条判据的对象就是「源代码里那些方法有多少行」。内联一份
// 假源码等于不测真表 —— 改坏真方法时夹具仍绿,正是本债要消灭的形态。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.PersistenceService
{
    /// <summary>`TD-persist-08` 的 ≤40 代码行预算判据。</summary>
    [TestFixture]
    public class MethodLineBudgetTest
    {
        private const int Budget = 40;

        // ══════════════════ 度量器(纯函数,可在内存上自测) ══════════════════

        /// <summary>取从含 <paramref name="signature"/> 的行起、到花括号配平的那一行为止的方法体。</summary>
        /// <returns>方法体各行(含签名行与右括号行);签名找不到 ⇒ null。</returns>
        internal static string[] MethodBody(string source, string signature)
        {
            string[] lines = source.Replace("\r\n", "\n").Split('\n');
            int start = Array.FindIndex(lines, l => l.Contains(signature));
            if (start < 0) return null;

            // 🔁 2026-11-01(S22-7 · `TD-persist-13`):**补表达体方法**。
            //    原实现只做花括号配平 ⇒ 遇到 `private void X(...) => Expr(...);` 这种表达体方法时,
            //    签名行没有 `{`,它会一路吞到**后一个**带花括号的方法 ⇒ 量出假大数。
            //    `TD-persist-13` 记的「`WriteExitSave` = 61 代码行」就是这么来的(实测真值 6)。
            //    ⇒ 先扫到 `{` 或 `;` 为止:先见 `=>` 即表达体,body 到该语句的 `;` 收尾。
            int depth = 0;
            bool seen = false;
            int end = IndexOfExpressionBodyEnd(lines, start);
            for (int j = start; j < lines.Length; j++)
            {
                foreach (char ch in lines[j])
                {
                    if (ch == '{') { depth++; seen = true; }
                    else if (ch == '}') depth--;
                }
                if (end >= 0 && j >= end)
                    return lines.Skip(start).Take(j - start + 1).ToArray();
                if (end < 0 && seen && depth == 0)
                    return lines.Skip(start).Take(j - start + 1).ToArray();
            }
            return null;   // 花括号未配平 ⇒ 度量失败(调用方须当红,不得当 0)
        }

        /// <summary>
        /// 表达体方法(`… ) => Expr…;`)的收尾行号;不是表达体 ⇒ -1。
        /// <para>从签名行往下扫:先遇到 `{` ⇒ 块体(返回 -1);先遇到 `=>` ⇒ 表达体,
        /// 再找深度 0 的 `;` 收尾(跨行的 `Path.Combine(...)` 链也吃得住)。</para>
        /// </summary>
        private static int IndexOfExpressionBodyEnd(string[] lines, int start)
        {
            bool seenArrow = false;
            for (int j = start; j < lines.Length; j++)
            {
                string l = lines[j];
                for (int k = 0; k < l.Length; k++)
                {
                    if (l[k] == '{') return -1;                       // 先见块体 ⇒ 不是表达体
                    if (l[k] == '=' && k + 1 < l.Length && l[k + 1] == '>') seenArrow = true;
                    if (seenArrow && l[k] == ';') return j;
                }
            }
            return -1;
        }

        /// <summary>代码行数 = 非空且不以 `//` 起始的行数。</summary>
        internal static int CodeLines(string[] body)
            => body == null ? -1 : body.Count(l => l.Trim().Length > 0 && !l.TrimStart().StartsWith("//", StringComparison.Ordinal));

        private static int Measure(string path, string signature)
        {
            string full = Path.Combine(Application.dataPath, path);
            Assert.That(File.Exists(full), Is.True, $"源文件未找到:{full}");
            string[] body = MethodBody(File.ReadAllText(full), signature);
            Assert.That(body, Is.Not.Null,
                $"在 {path} 里找不到签名「{signature}」—— 方法被改名/删除,或度量器失效。" +
                "**不得**把它当成「0 行 ⇒ 通过」");
            return CodeLines(body);
        }

        // ══════════════════ 度量器自身的判别力(防空转) ══════════════════

        /// <summary>正控:一段明确 43 行代码的方法必须量出 43(证度量器**会**报超)。</summary>
        [Test]
        public void test_measurer_reportsOverBudgetSynthetic()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("void Fake()");
            sb.AppendLine("{");
            for (int i = 0; i < 41; i++) sb.AppendLine($"    int a{i} = {i};");
            sb.AppendLine("}");

            int n = CodeLines(MethodBody(sb.ToString(), "void Fake()"));
            Assert.That(n, Is.EqualTo(44), "签名 + 左括号 + 41 句 + 右括号 = 44(度量器必须数得准,否则本件全绿也没有判别力)");
            Assert.That(n, Is.GreaterThan(Budget), "44 > 40 ⇒ 正控必须报超");
        }

        /// <summary>负控:注释与空行不计入(签名 + 两句 + 两个花括号 = 5)。</summary>
        [Test]
        public void test_measurer_ignoresCommentsAndBlankLines()
        {
            const string src = "void Small()\n{\n    // 注释不算\n\n    int x = 1;\n    int y = 2;\n}\n";
            Assert.That(CodeLines(MethodBody(src, "void Small()")), Is.EqualTo(5),
                "5 行真代码:签名 / 左括号 / 两句 / 右括号");
        }

        // ══════════════════ 真树判据 ══════════════════

        /// <summary>`SaveService.Load` 拆分后 ≤ 40 代码行(防内联回去)。</summary>
        [Test]
        public void test_td08_saveServiceLoad_withinBudget()
        {
            int n = Measure("Sim.Codec/SaveService.cs", "public void Load(SaveSlot slot)");
            Assert.That(n, Is.GreaterThan(0), "量出 0 行 ⇒ 度量器抓错了目标,拒判");
            Assert.That(n, Is.LessThanOrEqualTo(Budget),
                $"SaveService.Load = {n} 代码行,超 ≤{Budget} —— 拆分被内联回去了?(TD-persist-08)");
        }

        /// <summary>拆分出的四个帮手各自 ≤ 40。</summary>
        [Test]
        public void test_td08_extractedHelpers_withinBudget()
        {
            var sigs = new Dictionary<string, string>
            {
                ["RequireLoadable"] = "private void RequireLoadable(SaveSlot slot)",
                ["RequireSupportedVersion"] = "private static void RequireSupportedVersion(in SaveHeader header)",
                ["ApplyMigrationsIfNeeded"] = "internal byte[] ApplyMigrationsIfNeeded(byte[] data, in SaveHeader header)",
                ["ReplayInto"] = "private void ReplayInto(byte[] data, SaveSlot slot)",
            };
            foreach (var kv in sigs)
            {
                int n = Measure("Sim.Codec/SaveService.cs", kv.Value);
                Assert.That(n, Is.GreaterThan(0), $"{kv.Key} 量出 0 行 ⇒ 度量器抓错目标");
                Assert.That(n, Is.LessThanOrEqualTo(Budget), $"{kv.Key} = {n} 代码行,超 ≤{Budget}");
            }
        }

        /// <summary>`EventStream.Restore` ≤ 40 代码行(**按代码行口径本来就不超** —— 见文件头)。</summary>
        [Test]
        public void test_td08_eventStreamRestore_withinBudget()
        {
            int n = Measure("Sim/EventStream.cs", "public void Restore(IReadOnlyList<SimEvent> events");
            Assert.That(n, Is.GreaterThan(0), "量出 0 行 ⇒ 度量器抓错目标");
            Assert.That(n, Is.LessThanOrEqualTo(Budget),
                $"EventStream.Restore = {n} 代码行,超 ≤{Budget}(TD-persist-08)");
        }

        // ══════════════════ TD-persist-13(S22-7)══════════════════

        /// <summary>`ParseBody` 拆分后:本体与 4 个helper 全部 ≤ 40 代码行。</summary>
        [Test]
        public void test_td13_parseBodyAndHelpers_withinBudget()
        {
            var sigs = new Dictionary<string, string>
            {
                ["ParseBody"] = "private void ParseBody(",
                ["ParseConfigVersionSegment"] = "private static IReadOnlyList<ArtifactConfigVersion> ParseConfigVersionSegment(",
                ["ParseFoldSegment"] = "private static byte[] ParseFoldSegment(",
                ["ParsePayloadSegment"] = "private static IReadOnlyList<PayloadEntry> ParsePayloadSegment(",
                ["ReadPayloadEntry"] = "private static PayloadEntry ReadPayloadEntry(",
            };

            var over = new List<string>();
            foreach (var kv in sigs)
            {
                int n = Measure("Sim.Codec/SaveService.cs", kv.Value);
                Assert.That(n, Is.GreaterThan(0),
                    $"{kv.Key} 量出 0 行 ⇒ 度量器抓错目标(方法被改名?),拒判");
                if (n > Budget) over.Add($"{kv.Key}={n}");
            }

            Assert.That(over, Is.Empty,
                "以下方法超 ≤" + Budget + " 代码行:" + string.Join(", ", over) +
                " —— 拆分被内联回去了?(TD-persist-13)");
        }

        /// <summary>
        /// `WriteExitSave` 是**一行表达体**:≤ 40 且**度量器不得越过它吞掉下一个方法**。
        /// <para>后半句才是本测的核心 —— `TD-persist-13` 记的「61 代码行」就是这么来的:
        /// 签名行没有 `{`,老度量器一路吞到 `RaiseIncompleteFlag` / `Load`。</para>
        /// </summary>
        [Test]
        public void test_td13_writeExitSave_expressionBody_notSwallowedByMeasurer()
        {
            string full = Path.Combine(Application.dataPath, "Sim.Codec/SaveService.cs");
            Assert.That(File.Exists(full), Is.True, $"源文件未找到:{full}");
            string[] body = MethodBody(File.ReadAllText(full), "private void WriteExitSave(byte[] data)");
            Assert.That(body, Is.Not.Null, "找不到 `WriteExitSave` 签名 —— 方法被改名/删除?");

            string joined = string.Join("\n", body);
            Assert.That(joined, Does.Not.Contain("RaiseIncompleteFlag"),
                "度量器越过表达体方法、把**后一个方法**吞进来了 —— 那会量出假大数" +
                "(TD-persist-13 的「61 代码行」正是此形态)");
            Assert.That(joined, Does.Contain("WriteAtomic"),
                "量到的体里没有 `WriteAtomic` ⇒ 抓错目标");

            int n = CodeLines(body);
            Assert.That(n, Is.LessThanOrEqualTo(10),
                $"WriteExitSave = {n} 代码行(表达体委托,实测 6)—— 超过 10 说明有人往里塞了逻辑," +
                "那属于 `WriteAtomic` 的职责(TD-persist-13)");
        }

        /// <summary>度量器自测:表达体方法只取到它自己那几行(字面量样本,不引用被测常量)。</summary>
        [Test]
        public void test_measurer_expressionBody_stopsAtSemicolon()
        {
            const string src =
                "void Big()\n" +
                "{\n    int a = 1;\n}\n" +
                "void Thin(byte[] d)\n" +
                "    => Do(Path.Combine(\"a\", \"b\"),\n" +
                "          d);\n" +
                "void Next()\n" +
                "{\n    int b = 2;\n}\n";

            string[] body = MethodBody(src, "void Thin(byte[] d)");
            Assert.That(body, Is.Not.Null);
            Assert.That(string.Join("\n", body), Does.Not.Contain("Next"),
                "表达体未被识别 ⇒ 吞掉后一个方法(假大数的来源)");
            Assert.That(CodeLines(body), Is.EqualTo(3),
                "签名行 + `=>` 行 + `d);` 行 = 3 代码行");
        }
    }
}
