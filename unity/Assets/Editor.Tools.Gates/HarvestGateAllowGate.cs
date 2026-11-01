// 17 采集 —— **生产码不得传 `HarvestGate.Allow`**的静态门(S22-9 · 2026-11-01)
//
// ── 它防的是什么 ────────────────────────────────────────────────────────────
// `HarvestGate` 是裸 `readonly struct`,`HarvestGate.Allow` 是一对 `true/true`。
// 把 `Allow` 传给 `HarvestOrchestrator.Resolve` **在类型面上完全合法** —— 编译器不会说话,
// 夹具也不会(夹具本来就允许传)。而它的语义 = **跳过整道采前闸**:
// `CanCarry`(满载)与 `CanHarvest`(余量)都不再被求值,sprint-22 的 Exit 判据当场失效。
//
// `HarvestOrchestrator.Resolve` 的 XML 注释已写「生产裁决不得传 `Allow`」——
// **注释不是执行体**(假绿形态 F6「自述顶替门」)。本门是那句话的执行体。
//
// ── 判据 ────────────────────────────────────────────────────────────────────
// 扫 `Assets/**/*.cs`,**排除 `/Tests/`**:出现 `HarvestGate.Allow` 即报错。
// 排除测试是**判据本身的一部分** —— 夹具需要构造「全放行」来测编排器自身的分支,
// 那是合法的;生产码需要它则一定是跳过闸门。
//
// ⚠️ **本门不判 `new HarvestGate(true, true)`** —— 那同样是造全放行,但它是构造出来的值,
// 与 `Allow` 在语义上等价而写法不同。当前生产码零处出现该形态;若将来出现,须把判据
// 扩成「`HasCapacity=true` 且 `HasRemaining=true` 的常量构造」—— 那需要语法树,
// **本轮不做**,如实登记为已知边界(见 `HarvestGateAllowGateTest` 的边界测)。
//
// 权威:故事 sprint-22 S22-9 · `HarvestOutcome.cs:69-73`(Allow 的自陈禁令)
//       · `HarvestOrchestrator.Resolve` 的 `<param name="gate">` 注释

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>生产码零 `HarvestGate.Allow` 的静态门。</summary>
    public static class HarvestGateAllowGate
    {
        /// <summary>扫描根(相对工程根 `unity/`)。</summary>
        public const string ScanRootRelative = "Assets";

        /// <summary>被禁的 token(单一出处;判据与报文都读它)。</summary>
        public const string ForbiddenToken = "HarvestGate.Allow";

        /// <summary>
        /// 跑本门。
        /// </summary>
        /// <param name="projectRoot">工程根(`unity/`;`"."` = 当前目录)。</param>
        /// <returns>命中即一条错误(含 `file:line`);空 = 通过。</returns>
        public static List<string> RunAll(string projectRoot)
        {
            var errs = new List<string>();
            string root = Path.Combine(projectRoot, ScanRootRelative);
            if (!Directory.Exists(root))
            {
                errs.Add($"[采集闸门] 扫描根不存在:{root} —— 门无从执行");
                return errs;
            }

            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = file.Replace('\\', '/');
                if (normalized.Contains("/Tests/")) continue;

                string text;
                try { text = File.ReadAllText(file); }
                catch (IOException ex)
                {
                    errs.Add($"[采集闸门] 读不了 {normalized}:{ex.Message}");
                    continue;
                }

                // 剥离行注释 / 块注释 / 字符串 —— 注释里提到 `HarvestGate.Allow`
                // (比如解释「为什么禁止它」)不算违规,否则本门的文档会把本门弄红。
                string stripped = StripCommentsAndStrings(text);
                if (stripped.IndexOf(ForbiddenToken, System.StringComparison.Ordinal) < 0) continue;

                errs.Add(
                    $"[采集闸门] {normalized} 在**生产码**里出现 `{ForbiddenToken}` —— " +
                    "它的语义是跳过整道采前闸(`CanCarry` / `CanHarvest` 都不再求值)。" +
                    "生产裁决必须传由 `CarryLoadSolver.CanCarry` 与 `NodeRemaining.CanHarvest` " +
                    "求出的真值;`HarvestOrchestrator.Resolve` 的 XML 注释已明写此禁令,本门是它的执行体。" +
                    "装配点见 `HarvestInteractionDriver`(它**不接受** `HarvestGate` 形参)。");
            }

            return errs;
        }

        /// <summary>把注释与字符串字面量替换成空格(保留换行,行号仍可对)。</summary>
        private static string StripCommentsAndStrings(string src)
        {
            // 与 `ForagingFloatGate` / `MedConsFloatGate` 同族的轻量剥离:
            // 只求「注释/字符串不误报」,不求语法完整。
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            string noLine = Regex.Replace(noBlock, @"//[^\n]*", " ");
            return Regex.Replace(noLine, @"""(\\.|[^""\\])*""", "\"\"");
        }
    }
}
