// sprint-07 S2.3/S2.4 · 方笺页的**接线宿主**(UI 事件 → 装配层 · 2026-10-11)
//
// 权威来源:
//   sprint-07 §2 S2.3(落笔 → JudgmentWriter.TryRecord → 墨龄)/ S2.4(开方 →
//   PrescribeRequestDriver.TryPrescribe → DrugTreatmentApplied 入病史流)
//   casebook-39.md(方笺 = 脉案同一本书的一页 —— 不成新模态,不发相机意图,ADR-013 方笺裁定)
//   ADR-013 §9 C3(42 只渲染不持状态 —— 本宿主 = 装配层把「UI 事件 → 写者」接线的载体)
//
// ⚠️ 本宿主住 **Gameplay.Boot**:把 42 侧方笺页的 UI 事件(落笔 / 选档确认)翻译成
//   装配层写者调用。**不持 sim 态、不造 UI** —— 只登记事件 + 转发(呈现侧装配件,
//   与 InteractionInputDriver 同纪律)。「玩家操作在方笺页」的交互面(click/焦点)归 42;
//   本宿主为「该页的语义动作 → 写者」的接线面。
//
// ⚠️ 判定**先决**:
//   · 落笔须有开案(病例流查询);无开案 ⇒ 不写(JudgmentWriteResult.NoOpenCase)。
//   · 开方须有在场病人 + 库存(20 最小面)。库存 = 接线载体种子(真库存面归 20)。
//
// ✅ **2026-10-12 sprint-08 S2(用户裁定 甲):删「接线期限 1」** —— 原 `_prescribed` 单次
//   闸门是**接线期的脚手架**(S2.4 当时无玩家入口,故用「只许行使一次」证真链、防自动重复)。
//   常驻选档确认 UI 现已落地(42 方笺页戥子档位行 → `ICasebookCommandSink.SubmitDose`),
//   开方**由玩家每次落点驱动** ⇒ 闸门的存在反而**遮蔽真规则**:重复开方应由
//   **库存**拒绝(20 的 `IPortionsStore.HasPortions`),而非由接线层的单次标志拒绝。
//   ⚠️ **反假绿义务不变**:「玩家零输入 ⇒ 零事件」现由**玩家动作驱动**保证(42 侧无任何
//   自动重复路径),不再需要宿主层闸门 —— 宿主每调用一次 = 玩家落点一次。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 方笺页接线宿主 —— 把落笔 / 开方的语义动作接进 <see cref="JudgmentWriter"/> /
    /// <see cref="PrescribeRequestDriver"/>。
    /// <para>住装配层(呈现侧装配件):不持 sim 态、不造 UI —— 42 方笺页把动作交到这里,
    /// 本宿主完成「动作 → 写者」接线并返回结果码。</para>
    /// </summary>
    public sealed class PrescriptionPageHost
    {
        private readonly JudgmentWriter _judgment;
        private readonly PrescribeRequestDriver _prescribe;
        private readonly MinimalPortionsStore _portions;
        private readonly SimTickDriver _ticks;
        private int _authorId;

        private PatientId _boundPatient = PatientId.None;

        /// <param name="eventSink">事件写入通道(主机唯一 Append;ADR-005)。</param>
        /// <param name="encoder">载荷编码器(第七抽象点;落笔的 JudgmentRecorded 走具名路径)。</param>
        /// <param name="caseStreamEvents">病例流事件视图(落笔前置 —— 病人须有开案)。</param>
        /// <param name="prescribeDriver">处方驱动(开方五步 → DrugTreatmentApplied 入病史流)。</param>
        /// <param name="portionsStore">库存最小面(开方前置 —— 注入 1 份接线载体种子)。</param>
        /// <param name="tickDriver">tick 驱动(动作 tick = 当前逻辑 tick)。</param>
        /// <param name="initialAuthorId">初始玩家 actorId(机制 A id 空间;未定传 -1,
        /// 动作前须 <see cref="SetAuthor"/> 否则 fail-loud)。</param>
        public PrescriptionPageHost(IEventSink eventSink, IPayloadEncoder encoder,
                                    IReadOnlyList<SimEvent> caseStreamEvents,
                                    PrescribeRequestDriver prescribeDriver,
                                    MinimalPortionsStore portionsStore,
                                    SimTickDriver tickDriver,
                                    int initialAuthorId)
        {
            _authorId = initialAuthorId;

            // 落笔写者:37 的 JudgmentRecorded 生产者(assembly 层;freehandText P0 空串)
            // 作者 = 玩家 actorId,由 SetAuthor 设定(BootRoot 生成玩家后调用)
            _judgment = new JudgmentWriter(eventSink, encoder, caseStreamEvents,
                                           initialAuthorId);
            // ✅ 2026-10-12 C3-M2:依赖 fail-loud —— 原两参接受 null 静默降级
            //   (缺 ticks ⇒ 全写 tick 0;缺库存 ⇒ 开方恒 false),与全局「装配缺依赖
            //   = 具名异常」口径对齐(同构造 prescribeDriver 早已 ?? throw)。
            _prescribe = prescribeDriver ?? throw new ArgumentNullException(nameof(prescribeDriver));
            _portions = portionsStore ?? throw new ArgumentNullException(nameof(portionsStore));
            _ticks = tickDriver ?? throw new ArgumentNullException(nameof(tickDriver));
        }

        /// <summary>设定玩家 actorId(BootRoot 生成玩家后调用;动作前必须已定)。</summary>
        /// <exception cref="ArgumentOutOfRangeException">authorId 为负(未定态不得接动作)。</exception>
        public void SetAuthor(int authorId)
        {
            if (authorId < 0)
                throw new ArgumentOutOfRangeException(nameof(authorId), authorId,
                    "玩家 actorId 不得为负(机制 A 计数器 id 空间;未定态须先 SetAuthor)");
            _authorId = authorId;
            _judgment.SetAuthor(authorId);   // 落笔写者作者同步(S2.3 的诚实作者)
        }

        /// <summary>当前绑定病人(方笺页的会话对象;由 BootRoot 在查体/切页时设置)。</summary>
        public PatientId BoundPatient
        {
            get => _boundPatient;
            set => _boundPatient = value;
        }

        /// <summary>本宿主持有的落笔写者(装配层内共享面 —— sprint-08 S1.1 起
        /// <see cref="CasebookCommandRouter"/> 复用**同一实例**,使 <see cref="SetAuthor"/>
        /// 的作者同步对两条写路径同时生效:单写者 = 单一作者真源,不新开第二个
        /// <c>JudgmentWriter</c>(两个实例会各自持 <c>_authorPlayerId</c> = 第二份真相)。</summary>
        public JudgmentWriter Writer => _judgment;

        /// <summary>绑定病人(查体驱动调用;与 CasebookSource.ChangePatient 同步)。</summary>
        public void BindPatient(PatientId patient) => _boundPatient = patient;

        /// <summary>落笔一次判断(方笺页语义动作 → 37 写者)。</summary>
        /// <param name="lexiconId">判断词条 id(u16 值域;case_judgment_lexicon ordinal)。</param>
        /// <param name="confidence">把握度(u8 档位)。</param>
        /// <returns>结果码(仅 <see cref="JudgmentWriteResult.Recorded"/> = 真的写入)。</returns>
        public JudgmentWriteResult WriteJudgment(int lexiconId, byte confidence)
        {
            AssertAuthorReady();
            if (_boundPatient.Value < 0)
                throw new InvalidOperationException(
                    "方笺页落笔前须绑定病人(切脉案/查体时设置 BoundPatient)—— 无对象无从落笔");
            return _judgment.TryRecord(CurrentTick, _boundPatient, lexiconId, confidence);
        }

        /// <summary>
        /// 开方一次(方笺页选档确认 → 处方驱动)。
        /// <para>⚠️ **无单次闸门**(2026-10-12 S2 用户裁定 甲,删原接线期限 1)——
        /// 重复开方的拒绝归**库存**(20 的 <c>IPortionsStore</c>),不归接线层。
        /// 每调用一次 = 玩家落点一次(42 侧零自动重复路径)。</para>
        /// </summary>
        /// <param name="itemKey">所开之药(装配层最小表 = salicylic_acid)。</param>
        /// <param name="doseOrdinal">档序数(戥子落点;整剂路径恒 1)。</param>
        /// <returns>是否走完处方五步并入病史流(库存不足 ⇒ false,零事件)。</returns>
        public bool Prescribe(ItemKey itemKey, int doseOrdinal)
        {
            AssertAuthorReady();
            if (_boundPatient.Value < 0)
                throw new InvalidOperationException(
                    "方笺页开方前须绑定病人(无对象无从开方)");

            // ✅ 2026-10-12 C3-M3:库存前置真生效 —— 种子由组合根装配期一次 Grant,
            //   此处**不再自证**(原逐次 Grant 使 HasPortions 恒真 + 失败无限刷库存)。
            var intent = new PrescribeIntent(_authorId, _boundPatient, itemKey, doseOrdinal);
            return _prescribe.TryPrescribe(in intent, CurrentTick).Applied;
        }

        /// <summary>动作前置:玩家 actorId 已定(fail-loud —— 未 SetAuthor 即动作 = 装配遗漏)。</summary>
        private void AssertAuthorReady()
        {
            if (_authorId < 0)
                throw new InvalidOperationException(
                    "PrescriptionPageHost 玩家 actorId 未定 —— 须先 SetAuthor(BootRoot 生成玩家后)," +
                    "未定态拒绝接动作(作者缺失 = 事件作者无主)");
        }

        private long CurrentTick => _ticks?.CurrentTick ?? 0L;
    }
}