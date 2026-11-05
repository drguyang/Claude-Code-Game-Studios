// sprint-05 T1.5 · 相机跟随验证(2026-10-10)
//
// 判据(可证伪):玩家位移时相机跟随;PlayMode 断言相机位置 ≠ 初始。
// ADR-020:相机 = 自建机位,不引入 Cinemachine;跟随 = 每帧把相机位置设到玩家位置 + 偏移。
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
    /// <summary>相机跟随验证(sprint-05 T1.5)。</summary>
    public class CameraFollowWiringTest
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

            // ⚠️ **拆净同名预摆,不用 `GameObject.Find`**(2026-11-01 全量串跑**偶发红**根治):
            //   `GameObject.Find` **只查 active 对象**,而本件的预摆已被启动序 `SetActive(false)`
            //   ⇒ 旧写法(`Find` + `SetActive(true)`)会**故意把禁用的预摆重新激活并留在场上**。
            //   后果:后续测试(`death_transition_playmode_test`)自造同名 `Main Camera` 时,
            //   `BootRoot.FindInactiveInclusive` 按 `FindObjectsByType(..., SortMode.None)` 的
            //   **枚举序**取第一个 —— 残留旧件抢先被返回 ⇒ BootRoot 摘掉**错的那一个**的 listener
            //   ⇒ 本次预摆 listener 仍在 ⇒ `AC-ADR020-10` 抛。枚举序不保证稳定 ⇒ **偶发**。
            //   ⇒ 本件与 `audio_wiring_test` 同纪律:连 inactive 一起收、**全部销毁**
            //   (不是「恢复激活」——预摆是夹具,用完即弃,没有留到下一条的理由)。
            DestroyAllNamed("Main Camera");
            DestroyAllNamed("Player");
            DestroyAllNamed("CameraRig");

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
        public IEnumerator test_cameraFollow_followsPlayerMovement()
        {
            // Arrange:预摆 Main Camera(承 Boot.unity 工厂形态 —— ADR-023 §①)
            // ⚠️ 必须先造预摆:启动序第 5 步会禁用它并断言「恰好 1 个 AudioListener」。
            //    不补这个 fixture ⇒ listener = 0 ⇒ 断言按设计抛(fail-loud 是对的,
            //    是测试环境缺了生产场景本有的东西)。
            var presetCamObj = new GameObject("Main Camera");
            presetCamObj.AddComponent<Camera>();
            presetCamObj.AddComponent<AudioListener>();

            _root = new GameObject("BootRoot_CameraFollow");
            _boot = _root.AddComponent<BootRoot>();
            _boot.SaveRootOverride = PlayModeTempSaveRoot.New("camera_follow_wiring_test");

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(),
                "启动序须在超时内完成(未完成 = AudioListener 单挂点断言可能已抛 —— " +
                "Boot.unity 预摆 Main Camera 持唯一 listener,CameraRig 不得再加)");

            // 预摆相机应被禁用(不渲染,只持 listener 语义)
            Assert.IsFalse(presetCamObj.activeSelf,
                "预摆 Main Camera 应被启动序禁用 —— 不禁用 = 两个 Camera 都在渲染(浪费 + 双 listener 面)");

            // 强制编辑器帧节奏(与 landing 探针同纪律)
            Time.captureDeltaTime = 1f / 60f;
            _captureActive = true;

            // Act:记录初始相机位置
            var cameraRig = GameObject.Find("CameraRig");
            Assert.IsNotNull(cameraRig, "CameraRig 应存在");
            var camera = cameraRig.GetComponent<Camera>();
            Assert.IsNotNull(camera, "Camera 组件应存在");
            Vector3 initialCamPos = camera.transform.position;
            Debug.Log($"[CameraFollow] 初始相机位置 = {initialCamPos}");

            // 把玩家搬到界内远处(3,1,3)—— **不可用界外坐标**:出界会触发 T1.4 的
            // 坠落重置,玩家被送回出生点,相机跟着回窝 ⇒ 位移看似不足却是跟随正常的假阴性
            // (2026-10-10 实测:送(10,1,10)得位移 0.08,根因 = 出界坠落复位,非相机故障)。
            // 每帧钉住位置,排除「着陆后位移被 CC.Move 吃掉」的抖动。
            var player = GameObject.Find("Player");
            Assert.IsNotNull(player, "玩家应存在");
            Vector3 targetPos = new Vector3(3f, 1f, 3f);

            // 跑 2s(给相机跟随留时间);期间每帧把玩家钉在目标位
            long tickBefore = ReadCurrentTick();
            for (int i = 0; i < SampleFrames; i++)
            {
                player.transform.position = targetPos;
                yield return null;
            }
            player.transform.position = targetPos;
            yield return null;
            long tickAfter = ReadCurrentTick();
            Debug.Log($"[CameraFollow] capture 窗:tick {tickBefore} → {tickAfter}(Δ={tickAfter - tickBefore})");
            Debug.Log($"[CameraFollow] 玩家位置 = {player.transform.position}");

            // Assert:相机位置 ≈ 玩家位置 + 偏移(跟随生效)
            Vector3 finalCamPos = camera.transform.position;
            Debug.Log($"[CameraFollow] 最终相机位置 = {finalCamPos}");

            // 相机应指向玩家所在格(≈ targetPos + 越肩偏移),而非停在原地(初始偏移中心在原点)
            float distFromTarget = Vector3.Distance(
                new Vector3(finalCamPos.x, 0f, finalCamPos.z),
                new Vector3(targetPos.x, 0f, targetPos.z));
            Debug.Log($"[CameraFollow] 相机水平面到玩家距离 = {distFromTarget}(offset 水平分量 = {distFromTarget:F2})");

            Assert.That(distFromTarget, Is.GreaterThan(1.0f),
                $"相机水平投影应随玩家移到 ({targetPos.x}, {targetPos.z}) 附近(距离变化 > 1.0),实际 = {distFromTarget} —— " +
                "不足 = 相机未跟随(或玩家被坠落复位送回出生点,playtest bug #2 未修)");
        }

        /// <summary>
        /// 销毁场景内所有同名的 GameObject(含 **inactive** —— 见 <see cref="TearDown"/> 的说明)。
        /// <para>⚠️ 与 <c>audio_wiring_test</c> 同款实现:**不能用 `GameObject.Find`**
        /// (它只查 active 对象,而被启动序 `SetActive(false)` 的预摆恰是 inactive)。
        /// `Resources.FindObjectsOfTypeAll` 连未激活的一起收;`go.scene.IsValid()` 排除
        /// 资产(非场景对象 —— 销毁它们会破坏工程资源)。</para>
        /// </summary>
        private static void DestroyAllNamed(string name)
        {
            GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i];
                if (go != null && go.name == name && go.scene.IsValid())
                    UnityEngine.Object.Destroy(go);
            }
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
