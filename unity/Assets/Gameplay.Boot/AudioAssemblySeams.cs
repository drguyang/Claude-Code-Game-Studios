// 权威来源:production/epics/audio-system/story-018-runtime-audio-assembly.md(AC-018-3)
//   · A-1「`CueRenderSink` 零生产构造点」· A-4「`AudioSourcePool` 零生产构造点」
// ADR-028 ①(声源池唯一所有者 = 44)· ②(零场景预摆)· ADR-018 §一(44 只渲染)
// ADR-025 §①(Gameplay.Boot 可同时看见 Sim.Codec 与 Gameplay.Presentation)
//
// ⚠️ **为什么装配面是「缝」而不是「组合根自建全部」**:
//   素材装载(`AudioClipLibrary` → Addressables)与混音器解析(`AudioBusResolver` → AudioMixer)
//   都**依赖引擎资产**,而组合根被 62 处 EditMode 测试直接调用(无 catalog / 无 mixer)。
//   ⇒ 组合根只建**纯装配件**(池 / 渲染目标 / sink),引擎面资产由调用方经本缝注入:
//   - 不传缝(既有 62 处调用)⇒ sink 走 `NullRenderTarget`(契约语义仍成立,零声音);
//   - 传缝(BootRoot 生产路径)⇒ 真渲染 + 真总线 + 真 clip。
//
// ⚠️ **这不是「静默降级」**:null 缝的可观测面 = `CompositionRootServices.AudioSink` 的
//   `RenderTargetKind`(`"null"` / `"sources"`),启动期日志与测试都可断言 ——
//   「装了没装」不能靠猜。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Audio;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>音频装配的**外部资源缝**(story-018)。全部字段可 null ——
    /// 逐个 null 意味着该面未装配(见文件头注的「不是静默降级」)。</summary>
    public sealed class AudioAssemblySeams
    {
        /// <summary>clip 解析面(生产 = <see cref="AudioClipLibrary"/>;null ⇒ 所有 cue 播不出)。
        /// <para>⚠️ 具型而非接口 —— AC-44-B3 入口门禁止 44 命名空间新增自定义接口
        /// (见 `AudioSourceRenderTarget` 头注:clip 解析缝走委托)。本类住 `Gameplay.Boot`,
        /// 不在该门扫描面内,但**保持同口径**以免读者误以为可以反推一个接口。</para></summary>
        public AudioClipLibrary Clips;

        /// <summary>总线解析面(生产 = <see cref="AudioBusResolver"/>;null ⇒ 源不挂组,落 Master)。</summary>
        public AudioBusResolver Buses;

        /// <summary>事件表本身(cue 名 → ordinal 的解析面;null ⇒ 不做按名解析)。
        /// <para>⚠️ 与 <see cref="Clips"/> **同源**(同一份 cooked 字节解出的表)——
        /// 组合根用它把 GDD 点名的 cue 名(`Voice_Cough_Damp` / `BreathLayer_Base_Calm`)
        /// 解析成 ordinal,而不是在代码里写死 `(int)CueKind`(见 `PatientCueDriver` 头注)。</para></summary>
        public AudioEventDataSet? Table;

        /// <summary>格边长(米)—— 空间化换算用;≤ 0 ⇒ 用 1 m(仅当无空间化需求时合法)。</summary>
        public float LatticeSize = 1f;

        /// <summary>玩家总线音量初始化的**结果**(A-5):`ApplyDefaults` 的返回错误列表。
        /// <para>⚠️ <b>为什么把它带出来</b> —— 这是「启动序**真的**跑了 <c>ApplyDefaults</c>」的
        /// 唯一可观测面。**值断言不可靠**:`AudioMixer.SetFloat` 在 EditMode 下**不生效**
        /// (2026-10-18 实测:写 −80 后 `GetFloat` 仍读回 0 ⇒ 无法用「先破坏再复原」造判别力),
        /// 而资产初值恰等于出厂默认(0 dB)⇒ 删掉该调用时值断言**照样绿**(假绿)。
        /// 本字段非 null ⇒ 该调用执行过;空 ⇒ 执行且成功(非空时 `BuildAudioSeams` 已 fail-loud)。</para></summary>
        public IReadOnlyList<string> BusVolumeErrors;

        /// <summary>是否至少装配了渲染面(clip 或总线其一)。全 null ⇒ 视为「未装配」。</summary>
        public bool HasAny => Clips != null || Buses != null;

        /// <summary>把 cue 名解析成事件表 ordinal(GDD 点名的 cue 才走此路;名字不在表内 ⇒ false)。
        /// <para>⚠️ 只在**装配期**调用一次(P0 表恒小,线性扫描可接受;见
        /// <see cref="AudioEventDataSet.TryGetOrdinal"/> 的注释)。</para></summary>
        /// <param name="cue">cue 名(如 <c>Voice_Cough_Damp</c>)。</param>
        /// <param name="ordinal">出口:命中序号。</param>
        public bool TryResolveCue(string cue, out int ordinal)
        {
            ordinal = AudioEventDataSet.NoOrdinal;
            if (!Table.HasValue) return false;
            return Table.Value.TryGetOrdinal(cue, out ordinal);
        }
    }
}
