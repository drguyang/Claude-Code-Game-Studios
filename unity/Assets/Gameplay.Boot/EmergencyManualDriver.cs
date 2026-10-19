// sprint-06 G4 · 急救手动入口驱动(2026-10-11)
//
// 权威来源:
//   production/sprints/sprint-06.md §2 G4.1/G4.2 —— 移除自动触发(零输入零事件)
//   ADR-009 Amendment I(急救三 Kind 属判定输入 / 处置事件,落病史流、归属具体病人)
//   ADR-005(主机唯一 Append)· emergency-procedures.md 规则十一
//   sprint-05 T1.3 原 `EmergencyAttemptDriver` 的 `_triggeredPatients` 自动分支
//     **已删除**(P0 接线载体退役)—— 触发权归玩家,本驱动只提供手动入口。
//
// 设计口径:
//   - 驱动 = **手动入口容器**(零 tick 边沿自动逻辑):玩家对在场病人完成急救意图
//     (42 候选列表落笔 / 直读通道 End 的翻译)→ `TryManual` → HostEmergencyProcessor.Process
//     → EmergencyAttempt + EmergencyTreatmentApplied 双事件入病史流。
//   - **零输入观察窗 ⇒ 病史流零 EmergencyAttempt**(判据 G4.1;本类无任何隐式触发)。
//   - actorId(施予者)与 method(Manual / Skip)由调用方传入 —— 与 PrescribeIntent 同构;
//     BootRoot 生成玩家后经此传入。
//   - 最小动作表 = 一张行(止血包扎 · action 0;真实动作表归 21a/10 烘焙轮)。
//
// ⚠️ 单线程:tick 由 BootRoot 泵驱动,本驱动只在玩家意图发生时被调用。
//
// ⚠️ 2026-10-19(TD-epic-01 第八片 · F3 cause 关联的**写侧**):`cause` 原**硬编码 0**
//   ⇒ 每一次跳过在流上都被标成「玩家选择」,AC-10-21 的「纯键鼠 ⇒ cause = 降级」半边
//   在生产里**恒假**(降级读数永远为 0)。现改为:
//     `cause = ResolveCause()` —— 3 报告「本次动作无模拟量通道」⇒ 降级;否则 ⇒ 玩家选择。
//   判据出处 = 10 GDD 规则十二 `:467`「设备无模拟量通道(纯键鼠,`OQ-10-3` 的回退路径)⇒ 降级」
//   + Edge Cases `:728`「`magnitude` 通道无输入设备(纯键鼠)⇒ 回退形状,`cause = 降级`」。
//   ⚠️ **未注入来源 ⇒ 记 玩家选择** —— 显式记为夹具默认(守卫 `test_cause_deviceReadinessNotBound_defaultsToPlayerSelection`),
//      不是判据。生产路径 BootRoot 启动序 2.9 必注入 3 的 `EmergencyDeviceReadiness`
//      (经 <see cref="BindDeviceReadiness"/>;本类构造第三参同型,供组合根 / 夹具直传)。
//      接口 `IEmergencyDeviceReadiness` **住 3**(Gameplay.Input;判据归通道拥有者),
//      Boot 已引 3 ⇒ 零新增引用边。

using System;
using DaYiJingCheng.Gameplay.Input;           // IEmergencyDeviceReadiness(cause 判据的 3 侧读数)
using DaYiJingCheng.Gameplay.Input.Intents;   // AggregatedEmergency(3 的聚合交出物,story-009)
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.EmergencyProcedures;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>一次手动急救的回执(只读;失败原因不回传 —— 零提示纪律 AC-11-12 同族)。</summary>
    public readonly struct EmergencyManualResult
    {
        /// <summary>是否走完 Process(双事件入流)。</summary>
        public readonly bool Applied;

        public EmergencyManualResult(bool applied)
        {
            Applied = applied;
        }
    }

    /// <summary>
    /// 急救手动入口驱动 —— 把玩家的一次急救意图接进 <see cref="HostEmergencyProcessor"/>
    /// (规则十一)。住装配层(可引 Sim.EmergencyProcedures + Sim.Codec 系)。
    /// </summary>
    public sealed class EmergencyManualDriver
    {
        /// <summary>`EmergencyTreatmentApplied.cause` 的「玩家选择」序数(GDD 规则十二;序数承既有测试)。</summary>
        public const int CausePlayerSelection = 0;

        /// <summary>`EmergencyTreatmentApplied.cause` 的「降级」序数(GDD 规则十二;AC-10-21)。
        /// 与 `FeelLatencyEvaluator.GetKeyboardFallbackCause()` 同值 —— 10 侧已钉 1,此处不另立新值。</summary>
        public const int CauseDegraded = 1;

        private readonly HostEmergencyProcessor _processor;
        private readonly IPresenceQuery _presence;

        /// <summary>3 的设备就绪读数(可空 —— 未注入 = 夹具默认「玩家选择」,见文件头注)。</summary>
        private IEmergencyDeviceReadiness _devices;

        /// <summary>最小动作表(action 0 = 止血包扎;真实表归 10/21a 烘焙轮)。</summary>
        private static readonly EmergencyActionRow BandageRow = new EmergencyActionRow
        {
            ActionId = 0,
            Polarity = 2,        // 平
            BasePotency = 65536, // 1.0 in Q16.16
            MagThreshold = 50,
            MinEdges = 1,
            MinHoldTicks = 5,
            HalfLifeTicks = 100,
            DurationTicks = 200,
            ResultMul = new long[] { 0, 32768, 65536 }, // Missed/AppliedWeak/Applied
        };

        /// <param name="processor">主机侧急救处理器(规则十一)。</param>
        /// <param name="presence">在场查询 —— 急救前置:病人须在场
        /// (C3-M1;与处方侧 <c>PrescribeFlow</c> 1c 对称)。</param>
        /// <param name="devices">3 的设备就绪读数(判 `cause` 用;**可空** = 夹具默认「玩家选择」)。
        /// 生产由 BootRoot 以 <see cref="DeviceStateManager"/> 适配后注入。</param>
        public EmergencyManualDriver(HostEmergencyProcessor processor, IPresenceQuery presence,
                                     IEmergencyDeviceReadiness devices = null)
        {
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
            _presence = presence ?? throw new ArgumentNullException(nameof(presence));
            _devices = devices;
        }

        /// <summary>后置注入设备读数(BootRoot SetAuthor 同款形态 —— 设备态须先挂接才能读到在场设备)。
        /// 单次装配内恰一次;重复注入 = 编程错误(两个真源)。</summary>
        /// <exception cref="ArgumentNullException"><paramref name="devices"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">已注入过。</exception>
        public void BindDeviceReadiness(IEmergencyDeviceReadiness devices)
        {
            if (devices == null) throw new ArgumentNullException(nameof(devices));
            if (_devices != null)
                throw new InvalidOperationException("EmergencyManualDriver 已注入设备读数,不可重复注入。");
            _devices = devices;
        }

        /// <summary>当次动作的 `cause`(GDD 规则十二判据;见文件头注 —— 降级 ⇔ 无模拟量通道)。</summary>
        public int ResolveCause()
        {
            return IsAnalogChannelAbsent ? CauseDegraded : CausePlayerSelection;
        }

        /// <summary>当次动作是否**无模拟量通道**(键鼠回退形状;AC-10-21 的幅度半边)。
        /// <para>这是**判据**;`cause` 是它的投影(<see cref="ResolveCause"/>)。
        /// 两者分开传,是因为「未注入设备读数」时 `cause` 必须落到「玩家选择」(夹具默认),
        /// 而幅度门**不得**跟着豁免 —— 不知道就不回填(宁严勿猜)。
        /// 生产启动序 2.9 注入后两者恒同源。</para></summary>
        public bool IsAnalogChannelAbsent => _devices != null && _devices.IsAnalogChannelAbsent;

        /// <summary>
        /// 一次手动急救:构造判定输入 → <see cref="HostEmergencyProcessor.Process"/>
        /// (→ EmergencyAttempt + EmergencyTreatmentApplied 双事件入病史流)。
        /// </summary>
        /// <param name="patientId">被施救病人(须在场 —— C3-M1 前置;不在场 ⇒
        /// 返回 <c>Applied = false</c>,零事件)。</param>
        /// <param name="method">结算路径(0 = Manual · 1 = Skip —— 序数承既有测试与 GDD 声明)。</param>
        /// <param name="actorId">施予者(玩家)id。</param>
        /// <param name="tick">当前 tick。</param>
        /// <returns>回执(Applied = 双事件已入流;不在场 = false)。</returns>
        /// <exception cref="ArgumentOutOfRangeException">tick / method / actorId 非法。</exception>
        public EmergencyManualResult TryManual(PatientId patientId, int method, int actorId, long tick)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "逻辑 tick 不得为负");
            if (method < 0 || method > 1)
                throw new ArgumentOutOfRangeException(nameof(method), method,
                    "method ∈ {0 = Manual, 1 = Skip}(GDD 规则十一 method 两值闭集)");
            if (actorId < 0)
                throw new ArgumentOutOfRangeException(nameof(actorId), actorId,
                    "actorId 走 ADR-006 Amendment B 计数器 id 空间,不得为负");

            // C3-M1:在场前置(与处方侧 PrescribeFlow 1c 对称)—— 不在场病人
            // 不得接收双事件(归因到不存在对象);调用方(InteractionInputDriver)
            // 只选在场目标,此检查堵「离场/未出现 id 直调」的路径。
            if (!_presence.IsPresent(patientId))
                return new EmergencyManualResult(false);

            // 判定输入(最小形态:单沿 + 满幅度 ⇒ 稳度门 edges≤1 直通 Applied;判定归主机)
            var attempt = new EmergencyAttemptPayload(
                action: 0,           // HemostasisBandage
                holdTicks: 10,
                edges: 1,
                magPeak: 100,
                magLast: 80,
                method: method,
                actorId: actorId,
                edgeTicks: new[] { (int)tick });

            return TryAttempt(attempt, patientId, tick);
        }

        /// <summary>
        /// 直读通道聚合交出物入口(story-009 · 2026-10-14 用户裁定「F 键 = 按住 = 一次动作」):
        /// 把 3 的 <see cref="AggregatedEmergency"/> 映射为判域形状并走同一 Process 链。
        /// </summary>
        /// <remarks>
        /// ⚠️ **映射住本装配边界**(R-9.2 / AC-3-A6):<c>AggregatedEmergency</c> 是 3 的交出物,
        /// <see cref="EmergencyAttemptPayload"/> 是判域形状;method / actorId 是 3 侧不知道的
        /// 语境,在此补齐(method 恒 0 = Manual —— 直读通道没有跳过语义,跳过走
        /// <see cref="TryManual"/>(method 1))。**本类不写流** —— 写者恒
        /// <see cref="HostEmergencyProcessor"/>(ADR-005 主机唯一 Append)。
        /// </remarks>
        /// <param name="agg">通道聚合交出物(F-10.5 五字段;零样本不可能 —— 零样本在
        /// 通道 EndAction 已签约为 null)。</param>
        /// <param name="actorId">施予者(玩家)id。</param>
        /// <param name="patientId">被施救病人(须在场 —— 同 <see cref="TryManual"/>)。</param>
        /// <param name="tick">当前 tick。</param>
        /// <returns>回执(Applied = 双事件已入流;不在场 = false)。</returns>
        /// <exception cref="ArgumentOutOfRangeException">tick / actorId 非法。</exception>
        public EmergencyManualResult TryAggregated(in AggregatedEmergency agg, int actorId,
                                                    PatientId patientId, long tick)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "逻辑 tick 不得为负");
            if (actorId < 0)
                throw new ArgumentOutOfRangeException(nameof(actorId), actorId,
                    "actorId 走 ADR-006 Amendment B 计数器 id 空间,不得为负");
            if (!_presence.IsPresent(patientId))
                return new EmergencyManualResult(false);

            // 聚合 → 判域映射。⚠️ 量纲注记(登记数值轮):EdgeTicks 此处是「动作内样本序号」
            // (通道内单调计数,非逻辑 tick)—— F-10.5 的 hold_ticks = 末沿 − 首沿 在该量纲下
            // = 首末沿间的采样帧数;与 20 Hz tick 的换算归数值轮裁定(与三乘子同批)。
            var attempt = new EmergencyAttemptPayload(
                action: agg.Action,
                holdTicks: agg.HoldTicks,
                edges: agg.Edges,
                magPeak: agg.MagPeak,
                magLast: agg.MagLast,
                method: 0,           // 直读通道 = Manual(Skip 只经 TryManual)
                actorId: actorId,
                edgeTicks: agg.EdgeTicks);

            return TryAttempt(attempt, patientId, tick);
        }

        /// <summary>共享结算尾(presence 已由入口检查;ctx / 动作表行在此单源)。</summary>
        private EmergencyManualResult TryAttempt(EmergencyAttemptPayload attempt,
                                                  PatientId patientId, long tick)
        {
            var ctx = new JudgeContext
            {
                Level = 1,
                MagThresholdEffective = 50,
            };

            // cause = 判据解析(原 hardcode 0 ⇒ 降级恒 0;见文件头注 + ResolveCause)。
            // ⚠️ 第九片:幅度门豁免的判据**不是** cause —— 见下。
            _processor.Process(attempt, BandageRow, ctx, patientId, tick, ResolveCause(), IsAnalogChannelAbsent);
            return new EmergencyManualResult(true);
        }
    }
}
