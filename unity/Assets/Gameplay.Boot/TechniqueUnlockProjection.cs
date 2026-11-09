// 技法解锁的**流折叠**投影 —— ADR-043 §D7(2026-11-09 · T3)
//
// 权威:docs/architecture/adr-043-technique-unlock-kind.md §D7
//   · `ITechniqueUnlockQuery` 住 `Sim.Contracts`;实装住本层(装配 / 边界层)
//   · 同源可重建:解锁集合 = 病史流里该 (actor, technique) 是否出现过(ADR-016 §一 三源不变量)
//
// ⚠️ 与 `HarvestSkillLevelProjection` **同口径**:每次查询自后向前扫全流,O(n)。
//    P0 的调用面是「病人出现时的池过滤 / 动作可用性」,不是逐帧热路径。
//    在本类内部加缓存会引入「缓存何时失效」这个新判据 —— 真要优化须另开 ADR,
//    不得在此私自加(同 `HarvestSkillLevelProjection` 的立场)。
//
// ⚠️ 不做去重、不做排序:事件是**插入即事实**,同一条 (actor, technique) 正常至多一条
//    (写入侧 D6 保证 no-op);即便出现重复也只影响性能,不影响返回值。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>病史流 → 「此人会哪几招」的折叠投影。</summary>
    public sealed class TechniqueUnlockProjection : ITechniqueUnlockQuery
    {
        private readonly Func<IReadOnlyList<SimEvent>> _events;
        private readonly IPayloadDecoder _decoder;

        /// <param name="events">事件流只读视图(每次查询时取——装配早于流填充)。</param>
        /// <param name="decoder">载荷解码器(ADR-031)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public TechniqueUnlockProjection(Func<IReadOnlyList<SimEvent>> events,
                                         IPayloadDecoder decoder)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        }

        /// <inheritdoc />
        public bool IsUnlocked(int actorId, int techniqueId)
        {
            IReadOnlyList<SimEvent> events = _events();
            if (events == null) return false;

            for (int i = events.Count - 1; i >= 0; i--)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.TechniqueUnlocked) continue;

                var p = _decoder.Decode<TechniqueUnlockedPayload>(e.Kind, e.Payload);
                if (p.ActorId == actorId && p.TechniqueId == techniqueId) return true;
            }
            return false;
        }
    }
}
