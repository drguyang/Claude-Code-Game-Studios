// 权威来源:ADR-005 §Decision 一(哈希用 SplitMix64,整数域)· ADR-012 F7(ulong 表示 = UB 结构性消解)
//          · disease-simulation.md:733(固定 64 位无符号、显式 <</| 组装、不走 BitConverter 端序敏感路径、
//            unchecked 回绕为**定义性**语义)· ADR-007 §三(掷骰输入可从事件流重构)
//
// D-1 用户裁定(2026-09-22 批,U1 spike 批):多输入折叠 = **乙 · 链式 avalanche** —
//   state = 首输入;对每个后续输入 w:state = Avalanche(state + w);终态再 Avalanche 一次。
//   加法为 mod 2^64 回绕(ulong 天然)。事后改折叠 = 全体平台重签 + 9/52/7a 哈希语义作废,
//   故此形状一经落码即锁(黄金夹具 golden-v1 的第一条真源)。
//
// 落点理由(程序集):住 Sim.Contracts 而非 Sim —— ① S7 确定性格内偏移(ADR-023 ⑦)的生成者是
// 表现层,Sim 它引用不到(ADR-016 门 F 白名单);② 哈希是「三源之纯函数」(ADR-016 §一),
// 两侧都要能跑 ⇒ 与 Fix / WorldPos 同例进边界程序集。门 A 兼容:纯 BCL、零引擎引用。
//
// ⚠️ 逐位纪律(F7 / ADR-012 §一):全部算术落 ulong + unchecked。无符号溢出在 C# 与
// IL2CPP 生成的 C++ 两侧都是定义性回绕(mod 2^64)⇒ 有符号溢出 UB 结构性不存在。

using System;
using System.Text;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>SplitMix64 —— 全案唯一整数哈希源(ADR-005;禁 UnityEngine.Random,AC-52-06/07)。</summary>
    public static class SplitMix64
    {
        /// <summary>黄金比例增量(Steele et al. 标准形)。公开为常量 = 事件流重构方核对步进用。</summary>
        public const ulong Gamma = 0x9E3779B97F4A7C15UL;
        private const ulong Mul1 = 0xBF58476D1CE4E5B9UL;
        private const ulong Mul2 = 0x94D049BB133111EBUL;

        /// <summary>末段混淆(标准 avalanche 形)。纯函数,黄金向量直接钉它。</summary>
        public static ulong Avalanche(ulong z)
        {
            z = unchecked((z ^ (z >> 30)) * Mul1);
            z = unchecked((z ^ (z >> 27)) * Mul2);
            return unchecked(z ^ (z >> 31));
        }

        /// <summary>流形一步:state 前进 + 产出(ADR-007 §三 掷骰消费序号 = 反复调用本方法)。</summary>
        public static ulong NextValue(ref ulong state)
        {
            state = unchecked(state + Gamma);
            return Avalanche(state);
        }

        /// <summary>折叠一步(D-1 乙案原语):state ← Avalanche(state + w)。</summary>
        public static ulong Fold(ulong state, ulong w) => Avalanche(unchecked(state + w));

        // ── 折叠形门面:registry 调用形 SplitMix64(WorldSeed, x, y, ...) 的落点 ──
        // long 按位型转 u64(负哨兵如 PatientId.None = -1 → 0xFFFF_FFFF_FFFF_FFFF,
        // 与 52 的抽取序号、7a 的 id 空间同一口径)。
        // ⚠️ 首两输入 a、b 可交换(a+b 加法交换 + 同步 avalanche),第三输入起有序;
        // 消费方不得依赖首两参的交换性 —— 只是黄金测试的探针性质。

        public static ulong Hash(long a, long b)
            => Avalanche(Fold(U(a), U(b)));

        public static ulong Hash(long a, long b, long c)
            => Avalanche(Fold(Fold(U(a), U(b)), U(c)));

        public static ulong Hash(long a, long b, long c, long d)
            => Avalanche(Fold(Fold(Fold(U(a), U(b)), U(c)), U(d)));

        /// <summary>
        /// 加盐形:registry 的 <c>SplitMix64(WorldSeed, "case-salt", ordinal)</c> /
        /// <c>SplitMix64(WorldSeed, "gather", node_id, gather_seq)</c> 的落点。
        /// tag 编码块 = [字节数] + 每 8 字节**大端**补零块(显式 &lt;&lt;| 组装 ——
        /// disease-simulation.md:733 该句的原位落点,零端序敏感路径)。
        /// tag 只按 ASCII 解释(数据层校验非 ASCII 即导入失败,先于本调用);
        /// 空 tag 折叠 [0] 一块,与无 tag 形**不同值** ⇒ 盐位占位不撞。
        /// </summary>
        public static ulong HashTagged(long a, string tag, long b)
        {
            ulong state = FoldTag(U(a), tag);
            return Avalanche(Fold(state, U(b)));
        }

        public static ulong HashTagged(long a, string tag, long b, long c)
        {
            ulong state = FoldTag(U(a), tag);
            return Avalanche(Fold(Fold(state, U(b)), U(c)));
        }

        private static ulong FoldTag(ulong state, string tag)
        {
            if (tag == null) throw new ArgumentNullException(nameof(tag));
            byte[] bytes = Encoding.ASCII.GetBytes(tag);
            state = Fold(state, (ulong)(uint)bytes.Length);
            for (int i = 0; i < bytes.Length; i += 8)
            {
                int n = Math.Min(8, bytes.Length - i);
                ulong chunk = 0;
                for (int j = 0; j < 8; j++)
                {
                    byte v = j < n ? bytes[i + j] : (byte)0;
                    chunk = unchecked((chunk << 8) | v);   // 大端:首字节落高位
                }
                state = Fold(state, chunk);
            }
            return state;
        }

        private static ulong U(long x) => unchecked((ulong)x);
    }
}
