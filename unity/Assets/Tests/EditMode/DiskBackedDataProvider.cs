// 测试专用的**生产同款** `IDataProvider`:字节取自 `Assets/DataCooked/*.cooked.bytes`。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么需要它(不是「夹具便利」)
// ─────────────────────────────────────────────────────────────────────────────
// 凡是要走 `CompositionRoot.Assemble(saveBasePath: …)` 的夹具,**必须**给 provider:
//   · `BootRoot` 的生产启动序**永远**传 `DataCorePreloader.Provider`;
//   · 不给 ⇒ 组版闭包在 `ResolveConfigVersionSnapshot` 处抛
//     `[E-13][data-core] 无法派生 ConfigVersion 快照:dataProvider 为 null`;
//   · 自动兜底那条还会把该异常降级成 `LogError` ⇒ 表现为「目录空、无声」。
// 也就是说「不给 provider 的袋子」在生产上不存在,拿它测存档等于测一条不存在的装配。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么不能自造一个 `IDataProvider` 实现
// ─────────────────────────────────────────────────────────────────────────────
// `ResolveConfigVersionSnapshot` / `ResolveGlobalConfigVersion` 对 provider 做**类型断言**
// (必须恰是 `AddressablesDataProvider`,因为它持有 22 条产物地址表)。⇒ 自造实现会被拒。
// 本类因此走**同一个类**的可注入构造(`internal AddressablesDataProvider(Func<string,byte[]>)`),
// 只改「字节从哪来」,不改「失败怎么报」——与 `pipeline_gate_test` 反射调
// `DataCorePreloader.TryPreloadGated` 是同一先例。
//
// ⚠️ 读的是**同一份**烘焙产物(已入库的 `Assets/DataCooked/`,零合成、零假表),
//    解码器也是**同一批** `*CookedCodec` ⇒ 无第二真源。

using System;
using System.Reflection;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests
{
    /// <summary>磁盘烘焙产物支撑的 <see cref="IDataProvider"/>(EditMode 专用)。</summary>
    public static class DiskBackedDataProvider
    {
        /// <summary>
        /// 造一个与生产同类型、但字节取自 `Assets/DataCooked/` 的 provider。
        /// <para>缺失产物 ⇒ 抛出(E-13 口径,不返回默认值)。</para>
        /// </summary>
        /// <exception cref="AssertionException">可注入构造不存在(被删 / 改名)—— 夹具须显式失败,
        /// 不许静默退化成「走 Addressables」。</exception>
        public static IDataProvider Create()
        {
            ConstructorInfo ctor = typeof(AddressablesDataProvider).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                new[] { typeof(Func<string, byte[]>) },
                modifiers: null);
            Assert.That(ctor, Is.Not.Null,
                "[夹具红] `AddressablesDataProvider(Func<string, byte[]>)` 可注入构造缺失 —— " +
                "装配根对 provider 有类型断言,自造实现会被拒;构造被删/改名须同步改本件");
            return (IDataProvider)ctor.Invoke(new object[] { (Func<string, byte[]>)DataCookedDisk.ReadAll });
        }
    }
}
