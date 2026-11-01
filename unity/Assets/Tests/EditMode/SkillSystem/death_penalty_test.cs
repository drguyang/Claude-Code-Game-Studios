// ============================================================================
// 死亡掉级 EditMode 单测 —— Story 005 验收(30 侧承载)
// 权威来源: design/gdd/death-and-respawn.md **F-29-1**(整数截断除法 · 2026-09-19 修订)
//   · production/epics/skill-system/story-005-death-penalty.md(AC-1 ~ AC-5 初版钉值)
//   · ADR-026 §Decision 四(调参表 Fix 字段) + Implementation Guidelines
//   · ADR-005 确定性 sim(纯函数,无随机)
// ============================================================================
// 🔁 **2026-11-01 订正(S23-3 · 原钉值不删,如实登记)**:本文件原按 30 §4.3 初版
//   公式 `floor(level × 0.95)`(Q16.16 Fix 路径)钉值 {56, 37, 18} —— 该路径是
//   death-and-respawn GDD F-29-1 已登记的「同源同错」:`FixParse("19/20") = 62259`
//   (62259.2 量化丢低位),`20 × 62259 >> 16 = 18 ≠ 表值 19`。F-29-1 明裁:
//   **「在 30 修正前,以本 GDD 的 F-29-1 为掉级语义的权威式」**。S23-3 兑付该挂账
//   义务的**代码半边**(公式行文本订正仍挂 30 下一轮,见 skill-system.md §4.3 同日注),
//   本文件三条主例随权威式订正为 F-29-1 表值 **{57, 38, 19}**;原 Fix 路径的中间值
//   断言保留为**陷阱钉**(防止将来「顺手定点化」回潮)。
// ============================================================================
// 落点:unity/Assets/Tests/EditMode/SkillSystem/death_penalty_test.cs
// 测试策略:全部用 int 断言等级结果(F-29-1 表逐位);零随机种子、零时间依赖、零外部 I/O。
// ============================================================================

using System;
using NUnit.Framework;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;

namespace DaYiJingCheng.Tests.Unit.SkillSystem
{
    /// <summary>Story 005 死亡掉级的 EditMode 单测(权威式 = F-29-1 整数截断)。</summary>
    [TestFixture]
    internal sealed class DeathPenaltyTest
    {
        /// <summary>AC-1/AC-2 主例: (60 × 19) / 20 = 1140 / 20 = 57(F-29-1 表逐位)。</summary>
        [Test]
        public void test_ac2_level60_returns57()
        {
            int result = DeathPenalty.Apply(60);
            Assert.That(result, Is.EqualTo(57),
                "F-29-1 表: 60 → 57(整数截断 (60×19)/20)");
        }

        /// <summary>AC-1/AC-2 边缘: (40 × 19) / 20 = 760 / 20 = 38(F-29-1 表逐位)。</summary>
        [Test]
        public void test_ac2_level40_returns38()
        {
            int result = DeathPenalty.Apply(40);
            Assert.That(result, Is.EqualTo(38),
                "F-29-1 表: 40 → 38(整数截断 (40×19)/20)");
        }

        /// <summary>AC-1/AC-2 边缘: (20 × 19) / 20 = 380 / 20 = 19(F-29-1 表逐位)。</summary>
        [Test]
        public void test_ac1_level20_returns19()
        {
            int result = DeathPenalty.Apply(20);
            Assert.That(result, Is.EqualTo(19),
                "F-29-1 表: 20 → 19(整数截断 (20×19)/20)");
        }

        /// <summary>陷阱钉:F-29-1 定点陷阱的**数值本体** —— 若实现回潮成 Fix 路径,
        /// `20 × 62259 = 1245180`,`>> 16 = 18`(≠ 表值 19),本断言的注释链即失效。
        /// 本断言钉的是 Fix.Mul 的中间 raw 值(1245180),与 Apply 的正确输出 19
        /// **并排陈列**差一 —— 这正是 F-29-1 禁 `floor_Fix(Mul)` 的原因。</summary>
        [Test]
        public void test_ac4_fixTrap_rawValue_showsWhyIntTruncation()
        {
            Fix product = new Fix(20L * Fix.OneRaw) * SkillTuningTable.Default.DeathLoss;
            // 20 × 62259(Q16.16)= 1245180;>> 16 = 18 —— 与 F-29-1 表值 19 差一
            Assert.That(product.Raw, Is.EqualTo(1245180L),
                "定点路径中间值: 20×62259 = 1245180(>>16 得 18 ≠ 表值 19 —— F-29-1 定点陷阱本体)");
            Assert.That(DeathPenalty.Apply(20), Is.EqualTo(19),
                "权威式(int 截断)必须给 19,不是定点路径的 18");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-3: 允许掉到 0(不设下限保护)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-3 主例: (1 × 19) / 20 = 0(允许掉到 0 —— 「手生到不会」为设计语义)。</summary>
        [Test]
        public void test_ac3_level1_returns0()
        {
            int result = DeathPenalty.Apply(1);
            Assert.That(result, Is.EqualTo(0),
                "(1×19)/20 = 0(允许掉到 0)");
        }

        /// <summary>AC-3 边缘: 0 级再死仍是 0(恒等不动点)。</summary>
        [Test]
        public void test_ac3_level0_returns0()
        {
            int result = DeathPenalty.Apply(0);
            Assert.That(result, Is.EqualTo(0),
                "(0×19)/20 = 0(0 级稳定,不再减)");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AC-4: 截断语义(非舍入)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>AC-4: (10 × 19) / 20 = 9.5 → 9(截断,非 away-from-zero;
        /// Fix.Div 会把它舍成 10 —— F-29-1 禁 `Fix.Div` 的原因)。</summary>
        [Test]
        public void test_ac4_level10_returns9_floorTruncation()
        {
            int result = DeathPenalty.Apply(10);
            Assert.That(result, Is.EqualTo(9),
                "(10×19)/20 = 9.5 → 9(截断,非舍入)");
        }

        /// <summary>AC-4: (19 × 19) / 20 = 18.05 → 18。</summary>
        [Test]
        public void test_ac4_level19_returns18()
        {
            int result = DeathPenalty.Apply(19);
            Assert.That(result, Is.EqualTo(18),
                "(19×19)/20 = 18.05 → 18");
        }

        // ═══════════════════════════════════════════════════════════════════
        // 异常输入验证
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>等级超出 SKILL_CAP: (61 × 19) / 20 = 57(不抛异常;上限约束归成长侧)。</summary>
        [Test]
        public void test_ac2_levelAboveCap_returnsTruncated()
        {
            int result = DeathPenalty.Apply(61);
            Assert.That(result, Is.EqualTo(57),
                "(61×19)/20 = 57.95 → 57(超上限仍截断)");
        }

        /// <summary>负等级 → ArgumentOutOfRangeException。</summary>
        [Test]
        public void test_negativeLevel_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                DeathPenalty.Apply(-1);
            }, "currentLevel 为负须抛 ArgumentOutOfRangeException");
        }

        // ═══════════════════════════════════════════════════════════════════
        // DEATH_LOSS 常量验证(调参表默认值 + 与精确比的同源同步)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>默认 DEATH_LOSS = FixParse("19/20") = Fix(0.95)。</summary>
        [Test]
        public void test_defaultDeathLoss_is095()
        {
            Fix expected = Fix.FromRational(19L, 20L);
            Assert.That(SkillTuningTable.Default.DeathLoss.Raw, Is.EqualTo(expected.Raw),
                "Default.DeathLoss = FixParse('19/20') = 0.95");
        }

        /// <summary>同步守卫:`DeathPenalty.LossNum/LossDen`(精确比,求值依据)与
        /// `SkillTuningTable.Default.DeathLoss`(近似形,调参面)**必须同源** ——
        /// 数值轮改其一须同批改另一(DeathPenalty.cs 文件头义务的机械面)。</summary>
        [Test]
        public void test_lossPair_syncsWithTuningTableDeathLoss()
        {
            Fix fromPair = Fix.FromRational(DeathPenalty.LossNum, DeathPenalty.LossDen);
            Assert.That(SkillTuningTable.Default.DeathLoss.Raw, Is.EqualTo(fromPair.Raw),
                "DeathLoss(近似形)与 LossNum/LossDen(精确形)不同源 = 调参失同步,拒收");
        }
        // TODO: AC-5 — SkillGrown 事件触发契约由 Story 007 覆盖测试,
        // 本文件只定义「掉级后触发」语义,不验证事件机制(S23-3 在 29 侧补编排面)。
    }
}
