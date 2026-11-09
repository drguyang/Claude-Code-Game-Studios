// 技法解锁的**写者侧端口** —— ADR-043 §D2 / §D6(2026-11-09 · T3)
//
// 权威:docs/architecture/adr-043-technique-unlock-kind.md
//   · D2 写者 = 30;奇遇 / 手册的宿主只是**调用方**,经本端口写入
//   · D5 技法 id = 处置轴 action id ⇒ **闭集校验在写者侧**(发射器拿不到数据)
//   · D6 同一 (actor, technique) 至多一条 —— 重复授予 = **no-op,不写、不抛**
//   · ADR-005 主机唯一 Append;ADR-029 唯一编码路径 = IPayloadEncoder
//
// ⚠️ 本类是**唯一**写 `TechniqueUnlocked` 的地方(与 `AssemblySkillGrowthPort` 写 `SkillGrown`
//    同格)。任何第二处 `Append(TechniqueUnlocked)` 都算违规 —— 那会让「谁发的」失去单点。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>把「学会了一招」变成一条病史流事件。</summary>
    public sealed class TechniqueUnlockWriter
    {
        private readonly IEventSink _sink;
        private readonly IPayloadEncoder _encoder;
        private readonly ITechniqueUnlockQuery _query;
        private readonly ISet<int> _knownTechniqueIds;

        /// <param name="sink">主机事件写入通道(唯一 Append 落点)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 唯一路径)。</param>
        /// <param name="query">解锁查询 —— 用于 D6 的去重(**读自己写的流**)。</param>
        /// <param name="knownTechniqueIds">处置轴的全部 action id(闭集)。
        /// <para>⚠️ <c>null</c> = **未接线**(无数据 provider 的装配面)⇒ 跳过闭集校验,
        /// 与 `CompositionRoot` 里 `emergencyActionAxis == null` 的回退口径一致。
        /// 生产面恒非 null。</para></param>
        public TechniqueUnlockWriter(IEventSink sink, IPayloadEncoder encoder,
                                     ITechniqueUnlockQuery query,
                                     ISet<int> knownTechniqueIds)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _query = query ?? throw new ArgumentNullException(nameof(query));
            _knownTechniqueIds = knownTechniqueIds;
        }

        /// <summary>请求解锁。已解锁 ⇒ no-op 返回 <c>false</c>。</summary>
        /// <param name="actorId">学习者(玩家 id)。</param>
        /// <param name="techniqueId">技法 id(= 处置轴 action id)。</param>
        /// <param name="sourceId">来源 id(手册页 / 奇遇;不参与判定)。</param>
        /// <param name="tick">当前 tick(<see cref="ITickProvider.CurrentTick"/>)。</param>
        /// <returns><c>true</c> = 本次写出了事件;<c>false</c> = 已解锁,跳过。</returns>
        /// <exception cref="ArgumentException">技法 id 不在处置轴闭集内 —— 内容 / 接线错误,fail-loud。</exception>
        /// <exception cref="ArgumentOutOfRangeException">负 id / 负 tick(见 <see cref="TechniqueUnlockEmitter"/>)。</exception>
        public bool TryUnlock(int actorId, int techniqueId, int sourceId, long tick)
        {
            // ── 闭集门:技法 id 必须真的存在于处置轴 ──
            //    ⚠️ 不静默放行:一个拼错的 id 会写进流、**永远查不到对应动作**,
            //       而结果是「解锁了但用不了」—— 静默死数据,与 T2 的 min_skill_level 同族。
            if (_knownTechniqueIds != null && !_knownTechniqueIds.Contains(techniqueId))
                throw new ArgumentException(
                    $"techniqueId={techniqueId} 不在处置轴闭集内(disease_action_axis.json 的 action id)" +
                    " —— 写进去只会得到「解锁了但没有这个动作」的静默死行",
                    nameof(techniqueId));

            // ── D6:同一 (actor, technique) 至多一条 ⇒ 重复授予 no-op ──
            if (_query.IsUnlocked(actorId, techniqueId)) return false;

            TechniqueUnlockedPayload payload =
                TechniqueUnlockEmitter.EmitUnlock(actorId, techniqueId, sourceId);

            // Patient = None(-1):解锁是玩家级事实,与 ADR-007 五支同格(载荷无 patient 字段)。
            var e = new SimEvent(tick, PatientId.None, -1, EventKind.TechniqueUnlocked,
                                 _encoder.Encode(EventKind.TechniqueUnlocked, payload));
            _sink.Append(e);
            return true;
        }
    }
}
