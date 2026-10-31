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
// ⚠️ **残余缺口(不借绿)**:源级看不见 IL 层的 `conv.r4` / `conv.r8` —— 常量折叠或算术产生的
//   非字面量浮点不经源码文本。本仓的更强先例是 `EcozoneIntegerGates`(Mono.Cecil IL 级)。
//   **本批刻意不引入 IL 面** —— 17 的求值路径当前**零浮点算术**(`Fix` = Q16.16 raw long 整数比较)。
//   升级触发条件 = 17 的数值轮引入任何可能产生浮点的算术时,按 `EcozoneIntegerGates` 形状扩列。

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

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
    }
}
