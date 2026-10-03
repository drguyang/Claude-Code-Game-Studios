// AC-10-02 / AC-10-03 的 IL 执行点 —— 10 判定路径的整数域与边界纯净度。
//
// 权威来源:
//   design/gdd/emergency-procedures.md:1012  AC-10-02 [B]
//     「GIVEN 3 与 10 的程序集,WHEN asmdef 白名单 + IL 扫描,
//       THEN 3 侧**零** Judge / JudgeResult / SimEvent 引用(规则三)」
//   design/gdd/emergency-procedures.md:1013  AC-10-03 [B]
//     「GIVEN 10 的判定路径,WHEN 静态检查,THEN **零浮点字面量**(门 A / ADR-006)」
//   GDD:160 「由 AC-10-02 的 asmdef 白名单 + IL 扫描断言」
//   ADR-006 §三(定点域)· ADR-025 §①(程序集清单)· ADR-011(3 的输入边界)
//
// ⚠️ 2026-10-03 —— 本门补上一处**判据空转**(评审 A1/A2):
//   原 `reading_contract_test.cs:168-177` 的 AC-10-02 断言是
//     `inputAssembly.GetName().Name.Contains("Input")` —— 而 `inputAssembly` 取的是
//     `Sim.Contracts`,**该名永不含 "Input"** ⇒ 断言**恒真、零扫描**;
//   原 `:154-166` 的 AC-10-03 与 `:20-38` 的字段类型测**逐字重复**,无任何操作码检查。
//   ⇒ 两条 BLOCKING AC 此前**未被真正执行**。本门为其提供**真判据**。
//
// 形态与 `EcozoneIntegerGates`(AC-6-22)同族:
//   `public static class` + `IReadOnlyList<string>` 错误列表,由调用方决定 throw / assert;
//   **产物缺失 / 根找不到 / 闭包为空 ⇒ 记红**(拒以空集冒充绿)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>AC-10-02 / AC-10-03 的 IL 门。</summary>
    public static class EmergencyIntegerGates
    {
        /// <summary>3 侧(输入)程序集 —— AC-10-02 的被扫描面。</summary>
        public const string InputAssemblyName = "Gameplay.Input";

        /// <summary>10 侧(判定实现)程序集 —— AC-10-03 的被扫描面。</summary>
        public const string SimAssemblyName = "Sim";

        /// <summary>
        /// AC-10-02 的**零引用**判据面:3 侧不得出现这三者。
        /// 出处 = GDD:1012 逐字点名。
        /// </summary>
        public static readonly string[] ForbiddenInInput =
        {
            "JudgeResult",   // 10 的判定结果枚举
            "Judge",         // 10 的判定入口(JudgeEvaluator.Judge)
            "SimEvent",      // 边界程序集的事件类型(3 不应见)
        };

        /// <summary>AC-10-03 的浮点 IL 指令(与 `EcozoneIntegerGates` 同口径四枚)。</summary>
        private static readonly OpCode[] ForbiddenFloatOpcodes =
        {
            OpCodes.Conv_R4, OpCodes.Conv_R8,
            OpCodes.Ldc_R4, OpCodes.Ldc_R8,
        };

        /// <summary>浮点类型全名(含别名)。</summary>
        private static readonly string[] FloatTypeFullNames = { "System.Single", "System.Double" };

        /// <summary>
        /// 10 判定路径的**扫描根** —— AC-10-03 的判据面。
        /// 覆盖 GDD 规则四(F-10.4 强度)· F-10.5 的平局口径 · F-10.2(熟练度修正)·
        /// Judge 三扇门本体。
        /// </summary>
        public static readonly string[] JudgeRoots =
        {
            "DaYiJingCheng.Sim.EmergencyProcedures.JudgeEvaluator::Judge",
            "DaYiJingCheng.Sim.EmergencyProcedures.JudgeEvaluator::ScaleFixed",
            "DaYiJingCheng.Sim.EmergencyProcedures.JudgeEvaluator::ComputeJitter",
            "DaYiJingCheng.Sim.EmergencyProcedures.JudgeEvaluator::ComputeSkillMul",
        };

        // ══════════════════════════════════════════════════════════════════
        // AC-10-02 —— 3 侧零 Judge / JudgeResult / SimEvent 引用
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// AC-10-02:扫 3 侧程序集的**引用面 + 全类型 IL**,断言零 `Judge` / `JudgeResult` / `SimEvent`。
        /// </summary>
        /// <remarks>
        /// **双向判据**(缺一即空转):
        /// ① **引用面** —— `AssemblyDefinition.MainModule.AssemblyReferences` 不得含
        ///    声明这三者的程序集(`Sim` / `Sim.Contracts`);
        /// ② **IL 面** —— 全类型全方法体的 `call`/`callvirt`/`newobj` 操作数不得解析到这三个名字。
        /// 仅 ① 会漏「同名自造类型」;仅 ② 会漏「只经引用未直接调用」。
        /// </remarks>
        public static List<string> CheckInputBoundaryIl(string dllPath, out int scannedMethods)
        {
            var errs = new List<string>();
            scannedMethods = 0;

            if (!File.Exists(dllPath))
            {
                errs.Add($"[AC-10-02] 编译产物缺失「{dllPath}」—— 扫描面不存在,拒以空集冒充绿。");
                return errs;
            }

            using (var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters
            {
                ReadingMode = ReadingMode.Deferred,   // 同 EcozoneIntegerGates:Immediate 会 AssemblyResolutionException
                InMemory = true,
            }))
            {
                // ① 引用面
                foreach (var r in asm.MainModule.AssemblyReferences)
                {
                    if (r.Name == "Sim" || r.Name == "Sim.Contracts")
                        errs.Add($"[AC-10-02] 3 侧引用了「{r.Name}」—— " +
                                 "GDD:1012 要求 3 侧零 Judge/JudgeResult/SimEvent 引用" +
                                 "(该程序集是这三者的声明处;经它即可能见)。");
                }

                // ② IL 面:全类型全方法扫 call 目标名
                foreach (var type in AssemblyGates.AllTypes(asm.MainModule))
                {
                    if (type.Name == "<Module>") continue;
                    foreach (var method in AllMethods(type))
                    {
                        MethodBody body;
                        try { body = method.Body; } catch { continue; }
                        if (body == null) continue;
                        scannedMethods++;

                        foreach (var ins in body.Instructions)
                        {
                            MethodReference callee = null;
                            if (ins.OpCode == OpCodes.Call || ins.OpCode == OpCodes.Callvirt ||
                                ins.OpCode == OpCodes.Newobj)
                                callee = ins.Operand as MethodReference;
                            else if (ins.OpCode == OpCodes.Newarr)
                                callee = null;

                            if (callee != null)
                            {
                                foreach (var banned in ForbiddenInInput)
                                {
                                    if (callee.Name == banned ||
                                        callee.DeclaringType?.Name == banned)
                                        errs.Add($"[AC-10-02] {type.FullName}::{method.Name} " +
                                                 $"IL 命中被禁符号「{banned}」" +
                                                 $"({callee.DeclaringType?.FullName}::{callee.Name})。");
                                }
                            }

                            // newarr / 字段引用:类型面
                            var tr = ins.Operand as TypeReference;
                            if (tr != null)
                            {
                                foreach (var banned in ForbiddenInInput)
                                    if (tr.Name == banned)
                                        errs.Add($"[AC-10-02] {type.FullName}::{method.Name} " +
                                                 $"IL 命中被禁类型「{banned}」。");
                            }
                        }

                        // 局部 / 签名类型面
                        foreach (var v in body.Variables)
                        {
                            foreach (var banned in ForbiddenInInput)
                                if (v.VariableType.Name == banned)
                                    errs.Add($"[AC-10-02] {type.FullName}::{method.Name} " +
                                             $"局部变量类型为被禁的「{banned}」。");
                        }
                    }
                }
            }

            if (scannedMethods == 0)
                errs.Add("[AC-10-02] 扫描到的**方法数为 0** —— 拒以空集冒充绿(关了就永远绿)。");

            return errs;
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-10-03 —— 10 判定路径零浮点字面量
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// AC-10-03:从 <see cref="JudgeRoots"/> 各根出发取**真实 call 图闭包**,
        /// 扫闭包内每个方法体的浮点指令 + 局部/签名浮点类型。
        /// </summary>
        /// <param name="closureMembers">实际进入闭包的方法全名 —— 调用方可据它做**形状守卫**
        /// (核心方法若从闭包里消失,说明判定路径被改写,须重新人工确认)。</param>
        public static List<string> CheckJudgeIntegerIl(string dllPath, string[] roots,
                                                       out List<string> closureMembers)
        {
            var errs = new List<string>();
            closureMembers = new List<string>();

            if (!File.Exists(dllPath))
            {
                errs.Add($"[AC-10-03] 编译产物缺失「{dllPath}」—— 扫描面不存在,拒以空集冒充绿。");
                return errs;
            }
            if (roots == null || roots.Length == 0)
            {
                errs.Add("[AC-10-03] 扫描根为空 —— 拒以空集冒充绿。");
                return errs;
            }

            using (var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters
            {
                ReadingMode = ReadingMode.Deferred,
                InMemory = true,
            }))
            {
                var typesByName = new Dictionary<string, TypeDefinition>(StringComparer.Ordinal);
                foreach (var t in AssemblyGates.AllTypes(asm.MainModule))
                    if (t.Name != "<Module>" && !typesByName.ContainsKey(t.FullName))
                        typesByName[t.FullName] = t;

                var closure = new Dictionary<string, MethodDefinition>(StringComparer.Ordinal);
                var queue = new Queue<MethodDefinition>();

                foreach (var root in roots)
                {
                    int colon = root.IndexOf("::", StringComparison.Ordinal);
                    if (colon <= 0)
                    {
                        errs.Add($"[AC-10-03] 扫描根格式错:「{root}」(须为 TypeFullName::MethodName)");
                        continue;
                    }
                    string typeName = root.Substring(0, colon);
                    string methodName = root.Substring(colon + 2);

                    if (!typesByName.TryGetValue(typeName, out var rootType))
                    {
                        errs.Add($"[AC-10-03] 扫描根类型找不到:「{typeName}」" +
                                 "—— 拒以空集冒充绿(根改名须同步本表)。");
                        continue;
                    }

                    bool any = false;
                    foreach (var m in AllMethods(rootType))
                        if (m.Name == methodName)
                        {
                            any = true;
                            if (closure.TryAdd(m.FullName, m)) queue.Enqueue(m);
                        }
                    if (!any)
                        errs.Add($"[AC-10-03] 扫描根方法找不到:「{root}」—— 拒以空集冒充绿。");
                }

                while (queue.Count > 0)
                {
                    var cur = queue.Dequeue();
                    MethodBody body;
                    try { body = cur.Body; } catch { continue; }
                    if (body == null) continue;

                    foreach (var ins in body.Instructions)
                    {
                        var callee = ins.Operand as MethodReference;
                        if (callee == null) continue;
                        if (ins.OpCode != OpCodes.Call && ins.OpCode != OpCodes.Callvirt &&
                            ins.OpCode != OpCodes.Newobj) continue;

                        // ⚠️ **不调 `callee.Resolve()`** —— 它在跨程序集(BCL `netstandard` 等)
                        //    时抛 `AssemblyResolutionException`(实测:`ReadingMode.Deferred`
                        //    下解析器不加载 BCL)。与 `EcozoneIntegerGates` 同口径:
                        //    按 **TypeReference 的 FullName 名**在**本装配的类型表**里查 ——
                        //    跨装配的调用者天然查不到(名不在表内)⇒ 等价于「不追」,但零异常。
                        var declaring = callee.DeclaringType;
                        if (declaring == null) continue;
                        string declName = declaring.FullName;
                        // 泛型实例化(如 List`1<X>)⇒ 取泛型定义名
                        int tick = declName.IndexOf('<');
                        if (tick > 0) declName = declName.Substring(0, tick);

                        if (!typesByName.TryGetValue(declName, out var declType)) continue;  // 跨装配 ⇒ 不追

                        foreach (var cand in AllMethods(declType))
                        {
                            if (cand.Name != callee.Name) continue;
                            // 形参个数须一致(同名重载区分)
                            if (cand.Parameters.Count != callee.Parameters.Count) continue;
                            if (closure.TryAdd(cand.FullName, cand)) queue.Enqueue(cand);
                        }
                    }
                }

                foreach (var m in closure.Values)
                    closureMembers.Add(m.FullName);

                if (closure.Count == 0)
                {
                    errs.Add("[AC-10-03] 闭包为空 —— 拒以空集冒充绿(关了就永远绿)。");
                    return errs;
                }

                foreach (var m in closure.Values)
                {
                    MethodBody body;
                    try { body = m.Body; } catch { continue; }
                    if (body == null) continue;

                    foreach (var ins in body.Instructions)
                    {
                        if (ForbiddenFloatOpcodes.Contains(ins.OpCode))
                            errs.Add($"[AC-10-03] {m.FullName} 命中浮点指令「{ins.OpCode}」" +
                                     "(门 A / ADR-006:判定路径须全整数)。");

                        // 局部变量浮点
                        foreach (var v in body.Variables)
                            if (FloatTypeFullNames.Contains(v.VariableType.FullName))
                                errs.Add($"[AC-10-03] {m.FullName} 局部变量类型「{v.VariableType.FullName}」为浮点。");
                    }

                    // 签名面(参数 / 返回)
                    if (m.HasParameters)
                        foreach (var p in m.Parameters)
                            if (FloatTypeFullNames.Contains(p.ParameterType.FullName))
                                errs.Add($"[AC-10-03] {m.FullName} 参数「{p.Name}」类型为浮点。");
                    if (m.ReturnType != null && FloatTypeFullNames.Contains(m.ReturnType.FullName))
                        errs.Add($"[AC-10-03] {m.FullName} 返回类型为浮点。");
                }
            }

            return errs;
        }

        /// <summary>便捷重载:用默认 <see cref="JudgeRoots"/>。</summary>
        public static List<string> CheckJudgeIntegerIl(string dllPath)
            => CheckJudgeIntegerIl(dllPath, JudgeRoots, out _);

        /// <summary>枚举类型全部方法(含属性访问器 / 构造)。</summary>
        private static IEnumerable<MethodDefinition> AllMethods(TypeDefinition type)
        {
            foreach (var m in type.Methods) yield return m;
        }
    }
}
