// 权威来源:production/epics/audio-system/story-011-settings-exposure-reset.md(注册表 AC① / AC-44-13)
// GDD:design/gdd/audio-system.md §UI Requirements 设置暴露面注册表(:896-909)
//   · 「类型 / 区间 | float 0–1(dB -80–0)」—— **玩家面 = 线性,混音器面 = dB**,两域之间的换算
//     在本类收口(编码防线:禁各调用点自行 Math.Log10)。
// ADR-018 §三(七总线)· ADR-024 §⑥(单一出处:同一条换算两处实现 = 两处分叉)
//
// ⚠️ **为何要这个类**:GDD 的注册表行**同时**写了两套量纲(「float 0–1」= 玩家面滑杆值,
//    「dB -80–0」= mixer SetFloat 面)。若把两域混用,后果是**静默**的:玩家滑杆推 0.5 到
//    mixer 会被当作 -0.5 dB(几乎满音量),而玩家以为推到了一半。故换算**必须**单点收口,
//    且下界钳位(线性 0 ⇒ dB 负无穷,须钳到 GDD 声明的 -80)。
// ⚠️ **本类不做平滑 / 不做 ramp** —— ramp 归 FilterRamp / VolumeRamp(≥ 50 ms 硬下界,
//    AC-44-04);本类是**纯函数**换算,零状态。

using System;

namespace DaYiJingCheng.Gameplay.Presentation.Settings
{
    /// <summary>设置值「玩家线性域 ↔ 混音器 dB 域」的唯一换算点(GDD :896-909 两套量纲的桥)。</summary>
    public static class VolumeUnitMapping
    {
        /// <summary>dB 下界(GDD :896-909「dB −80–0」)—— 线性 0 的钳位目标。
        /// <para>⚠️ 取 <c>-80</c> 而非 <c>-Infinity</c>:<c>AudioMixer.SetFloat</c> 收 NaN/±Inf
        /// 的行为未实证(EditMode 下 SetFloat 按名字查表本身就不通,见 MixerRegistry 注),
        /// 而 -80 dB 在听觉上已等同静音,且是**有限值**(可安全落盘 / 可比对)。</para></summary>
        public const double MinDb = -80.0;

        /// <summary>dB 上界(GDD :896-909「dB −80–0」;线性 1.0 = 0 dB = 中性)。</summary>
        public const double MaxDb = 0.0;

        /// <summary>玩家线性值 → 混音器 dB(钳位到 <see cref="MinDb"/>..<see cref="MaxDb"/>)。
        /// <para>公式 = <c>20·log10(linear)</c>(振幅比,非功率比)。<c>linear ≤ 0</c> ⇒ 钳到下界
        /// (不返回 -Inf:见 <see cref="MinDb"/> 注);<c>linear ≥ 1</c> ⇒ 钳到 0 dB(不放大 ——
        /// 玩家滑杆**不是**增益级,「推到底」= 原样)。</para></summary>
        /// <param name="linear">玩家面线性值(GDD 域 0–1;域外值按钳位处理,不抛)。</param>
        public static double LinearToDb(double linear)
        {
            if (double.IsNaN(linear)) return MinDb;
            if (linear <= 0.0) return MinDb;
            if (linear >= 1.0) return MaxDb;

            double db = 20.0 * Math.Log10(linear);
            if (db < MinDb) return MinDb;
            if (db > MaxDb) return MaxDb;
            return db;
        }

        /// <summary>混音器 dB → 玩家线性值(钳位到 <c>0..1</c>)。
        /// <para>公式 = <c>10^(db/20)</c>。用于「从 sidecar 载入的旧值 / 外部注入的 dB 值」
        /// 回到玩家面 —— 本 story 的 P0 路径只用 <see cref="LinearToDb"/>(单向推送),
        /// 本方向为对称性保留并测试(禁留不可达的半个 API)。</para></summary>
        /// <param name="db">混音器面 dB(GDD 域 −80–0;域外值按钳位处理,不抛)。</param>
        public static double DbToLinear(double db)
        {
            if (double.IsNaN(db)) return 0.0;
            if (db <= MinDb) return 0.0;
            if (db >= MaxDb) return 1.0;
            return Math.Pow(10.0, db / 20.0);
        }
    }
}
