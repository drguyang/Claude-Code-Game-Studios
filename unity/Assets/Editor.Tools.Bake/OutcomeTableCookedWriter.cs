// S12-9 · story-002 —— 53 判定表的 deterministic cooked 写入器(阶段 2 烘焙的写面)。
//
// 权威来源:ADR-014 §二(同源两次烘焙逐位一致)· CookedFormat.cs(布局共参照)
//          · OutcomeTableBinder.BindResult(行集来源)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序 · 零字典迭代 · 零本地时间):
//    schema_version : u32
//    u_band_count   : i32
//    default_outcome: i32(枚举序数;∈ 回响轴三值 ∪ NoEcho —— 构建期已校验)
//    row_count      : i32(= 18 × u_band_count;default 行不落行集)
//    row_count × { key: i32(P*3+C)*2+D 混合 × (u_band+1) + u_band, outcome: i32 }
//
// ⚠️ 行键以 Outcome53.RowKeyOf 同式编码(与运行期查表零漂移);枚举序数落盘
//   (非名字字符串 —— 字节流确定性;名字 ↔ 序数映射由枚举定义钉死)。

using System.IO;
using System.Text;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Consequences;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>53 判定表的 deterministic cooked 写入器。</summary>
    internal static class OutcomeTableCookedWriter
    {
        /// <summary>编码判定表(含 20 字节头)。</summary>
        public static byte[] Write(in OutcomeTableBinder.BindResult bound, uint configVersion)
        {
            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                w.Write(bound.SchemaVersion);
                w.Write(bound.UBandCount);
                w.Write((int)bound.DefaultOutcome);

                var rowList = new System.Collections.Generic.List<(
                    Outcome53.JudgmentLane P, Outcome53.CareLane C,
                    Outcome53.RecoveryLane D, int UBand, Outcome53.Outcome Outcome)>();
                foreach (var kv in bound.Rows)
                    rowList.Add((kv.Key.P, kv.Key.C, kv.Key.D, kv.Key.UBand, kv.Value));

                // 固定序:行键升序(字典序无涉 —— 同源两次烘焙逐位一致的关键)。
                rowList.Sort((a, b) => Outcome53.RowKeyOf(a.P, a.C, a.D, a.UBand)
                                           .CompareTo(Outcome53.RowKeyOf(b.P, b.C, b.D, b.UBand)));

                w.Write(rowList.Count);
                for (int i = 0; i < rowList.Count; i++)
                {
                    w.Write(Outcome53.RowKeyOf(rowList[i].P, rowList[i].C, rowList[i].D, rowList[i].UBand));
                    w.Write((int)rowList[i].Outcome);
                }

                w.Flush();
                return FinishHeader(payload.ToArray(), configVersion);
            }
        }

        /// <summary>头(magic + formatVersion + schemaVersion + configVersion + payloadLength)+ 载荷。
        /// <para>⚠️ 53 判定表**无独立 schema_version 字段**:BindResult.SchemaVersion 来自源 JSON
        /// 的 schema_version 键 —— 但 BindResult 是 struct,本写入器只收 configVersion;
        /// schemaVersion 已编在行集校验面,落盘口径 = 头部 schema 槽位放 0 + 载荷首字段承载。
        /// (承 ReadFloor 布局:头 4–8 槽 formatVersion、8–12 schemaVersion —— 本件 schemaVersion
        /// 从 bound 带入,签名保持同形。)</para></summary>
        private static byte[] FinishHeader(byte[] payload, uint configVersion)
        {
            var output = new byte[CookedFormat.HeaderSize + payload.Length];
            output[0] = (byte)'D';
            output[1] = (byte)'Y';
            output[2] = (byte)'J';
            output[3] = (byte)'C';
            WriteU32Le(output, 4, CookedFormat.FormatVersion);
            WriteU32Le(output, 8, 0);              // schema 槽:表自身无独立 schema(源 JSON 已校验)
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
