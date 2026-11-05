// 权威来源:ADR-014 §二(确定性 *.cooked)· CookedFormat.cs(布局共参照)
//          · GDD random-events.md 规则二(池条目五字段 + cause_flag)
//
// ⚠️ 布局与 `Gameplay.Presentation.EventPoolCookedCodec` **严格镜像** —— 改一处必须改两处。
//
// ⚠️ 载荷布局(全部 little-endian,可变长 = 固定头 + 逐条定长):
//    night_num : i32 · night_den : i32 · max_num : i32 · max_den : i32
//    count     : i32
//    count × { key:i32 · w_base:i32 · tier:i32 · spawn_anchor:i32 · trigger_mode:i32 ·
//              handler:i32 · cause_flag_key:i32 · is_house_call:i32 } = 32 B/条
//    🔁 2026-11-05(`TD-randomevents-06`):`is_house_call` 追加在第 8 个 i32
//       (schema_version 1 → 2;读方 `EventPoolCookedCodec.ExpectedSchemaVersion` 同批改)。
//
// ⚠️ 条目**按 key 升序**落盘(Binder 已规范化)⇒ 读方不必再排序,
//    且「游走序 = 落盘序」使 CDF walk 的顺序成为产物的一部分(GDD `:687`)。

using System.IO;
using System.Text;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`52_event_pool` 的 deterministic cooked 写入器。</summary>
    internal static class EventPoolCookedWriter
    {
        /// <summary>每条条目的固定字节数(8 × i32)。</summary>
        public const int EntryBytes = 32;

        /// <summary>固定头部(5 × i32:四个乘子 + count)。</summary>
        public const int FixedPayloadBytes = 20;

        /// <summary>编码(含 20 字节头)。</summary>
        public static byte[] Write(in EventPoolBinder.BindResult bound, uint configVersion)
        {
            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                w.Write(bound.NightThreatMultNum);
                w.Write(bound.NightThreatMultDen);
                w.Write(bound.TodMultMaxNum);
                w.Write(bound.TodMultMaxDen);
                w.Write(bound.Entries.Count);
                for (int i = 0; i < bound.Entries.Count; i++)
                {
                    EventPoolEntryData e = bound.Entries[i];
                    w.Write(e.Key);
                    w.Write(e.WBase);
                    w.Write(e.Tier);
                    w.Write(e.SpawnAnchor);
                    w.Write(e.TriggerMode);
                    w.Write((int)e.Handler);
                    w.Write(e.CauseFlagKey);
                    w.Write(e.IsHouseCall ? 1 : 0);
                }
                w.Flush();
                return FinishHeader(payload.ToArray(), bound.SchemaVersion, configVersion);
            }
        }

        private static byte[] FinishHeader(byte[] payload, uint schemaVersion, uint configVersion)
        {
            var output = new byte[CookedFormat.HeaderSize + payload.Length];
            output[0] = (byte)'D';
            output[1] = (byte)'Y';
            output[2] = (byte)'J';
            output[3] = (byte)'C';
            WriteU32Le(output, 4, CookedFormat.FormatVersion);
            WriteU32Le(output, 8, schemaVersion);
            WriteU32Le(output, 12, configVersion);
            WriteU32Le(output, 16, (uint)payload.Length);
            System.Buffer.BlockCopy(payload, 0, output, CookedFormat.HeaderSize, payload.Length);
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
