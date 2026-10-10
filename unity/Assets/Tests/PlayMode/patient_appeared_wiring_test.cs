// sprint-05 T1.1 · 病人出现驱动接线验证(2026-10-10)
//
// 判据(可证伪):启动后 ≥1 个病人出现于在场视图;IPresentPatients 非空。
// 原状(F-6 复评实测):spawner 零生产调用方 ⇒ 运行期链可达性 0%。
// 本测验证接线闭合:BootRoot 启动序 → PatientAppearedDriver → SpawnNext → DiseaseOnset 入流。
//
// 形态承 composition_root_smoke_test(BootRoot 反射观测 + Addressables world 卸载)。
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
    /// <summary>病人出现驱动接线验证(sprint-05 T1.1)。</summary>
    public class PatientAppearedWiringTest
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
        public IEnumerator test_patientAppearedDriver_spawnsPatientAtBoot()
        {
            // Arrange:真启动序(与门④冒烟同源)
            _root = new GameObject("BootRoot_PatientWiring");
            _boot = _root.AddComponent<BootRoot>();

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), "启动序须在超时内完成");

            // 强制编辑器帧节奏(与 landing 探针同纪律)
            Time.captureDeltaTime = 1f / 60f;
            _captureActive = true;

            // Act:跑 2s(首 spawn 在启动序末即发生,这里给体征链 Step 留时间)
            // ⚠️ captureDeltaTime 对 unscaledDeltaTime 的传导**不确定**(引擎内部语义)——
            //   若 120 帧后 tick 仍未推进,改为用真实 WaitForSeconds 并把断言建立在
            //   「tick 推进了」之上。先观测 tick 数。
            long tickBefore = ReadCurrentTick();
            for (int i = 0; i < SampleFrames; i++)
                yield return null;
            long tickAfter = ReadCurrentTick();
            Debug.Log($"[PatientWiring] capture 窗:tick {tickBefore} → {tickAfter}(Δ={tickAfter - tickBefore})");

            // Assert 1:病史流有 DiseaseOnset 事件(写者存在性的运行期证据)
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

            bool hasOnset = false;
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
                Debug.Log($"[PatientWiring] EVENT: Kind={kind} Patient={patient} Tick={tick} Seq={seq}");
                if (kind == "DiseaseOnset")
                {
                    hasOnset = true;
                }
            }
            Debug.Log($"[PatientWiring] 病史流事件数 = {events.Count},含 DiseaseOnset = {hasOnset}");

            Assert.IsTrue(hasOnset,
                "病史流应含 DiseaseOnset 事件 —— 缺失 = 写者未运行期触发(接线未闭合)");

            // Assert 1b:首 tick 即 spawn(B 案 · 2026-10-10)
            // 用户 playtest 报告「随 WASD 出现很多个胶囊 + xyz 钉住」⇒ 诊断:编辑器里 tick
            // 推进极慢(300 帧仅 +2 tick),原驱动「每 400 tick 一个」使病人可见性被 tick
            // 速率绑架;观察窗口一拉长 tick 累积过 400 就又 spawn。B 案 = 首 tick 即 spawn
            // + 接线期上限哨 3(不碰 TICK_SECONDS 裁定值)。
            var driverProp = services.GetType().GetProperty("PatientAppearedDriver");
            Assert.IsNotNull(driverProp, "CompositionRootServices.PatientAppearedDriver 应存在");
            var driver = driverProp.GetValue(services);
            Assert.IsNotNull(driver, "PatientAppearedDriver 应非 null");
            var spawnedProp = driver.GetType().GetProperty("SpawnedCount");
            Assert.IsNotNull(spawnedProp, "PatientAppearedDriver.SpawnedCount 应存在(测试接缝)");
            int spawnedCount = (int)spawnedProp.GetValue(driver);
            Debug.Log($"[PatientWiring] 接线期已 spawn 病人数 = {spawnedCount}");
            Assert.That(spawnedCount, Is.GreaterThanOrEqualTo(1),
                $"首 tick 应即 spawn(实际 {spawnedCount})—— B 案接线判据;0 = 首 tick spawn 未生效");
            Assert.That(spawnedCount, Is.LessThanOrEqualTo(3),
                $"接线期上限哨 3(实际 {spawnedCount})—— 超出 = B 案上限哨失效(playtest 刷屏复发)");

            // Assert 2:在场视图非空(AC-15 在场检查的运行期证据)
            // ⚠️ 依赖 T1.1 自己的驱动已做 AddPresent(2026-10-10 修后);
            // T1.2(37 立案时登记)是另一条路径,本测只验 T1.1。
            var presenceProp = services.GetType().GetProperty("Presence");
            Assert.IsNotNull(presenceProp, "CompositionRootServices.Presence 应存在");
            var presence = presenceProp.GetValue(services);
            Assert.IsNotNull(presence, "Presence 应非 null");

            var countProp = presence.GetType().GetProperty("PresentCount");
            Assert.IsNotNull(countProp, "Presence 应有 PresentCount 属性");
            int presentCount = (int)countProp.GetValue(presence);
            Debug.Log($"[PatientWiring] 在场病人数 = {presentCount}");

            Assert.That(presentCount, Is.GreaterThanOrEqualTo(1),
                $"启动后应有 ≥1 个病人登记在场(实际 {presentCount})—— " +
                "0 = AddPresent 未在 spawn 后调用(AC-15 会拒收未登记病人)");
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
