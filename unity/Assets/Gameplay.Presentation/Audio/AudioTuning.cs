// 权威来源:production/epics/audio-system/story-004-breath-layers-precision-tiers.md(Implementation Notes)
//   · ramp ≥ 50 ms 常量**单处定义**(与 Story 006 交叉淡化共用底座)
//   · INSPIRE_FRACTION(I:E 派生)入 GDD §Tuning Knobs(用户调)
// GDD:design/gdd/audio-system.md F-44.1 注(ramp ≥ 50 ms 硬下界 :312)· §Tuning Knobs(:774 INSPIRE_FRACTION)
// ADR-018 §四 · TR-audio-005
//
// ⚠️ **硬纪律:游戏数值不硬编码** —— 本类承载的全部是**种子值**(story 硬纪律原文),
//    数值最终由用户轮调整;代码只保证「形状 + 单一出处」。
// ⚠️ **RampSeconds 是硬下界**(GDD F-44.1 注):调小它 = 违 AC-44-04(ramp ≥ 50 ms),
//    门侧会红 —— 这里放的是**种子**,不是可以随意拧的自由旋钮。

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>音频运行期常量的**单一出处**(种子值,用户调)。
    /// <para>Story 004 放入 <see cref="RampSeconds"/> 与 <see cref="InspireFraction"/>;
    /// Story 006 交叉淡化**复用同一 <see cref="RampSeconds"/>**,禁在别处另抄 0.05
    /// (两处执行 = 两处分叉,承 ADR-024 §⑥ 同一纪律)。</para></summary>
    public static class AudioTuning
    {
        /// <summary>滤波 / 噪声底 / 交叉淡化的 ramp 时长(秒)—— **≥ 50 ms 硬下界**的种子值。
        /// <para>GDD F-44.1 注 :312「所有滤波 / 噪声底变化须 ramp ≥ 50 ms,否则切换产生
        /// 爆音 / 相位抵消(AC-44-04)」。**与 Story 006 共用底座**(单处定义)。</para>
        /// <para>⚠️ **不可用 <c>TransitionTo(0.05f)</c> 兑现** —— 过渡曲线引擎内部、未文档化
        /// (见 <c>docs/engine-reference/unity/modules/audio.md</c> 「Ramp ≥ 50 ms 的可用依据」);
        /// 实现走 <see cref="FilterRamp"/> 的对数轴自插值 + 逐帧 <c>SetFloat</c>。</para></summary>
        public const float RampSeconds = 0.05f;

        /// <summary><c>INSPIRE_FRACTION</c>(吸气段占完整呼吸周期比例,I:E 派生)—— **种子值 ≈ 1/3**
        /// (静息生理;GDD §Tuning Knobs :774 安全范围 (0,1),用户调)。
        /// <para>相位换算**只准经 <see cref="InspirePhaseMapping"/>"**(编码防线 ①):
        /// 本常量是注入给映射函数的入参,业务代码**不得**自己拿它做乘法。</para></summary>
        public const double InspireFraction = 1.0 / 3.0;
    }
}
