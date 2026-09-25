// U0-b b2 · b3 · b4 —— 三条构建期断言(装配级)
//
// 权威来源:
//   b2 = ADR-017 §二(门 A 白名单,构建失败级)· ADR-025 §① 注
//        「`Sim` 引用集期望 = {BCL, Sim.Contracts}」—— ⚠️ 不是「恰 = 两元素」:
//        ADR-025 RC-5 已订正,没有任何 asmdef 字段能产出 BCL-only 引用集,
//        netstandard/mscorlib 等基座程序集必然出现 ⇒ 判据 = 「**不含任何引擎程序集,
//        且工程内程序集侧 ⊆ {Sim.Contracts}**」。
//   b3 = ADR-025 §④(未登记 asmdef = 构建失败;清单封闭性)
//   b4 = ADR-025 §② 甲案(ToFloat() 调用点白名单:Sim 内出现 = 构建失败)
//
// 落点理由:三条都是**编辑期**断言,住 Editor.Tools 族(不进构建,门 A 不约束 ——
// ADR-022 §① 同构)。触发面 = Unity 编译后自动刷新(ReloadAssemblyPostProcessor)+
// 手动菜单项;CI 侧由 ADR-012 矩阵的 Editor 格跑 EditMode 断言(归 CI 故事,见 README)。

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>构建期装配门。任一失败 = Debug.LogError + 编译日志红行(不打断编辑器,
    /// CI 以日志红行为失败判据;Unity Build 时由 OnPostProcess 升级为 throw)。</summary>
    internal static class AssemblyGates
    {
        // ── b3 的登记清单(ADR-025 §① 七装配 ∪ 测试族两装配 ∪ Editor.Tools 族)──
        // Editor.Tools 族拆 Level/Kindgen 两个具名装配是卡 §0.1 的落地形(ADR-025 表记「族」);
        // Gates(U0-b)+ Spike(U1 spike 批)+ Bake(Story 008 数据管线)同属该族追加 ——
        // 族内增员 = 只改本清单,不重开 ADR-025 §①。
        // Gameplay.Input = story-001 B1 拆装增员(ADR-025 §① 2026-09-25 已载;清单 2026-09-25 补登)。
        private static readonly HashSet<string> Manifest = new HashSet<string>
        {
            "Sim", "Sim.Contracts", "Sim.Codec",
            "Gameplay.Presentation", "Gameplay.UI", "Gameplay.Input",
            "Editor.Tools.Level", "Editor.Tools.Kindgen", "Editor.Tools.Gates",
            "Editor.Tools.Spike", "Editor.Tools.Bake",
            "Sim.Contracts.Tests", "Gameplay.Tests",
        };

        // ── b4 的白名单:允许调 ToFloat() 的装配(ADR-025 §② 甲案 = {Sim.Codec, Gameplay.*})──
        private static readonly string[] ToFloatWhitelistPrefixes =
            { "Sim.Codec", "Gameplay.Presentation", "Gameplay.UI", "Gameplay.Tests" };

        [MenuItem("大医精诚/Validation/Run Assembly Gates")]
        private static void RunMenu()
        {
            var errs = RunAll();
            CheckToFloatCallsites(errs);
            foreach (var e in errs) Debug.LogError(e);
            Debug.Log(errs.Count == 0
                ? "[AssemblyGates] b2/b3/b4 全过"
                : $"[AssemblyGates] {errs.Count} 条失败(见红行)");
        }

        public static List<string> RunAll()
        {
            var errs = new List<string>();
            CheckManifestClosure(errs);      // b3
            CheckGateA(errs);                // b2
            return errs;
        }

        // ═══ b3 装配封闭性:工程内 asmdef 名集合 ⊆ Manifest;Manifest 成员全存在 ═══
        // ⚠️ 程序集名取 asmdef **JSON 的 name 字段**,不取文件名 —— 实测两装配文件名
        //   (EditMode.asmdef / PlayMode.asmdef)与其声明名(Sim.Contracts.Tests /
        //   Gameplay.Tests)不一致;按文件名比对 = b3 自身误报。
        private static void CheckManifestClosure(List<string> errs)
        {
            // ⚠️ 扫描面 = Assets/ **之内**(工程自有装配)。
            // AssetDatabase.FindAssets("t:asmdef") 实测连 Packages/ 下解析出的包内
            // asmdef 一并吐回(Addressables 依赖图带进 ~100 支)⇒ 必须按路径前缀过滤。
            // ADR-025 §④ 的「未登记 asmdef = 构建失败」管的是本项目 asmdef,
            // 第一方包(Embedded)当前不存在;若日后引入,须连同清单口径一起裁。
            var found = new HashSet<string>();
            foreach (var f in Directory.GetFiles("Assets", "*.asmdef", SearchOption.AllDirectories))
            {
                var json = File.ReadAllText(f);
                var m = System.Text.RegularExpressions.Regex.Match(json, "\"name\"\\s*:\\s*\"([^\"]+)\"");
                if (m.Success) found.Add(m.Groups[1].Value);
            }
            foreach (var extra in found.Where(f => !Manifest.Contains(f)))
                errs.Add($"[b3] 未登记 asmdef「{extra}」—— 清单封闭性 = 构建失败(ADR-025 §④)。" +
                         "新增装配须先回写 ADR-025 §① 表。");
            foreach (var missing in Manifest.Where(m => !found.Contains(m)))
                errs.Add($"[b3] 清单成员「{missing}」在工程内不存在 —— 装配被删/改名?");
        }

        // ═══ b2 门 A:Sim / Sim.Contracts / Sim.Codec 三装配的引用集零引擎程序集 ═══
        private static readonly string[] GateAAssemblies = { "Sim", "Sim.Contracts", "Sim.Codec" };

        private static void CheckGateA(List<string> errs)
        {
            foreach (var asmName in GateAAssemblies)
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                              .FirstOrDefault(a => a.GetName().Name == asmName);
                if (asm == null)
                {
                    // 编译失败时本断言不可用 —— 红在编译器,不重复报。
                    continue;
                }
                var refs = asm.GetReferencedAssemblies().Select(r => r.Name).ToList();
                // Sim 侧的工程内允许集 = {Sim.Contracts}(Sim)或 ∅(Contracts)/ +Sim.Contracts(Codec)
                foreach (var r in refs.Where(r =>
                             r.StartsWith("UnityEngine") || r.StartsWith("UnityEditor") ||
                             r.StartsWith("Unity.") ))
                {
                    if (r.StartsWith("UnityEngine.TestRunner") || r.StartsWith("UnityEditor.TestRunner"))
                        continue;   // UTF 注入豁免(仅测试装配会出现;门 A 三装配本不该见到)
                    errs.Add($"[b2] 门 A 装配「{asmName}」引用了引擎程序集「{r}」—— 构建失败" +
                             "(ADR-017 §二:noEngineReferences 声明之外的隐式解析也算违例)。");
                }
                if (asmName == "Sim")
                {
                    var nonBcl = refs.Where(r =>
                        !r.StartsWith("System") && !r.StartsWith("netstandard") &&
                        r != "mscorlib" && r != "Mono").Except(new[] { "Sim.Contracts" });
                    foreach (var r in nonBcl)
                        errs.Add($"[b2] Sim 引用集出现 BCL∪{{Sim.Contracts}} 之外的「{r}」—— " +
                                 "期望引用集白名单违例(ADR-025 §①)。");
                }
            }
        }

        // ═══ b4 ToFloat() 调用点扫描(Sim 装配源文件内出现 = 违例)═══
        // 口径:源文本级扫描。**刻意不用 Roslyn**(ADR-024 §⑤ 同款口径「不引 analyzer」);
        // 命中 = `.ToFloat(` 出现在 Sim/Gameplay.Presentation 之外装配目录下。
        // 已知漏报面:注释与字符串字面量(误报方向,偏安全);`Fix x; x.ToFloat()` 经
        // 变量名任意 ⇒ 必须带点前缀匹配,不做纯标识符匹配(会漏 this.x.ToFloat() 的反向)。
        private static void CheckToFloatCallsites(List<string> errs)
        {
            var asmDirs = new Dictionary<string, string>
            {
                { "Sim", "Assets/Sim" },
                { "Sim.Codec", "Assets/Sim.Codec" },
                { "Gameplay.Presentation", "Assets/Gameplay.Presentation" },
                { "Gameplay.UI", "Assets/Gameplay.UI" },
            };
            foreach (var kv in asmDirs)
            {
                if (!Directory.Exists(kv.Value)) continue;
                foreach (var f in Directory.GetFiles(kv.Value, "*.cs", SearchOption.AllDirectories))
                {
                    // .g.cs 生成物同样在扫描面内(白名单按装配不按文件)。
                    var lines = File.ReadAllLines(f);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        int idx = lines[i].IndexOf(".ToFloat(", StringComparison.Ordinal);
                        if (idx < 0) continue;
                        bool allowed = kv.Key == "Gameplay.Presentation" || kv.Key == "Gameplay.UI"
                                    || ToFloatWhitelistPrefixes.Any(p => kv.Key.StartsWith(p));
                        if (!allowed)
                            errs.Add($"[b4] {f}:{i + 1} 装配「{kv.Key}」内出现 ToFloat() 调用 —— " +
                                     "Sim/Sim.Codec 侧 = 构建失败(ADR-025 §② 甲案白名单)。");
                    }
                }
            }
        }

        [InitializeOnLoadMethod]
        private static void RegisterReloadHook() => EditorApplication.delayCall += () =>
        {
            // 编译后自动跑一次 b3/b2(便宜:程序集名集合级);b4 文件扫描同样 <10ms 量级。
            var errs = RunAll();
            CheckToFloatCallsites(errs);
            foreach (var e in errs) Debug.LogError(e);
        };
    }
}
#endif
