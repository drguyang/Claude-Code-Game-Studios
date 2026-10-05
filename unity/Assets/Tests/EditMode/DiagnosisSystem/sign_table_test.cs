// diagnosis-system Story 002 —— 体征词条表 schema 与 P0 数据行(required evidence)。
//
// 登记落点: tests/unit/diagnosis_system/README.md(AC→测映射)
// 真身落点: unity/Assets/Tests/EditMode/DiagnosisSystem/sign_table_test.cs
//
// 权威来源:
//   GDD design/gdd/diagnosis-system.md —— R-8.1(七字段)/ R-8.2(P0 冻结清单)/
//     AC-8-32(字段完备性)/ AC-8-33(无 P1a)/ AC-8-34(外键闭合)/ AC-8-35(加行常量不变)/
//     C-6(neg_weight 仅阴性非空且 >0)/ D-8-9 · TR-diag-014(SLOT_BOUNDS ⊂ DIAG_TIERS)
//   ADR-014 §二/§三/§五(两阶段烘焙 · 未知键硬失败 · 枚举明文 · Fix 字符串 · ConfigVersion)
//   Story: production/epics/diagnosis-system/story-002-sign-lexicon-schema-and-p0-rows.md
//
// ⚠️ 本故事的承重面 = **校验器的调用点存在**(承 interaction story-007 F-1 教训):
//   DiagnosisSignBinder.Bind 是 DiagnosisSignTableValidator 的**唯一调用点** ——
//   负夹具全部是端到端黑盒(只断言「这条路径抛 + 错误文本含规则 tag + 恰一条错误」),
//   删掉 Bind() 里的 Validate(...) ⇒ 违例表静默烘出 ⇒ 本文件负例转红。
//
// ⚠️ 反空转四件(修复轮起):
//   ① 仓库真种子 + 合法夹具须**真通过**(证路径非恒拒);
//   ② 每类违例**各红在己**(经 BakeFails:断言触底到规则 tag + **恰一条错误**排他 ——
//      夹具串味 / 多错折叠即此抓);
//   ③ 扫描面/行集断言 Is.Not.Empty(防空过滤跑假绿 —— 承 story-001 Q-5);
//   ④ 内容金标:R-8.2 34 行的 id|通道|档|极性|揭示法|三档词 逐字冻结(QA MAJOR-1)。
//
// ⚠️ 驱动**生产**接缝:DiagnosisSignBaker.BakeFromRepo(菜单与测试共用同一台机器)、
//   DiagnosisSignBinderProbe.Bake(夹具直喂)—— 不是测试侧的重实现。
//
// ═════════════════════════════════════════════════════════════════════════
// ⚠️ NOT-RUN 显式登记(禁借绿 —— story Test Evidence 要求):
//   · **AC-8-34 反向孤儿子句 NOT-RUN(BLOCKED-BY-disease epic story 006 / TR-diag-013
//     no-adr-by-design)** —— 反向孤儿 = 「未被 9 的 R1 signs[] 引用的词」须显式标注「预留」;
//     该判据的输入(9 侧注册表)未落地 ⇒ test_ac834_reverseOrphan_blockedish 以 [Ignore] 挂起,
//     方法体诚实计算孤儿(去 Ignore 即 34 条孤儿全红 = 形态真实,非空转)。
//   · 正向外键闭合(9 → 8)本 story 即判:机制(ValidateForwardClosure)+ 负夹具 + 种子正例。
//   · tripwire:test_ac834_forward9sideSignsStillEmpty —— 现 F2 投影未实现
//     (ProgressionEvaluator 返回空 signs[],归 disease story 004);9 填入真实 sign_id 后本测转红,
//     强制把 Evaluate 产出的 signs[] 接进 ValidateForwardClosure(接线义务的触发器,非回归)。
//   · **AC-8-35 的 F-8.1/F-8.3 参数半边 NOT-RUN(QA MAJOR-2;story-003 复核后订正)** ——
//     扫描面 = 前缀 ns **代码常量** + DIAG_TIERS。story-003 已把 **运行期枚举 / 映射常量**
//     (DiagnosisSlot / SignReadState 枚举字面 + DiagnosisChannelMaskMap 静态数组)扩入面并
//     重钉 `b9354110` → `5bba361c`;**曲线系数**(BASE_READ / READ_FLOOR_MIN / READ_GAMMA)
//     与 NEG_CONF 族(story 004)**仍 NOT-RUN** —— 它们是 `assets/data/*.json` **数据**,
//     由产物 ConfigVersion 覆盖,非前缀 ns 代码常量(故「参数半边」仍不在此面)。
//   · **跨会话烘焙逐位一致 NOT-RUN**(承 interaction story-007 同款口径)——
//     本文件只证**同进程**内双跑一致。
// ═════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using NUnit.Framework;
// ⚠️ CS0104 消歧:`Sim.Contracts.SignChannel`(AC-21 位掩码静态类)与本表
//    R-8.1 通道**枚举**同名 —— 本文件 `SignChannel` 一律指枚举(枚举值断言用)。
using SignChannel = DaYiJingCheng.Gameplay.Presentation.Diagnosis.SignChannel;

namespace DaYiJingCheng.Tests.DiagnosisSystem
{
    [TestFixture]
    internal sealed class SignTableTest
    {
        // ═══════════════════════════════════════════════════════════
        //  夹具路径解析(仓库根;unity/Assets/Tests/... → 上五级)
        // ═══════════════════════════════════════════════════════════

        /// <summary>仓根:由**本文件路径**回溯(承 interaction/audio 先例)。
        /// ⚠️ 不能用 <c>AppContext.BaseDirectory</c> —— batch 模式指向 Unity 安装目录。</summary>
        private static string RepoRoot => ComputeRepoRoot();

        private static string ComputeRepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        private static string FixtureDir =>
            Path.Combine(RepoRoot, "tests", "unit", "diagnosis_system", "fixtures");

        private static string ReadFixture(string name)
        {
            string path = Path.Combine(FixtureDir, name);
            Assert.IsTrue(File.Exists(path), $"夹具不存在:{path}");
            return File.ReadAllText(path);
        }

        private static string SeedJson() => File.ReadAllText(
            Path.Combine(RepoRoot, "assets", "data", DiagnosisSignBaker.SignsFileName));

        /// <summary>夹具/种子 → 完整阶段 2(绑定 + 校验 + 编码)→ 回读行集。</summary>
        private static List<SignLexemeRow> RowsOf(string signsJson)
            => DiagnosisSignCookedCodec.Read(DiagnosisSignBinderProbe.Bake(signsJson)).Rows;

        private static string Joined(BakeValidationException ex) => string.Join("\n", ex.Errors);

        /// <summary>违例夹具驱动(端到端黑盒,QA MINOR-5 收口):
        /// 必须抛 <see cref="BakeValidationException"/>、**恰一条错误**(排他断言 ——
        /// 夹具串味 / 多错折叠即此抓)、且错误文本触底到全部给定规则 tag。</summary>
        private static BakeValidationException BakeFails(string signsJson, params string[] tags)
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => DiagnosisSignBinderProbe.Bake(signsJson), "违例源须聚合硬失败(绝不静默烘出)");
            Assert.AreEqual(1, ex.Errors.Count,
                "单违例夹具须恰一条错误(排他)—— 夹具串味 / 多错折叠即此抓\n实得:\n" + Joined(ex));
            string joined = Joined(ex);
            foreach (string tag in tags)
                Assert.IsTrue(joined.Contains(tag),
                    $"错误须触底到规则「{tag}」—— 实得:\n{joined}");
            return ex;
        }

        /// <summary>最小合法单行 JSON(P1a 闭集参数化用;channel / reveal_by 为参数)。</summary>
        private static string SingleSignJson(string channelJson, string revealJson) =>
            "{\"schema_version\":1,\"signs\":[{" +
            "\"sign_id\":\"sign_p1a_probe\"," +
            "\"display_词\":[\"粗档词\",\"中档词\",\"细档词\"]," +
            "\"channel\":" + channelJson + "," +
            "\"reveal_by\":" + revealJson + "," +
            "\"tier_named\":1," +
            "\"polarity\":\"阳性体征\"," +
            "\"neg_weight\":null}]}";

        // 仓库种子的 34 条主键(GDD R-8.2 冻结清单逐字 —— 任何增删改名必须过 /design-review)
        private static readonly string[] PinnedSignIds =
        {
            "sign_pallor", "sign_flush", "sign_diaphoresis", "sign_rash", "sign_koplik", "sign_jaundice",
            "sign_voice_weak", "sign_delirium", "sign_moan", "sign_dyspnea_speech",
            "sign_orthopnea", "sign_curled", "sign_trismus", "sign_opisthotonus", "sign_neck_stiffness",
            "sign_risus", "sign_joint_swelling",
            "sign_tachypnea", "sign_rales", "sign_retraction", "sign_cheyne_stokes", "sign_dullness",
            "sign_fever_skin", "sign_cold_clammy", "sign_pulse_rapid", "sign_pulse_bounding",
            "sign_relative_bradycardia", "sign_hepatosplenomegaly", "sign_abd_tenderness",
            "sign_purulent_stool",
            "sign_lung_clear", "sign_abd_soft", "sign_neck_supple", "sign_no_organomegaly",
        };

        // ═══════════════════════════════════════════════════════════
        //  反空转 ①:仓库真种子 + 合法夹具须**真通过**
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac832_repoSeedBakes34Rows()
        {
            // 仓库真种子须**真通过** —— 装载路径可用的最直接判据(非恒拒)。
            DiagnosisSignBaker.BakeOutput outp = DiagnosisSignBaker.BakeFromRepo(RepoRoot);
            Assert.That(outp.Rows, Is.Not.Empty, "扫描面自证(空集 ⇒ 种子/键失配,单跑假绿面)");
            Assert.AreEqual(34, outp.Rows.Count, "R-8.2 冻结清单 = 34 行(阳性 30 + 阴性 4)");
            Assert.Greater(outp.Cooked.Length, 20, "产物须含 20 字节头 + 载荷");
        }

        [Test]
        public void test_ac832_legalBaselineBakesSuccessfully()
        {
            // 合法夹具(1 阳 + 1 阴)⇒ 必须烘出产物并解出恰 2 行(QA MINOR-2:
            // 原 IsNotNull 近恒真 —— 升级为解码 + 行数 + 首行主键)。
            byte[] cooked = DiagnosisSignBinderProbe.Bake(ReadFixture("legal_baseline.json"));
            Assert.IsNotNull(cooked, "合法夹具须烘出产物");
            Assert.Greater(cooked.Length, 20, "产物须含 20 字节头 + 载荷");

            List<SignLexemeRow> rows = DiagnosisSignCookedCodec.Read(cooked).Rows;
            Assert.AreEqual(2, rows.Count, "合法夹具 = 1 阳 + 1 阴须解出 2 行");
            Assert.AreEqual("sign_pallor", rows[0].SignId, "首行主键须逐字(写读镜像)");
            Assert.AreEqual("sign_lung_clear", rows[1].SignId, "次行主键须逐字(写读镜像)");
        }

        [Test]
        public void test_ac832_roundTrip_validatorStillPasses_configVersionStable()
        {
            // 闭路:真种子烘焙 → 回读 → 回读行集喂回校验器**仍通过**(写读镜像 + 校验一致)。
            DiagnosisSignBaker.BakeOutput outp = DiagnosisSignBaker.BakeFromRepo(RepoRoot);
            DiagnosisSignDataSet ds = DiagnosisSignCookedCodec.Read(outp.Cooked);

            Assert.AreEqual(34, ds.Rows.Count, "回读须得 34 行(证载荷未截断)");
            Assert.DoesNotThrow(
                () => DiagnosisSignTableValidator.Validate(ds.Rows),
                "回读所得行集喂回校验器须仍通过(闭路)");
            Assert.AreEqual(outp.ConfigVersion, ds.ConfigVersion, "ConfigVersion 须往返一致");
            Assert.AreEqual(1u, ds.SchemaVersion, "schema_version = 1 须随产物落盘");
            Assert.AreEqual(DiagnosisSignBinderProbe.ConfigVersionOf(SeedJson()),
                outp.ConfigVersion, "ConfigVersion = 源文本内容哈希派生(与生产同源)");
        }

        // ═══════════════════════════════════════════════════════════
        //  反空转 ②:违例夹具**各红在己**(端到端黑盒 —— BakeFails:
        //  抛 + 恰一条错误 + 触底到规则 tag)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac832_channelP1aTongue_dualTagRed()
        {
            // AC-8-32 五通道闭集 + AC-8-33 P1a 禁入 —— 同一条错误**叠双 tag**。
            BakeFails(ReadFixture("invalid_channel_tongue.json"),
                "AC-8-32·channel", "AC-8-33·channel");
        }

        [Test]
        public void test_ac833_revealP1aWang_dualTagRed()
        {
            BakeFails(ReadFixture("invalid_reveal_wang.json"),
                "AC-8-32·reveal_by", "AC-8-33·reveal_by");
        }

        /// <summary>P1a 通道其余四值(舌在夹具;脉/情志/体质/时序参数化 —— QA MINOR-3:
        /// P1a 闭集**全值**覆盖,防只测首值)。</summary>
        [TestCase("\"脉\"")]
        [TestCase("\"情志\"")]
        [TestCase("\"体质\"")]
        [TestCase("\"时序\"")]
        public void test_ac833_p1aChannelValues_dualTagRed(string channelJson)
        {
            BakeFails(SingleSignJson(channelJson, "[\"视诊\"]"),
                "AC-8-32·channel", "AC-8-33·channel");
        }

        /// <summary>P1a 手段其余两值(望在夹具;闻/切参数化 —— QA MINOR-3 全值覆盖)。
        /// ⚠️ reveal_by 是**数组**字段 —— 用例值须为 JSON 数组(裸字符串会先撞「须为数组」,
        /// 拿不到 AC-8-33 双 tag;bootstrap 实测教训)。</summary>
        [TestCase("[\"闻\"]")]
        [TestCase("[\"切\"]")]
        public void test_ac833_p1aRevealValues_dualTagRed(string revealJson)
        {
            BakeFails(SingleSignJson("\"面色\"", revealJson),
                "AC-8-32·reveal_by", "AC-8-33·reveal_by");
        }

        [Test]
        public void test_ac832_revealEmptyArrayRed()
        {
            BakeFails(ReadFixture("invalid_reveal_empty.json"), "AC-8-32·reveal_by");
        }

        [Test]
        public void test_ac832_tier35TestLineRed()
        {
            // 35 = 检验线(P1a),8 侧刻意不进枚举(D-8-9 / GDD F-8.2)。
            // 触底断言钉「tier_named=35」(QA MINOR-1:裸 "35" 会被他处 35 误中)。
            BakeFails(ReadFixture("invalid_tier_35.json"),
                "AC-8-32·tier_named", "tier_named=35");
        }

        [Test]
        public void test_ac832_tierMidValue15Red()
        {
            BakeFails(ReadFixture("invalid_tier_mid.json"),
                "AC-8-32·tier_named", "tier_named=15");
        }

        [Test]
        public void test_ac832_positiveWithNegWeightRed()
        {
            BakeFails(ReadFixture("invalid_positive_with_weight.json"), "AC-8-32·neg_weight");
        }

        [Test]
        public void test_ac832_negativeMissingNegWeightRed()
        {
            // C-6:neg_weight 仅阴性非空且 >0 —— 缺失即拒(把握度公式无从谈起)。
            BakeFails(ReadFixture("invalid_negative_missing_weight.json"),
                "AC-8-32·neg_weight", "C-6");
        }

        [Test]
        public void test_ac832_negativeZeroNegWeightRed()
        {
            // C-6 >0:0 与缺失同罪(QA MINOR-6:补 C-6 触底,与 missing 条同标)。
            BakeFails(ReadFixture("invalid_negative_zero_weight.json"),
                "AC-8-32·neg_weight", "C-6");
        }

        [Test]
        public void test_ac832_displayTwoSlotsRed()
        {
            BakeFails(ReadFixture("invalid_display_two_slots.json"), "AC-8-32·display_词");
        }

        [Test]
        public void test_ac832_displayEmptyStringRed()
        {
            // 空串与阴性形态在呈现层同构 ⇒ 空档必须是 null(story Implementation Note 1)。
            BakeFails(ReadFixture("invalid_display_empty_string.json"), "AC-8-32·display_词");
        }

        [Test]
        public void test_ac832_polarityMissingRed()
        {
            BakeFails(ReadFixture("invalid_polarity_missing.json"), "AC-8-32·polarity");
        }

        [Test]
        public void test_ac832_polarityIllegalValueRed()
        {
            // 修复轮新增:极性明文不在 {阳性体征,阴性体征} 闭集 ⇒ 拒(QA MINOR-4)。
            BakeFails(ReadFixture("invalid_polarity_illegal.json"), "AC-8-32·polarity");
        }

        [Test]
        public void test_ac832_signIdMissingRed()
        {
            // 修复轮新增:主键字段缺失 ⇒ 拒(QA MINOR-4;R-8.1 主键的必填半边)。
            BakeFails(ReadFixture("invalid_sign_id_missing.json"), "AC-8-32·sign_id");
        }

        [Test]
        public void test_binder_unknownKeyHardFails()
        {
            // ADR-014 §三:未知键 = 硬失败,防拼写错误静默丢字段。
            BakeFails(ReadFixture("invalid_unknown_key.json"), "ADR-014·unknown-key");
        }

        [Test]
        public void test_binder_negWeightFloatTokenHardFails()
        {
            // ADR-014 §三:Fix 字段在 JSON 里写字符串 —— 浮点数字 token 即拒(不走 double 中转)。
            BakeFails(ReadFixture("invalid_neg_weight_float_token.json"), "ADR-014·fix-string");
        }

        [Test]
        public void test_binder_negWeightUnparseableHardFails()
        {
            // 修复轮新增:"abc" 不可解析 Fix 字面量 ⇒ 聚合 ADR-014·fix-string
            // (结构侧 S-M1:catch 整型 Exception —— "1/0" 的 DivideByZeroException 同不得逃逸)。
            BakeFails(ReadFixture("invalid_neg_weight_unparseable.json"), "ADR-014·fix-string");
        }

        [Test]
        public void test_r81_emptySignsRejectedRed()
        {
            // 修复轮新增:空词表静默出货 = 以空集冒充绿(反假绿纪律)⇒ 硬失败(结构侧 S-4)。
            BakeFails(ReadFixture("invalid_empty_signs.json"), "R-8.1·signs-empty");
        }

        [Test]
        public void test_r81_duplicateSignIdRed()
        {
            BakeFails(ReadFixture("invalid_duplicate_sign_id.json"), "R-8.1·sign_id");
        }

        [Test]
        public void test_ac833_labRowRed()
        {
            BakeFails(ReadFixture("invalid_lab_row.json"), "AC-8-33·lab");
        }

        [Test]
        public void test_ac834_historyChannelNotWhitelistedRed()
        {
            // 病史通道(通道外第二类证据)仅白名单给 sign_purulent_stool —— 其余行借用即新造通道。
            BakeFails(ReadFixture("invalid_history_channel_not_whitelisted.json"),
                "AC-8-34·channel-exception");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-8-34:词表 ↔ 9 外键闭合(正向本 story 判;反向 NOT-RUN)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac834_forwardMissingReferencedSign_red()
        {
            // 9 的 signs[] 引用了词表没有的 id ⇒ 红(负夹具承 ValidateForwardClosure 判据)。
            List<SignLexemeRow> rows = RowsOf(SeedJson());
            Assert.That(rows, Is.Not.Empty, "扫描面自证");

            var ex = Assert.Throws<ArgumentException>(
                () => DiagnosisSignTableValidator.ValidateForwardClosure(
                    rows, new[] { "sign_pallor", "sign_not_in_table" }),
                "外键悬空 ⇒ 硬失败(9 引用却无登记)");
            Assert.IsTrue(ex.Message.Contains("AC-8-34·fk"), "违例须点名 AC-8-34·fk");
            Assert.IsTrue(ex.Message.Contains("sign_not_in_table"), "错误须触底到悬空 id");
        }

        [Test]
        public void test_ac834_forwardSubsetPasses()
        {
            // 正向对照:引用集 ⊆ 词表 ⇒ 通过(证判据非恒拒)。含病史行(两类外键目标)。
            List<SignLexemeRow> rows = RowsOf(SeedJson());
            Assert.DoesNotThrow(
                () => DiagnosisSignTableValidator.ValidateForwardClosure(
                    rows, new[] { "sign_pallor", "sign_lung_clear", "sign_purulent_stool" }),
                "引用集 ⊆ 词表须通过(含病史类外键目标)");
        }

        /// <summary>
        /// <b>tripwire(可执行接线义务)</b>:F2 投影产的 signs[] 现恒空
        /// (ProgressionEvaluator 简化实现,归 disease story 004)。
        /// 9 侧一旦填入真实 sign_id,本断言**转红** —— 转红即提示把 Evaluate 产出的
        /// signs[] 接进 <see cref="DiagnosisSignTableValidator.ValidateForwardClosure"/>
        /// (AC-8-34 正向外键闭合的生产调用点)。禁以「测试红了」为由改回空断言。
        /// </summary>
        [Test]
        public void test_ac834_forward9sideSignsStillEmpty_tripwire()
        {
            var registry = new DiseaseRegistryEntry
            {
                DiseaseId = 1,
                DiseaseKey = "tripwire_disease",
                Polarity = 0,
                Severity = 3,
                Contagion = 1,
                Lethality = 0,
                TreatmentDifficulty = 5,
                RecoveryTime = 100,
                RelapseChance = 0,
                ComorbidityFactor = 0,
                SeasonalMod = 0,
                AgeMod = 0,
                GenderMod = 0,
                OccupationMod = 0,
                RegionMod = 0,
                ClimateMod = 0,
            };

            ProgressionResult result = ProgressionEvaluator.Evaluate(
                registry, 1000, 1500,
                new List<SimEvent>(), 12345UL, new PatientId(1));

            Assert.That(result.Signs, Is.Not.Null, "Signs 不得为 null(形状前提)");
            Assert.IsEmpty(result.Signs,
                "tripwire:F2 投影(signs[])仍未实现 —— 9 侧填入真实 sign_id 后本断言转红," +
                "强制把 Evaluate 产出的 signs[] 接进 ValidateForwardClosure(AC-8-34 生产调用点)。" +
                "转红 = 接线义务的触发信号,非回归 —— 禁改回空断言。");
        }

        /// <summary>
        /// AC-8-34 **反向孤儿子句** —— NOT-RUN(BLOCKED-BY-disease epic story 006 / TR-diag-013)。
        /// <para>去 Ignore 即诚实红:9 侧空 ⇒ 34 条全部孤儿(「预留」标注机制随 disease 侧注册表落地)。
        /// 禁借绿:不得把断言改松以求过。</para>
        /// </summary>
        [Test]
        [Ignore("BLOCKED-BY-disease epic story 006 / TR-diag-013(no-adr-by-design)—— " +
                "反向孤儿子句 NOT-RUN:9 的 R1 注册表未落地,引用集只能取空 ⇒ 去 Ignore 即 34 条孤儿全红" +
                "(形态诚实)。禁借绿。")]
        public void test_ac834_reverseOrphan_blockedish()
        {
            List<SignLexemeRow> rows = RowsOf(SeedJson());
            var referenced = new List<string>();   // 9 侧现状:零注册项

            var orphans = rows
                .Select(r => r.SignId)
                .Where(id => !referenced.Contains(id))
                .ToList();

            Assert.That(orphans, Is.Empty,
                "未引用孤儿须显式标注「预留」—— 现 34 条全部未被引用(9 侧未落地,本句 NOT-RUN)");
        }

        // ═══════════════════════════════════════════════════════════
        //  TR-diag-014 / D-8-9:SLOT_BOUNDS ⊂ DIAG_TIERS 双向耦合
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_trdiag014_slotBoundsCoupled_green()
        {
            // 现值:DIAG_TIERS {10,20,35,50} · SLOT_BOUNDS {10,20,50} ⇒ 耦合通过。
            Assert.DoesNotThrow(
                () => DiagnosisSignTableValidator.AssertSlotBoundsCoupled(
                    SkillTuningTable.Default.GetDiagTiers(), DiagnosisTuning.SlotBounds),
                "现值须耦合通过(35 为检验线,去 35 后 == 槽边界)");
        }

        [Test]
        public void test_trdiag014_slotBoundsDrift21_red()
        {
            // 30 侧把 20 改成 21 而本侧未改 ⇒ 纯函数必红(QA 用例「双向耦合」)。
            var ex = Assert.Throws<ArgumentException>(
                () => DiagnosisSignTableValidator.AssertSlotBoundsCoupled(
                    new[] { 10, 21, 35, 50 }, new[] { 10, 20, 50 }),
                "DIAG_TIERS 漂移 21 而 SLOT_BOUNDS 仍旧 ⇒ 构建期红");
            Assert.IsTrue(ex.Message.Contains("TR-diag-014"), "违例须点名 TR-diag-014");
        }

        // ═══════════════════════════════════════════════════════════
        //  R-8.2 清单存在性 + 内容金标(只承载,不改医学身份)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_r82_existence_pinnedIdsCountsAndTiers()
        {
            List<SignLexemeRow> rows = RowsOf(SeedJson());
            Assert.That(rows, Is.Not.Empty, "扫描面自证");

            // ① 主键集 == 冻结清单(增删改名必须过 /design-review)
            CollectionAssert.AreEquivalent(
                PinnedSignIds, rows.Select(r => r.SignId).ToArray(),
                "R-8.2 主键集须与 GDD 冻结清单逐字一致(34 项)");

            // ② 极性分布:阳性 30 / 阴性 4
            List<SignLexemeRow> negatives = rows.Where(r => r.Polarity == SignPolarity.Negative).ToList();
            List<SignLexemeRow> positives = rows.Where(r => r.Polarity == SignPolarity.Positive).ToList();
            Assert.AreEqual(4, negatives.Count, "阴性体征 = 4 行");
            Assert.AreEqual(30, positives.Count, "阳性体征 = 30 行");

            // ③ 阴性侧:全 tier_named = Lv20 + neg_weight raw > 0(C-6;不钉占位量级)
            foreach (SignLexemeRow n in negatives)
            {
                Assert.AreEqual(20, n.TierNamed, $"阴性 {n.SignId} 须 tier_named=20(与 L*_j 同锚)");
                Assert.That(n.NegWeightRaw, Is.Not.Null, $"阴性 {n.SignId} 须带 neg_weight");
                Assert.That(n.NegWeightRaw.Value, Is.GreaterThan(0), $"阴性 {n.SignId} neg_weight 须 >0(C-6)");
            }

            // ④ 阳性侧:全部不带 neg_weight
            foreach (SignLexemeRow p in positives)
                Assert.That(p.NegWeightRaw, Is.Null, $"阳性 {p.SignId} 不得带 neg_weight");

            // ⑤ 病史通道恰好 1 行 = sign_purulent_stool(通道外第二类证据)
            List<SignLexemeRow> history = rows.Where(r => r.Channel == SignChannel.History).ToList();
            Assert.AreEqual(1, history.Count, "病史通道恰好 1 行");
            Assert.AreEqual("sign_purulent_stool", history[0].SignId, "病史通道 = sign_purulent_stool");

            // ⑥ 五条物理通道全部在场(恰好五个值,无新造 —— AC-8-32)
            var channels = rows.Select(r => r.Channel)
                .Where(c => c != SignChannel.History)
                .Distinct()
                .ToArray();
            CollectionAssert.AreEquivalent(
                new[]
                {
                    SignChannel.FaceColor, SignChannel.Voice, SignChannel.Posture,
                    SignChannel.Breathing, SignChannel.Touch,
                },
                channels, "五通道须全部被使用(且无第六值)");
        }

        /// <summary>R-8.2 **内容金标**(QA MAJOR-1):34 行的
        /// id|通道序数|档|极性序数|揭示法序数串|粗档|中档|细档 逐字冻结。
        /// <para>存在性金标(test_r82_existence)不钉词与通道 —— 本测补上:任何词改动 /
        /// 通道挪动 / 档漂移 ⇒ 金标红,须**过 /design-review 并有意识重钉**(禁顺手重钉)。</para></summary>
        private const string GoldenContentHash = "3954e294";

        [Test]
        public void test_r82_contentFrozen_golden()
        {
            List<string> lines = BuildContentLines();
            Assert.AreEqual(34, lines.Count, "R-8.2 冻结行数 = 34");
            string actual = Fnv1aHex(lines);
            if (!string.Equals(GoldenContentHash, actual, StringComparison.Ordinal))
                Assert.Fail(
                    $"R-8.2 内容金标漂移:期望 {GoldenContentHash},实得 {actual} —— " +
                    "id/通道/档/极性/揭示法/三档词任一变化 ⇒ 须过 /design-review 并**有意识**重钉" +
                    "(禁顺手重钉)。\n实际内容行(id|channel|tier|polarity|reveal|粗|中|细):\n" +
                    string.Join("\n", lines));
        }

        /// <summary>内容行集:id|channel|tier|polarity|reveal 串|三档词(null → &lt;null&gt;),
        /// 按 ordinal 排序(行序无关,内容逐字有关)。</summary>
        private static List<string> BuildContentLines()
        {
            List<SignLexemeRow> rows = RowsOf(SeedJson());
            Assert.That(rows, Is.Not.Empty, "内容扫描面自证(空集 ⇒ 假绿面)");

            var lines = new List<string>(rows.Count);
            foreach (SignLexemeRow r in rows)
            {
                string w0 = r.DisplayWords[0] ?? "<null>";
                string w1 = r.DisplayWords[1] ?? "<null>";
                string w2 = r.DisplayWords[2] ?? "<null>";
                var reveal = new string[r.RevealBy.Length];
                for (int i = 0; i < r.RevealBy.Length; i++)
                    reveal[i] = ((int)r.RevealBy[i]).ToString(CultureInfo.InvariantCulture);
                lines.Add(
                    $"{r.SignId}|{(int)r.Channel}|{r.TierNamed}|{(int)r.Polarity}|" +
                    $"{string.Join(",", reveal)}|{w0}|{w1}|{w2}");
            }
            lines.Sort(StringComparer.Ordinal);
            return lines;
        }

        [Test]
        public void test_ac832_koplikEmptySlotExpressible()
        {
            // GDD:粗/中档 = —(要想到去查) → 显式 null(F-8.2 空白档回退的可表达性);
            // 括注「要想到去查」= 设计者批注,不入数据。禁空串(与阴性形态同构)。
            List<SignLexemeRow> rows = RowsOf(SeedJson());
            SignLexemeRow koplik = rows.Single(r => r.SignId == "sign_koplik");

            Assert.IsNull(koplik.DisplayWords[0], "koplik 粗档须为 null(空档记号)");
            Assert.IsNull(koplik.DisplayWords[1], "koplik 中档须为 null(空档记号)");
            Assert.AreEqual("口腔黏膜斑(Koplik 斑)", koplik.DisplayWords[2],
                "koplik 细档须逐字(半角括号,GDD R-8.2 冻结)");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-8-35:加一条体征 ⇒ 常量表哈希逐位不变
        // ═══════════════════════════════════════════════════════════

        /// <summary>AC-8-35 金标(FNV-1a 32 over 有序常量行 UTF8)。
        /// <para>⚠️ story-003 起**单一真源** = <see cref="DiagnosisGoldenScan.GoldenConstantsHash"/>
        /// (story-002 头注承诺的「扩金标」已兑现 —— story-003 新增 F-8.1 参数面后重钉,
        /// 两测试文件共用同一扫描器,不再各持一份)。</para>
        /// <para>前缀侧常量 / 枚举字面 / DIAG_TIERS 任一改动 ⇒ 金标红 ——
        /// 须**有意识**重钉(数值轮),不得顺手重钉。</para></summary>
        private const string GoldenConstantsHash = DiagnosisGoldenScan.GoldenConstantsHash;

        [Test]
        public void test_ac835_constantsGolden()
        {
            List<string> lines = BuildConstantLines();
            Assert.That(lines, Is.Not.Empty, "常量扫描面自证(空集 ⇒ 反射键失配,假绿面)");
            Assert.AreEqual(GoldenConstantsHash, Fnv1aHex(lines),
                "AC-8-35 常量表哈希须等于钉住的金标 —— 前缀侧静态常量 / 枚举字面 / DIAG_TIERS 任一改动" +
                "都会改变它(数值轮 / 结构变更须有意识地重钉,不是顺手重钉)");
        }

        [Test]
        public void test_ac835_addRow_constantsUnchanged()
        {
            // QA 用例「哈希不变」:JSON 只加一行(仅逐条字段)⇒ 两份种子都烘得出,
            // 且常量哈希前后相等 —— 加行**不需要**动任何代码常量(结构前提)。
            string seed = SeedJson();
            List<string> linesBefore = BuildConstantLines();

            // 绑定金标(QA MINOR-2):前后自比较恒真 —— 钉住「before 已在金标上」才可证伪。
            Assert.AreEqual(GoldenConstantsHash, Fnv1aHex(linesBefore),
                "AC-8-35:扩展烘焙前常量哈希须已在金标上(把本测与金标绑死,防同进程自比较恒真)");

            DiagnosisSignBaker.BakeOutput seedOutp = DiagnosisSignBaker.BakeFromRepo(RepoRoot);
            Assert.AreEqual(34, seedOutp.Rows.Count, "种子 34 行");

            const string extraRow =
                ",{\"sign_id\":\"sign_story002_extra\",\"display_词\":[\"额外粗档\",\"额外中档\",\"额外细档\"]," +
                "\"channel\":\"面色\",\"reveal_by\":[\"视诊\"],\"tier_named\":1," +
                "\"polarity\":\"阳性体征\",\"neg_weight\":null}";
            int lastBracket = seed.LastIndexOf(']');   // 字符重载(StringComparison 无 char 重载)
            Assert.Greater(lastBracket, 0, "前置:种子须含 signs 数组闭括号(替换面存在)");
            // 切点上下文(QA MINOR-8):']' 之后须只剩根对象闭括号 —— 防词条含 ']' 或根级
            // 后加数组时 LastIndexOf 插错位(静默把行插进别的数组)。
            Assert.IsTrue(
                seed.Substring(lastBracket + 1).TrimStart().StartsWith("}", StringComparison.Ordinal),
                "切点须是 signs 数组闭括号(其后只剩根对象闭括号)—— 插入位上下文自证");

            string extended = seed.Insert(lastBracket, extraRow);

            byte[] cooked = DiagnosisSignBinderProbe.Bake(extended);
            List<SignLexemeRow> extendedRows = DiagnosisSignCookedCodec.Read(cooked).Rows;
            Assert.AreEqual(35, extendedRows.Count, "扩展种子须烘出 35 行(加行零代码改动)");

            List<string> linesAfter = BuildConstantLines();
            Assert.AreEqual(string.Join("\n", linesBefore), string.Join("\n", linesAfter),
                "AC-8-35:加一条体征前后,常量表哈希须逐位不变(加行只新增逐条字段)");
            Assert.AreEqual(Fnv1aHex(linesBefore), Fnv1aHex(linesAfter), "哈希值本身须相等");
        }

        [Test]
        public void test_ac835_sensitivity_syntheticEntryReds()
        {
            // 敏感性:常量行集多一条 ⇒ 哈希必变(证哈希函数对内容敏感 —— 金标不是永真比较)。
            List<string> lines = BuildConstantLines();
            Assert.That(lines, Is.Not.Empty, "常量扫描面自证");

            string baseHash = Fnv1aHex(lines);
            var withExtra = new List<string>(lines)
            {
                "DaYiJingCheng.Tests.DiagnosisSystem.SyntheticProbe=42;",
            };
            Assert.AreNotEqual(baseHash, Fnv1aHex(withExtra),
                "多一条常量行 ⇒ 哈希必须变(否则金标比的是别的东西)");
        }

        // ═══════════════════════════════════════════════════════════
        //  烘焙确定性(ADR-014 §二;中文词表 + 枚举序数入字节)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_bakeDeterminism_doubleRun_byteEqual()
        {
            // ⚠️ 范围:**同进程**内两次烘焙逐位一致(写方固定字段序、零字典迭代、零本地时间)。
            //    跨会话逐位一致 NOT-RUN(承 interaction story-007 同款登记口径,见头注)。
            byte[] a = DiagnosisSignBaker.BakeFromRepo(RepoRoot).Cooked;
            byte[] b = DiagnosisSignBaker.BakeFromRepo(RepoRoot).Cooked;
            CollectionAssert.AreEqual(a, b, "同源两次烘焙(同进程)须逐位一致(ADR-014 §二)");
        }

        // ═══════════════════════════════════════════════════════════
        //  helpers:AC-8-35 常量哈希 —— **单一真源** = DiagnosisGoldenScan
        //  (story-002 建立;story-003 抽为共享件,两测试文件共用同一扫描器)
        // ═══════════════════════════════════════════════════════════

        private static List<string> BuildConstantLines() => DiagnosisGoldenScan.BuildConstantLines();

        private static string Fnv1aHex(IEnumerable<string> lines) => DiagnosisGoldenScan.Fnv1aHex(lines);
    }
}
