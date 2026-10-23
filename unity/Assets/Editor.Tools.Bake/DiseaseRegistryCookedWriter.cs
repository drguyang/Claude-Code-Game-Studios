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
// ⚠️ **2026-10-23 布局再扩展(schema_version 1 → 2)**:curve 块之后追加
//    `handle`(字符串)/ `relapse`(bool + 条件三字段)/ `signs[]`(i32 计数,-1 = null)。
//    这正是上一段预告的那件事,已按预告执行:作者态 `schema_version` 同步 1 → 2。
//    `treatable_by` **不写** —— 轴是单一真源(`D-9-K`),运行期走 `ITreatableByLookup`。
//    ⚠️ 盘的旧产物(若存在)会因版本门被读方拒收(E-13)—— 重烘即可,无迁移需求。
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

            // ── handle(2026-10-23 起,派生字段)──
            // null = 未派生(轴未接线 / 未经烘焙器直构的夹具)⇒ 写 null 标记,读方还原 null。
            WriteString32(w, e.Handle);

            // ── relapse(2026-10-23 起)──
            // null = 无复发块(合法形态,GDD R1.3)。写 bool 区分,读方据此跳过整块。
            w.Write(e.Relapse != null);
            if (e.Relapse != null)
            {
                w.Write(e.Relapse.ARel.Raw);              // Fix = raw long
                w.Write(e.Relapse.Interval);              // int
                w.Write(e.Relapse.TauFall.Raw);           // Fix = raw long
            }

            // ── signs[](2026-10-23 起)──
            // null = 未声明(R1-29 结构性跳过);空数组 = 显式空集(合法,GDD :291)。
            // ⚠️ 两者必须区分 —— 用 i32 计数并把 null 写成 -1(与 rows 的 null 标记同法)。
            if (e.Signs == null)
            {
                w.Write(-1);
            }
            else
            {
                w.Write(e.Signs.Length);
                for (int i = 0; i < e.Signs.Length; i++)
                {
                    SignEntry s = e.Signs[i];
                    if (s == null)
                    {
                        // null 词条:写 null 标记(-1 字节长)+ 0 通道位,读方还原 null。
                        // ⚠️ R1-29 已拒收 null 词条 —— 这里仍要能编码,因为写方
                        //    可能在**未经校验**的调用路径上被驱动(测试直构)。
                        w.Write(-1);
                        w.Write(0);
                        continue;
                    }
                    WriteString32(w, s.SignKey);
                    w.Write(s.ChannelMask);
                }
            }

            // ── treatable_by:**刻意不写** ──
            //   `disease_action_axis.json` 是单一真源(D-9-K),运行期查轴
            //   (`ITreatableByLookup`)。烘一份进来 = 第二真源、可分叉。
            //   ⇒ 读方把 `TreatableBy` 恒置 null,本处不写任何字节。
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
//   ② ~~`Relapse` / `TreatableBy` / `Signs` **仍未编码**~~ —— **2026-10-23 已兑现**:
//      `Relapse` / `Signs` 已编码(curve 块之后);`TreatableBy` **按裁定不编码**
//      (轴是单一真源 `D-9-K`,运行期走 `ITreatableByLookup`)—— 这是**设计**,不是欠账。
//      ⚠️ 原文写「布局须扩展且**格式版本号须 +1**」—— **该口径已就地订正**:
//      `CookedFormat.FormatVersion` 是**全局** 20 字节头格式号,**不随单个产物载荷演化**;
//      单个产物的载荷演化由头槽 `schemaVersion`(作者态 `schema_version`)承载。
//      本轮实际做的是:① 布局扩展 ② 作者态 `schema_version` **1 → 2** ③ 同步改读方与
//      `Materialize` 的逐项比对(三条**全部落地**,见 `CompareRow`)。
//   ③ ~~`Handle` **未编码** —— 它是派生字段,当前无人算~~ —— **2026-10-23 已兑现**:
//      派生逻辑落在 `Sim/DiseaseHandle.Derive`(binder 与测试共用一份实现),
//      binder 由 `DiseaseRegistryBaker.BakeFromRepo` 从 `disease_action_axis.json`
//      算出「该病种是否有对因处置」后传入;本件只负责把结果写进字节。
//      ⚠️ 原「影响面」注仍成立且**未被本轮消除**:全库唯一消费方
//      `CareLeverApplicability.CareApplicableConsistent` 对 `Handle == null` 返回 `true`
//      ⇒ 不编码不制造假红;现在编码了,**判据在真表上会真的触发** ——
//      这正是本轮想要的效果(把「恒不触发」变成「能触发」)。
//      ⚠️ 但「能触发」≠「已被触发过」:真表 13 行当前 handle 值由派生规则算出,
//      该一致性判据**仍无一条测试钉住它在真表上的输出**,归 9 的验证轮。
