// story-011 —— 移动压制的**真机层**(L2):玩家在急救动作期间到底走不走得动。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里
// ─────────────────────────────────────────────────────────────────────────────
// 读:**零仓库文件**(全部经生产启动序 + 反射读 `BootRoot` 私有字段)。
// 写:**零写盘**。
//
// ── 为什么必须有这一层 ────────────────────────────────────────────────────────
// L1(`emergency_motor_lease_wiring_test.cs`)只证「租约位被置/清」——那是 10 的写面。
// `AC-10-13` 的目的从句是「动作期间**玩家不动**」,它取决于 1 的**读面**:
// `PlayerController.Move` 读不读 `MotorLease.IsSuppressed`。
// 2026-11-02 实测:此前 `PlayerController` 内 `Suppress` **零命中** ⇒ 就算 10 置了位,玩家照样走。
// 本文件即为「读面已接」的唯一可失败判据。
//
// ── 判据形态(防 F4 空洞正控)─────────────────────────────────────────────────
// 免费移动(A)> 0 是**正控**:证明 `Move` 真的能推动玩家 —— 没有它,「压制后 = 0」
// 可能只是因为 `Move` 根本没生效(空转),而不是因为压制。
// 三步:免费 > 0 · 压制 == 0 · 恢复 > 0(恢复那一步同时证明不是一次性锁死)。
//
// ⚠️ 所有 40 次 `Move` 调用都在**同一帧**内完成(循环里不 yield)——否则 `BootRoot.Update`
//    会用零输入在两次调用之间减速,把三个相位混在一起。
//
// 权威来源:
//   `design/gdd/emergency-procedures.md` 规则九 `:374` · `AC-10-13` `:1056`
//   `design/gdd/player-controller-and-movement.md` 轴 4 `:271` / `AC-1-23`
//   `production/epics/emergency-procedures/story-011-motor-suppression-lease-wiring.md` R-11.4 甲案

using System;
using System.Collections;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Player;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode.EmergencyProcedures
{
    /// <summary>story-011 L2:压制位真的挡住了玩家的水平位移。</summary>
    public class EmergencyMotorSuppressionWiringTest
    {
        private const float BootTimeoutSeconds = 20f;
        private const int ActionOrdinal = 0;

        /// <summary>一次相位内的 `Move` 调用数(同一帧内跑完;40 × dt(1/60)= 0.67 s 等效)。</summary>
        private const int MovesPerPhase = 40;

        /// <summary>减速窗:把上一相位残留的速度清零(60 × dt = 1 s 等效,足够从 5 m/s 减到 0)。</summary>
        private const int SettleMoves = 60;

        private GameObject _root;
        private BootRoot _boot;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = 0f;

            DestroyAllNamed("Player");

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

            if (_root != null)
                UnityEngine.Object.Destroy(_root);

            DestroyAllNamed("Main Camera");
            yield return null;
        }

        // ══════════════ S11-06 后置绑定真的发生了 ══════════════

        [UnityTest]
        public IEnumerator test_s11_06_productionBoot_bindsMotorLeaseToEmergencyDriver()
        {
            yield return Boot();

            var driver = ReadEmergencyChannel();
            Assert.IsNotNull(driver, "服务袋须暴露 EmergencyChannel");
            Assert.IsTrue(driver.HasMotorLease,
                "生产启动序跑完后 `HasMotorLease` 必须为 true —— **删掉 BootRoot 的 BindMotorLease 调用" +
                "不会崩也不会报错**,只有这条断言会把「接线被悄悄删掉」变成红的(story-011 S11-06)");
        }

        // ══════════════ S11-07 玩家真的走不动(R-11.4 甲案)══════════════

        [UnityTest]
        public IEnumerator test_s11_07_suppression_blocksHorizontalMovement_andRestoresAfterEnd()
        {
            yield return Boot();

            var player = ReadPlayer();
            Assert.IsNotNull(player, "启动序应建玩家(PlayerController)");
            var driver = ReadEmergencyChannel();
            Assert.IsNotNull(driver, "服务袋须暴露 EmergencyChannel");
            Assert.IsTrue(driver.HasMotorLease, "前置:租约已绑定");

            Vector3 forward = Vector3.forward;

            // ── 相位 A:免费移动(正控)──────────────────────────
            Settle(player);
            float movedFree = MeasureHorizontal(player, forward);
            Assert.Greater(movedFree, 0.01f,
                $"正控失败:未压制时 `Move` 未能推动玩家(实测 {movedFree:F4} m)。" +
                "没有这条,下面「压制 = 0」可能只是 Move 空转,而不是压制生效(F4 空洞正控)。");

            // ── 相位 B:动作期间 ────────────────────────────────
            Settle(player);
            driver.Arm(ActionOrdinal, new PatientId(ReadFirstPresentId()));
            float movedSuppressed = MeasureHorizontal(player, forward);

            Assert.AreEqual(0f, movedSuppressed, 1e-6f,
                $"动作期间玩家**不得**产生水平位移(实测 {movedSuppressed:F4} m)。" +
                "若此处不为 0,先查 `PlayerController.Move` 里对 `IsSuppressed` 的读取 —— " +
                "**只把租约位置上是没用的,1 必须真的读它**(R-11.4 甲案)");

            // ── 相位 C:动作结束 ⇒ 恢复 ────────────────────────
            driver.EndAction(ReadPlayerActorId());
            Settle(player);
            float movedAfter = MeasureHorizontal(player, forward);

            Assert.Greater(movedAfter, 0.01f,
                $"动作结束后压制须撤销,玩家恢复可动(实测 {movedAfter:F4} m)" +
                "—— 否则「一次急救之后玩家永远动不了」");
        }

        // ══════════════ 辅助 ══════════════

        private IEnumerator Boot()
        {
            _root = new GameObject("BootRoot_MotorSuppression");
            _boot = _root.AddComponent<BootRoot>();

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), "启动序须在超时内完成");

            Time.captureDeltaTime = 1f / 60f;
            for (int i = 0; i < 3; i++)
                yield return null;
        }

        /// <summary>把上一相位残留的速度减到 0(零输入 ⇒ vTarget = 0)。</summary>
        private static void Settle(PlayerController player)
        {
            for (int i = 0; i < SettleMoves; i++)
                player.Move(Vector3.zero);
        }

        /// <summary>同一帧内连喂 40 次满幅输入,返回**水平**位移量(忽略重力造成的 y)。</summary>
        private static float MeasureHorizontal(PlayerController player, Vector3 dir)
        {
            Vector3 before = player.transform.position;
            for (int i = 0; i < MovesPerPhase; i++)
                player.Move(dir);
            Vector3 d = player.transform.position - before;
            return new Vector2(d.x, d.z).magnitude;
        }

        private static void DestroyAllNamed(string objectName)
        {
            var all = UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in all)
            {
                if (go != null && go.name == objectName)
                    UnityEngine.Object.Destroy(go);
            }
        }

        private bool ReadBooted()
        {
            var f = typeof(BootRoot).GetField("_booted",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null && f.FieldType == typeof(bool))
                return (bool)f.GetValue(_boot);
            return ReadServices() != null;
        }

        private object ReadServices()
        {
            if (_boot == null) return null;
            var field = typeof(BootRoot).GetField("_services",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(_boot);
        }

        private PlayerController ReadPlayer()
        {
            if (_boot == null) return null;
            var field = typeof(BootRoot).GetField("_player",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(_boot) as PlayerController;
        }

        private EmergencyChannelDriver ReadEmergencyChannel()
        {
            var services = ReadServices();
            if (services == null) return null;
            var prop = services.GetType().GetProperty("EmergencyChannel");
            return prop?.GetValue(services) as EmergencyChannelDriver;
        }

        private int ReadFirstPresentId()
        {
            var services = ReadServices();
            var presence = services?.GetType().GetProperty("Presence")?.GetValue(services);
            if (presence?.GetType().GetMethod("PresentPatientIds")?.Invoke(presence, null)
                is System.Collections.Generic.IReadOnlyCollection<int> ids)
            {
                foreach (int id in ids) return id;
            }
            return 0;
        }

        private int ReadPlayerActorId()
        {
            var services = ReadServices();
            var reg = services?.GetType().GetProperty("PlayerActors")?.GetValue(services);
            if (reg?.GetType().GetProperty("PlayerIds")?.GetValue(reg)
                is System.Collections.Generic.IReadOnlyCollection<int> ids)
            {
                foreach (int id in ids) return id;
            }
            return 0;
        }
    }
}
