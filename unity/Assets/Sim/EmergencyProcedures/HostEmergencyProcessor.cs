// emergency-procedures Story 004 / 007 — 主机侧管线
//
// 权威来源:
//   GDD emergency-procedures.md 规则十一: Subscribe(EmergencyAttempt) → Judge → Append × 2 → Seq 发号
//   GDD 规则五(:195-224): EmergencyTreatmentApplied 九字段载荷定义
//   GDD F-10.4(:604): drug_potency = RoundFix(BASE_POTENCY × ResultMul) / MUL_ONE(单一舍入)
//   ADR-005: 主机唯一 Append
//   ADR-006: Seq 发号
//   ADR-029 §③: 载荷编码唯一路径 = IPayloadEncoder(零手搓 PayloadRef)
//
// ⚠️ 2026-10-03(Story 007)修复两处手搓载荷(由 b6 门首次查出,前两轮评审与对账件均未登记):
//   :50  new PayloadRef(attempt.Action, attempt.HoldTicks, attempt.Edges)   —— 8 字段只写 3 个
//   :59  new PayloadRef(attempt.Action, (int)result, 0)                     —— 9 字段只写 1 个有意义
//        且把 `attempt.Action` 当 applied 首字段(`TreatmentId ≠ Action`)、`result` 当施予者 id。
//   ⇒ 现全部经 IPayloadEncoder,九字段/八字段逐项填充。
//
// ⚠️ 2026-10-09(M2 接线轮阶段 2 · 批次 E)病人归因修复:两处 Append 原写 `PatientId.None`
//   ⇒ 急救事件无法按病人归因,体征链缺急救分支。按 ADR-009 Amendment I(急救三 Kind 属
//   判定输入 / 处置事件,落病史流、归属具体病人)+ ADR-006 Amendment B(patient_id 必留,
//   高水位重构依赖),现改由 `Process` 形参 `patientId` 传入,入口 fail-loud 拒 None/负值。
//   与 11 侧 `PrescribeFlow` 写 `DrugTreatmentApplied` 时用 `req.PatientId` 同构。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.EmergencyProcedures
{
    /// <summary>
    /// 主机侧急救处理器（规则十一）。
    /// 订阅 EmergencyAttempt → Judge → Append(EmergencyAttempt) + Append(EmergencyTreatmentApplied)
    /// </summary>
    /// <remarks>
    /// <para><b>`Seq` 的现状(Story 007 裁定 A=丙,如实登记)</b>:两处载荷的 `Seq` 字段
    /// **置 0 占位** —— 其真源是「主机在 `Append` 时发号」(GDD 规则五),
    /// 而发号发生在 <see cref="IEventSink.Append"/> 内部(`SimEvent.Seq`),
    /// **载荷构造在其之前** ⇒ 本处理器拿不到该值。
    /// 载荷 `Seq` 与 header `Seq` 的语义关系须由**上行链**落地时裁定(45 / P1b 范围)。
    /// ⚠️ **本处置不构成「九字段齐备」的借绿** —— 该字段在 AC-10-39 的判据面内被显式标注为占位。</para>
    /// </remarks>
    public sealed class HostEmergencyProcessor
    {
        private readonly IEventSink _eventSink;
        private readonly IIdAuthority _idAuthority;
        private readonly IPayloadEncoder _encoder;

        /// <summary>DC-4 处置轴闭集(9 的烘焙产物 `owner=="10"` 段);<c>null</c> = 未注入。</summary>
        /// <para><b>2026-10-20 真源接线</b>:源 = `disease_action_axis.cooked.bytes` 的
        /// <c>actions</c> 段经 <c>DiseaseActionAxisCookedCodec</c> 解码后按 <c>owner=="10"</c>
        /// 过滤。⚠️ <b>不得</b>在 <c>Sim</c> 侧读产物(<c>Sim</c> 够不着 <c>Gameplay.Presentation</c>)
        /// ⇒ 由装配层注入,与 <see cref="IPayloadEncoder"/> 同构。</para>
        /// <para>⚠️ <b>null ≠ 空集</b>:<c>null</c> = 装配层未装(EditMode 15+ 既有调用方),
        /// 走 <see cref="EmergencyActionSchema.ValidateActionId"/> 的枚举影子;
        /// 空集 = **装配错误**,入口直接拒(拒以空集冒充绿)。</para>
        private readonly System.Collections.Generic.ISet<int> _actionAxis;

        /// <param name="eventSink">事件写入通道(主机唯一)。</param>
        /// <param name="idAuthority">发号权威。</param>
        /// <param name="encoder">
        /// 载荷编码器(ADR-029 §③)—— **唯一合法的载荷构造路径**。
        /// 本类内不得出现 <c>new PayloadRef(</c>(ADR-029 §③ 的门判据,由 b6 门强制)。
        /// </param>
        /// <param name="actionAxis">DC-4 处置轴闭集(<c>owner=="10"</c> 段的 action_id 集);
        /// <c>null</c> ⇒ 回退枚举影子(零行为变化 —— 15+ 个既有装配调用方全部不传本参)。</param>
        public HostEmergencyProcessor(IEventSink eventSink, IIdAuthority idAuthority,
                                      IPayloadEncoder encoder,
                                      System.Collections.Generic.ISet<int> actionAxis = null)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _idAuthority = idAuthority ?? throw new ArgumentNullException(nameof(idAuthority));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _actionAxis = actionAxis;
        }

        /// <summary>
        /// 处理 EmergencyAttempt（主机唯一入口）。
        /// </summary>
        /// <param name="attempt">判定输入载荷(八字段,含 `Method` / `ActorId`)。</param>
        /// <param name="action">动作表行(含 `Polarity` / `BasePotency` / `HalfLifeTicks` / `ResultMul`)。</param>
        /// <param name="ctx">判定上下文。</param>
        /// <param name="patientId">
        /// 接受急救的病人 id(事件头 `SimEvent.Patient` 归因)。
        /// 两条落流事件(<c>EmergencyAttempt</c> / <c>EmergencyTreatmentApplied</c>)均归属它 ——
        /// ADR-009 Amendment I + ADR-006 Amendment B(<c>patient_id</c> 必留)。
        /// ⚠️ 归因主体是**被施救的病人**,不是施予者 <c>attempt.ActorId</c>(施予者在载荷内)。
        /// </param>
        /// <param name="tick">当前 tick。</param>
        /// <param name="cause">
        /// 处置原因枚举(GDD `:467-469` 的判据:设备无模拟量通道 ⇒ **降级**;
        /// 玩家在设备具备时主动选跳过 ⇒ **玩家选择**)。
        /// ⚠️ 该判据的信息源在**输入层(3)**,本处理器收不到 ⇒ 由调用方传入。
        /// **真实调用方 = 主机上行链,归 45 / P1b;当前仅测试调用。**
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> 为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="patientId"/> 为 <see cref="PatientId.None"/>(-1)或负值 ——
        /// 急救事件必须归属具体病人,None 是事件侧哨兵,不得进入病史流归因。
        /// </exception>
        public void Process(EmergencyAttemptPayload attempt, EmergencyActionRow action,
                            JudgeContext ctx, PatientId patientId, long tick, int cause)
        {
            // 兼容入口(既有调用方):**不承载幅度门豁免判据** ⇒ 一律照常按 `MagPeak` 判。
            // ⚠️ 2026-10-19(第九片)起草时此入口曾**从 cause 反推**判据(`cause == 降级` ⇒ 豁免),
            //   被证伪轮 M-m-1 判为**等价变异**后复查发现:那是**用结算输出反推判定输入**,
            //   正是本项目反复剿的第二真源(且「未注入 ⇒ cause 记玩家选择」的默认会让它进一步失真)。
            //   ⇒ 判据只能由调用方**直接给**;不知道设备态就别回填(宁严勿猜)。
            //   生产路径(`EmergencyManualDriver`)走下方带 `analogChannelAbsent` 的重载。
            Process(attempt, action, ctx, patientId, tick, cause, analogChannelAbsent: false);
        }

        /// <summary>处理 EmergencyAttempt(带**幅度门判据**的重载;AC-10-21 的幅度半边)。</summary>
        /// <param name="analogChannelAbsent">本次动作是否无模拟量通道(纯键鼠回退形状)。
        /// true ⇒ **幅度门按 `MAG_MAX` 判**(载荷里的真实 `MagPeak` 不变,仍原样进流);
        /// false ⇒ 照常按 `MagPeak` 判。判据的信息源在输入层(3),由调用方传入。</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> 为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="patientId"/> 为 None / 负值;
        /// 或 <c>attempt.Action</c> / <c>action.ActionId</c> ∉ 注入的处置轴闭集(DC-4)。</exception>
        /// <exception cref="InvalidOperationException">处置轴闭集注入为空(DC-4;空集 = 装配错误)。</exception>
        public void Process(EmergencyAttemptPayload attempt, EmergencyActionRow action,
                            JudgeContext ctx, PatientId patientId, long tick, int cause,
                            bool analogChannelAbsent)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            // ── DC-4 处置轴闭集门(2026-10-20 真源接线)────────────────────────
            // 病因:`EmergencyActionSchema.ValidateActionId` 的注释自陈「真源接线须由
            //   Editor.Tools.Bake 侧调用 ValidateActionIdFromAxis 并传入 9 的烘焙产物」,
            //   但**两个方法在生产侧都是零调用** ⇒ DC-4 判据长期是 Enum.IsDefined 的影子。
            //   后果(枚举注释已点名):9 的轴若新增一个 owner=="10" 的 id(如 2),
            //   EmergencyAction 枚举没有对应成员 ⇒ 影子**错误地拒绝**它。
            // 现口径:轴注入 ⇒ 读轴;未注入 ⇒ 枚举影子(null 与空集区别对待,见字段注)。
            // ⚠️ 位置在 patientId 门**之前**:载荷里的 action 是非法的 ⇒ 后面算什么都没意义;
            //    且该门只读入参,不产生任何副作用(不 Append / 不发号)。
            if (_actionAxis != null)
            {
                if (_actionAxis.Count == 0)
                    throw new InvalidOperationException(
                        "[DC-4] 处置轴闭集注入为空 —— 拒以空集冒充绿(空集会让每一次合法动作都被拒)。" +
                        "装配层须从 disease_action_axis 产物 owner==\"10\" 段建集。");
                // ⚠️ 两个 action id 都要过门,缺一不可 —— 它们落**不同**的去处:
                //   · `attempt.Action` ⇒ `EmergencyAttempt` 载荷(判定输入,**进流**);
                //   · `action.ActionId`  ⇒ `EmergencyTreatmentApplied.treatmentId`(九字段之二)。
                //   只查后者会漏掉前者:轴外 id 照样进流,由 9 的构建期白名单拒收 = 静默失败。
                if (!_actionAxis.Contains(attempt.Action))
                    throw new ArgumentOutOfRangeException(nameof(attempt), attempt.Action,
                        $"[DC-4] 载荷 action = {attempt.Action} ∉ 处置轴 owner==\"10\" 段" +
                        $"({{{string.Join(", ", _actionAxis)}}})—— 该值进 EmergencyAttempt 载荷," +
                        "轴外 id 会被 9 的构建期白名单拒收(静默失败)。");

                if (!_actionAxis.Contains(action.ActionId))
                    throw new ArgumentOutOfRangeException(nameof(action), action.ActionId,
                        $"[DC-4] action_id = {action.ActionId} ∉ 处置轴 owner==\"10\" 段" +
                        $"({{{string.Join(", ", _actionAxis)}}})—— 9 的轴是处置 id 的唯一 master;" +
                        "轴外 id 落流会被 9 的构建期白名单拒收(静默失败)。");
            }

            // ── fail-loud 门(入口,先于 Judge 与任何 Append)──────────────────
            // 失效模式(2026-10-09 批次 E 修的原 bug):归因写死 PatientId.None
            //   ⇒ 消费方(37 结案门 / 9 体征链 / 7a 高水位重构)拿不到病人身份。
            // 哨兵与负 id 都不是合法病人 id(IIdAuthority 发号自 0 起)⇒ 入口拒,
            // 不允许 Judge / Append 半程执行后才暴露。
            if (patientId.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(patientId), patientId.Value,
                    $"急救事件须归属具体病人,{nameof(PatientId)}.None(-1) 是事件侧哨兵," +
                    "不得作为病史流事件头的归因(ADR-009 Amendment I / ADR-006 Amendment B)");

            // 构造 EmergencyReading 供 Judge 使用
            var reading = new EmergencyReading(
                attempt.Action,
                attempt.HoldTicks,
                attempt.Edges,
                attempt.EdgeTicks,
                attempt.MagPeak);

            // ── 幅度门判据(第九片):无模拟量通道 ⇒ 按 MAG_MAX 判;流里仍记真实采样 ──
            // 权威 = 10 GDD Edge Cases `:728`「`magnitude` 通道无输入设备(纯键鼠)⇒ 回退形状 =
            //   离散档,自动置 `magnitude = MAG_MAX`(即幅度门在键鼠上恒过),只走节奏 + 稳度双门」。
            // **为什么豁免只落求值侧、不写回载荷**:F-10.4 `:679-692` 规定 `mag_peak` 是
            //   「存在量词取峰值」的**判定量**且**进流**,而载荷是**判定输入**(ADR-011 Amendment B
            //   C 路:客户端上行 → 主机 Judge)—— 输入写的是玩家**真给了什么**,不是它被怎么判。
            //   回填若写进载荷,读流方就再也分不出「玩家按满了」与「设备按不出」。
            // ⇒ 门豁免只作用于本次 Judge 调用;回填的形状由 10 侧
            //   <see cref="FeelLatencyEvaluator.CreateKeyboardFallbackReading(int,int,int,int[])"/>
            //   唯一给出(节奏 / 稳度输入原样透传,只改幅度 —— 不在此另写一份回填逻辑)。
            EmergencyReading judgedReading = analogChannelAbsent
                ? FeelLatencyEvaluator.CreateKeyboardFallbackReading(
                    attempt.Action, attempt.HoldTicks, attempt.Edges, attempt.EdgeTicks)
                : reading;

            // Judge(⚠️ 2026-10-14 story-009 用户裁定「带最小 Judge 分支」):
            //   method = Skip(1) ⇒ 判定结果不走时序 / 幅度门 —— 规则六:跳过恒 AppliedWeak
            //   (取 <see cref="ModalPhaseEvaluator.Skip"/> 的 Result;无障碍开关
            //   SkipEquivalence 归 49 / P2,P0 机制位恒 OFF ⇒ accessibilityOn: false)。
            //   ⚠️ 强度**仍走 F-10.4 公式**(ResultMul[AppliedWeak] = 0.5 档),
            //   不在本处置拍值 —— 本分支只裁「结果」不裁「强度」。
            var result = attempt.Method == 1
                ? ModalPhaseEvaluator.Skip(accessibilityOn: false).Result
                : JudgeEvaluator.Judge(judgedReading, action, ctx);

            // ── Append ① EmergencyAttempt(物化;八字段全载)────────────────
            var attemptEvent = new SimEvent(
                tick,
                patientId, // 病人归因(批次 E;原 PatientId.None ⇒ 体征链/结案门拿不到病人)
                -1, // 未发号哨兵 O-1(由发号器给出 header Seq)
                EventKind.EmergencyAttempt,
                _encoder.Encode(EventKind.EmergencyAttempt, attempt));
            _eventSink.Append(attemptEvent);

            // ── Append ② EmergencyTreatmentApplied(九字段全载)─────────────
            // F-10.4: drug_potency = RoundFix(BASE_POTENCY × ResultMul[result]) / MUL_ONE
            //   中间积落 Q32.32,全程**只做一次舍入**(ROUND_HALF_AWAY_FROM_ZERO)
            //   —— 承首轮评审 R-2/A8:`ScaleFixed` 即该实现,禁 `a * b / c` 朴素写法。
            long drugPotency = JudgeEvaluator.ScaleFixed(
                action.BasePotency,
                action.ResultMul[(int)result]);

            var applied = new EmergencyTreatmentAppliedPayload(
                tick:          tick,
                treatmentId:   action.ActionId,      // 处置_id = EmergencyAction 枚举
                actorId:       attempt.ActorId,      // 施予者(不是 JudgeResult!)
                polarity:      action.Polarity,      // 处置词表查得(不是恒 0)
                drugPotency:   new Fix(drugPotency), // F-10.4 单一舍入
                halfLife:      action.HalfLifeTicks, // 动作数据表(不是恒 0 ⇒ 不触发 9 的 Decay 除零)
                method:        attempt.Method,       // Manual / Skip —— 使跳过在流上可判别
                cause:         cause,                // 玩家选择 / 降级
                seq:           0);                   // ⚠️ 占位,见类头注

            var appliedEvent = new SimEvent(
                tick,
                patientId, // 病人归因(与 ① 同一病人;批次 E)
                -1, // 未发号哨兵 O-1
                EventKind.EmergencyTreatmentApplied,
                _encoder.Encode(EventKind.EmergencyTreatmentApplied, applied));
            _eventSink.Append(appliedEvent);
        }
    }
}
