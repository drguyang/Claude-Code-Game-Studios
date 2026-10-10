// 权威来源:ADR-005 §Key Interfaces(:224)· ADR-006 §五(D-21-18)· ADR-025 §② 甲案
//
// Q16.16 定点域,内部 long。**刻意不定义 implicit operator float**(ADR-005:226)——
// 浮点泄漏是本项目反复警惕的静默失败面。
//
// ToFloat() 的「消费约束」不靠可见性,靠 ADR-025 §② 甲案的**构建期白名单断言**:
//   调用点所在 asmdef ∈ {Sim.Codec, Gameplay.*};在 Sim 内调用 = 构建失败(U0-b b4 执行)。
//   乙案(internal + InternalsVisibleTo)已被否决留档 —— Fix 必须在 Sim 与 Sim.Codec 两侧出现,
//   internal 反而切不开。
//
// ⚠️ 落盘纪律(ADR-006 §五 / D-21-18):Fix **不可**经 Unity 内置序列化器承载
//   (禁 JsonUtility / [SerializeField] / ScriptableObject / prefab 字段)。
//   该禁令由 Sim.Codec 的自定义编码器 + 一条 EditMode 探针守住(21a AC-21a-53)。

using System;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>Q16.16 定点值(内部 long)。全案模拟数学的唯一数值载体(ADR-005)。</summary>
    public readonly struct Fix
    {
        public const int FractionalBits = 16;
        public const long OneRaw = 1L << FractionalBits;   // 1.0 == 65536
        public const long ZeroRaw = 0L;                     // 0.0
        public static readonly Fix One = new Fix(OneRaw);
        public static readonly Fix Zero = new Fix(ZeroRaw);

        /// <summary>满级哨兵(raw = long.MaxValue)。永远不在合法 Q16.16 取值域内;
        /// 任何算术运算与之交互均触发 OverflowException。</summary>
        public static readonly Fix PositiveInfinity = new Fix(long.MaxValue);

        private readonly long _raw;

        // ⚠️ 构造入口 + Raw 读出 —— 超出 ADR 原文,由我(U0 起草方)补,
        //    因为 ADR-006 §五 要求编码器「显式写出 / 读入 _raw」却未给 API 面。
        //    b1b 复核(2026-09-22 批)**照准**:载荷 struct 的 `Fix` 字段(支 1-a 裁定)与
        //    Sim.Codec 编码器(未落地)均依赖此二成员;签名如需扩(FromRaw 命名等)归 codec 轮。
        /// <summary>按有理数构造 Fix,舍入模式 = ROUND_HALF_AWAY_FROM_ZERO(ADR-006)。</summary>
        /// <remarks>等价于 FixParse.FromRatio 的舍入路径,但接受原始分子/分母,不经字符串解析。</remarks>
        public static Fix FromRational(long numerator, long denominator)
        {
            if (denominator == 0) throw new DivideByZeroException("Fix.FromRational:分母为 0");
            long scaled = numerator << FractionalBits;
            return new Fix(FixParse.RoundHalfAwayFromZero(scaled, denominator));
        }

        public Fix(long raw) { _raw = raw; }

        /// <summary>落盘形状(8 字节小端的语义源)。编码器专用;非呈现路径。</summary>
        public long Raw => _raw;

        /// <summary>唯一浮点出口。调用点受构建期白名单约束(见文件头)。</summary>
        public float ToFloat() => (float)_raw / OneRaw;

        /// <summary>整数舍入(Q16.16 → 整数),统一模式 ROUND_HALF_AWAY_FROM_ZERO,
        /// 纯整数域内完成 —— ADR-006 §Decision 三 · AC-21a-42。
        /// <para>中点远离零:Round(0.5)=1 · Round(−0.5)=−1 · Round(1.5)=2 · Round(2.5)=3
        /// (显式切断 <c>Math.Round</c> 默认的 ties-to-even —— Round(2.5)=2 是被禁模式)。</para>
        /// <para><c>RoundMode</c> 是全局常量而非逐调用参数(ADR-006:178),
        /// 本方法不收 mode 参数即其落地形。</para>
        /// </summary>
        /// <returns>舍入到最近整数的值(中点远离零)。</returns>
        /// <example><c>FixParse.Parse("3/2").Round()</c> ⇒ <c>2</c>;
        /// <c>new Fix(-32768L).Round()</c> ⇒ <c>-1</c>(−0.5 远离零,不归 0)。</example>
        public long Round() => FixParse.RoundHalfAwayFromZero(_raw, OneRaw);

        // ── 中间乘(U1 spike · F7 落地形)──
        // 权威:disease-simulation.md AC-4 —— 「唯一路径 = 手工 hi/lo 拆分」(Int128 不存在,
        // 无条件钉 hi/lo 不留条件分支);「signed long 带进位加法 = UB ⇒ hi/lo 全程无符号」(四轮 K6);
        // 「禁有符号右移」;交叉项无符号乘 + 掩码;舍入 = ROUND_HALF_AWAY_FROM_ZERO
        // (ADR-006 §三「全部舍入」钉死本步,D-2 问题就此消解 —— 同 FixParse 同模式)。
        // 符号:先乘 |a|·|b| 无符号量积(≤ 2^126 精确),右移+舍入后一次回符号 ——
        // 中间全程 ulong,有符号溢出 UB 结构性不存在(F7)。溢出域外 = OverflowException
        // (「溢出即 bug」= AC-4/GDD Edge Case 口径,非静默回绕)。

        public static Fix operator *(Fix a, Fix b) => new Fix(MulRaw(a._raw, b._raw));

        /// <summary>raw 面的中间乘(单测/黄金向量的直接入口,不经 Fix 装箱)。</summary>
        public static long MulRaw(long a, long b)
        {
            bool neg = (a < 0) ^ (b < 0);
            // |值| 经 ulong 补码取负,避开 -long.MinValue 的 checked 陷阱(测试装配日后可挂 -checked+)
            ulong ua = a < 0 ? unchecked(0UL - (ulong)a) : (ulong)a;
            ulong ub = b < 0 ? unchecked(0UL - (ulong)b) : (ulong)b;

            // 64×64→128 无符号量积(四路拆分带进位,全部 ulong unchecked)
            ulong x0 = ua & 0xFFFFFFFFUL, x1 = ua >> 32;
            ulong y0 = ub & 0xFFFFFFFFUL, y1 = ub >> 32;
            ulong p00 = unchecked(x0 * y0);
            ulong p01 = unchecked(x0 * y1);
            ulong p10 = unchecked(x1 * y0);
            ulong p11 = unchecked(x1 * y1);
            ulong midSum = unchecked(p01 + p10);
            ulong midCarry = midSum < p01 ? 1UL : 0UL;                    // midSum 溢出进位(5 位域)
            ulong lo = unchecked(p00 + unchecked(midSum << 32));
            ulong loCarry = lo < p00 ? 1UL : 0UL;
            ulong hi = unchecked(p11 + (midSum >> 32) + (midCarry << 32) + loCarry);

            // (hi,lo) >> 16 回 Q16.16 + half-away:余数 bit15 即 rem/2^16 ≥ 0.5 位
            // 商 = hi<<48 | lo>>16 装入 u64 的充要 = hi < 2^16(mag < 2^80);超界即域外
            if ((hi >> 16) != 0UL)
                throw new OverflowException("Fix.MulRaw:中间乘积超出 Q16.16 域");
            ulong q = unchecked((hi << 48) | (lo >> 16));
            if ((lo & 0x8000UL) != 0UL)
            {
                ulong before = q;
                q = unchecked(q + 1UL);
                if (q < before)                                            // 舍入进位把 2^64-1 挤爆
                    throw new OverflowException("Fix.MulRaw:舍入进位溢出");
            }

            const ulong SignBound = 1UL << 63;
            if (q > SignBound || (q == SignBound && !neg))                // long 边界仅负侧可达
                throw new OverflowException("Fix.MulRaw:结果超出 int64 定点域");
            // q == 2^63 且 neg 时 unchecked -(long)q 恰回 long.MinValue(位形合法)
            return neg ? unchecked(-(long)q) : (long)q;
        }

        // ── 算术二元运算(加 / 减 / 乘 / 除) ─────────────────────────────────
        // Q16.16 同标度加法 = raw 直接相加,无舍入(同标度两 Fix 相加无需缩放)。
        // 溢出 = bug ⇒ checked 强制溢出检测(与 MulRaw 同口径)。

        /// <summary>Q16.16 加法(同标度,无舍入)。溢出时抛 <see cref="OverflowException"/>。</summary>
        public static Fix operator +(Fix a, Fix b)
        {
            checked { return new Fix(a._raw + b._raw); }
        }

        /// <summary>Q16.16 减法(同标度,无舍入)。溢出时抛 <see cref="OverflowException"/>。</summary>
        public static Fix operator -(Fix a, Fix b)
        {
            checked { return new Fix(a._raw - b._raw); }
        }

        /// <summary>Q16.16 定点除法。全程整数域,舍入 = ROUND_HALF_AWAY_FROM_ZERO(ADR-006)。
        /// <para>分母为零抛 <see cref="DivideByZeroException"/>;溢出抛 <see cref="OverflowException"/>。</para></summary>
        /// <remarks>算法:先计算 |a| * 2^16 / |b| 的整数商(64 位安全,本系统所有 Fix 均满足),
        /// 余数 >= 0x8000 时 +1(半 Away-From-Zero);最后回符号。</remarks>
        public static Fix operator /(Fix a, Fix b)
        {
            if (b._raw == 0) throw new DivideByZeroException("Fix.Div:分母为 0");

            bool neg = (a._raw < 0) ^ (b._raw < 0);
            ulong ua = a._raw < 0 ? unchecked(0UL - (ulong)a._raw) : (ulong)a._raw;
            ulong ub = b._raw < 0 ? unchecked(0UL - (ulong)b._raw) : (ulong)b._raw;

            // 目标: Q16.16(a) / Q16.16(b) = (a_raw / b_raw) 仍在 Q16.16
            // 即 floor( (a_raw * 2^16) / b_raw )
            if (ua > (ulong.MaxValue >> FractionalBits))
                throw new OverflowException("Fix.Div:被除数超出 Q16.16 可表示域(ua > 2^48)");
            ulong scaled = unchecked(ua << FractionalBits);
            ulong q = scaled / ub;                    // 整数商
            ulong rem = scaled % ub;                  // 余数

            // ROUND_HALF_AWAY_FROM_ZERO: 余数 * 2 >= 分母 → +1
            // 用 rem*2 >= ub 而非 rem >= ub>>1,确保奇分母时阈值 = ceil(ub/2)(承 FixParse.RoundHalfAwayFromZero)
            if (rem * 2 >= ub)
            {
                ulong before = q;
                q = unchecked(q + 1UL);
                if (q < before)                       // 溢出
                    throw new OverflowException("Fix.Div:舍入进位溢出");
            }

            const ulong SignBound = 1UL << 63;
            if (q > SignBound || (q == SignBound && !neg))
                throw new OverflowException("Fix.Div:结果超出 int64 定点域");
            // q == 2^63 时 unchecked -(long)q = long.MinValue(位形合法,同 MulRaw)
            return new Fix(neg ? unchecked(-(long)q) : (long)q);
        }

        // ── 比较运算符 ──────────────────────────────────────────────────────
        // PositiveInfinity 判等: raw == long.MaxValue 为唯一哨兵。
        // 语义上 Fix 相等 = raw 逐位相等(含 PositiveInfinity)。
        // 注意:不重写 Object.Equals / GetHashCode(值类型默认已按字段比较,
        // 重写反而不如默认;Fix 只用于比较/算术,不做 Dictionary 键)。

        public static bool operator ==(Fix a, Fix b) => a._raw == b._raw;
        public static bool operator !=(Fix a, Fix b) => a._raw != b._raw;
        public override bool Equals(object obj) => obj is Fix other && _raw == other._raw;
        public override int GetHashCode() => _raw.GetHashCode();

        // ── 整数幂与平方根(ADR-026 §Decision 一 · ADR-026 §Decision 二)──
        // 权威:ADR-026 ① FixPow 指数闭集 {整数, 整数+1/2};② 允许构建期定表记忆化;
        // ADR-026 Implementation Guideline 1(先 Fix.Div/ISqrt/Pow 再写成长公式)。
        // 全程整数域,禁 Math.Sqrt / Math.Pow / libm / 任意 float。

        /// <summary>整数幂(指数 >= 0)。exponent = 0 → Fix.One;exponent = 1 → base;
        /// exponent < 0 → ArgumentOutOfRangeException(本系统不用负指数)。</summary>
        /// <remarks>用 <see cref="operator*"/> 循环自乘,逐位确定(承 ADR-012 黄金夹具)。</remarks>
        public static Fix Pow(Fix baseValue, int exponent)
        {
            if (exponent < 0)
                throw new ArgumentOutOfRangeException(nameof(exponent),
                    "Fix.Pow(int):指数不可负(本系统只取非负整数)");
            if (exponent == 0) return One;
            if (exponent == 1) return baseValue;

            Fix result = One;
            for (int i = 0; i < exponent; i++)
                result *= baseValue;
            return result;
        }

        /// <summary>整数牛顿迭代求平方根,返回 Q16.16 定点值。
        /// <para>对 Q16.16 值 v,目标结果是 floor(sqrt(v) * 2^16)。
        /// 实现方式:把 v 的 raw 值左移 16 位(即乘以 65536),
        /// 然后对该缩放后的整数做 Newton 迭代求整数平方根,
        /// 迭代结果本身就是正确的 Q16.16 raw 值。</para>
        /// <para>全程 ulong 运算,不调 libm / 不经过 float。</para>
        /// </summary>
        public static Fix Sqrt(Fix value)
        {
            if (value._raw == 0) return new Fix(0L);
            if (value._raw < 0)
                throw new ArgumentOutOfRangeException(nameof(value),
                    "Fix.Sqrt:负数无实平方根");

            // 目标: floor(sqrt(v) * 2^16) = floor(sqrt(v_raw * 2^16))
            // 对 v_raw << 16 做整数 Newton 迭代,结果即为 Q16.16 raw 值
            // v_raw 须 < 2^48(ulong 左移 16 位不溢出);本系统所有合法 Fix
            // 均满足(技能系统最大中间值 ≈ 40 * 59^2 * 65536 ≈ 9.1e9 ≪ 2^48)
            ulong v = ((ulong)value._raw) << FractionalBits;
            ulong guess = v >> 1;
            while (true)
            {
                ulong next = (guess + v / guess) >> 1;
                if (next >= guess) break;          // 不动点(Newton 整数域单调递减收敛)
                guess = next;
            }

            return new Fix((long)guess);            // guess 已经是 Q16.16 raw 值
        }

        /// <summary>幂运算的唯一实现(指数闭集 {整数, 整数 + 1/2})。
        /// <para>整数部分(raw & 0xFFFF == 0) → <see cref="Pow(Fix,int)"/>;
        /// 半整数部分(raw & 0xFFFF == 0x8000) → Pow(整数部分) * Sqrt(base);
        /// 其余指数 → <see cref="ArgumentOutOfRangeException"/>。</para>
        /// </summary>
        public static Fix Pow(Fix baseValue, Fix exponent)
        {
            const ushort HalfFractional = 0x8000;  // 0.5 的 Q16.16 表示

            // 提取整数部分(指数的小数部分须全零或全 0x8000)
            int intPart = (int)(exponent._raw >> FractionalBits);
            ushort fracPart = (ushort)(exponent._raw & 0xFFFF);

            if (fracPart == 0)
            {
                // 纯整数指数
                return Pow(baseValue, intPart);
            }
            if (fracPart == HalfFractional)
            {
                // 半整数指数 = 整数幂 × sqrt(base)
                Fix intPow = Pow(baseValue, intPart);
                return intPow * Sqrt(baseValue);
            }

            throw new ArgumentOutOfRangeException(nameof(exponent),
                $"Fix.Pow(Fix):指数须 ∈ {{整数, 整数+1/2}},实际 raw={exponent._raw}(frac={exponent._raw & 0xFFFF:X4})");
        }

        // ── 指数函数(ADR-026 FixPow/FixSqrt 同族先例 · disease-simulation.md F0「定点库范围包含 Exp」)──
        // 权威:
        //   design/gdd/disease-simulation.md
        //     · F0「⚠️ 定点 Exp 必须计入实现量 —— F1/F2 通篇依赖它」(Base / Relapse / Decay 全是 e^(−τ/·))
        //     · F0「定点 Exp 是纯确定性函数(同输入同输出)」+ 中间精度行「精度取舍归 Gate 待标」
        //   docs/architecture/adr-026-skill-growth-fixed-point.md —— 唯一整数幂实现先例
        //     (禁 float / libm / Math.Exp;构建期查表允许但须逐值相等)
        //   docs/architecture/adr-006 —— ROUND_HALF_AWAY_FROM_ZERO 单一舍入
        //
        // 算法(ln2 范围规约 + Taylor 级数,全程整数域):
        //   e^x = 2^k · e^r , k = round(x / ln2) [ln2 取 Q32.32 常量 2977044472 = round(ln2·2^32),
        //         绝对误差 0.1804/2³² ≈ 4.2e-11,相对 ≈ 6.06e-11], r = x − k·ln2 ⇒ |r| ≤ ln2/2 ≈ 0.3466
        //   e^r = Σ_{n=0..10} r^n/n!,项递推 t_n = t_{n−1} · r / n:
        //         乘走既有 <see cref="MulRaw"/>(128 位 hi/lo 唯一路径),除走
        //         <see cref="FixParse.RoundHalfAwayFromZero"/> —— **不新造第二条宽乘路径**
        //   k ≥ 0:出口 = sum << k(纯移位,精确);k < 0:出口 = 半-away 移位
        //
        // 截断判据与收敛域:
        //   |R_10| ≤ |r|^11/11! ≤ (ln2/2)^11/11! ≈ 2.2e-13 ≈ 1.4e-8 LSB ≪ 1 ulp(整数域可分辨的最小量)
        //   级数在 r ∈ (−∞,∞) 绝对收敛;本实现锚定 |r| ≤ ln2/2。
        //   即便 k 因 ln2̂ 舍入偏差错取 ±1(实际不会:k 的误差 < 1e-8),|r| ≤ 1.5·ln2 时
        //   |R_10| ≤ 1.04^11/11! ≈ 3.8e-8,仍 ≪ 1 ulp —— 截断不构成误差预算项。
        //
        // 误差上界论证(相对结果值,1 ulp = 2⁻¹⁶):
        //   ① r 从 Q32.32 舍入到 Q16.16:|δr| ≤ 0.5×2⁻¹⁶ ⇒ e^r 相对误差 ≤ 0.5 ulp
        //   ② 级数 20 次舍入(10 乘 + 10 除,各 ≤ 0.5 LSB,误差随后乘 r/n 收缩):宽松计数
        //      ≤ ~10 LSB on S ∈ [0.707,1.414] ⇒ ≤ 16 ulp 相对(粗界,仅用于断言的可论证性;
        //      精确界由全域穷举给出,见下)
        //   ③ k·δ(ln2 常量误差):|k| ≤ 47 ⇒ 相对误差 ≤ 47·4.2e-11 ≈ 2e-9 ≪ 1 ulp
        //   ④ 截断 2.2e-13(上条);⑤ k ≥ 0 出口纯移位零误差;⑥ k < 0 出口 ≤ 0.5 LSB 绝对
        //   ⇒ **全域穷举实测**(2,907,261 点 = 本域全部整数 raw,对 Math.Exp 浮点神谕,
        //      神谕仅测试侧存在),误差三段口径(评审码-1 修正:3.12 仅在值 ≥ 1.0 子域成立,
        //      全域相对最大 100% 出现在量化下限 -772243 处 v=1/o=0.5 —— 属 Q16.16 固有量化,
        //      由绝对项承担,非缺陷):
        //      · 值 ≥ 1.0(oracle ≥ 65536 LSB):最大相对 3.12×2⁻¹⁶
        //      · 小值区(oracle < 4096 LSB):最大绝对 0.595 LSB
        //      · 中间带 [0.0625, 1.0):相对最大 8.43×2⁻¹⁶,由断言的绝对项 3 LSB 承担
        //   ⇒ 断言界 ε = 4×2⁻¹⁶ 相对 + 3 LSB 绝对(tests/EditMode/Sim/fix_exp_test.cs)
        //
        // 定义域与异常(GDD 未规定;形态对齐 FixPow / FixMul 惯例):
        //   · x_raw > 2135016(≈ 32.5777)⇒ OverflowException。数学上界 = floor(47·ln2·2^16)
        //     = 2135026(e^x ≥ 2^47 时 Fix 的 raw(long)装不下),实现收到 2135016 留 10 raw
        //     余量:级数出口 S 的舍入(+3 LSB 量级)会在最顶上数个 raw 把结果顶过 long.MaxValue
        //     —— 与其放行后再溢出,不如在域检查处一致地抛(同 FixMul「溢出即 bug」口径)。
        //     亦是 xQ32 = x_raw<<16 防静默回绕与 k 防出 int 的前置门。
        //   · x_raw ≤ −772244(= floor(−17·ln2·2^16),即 e^x < 0.5 LSB)⇒ Fix.Zero。
        //     这是「半 away 舍入把 < 0.5 LSB 收为 0」的**精确提前**,不是饱和近似 ——
        //     对该范围内每个 raw,真值舍入后就是 0(与 F1 的 Decay 不同,GDD 的 Δ ≥ 0 门在求值侧,
        //     本函数不代偿负指数爆炸:域检查已保证 |x| ≤ 11.79 以下全部归零,k ∈ [−17,47] 有界)。
        //   · x = 0 ⇒ Fix.One(级数恒等,快路径只省算不改值)。
        //   · 负有理指数(F1 的 e^(−W/H):W、H 为 Fix,指数 = Fix 除法结果)全支持 ——
        //     这是 Decay / Base 衰减段 / Relapse 的主用面。
        //   ⚠️ GDD F0 中间精度行「Exp 的中间量也在 128 位域,不『每步回降』」与本实现的
        //     「级数每项经 FixMul 落回 Q16.16」存在字面张力(F0 自标「精度取舍归 Gate 待标」;
        //     批次裁定 = 走既有 FixMul、不新造宽乘路径)—— 已在批次报告中登记,不在此自行改标。

        /// <summary>定点指数 e^x(Q16.16 → Q16.16),纯整数域:ln2 范围规约 + 10 阶 Taylor 级数。
        /// <para>全程禁 float / double / libm / <c>Math.Exp</c>;中间乘 = <see cref="MulRaw"/>
        /// (128 位 hi/lo 唯一路径),舍入 = ROUND_HALF_AWAY_FROM_ZERO(ADR-006)。</para>
        /// <para><b>误差</b>:相对 ≤ 4×2⁻¹⁶ + 绝对 ≤ 3 LSB(全域 2,907,261 点穷举对拍
        /// <c>Math.Exp</c> 神谕实测 3.12×2⁻¹⁶ / 0.595 LSB,断言见
        /// <c>tests/EditMode/Sim/fix_exp_test.cs</c>)。</para>
        /// <para><b>域</b>:x_raw ≤ 2135016(e^x ≥ 2^47 无处可放);x_raw ≤ −772244 ⇒ Zero
        /// (e^x &lt; 0.5 LSB,半-away 舍入恰为 0,精确提前非饱和);x = 0 ⇒ One。</para></summary>
        /// <param name="value">指数(Fix 有理数;F1/F2 的用面全为 ≤ 0 的衰减指数,本实现双向支持)。</param>
        /// <returns>e^x 的 Q16.16 定点值。</returns>
        /// <exception cref="OverflowException">x_raw &gt; 2135016(超可表示域),
        /// 或出口移位结果超出 long(域顶余量被级数舍入吃穿时的兜底,实际域检查已挡住)。</exception>
        public static Fix Exp(Fix value)
        {
            // 域常量(整数域内钉死;推导见文件头)。
            const long XMaxRaw = 2135016L;      // ≈ floor(47·ln2·2^16) − 10
            const long XZeroRaw = -772244L;     // = floor(−17·ln2·2^16):e^x < 0.5 LSB
            const long Ln2Q32 = 2977044472L;    // round(ln2 × 2^32)(Q32.32)
            const int SeriesTerms = 10;         // n = 0..10,截断余项 ≤ (ln2/2)^11/11! ≈ 2.2e-13

            long xr = value._raw;
            if (xr > XMaxRaw)
                throw new OverflowException(
                    $"Fix.Exp:x_raw={xr} 超出可表示域(上界 {XMaxRaw} ≈ ln(2^47),e^x ≥ 2^47 装不进 Fix)");
            if (xr <= XZeroRaw) return Zero;    // e^x < 0.5 LSB ⇒ 舍入即 0(精确,非饱和)
            if (xr == 0) return One;

            // 范围规约:Q32.32 域内求 k 与 r(|xr| ≤ 2135016 ⇒ xQ32 ≤ 1.4e11,不溢出)
            long xQ32 = xr << FractionalBits;
            int k = (int)FixParse.RoundHalfAwayFromZero(xQ32, Ln2Q32);          // |k| ≤ 47
            long rRaw = FixParse.RoundHalfAwayFromZero(xQ32 - k * Ln2Q32, 1L << FractionalBits);

            // e^r 的 Taylor 级数(t_0 = 1;t_n = t_{n−1}·r/n),项值单调衰减,累加同标度无舍入
            long term = OneRaw;
            long sum = OneRaw;
            for (int n = 1; n <= SeriesTerms; n++)
            {
                term = MulRaw(term, rRaw);
                term = FixParse.RoundHalfAwayFromZero(term, n);
                sum += term;
            }

            if (k >= 0)
            {
                ulong scaled = (ulong)sum << k;   // k ≤ 47:移位计数合法;纯移位零舍入
                if (scaled > long.MaxValue)
                    throw new OverflowException("Fix.Exp:结果超出 Q16.16 可表示域");
                return new Fix((long)scaled);
            }

            if (k <= -63) return Zero;            // 防御(域内 k ≥ −17 不可达):防 1L<<-k 移位计数越界
            return new Fix(FixParse.RoundHalfAwayFromZero(sum, 1L << -k));
        }
    }
}
