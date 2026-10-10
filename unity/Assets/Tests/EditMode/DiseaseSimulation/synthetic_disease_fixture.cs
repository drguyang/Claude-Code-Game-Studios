// M2 阶段 2 · 批次 B —— 合成 fixture 病种(1 病种占位,打通数据面 → 求值器 → 体征投影全链的数据半边)。
//
// ⚠️ 验收口径(2026-10-09 用户裁定①,单独登记):
//    本 fixture 的全部数值是**合成占位,非最终数值** —— 数值轮将整表替换。
//    其验收面 = **数据形状与管线可证伪**(必填字段齐 · 曲线形状自洽 · Fix 作者态口径 ·
//    构建期校验可过可红),**不是数值好玩性**。任何「这个数不好玩」不构成本 fixture 的缺陷。
//
// 权威来源:
//   GDD disease-simulation.md R1.3 —— curve.* / relapse.* / scale 字段 schema 与写入期校验
//   GDD disease-simulation.md F1 —— Base 上升/衰减段 + Relapse 复发项(数值须自洽跑出该形状)
//   GDD disease-simulation.md F2 —— SCALE_病种 投影;§Visual/Audio 二 —— signs 六通道位域
//   ADR-014 §四 —— 作者态 Fix 字段写字符串("1/2")经 FixParse,禁 JSON 数字/浮点字面量
//                   (本 fixture 为 EditMode 直构,不经 cooked —— 装载面归后续轮,已登记)
//
// 放置面:测试程序集(EditMode)—— 合成数据不得被误认作生产病种表;
//    批次 C(体征链核心)测试可直接复用本 fixture。
//    TreatableBy 必须为 null:非 null 会触发 R1-19 的 NotImplementedException(结构性不可达分支;
//    treatable_by 已拆独立轴文件 disease_action_axis.json,story-007)。

using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    /// <summary>
    /// 合成 fixture:1 个完整合法的病种注册表条目(占位非最终,数值轮整表替换)。
    /// </summary>
    public static class SyntheticDiseaseFixture
    {
        /// <summary>合成病种 id —— 刻意取 1..8(P0 冻结清单)之外,防与真实病种表混淆。</summary>
        public const int DiseaseId = 9001;

        /// <summary>合成病种 key —— 沿用库内 DIS_ 前缀惯例,但 SYNTH_FIXTURE 明示合成。</summary>
        public const string DiseaseKey = "DIS_SYNTH_FIXTURE";

        // 曲线关键刻度(与 Create() 内数值同源,供测试引用;单位 = tick @ TICK_SECONDS=0.05):
        //   潜伏 600 → 上升 600..1800(τ_rise=300)→ 衰减 τ_fall=600(self_limit)
        //   复发首击 τ=2400(A_rel=1/4 < A_peak=1/2 —— 「余烬」型,承 R1.3 校验 5/scale ≥ a_peak)
        public const int IncubationTicks = 600;
        public const int TauPeakTicks = 1800;
        public const int TauFallTicks = 600;
        public const int RelapseIntervalTicks = 2400;

        /// <summary>
        /// 构造 fixture 条目。每次调用返回全新对象(测试可自由变异,互不共享可变状态)。
        /// Fix 字段经 <see cref="FixParse.Parse(string)"/> 字符串直构 —— 与 ADR-014 作者态口径
        /// ("Fix 字段在 JSON 里写字符串")同一条解析路径,往返一致性由测试逐位守。
        /// </summary>
        public static DiseaseRegistryEntry Create()
        {
            return new DiseaseRegistryEntry
            {
                // ── 既有 16 字段(区间校验 R1-01…16 全过;数值同为合成占位)──
                DiseaseId = DiseaseId,
                DiseaseKey = DiseaseKey,
                Polarity = 1,           // 0=寒 1=热 2=平
                Severity = 3,           // 1-5
                Contagion = 1,          // 0-3
                Lethality = 1,          // 0-3(> 0:急性保持型校验 R1-22 的 lethal 代理)
                TreatmentDifficulty = 5, // 1-10
                RecoveryTime = 3600,    // ticks(> 0;既有字段,GDD 无对应行 —— 已登记的 16 字段错位)
                RelapseChance = 50,     // 0-100(既有字段;真复发语义在下方 Relapse 块)
                ComorbidityFactor = 20,
                SeasonalMod = 30,
                AgeMod = 15,
                GenderMod = 10,
                OccupationMod = 10,
                RegionMod = 20,
                ClimateMod = 25,

                // treatable_by 已拆独立轴文件,条目内不携带(见文件头注)
                TreatableBy = null,
                Handle = null,          // 派生字段,写入期不填(GDD R1.3)

                // ── 曲线面(R1.3 · 批次 B 新增)──
                Curve = new NaturalProgressCurve
                {
                    Incubation = IncubationTicks,
                    APeak = FixParse.Parse("1/2"),      // A_peak = 0.5(Progress 域)
                    TauRise = FixParse.Parse("300"),    // 上升时间常数 300 tick
                    TauPeak = TauPeakTicks,             // 1800 > 600 ✓(校验 2)
                    TauFall = FixParse.Parse("600"),    // 衰减时间常数 600 tick(self_limit 必填)
                    SelfLimit = true,
                    Plateau = false,
                    Sigma = FixParse.Parse("1/64"),     // σ 覆盖 = 1024 raw ≈ 0.0156(≥ 0 ✓)
                    BoundaryMode = "monotone"           // 白名单 {monotone, scan}
                },

                // 复发块(可选 —— fixture 刻意带上,让「上升→衰减→复发」三段全链可抽样)
                Relapse = new RelapseCurve
                {
                    ARel = FixParse.Parse("1/4"),       // 复发峰值 0.25 < A_peak 0.5(余烬型)
                    Interval = RelapseIntervalTicks,   // 首击 τ = 2400(AC-9:k=0 ⇒ interval×1)
                    TauFall = FixParse.Parse("300")    // 复发衰减比主曲线更急
                },

                // F2 归一化分母:SCALE = 1.0 ≥ A_peak = 0.5(校验 5)⇒ position 峰值 0.5 ∈ [0,1]
                Scale = FixParse.Parse("1"),

                // signs[]:词表内容归 8,此处只登记离散词键 + 通道位(六通道位域白名单内)
                Signs = new[]
                {
                    new SignEntry("synth_pallor", SignChannels.Face),
                    new SignEntry("synth_tachypnea", SignChannels.Breath),
                    new SignEntry("synth_weak_voice", SignChannels.Voice | SignChannels.Posture)
                }
            };
        }
    }
}
