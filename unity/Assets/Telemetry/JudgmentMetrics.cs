// 权威来源:ADR-019 §一(遥测 = 事件流回放产物)· §三(51 住边界层,只读不写)·
//   §五(P0 交付最小切面 = 指标重算器 + 调试视图 + 导出,零埋点 / 零上传 / 零玩家可见 UI)
//   · §六(51 不得替用户调数值 ⇒ 本程序集**只读**,零写回 `assets/data/` 路径 · AC-19-07)
//   · `production/epics/telemetry-analytics/story-002-f1-judgment-accuracy.md`(AC-51-B1…B4)
//
// ═══════════════════════════════════════════════════════════════════════════════
// ⚠️ **本程序集是 TD-epic-01 的还债件**(2026-10-19):普查实测 51 遥测在 `unity/Assets/` 下
//    **零生产文件**,而 10 个 story 全标 `Complete` —— AC 语义层由**测试自持谓词面**顶替
//    (`f1_judgment_accuracy_test.cs` 自陈「测试自持谓词面,零生产依赖」)。本件把谓词面上移为生产类。
//
// ⚠️ **门槛:与 `Sim.Contracts` 同款 `noEngineReferences: true`**(ADR-019 ③ 51 住边界层、不进 sim)
//    —— 读事件流是纯 C# 计算,不碰 `UnityEngine`。51 是**只读消费者**(ADR-005 三流的读者),
//    不是第四个 `IEventSink` 写入者(ADR-019 §三 逐字)。
// ╠️ 因此本程序集**不得**出现:网络调用 / `UnityEngine.*` / 写文件(导出归调用方,见下)。
// ═══════════════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Telemetry
{
    /// <summary>一次判断记录(F1 指标的输入形状 —— 由调用方从**事件流**重算后喂入)。
    /// <para>⚠️ 本类**不读流、不订阅流**:它只吃调用方已经还原出来的记录。
    /// 「从 `SimEvent` 流还原出 <see cref="JudgmentRecord"/>」归重算器
    ///(<see cref="JudgmentMetricsRecomputer"/>),51 仍只是消费者。</para></summary>
    public readonly struct JudgmentRecord
    {
        /// <summary>病例 id(只作聚合维度,不参与 F1 公式)。</summary>
        public readonly int CaseId;

        /// <summary>D(c) —— 该病例的真值集合(设计侧登记的病种集)。</summary>
        public readonly ISet<string> Truth;

        /// <summary>J(c) —— 全序最后一条判断事件给出的集合。</summary>
        public readonly ISet<string> Judged;

        /// <summary>是否存在判断改写(`JudgmentRevised` 事件,存新值)。</summary>
        public readonly bool HasRevision;

        /// <summary>构造。</summary>
        public JudgmentRecord(int caseId, ISet<string> truth,
                              ISet<string> judged, bool hasRevision)
        {
            CaseId = caseId;
            Truth = truth;
            Judged = judged;
            HasRevision = hasRevision;
        }
    }

    /// <summary>F1 判断层指标 —— 纯函数重算面(ADR-019 §一「回放即数据记录」的落地)。
    /// <para>**零埋点**:输入由调用方从既有事件流还原;本类不做任何 IO、不订阅、不写。</para>
    /// <para>**整数 (num, den) 对 + `ROUND_HALF_AWAY_FROM_ZERO`**(承 ADR-006 §五):
    /// 不在浮点域求值 ⇒ 跨平台逐位一致(ADR-012 的黄金夹具可对拍)。</para></summary>
    public static class JudgmentMetrics
    {
        /// <summary>F1 指标结果(整数对 + 分母 0 标记)。</summary>
        public readonly struct Result
        {
            /// <summary>F1a 分子 = |{c ∈ Scorable : J(c) == D(c)}|。</summary>
            public readonly int F1aNum;

            /// <summary>F1a 分母 = |Scorable|。</summary>
            public readonly int F1aDen;

            /// <summary>F1b 分子 = |{c : ∃ JudgmentRevised}|。</summary>
            public readonly int F1bNum;

            /// <summary>F1b 分母 = |Scorable|。</summary>
            public readonly int F1bDen;

            /// <summary>分母为 0 ⇒ 指标不可定义(**不是**「准确率 0」)。</summary>
            public readonly bool DenominatorZero;

            /// <summary>构造。</summary>
            public Result(int f1aNum, int f1aDen, int f1bNum, int f1bDen, bool denominatorZero)
            {
                F1aNum = f1aNum;
                F1aDen = f1aDen;
                F1bNum = f1bNum;
                F1bDen = f1bDen;
                DenominatorZero = denominatorZero;
            }
        }

        /// <summary>集合相等(含 ∅ == ∅ 判对)。
        /// <para>⚠️ 与 `ISet.SetEquals` 同义,单列出来是为让**判据有生产主语**
        /// —— 原实现住测试里,生产侧零主语 ⇒ AC 语义层无承载(本件修的正是这一条)。</para></summary>
        public static bool SetEquals(ISet<string> a, ISet<string> b)
        {
            if (a == null || b == null) return false;   // 空集 ≠ 缺失(缺失是装配错)
            return a.SetEquals(b);
        }

        /// <summary>计算 F1a / F1b(整数对)。
        /// <para>**分母 0 报 (0,0) + 标记** —— 不抛除零、不报 0/1、不报标量 0、不报 NaN
        /// (story-002 AC-51-B3 逐字)。</para></summary>
        /// <param name="records">从事件流还原出的判断记录(空 = 分母 0)。</param>
        public static Result Compute(IReadOnlyList<JudgmentRecord> records)
        {
            if (records == null || records.Count == 0)
                return new Result(0, 0, 0, 0, true);   // 分母 0 ⇒ 不可定义

            int scorable = records.Count;
            int correct = 0;
            int revised = 0;
            for (int i = 0; i < scorable; i++)
            {
                if (SetEquals(records[i].Judged, records[i].Truth)) correct++;
                if (records[i].HasRevision) revised++;
            }
            return new Result(correct, scorable, revised, scorable, false);
        }
    }
}
