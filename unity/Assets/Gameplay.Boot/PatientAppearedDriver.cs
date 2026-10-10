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
        private const int SpawnIntervalTicks = 400;

        /// <summary>接线期病人数上限哨(B 案):真节奏(52)到位后删除本常量与相关分支。
        /// <para>不是 sim 硬界 —— <see cref="DaYiJingCheng.Sim.EventStream.PATIENT_APPEARANCE_CAP"/>
        /// (24)才是;本哨只防接线期观测面被刷屏。</para></summary>
        private const int MaxPatientsDuringWiring = 3;

        private readonly PatientSpawner _spawner;
        private readonly IPresenceWriter _presence;
        private readonly int _diseaseId;
        private long _lastSpawnTick = -1;
        private int _spawnedCount;

        /// <param name="spawner">病人创建器(9 的 DiseaseOnset 写者)。</param>
        /// <param name="presence">在场登记写面(spawn 后须登记,否则 EventStream 的 AC-15 拒收)。</param>
        /// <param name="registry">病种注册表(取第一个条目的 DiseaseId;空表 fail-loud)。</param>
        public PatientAppearedDriver(PatientSpawner spawner, IPresenceWriter presence,
                                      IReadOnlyList<DiseaseRegistryEntry> registry)
        {
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
            _presence = presence ?? throw new ArgumentNullException(nameof(presence));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (registry.Count == 0)
                throw new ArgumentException(
                    "注册表为空 —— 无病种可 spawn;组合根的最小内联注册表应至少 1 条(R1-17)",
                    nameof(registry));
            _diseaseId = registry[0].DiseaseId;
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
            // ⚠️ 登记用的 cell 是**接线占位**(0,0,0)—— 真实位置归 6 世界生态区
            //    + 13 病人 AI 的空间侧;本驱动只保证「在场」这一布尔事实。
            var patientId = _spawner.SpawnNext(_diseaseId, tick);
            _presence.AddPresent(patientId, new WorldPos(0, 0, 0));
            _lastSpawnTick = tick;
            _spawnedCount++;
        }

        /// <summary>接线期已 spawn 的病人数(测试接缝 —— 可证伪上限哨与首 tick spawn)。</summary>
        public int SpawnedCount => _spawnedCount;
    }
}
