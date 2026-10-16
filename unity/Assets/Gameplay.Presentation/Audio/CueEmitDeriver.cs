// 权威来源:production/epics/audio-system/story-007-net-divergence-remote-derivation.md
//   (AC-44-D7 BLOCKING 静态三腿 —— ① 输入集 ∈ {流事件, 烘焙数据} 纯函数 · ② 时钟面 = 流/tick
//    驱动,禁帧与墙钟 · ③ 载荷面 = DTO 全字段(除注入的 Cell)同纯度)
// GDD:design/gdd/audio-system.md §Edge Cases 联机与空间化(「cue 本身不复制 —— 各客户端从
//   本地流副本 + 表现映射自行派生」)· F-44.6(Intensity 归一整数域)
// ADR-001 §一之二 消费纪律 3(cue 不进任何网络通道,客户端自派生)· ADR-005(客户端持流副本)
//   · ADR-008(跨流全序键)· ADR-012(SplitMix64 = 全案唯一整数哈希源)
//
// ⚠️ **本类是 AC-44-D7 核心判据的被测物**(2026-10-16 S13-9 补评):补评前该判据的
//    「重排差分哈希」被测物是**测试自己写的折叠函数** —— 测的是「我写的哈希对乱序输入稳定」,
//    与「44 的 cue 派生器对乱序流稳定」无关(评审原件 §2.1 判 🔴)。
//    本类落地后,被测物 = 派生器本体。
// ⚠️ **两段式装配的派生半**(Implementation Notes):本类产出 = 是否发出 + CueId/Intensity/Looped;
//    **`Cell` 于空间化时注入**(远端 ← IPositionalChannel,本机 ← 13 的 p.Cell)⇒ 本类产出
//    的 `AudioCueDto.Cell` 恒为 `default`,由 `SpatializationDirector` 族注入。
//    「派生禁读表现态」与「DTO 携带表注入 Cell」由此不冲突(回引 ADR-001 消费纪律 3)。
// ⚠️ **纯函数面**:零 `UnityEngine.Random` / `Time` / `DateTime` / `IPositionalChannel` 引用
//    (由 `AssemblyGates.CheckAudioClockTokens` + `CheckAudioPositionalChannelRefs` 的 IL 扫描
//    硬约束 —— 本类**在扫描面内**,不是旁路)。
// ⚠️ **禁 RNG / 禁墙钟**不是风格问题:各客户端若用本地 RNG 或帧时钟决定「发不发 cue」,
//    会**听到不同的病人**(GDD Edge Cases 硬前提)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>一条**到达的流事件**在 cue 派生面的投影(AC-44-D7 ① 的输入行)。
    /// <para>⚠️ 字段 = 跨流全序键四元组 + cue 载荷三件,全整数域。三轴 / 枚举以**基元 + 契约类型**
    /// 承载(`StreamId` / `PatientId` 住 `Sim.Contracts`,B4 字段白名单放行)。</para>
    /// <para>⚠️ 本类型是**纯数据载体**:不持状态、不进 sim、不经三流 —— 故登记进 44 的
    /// `EntryAllowlist`(与 `RankSource` / `VoiceBucket` 同性质)。</para></summary>
    public readonly struct CueEmitInput
    {
        /// <summary>逻辑 tick(20 Hz)。全序键第 1 分量。</summary>
        public readonly long Tick;
        /// <summary>所属逻辑流。全序键第 2 分量(优先级由成员值派生)。</summary>
        public readonly StreamId Stream;
        /// <summary>病人 / 受伤实体位(`PatientId.None` = -1,世界级事件)。全序键第 3 分量。</summary>
        public readonly int Patient;
        /// <summary>主机 Append 流水号。全序键第 4 分量。</summary>
        public readonly long Seq;

        /// <summary>语义 cue id(音频事件表 ordinal)。</summary>
        public readonly int CueId;
        /// <summary>0–255 归一强度(载荷面,进哈希)。</summary>
        public readonly byte Intensity;
        /// <summary>声源实体 id(`-1` = 环境音)。</summary>
        public readonly int Source;
        /// <summary>循环(呼吸层)/ 一次性(咳嗽)。</summary>
        public readonly bool Looped;

        /// <param name="tick">逻辑 tick。</param>
        /// <param name="stream">所属逻辑流。</param>
        /// <param name="patient">病人位(世界级 = -1)。</param>
        /// <param name="seq">主机流水号。</param>
        /// <param name="cueId">语义 cue id。</param>
        /// <param name="intensity">0–255 强度。</param>
        /// <param name="source">声源实体 id。</param>
        /// <param name="looped">是否循环 cue。</param>
        public CueEmitInput(long tick, StreamId stream, int patient, long seq,
                            int cueId, byte intensity, int source = -1, bool looped = false)
        {
            Tick = tick; Stream = stream; Patient = patient; Seq = seq;
            CueId = cueId; Intensity = intensity; Source = source; Looped = looped;
        }
    }

    /// <summary>cue 发射派生器(AC-44-D7 静态三腿的被测物)。
    /// <para>输入 = 一批**到达序可能被打乱**的流事件(<see cref="CueEmitInput"/>);
    /// 输出 = 经 <see cref="EventOrderKey"/> 全序排定后的 cue 序列(<see cref="AudioCueDto"/> 列表,
    /// `Cell` 恒为 `default` —— 注入归空间化)。</para>
    /// <para>判据 = **同流到达序打乱 ⇒ 派生出的 cue 序列哈希一致** —— 这是「各客户端从本地流副本
    /// 自行派生会听到同一序列」的可测形态(ADR-001 §一之二 消费纪律 3)。</para>
    /// <example>
    /// var cues = CueEmitDeriver.Derive(arrivals);       // 到达序任意
    /// ulong h  = CueEmitDeriver.CanonicalHash(cues);    // 与到达序无关
    /// </example></summary>
    public static class CueEmitDeriver
    {
        /// <summary>哈希域分隔常量(禁零起点 —— 零起点的链式折叠对空集与「首元素为 0」不可区分)。
        /// <para>值 = ASCII `A44DE` + `077B1E5C196` 的十六进制拼合,**非随机**:它是本判据的
        /// 域标签,改动它 = 改所有历史黄金夹具(ADR-012 版本化刷新纪律)。</para></summary>
        public const ulong DomainSeparator = 0xA44DE077B1E5C196UL;

        /// <summary>派生 cue 序列:到达序 → <see cref="EventOrderKey"/> 全序 → cue 行。
        /// <para>⚠️ **排序是判据的承重件** —— 去掉它,同流不同到达序会派生出不同序列
        /// (证伪轮的停用点之一)。</para></summary>
        /// <param name="arrivals">到达序任意的一批流事件投影(null ⇒ <see cref="ArgumentNullException"/>;
        /// 空集 ⇒ 空列表,非 null)。</param>
        /// <returns>按全序排定的 cue 序列(新建列表,调用方持有)。</returns>
        public static IReadOnlyList<AudioCueDto> Derive(IReadOnlyList<CueEmitInput> arrivals)
        {
            if (arrivals == null) throw new ArgumentNullException(nameof(arrivals));

            var keyed = new List<(EventOrderKey Key, CueEmitInput Ev)>(arrivals.Count);
            for (int i = 0; i < arrivals.Count; i++)
            {
                CueEmitInput e = arrivals[i];
                keyed.Add((new EventOrderKey(e.Tick, e.Stream, new PatientId(e.Patient), e.Seq), e));
            }
            keyed.Sort(static (a, b) => a.Key.CompareTo(b.Key));

            var cues = new List<AudioCueDto>(keyed.Count);
            for (int i = 0; i < keyed.Count; i++)
            {
                CueEmitInput e = keyed[i].Ev;
                cues.Add(new AudioCueDto(
                    cue: e.CueId,
                    intensity: e.Intensity,
                    cell: default,          // ⚠️ 注入值 —— 派生面不读位置(两段式装配)
                    source: e.Source,
                    looped: e.Looped));
            }
            return cues;
        }

        /// <summary>派生序列的**规范哈希**(重排差分判据的度量面)。
        /// <para>哈希输入 = 载荷面**全字段**(`Cue` / `Intensity` / `Source` / `Looped`)——
        /// `Cell` 除外,它是注入值而非派生物(AC-44-D7 ③「DTO 全字段(除 Cell)的派生同纯度」)。</para>
        /// <para>⚠️ **四个字段全部进哈希** —— 少折一个(如只折 `Cue` + `Intensity`)会让
        /// 「载荷面同纯度」的断言对那个字段失去判别力(证伪轮的停用点之二)。</para></summary>
        /// <param name="cues">已派生的 cue 序列(null ⇒ <see cref="ArgumentNullException"/>)。</param>
        /// <returns>链式折叠后的 64 位规范哈希。</returns>
        public static ulong CanonicalHash(IReadOnlyList<AudioCueDto> cues)
        {
            if (cues == null) throw new ArgumentNullException(nameof(cues));

            ulong state = SplitMix64.Avalanche(DomainSeparator);
            for (int i = 0; i < cues.Count; i++)
            {
                AudioCueDto c = cues[i];
                state = SplitMix64.Fold(state, (ulong)(uint)c.Cue);
                state = SplitMix64.Fold(state, c.Intensity);
                state = SplitMix64.Fold(state, (ulong)(uint)c.Source);
                state = SplitMix64.Fold(state, c.Looped ? 1UL : 0UL);
            }
            return SplitMix64.Avalanche(state);
        }
    }
}
