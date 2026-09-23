// R-1 · 世界流 16 支。schema 源 = entities.yaml payload_schema(ADR-024 §① 真源)。
// Craft 跨字段约束 = 两组等长(写侧拒 / 读侧拒):
//   输入组 |InputInstanceIds| == |ActualConsumed|(逐被点名实例对位 —— 20 的 fold 据此逐实例扣);
//   产出组 |OutputQty| == |OutputQuality| == |OutputInstanceIds|(逐 outputs 行对位);
//   组间不等长(n ⊥ m —— item-database D-21-5:炮制 = n=1,m=1 特例,制作 = n>1)。
// 出处 = processing.md R-18-A 载荷块 + item-database F1/F2;registry constraint 无等长条目。

using System;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    public static partial class PayloadCodec
    {
        // ── 19. ActorCellEntered ──────────────────────────────────────────
        // tag: 1 ActorId · 2 Cell(WorldPos)· 3 Tick

        public static byte[] Encode(in ActorCellEnteredPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.ActorId);
            w.WriteFieldWorldPos(2, p.Cell);
            w.WriteFieldInt64(3, p.Tick);
            return w.ToArray();
        }

        private static ActorCellEnteredPayload DecodeActorCellEntered(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int actorId = 0;
            WorldPos cell = default;
            long tick = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.ActorCellEntered), ref seen))
                {
                    case 1: actorId = r.ReadInt32LittleEndian(); break;
                    case 2: cell = r.ReadFieldWorldPos(); break;
                    case 3: tick = r.ReadInt64LittleEndian(); break;
                    default: throw new InvalidDataException($"ActorCellEntered: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.ActorCellEntered), seen, (1u << 3) - 1);
            r.EnsureFullyConsumed();
            return new ActorCellEnteredPayload(actorId, cell, tick);
        }

        // ── 20. ResourceHarvested(17 采集)────────────────────────────────
        // tag: 1 InstanceId · 2 NodeId · 3 GatherSeq · 4 Qty · 5 OutQuality

        public static byte[] Encode(in ResourceHarvestedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt64(1, p.InstanceId);
            w.WriteFieldInt32(2, p.NodeId);
            w.WriteFieldInt32(3, p.GatherSeq);
            w.WriteFieldInt32(4, p.Qty);
            w.WriteFieldInt32(5, p.OutQuality);
            return w.ToArray();
        }

        private static ResourceHarvestedPayload DecodeResourceHarvested(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            long instanceId = 0;
            int nodeId = 0, gatherSeq = 0, qty = 0, outQuality = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.ResourceHarvested), ref seen))
                {
                    case 1: instanceId = r.ReadInt64LittleEndian(); break;
                    case 2: nodeId = r.ReadInt32LittleEndian(); break;
                    case 3: gatherSeq = r.ReadInt32LittleEndian(); break;
                    case 4: qty = r.ReadInt32LittleEndian(); break;
                    case 5: outQuality = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException($"ResourceHarvested: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.ResourceHarvested), seen, (1u << 5) - 1);
            r.EnsureFullyConsumed();
            return new ResourceHarvestedPayload(instanceId, nodeId, gatherSeq, qty, outQuality);
        }

        // ── 21. DropSpawned(ADR-009 §七)──────────────────────────────────
        // tag: 1 InstanceId · 2 SpawnAnchor · 3 ItemKey · 4 Qty

        public static byte[] Encode(in DropSpawnedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt64(1, p.InstanceId);
            w.WriteFieldWorldPos(2, p.SpawnAnchor);
            w.WriteFieldInt32(3, p.ItemKey);
            w.WriteFieldInt32(4, p.Qty);
            return w.ToArray();
        }

        private static DropSpawnedPayload DecodeDropSpawned(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            long instanceId = 0;
            WorldPos spawnAnchor = default;
            int itemKey = 0, qty = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.DropSpawned), ref seen))
                {
                    case 1: instanceId = r.ReadInt64LittleEndian(); break;
                    case 2: spawnAnchor = r.ReadFieldWorldPos(); break;
                    case 3: itemKey = r.ReadInt32LittleEndian(); break;
                    case 4: qty = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException($"DropSpawned: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.DropSpawned), seen, (1u << 4) - 1);
            r.EnsureFullyConsumed();
            return new DropSpawnedPayload(instanceId, spawnAnchor, itemKey, qty);
        }

        // ── 22. DropClaimed(ADR-009 §七)──────────────────────────────────
        // tag: 1 InstanceId · 2 Claimer · 3 Tick

        public static byte[] Encode(in DropClaimedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt64(1, p.InstanceId);
            w.WriteFieldInt32(2, p.Claimer);
            w.WriteFieldInt64(3, p.Tick);
            return w.ToArray();
        }

        private static DropClaimedPayload DecodeDropClaimed(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            long instanceId = 0, tick = 0;
            int claimer = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.DropClaimed), ref seen))
                {
                    case 1: instanceId = r.ReadInt64LittleEndian(); break;
                    case 2: claimer = r.ReadInt32LittleEndian(); break;
                    case 3: tick = r.ReadInt64LittleEndian(); break;
                    default: throw new InvalidDataException($"DropClaimed: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.DropClaimed), seen, (1u << 3) - 1);
            r.EnsureFullyConsumed();
            return new DropClaimedPayload(instanceId, claimer, tick);
        }

        // ── 23. DropDespawned(ADR-009 §七)────────────────────────────────
        // tag: 1 InstanceId · 2 Qty · 3 Reason

        public static byte[] Encode(in DropDespawnedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt64(1, p.InstanceId);
            w.WriteFieldInt32(2, p.Qty);
            w.WriteFieldInt32(3, p.Reason);
            return w.ToArray();
        }

        private static DropDespawnedPayload DecodeDropDespawned(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            long instanceId = 0;
            int qty = 0, reason = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.DropDespawned), ref seen))
                {
                    case 1: instanceId = r.ReadInt64LittleEndian(); break;
                    case 2: qty = r.ReadInt32LittleEndian(); break;
                    case 3: reason = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException($"DropDespawned: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.DropDespawned), seen, (1u << 3) - 1);
            r.EnsureFullyConsumed();
            return new DropDespawnedPayload(instanceId, qty, reason);
        }

        // ── 24. Craft(ADR-024 补齐轮 · 21a)───────────────────────────────
        // tag: 1 ActorId · 2 RecipeId · 3 StartTick · 4 DurationTicks
        //      5 InputInstanceIds[] · 6 ActualConsumed[] · 7 OutputQty[]
        //      8 OutputQuality[] · 9 OutputInstanceIds[] · 10 ToolCell
        // 跨字段约束 = 两组等长(组间 n ⊥ m,见文件头)。

        public static byte[] Encode(in CraftPayload p)
        {
            if (p.InputInstanceIds == null || p.ActualConsumed == null || p.OutputQty == null
                || p.OutputQuality == null || p.OutputInstanceIds == null)
                throw new ArgumentException("Craft 五数组不得为 null", nameof(p));
            if (p.ActualConsumed.Length != p.InputInstanceIds.Length)
                throw new ArgumentException(
                    $"Craft 输入组不等长(输入实例 {p.InputInstanceIds.Length} ≠ 实耗 {p.ActualConsumed.Length})—— 坏数据不进字节面",
                    nameof(p));
            if (p.OutputQuality.Length != p.OutputQty.Length
                || p.OutputInstanceIds.Length != p.OutputQty.Length)
                throw new ArgumentException(
                    $"Craft 产出组不等长(产量 {p.OutputQty.Length} / 品级 {p.OutputQuality.Length} / 输出实例 {p.OutputInstanceIds.Length})—— 坏数据不进字节面",
                    nameof(p));
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.ActorId);
            w.WriteFieldInt32(2, p.RecipeId);
            w.WriteFieldInt64(3, p.StartTick);
            w.WriteFieldInt64(4, p.DurationTicks);
            w.WriteFieldArrayInt64(5, p.InputInstanceIds);
            w.WriteFieldArrayInt32(6, p.ActualConsumed);
            w.WriteFieldArrayInt32(7, p.OutputQty);
            w.WriteFieldArrayInt32(8, p.OutputQuality);
            w.WriteFieldArrayInt64(9, p.OutputInstanceIds);
            w.WriteFieldWorldPos(10, p.ToolCell);
            return w.ToArray();
        }

        private static CraftPayload DecodeCraft(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int actorId = 0, recipeId = 0;
            long startTick = 0, durationTicks = 0;
            long[] inputInstanceIds = null, outputInstanceIds = null;
            int[] actualConsumed = null, outputQty = null, outputQuality = null;
            WorldPos toolCell = default;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.Craft), ref seen))
                {
                    case 1: actorId = r.ReadInt32LittleEndian(); break;
                    case 2: recipeId = r.ReadInt32LittleEndian(); break;
                    case 3: startTick = r.ReadInt64LittleEndian(); break;
                    case 4: durationTicks = r.ReadInt64LittleEndian(); break;
                    case 5: inputInstanceIds = r.ReadFieldArrayInt64(); break;
                    case 6: actualConsumed = r.ReadFieldArrayInt32(); break;
                    case 7: outputQty = r.ReadFieldArrayInt32(); break;
                    case 8: outputQuality = r.ReadFieldArrayInt32(); break;
                    case 9: outputInstanceIds = r.ReadFieldArrayInt64(); break;
                    case 10: toolCell = r.ReadFieldWorldPos(); break;
                    default: throw new InvalidDataException($"Craft: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.Craft), seen, (1u << 10) - 1);
            r.EnsureFullyConsumed();
            if (actualConsumed.Length != inputInstanceIds.Length)
                throw new InvalidDataException(
                    $"Craft: 输入组不等长(输入实例 {inputInstanceIds.Length} ≠ 实耗 {actualConsumed.Length})—— 已损坏");
            if (outputQuality.Length != outputQty.Length
                || outputInstanceIds.Length != outputQty.Length)
                throw new InvalidDataException(
                    $"Craft: 产出组不等长(产量 {outputQty.Length} / 品级 {outputQuality.Length} / " +
                    $"输出实例 {outputInstanceIds.Length})—— 已损坏");
            return new CraftPayload(actorId, recipeId, startTick, durationTicks,
                inputInstanceIds, actualConsumed, outputQty, outputQuality, outputInstanceIds, toolCell);
        }

        // ── 25. StructurePlaced(ADR-015 §五)──────────────────────────────
        // tag: 1 StructureId · 2 Cell · 3 ModuleId · 4 Orientation · 5 Variant

        public static byte[] Encode(in StructurePlacedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt64(1, p.StructureId);
            w.WriteFieldWorldPos(2, p.Cell);
            w.WriteFieldInt32(3, p.ModuleId);
            w.WriteFieldInt32(4, p.Orientation);
            w.WriteFieldInt32(5, p.Variant);
            return w.ToArray();
        }

        private static StructurePlacedPayload DecodeStructurePlaced(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            long structureId = 0;
            WorldPos cell = default;
            int moduleId = 0, orientation = 0, variant = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.StructurePlaced), ref seen))
                {
                    case 1: structureId = r.ReadInt64LittleEndian(); break;
                    case 2: cell = r.ReadFieldWorldPos(); break;
                    case 3: moduleId = r.ReadInt32LittleEndian(); break;
                    case 4: orientation = r.ReadInt32LittleEndian(); break;
                    case 5: variant = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException($"StructurePlaced: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.StructurePlaced), seen, (1u << 5) - 1);
            r.EnsureFullyConsumed();
            return new StructurePlacedPayload(structureId, cell, moduleId, orientation, variant);
        }

        // ── 26. StructureModified(ADR-015 §五)────────────────────────────
        // tag: 1 StructureId · 2 Cell · 3 ModuleId · 4 NewOrientation · 5 NewVariant · 6 ModifiedFields

        public static byte[] Encode(in StructureModifiedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt64(1, p.StructureId);
            w.WriteFieldWorldPos(2, p.Cell);
            w.WriteFieldInt32(3, p.ModuleId);
            w.WriteFieldInt32(4, p.NewOrientation);
            w.WriteFieldInt32(5, p.NewVariant);
            w.WriteFieldInt32(6, p.ModifiedFields);
            return w.ToArray();
        }

        private static StructureModifiedPayload DecodeStructureModified(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            long structureId = 0;
            WorldPos cell = default;
            int moduleId = 0, newOrientation = 0, newVariant = 0, modifiedFields = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.StructureModified), ref seen))
                {
                    case 1: structureId = r.ReadInt64LittleEndian(); break;
                    case 2: cell = r.ReadFieldWorldPos(); break;
                    case 3: moduleId = r.ReadInt32LittleEndian(); break;
                    case 4: newOrientation = r.ReadInt32LittleEndian(); break;
                    case 5: newVariant = r.ReadInt32LittleEndian(); break;
                    case 6: modifiedFields = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException($"StructureModified: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.StructureModified), seen, (1u << 6) - 1);
            r.EnsureFullyConsumed();
            return new StructureModifiedPayload(structureId, cell, moduleId, newOrientation, newVariant, modifiedFields);
        }

        // ── 27. StructureRemoved(ADR-015 §五)─────────────────────────────
        // tag: 1 StructureId · 2 Cell · 3 ModuleId

        public static byte[] Encode(in StructureRemovedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt64(1, p.StructureId);
            w.WriteFieldWorldPos(2, p.Cell);
            w.WriteFieldInt32(3, p.ModuleId);
            return w.ToArray();
        }

        private static StructureRemovedPayload DecodeStructureRemoved(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            long structureId = 0;
            WorldPos cell = default;
            int moduleId = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.StructureRemoved), ref seen))
                {
                    case 1: structureId = r.ReadInt64LittleEndian(); break;
                    case 2: cell = r.ReadFieldWorldPos(); break;
                    case 3: moduleId = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException($"StructureRemoved: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.StructureRemoved), seen, (1u << 3) - 1);
            r.EnsureFullyConsumed();
            return new StructureRemovedPayload(structureId, cell, moduleId);
        }

        // ── 28. PoiStateChanged(ADR-021 §三)──────────────────────────────
        // tag: 1 PoiId · 2 NewState

        public static byte[] Encode(in PoiStateChangedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.PoiId);
            w.WriteFieldInt32(2, p.NewState);
            return w.ToArray();
        }

        private static PoiStateChangedPayload DecodePoiStateChanged(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int poiId = 0, newState = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.PoiStateChanged), ref seen))
                {
                    case 1: poiId = r.ReadInt32LittleEndian(); break;
                    case 2: newState = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException($"PoiStateChanged: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.PoiStateChanged), seen, (1u << 2) - 1);
            r.EnsureFullyConsumed();
            return new PoiStateChangedPayload(poiId, newState);
        }

        // ── 29. EnemyInjuryOnset(ADR-016 §二,形状同 InjuryOnset)─────────
        // tag: 1 ActorId · 2 TargetId · 3 InjuryId · 4 Magnitude(Fix)· 5 Tick · 6 DoseSeq

        public static byte[] Encode(in EnemyInjuryOnsetPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.ActorId);
            w.WriteFieldInt32(2, p.TargetId);
            w.WriteFieldInt32(3, p.InjuryId);
            w.WriteFieldFix(4, p.Magnitude);
            w.WriteFieldInt64(5, p.Tick);
            w.WriteFieldInt32(6, p.DoseSeq);
            return w.ToArray();
        }

        private static EnemyInjuryOnsetPayload DecodeEnemyInjuryOnset(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int actorId = 0, targetId = 0, injuryId = 0, doseSeq = 0;
            Fix magnitude = default;
            long tick = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.EnemyInjuryOnset), ref seen))
                {
                    case 1: actorId = r.ReadInt32LittleEndian(); break;
                    case 2: targetId = r.ReadInt32LittleEndian(); break;
                    case 3: injuryId = r.ReadInt32LittleEndian(); break;
                    case 4: magnitude = r.ReadFieldFix(); break;
                    case 5: tick = r.ReadInt64LittleEndian(); break;
                    case 6: doseSeq = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException($"EnemyInjuryOnset: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.EnemyInjuryOnset), seen, (1u << 6) - 1);
            r.EnsureFullyConsumed();
            return new EnemyInjuryOnsetPayload(actorId, targetId, injuryId, magnitude, tick, doseSeq);
        }

        // ── 30. InjuryStateChanged(25 战斗)────────────────────────────────
        // tag: 1 ActorId · 2 NewState

        public static byte[] Encode(in InjuryStateChangedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.ActorId);
            w.WriteFieldInt32(2, p.NewState);
            return w.ToArray();
        }

        private static InjuryStateChangedPayload DecodeInjuryStateChanged(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int actorId = 0, newState = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.InjuryStateChanged), ref seen))
                {
                    case 1: actorId = r.ReadInt32LittleEndian(); break;
                    case 2: newState = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException($"InjuryStateChanged: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.InjuryStateChanged), seen, (1u << 2) - 1);
            r.EnsureFullyConsumed();
            return new InjuryStateChangedPayload(actorId, newState);
        }

        // ── 31. EncounterStarted(52 奇遇)──────────────────────────────────
        // tag: 1 EncounterId · 2 ProtoId · 3 SpawnCell · 4 ActorIds[]

        public static byte[] Encode(in EncounterStartedPayload p)
        {
            if (p.ActorIds == null)
                throw new ArgumentException("ActorIds 不得为 null(=0 生成时为空数组)", nameof(p));
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.EncounterId);
            w.WriteFieldInt32(2, p.ProtoId);
            w.WriteFieldWorldPos(3, p.SpawnCell);
            w.WriteFieldArrayInt32(4, p.ActorIds);
            return w.ToArray();
        }

        private static EncounterStartedPayload DecodeEncounterStarted(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int encounterId = 0, protoId = 0;
            WorldPos spawnCell = default;
            int[] actorIds = null;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.EncounterStarted), ref seen))
                {
                    case 1: encounterId = r.ReadInt32LittleEndian(); break;
                    case 2: protoId = r.ReadInt32LittleEndian(); break;
                    case 3: spawnCell = r.ReadFieldWorldPos(); break;
                    case 4: actorIds = r.ReadFieldArrayInt32(); break;
                    default: throw new InvalidDataException($"EncounterStarted: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.EncounterStarted), seen, (1u << 4) - 1);
            r.EnsureFullyConsumed();
            return new EncounterStartedPayload(encounterId, protoId, spawnCell, actorIds);
        }

        // ── 32. EncounterEnded(52 奇遇)────────────────────────────────────
        // tag: 1 EncounterId · 2 Reason

        public static byte[] Encode(in EncounterEndedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.EncounterId);
            w.WriteFieldInt32(2, p.Reason);
            return w.ToArray();
        }

        private static EncounterEndedPayload DecodeEncounterEnded(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int encounterId = 0, reason = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.EncounterEnded), ref seen))
                {
                    case 1: encounterId = r.ReadInt32LittleEndian(); break;
                    case 2: reason = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException($"EncounterEnded: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.EncounterEnded), seen, (1u << 2) - 1);
            r.EnsureFullyConsumed();
            return new EncounterEndedPayload(encounterId, reason);
        }

        // ── 33. ConsequenceResolved(ADR-024 §④)───────────────────────────
        // tag: 1 Outcome · 2 PatientId

        public static byte[] Encode(in ConsequenceResolvedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.Outcome);
            w.WriteFieldInt32(2, p.PatientId);
            return w.ToArray();
        }

        private static ConsequenceResolvedPayload DecodeConsequenceResolved(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int outcome = 0, patientId = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.ConsequenceResolved), ref seen))
                {
                    case 1: outcome = r.ReadInt32LittleEndian(); break;
                    case 2: patientId = r.ReadInt32LittleEndian(); break;
                    default: throw new InvalidDataException($"ConsequenceResolved: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.ConsequenceResolved), seen, (1u << 2) - 1);
            r.EnsureFullyConsumed();
            return new ConsequenceResolvedPayload(outcome, patientId);
        }

        // ── 34. PlayerDied(26)─────────────────────────────────────────────
        // tag: 1 ActorId · 2 DeathCell · 3 Tick

        public static byte[] Encode(in PlayerDiedPayload p)
        {
            var w = new CodecWriter();
            w.WriteFieldInt32(1, p.ActorId);
            w.WriteFieldWorldPos(2, p.DeathCell);
            w.WriteFieldInt64(3, p.Tick);
            return w.ToArray();
        }

        private static PlayerDiedPayload DecodePlayerDied(ReadOnlySpan<byte> bytes)
        {
            var r = new CodecReader(bytes);
            uint seen = 0;
            int actorId = 0;
            WorldPos deathCell = default;
            long tick = 0;
            while (r.HasMore)
            {
                switch (ReadTagChecked(ref r, nameof(EventKind.PlayerDied), ref seen))
                {
                    case 1: actorId = r.ReadInt32LittleEndian(); break;
                    case 2: deathCell = r.ReadFieldWorldPos(); break;
                    case 3: tick = r.ReadInt64LittleEndian(); break;
                    default: throw new InvalidDataException($"PlayerDied: 未知 tag");
                }
            }
            RequireCompleteMask(nameof(EventKind.PlayerDied), seen, (1u << 3) - 1);
            r.EnsureFullyConsumed();
            return new PlayerDiedPayload(actorId, deathCell, tick);
        }
    }
}
