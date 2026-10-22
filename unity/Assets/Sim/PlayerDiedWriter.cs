// S18-1 AC ⑧ 半边 · GDD death-and-respawn.md 规则二 —— 9 的 `PlayerDied` 生产写入通道。
//
// 权威来源:
//   design/registry/entities.yaml:2057 —— SimEvent.Kind.PlayerDied:
//     stream: world · author: "9 疾病与伤情模拟(9 拥有 F4 致死判据;29 是消费者,Append 调用点 = 0)"
//     payload_schema: "actor_id: i32; death_cell: 格 WorldPos; tick: i64。
//                      无连续坐标 无死因文本 无伤害数值;无条件发出(背包为空也发);
//                      冷却期内不发;death_cell = 最后一条 ActorCellEntered 之 cell 原样携带"
//   design/gdd/death-and-respawn.md:90 —— 29 只消费布尔 `PlayerDeathWarranted`;
//     唯一来源 = 9 的 F4 输出 `DeathCandidate`
//   story-004-death-cooldown-from-stream.md:47 —— 管线序
//     `DeathCandidate → DeathAllowed 门 → PlayerDied`
//   ADR-005(主机唯一 Append;Seq 由发号器给出 ⇒ header Seq = -1 哨兵)
//   ADR-009 §二(世界流)· §六(有界性)
//   ADR-020 §四(玩家位移 = 表现态;`death_cell` 禁读实时物理位置)
//   ADR-029 §③(载荷唯一编码路径 = IPayloadEncoder;本类内禁 `new PayloadRef(`)
//
// 核心机制(三层分离,与 `CaseCloseWriter` 同形):
//   - **判据** = `EntityDeathGates`(纯函数:该不该死)—— 本类不复制判序。
//   - **门** = `DeathAllowed`(story-004 的冷却门,由调用方给出布尔)—— 本类不实现冷却。
//   - **写入** = 本类:前置复核 + 载荷装配 + 一条 `Append`。
//
// ⚠️ **本写者只写 `PlayerDied`,不写 `DeathCandidate`** —— 后者不是流 Kind
//   (见 `IDeathCandidateSource` 头注),它是进程内传递,不产生任何 `Append`。
//
// ⚠️ 登记项(本批不裁):
//   - **生产触发点**(9 的 F4 判出致死的那一 tick 调用本写者)尚未接线:归 9 的 F4 装配轮。
//     本类落地 = 「有写者」(`WriterExistenceGate` 判据① 可撤豁免表),不等于「已接线」。
//   - `IEventAuthority.IsHost` 门不在本件:与既有写者同格 —— 主机唯一由「谁调用写者」保证
//     (ADR-005),客户端写通道的统一门禁归 45 轮。
//   - `death_cell` 由**调用方**给出:它是「最后一条 `ActorCellEntered` 之 cell 原样携带」
//     (registry 原文),而读流是调用方(9 的 F4)的职责 —— 本类不反查流,
//     与 `CaseCloseWriter` 的 `disease_set` 由调用方给出同口径。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary><see cref="PlayerDiedWriter.TryWrite"/> 的结果码(零写入分支亦具名,不静默)。</summary>
    public enum PlayerDiedWriteResult
    {
        /// <summary>一条 <see cref="EventKind.PlayerDied"/> 入世界流(本次真正写入)。</summary>
        Written = 0,

        /// <summary>拒绝 —— 冷却门未开(<c>DeathAllowed == false</c>,story-004 / F-29-2);
        /// 零写入。冷却逻辑本身不在本类,本类只尊重调用方给出的门。</summary>
        RejectCooldown = 1,

        /// <summary>拒绝 —— 判死谓词否决(`SelfLimited` 为 true ⇒ 自限门恒开,死亡支关闭);
        /// 零写入。<b>本码的存在即证明本写者没有绕过谓词</b>。</summary>
        RejectPredicate = 2,

        /// <summary>拒绝 —— 入参非法(actorId 为负 / tick 为负);零写入。</summary>
        RejectInvalidArgument = 3,
    }

    /// <summary>
    /// `PlayerDied` 写者 —— 9 的世界流生产写入通道(主机侧)。
    /// </summary>
    /// <remarks>
    /// <para><b>为什么本类要复核谓词而不是信任调用方</b>:与 <see cref="CaseCloseWriter"/>
    /// 「前置条件在写者内按 GDD 复核,不信任调用方转述」同一条纪律。若调用方漏判
    /// `SelfLimited`,此处是最后一道闸 —— 且它返回具名结果码,调用方可区分。</para>
    /// <para><b>无条件发出(背包为空也发)</b>:registry 原文明确 —— 否则空背包死亡在三流
    /// 零痕迹,F-29-2 冷却与 F-29-4 掉级重建同时静默失败。本类不查背包。</para>
    /// </remarks>
    public sealed class PlayerDiedWriter
    {
        private readonly IEventSink _eventSink;
        private readonly IPayloadEncoder _encoder;

        /// <summary>
        /// 构造 `PlayerDied` 写者。
        /// </summary>
        /// <param name="eventSink">事件写入通道(主机唯一;生产 = 真 <c>EventStream</c>)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 §③ —— 唯一合法编码路径,
        /// 本类内不得出现 <c>new PayloadRef(</c>,由 b6 门强制)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public PlayerDiedWriter(IEventSink eventSink, IPayloadEncoder encoder)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
        }

        /// <summary>
        /// 尝试写出一个玩家的死亡事件。
        /// </summary>
        /// <param name="tick">死亡判定的逻辑 tick(20 Hz 量纲)。</param>
        /// <param name="actorId">玩家 id(走 ADR-016 §二 与病人共用的 id 空间)。</param>
        /// <param name="actorClass">实体类别 —— 判死谓词的第一入参(`IsCombatant` 真值表)。</param>
        /// <param name="entry">该玩家当前所受病种 / 伤情的注册表条目(判死谓词的第二入参)。</param>
        /// <param name="deathCell">死亡所在整数格 —— registry 原文要求「最后一条
        /// <c>ActorCellEntered</c> 之 cell 原样携带」,由调用方从流中取值
        /// (ADR-020 §四:禁读实时物理位置)。</param>
        /// <param name="deathAllowed">冷却门(story-004 的 `DeathAllowed` 求值结果);
        /// 冷却逻辑不在本类,本类只尊重它。</param>
        /// <returns>具名结果码。</returns>
        public PlayerDiedWriteResult TryWrite(
            long tick, int actorId, ActorClass actorClass,
            DiseaseRegistryEntry entry, WorldPos deathCell, bool deathAllowed)
        {
            if (tick < 0) return PlayerDiedWriteResult.RejectInvalidArgument;
            if (actorId < 0) return PlayerDiedWriteResult.RejectInvalidArgument;
            if (entry == null) return PlayerDiedWriteResult.RejectInvalidArgument;

            // ── 判死谓词(最后一道闸)──
            // `SelfLimited == true` ⇒ 自限门恒开、死亡支关闭 ⇒ 不得写。
            // 判据真源 = `EntityDeathGates`,本类不复制判序。
            if (EntityDeathGates.SelfLimited(actorClass, entry))
                return PlayerDiedWriteResult.RejectPredicate;

            // ── 冷却门(story-004;门在任何写流之前,见 story-004:47)──
            if (!deathAllowed)
                return PlayerDiedWriteResult.RejectCooldown;

            // ── 载荷:registry payload_schema 三字段(全整数,无连续坐标 / 无死因文本 / 无伤害数值)──
            var payload = new PlayerDiedPayload(actorId, deathCell, tick);
            var payloadRef = _encoder.Encode(EventKind.PlayerDied, payload);

            // ── 事件头:Patient = None(受伤实体侧事件,ADR-007 §四 —— 不污染高水位);
            //            Seq = -1 未发号哨兵(O-1)──
            _eventSink.Append(new SimEvent(
                tick, PatientId.None, -1, EventKind.PlayerDied, payloadRef));

            return PlayerDiedWriteResult.Written;
        }
    }
}
