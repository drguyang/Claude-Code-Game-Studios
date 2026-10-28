// 关卡工具 —— **引擎侧**格点参数的取样面(C2 的另一半)。
//
// 权威来源:
//   ADR-022 §四 **C2**(`|engineParam − Dequantize(bakedInt)| ≤ 1 量子`,硬失败)
//   ADR-015 §一之补(`slopeLimit` / `stepOffset` 的**真源 = 关卡工具**;系统 1 只是执行者)
//   `Sim.Contracts/LatticeParamQuantization`(量子与判据的**唯一出处** —— 本件不另抄公式)
//   ADR-006(`ROUND_HALF_AWAY_FROM_ZERO` 是全场唯一舍入模式;禁 `Math.Round` 默认 ties-to-even)
//
// ═══════════════════════════════════════════════════════════════════════════
// 为什么这里要自己写取整,而不用 `Mathf.RoundToInt`
// ═══════════════════════════════════════════════════════════════════════════
// `Mathf.RoundToInt` 的实现是 `Math.Round(f)`(**ties-to-even**,银行家舍入)。
// ADR-006 §Decision 明写:**单一舍入模式 `ROUND_HALF_AWAY_FROM_ZERO`,禁 ties-to-even**。
// 在 45° × 100 = 4500 这种整数结果上两者看不出差别,但 `stepOffset = 0.0125 m` 这种
// **恰好落在半量子**上的值会:.NET ties-to-even ⇒ 12;away-from-zero ⇒ 13。
// C2 的判据是「≤ 1 量子」—— 差 1 就已经踩在边界上,舍入模式不同会让同一次实测
// 在两种实现下得出不同结论。⇒ 取整方式**必须**与 ADR-006 一致,且**必须**写死在这里,
// 不能让调用方各自 `Mathf.Round`。
//
// ⚠️ **本件只做「读 + 量化」,不做判定** —— 判据在 `WalkabilityParityCheck`,
//    公式在 `LatticeParamQuantization.IsWithinOneQuantum`。三处不重复实现同一个不等式。

using System;
using DaYiJingCheng.Sim.Contracts;   // LatticeParamQuantization(量子常量)
using UnityEngine;                    // CharacterController

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>引擎侧的一对格点参数,**已换算到与烘焙产物相同的量子**。</summary>
    public readonly struct LatticeEngineParams
    {
        /// <summary>`slopeLimit` 的量化值(单位 = 0.01 度)。</summary>
        public readonly int SlopeLimitDegX100;

        /// <summary>`stepOffset` 的量化值(单位 = 1 mm)。</summary>
        public readonly int StepOffsetMm;

        /// <summary>构造一对量化后的引擎侧参数。</summary>
        /// <param name="slopeLimitDegX100">坡度上限(0.01 度)。</param>
        /// <param name="stepOffsetMm">台阶高度(1 mm)。</param>
        public LatticeEngineParams(int slopeLimitDegX100, int stepOffsetMm)
        {
            SlopeLimitDegX100 = slopeLimitDegX100;
            StepOffsetMm = stepOffsetMm;
        }
    }

    /// <summary>从真实 `CharacterController` 读一对参数并量化(取整 = away-from-zero,ADR-006)。</summary>
    public static class CharacterControllerParamProbe
    {
        /// <summary>
        /// 读控制器参数。
        /// </summary>
        /// <param name="controller">引擎侧控制器;**`null` ⇒ 返回 `false`** 并给出理由。</param>
        /// <param name="parameters">量化后的参数(仅在返回 `true` 时有意义)。</param>
        /// <param name="reason">返回 `false` 的原因(可读报文;返回 `true` 时为 `null`)。</param>
        /// <returns>是否取到了参数。</returns>
        /// <remarks>
        /// ⚠️ **`controller == null` 不是错误,是「这一侧今天没有对象可读」** ——
        /// 今日的玩家对象由 `BootRoot` 在**运行期** `AddComponent` 出来(`BootRoot.cs:349`),
        /// 编辑期没有 prefab / 场景实例可读。把「无对象」当失败会让 CI 在**正确配置**下也红,
        /// 把「无对象」当绿则是借绿 ⇒ 本方法只回 `false`,由调用方登记成 **`NOT-RUN`** 第三态。
        /// </remarks>
        public static bool TryRead(CharacterController controller, out LatticeEngineParams parameters,
            out string reason)
        {
            if (controller == null)
            {
                parameters = default;
                reason = "没有 `CharacterController` 对象可读 —— 玩家对象由 BootRoot 在运行期创建," +
                         "编辑期无 prefab / 场景实例";
                return false;
            }

            parameters = new LatticeEngineParams(
                Quantize(controller.slopeLimit, LatticeParamQuantization.SlopeLimitUnitsPerDegree),
                Quantize(controller.stepOffset, LatticeParamQuantization.StepOffsetUnitsPerMeter));
            reason = null;
            return true;
        }

        /// <summary>
        /// 把引擎侧的 `float` 值换算成量化整数(**away-from-zero**;**禁** ties-to-even)。
        /// </summary>
        /// <param name="value">引擎侧值(度 / 米)。</param>
        /// <param name="unitsPerBase">每 1 个基准单位 = 多少量子(取 `LatticeParamQuantization` 的常量)。</param>
        /// <returns>量化值(最近整数;半值向远离 0 的方向)。</returns>
        /// <remarks>
        /// ⚠️ 用 `double` 中转:Unity 的 `slopeLimit` 是 `float`,乘 100 后可能出现
        /// `4499.9995f` 这种表示误差;经 `double` 放大后再取整,能把「本应是 4500」稳稳落回 4500。
        /// 这不改变判据(1 量子容差本来就能吸收误差),只是让**正常情况下**偏差恰为 0。
        /// </remarks>
        public static int Quantize(float value, int unitsPerBase)
        {
            if (unitsPerBase <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(unitsPerBase), $"每基准单位量字数 = {unitsPerBase} 须 > 0(0 会让量化恒为 0)");

            double scaled = (double)value * unitsPerBase;
            // away-from-zero:正数 floor(x+0.5),负数 ceil(x-0.5)。**不是** Math.Round 的默认行为。
            return scaled >= 0
                ? (int)Math.Floor(scaled + 0.5d)
                : (int)Math.Ceiling(scaled - 0.5d);
        }
    }
}
