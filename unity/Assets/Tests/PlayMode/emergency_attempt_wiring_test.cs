// sprint-05 T1.3 → **sprint-06 G4.1 改造**(2026-10-11)
//
// ⚠️ **红线(sprint-06 §2 G4.3 qa-lead 注)**:旧断言「启动即自动两事件」= **假绿** ——
//   自动触发(`EmergencyAttemptDriver._triggeredPatients`)已在 G4.1 移除。
//   本测**反向改造**:启动后跑若干 tick 边沿,**零输入 ⇒ 病史流零 EmergencyAttempt**。
//
// 判据(可证伪):
//   · 启动序完整跑通 + 在场病人 ≥1(触发条件仍在)⇒ **零急救事件**(自动分支已删);
//   · 手动入口(tick 序列内调 EmergencyManualDriver.TryManual)⇒ 双事件入流。
//   ⚠️ 若未来重新引入自动触发,本测即红(零输入零事件被守死)。
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
    /// <summary>急救手动化驱动验证(sprint-06 G4.1/G4.2 —— 旧自动触发测改造)。</summary>
    public class EmergencyAttemptWiringTest
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
        public IEnumerator test_emergencyManualDriver_zerolInput_window_noEmergency()
        {
            // Arrange:真启动序(与门④冒烟同源)
            _root = new GameObject("BootRoot_EmergencyWiring");
            _boot = _root.AddComponent<BootRoot>();

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), "启动序须在超时内完成");

            // 强制编辑器帧节奏(与 landing 探针同纪律)
            Time.captureDeltaTime = 1f / 60f;
            _captureActive = true;

            // Act:跑 2s,**零急救意图输入**
            long tickBefore = ReadCurrentTick();
            for (int i = 0; i < SampleFrames; i++)
                yield return null;
            long tickAfter = ReadCurrentTick();
            Debug.Log($"[EmergencyWiring] capture 窗:tick {tickBefore} → {tickAfter}(Δ={tickAfter - tickBefore})");

            var services = ReadServices();
            Assert.IsNotNull(services, "启动序后 _services 应非 null");
            var events = ReadEvents(services);
            Assert.IsNotNull(events, "EventStream._events 应非 null");

            bool hasAttempt = HasKind(events, "EmergencyAttempt");
            bool hasApplied = HasKind(events, "EmergencyTreatmentApplied");

            // Assert 1(G4.1 判据):零输入 ⇒ 病史流**零** EmergencyAttempt / EmergencyTreatmentApplied
            Assert.IsFalse(hasAttempt,
                "零输入观察窗 ⇒ 病史流零 EmergencyAttempt —— 自动触发已移除(G4.1 红线)");
            Assert.IsFalse(hasApplied,
                "零输入 ⇒ 零处置事件(双事件都不发)");

            // Assert 2:在场视图非空(触发条件「病人在场」仍在 —— 零事件不是因无病人)
            var presenceProp = services.GetType().GetProperty("Presence");
            Assert.IsNotNull(presenceProp, "CompositionRootServices.Presence 应存在");
            var presence = presenceProp.GetValue(services);
            Assert.IsNotNull(presence, "Presence 应非 null");
            var countProp = presence.GetType().GetProperty("PresentCount");
            Assert.IsNotNull(countProp, "Presence 应有 PresentCount 属性");
            int presentCount = (int)countProp.GetValue(presence);
            Debug.Log($"[EmergencyWiring] 在场病人数 = {presentCount}");

            Assert.That(presentCount, Is.GreaterThanOrEqualTo(1),
                $"启动后应有 ≥1 个病人登记在场(实际 {presentCount})—— " +
                "零急救事件不是因无病人,而是因自动触发已移除");
        }

        [UnityTest]
        public IEnumerator test_emergencyManualDriver_manualEntry_writesBothEvents()
        {
            // Arrange:真启动序
            _root = new GameObject("BootRoot_EmergencyWiring_Manual");
            _boot = _root.AddComponent<BootRoot>();

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), "启动序须在超时内完成");

            Time.captureDeltaTime = 1f / 60f;
            _captureActive = true;

            // 等一个病人在场(触发前置)
            var services = ReadServices();
            for (int i = 0; i < SampleFrames && !AnyPresent(services); i++)
                yield return null;

            // Act:手动急救一次(真实生产装配路径)
            var manualProp = services.GetType().GetProperty("EmergencyManualDriver");
            Assert.IsNotNull(manualProp, "服务袋须暴露 EmergencyManualDriver");
            var manual = manualProp.GetValue(services);
            Assert.IsNotNull(manual, "EmergencyManualDriver 应非 null");

            int patientId = FirstPresentId(services);
            var patient = new PatientId(patientId);
            var tryManual = manual.GetType().GetMethod("TryManual");
            tryManual.Invoke(manual, new object[] { patient, 0, 9001, ReadCurrentTick() });

            var events = ReadEvents(services);
            bool hasAttempt = HasKind(events, "EmergencyAttempt");
            bool hasApplied = HasKind(events, "EmergencyTreatmentApplied");

            Assert.IsTrue(hasAttempt,
                "手动急救 ⇒ 病史流含 EmergencyAttempt(G4.2 判据)");
            Assert.IsTrue(hasApplied,
                "手动急救 ⇒ 病史流含 EmergencyTreatmentApplied(双事件)");
        }

        // ── 反射辅助 ───────────────────────────────────────────────

        private object ReadServices()
        {
            if (_boot == null) return null;
            var field = typeof(BootRoot).GetField("_services",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(_boot);
        }

        private static System.Collections.IList ReadEvents(object services)
        {
            var streamProp = services.GetType().GetProperty("Stream");
            if (streamProp == null) return null;
            var stream = streamProp.GetValue(services);
            if (stream == null) return null;
            var eventsField = stream.GetType().GetField("_events",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return eventsField?.GetValue(stream) as System.Collections.IList;
        }

        private static bool HasKind(System.Collections.IList events, string kind)
        {
            if (events == null) return false;
            foreach (var evt in events)
            {
                var kindField = evt.GetType().GetField("Kind");
                if (kindField?.GetValue(evt)?.ToString() == kind)
                    return true;
            }
            return false;
        }

        private static bool AnyPresent(object services)
        {
            var presenceProp = services.GetType().GetProperty("Presence");
            if (presenceProp == null) return false;
            var presence = presenceProp.GetValue(services);
            if (presence == null) return false;
            var countProp = presence.GetType().GetProperty("PresentCount");
            if (countProp == null) return false;
            return (int)countProp.GetValue(presence) >= 1;
        }

        private static int FirstPresentId(object services)
        {
            var presenceProp = services.GetType().GetProperty("Presence");
            var presence = presenceProp.GetValue(services);
            // PresentPatientIds 是**方法**(IPresenceQuery 接口成员),不是属性 —— 反射须 GetMethod
            var idsMethod = presence.GetType().GetMethod("PresentPatientIds");
            var ids = (System.Collections.IEnumerable)idsMethod.Invoke(presence, null);
            foreach (var id in ids) return (int)id;
            return -1;
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
