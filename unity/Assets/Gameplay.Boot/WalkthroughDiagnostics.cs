// 真包走查的**现场读数**(2026-11-06 · 承 S25-4c 走查的「盲走」教训)。
//
// ── 病因 ────────────────────────────────────────────────────────────────────
// 走查时看不出「玩家在哪一格、病人在哪一格、急救通道什么态、教学链解了几条事实」——
// 于是 `E` 按了没反应时,无法区分「没走到跟前」「交互被拒」「教学根本没被叫到」。
// 离线工具(`SaveCellDumpCli`)只能读**落盘的 checkpoint**(最多滞后 5 分钟),
// 且**读不到病人格**(病人位置是呈现态,不进流)。
//
// ── 本件做什么 ──────────────────────────────────────────────────────────────
// **仅在带 `-tutorialdiag` 启动时**每秒(tick 整除 20)打一行现场读数:
//   tick / 玩家 actor 与格 / 在场病人数 / 最近病人的 id·格·距离 / 急救通道态 / 教学链事实数
// 教学步发火的瞬间另打一行(步 id + 本轮口述条数)。
//
// ── 边界 ────────────────────────────────────────────────────────────────────
// ⚠️ **零默认开销**:旗标只在第一次访问时解析一次;未带旗标 ⇒ 每个 tick 只有一次
//    静态 bool 读,不打日志、不拼字符串。
// ⚠️ **只读**:不写流、不改状态、不碰存档 —— 它只把已有的运行期量拼成一行人读文本。
// ⚠️ **不是遥测**:输出走 `Debug.Log`(真包落 `-logFile`),与 51 的本地优先口径无冲突
//    (ADR-019:零出厂数据;本件只在开发者显式带旗标时说话)。
//
// 用法:
//   ./大医精诚.x86_64 -logFile <path>.log -tutorialdiag

using System;
using System.Text;
using DaYiJingCheng.Gameplay.PatientAI;   // PresenceRegistry(取病人格的真源)
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>`-tutorialdiag` 现场读数(默认关闭)。</summary>
    public static class WalkthroughDiagnostics
    {
        /// <summary>命令行旗标。带它才说话。</summary>
        public const string Flag = "-tutorialdiag";

        /// <summary>口述音轨复查旗标(`-tutorialnarrcheck`)。
        /// <para>带它 ⇒ 启动后把 48 的**全部口述**用生产解析面依次播一遍(每条一次,间隔 <see cref="NarrationCheckGapSeconds"/> 秒)。
        /// 用途:回答「其余几条 cue 在真包里解析得出、播得响吗」——
        /// **不替教学链发火**(发火仍只由事件流驱动),只走同一条 `翻译层 → 44 → 42` 路径。</para>
        /// <para>⚠️ 与 <see cref="Flag"/> 独立:两者可单用也可同用。默认关闭 ⇒ 生产零开销。</para></summary>
        public const string NarrationCheckFlag = "-tutorialnarrcheck";

        /// <summary>口述复查的条间间隔(秒)—— 须**大于最长素材**(Close 4.9 s),否则录音里会叠在一起。</summary>
        public const float NarrationCheckGapSeconds = 5.5f;

        /// <summary>每多少 tick 打一行(20 Hz ⇒ 1 s)。</summary>
        public const int ReportEveryTicks = 20;

        private static bool _resolved;
        private static bool _enabled;

        /// <summary>是否带旗标启动(进程内只解析一次)。</summary>
        public static bool Enabled
        {
            get
            {
                if (!_resolved)
                {
                    _resolved = true;
                    _enabled = Detect();
                }
                return _enabled;
            }
        }

        private static bool _narrationResolved;
        private static bool _narrationEnabled;

        /// <summary>是否带 `-tutorialnarrcheck` 启动(进程内只解析一次)。</summary>
        public static bool NarrationCheckEnabled
        {
            get
            {
                if (!_narrationResolved)
                {
                    _narrationResolved = true;
                    _narrationEnabled = HasArgument(NarrationCheckFlag);
                }
                return _narrationEnabled;
            }
        }

        private static bool Detect() => HasArgument(Flag);

        private static bool HasArgument(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>打一行现场读数。</summary>
        /// <param name="tick">当前 tick。</param>
        /// <param name="actorId">本机玩家 actor id(未生成 ⇒ 负数)。</param>
        /// <param name="playerCell">玩家最后提交的逻辑格。</param>
        /// <param name="presence">在场登记簿(可空 ⇒ 打「无登记簿」)。</param>
        /// <param name="channelState">急救通道态的字面(<c>null</c> ⇒ 打「未装配」)。</param>
        /// <param name="factCount">教学链已解码的事实条数。</param>
        /// <param name="seededSteps">构造期播种时已完成的步数。</param>
        public static void Report(long tick, int actorId, WorldPos playerCell,
                                  PresenceRegistry presence, string channelState,
                                  int factCount, int seededSteps)
        {
            var sb = new StringBuilder(160);
            sb.Append("[tutorialdiag] t=").Append(UnityEngine.Time.realtimeSinceStartup.ToString("F2"));
            sb.Append(" tick=").Append(tick);
            sb.Append(" actor=").Append(actorId);
            sb.Append(" cell=(").Append(playerCell.X).Append(',').Append(playerCell.Y)
              .Append(',').Append(playerCell.Z).Append(')');

            AppendPatients(sb, playerCell, presence);

            sb.Append(" 急救=").Append(channelState ?? "未装配");
            sb.Append(" 教学facts=").Append(factCount).Append(" seeded=").Append(seededSteps);
            UnityEngine.Debug.Log(sb.ToString());
        }

        /// <summary>模态解冻补跑的瞬间报告(每次解冻一行)。</summary>
        public static void ReportThaw(long tick, int factCount)
        {
            UnityEngine.Debug.Log(
                $"[tutorialdiag] ★解冻补跑 t={UnityEngine.Time.realtimeSinceStartup:F2} tick={tick} facts={factCount}");
        }

        /// <summary>教学步发火的瞬间报告。</summary>
        public static void ReportFired(long tick, System.Collections.Generic.IReadOnlyList<string> steps, int narrations)
        {
            var sb = new StringBuilder(96);
            sb.Append("[tutorialdiag] ★发火 tick=").Append(tick).Append(" steps=");
            if (steps == null || steps.Count == 0) sb.Append("(空)");
            else
                for (int i = 0; i < steps.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(steps[i]);
                }
            sb.Append(" 口述=").Append(narrations);
            UnityEngine.Debug.Log(sb.ToString());
        }

        private static void AppendPatients(StringBuilder sb, WorldPos playerCell, PresenceRegistry presence)
        {
            if (presence == null)
            {
                sb.Append(" 病人=(无登记簿)");
                return;
            }

            System.Collections.Generic.IReadOnlyCollection<int> ids = presence.PresentPatientIds();
            sb.Append(" 在场=").Append(ids == null ? 0 : ids.Count);
            if (ids == null || ids.Count == 0) return;

            int nearestId = -1;
            int nearestDist = int.MaxValue;
            var cells = new StringBuilder();
            foreach (int id in ids)
            {
                if (!presence.TryGetCell(new PatientId(id), out WorldPos cell)) continue;
                cells.Append(" #").Append(id).Append('@').Append('(')
                     .Append(cell.X).Append(',').Append(cell.Y).Append(',').Append(cell.Z).Append(')');

                int d = Math.Max(Math.Abs(cell.X - playerCell.X), Math.Abs(cell.Z - playerCell.Z));
                if (d < nearestDist) { nearestDist = d; nearestId = id; }
            }

            sb.Append(cells);
            if (nearestId >= 0)
                sb.Append(" 最近=#").Append(nearestId).Append(" 距离=").Append(nearestDist);
        }
    }
}
