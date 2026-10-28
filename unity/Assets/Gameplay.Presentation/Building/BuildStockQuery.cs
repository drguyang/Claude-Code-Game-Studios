// S20-19 · 42 建造发起方接线 —— **库存门的生产实装**(`IStockQuery`)
//
// 权威来源:
//   design/gdd/modular-building.md 规则二 条件⑤(「数据可及 + 库存 ≥ 1」)
//   design/gdd/inventory-and-items.md 规则六(原子单位 = 一次 `StructurePlaced` 的 `cost(m)`)
//   `Sim/World/PlaceableChecker.cs:217`(`IStockQuery` 的接口定义)
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么 `HasStock(moduleId)` 的判据是「够 `cost(m)` 份」而不是「有 1 件」
// ─────────────────────────────────────────────────────────────────────────────
// 接口的形参是 **`moduleId`** 而不是 `item_key`,说明它的语义就是「这个模块**造得出来**吗」。
// 而造一个模块要 `cost(m)` 份建材(规则七 `:124`),不是 1 份。
// 若实现成「有 ≥1 件就 true」,`PlaceableChecker` 会在材料只够半手时放行,
// 把「不足」的判定推给后面的选料器 —— 两处判据语义不一致,且条件⑤的原文
// (「库存 ≥ 1」)本就写在「模块」这一层,不是在「物品」这一层。
//
// ⇒ 本实装 = **直接委托** `BuildMaterialPlanner`(与 fold、与 `BuildController` 同一算法)。
//   条件⑤ 因此不是第二份判据,而是**同一判据的第三个调用点**。
//
// ⚠️ **`cost(m) ≤ 0` ⇒ true**(不需要材料就造得出来);**模块不在目录 ⇒ false**
//   (无成本真源即不可判通过 —— 与 `DemolishChecker.ValidateRefundable` 同款 fail-closed)。
// ⚠️ 本类**不判归属以外的事**:容量 / 品级 / 过期都不在面内(规则六:消耗是减重)。

using System;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Gameplay.Presentation.Building
{
    /// <summary>
    /// `IStockQuery` 的生产实装 —— 判「该模块的材料够不够造一个」。
    /// </summary>
    /// <remarks>
    /// <para><b>投影每次现取</b>(`Func&lt;InventoryProjection&gt;`),不缓存:
    /// 投影是派生态(ADR-009 §一),建造后由 fold 扣减,缓存一份就是第二真源。</para>
    /// </remarks>
    public sealed class BuildStockQuery : IStockQuery
    {
        private readonly Func<InventoryProjection> _projection;
        private readonly IModuleCatalog _modules;
        private readonly BuildOwnerSlot _owner;

        /// <param name="projection">库存投影读数(每次调用现取)。</param>
        /// <param name="modules">模块目录(取 `cost(m)` / `build_item_key`)。</param>
        /// <param name="owner">扣料归属槽(**与 `BuildController` 同一个实例** ——
        /// 各持一份 int 会在绑定漏一处时出现「库存门放行、扣料归 -1」)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public BuildStockQuery(Func<InventoryProjection> projection,
                               IModuleCatalog modules,
                               BuildOwnerSlot owner)
        {
            _projection = projection ?? throw new ArgumentNullException(nameof(projection));
            _modules = modules ?? throw new ArgumentNullException(nameof(modules));
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <inheritdoc />
        public bool HasStock(int moduleId)
        {
            BuildMaterialPlanResult r = BuildMaterialPlanner.PlanForModule(
                moduleId, _owner.Owner, _projection(), _modules, out _);
            return r == BuildMaterialPlanResult.Success;
        }
    }
}
