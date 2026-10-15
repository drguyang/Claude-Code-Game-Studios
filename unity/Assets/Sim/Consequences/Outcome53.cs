// S12-9 · story-002 —— F-53.1 确定性结算(Outcome = Judge(P,C,D,U))的实现件。
//
// 权威来源:
//   GDD design/gdd/medical-consequences.md —— F-53.1(Outcome 枚举非分数 · 双轴)/
//     规则三(U = SplitMix64(WorldSeed, "case-salt", salted_key) · 试药史判据 OQ-53-9)/
//     规则五(入向闭集)/ 规则七(后果非数 —— P0 刻意不给连续分值)/ AC-53-01/08
//   卡面 story-002 —— Judge = 判定表(作者态 → ADR-014 烘焙)+ 纯函数求值器;
//     表值与 U 的具体用法归数值轮,本件交**形状 + 烘焙校验 + 夹具跑通**
//   键域裁定(2026-10-15 用户裁):P 3 态 × C 3 态 × D 2 态 = 18 行 + default;
//     U 档列默认 1 档;无痕 = 显式枚举成员 NoEcho
//   ADR-005 / ADR-006(定点域 · 零浮点)/ ADR-012(逐位)/ ADR-025(Sim 引用集恰 = BCL+Contracts)
//
// ⚠️ 住 Sim 程序集 `DaYiJingCheng.Sim.Consequences`(AC-53-12:53 无独立程序集 ——
//   载体 = Sim 内命名空间;门 A / b2 / b6 全部继续适用)。
//
// ⚠️ 纯函数形状:Judge 静态、零字段;判定表是**入参**(依赖注入,承
//   DiagnosisReadFloorTable 形态)—— 两独立进程喂同一表 + 同一输入 ⇒ 逐位相同。
//
// ⚠️ 零浮点(AC-53-03):全文件无 float/double 字面量;U 以 Q16.16 raw long 承载,
//   档切分走整数比较(ADR-006 边界;FixParse 不入本件 —— 表值是枚举非数值)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Consequences
{
    /// <summary>
    /// F-53.1 判定域 —— Outcome 枚举闭集 + 键域三轴 + 判定表数据集 + Judge 纯函数。
    /// </summary>
    /// <remarks>
    /// <para><b>双轴</b>(GDD F-53.1,2026-09-19 用户裁定):结局轴(PatientLives/PatientLoses)
    /// 由 9 病程结算、53 只读不裁;回响轴(Residual/Recurrence/TrialHistory)= 判断误差的
    /// 物质痕迹,53 裁量。无痕 = <see cref="Outcome.NoEcho"/>(显式成员 —— 误诊自愈 ⇒
    /// 不回响,世界不撒谎)。</para>
    /// <para><b>P0 刻意不给连续分值</b>(AC-53-01):分值 = 数字 = 违反「后果是人不是数」
    /// (规则七)。出口类型恒为枚举。</para>
    /// </remarks>
    public static class Outcome53
    {
        // ════════════════════════════════════════════════════════════════════
        // Outcome 枚举闭集(AC-53-01;成员集 = GDD F-53.1 表,增员 = GDD 修订轮)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>后果枚举(闭集,恰 6 员)。</summary>
        public enum Outcome
        {
            /// <summary>结局轴:病人存活(9 结算;53 只读、不产此值 —— 表内永不出现)。</summary>
            PatientLives = 0,
            /// <summary>结局轴:病人走失(9 结算;53 只读、不产此值 —— 表内永不出现)。</summary>
            PatientLoses = 1,
            /// <summary>回响轴:后遗(判断误差的物质痕迹之一)。</summary>
            Residual = 2,
            /// <summary>回响轴:复现。</summary>
            Recurrence = 3,
            /// <summary>回响轴:试药史(OQ-53-9 谓词覆盖表行)。</summary>
            TrialHistory = 4,
            /// <summary>无痕(误诊自愈 / 对因确诊 ⇒ 不回响 —— 世界不撒谎)。</summary>
            NoEcho = 5,
        }

        // ════════════════════════════════════════════════════════════════════
        // 键域三轴(2026-10-15 用户裁定 3×3×2)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>P 轴 —— 判断三态(S-8.3:空/疑似/确定;由判断投影行 + 置信分界物化)。</summary>
        public enum JudgmentLane
        {
            /// <summary>空:无判断记录,或病名空置(lexiconId=0,《他回来了》第二幕合法终态)。</summary>
            Empty = 0,
            /// <summary>疑似:落笔但置信 < 分界。</summary>
            Suspected = 1,
            /// <summary>确定:落笔且置信 ≥ 分界。</summary>
            Confirmed = 2,
        }

        /// <summary>C 轴 —— 处置三态(命中位经 <see cref="IDiseaseProjection"/>,53 不判)。</summary>
        public enum CareLane
        {
            /// <summary>无处置。</summary>
            None = 0,
            /// <summary>有处置但动作未命中 treatable_by(命中位 = 9 注册表真源)。</summary>
            AppliedUnmatched = 1,
            /// <summary>有处置且动作命中 treatable_by。</summary>
            AppliedMatched = 2,
        }

        /// <summary>D 轴 —— 病程二态(「误诊自愈 ⇒ 无痕」判据位;真源 = 9 病程投影)。</summary>
        public enum RecoveryLane
        {
            /// <summary>痊愈@结案(误诊自愈判据的痊愈位)。</summary>
            Recovered = 0,
            /// <summary>未痊愈@结案。</summary>
            NotRecovered = 1,
        }

        // ════════════════════════════════════════════════════════════════════
        // JudgeInputs(AC-53-08:类型面闭集,零 verdict / 零 disease_id / 零生死位)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Judge 输入(键域投影后的闭集形状;恰 6 字段 —— 反射断言面)。</summary>
        /// <remarks>
        /// <para><b>零 verdict / correctness 字段</b>(AC-53-08:37 无此字段,53 自算);
        /// <b>零 disease_id</b>(AC-53-07 反渗前置:salted_key 语义替代);
        /// <b>零生死可控位</b>(双轴:结局轴 = 9,53 只读)。</para>
        /// </remarks>
        public readonly struct JudgeInputs
        {
            /// <summary>P 键(判断三态)。</summary>
            public readonly JudgmentLane P;
            /// <summary>C 键(处置三态)。</summary>
            public readonly CareLane C;
            /// <summary>D 键(病程二态)。</summary>
            public readonly RecoveryLane D;
            /// <summary>试药史痕迹位(OQ-53-9 谓词产出;true ⇒ 覆盖表行)。</summary>
            public readonly bool HasTrialHistory;
            /// <summary>U 档(档切分整数序数;band_count=1 时恒 0)。</summary>
            public readonly int UBand;
            /// <summary>U 的 Q16.16 raw(逐位可重构;夹具钉值用 —— 求值器只消费 UBand)。
            /// <para>ulong:哈希域原生型(SplitMix64 出口;与 <see cref="OutcomeJudgeDerive.URaw"/> 同型)。</para></summary>
            public readonly ulong URaw;

            /// <summary>构造 Judge 输入。</summary>
            public JudgeInputs(JudgmentLane p, CareLane c, RecoveryLane d,
                               bool hasTrialHistory, int uBand, ulong uRaw)
            {
                P = p; C = c; D = d; HasTrialHistory = hasTrialHistory;
                UBand = uBand; URaw = uRaw;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 键域物化(从 Inputs53 解码行投影到三轴;置信分界常量留槽)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>置信分界(P 轴 疑似/确定 切点;留槽 —— 值归数值轮,形状本件定)。
        /// <para>落笔置信档语义 = OQ-CB-5 甲循环(0 空/1 疑似/2 确定):≥ 2 = 确定。</para></summary>
        public const byte ConfidenceConfirmedFloor = 2;

        /// <summary>P 轴物化:判断投影行 → 三态(空 = 无行或 lexiconId=0)。</summary>
        public static JudgmentLane JudgmentLaneOf(IReadOnlyList<JudgmentRow53> judgments)
        {
            if (judgments == null || judgments.Count == 0)
                return JudgmentLane.Empty;
            var last = judgments[judgments.Count - 1];   // 聚合器覆盖式 ⇒ 单行;取尾行防御
            if (last.LexiconId == 0)
                return JudgmentLane.Empty;               // 病名空置(合法终态,S-8.3 性质 1)
            return last.Confidence >= ConfidenceConfirmedFloor
                ? JudgmentLane.Confirmed : JudgmentLane.Suspected;
        }

        /// <summary>C 轴物化:处置投影行 → 三态(命中位由调用方经投影接口给出,53 不判)。</summary>
        /// <param name="cares">处置行(C 路解码行)。</param>
        /// <param name="matched">动作命中位(真源 = 9 注册表 treatable_by;**53 不判极性/命中**)。</param>
        public static CareLane CareLaneOf(IReadOnlyList<CareRow53> cares, bool matched)
        {
            if (cares == null || cares.Count == 0)
                return CareLane.None;
            return matched ? CareLane.AppliedMatched : CareLane.AppliedUnmatched;
        }

        /// <summary>U 档切分:Q16.16 raw → 档序数(整数比较,零浮点;band_count=1 ⇒ 恒 0)。</summary>
        /// <remarks>
        /// <para>P0 默认 1 档 ⇒ band 恒 0(数值轮扩档后此处按均分切 —— 形状本件钉,
        /// 切点值归数值轮)。raw 域 [0, 65535](哈希高 16 位)。</para>
        /// </remarks>
        public static int BandOf(ulong uRaw, int bandCount)
        {
            if (bandCount <= 1) return 0;
            // 均分切分:raw ∈ [0, 65535] → band ∈ [0, bandCount-1](整数除法,零浮点)。
            ulong span = (ulong)(65536L / bandCount);
            int band = (int)(uRaw / span);
            return band >= bandCount ? bandCount - 1 : band;   // 尾桶并入末档
        }

        // ════════════════════════════════════════════════════════════════════
        // 判定表数据集(烘焙产物解码形;ADR-014)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>判定表(烘焙机产物的运行期形状;测试可直接驱动 Binder —— 反空转)。</summary>
        public sealed class Table
        {
            /// <summary>表行数(18;default 行不计)。</summary>
            public int RowCount => _rows.Count;
            /// <summary>U 档数(P0 默认 1;常量留槽,数值轮扩)。</summary>
            public int UBandCount => _uBandCount;
            /// <summary>是否带显式 default 行(构建期校验 ① 的空洞兜底)。</summary>
            public bool HasDefaultRow => _hasDefault;
            /// <summary>default 行出口(∈ 闭集)。</summary>
            public Outcome DefaultOutcome => _defaultOutcome;

            private readonly Dictionary<int, Outcome> _rows;
            private readonly int _uBandCount;
            private readonly bool _hasDefault;
            private readonly Outcome _defaultOutcome;

            /// <summary>构造表(公开供跨程序集 Bake 物化;运行期生产装载 = IDataProvider,归装配轮)。</summary>
            public Table(Dictionary<int, Outcome> rows, int uBandCount,
                         bool hasDefault, Outcome defaultOutcome)
            {
                _rows = rows; _uBandCount = uBandCount;
                _hasDefault = hasDefault; _defaultOutcome = defaultOutcome;
            }

            /// <summary>查表(键 = 三轴混合序数;未命中 ⇒ default 行,运行期不可达 ——
            /// 构建期已保证 18 组合全覆盖)。</summary>
            public bool TryLookup(JudgmentLane p, CareLane c, RecoveryLane d, out Outcome outcome)
            {
                outcome = _defaultOutcome;
                return _rows.TryGetValue(RowKey(p, c, d, 0), out outcome) ||
                       _rows.TryGetValue(RowKey(p, c, d, 1), out outcome);
            }

            /// <summary>按 (P,C,D,U档) 编行键(整数混合;与 Binder 同式)。</summary>
            internal static int RowKey(JudgmentLane p, CareLane c, RecoveryLane d, int uBand)
                => ((((int)p * 3 + (int)c) * 2 + (int)d) * Math.Max(1, uBand + 1) + uBand);
        }

        /// <summary>行键编码(Bake 侧落盘 / 运行期查表同式 —— 单一出处;public 供跨程序集 Bake)。</summary>
        public static int RowKeyOf(JudgmentLane p, CareLane c, RecoveryLane d, int uBand)
            => Table.RowKey(p, c, d, uBand);

        /// <summary>表物化入口(Bake 侧 / 测试装配用;运行期生产装载 = IDataProvider 读 cooked,归装配轮)。</summary>
        public static Table CreateTable(
            Dictionary<int, Outcome> rows, int uBandCount, bool hasDefault, Outcome defaultOutcome)
            => new Table(rows, uBandCount, hasDefault, defaultOutcome);

        // ════════════════════════════════════════════════════════════════════
        // Judge 纯函数(出口恒为枚举;零 ref/out;零字段)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Judge 求值器(静态纯函数;同输入同输出,零隐藏可变态)。</summary>
        /// <remarks>
        /// <para>求值序:① 试药史痕迹覆盖表行(OQ-53-9)→ ② 表查(P,C,D[,U 档])。</para>
        /// <para><b>结局轴永不产出</b>:表构建期校验 ② 只放行回响轴三值 + NoEcho;
        /// PatientLives/PatientLoses 是枚举闭集成员但**不进表**(F-53.2 定义域收窄同构
        /// —— 结局轴由 9 结算,53 只读)。</para>
        /// </remarks>
        public static class Judge
        {
            /// <summary>求值 F-53.1:Outcome = Judge(P, C, D, U)。</summary>
            /// <param name="inputs">键域投影后的输入(闭集形状;按值传 —— readonly struct,
            /// 值传与 in 同成本,且反射面「零 ByRef 形参」断言可判)。</param>
            /// <param name="table">判定表(烘焙产物;依赖注入)。</param>
            /// <returns>后果枚举(恒 ∈ 回响轴三值 ∪ NoEcho;结局轴两员不在此出口)。</returns>
            public static Outcome Evaluate(JudgeInputs inputs, Table table)
            {
                if (table == null) throw new ArgumentNullException(nameof(table));
                if (inputs.HasTrialHistory)
                    return Outcome.TrialHistory;             // OQ-53-9:物质痕迹覆盖表行

                if (table.TryLookup(inputs.P, inputs.C, inputs.D, out var outcome))
                    return outcome;
                return table.DefaultOutcome;                 // 构建期保证不可达;防御兜底
            }
        }
    }

    /// <summary>
    /// U 派生(F-53.1:U = SplitMix64(WorldSeed, "case-salt", salted_key) 的 Q16.16 物化)。
    /// </summary>
    /// <remarks>
    /// <para><b>键序 / 混盐写法逐位钉</b>(AC-53-15 黄金夹具):哈希高 16 位 = Q16.16 raw。</para>
    /// <para><b>退化路径钉死</b>(卡面 U 口径):salted_key 缺失(未认出模式)⇒ 显式中点
    /// 1/2(raw 32768)—— 非 0、非静默;数值轮只改常量不改形状。</para>
    /// </remarks>
    public static class OutcomeJudgeDerive
    {
        /// <summary>退化 U 常量:显式中点 1/2(Q16.16 raw = 32768)。非 0、非静默。</summary>
        public const long DegradedURaw = 32768;

        /// <summary>U 派生:SplitMix64(WorldSeed, "case-salt", salted_key) 高 16 位(Q16.16 raw)。</summary>
        public static ulong URaw(long worldSeed, ulong saltedKey)
            => SplitMix64.HashTagged(worldSeed, "case-salt", unchecked((long)saltedKey)) >> 48;

        /// <summary>退化路径(未认出模式):显式中点常量。</summary>
        public static ulong URawDegraded() => (ulong)DegradedURaw;
    }
}
