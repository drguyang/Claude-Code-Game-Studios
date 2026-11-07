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
        /// <param name="modal">当前模态(<c>SkeuoRuntimeDriver.CurrentModal</c>;可空 ⇒ 打「?」)。
        /// 🔁 2026-11-07 新增:真包里「就诊 = 开了脉案」此前只能靠截图肉眼判。</param>
        /// <param name="cameraMode">当前相机档位(<c>CameraRig.Mode</c>;可空 ⇒ 打「?」)。
        /// 🔁 2026-11-07 新增:急救档位(Treatment / Explore)同上。</param>
        public static void Report(long tick, int actorId, WorldPos playerCell,
                                  PresenceRegistry presence, string channelState,
                                  int factCount, int seededSteps,
                                  string modal = null, string cameraMode = null,
                                  string routed = null)
        {
            var sb = new StringBuilder(160);
            sb.Append("[tutorialdiag] t=").Append(UnityEngine.Time.realtimeSinceStartup.ToString("F2"));
            sb.Append(" tick=").Append(tick);
            sb.Append(" actor=").Append(actorId);
            sb.Append(" cell=(").Append(playerCell.X).Append(',').Append(playerCell.Y)
              .Append(',').Append(playerCell.Z).Append(')');

            AppendPatients(sb, playerCell, presence);

            sb.Append(" 急救=").Append(channelState ?? "未装配");
            // 🔁 2026-11-07(世界空间手柄 path 的真包判据):模态与相机档位此前**没有读数面** ——
            //    「就诊 = 开了脉案」「急救档位 = Treatment / Explore」在真包里只能靠截图肉眼判。
            //    两列都是**读**呈现层现状(DTO),不参与任何逻辑;不带 `-tutorialdiag` 时零开销。
            sb.Append(" 模态=").Append(modal ?? "?");
            sb.Append(" 档位=").Append(cameraMode ?? "?");
            // 🔁 2026-11-07(真包手柄注入排查):`手柄=` 列把 Unity **看到的** Gamepad 现状打出来 ——
            //    区分「设备没进 Input System」与「进了但按键码对不上」两因。只读、无副作用。
            sb.Append(" 手柄=").Append(DescribeGamepad());
            // 🔁 2026-11-07:`路由=` = 上一次 E(Interact)按下沿选中的种类
            //    (`InteractionPressRouter.LastRoutedKind`;`无` = 选过但没目标,`未建` = 路由未装配)。
            //    用来区分「按键没到驱动」与「到了但一个候选都没选中」。
            sb.Append(" 路由=").Append(routed ?? "?");
            sb.Append(" 教学facts=").Append(factCount).Append(" seeded=").Append(seededSteps);
            UnityEngine.Debug.Log(sb.ToString());
        }

        private static string _lastFocus;

        /// <summary>焦点读数(**每帧调,仅在焦点变化时打一行**)。
        /// <para>病因(2026-11-06 真包走查):走查者**看不见焦点在哪** ⇒ 只能盲按;
        /// 而「开屏根本没有焦点」时所有 Submit 都会被引擎**静默丢弃** ——
        /// 看起来就是「按键没反应」。周期读数又挂在 tick 边沿上,而模态摊开时 **tick 停**
        /// ⇒ 冻结期间一行都没有。故本读数走**每帧**,与 tick 无关。</para></summary>
        /// <param name="root">UI 面板根(可空 ⇒ 空操作)。</param>
        public static void ReportFocusIfChanged(UnityEngine.UIElements.VisualElement root)
        {
            if (root == null || root.panel == null) return;
            var focused = root.panel.focusController.focusedElement as UnityEngine.UIElements.VisualElement;
            string name;
            if (focused == null) name = "(无焦点)";
            else if (!string.IsNullOrEmpty(focused.name)) name = focused.name;
            else name = "<匿名;" + (focused.userData ?? "-") + ">";
            if (name == _lastFocus) return;
            _lastFocus = name;
            UnityEngine.Debug.Log("[tutorialdiag] 焦点=" + name);
        }

        private static string _lastModalTier;

        /// <summary>模态 / 相机档位读数(**每帧调,仅在变化时打一行**)。
        /// <para>🔁 2026-11-07(真包手柄判据):与 <see cref="ReportFocusIfChanged"/> 同因 ——
        /// 周期读数挂在 tick 边沿,而**模态摊开时 tick 停** ⇒ 脉案刚开的那一刻之后一行都没有,
        /// 「就诊 = 开了脉案」在日志里**永远看不到**(实测:整轮 0 条 `模态=Casebook`,
        /// 而屏其实已经开了、世界被冻结)。故走每帧 + 变化才打。</para></summary>
        public static void ReportModalTierIfChanged(string modal, string mode, long tick)
        {
            string key = (modal ?? "?") + "/" + (mode ?? "?");
            if (key == _lastModalTier) return;
            _lastModalTier = key;
            UnityEngine.Debug.Log($"[tutorialdiag] 模态={modal ?? "?"} 档位={mode ?? "?"} tick={tick}");
        }

        /// <summary>模态解冻补跑的瞬间报告(每次解冻一行)。</summary>
        public static void ReportThaw(long tick, int factCount)
        {
            UnityEngine.Debug.Log(
                $"[tutorialdiag] ★解冻补跑 t={UnityEngine.Time.realtimeSinceStartup:F2} tick={tick} facts={factCount}");
        }

        private static string _lastLexiconRings;

        /// <summary>病名册三环读数(**每帧调,仅在数值变化时打一行**)。
        /// <para>病因(2026-11-07):`judge` 步在真包走不通,已定位到「病名册零条目」,
        /// 但条目集 = `PointableLexiconIds(SeenDiseaseIds(viewer))` 三环里**哪一环空**未定 ——
        /// 三环各有完全不同的修法(未设观看者 / 查体没记进新颖度 / 词表反查不通),
        /// 猜错就白改。本读数一次真包即可定位。</para>
        /// <para>⚠️ 走**每帧**而非 tick 边沿:脉案摊开时世界冻结,周期读数一行都不会有。</para></summary>
        /// <param name="viewer">观看者 actorId(负 = 未设)。</param>
        /// <param name="seen">已见病种数(新颖度环)。</param>
        /// <param name="pointable">可指认词条数(词表反查环)。</param>
        /// <param name="rows">词表总行数(0 = 词表没装载)。</param>
        /// <param name="noveltyEntries">新颖度字典总条目数(含采集侧;0 = 从没写过)。</param>
        public static void ReportLexiconRingsIfChanged(int viewer, int seen, int pointable,
                                                      int rows, int noveltyEntries)
        {
            var sb = new StringBuilder(96);
            sb.Append("viewer=").Append(viewer).Append(" seen=").Append(seen)
              .Append(" pointable=").Append(pointable).Append(" 词表行=").Append(rows)
              .Append(" novelty=").Append(noveltyEntries);
            string key = sb.ToString();
            if (key == _lastLexiconRings) return;
            _lastLexiconRings = key;
            UnityEngine.Debug.Log("[tutorialdiag] 病名册环 t=" +
                                  UnityEngine.Time.realtimeSinceStartup.ToString("F2") + " " + key);
        }

        /// <summary>「见过病 D」投影落地的瞬间读数(每次查体一行)。
        /// <para>用途:三环读数说 `novelty=0` 时,靠这行区分「查体根本没走到投影」
        /// 与「走到了但抛了」。**不发火、不写流** —— 只读路径上的既有量。</para></summary>
        public static void ReportExamined(int patientId, int actorId, int diseaseId,
                                          string novelty, long tick)
        {
            UnityEngine.Debug.Log("[tutorialdiag] 查体投影 patient=" + patientId +
                                  " actor=" + actorId + " disease=" + diseaseId +
                                  " novelty=" + novelty + " tick=" + tick);
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

        /// <summary>
        /// Unity **看到的** Gamepad 现状(真包手柄注入排查用;2026-11-07)。
        /// <para>区分两因:设备根本没进 Input System(<c>无</c>)vs 进了但按键码对不上
        /// (名字有了、按住的位没有)。名字里的空格换成 <c>_</c> —— 读数行按空白切分。</para>
        /// </summary>
        private static string DescribeGamepad()
        {
            var gp = UnityEngine.InputSystem.Gamepad.current;
            if (gp == null) return "无";
            var sb = new StringBuilder(40);
            sb.Append((gp.name ?? "?").Replace(' ', '_')).Append('#').Append(gp.deviceId).Append(':');
            if (gp.buttonSouth.isPressed) sb.Append('S');
            if (gp.buttonEast.isPressed) sb.Append('E');
            if (gp.buttonWest.isPressed) sb.Append('W');
            if (gp.buttonNorth.isPressed) sb.Append('N');
            if (gp.startButton.isPressed) sb.Append('T');
            if (gp.leftStick.ReadValue().sqrMagnitude > 0.01f) sb.Append("_LS");
            return sb.ToString();
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
