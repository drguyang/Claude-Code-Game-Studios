// `CombatMagnitude` 的 MAG_FLOOR / MAG_CAP 与 GDD 已裁定值的漂移判据(2026-10-23 round35)。
//
// ── 病灶(实测) ────────────────────────────────────────────────────────────────
// GDD `combat-and-weapon-lines.md` `:1351` / `:1352` **已裁定**:
//   `MAG_FLOOR` | ✅ **`1/20`**(2026-10-07 代拍)· 安全区 (0, 0.2]
//   `MAG_CAP`   | ✅ **`1/2`**(2026-10-07)· 安全区 [FLOOR + max base_step, 1]
//
// 但 `unity/Assets/Sim/CombatMagnitude.cs:43-44` 写的是:
//   public const int MAG_FLOOR = 1000;
//   public const int MAG_CAP   = 10000;
// 且消费点是 **raw 构造** `new Fix(MAG_FLOOR)`(`:66` / `:69` / `:70` / `:82` / `:85` / `:86`)——
// `Fix(long raw)` 是 **Q16.16 raw**,不是数值(`Fix.cs:47`)⇒ 实际值:
//   `1000 / 65536  ≈ 0.01526`(GDD 要 0.05   ⇒ 差 3.28×)
//   `10000 / 65536 ≈ 0.15259`(GDD 要 0.5    ⇒ 差 3.28×)
// 两次都差同一个 3.28 倍,因为 1000/10000 恰好是把「0.0153 / 0.1526」当作十进制写了进来。
//
// ── 为什么既有测试全绿却没抓到 ────────────────────────────────────────────────
// `Tests/EditMode/Combat/combat_magnitude_f252_test.cs:34/42/62/99` 的断言是
//   `Assert.AreEqual(CombatMagnitude.MAG_FLOOR, result.Raw, ...)`
// —— **自引**该常量。于是无论常量是 3277 还是 1000,断言都成立。
// 这与 `judge_test.cs:322` 对 `GetResultMulTiers()` 的自引**同型**:
// **判据测的是常量本身,不测「该常量是否等于已裁定值」**。
// ⇒ `AC-25-2-01` 的字面(「结果 = MAG_FLOOR」)是成立的,但它**不承载** GDD 的数值裁定。
//
// ── 本文件做什么 ──────────────────────────────────────────────────────────────
// 用 **`FixParse.Parse("1/20")` / `Parse("1/2")`** 作为**外部真值**(而不是读 `MAG_FLOOR`),
// 黑盒驱动真 `CombatMagnitude.ComputeDegradedMagnitude` / `ComputeMagnitude`:
//   ① 退化式(`CP:=0`)结果恒 == `1/20`(AC-25-2-09 的**绝对值**层,不是自引层);
//   ② `CP = CP_MAX` 时结果 == `1/20 + base_step`(AC-25-2-02 绝对值层);
//   ③ `MAG_CAP` 的截断点 == `1/2`(超界输入被夹到 `1/2`,不是夹到 `10000` raw);
//   ④ 前提:`MAG_FLOOR ∈ (0, 0.2]`(A13a 安全区)、`MAG_FLOOR < MAG_CAP`(A13b)、`MAG_CAP ≤ 1`。
//      ④ **不是缺陷判据** —— 它修前修后都绿(见该条 remarks 的说明)。
//
// ⚠️ **本文件当前应当红 3 条**(①②③;④ 是前提,恒绿)。
//    红是它的正常工作状态。修法 = 把两个 `const int` 换成两个 `static readonly Fix`
//    (经 `FixParse.Parse`),并改 `new Fix(MAG_FLOOR)` 为直接用该 `Fix`。
//    **本文件不代为修改生产代码** —— 该值属 25 的数值面,且 `:1351` 写着「2026-10-07 代拍」,
//    要改须显式记账。
//
// ── 探针记录(2026-10-23)──────────────────────────────────────────────────────
// **FIX 探针**(把常量改为 GDD raw `3277` / `32768`)实测 **4 绿 1 红**:
//   ①②③ 全转绿 ⇒ 判据认的是**精确 raw 值**,不是「非零即绿」;
//   唯一变红的是 ④ —— 但那是我**自己写坏的**:初稿在 ④ 里断言
//   `MAG_FLOOR != GddFloor.Raw`(把「缺陷存在」写成了判据),修对反而红。
//   ⇒ **该断言已删除**(④ 改为纯前提),本类现 4 条真判据 + 1 条前提。
// 这条教训记为:`前提` 里不得出现「缺陷仍在」型断言 —— 它会让「修好了」变成失败。
//
// 权威来源:
//   `design/gdd/combat-and-weapon-lines.md` `:1351`(MAG_FLOOR = 1/20)/ `:1352`(MAG_CAP = 1/2)
//     `:749`/`:750`(Tuning 表)/ `:1973-1975`(AC-25-2-01/02)/ `:1999`(AC-25-2-09)
//   `unity/Assets/Sim/CombatMagnitude.cs:43-44`(被钉的生产常量)
//   `unity/Assets/Sim.Contracts/Fix.cs:47`(`Fix(long raw)` = raw 构造,非数值构造)
//   ADR-006 §五(`FixParse` 唯一解析入口)

using System;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Combat
{
    /// <summary>
    /// `CombatMagnitude` 的 MAG_FLOOR / MAG_CAP 与 GDD `:1351-1352` 已裁定值的**一致性**判据。
    /// </summary>
    [TestFixture]
    internal sealed class CombatMagnitudeGddValueDriftTest
    {
        // ── 外部真值:一律经 FixParse(ADR-006 §五),不写浮点字面量、不读被钉常量 ──
        private static readonly Fix GddFloor = FixParse.Parse("1/20");   // :1351
        private static readonly Fix GddCap = FixParse.Parse("1/2");      // :1352
        private static readonly Fix BaseStep = FixParse.Parse("1/4");    // :1350 起手值

        private static MagnitudeParams Params(Fix baseStep, Fix cpMax, int cooldownTicks = 15)
            => new MagnitudeParams(baseStep, cpMax, cooldownTicks);

        // ══════════════════════════════════════════════════════════════════
        // ① AC-25-2-09 的绝对值层:敌/兽退化式恒 = MAG_FLOOR(1/20)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 退化式(`CP := 0`)结果须恰为 GDD 的 `MAG_FLOOR = 1/20` 的 raw,与任何 CP 无关。
        /// </summary>
        /// <remarks>
        /// <b>当前应红</b>:生产常量是 `1000`,被当作 raw 用 ⇒ 结果 raw = `1000 + baseStep.Raw`,
        /// 而 GDD 要的是 raw `3277`(= 1/20 × 65536,四舍五入)+ baseStep。
        /// </remarks>
        [Test]
        public void test_degradedMagnitude_equalsGddFloor_notRaw1000()
        {
            Fix result = CombatMagnitude.ComputeDegradedMagnitude(Params(BaseStep, Fix.Zero));

            long expected = GddFloor.Raw + BaseStep.Raw;
            Assert.That(result.Raw, Is.EqualTo(expected),
                "AC-25-2-09 + GDD `:1351`:退化式 = MAG_FLOOR(1/20) + base_step。\n" +
                "外部真值 = FixParse.Parse(\"1/20\").Raw = " + GddFloor.Raw + ",\n" +
                "实测 raw = " + result.Raw + "(= " + result.Raw + " / 65536 = " +
                ((double)result.Raw / 65536.0).ToString("F5") + " 数值)。\n" +
                "生产常量 `CombatMagnitude.MAG_FLOOR = " + CombatMagnitude.MAG_FLOOR + "`\n" +
                "被当作 **raw** 喂进 `new Fix(MAG_FLOOR)` ⇒ 实际地板 = " + CombatMagnitude.MAG_FLOOR +
                " / 65536 ≈ " + ((double)CombatMagnitude.MAG_FLOOR / 65536.0).ToString("F5") +
                ",而非 GDD 的 0.05。\n" +
                "⇒ 零技能者(及全部敌/兽)的每一次命中都只有 GDD 地板的 ~30.5%");
        }

        /// <summary>
        /// AC-25-2-09 的**语义**层(与 ① 分开):退化式结果与传入 CP 无关。
        /// </summary>
        /// <remarks>
        /// <b>本条不判数值</b>,只判「禁出现第二条敌方装配式」——即使地板值错了,这一条也应绿。
        /// 单列它,是为了让 ① 的红**只可能**归因到「地板值错」而不是「退化式写错」。
        /// </remarks>
        [Test]
        public void test_degradedMagnitude_independentOfAnyCpValue_premise()
        {
            Fix a = CombatMagnitude.ComputeDegradedMagnitude(Params(BaseStep, Fix.Zero));
            Fix b = CombatMagnitude.ComputeDegradedMagnitude(Params(BaseStep, FixParse.Parse("80")));

            Assert.That(b.Raw, Is.EqualTo(a.Raw),
                "AC-25-2-09:退化式与 CP 输入无关(判据 = 传任意 CP 值结果不变)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ② AC-25-2-02 的绝对值层:CP = CP_MAX 时 = MAG_FLOOR + base_step
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// `CP = CP_MAX` 时结果须恰为 GDD 的 `MAG_FLOOR + base_step`。
        /// </summary>
        /// <remarks>
        /// <b>本条的判别力弱于 ①</b>:两端都加了 `base_step`,地板若偏小它也跟着偏小
        /// ⇒ 若 ① 红、② 也红,说明「地板值」这一根因同时解释了二者。
        /// 保留它是因为 AC-25-2-02 是**独立的 AC**,不因 ① 覆盖而免检。
        /// </remarks>
        [Test]
        public void test_fullCpMagnitude_equalsGddFloorPlusBaseStep()
        {
            Fix cpMax = FixParse.Parse("79");            // CP_MAX 派生量(GDD `:759` 起手值 79.2)
            Fix result = CombatMagnitude.ComputeMagnitude(cpMax, Params(BaseStep, cpMax));

            long expected = GddFloor.Raw + BaseStep.Raw;
            Assert.That(result.Raw, Is.EqualTo(expected),
                "AC-25-2-02 + GDD `:1351`:CP = CP_MAX ⇒ MAG_FLOOR(1/20) + base_step。\n" +
                "外部真值 raw = " + expected + ",实测 raw = " + result.Raw);
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ MAG_CAP 的截断点 = 1/2(不是 raw 10000)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 超界输入须被 clamp 到 GDD 的 `MAG_CAP = 1/2` 的 raw(32768),而非 `10000`。
        /// </summary>
        /// <remarks>
        /// <b>当前应红</b>:生产 `MAG_CAP = 10000` 当 raw ⇒ 封顶 10000/65536 ≈ 0.1526,
        /// 而 GDD 要 0.5 ⇒ 高技能段被**静默削平到不足三分之一**。
        /// 这正是 AC-25-2-03 想防的失败模式(「高技能段被 clamp 静默削平」)——
        /// 只是这里不是 `MAG_CAP &lt; FLOOR + max base_step` 造成,而是**单位错**造成。
        /// </remarks>
        [Test]
        public void test_magnitudeCap_clampsAtGddHalf_notRaw10000()
        {
            // 极大 CP + 极大 base_step ⇒ 必然触顶
            Fix hugeCp = FixParse.Parse("100000");
            Fix hugeStep = FixParse.Parse("1000");
            Fix result = CombatMagnitude.ComputeMagnitude(hugeCp, Params(hugeStep, FixParse.Parse("1")));

            Assert.That(result.Raw, Is.EqualTo(GddCap.Raw),
                "GDD `:1352`:MAG_CAP = 1/2 ⇒ raw " + GddCap.Raw + "。\n" +
                "实测 raw = " + result.Raw + "(≈ " +
                ((double)result.Raw / 65536.0).ToString("F5") + "),生产常量 MAG_CAP = " +
                CombatMagnitude.MAG_CAP);
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 前提:结构约束 A13a / A13b(A 组)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// A13a:`MAG_FLOOR ∈ (0, 0.2]`;A13b:`MAG_FLOOR &lt; MAG_CAP`;且两者都应按 raw 解释。
        /// </summary>
        /// <remarks>
        /// <b>本条修前修后都应绿</b> —— 它是**前提**,不是缺陷判据:
        /// 修前 `1000/65536 ≈ 0.0153`、`10000/65536 ≈ 0.1526` 恰在各自安全区内;
        /// 修后 `3277/65536 = 0.05`、`32768/65536 = 0.5` 同样在安全区内(`:1351` 明写 1/20 ∈ (0,0.2])。
        /// <para><b>它想说明的事</b>:区间约束(A13a/b)**成立**推不出「值等于已裁定值」——
        /// 两者是不同层,前者由 GDD `:749`/`:750` 的安全区承载,后者由 `:1351`/`:1352` 的裁定承载。
        /// 既往测试只守了区间(且是自引式),没人守裁定值 —— 这才是漂移存活的原因。</para>
        /// <para><b>⚠️ 曾经的写法是坏的(已改)</b>:初稿在此断言
        /// <c>MAG_FLOOR != GddFloor.Raw</c>(即「常量不是 3277」)——
        /// 那等于**把缺陷的存在写成判据**:一旦有人把值修对,本条反而变红。
        /// 探针 FIX(将常量改为 3277/32768)当场实测到这一点(4 绿 1 红,红的正是本条)
        /// ⇒ 已删除该断言,只保留真正的前提(区间)。</para>
        /// </remarks>
        [Test]
        public void test_structuralBounds_hold_premise()
        {
            // 常量是 raw(与 Fix.cs:47 的 `Fix(long raw)` 同域),故须除以 OneRaw 才是数值
            double floorValue = (double)CombatMagnitude.MAG_FLOOR / Fix.OneRaw;
            double capValue = (double)CombatMagnitude.MAG_CAP / Fix.OneRaw;

            Assert.That(floorValue, Is.GreaterThan(0.0), "A13a 下界:MAG_FLOOR > 0");
            Assert.That(floorValue, Is.LessThanOrEqualTo(0.2),
                "GDD `:749` 安全区上界 (0, 0.2] —— 实测 " + floorValue.ToString("F5"));
            Assert.That(floorValue, Is.LessThan(capValue), "A13b:MAG_FLOOR < MAG_CAP");
            Assert.That(capValue, Is.LessThanOrEqualTo(1.0),
                "GDD `:1352` 安全区上端:>1 ⇒ 一击填满 bar");
        }
    }
}
