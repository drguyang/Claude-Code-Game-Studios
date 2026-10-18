// 权威来源:production/epics/audio-system/story-018-runtime-audio-assembly.md(AC-018-1)
//   · A-2「`ICueRenderTarget` 只有 `NullRenderTarget`」—— 本类即真实现。
// ADR-028 ①(世界语境声源池**唯一所有者 = 44**)· ⑤(cue 驱动生命周期)
// ADR-018 §一(44 只渲染,不持游戏状态)· §二(`AudioCueDto` 唯一入口)
// GDD design/gdd/audio-system.md F-44.7(:619 同格 N 病人 = N 条独立声源,不合并)
//
// ⚠️ **本类不持句柄簿记** —— 句柄唯一性 / 同源替换 / 过期忽略全归 `CueRenderSink`
//   (它是 `IAudioCueSink` 的语义实现)。本类只做「44 的渲染动作 → 引擎对象」的翻译:
//   `PlayOneShot` → 池取源 + 载 clip + `PlayOneShot`;
//   `StartLoop` / `StopLoop` → 池取源 + `clip` + `Play` / `Stop`。
//   ⇒ 句柄 id 在本类只作**循环表的键**,不产生任何语义。
//
// ⚠️ **clip 解析与播放分离**:clip 解析走**注入的委托** `Func<int, AudioClip>`(素材装载归
//   Addressables 装配面;本类不引 `Addressables`,以便 EditMode 用假件直驱「cue → 池动作」这一半)。
//   ⚠️ **为什么是委托而不是自定义接口**:本命名空间的类型受 **AC-44-B3 入口门**约束 ——
//   `unity/Assets/Tests/EditMode/Audio/assembly_boundary_test.cs::ScanEntryPoints` 断言
//   「44 的类型实现的项目接口 ⊆ {IAudioCueSink} ∪ EntryInterfaceAllowlist」。曾用
//   `IClipProvider` 接口实现,该门**逐条判红**(2026-10-18 实测 3 条);改委托后零门面扩张
//   (委托不是「实现的项目接口」),也不必往白名单里加条目 —— 白名单每加一条都在削弱该门。
//   ⚠️ 解析不到 clip ⇒ **不静默**:记一次 miss 并计数(可观测),但不抛 —— 播放面不能因
//   一条 cue 缺素材炸掉整帧(素材存在性已由构建期 D6 门拦下,运行期 miss = 装配错)。
//
// ⚠️ **强度 / 距离 / 遮挡**不在本类面内:`Intensity` 的增益映射归 `VoiceVariantLib`
//   (13 的语义侧),空间化归 `SpatializationDirector`。本类只保证「有声源、有 clip、在响」。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary><c>ICueRenderTarget</c> 的**生产实现**(A-2):把 cue 渲染动作翻译成
    /// <see cref="AudioSourcePool"/> 上的真实播放。</summary>
    /// <para>⚠️ 只做翻译,零游戏状态(ADR-018 §一);句柄语义全归 <see cref="CueRenderSink"/>。</para>
    /// <example><code>var target = new AudioSourceRenderTarget(pool, cue => clipOf(cue), busForCue);
    /// var sink = new CueRenderSink(target);</code></example>
    public sealed class AudioSourceRenderTarget : ICueRenderTarget
    {
        private readonly AudioSourcePool _pool;
        private readonly Func<int, AudioClip> _clipOf;
        private readonly Func<int, string> _busForCue;

        // 循环句柄 id → 已占用的 actorId。循环 cue 的 actor = 句柄 id 的**负偏移**
        // (一次性 cue 用 cue.Source,循环用独立命名空间 —— 两者不会撞池条目)。
        // ⚠️ 负号是**键空间分隔**,不是语义:池只把 actorId 当字典键。
        private readonly Dictionary<int, int> _loopActors = new Dictionary<int, int>();

        /// <summary>clip 解析失败次数(可观测面;运行期 miss = 装配错,构建期 D6 门本应拦下)。</summary>
        public int ClipMissCount { get; private set; }

        /// <summary>构造。</summary>
        /// <param name="pool">声源池(非 null)。</param>
        /// <param name="clipOf">cue ordinal → clip 的解析缝(null ⇒ **恒 miss** —— 相当于
        /// 「总线路由可验、clip 面未装配」,每次调用记一次 <see cref="ClipMissCount"/>,可观测不静默)。</param>
        /// <param name="busForCue">cue ordinal → bus 名(事件表 `bus` 列;null = 不挂组)。
        /// <para>⚠️ 传函数而不是字典 —— 事件表是**唯一真源**(ADR-024 §⑥),本类不缓存第二份。</para></param>
        /// <exception cref="ArgumentNullException"><paramref name="pool"/> 为 null。</exception>
        public AudioSourceRenderTarget(AudioSourcePool pool, Func<int, AudioClip> clipOf = null,
                                       Func<int, string> busForCue = null)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _clipOf = clipOf;
            _busForCue = busForCue;
        }

        /// <summary>当前在响的循环数。</summary>
        public int ActiveLoopCount => _loopActors.Count;

        /// <inheritdoc/>
        public void PlayOneShot(in AudioCueDto cue)
        {
            AudioClip clip = _clipOf != null ? _clipOf(cue.Cue) : null;
            if (clip == null) { ClipMissCount++; return; }

            // 一次性 cue 的 actor 键 = 声源实体 id(病人 / 玩家);同实体复播复用同一源。
            AudioSource source = _pool.Spawn(cue.Source, cue.Cell.X, cue.Cell.Y, cue.Cell.Z,
                                             cue.Cue, BusOf(cue.Cue));
            source.clip = clip;
            source.loop = false;
            source.Play();
        }

        /// <inheritdoc/>
        public void StartLoop(in AudioCueDto cue, int handleId)
        {
            AudioClip clip = _clipOf != null ? _clipOf(cue.Cue) : null;
            if (clip == null) { ClipMissCount++; return; }

            int actor = LoopActorOf(handleId);
            _loopActors[handleId] = actor;

            AudioSource source = _pool.Spawn(actor, cue.Cell.X, cue.Cell.Y, cue.Cell.Z,
                                             cue.Cue, BusOf(cue.Cue));
            source.clip = clip;
            source.loop = true;
            source.Play();
        }

        /// <inheritdoc/>
        public void StopLoop(int handleId)
        {
            // 过期句柄 ⇒ 空操作(与 CueRenderSink 的「过期 EndLoop 忽略」同口径)。
            if (!_loopActors.TryGetValue(handleId, out int actor)) return;
            _loopActors.Remove(handleId);
            _pool.Release(actor);
        }

        private string BusOf(int cue)
        {
            if (_busForCue == null) return null;
            string bus = _busForCue(cue);
            return string.IsNullOrEmpty(bus) ? null : bus;
        }

        /// <summary>循环句柄 id → 池 actor 键(负偏移 = 与一次性 cue 的实体 id 分居两个键空间)。</summary>
        private static int LoopActorOf(int handleId) => -handleId - 1;
    }
}
