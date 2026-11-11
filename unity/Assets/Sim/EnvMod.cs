// ADR-006 / GDD time-and-weather.md §F-5.4 —— EnvMod 的环境分量(加法增量)。
//
// 权威来源:
//   GDD `time-and-weather.md`
//     · F-5.4(:388-410)`EnvMod_raw(cell, t) = g(气候属性(生态区), season_index,
//       Weather(cell,t).kind, Weather(cell,t).intensity, isNight(t))`
//     · **规则六**(:216-246)`EnvMod_total = 24 的医馆分量 + 5 的环境分量` ⇒ **加法域**
//       「**可为负** —— 是结构不是错误」;「**负值必须原样传递**」
//     · **B-3 订正**:5 侧**不钳制**(钳制唯一发生在 21a F1)
//     · 规则二 / `AC-5-12`:玩家不在任何生态区 ⇒ 取**全局默认**(非最近区)
//   `item-database.md` F1 —— `EnvMod_total = clamp(climate + clinic, MIN, MAX)`
//   `clinic-machine.md` —— 24 的中性出口 = `(0, 0)`(EC-24-01/02/07)
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔁 2026-11-11(`TD-processing-09` · 用户裁定「甲 · 加法增量」)—— 本文件改判
// ─────────────────────────────────────────────────────────────────────────────
// **旧实现(已删,原文不删留作记录)**:`ComputeEnvModRaw` 以**乘性** `Fix.One` 为中性,
// 并把五个输入用一条**未被任何文档授权的线性组合**揉在一起:
//   `result = 1 × 生态区基线 × Fix(kind*1000 + intensity) × (夜晚 ? 0.9 : 1) × Fix(1 + season*0.05)`
// 三处问题,逐条可核:
//   ① **量纲错**:F1 的 `EnvMod_total = climate + clinic` 是**相加的增量**(中性 0),
//      接进去会让每炉平白 +1.0 —— 产量约翻倍(债册 `TD-processing-09` 的原文算例)。
//   ② **公式是凭空造的**:`Fix(kind*1000 + intensity)` 的 raw = 1000..~1100,
//      即 Q16.16 下的 **0.015** —— 一个「天气」乘子把产量压到 1.5%。
//      该式在 GDD 里**没有出处**:`g(·)` 的形状是 **`OQ-5-4` 未裁**的旋钮
//      (`time-and-weather.md:522` 逐字「未定」,值归用户)。
//   ③ **中性名骗人**:空输入返回 `Fix.One`,而 24 的中性出口是 `Fix.Zero`
//      ⇒ 两个分量「各自的 0」不是同一个数。
//
// **现实现**:输出 = **加法增量**(中性 `Fix.Zero`,可为负,零钳制)。
// `g(·)` 的五项里,**今天只有一项有权威数据源**:生态区气候属性(6 定义 / 5 消费,
// `world-and-ecozones.md:918`)。其余四项的状态如实登记:
//   · `Weather.kind` / `.intensity` —— **天气无运行期消费方**(ADR-038 §D7 如实登记,
//     `WeatherRoll` 零生产调用点)⇒ 今天取不到值;
//   · `isNight` / `season_index` —— 5 内部可算(`TimeParams`),但它们在 `g(·)` 里的
//     **映射形状**(加多少)属于 `OQ-5-4` 未裁面 ⇒ **不得由实现轮自行补一个系数**。
// ⇒ 本实现 = `g` 的**基线项透传**:`EnvModDelta = 气候属性(生态区)`。
//   ⚠️ 这**不是**「g 已实现」,是「g 的已裁部分 + 其余零项」。`OQ-5-4` 裁下来之前,
//   任何在 `ComputeEnvModDelta` 里给昼夜 / 季节 / 天气凭空乘一个系数的写法,都是**第二真源**。
//
// ⚠️ **5 侧零钳制**(B-3 / AC-5-19):本文件**不引用** `ENV_MOD_MIN/MAX`,也不出现任何
//    clamp。越界是特性:5 的 −0.9 与 24 的 +0.4 合并后由 21a 单点钳制,才是规则六的算例。
//
// ⚠️ **零浮点**(门 A + ADR-006 / AC-5-13):全程 `Fix`,无 `float` / `double` 字面量。

using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// `g(·)` 的入参包(F-5.4 的五项)。
    /// <para>⚠️ <b>字段保留而暂不消费</b>:除 <see cref="EcozoneBaseline"/> 外的四项,
    /// 在 `g(·)` 形状(`OQ-5-4`)裁定前**不得**由实现侧赋予系数 —— 见文件头「现实现」段。
    /// 保留它们是为了让 F-5.4 的入参形状在类型面可见(AC-5-08 的反射面判据要求
    /// 「输出类型里没有速度 / 位移修正」,而**入参**的形状归 F-5.4)。</para>
    /// </summary>
    public readonly struct EnvModInput
    {
        /// <summary>生态区气候属性里的 **EnvMod 分量**(6 定义 / 5 消费;加法增量,中性 0)。</summary>
        public readonly Fix EcozoneBaseline;

        /// <summary>天气种类(`Weather.kind`;今日**无生产取值链**,见文件头)。</summary>
        public readonly int WeatherKind;

        /// <summary>天气强度(`Weather.intensity`;同上)。</summary>
        public readonly int WeatherIntensity;

        /// <summary>是否夜间(F-5.1 的二值;`g(·)` 内的映射形状未裁)。</summary>
        public readonly bool IsNight;

        /// <summary>季节下标(F-5.2;`g(·)` 内的映射形状未裁)。</summary>
        public readonly long SeasonIndex;

        /// <summary>构造。</summary>
        public EnvModInput(Fix ecozoneBaseline, int weatherKind, int weatherIntensity, bool isNight, long seasonIndex)
        {
            EcozoneBaseline = ecozoneBaseline;
            WeatherKind = weatherKind;
            WeatherIntensity = weatherIntensity;
            IsNight = isNight;
            SeasonIndex = seasonIndex;
        }
    }

    /// <summary>
    /// 5 的环境分量 —— **加法增量,中性 0,可为负,零钳制**。
    /// <para>⚠️ 与 <c>Sim.Clinic.EnvMod</c>(24 的医馆分量)是**两个量**:两者的中性都是 0,
    /// 但归属与数据源不同;合并与钳制**只在 21a F1**(AC-18-06 / B-3)。</para>
    /// </summary>
    public static class EnvMod
    {
        /// <summary>中性增量(`Fix.Zero`)—— 与 24 的中性出口 `(0, 0)` 同数。</summary>
        public static Fix Neutral => Fix.Zero;

        /// <summary>
        /// 计算 5 的环境分量(F-5.4 的 `EnvMod_raw`),**加法增量**。
        /// <para>今天 = <see cref="EnvModInput.EcozoneBaseline"/> 的透传(见文件头「现实现」段);
        /// 其余四项等 `OQ-5-4` 裁定后再进 `g(·)`。</para>
        /// </summary>
        /// <param name="input">F-5.4 的入参。</param>
        /// <returns>加法增量(可为负;**不做任何钳制**,B-3 / AC-5-19)。</returns>
        public static Fix ComputeEnvModDelta(in EnvModInput input) => input.EcozoneBaseline;

        /// <summary>验证同输入逐位同输出(AC-5-04 的纯函数面)。</summary>
        public static bool IsDeterministic(in EnvModInput input)
        {
            Fix r1 = ComputeEnvModDelta(input);
            Fix r2 = ComputeEnvModDelta(input);
            return r1.Raw == r2.Raw;
        }
    }
}
