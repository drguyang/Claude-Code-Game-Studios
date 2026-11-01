// ============================================================================
// 传送命令实装(1 侧)—— ITeleportCommandSink 的**生产实现**(S23 · 2026-11-01)
//
// ── 契约位置(ADR-025 §③ QQ-01 ①′)──────────────────────────────────────────
// 整数命令半 `ITeleportCommandSink.RequestTeleport(int, WorldPos)` 住 Sim.Contracts;
// **Vector3 连续半(本类)住 Gameplay.Presentation** —— 全案唯一跨门调用点
// (29 结算 → 触发 1 传送)在此接缝越过门 A。
//
// ── 写流归属(AC-29-09 的另一半)────────────────────────────────────────────
// 本类**零直接 Append** —— `ActorCellEntered` 由 `PlayerController` 的跨格提交链发
// (它读完 Teleport 后的新格,经其唯一 Append 点落流;ADR-020 Amendment B:格事件的
// 写者 = 1)。本类只做:整数格 → 连续坐标(格心 = cell + 0.5)→ `Teleport`。
//
// ── 迟绑定 ──────────────────────────────────────────────────────────────────
// `PlayerController` 由 BootRoot 在世界装配后生成;本类经 `BindPlayer` 后置绑定
// (CompositionRoot 构造、BootRoot 绑定的时序差),**未绑定即请求 ⇒ 抛**(fail-loud,
// 禁静默吞复活)。
// ============================================================================

using System;
using DaYiJingCheng.Sim.Contracts;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Player
{
    /// <summary>
    /// 传送命令的生产实装(1 侧):整数格 → 格心连续坐标 →
    /// <see cref="PlayerController.Teleport"/>(跨格事件由控制器提交链发,本类零 Append)。
    /// </summary>
    public sealed class TeleportCommandSink : ITeleportCommandSink
    {
        private Func<PlayerController> _playerOf;

        /// <summary>
        /// 玩家是否已绑定(装配层判定「复活传送可发令」的依据)。
        /// <para>⚠️ **未绑定 ≠ 复活失败**:纯 sim 装配(EditMode 夹具 / 无 BootRoot 的路径)
        /// 根本没有玩家对象 ⇒ 复活传送**无接收者**,此时 sim 侧不该因表现层缺席而崩
        /// (呈现三件套纪律:sim 不因呈现层未就绪而抛)。装配层据此**跳过发令并告警**,
        /// 而本类的 <see cref="RequestTeleport"/> 仍保持 fail-loud(真发令却无接收者 = 编程错误)。</para>
        /// </summary>
        public bool IsBound => _playerOf != null;

        /// <summary>后置绑定本地玩家(BootRoot 装配段一次性;未绑定即请求 ⇒ 抛)。</summary>
        /// <exception cref="InvalidOperationException">二次绑定(玩家唯一)。</exception>
        public void BindPlayer(Func<PlayerController> playerOf)
        {
            if (_playerOf != null)
                throw new InvalidOperationException("TeleportCommandSink 玩家已绑定 —— 二次绑定 = 静默换真源,拒绝");
            _playerOf = playerOf ?? throw new ArgumentNullException(nameof(playerOf));
        }

        /// <inheritdoc />
        public void RequestTeleport(int actorId, WorldPos cell)
        {
            if (actorId < 0)
                throw new ArgumentOutOfRangeException(nameof(actorId), actorId,
                    "actor id 走机制 A 计数器,不得为负");

            PlayerController player = _playerOf != null
                ? _playerOf()
                : throw new InvalidOperationException(
                    "TeleportCommandSink 玩家未绑定 —— 复活传送会静默丢失;装配须先 BindPlayer(fail-loud)");
            if (player == null)
                throw new InvalidOperationException(
                    "TeleportCommandSink 绑定的玩家读数为 null(玩家未生成/已销毁)—— 复活传送 fail-loud");

            // 整数格 → 格心(CellTransitionDetector.CellFromPosition = FloorToInt 的逆映射)。
            // y 同取格心 —— 控制器落地由 CharacterController/重力收敛。
            player.Teleport(new Vector3(cell.X + 0.5f, cell.Y + 0.5f, cell.Z + 0.5f));
            // ActorCellEntered 由 PlayerController 的跨格提交链发(唯一写者 = 1)—— 本类零 Append。
        }
    }
}
