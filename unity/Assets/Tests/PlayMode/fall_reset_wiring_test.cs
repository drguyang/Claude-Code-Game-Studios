// sprint-05 T1.4 · 坠落重置验证(2026-10-10)
//
// 判据(可证伪):玩家出界后 y 回到 ≥0(坠落重置生效)。
// playtest bug #1:出界无底会被每个玩家读成 bug;最低成本修复 = 坠落重置。
//
// 形态承 patient_appeared_wiring_test(BootRoot 反射观测 + Addressables world 卸载)。
// ⚠️ captureDeltaTime 全局态,[UnityTearDown] 恢复 0,防污染同套件其他测试。

using System;
using System.Collections;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
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
    /// <summary>坠落重置验证(sprint-05 T1.4)。</summary>
    public class FallResetWiringTest
    {
        private const float BootTimeoutSeconds = 15f;
        /// <summary>取证帧数(capture 1/60 下 = 2s)。</summary>
        private const int SampleFrames = 120;

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
        public IEnumerator test_fallReset_resetsPlayerWhenOutOfWorld()
        {
            // Arrange:真启动序(与门④冒烟同源)
            _root = new GameObject("BootRoot_FallReset");
            _boot = _root.AddComponent<BootRoot>();

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), "启动序须在超时内完成");

            // 强制编辑器帧节奏(与 landing 探针同纪律)
            Time.captureDeltaTime = 1f / 60f;
            _captureActive = true;

            // Act:传送玩家到世界边界外(模拟出界)
            var player = GameObject.Find("Player");
            Assert.IsNotNull(player, "玩家应存在");

            // 传送到远处(超出 10×10m Plane 边界)
            player.transform.position = new Vector3(100f, 1f, 100f);

            // 跑 2s(给坠落重置留时间)
            long tickBefore = ReadCurrentTick();
            for (int i = 0; i < SampleFrames; i++)
                yield return null;
            long tickAfter = ReadCurrentTick();
            Debug.Log($"[FallReset] capture 窗:tick {tickBefore} → {tickAfter}(Δ={tickAfter - tickBefore})");

            // Assert:玩家 y 位置 ≥ 0(坠落重置生效)
            float y = player.transform.position.y;
            Debug.Log($"[FallReset] 玩家 y = {y}");

            Assert.That(y, Is.GreaterThanOrEqualTo(0f),
                $"玩家出界后 y 应 ≥ 0(坠落重置生效),实际 y = {y} —— " +
                "负值 = 坠落重置未触发(playtest bug #1 未修复)");
        }

        private long ReadCurrentTick()
        {
            if (_boot == null) return -1;
            var field = typeof(BootRoot).GetField("_tickDriver",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var driver = field?.GetValue(_boot);
            if (driver == null) return -1;
            var prop = driver.GetType().GetProperty("CurrentTick");
            return (long)prop.GetValue(driver);
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
