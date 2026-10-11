// 权威来源:ADR-014 §五(data-core 组 + **首次 Step 前**启动预载)· ADR-023(Boot 场景承载启动序)
//          · Story 008(TR-itemdb-026:预载时机契约)
//
// ⚠️ 调用点 = Boot 场景启动序(ADR-023 三场景制)—— Boot 场景落地属后续故事,
//    本类型先立契约:任何 Step 开始前必须完成 Preload(),否则 E-13 启动期硬失败会先触发。
//
// ✅ 2026-10-12 C3-B1 修复:生产调用点已在 BootRoot 启动序第 2 步(Assemble 前)接入
//    (bootRoot 装配 += DataCorePreloader.Preload() + provider 传入 Assemble)。
//    本文件头注的「调用点尚不存在」已不再成立 —— 保留历史注记,上方旧注已订正。

using System;
using DaYiJingCheng.Sim.Contracts;
using UnityEngine.AddressableAssets;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>data-core 启动预载的静态入口(幂等)。
    /// <para><b>调用契约</b>:必须在 sim 首次 <c>Step</c> 之前调用一次 —— 调用点住 Boot 场景
    /// 启动序第 2 步(ADR-023 · 2026-10-12 C3-B1 已接:<see cref="DaYiJingCheng.Gameplay.Boot.BootRoot"/>
    /// Assemble 前 Preload + 传 provider);落地前任何提前 Step 都是调用方违约。</para>
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

        /// <summary>
        /// 可回退预载 —— 乙案(Addressables)装载成功返回 true;失败返回 false(不改任何状态)。
        /// <para>2026-10-12 C3-B1 补充:生产构建的 Addressables catalog 含全部 data-core 条目
        /// ⇒ TryPreload 成功走乙案;测试 / catalog 未重建环境(组条目缺)⇒ false,
        /// 由 <see cref="DaYiJingCheng.Gameplay.Boot.CompositionRoot"/> 的 dual-path 回退
        /// 磁盘兜底(同一份 cooked 字节 + 同一 codec —— 仍是真装载,非降级为 null)。
        /// 生产构建若乙案失败,磁盘兜底路径不存在(DataCooked 不进构建)⇒ 启动仍 [E-13]
        /// 硬失败 —— 判据不弱化。</para>
        /// <para>⚠️ 预检 = <see cref="Addressables.ResourceLocators"/> 任一 locator 的
        /// <c>Locate(key, typeof(TextAsset), out _)</c> 命中(先判存在再装载,零 Error 日志
        /// —— LoadAssetAsync 对缺 key 抛 <c>InvalidKeyException</c> 并打 [Error],污染测试
        /// 日志断言);再走生产同款 <see cref="Preload"/>。失败(预检不过 / 装载抛)⇒ false。</para></summary>
        public static bool TryPreload()
        {
            if (IsPreloaded) return true;

            // 预检:任一 locator 能解析 data-core 任一地址(存在性检查,零装载副作用)。
            // ⚠️ 只检 Signs 一条作为代表(catalog 的组条目要么全在要么全不在 ——
            //   data-core 组一次性入组;五条全检 = 浪费五次反射)。若该地址可解析,
            //   Preload 的其余四条地址同组必然也可解析(同一 m_SerializeEntries)。
            if (!KeyLocatable(SignTableAddress))
                return false;

            try
            {
                _provider = new AddressablesDataProvider();
                _provider.Preload();
                IsPreloaded = true;
                return true;
            }
            catch (Exception)
            {
                _provider = null;
                return false;
            }
        }

        /// <summary>地址是否可被 Addressables 解析(遍历 ResourceLocators 的 Locate)。</summary>
        private static bool KeyLocatable(string address)
        {
            try
            {
                foreach (var locator in Addressables.ResourceLocators)
                {
                    if (locator.Locate(address, typeof(UnityEngine.TextAsset), out _))
                        return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
            return false;
        }

        /// <summary>data-core 诊断词条地址(与 <see cref="AddressablesDataProvider"/> 同值)。</summary>
        private const string SignTableAddress = "diagnosis_signs.cooked.bytes";
    }
}
