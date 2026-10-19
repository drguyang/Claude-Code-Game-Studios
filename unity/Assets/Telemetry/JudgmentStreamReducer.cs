// 权威来源:ADR-019 §一(**回放即数据记录** —— 51 的输入是既有事件流,零新埋点)·
//   `design/gdd/telemetry-analytics.md` R2(不埋点:量必须**已可从事件流重算**)·
//   ADR-005(三流唯一真源)· ADR-029/031(`PayloadRef` 只寻址,内容须经 `IPayloadDecoder`)。
//
// ═══════════════════════════════════════════════════════════════════════════════
// ⚠️ 本文件是 TD-epic-01 的还债件(2026-10-19 第四片):「事件流 → `JudgmentRecord` 的还原器」
//    —— ADR-019 §五 P0 切面的第一项(**指标重算器**的输入侧),此前完全不存在。
//
// ⚠️ **为何用解码委托而非直接引 `Sim.Codec`**:`PayloadRef` 只带 `BlobId/Offset/Length`,
//    载荷**内容**要 `IPayloadDecoder`(ADR-031),其实现在 `Sim.Codec`。而 `Telemetry` 装配的
//    引用集被 `AC-51-A2` 钉死 ⊆ {`Sim.Contracts`}(GDD R3「不进 sim 程序集」的编译期面)
//    ⇒ **物理上引不到** decode 实现。解法 = 委托注入(与 ADR-031「池依赖在实现的构造函数,
//    不在接口签名」同型):调用方(装配层)把 decoder 包成委托传进来,51 仍是纯消费者。
// ═══════════════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Globalization;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Telemetry
{
    /// <summary>把事件流还原成 <see cref="JudgmentRecord"/> 序列(ADR-019 §一「回放即数据记录」的落地)。
    /// <para>**纯函数**(GDD R8):不读墙钟 / 帧序 / 执行次数 ⇒ 同流同值,可进 ADR-012 黄金夹具。</para>
    /// <para>**零埋点**(GDD R2):输入是既有病例流;不新增任何采集路径。</para></summary>
    public static class JudgmentStreamReducer
    {
        /// <summary>解码委托:从 `PayloadRef` 取载荷内容。
        /// <para>⚠️ 由**装配层**注入(它引得到 `Sim.Codec` 的 `PayloadDecoder`);
        /// 51 自己不解码 —— 这是 `AC-51-A2` 引用集钉死的必然结果,不是偷懒。</para>
        /// <para>返回 false = 该 ref 无对应载荷(.Skip 该条,不抛)。</para></summary>
        public delegate bool DecodeJudgment(PayloadRef payloadRef, EventKind kind,
                                            out JudgmentStreamReducer.DecodedJudgment decoded);

        /// <summary>解码后的一条判断(还原器的中间形状)。
        /// <para>⚠️ **只用全文中的三件事**:case / 病种集 / 是否改写。
        /// 置信度 / 作者 / 落笔时刻**不进 F1**(F1 只问「判对没判对、改没改」)——
        /// 少折字段 = 少一处可分叉真源。</para></summary>
        public readonly struct DecodedJudgment
        {
            /// <summary>病例 id。</summary>
            public readonly int CaseId;

            /// <summary>该条落笔的病种集(LexiconId 集;空 = 未落笔)。</summary>
            public readonly IReadOnlyList<int> DiseaseIds;

            /// <summary>是否为「改写史」条目(存新值,非差分)。</summary>
            public readonly bool IsRevision;

            /// <summary>构造。</summary>
            public DecodedJudgment(int caseId, IReadOnlyList<int> diseaseIds, bool isRevision)
            {
                CaseId = caseId;
                DiseaseIds = diseaseIds;
                IsRevision = isRevision;
            }
        }

        /// <summary>从**病例流**还原出 F1 的输入序列。
        /// <para>**全序取值**(ADR-005):同 case 多条判断时取**全序最后一条**作 J(c) ——
        /// 判据不是插入序,是 `(Tick, StreamPriority, Patient, Seq)` 全序键。</para>
        /// <para>**真值源**:`CaseClosed.disease_set`(GDD F1 逐字「用 CaseClosed.disease_set(模拟真值)」)。</para></summary>
        /// <param name="caseStream">病例流事件(**调用方须已按全序键排好序** —— 本类不重排,
        /// 排序权归重算器的单一出处,防两份排序真源)。</param>
        /// <param name="decode">解码委托(见 <see cref="DecodeJudgment"/>)。</param>
        /// <param name="skipped">解码失败 / 载荷形状不符而被跳过的条数(可观测;非静默吞)。</param>
        public static IReadOnlyList<JudgmentRecord> Reduce(
            IReadOnlyList<SimEvent> caseStream, DecodeJudgment decode, out int skipped)
        {
            skipped = 0;
            if (caseStream == null || caseStream.Count == 0 || decode == null)
                return new List<JudgmentRecord>();

            // 第一遍:收集每个 case 的**真值**(CaseClosed)与**最后一条判断**(全序最后一个)
            var truthOf = new Dictionary<int, HashSet<string>>();
            var lastJudgedOf = new Dictionary<int, HashSet<string>>();
            var revisedOf = new HashSet<int>();
            var caseOrder = new List<int>();          // 保持 case 首现序(输出确定性)

            for (int i = 0; i < caseStream.Count; i++)
            {
                SimEvent e = caseStream[i];
                if (!decode(e.Payload, e.Kind, out DecodedJudgment d))
                {
                    skipped++;                        // 可观测,不静默
                    continue;
                }

                switch (e.Kind)
                {
                    case EventKind.CaseClosed:
                        // 真值:CaseClosed.disease_set(一次一案;重复 ⇒ 后者覆盖)
                        var t = new HashSet<string>();
                        for (int k = 0; k < d.DiseaseIds.Count; k++)
                            t.Add(d.DiseaseIds[k].ToString(CultureInfo.InvariantCulture));
                        truthOf[d.CaseId] = t;
                        // ⚠️ **不在此处进 caseOrder**:Scorable 判据由第二遍统一做
                        //   (同时要求「有真值 **且** 有判断」)。首版在这里按
                        //   `!lastJudgedOf.ContainsKey(...)` 放行 ⇒ 有真值无判断的 case
                        //   会漏进 caseOrder 再被第二遍的 continue 丢掉 —— 逻辑对但绕。
                        if (!caseOrder.Contains(d.CaseId))
                            caseOrder.Add(d.CaseId);
                        break;

                    case EventKind.JudgmentRecorded:
                    case EventKind.JudgmentRevised:
                        // 全序最后一个 ⇒ 直接覆盖(数组已排序,后到即全序后)
                        var j = new HashSet<string>();
                        for (int k = 0; k < d.DiseaseIds.Count; k++)
                            j.Add(d.DiseaseIds[k].ToString(CultureInfo.InvariantCulture));
                        lastJudgedOf[d.CaseId] = j;
                        if (d.IsRevision || e.Kind == EventKind.JudgmentRevised)
                            revisedOf.Add(d.CaseId);
                        if (!caseOrder.Contains(d.CaseId))
                            caseOrder.Add(d.CaseId);
                        break;

                    default:
                        // CaseOpened / PatternRecognized / 读数为案 等 —— F1 不用,跳过但**不**计 skipped
                        // (它们是合法事件,不是解码失败)
                        break;
                }
            }

            // 第二遍:每个 case 产一条记录
            //   ⚠️ **Scorable = 有真值 **且** 有判断**(GDD F1 逐字「可打分的」):
            //     · 无真值(未结案)⇒ 不进分母;
            //     · 无判断(未落笔)⇒ 不进分母(**不是** J=∅ 判错 —— 那是「落笔判空集」,另一回事);
            //     · 两者皆有但判断是空集 ⇒ 进分母且 J=∅(判错)。
            var result = new List<JudgmentRecord>();
            for (int i = 0; i < caseOrder.Count; i++)
            {
                int cid = caseOrder[i];
                if (!truthOf.TryGetValue(cid, out HashSet<string> truth)) continue;     // 无真值 ⇒ 不可打分
                if (!lastJudgedOf.TryGetValue(cid, out HashSet<string> judged)) continue; // 无判断 ⇒ 不可打分

                result.Add(new JudgmentRecord(cid, truth, judged, revisedOf.Contains(cid)));
            }
            return result;
        }
    }
}
