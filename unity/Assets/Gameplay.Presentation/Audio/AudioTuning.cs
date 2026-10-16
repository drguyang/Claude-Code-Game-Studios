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

        /// <summary>`LOOP_EVAL_MAX_TICKS`(循环快照停滞多久 ⇒ 自评判停)—— **种子值 40 tick = 2 s**
        /// (20 Hz ⇒ 50 ms/tick)。
        /// <para>⚠️ **不是自由旋钮,是架构义务的量化**(ADR-001 §一之三 裁决二):该义务要求
        /// 「呈现侧循环终止**必须**自评兜底,不得依赖任何网络保证」,且「二者至少其一,**不得留空**」
        /// —— 本常量就是「不得留空」的 P0 兑现。取 0 / 负值 = 每轮必停(自证下界);
        /// 取过大 = 断线后呼吸层长时间不消失(违「不得出现呼吸层永不停止」的**反面**,
        /// 即停得太晚)。
        /// <para>数值归用户(§Tuning Knobs);本类只保证「有正有界值 + 单一出处」。</para></summary>
        public const int LoopEvalMaxTicks = 40;

        /// <summary>`HANDOVER_MS`(贴耳交接窗 —— 世界层压出 / 听诊层淡入的交叉时长)——
        /// **种子值 10 tick = 500 ms**(20 Hz ⇒ 50 ms/tick)。
        /// <para>GDD §Tuning Knobs :777 安全范围 = **≥ ramp 下界 50 ms**(即 ≥ 1 tick);
        /// 调太高 = 交接拖沓 / 呼吸断档感,调太低 = 近似硬切(爆音 / 双呼吸)。
        /// 本值取 10 tick 是**听觉上可辨但不拖沓**的种子(用户调)。</para>
        /// <para>⚠️ **不是自由旋钮**:`≤ 0` 会被 <see cref="HandoverMux"/> 构造期**结构性拒绝**
        /// (硬切违 GDD :562「禁『啪』一声切换」)。</para></summary>
        public const int HandoverTicks = 10;

        /// <summary>`MUSIC_XFADE_MIN_MS`(战斗乐层切换淡变**下限**,G1 护栏的数值载体)——
        /// **种子值 2000 ms**。
        /// <para>GDD §Tuning Knobs :778 安全范围 = **「≥ 数秒级(待调)」**;调太高 = 遭遇切换糊,
        /// 调太低 = 硬切违 G1(音乐例外的成立前提即此下界足够长,故调太低会使第 5 类例外
        /// **不成立**)。种子 2000 ms 落在「数秒级」内,且**留 2× 余量**给种子乐层行
        /// (`assets/data/audio_events.json` 的 `Music_Encounter_Layer.xfade_ms = 4000`)
        /// —— 门不会误红。</para>
        /// <para>⚠️ **owner = 44 本表**(AC-44-18③;25 侧同名引用,`combat-and-weapon-lines.md:1729`
        /// G1 行)。**本常量是下界的单一出处** —— `AudioEventTableGates.ValidateXfade`
        /// **引用**它,禁在门内另抄一份(两处执行 = 两处分叉,承 ADR-024 §⑥)。</para>
        /// <para>⚠️ **不是自由旋钮**:`xfade_ms < 本值` 会被构建期门**拒收**(AC-44-18①
        /// 「构建期数值比较,超界 = 失败」)。</para></summary>
        public const int MusicXfadeMinMs = 2000;

        /// <summary>`AUDIO_BUDGET_MS`(DSP 预算探针 P95 上限,60 s 采样窗)——
        /// **种子值 2.5 ms**(≈ 平面帧预算 16.6 ms 的 15%)。
        /// <para>GDD §Tuning Knobs :759 安全范围 = **「≤ 帧预算 15% 类(16.6 / 11.1 ms 为参考上界)」**;
        /// 调太高 = 音频超预算不被发现,调太低 = 探针过严(正常抖动即红)。</para>
        /// <para>⚠️ **不是运行期门** —— 本常量是**测量基线**,判据由
        /// <see cref="AudioBudgetProbe"/> 的纯谓词承载,实测 P95 归 evidence 文档
        /// (60 s 窗采样须真 GPU / 真音频回调,EditMode 无 DSP 图,不可在此断言)。</para></summary>
        public const double AudioBudgetMs = 2.5;
    }
}
