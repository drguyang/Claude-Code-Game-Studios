// patient-ai Story 002 —— 空间行为导演(在场消费 · 升序求值 · HomeRegion 缓存)。
//
// 权威来源:
//   GDD design/gdd/patient-ai.md §Detailed Rules 规则四(行为模拟范围 = 在场病人)
//     · §Formulas F-13.2 / F-13.7 · §States `Seeking{EnRoute/AtClinic}`
//   ADR-016 §三(读粗粒度整数格,禁表现态位置)· §六(13 出 IPresentPatients,不引用 37)
//   TR-patient-006(消费 9 的在场判定,不自建在场定义)· TR-patient-013(13 不生成/不删除病人)
//   TR-patient-017(同 tick 多病人求值顺序钉死:actor_id 升序,读上 tick 快照,无链式反应)
//
// ⚠️ **求值顺序钉死**(承 27 同型纪律):每 tick 按 `actor_id` **升序**逐病人求值,
//   纯函数读上 tick 快照 ⇒ 与字典插入序无关(AC 第 7 条 / TC-7)。
// ⚠️ **13 零 spawn/despawn** —— 在场集由 <see cref="IPresenceQuery"/> 给,13 只消费
//   (TR-patient-013;CAP=24 由 9 守,13 无自主增减)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Gameplay.PatientAI
{
    /// <summary>单病人的空间行为态(13 派生态 —— 不写回 sim,ADR-027)。</summary>
    public sealed class PatientSpatialState
    {
        /// <summary>`HomeRegion(p)` —— **只在初始化时求一次**(AC 第 6 条:静态定义,状态变化不回改)。</summary>
        public EcozoneId HomeRegion;
        /// <summary>逻辑位姿(积分量)。</summary>
        public LogicalPose Pose;
        /// <summary>本病人是否已知医馆(`KnowsClinic`,初始化时定)。</summary>
        public bool KnowsClinic;
        /// <summary>本 tick 求得的求医子相(`AtClinic` / `EnRoute`)—— F-13.2 / §States。
        /// <para>⚠️ 存下来是**为了可观测**(评审 F-1:此前相位只在 `Step` 局部,无法从外部证伪);
        /// 下游 F-13.6 的 `AwaitingCare`(优先级 3)直接吃它。</para></summary>
        public SeekingPhase Phase;
    }

    /// <summary>空间行为导演 —— 13 的在场循环。
    /// <para>**只读消费**:在场判定(`IPresenceQuery`)、生态区(`IClinicKnowledgeSource`)、
    /// 路径(注入的整数导航格查询)。**零写 sim**(ADR-027)。</para></summary>
    public sealed class PatientSpatialDirector
    {
        private readonly IPresenceQuery _presence;
        private readonly ClinicKnowledge _knowledge;
        private readonly Func<PatientId, WorldPos> _spawnAnchorOf;
        private readonly Func<PatientId, IReadOnlyList<WorldPos>> _pathOf;
        private readonly Func<WorldPos, bool> _inClinicCells;
        private readonly SpatialBands _bands;
        private readonly Fix _patientSpeed;

        // 派生态字典 —— 升序遍历(求值序钉死)。**不落盘、不写流。**
        private readonly Dictionary<int, PatientSpatialState> _states = new Dictionary<int, PatientSpatialState>();

        // 测试接缝:每病人的路径覆盖(生产侧由注入的 _pathOf 提供)
        private readonly Dictionary<int, IReadOnlyList<WorldPos>> _pathOverride = new Dictionary<int, IReadOnlyList<WorldPos>>();

        /// <summary>就诊知识源的调用计数(测试接缝 —— 「`HomeRegion` 只求一次」的可观测点)。
        /// <para>⚠️ 生产侧不读;由 <see cref="EcozoneOfCallCount"/> 暴露给夹具(MUT-I 坐实欠债)。</para></summary>
        private int _ecozoneOfCalls;

        /// <summary>求值序探针(测试接缝 —— F-2/MUT-G 修复:令求值序**可观测**)。
        /// <para>⚠️ 生产侧传 null(零开销);夹具传入即按**实际求值序**回调。</para></summary>
        private readonly Action<PatientId> _onEvaluated;

        public PatientSpatialDirector(
            IPresenceQuery presence,
            ClinicKnowledge knowledge,
            Func<PatientId, WorldPos> spawnAnchorOf,
            Func<PatientId, IReadOnlyList<WorldPos>> pathOf,
            Func<WorldPos, bool> inClinicCells,
            in SpatialBands bands,
            in Fix patientSpeed,
            Action<PatientId> onEvaluated = null)
        {
            _presence = presence ?? throw new ArgumentNullException(nameof(presence));
            _knowledge = knowledge ?? throw new ArgumentNullException(nameof(knowledge));
            _spawnAnchorOf = spawnAnchorOf ?? throw new ArgumentNullException(nameof(spawnAnchorOf));
            _pathOf = pathOf ?? throw new ArgumentNullException(nameof(pathOf));
            _inClinicCells = inClinicCells ?? throw new ArgumentNullException(nameof(inClinicCells));
            _onEvaluated = onEvaluated;   // 测试接缝(可空);生产侧不传

            // 装载断言(照 BehaviorBands 先例:破约束表硬失败,不静默)
            var errs = SpatialBands.Validate(bands);
            if (errs.Count > 0)
                throw new ArgumentException(
                    "SPATIAL_BAND_* 破硬约束(GDD §Tuning 二):\n" + string.Join("\n", errs), nameof(bands));

            if (!LogicalStepper.SpeedIsSafe(patientSpeed))
                throw new ArgumentException(
                    $"[F-13.7] PATIENT_SPEED({patientSpeed.Raw} raw) 须 ∈ [0, FIX_ONE={Fix.OneRaw}) —— " +
                    "破了 ⇒ 单 tick 前进 ≥ 2 格,穿过未查过的格(防跳格隧穿)", nameof(patientSpeed));

            _bands = bands;
            _patientSpeed = patientSpeed;
        }

        public int TrackedCount => _states.Count;

        /// <summary>`EcozoneOf` 累计调用次数(F-13.2 的 `HomeRegion` 求值次数)。
        /// <para>AC 第 6 条「只在初始化时求一次」⇒ 本值须恒 = `TrackedCount` 的入表次数
        /// (每次 <see cref="OnPresentEntered"/> 恰 +1,<see cref="Step"/> 不得再增)。
        /// ⚠️ 测试接缝(评审 F-3 修复):此前无任何可观测点,「只求一次」不可证伪。</para></summary>
        public int EcozoneOfCallCount => _ecozoneOfCalls;

        /// <summary>病人进入在场范围(GDD §Rules 表「病人进入在场范围」行)——
        /// 位置由 `WorldPos` 格 + 烘焙锚点播种,`acc := 0`。
        /// <para>⚠️ 13 **只登记**,进入判定由 9 给(TR-patient-006/013)。</para></summary>
        public void OnPresentEntered(PatientId id, WorldPos seedCell)
        {
            WorldPos anchor = _spawnAnchorOf(id);
            _ecozoneOfCalls++;   // F-13.2:EcozoneOf 求值点唯一在此(AC 第 6 条「只求一次」)
            _states[id.Value] = new PatientSpatialState
            {
                // HomeRegion 只求一次(静态定义)—— 入表后不再回改
                HomeRegion = _knowledge.HomeRegion(anchor),
                Pose = LogicalPose.Seed(seedCell),
                KnowsClinic = _knowledge.KnowsClinic(anchor)
            };
        }

        /// <summary>病人离开在场范围 —— 13 仅丢弃派生态(不生成 / 不删除病人实体)。</summary>
        public void OnPresentLeft(PatientId id) => _states.Remove(id.Value);

        /// <summary>加载后重置(AC-13-B4 / story-004 B1 修复):全部派生态归零。
        /// <para>⚠️ **story-004 修复**:原实现只重置 `Pose`,遗漏 `Phase` / `HomeRegion` / `KnowsClinic` ——
        /// 导致读档后求医子相与 HomeRegion 缓存**跨加载存活**。现全部归零,与「 freshly constructed 」语义一致。
        /// <para>⚠️ **Cell 也归零**:`LogicalPose.Seed` 保留 `Cell` 但归零 `Acc` —— 重放后 Cell 不同。
        /// 现 `Cell` 也归零(重新播种到起始格),与「 freshly constructed 」语义一致。</para></summary>
        public void ResetForLoad()
        {
            var keys = new List<int>(_states.Keys);
            foreach (var k in keys)
            {
                var s = _states[k];
                s.Pose = LogicalPose.Seed(new WorldPos(0, 0, 0));   // Cell + Acc 全归零
                s.Phase = SeekingPhase.EnRoute;            // 求医子相归零(重新求值)
                s.HomeRegion = EcozoneId.None;             // HomeRegion 缓存清空(重新求值)
                s.KnowsClinic = false;                     // 医馆认知清空(重新求值)
                _states[k] = s;
            }
        }

        /// <summary>取某一病人的派生态(只读;不存在返回 null)。</summary>
        public PatientSpatialState StateOf(PatientId id)
            => _states.TryGetValue(id.Value, out var s) ? s : null;

        /// <summary>取某一病人**本 tick 的求医子相**(F-13.2 / §States;不存在 ⇒ `EnRoute`)。
        /// <para>⚠️ 亦即 F-13.6 `AwaitingCare`(优先级 3)的输入 —— 单独暴露以便证伪
        /// 「`AtClinic` = 格成员判定」(评审 F-1)。</para></summary>
        public SeekingPhase PhaseOf(PatientId id)
            => _states.TryGetValue(id.Value, out var s) ? s.Phase : SeekingPhase.EnRoute;

        // ── 测试接缝(生产侧不调用)───────────────────────────────
        /// <summary>覆盖某病人的路径(测试用 —— 生产由注入的 <c>_pathOf</c> 提供)。</summary>
        public void SetPathForTest(PatientId id, IReadOnlyList<WorldPos> path)
            => _pathOverride[id.Value] = path;

        /// <summary>直接写入某病人的位姿(测试用 —— 模拟存档前状态)。</summary>
        public void SetPoseForTest(PatientId id, in LogicalPose pose)
        {
            if (_states.TryGetValue(id.Value, out var s)) { s.Pose = pose; }
        }

        /// <summary>单 tick 推进全部在场病人。
        /// <para>⚠️ **升序 id 求值**(TR-patient-017 求值序钉死)—— 与字典插入序无关。
        /// 读上 tick 快照 ⇒ 无链式反应。</para>
        /// <para>本 tick 的推进结果写回 `_states`(读上 tick 快照的写法:先算后写,
        /// 不中途读他人本 tick 结果)。</para></summary>
        public void Step(
            long tick,
            WorldPos playerCell,
            Func<PatientId, bool> frozenOf,
            Func<PatientId, BehaviorState> behaviorOf,
            Func<PatientId, SessionState> sessionOf,
            Func<PatientId, bool> terminalOf)
        {
            // 升序 id(求值序钉死)—— 不依赖 Dictionary 枚举序
            var ids = new List<int>(_states.Keys);
            ids.Sort();

            foreach (int key in ids)
            {
                var state = _states[key];
                var id = new PatientId(key);

                // 在场判定**消费 9 的输出**(TR-patient-006)—— 13 不自建在场定义
                if (!_presence.IsPresent(id))
                    continue;

                _onEvaluated?.Invoke(id);   // 求值序探针(测试接缝;生产 null)

                // 子相:**病人格 ∈ ClinicCells ⇒ AtClinic**(GDD §States:239-260)。
                // ⚠️ **判据是「格」不是「路径游标」**(评审 F-1 修复)——
                //    GDD 原文:「`SeekingPhase(p) = AtClinic if p.Cell ∈ ClinicCells`」
                //    + 变量表「`AtClinic` | 病人格 ∈ `ClinicCells` | **整数格成员判定**(禁 sqrt)」。
                //    旧实现用 `PathCursor >= path.Count - 1`(路径身份量)代偿几何量:
                //      · 假阳 —— 路径终点非医馆格(巡逻 / 失败回退路径)时仍判 AtClinic;
                //      · 假阴 —— 路径**途经**门前锚点格而终点在其后时漏判;
                //      · 单格路径(`Count <= 1`,病人**已站**在医馆格)恒判 EnRoute,与 :242 冲突。
                //    `ClinicCells` 是 24 的 `CONTEXT_TABLE` 房间格 ∪ 52 的 `CLINIC_FRONT` 锚点格,
                //    由只读谓词注入(与 `_pathOf` 同型,零写 sim)。路径只决定「往哪走」。
                var path = _pathOverride.TryGetValue(key, out var ov) ? ov : (_pathOf(id) ?? Array.Empty<WorldPos>());
                bool atClinic = _inClinicCells(state.Pose.Cell);
                var phase = atClinic ? SeekingPhase.AtClinic : SeekingPhase.EnRoute;
                state.Phase = phase;   // 可观测(F-1 修复;F-13.6 `AwaitingCare` 的输入)

                var moving = LogicalStepper.Moving(new MovingInputs(
                    terminal: terminalOf(id),
                    behavior: behaviorOf(id),
                    phase: phase,
                    session: sessionOf(id),
                    frozen: frozenOf(id)));

                LogicalStepper.Step(ref state.Pose, moving, _patientSpeed, path);

                // 不变量:0 ≤ acc < FIX_ONE(EC-13-04)—— 破了立即暴露,不静默传播
                if (!LogicalStepper.AccInvariantHolds(state.Pose.Acc))
                    throw new InvalidOperationException(
                        $"[EC-13-04] acc 破不变量:patient {key} acc={state.Pose.Acc.Raw} raw(须 ∈ [0,{Fix.OneRaw}))");

                _states[key] = state;
            }
        }
    }
}
