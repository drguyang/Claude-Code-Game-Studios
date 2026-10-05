// patient-ai Story 003 —— cue 调度 + 呼吸层生命周期(死亡表现 = 呼吸停止,非一次性播报)。
//
// 权威来源:
//   GDD design/gdd/patient-ai.md §Formulas F-13.5(Emit / CueInterval 分段常函数 / 相位)
//   ADR-018 §一/§四(44 只触发不持状态;无提示音铁律)· §六(禁 sting / jingle 报状态)
//   TR-patient-018/019 · AC-13-D2/D3/D4/D5/D6 · AC-44-09 同一白名单
//
// ⚠️ **本文件住 13 命名空间,且刻意不含 44 契约 token**(AC-44-B1 ① 逃逸谓词是按**文件**判的):
//   本调度器只产出「该不该发 / 发什么 kind / 强度几何」——**发射动作**由
//   `DaYiJingCheng.Gameplay.Presentation.Audio.PatientCueEmit`(44 前缀)完成。
//   ⇒ 两个关注点分居两文件:调度(13)与发射(44)。
//
// ⚠️ **终局 = 呼吸层停止,不是播报**(AC-13-D3)—— 这是本文件最承重的一条:
//   `TerminalFlag.Latched`(痊愈或死亡**皆**置位)的表现是 **`EndLoop` 掉呼吸层 + 姿态落最静止档**,
//   **零一次性音效**(无 sting / jingle / 素材切换)。终局这件事**不被播报**,只**消失**。
//   ⚠️ **`Terminal` 是锁存位,不是 `BehaviorState` 值** —— `BehaviorState` 只有
//   Idle / Seeking / Bedridden 三值(GDD §States 一-ter)。**昏迷(`Bedridden`)呼吸层仍在**,
//   那是昏迷与死亡的分界(AC-13-D3 / TR-019)。
//   理由(ADR-018 §六 无提示音铁律 + GDD §UI Requirements):提示音会让玩家
//   把「听到声音」当作状态播报源 —— 而本作要求状态只经**拟物通道**呈现。
//
// ⚠️ **零 PRNG**(AC-13-D5):相位由 `PatientCue.Phase`(hash32)派生。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.PatientAI
{
    /// <summary>呼吸层的生命周期动作(13 侧决策;**执行归 44**)。
    /// <para>⚠️ **只有三种** —— 没有「播报死亡」这一种,那是被禁的设计(AC-13-D3)。</para></summary>
    public enum BreathAction
    {
        /// <summary>不动作(稳态)。</summary>
        None = 0,
        /// <summary>启动 / 维持呼吸层循环(`BeginLoop`)。</summary>
        Begin = 1,
        /// <summary>停止呼吸层(`EndLoop`)—— **死亡 / 昏迷的表现就是这个**(AC-13-D3)。</summary>
        End = 2
    }

    /// <summary>单病人的 cue 调度决策(纯值 —— 13 产出,44 消费)。</summary>
    public readonly struct CueDispatch
    {
        /// <summary>是否发一次性 cue(咳嗽 / 呻吟)。</summary>
        public readonly bool EmitOneShot;
        /// <summary>一次性 cue 的语义种类(仅当 <see cref="EmitOneShot"/> 为真时有意义)。</summary>
        public readonly CueKind Kind;
        /// <summary>强度 `[0,255]`(已产物侧夹取 —— AC-13-D4)。</summary>
        public readonly byte Intensity;
        /// <summary>呼吸层动作。</summary>
        public readonly BreathAction Breath;
        /// <summary>姿态档(**最静止档 = 0**;`TerminalFlag.Latched` ⇒ 恒 0)。
        /// <para>⚠️ AC-13-D3「终局 = 呼吸层停止 + **姿态落最静止档**」的**承载场** ——
        /// 半个判据若无字段承载,断言就只能断言「没有的东西确实没有」(恒真)。</para>
        /// <para>⚠️ 这是 13 侧决策值;**姿态的呈现映射归 42**(13 只发档位,不渲染)。</para></summary>
        public readonly int PostureTier;

        public CueDispatch(bool emitOneShot, CueKind kind, byte intensity, BreathAction breath, int postureTier)
        {
            EmitOneShot = emitOneShot; Kind = kind; Intensity = intensity;
            Breath = breath; PostureTier = postureTier;
        }

        /// <summary>静默调度(不发声、不动呼吸层、姿态落最静止档)。</summary>
        public static CueDispatch Silent => new CueDispatch(false, CueKind.Cough, 0, BreathAction.None, 0);
    }

    /// <summary>cue 调度器(Story 003)—— 由「本 tick 该不该发」到「发什么」。
    /// <para>⚠️ **不持呼吸层句柄** —— 句柄是 44 的资源(ADR-018 §一);本类型只产出
    /// <see cref="BreathAction"/>,**唯一持有者(上游)负责收尾**。</para></summary>
    public static class PatientCueSchedule
    {
        /// <summary>呼吸层是否应当存在(**唯一的死亡判据**)。
        /// <para>⚠️ **判据 = `TerminalFlag.Latched`,不是 `BehaviorState`** —— `Terminal` 是
        /// **锁存位,不是状态**(GDD §States 一-ter;`BehaviorState` 只有 Idle / Seeking / Bedridden
        /// 三值)。**痊愈与死亡都置位**,故本判据同时覆盖两者:两者皆 = 呼吸层停止
        /// (GDD §Edge Cases / AC-13-D3:13 **不向玩家报告**是哪一种)。
        /// 表现上痊愈与死亡靠**其它拟物通道**区分,不靠音效。</para>
        /// <para>⚠️ **昏迷(`Bedridden`)呼吸层仍在(弱)** —— 这正是昏迷与死亡的分界
        /// (AC-13-D3 / TR-019):昏迷 = 呼吸层弱而不断;死亡 = 呼吸层消失。</para></summary>
        public static bool BreathShouldLive(in TerminalFlag terminal)
            => !terminal.Latched;

        /// <summary>本 tick 的调度决策。
        /// <para>⚠️ **终局(`TerminalFlag.Latched`)恒静默**(一次性 cue 也停)—— 不给「临终一声」,
        /// 那仍是播报(AC-13-D3)。</para></summary>
        /// <param name="behavior">行为态。</param>
        /// <param name="terminal">终局锁存位(**唯一死亡判据**;`TerminalFlag.None` = 未锁存)。</param>
        /// <param name="tick">当前 tick(F-13.5 的调度输入)。</param>
        /// <param name="lastEmitTick">上次发射 tick(`-1` = 从未)。</param>
        /// <param name="id">病人 id(相位派生)。</param>
        /// <param name="tier">症状档(F-13.5 分段常函数)。</param>
        /// <param name="position">`VitalsDto.Position`(**不夹取**;强度在产物侧夹)。</param>
        /// <param name="intervals">cue 间隔表。</param>
        /// <param name="breathAlive">呼吸层当前是否活着(由调用方按 44 侧句柄跟踪)。</param>
        /// <param name="kind">本病人该发的语义种类(咳嗽 / 呻吟;调度器不选素材)。</param>
        /// <param name="entryTick">本病人**进入在场**的 tick(调用方给)—— 首拍相位的纪元。
        /// <para>⚠️ **相位是相对入场时刻的偏移,不是绝对 tick**(F-13.5 ③)。</para></param>
        public static CueDispatch Decide(
            BehaviorState behavior, in TerminalFlag terminal, int tick, int lastEmitTick,
            PatientId id, SymptomTier tier, float position,
            in CueIntervals intervals, bool breathAlive, CueKind kind,
            int entryTick)
        {
            bool shouldLive = BreathShouldLive(terminal);
            BreathAction breath = shouldLive
                ? (breathAlive ? BreathAction.None : BreathAction.Begin)
                : (breathAlive ? BreathAction.End : BreathAction.None);

            // ⚠️ 终局:呼吸层收尾,**不发一次性 cue**,**姿态落最静止档**(无临终播报,AC-13-D3)。
            // [MUT-B]
            if (!shouldLive)
                return new CueDispatch(false, kind, 0, breath, postureTier: 0);

            int interval = intervals.For(tier);
            if (interval <= 0) return new CueDispatch(false, kind, 0, breath, PostureTier(behavior));   // 非法表 ⇒ 静默(表校验归 CueIntervals.Validate)

            int phase = PatientCue.Phase(id, tier, intervals);
            // 首拍 = **入场 tick + 相位**;此后按 interval 推进。
            // ⚠️ **相位是相对偏移,不是绝对 tick**(承 F-13.5 ③「错开多人」)。
            // 若写 `firstDue = phase`(相位即绝对 tick):`phase ∈ [0, interval)` 而真实
            // `tick` 是自世界诞生起的单调量(t 远大于 interval),则任意 `tick >= phase`
            // **恒真** ⇒ **所有病人首拍在同一 tick 齐发**,去同步彻底失效(TC-5 / F-13.5 ③
            // 存在的唯一理由被架空)。若写 `tick + phase`,相位随 `tick` 线性漂移 ——
            // 首拍被推到永远够不着的将来(饿死)。**唯一同时满足「入场后 interval 内必到」
            // 与「同档异 id 首拍错开」的形式 = `entryTick + phase`。**
            int firstDue = (lastEmitTick < 0) ? entryTick + phase : lastEmitTick + interval;
            bool due = tick >= firstDue;

            return new CueDispatch(
                emitOneShot: due,
                kind: kind,
                intensity: PatientCue.Intensity(position),   // 产物侧夹取(AC-13-D4)
                breath: breath,
                postureTier: PostureTier(behavior));
        }

        /// <summary>姿态档(未终局)—— `Bedridden`(昏迷)是最静止的活动态。
        /// <para>⚠️ 终局恒 0 由 <see cref="Decide"/> 直接给,不经本函数。</para></summary>
        private static int PostureTier(BehaviorState behavior)
            => behavior == BehaviorState.Bedridden ? 0 : 1;
    }
}
