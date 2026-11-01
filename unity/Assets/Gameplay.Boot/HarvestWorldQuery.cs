// `IHarvestWorldQuery` 的**生产实现** —— 17 采集驱动要的五问,逐问接真源(S22-9 · 2026-11-01)
//
// ── 每一问的真源(不许有第二个)────────────────────────────────────────────
//   ① 资源点(`nodeId` / `cell` / `itemKey`)  → `world_resources.cooked.bytes`(ADR-014 烘焙产物)
//   ② `qty_per_node`                          → 21a `item_database_items` 的 `gather_profile`
//   ③ `quality_distribution`                  → 同上(S22-9 ③ 才补上的字段)
//   ④ 单件重量                                 → 同上 `ItemDef.Weight`
//   ⑤ 当前载重                                 → 20 的 `InventoryProjection`(经 `CarryLoadSolver.Sum`)
//
// ⚠️ **本类零判据、零状态**:它只做「查表 + 转发」,任何「够不够 / 满不满」都归
//    `NodeRemaining.CanHarvest` / `CarryLoadSolver.CanCarry`。在这里写一份判据 = 第二真源。
//
// ⚠️ **不得读表现态位置**:资源点格来自烘焙逻辑层(ADR-015 §一),玩家格不进本类 ——
//    本类是**世界**查询面,玩家侧数据由 `ActorCellEntered` 经流确立后另行传入。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// <see cref="IHarvestWorldQuery"/> 的生产实现:烘焙逻辑层 + 21a 物品表 + 20 库存投影。
    /// </summary>
    /// <remarks>
    /// <para><b>为什么投影是 <c>Func</c> 而不是值</b>:库存投影是**派生态**,随事件流每次
    /// `Rebuild` 重算(`InventoryLoadService` 的唯一重建入口)。持一份快照 = 载重永远停在
    /// 装配那一刻,满载闸会对早已领走的东西判「还装得下」。</para>
    /// </remarks>
    public sealed class HarvestWorldQuery : IHarvestWorldQuery
    {
        private readonly IReadOnlyList<WorldResourceRow> _resources;
        private readonly Dictionary<int, ItemDef> _itemsByOrdinal;
        private readonly Func<InventoryProjection> _projection;
        private readonly Func<int, int> _weightOfOrdinal;
        private readonly int _noOwner;

        /// <summary>构造查询面。</summary>
        /// <param name="resources">资源点行集(`world_resources`,行序 = `BakedResourceIndex`)。</param>
        /// <param name="items">21a 物品表(品种 → `qty_per_node` / 分布 / 重量)。</param>
        /// <param name="projection">**取当前投影**的回调(每次调用重取,不缓存)。</param>
        /// <param name="ownerOfActor">玩家 id → 投影里的 owner(`InventoryProjection.NoOwner` 表示地上)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public HarvestWorldQuery(IReadOnlyList<WorldResourceRow> resources,
                                 ItemDataSet items,
                                 Func<InventoryProjection> projection,
                                 Func<int, int> actorOfOwner)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _projection = projection ?? throw new ArgumentNullException(nameof(projection));

            _itemsByOrdinal = new Dictionary<int, ItemDef>(items.Items?.Length ?? 0);
            if (items.Items != null)
                foreach (ItemDef def in items.Items)
                    _itemsByOrdinal[def.Ordinal] = def;

            // 重量查询面:与 `CarryLoadSolver.Sum` 的形参同形(ordinal → int)。
            // ⚠️ 缺行 ⇒ **抛**,不返回 0 —— 0 会让满载闸把「不知道多重」当成「没有重量」。
            _weightOfOrdinal = ordinal =>
            {
                if (!_itemsByOrdinal.TryGetValue(ordinal, out ItemDef def))
                    throw new InvalidOperationException(
                        $"[采集] item ordinal={ordinal} 在 21a 物品表中不存在 —— 载重无从求和");
                return def.Weight;
            };

            _ownerOfActor = actorOfOwner ?? throw new ArgumentNullException(nameof(actorOfOwner));
            _noOwner = InventoryProjection.NoOwner;
        }

        private readonly Func<int, int> _ownerOfActor;

        /// <inheritdoc />
        public bool TryGetResourceNode(long bakedResourceIndex, out int nodeId,
                                       out WorldPos cell, out int itemKey)
        {
            nodeId = -1; cell = default; itemKey = -1;

            // 4 的 `ForageSpot` 稳定 id = 资源点**行序**(`StableIdSource.BakedResourceIndex`)。
            if (bakedResourceIndex < 0 || bakedResourceIndex >= _resources.Count) return false;

            WorldResourceRow row = _resources[(int)bakedResourceIndex];
            nodeId = row.ResourceId;
            cell = row.Cell;
            itemKey = row.ItemKey;
            return true;
        }

        /// <inheritdoc />
        public int QtyPerNodeOf(int itemKey) => GatherOf(itemKey).QtyPerNode;

        /// <inheritdoc />
        public IReadOnlyList<int> QualityDistributionOf(int itemKey)
            => GatherOf(itemKey).QualityDistribution;

        /// <inheritdoc />
        public int UnitWeightOf(int itemKey) => _weightOfOrdinal(itemKey);

        /// <inheritdoc />
        public long CurrentLoadOf(int actorId)
        {
            int owner = _ownerOfActor(actorId);
            // ⚠️ `NoOwner` = 「在地上」—— 玩家不该是它。给 `Sum` 传一个哨兵会让载重恒 0
            //    (满载闸永不放行不了 ⇒ 静默放行)。fail-loud 而不是静默。
            if (owner == _noOwner)
                throw new InvalidOperationException(
                    $"[采集] actor={actorId} 未绑定到库存 owner —— 载重无从归属;" +
                    "静默按 0 计会让满载闸永远放行(CARRY_CAP 形同不存在)");

            return CarryLoadSolver.Sum(_projection(), _weightOfOrdinal, owner);
        }

        private GatherProfile GatherOf(int itemKey)
        {
            if (!_itemsByOrdinal.TryGetValue(itemKey, out ItemDef def))
                throw new InvalidOperationException(
                    $"[采集] item ordinal={itemKey} 在 21a 物品表中不存在 —— 采集数据无从取路");
            if (def.GatherProfile == null)
                throw new InvalidOperationException(
                    $"[采集] item ordinal={itemKey}(base_id={def.BaseId})非 material —— " +
                    "没有 gather_profile,却出现在资源点上 ⇒ 关卡工具导出与 21a 分家");
            return def.GatherProfile.Value;
        }
    }
}
