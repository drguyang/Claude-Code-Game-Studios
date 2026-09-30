// random-events Story 001 测试
//
// AC-52-01/02/03: 池条目 schema 校验
// AC-52-42: 档枚举闭集
// AC-52-43: P0 医馆不可损毁
// 门 A: 引用集白名单

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.RandomEvents
{
    public class EventPoolSchemaTest
    {
        private EventPoolEntry _validEntry;

        [SetUp]
        public void Setup()
        {
            _validEntry = new EventPoolEntry
            {
                Key = 1,
                WBase = 100,
                Tier = EventTier.Threat,
                Anchor = SpawnAnchor.ClinicFront,
                TriggerMode = TriggerMode.Random,
                CauseFlag = ""
            };
        }

        // AC-52-01: 池条目 schema 完整
        [Test]
        public void test_validEntry_passes()
        {
            Assert.DoesNotThrow(() => EventPoolValidator.Validate(_validEntry));
        }

        // AC-52-02: 档枚举闭集
        [Test]
        public void test_tierEnum_closedSet()
        {
            Assert.IsTrue(Enum.IsDefined(typeof(EventTier), EventTier.Threat));
            Assert.IsTrue(Enum.IsDefined(typeof(EventTier), EventTier.Opportunity));
            Assert.IsTrue(Enum.IsDefined(typeof(EventTier), EventTier.Reaction));
            Assert.IsTrue(Enum.IsDefined(typeof(EventTier), EventTier.Disaster));
            Assert.IsFalse(Enum.IsDefined(typeof(EventTier), (EventTier)999));
        }

        // AC-52-42: spawn_anchor P0 三员
        [Test]
        public void test_spawnAnchor_p0ThreeMembers()
        {
            Assert.IsTrue(Enum.IsDefined(typeof(SpawnAnchor), SpawnAnchor.ClinicFront));
            Assert.IsTrue(Enum.IsDefined(typeof(SpawnAnchor), SpawnAnchor.TravelPath));
            Assert.IsTrue(Enum.IsDefined(typeof(SpawnAnchor), SpawnAnchor.GatherPoint));
        }

        // 拒收表：Key < 0
        [Test]
        public void test_reject_negativeKey()
        {
            var invalidEntry = new EventPoolEntry
            {
                Key = -1,
                WBase = 100,
                Tier = EventTier.Threat,
                Anchor = SpawnAnchor.ClinicFront,
                TriggerMode = TriggerMode.Random
            };

            Assert.Throws<EventPoolValidationException>(() => EventPoolValidator.Validate(invalidEntry));
        }

        // 拒收表：WBase < 0
        [Test]
        public void test_reject_negativeWBase()
        {
            var invalidEntry = new EventPoolEntry
            {
                Key = 1,
                WBase = -100,
                Tier = EventTier.Threat,
                Anchor = SpawnAnchor.ClinicFront,
                TriggerMode = TriggerMode.Random
            };

            Assert.Throws<EventPoolValidationException>(() => EventPoolValidator.Validate(invalidEntry));
        }

        // 拒收表：Tier 不在闭集内
        [Test]
        public void test_reject_invalidTier()
        {
            var invalidEntry = new EventPoolEntry
            {
                Key = 1,
                WBase = 100,
                Tier = (EventTier)999,
                Anchor = SpawnAnchor.ClinicFront,
                TriggerMode = TriggerMode.Random
            };

            Assert.Throws<EventPoolValidationException>(() => EventPoolValidator.Validate(invalidEntry));
        }

        // 拒收表：Anchor 不在闭集内
        [Test]
        public void test_reject_invalidAnchor()
        {
            var invalidEntry = new EventPoolEntry
            {
                Key = 1,
                WBase = 100,
                Tier = EventTier.Threat,
                Anchor = (SpawnAnchor)999,
                TriggerMode = TriggerMode.Random
            };

            Assert.Throws<EventPoolValidationException>(() => EventPoolValidator.Validate(invalidEntry));
        }

        // AC-52-03: 零内容（无医学内容类型引用）
        [Test]
        public void test_noMedicalContentTypes()
        {
            // 验证 EventPoolEntry 无 float/double 字段
            var entryType = typeof(EventPoolEntry);
            foreach (var field in entryType.GetFields())
            {
                Assert.IsFalse(field.FieldType == typeof(float),
                    $"字段 {field.Name} 不应为 float");
                Assert.IsFalse(field.FieldType == typeof(double),
                    $"字段 {field.Name} 不应为 double");
            }
        }

        // 脚本条目通道
        [Test]
        public void test_scriptEntry_injectInterface()
        {
            // 验证 IEventDirector 接口存在
            var directorType = typeof(IEventDirector);
            Assert.IsNotNull(directorType);
            Assert.IsTrue(directorType.IsInterface);
        }

        // 触发方式枚举
        [Test]
        public void test_triggerMode_enum()
        {
            Assert.IsTrue(Enum.IsDefined(typeof(TriggerMode), TriggerMode.Random));
            Assert.IsTrue(Enum.IsDefined(typeof(TriggerMode), TriggerMode.Script));
        }
    }
}
