// S20-14 · inventory story-005 —— `DropSpawned` / `DropClaimed` / `DropDespawned`
// **三支世界流写者**(20 库存与物品,主机侧,**全案唯一**发出方)
//
// 权威来源:
//   design/registry/entities.yaml
//     · `SimEvent.Kind.DropSpawned`  —— stream: world · author: 20(触发方 ∈ {17/18/25/29})
//         payload = { instance_id: i64; spawn_anchor: 格 WorldPos; item_key: i32; qty: i32 }
//         `instance_id` 由 `IIdAuthority.NextItemInstanceId()` 发放,**不本地铸造**(D-21-27);
//         `spawn_anchor` = 出生锚点(**整数格**,ADR-015 §三);位置/轨迹**不进流**(§五)
//     · `SimEvent.Kind.DropClaimed`  —— stream: world · author: 20(主机裁决,唯一)
//         payload = { instance_id: i64; claimer: i32; tick: i64 }
//     · `SimEvent.Kind.DropDespawned` —— stream: world · author: 20(唯一)
//         payload = { instance_id: i64; qty: i32; reason: 枚举 }
//         颗粒度 = **一实例一条**;跨 N 栈 = N 条同 tick 相邻 Seq(OQ-20-10 裁 ①)
//   design/gdd/inventory-and-items.md
//     · 规则二(:128-136)消耗/归属路径表;BL-1:铸造点 = `DropSpawned`(出生即铸),
//       `DropClaimed` **只改归属不改发号**
//     · 规则六(:348-372)存取原子性;拾取 = **一件实例整件拒绝**,不拆分
//     · 规则九(:529-560)部分消耗:`qty` = 份数;`qty > 当前量` ⇒ **主机整体拒绝、零事件**
//   ADR-009 §二/§三(世界流三态)· §五(身份进流 / 位置表现)· §七(拾取三段式)
//   ADR-005(主机唯一 Append)· ADR-006(载荷 ⊆ 整数域)· ADR-010 §五(id 权威)
//   ADR-024(entities.yaml = Kind 单一真源)· ADR-029 §③(唯一编码路径 = IPayloadEncoder)
//   ADR-025 §①(`Sim` 引用集恰 = {BCL, Sim.Contracts})
//
// ─────────────────────────────────────────────────────────────────────────────
// 本类**不做**什么(边界,必须与「它做了什么」同读)
// ─────────────────────────────────────────────────────────────────────────────
// ① **不判拾取距离** —— ADR-009 §七 的三段式是「意图事件 + 主机**当下**判距 + 宽容半径」;
//    判距吃的是**表现态位置**(float),物理上住不进 `Sim`(门 A)。本类收的是
//    「判距**已通过**」之后的**效果写入**。判距本体归 4 的交互装配轮(登记项)。
// ② **不判容量** —— 容量判据唯一实现点 = `CarryLoadSolver.CanCarry`(`CarryLoad.cs:308`);
//    拾取的整件拒绝前置 = `InventoryTransaction.ValidatePickup`。本类只在**全部裁决通过后**
//    被调用,故「零事件」是结构性的 —— 不持写通道的拒绝路径写不出任何东西。
// ③ **不做换算** —— `dose → portions` 归 `D-21-29`(11 求值)。本类收已算准的整数份数。
// ④ **不铸 `PatientId`** —— 世界流事件 `Patient = PatientId.None`(-1 哨兵),
//    不污染 `max(patient_id)` 高水位(ADR-007 §四)。
// ⑤ **不写 `quality`** —— `DropSpawnedPayload` 四字段**无 quality 位**(R18 已登记);
//    17 采集路径的品级走 `ResourceHarvested.out_quality`(ADR-009 Amendment K),
//    其余触发方(25/27/29/18 溢出)的 quality **在流里不可重建** —— 那是 R18 的账,
//    本类不擅自扩载荷(扩 = ADR-009 窄修订)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么三支写者**共用一个类**,而不是三个类
// ─────────────────────────────────────────────────────────────────────────────
// `DropSpawned` 是 id 的**铸造点**,`DropClaimed` 是**归属变更**,`DropDespawned` 是**减量** ——
// 三者在 GDD 规则六的**同一个原子单位**里出现(一次拾取 = Spawned→Claimed 可能同 tick;
// 一次用药 = N 条 Despawned 必须整批)。拆成三个类,「谁是同一批」这条信息就丢了,
// 调用方又得自己拼 —— 那正是 `InventoryTransaction` 要消灭的形态。
// 同一个类持有同一个 `IEventSink`,天然能保证「同一批走同一个通道」。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>
    /// 三支掉落类世界流事件的**唯一生产写者**(20 库存与物品,主机侧)。
    /// </summary>
    /// <remarks>
    /// <para><b>事件头</b>:<c>Patient = PatientId.None</c>(ADR-007 §四);<c>Seq = -1</c>
    /// 未发号哨兵,由 <c>EventStream</c> 发号(O-1)。</para>
    /// <para>⚠️ <b>有写者 ≠ 已接线</b>:生产触发点(29 死亡散落 / 18 起货溢出 / 25 敌人掉落 /
    /// 4 的拾取交互)全归各自装配轮。本类只保证「被正确调用时写对」。</para>
    /// </remarks>
    public sealed class DropEventWriter
    {
        private readonly IEventSink _eventSink;
        private readonly IPayloadEncoder _encoder;
        private readonly IIdAuthority _idAuthority;

        /// <summary>构造三支写者。</summary>
        /// <param name="eventSink">事件写入通道(主机唯一;生产 = 真 <c>EventStream</c>)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 §③ —— 唯一合法编码路径;
        /// 本类内不得出现 <c>new PayloadRef(</c>,由 b6 门强制)。</param>
        /// <param name="idAuthority">发号权威(ADR-005 · ADR-010 §五)——
        /// <c>DropSpawned</c> 是 <c>instance_id</c> 的**铸造点**(规则二 BL-1)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public DropEventWriter(IEventSink eventSink, IPayloadEncoder encoder, IIdAuthority idAuthority)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _idAuthority = idAuthority ?? throw new ArgumentNullException(nameof(idAuthority));
        }

        /// <summary>
        /// 铸造一个新实例并记其出生(规则二 BL-1:铸造点 = 本事件)。
        /// </summary>
        /// <param name="tick">出生 tick。</param>
        /// <param name="spawnAnchor">出生锚点(**整数格**;ADR-015 §三)。</param>
        /// <param name="itemKey">物品表 ordinal(21a 的整数枚举形;本类不查表)。</param>
        /// <param name="qty">初始份数(&gt; 0)。</param>
        /// <returns>本次铸造的实例 id —— 调用方(17 / 25 / 29)据此再发归属 / 采集事件。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> &lt; 0
        /// 或 <paramref name="itemKey"/> &lt; 0 或 <paramref name="qty"/> &lt; 1。</exception>
        /// <remarks>
        /// <para>⚠️ <b>为什么由本类铸造而不是调用方传 id 进来</b>:<c>IIdAuthority</c> 的唯一实现
        /// 住 `Sim/ItemDatabase`,而本类也住 `Sim` ⇒ 同一程序集内合法。若让 17 侧铸造,
        /// 17 就得持 `IIdAuthority` 并自己**手工包装** id —— 那会撞 AC-21a-63
        /// 的「手工铸造」字面门(2026-10-27 实测:S20-13 的 `CraftEventWriter` 正是这么红的)。
        /// ⇒ **接口 `IDropWriter.Spawn` 收 `ItemInstanceId` 是 17 侧的形状**(它收已铸 id);
        /// 20 侧的生产写者**自己铸**,两者不矛盾 —— 一个是「写入面形状」,一个是「生产实现」。</para>
        /// <para>⚠️ 铸造与落流之间**没有事务**:`NextItemInstanceId()` 前进后若
        /// `Append` 抛(正常路径不会,见 <see cref="InventoryTransaction.TicksAreAppendable"/>),
        /// 该号被跳过、流里没有对应事件 —— 高水位重构(`ItemInstanceHighWaterCollector`)
        /// 会把它算作「未使用」,下次再发同号。这是**允许**的:号空间只要不重号即可,
        /// 空号不是错误(ADR-006 机制 A 只要求「永不复位 0」与「max+1 重构」)。</para>
        /// </remarks>
        public ItemInstanceId RecordSpawned(long tick, WorldPos spawnAnchor, int itemKey, int qty)
        {
            // ⚠️ **校验必须先于铸号**(S20-15 甲案落地的第一处自查):若照最简写法
            //    `=> RecordSpawnedWithId(tick, _idAuthority.Next(), …)`,非法入参会在
            //    校验之前就**烧掉一个号** —— 机制 A 计数器前进后不可回退,下一次合法
            //    采集的 id 就跳号。这正是 `test_invalidRequest_throwsBeforeAnyWrite`
            //    里「零消耗 instance_id」那条断言的语义面。故此处显式分三步。
            ValidateSpawned(tick, itemKey, qty);

            ItemInstanceId instanceId = _idAuthority.NextItemInstanceId();
            RecordSpawnedWithId(tick, instanceId, spawnAnchor, itemKey, qty);

            return instanceId;
        }

        /// <summary>
        /// 用一个**已铸好**的实例 id 记其出生(铸造点在调用方,本方法只落流)。
        /// </summary>
        /// <param name="tick">出生 tick。</param>
        /// <param name="instanceId">调用方已铸的实例 id(<c>≥ 0</c>;本文档类不校验它「是否已存在」
        /// —— 那要读投影,归 fold 侧)。</param>
        /// <param name="spawnAnchor">出生锚点(**整数格**;ADR-015 §三)。</param>
        /// <param name="itemKey">物品表 ordinal。</param>
        /// <param name="qty">初始份数(&gt; 0)。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> &lt; 0
        /// 或 <paramref name="instanceId"/>.Value &lt; 0 或 <paramref name="itemKey"/> &lt; 0
        /// 或 <paramref name="qty"/> &lt; 1。</exception>
        /// <remarks>
        /// <para><b>为什么需要这个入口(S20-15 · 甲案 · R-3 双铸造点冲突的裁决)</b>:
        /// 17 的采集编排里,<c>DropSpawned</c> 的 id 在**写任何事件之前**就已铸好
        /// (<c>HarvestOrchestrator:256</c>,由 <c>test_r11_instanceIdIsMintedBeforeAnyWrite</c>
        /// 断「一次采集恰申请 1 个 id」与 AC-17-05「17 侧零本地铸造」共同钉住)。
        /// 若适配器改调 <see cref="RecordSpawned"/>(本类自铸),一条采集会**烧两个号**
        /// ⇒ id 空洞 + 上测直接红。故本入口收「已铸 id」,与自铸入口**共用同一条 Append 路径**
        /// (<see cref="AppendSpawned"/>)—— 两个入口是**同一个写者的两副形状**,不是两条写路径。</para>
        /// <para>⚠️ <b>谁铸</b>:自铸入口归 25 / 27 / 29 / 18 溢出(它们手上没有 id);
        /// 收 id 入口归 17 采集(R11 要求在出生事件之前就确定 id)。两者不矛盾。</para>
        /// </remarks>
        public void RecordSpawnedWithId(long tick, ItemInstanceId instanceId,
                                        WorldPos spawnAnchor, int itemKey, int qty)
        {
            ValidateSpawned(tick, itemKey, qty);
            if (instanceId.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(instanceId), instanceId.Value,
                    "instance_id 走 ADR-006 Amendment B 机制 A 计数器,不得为负");

            AppendSpawned(tick, instanceId, spawnAnchor, itemKey, qty);
        }

        private static void ValidateSpawned(long tick, int itemKey, int qty)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");
            if (itemKey < 0)
                throw new ArgumentOutOfRangeException(nameof(itemKey), itemKey,
                    "item_key 是物品表 ordinal(≥ 0);负值属畸形输入");
            if (qty < 1)
                throw new ArgumentOutOfRangeException(nameof(qty), qty,
                    "初始份数须 ≥ 1 —— 零份的实例不该存在(与 DropDespawned 归零出投影同口径)");
        }

        /// <summary><c>DropSpawned</c> 的**唯一 Append 路径**(两个公开入口共用)。</summary>
        private void AppendSpawned(long tick, ItemInstanceId instanceId,
                                   WorldPos spawnAnchor, int itemKey, int qty)
        {
            var payload = new DropSpawnedPayload(instanceId.Value, spawnAnchor, itemKey, qty);

            _eventSink.Append(new SimEvent(
                tick, PatientId.None, -1, EventKind.DropSpawned,
                _encoder.Encode(EventKind.DropSpawned, payload)));
        }

        /// <summary>
        /// 记一条归属变更(规则二 BL-1:`DropClaimed` **只改归属,不改发号**)。
        /// </summary>
        /// <param name="tick">归属变更 tick(拾取三段式中「判定通过」的那一拍)。</param>
        /// <param name="instanceId">被拾取的实例 id(须**已出生**,规则二)。</param>
        /// <param name="claimer">拾取者 actor id(≥ 0)。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> &lt; 0
        /// 或 <paramref name="claimer"/> &lt; 0。</exception>
        /// <remarks>
        /// <para>⚠️ <b>本方法不复核「实例存在 / 未归属」</b> —— 那要读投影,归
        /// <see cref="InventoryTransaction.ValidatePickup"/>;本类在裁决通过后被调用。
        /// 重复 claim 由 fold 侧报 `DuplicateClaim`(`InventoryFold.cs:354-368`),
        /// 那是**重建期**的完整性判据,不是本类的入参校验。</para>
        /// </remarks>
        public void RecordClaimed(long tick, ItemInstanceId instanceId, int claimer)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");
            if (claimer < 0)
                throw new ArgumentOutOfRangeException(nameof(claimer), claimer,
                    "claimer 是 actor id(≥ 0);负值属畸形输入(世界级事件才用 None 哨兵)");

            var payload = new DropClaimedPayload(instanceId.Value, claimer, tick);

            _eventSink.Append(new SimEvent(
                tick, PatientId.None, -1, EventKind.DropClaimed,
                _encoder.Encode(EventKind.DropClaimed, payload)));
        }

        /// <summary>
        /// 记一条实例减量 / 消亡(规则九:一次用药跨 N 栈 ⇒ **N 条**同 tick)。
        /// </summary>
        /// <param name="tick">减量 tick(须与同一次请求的其他条目**同 tick**)。</param>
        /// <param name="instanceId">被扣的实例 id。</param>
        /// <param name="qty">本次扣的**份数**(&gt; 0;≤ 该实例当前量 —— 归
        /// <see cref="InventoryTransaction.ValidateConsumption"/>)。</param>
        /// <param name="reason">消亡理由的**枚举序数**(过期 / 被消耗)。
        /// ⚠️ 档表归 R-2 定稿(`entities.yaml` 该条 constraint 明文);本类只搬运,
        /// **不定义**任何档值 —— 定义它 = 在 20 侧私设 enum,那正是 R-2 的活。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> &lt; 0
        /// 或 <paramref name="qty"/> &lt; 1 或 <paramref name="reason"/> &lt; 0。</exception>
        /// <remarks>
        /// <para>⚠️ <b>「N 条同 tick」是调用方的编排义务</b>,不是本方法能保证的:
        /// 一次请求跨 N 栈时,调用方须在**同一个 tick** 上逐条调本方法。
        /// `EventStream` 的 `Seq` 发号在同一 `(Tick, Patient)` 组内自增
        /// (`EventStream.cs:224-236`)⇒ 相邻 `Seq` 由**写入顺序**自然得到,
        /// 本类不需要也不应该自己算 `Seq`。</para>
        /// <para>⚠️ <b>整请求原子性不在本方法</b>:「任一实例不成立 ⇒ 一条都不发」是
        /// <see cref="InventoryTransaction.ValidateConsumption"/> 的职责(先全验,后逐条写)。
        /// 逐条调本方法而**先逐条验**—— 反过来(边验边写)就是半途状态。</para>
        /// </remarks>
        public void RecordDespawned(long tick, ItemInstanceId instanceId, int qty, int reason)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");
            if (qty < 1)
                throw new ArgumentOutOfRangeException(nameof(qty), qty,
                    "扣减份数须 ≥ 1 —— 零份的「减量」是无意义事件(不写比写更好)");
            if (reason < 0)
                throw new ArgumentOutOfRangeException(nameof(reason), reason,
                    "reason 是枚举序数(≥ 0);负值属畸形输入");

            var payload = new DropDespawnedPayload(instanceId.Value, qty, reason);

            _eventSink.Append(new SimEvent(
                tick, PatientId.None, -1, EventKind.DropDespawned,
                _encoder.Encode(EventKind.DropDespawned, payload)));
        }
    }
}
