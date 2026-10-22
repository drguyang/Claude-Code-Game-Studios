// 权威来源:ADR-014 §二(确定性 *.cooked;同源两次烘焙逐位一致)· CookedFormat.cs(布局共参照)
//
// 读方镜像 = `Gameplay.Presentation/DiseaseRegistryCookedCodec.cs`(**2026-10-22 补立**)。
//   本件自此是**写方 / 读方成对**形态,与 `DiseaseActionAxisCookedWriter` 同款。
//   唯一自动兜现 = `DiseaseRegistryBaker.Materialize` 的真往返(改一侧布局不改进另一侧 ⇒ 那里红)。
//
// ⚠️ **2026-10-22 布局扩展(产物首次落盘前的定稿)**:载荷前加两个**全局**量
//    `trend_window_ticks` / `trend_eps`(GDD Tuning §四 `:1743-1744`)。
//    在此之前这两个量只活在 `BakeOutput` 里、**不透传进字节** ⇒ 生产路径拿不到值,
//    F4 的痊愈支 / 迁延支永远「未配置」(B1 缺口的②号断点)。
//    ⚠️ **没有格式版本升级**:本产物**从未在盘上存在过**(git 历史零笔),此刻是它的
//       **首次定义**,不存在需要迁移的旧字节。`CookedFormat.FormatVersion` 是**全局**格式号
//       (20 字节头布局),不随单个产物的载荷演化;单个产物的载荷演化由**头槽 `schemaVersion`**
//       (来自作者态 `schema_version`)承载 —— 与 `TelemetryAnalyticsCookedCodec` 同口径。
//       将来真改载荷形状(如补 `relapse` 块)时,须同步升作者态 `schema_version`。
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
        /// <param name="rows">注册表行集(<c>null</c> ⇒ 行数写 <c>-1</c>,读方拒收)。</param>
        /// <param name="schemaVersion">作者态 <c>schema_version</c>(进头槽 offset 8)。</param>
        /// <param name="configVersion">源内容哈希派生的 <c>ConfigVersion</c>(进头槽 offset 12)。</param>
        /// <param name="trendWindowTicks">全局 <c>W</c>(趋势差分窗口,tick;<c>0</c> = 未配置)。</param>
        /// <param name="trendEpsRaw">全局 <c>TREND_EPS</c>(Q16.16 raw;<c>0</c> = 未配置)。</param>
        public static byte[] Write(IReadOnlyList<DiseaseRegistryEntry> rows,
                                   uint schemaVersion, uint configVersion,
                                   long trendWindowTicks = 0L, long trendEpsRaw = 0L)
        {
            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                // ── 载荷首字段:schema_version 回声(与头槽 offset 8 同值)──
                //  ⚠️ 两处存同一个值**是刻意的**(承 `TelemetryAnalyticsCookedWriter` 先例):
                //     头槽供「不解析载荷就能判版本」的快速拒收;载荷内那份让「头槽被手改成
                //     能过的值、载荷却是旧布局」这种损坏**也能被抓到**(两者不等即拒)。
                w.Write(schemaVersion);                  // u32

                // ── 全局趋势窗口(载荷前两个量;固定字段顺序)──
                //  ⚠️ 放在行数**之前**:它们是「整份产物」的属性,不是某一行的。
                //     读方按同一顺序解(见 DiseaseRegistryCookedCodec)。
                w.Write(trendWindowTicks);               // i64 W
                w.Write(trendEpsRaw);                    // i64 TREND_EPS(raw)

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
//   ① ✅ **读方 codec 已补立**(2026-10-22)—— `Gameplay.Presentation/DiseaseRegistryCookedCodec.cs`
//      逐字段镜像本件的书写序。原文「读方 codec 不存在 ⇒ 产物无人消费」**已失效**(原文不删)。
//      ⚠️ 但「有读方」≠「生产路径真的读它」:生产装配(`CompositionRoot`)是否接上,
//      以装配层的自述为准,**不在本件的判据面内**。
//   ② `Relapse` / `TreatableBy` / `Signs` **仍未编码** —— 当前数据全为 `[]` / `null`。
//      ⚠️ 原文写「布局须扩展且**格式版本号须 +1**」—— **该口径已就地订正**:
//      `CookedFormat.FormatVersion` 是**全局** 20 字节头格式号,**不随单个产物载荷演化**;
//      单个产物的载荷演化由头槽 `schemaVersion`(作者态 `schema_version`)承载。
//      真补这三块时:① 布局扩展 ② 作者态 `schema_version` +1 ③ 同步改读方与 `Materialize` 的逐项比对。
//   ③ `Handle` **未编码** —— 它是派生字段,当前无人算(见 Binder 文件尾 ①)。
//      ⚠️ 影响面已核实:全库唯一消费方 `CareLeverApplicability.CareApplicableConsistent`
//      对 `Handle == null` 返回 `true`(「未声明不是不一致」)⇒ 不编码不制造假红,
//      但也意味着**一致性判据在真表上恒不触发**。属欠账,归派生逻辑轮。
