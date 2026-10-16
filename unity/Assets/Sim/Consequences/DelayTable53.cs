// S13 · story-003 —— F-53.2 延迟表(53_delay_table.json)的 **Sim 侧整数域视图**。
//
// 权威来源:
//   GDD design/gdd/medical-consequences.md F-53.2 `:268-294`
//     · `EmitTick(outcome) = Tick( CaseClosed ) + DelayTicks(outcome)`
//     · **定义域恰 = 回响轴三值**(Residual / Recurrence / TrialHistory;✅ 2026-10-07 已裁
//       600 / 1200 / 2400)—— 结局轴(`PatientLives` / `PatientLoses`)**不是 key**:
//       它由 9 结算、53 只读(`:285-291`「定义域收窄,不是破约束」)
//     · `DelayTicks > 0` 是**硬约束**(AC-53-06 承 AC-37-25 ①;`= 0` 会让后果与结案同 tick ⇒ 可被归因)
//   GDD `:259` / Edge Cases `:317` —— **无痕(NoEcho)不回响** ⇒ NoEcho 亦不在定义域
//   卡面 production/epics/medical-consequences/story-003-delayed-emit-and-world-stream.md
//     AC 第一条 + QA AC-2(≤0 / 缺行 / 结局轴作 key 三类构建期拒收)
//   ADR-024 §①(真源 = entities.yaml;本表值不进 registry)/ ADR-014 §二(作者态 → cooked)
//   ADR-005 / ADR-006(整数定点域;本表**零**定点量 —— delay 是 tick 计数,纯整数)
//   ADR-012 F7(int64 溢出:IL2CPP 侧有符号溢出为 UB ⇒ 相加走显式范围守卫,不靠 checked 回绕)
//
// ⚠️ 住 Sim 程序集 `DaYiJingCheng.Sim.Consequences`(门 A / b2 / b6 / MedConsFloatGate 全适用)。
//
// ⚠️ **定义域用白名单单机制**(用户裁定 2026-10-16):`{ Residual, Recurrence, TrialHistory }`
//   三支之外一律 schema 错 —— `no_echo` / `patient_lives` / `patient_loses` 走**同一条规则**,
//   不设各自的分支。白名单**从 `Outcome53.Outcome` 枚举 ordinal 派生**(零字符串副本、
//   与判定表键域零漂移)。
//
// ⚠️ **构造器拒收 = Sim 侧纵深防御**(与 `Outcome53.cs:237-244` 的 T-2 同口径):
//   构建期主校验点在 Binder(`DelayTableBinder`,Editor.Tools.Bake);此处防「绕过 Bake 直构表」。
//   两条拒收:① key ∉ 白名单(schema 错);② 白名单内 `delay_ticks ≤ 0`(AC-53-06)。
//   第三条:白名单全覆盖(缺行 ⇒ 拒收)—— 缺行会让「该回响不发」静默成立。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Sim.Consequences
{
    /// <summary>
    /// F-53.2 延迟表的运行期整数域视图(outcome ordinal → delay ticks)+ EmitTick 纯整数计算。
    /// </summary>
    public static class DelayTable53
    {
        /// <summary>定义域白名单(恰 3 支,升序)—— 从 <see cref="Outcome53.Outcome"/> ordinal 派生。
        /// <para>⚠️ 增员 = GDD 修订轮(F-53.1 枚举闭集 + F-53.2 定义域同时改),不得实现期扩。</para>
        /// <para>⚠️ **只读视图经 <see cref="Array.AsReadOnly{T}"/> 包裹**(W-4):裸 <c>int[]</c> 的
        /// <c>readonly</c> 只锁引用、元素仍可写,且调用方可将 <c>IReadOnlyList</c> cast 回 <c>int[]</c>
        /// 改写 ⇒ 白名单运行期可被拆掉(F-53.2 定义域收窄 + AC-53-07 的机制面护栏)。</para></summary>
        public static readonly IReadOnlyList<int> DomainOutcomes =
            Array.AsReadOnly(new[]
            {
                (int)Outcome53.Outcome.Residual,
                (int)Outcome53.Outcome.Recurrence,
                (int)Outcome53.Outcome.TrialHistory,
            });

        /// <summary>定义域判定(白名单单机制;白名单外 = schema 错 / 不发)。</summary>
        public static bool IsInDomain(int outcomeOrdinal)
        {
            var domain = DomainOutcomes;
            for (int i = 0; i < domain.Count; i++)
                if (domain[i] == outcomeOrdinal) return true;
            return false;
        }

        /// <summary>
        /// <see cref="EmitTick"/> = 结案 tick + 延迟 tick(**纯整数**,AC 第一条)。
        /// </summary>
        /// <remarks>
        /// <para>⚠️ **零回绕**(ADR-012 F7):C# 的 <c>int64</c> 溢出是定义性回绕,而 IL2CPP
        /// 生成的 C++ 有符号溢出是 UB ⇒ 两平台可能给**不同**结果。本方法用纯范围比较先挡,
        /// 越界 fail-loud(非静默回绕成早于结案的 tick —— 那会让后果「在结案之前」显形)。</para>
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="delayTicks"/> &lt;= 0
        /// (AC-53-06 的调用点复述:≤0 ⇒ 后果与结案同 tick,可被归因)。</exception>
        /// <exception cref="OverflowException">相加越出 <see cref="long"/> 上界。</exception>
        /// <remarks>⚠️ 只守**上界**:<paramref name="caseClosedTick"/> 为负时本方法照算 ——
        /// 结案 tick 的下界由流不变量兜(结案事件必先入流,`EventStream` 的 BCD-码-3 保证非降),
        /// 门 A 内不自守第二遍。</remarks>
        public static long EmitTick(long caseClosedTick, long delayTicks)
        {
            if (delayTicks <= 0)
                throw new ArgumentOutOfRangeException(nameof(delayTicks), delayTicks,
                    "AC-53-06:DelayTicks 须 > 0(= 0 会让后果与结案同 tick ⇒ 可被归因)");
            if (caseClosedTick > long.MaxValue - delayTicks)
                throw new OverflowException(
                    $"EmitTick 越界:caseClosedTick={caseClosedTick} + delayTicks={delayTicks}");
            return caseClosedTick + delayTicks;
        }

        /// <summary>延迟表(烘焙产物的运行期形状;测试 / Bake 侧直构,生产装载归装配轮)。</summary>
        public sealed class Table
        {
            private readonly Dictionary<int, long> _delayTicksByOutcome;

            /// <summary>构造延迟表并做三条拒收(见文件头)。</summary>
            /// <param name="delayTicksByOutcome">outcome ordinal → delay ticks。**构造期拷贝**
            /// (见实现:`rows` 为新字典;构造后改原字典**不影响**本表)。
            /// <para>⚠️ 与 <see cref="Outcome53.Table"/> **不同口径** —— 后者是 <c>_rows = rows</c>
            /// 直存不拷贝。本件取拷贝语义,以杜绝「外部字典在表构造后被改写 ⇒ 校验过的值被绕过」。</para></param>
            /// <exception cref="ArgumentException">① key ∉ 定义域白名单(schema 错,含结局轴 /
            /// NoEcho 作 key)② 白名单内值 ≤ 0(AC-53-06)③ 白名单缺行(全覆盖校验)。</exception>
            public Table(IReadOnlyDictionary<int, long> delayTicksByOutcome)
            {
                var rows = new Dictionary<int, long>();
                if (delayTicksByOutcome != null)
                {
                    foreach (var kv in delayTicksByOutcome)
                    {
                        // 拒收 ①:白名单单机制 —— 域外 key 一律 schema 错(不另设结局轴/NoEcho 分支)。
                        if (!IsInDomain(kv.Key))
                            throw new ArgumentException(
                                $"延迟表 key「{OutcomeNameOf(kv.Key)}」(ordinal={kv.Key})∉ 定义域" +
                                "(F-53.2 `:285-291`:定义域恰 = 回响轴三值;结局轴由 9 结算、" +
                                "53 只读,NoEcho 无痕不回响 ⇒ 收进本表即 schema 错)");

                        // 拒收 ②:AC-53-06 定义域内每条 > 0。
                        if (kv.Value <= 0)
                            throw new ArgumentException(
                                $"延迟表 outcome「{OutcomeNameOf(kv.Key)}」值 {kv.Value} ≤ 0 —— " +
                                "AC-53-06 / F-53.2 `:293` 硬约束(= 0 会让后果与结案同 tick,可被归因)");

                        rows[kv.Key] = kv.Value;
                    }
                }

                // 拒收 ③:全覆盖(缺行 = 该回响静默不发,不可观察)。
                for (int i = 0; i < DomainOutcomes.Count; i++)
                {
                    int outcome = DomainOutcomes[i];
                    if (!rows.ContainsKey(outcome))
                        throw new ArgumentException(
                            $"延迟表缺 outcome 行「{OutcomeNameOf(outcome)}」(ordinal={outcome})—— " +
                            "定义域内三支须全在(缺行会让该回响静默不发;构建期同一校验在 Binder)");
                }

                _delayTicksByOutcome = rows;
            }

            /// <summary>表行数(= 定义域大小,构造后恒 3)。</summary>
            public int RowCount => _delayTicksByOutcome.Count;

            /// <summary>查延迟量。</summary>
            /// <param name="outcomeOrdinal">Outcome 枚举序数(<see cref="Outcome53.Outcome"/>)。</param>
            /// <param name="delayTicks">命中时 = 延迟 tick 数(&gt; 0)。</param>
            /// <returns><c>false</c> = 域外(NoEcho 无痕 / 结局轴)= **合法不发**,非错误、不抛。</returns>
            public bool TryDelayTicks(int outcomeOrdinal, out long delayTicks)
                => _delayTicksByOutcome.TryGetValue(outcomeOrdinal, out delayTicks);
        }

        /// <summary>表物化入口(Bake 侧 / 测试装配;与 <see cref="Outcome53.CreateTable"/> 同形)。</summary>
        public static Table CreateTable(IReadOnlyDictionary<int, long> delayTicksByOutcome)
            => new Table(delayTicksByOutcome);

        /// <summary>错误信息用枚举名(未知 ordinal 回落数字,零字符串副本判据)。</summary>
        private static string OutcomeNameOf(int outcomeOrdinal)
            => Enum.IsDefined(typeof(Outcome53.Outcome), outcomeOrdinal)
                ? ((Outcome53.Outcome)outcomeOrdinal).ToString()
                : $"UNKNOWN({outcomeOrdinal})";
    }
}
