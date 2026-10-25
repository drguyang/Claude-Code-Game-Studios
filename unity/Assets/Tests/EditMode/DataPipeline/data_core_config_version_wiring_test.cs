// 权威来源:
//   `AC-51-D7`(`design/gdd/telemetry-analytics.md:621`,BLOCKING)
//   ADR-014 §五(`ConfigVersion` = 源数据集内容哈希派生,全局单一值)
//   ADR-010 §七(存档头与当前产物比对;**不匹配非致命**)
//   ADR-019 §五(开发者调试视图 = 本地汇总,无玩家可见 UI)
//   `TD-epic-01-f`(本件验其解除前置 ②③,即**接线**半边)
//
// ═══════════════════════════════════════════════════════════════════════════════
// 为什么必须另立本文件(2026-10-24 `TD-epic-01-f` 修复轮)
// ═══════════════════════════════════════════════════════════════════════════════
// 债册 `TD-epic-01-f` §五 第 3 条逐字:
//   「补一条**端到端**夹具 —— 走 `TelemetryDeveloperView.Render`(而非直接
//     `new ConfigVersionCheck`),断言不匹配时报告不含指标行。
//     ⚠️ 现有两条夹具直接构造 `ConfigVersionCheck`,**绕过了整条接线**
//     ⇒ 它们对 ① ② ③ 三处断点**全部无判别力**。」
//
// 本文件就是那条夹具,外加三处断点的逐点守卫:
//   · 断点①(`ConfigVersionCheck` 生产侧零构造)⇒ `test_wiring_*_mismatch_flag`
//   · 断点②(开发视图调用时不传 configVersion)⇒ 同一条(它走 `Render` 全链)
//   · 断点③(装配根写死 0U)⇒ `test_wiring_currentConfigVersion_isDerivedNotZero`
//
// ⚠️ **本文件跑真产物**:`byteSource` 读 `Assets/DataCooked/` 的 22 份**真** cooked 字节。
//   理由:全局 `ConfigVersion` 是 22 份产物头的组合,用合成头也能测,但那样
//   「22 条地址真的都被取到」就没有判据了(合成头可以让任何实现都对)。
//   盘上产物缺失 ⇒ 本文件红并提示重烘(与 `clinic_machine_cooked_codec_test.cs` 同款)。
//
// ⚠️ 测试纪律:无随机 · 无时间依赖 · 零写盘(只读 DataCooked)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Tests.DiseaseSimulation;   // SyntheticDiseaseFixture
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.DataPipeline
{
    [TestFixture]
    internal sealed class DataCoreConfigVersionWiringTest
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        private static string CookedDir
            => Path.Combine(RepoRoot, "unity", "Assets", "DataCooked");

        // ══════════════ 断点③:装配根真的派生,不是写死 0 ══════════════

        /// <summary>**装配根把真派生值喂给 51** —— `CurrentConfigVersion` 非 null 且 ≠ 0。
        /// <para><b>生产主语</b>:`CompositionRoot.AssembleCore` 构造 `CompositionRootServices` 时
        /// 传的 <c>() =&gt; ResolveGlobalConfigVersion(dataProvider)</c> 委托。</para>
        /// <para><b>判别力</b>:把该实参删掉(回到 `saveService` 就结束)⇒ 委托为 null ⇒
        /// 第一条断言红;把委托改成 <c>() =&gt; 0U</c>(断点③ 的原形态)⇒ 第二条断言红。</para>
        /// <para><b>为什么这条能抓住断点③</b>:断点③ 的实测形态是
        /// `CompositionRoot.cs:415` = `configVersion: 0U` —— 一个**常量**。真派生值是 22 份
        /// 真产物头的 FNV 组合,落在 0 上的概率是 2⁻³²;断言「≠ 0」足以区分。</para></summary>
        [Test]
        public void test_wiring_currentConfigVersion_isDerivedNotZero()
        {
            CompositionRootServices bag = AssembleWithRealProducts();

            Assert.That(bag.Telemetry.CurrentConfigVersion, Is.Not.Null,
                "装配根必须把全局 ConfigVersion 的提供者喂给 51 —— " +
                "为 null ⇒ 开发者预览永不做版本门(TD-epic-01-f 断点① 原样保留)");

            uint? current = bag.Telemetry.CurrentConfigVersion();
            Assert.That(current.HasValue, Is.True, "提供者须给出值,不得返回 null");
            Assert.That(current.Value, Is.Not.Zero,
                "全局 ConfigVersion 不得为 0 —— 0 是断点③ 的写死常量形态");
        }

        /// <summary>**派生值 = 22 份真产物头的组合**(不是取某一份,也不是别的什么数)。
        /// <para><b>判别力</b>:把 `LoadAllCookedHeaders` 改成只取第一条地址 ⇒ 本测红
        /// (期望值是 22 条的组合,单条的组合必然不同)。</para>
        /// <para>这条同时钉住「覆盖全部产物」—— `TD-epic-01-f` 明确否决了
        /// 「取单一主数据集」(那会让其余 21 份变了存档头不变,AC-51-D7 只盖住 1/22)。</para></summary>
        [Test]
        public void test_wiring_derivedValue_equalsAllProductsCombination()
        {
            CompositionRootServices bag = AssembleWithRealProducts();

            // 独立复算:自己按地址表读头、自己调 Combine —— 不经过 provider 的实现。
            var headers = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (string address in DataCoreAddresses())
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(CookedDir, address));
                var head = new byte[CookedFormat.HeaderSize];
                Array.Copy(bytes, head, CookedFormat.HeaderSize);
                headers[address] = head;
            }
            uint expected = DataCoreConfigVersion.Combine(headers);

            Assert.That(bag.Telemetry.CurrentConfigVersion().Value, Is.EqualTo(expected),
                "装配根给出的全局 ConfigVersion 须等于「全部产物头的组合」");
        }

        // ══════════════ 断点①②:端到端走 Render,不匹配 ⇒ 不出指标 ══════════════

        /// <summary>**端到端(债册 §五 第 3 条点名的那条)**:走 `TelemetryDeveloperView.Render`
        /// 全链,存档头版本错 ⇒ 报告含标注、**不含任何指标行**。
        /// <para><b>生产主语</b>:`TelemetryDeveloperView.Render:90` 把
        /// <c>r.ConfigVersion</c> 传给 `AllMetricsReport`;后者的版本门早退。</para>
        /// <para><b>判别力</b>:
        /// ① 把 `Render` 的调用改回 `AllMetricsReport(out int skipped)`(断点② 原形态)⇒
        ///    报告里出现 F1 指标行 ⇒ 第三条断言红;
        /// ② 把装配根的委托删掉 ⇒ 版本门不构造 ⇒ 同样红;
        /// ③ 把 `AllMetricsReport` 的版本门早退删掉 ⇒ 红。</para>
        /// <para>⚠️ 与既有两条夹具(`blank_and_degraded_test.cs:384` / `:408`)的区别:
        /// 那两条**直接 new `ConfigVersionCheck`** 传给 `AllMetricsReport`,跳过了
        /// `Render` 与装配根两段 ⇒ 对断点①②③ 零判别力(债册 §五 第 3 条已点名)。</para></summary>
        [Test]
        public void test_wiring_endToEnd_mismatch_flagsAndNoMetrics()
        {
            CompositionRootServices bag = AssembleWithRealProducts();
            uint current = bag.Telemetry.CurrentConfigVersion().Value;
            uint staleHeader = current ^ 0xFFFFFFFFu;   // 保证 ≠ current

            var request = new TelemetryPreviewRequest(
                skill: null, growthWindow: null, firstWindowTick: null,
                step: null, window: null, gapThreshold: null, minSample: null,
                configVersion: new DaYiJingCheng.Telemetry.ConfigVersionCheck(
                    saveHeaderConfigVersion: staleHeader, currentConfigVersion: current));

            string report = TelemetryDeveloperView.Render(bag.Telemetry, request);

            Assert.That(report, Does.Contain("版本不匹配"),
                "版本不符 ⇒ 报告须含显式标注(AC-51-D7)");
            Assert.That(report, Does.Contain(staleHeader.ToString()),
                "须点出存档头版本号");
            Assert.That(report, Does.Contain(current.ToString()),
                "须点出当前产物版本号");
            Assert.That(report, Does.Not.Contain("F1a"),
                "版本不符 ⇒ **不静默出数**:报告不得含任何指标行");
        }

        /// <summary>**端到端正向对照**:存档头版本对 ⇒ 出正常指标、无「版本不匹配」标注。
        /// <para><b>判别力</b>:把版本门写成恒拒 ⇒ 第一条断言红。</para>
        /// <para>这条防的是「为了让它不匹配而把门写成恒不匹配」—— 那是反向的假绿。</para></summary>
        [Test]
        public void test_wiring_endToEnd_match_noBannerAndMetricsPresent()
        {
            CompositionRootServices bag = AssembleWithRealProducts();
            uint current = bag.Telemetry.CurrentConfigVersion().Value;

            var request = new TelemetryPreviewRequest(
                skill: null, growthWindow: null, firstWindowTick: null,
                step: null, window: null, gapThreshold: null, minSample: null,
                configVersion: new DaYiJingCheng.Telemetry.ConfigVersionCheck(
                    saveHeaderConfigVersion: current, currentConfigVersion: current));

            string report = TelemetryDeveloperView.Render(bag.Telemetry, request);

            Assert.That(report, Does.Not.Contain("版本不匹配"), "版本一致 ⇒ 不得出该标注");
            Assert.That(report, Does.Contain("F1"), "版本一致 ⇒ 正常出指标");
        }

        /// <summary>**`RenderDeveloperSummary` 的版本门也真的接上了**(另一条消费入口)。
        /// <para>债册 §五 第 2 条要求「消费端把 `ConfigVersionCheck` 传进去」。
        /// `RenderDeveloperSummary` 是「只收 sink」的默认预览入口,它自己从
        /// `CurrentConfigVersion` 构造门 —— 若那条路断了,只有显式 `Render` 能用,
        /// 默认入口仍然静默出数。</para>
        /// <para><b>判别力</b>:删掉 `RenderDeveloperSummary` 里构造 `versionGate` 的整段 ⇒ 红。</para></summary>
        [Test]
        public void test_wiring_renderDeveloperSummary_gatesOnInjectedCurrent()
        {
            CompositionRootServices bag = AssembleWithRealProducts();
            uint current = bag.Telemetry.CurrentConfigVersion().Value;
            var sink = new RecordingSink();

            string report = bag.Telemetry.RenderDeveloperSummary(
                sink, saveHeaderConfigVersion: current ^ 0xFFFFFFFFu);

            Assert.That(report, Does.Contain("版本不匹配"),
                "默认预览入口也须走版本门(否则它仍静默出数)");
            Assert.That(report, Does.Not.Contain("F1a"), "不匹配 ⇒ 不出指标");
            Assert.That(sink.LogCount, Is.EqualTo(1), "正文须落 sink");
        }

        // ══════════════ 惰性派生:装配期不得因 provider 缺失而抛 ══════════════

        /// <summary>**`dataProvider == null` + `saveBasePath` 非空 ⇒ 装配仍然成功**。
        /// <para>这是「惰性派生」的守卫 —— 装配期不解析版本号,只在 `compose` 被调用时解析。</para>
        /// <para><b>为什么必须是惰性</b>:既有两条夹具
        /// (`save_composer_wiring_test.cs:341` / `:370`)传 `saveBasePath` 但 `dataProvider: null`,
        /// 它们验的是折叠谓词接线,与版本号无关。装配期就抛会让它们因**不相干的**理由转红
        /// (= 修复引入的假红)。</para>
        /// <para><b>判别力</b>:把派生从闭包内挪回装配期(即 `uint cv = ResolveGlobalConfigVersion(dataProvider);`
        /// 放在 `if` 里第一行)⇒ 本测在 `Assemble` 处抛 ⇒ 红。</para>
        /// <para>⚠️ **本测不验「调用时必抛」** ~~—— `Checkpoint` 在
        /// `RequireNarrativeContext()` 就先抛了(narrativeContext 今日恒 null,TD-persist-04),
        /// 够不到 `compose`。那条判据今日**不可达**,如实登记为 NOT-RUN,不用别的断言冒充~~
        /// (**2026-10-25 生产接线轮过期**,原文不删):瞬时源已接通
        /// (`NoPendingMedicalActionQuery`)⇒ `Checkpoint` **够得到** `compose` ——
        /// 「provider 缺失 ⇒ 调用时抛 E-13」自此可达;该判据由
        /// `save_composer_wiring_test` / `composition_root_test` 的 ⑤ 翻转件承载。</para></summary>
        [Test]
        public void test_lazy_noProviderWithSavePath_assembleStillSucceeds()
        {
            string dir = Path.Combine(Path.GetTempPath(),
                "dyjq_tdef01f_lazy_" + Guid.NewGuid().ToString("N"));
            try
            {
                Assert.DoesNotThrow(
                    () => CompositionRoot.Assemble(new[] { SyntheticDiseaseFixture.Create() },
                                                  saveBasePath: dir),
                    "装配期不得解析 ConfigVersion(惰性)—— 否则 provider 缺失的既有夹具全部假红");
            }
            finally
            {
                if (Directory.Exists(dir))
                {
                    try { Directory.Delete(dir, true); } catch { }
                }
            }
        }

        // ══════════════ 工具 ══════════════

        /// <summary>用**真 DataCooked 字节**装配组合根(provider 走 AddressablesDataProvider 的
        /// 内部字节源构造缝 —— 与 `clinic_machine_cooked_codec_test.cs:387` 同一手法)。</summary>
        private static CompositionRootServices AssembleWithRealProducts()
        {
            AddressablesDataProvider provider = MakeProvider(address =>
            {
                string path = Path.Combine(CookedDir, address);
                Assert.That(File.Exists(path), Is.True,
                    $"盘上产物缺失:{path} —— 重烘:「大医精诚/数据管线」全量烘焙");
                return File.ReadAllBytes(path);
            });

            return CompositionRoot.Assemble(new[] { SyntheticDiseaseFixture.Create() },
                                            dataProvider: provider);
        }

        /// <summary>经内部构造缝造 provider(internal ⇒ 反射;先例见
        /// `clinic_machine_cooked_codec_test.cs:387`)。</summary>
        private static AddressablesDataProvider MakeProvider(Func<string, byte[]> byteSource)
        {
            ConstructorInfo ctor = typeof(AddressablesDataProvider).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(Func<string, byte[]>) },
                modifiers: null);

            Assert.That(ctor, Is.Not.Null,
                "`AddressablesDataProvider(Func<string, byte[]>)` 内部构造不见了 —— 装载面夹具的注入缝被移除");

            return (AddressablesDataProvider)ctor.Invoke(new object[] { byteSource });
        }

        /// <summary>取权威地址表(`DataCorePreloader.DataCoreAddresses` 是 internal ⇒ 反射)。
        /// <para>⚠️ 刻意**反射读生产那张表**,不在测试里手抄 22 条 —— 手抄 = 第二份定义,
        /// 接线轮次追加产物时测试不会红(而生产会变),正是该表注释记了 10 次的历史教训。</para></summary>
        private static string[] DataCoreAddresses()
        {
            FieldInfo f = typeof(DataCorePreloader).GetField(
                "DataCoreAddresses", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(f, Is.Not.Null,
                "`DataCorePreloader.DataCoreAddresses` 不见了 —— 地址表是装配与预载的单一出处");
            var addresses = (string[])f.GetValue(null);
            Assert.That(addresses, Is.Not.Empty, "地址表不得为空");
            return addresses;
        }

        private sealed class RecordingSink : DaYiJingCheng.Telemetry.ITelemetrySink
        {
            public int WriteCount;
            public int LogCount;
            public void WriteLocalReport(string fileName, string content) => WriteCount++;
            public void LogDeveloperSummary(string summary) => LogCount++;
        }
    }
}
