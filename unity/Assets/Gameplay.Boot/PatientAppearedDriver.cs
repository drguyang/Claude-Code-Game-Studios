// sprint-05 T1.1 · 病人出现驱动(2026-10-10)
//
// 权威来源:
//   ADR-030 §③(9 的 DiseaseOnset 写者 = PatientSpawner)· ADR-005(主机唯一 Step)
//   disease-simulation.md 规则六(onset = 病史流第一行)· OQ-8(在场才模拟)
//   F-6 复评实测:spawner 零生产调用方 ⇒ 运行期链可达性 0% ⇒ 本驱动补上调用方
//
// 设计口径:
//   - 驱动 = tick 边沿按节奏调 SpawnNext;节奏 = 每 SpawnIntervalTicks 一个病人。
//   - **首 tick 即 spawn 一个**(B 案 · 2026-10-10 用户裁定):接线期人工 playtest 要
//     「有病人在场」可立刻观察,把可见性从 tick 速率里解耦。真实节奏归 52 + 6。
//   - 接线期上限哨 MaxPatientsDuringWiring(3):playtest 主观报告「随 WASD 出现很多个」
//     的根因不是 spawn 变快,而是 tick 在编辑器里推进极慢(300 帧仅 +2 tick)⇒ 玩家
//     观察窗口一拉长,tick 累积过 400 就再 spawn。上限哨让接线期病人数有界可数,
//     不刷屏;真节奏(52 随机事件导演)到位后本哨应删除。
//   - 节奏数值(SpawnIntervalTicks)是**接线旋钮**,不是裁定项 —— 归数值轮。
//     当前值 400 tick(20 Hz 下 = 20 秒一个病人)只保证「运行期有病人」可观察,
//     不表达设计意图(真实节奏由 52 随机事件导演 + 6 世界生态区产出)。
//   - 病种来源:注册表第一个条目的 DiseaseId(最小内联注册表 ⇒ 恒 1)。
//     注册表未建的现状下这是唯一不依赖假数据的取法。
//     🔁 **2026-11-08 订正**:改为**按下标轮转**(第 n 个病人取 `(index + n) % Count`)。
//        病因不是「取法不好」,是**副作用**:两名病人恒同病种 ⇒ 医师永远只见得到**一个病种**
//        ⇒ 病名册恒 1 条词 ⇒ 「换词改判 / 旧笔同屏」在真包里**结构上不可达**
//        (`[tutorialdiag] 病名册环 … seen=1`,两个病人各查一遍仍是 1)。
//        真表 16 行时两名病人拿到两个不同病种;单行注册表(测试 fixture)退化为原行为。
//   - 🔁 **2026-11-09(T4)**:轮转**先过投放资格**(`PatientDrawEligibility`:技法门硬 + 等级门软)。
//     「未解锁的病例不出现」自此在**本驱动**上也成立 —— 改前它把整张注册表无差别轮转,
//     `treatable_by` / `min_skill_level` 两列在生产里**零消费方**(列有值,没人看)。
//   - 有界性:驱动**不**强制 PATIENT_APPEARANCE_CAP(24)—— 那是 9 的 sim 侧硬界
//     (ADR-008 §六);本驱动只保证「按节奏出现」,超界由 sim 侧拒收(承 ADR-030 §有界性)。
//     ⚠️ 但驱动**自己**守一个更小的接线期上限(见上),两者不冲突:24 是 sim 硬界,
//     3 是本驱动的接线期观测哨。
//
// ⚠️ 单线程:tick 边沿由 BootRoot 的 Update 泵驱动,与 sim 的「主机唯一 Step」同线程。
//
// ⚠️ 在场登记前置(2026-10-10 实测):EventStream.Append 的 AC-15 在场检查
//   (`EventStream.cs:77`)会**拒收**不在 PresenceRegistry 的病人的事件 ——
//   这是设计正确性(防幽灵病人),但意味着 spawn 前必须先 AddPresent。
//   本驱动因此持有 IPresenceQuery 引用,在 SpawnNext 之前登记。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 病人出现驱动 —— 把 <see cref="PatientSpawner"/> 接进 tick 边沿序列(sprint-05 T1.1)。
    /// <para><b>只驱动,不持游戏状态</b>(与 BootRoot 同纪律):spawn 的真源在事件流,
    /// 本类只按节奏触发写事件。</para>
    /// </summary>
    public sealed class PatientAppearedDriver
    {
        /// <summary>出现节奏:tick 间隔。400 tick @ 20 Hz = 20 秒一个病人。</summary>
        // 🔁 **2026-10-29 听测前置批(`TD-listen-01` 的 L-4)**:原 400 tick = **20 s/人**,
        //    而原本的病程只有 1.55 s ⇒ 三个人**永远不可能同时活着**(凑齐 3 人需 ~40 s)。
        //    现值 60 tick = **3 s/人** ⇒ 3 人在 6 s 内到齐,而 L-3 拉长后的可听窗口 ≈ 10 s
        //    ⇒ 三人同时在"活"的区间成立。
        //    ⚠️ 本值自陈为「接线旋钮,非裁定项」(见文件头注)⇒ 改动不触碰任何 GDD 数值。
        private const int SpawnIntervalTicks = 60;

        /// <summary>接线期病人数上限哨(B 案):真节奏(52)到位后删除本常量与相关分支。
        /// <para>不是 sim 硬界 —— <see cref="DaYiJingCheng.Sim.EventStream.PATIENT_APPEARANCE_CAP"/>
        /// (24)才是;本哨只防接线期观测面被刷屏。</para>
        /// <para>🔁 <b>2026-11-06(P0 空间解 · 用户裁定)</b>:3 → <b>2</b> —— 与
        /// <see cref="PatientWiringPositions"/> 的两格一一对应(每格至多一个病人,
        /// 否则「分居两角」会被叠加成「同格重叠」)。</para></summary>
        private const int MaxPatientsDuringWiring = 2;

        private readonly PatientSpawner _spawner;
        private readonly IPresenceWriter _presence;
        /// <summary>按出现序轮转的注册表行(以 <c>registryIndex</c> 为起点)。
        /// <para>扫到第一个**通过投放资格**的行即取它;单条注册表 ⇒ 全部同一个
        /// (与轮转前逐位相同)。</para></summary>
        private readonly DiseaseRegistryEntry[] _rotation;
        /// <summary>投放资格判据(T4/T5)。<c>null</c> = 未接线 ⇒ 全部放行(与接线前逐位相同)。</summary>
        private readonly Func<DiseaseRegistryEntry, PatientDrawDecision> _admit;
        /// <summary>下一轮从哪一格开始扫(落选时停在原位,由下一次重新扫)。</summary>
        private int _rotationCursor;
        private long _lastSpawnTick = -1;
        private int _spawnedCount;
        private bool _loggedNoAdmissible;

        /// <param name="spawner">病人创建器(9 的 DiseaseOnset 写者)。</param>
        /// <param name="presence">在场登记写面(spawn 后须登记,否则 EventStream 的 AC-15 拒收)。</param>
        /// <param name="registry">病种注册表(默认取第一个条目;空表 fail-loud)。</param>
        /// <param name="registryIndex">取第几条(2026-10-29 · `TD-listen-01` 的 L-3:听测专用病种 = 1)。
        /// 默认 0 ⇒ 与接线前**逐位相同**。</param>
        /// <param name="admit">投放资格判据(T4/T5 · `PatientDrawEligibility` 的结果)。
        /// <b><c>null</c> = 未接线 ⇒ 不拦</b>(与 `ITreatableByLookup` 的 null 语义同源;
        /// 109 处既有夹具由此保持逐位不变)。返回 <c>PatientDrawDecision</c> 而不是 bool,
        /// 是为了让**钩子标记**能传出来(T5 §5.2 的「必须指出缺哪门技法」依赖它)。
        public PatientAppearedDriver(PatientSpawner spawner, IPresenceWriter presence,
                                      IReadOnlyList<DiseaseRegistryEntry> registry,
                                      int registryIndex = 0,
                                      Func<DiseaseRegistryEntry, PatientDrawDecision> admit = null)
        {
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
            _presence = presence ?? throw new ArgumentNullException(nameof(presence));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (registry.Count == 0)
                throw new ArgumentException(
                    "注册表为空 —— 无病种可 spawn;组合根的最小内联注册表应至少 1 条(R1-17)",
                    nameof(registry));
            if (registryIndex < 0 || registryIndex >= registry.Count)
                throw new ArgumentOutOfRangeException(
                    nameof(registryIndex), registryIndex,
                    $"registryIndex 须在 [0, {registry.Count}) —— 越界会取到不存在的病种");
            // 轮转序列 = 从 registryIndex 起绕一圈(整表参与,不只第一条)
            _rotation = new DiseaseRegistryEntry[registry.Count];
            for (int i = 0; i < registry.Count; i++)
                _rotation[i] = registry[(registryIndex + i) % registry.Count];
            _admit = admit;
        }

        /// <summary>
        /// tick 边沿驱动 —— 按节奏 spawn 病人。
        /// </summary>
        /// <param name="tick">本边沿自己的逻辑 tick(由帧泵回推,非末 tick)。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> 为负。</exception>
        public void OnTickEdge(long tick)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "逻辑 tick 不得为负");
            if (tick == _lastSpawnTick) return;

            // B 案(2026-10-10 用户裁定):首 tick 即 spawn 一个 —— 把「有病人在场」的可见性
            // 从 tick 推进速率里解耦(编辑器里 tick 极慢:300 帧仅 +2 tick)。
            // 接线期上限哨:达到后本驱动静默退出(不 spawn、不抛 —— 上限不是错误态)。
            bool firstTick = _lastSpawnTick < 0;
            if (!firstTick && tick - _lastSpawnTick < SpawnIntervalTicks) return;
            if (_spawnedCount >= MaxPatientsDuringWiring) return;

            // 在场登记前置:SpawnNext 之前登记,否则 EventStream.Append 的 AC-15
            // 在场检查拒收(2026-10-10 实测:事件数 0,无异常)。
            // 🔁 **2026-11-06(P0 空间解 · 用户裁定)**:登记格由字面量 `(0,0,0)` 改为
            //    `PatientWiringPositions` 的两内角之一 —— 此前**所有病人叠在同一格**上,
            //    既互相重叠,也让「病人把灶台压住」这类走查无法判定。
            //    ⚠️ 真实位置仍归 6 世界生态区 + 13 病人 AI 的空间侧;本表是接线占位。
            // 🔁 2026-11-08:按出现序轮转病种(见文件头注的订正条)。
            // 🔁 2026-11-09(T4):轮转序列先过**投放资格**(技法门硬 + 等级门软)——
            //    从游标起绕一圈取第一个通过者。全表落选 ⇒ 本拍**不产病人**
            //    (那是「还没解锁」的正常态,不是错误:不抛、不写半条事件)。
            //    ⚠️ `_admit == null` ⇒ 不拦,且取的格子与旧实现(`_spawnedCount % len`)
            //       逐位相同 —— 109 处既有夹具因此零改动。
            DiseaseRegistryEntry picked = null;
            PatientDrawDecision pickedDecision = default;
            for (int k = 0; k < _rotation.Length; k++)
            {
                int idx = (_rotationCursor + k) % _rotation.Length;
                DiseaseRegistryEntry candidate = _rotation[idx];
                if (_admit == null)
                {
                    picked = candidate;
                    pickedDecision = default;
                    _rotationCursor = (idx + 1) % _rotation.Length;
                    break;
                }
                PatientDrawDecision decision = _admit(candidate);
                if (decision.Admitted)
                {
                    picked = candidate;
                    pickedDecision = decision;
                    _rotationCursor = (idx + 1) % _rotation.Length;
                    break;
                }
            }
            if (picked == null)
            {
                // 前推节奏哨:否则每帧重扫 + 日志刷屏。真解锁后下一次扫描自然能成,
                // 故本分支**不**是终止态。
                _lastSpawnTick = tick;
                if (!_loggedNoAdmissible)
                {
                    _loggedNoAdmissible = true;
                    if (WalkthroughDiagnostics.Enabled)
                        UnityEngine.Debug.Log(
                            $"[tutorialdiag] 病人池为空 —— {_rotation.Length} 条注册表行全部未通过投放资格" +
                            "(技法未解锁 / 等级不足 / 数据未覆盖);解锁或补数据后自动恢复");
                }
                return;
            }

            int diseaseId = picked.DiseaseId;
            var patientId = _spawner.SpawnNext(diseaseId, tick);

            // 走查读数(仅 `-tutorialdiag`;默认零开销):回答「注册表有几条 / 第 n 个病人拿了哪个病种」。
            // 2026-11-08 加 —— 此前只能从 `病名册环 seen=` 反推,而那个读数分不清
            // 「两个病人同病种」与「第二次查体没生效」。
            if (WalkthroughDiagnostics.Enabled)
            {
                // T5:钩子病例必须在走查读数里可见 —— 否则「这一例为什么治不了」只能靠玩家猜。
                string hook = pickedDecision.IsHookCase ? " hook=1" : " hook=0";
                string missing = pickedDecision.MissingTechniques.Count == 0
                    ? ""
                    : " 缺技法=[" + string.Join(",", pickedDecision.MissingTechniques) + "]";
                UnityEngine.Debug.Log(
                    $"[tutorialdiag] 病人出现 #{_spawnedCount} id={patientId.Value} " +
                    $"disease={diseaseId} 注册表={_rotation.Length}{hook}{missing}");
            }
            _presence.AddPresent(patientId, PatientWiringPositions.CellFor(_spawnedCount));
            _lastSpawnTick = tick;
            _spawnedCount++;
        }

        /// <summary>接线期已 spawn 的病人数(测试接缝 —— 可证伪上限哨与首 tick spawn)。</summary>
        public int SpawnedCount => _spawnedCount;
    }
}
