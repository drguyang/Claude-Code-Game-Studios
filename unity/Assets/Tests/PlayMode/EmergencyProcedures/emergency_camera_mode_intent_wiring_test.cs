// story-010 —— 档位意图的**真机层**(L2)夹具:`CameraRig` 到底认不认这些请求。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里
// ─────────────────────────────────────────────────────────────────────────────
// 读:**零仓库文件**(全部经生产启动序 + 反射读 `BootRoot` 的私有字段;启动序自己读
//   Addressables 的 World 场景,那是生产路径,不是本夹具挑的表)。
// 写:**零写盘**。
//
// ── 为什么必须有这一层 ────────────────────────────────────────────────────────
// L1(`Tests/EditMode/EmergencyProcedures/emergency_camera_mode_intent_test.cs`)只证
// 「请求发了」,**不证「相机认了」**:档位是意图制,请求可能被更高优先级压掉,也可能入队后
// 根本不结算。L2 断的是 `AC-10-11` 的目的从句 —— **「相机不卡在近景」**。
//
// ── 这一层在 2026-11-02 抓出的既有缺陷 ──────────────────────────────────────
// `CameraRig.Tick` 此前**零生产调用点** ⇒ `CameraModeMachine.Settle` 从不执行 ⇒ 档位
// 永远锁死在 `Explore`(29 的 `Downed` / 10 的 `Treatment` 全部只是入队)。
// 用户裁定「本卡顺带接 Tick」⇒ `BootRoot.Update` 增一行(带日期注)。
// ⇒ 本文件同时是那一行的**唯一**回归守卫:删掉那行,本文件的 `Mode` 断言全部转红。
//
// ── 跨 epic 影响(如实登记,不得当噪声)─────────────────────────────────────
// 接上 `Tick` 会**同时**激活:① 29 的两段式倒下相机(`Downed` + 下沉/微倾);
// ② 39 将来的 `Casebook` 档转场。二者是**修复**(spec 早就要求),但行为变化须在
// `death-respawn` 与 `camera-viewpoint` 侧登记(coding-standards §工作树卫生 三分类的 ①)。
//
// 权威来源:
//   `design/gdd/emergency-procedures.md` 规则八 · `AC-10-11`
//   `production/epics/emergency-procedures/story-010-camera-mode-intent-wiring.md` R-10.6 L2
//   `design/gdd/camera-and-viewpoint.md` `R-2-5`(档位优先级)

using System;
using System.Collections;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Camera;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode.EmergencyProcedures
{
    /// <summary>
    /// story-010 L2:真 `CameraRig` 认不认 10 的档位意图(`Treatment` → `Explore`)。
    /// </summary>
    public class EmergencyCameraModeIntentWiringTest
    {
        private const float BootTimeoutSeconds = 20f;
        private const int MaxWaitFrames = 240;   // 1/60 capture ⇒ 最多 4 s
        private const int ActionOrdinal = 0;

        private GameObject _root;
        private BootRoot _boot;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = 0f;

            // ⚠️ 必须含 inactive:`GameObject.Find` 只查 active,而启动序 `SetActive(false)`
            //    的预摆恰是 inactive ⇒ 清场会静默失效(机械门 `pm-teardown-find` 判红)。
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

            // 预摆 Main Camera 带 AudioListener —— 与前序夹具同纪律
            DestroyAllNamed("Main Camera");

            yield return null;
        }

        // ══════════════ S10-06b 后置绑定真的发生了 ══════════════

        [UnityTest]
        public IEnumerator test_s10_06b_productionBoot_bindsCameraRigToEmergencyDriver()
        {
            yield return Boot();

            var driver = ReadEmergencyChannel();
            Assert.IsNotNull(driver, "服务袋须暴露 EmergencyChannel(EmergencyChannelDriver)");
            Assert.IsTrue(driver.HasCameraRig,
                "生产启动序跑完后 `HasCameraRig` 必须为 true —— **删掉 BootRoot 的 BindCameraRig 调用" +
                "不会崩也不会报错**,只有这条断言会把「接线被悄悄删掉」变成红的(story-010 R-10.2)");
        }

        // ══════════════ S10-06 真机档位 ══════════════

        [UnityTest]
        public IEnumerator test_s10_06_armMovesCameraToTreatment_endReturnsToExplore()
        {
            yield return Boot();

            var rig = ReadCameraRig();
            Assert.IsNotNull(rig, "启动序应建 CameraRig(第 5 步)");
            Assert.AreEqual(CameraMode.Explore, rig.Mode, "前置:启动档 = Explore");

            var driver = ReadEmergencyChannel();
            Assert.IsNotNull(driver, "服务袋须暴露 EmergencyChannel");

            // ── Act 1:动作开始 ────────────────────────────────
            driver.Arm(ActionOrdinal, new PatientId(ReadFirstPresentId()));
            yield return WaitForMode(rig, CameraMode.Treatment);

            Assert.AreEqual(CameraMode.Treatment, rig.Mode,
                "动作开始 ⇒ 相机须真的进 `Treatment`(AC-10-11 的前半)。\n" +
                "⚠️ 若本条红且 L1 绿,先查 `BootRoot.Update` 里的 `_cameraRig.Tick(...)` —— " +
                "**请求发了但档位机不结算**是同一种「看起来接好了其实没生效」的失败(2026-11-02 实测过一次)。");

            // ── Act 2:动作结束(零样本路径也要回档)────────────
            driver.EndAction(ReadPlayerActorId());
            yield return WaitForMode(rig, CameraMode.Explore);

            Assert.AreEqual(CameraMode.Explore, rig.Mode,
                "动作结束 ⇒ 相机须真的回 `Explore` —— 这是 AC-10-11 的目的从句「**相机不卡在近景**」");
        }

        // ══════════════ 辅助 ══════════════

        private IEnumerator Boot()
        {
            _root = new GameObject("BootRoot_CameraModeIntent");
            _boot = _root.AddComponent<BootRoot>();

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), "启动序须在超时内完成");

            Time.captureDeltaTime = 1f / 60f;

            // 让世界场景 additively 载入后的首帧跑完(相机第 5 步已建)
            for (int i = 0; i < 3; i++)
                yield return null;
        }

        /// <summary>轮询直到档位达成(转场时长 0.3 s ⇒ 约 18 帧),超时则让调用方断言失败。</summary>
        private static IEnumerator WaitForMode(CameraRig rig, CameraMode target)
        {
            for (int i = 0; i < MaxWaitFrames && rig.Mode != target; i++)
                yield return null;
        }

        /// <summary>按名销毁**含 inactive** 的同名对象(预摆相机被启动序禁用,`GameObject.Find` 找不到)。</summary>
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

            // 回退:服务袋非空即视为启动序已装配
            return ReadServices() != null;
        }

        private object ReadServices()
        {
            if (_boot == null) return null;
            var field = typeof(BootRoot).GetField("_services",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(_boot);
        }

        private CameraRig ReadCameraRig()
        {
            if (_boot == null) return null;
            var field = typeof(BootRoot).GetField("_cameraRig",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(_boot) as CameraRig;
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
            var presenceProp = services?.GetType().GetProperty("Presence");
            var presence = presenceProp?.GetValue(services);
            var ids = presence?.GetType().GetMethod("PresentPatientIds")?.Invoke(presence, null)
                      as System.Collections.Generic.IReadOnlyCollection<int>;
            if (ids != null)
            {
                foreach (int id in ids) return id;
            }
            return 0;
        }

        /// <summary>玩家 actorId 由启动序铸造;取「Player」组件读不到就用 0(只是事件归属,不影响档位判据)。</summary>
        private int ReadPlayerActorId()
        {
            var services = ReadServices();
            var regProp = services?.GetType().GetProperty("PlayerActors");
            var reg = regProp?.GetValue(services);
            if (reg == null) return 0;
            var idsProp = reg.GetType().GetProperty("PlayerIds");
            if (idsProp?.GetValue(reg) is System.Collections.Generic.IReadOnlyCollection<int> ids)
            {
                foreach (int id in ids) return id;
            }
            return 0;
        }
    }
}
