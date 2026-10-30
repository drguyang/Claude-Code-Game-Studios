// T1 · S9 —— 53 用药代价表(53_cost_table.json)的 deterministic cooked 写入器(阶段 2 烘焙的写面)。
//
// 权威来源:
//   ADR-014 §二(同源两次烘焙逐位一致:固定字段顺序 · 零字典迭代 · 零本地时间)
//   CookedFormat.cs(20 字节头布局的唯一共参照 —— 本件必须与之严格镜像)
//   ADR-035 §Decision ②(效果一律外键、全整数域、零 Fix)· ③(CostTable53 的运行期读形)
//   CostTableBinder.BindResult(行集来源;全部构建期校验在那一侧)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//    row_count : i32(0 = **空表,合法** —— 见下)
//    row_count × { harm_kind: i32, band: i32, injury_id: i32, severity_band: i32 }
//
// ⚠️ **空表合法**:S2 的 `rows: []` 是惰性形态(机制未启用),与 `DelayTableCookedWriter`
//   「空即抛」**刻意不同** —— 那张表空 = 53 永不发后果(静默失败);本表空 = 代价机制未启用
//   (ADR-035 §⑥ / S2 的已裁形态)。⇒ 本件**不**加空集守卫。
//
// ⚠️ 行序 = `(harm_kind, band)` 升序。ADR-035 §Decision ② 已把该二元组钉为**单射**键
//   ⇒ 它对行集是**全序**(唯一) ⇒ 不依赖 `Dictionary` 迭代序,同源两次烘焙逐位一致。
//
// ⚠️ **为什么产物里是整数 `injury_id` 而不是作者态字符串**:运行期 `CostTable53.Row.InjuryId`
//   是 `int`(S4 已落),而 ADR-014 要求「玩家构建零 JSON 解析器」⇒ 字符串 → 整数的解析
//   必须发生在**烘焙期**。解析器 = `CostTable53Bake`(它拿 9 的注册表),本件只做编码。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>53 用药代价表的 deterministic cooked 写入器。</summary>
    internal static class CostTableCookedWriter
    {
        /// <summary>编码代价表(含 20 字节头)。</summary>
        /// <param name="bound">绑定结果(行集 / schema_version)。</param>
        /// <param name="injuryIdByKey">作者态 `injury_id` 键 → 9 注册表的整数 id(**已由 Bake 层解析**)。</param>
        /// <param name="configVersion">源数据集内容哈希派生的 ConfigVersion。</param>
        /// <exception cref="ArgumentNullException"><paramref name="injuryIdByKey"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">键无法解析 / `harm_kind` 越出闭集 / 键重复。
        /// 三者都应由 Binder 拦在前面 ⇒ 抵达即「Binder 与写方口径脱钩」,fail-loud 而不是写出坏产物。</exception>
        public static byte[] Write(in CostTableBinder.BindResult bound,
                                   IReadOnlyDictionary<string, int> injuryIdByKey,
                                   uint configVersion)
        {
            if (injuryIdByKey == null) throw new ArgumentNullException(nameof(injuryIdByKey));

            var rows = new List<(int HarmKind, int Band, int InjuryId, int SeverityBand)>();
            if (bound.Rows != null)
            {
                for (int i = 0; i < bound.Rows.Count; i++)
                {
                    CostTableBinder.CostRow r = bound.Rows[i];
                    int harmKind = HarmKindOrdinal(r.HarmKind, i);
                    if (!injuryIdByKey.TryGetValue(r.InjuryId ?? string.Empty, out int injuryId))
                        throw new InvalidOperationException(
                            $"代价表 rows[{i}] injury_id「{r.InjuryId}」无法解析为 9 注册表的整数 id —— " +
                            "N-3 应已在 Binder 侧拒收;抵达此处 = Binder 与写方口径脱钩");
                    rows.Add((harmKind, r.Band, injuryId, r.SeverityBand));
                }
            }

            // 行序 = (harm_kind, band) 升序 —— 该二元组是单射键 ⇒ 全序(见文件头)。
            rows.Sort((a, b) =>
            {
                int cmp = a.HarmKind.CompareTo(b.HarmKind);
                if (cmp != 0) return cmp;
                cmp = a.Band.CompareTo(b.Band);
                if (cmp != 0) return cmp;
                cmp = a.InjuryId.CompareTo(b.InjuryId);          // 防御破平(单射下恒不触)
                if (cmp != 0) return cmp;
                return a.SeverityBand.CompareTo(b.SeverityBand);
            });

            for (int i = 1; i < rows.Count; i++)
            {
                if (rows[i].HarmKind == rows[i - 1].HarmKind && rows[i].Band == rows[i - 1].Band)
                    throw new InvalidOperationException(
                        $"代价表物化键碰撞:(harm_kind={rows[i].HarmKind}, band={rows[i].Band}) 出现两次 —— " +
                        "ADR-035 §Decision ② 要求该二元组单射(应已由 Binder 拒收)");
            }

            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                w.Write(rows.Count);
                for (int i = 0; i < rows.Count; i++)
                {
                    w.Write(rows[i].HarmKind);
                    w.Write(rows[i].Band);
                    w.Write(rows[i].InjuryId);
                    w.Write(rows[i].SeverityBand);
                }

                // ── 尾段:过量档位线(2026-10-30)─────────────────────────────
                // ⚠️ **尾段式**,不动上面的逐行布局。理由与 `PrescriptionActionsCookedWriter`
                //   同一个:行内插字段会让**旧产物**解出错位数据(静默);尾段式在旧产物上
                //   读越界 ⇒ 抛(fail-loud),而「旧产物读不出新段」本来就是要重烘的信号。
                // 布局:`threshold_count: i32` + 逐项 `Fix.raw: i64`(升序,由 Binder 保证)。
                w.Write(bound.OverdoseBandThresholds?.Count ?? 0);
                if (bound.OverdoseBandThresholds != null)
                {
                    for (int i = 0; i < bound.OverdoseBandThresholds.Count; i++)
                        w.Write(bound.OverdoseBandThresholds[i].Raw);
                }

                w.Flush();
                return FinishHeader(payload.ToArray(), bound.SchemaVersion, configVersion);
            }
        }

        /// <summary>作者态 `harm_kind` 字符串 → <see cref="CostHarmKind"/> 序数。</summary>
        /// <exception cref="InvalidOperationException">越出闭集(应已由 Binder 拒收)。</exception>
        private static int HarmKindOrdinal(string harmKind, int i)
        {
            if (harmKind == CostTableBinder.KindContraindication)
                return (int)CostHarmKind.Contraindication;
            if (harmKind == CostTableBinder.KindOverdose)
                return (int)CostHarmKind.Overdose;
            throw new InvalidOperationException(
                $"代价表 rows[{i}] harm_kind「{harmKind}」越出闭集 " +
                "{{contraindication, overdose}} —— 应已由 Binder 拒收");
        }

        /// <summary>头(magic + formatVersion + schemaVersion + configVersion + payloadLength)+ 载荷。</summary>
        /// <remarks>⚠️ 与 <see cref="DelayTableCookedWriter"/> 逐字同形(**刻意重复**而非抽公共件):
        /// 抽公共件会改动既有已结案的写入器,收益 = 4 个小方法,风险 = 动 6 张表的产物字节。
        /// 若将来第三张表出现,再一次性收敛(记为已知重复,不假装没有)。</remarks>
        private static byte[] FinishHeader(byte[] payload, uint schemaVersion, uint configVersion)
        {
            var output = new byte[CookedFormat.HeaderSize + payload.Length];
            string magic = CookedFormat.Magic;
            output[0] = (byte)magic[0];
            output[1] = (byte)magic[1];
            output[2] = (byte)magic[2];
            output[3] = (byte)magic[3];
            WriteU32Le(output, 4, CookedFormat.FormatVersion);
            WriteU32Le(output, 8, schemaVersion);   // 头槽承载 schema_version(不在载荷里重复)
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
