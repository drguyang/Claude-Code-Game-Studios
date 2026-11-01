// S23-5 · story-005 —— 复活落点与传送命令(29 侧发令面;2026-11-01)
//
// ── 契约归属(ADR-025 §③ QQ-01 ①′)──────────────────────────────────────────
// 传送契约拆两半:**整数命令半** `ITeleportCommandSink.RequestTeleport(int, WorldPos)`
// 住 `Sim.Contracts`(本类消费它 —— **全案唯一跨门调用点**:29 结算 → 触发 1 传送);
// `Vector3` 连续半(实际位移/过场)住 `Gameplay.Presentation`,**物理上进不了本类**
// (门 A 程序集无引擎引用)。
//
// ── 29 零写流(AC-29-09)────────────────────────────────────────────────────
// 本类**零 `IEventSink` 触点** —— `ActorCellEntered` 由 1 的传送管线发(它读命令后
// 执行位移并落格事件),29 只发整数命令。复活**零流事件**(过场位移零写流,ADR-020 §四;
// 复活不重置疾病/世界 —— 副作用负面清单,story-002 掉落物原样留在 death_cell)。
//
// ── 落点优先级(两分支)──────────────────────────────────────────────────────
// ① 医馆床类家具格(24 提供,**运行期查询面未接线 = BLOCKED-BY-24 实现半**;
//    选定规则本体已落 24 GDD 规则六:`bed` 标签候选集 + `(y,x,z)` 字典序取 min ——
//    **禁**把「取第一个床格」当实现:遍历序非确定性源,规则六 ② 明写);
// ② 兜底 = 世界出生点(ADR-015 逻辑层固定格,烘焙数据,由装配层注入,非运行期发明)。
// 联机共床:两玩家同床格 ⇒ 同格两条命令(由 1 串行定序执行;29 侧逐结算即发,不批不并)。
//
// ── ⚠️ 桩面边界(1 侧未实现)───────────────────────────────────────────────
// `ITeleportCommandSink` 的生产实装归 1(玩家控制器/表现侧)—— 本卡按卡面口径
// 「接口先行 + 桩面明确标注」:29 侧发令面以真契约直测,1 侧消费(发 `ActorCellEntered`)
// 的联调维持 BLOCKED-BY-9/24 标注,不借绿。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 医馆床格查询端口(24 的运行期查询面;**实现归 24 轮,现零生产命中**)。
    /// <para>选定规则 = 24 GDD 规则六(确定性 `(y,x,z)` 字典序 min);实现方须守住
    /// 确定性 —— 本端口对实现零假设,只约「有/无」与「格」。</para>
    /// </summary>
    public interface IClinicReviveCell
    {
        /// <summary>尝试取该玩家的复活床格(有医馆房间且有床类家具格 ⇒ true)。</summary>
        bool TryGet(int actorId, out WorldPos cell);
    }

    /// <summary>
    /// 复活发令面:按落点优先级(床格 → 世界出生点)向 1 的传送契约发整数命令。
    /// </summary>
    /// <remarks>
    /// <para><b>死亡结算段末步</b>(story-001 管线):`PlayerDied → 掉落 → 掉级 → 复活命令`
    /// —— 由 <see cref="DeathCandidateConsumer"/> 的 `revive` 回调在结算段内调
    /// <see cref="Issue"/>(仅 `PlayerDied` 真写后;冷却内再死连结算都不进)。</para>
    /// </remarks>
    public sealed class RespawnCommandIssuer
    {
        private readonly ITeleportCommandSink _teleport;
        private readonly IClinicReviveCell _clinicCellOf;
        private readonly WorldPos _spawnCell;

        /// <summary>构造复活发令面。</summary>
        /// <param name="teleport">1 侧传送契约的整数半(ADR-025 QQ-01 ①′)。</param>
        /// <param name="clinicCellOf">医馆床格端口(24 提供;**可 null = 未接线** ——
        /// 现值,落点恒兜底出生点;24 实现轮装配真查询面)。</param>
        /// <param name="spawnCell">世界出生点(ADR-015 逻辑层固定格,装配层注入)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="teleport"/> 为 null。</exception>
        public RespawnCommandIssuer(ITeleportCommandSink teleport,
            IClinicReviveCell clinicCellOf, WorldPos spawnCell)
        {
            _teleport = teleport ?? throw new ArgumentNullException(nameof(teleport));
            _clinicCellOf = clinicCellOf;
            _spawnCell = spawnCell;
        }

        /// <summary>
        /// 发一条复活传送命令(整数格;零流事件 —— 过场位移零写流)。
        /// </summary>
        /// <param name="actorId">复活玩家 id。</param>
        /// <exception cref="ArgumentOutOfRangeException">actorId 为负。</exception>
        public void Issue(int actorId)
        {
            if (actorId < 0)
                throw new ArgumentOutOfRangeException(nameof(actorId), actorId,
                    "玩家 id 走机制 A 计数器,不得为负");

            // 落点优先级:床格(24,未接线 = 跳过)→ 兜底出生点(ADR-015 固定格)。
            WorldPos cell = (_clinicCellOf != null && _clinicCellOf.TryGet(actorId, out WorldPos bed))
                ? bed
                : _spawnCell;
            _teleport.RequestTeleport(actorId, cell);
        }
    }
}
