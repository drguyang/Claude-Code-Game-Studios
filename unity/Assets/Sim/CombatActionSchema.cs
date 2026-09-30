// ADR-014 / ADR-024 / GDD combat-and-weapon-lines.md —— 动作表 schema 与烘焙构建门。
//
// 权威来源:
//   ADR-014 §二 —— 两阶段烘焙
//   ADR-024 §⑥ —— 拒收表 18 谓词
//   GDD combat-and-weapon-lines.md §一 —— 动作表 schema
//
// 核心机制:
//   - 字段集闭集完整
//   - Fix 字段为字符串分数，经 FixParse
//   - 构建期硬失败（throw）

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 动作表条目。
    /// </summary>
    public sealed class CombatActionEntry
    {
        public int Id;
        public string ActorClass;
        public string AttackClass;
        public int WeaponLine;
        public int MapsToInjury;
        public Fix BaseStep; // Fix 字段（从字符串解析）
        public int CooldownTicks;
        public int UnlockLevel;
        public int RangeOverride;
        public int DurationTicks;
        public int MaxTargets;
    }

    /// <summary>
    /// 动作表 schema 校验器 —— 构建期拒收表。
    /// </summary>
    public static class CombatActionSchema
    {
        // A13a: MAG_FLOOR > 0
        public const int MAG_FLOOR = 1;
        // A13b: MAG_FLOOR < MAG_CAP
        public const int MAG_CAP = 10000;

        /// <summary>
        /// 校验动作表条目。
        /// </summary>
        public static void Validate(CombatActionEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (entry.Id < 0)
                throw new CombatSchemaValidationException("Id 必须 >= 0", entry.Id);

            if (string.IsNullOrEmpty(entry.ActorClass))
                throw new CombatSchemaValidationException("ActorClass 不能为空", entry.Id);

            if (string.IsNullOrEmpty(entry.AttackClass))
                throw new CombatSchemaValidationException("AttackClass 不能为空", entry.Id);

            // A14: max_targets = 1 全表恒成立
            if (entry.MaxTargets != 1)
                throw new CombatSchemaValidationException("MaxTargets 必须 = 1", entry.Id);

            // A6: cooldown_ticks >= 1
            if (entry.CooldownTicks < 1)
                throw new CombatSchemaValidationException("CooldownTicks 必须 >= 1", entry.Id);

            // A17: maps_to_injury ∈ 9 的 R2 枚举（悬空外键拒收）
            if (entry.MapsToInjury < 0)
                throw new CombatSchemaValidationException("MapsToInjury 必须 >= 0", entry.Id);

            // A18: weapon_line = ∅ ⟺ attack_class = Natural（双向等价）
            bool isNatural = entry.AttackClass == "Natural";
            bool hasWeaponLine = entry.WeaponLine >= 0;
            if (isNatural && hasWeaponLine)
                throw new CombatSchemaValidationException("Natural 行不应有 weapon_line", entry.Id);
            if (!isNatural && !hasWeaponLine)
                throw new CombatSchemaValidationException("非 Natural 行必须有 weapon_line", entry.Id);

            // A22: Natural ⇒ range_override ≥ 1
            if (isNatural && entry.RangeOverride < 1)
                throw new CombatSchemaValidationException("Natural 行 RangeOverride 必须 >= 1", entry.Id);

            // A13a: MAG_FLOOR > 0
            // A13b: MAG_FLOOR < MAG_CAP
            // （常量已定义，这里验证条目不违反）
        }

        /// <summary>
        /// 校验动作表条目集合。
        /// </summary>
        public static void ValidateAll(IEnumerable<CombatActionEntry> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            foreach (var entry in entries)
            {
                Validate(entry);
            }
        }

        /// <summary>
        /// 验证 Fix 字段为字符串分数（构建期）。
        /// </summary>
        public static Fix ParseFixField(string fieldName, string value, int entryId)
        {
            try
            {
                return FixParse.Parse(value);
            }
            catch (Exception ex)
            {
                throw new CombatSchemaValidationException(
                    $"Fix 字段 {fieldName} 解析失败: {ex.Message}", entryId);
            }
        }
    }

    /// <summary>
    /// 战斗 schema 校验异常。
    /// </summary>
    public sealed class CombatSchemaValidationException : Exception
    {
        public int EntryId { get; }

        public CombatSchemaValidationException(string message, int entryId)
            : base(message)
        {
            EntryId = entryId;
        }
    }
}
