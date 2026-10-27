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
//    · 品级**只从载荷抄**(`ResourceHarvested.out_quality` / `Craft.OutputQuality`),
//      **永不计算**(那是 21a 的活);建材桶的恒 1 是**引 BL-28 的口径常量**,不是求值;
//    · 建造扣减量 `cost(m)` 从**模块目录**(烘焙产物)读,不由 fold 推算。
//
// ── 🔁 2026-10-27 R15–R18 闭合批(用户三项裁定)─────────────────────────────────
// 上文「不做 ordinal → ItemKey 解析」与「不注入 IModuleCatalog」**仍然成立**,本批没有推翻它们:
//   · fold 仍**不**把 ordinal 解析成 `ItemKey` —— 它只把「配方 outputs 的 ItemKey」经物品表
//     折回 **ordinal** 用于建行(方向相反:`ItemKey → ordinal`,不是 `ordinal → ItemKey`);
//   · `cost(m)` 仍不由 fold 推算 —— 它是目录里的一个**读数**。
// 但这批**新增了三份查表源**(全部是版本化烘焙产物,ADR-014 / ADR-015 §一「派生态」判据):
//   ① `ItemDataSet` —— 判断某个 ordinal 是否 `build_part`(R18 建材桶 / R16 扣料桶);
//   ② `RecipeDataSet` —— `Craft.RecipeId`(21a ordinal)→ `Recipe.Outputs`(R15);
//   ③ `IModuleCatalog` —— `ModuleDefinition.{Cost, BuildItemKey}`(R16 扣减量)。
// ⚠️ **三份都是「版本化烘焙数据」,属 ADR-016 §一 三源不变量的第二源** ——
//    不是第四来源,不破坏「零第三方」纪律。三源:事件流 / 烘焙数据 / 二者的纯函数。
//
// ⚠️ **纯函数性(`PlayerActorRegistry` 的特殊处置)**:R16 甲案要「唯一玩家」,而
//    「谁是玩家」**在流里没有任何事件**(全库 `grep PlayerSpawned|PlayerCreated` 零命中)——
//    它只存在于进程内 `PlayerActorRegistry`。若把该 registry 直接注进 fold,
//    fold 的结果就**依赖读取时刻的 registry 状态** ⇒ 不再是流 + 烘焙数据的纯函数
//    (同一份流两次 fold 可能得出不同归属)。⇒ 本类只收**快照值**
//    (<see cref="InventoryFoldContext.SolePlayer"/>),由调用方在 fold 前取一次。
//    该快照**不落流、不进存档**(承 `PlayerActorRegistry` 头注「不是状态真源」)。
//
// ⚠️ **单一 fold 域 = 世界流**(ADR-009 §一 / OQ-20-1):本类**只**消费
//    `StreamRouting.Of(kind) == StreamId.World` 的 Kind。病史流 / 病例流条目**不得**入 fold ——
//    这条由测试的双向夹具守,而不是靠本注释。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
// IModuleCatalog / ModuleDefinition 住 Sim.World(同程序集,非跨程序集边 —— ADR-025 §① 不变)
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// fold 的**外部只读查表源** + 单机归属快照(2026-10-27 R15–R18 闭合批新增)。
    ///
    /// <para><b>为什么收成一个对象而不是三个裸形参</b>:裸参 + 默认 null 会重演
    /// `StructureWriter` 的 <c>N-r1</c> 失败模式 —— 「不传 ⇒ 静默退化,且测试把退化
    /// 断言成正确行为」(`test_c2_noCatalog_noOccupancy_noThrow`)。收成一个**必填**对象后,
    /// 「忘了传表」在**编译期**不可表达;而「表在但查不到号」会走**显式缺口**
    /// (<see cref="InventoryFoldGapKind.CraftRecipeNotInTable"/> 等)⇒ <c>IsComplete = false</c>,
    /// 是**可见的失败**,不是静默算错。</para>
    ///
    /// <para><b>三份表全部是版本化烘焙产物</b>(ADR-014 / ADR-015 §一「派生态」判据)——
    /// 属 ADR-016 §一 三源不变量的第二源,不是第四来源。</para>
    ///
    /// <para><b>纯函数性</b>:<see cref="SolePlayer"/> 是**快照值**(fold 前取一次),不是活 registry。
    /// 理由见文件头注 —— 「谁是玩家」在流里零事件,活 registry 会让 fold 结果依赖读取时刻。</para>
    /// </summary>
    public sealed class InventoryFoldContext
    {
        /// <summary>物品种子表(判 <c>build_part</c> 类别 + 把 <see cref="ItemKey"/> 折回 ordinal)。</summary>
        public ItemDataSet Items { get; }

        /// <summary>配方表(<c>Craft.RecipeId</c> → <c>Recipe.Outputs</c>;R15 的映射键 = <see cref="Recipe.Ordinal"/>)。</summary>
        public RecipeDataSet Recipes { get; }

        /// <summary>模块目录(<c>ModuleDefinition.{Cost, BuildItemKey}</c>;R16 的扣减量来源)。</summary>
        public IModuleCatalog Modules { get; }

        /// <summary>
        /// **唯一玩家**的 actor id;多玩家 / 未知时为 <c>null</c>。
        ///
        /// <para>P0 单机由调用方传一个非 null 值(`PlayerActorRegistry` 里恰有 1 个玩家时);
        /// 联机 / 零玩家 / 多玩家一律 <c>null</c> ⇒ R16 归因**不可判** ⇒ 报缺口。</para>
        ///
        /// <para>⚠️ <b>为什么是 null 而不是哨兵 id</b>:<c>-1</c> 已是
        /// <see cref="InventoryProjection.NoOwner"/> 的合法值(「在地上」)。用同一个数
        /// 表示「不知道扣谁的」会让两种语义撞车。</para>
        /// </summary>
        public int? SolePlayer { get; }

        /// <summary>构造查表源。</summary>
        /// <param name="items">物品种子表(必须已解码;空表 ⇒ 所有 <c>build_part</c> 判定为负)。</param>
        /// <param name="recipes">配方表(空表 ⇒ 所有 <c>Craft</c> 产出报
        /// <see cref="InventoryFoldGapKind.CraftRecipeNotInTable"/>,**不静默丢** —— 那会让
        /// 「配方表没装载」退化成「炮制没产出」)。</param>
        /// <param name="modules">模块目录(null ⇒ **允许**,但每次 <c>StructurePlaced</c> / <c>StructureRemoved</c>
        /// 都会报显式缺口 —— 23 侧未接线时如实可见,不静默跳过扣料)。</param>
        /// <param name="solePlayer">唯一玩家 id(P0 单机);多玩家 / 未知传 <c>null</c>。</param>
        public InventoryFoldContext(ItemDataSet items, RecipeDataSet recipes,
            IModuleCatalog modules, int? solePlayer)
        {
            Items = items;
            Recipes = recipes;
            Modules = modules;
            SolePlayer = solePlayer;
        }

        /// <summary>
        /// **空上下文** —— 三份表皆空、无唯一玩家。
        ///
        /// <para>⚠️ <b>它不是「降级模式」,是「我确实没有表」的显式声明</b>:用它的 fold
        /// 只要遇到 <c>Craft</c> 有产出 / <c>StructurePlaced</c> / 非 17 来源的实例,
        /// 就**必然**报缺口(⇒ <c>IsComplete = false</c>)。纯 <c>Drop*</c> 夹具
        /// (只造 `DropSpawned` + `DropClaimed` + `DropDespawned`,且品级走
        /// <c>ResourceHarvested</c>)用它没有任何缺口 ⇒ 那是**真完整**,不是被放过。</para>
        ///
        /// <para>存在的理由:手写 <c>new InventoryFoldContext(default, default, null, null)</c>
        /// 在 42 个调用点上是一次性噪音;而省略形参会让「忘了传」与「确实没有」不可区分。
        /// 具名常量让后者成为一次**可 grep 的声明**。</para>
        /// </summary>
        public static readonly InventoryFoldContext None =
            new InventoryFoldContext(default, default, null, null);
    }

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

    /// <summary>缺口类别 —— R 相关的**逐条**对应 §涟漪登记的一个 R 行;其余是完整性不变量。</summary>
    /// <remarks>
    /// ⚠️ **2026-10-27 R15–R18 闭合批(批 4)删除了三个「旧形态」枚举值** ——
    /// `CraftOutputWithoutItemKey`(R15)/ `StructurePlacementWithoutActor`(R16)/
    /// `StructureRemovalWithoutRefundIdentity`(R17)。三者都是**结构性缺位**口径
    /// (「载荷永远装不下这个位」),在闭包落地后**零产生点**,只在 `RegistryId` 的
    /// switch 里挂着。
    /// <para><b>为什么删而不是留</b>(用户 2026-10-27 裁定「乙」):留着会让将来的读者
    /// 以为「这条缺口还可能被报」,而它对应的四条口径(R15 显式 ordinal / R16 单机归唯一玩家 /
    /// R17 地面掉落 / R18 各源侧载)**都没有向载荷加字段** ⇒ 旧形态在结构上已不可达。
    /// 且 `RegistryId` 是**穷举 switch**,删掉后任何新产生点都必须先登记才能编译 —— 这是
    /// 承 ADR-024「白名单须显式」的同一条纪律。</para>
    /// <para><b>安全前提(已实测)</b>:本枚举**没有任何序列化面**(全仓无 codec / 存档引用)、
    /// 无 `(int)` 数字依赖、无 `Sim` 外的装配引用 ⇒ 删除 + 重编号不产生兼容性问题。
    /// 复算:<c>grep -rn "InventoryFoldGapKind" unity/Assets --include=*.cs</c>
    /// 仅命中本文件 + 两个测试文件。</para>
    /// </remarks>
    public enum InventoryFoldGapKind
    {
        /// <summary>**R18** —— 某实例在整个流里**没有任何品级来源**
        /// (无 `ResourceHarvested.out_quality`,也非 `Craft` 产出)。</summary>
        InstanceWithoutQualitySource = 0,

        /// <summary>**实例 id 复用** —— `DropSpawned` 撞上一个已存在且未归零的 id
        /// (违 ADR-006 机制 A 不变量②:号不回收)。</summary>
        DuplicateInstanceId = 1,

        /// <summary>**引用未出生的实例** —— `DropClaimed` / `DropDespawned` / `Craft` 投料
        /// 点名了流里从未出生(或已归零)的 id;或 `ResourceHarvested` 有而
        /// `DropSpawned` 缺失。世界流不完整 = 投影不可信。</summary>
        DanglingInstanceReference = 2,

        /// <summary>**重复归属** —— 同一实例被 `DropClaimed` 两次(转移须走
        /// `DropSpawned → DropClaimed` 新序列,不是二次 claim)。</summary>
        DuplicateClaim = 3,

        /// <summary>**非法扣减量** —— `DropDespawned.qty ≤ 0` 或 &gt; 该实例当前 qty
        /// (后者属畸形输入:主机应整体拒绝、世界流零事件)。</summary>
        InvalidDespawnQuantity = 4,

        /// <summary>**扣减超出持有** —— `Craft.ActualConsumed[i]` 越界,或投料实例的持有者
        /// ≠ `Craft.actor_id`(投料只能扣自己箱里的)。</summary>
        ConsumptionExceedsHolding = 5,

        /// <summary>**品级来源互相矛盾** —— 同一实例被两处给出不同 `quality`。</summary>
        ConflictingQualitySource = 6,

        /// <summary>**载荷组内不等长** —— `Craft` 的 `InputInstanceIds ⟷ ActualConsumed`
        /// 或 `OutputQty ⟷ OutputQuality ⟷ OutputInstanceIds` 长度不一致(registry 约束)。
        /// <para>⚠️ **纵深防御,非生产可达**:`PayloadCodec.World.cs:200-208` 在**写侧**即抛
        /// (「坏数据不进字节面」)⇒ 合规 codec 写出的流到不了本分支。留着是为读侧收到
        /// 别端写坏的数据 / 生产池实现变更时**不静默错算**;测试侧亦无法用真 codec 构造
        /// 该形态 ⇒ 该分支**未被夹具钉住**(Proven = ✗,登记在 story 的 Test Evidence)。</para></summary>
        MalformedPayloadPairing = 7,

        // ══════════════════════════════════════════════════════════════════
        // 2026-10-27 R15–R18 闭合批新增 —— 查表 / 归属不可判的**显式**缺口。
        // ⚠️ 全部是 fail-closed 形态:「拿不到真源 ⇒ 拒绝出完整投影」,
        //    不是「拿不到就算 0」。照 `RequireComplete()` 的既有纪律。
        // ══════════════════════════════════════════════════════════════════

        /// <summary>**R15** —— `Craft.recipe_id` 在配方表里查无此号 ⇒ 产出物
        /// 确定不了 `item_key`。这是**数据 / 版本不一致**(可修),不是载荷缺位。
        /// <para>⚠️ 旧形态 `CraftOutputWithoutItemKey`(「载荷无 output item_key 位」)
        /// 已于 2026-10-27 批 4 随闭包删除 —— 产出 `item_key` 现由 `recipe_id` 查表折出。</para></summary>
        CraftRecipeNotInTable = 8,

        /// <summary>**R15** —— 配方查到了,但其 `outputs` 行数与载荷的
        /// `OutputQty` / `OutputQuality` / `OutputInstanceIds` 行数不一致 ⇒ 无法逐行对位建行。
        /// <para>⚠️ 这是**数据一致性**缺口(配方表版本 ≠ 载荷产生时的配方表版本),
        /// 不是载荷畸形(后者归 <see cref="MalformedPayloadPairing"/>)。</para></summary>
        CraftOutputCountMismatch = 9,

        /// <summary>**R16** —— 建造扣料归因不可判:``SolePlayer == null``
        /// (多玩家 / 零玩家 / 未接 registry)或 `StructurePlaced.module_id` 不在模块目录。</summary>
        BuildCostAttributionUnresolved = 10,

        /// <summary>**R16** —— 建造扣料**量不足**:唯一玩家名下该建材的库存
        /// 少于 `cost(m)`。主机应整体拒绝、世界流零事件 ⇒ 流里出现即畸形
        /// (同 <see cref="InvalidDespawnQuantity"/> 的判据形态)。</summary>
        BuildCostExceedsHolding = 11,
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
                    case InventoryFoldGapKind.CraftRecipeNotInTable: return "R15";
                    case InventoryFoldGapKind.CraftOutputCountMismatch: return "R15";
                    case InventoryFoldGapKind.BuildCostAttributionUnresolved: return "R16";
                    case InventoryFoldGapKind.BuildCostExceedsHolding: return "R16";
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
    /// <para><b>纯函数</b>:输入 = 事件流 + 解码器 + <see cref="InventoryFoldContext"/>(烘焙查表源
    /// 与单机归属**快照**),输出 = 投影。零**第四**来源 ——
    /// 不读快照存档、不读墙钟、不读表现态位置、不读活 registry。
    /// 三源 = 事件流 / 版本化烘焙数据 / 二者的纯函数(ADR-016 §一)。</para>
    /// <para><b>全序</b>:按 <c>(Tick, StreamPriority, Patient, Seq)</c> 升序应用
    /// (<see cref="EventOrderKey"/>);键完全相等时按**输入列表下标**升序 —— 显式 tiebreak,
    /// 不依赖 <c>List.Sort</c> 的不稳定性。⇒ 传入顺序任意,结果相同(排序在 fold 内,
    /// 不是调用方义务)。</para>
    /// </summary>
    /// <remarks>
    /// <para><b>六类 Kind 的转移**(规则一表;2026-10-27 起 `StructureRemoved` **退出**谓词集 ——
    /// 见其条目):</para>
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
    ///       **产出侧**(2026-10-27 起)= 经 <c>recipe_id</c> 查配方表、逐行建行(R15 闭合)。</description></item>
    /// <item><term><see cref="EventKind.StructurePlaced"/></term>
    ///       <description>建造扣料(2026-10-27 起)= 按 <see cref="InventoryFoldContext.SolePlayer"/>
    ///       归唯一玩家、从模块目录取 `cost(m)`(R16 甲案闭合);归因不可判 ⇒ 报缺口。</description></item>
    /// <item><term><see cref="EventKind.StructureRemoved"/></term>
    ///       <description>**不参与 fold**(2026-10-27 用户裁定「地面掉落」)—— 拆除的返还物由 23
    ///       另发一条**普通** <c>DropSpawned</c>,fold 照常当掉落处理。
    ///       ⇒ 本 Kind **移出**库存谓词集(见下方注释)。</description></item>
    /// </list>
    /// <para><b>域外 Kind 一律不计</b> —— 病史流 / 病例流条目进场也**不改变**投影
    /// (`OQ-20-1` ②:用药消耗走世界流 `DropDespawned`,不读 `DrugTreatmentApplied`)。
    /// 世界流内不属谓词的 Kind(`ActorCellEntered` / `StructureModified` /
    /// `PoiStateChanged` …)同样不参与。</para>
    /// </remarks>
    public static class InventoryFold
    {
        /// <summary>执行一次世界流折叠。</summary>
        /// <param name="events">事件流(**可为三流并集** —— 域外 Kind 自动跳过);顺序任意。</param>
        /// <param name="decoder">载荷解码器(注入;实现住 <c>Sim.Codec</c>)。</param>
        /// <param name="context">烘焙查表源 + 单机归属快照(<b>必填</b>,见下)。</param>
        /// <returns>投影 + 缺口清单。调用方须先看 <see cref="InventoryFoldResult.IsComplete"/>。</returns>
        /// <exception cref="ArgumentNullException">任一形参为 null(fail-closed:空解码器
        /// ⇒ 静默返回空投影 ⇒ 「重建成功但库存全空」的静默失败)。</exception>
        /// <remarks>
        /// ⚠️ <b><paramref name="context"/> 是必填形参,不是可选参数</b> —— 这是刻意避开
        /// `StructureWriter` 的 <c>N-r1</c> 失败模式(可选 + 默认 null ⇒ 静默退化,
        /// 且测试把退化断言成正确行为)。手头确实没有任何表的调用方(纯 `Drop*` 夹具)
        /// 须**显式**写 <see cref="InventoryFoldContext.None"/> —— 那是一句可 grep 的声明
        /// (「我知道我没有表」),而不是一次沉默的省略。
        /// </remarks>
        public static InventoryFoldResult Fold(IReadOnlyList<SimEvent> events, IPayloadDecoder decoder,
            InventoryFoldContext context)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (decoder == null)
                throw new ArgumentNullException(nameof(decoder),
                    "未注入 IPayloadDecoder —— 拒绝返回空投影(空投影 ⇒ 「库存全空」的静默失败)");
            if (context == null)
                throw new ArgumentNullException(nameof(context),
                    "未注入 InventoryFoldContext —— 拒绝以「无表」冒充「查表失败」。" +
                    "确实没有表请显式传 InventoryFoldContext.None(可 grep 的声明);" +
                    "可选参数会让「忘了传表」退化成静默少算(StructureWriter N-r1 的形态)");

            List<SimEvent> ordered = OrderByFullOrderKey(events);
            var gaps = new List<InventoryFoldGap>();

            // ── 表索引(一次建好;R15 / R16 / R18 共用)。
            ItemLookup lookup = ItemLookup.Build(context.Items);
            Dictionary<int, Recipe> recipesByOrdinal = BuildRecipeIndex(context.Recipes);

            // ── 预处理:品级来源与采集事实(顺序无关)。
            //    17 的实发序 = DropSpawned → ResourceHarvested → DropClaimed
            //    (HarvestOrchestrator.Resolve),⇒ 品级**必然**晚于出生到达,必须预扫。
            var quality = new Dictionary<long, int>();
            var harvested = new Dictionary<long, SimEvent>();
            CollectHarvested(ordered, decoder, quality, harvested, gaps);

            var rows = new Dictionary<long, InventoryRow>(ordered.Count / 4 + 1);

            // 品级「已解析」的实例集 —— 与 `quality` 分开是因为建材桶的 1 是**口径常量**
            // (BL-28)而非流里抄来的值:两者 Mix 进同一个字典会让「建材恒 1」看起来像
            // 「采集到品级 1」,混淆来源。
            var qualityResolved = new HashSet<long>(quality.Keys);

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
                        ApplyCraftOutputs(p, e.Kind, at, rows, gaps, quality, qualityResolved,
                            recipesByOrdinal, lookup);
                        break;
                    }

                    case EventKind.StructurePlaced:
                    {
                        // R16 甲案(用户 2026-10-27 裁定):P0 单机 ⇒ 建造扣料恒归**唯一玩家**。
                        // 归因不可判(SolePlayer == null / 模块不在目录)⇒ 显式缺口,不静默跳过。
                        StructurePlacedPayload p = decoder.Decode<StructurePlacedPayload>(e.Kind, e.Payload);
                        ApplyBuildCost(p, e.Kind, at, rows, gaps, context, lookup);
                        break;
                    }

                    case EventKind.StructureRemoved:
                        // 🔁 2026-10-27 用户裁定「地面掉落」⇒ 本 Kind **退出库存谓词集**。
                        //    拆除的返还物由 23 另发一条**普通 DropSpawned**(qty = Refund、
                        //    item_key = module.BuildItemKey、锚点 = 拆除格),fold 照常当掉落处理 ——
                        //    它不需要 structure_id,也就不需要本事件。
                        //    ⚠️ 「23 是否真的发了那条 DropSpawned」**不是 fold 的可判事实**:
                        //       fold 看不到「本该有而没有」的事件(那需要一份「应发清单」= 第二真源)。
                        //       ⇒ AC-23-16 的「不静默吞返还」由 **23 侧的 spy-sink 测试**钉
                        //       (`modular-building.md:558` 原文已把载体指到 20 侧测试文件),
                        //       **不由 fold 兜底**。本行留此注是为了让「为什么 fold 不查 StructureRemoved」
                        //       在代码里可读,而不是靠 commit message。
                        break;

                    default:
                        // 世界流内不属七谓词的 Kind(ActorCellEntered / StructureModified /
                        // PoiStateChanged / InjuryStateChanged …)⇒ 不参与库存 fold。
                        break;
                }
            }

            // ⚠️ 尾段两趟都按 **instance_id 升序**遍历(而非字典枚举序)——
            //    `Dictionary` 的枚举序不是契约,靠它会让「同一流 ⇒ 同一缺口序」成为偶然。

            // ── 尾段 ①:R18 —— 已建行里没有任何品级来源(2026-10-27 起 = **三桶白名单**)
            //    桶① 17 采集:ResourceHarvested.out_quality(已在 quality 里)
            //    桶② Craft 产出:Craft.OutputQuality(已在 ApplyCraftOutputs 里写入 qualityResolved)
            //    桶③ build_part:BL-28 已裁「P0 品级恒 1」⇒ 口径常量,不是流里抄来的值
            //    桶外(25 战斗掉落 / 27 敌人掉落 / 29 死亡掉落 / 18 起货溢出)⇒ 仍报缺口。
            //
            // ⚠️ **这是白名单,不是「凡是建材就放过」** —— 判据必须是
            //    「该实例的 item_key 在物品表里 category == BuildPart」,而**不是**
            //    「流的其余部分看起来像建造」。查不到序号的 instance 不享受本豁免(见下)。
            var rowIds = new List<long>(rows.Keys);
            rowIds.Sort();
            for (int i = 0; i < rowIds.Count; i++)
            {
                InventoryRow row = rows[rowIds[i]];
                if (row.Qty <= 0) continue;
                if (qualityResolved.Contains(rowIds[i])) continue;              // 桶① / 桶②

                if (lookup.IsBuildPart(row.ItemOrdinal))                        // 桶③
                {
                    // 品级按 BL-28 口径常量定(不是求值、不是推断):该行 instance 的
                    // Quality 字段保持 0 会与「恒 1」的口径不符,故就地归一。
                    rows[rowIds[i]] = new InventoryRow(row.InstanceId, row.ItemOrdinal,
                        BuildPartQualityGate.P0BuildPartQuality, row.Qty, row.Owner);
                    continue;
                }

                gaps.Add(new InventoryFoldGap(InventoryFoldGapKind.InstanceWithoutQualitySource,
                    EventKind.DropSpawned, InventoryFoldGap.NoEvent, rowIds[i],
                    "实例 " + rowIds[i] + "(item_key=" + row.ItemOrdinal + ")在整个流里没有品级来源 —— " +
                    "DropSpawnedPayload 无 quality 位;已闭合的三桶 = 17 采集 / Craft 产出 / build_part(BL-28), " +
                    "本条不属于任何一桶(25/27/29 掉落与 18 起货溢出仍缺载体)"));
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

        /// <summary>物品表的双向索引(ordinal → 类别 / ItemKey → ordinal)。
        /// <para>⚠️ 两个方向都是**必要**的:判是否 `build_part` 要从 <c>ordinal</c> 出发(R18 桶③),
        /// 而配方 outputs 给的是 <see cref="ItemKey"/>,要折回 <c>ordinal</c>(R15)。</para>
        /// <para>⚠️ <b>重复键不抛</b>(与 <c>InventoryLoadService</c> 的装配期校验分工不同):
        /// fold 是读侧,重复 ordinal 属**表缺陷**,但那由装载期门执法;此处「后者覆盖前者的静默」
        /// 换成**先到先得**(确定性),避免 fold 因表缺陷抛异常而使整个投影不可得 ——
        /// 缺口可读比异常可读更有信息量。</para></summary>
        private readonly struct ItemLookup
        {
            private readonly Dictionary<int, ItemDef> _byOrdinal;
            private readonly Dictionary<ItemKey, int> _ordinalOf;

            private ItemLookup(Dictionary<int, ItemDef> byOrdinal, Dictionary<ItemKey, int> ordinalOf)
            {
                _byOrdinal = byOrdinal;
                _ordinalOf = ordinalOf;
            }

            /// <summary>建索引(空表 ⇒ 两个空字典,所有查询返回 false)。</summary>
            public static ItemLookup Build(ItemDataSet data)
            {
                ItemDef[] items = data.Items;
                int n = items != null ? items.Length : 0;
                var byOrdinal = new Dictionary<int, ItemDef>(n);
                var ordinalOf = new Dictionary<ItemKey, int>(n);
                for (int i = 0; i < n; i++)
                {
                    ItemDef d = items[i];
                    if (!byOrdinal.ContainsKey(d.Ordinal)) byOrdinal[d.Ordinal] = d;
                    var key = new ItemKey(d.BaseId, d.ProcessingState);
                    if (!ordinalOf.ContainsKey(key)) ordinalOf[key] = d.Ordinal;
                }
                return new ItemLookup(byOrdinal, ordinalOf);
            }

            /// <summary>该 ordinal 是否 <c>build_part</c>(R18 桶③的判据)。
            /// <b>查不到 ⇒ false</b>:查不到就**不**享受豁免,落回 R18 缺口。</summary>
            public bool IsBuildPart(int ordinal)
                => _byOrdinal.TryGetValue(ordinal, out ItemDef d) && d.Category == ItemCategory.BuildPart;

            /// <summary><see cref="ItemKey"/> → ordinal(<c>false</c> = 物品表里没有这个键)。</summary>
            public bool TryOrdinalOf(ItemKey key, out int ordinal)
            {
                if (key.BaseId == null)
                {
                    ordinal = -1;
                    return false;
                }
                return _ordinalOf.TryGetValue(key, out ordinal);
            }
        }

        /// <summary>配方表索引:<c>Ordinal → Recipe</c>。重复 ordinal ⇒ 先到先得(同
        /// <see cref="ItemLookup"/> 的取舍)。</summary>
        private static Dictionary<int, Recipe> BuildRecipeIndex(RecipeDataSet data)
        {
            Recipe[] recipes = data.Recipes;
            int n = recipes != null ? recipes.Length : 0;
            var index = new Dictionary<int, Recipe>(n);
            for (int i = 0; i < n; i++)
            {
                if (!index.ContainsKey(recipes[i].Ordinal)) index[recipes[i].Ordinal] = recipes[i];
            }
            return index;
        }

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

        /// <summary>`Craft` 的**增益侧**(2026-10-27 R15 闭合)—— 经
        /// <c>recipe_id</c> 查配方表,逐 outputs 行建实例行。
        ///
        /// <para><b>映射链</b>:<c>Craft.RecipeId</c>(21a ordinal)→ <see cref="Recipe.Ordinal"/>
        /// → <see cref="Recipe.Outputs"/>[i].Key(<see cref="ItemKey"/>)→ 物品表折回
        /// <c>ordinal</c> → 建行。末步「折回」是本批唯一新增的表依赖(R15 的根因正是缺这一步)。</para>
        ///
        /// <para><b>建行的 owner = <c>Craft.ActorId</c></b>(炮制产出归炮制者;与扣料侧
        /// <see cref="ApplyCraftConsumption"/> 的归属判据同源)。</para>
        ///
        /// <para><b>三个 fail-closed 出口</b>(各报一种缺口,不静默丢):
        /// ① 配方表查无此号 ⇒ <see cref="InventoryFoldGapKind.CraftRecipeNotInTable"/>;
        /// ② 配方 outputs 行数 ≠ 载荷三组行数 ⇒ <see cref="InventoryFoldGapKind.CraftOutputCountMismatch"/>;
        /// ③ 配方里的 <see cref="ItemKey"/> 在物品表里折不出 ordinal ⇒ 同样报
        /// ①(「查不到 = 表不完整」,与 ① 同一个失败面)。</para>
        /// </summary>
        private static void ApplyCraftOutputs(CraftPayload p, EventKind kind, EventOrderKey at,
            Dictionary<long, InventoryRow> rows, List<InventoryFoldGap> gaps,
            Dictionary<long, int> quality, HashSet<long> qualityResolved,
            Dictionary<int, Recipe> recipesByOrdinal, ItemLookup lookup)
        {
            int nQty = p.OutputQty != null ? p.OutputQty.Length : 0;
            int nQuality = p.OutputQuality != null ? p.OutputQuality.Length : 0;
            int nIds = p.OutputInstanceIds != null ? p.OutputInstanceIds.Length : 0;

            if (nQty != nQuality || nQty != nIds)
            {
                gaps.Add(Gap(InventoryFoldGapKind.MalformedPayloadPairing, kind, at, 0L,
                    "OutputQty(" + nQty + ") / OutputQuality(" + nQuality +
                    ") / OutputInstanceIds(" + nIds + ") 不等长 —— " +
                    "三组须逐 outputs 行对位(registry 约束)"));
                return;
            }

            if (nQty == 0) return;      // 零产出 ⇒ 无增益侧(扣料侧已单独处理)

            long firstOutputId = nIds > 0 ? p.OutputInstanceIds[0] : 0L;

            // ── 映射链第一跳:recipe_id → Recipe
            if (!recipesByOrdinal.TryGetValue(p.RecipeId, out Recipe recipe))
            {
                gaps.Add(Gap(InventoryFoldGapKind.CraftRecipeNotInTable, kind, at, firstOutputId,
                    "recipe_id=" + p.RecipeId + " 在配方表里查无此号 ⇒ 产出 " + nQty +
                    " 行的 item_key 无法确定(R15 的映射键 = Recipe.Ordinal;查不到即表版本不一致 / 未装载)"));
                return;
            }

            // ── 映射链第二跳:配方 outputs 行数 ↔ 载荷三组行数
            int nRecipeOut = recipe.Outputs != null ? recipe.Outputs.Length : 0;
            if (nRecipeOut != nQty)
            {
                gaps.Add(Gap(InventoryFoldGapKind.CraftOutputCountMismatch, kind, at, firstOutputId,
                    "配方 '" + recipe.RecipeId + "'(ordinal=" + recipe.Ordinal + ") 声明 " + nRecipeOut +
                    " 行产出,而 Craft 载荷报 " + nQty + " 行 —— 逐行对位不可能" +
                    "(配方表版本 ≠ 载荷产生时的配方表版本)"));
                return;
            }

            // ── 建行:逐 outputs 行
            for (int k = 0; k < nQty; k++)
            {
                long outId = p.OutputInstanceIds[k];
                int qty = p.OutputQty[k];
                int q = p.OutputQuality[k];
                ItemKey key = recipe.Outputs[k].Key;

                // 第三跳:ItemKey → 物品表 ordinal(方向 = ItemKey → int,不是 int → ItemKey)
                if (!lookup.TryOrdinalOf(key, out int ordinal))
                {
                    gaps.Add(Gap(InventoryFoldGapKind.CraftRecipeNotInTable, kind, at, outId,
                        "配方 '" + recipe.RecipeId + "' 的第 " + k + " 行产出 (" +
                        key.BaseId + ", " + key.State + ") 在物品表里折不出 ordinal —— " +
                        "配方表与物品表版本不一致"));
                    continue;
                }

                if (qty <= 0)
                {
                    gaps.Add(Gap(InventoryFoldGapKind.InvalidDespawnQuantity, kind, at, outId,
                        "产出 qty=" + qty + " ≤ 0 —— 配方表 AC-21a-7/16 已拒收,流里出现即畸形"));
                    continue;
                }

                if (rows.TryGetValue(outId, out InventoryRow existing) && existing.Qty > 0)
                {
                    gaps.Add(Gap(InventoryFoldGapKind.DuplicateInstanceId, kind, at, outId,
                        "产出实例 id 已在流里出生过且未归零(qty=" + existing.Qty + ")—— " +
                        "违 ADR-006 机制 A 不变量②:号永不回收"));
                    continue;
                }

                rows[outId] = new InventoryRow(outId, ordinal, q, qty, p.ActorId);
                quality[outId] = q;
                qualityResolved.Add(outId);         // 桶②:品级来源 = Craft.OutputQuality
            }
        }

        /// <summary>`StructurePlaced` 的**扣料侧**(2026-10-27 R16 甲案闭合)。
        ///
        /// <para><b>甲案口径</b>(用户 2026-10-27 裁定):P0 单机 ⇒ 建造扣料**恒归唯一玩家**。
        /// 归属来源 = <see cref="InventoryFoldContext.SolePlayer"/>(fold 前取的快照,
        /// 不读活 registry —— 保纯函数性)。</para>
        ///
        /// <para><b>fail-closed 出口</b>:<c>SolePlayer == null</c>(多玩家 / 零玩家 / 未接)
        /// ⇒ <see cref="InventoryFoldGapKind.BuildCostAttributionUnresolved"/>;
        /// 模块不在目录 ⇒ 同一条(拿不到 <c>cost(m)</c> 与 <c>build_item_key</c>,
        /// 扣减量无真源);库存不足 ⇒ <see cref="InventoryFoldGapKind.BuildCostExceedsHolding"/>。</para>
        ///
        /// <para><b>���减方式</b>= 规则六的兜底序:同 <c>item_key</c> 多栈按
        /// <c>instance_id</c> 升序逐栈扣。BL-28 已裁 P0 建材品级恒 1 ⇒ 建材上「点名」与
        /// 「兜底序」结果恒等(<c>inventory-and-items.md:529-560</c> 规则六原文)。</para>
        /// </summary>
        private static void ApplyBuildCost(StructurePlacedPayload p, EventKind kind, EventOrderKey at,
            Dictionary<long, InventoryRow> rows, List<InventoryFoldGap> gaps,
            InventoryFoldContext context, ItemLookup lookup)
        {
            if (!context.SolePlayer.HasValue)
            {
                gaps.Add(Gap(InventoryFoldGapKind.BuildCostAttributionUnresolved, kind, at, 0L,
                    "structure_id=" + p.StructureId + " module_id=" + p.ModuleId + " —— " +
                    "建造扣料归因不可判:SolePlayer 为空(多玩家 / 零玩家 / 未接 PlayerActorRegistry)。" +
                    "R16 甲案只在 P0 单机成立;多玩家须走载荷补 actor 位(乙案)"));
                return;
            }

            if (context.Modules == null ||
                !context.Modules.GetModuleDefinition(p.ModuleId).HasValue)
            {
                gaps.Add(Gap(InventoryFoldGapKind.BuildCostAttributionUnresolved, kind, at, 0L,
                    "structure_id=" + p.StructureId + " module_id=" + p.ModuleId + " —— " +
                    "模块不在目录(或目录未注入)⇒ 取不到 cost(m) 与 build_item_key,扣减量无真源"));
                return;
            }

            ModuleDefinition def = context.Modules.GetModuleDefinition(p.ModuleId).Value;
            int need = def.Cost;
            int itemOrdinal = def.BuildItemKey;
            int owner = context.SolePlayer.Value;

            if (need <= 0) return;      // cost 为 0 的模块不扣料(目录 AC 已约束 cost ≥ 1,流里出现 0 不扣也不错)

            // ── 规则六兜底序:同 item_key 多栈按 instance_id 升序
            List<long> candidates = OwnedIdsOf(rows, owner, itemOrdinal);
            int available = 0;
            for (int i = 0; i < candidates.Count; i++) available += rows[candidates[i]].Qty;

            if (available < need)
            {
                gaps.Add(Gap(InventoryFoldGapKind.BuildCostExceedsHolding, kind, at, 0L,
                    "structure_id=" + p.StructureId + " 需 " + need + " 份 item_key=" + itemOrdinal +
                    ",而 actor=" + owner + " 名下只有 " + available + " 份 —— " +
                    "主机应整体拒绝、世界流零事件;流里出现即畸形"));
                return;
            }

            int remaining = need;
            for (int i = 0; i < candidates.Count && remaining > 0; i++)
            {
                InventoryRow r = rows[candidates[i]];
                int take = r.Qty < remaining ? r.Qty : remaining;
                rows[candidates[i]] = new InventoryRow(r.InstanceId, r.ItemOrdinal, r.Quality,
                    r.Qty - take, r.Owner);
                remaining -= take;
            }
        }

        /// <summary>收集某 actor 名下某 ordinal 的实例 id(**按 instance_id 升序**)。
        /// 规则六的兜底序要求稳定序 ⇒ 显式排序,不靠字典枚举序。</summary>
        private static List<long> OwnedIdsOf(Dictionary<long, InventoryRow> rows, int owner, int itemOrdinal)
        {
            var ids = new List<long>();
            foreach (KeyValuePair<long, InventoryRow> kv in rows)
            {
                if (kv.Value.Owner != owner) continue;
                if (kv.Value.ItemOrdinal != itemOrdinal) continue;
                if (kv.Value.Qty <= 0) continue;
                ids.Add(kv.Key);
            }
            ids.Sort();
            return ids;
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
