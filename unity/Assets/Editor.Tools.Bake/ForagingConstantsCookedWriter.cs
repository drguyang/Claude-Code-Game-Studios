// S20-3 · story-001 —— `foraging_constants` 的 cooked 写方(评审 W-3 甲案:补齐烘焙接线)。
//
// 权威来源:
//   ADR-014 §二(阶段 3:deterministic cooked 写出)· CookedFormat(头部契约)
//   `JudgmentLexiconCookedWriter` 同款结构(本仓最小的 cooked 写方先例)
//
// ⚠️ **载荷布局(全部 little-endian)** —— 与运行期读方严格镜像:
//   seasons_per_year      : i32
//   cap_table             : i32 元素数 + N × i32        (int 计数,非 Fix —— D-21-17)
//   season_mult           : i32 元素数 + N × i64 raw    (Fix)
//   regrow_window_ticks   : i64
//   base_capacity         : i32
//   decay_rate            : i64 raw                     (Fix)
//   decay_floor           : i64 raw                     (Fix)
//   gather_mul_cap        : i64 raw                     (Fix)
//
// ⚠️ 零 float / double(ADR-005/006)。`Fix` 一律写 raw `long`。
// ⚠️ 字段顺序 = `ForagingConstants` 构造参数序 —— 改序 = 改格式 = 读方同步改。
// ⚠️ 数组元素数写 `-1` 表示 null;本表的数组经 Binder 校验后**恒非 null**,
//   故此处不写 -1 分支(写了 = 一条永不执行的路径,反而模糊契约)。

using System.Collections.Generic;
using System.IO;
using System.Text;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`foraging_constants` 的 deterministic cooked 写入器(同源两次烘焙逐位一致)。</summary>
    internal static class ForagingConstantsCookedWriter
    {
        /// <summary>把已校验的绑定结果写成带 20 字节头的 cooked 字节流。</summary>
        /// <param name="r">阶段 2 的绑定结果(全部字段已过形状与值域判据)。</param>
        /// <param name="schemaVersion">源表 <c>schema_version</c>(写方头槽 offset 8 的唯一真值来源)。</param>
        /// <param name="configVersion">源内容哈希派生的 u32(ADR-014 §五)。</param>
        public static byte[] Write(
            in ForagingConstantsBinder.BindResult r, uint schemaVersion, uint configVersion)
        {
            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                w.Write(r.SeasonsPerYear);

                WriteIntArray(w, r.CapTable);
                WriteFixArray(w, r.SeasonMult);

                w.Write(r.RegrowWindowTicks);
                w.Write(r.BaseCapacity);
                w.Write(r.DecayRate.Raw);
                w.Write(r.DecayFloor.Raw);
                w.Write(r.GatherMulCap.Raw);

                w.Flush();
                return FinishHeader(payload.ToArray(), schemaVersion, configVersion);
            }
        }

        private static void WriteIntArray(BinaryWriter w, IReadOnlyList<int> values)
        {
            w.Write(values.Count);
            for (int i = 0; i < values.Count; i++)
                w.Write(values[i]);
        }

        private static void WriteFixArray(BinaryWriter w, IReadOnlyList<Fix> values)
        {
            w.Write(values.Count);
            for (int i = 0; i < values.Count; i++)
                w.Write(values[i].Raw);
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
