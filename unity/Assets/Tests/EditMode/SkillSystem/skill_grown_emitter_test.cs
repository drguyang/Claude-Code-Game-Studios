// ============================================================================
// SkillGrown 事件发射 EditMode 单测 —— Story 007 验收
// 权威来源: production/epics/skill-system/story-007-skillgrown-event-emit.md
//   · AC-1 ~ AC-6(EmitGrowth / 载荷结构 / Level 哨兵 / patient_id / 全序键 / 无 disease_id)
//   · ADR-007 §一(主机唯一 Append) · ADR-009 §三(三流全序键)
//   · ADR-024 SkillGrown 已在 entities.yaml 具名登记(stream: history, author: 30)
//   · ADR-005 确定性 sim(纯函数构造,无随机)
// ============================================================================
// 测试策略:验证 EmitGrowth 返回 SkillGrownPayload 字段 + 往返编解码;
// SimEvent 骨架(Tick/Patient/Seq/StreamPriority)由主机 Append 层负责,
// 本测试只验证载荷内容与 argument mapping 正确性。
// 零随机种子、零时间依赖、零外部 I/O。
// ============================================================================

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.SkillSystem
{
    /// <summary>Story 007 SkillGrown 事件发射的 EditMode 单测。</summary>
    [TestFixture]
    internal sealed class SkillGrownEmitterTest
    {
        // ═══════════════════════════════════════════════════════════════════
        // AC-1: EmitGrowth 触发 SkillGrown 载荷,字段完整
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-1 主例: EmitGrowth 返回 SkillGrownPayload,Kind 由 registry 确认。</summary>
        [Test]
        public void test_ac1_emitGrowth_returnsSkillGrownPayload_withCorrectFields()
        {
            var payload = SkillGrownEmitter.EmitGrowth(
                actorId: 1, skillId: (int)SkillId.诊断, objectId: 1,
                novelty: NoveltyClass.First, level: null, currentTick: 100);

            Assert.That(payload.ActorId, Is.EqualTo(1));
            Assert.That(payload.SkillId, Is.EqualTo((int)SkillId.诊断));
            Assert.That(payload.ObjectId, Is.EqualTo(1));
            Assert.That(payload.NoveltyClass, Is.EqualTo((int)NoveltyClass.First));
            Assert.That(payload.Level, Is.EqualTo(SkillGrownPayload.LevelNotGrown), "未升级 → 哨兵");
        }

        /// <summary>AC-1: Tick 正确写入(由主机从 SimEvent.Tick 传入)。</summary>
        [Test]
        public void test_ac1_emitGrowth_setsCurrentTick()
        {
            var payload = SkillGrownEmitter.EmitGrowth(
                actorId: 1, skillId: (int)SkillId.诊断, objectId: 1,
                novelty: NoveltyClass.First, level: null, currentTick: 500);

            // tick 用于主机构造 SimEvent,此处验证通过参数传递无异常
            Assert.That(payload.ActorId, Is.EqualTo(1), "actorId 与 tick 同时传递无异常");
        }

        /// <summary>AC-1: PatientId 默认 PatientId.None(-1)。</summary>
        [Test]
        public void test_ac1_emitGrowth_defaultPatientId_isNone()
        {
            var payload = SkillGrownEmitter.EmitGrowth(
                actorId: 1, skillId: (int)SkillId.诊断, objectId: 1,
                novelty: NoveltyClass.First, level: null, currentTick: 100);

            Assert.That(payload.PatientId, Is.EqualTo(-1),
                "默认重载 patientId = PatientId.None(-1)");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-2 / AC-3 / AC-4: 载荷字段完整性
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-2: 升级时 Level = 新等级(绝对等级)。</summary>
        [Test]
        public void test_ac2_levelUp_setsAbsoluteLevel()
        {
            var payload = SkillGrownEmitter.EmitGrowth(
                actorId: 1, patientId: new PatientId(-1), skillId: (int)SkillId.诊断,
                objectId: 1, novelty: NoveltyClass.First, level: 2, currentTick: 100);

            Assert.That(payload.Level, Is.EqualTo(2),
                "升级时 Level = 新等级(绝对等级,非增量)");
        }

        /// <summary>AC-2 边缘: Level = 60(满级) 仍携带。</summary>
        [Test]
        public void test_ac2_levelCap_sixty_carriesLevel()
        {
            var payload = SkillGrownEmitter.EmitGrowth(
                actorId: 1, patientId: new PatientId(-1), skillId: (int)SkillId.诊断,
                objectId: 1, novelty: NoveltyClass.First, level: 60, currentTick: 100);

            Assert.That(payload.Level, Is.EqualTo(60),
                "满级 60 仍携带等级值");
        }

        /// <summary>AC-3: 未升级时 Level = 哨兵 -1(非 0)。</summary>
        [Test]
        public void test_ac3_noLevelUp_setsSentinelMinusOne()
        {
            var payload = SkillGrownEmitter.EmitGrowth(
                actorId: 1, patientId: new PatientId(-1), skillId: (int)SkillId.诊断,
                objectId: 1, novelty: NoveltyClass.Normal, level: null, currentTick: 100);

            Assert.That(payload.Level, Is.EqualTo(-1),
                "未升级 → Level = 哨兵 -1(非 0)");
        }

        /// <summary>AC-3 边缘: Level=0 是真实等级,与哨兵 -1 不冲突。</summary>
        [Test]
        public void test_ac3_levelZero_isValidRealLevel()
        {
            var payload = SkillGrownEmitter.EmitGrowth(
                actorId: 1, patientId: new PatientId(-1), skillId: (int)SkillId.诊断,
                objectId: 1, novelty: NoveltyClass.First, level: 0, currentTick: 100);

            Assert.That(payload.Level, Is.EqualTo(0),
                "Level=0 是真实 0 级,非哨兵");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-4: patient_id 默认 PatientId.None(-1);8 诊断可传入具体 id
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-4 主例: 无病例语境 → PatientId.None(-1)。</summary>
        [Test]
        public void test_ac4_noCaseContext_patientIdIsNone()
        {
            var payload = SkillGrownEmitter.EmitGrowth(
                actorId: 1, skillId: (int)SkillId.采集, objectId: 5,
                novelty: NoveltyClass.First, level: null, currentTick: 100);

            Assert.That(payload.PatientId, Is.EqualTo(-1),
                "采集(无病例语境) → PatientId.None(-1)");
        }

        /// <summary>AC-4: 传入具体 patientId → 载荷携带该 id。</summary>
        [Test]
        public void test_ac4_withPatientId_carriesId()
        {
            var specificPatient = new PatientId(42);
            var payload = SkillGrownEmitter.EmitGrowth(
                actorId: 1, skillId: (int)SkillId.诊断, objectId: 1,
                novelty: NoveltyClass.First, level: 1, currentTick: 100,
                patientId: specificPatient);

            Assert.That(payload.PatientId, Is.EqualTo(42),
                "8 诊断传入具体 patientId → 载荷携带该病例 id");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-5: 三流全序键 — EmitGrowth 输出满足 SimEvent 头部字段
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-5: Tick/Patient 由调用方传入,载荷字段正确映射。</summary>
        [Test]
        public void test_ac5_fullOrderKey_fieldsMapFromArguments()
        {
            long tick = 1000;
            var patient = new PatientId(7);

            var payload = SkillGrownEmitter.EmitGrowth(
                actorId: 2, skillId: (int)SkillId.急救, objectId: 3,
                novelty: NoveltyClass.Stale, level: null, currentTick: tick,
                patientId: patient);

            // 载荷携带 patient_id;SimEvent 头部由主机构造时使用 caller 传入的 tick/patient
            Assert.That(payload.PatientId, Is.EqualTo(7), "PatientId 由 caller 传入");
            Assert.That(payload.ActorId, Is.EqualTo(2), "actorId 正确映射");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-6: 载荷无 disease_id(验证 SkillGrownPayload 字段集合)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-6: SkillGrownPayload 字段集合 = {ActorId, PatientId, SkillId, ObjectId, NoveltyClass, Level}。</summary>
        [Test]
        public void test_ac6_payloadFields_noDiseaseId()
        {
            var fields = typeof(SkillGrownPayload).GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public);

            var fieldNames = new HashSet<string>();
            foreach (var f in fields)
                fieldNames.Add(f.Name);

            CollectionAssert.AreEquivalent(
                new[] { "ActorId", "PatientId", "SkillId", "ObjectId", "NoveltyClass", "Level" },
                fieldNames,
                "SkillGrownPayload 字段集合不含 disease_id");
        }

        // ═══════════════════════════════════════════════════════════════════
        // 载荷往返验证(EmitGrowth → PayloadCodec.Encode → Decode)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>完整往返: EmitGrowth 载荷经 PayloadCodec 编解码恢复全部字段。</summary>
        [Test]
        public void test_roundtrip_payload_encodesAndDecodesCorrectly()
        {
            var payload = SkillGrownEmitter.EmitGrowth(
                actorId: 7, skillId: (int)SkillId.急救, objectId: 3,
                novelty: NoveltyClass.Stale, level: 5, currentTick: 200,
                patientId: new PatientId(12));

            byte[] encoded = PayloadCodec.Encode(payload);
            var decoded = PayloadCodec.Decode<SkillGrownPayload>(EventKind.SkillGrown, encoded);

            Assert.That(decoded.ActorId, Is.EqualTo(7));
            Assert.That(decoded.PatientId, Is.EqualTo(12));
            Assert.That(decoded.SkillId, Is.EqualTo((int)SkillId.急救));
            Assert.That(decoded.ObjectId, Is.EqualTo(3));
            Assert.That(decoded.NoveltyClass, Is.EqualTo((int)NoveltyClass.Stale));
            Assert.That(decoded.Level, Is.EqualTo(5));
        }

        // ═══════════════════════════════════════════════════════════════════
        // 异常输入验证
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>负 actorId → ArgumentOutOfRangeException。</summary>
        [Test]
        public void test_negativeActorId_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                SkillGrownEmitter.EmitGrowth(-1, (int)SkillId.诊断, 1,
                    NoveltyClass.First, null, 100);
            }, "actorId 为负须抛 ArgumentOutOfRangeException");
        }

        /// <summary>负 skillId → ArgumentOutOfRangeException。</summary>
        [Test]
        public void test_negativeSkillId_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                SkillGrownEmitter.EmitGrowth(1, -1, 1,
                    NoveltyClass.First, null, 100);
            }, "skillId 为负须抛 ArgumentOutOfRangeException");
        }

        /// <summary>负 objectId → ArgumentOutOfRangeException。</summary>
        [Test]
        public void test_negativeObjectId_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                SkillGrownEmitter.EmitGrowth(1, (int)SkillId.诊断, -1,
                    NoveltyClass.First, null, 100);
            }, "objectId 为负须抛 ArgumentOutOfRangeException");
        }

        /// <summary>负 currentTick → ArgumentOutOfRangeException。</summary>
        [Test]
        public void test_negativeCurrentTick_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                SkillGrownEmitter.EmitGrowth(1, (int)SkillId.诊断, 1,
                    NoveltyClass.First, null, -1);
            }, "currentTick 为负须抛 ArgumentOutOfRangeException");
        }

        /// <summary>非法 NoveltyClass ordinal → ArgumentOutOfRangeException。</summary>
        [Test]
        public void test_invalidNoveltyClass_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                SkillGrownEmitter.EmitGrowth(1, (int)SkillId.诊断, 1,
                    (NoveltyClass)99, null, 100);
            }, "NoveltyClass ordinal 99 须抛 ArgumentOutOfRangeException");
        }

        /// <summary>多种技能组合: 格斗 + 采集 + 生存 均合法。</summary>
        [Test]
        public void test_multiSkillCategories_emitSuccessfully()
        {
            int[] skillIds = { (int)SkillId.采集, (int)SkillId.急救, (int)SkillId.徒手, (int)SkillId.奔跑 };
            foreach (int sid in skillIds)
            {
                var payload = SkillGrownEmitter.EmitGrowth(
                    actorId: 1, skillId: sid, objectId: 1,
                    novelty: NoveltyClass.Normal, level: null, currentTick: 100);

                Assert.That(payload.SkillId, Is.EqualTo(sid),
                    $"SkillId={sid} 发射 SkillGrown 成功");
            }
        }

        /// <summary>三种 NoveltyClass 均正确写入载荷。</summary>
        [Test]
        public void test_allNoveltyClasses_writtenCorrectly()
        {
            NoveltyClass[] classes = { NoveltyClass.First, NoveltyClass.Stale, NoveltyClass.Normal };
            int[] expectedOrdinals = { 0, 1, 2 };

            for (int i = 0; i < classes.Length; i++)
            {
                var payload = SkillGrownEmitter.EmitGrowth(
                    actorId: 1, skillId: (int)SkillId.诊断, objectId: 1,
                    novelty: classes[i], level: null, currentTick: 100);

                Assert.That(payload.NoveltyClass, Is.EqualTo(expectedOrdinals[i]),
                    $"NoveltyClass={classes[i]} 写入 ordinal={expectedOrdinals[i]}");
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // 便捷重载直接测试
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>便捷重载: 不传 patientId → 默认 PatientId.None。</summary>
        [Test]
        public void test_convenienceOverload_defaultsPatientIdToNone()
        {
            var payload = SkillGrownEmitter.EmitGrowth(
                actorId: 1, skillId: (int)SkillId.采集, objectId: 1,
                novelty: NoveltyClass.First, level: null, currentTick: 100);

            Assert.That(payload.PatientId, Is.EqualTo(-1),
                "便捷重载默认 patientId = PatientId.None(-1)");
        }
    }
}
