// sprint-05 T2.0 · 病人最小可见实体验证(2026-10-10)
//
// 判据(可证伪):启动后 Hierarchy 存在 ≥1 个病人视觉实体(PatientVisual_*)。
// 病因(playtest 2026-10-10 §6):病人在场但零视觉实体 ⇒ 人工面 0% 覆盖核心循环入口,
// 第二轮 playtest 无法覆盖「病人出现」。本测守接线闭合:BootRoot 启动序 →
// PatientAppearedDriver(spawn + AddPresent)→ PatientVisualSpawner(按在场集 spawn 视觉)。
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
    /// <summary>病人最小可见实体验证(sprint-05 T2.0)。</summary>
    public class PatientVisualWiringTest
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

            // 视觉实体由 PatientVisualSpawner 自持根节点(PatientVisuals)——
            // BootRoot.OnDestroy 会调 DestroyAll 连根清;此处只兜底(防 OnDestroy 时序差异)
            var visualRoot = GameObject.Find("PatientVisuals");
            if (visualRoot != null) UnityEngine.Object.Destroy(visualRoot);
            foreach (var t in GameObject.FindObjectsOfType<Transform>())
            {
                if (t != null && t.name != null && t.name.StartsWith("PatientVisual_"))
                    UnityEngine.Object.Destroy(t.gameObject);
            }

            var player = GameObject.Find("Player");
            if (player != null) UnityEngine.Object.Destroy(player);

            var cameraRig = GameObject.Find("CameraRig");
            if (cameraRig != null) UnityEngine.Object.Destroy(cameraRig);

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
        public IEnumerator test_patientVisualSpawner_createsVisibleEntityAtBoot()
        {
            // Arrange:真启动序(与门④冒烟同源)
            _root = new GameObject("BootRoot_PatientVisual");
            _boot = _root.AddComponent<BootRoot>();

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), "启动序须在超时内完成");

            // 强制编辑器帧节奏(与 landing 探针同纪律)
            Time.captureDeltaTime = 1f / 60f;
            _captureActive = true;

            // Act:跑 2s(首 spawn 在启动序末即发生)
            long tickBefore = ReadCurrentTick();
            for (int i = 0; i < SampleFrames; i++)
                yield return null;
            long tickAfter = ReadCurrentTick();
            Debug.Log($"[PatientVisual] capture 窗:tick {tickBefore} → {tickAfter}(Δ={tickAfter - tickBefore})");

            // Assert 1:服务袋持有 PatientVisualSpawner(接线闭合)
            var servicesField = typeof(BootRoot).GetField("_services",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(servicesField, "BootRoot._services 应存在");
            var services = servicesField.GetValue(_boot);
            Assert.IsNotNull(services, "启动序后 _services 应非 null");

            var visualProp = services.GetType().GetProperty("PatientVisualSpawner");
            Assert.IsNotNull(visualProp, "CompositionRootServices.PatientVisualSpawner 应存在");
            var visualSpawner = visualProp.GetValue(services);
            Assert.IsNotNull(visualSpawner, "PatientVisualSpawner 应非 null");

            // Assert 2:Hierarchy 存在 ≥1 个病人视觉实体
            int visualCount = CountPatientVisualEntities();
            Debug.Log($"[PatientVisual] Hierarchy 病人视觉实体数 = {visualCount}");

            Assert.That(visualCount, Is.GreaterThanOrEqualTo(1),
                $"启动后应有 ≥1 个病人视觉实体(实际 {visualCount})—— " +
                "0 = 病人无可见实体,人工面仍看不到病人(playtest §6 核心循环 NOT-RUN 未解)");

            // Assert 3:视觉实体与在场病人一一对应(数量一致 = 无孤儿 / 无遗漏)
            var presenceProp = services.GetType().GetProperty("Presence");
            Assert.IsNotNull(presenceProp, "CompositionRootServices.Presence 应存在");
            var presence = presenceProp.GetValue(services);
            Assert.IsNotNull(presence, "Presence 应非 null");

            var countProp = presence.GetType().GetProperty("PresentCount");
            Assert.IsNotNull(countProp, "Presence 应有 PresentCount 属性");
            int presentCount = (int)countProp.GetValue(presence);
            Debug.Log($"[PatientVisual] 在场病人数 = {presentCount},视觉实体数 = {visualCount}");

            Assert.AreEqual(presentCount, visualCount,
                "视觉实体数应与在场病人数一致 —— 多 = 孤儿实体(离场未清);少 = 有病人在场无脸");
        }

        /// <summary>统计 Hierarchy 中命名的病人视觉实体。</summary>
        private static int CountPatientVisualEntities()
        {
            int n = 0;
            var all = GameObject.FindObjectsOfType<Transform>();
            foreach (var t in all)
            {
                if (t != null && t.name != null && t.name.StartsWith("PatientVisual_"))
                    n++;
            }
            return n;
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
