// diagnosis-system Story 005 —— 读数状态机(S-8.2 四态转移 + 两窗口托底)。
//
// 权威来源:
//   GDD design/gdd/diagnosis-system.md §S-8.2(读数四态 · 转移图 · 两窗口)
//     —— 转移图:待查─查─►阳性|阴性;阳性|阴性─STALE_WINDOW 或 threshold_transition─►旧;
//        旧─查─►新读数;任意─37 关病例─►清除
//   AC-8-24(恰四态无第四态)· AC-8-27(旧转移幂等,不重复提示)·
//   AC-8-22(已查读不出 = 阴性形态,不改口未查)· AC-8-31(无法配合 ⇒ 未查;等级不锁通道)·
//   AC-8-47(关病例全状态清除)· S-8.2 窗口托底(两 tick 由 39 外部传入,8 不存)
//   ADR-009(「旧」由 9 的 threshold_transition 触发 —— 事件为真源,8 只订阅)
//   Control Manifest:Forbidden「8 自己存两个 tick 值(归 39)」
//
// ⚠️ **纯规则零持久状态(铁律③)**:本类型静态、零字段;读数状态与两个 tick 值的
//    **持久化全在 39**(casebook epic,Ready 未实现 ⇒ 测试侧 fake 承接),8 只持转移规则。
//    所有 tick / 窗口值均为**入参**(数值归用户 —— Guardrail:以合成 tick 数注入)。
// ⚠️ **四态面 vs 五值字母表(AC-8-24 的两半)**:GDD 四态 = 待查/阳性/阴性/旧;
//    story 003 的 `SignReadState` 五值另置 `UnreadableNegative`(数据可分面,AC-8-22)。
//    本文件的 <see cref="FormOf"/> 把五值投影到四形态 —— 两个要求在测试中分别断言。
// ⚠️ **单一完成入口(2026-10-07 评审 S-1 修复)**:可得性判定与窗口规则**在同一路径强制
//    串联** —— 此前 GateChannel 的可得分支可绕过 RECHECK 抑制,两条 BLOCKING AC 互为可击穿面。

using System;

namespace DaYiJingCheng.Gameplay.Presentation.Diagnosis
{
    /// <summary>读数的**呈现形态**面(AC-8-24「体征行恰四态」)。
    /// <para>与五值数据字母表 <see cref="SignReadState"/> 的关系:
    /// <c>UnreadableNegative</c> 投影到 <see cref="Negative"/>(呈阴性形态,AC-8-22),
    /// 但数据面仍是独立值(与真阴性、与待查两两可分)。</para></summary>
    public enum ReadingForm : byte
    {
        /// <summary>待查(空行)。</summary>
        Pending = 0,

        /// <summary>阳性形态。</summary>
        Positive = 1,

        /// <summary>阴性形态(含「已查读不出」的数据变体)。</summary>
        Negative = 2,

        /// <summary>旧(过期 / 状态转移)。</summary>
        Stale = 3,
    }

    /// <summary>一次读数转移的结果(幂等与 cue 门的判据载体)。</summary>
    public readonly struct ReadingTransition
    {
        /// <summary>转移后的读数状态。</summary>
        public readonly SignReadState State;

        /// <summary>状态是否真的变化(音效 / cue 的唯一触发门 —— AC-8-27:
        /// 幂等重复 ⇒ false ⇒ 无任何重复提示)。</summary>
        public readonly bool Changed;

        /// <summary>本调用是否**新**应用了「旧」(幂等计数:三次 transition 恰一次 true)。</summary>
        public readonly bool StaleApplied;

        /// <summary>本调用是否产出了新读数(false = 被 RECHECK_WINDOW 抑制 / 通道不可得)。</summary>
        public readonly bool ProducedNewOutcome;

        public ReadingTransition(SignReadState state, bool changed,
                                 bool staleApplied, bool producedNewOutcome)
        {
            State = state;
            Changed = changed;
            StaleApplied = staleApplied;
            ProducedNewOutcome = producedNewOutcome;
        }

        /// <summary>无变化捷径。</summary>
        internal static ReadingTransition Unchanged(SignReadState state)
            => new ReadingTransition(state, false, false, false);
    }

    /// <summary>读数状态机(S-8.2)—— **纯静态零字段**(AC-8-3 / 铁律③)。
    /// <para>三条输入路径:查体完成(单一入口,含通道可得性 + 两窗口)/
    /// 9 的 threshold_transition / 关病例清除。</para></summary>
    public static class DiagnosisReadingFsm
    {
        /// <summary>已查且非旧(可被「旧」转移命中的集合:{阳性, 阴性, 已查读不出})。</summary>
        private static bool IsSettled(SignReadState s)
            => s == SignReadState.Positive || s == SignReadState.Negative
            || s == SignReadState.UnreadableNegative;

        /// <summary>五值数据字母表 → 四呈现形态(AC-8-24;恰四态的投影)。</summary>
        /// <param name="state">story 003 数据面状态。</param>
        /// <returns>四形态之一。</returns>
        public static ReadingForm FormOf(SignReadState state) => state switch
        {
            SignReadState.Blank => ReadingForm.Pending,
            SignReadState.Positive => ReadingForm.Positive,
            SignReadState.Negative => ReadingForm.Negative,
            SignReadState.UnreadableNegative => ReadingForm.Negative, // 呈阴性形态(AC-8-22)
            SignReadState.Stale => ReadingForm.Stale,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "字母表外值"),
        };

        /// <summary>
        /// <b>查体完成(单一完成入口)</b> —— 可得性判定与窗口规则**同路径强制串联**:
        /// ① 通道不可得 ⇒ 强制空行(AC-8-31,先于一切窗口与读数);
        /// ② 可得且距上次重查 ≤ <paramref name="recheckWindow"/> ⇒ 抑制,不产新读数(S-8.2);
        /// ③ 否则以 <paramref name="outcome"/> 物化新读数。
        /// <para><b>RECHECK 抑制</b>仅命中已查非旧态:
        /// <see cref="SignReadState.Blank"/>(首次查无上次读数)与 <see cref="SignReadState.Stale"/>
        /// 不受抑制 —— 转移图「旧─查─►新读数」优先于抑制,否则读数被打旧后永不可刷新。
        /// ⚠️ 该语义裁定为卡文未细分状态处的补白(评审 S-1 登记)。</para>
        /// <para><b>⚠️ S-2 语义裁定(登记)</b>:通道由可得翻不可得 ⇒ **回溯抹除已定读数**
        /// (卡 AC-8-31「该通道恒空行」字面;与快照冻结的张力已登记卡 Completion Notes,
        /// 39 接线前可改判)。</para>
        /// </summary>
        /// <param name="current">当前读数状态(39 持有,外部传入)。</param>
        /// <param name="outcome">本帧求值结果(story 003 求值器的输出)。</param>
        /// <param name="channelAccessible">通道是否可得(9/13 整数标志;**等级不参与**,
        /// 签名无 Skill 参数 —— AC-8-31 / AC-8-9 两向互证)。</param>
        /// <param name="completionTick">动作**完成**那一 tick(AC-8-48,非开始时刻)。</param>
        /// <param name="lastRecheckTick">上次重查 tick(39 查询接口;<c>null</c> = 从未查过)。</param>
        /// <param name="recheckWindow">复查抑制窗口(tick;值归用户,合成注入)。</param>
        /// <returns>转移结果。</returns>
        public static ReadingTransition OnExaminationCompleted(
            SignReadState current, in DiagnosisReadFloorEvaluator.Outcome outcome,
            bool channelAccessible,
            long completionTick, long? lastRecheckTick, int recheckWindow)
        {
            // ① 通道不可得 ⇒ 恒空行(AC-8-31;先于窗口与读数 —— 无法配合无「完成」可言)
            if (!channelAccessible)
            {
                var cleared = SignReadState.Blank;
                return new ReadingTransition(cleared, cleared != current, false, false);
            }

            // ② 窗口内复查抑制(仅已查非旧;Blank/Stale 不命中 —— 见 doc 裁定)
            if (lastRecheckTick.HasValue && IsSettled(current)
                && completionTick - lastRecheckTick.Value <= recheckWindow)
            {
                return ReadingTransition.Unchanged(current);
            }

            // ③ 物化新读数
            var next = outcome.State;
            return new ReadingTransition(next, next != current, false, true);
        }

        /// <summary>
        /// <b>9 的 threshold_transition 投递</b>(ADR-009:事件为真源,8 只订阅)。
        /// <para><b>幂等(AC-8-27)</b>:已「旧」再收 transition ⇒ 状态不变、
        /// <c>Changed</c> = false(cue 门不触发)、<c>StaleApplied</c> = false(计数不涨)——
        /// 连续 3 个 transition 恰一次 <c>StaleApplied</c>。</para>
        /// <para><see cref="SignReadState.Blank"/>(没查过)不参与 —— 转移图「阳性|阴性─►旧」。</para>
        /// <para><b>⚠️ S-3 登记</b>:幂等机制 = **状态判重**(当前态即幂等,
        /// 优于 Implementation Note 3 的「(patient,旧,新,tick) 当帧集」—— 零持久集、
        /// 利于重放;Note 3 的「需持久化则登记评审点」触发条件就此消解;
        /// 代价 = 不具备「排除早于当前读数的旧 transition」的排序判别,归 39 接线时复评)。</para>
        /// </summary>
        /// <param name="current">当前读数状态。</param>
        /// <returns>转移结果。</returns>
        public static ReadingTransition OnThresholdTransition(SignReadState current)
        {
            if (current == SignReadState.Stale)
                return ReadingTransition.Unchanged(SignReadState.Stale); // 幂等:不叠加
            if (!IsSettled(current))
                return ReadingTransition.Unchanged(current);            // Blank 无旧可言
            return new ReadingTransition(SignReadState.Stale, true, true, false);
        }

        /// <summary>
        /// <b>两窗口托底</b>(S-8.2;8 持规则,两个 tick 由 39 查询接口外部传入 —— 8 不存)。
        /// <para>· <b>RECHECK 托底</b>:当前 − 上次重查 &gt; <paramref name="recheckWindow"/> ⇒ 旧;
        /// · <b>STALE 托底</b>:当前 − 快照 &gt; <paramref name="staleWindow"/> ⇒ 旧。</para>
        /// <para>⚠️ 边界口径(含/不含 = <c>&gt;</c>)卡文只给了 RECHECK 的字面 <c>&gt;</c>,
        /// STALE 同形实现;<b>两窗口数值与边界细则归用户数值轮</b>(Guardrail)。</para>
        /// <para>同帧两窗皆到期 ⇒ 只标一次旧(<c>StaleApplied</c> 单次)。</para>
        /// </summary>
        /// <param name="current">当前读数状态。</param>
        /// <param name="currentTick">当前逻辑 tick。</param>
        /// <param name="snapshotTick">快照采样 tick(<c>null</c> = 无快照,持续型跟 tick 不过期)。</param>
        /// <param name="lastRecheckTick">上次重查 tick(<c>null</c> = 从未查)。</param>
        /// <param name="staleWindow">快照有效期窗口(tick;合成注入)。</param>
        /// <param name="recheckWindow">重查窗口(tick;合成注入)。</param>
        /// <returns>转移结果。</returns>
        public static ReadingTransition EvaluateWindows(
            SignReadState current, long currentTick,
            long? snapshotTick, long? lastRecheckTick,
            int staleWindow, int recheckWindow)
        {
            if (!IsSettled(current))
                return ReadingTransition.Unchanged(current); // 已旧 / 待查:无事

            bool recheckExpired = lastRecheckTick.HasValue
                && currentTick - lastRecheckTick.Value > recheckWindow;
            bool staleExpired = snapshotTick.HasValue
                && currentTick - snapshotTick.Value > staleWindow;

            if (!recheckExpired && !staleExpired)
                return ReadingTransition.Unchanged(current);

            return new ReadingTransition(SignReadState.Stale, true, true, false);
        }

        /// <summary>
        /// <b>关病例清除</b>(AC-8-47):任意读数状态 ⇒ 全状态清除(回待查)。
        /// </summary>
        /// <param name="current">当前读数状态(任意)。</param>
        /// <returns><see cref="SignReadState.Blank"/>。</returns>
        public static SignReadState OnCaseClosed(SignReadState current) => SignReadState.Blank;
    }
}
