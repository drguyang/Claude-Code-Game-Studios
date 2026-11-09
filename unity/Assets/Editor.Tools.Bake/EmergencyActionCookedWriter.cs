// `emergency_action` 的 deterministic cooked 写入器(T14 · 2026-11-09)
//
// 权威:ADR-014 §二(确定性 *.cooked)· `CookedFormat.cs`(布局共参照)
//       · `design/gdd/emergency-procedures.md` `:843`(动作表列)
//
// ⚠️ 布局与 `Gameplay.Presentation.EmergencyActionCookedCodec` **严格镜像**。
//
// 载荷布局(全部 little-endian):
//   row_count : i32
//   每行:
//     action_id        : i32
//     polarity         : i32
//     base_potency     : i64   (Fix raw)
//     mag_threshold    : i32
//     min_edges        : i32
//     min_hold_ticks   : i32
//     half_life_ticks  : i32
//     duration_ticks   : i32
//     result_mul[3]    : i64 × 3(Fix raw,按 JudgeResult 序数)

using System.Collections.Generic;
using System.IO;
using System.Text;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.EmergencyProcedures;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`emergency_action` 的 deterministic cooked 写入器。</summary>
    internal static class EmergencyActionCookedWriter
    {
        /// <summary>每行固定字节数(7 个 i32 + 1 个 i64 + 3 个 i64 = 28 + 8 + 24 = 60)。</summary>
        public const int BytesPerRow = 4 * 7 + 8 + 8 * 3;

        /// <summary>编码(含 20 字节头)。</summary>
        public static byte[] Write(IReadOnlyList<EmergencyActionRow> rows, uint schemaVersion,
                                   uint configVersion)
        {
            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                w.Write(rows.Count);
                for (int i = 0; i < rows.Count; i++)
                {
                    EmergencyActionRow r = rows[i];
                    w.Write(r.ActionId);
                    w.Write(r.Polarity);
                    w.Write(r.BasePotency);
                    w.Write(r.MagThreshold);
                    w.Write(r.MinEdges);
                    w.Write(r.MinHoldTicks);
                    w.Write(r.HalfLifeTicks);
                    w.Write(r.DurationTicks);
                    for (int t = 0; t < 3; t++) w.Write(r.ResultMul[t]);
                }
                w.Flush();
                return FinishHeader(payload.ToArray(), schemaVersion, configVersion);
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
