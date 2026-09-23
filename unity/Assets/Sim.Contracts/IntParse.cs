// 权威来源:ADR-006 §Decision 一(D-21-17 —— weight / stack_max 是 int 计数,移出 Fix 解析集)
//           ADR-014 §四(int 计数字段写 JSON 数字,不写字符串 Fix 语法)
//           Story 001 AC-21a-41 边缘:weight 写成 "3/4" 或 "1.5" 应拒 —— int 字段不接受 Fix 语法
//
// ⚠️ 本类**不是** FixParse 的别名,也不复用其返回类型 —— 它是**独立**的 int 入口:
//    计数字段(weight / stack_max / 剂量上下限一类)物理上不进定点域,只做整数比较。
//    把它们喂进 FixParse 会把一个整数计数塞进 Q16.16(ADR-006 二轮修正正是为堵此口径错误)。
//
// ⚠️ 运行期玩家构建不加载本类型(ADR-014 §二:烘焙期消费,玩家构建零解析器)。

using System;
using System.Globalization;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>字符串 → <see cref="int"/> 的唯一解析入口,供 <c>weight</c> / <c>stack_max</c>
    /// 等计数字段使用(D-21-17)。**拒** Fix 语法(分子/分母)与浮点字面量 —— 计数字段不接受
    /// 定点域的任何书写形式。</summary>
    public static class IntParse
    {
        /// <summary>解析计数字段字面量。合法形:纯整数串(可带符号),如 <c>"3"</c> · <c>"-1"</c>。
        /// <b>拒</b> <c>"3/4"</c>(Fix 分数语法)· <c>"1.5"</c>(浮点)· <c>"1e3"</c>(科学计数)·
        /// 空串 —— 任一非法形抛 <see cref="FormatException"/>,硬失败,不回退默认值。</summary>
        /// <param name="literal">字段原文(作者态 JSON 数字的字符串化,或 schema 层抽出的原文)。</param>
        /// <returns>解析出的 <see cref="int"/> 计数值。</returns>
        /// <example><c>IntParse.Parse("3")</c> ⇒ <c>3</c>;
        /// <c>IntParse.Parse("3/4")</c> ⇒ 抛 <see cref="FormatException"/>。</example>
        public static int Parse(string literal)
        {
            if (string.IsNullOrWhiteSpace(literal))
                throw new FormatException("IntParse:空字面量");

            // Fix 语法 / 浮点字面量显式拒(即便 int.TryParse 本就过不去,也给出确定的错误语义,
            // 防日后 NumberStyles 被放宽时静默放行 —— 与 FixParse 同款硬拒纪律)
            if (literal.IndexOf('/') >= 0 || literal.IndexOf('.') >= 0 ||
                literal.IndexOf('e') >= 0 || literal.IndexOf('E') >= 0)
                throw new FormatException(
                    $"IntParse:计数字段只接受整数,拒 Fix 语法/浮点字面量 \"{literal}\"(D-21-17)");

            if (!int.TryParse(literal, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value))
                throw new FormatException($"IntParse:无法解析为整数 \"{literal}\"");

            return value;
        }
    }
}
