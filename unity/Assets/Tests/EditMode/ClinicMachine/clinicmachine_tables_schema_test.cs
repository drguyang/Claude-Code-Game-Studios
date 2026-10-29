// Story 001 · clinic-machine —— 烘焙表与 schema 构建门(AC-24-05 / 06 / 08 半边 / 12 半边 · O-24-4 / O-24-5 · K_CONTEXT_MAX)。
//
// 卡面:production/epics/clinic-machine/story-001-baked-tables-schema-gates.md
//   (§Test Evidence 逐字点名本路径 —— 真身 = 本文件)
// 权威来源:
//   ADR-014 §三(阶段 2:白名单 / FixParse / schema_version / 聚合 throw)· §四(Fix 字段必须字符串)
//   GDD design/gdd/clinic-machine.md:
//     规则二(ROOM_TABLE 谓词 + 邻接子句 + 方向确定性)· 规则三 / F-24-3(K_speed 档集 / K_CONTEXT_MAX 派生)
//     · F-24-1(W 域 / 对称硬门)· F-24-2(n_max × TIER_MAX < 2^31)· O-24-4(ENV_MOD_MIN < 0)· O-24-5(四项断言)
//   OQ-24-1(2026-09-25)K_speed = {1, 7/8, 3/4} · OQ-24-2 TIER_MAX = 3 · OQ-24-5 n_max = 64 · OQ-24-6 W 域 [-1/4, +1/4]
//
// ⚠️ 被测对象 = **生产代码**(ClinicMachineSchema / 三个 Binder / ClinicMachineTablesBake / CookedWriter)
//    —— 不是本文件的副本。本文件只含夹具读取与断言。
//
// ⚠️ **AC-24-08 / AC-24-12 维持 BLOCKED-BY-O-24-1**:本文件的对应测只证**已实现半边**
//    (FURN_TABLE 的 schema **形状** / 子句类型闭集 + 方向确定性),**不构成**那两条 AC 通过。
//    闭集来源(`FURN_TYPES`)= 21a 家具目录扩展(O-24-1),本件**不伪造闭集**:接缝经注入参数,
//    测试替身是**协作对象**(命名 StubFurnSource,注释标明非真闭集),生产缺席由缺口哨兵自陈。
//
// ⚠️ 测试纪律:零写盘(夹具只读;违例走字符串入口 BakeFromJson / 单表 BindForTest)·
//    无随机 · 无时间依赖 · 夹具缺失即红(不静默跳过)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ClinicMachine
{
    /// <summary>24 三表烘焙 schema 门的 EditMode 测试。</summary>
    [TestFixture]
    public sealed class ClinicMachineTablesSchemaTest
    {
        // ════════════════════════════════════════════════════════════════════
        // 夹具基建(只读)
        // ════════════════════════════════════════════════════════════════════

        // 本文件在 unity/Assets/Tests/EditMode/ClinicMachine/ ⇒ 仓库根 = 上 5 层
        // (ClinicMachine→EditMode→Tests→Assets→unity→仓根)。承 Audio/settings_exposure_test.cs:47-49。
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

        /// <summary>断言异常的错误集**至少一条**包含给定子串(单一出口,失败信息给出全集)。</summary>
        private static void AssertErrorsContain(BakeValidationException ex, params string[] needles)
        {
            string joined = string.Join("; ", ex.Errors);
            Assert.That(ex.Errors.Count, Is.GreaterThan(0), "构建期拒收必须给出至少一条可读原因");
            for (int i = 0; i < needles.Length; i++)
                Assert.That(joined, Does.Contain(needles[i]),
                    $"错误集未包含「{needles[i]}」;全集 = {joined}");
        }

        /// <summary>按 room_type 取上下文行(语义值断言用;缺失即 Fail,不静默)。</summary>
        private static ClinicMachineContextBinder.ContextRow RowOf(
            ClinicMachineContextBinder.BindResult bound, string roomType)
        {
            for (int i = 0; i < bound.Rows.Count; i++)
                if (bound.Rows[i].RoomType == roomType)
                    return bound.Rows[i];
            Assert.Fail($"表缺 {roomType} 行(语义值断言的前提)");
            return default; // 不可达
        }

        /// <summary>取有向对 (a,b) 的权重 raw(语义值断言用;缺失即 Fail)。</summary>
        private static long PairW(ClinicMachineAdjBinder.BindResult bound, string a, string b)
        {
            for (int i = 0; i < bound.Pairs.Count; i++)
                if (bound.Pairs[i].A == a && bound.Pairs[i].B == b)
                    return bound.Pairs[i].W.Raw;
            Assert.Fail($"表缺有向对 ({a},{b})(语义值断言的前提)");
            return 0; // 不可达
        }

        /// <summary>注入用的**测试内局部**家具类型替身。
        /// <para>⚠️ **非真闭集**(`FURN_TYPES` 的真源 = 21a 家具目录扩展,O-24-1,实测零落点)。
        /// 它是**传给注入参数的协作对象**,不是声明的闭集 —— 用于证明接缝通电(裁决 2(a))。</para></summary>
        private sealed class StubFurnSource : IFurnTypeSource
        {
            private readonly IReadOnlyCollection<string> _types;

            public StubFurnSource(params string[] types) { _types = types; }

            public bool TryGetTypes(out IReadOnlyCollection<string> types)
            {
                types = _types;
                return true;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-24-05 · CONTEXT_TABLE 的 K_speed ∈ (0,1](档集单机制)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_context_k_speed_above_one_rejected()
        {
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindContextForTest(FixtureText("invalid_context_table.json")));

            AssertErrorsContain(ex, "k_speed", "5/4");
        }

        [Test]
        public void test_context_k_speed_zero_rejected()
        {
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindContextForTest(FixtureText("invalid_context_zero.json")));

            AssertErrorsContain(ex, "k_speed", "档集");
        }

        [Test]
        public void test_context_k_speed_boundary_one_accepted()
        {
            // 边界双向的另一侧:"1/1" 解析后 = 档集成员 1(raw 65536)⇒ 必须通过。
            ClinicMachineContextBinder.BindResult bound = ClinicMachineTablesBake.BindContextForTest(
                FixtureText("valid_context_boundary.json"));

            // 语义值断言(清债轮 · TD-clinic-09):只断 Count>0 时行内容全错仍绿。
            // 钉到夹具逐行值(Q16.16 raw:1/1=65536 · 7/8=57344 · 3/4=49152 · 1/4=16384 · 1/8=8192)。
            Assert.That(bound.Rows.Count, Is.EqualTo(3));
            Assert.That(RowOf(bound, "ward").KSpeed.Raw, Is.EqualTo(65536L));
            Assert.That(RowOf(bound, "ward").BaseEnv.Raw, Is.EqualTo(0L));
            Assert.That(RowOf(bound, "surgery").KSpeed.Raw, Is.EqualTo(57344L));
            Assert.That(RowOf(bound, "surgery").BaseEnv.Raw, Is.EqualTo(16384L));
            Assert.That(RowOf(bound, "herb_room").KSpeed.Raw, Is.EqualTo(49152L));
            Assert.That(RowOf(bound, "herb_room").BaseEnv.Raw, Is.EqualTo(8192L));
            Assert.That(bound.KContextMax.Raw, Is.EqualTo(65536L));
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-24-06 · ADJ_TABLE 对称性 = 烘焙硬门
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_adj_asymmetric_value_rejected()
        {
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindAdjForTest(FixtureText("invalid_adj_table.json")));

            AssertErrorsContain(ex, "不对称", "surgery_table");
        }

        [Test]
        public void test_adj_single_sided_pair_rejected()
        {
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindAdjForTest(FixtureText("invalid_adj_single_sided.json")));

            AssertErrorsContain(ex, "单向缺项", "chamber_pot");
        }

        [Test]
        public void test_adj_symmetric_pairs_accepted()
        {
            ClinicMachineAdjBinder.BindResult bound = ClinicMachineTablesBake.BindAdjForTest(
                RepoDataText("clinicmachine_adj.json"));

            Assert.That(bound.Pairs.Count, Is.EqualTo(6));
            Assert.That(bound.NMax, Is.EqualTo(64L));
            Assert.That(bound.WMin.Raw, Is.EqualTo(-16384L));
            Assert.That(bound.WMax.Raw, Is.EqualTo(16384L));

            // 值级对称(AC-24-06 的语义半边):每个有向行存在等权反向行 —— 只数行数时
            // 「权重全错但仍成对」的表照样绿(清债轮 · TD-clinic-09)。
            for (int i = 0; i < bound.Pairs.Count; i++)
            {
                string fwdA = bound.Pairs[i].A;
                string fwdB = bound.Pairs[i].B;
                long fwdW = bound.Pairs[i].W.Raw;
                bool mirrored = false;
                for (int j = 0; j < bound.Pairs.Count; j++)
                    if (bound.Pairs[j].A == fwdB && bound.Pairs[j].B == fwdA
                        && bound.Pairs[j].W.Raw == fwdW)
                    {
                        mirrored = true;
                        break;
                    }
                Assert.That(mirrored, Is.True, $"({fwdA},{fwdB}) 缺等权反向行");
            }

            // 代表行语义值(Q16.16 raw:1/8=8192 · -1/8=-8192 · -1/4=-16384)。
            Assert.That(PairW(bound, "surgery_table", "sterile_cabinet"), Is.EqualTo(8192L));
            Assert.That(PairW(bound, "herb_cabinet", "chamber_pot"), Is.EqualTo(-8192L));
            Assert.That(PairW(bound, "bed", "chamber_pot"), Is.EqualTo(-16384L));
        }

        // ════════════════════════════════════════════════════════════════════
        // O-24-5 · 溢出界(n_max × TIER_MAX < 2^31 且 |W_MIN| <= W_MAX)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_overflow_bounds_exact_2pow31_minus_one_accepted()
        {
            // 恰 2^31 − 1(= Int32Max)通过:乘积的允许上确界。
            var errors = new List<string>();
            ClinicMachineSchema.AssertOverflowBounds(
                ClinicMachineSchema.Int32Max, 1L, -16384L, 16384L, errors);

            Assert.That(errors, Is.Empty);

            // 判别力配对(清债轮 · TD-clinic-02 · M0b 实测:守卫停用时本测原样绿)。
            // 同测内驱动一次越界(Int32Max × 2 ≥ 2^31),钉守卫自己的报文 token ——
            // 守卫停用 ⇒ 本段红。接受侧自此不再「只断没报错」。
            var overflow = new List<string>();
            ClinicMachineSchema.AssertOverflowBounds(
                ClinicMachineSchema.Int32Max, 2L, -16384L, 16384L, overflow);

            Assert.That(string.Join("; ", overflow), Does.Contain("溢出界失败"));
        }

        [Test]
        public void test_overflow_bounds_at_2pow31_rejected()
        {
            // 恰 2^31 拒:边界双向的另一侧(不用夹具体 —— 该边界须可直驱)。
            var errors = new List<string>();
            ClinicMachineSchema.AssertOverflowBounds(
                ClinicMachineSchema.Int32Limit, 1L, -16384L, 16384L, errors);

            Assert.That(errors, Is.Not.Empty);
            Assert.That(string.Join("; ", errors), Does.Contain("溢出界"));
        }

        [Test]
        public void test_adj_n_max_boundary_floor_accepted()
        {
            // n_max = floor((2^31−1)/3) ⇒ ×3 = 2147483646 < 2^31。
            ClinicMachineAdjBinder.BindResult bound = ClinicMachineTablesBake.BindAdjForTest(
                FixtureText("valid_adj_boundary.json"));

            Assert.That(bound.NMax, Is.EqualTo(715827882L));

            // 判别力配对(清债轮 · TD-clinic-02 · M0b 实测:NMax 相等只证解析回读)。
            // 把解析出的边界值接回守卫本体:恰界须过、+1 须拒 —— 守卫停用 ⇒ 本段红。
            var atFloor = new List<string>();
            ClinicMachineSchema.AssertOverflowBounds(bound.NMax, 3L, -16384L, 16384L, atFloor);
            Assert.That(atFloor, Is.Empty);

            var overFloor = new List<string>();
            ClinicMachineSchema.AssertOverflowBounds(bound.NMax + 1L, 3L, -16384L, 16384L, overFloor);
            Assert.That(string.Join("; ", overFloor), Does.Contain("溢出界失败"));
        }

        [Test]
        public void test_adj_n_max_over_floor_rejected()
        {
            // n_max = floor + 1 ⇒ ×3 = 2147483649 >= 2^31。
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindAdjForTest(FixtureText("invalid_adj_overflow.json")));

            AssertErrorsContain(ex, "溢出界");
        }

        [Test]
        public void test_adj_wmin_abs_gt_wmax_rejected()
        {
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindAdjForTest(FixtureText("invalid_adj_wdomain.json")));

            // 断言钉到「界健全性」子分支 —— 该 token 被 AssertOverflowBounds ②段三条报文共用
            // (「W_MIN…须 < 0」/「W_MAX…须 > 0」/「界健全性失败:|W_MIN| > W_MAX」),
            // 只写 "W_MIN" 无法判别命中哪条(评审 R-10a / S27,F4 同族)。
            AssertErrorsContain(ex, "界健全性失败");
        }

        // ════════════════════════════════════════════════════════════════════
        // O-24-4 · ENV_MOD_MIN < 0(21a 常量,读表不硬编码)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_constants_env_mod_min_negative_accepted()
        {
            // 生产路径 = 读 assets/data/item_database_constants.json(env_mod_min = "-1/2")。
            ClinicMachineTablesBake.BakeOutput output = ClinicMachineTablesBake.BakeFromRepo(RepoRoot());

            // 语义值断言(清债轮 · TD-clinic-08):DoesNotThrow 只证「没炸」;钉到源表
            // 逐行值(Q16.16 raw:0 · 1/4=16384 · 1/8=8192)—— 哪些行被接受,逐值可判。
            Assert.That(output.Context.Rows.Count, Is.EqualTo(3));
            Assert.That(RowOf(output.Context, "ward").BaseEnv.Raw, Is.EqualTo(0L));
            Assert.That(RowOf(output.Context, "surgery").BaseEnv.Raw, Is.EqualTo(16384L));
            Assert.That(RowOf(output.Context, "herb_room").BaseEnv.Raw, Is.EqualTo(8192L));

            // 域边界接受侧(与下方非负拒收侧配对):base_env = ±ENV_MOD(= ±1/2,
            // 读真实常量文件)必须过 —— O-24-4 门在边界不误伤;钉解析值 ±32768。
            // ⚠️ context_id 必须取自 clinicmachine_context_names.json 的真实词表 —— 覆盖门
            // (AssertContextNameCoverage)拒收词表外的情境(否则该情境投影不出来 = 静默错读)。
            // 本测只验 base_env 域边界,context_id 取哪个词表项与断言无关,取真实项即可。
            const string contextAtDomainBounds =
                "{ \"schema_version\": 1, \"contexts\": [" +
                " { \"room_type\": \"ward\", \"context_id\": \"ward_plain\", \"k_speed\": \"1\", \"base_env\": \"-1/2\" }," +
                " { \"room_type\": \"surgery\", \"context_id\": \"surgery_sterile\", \"k_speed\": \"1\", \"base_env\": \"1/2\" } ] }";
            ClinicMachineTablesBake.BakeOutput bounds = ClinicMachineTablesBake.BakeFromJson(
                contextAtDomainBounds,
                RepoDataText("clinicmachine_adj.json"),
                RepoDataText("clinicmachine_room.json"),
                RepoDataText("clinicmachine_room_names.json"),
                RepoDataText("clinicmachine_context_names.json"),
                RepoDataText("item_database_constants.json"));

            Assert.That(RowOf(bounds.Context, "ward").BaseEnv.Raw, Is.EqualTo(-32768L));
            Assert.That(RowOf(bounds.Context, "surgery").BaseEnv.Raw, Is.EqualTo(32768L));
        }

        [Test]
        public void test_constants_env_mod_min_nonnegative_rejected()
        {
            // 负例证明 O-24-4 门不是空转:把 env_mod_min 改成 1/4 ⇒ 必拒。
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BakeFromJson(
                    RepoDataText("clinicmachine_context.json"),
                    RepoDataText("clinicmachine_adj.json"),
                    RepoDataText("clinicmachine_room.json"),
                    RepoDataText("clinicmachine_room_names.json"),
                    RepoDataText("clinicmachine_context_names.json"),
                    FixtureText("invalid_constants_env_mod_positive.json")));

            // ⚠️ 断言钉到 O-24-4 守卫**自己的**报文前缀,不用共用 token「ENV_MOD_MIN」——
            //    后者同时出现在 base_env 域校验的报文里,会把本测借绿(证伪轮 F4 实测存活,
            //    停用 AssertEnvModMinNegative 后仍是绿)。2026-10-20 证伪轮修正。
            AssertErrorsContain(ex, "O-24-4 失败");
        }

        // ════════════════════════════════════════════════════════════════════
        // 规则三 / F-24-1 · base_env ∈ [ENV_MOD_MIN, ENV_MOD_MAX](ENV 域 = 21a,非 24 的 W 域)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_context_base_env_outside_env_domain_rejected()
        {
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BakeFromJson(
                    FixtureText("invalid_context_base_env.json"),
                    RepoDataText("clinicmachine_adj.json"),
                    RepoDataText("clinicmachine_room.json"),
                    RepoDataText("clinicmachine_room_names.json"),
                    RepoDataText("clinicmachine_context_names.json"),
                    RepoDataText("item_database_constants.json")));

            AssertErrorsContain(ex, "base_env", "ENV_MOD_MIN");
        }

        // ════════════════════════════════════════════════════════════════════
        // K_CONTEXT_MAX = max(K_speed) —— 表派生,禁裸常数(F-24-3)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_k_context_max_derived_from_table()
        {
            ClinicMachineContextBinder.BindResult bound = ClinicMachineTablesBake.BindContextForTest(
                RepoDataText("clinicmachine_context.json"));

            long maxRaw = long.MinValue;
            for (int i = 0; i < bound.Rows.Count; i++)
                if (bound.Rows[i].KSpeed.Raw > maxRaw) maxRaw = bound.Rows[i].KSpeed.Raw;

            // ① 导出值 = 表行最大值(不是手填)
            Assert.That(bound.KContextMax.Raw, Is.EqualTo(maxRaw));

            // ② 且 = 档集里的最大档(档集本身是唯一出处,仍不抄字面量)
            long whitelistMax = long.MinValue;
            for (int i = 0; i < ClinicMachineSchema.KSpeedLiterals.Count; i++)
            {
                long raw = FixParse.Parse(ClinicMachineSchema.KSpeedLiterals[i]).Raw;
                if (raw > whitelistMax) whitelistMax = raw;
            }
            Assert.That(bound.KContextMax.Raw, Is.EqualTo(whitelistMax));
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-24-08 **已实现半边** · FURN_TABLE 字段形状(tier ∈ 整数域 / e_env Fix 字符串)
        //   ⚠️ 整条 AC 挂 BLOCKED-BY-O-24-1 —— 下列测只证 schema 形状门。
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_furn_table_field_spec_accepted()
        {
            Assert.DoesNotThrow(() => ClinicMachineSchema.AssertFurnTableShape(
                FixtureText("valid_furn_shape.json")));
        }

        [Test]
        public void test_furn_table_missing_field_rejected()
        {
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineSchema.AssertFurnTableShape(FixtureText("invalid_furn_missing_field.json")));

            AssertErrorsContain(ex, "e_env");
        }

        [Test]
        public void test_furn_table_wrong_token_kind_rejected()
        {
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineSchema.AssertFurnTableShape(FixtureText("invalid_furn_token_kinds.json")));

            // tier 写成字符串 / e_env 写成 JSON 数字 —— 两种 token 种类错都要报。
            AssertErrorsContain(ex, "tier", "e_env");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-24-12 **已实现半边** · 子句类型闭集 + 方向确定性;成员资格 = 注入接缝 + 缺口哨兵
        //   ⚠️ 整条 AC 挂 BLOCKED-BY-O-24-1 —— 成员资格未在生产路径强制。
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_room_unknown_clause_kind_rejected()
        {
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindRoomForTest(
                    FixtureText("invalid_room_clause_kind.json"), null));

            AssertErrorsContain(ex, "未知子句类型", "proximity");
        }

        [Test]
        public void test_room_clause_direction_invalid_rejected()
        {
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindRoomForTest(
                    FixtureText("invalid_room_direction.json"), null));

            // 🔁 2026-10-29(债册 TD-clinic-03 / 评审 R-10b):原断言「direction」是**共用 token** ——
            //    `RoomBinder` 的两条报文(`邻接子句缺字符串 direction` :250 / `未知方向 direction=` :257)
            //    都含它 ⇒ 走错分支也满足。改钉**唯一分支前缀**「未知方向」。
            AssertErrorsContain(ex, "未知方向");
        }

        [Test]
        public void test_room_furn_membership_enforced_with_injected_source()
        {
            // 注入替身覆盖全部引用类型 ⇒ 通过,且哨兵 = true(接缝通电,不是死码)。
            const string roomJson =
                @"{ ""schema_version"": 1, ""rooms"": [ { ""room_type"": ""ward"", ""clauses"": " +
                @"[ { ""kind"": ""has_type"", ""type"": ""bed"" }, " +
                @"{ ""kind"": ""has_type"", ""type"": ""cot"" } ] } ] }";

            ClinicMachineRoomBinder.BindResult bound = ClinicMachineTablesBake.BindRoomForTest(
                roomJson, new StubFurnSource("bed", "cot"));

            Assert.That(bound.FurnMembershipEnforced, Is.True);
        }

        [Test]
        public void test_room_furn_membership_rejects_type_outside_injected_source()
        {
            // 替身缺 cot ⇒ 成员资格门必须拒(证明不是只置标志)。
            const string roomJson =
                @"{ ""schema_version"": 1, ""rooms"": [ { ""room_type"": ""ward"", ""clauses"": " +
                @"[ { ""kind"": ""has_type"", ""type"": ""bed"" }, " +
                @"{ ""kind"": ""has_type"", ""type"": ""cot"" } ] } ] }";

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindRoomForTest(roomJson, new StubFurnSource("bed")));

            AssertErrorsContain(ex, "cot");
        }

        [Test]
        public void test_room_furn_membership_absent_sentinel_false()
        {
            // **缺口哨兵**:生产路径无闭集来源(O-24-1 未兑现)⇒ 必须显式自陈未强制。
            // O-24-1 落地后此测**转红是被要求的行为**,不得当回归修掉(承 S14-6 哨兵口径)。
            ClinicMachineRoomBinder.BindResult bound = ClinicMachineTablesBake.BindRoomForTest(
                RepoDataText("clinicmachine_room.json"), null);

            Assert.That(bound.FurnMembershipEnforced, Is.False);
        }

        // ════════════════════════════════════════════════════════════════════
        // 通用构建期门(ADR-014 §三 / §四)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_unknown_key_rejected()
        {
            const string json =
                @"{ ""schema_version"": 1, ""context"": [], ""contexts"": " +
                @"[ { ""room_type"": ""ward"", ""context_id"": ""c"", ""k_speed"": ""1"", ""base_env"": ""0"" } ] }";

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindContextForTest(json));

            AssertErrorsContain(ex, "未知键", "context");
        }

        [Test]
        public void test_schema_version_mismatch_rejected()
        {
            const string json =
                @"{ ""schema_version"": 999, ""contexts"": " +
                @"[ { ""room_type"": ""ward"", ""context_id"": ""c"", ""k_speed"": ""1"", ""base_env"": ""0"" } ] }";

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindContextForTest(json));

            AssertErrorsContain(ex, "schema_version");
        }

        [Test]
        public void test_fix_field_json_number_token_rejected()
        {
            // base_env 写成 JSON 数字 ⇒ 结构性拒(ADR-014 §四:Fix 字段必须字符串)。
            const string json =
                @"{ ""schema_version"": 1, ""contexts"": " +
                @"[ { ""room_type"": ""ward"", ""context_id"": ""c"", ""k_speed"": ""1"", ""base_env"": 0 } ] }";

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindContextForTest(json));

            // 🔁 2026-10-29(债册 TD-clinic-05 / 评审 R-10d):原断言「须为 JSON 字符串」被
            //    `ReadRequiredString`(Schema:380)与 `ReadRequiredFix`(Schema:410)**共用** ⇒
            //    走错守卫也满足。改钉 `ReadRequiredFix` **独有**的「Fix 字面量」。
            AssertErrorsContain(ex, "Fix 字面量");
        }

        [Test]
        public void test_multiple_errors_aggregated_in_single_throw()
        {
            // 两处独立违例(未知键 + k_speed 越界)⇒ 一次 throw 报全(不是首错即停)。
            const string json =
                @"{ ""schema_version"": 1, ""surprise"": 1, ""contexts"": " +
                @"[ { ""room_type"": ""ward"", ""context_id"": ""c"", ""k_speed"": ""5/4"", ""base_env"": ""0"" } ] }";

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindContextForTest(json));

            Assert.That(ex.Errors.Count, Is.GreaterThanOrEqualTo(2));
            AssertErrorsContain(ex, "surprise", "5/4");

            // 🔁 2026-10-29(债册 TD-clinic-06 / 评审 R-18):原测只断条数与子串,**不钉顺序** ——
            //    而聚合 throw 的**顺序**是 ADR-014 确定性面的一部分(同输入两次烘焙须逐位一致)。
            //    钉:**未知键(前言段)先于** 采样行段的 `k_speed` 越界 —— 即
            //    `RejectUnknownKeys` 必须先于逐行解析执行。调换两段 ⇒ 本断言红。
            string joinedOrder = string.Join("; ", ex.Errors);
            int idxUnknownKey = joinedOrder.IndexOf("surprise", StringComparison.Ordinal);
            int idxKSpeed = joinedOrder.IndexOf("5/4", StringComparison.Ordinal);
            Assert.That(idxUnknownKey, Is.GreaterThanOrEqualTo(0), "未知键报文缺失");
            Assert.That(idxKSpeed, Is.GreaterThanOrEqualTo(0), "k_speed 报文缺失");
            Assert.That(idxUnknownKey, Is.LessThan(idxKSpeed),
                "聚合顺序须为「前言段(未知键)先于逐行段(k_speed)」;实得全集 = " + joinedOrder);
        }

        // ════════════════════════════════════════════════════════════════════
        // 仓根烘焙与确定性(ADR-014 §二)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_bake_from_repo_seed_tables_accepted()
        {
            ClinicMachineTablesBake.BakeOutput output = ClinicMachineTablesBake.BakeFromRepo(RepoRoot());

            Assert.That(output.ContextCooked.Length, Is.GreaterThanOrEqualTo(CookedFormat.HeaderSize));
            Assert.That(output.AdjCooked.Length, Is.GreaterThanOrEqualTo(CookedFormat.HeaderSize));
            Assert.That(output.RoomCooked.Length, Is.GreaterThanOrEqualTo(CookedFormat.HeaderSize));

            // 语义值断言(清债轮 · TD-clinic-08/09):「非空」不证哪些行被接受;钉到
            // 三表逐行计数与代表行值(Q16.16 raw:1=65536 · -1/4=-16384)。
            Assert.That(output.Context.Rows.Count, Is.EqualTo(3));
            Assert.That(output.Adj.Pairs.Count, Is.EqualTo(6));
            Assert.That(output.Room.Rooms.Count, Is.EqualTo(3));
            Assert.That(RowOf(output.Context, "ward").KSpeed.Raw, Is.EqualTo(65536L));
            Assert.That(PairW(output.Adj, "bed", "chamber_pot"), Is.EqualTo(-16384L));

            // 缺口哨兵:生产路径无闭集来源。
            Assert.That(output.Room.FurnMembershipEnforced, Is.False);
        }

        [Test]
        public void test_cooked_bytes_deterministic_for_same_source()
        {
            // ADR-014 §二:同源两次烘焙逐位一致。
            ClinicMachineTablesBake.BakeOutput first = ClinicMachineTablesBake.BakeFromRepo(RepoRoot());
            ClinicMachineTablesBake.BakeOutput second = ClinicMachineTablesBake.BakeFromRepo(RepoRoot());

            Assert.That(first.ConfigVersion, Is.EqualTo(second.ConfigVersion));
            Assert.That(first.ContextCooked, Is.EqualTo(second.ContextCooked));
            Assert.That(first.AdjCooked, Is.EqualTo(second.AdjCooked));
            Assert.That(first.RoomCooked, Is.EqualTo(second.RoomCooked));
        }

        // ════════════════════════════════════════════════════════════════════
        // 评审修复轮补充(2026-10-20)—— R-08 / R-17 / D9 / D1 / D2
        // 原件:production/qa/evidence/review-clinic-001-2026-10-20.md
        // ════════════════════════════════════════════════════════════════════

        // ── R-08:AC-24-05 的语义层(区间 vs 档集)────────────────────────────

        [Test]
        public void test_context_k_speed_in_range_but_not_in_set_rejected()
        {
            // 值 ∈ (0,1] 但 ∉ 档集 {1, 7/8, 3/4} ⇒ 必拒。
            // 这条是 AC-24-05「离散档集」语义的**唯一判别面**:若实现退化为
            // 纯区间判据(0 < raw <= 65536),本测转红 —— 缺了它,29 条测试对
            // 「区间 vs 档集」分叉完全不可判(评审 R-08)。
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindContextForTest(
                    FixtureText("invalid_context_k_speed_not_in_set.json")));

            AssertErrorsContain(ex, "∉ 档集");
        }

        // ── D9:ADJ 键的单射性前提(控制字符门)──────────────────────────────

        [Test]
        public void test_adj_type_containing_separator_rejected()
        {
            // 键 = a + U+0001 + b。a 含分隔符 ⇒ 不同 (a,b) 可拼出同一键 ⇒ 必须构建期拒。
            // 评审 D9:原注释声称有此门但零实现,本测是该门的判别面。
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindAdjForTest(
                    "{ \"schema_version\": 1, \"n_max\": 64, \"w_min\": \"-1/4\", \"w_max\": \"1/4\"," +
                    " \"pairs\": [ { \"a\": \"bed\\u0001x\", \"b\": \"pot\", \"w\": \"1/8\" } ] }"));

            AssertErrorsContain(ex, "控制字符");
        }

        // ── D1 / D2:FURN 形状与三姊妹 binder 同纪律 ─────────────────────────

        [Test]
        public void test_furn_empty_table_rejected()
        {
            // D1:空家具目录 ⇒ 所有房间谓词恒不匹配(整表静默失效),拒。
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineSchema.AssertFurnTableShape(
                    "{ \"schema_version\": 1, \"furn\": [] }"));

            AssertErrorsContain(ex, "furn 为空表");
        }

        [Test]
        public void test_furn_duplicate_type_rejected()
        {
            // D2:每家具类型恰一行;重复 ⇒ 拒。
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineSchema.AssertFurnTableShape(
                    "{ \"schema_version\": 1, \"furn\": [" +
                    " { \"type\": \"bed\", \"tier\": 0, \"e_env\": \"0\" }," +
                    " { \"type\": \"bed\", \"tier\": 1, \"e_env\": \"1/64\" } ] }"));

            AssertErrorsContain(ex, "重复");
        }

        // ── R-17:七类边界(均为已实现的生产守卫,原零测试)──────────────────

        [Test]
        public void test_context_empty_table_rejected()
        {
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindContextForTest(
                    "{ \"schema_version\": 1, \"contexts\": [] }"));

            AssertErrorsContain(ex, "contexts 为空");
        }

        [Test]
        public void test_adj_empty_pairs_rejected()
        {
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindAdjForTest(
                    "{ \"schema_version\": 1, \"n_max\": 64, \"w_min\": \"-1/4\", \"w_max\": \"1/4\"," +
                    " \"pairs\": [] }"));

            AssertErrorsContain(ex, "pairs 为空");
        }

        [Test]
        public void test_room_empty_table_rejected()
        {
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindRoomForTest(
                    "{ \"schema_version\": 1, \"rooms\": [] }", null));

            AssertErrorsContain(ex, "rooms 为空");
        }

        [Test]
        public void test_room_empty_clauses_rejected()
        {
            // 空谓词会匹配任一布局(房间身份退化为恒真),拒。
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindRoomForTest(
                    "{ \"schema_version\": 1, \"rooms\": [ { \"room_type\": \"ward\", \"clauses\": [] } ] }", null));

            AssertErrorsContain(ex, "clauses 为空");
        }

        [Test]
        public void test_adj_duplicate_pair_rejected()
        {
            // 同一有向对两行 ⇒ 「取哪一行」成为隐式规则(静默失序),拒。
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindAdjForTest(
                    "{ \"schema_version\": 1, \"n_max\": 64, \"w_min\": \"-1/4\", \"w_max\": \"1/4\"," +
                    " \"pairs\": [" +
                    " { \"a\": \"bed\", \"b\": \"pot\", \"w\": \"1/8\" }," +
                    " { \"a\": \"bed\", \"b\": \"pot\", \"w\": \"1/8\" } ] }"));

            AssertErrorsContain(ex, "重复键");
        }

        [Test]
        public void test_context_duplicate_room_type_rejected()
        {
            // 每房间类型恰一上下文行;重复 ⇒ 拒。
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindContextForTest(
                    "{ \"schema_version\": 1, \"contexts\": [" +
                    " { \"room_type\": \"ward\", \"context_id\": \"a\", \"k_speed\": \"1\", \"base_env\": \"0\" }," +
                    " { \"room_type\": \"ward\", \"context_id\": \"b\", \"k_speed\": \"1\", \"base_env\": \"0\" } ] }"));

            AssertErrorsContain(ex, "重复");
        }

        [Test]
        public void test_overflow_n_max_nonpositive_rejected()
        {
            // n_max ≤ 0(单房间格数上界须为正整数)—— 直驱该分支,不经 JSON。
            var errors = new List<string>();
            ClinicMachineSchema.AssertOverflowBounds(0L, 3L, -16384L, 16384L, errors);

            Assert.That(string.Join("; ", errors), Does.Contain("须为正整数"));
        }

        [Test]
        public void test_adj_wmin_equals_wmax_rejected()
        {
            // w_min == w_max 无法满足 W_MIN < 0 < W_MAX ⇒ 必拒。
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindAdjForTest(
                    "{ \"schema_version\": 1, \"n_max\": 64, \"w_min\": \"1/4\", \"w_max\": \"1/4\"," +
                    " \"pairs\": [ { \"a\": \"bed\", \"b\": \"pot\", \"w\": \"1/8\" } ] }"));

            AssertErrorsContain(ex, "W_MIN");
        }

        [Test]
        public void test_context_null_fix_value_rejected()
        {
            // null 不是「缺字段」也不是合法 Fix —— 必填,拒。
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ClinicMachineTablesBake.BindContextForTest(
                    "{ \"schema_version\": 1, \"contexts\": [ { \"room_type\": \"ward\"," +
                    " \"context_id\": \"a\", \"k_speed\": null, \"base_env\": \"0\" } ] }"));

            AssertErrorsContain(ex, "为 null");
        }
    }
}
