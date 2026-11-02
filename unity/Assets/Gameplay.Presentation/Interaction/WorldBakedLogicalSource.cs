// ADR 权威来源:
//   GDD design/gdd/interaction-system.md —— 规则二(**第二源** = 6 的烘焙逻辑层)
//   ADR-015 §一(两层世界:确定性整数逻辑层,经 ADR-014 烘成 `*.cooked`)
//   ADR-022 §三(导出契约:`world_poi` / `world_resources` / `world_buildslots`)
//   ADR-009 §四(候选集 = 派生态)
//
// ⚠️ 本类之前第二源只有 `ResourceForageSpotSource`(仅资源点;POI / 槽位恒空)。
//   `TD-interaction-03` 的「第二源补齐」半边即本类:三张烘焙表 → 三个切片,
//   一次读全,不再各写各的适配。
//
// ── `SlotLinearKey` 的口径(4-DC-4)──────────────────────────────────────────
// `x + W·(y + H·z)`(**long 运算**)—— W/H 取产物头声明的范围
// (`WorldBuildSlotsDataSet.Width/Height`),与 `InteractionKindTable` 对 `4-DC-4` 的
// 校验口径同源。⚠️ 它是**第三键(StableId)**,必须可比:槽位格随世界尺寸线性化,
// 同图内唯一;若将来出现两处不同 W/H 的解释,本函数是唯一修改点。
//
// ⚠️ 本类**只装载,不判可交互** —— 槽位是否已被占用 / 是否允许该模块归 23 的
//   `BuildController`(它在结算时判)。候选集只回答「这里有可交互的东西」。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation;   // World*Row(烘焙产物行形状)
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Interaction
{
    /// <summary>
    /// 第二源的生产实装:6 的烘焙逻辑层三切片(POI 定义格 / 采集点 / 建造槽位)。
    /// <para><b>无状态</b>:三张表都是版本化烘焙数据(ADR-009 §四 的派生态,**不是**运行期可变状态)
    /// ⇒ 构造时一次性投影成 DTO 行,之后只读。</para>
    /// </summary>
    public sealed class WorldBakedLogicalSource : IWorldBakedSource
    {
        private readonly PoiCellDto[] _pois;
        private readonly ForageSpotDto[] _spots;
        private readonly BuildSlotDto[] _slots;

        /// <param name="pois">`world_poi` 行集(POI **定义**侧;状态归 6 的世界流,ADR-021)。</param>
        /// <param name="resources">`world_resources` 行集(行序即 `BakedResourceIndex`)。</param>
        /// <param name="slots">`world_buildslots` 行集。</param>
        /// <param name="worldW">槽位线性键的 W(取产物头声明范围)。</param>
        /// <param name="worldH">槽位线性键的 H(同上)。</param>
        /// <exception cref="ArgumentNullException">任一表为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">世界范围非正(线性键不可比)。</exception>
        public WorldBakedLogicalSource(IReadOnlyList<WorldPoiRow> pois,
                                       IReadOnlyList<WorldResourceRow> resources,
                                       IReadOnlyList<WorldBuildSlotRow> slots,
                                       int worldW, int worldH)
        {
            if (pois == null) throw new ArgumentNullException(nameof(pois));
            if (resources == null) throw new ArgumentNullException(nameof(resources));
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (worldW <= 0 || worldH <= 0)
                throw new ArgumentOutOfRangeException(nameof(worldW), worldW,
                    "槽位线性键需要正的 W/H(0 或负 ⇒ 键不可比,4-DC-4 的立论失效)");

            _pois = new PoiCellDto[pois.Count];
            for (int i = 0; i < pois.Count; i++)
                _pois[i] = new PoiCellDto(pois[i].Cell, pois[i].PoiId);

            _spots = new ForageSpotDto[resources.Count];
            for (int i = 0; i < resources.Count; i++)
                _spots[i] = new ForageSpotDto(resources[i].Cell, i);

            _slots = new BuildSlotDto[slots.Count];
            for (int i = 0; i < slots.Count; i++)
            {
                // 先落局部:`in` 形参要一个可寻址变量,`slots[i].Cell` 是行副本的成员(CS1612)。
                WorldPos cell = slots[i].Cell;
                _slots[i] = new BuildSlotDto(cell, LinearKey(in cell, worldW, worldH));
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<PoiCellDto> PoiCells => _pois;

        /// <inheritdoc />
        public IReadOnlyList<ForageSpotDto> ForageSpots => _spots;

        /// <inheritdoc />
        public IReadOnlyList<BuildSlotDto> BuildSlots => _slots;

        /// <summary>槽位线性键 <c>x + W·(y + H·z)</c>(4-DC-4;long 运算,防 int 回绕)。
        /// <para>⚠️ 与 `InteractionKindTableValidator` 对 `SlotLinearKey` 的 W/H/D 在场要求
        /// 是**同一口径的两端**:那里断「W/H/D 必须登记」,这里用同一个 W/H 算出键。</para>
        /// </summary>
        public static long LinearKey(in WorldPos cell, int worldW, int worldH)
            => (long)cell.X + (long)worldW * ((long)cell.Y + (long)worldH * cell.Z);
    }
}
