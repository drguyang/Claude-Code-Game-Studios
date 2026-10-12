// sprint-05 T2.1 + sprint-06 G1.4 · 42 侧只读数源的**生产实装**(2026-10-11 更新)
//
// 权威来源:
//   ADR-013 §9 C3 —— 42 只渲染不持状态;**本类是 42 与装配层之间的装配桥**,同样零缓存
//   ADR-025 §① —— `Gameplay.Boot` = 装配层(组合根案 A)
//   story-024 边界①② —— 42 零自定义映射(通道/词/极性全来自 8 的表)
//   G1 对撞裁决乙案(2026-10-11) —— revealed = **派生态,不进三流**;
//     「查了没有」+ 快照住 39 分册(<see cref="ExaminationLedger"/>)
//
// ⚠️ G1.4 语义切换(2026-10-11):revealed 真源从「F2 signs tripwire(恒空)」改为
//   **查体分册快照**。查体后(ExamineDriver 落快照)→ 42 值区出现词条;未查 ⇒ 空集
//   (未查 = 空行,`:329`,禁假数据)。空词表接线期 = 恒空行,词表装载后自动放行。
//
// ⚠️ 设计说明:
//   · 住 `Gameplay.Boot`(**不**住 `Gameplay.UI`):42 装配不可反向引用装配层
//     (ADR-025 §① Gameplay.UI 引用集 = {Sim.Contracts, Gameplay.Presentation};
//      而 `ICasebookDtoSource` 住 Gameplay.UI)⇒ 实现必须在能同时看见
//     `Gameplay.UI` + `Sim` + `Sim.Contracts` 的装配 = 本装配(与案 A 同理由)。
//   · 每次读都穿过分册(零缓存字段 —— 缓存 = 第二份真相,破 C3);
//     分册快照 = 冻结值(AC-8-25),row 引用来自 8 词表(ExamineDriver 求值时传入)。

using Array_ = System.Array;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 脉案只读数源的生产实装 —— 把 42 接到**查体分册快照**(sprint-05 T2.1 + G1.4)。
    /// </summary>
    public sealed class CasebookVitalsSource : ICasebookDtoSource
    {
        private readonly ExaminationLedger _ledger;
        private readonly IReadOnlyList<SimEvent> _caseStream;
        private readonly ITickProvider _tick;
        private readonly SeenDiseaseProjector _seenDisease;
        private readonly LexiconBookSource _lexiconBook;

        /// <summary>脉案命令路由(sprint-08 S1.1 件 E2)—— 档位真源。
        /// <para>⚠️ 42 **只读**本路由的 <see cref="CasebookCommandRouter.PendingConfidence"/>
        /// (42 零游戏状态字段,AC-42-D3);档位住在装配层,与 InkAgeTicks / SeenLexiconIds 同构。</para>
        /// <para>可为 null(未接命令面的旧装配面)—— 此时 <see cref="PendingConfidence"/> 恒 0。</para>
        /// <para>⚠️ 非 readonly:装配序上命令路由晚于本源构造(它复用方笺页的
        /// <c>JudgmentWriter</c>)⇒ 经 <see cref="BindCommands"/> 后置接回。</para></summary>
        private CasebookCommandRouter _commands;
        private PatientId _patient = PatientId.None;

        /// <summary>当前观看者(玩家)actorId —— 病名册条目集按人分册(铁律⑤「每人一本」)。
        /// <para>装配期未知(玩家 id 由 BootRoot 生成玩家后分配)⇒ 后置 <see cref="SetViewer"/>;
        /// 未设 ⇒ -1,病名册空(未见任何病)。</para></summary>
        private int _viewerActorId = -1;

        /// <param name="ledger">查体分册(「查了没有」+ 快照 —— revealed 真源)。</param>
        /// <param name="caseStreamEvents">病例流事件视图(墨龄 = 最近 Judgment 的 Tick —— G2.2)。</param>
        /// <param name="tick">tick 源(墨龄的「当前 tick」;AC-42-G6:差值在装配层算,42 零计时器)。</param>
        /// <param name="seenDisease">已见病投影器(sprint-08 S1.1 件 B;病名册条目集的数据源)。</param>
        /// <param name="lexiconBook">病名册条目源(sprint-08 S1.1 件 C;已见集经词表反查 ⇒ lexicon_id)。</param>
        public CasebookVitalsSource(ExaminationLedger ledger,
                                    IReadOnlyList<SimEvent> caseStreamEvents,
                                    ITickProvider tick,
                                    SeenDiseaseProjector seenDisease,
                                    LexiconBookSource lexiconBook,
                                    CasebookCommandRouter commands = null)
        {
            _ledger = ledger ?? throw new System.ArgumentNullException(nameof(ledger));
            _caseStream = caseStreamEvents ?? throw new System.ArgumentNullException(nameof(caseStreamEvents));
            _tick = tick ?? throw new System.ArgumentNullException(nameof(tick));
            _seenDisease = seenDisease ?? throw new System.ArgumentNullException(nameof(seenDisease));
            _lexiconBook = lexiconBook ?? throw new System.ArgumentNullException(nameof(lexiconBook));
            _commands = commands;   // 可空:未接命令面 ⇒ PendingConfidence 恒 0
        }

        /// <summary>当前绑定病人。</summary>
        public PatientId BoundPatient => _patient;

        /// <summary>换绑定病人(不持旧病人的任何量 —— 42 下次读即是新病人)。
        /// <para>⚠️ 同批把命令路由也切到同一病人(档位按病人分册;两处不同步 =
        /// 42 显示的档位与实际落笔对象错位)—— 单一会话对象的两个面。</para></summary>
        public void ChangePatient(PatientId patient)
        {
            _patient = patient;
            _commands?.BindPatient(patient);
        }

        /// <summary>接回命令路由(装配序后置:命令路由复用方笺页的 <c>JudgmentWriter</c>,
        /// 晚于本源构造)。接回时**立即**把当前绑定病人同步给路由(防两处病人错位)。</summary>
        /// <param name="commands">脉案命令路由(档位真源)。</param>
        /// <exception cref="System.ArgumentNullException">commands 为 null。</exception>
        public void BindCommands(CasebookCommandRouter commands)
        {
            _commands = commands ?? throw new System.ArgumentNullException(nameof(commands));
            if (_patient.Value >= 0) _commands.BindPatient(_patient);
        }

        /// <summary>设定观看者(玩家)actorId —— 病名册条目集按人分册(铁律⑤)。
        /// <para>BootRoot 生成玩家后调用(装配期 id 未定;与 <c>PrescriptionPageHost.SetAuthor</c>
        /// 同款后置形态)。未设 ⇒ 病名册空。</para></summary>
        /// <exception cref="System.ArgumentOutOfRangeException">actorId 为负。</exception>
        public void SetViewer(int actorId)
        {
            if (actorId < 0)
                throw new System.ArgumentOutOfRangeException(nameof(actorId), actorId,
                    "观看者 actorId 不得为负(机制 A 计数器 id 空间)");
            _viewerActorId = actorId;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <b>病名册条目集</b>(casebook-39 §5.2):已见病种集(本玩家)<c>∩</c> 词表 <c>disease_ids</c>
        /// ⇒ 可指认 <c>lexicon_id</c> 列表。<b>守密</b>:只出 id,不出 disease_id(AC-37-15)。
        /// <para>未 <see cref="SetViewer"/> ⇒ 空(未见任何病;反幻想二:没见过就翻不到)。</para>
        /// </remarks>
        public IReadOnlyList<int> SeenLexiconIds
        {
            get
            {
                if (_viewerActorId < 0) return Array_.Empty<int>();
                int[] seen = _seenDisease.SeenDiseaseIds(_viewerActorId);
                return _lexiconBook.PointableLexiconIds(seen);
            }
        }

        /// <inheritdoc/>
        /// <remarks>词条文本经装配层 <c>LexiconBookSource.TextOf</c> 解析(42 不持词表;守密:
        /// 只回文本,不回 disease_ids —— AC-37-15)。</remarks>
        public string TextOfLexicon(int lexiconId) => _lexiconBook.TextOf(lexiconId);

        /// <inheritdoc/>
        /// <remarks>
        /// <b>待落笔置信度档</b>(OQ-CB-5 甲循环落笔):0 = 空 / 1 = 疑似 / 2 = 确定。
        /// <para>真源 = 命令路由(装配层),42 每次渲染**回读**(零缓存,ADR-013 §9 C3)
        /// —— 与 <see cref="InkAgeTicks"/> / <see cref="SeenLexiconIds"/> 同款形态。</para>
        /// <para>未接命令面 ⇒ 0(空)。</para>
        /// </remarks>
        public byte PendingConfidence => _commands?.PendingConfidence ?? 0;

        /// <inheritdoc/>
        /// <remarks>已查 ⇒ 该次快照词条数;未查 ⇒ 0(空行)。</remarks>
        public int ActiveSignCount
        {
            get
            {
                if (_ledger.TryGetSnapshot(_patient.Value, out var snap))
                    return snap.Set.SignCount;
                return 0;
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// **真源 = 查体分册快照**(G1 · 乙案):已查 ⇒ 该次查体的 8 词表词条(冻结);
        /// 未查 ⇒ 空集(未查 = 空行,`:329`,禁假数据)。
        /// <para>不再恒空集 —— sprint-05 的接线期占位已被 G1 的 Ledger 快照替下。
        /// 「读出的词」住 8 词表;Ledger 只存**收录了哪几条**(引用,不经 42 复制)。</para>
        /// </remarks>
        public IReadOnlyList<SignLexemeRow> RevealedRows
        {
            get
            {
                if (!_ledger.TryGetSnapshot(_patient.Value, out var snap))
                    return Array_.Empty<SignLexemeRow>();
                return snap.Set.Rows ?? Array_.Empty<SignLexemeRow>();
            }
        }

        /// <inheritdoc/>
        /// <remarks>精度档 = 该次查体的档(快照内;未查恒粗档 0)。</remarks>
        public int PrecisionSlot
        {
            get
            {
                if (!_ledger.TryGetSnapshot(_patient.Value, out var snap))
                    return 0;
                return snap.Set.PrecisionSlot;
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// **墨龄(G2.2 · 派生态)** = `当前 tick(ITickProvider) − 该病人最近 JudgmentRecorded
        /// 事件的 Tick`。差值在装配层算(42 零计时器,AC-42-G6)。
        /// <para>未落笔 ⇒ 0 = 刚落笔(墨龄零 = 湿墨形态)。载荷 freehandText P0 不做,
        /// 只认事件头 Tick —— <see cref="CaseStreamQuery.LastJudgmentOf"/> 按 case 过滤,
        /// 此处取该病人最近一条 Judgment(含 Revised,README 按全序最后)。</para>
        /// </remarks>
        public long InkAgeTicks
        {
            get
            {
                CaseId? openCase = CaseStreamQuery.OpenCaseOf(_caseStream, _patient.Value);
                if (!openCase.HasValue) return 0L;

                SimEvent? last = CaseStreamQuery.LastJudgmentOf(_caseStream, openCase.Value);
                if (!last.HasValue) return 0L;

                long current = _tick.CurrentTick;
                long age = current - last.Value.Tick;
                return age < 0 ? 0L : age;
            }
        }
    }
}