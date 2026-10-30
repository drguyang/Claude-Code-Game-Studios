// 权威来源:ADR-005 :241(`IVitalsQuery { VitalsDto GetVitals(PatientId p); }` 唯一浮点出口)
//          · disease-simulation.md AC-20(BLOCKING 字段白名单)· AC-21(通道位整数存储)
//          · b1b 支 2-a 裁定(2026-09-22 批)= 甲:mask + count 全整数
//
// 本 DTO 是**全案唯一 float 出口**(ADR-005 / ADR-012 :97)。float 字段合法 = 裁决本体:
// 逐位判据只覆盖整数定点域(ADR-012),13 的 V8 裁定据此「只在主机运行、客户端不重算」。
// 支 2-a 甲的理由:词条→素材的解析住 13 / 8 的烘焙表,DTO 零数组零引用;
// AC-21「通道位唯一定义、构建期查位冲突」的断言落点 = 下方 SignChannel 常量(支 2-b 口径)。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>体征通道位(P0 现状八条,含「伤口」「主观」「病史」;集合归 8 拥有可扩展,9 不设上限 —— AC-21)。
    /// 位序即契约:新增通道 = 新位,构建期查冲突。词条→通道映射住 9 的烘焙表(R1.3 schema)。
    /// <para>⚠️ 后三位是**非体格**或**半体格**通道,8 侧的 <c>MaskOf</c> 对其中两个刻意返回 0 ——
    /// 见 <c>DiagnosisChannelMaskMap</c>。9 侧登记位是为了让 <c>signs[].channel_mask</c> 有处可落
    /// (R1-29 要求非零),不表示该位会出现在 <c>VitalsDto.SignChannelMask</c> 里。</para></summary>
    public static class SignChannel
    {
        public const int Complexion   = 1 << 0;  // 面色
        public const int Voice        = 1 << 1;  // 语声
        public const int Posture      = 1 << 2;  // 姿态
        public const int Breathing    = 1 << 3;  // 呼吸
        public const int Palpation    = 1 << 4;  // 触感
        public const int Wound        = 1 << 5;  // 伤口(2026-09-17 起第六条,承 25 V1 回填)
        public const int Subjective   = 1 << 6;  // 主观族(2026-10-23 起第七条,承 R3.6 豁免类)
        public const int History      = 1 << 7;  // 病史(2026-10-23 起第八条,问诊所得的非体格证据)
    }

    /// <summary>病情投影 DTO(8 / 13 / 24 的唯一取数形状)。
    /// ⚠️ 字段白名单由构建期反射断言守(AC-20 [L] BLOCKING):
    /// 含任何病种名 / 身份 / 剩余时间 / 百分比 / 血条值等**派生显示量** = 构建失败。</summary>
    public readonly struct VitalsDto
    {
        /// <summary>原始量 0–1(AC-20)。</summary>
        public readonly float Position;
        /// <summary>带符号趋势(AC-20)。</summary>
        public readonly float Trend;
        /// <summary>signs[] 词条的通道位按位或(整数存储,禁 float —— AC-21 BLOCKING 子句)。</summary>
        public readonly int SignChannelMask;
        /// <summary>词条计数(表现层选素材档用;空集 = 豁免类合法形态的「无主观词条」)。</summary>
        public readonly int SignCount;

        /// <summary>
        /// **同一味药的最大当前体内药量**(`Σ |drug_potency| × Decay`,按 `处置_id` 分组取最大;
        /// Q16.16,与 `drug_potency` 同量纲)。2026-10-30 数值轮 · `TD-sim-02` 的消解落点。
        /// </summary>
        /// <remarks>
        /// <para>⚠️ <b>这不是 `Progress` 的一部分</b> —— 它不进 F1、不进 `trend`、不进存档,
        /// 是病史事件流的纯函数(派生态)。理由见
        /// <c>ProgressionEvaluator.ComputePeakDrugLoad</c> 的 remarks:
        /// 把「过量」塞进 `Progress` 单值会让 `trend` 符号反转那类问题复发。</para>
        /// <para><b>消费者</b>:53 的代价表 —— 由它按自己的 `overdose_band_thresholds`
        /// 判「多少算过量、分几档」。9 只给**物质基础的量**,不判后果(band 归 53)。</para>
        /// <para>不传 = `default` = 0 = **无累积药量**(测试 stub 的合法默认值)。</para>
        /// </remarks>
        public readonly Fix PeakDrugLoad;

        public VitalsDto(float position, float trend, int signChannelMask, int signCount,
                         Fix peakDrugLoad = default)
        {
            Position = position; Trend = trend;
            SignChannelMask = signChannelMask; SignCount = signCount;
            PeakDrugLoad = peakDrugLoad;
        }
    }

    /// <summary>唯一浮点出口(ADR-005 抽象点之四;Abstractions.cs 头注预告自此兑现)。
    /// 门面实现住边界侧(9 的查询门面,ADR-025 后现名 = Sim.Contracts 类型 +
    /// Gameplay.* 侧实现);sim 内**禁读回**本 DTO 的 float 字段(disease-simulation.md :772)。</summary>
    public interface IVitalsQuery
    {
        VitalsDto GetVitals(PatientId p);
    }
}
