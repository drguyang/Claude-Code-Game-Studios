// sprint-05 T1.2 · 病例开账驱动接线验证(2026-10-10)
//
// 🔁 **2026-11-02 判据翻转(开册入口轮 · 结清 `TD-casebook-01`)**:
// 原判据 = 「启动后病例流**含** `CaseOpened`」—— 它守的是当时那条
// `BootRoot → CaseOpenedDriver.OnTickEdge → 每 tick 自动开案` 的链。
// **那条链本身与设计相反**(`case-system.md:176`「立案是一次玩家动作」/ `:927`
// 「玩家没去 ⇒ 不立案」),已退役 ⇒ 原判据若保留,**它守的正是要删掉的行为**。
//
// 现判据 = 「**不按任何键**跑满 2s ⇒ 病例流**零** `CaseOpened`」。
// 这正是债册 `TD-casebook-01` 的「何时消解」第三条(玩家没交互 ⇒ 零 CaseOpened)。
// ⚠️ 正向那一半(按 E ⇒ 立案 + 开册)**不在本文件** —— 它需要虚拟键注入,
// 归 `Tests/PlayMode/Casebook/casebook_entry_wiring_test.cs`(②③④)。
// 两个文件合起来才是完整的入口判据:本件守**否定**,那件守**肯定**。
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
        public IEnumerator test_caseOpenedDriver_doesNotOpenCaseWithoutInteraction()
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

            // 🔁 2026-11-02 翻转:原为 `Assert.IsTrue(hasCaseOpened, ...)`,那守的是**已退役**的
            //   自动开案。现口径 = 全程零按键 ⇒ 零 CaseOpened(详见文件头注)。
            Assert.IsFalse(hasCaseOpened,
                "**未按任何键**跑满本窗口 ⇒ 病例流必须零 CaseOpened —— " +
                "出现即 = 自动开案还活着(`case-system.md:176`「立案是一次玩家动作」、" +
                "`:927`「玩家没去 ⇒ 不立案」)。正向链见 `Casebook/casebook_entry_wiring_test.cs`");

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
