// patient-ai Story 001 —— 13 的两个正交状态维(GDD §States and Transitions 一 / 一-bis / 一-ter)。
//
// 权威来源:
//   GDD design/gdd/patient-ai.md §States 一 `BehaviorState`(三值:Idle / Seeking / Bedridden)
//     · §States 一-bis `SessionState`(None / InTreatment,边界层赐予)
//     · §States 一-ter `Terminal`(锁存位,不是状态)
//   ADR-016 §一 补注(13 决策住边界层)· TR-patient-004(不重建九态)
//
// ⚠️ **两维不复用同一组枚举值** —— 这是 GDD R3 重写的根因:原稿 `InTreatment` 既是行为态
//    又是视图枚举,造成歧义。现 `BehaviorState` 与 `SessionState` **物理上是两个枚举类型**。
// ⚠️ `Terminal` **不是状态,是锁存位** —— 单独用一个 struct(bool 对),不混进 `BehaviorState`。

namespace DaYiJingCheng.Gameplay.PatientAI
{
    /// <summary>13 唯一自主的行为状态机 —— 三值,全部是 `(position, trend, prev)` 的函数。
    /// <para>⚠️ **不含 `Waiting` / `InTreatment` / `Terminal`** —— GDD R3:原稿六态表自相矛盾
    /// (三者的进入条件都含 `VitalsDto` 里没有的量)。`Waiting` 被吸收进 `Seeking` 的**空间子相**;
    /// `InTreatment` 归 <see cref="SessionState"/>;`Terminal` 降级为锁存位。</para></summary>
    public enum BehaviorState
    {
        /// <summary>默认态:低频巡游(行为②)。</summary>
        Idle = 0,
        /// <summary>`position ≥ SEEK_MIN ∧ KnowsClinic` —— 走整数导航格 → 医馆(行为①)。</summary>
        Seeking = 1,
        /// <summary>`position ≥ COLLAPSE_MIN` —— 坐倒 / 半卧 / 躺平(行为③⑤);**不产生位移**。</summary>
        Bedridden = 2
    }

    /// <summary>会诊状态 —— **由边界层显式赐予,13 不推断**(GDD 规则十二 / AC-13-B5)。
    /// <para>⚠️ 与 <see cref="BehaviorState"/> **正交** —— 会诊中病人仍可处于任一行为态
    /// (躺床上接受查体的病人:`BehaviorState = Bedridden` ∧ `SessionState = InTreatment`)。</para></summary>
    public enum SessionState
    {
        /// <summary>默认;行为 = `Map()` 的结果。</summary>
        None = 0,
        /// <summary>边界层调 `OnExamSessionChanged(id, true)` 后;**覆盖** `Map()`(行为④)。</summary>
        InTreatment = 1
    }

    /// <summary>`Seeking` 的**空间子相**(GDD §States「求医的两个子相」)—— 不是状态。
    /// <para>切换**不改变行为决策的输入**(都是 `Seeking`),只是同一行为的位置表现。
    /// 把它拆成两个状态会制造一个假的转出条件(「到达」)。</para></summary>
    public enum SeekingPhase
    {
        /// <summary>沿整数导航格走(行为①前半)。</summary>
        EnRoute = 0,
        /// <summary>门口坐下 / 站立等待 + 视线朝玩家(行为①后半,原 `Waiting` 的全部内容)。</summary>
        AtClinic = 1
    }

    /// <summary>终局标志 —— **锁存位,不是状态**(GDD §States 一-ter)。
    /// <para>一旦置位**不再重新求值**(痊愈的病人不会因 `position` 回升而复活);
    /// 痊愈与死亡由**表现**区分(呼吸层 + 姿态),13 **不向玩家报告**。</para></summary>
    public readonly struct TerminalFlag
    {
        /// <summary>终态已置位(锁存)。</summary>
        public readonly bool Latched;
        /// <summary>置位时的成因(`position ≤ RECOVER_MAX` = 痊愈;`≥ DEATH_BAND_MIN` = 死亡)。</summary>
        public readonly TerminalCause Cause;

        public TerminalFlag(bool latched, TerminalCause cause) { Latched = latched; Cause = cause; }

        /// <summary>未置位。</summary>
        public static TerminalFlag None => new TerminalFlag(false, TerminalCause.None);
    }

    /// <summary>终态成因(仅表现区分用;判定与留痕归 9 —— 13 只切换表现)。</summary>
    public enum TerminalCause
    {
        None = 0,
        /// <summary>`position ≤ RECOVER_MAX ∧ trend ≤ 0` —— 痊愈锁存。</summary>
        Recovered = 1,
        /// <summary>`position ≥ DEATH_BAND_MIN` —— 死亡锁存(**判定与留痕归 9**)。</summary>
        Deceased = 2
    }
}
