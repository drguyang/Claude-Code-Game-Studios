// AC-7a-07 第二句 —— 「后台线程零 Unity API」的**编译期断言**。
//
// 权威来源:
//   design/gdd/persistence-service.md:507-514 —— AC-7a-07 原文 + 括注:
//     「**后台线程零 Unity API(2026-09-16 修订)**:运行时调用栈不可事后采集 ⇒ 改为
//      **编译期断言** —— `WritingAsync` 状态被调集合(**read only 数据的被写集合**)做 IL 扫描,
//      **零** `UnityEngine` 命名空间被调;`Application.persistentDataPath` 的读取
//      **主线程启动期缓存一次**(规则四),扫描证其不在后台被调集内。」
//   ADR-017 §二 —— 门 A(`noEngineReferences: true`)。
//   ADR-025 §① —— `Sim.Codec` 引用集恰 = {BCL, `Sim.Contracts`}。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 本门今日的实际判别力 —— **必须如实写清,否则就是借绿**
// ─────────────────────────────────────────────────────────────────────────────
// 被扫的两个类型(`SaveWriteQueue` / `SaveBufferPool`)都住在 `Sim.Codec`,而该装配的
// asmdef 已声明 `"noEngineReferences": true`,且 **b2 门**(`AssemblyGates.CheckGateA`)
// 已对 `Sim / Sim.Contracts / Sim.Codec` 三装配断言「引用集零引擎程序集」。
//
// ⇒ **在 b2 门已绿的前提下,本门不可能红** —— 一个编译不过 `UnityEngine` 的程序集里
//    不可能出现 `UnityEngine.*` 的调用指令。本门今日是 **b2 的推论**,不是独立判据。
//
// **那为什么还要它?** 三个具体理由,都不是「以防万一」这种空话:
//   ① **b2 的判据面是装配级引用表**(asmdef 的 `references` + 编译产物的 `AssemblyRef`),
//      本门的判据面是**方法级被调集合**。两者不同层 —— 若将来 `Sim.Codec` 因某个正当
//      理由引入一个**间接**依赖(例如某个 BCL 扩展包反过来引 `UnityEngine`),
//      b2 看 `AssemblyRef` 表会看到那个包名(不在黑名单内 ⇒ 绿),而本门会看到
//      **被调指令的声明类型**是 `UnityEngine.*`(红)。
//   ② GDD 的字面要求是「`WritingAsync` **状态被调集合**做 IL 扫描」—— 它点的是
//      **方法级**集合。用装配级门顶替方法级判据,是「判据被换成了更容易满足的那个」。
//   ③ 本门**点名了 `WritingAsync` 的落点**(`SaveWriteQueue` 的 `Drain` / `TryRunWithRetry`)。
//      若将来有人把写盘搬到别的方法、或新增一条旁路,只要它在这两个类型内就会被扫到;
//      而 b2 对「搬家」无感。
//
// ⚠️ **仍须登记的不满足**:GDD 说的是「`WritingAsync` 状态**被调集合**」,
//    本门扫的是「**worker 类型的方法体内**的调用指令」。两者的差别:
//    · 若 worker 调了一个**在别处定义**的方法,而那个方法内部用了 Unity API ——
//      本门只看**直接调用指令**,看不到第二层。
//    · 本门因此是**深度 1** 的近似。**已登记为已知盲区**(见 `KnownBlindSpots`)。
//    · 完整实现需要跨方法闭包分析(遍历被调方法的 body),本轮**未做** ——
//      在 b2 已绿的前提下它不会带来新的判别力,而实现成本与误报风险都显著。

using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>
    /// AC-7a-07:后台写盘 worker 的「零 Unity API」方法级 IL 断言。
    /// </summary>
    public static class SaveAsyncBoundaryGate
    {
        /// <summary>扫描面所在装配。</summary>
        public const string CodecAssemblyName = "Sim.Codec";

        /// <summary>
        /// 后台写盘路径的类型全名 —— 即 GDD 所说「`WritingAsync` 状态」的代码落点。
        /// <para>⚠️ 这份清单**须随写盘路径的实现同步**:若将来写盘搬到新类型,新类型不在此
        /// 清单内 ⇒ 本门对它零覆盖(而门仍然绿)。这正是「扫不到被测装配」形态,
        /// 故下方 `RequireScannerCoverage` 会断言清单里的每个类型**真的存在**。</para>
        /// </summary>
        public static readonly string[] WorkerTypeFullNames =
        {
            "DaYiJingCheng.Sim.Codec.SaveWriteQueue",
            "DaYiJingCheng.Sim.Codec.SaveBufferPool",
        };

        /// <summary>
        /// 引擎命名空间前缀 —— 命中任一即红。
        /// <para>`UnityEngine` / `UnityEditor` 是 GDD 点名的;`Unity.Collections` /
        /// `Unity.Burst` / `Unity.Jobs` / `Unity.Mathematics` 是 ADR-017 §二 点名的
        /// DOTS 侧程序集(同属引擎程序集,`Sim` 侧结构性排除)。</para>
        /// </summary>
        public static readonly string[] ForbiddenNamespacePrefixes =
        {
            "UnityEngine",
            "UnityEditor",
            "Unity.Collections",
            "Unity.Burst",
            "Unity.Jobs",
            "Unity.Mathematics",
        };

        /// <summary>已知盲区(如实登记,不借绿)。</summary>
        public static readonly string[] KnownBlindSpots =
        {
            "深度 1:只看 worker 类型方法体内的**直接**调用指令;" +
            "若被调方法在别处定义且其内部用 Unity API,本门看不到。",

            "反射调用(`typeof(X).GetMethod(...).Invoke`)不以 call 指令出现 ⇒ 扫不到。" +
            "本仓当前零此类形态;若引入须另裁。",

            "b2 门绿时本门不可能红 —— 它是 b2 的推论(见文件头 §判别力)。" +
            "本门的价值在**方法级**判据面与**搬家检测**,不在「拦住第一次违规」。",
        };

        /// <summary>
        /// 扫一个类型的全部方法体,返回命中清单。
        /// </summary>
        /// <param name="dllPath">`Sim.Codec.dll` 的路径。</param>
        /// <param name="hits">命中的调用点(诊断用)。</param>
        /// <returns>错误清单(空 = 通过)。</returns>
        public static List<string> CheckAsyncBoundaryIl(string dllPath, out List<string> hits)
        {
            var errs = new List<string>();
            hits = new List<string>();

            if (string.IsNullOrEmpty(dllPath) || !System.IO.File.Exists(dllPath))
            {
                errs.Add($"[AC-7a-07] 编译产物缺失「{dllPath}」—— 扫描面不存在,拒以空集冒充绿。");
                return errs;
            }

            using (var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters
            {
                // 与 SaveWritePathGate / EcozoneIntegerGates 同款:Deferred 才不触 Resolve。
                ReadingMode = ReadingMode.Deferred,
                InMemory = true,
            }))
            {
                var wanted = new HashSet<string>(WorkerTypeFullNames, StringComparer.Ordinal);
                var seen = new HashSet<string>(StringComparer.Ordinal);

                foreach (var type in AssemblyGates.AllTypes(asm.MainModule))
                {
                    if (type.Name == "<Module>") continue;
                    if (!wanted.Contains(type.FullName)) continue;

                    seen.Add(type.FullName);

                    foreach (var method in type.Methods)
                    {
                        if (!method.HasBody) continue;

                        foreach (var instr in method.Body.Instructions)
                        {
                            string forbidden = MatchForbiddenNamespace(instr);
                            if (forbidden == null) continue;

                            string site = $"{type.FullName}::{method.Name} → {forbidden}";
                            hits.Add(site);
                            errs.Add(
                                $"[AC-7a-07] 后台写盘路径出现引擎 API:{site}\n" +
                                "            GDD `:513-514` 逐字要求「`WritingAsync` 状态被调集合 IL 扫描,\n" +
                                "            **零** `UnityEngine` 命名空间被调」—— 后台线程禁调 Unity API\n" +
                                "            (Unity 对象只能在主线程访问;IL2CPP 下越线是未定义行为)。");
                        }
                    }
                }

                // ── 覆盖守卫 ⓪:清单本身为空 ⇒ 守卫零断言、门恒绿 ──
                // ⚠️ 2026-10-25 证伪轮 MA20 抓出的真缺口:下方覆盖守卫的判据是
                //    「清单**每一项**都找得到」—— 清单被清空时 foreach 零项 ⇒ 零断言。
                //    (类型改名 / 搬走会红,但「整表清空」逃逸。)
                if (WorkerTypeFullNames.Length == 0)
                {
                    errs.Add(
                        "[AC-7a-07] WorkerTypeFullNames 为空 —— 扫描清单被清空 ⇒ 本门对写盘路径\n" +
                        "            零覆盖,拒以空清单冒充绿。写盘路径落点见 GDD `:509`(WritingAsync),\n" +
                        "            清单须与实现同步维护。");
                }

                // ── 覆盖守卫:清单里的类型必须真的存在,否则门恒绿 ──
                // ⚠️ 这是本项目抓过五次的「扫不到被测装配」形态:类型改名 / 搬走之后,
                //    扫描面为空集,门照样绿,而写盘路径**零覆盖**。
                foreach (string w in WorkerTypeFullNames)
                {
                    if (!seen.Contains(w))
                    {
                        errs.Add(
                            $"[AC-7a-07] 扫描面丢失:清单点名的类型「{w}」在 {CodecAssemblyName} 里不存在。\n" +
                            "            写盘路径可能已改名 / 搬家 —— 本门对它零覆盖。\n" +
                            "            处置:同步更新 SaveAsyncBoundaryGate.WorkerTypeFullNames," +
                            "或确认该类型被删除并同步删清单项。");
                    }
                }

                // ── 反向守卫:类型存在但一个方法体都没扫到 ⇒ 同样是空集冒充绿 ──
                if (seen.Count > 0)
                {
                    int scannedBodies = 0;
                    foreach (var type in AssemblyGates.AllTypes(asm.MainModule))
                    {
                        if (!seen.Contains(type.FullName)) continue;
                        foreach (var m in type.Methods) if (m.HasBody) scannedBodies++;
                    }
                    if (scannedBodies == 0)
                    {
                        errs.Add(
                            "[AC-7a-07] 扫描面为空:清单点名的类型存在但**零个方法体**被扫到 —— " +
                            "拒以空集冒充绿。");
                    }
                }
            }

            return errs;
        }

        /// <summary>便捷重载(丢弃命中清单)。</summary>
        public static List<string> CheckAsyncBoundaryIl(string dllPath)
            => CheckAsyncBoundaryIl(dllPath, out _);

        /// <summary>
        /// 一条指令是否调用/构造了引擎命名空间下的成员。命中返回全名,否则 null。
        /// <para>判据形态与 <see cref="SaveWritePathGate"/> 一致:`call` / `callvirt` /
        /// `newobj` 的操作数是 <see cref="MethodReference"/>,取其
        /// <c>DeclaringType.FullName</c> 查前缀。</para>
        /// <para>⚠️ 另查 <c>ldtoken</c> / <c>ldstr</c> 之外的**类型引用**:`newarr` /
        /// `castclass` / `isinst` 的操作数也可能是 <see cref="TypeReference"/> ——
        /// 一并收,因为「把 Unity 类型当参数传」同样是后台线程碰 Unity。</para>
        /// </summary>
        private static string MatchForbiddenNamespace(Instruction instr)
        {
            string fullName = null;

            if (instr.OpCode == OpCodes.Call || instr.OpCode == OpCodes.Callvirt
                || instr.OpCode == OpCodes.Newobj || instr.OpCode == OpCodes.Ldftn
                || instr.OpCode == OpCodes.Ldvirtftn)
            {
                if (instr.Operand is MethodReference mr) fullName = mr.DeclaringType?.FullName;
            }
            else if (instr.OpCode == OpCodes.Newarr || instr.OpCode == OpCodes.Castclass
                     || instr.OpCode == OpCodes.Isinst || instr.OpCode == OpCodes.Box
                     || instr.OpCode == OpCodes.Unbox_Any)
            {
                if (instr.Operand is TypeReference tr) fullName = tr.FullName;
            }

            if (fullName == null) return null;

            foreach (string prefix in ForbiddenNamespacePrefixes)
            {
                if (fullName == prefix
                    || fullName.StartsWith(prefix + ".", StringComparison.Ordinal))
                    return fullName;
            }
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
                errs.Add("[AC-7a-07] EditorUtility.scriptCompilationFailed = true ⇒ 拒扫陈旧产物" +
                         "(假绿防护:读上一版 DLL = 假绿;红在编译器,本门只声明不可判)。");
                return errs;
            }
#endif

            string dll = AssemblyGates.ScriptAssemblyPath(CodecAssemblyName);
            errs.AddRange(CheckAsyncBoundaryIl(dll));
            return errs;
        }
    }
}
