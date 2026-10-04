// interaction-system Story 007 —— `interaction_kinds.json` 烘焙接线(NR-1:校验器的装载路径;AC-4-15 装载半边)
//
// 登记落点: tests/unit/interaction/interaction_kinds_bake_test.cs
// 真身落点: unity/Assets/Tests/EditMode/Interaction/interaction_kinds_bake_test.cs
//
// 权威来源:
//   GDD design/gdd/interaction-system.md —— §数据契约(4-DC-1…6)/ 规则一 / AC-4-15 / AC-4-17(单源)
//   ADR-014 §二/§三/§五(两阶段烘焙 · 未知键硬失败 · 枚举明文 · ConfigVersion 内容哈希)
//
// ⚠️ 本故事的承重面 = **校验器的调用点存在**:
//   story-006 交付了 `InteractionKindTableValidator` + 13 个 C# 夹具,但结构侧评审 F-1 记为
//   **死代码**(无任何烘焙路径调用它)。本文件从**装载路径**这一侧再证一次 ——
//   六类违例**各一份 JSON 夹具**,断言**烘焙期硬失败**。
//   ⚠️ 负夹具是**端到端黑盒**:它们**不自己判 4-DC**(那是 story-006 的 C# 夹具的事),
//      只断言「这条路径抛」。⇒ 删掉 `InteractionKindBinder.Bind` 里的 `Validate(...)` 调用,
//      违例表会**静默烘出**产物 ⇒ 本文件的负夹具**转红**。这就是「检验器真的在路径上承重」的判据。
//
// ⚠️ 反空转(本仓头号失效模式)三件:
//   ① 合法夹具 + **仓库真种子**须**真通过**(证这条路径非恒拒);
//   ② 六类违例须**各红在己**(不能一条大 if 全红)—— 逐条独立断言;
//   ③ 断言须**触底到装载路径的具体产物/错误**(抛的是 `BakeValidationException` 且错误文本含 DC 号),
//      非「只要抛了就算」的空转形态。
//
// ⚠️ 驱动**生产**接缝:`InteractionKindBaker.BakeFromRepo`(菜单与测试共用的同一台机器),
//   不是测试侧的重实现。

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Interaction
{
    public class InteractionKindsBakeTest
    {
        // ═══════════════════════════════════════════════════════════
        //  夹具路径解析(仓库根;unity/Assets → 上两/三级到仓根)
        // ═══════════════════════════════════════════════════════════

        /// <summary>仓根:由**本文件路径**回溯(unity/Assets/Tests/EditMode/Interaction ⇒ 上五级)。
        /// <para>⚠️ 不能用 <c>AppContext.BaseDirectory</c> —— batch 模式下它指向 Unity 安装目录
        /// 而非仓根(2026-10-04 首次实跑:18 条测试同因「未找到仓根」全红)。承仓库既有先例
        /// (Audio/Telemetry/SkeuomorphicUI 各测试同款 <c>[CallerFilePath]</c> 回溯)。</para></summary>
        private static string RepoRoot => ComputeRepoRoot();

        private static string ComputeRepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        private static string FixtureDir => Path.Combine(RepoRoot, "tests", "unit", "interaction", "fixtures");

        /// <summary>用给定源文本烘焙(绕开仓根种子 —— 直接喂夹具)。</summary>
        private static byte[] BakeFromText(string kindsJson, string dimsJson)
            => InteractionKindBinderProbe.Bake(kindsJson, dimsJson);

        private static string ReadFixture(string name)
        {
            string path = Path.Combine(FixtureDir, name);
            Assert.IsTrue(File.Exists(path), $"夹具不存在:{path}");
            return File.ReadAllText(path);
        }

        private static string SeedKinds() => File.ReadAllText(
            Path.Combine(RepoRoot, "assets", "data", InteractionKindBaker.KindsFileName));
        private static string SeedDims() => File.ReadAllText(
            Path.Combine(RepoRoot, "assets", "data", InteractionKindBaker.DimensionsFileName));

        // ═══════════════════════════════════════════════════════════
        //  反空转 ①:合法输入须**真通过**(证路径非恒拒)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac415loader_legalBaselineBakesSuccessfully()
        {
            // 合法夹具 + 独立维度表 ⇒ 必须烘出产物(非抛)。若这条红了,下面六条负例红也无意义。
            byte[] cooked = BakeFromText(ReadFixture("legal_baseline.json"), SeedDims());
            Assert.IsNotNull(cooked, "合法夹具须烘出产物");
            Assert.Greater(cooked.Length, 20, "产物须含 20 字节头 + 载荷");
        }

        [Test]
        public void test_ac415loader_repoSeedBakesSuccessfully()
        {
            // 仓库真种子须**真通过** —— 这是「装载路径可用」的最直接判据。
            InteractionKindBaker.BakeOutput outp = InteractionKindBaker.BakeFromRepo(RepoRoot);
            Assert.AreEqual(10, outp.Rows.Count, "仓库种子须 10 行(4-DC-2 闭集十项)");
            // ⚠️ QA §5:`> 20` 太松 —— 截断的载荷也能满足。断言**精确字节长**:
            //    头 20 B(HeaderSize)+ 头后维度四元 4×int(16 B)+ 行数 int(4 B)
            //    + 10 行 × 单行宽度;单行 = KindPriority int(4)· RoutesTo int(4)·
            //    DurationOwnerSystemId int(4)· W/H/D 三 int(12) = 24 B 的 int 段,
            //    加 5 个 byte 枚举/布尔(Kind · StableIdSource · SuppressesMotor ·
            //    DurationOwnerKind · IntentUplink)= 5 B ⇒ 单行 29 B。
            const int headerAfterSize = 16 + 4;      // 维度四元 + 行数
            const int rowBytes = 24 + 5;             // int 段 + 5 个 byte 段
            Assert.AreEqual(CookedFormat.HeaderSize + headerAfterSize + 10 * rowBytes,
                outp.Cooked.Length, "真种子产物须为精确长度(证载荷无截断)");
            // 回读须得完整 10 行 —— 与长度断言互补(长度对而内容被截不可能同时成立)。
            InteractionKindDataSet ds = InteractionKindCookedCodec.Read(outp.Cooked);
            Assert.AreEqual(10, ds.Rows.Count, "回读须得完整 10 行(证载荷未截断)");
        }

        [Test]
        public void test_ac415loader_legalTablePassesValidatorAfterRoundTrip()
        {
            // 闭路:真种子烘焙 → 回读 → 回读行集喂回校验器**仍通过**(写读两端镜像 + 校验一致)。
            InteractionKindBaker.BakeOutput outp = InteractionKindBaker.BakeFromRepo(RepoRoot);
            InteractionKindDataSet ds = InteractionKindCookedCodec.Read(outp.Cooked);

            Assert.AreEqual(10, ds.Rows.Count, "回读须得 10 行");
            // ⚠️ QA F-7:维度**必须取自产物烘焙所用的那组**,不得写字面量 ——
            //    写死 `3,32,16,8` 时,改了 `interaction_kinds_dimensions.json` 本测试仍绿,
            //    却在校验一张**从未被烘过**的表(期望值来源在测试内部 = 空转形态)。
            Assert.DoesNotThrow(
                () => InteractionKindTableValidator.Validate(
                    ds.Rows, ds.RInteract, ds.WorldW, ds.WorldH, ds.WorldD),
                "回读所得行集喂回校验器须仍通过(闭路;维度取自产物自身)");
            Assert.AreEqual(outp.ConfigVersion, ds.ConfigVersion, "ConfigVersion 须往返一致");
        }

        // ═══════════════════════════════════════════════════════════
        //  反空转 ②:六类违例**各红在己**(端到端黑盒 —— 只断「这条路径抛」)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac415loader_dc1ZeroRadiusHardFails()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => BakeFromText(ReadFixture("invalid_dc1_zero_radius.json"),
                                   ReadFixture("invalid_dc1_zero_radius_dimensions.json")),
                "4-DC-1:r_interact=0 ⇒ 阶段 2 烘焙硬失败");
            Assert.IsTrue(Joined(ex).Contains("4-DC-1"), "违例须点名 DC 号");
        }

        [Test]
        public void test_ac415loader_dc1RadiusAboveUpperBoundHardFails()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => BakeFromText(ReadFixture("invalid_dc1_radius_above_upper_bound.json"),
                                   ReadFixture("invalid_dc1_radius_above_upper_bound_dimensions.json")),
                "4-DC-1 上界:r_interact > min(W,H,D)−1 ⇒ 阶段 2 烘焙硬失败");
            Assert.IsTrue(Joined(ex).Contains("4-DC-1"), "违例须点名 DC 号");
        }

        [Test]
        public void test_ac415loader_dc2MissingKindHardFails()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => BakeFromText(ReadFixture("invalid_dc2_missing_kind.json"), SeedDims()),
                "4-DC-2:kind 缺项 ⇒ 阶段 2 烘焙硬失败");
            Assert.IsTrue(Joined(ex).Contains("4-DC-2"), "违例须点名 DC 号");
        }

        [Test]
        public void test_ac415loader_dc3PriorityTieHardFails()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => BakeFromText(ReadFixture("invalid_dc3_priority_tie.json"), SeedDims()),
                "4-DC-3:KindPriority 平局 ⇒ 阶段 2 烘焙硬失败");
            Assert.IsTrue(Joined(ex).Contains("4-DC-3"), "违例须点名 DC 号");
        }

        [Test]
        public void test_ac415loader_dc4IllegalStableIdSourceHardFails()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => BakeFromText(ReadFixture("invalid_dc4_illegal_stable_id_source.json"), SeedDims()),
                "4-DC-4:StableIdSource 明文 ∉ 枚举 ⇒ 阶段 2 烘焙硬失败");
            // 该夹具的枚举拼错在**绑定期**即被拒(早于 4-DC-4 的值域检查)—— 断言错误文本含字段名。
            Assert.IsTrue(Joined(ex).Contains("stable_id_source"), "违例须点名字段");
        }

        [Test]
        public void test_ac415loader_dc5UnregisteredRoutesToHardFails()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => BakeFromText(ReadFixture("invalid_dc5_unregistered_routes_to.json"), SeedDims()),
                "4-DC-5:RoutesTo 未登记 ⇒ 阶段 2 烘焙硬失败");
            Assert.IsTrue(Joined(ex).Contains("4-DC-5"), "违例须点名 DC 号");
        }

        [Test]
        public void test_ac415loader_dc6SuppressWithoutDurationOwnerHardFails()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => BakeFromText(ReadFixture("invalid_dc6_suppress_without_duration_owner.json"), SeedDims()),
                "4-DC-6:SuppressesMotor=true 而 DurationOwner=None ⇒ 阶段 2 烘焙硬失败");
            Assert.IsTrue(Joined(ex).Contains("4-DC-6"), "违例须点名 DC 号");
        }

        // ═══════════════════════════════════════════════════════════
        //  ADR-014 §三 的 schema 侧硬失败(与 4-DC 正交的绑定层判据)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_binder_unknownKeyHardFails()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => BakeFromText(ReadFixture("invalid_unknown_key_typo.json"), SeedDims()),
                "未知键(拼写错误)⇒ 硬失败,防静默丢字段");
            Assert.IsTrue(Joined(ex).Contains("未知键"), "错误须点名「未知键」");
        }

        [Test]
        public void test_binder_badEnumLiteralHardFails()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => BakeFromText(ReadFixture("invalid_bad_enum_literal.json"), SeedDims()),
                "枚举明文拼错 ⇒ 硬失败(禁 int 编码 ⇒ 拼错不可静默默认)");
            Assert.IsTrue(Joined(ex).Contains("duration_owner"), "错误须点名字段");
        }

        [Test]
        public void test_binder_intEncodedEnumHardFails()
        {
            // ADR-014 §三:枚举一律按**明文字符串**读入(禁 int 编码 —— 承 AC-21a-26 先例)。
            // int 编码的枚举值必须硬失败,否则拼错的枚举序数与合法值不可区分(静默默认)。
            var ex = Assert.Throws<BakeValidationException>(
                () => BakeFromText(ReadFixture("invalid_int_encoded_enum.json"), SeedDims()),
                "枚举以 int 编码 ⇒ 硬失败(禁 int 编码的 state)");
            Assert.IsTrue(Joined(ex).Contains("明文字符串枚举"),
                "错误须点名「须为明文字符串枚举」");
        }

        [Test]
        public void test_binder_floatTokenInIntFieldHardFails()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => BakeFromText(ReadFixture("invalid_float_token_in_int_field.json"), SeedDims()),
                "float token 落在 int 字段 ⇒ 结构化硬失败(不靠数值转换)");
            Assert.IsTrue(Joined(ex).Contains("kind_priority"), "错误须点名字段");
        }

        // ═══════════════════════════════════════════════════════════
        //  反空转 ③:确定性 + ConfigVersion 派生(ADR-014 §二/§五)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac415loader_sameSourcesProduceBitIdenticalBytes()
        {
            // ⚠️ QA F-6 范围订正:本测试证的是**同进程内**两次烘焙逐位一致(写方固定字段序、
            //    零字典迭代、零本地时间 ⇒ 该半边成立)。**跨会话**(两次独立编辑器会话)逐位一致
            //    **未测** —— 登记 NOT-RUN,见 story §已知未闭。不得把本绿读作跨会话判据。
            byte[] a = InteractionKindBaker.BakeFromRepo(RepoRoot).Cooked;
            byte[] b = InteractionKindBaker.BakeFromRepo(RepoRoot).Cooked;
            CollectionAssert.AreEqual(a, b, "同源两次烘焙(同进程)须逐位一致(ADR-014 §二)");
        }

        [Test]
        public void test_ac415loader_configVersionChangesWithSourceContent()
        {
            uint seed = InteractionKindBaker.BakeFromRepo(RepoRoot).ConfigVersion;

            // 改一个数值(合法范围内:把 Drop 的 priority 9 → 10 会破对拍 ⇒ 改用维度表的一个无害改动)
            // ⚠️ QA F-3:替换**必须先证其真改到了东西** —— 否则种子一旦被格式化为 `"world_w":32`
            //    (空格消失),Replace 静默空转,本测试会因「两值相等」以**假原因**转红。
            string seedDims = SeedDims();
            string dims = seedDims.Replace("\"world_w\": 32", "\"world_w\": 24");
            Assert.AreNotEqual(seedDims, dims,
                "前置:替换须真改到源文本(种子被重新格式化 ⇒ 本测试失效,须改断言口径)");
            uint changed = InteractionKindBinderProbe.ConfigVersionOf(SeedKinds(), dims);

            Assert.AreNotEqual(seed, changed, "源内容任一字节变化 ⇒ ConfigVersion 必须变(内容哈希派生)");
            // AC(C) 三分句之一「**改名**」:同内容、不同文件名 ⇒ 版本必须不同
            // (ConfigVersionUtility 把文件名混进哈希;MUT 掉该行原测试仍绿 ⇒ 此臂必需)。
            uint renamed = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>("interaction_kinds_v2.json", SeedKinds()),
                new KeyValuePair<string, string>(InteractionKindBaker.DimensionsFileName, SeedDims()),
            });
            Assert.AreNotEqual(seed, renamed, "仅改**文件名**(内容不变)⇒ ConfigVersion 必须变");

        }

        [Test]
        public void test_ac415loader_roundTripPreservesRowsInOrder()
        {
            InteractionKindBaker.BakeOutput outp = InteractionKindBaker.BakeFromRepo(RepoRoot);
            InteractionKindDataSet ds = InteractionKindCookedCodec.Read(outp.Cooked);

            Assert.AreEqual(outp.Rows.Count, ds.Rows.Count, "行数须往返一致");
            for (int i = 0; i < outp.Rows.Count; i++)
            {
                Assert.AreEqual(outp.Rows[i].Kind, ds.Rows[i].Kind, $"第 {i} 行 Kind 须保序");
                Assert.AreEqual(outp.Rows[i].KindPriority, ds.Rows[i].KindPriority, $"第 {i} 行 priority 须一致");
                Assert.AreEqual(outp.Rows[i].RoutesTo, ds.Rows[i].RoutesTo, $"第 {i} 行 routes 须一致");
                Assert.AreEqual(outp.Rows[i].SuppressesMotor, ds.Rows[i].SuppressesMotor, $"第 {i} 行 suppress 须一致");
                Assert.AreEqual(outp.Rows[i].DurationOwner, ds.Rows[i].DurationOwner, $"第 {i} 行 dur owner 须一致");
                Assert.AreEqual(outp.Rows[i].DurationOwnerSystemId, ds.Rows[i].DurationOwnerSystemId, $"第 {i} 行 owner id 须一致");
                Assert.AreEqual(outp.Rows[i].StableIdSource, ds.Rows[i].StableIdSource, $"第 {i} 行 source 须一致");
                Assert.AreEqual(outp.Rows[i].IntentUplink, ds.Rows[i].IntentUplink, $"第 {i} 行 uplink 须一致");
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  4-DC-4 ② 的**装载路径**半边(GDD `:1026` 字面:SlotLinearKey 项须同表登记 W/H/D)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac415loader_dc4SlotLinearKeyMissingDimensionHardFails()
        {
            // 行自报 world_d=0(维度表本身合法 ⇒ 4-DC-1 通过)⇒ 4-DC-4 ② 必须独立承重。
            // ⚠️ 这条是 4-DC-4 ② 在**装载路径**上的唯一可达形态:若维度表取 0,4-DC-1 会先拦
            //    (min(W,H,D)−1 < r_interact),故 ② 的输入只能落在**行**上。
            var ex = Assert.Throws<BakeValidationException>(
                () => BakeFromText(ReadFixture("invalid_slotlinear_row_zero_dimension.json"), SeedDims()),
                "4-DC-4 ②:SlotLinearKey 行 W/H/D 未在场 ⇒ 阶段 2 烘焙硬失败");
            Assert.IsTrue(Joined(ex).Contains("4-DC-4"), "违例须点名 4-DC-4");
            Assert.IsTrue(Joined(ex).Contains("W/H/D 未在场"), "错误须触底到 W/H/D 判据本身");
        }

        [Test]
        public void test_ac415loader_dc4SlotLinearKeyWithOwnDimensionsPasses()
        {
            // 正向对照:行自报合法 W/H/D ⇒ 须通过(证该判据非恒拒)。
            Assert.DoesNotThrow(
                () => BakeFromText(SeedKinds(), SeedDims()),
                "仓库真种子(SlotLinearKey 行取维度表 W/H/D)须通过");
        }

        // ═══════════════════════════════════════════════════════════

        private static string Joined(BakeValidationException ex) => string.Join("\n", ex.Errors);
    }
}
