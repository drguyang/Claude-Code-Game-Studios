// 真包走查的**读格子**手段(2026-11-06 · 承 S25-4c 走查的「盲走」教训)。
//
// ── 病因 ────────────────────────────────────────────────────────────────────
// S25-3 的走查靠「扫存档里的 `ActorCellEntered` 载荷」拿到玩家格;本轮的存档是
// **增量 checkpoint**(`save_auto.bin`,每 5 分钟 / 手动保存时覆写),没有现成的读法 ⇒
// 走查只能盲走,`E` 按了没反应时也说不清是「没走到跟前」还是「交互坏了」。
//
// ── 本工具做什么 ────────────────────────────────────────────────────────────
// 离线读一份存档,**打印「谁在哪一格」**:
//   · `ActorCellEntered` → actorId + 格(玩家 / 敌人跨越格时的世界流事件,ADR-020 §四)
//   · `EventArrived`     → 事件的 `SpawnAnchor`(病人出现的锚点,52 的掷骰结果)
//   · `DiseaseOnset`     → patientId(病人出现,ADR-030)
//   · `CaseOpened`       → patientId + 病种集(立案;教学 `observe` 步的判据)
//
// ── 边界 ────────────────────────────────────────────────────────────────────
// ⚠️ 住 `Editor.Tools.Spike`(Editor-only,`includePlatforms: ["Editor"]`)—— **不进构建**。
// ⚠️ **只读**:不写任何存档、不改运行期状态;载入走 `SaveService.Load` 的正常路径
//    (含迁移链),不是自己解析字节 —— 免得造出「第二套格式解读」。
// ⚠️ 读的是**落盘的存档**(最近一次 checkpoint),不是内存里的当前态 ⇒ 至多滞后 5 分钟。
//    走查里需要更近的快照时,先在暂停壳按「保存」再跑本工具。
//
// 用法(batch;仓库根下):
//   cd unity && Unity -batchmode -quit -projectPath "$PWD" \
//     -executeMethod DaYiJingCheng.EditorTools.Spike.SaveCellDumpCli.Dump -logFile Logs/celldump.log

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using UnityEditor;
using UnityEngine;

namespace DaYiJingCheng.EditorTools.Spike
{
    /// <summary>存档里的「谁在哪一格」——真包走查的读格子工具(Editor-only)。</summary>
    public static class SaveCellDumpCli
    {
        private const string LogTag = "[CellDump]";

        /// <summary>batch 入口。</summary>
        public static void Dump()
        {
            try
            {
                int rc = Run();
                EditorApplication.Exit(rc);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 失败:{ex.GetType().Name}: {ex.Message}");
                EditorApplication.Exit(1);
            }
        }

        private static int Run()
        {
            string savesRoot = Path.Combine(Application.persistentDataPath, "saves");
            Debug.Log($"{LogTag} saves 根 = {savesRoot}(persistentDataPath = {Application.persistentDataPath})");

            string worldDir = NewestWorldWithAutoSave(savesRoot);
            if (worldDir == null)
            {
                Debug.LogError($"{LogTag} 没有找到任何含 {SaveService.AutoSaveFileName} 的世界目录");
                return 2;
            }
            Debug.Log($"{LogTag} 世界目录 = {worldDir}");

            var pool = new InMemoryBlobPool();
            IReadOnlyList<SimEvent> events = null;

            var service = new SaveService(
                basePath: worldDir,
                codec: new SaveCodec(),
                // ⚠️ folded 面本工具**不用**(只读事件段);传 null = 不额外折叠,不是「无折叠」的断言。
                // ⚠️ narrativeContext 必填(SaveService 构造期 fail-closed):它是**写**侧的
                //    安全上下文闸门(「无未决医疗动作」);本工具**只读**,恒真即可 ——
                //    这里的 `() => true` 只作用在一个从不写盘的实例上,不是给生产开后门。
                narrativeContext: new FuncNarrativeContextQuery(() => true),
                // ⚠️ foldedRowSink 同样必填(fail-closed):折叠行是被折叠病人病史的唯一残留。
                //    本工具**不判折叠行**,但必须**接住**它们 —— 传一个丢弃委托是「本工具不需要」,
                //    不是「存档没有折叠行」。真要读折叠面时另加打印。
                foldedRowSink: _ => { },
                replaySink: e => events = e,
                blobRestoreSink: pool,
                migrations: new ISaveMigration[]
                {
                    new SaveMigrationV2ToV3(), new SaveMigrationV3ToV4(),
                    new SaveMigrationV4ToV5(), new SaveMigrationV5ToV6(),
                });

            service.Load(SaveService.AutoSaveSlot);

            if (events == null)
            {
                Debug.LogError($"{LogTag} 载入未回放任何事件(replaySink 未被调用)—— 存档可能为空或格式不符");
                return 3;
            }

            var decoder = new PayloadDecoder(pool);
            Debug.Log($"{LogTag} 事件总数 = {events.Count}");

            PrintActorCells(events, decoder);
            PrintPatients(events, decoder);
            PrintCases(events, decoder);
            return 0;
        }

        private static string NewestWorldWithAutoSave(string savesRoot)
        {
            if (!Directory.Exists(savesRoot)) return null;
            string best = null;
            DateTime bestTime = DateTime.MinValue;
            foreach (string dir in Directory.GetDirectories(savesRoot))
            {
                string auto = Path.Combine(dir, SaveService.AutoSaveFileName);
                if (!File.Exists(auto)) continue;
                DateTime t = File.GetLastWriteTimeUtc(auto);
                if (t > bestTime) { bestTime = t; best = dir; }
            }
            return best;
        }

        private static void PrintActorCells(IReadOnlyList<SimEvent> events, PayloadDecoder decoder)
        {
            // 每个 actor 的**最后一次**跨格 = 它现在大概在哪
            var last = new Dictionary<int, (WorldPos Cell, long Tick)>();
            int total = 0;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.ActorCellEntered) continue;
                ActorCellEnteredPayload p = decoder.Decode<ActorCellEnteredPayload>(e.Kind, in e.Payload);
                last[p.ActorId] = (p.Cell, p.Tick);
                total++;
            }

            Debug.Log($"{LogTag} ── ActorCellEntered ── 事件 {total} 条,涉及 {last.Count} 个 actor");
            foreach (var kv in last.OrderBy(k => k.Key))
                Debug.Log($"{LogTag}   actor {kv.Key,3} ⇒ 格 ({kv.Value.Cell.X},{kv.Value.Cell.Y},{kv.Value.Cell.Z}) @tick {kv.Value.Tick}");
        }

        private static void PrintPatients(IReadOnlyList<SimEvent> events, PayloadDecoder decoder)
        {
            Debug.Log($"{LogTag} ── 病人出现(病种 / 锚点)──");
            // `DiseaseOnset` 载荷**不含格**;病人的位置来自同一 tick 附近的 `EventArrived.SpawnAnchor`
            // (52 掷骰定了锚点,9 随即建病人)⇒ 按「最近一条 EventArrived」关联,并标出间隔 tick。
            WorldPos? lastAnchor = null;
            long lastAnchorTick = long.MinValue;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind == EventKind.EventArrived)
                {
                    EventArrivedPayload p = decoder.Decode<EventArrivedPayload>(e.Kind, in e.Payload);
                    lastAnchor = p.SpawnAnchor;
                    lastAnchorTick = e.Tick;
                    Debug.Log($"{LogTag}   EventArrived: eventKey {p.EventKey} · 锚点 " +
                              $"({p.SpawnAnchor.X},{p.SpawnAnchor.Y},{p.SpawnAnchor.Z}) · tick {e.Tick}");
                }
                else if (e.Kind == EventKind.DiseaseOnset)
                {
                    DiseaseOnsetPayload p = decoder.Decode<DiseaseOnsetPayload>(e.Kind, in e.Payload);
                    string where = lastAnchor.HasValue
                        ? $"锚点 ({lastAnchor.Value.X},{lastAnchor.Value.Y},{lastAnchor.Value.Z}) · 距上条 EventArrived {e.Tick - lastAnchorTick} tick"
                        : "锚点未知(此前无 EventArrived)";
                    Debug.Log($"{LogTag}   DiseaseOnset: patient {p.PatientId} · 病种 {p.DiseaseId} · tick {p.OnsetTick} · {where}");
                }
            }
        }

        private static void PrintCases(IReadOnlyList<SimEvent> events, PayloadDecoder decoder)
        {
            Debug.Log($"{LogTag} ── 立案 ──");
            int n = 0;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.CaseOpened) continue;
                CaseOpenedPayload p = decoder.Decode<CaseOpenedPayload>(e.Kind, in e.Payload);
                Debug.Log($"{LogTag}   CaseOpened: patient {p.PatientId} · tick {e.Tick}");
                n++;
            }
            Debug.Log($"{LogTag}   共 {n} 条(CaseOpened 是教学 observe 步的判据)");
        }
    }
}
