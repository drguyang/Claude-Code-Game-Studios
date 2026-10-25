// 权威来源:
//   **2026-10-26 用户裁定 ①-C / ②-C / ③-B**(两张显式词表文件;`ROOM_NONE = 0`;真实条目从 1 起)
//   GDD `design/gdd/clinic-machine.md` :340(字段集 = roomName 词表索引 / contexts[] 情境词表索引)
//     + 2026-10-26 补注(词表口径落地:冻结编号 / 覆盖门 / 投影单点)
//   `Sim.Contracts/ClinicEnvDto.cs` 头注「支 4-a」结案段
//   ADR-014 §二/§五(两阶段烘焙)· ADR-024(白名单 / 闭集纪律)
//
// ⚠️ **本件测的是「词表这条链的每一段都真的接上了」**:
//   ① 作者态 JSON → Binder(拒收面)② Binder → cooked 字节(编码面)
//   ③ cooked 字节 → 运行期表(解码面)④ 求值 → DTO(投影面)⑤ Memo 缓存 DTO(交付面)
//   缺任一段 = 「补烘 ≠ 接线」的典型半成品(产物在盘上、无人读它)。
//
// ⚠️ 每条断言都指向一个**具体失效模式**,不是「跑通即绿」。判别力在证伪轮逐条反证
//   (见 `production/qa/evidence/impl-clinic-name-tables-2026-10-26.md` §四)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Clinic;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ClinicMachine
{
    /// <summary>24 两张词表(ROOM_NAMES / CONTEXT_NAMES)的全链测试。</summary>
    [TestFixture]
    public sealed class ClinicNameTablesTest
    {
        private static string RepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        private static string FixtureText(string name)
        {
            string path = Path.Combine(
                RepoRoot(), "tests", "unit", "clinic_machine", "fixtures", name);
            Assert.That(File.Exists(path), Is.True, $"违例夹具缺失:{path}");
            return File.ReadAllText(path);
        }

        private static string RepoDataText(string name)
        {
            string path = Path.Combine(RepoRoot(), "assets", "data", name);
            Assert.That(File.Exists(path), Is.True, $"作者态源表缺失:{path}");
            return File.ReadAllText(path);
        }

        private static void AssertErrorsContain(BakeValidationException ex, params string[] needles)
        {
            string joined = string.Join("; ", ex.Errors);
            Assert.That(ex.Errors.Count, Is.GreaterThan(0), "构建期拒收必须给出至少一条可读原因");
            for (int i = 0; i < needles.Length; i++)
                Assert.That(joined, Does.Contain(needles[i]),
                    $"错误集未包含「{needles[i]}」;全集 = {joined}");
        }

        // ══════════════════ ① Binder 正例:编号来自作者态,不是行序 ══════════════════

        /// <summary>真源 JSON 的编号被逐条读出 —— 且**不是**按行序推算的(行序与编号刻意不同序)。</summary>
        /// <remarks><b>判别力</b>(两种按行序发号的退化形态都须转红):
        /// ① 下标法(`id = i`)—— 真源第 0 行是 `ward` 但编号是 1,下标法会给 0(与 ROOM_NONE 撞);
        /// ② 下标+1 法(`id = i + 1`)—— 真源三行的编号**恰好**等于 `i + 1`,故真源本身判别不出,
        /// 由下方**逆序字面量**那一段钉住(`herb_room` 在第 0 行却是编号 3)。</remarks>
        [Test]
        public void test_binder_roomNames_idsComeFromAuthoringNotRowOrder()
        {
            ClinicMachineRoomNamesBinder.BindResult bound =
                ClinicMachineTablesBake.BindRoomNamesForTest(
                    RepoDataText("clinicmachine_room_names.json"));

            Assert.That(bound.Rows.Count, Is.EqualTo(3), "真源三条(ward / surgery / herb_room)");

            var byName = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < bound.Rows.Count; i++) byName[bound.Rows[i].RoomType] = bound.Rows[i].Id;

            Assert.That(byName["ward"], Is.EqualTo(1));
            Assert.That(byName["surgery"], Is.EqualTo(2));
            Assert.That(byName["herb_room"], Is.EqualTo(3));

            // 行序 ≠ 编号:第 0 行的编号是 1(不是 0)—— 这条钉住「下标法」这一具体退化形态
            Assert.That(bound.Rows[0].Id, Is.EqualTo(1),
                "第 0 行的编号须是 1 —— 若为 0 说明编号被当成数组下标(0 是 ROOM_NONE 哨兵)");
            Assert.That(bound.SchemaVersion, Is.EqualTo(1u));

            // ⚠️ **上面那两条不足以钉住「按行序发号」的第二种退化形态 `id = 下标 + 1`**
            // —— 真源恰好是 ward(第0行)→1 / surgery(第1行)→2 / herb_room(第2行)→3,
            // 与 `i + 1` **同值** ⇒ 变异成 `i + 1` 时上述断言全绿(证伪轮 M1 实测逃逸)。
            // 故补一条**编号与行序刻意逆序**的字面量:按行序发号会让 herb_room 得 0/1 而非 3。
            const string reversedOrder =
                "{ \"schema_version\": 1, \"rooms\": [" +
                " { \"room_type\": \"herb_room\", \"id\": 3 }," +
                " { \"room_type\": \"surgery\", \"id\": 2 }," +
                " { \"room_type\": \"ward\", \"id\": 1 } ] }";

            ClinicMachineRoomNamesBinder.BindResult outOfOrder =
                ClinicMachineTablesBake.BindRoomNamesForTest(reversedOrder);

            var byNameReversed = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < outOfOrder.Rows.Count; i++)
                byNameReversed[outOfOrder.Rows[i].RoomType] = outOfOrder.Rows[i].Id;

            Assert.That(byNameReversed["herb_room"], Is.EqualTo(3),
                "行序第 0 行、作者态编号 3 —— 按行序发号(下标或下标+1)会得 0/1");
            Assert.That(byNameReversed["ward"], Is.EqualTo(1),
                "行序第 2 行、作者态编号 1 —— 按行序发号会得 2/3");
        }

        [Test]
        public void test_binder_contextNames_idsComeFromAuthoring()
        {
            ClinicMachineContextNamesBinder.BindResult bound =
                ClinicMachineTablesBake.BindContextNamesForTest(
                    RepoDataText("clinicmachine_context_names.json"));

            var byName = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < bound.Rows.Count; i++)
                byName[bound.Rows[i].ContextId] = bound.Rows[i].Id;

            Assert.That(byName["ward_plain"], Is.EqualTo(1));
            Assert.That(byName["surgery_sterile"], Is.EqualTo(2));
            Assert.That(byName["herb_heavy"], Is.EqualTo(3));
            Assert.That(bound.Rows[0].Id, Is.EqualTo(1));

            // ⚠️ **真源不足以钉住「按行序发号」** —— 真源三行的编号**恰好**是 1/2/3,
            // 与 `id = i` / `id = i + 1` 两种退化形态**同值** ⇒ 证伪轮 M1b 实测逃逸(全绿)。
            // 补一条编号与行序刻意逆序的字面量:按行序发号会让 herb_heavy 得 0/1 而非 3。
            const string reversedOrder =
                "{ \"schema_version\": 1, \"contexts\": [" +
                " { \"context_id\": \"herb_heavy\", \"id\": 3 }," +
                " { \"context_id\": \"surgery_sterile\", \"id\": 2 }," +
                " { \"context_id\": \"ward_plain\", \"id\": 1 } ] }";

            ClinicMachineContextNamesBinder.BindResult outOfOrder =
                ClinicMachineTablesBake.BindContextNamesForTest(reversedOrder);

            var byNameReversed = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < outOfOrder.Rows.Count; i++)
                byNameReversed[outOfOrder.Rows[i].ContextId] = outOfOrder.Rows[i].Id;

            Assert.That(byNameReversed["herb_heavy"], Is.EqualTo(3),
                "行序第 0 行、作者态编号 3 —— 按行序发号(下标或下标+1)会得 0/1");
            Assert.That(byNameReversed["ward_plain"], Is.EqualTo(1),
                "行序第 2 行、作者态编号 1 —— 按行序发号会得 2/3");
        }

        // ══════════════════ ② Binder 拒收面(裁定 ③-B 的类型机制守)══════════════════

        /// <summary>**id = 0 被拒** —— 「0 保留给 ROOM_NONE」由 `ReadRequiredPositiveInt` 类型机制守,
        /// 不靠另设一条 if(单机制)。</summary>
        /// <remarks><b>判别力</b>:把 `ReadRequiredPositiveInt` 换成 `ReadRequiredInt`,本测红。</remarks>
        [Test]
        public void test_binder_roomNames_zeroId_rejected()
        {
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindRoomNamesForTest(
                    FixtureText("invalid_room_names_zero_id.json")));
            AssertErrorsContain(ex, "id");
        }

        /// <summary>两个 room_type 争同一编号 ⇒ 投影二义(`RoomNameId` 反查会命中哪个?)。</summary>
        [Test]
        public void test_binder_roomNames_duplicateId_rejected()
        {
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindRoomNamesForTest(
                    FixtureText("invalid_room_names_duplicate_id.json")));
            AssertErrorsContain(ex, "重复");
        }

        [Test]
        public void test_binder_roomNames_duplicateKey_rejected()
        {
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindRoomNamesForTest(
                    FixtureText("invalid_room_names_duplicate_key.json")));
            AssertErrorsContain(ex, "重复");
        }

        /// <summary>空词表 ⇒ 全部房间投成 ROOM_NONE = 整链静默失效 ⇒ 拒。</summary>
        [Test]
        public void test_binder_roomNames_empty_rejected()
        {
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindRoomNamesForTest(
                    FixtureText("invalid_room_names_empty.json")));
            AssertErrorsContain(ex, "空");
        }

        /// <summary>白名单外键 ⇒ 拒(ADR-024 闭集纪律;拼错的键会静默变成「没写」)。</summary>
        [Test]
        public void test_binder_roomNames_unknownKey_rejected()
        {
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindRoomNamesForTest(
                    FixtureText("invalid_room_names_unknown_key.json")));
            AssertErrorsContain(ex, "display_name");
        }

        [Test]
        public void test_binder_contextNames_duplicateId_rejected()
        {
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindContextNamesForTest(
                    FixtureText("invalid_context_names_duplicate_id.json")));
            AssertErrorsContain(ex, "重复");
        }

        /// <summary>**运行期表核的四条守卫**(最后一道防线:产物损坏 / 外来产物)。
        /// <para>⚠️ 为什么单独测:烘焙期的 Binder 已挡过一遍,但 `ReadRoomNames` 解码的是
        /// **盘上的字节** —— 手工篡改过的 cooked 文件绕开了 Binder。表核若不守,
        /// 一个二义词表会被静默装上,直到某次投影给出错误编号才暴露。</para></summary>
        /// <remarks><b>判别力</b>:逐条停用 `ClinicNameTableCore` 的四条 if ⇒ 对应断言转红。</remarks>
        [Test]
        public void test_runtimeTableCore_rejectsCorruptVocabularies()
        {
            // ① 空表 —— 全部投影会落到 None(静默失效)
            Assert.Throws<ArgumentException>(() =>
                new ClinicRoomNameTable(Array.Empty<ClinicNameEntry>()), "空表须拒");

            // ② id = 0 —— 与 ROOM_NONE 哨兵撞
            Assert.Throws<ArgumentException>(() =>
                new ClinicRoomNameTable(new[] { new ClinicNameEntry("ward", 0) }), "id=0 须拒");

            // ③ 键重复 —— 一词一编号
            Assert.Throws<ArgumentException>(() =>
                new ClinicRoomNameTable(new[]
                {
                    new ClinicNameEntry("ward", 1),
                    new ClinicNameEntry("ward", 2),
                }), "同键两编号须拒");

            // ④ 编号重复 —— 投影二义(反查会命中哪个?)
            Assert.Throws<ArgumentException>(() =>
                new ClinicContextNameTable(new[]
                {
                    new ClinicNameEntry("ward_plain", 1),
                    new ClinicNameEntry("surgery_sterile", 1),
                }), "两词争同一编号须拒");

            // 对照:合法表装得上(证明上面四条不是「构造器恒抛」的空转断言)
            var ok = new ClinicRoomNameTable(new[] { new ClinicNameEntry("ward", 1) });
            Assert.That(ok.Count, Is.EqualTo(1));
            Assert.That(ok.TryGetId("ward", out int id), Is.True);
            Assert.That(id, Is.EqualTo(1));
        }

        // ══════════════════ ③ 跨表覆盖门(词表 ⊇ 表内出现的键)══════════════════

        /// <summary>ROOM_NAMES 缺 `ROOM_TABLE` 里的某个 room_type ⇒ 烘焙期硬失败。
        /// <para>⚠️ 这是词表存在的**理由**之一:缺项时运行期投影只能落 `ROOM_NONE`,
        /// 而那是「没有房间」的语义 —— 静默错读。</para></summary>
        /// <remarks><b>判别力</b>:停用 `AssertRoomNameCoverage` ⇒ 本测红(烘焙不再拒)。</remarks>
        [Test]
        public void test_coverage_roomNamesMissingTableRoomType_rejected()
        {
            // 词表只留 ward;ROOM_TABLE 里有 surgery / herb_room ⇒ 两条缺项
            const string partialNames =
                "{ \"schema_version\": 1, \"rooms\": [ { \"room_type\": \"ward\", \"id\": 1 } ] }";

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BakeFromJson(
                    RepoDataText("clinicmachine_context.json"),
                    RepoDataText("clinicmachine_adj.json"),
                    RepoDataText("clinicmachine_room.json"),
                    partialNames,
                    RepoDataText("clinicmachine_context_names.json"),
                    RepoDataText("item_database_constants.json")));

            AssertErrorsContain(ex, "ROOM_TABLE", "surgery");
        }

        /// <summary>CONTEXT_NAMES 缺 `CONTEXT_TABLE` 里的某个 context_id ⇒ 烘焙期硬失败。</summary>
        /// <remarks><b>判别力</b>:停用 `AssertContextNameCoverage` ⇒ 本测红。</remarks>
        [Test]
        public void test_coverage_contextNamesMissingTableContextId_rejected()
        {
            const string partialNames =
                "{ \"schema_version\": 1, \"contexts\": [ { \"context_id\": \"ward_plain\", \"id\": 1 } ] }";

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BakeFromJson(
                    RepoDataText("clinicmachine_context.json"),
                    RepoDataText("clinicmachine_adj.json"),
                    RepoDataText("clinicmachine_room.json"),
                    RepoDataText("clinicmachine_room_names.json"),
                    partialNames,
                    RepoDataText("item_database_constants.json")));

            AssertErrorsContain(ex, "CONTEXT_TABLE", "surgery_sterile");
        }

        /// <summary>**覆盖门是单向的**:词表有表内暂未用到的条目 ⇒ **不拒**(P1a 预留编号)。</summary>
        /// <remarks><b>判别力</b>:把覆盖门改成双向差集归零 ⇒ 本测红(多余的 `icu` 会被拒)。
        /// 这一条防的是「过度收紧」——它会让「为 P1a 先占编号」变得不可能。</remarks>
        [Test]
        public void test_coverage_extraNameEntry_accepted_oneWayOnly()
        {
            const string namesWithSpare =
                "{ \"schema_version\": 1, \"rooms\": [" +
                " { \"room_type\": \"ward\", \"id\": 1 }," +
                " { \"room_type\": \"surgery\", \"id\": 2 }," +
                " { \"room_type\": \"herb_room\", \"id\": 3 }," +
                " { \"room_type\": \"icu\", \"id\": 4 } ] }";   // 表里没有 icu ⇒ 允许(P1a 预留)

            Assert.DoesNotThrow(() => ClinicMachineTablesBake.BakeFromJson(
                RepoDataText("clinicmachine_context.json"),
                RepoDataText("clinicmachine_adj.json"),
                RepoDataText("clinicmachine_room.json"),
                namesWithSpare,
                RepoDataText("clinicmachine_context_names.json"),
                RepoDataText("item_database_constants.json")));
        }

        // ══════════════════ ④ 编码 ↔ 解码(写方镜像)══════════════════

        /// <summary>现烘字节 → 解码 → 双向查找,与作者态编号逐条相等。
        /// <para>⚠️ 这一条抓的是**字段序错位**:写方写 `(str, i32)`,读方若读成 `(i32, str)`
        /// 会先在字符串长度校验炸;若两处同错(如都写 `(i32, str)`)则本测的**值**断言仍能抓到
        /// (id 会被读成字符串长度)。</para></summary>
        /// <remarks><b>判别力</b>:把 `WriteRoomNames` 的 `w.Write(rows[i].Id)` 改成写 0 ⇒ 本测红。</remarks>
        [Test]
        public void test_codec_roomNames_roundTrip_preservesIdsBothWays()
        {
            ClinicMachineTablesBake.BakeOutput baked = ClinicMachineTablesBake.BakeFromRepo(RepoRoot());
            ClinicRoomNameDataSet decoded = ClinicMachineCookedCodec.ReadRoomNames(baked.RoomNamesCooked);

            Assert.That(decoded.Table.Count, Is.EqualTo(3));

            Assert.That(decoded.Table.TryGetId("ward", out int wardId), Is.True, "ward 须在词表里");
            Assert.That(wardId, Is.EqualTo(1));

            // 反向:编号 → 词(反查是 42 显示房间名的路径)
            Assert.That(decoded.Table.TryGetRoomType(2, out string surgery), Is.True);
            Assert.That(surgery, Is.EqualTo("surgery"));

            // 0 不是词表条目 —— 反查必须失败,而不是返回某个房间
            Assert.That(decoded.Table.TryGetRoomType(ClinicRoomNameTable.NoneId, out _), Is.False,
                "id=0 是 ROOM_NONE 哨兵,不得反查到任何房间");
        }

        [Test]
        public void test_codec_contextNames_roundTrip_preservesIdsBothWays()
        {
            ClinicMachineTablesBake.BakeOutput baked = ClinicMachineTablesBake.BakeFromRepo(RepoRoot());
            ClinicContextNameDataSet decoded =
                ClinicMachineCookedCodec.ReadContextNames(baked.ContextNamesCooked);

            Assert.That(decoded.Table.Count, Is.EqualTo(3));
            Assert.That(decoded.Table.TryGetId("surgery_sterile", out int id), Is.True);
            Assert.That(id, Is.EqualTo(2));
            Assert.That(decoded.Table.TryGetContextId(3, out string herb), Is.True);
            Assert.That(herb, Is.EqualTo("herb_heavy"));
        }

        /// <summary>盘上产物 == 现烘产物(两处字节同源;漂移 = 忘了重烘)。</summary>
        /// <remarks><b>判别力</b>:改一行作者态 JSON 而不重烘 ⇒ 本测红。</remarks>
        [Test]
        public void test_codec_diskArtifacts_matchFreshBake()
        {
            ClinicMachineTablesBake.BakeOutput fresh = ClinicMachineTablesBake.BakeFromRepo(RepoRoot());

            Assert.That(
                DataCookedDisk.ReadAll(AddressablesDataProvider.ClinicRoomNamesAddress),
                Is.EqualTo(fresh.RoomNamesCooked),
                "盘上 ROOM_NAMES 与现烘字节不一致 —— 改了作者态却没重烘");
            Assert.That(
                DataCookedDisk.ReadAll(AddressablesDataProvider.ClinicContextNamesAddress),
                Is.EqualTo(fresh.ContextNamesCooked),
                "盘上 CONTEXT_NAMES 与现烘字节不一致 —— 同上");
        }

        // ══════════════════ ⑤ DTO 投影(交付面)══════════════════

        /// <summary>未匹配 ⇒ `RoomNameId = 0`(ROOM_NONE 哨兵)、`ContextIds` 空数组(非 null)。
        /// <para>⚠️ 空集语义 = 「印卸不成立」,**不是**词表里的某个 id(tutorial-48 面板)。</para></summary>
        [Test]
        public void test_projection_unmatched_roomNameIdIsNoneSentinel()
        {
            ClinicRoomNameTable rooms = DataCookedDisk.LoadClinicRoomNames().Table;
            ClinicContextNameTable contexts = DataCookedDisk.LoadClinicContextNames().Table;

            ClinicEnvDto dto = ClinicEnvProjection.ToDto(
                UnmatchedEvaluation(), rooms, contexts);

            Assert.That(dto.RoomNameId, Is.EqualTo(0), "ROOM_NONE ⇒ 哨兵 0");
            Assert.That(dto.ContextIds, Is.Not.Null, "空集须是空数组而非 null");
            Assert.That(dto.ContextIds.Length, Is.EqualTo(0));
            Assert.That(dto.EnvMod.Raw, Is.EqualTo(0L), "中性出口(EC-24-01/02/07)");
            Assert.That(dto.EquipMod.Raw, Is.EqualTo(0L));
        }

        /// <summary>匹配 ⇒ 编号来自词表(不是 `room_type` 的哈希、不是行序)。</summary>
        /// <remarks><b>判别力</b>:把 `ResolveRoomNameId` 改成返回 `evaluation.RoomType.GetHashCode()`
        /// ⇒ 本测红(值不等于 2)。</remarks>
        [Test]
        public void test_projection_matched_usesVocabularyIds()
        {
            ClinicRoomNameTable rooms = DataCookedDisk.LoadClinicRoomNames().Table;
            ClinicContextNameTable contexts = DataCookedDisk.LoadClinicContextNames().Table;

            ClinicEnvDto dto = ClinicEnvProjection.ToDto(
                MatchedEvaluation("surgery", new[] { "surgery_sterile" },
                    envModRaw: 12345, equipModRaw: 678),
                rooms, contexts);

            Assert.That(dto.RoomNameId, Is.EqualTo(2), "surgery 的词表编号 = 2");
            Assert.That(dto.ContextIds, Is.EqualTo(new[] { 2 }), "surgery_sterile 的词表编号 = 2");
            Assert.That(dto.EnvMod.Raw, Is.EqualTo(12345L), "EnvMod 原样透传(不二次加工)");
            Assert.That(dto.EquipMod.Raw, Is.EqualTo(678L));
        }

        /// <summary>求值里有词表没有的 room_type ⇒ **throw**,不降级为 None。
        /// <para>⚠️ 降级 = 把「两份产物不同批」显示成「没有房间」—— 本项目反复抓过的静默失效形态。</para></summary>
        /// <remarks><b>判别力</b>:把 throw 改成 `return NoneId` ⇒ 本测红(不再抛)。</remarks>
        [Test]
        public void test_projection_unknownRoomType_throwsInsteadOfDegradingToNone()
        {
            ClinicRoomNameTable rooms = DataCookedDisk.LoadClinicRoomNames().Table;
            ClinicContextNameTable contexts = DataCookedDisk.LoadClinicContextNames().Table;

            var ex = Assert.Throws<InvalidOperationException>(() =>
                ClinicEnvProjection.ToDto(
                    MatchedEvaluation("icu_not_in_vocabulary", Array.Empty<string>(), 0, 0),
                    rooms, contexts));
            Assert.That(ex.Message, Does.Contain("ROOM_NAMES"));
        }

        /// <summary>情境 id 缺编号 ⇒ **throw**(静默丢项会让「印卸」误判为「不成立」)。</summary>
        [Test]
        public void test_projection_unknownContextId_throws()
        {
            ClinicRoomNameTable rooms = DataCookedDisk.LoadClinicRoomNames().Table;
            ClinicContextNameTable contexts = DataCookedDisk.LoadClinicContextNames().Table;

            var ex = Assert.Throws<InvalidOperationException>(() =>
                ClinicEnvProjection.ToDto(
                    MatchedEvaluation("ward", new[] { "context_not_in_vocabulary" }, 0, 0),
                    rooms, contexts));
            Assert.That(ex.Message, Does.Contain("CONTEXT_NAMES"));
        }

        // ══════════════════ 辅助 ══════════════════

        /// <summary>造一个「未匹配」的求值快照(中性出口;不经真求值器 —— 本件只测投影面)。</summary>
        private static ClinicEnvEvaluation UnmatchedEvaluation()
            => new ClinicEnvEvaluation(
                default, default, matched: false, roomType: null,
                Fix.Zero, Fix.Zero, Fix.One, Array.Empty<string>());

        /// <summary>造一个「已匹配」的求值快照(给定 room_type / contextIds / 两乘子)。</summary>
        private static ClinicEnvEvaluation MatchedEvaluation(
            string roomType, string[] contextIds, long envModRaw, long equipModRaw)
            => new ClinicEnvEvaluation(
                default, default, matched: true, roomType,
                new Fix(envModRaw), new Fix(equipModRaw), Fix.One, contextIds);
    }
}
