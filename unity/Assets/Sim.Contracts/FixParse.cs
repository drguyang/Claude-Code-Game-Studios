// 权威来源:ADR-006 §Decision 一(:108-124)· §Decision 三(舍入)· §Decision 二(D-21-17 解析集收窄)
//
// 这是「外部数据 → Fix」的**唯一**入口。刻意不提供 Parse(float) / implicit operator Fix(float) /
// (Fix)0.5 —— 浮点字面量一旦能进来,ADR-005 的整数域前提就在边界上破了。
//
// ⚠️ 运行期玩家构建**不加载本类型**(ADR-014 §二:两阶段工具链在构建期烘焙 *.cooked,
//   「玩家构建零 JSON 解析器、零 FixParse」)。故本类住 Sim.Contracts 是为**编辑期 / 烘焙期**服务;
//   其逐位平台一致性因此从运行期确定性面上消失(ADR-014 §二 的结构性规避)。
//
// ⚠️ D-21-17:`weight` / `stack_max` 是 int 计数,**不在本解析集内**(原稿误纳)。

using System;
using System.Globalization;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>字符串 → Q16.16 定点的唯一解析入口。只接受整数字面量或「分子/分母」。</summary>
    public static class FixParse
    {
        /// <summary>解析作者态字面量。合法形:<c>"-3"</c> · <c>"3/4"</c> · <c>"196608/65536"</c>。
        /// **拒**任何含小数点 / 指数的写法(<c>"0.75"</c> 直接抛)—— 浮点字面量是违例,不是待归一输入。</summary>
        /// <param name="mode">签名面保留以对齐 ADR-006:114;<see cref="RoundMode"/> 只有唯一成员,
        /// 故「全局唯一模式」由类型钉死(见 RoundMode.cs 文件头)。</param>
        public static Fix Parse(string literal, RoundMode mode = RoundMode.HalfAwayFromZero)
        {
            if (string.IsNullOrWhiteSpace(literal))
                throw new FormatException("FixParse:空字面量");

            // 浮点字面量硬拒(禁经 double / decimal 中转 —— ADR-014 §三 同一纪律)
            if (literal.IndexOf('.') >= 0 || literal.IndexOf('e') >= 0 || literal.IndexOf('E') >= 0)
                throw new FormatException(
                    $"FixParse:拒浮点字面量 \"{literal}\" —— 作者写法只能是整数或 分子/分母");

            int slash = literal.IndexOf('/');
            if (slash < 0)
            {
                if (!long.TryParse(literal, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long whole))
                    throw new FormatException($"FixParse:无法解析为整数 \"{literal}\"");
                return FromRatio(whole, 1L);
            }

            string num = literal.Substring(0, slash);
            string den = literal.Substring(slash + 1);
            if (!long.TryParse(num, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long n) ||
                !long.TryParse(den, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long d))
                throw new FormatException($"FixParse:分子/分母须为整数 \"{literal}\"");

            return FromRatio(n, d);
        }

        /// <summary>可选字段入口(D-21-6 · AC-21a-57 边缘:字段可为 P0 空 / JSON null,<b>不</b>触发硬失败)。
        /// <para><c>null</c>(字段缺席 / JSON null)⇒ 返回 <c>null</c>,schema 层记「无值」,不调 <see cref="Parse"/>。</para>
        /// <para>非 <c>null</c> 一律转 <see cref="Parse"/> —— <b>空串仍硬失败</b>(AC-21a-41 边缘:
        /// 空串是「写了但没值」,不是「缺席」,两者不得混同)。</para></summary>
        /// <param name="literal">字段原文;<c>null</c> 表示缺席 / JSON null。</param>
        /// <returns><c>null</c>(当输入为 <c>null</c>)否则解析出的 <see cref="Fix"/>。</returns>
        /// <example><c>FixParse.ParseOptional(null)</c> ⇒ <c>null</c>(D-21-6 合法);
        /// <c>FixParse.ParseOptional("")</c> ⇒ 抛 <see cref="FormatException"/>。</example>
        public static Fix? ParseOptional(string literal)
        {
            if (literal is null) return null;
            return Parse(literal);
        }

        /// <summary>整数 + 分母形式(唯一推荐的作者写法)。**不经浮点**。</summary>
        public static Fix FromRatio(long numerator, long denominator)
        {
            if (denominator == 0) throw new DivideByZeroException("FixParse:分母为 0");

            // 中间量落在 int64:|numerator| << 16 溢出即视为**输入域错误**,由调用方(数值轮)保证域。
            // 此处不做静默回绕(承种子测试同款口径)。
            long scaled = numerator << Fix.FractionalBits;
            return new Fix(RoundHalfAwayFromZero(scaled, denominator));
        }

        /// <summary>整数除法 + 就近舍入,中点**远离零**(ADR-006 §Decision 三)。</summary>
        public static long RoundHalfAwayFromZero(long dividend, long divisor)
        {
            long q = dividend / divisor;      // C# 整数除法 = 向零截断
            long r = dividend % divisor;
            if (r == 0) return q;

            // |r| < |divisor| 恒成立 ⇒ absR * 2 只在 |divisor| > 2^62 时溢出;
            // ADR-006 的输入域(|raw| ≲ 2^40)由数值轮保证,超域属导入期错误。
            long absR = Math.Abs(r);
            long absD = Math.Abs(divisor);

            // step = 分数部分 r/divisor 的方向;在中点上它恰是「远离零」的方向。
            int step = (r > 0) == (divisor > 0) ? +1 : -1;

            if (absR * 2 >= absD) return q + step;   // == 即中点 → 远离零
            return q;
        }
    }
}
