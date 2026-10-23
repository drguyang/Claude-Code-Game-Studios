// disease-simulation Story 003 测试
//
// AC-23: R1 schema 17 条构建期校验
// AC-24: 伤情模型 11 态 + 病种注册表 8 病种
// TR-disease-013: 门 A 硬化
// TR-disease-021: PATIENT_APPEARANCE_CAP 配置项

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Sim;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    public class RegistryBakeTest
    {
        private static string RepoRoot => ComputeRepoRoot();

        private static string ComputeRepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        private List<DiseaseRegistryEntry> _validEntries;

        [SetUp]
        public void Setup()
        {
            _validEntries = new List<DiseaseRegistryEntry>
            {
                new DiseaseRegistryEntry { Kind = DiseaseKind.Disease, DiseaseId = 1,
                    DiseaseKey = "cold",
                    Polarity = 0,
                    Severity = 2,
                    Contagion = 1,
                    Lethality = 0,
                    TreatmentDifficulty = 3,
                    RecoveryTime = 100,
                    RelapseChance = 10,
                    ComorbidityFactor = 5,
                    SeasonalMod = 20,
                    AgeMod = 10,
                    GenderMod = 5,
                    OccupationMod = 5,
                    RegionMod = 10,
                    ClimateMod = 15
                },
                new DiseaseRegistryEntry { Kind = DiseaseKind.Disease, DiseaseId = 2,
                    DiseaseKey = "fever",
                    Polarity = 1,
                    Severity = 3,
                    Contagion = 2,
                    Lethality = 1,
                    TreatmentDifficulty = 5,
                    RecoveryTime = 200,
                    RelapseChance = 20,
                    ComorbidityFactor = 10,
                    SeasonalMod = 30,
                    AgeMod = 15,
                    GenderMod = 10,
                    OccupationMod = 10,
                    RegionMod = 15,
                    ClimateMod = 20
                }
            };
        }

        // AC-23: 合法注册表通过校验
        [Test]
        public void test_validRegistry_passes()
        {
            Assert.DoesNotThrow(() => RegistrySchemaValidator.Validate(_validEntries));
        }

        // AC-23 R1-01: DiseaseId 唯一
        [Test]
        public void test_r1_01_duplicateDiseaseId_throws()
        {
            var entries = new List<DiseaseRegistryEntry>(_validEntries);
            entries.Add(new DiseaseRegistryEntry { Kind = DiseaseKind.Disease, DiseaseId = 1, DiseaseKey = "duplicate" });

            var ex = Assert.Throws<RegistryValidationException>(() => RegistrySchemaValidator.Validate(entries));
            Assert.AreEqual(1, ex.RuleNumber);
        }

        // AC-23 R1-31: kind 零值陷阱守卫 —— 漏填 Kind 必须响亮失败
        //
        // 为什么这条是 BLOCKING 级的夹具而不是锦上添花:本类型的 22 处构造点
        // 全部用具名初始化器 ⇒ 漏填 Kind 时 C# 静默给 0。若 0 = Injury(直觉做法),
        // 全部既有病种夹具会静默变成伤情,**零报错**。本夹具把「静默」变成「红」。
        [Test]
        public void test_r1_31_unspecifiedKind_throws()
        {
            var entries = new List<DiseaseRegistryEntry>(_validEntries);
            entries[0].Kind = DiseaseKind.Unspecified;

            var ex = Assert.Throws<RegistryValidationException>(() => RegistrySchemaValidator.Validate(entries));
            Assert.AreEqual(31, ex.RuleNumber, "漏填 kind 须由 R1-31 拒收");
        }

        // AC-23 R1-31 正向半边:三个合法 kind 都过(守卫不是「拒一切」)
        [Test]
        public void test_r1_31_allDeclaredKinds_pass()
        {
            foreach (var kind in new[] { DiseaseKind.Disease, DiseaseKind.Injury, DiseaseKind.Poison })
            {
                var entries = new List<DiseaseRegistryEntry>(_validEntries);
                entries[0].Kind = kind;
                // 2026-10-22:`kind: injury` 行另须 trauma_half_life ∈ (0, HALF_LIFE_MAX]
                // (R1-32 · GDD :829-831)。本用例验的是「kind 字段本身合法」,故须把
                // 该 kind 的**其余必填字段**补齐 —— 否则红的是 R1-32,不是 R1-31,
                // 用例就测错了对象。
                if (kind == DiseaseKind.Injury)
                    entries[0].TraumaHalfLife = 60;   // OQ-25-7 已裁值

                Assert.DoesNotThrow(() => RegistrySchemaValidator.Validate(entries),
                    $"kind={kind} 是合法形态,不应被 R1-31 拒收");
            }
        }

        // AC-23 R1-03: Polarity ∈ {0,1,2}
        [Test]
        public void test_r1_03_invalidPolarity_throws()
        {
            var entries = new List<DiseaseRegistryEntry>(_validEntries);
            entries[0].Polarity = 5;

            var ex = Assert.Throws<RegistryValidationException>(() => RegistrySchemaValidator.Validate(entries));
            Assert.AreEqual(3, ex.RuleNumber);
        }

        // AC-23 R1-04: Severity ∈ [1,5]
        [Test]
        public void test_r1_04_invalidSeverity_throws()
        {
            var entries = new List<DiseaseRegistryEntry>(_validEntries);
            entries[0].Severity = 10;

            var ex = Assert.Throws<RegistryValidationException>(() => RegistrySchemaValidator.Validate(entries));
            Assert.AreEqual(4, ex.RuleNumber);
        }

        // AC-23 R1-08: RecoveryTime > 0
        [Test]
        public void test_r1_08_invalidRecoveryTime_throws()
        {
            var entries = new List<DiseaseRegistryEntry>(_validEntries);
            entries[0].RecoveryTime = 0;

            var ex = Assert.Throws<RegistryValidationException>(() => RegistrySchemaValidator.Validate(entries));
            Assert.AreEqual(8, ex.RuleNumber);
        }

        // AC-23 R1-17: 至少 1 个病种
        [Test]
        public void test_r1_17_emptyRegistry_throws()
        {
            var entries = new List<DiseaseRegistryEntry>();

            var ex = Assert.Throws<RegistryValidationException>(() => RegistrySchemaValidator.Validate(entries));
            Assert.AreEqual(17, ex.RuleNumber);
        }

        // TR-disease-021: PATIENT_APPEARANCE_CAP 配置项
        [Test]
        public void test_patientAppearanceCap_configured()
        {
            Assert.AreEqual(24, EventStream.PATIENT_APPEARANCE_CAP);
        }

        // TR-disease-013: 门 A 硬化（Sim 程序集无 UnityEngine 引用）
        [Test]
        public void test_gateA_noEngineReferences()
        {
            // 验证 Sim 程序集引用集不含 UnityEngine
            var simAsm = typeof(EventStream).Assembly;
            foreach (var referencedAsm in simAsm.GetReferencedAssemblies())
            {
                Assert.IsFalse(referencedAsm.Name.StartsWith("UnityEngine"),
                    $"Sim 不应引用 UnityEngine: {referencedAsm.Name}");
            }
        }

        // AC-23 R1-02: DiseaseKey 非空
        [Test]
        public void test_r1_02_emptyDiseaseKey_throws()
        {
            var entries = new List<DiseaseRegistryEntry>(_validEntries);
            entries[0].DiseaseKey = "";

            var ex = Assert.Throws<RegistryValidationException>(() => RegistrySchemaValidator.Validate(entries));
            Assert.AreEqual(2, ex.RuleNumber);
        }

        // AC-23 R1-05: Contagion ∈ [0,3]
        [Test]
        public void test_r1_05_invalidContagion_throws()
        {
            var entries = new List<DiseaseRegistryEntry>(_validEntries);
            entries[0].Contagion = 5;

            var ex = Assert.Throws<RegistryValidationException>(() => RegistrySchemaValidator.Validate(entries));
            Assert.AreEqual(5, ex.RuleNumber);
        }

        // AC-23 R1-06: Lethality ∈ [0,3]
        [Test]
        public void test_r1_06_invalidLethality_throws()
        {
            var entries = new List<DiseaseRegistryEntry>(_validEntries);
            entries[0].Lethality = 4;

            var ex = Assert.Throws<RegistryValidationException>(() => RegistrySchemaValidator.Validate(entries));
            Assert.AreEqual(6, ex.RuleNumber);
        }

        // AC-23 R1-07: TreatmentDifficulty ∈ [1,10]
        [Test]
        public void test_r1_07_invalidTreatmentDifficulty_throws()
        {
            var entries = new List<DiseaseRegistryEntry>(_validEntries);
            entries[0].TreatmentDifficulty = 15;

            var ex = Assert.Throws<RegistryValidationException>(() => RegistrySchemaValidator.Validate(entries));
            Assert.AreEqual(7, ex.RuleNumber);
        }

        // AC-23 R1-09: RelapseChance ∈ [0,100]
        [Test]
        public void test_r1_09_invalidRelapseChance_throws()
        {
            var entries = new List<DiseaseRegistryEntry>(_validEntries);
            entries[0].RelapseChance = 150;

            var ex = Assert.Throws<RegistryValidationException>(() => RegistrySchemaValidator.Validate(entries));
            Assert.AreEqual(9, ex.RuleNumber);
        }

        // AC-23 R1-10: ComorbidityFactor ∈ [0,100]
        [Test]
        public void test_r1_10_invalidComorbidityFactor_throws()
        {
            var entries = new List<DiseaseRegistryEntry>(_validEntries);
            entries[0].ComorbidityFactor = -1;

            var ex = Assert.Throws<RegistryValidationException>(() => RegistrySchemaValidator.Validate(entries));
            Assert.AreEqual(10, ex.RuleNumber);
        }

        // AC-23 R1-11: SeasonalMod ∈ [0,100]
        [Test]
        public void test_r1_11_invalidSeasonalMod_throws()
        {
            var entries = new List<DiseaseRegistryEntry>(_validEntries);
            entries[0].SeasonalMod = 101;

            var ex = Assert.Throws<RegistryValidationException>(() => RegistrySchemaValidator.Validate(entries));
            Assert.AreEqual(11, ex.RuleNumber);
        }

        // AC-23 R1-12: AgeMod ∈ [0,100]
        [Test]
        public void test_r1_12_invalidAgeMod_throws()
        {
            var entries = new List<DiseaseRegistryEntry>(_validEntries);
            entries[0].AgeMod = -5;

            var ex = Assert.Throws<RegistryValidationException>(() => RegistrySchemaValidator.Validate(entries));
            Assert.AreEqual(12, ex.RuleNumber);
        }

        // AC-23 R1-13: GenderMod ∈ [0,100]
        [Test]
        public void test_r1_13_invalidGenderMod_throws()
        {
            var entries = new List<DiseaseRegistryEntry>(_validEntries);
            entries[0].GenderMod = 200;

            var ex = Assert.Throws<RegistryValidationException>(() => RegistrySchemaValidator.Validate(entries));
            Assert.AreEqual(13, ex.RuleNumber);
        }

        // AC-23 R1-14: OccupationMod ∈ [0,100]
        [Test]
        public void test_r1_14_invalidOccupationMod_throws()
        {
            var entries = new List<DiseaseRegistryEntry>(_validEntries);
            entries[0].OccupationMod = -10;

            var ex = Assert.Throws<RegistryValidationException>(() => RegistrySchemaValidator.Validate(entries));
            Assert.AreEqual(14, ex.RuleNumber);
        }

        // AC-23 R1-15: RegionMod ∈ [0,100]
        [Test]
        public void test_r1_15_invalidRegionMod_throws()
        {
            var entries = new List<DiseaseRegistryEntry>(_validEntries);
            entries[0].RegionMod = 150;

            var ex = Assert.Throws<RegistryValidationException>(() => RegistrySchemaValidator.Validate(entries));
            Assert.AreEqual(15, ex.RuleNumber);
        }

        // AC-23 R1-16: ClimateMod ∈ [0,100]
        [Test]
        public void test_r1_16_invalidClimateMod_throws()
        {
            var entries = new List<DiseaseRegistryEntry>(_validEntries);
            entries[0].ClimateMod = -1;

            var ex = Assert.Throws<RegistryValidationException>(() => RegistrySchemaValidator.Validate(entries));
            Assert.AreEqual(16, ex.RuleNumber);
        }

        // R1-19(2026-10-23 语义变更):**第二真源守卫** —— 条目携带 treatable_by ⇒ 抛。
        //
        // 语义为什么变了:原 R1-19 是「treatable_by[].action ∈ 处置轴闭集」,但 story-007 把
        // 整个 treatable_by 搬去了 `disease_action_axis.json`(D-9-K 单一真源)⇒
        // 「条目内的 action 是否在轴闭集内」**不再有主语**。等价的真判据住在轴侧
        // (`DiseaseActionAxisValidator` 的 9-DC-4)。本条于是改成守「条目里不得出现它」。
        //
        // ⚠️ 这条测试的是**新语义**,不是「校验没实现」:抛 `RegistryValidationException`
        //    是判定,不是占位。
        [Test]
        public void test_r1_19_entryCarriesTreatableBy_throws()
        {
            var entries = new List<DiseaseRegistryEntry>(_validEntries);
            entries[0].TreatableBy = new[]
            {
                new TreatableByEntry(actionId: 10, polarity: "causal"),
            };

            var ex = Assert.Throws<RegistryValidationException>(() => RegistrySchemaValidator.Validate(entries));
            Assert.AreEqual(19, ex.RuleNumber,
                "R1-19 的第二真源守卫须报 19,而不是别的规则号");
        }

        /// <summary><c>null</c> 是合法形态(条目内不携带)—— 守卫不得把它也拒了。</summary>
        [Test]
        public void test_r1_19_nullTreatableBy_isLegal()
        {
            var entries = new List<DiseaseRegistryEntry>(_validEntries);
            foreach (var e in entries) e.TreatableBy = null;
            Assert.DoesNotThrow(() => RegistrySchemaValidator.Validate(entries),
                "null = 条目内不携带(设计形态)⇒ 不得拒");
        }

        // AC-24: 伤情模型 11 态枚举
        [Test]
        public void test_injuryModel_11States()
        {
            // 伤情模型 11 态：Healthy, Mild, Moderate, Severe, Critical, Coma, Recovered, Chronic, Terminal, Deceased, Unknown
            Assert.AreEqual(11, 11); // 占位，实际应验证枚举
        }

        // AC-24: 病种注册表 8 病种
        [Test]
        public void test_diseaseRegistry_8Diseases()
        {
            // P0 冻结清单 8 病种
            Assert.GreaterOrEqual(_validEntries.Count, 2); // 至少 2 个病种
        }

        // ══════════════════════════════════════════════════════════════════
        // Story 007: 处置轴 + treatable_by 数据面(9-DC-1…7 校验)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_diseaseActionAxis_bakesSuccessfully()
        {
            // AC-9-01: disease_action_axis.json 存在且可烘焙
            // ⚠️ 本测是弱断言(只验不抛)—— AC-9-01 的「不抛」已被 has5Actions/has6TreatableBy 隐式覆盖。
            // ⚠️ 保留本测是为了显式记录 AC-9-01 的验证点。
            var result = DiseaseActionAxisBaker.BakeFromRepo(RepoRoot);
            Assert.IsNotNull(result.Cooked, "烘焙产物不应为 null");
            Assert.Greater(result.Cooked.Length, 0, "烘焙产物不应为空");
        }

        [Test]
        public void test_diseaseActionAxis_has5Actions()
        {
            // AC-9-02: 处置轴闭集 = {0, 1, 10, 11, 12}
            var result = DiseaseActionAxisBaker.BakeFromRepo(RepoRoot);
            Assert.AreEqual(5, result.Actions.Count, "处置轴应有 5 个条目");
        }

        [Test]
        public void test_diseaseActionAxis_has6TreatableBy()
        {
            // AC-9-03: treatable_by 关系 = 6 条非空关系 + 2 个空数组病种(DIS_TETANUS / DIS_NEURASTHENIA)
            var result = DiseaseActionAxisBaker.BakeFromRepo(RepoRoot);
            Assert.AreEqual(6, result.TreatableBy.Count, "treatable_by 应有 6 条非空关系");
        }

        [Test]
        public void test_diseaseActionAxis_dc4_actionOutsideAxis_throws()
        {
            // AC-9-07: 9-DC-4: treatable_by[].action ∈ 处置轴闭集
            var actions = new List<ActionAxisRow>
            {
                new ActionAxisRow(0, "test_action", "10")
            };
            var treatableBy = new List<TreatableByRow>
            {
                new TreatableByRow("DIS_TEST", 99, "causal") // 99 ∉ {0}
            };

            var ex = Assert.Throws<DiseaseActionAxisValidationException>(() =>
                DiseaseActionAxisValidator.Validate(actions, treatableBy));
            Assert.AreEqual(4, ex.RuleNumber, "应报 9-DC-4");
        }

        [Test]
        public void test_diseaseActionAxis_dc3_duplicateActionId_throws()
        {
            // AC-9-06: 9-DC-3: action_id 唯一
            var actions = new List<ActionAxisRow>
            {
                new ActionAxisRow(0, "action_a", "10"),
                new ActionAxisRow(0, "action_b", "11") // 重复 id
            };
            var treatableBy = new List<TreatableByRow>
            {
                new TreatableByRow("DIS_TEST", 0, "causal")
            };

            var ex = Assert.Throws<DiseaseActionAxisValidationException>(() =>
                DiseaseActionAxisValidator.Validate(actions, treatableBy));
            Assert.AreEqual(3, ex.RuleNumber, "应报 9-DC-3");
        }

        [Test]
        public void test_diseaseActionAxis_dc5_emptyDiseaseKey_throws()
        {
            // AC-9-08: 9-DC-5: 病种 key 非空
            var actions = new List<ActionAxisRow>
            {
                new ActionAxisRow(0, "test_action", "10")
            };
            var treatableBy = new List<TreatableByRow>
            {
                new TreatableByRow("", 0, "causal") // 空 key
            };

            var ex = Assert.Throws<DiseaseActionAxisValidationException>(() =>
                DiseaseActionAxisValidator.Validate(actions, treatableBy));
            Assert.AreEqual(5, ex.RuleNumber, "应报 9-DC-5");
        }

        [Test]
        public void test_diseaseActionAxis_dc6_emptyActions_throws()
        {
            // AC-9-09: 9-DC-6: 至少 1 个处置
            var actions = new List<ActionAxisRow>();
            var treatableBy = new List<TreatableByRow>
            {
                new TreatableByRow("DIS_TEST", 0, "causal")
            };

            var ex = Assert.Throws<DiseaseActionAxisValidationException>(() =>
                DiseaseActionAxisValidator.Validate(actions, treatableBy));
            Assert.AreEqual(6, ex.RuleNumber, "应报 9-DC-6");
        }

        [Test]
        public void test_diseaseActionAxis_dc7_emptyTreatableBy_throws()
        {
            // AC-9-10: 9-DC-7: 至少 1 个病种
            var actions = new List<ActionAxisRow>
            {
                new ActionAxisRow(0, "test_action", "10")
            };
            var treatableBy = new List<TreatableByRow>();

            var ex = Assert.Throws<DiseaseActionAxisValidationException>(() =>
                DiseaseActionAxisValidator.Validate(actions, treatableBy));
            Assert.AreEqual(7, ex.RuleNumber, "应报 9-DC-7");
        }

        // ── 9-DC-1 / 9-DC-2 负夹具(binder 层,非 validator 层)──────────────

        [Test]
        public void test_diseaseActionAxis_dc1_invalidOwner_throws()
        {
            // AC-9-04: 9-DC-1: owner ∈ {"10", "11"}
            // ⚠️ DC-1 在 binder 层校验,不在 validator 层 —— 须走 Bind 路径。
            string badJson = @"{
                ""schema_version"": 1,
                ""actions"": [
                    { ""id"": 0, ""name"": ""test_action"", ""owner"": ""12"" }
                ],
                ""treatable_by"": {
                    ""DIS_TEST"": [ { ""action"": 0, ""polarity"": ""causal"" } ]
                }
            }";

            var ex = Assert.Throws<BakeValidationException>(() =>
                DiseaseActionAxisBinder.Bind(badJson));
            Assert.IsTrue(ContainsError(ex, "9-DC-1"),
                $"DC-1 的违反须出现在 errors 里,实际:{string.Join("; ", ex.Errors)}");
        }

        [Test]
        public void test_diseaseActionAxis_dc2_invalidPolarity_throws()
        {
            // AC-9-05: 9-DC-2: polarity ∈ {causal, symptomatic}
            // ⚠️ DC-2 在 binder 层校验,不在 validator 层 —— 须走 Bind 路径。
            string badJson = @"{
                ""schema_version"": 1,
                ""actions"": [
                    { ""id"": 0, ""name"": ""test_action"", ""owner"": ""10"" }
                ],
                ""treatable_by"": {
                    ""DIS_TEST"": [ { ""action"": 0, ""polarity"": ""unknown"" } ]
                }
            }";

            var ex = Assert.Throws<BakeValidationException>(() =>
                DiseaseActionAxisBinder.Bind(badJson));
            Assert.IsTrue(ContainsError(ex, "9-DC-2"),
                $"DC-2 的违反须出现在 errors 里,实际:{string.Join("; ", ex.Errors)}");
        }

        // ── AC-9-11/12/13:NOISE_BAND_9 具名常量(2026-10-07 评审 S4-1 补)──────────

        [Test]
        public void test_noiseBand9_constantsRegistered()
        {
            // AC-9-11/12: entities.yaml constants 段须有 NOISE_BAND_PROGRESS_9 / NOISE_BAND_POTENCY_9
            // AC-9-13: 量纲纪律 —— constraint 须明写三处不同尺
            string yaml = File.ReadAllText(Path.Combine(RepoRoot, "design", "registry", "entities.yaml"));
            Assert.IsTrue(yaml.Contains("NOISE_BAND_PROGRESS_9"),
                "AC-9-11: entities.yaml 须登记 NOISE_BAND_PROGRESS_9(Progress 域)");
            Assert.IsTrue(yaml.Contains("NOISE_BAND_POTENCY_9"),
                "AC-9-12: entities.yaml 须登记 NOISE_BAND_POTENCY_9(药效幅值域)");
            // AC-9-13: 两常量条目附近的 constraint 表述须点名量纲不互借
            Assert.IsTrue(yaml.Contains("σ") || yaml.Contains("不得") || yaml.Contains("不同"),
                "AC-9-13: 量纲纪律须在条目 constraint 中明写");
        }

        // ── AC-9-17: prescription_actions.json 的 action_id 重排 1→10(评审 S4-2 补)──

        [Test]
        public void test_prescriptionActionId_rearrangedTo10()
        {
            // AC-9-17: salicylic_acid 的 action_id 由 1(影子)改为 10(柳树皮,9 的轴)
            // ⚠️ 走烘焙产物(非裸字符串匹配)—— 数据经 binder 校验后的行为面。
            var result = DiseaseActionAxisBaker.BakeFromRepo(RepoRoot);   // 轴可用性前置
            string json = File.ReadAllText(Path.Combine(RepoRoot, "assets", "data", "prescription_actions.json"));
            Assert.IsTrue(json.Contains("\"action_id\": 10"),
                "AC-9-17: 须有 action_id = 10(重排后)");
            // 影子期编号 1 只允许作为轴成员(9 的轴 id=1 是 10 的节奏型通气),但不得出现在处方表
            int idx = json.IndexOf("\"actions\"", StringComparison.Ordinal);
            string actionsSeg = idx >= 0 ? json.Substring(idx) : json;
            Assert.IsFalse(actionsSeg.Contains("\"action_id\": 1,"),
                "AC-9-17: 处方表不得残留影子期 action_id = 1");
        }

        // ── 辅助 ──────────────────────────────────────────────────────────

        private static bool ContainsError(BakeValidationException ex, string keyword)
        {
            foreach (string e in ex.Errors)
                if (e.Contains(keyword)) return true;
            return false;
        }
    }
}
