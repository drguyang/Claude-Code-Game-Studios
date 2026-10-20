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
using System.Collections.Generic;
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

        /// <summary>幂等标志的运行时复算 —— <b>只给 PlayMode 测试夹具用</b>(<c>Tests/PlayMode/PlayModeSetUp.cs</c> 的
        /// <c>[UnitySetUp]</c> 经反射调用),生产零调用点。
        /// <para><b>为什么需要(2026-10-17 实测)</b>:静态状态跨 Play 轮残留 —— ①<see cref="IsPreloaded"/>
        /// 被上一轮置 true ⇒ 本轮 <c>TryPreload()</c> 短路返回 true;②上一轮 <c>_provider</c> 持有的 Addressables
        /// 句柄随 Play 退出失效。两者叠加的结果是 <c>BootRoot</c> 第 2 步走「预载成功」分支、再取
        /// <see cref="Provider"/> ⇒ <c>InvalidOperationException</c>(PlayMode 9 红之一,2026-10-17 实测)。
        /// 连同 <see cref="AudioSettingsRegistry.Provider"/> 一并清空 —— 它同样跨轮残留。
        /// <para>⚠️ <b>2026-10-20 增列</b> <see cref="Interaction.KindPriorityTable"/>:读方接线后它同样
        /// 持有静态 <c>Provider</c> ⇒ 跨轮残留形态与前两者一致。置 <c>null</c> 即让已建表失效
        /// (该表的缓存按 Provider 委托身份判定,**无需**额外的复位 API)。</para>
        /// <para>production 路径永远不调本方法 ⇒ 判据不弱化(失败仍是 E-13 硬失败)。</para></summary>
        internal static void ResetForPlayModeTests()
        {
            _provider = null;
            IsPreloaded = false;
            Settings.AudioSettingsRegistry.Provider = null;
            Interaction.KindPriorityTable.Provider = null;
        }

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
        /// 日志断言);再走生产同款 <see cref="Preload"/>。失败(预检不过 / 装载抛)⇒ false。</para>
        /// <para>⚠️ <b>2026-10-17 订正 —— 预检必须逐条全检,不得只看一条「代表」</b>:
        /// 原口径「组条目要么全在要么全不在(data-core 一次入组)」<b>已被实测推翻</b> ——
        /// 组条目会**分多批**入组(8 侧 → 53 侧 → S14-6 的 44 侧两表),任意一次 catalog 重建
        /// 漏掉后加入的那批,预检仍过、装载中途抛 <c>InvalidKeyException</c>,该条 **[Error] 已打出**,
        /// 而 <c>TryPreload</c> 又吞掉异常返回 false ⇒ 调用方走磁盘兜底继续启动,
        /// Unity Test Framework 把这条 Error 判为「Unhandled log message」⇒ PlayMode 启动序整条红
        /// (2026-10-17 实测 9 条)。全检 = 在**任何**装载之前判死,零 Error 日志。
        /// 代价 = 与地址表条数等量的反射调用(2026-10-20 起 **15** 次),启动期一次,可忽略。</para>
        /// <para>⚠️ <b>2026-10-17 修复 —— 预检不只是闸,还要<b>记下缺失项并复位静态状态</b></b>:
        /// 原实现在失败分支直接 <c>return false</c>,既不留缺失诊断,也不碰 <c>_provider</c>/<c>IsPreloaded</c>
        /// ⇒ ① 调用方无从知道「缺哪条」;② 域重载后残留的旧 provider(句柄已失效)继续以 <c>IsPreloaded=true</c>
        /// 对外可见。现口径:失败 ⇒ 写 <see cref="LastPreloadDiagnostic"/> + 清静态状态 + false;
        /// 成功 ⇒ 诊断置「就绪」。诊断只增读面,不改变失败语义(E-13 / 磁盘兜底照旧)。</para></summary>
        public static bool TryPreload() =>
            TryPreloadGated(
                precheck: () => AllDataCoreKeysLocatable(DataCoreAddresses),
                factory: () => new AddressablesDataProvider(),
                load: provider => provider.Preload());

        /// <summary>预载的<b>可注入核心</b>(生产三缝 = 生产谓词 / <see cref="AddressablesDataProvider"/> / 其
        /// <c>Preload</c>;测试全部替换为探针)。抽出的唯一目的 = 让「预检不过 ⇒ <b>不构造、不装载</b>」
        /// 成为可执行判据 —— 该条文的行为半边在真实环境里不可观察(缺条目时短路本身不留下痕迹),
        /// <c>TD-audio-01f</c> M3 因此长期 NOT-RUN(详见 `docs/tech-debt-register.md` 2026-10-17 段)。
        /// <para>失败分支统一 ⇒ 复位静态状态 + 写诊断 + false;成功 ⇒ 标志 true + 诊断「无缺失」。</para></summary>
        /// <param name="precheck">预检谓词(false ⇒ 立即短路,<paramref name="factory"/> / <paramref name="load"/> 均不调用)。</param>
        /// <param name="factory">构造 provider(false ⇒ 不调用)。</param>
        /// <param name="load">执行装载(false ⇒ 不调用;抛 ⇒ 视为失败)。</param>
        internal static bool TryPreloadGated(
            Func<bool> precheck, Func<AddressablesDataProvider> factory, Action<AddressablesDataProvider> load)
        {
            // 幂等短路**须留诊断** —— 否则二次调用时无从回答「缺哪条」。
            if (IsPreloaded)
            {
                LastPreloadDiagnostic = DiagnoseDataCoreKeys(DataCoreAddresses, null);
                return true;
            }

            if (!precheck())
                return FailPreload();

            try
            {
                AddressablesDataProvider provider = factory();
                load(provider);
                _provider = provider;
                IsPreloaded = true;
                LastPreloadDiagnostic = DiagnoseDataCoreKeys(DataCoreAddresses, null);
                return true;
            }
            catch (Exception)
            {
                // 预检全过却装载失败 = 运行时阶段失败(解码 / 反射),不是缺地址 —— 诊断如实说「无缺失项」。
                return FailPreload();
            }
        }

        /// <summary>失败收口:复位静态状态 + 诊断 + false(单一出处,禁各分支另写)。</summary>
        private static bool FailPreload()
        {
            ResetForPlayModeTests();
            LastPreloadDiagnostic = DiagnoseDataCoreKeys(DataCoreAddresses, null);
            return false;
        }

        /// <summary>最近一次 <see cref="TryPreload"/> 的就绪诊断(只读;未调用过 = <c>null</c>)。
        /// <para>形态 = 字母序的 data-core 地址列表:`"missing=[]"` 表示全部可解析(或预载成功);
        /// `"missing=[a, b]"` 列出**不可解析**的地址。用途 = 让 PlayMode 侧有一个**可断言**的观察点,
        /// 替代「去数 [Error] 日志条数」这种与日志格式耦合的判据。</para></summary>
        public static string LastPreloadDiagnostic { get; private set; }

        /// <summary>纯谓词:按 <paramref name="locatable"/> 计算缺失项诊断串(可注入 ⇒ EditMode 可直驱);
        /// <paramref name="locatable"/> 为 null 时用生产谓词 <see cref="KeyLocatable"/>。</summary>
        /// <param name="addresses">待检查的地址集。</param>
        /// <param name="locatable">地址可解析判定(null ⇒ <see cref="KeyLocatable"/>)。</param>
        internal static string DiagnoseDataCoreKeys(IReadOnlyList<string> addresses, Func<string, bool> locatable)
        {
            Func<string, bool> probe = locatable ?? KeyLocatable;
            var missing = new List<string>();
            if (addresses != null)
            {
                foreach (string address in addresses)
                {
                    if (address == null) continue;
                    if (!probe(address) && !missing.Contains(address)) missing.Add(address);
                }
            }
            missing.Sort(StringComparer.Ordinal);
            return "missing=[" + string.Join(", ", missing) + "]";
        }

        /// <summary>逐条检查 <paramref name="addresses"/> 全部可解析;任一不可解析 ⇒ false。
        /// <para>与 <c>Preload()</c> 的装载面**一一对应**(同源常量,禁另抄字面量);
        /// 新增 data-core 表时本列表与 <c>Preload()</c> 须同批改。</para></summary>
        internal static bool AllDataCoreKeysLocatable(IReadOnlyList<string> addresses)
        {
            if (addresses == null) return false;
            for (int i = 0; i < addresses.Count; i++)
                if (!KeyLocatable(addresses[i])) return false;
            return true;
        }

        /// <summary>data-core 的全部装载地址 —— 与 <see cref="AddressablesDataProvider.Preload"/>
        /// 的装载面逐一对应(S14-6 追加 44 侧两表后共 7 条;TD-epic-01 第十一片追加设备切换
        /// 常量表后共 8 条;第十二片追加遥测参数旋钮表后共 9 条;2026-10-20 补接线追加
        /// 交互种类契约表 + 阴性把握度定表后共 **11** 条;2026-10-20 第二轮接线追加处方表 / 处置轴 /
        /// 53 判定表 / 53 延迟表后共 **15** 条 —— 与 `Preload()` 的
        /// `Ensure*` 调用数逐一相等,由 `test_preloader_list_sources_matchPreloadedProducts` 钉住)。</summary>
        internal static readonly string[] DataCoreAddresses =
        {
            AddressablesDataProvider.ItemsAddress,
            AddressablesDataProvider.RecipesAddress,
            AddressablesDataProvider.SignTableAddress,
            AddressablesDataProvider.ReadFloorTableAddress,
            AddressablesDataProvider.JudgmentLexiconAddress,
            AddressablesDataProvider.AudioEventsAddress,
            AddressablesDataProvider.AudioSettingsAddress,
            AddressablesDataProvider.DeviceSwitchTuningAddress,
            AddressablesDataProvider.TelemetryAnalyticsAddress,
            AddressablesDataProvider.InteractionKindsAddress,
            AddressablesDataProvider.DiagnosisNegativeConfidenceAddress,
            AddressablesDataProvider.PrescriptionActionsAddress,
            AddressablesDataProvider.DiseaseActionAxisAddress,
            AddressablesDataProvider.OutcomeTableAddress,
            AddressablesDataProvider.DelayTableAddress,
        };

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

    }
}
