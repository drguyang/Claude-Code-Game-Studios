// S20-19 · 42 建造发起方接线 —— **建造选料器**(兑现 `O-23-6` 的材料半边)
//
// 权威来源:
//   design/gdd/inventory-and-items.md 规则六(:348-372)
//     · **原子单位**「建造扣料 = 一次 `StructurePlaced` 的 `cost(m)`」;
//     · **BL-23 兜底序**:玩家未点名实例 ⇒ 同 `item_key` 多栈按 `instance_id` **升序**逐栈扣。
//   design/gdd/modular-building.md 规则七(`:124` `cost(m)` = 建造材料数)· 规则九(拆除返还)
//   ADR-005(主机唯一 Append / 整数域)· ADR-006(载荷 ⊆ 整数域)
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么需要**独立**一个选料器,而不是让每个调用点自己写一遍
// ─────────────────────────────────────────────────────────────────────────────
// 同一个选择算法在**两条路径**上都必须成立,且必须**逐份一致**:
//
//   ① **写前路径**(本批新增,`BuildController.TryPlace`):主机在 `Append` 之前
//      判「这一手材料够不够、会扣哪几栈」。不够 ⇒ 整体拒绝、世界流**零事件**。
//   ② **重建路径**(`InventoryFold.ApplyBuildCost`,2026-10-27 R16 甲案已落):
//      读档 / 投影重建时从流里的 `StructurePlaced` **反推**扣料。
//
// 两条路径若各写一份贪心,分歧 = 「主机验过 X 份、fold 扣了 Y 份」——
// 玩家的背包在读档前后**凭空多/少材料**,而且两边各自的测试都是绿的。
// ⇒ 本类是**唯一实现点**;`InventoryFold` 反过来调它(`ApplyBuildCost` 已改为委托)。
//
// ⚠️ **本类不写事件、不持状态**:它只产出 `ConsumeLine[]`(选中的实例与份数)。
//    「扣料」这件事在**建造路径上不是一条独立事件** —— 见下。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 建造路径**不**发 `DropDespawned`(这是本批最重要的一条事实)
// ─────────────────────────────────────────────────────────────────────────────
// 直觉做法是「先写 N 条 `DropDespawned` 扣料,再写 `StructurePlaced`」。**那是错的**:
// `InventoryFold` 的 `StructurePlaced` 分支**已经**按 `cost(m)` 扣料
// (`ApplyBuildCost`,`InventoryFold.cs:929`;2026-10-27 用户裁定 R16 甲案)。
// 再写一遍 `DropDespawned` ⇒ fold **双重扣减** ⇒ 玩家建一个花两份料。
//
// ⇒ 建造的**唯一**流事件 = 一条 `StructurePlaced`(规则六原文的原子单位即此)。
//   材料充足性因此是**写前判定**(本类),不是写后结算。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>选料裁决(具名,不说「差多少」—— 与 `InventoryRejection` 同款纪律)。</summary>
    public enum BuildMaterialPlanResult
    {
        /// <summary>材料足够(或该模块不需材料),计划可用。</summary>
        Success = 0,

        /// <summary>模块不在目录 ⇒ 取不到 `cost(m)` 与 `build_item_key`,扣减量无真源。</summary>
        ModuleNotFound = 1,

        /// <summary>同 `item_key` 同 owner 的可用总份数 &lt; `cost(m)` ⇒ 整手拒绝,不部分扣。</summary>
        NotEnoughMaterial = 2,
    }

    /// <summary>
    /// 一次建造的材料选取计划。
    /// <para><b>不完整时 <see cref="Lines"/> 为空</b>(fail-closed):「扣一半」在规则六里不存在。</para>
    /// </summary>
    public readonly struct BuildMaterialPlan
    {
        /// <summary>选中的 `(instance_id, qty)` 集,按 `instance_id` 升序(BL-23 兜底序)。</summary>
        public readonly ConsumeLine[] Lines;

        /// <summary>本次建造需要的总份数 = `cost(m)`。</summary>
        public readonly int RequiredQty;

        /// <summary>同 `item_key` 同 owner 的**可用总份数**(不论是否凑够 —— 缺口消息要用它)。</summary>
        public readonly int AvailableQty;

        /// <summary>是否凑够(<see cref="RequiredQty"/> ≤ <see cref="AvailableQty"/>)。
        /// `RequiredQty ≤ 0` 的模块恒 true(不扣料)。</summary>
        public bool IsComplete => RequiredQty <= AvailableQty;

        internal BuildMaterialPlan(ConsumeLine[] lines, int requiredQty, int availableQty)
        {
            Lines = lines;
            RequiredQty = requiredQty;
            AvailableQty = availableQty;
        }

        /// <summary>空计划(= 不需要材料,或未凑够且已置空)。</summary>
        public static BuildMaterialPlan Empty =>
            new BuildMaterialPlan(Array.Empty<ConsumeLine>(), 0, 0);
    }

    /// <summary>
    /// 建造选料器 —— 规则六 BL-23 兜底序的**唯一实现点**。
    /// </summary>
    /// <remarks>
    /// <para>两条读路径共用同一算法:<see cref="Plan(int,int,int,IReadOnlyDictionary{long,InventoryRow})"/>
    /// 吃 fold 的行表(重建路径);<see cref="Plan(int,int,int,InventoryProjection)"/>
    /// 吃投影(写前路径);<see cref="PlanForModule"/> 是「按模块取 cost / item_key」的便捷入口。</para>
    /// <para>⚠️ 纯函数:零副作用、零 I/O、零墙钟。**不**校验归属以外的任何业务规则
    /// (容量 / 品级 / 过期都不在面内)。</para>
    /// </remarks>
    public static class BuildMaterialPlanner
    {
        /// <summary>从**投影**选料(写前路径)。</summary>
        /// <param name="owner">扣料归属(单机 = 唯一玩家 actor id;`-1` = 无归属 ⇒ 恒不足)。</param>
        /// <param name="itemOrdinal">建材的物品表 ordinal(= `ModuleDefinition.BuildItemKey`)。</param>
        /// <param name="need">需要份数(= `ModuleDefinition.Cost`)。</param>
        /// <param name="projection">库存投影(切点 = 本次建造**之前**)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="projection"/> 为 null。</exception>
        public static BuildMaterialPlan Plan(int owner, int itemOrdinal, int need,
                                             InventoryProjection projection)
        {
            if (projection == null)
                throw new ArgumentNullException(nameof(projection),
                    "未传入库存投影 —— 拒绝以「没有材料」冒充「材料够」");

            return Plan(owner, itemOrdinal, need, projection.Rows);
        }

        /// <summary>从**行表**选料(fold 的重建路径 —— 与上一条**同一算法**)。</summary>
        /// <param name="owner">扣料归属。</param>
        /// <param name="itemOrdinal">建材 ordinal。</param>
        /// <param name="need">需要份数。</param>
        /// <param name="rows">行表(键 = `instance_id`)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="rows"/> 为 null。</exception>
        /// <remarks>
        /// ⚠️ <b>`need ≤ 0` ⇒ 空计划且 <see cref="BuildMaterialPlan.IsComplete"/> 为 true</b>:
        /// 不扣料是合法结论,不是「没算出来」。
        /// </remarks>
        public static BuildMaterialPlan Plan(int owner, int itemOrdinal, int need,
                                             IReadOnlyDictionary<long, InventoryRow> rows)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows),
                    "未传入库存行表 —— 拒绝以空表冒充「材料为空」");

            // 候选栈:同 owner、同 item_key、qty > 0。**显式排序** —— 字典枚举序不是契约。
            var ids = new List<long>();
            int available = 0;
            foreach (KeyValuePair<long, InventoryRow> kv in rows)
            {
                InventoryRow r = kv.Value;
                if (r.Owner != owner) continue;
                if (r.ItemOrdinal != itemOrdinal) continue;
                if (r.Qty <= 0) continue;
                ids.Add(kv.Key);
                available += r.Qty;
            }
            ids.Sort();                                   // BL-23:instance_id 升序

            if (need <= 0)
                return new BuildMaterialPlan(Array.Empty<ConsumeLine>(), need, available);

            // 不够 ⇒ **整手拒绝**,不返回半份计划(规则九 Edge Case:不部分扣)。
            if (available < need)
                return new BuildMaterialPlan(Array.Empty<ConsumeLine>(), need, available);

            var lines = new List<ConsumeLine>(ids.Count);
            int remaining = need;
            for (int i = 0; i < ids.Count && remaining > 0; i++)
            {
                InventoryRow r = rows[ids[i]];
                int take = r.Qty < remaining ? r.Qty : remaining;
                lines.Add(new ConsumeLine(ids[i], take));
                remaining -= take;
            }

            return new BuildMaterialPlan(lines.ToArray(), need, available);
        }

        /// <summary>按模块选料(写前路径的便捷入口):从目录取 `cost(m)` 与 `build_item_key`。</summary>
        /// <param name="moduleId">模块 id。</param>
        /// <param name="owner">扣料归属。</param>
        /// <param name="projection">库存投影。</param>
        /// <param name="modules">模块目录(取 cost / build_item_key 的**唯一**真源)。</param>
        /// <param name="plan">产出的计划(裁决非 <see cref="BuildMaterialPlanResult.Success"/> 时为空计划)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="modules"/> 或
        /// <paramref name="projection"/> 为 null。</exception>
        public static BuildMaterialPlanResult PlanForModule(
            int moduleId, int owner, InventoryProjection projection,
            IModuleCatalog modules, out BuildMaterialPlan plan)
        {
            if (modules == null)
                throw new ArgumentNullException(nameof(modules),
                    "未传入模块目录 —— 取不到 cost(m) 与 build_item_key,扣减量无真源");
            if (projection == null)
                throw new ArgumentNullException(nameof(projection),
                    "未传入库存投影 —— 拒绝以「没有材料」冒充「材料够」");

            ModuleDefinition? defOpt = modules.GetModuleDefinition(moduleId);
            if (!defOpt.HasValue)
            {
                plan = BuildMaterialPlan.Empty;
                return BuildMaterialPlanResult.ModuleNotFound;
            }

            ModuleDefinition def = defOpt.Value;
            plan = Plan(owner, def.BuildItemKey, def.Cost, projection);
            return plan.IsComplete
                ? BuildMaterialPlanResult.Success
                : BuildMaterialPlanResult.NotEnoughMaterial;
        }
    }
}
