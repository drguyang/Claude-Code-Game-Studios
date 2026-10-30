// T1 · S4 —— 禁忌 / 过量代价表(53_cost_table.json)的 **Sim 侧整数域只读视图**。
//
// 权威来源:
//   ADR-035 §Decision ②(表形状:效果一律外键、全整数域、零 Fix)/ ③(L3 = 运行期纵深防御)
//   ADR-035 §Decision ⑥(过量支 P0 挂起 ⇒ 表里出现 Overdose 行一律拒)
//   ADR-035 §Decision ④(载荷里的 `harm_kind` 是本文件的 `CostHarmKind`)
//   形态先例:Sim/Consequences/DelayTable53.cs(构造器义务由绑定器主校验 + Sim 侧复述拒收)
//
// ⚠️ **本文件与绑定器的分工**(照 `DelayTable53` 的双点形态):
//   · 构建期**主校验**在 `Editor.Tools.Bake/CostTableBinder`(作者态字符串 → 校验 → 落 cooked);
//   · 本文件是**运行期纵深防御**:有人**绕过 Bake 直接构造**也拿不到违规表。
//   ⇒ 两侧判据必须一致;不一致时**以更严的一侧为准**(本文件不放松任何一条)。
//
// ⚠️ **为什么 L3 也要查 `lethal`**:ADR-035 §Decision ③ 的 L3 原文是「**复述同一拒收**」。
//   若构造器只查「id 在注册表内」而不查 `lethal == false`,绕过 Bake 直构就能把**致死伤情**
//   塞进表里 ⇒ 「永不致死」只剩一条构建期路径在守。故本文件把 N-1 一并复述
//   (比 §Key Interfaces 的草稿多一条,按 §Decision ③ 的「同一拒收」执行)。
//
// ⚠️ **零 float / 零字符串**:运行期只认整数 id 与档序数;作者态字符串 → 整数的转换
//   是烘焙期的职责(本表不认识 `"INJ_HEMORRHAGE"` 这种字面量)。⇒ `N-3`(结局轴**符号**)
//   在整数域里退化为「id 不在注册表内」——同一个拒收,不另设分支。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Consequences
{
    /// <summary>代价表的运行期只读视图(`(harm_kind, band)` → 伤情 id + 程度档)。</summary>
    public static class CostTable53
    {
        /// <summary>一行代价(**全整数**;效果 = 外键,零伤害属性副本)。</summary>
        public readonly struct Row
        {
            /// <summary>伤害种类(<see cref="CostHarmKind"/> 的 ordinal)。</summary>
            public readonly int HarmKind;

            /// <summary>档序数(同 kind 内单射)。</summary>
            public readonly int Band;

            /// <summary>外键 → 9 注册表的**伤情** `DiseaseId`(构造期已验 `lethal == false`)。</summary>
            public readonly int InjuryId;

            /// <summary>程度档(引用 9 域的档位,**不是**后果分值 —— `AC-53-01`)。</summary>
            public readonly int SeverityBand;

            public Row(int harmKind, int band, int injuryId, int severityBand)
            {
                HarmKind = harmKind; Band = band; InjuryId = injuryId; SeverityBand = severityBand;
            }
        }

        /// <summary>查表结果只读视图。</summary>
        public sealed class Table
        {
            private readonly Dictionary<long, Row> _rows;

            /// <summary>构造并做纵深防御拒收(见文件头)。</summary>
            /// <param name="registry">9 的疾病注册表行(只读;为 <c>null</c> 时**只允许**空表)。</param>
            /// <param name="rows">代价行(可空 = 空表)。**构造期拷贝** ⇒ 外部集合事后改写不影响本表。</param>
            /// <param name="maxSeverityBand">程度档上界;<c>null</c> = 未声明(值归内容轮,不校上界)。</param>
            /// <exception cref="ArgumentNullException">有行而 <paramref name="registry"/> 为 null。</exception>
            /// <exception cref="ArgumentException">任一拒收(见各 <c>Reject*</c>)。</exception>
            public Table(IReadOnlyList<DiseaseRegistryEntry> registry, IReadOnlyList<Row> rows,
                         int? maxSeverityBand)
            {
                _rows = new Dictionary<long, Row>();
                int count = rows?.Count ?? 0;
                if (count == 0) return; // 空表 = 合法惰性形态(S2 骨架)
                if (registry == null)
                    throw new ArgumentNullException(nameof(registry),
                        "代价表有行却没有疾病注册表 —— 外键无处可验(N-1 不可执行)");

                var byId = IndexRegistry(registry);
                for (int i = 0; i < count; i++)
                {
                    Row row = rows[i];
                    RejectHarmKind(row.HarmKind);
                    RejectNegative(row, i);
                    if (maxSeverityBand != null && row.SeverityBand > maxSeverityBand.Value)
                        throw new ArgumentException(
                            $"costTable[{i}] severity_band={row.SeverityBand} > max_severity_band={maxSeverityBand.Value}");
                    RejectNonNonLethalInjury(row, i, byId);

                    long key = Key(row.HarmKind, row.Band);
                    if (_rows.ContainsKey(key))
                        throw new ArgumentException(
                            $"costTable[{i}] 重复键 (harm_kind={row.HarmKind}, band={row.Band}) —— " +
                            "(harm_kind, band) 须单射(ADR-035 §Decision ②)");
                    _rows.Add(key, row);
                }
            }

            /// <summary>表是否为空(空 = 惰性:任何查询都 miss,行为与「机制未启用」逐位一致)。</summary>
            public bool IsEmpty => _rows.Count == 0;

            /// <summary>行数。</summary>
            public int RowCount => _rows.Count;

            /// <summary>查一行。</summary>
            /// <returns><c>false</c> = 无此行(**合法 miss**,不是错误、不抛)。</returns>
            public bool TryLookup(int harmKind, int band, out int injuryId, out int severityBand)
            {
                if (_rows.TryGetValue(Key(harmKind, band), out Row row))
                {
                    injuryId = row.InjuryId;
                    severityBand = row.SeverityBand;
                    return true;
                }
                injuryId = 0;
                severityBand = 0;
                return false;
            }

            /// <summary>`(kind, band)` 压一个 long 作字典键(两者都是非负 int)。</summary>
            private static long Key(int harmKind, int band) => ((long)harmKind << 32) | (uint)band;
        }

        /// <summary>表物化入口(装配 / 测试;与 <see cref="DelayTable53.CreateTable"/> 同形)。</summary>
        public static Table CreateTable(IReadOnlyList<DiseaseRegistryEntry> registry, IReadOnlyList<Row> rows,
                                        int? maxSeverityBand)
            => new Table(registry, rows, maxSeverityBand);

        // ═════════════════════ 纵深防御拒收 ═════════════════════

        /// <summary>`harm_kind` 闭集闸:未知值一律拒。</summary>
        /// <remarks>
        /// 🔁 **2026-10-30:过量支解除挂起** —— `ADR-035 §Decision ⑥` 的解除条件是
        /// 「`TD-sim-02` + `TD-prescription-09` 两条均结案」,两者当日均已 closed:
        /// · `TD-prescription-09`:禁忌判定的承载 + 判定两侧(2026-10-30 早批);
        /// · `TD-sim-02`:9 侧新增 `ProgressionEvaluator.ComputePeakDrugLoad` +
        ///   `VitalsDto.PeakDrugLoad`(独立状态量,不进 `Progress`)。
        /// ⇒ 原「出现 `harm_kind=Overdose` 一律拒」的分支**撤销**,改为与其他合法值同路。
        /// ⚠️ 但「表里能放过量行」≠「过量支已启用」—— 启用与否由**作者态**
        /// `53_cost_table.json` 的 `harm_kind_enabled` 决定,构建期由 `CostTableBinder` 校验。
        /// </remarks>
        private static void RejectHarmKind(int harmKind)
        {
            if (harmKind == (int)CostHarmKind.Contraindication) return;
            if (harmKind == (int)CostHarmKind.Overdose) return;   // 🔁 2026-10-30 解除(见 remarks)
            throw new ArgumentException(
                $"代价表 harm_kind={harmKind} ∉ 闭集 {{Contraindication, Overdose}}");
        }

        /// <summary>档序数非负。</summary>
        private static void RejectNegative(Row row, int i)
        {
            if (row.Band < 0)
                throw new ArgumentException($"costTable[{i}] band={row.Band} < 0 —— 档序数自 0 起");
            if (row.SeverityBand < 0)
                throw new ArgumentException($"costTable[{i}] severity_band={row.SeverityBand} < 0");
        }

        /// <summary>N-1:外键必须存在、必须是伤情、必须 `lethal == false`。</summary>
        private static void RejectNonNonLethalInjury(Row row, int i, Dictionary<int, DiseaseRegistryEntry> byId)
        {
            if (!byId.TryGetValue(row.InjuryId, out DiseaseRegistryEntry e))
                throw new ArgumentException(
                    $"costTable[{i}] injury_id={row.InjuryId} 不在疾病注册表内 —— 外键失效" +
                    "(ADR-035 §Decision ②:效果**一律外键**)");
            if (e.Kind != DiseaseKind.Injury)
                throw new ArgumentException(
                    $"costTable[{i}] injury_id={row.InjuryId}「{e.DiseaseKey}」的 kind = {e.Kind} ≠ injury —— " +
                    "本字段指向 9 的**伤情**注册表闭集");
            if (e.Lethality != 0)
                throw new ArgumentException(
                    $"costTable[{i}] injury_id={row.InjuryId}「{e.DiseaseKey}」的 lethal = true —— " +
                    "**N-1(永不致死)**:白名单只能指非致死伤情");
        }

        /// <summary>按 `DiseaseId` 建索引(忽略键为空的行 —— 与绑定器同口径)。</summary>
        private static Dictionary<int, DiseaseRegistryEntry> IndexRegistry(IReadOnlyList<DiseaseRegistryEntry> registry)
        {
            var byId = new Dictionary<int, DiseaseRegistryEntry>();
            for (int i = 0; i < registry.Count; i++)
            {
                DiseaseRegistryEntry e = registry[i];
                if (e == null || string.IsNullOrEmpty(e.DiseaseKey)) continue;
                byId[e.DiseaseId] = e;
            }
            return byId;
        }
    }
}
