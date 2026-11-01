// S23 · story-006 —— 死亡/复活过渡的**投影查询**(sim 事实 → DeathTransitionDto;2026-11-01)
//
// ── 它是什么 ────────────────────────────────────────────────────────────────
// 纯函数:扫三流,把「该 actor 当前处于过渡的哪个 sim 相位」投影成
// `DeathTransitionDto`(全整数;spec `death-transition-29.md` §10)。
// 呈现层(42/表现)只读本 DTO + 自己的动画时长推视觉相位 —— **本类零写路径**
// (AC「呈现不持状态」;重开呈现层不改变任何 sim 事实)。
//
// ── 相位判据(倒序单遍,与 DeathCellQuery / DeathDropReclaimPolicy 同口径)──
//   ① 倒序找该 actor **最近一条 `PlayerDied`** —— 查无 ⇒ `PhaseNone`;
//   ② 有死亡事件(tick T)⇒ 倒序找该 actor 在 **T 之后**的 `ActorCellEntered`
//      (复活落格已落地,story-005 的传送执行体发)⇒ 有 ⇒ `PhaseReviving`,无 ⇒ `PhaseDowned`。
// ⚠️ 活着的玩家日常跨格**不会**误报 Reviving —— 判据锚在「最近一次死亡」之后,
//   没死过的人走不到 ②(有界性:每次调用至多扫两遍流,读侧-only)。
//
// ── 白名单纪律(story-006 AC-29-06 的结构面)────────────────────────────────
// 死亡呈现路径的类型闭集 = {`DeathTransitionDto`, `DeathTransitionQuery`}
// (+ 42 侧的水墨元件/动画资产,不属本类登记面)。`GameOver` / `Retry` / 统计面板
// **不在闭集 = 出现即红**(夹具负控钉住判别力)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 死亡/复活过渡投影:倒序扫流,产出 <see cref="DeathTransitionDto"/>(只读;零写路径)。
    /// </summary>
    public static class DeathTransitionQuery
    {
        /// <summary>投影该 actor 的过渡相位。</summary>
        /// <param name="events">三流只读视图(域外 Kind 自动跳过)。</param>
        /// <param name="decoder">载荷解码器。</param>
        /// <param name="actorId">目标 actor id。</param>
        /// <param name="isLocal">本地性(1 = 本地全屏过渡,0 = 远端局部标识;
        /// 装配层传入 —— 本地性是设备事实,不是流事实)。</param>
        /// <returns>只读投影(查无死亡事实 ⇒ `PhaseNone`)。</returns>
        /// <exception cref="ArgumentNullException">events / decoder 为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">actorId 为负 / isLocal 非 0/1。</exception>
        public static DeathTransitionDto Project(IReadOnlyList<SimEvent> events,
            IPayloadDecoder decoder, int actorId, int isLocal)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (decoder == null) throw new ArgumentNullException(nameof(decoder));
            if (actorId < 0)
                throw new ArgumentOutOfRangeException(nameof(actorId), actorId,
                    "actor id 走机制 A 计数器,不得为负");
            if (isLocal != 0 && isLocal != 1)
                throw new ArgumentOutOfRangeException(nameof(isLocal), isLocal,
                    "isLocal ∈ {0, 1}(设备事实,由装配层判定)");

            // ① 最近一条 PlayerDied(倒序)
            long deathTick = -1;
            for (int i = events.Count - 1; i >= 0; i--)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.PlayerDied) continue;
                PlayerDiedPayload p = decoder.Decode<PlayerDiedPayload>(e.Kind, e.Payload);
                if (p.ActorId != actorId) continue;
                deathTick = p.Tick;
                break;
            }
            if (deathTick < 0)
                return new DeathTransitionDto(DeathTransitionDto.PhaseNone, isLocal);

            // ② 死亡之后是否有该 actor 的复活落格(倒序)
            for (int i = events.Count - 1; i >= 0; i--)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.ActorCellEntered) continue;
                if (e.Tick <= deathTick) continue;   // 倒序首条即最新;≤ 死亡 tick 的不是复活
                ActorCellEnteredPayload p = decoder.Decode<ActorCellEnteredPayload>(e.Kind, e.Payload);
                if (p.ActorId != actorId) continue;
                return new DeathTransitionDto(DeathTransitionDto.PhaseReviving, isLocal);
            }
            return new DeathTransitionDto(DeathTransitionDto.PhaseDowned, isLocal);
        }
    }
}
