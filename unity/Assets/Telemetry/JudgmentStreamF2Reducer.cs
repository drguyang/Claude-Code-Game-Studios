// 权威来源:production/epics/telemetry-analytics/story-003-f2-misdiagnosis-matrix.md
//   (AC-51-B5…B7 — F2 误诊分布:混淆矩阵 M[t][j] + 三桶显式计数 + F1a 恒等式)
// ADR-019 §一(回放即数据记录)· ADR-006 §五(整数 (num, den) 对)
//
// ════════════════════════════════════════════════════════════════════════════════
// ⚠️ 本文件是 TD-epic-01 还债件(F2 接线):「事件流 → CaseRecord 序列」的还原器,
//    此前 F2 只有 EditMode 夹具驱动(喂假 CaseRecord),生产零调用 ⇒ AC-51-B5/B6/B7
//    在生产里无承载。本类把真 CaseClosed/JudgmentRecorded/Revised 还原为
//    MisdiagnosisMatrixMetrics.CaseRecord,喂给生产指标类。
//
// ⚠️ 为何用解码委托而非直接引 Sim.Codec:PayloadRef 只寻址,载荷内容须经
//    IPayloadDecoder(ADR-031),其实现在 Sim.Codec;Telemetry 装配的引用集被
//    AC-51-A2 钉死 ⊆ {Sim.Contracts} ⇒ 物理引不到 decode 实现。委托注入(与
//    ADR-031 / ADR-029 同型):调用方(装配层)把 decoder 包成委托传进来,
//    51 仍是纯消费者。
// ════════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Telemetry
{
    /// <summary>把事件流还原为 F2 的输入序列
    /// <see cref="MisdiagnosisMatrixMetrics.CaseRecord"/>。
    /// <para>**纯函数**(GDD R8):不读墙钟 / 帧序 / 执行次数 ⇒ 同流同值,可进
    /// ADR-012 黄金夹具。</para>
    /// <para>**零埋点**:输入是既有病例流;不新增任何采集路径。</para>
    /// <para>⚠️ 调用方须已按全序键 (Tick, StreamPriority, Patient, Seq) 排好流
    /// —— 本类不重排,排序权归重算器的单一出处,防两份排序真源。</para></summary>
    public static class JudgmentStreamF2Reducer
    {
        /// <summary>解码委托:从 PayloadRef 取载荷内容(由装配层注入)。
        /// <para>返回 false = 该 ref 无对应载荷(解码失败 / 未驻留),该条跳过,
        /// 不抛。</para>
        /// <para>⚠️ <b>`failure` 形参与 F1 侧同款(`TD-epic-01-b` A 方案,2026-10-30)</b>:
        /// 返回 false 时须填失败类别,让还原器能分类计数。两个 reducer 的委托形状
        /// **刻意保持一致** —— 装配层可以把同一个适配器喂给两者。</para></summary>
        public delegate bool DecodeJudgment(PayloadRef payloadRef, EventKind kind,
            out DecodedJudgment decoded, out DecodeFailureKind failure);

        /// <summary>解码后的一条判断(病例 + 病种集 + 是否改写)。
        /// <para>⚠️ DiseaseIds 元素 = 病种 ordinal(CaseClosed 位掩码展开)或
        /// lexicon_id(JudgmentRecorded/Revised)。元素以字符串形式喂入
        /// <see cref="MisdiagnosisMatrixMetrics.CaseRecord.Truth/.Judged">,
        /// diseaseEnum 由调用方传入对应的 ordinal 字符串集合。</para></summary>
        public readonly struct DecodedJudgment
        {
            /// <summary>病例 id。</summary>
            public readonly int CaseId;

            /// <summary>该条落笔的病种集(LexiconId / 位掩码 ordinal 集;空 = 未落笔)。</summary>
            public readonly IReadOnlyList<int> DiseaseIds;

            /// <summary>是否为「改写史」条目(存新值,非差分)。</summary>
            public readonly bool IsRevision;

            public DecodedJudgment(int caseId, IReadOnlyList<int> diseaseIds,
                bool isRevision)
            {
                CaseId = caseId;
                DiseaseIds = diseaseIds;
                IsRevision = isRevision;
            }
        }

        /// <summary>从病例流还原出 F2 的输入序列(每个 case 一条)。
        /// <para>**全序取值**:同 case 多条判断时取**全序最后一条**作 J(c) ——
        /// 判据不是插入序,是 (Tick, StreamPriority, Patient, Seq) 全序键。</para>
        /// <para>**Scorable 口径由 <see cref="MisdiagnosisMatrixMetrics.Compute"/>
        /// 统一做**:有真值 ∧ 有判断 ⇒ 进矩阵;无真值 ⇒ N_open;无判断 ⇒ N_open;
        /// 判断不可映射 ⇒ N_unmappable;共病 ⇒ N_comorbid(并计入 N_matched_comorbid
        /// 若 D == J)。</para></summary>
        /// <param name="caseStream">病例流事件(已按全序键排序)。</param>
        /// <param name="decode">解码委托(见 <see cref="DecodeJudgment"/>)。</param>
        /// <param name="skipped">解码失败 / 载荷不可用而被跳过的条数,**按类别分开**
        /// (`TD-epic-01-b` A 方案;`.Total` 是总数)。</param>
        public static IReadOnlyList<MisdiagnosisMatrixMetrics.CaseRecord> Reduce(
            IReadOnlyList<SimEvent> caseStream, DecodeJudgment decode, out SkipBreakdown skipped)
        {
            skipped = default;
            if (caseStream == null || caseStream.Count == 0 || decode == null)
                return new List<MisdiagnosisMatrixMetrics.CaseRecord>();

            // 第一遍:收集每个 case 的真值(CaseClosed)与最后一条判断(全序最后一个)
            //
            // ⚠️ JudgmentMappable 始终 = true(对输出的每条记录):
            //   · 有判断事件 ⇒ judged 可能为 ∅(落笔判空集) ⇒ 进「未落笔」列,不进 N_unmappable;
            //   · 无判断事件 ⇒ judged = ∅ ⇒ 同「未落笔」;
            //   · 真正的「不可映射」(携带不在 diseaseEnum 内的病种/词条)交给
            //     <see cref="MisdiagnosisMatrixMetrics.Compute"/> 里的 diseaseEnum 含性检查
            //     产生 N_unmappable(AC-51-B7:不静默丢,且不给 51 加解析器)。
            // 只有「无 CaseClosed」才进 N_open(由 HasCaseClosed 控制)。
            var truthOf = new Dictionary<int, HashSet<string>>();
            var lastJudgedOf = new Dictionary<int, HashSet<string>>();
            var hasCaseClosed = new HashSet<int>();
            var caseOrder = new List<int>();   // 保持 case 首现序(输出确定性)

            for (int i = 0; i < caseStream.Count; i++)
            {
                SimEvent e = caseStream[i];
                if (!decode(e.Payload, e.Kind, out DecodedJudgment d, out DecodeFailureKind why))
                {
                    skipped = skipped.Plus(why);   // 可观测**且分类**,不静默
                    continue;
                }

                switch (e.Kind)
                {
                    case EventKind.CaseClosed:
                    {
                        // 真值:CaseClosed.disease_set(位掩码 → ordinal 升序 → 字符串)。
                        // 元素已由调用方统一成同一字符串空间(见 TelemetryReporter.DecodeF2:
                        // truth = 位掩码 bit 位置字符串 = disease id; judged = 经 lexicon→disease
                        // 映射后的 disease id 字符串),二者可进同一 diseaseEnum 做含性检查。
                        var t = new HashSet<string>();
                        for (int k = 0; k < d.DiseaseIds.Count; k++)
                            t.Add(d.DiseaseIds[k].ToString());
                        truthOf[d.CaseId] = t;
                        hasCaseClosed.Add(d.CaseId);
                        if (!caseOrder.Contains(d.CaseId))
                            caseOrder.Add(d.CaseId);
                        break;
                    }

                    case EventKind.JudgmentRecorded:
                    case EventKind.JudgmentRevised:
                    {
                        // 全序最后一个 ⇒ 直接覆盖(数组已排序,后到即全序后)。
                        // judged 可能为 ∅(空判断) ⇒ 进「未落笔」列;不因空而 mappable=false。
                        var j = new HashSet<string>();
                        for (int k = 0; k < d.DiseaseIds.Count; k++)
                            j.Add(d.DiseaseIds[k].ToString());
                        lastJudgedOf[d.CaseId] = j;
                        if (!caseOrder.Contains(d.CaseId))
                            caseOrder.Add(d.CaseId);
                        break;
                    }

                    default:
                        // CaseOpened / PatternRecognized / 读数为案 等 —— F2 不用,
                        // 跳过但不计 skipped(合法事件,不是解码失败)
                        break;
                }
            }

            // 第二遍:每个 case 产一条 CaseRecord
            var result = new List<MisdiagnosisMatrixMetrics.CaseRecord>();
            for (int i = 0; i < caseOrder.Count; i++)
            {
                int cid = caseOrder[i];
                bool hasTruth = truthOf.TryGetValue(cid, out HashSet<string> truth);
                bool hasJudged = lastJudgedOf.TryGetValue(cid, out HashSet<string> judged);

                result.Add(new MisdiagnosisMatrixMetrics.CaseRecord
                {
                    CaseId = cid,
                    Truth = hasTruth ? truth : new HashSet<string>(),
                    Judged = hasJudged ? judged : new HashSet<string>(),
                    HasCaseClosed = hasCaseClosed.Contains(cid),
                    // 可打分 ⇔ 有 CaseClosed;「无判断」≠「不可打分」,判空集 = J=∅「未落笔」.
                    JudgmentMappable = true,
                });
            }
            return result;
        }
    }
}
