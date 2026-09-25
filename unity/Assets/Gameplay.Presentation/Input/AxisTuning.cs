// 权威来源:Story 002(production/epics/input-system/story-002-axis-processing.md)
//   · GDD input-system.md F-3.1 + §Tuning Knobs 一(四条并列装载期断言)
//   · ADR-006(外部数据 → Fix 只经 FixParse;定点域内纯整数比较)
//   · ADR-014 §四(Fix 字段创作态 JSON 字符串;校验失败 = 硬失败非警告)
//   · ADR-025 §① / §② 甲案(Gameplay.Input 不引 Sim.Contracts ⇒ 承载 Fix 的常量表住
//     Gameplay.Presentation;该装配在 b4 ToFloat 白名单内)
//
// 落点注记:手感能力住 Gameplay.Presentation 而非 Gameplay.Input —— ADR-025 §① 钉死
//   Gameplay.Input = 仅 Unity.InputSystem(AC-3-A6),Fix 在那边不可见;本落点零 asmdef 改动。

using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>F-3.1 轴处理常量表(手感层;输出不进流、不进 sim —— GDD 规则八)。
    /// <para><b>构造不校验</b> —— 四条装载期断言(AC-3-A9④)的机械门在
    /// <c>InputAxisTuningBinder</c>(ADR-014 硬失败);直接构造是绕过装载门的内存路径,
    /// 该路径合法存在的理由 = 测试负例(失效签名)要从这头把断言链打红。</para>
    /// <para><b>「CURVE_POW 有限」由 FixParse 字面量纪律承载</b>(ADR-006:拒一切非整数字面量,
    /// "∞" / "NaN" 串在解析层即 FormatException)—— Fix 域结构上无非有限值,Validate 无需再断;
    /// 与 ① 的运行期 NaN/∞ 防护是两道独立门(GDD F-3.1 ④)。</para></summary>
    public readonly struct AxisTuning
    {
        /// <summary>死区内缘:模长 ≤ 此值 ⇒ 输出 0(GDD F-3.1)。</summary>
        public Fix DzInner { get; }

        /// <summary>死区外缘:模长 ≥ 此值 ⇒ 满速(t = 1)。</summary>
        public Fix DzOuter { get; }

        /// <summary>缓动曲线幂;必须 &gt; 1/2,否则端点 C1 破缺(GDD F-3.1 Ⓐ 证明)。</summary>
        public Fix CurvePow { get; }

        public AxisTuning(Fix dzInner, Fix dzOuter, Fix curvePow)
        {
            DzInner = dzInner;
            DzOuter = dzOuter;
            CurvePow = curvePow;
        }

        /// <summary>四条装载期断言(GDD §Tuning Knobs 一 · AC-3-A9④)。
        /// 返回全部违例(空 = 合法);全部比较在整数域(Raw)完成 —— 无浮点、无容差。</summary>
        /// <param name="label">错误前缀(文件名 / 夹具名),null 省略。</param>
        public List<string> Validate(string label = null)
        {
            var errors = new List<string>();
            string tag = string.IsNullOrEmpty(label) ? string.Empty : label + ":";

            if (DzInner.Raw < 0)
                errors.Add($"{tag}DZ_INNER 必须 ≥ 0(raw={DzInner.Raw})—— 否则静止点 t > 0 ⇒「静止自走」(AC-3-A9③/④)");

            if (DzInner.Raw >= DzOuter.Raw)
                errors.Add($"{tag}DZ_INNER 必须 < DZ_OUTER(raw={DzInner.Raw} vs {DzOuter.Raw})—— 否则除零 / 反区间(AC-3-A9④)");

            // DZ_OUTER ≤ 1:m 已归一,> 1 使 t < 1 恒成立 ⇒ 满速永不触发(AC-3-A9② 的反面)
            if (DzOuter.Raw > Fix.OneRaw)
                errors.Add($"{tag}DZ_OUTER 必须 ≤ 1(raw={DzOuter.Raw})—— 否则满速永不触发(AC-3-A9②/④)");

            // CURVE_POW > 1/2:用整数 ×2 比较(OneRaw/2 = 32768 恰为 1/2;无溢出、无浮点)
            if (CurvePow.Raw <= Fix.OneRaw / 2)
                errors.Add($"{tag}CURVE_POW 必须 > 1/2(raw={CurvePow.Raw})—— 否则端点 C1 破缺(GDD F-3.1 Ⓐ · AC-3-A9④)");

            return errors;
        }
    }
}
