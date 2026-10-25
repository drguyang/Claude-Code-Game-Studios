// AC-7a-08 ③ —— 存档写盘的 IL 正面断言(仅 `SaveService` 可达写盘 API)。
//
// 权威来源:
//   design/gdd/persistence-service.md:517-522 —— AC-7a-08(BLOCKING)三方守之第三:
//     「**IL 正面断言(仅 `SaveService` 类可达 `FileStream.Write` / `File.Move`)**」。
//     原文判据三方 = ① 接口签名反射断言 ② 接口级正面枚举 ③ **本门**。
//   ADR-010 §四 —— 原子写 = tmp + flush + rename;§六 Checkpoint 唯一写入口。
//
// 为什么这一门必须存在(而不是靠「已存在两家写盘方」的既有事实):
//   AC-7a-08 的语义是「**不存在第四入口**」。前两方守(接口反射 + 正面枚举)只约束
//   `ISaveService` **自身**的公开面 —— 它们**看不见**旁路:
//   ① 别处 `new` 一个 `SaveService` 再直调 `SaveBytes`;
//   ② 别处直接 `File.WriteAllBytes("save_1.bin", …)` 绕开整个服务;
//   ③ `SaveService` 自己长出新写盘点(双入口漂移)。
//   ③ 恰是本仓已抓过的形态:`WriteExitSave` 首版就是逐行复制的第二份原子写实现
//   (`SaveService.cs:131-141` 的 W4 补正注)。
//
// ⚠️ **扫描面 = IL,不是源文本**。承 `EcozoneIntegerGates` 的先例与 FsWriteBoundaryGate
//   自己的「盲区 ③」自陈(`Path.Combine("..","..","docs")` 这类无锚点绕行 grep 看不见)。
//
// ⚠️ **两条判据(缺任一条,门就退化成恒绿)**:
//   A. **写盘点形状**:`SaveService` 内出现写盘 API 调用的方法集合,须恰等于白名单
//      (`WriteAtomic`),且 `WriteAtomic` 须真的含写盘调用(防空集冒充绿)。
//   B. **全程序集无旁路**:`Sim.Codec` 程序集内**除 `SaveService` 外**零写盘调用。
//
// ⚠️ **写盘 API 名单是单一真源**(复用 `FsWriteBoundaryGate.WriteApiNames`)——
//   两份字面量会漂移,而漂移是静默的(新 API 加进一处、另一处看不见)。
//
// ⚠️ **本门住在 Editor.Tools.Gates(不进构建)** —— 与 ADR-022 Tooling 层同构。
//
// ⚠️ **已知盲区(登记,不借绿)**:
//   ① 反射调用 `typeof(File).GetMethod("WriteAllBytes").Invoke(...)` 不会以 `call` 指令
//      出现 → 扫不到。本仓当前零此类形态;若引入须另裁(同 FsWriteBoundaryGate 盲区 ③ 口径)。
//   ② 只扫 `Sim.Codec` 程序集 —— 别家程序集(如 `Gameplay.Boot`)写自己的 sidecar 不属本门
//      判面(那是 3 输入 / 44 音频的**另一条**白名单,各有其门)。
//   ③ **`FileStream` 的实例方法写入**(`fs.Write(...)`)按**类型**判,不按方法名 —— 见 §实现注。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>
    /// AC-7a-08 ③:存档写盘可达面的 IL 门 —— 仅 <c>SaveService</c> 可达写盘 API。
    /// </summary>
    public static class SaveWritePathGate
    {
        /// <summary>扫描面所在装配(ADR-025 §① 登记的边界程序集)。</summary>
        public const string CodecAssemblyName = "Sim.Codec";

        /// <summary>唯一允许写盘的类(全名)。</summary>
        public const string AllowedWriteTypeFullName = "DaYiJingCheng.Sim.Codec.SaveService";

        /// <summary>
        /// 唯一允许写盘的方法(AC-7a-08 的白名单形状 = **一个**原子写原语;
        /// W4 补正的提取结果,两处入口各自只负责路径)。
        /// </summary>
        public const string AllowedWriteMethodName = "WriteAtomic";

        /// <summary>
        /// 写盘 API 名单 —— 从 <see cref="FsWriteBoundaryGate.WriteApiNames"/> 派生,
        /// **不重抄字面量**(两份会漂移,且漂移静默)。
        /// <para>⚠️ 名单是**源文本简名**(<c>File.WriteAllBytes</c>);IL 侧的操作数是
        /// <c>System.IO.File</c>。匹配见 <see cref="MatchWriteApi"/> 的剥前缀规则。</para>
        /// </summary>
        public static IReadOnlyList<string> WriteApiNames => FsWriteBoundaryGate.WriteApiNames;

        /// <summary>
        /// **类级允许但方法级不约束**的 API:目录准备。
        /// <para>AC-7a-08 原文点名的写盘面是 <c>FileStream.Write</c> / <c>File.Move</c>
        /// —— 「写存档」。<c>Directory.CreateDirectory</c> 是**目录存在性准备**
        /// (构造期一次,<c>SaveService.cs:113</c>),它不写存档内容、不构成第二入口。
        /// ⇒ 它受**类级**判据约束(除 <c>SaveService</c> 外零命中),但**不受**方法级白名单约束。</para>
        /// <para>⚠️ 这条区分是**显式登记**的,不是漏判后补的豁免 —— 判据:
        /// 「删掉它,存档还能写吗?」不能(目录不在则写失败)⇒ 它是前置条件,不是写路径。</para>
        /// </summary>
        public static readonly IReadOnlyList<string> ClassLevelOnlyApiNames = new[]
        {
            "Directory.CreateDirectory",
        };

        /// <summary>
        /// IL 层判据本体(纯函数:<paramref name="dllPath"/> ⇒ 错误列表)。
        /// <para>返回空 = 通过。产物缺失 / 白名单方法找不到 / 白名单方法零写盘调用
        /// ⇒ **记红**(拒以空集冒充绿,与 <c>EcozoneIntegerGates</c> 同纪律)。</para>
        /// </summary>
        /// <param name="dllPath">`Sim.Codec.dll` 的全路径。</param>
        /// <param name="hits">实际命中的 (类型::方法 → 写盘 API) 清单,供形状守卫与证伪测试用。</param>
        public static List<string> CheckSaveWritePathIl(string dllPath, out List<string> hits)
        {
            var errs = new List<string>();
            hits = new List<string>();

            if (!File.Exists(dllPath))
            {
                errs.Add($"[AC-7a-08③] 编译产物缺失「{dllPath}」—— 扫描面不存在,拒以空集冒充绿。");
                return errs;
            }

            var writeApis = new HashSet<string>(WriteApiNames, StringComparer.Ordinal);

            using (var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters
            {
                // 与 EcozoneIntegerGates.CheckEcozoneIntegerIl 同款:Deferred 才不触 Resolve。
                ReadingMode = ReadingMode.Deferred,
                InMemory = true,
            }))
            {
                bool sawAllowedMethod = false;
                int allowedMethodWriteCalls = 0;

                var classLevelOnly = new HashSet<string>(ClassLevelOnlyApiNames, StringComparer.Ordinal);

                foreach (var type in AssemblyGates.AllTypes(asm.MainModule))
                {
                    if (type.Name == "<Module>") continue;
                    bool isAllowedType = string.Equals(
                        type.FullName, AllowedWriteTypeFullName, StringComparison.Ordinal);

                    foreach (var method in type.Methods)
                    {
                        if (!method.HasBody) continue;

                        foreach (var instr in method.Body.Instructions)
                        {
                            string api = MatchWriteApi(instr, writeApis);
                            if (api == null) continue;

                            string site = $"{type.FullName}::{method.Name} → {api}";
                            hits.Add(site);

                            bool inAllowedMethod = isAllowedType &&
                                string.Equals(method.Name, AllowedWriteMethodName, StringComparison.Ordinal);

                            if (inAllowedMethod)
                            {
                                sawAllowedMethod = true;
                                allowedMethodWriteCalls++;
                                continue;
                            }

                            // 类级允许(method 级不约束):目录准备。见 ClassLevelOnlyApiNames 的判据。
                            if (isAllowedType && classLevelOnly.Contains(api))
                                continue;

                            errs.Add(
                                $"[AC-7a-08③] 写盘调用越出白名单:{site}\n" +
                                $"            AC-7a-08 要求「仅 {AllowedWriteTypeFullName} 类可达写盘 API」,\n" +
                                $"            且该类内唯一写盘方法 = {AllowedWriteMethodName}(原子写原语)。\n" +
                                "            「不存在第四入口」若靠接口反射守,旁路(别处直调 / 直接 File.*)看不见。");
                        }
                    }
                }

                // ── 形状守卫:白名单那一处必须**真的存在且有写盘**,否则门恒绿 ──
                if (!sawAllowedMethod)
                {
                    errs.Add(
                        $"[AC-7a-08③] 白名单方法 {AllowedWriteTypeFullName}::{AllowedWriteMethodName} " +
                        "不存在或零写盘调用 —— 扫描面丢失 / 写盘被搬到别处,拒以空集冒充绿。");
                }
                else if (allowedMethodWriteCalls == 0)
                {
                    errs.Add(
                        $"[AC-7a-08③] {AllowedWriteTypeFullName}::{AllowedWriteMethodName} 内零写盘调用 " +
                        "—— 原子写原语成了空壳(守卫逻辑仍在,真写盘已搬走)。");
                }
            }

            return errs;
        }

        /// <summary>便捷重载(丢弃命中清单)。</summary>
        public static List<string> CheckSaveWritePathIl(string dllPath)
            => CheckSaveWritePathIl(dllPath, out _);

        /// <summary>
        /// 一条指令是否命中写盘 API。命中则返回 API 名(<c>File.Move</c> 形态),否则 null。
        /// <para>判据形态 = <c>call</c> / <c>callvirt</c> / <c>newobj</c> 的操作数
        /// **声明类型全名 + 方法名** 拼成 <c>Type.Method</c> 后查名单。</para>
        /// <para>另接:任何对 <c>System.IO.FileStream</c> / <c>System.IO.StreamWriter</c> 的实例写
        /// (<c>Write</c> / <c>WriteAsync</c> / <c>Flush</c> 带走盘语义)也计入 —— 见实现注。</para>
        /// </summary>
        private static string MatchWriteApi(Instruction instr, HashSet<string> writeApis)
        {
            if (instr.OpCode != OpCodes.Call && instr.OpCode != OpCodes.Callvirt
                && instr.OpCode != OpCodes.Newobj)
                return null;

            if (!(instr.Operand is MethodReference mr)) return null;

            string declaring = mr.DeclaringType?.FullName;
            if (declaring == null) return null;

            string key = declaring + "." + mr.Name;
            if (writeApis.Contains(key)) return key;

            // ⚠️ **名单是源文本简名**(`File.WriteAllBytes`),IL 的操作数是**全名**
            //    (`System.IO.File.WriteAllBytes`)—— 首版按全名查,实测**全部漏判**
            //    (只命中 FileStream 分支)。剥掉 `System.IO.` 前缀再查一次。
            //    刻意**只剥这一个前缀**而不是「取最后两段」:后者会把任何程序集的
            //    `X.Y.File.WriteAllBytes` 也算命中,判据过宽 = 假红。
            const string ioPrefix = "System.IO.";
            if (declaring.StartsWith(ioPrefix, StringComparison.Ordinal))
            {
                string simplified = declaring.Substring(ioPrefix.Length) + "." + mr.Name;
                if (writeApis.Contains(simplified)) return simplified;
            }

            // `System.IO.File` 的简写形态(`File.WriteAllBytes` 名单即此形)已由上面覆盖;
            // 下面补 **FileStream 实例写**:当前 `WriteAtomic` 用 `new FileStream(...)` 只为
            // `Flush(true)`(把 OS 缓冲刷到盘),那是耐久性语义 —— 别处长出同样调用同样算写盘面。
            if ((declaring == "System.IO.FileStream" || declaring == "System.IO.StreamWriter")
                && (mr.Name == "Write" || mr.Name == "WriteAsync"
                    || mr.Name == "Flush" || mr.Name == "WriteByte"))
                return key;

            return null;
        }

        /// <summary>
        /// 门链入口(<c>AssemblyGates.RunAll</c> 调用)。
        /// </summary>
        public static List<string> RunAll()
        {
            var errs = new List<string>();

#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.scriptCompilationFailed)
            {
                errs.Add("[AC-7a-08③] EditorUtility.scriptCompilationFailed = true ⇒ 拒扫陈旧产物" +
                         "(假绿防护:读上一版 DLL = 假绿;红在编译器,本门只声明不可判)。");
                return errs;
            }
#endif

            string dll = AssemblyGates.ScriptAssemblyPath(CodecAssemblyName);
            errs.AddRange(CheckSaveWritePathIl(dll));
            return errs;
        }
    }
}
