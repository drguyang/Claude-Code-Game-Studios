// `build_modules.json`(23 模块目录 · 21a 作者态)的烘焙与域门测试。
//
// 权威来源:
//   GDD design/gdd/modular-building.md `:88`(模块目录字段集)· `:90`(锚点校验,评审 B5)
//     · `:355`(`module_type` = SlotType 枚举)· `:507`(`cost(m)` 域 ≥ 1)
//   GDD design/gdd/item-database.md `:859`(「与 `cost(m)` 同源」)
//   ADR-014 §三/§四(白名单 · Fix 字符串 · 聚合 throw)· ADR-015 §三(单一整数格)
//   `TD-itemdb-01`(本表是其兑现件)
//
// ⚠️ **判别力纪律**:每条负向夹具都对应一条**具体域门**。断言打的是错误报文里的
//   **判据关键词**,不是「抛了就行」—— 否则任何一处 throw 都能让所有负向夹具变绿。
//   该纪律的由来:`clinic_furniture_catalogue_test` 同款(承 `TD-clinic-01` 的假绿教训)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using DaYiJingCheng.EditorTools.Bake;

namespace DaYiJingCheng.Tests.ClinicMachine
{
    /// <summary>模块目录(<c>build_modules.json</c>)的烘焙 / 域门 / 确定性。</summary>
    public class BuildModulesBakeTest
    {
        private static string RepoRoot
            => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", ".."));

        // ══════════════════ 一 · 真种子(正向半边)══════════════════

        /// <summary>真种子过四道域门 —— 一条错都没有。</summary>
        /// <remarks><b>判别力</b>:任何一条域门误判(如把合法 `furn_type` 当成越界)
        /// ⇒ 本测当场红。它是「门不过紧」的唯一判据。</remarks>
        [Test]
        public void test_buildModules_realSeed_passesAllGates()
        {
            BuildModulesBake.BakeOutput output = BuildModulesBake.BakeFromRepo(RepoRoot);

            Assert.That(output.Bound.Rows.Count, Is.GreaterThan(0), "真种子须非空");
            Assert.That(output.Cooked.Length, Is.GreaterThan(20), "产物须含 20 字节头 + 载荷");
            Assert.That(output.ConfigVersion, Is.Not.EqualTo(0u), "ConfigVersion 须由内容哈希派生");
        }

        /// <summary>真种子声明的 `furn_type` 全部 ∈ `FURN_TYPES` 闭集(跨表同源门)。</summary>
        /// <remarks><b>判别力</b>:把种子里任一 `furn_type` 改成不存在的名字(如 `"ghost"`)
        /// ⇒ `BakeBuildModulesCore` 聚合 throw ⇒ 本测红。这是「同源」这句话的可执行形态。</remarks>
        [Test]
        public void test_buildModules_productionCore_enforcesFurnTypeSamesource()
        {
            ItemDatabaseFurnitureBake.BakeOutput furn;
            BuildModulesBake.BakeOutput result =
                DataBakeMenu.BakeBuildModulesCore(RepoRoot, out furn);

            var closed = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < furn.Bound.Rows.Count; i++) closed.Add(furn.Bound.Rows[i].Type);

            foreach (BuildModulesBinder.ModuleRow row in result.Bound.Rows)
            {
                if (row.FurnType == null) continue;
                Assert.That(closed.Contains(row.FurnType), Is.True,
                    $"module_id={row.ModuleId} 的 furn_type「{row.FurnType}」须在 FURN_TYPES 闭集内");
            }
        }

        /// <summary>跨表同源门**真的会拒** —— 走生产链路 <see cref="DataBakeMenu.BakeBuildModulesCore"/>。</summary>
        /// <remarks>
        /// <para><b>为什么必须走生产链路而不是 `BakeFromSourceText`</b>:跨表门**只存在于**
        /// `DataBakeMenu.BakeBuildModulesCore`(它先烘家具目录取闭集,再逐 `furn_type` 对照)。
        /// 若本测改走 Binder,它测的就是**另一条码路**,门即使被删也照样绿 —— 那正是
        /// `TD-clinic-01` 的假绿形态(「扫不到被测装配」)。</para>
        /// <para><b>判别力</b>:把生产核心里 `if (!known.Contains(row.FurnType))` 改成 `if (false)`
        /// ⇒ 本测红(不再抛)。停掉 `BakeBuildModulesCore` 里的 `throw` 亦然。</para>
        /// <para>⚠️ 本测在 `Path.GetTempPath()` 下造一个**最小仓根**(只含两张表),
        /// 不碰真仓库、不写 `assets/data/`(承 `.claude/rules/test-standards.md`;
        /// 写盘落 `GetTempPath()` 是 `FsWriteBoundaryGate` 允许的形态)。</para>
        /// </remarks>
        [Test]
        public void test_buildModules_productionCore_rejectsUnknownFurnType()
        {
            string tempRoot = MakeTempRepoRoot();
            try
            {
                // 模块目录:一条合法 + 一条**闭集外**的 furn_type。
                WriteModules(
                    tempRoot,
                    Row(1, "Bed", "\"bed\"", "[[0,0,0]]", 4, true),
                    Row(2, "Decor", "\"ghost\"", "[[0,0,0]]", 1, true));

                ItemDatabaseFurnitureBake.BakeOutput furn;
                var ex = Assert.Throws<BakeValidationException>(
                    () => DataBakeMenu.BakeBuildModulesCore(tempRoot, out furn));

                Assert.That(
                    ex.Errors.Any(e => e.IndexOf("FURN_TYPES 闭集", StringComparison.Ordinal) >= 0),
                    Is.True,
                    "错误报文须点名「不在 FURN_TYPES 闭集内」:\n" + string.Join("\n", ex.Errors));
                Assert.That(
                    ex.Errors.Any(e => e.IndexOf("ghost", StringComparison.Ordinal) >= 0),
                    Is.True,
                    "错误报文须点名坏值 ghost(否则读者不知道该改哪一行)");
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
            }
        }

        // ══════════════════ 一b · 跨表外键门(`build_item_key` → 物品表)══════════════════

        // ⚠️ 以下两条**必须**走 `DataBakeMenu.BakeBuildModulesCore`(生产链路),理由同上面
        //    `furn_type` 那条:`build_item_key` 的存在性 / category 判据**只存在于**该函数
        //    (它先烘物品表取 ordinal→category,再逐行对照)。改走 `BakeFromSourceText`
        //    测的是另一条码路,门被删了照样绿 —— `TD-clinic-01` 的「扫不到被测装配」形态。

        /// <summary>`build_item_key` 指向物品表里不存在的 ordinal ⇒ 拒(跨表外键门)。</summary>
        /// <remarks><b>判别力</b>:把 `BakeBuildModulesCore` 里的
        /// `if (!categoryByOrdinal.TryGetValue(...))` 改成 `if (false)` ⇒ 本测红(不再抛)。
        /// <para>⚠️ 本测的价值在**「不存在的号」不会被 Binder 拦住** —— Binder 只做
        /// `[0, int.MaxValue]` 域校验,`9999` 域上合法。只有跨表门能看出它是空指向。</para></remarks>
        [Test]
        public void test_buildModules_productionCore_rejectsDanglingBuildItemKey()
        {
            string tempRoot = MakeTempRepoRoot();
            try
            {
                WriteModules(
                    tempRoot,
                    Row(1, "Bed", "\"bed\"", "[[0,0,0]]", 4, true, buildItemKey: 9999));

                ItemDatabaseFurnitureBake.BakeOutput furn;
                var ex = Assert.Throws<BakeValidationException>(
                    () => DataBakeMenu.BakeBuildModulesCore(tempRoot, out furn));

                Assert.That(
                    ex.Errors.Any(e => e.IndexOf("不存在", StringComparison.Ordinal) >= 0),
                    Is.True,
                    "错误报文须说明「该 ordinal 在物品表里不存在」:\n" + string.Join("\n", ex.Errors));
                Assert.That(
                    ex.Errors.Any(e => e.IndexOf("9999", StringComparison.Ordinal) >= 0),
                    Is.True,
                    "错误报文须点名坏值 9999(否则读者不知道该改哪一行)");
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
            }
        }

        /// <summary>`build_item_key` 指向的物品 `category ≠ build_part` ⇒ 拒。</summary>
        /// <remarks><b>判别力</b>:把 `else if (cat != ItemCategory.BuildPart)` 改成 `else if (false)`
        /// ⇒ 本测红。
        /// <para>⚠️ 用的 **ordinal 0 = `willow_bark`(material)** —— 这不是随手挑的:它是
        /// 「作者写了 `build_item_key: 0` 以为在说『没指定』」的**真实**失败形态,
        /// 而 0 在真种子里**是合法存在的物品**。存在性门放行它,只有 category 门能拦。
        /// 若换成一个不存在的号,本测会与上一条同构,失去它该有的判别力。</para></remarks>
        [Test]
        public void test_buildModules_productionCore_rejectsNonBuildPartCategory()
        {
            string tempRoot = MakeTempRepoRoot();
            try
            {
                WriteModules(
                    tempRoot,
                    Row(1, "Bed", "\"bed\"", "[[0,0,0]]", 4, true, buildItemKey: 0));

                ItemDatabaseFurnitureBake.BakeOutput furn;
                var ex = Assert.Throws<BakeValidationException>(
                    () => DataBakeMenu.BakeBuildModulesCore(tempRoot, out furn));

                Assert.That(
                    ex.Errors.Any(e => e.IndexOf("build_part", StringComparison.Ordinal) >= 0),
                    Is.True,
                    "错误报文须点名要求的 category = build_part:\n" + string.Join("\n", ex.Errors));
                Assert.That(
                    ex.Errors.Any(e => e.IndexOf("Material", StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.True,
                    "错误报文须点名实际 category(Material),否则读者不知道它被读成了什么");
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
            }
        }

        // ══════════════════ 二 · 四条域门(负向)══════════════════

        /// <summary>`(0,0,0) ∉ occupancy_local` ⇒ 拒(评审 B5)。</summary>
        /// <remarks><b>判别力</b>:停用该门 ⇒ 本测红。锚点不在占用集里 = 「锚点可走、结构在其上」。</remarks>
        [Test]
        public void test_buildModules_missingAnchorCell_isRejected()
            => AssertRejected(
                SourceWith(Row(1, "Bed", "\"bed\"", "[[1,0,0],[2,0,0]]", 4, true)), "(0,0,0)");

        /// <summary>`module_id` 重复 ⇒ 拒。</summary>
        [Test]
        public void test_buildModules_duplicateModuleId_isRejected()
            => AssertRejected(
                SourceWith(
                    Row(1, "Bed", "\"bed\"", "[[0,0,0]]", 4, true),
                    Row(1, "Table", "\"grinder\"", "[[0,0,0]]", 3, true)),
                "重复");

        /// <summary>`module_id ≤ 0` ⇒ 拒(非正 id 与「未分配」不可分)。</summary>
        [Test]
        public void test_buildModules_nonPositiveModuleId_isRejected()
            => AssertRejected(
                SourceWith(Row(0, "Bed", "\"bed\"", "[[0,0,0]]", 4, true)), "(0,");

        /// <summary>`module_type` 不在 SlotType 枚举名闭集内 ⇒ 拒。</summary>
        /// <remarks><b>判别力</b>:停用该门 ⇒ 拼错的类型被读成 `None` ⇒ 所有槽位恒不匹配 = 静默失效。</remarks>
        [Test]
        public void test_buildModules_unknownModuleType_isRejected()
            => AssertRejected(
                SourceWith(Row(1, "Bedroom", "\"bed\"", "[[0,0,0]]", 4, true)), "SlotType 枚举名闭集");

        /// <summary>`module_type = "None"` ⇒ 拒(哨兵不是合法类型)。</summary>
        [Test]
        public void test_buildModules_noneModuleType_isRejected()
            => AssertRejected(
                SourceWith(Row(1, "None", "\"bed\"", "[[0,0,0]]", 4, true)), "SlotType 枚举名闭集");

        /// <summary>`cost < 1` ⇒ 拒。</summary>
        [Test]
        public void test_buildModules_zeroCost_isRejected()
            => AssertRejected(
                SourceWith(Row(1, "Bed", "\"bed\"", "[[0,0,0]]", 0, true)), "cost");

        /// <summary>`refundable` 缺席 ⇒ 拒(缺字段 ≠ false)。</summary>
        [Test]
        public void test_buildModules_missingRefundable_isRejected()
            => AssertRejected(
                "{\"schema_version\":2,\"modules\":[" +
                "{\"module_id\":1,\"module_type\":\"Bed\",\"furn_type\":\"bed\"," +
                "\"occupancy_local\":[[0,0,0]],\"cost\":4,\"build_item_key\":8}]}", "refundable");

        /// <summary>`furn_type` 缺席 ⇒ 拒(「没写」≠「明写 null」)。</summary>
        [Test]
        public void test_buildModules_missingFurnType_isRejected()
            => AssertRejected(
                "{\"schema_version\":2,\"modules\":[" +
                "{\"module_id\":1,\"module_type\":\"Bed\",\"occupancy_local\":[[0,0,0]]," +
                "\"cost\":4,\"refundable\":true,\"build_item_key\":8}]}", "furn_type");

        /// <summary>未知键 ⇒ 拒(ADR-014 §三)。</summary>
        [Test]
        public void test_buildModules_unknownKey_isRejected()
            => AssertRejected(
                "{\"schema_version\":2,\"modules\":[" +
                "{\"module_id\":1,\"module_type\":\"Bed\",\"furn_type\":\"bed\"," +
                "\"occupancy_local\":[[0,0,0]],\"cost\":4,\"refundable\":true," +
                "\"build_item_key\":8,\"tier\":2}]}",
                "未知键");

        /// <summary>空 `modules[]` ⇒ 拒(空目录让放置判定 ⑤ 恒失败)。</summary>
        [Test]
        public void test_buildModules_emptyTable_isRejected()
            => AssertRejected("{\"schema_version\":2,\"modules\":[]}", "modules");

        // ══════════════════ 二b · `build_item_key`(schema 2 新字段)══════════════════

        /// <summary>`build_item_key` 缺席 ⇒ 拒。
        /// <para><b>判别力</b>:停用 `TryReadBuildItemKey` 的「缺席」分支(默认 0 放行)
        /// ⇒ 本测红 —— 而那正是最坏的形态:0 是真种子里的 `willow_bark`(material),
        /// 建造扣料会去扣一栈药材而不报错。</para></summary>
        [Test]
        public void test_buildModules_missingBuildItemKey_isRejected()
            => AssertRejected(
                "{\"schema_version\":2,\"modules\":[" +
                "{\"module_id\":1,\"module_type\":\"Bed\",\"furn_type\":\"bed\"," +
                "\"occupancy_local\":[[0,0,0]],\"cost\":4,\"refundable\":true}]}",
                "build_item_key");

        /// <summary>`build_item_key` 写成字符串 ⇒ 拒(它是 i32 载荷形,不是名称)。
        /// <para><b>判别力</b>:容忍字符串会让作者以为是写物品名,而读方永远读不到。</para></summary>
        [Test]
        public void test_buildModules_buildItemKeyAsString_isRejected()
            => AssertRejected(
                SourceWith(Row(1, "Bed", "\"bed\"", "[[0,0,0]]", 4, true)
                    .Replace("\"build_item_key\":8", "\"build_item_key\":\"wooden_post\"")),
                "build_item_key");

        /// <summary>`build_item_key` 为负 ⇒ 拒(负号是「未赋值 ordinal」的哨兵,不是合法键)。</summary>
        [Test]
        public void test_buildModules_negativeBuildItemKey_isRejected()
            => AssertRejected(
                SourceWith(Row(1, "Bed", "\"bed\"", "[[0,0,0]]", 4, true, buildItemKey: -1)),
                "build_item_key");

        /// <summary>占用格三元组写成 2 项 ⇒ 拒。</summary>
        [Test]
        public void test_buildModules_badCellArity_isRejected()
            => AssertRejected(
                SourceWith(Row(1, "Bed", "\"bed\"", "[[0,0]]", 4, true)), "三元数组");

        /// <summary>同一行内重复占用格 ⇒ 拒(占用计数会虚高)。</summary>
        [Test]
        public void test_buildModules_duplicateCellInRow_isRejected()
            => AssertRejected(
                SourceWith(Row(1, "Bed", "\"bed\"", "[[0,0,0],[0,0,0]]", 4, true)), "重复");

        // ══════════════════ 三 · 确定性 ══════════════════

        /// <summary>同源两次烘焙逐位一致(ADR-014 §二)。</summary>
        /// <remarks><b>判别力</b>:去掉写方的 `rows.Sort(CompareModuleId)` 而作者重排 `modules[]`
        /// ⇒ 本测红(声明序会漏进字节流)。</remarks>
        [Test]
        public void test_buildModules_bakeIsDeterministic()
        {
            byte[] a = BuildModulesBake.BakeFromRepo(RepoRoot).Cooked;
            byte[] b = BuildModulesBake.BakeFromRepo(RepoRoot).Cooked;
            Assert.That(a, Is.EqualTo(b), "同源两次烘焙字节不等 ⇒ 有字典迭代序 / 本地时间进了产物");
        }

        /// <summary>行序 = `module_id` 升序(声明序不承载语义)。</summary>
        /// <remarks>
        /// <para><b>判别力</b>:把种子的 `modules[]` 顺序倒过来,**载荷段**应逐位相同。
        /// 若写方的 `rows.Sort(CompareModuleId)` 被去掉,载荷段会随声明序变 ⇒ 本测红。</para>
        /// <para>⚠️ <b>比的是载荷段(offset 20 起),不是整份字节</b> —— 首版比整份,当场红。
        /// 原因不是写方坏了:`ConfigVersion` 由**源文本内容哈希**派生(ADR-014 §五),
        /// 倒序 `modules[]` 必然改哈希 ⇒ 头槽 offset 12 必然不同。**那一项不同是正确行为**,
        /// 把它算进判据 = 让本测测「源文本是否相同」,而不是「行序是否被规范化」。</para>
        /// </remarks>
        [Test]
        public void test_buildModules_rowOrder_isModuleIdSorted()
        {
            string reversed = ReversedSeed();
            byte[] original = Payload(BuildModulesBake.BakeFromRepo(RepoRoot).Cooked);
            byte[] fromReversed = Payload(BuildModulesBake.BakeFromSourceText(reversed).Cooked);

            Assert.That(fromReversed, Is.EqualTo(original),
                "倒序的 modules[] 须烘出逐位相同的**载荷段** —— 否则声明序进了产物");

            // 反证另一半:头槽的 ConfigVersion **必须**不同(源文本变了)。
            // 若这一条也相同,说明 ConfigVersion 没在读源文本 ⇒ ADR-014 §五 的派生式被架空。
            uint a = BuildModulesBake.BakeFromRepo(RepoRoot).ConfigVersion;
            uint b = BuildModulesBake.BakeFromSourceText(reversed).ConfigVersion;
            Assert.That(b, Is.Not.EqualTo(a),
                "倒序源文本的 ConfigVersion 须不同 —— 相同说明它没在读源内容(ADR-014 §五)");
        }

        /// <summary>去掉 20 字节头,只留载荷段(<see cref="DaYiJingCheng.Sim.Contracts.CookedFormat.HeaderSize"/>)。</summary>
        private static byte[] Payload(byte[] cooked)
        {
            int header = DaYiJingCheng.Sim.Contracts.CookedFormat.HeaderSize;
            var payload = new byte[cooked.Length - header];
            Array.Copy(cooked, header, payload, 0, payload.Length);
            return payload;
        }

        // ══════════════════ 四 · 读方 ↔ 写方镜像 ══════════════════

        /// <summary>盘上字节与现烘字节**逐位相同**(补烘 ≠ 登记 ≠ 接线 的「补烘」半边)。</summary>
        /// <remarks><b>判别力</b>:改了作者态但忘了重烘 ⇒ 本测红。这是「盘上产物是陈的」的唯一判据。</remarks>
        [Test]
        public void test_buildModules_diskMatchesFreshBake()
        {
            string path = Path.Combine(
                RepoRoot, "unity", "Assets", "DataCooked", BuildModulesBake.CookedFileName);
            Assert.That(File.Exists(path), Is.True,
                $"产物不存在:{path} —— 先跑「烘焙 build_modules(23 模块目录 · 21a)」菜单");

            byte[] disk = File.ReadAllBytes(path);
            byte[] fresh = BuildModulesBake.BakeFromRepo(RepoRoot).Cooked;
            Assert.That(disk, Is.EqualTo(fresh), "盘上产物与现烘字节不一致 ⇒ 忘了重烘");
        }

        // ══════════════════ 辅助 ══════════════════

        /// <summary>
        /// 在 `Path.GetTempPath()` 下造一个**最小仓根**:拷入生产链路需要的全部真种子
        /// (21a 物品三件 + append-only 基线 + 家具目录),**不写** `build_modules.json`
        /// —— 由调用方用 <see cref="WriteModules"/> 注入。
        /// </summary>
        /// <remarks>
        /// ⚠️ **为什么必须连基线一起拷**:跨表外键门要先烘物品表(`ItemDatabaseBaker.BakeFromRepo`),
        /// 而它在**生产路径**恒读 `_ordinal_baseline.json` 且「缺失即硬失败」。少拷一件
        /// ⇒ 本测会因「基线文件不在」而红,**红的原因不对**(`TD-clinic-01` 同款的假红形态)。
        /// <para>⚠️ 落盘一律 `Path.GetTempPath()`(承 `.claude/rules/test-standards.md`
        /// 与 `FsWriteBoundaryGate`);真仓库 `assets/data/` 一字不动。</para>
        /// </remarks>
        private static string MakeTempRepoRoot()
        {
            string tempRoot = Path.Combine(
                Path.GetTempPath(), "djc-buildmodules-" + Guid.NewGuid().ToString("N"));
            string dataDir = Path.Combine(tempRoot, "assets", "data");
            Directory.CreateDirectory(dataDir);

            string[] seeds =
            {
                ItemDatabaseBaker.ItemsFileName,
                ItemDatabaseBaker.RecipesFileName,
                ItemDatabaseBaker.ConstantsFileName,
                OrdinalBaselineReader.FileName,
                ItemDatabaseFurnitureBinder.FileName,
            };
            foreach (string name in seeds)
                File.Copy(Path.Combine(RepoRoot, "assets", "data", name), Path.Combine(dataDir, name));

            return tempRoot;
        }

        /// <summary>把给定的模块行写成临时仓根里的 `build_modules.json`(schema 2)。</summary>
        private static void WriteModules(string tempRoot, params string[] rows)
            => File.WriteAllText(
                Path.Combine(tempRoot, "assets", "data", BuildModulesBinder.FileName),
                SourceWith(rows));

        /// <summary>单行 JSON 片段(`furnTypeJson` 由调用方带引号或写 `null`)。
        /// <para>🔁 2026-10-26:schema 2 起每行**必带** `build_item_key`(21a 物品表 ordinal)。
        /// 默认取 <see cref="BuildPartOrdinal"/> —— 真种子里唯一那条 `category = build_part` 的
        /// `wooden_post`。本辅助**不走跨表门**(那条门只在 `DataBakeMenu.BakeBuildModulesCore` 里),
        /// 故此处给的只是形状合法的 ordinal。</para></summary>
        private static string Row(
            int id, string moduleType, string furnTypeJson, string occupancy, int cost, bool refundable,
            int? buildItemKey = null)
            => $"{{\"module_id\":{id},\"module_type\":\"{moduleType}\"," +
               $"\"furn_type\":{furnTypeJson},\"occupancy_local\":{occupancy}," +
               $"\"cost\":{cost},\"refundable\":{(refundable ? "true" : "false")}," +
               $"\"build_item_key\":{buildItemKey ?? BuildPartOrdinal}}}";

        /// <summary>真种子里 `category = build_part` 的唯一物品(`wooden_post`)的 ordinal。</summary>
        private const int BuildPartOrdinal = 8;

        /// <summary>包成完整源文本(schema 2 —— 行尾带 `build_item_key`)。</summary>
        private static string SourceWith(params string[] rows)
            => "{\"schema_version\":2,\"modules\":[" + string.Join(",", rows) + "]}";

        /// <summary>把真种子的 `modules[]` 倒序(用于行序判别)。</summary>
        private static string ReversedSeed()
        {
            string path = Path.Combine(RepoRoot, "assets", "data", BuildModulesBinder.FileName);
            string text = File.ReadAllText(path);
            int start = text.IndexOf("\"modules\"", StringComparison.Ordinal);
            int open = text.IndexOf('[', start);
            int close = text.LastIndexOf(']');
            string inner = text.Substring(open + 1, close - open - 1);

            // 按顶层对象切分(每行的 occupancy_local 里也有 ] ⇒ 不能用简单 split)。
            var rows = new List<string>();
            int depth = 0, rowStart = -1;
            for (int i = 0; i < inner.Length; i++)
            {
                char c = inner[i];
                if (c == '{')
                {
                    if (depth == 0) rowStart = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && rowStart >= 0)
                    {
                        rows.Add(inner.Substring(rowStart, i - rowStart + 1));
                        rowStart = -1;
                    }
                }
            }

            rows.Reverse();
            // ⚠️ schema_version 从**源文件头**读,不写死 —— 写死会让「源表抬版本」这件事
            //    在本辅助里变成第二真源,抬版本时这里静默失效(本测会比较 schema 期望值 ⇒ 恒红,
            //    但红的原因会是辅助过期而不是被测行为)。
            return "{\"schema_version\":" + ReadSchemaVersionOf(text) +
                   ",\"modules\":[" + string.Join(",", rows) + "]}";
        }

        /// <summary>从源文本头里取 `schema_version` 的整数值(本文件格式固定,够用)。</summary>
        private static int ReadSchemaVersionOf(string text)
        {
            const string key = "\"schema_version\"";
            int at = text.IndexOf(key, StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), "源文本须有 schema_version");
            int colon = text.IndexOf(':', at + key.Length);
            int end = colon + 1;
            while (end < text.Length && (char.IsWhiteSpace(text[end]) || text[end] == ' ')) end++;
            int start = end;
            while (end < text.Length && char.IsDigit(text[end])) end++;
            Assert.That(end, Is.GreaterThan(start), "schema_version 须为整数");
            return int.Parse(text.Substring(start, end - start), System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>断言绑定被拒,且错误报文里含指定判据关键词。</summary>
        private static void AssertRejected(string json, string expectedKeyword)
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => BuildModulesBake.BakeFromSourceText(json));
            Assert.That(
                ex.Errors.Any(e => e.IndexOf(expectedKeyword, StringComparison.Ordinal) >= 0),
                Is.True,
                $"错误报文须点名判据「{expectedKeyword}」(否则任何 throw 都能让本夹具变绿):\n" +
                string.Join("\n", ex.Errors));
        }
    }
}
