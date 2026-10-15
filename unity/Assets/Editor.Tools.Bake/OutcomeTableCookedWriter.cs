// S12-9 · story-002 —— 53 判定表的 deterministic cooked 写入器(阶段 2 烘焙的写面)。
//
// 权威来源:ADR-014 §二(同源两次烘焙逐位一致)· CookedFormat.cs(布局共参照)
//          · OutcomeTableBinder.BindResult(行集来源)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序 · 零字典迭代 · 零本地时间):
//    u_band_count   : i32
//    default_outcome: i32(枚举序数;∈ 回响轴三值 ∪ NoEcho —— 构建期已校验)
//    row_count      : i32(= 18 × u_band_count;default 行不落行集)
//    row_count × { key: i32(P*3+C)*2+D 混合 × u_band_count + u_band, outcome: i32 }
//
// ⚠️ schema_version **只住头槽**(offset 8,单一出处)。
//   2026-10-15 返工(R-1):原实现头槽硬写 0、真值编进**载荷首字段** ——
//   ① 违 CookedFormat 头契约(九个姊妹写方全写 schemaVersion,53 是唯一写 0 的);
//   ② 同一真值两处存 = 第二真源,而读方 CookedCodec.OpenChecked 只读头槽
//      (schemaVersion != CookedFormat.SchemaVersion ⇒ InvalidDataException,启动期硬失败)。
//   现已删载荷首字段,头槽写 bound.SchemaVersion。
//
// ⚠️ 行键以 Outcome53.RowKeyOf 同式编码(与运行期查表零漂移);枚举序数落盘
//   (非名字字符串 —— 字节流确定性;名字 ↔ 序数映射由枚举定义钉死)。
//
// ⚠️ 行序 = **全序比较器**(键 → P → C → D → UBand 逐级破平)。
//   2026-10-15 返工(A):原实现用 List.Sort(不稳定)按单键排 —— 键相同时次序取决于
//   实现内部 ⇒ 同源两次烘焙字节可能不一致(违 ADR-014 §二)。全序比较器使确定性
//   **不依赖** Sort 的稳定性实现。

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
                w.Write(bound.UBandCount);
                w.Write((int)bound.DefaultOutcome);

                var rowList = new System.Collections.Generic.List<(
                    Outcome53.JudgmentLane P, Outcome53.CareLane C,
                    Outcome53.RecoveryLane D, int UBand, Outcome53.Outcome Outcome)>();
                foreach (var kv in bound.Rows)
                    rowList.Add((kv.Key.P, kv.Key.C, kv.Key.D, kv.Key.UBand, kv.Value));

                // 固定序:全序比较器(键 → P → C → D → UBand;字典序无涉)。
                // bandCount 从 bound 闭包捕获(**逐表量** —— 不是逐行量 uBand+1)。
                int bandCount = bound.UBandCount;
                rowList.Sort((a, b) =>
                {
                    int cmp = Outcome53.RowKeyOf(a.P, a.C, a.D, a.UBand, bandCount)
                        .CompareTo(Outcome53.RowKeyOf(b.P, b.C, b.D, b.UBand, bandCount));
                    if (cmp != 0) return cmp;
                    cmp = a.P.CompareTo(b.P);   if (cmp != 0) return cmp;
                    cmp = a.C.CompareTo(b.C);   if (cmp != 0) return cmp;
                    cmp = a.D.CompareTo(b.D);   if (cmp != 0) return cmp;
                    return a.UBand.CompareTo(b.UBand);
                });

                w.Write(rowList.Count);
                for (int i = 0; i < rowList.Count; i++)
                {
                    w.Write(Outcome53.RowKeyOf(rowList[i].P, rowList[i].C, rowList[i].D,
                                               rowList[i].UBand, bandCount));
                    w.Write((int)rowList[i].Outcome);
                }

                w.Flush();
                return FinishHeader(payload.ToArray(), bound.SchemaVersion, configVersion);
            }
        }

        /// <summary>头(magic + formatVersion + schemaVersion + configVersion + payloadLength)+ 载荷。</summary>
        private static byte[] FinishHeader(byte[] payload, uint schemaVersion, uint configVersion)
        {
            var output = new byte[CookedFormat.HeaderSize + payload.Length];
            output[0] = (byte)'D';
            output[1] = (byte)'Y';
            output[2] = (byte)'J';
            output[3] = (byte)'C';
            WriteU32Le(output, 4, CookedFormat.FormatVersion);
            WriteU32Le(output, 8, schemaVersion);   // R-1:头槽承载(非 0,非载荷首字段)
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
