// S18-AC5 前半句 · story-008 —— 9 侧的死亡候选**产出侧**(`IDeathCandidateSink` 实现)。
//
// 权威来源:
//   design/gdd/death-and-respawn.md:103 —— 归属表逐字:
//     「**死亡候选的产出** | **9** | `DeathCandidate`」
//   design/gdd/death-and-respawn.md:90  —— 「29 只消费一个布尔 `PlayerDeathWarranted(actor)`,
//                                          其唯一来源 = 9 的 F4 输出 `DeathCandidate`」
//   unity/Assets/Sim.Contracts/IDeathCandidateSource.cs:57-71 —— 接口本体(2026-10-22 落地)
//   ADR-009 §二(三问判据:候选不满足「可感知」⇒ **不进流**,进程内接口)
//   ADR-005(主机唯一 Step;本类只被主机侧 `StepAll` 调用)
//
// ── 本类存在的理由 ─────────────────────────────────────────────────────────────
// `IDeathCandidateSink` 自 2026-10-22 起**只有声明、零实现**,且 `Publish(` 全树
// (含 `Tests/`)**零命中** —— 产出路径在生产上从未存在过。本类是那个实现。
//
// ── 三条纪律(逐条对应 story-008 的 AC)────────────────────────────────────────
//   ① **单消费者**(AC-9-22):`Register` 二次调用 **抛**,不静默覆盖 —— 与
//      `EventStream` 的多订阅语义刻意不同:P0 只有 29 消费死亡候选,「谁覆盖了谁」
//      这种失败必须响,不能静默。未注册时 `Publish` **静默丢弃且不抛** ——
//      逐字承接口 `:68`「29 未装配的构建里 9 照常跑」。
//   ② **零状态**(AC-9-21):本类**不**判死、**不**查表、**不**编码、**不**写流。
//      它只持一个消费者引用。判据真源仍是 `F4OutcomeEvaluator` / `EntityDeathGates`,
//      本类不复制判序(与 `PlayerDiedWriter` / `InjuryStateChangedWriter` 同形)。
//   ③ **门 A**(AC-9-21):只依赖 `Sim.Contracts` 与 BCL。构造依赖 = 零。
//
// ── 有界性(为什么调用方必须只在「转移那一 tick」发)──────────────────────────
// `Deceased` 在 F4 九态里是**吸收态**(`Sim/StateMachine.cs:61-73`)⇒ 一旦判出,
// 此后每 tick 都是 `Deceased`。若每 tick 都 `Publish`,29 侧每 tick 都会收到一次候选。
// 本类**无法**在内部去重(它看不到状态),⇒ 去重责任在**调用方**
// (`DiseaseVitalsService.StepAll` 的「旧值 != Deceased && 新值 == Deceased」判据,
// 见该处 AC-9-25 的实现注)。本类只保证「调我一次,我转一次」。
//
// ⚠️ **诚实边界(AC-9-28,不得被读成「玩家会死」)**
//   本类**不保证生产上会被调用** —— 调用方 `DiseaseVitalsService` 的 F4 求值面只遍历
//   `DiseaseCourseBook.InOrder`,而该簿**只**由 `DiseaseOnset` 建档;玩家没有
//   `DiseaseOnset`(玩家 actorId 由 `BootRoot.cs:252` 从同一 `IIdAuthority` 取,
//   但**不**发 `DiseaseOnset`)⇒ **玩家永不进 F4** ⇒ 生产上 `Publish` 恒不因玩家触发。
//   ⇒ 本件交付的是「**产出路径存在且正确**」,不是「死亡链已可用」。
//   后者需要玩家染病路径(GDD `AC-35` 结构半边 + 玩家侧 `DiseaseOnset` 入口),
//   不在 story-008 面内。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 9 侧的死亡候选产出侧 —— 实现 <see cref="IDeathCandidateSink"/>。
    ///
    /// <para><b>零状态、零判据、零编码</b>:本类只持一个消费者引用。它不判死、不查注册表、
    /// 不编码载荷、不写流 —— 那些都归 <see cref="F4OutcomeEvaluator"/> /
    /// <see cref="EntityDeathGates"/> / 调用方。</para>
    ///
    /// <para><b>为什么「单消费者」要抛而不是覆盖</b>:P0 只有 29 消费死亡候选。
    /// 若将来出现第二个注册者,「静默覆盖」会让前一个消费者**永远收不到**候选 ——
    /// 那是玩家死了却没结算的静默失败。抛出来,让它在装配期就炸。</para>
    /// </summary>
    /// <remarks>
    /// <para><b>线程模型</b>:与 sim 的单线程 tick 模型一致(ADR-005 主机唯一 Step)⇒
    /// 本类**非线程安全**,不加锁。</para>
    /// </remarks>
    public sealed class DeathCandidateSink : IDeathCandidateSink
    {
        private IDeathCandidateSource _consumer;

        /// <summary>
        /// 已注册的消费者;<c>null</c> = 尚未装配。
        /// <para>只读暴露供装配期自检 —— 生产路径不应读它做判断(那会让「是否已注册」
        /// 变成第二条判据源)。</para>
        /// </summary>
        public IDeathCandidateSource Consumer => _consumer;

        /// <summary>
        /// 注册消费者(29 在装配期调用一次)。
        /// </summary>
        /// <param name="consumer">死亡候选的消费者。</param>
        /// <exception cref="ArgumentNullException"><paramref name="consumer"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">已有消费者注册 —— 单消费者契约,
        /// **拒绝静默覆盖**。</exception>
        public void Register(IDeathCandidateSource consumer)
        {
            if (consumer == null) throw new ArgumentNullException(nameof(consumer));

            if (_consumer != null)
            {
                throw new InvalidOperationException(
                    "死亡候选消费者已注册 —— 单消费者契约拒绝覆盖。" +
                    "P0 只有 29 消费死亡候选;第二个注册者若被静默接受,前一个将永远收不到候选" +
                    "(玩家死了却没结算的静默失败)");
            }

            _consumer = consumer;
        }

        /// <summary>
        /// 9 的 F4 判出应死时调用 —— 把候选推给已注册的消费者。
        ///
        /// <para><b>未注册 ⇒ 静默丢弃且不抛</b>(接口 <c>:68</c> 逐字):P0 允许
        /// 「29 未装配的构建里 9 照常跑」。</para>
        ///
        /// <para>⚠️ <b>调用方须自行保证「只在转移进 <c>Deceased</c> 的那一 tick 调一次」</b>
        /// —— 本类看不到状态,无法去重。见文件头的「有界性」节。</para>
        /// </summary>
        /// <param name="playerId">玩家 id(走 ADR-016 §二 与病人共用的 id 空间)。</param>
        /// <param name="tick">判定发生的逻辑 tick(20 Hz 量纲)。</param>
        public void Publish(int playerId, long tick)
        {
            _consumer?.OnDeathCandidate(playerId, tick);
        }
    }
}
