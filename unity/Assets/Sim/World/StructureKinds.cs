// 权威来源:modular-building.md 规则四(Structure* 世界流事件)
//             ADR-009 §三(Structure 三 Kind 骨架 · 载荷归本 GDD)
//             ADR-015 §五(模块化网格 · 朝向整数枚举)
//             ADR-029(载荷编码唯一路径 = IPayloadEncoder)
//
// 核心机制:
//   - StructurePlaced / Removed / Modified 世界流事件
//   - StructureInstanceRegistry: 实例表(派生态,不存事件流)
//   - 主机唯一 Append(通过构造时传入的 IEventSink)
//   - 全部整数载荷,无 float
//   - **载荷编码经 IPayloadEncoder**(ADR-029 §③:零手搓 PayloadRef)
//   - **拆除返还 = 另发一条 DropSpawned**(2026-10-27 · R17 闭合批 ·
//     用户裁定「地面掉落」)。返还物进**世界流**、由 20 落地(写者 = `DropEventWriter`),
//     23 只触发不写 —— 与 `entities.yaml` `DropSpawned.author` 的
//     「落地写者 = 20 / 触发方」口径一致。详见 `Remove` 的 remarks。
//
// ⚠️ 2026-10-02(ADR-029 接线支 Story 007)删除本文件内的三支 payload struct 副本:
//   StructurePlacedPayload / StructureRemovedPayload / StructureModifiedPayload
//   曾**重复定义**于本文件(DaYiJingCheng.Sim.World)与
//   Sim.Contracts/Payloads/WorldPayloads.cs(DaYiJingCheng.Sim.Contracts)。
//   两份字段同序同义,**但 `StructureId` 类型不同**(本文件 = int,契约版 = long)。
//   契约版才是权威 —— PayloadCodec 与全部测试用 long;本文件那三支**零引用**(死代码)。
//   ⇒ 已删,统一用契约版(本文件经 using DaYiJingCheng.Sim.Contracts 可见)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    #region 结构实例表(第三份派生态)

    /// <summary>结构实例数据。</summary>
    public readonly struct StructureInstance
    {
        /// <summary>
        /// 结构身份 —— **`long`(i64)**,非 `int`。
        /// ⚠️ **2026-10-03 修复(评审 C8/ID)**:原为 `int`,与
        /// `entities.yaml:2068` 登记的 `structure_id: i64` **类型不符**,
        /// 也与契约侧 `Sim.Contracts.StructurePlacedPayload.StructureId`(**long**)
        /// 及 codec(`PayloadCodec.World.cs:269` `WriteFieldInt64`)**不一致** ——
        /// 同一 id 在两处不同宽度,是**静默截断**风险(`int` 溢出即失真,
        /// 且与 `ItemInstanceId` 同模式的「计数器 + 高水位可重构」口径不匹配)。
        /// 现全链升 `long`,与契约 / codec / ADR-010 §五 的 `IIdAuthority` 机制 A 对齐。
        /// </summary>
        public readonly long StructureId;
        public readonly WorldPos Anchor;
        public readonly int ModuleId;
        public readonly int Orientation;
        public readonly int Variant;
        public readonly IReadOnlyList<WorldPos> OccupiedCells;

        public StructureInstance(long structureId, WorldPos anchor, int moduleId, int orientation, int variant, IReadOnlyList<WorldPos> occupiedCells = null)
        {
            StructureId = structureId;
            Anchor = anchor;
            ModuleId = moduleId;
            Orientation = orientation;
            Variant = variant;
            OccupiedCells = occupiedCells ?? new List<WorldPos> { anchor };
        }
    }

    /// <summary>结构实例表(派生态,不存事件流,由 Structure* 事件重建)。</summary>
    public sealed class StructureInstanceRegistry
    {
        private readonly Dictionary<long, StructureInstance> _instances = new Dictionary<long, StructureInstance>();
        private long _nextStructureId = 1;

        /// <summary>
        /// 注册新实例(返回新 id)。
        /// </summary>
        /// <param name="occupiedCells">
        /// **完整占用格集**(F-23-2b 整数旋转后的世界格)。
        /// ⚠️ **2026-10-03 修复(评审 C1)**:原实现自造 `new List&lt;WorldPos&gt; { anchor }`
        /// (注释自陈「简化:仅锚点格」)⇒ **多格模块的非锚点足迹格不进实例表**,
        /// 使 `DemolishChecker` 的逐格实体检查**只查锚点** ⇒ **抵消 B2 的修复**。
        /// 现由调用方传入**真足迹**(源 = `PlaceableChecker.ComputeOccupiedCells`,
        /// 即放置判定所用的同一函数 —— 两处自此同源)。
        /// </param>
        public long Register(WorldPos anchor, int moduleId, int orientation, int variant,
                             IReadOnlyList<WorldPos> occupiedCells = null)
        {
            long id = _nextStructureId++;
            // ⚠️ **2026-10-03(N-r1)**:兜底仍 = `{anchor}`,但**生产路径已 fail-closed**
            //    (`StructureWriter.Place` 无目录即抛)⇒ 该兜底**只服务既有测试的直调**。
            //    新调用方**须传真足迹**;若生产代码出现直调 `Register` 而不传足迹,
            //    须在评审中判为 C1 复现。
            var cells = occupiedCells ?? new List<WorldPos> { anchor };
            _instances[id] = new StructureInstance(id, anchor, moduleId, orientation, variant, cells);
            return id;
        }

        /// <summary>移除实例(返回 true 如果存在并移除)。</summary>
        public bool Remove(long structureId)
        {
            return _instances.Remove(structureId);
        }

        /// <summary>更新实例(朝向/变体)。</summary>
        /// <param name="newOccupiedCells">
        /// 旋转后的**新足迹**(可 null)。
        /// <para>⚠️ <b>2026-10-28(S20-18)新增</b>:原签名只更新朝向/变体字段,足迹**沿用旧值**。
        /// 而足迹随朝向变(`PlaceableChecker.ComputeOccupiedCells` 吃 orientation)⇒
        /// 旋转非方形模块后注册表里记的是**旧足迹**,与占用表、与 24 的布局析出全部错位。
        /// 现由调用方(唯一生产调用者 = <see cref="StructureWriter.Modify"/>)传入重算后的真足迹;
        /// 传 null = 足迹不变(仅变体位变化时的合法情形,也保住既有直调方的原行为)。</para>
        /// </param>
        public bool Update(long structureId, int? newOrientation, int? newVariant,
                           IReadOnlyList<WorldPos> newOccupiedCells = null)
        {
            if (!_instances.TryGetValue(structureId, out var inst))
                return false;

            int orientation = newOrientation ?? inst.Orientation;
            int variant = newVariant ?? inst.Variant;

            _instances[structureId] = new StructureInstance(
                inst.StructureId, inst.Anchor, inst.ModuleId, orientation, variant,
                newOccupiedCells ?? inst.OccupiedCells);
            return true;
        }

        /// <summary>查询实例。</summary>
        public bool TryGet(long structureId, out StructureInstance instance)
        {
            return _instances.TryGetValue(structureId, out instance);
        }

        /// <summary>获取锚点格上的实例 id(无则返回 -1)。</summary>
        public long GetInstanceAt(WorldPos anchor)
        {
            foreach (var kv in _instances)
            {
                if (kv.Value.Anchor.X == anchor.X &&
                    kv.Value.Anchor.Y == anchor.Y &&
                    kv.Value.Anchor.Z == anchor.Z)
                    return kv.Key;
            }
            return -1;
        }

        /// <summary>获取实例总数。</summary>
        public int Count => _instances.Count;

        /// <summary>
        /// 只读枚举全部实例(**无写面**)—— 供 24 的只读结构视图
        /// (<c>IPlacedStructureReadView</c>)的生产实现遍历注册表。
        /// <para>🔁 **2026-10-21 新增(`TD-building-01` 读侧)**:此前本类**无公开枚举器**
        /// (只有 `TryGet(id)` / `GetInstanceAt(anchor)` / `Count` / `Clear`)⇒ 24 在「不改 23」
        /// 的前提下**无法**遍历它,只能靠测试替身。该事实逐字记在
        /// `Sim/Clinic/IPlacedStructureReadView.cs` 的文件头(陷阱 ①)。</para>
        /// <para>⚠️ <b>只读承诺靠类型守</b>:本方法返回 <see cref="IEnumerable{T}"/> 而非
        /// 可变集合,且 <see cref="StructureInstance"/> 是 <c>readonly struct</c>
        /// ⇒ 调用方拿不到任何写回注册表的路径(与 24「只读不持占用真相」同构)。</para>
        /// <para>⚠️ <b>枚举序不作契约</b>:内部是 `Dictionary` 桶序。需要确定性序的调用方
        /// **必须自己排序**(24 侧的析出已在 `ClinicRoomExtraction.ExtractFromMembership`
        /// 做规范排序,不依赖本枚举序)。</para>
        /// </summary>
        public IEnumerable<StructureInstance> EnumerateInstances()
        {
            foreach (KeyValuePair<long, StructureInstance> kv in _instances)
                yield return kv.Value;
        }

        /// <summary>
        /// 清空实例表(**不复位发号计数器**)。
        /// <para>⚠️ <b>2026-10-28(S20-18):读档重建请用 <see cref="Reset"/>,不要用本方法</b> ——
        /// 本方法只清 `_instances`,发号计数器保持原值。两者混用会让重放后的第一条新结构
        /// 拿到「旧水位 + 1」的 id(存档里可能已存在该 id)。保留本方法是为「清表但续号」
        /// 这一独立语义(现有零生产调用点,见类注),不是重建入口。</para>
        /// </summary>
        public void Clear()
        {
            _instances.Clear();
        }

        /// <summary>
        /// 清空实例表 **并复位发号计数器**(读档重建入口)。S20-18 新增。
        /// <para><b>为什么必须复位</b>:重放按事件原号落库(<see cref="Restore"/> 会重新抬高高水位),
        /// 若计数器停在旧水位,重放后再 <see cref="Register"/> 会从旧水位续号 —— 而重放出来的
        /// 实例可能恰好占到那之间 ⇒ 号段不连续但**不撞号**,语义上却把「高水位 = max(id)+1」
        /// 这条可重构不变量(ADR-006 Amendment B 同型)破了。</para>
        /// <para>⚠️ 本方法<b>只清注册表</b> —— 世界占用表(`World._slotOccupied`)由
        /// <see cref="World.Reset"/> 一并清,二者的唯一调用者是
        /// <see cref="StructureRestorer"/> 的重放入口。</para>
        /// </summary>
        public void Reset()
        {
            _instances.Clear();
            _nextStructureId = 1;
        }

        /// <summary>
        /// 下一个可用 structure_id(不递增,仅预览)。
        /// <para>⚠️ 这是**唯一**发号计数器 —— `StructureWriter.PeekNextId` 委托到这里
        /// (2026-10-28 S20-18 评审:写者侧原有一份镜像,与本文脱钩后双向静默出错)。</para>
        /// </summary>
        public long PeekNextId => _nextStructureId;

        /// <summary>
        /// 按**事件原号**落库一条实例(世界流重放专用)。S20-18 新增。
        /// <para>与 <see cref="Register"/> 的分工:<c>Register</c> 是<b>写路径</b>(自己发号);
        /// 本方法是<b>重建路径</b>(号从 `StructurePlaced` 载荷里来)。重放**不得**用
        /// `Register` —— 那会重新发号,读档后的 structure id 就与存档里的事件、以及
        /// 下游可能已引用的 id 全部错位。</para>
        /// <para>同号重复落库 = 覆盖(流的写入侧不会产生同号两条 `StructurePlaced`,
        /// 真出现说明流已损坏;此处覆盖而非抛,与 <see cref="Update"/> 的既有语义一致)。</para>
        /// </summary>
        /// <param name="structureId">事件载荷里的 `structure_id`(必须 &gt; 0)。</param>
        /// <param name="occupiedCells">完整占用格集。**不接受 null** —— 重放必须带真足迹,
        /// 静默退回 `{anchor}` 会复现 C1(多格模块的非锚点足迹格不进实例表)。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="structureId"/> ≤ 0。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="occupiedCells"/> 为 null。</exception>
        public void Restore(long structureId, WorldPos anchor, int moduleId, int orientation, int variant,
                            IReadOnlyList<WorldPos> occupiedCells)
        {
            if (structureId <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(structureId), structureId,
                    "重放的 structure_id 必须 > 0 —— ≤ 0 表示载荷损坏或解码错位,"
                    + "静默落库会让 id 空间出现非法值(与 `PatientId.None = -1` 哨兵混淆)。");
            if (occupiedCells == null)
                throw new ArgumentNullException(
                    nameof(occupiedCells),
                    "重放必须带真足迹 —— 静默退回 {anchor} 会复现 C1(多格模块的非锚点格不进实例表)。");

            _instances[structureId] =
                new StructureInstance(structureId, anchor, moduleId, orientation, variant, occupiedCells);

            // 高水位 = max(id) + 1(ADR-006 Amendment B 同型的「计数器 + 高水位可重构」)。
            if (structureId >= _nextStructureId)
                _nextStructureId = structureId + 1;
        }
    }

    #endregion

    #region 结构写者

    /// <summary>结构世界流事件写者(主机唯一)。</summary>
    public sealed class StructureWriter
    {
        private readonly IEventSink _eventSink;
        private readonly StructureInstanceRegistry _registry;
        private readonly IPayloadEncoder _encoder;
        private readonly IModuleCatalog _moduleCatalog;
        private readonly IWorldOccupancy _occupancy;
        private readonly DropEventWriter _dropWriter;

        /// <param name="eventSink">事件写入通道(主机唯一)。</param>
        /// <param name="registry">结构实例表。</param>
        /// <param name="encoder">
        /// 载荷编码器(ADR-029 §③)—— **唯一合法的载荷构造路径**。
        /// 本类内不得出现 <c>new PayloadRef(</c>(ADR-029 §③ 的门判据)。
        /// </param>
        /// <param name="moduleCatalog">
        /// 模块目录 —— 用于取 `LocalOccupancy` 算**真足迹**(C1),
        /// 以及拆除时取 `Cost`(算返还量)与 `BuildItemKey`(返还算哪个物品)。
        /// </param>
        /// <param name="occupancy">
        /// 世界占用表写面 —— **放置/拆除须同步 Overlay**(C2)。
        /// 可传 null 表示「不接线」(向后兼容;但 F-23-1 对已放置结构将不生效)。
        /// </param>
        /// <param name="dropWriter">
        /// 20 的三支掉落写者(2026-10-27 R17 闭合批新增)—— 拆除返还物由
        /// **23 触发、20 落地**(`entities.yaml` `DropSpawned.author`:落地写者 = 20)。
        /// <para>⚠️ <b>必填,构造期 fail-closed(N-r1 同口径)</b>:<c>null</c> ⇒ 构造抛。
        /// 若做成可选 + 默认 null,「忘了接线」会退化成**拆除静默不返还** —— 那正是
        /// 2026-10-03 评审 C1/C2 判过的形态(C1 = 占用集退化、C2 = Overlay 不写),
        /// 且当时已有测试把退化路径断言为正确行为。本批不再重犯:不可省的东西不进默认值。</para>
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="eventSink"/> /
        /// <paramref name="registry"/> / <paramref name="encoder"/> /
        /// <paramref name="dropWriter"/> 任一为 null。</exception>
        public StructureWriter(IEventSink eventSink, StructureInstanceRegistry registry,
                               IPayloadEncoder encoder,
                               IModuleCatalog moduleCatalog = null,
                               IWorldOccupancy occupancy = null,
                               DropEventWriter dropWriter = null)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _moduleCatalog = moduleCatalog;
            _occupancy = occupancy;
            _dropWriter = dropWriter ?? throw new ArgumentNullException(nameof(dropWriter),
                "未注入 DropEventWriter —— 拆除返还物由 23 触发、20 落地(DropSpawned),"
                + "静默不返还 = 吞玩家材料,且与 AC-23-16「不静默吞返还」直接冲突。"
                + "确实不需要返还的测试请显式传一个带 CapturingSink 的真写者(可 grep 的声明)。");
        }

        /// <summary>下一个可用 structure_id(不递增,仅预览)。</summary>
        /// <remarks>
        /// 🔁 **2026-10-28(S20-18 评审)**:原实现读本类自己的 `_nextStructureId` 镜像字段,
        /// 而**真正发号的是注册表**(<see cref="StructureInstanceRegistry.Register"/>)。
        /// 两个计数器会在两条路径上脱钩,且都是**静默**:
        /// ① `World.ApplyBakedInitial` 在 `StructureWriter` 构造**之前**跑
        /// (`CompositionRoot.cs:917` vs `:941`)⇒ 基线把注册表推到 `N+1`,而本类镜像仍是 `1`;
        /// ② `World.Reset()` 把注册表复位到 `1`,而本类镜像**停在旧高水位**。
        /// 两个方向都让 `PeekNextId` 报一个**不会真的发出**的号。
        /// ⇒ 现直接委托注册表(唯一发号者),镜像字段删除。
        /// </remarks>
        public long PeekNextId => _registry.PeekNextId;

        /// <summary>放置模块(Append StructurePlaced 事件)。</summary>
        public long Place(WorldPos anchor, int moduleId, int orientation, int variant, long tick)
        {
            // ── C1:真足迹(与放置判定同源)─────────────────────────────
            // ⚠️ **2026-10-03 fail-closed(第二轮评审 N-r1)**:
            //    初版把 `moduleCatalog` / `occupancy` 设为**可选**(默认 null)以求向后兼容 ——
            //    结果 `moduleCatalog == null` 时 `occupied` 保持 null ⇒ `Register` 兜底 `{anchor}`
            //    (**C1 复现**)且 `OccupyCells` 不调(**C2 复现**),**全程静默**。
            //    更糟:`test_c2_noCatalog_noOccupancy_noThrow` 把该退化路径**断言为正确行为**。
            //    ⇒ 现改为 **fail-closed**:
            //      · 未注入 `moduleCatalog` ⇒ **抛**(结构写者无目录即不可用);
            //      · `moduleId` 不在目录 ⇒ **抛**(不许拿 `{anchor}` 冒充真足迹)。
            if (_moduleCatalog == null)
                throw new InvalidOperationException(
                    "StructureWriter 未注入 IModuleCatalog —— 无法算真足迹(F-23-2b)。" +
                    "静默兜底 {{anchor}} 会复现 C1(占用集退化)与 C2(Overlay 不写),故 fail-closed。");

            var defOpt = _moduleCatalog.GetModuleDefinition(moduleId);
            if (!defOpt.HasValue)
                throw new InvalidOperationException(
                    $"moduleId={moduleId} 不在模块目录内 —— 无法算真足迹。" +
                    "静默兜底 {{anchor}} 会复现 C1/C2,故 fail-closed。");

            IReadOnlyList<WorldPos> occupied =
                PlaceableChecker.ComputeOccupiedCells(defOpt.Value, anchor, orientation);

            long structureId = _registry.Register(anchor, moduleId, orientation, variant, occupied);

            // ── C2:同步世界占用表(F-23-1 EffectiveWalkable 的写路径)──────
            // ⚠️ **fail-closed(N-r1)**:未注入 `occupancy` ⇒ 抛。
            //    静默跳过会让 F-23-1 对已放置结构**不生效**(C2),且**无任何信号**。
            if (_occupancy == null)
                throw new InvalidOperationException(
                    "StructureWriter 未注入 IWorldOccupancy —— 放置不会写 Overlay ⇒ " +
                    "F-23-1 EffectiveWalkable 对已放置结构不生效(C2)。故 fail-closed。");
            _occupancy.OccupyCells(occupied);

            // ADR-029 §③:载荷经 IPayloadEncoder —— 五字段全载,零 bit-packing
            var payload = _encoder.Encode(EventKind.StructurePlaced,
                new StructurePlacedPayload(structureId, anchor, moduleId, orientation, variant));

            var evt = new SimEvent(tick, PatientId.None, -1 /* 未发号哨兵 O-1 */, EventKind.StructurePlaced, payload);
            _eventSink.Append(evt);

            return structureId;
        }

        /// <summary>拆除模块(Append <c>StructureRemoved</c>,并按 F-23-3 返还)。</summary>
        /// <param name="structureId">待拆结构(合法性由 <see cref="DemolishChecker.Check"/> 判 ——
        /// 本方法不重复判定,与既有分工一致)。</param>
        /// <param name="tick">拆除 tick。</param>
        /// <param name="demolishRefundRatio">返还率 `DEMOLISH_REFUND_RATIO`(**Q16.16 raw**;
        /// P0 恒 `65536` = 1.0 全额返还)。</param>
        /// <returns>拆除是否发生(结构不存在 ⇒ <c>false</c>,零事件)。</returns>
        /// <remarks>
        /// <para><b>返还链(2026-10-27 R17 闭合批 · 用户裁定「地面掉落」)</b>:
        /// 拆除后按 F-23-3 算 <c>Refund = ⌊cost(m) × R⌋</c>,经
        /// <see cref="DropEventWriter.RecordSpawned"/> 发一条**普通 <c>DropSpawned</c>** ——
        /// 锚点 = 被拆结构锚点格、<c>item_key</c> = <c>module.BuildItemKey</c>、
        /// <c>qty</c> = Refund、id 由 20 侧经 `IIdAuthority` 铸造。</para>
        /// <para>⚠️ <b>与 EC-23-11 原文的关系(收窄,已在 GDD 就地加注)</b>:原文把
        /// 「走 20 的标准入库路径」定为**成功分支**,仅「失败 = 掉落在地上」。P0 无 20 侧
        /// 结算路径(拾取/入库编排未落)⇒ 本批把**成功分支也走地面掉落**。语义上仍是
        /// 「返还物进世界、不吞」,变的是它落在哪 —— 玩家走过来捡即可。
        /// 这是**收窄不是重设计**:`DropSpawned` 一支事件覆盖两种分支,P1b 接上入库路径时
        /// 只需在成功分支改发 `DropClaimed`,事件形状不变。</para>
        /// <para>⚠️ <b>为什么返还不由 fold 兜底</b>:fold 只看得到流里**有**什么,
        /// 看不到「本该有而没有」的 <c>DropSpawned</c>(那要一份「应发清单」= 第二真源)。
        /// ⇒ AC-23-16 的「不静默吞返还」判据落在**本类的测试**(spy-sink 断事件存在),
        /// 不在 fold(`InventoryFold.cs` 的 `StructureRemoved` 分支已就地写明)。</para>
        /// <para>⚠️ <b>返还量为 0 或模块不可返还 ⇒ 不发事件</b>:<c>qty = 0</c> 的
        /// <c>DropSpawned</c> 是畸形载荷(`DropEventWriter.ValidateSpawned` 直接拒),
        /// 而 <c>refundable = false</c> 的消耗性家具本就无返还(EC-23-08)。
        /// 两种情形都**不是**「吞返还」—— 没有可返还之物。</para>
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="demolishRefundRatio"/>
        /// &lt; 0(Q16.16 raw 不得为负;0 = 零返还,合法)。</exception>
        public bool Remove(long structureId, long tick, int demolishRefundRatio)
        {
            if (demolishRefundRatio < 0)
                throw new ArgumentOutOfRangeException(nameof(demolishRefundRatio),
                    demolishRefundRatio, "返还率是 Q16.16 raw,不得为负(0 = 零返还,合法)");

            if (!_registry.TryGet(structureId, out var inst))
                return false;

            // ── C2:先释放占用格,再移除实例 ──────────────────────────
            if (_occupancy != null)
                _occupancy.FreeCells(inst.OccupiedCells);

            _registry.Remove(structureId);

            // ADR-029 §③:载荷经 IPayloadEncoder(三字段:structure_id / cell / module_id)
            var payload = _encoder.Encode(EventKind.StructureRemoved,
                new StructureRemovedPayload(structureId, inst.Anchor, inst.ModuleId));

            var evt = new SimEvent(tick, PatientId.None, -1 /* 未发号哨兵 O-1 */, EventKind.StructureRemoved, payload);
            _eventSink.Append(evt);

            // ── 返还(F-23-3)—— 在 StructureRemoved **之后**发 ──────────────
            //    ⚠️ 顺序有意义:拆除是**因**,返还是**果**。同 tick 相邻 Seq 由
            //    `EventStream` 按写入序自发(O-1),故「先 Removed 后 DropSpawned」
            //    在流里是稳定可读的因果序,不靠 tick 区分。
            EmitRefund(inst, tick, demolishRefundRatio);

            return true;
        }

        /// <summary>按 F-23-3 发返还 <c>DropSpawned</c>(`Remove` 的尾部;零返还时静默不发)。</summary>
        private void EmitRefund(in StructureInstance inst, long tick, int demolishRefundRatio)
        {
            // 目录缺失 / 模块不在目录 ⇒ 无 cost 与 build_item_key 真源 ⇒ 不返还。
            // ⚠️ 这是**可观测**的静默:Place 侧对同一情形是 fail-closed 抛(无目录算不出足迹),
            //    拆除侧不能抛 —— 那会让「旧存档里已不存在的 module_id」把拆解整个炸掉
            //    (EC-23-07:目录漂移下结构**保留为不透明实例**,不是让世界停摆)。
            //    ⇒ 此处记 Debug 级不变量失败由既有「不透明实例」路径承担,本方法直接返回。
            if (_moduleCatalog == null) return;
            ModuleDefinition? defOpt = _moduleCatalog.GetModuleDefinition(inst.ModuleId);
            if (!defOpt.HasValue) return;

            ModuleDefinition def = defOpt.Value;

            // refundable = 0 ⇒ 消耗性家具,无返还(EC-23-08 的 `refundable = false` 档)
            if (def.Refundable == 0) return;

            int refund = RefundCalculator.CalculateRefund(def.Cost, demolishRefundRatio);
            if (refund <= 0) return;   // 零返还 ≠ 吞返还(qty=0 的实例不该存在)

            // ⚠️ **自有写者自铸**:本类持 `DropEventWriter`(它持 `IIdAuthority`),
            //    调 `RecordSpawned` 让 20 侧铸号 —— 23 不碰 `IIdAuthority`,
            //    避免出现第二个铸造点(承 S20-15 R-3 双铸造点冲突的订正)。
            _dropWriter.RecordSpawned(tick, inst.Anchor, def.BuildItemKey, refund);
        }

        /// <summary>修改模块(朝向/变体,Append StructureModified 事件)。</summary>
        public bool Modify(long structureId, int? newOrientation, int? newVariant, long tick)
        {
            if (!_registry.TryGet(structureId, out var inst))
                return false;

            // 计算 modified_fields 位掩码
            int modifiedFields = 0;
            if (newOrientation.HasValue && newOrientation.Value != inst.Orientation)
                modifiedFields |= 1; // 朝向位
            if (newVariant.HasValue && newVariant.Value != inst.Variant)
                modifiedFields |= 2; // 变体位

            if (modifiedFields == 0)
                return false; // 无变更

            int orientation = newOrientation ?? inst.Orientation;
            int variant = newVariant ?? inst.Variant;

            // ── S20-18(2026-10-28):足迹随朝向变,必须同步改占用表与实例足迹 ─────────
            // ⚠️ **原实现的缺陷(实测,2026-10-28)**:本方法只 `_registry.Update(...)`,
            //    全程**零** `FreeCells` / `OccupyCells`,且 `Update` 收的还是**旧足迹**
            //    ⇒ 旋转非方形模块后**旧格仍不可走、新格仍可走**(违 F-23-1),注册表里
            //    记的也仍是旧足迹。对照 `Place`(占 `occupied`)、`Remove`(释
            //    `inst.OccupiedCells`),只有本方法漏 ⇒ 是遗漏,不是设计。
            // ⚠️ `ModifiableChecker` 早已算出 `newCells` 并逐格校验「新格 ∖ 旧格 全空」,
            //    只是写路径**没用它** —— 本处与之同源(`PlaceableChecker.ComputeOccupiedCells`)。
            // ⚠️ **变体不参与足迹**(该函数的形参无 variant)⇒ 仅变体位变化时不动占用表,
            //    也不查目录(省一次装载面依赖)。
            IReadOnlyList<WorldPos> newCells = null;
            if ((modifiedFields & 1) != 0)
            {
                if (_moduleCatalog == null)
                    throw new InvalidOperationException(
                        "StructureWriter 未注入 IModuleCatalog —— 无法重算旋转后的足迹。" +
                        "静默沿用旧足迹会让占用表与实际朝向分叉(F-23-1),故 fail-closed(同 Place)。");

                var defOpt = _moduleCatalog.GetModuleDefinition(inst.ModuleId);
                if (!defOpt.HasValue)
                    throw new InvalidOperationException(
                        $"moduleId={inst.ModuleId} 不在模块目录内 —— 无法重算旋转后的足迹。" +
                        "静默沿用旧足迹会复现 F-23-1,故 fail-closed(同 Place)。");

                if (_occupancy == null)
                    throw new InvalidOperationException(
                        "StructureWriter 未注入 IWorldOccupancy —— 旋转不会改 Overlay ⇒ " +
                        "F-23-1 对旋转后的足迹不生效。故 fail-closed(同 Place)。");

                newCells = PlaceableChecker.ComputeOccupiedCells(defOpt.Value, inst.Anchor, orientation);

                // 先释旧、后占新:两集合可重叠(1×1 模块旋转前后同格),bool 表上幂等。
                _occupancy.FreeCells(inst.OccupiedCells);
                _occupancy.OccupyCells(newCells);
            }

            _registry.Update(structureId, newOrientation, newVariant, newCells);

            // ADR-029 §③:载荷经 IPayloadEncoder —— 六字段全载(含 ModifiedFields 位掩码)。
            // ⚠️ 2026-10-02 修复:原实现把 orientation / variant **算出却未放进载荷**
            //    (只写 offset: 0),两字段被静默丢弃。本行起真正编码。
            var payload = _encoder.Encode(EventKind.StructureModified,
                new StructureModifiedPayload(structureId, inst.Anchor, inst.ModuleId,
                                             orientation, variant, modifiedFields));

            var evt = new SimEvent(tick, PatientId.None, -1 /* 未发号哨兵 O-1 */, EventKind.StructureModified, payload);
            _eventSink.Append(evt);

            return true;
        }
    }

    #endregion
}
