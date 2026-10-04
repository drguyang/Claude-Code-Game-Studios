// patient-ai Story 002 —— F-13.7 逻辑格步进(积分量,防跳格)。
//
// 权威来源:
//   GDD design/gdd/patient-ai.md §Formulas F-13.7(2026-09-18 R6 新增,照 F-27-4 纪律)
//   ADR-016 §九(2026-09-17 就地修订:冻结判据只用 sim 量 · `cell`/`acc` 是**积分量**,
//     跳过 step 就是少积分 ⇒ 冻结确实改变位姿,保证收窄为「可由三源重放复现」+「冻结须同冻 acc」)
//   TR-patient-016(sim 走整数导航格)· TR-patient-017(解冻不补算)
//
// ⚠️ **与 F-27-4 的关系 = 照搬纪律,不是共享代码**(GDD F-13.7 注):
//   13 与 27 共享基础设施但不共享代码;状态集不同(27 六态 / 13 三态 + 会诊)。
//   本件是 27 侧累加器纪律在 13 侧的**独立落实**。
//
// ⚠️ **三条不变量**(构建期断言):
//   ① `PATIENT_SPEED < FIX_ONE`(防跳格隧穿 —— 单 tick 前进 ≥ 2 格会穿过未查过的格);
//   ② `0 ≤ acc < FIX_ONE` 恒成立(EC-13-04);
//   ③ **冻结期 `acc` 不变**(AC-13-E5)—— 若冻结期仍累加,解冻首 tick 一次吐出攒下的步数
//      ⇒ `while` 把病人**瞬移**,那几格**从未被查过**。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.PatientAI
{
    /// <summary>步进所需的每病人积分态(F-13.7 的 `cell` / `acc` / `path_cursor`)。
    /// <para>**住 13 的派生态字典**(边界层内存),**不写回 sim**(ADR-027:13 零写)。
    /// 重建后由格 + 锚点**重新播种**(`acc := 0`,AC-13-B4)。</para></summary>
    public struct LogicalPose
    {
        /// <summary>当前逻辑格(整数,ADR-015 §三)。</summary>
        public WorldPos Cell;
        /// <summary>累加器余数 —— **恒 ∈ [0, FIX_ONE)**。</summary>
        public Fix Acc;
        /// <summary>整数路径游标(当前格在 `Path` 中的下标)。</summary>
        public int PathCursor;
        /// <summary>朝向(整数方向枚举,仅表现;F-13.7 的 `Facing`)。</summary>
        public int Facing;

        /// <summary>按格 + 锚点播种(AC-13-B4:`acc := 0`,不沿用存档前值)。</summary>
        public static LogicalPose Seed(WorldPos cell)
            => new LogicalPose { Cell = cell, Acc = Fix.Zero, PathCursor = 0, Facing = 0 };
    }

    /// <summary>`Moving(p)` 的五个合取项(GDD F-13.7)—— 逐字落地。
    /// <para>⚠️ 注意断言对象是**逻辑量**:13 不拥有表现态位置(AC-13-B2 R6 重写)。</para></summary>
    public readonly struct MovingInputs
    {
        /// <summary>`Terminal(p) == false` —— 终态锁存后不再移动。</summary>
        public readonly bool Terminal;
        /// <summary>`BehaviorState(p) ≠ Bedridden` —— 倒地态不产生位移(AC-13-B2)。</summary>
        public readonly BehaviorState Behavior;
        /// <summary>`SeekingPhase(p) == EnRoute` —— `AtClinic` 已到位,不再前进。</summary>
        public readonly SeekingPhase Phase;
        /// <summary>`SessionState(p) ≠ InTreatment` —— 会诊中站定。</summary>
        public readonly SessionState Session;
        /// <summary>`¬Frozen(p)` —— LOD 冻结(输入须 ∈ 白名单,AC-13-E4)。</summary>
        public readonly bool Frozen;

        public MovingInputs(bool terminal, BehaviorState behavior, SeekingPhase phase,
                            SessionState session, bool frozen)
        {
            Terminal = terminal; Behavior = behavior; Phase = phase;
            Session = session; Frozen = frozen;
        }
    }

    /// <summary>F-13.7 的纯整数落实 —— 累加器步进,**格坐标永远整数**。</summary>
    public static class LogicalStepper
    {
        /// <summary>`Moving(p)`(GDD F-13.7 合取式)。
        /// <para>`¬Terminal ∧ State ≠ Bedridden ∧ Phase == EnRoute ∧ Session ≠ InTreatment ∧ ¬Frozen`</para></summary>
        public static bool Moving(in MovingInputs m)
            => !m.Terminal
               && m.Behavior != BehaviorState.Bedridden
               && m.Phase == SeekingPhase.EnRoute
               && m.Session != SessionState.InTreatment
               && !m.Frozen;

        /// <summary>单 tick 步进(GDD F-13.7 伪码逐字落地)。
        /// <para>推进后写入 <paramref name="pose"/>;返回本 tick **实际前进的格数 ≤ 1**
        /// (由 `PATIENT_SPEED &lt; FIX_ONE` 保证)。</para>
        /// <para>⚠️ `Path` 为空或已到终点 ⇒ 不动(禁原地转圈假推进);此时 `acc` **仍按
        /// `Moving` 前进**(承 F-13.7 伪码:累加先于 `while`,无路可走时余数保留)。</para></summary>
        public static int Step(ref LogicalPose pose, bool moving, in Fix patientSpeed,
                               IReadOnlyList<WorldPos> path)
        {
            // ¬Moving:acc 不累加、不清零(冻结须同冻 acc —— AC-13-E5)
            if (!moving)
                return 0;

            // ⚠️ **路径已耗尽 ⇒ 等价于「到位」,按 ¬Moving 处理**(F-13.7:`SeekingPhase == AtClinic`
            //    时 `Moving` 为假)。若不在此处挡住,`acc` 会一路累加越过 FIX_ONE
            //    —— 而 `while` 无法消费它(无格可走)⇒ 破 `0 ≤ acc < FIX_ONE` 不变量。
            if (path == null || pose.PathCursor + 1 >= path.Count)
                return 0;

            pose.Acc = pose.Acc + patientSpeed;

            int advanced = 0;
            // ⚠️ `Fix` 刻意无 `<` / `>=` 等序关系算子(ADR-005:226 同族纪律:不做隐式 float)。
            //    raw 与值同序,故比较走 `.Raw`。
            //
            // ⚠️ **格耗尽检查必须在消费 `acc` 之前** —— 早前版本先 `acc -= FIX_ONE` 再检查路径,
            //    路径到终点时 break ⇒ **白白扣掉一整格余数**(acc 静默丢失 + 破坏不变量)。
            //    正确语义:余数只在**真的走了一格**时才被消费(F-13.7:acc 是「已攒够但还没走」的量)。
            while (pose.Acc.Raw >= Fix.OneRaw)
            {
                int next = pose.PathCursor + 1;
                if (path == null || next >= path.Count)
                    break;   // 路径耗尽:余数**保留在 acc**(未消费),不前进

                pose.Acc = pose.Acc - Fix.One;
                pose.Cell = path[next];
                pose.PathCursor = next;
                advanced++;
            }

            // 朝向:指向下一格(或当前格,若已到终点)—— F-13.7 `Facing`
            int ahead = Math.Min(pose.PathCursor + 1, (path?.Count ?? 0) - 1);
            if (path != null && ahead >= 0 && ahead < path.Count && ahead != pose.PathCursor)
                pose.Facing = DirectionOf(pose.Cell, path[ahead]);
            else
                pose.Facing = DirectionOf(pose.Cell, pose.Cell);

            return advanced;
        }

        /// <summary>整数朝向(仅表现;F-13.7 `DirFrom`)。粗粒度 —— 归表现态。</summary>
        public static int DirectionOf(WorldPos from, WorldPos to)
        {
            int dx = Math.Sign(to.X - from.X);
            int dz = Math.Sign(to.Z - from.Z);
            // 8 向编码:0..7,(-1,-1)..(1,1) 展平;中心 = 8(无方向)
            if (dx == 0 && dz == 0) return 8;
            return (dx + 1) * 3 + (dz + 1);
        }

        /// <summary>`PATIENT_SPEED &lt; FIX_ONE` 构建期断言(GDD F-13.7 ② 防跳格隧穿)。</summary>
        public static bool SpeedIsSafe(Fix patientSpeed)
            => patientSpeed.Raw >= 0 && patientSpeed.Raw < Fix.OneRaw;

        /// <summary>`0 ≤ acc &lt; FIX_ONE` 不变量(EC-13-04 / AC-13-E5 ②)。</summary>
        public static bool AccInvariantHolds(Fix acc)
            => acc.Raw >= 0 && acc.Raw < Fix.OneRaw;
    }
}
