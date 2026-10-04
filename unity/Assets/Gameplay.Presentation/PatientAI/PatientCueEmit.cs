// patient-ai Story 001 —— 13 → 44 的**唯一发射桥**(GDD F-13.5 `Emit`)。
//
// 权威来源:
//   GDD design/gdd/patient-ai.md §Formulas F-13.5(Emit)· §Edge Cases 一
//   ADR-018 §一/§四(44 只触发不持状态;无提示音铁律)· AC-13-D2/D3/D4 · AC-44-B1 ①/B3
//
// ⚠️ **本文件刻意独立于 PatientCue.cs**(2026-10-04):
//   b5① 逃逸谓词是**按文件**判的 —— 只要一个文件含 44 契约 token(`AudioCueDto` /
//   `IAudioCueSink`),该文件就**必须且只能**声明 44 前缀命名空间。13 的调度数值面
//   (`PatientCue` / `CueKind` / `CueIntervals`)住 13 命名空间,与 44 token 分隔在两个文件里。
//
// ⚠️ **签名只出现纯基元 + 白名单类型**(AC-44-B3):`patientId` / 格三轴以 int 承载,
//   在**方法体内**装配为 `PatientId` / `Int3` —— 方法体不在入口白名单扫描面内。

using System;
using DaYiJingCheng.Sim.Contracts;   // Int3 / WorldPos / PatientId / IAudioCueSink / AudioCueDto

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    using DaYiJingCheng.Gameplay.PatientAI;   // 44 前缀看不见 13 的 CueKind / PatientCue

    /// <summary>13 → 44 的**唯一发射桥**(F-13.5 `Emit`)。
    /// <para>⚠️ **本类型是 13 侧唯一出现 `IAudioCueSink` / `AudioCueDto` 的类型**,
    /// 故住 44 前缀命名空间(AC-44-B1 ① 逃逸谓词:含 44 契约 token 的文件必须声明 44 前缀)。
    /// 13 的调度数值面(`PatientCue.ClampByte` / `Intensity` / `Phase`)刻意**不**在此 ——
    /// 它们不是 44 的入口(AC-44-B3)。</para>
    /// <para>⚠️ **不携带状态变化语义**(AC-13-D2)—— 无 sting / jingle / 素材切换报状态。
    /// 死亡表现为**呼吸层停止**,无一次性播报(AC-13-D3)。</para></summary>
    public static class PatientCueEmit
    {
        /// <summary>发射一次性 cue(F-13.5 `Emit`;强度走 <see cref="PatientCue.Intensity"/>)。
        /// <para>⚠️ 本方法签名只出现**纯基元 + 白名单类型**:参数 = `IAudioCueSink`(白名单)·
        /// `int`(病人 id / 格三轴 / 语义种类) —— 44 入口白名单(AC-44-B3)。
        /// 13 命名空间类型(`CueKind` / `PatientId` / `Int3`)一律在**体内**装配。</para></summary>
        /// <param name="sink">44 的 cue 界面(只入不出,ADR-018 §一)。</param>
        /// <param name="patientId">病人 id(`PatientId.Value`;体内装配)。</param>
        /// <param name="kind">语义种类(`(int)CueKind`:咳嗽 0 / 呻吟 1)。</param>
        /// <param name="position">`VitalsDto.Position`(**不夹取**;夹取在产物侧)。</param>
        /// <param name="cellX">逻辑格 X(距离衰减归 44,13 不派生 `Tier`)。</param>
        /// <param name="cellY">逻辑格 Y。</param>
        /// <param name="cellZ">逻辑格 Z。</param>
        /// <param name="looped">一次性 = false。</param>
        public static void Emit(IAudioCueSink sink, int patientId, int kind,
                                float position, int cellX, int cellY, int cellZ,
                                bool looped = false)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            var cue = new AudioCueDto(
                cue: kind,
                intensity: PatientCue.Intensity(position),
                cell: new Int3(cellX, cellY, cellZ),
                source: patientId,
                looped: looped);
            sink.Emit(cue);
        }
    }
}
