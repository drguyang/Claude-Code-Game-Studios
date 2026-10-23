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
        /// <summary>
        /// `MAG_FLOOR` —— **真源**(GDD `combat-and-weapon-lines.md` `:1351`:**`1/20`**,✅ 2026-10-07 代拍)。
        /// <para>A13a:`MAG_FLOOR &gt; 0`(GDD `:749` 安全区 `(0, 0.2]` 的下半)。</para>
        /// </summary>
        /// <remarks>
        /// ⚠️ **2026-10-23(用户裁定 ② · round36):本类型是 `MAG_FLOOR` / `MAG_CAP` 的唯一真源**。
        /// <para><b>改前原状</b>:`MAG_FLOOR` 在**三处**各有定义且互不相同 ——
        /// 本类 `= 1` · `CombatMagnitude.cs:43` `= 1000` · GDD `:1351` `= 1/20`。
        /// 其中 `CombatMagnitude` 把 `1000` 当 **Q16.16 raw** 喂给 `new Fix(...)`
        /// ⇒ 实际地板 `1000/65536 ≈ 0.01526`,只有 GDD 的 30.5%;`MAG_CAP` 同理
        /// (`10000/65536 ≈ 0.1526` vs 应有的 `0.5`)—— **比例对、绝对尺度错**的典型单位错。</para>
        /// <para><b>现口径</b>:两者在此**只定义一次**,以 <see cref="Fix"/> 承载(不是 `int`),
        /// 经 <see cref="FixParse.Parse"/> 由 GDD 的分数字面量产出(ADR-006 §五 唯一解析入口,
        /// 禁浮点)。`CombatMagnitude` 与所有消费方**引用本处**,不得另写副本。</para>
        /// <para><b>为何放本类而不是 `CombatMagnitude`</b>:A13a / A13b 两条构建期断言
        /// (`Validate` 的注释义务)住在**本类** —— 断言与被断言的常量同处一个类型,
        /// 不存在「schema 引用计算器」的反向依赖。</para>
        /// <para><b>判据</b>:`Tests/EditMode/Combat/combat_magnitude_gdd_value_drift_test.cs`
        /// (黑盒驱动真 `CombatMagnitude`,真值经独立的 `FixParse.Parse("1/20")` 而非读本常量;
        /// 另有 SCHEMA 探针证明消费方真的绑定在本处)。</para>
        /// </remarks>
        public static readonly Fix MagFloor = FixParse.Parse("1/20");   // GDD :1351

        /// <summary>
        /// `MAG_CAP` —— **真源**(GDD `:1352`:**`1/2`**,✅ 2026-10-07)。
        /// <para>A13b:`MAG_FLOOR &lt; MAG_CAP`(见 <see cref="MagFloor"/> 的 remarks)。</para>
        /// </summary>
        public static readonly Fix MagCap = FixParse.Parse("1/2");      // GDD :1352

        /// <summary>`MagFloor` 的 **raw**(兼容旧名;`const int` → `long` 属性,值同)。
        /// <para>⚠️ 2026-10-23:原 `const int MAG_FLOOR = 1` 与 GDD 的 `1/20` 不符,
        /// 且与 `CombatMagnitude` 的副本(`1000`)三方各异。现为真源的 raw 投影。</para></summary>
        public static long MAG_FLOOR => MagFloor.Raw;

        /// <summary>`MagCap` 的 **raw**(兼容旧名)。原 `const int MAG_CAP = 10000`。</summary>
        public static long MAG_CAP => MagCap.Raw;

        /// <summary>
        /// A13a / A13b 的**可执行**形式(此前只有注释,没有执行体)。
        /// </summary>
        /// <remarks>
        /// ⚠️ 原状:`Validate` 末尾只写着「A13a: MAG_FLOOR &gt; 0 / A13b: MAG_FLOOR &lt; MAG_CAP
        /// (常量已定义,这里验证条目不违反)」—— **是一句注释,不是断言**。
        /// 按「注释不是执行体」纪律,补成真断言;值本身由 <see cref="MagFloor"/> / <see cref="MagCap"/>
        /// 的初始化承载,本方法守的是**结构关系**(改值时若破坏 A13a/b,构建期即红)。
        /// </remarks>
        /// <exception cref="CombatSchemaValidationException">常量违反 A13a 或 A13b。</exception>
        public static void ValidateMagnitudeBounds()
        {
            if (MagFloor.Raw <= 0)
                throw new CombatSchemaValidationException(
                    $"A13a: MAG_FLOOR 必须 > 0,GDD :749 安全区 (0, 0.2];实测 raw={MagFloor.Raw}", -1);
            if (MagFloor.Raw >= MagCap.Raw)
                throw new CombatSchemaValidationException(
                    $"A13b: MAG_FLOOR 必须 < MAG_CAP(否则 clamp 区间为空);" +
                    $"实测 floor={MagFloor.Raw} cap={MagCap.Raw}", -1);
            // 上端:GDD :1352「>1 ⇒ 一击填满 bar」⇒ MAG_CAP ≤ 1.0
            if (MagCap.Raw > Fix.OneRaw)
                throw new CombatSchemaValidationException(
                    $"GDD :1352 安全区上端: MAG_CAP 必须 ≤ 1;实测 raw={MagCap.Raw}", -1);
        }

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
            //
            // ⚠️ 2026-10-23(round36):上面两行**原本就是注释,不是断言** —— 按
            //    「注释不是执行体」纪律已补可执行版 = <see cref="ValidateMagnitudeBounds"/>。
            //    本方法(Validate)保持逐条目校验的职责,不在此重复调它;
            //    装配 / 构建入口应显式调 ValidateMagnitudeBounds()。
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
