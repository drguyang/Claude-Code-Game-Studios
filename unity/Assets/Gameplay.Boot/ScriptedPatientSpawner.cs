// 脚本产病人通道(N2 的第二个动作 · 2026-11-14)。
//
// 权威:
//   `design/gdd/content/chapter-plan.md` §0.2(甲案:门只约束**节奏 / 环境**产病人,
//      脚本病案走不受门约束的路径)· §0.4(甲′:孩子与挑夫**本身就是病人**,伤是叙事)
//   `design/levels/prologue-beat-script.md` P2-04(孩子)/ P2-07(挑夫)
//   ADR-030 §③(`DiseaseOnset` 写者 = `PatientSpawner`)· ADR-005(主机唯一 Step)
//
// ═══ 它为什么不能复用 `PatientAppearedDriver` ═════════════════════════════════
//   那条路径同时带来**三样**只属于「环境产病人」的东西:
//     ① 剧情阶段门(帐篷);② 每 `SpawnIntervalTicks` 一次的**节奏**;③ 接线期上限哨(2 人)。
//   脚本病案一样都不要 —— 它由剧情点名、要立刻出现、数量由剧本定。
//   ⇒ 与其给驱动加三个开关,不如把「创建 + 登记」这一层单独提出来(**同一个** `PatientSpawner`,
//      **同一个** `IPresenceWriter`,只是换一个调用点)。这与 52 池那条
//   (`EventPoolPatientSpawner.SpawnPatient`)是同构的,不是第二套真源。
//
// ═══ 边界 ═════════════════════════════════════════════════════════════════
//   ① **不写三流以外的东西**:它调用 `PatientSpawner`,后者写 `DiseaseOnset` —— 与所有病人同一条路。
//   ② **不设自己的上限**:有界性归 sim(`PATIENT_APPEARANCE_CAP = 24`,ADR-008 §六),超界由流拒收。
//   ③ **无门、无节奏、无接线哨**:上面三样都是环境路径的属性,不是病人的属性。
//
// ⚠️ **`AddPresent` 不可省**:`EventStream.Append` 的 AC-15 在场检查会拒收未登记的病人 ⇒
//   省掉它则流里有 `DiseaseOnset` 但 13 看不见、9 不 Step、37 不开案:**事件有了,世界没变**。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 剧情点名的病人出现:按给定病种与给定格创建病人并登记在场。
    /// <para><b>不受剧情阶段门约束</b>(门只接在环境路径上,见文件头)。</para>
    /// </summary>
    public sealed class ScriptedPatientSpawner
    {
        private readonly Func<int, long, PatientId> _spawnCore;
        private readonly IPresenceWriter _presence;
        private readonly Func<long> _tickNow;

        /// <summary>构造。</summary>
        /// <param name="spawnCore">真正的创建器(生产 = `PatientSpawner.SpawnNext`;
        /// 夹具可换记录器 —— 但「创建后必须登记在场」这条判据仍由本类持有)。</param>
        /// <param name="presence">在场写面(AC-15 前置)。</param>
        /// <param name="tickNow">当前逻辑 tick 的读取器(节拍动作没有 tick 形参,由它现取)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public ScriptedPatientSpawner(Func<int, long, PatientId> spawnCore,
                                      IPresenceWriter presence,
                                      Func<long> tickNow)
        {
            _spawnCore = spawnCore ?? throw new ArgumentNullException(nameof(spawnCore));
            _presence = presence ?? throw new ArgumentNullException(nameof(presence));
            _tickNow = tickNow ?? throw new ArgumentNullException(nameof(tickNow));
        }

        /// <summary>按当前 tick 创建一名脚本病人。</summary>
        /// <param name="diseaseId">病种 id(≥ 0;调用方给的不是用户输入,故越界即抛)。</param>
        /// <param name="cell">出现格(在场登记用)。</param>
        /// <returns>新病人的 id。</returns>
        public PatientId Spawn(int diseaseId, WorldPos cell) => Spawn(diseaseId, cell, _tickNow());

        /// <summary>按**显式** tick 创建一名脚本病人(夹具 / 回放用)。</summary>
        /// <param name="diseaseId">病种 id(≥ 0)。</param>
        /// <param name="cell">出现格。</param>
        /// <param name="tick">onset tick(≥ 0)。</param>
        /// <returns>新病人的 id。</returns>
        /// <exception cref="ArgumentOutOfRangeException">病种 id 或 tick 为负。</exception>
        public PatientId Spawn(int diseaseId, WorldPos cell, long tick)
        {
            if (diseaseId < 0)
                throw new ArgumentOutOfRangeException(nameof(diseaseId), diseaseId, "病种 id 不得为负");
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "onset tick 不得为负");

            PatientId spawned = _spawnCore(diseaseId, tick);
            _presence.AddPresent(spawned, cell);
            return spawned;
        }
    }
}
