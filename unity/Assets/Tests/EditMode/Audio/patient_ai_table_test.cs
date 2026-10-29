// 权威来源:`docs/tech-debt-register.md` `TD-audio-032` ①(调参常量入生产)
//   · ADR-014 §二/§三/§五(两阶段烘焙 · 白名单 / FixParse · ConfigVersion = 内容哈希)
//   · `coding-standards.md`「gameplay 值不 hardcode / data-driven」
//   · GDD `patient-ai.md` §Tuning 一 / 三(两族硬约束)
//
// ⚠️ **本夹具钉五件事**:
//   ① 真种子表(`assets/data/ai_patient.json`)过门 —— 零错误;
//   ② binder 的**白名单 / 固定键**真会拒(未知键 / 缺节 / Fix 写成浮点字面量);
//   ③ **两族硬约束**在烘焙期真的会红(不是只在运行期 Validate 里);
//   ④ cooked 写方 ↔ 读方**严格镜像**(往返后值不变 —— 含 float32 的精确往返);
//   ⑤ 生产装配点**不再取代码常量**(源文本判据:CompositionRoot 不再出现
//      `BehaviorBands.Default` / 不传 `intervals` 为 null)。
//
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖 · 零写盘。

using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    /// <summary>`TD-audio-032` ①:13 调参常量表走 ADR-014 烘焙管线。</summary>
    [TestFixture]
    public sealed class PatientAiTableTest
    {
        // ══════════════ ① 真种子表过门 ══════════════

        [Test]
        public void test_realSeedTable_zeroErrors()
        {
            string path = Path.Combine(RepoRoot(), "assets", "data", "ai_patient.json");
            Assert.That(File.Exists(path), Is.True, $"真种子缺失:{path}");

            PatientAiBinder.BindResult bound = PatientAiBinder.Bind(File.ReadAllText(path));

            Assert.That(bound.CueIntervals.Severe, Is.EqualTo(40));
            Assert.That(bound.CueIntervals.Mild, Is.EqualTo(90));
            Assert.That(bound.CueIntervals.Recover, Is.EqualTo(120));
            Assert.That(bound.SchemaVersion, Is.EqualTo(1u));
        }

        /// <summary>真种子表的 cue 间隔 = **现有代码常量的照抄**(整数域,无损)。
        /// <para>⚠️ 本条是「没改数值」的判据 —— 停用它,「顺手调了数」就无人察觉。</para></summary>
        [Test]
        public void test_realSeedTable_cueIntervalsMatchLegacyConstants()
        {
            string path = Path.Combine(RepoRoot(), "assets", "data", "ai_patient.json");
            PatientAiBinder.BindResult bound = PatientAiBinder.Bind(File.ReadAllText(path));

            CueIntervals legacy = CueIntervals.Default;

            Assert.That(bound.CueIntervals.Severe, Is.EqualTo(legacy.Severe), "severe 须照抄");
            Assert.That(bound.CueIntervals.Mild, Is.EqualTo(legacy.Mild), "mild 须照抄");
            Assert.That(bound.CueIntervals.Recover, Is.EqualTo(legacy.Recover), "recover 须照抄");
        }

        /// <summary>行为分档阈值 = 现有常量经**定点化**后的值(不是 float 字面量本身)。
        /// <para>⚠️ **为什么不能断言「等于 `BehaviorBands.Default` 的 float」**:GDD `patient-ai.md`
        /// 规则五明令「`ai_patient.json` 里**禁 `float` 字段**,非空间量写 `int` 或定点串」
        /// ⇒ 阈值必经 `FixParse` → Q16.16,而 `1/5` 在 Q16.16 里**不可精确表示**
        /// (离线复算:`1/5 × 65536 = 13107.2` ⇒ 13107 ⇒ `0.199996948`)。
        /// 这是**定点域的本质**,不是缺陷;`collapse_min = 3/4` 则精确(`49152/65536 = 0.75`)。
        /// ⇒ 本条断言**定点化的预期值**(字面量写在断言里,不从被测对象派生)。</para></summary>
        [Test]
        public void test_realSeedTable_behaviorBandsAreFixedPointQuantized()
        {
            string path = Path.Combine(RepoRoot(), "assets", "data", "ai_patient.json");
            PatientAiBinder.BindResult bound = PatientAiBinder.Bind(File.ReadAllText(path));

            // 期望值 = 源分数 × 65536 按 **ROUND_HALF_AWAY_FROM_ZERO**(ADR-006 §三)取整后回读。
            // ⚠️ 不是截断:`1/10 × 65536 = 6553.6` ⇒ `.6` 进位 ⇒ 6554 ⇒ `0.100006104`;
            //    而 `1/5 × 65536 = 13107.2` ⇒ 小数 `.2` 不进 ⇒ 13107 ⇒ `0.199996948`。
            //    首版我按截断写断言 ⇒ `recover_max` / `hyst` 两条实跑红(离线复算已修正)。
            Assert.That(bound.BehaviorBands.MildMin, Is.EqualTo(0.199996948f), "mild_min = 1/5 的 Q16.16");
            Assert.That(bound.BehaviorBands.SeekMin, Is.EqualTo(0.449996948f), "seek_min = 9/20 的 Q16.16");
            Assert.That(bound.BehaviorBands.CollapseMin, Is.EqualTo(0.75f), "collapse_min = 3/4 **精确**");
            Assert.That(bound.BehaviorBands.RecoverMax, Is.EqualTo(0.100006104f), "recover_max = 1/10 的 Q16.16(进位)");
            Assert.That(bound.BehaviorBands.Hyst, Is.EqualTo(0.100006104f), "hyst = 1/10 的 Q16.16(进位)");
            Assert.That(bound.BehaviorBands.DeathBandMin, Is.EqualTo(0.899993896f), "death_band_min = 9/10 的 Q16.16");
        }

        /// <summary>量化差**必须小于 1/65536**(否则说明定点化路径算错了,不是「精度损失」)。
        /// <para>本条钉的是「定点化的**量级**正确」—— 若有人把换算写成 ×65535 或 ×2^20,
        /// 上面逐值断言会全红,但本条额外给出**为什么会红**的判据。</para></summary>
        [Test]
        public void test_behaviorBands_quantizationWithinOneQ1616Step()
        {
            string path = Path.Combine(RepoRoot(), "assets", "data", "ai_patient.json");
            PatientAiBinder.BindResult bound = PatientAiBinder.Bind(File.ReadAllText(path));

            const float Step = 1f / 65536f;
            BehaviorBands legacy = BehaviorBands.Default;

            Assert.That(System.Math.Abs(bound.BehaviorBands.MildMin - legacy.MildMin), Is.LessThanOrEqualTo(Step));
            Assert.That(System.Math.Abs(bound.BehaviorBands.SeekMin - legacy.SeekMin), Is.LessThanOrEqualTo(Step));
            Assert.That(System.Math.Abs(bound.BehaviorBands.CollapseMin - legacy.CollapseMin), Is.LessThanOrEqualTo(Step));
            Assert.That(System.Math.Abs(bound.BehaviorBands.RecoverMax - legacy.RecoverMax), Is.LessThanOrEqualTo(Step));
            Assert.That(System.Math.Abs(bound.BehaviorBands.Hyst - legacy.Hyst), Is.LessThanOrEqualTo(Step));
            Assert.That(System.Math.Abs(bound.BehaviorBands.DeathBandMin - legacy.DeathBandMin), Is.LessThanOrEqualTo(Step));
        }

        // ══════════════ ② 白名单 / 固定键 ══════════════

        [Test]
        public void test_unknownRootKey_reportsRed()
        {
            string json = MinimalTable().Replace("\"schema_version\": 1,",
                "\"schema_version\": 1, \"typo_key\": 1,");

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() => PatientAiBinder.Bind(json));
            Assert.That(string.Join("\n", ex.Errors), Does.Contain("typo_key"));
            Assert.That(string.Join("\n", ex.Errors), Does.Contain("unknown-key"));
        }

        [Test]
        public void test_missingCueIntervals_reportsRed()
        {
            string json = "{ \"schema_version\": 1, \"behavior_bands\": { " +
                          "\"mild_min\": \"1/5\", \"seek_min\": \"9/20\", \"collapse_min\": \"3/4\", " +
                          "\"recover_max\": \"1/10\", \"hyst\": \"1/10\", \"death_band_min\": \"9/10\" } }";

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() => PatientAiBinder.Bind(json));
            Assert.That(string.Join("\n", ex.Errors), Does.Contain("cue_intervals"));
        }

        /// <summary>Fix 字段写**浮点字面量** ⇒ 拒(ADR-014 §三:须写分子/分母)。
        /// <para>⚠️ 实测踩过:首版 JSON 写 `"0.20"`,烘焙期 6 条红。</para></summary>
        [Test]
        public void test_floatLiteralInFixField_reportsRed()
        {
            string json = MinimalTable().Replace("\"mild_min\": \"1/5\"", "\"mild_min\": \"0.20\"");

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() => PatientAiBinder.Bind(json));
            string joined = string.Join("\n", ex.Errors);
            Assert.That(joined, Does.Contain("mild_min"));
            Assert.That(joined, Does.Contain("fix-string"));
        }

        /// <summary>Fix 字段写成 **JSON 数字**(而非字符串)⇒ 拒。
        /// <para>⚠️ 本条与上一条**打的是不同分支**:上一条测「字符串里的浮点字面量被 `FixParse` 拒」,
        /// 本条测「JSON 数字在**类型检查**就被拒」(根本没走到 `FixParse`)。
        /// 首轮证伪 M3(停用类型检查)假存活,根因 = 只有上一条 ⇒ **靶场里没有靶**。</para></summary>
        [Test]
        public void test_jsonNumberInFixField_reportsRed()
        {
            string json = MinimalTable().Replace("\"mild_min\": \"1/5\"", "\"mild_min\": 0.2");

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() => PatientAiBinder.Bind(json));
            string joined = string.Join("\n", ex.Errors);
            Assert.That(joined, Does.Contain("mild_min"));
            Assert.That(joined, Does.Contain("须为字符串 Fix 字面量"), joined);
        }

        /// <summary>整数字段写成**字符串** ⇒ 拒(它们是 tick 计数,不是 Fix)。</summary>
        [Test]
        public void test_stringInIntegerField_reportsRed()
        {
            string json = MinimalTable().Replace("\"severe\": 40", "\"severe\": \"40\"");

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() => PatientAiBinder.Bind(json));
            Assert.That(string.Join("\n", ex.Errors), Does.Contain("severe"));
        }

        // ══════════════ ③ 两族硬约束在烘焙期真的会红 ══════════════

        /// <summary>cue 间隔:`severe ≥ mild` ⇒ 红(「重档比轻档疏」方向不可辨)。</summary>
        [Test]
        public void test_cueIntervals_severeNotLessThanMild_reportsRed()
        {
            string json = MinimalTable().Replace("\"severe\": 40, \"mild\": 90", "\"severe\": 100, \"mild\": 90");

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() => PatientAiBinder.Bind(json));
            Assert.That(string.Join("\n", ex.Errors), Does.Contain("F-13.5"));
        }

        /// <summary>行为分档:单调链断裂(`mild_min ≥ seek_min`)⇒ 红。</summary>
        [Test]
        public void test_behaviorBands_monotonicChainBroken_reportsRed()
        {
            string json = MinimalTable().Replace("\"mild_min\": \"1/5\"", "\"mild_min\": \"1/2\"");

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() => PatientAiBinder.Bind(json));
            Assert.That(string.Join("\n", ex.Errors), Does.Contain("F-13.1"));
        }

        /// <summary>行为分档:滞回 ≥ seek_min ⇒ 红(Seeking 永不转出,病人赖在门口)。</summary>
        [Test]
        public void test_behaviorBands_hystNotLessThanSeekMin_reportsRed()
        {
            string json = MinimalTable().Replace("\"hyst\": \"1/10\"", "\"hyst\": \"1/2\"");

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() => PatientAiBinder.Bind(json));
            Assert.That(string.Join("\n", ex.Errors), Does.Contain("HYST"));
        }

        // ══════════════ ④ 写方 ↔ 读方严格镜像 ══════════════

        /// <summary>往返:烘焙 → 解码 ⇒ 两族值**逐字段不变**。
        /// <para>⚠️ 含 float32 的精确往返判据 —— 若有人把阈值改回 Q16.16 raw 落盘,
        /// `0.45` 会量化成 `0.4499969…`,本条即红。</para></summary>
        [Test]
        public void test_cookedRoundTrip_valuesUnchanged()
        {
            string path = Path.Combine(RepoRoot(), "assets", "data", "ai_patient.json");
            PatientAiBinder.BindResult bound = PatientAiBinder.Bind(File.ReadAllText(path));

            byte[] cooked = PatientAiCookedWriter.Write(bound, configVersion: 0xDEADBEEF);
            PatientAiDataSet decoded = PatientAiCookedCodec.Read(cooked);

            Assert.That(decoded.CueIntervals.Severe, Is.EqualTo(bound.CueIntervals.Severe));
            Assert.That(decoded.CueIntervals.Mild, Is.EqualTo(bound.CueIntervals.Mild));
            Assert.That(decoded.CueIntervals.Recover, Is.EqualTo(bound.CueIntervals.Recover));

            Assert.That(decoded.BehaviorBands.MildMin, Is.EqualTo(bound.BehaviorBands.MildMin));
            Assert.That(decoded.BehaviorBands.SeekMin, Is.EqualTo(bound.BehaviorBands.SeekMin));
            Assert.That(decoded.BehaviorBands.CollapseMin, Is.EqualTo(bound.BehaviorBands.CollapseMin));
            Assert.That(decoded.BehaviorBands.RecoverMax, Is.EqualTo(bound.BehaviorBands.RecoverMax));
            Assert.That(decoded.BehaviorBands.Hyst, Is.EqualTo(bound.BehaviorBands.Hyst));
            Assert.That(decoded.BehaviorBands.DeathBandMin, Is.EqualTo(bound.BehaviorBands.DeathBandMin));

            Assert.That(decoded.ConfigVersion, Is.EqualTo(0xDEADBEEFu));
            Assert.That(decoded.SchemaVersion, Is.EqualTo(bound.SchemaVersion));
        }

        /// <summary>产物长度 = 20 头 + 36 载荷(写方与读方常量须一致)。</summary>
        [Test]
        public void test_cookedLength_matchesDeclaredPayload()
        {
            string path = Path.Combine(RepoRoot(), "assets", "data", "ai_patient.json");
            PatientAiBinder.BindResult bound = PatientAiBinder.Bind(File.ReadAllText(path));

            byte[] cooked = PatientAiCookedWriter.Write(bound, configVersion: 1);

            Assert.That(cooked.Length, Is.EqualTo(CookedFormat.HeaderSize
                                                  + PatientAiCookedCodec.PayloadBytes),
                "写方产出的长度须 == 头 + 读方声明的载荷长(两处常量不一致即红)");
        }

        /// <summary>载荷被**截断** ⇒ 读方拒收(E-13)。
        /// <para>⚠️ 本条与上一条**打的是不同分支**:上一条测「写方产出的长度 == 声明常量」,
        /// 本条测「读方对**不合法载荷长度**的拒收」。首轮证伪 M8(停用长度校验)假存活,
        /// 根因 = 只有上一条(合法字节删掉校验照样读得通)⇒ **靶场里没有靶**。</para></summary>
        [Test]
        public void test_cookedTruncatedPayload_reportsRed()
        {
            string path = Path.Combine(RepoRoot(), "assets", "data", "ai_patient.json");
            PatientAiBinder.BindResult bound = PatientAiBinder.Bind(File.ReadAllText(path));
            byte[] cooked = PatientAiCookedWriter.Write(bound, configVersion: 1);

            // 砍掉载荷最后 4 字节(头部声明的 payloadLength 不变 ⇒ 长度校验须开火)
            var truncated = new byte[cooked.Length - 4];
            System.Array.Copy(cooked, truncated, truncated.Length);

            Assert.Throws<System.InvalidOperationException>(() => PatientAiCookedCodec.Read(truncated));
        }

        /// <summary>载荷被**加长** ⇒ 读方拒收(同上,反向)。</summary>
        [Test]
        public void test_cookedOversizedPayload_reportsRed()
        {
            string path = Path.Combine(RepoRoot(), "assets", "data", "ai_patient.json");
            PatientAiBinder.BindResult bound = PatientAiBinder.Bind(File.ReadAllText(path));
            byte[] cooked = PatientAiCookedWriter.Write(bound, configVersion: 1);

            var oversized = new byte[cooked.Length + 4];
            System.Array.Copy(cooked, oversized, cooked.Length);

            Assert.Throws<System.InvalidOperationException>(() => PatientAiCookedCodec.Read(oversized));
        }

        /// <summary>**头与载荷自洽、但载荷长 ≠ 36** ⇒ `PayloadBytes` 判据开火。
        /// <para>⚠️ **为什么需要这一条(实测踩过)**:首轮证伪 M8(停用 `PayloadBytes` 判据)假存活。
        /// 根因 = 读方有**两条长度判据**且只要头声明的是 36 就**完全等价**:
        /// ① 头校验 `payloadLength != bytes.Length - HeaderSize`;
        /// ② 载荷判据 `bytes.Length - pos != PayloadBytes`。
        /// 上面两条「截断 / 加长」夹具砍掉字节后**头声明的长度不变** ⇒ 判据 ① 先开火,
        /// 判据 ② 删掉也照样抛 ⇒ 假绿。本条造一份**头声明 40、实得 40** 的字节:
        /// 判据 ① 通过(自洽),判据 ② 开火(40 ≠ 36)。</para>
        /// <remarks>构法:写方产物(20 头 + 36 载荷)⇒ 改头的 payloadLength 为 40 ⇒ 尾部补 4 字节。</remarks></summary>
        [Test]
        public void test_cookedSelfConsistentButWrongLength_reportsRed()
        {
            string path = Path.Combine(RepoRoot(), "assets", "data", "ai_patient.json");
            PatientAiBinder.BindResult bound = PatientAiBinder.Bind(File.ReadAllText(path));
            byte[] cooked = PatientAiCookedWriter.Write(bound, configVersion: 1);

            int headerSize = CookedFormat.HeaderSize;
            int newPayload = PatientAiCookedCodec.PayloadBytes + 4;   // 40
            var forged = new byte[headerSize + newPayload];
            System.Array.Copy(cooked, forged, cooked.Length);
            // 头 offset 16 = payloadLength(u32 LE)⇒ 改成 40,与实得长度自洽
            forged[16] = (byte)(newPayload & 0xFF);
            forged[17] = (byte)((newPayload >> 8) & 0xFF);
            forged[18] = (byte)((newPayload >> 16) & 0xFF);
            forged[19] = (byte)((newPayload >> 24) & 0xFF);

            Assert.Throws<System.InvalidOperationException>(() => PatientAiCookedCodec.Read(forged));
        }

        /// <summary>版本不匹配 ⇒ 硬失败(E-13,绝不降级)。</summary>
        [Test]
        public void test_cookedSchemaVersionMismatch_reportsRed()
        {
            string path = Path.Combine(RepoRoot(), "assets", "data", "ai_patient.json");
            PatientAiBinder.BindResult bound = PatientAiBinder.Bind(File.ReadAllText(path));
            byte[] cooked = PatientAiCookedWriter.Write(bound, configVersion: 1);
            // 篡改头里的 schema_version(offset 8)为 2
            cooked[8] = 2;

            Assert.Throws<System.InvalidOperationException>(() => PatientAiCookedCodec.Read(cooked));
        }

        // ══════════════ ⑤ 生产装配点不再取代码常量(源文本判据)══════════════

        /// <summary>`CompositionRoot` 不得再出现 `BehaviorBands.Default` / `CueIntervals.Default`
        /// 作为**传给生产对象**的实参。
        /// <para>⚠️ 判据 = 源文本:本项是「接线真的改了」的唯一机械证据(与
        /// `test_bootRoot_appliesLattice_beforeAssemble` 同款「论证而非观测」口径 —— 如实登记)。</para></summary>
        [Test]
        public void test_compositionRoot_noLegacyDefaultConstants()
        {
            string path = Path.Combine(RepoRoot(), "unity", "Assets", "Gameplay.Boot", "CompositionRoot.cs");
            Assert.That(File.Exists(path), Is.True, $"CompositionRoot 缺失:{path}");
            string src = StripComments(File.ReadAllText(path));

            Assert.That(src, Does.Not.Contain("BehaviorBands.Default"),
                "装配层不得再取 BehaviorBands.Default(调参须来自烘焙产物)");
            Assert.That(src, Does.Not.Contain("CueIntervals.Default"),
                "装配层不得再取 CueIntervals.Default(调参须来自烘焙产物)");
            Assert.That(src, Does.Contain("DiagnosisCookedDisk.LoadAiPatient"),
                "装配层须有「无 provider 时直读产物」的兜底路径");
            Assert.That(src, Does.Contain("aiPatientTable.BehaviorBands"),
                "装配层须把烘焙产物喂给行为导演");
            Assert.That(src, Does.Contain("intervals: aiPatientTable.CueIntervals"),
                "装配层须把烘焙产物喂给 cue 驱动");
        }

        /// <summary>剥掉行注释与块注释(**保留字符串字面量**)。
        /// <para>⚠️ 首版判据没剥注释 ⇒ 因 `CompositionRoot` 的**历史注释**里提到
        /// `BehaviorBands.Default` 而假红(实跑踩过)。判据须只扫**可执行文本**。</para></summary>
        private static string StripComments(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            bool inLine = false, inBlock = false, inStr = false, inChar = false;

            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char next = i + 1 < src.Length ? src[i + 1] : '\0';

                if (inLine)
                {
                    if (c == '\n') { inLine = false; sb.Append(c); }
                    continue;
                }
                if (inBlock)
                {
                    if (c == '*' && next == '/') { inBlock = false; i++; }
                    else if (c == '\n') sb.Append(c);
                    continue;
                }
                if (inStr)
                {
                    sb.Append(c);
                    if (c == '\\' && next != '\0') { sb.Append(next); i++; }
                    else if (c == '"') inStr = false;
                    continue;
                }
                if (inChar)
                {
                    sb.Append(c);
                    if (c == '\\' && next != '\0') { sb.Append(next); i++; }
                    else if (c == '\'') inChar = false;
                    continue;
                }

                if (c == '/' && next == '/') { inLine = true; i++; continue; }
                if (c == '/' && next == '*') { inBlock = true; i++; continue; }
                if (c == '"') { inStr = true; sb.Append(c); continue; }
                if (c == '\'') { inChar = true; sb.Append(c); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        // ══════════════ 工具 ══════════════

        /// <summary>合法最小表(每测自行注入一处变异)。</summary>
        private static string MinimalTable() =>
            "{\n" +
            "  \"schema_version\": 1,\n" +
            "  \"cue_intervals\": { \"severe\": 40, \"mild\": 90, \"recover\": 120 },\n" +
            "  \"behavior_bands\": {\n" +
            "    \"mild_min\": \"1/5\", \"seek_min\": \"9/20\", \"collapse_min\": \"3/4\",\n" +
            "    \"recover_max\": \"1/10\", \"hyst\": \"1/10\", \"death_band_min\": \"9/10\"\n" +
            "  }\n" +
            "}";

        /// <summary>仓根(自本源文件向上找含 `assets/data` 的目录)。</summary>
        private static string RepoRoot()
        {
            string dir = System.IO.Path.GetDirectoryName(ThisFilePath());
            for (int i = 0; i < 8 && dir != null; i++)
            {
                if (Directory.Exists(System.IO.Path.Combine(dir, "assets", "data"))) return dir;
                dir = System.IO.Path.GetDirectoryName(dir);
            }
            Assert.Fail("找不到仓根(含 assets/data 的目录)");
            return null;
        }

        private static string ThisFilePath(
            [System.Runtime.CompilerServices.CallerFilePath] string path = null) => path;
    }
}
