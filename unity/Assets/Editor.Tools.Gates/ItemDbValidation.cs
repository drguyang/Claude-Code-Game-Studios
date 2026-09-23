// 权威来源:Story 002(production/epics/item-database/story-002-schema-types-and-primary-key.md)
//   · AC-21a-21 —— 同 (base_id, processing_state) 复合主键重复 ⇒ 拒
//   · AC-21a-22 —— category / processing_state 取枚举外字面量 ⇒ 拒(枚举闭合)
//   · AC-21a-59 —— stackable 被显式写入数据文件 ⇒ 拒(派生量不存储,§Schema A)
//   · AC-21a-48 —— 本文件零调参旋钮字面量(扫描门在测试侧;校验器所需取值一律入参,禁 const)
//   GDD:design/gdd/item-database.md §Schema A–F · §Edge Cases 写入期校验族(仅本故事三条)
//   ADR-014 §三(逐 schema 已知键白名单 / 硬失败归烘焙管线 Story 008 —— 本文件只提供可复用纯函数)
//
// ⚠️ 落点(unity-specialist 约束①,2026-09-23 中途修正):校验/扫描纯函数住 Editor.Tools.Gates
//    **不**住 Sim.Contracts —— 编辑期职责(AC-27 反射 / 008 烘焙期校验)不进玩家构建;
//    schema **类型**仍住 Sim.Contracts(契约层)。类型与校验分家 = 本故事的刻意结构。
// ⚠️ 执法体统一形态 = **错误列表(空 = 通过)**;「硬失败 throw」由调用方(008 烘焙管线 /
//    Story 006 校验套件)聚合非空列表后执行 —— 本文件是纯函数,无 I/O、无静态可变态。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>21a 物品/配方 schema 的构建期校验纯函数(Story 002 三条 AC 的执行体)。
    /// <para>全部方法:纯函数 · 无 I/O · 无静态可变态 · 返回错误列表(空 = 通过)——
    /// 供 Story 008 两阶段烘焙管线复用(聚合非空列表 ⇒ 构建失败,ADR-014 §三「全量校验失败 = 构建失败」)。</para>
    /// <para>本文件**不含**任何调参旋钮字面量(AC-21a-48);需要旋钮值的校验(守恒/区间族)
    /// 归 Story 006/007,以入参传入。</para>
    /// <example>
    /// var errors = ItemDbValidation.FindDuplicateCompositeKeys(records);
    /// if (errors.Count &gt; 0) throw new InvalidOperationException(string.Join("\n", errors));
    /// </example></summary>
    public static class ItemDbValidation
    {
        // ══════════ AC-21a-21:复合主键唯一性 ══════════

        /// <summary>扫复合主键序列,找出重复出现(≥2 次)的 <c>(base_id, processing_state)</c>。
        /// 每个重复键产出一条错误(恰两重 / 三重 / 任意重数同一判据)。
        /// 同 base_id 不同 state、不同 base_id 同 state 均**不**算重复(§Edge Cases「完全正常」)。</summary>
        /// <param name="keys">按表序给出的复合主键序列。</param>
        /// <returns>错误列表;空 = 唯一性通过。</returns>
        /// <example><c>FindDuplicateCompositeKeys(new[] { k1, k1 })</c> ⇒ 1 条错误;
        /// <c>FindDuplicateCompositeKeys(new[] { k1, k2 })</c>(k1.k2 同 base 异 state)⇒ 空。</example>
        public static IReadOnlyList<string> FindDuplicateCompositeKeys(IEnumerable<ItemKey> keys)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));

            var seen = new HashSet<ItemKey>();
            var duplicates = new HashSet<ItemKey>();
            var errors = new List<string>();

            foreach (ItemKey key in keys)
            {
                if (!seen.Add(key))
                {
                    if (duplicates.Add(key))
                        errors.Add(
                            $"复合主键重复:{key} —— (base_id, processing_state) 必须唯一(AC-21a-21)");
                }
            }

            return errors;
        }

        /// <summary>物品表记录列表版复合主键唯一性校验(投影到 <see cref="ItemKey"/> 后同
        /// <see cref="FindDuplicateCompositeKeys(IEnumerable{ItemKey})"/> 判据)。</summary>
        /// <param name="defs">物品表记录(解析后形)。</param>
        /// <returns>错误列表;空 = 通过。</returns>
        public static IReadOnlyList<string> FindDuplicateCompositeKeys(IEnumerable<ItemDef> defs)
        {
            if (defs == null) throw new ArgumentNullException(nameof(defs));

            var keys = new List<ItemKey>();
            foreach (ItemDef def in defs)
                keys.Add(new ItemKey(def.BaseId, def.ProcessingState));

            return FindDuplicateCompositeKeys(keys);
        }

        // ══════════ AC-21a-22:枚举闭合(逐字面量白名单解析)════════════

        /// <summary>严格解析 <c>processing_state</c> 字面量(P0 五值闭集,序数精确匹配)。
        /// <b>拒:</b>null · 空串 · 拼写近似(<c>"Raw"</c> 大小写不符)· int 字符串(<c>"3"</c>)·
        /// P1a 字面量(<c>"honey_fried"</c> / <c>"dry_fried"</c> —— 枚举无此二成员,天然拒)· 枚举外任意值。
        /// <para>刻意<b>不用</b> <c>Enum.TryParse</c>(它接受数字字符串与未定义数值,正中 AC-21a-22/26 拒收面)。</para></summary>
        /// <param name="literal">作者态 JSON 里的字符串原文。</param>
        /// <param name="value">解析结果(失败时为 <c>default</c>)。</param>
        /// <returns>true = 在闭集内。</returns>
        /// <example><c>TryParseProcessingState("raw", out var s)</c> ⇒ true;
        /// <c>TryParseProcessingState("Raw", out _)</c> ⇒ false(AC-21a-22 边缘)。</example>
        public static bool TryParseProcessingState(string literal, out ProcessingState value)
        {
            switch (literal)
            {
                case "raw": value = ProcessingState.Raw; return true;
                case "dried": value = ProcessingState.Dried; return true;
                case "extracted": value = ProcessingState.Extracted; return true;
                case "tincture": value = ProcessingState.Tincture; return true;
                case "pill": value = ProcessingState.Pill; return true;
                default: value = default(ProcessingState); return false;
            }
        }

        /// <summary>严格解析 <c>category</c> 字面量(六值闭集,序数精确匹配)。
        /// 拒:null · 空串 · 拼写近似 · int 字符串 · 枚举外任意值(判据同
        /// <see cref="TryParseProcessingState"/>)。</summary>
        /// <param name="literal">作者态 JSON 里的字符串原文。</param>
        /// <param name="value">解析结果(失败时为 <c>default</c>)。</param>
        /// <returns>true = 在闭集内。</returns>
        /// <example><c>TryParseItemCategory("build_part", out var c)</c> ⇒ true;
        /// <c>TryParseItemCategory("medicine", out _)</c> ⇒ false。</example>
        public static bool TryParseItemCategory(string literal, out ItemCategory value)
        {
            switch (literal)
            {
                case "material": value = ItemCategory.Material; return true;
                case "drug": value = ItemCategory.Drug; return true;
                case "tool": value = ItemCategory.Tool; return true;
                case "weapon": value = ItemCategory.Weapon; return true;
                case "build_part": value = ItemCategory.BuildPart; return true;
                case "food": value = ItemCategory.Food; return true;
                default: value = default(ItemCategory); return false;
            }
        }

        /// <summary>严格解析配方 <c>owner</c> 字面量(三值闭集;D-21-30)。
        /// 本方法供 Story 007 的 AC-21a-66 装载校验复用(本故事只落解析、不落三分子集断言)。</summary>
        /// <param name="literal">作者态 JSON 里的字符串原文。</param>
        /// <param name="value">解析结果(失败时为 <c>default</c>)。</param>
        /// <returns>true = 在闭集内。</returns>
        /// <example><c>TryParseRecipeOwner("process", out var o)</c> ⇒ true;
        /// <c>TryParseRecipeOwner("Process", out _)</c> ⇒ false(序数精确)。</example>
        public static bool TryParseRecipeOwner(string literal, out RecipeOwner value)
        {
            switch (literal)
            {
                case "process": value = RecipeOwner.Process; return true;
                case "craft": value = RecipeOwner.Craft; return true;
                case "build": value = RecipeOwner.Build; return true;
                default: value = default(RecipeOwner); return false;
            }
        }

        /// <summary>AC-21a-22 枚举闭合:校验一条记录的 <c>category</c> 与 <c>processing_state</c>
        /// 两个字面量均在 P0 闭集内(字段必填 —— null 亦拒,§Schema A 可空=否)。
        /// JSON 中必须是明文字符串非 int(int 编码的数据产物扫描 = AC-21a-26,归 Story 006 ——
        /// 本方法拒 int **字符串**形,那已是字面量出闭集)。</summary>
        /// <param name="categoryLiteral">category 字段原文(null = 缺失/JSON null)。</param>
        /// <param name="processingStateLiteral">processing_state 字段原文(null 同左)。</param>
        /// <returns>错误列表;空 = 两字段均闭合。</returns>
        /// <example><c>ValidateEnumClosure("drug", "raw")</c> ⇒ 空;
        /// <c>ValidateEnumClosure("medicine", "Raw")</c> ⇒ 2 条错误。</example>
        public static IReadOnlyList<string> ValidateEnumClosure(
            string categoryLiteral, string processingStateLiteral)
        {
            var errors = new List<string>();

            if (!TryParseItemCategory(categoryLiteral, out _))
                errors.Add(
                    $"category 枚举闭合失败:字面量 \"{categoryLiteral}\" ∉ P0 闭集" +
                    " material|drug|tool|weapon|build_part|food(AC-21a-22)");

            if (!TryParseProcessingState(processingStateLiteral, out _))
                errors.Add(
                    $"processing_state 枚举闭合失败:字面量 \"{processingStateLiteral}\" ∉ P0 闭集" +
                    " raw|dried|extracted|tincture|pill(AC-21a-22)");

            return errors;
        }

        // ══════════ AC-21a-59:stackable 派生量不存储 ═════════════

        /// <summary>校验一条物品记录的**原始 JSON 键集**:出现 <c>stackable</c> 键(无论值为
        /// true / false / null)即错 —— 它是 <c>stack_max &gt; 1</c> 的派生量,不存储(§Schema A)。
        /// <para><b>两级校验缝隙(登记)</b>:解析后 <see cref="ItemDef"/> 不携带未知键,
        /// 单靠解析后记录结构上抓不到本错 —— 故本方法接<b>原始键集</b>而非记录;
        /// Story 008 绑定层须把每条记录的原始键集一并交给校验(同一缝隙服务未知键白名单,
        /// ADR-014 §三)。值为 null 仍拒:<b>键存在 = 显式写入</b>。</para></summary>
        /// <param name="rawKeys">该记录在原始 JSON 文本中的键名集合(不含嵌套子对象键亦可 ——
        /// 只要顶层含 <c>stackable</c> 即会被抓)。</param>
        /// <param name="recordLabel">诊断标签(如 base_id 或行号;可省)。</param>
        /// <returns>错误列表;空 = 无显式 stackable 键。</returns>
        /// <example><c>FindStoredStackableKeys(new[] { "base_id", "stack_max", "stackable" })</c>
        /// ⇒ 1 条错误;<c>FindStoredStackableKeys(new[] { "base_id", "stack_max" })</c> ⇒ 空。</example>
        public static IReadOnlyList<string> FindStoredStackableKeys(
            IEnumerable<string> rawKeys, string recordLabel = "")
        {
            if (rawKeys == null) throw new ArgumentNullException(nameof(rawKeys));

            var errors = new List<string>();
            // F4 修(2026-09-23 双评审 S2):标签只拼一次 —— 旧实现 where 内已含「(记录 x)」,
            // 消息再前缀「记录 {where}」成双重「记录」。
            string where = string.IsNullOrEmpty(recordLabel) ? "记录" : $"记录({recordLabel})";

            foreach (string key in rawKeys)
            {
                if (string.Equals(key, "stackable", StringComparison.Ordinal))
                {
                    errors.Add(
                        $"{where} 显式存储 stackable —— 派生量不存储" +
                        "(= ItemDef.StackMax > 1,读 ItemDef.Stackable;AC-21a-59)");
                }
            }

            return errors;
        }
    }
}
