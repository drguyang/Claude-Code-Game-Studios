// S23-6(2026-11-01)· BootRoot 侧的 **E 按下沿路由** —— 拾取 claim 优先 → 采集 → 查体
//
// ── 为什么是独立类(不是 BootRoot 内联 lambda)────────────────────────────────
// BootRoot 是 MonoBehaviour:装配 lambda 写在它体内 ⇒ EditMode 不可直测。
// 「拾取优先于采集」是**路由判定**,必须有夹具(F6 教训:注释即接线 = 假绿形态)——
// 本类把它独立成静态纯函数,S23-6 夹具直接测序。
//
// ── 路由序(轮内就地裁定,已回写 sprint 卡;🔁 2026-11-02 加第二段的比距)────────
// ① 拾取 claim(地上的东西 = 玩家的「拿取」affordance,优先于环境资源点);
// ② 采集(S22-9 桥;**跨种类比距** —— 只在比最近病人更近时才抢;
//    闸门拒绝也算消费 —— 拒采的同时又体检 = 一次按键两个动作);
// ③ 都不消费 ⇒ false,就诊/查体照常(它们在 InteractionInputDriver 内,不在本类)。
//
// 🔁 **2026-11-02(开册入口轮)**:原第二段是「半径内**存在**采集点就消费」。
//   现世界 `r_interact = 3` 覆盖全部可站格 ⇒ 那条判据让**每一次 E 都被采集拿走**,
//   就诊 → 立案 → 开册整条链在运行期不可达(实测:出生格 `harvest=True`、病人格 `harvest=True`)。
//   改为比距后,「站在病人身上按 E」判病人更近 ⇒ 采集让路。
//   ⚠️ 这仍是 **F-4.1 的部分实现**(只比第一键 `d∞`,缺 `KindPriority` / `StableId`),见 `TryConsume` 的 param 注。
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
        /// <param name="nearestPatientDistance">
        /// 最近的在场病人的 `d∞` 距离(<see cref="int.MaxValue"/> = 无在场病人)。
        /// <para>🔁 **2026-11-02 新增(跨种类比距)**:采集只有在**比最近病人更近**时才消费按键。
        /// 原实现是「半径内只要有采集点就吞」—— 而现世界 `r_interact = 3` 覆盖全部可站格
        /// ⇒ **每一次 E 都被采集拿走,就诊/开册永不可达**(实测:出生格与病人格 `harvest=True`)。</para>
        /// <para>⚠️ <b>这是 F-4.1 的**部分**实现</b>,不是它本身:只比了第一键(`d∞`),
        /// 第二键 `KindPriority` / 第三键 `StableId` 仍缺(等距时本函数判**病人胜**,
        /// 而真全序应按 `KindPriority` 定)。补齐须把病人接成 4 候选源并走同一次 `Select` —— 另立债。</para>
        /// </param>
        /// <returns>true = 按键被拾取链或采集链消费(就诊/查体不触发);
        /// false = 都不消费 ⇒ 就诊/查体照常。</returns>
        public static bool TryConsume(DropClaimOrchestrator claim, HarvestInteractionBridge harvest,
            int actorId, long tick, WorldPos playerCell, int nearestPatientDistance)
        {
            // ① 拾取:物理上「脚下就有东西」,最高优先(既有裁定,未改)
            if (claim != null && claim.TryClaim(actorId, tick, playerCell)) return true;

            // ② 采集:**跨种类比距** —— 只有比最近病人更近才抢
            if (harvest == null) return false;
            return harvest.PeekNearestForageDistance() < nearestPatientDistance
                   && harvest.ClaimInteract();
        }
    }
}
