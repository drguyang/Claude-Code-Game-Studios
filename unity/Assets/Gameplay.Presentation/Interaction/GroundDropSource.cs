// ADR 权威来源:
//   GDD design/gdd/interaction-system.md —— 规则二(**第一源** = 世界流 `DropSpawned.spawn_anchor`)
//   ADR-009 §五(掉落 = **身份进流 / 位置表现**;`spawn_anchor` 取整数格)
//   ADR-009 §四(候选集 = 派生态:每次重建,不进流、不存档)
//   ADR-016 §三(候选源只出整数格,禁读表现态位置)
//
// ⚠️ 本类之前 `IDropSource` **零生产实装**(`CandidateSources.Drops` 在 `CompositionRoot`
//   里传的是 `null`)⇒ 掉落**根本不在候选集里**,拾取只能靠路由的
//   「选择器一无所得 ⇒ 兜底 TryClaim」—— 那是与 F-4.1 并列的**第二处判据**。
//   `TD-interaction-02` 的消解判据 ① 即本类。
//
// ── 锚点真源 = `DeathDropReclaimPolicy.TryGetSpawnAnchor`(不在此处另写一份)──────
// 「这个实例现在在地上哪格」= 世界流里该实例**最近一次** `DropSpawned` 的 `spawn_anchor`。
// 本类**调用** `Sim` 侧那个唯一定义,不复制扫描逻辑 —— 复制即第二真源:一旦两处对
// 「最近一次出生」的解释分叉,选择器(本类)与判距(`CanClaim`)会各算各的锚,
// 表现为「看得见、按不到」或「看得见、按到别处」。
//
// ⚠️ 只出**未归属**的地面实例(`InventoryProjection.InstancesOf(NoOwner)`)—— 已拾取的
//   实例必须立刻从候选集消失(否则玩家会在空地上再按一次、拿同一个 id)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;              // DeathDropReclaimPolicy / InventoryProjection
using DaYiJingCheng.Sim.Contracts;    // SimEvent / WorldPos / IPayloadDecoder

namespace DaYiJingCheng.Gameplay.Interaction
{
    /// <summary>
    /// 第一源的生产实装:把「地面上还有哪些掉落、各自在哪格」投影成 4 的候选行。
    /// <para><b>无缓存</b>:<see cref="Rows"/> 每次现算 —— 候选集是派生态(ADR-009 §四),
    /// 拾取后地面集会变,缓存即第二真源。</para>
    /// </summary>
    public sealed class GroundDropSource : IDropSource
    {
        private readonly Func<IReadOnlyList<SimEvent>> _eventsOf;
        private readonly IPayloadDecoder _decoder;
        private readonly Func<InventoryProjection> _projectionOf;

        /// <param name="eventsOf">事件流只读视图来源(派生态每次重取,不持快照)。</param>
        /// <param name="decoder">载荷解码器(锚点面读真源)。</param>
        /// <param name="projectionOf">库存投影来源(未归属地面行 = 候选面;每次重取)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public GroundDropSource(Func<IReadOnlyList<SimEvent>> eventsOf,
                                IPayloadDecoder decoder,
                                Func<InventoryProjection> projectionOf)
        {
            _eventsOf = eventsOf ?? throw new ArgumentNullException(nameof(eventsOf));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _projectionOf = projectionOf ?? throw new ArgumentNullException(nameof(projectionOf));
        }

        /// <inheritdoc/>
        /// <remarks>顺序**不承载语义** —— 全序由 F-4.1 三键定(见 `ICandidateSource` 契约)。
        /// 流里查无出生锚的实例**跳过**(不编造坐标 —— 它在流里本就没有「在哪」这个事实)。</remarks>
        public IReadOnlyList<DropDto> Rows
        {
            get
            {
                InventoryProjection projection = _projectionOf();
                IReadOnlyList<long> ground = projection.InstancesOf(InventoryProjection.NoOwner);
                if (ground.Count == 0) return Array.Empty<DropDto>();

                IReadOnlyList<SimEvent> events = _eventsOf();   // 一次取流,逐行查锚
                var rows = new List<DropDto>(ground.Count);
                for (int i = 0; i < ground.Count; i++)
                {
                    long instanceId = ground[i];
                    if (!DeathDropReclaimPolicy.TryGetSpawnAnchor(events, _decoder, instanceId,
                                                                 out WorldPos anchor))
                        continue;   // 无出生 ⇒ 无位置(禁假数据)
                    rows.Add(new DropDto(anchor, instanceId));
                }
                return rows;
            }
        }
    }
}
