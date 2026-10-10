// sprint-05 T1.2 · 病例开账驱动接线验证(2026-10-10)
//
// 判据(可证伪):启动后病例流含 CaseOpened 事件;开账条件(在场病人)满足。
// 原状(F-6 复评实测):CaseOpenWriter 零生产调用方 ⇒ 运行期链可达性 0%。
// 本测验证接线闭合:BootRoot 启动序 → CaseOpenedDriver → TryOpen → CaseOpened 入流。
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
    /// <summary>病例开账驱动接线验证(sprint-05 T1.2)。</summary>
    public class CaseOpenedWiringTest
    {
        private const float BootTimeoutSeconds = 15f;
        /// <summary>取证帧数(capture 1/60 下 = 2s;首 spawn 在启动序末即发生)。</summary>
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
        public IEnumerator test_caseOpenedDriver_opensCaseAtBoot()
        {
            // Arrange:真启动序(与门④冒烟同源)
            _root = new GameObject("BootRoot_CaseOpenedWiring");
            _boot = _root.AddComponent<BootRoot>();

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), "启动序须在超时内完成");

            // 强制编辑器帧节奏(与 landing 探针同纪律)
            Time.captureDeltaTime = 1f / 60f;
            _captureActive = true;

            // Act:跑 2s(首 spawn 在启动序末即发生,这里给开案驱动留时间)
            long tickBefore = ReadCurrentTick();
            for (int i = 0; i < SampleFrames; i++)
                yield return null;
            long tickAfter = ReadCurrentTick();
            Debug.Log($"[CaseOpenedWiring] capture 窗:tick {tickBefore} → {tickAfter}(Δ={tickAfter - tickBefore})");

            // Assert 1:病史流有 CaseOpened 事件(写者存在性的运行期证据)
            var servicesField = typeof(BootRoot).GetField("_services",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(servicesField, "BootRoot._services 应存在");
            var services = servicesField.GetValue(_boot);
            Assert.IsNotNull(services, "启动序后 _services 应非 null");

            var streamProp = services.GetType().GetProperty("Stream");
            Assert.IsNotNull(streamProp, "CompositionRootServices.Stream 应存在");
            var stream = streamProp.GetValue(services);
            Assert.IsNotNull(stream, "Stream 应非 null");

            var eventsField = stream.GetType().GetField("_events",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(eventsField, "EventStream._events 应存在");
            var events = eventsField.GetValue(stream) as System.Collections.IList;
            Assert.IsNotNull(events, "EventStream._events 应非 null");

            bool hasCaseOpened = false;
            foreach (var evt in events)
            {
                var kindField = evt.GetType().GetField("Kind");
                var patientField = evt.GetType().GetField("Patient");
                var tickField = evt.GetType().GetField("Tick");
                var seqField = evt.GetType().GetField("Seq");
                string kind = kindField?.GetValue(evt)?.ToString() ?? "?";
                string patient = patientField?.GetValue(evt)?.ToString() ?? "?";
                string tick = tickField?.GetValue(evt)?.ToString() ?? "?";
                string seq = seqField?.GetValue(evt)?.ToString() ?? "?";
                Debug.Log($"[CaseOpenedWiring] EVENT: Kind={kind} Patient={patient} Tick={tick} Seq={seq}");
                if (kind == "CaseOpened")
                {
                    hasCaseOpened = true;
                }
            }
            Debug.Log($"[CaseOpenedWiring] 病史流事件数 = {events.Count},含 CaseOpened = {hasCaseOpened}");

            Assert.IsTrue(hasCaseOpened,
                "病史流应含 CaseOpened 事件 —— 缺失 = 写者未运行期触发(接线未闭合)");

            // Assert 2:在场视图非空(开账前置条件 ①「病人存在」的运行期证据)
            var presenceProp = services.GetType().GetProperty("Presence");
            Assert.IsNotNull(presenceProp, "CompositionRootServices.Presence 应存在");
            var presence = presenceProp.GetValue(services);
            Assert.IsNotNull(presence, "Presence 应非 null");

            var countProp = presence.GetType().GetProperty("PresentCount");
            Assert.IsNotNull(countProp, "Presence 应有 PresentCount 属性");
            int presentCount = (int)countProp.GetValue(presence);
            Debug.Log($"[CaseOpenedWiring] 在场病人数 = {presentCount}");

            Assert.That(presentCount, Is.GreaterThanOrEqualTo(1),
                $"启动后应有 ≥1 个病人登记在场(实际 {presentCount})—— " +
                "0 = 开账前置条件 ① 不满足,CaseOpened 不应入流");
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
