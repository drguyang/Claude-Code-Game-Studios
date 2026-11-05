// ADR-010 §六 / 7b F-7b.2 —— 槽位两投影(纯函数)。
//
// 权威来源:
//   design/gdd/save-slot-ui.md:206-207 —— 两条公式
//     🔁 2026-11-04(ADR-041 §D2/§D3,用户裁定「覆盖合法」):Writable 删去 ¬Locked 与高水位两条子句
//     🔁 2026-11-06(用户裁定「锁搬到写侧」,ADR-042 同批):
//     Writable(slot) ⟺ ¬Locked(slot) ∧ InNarrativeContext(t)       ← ¬Locked 回到写入门
//     Loadable(slot) ⟺ InNarrativeContext(t)                       ← 摘掉 ¬Locked(读可反复)
//   design/gdd/save-slot-ui.md:212-214 —— 三个输入量(Locked / InNarrativeContext /
//     next_writable_seq)**全部归 7a**,7b 只呈现
//   design/gdd/persistence-service.md 规则十四 —— 只进不退 / 读档即锁 / 载入安全上下文门
//
// 为什么拆成「纯投影 + 有状态台账」两件(S19-1 判据 ③,2026-10-24 三家专家合成裁定):
//   公式里的 `next_writable_seq` 与 `Locked(slot)` 是**跨调用累积的状态**
//   (GDD 规则十四「只进不退」/「锁持续到下一次叙事事件 checkpoint」),
//   不可能是纯函数。但判据 ③ 的对象是**这两个函数**,不是它们读的那个量。
//   ⇒ 本文件只放**零实例字段**的静态投影;状态归 `SaveSlotLedger`(另一文件),
//     两者以 `SlotContext`(不可变快照)相接。

using System;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>
    /// 槽位投影的**全部外部输入**(不可变)。
    /// <para>构造一次、只读使用 —— 这使「同输入同输出」在类型层面可读,
    /// 而非靠「实现里别写缓存」的纪律。</para>
    /// <para><b><see cref="Locked"/> 是针对「本次查询的那个槽位」的锁态</b>
    /// (GDD `:212` 的 `Locked(slot)` 是槽位函数,不是全局量)⇒ 查询不同槽位须构造不同上下文。</para>
    /// </summary>
    public readonly struct SlotContext
    {
        /// <summary>该槽位是否已被读档锁住(规则十四「读档即锁」)。</summary>
        public readonly bool Locked;

        /// <summary>
        /// 当下是否在叙事上下文内(`InNarrativeContext(t)`)。
        /// <para><b>这是一个量</b>(GDD `:212` / `:219` 逐字「与手动存**共用同一观察钩子**」)——
        /// 不是「叙事上下文」与「未决医疗动作」两个正交查询。</para>
        /// </summary>
        public readonly bool InNarrativeContext;

        /// <summary>下一可写槽位序号(规则十四「只进不退」的高水位)。</summary>
        public readonly uint NextWritableSeq;

        /// <summary>
        /// 构造一次快照。三个形参逐一对应三个 readonly 字段,**无校验、无默认值** ——
        /// 本类型是纯数据载体,合法性由调用方(<see cref="SaveSlotLedger.Snapshot"/>)负责。
        /// </summary>
        /// <param name="locked">该槽位是否已被读档锁住。</param>
        /// <param name="inNarrativeContext">当下是否在叙事上下文内(一个量,见字段说明)。</param>
        /// <param name="nextWritableSeq">下一可写槽位序号。</param>
        public SlotContext(bool locked, bool inNarrativeContext, uint nextWritableSeq)
        {
            Locked = locked;
            InNarrativeContext = inNarrativeContext;
            NextWritableSeq = nextWritableSeq;
        }
    }

    /// <summary>
    /// 7b `F-7b.2` 的两条槽位投影 —— **纯函数,零实例字段**。
    /// <para>判据 ③ 的反射断言对象就是本类型与 <see cref="SlotContext"/>;
    /// 状态宿主 <see cref="SaveSlotLedger"/> **不在本判据面内**(它按定义是有状态的)。</para>
    /// <para>⚠️ <b>本类型今日零生产消费者</b>(`/code-review` S19-1 C-4 实测):
    /// `grep -rn "SlotProjections\." unity/Assets --include=*.cs | grep -v /Tests/` ⇒ <b>0 行</b>。
    /// 生产路径用的是两处**手写等价物**:
    /// ① `SaveSlotLedger.EnsureWritable`(`:88-101`,判 `slot_seq` 与上下文,**不查锁**);
    /// ② `SaveService.Load`(`:265-273`,判锁与上下文,**不查 `slot_seq`**)。
    /// ⇒ 判据 ③ 的绿证明的是**本类型自身**的纯度,<b>不证明生产路径走的就是这两条公式</b>。
    /// 风险 = 三份公式日后分叉时测试不会红。归 `TD-persist-03` 同批收敛
    /// (让 `EnsureWritable` / `Load` 改调本类型,或加「内联判定 ≡ 投影」对拍夹具)。</para>
    /// </summary>
    public static class SlotProjections
    {
        /// <summary>
        /// `Writable(slot) ⟺ ¬Locked ∧ InNarrativeContext`。
        /// <para>🔁 **2026-11-06(用户裁定,ADR-042 同批)锁从读侧搬到写侧** ——
        /// `¬Locked` **回到写入门**,并从 <see cref="Loadable"/> **移除**。
        /// 旧口径(2026-11-04 ADR-041 §D2/D3)是「锁拒读、覆写合法」;用户裁定反转为
        /// 「<b>锁挡覆写、读可反复</b>」:</para>
        /// <list type="bullet">
        ///   <item>`slot_seq ≥ next_writable_seq`(只进不退)**仍然不挡写** —— ADR-041 §D2 的
        ///         那半条裁定不动(未锁槽的覆写依旧合法);</item>
        ///   <item>`¬Locked` **恢复为写入门** —— 已锁 = 你刚从这里读回来,不许把当前进度盖回去。</item>
        /// </list>
        /// <para>槽位号仍作形参保留(调用方语义、签名稳定),但**不参与判定**。</para>
        /// </summary>
        public static bool Writable(SaveSlot slot, in SlotContext ctx)
            => !ctx.Locked && ctx.InNarrativeContext;

        /// <summary>
        /// `Loadable(slot) ⟺ InNarrativeContext`。
        /// <para>**刻意不读 <see cref="SlotContext.NextWritableSeq"/>** ——
        /// 载入与高水位无关。</para>
        /// <para>🔁 **2026-11-06**:载入**不再看锁**(锁搬到写侧,见 <see cref="Writable"/>)。
        /// 屏上的呈现随之反转:已锁行<b>整行灰底</b>、二段态里「启封」**墨字可用**、
        /// 「重写」灰置不可获焦。</para>
        /// </summary>
        public static bool Loadable(SaveSlot slot, in SlotContext ctx)
            => ctx.InNarrativeContext;
    }
}
