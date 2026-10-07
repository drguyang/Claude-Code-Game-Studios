// ADR-014 / GDD disease-simulation.md R1 —— 注册表 schema 与构建期校验。
//
// 权威来源:
//   ADR-014 §二 —— 两阶段烘焙：阶段 1 JsonTextReader 仅词法，阶段 2 自研 per-schema 绑定
//   GDD disease-simulation.md R1 —— 17 条构建期校验
//   ADR-017 §二 —— 门 A 硬化
//
// 核心机制:
//   - 17 条校验全为构建期硬失败（throw）
//   - 负夹具每条至少一个「只违该条」的最小反例
//   - Fix 字段必须写字符串 "3/4"，禁 JSON 数字字面量

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// treatable_by 关系行(处置 × 病种 → 极性)。
    /// </summary>
    public sealed class TreatableByEntry
    {
        /// <summary>处置 id(须 ∈ 处置轴闭集)。</summary>
        public int ActionId;

        /// <summary>极性(causal / symptomatic)。</summary>
        public string Polarity;

        public TreatableByEntry(int actionId, string polarity)
        {
            ActionId = actionId; Polarity = polarity;
        }
    }

    /// <summary>
    /// 病种注册表项。
    /// </summary>
    public sealed class DiseaseRegistryEntry
    {
        public int DiseaseId;
        public string DiseaseKey;
        public int Polarity; // 0 = 寒, 1 = 热, 2 = 平
        public int Severity; // 1-5
        public int Contagion; // 0-3
        public int Lethality; // 0-3
        public int TreatmentDifficulty; // 1-10
        public int RecoveryTime; // ticks
        public int RelapseChance; // 0-100
        public int ComorbidityFactor; // 0-100
        public int SeasonalMod; // 0-100
        public int AgeMod; // 0-100
        public int GenderMod; // 0-100
        public int OccupationMod; // 0-100
        public int RegionMod; // 0-100
        public int ClimateMod; // 0-100

        /// <summary>
        /// treatable_by 关系(处置 × 病种 → 极性)。
        /// ⚠️ 2026-10-07 补:9 GDD R1.3 把 treatable_by[] 定义为病种条目内的字段,
        /// 但病种条目的其余字段数值全冻结(曲线/严重度/传染性等),故 treatable_by
        /// 拆到独立文件 `disease_action_axis.json`(见 story-007)。
        /// 本字段是**派生字段**(写入期不填,构建期从 treatable_by 极性 / 病种 id 计算并校验)。
        /// </summary>
        public TreatableByEntry[] TreatableBy;

        /// <summary>
        /// handle 派生字段(写入期不填,构建期从 treatable_by 极性 / 病种 id 计算并校验)。
        /// 规则九(GDD `:329`):causal ⇔ ∃对因处置;否则 ∈ {伤寒, 痢疾, 心衰} ⇒ care;
        /// DIS_TETANUS ⇒ none;其余 ⇒ symptomatic_only。
        /// </summary>
        public string Handle;
    }

    /// <summary>
    /// 注册表 schema 校验器 —— 17 条构建期校验。
    /// </summary>
    public static class RegistrySchemaValidator
    {
        // R1 校验规则号
        public const int R1_CHECK_01 = 1; // DiseaseId 唯一
        public const int R1_CHECK_02 = 2; // DiseaseKey 非空
        public const int R1_CHECK_03 = 3; // Polarity ∈ {0,1,2}
        public const int R1_CHECK_04 = 4; // Severity ∈ [1,5]
        public const int R1_CHECK_05 = 5; // Contagion ∈ [0,3]
        public const int R1_CHECK_06 = 6; // Lethality ∈ [0,3]
        public const int R1_CHECK_07 = 7; // TreatmentDifficulty ∈ [1,10]
        public const int R1_CHECK_08 = 8; // RecoveryTime > 0
        public const int R1_CHECK_09 = 9; // RelapseChance ∈ [0,100]
        public const int R1_CHECK_10 = 10; // ComorbidityFactor ∈ [0,100]
        public const int R1_CHECK_11 = 11; // SeasonalMod ∈ [0,100]
        public const int R1_CHECK_12 = 12; // AgeMod ∈ [0,100]
        public const int R1_CHECK_13 = 13; // GenderMod ∈ [0,100]
        public const int R1_CHECK_14 = 14; // OccupationMod ∈ [0,100]
        public const int R1_CHECK_15 = 15; // RegionMod ∈ [0,100]
        public const int R1_CHECK_16 = 16; // ClimateMod ∈ [0,100]
        public const int R1_CHECK_17 = 17; // 至少 1 个病种
        public const int R1_CHECK_18 = 18; // treatable_by[].polarity ∈ {causal, symptomatic}
        public const int R1_CHECK_19 = 19; // treatable_by[].action ∈ 处置轴闭集

        /// <summary>
        /// 校验病种注册表 —— 17 条构建期校验。
        /// </summary>
        public static void Validate(List<DiseaseRegistryEntry> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            // R1-17: 至少 1 个病种
            if (entries.Count < 1)
                throw new RegistryValidationException(R1_CHECK_17, "至少需要 1 个病种");

            var diseaseIds = new HashSet<int>();

            foreach (var entry in entries)
            {
                // R1-01: DiseaseId 唯一
                if (!diseaseIds.Add(entry.DiseaseId))
                    throw new RegistryValidationException(R1_CHECK_01, $"DiseaseId 重复: {entry.DiseaseId}");

                // R1-02: DiseaseKey 非空
                if (string.IsNullOrEmpty(entry.DiseaseKey))
                    throw new RegistryValidationException(R1_CHECK_02, "DiseaseKey 不能为空");

                // R1-03: Polarity ∈ {0,1,2}
                if (entry.Polarity < 0 || entry.Polarity > 2)
                    throw new RegistryValidationException(R1_CHECK_03, $"Polarity 越界: {entry.Polarity}");

                // R1-04: Severity ∈ [1,5]
                if (entry.Severity < 1 || entry.Severity > 5)
                    throw new RegistryValidationException(R1_CHECK_04, $"Severity 越界: {entry.Severity}");

                // R1-05: Contagion ∈ [0,3]
                if (entry.Contagion < 0 || entry.Contagion > 3)
                    throw new RegistryValidationException(R1_CHECK_05, $"Contagion 越界: {entry.Contagion}");

                // R1-06: Lethality ∈ [0,3]
                if (entry.Lethality < 0 || entry.Lethality > 3)
                    throw new RegistryValidationException(R1_CHECK_06, $"Lethality 越界: {entry.Lethality}");

                // R1-07: TreatmentDifficulty ∈ [1,10]
                if (entry.TreatmentDifficulty < 1 || entry.TreatmentDifficulty > 10)
                    throw new RegistryValidationException(R1_CHECK_07, $"TreatmentDifficulty 越界: {entry.TreatmentDifficulty}");

                // R1-08: RecoveryTime > 0
                if (entry.RecoveryTime <= 0)
                    throw new RegistryValidationException(R1_CHECK_08, $"RecoveryTime 必须 > 0: {entry.RecoveryTime}");

                // R1-09: RelapseChance ∈ [0,100]
                if (entry.RelapseChance < 0 || entry.RelapseChance > 100)
                    throw new RegistryValidationException(R1_CHECK_09, $"RelapseChance 越界: {entry.RelapseChance}");

                // R1-10: ComorbidityFactor ∈ [0,100]
                if (entry.ComorbidityFactor < 0 || entry.ComorbidityFactor > 100)
                    throw new RegistryValidationException(R1_CHECK_10, $"ComorbidityFactor 越界: {entry.ComorbidityFactor}");

                // R1-11: SeasonalMod ∈ [0,100]
                if (entry.SeasonalMod < 0 || entry.SeasonalMod > 100)
                    throw new RegistryValidationException(R1_CHECK_11, $"SeasonalMod 越界: {entry.SeasonalMod}");

                // R1-12: AgeMod ∈ [0,100]
                if (entry.AgeMod < 0 || entry.AgeMod > 100)
                    throw new RegistryValidationException(R1_CHECK_12, $"AgeMod 越界: {entry.AgeMod}");

                // R1-13: GenderMod ∈ [0,100]
                if (entry.GenderMod < 0 || entry.GenderMod > 100)
                    throw new RegistryValidationException(R1_CHECK_13, $"GenderMod 越界: {entry.GenderMod}");

                // R1-14: OccupationMod ∈ [0,100]
                if (entry.OccupationMod < 0 || entry.OccupationMod > 100)
                    throw new RegistryValidationException(R1_CHECK_14, $"OccupationMod 越界: {entry.OccupationMod}");

                // R1-15: RegionMod ∈ [0,100]
                if (entry.RegionMod < 0 || entry.RegionMod > 100)
                    throw new RegistryValidationException(R1_CHECK_15, $"RegionMod 越界: {entry.RegionMod}");

                // R1-16: ClimateMod ∈ [0,100]
                if (entry.ClimateMod < 0 || entry.ClimateMod > 100)
                    throw new RegistryValidationException(R1_CHECK_16, $"ClimateMod 越界: {entry.ClimateMod}");

                // R1-18: treatable_by[].polarity ∈ {causal, symptomatic}
                if (entry.TreatableBy != null)
                {
                    for (int i = 0; i < entry.TreatableBy.Length; i++)
                    {
                        var tb = entry.TreatableBy[i];
                        if (tb.Polarity != "causal" && tb.Polarity != "symptomatic")
                            throw new RegistryValidationException(R1_CHECK_18,
                                $"treatable_by[{i}].polarity=\"{tb.Polarity}\" ∉ {{causal, symptomatic}}");
                    }
                }

                // R1-19: treatable_by[].action ∈ 处置轴闭集
                // ⚠️ 2026-10-07 补:9 GDD R1.3 把 treatable_by[] 定义为病种条目内的字段,
                //    但病种条目的其余字段数值全冻结,故 treatable_by 拆到独立文件
                //    `disease_action_axis.json`(见 story-007)。
                //    本校验在**完整 disease_registry.json** 存在时生效(当前不存在)。
                //    校验逻辑:action ∈ 处置轴闭集(由 DiseaseActionAxisBaker 产出)。
                //    ⚠️ 当前数据集:病种条目不存在 ⇒ 本校验**结构性不可达**(登记为已知弱点)。
                // ⚠️ 2026-10-07 评审 M1:空实现显式标记为未实现 —— 调用方无法区分
                //    「校验通过」与「校验未实现」。
                if (entry.TreatableBy != null)
                {
                    throw new NotImplementedException(
                        "R1-19: treatable_by[].action ∈ 处置轴闭集 —— " +
                        "待 disease_registry.json 存在后实现(当前结构性不可达)。" +
                        "登记为已知弱点:9 的 C# 16 字段 + 17 条区间校验 vs GDD §R1 的 17 条语义校验" +
                        "是**既有**问题,不属本轮授权面。归 9 的下一轮。");
                }
            }
        }
    }

    /// <summary>
    /// 注册表校验异常。
    /// </summary>
    public sealed class RegistryValidationException : Exception
    {
        public int RuleNumber { get; }

        public RegistryValidationException(int ruleNumber, string message)
            : base($"R1-{ruleNumber:D2}: {message}")
        {
            RuleNumber = ruleNumber;
        }
    }
}
