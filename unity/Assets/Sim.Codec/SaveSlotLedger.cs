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
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>
    /// 槽位台账 —— `AC-7a-19` 四子判据的状态宿主。
    /// <para><b>会话内存</b>:不序列化、不进存档体(ADR-010 §六「锁字段不进存档体」)⇒
    /// 跨会话复位。反 scum 靠「载入安全上下文门」而非格式锁。</para>
    /// </summary>
    public sealed class SaveSlotLedger
    {
        /// <summary>槽 1 = 退出保存(**保留槽**:固定身份,被反复重写)。</summary>
        public const uint ExitSlotSeq = 1;

        /// <summary>槽 2 = 自动存档(**保留槽**:定时兜底 + 事件锚点共用,原地覆盖)。</summary>
        public const uint AutoSlotSeq = 2;

        /// <summary>
        /// 手动存档的第一个槽号。**槽 3 起**,且仍然**只进不退**。
        /// <para>🔁 2026-11-03(`TD-persist-17` · 用户裁定「自动存档只占固定槽」):
        /// 此前自动存档与手动存档共用同一递增计数器 ⇒ 自动存档一路吃掉手动槽号。
        /// 现 1 / 2 是保留槽,3+ 才是玩家槽。</para>
        /// </summary>
        public const uint MinManualSlotSeq = 3;

        /// <summary>
        /// 该槽号是否**不是**玩家槽 —— 1 / 2 = 保留槽(可反复重写);
        /// 其余(&lt; 3,即 0)是**非法槽号**。
        /// </summary>
        public static bool IsReservedSlotSeq(uint seq) => seq == ExitSlotSeq || seq == AutoSlotSeq;

        /// <summary>
        /// 该槽位的 <c>.bak</c> 是否参与**读侧**回退(校验坏 / 缺失时改读 <c>.bak</c>)。
        /// <para><b>唯一定义处</b> —— 写侧(<see cref="SaveService"/>)与呈现侧
        /// (<see cref="SaveSlotProbe"/>)都必须调它,不得各写一份:两处若不同向,
        /// 会出现「屏上说有档、读档说没有」这种最难查的分叉。</para>
        /// <para>判据 = <see cref="IsReservedSlotSeq"/> —— 理由在**写侧**:
        /// <c>.bak</c> 只在「同一个文件被写第二次」时产生(`WriteAtomic` 的 final→bak 轮转)。
        /// 保留槽(退出保存 / 自动存档)反复重写 ⇒ 有 bak;手动槽只进不退 ⇒ **一生只写一次**
        /// ⇒ **永远没有 bak**。⇒ 对手动槽而言「回退到 bak」**无宾语**,
        /// 盘上若真有一份,那是旧版本残留,读它 = 静默装回更旧的档(违 `AC-7a-15`)。</para>
        /// <para>🔁 2026-11-04(`TD-persist-19` · 用户裁定「两步做掉」):本判据此前**只在写侧**成立
        /// (`TD-persist-16` 收窄了「玩家槽不产生 bak」),读侧仍无条件回退。
        /// 本条把口径补到读侧,并收成唯一出处。</para>
        /// </summary>
        public static bool FallsBackToBak(uint seq) => IsReservedSlotSeq(seq);

        private readonly INarrativeContextQuery _narrativeContext;
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
        /// <para>🔁 <b>2026-11-03 订正(`TD-persist-15`),原文不删</b>:</para>
        /// <para>① 原文写「锁与高水位都是会话内存(ADR-010 §六)」= <b>过度引据</b>。
        /// ADR-010 §六 逐字只裁「**锁字段不进存档体** —— 锁与安全门均会话内存」,
        /// <b>从未</b>裁过高水位。⇒ 「不进存档体」对本形参成立(它是构造期入参,
        /// 不序列化);但「高水位只能取本会话内存里发生过的值」**不成立**,见 ②。</para>
        /// <para>② <b>只进不退的语义是跨会话的</b>(GDD 规则十四),其唯一合法来源 =
        /// 存档目录里的**文件名**(`save_{N}.bin` 是否存在),**不是**存档体内容。
        /// 故 <see cref="SaveService"/> 构造时把本形参当<b>下限(floor)</b>,终值 =
        /// `max(floor, 磁盘最大槽号 + 1)`。把本形参当终值 ⇒ 每会话从 floor 起 ⇒
        /// 覆盖上一会话的槽位(<b>已实测发生</b>)。</para>
        /// <para>③ 原文「本形参今日在装配层不可达」/「归 `TD-persist-03`」均已过期:
        /// 通道已于 2026-11-03 打开,`TD-persist-03` 已于 2026-10-24 结案且其裁定面
        /// 不含本形参。现值见 <see cref="SaveService"/> 构造器注释。</para>
        /// </param>
        public SaveSlotLedger(INarrativeContextQuery narrativeContext, uint initialNextWritableSeq = 0)
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
            return new SlotContext(_locked.Contains(slot.SlotSeq), _narrativeContext.InNarrativeContext(), _nextWritableSeq);
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

            // 门 ⓪:槽号合法性 —— 0 不是任何槽(1 = 退出 / 2 = 自动 / 3+ = 手动)。
            //    ⚠️ 这一条**不是**「只进不退」,是「根本没有这个槽」。
            if (slot.SlotSeq == 0)
                throw new InvalidDataException(
                    "槽号 0 不是合法槽位:1 = 退出保存 · 2 = 自动存档 · 3+ = 手动存档" +
                    "(TD-persist-17 · 用户裁定 2026-11-03)");

            // 门 ①:只进不退 —— **只约束玩家槽**(≥ 3)。
            //    保留槽 1 / 2 是**固定身份、原地覆盖**:自动存档每来一次就顶掉上一份,
            //    这正是它该有的语义(它不是玩家的回滚点)。
            if (slot.SlotSeq >= MinManualSlotSeq && slot.SlotSeq < _nextWritableSeq)
                throw new InvalidDataException(
                    $"槽位只进不退:slot_seq={slot.SlotSeq} < next_writable_seq={_nextWritableSeq}" +
                    "(AC-7a-19 (b) —— 覆盖既有槽位由 7a 拒绝,不是 7b 提示)");

            if (!_narrativeContext.InNarrativeContext())
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
            // 保留槽(1 / 2)不推进高水位 —— 它们不进递增段。
            // ⚠️ 若不挡:写一次自动存档(槽 2)会把 `_nextWritableSeq` 推到 3,
            //    而 3 正是**第一个**手动槽 ⇒ 玩家的第一次手动存档会被莫名拒掉。
            if (slot.SlotSeq < MinManualSlotSeq) return;

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
