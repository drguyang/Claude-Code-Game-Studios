// 门④ · 组合根冒烟(Go/No-Go 之一 —— `production/sprints/sprint-04.md:154` 门④)
//
// 判据(逐字承门定义):**PlayMode:根对象存在 + Initialize 已调用 + EventStream 非空**。
//
// 覆盖面 = Boot 四步启动序**首次真跑**(阶段 1 装配轮登记「未验:编辑器真机 Play(Boot 四步
//   启动序首次运行)—— 归门④/收口轮」,`active.md:62`;本测试即其收口载体,2026-10-10 批次 F)。
//
// 形态:`new GameObject + AddComponent<BootRoot>()` ⇒ Awake/Start 自动跑(异步四步:
//   ① Addressables.InitializeAsync → ② CompositionRoot.Assemble → ③ LoadSceneAsync("world",
//   Additive) → ④ 玩家生成 + Player.Initialize)→ 帧循环等 `_booted` 或超时。
// fail-loud 语义:`BootRoot.Start` 任一步失败 = LogError + rethrow(E-13 启动期硬失败)⇒
//   异常/错误日志使本测试红(不吞错)—— 门的语义正是「启动序能不能跑」。
// 拆卸:`[UnityTearDown]` 尽力清理(Player 销毁 + world 经 Addressables 卸载 + 根销毁),
//   防本测试的 additive 场景污染同 run 其余测试。
//
// 与 boot_scene_wiring_test 的分工:后者(EditMode)= 静态接线断言(挂载 guid 恰 1 次 /
//   address "world" 同源);本测试(PlayMode)= 运行期真跑。两者互补,禁互相替代。

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
    /// <summary>
    /// 组合根冒烟 —— Boot 四步启动序在 PlayMode 下真跑,断言门④三判据。
    /// </summary>
    public class CompositionRootSmokeTest
    {
        /// <summary>启动序超时(秒):Addressables init + world 场景加载在 batch 下的宽松上界。</summary>
        private const float BootTimeoutSeconds = 15f;

        private GameObject _root;
        private BootRoot _boot;

        /// <summary>
        /// 拆卸(尽力而为,不断言):销毁 Player · world 场景经 Addressables 卸载 · 根销毁。
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var player = GameObject.Find("Player");
            if (player != null) UnityEngine.Object.Destroy(player);

            // world 场景句柄在 BootRoot 私有字段(启动序第 3 步);未走到第 3 步 ⇒ 句柄无效
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
                    valid = false; // 拆卸路径不阻断(无 yield 于 catch 内 —— iterator 合法性)
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
        /// 门④ 三判据:根对象存在 · Initialize 已调用 · EventStream 非空。
        /// 附:启动序第 4 步玩家生成佐证 + world 场景 additive 加载成功佐证。
        /// </summary>
        [UnityTest]
        public IEnumerator test_compositionRoot_smoke_bootSequenceCompletes()
        {
            // Arrange:根对象(Boot 启动序宿主;Awake 即跑,零异步零装配)
            _root = new GameObject("BootRoot_Smoke");
            _boot = _root.AddComponent<BootRoot>();

            // Act:帧循环等四步启动序完成(_booted = true 置位于 Player.Initialize 之后)
            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;

            // Assert ①:根对象存在
            Assert.IsNotNull(_boot, "BootRoot 根对象应存在(AddComponent 后不可为 null)");
            Assert.IsTrue(_boot.gameObject != null, "根对象的 GameObject 应存活");

            // Assert ②:Initialize 已调用(_booted 置位于 Player.Initialize 之后 ⇒ true = 四步全完成)
            Assert.IsTrue(ReadBooted(),
                $"Boot 四步启动序应在 {BootTimeoutSeconds}s 内完成(_booted = true)——" +
                "未完成 = 启动序卡死(Addressables init / world 加载)或中途失败(已由 fail-loud 日志暴露)");

            // Assert ③:EventStream 非空(组合根装配产出的事件流对象)
            var services = ReadServices();
            Assert.IsNotNull(services,
                "CompositionRootServices 服务袋应存在(启动序第 2 步 CompositionRoot.Assemble 已发生)");
            Assert.IsNotNull(services.Stream,
                "EventStream 非空(门④字面判据)—— 流对象缺失 = 装配未产出真源");
            Assert.GreaterOrEqual(services.Stream.Count, 0,
                "事件流应可读(计数 ≥ 0;冒烟不写事件,非空指引用非 null 语义)");

            // 附:启动序第 4 步佐证(玩家生成)
            Assert.IsNotNull(GameObject.Find("Player"),
                "玩家对象应已生成(启动序第 4 步:PlayerController + Initialize)");
        }

        // ── 反射读 BootRoot 私有状态(冒烟观测面;不改生产可见性)──────────────

        private bool ReadBooted()
        {
            if (_boot == null) return false;
            var field = typeof(BootRoot).GetField("_booted",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field != null && (bool)field.GetValue(_boot);
        }

        private CompositionRootServices ReadServices()
        {
            if (_boot == null) return null;
            var field = typeof(BootRoot).GetField("_services",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(_boot) as CompositionRootServices;
        }
    }
}
