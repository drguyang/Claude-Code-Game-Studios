// 权威来源:GDD F-37.1(同源检测 · FirstPerPatient · CandidateSeq · PatternFired)
//          · ADR-008 §五(盐契约:逐 D 派生)
//          · ADR-007 §二(WorldSeed 存档头)
//
// 同源检测纯函数:给定已完成流前缀(病例流 CaseClosed 事件集),返回本 tick 新触发的
// PatternRecognized 事件列表。不触 IEventSink,不触 IIdAuthority,只返回「该做什么」。
// 生产代码 —— 测试测的是本文件,不是测试自己的副本。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>同源检测结果。</summary>
    public readonly struct PatternDetectionResult
    {
        /// <summary>是否触发。</summary>
        public readonly bool Fired;
        /// <summary>触发的病种 ordinal 列表(按 salted_key 升序)。</summary>
        public readonly IReadOnlyList<int> FiredDiseases;
        /// <summary>成员集(每病种前 PATTERN_THRESHOLD 例的 case_id)。</summary>
        public readonly IReadOnlyList<CaseId> MemberSet;

        public PatternDetectionResult(bool fired, IReadOnlyList<int> firedDiseases, IReadOnlyList<CaseId> memberSet)
        {
            Fired = fired;
            FiredDiseases = firedDiseases;
            MemberSet = memberSet;
        }
    }

    /// <summary>
    /// 同源检测纯函数(GDD F-37.1)。
    /// </summary>
    public static class PatternDetector
    {
        /// <summary>
        /// 评估同源检测:给定病例流前缀,返回本 tick 新触发的 PatternRecognized 事件。
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
                candidateSeq[disease] = candidates;
            }

            // 4. 检查触发:长度 >= 阈值
            var firedDiseases = new List<int>();
            var memberSet = new List<CaseId>();
            foreach (var kvp in candidateSeq)
            {
                var disease = kvp.Key;
                var candidates = kvp.Value;
                if (candidates.Count >= patternThreshold)
                {
                    firedDiseases.Add(disease);
                    // MemberSet = 前 PATTERN_THRESHOLD 例
                    for (int i = 0; i < patternThreshold && i < candidates.Count; i++)
                    {
                        var e = candidates[i];
                        memberSet.Add(new CaseId(e.Tick, e.Patient.Value, e.Seq));
                    }
                }
            }

            // 5. 按 salted_key 升序排序
            firedDiseases.Sort((a, b) =>
            {
                ulong saltedA = SplitMix64.HashTagged(worldSeed, "case-salt", a);
                ulong saltedB = SplitMix64.HashTagged(worldSeed, "case-salt", b);
                return saltedA.CompareTo(saltedB);
            });

            return new PatternDetectionResult(firedDiseases.Count > 0, firedDiseases, memberSet);
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
        /// 判断病种是否已触发(读流:存在 PatternRecognized 事件)。
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
