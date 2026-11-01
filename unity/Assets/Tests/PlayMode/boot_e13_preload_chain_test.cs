// S22-3 · smoke `#1` 的载体 —— 启动序 E-13 硬失败链(真跑 `Addressables.InitializeAsync` + data-core 预载)
//
// ▍为什么此前「无载体」(`tests/smoke/critical_paths.json` #1 的 reason)
//   > 启动序(Boot → Addressables 预载)需真启动;headless 无载体
//   前半句是真的:**EditMode 里预检必然判死**(`pipeline_gate_test.cs:1243` 已把它钉成判据 ——
//   `TryPreload()` 在 EditMode 必须返回 false)。后半句**不成立**:PlayMode 有真 catalog,
//   `Addressables.InitializeAsync` + `data-core` 预载**真跑得起来**。
//
// ▍判据(可证伪)
//   A `addressablesInit_andDataCorePreload_succeed`:真 `InitializeAsync`(Status 非 Failed)
//     → `TryPreload()` 返回 **true** → `IsPreloaded` → 诊断 `missing=[]` →
//     **内容断言**:经 `Provider.Load<ItemDataSet>()` 取到**非空**行集且 `ConfigVersion != 0`
//     (证明字节真被解码进 provider,**不是**「地址可解析就算过」)
//   B `loadThrows_reportsFalse_noHalfState`:装载抛 ⇒ 返回 false、`IsPreloaded` 仍 false、
//     `Provider` **抛**而不是返回 null(E-13 的「不降级」面:null 解引用会把启动期硬失败
//     变成现场空指针)。⚠️ 该 `catch` 分支在 EditMode 侧零覆盖,不是重复件。
//     (「预检 false ⇒ 短路」已由 `pipeline_gate_test.cs` 钉住,本件**不复制**。)
//
// ▍刻意**不**测
//   · 失败时**退出码/进程终止**:Unity Test Framework 里杀不了宿主进程;E-13 的「终止」在
//     生产里由 `BootRoot.Start` 的 `throw` 兑现(源码面),本件钉的是**可观察的三条不降级性质**。
//   · 主菜单走查(AC-42-F1 面,归 UX)。
//
// 写盘边界:本件零写盘。

using System;
using System.Collections;
using System.Reflection;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>启动序 E-13 硬失败链(`data-core` 预载 + 失败不降级)。</summary>
    public sealed class boot_e13_preload_chain_test
    {
        // ══════════════════════════════════════════════════════════════════
        // A 主判据(smoke #1 的载体)
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_addressablesInit_andDataCorePreload_succeed_withContent()
        {
            // ── 1. 真跑 Addressables 初始化(与 `BootRoot.Start` 第 1 步同款三件:
            //    autoReleaseHandle: false + await Task + 读 Status + 自己 Release)──
            AsyncOperationHandle initHandle = Addressables.InitializeAsync(autoReleaseHandle: false);
            try
            {
                while (!initHandle.IsDone) yield return null;

                Assert.AreNotEqual(AsyncOperationStatus.Failed, initHandle.Status,
                    "Addressables.InitializeAsync 失败 ⇒ E-13 启动序第一步就断(" +
                    (initHandle.OperationException != null ? initHandle.OperationException.Message : "无异常对象") + ")");
            }
            finally
            {
                Addressables.Release(initHandle);
            }

            // ── 2. 真跑 data-core 预载 ──
            bool ok = DataCorePreloader.TryPreload();

            Assert.IsTrue(ok,
                "data-core 预载必须成功(诊断:" + DataCorePreloader.LastPreloadDiagnostic + ")" +
                " —— 失败即 E-13 启动期硬失败");
            Assert.IsTrue(DataCorePreloader.IsPreloaded, "预载成功 ⇒ 静态标志必须置位");
            Assert.AreEqual("missing=[]", DataCorePreloader.LastPreloadDiagnostic,
                "全部地址可解析(诊断串是唯一可断言的观察点,不靠数 [Error] 日志)");

            // ── 3. 内容断言 ①:预载**当场**已把物品表解出来 ──
            //    ⚠️ 只断 `TryPreload() == true` 不够:`Preload()` 改成空实现时,前两条照样绿。
            //    ⚠️ 只断下面 ② 的 `Load<ItemDataSet>()` 也**不够**:`EnsureItems()` 是**惰性**的
            //    (`AddressablesDataProvider.cs:532`),`Preload()` 什么都不做,`Load<>` 仍会自己补装
            //    ⇒ ② 对「预载真的跑过」零判别力(2026-11-01 读源码时抓到)。
            //    ⇒ 观察点取**私有缓存字段在预载刚结束时的状态**:`_items` 是 `ItemDataSet?`,
            //      `GetValue` 对 `HasValue == false` 返回 null ⇒ 空实现必现 null。
            var provider = DataCorePreloader.Provider as AddressablesDataProvider;
            Assert.IsNotNull(provider, "`TryPreload()` 成功 ⇒ provider 必须是 AddressablesDataProvider");
            FieldInfo itemsField = typeof(AddressablesDataProvider)
                .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(itemsField, "`AddressablesDataProvider._items` 缺失 —— 观察点被改名?");
            Assert.IsNotNull(itemsField.GetValue(provider),
                "预载结束后 `_items` 必须已填充 —— 为 null 说明 `Preload()` 没真的解码(空实现形态)");

            // ── 4. 内容断言 ②:正常读面取得到**非空**行集(字节可解 + 内容非空)──
            ItemDataSet items = DataCorePreloader.Provider.Load<ItemDataSet>();
            Assert.IsNotNull(items.Items, "物品表行集不得为 null");
            Assert.Greater(items.Items.Length, 0, "物品表行集不得为空(空 = 只有地址、没有字节)");
            Assert.AreNotEqual(0u, items.ConfigVersion,
                "内容版本号由烘焙时从源 JSON 字节派生 ⇒ 为 0 说明字节没走到解码");
        }

        // ══════════════════════════════════════════════════════════════════
        // B 装载抛 ⇒ false + 不降级(Provider 抛,而不是给 null)
        //   ⚠️ 本分支在 EditMode 侧**零覆盖**:`pipeline_gate_test` 只钉了
        //   「预检 false ⇒ 短路」与「预检 true ⇒ 装载恰一次」,`catch` 分支没人走。
        //   ⇒ 这条不是重复件。
        //   `TryPreloadGated` 是 internal 且 Presentation 未对测试装配开 IVT
        //   ⇒ 走反射(与 `pipeline_gate_test.cs:1266` 同法)。
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_loadThrows_reportsFalse_andLeavesNoHalfPreloadedState()
        {
            MethodInfo gated = typeof(DataCorePreloader).GetMethod(
                "TryPreloadGated", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(gated, "`TryPreloadGated` 缺失 —— 可注入核心被删?");

            // ⚠️ **自己复位,不指望全局夹具**:`TryPreloadGated` 首行是幂等短路
            //    (`if (IsPreloaded) return true`),而同一次 Play 运行里前一条测试
            //    (主判据 A)已经把标志置 true ⇒ 不复位时本测会在**短路分支**上返回 true,
            //    看起来像「装载抛被吞了」。2026-11-01 首跑实测即此形态(`Expected: False But was: True`)。
            //    复位后仍断一次前置:证明下面判的是 catch 分支,不是短路分支。
            ResetPreloaderStatics();
            Assert.IsFalse(DataCorePreloader.IsPreloaded,
                "[前置] 静态状态复位失败 ⇒ 本测会走到幂等短路分支,判定无意义");

            try
            {
                bool ok = (bool)gated.Invoke(null, new object[]
                {
                    (Func<bool>)(() => true),
                    (Func<AddressablesDataProvider>)(() => null),   // 本支不靠真 provider:load 会抛
                    (Action<AddressablesDataProvider>)(_ =>
                        throw new InvalidOperationException("[E-13] 模拟装载失败")),
                });

                Assert.IsFalse(ok, "装载抛 ⇒ TryPreload 必须返回 false(不得吞成 true)");
                Assert.IsFalse(DataCorePreloader.IsPreloaded, "装载抛 ⇒ 静态标志保持 false(无半预载态)");

                // 「不降级」的可观察面:取 Provider **抛**,不是返回 null。
                // 若返回 null,启动序会在更远的某处空指针 —— 那是把「启动期硬失败」变成现场崩溃。
                Assert.Throws<InvalidOperationException>(() => { var _ = DataCorePreloader.Provider; },
                    "未预载 ⇒ `Provider` 必须抛(不得返回 null)");
            }
            finally
            {
                ResetPreloaderStatics();
            }
        }

        /// <summary>把 `DataCorePreloader` 的跨轮静态状态清零(与 `PlayModeSetUp` 同一反射调用)。</summary>
        private static void ResetPreloaderStatics()
            => typeof(DataCorePreloader)
                   .GetMethod("ResetForPlayModeTests", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                   ?.Invoke(null, null);
    }
}
