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
    }
}
