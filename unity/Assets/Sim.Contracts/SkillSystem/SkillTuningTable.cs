// ============================================================================
// 技能调参表 —— 全部数值常量(定点域 / 整数域混合)
// 权威来源: Story 001 · ADR-026 §Decision 四(调参表默认值定点化)
// ============================================================================
// 本文件只存「值」,不实现 JSON 加载(归 21a 数据管线 + ADR-014 两阶段烘焙)。
// 运行期通过 SkillTuningTable.Load(cookedData) 注入,或直接 new 默认实例。
// ============================================================================

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Contracts.SkillSystem
{
    /// <summary>
    /// 技能调参表(不可变)。承载全部数值旋钮(§7 Tuning Knobs)。
    /// <para>Fix 字段在 JSON 源码写字符串 → FixParse(ADR-014);本类只存已解析值。</para>
    /// </summary>
    public sealed class SkillTuningTable
    {
        // ── 曲线参数 ──────────────────────────────────────────────────────────

        /// <summary>升级曲线系数 C(默认 40)。</summary>
        public readonly Fix C;

        /// <summary>升级曲线指数 P(默认 1.5)。取值集 ∈ {整数, 1/2}(G-1 限死)。</summary>
        public readonly Fix P;

        // ── 新颖度参数 ───────────────────────────────────────────────────────

        /// <summary>首次遇见倍率(默认 3.0)。</summary>
        public readonly Fix NoveltyFirst;

        /// <summary>冷却期内倍率(默认 0.2)。</summary>
        public readonly Fix NoveltyDecay;

        /// <summary>新颖度冷却期(tick 数,默认 24000 = 20 min @ 20 Hz)。</summary>
        public readonly int NoveltyCooldownTicks;

        // ── 死亡惩罚 ─────────────────────────────────────────────────────────

        /// <summary>死亡后等级保留比例(默认 0.95 = FixParse("19/20"))。</summary>
        public readonly Fix DeathLoss;

        // ── 战斗效能 ─────────────────────────────────────────────────────────

        /// <summary>医术修正上限(默认 0.20)。</summary>
        public readonly Fix MedCombatMod;

        /// <summary>各武器线系数(索引 = WeaponLine ordinal)。</summary>
        public readonly Fix[] WeaponMultipliers;

        // ── 诊断精度档 ───────────────────────────────────────────────────────

        /// <summary>诊断精度档阈值(默认 [10, 20, 35, 50])。</summary>
        public readonly int[] DiagTiers;

        /// <summary>洞察层级解锁等级(默认 [10, 20, 35, 50])。</summary>
        public readonly int[] InsightTiers;

        /// <summary>各层洞察治疗加成(默认 [5%, 10%, 20%, 30%])。</summary>
        public readonly Fix[] InsightBonus;

        // ── 昏迷阈值 ─────────────────────────────────────────────────────────

        /// <summary>敌人昏迷的生命阈值(恒为 0 = 生命归零即昏迷)。</summary>
        public static readonly Fix UnconsciousAt = default; // Fix(0)

        // ── 构造器 ────────────────────────────────────────────────────────────

        /// <summary>构造不可变调参表。</summary>
        public SkillTuningTable(
            Fix c, Fix p, Fix noveltyFirst, Fix noveltyDecay, int noveltyCooldownTicks,
            Fix deathLoss, Fix medCombatMod, Fix[] weaponMultipliers,
            int[] diagTiers, int[] insightTiers, Fix[] insightBonus)
        {
            C = c;
            P = p;
            NoveltyFirst = noveltyFirst;
            NoveltyDecay = noveltyDecay;
            NoveltyCooldownTicks = noveltyCooldownTicks;
            DeathLoss = deathLoss;
            MedCombatMod = medCombatMod;
            WeaponMultipliers = weaponMultipliers;
            DiagTiers = diagTiers;
            InsightTiers = insightTiers;
            InsightBonus = insightBonus;
        }

        // ── 默认实例 ──────────────────────────────────────────────────────────

        /// <summary>默认调参表(用户数值轮最终值替换此处)。</summary>
        public static readonly SkillTuningTable Default = new SkillTuningTable(
            c: new Fix(40L * Fix.OneRaw / 1),        // 40
            p: new Fix(3L * Fix.OneRaw / 2),         // 1.5 (例值,G-1 允许)
            noveltyFirst: new Fix(3L * Fix.OneRaw / 1),   // 3.0
            noveltyDecay: new Fix(2L * Fix.OneRaw / 10),  // 0.2
            noveltyCooldownTicks: 24000,          // 20 min @ 20 Hz
            deathLoss: new Fix(19L * Fix.OneRaw / 20),    // 0.95
            medCombatMod: new Fix(20L * Fix.OneRaw / 100), // 0.20
            weaponMultipliers: new Fix[]
            {
                new Fix(9L * Fix.OneRaw / 10),  // 徒手 = 0.9
                new Fix(1L * Fix.OneRaw / 1),   // 短兵 = 1.0
                new Fix(1L * Fix.OneRaw / 1),   // 钝器 = 1.0
                new Fix(11L * Fix.OneRaw / 10), // 长兵 = 1.1
                new Fix(8L * Fix.OneRaw / 10),  // 暗器 = 0.8
            },
            diagTiers: new[] { 10, 20, 35, 50 },
            insightTiers: new[] { 10, 20, 35, 50 },
            insightBonus: new Fix[]
            {
                new Fix(5L * Fix.OneRaw / 100),  // +5%
                new Fix(10L * Fix.OneRaw / 100), // +10%
                new Fix(20L * Fix.OneRaw / 100), // +20%
                new Fix(30L * Fix.OneRaw / 100), // +30%
            }
        );

        // ── 实例属性(只读公开) ────────────────────────────────────────────────

        /// <summary>升级曲线系数 C。</summary>
        public Fix CurveC => C;
        /// <summary>升级曲线指数 P。</summary>
        public Fix CurveP => P;
        /// <summary>首次遇见倍率。</summary>
        public Fix NoveltyFirstCoeff => NoveltyFirst;
        /// <summary>冷却期内倍率。</summary>
        public Fix NoveltyDecayCoeff => NoveltyDecay;
        /// <summary>新颖度冷却期 tick 数。</summary>
        public int NoveltyCooldown => NoveltyCooldownTicks;
        /// <summary>死亡保留比例。</summary>
        public Fix DeathRetention => DeathLoss;
        /// <summary>医术修正上限。</summary>
        public Fix MedicalCombatModifier => MedCombatMod;
        /// <summary>武器线系数数组。</summary>
        public Fix[] GetWeaponMultipliers() => (Fix[])WeaponMultipliers.Clone();
        /// <summary>诊断精度档。</summary>
        public int[] GetDiagTiers() => (int[])DiagTiers.Clone();
        /// <summary>洞察解锁等级。</summary>
        public int[] GetInsightTiers() => (int[])InsightTiers.Clone();
        /// <summary>洞察加成。</summary>
        public Fix[] GetInsightBonus() => (Fix[])InsightBonus.Clone();

        // ── 便利查询 ──────────────────────────────────────────────────────────

        /// <summary>获取指定武器线的系数。</summary>
        public Fix GetWeaponMultiplier(WeaponLine line)
            => WeaponMultipliers[(int)line];

        /// <summary>诊断精度档数量(不含 P1a 检验线 35)。</summary>
        public int DiagSlotCount => DiagTiers.Length - 1; // 3 槽(1-10 / 10-20 / 20-35 / 35-50 → 3 槽不含 35)
    }
}
