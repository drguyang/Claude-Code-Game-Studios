// 权威来源:production/epics/telemetry-analytics/story-003-f2-misdiagnosis-matrix.md
//   (AC-51-B5…B7 — F2 误诊分布:混淆矩阵 M[t][j] + 三桶显式计数 + F1a 恒等式)
//   · M[t][j] = |{c ∈ Scorable_single : truth(c)=t ∧ judged(c)=j}|;形状 = (|病种|+1)²
//   · Scorable_single = {c ∈ Scorable : |D(c)| ≤ 1 ∧ |J(c)| ≤ 1}(对称哨兵)
//   · 三桶:N_comorbid(|D|≥2∨|J|≥2)· N_open(无 CaseClosed)· N_unmappable(未携带病种枚举)
//   · F1a 恒等式 = Σ_{t∈病种} M[t][t] + M[无病][未落笔] + N_matched_comorbid
// ADR-019 §一(回放即数据记录)· ADR-006 §五(整数 (num, den) 对)
//
// ═══════════════════════════════════════════════════════════════════════════════
// ⚠️ **本程序集是 TD-epic-01 的还债件**(2026-10-19):普查实测 51 遥测在 `unity/Assets/` 下
//    零生产文件,而 10 个 story 全标 `Complete` —— AC 语义层由**测试自持谓词面**顶替
//    (测试文件自陈「测试自持谓词面,零生产依赖」)。本件把谓词面上移为生产类。
//
// ⚠️ **门槛:与 `Sim.Contracts` 同款 `noEngineReferences: true`**(ADR-019 ③ 51 住边界层、不进 sim)
//    —— 读事件流是纯 C# 计算,不碰 `UnityEngine`。51 是**只读消费者**(ADR-005 三流的读者),
//    不是第四个 `IEventSink` 写入者(ADR-019 §三 逐字)。
// ⚠️ 因此本程序集**不得**出现:网络调用 / `UnityEngine.*` / 写文件(导出归调用方,见上)。
// ═══════════════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Linq;

namespace DaYiJingCheng.Telemetry
{
    /// <summary>F2 误诊分布指标 —— 纯函数重算面(ADR-019 §一「回放即数据记录」的落地)。
    /// <para>语义 = GDD Formulas F2:混淆矩阵 M[t][j] + 三桶显式计数。</para>
    /// <para>**零埋点**:输入由调用方从既有事件流还原;本类不做任何 IO、不订阅、不写。</para></summary>
    /// <remarks>
    /// ⚠️ 上移台账(TD-epic-01):本类逐字搬自
    /// <c>Tests/EditMode/Telemetry/f2_misdiagnosis_matrix_test.cs</c> 的测试自持私有类
    /// <c>F2Formula</c> —— **上移 = 逐字搬移,零公式变更**(含常量、哨兵与桶顺序)。
    /// </remarks>
    public static class MisdiagnosisMatrixMetrics
    {
        /// <summary>一次判断记录(由调用方从**事件流**还原后喂入)。</summary>
        public sealed class CaseRecord
        {
            public int CaseId;
            public ISet<string> Truth;      // D(c)
            public ISet<string> Judged;     // J(c) = 全序最后一条
            public bool HasCaseClosed;      // ∃ CaseClosed
            public bool JudgmentMappable;   // judgment 携带病种枚举
        }

        /// <summary>计算混淆矩阵 + 三桶。
        /// 返回 (matrix, nComorbid, nOpen, nUnmappable, nMatchedComorbid)。
        /// matrix = Dictionary[(truth, judged), count];truth/judged ∈ 病种枚举 ∪ 哨兵。</summary>
        public static (Dictionary<(string, string), int> matrix, int nComorbid, int nOpen,
                       int nUnmappable, int nMatchedComorbid)
            Compute(IReadOnlyList<CaseRecord> records, ISet<string> diseaseEnum)
        {
            var matrix = new Dictionary<(string, string), int>();
            int nComorbid = 0, nOpen = 0, nUnmappable = 0, nMatchedComorbid = 0;

            foreach (var r in records)
            {
                // 三桶:不可归类
                if (!r.HasCaseClosed) { nOpen++; continue; }
                if (!r.JudgmentMappable) { nUnmappable++; continue; }

                bool comorbid = r.Truth.Count >= 2 || r.Judged.Count >= 2;
                if (comorbid)
                {
                    nComorbid++;
                    if (SetEquals(r.Judged, r.Truth)) nMatchedComorbid++;
                    continue;   // 共病不进矩阵
                }

                // Scorable_single:|D| ≤ 1 ∧ |J| ≤ 1
                string t = r.Truth.Count == 0 ? "无病" : r.Truth.First();
                string j = r.Judged.Count == 0 ? "未落笔" : r.Judged.First();
                if (!diseaseEnum.Contains(t) && t != "无病") { nUnmappable++; continue; }
                if (!diseaseEnum.Contains(j) && j != "未落笔") { nUnmappable++; continue; }

                var key = (t, j);
                matrix[key] = matrix.GetValueOrDefault(key) + 1;
            }
            return (matrix, nComorbid, nOpen, nUnmappable, nMatchedComorbid);
        }

        /// <summary>集合相等(含 ∅ == ∅ 判对)。</summary>
        public static bool SetEquals(ISet<string> a, ISet<string> b)
            => a.SetEquals(b);
    }
}
