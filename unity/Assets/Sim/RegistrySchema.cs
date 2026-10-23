// ADR-014 / GDD disease-simulation.md R1 —— 注册表 schema 与构建期校验。
//
// 权威来源:
//   ADR-014 §二 —— 两阶段烘焙：阶段 1 JsonTextReader 仅词法，阶段 2 自研 per-schema 绑定
//   GDD disease-simulation.md R1 —— 17 条构建期校验
//   GDD disease-simulation.md R1.3 —— natural_progress 字段 schema(curve.* / relapse.* / scale)
//   GDD disease-simulation.md F1/F2 —— Base/Relapse 分段与 SCALE_病种 投影
//   GDD disease-simulation.md §Visual/Audio 二 —— 体征八通道位域(signs[].channel_mask)
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
    /// 体征 → 感知通道位域(P0 八通道)。
    /// 权威:GDD disease-simulation.md §Visual/Audio Requirements 二
    /// (2026-09-16 三轮「通道 = 位域整数」用户裁决②;2026-09-17 五→六增伤口位;
    /// 2026-10-23 六→八增主观位与病史位,承 R3.6 豁免类主观族 + 问诊病史落 signs[])。
    /// 分工三件套:9 只登记通道位(本表),8 拥有词表与阈值,13/44 按位表现。
    /// <para>⚠️ <see cref="Subjective"/> 与 <see cref="History"/> 是**非体格位** —— 8 侧的
    /// <c>DiagnosisChannelMaskMap.MaskOf</c> 对二者刻意返回 0(不驱动表现层)。9 侧登记它们,
    /// 是为了让 R1-29「channel_mask 非零」在主观族 / 病史词条上成立,不是因为它们有体格所见。</para>
    /// </summary>
    public static class SignChannels
    {
        public const int Face = 1 << 0;    // 面色(苍白 · 潮红 · 发黄 · 发青)
        public const int Voice = 1 << 1;   // 语声(有力 · 虚弱 · 含糊 · 不语)
        public const int Posture = 1 << 2; // 姿态(坐起 · 半卧 · 蜷缩 · 不动)
        public const int Breath = 1 << 3;  // 呼吸(平稳 · 急促 · 浅慢 · 停顿)
        public const int Touch = 1 << 4;   // 触感(发热 · 发凉 · 湿冷)
        public const int Wound = 1 << 5;   // 伤口(2026-09-17 新增;伤情持续期外显)
        public const int Subjective = 1 << 6; // 主观族(2026-10-23 新增;R3.6 豁免类的乏力/失眠/心悸/情绪)
        public const int History = 1 << 7; // 病史(2026-10-23 新增;问诊所得的非体格证据)

        /// <summary>八通道闭集 —— channel_mask 只允许本掩码内的位(写入期白名单)。</summary>
        public const int All = Face | Voice | Posture | Breath | Touch | Wound | Subjective | History;
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
    /// 病种 / 伤情 / 中毒类别(GDD <c>disease-simulation.md:285</c>:
    /// <c>kind ∈ {injury, disease, poison}</c>)。
    ///
    /// <para><b>为什么 <see cref="Unspecified"/> = 0 而不是让 injury = 0</b> —— 零值陷阱:
    /// <see cref="DiseaseRegistryEntry"/> 的 <b>22 处构造点</b>(2 生产 + 20 测试)全部使用
    /// <b>具名初始化器</b>,漏填 <c>Kind</c> 时 C# 默认给 0。若 0 = <c>Injury</c>,
    /// 则全部既有病种夹具(伤寒 / 痢疾 / 心衰 …)会<b>静默</b>变成伤情,
    /// <b>没有任何东西报错</b>。故 0 留给 <see cref="Unspecified"/>,
    /// 由 <c>R1_CHECK_31</c> 拒收。</para>
    ///
    /// <para><b>⚠️ 本枚举落地 ≠ 伤情数据面可用</b>(2026-10-21 S18-1):GDD <c>:829-831</c> 要求
    /// <c>kind: injury</c> 行<b>另带</b> <c>SCALE_d</c> + <c>trauma_half_life</c>(整数 tick),
    /// 二者<b>尚未</b>落入本 schema,且后者上界受 <c>HALF_LIFE_MAX</c> 约束(数值归
    /// <c>OQ-25-7</c> 用户自裁);R2 的 11 项 <c>INJ_*</c> 亦<b>零数据行</b>
    /// (<c>assets/data/</c> 内 <c>INJ_</c> 零命中)。见 sprint-18 前置清单。</para>
    /// </summary>
    public enum DiseaseKind
    {
        /// <summary>未声明 —— <b>一律拒收</b>(<c>R1_CHECK_31</c>)。不是合法数据形态。</summary>
        Unspecified = 0,

        /// <summary>病种(伤寒 / 痢疾 / 心衰 / 破伤风 …)。</summary>
        Disease = 1,

        /// <summary>
        /// 伤情(R2 枚举 <c>INJ_*</c>)。⚠️ 该行另须 <c>trauma_half_life</c> + <c>SCALE_d</c>
        /// (GDD <c>:829-831</c>)—— 二者尚未落入本 schema,故本值当前<b>无合法数据行</b>。
        /// </summary>
        Injury = 2,

        /// <summary>中毒(<c>TOX_ENVENOMATION</c>)。</summary>
        Poison = 3,
    }

    /// <summary>
    /// 病种注册表项。
    /// </summary>
    public sealed class DiseaseRegistryEntry
    {
        public int DiseaseId;
        public string DiseaseKey;

        /// <summary>
        /// 病种类别(GDD <c>:285</c> <c>kind ∈ {injury, disease, poison}</c>)。
        /// <para>归 <b>9</b> 所有;25 只出 onset,不填本字段(GDD <c>:356-358</c> 规则九:
        /// 禁在代码里写 <c>if kind == injury</c> 分支 —— 伤情与病种走同一张 schema)。</para>
        /// <para>⚠️ <b>必须显式赋值</b>:零值 <see cref="DiseaseKind.Unspecified"/> 由
        /// <c>R1_CHECK_31</c> 拒收(零值陷阱守卫,理由见枚举注释)。</para>
        /// </summary>
        public DiseaseKind Kind;
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

        /// <summary>
        /// <c>trauma_half_life</c> —— 战伤累积项的衰减半衰期(整数 tick)。
        /// <para><b>仅 <c>kind: injury</c> 行必填</b>(GDD <c>:829-831</c>):F1 的
        /// <c>Decay_injury(Δ) = e^(−Δ/trauma_half_life) · [Δ ≥ 0]</c> 的分母。
        /// 非 injury 行不读本字段(留 0 即可)。</para>
        /// <para><b>值 = <c>60</c></b>(3 s @ 20 Hz)—— <c>OQ-25-7</c> 已裁 2026-10-07(代拍)。
        /// <b>上界 <c>HALF_LIFE_MAX = 120</c></b>(A25a):<c>trauma_half_life → ∞</c> ⇒
        /// <c>Decay → 1.0</c> ⇒ <c>1 − Decay = 0</c> ⇒ A13 除零。本字段的写入期校验
        /// (<c>R1_CHECK_32</c>)只查 <c>kind: injury ⇒ &gt; 0 ∧ ≤ HALF_LIFE_MAX</c>;
        /// <b>非 injury 行一律不查</b>(值无意义)。</para>
        /// <para>⚠️ <b>与 <c>Scale</c> 的关系</b>:GDD 把两者并称「伤情行另带」,但
        /// <c>SCALE_d</c> 就是既有的 <see cref="Scale"/>(同名同义,不是新字段)——
        /// 故本字段是**唯一**新增项。</para>
        /// </summary>
        public int TraumaHalfLife;
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
        //   🔁 2026-10-22:DEATH_THRESHOLD 已裁 = 38011 raw(≈0.58)⇒ 「值待定」不再是 4b 未实现的理由。
        //      4b 仍**未实现**,但现在是**欠账**(可做未做),不是**阻塞**(前提缺失)。
        public const int R1_CHECK_23 = 23; // SCALE > 0 ∧ SCALE ≥ A_peak(GDD 校验 5)
        public const int R1_CHECK_24 = 24; // A_peak > 0(GDD 校验 6)
        public const int R1_CHECK_25 = 25; // self_limit ⇒ τ_fall > 0(GDD 校验 7)
        public const int R1_CHECK_26 = 26; // relapse 块 ⇒ a_rel > 0 ∧ interval > 0 ∧ τ_rel_fall > 0(GDD 校验 3「齐全」+ 8「值域」合并 —— 类模型下块存在 = Relapse 非 null,「半截」的唯一形态就是值为 0/负,由 8 的 > 0 兜住)
        public const int R1_CHECK_27 = 27; // boundary_mode ∈ {monotone, scan}(R1.3 白名单,必填)
        public const int R1_CHECK_28 = 28; // σ 逐病种覆盖值 ≥ 0(Tuning Knobs §四;Sigma == null 走全局默认,不查)
        public const int R1_CHECK_29 = 29; // signs[].SignKey 非空 ∧ ChannelMask ⊆ 八通道闭集 ∧ ≠ 0(R1 表 + §Visual/Audio 二位域)

        // ── BCD 码-1(2026-10-10 登记债收口):R1-30 = GDD 校验 12 + F3 全局不等式 ──
        public const int R1_CHECK_30 = 30; // R_rise > ε_MIN(GDD 校验 12 + F3 全局不等式注,同一条的两个观测面)

        // ── kind 零值陷阱守卫(S18-1 · 2026-10-21;GDD R1 表 :285 的 kind 字段)──
        /// <summary>
        /// <c>Kind ≠ Unspecified</c> —— 零值陷阱守卫。
        /// <para><b>为什么需要这一条</b>:本类型 22 处构造点全部用<b>具名初始化器</b>,
        /// 漏填 <c>Kind</c> 时 C# 给 0。若把 0 定义成 <c>Injury</c>(直觉做法),
        /// 全部既有病种夹具会静默变成伤情且<b>零报错</b>。故 0 = <c>Unspecified</c>,
        /// 由本校验拒收 —— 漏填变成<b>响亮的失败</b>。</para>
        /// <para>⚠️ <b>覆盖面限制(如实登记,禁借绿)</b>:<see cref="RegistrySchemaValidator.Validate"/>
        /// 的调用点目前<b>全在测试</b>(<c>curve_fixture_test.cs</c> / <c>registry_bake_test.cs</c>);
        /// 生产装配路径(<c>CompositionRoot.CreateFallbackRegistry</c>)<b>不调</b>本校验
        /// ⇒ 本守卫<b>只</b>守住测试夹具与将来的 <c>disease_registry.json</c> 烘焙面,
        /// <b>不构成</b>生产侧零值陷阱的消除。生产侧消除随真病种表落地(ADR-014 阶段 2)。</para>
        /// </summary>
        public const int R1_CHECK_31 = 31;

        /// <summary>
        /// <c>kind: injury</c> 行须 <c>trauma_half_life &gt; 0</c> 且 <c>≤ HALF_LIFE_MAX</c>
        /// (GDD <c>:829-831</c> + <c>OQ-25-7</c> 的 A25a 上界)。
        /// <para><b>为什么必须有上界</b>(A25a):<c>trauma_half_life → ∞</c> ⇒
        /// <c>Decay = e^(−Δ/∞) → 1.0</c>,Q16.16 舍入下可达 ⇒ <c>1 − Decay = 0</c> ⇒
        /// A13 除零。<b>下界 0 则连击完全不叠加</b>,与幻想承诺 1 冲突。</para>
        /// <para>⚠️ <b>只查 <c>kind: injury</c></b> —— 其余 kind 的该字段无意义(留 0),
        /// 查它会造出「病种行必须填伤情字段」的假红。</para>
        /// </summary>
        public const int R1_CHECK_32 = 32;

        /// <summary>
        /// <c>DiseaseId ∈ [1, 64]</c> —— <c>DiseaseIdSet</c> 位掩码的**值域守卫**(2026-10-22 立)。
        ///
        /// <para><b>为什么需要这一条</b>:<c>DiseaseIdSet</c> 的位序契约是
        /// <c>bit index = DiseaseId − 1</c>,而位掩码是 <c>ulong</c> ⇒ 只有 64 个位可用。
        /// 而 <c>DiseaseIdSet</c> 自己的注释长期写着「**上限 = 64 病种**;超界 = **数据层构建失败**,
        /// 非本 struct 职责」—— <b>那个「构建失败」此前并不存在</b>(只有 R1-01 唯一性,
        /// 值域零校验)。这是「注释声称有门、门却不在」的形态,本常量把它补成真的。</para>
        ///
        /// <para><b>越界的后果是静默的,不是响亮的</b>:C# 的 <c>ulong</c> 移位计数按 <c>&amp; 0x3F</c>
        /// 掩码,<c>1UL &lt;&lt; (65 − 1)</c> = <c>1UL &lt;&lt; 64</c> = <c>1UL &lt;&lt; 0</c> = <b>1</b>
        /// ⇒ <c>DiseaseId = 65</c> 静默变成 <c>DiseaseId = 1</c> 的位。<b>不会抛、不会报、不会崩</b>,
        /// 只会在某天有人查「为什么 id 65 的病和 id 1 的病总是一起出现」时才发现。
        /// 下界 <c>≥ 1</c> 同理:<c>DiseaseId = 0</c> 会算出 <c>bit = −1</c> ⇒ 左移负数在 C# 里是
        /// <b>未定义行为</b>(运行时实际按 <c>&amp; 0x3F</c> 取 <c>63</c>)。</para>
        ///
        /// <para><b>覆盖面(如实登记,禁借绿)</b>:本校验只在 <c>Validate</c> 的调用点生效 ——
        /// 即 <c>DiseaseRegistryBinder.Bind</c>(烘焙面)与各测试夹具。
        /// <c>CompositionRoot.CreateFallbackRegistry</c> <b>不调</b> <c>Validate</c>
        /// (<c>CompositionRoot.cs:671</c> 自陈)⇒ 该路径上的 id 不受本门约束
        /// (它只产一条 <c>DiseaseId = 1</c>,在界内)。</para>
        /// </summary>
        public const int R1_CHECK_33 = 33;

        /// <summary><c>DiseaseId</c> 下界(含)—— <c>DiseaseIdSet</c> 的 <c>bit index = DiseaseId − 1</c> ⇒ 须 ≥ 1。</summary>
        public const int DiseaseIdMin = 1;

        /// <summary><c>DiseaseId</c> 上界(含)= <c>ulong</c> 的位数 —— 位掩码上限 64 病种。</summary>
        public const int DiseaseIdMax = 64;

        /// <summary>
        /// <b>R1-34</b> —— GDD 校验 4b / 14a:致死型曲线须能打到死线
        /// (<c>A_peak/SCALE ≥ DEATH_THRESHOLD</c>)。
        /// <para>适用型:急性保持型(`self_limit=false ∧ plateau=false`)**或**
        /// `plateau ∧ lethal`。缺它 ⇒ 「声明致死却永远打不到死线」= 无限迁延。</para>
        /// <para>🔁 2026-10-22 实现。此前**全库零校验**(`R1_CHECK_22` 自陈「4b 需 DEATH_THRESHOLD,
        /// 值待定未实现」;值裁后成为欠账)。</para>
        /// </summary>
        public const int R1_CHECK_34 = 34;

        /// <summary>
        /// <b>R1-35</b> —— GDD 校验 14b:豁免类(`plateau ∧ ¬lethal`)峰值窗
        /// <c>RECOVER_THRESHOLD &lt; A_peak/SCALE &lt; CRITICAL_THRESHOLD</c>。
        /// <para>下界缺 ⇒ 神经衰弱落地即成「痊愈」,「他还在病着」为假(R3.6);
        /// 上界缺 ⇒ 豁免类可达危殆,与「空体征」语义冲突。</para>
        /// <para>🔁 2026-10-22 实现,同批。</para>
        /// </summary>
        public const int R1_CHECK_35 = 35;

        /// <summary><c>HALF_LIFE_MAX</c> —— <c>trauma_half_life</c> 的上界(tick)。
        /// 值 = <c>120</c>(6 s @ 20 Hz),<c>OQ-25-7</c> 已裁 2026-10-07(代拍,A25a)。
        /// <para>⚠️ 本值住这里而非 <c>StateMachine</c>:它是<b>注册表写入期</b>的校验界,
        /// 不是运行期状态机阈值。GDD <c>combat-and-weapon-lines.md:1363</c> 是权威出处。</para></summary>
        public const int HalfLifeMax = 120;

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

        // ══════════════════════════════════════════════════════════════════════
        // 趋势窗口全局配置(W / TREND_EPS)的值域守卫 —— 2026-10-22 立
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// <b>R1-36</b> —— 趋势窗口全局配置(<c>W</c> / <c>TREND_EPS</c>)的值域守卫。
        ///
        /// <para><b>守什么</b>:<c>trend_window_ticks</c>(<c>W</c>)须 <c>≥ 0</c> 且
        /// <b>为偶数</b>;<c>trend_eps</c>(<c>TREND_EPS</c>,Q16.16 raw)须 <c>≥ 0</c>。</para>
        ///
        /// <para><b>GDD 对应条</b>:Tuning §四 <c>:1743-1744</c> 的安全区间 ——
        /// <c>W &gt; 0</c> 且须为偶数(取 <c>W/2</c> 做中心差分)、<c>TREND_EPS &gt; 0</c>。</para>
        ///
        /// <para>⚠️ <b><c>0</c> 是「未配置」哨兵,不是 GDD 的合法值</b> —— 数值轮裁定前
        /// 两个量都是 <c>0</c>,<see cref="F4OutcomeEvaluator"/> 据此判
        /// <c>RecoveryWindowInput.IsConfigured == false</c> 并**不求值**痊愈支 / 迁延支。
        /// ⇒ 本门**放行 0**(未配置),但负值与奇数 <c>W</c> 一律拒收:
        /// 奇数会让 <c>W/2</c> 的中心差分丢掉半个 tick 且静默偏置。
        /// 「<c>W = 0</c> 也放行」是**工程约定,GDD 无此条**,此处明写以绝混读。</para>
        ///
        /// <para><b>覆盖面(如实登记,禁借绿)</b>:调用点 = <c>DiseaseRegistryBinder.Bind</c>
        /// (烘焙面)。<c>CompositionRoot.CreateFallbackRegistry</c> 不读 JSON、不调本门。</para>
        /// </summary>
        public const int R1_CHECK_36 = 36;

        /// <summary>
        /// 校验趋势窗口全局配置(<c>W</c> / <c>TREND_EPS</c>)—— R1-36。
        /// <para>两个量都是**全局**旋钮:GDD Tuning §四 的段标题是「噪声与读数」,
        /// 不属 §二「逐病种 · R1.3 schema」,且 R1.3 的字段表里没有 <c>w</c> /
        /// <c>trend_eps</c> ⇒ 逐病种不是 GDD 的口径。</para>
        /// </summary>
        /// <param name="trendWindowTicks"><c>W</c>(tick 数);<c>0</c> = 未配置。</param>
        /// <param name="trendEpsRaw"><c>TREND_EPS</c>(Q16.16 raw);<c>0</c> = 未配置。</param>
        /// <exception cref="RegistryValidationException">负值,或 <c>W</c> 为奇数。</exception>
        public static void ValidateTrendWindow(long trendWindowTicks, long trendEpsRaw)
        {
            if (trendWindowTicks < 0)
                throw new RegistryValidationException(R1_CHECK_36,
                    $"trend_window_ticks(W) 须 ≥ 0(0 = 未配置): {trendWindowTicks}");
            if (trendWindowTicks % 2 != 0)
                throw new RegistryValidationException(R1_CHECK_36,
                    "trend_window_ticks(W) 须为偶数(GDD Tuning §四 :1743 —— 取 W/2 做中心差分): "
                    + trendWindowTicks);
            if (trendEpsRaw < 0)
                throw new RegistryValidationException(R1_CHECK_36,
                    $"trend_eps(TREND_EPS) 须 ≥ 0(0 = 未配置): {trendEpsRaw}");
        }

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

                // R1-33: DiseaseId ∈ [1, 64] —— DiseaseIdSet 位掩码的值域守卫(2026-10-22)。
                //   bit index = DiseaseId − 1 且掩码是 ulong ⇒ 界外会静默回绕(C# 移位计数 & 0x3F)。
                //   ⚠️ 这条必须在任何「DiseaseId → bit」的使用之前,故紧贴 R1-01。
                if (entry.DiseaseId < DiseaseIdMin || entry.DiseaseId > DiseaseIdMax)
                    throw new RegistryValidationException(R1_CHECK_33,
                        $"DiseaseId 越界: {entry.DiseaseId}(DiseaseKey='{entry.DiseaseKey}')—— " +
                        $"须 ∈ [{DiseaseIdMin}, {DiseaseIdMax}](DiseaseIdSet 位序 bit index = DiseaseId − 1;" +
                        "越界时 C# 的 ulong 移位按 & 0x3F 回绕 ⇒ 静默撞到别的病种的位,不抛不报)");

                // R1-02: DiseaseKey 非空
                if (string.IsNullOrEmpty(entry.DiseaseKey))
                    throw new RegistryValidationException(R1_CHECK_02, "DiseaseKey 不能为空");

                // R1-31: kind 零值陷阱守卫 —— 漏填 Kind ⇒ 响亮失败,不静默落成某一种类。
                if (entry.Kind == DiseaseKind.Unspecified)
                    throw new RegistryValidationException(R1_CHECK_31,
                        $"kind 未声明(DiseaseId={entry.DiseaseId}, DiseaseKey='{entry.DiseaseKey}')" +
                        " —— GDD R1 表要求 kind ∈ {injury, disease, poison};" +
                        "Unspecified=0 是「漏填」的哨兵,不是合法数据形态");

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

                // R1-19: treatable_by 的归属面(2026-10-23 结清 `D-9-P`,原 `NotImplementedException` 退役)
                //
                // ── 原文(2026-10-07,保留留痕)──
                //   「R1-19: treatable_by[].action ∈ 处置轴闭集 —— 待 disease_registry.json 存在后实现
                //    (当前结构性不可达)。登记为已知弱点……归 9 的下一轮。」
                //
                // ── 为什么它不能按原样实现,以及现在它变成什么 ──
                //   原计划是「条目里带 treatable_by[],逐条查 action ∈ 轴闭集」。但 story-007 起
                //   treatable_by 已**整体搬到** `disease_action_axis.json` 作单一真源
                //   (GDD `D-9-K`:极性以轴为准),条目里**不该**再有它。
                //   ⇒ 「条目内的 action 是否在轴闭集内」这个问题**不再有主语** ——
                //     真正的等价判据住在轴自己那边:**9-DC-4**
                //     (`DiseaseActionAxisValidator`),它对轴的每一行查 action ∈ 闭集。
                //   ⇒ 本条的**新语义**不是「检查」,是**守卫单一真源**:
                //     条目里出现非 null 的 `treatable_by` = 有人造了第二真源 ⇒ 硬失败。
                //
                //   ⚠️ 这与「校验未实现」不是同一状态:原注释说「调用方无法区分校验通过与校验未实现」,
                //      现在它可以区分了 —— 抛 `RegistryValidationException` 是**判定**,不是占位。
                if (entry.TreatableBy != null)
                {
                    throw new RegistryValidationException(R1_CHECK_19,
                        $"'{entry.DiseaseKey}' 的条目携带了 treatable_by —— " +
                        "该字段自 story-007 起已整体搬到 `disease_action_axis.json`(单一真源,D-9-K);" +
                        "条目内保留副本 = 第二真源,可与轴静默分叉。" +
                        "action ∈ 轴闭集的判据在轴侧:9-DC-4(DiseaseActionAxisValidator)");
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
                    //   🔁 2026-10-22 第三轮订正 —— **「字段错位」这个定性已被用户裁定推翻**(原文不删):
                    //      裁定口径 = 「保持 0-3,明确语义为『致死等级』」⇒ `Lethality ∈ [0,3]`
                    //      **不是**对 `bool lethal` 的错位映射,而是更细的定义域;`lethal ⇔ Lethality > 0`
                    //      是**正确定义**,不是代理近似。逐条论证见 `EntityDeathGates.LethalityOf`。
                    //      ⚠️ 但**本条注释的另一半仍然成立且重要**:binder 把 JSON 的 `lethal: bool`
                    //      映射为 `? 2 : 0`(`DiseaseRegistryBinder.cs:180`)⇒ 等级 1 与 3
                    //      **今天从作者态不可达**。这是作者态 schema 的刻意简化,不是缺陷。
                    //   🔁 2026-10-22 补注:**上述「值待定」的理由已失效** —— DEATH_THRESHOLD 已于
                    //      同日裁定为 38011 raw(≈0.58,档 C;见 StateMachine.cs:90)。
                    //      ⇒ 4b **现在可以实现**,但**仍未实现**(本轮不做,登记为欠账)。
                    //      ⚠️ 不得把本注释读成「4b 已过」—— 本处只校验 lethal 那一半,
                    //      `A_peak/SCALE ≥ DEATH_THRESHOLD` 那一半全库**零校验**。
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

                    // ══════════════════════════════════════════════════════════════════
                    // R1-34 / R1-35 ← GDD 校验 4b + 校验 14(2026-10-22 实现)
                    //
                    // 这是「值已裁 ≠ F4 可求值」三件缺口里的第三件 —— 此前**全库零校验**,
                    // 注册表可以收进一条「声明致死却永远打不到死线」的行。
                    //
                    //   4b  (急性保持型)      ⇒ A_peak/SCALE ≥ DEATH_THRESHOLD
                    //   14a (plateau ∧ lethal)⇒ A_peak/SCALE ≥ DEATH_THRESHOLD
                    //   14b (豁免类 plateau ∧ ¬lethal)⇒ RECOVER < A_peak/SCALE < CRITICAL
                    //
                    // 缺 4b/14a ⇒ 该型「过临界却同值不达死线」= 无限迁延;
                    // 缺 14b    ⇒ 神经衰弱落地即成「痊愈」,「他还在病着」为假(R3.6)。
                    //
                    // ⚠️ **比较在整数域完成,不做除法**(ADR-006 §三 ROUND_HALF_AWAY_FROM_ZERO
                    //    只约束「必须除」的地方)。`A_peak/SCALE ≥ DEATH` 等价于
                    //    `A_peak × ONE ≥ DEATH_raw × SCALE`(两边同乘正数,序不变;
                    //    SCALE > 0 已由 R1-23 保证)。128 位中间量走手工 hi/lo
                    //    (ADR-005 Amendment G),不用 System.Int128 / BigInteger。
                    // ══════════════════════════════════════════════════════════════════
                    {
                        long scale = entry.Scale.Raw;          // > 0(R1-23 已过)
                        long aPeak = curve.APeak.Raw;          // > 0(R1-24 已过)

                        // 左式 = aPeak/scale 与阈值 thr 的比较:aPeak × ONE  ?  thr × scale
                        // 两个乘积都可能超 int64(aPeak/scale 是 raw,量级 1e5 × 1e5 = 1e10 尚可,
                        // 但 raw 可达 2^48 ⇒ 乘积可达 2^64)⇒ 走 hi/lo 128 位比较。
                        bool AtLeast(long thrRaw) =>
                            CompareProducts(aPeak, Fix.OneRaw, thrRaw, scale) >= 0;

                        // 🔁 2026-10-22 实现轮自查修正:严格大于必须**精确表达**,
                        //    不得用 `AtLeast(RECOVER + 1)` 近似 —— 见下方 R1-35 下界处的长注。
                        bool StrictlyAbove(long thrRaw) =>
                            CompareProducts(aPeak, Fix.OneRaw, thrRaw, scale) > 0;

                        bool acuteHold = !curve.SelfLimit && !curve.Plateau;
                        bool plateau = curve.Plateau;
                        bool lethal = entry.Lethality > 0;

                        if (acuteHold || (plateau && lethal))
                        {
                            // 4b / 14a:须 ≥ DEATH_THRESHOLD
                            if (!AtLeast(StateMachine.DEATH_THRESHOLD))
                                throw new RegistryValidationException(R1_CHECK_34,
                                    $"A_peak/SCALE < DEATH_THRESHOLD(a_peak={aPeak}, scale={scale}, " +
                                    $"DEATH={StateMachine.DEATH_THRESHOLD} raw)—— " +
                                    $"{(acuteHold ? "急性保持型" : "plateau ∧ lethal")}须能打到死线," +
                                    "否则「声明致死却永远打不到死线」= 无限迁延(GDD 校验 4b / 14)");
                        }
                        else if (plateau && !lethal)
                        {
                            // 14b 豁免类下界:RECOVER < A_peak/SCALE < CRITICAL
                            //
                            // 🔁 2026-10-22 实现轮自查修正 —— 下界原写 `!AtLeast(RECOVER + 1)`,
                            //    即 `aPeak × ONE ≥ (RECOVER+1) × scale`。**这是错的口径**:
                            //    GDD 校验 14 写的是**严格大于** `RECOVER < A_peak/SCALE`,
                            //    精确式 = `aPeak × ONE > RECOVER × scale`。
                            //    两者在 `scale ≠ ONE` 时**不等价** —— `+1` 是「raw 上界」而非
                            //    「比值上界」,`scale` 越大误差越大。
                            //    实测可达的误拒:`a_peak=12, scale=100` ⇒ 比值 0.12 >
                            //    `RECOVER = 7864/65536 ≈ 0.119995`(GDD 判**合法**),
                            //    但旧写法要求 `aPeak ≥ 13` ⇒ **误拒合法行**。
                            //    ⇒ 改用 `StrictlyAbove`,在整数域精确表达 `>`。
                            if (!StrictlyAbove(StateMachine.RECOVER_THRESHOLD))
                                throw new RegistryValidationException(R1_CHECK_35,
                                    $"豁免类 A_peak/SCALE ≤ RECOVER_THRESHOLD(a_peak={aPeak}, scale={scale}, " +
                                    $"RECOVER={StateMachine.RECOVER_THRESHOLD} raw)—— " +
                                    "豁免类须严格高于痊愈门,否则落地即成「痊愈」," +
                                    "「他还在病着」为假(GDD 校验 14 · R3.6)");
                            if (AtLeast(StateMachine.CRITICAL_THRESHOLD))
                                throw new RegistryValidationException(R1_CHECK_35,
                                    $"豁免类 A_peak/SCALE ≥ CRITICAL_THRESHOLD(a_peak={aPeak}, scale={scale}, " +
                                    $"CRITICAL={StateMachine.CRITICAL_THRESHOLD} raw)—— " +
                                    "豁免类峰值须低于危殆门(GDD 校验 14)");
                        }
                    }

                    // R1-27 ← R1.3 字段表:boundary_mode 必填,白名单 {monotone, scan}
                    if (curve.BoundaryMode != "monotone" && curve.BoundaryMode != "scan")
                        throw new RegistryValidationException(R1_CHECK_27,
                            $"curve.boundary_mode=\"{curve.BoundaryMode}\" ∉ {{monotone, scan}}(R1.3 必填白名单)");

                    // R1-28 ← Tuning Knobs §四:σ ≥ 0(null = 未覆盖走全局默认,不查)
                    if (curve.Sigma.HasValue && curve.Sigma.Value.Raw < 0)
                        throw new RegistryValidationException(R1_CHECK_28,
                            $"curve.sigma 覆盖值须 ≥ 0: {curve.Sigma.Value.Raw}(Tuning §四)");
                }

                // R1-32 ← GDD :829-831 + OQ-25-7 的 A25a:kind: injury 行的 trauma_half_life
                //   须 > 0(否则连击不叠加,与幻想承诺 1 冲突)且 ≤ HALF_LIFE_MAX
                //   (→∞ ⇒ Decay → 1.0 ⇒ 1−Decay = 0 ⇒ A13 除零)。
                //   ⚠️ 只查 injury —— 其余 kind 的该字段无意义,查它会造假红。
                if (entry.Kind == DiseaseKind.Injury &&
                    (entry.TraumaHalfLife <= 0 || entry.TraumaHalfLife > HalfLifeMax))
                    throw new RegistryValidationException(R1_CHECK_32,
                        $"kind: injury 行须 0 < trauma_half_life ≤ {HalfLifeMax}:" +
                        $" {entry.DiseaseKey} = {entry.TraumaHalfLife}" +
                        "(GDD :829-831 + A25a;→0 连击不叠加,→∞ 则 1−Decay = 0 致 A13 除零)");

                // R1-26 ← GDD 校验 3 + 8(relapse 块出现时三字段齐全且 > 0;类模型下「齐全」
                //   = 块存在即字段在位,「半截」的唯一形态是 0/负值,由 > 0 兜住 —— 两条合并)。
                //   独立于 Curve 块(relapse 配置可单独声明,不因 Curve 缺位静默跳过)。
                if (entry.Relapse != null)
                {
                    if (entry.Relapse.ARel.Raw <= 0 || entry.Relapse.Interval <= 0 || entry.Relapse.TauFall.Raw <= 0)
                        throw new RegistryValidationException(R1_CHECK_26,
                            $"relapse 块三字段须全 > 0(a_rel={entry.Relapse.ARel.Raw}, interval={entry.Relapse.Interval}, tau_rel_fall={entry.Relapse.TauFall.Raw})(GDD 校验 3+8)");
                }

                // R1-29 ← R1 表 signs[] 行 + §Visual/Audio 二位域:词键非空、通道位 ∈ 八通道闭集且非零。
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
                                $"signs[{i}].channel_mask={sign.ChannelMask} ∉ 八通道闭集(非零且 ⊆ SignChannels.All)");
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // 128 位乘积比较 —— R1-34 / R1-35 的整数域判据(2026-10-22 立)
        // ══════════════════════════════════════════════════════════════════════════
        //
        // 为什么要它:校验 4b / 14 的判据是 `a/scale ≥ thr/ONE`,即
        //   `a × ONE ≥ thr × scale`(两边同乘 `scale × ONE`,均为正 ⇒ 序不变)。
        // 其中 `a` / `scale` / `thr` 都是 raw(`long`,可达 2^48 量级),两个乘积
        // **可能超 int64**(2^48 × 2^16 = 2^64)⇒ 必须走 128 位。
        //
        // ⚠️ **不用 `System.Int128`**(Unity 6.3 netstandard2.1 / IL2CPP 无此 .NET 7 类型,
        //    ADR-005 Amendment G / E-2)· **不用 `BigInteger`**(ADR-005 Amendment G / AC-4)。
        //    走**手工 hi/lo 两 `ulong` 带进位** —— 四路拆分,交叉项全程无符号 + 掩码提取,
        //    **禁有符号右移**(ADR-012 F7:IL2CPP 下 C# 有符号溢出为 UB)。
        //
        // 本函数只做**比较**,不做加减(比较是全部需求),故不需要完整 128 位加法器。

        /// <summary>
        /// 比较 <c>a × b</c> 与 <c>c × d</c>(全为非负整数 raw)——
        /// 返回 −1 / 0 / +1。用于 R1-34 / R1-35 的 <c>a_peak/scale</c> 阈值比较。
        /// <para><b>只接受非负入参</b>:判据面全是 raw 正数(R1-23 / R1-24 已保证
        /// <c>scale &gt; 0</c> / <c>a_peak &gt; 0</c>;阈值常量亦为正)。传负数 ⇒ 抛,
        /// 不静默按无符号处理(那会把负数读成巨大的正数)。</para>
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">任一入参为负。</exception>
        public static int CompareProducts(long a, long b, long c, long d)
        {
            if (a < 0) throw new ArgumentOutOfRangeException(nameof(a), a, "CompareProducts 只接受非负入参");
            if (b < 0) throw new ArgumentOutOfRangeException(nameof(b), b, "CompareProducts 只接受非负入参");
            if (c < 0) throw new ArgumentOutOfRangeException(nameof(c), c, "CompareProducts 只接受非负入参");
            if (d < 0) throw new ArgumentOutOfRangeException(nameof(d), d, "CompareProducts 只接受非负入参");

            // 左边 a×b
            Mul128((ulong)a, (ulong)b, out ulong loL, out ulong hiL);
            // 右边 c×d
            Mul128((ulong)c, (ulong)d, out ulong loR, out ulong hiR);

            if (hiL != hiR) return hiL < hiR ? -1 : 1;
            if (loL != loR) return loL < loR ? -1 : 1;
            return 0;
        }

        /// <summary>
        /// 无符号 64×64 ⇒ 128 位乘法(四路拆分,交叉项带进位)。
        /// <para>⚠️ 全程 <c>ulong</c>,**无有符号移位**(ADR-012 F7)。</para>
        /// </summary>
        private static void Mul128(ulong x, ulong y, out ulong lo, out ulong hi)
        {
            const ulong Mask = 0xFFFF_FFFFUL;

            ulong xLo = x & Mask;
            ulong xHi = x >> 32;
            ulong yLo = y & Mask;
            ulong yHi = y >> 32;

            // 四项乘积(每项 ≤ (2^32−1)^2 < 2^64,不溢出)
            ulong p0 = xLo * yLo;   // 权重 2^0
            ulong p1 = xLo * yHi;   // 权重 2^32
            ulong p2 = xHi * yLo;   // 权重 2^32
            ulong p3 = xHi * yHi;   // 权重 2^64

            // 低 64 位 = p0 + ((p1 + p2) << 32) 的低半
            ulong mid = (p0 >> 32) + (p1 & Mask) + (p2 & Mask);
            lo = (mid << 32) | (p0 & Mask);

            // 高 64 位 = p3 + (p1 >> 32) + (p2 >> 32) + (mid >> 32)
            hi = p3 + (p1 >> 32) + (p2 >> 32) + (mid >> 32);
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
