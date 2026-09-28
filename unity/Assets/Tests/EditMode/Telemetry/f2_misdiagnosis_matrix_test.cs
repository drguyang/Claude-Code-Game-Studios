// 权威来源:production/epics/telemetry-analytics/story-003-f2-misdiagnosis-matrix.md
//   (AC-51-B5…B7 — F2 误诊分布:混淆矩阵 M[t][j] + 三桶显式计数 + F1a 恒等式)
//   · M[t][j] = |{c ∈ Scorable_single : truth(c)=t ∧ judged(c)=j}|;形状 = (|病种|+1)²
//   · Scorable_single = {c ∈ Scorable : |D(c)| ≤ 1 ∧ |J(c)| ≤ 1}(对称哨兵)
//   · 三桶:N_comorbid(|D|≥2∨|J|≥2)· N_open(无 CaseClosed)· N_unmappable(未携带病种枚举)
//   · F1a 恒等式 = Σ_{t∈病种} M[t][t] + M[无病][未落笔] + N_matched_comorbid
// ADR-019 §一(回放即数据记录)· ADR-006 §五(整数 (num, den) 对)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/unit/telemetry/f2_misdiagnosis_matrix_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**;账本侧由 tests/integration/telemetry/README.md 互链。
// ⚠️ 负向夹具落位:与 Story 001/002 同型 —— 违例 fixture 住**测试命名空间**。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)·
//    夹具读取前置 File.Exists 断言(缺失即红,不静默跳过)。

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    [TestFixture]
    internal sealed class F2MisdiagnosisMatrixTest
    {
        // ══════════════ F2 公式(测试自持谓词面,零生产依赖)══════════════

        /// <summary>F2 公式谓词面(测试自持,纯函数)。
        /// 语义 = GDD Formulas F2:混淆矩阵 M[t][j] + 三桶显式计数。</summary>
        private static class F2Formula
        {
            /// <summary>一次判断记录(测试自持夹具)。</summary>
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
            /// matrix = Dictionary<(truth, judged), count>;truth/judged ∈ 病种枚举 ∪ 哨兵。</summary>
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

        // ══════════════ AC-51-B5:F2 与 F1 同源 + 差额记账 ══════════════

        /// <summary>AC-51-B5:F2 与 F1 同源 + 差额记账 ——
        /// F1a 分子 = Σ_{t∈病种} M[t][t] + M[无病][未落笔] + N_matched_comorbid;
        /// N_matched_comorbid ⊆ N_comorbid;「未落笔」列与「无病」行均非恒空。</summary>
        [Test]
        public void test_f2_f1aIdentity_threeTerms()
        {
            // Arrange:夹具含 D=∅∧J=∅、D={t}∧J=∅ 与共病判对各 ≥ 1 例
            var diseaseEnum = new HashSet<string> { "X", "Y" };
            var records = new List<F2Formula.CaseRecord>
            {
                new F2Formula.CaseRecord { CaseId = 1, Truth = new HashSet<string> { "X" }, Judged = new HashSet<string> { "X" }, HasCaseClosed = true, JudgmentMappable = true },  // M[X][X]
                new F2Formula.CaseRecord { CaseId = 2, Truth = new HashSet<string>(), Judged = new HashSet<string>(), HasCaseClosed = true, JudgmentMappable = true },               // M[无病][未落笔]
                new F2Formula.CaseRecord { CaseId = 3, Truth = new HashSet<string> { "X", "Y" }, Judged = new HashSet<string> { "X", "Y" }, HasCaseClosed = true, JudgmentMappable = true }, // 共病判对
                new F2Formula.CaseRecord { CaseId = 4, Truth = new HashSet<string> { "X" }, Judged = new HashSet<string> { "Y" }, HasCaseClosed = true, JudgmentMappable = true },  // M[X][Y] 判错
                new F2Formula.CaseRecord { CaseId = 5, Truth = new HashSet<string> { "X" }, Judged = new HashSet<string>(), HasCaseClosed = true, JudgmentMappable = true },     // M[X][未落笔] 单病种未落笔
                new F2Formula.CaseRecord { CaseId = 6, Truth = new HashSet<string>(), Judged = new HashSet<string> { "X" }, HasCaseClosed = true, JudgmentMappable = true },     // M[无病][X] 误开方
            };

            // Act
            var (matrix, nComorbid, _, _, nMatchedComorbid) = F2Formula.Compute(records, diseaseEnum);

            // Assert:F1a 恒等式三项
            int diagonal = matrix.Where(kv => kv.Key.Item1 == kv.Key.Item2 && kv.Key.Item1 != "无病")
                                 .Sum(kv => kv.Value);
            int noDiseaseNoJudgment = matrix.GetValueOrDefault(("无病", "未落笔"));
            int f1aNumerator = diagonal + noDiseaseNoJudgment + nMatchedComorbid;

            Assert.That(diagonal, Is.EqualTo(1), "Σ_{t∈病种} M[t][t] = 1(仅 M[X][X])");
            Assert.That(noDiseaseNoJudgment, Is.EqualTo(1), "M[无病][未落笔] = 1(∅==∅ 判对)");
            Assert.That(nMatchedComorbid, Is.EqualTo(1), "N_matched_comorbid = 1(共病判对)");
            Assert.That(f1aNumerator, Is.EqualTo(3), "F1a 分子 = 1 + 1 + 1 = 3");

            // Assert:N_matched_comorbid ⊆ N_comorbid
            Assert.That(nMatchedComorbid, Is.LessThanOrEqualTo(nComorbid),
                "N_matched_comorbid ⊆ N_comorbid");

            // Assert:「未落笔」列与「无病」行均非恒空
            int noJudgmentCol = matrix.Where(kv => kv.Key.Item2 == "未落笔").Sum(kv => kv.Value);
            int noDiseaseRow = matrix.Where(kv => kv.Key.Item1 == "无病").Sum(kv => kv.Value);
            Assert.That(noJudgmentCol, Is.EqualTo(2), "「未落笔」列非恒空(M[无病][未落笔] + M[X][未落笔] = 2)");
            Assert.That(noDiseaseRow, Is.EqualTo(2), "「无病」行非恒空(M[无病][未落笔] + M[无病][X] = 2)");
            // 显式断言 D={t} ∧ J=∅ 入「未落笔」列
            Assert.That(matrix.GetValueOrDefault(("X", "未落笔")), Is.EqualTo(1),
                "D={X} ∧ J=∅ 入 M[X][未落笔] 列(AC-51-B5 明文要求)");
        }

        // ══════════════ AC-51-B6:F2 三桶显式计数 ══════════════

        /// <summary>AC-51-B6:F2 三桶显式计数 —— ① |D| ≥ 2(共病) ② 无 CaseClosed(未结案)
        /// ③ judgment 未携带病种枚举(不可映射);三桶各等于该类例数;不出现在 M 任何格。</summary>
        [Test]
        public void test_f2_threeBucketsExplicitCount()
        {
            // Arrange:夹具含三类不可归类例(含 |D|≥2 与 |J|≥2 两条共病路径)
            var diseaseEnum = new HashSet<string> { "X", "Y" };
            var records = new List<F2Formula.CaseRecord>
            {
                new F2Formula.CaseRecord { CaseId = 1, Truth = new HashSet<string> { "X", "Y" }, Judged = new HashSet<string> { "X" }, HasCaseClosed = true, JudgmentMappable = true },   // ① 共病(|D|≥2)
                new F2Formula.CaseRecord { CaseId = 2, Truth = new HashSet<string> { "X" }, Judged = new HashSet<string> { "X" }, HasCaseClosed = false, JudgmentMappable = true },    // ② 未结案
                new F2Formula.CaseRecord { CaseId = 3, Truth = new HashSet<string> { "X" }, Judged = new HashSet<string> { "Unknown" }, HasCaseClosed = true, JudgmentMappable = false }, // ③ 不可映射
                new F2Formula.CaseRecord { CaseId = 4, Truth = new HashSet<string> { "X" }, Judged = new HashSet<string> { "X" }, HasCaseClosed = true, JudgmentMappable = true },     // 正常判对
                new F2Formula.CaseRecord { CaseId = 5, Truth = new HashSet<string> { "X" }, Judged = new HashSet<string> { "X", "Y" }, HasCaseClosed = true, JudgmentMappable = true }, // ①' 共病(|J|≥2)
            };

            // Act
            var (matrix, nComorbid, nOpen, nUnmappable, _) = F2Formula.Compute(records, diseaseEnum);

            // Assert:三桶各等于该类例数(含 |D|≥2 与 |J|≥2 两条共病路径)
            Assert.That(nComorbid, Is.EqualTo(2), "N_comorbid = 2(案例 1 |D|≥2 + 案例 5 |J|≥2)");
            Assert.That(nOpen, Is.EqualTo(1), "N_open = 1(案例 2)");
            Assert.That(nUnmappable, Is.EqualTo(1), "N_unmappable = 1(案例 3)");

            // Assert:三桶例不出现在 M 任何格
            Assert.That(matrix.Count, Is.EqualTo(1), "仅案例 4 入矩阵(M[X][X] = 1)");
            Assert.That(matrix.GetValueOrDefault(("X", "X")), Is.EqualTo(1));
        }

        // ══════════════ AC-51-B7:N_unmappable > 0 不得触发旁路采集 ══════════════

        /// <summary>AC-51-B7:N_unmappable > 0 不得触发旁路采集 ——
        /// 51 侧不存在病名字符串→枚举的解析/映射代码路径。</summary>
        [Test]
        public void test_f2_unmappableNoBypass()
        {
            // Arrange:扫描 51 命名空间下的类型
            var telemetryTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("DaYiJingCheng.Telemetry"))
                .ToList();

            // Act:检查是否存在病名解析/映射方法
            var violations = new List<string>();
            foreach (var t in telemetryTypes)
                foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Public |
                                              System.Reflection.BindingFlags.NonPublic |
                                              System.Reflection.BindingFlags.Instance |
                                              System.Reflection.BindingFlags.Static |
                                              System.Reflection.BindingFlags.DeclaredOnly))
                {
                    if (m.Name.Contains("Parse") || m.Name.Contains("Map") ||
                        m.Name.Contains("Resolve") || m.Name.Contains("Convert"))
                        violations.Add($"{t.Name}.{m.Name} 是解析/映射方法");
                }

            // Assert:零解析/映射方法(实现后 telemetryTypes 非空;唯一判据 = violations 为空)
            Assert.That(violations, Is.Empty,
                "51 不得有病名解析/映射代码路径(AC-51-B7:N_unmappable > 0 不得触发旁路采集):\n" +
                string.Join("\n", violations));
        }
    }
}
