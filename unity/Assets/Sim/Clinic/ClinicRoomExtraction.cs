// Story 002 · clinic-machine —— 24「房间析出与连通规则」(S17-1)。
//
// 卡面:production/epics/clinic-machine/story-002-room-extraction-connectivity.md
// GDD:design/gdd/clinic-machine.md
//   规则一 —— 房间 = 一个**连通的家具格集**;邻接 = 整数格 `ABS(dx)+ABS(dy) == 1`(4 邻接,同层);
//     外壳格(BakedInitial:壁 / 地基 / 房顶)与空地板格 = **非格**(不计分、不参与连通);
//     敞开空间(无围合)的家具簇**仍是合法房间**;单连通块前提:≥ 2 簇 ⇒ **整份布局 ROOM_NONE**。
//   EC-24-01(空布局 ⇒ ROOM_NONE,不是错误)· EC-24-02(未匹配房间类型 ⇒ 中性)· EC-24-04(外壳格 /
//     边界格/角落)· EC-24-07(≥ 2 簇 ⇒ 整盘 ROOM_NONE,禁「各块求和」/ 禁「只评最大块」)。
// ADR-015 §三(单一整数格)· §五(建造模块对齐到格;4 邻接判定,非物理 / 非 NavMesh)。
// ADR-005(纯函数派生,无状态,可重放)。
// control-manifest §Core Layer Rules:
//   Required —— 连通判据 `ABS(dx)+ABS(dy)==1`(4 邻接,同层);壳格 / 空地板不参与析出、不计分、不传导;
//   Forbidden —— PhysX 触发器 / Collider / NavMesh / 连续坐标参与房间判定;围合(flood-fill 封闭性)检查;
//   Guardrail —— ≥ 2 个不连通家具簇 ⇒ 整盘 `ROOM_NONE (0,0)`,不做「就近簇」部分生效(EC-24-07)。
//
// 门 A(ADR-017 §二 / ADR-025 §①):`Sim` 引用集恰 = {BCL, Sim.Contracts} ⇒ 本文件零 `UnityEngine`。
//   全部整数运算:无 float / double / Math.Round / sqrt。
//
// ═══════════════════════════════════════════════════════════════════════════════════════
// ⚠️⚠️ 甲案代理判据 —— 逐字登记(story §Implementation Notes 7,用户 2026-10-21 裁定)
// ═══════════════════════════════════════════════════════════════════════════════════════
// 本件的「家具格」定义 = **被 `StructurePlaced` 事件占用、且在本只读视图内可达的格**
// (即 23 的实例表里被占用的格)。
//
//   ⇒ **这是 `type(c) ∈ FURN_TYPES` 的代理判据,不是它的等价物;代理更宽** ——
//     任何放置件都算家具格,不论其类型是否落在 `FURN_TYPES` 闭集内。
//
// 为什么必须用代理(GDD F-24 公共定义的真判据在仓库里**无法求值**):
//   · `FURN_TYPES` 闭集**全库零定义**;`FURN_TABLE` 数据文件不存在;
//     `IFurnTypeSource` **零生产实现**;`build_modules.json`(`module_id` 的目录)同样不存在。
//     (`TD-clinic-01` 自陈封 001→005 全链入口。)
//     🔁 **2026-10-21 订正(O-24-1 兑现)**:前两项**已不成立** —— 闭集 = `ClinicFurnitureCatalogue`,
//     作者态 `assets/data/item_database_furniture.json` → `item_database_furniture.cooked.bytes`;
//     `IFurnTypeSource` 的生产实现 = `ItemDatabaseFurnitureBake.AsFurnTypeSource`。
//     仍成立的是 `build_modules.json`(23 的模块目录,`module_id` 的真源)—— 本类只**消费** id,
//     不产生 id。⇒ 真判据已可求值(见 `Extract(view, furnTypes)`);原文保留为历史口径。
//   · 闭集落地(`O-24-1`)时:若真判据与代理**不等价**,既有测试须**转红**,届时改判据并如实登记,
//     **不得静默改绿**(卡面实施约束 ③)。
//     🔁 **2026-10-21 执行记录**:两条入口并存(默认 = 代理,真判据 = 重载),
//     C11 检测测已按本条**改判并留痕**(见该测的「2026-10-21 改判」注释)。
//   · 缺口哨兵:`ClinicRoomExtraction.FurnTypeClosureEnforced == false`(恒 false),
//     由测试 `test_furn_type_closure_absent_sentinel_false` 钉住 —— 缺席**可观测而非静默**
//     (承 clinic-001 的缺口哨兵先例)。
//
// ⚠️ 本件**不得**为将来的类型匹配提前造接缝(卡面实施约束 ④):视图只出格、不出 `module_id`;
//   类型匹配(`ROOM_TABLE` 子句求值)归 S17-2 / 后续。
//
// ⚠️ 本算法**没有、也不得添加**围合 / flood-fill 封闭性检查(GDD 规则一明令):
//   开顶家具簇仍是合法房间(P0「零件摆对就运转」)。下面的并查集只回答
//   「S 被切成几个 4-连通分量」,它**不引入「内部 / 外部」概念**,也不从边界向外填充。
//
// ⚠️ 陷阱 ①(卡面实测):`StructureInstanceRegistry.GetInstanceAt(anchor)` **只比对 `Anchor`**
//   ⇒ 多格结构的非锚点格查不到。本件**绝不**逐格调 `GetInstanceAt` / `World.GetModuleAt`;
//   S 的唯一来源 = 视图逐实例报出的**完整 `OccupiedCells`**。
//   钉住它的测试:`test_multi_cell_structure_non_anchor_cells_are_traversed`。
//
// ⚠️ 陷阱 ②(卡面实测):注册表无公开枚举器 ⇒ 输入经 `IPlacedStructureReadView` 注入;
//   本程序集不引用 `StructureInstanceRegistry` / `StructureWriter` / `IEventSink` / `IWorldOccupancy`。

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Clinic
{
    /// <summary>
    /// 房间析出的结果(纯函数输出,**无缓存、无隐藏状态**)。
    /// <para><see cref="RoomCells"/> 就是「房间 → 格」的反向索引(乘子求值按房间取成员),
    /// 在返回值内**一次性构建**(卡面 §Implementation Notes 6:不缓存于 24 之外)。</para>
    /// <para>⚠️ `ROOM_NONE` 时 <see cref="RoomCells"/> **恒为空**(不提供部分成员)——
    /// 防「就近簇 / 最大簇部分生效」(EC-24-07 的 Guardrail)。失败的可诊断面由
    /// <see cref="ConnectedClusterCount"/> / <see cref="CellCount"/> 提供(全零是**可修复、可诊断**的失败态)。</para>
    /// </summary>
    public readonly struct ClinicRoomExtractionResult
    {
        private static readonly IReadOnlyList<WorldPos> EmptyCells = Array.Empty<WorldPos>();

        private readonly IReadOnlyList<WorldPos> _roomCells;

        /// <summary>房间标识;`ROOM_NONE (0,0)` = 未析出房间。</summary>
        public readonly ClinicRoomId RoomId;

        /// <summary>
        /// S 的 4-连通分量数(同层,`ABS(dx)+ABS(dy)==1`)。
        /// <para>0 = 布局内无任何家具格(EC-24-01,合法状态,不是错误);
        /// 1 = 恰好一个连通块(唯一可成房间的情形);≥ 2 = 整盘 `ROOM_NONE`(EC-24-07)。</para>
        /// </summary>
        public readonly int ConnectedClusterCount;

        /// <summary>|S| —— 去重后的家具格数(重叠占用格只算一次)。</summary>
        public readonly int CellCount;

        internal ClinicRoomExtractionResult(ClinicRoomId roomId, int connectedClusterCount, int cellCount,
                                            IReadOnlyList<WorldPos> roomCells)
        {
            RoomId = roomId;
            ConnectedClusterCount = connectedClusterCount;
            CellCount = cellCount;
            _roomCells = roomCells;
        }

        /// <summary>房间成员格(**规范排序**:x → y → z 升序);`ROOM_NONE` 时为空列表。</summary>
        /// <remarks>只读快照:调用方不得改写(实现为 <see cref="ReadOnlyCollection{T}"/> 包装)。</remarks>
        public IReadOnlyList<WorldPos> RoomCells => _roomCells ?? EmptyCells;

        /// <summary>是否析出房间(= <see cref="RoomId"/> 不是 `ROOM_NONE`)。</summary>
        public bool HasRoom => !RoomId.IsNone;
    }

    /// <summary>
    /// 系统 24 的房间析出纯函数:输入 = 23 的只读结构视图,输出 = 房间标识 + 成员格。
    /// <para>无状态(静态纯函数):同一布局两次求值 ⇒ 同一 <see cref="ClinicRoomId"/>;
    /// 调用之间不保留任何东西(AC 第 5 条 / ADR-005)。</para>
    /// </summary>
    public static class ClinicRoomExtraction
    {
        // ── 缺口哨兵 ────────────────────────────────────────────────────────────
        /// <summary>
        /// <c>FURN_TYPES</c> 闭集是否在**本类的默认入口**(<see cref="Extract(IPlacedStructureReadView)"/>)上被强制。
        /// <para>**恒 false** —— 默认入口至今走**代理判据**(被 `StructurePlaced` 占用的格),
        /// 代理**更宽**,不是等价物(见文件头「甲案代理判据」)。</para>
        /// <para>🔁 **2026-10-21(O-24-1 兑现轮)语义收窄**:闭集**已落地**
        /// (<see cref="ClinicFurnitureCatalogue"/> / `item_database_furniture.cooked.bytes`),
        /// 真判据**已可求值**并经 <see cref="Extract(IPlacedStructureReadView, IReadOnlyCollection{string})"/>
        /// 公开;但它**不是**默认入口 ⇒ 本哨兵仍为 false。它现在的唯一含义是
        /// 「默认入口 = 代理」,**不再**表示「闭集不存在」—— 后者由
        /// <see cref="ClinicModifierEvaluator.FurnTraitsProductionReady"/> 回答(已 true)。</para>
        /// <para>⚠️ 把默认入口改成真判据 ⇒ 本属性转 true;测试
        /// <c>test_furn_type_closure_absent_isDetectable_notJustObservable</c> 的豁免段须同步删除。</para>
        /// </summary>
        public static bool FurnTypeClosureEnforced => false;

        /// <summary>析出房间(纯函数;不抛设计态异常 —— 空布局也是合法输入,EC-24-01)。</summary>
        /// <param name="view">23 的只读结构视图(注入;本方法不持有其引用)。</param>
        /// <returns>恰好 1 个连通簇 ⇒ 真实 room id + 成员格;否则 `ROOM_NONE` + 空成员格。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="view"/> 为 null(接线错误,非设计态)。</exception>
        public static ClinicRoomExtractionResult Extract(IPlacedStructureReadView view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));

            // 外壳格 / 空地板格在本视图中**无表示** —— 二者都不是 `StructurePlaced` 的产物,
            // 因此在 23 的只读视图里**同样缺席**。这与 GDD 规则一的「非格」分类一致
            // (不计分、不参与连通);析出判据不需要区分「壳格」与「空地板」。
            return ExtractFromMembership(CollectFurnitureCells(view));
        }

        /// <summary>
        /// 真判据版析出:<c>S = { c | type(c) ∈ FURN_TYPES }</c>(GDD `:161` 逐字)。
        /// <para>🔁 **2026-10-21 新增(O-24-1 兑现)**:闭集终于有生产来源
        /// (<see cref="ClinicFurnitureCatalogue"/>),故真判据**可求值**。语义与
        /// <see cref="Extract(IPlacedStructureReadView)"/>(代理版)的**唯一差别** = 成员判据:
        /// 代理版把「凡被 `StructurePlaced` 占用的格」都算家具格,**更宽**(非家具结构会混进 S,
        /// 既可能把两个家具簇桥接成一个房间,也可能把 `|S|` 撑过 `n_max`)。</para>
        /// <para>⚠️ **不等价**,所以两条入口并存而非改写代理版:代理版是 story-002 已验收的行为,
        /// 改它会静默改变既有测试的语义(卡面实施约束 ③「不得静默改绿」)。生产装配层应选**本条**;
        /// 选哪条是装配层的接线,尚未落地 ⇒ 哨兵 <see cref="FurnTypeClosureEnforced"/> 仍为 false。</para>
        /// </summary>
        /// <param name="view">23 的只读结构视图(须实现 <see cref="IPlacedStructureReadView.GetModuleId"/>)。</param>
        /// <param name="furnTypes">`FURN_TYPES` 闭集(来自 <see cref="ClinicFurnitureCatalogue"/>;
        /// null ⇒ 退回代理版,即等价于单参重载)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="view"/> 为 null。</exception>
        public static ClinicRoomExtractionResult Extract(
            IPlacedStructureReadView view, IReadOnlyCollection<string> furnTypes)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (furnTypes == null) return Extract(view);
            return ExtractFromMembership(CollectFurnitureCells(view, furnTypes));
        }

        /// <summary>析出的单一实现体(两条入口共用 —— 成员判据之外的每一步都不许分叉)。</summary>
        private static ClinicRoomExtractionResult ExtractFromMembership(HashSet<WorldPos> membership)
        {
            // ── 1. 空布局 ────────────────────────────────────────────────────
            if (membership.Count == 0)
            {
                // EC-24-01:开局空医馆 —— 无连通格集 ⇒ ROOM_NONE。**不是错误**。
                return new ClinicRoomExtractionResult(ClinicRoomId.None, 0, 0, null);
            }

            // ── 2. 规范排序 ──────────────────────────────────────────────────
            // 消除两种**实现序**:视图的实例 / 格报告序,以及 HashSet/Dictionary 的桶序。
            // 排序后的一切(并查集处理序、成员序、哈希输入序)都只依赖格集本身。
            var cells = new List<WorldPos>(membership);
            cells.Sort(CompareCell);

            // ── 3. 4 连通分量 ────────────────────────────────────────────────
            int clusters = CountComponents(cells);
            if (clusters != 1)
            {
                // EC-24-07:≥ 2 簇 ⇒ **整盘** ROOM_NONE —— 不取最大簇、不逐块求和(求和即跨房间项)。
                return new ClinicRoomExtractionResult(ClinicRoomId.None, clusters, cells.Count, null);
            }

            // ── 4. 房间标识(由布局确定性派生)+ 成员格(反向索引一次性构建)──
            ClinicRoomId roomId = ComputeRoomId(cells);
            return new ClinicRoomExtractionResult(roomId, 1, cells.Count,
                new ReadOnlyCollection<WorldPos>(cells));
        }

        // ════════════════════════════════════════════════════════════════════
        // 内部实现(全部 private static —— 公有面刻意只有 Extract + 缺口哨兵)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 汇集 S:遍历视图的每个实例,取其<strong>完整占用格</strong>(不是锚点格)。
        /// <para>⚠️ 陷阱 ①:不可退化成逐格 <c>GetInstanceAt</c> —— 多格结构的非锚点格会丢。</para>
        /// <para>重叠占用格(防御性)去重:同一格只算一件家具格。</para>
        /// </summary>
        private static HashSet<WorldPos> CollectFurnitureCells(IPlacedStructureReadView view)
        {
            var membership = new HashSet<WorldPos>();
            int structureCount = view.StructureCount;
            for (int i = 0; i < structureCount; i++)
            {
                IReadOnlyList<WorldPos> occupied = view.GetOccupiedCells(i);
                // 视图契约声明非 null;违约时按空集处理 —— 析出不因「形状错误」改变语义,
                // 也不静默制造格(空集是唯一安全的退化,且不改变其它实例的贡献)。
                if (occupied == null) continue;
                for (int j = 0; j < occupied.Count; j++)
                    membership.Add(occupied[j]);
            }
            return membership;
        }

        /// <summary>
        /// 汇集 S(真判据版):只收 `type(c) ∈ FURN_TYPES` 的实例的格(GDD `:161`)。
        /// <para>⚠️ 与代理版的**唯一**差别在成员判据;实例报告序 / 格序仍由后续规范排序吸收。</para>
        /// <para>模块 id 为 null / 空 = 无模块标识 ⇒ `type(c)` 无从谈起 ⇒ **排除**
        /// (不是「随便算家具」;GDD 的判据是「属于闭集」,无从证明属于即不属于)。</para>
        /// </summary>
        private static HashSet<WorldPos> CollectFurnitureCells(
            IPlacedStructureReadView view, IReadOnlyCollection<string> furnTypes)
        {
            var membership = new HashSet<WorldPos>();
            int structureCount = view.StructureCount;
            for (int i = 0; i < structureCount; i++)
            {
                if (!IsFurnType(view.GetModuleId(i), furnTypes)) continue;

                IReadOnlyList<WorldPos> occupied = view.GetOccupiedCells(i);
                if (occupied == null) continue;
                for (int j = 0; j < occupied.Count; j++)
                    membership.Add(occupied[j]);
            }
            return membership;
        }

        /// <summary>`module_id ∈ FURN_TYPES`(序数比较;null / 空 ⇒ false)。</summary>
        private static bool IsFurnType(string moduleId, IReadOnlyCollection<string> furnTypes)
        {
            if (string.IsNullOrEmpty(moduleId)) return false;
            foreach (string t in furnTypes)
                if (string.Equals(t, moduleId, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>格的规范全序(x → y → z 升序)。纯比较,无减法(不会因极坐标溢出)。</summary>
        private static int CompareCell(WorldPos a, WorldPos b)
        {
            if (a.X != b.X) return a.X < b.X ? -1 : 1;
            if (a.Y != b.Y) return a.Y < b.Y ? -1 : 1;
            if (a.Z != b.Z) return a.Z < b.Z ? -1 : 1;
            return 0;
        }

        /// <summary>
        /// 并查集求 S 的 4-连通分量数。
        /// <para>邻接 = 整数格 `ABS(dx)+ABS(dy) == 1` 且<b>同层(z 相等)</b>(GDD 规则一 / ADR-015 §五)。
        /// 对角(`|dx|==1 ∧ |dy|==1`)**不连通**;跨层(`dz≠0`)**不连通**。</para>
        /// <para>只查 `+x` / `+y` 两个方向 —— 与 `adj_sum` 同口径(F-24-1 公共定义):每条边恰好并一次。</para>
        /// <para>⚠️ 这是**连通性枚举**,不是围合检测:没有「内部 / 外部」,也不从边界向外填充。</para>
        /// </summary>
        private static int CountComponents(List<WorldPos> cells)
        {
            int n = cells.Count;
            var indexOf = new Dictionary<WorldPos, int>(n);
            for (int i = 0; i < n; i++) indexOf[cells[i]] = i;

            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;

            int clusters = n;
            for (int i = 0; i < n; i++)
            {
                WorldPos c = cells[i];
                // 防 int 溢出制造「幻影邻格」:只有坐标未到上界时才查 +1 方向。
                if (c.X < int.MaxValue)
                    TryUnion(indexOf, parent, ref clusters, i, new WorldPos(c.X + 1, c.Y, c.Z));
                if (c.Y < int.MaxValue)
                    TryUnion(indexOf, parent, ref clusters, i, new WorldPos(c.X, c.Y + 1, c.Z));
            }
            return clusters;
        }

        /// <summary>若 <paramref name="neighbour"/> 属于 S,则把 <paramref name="from"/> 与它并入同一分量。</summary>
        private static void TryUnion(Dictionary<WorldPos, int> indexOf, int[] parent, ref int clusters,
                                     int from, WorldPos neighbour)
        {
            int to;
            if (!indexOf.TryGetValue(neighbour, out to)) return;

            int rootFrom = Find(parent, from);
            int rootTo = Find(parent, to);
            if (rootFrom == rootTo) return;

            // 小索引为根 —— 处理序已由规范排序固定 ⇒ 根选择也确定(结果与桶序无关)。
            if (rootFrom < rootTo) parent[rootTo] = rootFrom;
            else parent[rootFrom] = rootTo;
            clusters--;
        }

        /// <summary>并查集寻根(路径压缩)。</summary>
        private static int Find(int[] parent, int index)
        {
            while (parent[index] != index)
            {
                parent[index] = parent[parent[index]];
                index = parent[index];
            }
            return index;
        }

        // FNV-1a 的 64 位常量(确定性整数混合;零 BCL 哈希依赖)。
        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        /// <summary>
        /// 由**规范排序后的格集**派生房间标识(卡面 AC 第 5 条:同一布局两次求值 ⇒ 同一 room id)。
        /// <para>禁用面(逐条都有具体故障模式):`string.GetHashCode`(.NET Core 每进程随机化)·
        /// `UnityEngine.Random`(门 A 外)· 字典枚举序(桶序不保证)· `structure_id`(写入顺序产物)·
        /// 时间 / 随机源(重放不一致)。</para>
        /// <para>`ROOM_NONE = (0,0)` 保留给哨兵 ⇒ 哈希恰为 0 时**确定性**重映射低半为 1,
        /// 保证真实 id 与哨兵空间不交(卡面实施约束)。</para>
        /// </summary>
        private static ClinicRoomId ComputeRoomId(List<WorldPos> sortedCells)
        {
            ulong hash = FnvOffsetBasis;
            for (int i = 0; i < sortedCells.Count; i++)
            {
                WorldPos c = sortedCells[i];
                hash = MixInt(hash, c.X);
                hash = MixInt(hash, c.Y);
                hash = MixInt(hash, c.Z);
            }

            int high = unchecked((int)(hash >> 32));
            int low = unchecked((int)(hash & 0xFFFFFFFFUL));

            // 只有整段 64 位哈希恰为 0 才会走到这里(确定性重映射,不是随机)。
            if (high == 0 && low == 0) low = 1;

            return new ClinicRoomId(high, low);
        }

        /// <summary>FNV-1a 的 32 位分块变体:把一个 int 的无符号位模式混入哈希。</summary>
        private static ulong MixInt(ulong hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= FnvPrime;
            }
            return hash;
        }
    }
}
