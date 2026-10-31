// 7b 存档位 → 装配层的**载入意图**契约(TD-persist-04 裁定的「通电待信」那一段的 42 侧半)。
//
// 权威来源:
//   · design/gdd/save-slot-ui.md(7b —— 槽位语义 · 规则三「读档即锁」)
//   · design/ux/save-slots-7b.md §交互(「载入 = 无 SimEvent · `ISaveService.Load(SaveSlot)`」)
//   · ADR-013 §9 C3(42 只渲染,**永不持有游戏状态**)
//   · ADR-025 §①(装配方向不可倒:UI 不反向引用 Boot)
//   · 同型先例:`Skeuomorphic/CasebookBinding.cs` 的 `ICasebookCommandSink`
//     —— 42 定契约、装配层实现、驱动注入。
//
// ── 为什么本契约只有「载入」 ────────────────────────────────────────────
// 本轮的目的是把 `BootRoot.RequestLoad` 从零调用方变成**有调用方**,而不是把 7b 做全。
// 「存盘」那一半走 `Checkpoint`,由 7b 存档位轮按同一形态接(契约加一个方法即可),
// 不在本轮面内 —— **不写一个只有注释没有调用点的 `TrySave`**。
//
// ⚠️ 返回值语义与「不崩」:载入会抛(槽位损坏 / 载入安全上下文门 / 版本门拒收,
//    ADR-010 §七 的非致命项不在此列)。**42 不处理异常,也不吞** —— 由装配层实现方
//    决定记日志还是上抛;本契约只表达「这次载入意图成没成」。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>存档位屏的载入意图出口(42 侧契约;实现方住装配层)。</summary>
    public interface ISaveSlotCommandSink
    {
        /// <summary>请求载入某个槽位。</summary>
        /// <param name="slotSeq">槽位序号(`SaveSlot.SlotSeq` 的呈现侧同名量)。</param>
        /// <returns><c>true</c> = 装配层已受理并完成载入;<c>false</c> = 未受理
        /// (未接线 / 载入失败 —— 判定归装配层,42 只据此不给「成功」反馈)。</returns>
        bool TryLoad(uint slotSeq);
    }
}
