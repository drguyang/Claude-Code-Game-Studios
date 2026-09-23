// 权威来源:design/gdd/item-database.md §Formulas F1(:468-578)/ F2(:582-620)
//          · §Formulas 节首「权威边界(Q1 裁决)」:21 持有**唯一配方结算求解器**,
//            17 采集 / 18 炮制 / 19 制作只产出输入参数,不得自建结算逻辑
//          · AC-21a-1 / 2 / 4(数学保证)· AC-21a-5(唯一性 —— 入口见 RecipeSettlementEntryPoints.cs)
//          · ADR-005(主):确定性模拟 —— 纯函数、整数定点域、可逐位重放
//          · ADR-005 Amendment G:128 位中间结果唯一类型 = 手工 hi/lo 两 ulong
//          · ADR-006(次):单一舍入 ROUND_HALF_AWAY_FROM_ZERO、守恒律整数域求值
//          · ADR-025 §①:纯 sim 逻辑 → `Sim` 装配(引用集 {BCL, Sim.Contracts})
//
// ⚠️ **单一实现**(AC-21a-5/6 的全部意义):F1/F2 的每个等式在本文件各出现**一次**,
//    以 GDD 的**具名量**为公开纯函数(SkillMod / QualityMod / EnvMod_total / ΣM /
//    QtyMultiplier / OutputQty_i / Retain / EFF / OutputQuality / ActualConsumed_j)。
//    `Solve` 只是这些具名量的**复合**。18/19 侧不得重写其中任何一个(见入口文件)。
//
// ⚠️ **128 位中间结果**:本文件**不自建** hi/lo —— 唯一需要的中间乘(基数 × QtyMultiplier、
//    InputQuality × Retain)经 `Fix.operator *` → `Fix.MulRaw`,而 `MulRaw` **就是**
//    Amendment G 的手工 hi/lo 两 ulong 带进位实现(32 位四路拆分、全程无符号、掩码提取、
//    禁有符号右移)。⇒ 本文件零 `System.Int128` / 零 `BigInteger` / 零条件分支式 128 位,
//    且**没有第二份** 128 位代码。Amendment G 由此结构性满足(复用,而非重造)。
//
// ⚠️ **舍入**:全部走 `ROUND_HALF_AWAY_FROM_ZERO`,整数域内一次完成 —— 先乘后除、
//    最后落出参时**一次性**舍入(GDD :508-514 🔑:若先按 int 算,`Level < SKILL_CAP` 时
//    修正恒为 0,曲线退化成两段阶跃 = 死亡)。禁 `Math.Round` 默认 ties-to-even;禁 float/double 中转。
//
// ⚠️ **不写任何流**(Story 009 负责 ActualConsumed 进 Craft 事件载荷):本文件只算出量。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>一次配方结算的全部输入(GDD F1/F2 的变量表 —— **入参即全部输入**)。
    /// <para><b>纯数据</b>:不含任何运行时对象引用(可重建性三源不变量 —— 全部输入 ∈
    /// {事件流, 版本化烘焙数据, 二者的纯函数})。</para>
    /// <para>源系统分工(GDD §Dependencies):<c>Outputs</c>/<c>Inputs</c> 来自配方表(21 拥有);
    /// <c>CraftSkillLevel</c> 来自 30 技能(18 只传**等级**,不传结果);
    /// <c>InputQuality</c> 来自输入实例(21 拥有);<c>EquipMod</c> 来自 19 制作 / 24 医馆机器;
    /// <c>EnvModClimate</c> 来自 5 时间天气(<c>EnvMod_raw</c>,块哈希);
    /// <c>EnvModClinic</c> 来自 24 医馆机器。</para>
    /// </summary>
    public readonly struct RecipeSettlementRequest
    {
        /// <summary>产出项(**逐条**即 F1 的 <c>outputs_i</c>;基数 <c>qty &gt; 0</c> 归 schema 校验
        /// AC-21a-7/16)。配方是 <c>outputs[]</c> 不是一个标量(三轮 blocking #2)——
        /// <b>不存在配方级单一 BaseQty</b>。</summary>
        public readonly RecipeEntry[] Outputs;

        /// <summary>投入项(基数 —— <b>非实耗</b>;实耗 = <c>Ceil(base / EFF)</c> 是运行期派生量,
        /// 只随该次 Craft 历史事件走,D-21-15 / AC-21a-52)。</summary>
        public readonly RecipeEntry[] Inputs;

        /// <summary>炮制技能等级 <c>CraftSkill</c> ∈ [0, SKILL_CAP]。
        /// <b>与 F1 的 <c>Level</c> 是同一个量</b>(F2 变量表)—— 它同时驱动
        /// <c>SkillMod</c>、<c>Retain</c> 与 <c>EFF</c> 三条曲线,故只传一次。</summary>
        public readonly int CraftSkillLevel;

        /// <summary>输入品级 <c>InputQuality</c> ∈ [1, MAX_QUALITY]。
        /// <b><c>n &gt; 1</c> 时 = <c>min(各输入 quality)</c></b>(F2 变量表)——
        /// 取最小值的动作在**调用方**(它是输入事实,不是公式的一部分)。</summary>
        public readonly int InputQuality;

        /// <summary>设备修正 <c>EquipMod</c> ∈ [0, EQUIP_MOD_CAP](19 制作 · 24 医馆机器:
        /// 器具档位、丹房加成)。GDD 未给曲线形 ⇒ 由源系统直接给修正值。</summary>
        public readonly Fix EquipMod;

        /// <summary>环境修正分量甲 <c>EnvMod_climate</c>(5 的 <c>EnvMod_raw</c>,块哈希)。
        /// <b>未钳制、无域</b>(D-21-31)—— 求和与唯一钳制在 F1 正文执行,不落 5。</summary>
        public readonly Fix EnvModClimate;

        /// <summary>环境修正分量乙 <c>EnvMod_clinic</c>(24 医馆分量)。**未钳制、无域**
        /// (D-21-31)—— 不落 24、不落 18(18 只原样透传)。</summary>
        public readonly Fix EnvModClinic;

        /// <summary>构造结算请求。</summary>
        /// <param name="outputs">产出项(逐条基数;非 null)。</param>
        /// <param name="inputs">投入项(基数;非 null)。</param>
        /// <param name="craftSkillLevel">炮制技能等级(CraftSkill ≡ F1 的 Level)。</param>
        /// <param name="inputQuality">输入品级(n&gt;1 时 = min(各输入 quality))。</param>
        /// <param name="equipMod">设备修正(∈ [0, EQUIP_MOD_CAP])。</param>
        /// <param name="envModClimate">环境分量甲(**未钳制**)。</param>
        /// <param name="envModClinic">环境分量乙(**未钳制**)。</param>
        /// <example><c>new RecipeSettlementRequest(recipe.Outputs, recipe.Inputs, 30, 3,
        /// FixParse.Parse("1/5"), FixParse.Parse("-3/5"), FixParse.Parse("1/2"))</c></example>
        public RecipeSettlementRequest(
            RecipeEntry[] outputs,
            RecipeEntry[] inputs,
            int craftSkillLevel,
            int inputQuality,
            Fix equipMod,
            Fix envModClimate,
            Fix envModClinic)
        {
            Outputs = outputs;
            Inputs = inputs;
            CraftSkillLevel = craftSkillLevel;
            InputQuality = inputQuality;
            EquipMod = equipMod;
            EnvModClimate = envModClimate;
            EnvModClinic = envModClinic;
        }
    }

    /// <summary>一次配方结算的全部出参(GDD F1/F2 的出参集)。
    /// <para>🔒 出参与落盘量一律 <c>int</c>(GDD 整数域纪律 D-21-9);<c>Fix</c> 只出现在
    /// **中间量 / 诊断量**(<see cref="QtyMultiplier"/> / <see cref="EnvModTotal"/> /
    /// <see cref="SumOfModifiers"/> / <see cref="Efficiency"/>)。</para>
    /// <para>本结构**不写任何事件流**(Story 009 负责把 <see cref="ActualConsumed"/> 送进
    /// Craft 载荷)。</para>
    /// </summary>
    public readonly struct RecipeSettlementResult
    {
        /// <summary>逐条产出量 <c>OutputQty_i</c>(与 <see cref="RecipeSettlementRequest.Outputs"/>
        /// 同序、同长);<b>每条 ≥ 1</b>(结构性非零地板)。</summary>
        public readonly int[] OutputQty;

        /// <summary>产出品级 <c>OutputQuality</c> ∈ [1, InputQuality](F2)。</summary>
        public readonly int OutputQuality;

        /// <summary>转化效率 <c>EFF</c>(F2 曲线,<b>在 F1 被消费</b>)—— 技能的主出口。</summary>
        public readonly Fix Efficiency;

        /// <summary>逐条实耗 <c>ActualConsumed_j</c>(与 <see cref="RecipeSettlementRequest.Inputs"/>
        /// 同序、同长);<b>每条 ≥ 基数</b>(<c>EFF ≤ 1</c> ⇒ 恒不省基数,满技能取等)。</summary>
        public readonly int[] ActualConsumed;

        /// <summary>配方级乘子 <c>QtyMultiplier</c>(钳制后)。<b>一条配方一个,不逐条。</b></summary>
        public readonly Fix QtyMultiplier;

        /// <summary>环境修正合计 <c>EnvMod_total</c>(**钳制后**)—— F1 正文执行唯一钳制(D-21-31)。</summary>
        public readonly Fix EnvModTotal;

        /// <summary>修正合计 <c>ΣM = SkillMod + QualityMod + EquipMod + EnvMod_total</c>(诊断 / 守恒)。</summary>
        public readonly Fix SumOfModifiers;

        /// <summary>构造结算结果(求解器专用入口)。</summary>
        /// <param name="outputQty">逐条产出量(≥ 1)。</param>
        /// <param name="outputQuality">产出品级(∈ [1, InputQuality])。</param>
        /// <param name="efficiency">转化效率 EFF。</param>
        /// <param name="actualConsumed">逐条实耗(≥ 基数)。</param>
        /// <param name="qtyMultiplier">配方级乘子(钳制后)。</param>
        /// <param name="envModTotal">环境修正合计(钳制后)。</param>
        /// <param name="sumOfModifiers">ΣM。</param>
        public RecipeSettlementResult(
            int[] outputQty,
            int outputQuality,
            Fix efficiency,
            int[] actualConsumed,
            Fix qtyMultiplier,
            Fix envModTotal,
            Fix sumOfModifiers)
        {
            OutputQty = outputQty;
            OutputQuality = outputQuality;
            Efficiency = efficiency;
            ActualConsumed = actualConsumed;
            QtyMultiplier = qtyMultiplier;
            EnvModTotal = envModTotal;
            SumOfModifiers = sumOfModifiers;
        }
    }

    /// <summary>唯一求解器的函数签名(供 18/19 入口以**同一 MethodInfo** 收敛 —— AC-21a-5 的
    /// 反射断言面)。</summary>
    /// <param name="request">结算输入(纯数据)。</param>
    /// <param name="constants">常量表(纯数据)。</param>
    /// <returns>结算出参。</returns>
    public delegate RecipeSettlementResult RecipeSettlementFn(
        in RecipeSettlementRequest request,
        in RecipeSettlementConstants constants);

    /// <summary><b>全案唯一的配方结算求解器</b>(GDD §Formulas 权威边界 Q1 裁决;AC-21a-5/6)。
    /// <para>17 采集 / 18 炮制 / 19 制作**只产出输入参数**,不得自建结算逻辑 ——
    /// 两套算法 = 「两种路径都满足」破产(systems-index §9 C5 的推广)。</para>
    /// <para><b>纯函数</b>:入参即全部输入,不读任何运行时对象、不写任何流、无静态可变状态
    /// ⇒ 同参数集逐位产出同结果(病史流是唯一真源 ⇒ 可重放)。</para>
    /// <para><b>热路径与分配</b>:本类是**事件率**调用(每次 craft 结算一次),不是帧率热路径。
    /// <see cref="Solve"/> 分配两个数组(m + n 长),与输出规模同阶 —— 若日后需要零分配,
    /// 另加缓冲入参重载即可,不改变任何公式体。</para>
    /// </summary>
    public static class RecipeSettlementSolver
    {
        // ══════════════════════ F1 ══════════════════════

        /// <summary>技能修正 <c>SkillMod = SKILL_MOD_CAP × Level / SKILL_CAP</c>(F1)。
        /// <para>⚠️ <b>必须在 Q16.16 原始整数域求值</b>(GDD :508-514 🔑):若先按 <c>int</c> 算,
        /// <c>Level &lt; SKILL_CAP</c> 时结果**恒为 0**(<c>0.3 × 30 / 60</c> 整数除法 = 0),
        /// 曲线退化成两段阶跃。正解 = 先乘后除、全程 <c>long</c>:
        /// <c>19661 × 30 / 60 = 9830.5</c>,按 <c>ROUND_HALF_AWAY_FROM_ZERO</c> 舍入 ⇒ <c>9831</c> ✓
        /// (GDD :512 的示范数 <c>9830</c> 是**截断**写法,与 ADR-006 §三 的舍入契约不一致 ——
        /// 规则优先于示例,以本实现为准)。</para>
        /// </summary>
        /// <param name="constants">常量表(用 <c>SKILL_MOD_CAP</c> / <c>SKILL_CAP</c>)。</param>
        /// <param name="craftSkillLevel">技能等级 ∈ [0, SKILL_CAP](超域归数值轮 / 校验层)。</param>
        /// <returns><c>SkillMod</c>(raw;∈ [0, SKILL_MOD_CAP])。</returns>
        /// <example><c>SkillMod(c, 60)</c> ⇒ <c>SKILL_MOD_CAP</c>;<c>SkillMod(c, 0)</c> ⇒ <c>0</c>。</example>
        public static Fix SkillMod(in RecipeSettlementConstants constants, int craftSkillLevel)
            => CurveScaled(constants.SkillModCap, craftSkillLevel, constants.SkillCap);

        /// <summary>品级修正 <c>QualityMod = QUAL_MOD_CAP × (InQ − 1) / (MAX_QUALITY − 1)</c>(F1)。
        /// 曲线由 21 定义(GDD 变量表:源 = 输入实例)。同 <see cref="SkillMod"/> 的定点域纪律。</summary>
        /// <param name="constants">常量表(用 <c>QUAL_MOD_CAP</c> / <c>MAX_QUALITY</c>)。</param>
        /// <param name="inputQuality">输入品级 ∈ [1, MAX_QUALITY]。</param>
        /// <returns><c>QualityMod</c>(raw;∈ [0, QUAL_MOD_CAP])。</returns>
        /// <example><c>QualityMod(c, 1)</c> ⇒ <c>0</c>(最低品级无加成)。</example>
        public static Fix QualityMod(in RecipeSettlementConstants constants, int inputQuality)
            => CurveScaled(constants.QualModCap, inputQuality - 1, constants.MaxQuality - 1);

        /// <summary>环境修正合计 <c>EnvMod_total = clamp(EnvMod_climate + EnvMod_clinic,
        /// ENV_MOD_MIN, ENV_MOD_MAX)</c>(F1)。
        /// <para>⚠️ 两个分量入参**各自无域**(D-21-31)—— 源系统只供未钳制分量;
        /// <b>求和与唯一钳制在本函数执行</b>,不落 5、不落 24、不落 18(18 只原样透传)。</para>
        /// </summary>
        /// <param name="constants">常量表(用 <c>ENV_MOD_MIN</c> / <c>ENV_MOD_MAX</c>)。</param>
        /// <param name="envModClimate">分量甲(未钳制)。</param>
        /// <param name="envModClinic">分量乙(未钳制)。</param>
        /// <returns><c>EnvMod_total</c>(raw;∈ [ENV_MOD_MIN, ENV_MOD_MAX])。</returns>
        /// <example><c>EnvModTotal(c, Parse("-4/5"), Parse("-1"))</c> ⇒ <c>ENV_MOD_MIN</c>(钳制)。</example>
        public static Fix EnvModTotal(
            in RecipeSettlementConstants constants, Fix envModClimate, Fix envModClinic)
            => Clamp(Add(envModClimate, envModClinic), constants.EnvModMin, constants.EnvModMax);

        /// <summary>修正合计 <c>ΣM = SkillMod + QualityMod + EquipMod + EnvMod_total</c>(F1)。
        /// <para><b>为什么是「相加后乘」而不是「逐项相乘」</b>(GDD :516-521):相加**与顺序无关** ——
        /// 「定死叠加顺序」的要求取相加则**自动消失**;且每一项的边际价值不互相放大,
        /// 玩家可分别理解「技能不够」和「器具不行」。</para>
        /// <para>⚠️ 前提 = **定点整数域**。在 <c>float</c> 域「相加与顺序无关」是**假的**
        /// ((a+b)+c ≠ a+(b+c));本论证只在 D-21-9 的整数域下有效。</para>
        /// </summary>
        /// <param name="skillMod">技能修正。</param>
        /// <param name="qualityMod">品级修正。</param>
        /// <param name="equipMod">设备修正。</param>
        /// <param name="envModTotal">环境修正合计(**已钳制**)。</param>
        /// <returns>ΣM(raw;整数域加法,逐位可交换)。</returns>
        /// <example><c>SumOfModifiers(a, b, c, d).Raw == SumOfModifiers(d, c, b, a).Raw</c>。</example>
        public static Fix SumOfModifiers(Fix skillMod, Fix qualityMod, Fix equipMod, Fix envModTotal)
            => Add(Add(skillMod, qualityMod), Add(equipMod, envModTotal));

        /// <summary>配方级乘子 <c>QtyMultiplier = clamp(1 + ΣM, QTY_MULT_MIN, QTY_MULT_MAX)</c>(F1)。
        /// <b>一条配方一个,不逐条</b>。</summary>
        /// <param name="constants">常量表(用 <c>QTY_MULT_MIN</c> / <c>QTY_MULT_MAX</c>)。</param>
        /// <param name="sumOfModifiers">ΣM。</param>
        /// <returns><c>QtyMultiplier</c>(raw;∈ [QTY_MULT_MIN, QTY_MULT_MAX])。</returns>
        /// <example><c>QtyMultiplier(c, Parse("1/2"))</c> ⇒ <c>1.5</c>(未触界时)。</example>
        public static Fix QtyMultiplier(in RecipeSettlementConstants constants, Fix sumOfModifiers)
            => Clamp(Add(new Fix(Fix.OneRaw), sumOfModifiers), constants.QtyMultMin, constants.QtyMultMax);

        /// <summary>单条产出量 <c>OutputQty_i = max(1, Round(outputs_i.qty × QtyMultiplier))</c>(F1)。
        /// <para>🔑 <b><c>max(1, ·)</c> 才是「产出非零」的机制实现</b>(GDD :549-554):
        /// 原稿断言「<c>QTY_MULT_MIN &gt; 0</c> ⇒ 数学上不可能产出零」**为假** ——
        /// <c>Round(1 × 0.3) = 0</c>。现产出非零**是公式的结构**,不是旋钮的取值承诺:
        /// 想要产出归零必须删掉 <c>max(1, ·)</c>,而删了立刻违反 AC-21a-1。</para>
        /// </summary>
        /// <param name="baseQty">该条产出基数 <c>outputs_i.qty</c>(&gt; 0)。</param>
        /// <param name="qtyMultiplier">配方级乘子(**钳制后**)。</param>
        /// <returns>该条产出量(≥ 1)。</returns>
        /// <example><c>OutputQty(1, Parse("3/10"))</c> ⇒ <c>1</c>(<c>Round(0.3) = 0</c> ⇒ 地板救回);
        /// <c>OutputQty(2, Parse("3/2"))</c> ⇒ <c>3</c>。</example>
        public static int OutputQty(int baseQty, Fix qtyMultiplier)
        {
            Fix baseQtyFix = new Fix((long)baseQty * Fix.OneRaw);
            // 中间乘 = 定点域乘法 → Fix.MulRaw(Amendment G 的手工 hi/lo 两 ulong 带进位)
            long rounded = (baseQtyFix * qtyMultiplier).Round();
            long floored = rounded < 1L ? 1L : rounded;
            return ToInt32Checked(floored, "OutputQty_i");
        }

        /// <summary>单条实耗 <c>ActualConsumed_j = Ceil(inputs_j.qty / EFF)</c>(F1 投入端)。
        /// <para>🔑 <b>代价放在投入损耗,不是产出归零</b>(D-21-10 + D-21-15):<c>EFF ≤ 1</c> ⇒
        /// <c>ActualConsumed ≥ base</c>(满技能 <c>EFF = EFF_MAX = 1</c> 时**取等** = 零损耗)——
        /// 「转化是有代价」逐次结算都成立,不再是构建期口号。</para>
        /// </summary>
        /// <param name="baseQty">该条投入基数 <c>inputs_j.qty</c>(&gt; 0)。</param>
        /// <param name="efficiency">转化效率 <c>EFF</c>(∈ (0, 1])。</param>
        /// <returns>该条实耗(≥ 基数)。</returns>
        /// <exception cref="InvalidOperationException"><c>EFF ≤ 0</c>(除零兜底;构建期门见 AC-21a-11 一族)。</exception>
        /// <example><c>ActualConsumed(3, Parse("1/2"))</c> ⇒ <c>6</c>(低技能烧料凶);
        /// <c>ActualConsumed(3, Parse("1"))</c> ⇒ <c>3</c>(满技能取等)。</example>
        public static int ActualConsumed(int baseQty, Fix efficiency)
        {
            RequirePositiveEfficiency(efficiency);

            long numerator = (long)baseQty * Fix.OneRaw;
            return ToInt32Checked(CeilDiv(numerator, efficiency.Raw), "ActualConsumed_j");
        }

        /// <summary>EFF 守卫 —— 在 <see cref="Solve"/> 与 <see cref="ActualConsumed"/> **共用**,
        /// 使坏常量表的行为**不随投入数组长度分叉**(空投入数组也在同一处被拒)。</summary>
        /// <exception cref="InvalidOperationException"><c>EFF ≤ 0</c>。</exception>
        private static void RequirePositiveEfficiency(Fix efficiency)
        {
            if (efficiency.Raw <= 0L)
                throw new InvalidOperationException(
                    "EFF ≤ 0 —— EFF_MIN ∈ (0, EFF_MAX] 是构建期硬门(GDD §Tuning Knobs);" +
                    "运行期兜底拒绝,不静默除零。");
        }

        // ══════════════════════ F2 ══════════════════════

        /// <summary>品级保留率 <c>Retain(S) = RETAIN_MIN + (RETAIN_MAX − RETAIN_MIN) × (S / SKILL_CAP)</c>(F2)。
        /// <b>技能越高保留越多。</b>曲线为定点插值(禁 int 域先算,理由同 <see cref="SkillMod"/>)。</summary>
        /// <param name="constants">常量表(用 <c>RETAIN_MIN</c> / <c>RETAIN_MAX</c> / <c>SKILL_CAP</c>)。</param>
        /// <param name="craftSkillLevel">技能等级 ∈ [0, SKILL_CAP]。</param>
        /// <returns><c>Retain</c>(raw;∈ [RETAIN_MIN, RETAIN_MAX])。</returns>
        /// <example><c>Retain(c, 0)</c> ⇒ <c>RETAIN_MIN</c>;<c>Retain(c, 60)</c> ⇒ <c>RETAIN_MAX</c>。</example>
        public static Fix Retain(in RecipeSettlementConstants constants, int craftSkillLevel)
            => Interpolate(constants.RetainMin, constants.RetainMax, craftSkillLevel, constants.SkillCap);

        /// <summary>转化效率 <c>EFF = EFF_MIN + (EFF_MAX − EFF_MIN) × (S / SKILL_CAP)</c>(F2)。
        /// <para>🔑 <b>技能的主出口不在 F2 —— 在 <c>EFF</c>,而 <c>EFF</c> 在 F1 被消费</b>
        /// (GDD :609-615)。原设计把技能押在 F2 的档位阶梯上,<c>InputQuality = 1</c> 时
        /// <c>clamp(·,1,1)</c> 恒等 ⇒ 炮制技能对一级原料零效果,线体感死亡。
        /// 现 F2 退为「高品级输入是否保值」这一个离散问题,连续出口移到 <c>EFF</c>。</para>
        /// </summary>
        /// <param name="constants">常量表(用 <c>EFF_MIN</c> / <c>EFF_MAX</c> / <c>SKILL_CAP</c>)。</param>
        /// <param name="craftSkillLevel">技能等级 ∈ [0, SKILL_CAP]。</param>
        /// <returns><c>EFF</c>(raw;∈ [EFF_MIN, EFF_MAX])。</returns>
        /// <example><c>Efficiency(c, 0)</c> ⇒ <c>EFF_MIN</c>;<c>Efficiency(c, 60)</c> ⇒ <c>EFF_MAX</c>。</example>
        public static Fix Efficiency(in RecipeSettlementConstants constants, int craftSkillLevel)
            => Interpolate(constants.EffMin, constants.EffMax, craftSkillLevel, constants.SkillCap);

        /// <summary>产出品级 <c>OutputQuality = clamp(Round(InputQuality × Retain(CraftSkill)), 1, InputQuality)</c>(F2)。
        /// <para>🔑 <b>clamp 的上界是 <c>InputQuality</c>,不是 <c>MAX_QUALITY</c></b>
        /// (GDD :604-607):「输入定上限,技能定能保住多少」—— 炮制永远不能把品级提上去,
        /// 只能尽量不损失。若上界放到 <c>MAX_QUALITY</c>,玩家会拿垃圾原料反复炮制刷品级,
        /// 采集技能白给。</para>
        /// <para>🔑 下界 1 与 <c>RETAIN_MIN &gt; 0</c> 共同保证「不归零」;满技能
        /// <c>RETAIN_MAX = 1</c> 时取等 <c>OutputQuality = InputQuality</c>。</para>
        /// </summary>
        /// <param name="constants">常量表。</param>
        /// <param name="inputQuality">输入品级 ∈ [1, MAX_QUALITY]。</param>
        /// <param name="craftSkillLevel">技能等级 ∈ [0, SKILL_CAP]。</param>
        /// <returns>产出品级(∈ [1, InputQuality])。</returns>
        /// <exception cref="InvalidOperationException"><paramref name="inputQuality"/> &lt; 1(域外;
        /// 否则下钳 1 会**越过**上钳 InputQuality,产出品级反高于输入 = 刷品级路径 —— AC-21a-2 禁止)。</exception>
        /// <example><c>OutputQuality(c, 1, 60)</c> ⇒ <c>1</c>(恒等 clamp);
        /// <c>OutputQuality(c, 5, 0)</c> ⇒ <c>Round(5 × RETAIN_MIN)</c>。</example>
        public static int OutputQuality(
            in RecipeSettlementConstants constants, int inputQuality, int craftSkillLevel)
        {
            if (inputQuality < 1)
                throw new InvalidOperationException(
                    $"InputQuality = {inputQuality} < 1 —— 域 ∈ [1, MAX_QUALITY](F2 变量表);" +
                    "域外的拒收归构建期范围门(Story 005),运行期兜底拒绝 —— " +
                    "否则下钳 1 会越过上钳 InputQuality,产出品级反高于输入(AC-21a-2)。");

            Fix inputQualityFix = new Fix((long)inputQuality * Fix.OneRaw);
            long rounded = (inputQualityFix * Retain(constants, craftSkillLevel)).Round();

            if (rounded > inputQuality) rounded = inputQuality;   // 上界 = 输入品级(不是 MAX_QUALITY)
            if (rounded < 1L) rounded = 1L;
            return ToInt32Checked(rounded, "OutputQuality");
        }

        // ══════════════════════ 复合:唯一求解入口 ══════════════════════

        /// <summary><b>唯一求解入口</b> —— 把上面各具名量复合成一次完整结算(F1 + F2)。
        /// 18 炮制 / 19 制作经各自的入口
        /// (<see cref="ProcessingSettlementEntry"/> / <see cref="CraftingSettlementEntry"/>)
        /// 收敛到**本方法这一个 MethodInfo**(AC-21a-5)。
        /// </summary>
        /// <param name="request">结算输入(纯数据;两个数组非 null)。</param>
        /// <param name="constants">常量表(纯数据)。</param>
        /// <returns>逐条产出量 / 产出品级 / EFF / 逐条实耗 / 钳制后乘子 / 钳制后 EnvMod_total / ΣM。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> 的数组为 null。</exception>
        /// <exception cref="InvalidOperationException">EFF ≤ 0(见 <see cref="ActualConsumed"/>)。</exception>
        /// <exception cref="OverflowException">出参超出 <c>int</c> 域(域错误归数值轮,不静默回绕)。</exception>
        /// <example><c>RecipeSettlementSolver.Solve(request, constants)</c>
        /// ⇒ <c>OutputQty = [2, 3]</c>(GDD F1 示范形状:m=2、ΣM = 0.5 ⇒ QtyMultiplier = 1.5)。</example>
        public static RecipeSettlementResult Solve(
            in RecipeSettlementRequest request, in RecipeSettlementConstants constants)
        {
            if (request.Outputs == null) throw new ArgumentNullException(nameof(request.Outputs));
            if (request.Inputs == null) throw new ArgumentNullException(nameof(request.Inputs));

            Fix envModTotal = EnvModTotal(constants, request.EnvModClimate, request.EnvModClinic);
            Fix sumOfModifiers = SumOfModifiers(
                SkillMod(constants, request.CraftSkillLevel),
                QualityMod(constants, request.InputQuality),
                request.EquipMod,
                envModTotal);
            Fix qtyMultiplier = QtyMultiplier(constants, sumOfModifiers);
            Fix efficiency = Efficiency(constants, request.CraftSkillLevel);

            // 坏常量表(EFF ≤ 0)在**进入计量前**即拒 —— 与 ActualConsumed 共用同一守卫,
            // 使行为不随 Inputs 长度分叉(空数组也在同一处被拒)。
            RequirePositiveEfficiency(efficiency);

            var outputQty = new int[request.Outputs.Length];
            for (int i = 0; i < outputQty.Length; i++)
                outputQty[i] = OutputQty(request.Outputs[i].Qty, qtyMultiplier);

            var actualConsumed = new int[request.Inputs.Length];
            for (int j = 0; j < actualConsumed.Length; j++)
                actualConsumed[j] = ActualConsumed(request.Inputs[j].Qty, efficiency);

            return new RecipeSettlementResult(
                outputQty,
                OutputQuality(constants, request.InputQuality, request.CraftSkillLevel),
                efficiency,
                actualConsumed,
                qtyMultiplier,
                envModTotal,
                sumOfModifiers);
        }

        // ══════════════════════ 私有整数域助手 ══════════════════════
        // 全部在 raw 整数域完成(ADR-006 §Decision 四);唯一舍入模式 = ROUND_HALF_AWAY_FROM_ZERO。
        // `Fix` 的公开面只有 `operator *` / `Raw` / `Round()`(见 Fix.cs)—— 加减与钳制
        // 以 raw 直算表达,不改动基础类型(改 `Fix` 面 = 动 ADR-005/006 承重契约)。

        /// <summary>定点加法(raw 域;整数加法逐位可交换 —— ΣM 顺序无关性的实现面)。</summary>
        private static Fix Add(Fix a, Fix b) => new Fix(a.Raw + b.Raw);

        /// <summary>定点钳制(raw 域比较;禁浮点比较 —— ADR-006 §Decision 四)。</summary>
        private static Fix Clamp(Fix value, Fix lower, Fix upper)
        {
            if (value.Raw < lower.Raw) return lower;
            if (value.Raw > upper.Raw) return upper;
            return value;
        }

        /// <summary>比例曲线 <c>cap × amount / capValue</c>(先乘后除,<b>一次性</b>舍入)。
        /// 中间积落在 <c>long</c>:<c>cap</c> 是 Q16.16 raw(域 ≲ 2^40,ADR-006),
        /// <c>amount ≤ capValue</c> ⇒ 无溢出。
        /// <para>⚠️ <c>amount</c> 先钳入 <c>[0, capValue]</c>(本类的主导惯例 —— 同
        /// <see cref="QtyMultiplier"/> / <see cref="EnvModTotal"/> 的 clamp):域外输入
        /// (如 <c>InputQuality &gt; MAX_QUALITY</c>)由此**不可能**把曲线推过 <c>cap</c>
        /// (否则 ΣM 被静默放大)。域外的**拒收**归构建期范围门(Story 005),不在本纯函数内抛。</para>
        /// </summary>
        /// <exception cref="InvalidOperationException"><c>capValue ≤ 0</c>(如 <c>SKILL_CAP = 0</c> /
        /// <c>MAX_QUALITY = 1</c>)—— 曲线分母,显式拒而非裸 <see cref="DivideByZeroException"/>。</exception>
        private static Fix CurveScaled(Fix cap, int amount, int capValue)
        {
            RequirePositiveCurveDivisor(capValue, "SKILL_CAP / (MAX_QUALITY − 1)");
            int bounded = amount < 0 ? 0 : (amount > capValue ? capValue : amount);
            return new Fix(FixParse.RoundHalfAwayFromZero(cap.Raw * bounded, capValue));
        }

        /// <summary>定点线性插值 <c>lo + (hi − lo) × level / cap</c>(一次性舍入)。
        /// <para>⚠️ <c>level</c> 先钳入 <c>[0, cap]</c>:<c>CraftSkillLevel &gt; SKILL_CAP</c> 由此
        /// **不可能**把 <c>EFF</c> 推过 <c>EFF_MAX ≤ 1</c>(否则 <c>ActualConsumed &lt; 基数</c> =
        /// 凭空造料,违反 ADR-006 §四守恒律;QA 复核 §4d)。</para>
        /// </summary>
        /// <exception cref="InvalidOperationException"><c>cap ≤ 0</c>(<c>SKILL_CAP = 0</c>)。</exception>
        private static Fix Interpolate(Fix lo, Fix hi, int level, int cap)
        {
            RequirePositiveCurveDivisor(cap, "SKILL_CAP");
            int bounded = level < 0 ? 0 : (level > cap ? cap : level);
            return new Fix(lo.Raw + FixParse.RoundHalfAwayFromZero((hi.Raw - lo.Raw) * bounded, cap));
        }

        /// <summary>曲线分母守卫 —— <c>capValue ≤ 0</c> 时显式抛(点分量名),不静默除零。
        /// 承 <see cref="ActualConsumed"/> 对 <c>EFF ≤ 0</c> 的同款纪律;构建期范围门归 Story 005。</summary>
        private static void RequirePositiveCurveDivisor(int capValue, string what)
        {
            if (capValue <= 0)
                throw new InvalidOperationException(
                    $"曲线分母 {what} = {capValue} ≤ 0 —— 必须 > 0(SKILL_CAP > 0 / MAX_QUALITY ≥ 2);" +
                    "构建期范围门归 Story 005,运行期兜底拒绝,不静默除零。");
        }

        /// <summary>整数上取整除法 <c>Ceil(n / d)</c>(入参恒正 —— <c>base &gt; 0</c> · <c>EFF &gt; 0</c>)。
        /// 这是 F1 的 <c>ActualConsumed_j = Ceil(inputs_j.qty / EFF)</c> 的唯一实现。</summary>
        private static long CeilDiv(long numerator, long denominator)
            => (numerator + denominator - 1L) / denominator;

        /// <summary>出参域检查:🔒 GDD 整数域纪律 D-21-9 —— 出参与落盘量一律 <c>int</c>。
        /// 超域 = 数值轮错误,显式抛,**不静默截断 / 不回绕**。</summary>
        private static int ToInt32Checked(long value, string quantity)
        {
            if (value > int.MaxValue || value < int.MinValue)
                throw new OverflowException(
                    $"{quantity} 超出 int 出参域(GDD D-21-9:出参与落盘量一律 int)" +
                    $"—— 实际 raw 值 {value};该域错误归数值轮。");
            return (int)value;
        }
    }
}
