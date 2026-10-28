// S20-19 · 42 建造发起方接线 —— 扣料归属的**晚绑定槽**
//
// 权威来源:
//   design/gdd/inventory-and-items.md 规则六 + R16 甲案(2026-10-27 用户裁定):
//     **P0 单机 ⇒ 建造扣料恒归唯一玩家**;`InventoryFoldContext.SolePlayer` 是同一口径的
//     fold 侧快照位。
//   `BootRoot.cs:346`(玩家 actorId 在 `Assemble` **之后**才由 `IIdAuthority` 铸造)
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么需要这个槽,而不是在装配时把 owner 传成 int
// ─────────────────────────────────────────────────────────────────────────────
// `CompositionRoot.Assemble` 跑的时候**玩家还不存在** —— actorId 是装配完成后由
// `IIdAuthority.NextPatientId()` 铸的(`BootRoot` 的启动序)。这与既有的
// `PrescriptionPageHost.SetAuthor` / `CasebookSource.SetViewer` / `RandomEventsDirector.BindPlayer`
// 是**同一类后置绑定**,不是本批发明的形态。
//
// 槽把「归属」从**构造期**推到**绑定期**,且让两个消费者(`BuildStockQuery` 与
// `BuildController`)**共享同一个值** —— 各持一份 int 就会在绑定漏掉一处时
// 出现「库存门放行、扣料归到 -1」的分叉。
//
// ⚠️ **未绑定(Owner = -1)时一切拒绝**:`BuildMaterialPlanner` 按 owner 过滤,
//   -1 名下永远没有材料 ⇒ 建造恒 `InsufficientStock`。这是 fail-closed,不是静默放行。
// ⚠️ 本槽**只服务 P0 单机**;联机多玩家的扣料归因须走载荷补 actor 位(乙案,
//   `InventoryFold.cs:938` 已登记),届时本槽作废。

using System;

namespace DaYiJingCheng.Gameplay.Presentation.Building
{
    /// <summary>扣料归属的晚绑定槽(未绑定 = `-1`,一切建造拒绝)。</summary>
    public sealed class BuildOwnerSlot
    {
        /// <summary>未绑定哨兵(= `InventoryProjection.NoOwner`,但语义不同:这里是「还不知道是谁」)。</summary>
        public const int Unbound = -1;

        /// <summary>当前扣料归属(未绑定 = <see cref="Unbound"/>)。</summary>
        public int Owner { get; private set; } = Unbound;

        /// <summary>是否已绑定。</summary>
        public bool IsBound => Owner != Unbound;

        /// <summary>
        /// 绑定归属(玩家 actorId 铸造之后调,唯一生产调用点 = `BootRoot`)。
        /// </summary>
        /// <param name="actorId">玩家 actor id(≥ 0)。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="actorId"/> &lt; 0
        /// —— 负值是世界级事件的哨兵值,不是合法 actor。</exception>
        /// <exception cref="InvalidOperationException">已绑定到**另一个** id
        /// —— 重绑会让「库存门读 A、扣料归 B」短暂成立,须显式炸而不是覆盖。</exception>
        public void Bind(int actorId)
        {
            if (actorId < 0)
                throw new ArgumentOutOfRangeException(nameof(actorId), actorId,
                    "actor id 不得为负 —— 负值是 `PatientId.None` 一类的世界级哨兵,不是合法 actor");

            if (IsBound && Owner != actorId)
                throw new InvalidOperationException(
                    $"扣料归属已绑定到 actor={Owner},不允许重绑到 {actorId} —— "
                    + "重绑窗口内库存门与扣料会读不同的 id");

            Owner = actorId;
        }
    }
}
