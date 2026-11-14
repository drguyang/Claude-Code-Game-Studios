// 序章主链的**端到端**判据(`写端到端夹具走完整条链` · 2026-11-14)
//
// ═══ 它补的是什么 ══════════════════════════════════════════════════════════
//   序章三段节拍表(P1 七拍 / P2 八拍 / P3 四拍)每拍都有独立夹具,但
//   **「采集 → 帐篷 → 病人 → 立案」这条主链从头到尾没有任何一条夹具走过一次**。
//   每一环单独绿,不等于把它们接起来还绿 —— 本件就是把它们接起来。
//
// ═══ 五环,逐环走**生产路径** ═══════════════════════════════════════════════
//   ① 采集   `services.HarvestDriver.TryHarvest` + `Advance` ⇒ 真节点(资源点行序 0,
//             cell (116,0,20), item_key 4 = 止血草)⇒ `ResourceHarvested` 落流。
//   ② 建帐篷 `services.StructureWriter.Place(TentCell, 10, …)` ⇒ `StructurePlaced` 落流。
//             ⚠️ **材料门不在这条路径上** —— 见下方「已知断链」第 ① 条。
//   ③ 病人   `DiseaseOnset` 在帐篷建成**之后**出现(剧情阶段门打开)。
//   ④ 立案   `services.CaseOpenedDriver.TryOpen(病人, tick)` ⇒ `CaseOpened` 落流。
//   ⑤ 事实   `PrologueFactCounter` 读到第三段的三条判据:
//             `PlacedCount(10) ≥ 1` · `Of(DiseaseOnset) ≥ 1` · `Of(CaseOpened) ≥ 1`
//             —— 这三条正是 P3-02b / P3-04 / P3-05 的触发条件。
//
// ═══ ⚠️ 本件**不**覆盖的两环(禁借绿,写在最上面)═══════════════════════════
//   **①「5 根木柱从哪来」今天没有任何合法路径** —— 本轮实测,两条都断:
//     · **采集断**:`HarvestWorldQuery.GatherOf(8)` 对 `wooden_post`(category = `build_part`)
//       直接 `throw`(「非 material ⇒ 没有 gather_profile」)。而 17 的 P0 品种闭集
//       (`AC-17-01b`,carrier `Tests/EditMode/Foraging/foraging_data_load_test.cs:666-708`)
//       明确**只允许四味药材**,第 5 个 gather_profile 混入即红 ⇒ 给木柱补分布 = 改一条
//       BLOCKING AC,须裁定,不是实现动作。
//     · **合成断**:`assemble_wooden_post`(`item_database_recipes.json` ordinal 3,
//       3 × 干柳树皮 → 1 木柱)**没有任何生产 driver** —— `Craft` 在
//       `Gameplay.Boot` / `Gameplay.Presentation` 里零写入方。
//     ⇒ 后果:`BuildController.TryPlace(10, …)` 对真玩家**永远**返回 `InsufficientStock`。
//       本件因此**不**断言「玩家能建起帐篷」,只断言「帐篷建成之后的那条链是通的」。
//       断链本身登记在 `docs/tech-debt-register.md`(`TD-building-07`)。
//   **② 第三段的区域门(P3-01 `x ≥ 96`)**需要玩家真的走到那儿 —— 那是输入驱动,
//      batch PlayMode 里没有输入。本件只钉第三段**读的三条事实**,不钉区域门
//      (区域门由 `region_trigger_test` / `prologue_third_act_test` 各自覆盖)。
//
// ═══ 形态 ═══════════════════════════════════════════════════════════════════
//   承 `case_opened_wiring_test`(真启动序 + Addressables world 卸载 + captureDeltaTime 恢复)。
//   ⚠️ `Time.captureDeltaTime` 是**全局态**,TearDown 必须恢复 0,否则污染同套件其他用例。

using System;
using System.Collections;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Gameplay.Presentation.Player;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>序章主链(采集 → 帐篷 → 病人 → 立案 → 第三段事实)的端到端判据。</summary>
    public class PrologueEndToEndTest
    {
        /// <summary>启动序超时(与同族夹具同值)。</summary>
        private const float BootTimeoutSeconds = 15f;

        /// <summary>等病人的帧预算(门在帐篷建成后 1 tick 内就开 —— 80 帧 = 4 倍余量)。</summary>
        private const int PatientWaitFrames = 80;

        /// <summary>Tent 模块 id(`build_modules.json`;与 `PatientGateTestSupport` 同值)。</summary>
        private const int TentModuleId = 10;

        /// <summary>canonical 关卡的 Tent 槽(`world_buildslots.json`)。</summary>
        private static readonly WorldPos TentCell = new WorldPos(108, 0, 16);

        /// <summary>资源点行序 0(`world_resources.json` 第一行)。
        /// ⚠️ 必须是**有 `gather_profile` 的品种** —— 止血草(item_key 4)。</summary>
        private static readonly WorldPos HarvestCell = new WorldPos(116, 0, 20);

        private GameObject _root;
        private BootRoot _boot;
        private bool _captureActive;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_captureActive)
            {
                Time.captureDeltaTime = 0f;
                _captureActive = false;
            }

            // ⚠️ 不用 `GameObject.Find`(只找 active;`pm-teardown-find` 门)。
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || !go.scene.IsValid()) continue;
                if (go.name == "Player") UnityEngine.Object.Destroy(go);
            }

            AsyncOperationHandle<SceneInstance> handle = default;
            bool valid = false;
            if (_boot != null)
            {
                try
                {
                    var field = typeof(BootRoot).GetField("_worldLoadHandle",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    handle = (AsyncOperationHandle<SceneInstance>)field.GetValue(_boot);
                    valid = handle.IsValid();
                }
                catch (Exception)
                {
                    valid = false;
                }
            }

            if (valid)
            {
                var unload = Addressables.UnloadSceneAsync(handle);
                while (!unload.IsDone)
                    yield return null;
            }

            if (_root != null) UnityEngine.Object.Destroy(_root);
            yield return null;
        }

        /// <summary>
        /// 主链一次走完:采集一个真节点 → 建帐篷 → 等病人 → 立案 → 三条第三段事实齐备。
        /// </summary>
        [UnityTest]
        public IEnumerator test_endToEnd_harvestThenTentThenPatientThenCase()
        {
            yield return Boot();
            CompositionRootServices services = Services();

            long tick = services.TickDriver.CurrentTick;
            int playerId = PlayerActorId();
            Assert.GreaterOrEqual(playerId, 0, "启动序应铸造并登记本机玩家 id");

            // ── 第 ⓪ 环(基线):帐篷未建 ⇒ 门关着 ───────────────────────────────
            int onsetsBefore = CountKind(services, EventKind.DiseaseOnset);
            int harvestedBefore = CountKind(services, EventKind.ResourceHarvested);
            Debug.Log($"[ProE2E] 基线:tick={tick} 玩家={playerId} " +
                      $"DiseaseOnset={onsetsBefore} ResourceHarvested={harvestedBefore}");

            // ── 第 ① 环:采集(生产传送 + 生产按键路由 + 生产 tick 推进)─────────────
            // ⚠️ 采集是**持续交互**(`duration_ticks` = 20),而推进它的唯一生产入口是
            //    `InteractionPressRouter.OnTick` —— 它每 tick 拿**玩家真实格**调 `Advance`,
            //    玩家格一变就按「移动打断」清零(规则六)。所以夹具**必须先让玩家真的站在
            //    那个格上**(经 ADR-025 的传送契约),否则手动 `Advance` 会被下一帧的
            //    生产路由当场取消 —— 而夹具自己伪造的 tick 又会撞 `BCD-码-3`。
            services.TeleportSink.RequestTeleport(playerId, HarvestCell);

            PlayerController player = FindPlayer();
            Assert.IsNotNull(player, "启动序应实例化 PlayerController(传送命令的落点)");
            var teleportDeadline = DateTime.UtcNow.AddSeconds(10);
            while (player.Cell != HarvestCell && DateTime.UtcNow < teleportDeadline)
                yield return null;
            Assert.AreEqual(HarvestCell, player.Cell,
                $"传送后玩家应站在资源点格 {HarvestCell} 上(实际 {player.Cell})");

            tick = services.TickDriver.CurrentTick;
            var intent = new InteractIntent(HarvestCell, pressed: true, tick: tick);
            HarvestAttempt armed = services.HarvestDriver.TryHarvest(in intent, playerId);
            Assert.IsTrue(services.HarvestDriver.IsHolding,
                $"在资源点格 {HarvestCell} 上按交互 ⇒ 采集窗口应起表(实际 {Describe(armed)})。" +
                "起不来 = 候选集/半径/资源点索引之一断链");

            // 归生产路由推进:跑够 `duration_ticks`(20)+ 余量,等 `ResourceHarvested` 落流
            int harvestedAfter = harvestedBefore;
            var harvestDeadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < harvestDeadline)
            {
                yield return null;
                harvestedAfter = CountKind(services, EventKind.ResourceHarvested);
                if (harvestedAfter > harvestedBefore) break;
            }
            Debug.Log($"[ProE2E] 采集结算后 ResourceHarvested:{harvestedBefore} → {harvestedAfter}");

            Assert.Greater(harvestedAfter, harvestedBefore,
                "采集窗口满时长后病史流应多一条 `ResourceHarvested` —— " +
                "0 增量意味着生产路由没把它推进到结算(或被「移动/受伤」静默打断)");

            // ── 第 ② 环:帐篷落流 ─────────────────────────────────────────────
            tick = services.TickDriver.CurrentTick;
            long structureId = services.StructureWriter.Place(
                TentCell, TentModuleId, orientation: 0, variant: 0, tick);
            Assert.Greater(structureId, 0,
                $"在 Tent 槽 {TentCell} 上落结构应返回正 id(实际 {structureId})");

            var facts = new PrologueFactCounter();
            facts.Sync(services.Stream.Events, services.Decoder);
            Assert.AreEqual(1, facts.PlacedCount(TentModuleId),
                $"帐篷落流后 `PlacedCount({TentModuleId})` 应为 1 —— 这是 P3-02b 的**唯一**判据");

            // ── 第 ③ 环:病人出现(门在帐篷建成后才开)────────────────────────────
            int onsetsAfter = onsetsBefore;
            for (int i = 0; i < PatientWaitFrames; i++)
            {
                yield return null;
                onsetsAfter = CountKind(services, EventKind.DiseaseOnset);
                if (onsetsAfter > onsetsBefore) break;
            }
            Debug.Log($"[ProE2E] 帐篷建成后 DiseaseOnset:{onsetsBefore} → {onsetsAfter}");
            Assert.Greater(onsetsAfter, onsetsBefore,
                $"[{PatientWaitFrames} 帧] 帐篷建成后应出现 `DiseaseOnset` —— " +
                "增量 0 意味着剧情阶段门没被这顶帐篷打开,或产病人链断在中间");

            // ── 第 ④ 环:立案 ──────────────────────────────────────────────────
            PatientId patient = LastOnsetPatient(services);
            Assert.AreNotEqual(PatientId.None, patient, "刚出现的病人应有正 id");

            Assert.IsTrue(services.Presence.IsPresent(patient),
                $"新出现的病人 {patient.Value} 应已登记在场(立案的前置 ①)");

            tick = services.TickDriver.CurrentTick;
            CaseOpenWriteResult opened = services.CaseOpenedDriver.TryOpen(patient, tick);
            Assert.AreEqual(CaseOpenWriteResult.Opened, opened,
                $"对在场病人 {patient.Value} 立案应成功(实际 {opened})");
            Assert.GreaterOrEqual(CountKind(services, EventKind.CaseOpened), 1,
                "`TryOpen` 返回 `Opened` ⇒ 病例流必须有 `CaseOpened`(返回值与实际写入不许分叉)");

            // ── 第 ⑤ 环:第三段三条事实齐备(节拍表读的就是它们)──────────────────
            facts.Sync(services.Stream.Events, services.Decoder);
            Debug.Log($"[ProE2E] 终态事实:{facts.Summary}");
            Assert.GreaterOrEqual(facts.PlacedCount(TentModuleId), 1, "P3-02b 判据:帐篷建成");
            Assert.GreaterOrEqual(facts.Of(EventKind.DiseaseOnset), 1, "P3-04 判据:有人来看病");
            Assert.GreaterOrEqual(facts.Of(EventKind.CaseOpened), 1, "P3-05 判据:已立案");
        }

        // ═══ 基础设施(与同族夹具同形)════════════════════════════════════════════

        private IEnumerator Boot()
        {
            _root = new GameObject("BootRoot_PrologueE2E");
            _boot = _root.AddComponent<BootRoot>();
            _boot.SaveRootOverride = PlayModeTempSaveRoot.New("prologue_end_to_end");

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), "启动序须在超时内完成");

            // 固定帧节奏(与 landing 探针同纪律):batch 下帧长不定会放大时间窗。
            Time.captureDeltaTime = 1f / 60f;
            _captureActive = true;
        }

        private CompositionRootServices Services()
        {
            var field = typeof(BootRoot).GetField("_services",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var services = (CompositionRootServices)field?.GetValue(_boot);
            Assert.IsNotNull(services, "启动序后 `_services` 应非 null");
            return services;
        }

        private int PlayerActorId()
        {
            var field = typeof(BootRoot).GetField("_playerActorId",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "BootRoot._playerActorId 应存在(玩家 actor 铸造点)");
            return (int)field.GetValue(_boot);
        }

        private bool ReadBooted()
        {
            if (_boot == null) return false;
            var field = typeof(BootRoot).GetField("_booted",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field != null && (bool)field.GetValue(_boot);
        }

        private static int CountKind(CompositionRootServices services, EventKind kind)
        {
            var events = services.Stream.Events;
            int n = 0;
            for (int i = 0; i < events.Count; i++)
                if (events[i].Kind == kind) n++;
            return n;
        }

        private static PlayerController FindPlayer()
        {
            // ⚠️ 不用 `GameObject.Find`(只找 active;`pm-teardown-find` 门)。
            foreach (PlayerController p in Resources.FindObjectsOfTypeAll<PlayerController>())
            {
                if (p == null || !p.gameObject.scene.IsValid()) continue;
                return p;
            }
            return null;
        }

        /// <summary>流里**最后一条** `DiseaseOnset` 的病人 id(病人 id 就住在事件头,不必解载荷)。</summary>
        private static PatientId LastOnsetPatient(CompositionRootServices services)
        {
            var events = services.Stream.Events;
            for (int i = events.Count - 1; i >= 0; i--)
                if (events[i].Kind == EventKind.DiseaseOnset) return events[i].Patient;
            return PatientId.None;
        }

        private static string Describe(HarvestAttempt attempt)
            => attempt.Attempted
                ? (attempt.Outcome.Succeeded
                    ? "成功"
                    : "被拒:" + attempt.Outcome.Failure)
                : "未发起(None)";
    }
}
