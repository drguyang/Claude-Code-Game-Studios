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

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Sim.Contracts;           // Int3(遮挡缝的双格入参;B3 白名单既有成员)

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

        /// <summary>格边长(**米**)—— 空间化换算用。
        /// <para>🔁 <b>2026-10-29(`TD-audio-035` ① 换源轮)</b>:默认值由 <c>1f</c> 改为 <c>0f</c>。
        /// <c>0</c> = **未供给**(不是「1 米」),组合根据此回落 <c>AudioTuning.LatticeSizePlaceholder</c>
        /// —— 旧默认值 <c>1f</c> 是一份**悄悄生效的 44 自造比例**,与 6 的产物同值纯属巧合。</para>
        /// <para>生产赋值点 = `BootRoot`(取 <c>MinimalWorldFactory.Create(…, out latticeSizeMm)</c>
        /// 的产物值,经 <see cref="ApplyLatticeFromWorldGeometry"/> 换算)。</para></summary>
        public float LatticeSize;

        /// <summary>毫米 → 米(**唯一换算处**;48 的边界量在 6 侧是整数毫米,44 侧要米)。
        /// <para>⚠️ 为什么换算住这里而不是 `LatticeToWorld`:`LatticeToWorld` 的契约是「格 → 世界坐标」,
        /// 它的 <c>latticeSize</c> 入参**已经是米**;单位换算是**数据边界**的事,住装配层。
        /// 两处各写一次 `/ 1000f` = 第二真源。</para></summary>
        /// <param name="latticeSizeMm">格边长(整数毫米;须 <c>&gt; 0</c>)。</param>
        /// <returns>格边长(米)。</returns>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="latticeSizeMm"/> ≤ 0。</exception>
        public static float MetersFromMillimetres(int latticeSizeMm)
        {
            if (latticeSizeMm <= 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(latticeSizeMm), latticeSizeMm,
                    "LATTICE_SIZE 须 > 0(毫米)—— 0 会让全部格塌到原点,负值镜像世界;" +
                    "GDD F-6-1 的右端为 0 时不等式恒假但无人求值 ⇒ 静默失效");
            }
            return latticeSizeMm / 1000f;
        }

        /// <summary>把 6 的世界几何产物声明的格边长写进缝(生产唯一写入点 = `BootRoot`)。
        /// <para><paramref name="seams"/> 为 <c>null</c>(预载失败 ⇒ 缝不接)⇒ **空操作**
        /// —— 与「缝不接 ⇒ `NullRenderTarget` 链」同一处置,不是静默降级:
        /// 那条路径上 `AudioRenderAssembled = false` 本就可观测。</para></summary>
        /// <param name="seams">音频装配缝(可为 null)。</param>
        /// <param name="latticeSizeMm">6 的 `LATTICE_SIZE`(整数毫米)。</param>
        public static void ApplyLatticeFromWorldGeometry(AudioAssemblySeams seams, int latticeSizeMm)
        {
            if (seams == null) return;
            seams.LatticeSize = MetersFromMillimetres(latticeSizeMm);
        }

        /// <summary>玩家总线音量初始化的**结果**(A-5):`ApplyDefaults` 的返回错误列表。
        /// <para>⚠️ <b>为什么把它带出来</b> —— 这是「启动序**真的**跑了 <c>ApplyDefaults</c>」的
        /// 唯一可观测面。**值断言不可靠**:`AudioMixer.SetFloat` 在 EditMode 下**不生效**
        /// (2026-10-18 实测:写 −80 后 `GetFloat` 仍读回 0 ⇒ 无法用「先破坏再复原」造判别力),
        /// 而资产初值恰等于出厂默认(0 dB)⇒ 删掉该调用时值断言**照样绿**(假绿)。
        /// 本字段非 null ⇒ 该调用执行过;空 ⇒ 执行且成功(非空时 `BuildAudioSeams` 已 fail-loud)。</para></summary>
        public IReadOnlyList<string> BusVolumeErrors;

        /// <summary>是否至少装配了渲染面(clip 或总线其一)。全 null ⇒ 视为「未装配」。</summary>
        public bool HasAny => Clips != null || Buses != null;

        /// <summary>遮挡判定缝:**(听者格, 声源格) → 是否被挡**。
        /// <para>🔁 2026-10-29(`TD-audio-035` ②,用户裁定甲案)。生产赋值点 = `BootRoot`
        /// (把 `LatticeOcclusionProbe.IsBlocked` 绑上世界几何);null = 未接遮挡链
        /// (62 处 EditMode 夹具直调 `Assemble` 时即如此)。</para>
        /// <para>⚠️ 委托形状 = `Func&lt;Int3, Int3, bool&gt;`,**不含任何 Sim 类型** ——
        /// 44 的入口白名单(AC-44-B3)禁 `Sim` / `Sim.Codec` 类型进公开签名,`Int3` 住
        /// `Sim.Contracts` 且是白名单既有成员。</para></summary>
        public Func<Int3, Int3, bool> OccludedFor;

        /// <summary>听者当前格的读取缝(相机格)。null ⇒ 44 以**声源格自身**为听者格
        /// (零长视线 ⇒ 恒不遮挡,等价旧行为,且不伪造听者位置)。</summary>
        public Func<Int3> ListenerCell;

        /// <summary>把世界几何绑成遮挡缝(生产唯一写入点 = `BootRoot`)。
        /// <para><paramref name="seams"/> 为 null(预载失败 ⇒ 缝不接)⇒ **空操作**,与
        /// 「缝不接 ⇒ `NullRenderTarget` 链」同一处置。</para></summary>
        /// <param name="seams">音频装配缝(可为 null)。</param>
        /// <param name="geometry">世界几何(null ⇒ 不绑,遮挡链保持未接)。</param>
        public static void BindOcclusion(AudioAssemblySeams seams,
                                         DaYiJingCheng.Sim.World.WorldGeometry geometry)
        {
            if (seams == null) return;
            if (geometry.SizeX <= 0 || geometry.SizeY <= 0 || geometry.SizeZ <= 0) return;
            seams.OccludedFor = (from, to) => LatticeOcclusionProbe.IsBlocked(geometry, from, to);
        }

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
