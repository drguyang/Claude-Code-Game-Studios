// 相机「倒下」两段式 —— **视觉走查**夹具(2 侧 · 2026-11-01)
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读:**零读盘**(真启动装配;Addressables world 场景由启动序拉)。
// 写:**仅图形模式写截图** ⇒ `unity/Logs/shots/`(gitignored 指定暂存区 —— 具名声明:
//    `cdw_00_explore.png`(常态基线)· `cdw_01_sag_030.png` / `cdw_02_sag_060.png` /
//    `cdw_03_sag_090.png`(下沉进度 0.3 / 0.6 / 0.9)· `cdw_04_settled.png`(躺定)。
//    ⚠️ **batch 结构性零写盘**:`ScreenCapture` 依赖 game view backbuffer,
//    `WaitForEndOfFrame` 在无渲染帧的 batch 下**永不触发(协程挂死,2026-11-01 实测)**;
//    故截图路径全程由 `Application.isBatchMode` 守卫。
//
// ─────────────────────────────────────────────────────────────────────────────
// 本件的目的:**把「观感」变成可看的东西**,不是断言数值
// ─────────────────────────────────────────────────────────────────────────────
//   机械判据(`camera_downed_phases_test`)只证明**位移发生了**;它们证明不了
//   「看起来像失去支撑」。spec §9 的三条形状要求里,只有「禁甩镜」「非视场旋转」
//   能机械化;**「下沉 + 微倾的幅度/节奏是否读作失去支撑」只能靠看**。
//   ⇒ 本件在真实启动序里驱动相机进 `Downed`,在三个时刻截图,供人(或视觉模型)判读。
//
// ⚠️ **本件不断言观感**(那是 `[L]` 人工面);它只断言**截图路径本身跑通了**
//   (相位到位 + 位姿真的变了),否则截出来的图无意义。

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Camera;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>相机倒下两段式的视觉走查(图形模式产图;batch 只验路径)。</summary>
    [TestFixture]
    public sealed class CameraDownedVisualWalkthrough
    {
        private GameObject _root;
        private BootRoot _boot;
        private CameraRig _rig;

        private const float BootTimeoutSeconds = 30f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 🔴 **2026-11-01 全量串跑实测修复**:本件走过真启动序 ⇒ 以 **additive** 方式
            //    装载了 Addressables 的 World 场景。**卸载 additive 场景不会由
            //    `Object.Destroy` 或测试框架自动完成** ⇒ 它会**留到后续所有 PlayMode 测试**。
            //    后果实测:`main_menu_wiring_test` 的定序判据
            //    「主菜单必须出现在**世界装载之前**」读到「World 已装载」⇒ 转红
            //    (定向单跑绿 / 全量串跑红 —— 本项目第 N 次踩这个形态)。
            //    ⇒ 必须显式 `Addressables.UnloadSceneAsync(handle)`,handle 由反射从
            //    `BootRoot._worldLoadHandle` 取(承既有 boot 夹具范式:
            //    `death_transition_playmode_test` / `camera_follow_wiring_test` 等 10 件同款)。
            AsyncOperationHandle<SceneInstance> worldHandle = default;
            if (_boot != null)
            {
                try
                {
                    var f = typeof(BootRoot).GetField("_worldLoadHandle",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    worldHandle = (AsyncOperationHandle<SceneInstance>)f.GetValue(_boot);
                }
                catch (Exception)
                {
                    worldHandle = default;
                }
            }
            if (worldHandle.IsValid())
            {
                var unload = Addressables.UnloadSceneAsync(worldHandle);
                while (!unload.IsDone) yield return null;
            }

            // ⚠️ 次序 = 先拆 BootRoot 层级,再动预摆(承 `death_transition_playmode_test` 的
            //    「两个 active AudioListener 并存窗口会让 AC-ADR020-10 每帧洪泛」教训)。
            //    ⚠️ Presenter 是**独立 GameObject**,不在 `_root` 层级下 ⇒ 须显式销毁
            //    (否则留到后续测试,抢别人的相机档位)。
            var services = ReadServices();
            var presenter = services != null ? services.DeathPresenter : null;
            if (presenter != null) UnityEngine.Object.Destroy(presenter.gameObject);

            DestroyAllNamed("Player");
            DestroyAllNamed("CameraRig");
            yield return null;

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
                _boot = null;
            }
            _rig = null;
            yield return null;

            // 预摆最后拆:它带 `AudioListener`,须在 CameraRig 已销毁之后再移除。
            DestroyAllNamed("Main Camera");
            yield return null;
        }

        /// <summary>
        /// 驱动相机进 `Downed`,在 常态 / 下沉中段 / 躺定 三个时刻截图。
        /// </summary>
        [UnityTest]
        public IEnumerator test_downed_twoPhase_producesWalkthroughShots()
        {
            // Arrange:清场 + 预摆(承 death_transition 夹具同纪律)。
            foreach (var stale in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (stale != null) UnityEngine.Object.Destroy(stale.gameObject);
            foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (cam != null && cam.gameObject.name == "Main Camera")
                    UnityEngine.Object.Destroy(cam.gameObject);
            yield return null;
            yield return null;

            var presetCamObj = new GameObject("Main Camera");
            presetCamObj.AddComponent<Camera>();
            presetCamObj.AddComponent<AudioListener>();

            _root = new GameObject("BootRoot_DownedWalkthrough");
            _boot = _root.AddComponent<BootRoot>();
            _boot.SaveRootOverride = PlayModeTempSaveRoot.New("camera_downed_visual_walkthrough");

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(ReadBooted(), "启动序须在超时内完成");

            _rig = ReadCameraRig();
            Assert.IsNotNull(_rig, "启动序应建 CameraRig(第 5 步)");

            // ⚠️ **必须停掉 `DeathTransitionPresenter`**(2026-11-01 第二次实测后订正):
            //    它在真实启动序里每帧跑 `Update`,检测到「没死亡」⇒ 发 `Explore`
            //    ⇒ 与我的 `Downed` 请求**每帧竞争**。而**甲案**允许同请求方(29)降档
            //    ⇒ 我每帧补 `Downed` 也补不过它(它在我之后发,下一帧结算时 `_pending`
            //    里只剩 `Explore`)⇒ 首两跑都是 `phase=None sag=0.00`、前三张图字节完全相同。
            //    ⇒ 本走查要测的是**相机两段式观感**,不是呈现器联动
            //    (后者由 `death_transition_playmode_test` 覆盖)⇒ 停掉干扰源。
            // ⚠️ Presenter 是**独立 GameObject**(`new GameObject("DeathTransitionPresenter")`,
            //    `BootRoot.cs:537`),**不在** `_root` 层级下 ⇒ `GetComponentInChildren` 找不到
            //    (第三次实测:`停用` 那行没打印)。改从**服务袋**取(它是唯一实例的登记处,
            //    承 `death_transition_playmode_test` 的同款纪律)。
            var services = ReadServices();
            var presenter = services?.DeathPresenter;
            if (presenter != null)
            {
                presenter.enabled = false;
                Debug.Log("[CDW] 已停用 DeathTransitionPresenter(避免它与本走查抢档位)");
            }
            else
            {
                Debug.LogWarning("[CDW] 未取到 DeathPresenter —— 呈现器仍在跑,档位可能被它抢回");
            }

            // ① 常态基线(Explore)。
            _rig.SetModeForTest(CameraMode.Explore, requesterId: 29);
            for (int i = 0; i < 5; i++) yield return null;
            // 🔁 **2026-11-01 补:采基线的真实相机变换**。
            //    理由 = 覆盖缺口:`CameraRig` 的 EditMode 夹具只测**自己算出来的**位姿,
            //    而 `BootRoot` 把它落到 `cam.transform` 上那一刻(`:1717-1723`)没有任何夹具守。
            //    若有人把接线改回 `cam.transform.LookAt(target)`,微倾会**静默失效**
            //    (EditMode 全绿,只有图上看得出)—— 典型的「行为等价掩盖结构分叉」预警面。
            Camera rigCam = _rig.Camera;
            Assert.IsNotNull(rigCam, "CameraRig 上须有 Camera 组件");
            Vector3 baselinePos = rigCam.transform.position;
            Vector3 baselineFwd = rigCam.transform.forward;
            CaptureShot("cdw_00_explore.png");
            yield return CaptureEndOfFrame();

            // ② 下沉过程:在**进度 0.3 / 0.6 / 0.9** 三个点各截一张(看曲线形状)。
            // ⚠️ **判据用相位机自己的进度,不用累计 dt**(2026-11-01 实测订正):
            //    batch 下 `Time.deltaTime` 可能很大,累计 dt 一次循环就跨过一半
            //    ⇒ 首跑截到 sag=0.88(几乎到底,不是「中段」)。
            // ⚠️ **首跑还发现一件更重要的事**:`EaseOutSag(0.5) = 1 - 0.5³ = 0.875`
            //    ⇒ 缓动曲线**极度前置**:时间过半时已下沉 87.5%。这决定了「失支撑」
            //    观感是「猛地一沉然后慢慢停住」,不是「匀均下沉」——
            //    ⚠️ 这正是要**看**的东西,故多点取样。
            _rig.SetModeForTest(CameraMode.Downed, requesterId: 29);
            foreach (var (target, name) in new[]
                     {
                         (0.3f, "cdw_01_sag_030.png"),
                         (0.6f, "cdw_02_sag_060.png"),
                         (0.9f, "cdw_03_sag_090.png"),
                     })
            {
                // ⚠️ **循环条件不得带 `phase == Sagging`**(2026-11-01 第四次实测后订正):
                //    `SetModeForTest` 只置 `Mode = Downed`,**相位机要等第一次 `Tick` 才从
                //    `None` 转 `Sagging`** ⇒ 首版把 `phase == Sagging` 写进循环条件,
                //    进循环时它是 `None` ⇒ **循环一次都不跑** ⇒ 直接截图(三张图字节完全相同)。
                //    ⇒ 条件只留「进度未达目标 + 未超时」;相位由 `Step` 自己推进。
                float dl = Time.realtimeSinceStartup + 5f;
                while (_rig.DownedSagFraction < target && Time.realtimeSinceStartup < dl)
                {
                    _rig.Tick(Time.deltaTime);   // 显式驱动(相机在真实启动序里也由 BootRoot 驱动)
                    yield return null;
                }
                Debug.Log($"[CDW] 目标 {target:0.0}: mode={_rig.Mode} phase={_rig.DownedPhase} " +
                          $"sag={_rig.DownedSagFraction:0.00} tilt={_rig.DownedTiltFraction:0.00} " +
                          $"requester={ReadModeMachine().CurrentRequesterId}");
                CaptureShot(name);
                yield return CaptureEndOfFrame();
            }
            // ③ 躺定(推进到第二段)。
            float settleDeadline = Time.realtimeSinceStartup + 5f;
            while (_rig.DownedPhase != CameraDownedPhase.Settled
                   && Time.realtimeSinceStartup < settleDeadline)
            {
                _rig.Tick(Time.deltaTime);
                yield return null;
            }
            Assert.AreEqual(CameraDownedPhase.Settled, _rig.DownedPhase,
                "须推进到躺定(否则第三张图无意义)");

            Debug.Log($"[CDW] 躺定: sag={_rig.DownedSagFraction:0.00} tilt={_rig.DownedTiltFraction:0.00}");
            CaptureShot("cdw_04_settled.png");
            yield return CaptureEndOfFrame();

            // ── 只断言**路径**跑通(观感归 [L] 人工)──────────────────────
            Assert.AreEqual(1f, _rig.DownedSagFraction, 1e-4f, "下沉进度须到位");
            Assert.AreEqual(1f, _rig.DownedTiltFraction, 1e-4f, "微倾进度须到位");

            // ── 接线判据(2026-11-01 补):微倾/下沉**真的落到了相机变换上** ─────
            //    方案 ①(微倾走 `_pitch`)与方案 ②(微倾走 `GetLookTarget`)在
            //    `CameraRig` 内部都能算出「视线向下」;**唯一的区别只在 `BootRoot` 的接线**:
            //      ① `LookAt(target)`      ⇒ 视线目标不动 ⇒ 微倾被静默丢弃
            //      ② `LookAt(GetLookTarget(target))` ⇒ 微倾生效
            //    ⇒ 本条是那个接线的**唯一机械载体**。改回 ① ⇒ 这里红。
            Camera camSettled = _rig.Camera;
            Vector3 settledPos = camSettled.transform.position;
            Vector3 settledFwd = camSettled.transform.forward;

            Assert.Less(settledPos.y, baselinePos.y - 0.5f * CameraDownedDriver.SagDepth,
                $"躺定时相机 Y({settledPos.y:0.000})须明显低于常态({baselinePos.y:0.000})" +
                $" —— 下沉须真的作用到 `cam.transform` 上(BootRoot 接线)");

            // ⚠️ 判据**不能**用「settledFwd.y < baselineFwd.y」—— 下沉本身就会让俯角变化
            //    (相机降低 ⇒ 看向同一目标时俯角**变小**),两股变化混在一起,阈值没法定。
            //    改用**对照重建**:若接线退回 `LookAt(target)`,躺定时朝向必然是
            //    `normalize(playerPos - settledPos)`;实际朝向必须**明显更低**。
            Transform playerT = ReadPlayerTransform();
            Assert.IsNotNull(playerT, "启动序应建 Player(否则相机没有跟随目标,BootRoot 相机段整段不跑)");
            Vector3 noTiltFwd = (playerT.position - settledPos).normalized;

            Assert.Less(settledFwd.y, noTiltFwd.y - 0.10f,
                $"躺定时相机朝向 y 分量({settledFwd.y:0.000})须明显低于「微倾被丢弃」时的重建值" +
                $"({noTiltFwd.y:0.000})。若接线退回 `cam.transform.LookAt(target)`,两者会相等 ⇒ 本断言转红" +
                "(实测差 0.133,阈值 0.10 ⇒ 1.33× 余量;证伪轮 M6 已验:退回后差值恰为 0)");

            Debug.Log($"[CDW] 接线判据: ΔY={settledPos.y - baselinePos.y:0.000} m, " +
                      $"forward.y 基线 {baselineFwd.y:0.000} / 躺定 {settledFwd.y:0.000} / " +
                      $"去微倾重建 {noTiltFwd.y:0.000}");
        }

        // ── 截图辅助(图形模式才产出;batch 结构性无 backbuffer)────────────

        private static void CaptureShot(string fileName)
        {
            if (Application.isBatchMode) return;   // batch 结构性无帧 ⇒ 不请求
            string dir = Path.Combine(Application.dataPath, "..", "Logs", "shots");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName);
            ScreenCapture.CaptureScreenshot(path, 2);
            Debug.Log($"[CDW] 已请求截图(2×)⇒ {path}");
        }

        private static IEnumerator CaptureEndOfFrame()
        {
            if (Application.isBatchMode) yield break;
            yield return new WaitForEndOfFrame();
            yield return null;
        }

        // ── 反射观测(承既有 BootRoot 夹具纪律:不改生产可见性)──────────

        private bool ReadBooted()
        {
            if (_boot == null) return false;
            var f = typeof(BootRoot).GetField("_booted", BindingFlags.NonPublic | BindingFlags.Instance);
            return f != null && (bool)f.GetValue(_boot);
        }

        /// <summary>读档位机(`CameraRig._modeMachine` 私有 ⇒ 反射,只读;诊断用)。</summary>
        private CameraModeMachine ReadModeMachine()
        {
            if (_rig == null) return null;
            var f = typeof(CameraRig).GetField("_modeMachine",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return f?.GetValue(_rig) as CameraModeMachine;
        }

        /// <summary>读服务袋(`_services` 是 BootRoot 私有字段 ⇒ 反射,只读)。</summary>
        private CompositionRootServices ReadServices()
        {
            if (_boot == null) return null;
            var f = typeof(BootRoot).GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance);
            return f?.GetValue(_boot) as CompositionRootServices;
        }

        /// <summary>
        /// 读玩家变换(`BootRoot._player`,私有 ⇒ 反射,只读)。
        /// <para>用于重建「若微倾被丢弃,相机朝向应当是什么」—— 即 `LookAt(target)` 的对照值。</para>
        /// </summary>
        private Transform ReadPlayerTransform()
        {
            if (_boot == null) return null;
            var f = typeof(BootRoot).GetField("_player", BindingFlags.NonPublic | BindingFlags.Instance);
            var player = f?.GetValue(_boot) as MonoBehaviour;
            return player != null ? player.transform : null;
        }

        /// <summary>`CameraRig` 是 BootRoot 私有字段(不在服务袋)⇒ 反射取(只读)。</summary>
        private CameraRig ReadCameraRig()
        {
            if (_boot == null) return null;
            var f = typeof(BootRoot).GetField("_cameraRig",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return f?.GetValue(_boot) as CameraRig;
        }

        /// <summary>销毁场景内所有同名 GameObject(含 inactive)。</summary>
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
    }
}
