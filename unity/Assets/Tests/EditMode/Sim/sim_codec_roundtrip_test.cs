// tests/EditMode/Sim/sim_codec_roundtrip_test.cs
//
// R-1(U0-b 残留)的可执行验收面。覆盖:
//   1. 34 支载荷 Encode→Decode 逐字段往返(病史 13 / 病例 5 / 世界 16);
//   2. SimEvent 头(G-1 权威序)+ **乱序容忍**(field-name 编码判据 —— 重排不改可解码性);
//   3. 严格性:未知 tag / 重复 tag / 缺字段 / 跨字段约束(EmergencyAttempt · Craft);
//   4. blob 池访问签名(拍板点 0-2):命中 / 未命中 / 越界;
//   5. freehand_text(G-3)UTF-8 往返 + sim 面读取禁令(非 Judgment Kind 调用即抛);
//   6. **D-21-18 探针**(ADR-006 §Validation):内置序列化器必须丢 Fix,
//      自定义编码器必须逐位还原 —— 这是 ADR-006 §五 点名的 EditMode 守卫;
//   7. 小端原语 vs **独立手写参考 loop** 对拍(b5 义务的迁移落点:参考 loop 自
//      sim_fixedpoint_test.cs 迁入本文件,种子测试自此零本地编码器);
//   8. 黄金字节夹具(ADR-010 §一「显式小端 + 黄金夹具钉死」)—— 期望值由 **独立 Python
//      实现**生成(非从本 C# 实现回读),按 ADR-012 版本化纪律:红了改实现,**禁改期望值**。
//
// 边界:跨平台逐位等价(Editor vs IL2CPP)不在本文件断言 —— 归 ADR-012 黄金矩阵,
//   本文件只钉「同一实现同一平台产字节稳定 + 与独立实现产字节一致」。

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Codec;
using UnityEngine;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    [TestFixture]
    internal sealed class SimCodecRoundtripTest
    {
        // ── 1. 病史流 13 支往返 ────────────────────────────────────────────

        [Test]
        public void test_simCodec_historyPayloads_roundTrip()
        {
            // Arrange / Act / Assert —— 每支取含负值 / 极值 / 定点的样本,逐字段断言。

            var injury = new InjuryOnsetPayload(1, 2, 3, new Fix(49152L), 1000L, 4);
            var dInjury = PayloadCodec.Decode<InjuryOnsetPayload>(
                EventKind.InjuryOnset, PayloadCodec.Encode(injury));
            Assert.That(dInjury.ActorId, Is.EqualTo(1), "InjuryOnset.ActorId");
            Assert.That(dInjury.TargetId, Is.EqualTo(2), "InjuryOnset.TargetId");
            Assert.That(dInjury.InjuryId, Is.EqualTo(3), "InjuryOnset.InjuryId");
            Assert.That(dInjury.Magnitude.Raw, Is.EqualTo(49152L), "InjuryOnset.Magnitude(Fix 逐位)");
            Assert.That(dInjury.Tick, Is.EqualTo(1000L), "InjuryOnset.Tick");
            Assert.That(dInjury.DoseSeq, Is.EqualTo(4), "InjuryOnset.DoseSeq");

            var compoundOn = new CompoundTriggeredPayload(100L, 5, 7, 11);
            var dCompoundOn = PayloadCodec.Decode<CompoundTriggeredPayload>(
                EventKind.CompoundTriggered, PayloadCodec.Encode(compoundOn));
            Assert.That(dCompoundOn.Tick, Is.EqualTo(100L), "CompoundTriggered.Tick");
            Assert.That(dCompoundOn.TargetId, Is.EqualTo(5), "CompoundTriggered.TargetId");
            Assert.That(dCompoundOn.RuleId, Is.EqualTo(7), "CompoundTriggered.RuleId");
            Assert.That(dCompoundOn.ComponentId, Is.EqualTo(11), "CompoundTriggered.ComponentId");

            var compoundOff = new CompoundExpiredPayload(120L, 5, 7);
            var dCompoundOff = PayloadCodec.Decode<CompoundExpiredPayload>(
                EventKind.CompoundExpired, PayloadCodec.Encode(compoundOff));
            Assert.That(dCompoundOff.Tick, Is.EqualTo(120L), "CompoundExpired.Tick");
            Assert.That(dCompoundOff.TargetId, Is.EqualTo(5), "CompoundExpired.TargetId");
            Assert.That(dCompoundOff.RuleId, Is.EqualTo(7), "CompoundExpired.RuleId");

            var care = new CareAppliedPayload(150L, 2, 999, 1);
            var dCare = PayloadCodec.Decode<CareAppliedPayload>(
                EventKind.CareApplied, PayloadCodec.Encode(care));
            Assert.That(dCare.Tick, Is.EqualTo(150L), "CareApplied.Tick");
            Assert.That(dCare.ActionId, Is.EqualTo(2), "CareApplied.ActionId");
            Assert.That(dCare.Caregiver, Is.EqualTo(999), "CareApplied.Caregiver");
            Assert.That(dCare.Phase, Is.EqualTo(1), "CareApplied.Phase");

            var attempt = new EmergencyAttemptPayload(1, 20, 3, 400, 350, 0, 9, new[] { 10, 25, 25 });
            var dAttempt = PayloadCodec.Decode<EmergencyAttemptPayload>(
                EventKind.EmergencyAttempt, PayloadCodec.Encode(attempt));
            Assert.That(dAttempt.Action, Is.EqualTo(1), "EmergencyAttempt.Action");
            Assert.That(dAttempt.HoldTicks, Is.EqualTo(20), "EmergencyAttempt.HoldTicks");
            Assert.That(dAttempt.Edges, Is.EqualTo(3), "EmergencyAttempt.Edges");
            Assert.That(dAttempt.MagPeak, Is.EqualTo(400), "EmergencyAttempt.MagPeak");
            Assert.That(dAttempt.MagLast, Is.EqualTo(350), "EmergencyAttempt.MagLast");
            Assert.That(dAttempt.Method, Is.EqualTo(0), "EmergencyAttempt.Method");
            Assert.That(dAttempt.ActorId, Is.EqualTo(9), "EmergencyAttempt.ActorId");
            CollectionAssert.AreEqual(new[] { 10, 25, 25 }, dAttempt.EdgeTicks,
                "EmergencyAttempt.EdgeTicks(数组逐元素)");

            var emergencyTx = new EmergencyTreatmentAppliedPayload(
                200L, 4, 999, -1, new Fix(32768L), 40L, 1, 2, 77L);
            var dEmergencyTx = PayloadCodec.Decode<EmergencyTreatmentAppliedPayload>(
                EventKind.EmergencyTreatmentApplied, PayloadCodec.Encode(emergencyTx));
            Assert.That(dEmergencyTx.Tick, Is.EqualTo(200L), "EmergencyTreatmentApplied.Tick");
            Assert.That(dEmergencyTx.TreatmentId, Is.EqualTo(4), "EmergencyTreatmentApplied.TreatmentId");
            Assert.That(dEmergencyTx.ActorId, Is.EqualTo(999), "EmergencyTreatmentApplied.ActorId");
            Assert.That(dEmergencyTx.Polarity, Is.EqualTo(-1), "EmergencyTreatmentApplied.Polarity(负值)");
            Assert.That(dEmergencyTx.DrugPotency.Raw, Is.EqualTo(32768L), "EmergencyTreatmentApplied.DrugPotency");
            Assert.That(dEmergencyTx.HalfLife, Is.EqualTo(40L), "EmergencyTreatmentApplied.HalfLife");
            Assert.That(dEmergencyTx.Method, Is.EqualTo(1), "EmergencyTreatmentApplied.Method");
            Assert.That(dEmergencyTx.Cause, Is.EqualTo(2), "EmergencyTreatmentApplied.Cause");
            Assert.That(dEmergencyTx.Seq, Is.EqualTo(77L), "EmergencyTreatmentApplied.Seq");

            var drugTx = new DrugTreatmentAppliedPayload(210L, 5, 999, 1, new Fix(49152L), 60L, 78L);
            var dDrugTx = PayloadCodec.Decode<DrugTreatmentAppliedPayload>(
                EventKind.DrugTreatmentApplied, PayloadCodec.Encode(drugTx));
            Assert.That(dDrugTx.Tick, Is.EqualTo(210L), "DrugTreatmentApplied.Tick");
            Assert.That(dDrugTx.TreatmentId, Is.EqualTo(5), "DrugTreatmentApplied.TreatmentId");
            Assert.That(dDrugTx.ActorId, Is.EqualTo(999), "DrugTreatmentApplied.ActorId");
            Assert.That(dDrugTx.Polarity, Is.EqualTo(1), "DrugTreatmentApplied.Polarity");
            Assert.That(dDrugTx.DrugPotency.Raw, Is.EqualTo(49152L), "DrugTreatmentApplied.DrugPotency");
            Assert.That(dDrugTx.HalfLife, Is.EqualTo(60L), "DrugTreatmentApplied.HalfLife");
            Assert.That(dDrugTx.Seq, Is.EqualTo(78L), "DrugTreatmentApplied.Seq");

            var skill = new SkillGrownPayload(1, -1, 3, 12, 0, 5);
            var dSkill = PayloadCodec.Decode<SkillGrownPayload>(
                EventKind.SkillGrown, PayloadCodec.Encode(skill));
            Assert.That(dSkill.ActorId, Is.EqualTo(1), "SkillGrown.ActorId");
            Assert.That(dSkill.PatientId, Is.EqualTo(-1), "SkillGrown.PatientId(None 哨兵)");
            Assert.That(dSkill.SkillId, Is.EqualTo(3), "SkillGrown.SkillId");
            Assert.That(dSkill.ObjectId, Is.EqualTo(12), "SkillGrown.ObjectId");
            Assert.That(dSkill.NoveltyClass, Is.EqualTo(0), "SkillGrown.NoveltyClass");
            Assert.That(dSkill.Level, Is.EqualTo(5), "SkillGrown.Level");

            var rolled = new EventRolledPayload(2, 1, 1234567890123L, 15);
            var dRolled = PayloadCodec.Decode<EventRolledPayload>(
                EventKind.EventRolled, PayloadCodec.Encode(rolled));
            Assert.That(dRolled.Win, Is.EqualTo(2), "EventRolled.Win");
            Assert.That(dRolled.Tier, Is.EqualTo(1), "EventRolled.Tier");
            Assert.That(dRolled.Ordinal, Is.EqualTo(1234567890123L), "EventRolled.Ordinal");
            Assert.That(dRolled.ChosenKey, Is.EqualTo(15), "EventRolled.ChosenKey");

            var arrived = new EventArrivedPayload(9, 2, new WorldPos(10, 0, -3), -1);
            var dArrived = PayloadCodec.Decode<EventArrivedPayload>(
                EventKind.EventArrived, PayloadCodec.Encode(arrived));
            Assert.That(dArrived.EventKey, Is.EqualTo(9), "EventArrived.EventKey");
            Assert.That(dArrived.Tier, Is.EqualTo(2), "EventArrived.Tier");
            Assert.That(dArrived.SpawnAnchor.X, Is.EqualTo(10), "EventArrived.SpawnAnchor.X");
            Assert.That(dArrived.SpawnAnchor.Y, Is.EqualTo(0), "EventArrived.SpawnAnchor.Y");
            Assert.That(dArrived.SpawnAnchor.Z, Is.EqualTo(-3), "EventArrived.SpawnAnchor.Z(负格)");
            Assert.That(dArrived.CauseClueKey, Is.EqualTo(-1), "EventArrived.CauseClueKey(哨兵)");

            var deferred = new ThreatDeferredPayload(0, 9);
            var dDeferred = PayloadCodec.Decode<ThreatDeferredPayload>(
                EventKind.ThreatDeferred, PayloadCodec.Encode(deferred));
            Assert.That(dDeferred.SlotIndex, Is.EqualTo(0), "ThreatDeferred.SlotIndex");
            Assert.That(dDeferred.EventKey, Is.EqualTo(9), "ThreatDeferred.EventKey");

            var cleared = new ThreatDeferralClearedPayload(1, 0);
            var dCleared = PayloadCodec.Decode<ThreatDeferralClearedPayload>(
                EventKind.ThreatDeferralCleared, PayloadCodec.Encode(cleared));
            Assert.That(dCleared.SlotIndex, Is.EqualTo(1), "ThreatDeferralCleared.SlotIndex");
            Assert.That(dCleared.Reason, Is.EqualTo(0), "ThreatDeferralCleared.Reason");

            var flag = new HistoryFlagChangedPayload(6, 1);
            var dFlag = PayloadCodec.Decode<HistoryFlagChangedPayload>(
                EventKind.HistoryFlagChanged, PayloadCodec.Encode(flag));
            Assert.That(dFlag.FlagId, Is.EqualTo(6), "HistoryFlagChanged.FlagId");
            Assert.That(dFlag.NewValue, Is.EqualTo(1), "HistoryFlagChanged.NewValue");
        }

        // ── 2. 病例流 5 支往返 ────────────────────────────────────────────

        [Test]
        public void test_simCodec_casePayloads_roundTrip()
        {
            var opened = new CaseOpenedPayload(3, new DiseaseIdSet(0b101UL));
            var dOpened = PayloadCodec.Decode<CaseOpenedPayload>(
                EventKind.CaseOpened, PayloadCodec.Encode(opened));
            Assert.That(dOpened.PatientId, Is.EqualTo(3), "CaseOpened.PatientId");
            Assert.That(dOpened.DiseaseSnapshot.Bits, Is.EqualTo(0b101UL), "CaseOpened.DiseaseSnapshot.Bits");

            var closed = new CaseClosedPayload(
                3, new CaseId(50L, 3, 9L), new DiseaseIdSet(5UL), true);
            var dClosed = PayloadCodec.Decode<CaseClosedPayload>(
                EventKind.CaseClosed, PayloadCodec.Encode(closed));
            Assert.That(dClosed.PatientId, Is.EqualTo(3), "CaseClosed.PatientId");
            Assert.That(dClosed.CaseId.Tick, Is.EqualTo(50L), "CaseClosed.CaseId.Tick");
            Assert.That(dClosed.CaseId.Patient, Is.EqualTo(3), "CaseClosed.CaseId.Patient");
            Assert.That(dClosed.CaseId.Seq, Is.EqualTo(9L), "CaseClosed.CaseId.Seq");
            Assert.That(dClosed.DiseaseSet.Bits, Is.EqualTo(5UL), "CaseClosed.DiseaseSet.Bits");
            Assert.That(dClosed.Treated, Is.True, "CaseClosed.Treated=true");

            var closedUntreated = new CaseClosedPayload(
                3, new CaseId(50L, 3, 9L), new DiseaseIdSet(5UL), false);
            var dClosedUntreated = PayloadCodec.Decode<CaseClosedPayload>(
                EventKind.CaseClosed, PayloadCodec.Encode(closedUntreated));
            Assert.That(dClosedUntreated.Treated, Is.False, "CaseClosed.Treated=false(不塌成 true)");

            var pattern = new PatternRecognizedPayload(
                -1, new CaseId(60L, -1, 3L), 0xDEADBEEFCAFEF00DUL, new DiseaseIdSet(0b1111UL), 2);
            var dPattern = PayloadCodec.Decode<PatternRecognizedPayload>(
                EventKind.PatternRecognized, PayloadCodec.Encode(pattern));
            Assert.That(dPattern.PatientId, Is.EqualTo(-1), "PatternRecognized.PatientId(None)");
            Assert.That(dPattern.AnchorCase.Tick, Is.EqualTo(60L), "PatternRecognized.AnchorCase.Tick");
            Assert.That(dPattern.AnchorCase.Patient, Is.EqualTo(-1), "PatternRecognized.AnchorCase.Patient");
            Assert.That(dPattern.AnchorCase.Seq, Is.EqualTo(3L), "PatternRecognized.AnchorCase.Seq");
            Assert.That(dPattern.SaltedKey, Is.EqualTo(0xDEADBEEFCAFEF00DUL), "PatternRecognized.SaltedKey(u64 全宽)");
            Assert.That(dPattern.MemberDiseaseSet.Bits, Is.EqualTo(0b1111UL), "PatternRecognized.MemberDiseaseSet.Bits");
            Assert.That(dPattern.Version, Is.EqualTo(2), "PatternRecognized.Version");

            var recorded = new JudgmentRecordedPayload(4, new CaseId(100L, 4, 2L), 1, 7, 2);
            var dRecorded = PayloadCodec.Decode<JudgmentRecordedPayload>(
                EventKind.JudgmentRecorded, PayloadCodec.Encode(recorded, ""));
            Assert.That(dRecorded.PatientId, Is.EqualTo(4), "JudgmentRecorded.PatientId");
            Assert.That(dRecorded.CaseId.Tick, Is.EqualTo(100L), "JudgmentRecorded.CaseId.Tick");
            Assert.That(dRecorded.CaseId.Patient, Is.EqualTo(4), "JudgmentRecorded.CaseId.Patient");
            Assert.That(dRecorded.CaseId.Seq, Is.EqualTo(2L), "JudgmentRecorded.CaseId.Seq");
            Assert.That(dRecorded.AuthorPlayerId, Is.EqualTo(1), "JudgmentRecorded.AuthorPlayerId");
            Assert.That(dRecorded.LexiconId, Is.EqualTo(7), "JudgmentRecorded.LexiconId");
            Assert.That(dRecorded.Confidence, Is.EqualTo((byte)2), "JudgmentRecorded.Confidence");

            var revised = new JudgmentRevisedPayload(4, new CaseId(100L, 4, 2L), 2, 8, 1);
            var dRevised = PayloadCodec.Decode<JudgmentRevisedPayload>(
                EventKind.JudgmentRevised, PayloadCodec.Encode(revised, ""));
            Assert.That(dRevised.PatientId, Is.EqualTo(4), "JudgmentRevised.PatientId");
            Assert.That(dRevised.CaseId.Tick, Is.EqualTo(100L), "JudgmentRevised.CaseId.Tick");
            Assert.That(dRevised.CaseId.Patient, Is.EqualTo(4), "JudgmentRevised.CaseId.Patient");
            Assert.That(dRevised.CaseId.Seq, Is.EqualTo(2L), "JudgmentRevised.CaseId.Seq");
            Assert.That(dRevised.AuthorPlayerId, Is.EqualTo(2), "JudgmentRevised.AuthorPlayerId");
            Assert.That(dRevised.LexiconId, Is.EqualTo(8), "JudgmentRevised.LexiconId");
            Assert.That(dRevised.Confidence, Is.EqualTo((byte)1), "JudgmentRevised.Confidence");
        }

        // ── 3. 世界流 16 支往返 ───────────────────────────────────────────

        [Test]
        public void test_simCodec_worldPayloads_roundTrip()
        {
            var cellEntered = new ActorCellEnteredPayload(1, new WorldPos(5, 1, -2), 300L);
            var dCellEntered = PayloadCodec.Decode<ActorCellEnteredPayload>(
                EventKind.ActorCellEntered, PayloadCodec.Encode(cellEntered));
            Assert.That(dCellEntered.ActorId, Is.EqualTo(1), "ActorCellEntered.ActorId");
            Assert.That(dCellEntered.Cell.X, Is.EqualTo(5), "ActorCellEntered.Cell.X");
            Assert.That(dCellEntered.Cell.Y, Is.EqualTo(1), "ActorCellEntered.Cell.Y");
            Assert.That(dCellEntered.Cell.Z, Is.EqualTo(-2), "ActorCellEntered.Cell.Z");
            Assert.That(dCellEntered.Tick, Is.EqualTo(300L), "ActorCellEntered.Tick");

            var harvested = new ResourceHarvestedPayload(1001L, 42, 3, 2, 1);
            var dHarvested = PayloadCodec.Decode<ResourceHarvestedPayload>(
                EventKind.ResourceHarvested, PayloadCodec.Encode(harvested));
            Assert.That(dHarvested.InstanceId, Is.EqualTo(1001L), "ResourceHarvested.InstanceId");
            Assert.That(dHarvested.NodeId, Is.EqualTo(42), "ResourceHarvested.NodeId");
            Assert.That(dHarvested.GatherSeq, Is.EqualTo(3), "ResourceHarvested.GatherSeq");
            Assert.That(dHarvested.Qty, Is.EqualTo(2), "ResourceHarvested.Qty");
            Assert.That(dHarvested.OutQuality, Is.EqualTo(1), "ResourceHarvested.OutQuality");

            var spawned = new DropSpawnedPayload(2002L, new WorldPos(0, 0, 0), 17, 4);
            var dSpawned = PayloadCodec.Decode<DropSpawnedPayload>(
                EventKind.DropSpawned, PayloadCodec.Encode(spawned));
            Assert.That(dSpawned.InstanceId, Is.EqualTo(2002L), "DropSpawned.InstanceId");
            Assert.That(dSpawned.SpawnAnchor.X, Is.EqualTo(0), "DropSpawned.SpawnAnchor.X");
            Assert.That(dSpawned.ItemKey, Is.EqualTo(17), "DropSpawned.ItemKey");
            Assert.That(dSpawned.Qty, Is.EqualTo(4), "DropSpawned.Qty");

            var claimed = new DropClaimedPayload(2002L, 1, 310L);
            var dClaimed = PayloadCodec.Decode<DropClaimedPayload>(
                EventKind.DropClaimed, PayloadCodec.Encode(claimed));
            Assert.That(dClaimed.InstanceId, Is.EqualTo(2002L), "DropClaimed.InstanceId");
            Assert.That(dClaimed.Claimer, Is.EqualTo(1), "DropClaimed.Claimer");
            Assert.That(dClaimed.Tick, Is.EqualTo(310L), "DropClaimed.Tick");

            var despawned = new DropDespawnedPayload(2002L, 4, 0);
            var dDespawned = PayloadCodec.Decode<DropDespawnedPayload>(
                EventKind.DropDespawned, PayloadCodec.Encode(despawned));
            Assert.That(dDespawned.InstanceId, Is.EqualTo(2002L), "DropDespawned.InstanceId");
            Assert.That(dDespawned.Qty, Is.EqualTo(4), "DropDespawned.Qty");
            Assert.That(dDespawned.Reason, Is.EqualTo(0), "DropDespawned.Reason");

            var craft = new CraftPayload(1, 6, 320L, 40L,
                new[] { 3001L, 3002L }, new[] { 1, 2 }, new[] { 3 },
                new[] { 1 }, new[] { 4001L }, new WorldPos(2, 2, 2));
            var dCraft = PayloadCodec.Decode<CraftPayload>(
                EventKind.Craft, PayloadCodec.Encode(craft));
            Assert.That(dCraft.ActorId, Is.EqualTo(1), "Craft.ActorId");
            Assert.That(dCraft.RecipeId, Is.EqualTo(6), "Craft.RecipeId");
            Assert.That(dCraft.StartTick, Is.EqualTo(320L), "Craft.StartTick");
            Assert.That(dCraft.DurationTicks, Is.EqualTo(40L), "Craft.DurationTicks");
            CollectionAssert.AreEqual(new[] { 3001L, 3002L }, dCraft.InputInstanceIds, "Craft.InputInstanceIds");
            CollectionAssert.AreEqual(new[] { 1, 2 }, dCraft.ActualConsumed, "Craft.ActualConsumed");
            CollectionAssert.AreEqual(new[] { 3 }, dCraft.OutputQty, "Craft.OutputQty");
            CollectionAssert.AreEqual(new[] { 1 }, dCraft.OutputQuality, "Craft.OutputQuality");
            CollectionAssert.AreEqual(new[] { 4001L }, dCraft.OutputInstanceIds, "Craft.OutputInstanceIds");
            Assert.That(dCraft.ToolCell.X, Is.EqualTo(2), "Craft.ToolCell.X");
            Assert.That(dCraft.ToolCell.Y, Is.EqualTo(2), "Craft.ToolCell.Y");
            Assert.That(dCraft.ToolCell.Z, Is.EqualTo(2), "Craft.ToolCell.Z");

            var placed = new StructurePlacedPayload(9001L, new WorldPos(7, 0, 7), 3, 90, 1);
            var dPlaced = PayloadCodec.Decode<StructurePlacedPayload>(
                EventKind.StructurePlaced, PayloadCodec.Encode(placed));
            Assert.That(dPlaced.StructureId, Is.EqualTo(9001L), "StructurePlaced.StructureId");
            Assert.That(dPlaced.Cell.X, Is.EqualTo(7), "StructurePlaced.Cell.X");
            Assert.That(dPlaced.ModuleId, Is.EqualTo(3), "StructurePlaced.ModuleId");
            Assert.That(dPlaced.Orientation, Is.EqualTo(90), "StructurePlaced.Orientation");
            Assert.That(dPlaced.Variant, Is.EqualTo(1), "StructurePlaced.Variant");

            var modified = new StructureModifiedPayload(9001L, new WorldPos(7, 0, 7), 3, 180, 2, 0b011);
            var dModified = PayloadCodec.Decode<StructureModifiedPayload>(
                EventKind.StructureModified, PayloadCodec.Encode(modified));
            Assert.That(dModified.StructureId, Is.EqualTo(9001L), "StructureModified.StructureId");
            Assert.That(dModified.Cell.X, Is.EqualTo(7), "StructureModified.Cell.X");
            Assert.That(dModified.ModuleId, Is.EqualTo(3), "StructureModified.ModuleId");
            Assert.That(dModified.NewOrientation, Is.EqualTo(180), "StructureModified.NewOrientation");
            Assert.That(dModified.NewVariant, Is.EqualTo(2), "StructureModified.NewVariant");
            Assert.That(dModified.ModifiedFields, Is.EqualTo(0b011), "StructureModified.ModifiedFields(位掩码)");

            var removed = new StructureRemovedPayload(9001L, new WorldPos(7, 0, 7), 3);
            var dRemoved = PayloadCodec.Decode<StructureRemovedPayload>(
                EventKind.StructureRemoved, PayloadCodec.Encode(removed));
            Assert.That(dRemoved.StructureId, Is.EqualTo(9001L), "StructureRemoved.StructureId");
            Assert.That(dRemoved.Cell.X, Is.EqualTo(7), "StructureRemoved.Cell.X");
            Assert.That(dRemoved.ModuleId, Is.EqualTo(3), "StructureRemoved.ModuleId");

            var poi = new PoiStateChangedPayload(12, 2);
            var dPoi = PayloadCodec.Decode<PoiStateChangedPayload>(
                EventKind.PoiStateChanged, PayloadCodec.Encode(poi));
            Assert.That(dPoi.PoiId, Is.EqualTo(12), "PoiStateChanged.PoiId");
            Assert.That(dPoi.NewState, Is.EqualTo(2), "PoiStateChanged.NewState");

            var enemyInjury = new EnemyInjuryOnsetPayload(50, 1, 2, new Fix(16384L), 400L, 1);
            var dEnemyInjury = PayloadCodec.Decode<EnemyInjuryOnsetPayload>(
                EventKind.EnemyInjuryOnset, PayloadCodec.Encode(enemyInjury));
            Assert.That(dEnemyInjury.ActorId, Is.EqualTo(50), "EnemyInjuryOnset.ActorId");
            Assert.That(dEnemyInjury.TargetId, Is.EqualTo(1), "EnemyInjuryOnset.TargetId");
            Assert.That(dEnemyInjury.InjuryId, Is.EqualTo(2), "EnemyInjuryOnset.InjuryId");
            Assert.That(dEnemyInjury.Magnitude.Raw, Is.EqualTo(16384L), "EnemyInjuryOnset.Magnitude");
            Assert.That(dEnemyInjury.Tick, Is.EqualTo(400L), "EnemyInjuryOnset.Tick");
            Assert.That(dEnemyInjury.DoseSeq, Is.EqualTo(1), "EnemyInjuryOnset.DoseSeq");

            var injuryState = new InjuryStateChangedPayload(50, 1);
            var dInjuryState = PayloadCodec.Decode<InjuryStateChangedPayload>(
                EventKind.InjuryStateChanged, PayloadCodec.Encode(injuryState));
            Assert.That(dInjuryState.ActorId, Is.EqualTo(50), "InjuryStateChanged.ActorId");
            Assert.That(dInjuryState.NewState, Is.EqualTo(1), "InjuryStateChanged.NewState");

            var encounterStarted = new EncounterStartedPayload(3, 8, new WorldPos(-1, 5, 9), new[] { 70, 71 });
            var dEncounterStarted = PayloadCodec.Decode<EncounterStartedPayload>(
                EventKind.EncounterStarted, PayloadCodec.Encode(encounterStarted));
            Assert.That(dEncounterStarted.EncounterId, Is.EqualTo(3), "EncounterStarted.EncounterId");
            Assert.That(dEncounterStarted.ProtoId, Is.EqualTo(8), "EncounterStarted.ProtoId");
            Assert.That(dEncounterStarted.SpawnCell.X, Is.EqualTo(-1), "EncounterStarted.SpawnCell.X");
            CollectionAssert.AreEqual(new[] { 70, 71 }, dEncounterStarted.ActorIds, "EncounterStarted.ActorIds");

            var encounterEnded = new EncounterEndedPayload(3, 1);
            var dEncounterEnded = PayloadCodec.Decode<EncounterEndedPayload>(
                EventKind.EncounterEnded, PayloadCodec.Encode(encounterEnded));
            Assert.That(dEncounterEnded.EncounterId, Is.EqualTo(3), "EncounterEnded.EncounterId");
            Assert.That(dEncounterEnded.Reason, Is.EqualTo(1), "EncounterEnded.Reason");

            var consequence = new ConsequenceResolvedPayload(2, 4);
            var dConsequence = PayloadCodec.Decode<ConsequenceResolvedPayload>(
                EventKind.ConsequenceResolved, PayloadCodec.Encode(consequence));
            Assert.That(dConsequence.Outcome, Is.EqualTo(2), "ConsequenceResolved.Outcome");
            Assert.That(dConsequence.PatientId, Is.EqualTo(4), "ConsequenceResolved.PatientId");

            var died = new PlayerDiedPayload(1, new WorldPos(9, 9, 9), 500L);
            var dDied = PayloadCodec.Decode<PlayerDiedPayload>(
                EventKind.PlayerDied, PayloadCodec.Encode(died));
            Assert.That(dDied.ActorId, Is.EqualTo(1), "PlayerDied.ActorId");
            Assert.That(dDied.DeathCell.X, Is.EqualTo(9), "PlayerDied.DeathCell.X");
            Assert.That(dDied.DeathCell.Y, Is.EqualTo(9), "PlayerDied.DeathCell.Y");
            Assert.That(dDied.DeathCell.Z, Is.EqualTo(9), "PlayerDied.DeathCell.Z");
            Assert.That(dDied.Tick, Is.EqualTo(500L), "PlayerDied.Tick");
        }

        // ── 4-5. SimEvent 头往返 + 乱序容忍 ───────────────────────────────

        [Test]
        public void test_simCodec_simEventHeader_roundTrip()
        {
            var e = new SimEvent(100L, new PatientId(7), 5L, EventKind.SkillGrown,
                new PayloadRef(0, 0, 30));
            var d = SimEventCodec.Decode(SimEventCodec.Encode(e));

            Assert.That(d.Tick, Is.EqualTo(100L), "SimEvent.Tick");
            Assert.That(d.Patient.Value, Is.EqualTo(7), "SimEvent.Patient.Value");
            Assert.That(d.Seq, Is.EqualTo(5L), "SimEvent.Seq");
            Assert.That(d.Kind, Is.EqualTo(EventKind.SkillGrown), "SimEvent.Kind");
            Assert.That(d.Payload.BlobId, Is.EqualTo(0), "SimEvent.Payload.BlobId");
            Assert.That(d.Payload.Offset, Is.EqualTo(0), "SimEvent.Payload.Offset");
            Assert.That(d.Payload.Length, Is.EqualTo(30), "SimEvent.Payload.Length");
        }

        [Test]
        public void test_simCodec_simEvent_shuffledFieldOrder_decodes()
        {
            // Arrange —— 非 canonical 顺序写同一头(字段名编码判据:顺序无关可解码性)。
            var w = new CodecWriter();
            w.WriteFieldInt64(3, 5L);                         // Seq 先写
            w.WriteFieldInt64(1, 100L);                       // Tick 次之
            w.WriteFieldInt32(4, (int)EventKind.SkillGrown);  // Kind 第四
            w.WriteFieldPayloadRef(5, new PayloadRef(0, 0, 30));
            w.WriteFieldInt32(2, 7);                          // Patient 最后

            // Act
            var d = SimEventCodec.Decode(w.ToArray());

            // Assert
            Assert.That(d.Tick, Is.EqualTo(100L), "乱序头.Tick");
            Assert.That(d.Patient.Value, Is.EqualTo(7), "乱序头.Patient");
            Assert.That(d.Seq, Is.EqualTo(5L), "乱序头.Seq");
            Assert.That(d.Kind, Is.EqualTo(EventKind.SkillGrown), "乱序头.Kind");
            Assert.That(d.Payload.Length, Is.EqualTo(30), "乱序头.Payload.Length");
        }

        // ── 6-8. 严格性:未知 / 重复 / 缺字段 ─────────────────────────────

        [Test]
        public void test_simCodec_unknownTag_throws()
        {
            byte[] canonical = PayloadCodec.Encode(new SkillGrownPayload(1, -1, 3, 12, 0, 5));
            var corrupted = new byte[canonical.Length + 1];
            Array.Copy(canonical, corrupted, canonical.Length);
            corrupted[canonical.Length] = 0x50;               // 远超 1..31 之外?0x50=80 > 31 ⇒ 越界;二者皆须拒

            Assert.Throws<InvalidDataException>(
                () => PayloadCodec.Decode<SkillGrownPayload>(EventKind.SkillGrown, corrupted),
                "未知/越界 tag 必须拒收,不得静默跳过");
        }

        [Test]
        public void test_simCodec_duplicateTag_throws()
        {
            var w = new CodecWriter();
            w.WriteFieldInt32(1, 1);
            w.WriteFieldInt32(2, -1);
            w.WriteFieldInt32(3, 3);
            w.WriteFieldInt32(4, 12);
            w.WriteFieldInt32(5, 0);
            w.WriteFieldInt32(6, 5);
            w.WriteFieldInt32(1, 2);                          // tag 1 写第二次

            Assert.Throws<InvalidDataException>(
                () => PayloadCodec.Decode<SkillGrownPayload>(EventKind.SkillGrown, w.ToArray()),
                "重复 tag = 同一字段两份真相,必须拒收");
        }

        [Test]
        public void test_simCodec_missingField_throws()
        {
            byte[] canonical = PayloadCodec.Encode(new InjuryOnsetPayload(
                1, 2, 3, new Fix(49152L), 1000L, 4));
            byte[] truncated = new byte[canonical.Length - 5]; // 末字段 DoseSeq = tag(1B)+ i32(4B)
            Array.Copy(canonical, truncated, truncated.Length);

            Assert.Throws<InvalidDataException>(
                () => PayloadCodec.Decode<InjuryOnsetPayload>(EventKind.InjuryOnset, truncated),
                "缺字段(掩码不符)必须拒收 —— 不得用 default 顶上");
        }

        // ── 9-11. 跨字段约束双侧拒收 ──────────────────────────────────────

        [Test]
        public void test_simCodec_emergencyAttemptEdgesMismatch_encodeThrows()
        {
            var bad = new EmergencyAttemptPayload(1, 20, /*Edges*/ 2, 400, 350, 0, 9,
                new[] { 10, 25, 25 });                        // 长度 3 ≠ Edges 2

            Assert.Throws<ArgumentException>(
                () => PayloadCodec.Encode(bad),
                "Edges 与 EdgeTicks.Length 不符 —— 写侧拒收,坏数据不进字节面");
        }

        [Test]
        public void test_simCodec_emergencyAttemptEdgesMismatch_decodeThrows()
        {
            byte[] canonical = PayloadCodec.Encode(new EmergencyAttemptPayload(
                1, 20, 3, 400, 350, 0, 9, new[] { 10, 25, 25 }));
            // 布局:tag1@0(5B)→ tag2@5(5B)→ tag3@10:tag 字节 @10,值 @11..14
            byte[] corrupted = (byte[])canonical.Clone();
            corrupted[11] = 99; corrupted[12] = 0; corrupted[13] = 0; corrupted[14] = 0;

            Assert.Throws<InvalidDataException>(
                () => PayloadCodec.Decode<EmergencyAttemptPayload>(EventKind.EmergencyAttempt, corrupted),
                "读侧复验 Edges == EdgeTicks.Length —— 手改 Edges 必须被抓");
        }

        [Test]
        public void test_simCodec_craftArrayLengthMismatch_encodeThrows()
        {
            var bad = new CraftPayload(1, 6, 320L, 40L,
                new[] { 3001L, 3002L },                       // 输入组:输入实例 2
                new[] { 1 },                                  // 实耗 1 ⇒ 输入组不等长
                new[] { 3 }, new[] { 1 }, new[] { 4001L },
                new WorldPos(2, 2, 2));

            Assert.Throws<ArgumentException>(
                () => PayloadCodec.Encode(bad),
                "Craft 输入组等长对位(输入实例 ⟷ 实耗)—— 写侧拒收");
        }

        // ── 12-13. freehand_text(G-3)+ sim 面禁令 ────────────────────────

        [Test]
        public void test_simCodec_freehandUtf8_roundTrip()
        {
            var p = new JudgmentRecordedPayload(4, new CaseId(100L, 4, 2L), 1, 7, 2);
            const string text = "脉数浮滑,右关尤甚";
            byte[] bytes = PayloadCodec.Encode(p, text);
            var pool = new StubBlobPool();
            pool.Add(0, bytes);
            var e = new SimEvent(100L, new PatientId(4), 2L, EventKind.JudgmentRecorded,
                new PayloadRef(0, 0, bytes.Length));

            // sim 面:结构体照常解出(无 freehand 字段)
            Assert.That(PayloadCodec.TryGetPayload<JudgmentRecordedPayload>(e, pool, out var simFace), Is.True,
                "池命中 ⇒ 解码成功");
            Assert.That(simFace.LexiconId, Is.EqualTo(7), "sim 面 LexiconId");
            Assert.That(simFace.Confidence, Is.EqualTo((byte)2), "sim 面 Confidence");

            // 呈现面:同一段字节另走 TryGetFreehandText 取回原文
            Assert.That(PayloadCodec.TryGetFreehandText(e, pool, out string got), Is.True, "池命中");
            Assert.That(got, Is.EqualTo(text), "freehand_text 中文 UTF-8 逐字往返");

            // 空文合法(= 空栏)
            byte[] emptyBytes = PayloadCodec.Encode(p, "");
            var pool2 = new StubBlobPool();
            pool2.Add(1, emptyBytes);
            var e2 = new SimEvent(100L, new PatientId(4), 3L, EventKind.JudgmentRecorded,
                new PayloadRef(1, 0, emptyBytes.Length));
            Assert.That(PayloadCodec.TryGetFreehandText(e2, pool2, out string gotEmpty), Is.True, "空文命中");
            Assert.That(gotEmpty, Is.EqualTo(""), "空栏 ⇒ 空串(非 null)");
        }

        [Test]
        public void test_simCodec_tryGetFreehand_wrongKind_throws()
        {
            byte[] bytes = PayloadCodec.Encode(new SkillGrownPayload(1, -1, 3, 12, 0, 5));
            var pool = new StubBlobPool();
            pool.Add(0, bytes);
            var e = new SimEvent(1L, new PatientId(-1), 1L, EventKind.SkillGrown,
                new PayloadRef(0, 0, bytes.Length));

            Assert.Throws<InvalidOperationException>(
                () => PayloadCodec.TryGetFreehandText(e, pool, out _),
                "非 Judgment Kind 上取自由文本 = 编程错误(违例纪律),必须抛不得返回空");
        }

        // ── 14-15. blob 池访问签名(拍板点 0-2)───────────────────────────

        [Test]
        public void test_simCodec_tryGetPayload_missingBlob_returnsFalse()
        {
            var pool = new StubBlobPool();                   // 空池
            var e = new SimEvent(1L, new PatientId(1), 1L, EventKind.SkillGrown,
                new PayloadRef(/*BlobId*/ 42, 0, 30));

            Assert.That(PayloadCodec.TryGetPayload<SkillGrownPayload>(e, pool, out _), Is.False,
                "池未命中 ⇒ false(宽容,由调用方降级),不得抛");
        }

        [Test]
        public void test_simCodec_tryGetPayload_outOfBounds_returnsFalse()
        {
            var pool = new StubBlobPool();
            pool.Add(0, new byte[10]);
            var e = new SimEvent(1L, new PatientId(1), 1L, EventKind.SkillGrown,
                new PayloadRef(0, /*Offset*/ 8, /*Length*/ 5));   // 8+5=13 > 10

            Assert.That(PayloadCodec.TryGetPayload<SkillGrownPayload>(e, pool, out _), Is.False,
                "引用越界 ⇒ false,不得切片抛出");
        }

        // ── 16. D-21-18 探针(ADR-006 §Validation 点名的 EditMode 守卫)────

        [Test]
        public void test_simCodec_fixEncoder_bypassesUnitySerializer()
        {
            // Arrange
            const long originalRaw = 49152L;                 // 3/4
            var original = new Fix(originalRaw);

            // Act A —— Unity 内置序列化器路径
            var carrier = new FixCarrier { value = original };
            string json = JsonUtility.ToJson(carrier);
            var restored = JsonUtility.FromJson<FixCarrier>(json);
            long viaUnity = restored != null ? restored.value.Raw : 0L;

            // Act B —— Sim.Codec 自定义编码器路径
            var w = new CodecWriter();
            w.WriteFieldFix(1, original);
            byte[] bytes = w.ToArray();
            var r = new CodecReader(bytes);
            byte tag = r.ReadTag();
            Fix viaCodec = r.ReadFieldFix();

            // Assert —— 立论的两半都必须成立
            Assert.That(viaUnity, Is.Not.EqualTo(originalRaw),
                "内置序列化器**必须**丢 Fix(D-21-18 立论);若本断言红 = Unity 行为变了,重读 ADR-006 §五");
            Assert.That(tag, Is.EqualTo(1), "自定义编码器 tag 落笔");
            Assert.That(viaCodec.Raw, Is.EqualTo(originalRaw),
                "自定义编码器必须逐位还原 Fix —— 丢值 = 「这剂药没效」静默失效");
            Assert.That(bytes.Length, Is.EqualTo(9), "tag(1B)+ Fix(8B)定宽");
        }

        [Serializable]
        private sealed class FixCarrier
        {
            public Fix value;
        }

        // ── 17. 小端原语 vs 独立手写参考 loop(b5 迁移落点)────────────────

        [TestCase(0L)]
        [TestCase(1L)]
        [TestCase(-1L)]
        [TestCase(49152L)]
        [TestCase(unchecked((long)0xDEADBEEFCAFEF00DUL))]
        [TestCase(long.MinValue)]
        [TestCase(long.MaxValue)]
        public void test_simCodec_lePrimitives_matchIndependentReference(long value)
        {
            // 生产件
            byte[] production = new byte[8];
            CodecPrimitives.WriteInt64LittleEndian(production, value);
            long restoredProduction = CodecPrimitives.ReadInt64LittleEndian(production);

            // 独立手写参考(自 sim_fixedpoint_test.cs 迁入 —— b5 义务:与生产件对拍)
            byte[] reference = ReferenceWriteInt64LittleEndian(value);
            long restoredReference = ReferenceReadInt64LittleEndian(reference);

            CollectionAssert.AreEqual(reference, production,
                $"生产件与独立参考的 8 字节不一致(value={value})—— 端序或位移有分歧");
            Assert.That(restoredProduction, Is.EqualTo(value), "生产件自往返");
            Assert.That(restoredReference, Is.EqualTo(value), "参考自往返(对照)");
        }

        private static byte[] ReferenceWriteInt64LittleEndian(long v)
        {
            var b = new byte[8];
            for (int i = 0; i < 8; i++) b[i] = (byte)((v >> (8 * i)) & 0xFF);
            return b;
        }

        private static long ReferenceReadInt64LittleEndian(byte[] b)
        {
            long v = 0;
            for (int i = 0; i < 8; i++) v |= ((long)b[i]) << (8 * i);
            return v;
        }

        // ── 18-23. 黄金字节夹具(独立 Python 实现产出;禁改期望值)──────────

        [Test]
        public void test_simCodec_simEventHeader_matchesGoldenBytes()
        {
            var e = new SimEvent(100L, new PatientId(7), 5L, EventKind.SkillGrown,
                new PayloadRef(0, 0, 30));
            AssertGolden(
                "016400000000000000020700000003050000000000000004070000000500000000000000001e000000",
                SimEventCodec.Encode(e),
                "SimEvent 头(G-1 序)黄金字节");
        }

        [Test]
        public void test_simCodec_injuryOnset_matchesGoldenBytes()
        {
            var p = new InjuryOnsetPayload(1, 2, 3, new Fix(49152L), 1000L, 4);
            AssertGolden(
                "0101000000020200000003030000000400c000000000000005e8030000000000000604000000",
                PayloadCodec.Encode(p),
                "InjuryOnset(含 Fix)黄金字节");
        }

        [Test]
        public void test_simCodec_emergencyAttempt_matchesGoldenBytes()
        {
            var p = new EmergencyAttemptPayload(1, 20, 3, 400, 350, 0, 9, new[] { 10, 25, 25 });
            AssertGolden(
                "0101000000021400000003030000000490010000055e0100000600000000070900000008030000000a0000001900000019000000",
                PayloadCodec.Encode(p),
                "EmergencyAttempt(含 int 数组)黄金字节");
        }

        [Test]
        public void test_simCodec_skillGrown_matchesGoldenBytes()
        {
            var p = new SkillGrownPayload(1, -1, 3, 12, 0, 5);
            AssertGolden(
                "010100000002ffffffff0303000000040c00000005000000000605000000",
                PayloadCodec.Encode(p),
                "SkillGrown(含 PatientId=-1)黄金字节");
        }

        [Test]
        public void test_simCodec_caseClosed_matchesGoldenBytes()
        {
            var p = new CaseClosedPayload(3, new CaseId(50L, 3, 9L), new DiseaseIdSet(5UL), true);
            AssertGolden(
                "01030000000232000000000000000300000009000000000000000305000000000000000401",
                PayloadCodec.Encode(p),
                "CaseClosed(含 CaseId 内联 + bool)黄金字节");
        }

        [Test]
        public void test_simCodec_judgmentRecorded_matchesGoldenBytes()
        {
            var p = new JudgmentRecordedPayload(4, new CaseId(100L, 4, 2L), 1, 7, 2);
            AssertGolden(
                "0104000000026400000000000000040000000200000000000000030100000004070000000502060c000000e88489e695b0e6b5aee6bb91",
                PayloadCodec.Encode(p, "脉数浮滑"),
                "JudgmentRecorded(含 freehand UTF-8 变长段)黄金字节");
        }

        private static void AssertGolden(string expectedHex, byte[] actual, string what)
        {
            byte[] expected = Hex(expectedHex);
            Assert.That(actual.Length, Is.EqualTo(expected.Length),
                $"{what}:长度变了 —— 红了改**实现**,禁改期望值(ADR-012 版本化纪律)");
            CollectionAssert.AreEqual(expected, actual,
                $"{what}:逐字节不符 —— 红了改**实现**,禁改期望值(ADR-012 版本化纪律)");
        }

        private static byte[] Hex(string s)
        {
            var result = new byte[s.Length / 2];
            for (int i = 0; i < result.Length; i++)
                result[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
            return result;
        }

        // ── 24. 头侧引用非法值 ────────────────────────────────────────────

        [Test]
        public void test_simCodec_negativePayloadRef_throws()
        {
            var e = new SimEvent(1L, new PatientId(1), 1L, EventKind.SkillGrown,
                new PayloadRef(0, /*Offset*/ -1, 4));
            byte[] bytes = SimEventCodec.Encode(e);

            Assert.Throws<InvalidDataException>(
                () => SimEventCodec.Decode(bytes),
                "PayloadRef.Offset < 0 无切片语义 —— 头解码必须拒收");
        }

        // ── 测试桩 ────────────────────────────────────────────────────────

        private sealed class StubBlobPool : IBlobPool
        {
            private readonly Dictionary<int, byte[]> _blobs = new Dictionary<int, byte[]>();

            public void Add(int blobId, byte[] bytes) => _blobs[blobId] = bytes;

            public bool TryGetBlob(int blobId, out ReadOnlyMemory<byte> blob)
            {
                if (_blobs.TryGetValue(blobId, out var bytes)) { blob = bytes; return true; }
                blob = default;
                return false;
            }
        }
    }
}
