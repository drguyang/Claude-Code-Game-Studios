// Story 001 · clinic-machine —— 24 三表的 deterministic cooked 写入器(阶段 2 烘焙的写面)。
//
// 权威来源:
//   ADR-014 §二(同源两次烘焙逐位一致:固定字段顺序 · 零字典迭代 · 零本地时间)
//   · CookedFormat.cs(20 字节头布局的唯一共参照)
//   · 三个 Binder 的 BindResult(行集唯一来源)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序;字符串 = i32 字节长 + UTF-8,长度 -1 = null):
//   clinicmachine_context.cooked
//     i64 k_context_max_raw        ← F-24-3 表派生导出值(交付系统 1;非裸常数)
//     i32 row_count
//     row_count × { str room_type; str context_id; i64 k_speed_raw; i64 base_env_raw }
//   clinicmachine_adj.cooked
//     i64 w_min_raw; i64 w_max_raw; i64 n_max
//     i32 pair_count
//     pair_count × { str a; str b; i64 w_raw }
//   clinicmachine_room.cooked
//     i32 room_count
//     room_count × { str room_type; i32 clause_count;
//                    clause_count × { u8 kind; str type; i32 min; str neighbor_type; u8 direction } }
//
// ⚠️ **行序一律全序**(承 story-002 返工(A)的教训:`List.Sort` 不稳定 ⇒ 键相同时序取决于实现内部,
//   同源两次烘焙字节可能不一致,违 ADR-014 §二)。故:
//     context 行 = (room_type, context_id) 升序(room_type 构建期已互异 ⇒ 单键已充分,二级仅防御)
//     adj   行 = (a, b) 升序(构建期键互异)
//     room  行 = room_type 升序;clause = (kind, type, neighbor_type, direction, min) 升序
//
// ⚠️ **schema_version 只住头槽**(offset 8,单一出处)。承 story-002 BLOCKING R-1 教训:头槽硬写 0、
//   真值编进载荷首字段 ⇒ 违 CookedFormat 头契约(读方 OpenChecked 只读头槽比对)+ 第二真源。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>24 三表的 deterministic cooked 写入器。</summary>
    internal static class ClinicMachineCookedWriter
    {
        /// <summary>编码 CONTEXT_TABLE(含 20 字节头)。</summary>
        public static byte[] WriteContext(
            in ClinicMachineContextBinder.BindResult bound, uint configVersion)
        {
            var rows = new List<ClinicMachineContextBinder.ContextRow>(bound.Rows);
            if (rows.Count == 0)
                throw new InvalidOperationException(
                    "CONTEXT_TABLE 绑定行集为空 ⇒ 拒绝落盘(Binder 的空表校验应已拦;" +
                    "空表无法派生 K_CONTEXT_MAX = 静默失败)");

            rows.Sort((x, y) =>
            {
                int cmp = string.CompareOrdinal(x.RoomType, y.RoomType);
                if (cmp != 0) return cmp;
                return string.CompareOrdinal(x.ContextId, y.ContextId);
            });

            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                w.Write(bound.KContextMax.Raw);   // 导出值先落(系统 1 可直读)
                w.Write(rows.Count);
                for (int i = 0; i < rows.Count; i++)
                {
                    WriteString(w, rows[i].RoomType);
                    WriteString(w, rows[i].ContextId);
                    w.Write(rows[i].KSpeed.Raw);
                    w.Write(rows[i].BaseEnv.Raw);
                }
                w.Flush();
                return FinishHeader(payload.ToArray(), bound.SchemaVersion, configVersion);
            }
        }

        /// <summary>编码 ADJ_TABLE(含 20 字节头)。</summary>
        public static byte[] WriteAdj(
            in ClinicMachineAdjBinder.BindResult bound, uint configVersion)
        {
            var pairs = new List<ClinicMachineAdjBinder.AdjPair>(bound.Pairs);
            if (pairs.Count == 0)
                throw new InvalidOperationException(
                    "ADJ_TABLE 绑定行集为空 ⇒ 拒绝落盘(Binder 的空表校验应已拦;" +
                    "空邻接表让 adj_sum 恒 0 = 静默中性)");

            pairs.Sort((x, y) =>
            {
                int cmp = string.CompareOrdinal(x.A, y.A);
                if (cmp != 0) return cmp;
                cmp = string.CompareOrdinal(x.B, y.B);
                if (cmp != 0) return cmp;
                return x.W.Raw.CompareTo(y.W.Raw);   // 防御破平(构建期键互异 ⇒ 恒不触)
            });

            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                w.Write(bound.WMin.Raw);
                w.Write(bound.WMax.Raw);
                w.Write(bound.NMax);
                w.Write(pairs.Count);
                for (int i = 0; i < pairs.Count; i++)
                {
                    WriteString(w, pairs[i].A);
                    WriteString(w, pairs[i].B);
                    w.Write(pairs[i].W.Raw);
                }
                w.Flush();
                return FinishHeader(payload.ToArray(), bound.SchemaVersion, configVersion);
            }
        }

        /// <summary>编码 ROOM_TABLE(含 20 字节头)。</summary>
        public static byte[] WriteRoom(
            in ClinicMachineRoomBinder.BindResult bound, uint configVersion)
        {
            var rooms = new List<ClinicMachineRoomBinder.RoomDef>(bound.Rooms);
            if (rooms.Count == 0)
                throw new InvalidOperationException(
                    "ROOM_TABLE 绑定行集为空 ⇒ 拒绝落盘(Binder 的空表校验应已拦;" +
                    "空表让所有布局恒 ROOM_NONE = 整表静默失效)");

            rooms.Sort((x, y) => string.CompareOrdinal(x.RoomType, y.RoomType));

            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                w.Write(rooms.Count);
                for (int i = 0; i < rooms.Count; i++)
                {
                    WriteString(w, rooms[i].RoomType);

                    var clauses = new List<ClinicMachineRoomBinder.RoomClause>(rooms[i].Clauses);
                    clauses.Sort(CompareClause);
                    w.Write(clauses.Count);

                    for (int c = 0; c < clauses.Count; c++)
                    {
                        ClinicMachineRoomBinder.RoomClause clause = clauses[c];
                        w.Write((byte)clause.Kind);
                        WriteString(w, clause.Type);
                        w.Write(clause.Min);
                        WriteString(w, clause.NeighborType);     // null ⇒ -1(定长口径统一)
                        w.Write((byte)clause.Direction);
                    }
                }
                w.Flush();
                return FinishHeader(payload.ToArray(), bound.SchemaVersion, configVersion);
            }
        }

        /// <summary>子句全序(kind → type → neighbor_type → direction → min)。</summary>
        private static int CompareClause(
            ClinicMachineRoomBinder.RoomClause x, ClinicMachineRoomBinder.RoomClause y)
        {
            int cmp = ((byte)x.Kind).CompareTo((byte)y.Kind);
            if (cmp != 0) return cmp;

            cmp = string.CompareOrdinal(x.Type, y.Type);
            if (cmp != 0) return cmp;

            cmp = string.CompareOrdinal(x.NeighborType ?? string.Empty, y.NeighborType ?? string.Empty);
            if (cmp != 0) return cmp;

            cmp = ((byte)x.Direction).CompareTo((byte)y.Direction);
            if (cmp != 0) return cmp;

            return x.Min.CompareTo(y.Min);
        }

        /// <summary>长度前缀字符串(-1 = null;承 CookedWriter.WriteString 口径)。</summary>
        private static void WriteString(BinaryWriter w, string s)
        {
            if (s == null)
            {
                w.Write(-1);
                return;
            }
            byte[] utf8 = Encoding.UTF8.GetBytes(s);
            w.Write(utf8.Length);
            w.Write(utf8);
        }

        /// <summary>头(magic + formatVersion + schemaVersion + configVersion + payloadLength)+ 载荷。</summary>
        private static byte[] FinishHeader(byte[] payload, uint schemaVersion, uint configVersion)
        {
            var output = new byte[CookedFormat.HeaderSize + payload.Length];
            string magic = CookedFormat.Magic;
            output[0] = (byte)magic[0];
            output[1] = (byte)magic[1];
            output[2] = (byte)magic[2];
            output[3] = (byte)magic[3];
            WriteU32Le(output, 4, CookedFormat.FormatVersion);
            WriteU32Le(output, 8, schemaVersion);   // 头槽承载(非 0,非载荷首字段)
            WriteU32Le(output, 12, configVersion);
            WriteU32Le(output, 16, (uint)payload.Length);
            Buffer.BlockCopy(payload, 0, output, CookedFormat.HeaderSize, payload.Length);
            return output;
        }

        private static void WriteU32Le(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}
