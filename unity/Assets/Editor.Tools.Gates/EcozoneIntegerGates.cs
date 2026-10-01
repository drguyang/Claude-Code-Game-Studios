// 权威来源:GDD world-and-ecozones.md AC-6-22(BLOCKING)—— `EcozoneOf` 全程整数
//   · 判据(原文):`EcozoneOf` 及其**传递闭包**(其调用的全部同级方法)的 IL
//     **不得出现** `conv.r4` / `conv.r8` / `ldc.r4` / `ldc.r8` / 任何 `float` / `double` 局部
//     (与门 B 同法,Mono.Cecil);
//   · **附带声明**(:1367):该函数**不含 y 读取**(F-6-3 只用 x / z)。
//   EC-6-9(射线穿顶点半开区间)· F-6-3 §四(横轴 / int64 溢出)。
//
// 落点:Editor.Tools.Gates(EditMode 门,不进构建;照 AssemblyGates.cs / InputBoundaryGates.cs
//   先例 = `public static class` + `IReadOnlyList<string>` 错误列表形态,由调用方决定 throw /
//   聚合 —— 本类自身不引 NUnit)。
//
// 为什么单独成条(GDD :1290 的理由):射线法**极易**在某次重构中被写成浮点(除以斜率),
// 而它一旦变浮点即破坏 ADR-006 边界 —— 该风险值得一条**专属**断言。
//
// ⚠️ 2026-10-01 落地时如实登记的三条判据边界:
//   ① **「传递闭包」= 从根方法出发的真实 call 图**,不是「命名空间下全部方法」。
//      第一版扫整个 `DaYiJingCheng.Sim.World`,结果把 **Story 004 的 ChunkTopology /
//      ChunkActivator**(它们合法读取 Y)判成红 —— 那是**判据过宽**制造的假红,
//      与「假绿」同属静默失败。改为 call 图闭包后,扫描面收敂到 AC 原文的形状。
//   ② **`ldc.r4` / `ldc.r8` 与 `conv.r4` / `conv.r8` 是不充分判据**:常量浮点折叠后
//      可能以 `ldc.i4` + `conv.r4` 或元数据 token 出现;故另加「`float` / `double`
//      局部 / 参数 / 返回类型」的类型面判据(Cecil `MethodDefinition.Body.Variables` 等)。
//   ③ **y 读取判据 = 逐指令判 `ldfld` / `ldsfld` / `call` 的操作数名**是否落在
//      {`Y`, `get_Y`}(Deferred 下取自 FieldReference / MethodReference.Name,无需 Resolve)——
//      比「载荷形状判」更强,能抓住「顺手读了 y 又没用」的重构。
//      生产侧零命中的前提是 **Register 的「重合相邻顶点」判据只用 x/z 比较**
//      (y 不参与 ⇒ 同 (x,z) 不同 y 的两个顶点对本多边形就是同一个顶点)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>
    /// AC-6-22 的 IL 执行点:<c>EcozoneOf</c>(及装载路径)的传递闭包零浮点 + 零 y 读取。
    /// </summary>
    public static class EcozoneIntegerGates
    {
        /// <summary>扫描面所在装配(Story 002 的实现落点;ADR-025 §① 登记的 Sim)。</summary>
        public const string SimAssemblyName = "Sim";

        /// <summary>AC-6-22 的扫描根(生产侧)──────
        /// <c>EcozoneRegistry::EcozoneOf</c> = AC 原文点名的查询入口;
        /// <c>EcozoneRegistry::Register</c> = 同模块的装载入口(它调 ValidateNoSelfIntersection
        /// ⇒ SegmentsProperlyCross ⇒ Cross,即 int64 叉积纪律所在的调用链)。
        /// 两条根的闭包并集覆盖本模块全部几何判定路径。</summary>
        public static readonly string[] EcozoneRoots =
        {
            "DaYiJingCheng.Sim.World.EcozoneRegistry::EcozoneOf",
            "DaYiJingCheng.Sim.World.EcozoneRegistry::Register",
        };

        /// <summary>AC-6-22 点名的浮点 IL 指令(原文四个)。</summary>
        private static readonly OpCode[] ForbiddenFloatOpcodes =
        {
            OpCodes.Conv_R4, OpCodes.Conv_R8,
            OpCodes.Ldc_R4, OpCodes.Ldc_R8,
        };

        /// <summary>y 读取判据:`WorldPos.Y` 在 IL 上的两种出现形态(字段名 / 属性 getter 名)。</summary>
        private static readonly string[] YReadTokens = { "Y", "get_Y" };

        /// <summary>浮点类型全名(含别名;与 InputBoundaryGates.ReadingFloatFullNames 同口径)。</summary>
        private static readonly string[] FloatTypeFullNames = { "System.Single", "System.Double" };

        /// <summary>
        /// AC-6-22 的 IL 扫描:从 <paramref name="roots"/> 各根方法出发,取**真实 call 图闭包**,
        /// 扫描闭包内每个方法体的指令面 + 局部 / 签名类型面。
        /// <para>返回错误列表(空 = 通过)。产物缺失 / 根方法找不到 / 闭包为空 ⇒ **记红**
        /// (拒以空集冒充绿,与 CheckAudioClockTokens 同纪律)。</para>
        /// <para><paramref name="closureMembers"/> = 实际进入闭包的方法全名清单(调用方可据
        /// 它做形状守卫:核心方法若从闭包里消失,说明查询路径被改写,须重新人工确认)。</para>
        /// </summary>
        public static List<string> CheckEcozoneIntegerIl(string dllPath, string[] roots,
                                                         out List<string> closureMembers)
        {
            var errs = new List<string>();
            closureMembers = new List<string>();
            if (!File.Exists(dllPath))
            {
                errs.Add($"[AC-6-22] 编译产物缺失「{dllPath}」—— 扫描面不存在,拒以空集冒充绿。");
                return errs;
            }
            if (roots == null || roots.Length == 0)
            {
                errs.Add("[AC-6-22] 扫描根为空 —— 拒以空集冒充绿(关了就永远绿)。");
                return errs;
            }

            using (var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters
            {
                // 与 AssemblyGates.CheckAudioAssemblyIl / CheckAudioClockTokens 同款:
                // Deferred 才不触 Resolve(Immediate 会 AssemblyResolutionException)。
                ReadingMode = ReadingMode.Deferred,
                InMemory = true,
            }))
            {
                var typesByName = new Dictionary<string, TypeDefinition>(StringComparer.Ordinal);
                foreach (var t in AssemblyGates.AllTypes(asm.MainModule))
                    if (t.Name != "<Module>" && !typesByName.ContainsKey(t.FullName))
                        typesByName[t.FullName] = t;

                var closure = new Dictionary<string, MethodDefinition>(StringComparer.Ordinal);

                foreach (var root in roots)
                {
                    int colon = root.IndexOf("::", StringComparison.Ordinal);
                    if (colon <= 0)
                    {
                        errs.Add($"[AC-6-22] 扫描根格式错:「{root}」(须为 TypeFullName::MethodName)");
                        continue;
                    }
                    string typeName = root.Substring(0, colon);
                    string methodName = root.Substring(colon + 2);

                    if (!typesByName.TryGetValue(typeName, out var rootType))
                    {
                        errs.Add($"[AC-6-22] 扫描根类型「{typeName}」在产品「{dllPath}」内不存在 —— "
                                 + "扫描面丢失(改名 / 搬命名空间即扫不到),拒以空集冒充绿。");
                        continue;
                    }
                    var rootMethod = FindMethod(rootType, methodName);
                    if (rootMethod == null)
                    {
                        errs.Add($"[AC-6-22] 扫描根方法「{methodName}」在「{typeName}」内不存在 —— "
                                 + "扫描面丢失,拒以空集冒充绿。");
                        continue;
                    }

                    WalkClosure(rootMethod, typesByName, closure, errs);
                }

                foreach (var kv in closure)
                {
                    closureMembers.Add(kv.Key);
                    errs.AddRange(CheckMethod(kv.Value, asm.Name.Name));
                }

                if (closure.Count == 0)
                    errs.Add($"[AC-6-22] call 图闭包为空(根方法一个都没进闭包)—— "
                             + "扫描面丢失,拒以空集冒充绿。");
            }
            return errs;
        }

        /// <summary>生产侧便捷重载:扫 <see cref="EcozoneRoots"/>。</summary>
        public static List<string> CheckEcozoneIntegerIl(string dllPath)
            => CheckEcozoneIntegerIl(dllPath, EcozoneRoots, out _);

        /// <summary>
        /// call 图闭包:BFS 沿 call / callvirt / newobj 的方法引用走,**只收本装配内**的方法
        /// (外部 BCL / 引擎方法不进闭包 —— 它们的 IL 不是我们的代码;若闭包里调用了外部浮点
        /// 实现,该 call 指令所在方法体自身必含把整数转成浮点的 `conv.r4`,由指令面接住)。
        /// </summary>
        private static void WalkClosure(MethodDefinition root,
                                        Dictionary<string, TypeDefinition> typesByName,
                                        Dictionary<string, MethodDefinition> closure,
                                        List<string> errs)
        {
            var queue = new Queue<MethodDefinition>();
            queue.Enqueue(root);
            closure[FullName(root)] = root;

            while (queue.Count > 0)
            {
                var m = queue.Dequeue();
                if (!m.HasBody) continue;

                foreach (var instr in m.Body.Instructions)
                {
                    MethodReference mr = null;
                    if (instr.OpCode == OpCodes.Call || instr.OpCode == OpCodes.Callvirt
                        || instr.OpCode == OpCodes.Newobj || instr.OpCode == OpCodes.Jmp)
                        mr = instr.Operand as MethodReference;
                    if (mr == null) continue;

                    string declaring = mr.DeclaringType?.FullName;
                    if (declaring == null || !typesByName.TryGetValue(declaring, out var declType))
                        continue;   // 外部方法:不进闭包(见上面说明)

                    var target = FindMethod(declType, mr.Name, mr.Parameters.Count);
                    if (target == null)
                    {
                        errs.Add($"[AC-6-22] 闭包解析失败:{FullName(m)} 调用了 "
                                 + $"{declaring}::{mr.Name}({mr.Parameters.Count} 参),但本装配内无同名同参方法 —— "
                                 + "call 图不完整 ⇒ 闭包判定不可信,记红而非静默跳过。");
                        continue;
                    }

                    string key = FullName(target);
                    if (closure.TryAdd(key, target))
                        queue.Enqueue(target);
                }
            }
        }

        /// <summary>按名字(+ 可选参数数)找方法;取第一个具方法体的。</summary>
        private static MethodDefinition FindMethod(TypeDefinition type, string name, int paramCount = -1)
        {
            foreach (var m in type.Methods)
            {
                if (m.Name != name) continue;
                if (paramCount >= 0 && m.Parameters.Count != paramCount) continue;
                if (!m.HasBody && paramCount < 0) continue;
                return m;
            }
            return null;
        }

        private static string FullName(MethodDefinition m)
            => $"{m.DeclaringType.FullName}::{m.Name}({m.Parameters.Count})";

        /// <summary>单方法的 IL 判定(指令面 + 局部 / 签名类型面)。</summary>
        private static List<string> CheckMethod(MethodDefinition m, string assemblyName)
        {
            var errs = new List<string>();
            string where = $"{assemblyName}::{FullName(m)}";

            foreach (var p in m.Parameters)
            {
                if (IsFloatType(p.ParameterType))
                    errs.Add($"[AC-6-22] {where} 参数 {p.Name} 类型为浮点「{p.ParameterType.Name}」"
                             + "—— 射线法须全整数(F-6-3 §四;一「除以斜率」即破 ADR-006 边界)。");
            }
            if (IsFloatType(m.ReturnType))
                errs.Add($"[AC-6-22] {where} 返回类型为浮点「{m.ReturnType.Name}」—— 同上。");

            if (m.Body.HasVariables)
            {
                foreach (var v in m.Body.Variables)
                {
                    if (IsFloatType(v.VariableType))
                        errs.Add($"[AC-6-22] {where} 含浮点局部「{v.VariableType.Name}」"
                                 + "(AC-6-22 原文:任何 float / double 局部)—— 射线法须全整数。");
                }
            }

            foreach (var instr in m.Body.Instructions)
            {
                // ① 浮点 opcode 面(原文四个)。
                foreach (var op in ForbiddenFloatOpcodes)
                {
                    if (instr.OpCode.Code == op.Code)
                    {
                        errs.Add($"[AC-6-22] {where} 含浮点指令 {instr.OpCode.Name}"
                                 + " —— AC-6-22 原文点名禁 conv.r4 / conv.r8 / ldc.r4 / ldc.r8。");
                        break;
                    }
                }

                // ② y 读取面(附带声明,GDD :1367)。
                if (instr.OpCode == OpCodes.Ldfld || instr.OpCode == OpCodes.Ldsfld)
                {
                    var fr = instr.Operand as FieldReference;
                    if (fr != null && IsYToken(fr.Name))
                        errs.Add($"[AC-6-22] {where} 读取字段 {fr.Name} —— F-6-3 只用 x/z,"
                                 + "该函数不得含 y 读取(GDD :1367 附带声明)。");
                }
                else if (instr.OpCode == OpCodes.Call || instr.OpCode == OpCodes.Callvirt)
                {
                    var mr = instr.Operand as MethodReference;
                    if (mr != null && IsYToken(mr.Name))
                        errs.Add($"[AC-6-22] {where} 调用 {mr.Name} —— F-6-3 只用 x/z,"
                                 + "该函数不得含 y 读取(GDD :1367 附带声明)。");
                }
            }
            return errs;
        }

        private static bool IsYToken(string name) => YReadTokens.Contains(name);

        private static bool IsFloatType(TypeReference t)
            => t != null && FloatTypeFullNames.Contains(t.FullName ?? "");
    }
}
