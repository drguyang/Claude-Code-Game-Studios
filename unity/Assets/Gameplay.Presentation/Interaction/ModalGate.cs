// ADR 权威来源:
//   GDD design/gdd/interaction-system.md —— 规则七(模态抑制,两个方向)
//     / F-4.2(模态门 = 布尔合取)/ 规则九(4 无状态)/ 规则八(4 是 MotorSuppressed 合法调用者)
//   ADR-013 §十 Amendment A(`IModalState` / `ModalId` 闭集,42 侧权威声明)
//   ADR-011(`InteractIntent` 由 3 的动作映射供给)/ ADR-005(输入是意图源)
//   ADR-020 §四 对称(玩家位移 = 纯表现态;抑制的写方在 1)
//
// ⚠️ 三处承重(逐字承 story-005 Context):
//   ① **模态门 = 布尔合取**(F-4.2):`Accept(intent) ⟺ ¬Armed(10) ∧ ¬ModalOpen(42)` ——
//      两个输入都是**只读**的外部状态,4 不持有任何一份(规则九:4 不是模态的真源)。
//   ② **引用而非复制**(AC-4-09):4 侧**不得**出现 `ModalId` 成员名清单,只允许
//      `Modal ≠ ModalId.None` 这一个比较 ⇒ 闭集新增屏时 4 侧零改动。
//   ③ **丢弃而非排队**(F-4.2):模态期按下的交互 = **没发生**。不缓存、不重放、不倾泻。
//
// ⚠️ 程序集归属(承 ADR-025 §①):`IModalState` 的**实现**住 42(`Gameplay.UI.Skeuomorphic`,
//   那里已有 7 个 `*Screen` 实现体),但本门(4 侧消费点)住 `Gameplay.Presentation` ——
//   `Gameplay.UI` 引用 `Gameplay.Presentation`(单向),故 Presentation **不能**引用 UI 侧的接口
//   (否则循环)。⇒ 与 `IFocusable` 同处置(Presentation/Skeuomorphic/IFocusable.cs 逐字先例:
//   「住 Gameplay.Presentation 程序集 —— 避免 Gameplay.UI → Gameplay.Presentation 的循环引用」):
//   在 Presentation 侧声明**消费面契约** `IModalGateState`,由 42 的 `IModalState` 适配实现。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Interaction
{
    /// <summary>
    /// 模态门方向②的**消费面契约**(F-4.2 的 `ModalOpen(42)`)。
    /// <para>只读:一个 getter,零 setter —— 4 不写模态(真源 = 42 渲染层自身)。</para>
    /// <para><b>程序集归属</b>:住 `Gameplay.Presentation`(4 侧消费点所在层),
    /// 由 42 的 `IModalState` 适配实现 —— 避免 `Gameplay.UI` → `Gameplay.Presentation`
    /// 的循环引用(<see cref="DaYiJingCheng.Gameplay.Presentation.Skeuomorphic.IFocusable"/> 同处置)。
    /// ⚠️ <b>反转配对</b>(2026-10-04 结构侧 F-6):本契约以 <c>Gameplay.UI</c> 的 <c>IModalState</c>
    /// 为**适配源**,而 `Gameplay.UI` 单向引用 `Gameplay.Presentation` ⇒ 适配器必须住 42 侧
    /// (它两件都看得见),本层只声明**消费面**(只有本层看得见的那个布尔)。</para>
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>引用而非复制(AC-4-09)</b>:本契约只暴露**一个布尔开合** —— 4 侧**看不到**
    /// `ModalId` 的成员名清单,也看不到「哪一个模态」的读数 ⇒ 闭集新增屏时 4 侧零改动。
    /// 真枚举 `ModalId` 的权威声明唯在 42(ADR-013 §十 Amendment A),本层不复制其成员。
    /// </remarks>
    public interface IModalGateState
    {
        /// <summary>
        /// 是否有模态界面摊开(42 的 `Modal ≠ ModalId.None`,已收敛为 bool)。
        /// <para>本门只消费这个布尔 —— 不消费「哪一个模态」,故 4 侧不复制闭集成员名(AC-4-09)。</para>
        /// <para>⚠️ <b>刻意只有一个成员</b>(2026-10-04 结构侧 F-2):契约面上**没有**「哪一个模态」
        /// 的读数 —— 无 ordinal getter ⇒ 4 侧结构上<b>不可能</b>长出「按模态分派」的分支
        /// (AC-4-09「引用而非复制」的充分形态;多一个成员 = 多一处将来可能被分支的面)。</para>
        /// </summary>
        bool IsOpen { get; }
    }

    /// <summary>
    /// 模态门(规则七 · 两个方向的布尔合取,F-4.2)。
    /// <para><b>方向 ①</b>:10 的 `Armed` 态(10 侧只读)-> 意图被压制。</para>
    /// <para><b>方向 ②</b>:42 的模态开集(<see cref="IModalGateState"/> 只读)-> 意图被拒收。</para>
    /// <para><b>合取</b>:任一成立 ⇒ 意图<b>丢弃</b>(不排队、不缓存、不重放 —— F-4.2)。</para>
    /// </summary>
    /// <remarks>
    /// <para><b>无状态(规则九 / AC-4-04)</b>:本门只有 <c>readonly</c> 依赖引用,无任何可变字段 ——
    /// 不记「刚才拒了几个」,不缓存被拒的意图。清空重建后同输入 ⇒ 同输出。</para>
    /// <para><b>不持有门输入</b>:`Armed` 归 10,`ModalOpen` 归 42 —— 本门只**读**,
    /// 不是任何一方的真源(规则九表:4 不持有「现在是不是模态」)。</para>
    /// </remarks>
    public sealed class ModalGate
    {
        // ⚠️ 规则九结构半边:这里刻意只有两个 readonly 只读门,**零**「拒了几次」/「上次模态」记账。
        //   若有人加一个缓存被拒意图的队列字段 ⇒ 反射扫描(ScanForMutableState)即红,
        //   且「丢弃而非排队」的判据随之击穿(F-4.2:模态期按下的交互 = 没发生)。

        /// <summary>方向①:10 的 `Armed` 通道态(只读;真源在 10)。</summary>
        private readonly IArmedState _armed;

        /// <summary>方向②:42 的模态开集只读态(只读;真源在 42)。</summary>
        private readonly IModalGateState _modal;

        /// <summary>构造模态门(两个只读门各来自其真源系统)。</summary>
        /// <param name="armed">10 的 `Armed` 只读门。</param>
        /// <param name="modal">42 的模态开集只读门(<see cref="IModalGateState"/>)。</param>
        public ModalGate(IArmedState armed, IModalGateState modal)
        {
            _armed = armed ?? throw new ArgumentNullException(nameof(armed));
            _modal = modal ?? throw new ArgumentNullException(nameof(modal));
        }

        /// <summary>
        /// 意图是否被接受(F-4.2 的布尔合取):<c>Accept(intent) ⟺ ¬Armed(10) ∧ ¬ModalOpen(42)</c>。
        /// </summary>
        /// <param name="intent">玩家交互意图(未被本门消费时原样返回给选择器)。</param>
        /// <returns>
        /// <c>true</c> = 两门皆开 ⇒ 意图通过;两门任一闭 ⇒ <c>false</c>(意图<b>丢弃</b>)。
        /// </returns>
        public bool Accept(in InteractIntent intent)
        {
            // 方向①:10 的 Armed 期 ⇒ 压制(10 拥有,本门只读)。
            if (_armed.IsArmed) return false;

            // 方向②:42 的任一模态摊开 ⇒ 拒收(42 拥有,本门只读)。
            //   判据 = 一个布尔 —— 4 侧**不复制** ModalId 成员名清单(AC-4-09)。
            if (_modal.IsOpen) return false;

            return true;
        }

        /// <summary>
        /// 选择入口(门 + 选择器的**唯一合法接线**):门开 ⇒ 选择;门闭 ⇒ <see cref="InteractTarget.None"/>。
        /// <para>⚠️ 本方法实现「<b>丢弃而非排队</b>」:门闭时<b>不</b>把 intent 存起来 —— 直接返回 None,
        /// 意图就此消失(F-4.2:「模态期按下的交互 = 没发生」)。</para>
        /// </summary>
        /// <param name="selector">目标选择器(F-4.1 三键全序;story 002)。</param>
        /// <param name="intent">玩家交互意图。</param>
        /// <param name="candidates">候选集(派生态;story 003 装配)。</param>
        /// <returns>门开时的选择结果;门闭时 <see cref="InteractTarget.None"/>。</returns>
        public InteractTarget Select(
            InteractionSelector selector,
            in InteractIntent intent,
            System.Collections.Generic.IReadOnlyList<Candidate> candidates)
        {
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            if (!Accept(intent)) return InteractTarget.None;
            return selector.Select(intent, candidates);
        }

        /// <summary>
        /// 移动压制租约的**写侧调用面**(规则八 / AC-4-19)。
        /// <para>4 的调用面 = <c>Acquire(LeaseSource.Self)</c> / <c>Release(LeaseSource.Self)</c>
        /// —— <b>无</b> <c>SetSuppressed(bool)</c>(单 bool + 三个互不知晓的写者 = 丢失更新,
        /// 承 `OQ-4-13`)。</para>
        /// <para>⚠️ 本门**不持有租约** —— 租约(位图 + OR 聚合)归 1(玩家控制器);
        /// 本方法只是 4 侧调用入口,把 <see cref="LeaseSource.Self"/> 转发给 1 的 <c>MotorLease</c>。</para>
        /// </summary>
        /// <param name="lease">1 的 per-source 租约位图(真源在 1)。</param>
        /// <param name="acquire">true = 置 4 的位;false = 清 4 的位(只碰自己那一位)。</param>
        public static void SetMotorSuppression(
            DaYiJingCheng.Gameplay.Presentation.Player.MotorLease lease, bool acquire)
        {
            if (lease == null) throw new ArgumentNullException(nameof(lease));
            if (acquire)
                lease.Acquire(DaYiJingCheng.Gameplay.Presentation.Player.LeaseSource.Self);
            else
                lease.Release(DaYiJingCheng.Gameplay.Presentation.Player.LeaseSource.Self);
        }
    }

    /// <summary>
    /// 方向①的消费面契约(10 的 `Armed` 通道态,只读)。
    /// <para>只读:一个 getter,零 setter —— `Armed` 的状态本体归 10(10 自己置、自己清)。</para>
    /// <para>住 `Gameplay.Presentation`(4 侧消费点所在层)。10 的 `Armed` 是**边界层**态
    /// (GDD `:76` 「10 的 `Armed` 通道态(边界层)」),实现体归 10。</para>
    /// </summary>
    public interface IArmedState
    {
        /// <summary>急救动作是否处于 `Armed`(压制)态。</summary>
        bool IsArmed { get; }
    }
}
