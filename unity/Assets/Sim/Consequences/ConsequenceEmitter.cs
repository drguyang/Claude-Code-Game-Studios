// S13 · story-003 —— F-53.2 延迟发出 + ConsequenceResolved **世界流**写者(53 唯一写者)。
//
// 权威来源:
//   GDD design/gdd/medical-consequences.md
//     · F-53.2 `:268-294`:`EmitTick = Tick(CaseClosed) + DelayTable[outcome]`;
//       定义域恰 = 回响轴三值(结局轴不是 key);`DelayTicks > 0` 硬约束
//     · 规则六(不可归因):后果事件载荷**零 case_id / 零指向触发病例的指针**;`patient_id` 允许
//     · 规则八:`ConsequenceResolved` 直登 registry(ADR-024 §①);**P0 零订阅者仍照发**(水龙头纪律)
//     · Edge Cases `:322`「后果延迟期内存档 ⇒ 未发的后果事件在重放时按 EmitTick 重建 —— 零独立状态」
//     · Edge Cases `:323`「延迟期内玩家再次结案 ⇒ 两个后果**各自**按 EmitTick 发出;**不合并、不覆盖**」
//     · `:259` / `:317` 无痕(NoEcho)不回响 ⇒ 域外 outcome = **合法不发**(非错误)
//     · AC-53-06(定义域内每条 > 0 + 53 程序集**零跨 tick 可变字段**)/ AC-53-07(反渗)/ AC-53-14(进流非派生缓存)
//   卡面 production/epics/medical-consequences/story-003-delayed-emit-and-world-stream.md
//     · Control Manifest Required:写流经 `IEventAuthority` 查权 + 主机 `Append` 发 `Seq`;EmitTick 纯整数
//     · Control Manifest Forbidden:**缓存「待发表」进字段**(派生态不落私有态,每次现算)
//     · Guardrail:每病例至多一条后果事件;发出幂等**从流可判**(判据读流非读内存)
//     · Implementation Note 1:窗口/lookback 须保证**流前缀单调**(CatchUp 一条不漏,挂 ADR-005)
//     · Implementation Note 2:**不自建第二索引持久结构**(承 AC-53-06 零可变态 + TR-016 有界性同族)
//     · Implementation Note 4:零 case_id 在**构造器层**物理不可传(参数集不含),非运行期置 null
//   ADR-009 §一/§二(后果状态 = 模拟态进流)· §六(世界流有界性)
//   ADR-008 §二 跨流全序键 `(Tick, StreamPriority, Patient, Seq)`(排序经 `EventOrder`)
//   ADR-024 §① 真源 = entities.yaml(本 Kind `stream: world` / `author: 53` / `Patient = patient_id`)
//   ADR-029 §③ 载荷唯一编码路径 = `IPayloadEncoder`(本类内禁 `new PayloadRef(`,b6 门强制)
//   ADR-005(主机唯一 Append;header `Seq = -1` 未发号哨兵,由 EventStream 发号 —— 同 PoiStateMachine /
//     CaseCloseWriter / PatternRecognitionWriter 三先例)
//   ADR-007 §四(世界级事件用 PatientId.None 哨兵;**本件不用** —— 后果逐例属某病人,恒携真 id)
//
// ════════════════════════════════════════════════════════════════════════════
// 形状裁定(用户 2026-10-16 裁,卡面未逐字)
// ════════════════════════════════════════════════════════════════════════════
//
// **A 案 —— Outcome 由调用方喂入**。outcome = `Judge(P,C,D,U)` 的输出,**不在流里**;
// 而 AC-53-06 禁 53 持任何跨 tick 可变态 ⇒ 每次查询须重算判定链。把 story-001 聚合器 +
// story-002 Judge 全链塞进 `Emit` 签名会让发射器变成第二条装配管线 ⇒ 改为
// `eligible` 形参(每合格结案一行),由**装配层每 tick** 现算现喂(零缓存)。
// 发射器因此是「(流前缀, 合格集, nowTick) → 写入」的纯函数 + 唯一副作用 = `Append`。
//
// **D-3 幂等判据 = 按病人计数差(读流,非内存 flag)**。载荷零 case_id(AC-53-07)⇒
// 「本案是否已发」**不可**由 case_id 关联;而 `(EmitTick, Patient)` 作键会在
// 「同病人两案延迟不同但 EmitTick 恰好相同」时**静默吞第二条**(违 GDD `:323`「不合并」)。
// 现口径:
//     pending(p) = |{ 合格行:p,EmitTick ≤ now }| − |{ 流中 ConsequenceResolved:p }|
// 发出序 = EmitTick 升序 ⇒ 「已发的必是 EmitTick 较小者」成立 ⇒ 差额取尾部。
// 判据全程读流(证伪轮 ④:把幂等改成读内存 flag ⇒ 重放夹具必红)。
//
// **P0 lookback = 全流扫(无下界)**。`lookback` 是**性能旋钮**,值归数值轮(卡面 Note 6);
// P0 取「无下界」⇒ CatchUp / 读档补算**一条不漏**(卡面 Note 1 的流前缀单调义务由
// EmitTick 升序 Append 兑现)。扩窗 = 纯性能改动、零语义变化(本选择不引入任何状态)。
//
// ⚠️ **跨域接缝登记(用户 2026-10-16 裁「照发 + 具名登记」,归 9 / 7a 轮)**:
// `EventStream.Append` 的 AC-15 CAP 分支(EventStream.cs:77-84)对
// `Patient != None` 的事件查在场:`!IsPresent(pid) ∧ PresentCount ≥ 24` ⇒ **throw**。
// 本件的 `Patient` 恒为真 `patient_id`(registry `entities.yaml:2160` 不动)⇒
// **离屏病人到期后果受该 CAP 分支约束**。本件**不 catch** —— 异常向上传播 = 主机语义
// (静默吞掉后果会破 AC-53-14「后果状态进世界流」)。该接缝须 9/7a 裁(离屏病人是否
// 应计入 PresentCount / 或后果支走 CAP 豁免)。
//
// ⚠️ 门 A:本文件零 UnityEngine / 零 Sim.Codec;零定点量(F-53.2 全案纯整数)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Consequences
{
    /// <summary><see cref="ConsequenceEmitter.Emit"/> 的结果码(零写入分支亦具名,不静默)。</summary>
    public enum ConsequenceEmitResult
    {
        /// <summary>本 tick 至少写入一条 <see cref="EventKind.ConsequenceResolved"/>。</summary>
        Emitted = 0,

        /// <summary>零写入 —— 无到期待发行(合格集空 / 全部 EmitTick 仍在延迟窗内 / 差额为 0)。</summary>
        NothingPending = 1,

        /// <summary>零写入 —— **非主机**(<see cref="IEventAuthority.IsHost"/> 为假)。
        /// <para>主机唯一 Append(ADR-005);与 <c>PoiStateMachine</c> 的 <c>NotHost</c> 同口径
        /// (拒写行为 + 可诊断性皆成立,不与「无待发」同码)。</para></summary>
        NotHost = 2,
    }

    /// <summary>
    /// 延迟后果发射器 —— 53 的 <c>ConsequenceResolved</c> 生产写入通道(世界流,主机侧)。
    /// </summary>
    /// <remarks>
    /// <para><b>零跨 tick 可变态</b>(AC-53-06 / 卡面 Forbidden):本类的实例字段全是构造期注入的
    /// **不可变协作方**(全 <c>readonly</c>,零集合字段、零「待发表」字段);待发行集是
    /// <see cref="Emit"/> 每次调用现算的局部量 ⇒ 任意流前缀重放与在线态逐位一致。</para>
    /// <para><b>只写 <see cref="EventKind.ConsequenceResolved"/></b>(世界流;53 不写病史流 / 病例流 ——
    /// 卡面 Forbidden)。判定归 story-002,聚合归 story-001,呈现归 story-004。</para>
    /// </remarks>
    public sealed class ConsequenceEmitter
    {
        private readonly IEventSink _eventSink;
        private readonly IEventAuthority _eventAuthority;
        private readonly IPayloadEncoder _encoder;
        private readonly DelayTable53.Table _delayTable;

        /// <summary>构造发射器(依赖注入,承 ADR-005 / coding-standards「singleton 禁用」)。</summary>
        /// <param name="eventSink">事件写入通道(主机唯一;生产 = 真 <c>EventStream</c>)。</param>
        /// <param name="eventAuthority">写权权威(<see cref="IEventAuthority.IsHost"/> 门)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 §③ —— 唯一合法编码路径)。</param>
        /// <param name="delayTable">延迟表(F-53.2 整数域视图;A 案下由调用方经 IDataProvider 装载)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public ConsequenceEmitter(
            IEventSink eventSink, IEventAuthority eventAuthority,
            IPayloadEncoder encoder, DelayTable53.Table delayTable)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _eventAuthority = eventAuthority ?? throw new ArgumentNullException(nameof(eventAuthority));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _delayTable = delayTable ?? throw new ArgumentNullException(nameof(delayTable));
        }

        /// <summary>
        /// 发出到期待行的后果事件(F-53.2):对 <paramref name="stream"/> 前缀现算待发行并逐条 Append。
        /// </summary>
        /// <remarks>
        /// <para><b>P0 零订阅者仍照发</b>(AC-53-07 / 规则八水龙头纪律):本方法**无任何消费者查询**,
        /// 无消费者时事件照写世界流(「没人听就不发」的路径在本件编译面上不存在)。</para>
        /// <para><b>发出序</b> = EmitTick 升序(同 EmitTick 按来源结案的整数全序分量
        /// <c>(Tick, Patient, Seq)</c> 升序;`StreamPriority` 对全部来源 <c>CaseClosed</c> 恒等,
        /// 故三分量与 <see cref="EventOrder"/> 四分量等价)⇒ 满足 <c>EventStream</c> 的
        /// BCD-码-3「流按 Tick 非降」硬不变量(EventStream.cs:109-115)。</para>
        /// <para>⚠️ <b>待发行不持有任何事件引用</b>:破平只取结案事件的整数全序分量 ⇒
        /// AC-53-07 的「零病例指针」在内部类型面同样成立(不是只保证载荷面)。</para>
        /// </remarks>
        /// <param name="stream">三流混合的**只读流视图**(生产 = <c>bag.Stream.Events</c>)——
        /// 幂等差额在此读(判据读流非读内存);null / 空 = 无任何已发行。</param>
        /// <param name="eligible">A 案入参:每**合格**结案一行(= 该案的 <c>CaseClosed</c> 事件 +
        /// story-002 的 Outcome 枚举序数),由装配层每 tick 现算现喂(零缓存)。
        /// 域外 outcome(NoEcho 无痕 / 结局轴)= 合法不发,<b>不抛</b>(W-4 ①)。
        /// <para>⚠️ <b>调用方义务(precondition,S-B1)</b>:本集须含**全部** <c>EmitTick ≤ nowTick</c>
        /// 的合格结案行 —— **不得**按 lookback / 时间下界裁剪(卡面 Note 6 的 lookback 是**性能旋钮**,
        /// P0 取无下界)。理由:幂等判据按「该病人升序前缀」比对流中已发行数,若队首到期行被裁掉,
        /// 余行 <c>queueIndex</c> 从 0 重数 ⇒ 被误判「已发」⇒ 该病人余下后果**永久不发**,
        /// 而返回码仍是合法的 <c>NothingPending</c>(静默、不可观察,破 AC-53-14)。
        /// 后续若要引入窗口,须同时把幂等判据改成「按 EmitTick 下界对齐」而非「按计数前缀」。</para></param>
        /// <param name="nowTick">当前逻辑 tick(20 Hz;由 <c>ITickProvider</c> 注入的调用方给出)。</param>
        /// <param name="emittedCount">出参:本次实际写入条数(零写入分支亦如实归 0)。</param>
        /// <returns>结果码;仅 <see cref="ConsequenceEmitResult.Emitted"/> 意味着真的写了。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="nowTick"/> &lt; 0
        /// (与 <c>CaseCloseWriter</c> / <c>PatternRecognitionWriter</c> 同口径:入参非法 = 编程错误)。</exception>
        /// <exception cref="InvalidOperationException"><see cref="EmitTick"/> 越出
        /// <see cref="long"/> 上界(DelayTable53.EmitTick 的零回绕守卫,ADR-012 F7)。
        /// ⚠️ **调用方义务(precondition)**:调用方**不得 catch 本异常后于更低 tick 重试** ——
        /// Append 逐条即时提交,中途抛出 ⇒ 已写入的到期行留在流里(回滚归 7a/45 事务面,
        /// 不在本件;调用方吞异常再试 = 让后续 Emit 撞 BCD-码-3 的确定性路径)。</exception>
        public ConsequenceEmitResult Emit(
            IReadOnlyList<SimEvent> stream,
            IReadOnlyList<(SimEvent CaseClosed, int Outcome)> eligible,
            long nowTick,
            out int emittedCount)
        {
            emittedCount = 0;
            if (nowTick < 0)
                throw new ArgumentOutOfRangeException(nameof(nowTick), nowTick, "tick 不得为负");

            // ── 主机唯一写(ADR-005;与 PoiStateMachine 同形)──
            if (!_eventAuthority.IsHost) return ConsequenceEmitResult.NotHost;

            var due = BuildDueRows(eligible, nowTick);
            if (due.Count == 0) return ConsequenceEmitResult.NothingPending;

            // ── ② 幂等差额(读流)──
            var pending = SelectPending(due, stream);
            if (pending.Count == 0) return ConsequenceEmitResult.NothingPending;

            // ── ③ 逐条 Append(升序;Seq = -1 哨兵由 EventStream 发号)──
            emittedCount = AppendDueRows(pending, stream);
            return ConsequenceEmitResult.Emitted;
        }

        /// <summary>① 现算到期集(纯函数;零跨 tick 字段)+ 升序定序。</summary>
        /// <remarks>
        /// <para><b>升序定序</b>(证伪轮 ① 的载体):EmitTick → 来源结案的整数全序分量。
        /// ⚠️ 破平**只取整数分量**(Tick / Patient / Seq),不持 <see cref="SimEvent"/> 引用 ⇒
        /// AC-53-07 的「零病例指针」在内部类型面亦成立;来源 <c>CaseClosed</c> 恒
        /// <c>StreamId.Case</c>,故 <c>StreamPriority</c> 对本集恒等 ⇒ 三分量与
        /// <see cref="EventOrder"/> 的四分量等价。</para>
        /// <para><b>行内去重</b>(卡面 Guardrail「每病例至多一条后果」的载体):同一
        /// <c>CaseClosed</c> 被喂两次(上游重复喂)时,仅取一条 —— 否则出两条后果,且
        /// <c>EventStream</c> 的未发号去重键含 <c>PayloadRef</c> 三字段而 blob 池**不 intern**
        /// ⇒ 同内容两行 = 两键 = 去重抓不到 ⇒ 双发。去重键取五元组(零跨 tick 态,不破 AC-53-06)。</para>
        /// </remarks>
        private List<DueRow> BuildDueRows(
            IReadOnlyList<(SimEvent CaseClosed, int Outcome)> eligible, long nowTick)
        {
            var due = new List<DueRow>();
            var seen = new HashSet<(long, int, int, long, long)>();
            if (eligible == null) return due;

            for (int i = 0; i < eligible.Count; i++)
            {
                SimEvent close = eligible[i].CaseClosed;
                int outcome = eligible[i].Outcome;

                if (close.Kind != EventKind.CaseClosed)
                    throw new ArgumentException(
                        $"eligible[{i}] 不是 CaseClosed(实得 {close.Kind})—— 合格行的触发事件" +
                        "须为该病例的结案事件(F-53.2 自变量 = Tick(CaseClosed))", nameof(eligible));

                // 世界级哨兵不合法:后果逐例属某病人,None 会产出「无病人」的世界级幻影后果,
                // 且绕过 EventStream 的 AC-15 在场检查(EventStream.cs:77 显式跳过 None)。
                if (close.Patient == PatientId.None)
                    throw new ArgumentException(
                        $"eligible[{i}] 的 CaseClosed 携 PatientId.None —— 后果逐例属某病人," +
                        "须携真 patient_id(类头口径 + registry entities.yaml:2160 的 Patient = patient_id; " +
                        "None 会产出无病人归因的世界级幻影后果,并绕过 AC-15 在场检查)", nameof(eligible));

                if (!_delayTable.TryDelayTicks(outcome, out long delayTicks))
                    continue;   // 域外 = 合法不发(NoEcho 无痕 / 结局轴由 9 即时结算 —— F-53.2 定义域收窄)

                long emitTick = DelayTable53.EmitTick(close.Tick, delayTicks);
                if (emitTick > nowTick) continue;   // 仍在延迟窗内(证伪轮 ⑤ 的窗上界)

                int patient = close.Patient.Value;
                if (!seen.Add((emitTick, patient, outcome, close.Tick, close.Seq)))
                    continue;   // 上游重复喂同一结案 ⇒ 只发一条(卡面 Guardrail)

                due.Add(new DueRow(emitTick, patient, outcome, close.Tick, patient, close.Seq));
            }

            due.Sort((a, b) =>
            {
                int cmp = a.EmitTick.CompareTo(b.EmitTick);
                if (cmp != 0) return cmp;
                cmp = a.CloseTick.CompareTo(b.CloseTick);
                if (cmp != 0) return cmp;
                cmp = a.ClosePatientId.CompareTo(b.ClosePatientId);
                if (cmp != 0) return cmp;
                return a.CloseSeq.CompareTo(b.CloseSeq);
            });

            return due;
        }

        /// <summary>② 幂等差额(读流):去掉该病人「升序前缀中已发行」的部分。</summary>
        /// <remarks>
        /// <para>已发行数按病人计(载荷零 <c>case_id</c> ⇒ 不可按案关联)。差额口径 =
        /// **升序前缀**:<paramref name="due"/> 已按 EmitTick 升序,某病人「已有 <c>already</c> 条
        /// 后果」⇒ 其队首 <c>already</c> 行视为已发、跳过;其后的到期行才补发。
        /// ⚠️ 取**尾部**是同型静默面(队首到期行永不落地)。</para>
        /// <para>现实触发路径 = 半提交:某病人第 k 条到期行被 <c>EventStream</c> 的 CAP/去重等分支
        /// 拒绝/吞掉 ⇒ 流计数 k−1 而队首 k 条都在 <paramref name="due"/> ⇒ 前缀口径自洽补发;
        /// 尾部口径下 k 行永远排不进 ⇒ 静默丢后果(破 AC-53-14)。</para>
        /// <para>⚠️ <b>调用方义务(见 <see cref="Emit"/> 的 <c>eligible</c> 文档)</b>:<paramref name="due"/>
        /// 必须是该病人**全部** <c>EmitTick ≤ now</c> 的到期行 —— 若上游按 lookback 下界裁过合格集,
        /// 已发行的队首行会脱离本集 ⇒ 余行 <c>queueIndex</c> 从 0 重数 ⇒ 被误判「已发」而**永久漏发**
        /// (返回码仍是合法的 <c>NothingPending</c>,不可观察)。</para>
        /// </remarks>
        private static List<DueRow> SelectPending(List<DueRow> due, IReadOnlyList<SimEvent> stream)
        {
            var emittedByPatient = CountEmittedByPatient(stream);
            var pending = new List<DueRow>();
            var dueSoFarByPatient = new Dictionary<int, int>();
            for (int i = 0; i < due.Count; i++)
            {
                DueRow row = due[i];
                int patient = row.PatientId;
                dueSoFarByPatient.TryGetValue(patient, out int queueIndex);   // 该病人第几行(升序)
                dueSoFarByPatient[patient] = queueIndex + 1;

                emittedByPatient.TryGetValue(patient, out int already);
                if (queueIndex < already) continue;   // 升序前缀中已有流中后果 ⇒ 不重发(幂等)

                pending.Add(row);
            }
            return pending;
        }

        /// <summary>③ 逐条 Append(升序;Seq = -1 哨兵由 EventStream 发号)。</summary>
        /// <returns>实际写入条数。</returns>
        /// <exception cref="InvalidOperationException">到期行 <c>EmitTick</c> 早于
        /// <paramref name="stream"/> 流尾 tick ⇒ 追加必撞 <c>EventStream</c> 的 BCD-码-3
        /// 单调断言。此处**提前**以具名异常诊断(S-B2):现实路径 = CAP 半提交(前几条已入流、
        /// 其余被 AC-15 拒)后他写者把流尾推进 ⇒ 补发时 overdue 行回退于流尾。跨域归属
        /// (离屏病人后果是否应 CAP 豁免)归 9 / 7a 轮,见类头登记。</exception>
        private int AppendDueRows(List<DueRow> pending, IReadOnlyList<SimEvent> stream)
        {
            // S-B2:预检「回退于流尾」——把不可恢复的 BCD-码-3 崩转为指向成因的具名失败。
            // 判据与 EventStream 的 _lastAppendedTick 等价:流按 Tick 非降 ⇒ 流尾 tick = 该基线
            // (同一条流的假设与 CountEmittedByPatient 一致 —— stream 即本发射器的写入目标视图)。
            if (stream != null && stream.Count > 0)
            {
                long streamTailTick = stream[stream.Count - 1].Tick;
                if (pending[0].EmitTick < streamTailTick)
                    throw new InvalidOperationException(
                        $"ConsequenceResolved 到期行 EmitTick={pending[0].EmitTick} 回退于流尾 " +
                        $"tick={streamTailTick} ⇒ 追加必撞 BCD-码-3(流须按 Tick 非降)。" +
                        "现实成因 = 延迟窗内的半提交:前几条已入流、其余被 AC-15 CAP 分支拒收," +
                        "此后他写者把流尾推进 ⇒ 补发不可能。**不得**吞掉本异常(静默漏后果会破 " +
                        "AC-53-14「后果状态进世界流」);跨域归属(离屏病人后果是否应 CAP 豁免)" +
                        "归 9 / 7a 轮裁(见类头跨域接缝登记)。");
            }

            int written = 0;
            for (int i = 0; i < pending.Count; i++)
            {
                DueRow row = pending[i];
                var payload = new ConsequenceResolvedPayload(row.Outcome, row.PatientId);
                var payloadRef = _encoder.Encode(EventKind.ConsequenceResolved, payload);

                _eventSink.Append(new SimEvent(
                    row.EmitTick, new PatientId(row.PatientId), -1,
                    EventKind.ConsequenceResolved, payloadRef));
                written++;
            }
            return written;
        }

        /// <summary>发出条数(便捷重载;丢弃 out 形参)。</summary>
        public ConsequenceEmitResult Emit(
            IReadOnlyList<SimEvent> stream,
            IReadOnlyList<(SimEvent CaseClosed, int Outcome)> eligible,
            long nowTick)
            => Emit(stream, eligible, nowTick, out _);

        /// <summary>流中各病人已有的 <see cref="EventKind.ConsequenceResolved"/> 条数(幂等差额右端)。</summary>
        /// <remarks>
        /// <para>刻意**不按 tick 下界过滤**:流里只要存在该病人的后果行就计入 —— 「不发第二条」
        /// 是比「不发窗口外的第二条」更强的护栏(有界性 ≤ 病例流条数,ADR-009 §六 / registry)。
        /// 反常前缀(后果行数 &gt; 合格数,如合格集被上游裁小)⇒ 差额 ≤ 0 ⇒ 本件不发、不抛。</para>
        /// </remarks>
        private static Dictionary<int, int> CountEmittedByPatient(IReadOnlyList<SimEvent> stream)
        {
            var counts = new Dictionary<int, int>();
            if (stream == null) return counts;
            for (int i = 0; i < stream.Count; i++)
            {
                SimEvent e = stream[i];
                if (e.Kind != EventKind.ConsequenceResolved) continue;
                counts.TryGetValue(e.Patient.Value, out int c);
                counts[e.Patient.Value] = c + 1;
            }
            return counts;
        }

        /// <summary>到期待发行(局部量 —— 不进任何字段;AC-53-06 零可变态的载体)。
        /// <para>⚠️ **零事件引用**:定序破平只用结案事件的整数全序分量 ⇒ 内部类型面亦无
        /// 「病例指针」(AC-53-07 的扫描面因此从「载荷」扩到「内部类型」仍零命中)。</para></summary>
        private readonly struct DueRow
        {
            /// <summary>发出 tick = <c>Tick(CaseClosed) + DelayTicks</c>(F-53.2)。</summary>
            public readonly long EmitTick;
            /// <summary>逐例结算患者(载荷 <c>patient_id</c> 与事件头 <c>Patient</c> 同源;
            /// <c>patient_id</c> **允许** —— AC-53-07 括注)。</summary>
            public readonly int PatientId;
            /// <summary>Outcome 枚举序数(恒 ∈ 回响轴 —— 域外在查表时已被跳过)。</summary>
            public readonly int Outcome;
            /// <summary>来源结案的全序分量(仅定序用;不进载荷)。</summary>
            public readonly long CloseTick;
            /// <summary>来源结案的 Patient 分量(仅定序用)。</summary>
            public readonly int ClosePatientId;
            /// <summary>来源结案的 Seq 分量(仅定序用)。</summary>
            public readonly long CloseSeq;

            public DueRow(long emitTick, int patientId, int outcome,
                          long closeTick, int closePatientId, long closeSeq)
            {
                EmitTick = emitTick; PatientId = patientId; Outcome = outcome;
                CloseTick = closeTick; ClosePatientId = closePatientId; CloseSeq = closeSeq;
            }
        }
    }
}
