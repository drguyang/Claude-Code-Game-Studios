// ADR-010 §六 规则十四 —— 槽位台账(有状态)。
//
// 权威来源:
//   design/gdd/persistence-service.md 规则十四:
//     · 槽位**只进不退** —— 🔁 **2026-11-04 由 ADR-041 §D2 收窄**:它不再是写入门,
//       而是「『留档』追加到下一号」的默认目标语义;覆盖既有槽位**合法**。
//     · 读档即锁:从某槽位载入后,该槽位在同一会话内锁定 reads 侧,锁持续到下一次叙事事件 checkpoint
//       (**覆写不清锁** —— ADR-041 §D3)
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
        /// 手动存档的第一个槽号。**槽 3 起**;覆盖自 2026-11-04 起合法(ADR-041 §D2),
        /// 「只进不退」只剩「『留档』默认追加到下一号」这一层语义。
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
        /// 该槽位的 <c>.bak</c> 是否**算作这个槽的一条存档** —— 探测(占用态)、读档回退、
        /// 「最近一份」三处**同一条判据**,不再分叉。
        /// <para>🔁 **2026-11-05 统一(ADR-041 §D4 修订 · 用户裁定「按行业惯例纠正」)**:
        /// 2026-11-04 曾把它拆成两条(`ProbeCountsBak` / `ReadFallsBackToBak`),理由是
        /// 「有 bak 不代表这个槽现在有档」。那条理由对**旧残留**成立,对**只剩备份**不成立 ——
        /// 而备份的语义就是「主档没了用它」。拆开后的实际后果是:崩溃落在 `WriteAtomic` 的
        /// `final→bak` 与 `tmp→final` 之间时,final 没了、`bak` 是一份**完整的上一版**,
        /// 而册子说「这里没写过」、读档也说空槽 ⇒ **那份世界不可达**(而 ADR-041 之前
        /// 手动槽根本产不出 bak,所以这个形态是覆写功能自己带出来的)。</para>
        /// <para>⚠️ **合并不会重造 `TD-persist-19`**(「屏上说有档 ⇒ 按下去说空槽」):那条病
        /// 的成因是「探测认 bak 而**读档不认**」。今日读档对全部槽型都认 bak ⇒ 三处同向,
        /// 无论 bak 算不算占用,屏与载入都不会分叉。**分叉的成因是两处不一致,不是取值本身。**</para>
        /// </summary>
        public static bool BakCountsAsSave(uint seq) => true;

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
        /// 写前校验,不通过即抛。
        /// <para>门⓪:槽号 0 非法。</para>
        /// <para>门(c):不在叙事上下文内 ⇒ 抛(`AC-7a-19 (c)` 原样保留)。</para>
        /// <para>🔁 **2026-11-04(ADR-041 §D2,用户裁定)**:原门①「覆盖既有槽位 ⇒ 抛」
        /// **已删除** —— 覆盖是合法动作。其理由(「每页 = 一次免费重试」的定价)在实现里
        /// 从不成立:手动槽总数无上限(`save-slots-7b.md:108`)且从不自动裁剪(`OQ-7a-8`),
        /// 重试本就不花代价。真正守反 scum 的两条**不依赖它**:读档即锁(见 <see cref="OnLoaded"/>)
        /// 与叙事上下文门。</para>
        /// <para>⇒ <c>_nextWritableSeq</c> 的语义收窄为「『留档』的默认目标」,
        /// **不再是写入门**。</para>
        /// <para>锁态**不在此拒** —— 锁拒的是 reads 侧(`Loadable`);覆写一个已锁槽是允许的,
        /// 且**覆写不清锁**(见 <see cref="OnCheckpointCommitted"/> 的注)。</para>
        /// </summary>
        /// <exception cref="InvalidOperationException">未注入钩子(fail-closed)。</exception>
        /// <exception cref="InvalidDataException">槽号 0(不是任何槽)。</exception>
        /// <exception cref="InvalidOperationException">不在叙事上下文内。</exception>
        public void EnsureWritable(SaveSlot slot)
        {
            if (_narrativeContext == null) throw NoContextSource();

            // 门 ⓪:槽号合法性 —— 0 不是任何槽(1 = 退出 / 2 = 自动 / 3+ = 手动)。
            if (slot.SlotSeq == 0)
                throw new InvalidDataException(
                    "槽号 0 不是合法槽位:1 = 退出保存 · 2 = 自动存档 · 3+ = 手动存档" +
                    "(TD-persist-17 · 用户裁定 2026-11-03)");

            // 门 ①(「覆盖既有槽位 ⇒ 抛」)已于 2026-11-04 删除 —— ADR-041 §D2。

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
            //    而 3 正是**第一个**手动槽 ⇒ 「留档」的默认目标会被推错一页。
            if (slot.SlotSeq < MinManualSlotSeq) return;

            uint next = slot.SlotSeq + 1;
            if (next > _nextWritableSeq) _nextWritableSeq = next;

            // ⚠️ **本方法不得触碰 `_locked`(ADR-041 §D3 的承载不变式)**:
            //    覆写一个已锁槽**不解锁** —— 否则「读 A → 覆写 A → 再读 A」在同一叙事
            //    窗口内可循环,反 scum 归零。锁只在 `OnNarrativeAnchor` 整批解除。
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
