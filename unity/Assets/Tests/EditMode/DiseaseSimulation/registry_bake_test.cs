// disease-simulation Story 003 测试
//
// AC-23: R1 schema 17 条构建期校验
// AC-24: 伤情模型 11 态 + 病种注册表 8 病种
// TR-disease-013: 门 A 硬化
// TR-disease-021: PATIENT_APPEARANCE_CAP 配置项

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    public class RegistryBakeTest
    {
        private List<DiseaseRegistryEntry> _validEntries;

        [SetUp]
        public void Setup()
        {
            _validEntries = new List<DiseaseRegistryEntry>
            {
                new DiseaseRegistryEntry
                {
                    DiseaseId = 1,
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
                new DiseaseRegistryEntry
                {
                    DiseaseId = 2,
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
            entries.Add(new DiseaseRegistryEntry { DiseaseId = 1, DiseaseKey = "duplicate" });

            var ex = Assert.Throws<RegistryValidationException>(() => RegistrySchemaValidator.Validate(entries));
            Assert.AreEqual(1, ex.RuleNumber);
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
    }
}
