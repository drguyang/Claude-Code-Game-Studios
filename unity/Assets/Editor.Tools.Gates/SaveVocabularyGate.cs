// AC-7a-09 第二半 —— 忘词令(规则七判据层):q 系列符号零命中。
//
// 权威来源:
//   design/gdd/persistence-service.md:527-532 —— AC-7a-09 原文:
//     「GIVEN 全仓符号表,WHEN 判据端扫描 `quicksave` / `quickload` / `save-now-slot`
//      类标识符,THEN 零命中(扫描集与假阳性白名单见 AC-7a-04)」
//   design/gdd/persistence-service.md:142-150 —— 规则七双守:
//     代码层(接口签名反射,已有夹具 folded_predicate_test:655)= 半守;
//     判据层(忘词令符号扫描)= 本门。
//   OQ-7a-2(✅ 2026-09-16 已裁):载体原裁 ADR-012 CI 门。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 载体前提变化(2026-10-25 用户裁定提前落本地门,原文不删)
// ─────────────────────────────────────────────────────────────────────────────
// 原 blocked 理由:「单独落扫描器 = 没有东西会跑它 ⇒ 记为有载体即借绿」(blocked-by
// TD-ci-01)。该前提**已变**:`AssemblyGates.RunAll` 自 2026-10-25 起承载
// `SaveWritePathGate` / `SaveAsyncBoundaryGate` 等持久化门族,且有**三处强制执行点**
// (菜单 / reload / 构建前门)⇒ 本门挂 RunAll 即有东西跑它。与 ADR-012 CI 门**不互斥**
// —— 将来 CI 矩阵(TD-ci-01)落地时再挂一层同款判据。
//
// ⚠️ 扫描形态 —— **Cecil 符号名扫描,不是源码文本 grep**:
// AC-7a-04 原文「注释 / 字符串字面量假阳性由扫描工具置于白名单,不误伤」。
// Cecil 只见**标识符**(类型 / 方法 / 字段 / 属性 / 事件名),注释与字符串天然不可见
// ⇒ 白名单结构性为零,不需要白名单表。GDD `:144`「断言**符号表**不含」的字面即此。
//
// ⚠️ 本门类名刻意**不含任何触发词** —— 门自身住在 Editor.Tools.Gates(不在扫描集),
// 但源码文本级工具(如未来 grep-IL 腿)会看到本文件;类名避开触发词使两类工具互不误伤。

using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>
    /// AC-7a-09:忘词令 —— 运行期装配符号表中 q 系列标识符零命中。
    /// </summary>
    public static class SaveVocabularyGate
    {
        /// <summary>扫描集 = 运行期装配(b3 Manifest 的非 Editor / 非 Tests 成员)。</summary>
        public static readonly string[] RuntimeAssemblies =
        {
            "Sim", "Sim.Contracts", "Sim.Codec",
            "Gameplay.Boot", "Gameplay.Presentation", "Gameplay.UI", "Gameplay.Input",
            "Telemetry",
        };

        /// <summary>
        /// 忘词令触发词(小写比对)。`save-now-slot` 在合法 C# 标识符中只可能是
        /// `SaveNowSlot` 形态 ⇒ 以 `savenow` 收录;连写 `quicksave` / `quickload` 为 GDD 点名。
        /// </summary>
        public static readonly string[] ForbiddenTerms =
        {
            "quicksave",
            "quickload",
            "savenow",
        };

        /// <summary>
        /// 纯函数层:对一个已加载装配跑忘词令。
        /// <para>拆出来供夹具**内存注入**违规符号(Cecil 程序化构造)——
        /// 否则「门真的会红」只能靠改仓库源码证明,夹具做不出负例。</para>
        /// </summary>
        /// <param name="asm">被扫装配。</param>
        /// <param name="scannedTypes">实际扫过的类型数(守卫:零 = 空集冒充绿)。</param>
        /// <param name="assemblyLabel">错误消息里显示的装配名。</param>
        public static List<string> CheckSymbolsIn(AssemblyDefinition asm,
                                                  out int scannedTypes,
                                                  string assemblyLabel = null)
        {
            scannedTypes = 0;
            var errs = new List<string>();
            string label = assemblyLabel ?? asm.MainModule.Name ?? asm.FullName;

            if (ForbiddenTerms.Length == 0)
            {
                errs.Add("[AC-7a-09] ForbiddenTerms 为空 —— 忘词令词表被清空 ⇒ 本门零判别,拒以空词表冒充绿。");
            }

            foreach (var type in AssemblyGates.AllTypes(asm.MainModule))
            {
                if (type.Name == "<Module>") continue;
                scannedTypes++;

                CheckOne(errs, label, $"type {type.FullName}", type.FullName);

                foreach (var m in type.Methods) CheckOne(errs, label, $"method {type.FullName}.{m.Name}", m.Name);
                foreach (var f in type.Fields) CheckOne(errs, label, $"field {type.FullName}.{f.Name}", f.Name);
                foreach (var p in type.Properties) CheckOne(errs, label, $"property {type.FullName}.{p.Name}", p.Name);
                foreach (var e in type.Events) CheckOne(errs, label, $"event {type.FullName}.{e.Name}", e.Name);
            }

            return errs;
        }

        private static void CheckOne(List<string> errs, string assembly, string site, string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return;
            string flat = identifier.ToLowerInvariant().Replace("-", "").Replace("_", "");
            foreach (string term in ForbiddenTerms)
            {
                if (flat.Contains(term))
                {
                    errs.Add(
                        $"[AC-7a-09] 忘词令命中:{assembly} 的符号「{site}」含违禁词「{term}」。\n" +
                        "            规则七(2026-09-16 玩家裁定):7a 禁 quicksave / quickload ——\n" +
                        "            「再覆盖回去」的闭合回路 = 事实上的 quicksave,玩家 NEVER 丢过进度。\n" +
                        "            处置:删除该符号或更名;若确属合法用途,须另开裁定轮改 GDD 而非加白名单。");
                    return;   // 同一符号只报一次
                }
            }
        }

        /// <summary>
        /// 门链入口(<c>AssemblyGates.RunAll</c> 调用):对运行期装配逐个跑纯函数层。
        /// </summary>
        public static List<string> RunAll()
        {
            var errs = new List<string>();

            if (RuntimeAssemblies.Length == 0)
            {
                errs.Add("[AC-7a-09] RuntimeAssemblies 为空 —— 扫描集被清空 ⇒ 本门零覆盖,拒以空集冒充绿。");
                return errs;
            }

            foreach (string asmName in RuntimeAssemblies)
            {
                string dll = AssemblyGates.ScriptAssemblyPath(asmName);
                if (!System.IO.File.Exists(dll))
                {
                    errs.Add($"[AC-7a-09] 编译产物缺失「{dll}」—— 扫描面不存在,拒以空集冒充绿。");
                    continue;
                }

                using (var asm = AssemblyDefinition.ReadAssembly(dll, new ReaderParameters
                {
                    // 与 SaveWritePathGate / SaveAsyncBoundaryGate 同款:Deferred 才不触 Resolve。
                    ReadingMode = ReadingMode.Deferred,
                    InMemory = true,
                }))
                {
                    errs.AddRange(CheckSymbolsIn(asm, out int scanned, asmName));
                    if (scanned == 0)
                    {
                        errs.Add($"[AC-7a-09] {asmName}:零个类型被扫到 —— 拒以空集冒充绿。");
                    }
                }
            }

            return errs;
        }
    }
}
