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

        private readonly long _raw;

        // ⚠️ 构造入口 + Raw 读出 —— 超出 ADR 原文,由我(U0 起草方)补,
        //    因为 ADR-006 §五 要求编码器「显式写出 / 读入 _raw」却未给 API 面。
        //    b1b 复核(2026-09-22 批)**照准**:载荷 struct 的 `Fix` 字段(支 1-a 裁定)与
        //    Sim.Codec 编码器(未落地)均依赖此二成员;签名如需扩(FromRaw 命名等)归 codec 轮。
        public Fix(long raw) { _raw = raw; }

        /// <summary>落盘形状(8 字节小端的语义源)。编码器专用;非呈现路径。</summary>
        public long Raw => _raw;

        /// <summary>唯一浮点出口。调用点受构建期白名单约束(见文件头)。</summary>
        public float ToFloat() => (float)_raw / OneRaw;

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
    }
}
