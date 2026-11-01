// S23-6(2026-11-01)· BootRoot 侧的 **E 按下沿路由** —— 拾取 claim 优先 → 采集 → 查体
//
// ── 为什么是独立类(不是 BootRoot 内联 lambda)────────────────────────────────
// BootRoot 是 MonoBehaviour:装配 lambda 写在它体内 ⇒ EditMode 不可直测。
// 「拾取优先于采集」是**路由判定**,必须有夹具(F6 教训:注释即接线 = 假绿形态)——
// 本类把它独立成静态纯函数,S23-6 夹具直接测序。
//
// ── 路由序(轮内就地裁定,已回写 sprint 卡)──────────────────────────────────
// ① 拾取 claim(地上的东西 = 玩家的「拿取」affordance,优先于环境资源点);
// ② 采集(S22-9 桥;闸门拒绝也算消费 —— 拒采的同时又查体 = 一次按键两个动作);
// ③ 都不消费 ⇒ false,查体照常(查体在 InteractionInputDriver 内,不在本类)。
// 拾取拒 ⇒ **零事件**落回采集链(编排面 guarantee);单一边沿所有者不变
// (判定都在 InteractionInputDriver 的边沿之内,两支都不另持边沿位)。

using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>E 按下沿的 claim 路由:拾取优先 → 采集;回报按键是否被消费。</summary>
    public static class InteractClaimRoute
    {
        /// <summary>尝试把一次交互按键消费掉(拾取 → 采集)。</summary>
        /// <param name="claim">拾取编排(S23-6;可 null = 未装配)。</param>
        /// <param name="harvest">采集桥(S22-9;可 null = 未装配)。</param>
        /// <param name="actorId">玩家 id(拾取的归属面)。</param>
        /// <param name="tick">当前逻辑 tick。</param>
        /// <param name="playerCell">玩家**经流确立格**(ADR-020 §四;两支同源读法)。</param>
        /// <returns>true = 按键被拾取链或采集链消费(查体不触发);
        /// false = 两支都不消费 ⇒ 查体照常。</returns>
        public static bool TryConsume(DropClaimOrchestrator claim, HarvestInteractionBridge harvest,
            int actorId, long tick, WorldPos playerCell)
            => (claim != null && claim.TryClaim(actorId, tick, playerCell))
               || (harvest != null && harvest.ClaimInteract());
    }
}
