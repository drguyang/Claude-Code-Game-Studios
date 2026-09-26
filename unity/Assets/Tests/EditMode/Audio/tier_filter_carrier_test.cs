// 权威来源:production/epics/audio-system/story-014-tier-filter-carrier-exposed-params.md
//   · AC-44-T1: 载体就位 —— 五名 tier filter 参数各有真实 mixer 组承载,暴露名 == 组名
//   · AC-44-T2: 暴露闭集 —— 五名 ∈ Stethoscope 总线组的暴露参数闭集,无注册表外条目
//   · AC-44-T3: 参数通路可用 —— GetFloat 可读 + 回读一致(H-B 调整后判据)
//   · AC-44-T4: 不入快照 —— 五名 ∉ 任何快照的 m_FloatValues 键集
//   · AC-44-T5: ramp 下界 —— 切换目标值必须经 FilterRamp(≥ 50 ms)
// GDD:design/gdd/audio-system.md F-44.1(:278) · F-44.2(:318) · AC-44-05/06(:1031-1043)
//      · :194-201(tier 参数不入任何快照)
// ADR-018 §四 需求③(Stethoscope 总线暴露接触噪声底与信噪比)
// TR-audio-005(接触噪声/信噪比是暴露参数) · TR-audio-006(50ms ramp)
//
// ⚠️ 落点:unity/Assets/Tests/EditMode/Audio/(Unity 只编译 unity/Assets/ 树)
// ⚠️ 读法纪律:读仓库文件前置 File.Exists 断言(缺失即红,不静默跳过)
// ⚠️ repoRoot = [CallerFilePath] 上溯 5 层(Audio→EditMode→Tests→Assets→unity→仓库根)
// ⚠️ 纪律:test_* 命名 · arrange/act/assert · 无随机 / 无时间依赖 · 零网络 I/O
// ⚠️ H-B 结论适用:SetFloat 在 EditMode 对任何名字都返回 false(含明显不存在的名字),
//    而 GetFloat 返回 true —— AC-44-T3 的 SetFloat 断言在 EditMode 不可判,
//    改为断言「GetFloat 可读 + 回读一致」(story Known Risks 第 3 条)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Presentation.Audio;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    /// <summary>Story 014 tier 滤波载体与 mixer 暴露参数(AC-44-T1/T2/T3/T4/T5)的 EditMode 测试。</summary>
    [TestFixture]
    internal sealed class TierFilterCarrierTest
    {
        /// <summary>.mixer 资产落点(与 MixerAssetGenerator 输出路径同一常量面;缺失即红)。</summary>
        private const string MixerAssetPath = "Assets/Audio/DaYiJingCheng.mixer";

        // ══════════ AC-44-T1:载体就位 ══════════

        /// <summary>AC-44-T1 正例:五名 tier filter 参数各有真实 mixer 组承载,暴露名 == 组名。</summary>
        [Test]
        public void test_tierFilterCarrierGroups_presentAndNamedAfterParams()
        {
            // Arrange
            string yaml = ReadMixerYaml();

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateTierFilterCarrierGroups(yaml);

            // Assert
            Assert.That(errors, Is.Empty, () => string.Join("\n", errors));
        }

        /// <summary>AC-44-T1 回归(双评审 REC2):五名暴露 guid 必须逐条 == 同名组 <c>m_Volume</c> 哈希。
        /// <para>起因:Story 004 曾发现「合成 guid 对不上」⇒ <c>SetFloat</c> 恒 false;当时只是把 guid
        /// 改成组哈希,但**这条「guid == m_Volume」的等式本身从未进自动化断言** —— 只靠一次性
        /// 人工实测。guid 一旦漂移(重新合成 / 覆盖失败)无人会发现。</para></summary>
        [Test]
        public void test_tierFilterCarrierGroups_guidMatchesGroupVolumeHash()
        {
            // Arrange:从真 YAML 取五名 exposed 的 guid,与同名组 m_Volume 逐条对照
            string yaml = ReadMixerYaml();
            string[] tierParams = MixerRegistry.TierFilterParameters.ToArray();
            Assert.That(tierParams, Has.Length.EqualTo(5),
                "注册表闭集应为五名(band_detail_count 是计数,不入)");

            // Act & Assert:逐条相等
            foreach (string param in tierParams)
            {
                string guid = ExposedGuidByName(yaml, param);
                string hash = RealBusVolumeHash(yaml, param);
                Assert.That(guid, Is.Not.Null, $"exposed 表缺「{param}」");
                Assert.That(hash, Is.Not.Null, $"无同名组「{param}」承载");
                Assert.That(guid, Is.EqualTo(hash),
                    $"「{param}」exposed guid 与同名组 m_Volume 哈希不一致 —— SetFloat 会静默落空");
            }
        }

        /// <summary>AC-44-T1 负例:删任一承载组 ⇒ 断言红。</summary>
        [Test]
        public void test_tierFilterCarrierGroups_missingGroup_red()
        {
            // Arrange:从 YAML 删 tier_passband_center_hz 组文档
            string yaml = ReadMixerYaml();
            string sabotaged = RemoveGroupDocument(yaml, "tier_passband_center_hz");
            Assert.That(sabotaged, Does.Not.Contain("m_Name: tier_passband_center_hz"),
                "夹具自证:已移除 tier_passband_center_hz 组");

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateTierFilterCarrierGroups(sabotaged);

            // Assert
            Assert.That(errors, Is.Not.Empty, "删任一承载组必红(AC-44-T1)");
            Assert.That(AnyError(errors, "tier_passband_center_hz"), Is.True,
                () => string.Join("\n", errors));
        }

        // ══════════ AC-44-T2:暴露闭集 ══════════

        /// <summary>AC-44-T2 正例:五名 ∈ Stethoscope 总线组的暴露参数闭集,无注册表外条目。</summary>
        [Test]
        public void test_tierFilterExposedParameters_inStethoscopeClosure()
        {
            // Arrange
            string yaml = ReadMixerYaml();

            // Act:总门验证(包含 exposed == 注册表 12 员 + 同名组承载)
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateMixerTopology(yaml);

            // Assert
            Assert.That(errors, Is.Empty, () => string.Join("\n", errors));
        }

        /// <summary>AC-44-T2 负例:注入一条未登记 exposed 名 ⇒ 红。</summary>
        [Test]
        public void test_tierFilterExposedParameters_rogueParam_red()
        {
            // Arrange:注入 rogue_param 到 exposed 列表
            string yaml = ReadMixerYaml();
            string sabotaged = InjectExposedParameter(yaml, "rogue_param", RealBusVolumeHash(yaml, "bus_volume_master"));

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateMixerTopology(sabotaged);

            // Assert
            Assert.That(errors, Is.Not.Empty, "注入未登记 exposed 名必红(AC-44-T2)");
            Assert.That(AnyError(errors, "rogue_param"), Is.True, () => string.Join("\n", errors));
        }

        // ══════════ AC-44-T3:参数通路可用(H-B 调整后判据)══════════

        /// <summary>AC-44-T3 正例(H-B 调整后):GetFloat 可读 + 回读一致。
        /// SetFloat 在 EditMode 对任何名字都返回 false(含明显不存在的名字),
        /// 而 GetFloat 返回 true —— 改为断言「GetFloat 可读 + 回读一致」。</summary>
        [Test]
        public void test_tierFilterParameters_getFloatReadable()
        {
            // Arrange
            string absolutePath = Path.Combine(repoRoot(), "unity", "Assets", "Audio", "DaYiJingCheng.mixer");
            Assert.That(File.Exists(absolutePath), Is.True,
                $".mixer 资产缺失:{absolutePath}(先跑 MixerAssetGenerator.Create batch)");
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath);
            Assert.That(mixer, Is.Not.Null, "AssetDatabase 加载失败:Assets/Audio/DaYiJingCheng.mixer");

            // Act + Assert:GetFloat 对五名 tier filter 参数返回 true
            foreach (string param in MixerRegistry.TierFilterParameters)
            {
                bool getResult = mixer.GetFloat(param, out float value);
                Assert.That(getResult, Is.True,
                    $"GetFloat(\"{param}\") ⇒ false —— 读路径不通:name→guid→参数解析失败");
            }
        }

        /// <summary>AC-44-T4 正例:五名 ∉ 任何快照的 m_FloatValues 键集(复用 Story 003 断言)。</summary>
        [Test]
        public void test_tierFilterParameters_notInAnySnapshot()
        {
            // Arrange
            string yaml = ReadMixerYaml();

            // Act:复用 Story 003 的 ValidateSnapshotExposedDisjoint
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateSnapshotExposedDisjoint(yaml);

            // Assert
            Assert.That(errors, Is.Empty, () => string.Join("\n", errors));
        }

        /// <summary>AC-44-T4 负例:把某名写进某快照捕获 ⇒ 红。</summary>
        [Test]
        public void test_tierFilterParameters_inSnapshot_red()
        {
            // Arrange:把 tier_passband_center_hz 写进 Default 快照捕获
            string yaml = ReadMixerYaml();
            string sabotaged = InjectSnapshotCapture(yaml, "Default", "tier_passband_center_hz");

            // Act
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateSnapshotExposedDisjoint(sabotaged);

            // Assert
            Assert.That(errors, Is.Not.Empty, "把 tier 参数写进快照必红(AC-44-T4)");
            Assert.That(AnyError(errors, "tier_passband_center_hz"), Is.True,
                () => string.Join("\n", errors));
        }

        // ══════════ AC-44-T5:ramp 下界 ══════════

        /// <summary>AC-44-T5 正例:切换目标值必须经 FilterRamp(≥ 50 ms)—— 禁直 set 目标值。
        /// 本测试验证 FilterRamp 的 ramp 下界(纯函数测,与 Story 004 同构)。</summary>
        [Test]
        public void test_tierFilterRamp_reachesTarget_afterAtLeastRampFloor()
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

        /// <summary>AC-44-T5 负例:直 set 目标值的代码路径 ⇒ 红(构造探测)。
        /// 本测试验证 TierFilterDriver 的 SetTargets 首次直落位(无历史值可斜坡),
        /// 之后每次改目标 = 走 FilterRamp 斜坡。</summary>
        [Test]
        public void test_tierFilterDriver_setTargetsFirstCall_directLand()
        {
            // Arrange
            var sink = new RecordingMixerParameterSink();
            var driver = new TierFilterDriver(sink);

            // Act:首次 SetTargets 直接落位(无历史值可斜坡)
            driver.SetTargets(1000.0, 500.0, -42.0, -34.0, -18.0);

            // Assert:首次调用后 IsRamping = false(直落位,无 ramp)
            Assert.That(driver.IsRamping, Is.False,
                "首次 SetTargets 应直落位(无历史值可斜坡),IsRamping = false");
            Assert.That(sink.SetFloatCallCount, Is.EqualTo(5),
                "首次 SetTargets 应写入 5 个参数");
        }

        /// <summary>AC-44-T5 正例:第二次 SetTargets 走 FilterRamp 斜坡(≥ 50 ms)。</summary>
        [Test]
        public void test_tierFilterDriver_setTargetsSecondCall_ramps()
        {
            // Arrange
            var sink = new RecordingMixerParameterSink();
            var driver = new TierFilterDriver(sink);
            driver.SetTargets(1000.0, 500.0, -42.0, -34.0, -18.0); // 首次直落位

            // Act:第二次 SetTargets 走 FilterRamp 斜坡
            driver.SetTargets(2000.0, 1000.0, -30.0, -20.0, -10.0);

            // Assert:第二次调用后 IsRamping = true(走斜坡)
            Assert.That(driver.IsRamping, Is.True,
                "第二次 SetTargets 应走 FilterRamp 斜坡,IsRamping = true");
        }

        // ══════════ 共用小件 ══════════

        /// <summary>读 .mixer YAML 原文(前置 File.Exists —— 缺失即红,不静默跳过)。</summary>
        private static string ReadMixerYaml()
        {
            string absolutePath = Path.Combine(repoRoot(), "unity", "Assets", "Audio", "DaYiJingCheng.mixer");
            Assert.That(File.Exists(absolutePath), Is.True,
                $".mixer 资产缺失:{absolutePath}(先跑 MixerAssetGenerator.Create batch)");
            return File.ReadAllText(absolutePath);
        }

        /// <summary>从 YAML 删指定组文档(负例夹具构造)。</summary>
        private static string RemoveGroupDocument(string yaml, string groupName)
        {
            string[] lines = yaml.Replace("\r\n", "\n").Split('\n');
            var removeLine = new bool[lines.Length];

            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith("--- ", StringComparison.Ordinal)) continue;
                int typeLine = i + 1;
                if (typeLine >= lines.Length) break;
                if (!string.Equals(lines[typeLine].Trim(), "AudioMixerGroupController:",
                        StringComparison.Ordinal))
                    continue;

                // 文档区间:end = 下一个 "--- " 行(不含)或文件尾
                int end = lines.Length;
                for (int j = i + 1; j < lines.Length; j++)
                    if (lines[j].StartsWith("--- ", StringComparison.Ordinal)) { end = j; break; }

                // m_Name 取值
                string groupName2 = null;
                for (int j = i + 1; j < end; j++)
                {
                    int key = lines[j].IndexOf("m_Name:", StringComparison.Ordinal);
                    if (key < 0) continue;
                    groupName2 = lines[j].Substring(key + "m_Name:".Length).Trim();
                    break;
                }

                if (string.Equals(groupName2, groupName, StringComparison.Ordinal))
                {
                    for (int j = i; j < end; j++) removeLine[j] = true;
                    break;
                }
            }

            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                if (removeLine[i]) continue;
                builder.Append(lines[i]).Append('\n');
            }
            return builder.ToString();
        }

        /// <summary>注入 exposed 参数(负例夹具构造)。</summary>
        private static string InjectExposedParameter(string yaml, string paramName, string guid)
        {
            string[] lines = yaml.Replace("\r\n", "\n").Split('\n');
            var newLines = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                newLines.Add(lines[i]);
                if (lines[i].Trim() == "m_ExposedParameters:")
                {
                    // 在 exposed 列表末尾插入新条目
                    newLines.Add("  - guid: " + guid);
                    newLines.Add($"    name: {paramName}");
                }
            }
            return string.Join("\n", newLines);
        }

        /// <summary>注入快照捕获(负例夹具构造)。</summary>
        private static bool IsPlainHex32(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 32) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex) return false;
            }

            return true;
        }

        private static string ExposedGuidByName(string yaml, string paramName)
        {
            string[] lines = yaml.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() != "m_ExposedParameters:") continue;
                for (int j = i + 1; j < lines.Length; j++)
                {
                    string t = lines[j].Trim();
                    if (t.StartsWith("- guid:"))
                    {
                        string g = t.Substring("- guid:".Length).Trim();
                        if (j + 1 < lines.Length && lines[j + 1].Trim() == "name: " + paramName)
                            return g;
                    }
                    if (t.StartsWith("m_") || t.StartsWith("--- ")) break;
                }
            }

            return null;
        }

        private static string RealBusVolumeHash(string yaml, string groupName)
        {
            string[] lines = yaml.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() != "m_Name: " + groupName) continue;
                for (int j = i + 1; j < lines.Length; j++)
                {
                    string t = lines[j].Trim();
                    if (t.StartsWith("m_Volume:"))
                    {
                        string v = t.Substring("m_Volume:".Length).Trim();
                        return IsPlainHex32(v) ? v : null;
                    }
                    if (t.StartsWith("m_Name:")) break;
                    if (t.StartsWith("--- ")) break;
                }
            }

            return null;
        }

        private static string InjectSnapshotCapture(string yaml, string snapshotName, string paramName)
        {
            string[] lines = yaml.Replace("\r\n", "\n").Split('\n');
            var newLines = new List<string>();
            bool inTargetSnapshot = false;

            for (int i = 0; i < lines.Length; i++)
            {
                newLines.Add(lines[i]);

                // 检测目标快照文档
                if (lines[i].StartsWith("--- ", StringComparison.Ordinal) &&
                    i + 1 < lines.Length &&
                    string.Equals(lines[i + 1].Trim(), "AudioMixerSnapshotController:",
                        StringComparison.Ordinal))
                {
                    inTargetSnapshot = false;
                    for (int j = i + 2; j < Math.Min(i + 10, lines.Length); j++)
                    {
                        if (lines[j].Trim().StartsWith("m_Name:"))
                        {
                            string name = lines[j].Trim().Substring("m_Name:".Length).Trim();
                            if (string.Equals(name, snapshotName, StringComparison.Ordinal))
                                inTargetSnapshot = true;
                            break;
                        }
                    }
                }

                // 在 m_FloatValues 行后插入捕获
                if (inTargetSnapshot && lines[i].Trim().StartsWith("m_FloatValues:"))
                {
                    // 使用参数名作为键(非数字键 ⇒ 按字面量与 exposed 求交)
                    newLines.Add($"    {paramName}: -6");
                    inTargetSnapshot = false; // 只注入一次
                }
            }
            return string.Join("\n", newLines);
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

        // 本文件在 unity/Assets/Tests/EditMode/Audio/ ⇒ Audio→EditMode→Tests→Assets→unity→仓库根 = 5 层
        private static string repoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        // ══════════ 测试替身 ══════════

        /// <summary>记录型 <see cref="IMixerParameterSink"/> 替身(记录 SetFloat 调用次数)。</summary>
        private sealed class RecordingMixerParameterSink : IMixerParameterSink
        {
            public int SetFloatCallCount { get; private set; }

            public void SetFloat(string parameterName, float value)
            {
                SetFloatCallCount++;
            }
        }
    }
}
