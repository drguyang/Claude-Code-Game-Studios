// S18-1 AC ⑦ 另半边 · story-001:48 —— 29 侧的死亡候选注入点。
//
// 权威来源:
//   production/epics/death-respawn/story-001-player-died-settlement.md:48
//     —— 「`IDeathCandidateSource`(9 侧提供)→ 29 的 `OnDeathCandidate(playerId, tick)`;
//         29 内部只有『转发 + 发射 + 编排后续 story-002/003/004/005』,**无生命值读取路径**」
//   :49 —— 「DeathCandidate 与 PlayerDied 的去重:同一 candidate 只铸一条 PlayerDied
//          (判据 = 流前缀纯函数;冷却在 story-004,不在本层混入)」
//   story-004:47 —— 管线序 `DeathCandidate → DeathAllowed 门 → PlayerDied`
//   design/gdd/death-and-respawn.md:90 —— 29 只消费布尔 `PlayerDeathWarranted(actor)`
//
// 核心机制(三层分离,与两个写者同形):
//   - **消费面** = 本类实现 `IDeathCandidateSource` —— 9 调 `OnDeathCandidate` 进来。
//   - **判据** = `PlayerDeathWarranted`(本类的私有纯函数:该候选是否该铸 `PlayerDied`)。
//   - **写入** = 委托给 `PlayerDiedWriter` —— 本类**不直接 `Append`**,写者仍是唯一写入口。
//
// ⚠️ **`PlayerDeathWarranted` 是本类的私有纯函数,不是公开 API** ——
//   GDD `:90` 说「29 只消费一个布尔 `PlayerDeathWarranted(actor)`」,该布尔是
//   29 的**内部判定**,不是跨系统契约。跨系统契约只有 `IDeathCandidateSource`
//   与 `PlayerDied` 事件两条(见 QA plan:121「形状 = 进程内接口,不是事件」)。
//
// ⚠️ 登记项(本批不裁):
//   - **`DeathAllowed` 冷却门的求值**归 story-004(`LastDeathTick` 读世界流),
//     本类只**接收**门的值,不实现冷却 —— 承 story-001:49「冷却在 story-004,不在本层混入」。
//   - **`death_cell` 的取值**(最后一条 `ActorCellEntered` 之 cell)由装配层从流中取,
//     本类不反查流 —— 承 ADR-020 §四 与 `PlayerDiedWriter` 的同口径登记项。
//   - 生产装配(9 侧 `IDeathCandidateSink` 实现 + 本类注册)归 9 / 29 的装配轮。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 29 侧的死亡候选注入点 —— 实现 <see cref="IDeathCandidateSource"/>,把 9 的候选
    /// 转成一条 `PlayerDied`。
    /// </summary>
    /// <remarks>
    /// <para><b>「无生命值读取路径」的落实</b>:本类与 <see cref="IDeathCandidateSource"/>
    /// 的接口面上**没有任何可读生命值的成员** —— 该约束是编译期不可表达,不靠纪律守。</para>
    /// </remarks>
    public sealed class DeathCandidateConsumer : IDeathCandidateSource
    {
        private readonly PlayerDiedWriter _writer;
        private readonly Func<int, long, bool> _deathAllowed;
        private readonly Func<int, ActorClass> _actorClassOf;
        private readonly Func<int, DiseaseRegistryEntry> _entryOf;
        private readonly Func<int, WorldPos> _deathCellOf;

        /// <summary>
        /// 构造 29 侧的候选注入点。
        /// </summary>
        /// <param name="writer">`PlayerDied` 写者 —— 本类不直接 Append,写入口唯一。</param>
        /// <param name="deathAllowed">冷却门求值(story-004 的 `DeathAllowed(actor, t)`)。
        /// <para>⚠️ **形参是 <c>(actor, tick)</c> 两元,不是一元 <c>actor</c>** —— 2026-10-24
        /// story-004 实现轮就地订正(原文不删)。原签名 <c>Func&lt;int, bool&gt;</c> 把 tick
        /// 固定成「求值那一刻的某个外部时刻」,而 F-29-2 的谓词是
        /// <c>DeathAllowed(actor, t) ⟺ t − last_death_tick(actor) ≥ DEATH_COOLDOWN</c>
        /// —— **t 是判据的一半**。一元面下,实现方只能去读墙钟 / 渲染帧 / 一个缓存的
        /// 「当前 tick」,那正是 story-004 Control Manifest 的 Forbidden 项
        /// (「墙钟/渲染帧参与冷却」)与 ADR-016 §一 的第四来源。</para>
        /// <para>tick 的唯一合法来源 = 9 推来的候选自带的那一个
        /// (<see cref="IDeathCandidateSource.OnDeathCandidate"/> 的第二形参)——
        /// 判定发生的那一逻辑 tick。本类把它原样转给门,门不另取时刻。</para></param>
        /// <param name="actorClassOf">实体类别查询(判死谓词的第一入参来源)。</param>
        /// <param name="entryOf">该实体当前病种 / 伤情条目查询(判死谓词的第二入参来源)。</param>
        /// <param name="deathCellOf">死亡格查询(装配层从流中取最后一条
        /// `ActorCellEntered` 之 cell,承 ADR-020 §四)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public DeathCandidateConsumer(
            PlayerDiedWriter writer,
            Func<int, long, bool> deathAllowed,
            Func<int, ActorClass> actorClassOf,
            Func<int, DiseaseRegistryEntry> entryOf,
            Func<int, WorldPos> deathCellOf)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _deathAllowed = deathAllowed ?? throw new ArgumentNullException(nameof(deathAllowed));
            _actorClassOf = actorClassOf ?? throw new ArgumentNullException(nameof(actorClassOf));
            _entryOf = entryOf ?? throw new ArgumentNullException(nameof(entryOf));
            _deathCellOf = deathCellOf ?? throw new ArgumentNullException(nameof(deathCellOf));
        }

        /// <summary>
        /// 9 侧推来一个死亡候选 ⇒ 判定该不该铸 `PlayerDied`,该则委托写者写入。
        /// </summary>
        /// <param name="playerId">玩家 id。</param>
        /// <param name="tick">判定发生的逻辑 tick。</param>
        public void OnDeathCandidate(int playerId, long tick)
        {
            DiseaseRegistryEntry entry = _entryOf(playerId);
            ActorClass actorClass = _actorClassOf(playerId);

            if (!PlayerDeathWarranted(playerId, actorClass, entry))
                return; // 谓词否决 ⇒ 零写入(写者内还会再复核一次,双重闸)

            // 冷却门求值 —— tick 取**候选自带的那一个**(判据的另一半),
            // 不另读时钟(见 ctor 的 deathAllowed 注释)。
            _writer.TryWrite(
                tick, playerId, actorClass, entry,
                _deathCellOf(playerId), _deathAllowed(playerId, tick));
        }

        /// <summary>
        /// 该候选是否**应当**铸一条 `PlayerDied`(GDD `:90` 的布尔)。
        ///
        /// <para><b>私有纯函数</b> —— 这是 29 的内部判定,不是跨系统契约。
        /// 判据真源 = <see cref="EntityDeathGates.SelfLimited"/>,本类不复制判序。</para>
        /// </summary>
        private static bool PlayerDeathWarranted(
            int playerId, ActorClass actorClass, DiseaseRegistryEntry entry)
        {
            // 玩家 id 非法 ⇒ 不铸(fail-safe:宁可不结算,不铸错 id 的事件)。
            if (playerId < 0) return false;
            if (entry == null) return false;

            // 自限门恒开 ⇒ 死亡支关闭 ⇒ 不铸。
            return !EntityDeathGates.SelfLimited(actorClass, entry);
        }
    }
}
