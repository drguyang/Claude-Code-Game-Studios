// sprint-07 S2 · 玩家交互输入驱动(E=查体 · F=急救 · 2026-10-11)
//
// 权威来源:
//   sprint-07 §2 S2.2(按 E 近病人 → 查体 → 值区有词 [8 真词表])
//   story-009(2026-10-14 用户裁定「按住 = 一次动作」):F 按下沿 → 直读通道 Arm;
//     松开沿 → EndAction 聚合上行(ADR-011 §二 直读通道独立于 42 UI 栈)
//   ADR-011 §一(action-based 官方路线过渡最小面 —— 同 MovementInputReader 登记:
//     动作资产 runtime 装载归独立 story,本驱动直读设备按键形状)
//   ADR-009 §七(意图 → 主机判距 → 效果进流 · 单机本地路径)
//   ADR-013 §9 C3(呈现侧只渲染不持状态 —— 本驱动只翻拍按键,零 sim 真值)
//
// ⚠️ 本驱动 = 呈现侧装配件(住 Gameplay.Boot):把「玩家按键意图」翻译成通道预约 /
//   查体调用(ExamineDriver.OnExamine / EmergencyChannelDriver.Arm / EndAction),零 sim 状态。
//   「按下沿」判据 = 上帧按住位比对(持键不重复触发 —— 查体是「一次交互一次动作」;
//   急救 = 按住一次,事件在松开沿聚合上行)。
//
// ⚠️ **定位源(接线期) = 病人视觉实体位置**(PatientVisualSpawner.TryGetVisualPosition,
//   占位环);真实位置归 6 世界生态区 + 13 空间侧。最近者 = 距玩家位置的平方距离最小者
//   (纯呈现侧读数,不写世界流 —— ADR-020 §四 位移纯表现态)。
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

        /// <summary>模态门(规则七方向②;可 null = 未接线形态 —— 那时不做模态抑制)。</summary>
        private readonly ModalGate _gate;

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
        public InteractionInputDriver(CompositionRootServices services,
                                      Func<Vector3> playerPosition,
                                      int actorId,
                                      ModalGate gate = null)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _playerPosition = playerPosition ?? throw new ArgumentNullException(nameof(playerPosition));
            _actorId = actorId;
            _gate = gate;
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
                TryExamine();
            _interactHeld = interact;

            bool emergency = PlayerInputReader.EmergencyPressed();
            if (emergency && !_emergencyHeld && allowed)
                BeginEmergency();
            else if (!emergency && _emergencyHeld)
                EndEmergency();
            _emergencyHeld = emergency;

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

            _services.EmergencyChannel.Arm(actionOrdinal: 0, target);   // 0 = 止血包扎(真表归 21a/10)
            LastEmergencyPatient = target;
        }

        /// <summary>F 松开沿:EndAction 聚合上行(零样本 / 不在场 ⇒ 零事件,零提示纪律)。
        /// 已被中止(超阈值)时通道已回 Idle ⇒ 此处幂等 no-op(中止不发,规则六之甲)。</summary>
        private void EndEmergency()
        {
            if (_services.EmergencyChannel == null) return;
            if (!_services.EmergencyChannel.IsArmed) return;
            _services.EmergencyChannel.EndAction(_actorId);
        }

        private void TryExamine()
        {
            if (_services.ExamineDriver == null) return;
            PatientId target = NearestPresentPatient();
            if (target.Value < 0) return;

            _services.ExamineDriver.OnExamine(target, CurrentTick);
            // sprint-08 S1.1 件 B:查体完成 ⇒ 记「见过病 D」(诊断成长落病史流)。
            // ⚠️ 次序:OnExamine 先(病人须已建档 + 快照落册),投影器再读 Courses 取病种 D。
            //   投影器 fail-loud(病人未建档 ⇒ PatientCourseNotFoundException)—— 但 OnExamine
            //   已先 GetVitals(同款 fail-loud),故此处病人在场性已被前置判据兜住。
            _services.SeenDisease?.OnExamined(target, CurrentTick, _actorId);
            _services.CasebookSource?.ChangePatient(target);
            _services.PrescriptionPage?.BindPatient(target);   // 方笺页对象同步(S2.3/S2.4)
            LastExaminedPatient = target;
        }

        /// <summary>最近在场病人(视觉实体位置 × 玩家位置的平方距离最小;接线期定位源
        /// = 视觉实体 —— 见文件头注)。</summary>
        private PatientId NearestPresentPatient()
        {
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

        private long CurrentTick => _services.TickDriver?.CurrentTick ?? 0L;
    }
}