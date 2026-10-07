// diagnosis-system —— AC-8-35 常量金标的**共享扫描真源**(story-002 建立,story-003 扩面)。
//
// 权威来源:
//   GDD design/gdd/diagnosis-system.md AC-8-35(加一条体征 ⇒ 全部全局常量表与 F-8.1/F-8.3
//     参数逐位不变)· Story 002(Q-MAJOR-1 内容金标)· Story 003(Implementation Note 1
//     「表哈希进 story 002 的哈希不变判据」+ 承 Q-MAJOR-2 的「F-8.1 参数半边」)
//   ADR-026(DIAG_TIERS 数值归 30 · FixPow 定表先例)
//
// ⚠️ **本件是唯一扫描真源**:`sign_table_test`(story 002)与 `read_floor_slots_test`(story 003)
//   共用 `BuildConstantLines` / `Fnv1aHex` / `GoldenConstantsHash` —— 两处各持一份会漂移,
//    且「扩面」必须一次改一处(story-002 头注承诺的「扩金标」在此兑现)。
//
// ⚠️ 扫描面 = 前缀命名空间 `…Presentation.Diagnosis` 下的**字面量 / 静态只读字段**
//    (含枚举字面 —— 枚举成员是 IsLiteral)+ 30 侧 `DIAG_TIERS`(耦合常量的另一半)。
//    故 story-003 新增的 `DiagnosisSlot` / `SignReadState` 枚举字面与
//    `DiagnosisChannelMaskMap` 的 static readonly 数组**自动入面** ⇒ 金标必有意识重钉。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiagnosisSystem
{
    /// <summary>AC-8-35 常量金标的共享扫描器(story 002 建立;story 003 扩面重钉)。
    /// <para>FNV-1a 32 over 有序行 UTF8;扫描面 = 前缀 ns 常量 + DIAG_TIERS。</para></summary>
    internal static class DiagnosisGoldenScan
    {
        /// <summary>与 <c>DiagnosisBoundaryGates.DiagnosisModuleNamespacePrefix</c> 同值
        /// (避免引用 Gates 程序集耦合测试面)。</summary>
        public const string Prefix = "DaYiJingCheng.Gameplay.Presentation.Diagnosis";

        /// <summary>AC-8-35 常量表金标。
        /// <para>⚠️ 值随**前缀侧任何字面量 / 静态只读字段 / 枚举字面 / DIAG_TIERS** 变化 ——
        /// 数值轮 / 结构变更须**有意识地重钉**(过 /design-review),不得顺手重钉。</para>
        /// <para>⚠️ **扫描面 = 前缀 ns 代码常量**(字面量 / static readonly,含枚举字面)+ DIAG_TIERS;
        /// **不含** `assets/data/*.json` 的**曲线数据参数**(BASE_READ / READ_FLOOR_MIN /
        /// READ_GAMMA 等)—— 那些由产物 ConfigVersion(内容哈希)覆盖。故 AC-8-35 的
        /// 「F-8.1/F-8.3 **参数**半边」**仍 NOT-RUN**(见 read_floor_slots_test 头注)。</para>
        /// <para>历史:<c>b9354110</c>(story-002 建立)→ <c>5bba361c</c>(story-003 首轮:扩
        /// **运行期枚举 / 映射常量**面 —— DiagnosisSlot / SignReadState 枚举字面 +
        /// DiagnosisChannelMaskMap 静态数组)→ <c>f75a8170</c>(story-003 评审修复轮:
        /// **代码常量修正** —— DiagnosisReadFloorCookedCodec.FixedHeadBytes 36 → 32,
        /// 该常量属前缀 ns 代码常量故入面;有意识重钉)→ <c>a7bfec31</c>(story-004:
        /// **新族落地** —— 四个新前缀 ns 常量自动入面:
        /// <c>DiagnosisNegativeConfidenceCookedCodec.ExpectedSchemaVersion=1</c> /
        /// <c>…CookedCodec.FixedHeadBytes=48</c> /
        /// <c>DiagnosisNegativeConfidenceEvaluator.NeverExcludes=-1</c> /
        /// <c>DiagnosisNegativeConfidenceTable.Q16One=65536</c>)→ <c>72db379c</c>(story-004
        /// **评审修复轮**:消重 —— 求值器自持的第二份 <c>Q16One</c> 副本删除(经
        /// <c>Table.RawToFloat</c> 复用),前缀 ns 常量**少一行**故重钉;
        /// 该行的消失即「唯一换算出口」的物化面,非数值轮产物)→ <c>7ce0ce27</c>(story-005:
        /// **新枚举面落地** —— 读数状态机扩前缀 ns 枚举字面:<c>ReadingForm</c> 四值 +
        /// <c>JudgmentState</c> 三值自动入面,常量行集 +7 ⇒ 有意识重钉)→ 本值(story-006:
        /// **词表路由枚举落地** —— <c>PatientCoarseState</c> 四值 +
        /// <c>VisitRoute</c> 四值自动入面,常量行集 +8 ⇒ 有意识重钉;
        /// 实测 <c>b711a17c</c>,2026-10-08)。</para></summary>
        public const string GoldenConstantsHash = "b711a17c";

        /// <summary>常量行集:住前缀类型的字面量 / 静态只读字段(含枚举字面),
        /// 加一行「30 侧诊断档位」(耦合常量的另一半),按 ordinal 排序。</summary>
        public static List<string> BuildConstantLines()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Gameplay.Presentation");
            Assert.That(asm, Is.Not.Null, "Gameplay.Presentation 装配须已加载");

            Type[] all;
            try
            {
                all = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                all = ex.Types.Where(t => t != null).ToArray();
            }

            var lines = new List<string>();
            foreach (Type t in all)
            {
                if (t == null || !InPrefix(t)) continue;
                if (t.Name.IndexOf('<') >= 0 || t.Name.IndexOf('>') >= 0) continue;  // 编译器生成

                foreach (FieldInfo f in t.GetFields(
                    BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static))
                {
                    if (f.Name.IndexOf('<') >= 0 || f.Name.IndexOf('>') >= 0) continue;
                    if (!(f.IsLiteral || (f.IsStatic && f.IsInitOnly))) continue;
                    lines.Add($"{t.FullName}.{f.Name}={FormatConst(f)};");
                }
            }

            // 30 侧 DIAG_TIERS(与 SLOT_BOUNDS 耦合的常量另一半;30 的 GDD 数值层)
            int[] tiers = SkillTuningTable.Default.GetDiagTiers();
            lines.Add(
                $"{typeof(SkillTuningTable).FullName}." +
                $"{nameof(SkillTuningTable.GetDiagTiers)}={string.Join(",", tiers)};");

            lines.Sort(StringComparer.Ordinal);
            return lines;
        }

        /// <summary>前缀命名空间判定(嵌套类型回溯声明类型)。</summary>
        public static bool InPrefix(Type t)
        {
            while (t != null && string.IsNullOrEmpty(t.Namespace)) t = t.DeclaringType;
            if (t == null) return false;
            string ns = t.Namespace;
            return ns == Prefix || ns.StartsWith(Prefix + ".", StringComparison.Ordinal);
        }

        /// <summary>常量字面格式化(类型回落只记类型名 —— 状态值不进金标)。</summary>
        public static string FormatConst(FieldInfo f)
        {
            object v = f.GetValue(null);
            if (v == null) return "";
            if (v is int[] arr)
                return string.Join(",", Array.ConvertAll(arr,
                    x => x.ToString(CultureInfo.InvariantCulture)));
            if (f.FieldType.IsEnum)
                return Convert.ToInt64(v, CultureInfo.InvariantCulture)
                    .ToString(CultureInfo.InvariantCulture);
            if (v is bool b) return b ? "True" : "False";
            if (v is IFormattable form) return form.ToString(null, CultureInfo.InvariantCulture);
            // 兜底(QA MINOR-9):非 IFormattable 只记**类型名**(对象状态值不进金标)。
            return "<" + f.FieldType.FullName + ">";
        }

        /// <summary>FNV-1a 32(UTF8 字节流;固定初值/素数,跨平台确定)。</summary>
        public static string Fnv1aHex(IEnumerable<string> lines)
        {
            uint hash = 2166136261u;
            foreach (string line in lines)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(line);
                foreach (byte b in bytes)
                {
                    hash ^= b;
                    hash *= 16777619u;
                }
            }
            return hash.ToString("x8", CultureInfo.InvariantCulture);
        }
    }
}
