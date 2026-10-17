// ADR-012 §Validation · F2 spike 的 **CLI 可达化**探针(2026-10-17 立)
//
// 权威来源:
//   ADR-012 §二:CI 对拍矩阵三格 = Linux-x64-Mono / Linux-x64-IL2CPP / **Linux-ARM64-IL2CPP(交叉构建)**
//   ADR-012 §二「矩阵实现(F1/F2 修正)」:154-156 —— 「Linux-ARM64 是受支持目标」
//   ADR-012 §Verification ②:**F2 spike = x86-64 runner 交叉构建 ARM64 IL2CPP player
//     → 原生 ARM64 runner 只跑 player,验证链路可用性** —— 该 spike **未跑**
//   project CLAUDE.md「Unity Debugging(CLI First)」:凡 CLI 能确认的,一律自己跑,不让用户手动操作编辑器
//
// ⚠️ **为什么需要它(而不是继续把「未跑」挂在文档里)**
//   F2 的前置是一个**本机可答的事实问题**:当前工具链到底有没有 Linux-ARM64 构建目标?
//   此前只能从「PlaybackEngines 目录里没看到 arm64」**间接推断**,推断不是证据 ——
//   而 ADR-012 的矩阵是否成立**完全**取决于这个问题的答案。
//   本探针把它变成一条**可复跑的命令**:枚举 `UnityEditor.BuildTarget` 全量 +
//   逐目标 `BuildPipeline.IsBuildTargetSupported` 判决 + PlaybackEngines 落盘列举。
//   ⇒ 引擎升级后重跑同一命令即可复核(承 ADR-012 §Risks「引擎升级时须重跑矩阵」)。
//
// ⚠️ **本类只读不写**:不碰任何资产、不改任何设置、不产任何产物。
//   纯诊断 ⇒ 可安全地在任何 batch 会话里跑,无「先验后改」问题。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DaYiJingCheng.EditorTools.Bake.Cli
{
    /// <summary>构建目标能力探针(ADR-012 F2 spike 的前置事实;CLI 只读诊断)。</summary>
    public static class BuildTargetProbeCli
    {
        /// <summary>batchmode 入口:<c>-executeMethod DaYiJingCheng.EditorTools.Bake.Cli.BuildTargetProbeCli.Probe</c>。
        /// <para>输出全部走 <c>Debug.Log</c>(batch log 可 grep);**恒 exit 0** ——
        /// 本探针报告事实,不判决成败(判决归读日志的人 / ADR 轮)。</para></summary>
        public static void Probe()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══ BuildTarget 能力探针(ADR-012 F2)═══");
            sb.AppendLine("Unity 版本:" + Application.unityVersion);
            sb.AppendLine("平台:" + Application.platform);
            sb.AppendLine("CPU 架构:" + SystemInfo.processorType);

            // ── ① 全量 BuildTarget 枚举名(高亮 arm64 / linux 相关)──
            string[] names = Enum.GetNames(typeof(BuildTarget));
            Array.Sort(names, StringComparer.Ordinal);

            var armish = new List<string>();
            var linuxish = new List<string>();
            foreach (string n in names)
            {
                string lower = n.ToLowerInvariant();
                if (lower.Contains("arm")) armish.Add(n);
                if (lower.Contains("linux")) linuxish.Add(n);
            }

            sb.AppendLine($"── ① BuildTarget 枚举共 {names.Length} 项 ──");
            sb.AppendLine("  含「arm」的目标:" + (armish.Count == 0 ? "(无)" : string.Join(", ", armish)));
            sb.AppendLine("  含「linux」的目标:" + (linuxish.Count == 0 ? "(无)" : string.Join(", ", linuxish)));

            // ── ② 逐目标支持性判决(只判 arm/linux 相关 + 本机 Standalone 族,避免刷屏)──
            sb.AppendLine("── ② IsBuildTargetSupported 逐条判决 ──");
            var probe = new List<string>();
            probe.AddRange(armish);
            probe.AddRange(linuxish);
            foreach (string extra in new[] { "StandaloneLinux64", "StandaloneWindows64", "StandaloneOSX" })
                if (!probe.Contains(extra)) probe.Add(extra);
            probe.Sort(StringComparer.Ordinal);

            foreach (string n in probe)
            {
                if (!Enum.TryParse(n, out BuildTarget t)) { sb.AppendLine($"  {n,-32} = 枚举无此项"); continue; }
                bool supported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, t);
                sb.AppendLine($"  {n,-32} = {(supported ? "✅ 受支持" : "❌ 不受支持")}");
            }

            // ── ③ PlaybackEngines 落盘列举(受支持性的**物理**旁证)──
            sb.AppendLine("── ③ PlaybackEngines 落盘 ──");
            string pe = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines");
            if (Directory.Exists(pe))
            {
                var dirs = new List<string>(Directory.GetDirectories(pe));
                dirs.Sort(StringComparer.Ordinal);
                foreach (string d in dirs)
                {
                    sb.AppendLine("  " + Path.GetFileName(d));
                    string variations = Path.Combine(d, "Variations");
                    if (!Directory.Exists(variations)) continue;
                    var vs = new List<string>(Directory.GetDirectories(variations));
                    vs.Sort(StringComparer.Ordinal);
                    foreach (string v in vs) sb.AppendLine("      └ " + Path.GetFileName(v));
                }
            }
            else sb.AppendLine("  (无 PlaybackEngines 目录)");

            // ── ④ 脚本后端可用性 ──
            sb.AppendLine("── ④ 脚本后端 ──");
            sb.AppendLine("  Standalone 当前后端 = " + PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone));
            sb.AppendLine("  IL2CPP 是否已装(Standalone) = " +
                (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64)
                    ? "见 ② 判决" : "N/A"));
            sb.AppendLine("═══ 探针结束 ═══");

            Debug.Log(sb.ToString());
        }
    }
}
