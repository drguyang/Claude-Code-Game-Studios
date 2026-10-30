// ADR-035 §①B —— 「哪些药 × 哪些禁忌成立」的**触发条件面**,归 11(处方用药)。
//
// 权威来源:
//   ADR-035 §①B(触发条件面归 11)· ②(53 的代价表键 = `(harm_kind, band)`)
//   `prescription-and-medication.md` 规则八(11 **不拦不扣**:本判据**只记账**,不拦截处方 /
//   不减药效 / 不弹警告 —— 后果由 53 结算,承 `OQ-11-5`)
//   `AC-11-01` ②(体征逐位相同、病种不同 ⇒ 载荷逐字段相等)
//
// ⚠️ **为什么判据读 `SignChannelMask` 而不是病种**:
//   `AC-11-01` ② 要求「体征逐位相同、病种不同」的病人拿到**逐字段相等**的载荷。
//   若判据读病种 ⇒ 载荷随病种变 ⇒ 直接撞该条(BLOCKING)。
//   读体征通道位则天然成立 —— 体征相同 ⇒ 通道位相同 ⇒ 档相同。
//   且 `VitalsDto` 的 `float` 两个字段**不参与**(`disease-simulation.md:772` 禁 sim 读回),
//   本判据只用整数位域 ⇒ 11 的「零浮点」不破。
//
// ⚠️ **今日生产环境该位域恒为 0**(`DiseaseVitalsService.ComputeVitals` 的 `signChannelMask: 0`
//   —— F2 投影未接线,9 侧产不出词条)⇒ 判据在生产里**恒不命中**。这不是本文件的本事缺陷,
//   是 9 侧的接线缺口;登记为债,不在本文件兜底(兜底 = 自造体征)。

using System.Collections.Generic;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>一条禁忌规则:**病人同时出现**这些体征通道 ⇒ 命中,档 = <see cref="Band"/>。</summary>
    /// <remarks>
    /// <para><b>匹配语义</b>:`(patientMask ∧ ChannelMask) == ChannelMask` —— **全部**要求通道都出现才算命中
    /// (不是「任一」)。理由:禁忌是合取条件(「面白**且**喘」才是危候,单有面白只是轻候),
    /// 析取会让重档在轻病人身上误命中。</para>
    /// <para><b>多规则命中 ⇒ 取最高档**(<see cref="ContraindicationEvaluator.Evaluate"/>)。
    /// 档序数越大越重,与 53 的 `bands` 同序。</para>
    /// </remarks>
    public readonly struct ContraindicationRule
    {
        /// <summary>要求的体征通道位或(<see cref="SignChannel"/> 的子集);<b>必须非零</b>。</summary>
        public readonly int ChannelMask;

        /// <summary>命中时归入的**禁忌档序数**(对应 53 代价表的 `band`)。</summary>
        public readonly int Band;

        public ContraindicationRule(int channelMask, int band)
        {
            ChannelMask = channelMask; Band = band;
        }
    }

    /// <summary>禁忌判定的**唯一求值点**(纯函数,零状态,可重放)。</summary>
    public static class ContraindicationEvaluator
    {
        /// <summary>「本剂未命中任何禁忌」的哨兵 —— 与 <c>DrugTreatmentAppliedPayload.NoContraindication</c> 同值。</summary>
        public const int NoContraindication = -1;

        /// <summary>八通道闭集的并(`SignChannel` 的位域全 1)。</summary>
        public const int AllChannels =
            SignChannel.Complexion | SignChannel.Voice | SignChannel.Posture | SignChannel.Breathing
            | SignChannel.Palpation | SignChannel.Wound | SignChannel.Subjective | SignChannel.History;

        /// <summary>求值:返回命中的**最高**档;无命中返回 <see cref="NoContraindication"/>。</summary>
        /// <param name="rules">该药的规则集(<c>null</c> / 空 ⇒ 永不命中)。</param>
        /// <param name="signChannelMask">病人的体征通道位域(`VitalsDto.SignChannelMask`)。</param>
        public static int Evaluate(IReadOnlyList<ContraindicationRule> rules, int signChannelMask)
        {
            if (rules == null) return NoContraindication;
            int best = NoContraindication;
            for (int i = 0; i < rules.Count; i++)
            {
                ContraindicationRule r = rules[i];
                if (r.ChannelMask == 0) continue;                       // 空掩码不得当作「恒命中」
                if ((signChannelMask & r.ChannelMask) != r.ChannelMask) continue;
                if (r.Band > best) best = r.Band;                       // 取最高档
            }
            return best;
        }
    }
}
