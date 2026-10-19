// PlayMode 全量级夹具 —— 每次 PlayMode 运行前把跨轮静态状态复算一次。
//
// ⚠️ 2026-10-17 立(PlayMode 9 红的第二根因)。病根不是任何一条测试,是**静态状态跨 Play 轮残留**:
//    `DataCorePreloader.IsPreloaded` 在上一轮 PlayMode 被置 true,退出 Play 后**不复位**;
//    本轮 `TryPreload()` 于是短路返回 true,而 `_provider` 持有的 Addressables 句柄已随上一轮失效
//    ⇒ `BootRoot` 第 2 步走「预载成功」分支后取 `DataCorePreloader.Provider`
//    ⇒ `InvalidOperationException: 尚未 Preload()`,Unity Test Framework 判「Unhandled log message」
//    ⇒ 全部 Boot 启动序测试红。`AudioSettingsRegistry.Provider` 同属跨轮残留,一并清。
//
// 形态说明:`[UnitySetUp]` **没有作用域限定到每条测试** —— 凡带它的静态方法,每**条**测试跑前都会执行。
//    本夹具因此只做**幂等**的事(清静态状态),不随测试数放大成本;它**不改变**任何生产判据
//    (预载失败仍是 E-13 硬失败,见 `BootRoot.Start` 第 2 步)。
//
// 判据:删本文件 ⇒ PlayMode 全量须重现 9 红(s46 实测;见 docs/tech-debt-register.md 2026-10-17 段)。
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>PlayMode 全局前置:复算 <see cref="DataCorePreloader"/> 的跨轮静态状态。</summary>
    internal static class PlayModeSetUp
    {
        /// <summary>每条 PlayMode 测试前复算预载状态 —— 见文件头。</summary>
        [UnitySetUp]
        public static void ResetDataCorePreloaderFlag()
        {
            // 反射而非直接引用:`ResetForPlayModeTests` 是 `internal`(Presentation 未对 Gameplay.Tests
            // 开 IVT),门面口径 = 只在这一处出现,命名不符即红(语义同「编译期找不到」)。
            var t = typeof(DaYiJingCheng.Gameplay.Presentation.DataCorePreloader);
            MethodInfo m = t.GetMethod("ResetForPlayModeTests",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(m, "DataCorePreloader.ResetForPlayModeTests 缺失 —— 跨轮静态状态无法复算," +
                                "PlayMode 第二轮起 Boot 启动序会整条红");
            m.Invoke(null, null);
        }
    }
}
