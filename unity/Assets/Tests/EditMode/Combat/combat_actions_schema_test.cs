// combat-weapons Story 001 测试
//
// AC-1: schema 校验通过
// AC-2: A20 漂移门
// AC-3: Kindgen 登记

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Combat
{
    public class CombatActionsSchemaTest
    {
        private CombatActionEntry _validEntry;

        [SetUp]
        public void Setup()
        {
            _validEntry = new CombatActionEntry
            {
                Id = 1,
                ActorClass = "Player",
                AttackClass = "Melee",
                WeaponLine = 0,
                MapsToInjury = 0,
                BaseStep = new Fix(1000),
                CooldownTicks = 10,
                UnlockLevel = 1,
                RangeOverride = 0,
                DurationTicks = 5,
                MaxTargets = 1
            };
        }

        // AC-1: schema 校验通过
        [Test]
        public void test_validEntry_passes()
        {
            Assert.DoesNotThrow(() => CombatActionSchema.Validate(_validEntry));
        }

        // AC-1: A14 max_targets = 1
        [Test]
        public void test_a14_maxTargetsMustBeOne()
        {
            var invalidEntry = new CombatActionEntry
            {
                Id = 2,
                ActorClass = "Player",
                AttackClass = "Melee",
                WeaponLine = 0,
                MapsToInjury = 0,
                BaseStep = new Fix(1000),
                CooldownTicks = 10,
                UnlockLevel = 1,
                RangeOverride = 0,
                DurationTicks = 5,
                MaxTargets = 2
            };

            Assert.Throws<CombatSchemaValidationException>(() => CombatActionSchema.Validate(invalidEntry));
        }

        // AC-1: A17 cooldown_ticks >= 1
        [Test]
        public void test_a17_cooldownTicksMustBePositive()
        {
            var invalidEntry = new CombatActionEntry
            {
                Id = 3,
                ActorClass = "Player",
                AttackClass = "Melee",
                WeaponLine = 0,
                MapsToInjury = 0,
                BaseStep = new Fix(1000),
                CooldownTicks = 0,
                UnlockLevel = 1,
                RangeOverride = 0,
                DurationTicks = 5,
                MaxTargets = 1
            };

            Assert.Throws<CombatSchemaValidationException>(() => CombatActionSchema.Validate(invalidEntry));
        }

        // AC-1: A13a MAG_FLOOR > 0
        [Test]
        public void test_a13a_magFloorPositive()
        {
            Assert.Greater(CombatActionSchema.MAG_FLOOR, 0, "MAG_FLOOR 必须 > 0");
        }

        // AC-1: A13b MAG_FLOOR < MAG_CAP
        [Test]
        public void test_a13b_magFloorLessThanCap()
        {
            Assert.Less(CombatActionSchema.MAG_FLOOR, CombatActionSchema.MAG_CAP, "MAG_FLOOR 必须 < MAG_CAP");
        }

        // AC-2: Fix 字段解析
        [Test]
        public void test_fixField_parsesFraction()
        {
            var result = CombatActionSchema.ParseFixField("base_step", "1/4", 1);
            Assert.AreEqual(new Fix(16384).Raw, result.Raw, "1/4 应解析为 16384");
        }

        // AC-2: Fix 字段拒绝浮点字面量
        [Test]
        public void test_fixField_rejectsFloatLiteral()
        {
            Assert.Throws<CombatSchemaValidationException>(() =>
                CombatActionSchema.ParseFixField("base_step", "0.25", 1));
        }

        // AC-3: Kindgen 登记
        [Test]
        public void test_kindgen_registeredKinds()
        {
            // 验证 InjuryOnset / EnemyInjuryOnset / InjuryStateChanged 存在
            Assert.IsTrue(Enum.IsDefined(typeof(EventKind), EventKind.InjuryOnset));
            Assert.IsTrue(Enum.IsDefined(typeof(EventKind), EventKind.EnemyInjuryOnset));
            Assert.IsTrue(Enum.IsDefined(typeof(EventKind), EventKind.InjuryStateChanged));
        }

        // A17: maps_to_injury 外键校验
        [Test]
        public void test_a17_mapsToInjuryForeignKey()
        {
            var invalidEntry = new CombatActionEntry
            {
                Id = 10,
                ActorClass = "Player",
                AttackClass = "Melee",
                WeaponLine = 0,
                MapsToInjury = -1, // 悬空外键
                BaseStep = new Fix(1000),
                CooldownTicks = 10,
                UnlockLevel = 1,
                RangeOverride = 0,
                DurationTicks = 5,
                MaxTargets = 1
            };

            Assert.Throws<CombatSchemaValidationException>(() => CombatActionSchema.Validate(invalidEntry));
        }

        // A18: weapon_line ⟺ Natural 等价关系
        [Test]
        public void test_a18_weaponLineNaturalEquivalence()
        {
            // Natural 行不应有 weapon_line
            var naturalEntry = new CombatActionEntry
            {
                Id = 11,
                ActorClass = "Beast",
                AttackClass = "Natural",
                WeaponLine = 0, // 有 weapon_line
                MapsToInjury = 0,
                BaseStep = new Fix(1000),
                CooldownTicks = 10,
                UnlockLevel = 1,
                RangeOverride = 1,
                DurationTicks = 5,
                MaxTargets = 1
            };

            Assert.Throws<CombatSchemaValidationException>(() => CombatActionSchema.Validate(naturalEntry));
        }

        // A22: Natural ⇒ range_override ≥ 1
        [Test]
        public void test_a22_naturalRangeOverride()
        {
            var invalidEntry = new CombatActionEntry
            {
                Id = 12,
                ActorClass = "Beast",
                AttackClass = "Natural",
                WeaponLine = -1, // 无 weapon_line
                MapsToInjury = 0,
                BaseStep = new Fix(1000),
                CooldownTicks = 10,
                UnlockLevel = 1,
                RangeOverride = 0, // 违反 A22
                DurationTicks = 5,
                MaxTargets = 1
            };

            Assert.Throws<CombatSchemaValidationException>(() => CombatActionSchema.Validate(invalidEntry));
        }

        // A18: 非 Natural 行必须有 weapon_line
        [Test]
        public void test_a18_nonNaturalMustHaveWeaponLine()
        {
            var invalidEntry = new CombatActionEntry
            {
                Id = 13,
                ActorClass = "Player",
                AttackClass = "Melee",
                WeaponLine = -1, // 无 weapon_line
                MapsToInjury = 0,
                BaseStep = new Fix(1000),
                CooldownTicks = 10,
                UnlockLevel = 1,
                RangeOverride = 0,
                DurationTicks = 5,
                MaxTargets = 1
            };

            Assert.Throws<CombatSchemaValidationException>(() => CombatActionSchema.Validate(invalidEntry));
        }
    }
}
