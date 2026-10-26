// S20-3 · story-001 —— 采集调参表的**烘焙接线**夹具(评审 W-3 甲案的兑现件)。
//
// 权威来源:
//   ADR-014 §二(两阶段烘焙)· §五(ConfigVersion 内容哈希)
//   CookedFormat(头部契约)· `build_modules_bake_test` / `clinicmachine_tables_schema_test` 同款结构
//   story production/epics/foraging/story-001-...md(Completion Notes §烘焙接线)
//
// ⚠️ **本文件的存在理由**(评审 W-3):补烘前 `ForagingConstantsBinder` 是 25 个 Binder 中
//   **唯一零生产调用方**的 —— 44 条夹具全是直调 `Bind`,证明的是「Binder 能拒收」,
//   **不证明**「Binder 在生产烘焙路径上被执行」。本文件钉的是后者。
//
// ⚠️ **不写盘**:全部走 `BakeFromRepo` 取内存字节,落盘归菜单层(`DataBakeMenu`),
//   与 `build_modules_bake_test` 同口径(不触 `FsWriteBoundaryGate`)。
//
// ⚠️ **本文件不重测 Binder 的 8 条校验** —— 那是 `foraging_data_load_test` 的活。
//   本文件只测四件:① Baker 真的调用了 Binder(负例:坏表 ⇒ Baker 抛);
//   ② Baker 的 MAX_QUALITY 真的从 21a 读(跨表耦合;2026-10-26 证伪轮 W1/W2 双双
//   `red=0` 假绿后补);③ 产物字节确定(同源两次逐位一致);④ 头部契约正确。

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Foraging
{
    /// <summary>采集调参表的烘焙接线(Baker → Binder → CookedWriter → 头部契约)。</summary>
    [TestFixture]
    public class foraging_bake_wiring_test
    {
        // 本文件在 unity/Assets/Tests/EditMode/Foraging/ ⇒ 仓库根 = 上 5 层
        // (Foraging→EditMode→Tests→Assets→unity→仓根)。承 clinicmachine_tables_schema_test.cs:43 先例。
        private static string RepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        // ══════════════════════════════════════════════════════════════
        // ① 生产烘焙路径真的跑通(不是「Binder 单独能跑」)
        // ══════════════════════════════════════════════════════════════

        /// <summary>真源表经 `BakeFromRepo` 能烘出带头的产物。</summary>
        /// <remarks><b>判别力</b>:把 `BakeFromRepo` 里的 `Bind` 调用删掉直接构造 `BindResult`
        /// ⇒ 本测仍绿,但 <see cref="test_w3_badSourceTable_bakerRejects"/> 会红 ——
        /// 两条配合才钉住「Baker 真的经过了 Binder」。
        /// <para>⚠️ 2026-10-26 订正:原稿此处写「(或让 `Read21aMaxQuality` 返回常量 5)」也是
        /// 本组夹具的反证 —— **该断言经证伪轮 W2 实测为假**(`red=0`):真实 `MAX_QUALITY` 恰为 5,
        /// 写死与真读产物逐位相同。该属性的正确反证现由
        /// <see cref="test_w3_maxQuality_isReadFrom21a_notHardcoded"/> 承担。原文不删,就地加注。</para></remarks>
        [Test]
        public void test_w3_realTable_bakesSuccessfully()
        {
            ForagingConstantsBaker.BakeOutput output = ForagingConstantsBaker.BakeFromRepo(RepoRoot());

            Assert.That(output.Cooked, Is.Not.Null, "产物不得为 null");
            Assert.That(output.Cooked.Length, Is.GreaterThan(CookedFormat.HeaderSize),
                "产物须含 20 字节头 + 非空载荷");
            Assert.That(output.ConfigVersion, Is.Not.EqualTo(0u),
                "ConfigVersion 须由内容哈希派生(ADR-014 §五)");
            Assert.That(output.Bound.CapTable.Length, Is.EqualTo(61), "CapTable 须 61 档");
            Assert.That(output.MaxQuality, Is.GreaterThan(0), "21a 的 MAX_QUALITY 须被读到");
        }

        /// <summary>**核心判据**:坏源表经 `BakeFromRepo` 必须抛 —— 证明 Binder 在生产路径上执行。</summary>
        /// <remarks>
        /// <para><b>判别力</b>:本测是 W-3 缺口的**唯一直接反证**。若 `BakeFromRepo` 绕过 Binder
        /// (例如直接把 JSON 塞进 CookedWriter,或让 Binder 的错误被吞),本测转红。</para>
        /// <para>⚠️ 用**临时目录**造坏表,不碰 `assets/data/`(作者态)。写盘落 `Path.GetTempPath()`,
        /// 承 `.claude/rules/test-standards.md` 与 `FsWriteBoundaryGate` R1。</para>
        /// </remarks>
        [Test]
        public void test_w3_badSourceTable_bakerRejects()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(),
                "foraging-bake-wiring-" + Guid.NewGuid().ToString("N"));
            string dataDir = Path.Combine(tempRoot, "assets", "data");
            Directory.CreateDirectory(dataDir);

            try
            {
                // 把真实三表复制过去(21a 的 MAX_QUALITY 要能读出来),再破坏 17 的表。
                string repoData = Path.Combine(RepoRoot(), "assets", "data");
                foreach (string name in new[]
                         {
                             ItemDatabaseBaker.ItemsFileName,
                             ItemDatabaseBaker.RecipesFileName,
                             ItemDatabaseBaker.ConstantsFileName,
                         })
                {
                    File.Copy(Path.Combine(repoData, name), Path.Combine(dataDir, name));
                }

                // 坏表:`regrow_window_ticks` = 0 ⇒ Binder 的 `rw <= 0` 守卫必拒(AC-17-08b)。
                // 用真实表文本做基底,只改这一处 —— 保证「拒收」的原因是这一处,不是别的。
                string good = File.ReadAllText(Path.Combine(repoData, ForagingConstantsBaker.FileName));
                string bad = good.Replace("\"regrow_window_ticks\": 1200", "\"regrow_window_ticks\": 0");
                Assert.That(bad, Is.Not.EqualTo(good), "夹具自检:锚串须命中(否则本变异打空靶)");
                File.WriteAllText(Path.Combine(dataDir, ForagingConstantsBaker.FileName), bad);

                var ex = Assert.Throws<BakeValidationException>(
                    () => ForagingConstantsBaker.BakeFromRepo(tempRoot),
                    "坏表经 BakeFromRepo 必须抛 —— 否则 Binder 未在生产烘焙路径上执行(W-3)");

                Assert.That(ex.Message, Does.Contain("regrow_window_ticks"),
                    "拒收原因须点名字段(不是别的错先炸)。实得:" + ex.Message);
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        /// <summary>
        /// **跨表耦合判据**:Baker 的 `MAX_QUALITY` 必须**真从 21a 读**,不得写死。
        /// </summary>
        /// <remarks>
        /// <para>⚠️ **本测是 2026-10-26 证伪轮 W1/W2 双双 `red=0` 后补的**(S12-4 假绿修复)。
        /// 原夹具集合无法区分「读 21a」与「写死 5」—— 因为**真实 `MAX_QUALITY` 恰为 5**,
        /// 而 17 的 `cap_table` 最大值只有 2(2 ≤ 5 恒成立)⇒ 两者产物逐位相同,
        /// 任何变异都打空靶。这正是「用真实值做变异会静默打空靶」的教科书形态。</para>
        /// <para><b>构造</b>:把 21a 的 `max_quality` 由 5 改成 **1**(不动 17 的表分毫),
        /// 于是 `cap_table` 里的 2 变成违例。真读路径 ⇒ 拒收;写死 5 的变体 ⇒ 放行。</para>
        /// <para><b>判别力(两条独立属性,同一夹具钉住)</b>:
        /// ① `Read21aMaxQuality(repoRoot)` → 常量 `5` ⇒ 本测红(不读 21a);
        /// ② `Bind(json, maxQuality)` → `Bind(json, 5)` ⇒ 本测红(读了 21a 但没把值喂进校验)。</para>
        /// <para>⚠️ 用**临时目录**,不碰 `assets/data/`(作者态)。写盘落 `Path.GetTempPath()`。</para>
        /// </remarks>
        [Test]
        public void test_w3_maxQuality_isReadFrom21a_notHardcoded()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(),
                "foraging-bake-xtable-" + Guid.NewGuid().ToString("N"));
            string dataDir = Path.Combine(tempRoot, "assets", "data");
            Directory.CreateDirectory(dataDir);

            try
            {
                string repoData = Path.Combine(RepoRoot(), "assets", "data");
                foreach (string name in new[]
                         {
                             ItemDatabaseBaker.ItemsFileName,
                             ItemDatabaseBaker.RecipesFileName,
                             ItemDatabaseBaker.ConstantsFileName,
                             ForagingConstantsBaker.FileName,
                         })
                {
                    File.Copy(Path.Combine(repoData, name), Path.Combine(dataDir, name));
                }

                // 只动 21a 的 max_quality:5 → 1。17 的表**一个字节不改**。
                string constantsPath = Path.Combine(dataDir, ItemDatabaseBaker.ConstantsFileName);
                string goodConstants = File.ReadAllText(constantsPath);
                string tightened = goodConstants.Replace("\"max_quality\": 5", "\"max_quality\": 1");
                Assert.That(tightened, Is.Not.EqualTo(goodConstants),
                    "夹具自检:锚串 `\"max_quality\": 5` 须在 21a 常量表里恰命中(否则本测打空靶)");
                File.WriteAllText(constantsPath, tightened);

                // 前置自检:17 的真实表**确实**含有超过 1 的 CapTable 值 ——
                // 否则下面「真读 ⇒ 拒收」的推理不成立(夹具内部一致性)。
                var probe = Assert.Throws<BakeValidationException>(
                    () => ForagingConstantsBaker.BakeFromRepo(tempRoot),
                    "21a max_quality = 1 时,cap_table 中的 2 必须被拒 —— " +
                    "放行说明 MAX_QUALITY 没有被真读进来(W1/W2 假绿)或没喂进校验");

                Assert.That(probe.Message, Does.Contain("cap_table"),
                    "拒收原因须点名 cap_table。实得:" + probe.Message);
                Assert.That(probe.Message, Does.Contain("MAX_QUALITY = 1"),
                    "报错须回显**从 21a 读到的** 1(而非写死的 5)。实得:" + probe.Message);
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        // ══════════════════════════════════════════════════════════════
        // ② 字节确定性(同源两次烘焙逐位一致)
        // ══════════════════════════════════════════════════════════════

        /// <summary>同源两次烘焙字节逐位相等。</summary>
        /// <remarks><b>判别力</b>:若 CookedWriter 里混进字典迭代序 / 本地时间 / `DateTime.Now`,
        /// 两次烘焙字节不等 ⇒ 本测红(承 `build_modules_bake_test:211-214` 同款判据)。</remarks>
        [Test]
        public void test_w3_twoBakes_produceIdenticalBytes()
        {
            byte[] a = ForagingConstantsBaker.BakeFromRepo(RepoRoot()).Cooked;
            byte[] b = ForagingConstantsBaker.BakeFromRepo(RepoRoot()).Cooked;

            Assert.That(a, Is.EqualTo(b),
                "同源两次烘焙字节不等 ⇒ 有字典迭代序 / 本地时间进了产物");
        }

        // ══════════════════════════════════════════════════════════════
        // ③ 头部契约(CookedFormat)
        // ══════════════════════════════════════════════════════════════

        /// <summary>头部五槽逐槽对齐 <see cref="CookedFormat"/>。</summary>
        /// <remarks><b>判别力</b>:magic 写错 / FormatVersion 写反 / payloadLength 与实长不符
        /// ⇒ 读方(E-13 启动期硬失败)会在运行期炸,而单测在此提前抓住。</remarks>
        [Test]
        public void test_w3_cookedHeader_matchesCookedFormat()
        {
            ForagingConstantsBaker.BakeOutput output = ForagingConstantsBaker.BakeFromRepo(RepoRoot());
            byte[] cooked = output.Cooked;

            Assert.That(cooked.Length, Is.GreaterThanOrEqualTo(CookedFormat.HeaderSize));

            // magic = "DYJC"
            Assert.That(cooked[0], Is.EqualTo((byte)'D'), "magic[0]");
            Assert.That(cooked[1], Is.EqualTo((byte)'Y'), "magic[1]");
            Assert.That(cooked[2], Is.EqualTo((byte)'J'), "magic[2]");
            Assert.That(cooked[3], Is.EqualTo((byte)'C'), "magic[3]");

            Assert.That(ReadU32Le(cooked, 4), Is.EqualTo(CookedFormat.FormatVersion), "offset 4 formatVersion");
            Assert.That(ReadU32Le(cooked, 8), Is.EqualTo(CookedFormat.SchemaVersion), "offset 8 schemaVersion");

            uint configVersion = ReadU32Le(cooked, 12);
            Assert.That(configVersion, Is.EqualTo(output.ConfigVersion),
                "offset 12 configVersion 须 = BakeOutput.ConfigVersion(单一真值)");

            uint payloadLength = ReadU32Le(cooked, 16);
            Assert.That((int)payloadLength, Is.EqualTo(cooked.Length - CookedFormat.HeaderSize),
                "offset 16 payloadLength 须 = 实长 − HeaderSize(否则读方越界或截断)");
        }

        private static uint ReadU32Le(byte[] buffer, int offset)
            => (uint)(buffer[offset]
                      | (buffer[offset + 1] << 8)
                      | (buffer[offset + 2] << 16)
                      | (buffer[offset + 3] << 24));

        // ══════════════════════════════════════════════════════════════
        // ④ 产物名与菜单常量同源
        // ══════════════════════════════════════════════════════════════

        /// <summary>Baker 的产物名与 `DataBakeMenu` 的 Addressables 条目名必须同值。</summary>
        /// <remarks><b>判别力</b>:两处写死不同字面串 ⇒ 菜单落盘的文件名与 Baker 自报的名字不符,
        /// 运行期按 Addressables 地址装载会找不到(静默 404 而非编译错)。</remarks>
        [Test]
        public void test_w3_cookedFileName_matchesMenuConstant()
        {
            Assert.That(ForagingConstantsBaker.CookedFileName,
                Is.EqualTo(DataBakeMenu.ForagingConstantsCookedAssetName),
                "Baker.CookedFileName 与 DataBakeMenu.ForagingConstantsCookedAssetName 必须同值");
        }
    }
}
