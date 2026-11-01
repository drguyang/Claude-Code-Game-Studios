// 采集链在**装配层**需要的两件小胶水(S22-9 · 2026-11-01)
//
// ⚠️ 本文件**不新增任何判据**,只做形状适配。两件都刻意**具名**成它们真实的样子 ——
//    「未接线的空实现」不许叫 `DefaultXxx` 或 `SimpleXxx` 那种听起来像能用的名字。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// <see cref="ISkillLevelQuery"/> 的**流折叠实现** —— 等级从病史流的 <c>SkillGrown</c> 重构。
    /// </summary>
    /// <remarks>
    /// <para><b>为什么从流重构而不是存一份快照</b>:ADR-026 ⑥ 明定等级**不独立快照**、
    /// 从流重构。存快照 = 第二真源,读档后与流不一致时无从判断谁对。</para>
    /// <para><b>判据</b>:取该 `(actor, skill)` 最后一条 <c>SkillGrown</c> 的 <c>Level</c>;
    /// <c>LevelNotGrown(-1)</c> 表示「本次未升级」⇒ 跳过,继续往前找。从未成长过 ⇒ <c>0</c>
    /// (30 的等级域下界,零级是真实等级)。</para>
    /// <para>⚠️ 本类**每次查询重扫全流**(O(n))。P0 的采集是玩家主动动作(每 tick 至多一次),
    /// 不是热路径;真要优化须另开 ADR,不得在此私自加缓存(会引入「缓存何时失效」这个新判据)。</para>
    /// </remarks>
    public sealed class HarvestSkillLevelProjection : DaYiJingCheng.Sim.Contracts.ISkillLevelQuery
    {
        private readonly Func<IReadOnlyList<DaYiJingCheng.Sim.Contracts.SimEvent>> _events;
        private readonly DaYiJingCheng.Sim.Contracts.IPayloadDecoder _decoder;

        /// <summary>构造投影。</summary>
        /// <param name="events">事件流只读视图。</param>
        /// <param name="decoder">载荷解码器(ADR-031)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public HarvestSkillLevelProjection(
            Func<IReadOnlyList<DaYiJingCheng.Sim.Contracts.SimEvent>> events,
            DaYiJingCheng.Sim.Contracts.IPayloadDecoder decoder)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        }

        /// <inheritdoc />
        public int QueryLevel(int actorId, int skillId)
        {
            IReadOnlyList<DaYiJingCheng.Sim.Contracts.SimEvent> events = _events();
            if (events == null) return 0;

            // 自后向前找最后一条「真升级」—— 流序即时刻序,后写者胜。
            for (int i = events.Count - 1; i >= 0; i--)
            {
                DaYiJingCheng.Sim.Contracts.SimEvent e = events[i];
                if (e.Kind != DaYiJingCheng.Sim.Contracts.EventKind.SkillGrown) continue;

                var p = _decoder.Decode<DaYiJingCheng.Sim.Contracts.SkillGrownPayload>(
                    e.Kind, e.Payload);
                if (p.ActorId != actorId || p.SkillId != skillId) continue;
                if (p.Level == DaYiJingCheng.Sim.Contracts.SkillGrownPayload.LevelNotGrown) continue;
                return p.Level;
            }
            return 0;
        }
    }

    /// <summary>
    /// <see cref="IDiscoveryReporter"/> 的**未接线**占位(4 的 story-004 面未落)。
    /// </summary>
    /// <remarks>
    /// <para>它**确实什么都不做**,名字与文档都这么说 —— 装配点看到 `Unwired…` 就该知道
    /// 「POI 发现链路未接」。改名叫 `NoopDiscoveryReporter` 会把这件事藏进一个中性的词里。</para>
    /// <para>⚠️ 它**不影响**采集链:采集走 <see cref="InteractableKind.ForageSpot"/>,
    /// 而 4 的选择器只在 <see cref="InteractableKind.PoiCell"/> 上触发 discovery 上报。</para>
    /// </remarks>
    public sealed class UnwiredDiscoveryReporter : IDiscoveryReporter
    {
        /// <summary>进程内唯一实例(无状态)。</summary>
        public static readonly UnwiredDiscoveryReporter Instance = new UnwiredDiscoveryReporter();

        private UnwiredDiscoveryReporter() { }

        /// <inheritdoc />
        /// <remarks>刻意空实现 —— 见类注。</remarks>
        public void Request(in DiscoveryRequest request) { }
    }

    /// <summary>
    /// 第二源(6 烘焙逻辑层)的**采集点半边**实现 —— 只有资源点,POI / 建造槽位为空。
    /// </summary>
    /// <remarks>
    /// <para><b>为什么只给采集点</b>:本类是为 17 采集链接线而生的最小适配。
    /// POI 格与建造槽位各有自己的真源(`world_poi` / `world_buildslots`),把它们也塞进来
    /// 会让「这一源到底谁提供」变得含糊 —— 需要时再各写各的适配,不在这里预支。</para>
    /// <para>⚠️ 空的那两项**不是「没有 POI / 没有槽位」**,是「本适配未提供」。
    /// 两者在候选集里不可区分 —— 这是本类的**已知边界**,故具名登记在装配点注释里。</para>
    /// </remarks>
    public sealed class ResourceForageSpotSource : IWorldBakedSource
    {
        private static readonly PoiCellDto[] NoPoi = Array.Empty<PoiCellDto>();
        private static readonly BuildSlotDto[] NoSlots = Array.Empty<BuildSlotDto>();

        private readonly ForageSpotDto[] _spots;

        /// <summary>从资源点行集构造(行序即 `BakedResourceIndex`)。</summary>
        /// <param name="resources">`world_resources` 行集。</param>
        /// <exception cref="ArgumentNullException"><paramref name="resources"/> 为 null。</exception>
        public ResourceForageSpotSource(IReadOnlyList<WorldResourceRow> resources)
        {
            if (resources == null) throw new ArgumentNullException(nameof(resources));

            _spots = new ForageSpotDto[resources.Count];
            for (int i = 0; i < resources.Count; i++)
                _spots[i] = new ForageSpotDto(resources[i].Cell, i);
        }

        /// <inheritdoc />
        public IReadOnlyList<PoiCellDto> PoiCells => NoPoi;

        /// <inheritdoc />
        public IReadOnlyList<ForageSpotDto> ForageSpots => _spots;

        /// <inheritdoc />
        public IReadOnlyList<BuildSlotDto> BuildSlots => NoSlots;
    }
}
