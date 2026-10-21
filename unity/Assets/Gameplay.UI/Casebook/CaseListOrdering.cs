// 权威来源:design/gdd/casebook.md —— 规则三(病例列表)· F-39.1(SortKey / OrderedList)
//          · F-39.2(Page / 串接组)· AC-39-02(全序性)· AC-39-03(流前缀 + 零展示序字段)
//          · ADR-008 §一(病例流 = 第二条逻辑流;case_id = CaseOpened 的 (Tick, Patient, Seq) 三元组)
//          · ADR-010 §三 义务表(排序派生**不入存档** —— 本文件零写盘)
//          · ADR-009 §一 Q1 判据(读时派生 = 派生态,不是第二真源)
//          · production/epics/casebook/story-002-case-list-sort-pure-function.md
//
// 三条纪律(改本文件前先读):
//   ① **比较器禁 `a - b`**(int64 溢出会静默反转大小关系)—— 一律逐分量 `CompareTo` / 比较链。
//      tick 是 long,`MaxValue - MinValue` 回绕后符号错 ⇒ 排序结果在极值处**无征兆地**错。
//   ② **零「display order / index」字段** —— 顺序是**读时投影**(AC-39-03 反射判据);
//      一旦落成类型字段,就出现第二真源(重放/补算时旧序与新序可分叉)。
//   ③ **本函数不持状态**:入参 → 新数组,**零静态可变字段、零缓存**;每次调用重算
//      (caching = 第二份真相,同 42 的 `IDtoSource` 铁律)。
//
// 边界(本文件刻意不做):
//   · 不读 `IEventSink` / 不做流加载 —— 入参就是「已加载病例流的只读事件列表」
//     (story Implementation Notes ①)。`CasesOf` 具名接口 = TR-casebook-008 gap,
//     待 37 修订轮回写;**本文件不声称已接上加载路径**。
//   · 不做按病种 / 日期 / 状态的重排或检索(规则四 ② · AC-39-05 —— 那是 Story 004 的禁用守卫面)。
//   · 不做「未结 N 计数」(反幻想一 / AC-39-08;归 casebook-004 的禁项)。
//   · 不做每页病例数 / 分册容量(呈现数值归用户数值轮,story Implementation Notes ⑤)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.UI.Casebook
{
    /// <summary>
    /// 病例列表的排序键(F-39.1):<c>(patient_id, Tick(CaseOpened), Seq(CaseOpened))</c>。
    /// <para><b>全序</b>:三个分量都是整数域,逐分量字典序比较 ⇒ 任意两个 <b>不同</b> 病例的键必不等
    /// (case_id 三元组本身在病例流内唯一,ADR-008 §一;而这里的三元组就是 case_id 本身 ⇒
    /// 「键相等」⇔「同一病例」)。因此不存在并列残留 —— 不同平台 `Array.Sort` 的稳定化差异
    /// 不会显形(AC-39-02 Guardrail)。</para>
    /// <para><b>零落盘</b>:本键**读时现算**,不序列化、不进存档(TR-casebook-005 · ADR-010 §三)。</para>
    /// </summary>
    public readonly struct CaseSortKey : IComparable<CaseSortKey>, IEquatable<CaseSortKey>
    {
        /// <summary>首分量 = 串接键(GDD F-39.1「串接键 = 首分量」;同 `patient_id` 的病例成「一叠」)。</summary>
        public readonly int PatientId;

        /// <summary><c>CaseOpened</c> 事件自身的 `SimEvent.Tick`(载荷不重复携带开案 tick —— ADR-008 订正)。</summary>
        public readonly long OpenedTick;

        /// <summary><c>CaseOpened</c> 的主机发号 `SimEvent.Seq`(同 (Tick, Patient) 内唯一)。</summary>
        public readonly long OpenedSeq;

        /// <summary>构造排序键。</summary>
        /// <param name="patientId">病人 id(`SimEvent.Patient.Value`;负值 = 哨兵,仍可比)。</param>
        /// <param name="openedTick">开案 tick。</param>
        /// <param name="openedSeq">开案事件的主机流水号。</param>
        public CaseSortKey(int patientId, long openedTick, long openedSeq)
        {
            PatientId = patientId; OpenedTick = openedTick; OpenedSeq = openedSeq;
        }

        /// <summary>
        /// 逐分量字典序:patient_id → OpenedTick → OpenedSeq(升序)。
        /// <para>⚠️ <b>禁改写为减法</b>:<c>(int)(OpenedTick - other.OpenedTick)</c> 在 INT64 极值处回绕,
        /// 符号会反转(GDD 未点名,但 story 回填判据 #1 直接钉这一条)。</para>
        /// </summary>
        public int CompareTo(CaseSortKey other)
        {
            int c = PatientId.CompareTo(other.PatientId);
            if (c != 0) return c;
            c = OpenedTick.CompareTo(other.OpenedTick);
            if (c != 0) return c;
            return OpenedSeq.CompareTo(other.OpenedSeq);
        }

        /// <summary>键相等 ⇔ 同一病例(三元组唯一性,见类型注释)。</summary>
        public bool Equals(CaseSortKey other) => CompareTo(other) == 0;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is CaseSortKey k && Equals(k);

        /// <summary>仅用于字典键 / 测试断言;不落盘(ADR-007 §四 同类纪律)。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int h = PatientId.GetHashCode();
                h = (h * 397) ^ OpenedTick.GetHashCode();
                return (h * 397) ^ OpenedSeq.GetHashCode();
            }
        }

        /// <summary>键值相等。</summary>
        public static bool operator ==(CaseSortKey a, CaseSortKey b) => a.Equals(b);
        /// <summary>键值不等。</summary>
        public static bool operator !=(CaseSortKey a, CaseSortKey b) => !a.Equals(b);
        /// <summary>严格小于(a 排在 b 之前)。</summary>
        public static bool operator <(CaseSortKey a, CaseSortKey b) => a.CompareTo(b) < 0;
        /// <summary>严格大于。</summary>
        public static bool operator >(CaseSortKey a, CaseSortKey b) => a.CompareTo(b) > 0;
        /// <summary>小于等于。</summary>
        public static bool operator <=(CaseSortKey a, CaseSortKey b) => a.CompareTo(b) <= 0;
        /// <summary>大于等于。</summary>
        public static bool operator >=(CaseSortKey a, CaseSortKey b) => a.CompareTo(b) >= 0;

        /// <summary>便于失败信息定位(不参与比较)。</summary>
        public override string ToString() => $"({PatientId},{OpenedTick},{OpenedSeq})";
    }

    /// <summary>
    /// 列表项 —— 「一个病例」在 39 侧的只读投影(F-39.1 的 <c>c</c>)。
    /// <para><b>只带排序所需的三元组 + case_id</b>:病种 / 判断 / 状态都不在这里 ——
    /// 39 只出**顺序**,内容归 37 / 42(且病种面受 AC-37-15 闭集约束,不得随手带进来)。</para>
    /// <para>值类型、全整数、无引用字段 ⇒ 反射面天然无「展示序」字段(AC-39-03)。</para>
    /// </summary>
    public readonly struct CaseEntry
    {
        /// <summary>该病例所属病人(串接组键)。</summary>
        public readonly int PatientId;

        /// <summary>开案 tick(= <c>CaseOpened</c> 事件的 `Tick`)。</summary>
        public readonly long OpenedTick;

        /// <summary>开案事件的主机流水号(= <c>CaseOpened</c> 事件的 `Seq`)。</summary>
        public readonly long OpenedSeq;

        /// <summary>case_id 三元组(与开案事件**同一个**三元组,不另编号)。</summary>
        public readonly CaseId Case;

        /// <summary>构造一个列表项。</summary>
        /// <param name="patientId">病人 id。</param>
        /// <param name="openedTick">开案 tick。</param>
        /// <param name="openedSeq">开案流水号。</param>
        public CaseEntry(int patientId, long openedTick, long openedSeq)
        {
            PatientId = patientId; OpenedTick = openedTick; OpenedSeq = openedSeq;
            Case = new CaseId(openedTick, patientId, openedSeq);
        }

        /// <summary>本项的排序键(读时构造 —— 值类型,不分配)。</summary>
        public CaseSortKey SortKey => new CaseSortKey(PatientId, OpenedTick, OpenedSeq);
    }

    /// <summary>
    /// 「线订成叠」的**连续段标记**(32 位起点 + 长度,指向有序列表)。
    /// <para>GDD 规则三:「串接组(同 `patient_id`)= 线订成叠;37 定串接键,39 定叠的呈现」。
    /// 本类型只给**结构**(哪一段是一叠),**不给视觉** —— 装订形态归 42。</para>
    /// <para>⚠️ 本类型**不是**「第二个病例容器」:组内病例仍住
    /// <see cref="CaseSheet.Cases"/> 同一数组,组只持下标区间 ⇒ 组与列表不会分叉。</para>
    /// </summary>
    public readonly struct CaseGroup
    {
        /// <summary>本叠的串接键(同 `patient_id`)。</summary>
        public readonly int PatientId;

        /// <summary>本叠在有序列表中的首项下标(含)。</summary>
        public readonly int Start;

        /// <summary>本叠的病例数(≥ 1;1 = 该病人只此一例,仍成一叠 —— 段长 1 不特判)。</summary>
        public readonly int Count;

        /// <summary>构造一个组标记。</summary>
        /// <param name="patientId">该组病人 id。</param>
        /// <param name="start">有序列表内的首项下标。</param>
        /// <param name="count">病例数。</param>
        public CaseGroup(int patientId, int start, int count)
        {
            PatientId = patientId; Start = start; Count = count;
        }

        /// <summary>末项下标(含);空组不构造,故恒 ≥ <see cref="Start"/>。</summary>
        public int EndInclusive => Start + Count - 1;
    }

    /// <summary>
    /// 39 列表的一次求值结果 = **全序列 + 分组标记**(两者由同一次排序产出,不分两次算)。
    /// <para>42 的消费路径:按 <see cref="Groups"/> 逐叠装订,组内按
    /// <see cref="Cases"/> 的原序(page = 组内第 n 项)—— **不做二次排序**。</para>
    /// </summary>
    public readonly struct CaseSheet
    {
        /// <summary>全序病例列表(升序;组内顺序同样由三元组决定,不重排)。</summary>
        public readonly IReadOnlyList<CaseEntry> Cases;

        /// <summary>串接组(按段首三元组升序;每组的区间是 <see cref="Cases"/> 上的连续段)。</summary>
        public readonly IReadOnlyList<CaseGroup> Groups;

        /// <summary>构造结果。</summary>
        /// <param name="cases">有序病例列表。</param>
        /// <param name="groups">连续段标记。</param>
        public CaseSheet(IReadOnlyList<CaseEntry> cases, IReadOnlyList<CaseGroup> groups)
        {
            Cases = cases; Groups = groups;
        }

        /// <summary>列表长度(方便调用方,不另存字段 —— 派生自 <see cref="Cases"/>)。</summary>
        public int Count => Cases == null ? 0 : Cases.Count;
    }

    /// <summary>
    /// 病例列表排序(**纯函数 · 零落盘 · 零可变状态**)。F-39.1 的实现体。
    /// <para>唯一输入 = 已加载病例流的**只读事件列表**;唯一输出 = 新数组。
    /// 同入同出:两次调用逐位相同,与入参的**输入顺序无关**(输入乱序不改结果)。</para>
    /// <para><b>用法</b>:</para>
    /// <code>
    /// CaseSheet sheet = CaseListOrdering.OrderSheet(caseStreamEvents);
    /// foreach (CaseGroup g in sheet.Groups)          // 一叠一叠装订
    ///     for (int i = g.Start; i &lt;= g.EndInclusive; i++)
    ///         Render(sheet.Cases[i]);                 // 组内即全序,禁重排
    /// </code>
    /// </summary>
    public static class CaseListOrdering
    {
        /// <summary>取该事件的排序键(纯函数;非 `CaseOpened` 事件无键,调用方须先过滤)。</summary>
        /// <param name="openedCase">一条 <c>CaseOpened</c> 事件。</param>
        /// <returns>该病例的三元组键。</returns>
        public static CaseSortKey KeyOf(in SimEvent openedCase) =>
            new CaseSortKey(openedCase.Patient.Value, openedCase.Tick, openedCase.Seq);

        /// <summary>
        /// 全序病例列表(F-39.1 <c>OrderedList</c>)。
        /// <para>入参为空 / `null` ⇒ 返回**空数组**,不报错(AC 第 6 条);空流是合法终态,
        /// 不是「加载失败」—— 本函数**不**区分二者(区分归加载侧)。</para>
        /// <para>非 `CaseOpened` 的 Kind 一律跳过(病例流里还有 `CaseClosed` /
        /// `JudgmentRecorded` 等,它们不是列表项)。</para>
        /// </summary>
        /// <param name="events">病例流事件(顺序任意 —— 排不排序都不影响输出)。</param>
        /// <returns>按 <see cref="CaseSortKey"/> 严格升序排列的新数组。</returns>
        public static CaseEntry[] OrderedList(IReadOnlyList<SimEvent> events)
        {
            CaseEntry[] items = Project(events);
            Array.Sort(items, CompareEntries);
            return items;
        }

        /// <summary>
        /// 按「线订成叠」切连续段(AC 第 5 条)。输入**必须是** <see cref="OrderedList"/> 的输出。
        /// <para>同 `patient_id` 的项在全序里必然连续(首分量即 `patient_id`)⇒ 一次线性扫描即可切段;
        /// 因此「同病人病例被其他病人分隔」在**输出侧不可能出现** —— 该 Edge case 描述的是**输入侧**
        /// 的交错开案,已在排序里被吸收(qa-plan 回填 #3 已登记)。</para>
        /// </summary>
        /// <param name="orderedCases">有序病例列表(通常即 <see cref="OrderedList"/> 的返回)。</param>
        /// <returns>组标记(按出现序;空输入 ⇒ 空数组)。</returns>
        public static CaseGroup[] BuildGroups(IReadOnlyList<CaseEntry> orderedCases)
        {
            if (orderedCases == null || orderedCases.Count == 0) return Array.Empty<CaseGroup>();

            var groups = new List<CaseGroup>();
            int start = 0;
            for (int i = 1; i <= orderedCases.Count; i++)
            {
                bool boundary = i == orderedCases.Count ||
                                orderedCases[i].PatientId != orderedCases[start].PatientId;
                if (!boundary) continue;
                groups.Add(new CaseGroup(orderedCases[start].PatientId, start, i - start));
                start = i;
            }
            return groups.ToArray();
        }

        /// <summary>
        /// 一次求值:全序列 + 分组标记(42 的单次调用入口 —— 两者必来自同一次排序,
        /// 分两次算会造出「组与列表不同源」的窗口)。
        /// </summary>
        /// <param name="events">病例流事件(顺序任意)。</param>
        /// <returns>全序列表与其连续段标记;空流 ⇒ 两项均为空数组。</returns>
        public static CaseSheet OrderSheet(IReadOnlyList<SimEvent> events)
        {
            CaseEntry[] ordered = OrderedList(events);
            return new CaseSheet(ordered, BuildGroups(ordered));
        }

        /// <summary>两病例项的全序比较(负 = a 先)。委托给键的比较 —— 只有一份比较语义。</summary>
        /// <param name="a">左项。</param>
        /// <param name="b">右项。</param>
        /// <returns>负 / 零 / 正。</returns>
        public static int CompareEntries(CaseEntry a, CaseEntry b) => a.SortKey.CompareTo(b.SortKey);

        /// <summary>把事件流过滤 + 投影为列表项(一次遍历,不保留事件引用)。</summary>
        private static CaseEntry[] Project(IReadOnlyList<SimEvent> events)
        {
            if (events == null || events.Count == 0) return Array.Empty<CaseEntry>();

            var buffer = new List<CaseEntry>(events.Count);
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.CaseOpened) continue;
                buffer.Add(new CaseEntry(e.Patient.Value, e.Tick, e.Seq));
            }
            return buffer.ToArray();
        }
    }
}
