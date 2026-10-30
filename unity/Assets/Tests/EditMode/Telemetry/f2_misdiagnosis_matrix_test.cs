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
using DaYiJingCheng.Telemetry;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    [TestFixture]
    internal sealed class F2MisdiagnosisMatrixTest
    {
        // ⚠️ S19 修复(TD-epic-01):原「测试自持谓词面」已删除 ——
        //    谓词面上移为生产类 `DaYiJingCheng.Telemetry.MisdiagnosisMatrixMetrics`
        //    (ADR-019 §一;那个自持面在普查里被判为「AC 语义层无生产主语」)。
        //    本文件现在**只做夹具 + 断言**,不再自带公式。

        // ══════════════ AC-51-B5:F2 与 F1 同源 + 差额记账 ══════════════

        /// <summary>AC-51-B5:F2 与 F1 同源 + 差额记账 ——
        /// F1a 分子 = Σ_{t∈病种} M[t][t] + M[无病][未落笔] + N_matched_comorbid;
        /// N_matched_comorbid ⊆ N_comorbid;「未落笔」列与「无病」行均非恒空。</summary>
        [Test]
        public void test_f2_f1aIdentity_threeTerms()
        {
            // Arrange:夹具含 D=∅∧J=∅、D={t}∧J=∅ 与共病判对各 ≥ 1 例
            var diseaseEnum = new HashSet<string> { "X", "Y" };
            var records = new List<MisdiagnosisMatrixMetrics.CaseRecord>
            {
                new MisdiagnosisMatrixMetrics.CaseRecord { CaseId = 1, Truth = new HashSet<string> { "X" }, Judged = new HashSet<string> { "X" }, HasCaseClosed = true, JudgmentMappable = true },  // M[X][X]
                new MisdiagnosisMatrixMetrics.CaseRecord { CaseId = 2, Truth = new HashSet<string>(), Judged = new HashSet<string>(), HasCaseClosed = true, JudgmentMappable = true },               // M[无病][未落笔]
                new MisdiagnosisMatrixMetrics.CaseRecord { CaseId = 3, Truth = new HashSet<string> { "X", "Y" }, Judged = new HashSet<string> { "X", "Y" }, HasCaseClosed = true, JudgmentMappable = true }, // 共病判对
                new MisdiagnosisMatrixMetrics.CaseRecord { CaseId = 4, Truth = new HashSet<string> { "X" }, Judged = new HashSet<string> { "Y" }, HasCaseClosed = true, JudgmentMappable = true },  // M[X][Y] 判错
                new MisdiagnosisMatrixMetrics.CaseRecord { CaseId = 5, Truth = new HashSet<string> { "X" }, Judged = new HashSet<string>(), HasCaseClosed = true, JudgmentMappable = true },     // M[X][未落笔] 单病种未落笔
                new MisdiagnosisMatrixMetrics.CaseRecord { CaseId = 6, Truth = new HashSet<string>(), Judged = new HashSet<string> { "X" }, HasCaseClosed = true, JudgmentMappable = true },     // M[无病][X] 误开方
            };

            // Act
            var (matrix, nComorbid, _, _, nMatchedComorbid) = MisdiagnosisMatrixMetrics.Compute(records, diseaseEnum);

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
            var records = new List<MisdiagnosisMatrixMetrics.CaseRecord>
            {
                new MisdiagnosisMatrixMetrics.CaseRecord { CaseId = 1, Truth = new HashSet<string> { "X", "Y" }, Judged = new HashSet<string> { "X" }, HasCaseClosed = true, JudgmentMappable = true },   // ① 共病(|D|≥2)
                new MisdiagnosisMatrixMetrics.CaseRecord { CaseId = 2, Truth = new HashSet<string> { "X" }, Judged = new HashSet<string> { "X" }, HasCaseClosed = false, JudgmentMappable = true },    // ② 未结案
                new MisdiagnosisMatrixMetrics.CaseRecord { CaseId = 3, Truth = new HashSet<string> { "X" }, Judged = new HashSet<string> { "Unknown" }, HasCaseClosed = true, JudgmentMappable = false }, // ③ 不可映射
                new MisdiagnosisMatrixMetrics.CaseRecord { CaseId = 4, Truth = new HashSet<string> { "X" }, Judged = new HashSet<string> { "X" }, HasCaseClosed = true, JudgmentMappable = true },     // 正常判对
                new MisdiagnosisMatrixMetrics.CaseRecord { CaseId = 5, Truth = new HashSet<string> { "X" }, Judged = new HashSet<string> { "X", "Y" }, HasCaseClosed = true, JudgmentMappable = true }, // ①' 共病(|J|≥2)
            };

            // Act
            var (matrix, nComorbid, nOpen, nUnmappable, _) = MisdiagnosisMatrixMetrics.Compute(records, diseaseEnum);

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
            // 🔴 **2026-10-30 修(`TD-epic-01-e` 复核轮)**:本条判据此前有**两处**假绿,均已修。
            //
            // 缺口 ① **扫描面漏了 51 的主实装** ——
            //   原判据只扫 `Namespace.StartsWith("DaYiJingCheng.Telemetry")`,
            //   而 51 的**生产实装** `TelemetryReporter` 住在
            //   **`DaYiJingCheng.Gameplay.Boot`**(`TelemetryReporter.cs:27`),
            //   全部解码 / 分派 / 报告逻辑都在那里。
            //   ⇒ 判据扫的是**纯函数层**,而**可能真有旁路采集的地方没被扫**。
            //   实测确认:`grep -oE "...Parse...|...Map...|...Resolve...|...Convert...("
            //   unity/Assets/Gameplay.Boot/TelemetryReporter.cs` ⇒ 今日零命中,
            //   但那是**巧合**,不是判据保证的 —— 明天有人加一个 `ResolveDiseaseName`
            //   到 reporter 里,本条**仍然绿**。
            //
            // 缺口 ② **无「扫描面非空」守卫** ——
            //   原注释写「实现后 telemetryTypes 非空」,但**没有任何断言**保证它。
            //   若命名空间被重命名 / 程序集未加载 ⇒ `telemetryTypes` 为空
            //   ⇒ 循环空转 ⇒ `violations` 为空 ⇒ **判据恒绿**(本项目抓过多次的「空扫描」形态)。
            //
            // ✅ **证伪轮(2026-10-30,两条变异各转红,还原 SHA-256 逐字节一致)**:
            //   · M11 —— 在 `TelemetryReporter`(主实装)里加
            //     `private static string ResolveDiseaseName(int id)`:
            //     **红**。这条打的是缺口 ① 的修法(具名类型必须被扫)。
            //     变异前该判据对主实装**零判别力**(它根本不在扫描面内)。
            //   · M12 —— 在 `DaYiJingCheng.Telemetry` 命名空间里加
            //     `internal static class DiseaseNameMapper { … MapDiseaseName(…) }`:
            //     **红**。这条打的是**原判据就有的**命名空间扫描那半 ——
            //     此前从未被证伪过,今日补证。
            //   · 两轮均 `total=3 failed=1`,且**红的就是本条**(另两条恒绿 ⇒
            //     判别力精确落在本判据上,不是连带污染)。
            //   ⚠️ 未反证项:**缺口 ② 的空扫描守卫本身**没有专打变异 ——
            //     要打它须改命名空间字符串或摘掉 `telemetryOwnerTypes` 条目,
            //     而这两者都在**夹具**里(改夹具 = 改判据,不是打判据)。
            //     ⇒ 该守卫的**存在性**可读,其**判别力**如实登记为「无专打变异」。

            // Arrange:扫描面 = 51 的**全部生产类型**,分两块:
            //   ⓐ 纯函数层 = 命名空间 `DaYiJingCheng.Telemetry` 下的**全部类型**
            //      (Metrics / Reducers / Report / StreamIntegrity …);
            //   ⓑ **主实装** = `Gameplay.Boot` 里的 **51 专属类**,**逐个具名**。
            //
            //   ⚠️ **为什么 ⓑ 不能写成「整个 `Gameplay.Boot` 命名空间」**(2026-10-30 实测):
            //      那样写会扫到 Boot 里**与 51 无关**的启动配置解析 —— 首轮实跑即红 11 条:
            //      `BootRoot.ResolveTelemetryKnobs` / `ResolveDeviceSwitchKnobs` /
            //      `CompositionRoot.ResolveGlobalConfigVersion` / `AudioAssemblySeams.TryResolveCue` …
            //      它们全是「解析配置旋钮」,**不是**「解析病名」,与 AC-51-B7 无关。
            //      ⇒ 判据面须精确到**51 的实装类**,而不是它恰好所在的命名空间。
            //   ⚠️ 具名的代价:新增 51 实装类时**必须**加进 `telemetryOwnerTypes`,
            //      否则那个类里的旁路采集抓不到。该代价由下方「必须命中」守卫部分缓解
            //      (漏加 = 少一个必须命中的条目,但不会静默通过 —— 因为命中的那几个仍被检查)。
            string[] productionNamespaces = { "DaYiJingCheng.Telemetry" };
            string[] telemetryOwnerTypes =
            {
                "DaYiJingCheng.Gameplay.Boot.TelemetryReporter",      // 51 的主实装
                "DaYiJingCheng.Gameplay.Boot.TelemetryDeveloperView", // 51 的调试视图
            };

            var allTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .ToList();

            var telemetryTypes = allTypes
                .Where(t => t.Namespace != null
                            && productionNamespaces.Any(ns =>
                                   t.Namespace == ns || t.Namespace.StartsWith(ns + ".")))
                .ToList();

            // ⚠️ **空扫描守卫(缺口 ② 的修法)**:扫描面**必须**非空。
            Assert.That(telemetryTypes, Is.Not.Empty,
                "扫描面为空 —— 本判据会空转通过(假绿)。检查命名空间是否被重命名 / 程序集是否加载");

            // ⚠️ **主实装必须真的被扫到(缺口 ① 的修法 + 判别力守卫)**:
            //    逐个具名类型都必须**在扫描面里**。缺一个 ⇒ 红(而不是「它不在就不查它」)。
            foreach (string fqn in telemetryOwnerTypes)
            {
                Type t = allTypes.FirstOrDefault(x => x.FullName == fqn);
                Assert.That(t, Is.Not.Null,
                    $"找不到 51 的实装类 `{fqn}` —— 类被改名 / 移走 ⇒ 本判据的扫描面失真。" +
                    "须同批更新 `telemetryOwnerTypes`(不得留着一条永不命中的路径)");
                telemetryTypes.Add(t);
            }

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
                        violations.Add($"{t.Namespace}.{t.Name}.{m.Name} 是解析/映射方法");
                }

            // Assert:零解析/映射方法
            Assert.That(violations, Is.Empty,
                "51 不得有病名解析/映射代码路径(AC-51-B7:N_unmappable > 0 不得触发旁路采集):\n" +
                string.Join("\n", violations));
        }
    }
}
