// AC-11-22 / AC-11-10 / AC-10-06b 的 IL 与 metadata 执行点 —— 11 的写者独占 · 算法独占 · 载荷成对。
//
// 权威来源:
//   design/gdd/prescription-and-medication.md  AC-11-22 [B]「写者独占」
//     —— `DrugTreatmentApplied` 构造点仅在 11 程序集(IL/metadata 扫描唯一 `newobj` 站点集 ⊆ 11)
//   design/gdd/prescription-and-medication.md  AC-11-10 [B]「算法独占」
//     —— 11 不定义 `SkillMul` / `ResultMul` / `JudgeResult`
//   design/gdd/emergency-procedures.md         AC-10-06b(11/10 载荷定义交叉校验)
//   TR-prescription-018 / TR-prescription-013 / TR-prescription-003
//   ADR-009 Amendment I(写者 = 11)· ADR-024(`entities.yaml` author=11)· ADR-025 §①(装配清单)
//
// 形态与 `EmergencyIntegerGates`(AC-10-02/03)· `EcozoneIntegerGates`(AC-6-22)同族:
//   `public static class` + `List<string>` 错误列表,调用方决定 throw / assert;
//   **产物缺失 / 根找不到 / 命中数为 0 ⇒ 记红**(拒以空集冒充绿)。
//
// ⚠️ 判据面纪律(与 b5 / AC-10-02 同款教训 —— 「断言恒真、零扫描」是本工程的高发失效模式):
//   本文件每条谓词都必须能被**同一谓词**在**另一真实编译产物**上打出非空结果,
//   否则它是空转。故每条谓词都配一条 `PositiveControl*` 入口,由测试真跑真断言。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>AC-11-22 / AC-11-10 / AC-10-06b 的 IL·metadata 门。</summary>
    public static class PrescriptionWriterGates
    {
        // ── 具名常量(测试侧引用,避免字符串漂移)────────────────────────────

        /// <summary>11 侧载荷 struct 名(与 10 侧成对的左半边)。</summary>
        public const string DrugPayloadTypeName = "DrugTreatmentAppliedPayload";

        /// <summary>10 侧载荷 struct 名(成对的右半边)。</summary>
        public const string EmergencyPayloadTypeName = "EmergencyTreatmentAppliedPayload";

        /// <summary>11 写者所在装配。</summary>
        public const string SimAssemblyName = "Sim";

        /// <summary>载荷 codec 所在装配(解码路径会**重建**载荷,非写者)。</summary>
        public const string CodecAssemblyName = "Sim.Codec";

        /// <summary>11 的命名空间前缀(算法独占的扫描面)。</summary>
        public const string PrescriptionNamespacePrefix = "DaYiJingCheng.Sim.Prescription";

        /// <summary>10 的命名空间前缀(算法独占的**阳性对照**扫描面 —— 那里确有这三个符号)。</summary>
        public const string EmergencyNamespacePrefix = "DaYiJingCheng.Sim.EmergencyProcedures";

        /// <summary>AC-11-10 的三个禁名 —— 出处 = GDD 逐字点名。</summary>
        public static readonly string[] AlgorithmExclusiveForbidden =
        {
            "SkillMul",      // 10 的稳度容差乘子(11 无消费点)
            "ResultMul",     // 10 的 result_mul 三档(11 恒 Applied,乘 1 是空运算)
            "JudgeResult",   // 10 的判定结果枚举(11 无判定步)
        };

        /// <summary>
        /// AC-11-01① 的 IL 判据面 —— 11 的输入侧**零病名符号**。
        /// <para>出处:TR-prescription-001「零 diagnosis / disease_id / tier_named 引用,
        /// 零 indications 匹配逻辑」+ 规则一。反射只证**签名面**,本集证**实现面**
        /// (间接引用 / 只读一处也算) —— 两者互补,缺一即留洞。</para>
        /// </summary>
        public static readonly string[] DiseaseNameForbidden =
        {
            "Indications", "Contraindications",     // 适应症 / 禁忌(11 只呈现不拦 —— 且 P0 连呈现都不做)
            "DiseaseId", "DiseaseIdSet",            // 病种 id 与其集合
            "TierNamed",                            // 具名档位(病种级布尔)
            "Severity",                             // 严重度(病种级量,OQ-11-13 已裁归 13/9)
            "TreatableBy",                          // 9 的可治布尔(BL-3 改判后 11 不读)
            // ⚠️ **2026-10-06 结构侧评审 M-3 补入**:AC-11-01① 原文**逐字点名**该类型
            //    (GDD `prescription-and-medication.md:141`「不含 8 的 `DiagnosisResult` 类」),
            //    而本集此前漏收 ⇒ 反射半边(GDD 的①)点名的类型**根本没被断言**。
            //    当前 8 侧尚未落该类型(grep 零命中)⇒ 断言平凡通过;一旦落型即成为真判据。
            "DiagnosisResult",
        };

        /// <summary>
        /// AC-11-10 的**引用集**半边:11 的写者装配不得引用声明判定/乘子的程序集之外的东西。
        /// <para>实测口径(与 ADR-025 RC-5 同教训):没有任何 asmdef 字段能产出「恰两元素」的
        /// 引用集,基座程序集必然出现 ⇒ 判据 = 「**不含任何引擎程序集,且工程内程序集侧 ⊆
        /// {Sim.Contracts}**」,与 b2 门同款。</para>
        /// </summary>
        public static readonly string[] SimAllowedProjectReferences = { "Sim.Contracts" };

        /// <summary>
        /// 载荷成对校验里 **10 侧独有** 的字段名 —— 11/10 除这两项外字段名/类型/序数须完全一致。
        /// 出处 = `entities.yaml` 的 `DrugTreatmentApplied.payload_schema` 与
        /// `EmergencyTreatmentAppliedPayload` 的结构体声明。
        /// </summary>
        public static readonly string[] TenSideOnlyFields = { "Method", "Cause" };

        /// <summary>
        /// **生产装配内**的合法载荷构造点(写者独占的白名单)。
        /// <para>⚠️ 白名单**必须**是具名的 (装配, 类型) 对,不是前缀/通配 —— 前缀白名单
        /// 等于把整族装配永久豁免(与 b6 的「豁免须具名有界」同纪律)。</para>
        /// </summary>
        public static readonly (string Assembly, string Type)[] ProductionCtorSites =
        {
            // ① 唯一**写者**:11 的流程本体。AC-11-22 的正题。
            (SimAssemblyName, "DaYiJingCheng.Sim.Prescription.PrescribeFlow"),
        };

        /// <summary>
        /// **非写者**的合法构造点 —— 与 <see cref="ProductionCtorSites"/> **分开登记**,不混为一谈。
        /// <para>⚠️ **2026-10-06 结构侧评审 M-2 的修复**:此前把 `PayloadCodec` 塞进
        /// `ProductionCtorSites` 是**门单方面放宽 AC 原文** —— AC-11-22 / TR-prescription-018
        /// 逐字为「构造点**仅存在于 11 的程序集**」,**没有任何 read/write 区分**,
        /// 而解码路径确实在 11 之外构造该 struct(`PayloadCodec.History.cs:295`)。
        /// 把它登记为「生产白名单」等于用注释改写 AC。</para>
        /// <para>现改为**三类分离登记**:① 写者(`ProductionCtorSites`,AC-11-22 正题);
        /// ② 解码重建(本表,<b>与 AC 文本的背离在此显式记账</b>,并要求 AC 修订);
        /// ③ 测试夹具(`.Tests` 装配,自动单列)。判据仍是「构造点 ⊆ ①∪②∪③」,
        /// 但 ② 是**有账的背离**,不是静默豁免。</para>
        /// <para>⚠️ **AC 修订义务(未闭,禁借绿)**:AC-11-22 / TR-prescription-018 的原文须
        /// 由 producer / TD 裁定是否收窄为「待写入载荷的构造」;在修订落盘前,
        /// 本表是**已知的判据-文本背离**,`RunAll` 会把它作为 WARN 级摘要报出。</para>
        /// </summary>
        public static readonly (string Assembly, string Type)[] DecoderCtorSites =
        {
            // 解码重建:`PayloadCodec` 从字节还原载荷 —— 其产物是内存中的只读值,
            // 不经 `IEventSink`,不是写者。**但它确在 11 之外构造该类型** ⇒ 与 AC 字面冲突。
            (CodecAssemblyName, "DaYiJingCheng.Sim.Codec.PayloadCodec"),
        };

        // ══════════════════════════════════════════════════════════════════
        // AC-11-22 —— 写者独占:载荷构造点(IL `newobj` / `call .ctor`)站点集 ⊆ 白名单
        // ══════════════════════════════════════════════════════════════════

        /// <summary>`Library/ScriptAssemblies` 下的全部编译产物路径(按名排序,确定性)。</summary>
        public static string[] AllScriptAssemblyPaths()
        {
            string dir = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", "Library", "ScriptAssemblies"));
            if (!Directory.Exists(dir)) return Array.Empty<string>();
            return Directory.GetFiles(dir, "*.dll", SearchOption.TopDirectoryOnly)
                            .OrderBy(p => p, StringComparer.Ordinal)
                            .ToArray();
        }

        /// <summary>
        /// **工程内**装配名集合 —— 从 `Assets/**/*.asmdef` 的 JSON `name` 字段读出。
        /// <para>⚠️ 不取文件名:实测两装配文件名(`EditMode.asmdef` / `PlayMode.asmdef`)与其
        /// 声明名(`Sim.Contracts.Tests` / `Gameplay.Tests`)不一致(承 b3 门同一教训)。</para>
        /// </summary>
        public static HashSet<string> ProjectAssemblyNames()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            string assets = UnityEngine.Application.dataPath;
            if (!Directory.Exists(assets)) return names;

            foreach (var f in Directory.GetFiles(assets, "*.asmdef", SearchOption.AllDirectories))
            {
                string text;
                try { text = File.ReadAllText(f); } catch { continue; }
                var m = System.Text.RegularExpressions.Regex.Match(
                    text, "\"name\"\\s*:\\s*\"([^\"]+)\"");
                if (m.Success) names.Add(m.Groups[1].Value);
            }
            return names;
        }

        /// <summary>
        /// AC-11-22 的**扫描面** —— `Library/ScriptAssemblies` 下**属于本工程**的产物
        /// (名 ∈ <see cref="ProjectAssemblyNames"/>),按名排序。
        /// </summary>
        /// <remarks>
        /// **为何不扫全部产物**:`ScriptAssemblies` 下有 90 个 dll,含第三方 / 编辑器扩展,
        /// 对它们做 Cecil 读取会产出与判据无关的解析噪声(且「读不了」会被误当「没问题」)。
        /// 本工程装配是**构造载荷的必要面**(不引 `Sim.Contracts` 就构造不了该类型),
        /// 故以「工程装配」为界既不漏也不噪 —— 且扫描面塌缩会被
        /// <see cref="CheckDrugPayloadCtorSites"/> 的 `scannedAssemblies == 0` 判红接住。
        /// </remarks>
        public static string[] ProjectScriptAssemblyPaths()
        {
            var names = ProjectAssemblyNames();
            if (names.Count == 0) return Array.Empty<string>();
            return AllScriptAssemblyPaths()
                   .Where(p => names.Contains(Path.GetFileNameWithoutExtension(p)))
                   .ToArray();
        }

        /// <summary>装配名 → 是否测试装配(判据:名以 `.Tests` 结尾 —— 与 ADR-025 §① 的测试族命名一致)。</summary>
        public static bool IsTestAssembly(string assemblyName)
            => assemblyName != null && assemblyName.EndsWith(".Tests", StringComparison.Ordinal);

        /// <summary>
        /// AC-11-22:扫**全部编译产物**的 IL,收集 `newobj &lt;载荷 ctor&gt;`(引用类型)与
        /// `call &lt;载荷 ctor&gt;`(值类型 —— 载荷是 `struct`,此路才是实路)站点,
        /// 断言生产装配内的站点集 ⊆ <paramref name="allowed"/>。
        /// </summary>
        /// <param name="dllPaths">被扫描的产物(空 / 全缺 ⇒ 红)。</param>
        /// <param name="allowed">生产装配内的合法 (装配, 声明类型) 对。</param>
        /// <param name="scannedAssemblies">成功打开的装配数(0 ⇒ 红)。</param>
        /// <param name="hits">全部命中的 (装配, 类型) 对 —— 供测试做**双向**断言。</param>
        /// <remarks>
        /// **双向判据**(缺一即空转):
        /// ① **上界** —— 生产装配内的每个命中 ∈ 白名单;
        /// ② **下界** —— 白名单的每一条**必须实际命中**(否则白名单是死的,判据面丢失);
        /// ③ **测试装配隔离** —— `.Tests` 装配内的命中单列,不进 ① 的判定
        ///    (测试夹具构造载荷只为往返断言,不是写者);但**必须**存在至少一处
        ///    —— 否则 ③ 的隔离无从证伪(且它是 ① 的天然阳性对照面)。
        /// </remarks>
        public static List<string> CheckDrugPayloadCtorSites(
            string[] dllPaths,
            (string Assembly, string Type)[] allowed,
            out int scannedAssemblies,
            out List<(string Assembly, string Type)> hits)
        {
            var errs = new List<string>();
            scannedAssemblies = 0;
            hits = new List<(string, string)>();

            if (dllPaths == null || dllPaths.Length == 0)
            {
                errs.Add("[AC-11-22] 被扫描产物列表为空 —— 拒以空集冒充绿。");
                return errs;
            }
            if (allowed == null)
            {
                errs.Add("[AC-11-22] 白名单为 null —— 拒以「无约束」冒充绿。");
                return errs;
            }

            // 上界 = 写者白名单 ∪ **解码重建点**(分开登记,见 `DecoderCtorSites` 的记账注)。
            // 下界只对**写者白名单**成立 —— 解码点是否命中不进 ② 的判定(它不是写者)。
            var allowedSet = new HashSet<(string, string)>(allowed);
            allowedSet.UnionWith(DecoderCtorSites);
            var allowedSeen = new HashSet<(string, string)>();

            foreach (var path in dllPaths)
            {
                if (!File.Exists(path)) continue;
                AssemblyDefinition asm;
                try
                {
                    asm = AssemblyDefinition.ReadAssembly(path, new ReaderParameters
                    {
                        ReadingMode = ReadingMode.Deferred,
                        InMemory = true,
                    });
                }
                catch (Exception ex)
                {
                    errs.Add($"[AC-11-22] 产物无法读取「{Path.GetFileName(path)}」:{ex.GetType().Name} " +
                             "—— 扫描面不完整,拒以部分冒充全。");
                    continue;
                }

                using (asm)
                {
                    scannedAssemblies++;
                    string asmName = asm.Name.Name;
                    bool isTest = IsTestAssembly(asmName);

                    foreach (var type in AssemblyGates.AllTypes(asm.MainModule))
                    {
                        if (type.Name == "<Module>") continue;
                        foreach (var method in type.Methods)
                        {
                            MethodBody body;
                            try { body = method.Body; } catch { continue; }
                            if (body == null) continue;

                            foreach (var ins in body.Instructions)
                            {
                                // ⚠️ **两种构造指令都要收**:载荷是 `struct` ⇒ C# 编译器为
                                //     `new Payload(...)` 发 `call instance void .ctor(...)`,
                                //     **不是** `newobj`(后者只用于引用类型)。
                                //     只扫 `newobj` ⇒ 站点集恒空 ⇒ 白名单「零命中」误报
                                //     (2026-10-06 实测踩中:Sim.dll 内确有该构造点却零命中)。
                                bool isNewobj = ins.OpCode == OpCodes.Newobj;
                                bool isCtorCall = ins.OpCode == OpCodes.Call && ins.Operand is MethodReference mr0
                                                  && mr0.Name == ".ctor";
                                if (!isNewobj && !isCtorCall) continue;
                                var ctor = ins.Operand as MethodReference;
                                if (ctor?.DeclaringType == null) continue;
                                if (ctor.DeclaringType.Name != DrugPayloadTypeName) continue;

                                var site = (asmName, type.FullName);
                                hits.Add(site);

                                if (isTest) continue;      // ③ 测试装配单列
                                allowedSeen.Add(site);
                                if (!allowedSet.Contains(site))
                                    errs.Add($"[AC-11-22] 生产装配「{asmName}」的 " +
                                             $"{type.FullName}::{method.Name} 构造了 " +
                                             $"{DrugPayloadTypeName} —— 写者独占要求构造点 ⊆ 11" +
                                             "(AC-11-22 / TR-prescription-018)。");
                            }
                        }
                    }
                }
            }

            if (scannedAssemblies == 0)
            {
                errs.Add("[AC-11-22] 成功打开的装配数为 0 —— 拒以空集冒充绿(产物未编译 / 路径漂移)。");
                return errs;
            }

            // ② 下界:白名单每条都须真命中
            foreach (var a in allowed)
                if (!allowedSeen.Contains(a))
                    errs.Add($"[AC-11-22] 白名单条目「{a.Assembly} / {a.Type}」**零命中** —— " +
                             "白名单是死的(类型改名 / 装配未编译 / 白名单陈旧)," +
                             "拒以「无人违例」冒充绿。");

            // ③ 测试装配隔离须可证伪:必须真存在测试侧构造点
            if (!hits.Any(h => IsTestAssembly(h.Assembly)))
                errs.Add("[AC-11-22] 测试装配内零构造点 —— 隔离判据无从证伪" +
                         "(且它本是本谓词的天然阳性对照面),须复核扫描面。");

            if (hits.Count == 0)
                errs.Add("[AC-11-22] 全库零构造点 —— 拒以空集冒充绿(载荷从未被构造 = 接线断裂)。");

            return errs;
        }

        /// <summary>便捷重载:用默认白名单 + 默认产物列表。</summary>
        public static List<string> CheckDrugPayloadCtorSites(out int scannedAssemblies,
                                                            out List<(string Assembly, string Type)> hits)
            => CheckDrugPayloadCtorSites(ProjectScriptAssemblyPaths(), ProductionCtorSites,
                                         out scannedAssemblies, out hits);

        // ══════════════════════════════════════════════════════════════════
        // AC-11-10 的引用集半边 —— 11 写者装配的引用面
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// AC-11-10(引用集半边):11 写者装配的引用面须
        /// ① **零引擎程序集**(门 A / ADR-017 §二),且
        /// ② **工程内程序集侧 ⊆ <paramref name="allowedProjectRefs"/>**(ADR-025 §①)。
        /// </summary>
        /// <param name="dllPath">11 写者装配产物(`Sim.dll`)。</param>
        /// <param name="projectAssemblyNames">工程内装配名全集(用于区分工程引用与 BCL/引擎引用)。</param>
        /// <param name="allowedProjectRefs">允许的工程内引用(ADR-025 §①:恰 `Sim.Contracts`)。</param>
        /// <param name="engineRefs">命中的引擎程序集名 —— 供测试断言「引擎面**非空**」,
        /// 否则 `IsBclRef` 的判别面没被真正驱动过。</param>
        public static List<string> CheckSimReferenceFace(
            string dllPath, HashSet<string> projectAssemblyNames, string[] allowedProjectRefs,
            out List<string> engineRefs)
        {
            var errs = new List<string>();
            engineRefs = new List<string>();

            if (!File.Exists(dllPath))
            {
                errs.Add($"[AC-11-10] 产物缺失「{dllPath}」—— 引用面不可读,拒以空集冒充绿。");
                return errs;
            }
            if (projectAssemblyNames == null || projectAssemblyNames.Count == 0)
            {
                errs.Add("[AC-11-10] 工程装配名集合为空 —— 无法区分工程引用与 BCL,拒以空集冒充绿。");
                return errs;
            }

            var allowed = new HashSet<string>(allowedProjectRefs ?? Array.Empty<string>(), StringComparer.Ordinal);

            using (var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters
            {
                ReadingMode = ReadingMode.Deferred,
                InMemory = true,
            }))
            {
                foreach (var r in asm.MainModule.AssemblyReferences)
                {
                    // BCL 面由 AssemblyGates 的**唯一权威实现**给出 —— 本门不复制一份白名单
                    // (复制 = 第二真源,漂移时两处不同步)。`IsBclRef` 与 `IsEngineRef` 互斥
                    // ⇒ 先判 BCL 不会漏引擎。
                    if (AssemblyGates.IsBclRef(r.Name)) continue;

                    // 非 BCL:要么是工程内装配(受 ADR-025 §① 约束),要么是引擎程序集(门 A 禁)
                    if (projectAssemblyNames.Contains(r.Name))
                    {
                        if (!allowed.Contains(r.Name))
                            errs.Add($"[AC-11-10] 11 写者装配引用了工程内程序集「{r.Name}」—— " +
                                     $"ADR-025 §① 规定引用集恰 = {{{string.Join(", ", allowedProjectRefs ?? Array.Empty<string>())}}}。");
                    }
                    else if (AssemblyGates.IsEngineRef(r.Name))
                    {
                        engineRefs.Add(r.Name);
                    }
                    else
                    {
                        // 既非 BCL、非工程装配、非引擎前缀 —— 未登记面(第三方 / 手改 asmdef)。
                        // 不静默放行:ADR-025 §④ 的封闭性要求未登记者必须显式出现。
                        engineRefs.Add(r.Name);
                    }
                }
            }

            foreach (var e in engineRefs)
                errs.Add($"[AC-11-10] 11 写者装配引用了引擎程序集「{e}」—— " +
                         "门 A(ADR-017 §二 / ADR-025 §①)要求 `Sim` 零引擎引用。");

            return errs;
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-11-10 —— 算法独占:11 命名空间内零 SkillMul / ResultMul / JudgeResult
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// AC-11-10:扫 <paramref name="nsPrefix"/> 命名空间下的全部类型,
        /// 断言零 `SkillMul` / `ResultMul` / `JudgeResult` 符号。
        /// </summary>
        /// <param name="dllPath">被扫描产物。</param>
        /// <param name="nsPrefix">扫描面命名空间前缀。</param>
        /// <param name="forbidden">禁名集合。</param>
        /// <param name="scannedTypes">扫描到的类型数(0 ⇒ 红)。</param>
        /// <param name="scannedMethods">扫描到的方法数(0 ⇒ 红)。</param>
        /// <remarks>
        /// **五面同扫**(任一单独都不够):
        /// ① 字段类型名 / ② 方法签名(参数 + 返回)/ ③ 局部变量类型 /
        /// ④ IL 指令的 call·callvirt·newobj 目标名与声明类型名 /
        /// ⑤ IL 指令的 ldfld·stfld·ldsfld·ldtoken 字段名与声明类型名。
        /// ④ 漏「只声明不调用」,⑤ 漏「只调用不声明」;①–③ 补「名字只在签名里」。
        /// </remarks>
        public static List<string> CheckAlgorithmExclusivity(
            string dllPath, string nsPrefix, string[] forbidden,
            out int scannedTypes, out int scannedMethods)
        {
            var errs = new List<string>();
            scannedTypes = 0;
            scannedMethods = 0;

            if (!File.Exists(dllPath))
            {
                errs.Add($"[AC-11-10] 编译产物缺失「{dllPath}」—— 扫描面不存在,拒以空集冒充绿。");
                return errs;
            }
            if (string.IsNullOrEmpty(nsPrefix))
            {
                errs.Add("[AC-11-10] 命名空间前缀为空 —— 拒以全库冒充扫描面。");
                return errs;
            }
            if (forbidden == null || forbidden.Length == 0)
            {
                errs.Add("[AC-11-10] 禁名集合为空 —— 拒以「无约束」冒充绿。");
                return errs;
            }

            var banned = new HashSet<string>(forbidden, StringComparer.Ordinal);

            using (var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters
            {
                ReadingMode = ReadingMode.Deferred,
                InMemory = true,
            }))
            {
                foreach (var type in AssemblyGates.AllTypes(asm.MainModule))
                {
                    if (type.Name == "<Module>") continue;
                    if (!AssemblyGates.IsInNamespacePrefix(type.Namespace, nsPrefix)) continue;
                    scannedTypes++;

                    // ① 字段类型
                    foreach (var f in type.Fields)
                        if (f.FieldType != null && IsBannedName(f.FieldType.Name, banned))
                            errs.Add($"[AC-11-10] {type.FullName} 字段「{f.Name}」类型为被禁的 " +
                                     $"「{f.FieldType.Name}」(AC-11-10 算法独占)。");

                    foreach (var method in type.Methods)
                    {
                        scannedMethods++;

                        // ② 签名(参数 + 返回)
                        foreach (var p in method.Parameters)
                            if (p.ParameterType != null && IsBannedName(p.ParameterType.Name, banned))
                                errs.Add($"[AC-11-10] {type.FullName}::{method.Name} 参数「{p.Name}」" +
                                         $"类型为被禁的「{p.ParameterType.Name}」。");
                        if (method.ReturnType != null && IsBannedName(method.ReturnType.Name, banned))
                            errs.Add($"[AC-11-10] {type.FullName}::{method.Name} 返回类型为被禁的 " +
                                     $"「{method.ReturnType.Name}」。");

                        MethodBody body;
                        try { body = method.Body; } catch { continue; }
                        if (body == null) continue;

                        // ③ 局部变量
                        foreach (var v in body.Variables)
                            if (v.VariableType != null && IsBannedName(v.VariableType.Name, banned))
                                errs.Add($"[AC-11-10] {type.FullName}::{method.Name} 局部变量类型为" +
                                         $"被禁的「{v.VariableType.Name}」。");

                        foreach (var ins in body.Instructions)
                        {
                            // ④ 方法引用(call / callvirt / newobj)
                            var callee = ins.Operand as MethodReference;
                            if (callee != null)
                            {
                                if (IsBannedName(callee.Name, banned))
                                    errs.Add($"[AC-11-10] {type.FullName}::{method.Name} IL 调用被禁的 " +
                                             $"「{callee.DeclaringType?.Name}::{callee.Name}」。");
                                if (callee.DeclaringType != null &&
                                    IsBannedName(callee.DeclaringType.Name, banned))
                                    errs.Add($"[AC-11-10] {type.FullName}::{method.Name} IL 触及被禁类型 " +
                                             $"「{callee.DeclaringType.FullName}」。");
                            }

                            // ⑤ 字段引用(ldfld / stfld / ldsfld / ldtoken)
                            var field = ins.Operand as FieldReference;
                            if (field != null)
                            {
                                if (IsBannedName(field.Name, banned))
                                    errs.Add($"[AC-11-10] {type.FullName}::{method.Name} IL 读写被禁字段 " +
                                             $"「{field.DeclaringType?.Name}::{field.Name}」。");
                                if (field.DeclaringType != null &&
                                    IsBannedName(field.DeclaringType.Name, banned))
                                    errs.Add($"[AC-11-10] {type.FullName}::{method.Name} IL 字段声明类型为" +
                                             $"被禁的「{field.DeclaringType.FullName}」。");
                            }

                            // 类型操作数(newarr / castclass / box …)
                            var tr = ins.Operand as TypeReference;
                            if (tr != null && IsBannedName(tr.Name, banned))
                                errs.Add($"[AC-11-10] {type.FullName}::{method.Name} IL 命中被禁类型 " +
                                         $"「{tr.FullName}」。");
                        }
                    }
                }
            }

            if (scannedTypes == 0)
                errs.Add($"[AC-11-10] 命名空间前缀「{nsPrefix}」在产物内 0 命中 —— " +
                         "拒以空集冒充绿(命名空间改名 / 产物未编译)。");
            else if (scannedMethods == 0)
                errs.Add($"[AC-11-10] 前缀「{nsPrefix}」下扫描到 0 个方法 —— 拒以空集冒充绿。");

            return errs;
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-11-01① IL 半边 —— 11 输入侧零病名符号
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// AC-11-01①(IL 半边):扫 11 命名空间下全部类型的字段/签名/局部/IL 操作数,
        /// 断言零 <see cref="DiseaseNameForbidden"/> 中的符号。
        /// </summary>
        /// <remarks>
        /// ⚠️ **与反射判据互补,不是替代**:反射只看得见 `public` 签名面;
        /// `DrugProfile` 上**确实挂着** `Indications` / `Contraindications` 两个属性
        /// (21a 的字段,11 的入参类型上就有)⇒ 只要 11 的实现体里读它一次,
        /// 病名就静默进了 11 的决策 —— 反射**看不见**,本谓词看得见。
        /// 本谓词只扫**声明在 11 命名空间内**的代码,不扫 `Sim.Contracts` 的字段声明本身。
        /// </remarks>
        public static List<string> CheckDiseaseNameIl(
            string dllPath, string nsPrefix, string[] forbidden,
            out int scannedTypes, out int scannedMethods)
        {
            var errs = new List<string>();
            scannedTypes = 0;
            scannedMethods = 0;

            if (!File.Exists(dllPath))
            {
                errs.Add($"[AC-11-01①] 编译产物缺失「{dllPath}」—— 扫描面不存在,拒以空集冒充绿。");
                return errs;
            }
            if (forbidden == null || forbidden.Length == 0)
            {
                errs.Add("[AC-11-01①] 禁名集合为空 —— 拒以「无约束」冒充绿。");
                return errs;
            }

            var banned = new HashSet<string>(forbidden, StringComparer.Ordinal);

            using (var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters
            {
                ReadingMode = ReadingMode.Deferred,
                InMemory = true,
            }))
            {
                foreach (var type in AssemblyGates.AllTypes(asm.MainModule))
                {
                    if (type.Name == "<Module>") continue;
                    if (!AssemblyGates.IsInNamespacePrefix(type.Namespace, nsPrefix)) continue;
                    scannedTypes++;

                    foreach (var method in type.Methods)
                    {
                        scannedMethods++;

                        // ① 签名(参数 / 返回)
                        foreach (var p in method.Parameters)
                            if (p.ParameterType != null && IsBannedName(p.ParameterType.Name, banned))
                                errs.Add($"[AC-11-01①] {type.FullName}::{method.Name} 参数「{p.Name}」" +
                                         $"类型为病名面「{p.ParameterType.Name}」。");
                        if (method.ReturnType != null && IsBannedName(method.ReturnType.Name, banned))
                            errs.Add($"[AC-11-01①] {type.FullName}::{method.Name} 返回类型为病名面" +
                                     $"「{method.ReturnType.Name}」。");

                        MethodBody body;
                        try { body = method.Body; } catch { continue; }
                        if (body == null) continue;

                        // ② 局部变量
                        foreach (var v in body.Variables)
                            if (v.VariableType != null && IsBannedName(v.VariableType.Name, banned))
                                errs.Add($"[AC-11-01①] {type.FullName}::{method.Name} 局部变量类型为" +
                                         $"病名面「{v.VariableType.Name}」。");

                        foreach (var ins in body.Instructions)
                        {
                            // ③ 字段引用 —— **本谓词的核心面**:11 读 DrugProfile.Indications
                            //    走的就是 ldfld / call get_Indications
                            var field = ins.Operand as FieldReference;
                            if (field != null && IsBannedName(field.Name, banned))
                                errs.Add($"[AC-11-01①] {type.FullName}::{method.Name} IL 读写病名面字段 " +
                                         $"「{field.DeclaringType?.Name}::{field.Name}」" +
                                         "(TR-prescription-001:11 零 indications 匹配逻辑)。");

                            // ④ 方法引用 —— 属性访问器形如 get_Indications
                            var callee = ins.Operand as MethodReference;
                            if (callee != null)
                            {
                                if (IsBannedName(callee.Name, banned))
                                    errs.Add($"[AC-11-01①] {type.FullName}::{method.Name} IL 调用病名面 " +
                                             $"「{callee.DeclaringType?.Name}::{callee.Name}」。");
                                if (callee.DeclaringType != null &&
                                    IsBannedName(callee.DeclaringType.Name, banned))
                                    errs.Add($"[AC-11-01①] {type.FullName}::{method.Name} IL 触及病名面类型 " +
                                             $"「{callee.DeclaringType.FullName}」。");
                            }

                            var tr = ins.Operand as TypeReference;
                            if (tr != null && IsBannedName(tr.Name, banned))
                                errs.Add($"[AC-11-01①] {type.FullName}::{method.Name} IL 命中病名面类型 " +
                                         $"「{tr.FullName}」。");
                        }
                    }
                }
            }

            if (scannedTypes == 0)
                errs.Add($"[AC-11-01①] 命名空间前缀「{nsPrefix}」在产物内 0 命中 —— 拒以空集冒充绿。");
            else if (scannedMethods == 0)
                errs.Add($"[AC-11-01①] 前缀「{nsPrefix}」下扫描到 0 个方法 —— 拒以空集冒充绿。");

            return errs;
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-10-06b —— 11/10 载荷定义成对交叉校验(metadata 面)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// AC-10-06b:从 `Sim.Contracts` 产物读两个载荷 struct 的**实例字段声明序**,
        /// 断言 10 侧剔除 <paramref name="tenSideOnly"/> 后与 11 侧**逐位**同名同类型。
        /// </summary>
        /// <param name="contractsDllPath">`Sim.Contracts.dll` 路径。</param>
        /// <param name="tenSideOnly">10 侧独有的字段名(本判据的豁免集)。</param>
        /// <param name="comparedFields">实际参与逐位比较的字段数(0 ⇒ 红)。</param>
        /// <remarks>
        /// **为何逐位而非集合比较**:载荷字段的**序数**是 codec tag 的语义锚
        /// (`PayloadCodec.History.cs` 的 tag 表)—— 集合相等而序数对调 = 静默错位,
        /// 正是 AC-10-06b「漂移会静默进 9 的和式」所指的失效模式。
        /// </remarks>
        public static List<string> CheckPayloadPairing(
            string contractsDllPath, string[] tenSideOnly, out int comparedFields)
        {
            var errs = new List<string>();
            comparedFields = 0;

            if (!File.Exists(contractsDllPath))
            {
                errs.Add($"[AC-10-06b] 产物缺失「{contractsDllPath}」—— 拒以空集冒充绿。");
                return errs;
            }

            var skip = new HashSet<string>(tenSideOnly ?? Array.Empty<string>(), StringComparer.Ordinal);

            using (var asm = AssemblyDefinition.ReadAssembly(contractsDllPath, new ReaderParameters
            {
                ReadingMode = ReadingMode.Deferred,
                InMemory = true,
            }))
            {
                TypeDefinition drug = null, emergency = null;
                foreach (var t in AssemblyGates.AllTypes(asm.MainModule))
                {
                    if (t.Name == DrugPayloadTypeName) drug = t;
                    else if (t.Name == EmergencyPayloadTypeName) emergency = t;
                }

                if (drug == null)
                {
                    errs.Add($"[AC-10-06b] 11 侧载荷类型「{DrugPayloadTypeName}」在 " +
                             "Sim.Contracts 内找不到 —— 判据面丢失。");
                    return errs;
                }
                if (emergency == null)
                {
                    errs.Add($"[AC-10-06b] 10 侧载荷类型「{EmergencyPayloadTypeName}」在 " +
                             "Sim.Contracts 内找不到 —— 成对的另一半缺失,无从比对。");
                    return errs;
                }

                var a = InstanceFieldsInOrder(drug);
                var b = InstanceFieldsInOrder(emergency).Where(f => !skip.Contains(f.Name)).ToList();

                if (a.Count == 0 || b.Count == 0)
                {
                    errs.Add("[AC-10-06b] 一侧字段列表为空 —— 拒以空集冒充绿" +
                             "(struct 被改写 / 字段提取逻辑失效)。");
                    return errs;
                }

                if (a.Count != b.Count)
                    errs.Add($"[AC-10-06b] 字段数不等:11 侧 {a.Count} 项,10 侧(剔除 " +
                             $"{string.Join("/", tenSideOnly ?? Array.Empty<string>())} 后){b.Count} 项。");

                int n = Math.Min(a.Count, b.Count);
                for (int i = 0; i < n; i++)
                {
                    comparedFields++;
                    if (a[i].Name != b[i].Name)
                        errs.Add($"[AC-10-06b] 序数 {i} 字段名漂移:11「{a[i].Name}」vs 10「{b[i].Name}」" +
                                 "(序数是 codec tag 的语义锚 —— 对调 = 静默错位)。");
                    else if (a[i].Type != b[i].Type)
                        errs.Add($"[AC-10-06b] 序数 {i} 字段「{a[i].Name}」类型漂移:" +
                                 $"11「{a[i].Type}」vs 10「{b[i].Type}」。");
                }

                // 10 侧独有字段须**真的存在**(否则豁免集是死的,可能掩盖真漂移)
                foreach (var name in tenSideOnly ?? Array.Empty<string>())
                {
                    if (InstanceFieldsInOrder(emergency).All(f => f.Name != name))
                        errs.Add($"[AC-10-06b] 豁免字段「{name}」在 10 侧不存在 —— " +
                                 "豁免集陈旧(掩盖面:同名字段若已改名,本应报漂移)。");
                }
            }

            return errs;
        }

        /// <summary>
        /// 符号名是否命中禁名集 —— **带词边界**,不是裸子串。
        /// </summary>
        /// <remarks>
        /// <para>⚠️ 为何不裸 `Contains`:GDD 点名的 `SkillMul` 在 10 侧的**真实**声明是
        /// `ComputeSkillMul`(PascalCase 复合名)—— 裸子串比对会在 `ResultMultiplier`
        /// 这类无关名上误伤;而**只认精确名**又会漏掉 `ComputeSkillMul`(它正是
        /// 「11 定义了 SkillMul 求值」的违规形态)。</para>
        /// <para>故左边界 = 名首 / 非标识符字符 / **PascalCase 复合边界**
        /// (前一字符小写 且 命中首字符大写);右边界 = 名尾 / 非标识符字符。
        /// 实测三例:`ResultMul`(精确)✓ · `ComputeSkillMul`(复合)✓ ·
        /// `ResultMultiplier`(无关)✗。</para>
        /// <para>⚠️ **2026-10-06 结构侧评审 B-1 修复 —— 属性访问器面**:
        /// `DrugProfile.Indications` 是 **auto-property**(`Sim.Contracts/ItemDatabase/DrugProfile.cs:39`),
        /// 11 读它编译为 `call ... get_Indications()`,该调用名**不含**任何词边界
        /// (`get_` 与 `Indications` 之间是 `_` —— 标识符字符,且前一字符非小写)
        /// ⇒ 原谓词对**属性调用点恒假阴性**。调用点 IL 也**不引用** backing field
        /// (那是 getter 自己体内的事)⇒ 字段面同样够不到。
        /// **这正是 AC-11-01① 点名的威胁形态**,故须显式展开 `get_` / `set_` 前缀。</para>
        /// </remarks>
        public static bool IsBannedName(string name, HashSet<string> banned)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (banned.Contains(name)) return true;          // 精确名(如 JudgeResult 类型名)

            // 属性访问器展开:C# 编译器把 `x.Indications` 编成 `get_Indications()`,
            // 把 `x.Indications = v` 编成 `set_Indications(v)`。二者都是**标识符**,
            // 词边界规则够不到 ⇒ 先剥前缀再判。
            // ⚠️ 只剥**一次**且只在名首 —— `get_get_X` 这种非编译器产物不该被展开。
            foreach (var prefix in AccessorPrefixes)
                if (name.StartsWith(prefix, StringComparison.Ordinal))
                    return banned.Contains(name.Substring(prefix.Length));

            foreach (var b in banned)
            {
                int at = name.IndexOf(b, StringComparison.Ordinal);
                while (at >= 0)
                {
                    bool leftOk = at == 0
                                  || !IsIdentChar(name[at - 1])
                                  || (char.IsLower(name[at - 1]) && char.IsUpper(name[at]));
                    int end = at + b.Length;
                    bool rightOk = end == name.Length || !IsIdentChar(name[end]);
                    if (leftOk && rightOk) return true;
                    at = name.IndexOf(b, at + 1, StringComparison.Ordinal);
                }
            }
            return false;
        }

        /// <summary>C# 属性访问器的编译器生成前缀(`get_` / `set_`)—— 见 <see cref="IsBannedName"/> 的 B-1 注。</summary>
        private static readonly string[] AccessorPrefixes = { "get_", "set_" };

        private static bool IsIdentChar(char c)
            => char.IsLetterOrDigit(c) || c == '_';

        /// <summary>按**声明序**取实例字段(排除编译器生成的 backing field 与静态字段)。</summary>
        private static List<(string Name, string Type)> InstanceFieldsInOrder(TypeDefinition t)
        {
            var list = new List<(string, string)>();
            foreach (var f in t.Fields)
            {
                if (f.IsStatic) continue;
                if (f.Name.Contains("k__BackingField")) continue;
                if (f.IsPrivate && f.Name.StartsWith("<", StringComparison.Ordinal)) continue;
                list.Add((f.Name, f.FieldType?.FullName ?? "?"));
            }
            return list;
        }

        // ══════════════════════════════════════════════════════════════════
        // 汇总入口(与 AssemblyGates.RunMenu 同格:构建前 fail-fast / 菜单 / 测试三处驱动)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 跑本文件全部谓词。返回错误列表(空 = 全过)。
        /// </summary>
        /// <param name="summary">逐条判据的实测计数 —— 供调用方打印**扫描面证据**
        /// (只有「0 条错误」而无「扫了多少」的日志 = 无法区分「全过」与「没跑」)。</param>
        public static List<string> RunAll(out List<string> summary)
        {
            var errs = new List<string>();
            summary = new List<string>();

            string simDll = AssemblyGates.ScriptAssemblyPath(SimAssemblyName);
            string codecDll = AssemblyGates.ScriptAssemblyPath(CodecAssemblyName);
            string contractsDll = AssemblyGates.ScriptAssemblyPath("Sim.Contracts");

            // ① AC-11-22 写者独占
            errs.AddRange(CheckDrugPayloadCtorSites(out int asmCount, out var hits));
            summary.Add($"[AC-11-22] 扫描装配 {asmCount} 个 · 构造点 {hits.Count} 处 " +
                        $"({string.Join(", ", hits.Select(h => h.Assembly + "/" + h.Type.Split('.').Last()).Distinct())})");

            // ② AC-11-10 算法独占 + 引用集
            errs.AddRange(CheckAlgorithmExclusivity(
                simDll, PrescriptionNamespacePrefix, AlgorithmExclusiveForbidden,
                out int aTypes, out int aMethods));
            summary.Add($"[AC-11-10] 算法面 {PrescriptionNamespacePrefix}:类型 {aTypes} · 方法 {aMethods}");

            errs.AddRange(CheckSimReferenceFace(
                simDll, ProjectAssemblyNames(), SimAllowedProjectReferences, out var engineRefs));
            summary.Add($"[AC-11-10] 引用面:引擎/未登记 {engineRefs.Count} 个");

            // ③ AC-11-01① IL 半边
            errs.AddRange(CheckDiseaseNameIl(
                simDll, PrescriptionNamespacePrefix, DiseaseNameForbidden,
                out int dTypes, out int dMethods));
            summary.Add($"[AC-11-01①] 病名面 {PrescriptionNamespacePrefix}:类型 {dTypes} · 方法 {dMethods}");

            // ④ AC-10-06b 载荷成对
            errs.AddRange(CheckPayloadPairing(contractsDll, TenSideOnlyFields, out int compared));
            summary.Add($"[AC-10-06b] 逐位比较字段 {compared} 项(10 侧豁免 {string.Join("/", TenSideOnlyFields)})");

            // ⚠️ 扫描面塌缩守卫 —— 任一面为 0 即红(「没跑」不得伪装成「全过」)
            if (asmCount == 0) errs.Add("[PrescriptionWriterGates] 扫描装配数为 0 —— 门未真正执行。");
            if (aMethods == 0) errs.Add("[PrescriptionWriterGates] 算法面方法数为 0 —— 门未真正执行。");
            if (dMethods == 0) errs.Add("[PrescriptionWriterGates] 病名面方法数为 0 —— 门未真正执行。");
            if (compared == 0) errs.Add("[PrescriptionWriterGates] 载荷比较字段数为 0 —— 门未真正执行。");

            // ⑤ **判据-文本背离的显式记账**(结构侧评审 M-2)——
            //    解码路径在 11 之外构造该载荷,而 AC-11-22 / TR-prescription-018 的字面是
            //    「构造点**仅存在于 11 的程序集**」。本门以 `DecoderCtorSites` 放行它,
            //    但**不静默**:摘要里带 ⚠️,构建日志可见。
            //    ⚠️ 这**不是** WARN 级放行 —— 若 AC 修订后要求收紧,须把该表清空
            //    (清空即红:下界会发现白名单条目零命中)。
            summary.Add($"[AC-11-22] ⚠️ 判据-文本背离:解码重建点 {DecoderCtorSites.Length} 处" +
                        $"({string.Join(", ", DecoderCtorSites.Select(d => d.Assembly + "/" + d.Type.Split('.').Last()))})" +
                        " 在 11 之外构造该载荷 —— AC-11-22 / TR-prescription-018 原文为「仅存在于 11」," +
                        "收窄裁定未落盘(归 producer / TD)。");

            return errs;
        }

        /// <summary>便捷重载:只要错误列表。</summary>
        public static List<string> RunAll() => RunAll(out _);

    }
}
