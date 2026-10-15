// 权威来源:GDD 规则六(结案唯一出口 · 已处置前置 · 单例 · 不可撤销 · 幂等拒收)
//          · ADR-008 §四(处置证据窗口化 + 快照;方向订正 2026-09-17)
//          · ADR-009 §二(模拟态判据 —— 入流义务与触发者无关)
//
// 结案决策纯函数:给定病人 p 的当前开案状态、病史流处置证据、玩家勾选,返回决策结果。
// 不触 IEventSink,不触 IIdAuthority,只返回「该做什么」。
// 生产代码 —— 测试测的是本文件,不是测试自己的副本。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>结案决策结果。</summary>
    public enum CaseCloseDecision
    {
        /// <summary>可结案 —— 前置满足,写入 CaseClosed。</summary>
        CanClose = 0,
        /// <summary>拒绝 —— 未勾选「已处置」。</summary>
        RejectNotChecked = 1,
        /// <summary>拒绝 —— 窗口内无处置事件。</summary>
        RejectNoTreatment = 2,
        /// <summary>拒绝 —— 案件已结(幂等拒收)。</summary>
        RejectAlreadyClosed = 3,
        /// <summary>拒绝 —— 案件未开。</summary>
        RejectNotOpen = 4,
    }

    /// <summary>
    /// 结案决策纯函数(GDD 规则六)。
    /// </summary>
    public static class CaseCloseDecider
    {
        /// <summary>
        /// 结案决策:给定案件状态、病史流处置证据、玩家勾选,返回决策结果。
        /// </summary>
        /// <param name="isOpen">案件是否开着(**流派生**:开过 ∧ ¬已结)。</param>
        /// <param name="isClosed">案件是否已结(**流派生**)。</param>
        /// <param name="hasTreatmentInWindow">窗口内是否有处置事件。</param>
        /// <param name="isChecked">玩家是否勾选「已处置」。</param>
        /// <returns>决策结果。</returns>
        public static CaseCloseDecision Decide(bool isOpen, bool isClosed, bool hasTreatmentInWindow, bool isChecked)
        {
            // 🔴 2026-10-15 修复(sprint-11 S11-8 · 补评件 story-003 · AC-37-03 面):两判序**已对调**。
            //
            // 契约:两 bool 由**同一流**派生(见 CaseCloseQuery)⇒ isOpen = 开过 ∧ ¬isClosed。
            //   (false, true)  = 开过且已结 —— 🔴 修前此组合是**正常生产路径**(结案后玩家再点),
            //                    而旧序 `!isOpen` 在前 ⇒ 已结案被报成「未开」(错因码)。
            //   (false, false) = 从未开过。
            //   (true,  true)  = **不可能**(isOpen 已蕴含 ¬isClosed)⇒ 无需处理、不再可达。
            // 「已结」比「未开」更具体(已结蕴含曾开)⇒ 具体者先判。
            //
            // ⚠️ 结果面零变化:两分支都是**拒绝且不产生事件**(GDD 规则六-4 的唯一要求);
            //    改动只在**理由码**。既有全部调用点行为不变(唯一受影响的组合恰是上表 (false,true))。

            // 案件已结 → 幂等拒收(AC-37-03)
            if (isClosed)
                return CaseCloseDecision.RejectAlreadyClosed;

            // 案件未开 → 拒绝
            if (!isOpen)
                return CaseCloseDecision.RejectNotOpen;

            // 未勾选 → 拒绝
            if (!isChecked)
                return CaseCloseDecision.RejectNotChecked;

            // 窗口内无处置事件 → 拒绝
            if (!hasTreatmentInWindow)
                return CaseCloseDecision.RejectNoTreatment;

            // 前置满足 → 可结案
            return CaseCloseDecision.CanClose;
        }
    }

    /// <summary>
    /// 处置集白名单(`treatment event` —— **权威 = ADR-008 §四 `:216-249`**,🟢 2026-10-15 补立)。
    ///
    /// <para><b>定义</b>:
    /// <c>treatment event := e.Kind ∈ { EmergencyTreatmentApplied, DrugTreatmentApplied, CareApplied }</c></para>
    ///
    /// <para><b>闭环纪律</b>(ADR-008 §四):增删成员须**同时**改 <c>adr-008</c> §四 +
    /// <c>case-system.md</c> 规则六 / 规则八 / F-37.2 —— **四处口径一致,禁止任一处单独扩集**。
    /// 本类即该枚举的**唯一实现落点**。</para>
    ///
    /// <para>⚠️ 此处**刻意不引行号**:2026-10-15 核验发现四处引用的行号已全部漂移
    /// (旧号 `:666`/`:679`/`:254`/`:280` → 实测 `:671`+`:674`+`:686` / `:255` / `:283`),
    /// 且 <c>case-system.md</c> 自身的闭环纪律行也自引了同一组旧号。**节名稳定,行号不稳** ——
    /// 复查请按节名检索。</para>
    ///
    /// <para>🔴 <b>2026-10-15 修复(sprint-11 S11-10 · 补评件 story-003 §二 B5)</b>:
    /// 本类原为 <c>{ EmergencyTreatmentApplied, DrugTreatmentApplied, InjuryOnset }</c> ——
    /// **错收** 25 的战斗伤害结算(⇒「病人挨了打」被计入「已被处置」,玩家把病人打到即可满足
    /// 结案前置,**正面击穿** <c>case-system.md</c> 的 F-37.1 边界表与 EC-37-3 两条
    /// 「玩家空手连关三例 ⇒ **不可能**」),
    /// 同时**漏收** <c>CareApplied</c>(8 的护理)—— 取舍方向与语义**完全相反**。</para>
    /// </summary>
    public static class TreatmentKinds
    {
        /// <summary>处置集**真源**(<c>private</c> —— 见 <see cref="MembersSnapshot"/> 的 A-1 说明)。</summary>
        private static readonly HashSet<EventKind> Members = new HashSet<EventKind>
        {
            EventKind.EmergencyTreatmentApplied,  // 10 急救**已施加**(ADR-009 Amendment I)
            EventKind.DrugTreatmentApplied,       // 11 用药**已施加**
            EventKind.CareApplied,                // 8  护理动作(病史流)
        };

        /// <summary>判断某 Kind 是否属于处置集。</summary>
        public static bool Contains(EventKind kind) => Members.Contains(kind);

        /// <summary>处置集成员的**只读快照**(每次调用返回**独立副本**)。
        ///
        /// <para>🔴 <b>2026-10-15 修复(sprint-11 S11-10 顺批 · 补评件 ADVISORY A-1)</b>:
        /// 原实现暴露 <c>public static readonly HashSet&lt;EventKind&gt; Set</c> ——
        /// <c>readonly</c> 只锁**引用**,集合**内容全仓可写**(任一处
        /// <c>TreatmentKinds.Set.Add(...)</c> 即**静默扩集**),破坏 ADR-005 的确定性纪律。
        /// 现改为 <c>private</c> 真源 + 本出口返**拷贝** ⇒ 调用方**改不动**真源。</para>
        ///
        /// <para>⚠️ 顺序**非契约**(<c>HashSet</c> 迭代序无保证);需要集合语义的用法一律走 set 比较。
        /// 本出口的服务对象 = 「**双向穷尽**」断言 —— 单向 <c>Contains</c> **抓不到**「偷偷多收一员」,
        /// 而 B5 正是这样逃过测试面的。</para>
        /// </summary>
        /// <returns>处置集成员的独立副本(改动它不影响真源)。</returns>
        public static EventKind[] MembersSnapshot()
        {
            var copy = new EventKind[Members.Count];
            Members.CopyTo(copy);
            return copy;
        }
    }

    /// <summary>
    /// 流前缀查询:结案前置谓词(GDD F-37.2)。
    /// </summary>
    public static class CaseCloseQuery
    {
        /// <summary>
        /// 已处置(c) := ∃ e ∈ 病史流: e.Patient = c.patient_id
        ///   ∧ e.Kind ∈ 处置集
        ///   ∧ e.Tick ∈ [CaseOpened.Tick, CloseTick]
        /// 窗口方向:处置 tick 落在立案与结案之间(2026-09-17 订正)。
        /// </summary>
        /// <param name="historyEvents">病史流事件列表。</param>
        /// <param name="patientId">病人 id。</param>
        /// <param name="openedTick">立案 tick(CaseOpened.Tick)。</param>
        /// <param name="closeTick">结案 tick(CaseClosed.Tick)。</param>
        /// <returns>窗口内是否有处置事件。</returns>
        public static bool HasTreatmentInWindow(
            IReadOnlyList<SimEvent> historyEvents, int patientId, long openedTick, long closeTick)
        {
            foreach (var e in historyEvents)
            {
                if (e.Patient.Value != patientId)
                    continue;
                if (!TreatmentKinds.Contains(e.Kind))
                    continue;
                // ⚠️ 窗口判据**复用** IsInWindow,不内联重写(2026-10-15 ADVISORY 清理轮 · 003 §6.3)。
                // 原稿此处内联 `e.Tick < openedTick || e.Tick > closeTick` = 同一闭区间的**第二份**实现
                // ⇒ 两处可各自漂移;且 IsInWindow 当时**生产零调用点**(只有测试在打)。
                // 现 IsInWindow 是窗口语义的**唯一出处**,本方法只是它的流扫描壳。
                if (!IsInWindow(e.Tick, openedTick, closeTick))
                    continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 案件是否已结(流前缀纯函数)。
        ///
        /// <para><b>关联口径 = 载荷解码</b>(2026-10-15 接线轮 · 承 ADR-031 V-8 ①「退」):
        /// <c>CaseClosedPayload.CaseId</c> 是**权威关联字段** —— 本条 <c>CaseClosed</c> 属于哪个
        /// 案件,由它**自己的载荷**说出,不由流序推断。逐条扫描:任一 <c>CaseClosed</c> 的载荷
        /// <c>CaseId</c> 精确等于入参 ⇒ 已结。</para>
        ///
        /// <para><b>退掉了什么</b>:原实现取「其前最近一条该病人的 <c>CaseOpened</c>」的**流序关联**,
        /// 与载荷解码**只在合法流上等价**,并自陈「等价性前提由本处举证」;解码把该前提**消掉**
        /// (违反「同一病人同时至多一个开案」的非法流上,流序关联会误判,解码不会)。</para>
        ///
        /// <para>🔴 <b>2026-10-15 修复(sprint-11 S11-8 · 补评件 story-003 §二 B-1)</b>:
        /// 原实现 `if (e.Patient.Value == caseId.Patient) return true;` 把「该病人**曾有过**任何
        /// <c>CaseClosed</c>」当作「**本** case_id 已结」⇒ 复诊新案被判已结,
        /// <c>Decide</c> 走 `RejectAlreadyClosed` ⇒ **复诊案永远无法结案**
        /// (GDD `:250-256` 幂等定义 = **同一** case_id;EC-37-1 复诊 = 新病例)。</para>
        /// </summary>
        /// <param name="caseEvents">病例流事件列表(已按全序 <c>(Tick, Patient, Seq)</c> 排列)。</param>
        /// <param name="caseId">案件 case_id。</param>
        /// <param name="decoder">载荷解码器(ADR-031 §① 第八抽象点;读 <c>CaseClosedPayload.CaseId</c>)。</param>
        /// <returns>是否已结。</returns>
        public static bool IsClosed(IReadOnlyList<SimEvent> caseEvents, CaseId caseId,
                                    IPayloadDecoder decoder)
        {
            if (decoder == null) throw new ArgumentNullException(nameof(decoder));

            foreach (var e in caseEvents)
            {
                if (e.Kind != EventKind.CaseClosed)
                    continue;

                var payload = decoder.Decode<CaseClosedPayload>(EventKind.CaseClosed, e.Payload);
                if (payload.CaseId.Equals(caseId))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 案件是否开着 := 该案**开过** ∧ **未结**(GDD 规则六-4:结案不可撤销、不重开)。
        ///
        /// <para>与 <see cref="IsClosed"/> **共用同一契约与同一前置**(入参 = 病例流全序前缀)。
        /// 🔴 2026-10-15 修复(S11-8 · 补评件 ADVISORY A-6):两函数此前对**同一形参**
        /// <c>caseEvents</c> 隐含**不同**输入契约 —— 一个的注释要求「该案子集」、另一个按整表三元组查找,
        /// 是 B-1 的土壤;现统一为「整条全序流」,并由同一扫描派生,结构上不可再分叉。</para>
        /// </summary>
        /// <param name="caseEvents">病例流事件列表(已按全序 <c>(Tick, Patient, Seq)</c> 排列)。</param>
        /// <param name="caseId">案件 case_id。</param>
        /// <param name="decoder">载荷解码器(转交 <see cref="IsClosed"/>)。</param>
        /// <returns>是否开着。</returns>
        public static bool IsOpen(IReadOnlyList<SimEvent> caseEvents, CaseId caseId,
                                  IPayloadDecoder decoder)
        {
            return OpenedInStream(caseEvents, caseId) && !IsClosed(caseEvents, caseId, decoder);
        }

        /// <summary>该案是否在流中**开过**(精确三元组匹配 <c>CaseOpened</c>)。
        /// 守卫「从未开过的 caseId」不得被 <see cref="IsOpen"/> 判为真。</summary>
        private static bool OpenedInStream(IReadOnlyList<SimEvent> caseEvents, CaseId caseId)
        {
            foreach (var e in caseEvents)
            {
                if (e.Kind != EventKind.CaseOpened)
                    continue;
                if (e.Tick == caseId.Tick && e.Patient.Value == caseId.Patient && e.Seq == caseId.Seq)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 窗口边界:e.Tick == openedTick(立案即处置)与 == closeTick 均计入(闭区间)。
        /// </summary>
        /// <param name="tick">处置事件 tick。</param>
        /// <param name="openedTick">立案 tick。</param>
        /// <param name="closeTick">结案 tick。</param>
        /// <returns>是否在窗口内(闭区间)。</returns>
        public static bool IsInWindow(long tick, long openedTick, long closeTick)
        {
            return tick >= openedTick && tick <= closeTick;
        }
    }
}
