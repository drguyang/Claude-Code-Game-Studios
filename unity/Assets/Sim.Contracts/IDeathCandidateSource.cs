// S18-1 AC ⑦ · GDD death-and-respawn.md 规则二 —— 29 的死亡候选输入源抽象点。
//
// 权威来源:
//   design/gdd/death-and-respawn.md:90   —— 「29 只消费一个**布尔** `PlayerDeathWarranted(actor)`,
//                                            其唯一来源 = 9 的 F4 输出 `DeathCandidate`」
//   production/epics/death-respawn/story-001-player-died-settlement.md:48
//                                        —— 「`IDeathCandidateSource`(9 侧提供)→ 29 的
//                                            `OnDeathCandidate(playerId, tick)`;29 内部只有
//                                            『转发 + 发射 + 编排后续』,**无生命值读取路径**」
//   story-004-death-cooldown-from-stream.md:47
//                                        —— 管线序 `DeathCandidate → DeathAllowed 门 → PlayerDied`
//   production/qa/qa-plan-sprint-18-2026-10-21.md:121 / :294
//                                        —— **形状已定形**:进程内接口,**不是流 Kind**
//                                           (`entities.yaml` 35 个 Kind 零命中)
//
// 为什么是**接口**而不是事件(2026-10-21 用户裁定 读法 A,原文不删):
//   `AC-29-13` 原文写「29 的输入 **Kind** 集 ⊆ {DeathCandidate}」,而 `DeathCandidate`
//   不在 registry 的 35 个 Kind 里 ⇒ 该措辞按字面**不可判**。裁定 = 读作「输入**源**集」,
//   判据 = 29 的构造依赖集 ⊆ {`IDeathCandidateSource`}。`PlayerDied` 是**输出**不是输入。
//
// ⚠️ 本接口**刻意只有查询 + 通知** —— 生命值读取路径**不在此面内**:
//   29 不得反查「这个玩家还剩多少血」,它只被告知「谁该死」。这条是 story-001:48
//   「29 内部…无生命值读取路径」的接口级落实 —— 接口上**没有**可读生命值的成员,
//   故该约束是**编译期不可表达**,不靠纪律守。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 死亡候选输入源(9 侧提供,29 侧消费)—— 进程内接口,**非流 Kind**。
    ///
    /// <para><b>语义</b>:9 的 F4 判出某玩家应死后,通过本接口把该候选交给 29;
    /// 29 只做「转发 + 发射 `PlayerDied` + 编排后续」,**不读生命值**。</para>
    ///
    /// <para><b>为什么不是事件流</b>:`DeathCandidate` 不进任何流 —— 它是**进程内**的
    /// 一次判定结果传递。真源仍是 9 的 F4 判定与 29 写出的 `PlayerDied`
    /// (ADR-005 唯一真源;ADR-009 §二 三问判据:候选本身不满足「可感知」)。</para>
    /// </summary>
    public interface IDeathCandidateSource
    {
        /// <summary>
        /// 9 → 29 的候选通知:玩家 <paramref name="playerId"/> 在 <paramref name="tick"/> 应死。
        /// <para>⚠️ <b>本方法不返回是否采纳</b> —— 29 的 `DeathAllowed` 门(story-004)
        /// 与去重(story-001:49)都在 29 内部裁决;9 不感知结果。</para>
        /// </summary>
        /// <param name="playerId">玩家 id(走 ADR-016 §二 与病人共用的 id 空间)。</param>
        /// <param name="tick">判定发生的逻辑 tick(20 Hz 量纲)。</param>
        void OnDeathCandidate(int playerId, long tick);
    }

    /// <summary>
    /// 死亡候选的**产出侧**(9 实现)—— 与 <see cref="IDeathCandidateSource"/> 成对。
    ///
    /// <para><b>为什么拆两个接口</b>:9 是产者、29 是消者,二者依赖方向相反。
    /// 合成一个会让 9 看见 29 的消费面(接口隔离原则),也让「9 侧提供 / 29 侧消费」
    /// 这条契约在类型上不可见。</para>
    /// </summary>
    public interface IDeathCandidateSink
    {
        /// <summary>
        /// 注册消费者(29 在装配期调用一次)。
        /// <para>⚠️ <b>单消费者</b>:P0 只有 29 消费死亡候选;多注册 ⇒ 后注册覆盖前注册,
        /// 且实现方须抛错而非静默 —— 见实现侧断言。</para>
        /// </summary>
        void Register(IDeathCandidateSource consumer);

        /// <summary>
        /// 9 的 F4 判出应死时调用 —— 把候选推给已注册的消费者。
        /// <para>未注册消费者时**静默丢弃**(P0 允许:29 未装配的构建里 9 照常跑)。</para>
        /// </summary>
        void Publish(int playerId, long tick);
    }
}
