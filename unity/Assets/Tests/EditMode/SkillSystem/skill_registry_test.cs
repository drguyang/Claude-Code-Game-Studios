// ============================================================================
// 技能注册表测试 —— Story 001 验收
// 权威来源: production/epics/skill-system/story-001-skill-registry-and-definitions.md
// ============================================================================
// 落点:unity/Assets/Tests/EditMode/SkillSystem/skill_registry_test.cs
//    按 ADR-025 §⑤(2026-09-23 路径订正注)Unity 只编译 Assets/ 树,真身落此。
// ============================================================================

using System;
using System.Linq;
using NUnit.Framework;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;

namespace DaYiJingCheng.Tests.Unit.SkillSystem
{
    [TestFixture]
    internal sealed class SkillRegistryTest
    {
        // ════════════════ AC-1: 19 项技能全部可枚举 ════════════════

        [Test]
        public void test_skillRegistry_all19Skills_enumerable()
        {
            var all = SkillRegistry.GetAllDefinitions();
            Assert.That(all.Length, Is.EqualTo(SkillRegistry.SkillCount),
                "应恰好 19 项技能");
        }

        [Test]
        public void test_skillRegistry_eachSkill_hasNonEmptyFields()
        {
            foreach (var def in SkillRegistry.GetAllDefinitions())
            {
                Assert.That(def.Name, Is.Not.Empty, $"SkillId={def.SkillId} 名称非空");
                Assert.That(def.GrowthTrigger, Is.Not.Empty, $"SkillId={def.SkillId} 成长触发非空");
            }
        }

        // ════════════════ AC-2: P0/P1a 分流正确 ════════════════

        [Test]
        public void test_skillRegistry_p0Count_is7()
        {
            var p0 = SkillRegistry.GetP0Skills().ToList();
            Assert.That(p0.Count, Is.EqualTo(SkillRegistry.P0SkillCount),
                "P0 应恰好 7 项");

            var names = p0.Select(d => d.Name).OrderBy(n => n).ToList();
            Assert.That(names, Is.EquivalentTo(new[]
            {
                "采集", "处方用药", "急救", "诊断", "炮制", "徒手", "短兵",
            }), "P0 七项应包含:诊断/急救/处方用药/采集/炮制/徒手/短兵");
        }

        [Test]
        public void test_skillRegistry_p1aCount_is12()
        {
            var p1a = SkillRegistry.GetP1aSkills().ToList();
            Assert.That(p1a.Count, Is.EqualTo(SkillRegistry.SkillCount - SkillRegistry.P0SkillCount),
                "P1a 应 12 项(19-7)");
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC-2 / AC-3 已在上方合并验证(P0 名称集 + 无依赖 + 辨证依赖条件)
        // ════════════════════════════════════════════════════════════════════════

        // ════════════════ AC-3: 技能依赖解锁条件正确 ════════════════

        [Test]
        public void test_skillRegistry_dependency_unlockCondition()
        {
            // 辨证 = 诊断 ≥ 10 解锁
            var bianZheng = SkillRegistry.GetDefinition((int)SkillId.辨证);
            Assert.That(bianZheng.Dependencies.Length, Is.EqualTo(1),
                "辨证应有 1 个依赖");
            Assert.That(bianZheng.Dependencies[0].PrerequisiteSkillId,
                Is.EqualTo((int)SkillId.诊断));
            Assert.That(bianZheng.Dependencies[0].MinLevel, Is.EqualTo(10));
        }

        [Test]
        public void test_skillRegistry_p0Skills_haveNoDependencies()
        {
            foreach (var def in SkillRegistry.GetP0Skills())
            {
                Assert.That(def.Dependencies.Length, Is.EqualTo(0),
                    $"{def.Name}(P0) 应无解锁依赖");
            }
        }

        // ════════════════ AC-4: SKILL_CAP 单一常量 ════════════════

        [Test]
        public void test_skillRegistry_skillCap_is60()
        {
            Assert.That(SkillRegistry.SKILL_CAP, Is.EqualTo(60));
        }

        [Test]
        public void test_skillRegistry_skillCap_appliesToAll()
        {
            foreach (var def in SkillRegistry.GetAllDefinitions())
            {
                Assert.That(def.Dependencies.All(d => d.MinLevel <= 60),
                    $"{def.Name} 的依赖等级不超 SKILL_CAP");
            }
        }

        // ════════════════ AC-5: 调参表 Fix 字段可 Parse ════════════════

        [Test]
        public void test_skillTuningTable_default_deathLoss_is0195()
        {
            var table = SkillTuningTable.Default;
            // 0.95 = 19/20 => raw = 19 * 65536 / 20 = 62233600 / 20... wait
            // Fix(19/20) raw = 19 * OneRaw / 20 = 19 * 65536 / 20
            long expectedRaw = 19L * Fix.OneRaw / 20;
            Assert.That(table.DeathLoss.Raw, Is.EqualTo(expectedRaw),
                "DEATH_LOSS = FixParse(\"19/20\") = 0.95");
        }

        [Test]
        public void test_skillTuningTable_default_noveltyCoefficients()
        {
            var table = SkillTuningTable.Default;
            Assert.That(table.NoveltyFirstCoeff.Raw, Is.EqualTo(3L * Fix.OneRaw),
                "NOVELTY_FIRST = 3.0");
            Assert.That(table.NoveltyDecayCoeff.Raw, Is.EqualTo(2L * Fix.OneRaw / 10),
                "NOVELTY_DECAY = 0.2");
        }

        [Test]
        public void test_skillTuningTable_default_fixValues_matchFixParse()
        {
            // 验证 Fix.FromRational 舍入路径与 FixParse 一致(截断 vs 舍入 BLOCKING 修复验证)
            var table = SkillTuningTable.Default;
            Assert.That(table.WeaponMultipliers[(int)WeaponLine.长兵].Raw,
                Is.EqualTo(FixParse.FromRatio(11L, 10L).Raw),
                "长兵 1.1: FromRational 与 FixParse 应一致");
            Assert.That(table.WeaponMultipliers[(int)WeaponLine.暗器].Raw,
                Is.EqualTo(FixParse.FromRatio(8L, 10L).Raw),
                "暗器 0.8: FromRational 与 FixParse 应一致");
        }

        [Test]
        public void test_skillTuningTable_medCombatMod_is020()
        {
            var table = SkillTuningTable.Default;
            Assert.That(table.MedicalCombatModifier.Raw, Is.EqualTo(20L * Fix.OneRaw / 100),
                "MED_COMBAT_MOD = 0.20");
        }

        [Test]
        public void test_skillTuningTable_runBase_asFix()
        {
            // 奔跑 BASE = 0.2 => raw = 1 * OneRaw / 5 = 13107
            var runDef = SkillRegistry.GetDefinition((int)SkillId.奔跑);
            Assert.That(runDef.BaseXp.Raw, Is.EqualTo(Fix.OneRaw / 5),
                "奔跑 BASE = 0.2 => Fix.OneRaw / 5");

            // 诊断 BASE = 8 => raw = 8 * OneRaw
            var diagDef = SkillRegistry.GetDefinition((int)SkillId.诊断);
            Assert.That(diagDef.BaseXp.Raw, Is.EqualTo(8L * Fix.OneRaw),
                "诊断 BASE = 8 => 8 * OneRaw");
        }

        // ════════════════ AC-6: DIAG_TIERS 可查询 ════════════════

        [Test]
        public void test_skillTuningTable_diagTiers_correct()
        {
            var table = SkillTuningTable.Default;
            var tiers = table.GetDiagTiers();
            Assert.That(tiers, Is.EquivalentTo(new[] { 10, 20, 35, 50 }));
        }

        [Test]
        public void test_skillTuningTable_insightTiers_correct()
        {
            var table = SkillTuningTable.Default;
            var tiers = table.GetInsightTiers();
            Assert.That(tiers, Is.EquivalentTo(new[] { 10, 20, 35, 50 }));
        }

        [Test]
        public void test_skillTuningTable_diagSlotCount()
        {
            var table = SkillTuningTable.Default;
            // 4 tiers => 3 slots(不含 P1a 检验线 35)
            Assert.That(table.DiagSlotCount, Is.EqualTo(3));
        }

        // ════════════════ 辅助查询接口 ════════════════

        [Test]
        public void test_skillRegistry_isValidSkillId_rangeCheck()
        {
            Assert.That(SkillRegistry.IsValidSkillId(0), Is.True);
            Assert.That(SkillRegistry.IsValidSkillId(18), Is.True);
            Assert.That(SkillRegistry.IsValidSkillId(19), Is.False);
            Assert.That(SkillRegistry.IsValidSkillId(-1), Is.False);
        }

        [Test]
        public void test_skillRegistry_getDefinition_outOfRange_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SkillRegistry.GetDefinition(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SkillRegistry.GetDefinition(19));
        }

        [Test]
        public void test_skillDefinition_nullDependencies_coercesToEmpty()
        {
            // 防御性构造:null dependencies → Array.Empty<SkillDependency>()
            var def = new SkillDefinition(
                skillId: 99, name: "x", category: SkillCategory.医术,
                growthTrigger: "x", baseXp: new Fix(Fix.OneRaw),
                dependencies: null, isP0: true);
            Assert.That(def.Dependencies, Is.Not.Null, "null 依赖应被转为空数组");
            Assert.That(def.Dependencies.Length, Is.EqualTo(0));
        }

        [Test]
        public void test_skillRegistry_isP0_correct()
        {
            Assert.That(SkillRegistry.IsP0((int)SkillId.诊断), Is.True);
            Assert.That(SkillRegistry.IsP0((int)SkillId.辨证), Is.False);
            Assert.That(SkillRegistry.IsP0((int)SkillId.短兵), Is.True);
            Assert.That(SkillRegistry.IsP0((int)SkillId.钝器), Is.False);
        }

        [Test]
        public void test_skillRegistry_isCombatSkill_correct()
        {
            Assert.That(SkillRegistry.IsCombatSkill((int)SkillId.徒手), Is.True);
            Assert.That(SkillRegistry.IsCombatSkill((int)SkillId.诊断), Is.False);
            Assert.That(SkillRegistry.IsCombatSkill((int)SkillId.暗器), Is.True);
        }

        [Test]
        public void test_skillRegistry_getByCategory_correct()
        {
            var medical = SkillRegistry.GetByCategory(SkillCategory.医术).ToList();
            Assert.That(medical.Count, Is.EqualTo(6), "医术 6 项");

            var combat = SkillRegistry.GetByCategory(SkillCategory.格斗).ToList();
            Assert.That(combat.Count, Is.EqualTo(5), "格斗 5 项");
        }

        [Test]
        public void test_skillRegistry_getLinkedMedicalSkill_correct()
        {
            Assert.That(SkillRegistry.GetLinkedMedicalSkill(WeaponLine.徒手),
                Is.EqualTo((int)SkillId.急救));
            Assert.That(SkillRegistry.GetLinkedMedicalSkill(WeaponLine.短兵),
                Is.EqualTo((int)SkillId.手术));
            Assert.That(SkillRegistry.GetLinkedMedicalSkill(WeaponLine.钝器),
                Is.EqualTo(-1));
        }
    }
}
