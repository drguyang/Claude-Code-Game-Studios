// 关卡工具 —— **CI 入口**(AC-4:整条 C1–C6 检查链可在 CI 里跑,不需要打开编辑器 GUI)。
//
// 权威来源:
//   ADR-022 §四(C1–C6;硬失败项 `throw`,告警项不使构建失败)· §Validation
//     (「两层一致性检查在 CI 中对已提交关卡可跑通」)
//   `coding-standards.md` §Review Evidence Standards(「某件有门」的自述须给 `file:line`
//     + 一条能证明它**真的会红**的命令)
//
// ═══════════════════════════════════════════════════════════════════════════
// 退出码约定(**三态**,不是两态)
// ═══════════════════════════════════════════════════════════════════════════
//   0 = 没有硬失败(告警可以有,`NotRun` 也可以有 —— 两者都**不**使构建失败)
//   1 = 有硬失败(C1–C6 里的 `throw` 项)
//   2 = **检查链本身跑不起来**(关卡源构造失败 / 未捕获异常)——
//       ⚠️ 与 1 **刻意分开**:「代码有缺陷」与「门坏了」的处置完全不同,
//       合成一个码会让「门坏了」看起来像「代码红了」,而本仓已经踩过
//       「门没跑到」被读成「门跑了且通过」的反向版本。
//
// ⚠️ **`NotRun` 不影响退出码,但一定会被打印。** 静默跳过 = 借绿。

using System;
using System.Collections.Generic;
using System.Text;
using DaYiJingCheng.Sim.Contracts;   // WorldPos
using DaYiJingCheng.Sim.World;       // IModuleCatalog
using UnityEngine;                    // CharacterController / GameObject

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>一次 CI 检查的结果(退出码 + 可读报告)。</summary>
    public readonly struct LevelCiResult
    {
        /// <summary>`0` 无硬失败 · `1` 有硬失败 · `2` 检查链跑不起来。</summary>
        public readonly int ExitCode;

        /// <summary>检查报告(`ExitCode == 2` 时为 `null`)。</summary>
        public readonly CheckReport Report;

        /// <summary>致命错误报文(`ExitCode == 2` 时非空)。</summary>
        public readonly string Fatal;

        /// <summary>构造结果。</summary>
        public LevelCiResult(int exitCode, CheckReport report, string fatal)
        {
            ExitCode = exitCode;
            Report = report;
            Fatal = fatal;
        }

        /// <summary>渲染成可读文本(CI 日志 / 终端)。</summary>
        public string Render()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[关卡工具 CI] C1–C6 一致性检查");

            if (ExitCode == 2)
            {
                sb.AppendLine($"  ✗ 检查链未能跑起来(退出码 2):{Fatal}");
                return sb.ToString();
            }

            if (Report.Violations.Count > 0)
            {
                sb.AppendLine($"  ✗ 硬失败 {Report.Violations.Count} 条:");
                foreach (string v in Report.Violations) sb.AppendLine($"      · {v}");
            }
            else
            {
                sb.AppendLine("  ✓ 无硬失败");
            }

            if (Report.Warnings.Count > 0)
            {
                sb.AppendLine($"  ⚠ 告警 {Report.Warnings.Count} 条(**不**使构建失败):");
                foreach (string w in Report.Warnings) sb.AppendLine($"      · {w}");
            }

            // ⚠️ `NotRun` 永远打印 —— 它是「没跑」与「跑过且干净」在日志上的唯一区分。
            if (Report.NotRun.Count > 0)
            {
                sb.AppendLine($"  ○ 未跑 {Report.NotRun.Count} 条(无输入,不是通过):");
                foreach (string n in Report.NotRun) sb.AppendLine($"      · {n}");
            }

            if (Report.Unimplemented.Count > 0)
            {
                sb.AppendLine($"  ? 尚未实现 {Report.Unimplemented.Count} 条:");
                foreach (string u in Report.Unimplemented) sb.AppendLine($"      · {u}");
            }

            sb.AppendLine($"  退出码 = {ExitCode}(0 无硬失败 / 1 有硬失败 / 2 链跑不起来)");
            return sb.ToString();
        }
    }

    /// <summary>CI 入口:一条命令跑完 C1–C6,并把结果落成退出码。</summary>
    public static class LevelCiRunner
    {
        /// <summary>无硬失败。</summary>
        public const int ExitGreen = 0;
        /// <summary>有硬失败。</summary>
        public const int ExitViolation = 1;
        /// <summary>检查链跑不起来(关卡源构造失败 / 未捕获异常)。</summary>
        public const int ExitFatal = 2;

        /// <summary>
        /// 跑一次完整检查(**不退出进程**;夹具与 `RunBatch` 共用这一条路径)。
        /// </summary>
        /// <param name="src">关卡源。</param>
        /// <param name="controller">引擎侧控制器(C2 的另一半);`null` ⇒ C2 进 `NotRun`。</param>
        /// <param name="catalog">模块目录(C2' 的占用格真源);`null` ⇒ 足迹半边进 `NotRun`。</param>
        /// <param name="navSampler">C3 采样器;`null` ⇒ 不跑 C3(并登记 `NotRun`)。</param>
        /// <param name="engineParams">
        /// 引擎侧参数(已量化)。**优先于** <paramref name="controller"/> —— CI 入口用
        /// `CharacterControllerParamProbe.ProbeCurrent()` 取(带缓存),夹具可直接注入。
        /// 两者都不给 ⇒ C2 进 `NotRun`。
        /// </param>
        /// <returns>结果(退出码 + 报告)。</returns>
        public static LevelCiResult Run(LevelSource src, CharacterController controller = null,
            IModuleCatalog catalog = null, Func<WorldPos, NavReachability> navSampler = null,
            LatticeEngineParams? engineParams = null)
        {
            try
            {
                // 显式给的参数优先(导出器/CI 用 ProbeCurrent 取);否则从 controller 现读。
                LatticeEngineParams? engine = engineParams;
                if (!engine.HasValue &&
                    CharacterControllerParamProbe.TryRead(controller, out LatticeEngineParams p, out _))
                    engine = p;

                CheckReport report = LevelConsistencyCheck.Check(src, navSampler, engine, catalog);
                return new LevelCiResult(report.IsGreen ? ExitGreen : ExitViolation, report, null);
            }
            catch (Exception ex)
            {
                // ⚠️ 这里**必须**接住:未捕获异常在 batch 模式下会以别的码退出,
                //    而那与本件约定的 1 / 2 都无法区分。
                return new LevelCiResult(ExitFatal, null, $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// 批处理入口(`-executeMethod DaYiJingCheng.EditorTools.Level.LevelCiRunner.RunBatch`)。
        /// <para>⚠️ 须与 `-quit` 同时使用(承 CLAUDE.md 的 CLI 纪律:`-executeMethod` **必须**带 `-quit`)。</para>
        /// </summary>
        public static void RunBatch()
        {
            LevelCiResult result;
            try
            {
                LevelSource src = P0ClinicLevel.Define();

                // 引擎侧参数的**唯一可信来源** = 一个真 `CharacterController`。
                // 🔁 **2026-10-29 复评 B-9 修复**:此前这里**自己** new 了一个探针对象,
                //    与导出器那 7 处是**两份同构实现**(连「比的是引擎默认、非玩家实测」的
                //    自述都逐字重复)—— 单一出处被破坏,两处日后必然漂移。
                //    现统一走 `CharacterControllerParamProbe.ProbeCurrent()`(带 domain 级缓存)。
                // ⚠️ 它读的是 **Unity 的默认参数** —— 今日 `PlayerController` 尚未写
                //    `slopeLimit` / `stepOffset`(那两条归系统 1,债册 `TD-player-01`)。
                //    **如实登记:本 CI 比的是「烘焙值 vs 引擎默认」,不是「烘焙值 vs 玩家实测」。**
                result = Run(src, controller: null, catalog: null,
                             navSampler: NavMeshReachabilitySampler.For(src),
                             engineParams: CharacterControllerParamProbe.ProbeCurrent());
            }
            catch (Exception ex)
            {
                result = new LevelCiResult(ExitFatal, null, $"{ex.GetType().Name}: {ex.Message}");
            }

            Console.WriteLine(result.Render());
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(result.ExitCode);
#else
            Environment.Exit(result.ExitCode);
#endif
        }
    }
}
