// 剧情阶段门的**正向**判据(2026-11-14)。
//
// ═══ 它补的是什么 ════════════════════════════════════════════════════════
//   2026-11-12 用户裁定「甲」给环境产病人加了一道门:
//   **世界骨架里任一 `Tent` 槽被占用之前,不产病人**
//   (`CompositionRoot.cs:572-575` 组装 → `:579` 与 `:848-850` 两处消费)。
//
//   该门此前**没有任何正向覆盖** —— 它只以「13 条下游用例集体转红」的形态出现过一次
//   (2026-11-12 P1-b2 换绑 canonical 之后)。那 13 条现在改成了「先建帐篷再断言」
//   (`PatientGateTestSupport.EnsureTentBuilt`),**门本身就没有人证明了**。
//   本件补的正是这一块:没有帐篷 ⇒ 一个病人都没有。
//
// ═══ 为什么它不是空转 ═════════════════════════════════════════════════════
//   `PatientAppearedDriver.cs:147-149` 的 `firstTick` 分支:**第一个 tick 就产病人**
//   (不等 `SpawnIntervalTicks`)。⇒ 门若失效,第 1 个 tick 内就会出现 `DiseaseOnset`,
//   下面 `test_noTent_noPatientSpawned` 当场转红。判据有判别力,不是「等不到所以绿」。
//
//   `test_tentBuilt_patientAppears` 反向锁住另一半:帐篷建成之后**必须**能产病人 ——
//   否则「门永远关着」也会让上一条绿。两条合起来才是门的完整判据。

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
    public class patient_gate_test
    {
        private const float BootTimeoutSeconds = 15f;

        /// <summary>观测窗帧数。`firstTick` 在首个 tick 即产病人 ⇒ 80 帧足够覆盖多轮 tick。</summary>
        private const int SampleFrames = 80;

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

        /// <summary>
        /// **门关着**:不建帐篷 ⇒ 在场病人恒 0(病史流里没有 `DiseaseOnset`)。
        /// </summary>
        [UnityTest]
        public IEnumerator test_noTent_noPatientSpawned()
        {
            yield return Boot();

            yield return RunFrames(SampleFrames);

            Assert.AreEqual(0, CountDiseaseOnset(),
                "帐篷未建成时不得产病人 —— 门失效(或 canonical 关卡丢了 Tent 槽 ⇒ 门退化为 null)");
        }

        /// <summary>
        /// **门开得动**:经生产写者落 `StructurePlaced` 占上 Tent 槽 ⇒ 病人开始出现。
        /// 反向锁住「门永远关着」这种假绿。
        /// </summary>
        [UnityTest]
        public IEnumerator test_tentBuilt_patientAppears()
        {
            yield return Boot();

            yield return RunFrames(SampleFrames);
            Assert.AreEqual(0, CountDiseaseOnset(),
                "[前置] 建帐篷之前不应有病人 —— 本断言失败说明门没关住,后一条也就失去意义");

            PatientGateTestSupport.EnsureTentBuilt(_boot);

            yield return RunFrames(SampleFrames);
            Assert.GreaterOrEqual(CountDiseaseOnset(), 1,
                "帐篷建成后应产病人 —— `firstTick` 分支在首个 tick 即 spawn,80 帧足够");
        }

        private IEnumerator Boot()
        {
            _root = new GameObject("BootRoot_PatientGate");
            _boot = _root.AddComponent<BootRoot>();
            _boot.SaveRootOverride = PlayModeTempSaveRoot.New("patient_gate_test");

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), "启动序须在超时内完成");
        }

        private static IEnumerator RunFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
                yield return null;
        }

        /// <summary>病史流里 `DiseaseOnset` 的条数(反射读,不新增生产读面)。</summary>
        private int CountDiseaseOnset()
        {
            object services = typeof(BootRoot)
                .GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_boot);
            Assert.IsNotNull(services, "启动序后 `_services` 应非 null");

            object stream = services.GetType().GetProperty("Stream").GetValue(services);
            Assert.IsNotNull(stream, "`CompositionRootServices.Stream` 应非 null");

            var events = (IList)stream.GetType()
                .GetField("_events", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(stream);
            Assert.IsNotNull(events, "`EventStream._events` 应非 null");

            int count = 0;
            foreach (object evt in events)
            {
                object kind = evt.GetType().GetField("Kind")?.GetValue(evt);
                if (kind != null && kind.ToString() == "DiseaseOnset") count++;
            }
            return count;
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
