// PRE-S18-A 部分兑现 —— R2 战伤白名单五项的**真数据行**判据(2026-10-22 立)。
//
// ══════════════════════════════════════════════════════════════════════════
// 本文件要证明的四件事(缺一即「数据行已建」不成立)
// ══════════════════════════════════════════════════════════════════════════
//   ① **真文件存在且五行齐** —— `assets/data/disease_registry.json` 里恰有
//      白名单五项(`death-and-respawn.md:96` 的闭集),不是空文件 / 少行。
//   ② **五行真能过 C# 校验器** —— 逐行绑成 `DiseaseRegistryEntry` 后调
//      `RegistrySchemaValidator.Validate`,**零违例**。
//      ⚠️ 这一条不能用「我写了个 Python 脚本跑过」代替 —— 那是判据的复刻,
//      不是判据本身。本项目的假绿形态之一正是「测试跑自造断言」。
//   ③ **五项可达死** —— `A_peak/SCALE ≥ DEATH_THRESHOLD`(校验 4b 的语义),
//      用**真常量** `StateMachine.DEATH_THRESHOLD` 算,不是抄一个数。
//   ④ **判据会红** —— 注入违例(改一个值 / 删一行 / 缺 trauma_half_life),
//      要求**各自**被对应规则号抓到。没有这一条,「零违例」与「没真跑校验」无法区分。
//
// ⚠️ 本文件**只读**仓库文件(不写盘)⇒ 不触发 `FsWriteBoundaryGate` 的 R1/R2。
//
// 权威:`design/gdd/disease-simulation.md` §R2 / §R1.3 / §二之一 ·
//      `death-and-respawn.md:96`(白名单闭集)· `OQ-25-7`(trauma_half_life)·
//      `unity/Assets/Sim/RegistrySchema.cs`(被测校验器)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>R2 战伤白名单五项数据行 —— 存在性 + 真校验器 + 可达死 + 判据会红。</summary>
    [TestFixture]
    public sealed class DiseaseRegistryDataTest
    {
        /// <summary>白名单闭集(<c>death-and-respawn.md:96</c> —— <c>lethal(d) ∧ d ∈ 五项</c>)。</summary>
        private static readonly string[] Whitelist =
        {
            "INJ_STAB", "INJ_LACERATION", "INJ_HEMORRHAGE", "INJ_CLAW", "INJ_BITE",
        };

        private static string RepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        private static string DataPath =>
            Path.Combine(RepoRoot(), "assets", "data", "disease_registry.json");

        // ══════════════════════════════════════════════════════════════════════
        // 极简 JSON 读取 —— 只为把真文件读成 DTO。
        // ⚠️ 刻意**不用** Newtonsoft / 任何既有 binder:本测试要验的是「文件内容
        //    能过校验器」,不是「某个 binder 能解析它」。binder 是另一个交付物,
        //    它若不存在,本测试不该因此变红(那是把两件事绑死)。
        //    真实装载走 ADR-014 阶段 2 的 `DiseaseRegistryBinder`(尚未建)。
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>从作者态 JSON 原文抽出五个伤情行,绑成 schema 对象。</summary>
        private static List<DiseaseRegistryEntry> LoadRows(string json)
        {
            var rows = new List<DiseaseRegistryEntry>();

            // 按 `"key": "INJ_..."` 切行 —— 每行是一个 `{ ... }` 块。
            int cursor = 0;
            while (true)
            {
                int keyAt = json.IndexOf("\"key\":", cursor, StringComparison.Ordinal);
                if (keyAt < 0) break;

                // 该行对象的起止:向前找最近的 `{`,向后按花括号配平找匹配的 `}`
                int start = json.LastIndexOf('{', keyAt);
                int depth = 0, end = start;
                for (int i = start; i < json.Length; i++)
                {
                    if (json[i] == '{') depth++;
                    else if (json[i] == '}') { depth--; if (depth == 0) { end = i; break; } }
                }

                string block = json.Substring(start, end - start + 1);
                cursor = end + 1;

                if (!block.Contains("\"kind\": \"injury\"", StringComparison.Ordinal)) continue;
                rows.Add(BindInjuryRow(block));
            }

            return rows;
        }

        /// <summary>把一个 injury 行绑成 <see cref="DiseaseRegistryEntry"/>。</summary>
        private static DiseaseRegistryEntry BindInjuryRow(string block)
        {
            var e = new DiseaseRegistryEntry
            {
                DiseaseId = (int)Num(block, "id"),
                DiseaseKey = Str(block, "key"),
                Kind = DiseaseKind.Injury,
                Severity = (int)Num(block, "severity"),
                Lethality = block.Contains("\"lethal\": true", StringComparison.Ordinal) ? 2 : 0,
                Contagion = 0,
                Polarity = 2,                       // 平(伤情无寒热)
                TreatmentDifficulty = (int)Num(block, "treatment_difficulty", 5),
                RecoveryTime = (int)Num(block, "recovery_time", 1),
                Scale = FixParse.Parse(Str(block, "scale")),
                TraumaHalfLife = (int)Num(block, "trauma_half_life"),
            };

            // natural_progress 子块
            int npAt = block.IndexOf("\"natural_progress\":", StringComparison.Ordinal);
            Assert.That(npAt, Is.GreaterThanOrEqualTo(0), $"{e.DiseaseKey} 缺 natural_progress");
            int npStart = block.IndexOf('{', npAt);
            int depth = 0, npEnd = npStart;
            for (int i = npStart; i < block.Length; i++)
            {
                if (block[i] == '{') depth++;
                else if (block[i] == '}') { depth--; if (depth == 0) { npEnd = i; break; } }
            }
            string np = block.Substring(npStart, npEnd - npStart + 1);

            e.Curve = new NaturalProgressCurve
            {
                Incubation = (int)Num(np, "incubation"),
                APeak = FixParse.Parse(Str(np, "a_peak")),
                TauRise = FixParse.Parse(Str(np, "tau_rise")),
                TauPeak = (int)Num(np, "tau_peak"),
                TauFall = FixParse.Parse(Str(np, "tau_fall")),
                Plateau = np.Contains("\"plateau\": true", StringComparison.Ordinal),
                SelfLimit = np.Contains("\"self_limit\": true", StringComparison.Ordinal),
                Sigma = null,
                BoundaryMode = Str(np, "boundary_mode"),
            };

            return e;
        }

        /// <summary>取 `"name": <数字>` 的值(逗号 / 行尾均可)。</summary>
        private static double Num(string s, string name, double fallback = double.NaN)
        {
            string token = "\"" + name + "\":";
            int at = s.IndexOf(token, StringComparison.Ordinal);
            if (at < 0)
            {
                Assert.That(double.IsNaN(fallback), Is.False, $"缺字段 `{name}`");
                return fallback;
            }
            int i = at + token.Length;
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            int j = i;
            while (j < s.Length && (char.IsDigit(s[j]) || s[j] == '-' || s[j] == '+' || s[j] == '.')) j++;
            return double.Parse(s.Substring(i, j - i), System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>取 `"name": "字符串"` 的值。</summary>
        private static string Str(string s, string name)
        {
            string token = "\"" + name + "\":";
            int at = s.IndexOf(token, StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), $"缺字段 `{name}`");
            int q1 = s.IndexOf('"', at + token.Length);
            int q2 = s.IndexOf('"', q1 + 1);
            return s.Substring(q1 + 1, q2 - q1 - 1);
        }

        // ══════════════════════════════════════════════════════════════════════
        // ① 真文件存在且五行齐
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void test_dataFile_exists()
        {
            Assert.That(File.Exists(DataPath), Is.True,
                $"`assets/data/disease_registry.json` 不存在:{DataPath}\n" +
                "⇒ PRE-S18-A 的伤情行未建(空集冒充绿是本项目抓过的形态)");
        }

        [Test]
        public void test_dataFile_containsExactlyTheFiveWhitelistInjuryRows()
        {
            // ⚠️ 2026-10-22 改名 + 收窄:本断言原先要求「**全部行**恰等于白名单五项」。
            //    八病种落地后该口径失效 —— 文件里现在有 5 伤情 + 8 病种 = 13 行。
            //    改为只对 `kind: injury` 的行断言闭集,病种行由下一条断言管。
            var rows = LoadRows(File.ReadAllText(DataPath));
            string[] keys = rows
                .Where(r => r.DiseaseKey.StartsWith("INJ_", StringComparison.Ordinal))
                .Select(r => r.DiseaseKey)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToArray();
            string[] expected = Whitelist.OrderBy(k => k, StringComparer.Ordinal).ToArray();

            Assert.That(keys, Is.EqualTo(expected),
                "伤情行须恰等于白名单闭集(death-and-respawn.md:96)\n" +
                $"实得:{string.Join(", ", keys)}\n应得:{string.Join(", ", expected)}");
        }

        /// <summary>
        /// R3.3 八病种必在(GDD :428 表)。**断言键集合,不写死行数** ——
        /// 写死行数会让「加一行数据」变成「改一个测试」。
        /// </summary>
        [Test]
        public void test_dataFile_containsAllEightR33Diseases()
        {
            // ⚠️ 走**真 binder**,不走本文件的 `LoadRows` ——
            //    后者是手写最小读取器,第 83 行硬编码 `continue` 掉所有非 injury 行,
            //    用它断言病种存在会**永远红**(读不到 ≠ 不存在)。
            var bound = DaYiJingCheng.EditorTools.Bake.DiseaseRegistryBinder
                .Bind(File.ReadAllText(DataPath));

            var keys = new System.Collections.Generic.HashSet<string>();
            foreach (var r in bound.Rows) keys.Add(r.DiseaseKey);

            string[] required =
            {
                "DIS_MALARIA", "DIS_RHEUMATIC_FEVER", "DIS_HEART_FAILURE", "DIS_TYPHOID",
                "DIS_MEASLES", "DIS_DYSENTERY", "DIS_TETANUS", "DIS_NEURASTHENIA",
            };
            foreach (string k in required)
                Assert.That(keys, Does.Contain(k), $"R3.3 病种缺失:{k}(GDD :428)");

            // 病种行的 kind 必须是 Disease(不是 Injury)—— 防「建了行但 kind 写错」
            foreach (var r in bound.Rows)
                if (r.DiseaseKey.StartsWith("DIS_", StringComparison.Ordinal))
                    Assert.That(r.Kind, Is.EqualTo(DiseaseKind.Disease),
                        $"{r.DiseaseKey} 的 kind 应为 Disease");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ② 真校验器零违例
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void test_rowsPassTheRealValidator()
        {
            var rows = LoadRows(File.ReadAllText(DataPath));
            Assert.That(rows.Count, Is.EqualTo(5), "读到的行数不是 5 —— 装载面本身有问题");

            Assert.DoesNotThrow(() => RegistrySchemaValidator.Validate(rows),
                "真 `RegistrySchemaValidator.Validate` 拒收了数据行 —— 数值或字段有违例");
        }

        /// <summary>
        /// 五项一律**急性保持型**(`self_limit=false ∧ plateau=false`)。
        /// <para>这是白名单伤情能致死的**结构前提**:GDD `:581` 的死亡支要求
        /// `lethal ∧ self_limit=false`。自愈型的伤永远不会死人。</para>
        /// </summary>
        [Test]
        public void test_allRows_areAcuteHoldType()
        {
            var rows = LoadRows(File.ReadAllText(DataPath));
            foreach (var r in rows)
            {
                Assert.That(r.Curve.SelfLimit, Is.False,
                    $"{r.DiseaseKey} 是 self_limit=true ⇒ 该伤永远不会致死,与白名单意图冲突");
                Assert.That(r.Curve.Plateau, Is.False, $"{r.DiseaseKey} 不该是 plateau");
                Assert.That(r.Lethality, Is.GreaterThan(0),
                    $"{r.DiseaseKey} 是急性保持型但 Lethality=0 ⇒ 违校验 4");
            }
        }

        /// <summary><c>trauma_half_life</c> 五项须为 60(<c>OQ-25-7</c> 2026-10-07 已裁)。</summary>
        [Test]
        public void test_traumaHalfLife_isTheAdjudicatedValue()
        {
            var rows = LoadRows(File.ReadAllText(DataPath));
            foreach (var r in rows)
            {
                Assert.That(r.TraumaHalfLife, Is.EqualTo(60),
                    $"{r.DiseaseKey} 的 trauma_half_life 不是 OQ-25-7 裁定的 60");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // ③ 五项可达死(用真常量,不抄数)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// `A_peak/SCALE ≥ DEATH_THRESHOLD` —— 校验 4b 的语义。
        /// <para>⚠️ 读的是**真** `StateMachine.DEATH_THRESHOLD` 常量,不是抄来的 9000。
        /// 该常量一旦裁定成别的值,本条会**自动**跟着变 —— 这才是判据,不是快照。</para>
        /// </summary>
        [Test]
        public void test_allRows_canReachDeathThreshold()
        {
            var rows = LoadRows(File.ReadAllText(DataPath));
            var death = new Fix(StateMachine.DEATH_THRESHOLD);

            foreach (var r in rows)
            {
                // A_peak / SCALE 在 Q16.16 下 = (APeak.Raw << 16) / Scale.Raw
                long apexOverScale = (r.Curve.APeak.Raw * Fix.OneRaw) / r.Scale.Raw;
                Assert.That(apexOverScale, Is.GreaterThanOrEqualTo(death.Raw),
                    $"{r.DiseaseKey}: A_peak/SCALE = {apexOverScale} raw < DEATH_THRESHOLD = {death.Raw} raw\n" +
                    "⇒ 该伤「过临界却同值不达死线」= 无限迁延(GDD 校验 4b)");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // ④ 判据会红(注入违例)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>`trauma_half_life = 0` ⇒ `R1_CHECK_32` 红(连击不叠加)。</summary>
        [Test]
        public void test_r1_32_zeroTraumaHalfLife_throws()
        {
            var rows = LoadRows(File.ReadAllText(DataPath));
            rows[0].TraumaHalfLife = 0;

            var ex = Assert.Throws<RegistryValidationException>(
                () => RegistrySchemaValidator.Validate(rows));
            Assert.That(ex.RuleNumber, Is.EqualTo(RegistrySchemaValidator.R1_CHECK_32));
        }

        /// <summary>`trauma_half_life` 超 `HALF_LIFE_MAX` ⇒ `R1_CHECK_32` 红(否则 A13 除零)。</summary>
        [Test]
        public void test_r1_32_overMaxTraumaHalfLife_throws()
        {
            var rows = LoadRows(File.ReadAllText(DataPath));
            rows[0].TraumaHalfLife = RegistrySchemaValidator.HalfLifeMax + 1;

            var ex = Assert.Throws<RegistryValidationException>(
                () => RegistrySchemaValidator.Validate(rows));
            Assert.That(ex.RuleNumber, Is.EqualTo(RegistrySchemaValidator.R1_CHECK_32));
        }

        /// <summary>非 injury 行**不查** `trauma_half_life`(留 0 合法)—— 防判据过宽。</summary>
        [Test]
        public void test_r1_32_doesNotApplyToDiseaseRows()
        {
            var rows = LoadRows(File.ReadAllText(DataPath));
            // 造一个 disease 行,trauma_half_life 留 0(默认值)
            var disease = new DiseaseRegistryEntry
            {
                // 🔁 2026-10-22:99 → 64。R1_CHECK_33 立起后 DiseaseId ∈ [1, 64],
                //   99 越界会被 R1-33 拒收 ⇒ 本测试会因「别的规则红」而失去判别力
                //   (它要证的是「R1-32 不适用于非 injury 行」)。取上界 64:与真表 2..14 不冲突。
                DiseaseId = 64, DiseaseKey = "DIS_PROBE", Kind = DiseaseKind.Disease,
                Polarity = 2, Severity = 2, Contagion = 0, Lethality = 0,
                TreatmentDifficulty = 5, RecoveryTime = 100,
                Scale = new Fix(Fix.OneRaw), TraumaHalfLife = 0,
                Curve = null,
            };
            rows.Add(disease);

            Assert.DoesNotThrow(() => RegistrySchemaValidator.Validate(rows),
                "非 injury 行被 R1-32 误查了 —— 判据过宽");
        }

        /// <summary>`A_peak > Scale` ⇒ `R1_CHECK_23` 红(position 永久钉 1.0)。</summary>
        [Test]
        public void test_r1_23_apexAboveScale_throws()
        {
            var rows = LoadRows(File.ReadAllText(DataPath));
            rows[0].Curve.APeak = new Fix(rows[0].Scale.Raw + Fix.OneRaw);

            var ex = Assert.Throws<RegistryValidationException>(
                () => RegistrySchemaValidator.Validate(rows));
            Assert.That(ex.RuleNumber, Is.EqualTo(RegistrySchemaValidator.R1_CHECK_23));
        }

        /// <summary>急性保持型但 <c>Lethality = 0</c> ⇒ `R1_CHECK_22` 红(无限迁延)。</summary>
        [Test]
        public void test_r1_22_acuteHoldWithoutLethal_throws()
        {
            var rows = LoadRows(File.ReadAllText(DataPath));
            rows[0].Lethality = 0;

            var ex = Assert.Throws<RegistryValidationException>(
                () => RegistrySchemaValidator.Validate(rows));
            Assert.That(ex.RuleNumber, Is.EqualTo(RegistrySchemaValidator.R1_CHECK_22));
        }

        /// <summary>`DiseaseId` 重复 ⇒ `R1_CHECK_01` 红。</summary>
        [Test]
        public void test_r1_01_duplicateId_throws()
        {
            var rows = LoadRows(File.ReadAllText(DataPath));
            rows[1].DiseaseId = rows[0].DiseaseId;

            var ex = Assert.Throws<RegistryValidationException>(
                () => RegistrySchemaValidator.Validate(rows));
            Assert.That(ex.RuleNumber, Is.EqualTo(RegistrySchemaValidator.R1_CHECK_01));
        }

        /// <summary>删掉一行 ⇒ 只剩 4 行,存在性判据须红(防「少一行也绿」)。</summary>
        [Test]
        public void test_missingRow_isDetected()
        {
            string json = File.ReadAllText(DataPath);
            var rows = LoadRows(json);
            Assert.That(rows.Count, Is.EqualTo(5));

            // 从原文里挖掉 INJ_CLAW 整行,重读
            int at = json.IndexOf("\"key\": \"INJ_CLAW\"", StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0));
            int start = json.LastIndexOf('{', at);
            int depth = 0, end = start;
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}') { depth--; if (depth == 0) { end = i; break; } }
            }
            string trimmed = json.Remove(start, end - start + 1);

            var rowsAfter = LoadRows(trimmed);
            Assert.That(rowsAfter.Count, Is.EqualTo(4), "挖掉一行后应剩 4 行");
            Assert.That(rowsAfter.Select(r => r.DiseaseKey), Does.Not.Contain("INJ_CLAW"));
        }
    }
}
