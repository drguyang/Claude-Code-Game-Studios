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
// ── 两个方法 = 7b 交互表的两行 ─────────────────────────────────────────
// 🔁 **2026-11-01 补轮**:上一条注写的是「本契约只有载入,'存盘'那一半由 7b 存档位轮
//    按同一形态接(契约加一个方法即可)」—— 本轮就是那一轮。现契约 = 载入 + 落笔。
//    ⚠️ `TrySave()` **不带槽位号**:目标槽 = 台账的 `NextWritableSeq`,归 7a
//    (`SaveSlotLedger` 是「下一可写槽」的唯一宿主)。让 42 传一个 seq 等于把
//    「现在该写第几号」复制到呈现层 = 第二份判据。
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

        /// <summary>
        /// 请求**落笔**:把当前世界存进「下一可写槽」(`save-slots-7b.md:202`)。
        /// <para>⚠️ **不带槽位号** —— 目标槽由 7a 的 `SaveSlotLedger.NextWritableSeq` 决定,
        /// 42 不持有「现在该写第几号」(ADR-013 §9 C3)。</para>
        /// <para>写入的四道门(叙事上下文 / 槽位只进不退 / 组版 / 写队列)全在
        /// `ISaveService.Checkpoint` 内,本契约**不复制任何一道**。</para>
        /// </summary>
        /// <returns><c>true</c> = 装配层已受理(已入写队列);<c>false</c> = 未受理
        /// (未接线 / 被门拒绝 —— 判定归装配层,42 只据此不给「成功」反馈)。</returns>
        bool TrySave();
    }
}
