// 手册近景(42 的 `PaperCloseup48`)的**内容宿主** —— T10 · 2026-11-09
//
// 权威:`design/gdd/reviews/technique-unlock-map-2026-11-09.md` §5.2(钩子病例的载体 = 手册缺页)
//       · ADR-013 §9 C3(42 只渲染,永不持有游戏状态)· §十-B(`ModalId.PaperCloseup48` = 第七员)
//       · ADR-043 §D7(`ITechniqueUnlockQuery` = 病史流折叠投影,同源可重建)
//
// ── 本件补的是什么断点(2026-11-09 侦察,三处零消费者)──────────────────────
// ① `PaperCloseupScreen.Bind(papers)` —— 零生产调用方;
// ② `PaperCloseupScreen.SetMissingPaperIds(...)` —— 零生产调用方;
// ③ `TutorialPaperMissing.Compute(...)` —— 零生产调用方。
// ⇒ T6 会画、T8 会判、48 有表,**三者之间没有任何一根线**。本件就是那根线。
//
// ── 为什么「缺页」只有一个判据源 ──────────────────────────────────────────
// 钩子病例侧也有一个「缺哪些技法」(`PatientDrawDecision.MissingTechniques`),**不**接进来:
// 它的定义 = 「病种 `treatable_by` 里玩家还没解锁的技法」,而 `TutorialPaperMissing` 的定义 =
// 「教某门技法、而该技法还没解锁的页」——**前者是后者的子集**(没解锁的技法若有教学页,
// 那一页必缺)。接两个源 = 同一事实两个口径,迟早一个说缺一个说不缺。
// ⇒ 屏只收 `TutorialPaperMissing` 的结果。钩子病例「指出**哪一门**」的细分(要高亮其中一页)
//    今日**没有呈现载体**(屏的缺页态是二值的撕口)—— 如实登记为未做,不造第三种状态。
//
// ── ⚠️ 仍不交可见行为 ─────────────────────────────────────────────────────
// 本件交付的是**宿主**:`TryPresent` 被调用时,开屏 + 喂数据 + 标缺页一次做完。
// 「走近摊纸」的世界交互(4 的交互点 + 关卡布置)仍然**不存在**。
// 🔁 **2026-11-10(T18 甲)订正**:「玩家今天打不开这本手册」已不成立 ——
//   开屏入口 = `M` / 手柄 `select` → `PaperCloseupToggleDriver` → `BootRoot.TryPresentManual()`。
//   本段原文留档:它当时说的是「没有**任何**入口」,那一半已经补掉了;
//   还缺的只是「走近摊纸」这种**世界内**的入口(C1-a)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Tutorial;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>手册近景一次的呈现快照(纸条目 + 缺页 id 集)。</summary>
    /// <remarks>
    /// 做成值快照而不是「让屏自己读」的理由:屏不该看见 `ITechniqueUnlockQuery`
    /// (那是 sim 侧的读面),也不该看见内容表(那是数据面)。装配层算好、一次性交出去。
    /// </remarks>
    public readonly struct PaperCloseupSnapshot
    {
        /// <summary>纸条目(48 内容表的 `papers[]`;可空集)。</summary>
        public readonly IReadOnlyList<TutorialPaperRow> Papers;

        /// <summary>缺页的纸条目 id(可空集 = 无缺页)。</summary>
        public readonly IReadOnlyList<string> MissingPaperIds;

        /// <summary>构造。</summary>
        public PaperCloseupSnapshot(IReadOnlyList<TutorialPaperRow> papers,
                                    IReadOnlyList<string> missingPaperIds)
        {
            Papers = papers ?? throw new ArgumentNullException(nameof(papers));
            MissingPaperIds = missingPaperIds ?? throw new ArgumentNullException(nameof(missingPaperIds));
        }
    }

    /// <summary>
    /// 手册近景的内容宿主:算缺页 → 开屏 → 喂屏。
    /// </summary>
    /// <remarks>
    /// <para><b>不持游戏状态</b>:每次 <see cref="Build"/> 都从内容表 + 解锁查询现算,
    /// 无缓存(承 ADR-013 §9 C3 与 AC-53-06 的同款理由 —— 读档后同刻结果一致)。</para>
    /// <para><b>查询缺席 = 装配错误,不是「没有缺页」</b>:<see cref="TutorialPaperMissing"/> 的
    /// 纪律是「查询未注入必须抛」,否则「系统没接上」会被渲染成「整本手册完好」——
    /// 那是最坏的一种假绿。本类在构造期拒收 null。</para>
    /// </remarks>
    public sealed class PaperCloseupPresenter
    {
        private readonly IReadOnlyList<TutorialPaperRow> _papers;
        private readonly ITechniqueUnlockQuery _unlocks;
        private readonly Func<int> _actorIdSource;

        /// <summary>构造。</summary>
        /// <param name="papers">纸条目(48 内容表的 `papers[]`;与教学链**同一个只读视图**,
        /// 经 `TutorialChainSeams.Content.Papers`)。</param>
        /// <param name="unlocks">技法解锁只读查询(ADR-043 §D7);**不得为 null**。</param>
        /// <param name="actorIdSource">玩家 id(机制 A 发号;迟绑定 —— 装配期还没有)。
        /// 返回负值 = 尚未绑定 ⇒ <see cref="Build"/> 抛。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public PaperCloseupPresenter(IReadOnlyList<TutorialPaperRow> papers, ITechniqueUnlockQuery unlocks,
                                     Func<int> actorIdSource)
        {
            _papers = papers ?? throw new ArgumentNullException(nameof(papers));
            _unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks),
                "技法解锁查询未注入 —— 把「没接上」渲染成「整本手册完好」是最坏的一种假绿");
            _actorIdSource = actorIdSource ?? throw new ArgumentNullException(nameof(actorIdSource));
        }

        /// <summary>纸条目总数(观测面;0 = 内容表里一张纸都没有)。</summary>
        public int PaperCount => _papers.Count;

        /// <summary>
        /// 现算一次呈现快照。
        /// </summary>
        /// <returns>纸条目 + 缺页 id 集(缺页 ⇔ 教会某门技法且该技法未解锁)。</returns>
        /// <exception cref="InvalidOperationException">玩家 id 尚未绑定(返回了负值)。</exception>
        public PaperCloseupSnapshot Build()
        {
            int actorId = _actorIdSource();
            if (actorId < 0)
                throw new InvalidOperationException(
                    "玩家 id 尚未绑定(actorIdSource 返回 " + actorId + ") —— " +
                    "手册缺页的判定以「**这个**玩家学会了什么」为前提,无业主时不得算");

            IReadOnlyList<string> missing = TutorialPaperMissing.Compute(
                _papers, techniqueId => _unlocks.IsUnlocked(actorId, techniqueId));

            return new PaperCloseupSnapshot(_papers, missing);
        }

        /// <summary>
        /// 打开手册近景并喂数据(缺页态一并标上)。
        /// </summary>
        /// <param name="ui">42 的运行时驱动(装配层持有)。</param>
        /// <returns><c>true</c> = 屏已打开并喂好;<c>false</c> = 开屏被拒(已有更高优先级模态)。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ui"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">模态开出来了但屏不是
        /// <see cref="PaperCloseupScreen"/> —— 工厂注册错了,属接线错误。</exception>
        /// <remarks>
        /// ⚠️ **先算后开**:`Build()` 可能抛(玩家 id 未绑定)。若先开屏再算,
        /// 抛的时候屏已经摊开了 —— 玩家会看到一本空手册然后崩。故顺序不可换。
        /// </remarks>
        public bool TryPresent(SkeuoRuntimeDriver ui)
        {
            if (ui == null) throw new ArgumentNullException(nameof(ui));

            PaperCloseupSnapshot snapshot = Build();

            if (!ui.OpenModal(ModalId.PaperCloseup48)) return false;

            if (!(ui.Screen is PaperCloseupScreen screen))
                throw new InvalidOperationException(
                    "PaperCloseup48 开出来的屏不是 PaperCloseupScreen(实得 " +
                    (ui.Screen == null ? "null" : ui.Screen.GetType().Name) + ") —— 工厂注册错误");

            screen.Bind(snapshot.Papers);
            screen.SetMissingPaperIds(snapshot.MissingPaperIds);
            return true;
        }
    }
}
