// 权威来源:design/gdd/item-database.md
//   · §Tuning Knobs(:892-949)的「全局常量表」侧 —— 跨条目量(QTY_MULT_* / *_MOD_CAP /
//     ENV_MOD_* / RETAIN_* / EFF_* / MAX_QUALITY);SKILL_CAP 引用自 30 技能系统
//   · §Formulas F1(:468-578)/ F2(:582-620)
//   · AC-21a-3(常量表自洽性 —— 非求解器行为)· AC-21a-48(调参旋钮零硬编码)
//   · ADR-006 §Decision 三(单一舍入模式)· §Decision 四(守恒律整数域内求值)
//   · ADR-025 §①(装配归属:纯 sim 逻辑 → `Sim`,引用集 {BCL, Sim.Contracts})
//
// ⚠️ 本文件**不含任何数值** —— GDD §Tuning Knobs「默认」列一律留空(数值用户自己调)。
//    形状在此声明,值由 21a 全局常量表(assets/data/item_database_constants.json →
//    ADR-014 两阶段烘焙 → IDataProvider)在启动期注入。本故事(003)不建烘焙通路
//    (Story 008 负责),故值由调用方(未来 provider / 测试)构造。
//    这正兑现 AC-21a-48:生产代码里零字面量 ⇒ 不存在「改了 GDD 忘了改代码」的漂移面。
//
// ⚠️ 数值分两处,**不得第三处**(GDD §Tuning Knobs「归属与存放」):
//    逐条量(stack_max / weight / outputs_i.qty / duration_ticks / skill_gate / min_quality)
//    住**物品/配方数据文件**;跨条目量住**本表**。逐条量**不进本表**。

using System;

namespace DaYiJingCheng.Sim
{
    /// <summary>F1 / F2 的全部**跨条目**调参常量(GDD §Tuning Knobs 的「全局常量表」侧)。
    /// <para><b>零数值</b> —— 本类型只声明形状,值经 ADR-014 烘焙产物注入(AC-21a-48)。
    /// 具体取值 = 数值待用户(GDD「默认」列留空)。</para>
    /// <para><b>纯值 struct</b> —— 求解器只读它,不持有任何运行时对象
    /// (可重建性三源不变量:入参即全部输入)。</para>
    /// <para>★ 本表带一条**自洽性**约束(AC-21a-3,由
    /// <see cref="RecipeSettlementConstantTableValidator"/> 校验):
    /// <c>Σ(正的 cap) + max(0, ENV_MOD_MAX) ≤ QTY_MULT_MAX − 1</c> —— 否则高投入段被
    /// <c>clamp</c> <b>静默截断</b>(投入无回报)。</para>
    /// </summary>
    public readonly struct RecipeSettlementConstants
    {
        /// <summary><c>QTY_MULT_MIN</c> —— 最差产出的下限(F1)。安全范围 <c>≥ 0</c>;
        /// <b>产出非零由 <c>max(1, ·)</c> 结构性保证,不依赖本值 &gt; 0</b>(GDD 🔑:原稿断言
        /// 「<c>QTY_MULT_MIN &gt; 0</c> ⇒ 产出非零」为假 —— <c>Round(1 × 0.3) = 0</c>)。</summary>
        public readonly Fix QtyMultMin;

        /// <summary><c>QTY_MULT_MAX</c> —— 最好产出的上限(F1)。安全范围两侧同时锁死:
        /// ① <c>≥ 1 + Σ(正的 cap) + max(0, ENV_MOD_MAX)</c>(否则高投入被静默截断);
        /// ② 守恒上界 <c>QTY_MULT_MAX × Σ(w × 产出基数) ≤ EFF_MAX × Σ(w × 输入基数)</c>
        /// (否则运行期凭空造物 —— AC-21a-8,归 Story 005)。</summary>
        public readonly Fix QtyMultMax;

        /// <summary><c>SKILL_MOD_CAP</c> —— 炮制技能的最大加成(F1)。曲线
        /// <c>SkillMod = cap × Level / SKILL_CAP</c> 由求解器定义,源系统**只传等级**。</summary>
        public readonly Fix SkillModCap;

        /// <summary><c>QUAL_MOD_CAP</c> —— 品级的最大加成(F1)。曲线
        /// <c>QualityMod = cap × (InQ − 1) / (MAX_QUALITY − 1)</c> 由求解器定义。</summary>
        public readonly Fix QualModCap;

        /// <summary><c>EQUIP_MOD_CAP</c> —— 器具 / 医馆的最大加成(F1;源 = 19 制作 · 24 医馆机器)。
        /// GDD 未给曲线形 ⇒ 源系统直接传修正值 <see cref="RecipeSettlementRequest.EquipMod"/>。</summary>
        public readonly Fix EquipModCap;

        /// <summary><c>ENV_MOD_MIN</c> —— 环境修正带下界(F1;可为负:火候难控、背阴)。
        /// 由 F1 正文对**两个未钳制分量之和**唯一钳制(D-21-31)。</summary>
        public readonly Fix EnvModMin;

        /// <summary><c>ENV_MOD_MAX</c> —— 环境修正带上界(F1)。<b>须计入 AC-21a-3</b>
        /// (<c>max(0, ENV_MOD_MAX)</c>)—— 原稿只约束 <c>*_CAP</c> 会漏它。</summary>
        public readonly Fix EnvModMax;

        /// <summary><c>RETAIN_MIN</c> —— 技能 0 时的品级保留率(F2)。安全范围 <c>(0, 1]</c>,
        /// <b>必须 &gt; 0</b>(否则零技能 ⇒ 品级归零)。</summary>
        public readonly Fix RetainMin;

        /// <summary><c>RETAIN_MAX</c> —— 满技能时的品级保留率(F2)。安全范围
        /// <c>[RETAIN_MIN, 1]</c>;<c>= 1.0</c> 表示满技能完全保值;<c>&gt; 1</c> 会被 clamp 掩盖,
        /// 故归构建期拒(AC-21a-11,Story 005)。</summary>
        public readonly Fix RetainMax;

        /// <summary><c>EFF_MIN</c> —— 技能 0 时的转化效率(F1 投入端 / F2 曲线)。
        /// 安全范围 <c>(0, EFF_MAX]</c> —— <b>不含 0</b>:<c>EFF</c> 是 F1 的除数,
        /// <c>EFF_MIN = 0</c> ⇒ 运行期除零,<c>&lt; 0</c> ⇒ 实耗为负(凭空造料)。</summary>
        public readonly Fix EffMin;

        /// <summary><c>EFF_MAX</c> —— 转化效率上限(F1 / F2)。安全范围 <c>≤ 1</c>;
        /// <c>= 1</c> = 满技能无损耗(实耗取等基数);<c>&gt; 1</c> ⇒ 构建期硬失败
        /// (它是「凭空造物」的边界,不是平衡旋钮 —— AC-21a-8)。</summary>
        public readonly Fix EffMax;

        /// <summary><c>MAX_QUALITY</c> —— 品级档数(F2 / F5 / D-21-16 / D-21-24)。
        /// 安全范围 <c>≥ 2</c>(整数);↑ 则品级维度更细,但堆叠基数与
        /// <c>axis_offset_by_quality[]</c> / <c>quality_character[]</c> 长度同步放大。</summary>
        public readonly int MaxQuality;

        /// <summary><c>SKILL_CAP</c> —— 技能上限。<b>引用自 30 技能系统,本系统不拥有</b>
        /// (F2 变量表;<c>Level</c> 与 F2 的 <c>CraftSkill</c> 是同一个量)。
        /// 本表只承载其**值**,不定义其语义。</summary>
        public readonly int SkillCap;

        /// <summary>构造常量表。**值由数据侧提供**(作者态 JSON → ADR-014 烘焙 → provider);
        /// 本构造函数不校验取值范围 —— 范围校验是构建期门(AC-21a-9…13,归 Story 005)。
        /// 本类型只提供 AC-21a-3 的**自洽性**校验。</summary>
        /// <param name="qtyMultMin">QTY_MULT_MIN(≥ 0)。</param>
        /// <param name="qtyMultMax">QTY_MULT_MAX(两侧锁死,见字段注)。</param>
        /// <param name="skillModCap">SKILL_MOD_CAP(≥ 0)。</param>
        /// <param name="qualModCap">QUAL_MOD_CAP(≥ 0)。</param>
        /// <param name="equipModCap">EQUIP_MOD_CAP(≥ 0)。</param>
        /// <param name="envModMin">ENV_MOD_MIN(可负)。</param>
        /// <param name="envModMax">ENV_MOD_MAX(可负;须计入 AC-21a-3)。</param>
        /// <param name="retainMin">RETAIN_MIN(∈ (0, 1])。</param>
        /// <param name="retainMax">RETAIN_MAX(∈ [RETAIN_MIN, 1])。</param>
        /// <param name="effMin">EFF_MIN(∈ (0, EFF_MAX])。</param>
        /// <param name="effMax">EFF_MAX(≤ 1)。</param>
        /// <param name="maxQuality">MAX_QUALITY(≥ 2)。</param>
        /// <param name="skillCap">SKILL_CAP(&gt; 0;引用自 30)。</param>
        public RecipeSettlementConstants(
            Fix qtyMultMin,
            Fix qtyMultMax,
            Fix skillModCap,
            Fix qualModCap,
            Fix equipModCap,
            Fix envModMin,
            Fix envModMax,
            Fix retainMin,
            Fix retainMax,
            Fix effMin,
            Fix effMax,
            int maxQuality,
            int skillCap)
        {
            QtyMultMin = qtyMultMin;
            QtyMultMax = qtyMultMax;
            SkillModCap = skillModCap;
            QualModCap = qualModCap;
            EquipModCap = equipModCap;
            EnvModMin = envModMin;
            EnvModMax = envModMax;
            RetainMin = retainMin;
            RetainMax = retainMax;
            EffMin = effMin;
            EffMax = effMax;
            MaxQuality = maxQuality;
            SkillCap = skillCap;
        }
    }

    /// <summary>常量表**自洽性**校验器(AC-21a-3)。
    /// <para>⚠️ <b>这不是求解器行为</b> —— 该不等式与任何求解输入无关,对求解器恒真;
    /// 原稿把它写成「WHEN 调用 F1」是**范畴错误**(GDD :1034 原文已订正)。</para>
    /// <para>判据:<c>Σ(正的 cap) + max(0, ENV_MOD_MAX) ≤ QTY_MULT_MAX − 1</c>。
    /// 反面(超限即拒)= AC-21a-9,共用夹具
    /// <c>tests/unit/item_database/fixtures/invalid_cap_sum.json</c>(归 Story 005 执行)。</para>
    /// <para>求值在 <b>raw 整数域</b>完成(ADR-006 §Decision 四:整数域内求值,不用浮点比较)。</para>
    /// </summary>
    public static class RecipeSettlementConstantTableValidator
    {
        /// <summary>常量表是否满足 AC-21a-3 的不等式(纯函数,无 I/O)。</summary>
        /// <param name="constants">待校验常量表。</param>
        /// <returns><c>true</c> = 自洽;`false` = 高投入段会被 clamp 静默截断。</returns>
        /// <example><c>RecipeSettlementConstantTableValidator.IsSelfConsistent(c)</c> ⇒ <c>true</c>
        /// (caps 0.3+0.1+0.2 = 0.6,ENV_MOD_MAX 0.5,和 1.1 ≤ QTY_MULT_MAX 2.5 − 1)。</example>
        public static bool IsSelfConsistent(in RecipeSettlementConstants constants)
            => PositiveCapsRaw(constants) + Positive(constants.EnvModMax.Raw)
               <= constants.QtyMultMax.Raw - Fix.OneRaw;

        /// <summary>超限即**硬失败**(<c>throw</c>,非 <c>Debug.Assert</c> —— ADR-024 §Decision ⑤
        /// 同款纪律:构建期门必须显式抛,禁静默)。错误文本点名两边 raw 值,便于数值轮定位。</summary>
        /// <param name="constants">待校验常量表。</param>
        /// <exception cref="InvalidOperationException">不等式不成立。</exception>
        /// <example><c>RecipeSettlementConstantTableValidator.ThrowIfInconsistent(c)</c>
        /// —— 夹具 <c>invalid_cap_sum.json</c> 必抛。</example>
        public static void ThrowIfInconsistent(in RecipeSettlementConstants constants)
        {
            if (IsSelfConsistent(constants)) return;

            long left = PositiveCapsRaw(constants) + Positive(constants.EnvModMax.Raw);
            long right = constants.QtyMultMax.Raw - Fix.OneRaw;
            throw new InvalidOperationException(
                "常量表自洽性失败(AC-21a-3 ≡ AC-21a-9 反面):" +
                $"Σ(正的 cap) + max(0, ENV_MOD_MAX) = {left} raw > QTY_MULT_MAX − 1 = {right} raw。" +
                "后果 = 高投入段被 clamp 静默截断(投入无回报)。" +
                "修法(数值归用户):抬 QTY_MULT_MAX,或降某一 *_CAP / ENV_MOD_MAX。");
        }

        /// <summary>Σ(正的 cap) —— 只取正项,raw 域求和。</summary>
        private static long PositiveCapsRaw(in RecipeSettlementConstants constants)
            => Positive(constants.SkillModCap.Raw)
               + Positive(constants.QualModCap.Raw)
               + Positive(constants.EquipModCap.Raw);

        /// <summary>max(0, ·) —— 负项不计入(ENV_MOD_MAX ≤ 0 时该项消失,AC-21a-3 边缘)。</summary>
        private static long Positive(long raw) => raw > 0L ? raw : 0L;
    }
}
