// 权威来源:GDD F-37.1(同源检测 · FirstPerPatient · CandidateSeq · PatternFired)
//          · GDD F-37.1b(脚本三案链 ScriptedChain / ChainEligible · 一次性)
//          · ADR-008 §五(盐契约:逐 D 派生)
//          · ADR-007 §二(WorldSeed 存档头)
//
// 同源检测纯函数:给定已完成流前缀(病例流 CaseClosed 事件集),返回本 tick 新触发的
// PatternRecognized 事件列表。不触 IEventSink,不触 IIdAuthority,只返回「该做什么」。
// 生产代码 —— 测试测的是本文件,不是测试自己的副本。
//
// ⚠️ 两层语义,勿混:
//   · Evaluate      = **达标判定层**。返回「该 D 是否已达阈值」的**前缀属性** ——
//                     已达标的 D 在第 4、5、6 例结案后**仍然** Fired = true。
//                     这不是缺陷:AC-37-07/08 的「长度 < 阈值不触发 / 恰达标触发」测的是本层。
//   · NextEmissions = **发射决策层**。在达标之上再过「一次性」门(GDD F-37.1b:
//                     `PatternRecognized` 已发出 ⇒ 永不重发),返回**本 tick 真正应发射**者。
//                     AC-37-09/29 的「第 4 例不重发 / 永不重发」属本层。
//   ⇒ 「不重发」若写在调用方 = 未写未测的契约(Story 004 补评 CB-1/TB-4);本层即其载体。
//   · CB-3(2026-10-15 修):达标判定层的**扁平 MemberSet 已删** —— 它既按 Dictionary 迭代序
//     拼装(违 AC-37-27),又无逐 D 归属(违 registry 逐事件 member_set)。成员集现**只有**
//     NextEmissions 的逐 D 路径(PatternEmission.MemberSet)。
//   ⚠️ 写者(`Append` + 发号 `Seq`)**不在本文件** —— 本文件只到「该做什么」,不含任何
//      `IEventSink` 调用。写者已落 `Sim/PatternRecognitionWriter.cs`(2026-10-15,与同批
//      撤 `WriterExistenceGate` 的 `PatternRecognized` 具名豁免一并;**生产触发点接线**
//      仍归 37 实现轮的装配面,与本文件无关)。
//      (历史注:Story 004 补评 CB-1/TB-4 曾记「发射语义无生产载体」—— 该缺口由
//       `NextEmissions`(S11-9)+ `PatternRecognitionWriter`(2026-10-15)两件合起来闭合。)

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>同源检测结果(达标判定层)。</summary>
    /// <remarks>
    /// <para><b>只承载「哪些 D 达标」,不承载成员集</b> —— 曾经存在的扁平
    /// <c>IReadOnlyList&lt;CaseId&gt; MemberSet</c> 已**删除**(Story 004 补评 CB-3,2026-10-15),两处原因:
    /// ① 它由 `foreach (var kvp in candidateSeq)` 的 **`Dictionary` 迭代序**拼装
    ///    ⇒ 违 AC-37-27「禁集合迭代序」(该序在 C# 里不保证跨平台一致);
    /// ② 它是**扁平**结构,多 D 同时达标时无病种归属,而 registry
    ///    `SimEvent.Kind.PatternRecognized.payload_schema` 要求 `member_set` **逐事件**携带。</para>
    /// <para><b>需要成员集的调用方一律走 <see cref="PatternDetector.NextEmissions"/></b> ——
    /// 它返回<b>逐 D</b> 的 <see cref="PatternEmission.MemberSet"/>(按全序键 (Tick, Patient, Seq)
    /// 定序、与 `Dictionary` 迭代序无关)。删除前的消费者调查:全仓仅测试件引用该字段,
    /// 生产侧唯一调用点(<c>PatternRecognitionWriter</c>)只读 <see cref="Fired"/>。</para>
    /// </remarks>
    public readonly struct PatternDetectionResult
    {
        /// <summary>是否触发。</summary>
        public readonly bool Fired;
        /// <summary>触发的病种 ordinal 列表(按 salted_key 升序)。</summary>
        public readonly IReadOnlyList<int> FiredDiseases;

        public PatternDetectionResult(bool fired, IReadOnlyList<int> firedDiseases)
        {
            Fired = fired;
            FiredDiseases = firedDiseases;
        }
    }

    /// <summary>
    /// 脚本三案链定义(GDD F-37.1b `ScriptedChain(D)` 的载体)。
    /// </summary>
    /// <remarks>
    /// <para><b>只承载机制输入,不含任何具体 patient_id</b> —— 具名角色 → 数字 id 的绑定
    /// 属**内容值**:GDD F-37.1b 逐字「具名角色 → 数字 <c>patient_id</c> 的绑定仍属内容值:
    /// 由 7a 高水位在首次立案时分配,绑定落 ADR-014 烘焙(承「骨架先行,载荷归内容」纪律 ——
    /// 本 GDD 只登记机制与判定域,**不落数值**)」。P0 尚无该烘焙产物 ⇒ 本类型的值由调用方注入
    /// (测试注入字面量;生产由 ADR-014 烘焙产物经装配层提供)。</para>
    /// <para><b>⚠️ 声明序不参与判定</b> —— GDD F-37.1b 同时写明「触发仍走 F-37.1 的**同一算式**
    /// (只是 <c>CandidateSeq</c> 的定义域收窄到 <c>ScriptedChain(D)</c> 的成员)⇒ **不需要第二套公式**」,
    /// 而该算式按全序键 <c>(Tick, Patient, Seq)</c> 定序 ⇒ <see cref="MemberPatientIds"/> 的**排列
    /// 不被消费**。该「有序」只作内容语义(叙事先后 甲 → 乙 → 丙)。此读法已由
    /// <c>test_ac3729_declarationOrder_notConsumed</c> 钉死(2026-10-15 用户裁定)。</para>
    /// </remarks>
    public readonly struct ScriptedChain
    {
        /// <summary>该链全体成员所属的病种 ordinal(GDD F-37.1b「同 D = 三次失血」)。</summary>
        public readonly int DiseaseOrdinal;

        /// <summary>显式指定的成员 patient_id(内容值 —— 由烘焙 / 调用方注入,非本层常量)。</summary>
        public readonly IReadOnlyList<int> MemberPatientIds;

        public ScriptedChain(int diseaseOrdinal, IReadOnlyList<int> memberPatientIds)
        {
            DiseaseOrdinal = diseaseOrdinal;
            MemberPatientIds = memberPatientIds;
        }
    }

    /// <summary>
    /// 脚本链成员判定域(GDD F-37.1b `ChainEligible(D)` 的兑现)。
    /// </summary>
    public static class ChainEligible
    {
        /// <summary>
        /// 病例 c 是否属该链 —— GDD F-37.1b 逐字定义:`ChainEligible(D) := 病例 c 满足
        /// c.Patient ∈ ScriptedChain(D)`。
        /// </summary>
        /// <param name="chain">脚本链。</param>
        /// <param name="patientId">病例的 patient_id。</param>
        /// <returns>该病人是否为链成员。</returns>
        public static bool IsEligible(in ScriptedChain chain, int patientId)
        {
            var members = chain.MemberPatientIds;
            if (members == null)
                return false;

            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] == patientId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 从链集中取覆盖某病种的链。同一 D 至多一条链(违反 = 内容数据错误,
        /// 取首条 —— 本层不做校验,数据合法性归 ADR-014 阶段 2)。
        /// </summary>
        /// <param name="chains">链集(可空 —— 空 = 无脚本链,全走自然检测)。</param>
        /// <param name="diseaseOrdinal">病种 ordinal。</param>
        /// <param name="chain">命中的链。</param>
        /// <returns>是否命中。</returns>
        public static bool TryChainOf(
            IReadOnlyList<ScriptedChain> chains, int diseaseOrdinal, out ScriptedChain chain)
        {
            if (chains != null)
            {
                for (int i = 0; i < chains.Count; i++)
                {
                    if (chains[i].DiseaseOrdinal == diseaseOrdinal)
                    {
                        chain = chains[i];
                        return true;
                    }
                }
            }

            chain = default;
            return false;
        }
    }

    /// <summary>
    /// 本 tick **应发射**的一条 PatternRecognized 的决定量(发射决策层输出)。
    /// </summary>
    /// <remarks>
    /// 调用方据此构造载荷并 `Append`(写者仍归 37 实现轮);本类型**不含** `Seq`
    /// (发号权归 <c>IIdAuthority</c> / 主机)与 `Tick`(发号时刻)。
    /// </remarks>
    public readonly struct PatternEmission
    {
        /// <summary>病种 ordinal。</summary>
        public readonly int DiseaseOrdinal;

        /// <summary>salted_key = `SplitMix64(WorldSeed, "case-salt", ordinal(D))`(ADR-008 §五)。</summary>
        public readonly ulong SaltedKey;

        /// <summary>成员集(冻结于触发 tick;逐 D 归属 —— 即 `member_set` 载荷)。</summary>
        public readonly IReadOnlyList<CaseId> MemberSet;

        public PatternEmission(int diseaseOrdinal, ulong saltedKey, IReadOnlyList<CaseId> memberSet)
        {
            DiseaseOrdinal = diseaseOrdinal;
            SaltedKey = saltedKey;
            MemberSet = memberSet;
        }
    }

    /// <summary>
    /// 同源检测纯函数(GDD F-37.1 / F-37.1b)。
    /// </summary>
    public static class PatternDetector
    {
        /// <summary>
        /// 评估同源检测(无脚本链 —— 全走自然检测)。
        /// </summary>
        /// <param name="caseEvents">病例流事件列表(已按全序排列)。</param>
        /// <param name="worldSeed">WorldSeed(存档头)。</param>
        /// <param name="patternThreshold">阈值(P0 = 3)。</param>
        /// <param name="diseaseExtractor">从事件提取病种 ordinal 数组的委托(生产 = 解码 CaseClosedPayload.DiseaseSet)。</param>
        /// <returns>检测结果。</returns>
        public static PatternDetectionResult Evaluate(
            IReadOnlyList<SimEvent> caseEvents, long worldSeed, int patternThreshold,
            Func<SimEvent, int[]> diseaseExtractor)
        {
            return Evaluate(caseEvents, worldSeed, patternThreshold, diseaseExtractor, null);
        }

        /// <summary>
        /// 评估同源检测:给定病例流前缀,返回本 tick 新触发的 PatternRecognized 事件。
        /// </summary>
        /// <param name="caseEvents">病例流事件列表(已按全序排列)。</param>
        /// <param name="worldSeed">WorldSeed(存档头)。</param>
        /// <param name="patternThreshold">阈值(P0 = 3)。</param>
        /// <param name="diseaseExtractor">从事件提取病种 ordinal 数组的委托(生产 = 解码 CaseClosedPayload.DiseaseSet)。</param>
        /// <param name="scriptedChains">
        /// 脚本三案链集(GDD F-37.1b)。`null` / 空 = 无脚本链 ⇒ 全部病种走自然检测。
        /// 命中链的病种,其 `CandidateSeq` 定义域收窄到链成员(逐 D 二选一,非全局模式)。
        /// </param>
        /// <returns>检测结果(达标判定层 —— 见文件头两层语义说明)。</returns>
        public static PatternDetectionResult Evaluate(
            IReadOnlyList<SimEvent> caseEvents, long worldSeed, int patternThreshold,
            Func<SimEvent, int[]> diseaseExtractor,
            IReadOnlyList<ScriptedChain> scriptedChains)
        {
            var candidateSeq = ComputeCandidateSeq(caseEvents, diseaseExtractor, scriptedChains);

            // 4. 检查触发:长度 >= 阈值
            //
            // ⚠️ 本循环**只产出 FiredDiseases** —— 成员集不在此产出(CB-3:此处曾按
            //    `candidateSeq` 的 `Dictionary` 迭代序拼一个扁平 memberSet,两次违 AC-37-27;
            //    成员集现只经 NextEmissions 的逐 D 路径)。产出顺序不构成契约:紧随其后的
            //    SortBySaltedKey 才是唯一定序源。
            var firedDiseases = new List<int>();
            foreach (var kvp in candidateSeq)
            {
                if (kvp.Value.Count >= patternThreshold)
                    firedDiseases.Add(kvp.Key);
            }

            // 5. 按 salted_key 升序排序
            SortBySaltedKey(firedDiseases, worldSeed);

            return new PatternDetectionResult(firedDiseases.Count > 0, firedDiseases);
        }

        /// <summary>
        /// 本 tick **应发射**的 PatternRecognized(发射决策层)—— 达标之上再过「一次性」门。
        /// </summary>
        /// <remarks>
        /// <para>GDD F-37.1b 逐字:「🔴 **一次性**(用户裁定):`PatternRecognized` 已经发出 ⇒
        /// **永不重发**,与现有『第 4 例不重发』同一条规则。」本方法即该规则的**生产载体** ——
        /// 流中已存在同 `salted_key` 的 `PatternRecognized` ⇒ 该 D 不再出现在返回值里。</para>
        /// <para>返回顺序 = `salted_key` 整数升序(AC-37-27:同 tick 多 D 按 salted_key 升序写,
        /// 禁 `disease_set` 迭代序)。</para>
        /// <para>⚠️ 与 <see cref="Evaluate"/> 的关系:`Evaluate` 返回**前缀属性**(达标即恒真),
        /// 本方法返回**增量**(已发过的 D 被滤掉)。两者都保留是刻意的 —— 前者的性质由
        /// AC-37-07/08 锁定,后者的性质由 AC-37-09/29 锁定。</para>
        /// </remarks>
        /// <param name="caseEvents">病例流事件列表(已按全序排列)。</param>
        /// <param name="worldSeed">WorldSeed(存档头)。</param>
        /// <param name="patternThreshold">阈值(P0 = 3)。</param>
        /// <param name="diseaseExtractor">从 CaseClosed 提取病种 ordinal 数组的委托。</param>
        /// <param name="saltedKeyExtractor">从 `PatternRecognized` 提取 salted_key 的委托(载荷解码归 `Sim.Codec`)。</param>
        /// <param name="scriptedChains">脚本三案链集(可空)。</param>
        /// <returns>本 tick 应发射的列表(空 = 无事可做)。</returns>
        public static IReadOnlyList<PatternEmission> NextEmissions(
            IReadOnlyList<SimEvent> caseEvents, long worldSeed, int patternThreshold,
            Func<SimEvent, int[]> diseaseExtractor,
            Func<SimEvent, ulong> saltedKeyExtractor,
            IReadOnlyList<ScriptedChain> scriptedChains)
        {
            var candidateSeq = ComputeCandidateSeq(caseEvents, diseaseExtractor, scriptedChains);

            // 达标集(顺序统一走 SortBySaltedKey 单一定序源)
            var firedDiseases = new List<int>();
            foreach (var kvp in candidateSeq)
            {
                if (kvp.Value.Count >= patternThreshold)
                    firedDiseases.Add(kvp.Key);
            }
            SortBySaltedKey(firedDiseases, worldSeed);

            var emissions = new List<PatternEmission>(firedDiseases.Count);
            for (int d = 0; d < firedDiseases.Count; d++)
            {
                int disease = firedDiseases[d];

                // 「一次性」门(GDD F-37.1b)
                if (IsFired(caseEvents, disease, worldSeed, saltedKeyExtractor))
                    continue;

                // 逐 D 切成员(唯一路径 —— PatternDetectionResult 的扁平 MemberSet 已于 2026-10-15 删除,见 CB-3)
                var candidates = candidateSeq[disease];
                var members = new List<CaseId>(patternThreshold);
                for (int i = 0; i < patternThreshold && i < candidates.Count; i++)
                {
                    var e = candidates[i];
                    members.Add(new CaseId(e.Tick, e.Patient.Value, e.Seq));
                }

                emissions.Add(new PatternEmission(disease, ComputeSaltedKey(worldSeed, disease), members));
            }

            return emissions;
        }

        /// <summary>
        /// 构造各病种的 `CandidateSeq`(GDD F-37.1 步骤 1–3 + F-37.1b 定义域收窄)。
        /// </summary>
        /// <remarks>
        /// ⚠️ 返回值的 **`Dictionary` 迭代序不是契约** —— 任何依赖它的调用方都违 AC-37-27
        /// (补评 CB-3 ①)。需要定序处一律走 <see cref="SortBySaltedKey"/>。
        /// </remarks>
        private static Dictionary<int, List<SimEvent>> ComputeCandidateSeq(
            IReadOnlyList<SimEvent> caseEvents,
            Func<SimEvent, int[]> diseaseExtractor,
            IReadOnlyList<ScriptedChain> scriptedChains)
        {
            // 1. 收集所有 CaseClosed 事件
            var closedEvents = new List<SimEvent>();
            foreach (var e in caseEvents)
            {
                if (e.Kind == EventKind.CaseClosed)
                    closedEvents.Add(e);
            }

            // 2. 按病种分组:每病种取 FirstPerPatient(每病人全序最早例)
            var firstPerPatient = new Dictionary<int, List<SimEvent>>();
            foreach (var e in closedEvents)
            {
                foreach (int diseaseOrdinal in diseaseExtractor(e))
                {
                    if (!firstPerPatient.TryGetValue(diseaseOrdinal, out var list))
                    {
                        list = new List<SimEvent>();
                        firstPerPatient[diseaseOrdinal] = list;
                    }
                    list.Add(e);
                }
            }

            // 3. 每病种去重:每病人只保留全序最早例
            var candidateSeq = new Dictionary<int, List<SimEvent>>();
            foreach (var kvp in firstPerPatient)
            {
                var disease = kvp.Key;
                var events = kvp.Value;
                var perPatient = new Dictionary<int, SimEvent>();
                foreach (var e in events)
                {
                    int patient = e.Patient.Value;
                    if (!perPatient.TryGetValue(patient, out var existing) ||
                        CompareFullOrder(e, existing) < 0)
                    {
                        perPatient[patient] = e;
                    }
                }
                var candidates = new List<SimEvent>(perPatient.Values);
                candidates.Sort((a, b) =>
                {
                    int cmp = a.Tick.CompareTo(b.Tick);
                    if (cmp != 0) return cmp;
                    cmp = a.Patient.Value.CompareTo(b.Patient.Value);
                    if (cmp != 0) return cmp;
                    return a.Seq.CompareTo(b.Seq);
                });

                // 3b.【GDD F-37.1b】脚本链定义域收窄 —— 逐 D 二选一
                //
                // GDD 逐字:「成员判定域 = 显式指定,不是『全局首次三例』(旧算法的行为
                // 保留给**非脚本**的同源检测,即自然疫情)。脚本条目点名的三个 patient_id
                // 之外的同病种病例 ⇒ 不进该链、不消耗该链的判定名额。」
                // ⇒ 命中链的病种:候选只留链成员(自然病例既不计数也不占名额);
                //   未命中链的病种:维持 F-37.1 原算式(自然检测)。
                if (ChainEligible.TryChainOf(scriptedChains, disease, out var chain))
                {
                    var narrowed = new List<SimEvent>(candidates.Count);
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        if (ChainEligible.IsEligible(chain, candidates[i].Patient.Value))
                            narrowed.Add(candidates[i]);
                    }
                    candidates = narrowed;
                }

                candidateSeq[disease] = candidates;
            }

            return candidateSeq;
        }

        /// <summary>
        /// 病种定序(单一定序源):<paramref name="keyOf"/> 派生键**升序**;键相等时以
        /// **ordinal 升序**为次级键(AC-37-27 / ADR-008 §五)。
        /// </summary>
        /// <remarks>
        /// <para><b>⚠️ 次级键的由来(2026-10-15 ADVISORY 清理轮)</b> —— <c>List.Sort</c> 是
        /// **不稳定排序** ⇒ 64 位键碰撞时次序**未定义**,与 AC-37-27 后半句「换一个插入序重跑,
        /// 事件流**逐位相同**」有理论张力。补次级键后比较器构成**全序**,排序结果与
        /// <c>List.Sort</c> 的实现细节无关。</para>
        /// <para><b>⚠️ 为什么形参是委托而不是 <c>long worldSeed</c>(可捕获性要求)</b> ——
        /// 键空间 2⁶⁴ 使**真实碰撞在 P0 不可构造** ⇒ 若内部硬取 <c>SplitMix64.HashTagged</c>,
        /// 次级键**永远没有可证伪夹具**(改回无次级键,测试照样全绿)。取注入形后,测试可喂
        /// **人为碰撞键**(如 <c>d =&gt; (ulong)(d % 2)</c>)**端到端**打这条 tie-break。
        /// 生产调用点见 <see cref="Evaluate"/> / <see cref="NextEmissions"/>。</para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">任一形参为 null。</exception>
        public static void SortBySaltedKey(List<int> diseases, Func<int, ulong> keyOf)
        {
            if (diseases == null) throw new ArgumentNullException(nameof(diseases));
            if (keyOf == null) throw new ArgumentNullException(nameof(keyOf));

            diseases.Sort((a, b) =>
            {
                int cmp = keyOf(a).CompareTo(keyOf(b));
                return cmp != 0 ? cmp : a.CompareTo(b);   // 次级键:ordinal 升序
            });
        }

        /// <summary>生产用便捷形:键 = <c>SplitMix64.HashTagged(worldSeed, "case-salt", D)</c>(ADR-008 §五 盐契约)。</summary>
        private static void SortBySaltedKey(List<int> diseases, long worldSeed)
        {
            SortBySaltedKey(diseases, d => SplitMix64.HashTagged(worldSeed, "case-salt", d));
        }

        /// <summary>
        /// 全序比较:(Tick, Patient, Seq) 升序。
        /// </summary>
        private static int CompareFullOrder(SimEvent a, SimEvent b)
        {
            int cmp = a.Tick.CompareTo(b.Tick);
            if (cmp != 0) return cmp;
            cmp = a.Patient.Value.CompareTo(b.Patient.Value);
            if (cmp != 0) return cmp;
            return a.Seq.CompareTo(b.Seq);
        }

        /// <summary>
        /// 计算 salted_key(GDD F-37.1 / ADR-008 §五)。
        /// </summary>
        /// <param name="worldSeed">WorldSeed。</param>
        /// <param name="diseaseOrdinal">病种 ordinal。</param>
        /// <returns>salted_key。</returns>
        public static ulong ComputeSaltedKey(long worldSeed, int diseaseOrdinal)
        {
            return SplitMix64.HashTagged(worldSeed, "case-salt", diseaseOrdinal);
        }

        /// <summary>
        /// 判断病种是否已触发(读流:存在同 salted_key 的 PatternRecognized 事件)。
        /// </summary>
        /// <param name="caseEvents">病例流事件列表。</param>
        /// <param name="diseaseOrdinal">病种 ordinal。</param>
        /// <param name="worldSeed">WorldSeed。</param>
        /// <param name="saltedKeyExtractor">从 PatternRecognized 事件提取 salted_key 的委托。</param>
        /// <returns>是否已触发。</returns>
        public static bool IsFired(
            IReadOnlyList<SimEvent> caseEvents, int diseaseOrdinal, long worldSeed,
            Func<SimEvent, ulong> saltedKeyExtractor)
        {
            ulong saltedKey = ComputeSaltedKey(worldSeed, diseaseOrdinal);
            foreach (var e in caseEvents)
            {
                if (e.Kind != EventKind.PatternRecognized)
                    continue;
                if (saltedKeyExtractor(e) == saltedKey)
                    return true;
            }
            return false;
        }
    }
}
