// S20-19 · 42 建造发起方接线 —— **建造编排器**(兑现 `O-23-6` 的接线半边)
//
// 权威来源:
//   design/gdd/modular-building.md
//     · 规则二(放置合法性五条件 —— 判定器 = `PlaceableChecker`,本类**不复判**)
//     · 规则七(:124 `cost(m)` = 建造材料数)· 规则九(拆除与回收,F-23-3 返还)
//     · EC-23-08(`refundable = false` 的消耗性家具无返还 —— 由 23 的 `Remove` 自理)
//   design/gdd/inventory-and-items.md 规则六(建造扣料 = 一次 `StructurePlaced` 的 `cost(m)`)
//   design/gdd/skeuomorphic-ui.md §建造交互(本批新增节 —— 建造 = **世界内操作态**)
//   ADR-009 §七(意图 → 主机当下判定 → 效果进流)
//   ADR-011 §一(action-based;输入读取在 Boot 侧的 `BuildInputReader`/`BuildInputDriver`)
//   ADR-013 §十(`ModalId` 闭集 7 员 —— **建造不占模态**,本类零 `IModalState` 依赖)
//   ADR-020 §四(玩家位移纯表现态;本类只读**已量化**的整数格)
//   ADR-025 §①(程序集引用集 —— 本类住 Presentation,不碰 `Sim` 内部写面)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本类**只做编排**,不重写任何判定
// ─────────────────────────────────────────────────────────────────────────────
//   合法性   → `PlaceableChecker.Check`      (五条件,含库存门)
//   可改性   → `ModifiableChecker.Check`     (新格 ∖ 旧格 全空)
//   可拆性   → `DemolishChecker.Check`       (存在 / 非 Shell / 无实体)
//   材料     → `BuildMaterialPlanner`        (规则六 BL-23 兜底序,**与 fold 同源**)
//   落流     → `StructureWriter`             (主机唯一 `Append`)
// 本类**零**判据实现 ⇒ 任何「同一条件两处判定」的分叉都不可能在本文件发生。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 建造**不**写 `DropDespawned`(材料扣减是 `StructurePlaced` 的派生)
// ─────────────────────────────────────────────────────────────────────────────
// `InventoryFold.ApplyBuildCost` 已在重建期按 `cost(m)` 扣料(2026-10-27 R16 甲案)。
// 若本类再写 N 条 `DropDespawned`,fold 会**双重扣减**。故:
//   · 材料充足性 = **写前判定**(`BuildMaterialPlanner`,纯函数);
//   · 落流事件 = **恰一条** `StructurePlaced`;
//   · 扣减在投影上的**可见变化**由调用方 `Rebuild()` 投影后从 fold 读出。
// 详见 `BuildMaterialPlanner.cs` 的同类说明。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 失败路径**零副作用**(AC B-2 / B-6)
// ─────────────────────────────────────────────────────────────────────────────
// 每条 `Try*` 的顺序都是「先全判、后落流」:任一判定不过 ⇒ **直接返回,不调写者**。
// 「先调写者再判」在结果状态上可能看不出差别,只有**调用次数 / 事件条数**能抓到 ——
// 夹具据此断言(见 `build_controller_test.cs`)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Gameplay.Presentation.Building
{
    /// <summary>
    /// 建造编排器 —— 把「玩家在某个整数格上按下放置/拆除/旋转」翻译成判定 + 落流。
    /// </summary>
    /// <remarks>
    /// <para><b>无状态</b>:建造模式的开关、选中模块、当前朝向都住在驱动侧
    /// (<c>BuildInputDriver</c>)—— 它们是**表现态**,不是世界状态(ADR-009 §四)。</para>
    /// <para><b>零引擎依赖</b>:本类只用 `Sim` 的整数域类型 ⇒ EditMode 可直调,
    /// 不需要场景 / 玩家对象。</para>
    /// </remarks>
    public sealed class BuildController
    {
        private readonly PlaceableChecker _placeable;
        private readonly DemolishChecker _demolish;
        private readonly ModifiableChecker _modifiable;
        private readonly StructureWriter _writer;
        private readonly StructureInstanceRegistry _registry;
        private readonly IModuleCatalog _modules;
        private readonly Func<InventoryProjection> _projection;
        private readonly Func<long> _tick;
        private readonly BuildOwnerSlot _owner;

        /// <param name="placeable">放置判定器(五条件,含库存门)。</param>
        /// <param name="demolish">拆除判定器。</param>
        /// <param name="modifiable">改朝向判定器。</param>
        /// <param name="writer">23 的结构写者(主机唯一 `Append`)。</param>
        /// <param name="registry">结构实例表(本类只读 —— 取当前朝向 / 按格找实例)。</param>
        /// <param name="modules">模块目录(取 `cost(m)` / `build_item_key`)。</param>
        /// <param name="projection">库存投影读数(每次调用**现取**,不缓存 —— 投影是派生态)。</param>
        /// <param name="tick">当前 tick(ADR-005 唯一时钟;禁读墙钟)。</param>
        /// <param name="owner">扣料归属槽(晚绑定 —— 玩家 actorId 在装配之后才铸造;
        /// **与 `BuildStockQuery` 同一个实例**)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null(fail-closed:建造链条缺一环
        /// 就必须在装配期炸,而不是在玩家按下放置时静默无效)。</exception>
        public BuildController(
            PlaceableChecker placeable,
            DemolishChecker demolish,
            ModifiableChecker modifiable,
            StructureWriter writer,
            StructureInstanceRegistry registry,
            IModuleCatalog modules,
            Func<InventoryProjection> projection,
            Func<long> tick,
            BuildOwnerSlot owner)
        {
            _placeable = placeable ?? throw new ArgumentNullException(nameof(placeable));
            _demolish = demolish ?? throw new ArgumentNullException(nameof(demolish));
            _modifiable = modifiable ?? throw new ArgumentNullException(nameof(modifiable));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _modules = modules ?? throw new ArgumentNullException(nameof(modules));
            _projection = projection ?? throw new ArgumentNullException(
                nameof(projection), "未注入库存投影读数 —— 材料充足性无从判定");
            _tick = tick ?? throw new ArgumentNullException(nameof(tick));
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>扣料归属槽(未绑定时 <see cref="BuildOwnerSlot.Unbound"/>,建造恒拒)。</summary>
        public BuildOwnerSlot OwnerSlot => _owner;

        /// <summary>当前扣料归属(P0 单机 = 唯一玩家;未绑定 = -1)。</summary>
        public int Owner => _owner.Owner;

        /// <summary>
        /// 绑定扣料归属(玩家 actorId 铸造之后调)。
        /// <para>⚠️ 转发给 <see cref="OwnerSlot"/> —— 本类**不持第二份 owner**,
        /// 否则 `BuildStockQuery` 与本类会在绑定漏一处时分叉。</para>
        /// </summary>
        public void BindOwner(int actorId) => _owner.Bind(actorId);

        /// <summary>
        /// 尝试放置一个模块。
        /// <para><b>顺序 = 判定 → 选料 → 落流</b>;任一不过 ⇒ 直接返回,写者**零调用**。</para>
        /// </summary>
        /// <param name="moduleId">模块 id(须在目录内)。</param>
        /// <param name="anchor">锚点整数格(**已量化** —— 调用方经
        /// <c>CellTransitionDetector.CellFromPosition</c> 得到,本类不做 float→格转换)。</param>
        /// <param name="orientation">朝向(0 / 90 / 180 / 270;其余由判定器抛)。</param>
        /// <param name="variant">变体档(不参与足迹)。</param>
        /// <returns>判定结果;<see cref="PlaceableResult.Success"/> 表示已落流。</returns>
        public PlaceableResult TryPlace(int moduleId, WorldPos anchor, int orientation, int variant = 0)
        {
            // ① 五条件(含库存门 —— 其生产实装 `BuildStockQuery` 也走选料器,同源)
            PlaceableResult check = _placeable.Check(moduleId, anchor, orientation);
            if (check != PlaceableResult.Success)
                return check;

            // ② 选料(规则六 BL-23):不足 ⇒ 整手拒绝,**零事件**
            BuildMaterialPlanResult planResult = BuildMaterialPlanner.PlanForModule(
                moduleId, _owner.Owner, _projection(), _modules, out _);
            if (planResult == BuildMaterialPlanResult.ModuleNotFound)
                return PlaceableResult.ModuleNotFound;
            if (planResult != BuildMaterialPlanResult.Success)
                return PlaceableResult.InsufficientStock;

            // ③ 落流(唯一事件 = StructurePlaced;扣减由 fold 派生)
            _writer.Place(anchor, moduleId, orientation, variant, _tick());
            return PlaceableResult.Success;
        }

        /// <summary>
        /// 尝试拆除一个结构。
        /// <para>判定不过 ⇒ 写者**零调用**(AC B-6 的副作用判据)。</para>
        /// </summary>
        /// <param name="structureId">待拆结构 id。</param>
        /// <returns>判定结果;<see cref="DemolishResult.Success"/> 表示已落流。</returns>
        public DemolishResult TryRemove(long structureId)
        {
            DemolishResult check = _demolish.Check(structureId);
            if (check != DemolishResult.Success)
                return check;

            bool removed = _writer.Remove(structureId, _tick(), DemolishChecker.P0DemolishRefundRatio);
            return removed ? DemolishResult.Success : DemolishResult.StructureNotFound;
        }

        /// <summary>
        /// 尝试把结构旋到指定朝向(幽灵循环选择的落点)。
        /// </summary>
        /// <param name="structureId">待改结构 id。</param>
        /// <param name="newOrientation">目标朝向(0 / 90 / 180 / 270)。</param>
        /// <returns>判定结果;<see cref="ModifiableResult.Success"/> 表示已落流。</returns>
        public ModifiableResult TryRotate(long structureId, int newOrientation)
        {
            if (!_registry.TryGet(structureId, out StructureInstance inst))
                return ModifiableResult.StructureNotFound;

            ModifiableResult check = _modifiable.Check(structureId, newOrientation, inst.Variant);
            if (check != ModifiableResult.Success)
                return check;

            bool modified = _writer.Modify(structureId, newOrientation, null, _tick());
            return modified ? ModifiableResult.Success : ModifiableResult.StructureNotFound;
        }

        /// <summary>
        /// 把结构**相对当前朝向**旋 <paramref name="deltaDegrees"/> 度(幽灵循环的落点)。
        /// </summary>
        /// <param name="structureId">待改结构 id。</param>
        /// <param name="deltaDegrees">增量(只接受 90 / 180 / 270 —— 与
        /// `PlaceableChecker.ComputeOccupiedCells` 的合法朝向集同源)。</param>
        /// <returns>判定结果(结构不存在 ⇒ <see cref="ModifiableResult.StructureNotFound"/>)。</returns>
        /// <exception cref="ArgumentException"><paramref name="deltaDegrees"/> 不在 {90,180,270}。</exception>
        public ModifiableResult TryRotateBy(long structureId, int deltaDegrees)
        {
            if (deltaDegrees != 90 && deltaDegrees != 180 && deltaDegrees != 270)
                throw new ArgumentException(
                    $"增量朝向只接受 90 / 180 / 270(四向整数旋转),收到 {deltaDegrees}",
                    nameof(deltaDegrees));

            if (!_registry.TryGet(structureId, out StructureInstance inst))
                return ModifiableResult.StructureNotFound;

            int target = (((inst.Orientation + deltaDegrees) % 360) + 360) % 360;
            return TryRotate(structureId, target);
        }

        /// <summary>模块目录里的全部模块 id(供驱动侧选默认幽灵模块)。</summary>
        /// <remarks>
        /// ⚠️ 这只是**目录投影**,不是「玩家已解锁的模块集」—— 后者归 42 / 21a 的选料界面,
        /// 本批尚未落地(见 `sprint-20` §十 S20-19 挂标)。
        /// </remarks>
        public IReadOnlyCollection<int> ModuleIds => _modules.GetAllModuleIds();

        /// <summary>该格上的结构 id(无则 -1)—— 供驱动侧把「指向某格」翻译成「拆哪个」。</summary>
        public long StructureIdAt(WorldPos cell) => _registry.GetInstanceAt(cell);
    }
}
