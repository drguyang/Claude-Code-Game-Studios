// sprint-07 S2 · 玩家交互输入驱动(E=就诊 · F=急救 · 2026-10-11)
// 🔁 2026-11-02 · 开册入口轮(结清 `TD-casebook-01`):E 键从「查体」提升为**就诊**
//    —— 一次按下 = ① 立案(`CaseOpened` 入病例流)② 查体(就近,P0 代模态内行级动作)
//    ③ 开册(`OpenModal(ModalId.Casebook)`)。此前 E **只**查体:病例由 tick 自动开、
//    脉案屏谁都打不开(`case-system.md:176`「立案是一次玩家动作」被架空)。
//    ⚠️ **残余偏差已登记**:`S-8.4` 路线甲说「查体 = 脉案模态行级动作」,而 P0 由世界 E 代做
//    (模态内行级查体入口不存在)。本偏差**不**由本轮的立案/开册改动消除,见 `TD-casebook-02`。
//
// 权威来源:
//   sprint-07 §2 S2.2(按 E 近病人 → 查体 → 值区有词 [8 真词表])
//   diagnosis-system.md S-8.4(路线甲:世界空间裸 Interact = 就诊,单义)
//   casebook-39.md §3 入口 / §7 交互表(开册(就诊) 路由 37,立案时刻 = 该 tick)
//   case-system.md 规则二-A / :176(立案是一次玩家动作)
//   story-009(2026-10-14 用户裁定「按住 = 一次动作」):F 按下沿 → 直读通道 Arm;
//     松开沿 → EndAction 聚合上行(ADR-011 §二 直读通道独立于 42 UI 栈)
//   ADR-011 §一(action-based 官方路线过渡最小面 —— 同 MovementInputReader 登记:
//     动作资产 runtime 装载归独立 story,本驱动直读设备按键形状)
//   ADR-009 §七(意图 → 主机判距 → 效果进流 · 单机本地路径)
//   ADR-013 §9 C3(呈现侧只渲染不持状态 —— 本驱动只翻拍按键,零 sim 真值)
//
// ⚠️ 本驱动 = 呈现侧装配件(住 Gameplay.Boot):把「玩家按键意图」翻译成通道预约 /
//   就诊(立案 + 开册)/ 急救通道(EmergencyChannelDriver.Arm / EndAction),零 sim 状态。
//   **立案判定不在本类** —— 本类只调 writer 缝,判定归 37。
//   🔁 2026-11-02:**查体不在本类**(`TD-diagnosis-01`)—— 世界 E 只到「开册」,
//   查体归脉案六行的 Submit(`ICasebookCommandSink.ExamineChannel`)。
//   「按下沿」判据 = 上帧按住位比对(持键不重复触发 —— 查体是「一次交互一次动作」;
//   急救 = 按住一次,事件在松开沿聚合上行)。
//
// 🔁 2026-11-02(结清 `TD-interaction-01`):**E 打给谁不再由本类决定** ——
//   `routeInteractPress` 接到 `InteractionPressRouter`,那里一次 `InteractionSelector.Select`
//   按 F-4.1 三键定种类。本类只在**未接线**形态下退回「最近在场病人」(夹具用)。
//
// ⚠️ **定位源**:**生产 = 整数格**(`PresenceRegistry.TryGetCell` + 玩家经流确立格;
//   4 规则二「玩家格取路 (a)」)。旧注写的「视觉实体位置 × 玩家 transform 平方距离」现在
//   只剩**夹具回落**(未注入 `playerCell` 时),因为 `PatientVisualSpawner` 的占位环是表现态量。
//
// ⚠️ **S2.3(落笔→墨龄)/ S2.4(开方→处方)不在此文件**:那两个入口的「玩家操作」在 42 侧
//   (方笺落笔 / 戥子选档确认),是 UI 事件 → 装配层的接线故事 —— 本驱动只接**设备级** E/F;
//   不做自动行使(自动开方/落笔 = 玩家零输入出事件 = 假绿,承「灰盒测不对」根因)。
//   S2.3/S2.4 的 UI 消费面为沉淀下一拍(42 接线轮),本文件登记为缺口。

using System;
using UnityEngine;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Gameplay.Interaction;   // ModalGate(规则七方向②)
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 玩家交互输入驱动 —— 每帧读 E/F 按下沿 → 换算为查体 / 急救调用。
    /// <para>只读设备(承 <see cref="PlayerInputReader"/>),不碰动作资产;
    /// 持「上帧按住位」布尔(纯输入边沿状态,零 sim 态)。</para>
    /// </summary>
    public sealed class InteractionInputDriver
    {
        private readonly CompositionRootServices _services;
        private readonly Func<Vector3> _playerPosition;
        private readonly int _actorId;

        private bool _interactHeld;
        private bool _emergencyHeld;
        private bool _referHeld;

        /// <summary>当前选中的急救动作(<c>EmergencyAction</c> ordinal;B2-b,2026-11-10)。
        /// <para>默认 0 = 止血包扎 —— 与 B2-b 之前的硬编码逐位相同,故「不按数字键」的玩家
        /// 行为零变化。</para></summary>
        private int _selectedAction;

        /// <summary>上一帧的动作选择键(-1 = 未按)。边沿化用 —— 按住同一数字键不重复换动作。</summary>
        private int _lastActionKey = -1;

        /// <summary>模态门(规则七方向②;可 null = 未接线形态 —— 那时不做模态抑制)。</summary>
        private readonly ModalGate _gate;

        /// <summary>E 按下沿的**统一路由**(2026-11-02 · 结清 `TD-interaction-01`;可 null = 未接线)。
        /// <para>实参由装配层接到 <see cref="InteractionPressRouter.OnPress"/> —— 那里
        /// **一次 `InteractionSelector.Select` 定种类**,再按种类分派到 39 / 17 / 20。
        /// 🔁 本段的旧形态是「采集 claim 钩子」:`Func&lt;bool&gt;`,采集链先挑一遍、
        /// 挑不到才落回本类的 `NearestPresentPatient` —— 那就是两处判据。
        /// </para>
        /// <para>⚠️ 边沿位**仍由本类持有**(单一边沿所有者);钩子只在边沿内被调。</para></summary>
        private readonly Func<bool> _routeInteractPress;

        /// <summary>就诊 → 立案(2026-11-02 · `TD-casebook-01`;可 null = 未接线)。
        /// <para>参数 = (被就诊的病人, 立案 tick)。实参由装配层接到
        /// `CaseOpenedDriver.TryOpen` —— 本类**不做**立案判定(在场 / 去重归 37 的 writer)。</para></summary>
        private readonly Action<PatientId, long> _caseOpen;

        /// <summary>开册(2026-11-02 · `TD-casebook-01`;可 null = 未接线)。
        /// <para>实参由装配层接到 42 的 `SkeuoRuntimeDriver.OpenModal(ModalId.Casebook)`。
        /// ⚠️ 本类只发「开」这一个意图,不读相机 / 不持模态态(ADR-013 §9 C3)。</para></summary>
        private readonly Func<bool> _openCasebook;

        /// <summary>玩家**经流确立格**来源(2026-11-02 新增;可 null = 未注入 ⇒ 回落视觉位置)。
        /// <para>用途 = 急救 Arm 的目标选择(全序第一键 `d∞` 的输入)与「未接线」兜底的最近者。
        /// 🔁 旧形态只有 <see cref="_playerPosition"/> 一路(表现态连续位置)—— 与 4 规则二
        /// 「玩家格取路 (a) = 经流确立的格」冲突;本轮把**生产**路径换成格,视觉位置只留作夹具回落。</para></summary>
        private readonly Func<WorldPos> _playerCell;

        /// <summary>R 按下沿 → **转诊 / 送医**(C3-a · 2026-11-10;可 null = 未接线)。
        /// <para>实参由装配层接到 `HookCaseReferDriver.TryRefer` —— 本类**不做**病例查找,
        /// 也不判「这一例能不能转」(那归 37 的写者 + 本案是否仍开着)。</para>
        /// <para>⚠️ ADR-044 §D7 的「零调用方」说的正是这条缝:本缝接上之前,
        /// `HookCaseClosureWriter` 没有任何生产调用点。</para></summary>
        private readonly Action<PatientId> _routeReferPress;

        /// <summary>最近一次被查体的病人(观测面/测试接缝;None = 从未触发)。</summary>
        public PatientId LastExaminedPatient { get; private set; } = PatientId.None;

        /// <summary>最近一次被急救的病人(同上)。</summary>
        public PatientId LastEmergencyPatient { get; private set; } = PatientId.None;

        /// <param name="services">组合根服务袋(驱动调其 ExamineDriver /
        /// EmergencyManualDriver / Presence / PatientVisualSpawner / CasebookSource / TickDriver)。</param>
        /// <param name="playerPosition">玩家位置读数(接线面 = PlayerController.transform 转读)。</param>
        /// <param name="actorId">玩家 actorId(急救施予者;机制 A id 空间)。</param>
        /// <param name="gate">
        /// 模态门(规则七:¬Armed ∧ ¬模态开)。<c>null</c> = 未接线 —— 则是**未接线形态**,不做抑制。
        /// <para>🔁 2026-10-31:此前本驱动**完全不看模态** ⇒ 脉案/存档位开着时按 E 仍会查体,
        /// 违反 F-4.2「模态期按下的交互 = 没发生」。</para>
        /// </param>
        /// <param name="routeInteractPress">
        /// E 按下沿的统一路由(2026-11-02;可 null = 未接线)。
        /// <para>调用次序:模态门放行后。**接线时本类不再自行决定「打给谁」** ——
        /// 种类由 <see cref="InteractionPressRouter"/> 的一次 `Select` 定;本类只在
        /// **未接线形态**(夹具)下退回「最近在场病人」,见 <see cref="OnFrame"/> 的注。</para>
        /// </param>
        /// <param name="caseOpen">就诊 → 立案(2026-11-02;`null` = 未接线 ⇒ 不立案,
        /// 与「玩家没交互」同形)。</param>
        /// <param name="openCasebook">开册(2026-11-02;`null` = 未接线 ⇒ 只立案不开屏)。</param>
        public InteractionInputDriver(CompositionRootServices services,
                                      Func<Vector3> playerPosition,
                                      int actorId,
                                      ModalGate gate = null,
                                      Func<bool> routeInteractPress = null,
                                      Action<PatientId, long> caseOpen = null,
                                      Func<bool> openCasebook = null,
                                      Func<WorldPos> playerCell = null,
                                      Action<PatientId> routeReferPress = null)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _playerPosition = playerPosition ?? throw new ArgumentNullException(nameof(playerPosition));
            _actorId = actorId;
            _gate = gate;
            _routeInteractPress = routeInteractPress;
            _caseOpen = caseOpen;
            _playerCell = playerCell;
            _openCasebook = openCasebook;
            _routeReferPress = routeReferPress;
        }

        /// <summary>
        /// 本帧的交互意图能不能落地(F-4.2)。
        /// <para>⚠️ 传 <see cref="InteractIntent.None"/>:`ModalGate.Accept` 只读两个门
        /// (`_armed` / `_modal`),**不看意图内容** —— 这里问的是「现在能不能交互」,
        /// 不是「这个意图能不能过」。未接线(`_gate == null`)⇒ 放行(诚实降级)。</para>
        /// </summary>
        private bool IntentAllowed()
            => _gate == null || _gate.Accept(InteractIntent.None);

        /// <summary>
        /// 每帧处理玩家交互输入(由 <c>BootRoot.Update</c> 转发)。
        /// </summary>
        public void OnFrame()
        {
            // 模态/Armed 期:意图**丢弃**(F-4.2 —— 不排队、不缓存、不重放)。
            // ⚠️ 边沿位照常更新:模态期间按住键、关掉模态后**不补触发**(否则等于缓存了意图)。
            bool allowed = IntentAllowed();

            bool interact = PlayerInputReader.InteractPressed();
            if (interact && !_interactHeld && allowed)
            {
                // 🔁 2026-11-02(结清 `TD-interaction-01`):**一次路由**决定「这一下打给谁」。
                //   接线时本类不再自己挑病人 —— 种类归 4 的 `InteractionSelector`(全序三键)。
                // ⚠️ 未接线(`null`)⇒ 退回「最近在场病人」的就诊:那是**夹具形态**
                //    (`interaction_input_chain_test` 等直接构造本类、不建路由),
                //    不是生产路径 —— 生产由 `BootRoot` 恒接 `InteractionPressRouter`。
                //    模态门已由 allowed 前置:模态开 ⇒ 本分支不进 ⇒ 各支同被抑制(F-4.2)。
                if (_routeInteractPress != null)
                    _routeInteractPress();
                else
                    Visit(NearestPresentPatient());
            }
            _interactHeld = interact;

            bool emergency = PlayerInputReader.EmergencyPressed();

            // ── B2-b(2026-11-10):动作选择键 ─────────────────────────────────
            // 语义 = **按住急救键期间换动作 = 当前动作作废、用新动作重来**。
            // 为什么不是「中途改 arm」:直读通道的 `Arm` 在非 Idle 态**抛**
            // (`EmergencyDirectReadChannel.Arm`:重复预约 = 编程错误),故只能
            // Abort(丢弃累计、不发) → Arm(新动作)。这也符合手感 —— 做到一半换动作,
            // 之前的拍子本就不该算进新动作。
            int actionKey = PlayerInputReader.EmergencyActionPressed();
            bool actionSwitched = false;
            if (actionKey >= 0 && actionKey != _lastActionKey)
            {
                _selectedAction = actionKey;
                actionSwitched = true;
            }
            _lastActionKey = actionKey;

            if (emergency && !_emergencyHeld && allowed)
                BeginEmergency();
            else if (!emergency && _emergencyHeld)
                EndEmergency();
            else if (emergency && actionSwitched && allowed)
                SwitchEmergencyAction();
            _emergencyHeld = emergency;

            // ── C3-a(2026-11-10):转诊 / 送医键 ──────────────────────────────
            bool refer = PlayerInputReader.ReferPressed();
            if (refer && !_referHeld && allowed && _routeReferPress != null)
                _routeReferPress(NearestPresentPatient());
            _referHeld = refer;

            // 观测位**不清零**:Last* = 「最近一次触发」的观测面(测试读走最近一次,
            // 而非「仅本帧」)。初始 None ⇒ 从未触发恒 None(EditMode 空转判据不依赖清除)。
        }

        /// <summary>F 按下沿(story-009 · 2026-10-14 用户裁定「按住 = 一次动作」):
        /// 预约直读通道(Arm)+ 锁定最近在场病人。无在场病人 ⇒ 不预约(Idle 门不动)。
        /// ⚠️ 本沿**零事件** —— 事件只在松开沿聚合上行(或超阈值中止)。</summary>
        private void BeginEmergency()
        {
            if (_services.EmergencyChannel == null) return;
            PatientId target = NearestPresentPatient();
            if (target.Value < 0) return;

            _services.EmergencyChannel.Arm(actionOrdinal: _selectedAction, target);   // B2-b:选中的动作(默认 0 = 止血包扎)
            LastEmergencyPatient = target;
        }

        /// <summary>
        /// 按住急救键期间换动作(B2-b · 2026-11-10)。
        /// <para><b>为什么是 Abort + Arm 而不是「改 arm」</b>:直读通道的 `Arm` 在非 Idle 态
        /// **抛**(重复预约 = 编程错误,见 `EmergencyDirectReadChannel.Arm` 的守卫),故换动作
        /// 只能「作废当前这次、用新动作重开」。语义上也对:做到一半换动作,之前的拍子
        /// 本就不该算进新动作。</para>
        /// <para>⚠️ <b>Abort = 丢弃累计、不发事件</b>(规则六之甲 / F-10.5 附注)——
        /// 换动作**不产生**一条 `EmergencyAttempt`。若玩家换完就松手,本次动作零事件。</para>
        /// <para>目标沿用 <see cref="LastEmergencyPatient"/>(Arm 时锁定的那个人)——
        /// 换动作不该换病人,否则玩家会在「动作选择」里意外改变目标。</para>
        /// </summary>
        private void SwitchEmergencyAction()
        {
            if (_services.EmergencyChannel == null) return;
            if (!_services.EmergencyChannel.IsArmed) return;

            PatientId target = LastEmergencyPatient;
            if (target.Value < 0) return;

            _services.EmergencyChannel.AbortAction();
            _services.EmergencyChannel.Arm(actionOrdinal: _selectedAction, target);
        }

        /// <summary>F 松开沿:EndAction 聚合上行(零样本 / 不在场 ⇒ 零事件,零提示纪律)。
        /// 已被中止(超阈值)时通道已回 Idle ⇒ 此处幂等 no-op(中止不发,规则六之甲)。</summary>
        private void EndEmergency()
        {
            if (_services.EmergencyChannel == null) return;
            if (!_services.EmergencyChannel.IsArmed) return;
            _services.EmergencyChannel.EndAction(_actorId);
        }

        /// <summary>
        /// 世界空间一次就诊(2026-11-02 起 = 立案 + 开册;此前只有查体)。
        /// <para>次序 = **① 立案 → ② 切病人 → ③ 开册**,每一步不依赖后一步:</para>
        /// <list type="number">
        ///   <item><b>立案</b> —— `CaseOpened` 入病例流(`S-8.4` 单义:世界 Interact 只是就诊)。
        ///        病人不在场 / 已有开案 ⇒ writer 返回具名零写入,不新开案(`AC-37-26`)。</item>
        ///   <item><b>切病人</b> —— 把脉案 / 方笺指向这个人(`CasebookSource.ChangePatient`)。</item>
        ///   <item><b>开册</b> —— 42 的 `OpenModal(Casebook)`(`casebook-39.md` §7「开册(就诊)」)。</item>
        /// </list>
        /// <para>⚠️ 立案**先于**开册:屏一打开就在读数据源,顺序反了会开出一次空册。</para>
        /// <para>🔁 2026-11-02(结清 `TD-interaction-01`):本方法**公开**且**由调用方指名病人** ——
        /// 「打给谁」归 <see cref="InteractionPressRouter"/> 的一次 `Select`,
        /// 本类不再自己找最近者(那会是与 4 并列的第二处选择判据)。</para>
        /// </summary>
        /// <param name="target">被就诊的病人(由 4 的目标选择器给出)。</param>
        public void Visit(PatientId target)
        {
            if (target.Value < 0) return;

            // ① 就诊 / 立案(规则二-A:立案是一次玩家动作)
            _caseOpen?.Invoke(target, CurrentTick);

            // ② 切脉案 / 方笺的当前病人(把册子指向这个人)
            // 🔁 2026-11-02(结清 `TD-diagnosis-01`):**世界按键不再查体**。
            //    `S-8.4` 路线甲逐字:「世界空间裸 `Interact` = 就诊(单义);**查体 = 脉案模态
            //    行级动作**」⇒ 查体入口只余 42 的六行 Submit(`ICasebookCommandSink.ExamineChannel`
            //    → `CasebookCommandRouter` → `ExamineDriver.ExamineChannel`)。
            //    ⚠️ `LastExaminedPatient` 因此**只在行级查体时才被写** —— 世界按键恒不写它,
            //       这正好是「世界 E 只立案 + 开册」的观测判据。
            _services.CasebookSource?.ChangePatient(target);
            _services.PrescriptionPage?.BindPatient(target);   // 方笺页对象同步(S2.3/S2.4)

            // ③ 开册(42 的唯一入口;返回 false = 无面板 / 未接线 —— 不抛,不静默改立案)
            _openCasebook?.Invoke();
        }

        /// <summary>
        /// 最近的在场病人 —— **急救 Arm 的目标**,以及「未接线」形态下 E 的就诊对象。
        /// <para><b>生产分支(注入玩家格)= 整数格 argmin</b>:读
        /// <c>PresenceRegistry.TryGetCell</c> + 玩家**经流确立格**(4 规则二「玩家格取路 (a)」·
        /// ADR-016 §三)。同距时取 <c>patient_id</c> 小者(可重放,不依赖枚举序)。</para>
        /// <para><b>夹具分支(未注入玩家格)= 视觉位置平方距离</b>:保留旧行为,供直接构造本类的
        /// 夹具使用(<c>interaction_input_chain_test</c> 等)。⚠️ 该分支**不供生产** ——
        /// 生产装配恒注入 `() =&gt; _player.LastCommittedCell`。</para>
        /// </summary>
        private PatientId NearestPresentPatient()
        {
            if (_playerCell != null) return NearestPresentPatientByCell();

            var spawner = _services.PatientVisualSpawner;
            if (spawner == null) return PatientId.None;

            Vector3 origin = _playerPosition();
            PatientId best = PatientId.None;
            float bestSqr = float.MaxValue;

            foreach (int pid in _services.Presence.PresentPatientIds())
            {
                if (!spawner.TryGetVisualPosition(pid, out Vector3 pos)) continue;
                float sqr = (pos - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = new PatientId(pid);
                }
            }
            return best;
        }

        /// <summary>整数格最近者(F-4.1 第一键的口径:`d∞`;同距取 <c>patient_id</c> 小者)。</summary>
        private PatientId NearestPresentPatientByCell()
        {
            if (_services.Presence == null) return PatientId.None;

            WorldPos from = _playerCell();
            PatientId best = PatientId.None;
            long bestDistance = long.MaxValue;

            foreach (int pid in _services.Presence.PresentPatientIds())
            {
                if (!_services.Presence.TryGetCell(new PatientId(pid), out WorldPos cell)) continue;

                long dx = Math.Abs((long)from.X - cell.X);
                long dy = Math.Abs((long)from.Y - cell.Y);
                long dz = Math.Abs((long)from.Z - cell.Z);
                long m = dx > dy ? dx : dy;
                if (m < dz) m = dz;

                if (m < bestDistance || (m == bestDistance && best.Value >= 0 && pid < best.Value))
                {
                    bestDistance = m;
                    best = new PatientId(pid);
                }
            }
            return best;
        }

        private long CurrentTick => _services.TickDriver?.CurrentTick ?? 0L;
    }
}