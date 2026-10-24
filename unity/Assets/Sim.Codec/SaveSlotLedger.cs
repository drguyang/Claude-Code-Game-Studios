// ADR-010 §六 规则十四 —— 槽位台账(有状态)。
//
// 权威来源:
//   design/gdd/persistence-service.md 规则十四:
//     · 槽位只进不退:`Checkpoint(slot)` 只能写按时间序递增的槽位,覆盖已有槽位 = 7a 拒绝
//     · 读档即锁:从某槽位载入后,该槽位在同一会话内锁定 reads 侧,锁持续到下一次叙事事件 checkpoint
//     · 载入安全上下文门:读档与写同钩,共用同一观察钩子
//   design/gdd/save-slot-ui.md:212-214 —— 三个量全归 7a
//   ADR-010 §六 —— 「锁字段不进存档体」:锁与安全门均**会话内存**,跨会话复位
//
// 与 SlotProjections 的分工(S19-1 判据 ③):
//   `SlotProjections` = 纯函数(零状态),`SlotContext` = 它收到的不可变输入,
//   本类 = **唯一的状态宿主**(持 Locked 集 / next_writable_seq / 叙事上下文钩子)。
//   ⇒ 判据 ③ 的反射断言只扫前两者;本类另以 (a)(b)(c)(d) 四例状态迁移夹具覆盖。
//
// ⚠️ fail-closed(用户 2026-10-24 裁定,承 PRE-S19-A ⑧b):
//   未注入 `narrativeContext` ⇒ 一切查询与写入**拒绝并抛具名异常**。
//   **禁 fail-open** —— 恒真 = save-scum 通道在生产路径默开,而测试全绿。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>
    /// 槽位台账 —— `AC-7a-19` 四子判据的状态宿主。
    /// <para><b>会话内存</b>:不序列化、不进存档体(ADR-010 §六「锁字段不进存档体」)⇒
    /// 跨会话复位。反 scum 靠「载入安全上下文门」而非格式锁。</para>
    /// </summary>
    public sealed class SaveSlotLedger
    {
        private readonly Func<bool> _narrativeContext;
        private readonly HashSet<uint> _locked = new HashSet<uint>();
        private uint _nextWritableSeq;

        /// <summary>
        /// 构造。
        /// </summary>
        /// <param name="narrativeContext">
        /// 叙事上下文观察钩子(`InNarrativeContext(t)`)。
        /// <para><b>可空但 fail-closed</b>:未注入 ⇒ <see cref="Snapshot"/> /
        /// <see cref="EnsureWritable"/> / <see cref="OnLoaded"/> 一律抛
        /// <see cref="InvalidOperationException"/>。这与「注入一个恒 false 的委托」**不等价**
        /// —— 后者是可用的拒绝,前者是**配置错误**,必须能被测试区分。</para>
        /// <para>真实驱动源归 8 / 37 / 52 的实现轮(`TD-persist-04`),本类只定义门。</para>
        /// </param>
        /// <param name="initialNextWritableSeq">
        /// 高水位初值。缺省 0 ⇒ 任何槽位都还没写过。
        /// <para>⚠️ 从存档恢复时**不得**用存档体里的值 —— 锁与高水位都是会话内存
        /// (ADR-010 §六)。初值由装配层按「本会话从哪个槽位起步」决定。</para>
        /// </param>
        public SaveSlotLedger(Func<bool> narrativeContext, uint initialNextWritableSeq = 0)
        {
            _narrativeContext = narrativeContext;
            _nextWritableSeq = initialNextWritableSeq;
        }

        /// <summary>叙事上下文钩子是否已注入。装配层与测试用,不参与判定。</summary>
        public bool HasNarrativeContextSource => _narrativeContext != null;

        /// <summary>下一可写槽位序号(只进不退)。</summary>
        public uint NextWritableSeq => _nextWritableSeq;

        /// <summary>
        /// 构造指定槽位的投影上下文。
        /// </summary>
        /// <exception cref="InvalidOperationException">未注入叙事上下文钩子(fail-closed)。</exception>
        public SlotContext Snapshot(SaveSlot slot)
        {
            if (_narrativeContext == null) throw NoContextSource();
            return new SlotContext(_locked.Contains(slot.SlotSeq), _narrativeContext(), _nextWritableSeq);
        }

        /// <summary>该槽位是否已锁(只读查询;不触发 fail-closed —— 锁态与上下文无关)。</summary>
        public bool IsLocked(SaveSlot slot) => _locked.Contains(slot.SlotSeq);

        /// <summary>
        /// `AC-7a-19 (b)(c)` —— 写前校验,不通过即抛。
        /// <para>(b) 覆盖既有槽位(`slot_seq &lt; next_writable_seq`)⇒ 抛;</para>
        /// <para>(c) 不在叙事上下文内 ⇒ 抛。</para>
        /// <para>锁态**不在此拒** —— 锁拒的是 reads 侧(`Loadable`),写入新槽位不受旧锁影响
        /// (GDD `:213`「该门不改变锁的 per-slot 会话语义」)。</para>
        /// </summary>
        /// <exception cref="InvalidOperationException">未注入钩子(fail-closed)。</exception>
        /// <exception cref="InvalidDataException">覆盖既有槽位(只进不退违例)。</exception>
        /// <exception cref="InvalidOperationException">不在叙事上下文内。</exception>
        public void EnsureWritable(SaveSlot slot)
        {
            if (_narrativeContext == null) throw NoContextSource();

            if (slot.SlotSeq < _nextWritableSeq)
                throw new InvalidDataException(
                    $"槽位只进不退:slot_seq={slot.SlotSeq} < next_writable_seq={_nextWritableSeq}" +
                    "(AC-7a-19 (b) —— 覆盖既有槽位由 7a 拒绝,不是 7b 提示)");

            if (!_narrativeContext())
                throw new InvalidOperationException(
                    $"不在叙事上下文内,拒绝写入 slot={slot.SlotSeq}" +
                    "(AC-7a-19 (c) / 规则十四「手动存须在无未决医疗动作的上下文」)");
        }

        /// <summary>
        /// 写盘成功后推进高水位(只进不退)。
        /// <para>调用点须在 `SaveBytes` **返回之后** —— 写失败则高水位不动,
        /// 否则一次失败的写会永久吃掉一个槽位。</para>
        /// </summary>
        public void OnCheckpointCommitted(SaveSlot slot)
        {
            uint next = slot.SlotSeq + 1;
            if (next > _nextWritableSeq) _nextWritableSeq = next;
        }

        /// <summary>
        /// `AC-7a-19 (d)` —— 读档成功后置锁(reads 侧)。
        /// <para>锁持续到下一次叙事事件 checkpoint,由 <see cref="OnNarrativeAnchor"/> 解除。</para>
        /// </summary>
        public void OnLoaded(SaveSlot slot)
        {
            _locked.Add(slot.SlotSeq);
        }

        /// <summary>
        /// 叙事事件锚点到达 ⇒ 解除**全部**槽位的锁。
        /// <para>依据 GDD 规则十四「锁持续到下一次叙事事件触发 checkpoint」——
        /// 锁的作用域是「两次叙事事件之间」,锚点一到即整体失效。</para>
        /// <para>⚠️ 高水位**不在此复位**(只进不退,与锁正交)。</para>
        /// </summary>
        public void OnNarrativeAnchor()
        {
            _locked.Clear();
        }

        private static InvalidOperationException NoContextSource()
            => new InvalidOperationException(
                "SaveSlotLedger 未注入叙事上下文钩子(fail-closed)—— " +
                "恒真会让 save-scum 通道在生产路径默开而测试全绿;" +
                "真实驱动源归 8 / 37 / 52 的实现轮(TD-persist-04)。");
    }
}
