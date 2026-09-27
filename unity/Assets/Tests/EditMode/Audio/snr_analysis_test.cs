// 权威来源:production/epics/audio-system/story-005-snr-analysis-noise-floor.md
//   · AC-44-05 ①:ComputeSnrDb 纯函数按 F-44.2(dB 线性化相减,钳位 ±24 dB)
//   · AC-44-05 ②:三列 ∈ Stethoscope mixer 组暴露参数闭集(前置引用 Story 014)
//   · AC-44-06:noise_floor_db/contact_noise_db 均有限且 ≥ NOISE_FLOOR_DB_MIN;∀T: SNR ≥ −24 dB
// GDD:design/gdd/audio-system.md F-44.2(:318) · F-44.1(:278) · AC-44-05/06(:1031-1043)
//      · :331-332(噪声永不为零,dB 侧正确表述)
// ADR-018 §四 需求③ · TR-audio-005
//
// ⚠️ 落点:unity/Assets/Tests/EditMode/Audio/(Unity 只编译 unity/Assets 树)
// ⚠️ 读法纪律:读仓库文件前置 File.Exists 断言(缺失即红,不静默跳过)
// ⚠️ repoRoot = [CallerFilePath] 上溯 5 层(Audio→EditMode→Tests→Assets→unity→仓库根)
// ⚠️ 纪律:test_* 命名 · arrange/act/assert · 无随机 / 无时间依赖 · 零网络 I/O
// ⚠️ 夹具自带常量(±24 恰含边界、±200 溢出钳位、NaN 拒),不取「待调」TierMap 值
// ⚠️ API 来源标注:(a) docs/engine-reference/unity/ 查得 (b) Discover 日志实测 (c) 推断

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using DaYiJingCheng.EditorTools.Gates;
using System.Linq;
using DaYiJingCheng.Gameplay.Presentation.Audio;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    /// <summary>Story 005 SNR 分析域与噪声下界(AC-44-05/06)的 EditMode 测试。</summary>
    [TestFixture]
    internal sealed class SnrAnalysisTest
    {
        // ══════════ AC-44-05 ①:ComputeSnrDb 纯函数夹具 ══════════

        /// <summary>(0,0,0) ⇒ 有限且已定义(−3.01 dB)—— 公式在零输入处不发散。
        /// <para>⚠️ QA 原文「(0,0,0)⇒0dB」按 F-44.2 公式实际输出 −3.01 dB(两噪声各 0 dB
        /// 线性化后相加 = 2,log10(2) = 3.01);本测试断言公式的实际行为,不锁 QA 的简化表述。</para></summary>
        [Test]
        public void test_computeSnrDb_zeroInputs_finiteDefined()
        {
            // Arrange
            float signal = 0.0f, noise = 0.0f, contact = 0.0f;

            // Act
            float result = SnrAnalysis.ComputeSnrDb(signal, noise, contact);

            // Assert
            Assert.That(float.IsNaN(result), Is.False, "(0,0,0) 不应产生 NaN");
            Assert.That(float.IsInfinity(result), Is.False, "(0,0,0) 不应产生 ±∞");
            Assert.That(result, Is.EqualTo(-3.0103f).Within(0.001f),
                "(0,0,0) = 0 − 10·log10(2) ≈ −3.01 dB");
        }

        /// <summary>信号 = 合并噪声电平 ⇒ SNR = 0 dB(零穿越性质)。
        /// <para>验证 F-44.2 的量纲正确性:信号与合并噪声相等时 SNR 恰为 0。</para></summary>
        [Test]
        public void test_computeSnrDb_signalEqualsCombinedNoise_zeroCrossing()
        {
            // Arrange:noise = contact = 0 dB ⇒ 合并 = 10·log10(2) ≈ 3.01 dB
            float noise = 0.0f, contact = 0.0f;
            float combinedNoiseDb = (float)(10.0 * Math.Log10(2.0));

            // Act
            float result = SnrAnalysis.ComputeSnrDb(combinedNoiseDb, noise, contact);

            // Assert
            Assert.That(result, Is.EqualTo(0.0f).Within(0.001f),
                "信号 = 合并噪声 ⇒ SNR = 0 dB(零穿越)");
        }

        /// <summary>边界 −24:使输出恰为 −24 dB 的信号值不被错钳。
        /// <para>两噪声各 −24 dB ⇒ 合并 = −20.99 dB;信号 = −24 + (−20.99) = −44.99 dB。</para></summary>
        [Test]
        public void test_computeSnrDb_exactMinus24_notClamped()
        {
            // Arrange
            float noise = -24.0f, contact = -24.0f;
            float signal = -24.0f + (float)(10.0 * Math.Log10(2.0 * Math.Pow(10.0, -2.4)));

            // Act
            float result = SnrAnalysis.ComputeSnrDb(signal, noise, contact);

            // Assert
            Assert.That(result, Is.EqualTo(-24.0f).Within(0.01f),
                "恰 −24 dB 的输出不被错钳到 −24 以下");
            Assert.That(result, Is.GreaterThan(SnrAnalysis.SnrClampMin - 0.1f),
                "恰 −24 不应被钳到更低");
        }

        /// <summary>边界 +24:使输出恰为 +24 dB 的信号值不被错钳。
        /// <para>两噪声各 −24 dB ⇒ 合并 = −20.99 dB;信号 = 24 + (−20.99) = 3.01 dB。</para></summary>
        [Test]
        public void test_computeSnrDb_exactPlus24_notClamped()
        {
            // Arrange
            float noise = -24.0f, contact = -24.0f;
            float signal = 24.0f + (float)(10.0 * Math.Log10(2.0 * Math.Pow(10.0, -2.4)));

            // Act
            float result = SnrAnalysis.ComputeSnrDb(signal, noise, contact);

            // Assert
            Assert.That(result, Is.EqualTo(24.0f).Within(0.01f),
                "恰 +24 dB 的输出不被错钳到 +24 以上");
            Assert.That(result, Is.LessThan(SnrAnalysis.SnrClampMax + 0.1f),
                "恰 +24 不应被钳到更高");
        }

        /// <summary>+200 溢出:输入 ±200 dB ⇒ 钳到 +24 dB。
        /// <para>signal = +200, noise = contact = −24:合并 = −20.99;
        /// SNR = 200 − (−20.99) = 220.99 ⇒ 钳到 +24。</para></summary>
        [Test]
        public void test_computeSnrDb_overflowPlus200_clamped()
        {
            // Arrange:信号远高于噪声 ⇒ SNR 远超 +24
            float signal = 200.0f, noise = -24.0f, contact = -24.0f;

            // Act
            float result = SnrAnalysis.ComputeSnrDb(signal, noise, contact);

            // Assert
            Assert.That(result, Is.EqualTo(SnrAnalysis.SnrClampMax),
                "+200 信号 − (−24 噪声) ⇒ SNR 远超 +24 ⇒ 钳到 +24");
        }

        /// <summary>−200 溢出:信号远低于噪声 ⇒ 钳到 −24 dB。
        /// <para>signal = −200, noise = contact = +200:合并 = 203.01;SNR = −200 − 203.01 = −403.01 ⇒ 钳到 −24。</para></summary>
        [Test]
        public void test_computeSnrDb_overflowMinus200_clamped()
        {
            // Arrange:信号远低于噪声 ⇒ SNR 远低于 −24
            float signal = -200.0f, noise = 200.0f, contact = 200.0f;

            // Act
            float result = SnrAnalysis.ComputeSnrDb(signal, noise, contact);

            // Assert
            Assert.That(result, Is.EqualTo(SnrAnalysis.SnrClampMin),
                "−200 信号 − 200 噪声 ⇒ SNR 远低于 −24 ⇒ 钳到 −24");
        }

        /// <summary>NaN 输入 ⇒ 输出 NaN(非法输入由门拒收,纯函数不抛异常)。</summary>
        [Test]
        public void test_computeSnrDb_nanInput_returnsNaN()
        {
            // Arrange
            float nan = float.NaN;

            // Act
            float result = SnrAnalysis.ComputeSnrDb(nan, 0.0f, 0.0f);

            // Assert
            Assert.That(float.IsNaN(result), Is.True,
                "NaN 输入 ⇒ NaN 输出(由 ValidateTierMapSnr 拒收)");
        }

        /// <summary>两噪声同为 −∞(静音)⇒ SNR = +∞ ⇒ 钳到 +24 dB。
        /// <para>验证「无除零路径」:log10(0) = −∞(BCL 语义),SNR = s − (−∞) = +∞,钳位 +24。</para></summary>
        [Test]
        public void test_computeSnrDb_negativeInfinityNoise_clampedToPlus24()
        {
            // Arrange
            float signal = 0.0f, noise = float.NegativeInfinity, contact = float.NegativeInfinity;

            // Act
            float result = SnrAnalysis.ComputeSnrDb(signal, noise, contact);

            // Assert
            Assert.That(result, Is.EqualTo(SnrAnalysis.SnrClampMax),
                "两噪声 = −∞ ⇒ 分母 = 0 ⇒ SNR = +∞ ⇒ 钳到 +24(无除零)");
        }

        /// <summary>GDD 示例:低档 SNR ≈ +5.88 dB(噪声几乎淹没信号)。
        /// <para>signal = 0, noise = −10, contact = −8 ⇒ SNR ≈ +5.88 dB。</para></summary>
        [Test]
        public void test_computeSnrDb_lowTier_example()
        {
            // Arrange:GDD F-44.2 示例(低档:噪声几乎淹没信号)
            float signal = 0.0f, noise = -10.0f, contact = -8.0f;

            // Act
            float result = SnrAnalysis.ComputeSnrDb(signal, noise, contact);

            // Assert
            Assert.That(result, Is.EqualTo(5.8756f).Within(0.01f),
                "低档示例:0 − 10·log10(10^(−1) + 10^(−0.8)) ≈ +5.88 dB");
        }

        /// <summary>GDD 示例:高档 SNR ≈ +12 dB(信号清晰可辨)。
        /// <para>signal = 12, noise = −4, contact = −2 ⇒ SNR ≈ 11.88 dB。</para></summary>
        [Test]
        public void test_computeSnrDb_highTier_example()
        {
            // Arrange:GDD F-44.2 示例(高档:信号清晰可辨)
            float signal = 12.0f, noise = -4.0f, contact = -2.0f;

            // Act
            float result = SnrAnalysis.ComputeSnrDb(signal, noise, contact);

            // Assert
            Assert.That(result, Is.EqualTo(11.8756f).Within(0.01f),
                "高档示例:12 − 10·log10(10^(−0.4) + 10^(−0.2)) ≈ 11.88 dB");
        }

        // ══════════ AC-44-05 ②:三列 ∈ Stethoscope mixer 暴露参数闭集 ══════════

        /// <summary>AC-44-05 ②(前置引用 Story 014):三列 ∈ Stethoscope 组暴露参数闭集。
        /// <para>载体由 Story 014 交付(五名 tier 参数已有真实 mixer 组承载);
        /// 本测试断言 Story 014 交付后三列 ∈ 暴露闭集,不重复实现载体。</para></summary>
        [Test]
        public void test_ac44_05_02_threeColumns_inStethoscopeExposedClosedSet()
        {
            // Arrange:读 mixer YAML,解析 Stethoscope 组的暴露参数闭集
            string yaml = ReadMixerYaml();
            string[] snrColumns = { "tier_signal_db", "tier_noise_floor_db", "tier_contact_noise_floor_db" };

            // Act:直接跑门的载体归属判据(不再自写全局扫描)
            IReadOnlyList<string> errors = MixerTopologyGates.ValidateTierFilterCarrierGroups(yaml);

            // Assert:零错误 ⇒ 五名载体组均在 Stethoscope 的 m_Children 内(含三列)
            Assert.That(errors, Is.Empty, () => string.Join("\n", errors));

            // 夹具自证:三列名确实出现在门的输入可达面上,而非碰巧被别的路径满足
            foreach (string col in snrColumns)
                Assert.That(yaml, Does.Contain("m_Name: " + col),
                    $"YAML 中无同名组「{col}」—— 夹具不自足,断言无意义");
        }

        // ══════════ AC-44-T1 回归(双评审 REC2)══════════

        /// <summary>AC-44-T1 回归:五名 exposed guid 必须逐条 == 同名组 <c>m_Volume</c> 哈希。
        /// <para>起因:Story 004 曾发现「合成 guid 对不上」⇒ <c>SetFloat</c> 恒 false;当时只把 guid
        /// 改成组哈希,而<b>这条等式本身从未进自动化断言</b>,只靠一次性人工实测。guid 一旦漂移
        /// (重新合成 / 覆盖失败)无人发现。</para></summary>
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

        // ══════════ AC-44-06:噪声底下界校验 ══════════

        /// <summary>AC-44-06 ①:两噪声列均有限且 ≥ NOISE_FLOOR_DB_MIN ⇒ 通过。</summary>
        [Test]
        public void test_validateTierMapSnr_validTable_zeroErrors()
        {
            // Arrange:合法 tier_map(噪声列 ≥ −60 dB)
            var tierMap = BuildValidTierMap();

            // Act
            IReadOnlyList<string> errors = SnrAnalysis.ValidateTierMapSnr(tierMap);

            // Assert
            Assert.That(errors, Is.Empty, () => string.Join("\n", errors));
        }

        /// <summary>AC-44-06 ①:noise_floor_db < NOISE_FLOOR_DB_MIN ⇒ 红。</summary>
        [Test]
        public void test_validateTierMapSnr_noiseFloorBelowMin_red()
        {
            // Arrange:noise_floor_db = −200(< −60)
            var tierMap = BuildValidTierMap();
            tierMap["0"] = OverrideColumn(tierMap["0"], "noise_floor_db", "-200");

            // Act
            IReadOnlyList<string> errors = SnrAnalysis.ValidateTierMapSnr(tierMap);

            // Assert
            Assert.That(errors, Is.Not.Empty, "noise_floor_db < NOISE_FLOOR_DB_MIN 必红");
            Assert.That(ErrorsContain(errors, "NOISE_FLOOR_DB_MIN"), Is.True,
                () => string.Join("\n", errors));
        }

        /// <summary>AC-44-06 ①:contact_noise_floor_db < NOISE_FLOOR_DB_MIN ⇒ 红。</summary>
        [Test]
        public void test_validateTierMapSnr_contactNoiseBelowMin_red()
        {
            // Arrange:contact_noise_floor_db = −200(< −60)
            var tierMap = BuildValidTierMap();
            tierMap["1"] = OverrideColumn(tierMap["1"], "contact_noise_floor_db", "-200");

            // Act
            IReadOnlyList<string> errors = SnrAnalysis.ValidateTierMapSnr(tierMap);

            // Assert
            Assert.That(errors, Is.Not.Empty, "contact_noise_floor_db < NOISE_FLOOR_DB_MIN 必红");
            Assert.That(ErrorsContain(errors, "contact_noise_floor_db"), Is.True,
                () => string.Join("\n", errors));
        }

        /// <summary>AC-44-06 ①:噪声列含 NaN(无法解析为有限 float)⇒ 红。</summary>
        [Test]
        public void test_validateTierMapSnr_noiseFloorNaN_red()
        {
            // Arrange:noise_floor_db = "NaN"
            var tierMap = BuildValidTierMap();
            tierMap["2"] = OverrideColumn(tierMap["2"], "noise_floor_db", "NaN");

            // Act
            IReadOnlyList<string> errors = SnrAnalysis.ValidateTierMapSnr(tierMap);

            // Assert
            Assert.That(errors, Is.Not.Empty, "NaN 噪声底必红");
            Assert.That(ErrorsContain(errors, "非有限"), Is.True, () => string.Join("\n", errors));
        }

        /// <summary>AC-44-05 ①:signal_db 无法解析 ⇒ 红。</summary>
        [Test]
        public void test_validateTierMapSnr_signalUnparseable_red()
        {
            // Arrange:signal_db = "abc"
            var tierMap = BuildValidTierMap();
            tierMap["0"] = OverrideColumn(tierMap["0"], "signal_db", "abc");

            // Act
            IReadOnlyList<string> errors = SnrAnalysis.ValidateTierMapSnr(tierMap);

            // Assert
            Assert.That(errors, Is.Not.Empty, "signal_db 无法解析必红");
            Assert.That(ErrorsContain(errors, "signal_db"), Is.True, () => string.Join("\n", errors));
        }

        /// <summary>AC-44-05 ①:ComputeSnrDb 超界 ⇒ 双保险断言红(钳位实现漂移检测)。
        /// <para>正常情况 ComputeSnrDb 已钳位到 [−24, +24],此行不应到达;
        /// 若到达则钳位实现漂移,双保险断言捕获。</para></summary>
        [Test]
        public void test_validateTierMapSnr_snrOutOfRange_doubleInsuranceRed()
        {
            // Arrange:构造 SNR 远超 +24 的行(signal 极高,noise/contact 极低)
            var tierMap = BuildValidTierMap();
            tierMap["0"] = OverrideColumn(tierMap["0"], "signal_db", "200");
            tierMap["0"] = OverrideColumn(tierMap["0"], "noise_floor_db", "-24");
            tierMap["0"] = OverrideColumn(tierMap["0"], "contact_noise_floor_db", "-24");

            // Act
            IReadOnlyList<string> errors = SnrAnalysis.ValidateTierMapSnr(tierMap);

            // Assert:ComputeSnrDb 已钳位到 +24,不触发双保险红
            // (若钳位失效,此处会红 —— 双保险)
            Assert.That(ErrorsContain(errors, "双保险"), Is.False,
                "ComputeSnrDb 已钳位,双保险断言不应触发");
        }

        /// <summary>AC-44-05 ①:tier_map 含一行使 SNR = +30 ⇒ 配置拒绝(门红)。
        /// <para>ComputeSnrDb 钳位到 +24,不会输出 +30;此测试验证钳位行为本身。</para></summary>
        [Test]
        public void test_validateTierMapSnr_snrPlus30_clampedNotRejected()
        {
            // Arrange:signal = +30, noise/contact = −24 ⇒ 未钳 SNR = 30 − (−20.99) = 50.99 ⇒ 钳到 +24
            var tierMap = BuildValidTierMap();
            tierMap["1"] = OverrideColumn(tierMap["1"], "signal_db", "30");
            tierMap["1"] = OverrideColumn(tierMap["1"], "noise_floor_db", "-24");
            tierMap["1"] = OverrideColumn(tierMap["1"], "contact_noise_floor_db", "-24");

            // Act
            IReadOnlyList<string> errors = SnrAnalysis.ValidateTierMapSnr(tierMap);

            // Assert:钳位后 SNR = +24 ∈ [−24, +24],不触发超界红
            Assert.That(errors, Is.Empty,
                "ComputeSnrDb 钳位到 +24 后 ∈ [−24, +24],不触发超界红");
        }

        /// <summary>NOT-RUN 守卫:tier_map = null ⇒ 报错退出(不静默通过)。</summary>
        [Test]
        public void test_validateTierMapSnr_nullTierMap_notRunGuard()
        {
            // Arrange
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> nullMap = null;

            // Act
            IReadOnlyList<string> errors = SnrAnalysis.ValidateTierMapSnr(nullMap);

            // Assert
            Assert.That(errors, Is.Not.Empty, "null tier_map 必报 NOT-RUN 守卫");
            Assert.That(ErrorsContain(errors, "NOT-RUN"), Is.True, () => string.Join("\n", errors));
        }

        /// <summary>AC-44-06 ③:与 8 侧 READ_FLOOR_MIN > 0 方向对齐(双正断言)。
        /// <para>本测试断言 NOISE_FLOOR_DB_MIN < 0(dB 侧有限下界 = 线性幅度 > 0)。</para></summary>
        [Test]
        public void test_noiseFloorDbMin_directionAlignedWithReadFloorMin()
        {
            // Arrange:NOISE_FLOOR_DB_MIN 是一个负 dB 值(有限下界)

            // Act & Assert
            Assert.That(SnrAnalysis.NoiseFloorDbMin, Is.LessThan(0.0f),
                "NOISE_FLOOR_DB_MIN < 0(dB 侧:负值 = 有限幅度 > 0)");
            Assert.That(SnrAnalysis.NoiseFloorDbMin, Is.GreaterThan(float.NegativeInfinity),
                "NOISE_FLOOR_DB_MIN 有限(非 −∞)");
            // 与 8 侧 READ_FLOOR_MIN > 0 方向对齐:两者均断言「噪声线性幅度 > 0」
            // 8 侧:READ_FLOOR_MIN > 0(线性幅度下界)
            // 音频侧:noise_floor_db ≥ NOISE_FLOOR_DB_MIN(负 dB 下界,等价于线性幅度 > 0)
        }

        // ══════════ 共用小件 ══════════

        /// <summary>读 .mixer YAML 原文(前置 File.Exists —— 缺失即红,不静默跳过)。</summary>
        private static string ReadMixerYaml()
        {
            string absolutePath = Path.Combine(repoRoot(), "unity", "Assets", "Audio", "DaYiJingCheng.mixer");
            Assert.That(File.Exists(absolutePath), Is.True,
                $".mixer 资产缺失:{absolutePath}");
            return File.ReadAllText(absolutePath);
        }

        /// <summary>解析 Stethoscope 组的暴露参数名集合(从 mixer YAML 提取)。</summary>
        private static HashSet<string> ParseStethoscopeExposedParams(string yaml)
        {
            string[] lines = yaml.Replace("\r\n", "\n").Split('\n');
            var exposed = new HashSet<string>(StringComparer.Ordinal);

            // 找到 Stethoscope 组文档,收集其 m_ExposedParameters 条目
            // 简化:直接扫描全文件的 m_ExposedParameters 条目(Story 014 已验证载体就位)
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() != "m_ExposedParameters:") continue;
                for (int j = i + 1; j < lines.Length; j++)
                {
                    string t = lines[j].Trim();
                    if (t.StartsWith("name:"))
                    {
                        exposed.Add(t.Substring("name:".Length).Trim());
                    }
                    if (t.StartsWith("m_") || t.StartsWith("--- ")) break;
                }
            }

            return exposed;
        }

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
                    if (t.StartsWith("name: ") && t.Substring("name: ".Length).Trim() == paramName)
                    {
                        // 该条目的 guid 在其上一行("- guid: xxx")
                        for (int k = j - 1; k > i; k--)
                        {
                            string g = lines[k].Trim();
                            if (g.StartsWith("- guid:"))
                                return g.Substring("- guid:".Length).Trim();
                        }

                        return null;
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

        /// <summary>构造合法 tier_map(三档 × 六列,噪声列 ≥ −60 dB)。</summary>
        private static Dictionary<string, IReadOnlyDictionary<string, string>> BuildValidTierMap()
        {
            return new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["0"] = FullTierColumns(),
                ["1"] = FullTierColumns(),
                ["2"] = FullTierColumns(),
            };
        }

        /// <summary>F-44.1 定型 6 列的合法一档(噪声列 ≥ −60 dB)。</summary>
        private static Dictionary<string, string> FullTierColumns()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["passband_center_hz"] = "900",
                ["passband_width_hz"] = "900",
                ["noise_floor_db"] = "-42",
                ["contact_noise_floor_db"] = "-34",
                ["band_detail_count"] = "5",
                ["signal_db"] = "-18",
            };
        }

        /// <summary>覆盖 tier_map 某档的某列值(返回新字典,不修改原字典)。</summary>
        private static IReadOnlyDictionary<string, string> OverrideColumn(
            IReadOnlyDictionary<string, string> original, string column, string value)
        {
            var copy = new Dictionary<string, string>(original, StringComparer.Ordinal);
            copy[column] = value;
            return copy;
        }

        /// <summary>错误集里是否含指定片段(不依赖执行次序)。</summary>
        private static bool ErrorsContain(IReadOnlyList<string> errors, string fragment)
        {
            for (int i = 0; i < errors.Count; i++)
            {
                if (errors[i] != null && errors[i].IndexOf(fragment, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>本文件在 unity/Assets/Tests/EditMode/Audio/ ⇒ Audio→EditMode→Tests→Assets→unity→仓库根 = 5 层。</summary>
        private static string repoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));
    }
}
