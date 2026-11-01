// S23-2 · story-002 —— 死亡散落的**结算面**(29;2026-11-01)
//
// ── 流形状(本类是「玩家携带物品落地」的唯一生产写入编排)───────────────────────
// 对玩家携带的每个 `ItemInstance` i(GDD `death-and-respawn.md:136-144` 规则三):
//
//     ① `DropDespawned { i.id, i.qty, reason = DeathDrop }`   —— 消耗持有
//     ② `DropSpawned  { i.id, spawn_anchor = death_cell, i.key, i.qty }` —— **同 id 落地**
//
// ⚠️ **为什么必须先 Despawn 再 Spawn(GDD 伪码只写了 Spawn,此处是它的机械补全)**:
//   `InventoryFold` 对「已出生且未归零」实例的二次 `DropSpawned` 报
//   `DuplicateInstanceId` 缺口(ADR-006 机制 A 不变量②的守卫);且只 Spawn 不消耗
//   会让同一件物品**同时**在玩家库存与地面各一份(fold 双行)。先归零再出生,
//   fold 的两段处置恰好各吃一条(「归零即出投影」→「同 id 可再出生」),清单重放逐位重建。
//   本决定已回写 story-002 Implementation Notes(2026-11-01 注)。
//
// ── 「29 零铸造」的落实(AC 的结构判据)─────────────────────────────────────────
// 本类**不触 `IIdAuthority`** —— 载荷 id = 20 库存行 id **原样**(GDD `:142`
// `instance_id = i.id`;AC「载荷 id 与 20 库存行一致」)。`RecordSpawnedWithId` 是
// 20 写者为「已铸 id 出生」留的通道(23 拆除返还同款);新铸通道(`RecordSpawned`)
// 17 采集专用 —— 两条通道在同一个类里,保「同一批走同一个 sink」。
//
// ── 投影来源的后置绑定 ────────────────────────────────────────────────────────
// 库存投影是**派生态**(每次结算重 fold,不持快照 —— 持快照 = 载重停在装配那一刻)。
// 来源(`InventoryLoadService`)在装配序列里**晚于**死亡消费链构造,故走
// `BindProjectionSource` 后置绑定(与 `SetAuthor` / `SetViewer` / `BindPlayer` 同格):
// **未绑定即 `Settle` ⇒ 抛**(fail-closed —— 静默空投影 = 「背包全空」的静默失败);
// **二次绑定 ⇒ 抛**(来源是唯一真相,换源 = 静默换真源)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 死亡散落结算:把玩家携带的每件物品写成「消耗持有 + 同 id 落地」两条世界流事件。
    /// </summary>
    public sealed class DeathDropSettlement
    {
        /// <summary>
        /// `DropDespawned.reason` 的**死亡散落**序数。
        /// <para>⚠️ R-2 的 reason 枚举表**未定稿**(`WorldPayloads.cs:70` 自陈「档表归 R-2 定稿」),
        /// 本常量是首个生产取值的**占序登记**:R-2 定稿时若挪号,**必须**与本常量同批改
        /// (否则流里已写出的 0 会被新表读成别的语义)。登记归 R-2 / entities.yaml。</para>
        /// </summary>
        public const int DespawnReasonDeathDrop = 0;

        private readonly DropEventWriter _drops;
        private Func<InventoryProjection> _projectionOf;

        /// <summary>构造结算面(投影来源**必须**经 <see cref="BindProjectionSource"/> 后置绑定)。</summary>
        /// <param name="drops">20 的三支掉落写者(全案唯一发出方;本类不直接 Append)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="drops"/> 为 null。</exception>
        public DeathDropSettlement(DropEventWriter drops)
        {
            _drops = drops ?? throw new ArgumentNullException(nameof(drops));
        }

        /// <summary>后置绑定投影来源(装配轮一次性;承后置绑定同格纪律)。</summary>
        /// <exception cref="ArgumentNullException">委托为 null。</exception>
        /// <exception cref="InvalidOperationException">二次绑定(来源唯一,换源 = 静默换真源)。</exception>
        public void BindProjectionSource(Func<InventoryProjection> projectionOf)
        {
            if (_projectionOf != null)
                throw new InvalidOperationException(
                    "DeathDropSettlement 的投影来源已绑定 —— 二次绑定 = 静默换真源,拒绝");
            _projectionOf = projectionOf ?? throw new ArgumentNullException(nameof(projectionOf));
        }

        /// <summary>
        /// 死亡散落:对 <paramref name="actorId"/> 携带的每件物品写「消耗持有 + 同 id 落地」。
        /// </summary>
        /// <param name="tick">死亡判定的逻辑 tick(与 `PlayerDied` 同 tick —— 结算段第二位)。</param>
        /// <param name="actorId">玩家 id(投影的 owner 面)。</param>
        /// <param name="deathCell">死亡所在整数格(= `PlayerDied` 载荷携带的同一格;
        /// 调用方传一次,本类不重查流)。</param>
        /// <returns>散落件数(= 写出的 `DropSpawned` 条数;空背包 = 0,且**零事件**)。</returns>
        /// <exception cref="ArgumentOutOfRangeException">tick / actorId 为负。</exception>
        /// <exception cref="InvalidOperationException">投影来源未绑定。</exception>
        public int Settle(long tick, int actorId, WorldPos deathCell)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");
            if (actorId < 0)
                throw new ArgumentOutOfRangeException(nameof(actorId), actorId,
                    "玩家 id 走机制 A 计数器,不得为负");
            if (_projectionOf == null)
                throw new InvalidOperationException(
                    "投影来源未绑定 —— 结算会静默退化成「背包全空」;装配须先 BindProjectionSource");

            // 派生态**每次重取**(不持快照);实例序 = `InstancesOf` 的 instance_id 升序
            // (投影文档钉的确定性序 —— 换页/摆放同读这一序,非字典迭代序)。
            InventoryProjection projection = _projectionOf();
            IReadOnlyList<long> instances = projection.InstancesOf(actorId);

            int dropped = 0;
            for (int i = 0; i < instances.Count; i++)
            {
                // TryGet 理论不可达(InstancesOf 与行表同源);守住 fail-closed 而非静默跳过。
                if (!projection.TryGet(instances[i], out InventoryRow row))
                    throw new InvalidOperationException(
                        $"投影不一致:InstancesOf 含 {instances[i]} 但行表查无 —— 派生态损坏");

                // ① 消耗持有(一实例一条,OQ-20-10 ①;reason 占序见常量注)。
                //    裸 long 重载 —— 投影行 id 原样,免 struct 包装(AC-17-05 IL 门:
                //    Sim 内 ItemInstanceId 构造点唯一落权威;fold 同款裸 long 惯例)。
                _drops.RecordDespawned(tick, row.InstanceId, row.Qty, DespawnReasonDeathDrop);
                // ② 同 id 落地(spawn_anchor = death_cell 整数格;quality 不在载荷 —— R18 账)。
                _drops.RecordSpawnedWithId(tick, row.InstanceId, deathCell,
                    row.ItemOrdinal, row.Qty);
                dropped++;
            }
            return dropped;
        }
    }
}
