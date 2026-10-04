// patient-ai Story 002 —— 13 的感知判据与决策 LOD(GDD F-13.3 / F-13.4)。
//
// 权威来源:
//   GDD design/gdd/patient-ai.md §Formulas F-13.3(`Perceives` 整数格距,禁 sqrt)
//     · F-13.4(`Band(p)` 三档 + `DecisionInterval(p)`)· §Tuning 二(NEAR_R < FAR_R 装载断言)
//   ADR-016 §三(感知 = 粗粒度整数格;禁读表现态位置)
//   TR-patient-009(感知输入 = 粗粒度整数格;禁 Transform/Raycast/NavMesh;格距整数平方和禁 sqrt)
//   TR-patient-017(决策 LOD:同配置逐位一致 + 判据白名单 + 解冻不补算)
//
// ⚠️ **本件是 13 侧的独立落实,不是 27 的共享代码**(GDD F-13.7 注:13 与 27 共享基础设施
//    但不共享代码)。27 的 LOD 阈值 / 状态集与 13 不同。
//
// ⚠️ **禁 `sqrt`**(AC-13-E3):全部比较走**整数平方和**。`d2` 在 int64 域求值
//    (Δx 可达 i32 满量程 ⇒ Δx² 可达 ~2^62,三项和溢 int32,故用 long)。
//    ⚠️ 量程断言:构造期 `PERCEPT_R²` / `NEAR_R²` / `FAR_R²` 必须 ≤ 安全上限,
//       否则三项和可能溢 int64(EC-13-02)。见 `SpatialBands.Validate`。

using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.PatientAI
{
    /// <summary>决策 LOD 三档(GDD F-13.4)。</summary>
    public enum LodBand
    {
        /// <summary>`d² ≤ NEAR_R²` —— 最近,最高决策频率。</summary>
        Near = 0,
        /// <summary>`NEAR_R² &lt; d² ≤ FAR_R²`。</summary>
        Mid = 1,
        /// <summary>否则 —— 最远,最低决策频率。</summary>
        Far = 2
    }

    /// <summary>13 的感知 / LOD 配置(GDD §Tuning 二)。
    /// <para><b>硬约束</b>(装载断言):<c>0 &lt; NEAR_R &lt; FAR_R</c> ·
    /// <c>0 &lt; PERCEPT_R</c> · 三个半径的平方 ≤ <see cref="MaxSafeRadius"/>
    /// (量程断言 —— 三项平方和须可容 int64)。</para></summary>
    public readonly struct SpatialBands
    {
        /// <summary>感知半径(格)—— `PERCEPT_R`。</summary>
        public readonly int PerceptR;
        /// <summary>LOD 近档边界(格)—— `NEAR_R`。</summary>
        public readonly int NearR;
        /// <summary>LOD 远档边界(格)—— `FAR_R`。</summary>
        public readonly int FarR;
        /// <summary>三档决策间隔(tick)—— `LOD_INTERVAL[Near/Mid/Far]`。</summary>
        public readonly int IntervalNear;
        public readonly int IntervalMid;
        public readonly int IntervalFar;

        public SpatialBands(int perceptR, int nearR, int farR,
                            int intervalNear, int intervalMid, int intervalFar)
        {
            PerceptR = perceptR; NearR = nearR; FarR = farR;
            IntervalNear = intervalNear; IntervalMid = intervalMid; IntervalFar = intervalFar;
        }

        /// <summary>半径安全上限 —— 三项 `r²` 和须 &lt; int64 满量程(<c>2^63−1</c>)。
        /// <para>取 <c>2^21</c>(≈ 2.09M 格 ⇒ `r² ≈ 2^42`,三项和 ≈ `2^44`,远在界内)。
        /// 远超任何可玩世界尺度(ADR-015 单一整数格),纯作**溢出护栏**。</para></summary>
        public const int MaxSafeRadius = 1 << 21;

        /// <summary>GDD §Tuning 二「样例合法集」—— **不是真值**,数值轮由用户改。</summary>
        public static SpatialBands Default => new SpatialBands(
            perceptR: 12, nearR: 8, farR: 32,
            intervalNear: 2, intervalMid: 5, intervalFar: 20);

        /// <summary>硬约束校验(照 <see cref="BehaviorBands.Validate"/> 先例:不引 NUnit)。</summary>
        public static System.Collections.Generic.List<string> Validate(in SpatialBands b)
        {
            var errs = new System.Collections.Generic.List<string>();

            // AC-13-E4 / §Tuning 二:0 < NEAR_R < FAR_R
            if (!(b.NearR > 0))
                errs.Add($"[F-13.4] NEAR_R({b.NearR}) 须 > 0");
            if (!(b.FarR > b.NearR))
                errs.Add($"[F-13.4] FAR_R({b.FarR}) 须 > NEAR_R({b.NearR}) —— 否则 Mid 档为空");

            // 感知半径
            if (!(b.PerceptR > 0))
                errs.Add($"[F-13.3] PERCEPT_R({b.PerceptR}) 须 > 0");

            // 量程(EC-13-02):三项平方和溢 int64 护栏
            foreach (var (name, r) in new[] { ("PERCEPT_R", b.PerceptR), ("NEAR_R", b.NearR), ("FAR_R", b.FarR) })
                if (r > MaxSafeRadius)
                    errs.Add($"[EC-13-02] {name}({r}) 须 ≤ MaxSafeRadius({MaxSafeRadius}) —— 否则 d² 三项和可能溢 int64");

            // 决策间隔须为正(0 ⇒ 每 tick 决策,退化为无节流;负 ⇒ 无意义)
            if (b.IntervalNear <= 0) errs.Add($"[F-13.4] LOD_INTERVAL[Near]({b.IntervalNear}) 须 > 0");
            if (b.IntervalMid <= 0) errs.Add($"[F-13.4] LOD_INTERVAL[Mid]({b.IntervalMid}) 须 > 0");
            if (b.IntervalFar <= 0) errs.Add($"[F-13.4] LOD_INTERVAL[Far]({b.IntervalFar}) 须 > 0");

            return errs;
        }
    }

    /// <summary>F-13.3 / F-13.4 的纯整数落实。**零浮点、零 sqrt、零引擎 API**(AC-13-E3)。
    /// <para>全部输入 = 两个 <see cref="WorldPos"/> 整数格 + 常数(AC-13-E4 白名单)。</para></summary>
    public static class SpatialPerception
    {
        /// <summary>`d² = Δx² + Δy² + Δz²`(GDD F-13.3)—— **整数平方和,int64 承载**。
        /// <para>⚠️ Δ 在 i32 域差分;`Δx²` 可达 ~`2^62` ⇒ 三项和**必须**用 long
        /// (int32 会静默回绕)。量程由 <see cref="SpatialBands.MaxSafeRadius"/> 断言守。</para></summary>
        public static long SqrDistance(WorldPos a, WorldPos b)
        {
            long dx = (long)a.X - b.X;
            long dy = (long)a.Y - b.Y;
            long dz = (long)a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        /// <summary>`Perceives(p, player)`(GDD F-13.3)—— 整数平方和比较,**禁 sqrt**。
        /// <para><c>Δx² + Δy² + Δz² ≤ PERCEPT_R²</c>。半径平方用 long 求,防 int32 溢。</para></summary>
        public static bool Perceives(WorldPos patientCell, WorldPos playerCell, int perceptR)
        {
            long r2 = (long)perceptR * perceptR;
            return SqrDistance(patientCell, playerCell) <= r2;
        }

        /// <summary>`Band(p)`(GDD F-13.4)—— 按 `d²` 三档,输入恰 ⊆ {`d²`, 常数}。
        /// <para>⚠️ AC-13-E4:**禁相机可见性 / `Time.deltaTime` / 帧号**进入本判据。</para></summary>
        public static LodBand Band(WorldPos patientCell, WorldPos playerCell, in SpatialBands bands)
        {
            long d2 = SqrDistance(patientCell, playerCell);
            long near2 = (long)bands.NearR * bands.NearR;
            if (d2 <= near2) return LodBand.Near;
            long far2 = (long)bands.FarR * bands.FarR;
            if (d2 <= far2) return LodBand.Mid;
            return LodBand.Far;
        }

        /// <summary>`DecisionInterval(p) = LOD_INTERVAL[Band(p)]`(GDD F-13.4)。</summary>
        public static int DecisionInterval(WorldPos patientCell, WorldPos playerCell, in SpatialBands bands)
        {
            switch (Band(patientCell, playerCell, bands))
            {
                case LodBand.Near: return bands.IntervalNear;
                case LodBand.Mid: return bands.IntervalMid;
                default: return bands.IntervalFar;
            }
        }

        /// <summary>LOD 节流判据:本 tick 是否该重决策。
        /// <para>输入恰 ⊆ {`Tick`, `last_decision_tick`, `LOD_INTERVAL`}(AC-13-E4 白名单)——
        /// **禁**帧号 / 墙钟。</para></summary>
        public static bool ShouldDecideNow(long tick, long lastDecisionTick, int interval)
        {
            // interval ≤ 0 视为「每 tick 决策」(配置错由 Validate 挡,此处防御性退化为 1)
            if (interval <= 1) return true;
            return tick - lastDecisionTick >= interval;
        }
    }
}
