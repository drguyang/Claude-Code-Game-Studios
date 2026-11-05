// 玩家实体 id 的**迟绑定槽** —— 只为一个消费者存在:F1 上下文闭包(`TD-randomevents-05`)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么需要它
// ─────────────────────────────────────────────────────────────────────────────
//   `CompositionRoot` 在装配期就要建 `RandomEventSpawnChain` 的 `contextOf` 闭包,
//   而**玩家 actorId 要到 `BootRoot` 后置生成玩家之后才存在**(`BootRoot.cs:660`
//   `RandomEventsDirector?.BindPlayer(actorId)`)。闭包捕获不到一个还没诞生的值。
//
//   同族先例(不是新机制):`NoPendingMedicalActionQuery.Bind` · `ClinicCellSource.Bind`
//   · `TeleportSink.BindPlayer` · `CasebookCommandRouter.SetAuthor` —— 都是
//   「装配期出实例,玩家就绪后一次性 `Bind`」。本类只是把「玩家 id」这一小格单独拎出来,
//   让**闭包与写者拿到同一个值**,而不是各存一份。
//
// ─────────────────────────────────────────────────────────────────────────────
// 纪律
// ─────────────────────────────────────────────────────────────────────────────
//   - **只许 Bind 一次**:二次绑定 = 两个值并存 = 导演对同一 tick 用两个玩家 id
//     (与 `IClinicCellSource` 头注的同源禁令同一病理)。
//   - **未绑定期 `ActorId = -1`**:`PatientId.None` 是世界级事件哨兵,不是玩家 ——
//     消费方须先判 `IsBound`,不得把 -1 当合法 id 去扫流。
//   - 它**不进 sim、不进流、不落盘**:纯装配期内存态(与 `RandomEventsDirectorDriver`
//     持有的 `_writer` 同格)。

using System;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 玩家实体 id 的迟绑定槽(装配期建实例,玩家生成后 <see cref="Bind"/> 一次)。
    /// </summary>
    public sealed class PlayerActorBinding
    {
        private int _actorId = -1;

        /// <summary>玩家实体 id;未绑定期为 -1(须先看 <see cref="IsBound"/>)。</summary>
        public int ActorId => _actorId;

        /// <summary>是否已绑定。</summary>
        public bool IsBound => _actorId >= 0;

        /// <summary>
        /// 绑定玩家 id(**只许一次**;玩家 id 单调铸造,换源须换新装配)。
        /// </summary>
        /// <param name="actorId">玩家实体 id(≥ 0)。</param>
        /// <exception cref="ArgumentOutOfRangeException">id 为负。</exception>
        /// <exception cref="InvalidOperationException">已绑定过。</exception>
        public void Bind(int actorId)
        {
            if (actorId < 0)
                throw new ArgumentOutOfRangeException(nameof(actorId), actorId,
                    "玩家实体 id 不得为负 —— PatientId.None(-1)是世界级事件哨兵,不是玩家");
            if (_actorId >= 0)
                throw new InvalidOperationException(
                    "玩家 actorId 重复绑定 —— 玩家 id 单调铸造,只许一次(换源须换新装配)");
            _actorId = actorId;
        }
    }
}
