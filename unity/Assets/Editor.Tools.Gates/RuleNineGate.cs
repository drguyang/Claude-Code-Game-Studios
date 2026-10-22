// AC ⑥ · GDD disease-simulation.md 规则九 —— 「注册表是数据,不是代码」机械门。
//
// ══════════════════════════════════════════════════════════════════════════
// 立规缘由(sprint-18 S18-1 AC ⑥ = `R2-AC6`,长期 NOT-RUN)
// ══════════════════════════════════════════════════════════════════════════
// S18-1 的测试注释曾声称「文件末尾有 AST 式断言」守住规则九,**实测该断言从未存在**,
// 全仓 `Editor.Tools.Gates` 亦无规则九门 ⇒ AC ⑥ 一直是 NOT-RUN 却被当成已满足。
// 这是本项目抓过的**第五个假绿形态**:不是「测试跑自造断言」,是
// **用一句自述顶替了一个从未存在的门**。⇒ 立本门,并同步 `coding-standards.md`
// §Review Evidence Standards 的「某件有门须给 file:line + 可跑命令」条。
//
// ══════════════════════════════════════════════════════════════════════════
// 权威来源
// ══════════════════════════════════════════════════════════════════════════
//   design/gdd/disease-simulation.md:243-245 —— 规则九原文:
//     「注册表是数据,不是代码 … **新增病种 = 加一行数据,不改一行逻辑**。
//       任何「为某个特定病种写的 if」都是本规则的违反。」
//   design/gdd/disease-simulation.md:356-358 —— 伤情同样走这张 schema;
//     「**省字段 = 实现里多一个 `if kind == injury`**,违反规则九」
//   design/gdd/disease-simulation.md:329 —— `handle` 派生判据(同样禁按 kind 分支)
//   production/qa/qa-plan-sprint-18-2026-10-21.md:80 —— **AC ⑥ 的判据形态**:
//     「判据形态 = **反射 / IL 扫描 + 源码 grep 双证**;单靠 grep 可被换写法绕过」
//     ⇒ 本门因此有**两个半边**:`CheckSource`(源码)+ `CheckIl`(IL)。**缺一即不达标。**
//   `EntityDeathGates.cs:39` —— 「本类**零** `if kind == injury` 式的类别分支」
//
// ══════════════════════════════════════════════════════════════════════════
// 判据内核:「控制流分支」≠「kind 出现在表达式里」
// ══════════════════════════════════════════════════════════════════════════
// 这条区分是本门全部判据力的来源,也是它最容易写成误报的地方:
//   · ✅ **合法**:`entry.Kind == DiseaseKind.Injury && …` 作为**合取项**参与判据
//     (`EntityDeathGates.IsBattleInjury`:`:79-81`)。它把 `kind` 当**数据成员**判,
//     病种与伤情走同一张 schema、同一段代码,`kind` 只是一个字段值。
//   · ❌ **违例**:`if (kind == injury) { …A… } else { …B… }` —— 两条**不同的执行路径**,
//     等于「为伤情单独写了一段逻辑」,正是规则九说的「为某个特定病种写的 if」。
//
// ══════════════════════════════════════════════════════════════════════════
// 规则清单
// ══════════════════════════════════════════════════════════════════════════
// **源码半**(`CheckSource`,扫 `Assets/Sim/**` + `Assets/Sim.Contracts/**`)
//   S1  `switch (<x>.Kind)` 且**该字段类型是 `DiseaseKind`**  ⇒ 红(按类别分派)
//   S2  `if (…Kind == DiseaseKind.<成员>…)` 且分支体**非单条 guard 语句** ⇒ 红(控制流分支)
//       白名单形态 = 分支体恰一条 `return` 或 `throw`(数据成员判定 / 校验守卫,
//       现存两例:`EntityDeathGates.IsBattleInjury` 的 `return` 合取、
//       `RegistrySchema.Validate` 的 `throw` 守卫)
//   S3  `<Kind 比较> ? … : …`(三元)          ⇒ 红(表达式形态的分支)
//   S4  `DiseaseKey == "<字面量>"` 类           ⇒ 红(为**具体病种**写的 if,规则九最直接形态)
//
// **IL 半**(`CheckIl`,Cecil 扫 `Sim.dll` + `Sim.Contracts.dll`)
//   I1  `switch` 指令的操作数来自 **`DiseaseKind` 类型**的 `Kind` 字段装载 ⇒ 红(Kind 分派表)
//   I2  同一方法内 ≥3 个不同的 `DiseaseKind` 成员常量与 `Kind` 比较 ⇒ 红(穷举式分支)
//   I3  `DiseaseKey` 字段装载后与 `ldstr` 常量做 `String.op_Equality` / `String.Equals`
//       ⇒ 红(硬编码病种名 —— 源码改写形态也绕不过)
//
// ⚠️ **判据必须锚在「字段类型 = `DiseaseKind`」上,不能锚在字段名 `Kind` 上**
//   —— 这是本门首轮实测踩到的**假红**(与假绿同属静默失败):
//   首版只匹配 `switch (x.Kind)`,真工程立刻报出两处 ——
//     · `Clinic/ClinicRoomMatcher.cs:279`  `switch (clause.Kind)` = `ClinicRoomClauseKind`
//       (医馆房间**子句类型**:MinCount / HasType / AdjacencyMinCount)
//     · `Consequences/ConsequenceInputAggregator.cs:294` `switch (e.Kind)` = `EventKind`
//       (事件类型分派 —— 全仓 35 支 Kind 的**路由**就靠它,禁掉它等于禁掉 ADR-024)
//   两者都与「病种类别」无关。⇒ 判据收紧到**类型**级:源码半要求比较式右侧出现
//   `DiseaseKind.<成员>`(排除 `ClinicRoomClauseKind.` / `EventKind.` 等同名尾巴),
//   IL 半要求 `FieldReference.FieldType` 解析为 `DiseaseKind`。
//   **这条收紧本身也是判据**:若日后有人把 `DiseaseKind` 改名或把字段换成别的类型,
//   本门会静默失去判别力 ⇒ 由 `test_ruleNine_ilHalf_diseaseKindAnchorIsLive` 钉住。
//
// **为什么两半都要**(承 qa-plan:80 原文):源码半会漏
//   `switch` 的等价改写(`if/else if` 链被抽成 helper)、`DiseaseKey.Equals(const)` 的
//   调用序颠倒;IL 半会漏语义(它只看得到指令,判不了「这段分支算不算业务逻辑」)。
//   **两半各自盲区不同** ⇒ 缺一即不达标,不得以「另一半绿了」代替。
//
// ══════════════════════════════════════════════════════════════════════════
// 已知盲区(登记,不借绿)
// ══════════════════════════════════════════════════════════════════════════
//   ① 源码半**不建 AST** —— 条件跨行靠括号配平,极复杂嵌套可能漏;
//   ② IL 半的 I2 阈值 ≥3 刻意留了 2 臂的余量 —— 2 臂违例由源码半 S2 接住;
//      两半都绕过的形态(如反射构造的分派)看不见;
//   ③ 病种数上限(~8)本身**不**由本门判 —— 那是 R1 表数据量,归注册表校验。
//
// ══════════════════════════════════════════════════════════════════════════
// 扫描面
// ══════════════════════════════════════════════════════════════════════════
// 源码 = `Assets/Sim/**` + `Assets/Sim.Contracts/**`(门 A 内侧)。
// **刻意不含** `Assets/Editor.Tools.Gates/` 自身(本门文本里必然出现 `Kind ==`)
// 与 `Assets/Tests/`(测试**应当**能用具体病种构造夹具 —— 且 IL 半的非空转夹具正住那里)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEditor;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>规则九门:`kind` 驱动的控制流分支 / 具体病种硬编码 = 违例(源码 + IL 双证)。</summary>
    public static class RuleNineGate
    {
        /// <summary>源码半扫描目录(相对工程根 `unity/`)。门 A 内侧两个装配。</summary>
        public static readonly string[] ScanDirs = { "Assets/Sim", "Assets/Sim.Contracts" };

        /// <summary>IL 半扫描装配(ADR-025 §① 门 A 内侧)。</summary>
        public static readonly string[] IlScanAssemblies = { "Sim", "Sim.Contracts" };

        /// <summary>`DiseaseKind` 的成员名(用于源码正则与 IL 常量映射)。</summary>
        public static readonly string[] KindMemberNames =
            { "Unspecified", "Disease", "Injury", "Poison" };

        // ══════════════════════════════════════════════════════════════════════
        // 门链入口(构建前门 / 菜单)—— 两半合流
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// **构建前门 / 菜单入口** —— 源码半 + IL 半合流,含陈旧产物守卫。
        /// <para>守卫形态复刻 <see cref="EcozoneIntegerGates.RunAll(out List{string})"/>:
        /// 住在**门自己**身上,不指望每个调用方各写一遍。</para>
        /// </summary>
        /// <param name="projectRoot">工程根(通常 `"."`)。</param>
        public static List<string> RunAll(string projectRoot = ".")
        {
            var errs = new List<string>();
            errs.AddRange(CheckSource(projectRoot));

            // IL 半读的是上一次编译产物 —— 编译失败时扫它 = 拿旧代码发言。
            if (EditorUtility.scriptCompilationFailed)
            {
                errs.Add("[RuleNine] EditorUtility.scriptCompilationFailed = true ⇒ 拒扫陈旧产物" +
                         "(假绿防护:读上一版 DLL = 假绿;红在编译器,本门只声明不可判)。");
                return errs;
            }

            foreach (string asmName in IlScanAssemblies)
            {
                string dll = AssemblyGates.ScriptAssemblyPath(asmName);
                errs.AddRange(CheckIl(dll, asmName));
            }
            return errs;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 源码半
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>`if` / `else if` 的开头(用于取判定表达式)。</summary>
        private static readonly Regex IfHead = new Regex(@"\b(?:else\s+)?if\s*\(", RegexOptions.Compiled);

        /// <summary>
        /// S1:`switch` 的判定对象是 `Kind`,**且该 `switch` 的 case 标签里有 `DiseaseKind.<成员>`**。
        ///
        /// <para>⚠️ 为什么要 case 标签作证:仅凭 `switch (x.Kind)` 无法区分字段类型 ——
        /// 真工程有 `ClinicRoomClauseKind` / `EventKind` 两处同名 `Kind` 字段,首版判据
        /// 对它们假红(见文件头)。`case DiseaseKind.X:` 是**类型级**证据,同名尾巴
        /// (`case ClinicRoomClauseKind.X:`)不会命中。</para>
        /// </summary>
        private static readonly Regex KindSwitchHead = new Regex(
            @"\bswitch\s*\(\s*[\w.]+\.Kind\s*\)", RegexOptions.Compiled);

        /// <summary>`case DiseaseKind.<成员>:` —— `DiseaseKind` 分派的类型级证据。</summary>
        private static readonly Regex DiseaseKindCaseLabel = new Regex(
            @"\bcase\s+DiseaseKind\s*\.\s*(?:Unspecified|Disease|Injury|Poison)\b", RegexOptions.Compiled);

        /// <summary>
        /// S2 / S3:`kind` 与 **`DiseaseKind` 成员**的比较。
        ///
        /// <para>⚠️ 右侧/左侧**必须**带 `DiseaseKind.` 限定 —— 裸成员名会让
        /// `ClinicRoomClauseKind.Injury` 之类的同名尾巴误命中。当前全仓零裸写形态
        /// (`grep -rn "using static.*DiseaseKind"` = 0),故收紧不损失覆盖。</para>
        /// </summary>
        private static readonly Regex KindComparison = new Regex(
            @"\b[\w.]+\.Kind\s*[!=]=\s*DiseaseKind\s*\.\s*(?:Unspecified|Disease|Injury|Poison)\b" +
            @"|\bDiseaseKind\s*\.\s*(?:Unspecified|Disease|Injury|Poison)\s*[!=]=\s*[\w.]+\.Kind\b",
            RegexOptions.Compiled);

        /// <summary>S3:三元运算符。</summary>
        private static readonly Regex Ternary = new Regex(@"\?[^?]*:", RegexOptions.Compiled);

        /// <summary>
        /// S4:具体病种名的硬编码比较。
        /// <para>三种形态:`DiseaseKey == "X"` · `"X" == DiseaseKey` · `DiseaseKey.Equals("X")` /
        /// `string.Equals(DiseaseKey, "X")`。</para>
        /// </summary>
        private static readonly Regex DiseaseKeyLiteral = new Regex(
            @"DiseaseKey\s*==\s*""|""\s*==\s*[\w.]*DiseaseKey\b" +
            @"|DiseaseKey\s*\.\s*Equals\s*\(\s*""|Equals\s*\(\s*[\w.]*DiseaseKey\s*,\s*""",
            RegexOptions.Compiled);

        /// <summary>
        /// **源码半** —— 扫 <see cref="ScanDirs"/> 下全部 `.cs`,判 S1–S4。
        /// </summary>
        /// <param name="projectRoot">工程根(通常 `"."`)。</param>
        /// <returns>违例描述列表;空 = 通过。</returns>
        public static List<string> CheckSource(string projectRoot)
        {
            var errs = new List<string>();
            int scanned = 0;

            foreach (string dir in ScanDirs)
            {
                string full = Path.Combine(projectRoot, dir);
                if (!Directory.Exists(full)) continue;

                foreach (string file in Directory.GetFiles(full, "*.cs", SearchOption.AllDirectories))
                {
                    scanned++;
                    errs.AddRange(ScanSourceFile(projectRoot, file));
                }
            }

            // ⚠️ 扫描根为 0 ⇒ 扫描面丢失(假绿面),响亮失败而非静默通过。
            if (scanned == 0)
                errs.Add("[RuleNine/S] 源码扫描根为 0 —— 扫描面丢失(假绿面),请查装配路径。");

            return errs;
        }

        /// <summary>扫描单个源文件。</summary>
        private static IEnumerable<string> ScanSourceFile(string projectRoot, string file)
        {
            string root = projectRoot.TrimEnd('/', '\\');
            string rel = (root.Length == 0 || root == ".")
                ? file.Replace('\\', '/')
                : file.Replace(root + "/", "").Replace('\\', '/');

            string[] lines = File.ReadAllLines(file);
            string[] code = StripComments(lines);   // 剥注释,避免注记里的示例误报

            for (int i = 0; i < code.Length; i++)
            {
                string line = code[i];
                if (line.Length == 0) continue;

                // ── S1:switch (x.Kind) 且 case 标签证明字段类型是 DiseaseKind ──
                if (KindSwitchHead.IsMatch(line) && SwitchDispatchesOnDiseaseKind(code, i))
                {
                    yield return $"[RuleNine/S1] {rel}:{i + 1} —— `switch` 按 DiseaseKind 分派 ⇒ " +
                                 "规则九违例(注册表是数据,不是代码)。改为数据驱动的字段判定。";
                    continue;
                }

                // ── S4:具体病种名硬编码 ──
                if (DiseaseKeyLiteral.IsMatch(line))
                {
                    yield return $"[RuleNine/S4] {rel}:{i + 1} —— 以**具体病种名**做比较 ⇒ 规则九违例" +
                                 $"(`disease-simulation.md:243-245`「为某个特定病种写的 if」)。原行:`{lines[i].Trim()}`";
                    continue;
                }

                if (!KindComparison.IsMatch(line)) continue;

                // ── S3:三元(Kind 驱动分支的表达式形态)──
                if (Ternary.IsMatch(line))
                {
                    yield return $"[RuleNine/S3] {rel}:{i + 1} —— 三元运算符以 kind 为条件 ⇒ 规则九违例" +
                                 $"(表达式形态的分支)。原行:`{lines[i].Trim()}`";
                    continue;
                }

                // ── S2:if / else if 条件里含 kind 比较 ──
                if (!IfHead.IsMatch(line)) continue;
                if (IsSingleGuardStatement(code, i))
                    continue;   // 数据成员判定 / 校验守卫 ⇒ 放行(见文件头 S2 白名单形态)

                yield return $"[RuleNine/S2] {rel}:{i + 1} —— `if` 条件里按 kind 分支且分支体非单条 " +
                             $"`return` / `throw` ⇒ 规则九违例。原行:`{lines[i].Trim()}`";
            }
        }

        /// <summary>
        /// 判该 `switch (x.Kind)` 的 case 标签里是否有 `DiseaseKind.<成员>`
        /// —— 即**字段类型确实是被规则九管辖的 `DiseaseKind`**。
        ///
        /// <para>扫描窗口 = `switch` 行起 40 行内或到花括号配平为止(取先到者)。
        /// 40 行是经验值:`ConsequenceInputAggregator` 的三臂 switch 每臂 6 行,
        /// 最长的既有 switch 在此窗口内闭合。</para>
        /// </summary>
        private static bool SwitchDispatchesOnDiseaseKind(string[] code, int switchLine)
        {
            int depth = 0;
            bool opened = false;

            for (int p = switchLine; p < code.Length && p < switchLine + 40; p++)
            {
                foreach (char c in code[p])
                {
                    if (c == '{') { depth++; opened = true; }
                    else if (c == '}')
                    {
                        depth--;
                        if (opened && depth == 0) return false;   // switch 体闭合,未见 DiseaseKind 标签
                    }
                }

                if (p > switchLine || code[p].Contains("{"))
                {
                    if (DiseaseKindCaseLabel.IsMatch(code[p])) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 判该 `if` 是否属「单条 guard 语句」形态(⇒ 放行)。
        ///
        /// <para>形态:条件块结束后,分支体只有**一条**语句,且以 `return` 或 `throw` 开头。
        /// 这是把 `kind` 当**字段值**用(数据成员判定),或做**入参校验**(漏填守卫),
        /// 不是「为某个类别写一段逻辑」。</para>
        /// <para>现存两例:`EntityDeathGates.IsBattleInjury`(单 `return` 合取)、
        /// `RegistrySchema.Validate` 的 R1-31 零值陷阱守卫(单 `throw`)。</para>
        /// </summary>
        private static bool IsSingleGuardStatement(string[] code, int ifLine)
        {
            // 找条件右括号所在行(支持跨行条件)。
            int depth = 0, condEnd = -1;
            bool opened = false;
            for (int p = ifLine; p < code.Length && p < ifLine + 12; p++)
            {
                foreach (char c in code[p])
                {
                    if (c == '(') { depth++; opened = true; }
                    else if (c == ')')
                    {
                        depth--;
                        if (opened && depth == 0) { condEnd = p; break; }
                    }
                }
                if (condEnd >= 0) break;
            }
            if (condEnd < 0) return false;

            // 条件行右括号之后的残余。
            string tail = code[condEnd];
            int closeIdx = tail.LastIndexOf(')');
            tail = (closeIdx >= 0 && closeIdx + 1 < tail.Length)
                ? tail.Substring(closeIdx + 1).Trim()
                : "";

            var body = new List<string>();
            bool braced = tail.StartsWith("{");
            if (braced && tail.Length > 1) body.Add(tail.Substring(1).Trim());

            for (int q = condEnd + 1; q < code.Length && q < condEnd + 12; q++)
            {
                string s = code[q].Trim();
                if (braced)
                {
                    if (s.StartsWith("}")) break;
                    if (s.Length > 0) body.Add(s);
                }
                else
                {
                    if (s.Length == 0) continue;
                    body.Add(s);
                    break;   // 无花括号 ⇒ 单语句
                }
            }

            var nonEmpty = body.FindAll(s => s.Length > 0);
            if (nonEmpty.Count != 1) return false;

            string only = nonEmpty[0];
            return only.StartsWith("return ", StringComparison.Ordinal)
                || only.StartsWith("return(", StringComparison.Ordinal)
                || only.StartsWith("return;", StringComparison.Ordinal)
                || only.StartsWith("throw ", StringComparison.Ordinal)
                || only.StartsWith("throw(", StringComparison.Ordinal);
        }

        /// <summary>剥行注释与块注释(保留行数,便于行号对齐)。</summary>
        private static string[] StripComments(string[] lines)
        {
            var outp = new string[lines.Length];
            bool inBlock = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string s = lines[i];
                var sb = new System.Text.StringBuilder(s.Length);
                for (int j = 0; j < s.Length; j++)
                {
                    if (inBlock)
                    {
                        if (j + 1 < s.Length && s[j] == '*' && s[j + 1] == '/')
                        { inBlock = false; j++; }
                        continue;
                    }
                    if (j + 1 < s.Length && s[j] == '/' && s[j + 1] == '/') break;      // 行注释
                    if (j + 1 < s.Length && s[j] == '/' && s[j + 1] == '*')
                    { inBlock = true; j++; continue; }
                    sb.Append(s[j]);
                }
                outp[i] = sb.ToString();
            }
            return outp;
        }

        // ══════════════════════════════════════════════════════════════════════
        // IL 半(Cecil)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// **IL 半** —— 扫 <paramref name="dllPath"/> 内全部方法体,判 I1–I3。
        /// <para>产物缺失 / 零方法 ⇒ **记红**(拒以空集冒充绿,与
        /// <see cref="EcozoneIntegerGates"/> 同纪律)。</para>
        /// </summary>
        /// <param name="dllPath">编译产物绝对路径。</param>
        /// <param name="label">报错标签(装配名;为空时取文件名)。</param>
        public static List<string> CheckIl(string dllPath, string label = null)
        {
            var errs = new List<string>();
            label = string.IsNullOrEmpty(label) ? Path.GetFileNameWithoutExtension(dllPath) : label;

            if (!File.Exists(dllPath))
            {
                errs.Add($"[RuleNine/I] 编译产物缺失「{dllPath}」—— 扫描面不存在,拒以空集冒充绿。");
                return errs;
            }

            int methodCount = 0;
            using (var asm = AssemblyDefinition.ReadAssembly(dllPath,
                       new ReaderParameters { ReadSymbols = false }))
            {
                foreach (var type in AllTypes(asm.MainModule))
                {
                    foreach (var m in type.Methods)
                    {
                        if (!m.HasBody) continue;
                        methodCount++;
                        errs.AddRange(ScanMethodIl(label, m));
                    }
                }
            }

            if (methodCount == 0)
                errs.Add($"[RuleNine/I] 「{label}」内零方法体 —— 扫描面丢失(假绿面)。");

            return errs;
        }

        /// <summary>递归取模块内全部类型(含嵌套)。</summary>
        private static IEnumerable<TypeDefinition> AllTypes(ModuleDefinition module)
        {
            var stack = new Stack<TypeDefinition>(module.Types);
            while (stack.Count > 0)
            {
                var t = stack.Pop();
                yield return t;
                foreach (var n in t.NestedTypes) stack.Push(n);
            }
        }

        /// <summary>扫描单个方法体,判 I1–I3。</summary>
        private static IEnumerable<string> ScanMethodIl(string label, MethodDefinition m)
        {
            var instrs = m.Body.Instructions;
            var kindConstants = new HashSet<int>();   // I2 用

            for (int i = 0; i < instrs.Count; i++)
            {
                var ins = instrs[i];

                // ── I1:switch 的操作数来自 Kind 装载 ──
                if (ins.OpCode == OpCodes.Switch && KindLoadedWithin(instrs, i, 8))
                {
                    yield return $"[RuleNine/I1] {label}::{m.FullName} @IL_{ins.Offset:x4} —— " +
                                 "`switch` 按 Kind 分派 ⇒ 规则九违例(按类别分派的经典形态)。";
                }

                // ── I3:DiseaseKey 装载后与 ldstr 常量比较 ──
                if (IsStringEqualityCall(ins) && DiseaseKeyLoadedWithin(instrs, i, 5))
                {
                    yield return $"[RuleNine/I3] {label}::{m.FullName} @IL_{ins.Offset:x4} —— " +
                                 "`DiseaseKey` 与字符串常量做相等比较 ⇒ 规则九违例" +
                                 "(为**具体病种**写分支;IL 面,源码改写绕不过)。";
                }

                // ── I2:收集与 Kind 比较的枚举常量 ──
                // 形态 = `ldarg.0; ldfld Kind; ldc.i4.N; ceq` —— 常量在装载**之后**,
                // 且**紧跟比较指令**。
                //
                // ⚠️ **2026-10-22 修(实测假红,承 `R1_CHECK_32` 落地轮)**:
                //   原判据是「窗口 `i..i+3` 内的任意 `ldc.i4` 都算」—— 那个窗口会把
                //   **与 Kind 无关的整数常量**一并捞进来。实测 `RegistrySchemaValidator.Validate`
                //   报出常量 `(0, 2, 31)`:**31 是 `R1_CHECK_31` 的异常码**,根本不是
                //   `DiseaseKind` 成员(合法值域只有 0..3)。⇒ 判据过宽 = **假红**,
                //   与假绿同属静默失败。
                //   收紧为:**常量必须紧跟 `ceq` / `beq` / `bne.un`** 才算「与 Kind 比较」。
                //   ⚠️ 判别力为何不损:外层 `i` 遍历全部指令,而 `KindLoadedWithin(i, 2)`
                //   在 `ldfld` 位于 `i-1` **或** `i-2` 时都成立 ⇒ 对同一处 `Kind == X`,
                //   外层会在两个相邻的 `i` 上各跑一次,窗口合并后覆盖
                //   `ldfld` 之后 1..5 条指令 —— `Kind == DiseaseKind.X` 的标准 IL
                //   (`ldfld Kind; ldc.i4.X; ceq`)必然落在其中,且 `ldc.i4` 恒紧邻 `ceq`。
                if (KindLoadedWithin(instrs, i, 2))
                {
                    for (int k = i; k < instrs.Count && k <= i + 3; k++)
                    {
                        int? v = TryGetI4(instrs[k]);
                        if (!v.HasValue) continue;
                        if (k + 1 >= instrs.Count) continue;
                        if (IsComparisonOp(instrs[k + 1].OpCode)) kindConstants.Add(v.Value);
                    }
                }
            }

            if (kindConstants.Count >= 3)
            {
                yield return $"[RuleNine/I2] {label}::{m.FullName} —— 同一方法内以 " +
                             $"{kindConstants.Count} 个不同的 DiseaseKind 常量与 Kind 比较" +
                             $"({string.Join(", ", kindConstants.OrderBy(x => x))}) ⇒ 穷举式分支,规则九违例。";
            }
        }

        /// <summary>`DiseaseKind` 枚举的完整类型名(锚点)。</summary>
        public const string DiseaseKindFullName = "DaYiJingCheng.Sim.DiseaseKind";

        /// <summary>
        /// 该指令是否在 <paramref name="window"/> 条内装载了**类型为 `DiseaseKind`** 的 `Kind` 字段。
        ///
        /// <para>⚠️ 判据锚在 `FieldReference.FieldType`,**不是**字段名 —— 真工程有
        /// `ClinicRoomClauseKind` / `EventKind` 两处同名 `Kind` 字段(见文件头假红记录)。
        /// 用类型判定后,那两处不再命中,而真正的 `DiseaseRegistryEntry.Kind` 仍命中。</para>
        /// </summary>
        private static bool KindLoadedWithin(IList<Instruction> instrs, int index, int window)
        {
            for (int k = Math.Max(0, index - window); k < index; k++)
            {
                if (instrs[k].OpCode == OpCodes.Ldfld || instrs[k].OpCode == OpCodes.Ldflda
                    || instrs[k].OpCode == OpCodes.Ldsfld)
                {
                    if (instrs[k].Operand is FieldReference fr
                        && fr.Name == "Kind"
                        && fr.FieldType?.FullName == DiseaseKindFullName) return true;
                }
            }
            return false;
        }

        /// <summary>该指令是否在 <paramref name="window"/> 条内装载了 `DiseaseKey` 字段。</summary>
        private static bool DiseaseKeyLoadedWithin(IList<Instruction> instrs, int index, int window)
        {
            for (int k = Math.Max(0, index - window); k < index; k++)
            {
                if (instrs[k].OpCode == OpCodes.Ldfld || instrs[k].OpCode == OpCodes.Ldsfld)
                {
                    if (instrs[k].Operand is FieldReference fr && fr.Name == "DiseaseKey") return true;
                }
            }
            return false;
        }

        /// <summary>该指令是否是对字符串相等性的调用(`String.op_Equality` / `String.Equals`)。</summary>
        private static bool IsStringEqualityCall(Instruction ins)
        {
            if (ins.OpCode != OpCodes.Call && ins.OpCode != OpCodes.Callvirt) return false;
            if (!(ins.Operand is MethodReference mr)) return false;
            if (mr.DeclaringType?.FullName != "System.String") return false;
            return mr.Name == "op_Equality" || mr.Name == "Equals";
        }

        /// <summary>
        /// 该指令是否为「整数比较」(I2 用 —— 只有紧跟它的 `ldc.i4` 才算「与 Kind 比较」)。
        /// <para>✅ 2026-10-22 加:原判据把窗口内**任意**整数常量都收进 I2,导致
        /// <c>R1_CHECK_31</c> 的异常码 31 被当成 <c>DiseaseKind</c> 成员(合法值域仅 0..3)
        /// ⇒ **假红**。收紧到「常量须紧跟比较指令」后仍有判别力:
        /// <c>Kind == DiseaseKind.X</c> 的 IL 恒为 <c>ldfld Kind; ldc.i4.X; ceq</c>。</para>
        /// </summary>
        private static bool IsComparisonOp(OpCode op) =>
            op == OpCodes.Ceq || op == OpCodes.Beq || op == OpCodes.Beq_S
            || op == OpCodes.Bne_Un || op == OpCodes.Bne_Un_S;

        /// <summary>把 `ldc.i4*` 形态取成 int(非该形态返回 null)。</summary>
        private static int? TryGetI4(Instruction ins)
        {
            OpCode op = ins.OpCode;
            if (op == OpCodes.Ldc_I4_0) return 0;
            if (op == OpCodes.Ldc_I4_1) return 1;
            if (op == OpCodes.Ldc_I4_2) return 2;
            if (op == OpCodes.Ldc_I4_3) return 3;
            if (op == OpCodes.Ldc_I4_4) return 4;
            if (op == OpCodes.Ldc_I4_5) return 5;
            if (op == OpCodes.Ldc_I4_6) return 6;
            if (op == OpCodes.Ldc_I4_7) return 7;
            if (op == OpCodes.Ldc_I4_8) return 8;
            if (op == OpCodes.Ldc_I4_M1) return -1;
            if (op == OpCodes.Ldc_I4_S) return Convert.ToInt32(ins.Operand);
            if (op == OpCodes.Ldc_I4) return Convert.ToInt32(ins.Operand);
            return null;
        }
    }
}
