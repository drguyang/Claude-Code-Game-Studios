// S20-14 · inventory story-005 —— **统一写入事务模板**(20 库存与物品,主机侧)
//
// 权威来源:
//   design/gdd/inventory-and-items.md
//     · 规则六(:348-372)**存取原子性** —— 「一次装卸要么全成,要么全不成」;
//       原子单位四行表(拾取 = 一件实例 / 炮制 = 一次结算 / 用药 = 一次请求 /
//       建造扣料 = 一次 `StructurePlaced` 的 `cost(m)`)。
//     · 规则六 BL-23(:359-372)消耗意图 = 直接点名 `[(instance_id, qty)]`;
//       id 升序只是「玩家未指定时」的**兜底序**。
//     · 规则九(:529-560)**跨栈部分消耗** —— `OQ-20-10` 裁 ①:跨 N 栈 ⇒ N 条 `DropDespawned`
//       同 tick 相邻 `Seq`;**任一被点名实例不成立 ⇒ 一条都不发**(整请求原子性)。
//     · Edge Cases(:613-621)超容量拾取整件拒 / 炮制中途失败整体回滚 /
//       `qty` 畸形 ⇒ 主机整体拒绝、世界流零事件、不部分扣、不出现负 `qty`。
//   design/registry/entities.yaml `SimEvent.Kind.{DropSpawned, DropClaimed, DropDespawned}`
//     · author 全 = 20(唯一);`Patient = PatientId.None`;载荷 ⊆ 整数域。
//   ADR-005(主机唯一 Append/发号)· ADR-006(载荷 ⊆ 整数域 · 单一舍入)
//   ADR-009 §二/§三(世界流)· §七(拾取三段式)
//   ADR-025 §①(`Sim` 引用集恰 = {BCL, Sim.Contracts})
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么需要一个**模板类**,而不是让每个调用点自己「先验后写」
// ─────────────────────────────────────────────────────────────────────────────
// `EventStream.Append` 有**两种**失败形态,且都不是调用方能「再试一次」的:
//
//   ① **抛**(BCD-码-3 tick 回退 / AC-15 CAP) —— 抛出点在任何状态写入**之前**
//      (`EventStream.cs:213` 的断言先于 `_dedupKeys.Add`),故**单条**本身不污染。
//   ② **静默去重返回**(`EventStream.cs:206-209`) —— 同键重发被吞,不抛、不报、无返回值。
//      调用方**无法**从 `Append` 得知自己这条到底进没进流。
//
// ⇒ 一次请求要落 **N 条**事件时(跨栈消耗 = N 条 `DropDespawned`),
//   「逐条埋头写」的失败模式是**部分落流**:
//   第 1 条进去、第 2 条抛 tick 回退 ⇒ 玩家扣了 5 份却只消失了 3 份,
//   而投影 `InventoryFold` 会忠实地把这个**半途状态**当成真值重建出来。
//
// 本类的解法 = **两阶段**:
//   **阶段一(纯校验)**:把整批事件逐条做「入流前置」检查 —— 一次**纯函数**,
//                        零副作用,任一不过 ⇒ 整批拒,**零事件**。
//   **阶段二(落流)**:前置全过后才真正 `Append`,且**每条都在自己的作用域内**。
//
// ⚠️ **诚实标注本类保证的边界**:
//   · 阶段二里若 `Append` **仍抛**(tick 回退的判据与阶段一同一函数,故正常路径不会;
//     但若上游在两次之间改了流)tick),前面已 Append 的条目**不会**被回滚 ——
//     本类**不是**数据库事务,`EventStream` 没有 undo。
//   · 因此本类的承诺精确表述为:「**入流前置**在整批上原子;前置换态,本类不救」。
//     这与 GDD 规则六的语义一致 —— 规则六管的是**业务裁决**(容量 / 数量 / 归属),
//     业务裁决全在阶段一,全过才写。
//
// ⚠️ 本类**不**持投影、**不**判容量、**不**定事件载荷形状:
//   · 容量判据 = `CarryLoadSolver.CanCarry`(`CarryLoad.cs:308`,唯一实现点);
//   · 投影 = `InventoryFold.Fold`(`InventoryFold.cs:305`);
//   · 载荷 = 各写者自有(`DropEventWriter`)。
//   本类只做**「整批前置全过 ⇒ 才逐条写」**这一件事。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>一批准入的**拒绝理由** —— 具名,不说「差多少」(与 `CraftRejection` 同款纪律)。</summary>
    public enum InventoryRejection
    {
        /// <summary>整批通过。</summary>
        None = 0,

        /// <summary>容量不足 —— 整件拒绝,地面实例留存(规则六 · AC-20-06)。</summary>
        CapacityInsufficient = 1,

        /// <summary>目标实例不存在(不在投影里,或已归零出投影)。</summary>
        InstanceNotFound = 2,

        /// <summary>目标实例当前归属不是请求的持有者(不是你的,不能消耗)。</summary>
        NotOwnedByActor = 3,

        /// <summary>请求份数 &gt; 该实例当前 qty(规则九 Edge Case:`qty` 畸形 ⇒ 整请求拒,不部分扣)。</summary>
        QuantityExceedsAvailable = 4,

        /// <summary>请求份数 ≤ 0(畸形输入,不是「什么都没要」)。</summary>
        QuantityNotPositive = 5,

        /// <summary>目标实例已在本次请求中被前一条扣光(同一次请求里重复点名同一实例)。</summary>
        DuplicateInstanceInRequest = 6,
    }

    /// <summary>一次**消耗**请求的一行 —— 玩家(经 11 / 42)点名的 `(instance_id, qty)`。</summary>
    /// <remarks>
    /// <para>⚠️ 这是 GDD 规则六 BL-23 **④ 的落地形**:消耗意图**直接点名实例集**,
    /// id 升序只是「玩家未指定时」的兜底序,**不是**本类的裁决。</para>
    /// <para>`Qty` 的语义 = **份数**(不是重量、不是剂量)。`dose → portions` 的换算归
    /// `D-21-29`(11 求值,20 只收已算准的整数) —— 本类零换算。</para>
    /// </remarks>
    public readonly struct ConsumeLine
    {
        /// <summary>被点名的实例 id。</summary>
        public readonly long InstanceId;

        /// <summary>要扣的份数(&gt; 0;≤ 该实例当前 qty)。</summary>
        public readonly int Qty;

        /// <summary>构造一行消耗请求。</summary>
        public ConsumeLine(long instanceId, int qty)
        {
            InstanceId = instanceId; Qty = qty;
        }
    }

    /// <summary>
    /// **统一写入事务模板** —— 20 的全部写入路径共用(规则六)。
    /// </summary>
    /// <remarks>
    /// <para><b>用法</b>:调用方先把自己的业务裁决做完(准入 / 容量 / 数量 / 归属),
    /// 把**已裁决通过**的整批效果交给 <see cref="Commit"/>;或对消耗类请求直接调
    /// <see cref="ValidateConsumption"/>(本类代办那五条前置)。</para>
    /// <para>⚠️ 本类**不**是「自动回滚」事务 —— 见文件头「诚实标注本类保证的边界」。</para>
    /// </remarks>
    public static class InventoryTransaction
    {
        /// <summary>
        /// 校验一批消耗请求能否**整批**成立(纯函数:零副作用、零 I/O、零墙钟)。
        /// </summary>
        /// <param name="lines">玩家点名的 `(instance_id, qty)` 集 —— 顺序无关(逐条独立校验)。</param>
        /// <param name="owner">发起请求的玩家(被点名实例的当前归属须等于它)。</param>
        /// <param name="projection">库存投影(切点 = 请求**之前**的流状态)。</param>
        /// <returns>整批的裁决;只有 <see cref="InventoryRejection.None"/> 才可落流。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="lines"/> 或
        /// <paramref name="projection"/> 为 null —— 拒绝当作「空请求」放行(那是静默失效)。</exception>
        /// <remarks>
        /// <para><b>五条前置**(逐条按 GDD 规则六 / 规则九 Edge Cases):</para>
        /// <list type="number">
        /// <item><description>`Qty ≤ 0` ⇒ <see cref="InventoryRejection.QuantityNotPositive"/></description></item>
        /// <item><description>同一请求内**重复点名**同一实例 ⇒ <see cref="InventoryRejection.DuplicateInstanceInRequest"/>
        ///   —— 否则两条各自的 `qty` 都合法、合起来超量,而逐条校验看不出。</description></item>
        /// <item><description>实例不在投影里 ⇒ <see cref="InventoryRejection.InstanceNotFound"/></description></item>
        /// <item><description>实例归属 ≠ <paramref name="owner"/> ⇒ <see cref="InventoryRejection.NotOwnedByActor"/>
        ///   —— **地上实例**(`NoOwner`)同样拒:捡都不算捡,不能直接消耗。</description></item>
        /// <item><description>`Qty >` 该实例当前 qty ⇒ <see cref="InventoryRejection.QuantityExceedsAvailable"/>
        ///   —— 规则九:「不部分扣、不夹带负数」。</description></item>
        /// </list>
        /// <para>⚠️ <b>本方法刻意**不**判容量</b>:消耗是**减重**,容量裁决对减重无意义
        /// (容量判据属于「入包」方向 = `CarryLoadSolver.CanCarry`,那是拾取/产出的活)。
        /// 把容量塞进消耗校验会产生「背包满了所以不能吃药」这种荒谬拒绝。</para>
        /// <para>⚠️ <b>空集是合法的</b>(`lines.Length == 0` ⇒ `None`)—— 它是「这次什么都不消耗」,
        /// 是可表达的业务意图,不是编程错误(与 `AdmissiblePredicate` 对空集的同名处置一致)。</para>
        /// </remarks>
        public static InventoryRejection ValidateConsumption(
            IReadOnlyList<ConsumeLine> lines,
            int owner,
            InventoryProjection projection)
        {
            if (lines == null)
                throw new ArgumentNullException(nameof(lines),
                    "未传入消耗请求集 —— 拒绝以空集冒充「什么都不消耗」");
            if (projection == null)
                throw new ArgumentNullException(nameof(projection),
                    "未传入库存投影 —— 无从校验归属与余量");

            var seen = new HashSet<long>();
            for (int i = 0; i < lines.Count; i++)
            {
                ConsumeLine line = lines[i];

                if (line.Qty <= 0)
                    return InventoryRejection.QuantityNotPositive;

                // 重复点名:两条各自合法,合起来超量 —— 逐条校验的结构性盲点。
                if (!seen.Add(line.InstanceId))
                    return InventoryRejection.DuplicateInstanceInRequest;

                if (!projection.TryGet(line.InstanceId, out InventoryRow row))
                    return InventoryRejection.InstanceNotFound;

                if (row.Owner != owner)
                    return InventoryRejection.NotOwnedByActor;

                if (line.Qty > row.Qty)
                    return InventoryRejection.QuantityExceedsAvailable;
            }

            return InventoryRejection.None;
        }

        /// <summary>
        /// 校验一批**拾取**请求能否整件成立(规则六 · AC-20-06)。
        /// </summary>
        /// <param name="instanceId">地面上的目标实例。</param>
        /// <param name="claimer">发起拾取的玩家。</param>
        /// <param name="currentCarryLoad">玩家当前载重(≥ 0)。</param>
        /// <param name="unitWeight">目标实例的单件重量(&gt; 0)。</param>
        /// <param name="capacity">容量上限。</param>
        /// <param name="projection">库存投影(切点 = 拾取**之前**)。</param>
        /// <returns>整件裁决;只有 `None` 才可落流。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="projection"/> 为 null。</exception>
        /// <remarks>
        /// <para><b>拾取 = 整件</b>(规则六原子单位表第一行):<b>不拆分</b> ——
        /// 超容量时整件留在原地,不存在「先捡一半」。</para>
        /// <para>容量判据**只经** <see cref="CarryLoadSolver.CanCarry"/>:本类不重写
        /// `load + w × qty ≤ cap`(那是唯一实现点的第二份)。</para>
        /// <para>目标须**在地上**(`Owner == NoOwner`):已归属的实例再被「拾取」是
        /// 重复 claim,由 fold 侧判 `DuplicateClaim`,本类提前拒。</para>
        /// </remarks>
        public static InventoryRejection ValidatePickup(
            long instanceId,
            int claimer,
            long currentCarryLoad,
            int unitWeight,
            CarryCapacity capacity,
            InventoryProjection projection)
        {
            if (projection == null)
                throw new ArgumentNullException(nameof(projection),
                    "未传入库存投影 —— 无从校验「它还在不在原地」");

            if (!projection.TryGet(instanceId, out InventoryRow row))
                return InventoryRejection.InstanceNotFound;

            // 已归属 ⇒ 不是「拾取」,是重复 claim(规则六:整件,不重复计)。
            if (row.Owner != InventoryProjection.NoOwner)
                return InventoryRejection.NotOwnedByActor;

            if (!CarryLoadSolver.CanCarry(currentCarryLoad, unitWeight, row.Qty, capacity))
                return InventoryRejection.CapacityInsufficient;

            return InventoryRejection.None;
        }

        /// <summary>
        /// **落流** —— 前置全过后,把整批事件逐条交给 <paramref name="sink"/>。
        /// </summary>
        /// <param name="events">**已构造好**的事件批次(顺序 = 落流顺序;`Seq` 由 sink 发号)。</param>
        /// <param name="sink">事件写入通道(主机唯一)。</param>
        /// <exception cref="ArgumentNullException">任一入参为 null。</exception>
        /// <remarks>
        /// <para>⚠️ <b>返回 void 是刻意的</b>:`EventStream.Append` 对**重发**是
        /// **静默返回**(`EventStream.cs:206-209`),「进了几条」在门 A 的接口面上
        /// **不可观测** —— `IEventSink` 只有 `Append`,没有长度。若本方法编一个计数返回,
        /// 那个数就是**猜**的。要数「真进了几条」的调用方,自己去读具体 sink 的可观测状态
        /// (测试里 = `EventStream.Events.Count`),不要从本方法的返回值推。</para>
        /// <para>⚠️ 若中途 <c>Append</c> **抛出**(正常路径不会 ——
        /// <see cref="TicksAreAppendable"/> 已把 tick 单调前置过了一遍),异常原样上抛,
        /// **已写的条目不回滚**。这是本类保证的边界,见文件头。</para>
        /// </remarks>
        public static void Commit(IReadOnlyList<SimEvent> events, IEventSink sink)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (sink == null) throw new ArgumentNullException(nameof(sink));

            for (int i = 0; i < events.Count; i++)
                sink.Append(events[i]);
        }

        /// <summary>
        /// 校验一批事件的 **tick 单调前置**(`BCD-码-3`)—— 与 `EventStream.AppendCore`
        /// 里的那条断言**同一条判据**,搬到写之前做,使「整批原子」有意义。
        /// </summary>
        /// <param name="events">待写批次。</param>
        /// <param name="lastAppendedTick">流里**已入**的最大 tick(调用方从流读;空流传 <c>long.MinValue</c>)。</param>
        /// <returns>批内全部可写 ⇒ true。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="events"/> 为 null。</exception>
        /// <remarks>
        /// <para>⚠️ 为什么要预查而不是靠 <c>Append</c> 抛:跨栈消耗 = N 条,
        /// 若第 2 条才抛而第 1 条已入流 ⇒ **半途状态**(见文件头)。
        /// 预查把「会抛」变成「整批拒」。</para>
        /// <para>⚠️ 本方法**只**覆盖 tick 单调这一条;AC-15 的 CAP 关只对
        /// `Patient != None` 生效,而世界流事件一律 `PatientId.None`(`EventStream.cs:181`
        /// 的 `enforceCap` 分支对本批恒不触发)⇒ 无需预查。</para>
        /// </remarks>
        public static bool TicksAreAppendable(IReadOnlyList<SimEvent> events, long lastAppendedTick)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));

            long previous = lastAppendedTick;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Tick < previous) return false;
                previous = events[i].Tick;
            }
            return true;
        }
    }
}
