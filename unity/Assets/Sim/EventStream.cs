// ADR-005 / ADR-008 —— 病史事件流实现。
//
// 权威来源:
//   ADR-005 §Key Interfaces —— 事件流唯一真源
//   ADR-008 §一 —— 病例流路由口径
//   ADR-024 §① —— Kind 白名单路由
//   GDD disease-simulation.md —— AC-15 有界性, AC-36 id 机制
//
// 核心机制:
//   - Append 纯函数路由（Kind → StreamId）
//   - Seq 发号器（每 tick 复位）
//   - 去重（条件键:已发号 = 四元组 (Kind, Patient, Tick, Seq);未发号补 PayloadRef 身份 —— O-4）
//   - 有界性（PATIENT_APPEARANCE_CAP = 24;跳过 PatientId.None —— O-5）
//
// ⚠️ 次序(承既有,测试面 F-4 登记):CAP 检查在去重**之前** ⇒ 病人已离场且 CAP 满时,
//    重发事件先抛而非走去重短路。45 重传路径的重试语义须知悉(归 45 轮)。
//
// BCD-码-3(2026-10-10 · 登记债收口):**流按 Tick 非降**是写入期硬不变量。
//   消费端 `DiseaseVitalsService.ApplyNewEvents` 的 `if (e.Tick > tick) break` 依赖
//   物理列表按 Tick 非降;乱序入流 ⇒ 未来事件挡道,其后的过去事件要等它自己的边沿
//   才被读到(静默晚到一整个 tick 跨度)。故去重短路**之后**、入列表**之前**断言
//   `e.Tick ≥ 上次入流 tick`,违例 = fail-loud。重发(旧 tick)走去重短路,不触断言。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 病史事件流 —— 唯一真源。
    /// </summary>
    public sealed class EventStream : IEventSink
    {
        // AC-15: 同场被模拟病人数硬上限
        public const int PATIENT_APPEARANCE_CAP = 24;

        private readonly List<SimEvent> _events = new List<SimEvent>();
        private readonly IIdAuthority _idAuthority;
        private readonly IPresenceQuery _presenceQuery;
        private readonly HashSet<string> _dedupKeys = new HashSet<string>();

        // 折叠行(TD-persist-03 修复轮):读档恢复时从存档折叠段登记;
        // 是被折叠病人病史的唯一残留 —— 高水位(ADR-006 Amendment B)与
        // 下一次 Checkpoint 的折叠行回写(SaveComposer 既有折叠行入参)都从这里取。
        private readonly List<FoldRow> _foldedRows = new List<FoldRow>();

        // 恢复批的「(Tick, Patient) → 组最大 Seq」登记(TD-persist-03):
        // Restore 两遍扫描填充;AppendCore 在组创建时播种发号器(只抬不压)——
        // 防止恢复后同组新事件领到已被恢复事件占用的号(同 Kind 即撞去重键被吞)。
        // ⚠️ 仅恢复路径写入;纯 Append 会话恒空 ⇒ 发号器行为与既有逐字相同
        // (`event_stream_test` o1 / dedup 两钉不受扰)。单调门保证不会再回到
        // 更早的 tick ⇒ 旧组条目不再被查询,留存无害;Clear / Restore(替换)清空。
        private readonly Dictionary<(long Tick, int Patient), long> _restoredGroupMax
            = new Dictionary<(long Tick, int Patient), long>();

        // Seq 发号器状态
        private long _lastSeqTick = -1;
        private PatientId _lastSeqPatient = PatientId.None;
        private long _currentSeq = 0;

        // 单调断言状态(BCD-码-3):上次**实际入流**的 tick;初值 long.MinValue 使首条无下界。
        private long _lastAppendedTick = long.MinValue;

        public EventStream(IIdAuthority idAuthority, IPresenceQuery presenceQuery)
        {
            _idAuthority = idAuthority ?? throw new ArgumentNullException(nameof(idAuthority));
            _presenceQuery = presenceQuery ?? throw new ArgumentNullException(nameof(presenceQuery));
        }

        /// <summary>
        /// 事件流中的事件数。
        /// </summary>
        public int Count => _events.Count;

        /// <summary>
        /// 只读事件列表。
        /// </summary>
        public IReadOnlyList<SimEvent> Events => _events;

        /// <summary>
        /// 折叠行(只读视图)—— 被折叠病人病史的唯一残留记录。
        /// <para>来源 = <see cref="Restore"/>(读档);<see cref="Clear"/> 清空。
        /// <b>运行期折叠不走这里</b>:Checkpoint 的折叠是组版期的存档侧动作
        /// (<c>SaveComposer</c>),不改变运行期流。</para>
        /// </summary>
        public IReadOnlyList<FoldRow> FoldedRows => _foldedRows;

        /// <inheritdoc />
        public void Append(in SimEvent e) => AppendCore(in e, enforceCap: true);

        /// <summary>
        /// 读档恢复入口(<c>TD-persist-03</c> 动作 ①②,用户 2026-10-24 裁定)。
        /// 以存档内容<b>替换</b>当前流(先 <see cref="Clear"/> 再逐条恢复),
        /// 并登记折叠行(<see cref="FoldedRows"/>)。
        /// </summary>
        /// <para><b>对 <see cref="Append"/> 三关的取舍(用户 2026-10-24 裁定「三分化」):</b></para>
        /// <para>· <b>CAP 绕过</b> —— 读档不是新病人到场;读档时在场视图尚未重建
        /// (<c>IPresenceQuery</c> 表现层注入),走 CAP 必然误抛(债条目实测 ②)。</para>
        /// <para>· <b>去重 / 单调保留</b> —— 它们是流不变量:恢复序按存档序(Tick 非降),
        /// 重复事件 = 档损坏;恢复的事件照常登记去重键 ⇒ 后续生产 <see cref="Append"/>
        /// 对已恢复事件的真重发仍被幂等拒收。</para>
        /// <para>· <b>Seq 不重发</b> —— 恢复事件已带 <c>Seq ≥ 0</c>,<see cref="AppendCore"/>
        /// 的既有哨兵(<c>Seq &lt; 0</c> 才发号,O-1)天然不重发;发号器状态随恢复事件推进,
        /// 恢复后同一 (Tick, Patient) 的新事件接续正确下一号。</para>
        /// <param name="events">存档事件段解出的全序列(三流合一,按存档序)。</param>
        /// <param name="foldedRows">存档折叠段解出的折叠行(可为空列表;行是
        /// 被折叠病人病史的<b>唯一残留</b>,高水位 <see cref="GetNextPatientId"/> 依赖它,
        /// ADR-006 Amendment B)。同一 (PatientId, DiseaseId) 重复 = 档损坏,抛。</param>
        /// <exception cref="ArgumentNullException">任一入参为 null。</exception>
        /// <exception cref="InvalidOperationException">
        /// 恢复序 Tick 回退(BCD-码-3),或折叠行 (PatientId, DiseaseId) 重复。
        /// </exception>
        /// <remarks>
        /// ⚠️ <b>批非原子</b>:恢复按存档序逐条过 <see cref="AppendCore"/>,违例(如 Tick 回退)
        /// 抛出时**前面的条目已入流**(单条内部仍零污染)。这是可接受的:Restore 是
        /// <b>替换语义</b>(先 <see cref="Clear"/>)⇒ 重试 Load 会整体重建,部分应用自愈;
        /// 且 Load 侧锁在读档成功后才置位,失败不锁槽。
        /// </remarks>
        public void Restore(IReadOnlyList<SimEvent> events, IReadOnlyList<FoldRow> foldedRows)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (foldedRows == null) throw new ArgumentNullException(nameof(foldedRows));

            // 折叠行先于事件校验(违例抛出时流零污染 —— 与单调断言同款选位)。
            var seenRows = new HashSet<long>();
            for (int i = 0; i < foldedRows.Count; i++)
            {
                FoldRow row = foldedRows[i];
                // (patientId, diseaseId) 压一个 long 做键:patientId 与 diseaseId 均 int。
                long key = ((long)row.PatientId << 32) | (uint)row.DiseaseId;
                if (!seenRows.Add(key))
                    throw new InvalidOperationException(
                        $"折叠行重复:patientId={row.PatientId}, diseaseId={row.DiseaseId} —— " +
                        "ADR-010 §二 一行 = 病人 × 病种,重复 = 档损坏");
            }

            Clear();

            // 两遍扫描:第一遍建「组最大号表」(Restore 在前 ⇒ AppendCore 播种时表已全),
            // 第二遍逐条过 AppendCore(绕 CAP;去重/单调/计数器与 Append 完全同路)。
            _restoredGroupMax.Clear();
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                var gkey = (e.Tick, e.Patient.Value);
                if (_restoredGroupMax.TryGetValue(gkey, out long cur))
                {
                    if (e.Seq > cur) _restoredGroupMax[gkey] = e.Seq;
                }
                else
                {
                    _restoredGroupMax[gkey] = e.Seq;
                }
            }
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i]; // 索引器是返回值,不能按 in 直传(CS8156)
                AppendCore(in e, enforceCap: false);
            }
            _foldedRows.AddRange(foldedRows);
        }

        /// <summary>
        /// Append 的实现核。<see cref="Append"/> 传 <c>enforceCap: true</c>,
        /// <see cref="Restore"/> 传 <c>false</c>(读档重建,见其 doc)。
        /// ⚠️ 其余各关(去重 / 单调 / 发号器推进)两条路径<b>完全同路</b> ——
        /// 它们是流不变量,不是「新事件」专属。
        /// </summary>
        private void AppendCore(in SimEvent e, bool enforceCap)
        {
            // AC-15: 有界性检查(O-5 修复 · 2026-10-09:跳过 PatientId.None)
            // 失效模式(O-5):世界事件(StructurePlaced / PoiStateChanged / ActorCellEntered …)
            // 带 PatientId.None,IsPresent(None) 恒 false ⇒ 在场满 CAP 时
            // 被误判「新病人」拒收 —— 玩家立刻不能盖房、POI 状态机与跨格事件全抛异常。
            // ⚠️ 2026-10-09 批次 E 起,急救两支(EmergencyAttempt / EmergencyTreatmentApplied)
            // 已带真实病人归因(不再是 None 举例)⇒ 会进本分支的在场/CAP 检查。
            // CAP 守的是**同场被模拟病人数**(ADR-008 §六 有界性),None 不是病人 ⇒ 不进本分支。
            // ⚠️ Restore 路径绕过本关(用户 2026-10-24 裁定「三分化」):在场视图尚未重建,
            //    CAP 检查对恢复事件必然误抛;有界性由「存档自带的事件就是历史事实」保证。
            if (enforceCap && e.Patient != PatientId.None && !_presenceQuery.IsPresent(e.Patient))
            {
                if (_presenceQuery.PresentCount >= PATIENT_APPEARANCE_CAP)
                {
                    throw new InvalidOperationException(
                        $"AC-15: 在场病人数已达上限 {PATIENT_APPEARANCE_CAP}，拒收新病人");
                }
            }

            // AC-15: 去重(O-4 修复 · 2026-10-09:未发号键补载荷身份)
            // 失效模式(O-4):入流 Seq 对未发号事件恒为 -1,只按 (Kind, Patient, Tick, Seq)
            // 取键会让同 tick 同 Kind 的**两条不同事件**坍缩 —— 第二条静默丢弃,而其业务
            // 结果(如 structureId 已返回调用方)已生效 ⇒ 重建 / 回放时该结果消失。
            // 现实触发:同一 tick 放置两个结构 ⇒ 第二条 StructurePlaced 被吞。
            // 口径:
            //   · 未发号(Seq < 0):键补 PayloadRef 身份 (BlobId, Offset, Length) ——
            //     未发号事件的唯一区分只能来自载荷;同一 SimEvent 重发 ⇒ 同 PayloadRef ⇒
            //     同键 ⇒ 幂等拒收(既有重发语义保留)。
            //   · 已发号 / 显式 Seq:维持四元组 —— Seq 已是事件身份(重传带原 Seq 命中同键),
            //     且不受载荷重编码影响(重建 / 传输路径不产生假阴性去重)。
            // ⚠️ Sim 门 A 只见 Sim.Contracts ⇒ 只能取 PayloadRef 三字段,读不了 payload
            //    内容(那归 Sim.Codec)—— 故身份 = ref 本身,不是首字段语义值。
            string dedupKey = e.Seq < 0
                ? $"{e.Kind}_{e.Patient.Value}_{e.Tick}_{e.Seq}_{e.Payload.BlobId}_{e.Payload.Offset}_{e.Payload.Length}"
                : $"{e.Kind}_{e.Patient.Value}_{e.Tick}_{e.Seq}";
            if (_dedupKeys.Contains(dedupKey))
            {
                return; // 重发拒收(旧 tick 重发到此短路,不触单调断言 —— 45 重传语义保留)
            }

            // BCD-码-3(2026-10-10):流按 Tick 非降 —— 去重之后、任何状态写入之前断言。
            // 断言点选在 `_dedupKeys.Add` 之前:违例抛出时流零污染(键 / 发号器均未动)。
            if (e.Tick < _lastAppendedTick)
            {
                throw new InvalidOperationException(
                    $"BCD-码-3: 事件 tick {e.Tick} 回退于已入流 tick {_lastAppendedTick} —— " +
                    "流必须按 Tick 非降(消费端游标 `if (e.Tick > tick) break` 依赖该不变量," +
                    "乱序入流会让过去事件被未来事件挡住、静默晚到)。");
            }

            _dedupKeys.Add(dedupKey);

            // AC-36: Seq 发号（每 tick 复位）—— 计数器对每条同 (Tick, Patient) 事件
            // 都前进(含显式事件,承既有行为;`event_stream_test` o1 / dedup 两钉逐字钉死)。
            // 🔁 TD-persist-03:恢复批的组最大号经 `_restoredGroupMax` 在组创建时**播种**
            //    (见其字段 doc)—— 本体计数器语义一字未动。
            SimEvent eventWithSeq = e;
            if (e.Tick != _lastSeqTick || e.Patient != _lastSeqPatient)
            {
                _currentSeq = 0;
                _lastSeqTick = e.Tick;
                _lastSeqPatient = e.Patient;
            }
            else
            {
                _currentSeq++;
            }

            // 播种:该 (Tick, Patient) 组在恢复批中已有事件 ⇒ 发号器抬到「组最大号 + 1」,
            // 只抬不压(gmax+1 ≤ 当前计数器时不扰 ++ 前进)。纯 Append 会话表恒空 ⇒ 零效果。
            if (_restoredGroupMax.Count > 0
                && _restoredGroupMax.TryGetValue((e.Tick, e.Patient.Value), out long groupMax)
                && groupMax + 1 > _currentSeq)
            {
                _currentSeq = groupMax + 1;
            }

            // 如果事件没有 Seq,则发号
            // O-1 修复(2026-10-09):哨兵由 `Seq == 0` 改为 `Seq < 0` ——
            // 0 是合法首号值,原哨兵与 _currentSeq 首值冲突,
            // 「发了 0 号」与「没发号」不可区分。调用方待发事件一律传 -1。
            if (e.Seq < 0)
            {
                eventWithSeq = new SimEvent(e.Tick, e.Patient, _currentSeq, e.Kind, e.Payload);
            }

            _events.Add(eventWithSeq);
            _lastAppendedTick = e.Tick; // BCD-码-3:单调基线只随**实际入流**前进(去重短路不触)
        }

        /// <summary>
        /// 获取下一个 patient_id（从事件流重构）。
        /// <para>⚠️ <b>含折叠行</b>(TD-persist-03 修复轮):被折叠病人的事件已从流中抹除,
        /// 其 id 只活在折叠行里 —— 漏掉它们会让读档后新病人复用已折叠的 id
        /// (ADR-006 Amendment B「终态折叠行必须保留 patient_id」正是为此)。</para>
        /// </summary>
        public PatientId GetNextPatientId()
        {
            int max = -1;
            foreach (var e in _events)
            {
                if (e.Patient != PatientId.None && e.Patient.Value > max)
                {
                    max = e.Patient.Value;
                }
            }
            for (int i = 0; i < _foldedRows.Count; i++)
            {
                if (_foldedRows[i].PatientId > max)
                {
                    max = _foldedRows[i].PatientId;
                }
            }
            return new PatientId(max + 1);
        }

        /// <summary>
        /// 清空事件流（测试用）。
        /// </summary>
        public void Clear()
        {
            _events.Clear();
            _dedupKeys.Clear();
            _foldedRows.Clear(); // 折叠行与流同寿命:流清空 = 折叠记录一并失效
            _restoredGroupMax.Clear(); // 组最大号表与流同寿命
            _lastSeqTick = -1;
            _lastSeqPatient = PatientId.None;
            _currentSeq = 0;
            _lastAppendedTick = long.MinValue; // BCD-码-3:清空即解除单调基线(与发号器同格复位)
        }
    }
}
