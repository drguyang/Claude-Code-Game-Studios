// 权威来源:ADR-014 §二(确定性 *.cooked;同源两次烘焙逐位一致)· CookedFormat.cs(布局共参照)
//
// ⚠️ 布局**由本件单方面定义** —— 读方 codec **尚不存在**(见文件尾 §未实现登记)。
//    与 `DiseaseActionAxisCookedWriter` 不同,那一件有 `DiseaseActionAxisCookedCodec` 镜像;
//    本件是**先有写方、后补读方**的形态。⇒ **产物当前无人消费**。
//    这不是「管线已通」—— 只证明「能产出确定字节」。
//
// ⚠️ 字符串一律 i32 字节长 + UTF-8(**不是** BinaryWriter.Write(string) 的 7-bit 变长前缀)
//    —— 承 `DiseaseActionAxisCookedWriter` 2026-10-20 的契约违例修正。
//
// ⚠️ Fix 字段写 **raw long**(ADR-014 §四:玩家构建零 FixParse,`.cooked` 里 Fix = raw)。

using System.Collections.Generic;
using System.IO;
using System.Text;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>病种注册表的 deterministic cooked 写入器。
    /// <para>同一批行两次 ⇒ 逐位一致字节流(固定字段顺序、零字典迭代、零本地时间)。</para></summary>
    internal static class DiseaseRegistryCookedWriter
    {
        /// <summary>编码契约行集(含 20 字节头)。</summary>
        public static byte[] Write(IReadOnlyList<DiseaseRegistryEntry> rows,
                                   uint schemaVersion, uint configVersion)
        {
            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                w.Write(rows?.Count ?? -1);
                if (rows != null)
                {
                    for (int i = 0; i < rows.Count; i++)
                        WriteRow(w, rows[i]);
                }

                w.Flush();
                return FinishHeader(payload.ToArray(), schemaVersion, configVersion);
            }
        }

        /// <summary>单行编码(字段顺序 = 本方法的书写序,读方须镜像)。</summary>
        private static void WriteRow(BinaryWriter w, DiseaseRegistryEntry e)
        {
            w.Write(e.DiseaseId);                        // int
            WriteString32(w, e.DiseaseKey);              // i32 字节长 + UTF-8
            w.Write((int)e.Kind);                        // 枚举按 int
            w.Write(e.Polarity);
            w.Write(e.Severity);
            w.Write(e.Contagion);
            w.Write(e.Lethality);
            w.Write(e.TreatmentDifficulty);
            w.Write(e.RecoveryTime);
            w.Write(e.RelapseChance);
            w.Write(e.ComorbidityFactor);
            w.Write(e.SeasonalMod);
            w.Write(e.AgeMod);
            w.Write(e.GenderMod);
            w.Write(e.OccupationMod);
            w.Write(e.RegionMod);
            w.Write(e.ClimateMod);
            w.Write(e.Scale.Raw);                        // Fix = raw long
            w.Write(e.TraumaHalfLife);                   // int(仅 injury 有意义)

            // ── curve ──
            // null = 既有占位条目(曲线未声明)。写一个 bool 区分,读方据此跳过整块。
            w.Write(e.Curve != null);
            if (e.Curve != null)
            {
                w.Write(e.Curve.Incubation);
                w.Write(e.Curve.APeak.Raw);
                w.Write(e.Curve.TauRise.Raw);
                w.Write(e.Curve.TauPeak);
                w.Write(e.Curve.TauFall.Raw);
                w.Write(e.Curve.Plateau);
                w.Write(e.Curve.SelfLimit);
                // Sigma:Fix? —— 先写 hasValue,再写 raw
                w.Write(e.Curve.Sigma.HasValue);
                if (e.Curve.Sigma.HasValue) w.Write(e.Curve.Sigma.Value.Raw);
                WriteString32(w, e.Curve.BoundaryMode);
            }
        }

        /// <summary>写 <see cref="CookedFormat"/> 规定的字符串形状:i32 字节长 + UTF-8(无终止符)。</summary>
        private static void WriteString32(BinaryWriter w, string value)
        {
            if (value == null) { w.Write(-1); return; }
            byte[] utf8 = Encoding.UTF8.GetBytes(value);
            w.Write(utf8.Length);
            w.Write(utf8);
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
            WriteU32Le(output, 8, schemaVersion);
            WriteU32Le(output, 12, configVersion);
            WriteU32Le(output, 16, (uint)payload.Length);
            System.Buffer.BlockCopy(payload, 0, output, CookedFormat.HeaderSize, payload.Length);
            return output;
        }

        private static void WriteU32Le(byte[] buf, int offset, uint v)
        {
            buf[offset + 0] = (byte)(v & 0xFF);
            buf[offset + 1] = (byte)((v >> 8) & 0xFF);
            buf[offset + 2] = (byte)((v >> 16) & 0xFF);
            buf[offset + 3] = (byte)((v >> 24) & 0xFF);
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════
// §未实现登记(如实列出,禁借绿)
// ══════════════════════════════════════════════════════════════════════════
//   ① **读方 codec 不存在** —— 没有 `DiseaseRegistryCookedCodec`。产物无人消费。
//      与 `DiseaseActionAxis` 不同,那一件写方/读方成对;本件只有写方。
//   ② `Relapse` / `TreatableBy` / `Signs` **未编码** —— 当前数据全为 null。
//      一旦八病种落地(疟疾带复发块),布局须扩展且**格式版本号须 +1**。
//   ③ `Handle` **未编码** —— 它是派生字段,当前无人算(见 Binder 文件尾 ①)。
