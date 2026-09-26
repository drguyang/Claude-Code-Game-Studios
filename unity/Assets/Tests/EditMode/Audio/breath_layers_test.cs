// 权威来源:production/epics/audio-system/story-004-breath-layers-precision-tiers.md
//   · AC-44-01(数据级非静音,2026-09-26 换载体):① tier_map 三档六列齐 + 附加音层行存在且 loop:true
//     ② .mixer 无 tier 条件静音标志位(资产静态 + 脚本调用点两半)③ RMS 探针(可选 PlayMode,本文件不含)
//   · AC-44-08 [A]:adventitious_policy 存在 + 窗口 ⊆ 吸气段 + 咳嗽路径不 EndLoop
//   · AC-44-14:clock_ref 闭合(schema 半归 Story 002;本文件承接运行期同节拍半)
//   · AC-44-02 [L] 听测 → 不写自动化测试,证据见 production/qa/evidence/breath-layers-listen-evidence.md
// GDD:design/gdd/audio-system.md 规则四 · F-44.1 · AC-44-01(:988,2026-09-26 已修订)
// ADR-018 §四 · TR-audio-005
//
// ⚠️ 读法纪律:读仓库文件前置 File.Exists 断言(缺失即红,不静默跳过)。
// ⚠️ repoRoot = [CallerFilePath] 上溯 **5 层**(Audio→EditMode→Tests→Assets→unity→仓库根);
//    写 4 层会把 root 落在 unity/ 下 —— Story 003 踩过,一个 bug 拖红 9 条。
// ⚠️ 纪律:test_* 命名 · arrange/act/assert · 无随机 / 无时间依赖 · 零网络 I/O。
// ⚠️ 构造夹具:本文件的事件表正/负例**在测试内直接构造**(AudioEventTable 是纯数据 DTO,
//    零文件依赖 = 单测隔离;真种子与 JSON 夹具的读取测试住 event_table_gate_test(Story 002))。
// ⚠️ Q6(2026-09-26):loop 源上禁 PlayScheduled —— 本文件**不断言**调度;门控音量断言走 Q7 ramp
//    (进/出窗各 ≥ AudioTuning.RampSeconds,经 FilterRamp 纯函数可测点)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;
using UnityEditor;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    /// <summary>Story 004 听诊呼吸两层与精度档(AC-44-01 / 08[A] / 14 / 02[骨架])的 EditMode 测试。</summary>
    [TestFixture]
    internal sealed class BreathLayersTest
    {
        // ══════════ AC-44-01 ①:六列齐 + 附加音层行存在且 loop: true(正/负)══════════

        /// <summary>AC-44-01 ① 正例:三档六列齐 + 附加音层行在且 loop: true ⇒ 零错误。</summary>
        [Test]
        public void test_tierMapSixColumnsAndAdventitiousLoop_present_zeroErrors()
        {
            // Arrange
            AudioEventTable table = ValidTable();

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.ValidateAc4401DataLevel(table);

            // Assert
            Assert.That(errors, Is.Empty, () => string.Join("\n", errors));
        }

        /// <summary>AC-44-01 ① 负例:删某档一列(缺列)⇒ 红,错误点名缺列与规则 5。</summary>
        [Test]
        public void test_tierMapSixColumns_missingColumn_red()
        {
            // Arrange:档 1 删 signal_db(F-44.1 第 6 列)
            AudioEventTable table = ValidTable();
            ((Dictionary<string, string>)table.TierMap["1"]).Remove("signal_db");

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.ValidateAc4401DataLevel(table);

            // Assert
            Assert.That(AnyError(errors, "signal_db 缺失"), Is.True, () => string.Join("\n", errors));
            Assert.That(AnyError(errors, "规则5"), Is.True, () => string.Join("\n", errors));
        }

        /// <summary>AC-44-01 ① 负例:附加音层行缺失(无附加层 ⇒ 该档附加音层为零)⇒ 红。</summary>
        [Test]
        public void test_adventitiousRow_absent_red()
        {
            // Arrange:只剩基础层行
            AudioEventTable table = ValidTable();
            table.Rows = new List<AudioEventTableRow> { BaseLayerRow() };

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.ValidateAc4401DataLevel(table);

            // Assert
            Assert.That(AnyError(errors, "AC-44-01 ①"), Is.True, () => string.Join("\n", errors));
            Assert.That(AnyError(errors, "无任何附加音层行"), Is.True, () => string.Join("\n", errors));
        }

        /// <summary>AC-44-01 ① 负例:附加音层行 loop 缺失 / loop: false ⇒ 红
        /// (raw DTO 下两种形态同落 <c>Loop == false</c>,故合一条)。</summary>
        [Test]
        public void test_adventitiousRow_loopNotTrue_red()
        {
            // Arrange
            AudioEventTable table = ValidTable();
            foreach (AudioEventTableRow row in table.Rows)
                if (row.Cue == "BreathLayer_Adventitious_Fine")
                    row.Loop = false;

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.ValidateAc4401DataLevel(table);

            // Assert:规则 8 的 loop 判据(002 已实现,本 AC 引用)
            Assert.That(AnyError(errors, "loop ≠ true"), Is.True, () => string.Join("\n", errors));
            Assert.That(AnyError(errors, "规则 8"), Is.True, () => string.Join("\n", errors));
        }

        // ══════════ AC-44-01 ②:mixer 静音标志位 —— 资产静态半 ══════════

        /// <summary>AC-44-01 ② 资产静态正例:真 .mixer 无 m_Mute: 1 / 无 tier×mute 同现名 ⇒ 零错误。</summary>
        [Test]
        public void test_mixerNoTierMuteFlag_assetSide_zeroErrors()
        {
            // Arrange
            string yaml = ReadRepoFile("unity", "Assets", "Audio", "DaYiJingCheng.mixer");

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateNoTierMuteFlags(yaml);

            // Assert
            Assert.That(errors, Is.Empty, () => string.Join("\n", errors));
        }

        /// <summary>AC-44-01 ② 资产静态负例:插入 m_Mute: 1 的组 ⇒ 红,错误点名 AC。</summary>
        [Test]
        public void test_mixerNoTierMuteFlag_mutedGroup_red()
        {
            // Arrange:内联最小组文档(单因违例 = m_Mute: 1)
            const string yaml =
                "--- !u!243 &12345\n" +
                "AudioMixerGroupController:\n" +
                "  m_Name: Stethoscope\n" +
                "  m_Mute: 1\n";

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateNoTierMuteFlags(yaml);

            // Assert
            Assert.That(AnyError(errors, "m_Mute: 1"), Is.True, () => string.Join("\n", errors));
            Assert.That(AnyError(errors, "AC-44-01 ②"), Is.True, () => string.Join("\n", errors));
        }

        /// <summary>AC-44-01 ② 资产静态负例:组名 tier×mute 同现 ⇒ 红。</summary>
        [Test]
        public void test_mixerNoTierMuteFlag_tierMuteExposedName_red()
        {
            // Arrange:名字同现违例(m_Mute 本身为 0 —— 证明名字判据独立生效)
            const string yaml =
                "--- !u!243 &12345\n" +
                "AudioMixerGroupController:\n" +
                "  m_Name: tier_low_mute\n" +
                "  m_Mute: 0\n";

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateNoTierMuteFlags(yaml);

            // Assert
            Assert.That(AnyError(errors, "同现"), Is.True, () => string.Join("\n", errors));
            Assert.That(AnyError(errors, "AC-44-01 ②"), Is.True, () => string.Join("\n", errors));
        }

        /// <summary>AC-44-01 ② 空转守卫:真资产解析键数 &gt; 0;空输入 = NOT-RUN,不得以空集冒充绿。</summary>
        [Test]
        public void test_muteScan_resolvesAtLeastOneKey_notRunGuard()
        {
            // Arrange
            string realMixer = ReadRepoFile("unity", "Assets", "Audio", "DaYiJingCheng.mixer");

            // Act:真资产计数 + 空输入校验
            int realKeys = MixerTopologyGates.CountMuteScanKeys(realMixer);
            IReadOnlyList<string> emptyErrors = MixerTopologyGates.ValidateNoTierMuteFlags("");

            // Assert
            Assert.That(realKeys, Is.GreaterThan(0),
                "非空转守卫:真 .mixer 至少解析出一个可判定键(m_Mute 组 / 捕获值),0 = NOT-RUN 不是绿");
            Assert.That(AnyError(emptyErrors, "NOT-RUN"), Is.True,
                () => "空输入必须 NOT-RUN:\n" + string.Join("\n", emptyErrors));
            Assert.That(AnyError(emptyErrors, "AC-44-01 ②"), Is.True,
                () => string.Join("\n", emptyErrors));
        }

        // ══════════ AC-44-01 ②:mixer 静音路径 —— 脚本调用点半 ══════════

        /// <summary>AC-44-01 ② 脚本正例:源文本无 SetFloat(≤-80) / 无 .mute = true 赋值 ⇒ 零错误。</summary>
        [Test]
        public void test_mixerNoTierMuteFlag_scriptSide_zeroErrors()
        {
            // Arrange:干净源(注释里的违例不计;.mute 读取与 == 比较不命中)
            string[] lines =
            {
                "// 注释内:audioMixer.SetFloat(\"x\", -90f);",
                "audioMixer.SetFloat(\"tier_noise_floor_db\", -42f);",
                "snapshot.TransitionTo(0.05f);",
                "if (source.mute) { /* 读取,非赋值 */ }",
                "bool off = source.mute == false;",
            };

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateNoMuteCallSites("Clean.cs", lines);

            // Assert
            Assert.That(errors, Is.Empty, () => string.Join("\n", errors));
        }

        /// <summary>AC-44-01 ② 脚本负例:SetFloat(…, -90f)(≤ −80)⇒ 红,错误点名 AC。</summary>
        [Test]
        public void test_mixerNoTierMuteFlag_setFloatBelowFloor_red()
        {
            // Arrange
            string[] lines =
            {
                "audioMixer.SetFloat(\"tier_noise_floor_db\", -90f);",
            };

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateNoMuteCallSites("Mute.cs", lines);

            // Assert
            Assert.That(AnyError(errors, "SetFloat"), Is.True, () => string.Join("\n", errors));
            Assert.That(AnyError(errors, "AC-44-01 ②"), Is.True, () => string.Join("\n", errors));
        }

        /// <summary>B2(2026-09-26 双评审):<c>.mute = true</c> **赋值分支**负例 ——
        /// 该分支此前只有正例(读取行),静默失修不会红。</summary>
        [Test]
        public void test_mixerNoTierMuteFlag_trueAssignment_red()
        {
            // Arrange:赋值形态(读取 / == 比较已由正例覆盖)
            string[] lines = { "source.mute = true;" };

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateNoMuteCallSites("MuteAssign.cs", lines);

            // Assert
            Assert.That(AnyError(errors, ".mute = true"), Is.True, () => string.Join("\n", errors));
            Assert.That(AnyError(errors, "AC-44-01 ②"), Is.True, () => string.Join("\n", errors));
        }

        /// <summary>B3(2026-09-26 双评审):捕获值 ≤ −80 判据负例 —— 快照捕获 −90 ⇒ 红
        /// (第三条判据此前零测试)。</summary>
        [Test]
        public void test_mixerNoTierMuteFlag_captureValueBelowFloor_red()
        {
            // Arrange:快照 m_FloatValues 含 −90(单因违例)
            const string yaml =
                "--- !u!245 &2\n" +
                "AudioMixerSnapshotController:\n" +
                "  m_Name: Snap\n" +
                "  m_FloatValues: {1234: -90}\n";

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateNoTierMuteFlags(yaml);

            // Assert
            Assert.That(AnyError(errors, "-90"), Is.True, () => string.Join("\n", errors));
            Assert.That(AnyError(errors, "AC-44-01 ②"), Is.True, () => string.Join("\n", errors));
        }

        /// <summary>B3 阈值边界:duck 快照的 −6 **不红**(阈值 −80 刻意不取 −60 —— 防误伤,
        /// unity-specialist Q1 实测裁定)。</summary>
        [Test]
        public void test_mixerNoTierMuteFlag_duckCaptureValue_notFlagged()
        {
            // Arrange:−6 > −80 ⇒ 合法 duck 电平
            const string yaml =
                "--- !u!245 &2\n" +
                "AudioMixerSnapshotController:\n" +
                "  m_Name: Snap\n" +
                "  m_FloatValues: {1234: -6}\n";

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateNoTierMuteFlags(yaml);

            // Assert
            Assert.That(errors, Is.Empty, () => string.Join("\n", errors));
        }

        /// <summary>B1(2026-09-26 双评审,最重):脚本半**接线 + 真身面** —— 遍历**真实运行期
        /// 源树**逐文件跑 <c>ValidateNoMuteCallSites</c>,断言零违规。内联负例只测判据本身;
        /// 真源里写穿不红 = 该 AC 只有资产半在跑。
        /// <para><b>扫描面与排除理由</b>:五棵运行期装配树(Gameplay.Presentation = 44 的
        /// SetFloat 唯一写入面;Sim / Sim.Contracts / Gameplay.Input / Gameplay.UI = 其余出货
        /// 运行期装配,已 grep 核实零 SetFloat)。**排除** <c>Editor.Tools.*</c>(ADR-025 编辑器
        /// 装配不进构建,运行期零存在;且门自身的错误消息串含判据字面量,扫自己 = 恒红)与
        /// <c>Tests</c>(内联负例字符串是判据测试本体)。</para></summary>
        [Test]
        public void test_muteCallSites_realRuntimeTrees_zeroViolations()
        {
            // Arrange:五棵运行期源树(目录缺失即红,不静默跳过)
            string[] runtimeTrees =
            {
                "Gameplay.Presentation", "Gameplay.Input", "Gameplay.UI", "Sim", "Sim.Contracts",
            };

            // Act
            var violations = new List<string>();
            int scannedFiles = 0;
            foreach (string tree in runtimeTrees)
            {
                string dir = Path.Combine(repoRoot(), "unity", "Assets", tree);
                Assert.That(Directory.Exists(dir), Is.True, $"源树缺失:{dir}");
                foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    Assert.That(File.Exists(file), Is.True, $"遍历到不可读文件:{file}");
                    string[] lines = File.ReadAllText(file).Replace("\r\n", "\n").Split('\n');
                    IReadOnlyList<string> errors =
                        MixerTopologyGates.ValidateNoMuteCallSites(file, lines);
                    for (int i = 0; i < errors.Count; i++)
                        violations.Add(errors[i]);
                    scannedFiles++;
                }
            }

            // Assert
            Assert.That(scannedFiles, Is.GreaterThan(0), "0 文件 = 空转(源树结构漂移?)");
            Assert.That(violations, Is.Empty,
                () => "真源命中静音路径(BLOCKING AC-44-01 ②):\n" + string.Join("\n", violations));
        }

        /// <summary>exposed 配置守卫探针(**判据已于 2026-09-27 由 SetFloat 改为配置正确性 + 读路径可用**)。
        /// <para><b>H-B 确认(实验 1–7,2026-09-27)</b>:<c>AudioMixer.SetFloat</c> 在 EditMode 对
        /// **任何名字**都返回 false —— 包括明显不存在的 <c>___this_param_does_not_exist___</c>;
        /// <c>GetFloat</c> 对同一真实名字返回 <b>true</b>。⇒ <c>SetFloat</c> 的「按名字查表」
        /// native 路径在此环境本身不通(EditMode / 无活跃 DSP 图 / native 状态),
        /// **与 exposed 配置完全无关**。</para>
        /// <para><b>已证伪的假设(每一条当时都看似合理)</b>:① 合成 GUID <c>b500…</c> 对不上(已修成
        /// 组 <c>m_Volume</c> 哈希仍 false)② Unity 没保留条目(反射读到 7 条且一致)
        /// ③ <c>AddExposedParameter</c> 补救(cache 实测仍 0)④ <c>ResolveExposedParameterPath</c>
        /// 注入 cache(0→1 但 <c>SetFloat</c> 仍 false)。⇒ 不再继续猜。</para>
        /// <para><b>本探针判据</b>(两者均为实验已证实为真):反射读回 7 条 guid 与 YAML 写入
        /// **逐条一致**(配置正确性 —— 本次缺陷真正的修复面)+ <c>GetFloat</c> 返回 true
        /// (读路径可用,证明 name→guid→参数解析成功)。</para>
        /// <para>⚠️ <c>SetFloat</c> 在**运行期(播放态)仍有效** —— 本限制只适用于 EditMode 测试环境;
        /// <c>PlayerBusVolumeInitializer.ApplyDefaults</c> 的 7×<c>SetFloat</c> 在运行期正常。</para>
        /// <para><b>读法探测史</b>:① <c>AudioMixer.exposedParameters</c> 非公开(编译错证伪)
        /// ② <c>SerializedObject</c> 读 <c>guid</c> 为 <c>Generic</c>/GUID 结构非 string(读法未决)
        /// ③ <b>本版 = 反射读内部面</b>(Discover 实测 <c>AudioMixerController::get_exposedParameters()</c>)。</para>
        /// <para>⚠️ <b>返回类型 Knowledge Gap</b>:engine-reference <c>audio.md</c> 未记
        /// <c>SetFloat</c>/<c>GetFloat</c> 的返回类型 —— 长期稳定 API 面均为 <c>bool</c>,以实测为准。</para>
        /// <para>缺失即红:<c>.mixer</c> 由 MixerAssetGenerator batch 生成(承 003 同一预期红项)。</para></summary>
        [Test]
        public void test_busVolumeExposedConfig_matchesGroupVolumeHash_andGetFloatReadable()
        {
            // Arrange
            string absolutePath = Path.Combine(repoRoot(), "unity", "Assets", "Audio", "DaYiJingCheng.mixer");
            Assert.That(File.Exists(absolutePath), Is.True,
                $".mixer 资产缺失:{absolutePath}(先跑 MixerAssetGenerator.Create batch)");
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Audio/DaYiJingCheng.mixer");
            Assert.That(mixer, Is.Not.Null, "AssetDatabase 加载失败:Assets/Audio/DaYiJingCheng.mixer");

            // ── 反射读内部面(Discover 实测存在:AudioMixerController::get_exposedParameters()
            //    ⇒ ExposedAudioParameter[];**最后一次探测** —— 三条出口只走一条,不论哪条
            //    都不再提新修法;日志注明「反射读内部面」)──
            Dictionary<string, string> yamlWritten = ReadYamlExposedGuids(File.ReadAllText(absolutePath));

            var readBack = new List<KeyValuePair<string, string>>();   // name → guid 原值
            var guidTypes = new Dictionary<string, string>(StringComparer.Ordinal);
            bool reflectReached = false;   // 反射拿到数组并逐项 dump(读法层面走到头)

            Type mixerType = mixer.GetType();
            PropertyInfo exposedPi = mixerType.GetProperty(
                "exposedParameters",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Debug.Log($"[诊断·反射·读内部面] GetType() = {mixerType.FullName}; " +
                      "GetProperty(\"exposedParameters\") = " +
                      (exposedPi != null ? $"found({exposedPi.PropertyType.FullName})" : "null"));

            if (exposedPi != null)
            {
                object exposed = null;
                try
                {
                    exposed = exposedPi.GetValue(mixer);
                }
                catch (Exception ex)
                {
                    Debug.Log($"[诊断·反射·读内部面] GetValue 抛 {ex.GetType().Name}: {ex.Message}");
                }

                if (exposed is Array arr)
                {
                    reflectReached = true;
                    Type elemType = arr.GetType().GetElementType();
                    Debug.Log($"[诊断·反射·读内部面] 读到 {arr.Length} 条;元素类型 = {elemType?.FullName};" +
                              " 逐项 GetFields + GetProperties 全量 dump(不预设字段名)");
                    foreach (object elem in arr)
                    {
                        if (elem == null) continue;
                        var dump = new StringBuilder();
                        string nameVal = null;
                        string guidVal = null;

                        foreach (FieldInfo field in elem.GetType().GetFields(
                                     BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            object fv;
                            try { fv = field.GetValue(elem); }
                            catch (Exception ex) { fv = "(" + ex.GetType().Name + ")"; }
                            dump.Append(" | field ").Append(field.Name)
                                .Append('(').Append(field.FieldType.Name).Append(")=")
                                .Append(fv ?? "(null)");
                            nameVal = nameVal ?? PickMemberValue(field.Name, fv, "name");
                            guidVal = guidVal ?? PickMemberValue(field.Name, fv, "guid");
                        }

                        foreach (PropertyInfo pi in elem.GetType().GetProperties(
                                     BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            object pv;
                            try { pv = pi.GetValue(elem, null); }
                            catch (Exception ex) { pv = "(" + ex.GetType().Name + ")"; }
                            dump.Append(" | prop ").Append(pi.Name)
                                .Append('(').Append(pi.PropertyType.Name).Append(")=")
                                .Append(pv ?? "(null)");
                            nameVal = nameVal ?? PickMemberValue(pi.Name, pv, "name");
                            guidVal = guidVal ?? PickMemberValue(pi.Name, pv, "guid");
                        }

                        Debug.Log($"[诊断·反射·读内部面] element dump:{dump}");

                        string finalName = nameVal ?? "(name 未取到)";
                        string finalGuid = guidVal ?? "(guid 未取到)";
                        readBack.Add(new KeyValuePair<string, string>(finalName, finalGuid));
                        guidTypes[finalName] = "(反射读内部面)";
                    }
                }
                else
                {
                    Debug.Log("[诊断·反射·读内部面] 返回值非数组:" +
                              (exposed == null ? "null" : exposed.GetType().FullName) +
                              " —— [停手出口2]");
                }
            }
            else
            {
                Debug.Log("[诊断·反射·读内部面] 属性不存在 —— [停手出口2]");
            }

            Debug.Log($"[诊断] 反射读回 {readBack.Count} 条;YAML 写入 {yamlWritten.Count} 条" +
                      (reflectReached ? "(反射读内部面)" : "(反射未达 —— 见上,[停手出口2])"));
            bool allMatch = readBack.Count > 0 && readBack.Count == yamlWritten.Count;
            bool allGuidReadOk = readBack.Count > 0;
            foreach (KeyValuePair<string, string> pair in readBack)
            {
                yamlWritten.TryGetValue(pair.Key, out string written);
                guidTypes.TryGetValue(pair.Key, out string guidType);
                string typeLabel = string.IsNullOrEmpty(guidType) ? "(类型未知)" : guidType;
                bool readOk = IsPlainHex32(pair.Value);
                bool match = readOk && written != null &&
                             string.Equals(pair.Value, written, StringComparison.OrdinalIgnoreCase);
                if (!readOk) allGuidReadOk = false;
                if (!match) allMatch = false;
                Debug.Log($"[诊断] exposed: name={pair.Key} guid_type={typeLabel} guid_raw={pair.Value} | " +
                          $"YAML 写入 guid={(written ?? "(无)")}" +
                          (readOk
                              ? (match ? " | 一致" : " | 不一致")
                              : " | **原值未取到(读法问题,不据此判导入)**"));
            }

            // ── Act:先**采样** SetFloat / GetFloat 全部结果(后断言)——
            //    探针红时诊断日志仍完整(断言抛出即截断 = 日志残缺)
            var setResults = new Dictionary<string, bool>(StringComparer.Ordinal);
            var getResults = new Dictionary<string, bool>(StringComparer.Ordinal);
            var roundTrips = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (string param in MixerRegistry.BusVolumeParameters)
            {
                setResults[param] = mixer.SetFloat(param, 0f);
                getResults[param] = mixer.GetFloat(param, out float value);
                roundTrips[param] = value;
            }

            // ── 结论:读回事实 × SetFloat 事实 → **三条出口只走一条**(最后一次探测;
            //    不论哪条出口都不再提新修法);原文进日志供判读 ──
            bool allSetTrue = true;
            foreach (KeyValuePair<string, bool> kv in setResults)
                if (!kv.Value) allSetTrue = false;

            if (!reflectReached)
            {
                Debug.Log("[诊断结论·停手出口2] 反射读内部面失败(见 [诊断·反射·读内部面])——" +
                          " 公开面(SerializedObject)与内部面(反射)读法均尽,按裁定**停手**," +
                          " 不再提新修法;探针三证保持红");
            }
            else if (readBack.Count == 0)
            {
                Debug.Log("[诊断结论·停手出口2] 反射读到数组但 0 条有效 —— 按裁定**停手**," +
                          " 不猜结构,不再提新修法");
            }
            else if (!allGuidReadOk)
            {
                Debug.Log("[诊断结论·停手出口2] guid 原值仍未取到(guid_type / guid_raw 见上行)" +
                          " —— 读法未尽,**不据此判导入**;按裁定**停手**,不猜字段名");
            }
            else if (!allMatch)
            {
                Debug.Log("[诊断结论·停手出口3] 读回 guid 与 YAML 写入**真不一致**" +
                          "(原值已取到)—— 真实结构问题,按裁定**停手**,不提新修法");
            }
            else if (allSetTrue)
            {
                Debug.Log($"[诊断结论·已通] 读回 {readBack.Count} 条 == 写入值 且 7×SetFloat 全 true —— 通路成立");
            }
            else
            {
                Debug.Log("[诊断结论·停手出口1(= 原候选4)] 读回条目 == YAML 写入值,但 SetFloat 仍 false ⇒" +
                          " **公开面无法判定**:「配置缺一环」vs「EditMode 无活跃 DSP 图」不可区分;" +
                          " 对照判别不可行(.mixer 无 Unity 自产 exposed 参数)—— 按裁定**停手**," +
                          " 归 003 已知阻断缺陷,不再提新修法");
            }

            // ── KNOWN DEFECT Skip(2026-09-26 出口1 确认后用户裁定;**Skip ≠ 通过**)──
            //    缺陷在场(allSetTrue = false)⇒ Ignore:结果 = **Skipped**,理由随测试名可见;
            //    通路真修好(allSetTrue = true)⇒ Ignore **自动不触发**,直落三证断言恢复验收
            //    (与无条件 Ignore 的差异见 summary doc —— 无条件版修好后永远到不了断言 = 假验收死角)。
            // ── Assert(判据 = 配置正确性 + 读路径可用;H-B 确认后不再以 SetFloat 为判据)──
            // ① 反射读回 7 条 guid 与 YAML 写入逐条一致(配置正确性 = 本次缺陷真正的修复面)
            Assert.That(allMatch, Is.True,
                "exposed 配置错误:反射读回的 guid 与 YAML 写入不一致(见上行 [诊断] 逐条对照)");
            Assert.That(allGuidReadOk, Is.True,
                "反射未能取到 guid 原值(读法问题,不据此判导入 —— 见上行)");
            // ② 读路径可用:GetFloat 对真实名字返回 true(证明 name→guid→参数解析成功)
            foreach (string param in MixerRegistry.BusVolumeParameters)
                Assert.That(getResults[param], Is.True,
                    $"GetFloat(\"{param}\") ⇒ false —— 读路径不通:name→guid→参数解析失败(见 [诊断] 日志)");

            // ③ H-B 事实记录(**不作判据**):SetFloat 在 EditMode 对任何名字都 false(含明显不存在的
            //    名字),与 exposed 配置无关 ⇒ 运行期(播放态)仍有效,本限制只适用于 EditMode 测试环境
            Debug.Log($"[判据记录·H-B] 7×SetFloat 在 EditMode 全部返回 false;" +
                      $" 对照组 SetFloat(\"___this_param_does_not_exist___\") 亦 false;" +
                      $" GetFloat 对同一真实名字返回 true ⇒ SetFloat 的按名查表 native 路径在此环境不通。");
        }

        // ══════════ AC-44-08 [A]:相位窗 ⊆ 吸气段 + 咳嗽不 EndLoop ══════════

        /// <summary>AC-44-08 [A] 正例:trigger_phase ∈ [0.8,1.0] 且 ±jitter 不出吸气段 ⇒ 零错误。</summary>
        [Test]
        public void test_adventitiousPolicyWindow_withinInspireSegment_zeroErrors()
        {
            // Arrange:0.9 ± 0.05 ⊆ [0,1](相对吸气段分数)
            AudioEventTable table = ValidTable();

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.ValidateLoopClock(table.Rows);

            // Assert
            Assert.That(errors, Is.Empty, () => string.Join("\n", errors));
        }

        /// <summary>AC-44-08 [A] 负例:trigger_phase = 0.5(出窗)⇒ 红,错误点名规则 8。</summary>
        [Test]
        public void test_adventitiousPolicyWindow_outOfWindow_red()
        {
            // Arrange
            AudioEventTable table = ValidTable();
            foreach (AudioEventTableRow row in table.Rows)
                if (row.Cue == "BreathLayer_Adventitious_Fine")
                    row.Policy.TriggerPhaseRaw = "0.5";

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.ValidateLoopClock(table.Rows);

            // Assert
            Assert.That(AnyError(errors, "trigger_phase = 0.5"), Is.True, () => string.Join("\n", errors));
            Assert.That(AnyError(errors, "规则 8"), Is.True, () => string.Join("\n", errors));
        }

        /// <summary>AC-44-08 ③ 正例:咳嗽走一次性通道 —— 记录型 sink 上零次
        /// <c>EndLoop</c>(假 sink 行为测试,主判据承 unity-specialist Q5)。</summary>
        [Test]
        public void test_coughNeverEndsBreathLoop_zeroEndLoopCalls()
        {
            // Arrange:呼吸循环已在播(上游持句柄)
            var sink = new RecordingCueSink();
            sink.BeginLoop(new AudioCueDto(1, 0, default, 7, true));

            // Act:咳嗽经唯一入口派发
            CoughChannel.Dispatch(sink, new AudioCueDto(2, 0, default, 7, false));

            // Assert
            Assert.That(sink.EmitCount, Is.EqualTo(1), "咳嗽必须走 Emit 一次性通道");
            Assert.That(sink.EndedHandles, Is.Empty,
                "咳嗽路径零次 EndLoop —— 停呼吸循环 = 「啰音被咳掉了」,违 AC-44-08(BLOCKING)");
        }

        /// <summary>AC-44-08 ③ 对照面(判据空转守卫):咳嗽在场不计数,持有者收尾恰 2 次
        /// EndLoop(附加层 + 基础层)且句柄不串线。</summary>
        [Test]
        public void test_endBreathCycle_endsBothLayersExactlyOnce()
        {
            // Arrange:两层循环已起
            var sink = new RecordingCueSink();
            AudioCueHandle baseHandle = sink.BeginLoop(new AudioCueDto(1, 0, default, 7, true));
            AudioCueHandle advHandle = sink.BeginLoop(new AudioCueDto(3, 0, default, 7, true));

            // Act:咳嗽(应零影响)→ 持有者收尾两层
            CoughChannel.Dispatch(sink, new AudioCueDto(2, 0, default, 7, false));
            sink.EndLoop(baseHandle);
            sink.EndLoop(advHandle);

            // Assert
            Assert.That(sink.EndedHandles, Has.Count.EqualTo(2),
                "收尾恰 2 次(咳嗽不计入);多了 = 咳嗽串进循环,少了 = 漏收尾");
            Assert.That(sink.EndedHandles[0], Is.EqualTo(baseHandle.Id));
            Assert.That(sink.EndedHandles[1], Is.EqualTo(advHandle.Id),
                "句柄不串线(基础层 / 附加层各自对号)");
            Assert.That(baseHandle.Id, Is.Not.EqualTo(advHandle.Id), "夹具自证:两层句柄相异");
        }

        // ══════════ AC-44-14:两层同节拍 / 禁独立自由循环(运行期半)══════════

        /// <summary>AC-44-14 正例:附加层窗口只从基础层周期导出,且与
        /// <see cref="InspirePhaseMapping.ToAbsoluteOffsetSeconds"/> 单一乘法路径逐位一致
        /// (禁第二条乘法 = 编码防线 ① 消费方核对)。</summary>
        [Test]
        public void test_adventitiousWindow_derivedFromBasePeriod_singleMultiplyPath()
        {
            // Arrange
            const double triggerPhase = 0.9;
            const double jitter = 0.05;
            const double cycleSeconds = 3.0;
            const double cycleStartDsp = 100.0;
            double inspireFraction = AudioTuning.InspireFraction;

            // Act:门的调度窗
            BreathPhaseGate.AdventitiousWindowDsp(
                cycleStartDsp, triggerPhase, jitter, inspireFraction, cycleSeconds,
                out double windowStart, out double windowEnd);

            // 参照:唯一映射函数直算(任何旁路乘法都会在此对不上)
            double expectedCenter = InspirePhaseMapping.ToAbsoluteOffsetSeconds(
                triggerPhase, inspireFraction, cycleSeconds);
            double expectedJitter = InspirePhaseMapping.ToAbsoluteJitterSeconds(
                jitter, inspireFraction, cycleSeconds);

            // 周期分数窗(驱动门控用)也必须等于同一映射(周期取 1)
            BreathPhaseGate.WindowCycleFraction(triggerPhase, jitter, inspireFraction,
                out double startFraction, out double endFraction);

            // Assert
            Assert.That(windowStart,
                Is.EqualTo(cycleStartDsp + (expectedCenter - expectedJitter)).Within(1e-9),
                "窗口起点 = 周期起点 + (offset − jitter),与映射函数同式");
            Assert.That(windowEnd,
                Is.EqualTo(cycleStartDsp + (expectedCenter + expectedJitter)).Within(1e-9),
                "窗口终点同上");
            Assert.That(startFraction, Is.EqualTo(
                    InspirePhaseMapping.ToAbsoluteOffsetSeconds(triggerPhase, inspireFraction, 1.0) -
                    InspirePhaseMapping.ToAbsoluteJitterSeconds(jitter, inspireFraction, 1.0))
                .Within(1e-12), "周期分数窗零处自乘 INSPIRE_FRACTION(编码防线 ①)");
            Assert.That(endFraction, Is.LessThanOrEqualTo(inspireFraction + 1e-12),
                "窗终点 ⊆ 吸气段(周期分数域)");
        }

        /// <summary>AC-44-14 负例:基础层周期 T ≤ 0 ⇒ 窗口无效(拒绝出窗,不静默给合法秒)。</summary>
        [Test]
        public void test_adventitiousWindow_nonPositivePeriod_invalid()
        {
            // Arrange:映射函数对 T ≤ 0 的防线 = 显式 LogError + 返回 0(须先 Expect,否则测试失败)
            LogAssert.Expect(LogType.Error, new Regex("完整呼吸周期 T 须 > 0"));
            LogAssert.Expect(LogType.Error, new Regex("完整呼吸周期 T 须 > 0"));

            // Act
            BreathPhaseGate.AdventitiousWindowDsp(
                50.0, 0.9, 0.0, AudioTuning.InspireFraction, 0.0,
                out double windowStart, out double windowEnd);

            // Assert:窗口坍缩到周期起点(0 秒偏移),不产生越界秒值
            Assert.That(windowStart, Is.EqualTo(50.0).Within(1e-9));
            Assert.That(windowEnd, Is.EqualTo(50.0).Within(1e-9));
        }

        /// <summary>AC-44-14 schema 半回归哨兵:悬空 clock_ref 必红(002 已实现,
        /// 本文件承 AC 文本各一正一负,防 004 侧误以为「运行期半」豁免 schema 半)。</summary>
        [Test]
        public void test_clockRefDangling_schemaHalf_red()
        {
            // Arrange
            AudioEventTable table = ValidTable();
            foreach (AudioEventTableRow row in table.Rows)
                if (row.Cue == "BreathLayer_Adventitious_Fine")
                    row.ClockRef = "NoSuchBaseLayerCue";

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.ValidateLoopClock(table.Rows);

            // Assert
            Assert.That(AnyError(errors, "悬空"), Is.True, () => string.Join("\n", errors));
            Assert.That(AnyError(errors, "AC-44-14"), Is.True, () => string.Join("\n", errors));
        }

        /// <summary>AC-44-14 正例:合法基础层 + 附加层行(共用同一节拍时钟)⇒ 零错误;
        /// 窗口整体 ⊆ 吸气段(同一周期 = 同一节拍,无独立自由循环的落脚点)。</summary>
        [Test]
        public void test_twoLayersShareSameBeat_zeroErrors()
        {
            // Arrange
            AudioEventTable table = ValidTable();

            // Act
            IReadOnlyList<string> errors = AudioEventTableGates.ValidateLoopClock(table.Rows);
            BreathPhaseGate.WindowCycleFraction(0.9, 0.05, AudioTuning.InspireFraction,
                out double windowStart, out double windowEnd);

            // Assert
            Assert.That(errors, Is.Empty, () => string.Join("\n", errors));
            Assert.That(windowStart, Is.GreaterThanOrEqualTo(0.0));
            Assert.That(windowEnd, Is.LessThanOrEqualTo(AudioTuning.InspireFraction + 1e-12),
                "附加层窗口 ⊆ 吸气段 —— 与基础层同一周期节拍,无第二时钟");
        }

        // ══════════ AC-44-02 [L]:证据骨架(结构断言,不执行听测)══════════

        /// <summary>AC-44-02 证据骨架正例:听测证据文档存在且含协议标记
        /// (三对 / n ≥ 6 / ≥ 75% / 未签署行)—— **不断言已执行**。</summary>
        [Test]
        public void test_listenEvidenceSkeleton_protocolMarkersPresent()
        {
            // Arrange
            string text = ReadRepoFile("production", "qa", "evidence",
                "breath-layers-listen-evidence.md");

            // Act
            IReadOnlyList<string> missing = MissingMarkers(text);

            // Assert:结构断言(存在性 + 协议阈值),非听测结论
            Assert.That(missing, Is.Empty,
                () => "证据骨架缺协议标记:" + string.Join(", ", missing));
            Assert.That(text, Does.Contain("(0,1)"), "三对 forced-choice 标记缺失");
            Assert.That(text, Does.Contain("[ ] Approved"), "签署表未签署行缺失");
            Assert.That(text, Does.Contain("听测未执行"), "骨架必须自陈未执行(防假装通过)");
        }

        /// <summary>AC-44-02 证据骨架负例:缺阈值标记的文本 ⇒ 红(本地标记校验,防骨架漏项)。</summary>
        [Test]
        public void test_listenEvidenceSkeleton_missingThresholdMarker_red()
        {
            // Arrange:缺全部阈值标记的文本
            const string incomplete = "受试者 配对 尝试 判对数";

            // Act
            IReadOnlyList<string> missing = MissingMarkers(incomplete);

            // Assert
            Assert.That(missing, Is.Not.Empty, "缺阈值标记必须红(标记校验不可空转)");
        }

        // ══════════ 纯函数测(Q7 / Q4 可测点:零外部依赖,优先判)══════════

        /// <summary>Q7 可测点 ①:任意帧的 ramp 推进 ≤ 斜率上限 <c>MaxStepForFrame</c>(纯函数)。</summary>
        [Test]
        public void test_filterRamp_frameDelta_withinSlopeCeiling()
        {
            // Arrange:轴域 0 → −10(任意单位),50 ms;ramp 不依赖物理量语义
            FilterRamp.State state = FilterRamp.Begin(0.0, -10.0);
            const double dt = 1.0 / 60.0;
            double ceiling = FilterRamp.MaxStepForFrame(state, dt);
            double previous = 0.0;

            // Act + Assert:逐帧断言 Δ ≤ maxΔ
            int frames = 0;
            while (state.Active && frames < 1000)
            {
                double value = FilterRamp.Step(ref state, dt);
                double delta = Math.Abs(value - previous);
                Assert.That(delta, Is.LessThanOrEqualTo(ceiling + 1e-9),
                    $"第 {frames + 1} 帧推进 {delta} 超斜率上限 {ceiling}");
                previous = value;
                frames++;
            }

            Assert.That(state.Active, Is.False, "1000 帧内必须到位(空转守卫)");
        }

        /// <summary>Q7 可测点 ②:到位计时 ≥ <c>AudioTuning.RampSeconds</c>(≥ 50 ms 硬下界)。</summary>
        [Test]
        public void test_filterRamp_reachesTarget_afterAtLeastRampFloor()
        {
            // Arrange:dt 不整除 0.05(0.017 × 3 = 0.051)—— 断言墙钟 ≥ 下限而非 == 下限
            FilterRamp.State state = FilterRamp.Begin(0.0, 1.0);
            const double dt = 0.017;
            double previous = 0.0;
            double wallSeconds = 0.0;

            // Act
            int frames = 0;
            while (state.Active && frames < 1000)
            {
                previous = FilterRamp.Step(ref state, dt);
                wallSeconds += dt;
                frames++;
            }

            // Assert
            Assert.That(previous, Is.EqualTo(1.0).Within(1e-12), "必须到位到目标值");
            Assert.That(wallSeconds, Is.GreaterThanOrEqualTo(AudioTuning.RampSeconds - 1e-12),
                $"到位墙钟 {wallSeconds} < RampSeconds {AudioTuning.RampSeconds}(GDD F-44.1 硬下界)");
        }

        /// <summary>R2(2026-09-26 评审):<c>Begin(duration ≤ 0)</c> LogError 分支负例 ——
        /// 显式防线 + 按 0 处理(Active false)。</summary>
        [Test]
        public void test_filterRamp_beginNonPositiveDuration_logsAndMarksInactive()
        {
            // Arrange + Act:须先 Expect(防线是显式 LogError,非静默)
            LogAssert.Expect(LogType.Error, new Regex("ramp 时长须 > 0"));
            FilterRamp.State state = FilterRamp.Begin(0.0, 1.0, 0.0);

            // Assert
            Assert.That(state.Active, Is.False, "时长 0 = 无斜坡(不产生假 ramp)");
            Assert.That(state.DurationSeconds, Is.EqualTo(0.0));
        }

        /// <summary>R2:<c>Step(dt &lt; 0)</c> LogError 分支负例 —— 负 dt 不推进。</summary>
        [Test]
        public void test_filterRamp_negativeDt_noAdvance()
        {
            // Arrange
            FilterRamp.State state = FilterRamp.Begin(0.0, 1.0);

            // Act:先 Expect
            LogAssert.Expect(LogType.Error, new Regex("dt < 0"));
            double value = FilterRamp.Step(ref state, -1.0);

            // Assert
            Assert.That(value, Is.EqualTo(0.0).Within(1e-12), "负 dt 不推进(返回起点)");
            Assert.That(state.ElapsedSeconds, Is.EqualTo(0.0).Within(1e-12));
            Assert.That(state.Active, Is.True, "本帧不推进 ≠ 会话结束");
        }

        /// <summary>R2:<c>Step(dt = 0)</c> 合法零帧 —— 不推进且**无日志**(暂停帧语义)。</summary>
        [Test]
        public void test_filterRamp_zeroDt_noAdvanceNoLog()
        {
            // Arrange
            FilterRamp.State state = FilterRamp.Begin(0.0, 1.0);

            // Act(无 LogAssert:零 dt 不得触发任何 LogError)
            double value = FilterRamp.Step(ref state, 0.0);

            // Assert
            Assert.That(value, Is.EqualTo(0.0).Within(1e-12));
            Assert.That(state.ElapsedSeconds, Is.EqualTo(0.0).Within(1e-12));
            Assert.That(state.Active, Is.True);
        }

        /// <summary>R2:<c>HertzToOctaveAxis(≤ 0)</c> LogError 分支负例 —— 返回安全 0 轴。</summary>
        [Test]
        public void test_filterRamp_nonPositiveHz_logsSafeZero()
        {
            // Act + Assert:两次调用各一条防线日志(逐条 Expect)
            LogAssert.Expect(LogType.Error, new Regex("频率须 > 0"));
            Assert.That(FilterRamp.HertzToOctaveAxis(0.0), Is.EqualTo(0.0));

            LogAssert.Expect(LogType.Error, new Regex("频率须 > 0"));
            Assert.That(FilterRamp.HertzToOctaveAxis(-5.0), Is.EqualTo(0.0));
        }

        /// <summary>常量单处 + 硬下界:RampSeconds ≥ 50 ms(GDD :312);InspireFraction ∈ (0,1)。</summary>
        [Test]
        public void test_audioTuning_rampSeconds_atLeastGddFloor()
        {
            Assert.That(AudioTuning.RampSeconds, Is.GreaterThanOrEqualTo(0.05f),
                "GDD F-44.1 注:ramp ≥ 50 ms 硬下界(种子值也不得低于下限)");
            Assert.That(AudioTuning.InspireFraction, Is.InRange(0.0001, 0.9999),
                "GDD §Tuning Knobs:INSPIRE_FRACTION 安全范围 (0,1)");
        }

        /// <summary>Q4:相位分母 = <c>clip.length</c>(非旋钮 T)—— 同一 timeSamples,
        /// 不同 clip 长度 ⇒ 不同相位(分母来自 clip)。</summary>
        [Test]
        public void test_cyclePhase_denominatorIsClipLength()
        {
            // Arrange + Act
            double phaseAtFourSeconds = BreathPhaseGate.CyclePhase(2000, 1000, 4.0);
            double phaseAtTwoSeconds = BreathPhaseGate.CyclePhase(2000, 1000, 2.0);

            // Assert
            Assert.That(phaseAtFourSeconds, Is.EqualTo(0.5).Within(1e-12));
            Assert.That(phaseAtTwoSeconds, Is.EqualTo(1.0).Within(1e-12),
                "分母 = freq × clip.length:clip 变短则同一采样位置更靠后(pitch ≠ 1 的真实 T 由此吸收)");
        }

        /// <summary>形态裁定:相位旗复位跟着 timeSamples —— 回绕与 Seek 前跳都要复位,正常推进不复位。</summary>
        [Test]
        public void test_shouldResetCycleFlag_wrapAndSeekBothReset()
        {
            // Act + Assert
            Assert.That(BreathPhaseGate.ShouldResetCycleFlag(0.97, 0.01, 0.1), Is.True,
                "回绕(相位倒退)必须复位");
            Assert.That(BreathPhaseGate.ShouldResetCycleFlag(0.10, 0.60, 0.05), Is.True,
                "Seek 前跳(超出期望推进)必须复位 —— 防跳过窗口后漏发");
            Assert.That(BreathPhaseGate.ShouldResetCycleFlag(0.10, 0.12, 0.05), Is.False,
                "正常推进不得复位(误复位 = 同周期双发)");
        }

        /// <summary>R3(2026-09-26 评审):<c>CyclePhase</c> 非法分母负例 ——
        /// frequency ≤ 0 与 timeSamples &lt; 0 两路均 LogError + 返回周期原点 0。</summary>
        [Test]
        public void test_cyclePhase_invalidDenominator_logsSafeZero()
        {
            // Act + Assert:两路防线各一条日志(逐条 Expect)
            LogAssert.Expect(LogType.Error, new Regex("相位分母非法"));
            Assert.That(BreathPhaseGate.CyclePhase(0, 0, 4.0), Is.EqualTo(0.0),
                "frequency ≤ 0 ⇒ 0(周期原点),不抛不静默");

            LogAssert.Expect(LogType.Error, new Regex("相位分母非法"));
            Assert.That(BreathPhaseGate.CyclePhase(-1, 1000, 4.0), Is.EqualTo(0.0),
                "timeSamples < 0 同防线");
        }

        /// <summary>R3:<c>ShouldFire</c> 的 <c>inspireFraction ∉ (0,1)</c> 分支负例 ——
        /// 旋钮越界 ⇒ 不触发(GDD §Tuning Knobs 安全范围)。</summary>
        [Test]
        public void test_shouldFire_invalidInspireFraction_returnsFalse()
        {
            // Act + Assert:先 Expect 防线日志
            LogAssert.Expect(LogType.Error, new Regex("INSPIRE_FRACTION 须"));
            Assert.That(
                BreathPhaseGate.ShouldFire(0.3, 0.9, 0.05, 1.5, false), Is.False,
                "F ∉ (0,1) ⇒ 本周期不触发(不落医学反相窗口)");
        }

        /// <summary>Q7:门控音量进窗 / 出窗**各走一次 ≥ RampSeconds ramp**(禁裸 0/1)——
        /// 经 BreathLayerDriver + 记录型传输的行为断言。</summary>
        [Test]
        public void test_gateVolume_openAndClose_eachRampsAtLeast50ms()
        {
            // Arrange:基础层 1000 Hz × 4 s = 4000 采样;窗 [0.9×F − 0.05×F, 0.9×F + 0.05×F]
            // ⊇ phase 0.3(= 1200/4000);F = AudioTuning.InspireFraction
            var baseTransport = new FakeLayerTransport
            {
                Frequency = 1000,
                ClipLengthSeconds = 4.0,
                TimeSamples = 0,
            };
            var advTransport = new FakeLayerTransport { Frequency = 1000, ClipLengthSeconds = 4.0 };
            var driver = new BreathLayerDriver(baseTransport, advTransport);

            // Act 1:窗外(首帧)
            driver.Tick(0.00, 0.9, 0.05, AudioTuning.InspireFraction);
            Assert.That(driver.GateVolume, Is.EqualTo(0f).Within(1e-6f),
                "窗外音量 = 0");

            // Act 2:进窗(phase = 1200/4000 = 0.3 ∈ 窗)—— 首帧只开 ramp,不裸跳 1
            baseTransport.TimeSamples = 1200;
            driver.Tick(0.01, 0.9, 0.05, AudioTuning.InspireFraction);
            Assert.That(driver.GateRampActive, Is.True, "进窗必须开 ramp");
            Assert.That(driver.GateVolume, Is.EqualTo(0.2f).Within(0.001f),
                $"进窗首帧 = Step(0.01/0.05) ≈ 0.2,不是裸 1;实得 {driver.GateVolume}");

            // Act 3:继续推进到位(0.02…0.06 ⇒ 累计 0.06 ≥ 0.05)
            for (int i = 2; i <= 6; i++)
                driver.Tick(i * 0.01, 0.9, 0.05, AudioTuning.InspireFraction);

            // Assert:开到位
            Assert.That(driver.GateVolume, Is.EqualTo(1f).Within(1e-6f), "进窗 ramp 必须到位到 1");
            Assert.That(driver.GateRampActive, Is.False);
            Assert.That(driver.GateRampElapsedSeconds,
                Is.GreaterThanOrEqualTo(AudioTuning.RampSeconds - 1e-9),
                "进窗 ramp 时长 ≥ RampSeconds(GDD :312)");

            // Act 4:出窗(phase = 2000/4000 = 0.5)—— 同样开 ramp,不裸跳 0
            baseTransport.TimeSamples = 2000;
            driver.Tick(0.07, 0.9, 0.05, AudioTuning.InspireFraction);
            Assert.That(driver.GateRampActive, Is.True, "出窗必须开 ramp");
            Assert.That(driver.GateVolume, Is.EqualTo(0.8f).Within(0.001f),
                $"出窗首帧 = Step(1 → 0.8),不是裸 0;实得 {driver.GateVolume}");

            // Act 5:出窗推进到位(0.08…0.12 ⇒ 累计 0.06 ≥ 0.05)
            for (int i = 8; i <= 12; i++)
                driver.Tick(i * 0.01, 0.9, 0.05, AudioTuning.InspireFraction);

            // Assert:关到位
            Assert.That(driver.GateVolume, Is.EqualTo(0f).Within(1e-6f), "出窗 ramp 必须到位到 0");
            Assert.That(driver.GateRampActive, Is.False);
            Assert.That(driver.GateRampElapsedSeconds,
                Is.GreaterThanOrEqualTo(AudioTuning.RampSeconds - 1e-9),
                "出窗 ramp 时长 ≥ RampSeconds");
            Assert.That(advTransport.Volume, Is.EqualTo(0f).Within(1e-6f),
                "门控输出必须落到传输(假件核对)");
        }

        // ══════════ 共用小件 ══════════

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

        /// <summary>合法表:三档六列 + 基础层行 + 附加层行(loop / clock_ref / policy 齐)。</summary>
        private static AudioEventTable ValidTable()
        {
            var tierMap = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
            {
                ["0"] = FullTierColumns(),
                ["1"] = FullTierColumns(),
                ["2"] = FullTierColumns(),
            };

            return new AudioEventTable
            {
                SchemaVersionRaw = "1",
                TierMap = tierMap,
                Rows = new List<AudioEventTableRow> { BaseLayerRow(), AdventitiousRow() },
            };
        }

        /// <summary>基础气流层行(loop 自循环、无 clock_ref、持续生理)。</summary>
        private static AudioEventTableRow BaseLayerRow()
            => new AudioEventTableRow
            {
                Cue = "BreathLayer_Base_Calm",
                Loop = true,
                ClockRef = null,
                WhitelistCategory = "ContinuousPhysiology",
                TriggerSource = "ContinuousPhysiology",
                SchemaVersionRaw = "1",
            };

        /// <summary>附加音层行(loop: true + clock_ref 指基础层 + 相位窗 0.9 ± 0.05)。</summary>
        private static AudioEventTableRow AdventitiousRow()
            => new AudioEventTableRow
            {
                Cue = "BreathLayer_Adventitious_Fine",
                Loop = true,
                ClockRef = "BreathLayer_Base_Calm",
                WhitelistCategory = "ContinuousPhysiology",
                TriggerSource = "ContinuousPhysiology",
                SchemaVersionRaw = "1",
                Assets = new List<string> { "sfx_breath_adventitious_fine.wav" },
                Policy = new AdventitiousPolicyRaw
                {
                    TriggerPhaseRaw = "0.9",
                    JitterRaw = "0.05",
                },
            };

        /// <summary>读仓库文件(前置 File.Exists —— 缺失即红,不静默跳过)。</summary>
        private static string ReadRepoFile(params string[] segments)
        {
            string[] all = new string[segments.Length + 1];
            all[0] = repoRoot();
            Array.Copy(segments, 0, all, 1, segments.Length);
            string path = Path.Combine(all);
            Assert.That(File.Exists(path), Is.True, $"仓库文件缺失:{path}");
            return File.ReadAllText(path);
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

        /// <summary>反射 dump 里的成员取值器(诊断辅助):成员名含 <paramref name="wantedKeyword"/>
        /// (name / guid,忽略大小写 —— **dump 全量先行、选取后行**,不硬预设字段形态)。
        /// string 直取;其它类型 <c>ToString()</c>(GUID 结构 = 32 hex);空串 / 括号占位
        /// (异常标记)⇒ null(视为未取到,由 <see cref="IsPlainHex32"/> 在下游自证)。</summary>
        private static string PickMemberValue(string memberName, object value, string wantedKeyword)
        {
            if (value == null) return null;
            if (memberName.IndexOf(wantedKeyword, StringComparison.OrdinalIgnoreCase) < 0)
                return null;
            if (value is string s)
                return string.IsNullOrEmpty(s) ? null : s;
            string text = value.ToString();
            if (string.IsNullOrEmpty(text) || text.StartsWith("(", StringComparison.Ordinal))
                return null;
            return text;
        }

        /// <summary>是否**纯 32 位 hex**(= 真 guid 原值已取到;诊断占位串 / 空值 ⇒ false)。</summary>
        private static bool IsPlainHex32(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 32)
                return false;
            foreach (char c in value)
            {
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex) return false;
            }

            return true;
        }

        /// <summary>从 .mixer YAML 原文读 <c>m_ExposedParameters</c> 的 name → guid
        /// (诊断对照用 —— 与反射读内部面的加载值比对;手扫,零正则)。</summary>
        private static Dictionary<string, string> ReadYamlExposedGuids(string yamlText)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(yamlText)) return map;
            string[] lines = yamlText.Replace("\r\n", "\n").Split('\n');
            int start = -1;
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Trim() == "m_ExposedParameters:") { start = i; break; }
            if (start < 0) return map;

            string pendingGuid = null;
            for (int i = start + 1; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.StartsWith("- guid:", StringComparison.Ordinal) ||
                    trimmed.StartsWith("guid:", StringComparison.Ordinal))
                {
                    int key = trimmed.IndexOf("guid:", StringComparison.Ordinal);
                    pendingGuid = trimmed.Substring(key + "guid:".Length).Trim();
                    continue;
                }

                if (trimmed.StartsWith("- name:", StringComparison.Ordinal) ||
                    trimmed.StartsWith("name:", StringComparison.Ordinal))
                {
                    int key = trimmed.IndexOf("name:", StringComparison.Ordinal);
                    string name = trimmed.Substring(key + "name:".Length).Trim();
                    if (pendingGuid != null)
                        map[name] = pendingGuid;
                    pendingGuid = null;
                    continue;
                }

                if (trimmed.Length == 0) continue;
                break;   // 块结束
            }

            return map;
        }

        /// <summary>证据骨架的协议阈值标记缺项(结构断言用;非听测判据)。</summary>
        private static IReadOnlyList<string> MissingMarkers(string text)
        {
            var missing = new List<string>();
            foreach (string marker in new[] { "n ≥ 6", "≥ 75%" })
            {
                if (text == null || text.IndexOf(marker, StringComparison.Ordinal) < 0)
                    missing.Add(marker);
            }

            return missing;
        }

        // 本文件在 unity/Assets/Tests/EditMode/Audio/ ⇒ Audio→EditMode→Tests→Assets→unity→仓库根 = 5 层
        // (写 4 层 ⇒ root 落在 unity/ 下,夹具 / GDD / 源树全部报「缺失」—— Story 003 踩过)
        private static string repoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        // ══════════ 测试替身 ══════════

        /// <summary>记录型 <c>IAudioCueSink</c> 替身(unity-specialist Q5 主判据形态;
        /// 零引擎依赖,断言咳嗽 / 收尾行为)。</summary>
        private sealed class RecordingCueSink : IAudioCueSink
        {
            /// <summary>Emit 调用次数(咳嗽 = 一次性通道)。</summary>
            public int EmitCount;

            /// <summary>EndLoop 收到的句柄 id 序列(收尾核对)。</summary>
            public readonly List<int> EndedHandles = new List<int>();

            private int _nextId = 100;

            /// <inheritdoc/>
            public void Emit(in AudioCueDto cue) => EmitCount++;

            /// <inheritdoc/>
            public AudioCueHandle BeginLoop(in AudioCueDto cue)
                => new AudioCueHandle(_nextId++);

            /// <inheritdoc/>
            public void EndLoop(AudioCueHandle handle) => EndedHandles.Add(handle.Id);

            /// <inheritdoc/>
            public void SetTier(TierSource source)
            {
                // 记录型替身:档位切换不影响本测试的断言面
            }
        }

        /// <summary>记录型 <c>IBreathLayerTransport</c> 替身(相位脚本 + 音量记录;
        /// 零 <c>AudioSource</c> 依赖)。</summary>
        private sealed class FakeLayerTransport : IBreathLayerTransport
        {
            /// <inheritdoc/>
            public int TimeSamples { get; set; }

            /// <inheritdoc/>
            public int Frequency { get; set; } = 1000;

            /// <inheritdoc/>
            public double ClipLengthSeconds { get; set; } = 4.0;

            /// <inheritdoc/>
            public float Volume { get; set; }
        }
    }
}
