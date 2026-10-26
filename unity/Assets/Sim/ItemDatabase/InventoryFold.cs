// AC-20-03 / AC-20-05 / AC-20-16 / AC-20-17 —— `InventoryOf` 的世界流折叠重建器(本体)。
//
// 权威来源:
//   design/gdd/inventory-and-items.md
//     规则一 —— `InventoryOf(player) = fold(世界流, 谓词 ∈ {DropSpawned, DropClaimed,
//       DropDespawned, StructureRemoved, Craft.ActualConsumed, StructurePlaced, ResourceHarvested})`
//     规则一 —— `InventoryOf` = **派生**(读时计算);内存索引 = **优化非真相**;
//       索引与 fold 冲突 ⇒ **fold 赢**(ADR-009 §二 / ADR-010)
//     规则二 —— 全序键 `(Tick, StreamPriority, Patient, Seq)`、机造点、高水位
//     规则三 —— 堆叠键 `(item_key, quality)`
//     规则五 —— 20 **不求值**:只搬运上游算好的整数(AC-20-05 的出处)
//     §涟漪登记 **R15 / R16 / R17 / R18**(2026-10-26 实现轮登记)—— 四处「载荷位本身不存在」
//   docs/architecture/adr-009-world-state-event-boundary.md §一 / §二(三态分类 · 流优先级)
//   docs/architecture/adr-005-deterministic-sim.md —— 事件流唯一真源;主机唯一 Append
//   docs/architecture/adr-031-payload-decoder-abstraction.md §①/§② —— Sim 侧读载荷的唯一合法路径
//   production/epics/inventory-items/story-001-inventoryof-fold-rebuild.md
//
// ── 交付边界(卡面 §4 + 用户 2026-10-26 裁定「先登记欠账,再写本体」)──────────────
// 卡面原设想 = 「为 R2 / R3 未定稿字段构造**合法占位**跑通」。实现轮前置核实发现
// **四处载荷位根本不存在**(R15 / R16 / R17 / R18)—— 对这些位「占位」= 发明一个生产端
// 永不发出的字段 ⇒ 跑通的只是夹具,不是链路。
// ⇒ 本件的判据不是「能跑通」,而是:
//   · **可重建子域**(`Drop*` 三支 + `ResourceHarvested` 品级绑定 + `Craft` 扣料侧)
//     逐位等 —— 真跑,真断言;
//   · **缺载体路径一律进 `Gaps`**(不静默跳过、不造占位)
//     ⇒「缺载体 ⇒ 拒绝出完整投影」本身是可判伪的正向判据(`IsComplete` 二值)。
//
// ⚠️ **`IsComplete == false` 时 `Projection` 是「部分应用」的中间态,不得当作 `InventoryOf`**;
//    唯一出口 = `RequireComplete()`(不完整即抛)。
//
// ⚠️ **本文件零 `UnityEngine`、零 `float`、零求值**(门 A / AC-20-05):
//    · 堆叠键用 **物品表 ordinal**(载荷里本来就是 int),不做 ordinal → `ItemKey` 解析
//      ⇒ 不需要 `ItemDataSet`,也不引入「读物品表」这条非流来源;
//    · 品级**只从载荷抄**(`ResourceHarvested.out_quality`),**永不计算**(那是 21a 的活);
//    · 建造扣减量 `cost(m)` 在 R16 闭合前**拿不到归属** ⇒ 本件不注入 `IModuleCatalog`。
//
// ⚠️ **单一 fold 域 = 世界流**(ADR-009 §一 / OQ-20-1):本类**只**消费
//    `StreamRouting.Of(kind) == StreamId.World` 的 Kind。病史流 / 病例流条目**不得**入 fold ——
//    这条由测试的双向夹具守,而不是靠本注释。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>投影里的一行 —— 一个物品实例的当前可见态(规则三的堆叠键已在此展开)。</summary>
    public readonly struct InventoryRow
    {
        /// <summary>实例 id(<c>IIdAuthority</c> 发放;ADR-006 机制 A 号空间)。</summary>
        public readonly long InstanceId;

        /// <summary>物品表 ordinal(= 载荷 `item_key`,D-21-13 的整数枚举形)。</summary>
        public readonly int ItemOrdinal;

        /// <summary>品级档(从 `ResourceHarvested.out_quality` 抄入,**非求值**)。</summary>
        public readonly int Quality;

        /// <summary>当前数量(&le; 0 的实例不出现在投影里)。</summary>
        public readonly int Qty;

        /// <summary>持有者 actor id;<see cref="InventoryProjection.NoOwner"/> = 在地上 / 未归属。</summary>
        public readonly int Owner;

        /// <summary>构造一行(投影专用)。</summary>
        public InventoryRow(long instanceId, int itemOrdinal, int quality, int qty, int owner)
        {
            InstanceId = instanceId; ItemOrdinal = itemOrdinal;
            Quality = quality; Qty = qty; Owner = owner;
        }

        /// <summary>堆叠键 = <c>(item_key, quality)</c>(规则三的整数形)。</summary>
        public long StackKey => ((long)ItemOrdinal << 32) | (uint)Quality;
    }

    /// <summary>缺口类别 —— 前四类**逐条**对应 §涟漪登记的一个 R 行;其余是完整性不变量。</summary>
    public enum InventoryFoldGapKind
    {
        /// <summary>**R15** —— `Craft` 的产出物无法确定 `item_key`(载荷无 output item_key 位;
        /// 且 `recipe_id` 在全仓无 <c>int → Recipe</c> 映射)。⇒ 增益侧建不出 `(item_key, quality)`。</summary>
        CraftOutputWithoutItemKey = 0,

        /// <summary>**R16** —— `StructurePlaced` 无 actor 位 ⇒ 建造扣料记不到人。</summary>
        StructurePlacementWithoutActor = 1,

        /// <summary>**R17** —— `StructureRemoved` 返还物既无 <c>instance_id</c> 也无 `item_key`。</summary>
        StructureRemovalWithoutRefundIdentity = 2,

        /// <summary>**R18** —— 某实例在整个流里**没有任何品级来源**
        /// (无 `ResourceHarvested.out_quality`,也非 `Craft` 产出)。</summary>
        InstanceWithoutQualitySource = 3,

        /// <summary>**实例 id 复用** —— `DropSpawned` 撞上一个已存在且未归零的 id
        /// (违 ADR-006 机制 A 不变量②:号不回收)。</summary>
        DuplicateInstanceId = 4,

        /// <summary>**引用未出生的实例** —— `DropClaimed` / `DropDespawned` / `Craft` 投料
        /// 点名了流里从未出生(或已归零)的 id;或 `ResourceHarvested` 有而
        /// `DropSpawned` 缺失。世界流不完整 = 投影不可信。</summary>
        DanglingInstanceReference = 5,

        /// <summary>**重复归属** —— 同一实例被 `DropClaimed` 两次(转移须走
        /// `DropSpawned → DropClaimed` 新序列,不是二次 claim)。</summary>
        DuplicateClaim = 6,

        /// <summary>**非法扣减量** —— `DropDespawned.qty ≤ 0` 或 &gt; 该实例当前 qty
        /// (后者属畸形输入:主机应整体拒绝、世界流零事件)。</summary>
        InvalidDespawnQuantity = 7,

        /// <summary>**扣减超出持有** —— `Craft.ActualConsumed[i]` 越界,或投料实例的持有者
        /// ≠ `Craft.actor_id`(投料只能扣自己箱里的)。</summary>
        ConsumptionExceedsHolding = 8,

        /// <summary>**品级来源互相矛盾** —— 同一实例被两处给出不同 `quality`。</summary>
        ConflictingQualitySource = 9,

        /// <summary>**载荷组内不等长** —— `Craft` 的 `InputInstanceIds ⟷ ActualConsumed`
        /// 或 `OutputQty ⟷ OutputQuality ⟷ OutputInstanceIds` 长度不一致(registry 约束)。
        /// <para>⚠️ **纵深防御,非生产可达**:`PayloadCodec.World.cs:200-208` 在**写侧**即抛
        /// (「坏数据不进字节面」)⇒ 合规 codec 写出的流到不了本分支。留着是为读侧收到
        /// 别端写坏的数据 / 生产池实现变更时**不静默错算**;测试侧亦无法用真 codec 构造
        /// 该形态 ⇒ 该分支**未被夹具钉住**(Proven = ✗,登记在 story 的 Test Evidence)。</para></summary>
        MalformedPayloadPairing = 10,
    }

    /// <summary>一条缺口 —— 带**可追溯的定位**(R 编号 + Kind + 全序键 + 实例 id)。</summary>
    public readonly struct InventoryFoldGap
    {
        /// <summary>「无具体事件」的全序键哨兵(Tick 0 / 世界流 / 无病人 / 未发号)。</summary>
        public static readonly EventOrderKey NoEvent = new EventOrderKey(0L, StreamId.World, PatientId.None, -1L);

        /// <summary>缺口类别。</summary>
        public readonly InventoryFoldGapKind Kind;

        /// <summary>触发缺口的 <see cref="EventKind"/>(完整性类缺口同样点名 offending 事件)。</summary>
        public readonly EventKind EventKind;

        /// <summary>触发事件的跨流全序键(定位到具体一条);无具体事件时 = <see cref="NoEvent"/>。</summary>
        public readonly EventOrderKey At;

        /// <summary>涉及的实例 id;<c>0</c> 表示该缺口不针对单一实例。</summary>
        public readonly long InstanceId;

        /// <summary>人类可读说明(含 R 编号 —— 便于把缺口映射回 §涟漪登记)。</summary>
        public readonly string Message;

        /// <summary>构造一条缺口。</summary>
        public InventoryFoldGap(InventoryFoldGapKind kind, EventKind eventKind, EventOrderKey at,
            long instanceId, string message)
        {
            Kind = kind; EventKind = eventKind; At = at; InstanceId = instanceId; Message = message;
        }

        /// <summary>该缺口对应的涟漪登记编号(无对应 R 行的完整性判据返回空串)。</summary>
        public string RegistryId
        {
            get
            {
                switch (Kind)
                {
                    case InventoryFoldGapKind.CraftOutputWithoutItemKey: return "R15";
                    case InventoryFoldGapKind.StructurePlacementWithoutActor: return "R16";
                    case InventoryFoldGapKind.StructureRemovalWithoutRefundIdentity: return "R17";
                    case InventoryFoldGapKind.InstanceWithoutQualitySource: return "R18";
                    default: return string.Empty;
                }
            }
        }

        /// <inheritdoc />
        public override string ToString() =>
            "[" + Kind + (RegistryId.Length > 0 ? " / " + RegistryId : string.Empty) + "] " +
            EventKind + " @ (tick " + At.Tick + ", seq " + At.Seq + ") instance=" + InstanceId +
            ": " + Message;
    }

    /// <summary>
    /// `InventoryOf` 的投影 —— 实例表 + 按持有者派生视图(规则一)。
    /// <para><b>只读</b>:消费者不得回写(fold 的唯一写入口是事件流)。</para>
    /// <para><b>非真源</b>:本型是**派生态**(ADR-009 §一)。与 7a 快照冲突时**以流为准** ——
    /// 故本类**不提供**「用快照覆盖」的任何入口(那会是第二真源)。</para>
    /// </summary>
    public sealed class InventoryProjection
    {
        /// <summary>无持有者哨兵 —— 实例在地上 / 尚未被 `DropClaimed`。</summary>
        public const int NoOwner = -1;

        private readonly Dictionary<long, InventoryRow> _byInstance;
        private readonly Dictionary<int, List<long>> _byOwner;

        internal InventoryProjection(Dictionary<long, InventoryRow> byInstance,
            Dictionary<int, List<long>> byOwner)
        {
            _byInstance = byInstance;
            _byOwner = byOwner;
        }

        /// <summary>实例总数(qty &gt; 0 的行数)。</summary>
        public int InstanceCount => _byInstance.Count;

        /// <summary>按实例 id 查行。</summary>
        public bool TryGet(long instanceId, out InventoryRow row) => _byInstance.TryGetValue(instanceId, out row);

        /// <summary>某持有者名下的实例 id 集(**按 `instance_id` 升序** —— 换页 / 摆放序都读这一序)。
        /// 无实例时返回空数组(非 null)。</summary>
        public IReadOnlyList<long> InstancesOf(int owner)
            => _byOwner.TryGetValue(owner, out List<long> list)
                ? (IReadOnlyList<long>)list
                : Array.Empty<long>();

        /// <summary>全部实例 id(含未归属行 —— <see cref="NoOwner"/>)。
        /// ⚠️ 顺序**不保证**(字典键序);要稳定序请走 <see cref="InstancesOf"/> 或
        /// <see cref="CanonicalForm"/>。</summary>
        public IEnumerable<long> AllInstanceIds => _byInstance.Keys;

        /// <summary>投影的**规范串形** —— 供逐位比对 / 日志 / 断言的稳定表示。
        /// 行按 `instance_id` 升序,每行 <c>id:ordinal:quality:qty:owner</c>,以 <c>;</c> 分隔。
        /// 空投影 = 空串。</summary>
        public string CanonicalForm()
        {
            var ids = new List<long>(_byInstance.Keys);
            ids.Sort();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0) sb.Append(';');
                InventoryRow r = _byInstance[ids[i]];
                sb.Append(r.InstanceId).Append(':').Append(r.ItemOrdinal).Append(':')
                  .Append(r.Quality).Append(':').Append(r.Qty).Append(':').Append(r.Owner);
            }
            return sb.ToString();
        }
    }

    /// <summary>fold 结果 —— 投影 + 缺口清单 + **二值完整性判据**。</summary>
    public sealed class InventoryFoldResult
    {
        /// <summary>投影(<see cref="IsComplete"/> 为 false 时是**部分应用态**,不可当 `InventoryOf`)。</summary>
        public InventoryProjection Projection { get; }

        /// <summary>缺口清单(空 = 可完整重建)。**顺序 = 发现序**,同一流恒定。</summary>
        public IReadOnlyList<InventoryFoldGap> Gaps { get; }

        /// <summary>是否完整重建(`Gaps.Count == 0`)。AC-20-03 只在为 true 时成立。</summary>
        public bool IsComplete => Gaps.Count == 0;

        internal InventoryFoldResult(InventoryProjection projection, IReadOnlyList<InventoryFoldGap> gaps)
        {
            Projection = projection;
            Gaps = gaps;
        }

        /// <summary>要求完整 —— 不完整则抛,消息含**全部**缺口(便于一次修完,不做逐条试错)。</summary>
        /// <exception cref="InvalidOperationException"><see cref="IsComplete"/> 为 false。</exception>
        public InventoryProjection RequireComplete()
        {
            if (IsComplete) return Projection;
            throw new InvalidOperationException(
                "InventoryOf 逐位重建未成立:" + Gaps.Count + " 处缺口。缺载体 ⇒ 投影不可信," +
                "拒绝返回(禁静默降级)。逐条:\n  " + string.Join("\n  ", Gaps));
        }
    }

    /// <summary>
    /// `InventoryOf` 的折叠重建器本体(规则一)。
    /// <para><b>纯函数</b>:输入 = 事件流 + 解码器,输出 = 投影;零第三来源
    /// (不读物品表、不读快照、不读墙钟、不读表现态位置)。</para>
    /// <para><b>全序</b>:按 <c>(Tick, StreamPriority, Patient, Seq)</c> 升序应用
    /// (<see cref="EventOrderKey"/>);键完全相等时按**输入列表下标**升序 —— 显式 tiebreak,
    /// 不依赖 <c>List.Sort</c> 的不稳定性。⇒ 传入顺序任意,结果相同(排序在 fold 内,
    /// 不是调用方义务)。</para>
    /// </summary>
    /// <remarks>
    /// <para><b>七类 Kind 的转移**(规则一表):</para>
    /// <list type="table">
    /// <item><term><see cref="EventKind.DropSpawned"/></term>
    ///       <description>建条目(`item_key` + `qty`);owner = <see cref="InventoryProjection.NoOwner"/>。</description></item>
    /// <item><term><see cref="EventKind.DropClaimed"/></term>
    ///       <description>迁 owner(只改归属,不改 id / 不改 qty)。</description></item>
    /// <item><term><see cref="EventKind.DropDespawned"/></term>
    ///       <description>扣 `qty`;归零即出投影。</description></item>
    /// <item><term><see cref="EventKind.ResourceHarvested"/></term>
    ///       <description>绑 `quality`(**预扫一遍** ⇒ 与出生事件的先后无关)。</description></item>
    /// <item><term><see cref="EventKind.Craft"/></term>
    ///       <description>扣料 = `InputInstanceIds ⟷ ActualConsumed`;
    ///       **产出侧 → R15 缺载体**。</description></item>
    /// <item><term><see cref="EventKind.StructurePlaced"/></term>
    ///       <description>**→ R16 无 actor 位**。</description></item>
    /// <item><term><see cref="EventKind.StructureRemoved"/></term>
    ///       <description>**→ R17 无 id / 无 item_key**。</description></item>
    /// </list>
    /// <para><b>域外 Kind 一律不计</b> —— 病史流 / 病例流条目进场也**不改变**投影
    /// (`OQ-20-1` ②:用药消耗走世界流 `DropDespawned`,不读 `DrugTreatmentApplied`)。
    /// 世界流内不属七谓词的 Kind(`ActorCellEntered` / `StructureModified` /
    /// `PoiStateChanged` …)同样不参与。</para>
    /// </remarks>
    public static class InventoryFold
    {
        /// <summary>执行一次世界流折叠。</summary>
        /// <param name="events">事件流(**可为三流并集** —— 域外 Kind 自动跳过);顺序任意。</param>
        /// <param name="decoder">载荷解码器(注入;实现住 <c>Sim.Codec</c>)。</param>
        /// <returns>投影 + 缺口清单。调用方须先看 <see cref="InventoryFoldResult.IsComplete"/>。</returns>
        /// <exception cref="ArgumentNullException">任一形参为 null(fail-closed:空解码器
        /// ⇒ 静默返回空投影 ⇒ 「重建成功但库存全空」的静默失败)。</exception>
        public static InventoryFoldResult Fold(IReadOnlyList<SimEvent> events, IPayloadDecoder decoder)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (decoder == null)
                throw new ArgumentNullException(nameof(decoder),
                    "未注入 IPayloadDecoder —— 拒绝返回空投影(空投影 ⇒ 「库存全空」的静默失败)");

            List<SimEvent> ordered = OrderByFullOrderKey(events);
            var gaps = new List<InventoryFoldGap>();

            // ── 预处理:品级来源与采集事实(顺序无关)。
            //    17 的实发序 = DropSpawned → ResourceHarvested → DropClaimed
            //    (HarvestOrchestrator.Resolve),⇒ 品级**必然**晚于出生到达,必须预扫。
            var quality = new Dictionary<long, int>();
            var harvested = new Dictionary<long, SimEvent>();
            CollectHarvested(ordered, decoder, quality, harvested, gaps);

            var rows = new Dictionary<long, InventoryRow>(ordered.Count / 4 + 1);
            var craftOutputIds = new HashSet<long>();

            for (int i = 0; i < ordered.Count; i++)
            {
                SimEvent e = ordered[i];
                if (StreamRouting.Of(e.Kind) != StreamId.World) continue;   // 单流域:世界流

                var at = new EventOrderKey(e.Tick, StreamId.World, e.Patient, e.Seq);

                switch (e.Kind)
                {
                    case EventKind.DropSpawned:
                    {
                        DropSpawnedPayload p = decoder.Decode<DropSpawnedPayload>(e.Kind, e.Payload);
                        if (rows.TryGetValue(p.InstanceId, out InventoryRow existing) && existing.Qty > 0)
                        {
                            gaps.Add(Gap(InventoryFoldGapKind.DuplicateInstanceId, e.Kind, at, p.InstanceId,
                                "实例 id 已在流里出生过且未归零(qty=" + existing.Qty + ")—— " +
                                "违 ADR-006 机制 A 不变量②:号永不回收"));
                            break;
                        }
                        int q = quality.TryGetValue(p.InstanceId, out int known) ? known : 0;
                        rows[p.InstanceId] = new InventoryRow(
                            p.InstanceId, p.ItemKey, q, p.Qty, InventoryProjection.NoOwner);
                        break;
                    }

                    case EventKind.ResourceHarvested:
                        // 品级与采集事实已在预处理收齐(「有采集无出生」在尾段判)。
                        break;

                    case EventKind.DropClaimed:
                    {
                        DropClaimedPayload p = decoder.Decode<DropClaimedPayload>(e.Kind, e.Payload);
                        if (!rows.TryGetValue(p.InstanceId, out InventoryRow row) || row.Qty <= 0)
                        {
                            gaps.Add(Gap(InventoryFoldGapKind.DanglingInstanceReference, e.Kind, at, p.InstanceId,
                                "DropClaimed 点名了一个流里未出生 / 已归零的实例 —— 世界流不完整,投影不可信"));
                            break;
                        }
                        if (row.Owner != InventoryProjection.NoOwner)
                        {
                            gaps.Add(Gap(InventoryFoldGapKind.DuplicateClaim, e.Kind, at, p.InstanceId,
                                "实例已归属 actor=" + row.Owner + ",又被 actor=" + p.Claimer + " 认领 —— " +
                                "转移(P1a)须走 DropSpawned → DropClaimed 新序列,不是二次 claim"));
                            break;
                        }
                        rows[p.InstanceId] = new InventoryRow(
                            row.InstanceId, row.ItemOrdinal, row.Quality, row.Qty, p.Claimer);
                        break;
                    }

                    case EventKind.DropDespawned:
                    {
                        DropDespawnedPayload p = decoder.Decode<DropDespawnedPayload>(e.Kind, e.Payload);
                        if (!rows.TryGetValue(p.InstanceId, out InventoryRow row) || row.Qty <= 0)
                        {
                            gaps.Add(Gap(InventoryFoldGapKind.DanglingInstanceReference, e.Kind, at, p.InstanceId,
                                "DropDespawned 点名了一个流里未出生 / 已归零的实例"));
                            break;
                        }
                        if (p.Qty <= 0)
                        {
                            gaps.Add(Gap(InventoryFoldGapKind.InvalidDespawnQuantity, e.Kind, at, p.InstanceId,
                                "qty=" + p.Qty + " —— 扣减量须 > 0(负值 / 零属畸形输入,不是状态)"));
                            break;
                        }
                        if (p.Qty > row.Qty)
                        {
                            gaps.Add(Gap(InventoryFoldGapKind.InvalidDespawnQuantity, e.Kind, at, p.InstanceId,
                                "qty=" + p.Qty + " > 实例当前 qty=" + row.Qty +
                                " —— 主机应整体拒绝、世界流零事件;流里出现即畸形"));
                            break;
                        }
                        rows[p.InstanceId] = new InventoryRow(
                            row.InstanceId, row.ItemOrdinal, row.Quality, row.Qty - p.Qty, row.Owner);
                        break;
                    }

                    case EventKind.Craft:
                    {
                        CraftPayload p = decoder.Decode<CraftPayload>(e.Kind, e.Payload);
                        ApplyCraftConsumption(p, e.Kind, at, rows, gaps);
                        AccumulateCraftOutputs(p, e.Kind, at, craftOutputIds, gaps);
                        break;
                    }

                    case EventKind.StructurePlaced:
                    {
                        // R16 —— 扣 cost(module_id) 需要知道「扣谁的」,而载荷五字段无 actor 位。
                        StructurePlacedPayload p = decoder.Decode<StructurePlacedPayload>(e.Kind, e.Payload);
                        gaps.Add(Gap(InventoryFoldGapKind.StructurePlacementWithoutActor, e.Kind, at, 0L,
                            "structure_id=" + p.StructureId + " module_id=" + p.ModuleId + " —— " +
                            "StructurePlacedPayload 无 actor 位,扣减记不到人" +
                            "(P0 单机可退化,但退化是推断不是裁定)"));
                        break;
                    }

                    case EventKind.StructureRemoved:
                    {
                        // R17 —— 返还物既无 instance_id 也无 item_key。
                        StructureRemovedPayload p = decoder.Decode<StructureRemovedPayload>(e.Kind, e.Payload);
                        gaps.Add(Gap(InventoryFoldGapKind.StructureRemovalWithoutRefundIdentity, e.Kind, at, 0L,
                            "structure_id=" + p.StructureId + " module_id=" + p.ModuleId + " —— " +
                            "StructureRemovedPayload 只有三字段,返还物既无 instance_id 也无 item_key" +
                            "(BL-21 只说「必新铸」,未定「铸出来落进哪条事件的哪个位」)"));
                        break;
                    }

                    default:
                        // 世界流内不属七谓词的 Kind(ActorCellEntered / StructureModified /
                        // PoiStateChanged / InjuryStateChanged …)⇒ 不参与库存 fold。
                        break;
                }
            }

            // ⚠️ 尾段两趟都按 **instance_id 升序**遍历(而非字典枚举序)——
            //    `Dictionary` 的枚举序不是契约,靠它会让「同一流 ⇒ 同一缺口序」成为偶然。

            // ── 尾段 ①:R18 —— 已建行里没有任何品级来源
            var rowIds = new List<long>(rows.Keys);
            rowIds.Sort();
            for (int i = 0; i < rowIds.Count; i++)
            {
                InventoryRow row = rows[rowIds[i]];
                if (row.Qty <= 0) continue;
                if (quality.ContainsKey(rowIds[i])) continue;
                if (craftOutputIds.Contains(rowIds[i])) continue;

                gaps.Add(new InventoryFoldGap(InventoryFoldGapKind.InstanceWithoutQualitySource,
                    EventKind.DropSpawned, InventoryFoldGap.NoEvent, rowIds[i],
                    "实例 " + rowIds[i] + "(item_key=" + row.ItemOrdinal + ")在整个流里没有品级来源 —— " +
                    "DropSpawnedPayload 无 quality 位;17 路径由 ResourceHarvested.out_quality 补齐," +
                    "25/27/29 掉落与 18 起货溢出则没有"));
            }

            // ── 尾段 ②:`ResourceHarvested` 有、`DropSpawned` 没有
            //    ⇒ 采集已记但实例从未出生(20 侧写者缺位 / 写入失败 / 流被截断)。
            //    判据是「缺出生」而非「缺品级」—— 故归 Dangling,不归 R18。
            var harvestIds = new List<long>(harvested.Keys);
            harvestIds.Sort();
            for (int i = 0; i < harvestIds.Count; i++)
            {
                if (rows.ContainsKey(harvestIds[i])) continue;
                SimEvent e = harvested[harvestIds[i]];
                gaps.Add(Gap(InventoryFoldGapKind.DanglingInstanceReference, e.Kind,
                    new EventOrderKey(e.Tick, StreamId.World, e.Patient, e.Seq), harvestIds[i],
                    "ResourceHarvested 记录了该实例的采集事实,但流里没有对应的 DropSpawned —— " +
                    "实例从未出生,投影不可信"));
            }

            return new InventoryFoldResult(BuildProjection(rows), gaps);
        }

        // ══════════════════════════════════════════════════════════════════
        // 内部
        // ══════════════════════════════════════════════════════════════════

        /// <summary>按全序键排定;键完全相等时按输入下标(显式稳定 tiebreak)。</summary>
        private static List<SimEvent> OrderByFullOrderKey(IReadOnlyList<SimEvent> events)
        {
            int n = events.Count;
            var keys = new EventOrderKey[n];
            for (int i = 0; i < n; i++)
            {
                SimEvent e = events[i];
                keys[i] = new EventOrderKey(e.Tick, StreamRouting.Of(e.Kind), e.Patient, e.Seq);
            }

            // ⚠️ `List.Sort` / `Array.Sort`(Comparison 重载)均**不稳定** ⇒ 显式带下标 tiebreak。
            var idx = new int[n];
            for (int i = 0; i < n; i++) idx[i] = i;
            Array.Sort(idx, (a, b) =>
            {
                int c = keys[a].CompareTo(keys[b]);
                return c != 0 ? c : a.CompareTo(b);
            });

            var ordered = new List<SimEvent>(n);
            for (int i = 0; i < n; i++) ordered.Add(events[idx[i]]);
            return ordered;
        }

        /// <summary>扫一遍收齐「实例 → 品级」与「实例 → 首条 `ResourceHarvested`」。
        /// 同一实例两处矛盾 ⇒ <see cref="InventoryFoldGapKind.ConflictingQualitySource"/>。</summary>
        private static void CollectHarvested(List<SimEvent> ordered, IPayloadDecoder decoder,
            Dictionary<long, int> quality, Dictionary<long, SimEvent> firstSeen, List<InventoryFoldGap> gaps)
        {
            for (int i = 0; i < ordered.Count; i++)
            {
                SimEvent e = ordered[i];
                if (e.Kind != EventKind.ResourceHarvested) continue;

                ResourceHarvestedPayload p = decoder.Decode<ResourceHarvestedPayload>(e.Kind, e.Payload);
                if (quality.TryGetValue(p.InstanceId, out int seen))
                {
                    if (seen != p.OutQuality)
                    {
                        gaps.Add(Gap(InventoryFoldGapKind.ConflictingQualitySource, e.Kind,
                            new EventOrderKey(e.Tick, StreamId.World, e.Patient, e.Seq), p.InstanceId,
                            "该实例先前记为 quality=" + seen + ",此处为 " + p.OutQuality +
                            " —— 品级来源互相矛盾"));
                    }
                    continue;
                }
                quality[p.InstanceId] = p.OutQuality;
                firstSeen[p.InstanceId] = e;
            }
        }

        /// <summary>`Craft` 的**扣料侧**:`InputInstanceIds ⟷ ActualConsumed` 逐位对位。</summary>
        private static void ApplyCraftConsumption(CraftPayload p, EventKind kind, EventOrderKey at,
            Dictionary<long, InventoryRow> rows, List<InventoryFoldGap> gaps)
        {
            int n = p.InputInstanceIds != null ? p.InputInstanceIds.Length : 0;
            int m = p.ActualConsumed != null ? p.ActualConsumed.Length : 0;

            if (n != m)
            {
                gaps.Add(Gap(InventoryFoldGapKind.MalformedPayloadPairing, kind, at, 0L,
                    "InputInstanceIds(" + n + ") ⟷ ActualConsumed(" + m + ") 不等长 —— " +
                    "两组须逐被点名实例对位(registry 约束)"));
                return;
            }

            for (int k = 0; k < n; k++)
            {
                long id = p.InputInstanceIds[k];
                int need = p.ActualConsumed[k];

                if (!rows.TryGetValue(id, out InventoryRow r) || r.Qty <= 0)
                {
                    gaps.Add(Gap(InventoryFoldGapKind.DanglingInstanceReference, kind, at, id,
                        "Craft 投料点名了一个流里未出生 / 已归零的实例"));
                    continue;
                }
                if (r.Owner != p.ActorId)
                {
                    gaps.Add(Gap(InventoryFoldGapKind.ConsumptionExceedsHolding, kind, at, id,
                        "投料实例属 actor=" + r.Owner + ",而 Craft.actor_id=" + p.ActorId +
                        " —— 投料只能扣自己箱里的"));
                    continue;
                }
                if (need < 0 || need > r.Qty)
                {
                    gaps.Add(Gap(InventoryFoldGapKind.ConsumptionExceedsHolding, kind, at, id,
                        "ActualConsumed=" + need + " 越界(实例当前 qty=" + r.Qty + ")"));
                    continue;
                }
                rows[id] = new InventoryRow(r.InstanceId, r.ItemOrdinal, r.Quality, r.Qty - need, r.Owner);
            }
        }

        /// <summary>`Craft` 的**增益侧** —— **R15 缺载体**:载荷无产出 `item_key` 位。
        /// 只登记缺口,**不建行**(不造占位)。</summary>
        private static void AccumulateCraftOutputs(CraftPayload p, EventKind kind, EventOrderKey at,
            HashSet<long> craftOutputIds, List<InventoryFoldGap> gaps)
        {
            int nQty = p.OutputQty != null ? p.OutputQty.Length : 0;
            int nQuality = p.OutputQuality != null ? p.OutputQuality.Length : 0;
            int nIds = p.OutputInstanceIds != null ? p.OutputInstanceIds.Length : 0;

            if (p.OutputInstanceIds != null)
                for (int k = 0; k < nIds; k++) craftOutputIds.Add(p.OutputInstanceIds[k]);

            if (nQty != nQuality || nQty != nIds)
            {
                gaps.Add(Gap(InventoryFoldGapKind.MalformedPayloadPairing, kind, at, 0L,
                    "OutputQty(" + nQty + ") / OutputQuality(" + nQuality +
                    ") / OutputInstanceIds(" + nIds + ") 不等长 —— " +
                    "三组须逐 outputs 行对位(registry 约束)"));
            }

            if (nQty == 0) return;      // 零产出 ⇒ 无增益侧

            long firstOutputId = nIds > 0 ? p.OutputInstanceIds[0] : 0L;
            gaps.Add(Gap(InventoryFoldGapKind.CraftOutputWithoutItemKey, kind, at, firstOutputId,
                "产出 " + nQty + " 行,但 CraftPayload 只有 " +
                "OutputQty/OutputQuality/OutputInstanceIds —— **无产出 item_key 位**;" +
                "且 recipe_id(i32)在全仓无 int → Recipe 映射(Recipe.RecipeId 是 string," +
                "烘焙产物无 ordinal,`_ordinal_baseline.json` 只有物品 registry)"));
        }

        /// <summary>把行表摊成投影(含按持有者的升序视图)。归零行不出投影。</summary>
        private static InventoryProjection BuildProjection(Dictionary<long, InventoryRow> rows)
        {
            var live = new Dictionary<long, InventoryRow>(rows.Count);
            var byOwner = new Dictionary<int, List<long>>();

            foreach (KeyValuePair<long, InventoryRow> kv in rows)
            {
                if (kv.Value.Qty <= 0) continue;             // 归零 ⇒ 出投影(实例已消失)
                live[kv.Key] = kv.Value;
                if (!byOwner.TryGetValue(kv.Value.Owner, out List<long> list))
                {
                    list = new List<long>();
                    byOwner[kv.Value.Owner] = list;
                }
                list.Add(kv.Key);
            }

            foreach (List<long> list in byOwner.Values) list.Sort();   // instance_id 升序

            return new InventoryProjection(live, byOwner);
        }

        private static InventoryFoldGap Gap(InventoryFoldGapKind kind, EventKind eventKind,
            EventOrderKey at, long instanceId, string message)
            => new InventoryFoldGap(kind, eventKind, at, instanceId, message);
    }
}
