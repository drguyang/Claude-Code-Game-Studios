// `world_buildslots.json`(建造槽位骨架 · ADR-022 §三)的烘焙 / 域门 / 读方镜像 / 端到端测试。
//
// 权威来源:
//   ADR-022 §二(唯一作者 = 关卡工具)· §三(整数格 + `BakedInitial`)· §四(C1–C6)
//   ADR-014 §三/§四(白名单 · 聚合 throw)· ADR-015 §三(单一整数格)
//
// ⚠️ **判别力纪律**(承 `build_modules_bake_test.cs` / `TD-clinic-01`):每条负向夹具都对应
//   一条**具体域门**,断言打的是错误报文里的**判据关键词**(字面量),不是「抛了就行」。
//   ⚠️ 关键词一律写字面量,**不**引用被测类的常量 —— 自指判据在变异时会两边一起动,
//   零红逃逸。
//
// ⚠️ **写盘纪律**:负向夹具在 `Path.GetTempPath()` 下造最小仓根(承 `.claude/rules/test-standards.md`
//   与 `FsWriteBoundaryGate`);真仓库 `assets/data/` 一字不动。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using UnityEngine;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    /// <summary>建造槽位骨架:域门 / 确定性 / 读方镜像 / 生产装载端到端。</summary>
    [TestFixture]
    public class world_buildslots_bake_test
    {
        private static string RepoRoot
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        // ══════════════════ 一 · 真种子(正向半边)══════════════════

        /// <summary>真种子过五道域门,且 Decor / Shell 两个分支都可达。</summary>
        /// <remarks><b>判别力</b>:任一门误判(把合法行当越界)⇒ 本测当场红;
        /// 导出器把某个区域丢掉 ⇒ 「Decor 与 Shell 均在」那条断言红。</remarks>
        [Test]
        public void test_worldBuildSlots_realSeed_passesAllGates()
        {
            WorldBuildSlotsBake.BakeOutput output = WorldBuildSlotsBake.BakeFromRepo(RepoRoot);

            Assert.That(output.Bound.Rows.Count, Is.GreaterThan(0), "真种子须非空");
            Assert.That(output.Cooked.Length, Is.GreaterThan(20), "产物须含 20 字节头 + 载荷");
            Assert.That(output.ConfigVersion, Is.Not.EqualTo(0u), "ConfigVersion 须由内容哈希派生");
            Assert.That(output.Bound.Width, Is.EqualTo(16), "声明范围 X 须 = 合成世界 16");
            Assert.That(output.Bound.Height, Is.EqualTo(1), "声明范围 Y 须 = 合成世界 1");
            Assert.That(output.Bound.Depth, Is.EqualTo(16), "声明范围 Z 须 = 合成世界 16");

            // Decor = SlotType 序数 5;Shell = 6(写 401 字面量,不引用被测枚举 —— 自指判据会逃逸)。
            Assert.That(output.Bound.Rows.Any(r => r.TypeOrdinal == 5), Is.True,
                "种子须含 Decor 槽位(否则 Decor 分支不可达)");
            Assert.That(output.Bound.Rows.Any(r => r.TypeOrdinal == 6), Is.True,
                "种子须含 Shell 槽位(否则 Shell 分支不可达)");

            // `BakedInitial` **两侧都要有真实取值** —— 全 true = 「开档医馆已建满」= 玩家无可建格,
            // 与本轮目的相反;全 false = 该字段恒假,等于没测。两侧都断,任一侧塌掉本测即红。
            Assert.That(output.Bound.Rows.Any(r => r.BakedInitial), Is.True,
                "种子须至少一格 `baked_initial = true`(外壳在开档时就存在)");
            Assert.That(output.Bound.Rows.Any(r => !r.BakedInitial), Is.True,
                "种子须至少一格 `baked_initial = false`(空地可建;全 true ⇒ 玩家建不了任何东西)");
        }

        // ══════════════════ 二 · 生产核真的会拒(负向,走 DataBakeMenu)══════════════════

        /// <summary>越界格 ⇒ 生产核抛,且报文点名「超出声明范围」+ 坏格坐标。</summary>
        /// <remarks><b>判别力</b>:把 `ParseRow` 的「格超出声明范围」分支改成 `if (false)`
        /// ⇒ 本测红(不再抛)。</remarks>
        [Test]
        public void test_worldBuildSlots_productionCore_rejectsOutOfBoundsCell()
        {
            string tempRoot = MakeTempRepoRoot(
                "{\"schema_version\":2,\"level_id\":\"tmp\",\"world_size\":[4,1,4]," +
                "\"slots\":[{\"cell\":[9,0,0],\"slot_type\":\"Decor\",\"baked_initial\":false}]}");
            try
            {
                var ex = Assert.Throws<BakeValidationException>(
                    () => DataBakeMenu.BakeWorldBuildSlotsCore(tempRoot));

                Assert.That(ex.Errors.Any(e => e.IndexOf("超出声明范围", StringComparison.Ordinal) >= 0),
                    Is.True, "报文须点名判据「超出声明范围」:\n" + string.Join("\n", ex.Errors));
                Assert.That(ex.Errors.Any(e => e.IndexOf("(9,0,0)", StringComparison.Ordinal) >= 0),
                    Is.True, "报文须点名坏格 (9,0,0)(否则读者不知道该改哪一行)");
                // ⚠️ 报文前缀是 `{FileName}:`,即 `world_buildslots.json:` —— 首版这里写成
                //    `world_buildslots:`(少了 `.json`),首次实跑即红。**是夹具写错,不是生产错**;
                //    订正依据 = `WorldBuildSlotsBinder` 全部 `errors.Add($"{FileName}:…")`。
                Assert.That(ex.Errors.Any(e => e.StartsWith("world_buildslots.json:", StringComparison.Ordinal)),
                    Is.True, "报文须以 `world_buildslots.json:` 起头(多表聚合时读者要靠它定位是哪张表):\n"
                             + string.Join("\n", ex.Errors));
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
            }
        }

        /// <summary>同一最小仓根换成合法行 ⇒ 生产核**放行**(证明上一条不是「核恒抛」)。</summary>
        /// <remarks><b>判别力</b>:若生产核被改成无条件抛,上一条仍绿而本条红 —— 成对使用。</remarks>
        [Test]
        public void test_worldBuildSlots_productionCore_acceptsValidRow()
        {
            string tempRoot = MakeTempRepoRoot(
                "{\"schema_version\":2,\"level_id\":\"tmp\",\"world_size\":[4,1,4]," +
                "\"slots\":[{\"cell\":[1,0,1],\"slot_type\":\"Decor\",\"baked_initial\":false}]}");
            try
            {
                WorldBuildSlotsBake.BakeOutput output =
                    DataBakeMenu.BakeWorldBuildSlotsCore(tempRoot);

                Assert.That(output.Bound.Rows.Count, Is.EqualTo(1));
                Assert.That(output.Bound.Rows[0].X, Is.EqualTo(1));
                Assert.That(output.Bound.Rows[0].TypeOrdinal, Is.EqualTo(5), "Decor 序数须为 5");
                Assert.That(output.Bound.Rows[0].BakedInitial, Is.False);
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
            }
        }

        // ══════════════════ 三 · 其余域门(负向)══════════════════

        /// <summary>空 `slots[]` ⇒ 拒。</summary>
        [Test]
        public void test_worldBuildSlots_emptyTable_isRejected()
            => AssertRejected(
                "{\"schema_version\":2,\"level_id\":\"t\",\"world_size\":[4,1,4],\"slots\":[]}",
                "为空表");

        /// <summary>重复格 ⇒ 拒。</summary>
        [Test]
        public void test_worldBuildSlots_duplicateCell_isRejected()
            => AssertRejected(
                "{\"schema_version\":2,\"level_id\":\"t\",\"world_size\":[4,1,4],\"slots\":[" +
                "{\"cell\":[1,0,1],\"slot_type\":\"Decor\",\"baked_initial\":false}," +
                "{\"cell\":[1,0,1],\"slot_type\":\"Shell\",\"baked_initial\":false}]}",
                "重复");

        /// <summary>`slot_type = "None"` ⇒ 拒(哨兵不是合法类型)。</summary>
        [Test]
        public void test_worldBuildSlots_noneSlotType_isRejected()
            => AssertRejected(
                "{\"schema_version\":2,\"level_id\":\"t\",\"world_size\":[4,1,4],\"slots\":[" +
                "{\"cell\":[1,0,1],\"slot_type\":\"None\",\"baked_initial\":false}]}",
                "SlotType 闭集");

        /// <summary>`baked_initial` 缺席 ⇒ 拒(「没写」≠「明写 false」)。</summary>
        [Test]
        public void test_worldBuildSlots_missingBakedInitial_isRejected()
            => AssertRejected(
                "{\"schema_version\":2,\"level_id\":\"t\",\"world_size\":[4,1,4],\"slots\":[" +
                "{\"cell\":[1,0,1],\"slot_type\":\"Decor\"}]}",
                "baked_initial");

        /// <summary>未知键 ⇒ 拒(ADR-014 §三)。</summary>
        [Test]
        public void test_worldBuildSlots_unknownKey_isRejected()
            => AssertRejected(
                "{\"schema_version\":2,\"level_id\":\"t\",\"world_size\":[4,1,4],\"slots\":[" +
                "{\"cell\":[1,0,1],\"slot_type\":\"Decor\",\"baked_initial\":false,\"module_id\":null,\"tier\":2}]}",
                "未知键");

        // ══════════════════ 三之二 · schema 2 的 `module_id` 域门 ══════════════════
        //
        // ⚠️ 这两条是 S20-20 挂标 #2 的核心:此前 `baked_initial = true` 只声明「这格开档
        //    已占用」,**说不出被什么占用** ⇒ 运行期算不出足迹、发不出 `structure_id`
        //    (GDD 规则三要求初始家具进实例表)。`module_id` 就是那个缺的字段。
        //    两侧都拒:true 无 id(算不出)与 false 有 id(自相矛盾)。

        /// <summary>`baked_initial = true` 但 `module_id` 为 null ⇒ 拒。</summary>
        /// <remarks><b>判别力</b>:把 `TryReadModuleId` 的 `if (bakedInitial)` 分支停用
        /// ⇒ 本条红(不再拒,开档家具静默丢失足迹)。</remarks>
        [Test]
        public void test_worldBuildSlots_bakedInitialTrueWithoutModuleId_isRejected()
            => AssertRejected(
                "{\"schema_version\":2,\"level_id\":\"t\",\"world_size\":[4,1,4],\"slots\":[" +
                "{\"cell\":[1,0,1],\"slot_type\":\"Shell\",\"baked_initial\":true,\"module_id\":null}]}",
                "module_id");

        /// <summary>`baked_initial = true` 且**整个字段缺席** ⇒ 同样拒(缺 = 作者遗漏)。</summary>
        /// <remarks><b>判别力</b>:若把「缺席」当成合法 null 放行 ⇒ 本条红;
        /// 它与上一条各挡一面(显式 null vs 漏写),不能互相代偿。</remarks>
        [Test]
        public void test_worldBuildSlots_bakedInitialTrueMissingModuleIdField_isRejected()
            => AssertRejected(
                "{\"schema_version\":2,\"level_id\":\"t\",\"world_size\":[4,1,4],\"slots\":[" +
                "{\"cell\":[1,0,1],\"slot_type\":\"Shell\",\"baked_initial\":true}]}",
                "module_id");

        /// <summary>`baked_initial = false` 却写了 `module_id` ⇒ 拒(自相矛盾)。</summary>
        /// <remarks><b>判别力</b>:删掉 `TryReadModuleId` 的 `!isNull` 分支 ⇒ 本条红。
        /// 这条**不是**上两条的对称赘述 —— 它挡的是反方向的错:读者会以为「有家具但没占」。</remarks>
        [Test]
        public void test_worldBuildSlots_bakedInitialFalseWithModuleId_isRejected()
            => AssertRejected(
                "{\"schema_version\":2,\"level_id\":\"t\",\"world_size\":[4,1,4],\"slots\":[" +
                "{\"cell\":[1,0,1],\"slot_type\":\"Decor\",\"baked_initial\":false,\"module_id\":7}]}",
                "module_id");

        /// <summary>`module_id = 0`(哨兵位)⇒ 拒。</summary>
        /// <remarks><b>判别力</b>:把 `n.Int &lt; 1` 的比较改成 `&lt; 0` ⇒ 本条红。</remarks>
        [Test]
        public void test_worldBuildSlots_moduleIdZero_isRejected()
            => AssertRejected(
                "{\"schema_version\":2,\"level_id\":\"t\",\"world_size\":[4,1,4],\"slots\":[" +
                "{\"cell\":[1,0,1],\"slot_type\":\"Shell\",\"baked_initial\":true,\"module_id\":0}]}",
                "module_id");

        /// <summary>合法行(`true` + 真 id)⇒ **放行**,且 id 原样保留(证明上四条不是「恒拒」)。</summary>
        [Test]
        public void test_worldBuildSlots_bakedInitialTrueWithModuleId_isAccepted()
        {
            WorldBuildSlotsBinder.BindResult r = WorldBuildSlotsBinder.Bind(
                "{\"schema_version\":2,\"level_id\":\"t\",\"world_size\":[4,1,4],\"slots\":[" +
                "{\"cell\":[1,0,1],\"slot_type\":\"Shell\",\"baked_initial\":true,\"module_id\":8}]}");

            Assert.That(r.Rows.Count, Is.EqualTo(1));
            Assert.That(r.Rows[0].BakedInitial, Is.True);
            Assert.That(r.Rows[0].ModuleId, Is.EqualTo(8), "module_id 须原样保留,不做映射");
        }

        // ══════════════════ 四 · 确定性 + 读方 ↔ 写方镜像 ══════════════════

        /// <summary>同源两次烘焙逐位一致(ADR-014 §二)。</summary>
        [Test]
        public void test_worldBuildSlots_bakeIsDeterministic()
        {
            byte[] a = WorldBuildSlotsBake.BakeFromRepo(RepoRoot).Cooked;
            byte[] b = WorldBuildSlotsBake.BakeFromRepo(RepoRoot).Cooked;
            Assert.That(a, Is.EqualTo(b), "同源两次烘焙字节不等 ⇒ 有字典迭代序 / 本地时间进了产物");
        }

        /// <summary>cooked 往返:编码 → 解码 → 行逐字段相等(行序双方均 (x,y,z) 升序)。</summary>
        /// <remarks><b>判别力</b>:写方 `CompareCell` 与读方字段序任一错位 ⇒ 本测红。</remarks>
        [Test]
        public void test_worldBuildSlots_cookedRoundTrip_isFieldwiseIdentical()
        {
            WorldBuildSlotsBake.BakeOutput output = WorldBuildSlotsBake.BakeFromRepo(RepoRoot);
            WorldBuildSlotsDataSet decoded = WorldBuildSlotsCookedCodec.Read(output.Cooked);

            var expected = new List<WorldBuildSlotsBinder.SlotRow>(output.Bound.Rows);
            expected.Sort((p, q) =>
            {
                int cmp = p.X.CompareTo(q.X);
                if (cmp != 0) return cmp;
                cmp = p.Y.CompareTo(q.Y);
                if (cmp != 0) return cmp;
                return p.Z.CompareTo(q.Z);
            });

            Assert.That(decoded.Rows.Count, Is.EqualTo(expected.Count), "行数须一致");
            Assert.That(decoded.Width, Is.EqualTo(output.Bound.Width));
            Assert.That(decoded.Height, Is.EqualTo(output.Bound.Height));
            Assert.That(decoded.Depth, Is.EqualTo(output.Bound.Depth));
            Assert.That(decoded.ConfigVersion, Is.EqualTo(output.ConfigVersion),
                "头槽 ConfigVersion 须与烘焙输出一致");
            Assert.That(decoded.SchemaVersion, Is.EqualTo(2u), "头槽 schema_version 须 = 2");

            for (int i = 0; i < expected.Count; i++)
            {
                WorldBuildSlotRow got = decoded.Rows[i];
                WorldBuildSlotsBinder.SlotRow want = expected[i];
                Assert.That(got.Cell.X, Is.EqualTo(want.X), $"rows[{i}].x");
                Assert.That(got.Cell.Y, Is.EqualTo(want.Y), $"rows[{i}].y");
                Assert.That(got.Cell.Z, Is.EqualTo(want.Z), $"rows[{i}].z");
                Assert.That(got.AllowedMask, Is.EqualTo(1 << want.TypeOrdinal),
                    $"rows[{i}].allowed_mask 须 = 1 << {want.TypeOrdinal}");
                Assert.That(got.BakedInitial, Is.EqualTo(want.BakedInitial), $"rows[{i}].baked_initial");
                // schema 2:module_id 往返(schema 1 时这一行无从写起 —— 字段根本不存在)
                Assert.That(got.ModuleId, Is.EqualTo(want.ModuleId), $"rows[{i}].module_id");
            }
        }

        /// <summary>盘上产物与现烘字节**逐位相同**(忘了重烘 ⇒ 红)。</summary>
        [Test]
        public void test_worldBuildSlots_diskMatchesFreshBake()
        {
            string path = Path.Combine(
                RepoRoot, "unity", "Assets", "DataCooked", "world_buildslots.cooked.bytes");
            Assert.That(File.Exists(path), Is.True,
                $"产物不存在:{path} —— 先跑关卡工具导出 + 「烘焙 world_buildslots」菜单");

            byte[] disk = File.ReadAllBytes(path);
            byte[] fresh = WorldBuildSlotsBake.BakeFromRepo(RepoRoot).Cooked;
            Assert.That(disk, Is.EqualTo(fresh), "盘上产物与现烘字节不一致 ⇒ 忘了重烘");
        }

        /// <summary>
        /// 盘上产物**逐字段**等于作者态源 JSON —— 即「产物内容对不对」,而非「产物与现烘一致」。
        /// </summary>
        /// <remarks>
        /// <para><b>为什么必须有这一条(2026-10-28 补 · 结清 `review-s20-20` §七-3)</b>:
        /// 上一条 `test_worldBuildSlots_diskMatchesFreshBake` 只证明**盘上 == 现烘**。
        /// 若烘焙器自身有 bug(`baked_initial` 全写成 false / `module_id` 写成 -1 / 行序乱),
        /// 两边**一样地错** ⇒ 那条**照样绿**。本条把判据钉在**作者态源 JSON**上,
        /// 绕开烘焙器(正则逐字段解析,不调 `WorldBuildSlotsBake`、不调生产 codec)——
        /// 两端都不经被测件,故无自指。</para>
        /// <para><b>判别力(实测 M7 · 2026-10-28)</b>:把**盘上产物**第 0 行的 `baked_initial`
        /// 由 `1` 改成 `0`(偏移 52)⇒ 本测红,报文点名「第 0 行与源 JSON 不符 ——
        /// 产物 = (1,0,1,False,8),源 = (1,0,1,True,8)」。</para>
        /// <para>⚠️ <b>如实登记 M7 的判别力边界</b>:该变异**不是本测独占** —— 同轮另有 4 条红
        /// (`diskMatchesFreshBake` / `bakedInitial_isAppliedThroughProductionAssembly` /
        /// `minimalWorld_catalogNonEmpty` / `reset_returnsToBaselineNotEmpty`)。
        /// 也就是说「产物内容被改」这件事,**旧夹具也能察觉**(经由「盘上 ≠ 现烘」这条)。
        /// 本测**独占**的那一面是「**烘焙器误读源**」—— 此时盘上 == 现烘(两边一样地错),
        /// `diskMatchesFreshBake` **必绿**,只有本测能抓。</para>
        /// <para>⚠️ <b>该独占面本轮未能实测</b>:尝试的变异(M8:Binder 的 `bakedInitial = n.Bool`
        /// 改恒 `false`)被**烘焙期域门**当场拒收(`baked_initial = 0` 的行 `module_id` 须为 -1),
        /// 重烘失败、产物未变 ⇒ 造不出「盘上 == 现烘但内容错」的现场。
        /// ⇒ 该面**只经离线推演**,不标已证 —— 承「本轮未运行其判据 ⇒ 不得标 Proven ✅」。
        /// 要实测它,须先找一个**能同时过域门**的误读形态(见 `TD-building-03`)。</para>
        /// <para>⚠️ <b>零 JSON 库</b>:`EditMode.asmdef` 是 `overrideReferences: true`,
        /// `precompiledReferences` 只有 `nunit.framework.dll` / `Mono.Cecil.dll` ⇒ 测试树
        /// **够不着** Newtonsoft。故用正则按行解析 —— 源文件是「一槽位一行」的扁平结构,
        /// 正则足以逐字段取全,且比引一个 JSON 库更不容易把格式错误读成「解析成功」。</para>
        /// <para>⚠️ <b>本测不验什么</b>:`allowed_mask` 的位含义归 `BuildModulesBinder`
        /// 与槽位域门(已由本文件其余夹具覆盖),本条只比对「产物 vs 源」的**逐字段相等**。</para>
        /// </remarks>
        [Test]
        public void test_worldBuildSlots_productMatchesSourceJson_fieldByField()
        {
            string jsonPath = Path.Combine(RepoRoot, "assets", "data", "world_buildslots.json");
            string cookedPath = Path.Combine(
                RepoRoot, "unity", "Assets", "DataCooked", "world_buildslots.cooked.bytes");
            Assert.That(File.Exists(jsonPath), Is.True, $"源 JSON 不存在:{jsonPath}");
            Assert.That(File.Exists(cookedPath), Is.True, $"产物不存在:{cookedPath}");

            string json = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);

            // ── 源侧:顶层三件 ──
            var schemaM = System.Text.RegularExpressions.Regex.Match(
                json, @"""schema_version""\s*:\s*(\d+)");
            Assert.That(schemaM.Success, Is.True, "源 JSON 里取不到 schema_version");
            uint srcSchema = uint.Parse(schemaM.Groups[1].Value);

            var sizeM = System.Text.RegularExpressions.Regex.Match(
                json, @"""world_size""\s*:\s*\[\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*\]");
            Assert.That(sizeM.Success, Is.True, "源 JSON 里取不到 world_size");
            var srcSize = (int.Parse(sizeM.Groups[1].Value),
                           int.Parse(sizeM.Groups[2].Value),
                           int.Parse(sizeM.Groups[3].Value));

            // ── 源侧:逐槽位(每行一条;字段序与 `assets/data/world_buildslots.json` 一致)──
            var rowRe = new System.Text.RegularExpressions.Regex(
                @"\{\s*""cell""\s*:\s*\[\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*\]\s*," +
                @"\s*""slot_type""\s*:\s*""(\w+)""\s*," +
                @"\s*""baked_initial""\s*:\s*(true|false)\s*," +
                @"\s*""module_id""\s*:\s*(null|-?\d+)\s*\}");
            var srcRows = new List<(int X, int Y, int Z, bool Baked, int ModuleId)>();
            foreach (System.Text.RegularExpressions.Match m in rowRe.Matches(json))
            {
                int mid = m.Groups[6].Value == "null" ? -1 : int.Parse(m.Groups[6].Value);
                srcRows.Add((int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value),
                             int.Parse(m.Groups[3].Value), m.Groups[5].Value == "true", mid));
            }
            Assert.That(srcRows.Count, Is.GreaterThan(0),
                "扫描键守卫 —— 源侧一条槽位都没解析出来,下面的比对会被「两边都空」满足");

            // ── 产物侧:独立解码(不经生产 codec,避免自指)──
            byte[] b = File.ReadAllBytes(cookedPath);
            Assert.That(b.Length, Is.GreaterThanOrEqualTo(36), "产物短于「头 20 + 尺寸 16」");
            Assert.That(System.Text.Encoding.ASCII.GetString(b, 0, 4), Is.EqualTo("DYJC"),
                "魔数不符 —— 产物损坏或不是本产物");

            int off = 20;   // magic(4) + formatVer(4) + schemaVer(4) + ConfigVersion(4) + payloadLen(4)
            int ReadI32() { int v = BitConverter.ToInt32(b, off); off += 4; return v; }

            // ⚠️ 头字段偏移:`0` magic · `4` formatVer · `8` schemaVer · `12` ConfigVersion · `16` payloadLen。
            //    (`CookedFormat.HeaderSize = 20` = 4 + 四个 u32。)
            Assert.That(BitConverter.ToUInt32(b, 8), Is.EqualTo(srcSchema),
                "产物 schemaVer 须等于源 schema_version");
            int cx = ReadI32(), cy = ReadI32(), cz = ReadI32(), n = ReadI32();
            Assert.That((cx, cy, cz), Is.EqualTo(srcSize), "产物 world_size 须等于源 world_size");
            Assert.That(n, Is.EqualTo(srcRows.Count), "产物 row_count 须等于源槽位数");

            var prodRows = new List<(int X, int Y, int Z, bool Baked, int ModuleId)>();
            for (int i = 0; i < n; i++)
            {
                int x = ReadI32(), y = ReadI32(), z = ReadI32();
                ReadI32();                       // allowed_mask —— 归槽位域门,本条不比
                bool baked = ReadI32() == 1;
                prodRows.Add((x, y, z, baked, ReadI32()));
            }
            Assert.That(off, Is.EqualTo(b.Length), "解到文件尾 —— 字节数须与 row_count 自洽");

            // ── 逐行比对(产物行序 = (x,y,z) 升序,与源同序)──
            for (int i = 0; i < n; i++)
            {
                Assert.That(prodRows[i], Is.EqualTo(srcRows[i]),
                    $"第 {i} 行与源 JSON 不符 —— 产物 = {prodRows[i]},源 = {srcRows[i]}");
            }

            // ── 两个计数的显式点名(逐行比对已蕴含,但让失败报文直说「几件家具」)──
            Assert.That(prodRows.Count(r => r.Baked), Is.EqualTo(srcRows.Count(r => r.Baked)),
                "产物 baked_initial 条数须等于源");
            Assert.That(prodRows.Count(r => r.ModuleId >= 1), Is.EqualTo(srcRows.Count(r => r.ModuleId >= 1)),
                "产物带 module_id 的条数须等于源(开档家具必须知道自己是哪件)");
        }

        // ══════════════════ 五 · 端到端(生产装配路径)══════════════════

        /// <summary>
        /// `MinimalWorldFactory.Create()` 的目录**非空**,且真实槽位跑
        /// `PlaceableChecker.Check` 不再返回 `SlotNotFound`。
        /// </summary>
        /// <remarks>
        /// <para><b>这是本任务的目的位</b>:此前 `RegisterSlot` 零生产调用点 ⇒ 恒 `SlotNotFound`。</para>
        /// <para><b>判别力</b>:把 `MinimalWorldFactory.Create()` 里的 `RegisterSlot` 循环删掉
        /// ⇒ 本测红(`Count == 0` 与 `SlotNotFound` 两条断言同时红)。</para>
        /// </remarks>
        [Test]
        public void test_worldBuildSlots_minimalWorld_catalogNonEmptyAndCheckNotSlotNotFound()
        {
            World world = MinimalWorldFactory.Create();

            Assert.That(world.SlotCatalog.Count, Is.GreaterThan(0),
                "生产装配的槽位目录须非空 —— 否则玩家建不了任何东西");
            Assert.That(world.SlotCatalog.Count, Is.EqualTo(
                    WorldBuildSlotsBake.BakeFromRepo(RepoRoot).Bound.Rows.Count),
                "目录格数须等于烘焙产物行数(装载不得丢行)");

            // Decor 3×3 区里的一格**空地**(种子定义见 P0ClinicLevel:6..8 × 6..8;
            // (6,0,6) 是唯一那格 `baked_initial = true` 的已占用格 —— 刻意不用它:
            // 🔁 2026-10-28(S20-20 挂标 #2)订正 —— 原文「运行期今日不应用初始占用」已过期,
            //    基线**现已应用**(`World.ApplyBakedInitial`),用 (6,0,6) 会得到 `Occupied`;
            //    本条判的是「空地可建」,所以仍取空地 (7,0,7)。下面另立一条腿专门判基线。
            var anchor = new WorldPos(7, 0, 7);
            Assert.That(world.SlotCatalog.IsValidSlot(anchor), Is.True,
                "Decor 区的锚点格须在目录内(种子与装载的坐标对不上 ⇒ 数据与世界不是同一张图纸)");

            var checker = new PlaceableChecker(
                world.SlotCatalog,
                new OneModuleCatalog(1, SlotType.Decor, new[] { new WorldPos(0, 0, 0) }),
                new AlwaysHasStock(),
                new NeverOccupied(),
                new NobodyPresent());

            PlaceableResult result = checker.Check(1, anchor);

            Assert.That(result, Is.Not.EqualTo(PlaceableResult.SlotNotFound),
                "真实槽位 + 匹配模块不得再返回 SlotNotFound(本条 = 本任务的目的判据)");
            Assert.That(result, Is.EqualTo(PlaceableResult.Success),
                $"全条件应满足(实得 {result})");
        }

        // ══════════════════ 六 · 开档基线真的应用了(S20-20 挂标 #2)══════════════════
        //
        // ⚠️ 这一节判的是 **`Overlay(0) := BakedInitial`**(GDD 规则三 / 评审裁定 B3)。
        //    此前 `BuildSlotCatalog` 无承载字段 ⇒ 数据声明了、运行期不应用;那正是挂标 #2。
        //
        // ⚠️ 走**生产装配入口** `CompositionRoot.Assemble(world:)`,不是测试手调
        //    `world.ApplyBakedInitial` —— 后者只能证明「那个方法能用」,证明不了
        //    「装配路径会调它」。挂标 #2 的病灶正在装配这一层。

        /// <summary>经生产装配 ⇒ 基线占用落在**种子声明的那几格**上,且初始家具进了实例表。</summary>
        /// <remarks>
        /// <para><b>判别力</b>:把 `CompositionRoot` 里那行 `world.ApplyBakedInitial(modulesCatalog)`
        /// 删掉 ⇒ `BakedInitialStructureCount == 0` 与「(1,0,1) 不可走」两条同时红。</para>
        /// <para>⚠️ **格数不写死**(种子一改就红且红得离真因很远);改判「种子声明了哪些格,
        /// 那几格就占着」—— 判据从数据推出,不从常量抄。</para>
        /// </remarks>
        [Test]
        public void test_worldBuildSlots_bakedInitial_isAppliedThroughProductionAssembly()
        {
            World world = MinimalWorldFactory.Create();
            CompositionRoot.Assemble(world: world);

            WorldBuildSlotsBake.BakeOutput output = WorldBuildSlotsBake.BakeFromRepo(RepoRoot);
            var declared = output.Bound.Rows.Where(r => r.BakedInitial).ToList();
            Assert.That(declared.Count, Is.GreaterThan(0),
                "前置:种子须至少一格 baked_initial = true(否则本节零观察面)");

            Assert.That(world.BakedInitialStructureCount, Is.EqualTo(declared.Count),
                $"已应用的初始家具数须 = 种子声明数 {declared.Count}(数据说 5 格,运行期就得有 5 件)");

            foreach (WorldBuildSlotsBinder.SlotRow row in declared)
            {
                var cell = new WorldPos(row.X, row.Y, row.Z);
                Assert.That(world.IsSlotOccupied(cell), Is.True,
                    $"({row.X},{row.Y},{row.Z}) 声明了 baked_initial = true ⇒ 开档须已占用");
                Assert.That(world.IsEffectivelyWalkable(cell), Is.False,
                    $"({row.X},{row.Y},{row.Z}) 被初始家具占着 ⇒ 27/13 读到的须是不可走");
                Assert.That(world.GetModuleAt(cell), Is.EqualTo(row.ModuleId),
                    $"({row.X},{row.Y},{row.Z}) 的模块 id 须是种子写的 {row.ModuleId} —— " +
                    "「占了但不知道被什么占」等于没应用");
            }

            // 反向半边:非 baked_initial 的槽位必须是**空的** —— 否则上面那些断言
            // 可能只是「全世界都占着」(那会让它们全部无判别力)。
            foreach (WorldBuildSlotsBinder.SlotRow row in
                     output.Bound.Rows.Where(r => !r.BakedInitial).Take(3))
            {
                var cell = new WorldPos(row.X, row.Y, row.Z);
                Assert.That(world.IsSlotOccupied(cell), Is.False,
                    $"({row.X},{row.Y},{row.Z}) 是空地 ⇒ 不得被基线占(否则「基线应用了」与" +
                    "「整个骨架都占着」不可区分)");
            }
        }

        /// <summary>
        /// 发号确定性夹具用的模块 id = 7(`chamber_pot`,Decor,足迹 `[[0,0,0]]` 单格)。
        /// <para>⚠️ 写字面量而非引用目录常量:自指判据在变异时两边一起动,零红逃逸
        /// (承本文件头的判别力纪律)。</para>
        /// </summary>
        private const int DecorModuleId = 7;

        /// <summary>
        /// 基线发号**确定性**:同一份数据跑两遍 ⇒ 同一批 `structure_id`,且 id 恰为 1..N 连续。
        /// <para>⚠️ 这是 GDD 规则三「`Overlay` 是派生态、可逐条重建」的前提 —— 发号若随
        /// 字典迭代序漂移,两次读档会得到两套 id,而下游(24 的布局析出)按 id 引用。</para>
        /// </summary>
        /// <remarks>
        /// <para>🔁 <b>2026-10-28 证伪轮 M1/M2 双逃逸后重写(原文不删)</b>:首版走
        /// `CompositionRoot.Assemble` 造世界,于是 **M1(装配层不应用基线)下两边都是空集**
        /// —— `ids` 为空、`BakedInitialStructureCount` 都是 0 ⇒ **三条断言全部平凡成立**。
        /// 这是本项目反复抓过的「**空集借绿**」形态(同 `S-2` 的空子集借绿)。
        /// 现改为**手造骨架**:① 显式前置断言 `ids.Count &gt; 0`(空集直接红,堵死平凡成立);
        /// ② 不走装配层 ⇒ 与 M1 解耦,本条专判 `World` 自己的发号属性。</para>
        /// <para><b>判别力</b>(M2 已实测):把 `baked.Sort(...)` 停用 ⇒ 发号顺序退回
        /// `BuildSlotCatalog._slots` 的枚举序。**构造的关键**是让插入序 ≠ (x,y,z) 升序
        /// (下方刻意乱序注册)⇒ 排序一旦停用,id 与格的对应就变 ⇒ 本条红。</para>
        /// </remarks>
        [Test]
        public void test_worldBuildSlots_bakedInitial_idsAreDeterministicAndContiguous()
        {
            // ⚠️ **刻意乱序注册**(z 递减),使「插入序」与「(x,y,z) 升序」**不同** ——
            //    否则停用排序也得到同一结果,夹具必然假绿(M2 首版就是这么逃逸的)。
            BuildSlotCatalog MakeSkeleton()
            {
                var catalog = new BuildSlotCatalog();
                for (int i = 3; i >= 1; i--)
                    catalog.RegisterSlot(new BuildSlot(
                        new WorldPos(i, 0, 1), BitMask.Single(SlotType.Decor),
                        bakedInitial: true, moduleId: DecorModuleId));
                return catalog;
            }

            World MakeWorld()
            {
                var terrain = new TerrainCell[8, 1, 8];
                for (int x = 0; x < 8; x++)
                    for (int z = 0; z < 8; z++)
                        terrain[x, 0, z] = new TerrainCell(true, 0);
                var poi = new int[64];
                var resource = new int[64];
                for (int i = 0; i < 64; i++) { poi[i] = -1; resource[i] = -1; }
                return new World(new WorldGeometry(terrain, poi, resource), MakeSkeleton());
            }

            IModuleCatalog modules = new BuildModulesCatalog(DataCookedDisk.LoadBuildModules());

            World first = MakeWorld();
            first.ApplyBakedInitial(modules);
            World second = MakeWorld();
            second.ApplyBakedInitial(modules);

            // ── 前置:**非空**(堵死「空集借绿」—— 首版正是死在这里)──
            Assert.That(first.BakedInitialStructureCount, Is.EqualTo(3),
                "前置:3 格 baked_initial ⇒ 须真有 3 件初始家具;" +
                "为 0 说明基线未应用(此时下面每条都会平凡成立 = 假绿)");

            Assert.That(first.BakedInitialStructureCount, Is.EqualTo(second.BakedInitialStructureCount),
                "两次装配的初始家具数须相同");

            var ids = new List<long>();
            foreach (StructureInstance inst in first.Structures.EnumerateInstances())
                ids.Add(inst.StructureId);
            ids.Sort();
            Assert.That(ids, Is.EqualTo(
                    Enumerable.Range(1, ids.Count).Select(i => (long)i).ToList()),
                "基线实例 id 须恰为 1..N 连续(空号 = 发号路径有别的来源)");

            // ── 本条的主判据:**id 与格的对应由 (x,y,z) 升序决定**,与注册序无关 ──
            //    停用排序 ⇒ 按注册序(z 递减)发号 ⇒ x=1 的格拿到 id 3 ⇒ 红。
            for (int x = 1; x <= 3; x++)
            {
                long id = first.Structures.GetInstanceAt(new WorldPos(x, 0, 1));
                Assert.That(id, Is.EqualTo((long)x),
                    $"格 ({x},0,1) 的 structure_id 须 = {x} —— 发号按 (x,y,z) 升序," +
                    "与骨架的注册序无关;红在此处 = 排序没生效(字典枚举序漏进来了)");
            }

            // ── 同一格在两次装配里得到**同一个** id ──
            foreach (StructureInstance inst in first.Structures.EnumerateInstances())
            {
                long otherId = second.Structures.GetInstanceAt(inst.Anchor);
                Assert.That(otherId, Is.EqualTo(inst.StructureId),
                    $"格 ({inst.Anchor.X},{inst.Anchor.Y},{inst.Anchor.Z}) 两次装配的 id 不同 ⇒ 发号非确定性");
            }
        }

        /// <summary>
        /// `ApplyBakedInitial` 的**只许一次**守卫:第二次调用须 `throw`,且报文点名「已应用过一次」。
        /// <para>⚠️ 立此条缘由(2026-10-28 自查):该守卫此前**有代码、零夹具**
        /// (证据件 §七-1)。它挡的是「基线把 id 1…N 重新 `Restore` 到已被玩家改动过的实例表上,
        /// 静默覆盖玩家同号结构」—— 静默失败,故必须有证明。</para>
        /// </summary>
        /// <remarks><b>判别力</b>:删掉 `if (_moduleCatalogForBaseline != null) throw` 那三行
        /// ⇒ 本条红(第二次调用不抛)。</remarks>
        [Test]
        public void test_worldBuildSlots_applyBakedInitial_secondCallThrows()
        {
            BuildSlotCatalog MakeSkeleton()
            {
                var catalog = new BuildSlotCatalog();
                catalog.RegisterSlot(new BuildSlot(
                    new WorldPos(1, 0, 1), BitMask.Single(SlotType.Decor),
                    bakedInitial: true, moduleId: DecorModuleId));
                return catalog;
            }

            var terrain = new TerrainCell[8, 1, 8];
            for (int x = 0; x < 8; x++)
                for (int z = 0; z < 8; z++)
                    terrain[x, 0, z] = new TerrainCell(true, 0);
            var poi = new int[64];
            var resource = new int[64];
            for (int i = 0; i < 64; i++) { poi[i] = -1; resource[i] = -1; }

            var world = new World(new WorldGeometry(terrain, poi, resource), MakeSkeleton());
            IModuleCatalog modules = new BuildModulesCatalog(DataCookedDisk.LoadBuildModules());

            world.ApplyBakedInitial(modules);   // 第一次:允许

            var ex = Assert.Throws<InvalidOperationException>(
                () => world.ApplyBakedInitial(modules));
            Assert.That(ex.Message.IndexOf("已应用过一次", StringComparison.Ordinal) >= 0, Is.True,
                "报文须点名「已应用过一次」(否则任何 InvalidOperationException 都能让本夹具变绿):\n"
                + ex.Message);
        }

        /// <summary>
        /// 骨架指向**目录里不存在**的模块 ⇒ `ApplyBakedInitial` 须 `throw` 而非静默跳过,
        /// **且抛出时世界回到「未应用」态**(原子性)。
        /// <para>⚠️ 立此条缘由(2026-10-28 自查):该 fail-closed 分支此前**零夹具**
        /// (证据件 §七-2)。静默跳过 = 开档少一件家具且**无任何信号**,
        /// 正是本挂标要修的那个缺陷本身。</para>
        /// <para>🔁 <b>2026-10-28 评审 B-2 修复(原文不删,就地加注)</b>:首版骨架是**单格**
        /// (`moduleId: 999`),抛在 `i=0` ⇒ 后面那条「抛之前不得落任何实例」**恒真**
        /// (循环体一次都没跑完过,本来就不可能落库)。那不是「证明零落库」,是「没机会落库」。
        /// 现改为**三格,非法格在中间**:`[合法, 非法, 合法]` ⇒ 抛出时 i=0 的合法家具**已经**
        /// 落库并占格,回滚是否真的把它抹掉才成为**可观测**的事。这条同时钉住:
        /// ① fail-closed 会抛;② 抛后实例表空;③ 抛后占用表空;④ 抛后可重试(标志已复位)。
        /// </para>
        /// </summary>
        /// <remarks><b>判别力</b>:① 把那条 `throw` 换成 `continue` ⇒ 本条红(不抛);
        /// ② 删掉 `ApplyBakedInitial` 的 `catch` 回滚块 ⇒ 实例表 / 占用表两条红(半应用残留)。</remarks>
        [Test]
        public void test_worldBuildSlots_applyBakedInitial_moduleNotInCatalogThrows()
        {
            var catalog = new BuildSlotCatalog();
            // 999 不在 build_modules.json(实测只有 1..9)⇒ 必然查不到。
            // 合法件用与「已应用过一次」夹具同一个 DecorModuleId(目录内确有此 id)。
            // 排序键 = (x,y,z) 升序 ⇒ 索引 0 是 (1,0,1) 合法,索引 1 是 (2,0,1) 非法。
            catalog.RegisterSlot(new BuildSlot(
                new WorldPos(1, 0, 1), BitMask.Single(SlotType.Decor),
                bakedInitial: true, moduleId: DecorModuleId));
            catalog.RegisterSlot(new BuildSlot(
                new WorldPos(2, 0, 1), BitMask.Single(SlotType.Decor),
                bakedInitial: true, moduleId: 999));
            catalog.RegisterSlot(new BuildSlot(
                new WorldPos(3, 0, 1), BitMask.Single(SlotType.Decor),
                bakedInitial: true, moduleId: DecorModuleId));

            var terrain = new TerrainCell[8, 1, 8];
            for (int x = 0; x < 8; x++)
                for (int z = 0; z < 8; z++)
                    terrain[x, 0, z] = new TerrainCell(true, 0);
            var poi = new int[64];
            var resource = new int[64];
            for (int i = 0; i < 64; i++) { poi[i] = -1; resource[i] = -1; }

            var world = new World(new WorldGeometry(terrain, poi, resource), catalog);
            IModuleCatalog modules = new BuildModulesCatalog(DataCookedDisk.LoadBuildModules());

            // ── 前置:证明「抛之前确实落过库」,否则下面的「零残留」仍是恒真 ──
            // 用一次**不抛**的单格世界反证「合法格真的会落库 + 占格」,而不是空口假定。
            var probeCatalog = new BuildSlotCatalog();
            probeCatalog.RegisterSlot(new BuildSlot(
                new WorldPos(1, 0, 1), BitMask.Single(SlotType.Decor),
                bakedInitial: true, moduleId: DecorModuleId));
            var probe = new World(new WorldGeometry(terrain, poi, resource), probeCatalog);
            probe.ApplyBakedInitial(modules);
            Assert.That(probe.Structures.EnumerateInstances().Any(), Is.True,
                "前置失效:合法格本身就不落库 ⇒ 本夹具的「零残留」断言对回滚无判别力");
            Assert.That(probe.IsSlotOccupied(new WorldPos(1, 0, 1)), Is.True,
                "前置失效:合法格本身就不占格 ⇒ 本夹具的「占用归零」断言对回滚无判别力");

            // ── 被测:三格骨架,非法格在中间 ⇒ i=0 已落库,随后 i=1 抛 ──
            var ex = Assert.Throws<InvalidOperationException>(
                () => world.ApplyBakedInitial(modules));
            Assert.That(ex.Message.IndexOf("不在模块目录内", StringComparison.Ordinal) >= 0, Is.True,
                "报文须点名「不在模块目录内」:\n" + ex.Message);

            // ⚠️ 查**实例表**而不是 `BakedInitialStructureCount` —— 后者是回滚时一并复位的,
            //    单查它会与「从没跑过」同形。实例表才是「有没有东西真被落进去」的判据。
            Assert.That(world.Structures.EnumerateInstances().Any(), Is.False,
                "抛出时世界须回到「未应用」态 —— i=0 的合法件不得残留(半应用 = 开档少几件且无信号)");
            Assert.That(world.BakedInitialStructureCount, Is.EqualTo(0),
                "计数须一并复位(否则调用方读到「应用了 N 件」而世界是空的)");
            Assert.That(world.IsSlotOccupied(new WorldPos(1, 0, 1)), Is.False,
                "i=0 已占的格须被释放 —— 只清实例表不清占用表 = 那格永远不可走");

            // ── 重试:标志已复位 ⇒ 修好数据后可以再来一次(报文不得说「已应用过一次」) ──
            var fixedCatalog = new BuildSlotCatalog();
            fixedCatalog.RegisterSlot(new BuildSlot(
                new WorldPos(1, 0, 1), BitMask.Single(SlotType.Decor),
                bakedInitial: true, moduleId: DecorModuleId));
            var retryWorld = new World(new WorldGeometry(terrain, poi, resource), fixedCatalog);
            Assert.DoesNotThrow(() => retryWorld.ApplyBakedInitial(modules),
                "同一 world 抛过一次后,标志须已复位 ⇒ 再次调用不得报「已应用过一次」");
        }

        /// <summary>
        /// `World.Reset()` 回到**基线**而不是空世界(GDD 规则三的 `Overlay(t) := BakedInitial ⊕ 事件`)。
        /// <para>⚠️ 这条挡的是「读档丢房子」:重置是读档重放的第一步,若它清空基线,
        /// 玩家每次读档后外壳与开档家具全部消失 —— 而**所有既有夹具都看不出来**
        /// (它们造的世界没有基线)。</para>
        /// </summary>
        /// <remarks><b>判别力</b>:把 `World.Reset` 里的 `ApplyBakedInitialBaseline()` 调用删掉
        /// ⇒ 本条红(占用数从「基线数」变 0)。</remarks>
        [Test]
        public void test_worldBuildSlots_reset_returnsToBaselineNotEmpty()
        {
            World world = MinimalWorldFactory.Create();
            CompositionRoot.Assemble(world: world);

            int baselineStructures = world.BakedInitialStructureCount;
            int baselineOccupied = world.OccupiedCount;
            Assert.That(baselineStructures, Is.GreaterThan(0), "前置:须已应用基线");

            world.Reset();

            Assert.That(world.BakedInitialStructureCount, Is.EqualTo(baselineStructures),
                "重置后初始家具数须回到基线数(重置 = 回基线,不是回空 —— 规则三)");
            Assert.That(world.OccupiedCount, Is.EqualTo(baselineOccupied),
                "重置后占用数须回到基线数");
        }

        // ══════════════════ 辅助 ══════════════════

        /// <summary>在 `Path.GetTempPath()` 下造一个只含 `world_buildslots.json` 的最小仓根。</summary>
        private static string MakeTempRepoRoot(string json)
        {
            string tempRoot = Path.Combine(
                Path.GetTempPath(), "djc-buildslots-" + Guid.NewGuid().ToString("N"));
            string dataDir = Path.Combine(tempRoot, "assets", "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "world_buildslots.json"), json);
            return tempRoot;
        }

        /// <summary>断言绑定被拒,且错误报文里含指定**字面量**判据关键词。</summary>
        private static void AssertRejected(string json, string expectedKeyword)
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => WorldBuildSlotsBake.BakeFromSourceText(json));
            Assert.That(
                ex.Errors.Any(e => e.IndexOf(expectedKeyword, StringComparison.Ordinal) >= 0),
                Is.True,
                $"错误报文须点名判据「{expectedKeyword}」(否则任何 throw 都能让本夹具变绿):\n" +
                string.Join("\n", ex.Errors));
        }

        /// <summary>只有一个模块的目录(端到端用;不做别的判定)。</summary>
        private sealed class OneModuleCatalog : IModuleCatalog
        {
            private readonly ModuleDefinition _definition;

            public OneModuleCatalog(int moduleId, SlotType type, IReadOnlyList<WorldPos> occupancy)
            {
                _definition = new ModuleDefinition(
                    moduleId, type, occupancy, new[] { 0 }, cost: 1, buildItemKey: 0);
            }

            public ModuleDefinition? GetModuleDefinition(int moduleId)
                => moduleId == _definition.ModuleId ? _definition : (ModuleDefinition?)null;

            public bool ContainsModule(int moduleId) => moduleId == _definition.ModuleId;

            public IReadOnlyCollection<int> GetAllModuleIds() => new[] { _definition.ModuleId };
        }

        private sealed class AlwaysHasStock : IStockQuery
        {
            public bool HasStock(int moduleId) => true;
        }

        private sealed class NeverOccupied : IOccupancyQuery
        {
            public bool IsOccupied(WorldPos cell) => false;
        }

        /// <summary>无人 / 无病人(端到端用;`IPresenceQuery` 四个成员全实现 —— 漏一个即编译失败)。</summary>
        private sealed class NobodyPresent : IPresenceQuery
        {
            public bool IsPresent(PatientId patientId) => false;
            public int PresentCount => 0;
            public bool IsPresentAt(WorldPos cell) => false;
            public IReadOnlyCollection<int> PresentPatientIds() => Array.Empty<int>();
        }
    }
}
