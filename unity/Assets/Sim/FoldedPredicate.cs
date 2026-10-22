// GDD persistence-service.md F-7a-4 —— 折叠谓词实现(7a 的义务 · 门 G-2 / TD-persist-01)。
//
// 权威来源:
//   GDD persistence-service.md:314-330 —— Folded(p) = 终态(p) ∧ ¬豁免(p) ∧ ¬未结案(p)
//   GDD persistence-service.md:321     —— ¬豁免 = 豁免类(lethal = false ∧ plateau,R3.6)
//                                           **或**携带 relapse.* 块
//   ADR-008 §六                        —— 条件 ③(未结案可从病例流重构)
//   ADR-026 §⑦                         —— 条件 ④ SkillGrown 行豁免(折叠动作侧,不在本布尔里)
//   ADR-032 ⑥                          —— 条件 ① 消费口径 = 存在性查询,不重放 F4
//   ADR-025 §①:112                     —— 甲案:抽象点住 Contracts、本实现住 Sim
//
// 三条件的落点(逐条):
//   ① 终态(p)   = 病史流存在该病人的 PatientOutcomeChanged(ADR-032 ⑥ 逐字)
//   ② ¬豁免(p)  = 病人的**任一**病种不属于豁免类、且不带 relapse.* 块
//                 `D_p` ← 该病人的 DiseaseOnset 事件(ADR-030:病人「带着某病出现」的唯一载体)
//   ③ ¬未结案(p)= `CaseStreamQuery.HasOpenCase` 的**否定**(复用,不另抄一份 —— 第二真源禁令)
//
// 条件 ④(SkillGrown 行不被抹除)不在本类 —— 它是**折叠动作**的豁免,
//   落 `HistoryFolder`(ADR-026 §⑦ 判据 = 「折叠之后 Level(s)@t 逐位一致」,不是本布尔返回值)。
//
// ⚠️ `lethal(d)` 的取法承既有先例 `Lethality > 0`(`RegistrySchema.cs:398-401` 自陈
//   「GDD R1 表写 bool lethal,既有字段是 int Lethality」;`R1_CHECK_22` 即如此写;
//   `EntityDeathGates.LethalityOf` 同款)。本类**不另立**第二套 lethal 语义。
//   `PRE-S18-D` 仍挂「待用户确认」—— 若改判为 bool 字段,改动落点 = 本文件一处 + 实体门一处。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 折叠谓词实现 —— 四条件里 ①②③ 的合并点。
    ///
    /// <para><b>全部输入 ∈ {事件流 + 烘焙数据}</b>(ADR-016 §一 三源不变量):
    /// 病史流、病例流、病种注册表。**无快照、无墙钟、无表现态**。</para>
    ///
    /// <para><b>无状态缓存</b>:每次求值现读。缓存流前缀会引入一份可与真源失步的副本,
    /// 与「单一真源」相抵 —— 本谓词不在热路径上。</para>
    /// </summary>
    public sealed class FoldedPredicate : IFoldedPredicate
    {
        private readonly Func<IReadOnlyList<SimEvent>> _historyStream;
        private readonly Func<IReadOnlyList<SimEvent>> _caseStream;
        private readonly Func<int, DiseaseRegistryEntry> _diseaseLookup;
        private readonly IPayloadDecoder _decoder;

        /// <summary>
        /// 构造。
        /// </summary>
        /// <param name="historyStream">病史流只读视图(每次求值现取 —— 流会增长,不缓存)。</param>
        /// <param name="caseStream">病例流只读视图(同上)。</param>
        /// <param name="diseaseLookup">病种 id → 注册表条目;查无返回 <c>null</c>(本类对 null fail-loud)。</param>
        /// <param name="decoder">载荷解码器(ADR-031 第八抽象点 —— `Sim` 读载荷的唯一合法路径)。</param>
        public FoldedPredicate(
            Func<IReadOnlyList<SimEvent>> historyStream,
            Func<IReadOnlyList<SimEvent>> caseStream,
            Func<int, DiseaseRegistryEntry> diseaseLookup,
            IPayloadDecoder decoder)
        {
            _historyStream = historyStream ?? throw new ArgumentNullException(nameof(historyStream));
            _caseStream = caseStream ?? throw new ArgumentNullException(nameof(caseStream));
            _diseaseLookup = diseaseLookup ?? throw new ArgumentNullException(nameof(diseaseLookup));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        }

        /// <inheritdoc />
        public bool Folded(int patientId)
        {
            if (patientId < 0)
                throw new ArgumentOutOfRangeException(nameof(patientId), patientId,
                    "病人 id 不得为负 —— PatientId.None(-1)是「世界级事件」哨兵,不是可折叠对象");

            // 三条件按 ①②③ 次序短路;次序不影响结论,只影响开销。
            if (!Terminal(patientId)) return false;
            if (Exempt(patientId)) return false;
            if (HasOpenCase(patientId)) return false;
            return true;
        }

        /// <summary>
        /// 条件 ① —— <c>终态(p)</c>:病史流是否存在该病人的 <see cref="EventKind.PatientOutcomeChanged"/>。
        ///
        /// <para><b>存在性查询,不重放 F4</b>(ADR-032 ⑥ 逐字)。这正是「终态从『重算』变成
        /// 『记录』」的落点 —— 代价是该事件的权威由 9 的原子性义务保证(ADR-032 ③)。</para>
        /// </summary>
        public bool Terminal(int patientId)
        {
            IReadOnlyList<SimEvent> events = _historyStream();
            if (events == null) return false;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Patient.Value == patientId && e.Kind == EventKind.PatientOutcomeChanged)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 条件 ② —— <c>豁免(p)</c>:该病人的**任一**病种属于豁免类,或携带 <c>relapse.*</c> 块。
        ///
        /// <para><b>口径对齐点</b>(GDD <c>:321</c>):豁免类 = <c>lethal = false ∧ plateau</c>。
        /// **全 <c>plateau</c> 免豁是错的** —— <c>plateau ∧ lethal</c> 型可达终态、可折叠;
        /// 把 <c>lethal</c> 那一半漏掉,该型病人会被误豁免 ⇒ 永不折叠(不清错,只是白留无界流)。</para>
        ///
        /// <para><b><c>relapse.*</c> 与 <c>plateau</c> 正交</b>:携带复发块的病种回放需完整病程,
        /// 与迁延保持无关(AC-36)。</para>
        /// </summary>
        public bool Exempt(int patientId)
        {
            IReadOnlyList<int> diseases = DiseasesOf(patientId);
            for (int i = 0; i < diseases.Count; i++)
            {
                int diseaseId = diseases[i];
                DiseaseRegistryEntry entry = _diseaseLookup(diseaseId);
                if (entry == null)
                    throw new InvalidOperationException(
                        $"病种 {diseaseId} 不在注册表内(病人 {patientId})—— 折叠谓词拒绝对未知病种下豁免结论:" +
                        "当作「非豁免」会折掉一个我们没能力评估的病人,而折叠不可逆");

                // 豁免类:lethal = false ∧ plateau。`lethal(d)` 取 Lethality > 0(承既有先例)。
                if (entry.Lethality <= 0
                    && entry.Curve != null
                    && entry.Curve.Plateau)
                {
                    return true;
                }

                // 携带 relapse.* 块 ⇒ 回放需完整病程,不折叠。
                if (entry.Relapse != null)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 条件 ③ —— <c>未结案(p)</c>。<b>复用</b> <see cref="CaseStreamQuery.HasOpenCase"/>,
        /// 不另写一份判定(第二真源禁令)。
        /// </summary>
        public bool HasOpenCase(int patientId)
        {
            IReadOnlyList<SimEvent> events = _caseStream();
            if (events == null) return false;
            return CaseStreamQuery.HasOpenCase(events, patientId);
        }

        /// <summary>
        /// 该病人携带的病种集合(按首次出现序去重)。
        ///
        /// <para><b>载体 = <c>DiseaseOnset</c></b>(ADR-030):「病人带着某病出现」是它在病史流上的
        /// 唯一记录(病人在出现那一刻就带着某个病种,不立第二个 Kind)。</para>
        /// </summary>
        public IReadOnlyList<int> DiseasesOf(int patientId)
        {
            var ids = new List<int>();
            IReadOnlyList<SimEvent> events = _historyStream();
            if (events == null) return ids;

            var seen = new HashSet<int>();
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Patient.Value != patientId || e.Kind != EventKind.DiseaseOnset)
                    continue;

                DiseaseOnsetPayload payload =
                    _decoder.Decode<DiseaseOnsetPayload>(EventKind.DiseaseOnset, e.Payload);
                if (seen.Add(payload.DiseaseId))
                    ids.Add(payload.DiseaseId);
            }
            return ids;
        }
    }
}
