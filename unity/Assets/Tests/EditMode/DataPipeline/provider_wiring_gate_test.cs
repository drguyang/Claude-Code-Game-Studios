// 权威来源:ADR-014 §五(Addressables 单组 `data-core` + 首次 Step 前预载 + E-13 硬失败)
//          · ADR-025 §①(边界程序集 `Gameplay.Presentation` 的引用集)
//          · 先例 `Audio/pipeline_gate_test.cs:test_wiring_addresses_singleSourceOfTruth`
//            (address 常量 ↔ 产物名同源)与 `test_preloader_list_sources_matchPreloadedProducts`
//            (address 常量 ↔ `DataCorePreloader.DataCoreAddresses`)
//
// ⚠️ **本文件补的是「装载面接线」的门** —— 2026-10-20 补烘 6 份欠账后,其中 2 份
//    (`interaction_kinds` / `diagnosis_negative_confidence`)接进了运行期装载面。
//    **补烘 ≠ 接线**:产物字节在盘上,不等于启动时会有人读它。接线由**四处**共同成立,
//    少一处就静默失效,故四处各有一门:
//
//      ① `AddressablesDataProvider` 的 `*Address` 常量 == baker 的产物名;
//      ② `data-core` Addressables 组的条目集 ⊇ 全部 `*Address` 常量
//         (缺条目 ⇒ 启动预载 `FetchBytes` 直接 E-13 硬失败);
//      ③ 组条目 → `DataCooked/` 真文件 + GUID 与 `.meta` 一致
//         (手改 YAML 打错 GUID ⇒ 条目指向虚空,`m_Address` 查不到);
//      ④ `Preload()` 的 `Ensure*` 调用集 == `Ensure*` 方法集 == 地址表集
//         (方法写了但没接进 `Preload` ⇒ 缓存永不填充,`Load<T>` 在首次调用时才炸)。
//
// ⚠️ **不查什么**:装载的**真字节解码**归 `PlayMode`(EditMode 无 Addressables locator);
//    本文件只钉「分支存在且被调用」——`Load<T>` 的判别用**异常类型/报文**区分
//    「走到装载」与「走到未知类型兜底」,不依赖装载成功。
//
// ⚠️ 零 Addressables API 引用:组文件按 YAML 文本读,地址由反射取自数据源(禁另抄字面量)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.Unit.DataPipeline
{
    /// <summary>
    /// <b>data-core 装载面接线门</b> —— 产物名 / address 常量 / Addressables 组 / <c>Preload()</c>
    /// 四处的集合必须闭合。
    /// <para>⚠️ 每个断言都指向一个**具体失效模式**(见文件头 ①②③④),不是「跑通即绿」。</para>
    /// </summary>
    [TestFixture]
    public class ProviderWiringGateTest
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        /// <summary>盘上产物目录(<c>unity/Assets/DataCooked/</c>)。</summary>
        private static string CookedDir => Path.Combine(RepoRoot, "unity", "Assets", "DataCooked");

        /// <summary>data-core 组定义(作者态,**已入库**)。</summary>
        private static string GroupAssetPath
            => Path.Combine(RepoRoot, "unity", "Assets", "AddressableAssetsData",
                            "AssetGroups", "data-core.asset");

        /// <summary>provider 源码(供 ④ 的 `Preload()` 调用面静态扫描)。</summary>
        private static string ProviderSourcePath
            => Path.Combine(RepoRoot, "unity", "Assets", "Gameplay.Presentation",
                            "AddressablesDataProvider.cs");

        // ══════════════════ 数据源(反射 / 文本,禁测试内另抄)══════════════════

        /// <summary>provider 的全部公开 <c>*Address</c> 字符串常量(运行期装载面的唯一声明处)。</summary>
        private static string[] ProviderAddressConstants()
            => typeof(AddressablesDataProvider)
                .GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string)
                            && f.Name.EndsWith("Address", StringComparison.Ordinal))
                .Select(f => (string)f.GetRawConstantValue())
                .ToArray();

        /// <summary>`DataCorePreloader.DataCoreAddresses`(预载预检的地址表)。</summary>
        private static string[] PreloaderAddressTable()
        {
            var field = typeof(DataCorePreloader).GetField("DataCoreAddresses",
                System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public);
            Assert.That(field, Is.Not.Null, "DataCorePreloader.DataCoreAddresses 缺失 —— 预检面被删?");
            Object raw = field.GetValue(null);
            Assert.That(raw, Is.Not.Null, "地址表为 null");
            return (string[])raw;
        }

        /// <summary>provider 上全部私有 <c>Ensure*</c> 方法名(每个地址一个装载入口)。</summary>
        private static string[] EnsureMethodNames()
            => typeof(AddressablesDataProvider)
                .GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.DeclaredOnly)
                .Where(m => m.Name.StartsWith("Ensure", StringComparison.Ordinal) && m.GetParameters().Length == 0)
                .Select(m => m.Name)
                .ToArray();

        /// <summary>解析 `data-core.asset` 的条目:每条形如 `- m_GUID: …` 紧跟 `m_Address: …`。</summary>
        private static List<(string Guid, string Address)> ReadDataCoreEntries()
        {
            Assert.That(File.Exists(GroupAssetPath), Is.True,
                $"data-core 组定义不存在:{GroupAssetPath}(组定义是作者态,须入库)");
            string[] lines = File.ReadAllLines(GroupAssetPath);

            var entries = new List<(string, string)>();
            string pendingGuid = null;
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                Match g = Regex.Match(line, @"^-\s*m_GUID:\s*([0-9a-fA-F]{32})\s*$");
                if (g.Success) { pendingGuid = g.Groups[1].Value.ToLowerInvariant(); continue; }
                Match a = Regex.Match(line, @"^m_Address:\s*(\S+)\s*$");
                if (a.Success && pendingGuid != null)
                {
                    entries.Add((pendingGuid, a.Groups[1].Value));
                    pendingGuid = null;
                }
            }
            return entries;
        }

        // ══════════════════ ① address 常量 == baker 产物名 ══════════════════

        /// <summary>2026-10-20 新接线的六份(路 A 两份 + 第二轮四份):运行期 address 字面串必须与
        /// baker 声明**逐字相等**。
        /// <para><b>判别力</b>:任一侧改一个字符 ⇒ 本测红。这正是「改了产物名却忘了改装载面」的形态。</para></summary>
        [Test]
        public void test_wiring_newArtifactAddresses_matchBakerArtifactNames()
        {
            Assert.That(AddressablesDataProvider.InteractionKindsAddress,
                Is.EqualTo(DataBakeMenu.InteractionKindsCookedAssetName),
                "交互种类契约表的 address 与 baker 产物名不一致 ⇒ 启动预载查不到该文件(E-13 硬失败)");
            Assert.That(AddressablesDataProvider.DiagnosisNegativeConfidenceAddress,
                Is.EqualTo(DataBakeMenu.DiagnosisNegativeConfidenceCookedAssetName),
                "阴性把握度定表的 address 与 baker 产物名不一致 ⇒ 同上");
            Assert.That(AddressablesDataProvider.PrescriptionActionsAddress,
                Is.EqualTo(DataBakeMenu.PrescriptionActionsCookedAssetName),
                "处方表的 address 与 baker 产物名不一致 ⇒ 同上");
            Assert.That(AddressablesDataProvider.DiseaseActionAxisAddress,
                Is.EqualTo(DataBakeMenu.DiseaseActionAxisCookedAssetName),
                "处置轴契约表的 address 与 baker 产物名不一致 ⇒ 同上");
            Assert.That(AddressablesDataProvider.OutcomeTableAddress,
                Is.EqualTo(DataBakeMenu.OutcomeTableCookedAssetName),
                "53 判定表的 address 与 baker 产物名不一致 ⇒ 同上");
            Assert.That(AddressablesDataProvider.DelayTableAddress,
                Is.EqualTo(DataBakeMenu.DelayTableCookedAssetName),
                "53 延迟表的 address 与 baker 产物名不一致 ⇒ 同上");
        }

        // ══════════════════ ② data-core 组 ⊇ 全部 address 常量 ══════════════════

        /// <summary>每一个 <c>*Address</c> 常量都必须是 <c>data-core</c> 组里**恰一条**条目。
        /// <para><b>为什么必须有</b>:`FetchBytes` 走 `Addressables.LoadAssetAsync(address)`;
        /// 组里没这条 ⇒ 启动期 E-13 硬失败(整链起不来),而**其它测试全绿**(它们不碰 Addressables)。
        /// <para><b>判别力</b>:从组文件删掉任一 `- m_GUID/m_Address` 块 ⇒ 该地址「恰 1 条」红;
        /// 同一地址写两条 ⇒ 重复红。</para></summary>
        [Test]
        public void test_wiring_dataCoreGroup_containsEveryProviderAddress()
        {
            string[] sources = ProviderAddressConstants();
            Assert.That(sources.Length, Is.GreaterThanOrEqualTo(15),
                "扫描键守卫 —— 取不到 address 常量则下面的逐条断言会被「什么都不查」满足");

            List<(string Guid, string Address)> entries = ReadDataCoreEntries();
            Assert.That(entries.Count, Is.GreaterThanOrEqualTo(sources.Length),
                $"data-core 组条目数 {entries.Count} 少于 address 常量数 {sources.Length}");

            foreach (string address in sources)
            {
                Assert.That(entries.Count(e => e.Address == address), Is.EqualTo(1),
                    $"address「{address}」在 data-core 组中应恰有 1 条条目" +
                    "(缺 ⇒ 启动预载 E-13 硬失败;重 ⇒ 组定义自相矛盾)");
            }
            Assert.That(entries.Select(e => e.Address).Distinct().Count(), Is.EqualTo(entries.Count),
                "data-core 组存在重复 address");
        }

        // ══════════════════ ③ 组条目 → 真文件 + GUID 一致 ══════════════════

        /// <summary>组里每一条 address 都必须在 <c>DataCooked/</c> 有真文件。</summary>
        [Test]
        public void test_wiring_dataCoreGroup_entriesResolveToExistingArtifacts()
        {
            List<(string Guid, string Address)> entries = ReadDataCoreEntries();
            Assert.That(entries.Count, Is.GreaterThan(0), "扫描键守卫 —— 组里零条目则本测恒真");

            foreach ((string _, string address) in entries)
            {
                string path = Path.Combine(CookedDir, address);
                Assert.That(File.Exists(path), Is.True,
                    $"组条目「{address}」指向的产物不在盘上:{path}(组 ↔ 盘 脱节 ⇒ 打包期才炸)");
            }
        }

        /// <summary>组条目的 <c>m_GUID</c> 必须等于产物 <c>.meta</c> 里登记的 GUID。
        /// <para><b>为什么单列</b>:组文件是**手可编辑的 YAML**。改 address 而抄错/漏改 GUID,
        /// YAML 依然合法、address 依然对得上,但条目指向虚空 —— 属**静默**失效,
        /// 只有打包时才以「找不到资产」暴露。</para></summary>
        [Test]
        public void test_wiring_dataCoreGroup_guidsMatchArtifactMeta()
        {
            List<(string Guid, string Address)> entries = ReadDataCoreEntries();
            Assert.That(entries.Count, Is.GreaterThan(0), "扫描键守卫");

            foreach ((string guid, string address) in entries)
            {
                string metaPath = Path.Combine(CookedDir, address + ".meta");
                Assert.That(File.Exists(metaPath), Is.True, $"产物 .meta 缺失:{metaPath}");

                Match m = Regex.Match(File.ReadAllText(metaPath), @"^guid:\s*([0-9a-fA-F]{32})",
                    RegexOptions.Multiline);
                Assert.That(m.Success, Is.True, $"{metaPath} 内无 guid 行");
                Assert.That(m.Groups[1].Value.ToLowerInvariant(), Is.EqualTo(guid),
                    $"组条目「{address}」的 GUID 与 .meta 不一致 ⇒ 条目指向虚空(打包期才暴露)");
            }
        }

        // ══════════════════ ④ Preload() 覆盖全部 Ensure* ══════════════════

        /// <summary>`Ensure*` 方法集 == `Preload()` 的调用集 == 地址表集 —— 三者逐一闭合。
        /// <para><b>病根</b>:`EnsureXxx()` 写完却忘了加进 `Preload()` 时,方法存在、编译通过、
        /// 甚至「Ensure 计数」类断言也绿,但缓存**永不填充**;首次 `Load<XxxDataSet>()`
        /// 在别处才炸,且现场远离病根。</para>
        /// <para><b>判别力</b>:从 `Preload()` 删掉任一 `Ensure*();` 调用 ⇒ 本测红(调用面扫描);
        /// 新增 `Ensure*` 方法而不进 `Preload` ⇒ 亦红。</para></summary>
        [Test]
        public void test_wiring_preload_invokesEveryEnsureMethod()
        {
            string[] ensures = EnsureMethodNames();
            Assert.That(ensures.Length, Is.GreaterThanOrEqualTo(15),
                "扫描键守卫 —— 取不到 Ensure* 方法则下面的覆盖断言会被「什么都不查」满足");

            Assert.That(File.Exists(ProviderSourcePath), Is.True, $"provider 源码缺失:{ProviderSourcePath}");
            string src = File.ReadAllText(ProviderSourcePath);

            int start = src.IndexOf("public void Preload()", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThan(0), "provider 源码内找不到 `public void Preload()`");
            int end = src.IndexOf("private void Ensure", start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start),
                "找不到 Preload() 之后的第一个 `private void Ensure` —— 方法体边界解析失败");
            string body = src.Substring(start, end - start);

            foreach (string name in ensures)
            {
                Assert.That(body, Does.Contain(name + "()"),
                    $"`{name}` 未出现在 `Preload()` 体内 ⇒ 该表永不被预载" +
                    "(方法存在 ≠ 被调用;首次 Load 时才会在远处炸)");
            }

            // 三者基数相等:方法数 == 地址表数 == address 常量数。
            string[] table = PreloaderAddressTable();
            string[] constants = ProviderAddressConstants();
            Assert.That(ensures.Length, Is.EqualTo(table.Length),
                $"Ensure* 方法数 {ensures.Length} ≠ DataCoreAddresses 长度 {table.Length}");
            Assert.That(constants.Length, Is.EqualTo(table.Length),
                $"address 常量数 {constants.Length} ≠ DataCoreAddresses 长度 {table.Length}");
        }

        // ══════════════════ 分支存在:`Load<T>` 走到装载面,而非未知类型兜底 ══════════════════

        /// <summary>`Load&lt;InteractionKindDataSet&gt;()` 必须命中**装载分支**。
        /// <para><b>判别法(不依赖装载成功)</b>:EditMode 下无 Addressables locator ⇒ 装载必失败,
        /// 但**失败报文**能区分两条路径 —— 走到装载分支 ⇒ 报文含 address 与「启动期硬失败」;
        /// 分支缺失 ⇒ 报文是「未知数据集类型」。两者互斥,故本测有判别力。</para>
        /// <para><b>判别力</b>:删掉 `Load<T>` 里那个 `if (typeof(TDataSet) == typeof(InteractionKindDataSet))`
        /// 分支 ⇒ 本测红(报文变成「未知数据集类型」)。</para>
        /// <para>⚠️ 装载失败会触发 Addressables 的日志;`LogAssert.ignoreFailingMessages`
        /// 只关掉**日志门**,不断言未执行 —— 断言仍逐个跑。</para></summary>
        [Test]
        public void test_wiring_loadBranch_interactionKinds_exists()
        {
            AssertLoadHitsRealLoadPath<InteractionKindDataSet>(
                AddressablesDataProvider.InteractionKindsAddress,
                ds => Assert.That(ds.Rows, Is.Not.Null));
        }

        /// <summary>同 <see cref="test_wiring_loadBranch_interactionKinds_exists"/>,
        /// 对象 = 阴性把握度定表。</summary>
        [Test]
        public void test_wiring_loadBranch_negativeConfidence_exists()
        {
            AssertLoadHitsRealLoadPath<DiagnosisNegativeConfidenceDataSet>(
                AddressablesDataProvider.DiagnosisNegativeConfidenceAddress,
                ds => Assert.That(ds.Table.Count, Is.GreaterThan(0)));
        }

        /// <summary>同 <see cref="test_wiring_loadBranch_interactionKinds_exists"/>,
        /// 对象 = 处方表。</summary>
        [Test]
        public void test_wiring_loadBranch_prescriptionActions_exists()
        {
            AssertLoadHitsRealLoadPath<PrescriptionActionsDataSet>(
                AddressablesDataProvider.PrescriptionActionsAddress,
                ds => Assert.That(ds.Rows, Is.Not.Null));
        }

        /// <summary>同 <see cref="test_wiring_loadBranch_interactionKinds_exists"/>,
        /// 对象 = 处置轴契约表。</summary>
        [Test]
        public void test_wiring_loadBranch_diseaseActionAxis_exists()
        {
            AssertLoadHitsRealLoadPath<DiseaseActionAxisDataSet>(
                AddressablesDataProvider.DiseaseActionAxisAddress,
                ds => Assert.That(ds.Actions, Is.Not.Null));
        }

        /// <summary>同 <see cref="test_wiring_loadBranch_interactionKinds_exists"/>,
        /// 对象 = 53 判定表。</summary>
        [Test]
        public void test_wiring_loadBranch_outcomeTable_exists()
        {
            AssertLoadHitsRealLoadPath<OutcomeTable53DataSet>(
                AddressablesDataProvider.OutcomeTableAddress,
                ds => Assert.That(ds.Table, Is.Not.Null));
        }

        /// <summary>同 <see cref="test_wiring_loadBranch_interactionKinds_exists"/>,
        /// 对象 = 53 延迟表。</summary>
        [Test]
        public void test_wiring_loadBranch_delayTable_exists()
        {
            AssertLoadHitsRealLoadPath<DelayTable53DataSet>(
                AddressablesDataProvider.DelayTableAddress,
                ds => Assert.That(ds.Table, Is.Not.Null));
        }

        private static void AssertLoadHitsRealLoadPath<TDataSet>(
            string expectedAddress, Action<TDataSet> onSuccess) where TDataSet : struct        {
            var provider = new AddressablesDataProvider();
            bool ignore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                TDataSet ds = provider.Load<TDataSet>(); // EditMode:预期失败,但路径可判
                onSuccess(ds);                            // 若真的装载成功,断言其内容非空
            }
            catch (InvalidOperationException ex)
            {
                Assert.That(ex.Message, Does.Not.Contain("未知数据集类型"),
                    $"`Load<{typeof(TDataSet).Name}>` 落到了未知类型兜底 —— 装载分支缺失");

                // 报文比对用**产物基名**(去掉 `.cooked.bytes`):`FetchBytes` 失败报全文 address,
                // 而 codec 解码失败报 `[E-13] <基名> cooked:...` —— 两种失败都必须点名本表。
                string baseName = expectedAddress.Replace(".cooked.bytes", string.Empty);
                Assert.That(ex.Message, Does.Contain(baseName),
                    $"装载失败报文未点名产物「{baseName}」—— 无法确认走的是该表的装载路径。实际报文:{ex.Message}");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = ignore;
            }
        }
    }
}
