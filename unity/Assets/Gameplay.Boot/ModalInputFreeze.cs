// 模态期的**输入冻结**(2026-10-31 · ADR-013 §十-C 的收尾件)。
//
// 权威来源:
//   · `design/gdd/interaction-system.md` 规则七(模态期交互意图被丢弃,两个方向)
//   · ADR-013 §十 / §十-C(42 是模态开集的唯一真源;4 侧只读布尔)
//   · `BuildInputDriver.cs` 头注 ①「不检查模态开集」—— **本类就是那条已知边界的收口**
//
// ── 为什么单开一个类,而不是在 `BootRoot.Update` 里写个 if ─────────────
// 规则七的两向抑制此前只覆盖 **E 查体 / F 急救**(`InteractionInputDriver`)与
// **建造四动作**(本轮补),但**移动轴**一直漏着 —— 暂停壳摊开时玩家照样走得动,
// 「暂停」名不副实。三处都要问同一个问题,答案必须出自**同一个读面**
// (`IModalGateState.IsOpen`),否则就是三份各自演化的「什么算挡路」。
//
// ⚠️ 本类**只做读取与折零**,不判定「暂停该不该冻世界」:
//   世界 tick **照常推进**(sim 侧无冻结机制,那是另一个裁定);
//   本类挡的是**输入意图**。两者的分界见 ADR-013 §十-C 的「未做」段。

using System;
using UnityEngine;                                 // Vector2(仅限装配层,不进 sim)

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>模态挡路期的输入冻结(移动轴 + 建造动作的**唯一**判定点)。</summary>
    public sealed class ModalInputFreeze
    {
        private readonly Func<bool> _blocked;

        /// <summary>构造。</summary>
        /// <param name="blocked">是否有挡路的模态摊开(生产面 = `ModalGateStateAdapter.IsOpen`)。
        /// 为 <c>null</c> ⇒ 恒不冻结(诚实降级:无 UI ⇒ 无模态)。</param>
        public ModalInputFreeze(Func<bool> blocked)
        {
            _blocked = blocked;
        }

        /// <summary>当前是否冻结输入(实时读,零缓存 —— 缓存 = 第二真源)。</summary>
        public bool IsBlocked => _blocked != null && _blocked();

        /// <summary>
        /// 折零移动轴。
        /// <para>⚠️ 返回 <c>Vector2.zero</c> 而**不是**让调用方跳过 `PumpFrame` ——
        /// tick 泵仍须每帧推进(世界不冻),只是玩家不动。</para>
        /// </summary>
        public Vector2 FilterMovement(Vector2 moveAxis) => IsBlocked ? Vector2.zero : moveAxis;
    }
}
