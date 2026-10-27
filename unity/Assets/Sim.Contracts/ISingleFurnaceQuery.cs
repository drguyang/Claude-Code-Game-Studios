// S20-4 · processing story-001 —— 单炉空闲只读接缝(AC-18-10 的第三条件)
//
// 权威来源:
//   design/gdd/processing.md
//     · 规则八 并发模型表(:478-482)「第二炉点火(本玩家已有一炉在烧)⇒ 拒绝,零事件」
//     · OQ-18-3 已裁 [甲]:per-player 单炉,**无排队**(排队 = 进程态,与 OQ-18-5 冲突)
//     · AC-18-10 —— 候选集 ⟺ `Admissible ∧ owner=process ∧ 单炉空闲`
//   ADR-005 —— 单炉状态是**事件流的派生量**(FurnaceState 从 `Craft` 事件折叠),
//              不是独立可变状态;本接口只把它投影成「此刻忙不忙」这一位
//   ADR-025 §① —— 住 `Sim.Contracts`,引用集恰 = BCL
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么是**接缝**而不是直接调 `FurnaceState.IsBusy`
// ─────────────────────────────────────────────────────────────────────────────
// `FurnaceState.IsBusy(events, decoder, actorId, tick)` 要收**整条事件流 + 载荷解码器**。
// 把这两样塞进准入谓词会让它不再是 `(recipe, actor, consumedInstances)` 的纯函数:
//
//   ① **预答路径物理上拿不到全流** —— 客户端只持本地预表现,`IReadOnlyList<SimEvent>` 不存在。
//      而 AC-18-20 要求预答与终裁**调同一个谓词**;谓词签名若含事件流,两路径就不可能同源。
//   ② **三源不变量(ADR-016 §一)** —— 谓词输入须 ∈ {事件流, 烘焙数据, 二者的纯函数}。
//      「整条事件流」本身合法,但把它做成**参数**会让「同一 tick 不同切片 ⇒ 不同答案」成为
//      可表达状态;收成 `bool` 之后该失败模式在类型上不可表达。
//
// ⇒ 判定权威仍是 `FurnaceState`(它拥有折叠规则);本接口只是**那一问的形状**。
//   生产装配把 `FurnaceState.IsBusy(...)` 包一层实现它即可,不新增第二判据。
//
// ⚠️ 本接口**不声明**「空闲」的区间开闭 —— 那是 `FurnaceState.IsBusy` 的口径
//    (半开区间 `[start, start+duration)`,见该处 2026-10-26 评审 W2 注记)。
//    实现方照抄即可,本接口不重述、不覆盖。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>单炉空闲查询 —— 某玩家此刻是否已有一炉在烧(AC-18-10 第三条件的只读形)。</summary>
    /// <remarks>
    /// <para><b>语义</b>:per-player 单炉(OQ-18-3 裁 [甲])。跨玩家并发点火**允许**
    /// (GDD 规则八「多名玩家同时用同一器具」行),故查询键是 <c>actorId</c> 而非炉/格。</para>
    /// <para><b>纯查询</b>:实现方不得在本方法内推进任何状态(含缓存时间戳)。
    /// 谓词每 tick 可能被求值多次,带副作用的实现会让「同输入不同答案」变得可表达。</para>
    /// </remarks>
    public interface ISingleFurnaceQuery
    {
        /// <summary>该玩家此刻是否已有一炉在烧。</summary>
        /// <param name="actorId">玩家 id(与 <c>InventoryOf(player)</c> 的归因同一号空间)。</param>
        /// <returns>正在烧 ⇒ <c>true</c>(该玩家的候选集为空)。</returns>
        bool IsBusy(int actorId);
    }
}
