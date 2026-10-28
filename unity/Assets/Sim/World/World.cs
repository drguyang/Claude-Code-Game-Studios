// 权威来源:modular-building.md 规则三(OccupancyOverlay 合成 · F-23-1)
//             ADR-015 §三(单一整数格 · Nav 与 Overlay 共用 WorldPos)
//             ADR-022 §三(world_buildslots.json 槽位骨架)
//
// 核心机制:
//   - World 类: 组合 Nav(静态) + Overlay(动态占用) → EffectiveWalkable
//   - 单一占用表 slot_occupied(23 唯一写者)
//   - Overlay.blocked = slot_occupied 的纯函数投影
//   - 合成点唯一: 23 产出, 27/13 只读

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>世界状态(组合 Nav + Overlay)。</summary>
    /// <remarks>
    /// <para>🔁 <b>2026-10-28(S20-19)</b>:除写面 <see cref="IWorldOccupancy"/> 外,
    /// 另实现**读面** <see cref="IOccupancyQuery"/> —— 它此前**零生产实现**
    /// (只有测试替身),而 <see cref="PlaceableChecker"/> / <see cref="DemolishChecker"/> /
    /// <see cref="ModifiableChecker"/> 三个判定器都吃它 ⇒ 42 的建造发起方一接线就会撞上
    /// 「判定器无生产构造点」。本类已有 <see cref="IsSlotOccupied"/>(同一判据),
    /// 显式实现只是**把既有能力接到接口上**,零新增逻辑、零新增状态。</para>
    /// <para>⚠️ 刻意用**显式接口实现**:`IsOccupied` 这个名字对 `World` 的读者不如
    /// `IsSlotOccupied` 准确(它问的是「槽位占用表」,不是泛化的「占用」);
    /// 显式实现让两个名字并存而不互相冒充。</para>
    /// </remarks>
    public sealed class World : IWorldOccupancy, IOccupancyQuery
    {
        private readonly WorldGeometry _geometry;               // 静态几何(ADR-015)
        private readonly StructureInstanceRegistry _structures; // 结构实例表(派生态)
        private readonly BuildSlotCatalog _slotCatalog;         // 槽位骨架(派生态)
        private readonly bool[] _slotOccupied;                  // 占用表(单真相)

        /// <summary>
        /// 基线应用用的模块目录(<see cref="ApplyBakedInitial"/> 注入;<c>null</c> = 未应用基线)。
        /// <para>⚠️ 只被 <see cref="Reset"/> 复用一次 —— 它的存在是为了让「重置 = 回基线」
        /// 不需要调用方每次重传目录(重置的唯一生产调用者是 `StructureRestorer`,它已持目录)。</para>
        /// </summary>
        private IModuleCatalog _moduleCatalogForBaseline;

        public WorldGeometry Geometry => _geometry;
        public StructureInstanceRegistry Structures => _structures;
        public BuildSlotCatalog SlotCatalog => _slotCatalog;

        /// <summary>
        /// 已应用的 `BakedInitial` 实例数(<see cref="ApplyBakedInitial"/> 之后才有意义;
        /// 未应用恒 <c>0</c>)。
        /// <para>⚠️ 存在的理由 = **可观测性**:「数据声明了初始占用」与「运行期真的应用了」
        /// 在占用表上只差几格,靠日志数不出来;装配层 / 夹具直接读这个数。</para>
        /// </summary>
        public int BakedInitialStructureCount { get; private set; }

        /// <summary>构造 World。</summary>
        public World(WorldGeometry geometry, BuildSlotCatalog slotCatalog)
        {
            _geometry = geometry;
            _slotCatalog = slotCatalog ?? throw new ArgumentNullException(nameof(slotCatalog));
            _structures = new StructureInstanceRegistry();

            // 初始化占用表(WorldGeometry 的 TotalCells)
            _slotOccupied = new bool[geometry.TotalCells];
        }

        /// <summary>
        /// 应用**开档基线** `Overlay(0) := BakedInitial`(GDD `modular-building.md` 规则三 · 评审裁定 B3)。
        /// <para>🔁 <b>2026-10-28(S20-20 挂标 #2)新增</b>:此前 `BuildSlotCatalog` 根本不承载
        /// `BakedInitial` 标记 ⇒ 数据声明了初始占用、运行期**不应用**(开档医馆是空的)。
        /// 本方法即那个缺口的生产落点。</para>
        /// <para><b>做了什么</b>:遍历骨架里 `BakedInitial == true` 的槽位,按
        /// <c>(x,y,z)</c> 升序 —— 这是**确定性发号**的前提 —— 逐格
        /// ① 按模块目录算真足迹(与放置判定同源 `PlaceableChecker.ComputeOccupiedCells`)、
        /// ② 以 <c>structure_id = 1, 2, …</c> 落进实例表(规则三:初始家具的 `structure_id`
        /// 由基线定义时分配,进实例表 —— 即规则七的「结构身份」)、
        /// ③ 占住足迹格。</para>
        /// <para>⚠️ <b>不发任何事件</b>(规则三:开档家具不合成 `StructurePlaced`,
        /// 基线占用不进任何事件流、不存档)。故本方法**不持 `IEventSink`** —— 它在签名上
        /// 就没有写流的能力,不是靠纪律。</para>
        /// <para>⚠️ <b>发号确定性</b>:同一次加载跑两遍得到同一批 id(排序键全序 + 无字典迭代序)。
        /// 这使「基线 + 流重放」在两次读档之间逐位可复现(规则三的 `Overlay(t) := BakedInitial ⊕ 事件`)。</para>
        /// </summary>
        /// <param name="moduleCatalog">模块目录(算足迹 / 校验模块存在;非 null)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="moduleCatalog"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">
        /// 某格 <c>BakedInitial == true</c> 但未给 `module_id`(槽位 0 模块 id),
        /// 或其 `module_id` 不在目录内 —— 二者都是**数据与目录不同源**,
        /// 静默跳过会复现本方法要修的那个缺陷(「声明了但不应用」),故 fail-closed。
        /// </exception>
        public void ApplyBakedInitial(IModuleCatalog moduleCatalog)
        {
            if (moduleCatalog == null) throw new ArgumentNullException(nameof(moduleCatalog));
            // ⚠️ **只许一次**(fail-closed):第二次调用会把 id 1…N 重新 `Restore` 到**已被
            //    玩家改动过的**实例表上 —— 若其间玩家新放的结构恰好占到同号,基线会静默覆盖它。
            //    基线属于「开档」这一个时点;要再来一次必须走 `Reset()`(它按顺序重建,不撞号)。
            if (_moduleCatalogForBaseline != null)
                throw new InvalidOperationException(
                    "`ApplyBakedInitial` 已应用过一次 —— 基线只在开档时点应用一次;" +
                    "重新回到基线请用 `Reset()`(它清空后按序重建,不会与既有实例撞号)");

            _moduleCatalogForBaseline = moduleCatalog;

            // ── 原子性(2026-10-28 评审 B-1 修复)──────────────────────────
            // ⚠️ **原文不删,就地加注**:初版只写 `_moduleCatalogForBaseline = moduleCatalog;`
            //    然后直接调本体。那有两个后果,均属**静默**失败:
            //    ① **半应用**:骨架 `[合法格, 非法 module_id 格]` 时,i=0 已 `Restore` 落库
            //       且已 `OccupyCells` 占格,随后 i=1 抛 ⇒ 世界停在「开档少了几件、但少的那几件
            //       的格已经被占住」的中间态,且**没有任何信号**;
            //    ② **报文误导**:标志已置 ⇒ 调用方重试时收到「已应用过一次」,而它其实**从没
            //       完整应用过** —— 报文把「失败了」说成了「做过了」。
            //    ⇒ 现改为**全有或全无**:任一步抛 ⇒ 回滚到「未应用」态(实例表 / 占用表 / 标志 /
            //       计数四样一起复位),再原样抛出。回滚**不重放基线**(那是 `Reset()` 的语义,
            //       此处要的是「回到没应用过」)。
            try
            {
                ApplyBakedInitialBaseline();
            }
            catch
            {
                _structures.Reset();                                   // 清实例表 + 复位发号
                Array.Clear(_slotOccupied, 0, _slotOccupied.Length);   // 清占用
                _moduleCatalogForBaseline = null;                      // 允许修好数据后重试
                BakedInitialStructureCount = 0;
                throw;
            }
        }

        /// <summary>基线应用本体(<see cref="ApplyBakedInitial"/> 与 <see cref="Reset"/> 共用)。</summary>
        private void ApplyBakedInitialBaseline()
        {
            if (_moduleCatalogForBaseline == null) return;   // 未注入 ⇒ 不应用(既有测试世界)

            var baked = new List<BuildSlot>();
            foreach (WorldPos pos in _slotCatalog.GetAllSlotPositions())
                if (_slotCatalog.TryGetSlot(pos, out BuildSlot s) && s.BakedInitial)
                    baked.Add(s);

            // 全序 (x,y,z) 升序 —— 发号确定性的唯一来源(骨架是集合,枚举序不作契约)。
            baked.Sort((a, b) =>
            {
                int cmp = a.Position.X.CompareTo(b.Position.X);
                if (cmp != 0) return cmp;
                cmp = a.Position.Y.CompareTo(b.Position.Y);
                if (cmp != 0) return cmp;
                return a.Position.Z.CompareTo(b.Position.Z);
            });

            long nextId = 1;
            for (int i = 0; i < baked.Count; i++)
            {
                BuildSlot slot = baked[i];

                if (slot.ModuleId == BuildSlot.NoModule)
                    throw new InvalidOperationException(
                        $"槽位 ({slot.Position.X},{slot.Position.Y},{slot.Position.Z}) 标了 " +
                        "`baked_initial = true` 却没有 `module_id` —— 开档家具必须指明是哪件家具;" +
                        "静默跳过会复现「声明了但不应用」这个缺陷本身(烘焙期应已拦,此处是纵深防御)");

                ModuleDefinition? defOpt = _moduleCatalogForBaseline.GetModuleDefinition(slot.ModuleId);
                if (!defOpt.HasValue)
                    throw new InvalidOperationException(
                        $"槽位 ({slot.Position.X},{slot.Position.Y},{slot.Position.Z}) 的 " +
                        $"`module_id = {slot.ModuleId}` 不在模块目录内 —— 槽位骨架与模块目录不同源;" +
                        "静默跳过会让开档少一件家具且无任何信号");

                // 足迹与放置判定**同源**(C1 的教训:两处各算一份就会错位)。
                // 基线朝向恒 0 —— 作者态今日无朝向字段;将来要支持须同时改导出契约。
                IReadOnlyList<WorldPos> cells =
                    PlaceableChecker.ComputeOccupiedCells(defOpt.Value, slot.Position, orientation: 0);

                _structures.Restore(nextId++, slot.Position, slot.ModuleId,
                                    orientation: 0, variant: 0, occupiedCells: cells);
                OccupyCells(cells);
            }

            BakedInitialStructureCount = (int)(nextId - 1);
        }

        /// <summary>
        /// 把世界重置为**开档基线**(= `Overlay(0) := BakedInitial`)—— 读档重建前的第一步
        /// (S20-18 · 2026-10-28;基线半边 = S20-20 挂标 #2)。
        /// <para>清两样**派生态**:结构实例表(含发号计数器,<see cref="StructureInstanceRegistry.Reset"/>)
        /// + 占用表 <c>_slotOccupied</c> 全零;**随后重放基线**
        /// (<see cref="ApplyBakedInitial"/> 已注入目录时)。</para>
        /// <para>🔁 <b>2026-10-28 语义订正(原文不删)</b>:本方法原文档写着「重置为**空**」。
        /// 那是**错的** —— GDD 规则三定的是 `Overlay(t) := BakedInitial ⊕ 事件序列`,
        /// 即重放的起点**不是空世界**而是基线。按原文实现,读档后外壳与开档家具会全部消失
        /// (`StructureRestorer` 的两处 `Reset()` 各自抹掉基线)⇒ 玩家读档丢房子。
        /// 现语义 = 「重置到基线」;未注入目录的世界(既有 109 处夹具)行为**不变**(仍是空)。</para>
        /// <para><b>刻意不清的另两样</b>:<c>_geometry</c>(世界几何)与 <c>_slotCatalog</c>(槽位骨架)
        /// —— 它们是 ADR-015 的**烘焙静态层**,读档不改变;清零它们等于把静态数据当成状态,
        /// 且 `WorldGeometry` 根本没有「重建为空」的入口(`WorldLattice.cs:295-298` 明令)。</para>
        /// <para>⚠️ <b>唯一生产调用者 = <see cref="StructureRestorer"/> 的重放入口</b>:重置与
        /// 重放必须成对(只重置不重放 ⇒ 房子全消失;只重放不重置 ⇒ 旧世界残留并叠加)。
        /// 故本方法**不单独接进任何启动/读档路径**,只作为重放入口内部的第一步。</para>
        /// </summary>
        public void Reset()
        {
            _structures.Reset();
            Array.Clear(_slotOccupied, 0, _slotOccupied.Length);
            // ⚠️ **2026-10-28 评审 B-1 同型加固**:基线本体若在中途抛(骨架与目录不同源),
            //    半应用会让世界停在「清了一半 + 落了半个基线」的中间态,而 `StructureRestorer`
            //    的 catch 会**再调一次本方法** —— 那会把「回滚到空态」变成「再抛一次」,
            //    原始异常被嵌套异常顶掉。⇒ 失败即清成**确定的空态**再抛。
            try
            {
                ApplyBakedInitialBaseline();   // 未注入目录 ⇒ 内部早退,行为与订正前一致
            }
            catch
            {
                _structures.Reset();
                Array.Clear(_slotOccupied, 0, _slotOccupied.Length);
                BakedInitialStructureCount = 0;
                throw;
            }
        }

        // ── 占用查询(Overlay.blocked = slot_occupied 投影) ──

        /// <summary>检查格是否被占用。越界返回 false(fail-closed)。</summary>
        public bool IsSlotOccupied(WorldPos cell)
        {
            int idx = _geometry.ToIndex(cell);
            if (idx < 0 || idx >= _slotOccupied.Length)
                return false;
            return _slotOccupied[idx];
        }

        /// <summary>
        /// <see cref="IOccupancyQuery"/> 的实现 —— 与 <see cref="IsSlotOccupied"/> **同一判据**
        /// (S20-19 · 2026-10-28)。
        /// </summary>
        /// <remarks>
        /// ⚠️ 越界返回 false(与 <see cref="IsSlotOccupied"/> 一致,fail-closed)。
        /// 判定器侧的 `OutOfRegion` 条件会先拦掉越界格,本处不重复判。
        /// </remarks>
        bool IOccupancyQuery.IsOccupied(WorldPos cell) => IsSlotOccupied(cell);

        /// <summary>获取占用表(用于合成 EffectiveWalkable)。</summary>
        public IReadOnlyList<bool> GetOccupancyArray()
        {
            return Array.AsReadOnly(_slotOccupied);
        }

        // ── 占用变更(23 唯一写者) ──

        /// <summary>标记格为占用。</summary>
        public void OccupyCell(WorldPos cell)
        {
            int idx = _geometry.ToIndex(cell);
            if (idx >= 0 && idx < _slotOccupied.Length)
                _slotOccupied[idx] = true;
        }

        /// <summary>清除格的占用。</summary>
        public void FreeCell(WorldPos cell)
        {
            int idx = _geometry.ToIndex(cell);
            if (idx >= 0 && idx < _slotOccupied.Length)
                _slotOccupied[idx] = false;
        }

        /// <summary>批量占用(模块放置时使用)。</summary>
        public void OccupyCells(IEnumerable<WorldPos> cells)
        {
            foreach (var cell in cells)
                OccupyCell(cell);
        }

        /// <summary>批量释放(模块拆除时使用)。</summary>
        public void FreeCells(IEnumerable<WorldPos> cells)
        {
            foreach (var cell in cells)
                FreeCell(cell);
        }

        // ── EffectiveWalkable 合成(F-23-1) ──

        /// <summary>
        /// EffectiveWalkable(cell) := Nav[cell].walkable ∧ ¬slot_occupied(cell)
        /// 27 / 13 只读此方法,不读 Nav 原件。
        /// </summary>
        public bool IsEffectivelyWalkable(WorldPos cell)
        {
            int idx = _geometry.ToIndex(cell);
            if (idx < 0 || idx >= _slotOccupied.Length)
                return false;

            TerrainCell terrain = _geometry.GetTerrain(cell);
            return terrain.Walkable && !_slotOccupied[idx];
        }

        /// <summary>获取格的 EffectiveWalkable(用于 27/13 查询接口)。</summary>
        public bool GetEffectiveWalkable(WorldPos cell)
        {
            return IsEffectivelyWalkable(cell);
        }

        // ── 结构查询 ──

        /// <summary>获取指定格的模块 id(无则返回 -1)。</summary>
        public int GetModuleAt(WorldPos cell)
        {
            // ⚠️ 2026-10-03(C8/ID):structure id 全链升 `long` —— 局部变量随之。
            long sid = _structures.GetInstanceAt(cell);
            if (sid < 0)
                return -1;

            if (_structures.TryGet(sid, out var inst))
                return inst.ModuleId;

            return -1;
        }

        /// <summary>获取总占用数。</summary>
        public int OccupiedCount
        {
            get
            {
                int count = 0;
                foreach (var b in _slotOccupied)
                    if (b) count++;
                return count;
            }
        }
    }

    /// <summary>
    /// OccupancyOverlay —— slot_occupied 的纯函数投影。
    /// 27 / 13 只读此类型,不读 Nav 原件。
    /// </summary>
    public readonly struct OccupancyOverlay
    {
        private readonly IReadOnlyList<bool> _slotOccupied;

        public OccupancyOverlay(IReadOnlyList<bool> slotOccupied)
        {
            _slotOccupied = slotOccupied;
        }

        /// <summary>格是否被占用(blocked)。越界返回 true(fail-closed)。</summary>
        public bool IsBlocked(WorldPos cell, WorldGeometry geometry)
        {
            int idx = geometry.ToIndex(cell);
            if (idx < 0 || idx >= _slotOccupied.Count)
                return true;
            return _slotOccupied[idx];
        }
    }
}
