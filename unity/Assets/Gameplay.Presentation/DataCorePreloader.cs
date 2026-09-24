// 权威来源:ADR-014 §五(data-core 组 + **首次 Step 前**启动预载)· ADR-023(Boot 场景承载启动序)
//          · Story 008(TR-itemdb-026:预载时机契约)
//
// ⚠️ 调用点 = Boot 场景启动序(ADR-023 三场景制)—— Boot 场景落地属后续故事,
//    本类型先立契约:任何 Step 开始前必须完成 Preload(),否则 E-13 启动期硬失败会先触发。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>data-core 启动预载的静态入口(幂等)。
    /// <para><b>调用契约</b>:必须在 sim 首次 <c>Step</c> 之前调用一次 —— 调用点住 Boot 场景
    /// 启动序(ADR-023),尚不存在;落地前任何提前 Step 都是调用方违约。</para>
    /// <para>失败 = E-13 启动期硬失败(经 <see cref="AddressablesDataProvider"/> 抛出),
    /// 绝不静默继续。</para>
    /// <example><code>DataCorePreloader.Preload();          // Boot 启动序,Step 之前
    /// var items = DataCorePreloader.Provider.Load&lt;ItemDataSet&gt;();</code></example>
    /// </summary>
    public static class DataCorePreloader
    {
        private static AddressablesDataProvider _provider;

        /// <summary>已预载的 provider(未预载即访问 = 抛,防「先取数后预载」的时序错)。</summary>
        public static IDataProvider Provider =>
            _provider ?? throw new InvalidOperationException(
                "DataCorePreloader 尚未 Preload() —— 契约:首次 Step 之前必须完成 data-core 预载(ADR-014 §五)");

        /// <summary>是否已预载(幂等标志;单线程启动序使用,不加锁 —— Boot 主线程调用)。</summary>
        public static bool IsPreloaded { get; private set; }

        /// <summary>预载 data-core 全部产物(物品表 + 配方/常量表;幂等)。
        /// <para>必须在首次 Step 之前调用;失败即 [E-13] 启动期硬失败,不返回。</para></summary>
        /// <exception cref="InvalidOperationException">Addressables 装载 / cooked 解码失败。</exception>
        public static void Preload()
        {
            if (IsPreloaded) return;
            _provider = new AddressablesDataProvider();
            _provider.Preload(); // 失败 ⇒ [E-13] InvalidOperationException,启动终止
            IsPreloaded = true;
        }
    }
}
