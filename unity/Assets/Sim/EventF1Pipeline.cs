// ADR-005 / GDD random-events.md F1 —— 抽取管线：配额、上下文门与强度轴。
//
// 权威来源:
//   ADR-005 §Key Interfaces —— 确定性模拟
//   GDD random-events.md F1 —— 四步：档配额 → W_i 调制 → 强度定档 → 预算后生成
//
// 核心机制:
//   - Hamilton 最大余额法整数拆分
//   - ContextMult 只在配额层进入
//   - TODMult 夜窗只乘威胁档
//   - 先舍入后钳制

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// F1 管线输入。
    /// </summary>
    public readonly struct F1PipelineInput
    {
        public readonly long Window;
        public readonly long WindowSize;
        public readonly bool IsNight;
        public readonly bool IsOnExpedition;
        public readonly bool IsInClinic;
        public readonly bool InCombatCooldown;
        public readonly long SeasonIndex;

        public F1PipelineInput(long window, long windowSize, bool isNight, bool isOnExpedition, bool isInClinic, bool inCombatCooldown, long seasonIndex)
        {
            Window = window;
            WindowSize = windowSize;
            IsNight = isNight;
            IsOnExpedition = isOnExpedition;
            IsInClinic = isInClinic;
            InCombatCooldown = inCombatCooldown;
            SeasonIndex = seasonIndex;
        }
    }

    /// <summary>
    /// F1 管线输出。
    /// </summary>
    public readonly struct F1PipelineOutput
    {
        public readonly int[] Quotas;
        public readonly int StrengthTier;
        public readonly bool ContextGatePassed;

        public F1PipelineOutput(int[] quotas, int strengthTier, bool contextGatePassed)
        {
            Quotas = quotas;
            StrengthTier = strengthTier;
            ContextGatePassed = contextGatePassed;
        }
    }

    /// <summary>
    /// F1 抽取管线 —— 配额、上下文门与强度轴。
    /// </summary>
    public static class EventF1Pipeline
    {
        // 档占比（简化版：威胁 3/8, 机会 3/8, 反应 1/8, 灾难 1/8）
        private static readonly int[] TierRatios = { 3, 3, 1, 1 };
        private const int RatioSum = 8;

        /// <summary>
        /// Hamilton 最大余额法整数拆分。
        /// </summary>
        public static int[] HamiltonSplit(long windowSize)
        {
            return HamiltonSplit(windowSize, null, null);
        }

        /// <summary>
        /// Hamilton 最大余额法整数拆分（带上下文乘子）。
        /// 当某档 ×0 时，该档配额 = 0，其余档按原比例分配全部 windowSize。
        /// </summary>
        public static int[] HamiltonSplit(long windowSize, int[] contextMult, int[] reputationMult)
        {
            if (windowSize <= 0)
                throw new ArgumentException("windowSize 必须 > 0", nameof(windowSize));

            var quotas = new int[4];
            var remainders = new long[4];
            long total = 0;

            // 计算有效比例（考虑 ContextMult 和 ReputationMult）
            var effectiveRatios = new long[4];
            long effectiveSum = 0;
            for (int i = 0; i < 4; i++)
            {
                long ratio = TierRatios[i];
                if (contextMult != null) ratio *= contextMult[i];
                if (reputationMult != null) ratio *= reputationMult[i];
                effectiveRatios[i] = ratio;
                effectiveSum += ratio;
            }

            // 如果全部比例为 0，返回全零
            if (effectiveSum == 0)
            {
                return new int[] { 0, 0, 0, 0 };
            }

            // 整数 floor（按有效比例分配全部 windowSize）
            for (int i = 0; i < 4; i++)
            {
                quotas[i] = (int)(windowSize * effectiveRatios[i] / effectiveSum);
                remainders[i] = windowSize * effectiveRatios[i] % effectiveSum;
                total += quotas[i];
            }

            // 余额按余数降序排列（并列按档序：威胁<机会<反应<灾难）
            long remaining = windowSize - total;
            var indices = new[] { 0, 1, 2, 3 };
            Array.Sort(indices, (a, b) =>
            {
                int cmp = remainders[b].CompareTo(remainders[a]);
                return cmp != 0 ? cmp : a.CompareTo(b);
            });

            for (int i = 0; i < remaining && i < 4; i++)
            {
                // 只给配额 > 0 的档分配余额
                if (quotas[indices[i]] > 0)
                {
                    quotas[indices[i]]++;
                }
            }

            return quotas;
        }

        /// <summary>
        /// ContextGate 布尔判据 —— **只判「是否全档被压成 0」**。
        /// </summary>
        /// <remarks>
        /// <para>🔁 <b>2026-11-05 口径订正(`TD-randomevents-05`)</b>:原实现 =
        /// <c>IsOnExpedition &amp;&amp; !IsInClinic</c> —— 那是**全档门**,与
        /// <b>AC-52-29(BLOCKING)</b> 直接冲突:</para>
        /// <para>AC-52-29 逐字:「玩家在医馆地块内,威胁档条目到期 ⇒ 被 <c>ContextMult = 0</c>
        /// 压制……**机会 / 反应档照常生成**」。GDD 规则六之二同款:「医馆不是绝对安全区……
        /// 只有**威胁档**在医馆内被压制」。原实现把机会 / 反应档一并挡死 ⇒ **永不在医馆内生成**,
        /// 与 AC 相反。</para>
        /// <para>现口径:<b>档级压制归 <see cref="ComputeContextMult"/>(进配额层)</b>,
        /// 本方法只回答「有没有任何一档还活着」—— 全档归零的唯一来源 = 战斗冷却
        /// (规则六之二第三行:战斗结算后全档 ×0)。</para>
        /// <para>⚠️ 配套:调用方仍须判 <c>Quotas[tier] &lt;= 0</c> —— 医馆内威胁档走的是
        /// <c>NoQuota</c>,不是 <c>NoContext</c>。两条分支**不同因**,不得合并。</para>
        /// </remarks>
        public static bool EvaluateContextGate(F1PipelineInput input)
        {
            // 全档归零的唯一原因 = 战斗冷却(GDD 规则六之二第三行)。
            // ⚠️ 医馆**不在此处判** —— 它只压威胁档,由 ComputeContextMult 表达(AC-52-29)。
            return !input.InCombatCooldown;
        }

        /// <summary>
        /// 计算 ContextMult（每档的上下文乘子）—— GDD 规则六之二的三行。
        /// 战斗冷却：全档 ×0 · 医馆：威胁档 ×0、其余保留 · 出诊路径：威胁 / 机会档 ×<paramref name="travelMult"/>。
        /// </summary>
        /// <param name="travelMult">
        /// GDD 的 `CONTEXT_TRAVEL`(在出诊路径上 ×，规则逐字要求 **≥ 1**，机会 / 威胁档皆抬)。
        /// <para>🔁 2026-11-05(`TD-randomevents-05`):改前本函数**完全没读 `IsOnExpedition`**
        /// —— 于是那个旗标求值出来也没有任何消费方(「零消费方 = AC 未落地」)。
        /// 现把它接进配额层。</para>
        /// <para>⚠️ <b>数值 = 1 是「尚未调参」，不是「调好了」</b>:GDD 只钉 `≥ 1` 与「抬」的
        /// 方向，具体倍数归**数值轮**(ADR-019 §六:实现侧不得替用户定数值)。
        /// `HamiltonSplit` 的乘子是**整数**，故这里只接受整数倍(分母须为 1)。</para>
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="travelMult"/> &lt; 1
        /// (GDD 逐字要求 `num ≥ den`，即 ≥ 1)。</exception>
        public static int[] ComputeContextMult(F1PipelineInput input, int travelMult = 1)
        {
            if (travelMult < 1)
                throw new ArgumentOutOfRangeException(nameof(travelMult), travelMult,
                    "CONTEXT_TRAVEL 须 ≥ 1(GDD 规则六之二:num ≥ den)");

            var mult = new int[4];

            // 战斗冷却：全档 ×0
            if (input.InCombatCooldown)
            {
                for (int i = 0; i < 4; i++) mult[i] = 0;
                return mult;
            }

            for (int i = 0; i < 4; i++) mult[i] = 1;

            // 在医馆地块：威胁档 ×0，机会/反应保留(AC-52-29)
            if (input.IsInClinic)
            {
                mult[0] = 0;
                return mult;
            }

            // 在出诊路径上：威胁 / 机会档抬升(反应 / 灾难档不动 —— GDD 只点名前两档)。
            if (input.IsOnExpedition)
            {
                mult[0] *= travelMult;
                mult[1] *= travelMult;
            }

            return mult;
        }

        /// <summary>
        /// 计算强度档（先舍入后钳制）。
        /// </summary>
        public static int ComputeStrengthTier(Fix strengthRaw, int minTier, int maxTier)
        {
            // 先舍入
            long rounded = strengthRaw.Round();

            // 后钳制
            if (rounded < minTier) return minTier;
            if (rounded > maxTier) return maxTier;
            return (int)rounded;
        }

        /// <summary>
        /// 执行 F1 管线。
        /// </summary>
        public static F1PipelineOutput Execute(F1PipelineInput input, int minTier, int maxTier)
        {
            // ① 档配额 —— **必须**把 ContextMult 交给拆分器(AC-52-20「ContextMult 只在
            //    配额层进入」)。
            // 🔁 **2026-11-05 修复(`TD-randomevents-05`)**:改前传的是无参重载
            //    (`HamiltonSplit(input.WindowSize)`)⇒ `ComputeContextMult` **零生产调用方**,
            //    医馆 / 战斗冷却对配额**没有任何影响**;而旧夹具只测 `ComputeContextMult`
            //    自身输出、不测它是否进管线 ⇒ 全绿,**自造断言顶替 AC 语义层**(假绿 F1)。
            int[] contextMult = ComputeContextMult(input);
            var quotas = HamiltonSplit(input.WindowSize, contextMult, null);

            // ② ContextGate
            bool contextGate = EvaluateContextGate(input);

            // ③ 强度定档（简化版：固定值）
            int strengthTier = ComputeStrengthTier(new Fix(5000), minTier, maxTier);

            return new F1PipelineOutput(quotas, strengthTier, contextGate);
        }
    }
}
