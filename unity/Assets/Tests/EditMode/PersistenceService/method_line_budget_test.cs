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
// 同一个 `SaveService.cs` 里今日仍有**超出**的方法:`ParseBody` / `WriteExitSave`
// (2026-10-31 实测:代码行 68 / 61)。它们**不在 `TD-persist-08` 的登记面内**
// (`-08` 只点名 `Load` 与 `Restore`),已另立 `TD-persist-13` 跟踪。
// ⇒ 本件只钉 `-08` 面内的六个方法,**不得**据此声称「SaveService 已合规」。
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

            int depth = 0;
            bool seen = false;
            for (int j = start; j < lines.Length; j++)
            {
                foreach (char ch in lines[j])
                {
                    if (ch == '{') { depth++; seen = true; }
                    else if (ch == '}') depth--;
                }
                if (seen && depth == 0)
                    return lines.Skip(start).Take(j - start + 1).ToArray();
            }
            return null;   // 花括号未配平 ⇒ 度量失败(调用方须当红,不得当 0)
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
    }
}
