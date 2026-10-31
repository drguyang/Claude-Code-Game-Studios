// story-005 · AC-17-10 —— 17(采集)求值路径的**零浮点**静态门。
//
// 权威来源:
//   design/gdd/foraging.md AC-17-10(BLOCKING):
//     「GIVEN F-17-1 / F-17-2 / F-17-3 的求值路径,WHEN Roslyn 字面量扫描 + 类型签名断言,
//      THEN sim 字段零 float/double、零浮点字面量;⚠️ FixParse 在烘焙期,不在运行期」
//   story production/epics/foraging/story-005-action-atomicity-failure-convergence.md
//     Implementation Note 5:「复用 item-database story-011 已建的扫描件」
//   ADR-006(定点域边界)· ADR-014 §三(FixParse 只在烘焙期执行,玩家构建零解析器)
//
// ── 与 AC 措辞的一处**收窄**(如实登记)──
// AC 写「Roslyn 字面量扫描」。本门实际是**注释/字符串剥离后的文本扫描**(token + 正则),
// 与 `MedConsFloatGate`(AC-53-03)/ `InputBoundaryGates` 同族。取文本扫描而非 Roslyn 语法树:
// ① 本仓既有先例即此形(§story-005 note 5 指的「已建扫描件」就是它);
// ② 判据是「零 float/double **字面量与 token**」,文本面对该判据**偏安全**(误报可见、漏报为零);
// ③ 引 Roslyn 会让本门依赖编译器包,而门在**编辑期**跑、不产生产物。
// ── 两级扫描:源级(快)+ IL 级(强)────────────────────────────────────────
// 源级看得见「写出来的」浮点;看不见 **IL 层的 `conv.r4` / `conv.r8`** —— 常量折叠、跨方法内联、
// 库调用产生的非字面量浮点都不经源码文本。
// 🔁 **2026-10-31 用户裁定「现在修复」**:补齐 IL 面(`CheckIl`,Mono.Cecil,与
//   `EcozoneIntegerGates` / `CheckAudioAssemblyIl` 同法)。⇒ 原「残余缺口」**已关闭**。
//   源级仍保留:它是**更早、更便宜**的一道(编译前即可跑,IL 面要等 DLL)。
// ⚠️ 关于 AC 措辞里的「Roslyn」:IL 级**严格强于** Roslyn 源级(后者同样看不见 IL 里的
//   `conv.r4`,也不跨装配看内联)。取 IL 面 = **加强**,不是削减;此差异如实登记在 story 里。

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEditor;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>17 采集 —— 求值路径零浮点字面量 / 零浮点 token 的静态门(AC-17-10)。</summary>
    public static class ForagingFloatGate
    {
        /// <summary>扫描目录(相对工程根 `unity/`;17 的实现落点,单一出处)。</summary>
        public const string ForagingDirRelative = "Assets/Sim/Foraging";

        /// <summary>浮点类禁名 token(AC-17-10)。</summary>
        /// <remarks>
        /// ⚠️ 与 `MedConsFloatGate.FloatTokens` 同口径但**各自持有** —— 两门扫的是不同目录、
        /// 判的是不同 AC;共享一份数组会让「改 53 的词表」静默改掉 17 的判据。
        /// 共用的是**实现管线**(<see cref="MedConsFloatGate.StripComments"/> /
        /// <see cref="MedConsFloatGate.FindFloatLiterals"/>),那才是应当唯一的东西。
        /// </remarks>
        public static readonly string[] FloatTokens =
        {
            "float", "double", "Single", "Double", "decimal", "Decimal",
        };

        /// <summary>
        /// 扫描整个 `Assets/Sim/Foraging`(递归)。空清单 = 绿。
        /// </summary>
        /// <param name="unityProjectRoot">工程根(`unity/`);与 `MedConsFloatGate` 同为相对 `.`。
        /// </param>
        public static List<string> RunAll(string unityProjectRoot)
        {
            var errs = new List<string>();
            string dir = Path.Combine(unityProjectRoot, ForagingDirRelative);
            if (!Directory.Exists(dir))
            {
                errs.Add($"[foraging] 扫描目录不存在:{dir}(17 实现落点被移?门须同步改口径)");
                return errs;
            }

            // 递归:17 若分子目录(如 Foraging/Data/)而门走 TopDirectoryOnly,那一支会**静默**漏扫。
            foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                foreach (string e in ScanSource(File.ReadAllText(file)))
                    errs.Add($"[foraging·AC-17-10] {Path.GetFileName(file)} {e}");
            return errs;
        }

        /// <summary>
        /// 纯函数:扫一段**源文本**,返回违规描述。空清单 = 该文本合规。
        /// </summary>
        /// <remarks>
        /// <para>与 <see cref="RunAll"/> 走**同一条管线** —— 测试直驱本方法即可证明门有判别力,
        /// 不必往仓库里写违规文件(那会撞 `FsWriteBoundaryGate`)。</para>
        /// <para>两份扫描面刻意不同(承 `MedConsFloatGate` 的口径):
        /// token 面**保留字符串**(偏安全);字面量面**剥掉字符串**(`"F-17.2"` 是文档引用,
        /// 不是 C# 浮点字面量)。</para>
        /// </remarks>
        public static List<string> ScanSource(string source)
        {
            var errs = new List<string>();
            if (string.IsNullOrEmpty(source)) return errs;

            string tokenText = MedConsFloatGate.StripComments(source);
            foreach (string tok in FloatTokens)
                if (Regex.IsMatch(tokenText, $@"\b{Regex.Escape(tok)}\b"))
                    errs.Add($"出现浮点 token「{tok}」—— 17 求值路径以 Q16.16 `Fix` 承载,零浮点(AC-17-10)");

            foreach (string lit in MedConsFloatGate.FindFloatLiterals(source))
                errs.Add($"出现浮点字面量「{lit}」—— 同上;固定小数须走 `Fix.FromRational`(ADR-006)");

            return errs;
        }

        /// <summary>装配内被本门判决的命名空间根(17 的实现落点)。</summary>
        public const string SimForagingRoot = "DaYiJingCheng.Sim.Foraging";

        /// <summary>浮点相关 IL 指令(与 `EcozoneIntegerGates.ForbiddenFloatOpcodes` 同集)。</summary>
        private static readonly OpCode[] ForbiddenFloatOpcodes =
        {
            OpCodes.Conv_R4, OpCodes.Conv_R8, OpCodes.Ldc_R4, OpCodes.Ldc_R8,
        };

        /// <summary>
        /// IL 级:扫 `Sim` 装配里 `DaYiJingCheng.Sim.Foraging*` 的**全部方法体 + 字段/签名类型**。
        /// </summary>
        /// <param name="dllPath">`Sim.dll` 绝对路径(`AssemblyGates.ScriptAssemblyPath("Sim")`)。</param>
        /// <remarks>
        /// <para><b>为什么必须有这一级</b>:源级判据看不见 `conv.r4` / `conv.r8` —— 常量折叠、
        /// 库调用、跨方法内联产生的非字面量浮点**不经源码文本**。只跑源级 = 对这类泄漏失明。</para>
        /// <para><b>空集不算绿</b>:DLL 缺失 / 扫不到任何类型 / 根为空,一律报错 ——
        /// 否则「门开了但没扫到东西」会以绿的形式留下。</para>
        /// </remarks>
        public static List<string> CheckIl(string dllPath)
        {
            var errs = new List<string>();
            if (string.IsNullOrEmpty(dllPath) || !File.Exists(dllPath))
            {
                errs.Add($"[AC-17-10] 编译产物缺失「{dllPath}」—— 扫描面不存在,拒以空集冒充绿。");
                return errs;
            }

            using (var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters
            {
                // 与 EcozoneIntegerGates 同款:Deferred 才不触 Resolve(Immediate 会抛解析异常)
                ReadingMode = ReadingMode.Deferred,
                InMemory = true,
            }))
            {
                int scanned = 0;
                foreach (ModuleDefinition module in asm.Modules)
                    foreach (TypeDefinition type in module.Types)
                        scanned += ScanIlType(type, errs);

                if (scanned == 0)
                    errs.Add($"[AC-17-10] 在「{Path.GetFileName(dllPath)}」里一个 `{SimForagingRoot}*` " +
                             "类型都没扫到 —— 扫描面为空,拒以空集冒充绿(命名空间被改名?)");
            }

            return errs;
        }

        private static int ScanIlType(TypeDefinition type, List<string> errs)
        {
            int n = 0;
            if (type.FullName.StartsWith(SimForagingRoot, System.StringComparison.Ordinal))
            {
                n++;
                foreach (FieldDefinition f in type.Fields)
                    if (IsFloatish(f.FieldType))
                        errs.Add($"[AC-17-10·IL] {type.FullName}.{f.Name} 字段类型 = {f.FieldType.FullName}");

                foreach (MethodDefinition m in type.Methods)
                {
                    if (IsFloatish(m.ReturnType))
                        errs.Add($"[AC-17-10·IL] {type.FullName}.{m.Name} 返回类型 = {m.ReturnType.FullName}");
                    foreach (ParameterDefinition p in m.Parameters)
                        if (IsFloatish(p.ParameterType))
                            errs.Add($"[AC-17-10·IL] {type.FullName}.{m.Name}({p.Name}) 参数类型 = {p.ParameterType.FullName}");
                    if (!m.HasBody) continue;
                    foreach (VariableDefinition v in m.Body.Variables)
                        if (IsFloatish(v.VariableType))
                            errs.Add($"[AC-17-10·IL] {type.FullName}.{m.Name} 局部变量 = {v.VariableType.FullName}");
                    foreach (Instruction ins in m.Body.Instructions)
                        foreach (OpCode op in ForbiddenFloatOpcodes)
                            if (ins.OpCode == op)
                                errs.Add($"[AC-17-10·IL] {type.FullName}.{m.Name} 出现浮点指令 `{op.Name}`");
                }
            }

            foreach (TypeDefinition nested in type.NestedTypes) n += ScanIlType(nested, errs);
            return n;
        }

        private static bool IsFloatish(TypeReference t)
            => t != null && (t.FullName == "System.Single" || t.FullName == "System.Double"
                             || t.FullName == "System.Decimal");

        /// <summary>
        /// 生产入口:源级 + IL 级两级一起跑。**编译失败时拒扫旧 DLL**(假绿防护)。
        /// </summary>
        public static List<string> RunAllWithIl(string unityProjectRoot)
        {
            var errs = new List<string>(RunAll(unityProjectRoot));
            if (EditorUtility.scriptCompilationFailed)
            {
                errs.Add("[AC-17-10·IL] scriptCompilationFailed = true ⇒ 拒扫陈旧产物" +
                         "(读上一版 DLL = 假绿;红在编译器,本门只声明不可判)。");
                return errs;
            }
            errs.AddRange(CheckIl(AssemblyGates.ScriptAssemblyPath(EcozoneIntegerGates.SimAssemblyName)));
            return errs;
        }
    }
}
