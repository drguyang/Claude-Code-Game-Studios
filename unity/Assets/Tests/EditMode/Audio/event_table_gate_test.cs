// 权威来源:production/epics/audio-system/story-002-event-table-schema-gate.md(QA Test Cases + AC)
//   · AC-44-09(BLOCKING)—— 白名单双查 + **三重负向**(Sting 类别 / 类别合法 + VitalsCrossed /
//     空表 · 缺必需 cue)+ 配对混搭 + NOT-RUN 守卫(字段缺失 ⇒ 报错退出,与「检查通过」可区分)
//   · AC-44-D9 —— 每行 whitelist_category + trigger_source + schema_version(缺一 ⇒ 构建失败)
//   · schema 校验规则 1–9(design/gdd/audio-system.md §Event Table Schema :531-548)
// TR-audio-002(白名单断言 + 双负向)· TR-audio-010(事件表走 ADR-014 烘焙,零 JSON 解析器)
// ADR-014 §三(两阶段;本测试驱动的是阶段 2 的校验纯函数)· ADR-018 §六(白名单机械化)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/unit/audio_system/event_table_gate_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**(与 item-database / story-001 同一先例);
//    账本侧由 tests/unit/audio_system/README.md 互链。
// ⚠️ 夹具落位:tests/unit/audio_system/fixtures/*.json(承 item-database 夹具先例)——
//    负向夹具住仓库根,**不进 unity/Assets 树**(不被 Unity 导入)。
// ⚠️ 读法纪律:玩家构建零 JSON 解析器 ⇒ **测试侧同样手写抽取**(结构化扫描 + 引号/括号配对),
//    **不引 Newtonsoft**、不用 JsonTextReader(承 quality_timeline_stacking_test 手写读取先例)。
// ⚠️ 正例 = 合法夹具 + **仓库真种子** assets/data/audio_events.json(规则 7 对真表生效)。
// ⚠️ 纪律:test_* 命名 · arrange/act/assert · 无随机 / 无时间依赖 · 零外部网络 I/O;
//    夹具 / 种子读取前置 File.Exists 断言(缺失即红,不静默跳过)。
// ⚠️ QA 判据(2026-09-26 顾问改判):规则 8 场景由「trigger_phase = 0.95×全周期」改为
//    **trigger_phase = 0.5(相对吸气段分数,出窗 [0.8,1.0])** —— 旧场景在分数编码下不可判。
// ⚠️ 编码防线(四件之 ②③):本文件附带 InspirePhaseMapping 的映射测试(分数 → 绝对相位唯一收口);
//    单位钉死见种子 _note(吸气段时长分数,0 = 吸气起点)。
// ⚠️ 范围:本门**不查 assets[] 素材存在性(NOT-RUN,归 Story 010 D6)** —— 正例只证 schema 侧。

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Presentation.Audio;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    /// <summary>Story 002 音频事件表 schema 与白名单门(AC-44-09 / AC-44-D9 / 规则 1–9)的 EditMode 测试。</summary>
    [TestFixture]
    internal sealed class EventTableGateTest
    {
        // ══════════════ 正例 ══════════════

        /// <summary>AC-44-09 正例 + 规则 1–9 全绿:合法夹具跑总门 ⇒ 零错误。</summary>
        [Test]
        public void test_validMinimalTable_zeroErrors()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors, Is.Empty,
                "合法最小表必须全绿,否则后续负向夹具的红失去归因:\n" + string.Join("\n", errors));
            Assert.That(table.Rows, Has.Count.EqualTo(5), "夹具自证:四支必需 cue + 一支乐层行");
        }

        /// <summary>仓库真种子过门(规则 7 对 <c>assets/data/audio_events.json</c> 生效:空表 / 缺必需 cue 都会在这里红)。</summary>
        [Test]
        public void test_realSeedAudioEvents_zeroErrors()
        {
            // Arrange
            string path = Path.Combine(repoRoot(), "assets", "data", "audio_events.json");
            Assert.That(File.Exists(path), Is.True, $"真种子缺失(Story 002 交付物):{path}");

            // Act
            AudioEventTable table = ParseTable(File.ReadAllText(path));
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors, Is.Empty,
                "assets/data/audio_events.json 必须过门:\n" + string.Join("\n", errors));
        }

        // ══════════════ AC-44-09 三重负向 ①:Sting 类别 ══════════════

        /// <summary>负向 ①(规则 1):注入 <c>whitelist_category: Sting</c> 的行 ⇒ 失败,且错误恰 1 条(单因隔离)。</summary>
        [Test]
        public void test_stingCategory_reportsRed()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("invalid_sting_category.json"));
            Assert.That(Row(table, "ui_sting_levelup").WhitelistCategory, Is.EqualTo("Sting"),
                "夹具自证:注入行类别 = Sting");

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("Sting"));
            Assert.That(errors[0], Does.Contain("规则 1"));
        }

        // ══════════════ AC-44-09 三重负向 ②:类别合法 + 禁止时机 ══════════════

        /// <summary>负向 ②(规则 2):类别合法(PlayerAction)但 <c>trigger_source: VitalsCrossed</c> ⇒ 同样失败。
        /// 堵「阈值时刻的咳嗽」的那一半。</summary>
        [Test]
        public void test_legalCategoryForbiddenTrigger_reportsRed()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("invalid_trigger_vitalscrossed.json"));
            AudioEventTableRow row = Row(table, "ui_page_turn");
            Assert.That(row.WhitelistCategory, Is.EqualTo("PlayerAction"), "夹具自证:类别合法");
            Assert.That(row.TriggerSource, Is.EqualTo("VitalsCrossed"), "夹具自证:时机违禁");

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("VitalsCrossed"));
            Assert.That(errors[0], Does.Contain("禁止集"));
        }

        // ══════════════ AC-44-09 三重负向 ③a:空表 ══════════════

        /// <summary>第三负向 a(规则 7):<c>rows = []</c>(0 行)⇒ 失败 —— 堵「0 行全绿」假象。</summary>
        [Test]
        public void test_emptyTable_reportsRed()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("empty_table.json"));
            Assert.That(table.Rows, Is.Empty, "夹具自证:0 行");

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("空表"));
            Assert.That(errors[0], Does.Contain("规则7"));
        }

        /// <summary>第三负向 b(规则 7):删除任一**必需 cue** 行 ⇒ 失败(此处删语声族)。</summary>
        [Test]
        public void test_missingRequiredCue_reportsRed()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            var kept = new List<AudioEventTableRow>();
            foreach (AudioEventTableRow row in table.Rows)
            {
                if (row.Cue != "Voice_Cough_Damp")
                    kept.Add(row);
            }

            table.Rows = kept;
            Assert.That(kept, Has.Count.EqualTo(4), "Arrange 自证:恰删一行");

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("Voice_Cough_Damp"));
            Assert.That(errors[0], Does.Contain("规则7"));
        }

        // ══════════════ AC-44-09 配对夹具(规则 6)═════════════

        /// <summary>规则 6:两侧各自 ∈ 合法集的混搭(<c>PlayerAction</c> × <c>ContinuousPhysiology</c>)⇒ 失败
        ///(查「谎报形态」的那一半 —— 申报非检测,措辞承 ADR-018 §六 / GDD 降调注)。</summary>
        [Test]
        public void test_crossPairCategoryTrigger_reportsRed()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("invalid_trigger_crosspair.json"));
            AudioEventTableRow row = Row(table, "ui_page_turn");
            Assert.That(
                AudioEventTableGates.WhitelistCategories, Does.Contain(row.WhitelistCategory),
                "夹具自证:类别 ∈ 五类(单查会放行)");
            Assert.That(
                AudioEventTableGates.LegalTriggerSources, Does.Contain(row.TriggerSource),
                "夹具自证:时机 ∈ 合法集(单查会放行)");

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert:配对表必须抓住「单查各自 ∈ 集合」放行的混搭
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("配对违例"));
            Assert.That(errors[0], Does.Contain("规则 6"));
        }

        /// <summary>规则 6 的乐层半边(QA Edge):<c>MusicLayer</c> 只能配 <c>EncounterMusicLayer</c> ——
        /// 配成 <c>PlayerAction</c>(两侧各自 ∈ 集合)必须被配对表抓住。</summary>
        [Test]
        public void test_musicLayerPairedWithPlayerAction_reportsRed()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            AudioEventTableRow music = Row(table, "Music_Encounter_Layer");
            music.TriggerSource = "PlayerAction";
            Assert.That(AudioEventTableGates.LegalTriggerSources, Does.Contain("PlayerAction"),
                "Arrange 自证:混搭的时机侧仍 ∈ 合法集(否则查的是规则 2,不是配对表)");

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("配对违例"));
            Assert.That(errors[0], Does.Contain("MusicLayer"));
        }

        // ══════════════ NOT-RUN 守卫(规则 3;「没检查」≠「检查通过」)═════════════

        /// <summary>字段缺失:删 <c>trigger_source</c> ⇒ **报错退出**(错误带 NOT-RUN 标记,可与通过区分)。</summary>
        [Test]
        public void test_missingTriggerSource_reportsNotRunGuard()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            Row(table, "Voice_Cough_Damp").TriggerSource = null;

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("NOT-RUN"));
            Assert.That(errors[0], Does.Contain("trigger_source"));
        }

        /// <summary>字段缺失:删 <c>whitelist_category</c> ⇒ NOT-RUN 报错(规则 1 不可静默过)。</summary>
        [Test]
        public void test_missingWhitelistCategory_reportsNotRunGuard()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            Row(table, "Voice_Cough_Damp").WhitelistCategory = null;

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("NOT-RUN"));
            Assert.That(errors[0], Does.Contain("whitelist_category"));
        }

        /// <summary>字段缺失:<c>rows</c> 整段缺失 ⇒ 每条规则各自 NOT-RUN(全表不可执行,不得放行)。</summary>
        [Test]
        public void test_rowsFieldMissing_reportsNotRunGuard()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            table.Rows = null;

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors, Is.Not.Empty);
            foreach (string error in errors)
                Assert.That(error, Does.Contain("NOT-RUN"), $"每条都须标 NOT-RUN:{error}");
        }

        // ══════════════ AC-44-D9:每行 schema_version ══════════════

        /// <summary>AC-44-D9:删行级 <c>schema_version</c> ⇒ 构建失败(NOT-RUN 守卫)。</summary>
        [Test]
        public void test_missingRowSchemaVersion_reportsRed()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            Row(table, "Voice_Cough_Damp").SchemaVersionRaw = null;

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("schema_version"));
            Assert.That(errors[0], Does.Contain("AC-44-D9"));
        }

        /// <summary>AC-44-D9:行级 <c>schema_version</c> 非整数字面量 ⇒ 类型拒收。</summary>
        [Test]
        public void test_rowSchemaVersionNonInteger_reportsRed()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            Row(table, "Voice_Cough_Damp").SchemaVersionRaw = "\"1\"";

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("非整数"));
        }

        /// <summary>顶层 <c>schema_version</c> 缺失 ⇒ NOT-RUN(ADR-014 §三 数据 schema 版本化)。</summary>
        [Test]
        public void test_missingTopLevelSchemaVersion_reportsNotRunGuard()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            table.SchemaVersionRaw = null;

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("NOT-RUN"));
            Assert.That(errors[0], Does.Contain("顶层 schema_version"));
        }

        /// <summary>AC-44-D9 三连第三击:行级 <c>schema_version</c> ≠ 顶层 ⇒ 拒(不匹配)。</summary>
        [Test]
        public void test_rowSchemaVersionMismatchedWithTopLevel_reportsRed()
        {
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            Assert.That(table.SchemaVersionRaw, Is.EqualTo("1"), "夹具自证:顶层 = 1");
            Row(table, "Voice_Cough_Damp").SchemaVersionRaw = "2";

            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("≠ 顶层"));
            Assert.That(errors[0], Does.Contain("不匹配"));
        }

        // ══════════════ 三层白名单:未知键 = 硬失败(ADR-014 §三)═════════════

        /// <summary>顶层未知键(拼写错 / 未登记键)⇒ 构建失败,不得静默丢字段。</summary>
        [Test]
        public void test_unknownTopLevelKey_reportsRed()
        {
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            table.PresentKeys = new List<string>(table.PresentKeys) { "tier_map_typo" };

            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("tier_map_typo"));
            Assert.That(errors[0], Does.Contain("未知键"));
        }

        /// <summary>行级未知键 ⇒ 构建失败(新增行键须先改 <c>AudioEventTableGates.RowKeys</c>)。</summary>
        [Test]
        public void test_unknownRowKey_reportsRed()
        {
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            AudioEventTableRow row = Row(table, "Voice_Cough_Damp");
            row.PresentKeys = new List<string>(row.PresentKeys) { "gain" };

            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("未知行键"));
            Assert.That(errors[0], Does.Contain("gain"));
        }

        /// <summary>键集输入缺失 ⇒ NOT-RUN(「TryGet false → continue」是假绿形态,必须加错误)。</summary>
        [Test]
        public void test_presentKeysMissing_reportsNotRunGuard()
        {
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            table.PresentKeys = null;

            IReadOnlyList<string> errors = AudioEventTableGates.ValidateKnownKeys(table);

            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("NOT-RUN"));
        }

        // ══════════════ 规则 5:tier_map 三键 + 六列集 / per-row 禁滤波列 ══════════════

        /// <summary>规则 5 下半:行级 <c>tier_params</c> 含滤波列 ⇒ 构建失败(F4=甲 一张表)。</summary>
        [Test]
        public void test_tierParamsFilterColumn_reportsRed()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("invalid_tiermap_filter_key.json"));
            IReadOnlyDictionary<string, string> tierParams = Row(table, "Voice_Cough_Damp").TierParams["1"];
            Assert.That(tierParams.ContainsKey("passband_width_hz"), Is.True,
                "夹具自证:行级 per-tier 覆盖携带滤波列");

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("含滤波列"),
                "须命中滤波列分支(而非档位键 ∉ {0,1,2} 分支 —— 那是扁平形状的错)");
            Assert.That(errors[0], Does.Contain("passband_width_hz"));
            Assert.That(errors[0], Does.Contain("规则 5"));
        }

        /// <summary>规则 5 上半:顶层 <c>tier_map</c> 缺失 ⇒ NOT-RUN(规则不可执行)。</summary>
        [Test]
        public void test_tierMapMissing_reportsNotRunGuard()
        {
            var table = new AudioEventTable { SchemaVersionRaw = "1", TierMap = null, Rows = Array.Empty<AudioEventTableRow>() };

            IReadOnlyList<string> errors = AudioEventTableGates.ValidateTierMap(table);

            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("NOT-RUN"));
            Assert.That(errors[0], Does.Contain("tier_map"));
        }

        /// <summary>规则 5:档位键集须恰 = {"0","1","2"} —— 缺档位键 ⇒ 拒。</summary>
        [Test]
        public void test_tierMapMissingTierKey_reportsRed()
        {
            var table = new AudioEventTable
            {
                SchemaVersionRaw = "1",
                TierMap = new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    ["0"] = FullTierColumns(),
                    ["2"] = FullTierColumns(),
                },
                Rows = Array.Empty<AudioEventTableRow>(),
            };

            IReadOnlyList<string> errors = AudioEventTableGates.ValidateTierMap(table);

            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("「1」"));
        }

        /// <summary>规则 5:每档列集须**恰** F-44.1 的 6 列 —— 缺列 ⇒ 拒。</summary>
        [Test]
        public void test_tierMapMissingColumn_reportsRed()
        {
            Dictionary<string, string> tier0 = FullTierColumns();
            tier0.Remove("signal_db");
            var table = new AudioEventTable
            {
                SchemaVersionRaw = "1",
                TierMap = new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    ["0"] = tier0,
                    ["1"] = FullTierColumns(),
                    ["2"] = FullTierColumns(),
                },
                Rows = Array.Empty<AudioEventTableRow>(),
            };

            IReadOnlyList<string> errors = AudioEventTableGates.ValidateTierMap(table);

            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("signal_db"));
        }

        /// <summary>规则 5:列集外的多余列(旧 4 列 / 任意 gain)⇒ 拒(防静默扩列)。</summary>
        [Test]
        public void test_tierMapUnknownColumn_reportsRed()
        {
            Dictionary<string, string> tier0 = FullTierColumns();
            tier0["gain"] = "1";
            var table = new AudioEventTable
            {
                SchemaVersionRaw = "1",
                TierMap = new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    ["0"] = tier0,
                    ["1"] = FullTierColumns(),
                    ["2"] = FullTierColumns(),
                },
                Rows = Array.Empty<AudioEventTableRow>(),
            };

            IReadOnlyList<string> errors = AudioEventTableGates.ValidateTierMap(table);

            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("gain"));
        }

        /// <summary>规则 5 正例:三档 × 6 列齐 ⇒ 过。</summary>
        [Test]
        public void test_tierMapSixColumnsAllTiers_accepted()
        {
            var table = new AudioEventTable
            {
                SchemaVersionRaw = "1",
                TierMap = new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    ["0"] = FullTierColumns(),
                    ["1"] = FullTierColumns(),
                    ["2"] = FullTierColumns(),
                },
                Rows = Array.Empty<AudioEventTableRow>(),
            };

            Assert.That(AudioEventTableGates.ValidateTierMap(table), Is.Empty);
        }

        // ══════════════ 规则 8:clock_ref 闭合 + 相位窗 ══════════════

        /// <summary>规则 8 / AC-44-14:<c>clock_ref</c> 悬空(表内无该 cue)⇒ 失败。</summary>
        [Test]
        public void test_danglingClockRef_reportsRed()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("invalid_clock_ref_dangling.json"));
            Assert.That(Row(table, "BreathLayer_Adventitious_Fine").ClockRef,
                Is.EqualTo("BreathLayer_Base_Missing"), "夹具自证:引用不存在的行");

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("悬空"));
            Assert.That(errors[0], Does.Contain("AC-44-14"));
        }

        /// <summary>规则 8:<c>clock_ref</c> 指向**非基础层**行(乐层行,类别不是持续生理声)⇒ 拒。</summary>
        [Test]
        public void test_clockRefTargetNotBaseLayer_reportsRed()
        {
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            Row(table, "BreathLayer_Adventitious_Fine").ClockRef = "Music_Encounter_Layer";

            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("不是基础气流层行"));
        }

        /// <summary>规则 8 NOT-RUN:附加音层行(cue 前缀判定器)删 <c>clock_ref</c> 与 <c>adventitious_policy</c>
        /// ⇒ 两条守卫错误(相位锁定与相位窗都不可执行,不得静默过)。</summary>
        [Test]
        public void test_adventitiousRowMissingClockRefAndPolicy_reportsNotRunGuard()
        {
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            AudioEventTableRow row = Row(table, "BreathLayer_Adventitious_Fine");
            row.ClockRef = null;
            row.Policy = null;

            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // 计数断言保留(恰两条),但**不锁顺序** —— 门按规则聚合,次序不是契约
            // (2026-09-26 复审 REC:原 errors[0]/errors[1] 与本文件 AnyError 纪律自相矛盾)。
            Assert.That(errors.Count, Is.EqualTo(2), string.Join("\n", errors));
            Assert.That(AnyError(errors, "clock_ref"), Is.True, string.Join("\n", errors));
            Assert.That(AnyError(errors, "adventitious_policy"), Is.True, string.Join("\n", errors));
            foreach (string error in errors)
                Assert.That(error, Does.Contain("NOT-RUN"));
        }

        /// <summary>规则 8(QA 场景,2026-09-26 改判):<c>trigger_phase = 0.5</c>
        /// (相对吸气段分数)落在窗 [0.8, 1.0] 之外 ⇒ 拒。</summary>
        [Test]
        public void test_triggerPhaseOutOfWindow_reportsRed()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("invalid_trigger_phase_out_of_window.json"));
            AudioEventTableRow row = Row(table, "BreathLayer_Adventitious_Fine");
            Assert.That(row.Policy.TriggerPhaseRaw, Is.EqualTo("0.5"), "夹具自证:相位 0.5(出窗)");

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("∉ [0.8, 1]"));
            Assert.That(errors[0], Does.Contain("规则 8"));
        }

        /// <summary>规则 8 防线:相位写成 **JSON 字符串**(旧 ×全周期 编码残留,如
        /// <c>"0.95×全周期"</c>)⇒ 类型 / 编码拒收,不得被当成 0.95 或截断成 0 放行。</summary>
        [Test]
        public void test_triggerPhaseNonNumericLiteral_reportsRed()
        {
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            Row(table, "BreathLayer_Adventitious_Fine").Policy.TriggerPhaseRaw = "\"0.95×全周期\"";

            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("0.95×全周期"));
            Assert.That(errors[0], Does.Contain("非数字字面量"));
            Assert.That(errors[0], Does.Contain("规则 8"));
        }

        /// <summary>规则 8:相位高于吸气段终点(&gt; 1 = 越出吸气段)⇒ 拒。</summary>
        [Test]
        public void test_triggerPhaseAboveInspireWindow_reportsRed()
        {
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            AudioEventTableRow row = Row(table, "BreathLayer_Adventitious_Fine");
            row.Policy.TriggerPhaseRaw = "1.05";
            row.Policy.JitterRaw = "0";

            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            Assert.That(errors, Is.Not.Empty, string.Join("\n", errors));
            bool rangeHit = false;
            foreach (string error in errors)
            {
                if (error.Contains("∉ [0.8, 1]"))
                    rangeHit = true;
            }

            Assert.That(rangeHit, Is.True, "须报相位越窗:\n" + string.Join("\n", errors));
        }

        /// <summary>规则 8 / GDD §Edge Cases:相位在窗内但 ±jitter 越出吸气段 ⇒ 拒(jitter 不得出吸气段)。</summary>
        [Test]
        public void test_jitterPushesWindowOutOfInspireSegment_reportsRed()
        {
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            AudioEventTableRow row = Row(table, "BreathLayer_Adventitious_Fine");
            row.Policy.TriggerPhaseRaw = "0.85";
            row.Policy.JitterRaw = "0.2"; // 窗 [0.65, 1.05] ⊄ [0,1]

            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("越出吸气段"));
        }

        /// <summary>规则 8 NOT-RUN:<c>adventitious_policy</c> 缺 <c>trigger_phase</c> ⇒ 守卫报错(非静默过)。</summary>
        [Test]
        public void test_missingTriggerPhase_reportsNotRunGuard()
        {
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            Row(table, "BreathLayer_Adventitious_Fine").Policy.TriggerPhaseRaw = null;

            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("NOT-RUN"));
            Assert.That(errors[0], Does.Contain("trigger_phase"));
        }

        // ══════════════ xfade_ms 存在性 / 类型(数值断言归 Story 012)═════════════

        /// <summary>乐层行缺 <c>xfade_ms</c> ⇒ 拒(存在性;<c>≥ MUSIC_XFADE_MIN_MS</c> 归 Story 012)。</summary>
        [Test]
        public void test_musicRowMissingXfade_reportsRed()
        {
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            Row(table, "Music_Encounter_Layer").XfadeRaw = null;

            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("xfade_ms"));
            Assert.That(errors[0], Does.Contain("乐层行"));
        }

        /// <summary>乐层行 <c>xfade_ms ≤ 0</c> ⇒ 拒(须为正 int)。</summary>
        [Test]
        public void test_musicRowNonPositiveXfade_reportsRed()
        {
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            Row(table, "Music_Encounter_Layer").XfadeRaw = "0";

            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("≤ 0"));
        }

        /// <summary>非乐层行的 <c>xfade_ms</c> 可选,但**在场须为整数** —— 1.5 ⇒ 类型拒。</summary>
        [Test]
        public void test_nonMusicRowNonIntegerXfade_reportsRed()
        {
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            Row(table, "Voice_Cough_Damp").XfadeRaw = "1.5";

            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("非整数"));
        }

        // ══════════════ subtitle_text:语声 / 口述族键集(AC-44-15 [A] 半)═════════════

        /// <summary>语声族 cue 缺 <c>subtitle_text</c> 字段 ⇒ 拒(NOT-RUN 守卫 + AC-44-15 [A])。</summary>
        [Test]
        public void test_subtitleMissingOnVoiceCue_reportsRed()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("invalid_subtitle_missing.json"));
            Assert.That(Row(table, "Voice_Cough_Damp").SubtitleText, Is.Null, "夹具自证:字段缺失");

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("subtitle_text"));
            Assert.That(errors[0], Does.Contain("AC-44-15"));
        }

        /// <summary>语声族 cue 的 <c>subtitle_text</c> 字段在但**为空** ⇒ 仍拒(键在值空不可呈现)。</summary>
        [Test]
        public void test_subtitleEmptyOnVoiceCue_reportsRed()
        {
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            Row(table, "Voice_Cough_Damp").SubtitleText = "";

            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("subtitle_text 为空"));
        }

        /// <summary>教学口述族(<c>Narration_*</c> 前缀)同样受 subtitle_text 键集覆盖(48 口述,AC-44-15 含 48)。</summary>
        [Test]
        public void test_narrationCueSubtitleCovered_reportsRedWhenMissing()
        {
            // Arrange:独立构造口述行(不污染合法夹具的必需 cue 集 —— 本测试只跑字幕判据)
            var missing = new AudioEventTableRow { Cue = "Narration_Tutorial_01" };

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.ValidateSubtitles(new[] { missing });

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("Narration_Tutorial_01"));
            Assert.That(errors[0], Does.Contain("NOT-RUN"), "字段缺失 = 守卫报错,不是「检查通过」");

            // 反向:带非空字幕的口述行 ⇒ 过
            missing.SubtitleText = "先按诊,再问诊。";
            Assert.That(AudioEventTableGates.ValidateSubtitles(new[] { missing }), Is.Empty);
        }

        /// <summary>非语声族 cue(乐层)不要求 <c>subtitle_text</c> —— 族前缀判据的反向边界。</summary>
        [Test]
        public void test_nonSubtitleCue_withoutSubtitle_accepted()
        {
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            Assert.That(Row(table, "Music_Encounter_Layer").SubtitleText, Is.Null, "乐层行本无字幕");
            Assert.That(AudioEventTableGates.IsSubtitleCue("Music_Encounter_Layer"), Is.False);
            Assert.That(AudioEventTableGates.IsSubtitleCue("Voice_Cough_Damp"), Is.True);
            Assert.That(AudioEventTableGates.IsSubtitleCue("Narration_Tutorial_01"), Is.True);

            Assert.That(AudioEventTableGates.ValidateSubtitles(table.Rows), Is.Empty);
        }

        // ═══════════════ 2026-09-26 审查补测(6 条 REC + qa 缺口)═══════════════

        /// <summary>规则 5 下半的**另一半**:扁平 <c>tier_params</c>(键 = 列名)⇒ 档位键 ∉
        /// {"0","1","2"} 拒收 —— 扁平残形绕开 GDD :487 嵌套形状的口子(与上一条嵌套夹具配对)。</summary>
        [Test]
        public void test_tierParamsFlatShape_reportsRed()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("invalid_tier_params_flat_shape.json"));
            Assert.That(Row(table, "Voice_Cough_Damp").TierParams.ContainsKey("passband_width_hz"), Is.True,
                "夹具自证:扁平形状(键 = 列名)");

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("档位键"));
            Assert.That(errors[0], Does.Contain("规则 5"));
        }

        /// <summary>顶层 <c>schema_version</c> 非整数 ⇒ 显式红(原静默放行 + 行级比较跳过 = 假绿)。</summary>
        [Test]
        public void test_topLevelSchemaVersionNonInteger_reportsRed()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            table.SchemaVersionRaw = "\"abc\"";

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("非整数"));
        }

        /// <summary>规则 8 第四入口:cue 改名(前缀 / Policy / ClockRef 三入口全不命中)但 <c>assets[]</c>
        /// 含附加音层素材族 ⇒ 仍判为附加音层行,字段缺失 NOT-RUN 不被跳过。
        /// <para>走**追加改名副本**而非原地改名:原地改名会连带触发规则 7(必需 cue 缺失),
        /// 把第四入口的判定和另一条规则搅在一起(2026-09-26 首版即因此假红 3 ≠ 2)。</para></summary>
        [Test]
        public void test_rule8_adventitiousAssetEntry_catchesRenamedCue()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            AudioEventTableRow src = Row(table, "BreathLayer_Adventitious_Fine");
            var renamed = new AudioEventTableRow
            {
                Cue = "FineCrackle",
                SchemaVersionRaw = src.SchemaVersionRaw,
                WhitelistCategory = src.WhitelistCategory,
                TriggerSource = src.TriggerSource,
                Bus = src.Bus,
                Loop = true,
                ClockRef = null,
                Policy = null,
                Assets = new List<string> { "sfx_breath_adventitious_fine.wav" },
                PresentKeys = src.PresentKeys,
            };
            table.Rows = new List<AudioEventTableRow>(table.Rows) { renamed };

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(2), string.Join("\n", errors));
            Assert.That(AnyError(errors, "clock_ref"), Is.True, string.Join("\n", errors));
            Assert.That(AnyError(errors, "adventitious_policy"), Is.True, string.Join("\n", errors));
            foreach (string error in errors)
                Assert.That(error, Does.Contain("NOT-RUN"), error);
        }

        /// <summary>规则 9(cue 唯一):同名 cue 双行 ⇒ 拒(<c>clock_ref</c> 首配歧义 + 必需集聚合掩盖)。
        /// <para>走**追加同名副本**而非改名:改名会连带触发规则 7(必需 cue 缺失),让断言分不清
        /// 是规则 9 还是别的规则在红(与上一条同一教训)。</para></summary>
        [Test]
        public void test_cueDuplicate_reportsRed()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            AudioEventTableRow src = Row(table, "Music_Encounter_Layer");
            table.Rows = new List<AudioEventTableRow>(table.Rows) { src };

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("重复"));
            Assert.That(errors[0], Does.Contain("规则 9"));
        }

        /// <summary>规则 8:附加音层行 <c>loop ≠ true</c> ⇒ 拒(QA 点名的零测分支)。</summary>
        [Test]
        public void test_rule8_loopFalse_reportsRed()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            Row(table, "BreathLayer_Adventitious_Fine").Loop = false;

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(AnyError(errors, "loop"), Is.True, string.Join("\n", errors));
        }

        /// <summary>禁止集大小写敏感性(QA 点名 Edge):小写 <c>vitalscrossed</c> 不落禁止集,
        /// 但**必然不 ∉ 合法集** ⇒ 仍红(规则 2 的另一臂)。</summary>
        [Test]
        public void test_forbiddenTriggerSet_caseSensitivity()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            Row(table, "Voice_Cough_Damp").TriggerSource = "vitalscrossed";

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("∉ 合法集"));
        }

        /// <summary>扫描器截断哨兵:结构不完整的 JSON → <c>PresentKeys</c> 注入 <c>__truncated__</c>
        /// 被三层白名单拒收(「非 null 但不全」原为静默漏判面)。</summary>
        [Test]
        public void test_truncatedParse_sabotagesWhitelist()
        {
            // Arrange
            AudioEventTable table = ParseTable("{ \"schema_version\": \"1\", \"rows\": [");

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.ValidateKnownKeys(table);

            // Assert
            Assert.That(errors, Is.Not.Empty, string.Join("\n", errors));
            Assert.That(AnyError(errors, "__truncated__"), Is.True, string.Join("\n", errors));
            Assert.That(AnyError(errors, "未知键"), Is.True, string.Join("\n", errors));
        }

        // ═══════════════ 2026-09-26 复审补测(零覆盖分支 + 行身份守卫)═══════════════

        /// <summary>行身份守卫:某行缺 <c>cue</c> ⇒ NOT-RUN(缺它则规则 7 / 9 与字幕族判据全部失效)。</summary>
        [Test]
        public void test_rowMissingCue_reportsNotRunGuard()
        {
            // Arrange:拿一条**不在必需清单内**的行置空 cue,避免串扰规则 7
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            Row(table, "Music_Encounter_Layer").Cue = null;

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("缺 cue"));
            Assert.That(errors[0], Does.Contain("规则3"));
        }

        /// <summary>行身份守卫:<c>rows</c> 里混入 <c>null</c> 行 ⇒ 必须报出**身份级**原因。
        /// <para>规则 1 / 2 / AC-44-D9 会因 <c>row == null || 缺字段</c> 合并条件各再报一条
        ///(措辞对 null 行是「缺 whitelist_category」),故此处**不断言错误总数** —— 只钉
        /// 身份级那条存在,免得守卫被删而计数恰好还对得上。</para></summary>
        [Test]
        public void test_rowNullElement_reportsNotRunGuard()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            table.Rows = new List<AudioEventTableRow>(table.Rows) { null };

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors, Is.Not.Empty, string.Join("\n", errors));
            Assert.That(AnyError(errors, "本体为 null"), Is.True, string.Join("\n", errors));
            Assert.That(AnyError(errors, "规则3"), Is.True, string.Join("\n", errors));
            foreach (string error in errors)
                Assert.That(error, Does.Contain("NOT-RUN"), error);
        }

        /// <summary>总门本体守卫:<c>Validate(null)</c> ⇒ 报错退出,不返回空表。</summary>
        [Test]
        public void test_validateNullTable_reportsNotRunGuard()
        {
            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(null);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("事件表本体缺失"));
            Assert.That(errors[0], Does.Contain("NOT-RUN"));
        }

        /// <summary>三层白名单第二层的 NOT-RUN:**行级** <c>PresentKeys</c> 缺失 ⇒ 报错
        ///(原测只置顶层,行级那条守卫是未验证代码)。</summary>
        [Test]
        public void test_rowPresentKeysMissing_reportsNotRunGuard()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            Row(table, "Voice_Cough_Damp").PresentKeys = null;

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("键集不可枚举"));
            Assert.That(errors[0], Does.Contain("行级未知键白名单"));
        }

        /// <summary>规则 5 下半 NOT-RUN:<c>tier_params["1"]</c> 缺对象体 ⇒ 报错(列集不可执行)。</summary>
        [Test]
        public void test_tierParamsTierBodyMissing_reportsNotRunGuard()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            Row(table, "Voice_Cough_Damp").TierParams =
                new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
                {
                    ["1"] = null,
                };

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("缺对象体"));
            Assert.That(errors[0], Does.Contain("NOT-RUN"));
        }

        /// <summary>规则 8:jitter 字段缺失 ⇒ NOT-RUN 守卫(相位抖动载体不可执行)。</summary>
        [Test]
        public void test_jitterMissing_reportsNotRunGuard()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            Row(table, "BreathLayer_Adventitious_Fine").Policy.JitterRaw = null;

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("缺 jitter"));
            Assert.That(errors[0], Does.Contain("NOT-RUN"));
        }

        /// <summary>规则 8:jitter 非数字字面量 ⇒ 拒。</summary>
        [Test]
        public void test_jitterNonNumeric_reportsRed()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            Row(table, "BreathLayer_Adventitious_Fine").Policy.JitterRaw = "fast";

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("不可解析为数字"));
            Assert.That(errors[0], Does.Contain("规则 8"));
        }

        /// <summary>规则 8:jitter &lt; 0 ⇒ 拒(抖动须非负)。</summary>
        [Test]
        public void test_jitterNegative_reportsRed()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            Row(table, "BreathLayer_Adventitious_Fine").Policy.JitterRaw = "-0.1";

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("抖动须为非负"));
        }

        /// <summary>规则 5 上半:顶层 <c>tier_map</c> 多出档位键 <c>"3"</c> ⇒ 拒(键集须恰 = 0/1/2)。</summary>
        [Test]
        public void test_tierMapExtraTierKey_reportsRed()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            var tiers = new Dictionary<string, IReadOnlyDictionary<string, string>>(
                (Dictionary<string, IReadOnlyDictionary<string, string>>)table.TierMap,
                StringComparer.Ordinal)
            {
                ["3"] = FullTierColumns(),
            };
            table.TierMap = tiers;

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("含档位键「3」"));
        }

        /// <summary>规则 5 上半:某档位键存在但值为 <c>null</c> ⇒ 拒(与「缺键」同一守卫)。</summary>
        [Test]
        public void test_tierMapNullTierValue_reportsRed()
        {
            // Arrange
            AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
            var tiers = new Dictionary<string, IReadOnlyDictionary<string, string>>(
                (Dictionary<string, IReadOnlyDictionary<string, string>>)table.TierMap,
                StringComparer.Ordinal)
            {
                ["1"] = null,
            };
            table.TierMap = tiers;

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.Validate(table);

            // Assert
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("缺档位键「1」"));
        }

        /// <summary>截断哨兵 · 复审 REC-1:**根未闭合但中间有 <c>}</c>** 的嵌套值
        /// —— <c>InnerOfObject</c> 按最后一个 <c>}</c> 截断,内层 <c>Balanced</c> 深度未归零
        /// ⇒ 必须注入 <c>__truncated__</c>(原实现静默吞掉 = 三层白名单欠扫)。</summary>
        [Test]
        public void test_truncatedNestedValue_reportsNotRunGuard()
        {
            // Arrange:根对象未闭合,唯一 `}` 在内层数组/对象中间
            AudioEventTable table = ParseTable("{\"a\": {\"x\": {\"y\": 1}, \"b\": 2");

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.ValidateKnownKeys(table);

            // Assert
            Assert.That(errors, Is.Not.Empty, string.Join("\n", errors));
            Assert.That(AnyError(errors, "__truncated__"), Is.True, string.Join("\n", errors));
        }

        // ══════════════ 配对表 / 闭集的代码侧自证 ══════════════

        /// <summary>配对表闭集正例:前四类同名自配 + 乐层配 EncounterMusicLayer ⇒ 放行;其余组合 ⇒ 拒。</summary>
        [Test]
        public void test_pairingTable_closedSetBoundary()
        {
            Assert.That(AudioEventTableGates.IsLegalCategoryTriggerPair("PlayerAction", "PlayerAction"), Is.True);
            Assert.That(AudioEventTableGates.IsLegalCategoryTriggerPair("PlayerExamAction", "PlayerExamAction"), Is.True);
            Assert.That(AudioEventTableGates.IsLegalCategoryTriggerPair("WorldPerceptible", "WorldPerceptible"), Is.True);
            Assert.That(
                AudioEventTableGates.IsLegalCategoryTriggerPair("ContinuousPhysiology", "ContinuousPhysiology"), Is.True);
            Assert.That(
                AudioEventTableGates.IsLegalCategoryTriggerPair("MusicLayer", "EncounterMusicLayer"), Is.True);

            Assert.That(AudioEventTableGates.IsLegalCategoryTriggerPair("MusicLayer", "MusicLayer"), Is.False,
                "内容侧字面量配到时机侧 = 混搭(Encounter 前缀缺失)");
            Assert.That(AudioEventTableGates.IsLegalCategoryTriggerPair("ContinuousPhysiology", "WorldPerceptible"), Is.False);
            Assert.That(AudioEventTableGates.IsLegalCategoryTriggerPair("PlayerAction", "EncounterMusicLayer"), Is.False);
        }

        /// <summary>闭集**定义在代码**:禁止集字面量必须由门拒收(数据侧无法自行扩集过 CI)。</summary>
        [Test]
        public void test_forbiddenTriggerSet_allRejected()
        {
            foreach (string forbidden in AudioEventTableGates.ForbiddenTriggerSources)
            {
                AudioEventTable table = ParseTable(readFixture("valid_minimal_table.json"));
                Row(table, "Voice_Cough_Damp").TriggerSource = forbidden;

                IReadOnlyList<string> errors = AudioEventTableGates.ValidateTriggerSources(table.Rows);
                Assert.That(errors, Is.Not.Empty, $"禁止集 {forbidden} 必须被拒");
                Assert.That(errors[0], Does.Contain(forbidden));
            }
        }

        // ══════════════ 编码防线 ②:分数 → 绝对相位的唯一映射 ══════════════

        /// <summary>编码防线 ②:窗内相位(0.95 × 吸气段分数)经唯一映射后必落
        /// <c>[0.8×F, F] × T</c>(F = INSPIRE_FRACTION;此处 F = 1/3 ⇒ [0.267, 0.333]T)——
        /// 证明门的分数窗与运行期换算同源,不依赖旋钮取值。</summary>
        [Test]
        public void test_inspirePhaseMapping_windowMapsInsideInspireSegment()
        {
            const double inspireFraction = 1.0 / 3.0; // 夹具值:静息 I:E ≈ 1:2(GDD :769)
            const double period = 1.0;                 // 夹具值:以 T = 1 归一,断言系数

            double offset = InspirePhaseMapping.ToAbsoluteOffsetSeconds(0.95, inspireFraction, period);

            Assert.That(offset, Is.GreaterThanOrEqualTo(0.8 * inspireFraction - 1e-9),
                "窗下界 0.8 映射后 = 0.8×F×T");
            Assert.That(offset, Is.LessThanOrEqualTo(inspireFraction + 1e-9),
                "窗上界 1.0 映射后 = F×T(吸气终点,不得越出)");
            Assert.That(offset, Is.InRange(0.2666, 0.3334),
                "F = 1/3 时 offset ∈ [0.267, 0.333]T(顾问复核判据)");
        }

        /// <summary>编码防线 ② 反向:出窗相位(0.5)映射后落在吸气段前半 —— 与门的红同源
        /// (门拒的正是「吸气中段触发」这种医学错位)。</summary>
        [Test]
        public void test_inspirePhaseMapping_outOfWindowPhaseBeforeLateInspireWindow()
        {
            const double inspireFraction = 1.0 / 3.0;
            double outOfWindow = InspirePhaseMapping.ToAbsoluteOffsetSeconds(0.5, inspireFraction, 1.0);

            Assert.That(outOfWindow, Is.LessThan(0.8 * inspireFraction),
                "0.5 × 吸气段 落在吸气前半 —— 不在吸气末 20% 窗内(与夹具红一致)");
        }

        /// <summary>编码防线 ② 的旋钮无关性:调 INSPIRE_FRACTION 不改变分数语义 ——
        /// 窗内分数恒映射到吸气段内的同一相对位置(存全周期分数则此性质不成立 = 假红源)。</summary>
        [Test]
        public void test_inspirePhaseMapping_knobChangeKeepsRelativePosition()
        {
            const double phase = 0.9;
            foreach (double f in new[] { 0.25, 1.0 / 3.0, 0.4 })
            {
                double offset = InspirePhaseMapping.ToAbsoluteOffsetSeconds(phase, f, 1.0);
                double relative = offset / f; // 折回吸气段内相对位置
                Assert.That(relative, Is.EqualTo(phase).Within(1e-9),
                    $"F = {f} 改变只缩放吸气段长度,分数语义不变");
                Assert.That(offset, Is.LessThanOrEqualTo(f + 1e-9), "仍不得越出吸气段");
            }
        }

        // ══════════════ 夹具 / 种子读取与手写 JSON 抽取 ══════════════

        /// <summary>读负向 / 正例夹具(tests/unit/audio_system/fixtures/;前置 File.Exists,缺失即红)。</summary>
        private static string readFixture(string fileName)
        {
            string path = Path.Combine(repoRoot(), "tests", "unit", "audio_system", "fixtures", fileName);
            Assert.That(File.Exists(path), Is.True, $"夹具缺失(Story 002 QA 指定):{path}");
            return File.ReadAllText(path);
        }

        /// <summary>仓库根([CallerFilePath] 上溯五级;与 item-database / input-system 先例同式)。</summary>
        private static string repoRoot([CallerFilePath] string thisFile = "")
        {
            string dir = Path.GetDirectoryName(thisFile) ?? ".";
            return Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", ".."));
        }

        /// <summary>取 cue 行(不存在即断言失败 —— 防夹具改名后测试静默走空分支)。</summary>
        private static AudioEventTableRow Row(AudioEventTable table, string cue)
        {
            Assert.That(table.Rows, Is.Not.Null, "表缺 rows 段");
            foreach (AudioEventTableRow row in table.Rows)
            {
                if (row.Cue == cue)
                    return row;
            }

            Assert.Fail($"表中无 cue 行「{cue}」(夹具 / 种子结构漂移?)");
            return null;
        }

        /// <summary>错误集里是否含指定片段(不依赖执行次序 —— 门按规则聚合,顺序不作契约)。</summary>
        private static bool AnyError(IReadOnlyList<string> errors, string fragment)
        {
            for (int i = 0; i < errors.Count; i++)
            {
                if (errors[i] != null && errors[i].IndexOf(fragment, StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        /// <summary>F-44.1 定型 6 列的合法一档(列名与 AudioEventTableGates.TierMapColumns 一致)。</summary>
        private static Dictionary<string, string> FullTierColumns()
            => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["passband_center_hz"] = "900",
                ["passband_width_hz"] = "900",
                ["noise_floor_db"] = "-42",
                ["contact_noise_floor_db"] = "-34",
                ["band_detail_count"] = "5",
                ["signal_db"] = "-18",
            };

        // ── 手写 JSON 抽取(零 Newtonsoft:引号 / 括号配对扫描;玩家构建零解析器纪律的测试侧对应)──

        /// <summary>整表文本 → 绑定前结构(顶层 schema_version / tier_map / rows / 全部键集)。</summary>
        private static AudioEventTable ParseTable(string json)
        {
            Dictionary<string, string> root = ReadMembers(InnerOfObject(json));
            var table = new AudioEventTable
            {
                SchemaVersionRaw = Get(root, "schema_version"),
                PresentKeys = new List<string>(root.Keys),
            };

            if (root.TryGetValue("tier_map", out string tierMapRaw) && tierMapRaw != null)
            {
                var tiers = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> tier in ReadMembers(InnerOfObject(tierMapRaw)))
                    tiers[tier.Key] = ReadMembers(InnerOfObject(tier.Value));
                table.TierMap = tiers;
            }

            if (root.TryGetValue("rows", out string rowsRaw) && rowsRaw != null)
            {
                var rows = new List<AudioEventTableRow>();
                foreach (string element in SplitArrayElements(rowsRaw))
                    rows.Add(ParseRow(element));
                table.Rows = rows;
            }

            return table;
        }

        /// <summary>单个行对象原文 → <see cref="AudioEventTableRow"/>(字段缺失保持 null,供 NOT-RUN 守卫区分)。</summary>
        private static AudioEventTableRow ParseRow(string objectRaw)
        {
            Dictionary<string, string> m = ReadMembers(InnerOfObject(objectRaw));
            var row = new AudioEventTableRow
            {
                Cue = Str(m, "cue"),
                SchemaVersionRaw = Get(m, "schema_version"),
                WhitelistCategory = Str(m, "whitelist_category"),
                TriggerSource = Str(m, "trigger_source"),
                Bus = Str(m, "bus"),
                ClockRef = Str(m, "clock_ref"),
                SubtitleText = m.ContainsKey("subtitle_text") ? Str(m, "subtitle_text") : null,
                XfadeRaw = Get(m, "xfade_ms"),
                PresentKeys = new List<string>(m.Keys),
            };

            if (m.TryGetValue("loop", out string loopRaw))
                row.Loop = bool.TryParse(loopRaw, out bool loop) && loop;

            if (m.TryGetValue("assets", out string assetsRaw))
            {
                var assets = new List<string>();
                foreach (string element in SplitArrayElements(assetsRaw))
                    assets.Add(Unquote(element));
                row.Assets = assets;
            }

            if (m.TryGetValue("tier_params", out string tierParamsRaw) && tierParamsRaw != null)
            {
                var tierParams = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> tier in ReadMembers(InnerOfObject(tierParamsRaw)))
                {
                    string raw = tier.Value ?? string.Empty;
                    // GDD :487 形状 = per-tier 嵌套;扁平残形(值非对象)以**自键占位**保留档位键,
                    // 交由门的「档位键 ∉ {0,1,2}」拒收。若这里仍 ReadMembers(标量),会把标量当
                    // 结构截断注入 __truncated__ —— 夹具的红就变成由解析器哨兵引起的(耦合隐患,
                    // 2026-09-26 复审 REC)。
                    tierParams[tier.Key] = raw.TrimStart().StartsWith("{", StringComparison.Ordinal)
                        ? ReadMembers(InnerOfObject(raw))
                        : new Dictionary<string, string>(StringComparer.Ordinal) { [tier.Key] = raw };
                }
                row.TierParams = tierParams;
            }

            if (m.TryGetValue("adventitious_policy", out string policyRaw))
            {
                Dictionary<string, string> policy = ReadMembers(InnerOfObject(policyRaw));
                row.Policy = new AdventitiousPolicyRaw
                {
                    TriggerPhaseRaw = Get(policy, "trigger_phase"),
                    JitterRaw = Get(policy, "jitter"),
                };
            }

            return row;
        }

        /// <summary>取对象正文(剥最外层花括号;无花括号时原样返回,交给后续扫描报错)。</summary>
        private static string InnerOfObject(string text)
        {
            int open = text.IndexOf('{');
            int close = text.LastIndexOf('}');
            if (open < 0 || close <= open)
                return text;
            return text.Substring(open + 1, close - open - 1);
        }

        /// <summary>扫描对象正文的 <c>key: value</c> 成员(value = 对象 / 数组 / 字符串 / 标量原文,不解析)。</summary>
        private static Dictionary<string, string> ReadMembers(string inner)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            int i = 0;
            bool truncated = false;
            while (i < inner.Length)
            {
                SkipWs(inner, ref i);
                if (i >= inner.Length || inner[i] == '}')
                    break;
                if (inner[i] != '"')
                {
                    truncated = true; // 结构异常(既非成员起点也非边界)⇒ 截断
                    break;
                }

                string key = Unquote(ReadQuoted(inner, ref i, out bool keyClosed));
                if (!keyClosed)
                {
                    truncated = true; // 引号未闭合 ⇒ 截断
                    break;
                }
                SkipWs(inner, ref i);
                if (i >= inner.Length || inner[i] != ':')
                {
                    truncated = true; // 键后无 ':' ⇒ 截断
                    break;
                }
                i++;
                SkipWs(inner, ref i);
                map[key] = ReadValue(inner, ref i, out bool valueTruncated);
                if (valueTruncated)
                    truncated = true; // 值(嵌套对象 / 数组)未闭合 ⇒ 截断 —— 见 REC-1

                SkipWs(inner, ref i);
                if (i < inner.Length && inner[i] == ',')
                    i++;
            }

            // 2026-09-26 审查 REC:结构截断 ⇒ 注入哨兵键,让三层白名单拒收 ——
            // 否则断点后的未知键永不进 PresentKeys =「非 null 但不全」的静默漏判面。
            // ⚠️ 只在**显式结构异常 break** 时注入:自然耗尽(i >= length)是正常结束,
            // 原稿用 `i >= inner.Length || inner[i] != '}'` 会对每次正常解析都误注入。
            if (truncated)
                map["__truncated__"] = "1";

            return map;
        }

        /// <summary>读一个值:平衡的 <c>{…}</c> / <c>[…]</c>、带引号字符串,或直到分隔符的标量。
        /// <para><c>truncated</c> = 该值**未被正确闭合**(括号深度未归零 / 引号未闭合)——
        /// 由调用方并入自己的截断标志(2026-09-26 审查 REC-1:原实现静默吞掉)。</para></summary>
        private static string ReadValue(string s, ref int i, out bool truncated)
        {
            truncated = false;
            if (i >= s.Length)
            {
                truncated = true; // 值位置直接耗尽
                return string.Empty;
            }

            char c = s[i];
            if (c == '{' || c == '[')
            {
                char open = c;
                char close = c == '{' ? '}' : ']';
                return Balanced(s, ref i, open, close, out truncated);
            }

            if (c == '"')
            {
                int start = i;
                ReadQuoted(s, ref i, out bool closed);
                truncated = !closed;
                return s.Substring(start, i - start);
            }

            int scalarStart = i;
            while (i < s.Length && s[i] != ',' && s[i] != '}' && s[i] != ']' && !char.IsWhiteSpace(s[i]))
                i++;
            return s.Substring(scalarStart, i - scalarStart);
        }

        /// <summary>读平衡括号段(字符串内的括号忽略;返回含括号原文)。
        /// <para><c>truncated</c> = 走到串尾仍未闭合(深度未归零)。</para></summary>
        private static string Balanced(string s, ref int i, char open, char close, out bool truncated)
        {
            int start = i;
            int depth = 0;
            truncated = false;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '"')
                {
                    ReadQuoted(s, ref i, out bool closed);
                    if (!closed)
                    {
                        truncated = true;
                        return s.Substring(start);
                    }
                    continue;
                }

                if (c == open)
                    depth++;
                else if (c == close)
                {
                    depth--;
                    i++;
                    if (depth == 0)
                        return s.Substring(start, i - start);
                    continue;
                }

                i++;
            }

            truncated = true; // 深度未归零 ⇒ 结构截断(原实现静默返回余下 = 欠扫面)
            return s.Substring(start);
        }

        /// <summary>读带引号字符串;返回**含引号**原文,i 停在闭引号之后。
        /// <para><c>closed</c> = 找到闭引号(false = 串尾耗尽)。</para></summary>
        private static string ReadQuoted(string s, ref int i, out bool closed)
        {
            int start = i;
            i++; // 跳过开引号
            while (i < s.Length)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i += 2;
                    continue;
                }

                if (s[i] == '"')
                {
                    i++;
                    closed = true;
                    return s.Substring(start, i - start);
                }

                i++;
            }

            closed = false;
            return s.Substring(start);
        }

        /// <summary>拆数组元素原文(对象 / 数组 / 字符串 / 标量);入参须含最外层方括号。</summary>
        private static List<string> SplitArrayElements(string arrayRaw)
        {
            var list = new List<string>();
            int i = 0;
            SkipWs(arrayRaw, ref i);
            if (i >= arrayRaw.Length || arrayRaw[i] != '[')
                return list;
            i++;

            while (i < arrayRaw.Length)
            {
                SkipWs(arrayRaw, ref i);
                if (i >= arrayRaw.Length || arrayRaw[i] == ']')
                    break;

                // 数组本体已由调用方 ReadValue 的 Balanced 校验闭合(截断在那里上报),
                // 元素级不再重复上报。
                list.Add(ReadValue(arrayRaw, ref i, out _));
                SkipWs(arrayRaw, ref i);
                if (i < arrayRaw.Length && arrayRaw[i] == ',')
                    i++;
            }

            return list;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i]))
                i++;
        }

        /// <summary>取原文字段(缺失 ⇒ null,供 NOT-RUN 守卫区分「字段缺失」)。</summary>
        private static string Get(Dictionary<string, string> members, string key)
            => members.TryGetValue(key, out string value) ? value : null;

        /// <summary>取字符串字段(缺失或非字符串字面量 ⇒ null)。</summary>
        private static string Str(Dictionary<string, string> members, string key)
        {
            string raw = Get(members, key);
            return raw != null && raw.StartsWith("\"", StringComparison.Ordinal) ? Unquote(raw) : null;
        }

        /// <summary>去引号(仅剥最外层;转义序列按原文保留 —— 夹具文本不含转义)。</summary>
        private static string Unquote(string raw)
        {
            if (raw == null)
                return null;
            if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
                return raw.Substring(1, raw.Length - 2);
            return raw;
        }
    }
}
