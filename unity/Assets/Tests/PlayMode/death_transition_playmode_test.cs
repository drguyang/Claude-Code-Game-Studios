// S23-7 · story-005 接线兑付 + story-006 视觉装配 —— PlayMode 全链(2026-11-01)
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读:**零读盘**(真启动装配;Addressables world 场景由启动序拉)。
// 写:**仅图形模式写 2 张截图** ⇒ `unity/Logs/shots/`(gitignored 指定暂存区 —— 具名声明:
//    `s237_death_falling.png`(Φ1 倒下渐入)· `s237_revive_waking.png`(Φ3 醒来),
//    承仓库「Visual/Feel → 截图 + 签核」体例)。
//    ⚠️ **batch 结构性零写盘**:`ScreenCapture` 依赖 game view backbuffer,
//    `WaitForEndOfFrame` 在无渲染帧的 batch 下**永不触发(协程挂死,2026-11-01 实测)**;
//    故截图路径全程由 `Application.isBatchMode` 守卫 —— batch 跑 = 零写盘、零挂死风险。
//
// ── 本件记分面 ──────────────────────────────────────────────────────────────
// ✅ AC-29-10(实发半):1 侧传送实装(TeleportCommandSink)→ PlayerController.Teleport
//    → 跨格提交链发 ActorCellEntered(AC-29-10 的「复活来源 ActorCellEntered 实发数」自此有真链)
// ✅ story-006 视觉装配:呈现器相位序列(渐入 → 躺定 → 醒来 → 消散 → 零叠层)真启动可见
//    (CanvasGroup alpha 取证)+ [L] 走查截图(2 帧;主创签核仍人工)
// ⚠️ **不记绿**:[L] 听测(AC-29-12)与节奏主观判定仍待桌面批人工签核;本件只产出
//    截图与自动化形状面。
// 形态承 camera_follow_wiring_test(BootRoot 反射观测 + Addressables world 卸载)。

using System;
using System.IO;
using System.Collections;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>死亡过渡 + 复活传送的真启动全链(1 侧实装 + 42 侧呈现装配)。</summary>
    public class DeathTransitionPlayModeTest
    {
        private const float BootTimeoutSeconds = 15f;

        private GameObject _root;
        private BootRoot _boot;
        private CompositionRootServices _services;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // ⚠️ **清理次序 = 先拆 BootRoot 层级,再恢复预摆**(2026-11-01 实测修复):
            // 反过来(先 `preset.SetActive(true)` 后拆 CameraRig)会开一个**两个 active
            // AudioListener 并存**的窗口 —— `AC-ADR020-10` 的单挂点断言在该窗口里**每帧洪泛**
            // (全量 PlayMode 实测日志膨胀到 205 MB / 180 万条,Editor 近乎停摆)。
            // CameraRig 已 `SetParent(BootRoot)`(启动序第 5 步)⇒ 随 `_root` 层级销毁,
            // **不**显式 Destroy(否则与层级销毁重复)。

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

            // ⚠️ 经服务袋引用销毁(不按名字 `Find` —— 同名残留会让本件误删别人的实例)。
            var presenter = _services != null ? _services.DeathPresenter : null;
            if (presenter != null) UnityEngine.Object.Destroy(presenter.gameObject);

            var player = GameObject.Find("Player");
            if (player != null) UnityEngine.Object.Destroy(player);

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
                _boot = null;
            }

            // 让 Destroy 生效(**必须**在动预摆之前):否则两个 listener 在同一帧并存
            // ⇒ 触发上述洪泛。
            yield return null;

            // ⚠️ **销毁本件自造的预摆**(2026-11-01 第二次实测修复):它带 AudioListener,
            // 若只 `SetActive(true)` 而不销毁,会**留到后续所有 PlayMode 测试**,与它们各自的
            // CameraRig listener 并存 ⇒ 同一洪泛(实测日志 14 MB / 15 万条)。
            // 本件的预摆是**测试夹具**(生产场景由 Boot.unity 预摆),用完即弃。
            var preset = GameObject.Find("Main Camera");
            if (preset != null)
            {
                UnityEngine.Object.Destroy(preset);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator test_deathToRevive_realBoot_teleportEmitsCellEntered_andPresenterRunsPhases()
        {
            // Arrange:预摆 Main Camera(启动序断言「恰 1 AudioListener」的 fixture)。
            // ⚠️ **清场先行**(2026-11-01 全量串跑实测):前序测试可能留下 Camera / AudioListener
            // ⇒ 本件启动序读到 ≥2 个 ⇒ `AC-ADR020-10` fail-loud(全量串跑红,定向单跑不暴露)。
            // ⚠️ `FindObjectsOfType<T>(true)` = **含未激活对象**:前序测试的 CameraRig 可能已被
            // `SetActive(false)`(启动序第 5 步),默认重载搜不到 ⇒ 仍会计入 listener 总数。
            foreach (var stale in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (stale != null) UnityEngine.Object.Destroy(stale.gameObject);
            }
            // 连同 CameraRig 宿主一起拆(它们可能各自挂着别的 listener / 相机组件)。
            foreach (var rig in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (rig != null && rig.name == "CameraRig")
                    UnityEngine.Object.Destroy(rig.gameObject);
            }

            // ⚠️ **拆掉一切残留的 `Main Camera`**(2026-11-01 全量串跑**偶发红**根治):
            //   根因 = **多份同名预摆**。`camera_follow_wiring_test` / `audio_wiring_test` /
            //   本件各自 `new GameObject("Main Camera")`,而前两者的 TearDown 用
            //   `GameObject.Find("Main Camera")` —— **只查激活对象**,被启动序
            //   `SetActive(false)` 的那个就**留在场上**。
            //   随后 `BootRoot.FindInactiveInclusive("Main Camera")` 按
            //   `FindObjectsByType(Camera, Include)` 的**枚举序**返回**第一个**匹配 ——
            //   残留的旧件会抢在本次新建的前面被返回 ⇒ BootRoot 摘掉**错的那一个**的
            //   listener ⇒ 本次预摆的 listener 仍在 ⇒ `active/all = 2` ⇒ `AC-ADR020-10` 抛。
            //   ⚠️ **为什么是偶发**:`FindObjectsSortMode.None` 的枚举序不保证稳定
            //   ⇒ 有时先返回本次新建的(摘对了 ⇒ 绿),有时先返回残留的(摘错 ⇒ 红)。
            //   ⇒ 本件清场必须**把同名件全拆干净**,不能只清 listener 组件。
            foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cam != null && cam.gameObject.name == "Main Camera")
                    UnityEngine.Object.Destroy(cam.gameObject);
            }
            yield return null;   // 让销毁生效(否则本帧仍计入 active 数)
            yield return null;   // 两帧:Destroy 在帧末生效,再等一帧确保 active 集已刷新

            var presetCamObj = new GameObject("Main Camera");
            presetCamObj.AddComponent<Camera>();
            presetCamObj.AddComponent<AudioListener>();

            _root = new GameObject("BootRoot_DeathTransition");
            _boot = _root.AddComponent<BootRoot>();

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), "启动序须在超时内完成(形态承 camera_follow 夹具)");

            _services = ReadServices();
            Assert.IsNotNull(_services, "服务袋经反射可读");
            Assert.IsNotNull(_services.TeleportSink, "1 侧传送实装已装配(story-005 接线兑付)");

            // 找本地玩家 id(花名册唯一登记;BootRoot 4.4 注册)。
            int playerId = -1;
            for (int id = 0; id < 64 && playerId < 0; id++)
                if (_services.PlayerActors.IsPlayer(id))
                    playerId = id;
            Assert.GreaterOrEqual(playerId, 0, "本地玩家 id 应已在花名册");

            var player = GameObject.Find("Player");
            Assert.IsNotNull(player, "玩家应存在");
            Vector3 posBefore = player.transform.position;

            long tick = ReadCurrentTick() + 1;

            // Act ①:写入死亡事实(测试侧临时写者 —— 模拟 sim 判死;驱动呈现投影)。
            var diedPayload = new PlayerDiedPayload(playerId, new WorldPos(5, 0, 3), tick);
            _services.EventSink.Append(new SimEvent(
                tick, PatientId.None, -1, EventKind.PlayerDied,
                _services.Encoder.Encode(EventKind.PlayerDied, diedPayload)));
            yield return null;

            // ⚠️ **等 tick 前进**(2026-11-01 全量串跑实测):传送的 `ActorCellEntered` 必须
            // **严格晚于** `deathTick`(`DeathTransitionQuery` 判据是 `e.Tick > deathTick`),
            // 否则投影恒 Downed ⇒ 相位机永不转 Waking ⇒ 消散断言必红。
            // 串跑时 `tick = CurrentTick + 1` 可能恰被同一帧的边沿追上 ⇒ 显式推进数个 tick。
            long minCellTick = tick + 1;
            float tickDeadline = 5f;
            while (ReadCurrentTick() < minCellTick && tickDeadline > 0f)
            {
                tickDeadline -= Time.deltaTime;
                yield return null;
            }
            Assert.GreaterOrEqual(ReadCurrentTick(), minCellTick,
                "[前置] tick 须已前进到 deathTick 之后(传送格事件才能投影成 Reviving)");

            // 呈现器开始渐入(Φ1)。
            // ⚠️ **经服务袋取唯一实例,不用 `GameObject.Find`**(2026-11-01 全量串跑实测):
            // 按名字查找在同名残留对象存在时**有歧义**(会拿到别的实例 ⇒ 其 Canvas 未建 ⇒
            // CanvasGroup 取不到)。服务袋引用 = BootRoot 4.3b 装配的那一个。
            var presenterComp = _services.DeathPresenter;
            Assert.IsNotNull(presenterComp, "死亡过渡呈现器应已装配(BootRoot 4.3b · 服务袋引用)");

            // ⚠️ `includeInactive: true` 是**必须的**:Φ0(零叠层)时 `canvasGo.SetActive(false)`,
            // 而默认 `GetComponentInChildren` 不搜未激活对象。
            CanvasGroup group = null;
            float groupDeadline = Time.realtimeSinceStartup + 2f;
            while (group == null && Time.realtimeSinceStartup < groupDeadline)
            {
                yield return null;
                group = presenterComp.GetComponentInChildren<CanvasGroup>(true);
            }
            Assert.IsNotNull(group, "全屏叠层 CanvasGroup 应存在(Awake 建 Canvas 后)");
            Assert.IsTrue(group.gameObject.activeInHierarchy, "叠层已激活(Φ1)");
            Assert.That(group.alpha, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f), "渐入中");

            // [L] 走查截图 ①:倒下渐入(Φ1)。
            // ⚠️ **只在有渲染帧时产出** —— batch(-nographics / 无 backbuffer)下
            // `WaitForEndOfFrame` 永不触发(协程挂死,2026-11-01 实测),
            // 故用 `Application.isBatchMode` 守卫:batch 跳过截图、图形模式才截。
            // 落 `Logs/shots/`(gitignored 暂存区,文件头已具名声明)。
            // ⚠️ **截图时机 = 轮询 `group.alpha` 到 ≥ 0.7**,不是固定等 0.4 s:
            //    Φ1 渐入 1.2 s,0.4 s 时 alpha ≈ 0.33 ⇒ 叠层淡到「看起来没截到」。
            //    且固定 `WaitForSeconds` 走**真实时间**,而相位机吃 `Time.deltaTime`
            //    —— 前序夹具留下的 `Time.captureDeltaTime` 会让两者脱钩(2026-11-01 实测)。
            float shotDeadline = Time.realtimeSinceStartup + 5f;
            while (group.alpha < 0.7f && Time.realtimeSinceStartup < shotDeadline)
            {
                yield return null;
            }
            Debug.Log($"[S237] 截图①时机: group.alpha={group.alpha:0.00}(期望 ≥ 0.7)");
            CaptureShot("s237_death_falling.png");
            yield return CaptureEndOfFrame();

            // Act ②:1 侧传送实装 —— RequestTeleport(整数命令半)→ 格心连续坐标。
            var bedCell = new WorldPos(2, 0, 2);
            _services.TeleportSink.RequestTeleport(playerId, bedCell);
            Debug.Log($"[S237] teleport 后位置 = {player.transform.position}(期望格心 (2.5, 0.5, 2.5))");
            yield return new WaitForSeconds(0.3f);   // 等 ≥ 若干 tick 边沿(50 ms/个)提交跨格

            // [L] 走查截图 ②:醒来(Φ3;复活落地后相位)。
            yield return new WaitForSeconds(0.5f);
            CaptureShot("s237_revive_waking.png");
            yield return CaptureEndOfFrame();

            // 诊断 dump:该 actor 的全部格事件(tick + 格),供失败归因。
            foreach (var e in _services.Stream.Events.Where(e => e.Kind == EventKind.ActorCellEntered))
            {
                var dp = _services.Decoder.Decode<ActorCellEnteredPayload>(e.Kind, e.Payload);
                if (dp.ActorId == playerId)
                    Debug.Log($"[S237] cell 事件: tick={e.Tick} cell=({dp.Cell.X},{dp.Cell.Y},{dp.Cell.Z})");
            }
            Debug.Log($"[S237] deathTick(过滤下界)={tick} 当前 CurrentTick={ReadCurrentTick()}");

            // Assert:AC-29-10 实发半 —— PlayerDied 之后出现该 actor 的 ActorCellEntered,
            // 其中**存在** teleport 提交的落格事件(∃ 判据;不假设「此后无人再动玩家」)。
            var cellAfter = _services.Stream.Events
                .Where(e => e.Kind == EventKind.ActorCellEntered)
                .Select(e => _services.Decoder.Decode<ActorCellEnteredPayload>(e.Kind, e.Payload))
                .Where(p => p.ActorId == playerId && p.Tick >= tick)
                .ToList();
            Assert.IsNotEmpty(cellAfter,
                "复活传送后控制器跨格提交链**实发** ActorCellEntered(AC-29-10 实发半;写者 = 1)");
            Assert.IsTrue(cellAfter.Any(p =>
                    p.Cell.X == bedCell.X && p.Cell.Y == bedCell.Y && p.Cell.Z == bedCell.Z),
                $"落格 = 命令格(∃ teleport 提交;实发集 = [{string.Join("; ", cellAfter.Select(p => $"({p.Cell.X},{p.Cell.Y},{p.Cell.Z})@{p.Tick}"))}])");
            Assert.That(player.transform.position.x, Is.EqualTo(bedCell.X + 0.5f).Within(0.05f),
                "玩家连续坐标 = 格心(整数命令半 → Vector3 连续半,ADR-025 QQ-01)");

            // [L] 走查支撑:醒来相位(Φ3;传送后 0.6 s,此段 alpha 恒 1 满屏)。
            yield return new WaitForSeconds(0.6f);

            // Act ③:过渡序列走完(Wake 1.5 s + Fade 1.0 s ≈ 2.5 s)。
            // ⚠️ **按游戏时间(Σ Time.deltaTime)轮询,不按真实时间**(2026-11-01 全量串跑实测):
            // 相位机自身吃 `Time.deltaTime`,而 `captureDeltaTime`(前序测试的全局态)会让
            // `deltaTime` 与真实秒**脱钩** ⇒ 按真实时间等 8 s 可能只推进了不足 2.5 s 游戏时间
            // (定向单跑无此污染,全量串跑才暴露)。游戏时间口径与相位机同源 = 唯一可靠判据。
            var drvField = typeof(DaYiJingCheng.Gameplay.Presentation.DeathTransition.DeathTransitionPresenter)
                .GetField("_driver", BindingFlags.NonPublic | BindingFlags.Instance);
            float gameSeconds = 0f;
            float nextProbe = 0f;
            bool settled = false;
            while (gameSeconds < 8f && !settled)
            {
                yield return null;
                gameSeconds += Time.deltaTime;
                settled = group.alpha == 0f && !group.gameObject.activeSelf;
                if (gameSeconds >= nextProbe)
                {
                    nextProbe = gameSeconds + 2f;
                    var drv = (DaYiJingCheng.Gameplay.Presentation.DeathTransition.DeathTransitionDriver)
                        drvField.GetValue(presenterComp);
                    int probePhase = DaYiJingCheng.Sim.DeathTransitionQuery.Project(
                        _services.Stream.Events, _services.Decoder, playerId, 1).Phase;
                    Debug.Log($"[S237] 消散探针: 游戏时间={gameSeconds:0.0#}s" +
                              $" driver={drv.Phase} Elapsed={drv.Elapsed:0.##}" +
                              $" 测试侧Query.Phase={probePhase} alpha={group.alpha:0.###}");
                }
            }
            Assert.IsTrue(settled,
                $"消散毕 ⇒ alpha 0 + 叠层停用(Φ0);终态 alpha={group.alpha:0.###} active={group.gameObject.activeSelf}");
            Assert.IsFalse(group.gameObject.activeSelf, "Φ0 ⇒ 叠层停用(零开销)");

            Debug.Log($"[DeathTransition] 全链 OK:pos {posBefore} → {player.transform.position};" +
                      $"格事件 {cellAfter.Count} 条;相位序列 Falling→Lying→Waking→Fading→Inactive 已走完");
        }

        // ── [L] 走查截图(图形模式才产出;batch 结构性无 backbuffer)────────────────

        /// <summary>
        /// 请求截图(<b>仅图形模式</b>)。batch / nographics 下 `ScreenCapture` 出零字节 PNG,
        /// 且 `WaitForEndOfFrame` 永不触发 ⇒ 必须守卫,否则协程挂死(2026-11-01 实测挂 10 分钟)。
        /// </summary>
        private static void CaptureShot(string fileName)
        {
            if (Application.isBatchMode) return;   // batch 结构性无帧 ⇒ 不请求
            string dir = Path.Combine(Application.dataPath, "..", "Logs", "shots");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName);
            ScreenCapture.CaptureScreenshot(path, 2);
            Debug.Log($"[S237] 已请求截图(2×)⇒ {path}(写盘延后 1–2 帧)");
        }

        /// <summary>等帧末 —— <b>仅图形模式</b>(batch 下该 yield 永不返回)。</summary>
        private static IEnumerator CaptureEndOfFrame()
        {
            if (Application.isBatchMode) yield break;
            yield return new WaitForEndOfFrame();
            yield return null;   // 让 ScreenCapture 写盘
        }

        private bool ReadBooted()
        {
            if (_boot == null) return false;
            var field = typeof(BootRoot).GetField("_booted", BindingFlags.NonPublic | BindingFlags.Instance);
            return field != null && (bool)field.GetValue(_boot);
        }

        private CompositionRootServices ReadServices()
        {
            var field = typeof(BootRoot).GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(_boot) as CompositionRootServices;
        }

        private long ReadCurrentTick()
        {
            if (_boot == null) return -1;
            var field = typeof(BootRoot).GetField("_tickDriver", BindingFlags.NonPublic | BindingFlags.Instance);
            var driver = field?.GetValue(_boot);
            if (driver == null) return 0;
            var prop = driver.GetType().GetProperty("CurrentTick");
            return prop != null ? (long)prop.GetValue(driver) : 0;
        }
    }
}
