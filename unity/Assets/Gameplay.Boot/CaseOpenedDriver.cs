// sprint-05 T1.2 · 病例开账驱动(2026-10-10)
//
// 权威来源:
//   ADR-008 §一(病例流路由)· ADR-005(主机唯一 Append)
//   case-system.md 规则二(立案 A/B 两路径 · 立案时刻 = 就诊交互的那一 tick)
//   F-6 复评实测:CaseOpenWriter 零生产调用方 ⇒ 运行期链可达性 0% ⇒ 本驱动补上调用方
//
// 设计口径:
//   - 驱动 = 每 tick 边沿检查在场病人,对未开案的病人自动开案。
//   - 开案条件 = 病人在场 + 未已有开案(CaseOpenWriter.TryOpen 内部检查)。
//   - diseaseSnapshot = 空集(P0 接线载体;真实快照归 9 诊断链 + 数值轮)。
//   - 有界性:每病人最多一个开案(AC-37-26),驱动不强制超界。
//
// ⚠️ 单线程:tick 边沿由 BootRoot 的 Update 泵驱动,与 sim 的「主机唯一 Step」同线程。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 病例开账驱动 —— 把 <see cref="CaseOpenWriter"/> 接进 tick 边沿序列(sprint-05 T1.2)。
    /// <para><b>只驱动,不持游戏状态</b>(与 BootRoot 同纪律):开案真源在事件流,
    /// 本类只按节奏触发写事件。</para>
    /// </summary>
    public sealed class CaseOpenedDriver
    {
        private readonly CaseOpenWriter _writer;
        private readonly IPresenceQuery _presence;
        private readonly HashSet<int> _openedPatients = new HashSet<int>();

        /// <param name="writer">立案写者(37 的 CaseOpened 写者)。</param>
        /// <param name="presence">在场查询(开案前置条件 ①「病人存在」的真源)。</param>
        public CaseOpenedDriver(CaseOpenWriter writer, IPresenceQuery presence)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _presence = presence ?? throw new ArgumentNullException(nameof(presence));
        }

        /// <summary>
        /// tick 边沿驱动 —— 对在场且未开案的病人自动开案。
        /// </summary>
        /// <param name="tick">本边沿自己的逻辑 tick(由帧泵回推,非末 tick)。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> 为负。</exception>
        public void OnTickEdge(long tick)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "逻辑 tick 不得为负");

            // 遍历在场病人,对未开案的病人开案
            // ⚠️ P0 接线载体:真实开案触发点 = 就诊交互(归 37/4/8 的交互装配轮)
            foreach (var patientId in _presence.PresentPatientIds())
            {
                if (_openedPatients.Contains(patientId))
                    continue;

                var result = _writer.TryOpen(tick, new PatientId(patientId), default);
                if (result == CaseOpenWriteResult.Opened)
                {
                    _openedPatients.Add(patientId);
                }
            }
        }
    }
}
