// 技法解锁触发通路的**生产接线**验证(T9 · 2026-11-09)
//
// 判据(可证伪,四条):
//   ① 真启动序之后 `CompositionRootServices.TechniqueUnlockDriver` **非 null**
//      —— 缺失 = `BootRoot` 第 4.4b 步没跑 / 被跳过,「通路已接」这句话没有主语;
//   ② 驱动用的**写者**与 `TechniqueUnlockWriter` 是**同一个实例**
//      —— 否则解锁写进第二条流,静默双真源;
//   ③ 驱动读的**事件流**与 `CompositionRootServices.Stream` 是同一个实例
//      —— 否则扫的是另一条流,永远零命中(与 ADR-036 D5 同族失效模式);
//   ④ 驱动的 actor id = `BootRoot._playerActorId`
//      —— 否则解锁写给了一个不存在的学习者(流里有行、玩家学不到)。
//
// ⚠️ **本测不证明「规则会触发」** —— 生产规则表今日为空(`TechniqueUnlockRules.Default`),
//    触发语义由 EditMode 的 `technique_unlock_trigger_test` 证。两者分工必须分开读:
//    本件证**接线存在且指向正确对象**,彼件证**匹配语义正确**。
//
// 形态承 `patient_appeared_wiring_test`(真启动序 + 反射观测 + Addressables 世界卸载)。

using System;
using System.Collections;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>技法解锁触发驱动的生产接线验证(T9)。</summary>
    public class TechniqueUnlockWiringTest
    {
        private const float BootTimeoutSeconds = 15f;

        private GameObject _root;
        private BootRoot _boot;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var player = GameObject.Find("Player");
            if (player != null) UnityEngine.Object.Destroy(player);

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
            yield return null;
        }

        [UnityTest]
        public IEnumerator test_techniqueUnlockDriver_isWiredToProductionStreamAndWriter()
        {
            _root = new GameObject("BootRoot_TechniqueUnlockWiring");
            _boot = _root.AddComponent<BootRoot>();
            _boot.SaveRootOverride = PlayModeTempSaveRoot.New("technique_unlock_wiring_test");

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), "启动序须在超时内完成");

            // 再走一帧:tick 泵跑过至少一次,确保第 4.4b 步已执行完
            yield return null;

            object services = typeof(BootRoot)
                .GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(_boot);
            Assert.IsNotNull(services, "启动序后 _services 应非 null");

            // ── ① 驱动存在 ───────────────────────────────────────────────
            object driver = services.GetType()
                .GetProperty("TechniqueUnlockDriver")?.GetValue(services);
            Assert.IsNotNull(driver,
                "CompositionRootServices.TechniqueUnlockDriver 为 null ⇒ BootRoot 第 4.4b 步未执行,触发通路未接");

            // ── ② 写者同源 ───────────────────────────────────────────────
            object writer = services.GetType()
                .GetProperty("TechniqueUnlockWriter")?.GetValue(services);
            Assert.IsNotNull(writer, "TechniqueUnlockWriter 应非 null(ADR-043 的唯一写者)");

            object driverWriter = driver.GetType()
                .GetField("_writer", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(driver);
            Assert.AreSame(writer, driverWriter,
                "驱动手里的写者不是服务袋里那一个 ⇒ 解锁会写进另一条通路(双真源,静默)");

            // ── ③ 读流同源 ───────────────────────────────────────────────
            object stream = services.GetType().GetProperty("Stream")?.GetValue(services);
            Assert.IsNotNull(stream, "CompositionRootServices.Stream 应非 null");

            object driverStream = driver.GetType()
                .GetField("_stream", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(driver);
            Assert.AreSame(stream, driverStream,
                "驱动扫的不是生产事件流 ⇒ 永远零命中(与 ADR-036 D5 同族:读一个世界、写另一个世界)");

            // ── ④ 学习者 = 玩家 ──────────────────────────────────────────
            object playerActorId = typeof(BootRoot)
                .GetField("_playerActorId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(_boot);
            Assert.IsNotNull(playerActorId, "BootRoot._playerActorId 应存在(第 4 步铸造)");

            object driverActorId = driver.GetType()
                .GetField("_actorId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(driver);
            Assert.AreEqual((int)playerActorId, (int)driverActorId,
                "解锁须写给玩家本人;写给别的 id = 流里有行而玩家学不到");

            // ── 附:今日内容态(不是判据缺失,是内容未裁定)────────────────
            object ruleCount = driver.GetType().GetProperty("RuleCount")?.GetValue(driver);
            Assert.AreEqual(0, (int)ruleCount,
                "生产规则表今日为空 —— 若有人填了行,须同批更新 T9 证据件的「未做」表");
        }

        private bool ReadBooted()
        {
            if (_boot == null) return false;
            var field = typeof(BootRoot).GetField("_booted",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field != null && (bool)field.GetValue(_boot);
        }
    }
}
