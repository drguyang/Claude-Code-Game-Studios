// ============================================================================
// 技能系统注册表 —— 19 项技能定义、枚举与查询接口
// 权威来源: Story 001 · ADR-026 §Decision 五(门槛/解锁 = 整数等级比较)
// ============================================================================
// 本文件纯数据 + 静态查询,零 Unity 依赖,住 Sim.Contracts(BCL only)。
// 所有非整数字段用 Fix; Fix 字段在 JSON 源码写字符串 → FixParse(ADR-014),
// 但本文件不实现 JSON 加载(归 21a 数据管线 + Story 001 Out of Scope)。
// ============================================================================

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Contracts.SkillSystem
{
    // ── 技能类别 ────────────────────────────────────────────────────────────

    /// <summary>技能大类(6 类)。</summary>
    public enum SkillCategory : int
    {
        医术 = 0,
        生产 = 1,
        社会 = 2,
        生存 = 3,
        格斗 = 4,
    }

    // ── 新颖度类别 ─────────────────────────────────────────────────────────

    /// <summary>新颖度三档(承 ADR-026 · GDD §3.2)。</summary>
    public enum NoveltyClass : int
    {
        /// <summary>首次遇见该对象 → ×3.0。</summary>
        First = 0,
        /// <summary>冷却期内再次遇见 → ×0.2。</summary>
        Stale = 1,
        /// <summary>冷却期外(已见过但冷却已过) → ×1.0。</summary>
        Normal = 2,
    }

    // ── 武器线 ─────────────────────────────────────────────────────────────

    /// <summary>格斗武器线(5 条)。P0 = 徒手 + 短兵。</summary>
    public enum WeaponLine : int
    {
        徒手 = 0,
        短兵 = 1,
        钝器 = 2,
        长兵 = 3,
        暗器 = 4,
    }

    // ── 技能依赖 ───────────────────────────────────────────────────────────

    /// <summary>单个解锁依赖:前置技能 + 最低等级。</summary>
    public readonly struct SkillDependency
    {
        /// <summary>前置技能 id(SkillId ordinal)。</summary>
        public readonly int PrerequisiteSkillId;
        /// <summary>最低等级(≥ 此值才解锁)。</summary>
        public readonly int MinLevel;

        public SkillDependency(int prerequisiteSkillId, int minLevel)
        {
            PrerequisiteSkillId = prerequisiteSkillId;
            MinLevel = minLevel;
        }
    }

    // ── 技能定义 ───────────────────────────────────────────────────────────

    /// <summary>单条技能的完整定义(只读)。</summary>
    public readonly struct SkillDefinition
    {
        /// <summary>技能 id(SkillId ordinal, 0–18)。</summary>
        public readonly int SkillId;
        /// <summary>技能名称(中文,用于 UI 显示)。</summary>
        public readonly string Name;
        /// <summary>技能类别。</summary>
        public readonly SkillCategory Category;
        /// <summary>成长触发描述(人类可读,用于工具提示)。</summary>
        public readonly string GrowthTrigger;
        /// <summary>单次动作基础经验值(Fix;BASE 字段)。</summary>
        public readonly Fix BaseXp;
        /// <summary>解锁依赖列表(空 = 无依赖,出生可用)。</summary>
        public readonly SkillDependency[] Dependencies;
        /// <summary>P0 上线标志。</summary>
        public readonly bool IsP0;
        /// <summary>关联医术技能 id(仅格斗线有意义,其余 = -1)。</summary>
        public readonly int LinkedMedicalSkillId;

        public SkillDefinition(int skillId, string name, SkillCategory category,
            string growthTrigger, Fix baseXp, SkillDependency[] dependencies,
            bool isP0, int linkedMedicalSkillId = -1)
        {
            SkillId = skillId;
            Name = name;
            Category = category;
            GrowthTrigger = growthTrigger;
            BaseXp = baseXp;
            Dependencies = dependencies ?? System.Array.Empty<SkillDependency>();
            IsP0 = isP0;
            LinkedMedicalSkillId = linkedMedicalSkillId;
        }
    }

    // ── 技能 Id 枚举 ────────────────────────────────────────────────────────

    /// <summary>19 项技能 id(ordinal 0–18)。</summary>
    /// <remarks>
    /// 枚举值 = ordinal,与 SkillGrownPayload.SkillId 对齐(承 ADR-024 registry)。
    /// P0 七项: 0..6; P1a 十二项: 7..18。
    /// </remarks>
    public enum SkillId : int
    {
        // ── 医术(6) ──────────────────────────────────────────────────────────
        诊断 = 0,           // P0
        辨证 = 1,           // P1a
        急救 = 2,           // P0
        手术 = 3,           // P1a
        处方用药 = 4,       // P0
        针灸 = 5,           // P0

        // ── 生产(3) ──────────────────────────────────────────────────────────
        采集 = 6,           // P0
        炮制 = 7,           // P0
        装备 = 8,           // P0

        // ── 社会(3) ──────────────────────────────────────────────────────────
        沟通 = 9,           // P0
        公卫 = 10,          // P0
        商业 = 11,          // P0

        // ── 生存(2) ──────────────────────────────────────────────────────────
        奔跑 = 12,          // P0
        潜行 = 13,          // P0

        // ── 格斗(5) ──────────────────────────────────────────────────────────
        徒手 = 14,          // P0
        短兵 = 15,          // P0
        钝器 = 16,          // P1a
        长兵 = 17,          // P1a
        暗器 = 18,          // P1a
    }

    // ── 注册表 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 技能注册表 —— 19 项技能定义 + 查询接口。
    /// <para>纯静态数据,零 Unity 依赖,住 Sim.Contracts(BCL only)。</para>
    /// </summary>
    /// <remarks>
    /// 构造方式:静态只读数组 + 索引器。19 项硬编码 = GDD 固定内容,
    /// 数值调参归 SkillTuningTable(本文件只定「结构」)。
    /// </remarks>
    public static class SkillRegistry
    {
        /// <summary>技能总数(硬编码 19)。</summary>
        public const int SkillCount = 19;

        /// <summary>等级硬上限(承 GDD §3.2 · ADR-026)。</summary>
        public const int SKILL_CAP = 60;

        /// <summary>P0 上线技能数(7 项)。</summary>
        public const int P0SkillCount = 7;

        private static readonly SkillDefinition[] _definitions;

        /// <summary>按 SkillId 查询技能定义。</summary>
        public static SkillDefinition GetDefinition(int skillId)
        {
            if ((uint)skillId >= (uint)_definitions.Length)
                throw new ArgumentOutOfRangeException(nameof(skillId),
                    $"SkillId {skillId} 超出范围 [0, {_definitions.Length - 1}]");
            return _definitions[skillId];
        }

        /// <summary>枚举全部技能定义(只读副本)。</summary>
        public static SkillDefinition[] GetAllDefinitions()
        {
            // 返回副本防外部修改
            var copy = new SkillDefinition[_definitions.Length];
            System.Array.Copy(_definitions, copy, _definitions.Length);
            return copy;
        }

        /// <summary>枚举全部 P0 技能。</summary>
        public static IEnumerable<SkillDefinition> GetP0Skills()
        {
            foreach (var def in _definitions)
                if (def.IsP0)
                    yield return def;
        }

        /// <summary>枚举全部 P1a 技能。</summary>
        public static IEnumerable<SkillDefinition> GetP1aSkills()
        {
            foreach (var def in _definitions)
                if (!def.IsP0)
                    yield return def;
        }

        /// <summary>按类别查询技能。</summary>
        public static IEnumerable<SkillDefinition> GetByCategory(SkillCategory category)
        {
            foreach (var def in _definitions)
                if (def.Category == category)
                    yield return def;
        }

        /// <summary>检查 skillId 是否在有效范围内。</summary>
        public static bool IsValidSkillId(int skillId)
            => (uint)skillId < (uint)_definitions.Length;

        /// <summary>检查 skillId 是否为 P0 上线技能。</summary>
        public static bool IsP0(int skillId)
        {
            if ((uint)skillId >= (uint)_definitions.Length) return false;
            return _definitions[skillId].IsP0;
        }

        /// <summary>检查 skillId 是否为格斗线技能。</summary>
        public static bool IsCombatSkill(int skillId)
        {
            if ((uint)skillId >= (uint)_definitions.Length) return false;
            var cat = _definitions[skillId].Category;
            return cat == SkillCategory.格斗;
        }

        /// <summary>获取格斗线的关联医术技能 id(仅格斗线有意义,其余 = -1)。</summary>
        public static int GetLinkedMedicalSkill(WeaponLine weaponLine)
        {
            return weaponLine switch
            {
                WeaponLine.徒手 => (int)SkillId.急救,
                WeaponLine.短兵 => (int)SkillId.手术,
                _ => -1,
            };
        }

        // ── 静态构造 ──────────────────────────────────────────────────────────

        static SkillRegistry()
        {
            // BASE 值(§4.1 表格):Fix 常量,JSON 源码写字符串 → FixParse。
            // 本文件用 new Fix(long raw) 直接构造,等价于 FixParse 输出。
            // OneRaw = 65536 (Q16.16)
            _definitions = new[]
            {
                // ── 医术(6) ──────────────────────────────────────────────────────
                new SkillDefinition((int)SkillId.诊断,   "诊断",   SkillCategory.医术, "完成一次查体/问诊并作出诊断(不论对错)",  new Fix(8L * Fix.OneRaw / 1),       Array.Empty<SkillDependency>(), true),
                new SkillDefinition((int)SkillId.辨证,   "辨证",   SkillCategory.医术, "完成一次望闻问切并作出整体判断(不论对错)",new Fix(8L * Fix.OneRaw / 1),       new[] { new SkillDependency((int)SkillId.诊断, 10) }, false),
                new SkillDefinition((int)SkillId.急救,   "急救",   SkillCategory.医术, "执行止血/包扎/正骨/复苏",               new Fix(6L * Fix.OneRaw / 1),       Array.Empty<SkillDependency>(), true),
                new SkillDefinition((int)SkillId.手术,   "手术",   SkillCategory.医术, "执行手术",                              new Fix(10L * Fix.OneRaw / 1),      Array.Empty<SkillDependency>(), false),
                new SkillDefinition((int)SkillId.处方用药,"处方用药",SkillCategory.医术, "开出一张处方并被执行",                   new Fix(6L * Fix.OneRaw / 1),       Array.Empty<SkillDependency>(), true),
                new SkillDefinition((int)SkillId.针灸,   "针灸",   SkillCategory.医术, "施针",                                  new Fix(6L * Fix.OneRaw / 1),       Array.Empty<SkillDependency>(), false),

                // ── 生产(3) ──────────────────────────────────────────────────────
                new SkillDefinition((int)SkillId.采集,   "采集",   SkillCategory.生产, "采到一味药材",                          new Fix(1L * Fix.OneRaw / 1),       Array.Empty<SkillDependency>(), true),
                new SkillDefinition((int)SkillId.炮制,   "炮制",   SkillCategory.生产, "完成一次炮制",                           new Fix(3L * Fix.OneRaw / 1),       Array.Empty<SkillDependency>(), true),
                new SkillDefinition((int)SkillId.装备,   "装备",   SkillCategory.生产, "制作器械/设备",                          new Fix(5L * Fix.OneRaw / 1),       Array.Empty<SkillDependency>(), false),

                // ── 社会(3) ──────────────────────────────────────────────────────
                new SkillDefinition((int)SkillId.沟通,   "沟通",   SkillCategory.社会, "完成一次交涉/问诊对话",                  new Fix(2L * Fix.OneRaw / 1),       Array.Empty<SkillDependency>(), false),
                new SkillDefinition((int)SkillId.公卫,   "公卫",   SkillCategory.社会, "处理一次防疫/检疫/群体救治",              new Fix(8L * Fix.OneRaw / 1),       Array.Empty<SkillDependency>(), false),
                new SkillDefinition((int)SkillId.商业,   "商业",   SkillCategory.社会, "完成一次买卖/定价",                       new Fix(2L * Fix.OneRaw / 1),       Array.Empty<SkillDependency>(), false),

                // ── 生存(2) ──────────────────────────────────────────────────────
                new SkillDefinition((int)SkillId.奔跑,   "奔跑",   SkillCategory.生存, "奔跑",                                  new Fix(1L * Fix.OneRaw / 5),       Array.Empty<SkillDependency>(), false), // 0.2
                new SkillDefinition((int)SkillId.潜行,   "潜行",   SkillCategory.生存, "潜行",                                  new Fix(5L * Fix.OneRaw / 10),      Array.Empty<SkillDependency>(), false), // 0.5

                // ── 格斗(5) ──────────────────────────────────────────────────────
                new SkillDefinition((int)SkillId.徒手,   "徒手",   SkillCategory.格斗, "徒手命中/制服",                          new Fix(2L * Fix.OneRaw / 1),       Array.Empty<SkillDependency>(), true),
                new SkillDefinition((int)SkillId.短兵,   "短兵",   SkillCategory.格斗, "短兵器命中",                             new Fix(4L * Fix.OneRaw / 1),       Array.Empty<SkillDependency>(), true),
                new SkillDefinition((int)SkillId.钝器,   "钝器",   SkillCategory.格斗, "钝器命中",                              new Fix(4L * Fix.OneRaw / 1),       Array.Empty<SkillDependency>(), false),
                new SkillDefinition((int)SkillId.长兵,   "长兵",   SkillCategory.格斗, "长兵器命中",                             new Fix(4L * Fix.OneRaw / 1),       Array.Empty<SkillDependency>(), false),
                new SkillDefinition((int)SkillId.暗器,   "暗器",   SkillCategory.格斗, "投掷命中",                              new Fix(5L * Fix.OneRaw / 1),       Array.Empty<SkillDependency>(), false),
            };
        }
    }
}
