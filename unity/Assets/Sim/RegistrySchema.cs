// ADR-014 / GDD disease-simulation.md R1 —— 注册表 schema 与构建期校验。
//
// 权威来源:
//   ADR-014 §二 —— 两阶段烘焙：阶段 1 JsonTextReader 仅词法，阶段 2 自研 per-schema 绑定
//   GDD disease-simulation.md R1 —— 17 条构建期校验
//   GDD disease-simulation.md R1.3 —— natural_progress 字段 schema(curve.* / relapse.* / scale)
//   GDD disease-simulation.md F1/F2 —— Base/Relapse 分段与 SCALE_病种 投影
//   GDD disease-simulation.md §Visual/Audio 二 —— 体征六通道位域(signs[].channel_mask)
//   ADR-017 §二 —— 门 A 硬化
//
// 核心机制:
//   - 17 条校验全为构建期硬失败（throw）
//   - 负夹具每条至少一个「只违该条」的最小反例
//   - Fix 字段必须写字符串 "3/4"，禁 JSON 数字字面量
//   - 曲线面(R1-20…29,M2 阶段 2 批次 B;R1-30 为 BCD 码-1 收口补,2026-10-10)随 Curve 块同生:
//     Curve == null 的既有占位条目
//     整块结构性跳过(登记为已知弱点,新数据一律带 Curve)

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
    /// 体征 → 感知通道位域(P0 六通道)。
    /// 权威:GDD disease-simulation.md §Visual/Audio Requirements 二
    /// (2026-09-16 三轮「通道 = 位域整数」用户裁决②;2026-09-17 五→六增伤口位)。
    /// 分工三件套:9 只登记通道位(本表),8 拥有词表与阈值,13/44 按位表现。
    /// </summary>
    public static class SignChannels
    {
        public const int Face = 1 << 0;    // 面色(苍白 · 潮红 · 发黄 · 发青)
        public const int Voice = 1 << 1;   // 语声(有力 · 虚弱 · 含糊 · 不语)
        public const int Posture = 1 << 2; // 姿态(坐起 · 半卧 · 蜷缩 · 不动)
        public const int Breath = 1 << 3;  // 呼吸(平稳 · 急促 · 浅慢 · 停顿)
        public const int Touch = 1 << 4;   // 触感(发热 · 发凉 · 湿冷)
        public const int Wound = 1 << 5;   // 伤口(2026-09-17 新增;伤情持续期外显)

        /// <summary>六通道闭集 —— channel_mask 只允许本掩码内的位(写入期白名单)。</summary>
        public const int All = Face | Voice | Posture | Breath | Touch | Wound;
    }

    /// <summary>
    /// signs[] 词条(GDD R1 表 signs[] 行:体征词表引用,可为空集)。
    /// 「9 只保证体征是离散词」—— 词键与通道位是数据契约;词表内容与阈值归 8(§Visual/Audio 二)。
    /// </summary>
    public sealed class SignEntry
    {
        /// <summary>体征词键(8 拥有词表;9 只存引用,非空)。</summary>
        public string SignKey;

        /// <summary>通道位(∈ <see cref="SignChannels.All"/> 按位或,非零 —— 写入期白名单校验)。</summary>
        public int ChannelMask;

        public SignEntry(string signKey, int channelMask)
        {
            SignKey = signKey; ChannelMask = channelMask;
        }
    }

    /// <summary>
    /// natural_progress 曲线具名参数(GDD R1.3 schema;公式面 = F1 Base 分段)。
    /// 权威:GDD disease-simulation.md R1.3 字段表 + F1「Base(τ)」+ Tuning Knobs §二。
    /// ⚠️ 禁写成 AnimationCurve(F0:绑 UnityEngine、违规则七、不可跨平台)——
    /// 本组具名参数即其替代表达(R1.3 的存在理由)。
    /// </summary>
    public sealed class NaturalProgressCurve
    {
        /// <summary>F1 <c>incubation</c>:潜伏期 tick 数(必填;τ &lt; Incubation ⇒ Base ≡ 0,噪声同抑制)。</summary>
        public int Incubation;

        /// <summary>F1 <c>A_peak</c>:峰值幅度(必填,&gt; 0;GDD R1.3 校验 6)。</summary>
        public Fix APeak;

        /// <summary>F1 <c>τ_rise</c>:上升时间常数(单位 tick,必填 &gt; 0;越大越缓)。</summary>
        public Fix TauRise;

        /// <summary>F1 <c>τ_peak</c>:达峰时刻 tick(必填,须 &gt; Incubation —— GDD R1.3 校验 2)。</summary>
        public int TauPeak;

        /// <summary>F1 <c>τ_fall</c>:衰减时间常数(单位 tick;self_limit 时必填 &gt; 0,否则忽略)。</summary>
        public Fix TauFall;

        /// <summary>F1 <c>plateau</c>:迁延保持(默认 false;与 <see cref="SelfLimit"/> 互斥)。</summary>
        public bool Plateau;

        /// <summary>F1 <c>self_limit</c>:自愈衰减(默认 false;与 <see cref="Plateau"/> 互斥;
        /// 二者皆 false = 急性保持型,仅 lethal 病种合法 —— GDD R1.3 校验 4)。</summary>
        public bool SelfLimit;

        /// <summary>F1/F3 <c>σ</c>:噪声旋钮逐病种覆盖(GDD R1.3「全局默认;可逐病种覆盖」)。
        /// null = 未覆盖,用全局默认(全局 σ 默认值归数值轮,当前不存在)。</summary>
        public Fix? Sigma;

        /// <summary>F3 <c>boundary_mode</c>:必填白名单 "monotone" | "scan"(R1.3 字段表)。</summary>
        public string BoundaryMode;
    }

    /// <summary>
    /// relapse.* 复发块(GDD R1.3:「三字段同生共死」—— 块出现时三个必须齐全)。
    /// 公式面 = F1 <c>Relapse(τ) = A_rel × e^(−(τ − τ_rel)/τ_rel_fall) · [τ ≥ τ_rel]</c>,
    /// <c>τ_rel = relapse_interval × (复发次数 + 1)</c>。null = 该病种无复发。
    /// </summary>
    public sealed class RelapseCurve
    {
        /// <summary>F1 <c>A_rel</c>:复发峰值幅度(块存在时必填 &gt; 0)。</summary>
        public Fix ARel;

        /// <summary>F1 <c>relapse_interval</c>:复发周期 tick(必填 &gt; 0 —— GDD Edge Cases 写入期拒 0/负)。</summary>
        public int Interval;

        /// <summary>F1 <c>τ_rel_fall</c>:复发衰减时间常数(单位 tick;必填 &gt; 0)。</summary>
        public Fix TauFall;
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

        /// <summary>
        /// natural_progress 曲线(GDD R1.3 的 curve.* 块)。
        /// null = M2 批次 B 之前的既有占位条目(曲线字段未声明)—— 曲线面校验(R1-20…25/27/28/30)
        /// 对其结构性跳过,登记为已知弱点;新数据(disease_registry.json 装载面)一律非 null。
        /// </summary>
        public NaturalProgressCurve Curve;

        /// <summary>
        /// relapse.* 复发块(GDD R1.3)。null = 无复发(合法形态)。
        /// 有块时三字段校验(R1-26)独立于 Curve 是否存在。
        /// </summary>
        public RelapseCurve Relapse;

        /// <summary>
        /// F2 <c>SCALE_病种</c>:position 归一化分母(GDD R1.3「必填,须 &gt; 0」且 ≥ A_peak ——
        /// 校验 5;同时是 9 自己的状态机变量,不迁 8,见 F2 表)。
        /// 与曲线块同生校验:Curve == null 的既有条目不查(否则 Scale == 0 默认值必违)。
        /// </summary>
        public Fix Scale;

        /// <summary>
        /// signs[] 体征词表引用(GDD R1 表:可为空集)。词键 + 通道位是 9 的数据契约,
        /// 词表内容与阈值归 8(§Visual/Audio 二)。null = 未声明(既有条目),校验跳过。
        /// </summary>
        public SignEntry[] Signs;
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

        // ── 曲线面(M2 阶段 2 批次 B;编号 20+ 续在既有 19 条之后,每条映射 GDD R1.3 校验号)──
        public const int R1_CHECK_20 = 20; // curve.self_limit / plateau 互斥(GDD R1.3 校验 1)
        public const int R1_CHECK_21 = 21; // τ_peak > incubation ∧ τ_rise > 0 ∧ incubation ≥ 0(GDD 校验 2 + Tuning §二)
        public const int R1_CHECK_22 = 22; // 急性保持型(双 false)仅当 lethal(GDD 校验 4 前半;4b 需 DEATH_THRESHOLD —— 值待定,未实现)
        public const int R1_CHECK_23 = 23; // SCALE > 0 ∧ SCALE ≥ A_peak(GDD 校验 5)
        public const int R1_CHECK_24 = 24; // A_peak > 0(GDD 校验 6)
        public const int R1_CHECK_25 = 25; // self_limit ⇒ τ_fall > 0(GDD 校验 7)
        public const int R1_CHECK_26 = 26; // relapse 块 ⇒ a_rel > 0 ∧ interval > 0 ∧ τ_rel_fall > 0(GDD 校验 3「齐全」+ 8「值域」合并 —— 类模型下块存在 = Relapse 非 null,「半截」的唯一形态就是值为 0/负,由 8 的 > 0 兜住)
        public const int R1_CHECK_27 = 27; // boundary_mode ∈ {monotone, scan}(R1.3 白名单,必填)
        public const int R1_CHECK_28 = 28; // σ 逐病种覆盖值 ≥ 0(Tuning Knobs §四;Sigma == null 走全局默认,不查)
        public const int R1_CHECK_29 = 29; // signs[].SignKey 非空 ∧ ChannelMask ⊆ 六通道闭集 ∧ ≠ 0(R1 表 + §Visual/Audio 二位域)

        // ── BCD 码-1(2026-10-10 登记债收口):R1-30 = GDD 校验 12 + F3 全局不等式 ──
        public const int R1_CHECK_30 = 30; // R_rise > ε_MIN(GDD 校验 12 + F3 全局不等式注,同一条的两个观测面)

        /// <summary>
        /// ε_MIN(Q16.16 raw 单位)= 1 —— 归一化 `R_rise` 的下界守卫。
        /// 权威:GDD disease-simulation.md Tuning §六 ε_MIN 行(2026-10-10 用户授权裁定定值
        /// 「1/65536」= Q16.16 最小正单位)+ 校验 12 + F3 全局不等式注(登记三件套)。
        /// 定值依据:与旋钮登记的字面目的(防 Q16.16 下 R_rise 舍为 0 除零)逐位对齐 ——
        /// R_rise.Raw ≤ 1 的配置其归一化放大 ≥ 2¹⁵ 倍,本身已是数值垃圾,拒收无争议;
        /// 在 τ_rise / τ_peak 安全区间(数值轮)未定前,1 LSB 是唯一可证满足
        /// 「ε_MIN &lt; R_rise(最小合法配置)」的取值;数值轮收紧参数边界后可**抬升**
        /// (抬 ε_MIN = 收紧),须同批复核现网数据(GDD Tuning §六 同步注)。
        /// </summary>
        public const long EpsilonMinRaw = 1;

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

                // ── 曲线面校验(R1-20…25/27/28/30 · GDD R1.3 写入期校验;M2 阶段 2 批次 B
                //    + BCD 码-1 收口的 R1-30,2026-10-10)──
                // ⚠️ Curve == null = 批次 B 前的既有占位条目(曲线字段未声明),整块结构性跳过 ——
                //    登记为已知弱点:曲线字段对旧条目非必填。新数据(disease_registry.json
                //    装载面)一律带 Curve;Relapse / Signs 各自按非 null 独立校验,不受此门限制。
                if (entry.Curve != null)
                {
                    var curve = entry.Curve;

                    // R1-20 ← GDD 校验 1:一个说会降、一个说会停,同时为真是配置错误
                    if (curve.SelfLimit && curve.Plateau)
                        throw new RegistryValidationException(R1_CHECK_20,
                            "curve.self_limit 与 curve.plateau 不可同为 true(GDD 校验 1)");

                    // R1-21 ← GDD 校验 2:τ_peak > incubation ∧ τ_rise > 0,否则 F1 分段倒置/负指数;
                    //          incubation ≥ 0 同尺(Tuning Knobs §二 安全区间)
                    if (curve.Incubation < 0 || curve.TauPeak <= curve.Incubation || curve.TauRise.Raw <= 0)
                        throw new RegistryValidationException(R1_CHECK_21,
                            $"曲线分段须满足 incubation({curve.Incubation}) ≥ 0 ∧ tau_peak({curve.TauPeak}) > incubation ∧ tau_rise > 0(GDD 校验 2)");

                    // R1-30 ← GDD 校验 12 + F3 全局不等式(BCD 码-1 · 2026-10-10 用户授权定值落地):
                    //   R_rise = 1 − e^(−(τ_peak − incubation)/τ_rise) > ε_MIN(= 1 LSB,见 EpsilonMinRaw)
                    //   防:分子跨度相对 τ_rise 过小 ⇒ R_rise 在 Q16.16 下舍为 0 ⇒ 上升段归一化
                    //   除零 / 噪声放大。计算形与求值侧 ComputeBaseF1 的 ExpNeg 逐位同源(同比值构造)
                    //   ⇒ 写入期过门 ⇒ 求值侧 `rRise ≤ 0` fail-loud 不可达(其为本门的子集兜底)。
                    long riseSpan = curve.TauPeak - curve.Incubation; // ≥ 1(R1-21 已过)
                    Fix riseRatio = new Fix(riseSpan * Fix.OneRaw) / curve.TauRise;
                    Fix rRise = Fix.One - Fix.Exp(Fix.Zero - riseRatio);
                    if (rRise.Raw <= EpsilonMinRaw)
                        throw new RegistryValidationException(R1_CHECK_30,
                            $"R_rise = {rRise.Raw} raw ≤ ε_MIN = {EpsilonMinRaw} raw(1 LSB):" +
                            $"τ_peak({curve.TauPeak}) − incubation({curve.Incubation}) = {riseSpan} tick 相对 τ_rise 过小," +
                            "Q16.16 下上升段归一化分母失效(GDD 校验 12 + F3 全局不等式;ε_MIN 定值见 Tuning §六)");

                    // R1-22 ← GDD 校验 4 前半:急性保持型(双 false)仅当 lethal = true ——
                    //   否则该病永远停在峰值又不会死 = 无限迁延。
                    //   ⚠️ 既有字段是 Lethality ∈ [0,3] 整数(GDD R1 表写 bool `lethal`,字段错位
                    //      属既有 16 字段问题),此处以 Lethality > 0 代 lethal;GDD 校验 4b
                    //      (A_peak/SCALE ≥ DEATH_THRESHOLD)因 DEATH_THRESHOLD 值待定未实现,登记。
                    if (!curve.SelfLimit && !curve.Plateau && entry.Lethality <= 0)
                        throw new RegistryValidationException(R1_CHECK_22,
                            "急性保持型(self_limit=false ∧ plateau=false)仅当 lethal=true(GDD 校验 4;4b 需 DEATH_THRESHOLD,值待定未实现)");

                    // R1-24 ← GDD 校验 6:A_peak > 0
                    if (curve.APeak.Raw <= 0)
                        throw new RegistryValidationException(R1_CHECK_24,
                            $"curve.a_peak 必须 > 0: {curve.APeak.Raw}(GDD 校验 6)");

                    // R1-23 ← GDD 校验 5:SCALE > 0 且 ≥ A_peak —— 否则 position 永久钉 1.0
                    if (entry.Scale.Raw <= 0 || entry.Scale.Raw < curve.APeak.Raw)
                        throw new RegistryValidationException(R1_CHECK_23,
                            $"scale 须 > 0 且 ≥ a_peak(scale={entry.Scale.Raw}, a_peak={curve.APeak.Raw})(GDD 校验 5)");

                    // R1-25 ← GDD 校验 7:τ_fall = 0 ⇒ e^(−τ/0) = 0/0 = NaN
                    if (curve.SelfLimit && curve.TauFall.Raw <= 0)
                        throw new RegistryValidationException(R1_CHECK_25,
                            $"self_limit=true 时 curve.tau_fall 必须 > 0: {curve.TauFall.Raw}(GDD 校验 7)");

                    // R1-27 ← R1.3 字段表:boundary_mode 必填,白名单 {monotone, scan}
                    if (curve.BoundaryMode != "monotone" && curve.BoundaryMode != "scan")
                        throw new RegistryValidationException(R1_CHECK_27,
                            $"curve.boundary_mode=\"{curve.BoundaryMode}\" ∉ {{monotone, scan}}(R1.3 必填白名单)");

                    // R1-28 ← Tuning Knobs §四:σ ≥ 0(null = 未覆盖走全局默认,不查)
                    if (curve.Sigma.HasValue && curve.Sigma.Value.Raw < 0)
                        throw new RegistryValidationException(R1_CHECK_28,
                            $"curve.sigma 覆盖值须 ≥ 0: {curve.Sigma.Value.Raw}(Tuning §四)");
                }

                // R1-26 ← GDD 校验 3 + 8(relapse 块出现时三字段齐全且 > 0;类模型下「齐全」
                //   = 块存在即字段在位,「半截」的唯一形态是 0/负值,由 > 0 兜住 —— 两条合并)。
                //   独立于 Curve 块(relapse 配置可单独声明,不因 Curve 缺位静默跳过)。
                if (entry.Relapse != null)
                {
                    if (entry.Relapse.ARel.Raw <= 0 || entry.Relapse.Interval <= 0 || entry.Relapse.TauFall.Raw <= 0)
                        throw new RegistryValidationException(R1_CHECK_26,
                            $"relapse 块三字段须全 > 0(a_rel={entry.Relapse.ARel.Raw}, interval={entry.Relapse.Interval}, tau_rel_fall={entry.Relapse.TauFall.Raw})(GDD 校验 3+8)");
                }

                // R1-29 ← R1 表 signs[] 行 + §Visual/Audio 二位域:词键非空、通道位 ∈ 六通道闭集且非零。
                //   空数组 = 合法(R1 表「可为空集」);null = 未声明(既有条目),跳过。
                if (entry.Signs != null)
                {
                    for (int i = 0; i < entry.Signs.Length; i++)
                    {
                        var sign = entry.Signs[i];
                        if (sign == null || string.IsNullOrEmpty(sign.SignKey))
                            throw new RegistryValidationException(R1_CHECK_29,
                                $"signs[{i}].SignKey 不能为空");
                        if (sign.ChannelMask == 0 || (sign.ChannelMask & ~SignChannels.All) != 0)
                            throw new RegistryValidationException(R1_CHECK_29,
                                $"signs[{i}].channel_mask={sign.ChannelMask} ∉ 六通道闭集(非零且 ⊆ SignChannels.All)");
                    }
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
