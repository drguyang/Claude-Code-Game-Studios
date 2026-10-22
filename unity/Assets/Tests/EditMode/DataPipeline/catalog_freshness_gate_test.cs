// 预建 catalog 与 data-core 组定义的**条目闭合门**(2026-10-23 round30)。
//
// ── 为什么需要本门 ────────────────────────────────────────────────────────────
// `provider_wiring_gate_test` 钉的是**作者态**四处闭合(address 常量 / 组定义 / 产物文件 / Preload),
// 但它**全部读 `unity/Assets/` 下的入库文件**,碰不到 `Library/` 里的**构建产物**。
// 而运行期真正解析的是构建出的 catalog:
//
//   `Addressables.LoadAssetAsync(address)` → catalog 查 Location
//   → 查不到 ⇒ `InvalidKeyException` ⇒ `FetchBytes` 抛 ⇒ `Preload()` 启动期硬失败
//
// ⇒ 存在一条**两侧都绿、中间断开**的静默失效:
//     组定义里有条目(作者态绿)+ 产物字节在盘上(漂移门绿)+ 但 catalog 是旧的 ⇒ 启动全挂。
//
// ── 实证(2026-10-23 实测,本门立规缘由) ──────────────────────────────────────
//   组定义 `data-core.asset`:**22** 条
//   构建出的 catalog(`Library/.../Linux/catalog.bin`):**21** 条
//   缺的恰是 **`disease_registry.cooked.bytes`** —— B1 接真时新增,组定义改了,
//   **catalog 没重建**(`m_BuildAddressablesWithPlayerBuild: 0` ⇒ 打包也不会顺带重建)。
//   该份已进 `Preload()`(`EnsureDiseaseRegistry`),⇒ 当前这棵树**启动必挂**。
//   而当时全量 EditMode 4647 条**全绿** —— 因为没有任何一条读 catalog。
//
// ── 本门查什么 / 不查什么 ────────────────────────────────────────────────────
//   查 —— data-core 组的**每一条** address 都在构建出的 catalog 里出现。
//   不查 —— ① 其它组(audio / Default Local Group)的条目(它们不承担启动期硬失败);
//           ② 产物字节内容(归漂移门);③ 组定义本身(归 provider_wiring_gate)。
//
// ⚠️ **catalog 是 gitignored 的机器本地产物**(`.gitignore:61 [Ll]ibrary/`)⇒ 新检出 /
//    CI 上不存在。故本门在文件缺失时 **Inconclusive**,不是红也不是绿:
//    绿 = 「比过了,闭合」;红 = 「比过了,缺条目」;Inconclusive = 「没得比」。
//    **禁把 Inconclusive 当绿**(承借绿禁令)—— 判据里写明。
//
// 权威来源:ADR-014 §五(data-core 单组 + 首次 Step 前预载 + E-13 硬失败)
//          · `DataBakeMenu.BuildAddressablesContent`(重建 catalog 的唯一入口)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.DataPipeline
{
    /// <summary>
    /// 构建出的 Addressables catalog 与 <c>data-core</c> 组定义的条目闭合门。
    /// </summary>
    /// <remarks>
    /// 与 <c>ProviderWiringGateTest</c> 的分工:那个钉**作者态**(入库文件之间的一致性),
    /// 本门钉**作者态 → 构建产物**这一跳。两者缺一不可 —— 作者态全对但没重建,运行期照样挂。
    /// </remarks>
    [TestFixture]
    public sealed class CatalogFreshnessGateTest
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        private static string GroupAssetPath
            => Path.Combine(RepoRoot, "unity", "Assets", "AddressableAssetsData",
                            "AssetGroups", "data-core.asset");

        /// <summary>构建产物根(<c>unity/Library/com.unity.addressables/aa/</c>)。</summary>
        private static string CatalogRoot
            => Path.Combine(RepoRoot, "unity", "Library", "com.unity.addressables", "aa");

        /// <summary>
        /// 找到构建出的 catalog。<c>aa/</c> 下按平台分目录(<c>Linux</c> / <c>Windows</c> / …),
        /// 取**最近修改**的那份 —— 本门只关心「盘上最新的构建是否含全部条目」。
        /// </summary>
        private static string FindCatalog()
        {
            if (!Directory.Exists(CatalogRoot)) return null;

            return Directory.EnumerateFiles(CatalogRoot, "catalog.bin", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        /// <summary>解析 `data-core.asset` 的条目 address(与 <c>ProviderWiringGateTest</c> 同法)。</summary>
        private static List<string> DataCoreAddresses()
        {
            Assert.That(File.Exists(GroupAssetPath), Is.True,
                $"data-core 组定义不存在:{GroupAssetPath}(组定义是作者态,须入库)");

            var addresses = new List<string>();
            string pendingGuid = null;
            foreach (string raw in File.ReadAllLines(GroupAssetPath))
            {
                string line = raw.Trim();
                Match g = Regex.Match(line, @"^-\s*m_GUID:\s*([0-9a-fA-F]{32})\s*$");
                if (g.Success) { pendingGuid = g.Groups[1].Value; continue; }
                Match a = Regex.Match(line, @"^m_Address:\s*(\S+)\s*$");
                if (a.Success && pendingGuid != null)
                {
                    addresses.Add(a.Groups[1].Value);
                    pendingGuid = null;
                }
            }
            return addresses;
        }

        /// <summary>
        /// 从 catalog 二进制里提取全部 <c>*.cooked.bytes</c> 字面串。
        /// <para><b>为什么用字面串而不是解析二进制结构</b>:catalog 是 Unity 私有格式
        /// (文件头魔数 <c>0x0DE38942</c> + 版本化段表,6.x 与后续版本不保证稳定)。
        /// 本门要的判据只是「这个 address 字符串在不在里面」,而 address 是**明文串**存储 ——
        /// 字面串匹配足以判别,且不随 catalog 版本升级而失效。</para>
        /// <para>⚠️ 匹配锚定完整文件名(<c>[a-z0-9_]+\.cooked\.bytes</c>),
        /// 避免 <c>foo</c> 命中 <c>foobar</c> 这类子串假阳性。</para>
        /// </summary>
        private static HashSet<string> CatalogCookedEntries(string catalogPath)
        {
            byte[] bytes = File.ReadAllBytes(catalogPath);

            // 只取可打印 ASCII 区段作文本视图;catalog 的地址串是 UTF-8 ASCII。
            var text = new System.Text.StringBuilder(bytes.Length);
            foreach (byte b in bytes)
                text.Append(b >= 0x20 && b < 0x7F ? (char)b : '\n');

            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(text.ToString(), @"[a-z0-9_]+\.cooked\.bytes"))
                found.Add(m.Value);
            return found;
        }

        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// <b>本门的主判据</b>:data-core 组里每一条 address 都在构建出的 catalog 里。
        /// </summary>
        /// <remarks>
        /// <b>判别力(实证)</b>:本门落地当天,盘上实测 组 22 条 / catalog 21 条,
        /// 缺 <c>disease_registry.cooked.bytes</c> ⇒ 本条**当场红并点名该条**。
        /// 重建 catalog 后转绿。
        /// <para>⚠️ catalog 缺失 ⇒ <b>Inconclusive</b>(不是绿)。新检出 / CI 上无 `Library/`,
        /// 此时本门无从比对 —— 如实登记,不得当通过。</para>
        /// </remarks>
        [Test]
        public void test_catalog_containsEveryDataCoreAddress()
        {
            List<string> declared = DataCoreAddresses();
            Assert.That(declared, Is.Not.Empty, "data-core 组解析出 0 条 —— 解析器或组文件已变");

            string catalog = FindCatalog();
            if (catalog == null)
            {
                Assert.Inconclusive(
                    $"未找到构建出的 catalog({CatalogRoot} 下无 catalog.bin)—— " +
                    "本门无从比对。新检出 / CI 属正常;本地请先跑「构建 Addressables 内容」菜单。" +
                    "⚠️ Inconclusive 不等于通过。");
                return;
            }

            HashSet<string> built = CatalogCookedEntries(catalog);
            List<string> missing = declared.Where(a => !built.Contains(a)).ToList();

            Assert.That(missing, Is.Empty,
                $"data-core 组声明了 {declared.Count} 条,构建出的 catalog 只有 {built.Count} 条 —— " +
                $"缺:{string.Join(", ", missing)}\n" +
                $"catalog = {catalog}\n" +
                "⇒ 运行期 `LoadAssetAsync` 查不到 Location ⇒ `Preload()` 启动期硬失败(E-13)。\n" +
                "修:跑「大医精诚/数据管线/构建 Addressables 内容(BuildPlayerContent)」重建 catalog。\n" +
                "⚠️ `m_BuildAddressablesWithPlayerBuild: 0` ⇒ 打包**不会**顺带重建,必须显式跑。");
        }

        /// <summary>
        /// 反方向:构建出的 catalog 里不该有组定义**从未声明**的 <c>.cooked.bytes</c>。
        /// <para><b>为什么单列</b>:条目被从组里删掉但没重建 ⇒ catalog 留着幽灵条目。
        /// 幽灵条目本身不致命(没人查它),但它说明 catalog 与组定义**已经脱节** ——
        /// 与上面那条是同一根因的两个方向,分开报能让「该重建了」这件事一眼可见。</para>
        /// </summary>
        [Test]
        public void test_catalog_hasNoGhostCookedEntries()
        {
            string catalog = FindCatalog();
            if (catalog == null)
            {
                Assert.Inconclusive(
                    $"未找到构建出的 catalog({CatalogRoot} 下无 catalog.bin)—— 本门无从比对。" +
                    "⚠️ Inconclusive 不等于通过。");
                return;
            }

            HashSet<string> declared = new HashSet<string>(DataCoreAddresses(), StringComparer.Ordinal);
            List<string> ghosts = CatalogCookedEntries(catalog)
                .Where(a => !declared.Contains(a))
                .OrderBy(a => a, StringComparer.Ordinal)
                .ToList();

            Assert.That(ghosts, Is.Empty,
                $"catalog 里有组定义未声明的 .cooked.bytes:{string.Join(", ", ghosts)}\n" +
                $"catalog = {catalog}\n" +
                "⇒ catalog 与组定义已脱节(条目被删但没重建)。请重建 catalog。");
        }

        /// <summary>
        /// 判据自身的**前提**:组定义确实非空、catalog 提取器确实能提取到东西。
        /// <para><b>为什么单列</b>:若 <see cref="CatalogCookedEntries"/> 因 catalog 格式变化
        /// 提取出**空集**,上面两条会**双双绿**(空集 ⊆ 任何集 / 空集无幽灵)——
        /// 那是本门最危险的失效形态(门坏了但报绿)。本条把「提取器还能工作」钉成前提。</para>
        /// </summary>
        [Test]
        public void test_catalog_extractor_stillFindsEntries_premise()
        {
            string catalog = FindCatalog();
            if (catalog == null)
            {
                Assert.Inconclusive($"未找到构建出的 catalog({CatalogRoot})—— 前提无从检验。");
                return;
            }

            HashSet<string> built = CatalogCookedEntries(catalog);
            Assert.That(built.Count, Is.GreaterThan(0),
                $"从 catalog 提取到 0 条 .cooked.bytes —— 提取器失效或 catalog 格式已变。" +
                $"此时上两条会**假绿**(空集恒满足),故本条单独钉住。catalog = {catalog}");
            Assert.That(DataCoreAddresses().Count, Is.GreaterThan(0), "组定义解析出 0 条");
        }
    }
}
