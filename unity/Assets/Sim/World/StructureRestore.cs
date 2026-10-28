// 世界流 → World 的重放器(S20-18 · 2026-10-28)。
//
// 权威来源:
//   · `production/sprints/sprint-20.md` §十 S20-18(本件即该卡的实现面)
//   · ADR-009 §四(派生态:由事件流确定性重建,不持状态、不写流)
//   · ADR-015 §五(建造模块对齐整数格;足迹由模块定义 + 朝向决定)
//   · ADR-006 Amendment B 同型(「计数器 + 高水位可重构」—— `structure_id` 重放须保号)
//   · 同型先例:`PoiStateMachine.RebuildFromDecoded`(Sim/World/PoiStateMachine.cs:161)
//
// 为什么需要本件(实测,2026-10-28):
//   ① `World` 无重置入口(`StructureInstanceRegistry.Clear()` 只清 `_instances`,
//      不清 `World._slotOccupied`、不复位发号)⇒ 旧世界**没法销毁**;
//   ② 全仓**零**「世界流 → World」重放器(`StructurePlaced/Removed/Modified` 的读方
//      只有表现层只读查询)⇒ 就算清零,读档后世界会**变空**(房子的记忆全没了)。
//   两件合起来 = 「读档后旧世界还在」这件事**无法被正确处理**,只能被沉默。
//
// ⚠️ **本类不自行解码**(ADR-029 / ADR-031 的甲案口径,与 `PoiStateMachine` 一致):
//   载荷解码器 `PayloadCodec` 住 `Sim.Codec`,而本类住 `Sim`(够不着,且该引用边由
//   ADR-025 §①:111 禁止 + b2 门强制)。故 `RebuildFromDecoded` 收**已解码**的
//   `StructureRestoreOp` 序列 —— 与写侧对称:写侧交出已编码的 `PayloadRef`,
//   读侧收下已解码的字段。**Sim 侧零 codec 依赖。**
//
// ⚠️ **本类不写流**:纯重建,零 `IEventSink.Append`(ADR-009 §四;与
//   `InventoryLoadService` 的「零缓存、零增量、不写流」同纪律)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>重放操作的三支闭集(与 <c>EventKind</c> 的三支一一对应)。</summary>
    /// <remarks>
    /// ⚠️ 刻意写成具名枚举而非复用 `EventKind`:本类住 `Sim`,而 `EventKind` 是
    /// 路由白名单的**超集**(35 支);收窄到三支 = 让「重放器不认其它 Kind」成为
    /// **类型上不可表达**的事,而不是靠运行期 switch 的 default 分支守。
    /// </remarks>
    public enum StructureRestoreKind : int
    {
        /// <summary>`StructurePlaced`:新建实例 + 占足迹。</summary>
        Placed = 0,

        /// <summary>`StructureRemoved`:释足迹 + 删实例。</summary>
        Removed = 1,

        /// <summary>`StructureModified`:释旧足迹 + 占新足迹 + 改实例。</summary>
        Modified = 2,
    }

    /// <summary>
    /// 一条已解码的建造事件(重放输入)。
    /// <para>字段与 <c>StructurePlacedPayload</c> / <c>StructureRemovedPayload</c> /
    /// <c>StructureModifiedPayload</c> 的并集对齐 —— 三支共用一条结构,
    /// 未用字段在各支里是「无意义值」而不是「缺省」:</para>
    /// <list type="bullet">
    ///   <item><c>Placed</c>:`Orientation` / `Variant` 有效。</item>
    ///   <item><c>Removed</c>:只用 `StructureId`(`Anchor` / `ModuleId` 取自注册表的既有记录)。</item>
    ///   <item><c>Modified</c>:`Orientation` / `Variant` 是**绝对新值**(写侧
    ///   `StructureWriter.Modify` 把 `newOrientation ?? inst.Orientation` 解析后放进载荷 ⇒
    ///   载荷恒载绝对値,不依赖 `ModifiedFields` 位掩码)。</item>
    /// </list>
    /// </summary>
    public readonly struct StructureRestoreOp
    {
        /// <summary>操作支。</summary>
        public readonly StructureRestoreKind Kind;

        /// <summary>事件载荷里的 `structure_id`(`StructureRemoved` / `StructureModified` 须已存在)。</summary>
        public readonly long StructureId;

        /// <summary>放置锚点格(仅 `Placed` 使用)。</summary>
        public readonly WorldPos Anchor;

        /// <summary>模块 id(仅 `Placed` 使用)。</summary>
        public readonly int ModuleId;

        /// <summary>朝向(`Placed` = 放置朝向;`Modified` = 旋转后朝向)。</summary>
        public readonly int Orientation;

        /// <summary>变体(`Placed` = 放置变体;`Modified` = 改后变体)。</summary>
        public readonly int Variant;

        private StructureRestoreOp(StructureRestoreKind kind, long structureId, WorldPos anchor,
                                   int moduleId, int orientation, int variant)
        {
            Kind = kind;
            StructureId = structureId;
            Anchor = anchor;
            ModuleId = moduleId;
            Orientation = orientation;
            Variant = variant;
        }

        /// <summary>`StructurePlaced` → op。</summary>
        public static StructureRestoreOp Placed(long structureId, WorldPos anchor, int moduleId,
                                                int orientation, int variant)
            => new StructureRestoreOp(StructureRestoreKind.Placed, structureId, anchor,
                                      moduleId, orientation, variant);

        /// <summary>`StructureRemoved` → op(`Anchor` / `ModuleId` 会被重放器忽略)。</summary>
        public static StructureRestoreOp Removed(long structureId, WorldPos anchor = default, int moduleId = -1)
            => new StructureRestoreOp(StructureRestoreKind.Removed, structureId, anchor, moduleId, 0, 0);

        /// <summary>`StructureModified` → op(朝向 / 变体为**绝对新值**)。</summary>
        public static StructureRestoreOp Modified(long structureId, int newOrientation, int newVariant)
            => new StructureRestoreOp(StructureRestoreKind.Modified, structureId, default, -1,
                                      newOrientation, newVariant);
    }

    /// <summary>
    /// 把已解码的世界流事件重放进 <see cref="World"/> —— 读档重建的唯一入口。
    /// <para><b>重置与重放是**一个原子操作**</b>:<see cref="RebuildFromDecoded"/> 内部
    /// 先 <see cref="World.Reset"/>,无论成功失败,返回 / 抛出后世界都不处于「旧态叠加」
    /// 的半成品 —— 抛出时世界回到**重置后的空态**(见 `<exception>`)。</para>
    /// </summary>
    public sealed class StructureRestorer
    {
        private readonly World _world;
        private readonly IModuleCatalog _moduleCatalog;

        /// <summary>构造重放器。</summary>
        /// <param name="world">被重建的世界(非 null)。</param>
        /// <param name="moduleCatalog">
        /// 模块目录(非 null)—— 足迹必须由它与朝向**重算**,不能从流里读:
        /// `StructureRemoved` / `StructureModified` 载荷里没有完整足迹,
        /// 而 `StructurePlaced` 的足迹是模块定义的**纯函数**(F-23-2b)。
        /// </param>
        /// <exception cref="ArgumentNullException">任一形参为 null(fail-closed ——
        /// 空目录 ⇒ 无法算足迹 ⇒ 静默退化为 `{anchor}` 即 C1 复现)。</exception>
        public StructureRestorer(World world, IModuleCatalog moduleCatalog)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _moduleCatalog = moduleCatalog ?? throw new ArgumentNullException(
                nameof(moduleCatalog),
                "未注入 IModuleCatalog —— 无法算重放足迹;静默退化为 {anchor} 会复现 C1");
        }

        /// <summary>
        /// 按**事件全序**重放已解码的建造事件:先清空世界,再逐条落库。
        /// </summary>
        /// <param name="ops">
        /// 已解码的操作序列,**须按事件全序排列**(调用方保证 —— 生产 = 按 `stream.Events`
        /// 的原序过滤,见 `Gameplay.Boot/WorldLoadService`;本类不重排:
        /// 重排需要一个「全序键」的解释权,而那归 ADR-008 的 `(Tick, StreamPriority, Patient, Seq)`,
        /// 不该由重放器二次发明)。
        /// </param>
        /// <returns>实际应用的条数(<c>0</c> = 流里没有建造事件 ⇒ 世界保持**重置后的空态**)。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ops"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">
        /// `Placed` 的 `moduleId` 不在目录内 / `Removed` 或 `Modified` 指向**不存在的 id**。
        /// <para>⚠️ 抛出时世界已被**重新重置为空** —— 调用方看到异常即知「读档失败」,
        /// 不会拿到一个「重放了一半」的世界。这条是**原子性**的兑现方式:不预校验两遍
        /// (那要把整个重放先模拟一遍),而是「失败即回滚到空态」。</para>
        /// </exception>
        public int RebuildFromDecoded(IReadOnlyList<StructureRestoreOp> ops)
        {
            if (ops == null) throw new ArgumentNullException(nameof(ops));

            _world.Reset();

            try
            {
                int applied = 0;
                for (int i = 0; i < ops.Count; i++)
                {
                    Apply(ops[i], i);
                    applied++;
                }
                return applied;
            }
            catch
            {
                // 原子性:抛出后世界 = 重置后的空态,不留「重放了一半」的中间态。
                _world.Reset();
                throw;
            }
        }

        private void Apply(StructureRestoreOp op, int index)
        {
            switch (op.Kind)
            {
                case StructureRestoreKind.Placed:
                {
                    IReadOnlyList<WorldPos> cells = FootprintOf(op.ModuleId, op.Anchor, op.Orientation, index);

                    // ── 同号覆盖须先释放旧足迹(2026-10-28 评审 B-1 修复)─────────────
                    // ⚠️ **原文不删,就地加注**:初版直接 `Restore` + `OccupyCells`。
                    //    `Restore` 是**注册表操作,从不碰占用表** —— 它对同号是 `_instances[id] = …`
                    //    静默覆盖(该语义本身有据,见 `Registry.Restore` 的文档)。但**占用表没人管**:
                    //    被覆盖的那条实例的旧足迹**仍留在 `_slotOccupied` 里** ⇒ 那些格**永久不可走**,
                    //    而且**没有任何信号**。
                    // **可达路径(不是假想)**:基线占 `1..N`(`World.ApplyBakedInitial`),
                    //    而 baseline 之前的存档(schema 1)里玩家放置从 `1` 起号 ⇒ 重放第一条
                    //    `Placed(id=1)` 就撞上基线第 1 件 ⇒ 基线那件的足迹格永远阻塞。
                    // **为什么修在这里而不是 `Restore` 里**:`Restore` 只认注册表,不知道占用表存在
                    //    (注册表与占用表是两个对象,`World` 才是唯一知道两者的人都)。让 `Restore`
                    //    去释放占用会**反转依赖方向**;而本类是「把流落进世界」的唯一入口,
                    //    保持「实例表 ↔ 占用表」同步正是它的职责 —— 与 `Removed` / `Modified`
                    //    两支已经在做的事完全同构(那两支都先 `FreeCells` 再动)。
                    // ⚠️ **未结(不在本次修复面)**:schema-1 存档撞号后「基线家具被存档里的结构
                    //    顶替」这个**语义**问题,本修复只保证**足迹一致**,不裁定「谁该赢」。
                    //    见 `production/qa/evidence/review-s20-18-2026-10-28.md` §五。
                    if (_world.Structures.TryGet(op.StructureId, out StructureInstance existing))
                        _world.FreeCells(existing.OccupiedCells);

                    _world.Structures.Restore(op.StructureId, op.Anchor, op.ModuleId,
                                              op.Orientation, op.Variant, cells);
                    _world.OccupyCells(cells);
                    break;
                }

                case StructureRestoreKind.Removed:
                {
                    if (!_world.Structures.TryGet(op.StructureId, out StructureInstance inst))
                        throw new InvalidOperationException(
                            $"重放第 {index} 条 StructureRemoved:structureId={op.StructureId} 不在实例表内 —— "
                            + "流已损坏(拆除指向一个从未放置过的结构)或事件序错乱。"
                            + "静默跳过会让世界与流分叉,故 fail-closed。");

                    _world.FreeCells(inst.OccupiedCells);
                    _world.Structures.Remove(op.StructureId);
                    break;
                }

                case StructureRestoreKind.Modified:
                {
                    if (!_world.Structures.TryGet(op.StructureId, out StructureInstance inst))
                        throw new InvalidOperationException(
                            $"重放第 {index} 条 StructureModified:structureId={op.StructureId} 不在实例表内 —— "
                            + "流已损坏或事件序错乱。静默跳过会让世界与流分叉,故 fail-closed。");

                    // 锚点 / 模块取**注册表的既有记录**,不取载荷 —— 身份以已落库的实例为准。
                    IReadOnlyList<WorldPos> cells = FootprintOf(inst.ModuleId, inst.Anchor, op.Orientation, index);

                    _world.FreeCells(inst.OccupiedCells);
                    _world.OccupyCells(cells);
                    _world.Structures.Restore(op.StructureId, inst.Anchor, inst.ModuleId,
                                              op.Orientation, op.Variant, cells);
                    break;
                }

                default:
                    // 闭集外的值只可能来自错误解码;不静默吞。
                    throw new InvalidOperationException(
                        $"重放第 {index} 条:未知的 StructureRestoreKind={(int)op.Kind}。");
            }
        }

        /// <summary>
        /// 足迹 = `PlaceableChecker.ComputeOccupiedCells(模块定义, 锚点, 朝向)` ——
        /// 与放置判定 / 可改判定 / `StructureWriter.Place` **同一函数**(两处同源是 C1 的修复前提)。
        /// </summary>
        private IReadOnlyList<WorldPos> FootprintOf(int moduleId, WorldPos anchor, int orientation, int index)
        {
            ModuleDefinition? defOpt = _moduleCatalog.GetModuleDefinition(moduleId);
            if (!defOpt.HasValue)
                throw new InvalidOperationException(
                    $"重放第 {index} 条:moduleId={moduleId} 不在模块目录内 —— 无法算足迹。"
                    + "静默退化为 {anchor} 会复现 C1(多格模块的非锚点足迹格不进实例表),故 fail-closed。");

            return PlaceableChecker.ComputeOccupiedCells(defOpt.Value, anchor, orientation);
        }
    }
}
