// ADR-005 / GDD disease-simulation.md §F4 —— 九态阈值机与死亡判定。
//
// 权威来源:
//   ADR-005 §Key Interfaces —— 事件进流，状态机是事件的纯函数
//   GDD disease-simulation.md §F4 —— 九态状态机与阈值
//   GDD disease-simulation.md 规则三 —— 铁律一「死因唯一」/ 铁律二「伪治疗不续命」
//
// 核心机制:
//   - 三阈值 CRITICAL < COMA < DEATH 且 DEATH < 1
//   - 死亡判定按严重度降序先判
//   - 照护杠杆适用集 = {伤寒/痢疾/心衰} 3 种
//   - 状态迁移 ⇒ 同 tick 落 threshold_transition 事件

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 伤情状态（九态）。
    /// </summary>
    /// <remarks>
    /// <para><b>🔁 2026-10-22 第三轮订正 —— 本枚举的「九态」与 GDD 的「九态」<u>不是同一套词汇</u></b>
    /// (此前两处注释把二者当成同一物,均已就地加注)。逐值对照:</para>
    /// <list type="table">
    /// <item><term>GDD 九态</term><description>潜伏 · 发病 · 进展 · 好转 · 迁延 · 危殆 · 昏迷 · 痊愈 · 死亡
    /// (<c>disease-simulation.md:536-546</c>)</description></item>
    /// <item><term>本枚举</term><description>Healthy · Mild · Moderate · Severe · Critical · Coma ·
    /// Recovered · Chronic · Deceased</description></item>
    /// </list>
    /// <para><b>可对齐的只有 5 对</b>:<c>Coma ↔ 昏迷</c> · <c>Critical ↔ 危殆</c> ·
    /// <c>Recovered ↔ 痊愈</c> · <c>Chronic ↔ 迁延</c> · <c>Deceased ↔ 死亡</c>。
    /// <b>GDD 有而本枚举无</b>:潜伏 / 发病 / 进展 / 好转 —— 四者都是**病程过程态**,
    /// GDD 由 <c>τ</c>(病程时长)与 <c>sign(trend)</c> 分段得出,<b>不是 <c>position</c> 阈值的函数</b>。
    /// <b>本枚举有而 GDD 无</b>:<c>Healthy</c> / <c>Mild</c> / <c>Moderate</c> / <c>Severe</c> ——
    /// 四者是按 <c>position</c> 切的三档严重度 + 健康,**GDD 九态表里没有这三档**
    /// (其阈值 <see cref="StateMachine.SEVERE_THRESHOLD"/> / <see cref="StateMachine.MODERATE_THRESHOLD"/>
    /// 亦无 GDD 出处)。</para>
    /// <para><b>⚠️ 为什么不是缺陷、也不是可随手改的命名</b>:</para>
    /// <list type="number">
    /// <item><b>两个枚举服务两个不同的消费者</b>。<c>InjuryState</c> 是
    /// <see cref="EventKind.InjuryStateChanged"/> 的载荷词汇(<c>new_state</c> 枚举,
    /// <c>entities.yaml:2382</c>),服务<b>受伤实体集 {敌人, 玩家}</b>、落<b>世界流</b>;
    /// GDD 九态是<b>病人侧</b>病程语义、落<b>病史流</b>。改本枚举 = 改一条已登记 Kind 的载荷域。</item>
    /// <item><b>GDD 明确禁止下游重建九态</b>:13 病人 AI 只按 <c>position</c>/<c>trend</c> 自有
    /// <c>BEHAVIOR_BAND_*</c> 分档(<c>:528</c> / <c>:646</c> / <c>:1487</c>,ADR-016 §六)——
    /// 「13 不重建九态」。故 GDD 九态<b>从未计划</b>成为一个跨系统的状态枚举。</item>
    /// <item><b>病人侧的状态转移本就不走本枚举</b>:<c>entities.yaml:2383</c> 与
    /// <c>InjuryStateChangedWriter</c> 头注均明写「病人侧的状态转移由 9 的病史流既有 Kind 承载,
    /// 本 Kind 服务受伤实体集」。⇒ 病人侧终态由 <see cref="EventKind.PatientOutcomeChanged"/>
    /// (ADR-032,outcome ∈ {Recovered, Died})承载,<b>不经 <c>InjuryState</c></b>。</item>
    /// </list>
    /// <para><b>⚠️ 当前的实际可达性</b>:本枚举的<b>唯一生产读取方</b>是
    /// <see cref="StateMachine.ComputeTransition"/> 的调用点
    /// <see cref="InjuryStateChangedWriter"/>,而该写者<b>自身零生产构造点</b>
    /// (只在 <c>death_candidate_writers_test.cs:47</c> 出现)⇒ 整条链在生产上**不可达**。
    /// 本订正**只加注、不改值** —— 改值须先裁「受伤实体的状态词汇是否要与 GDD 九态合并」,
    /// 那是一条会动已登记 Kind 载荷域的架构问题,不在实现轮自行处置。</para>
    /// </remarks>
    public enum InjuryState
    {
        Healthy = 0,
        Mild = 1,
        Moderate = 2,
        Severe = 3,
        Critical = 4,
        Coma = 5,
        Recovered = 6,
        Chronic = 7,
        Deceased = 8
    }

    /// <summary>
    /// 状态迁移事件。
    /// </summary>
    public readonly struct ThresholdTransition
    {
        public readonly PatientId Patient;
        public readonly InjuryState OldState;
        public readonly InjuryState NewState;

        public ThresholdTransition(PatientId patient, InjuryState oldState, InjuryState newState)
        {
            Patient = patient;
            OldState = oldState;
            NewState = newState;
        }
    }

    /// <summary>
    /// 九态阈值机 —— F4 状态机。
    /// </summary>
    public static class StateMachine
    {
        // AC-17: 三阈值严格单调
        //
        // ✅ **以下三个值已于 2026-10-22 由用户裁定(档 C)**,不再是占位值。
        //   原「实现期占位值」注保留在下方(原文不删)。
        //
        //   裁定记录:GDD `design/gdd/disease-simulation.md` §五(:1676-1679);
        //   `design/registry/entities.yaml` 同名三块 value 同批改为裁定值。
        //   量纲:Q16.16 raw(65536 = 1.0)。
        //     CRITICAL = 19661 raw ≈ 0.3000
        //     COMA     = 29491 raw ≈ 0.4500
        //     DEATH    = 38011 raw ≈ 0.5800
        //   约束:`CRITICAL < COMA < DEATH < 1`(校验 9)—— 三条均满足。
        //
        //   ⚠️ **裁定档 C 的关键取舍**:DEATH 取 0.58(高于原现网 0.1373),
        //   使 R2 五个伤情行产生真实梯度 —— `INJ_LACERATION` 的 `A_peak/Scale = 0.60`
        //   仅比死线高 **0.02**(勉强致死),`INJ_STAB` 的 1.00 高出 0.42(几乎必死)。
        //   ⇒ 伤情的「致命性分档」首次在数值上可观测,而非仅靠 `lethal` 布尔。
        //
        //   ⚠️ 连带(仍有效):`DEATH_THRESHOLD` 同时是注册表写入期两条下界的量纲来源
        //   (校验 4b / 14:`plateau ∧ lethal` 与急性保持型须 `A_peak/SCALE ≥ DEATH_THRESHOLD`)。
        //   本值一动,伤情 / 病种数据行的 `A_peak` / `Scale` 须同批复核
        //   —— 2026-10-22 已复核 13 行(5 伤情 + 8 病种),全部满足。
        //
        //   ── 以下为原文,保留作闭环记录 ──────────────────────────────────
        //   ⚠️⚠️ **以下三个值是实现期占位值,不是裁定值**(2026-10-22 补注,承 PRE-S18-B)。
        //   权威:`design/gdd/disease-simulation.md` §五 三行 default 全为 `*待定*`;
        //   `design/registry/entities.yaml` 的 `CRITICAL_THRESHOLD` / `COMA_THRESHOLD` /
        //   `DEATH_THRESHOLD` 三块 value 亦写「待定」并把本处值标注为「现网实现值,非裁定值」。
        //   数值归用户数值轮 —— **改它们须先有裁定记录**,不得由实现侧自行调。
        //   (↑ 上述状态已于 2026-10-22 结清。)
        public const int CRITICAL_THRESHOLD = 19661;   // 0.30 × 65536
        public const int COMA_THRESHOLD = 29491;       // 0.45 × 65536
        public const int DEATH_THRESHOLD = 38011;      // 0.58 × 65536

        /// <summary>
        /// 干预窗口 —— 最后一次有效干预后多久算「放弃」(F4 死亡支第四条件)。
        /// <para><b>2026-10-22 用户裁定 = 3600 tick</b>(@ 20 Hz = 180 秒 = 3 分钟)。
        /// 裁定口径:玩家离开病人 30 秒算照护中止(<see cref="CARE_GAP"/>),再过 3 分钟死 ——
        /// 留出跑腿取药的时间,但铁律二的悲剧面仍可达成。</para>
        /// <para>⚠️ <b>本常量 2026-10-22 之前不存在</b>(全库 `grep "TREATMENT_WINDOW\s*="`
        /// 零命中)—— 值虽已裁、登记在 `entities.yaml` + GDD §五,但 C# 侧连符号都没有。
        /// F4 求值体落地时补此常量,是「值已裁 ≠ 可求值」三件缺口里最基础的一件。</para>
        /// <para>单位 = <b>tick</b>(非 raw),与 `last_intervention` / `currentTick` 同量纲。</para>
        /// </summary>
        public const int TREATMENT_WINDOW = 3600;

        /// <summary>
        /// 连续照护判定阈 —— 相邻两条护理动作 gap &gt; 本值 ⇒ 视为照护中止
        /// (GDD 规则六 · 集群 2 的派生定义 `last_intervention(照护) =
        /// max(最后一条护理动作止, 最后一条护理动作事件 tick + CARE_GAP)`)。
        /// <para><b>2026-10-22 用户裁定 = 600 tick</b>(@ 20 Hz = 30 秒)。</para>
        /// <para><b>排序校验(GDD 校验 17)</b>:<c>TREATMENT_WINDOW ≥ CARE_GAP</c> ——
        /// 否则「连续照护」在窗口内会被误判中止。现值为 3600 ≥ 600 ✓。
        /// 该排序由 <see cref="F4OutcomeEvaluator"/> 的静态构造断言 + 测试双重钉住。</para>
        /// </summary>
        public const int CARE_GAP = 600;

        /// <summary>
        /// 痊愈判定门 —— `position_agg &lt; 本值` 且低位稳定 ≥ W ⇒ 痊愈(B5,2026-09-15)。
        /// <para><b>2026-10-22 用户裁定 = 7864 raw</b>(≈ 0.12)。排序
        /// `RECOVER &lt; CRITICAL &lt; COMA &lt; DEATH` 满足(0.12 &lt; 0.30 &lt; 0.45 &lt; 0.58)。</para>
        /// <para>⚠️ <b>本常量 2026-10-22 之前不存在</b>(与 `TREATMENT_WINDOW` / `CARE_GAP` 同批补)。
        /// 它当前**唯一的消费者**是注册表的 R1-35(豁免类下界校验);
        /// F4 的痊愈支(需要 `trend_backward_agg`)仍未实现 —— 见 `F4OutcomeEvaluator` 的登记项。</para>
        /// <para>🔁 <b>2026-10-22 第三轮订正 —— 上段后两句已失效(原文不删)</b>:
        /// F4 的痊愈支**已实现**(<see cref="F4OutcomeEvaluator"/> 的
        /// <c>TryEvaluateRecovered</c>),它**消费本常量**作 <c>position_agg &lt; RECOVER</c> 的判据。
        /// ⇒ 现有**两个**消费者:① 注册表 R1-35 豁免类下界;② F4 痊愈支第一条件。
        /// ⚠️ 痊愈支的另两个量(<c>W</c> / <c>TREND_EPS</c>)仍**未裁定** ——
        /// 它们作**入参**传入(<c>RecoveryWindowInput</c>),不写死在这里。</para>
        /// </summary>
        public const int RECOVER_THRESHOLD = 7864;

        /// <summary>
        /// 重度档下界(GDD 九态分级,`position ≥ 本值` ⇒ Severe)。
        /// <para>🔁 2026-10-22:原为 <see cref="ComputeTransition"/> 里的裸字面量 <c>2000</c>。
        /// 提为具名常量是因为 <see cref="F4OutcomeEvaluator"/> 也要用它 ——
        /// 两处各写一个字面量就是两份真源,改一处不改另一处会静默分叉。
        /// <b>值未改</b>(仍 = 2000)。</para>
        /// <para>⚠️ <b>2026-10-22 第三轮订正 —— 「GDD 九态分级」这个归属是错的(原文不删)</b>:
        /// 实测 GDD <c>disease-simulation.md</c> 的九态表(:536-546)**没有**「轻度 / 中度 / 重度」三档,
        /// 全篇亦无 <c>2000</c> / <c>1000</c> 作阈值。本常量与 <see cref="MODERATE_THRESHOLD"/> 的
        /// 出处是 <see cref="InjuryState"/> 这个**九值枚举本身**(C# 侧既有词汇),
        /// **不是 GDD 的九态**。两套词汇的对应关系见 <see cref="InjuryState"/> 的订正注。</para>
        /// </summary>
        public const int SEVERE_THRESHOLD = 2000;

        /// <summary>
        /// 中度档下界(同上,裸字面量 <c>1000</c> 提为具名常量;**值未改**)。
        /// </summary>
        public const int MODERATE_THRESHOLD = 1000;

        // AC-30: 照护杠杆适用集
        public static readonly HashSet<string> CareApplicableDiseases = new HashSet<string>
        {
            "typhoid",    // 伤寒
            "dysentery",  // 痢疾
            "heart_failure" // 心衰
        };

        /// <summary>
        /// 计算状态迁移。
        ///
        /// <para><b>⚠️ 本方法只做「纯阈值分级」,<u>不</u>承担 F4 的四条件合取(2026-10-22 裁定)</b>。</para>
        ///
        /// <para>GDD <c>:1086-1090</c> 的死亡支是<b>四条件合取</b>:
        /// <c>LethalFor ∧ ¬SelfLimited ∧ position_agg ≥ DEATH_THRESHOLD ∧ (t − last_intervention) &gt; TREATMENT_WINDOW</c>。
        /// 本方法<b>只实现第三个</b>(阈值)。另三条件(<b>两门 + 窗口</b>)的求值体
        /// <b>全库不存在</b> —— 见 <see cref="EntityDeathGates"/> 的 2026-10-22 补注。
        /// 两门在 <see cref="EntityDeathGates"/>(有独立测试),窗口与 <c>last_intervention</c> 无落点。</para>
        ///
        /// <para><b>⚠️ 为什么两门不塞进来(而不是「还没塞」)</b> —— 三条结构性理由,不是排期:</para>
        /// <list type="number">
        /// <item><b>签名不足</b>:两门要 <c>(ActorClass, DiseaseRegistryEntry)</c>,本方法只有一个
        /// <c>string diseaseKey</c> —— 一个都给不出。<c>ActorClass</c> 是判死谓词的<b>第一入参</b>
        /// (敌人/兽 vs 玩家/病人),靠 <c>diseaseKey</c> 永远推不出来。</item>
        /// <item><b>实体集不同</b>:本方法现被 <see cref="InjuryStateChangedWriter"/> 调用,
        /// 那是<b>受伤实体集 {敌人, 玩家}</b>、落世界流;而 F4 死亡支是<b>病人侧</b>病史流判据(9 的 <c>Step</c>)。
        /// 两者合流 ⇒ 只能按实体类别分叉控制流,正是<b>规则九</b>要防的形态。</item>
        /// <item><b>纯度</b>:两门各自已有独立可证伪的判据(<c>entity_death_gates_test</c>)。
        /// 合成一个函数会让「门错」与「窗口错」在同一个红里,分不开。</item>
        /// </list>
        ///
        /// <para><b>⇒ 归属</b>:F4 求值体另立纯函数(裁定名 <c>F4OutcomeEvaluator.Evaluate</c>),
        /// 与 <c>last_intervention</c> 聚合器<b>同批</b>实现 —— 否则 F4 只能拿外部灌进来的数,
        /// 无法从三源重算,违 ADR-016 §一 三源不变量。</para>
        ///
        /// <para><b>⚠️ 三个形参当前<b>函数体一个都不读</b></b>(<c>lastInterventionTick</c> /
        /// <c>currentTick</c> / <c>diseaseKey</c>)—— 这不是笔误,是上面的缺口的<b>直接症状</b>:
        /// 窗口条件与两门都还没实现,它们暂时无用武之地。保留签名是为了让
        /// <see cref="InjuryStateChangedWriter"/> 的调用点不必随 F4 落地而改。
        /// <b>不得</b>因为「读了也没用」就把它们删掉。</para>
        /// </summary>
        public static InjuryState ComputeTransition(
            InjuryState currentState,
            Fix positionAgg,
            long lastInterventionTick,
            long currentTick,
            string diseaseKey)
        {
            // AC-29: 判定顺序锁 —— 严重度降序
            // DEATH → COMA → CRITICAL → ...

            // 死亡判定
            if (positionAgg.Raw >= DEATH_THRESHOLD)
            {
                return InjuryState.Deceased;
            }

            // 昏迷判定
            if (positionAgg.Raw >= COMA_THRESHOLD)
            {
                return InjuryState.Coma;
            }

            // 危殆判定
            if (positionAgg.Raw >= CRITICAL_THRESHOLD)
            {
                return InjuryState.Critical;
            }

            // 重度判定
            if (positionAgg.Raw >= SEVERE_THRESHOLD)
            {
                return InjuryState.Severe;
            }

            // 中度判定
            if (positionAgg.Raw >= MODERATE_THRESHOLD)
            {
                return InjuryState.Moderate;
            }

            // 轻度判定
            if (positionAgg.Raw > 0)
            {
                return InjuryState.Mild;
            }

            // 健康
            return InjuryState.Healthy;
        }

        /// <summary>
        /// 判断是否可照护。
        /// </summary>
        public static bool IsCareApplicable(string diseaseKey)
        {
            return CareApplicableDiseases.Contains(diseaseKey);
        }

        /// <summary>
        /// 判断是否已死亡。
        /// </summary>
        public static bool IsDeceased(InjuryState state)
        {
            return state == InjuryState.Deceased;
        }

        /// <summary>
        /// 判断是否已痊愈。
        /// </summary>
        public static bool IsRecovered(InjuryState state)
        {
            return state == InjuryState.Recovered;
        }
    }
}
