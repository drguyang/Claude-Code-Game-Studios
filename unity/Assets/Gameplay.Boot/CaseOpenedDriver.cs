// sprint-05 T1.2 · 病例开账驱动(2026-10-10)
// 🔁 2026-11-02 · 开册入口轮:自动开案退役,本类改为**玩家就诊动作**的立案入口。
//
// 权威来源:
//   ADR-008 §一(病例流路由)· ADR-005(主机唯一 Append)
//   case-system.md 规则二-A(立案时刻 = 抵达 + 就诊交互)· :176(「立案是一次玩家动作」)
//   case-system.md :927(「急召出诊事件降临但玩家没去 ⇒ 不立案」)
//   casebook-39.md §3 入口 / §7 交互表(世界空间裸 Interact = 就诊/立案)
//   diagnosis-system.md S-8.4(路线甲 = 模态分流:世界 Interact **只**是就诊)
//
// ── 为什么改(2026-11-02 · 结清 `TD-casebook-01`)──────────────────────
// 本类原为 `OnTickEdge(tick)`:每 tick 扫在场病人,**对未开案者自动开案**。
// 那个形态与规则二-A **方向相反** —— 规则写「玩家没去 ⇒ 不立案」,实装是
// 「病人在场 ⇒ 自动立案」。它当时是一条被注释标注的 P0 占位(`:53` 旧注),
// 但**没有任何 story / 债**认领它,于是占位活了很久:
// 病例在玩家一次都没走过去时就被开出,而脉案屏反倒**谁都打不开**(零 `OpenModal` 调用点)。
//
// 现口径:**立案唯一触发 = 玩家对病人的 `Interact`**(经 `InteractionInputDriver` 的就诊分支)。
// 驱动本身仍是纯转发 + tick 守卫,真正的判定(在场 / 未已有开案 / `PatientId.None`)在
// `CaseOpenWriter.TryOpen` 里一次做完 —— 不在此处复制第二份判据。
//
// ⚠️ 单线程:tick 由 `BootRoot` 的 Update 泵驱动,与 sim 的「主机唯一 Step」同线程。
// ⚠️ 有界性:每病人每案至多一条 `CaseOpened`(`AC-37-26`),由 writer 的
//    `CaseStreamQuery.HasOpenCase` 前置保证,本类不再持第二份「已开案」集合。

using System;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 立案驱动 —— 37 的 `CaseOpened` 生产入口(玩家就诊动作)。
    /// <para><b>只驱动,不持游戏状态</b>(与 `BootRoot` 同纪律):开案真源在事件流,
    /// 本类只把「玩家对谁就诊」翻译成一次 writer 调用。</para>
    /// </summary>
    public sealed class CaseOpenedDriver
    {
        private readonly CaseOpenWriter _writer;

        /// <param name="writer">立案写者(37 的 `CaseOpened` 写者;在场 / 去重前置在其内部)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="writer"/> 为 null。</exception>
        public CaseOpenedDriver(CaseOpenWriter writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        /// <summary>
        /// 就诊 → 立案(玩家动作)。同一病人再次就诊 ⇒ <see cref="CaseOpenWriteResult.ReturnedExisting"/>,
        /// **零写入**(GDD 规则二 / `AC-37-26`「不新开案,回到那一例」)。
        /// </summary>
        /// <param name="patient">被就诊的病人。</param>
        /// <param name="tick">本次交互的逻辑 tick(立案时刻 = 该 tick)。</param>
        /// <returns>writer 的结果码(零写入分支亦具名)。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> 为负。</exception>
        public CaseOpenWriteResult TryOpen(PatientId patient, long tick)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "逻辑 tick 不得为负");

            return _writer.TryOpen(tick, patient, default);
        }
    }
}
