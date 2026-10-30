// 9 注册表烘焙管线 —— 判据测试(2026-10-22 立)。
//
// ══════════════════════════════════════════════════════════════════════════
// 本文件要证明的四件事
// ══════════════════════════════════════════════════════════════════════════
//   ① **真源文件能烘** —— `assets/data/disease_registry.json` 走
//      `DiseaseRegistryBaker.BakeFromRepo` 产出非空字节,零违例。
//   ② **产物确定** —— 同一源文本两次烘焙 ⇒ **逐位相同**字节(ADR-014 §二)。
//   ③ **头契约正确** —— magic "DYJC" / formatVersion / schemaVersion / payloadLength
//      逐字段核对,且 payloadLength + HeaderSize == 总长。
//   ④ **管线会红** —— 注入违例(JSON 数字写 Fix / 未知键 / kind 拼错 /
//      非 injury 行漏 trauma_half_life …)要求**各自**被拒。
//      没有这一条,「烘焙成功」与「binder 什么都没检查」无法区分。
//
// ⚠️ 本文件**只读**仓库文件(不写盘)⇒ 不触发 `FsWriteBoundaryGate`。
//
// 权威:ADR-014 §二/§三/§四/§五 · `CookedFormat.cs` · `DiseaseRegistryBinder.cs`

using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Sim;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>注册表烘焙:真源可烘 + 字节确定 + 头契约 + 违例各自被拒。</summary>
    [TestFixture]
    public sealed class DiseaseRegistryBakePipelineTest
    {
        private static string RepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        private static string SourcePath =>
            Path.Combine(RepoRoot(), "assets", "data", "disease_registry.json");

        /// <summary>取一份**最小合法**源文本(单行 injury),供注入测试改写。</summary>
        private static string MinimalJson() => @"{
  ""schema_version"": 2,
  ""diseases"": [
    {
      ""id"": 2,
      ""key"": ""INJ_PROBE"",
      ""kind"": ""injury"",
      ""severity"": 4,
      ""lethal"": true,
      ""scale"": ""100"",
      ""trauma_half_life"": 60,
      ""natural_progress"": {
        ""incubation"": 0,
        ""a_peak"": ""100"",
        ""tau_rise"": ""40"",
        ""tau_peak"": 300,
        ""tau_fall"": ""600"",
        ""plateau"": false,
        ""self_limit"": false,
        ""boundary_mode"": ""monotone""
      }
    }
  ]
}";

        // ══════════════════════════════════════════════════════════════════════
        // ① 真源文件能烘
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void test_realSource_bakesWithoutError()
        {
            Assert.That(File.Exists(SourcePath), Is.True, $"源文件不存在:{SourcePath}");

            var outp = DiseaseRegistryBaker.BakeFromRepo(RepoRoot());

            Assert.That(outp.Cooked, Is.Not.Null.And.Not.Empty, "烘焙产出空字节");
            Assert.That(outp.ConfigVersion, Is.Not.Zero, "ConfigVersion 不得为 0(内容哈希派生)");

            // ⚠️ 不写死行数 —— 写死会让「加一行数据」变成「改一个测试」,
            //    而这两件事的语义完全不同。改为断言**内容结构**:
            //    ① 至少 1 行(R1-17);② 伤情与病种两类都在;③ 每行都有曲线。
            Assert.That(outp.Rows.Count, Is.GreaterThanOrEqualTo(1), "R1-17:至少 1 个病种");

            var keys = new System.Collections.Generic.HashSet<string>();
            foreach (var row in outp.Rows) keys.Add(row.DiseaseKey);

            Assert.That(keys, Does.Contain("INJ_STAB"), "R2 白名单伤情缺失");
            Assert.That(keys, Does.Contain("DIS_MALARIA"), "R3.3 病种缺失");
            Assert.That(keys, Does.Contain("DIS_TETANUS"), "R3.3 病种缺失");

            foreach (var row in outp.Rows)
                Assert.That(row.Curve, Is.Not.Null, $"{row.DiseaseKey} 缺 natural_progress 曲线(R1.2 铁律)");
        }

        /// <summary>最小合法源文本也必须能烘 —— 防「只对真文件碰巧能跑」。</summary>
        [Test]
        public void test_minimalValidSource_bakes()
        {
            var outp = DiseaseRegistryBaker.BakeFromJson(MinimalJson());
            Assert.That(outp.Cooked, Is.Not.Empty);
            Assert.That(outp.Rows.Count, Is.EqualTo(1));
        }

        /// <summary>
        /// **真表的 <c>handle</c> 逐行落在 GDD <c>:294</c> 的闭集内,且三条代表性派生都出现。**
        ///
        /// <para><b>为什么这条必须有</b>:<c>handle</c> 是**派生字段**(GDD <c>:329</c>),
        /// 派生出错不会抛 —— 它只会写进字节,然后在 8 的「这病我有没有牌」里静默错。
        /// 只断言「闭集内」不够(全填 <c>symptomatic_only</c> 也满足);只断言「三档都出现」
        /// 也不够(可以出现得不对)。下面把<b>逐行期望值</b>钉死,二者同时成立。</para>
        ///
        /// <para>⚠️ 期望值**不是**抄产物,而是按 GDD <c>:425-437</c> 的病种表 + 轴
        /// <c>disease_action_axis.json</c> 的 <c>treatable_by</c> 反推出来的:
        /// 只有 <c>DIS_MALARIA</c> 有 <c>polarity = causal</c> 的对因处置(金鸡纳皮);
        /// <c>DIS_TETANUS</c> 发病后无解 ⇒ <c>none</c>;其余七个病种与五个伤情
        /// ⇒ <c>symptomatic_only</c>。</para>
        ///
        /// <para><b>🔁 2026-10-23 用户裁定 ② 已修前缀缺陷(原文不删,留痕)</b>:
        /// 原文写「当前真表里 <c>care</c> 一档为 0 行 —— 这是缺陷」,根因是
        /// <c>StateMachine.CareApplicableDiseases</c> 用**裸键**而注册表键是 <c>DIS_</c> 前缀
        /// ⇒ 交集为空 ⇒ 三个 GDD 点名该为 <c>care</c> 的病种被派生成 <c>symptomatic_only</c>。
        /// <b>现已修复</b>:归一落在 <c>CareLeverApplicability</c> 的查表层
        /// (裸键 + 前缀键等价),故下面三行期望值改回 GDD <c>:294</c> 要求的 <c>care</c>。</para>
        ///
        /// <para><b>⚠️ 三行 <c>care</c> 是本测试的<em>主判据</em>,不是陪衬</b> —— 若归一实现
        /// 回退(或有人把查表改回裸键直查),这三行会同时红;它们是这条缺陷的绊线。</para>
        /// </summary>
        [Test]
        public void test_realSource_handleIsDerivedIntoClosedSet_perRow()
        {
            var outp = DiseaseRegistryBaker.BakeFromRepo(RepoRoot());

            var expected = new System.Collections.Generic.Dictionary<string, string>
            {
                { "INJ_STAB", "symptomatic_only" },
                { "INJ_LACERATION", "symptomatic_only" },
                { "INJ_HEMORRHAGE", "symptomatic_only" },
                { "INJ_CLAW", "symptomatic_only" },
                { "INJ_BITE", "symptomatic_only" },
                // 🔁 2026-10-30 新增:自限型非致死伤情(用户裁定,ADR-035 T2 的合法行载体)。
                //    处置轴与其余伤情同 = symptomatic_only(`treatable_by: []` ∧ `causes: []`)。
                { "INJ_BLUNT", "symptomatic_only" },
                // 🔁 2026-10-30 新增:药毒(53 代价表中/重两档的落点)。处置轴同上 = symptomatic_only。
                { "INJ_DRUG_HARM", "symptomatic_only" },
                { "DIS_MALARIA", "causal" },              // 轴:action 12 / causal(金鸡纳皮)
                { "DIS_RHEUMATIC_FEVER", "symptomatic_only" },
                { "DIS_HEART_FAILURE", "care" },          // GDD :294 —— 照护适用集三员之一
                { "DIS_TYPHOID", "care" },                // 同上
                { "DIS_MEASLES", "symptomatic_only" },
                { "DIS_DYSENTERY", "care" },              // 同上
                { "DIS_TETANUS", "none" },                // 发病后无解(K8)
                { "DIS_NEURASTHENIA", "symptomatic_only" },
            };

            Assert.That(outp.Rows.Count, Is.EqualTo(expected.Count),
                "真表行数变了 ⇒ 本表的逐行期望须同步复核(不是改数字,是重新按 GDD 推)");

            foreach (var row in outp.Rows)
            {
                Assert.That(DaYiJingCheng.Sim.DiseaseHandle.IsValid(row.Handle), Is.True,
                    $"{row.DiseaseKey}.handle=\"{row.Handle}\" ∉ GDD :294 闭集");
                Assert.That(expected.TryGetValue(row.DiseaseKey, out string want), Is.True,
                    $"{row.DiseaseKey} 不在本测试的期望表里 —— 新增病种须按 GDD :425-437 补一行期望");
                Assert.That(row.Handle, Is.EqualTo(want),
                    $"{row.DiseaseKey}.handle 派生值不符 —— 期望 {want},实得 {row.Handle}");
            }
        }

        /// <summary>
        /// **<c>handle</c> 手填 = 烘焙期硬失败**(GDD <c>:329</c>「派生而非手填」)。
        /// <para>这条防的是「表里写 causal、实际没有对因药」的静默撒谎 ——
        /// 若 binder 静默丢弃手填值,作者**看不到自己的错**。</para>
        /// </summary>
        [Test]
        public void test_handFilledHandle_isRejected()
        {
            // MinimalJson() 是逐字字符串 ⇒ 引号在源里写成两个双引号,替换串须照抄。
            string bad = MinimalJson().Replace(
                @"""scale"": ""100"",",
                @"""scale"": ""100"", ""handle"": ""causal"",");
            var ex = Assert.Throws<BakeValidationException>(() => DiseaseRegistryBaker.BakeFromJson(bad));
            Assert.That(ex.Message, Does.Contain("handle"),
                "报文须点名 handle,否则作者看不出是自己写了派生字段");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ①b 新编码三块(handle / relapse / signs[])的**非空**往返
        // ══════════════════════════════════════════════════════════════════════
        //
        // ⚠️ 为什么单列:真表 13 行的 `signs` 全是 `[]`、`relapse` 全是 null。
        //    只跑真表 ⇒ 这两块的**内容编码**一行都没被执行过 —— 「全空也绿」是
        //    本项目抓过多次的假绿形态。下面用**非空**夹具把编码/解码真的走一遍。

        /// <summary>含非空 <c>signs[]</c> + <c>relapse</c> 块的最小源文本。</summary>
        private static string RichJson() => @"{
  ""schema_version"": 2,
  ""diseases"": [
    {
      ""id"": 7,
      ""key"": ""DIS_PROBE_RICH"",
      ""kind"": ""disease"",
      ""severity"": 5,
      ""lethal"": true,
      ""scale"": ""100"",
      ""trauma_half_life"": 0,
      ""relapse"": { ""a_rel"": ""1/4"", ""interval"": 1200, ""tau_fall"": ""300"" },
      ""signs"": [
        { ""sign_key"": ""sign_chill"", ""channel_mask"": 1 },
        { ""sign_key"": ""sign_sweat"", ""channel_mask"": 9 }
      ],
      ""natural_progress"": {
        ""incubation"": 0,
        ""a_peak"": ""100"",
        ""tau_rise"": ""40"",
        ""tau_peak"": 300,
        ""tau_fall"": ""600"",
        ""plateau"": false,
        ""self_limit"": false,
        ""boundary_mode"": ""monotone""
      }
    }
  ]
}";

        /// <summary>
        /// 非空 <c>signs[]</c> / <c>relapse</c> 经**真往返**(写方 → 读方 codec → 逐字段比对)无损。
        /// <para>判据走 <c>Materialize</c> —— 它是写/读两份手写布局的**唯一自动兜现**。
        /// 只断言「解出来不抛」不够:解出来全 null 也能不抛。</para>
        /// </summary>
        [Test]
        public void test_richRow_signsAndRelapse_surviveRoundTrip()
        {
            var outp = DiseaseRegistryBaker.BakeFromJson(RichJson());
            var dataSet = DiseaseRegistryBaker.Materialize(outp);   // 内含逐字段比对,分叉即抛

            Assert.That(dataSet.Rows.Count, Is.EqualTo(1));
            var row = dataSet.Rows[0];

            Assert.That(row.Relapse, Is.Not.Null, "relapse 块往返后丢了");
            // raw 换算(逐位,不调 FixParse —— 断言的期望值应当是**字面量推导**,
            // 而不是再走一遍被断言对象所用的同一个解析器):
            //   "1/4" ⇒ 65536 / 4 = 16384;"300" ⇒ 300 × 65536 = 19660800。
            Assert.That(row.Relapse.ARel.Raw, Is.EqualTo(16384L), "\"1/4\" 的 raw 应为 16384");
            Assert.That(row.Relapse.Interval, Is.EqualTo(1200));
            Assert.That(row.Relapse.TauFall.Raw, Is.EqualTo(19660800L), "\"300\" 的 raw 应为 19660800");

            Assert.That(row.Signs, Is.Not.Null, "signs[] 往返后丢了");
            Assert.That(row.Signs.Length, Is.EqualTo(2), "signs[] 条数往返后不符");
            Assert.That(row.Signs[0].SignKey, Is.EqualTo("sign_chill"));
            Assert.That(row.Signs[0].ChannelMask, Is.EqualTo(1));
            Assert.That(row.Signs[1].SignKey, Is.EqualTo("sign_sweat"));
            Assert.That(row.Signs[1].ChannelMask, Is.EqualTo(9), "9 = Face|Breath(两位或)");
            // ⚠️ 第二行刻意用**两位或**而不是 3:3 = Face|Voice 也在闭集内,但它与
            //    「单通道 1/2」在肉眼上太近,写错位不会立刻看出来。9 的两位相隔,
            //    任一位写错都会让本断言红。
        }

        /// <summary>
        /// <c>signs</c> **缺省(null)** 与 **显式空集(<c>[]</c>)** 往返后仍是两个不同状态。
        /// <para>GDD <c>:291</c> 只允许后者为空;把两者混成一个值 = 让「没声明」伪装成
        /// 「声明了但没有」,而 R1-29 对两者的处置不同(null 跳过 / 空集放行)。</para>
        /// </summary>
        [Test]
        public void test_signsNullVsEmpty_stayDistinct()
        {
            var nullRow = DiseaseRegistryBaker.Materialize(
                DiseaseRegistryBaker.BakeFromJson(MinimalJson())).Rows[0];
            Assert.That(nullRow.Signs, Is.Null, "缺省 signs 应解回 null(未声明)");

            var emptyRow = DiseaseRegistryBaker.Materialize(
                DiseaseRegistryBaker.BakeFromJson(
                    MinimalJson().Replace(
                        @"""scale"": ""100"",",
                        @"""scale"": ""100"", ""signs"": [],"))).Rows[0];
            Assert.That(emptyRow.Signs, Is.Not.Null, "显式空集应解回空数组,不是 null");
            Assert.That(emptyRow.Signs.Length, Is.EqualTo(0), "显式空集长度应为 0");
        }

        /// <summary>
        /// **真表 <c>signs[]</c> 逐病种与 GDD `diagnosis-system.md:462-580` 一致** ——
        /// 2026-10-23 用户裁定「一并补设计 / 阴性体征进」后新立。
        ///
        /// <para><b>为什么必须有这条</b>:在填表之前,真表 13 行的 <c>signs</c> 全是
        /// <c>[]</c>,而**没有任何判据**钉住这一点 —— 上面那些往返测试用的是**合成夹具**
        /// (<c>RichJson</c>),合成夹具绿不能证明真表有内容。这正是本项目抓过多次的
        /// 「全空也绿」形态。<b>本测试数没变就是证据</b>:填完 26 条词条后全量仍是
        /// <c>4692</c>,说明此前零判据覆盖真表 signs 内容。</para>
        ///
        /// <para><b>期望值来源</b>:不是抄产物,而是按 8 的 GDD 体征词表**末列「关联」**
        /// (阳性表)与**「排除什么」**(阴性表)逐条反推。每行末的阴性词条按用户
        /// 2026-10-23 裁定「进,作为鉴别锚点」保留。</para>
        ///
        /// <para><b>✅ 2026-10-23 补完</b>:此前留空的六行已按用户裁定填齐 ——
        /// 五个 <c>INJ_*</c> 走新增的**伤口**通道(8 侧此前无该序数标签,是它们无从填写的根因),
        /// <c>DIS_NEURASTHENIA</c> 走新增的**主观**通道(R3.6 主观族,词表此前零条)。
        /// <c>DIS_DYSENTERY</c> 另补 <c>sign_purulent_stool</c>(病史位)。</para>
        ///
        /// <para><b>⚠️ 填表顺序是「先补词表、再填注册表」</b>:8 侧体征词表是外键的真源,
        /// 先填注册表会撞 <c>AC-8-34·fk</c> 外键悬空。本次顺序:词表 34 → 43 →
        /// 注册表六行 → 重烘。</para>
        /// </summary>
        [Test]
        public void test_realSource_signsMatchGddVocabulary_perRow()
        {
            var outp = DiseaseRegistryBaker.BakeFromRepo(RepoRoot());

            // 键序 = GDD 表格的书写序(面色 → 语声 → 姿态 → 呼吸 → 触感),
            // 末项为阴性锚点(用户 2026-10-23 裁定)。
            var expected = new System.Collections.Generic.Dictionary<string, string[]>
            {
                // GDD :462 sign_pallor(失血·休克·心衰) · :486 orthopnea(心衰)
                // :488 dyspnea_speech(心衰) · :506 tachypnea · :507 rales(心衰肺水肿)
                // :568 lung_clear 阴性「排除心衰」
                { "DIS_HEART_FAILURE", new[] {
                    "sign_pallor", "sign_dyspnea_speech", "sign_orthopnea",
                    "sign_tachypnea", "sign_rales", "sign_lung_clear" } },

                // GDD :488 trismus · :489 opisthotonus · :490 neck_stiffness · :491 risus(均破伤风)
                // :570 neck_supple 阴性「排除破伤风(早期)」
                { "DIS_TETANUS", new[] {
                    "sign_trismus", "sign_opisthotonus", "sign_neck_stiffness",
                    "sign_risus", "sign_neck_supple" } },

                // GDD :463 flush(发热·伤寒) · :470 delirium(伤寒) · :541 relative_bradycardia(伤寒)
                { "DIS_TYPHOID", new[] {
                    "sign_flush", "sign_delirium", "sign_relative_bradycardia" } },

                // GDD :463 flush(麻疹) · :465 rash(麻疹出疹期) · :466 koplik(麻疹出疹前唯一特异所见)
                { "DIS_MEASLES", new[] {
                    "sign_flush", "sign_rash", "sign_koplik" } },

                // GDD :464 diaphoresis(疟疾出汗期) · :467 jaundice(疟疾溶血)
                // :543 hepatosplenomegaly(疟疾) · :571 no_organomegaly 阴性「排除疟疾」
                { "DIS_MALARIA", new[] {
                    "sign_diaphoresis", "sign_jaundice",
                    "sign_hepatosplenomegaly", "sign_no_organomegaly" } },

                // GDD :487 curled(痢疾) · :544 abd_tenderness(痢疾)
                // :569 abd_soft 阴性「排除痢疾」—— P0 最难一对鉴别的唯一阴性支点
                // :556 purulent_stool「补上它,本才有自己的确诊所见」(2026-10-23 补位)
                { "DIS_DYSENTERY", new[] {
                    "sign_curled", "sign_abd_tenderness", "sign_abd_soft",
                    "sign_purulent_stool" } },

                // GDD :463 flush(风湿热) · :492 joint_swelling(风湿热,「游走性」是鉴别点)
                { "DIS_RHEUMATIC_FEVER", new[] {
                    "sign_flush", "sign_joint_swelling" } },

                // ── 伤情五行:伤口通道(2026-10-23 起 8 侧有 Wound 序数标签)+ 呻吟 ──
                // GDD :473 sign_moan「各类剧痛」—— 伤情共用
                // GDD :550-554 五条伤口词条(形态权威 = 25 combat-and-weapon-lines.md:1490-1499)
                { "INJ_STAB", new[] {
                    "sign_wound_stab", "sign_moan" } },
                { "INJ_LACERATION", new[] {
                    "sign_wound_laceration", "sign_moan" } },
                // 失血 = 状态型伤情,四联征逐条见 GDD 关联列(失血 · 休克)
                { "INJ_HEMORRHAGE", new[] {
                    "sign_wound_hemorrhage", "sign_pallor",
                    "sign_voice_weak", "sign_cold_clammy", "sign_pulse_rapid" } },
                { "INJ_CLAW", new[] {
                    "sign_wound_claw", "sign_moan" } },
                { "INJ_BITE", new[] {
                    "sign_wound_bite", "sign_moan" } },
                // 🔁 2026-10-30 新增(INJ_BLUNT,自限型非致死)。**只有 `sign_moan`**
                //    —— 刻意**不**复用五条 `sign_wound_*`:那五条是利器的**形态**词
                //    (刺 / 割 / 失血 / 爪 / 咬),钝击不在其中;为它新造一个 `sign_wound_blunt`
                //    须改 25 的伤情词表 + GDD 表,**不属本轮**。钝击的共性是「各类剧痛」
                //    (GDD :473 `sign_moan`),单用它是诚实的下限,不是遗漏。
                { "INJ_BLUNT", new[] {
                    "sign_moan" } },
                // 🔁 2026-10-30 新增(INJ_DRUG_HARM,药毒)。三通道:面色 / 语声 / 主观 ——
                //    都是既有体征键,不新增 8 的体征行。序 = 通道位序(1 → 2 → 64)。
                { "INJ_DRUG_HARM", new[] {
                    "sign_pallor", "sign_delirium", "sign_fatigue" } },

                // ── 豁免类:主观族(R3.6 —— 客观族中性,主观族有词条)──
                { "DIS_NEURASTHENIA", new[] {
                    "sign_fatigue", "sign_insomnia", "sign_palpitation", "sign_low_mood" } },
            };

            Assert.That(outp.Rows.Count, Is.EqualTo(expected.Count),
                "真表行数变了 ⇒ 本表须同步复核(按 GDD 体征表重新推,不是改数字)");

            int total = 0;
            foreach (var row in outp.Rows)
            {
                Assert.That(expected.TryGetValue(row.DiseaseKey, out string[] want), Is.True,
                    $"{row.DiseaseKey} 不在期望表里 —— 新增病种须按 8 的体征词表补一行");
                Assert.That(row.Signs, Is.Not.Null,
                    $"{row.DiseaseKey}.signs 为 null(未声明)—— 真表每行都该显式声明");

                string[] got = new string[row.Signs.Length];
                for (int i = 0; i < got.Length; i++) got[i] = row.Signs[i].SignKey;

                Assert.That(got, Is.EqualTo(want),
                    $"{row.DiseaseKey}.signs 与 GDD 体征表不符 —— 期望 [{string.Join(",", want)}],"
                    + $"实得 [{string.Join(",", got)}]");

                // 通道位必须与词表自带的 channel 一致(防「词填对了、位填错了」)
                for (int i = 0; i < row.Signs.Length; i++)
                {
                    Assert.That(row.Signs[i].ChannelMask, Is.Not.EqualTo(0),
                        $"{row.DiseaseKey}.signs[{i}] 通道位为 0(R1-29 拒收)");
                    Assert.That(row.Signs[i].ChannelMask & ~SignChannels.All, Is.EqualTo(0),
                        $"{row.DiseaseKey}.signs[{i}] 通道位越出八通道闭集");
                }
                total += got.Length;
            }

            // 绊线:若有人把真表 signs 全清空,上面逐行比对已红;此处再钉一个总数,
            // 让「全空」这件事在断言消息里直接可见。
            // ⚠️ 2026-10-23:总数**改为从 expected 求和** —— 原写死 26(7 病种),而本表的
            // 循环面是 13 行(7 病种 + 6 伤情),两者本就不对齐;填实伤口词表后总数变 44 时
            // 又红一次。派生值同时满足两个目的:①「全空」仍可见(total=0 ≠ 期望和);
            // ② 新增病种时只改上面的 expected 表,**不会**再漏改这里(防第二处真源)。
            int expectedTotal = expected.Values.Sum(v => v.Length);
            Assert.That(total, Is.EqualTo(expectedTotal),
                $"真表体征词条总数应 {expectedTotal}({expected.Count} 行);实得 {total} —— 归零 = 用户 2026-10-23 的填充被抹掉");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ② 产物确定(逐位)
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void test_sameSource_producesBitIdenticalBytes()
        {
            string json = File.ReadAllText(SourcePath);

            byte[] a = DiseaseRegistryBaker.BakeFromJson(json).Cooked;
            byte[] b = DiseaseRegistryBaker.BakeFromJson(json).Cooked;

            Assert.That(a.Length, Is.EqualTo(b.Length), "两次烘焙长度不同");
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    Assert.Fail($"第 {i} 字节不同:{a[i]:X2} vs {b[i]:X2} —— 产物非确定");
            }
        }

        /// <summary>ConfigVersion 相同 ⇒ 字节相同(内容哈希派生的一致性)。</summary>
        [Test]
        public void test_sameContent_sameConfigVersion()
        {
            string json = File.ReadAllText(SourcePath);
            uint v1 = DiseaseRegistryBaker.BakeFromJson(json).ConfigVersion;
            uint v2 = DiseaseRegistryBaker.BakeFromJson(json).ConfigVersion;
            Assert.That(v1, Is.EqualTo(v2));
        }

        /// <summary>内容变了 ⇒ ConfigVersion 变(否则存档头比对失效)。</summary>
        [Test]
        public void test_changedContent_changesConfigVersion()
        {
            string json = File.ReadAllText(SourcePath);
            uint before = DiseaseRegistryBaker.BakeFromJson(json).ConfigVersion;

            // 改一个数(INJ_BITE 的 tau_peak 700 → 701)
            string mutated = json.Replace("\"tau_peak\": 700", "\"tau_peak\": 701");
            Assert.That(mutated, Is.Not.EqualTo(json), "夹具没改到东西");

            uint after = DiseaseRegistryBaker.BakeFromJson(mutated).ConfigVersion;
            Assert.That(after, Is.Not.EqualTo(before),
                "内容变了 ConfigVersion 没变 —— 存档头比对会失效(ADR-014 §五)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ③ 头契约
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void test_header_matchesCookedFormat()
        {
            var outp = DiseaseRegistryBaker.BakeFromJson(MinimalJson());
            byte[] b = outp.Cooked;

            Assert.That(b.Length, Is.GreaterThan(DaYiJingCheng.Sim.Contracts.CookedFormat.HeaderSize));

            // magic "DYJC"
            Assert.That(b[0], Is.EqualTo((byte)'D'));
            Assert.That(b[1], Is.EqualTo((byte)'Y'));
            Assert.That(b[2], Is.EqualTo((byte)'J'));
            Assert.That(b[3], Is.EqualTo((byte)'C'));

            Assert.That(ReadU32Le(b, 4), Is.EqualTo(DaYiJingCheng.Sim.Contracts.CookedFormat.FormatVersion),
                "formatVersion 不符");
            // ⚠️ 2026-10-23 起期望值改 2(布局加了 handle / relapse / signs[] 三块)。
            //    断言**仍写死字面量**而不是读 `ExpectedSchemaVersion` —— 那样本行会退化成
            //    「常量 == 常量」的同义反复,测不出「binder 把源文件声明的版本原样写进头槽」。
            Assert.That(ReadU32Le(b, 8), Is.EqualTo(2u), "schemaVersion 应为源文件声明的 2");
            Assert.That(ReadU32Le(b, 12), Is.EqualTo(outp.ConfigVersion), "configVersion 不符");

            uint payloadLen = ReadU32Le(b, 16);
            Assert.That(DaYiJingCheng.Sim.Contracts.CookedFormat.HeaderSize + payloadLen,
                Is.EqualTo(b.Length), "payloadLength 与实际长度不符(头契约破裂)");
        }

        private static uint ReadU32Le(byte[] b, int off) =>
            (uint)(b[off] | (b[off + 1] << 8) | (b[off + 2] << 16) | (b[off + 3] << 24));

        // ══════════════════════════════════════════════════════════════════════
        // ④ 管线会红
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// **Fix 字段写成 JSON 数字 ⇒ 硬拒**(ADR-014 §四 硬规则 1)。
        /// <para>这是整条管线最要紧的一条判据:数字 token 经 double/decimal 中转
        /// = 浮点进内存,绕 `FixParse` —— 而且它是**静默**的。</para>
        /// </summary>
        [Test]
        public void test_fixAsJsonNumber_isRejected()
        {
            string bad = MinimalJson().Replace("\"a_peak\": \"100\"", "\"a_peak\": 100");

            var ex = Assert.Throws<BakeValidationException>(() => DiseaseRegistryBaker.BakeFromJson(bad));
            Assert.That(ex.Message, Does.Contain("须为**字符串**").Or.Contain("浮点泄漏"),
                "Fix 写成 JSON 数字未被拒 —— 浮点会静默进定点域");
        }

        /// <summary>Fix 字段写成浮点字面量字符串 ⇒ 被 FixParse 拒(禁小数)。</summary>
        [Test]
        public void test_fixAsFloatString_isRejected()
        {
            string bad = MinimalJson().Replace("\"a_peak\": \"100\"", "\"a_peak\": \"0.75\"");
            Assert.Throws<BakeValidationException>(() => DiseaseRegistryBaker.BakeFromJson(bad));
        }

        /// <summary>未知键 ⇒ 硬失败(ADR-014 §三,防拼写错误静默丢字段)。</summary>
        [Test]
        public void test_unknownKey_isRejected()
        {
            string bad = MinimalJson().Replace("\"severity\": 4", "\"severty\": 4");
            var ex = Assert.Throws<BakeValidationException>(() => DiseaseRegistryBaker.BakeFromJson(bad));
            Assert.That(ex.Message, Does.Contain("未知键").Or.Contain("severty"));
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑤ 全局趋势窗口(W / TREND_EPS)—— R1-36(2026-10-22 铺管轮)
        // ══════════════════════════════════════════════════════════════════════
        //
        // ⚠️ 三条一组,**缺一即留盲区**:
        //   · 只测「0 可省」 ⇒ 「两键被整个忽略」与「正确读成 0」同效;
        //   · 只测「负值拒」 ⇒ 无法区分「真校验」与「任何非零都拒」;
        //   · 只测「读得到」 ⇒ 读的是 binder 自己的默认值还是 JSON 里的值,分不开。
        //   ⇒ 必须有「给非零值 ⇒ 读回非零」这条**正向**判据(与两个负向判据配对)。

        /// <summary>两键**可省**:缺 = 未配置 = 0(数值轮未裁)。</summary>
        [Test]
        public void test_trendWindow_absent_meansUnconfiguredZero()
        {
            // MinimalJson() 不含 trend_window_ticks / trend_eps
            var outp = DiseaseRegistryBaker.BakeFromJson(MinimalJson());
            Assert.That(outp.TrendWindowTicks, Is.EqualTo(0L),
                "缺 trend_window_ticks ⇒ 未配置哨兵 0,不得填任何非零默认值");
            Assert.That(outp.TrendEpsRaw, Is.EqualTo(0L),
                "缺 trend_eps ⇒ 未配置哨兵 0");
        }

        /// <summary>
        /// **正向**:给非零值 ⇒ 原样读回。
        /// <para>没有这条,「binder 根本没读这两个键、永远返回默认 0」与「正确实现」同效 ——
        /// 上面那条可省测试与下面那条负值测试都发现不了。</para>
        /// <para>⚠️ `trend_eps` 按 ADR-014 §四硬规则写**字符串**(Fix 字段),写 JSON 数字会被
        /// `ReadFix` 记错(浮点泄漏路径)。</para>
        /// <para>⚠️ **字面量的语义(实测踩过)**:`FixParse` 的裸整数 `"1966"` 表示**值 1966**
        /// (raw = 1966 × 65536 = 128843776),**不是** raw 1966。
        /// 要表达 raw 1966 须写比值 `"1966/65536"`(`FixParse.cs:20` 的合法形)。
        /// 本条用 `"1966/65536"` ⇒ raw 恰 1966,使断言能**逐位**钉住「读的是 raw 不是值」。</para>
        /// </summary>
        [Test]
        public void test_trendWindow_present_isReadBack()
        {
            string json = MinimalJson().Replace(
                "\"schema_version\": 2,",
                "\"schema_version\": 2,\n  \"trend_window_ticks\": 120,\n  \"trend_eps\": \"1966/65536\",");
            var outp = DiseaseRegistryBaker.BakeFromJson(json);

            Assert.That(outp.TrendWindowTicks, Is.EqualTo(120L),
                "trend_window_ticks 须原样读回(120),否则 binder 只是返回了默认 0");
            Assert.That(outp.TrendEpsRaw, Is.EqualTo(1966L),
                "trend_eps 须按 Fix **raw** 原样读回(1966)。写 \"1966/65536\" 而非 \"1966\" —— "
                + "后者是值 1966(raw 128843776),拿它断言会掩盖「raw / 值」的单位混淆");
        }

        /// <summary>负值 / 奇数 W ⇒ R1-36 拒收(负数与奇数各自独立)。</summary>
        [Test]
        public void test_trendWindow_outOfRange_isRejected()
        {
            // ① 负 W
            string negW = MinimalJson().Replace(
                "\"schema_version\": 2,",
                "\"schema_version\": 2,\n  \"trend_window_ticks\": -2,");
            var ex1 = Assert.Throws<BakeValidationException>(
                () => DiseaseRegistryBaker.BakeFromJson(negW), "负 W 须拒(0 = 未配置,负值无意义)");
            Assert.That(ex1.Message, Does.Contain("trend_window_ticks"));

            // ② 奇数 W —— GDD Tuning :1743「须为偶数(取 W/2)」
            string oddW = MinimalJson().Replace(
                "\"schema_version\": 2,",
                "\"schema_version\": 2,\n  \"trend_window_ticks\": 121,");
            var ex2 = Assert.Throws<BakeValidationException>(
                () => DiseaseRegistryBaker.BakeFromJson(oddW), "奇数 W 须拒(取 W/2 会丢半个 tick)");
            Assert.That(ex2.Message, Does.Contain("trend_window_ticks"));

            // ③ 负 TREND_EPS
            string negEps = MinimalJson().Replace(
                "\"schema_version\": 2,",
                "\"schema_version\": 2,\n  \"trend_eps\": \"-1\",");
            var ex3 = Assert.Throws<BakeValidationException>(
                () => DiseaseRegistryBaker.BakeFromJson(negEps), "负 TREND_EPS 须拒");
            Assert.That(ex3.Message, Does.Contain("trend_eps"));
        }

        /// <summary>`_` 前缀的作者态注记**不**算未知键(防判据过宽)。</summary>
        [Test]
        public void test_underscorePrefixedKeys_areAllowed()
        {
            string withNote = MinimalJson().Replace(
                "\"schema_version\": 2,",
                "\"schema_version\": 2,\n  \"_note\": \"作者态注记\",");
            Assert.DoesNotThrow(() => DiseaseRegistryBaker.BakeFromJson(withNote),
                "`_` 前缀注记被当成未知键 —— 判据过宽");
        }

        /// <summary>
        /// `schema_version` 与读方认领不符 ⇒ **烘焙期**拒(不是运行期 E-13)。
        /// <para><b>为什么这条必须有</b>:2026-10-23 布局升到 2 时实测过这个缺口 ——
        /// 四份合成源仍写 `1`,`Materialize`(真往返)当场红,而**裸烘焙**
        /// (`BakeFromRepo(...).Cooked`,产物漂移门走的那条)一路绿 ⇒
        /// 盘上会落一份**读方拒收**的产物而构建不报错。失败点从构建期漏到运行期。</para>
        /// <para>判据取 `1`(上一版真实值)而不是 `999`(随便一个越界值):前者正是
        /// 「忘了升版本号」的真实形态。</para>
        /// </summary>
        [Test]
        public void test_schemaVersionMismatch_isRejectedAtBake()
        {
            string bad = MinimalJson().Replace("\"schema_version\": 2", "\"schema_version\": 1");
            var ex = Assert.Throws<BakeValidationException>(() => DiseaseRegistryBaker.BakeFromJson(bad));
            Assert.That(ex.Message, Does.Contain("schema_version"),
                "报文须点名 schema_version,否则作者看不出是自己版本号写错了");
        }

        /// <summary>`kind` 拼错 / 不在闭集 ⇒ 被拒。</summary>
        [Test]
        public void test_unknownKind_isRejected()
        {
            string bad = MinimalJson().Replace("\"kind\": \"injury\"", "\"kind\": \"wound\"");
            var ex = Assert.Throws<BakeValidationException>(() => DiseaseRegistryBaker.BakeFromJson(bad));
            Assert.That(ex.Message, Does.Contain("wound"));
        }

        /// <summary>injury 行漏 `trauma_half_life` ⇒ `R1_CHECK_32` 拒。</summary>
        [Test]
        public void test_injuryMissingTraumaHalfLife_isRejected()
        {
            string bad = MinimalJson().Replace("\"trauma_half_life\": 60,", "");
            var ex = Assert.Throws<BakeValidationException>(() => DiseaseRegistryBaker.BakeFromJson(bad));
            Assert.That(ex.Message, Does.Contain("R1-32"));
        }

        /// <summary>`A_peak > Scale` ⇒ `R1_CHECK_23` 拒。</summary>
        [Test]
        public void test_apexAboveScale_isRejected()
        {
            string bad = MinimalJson().Replace("\"a_peak\": \"100\"", "\"a_peak\": \"150\"");
            var ex = Assert.Throws<BakeValidationException>(() => DiseaseRegistryBaker.BakeFromJson(bad));
            Assert.That(ex.Message, Does.Contain("R1-23"));
        }

        /// <summary>急性保持型但 `lethal: false` ⇒ `R1_CHECK_22` 拒。</summary>
        [Test]
        public void test_acuteHoldWithoutLethal_isRejected()
        {
            string bad = MinimalJson().Replace("\"lethal\": true", "\"lethal\": false");
            var ex = Assert.Throws<BakeValidationException>(() => DiseaseRegistryBaker.BakeFromJson(bad));
            Assert.That(ex.Message, Does.Contain("R1-22"));
        }

        /// <summary>缺 `natural_progress` ⇒ 拒(R1.2 铁律:必须存在且可序列化)。</summary>
        [Test]
        public void test_missingNaturalProgress_isRejected()
        {
            string bad = MinimalJson().Replace("\"natural_progress\": {", "\"_removed_np\": {");
            var ex = Assert.Throws<BakeValidationException>(() => DiseaseRegistryBaker.BakeFromJson(bad));
            Assert.That(ex.Message, Does.Contain("natural_progress"));
        }

        /// <summary>空 `diseases` 数组 ⇒ `R1_CHECK_17` 拒(至少 1 个)。</summary>
        [Test]
        public void test_emptyRegistry_isRejected()
        {
            string bad = @"{
  ""schema_version"": 2,
  ""diseases"": []
}";
            var ex = Assert.Throws<BakeValidationException>(() => DiseaseRegistryBaker.BakeFromJson(bad));
            Assert.That(ex.Message, Does.Contain("R1-17"));
        }

        /// <summary>语法坏掉的 JSON ⇒ 阶段 1 词法拒(不是崩)。</summary>
        [Test]
        public void test_malformedJson_isRejectedAtStage1()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => DiseaseRegistryBaker.BakeFromJson("{ \"schema_version\": 2, "));
            Assert.That(ex.Message, Does.Contain("阶段1").Or.Contain("词法"));
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑤ 丢失登记 —— 白名单内但未绑定的键必须**可见**
        // ══════════════════════════════════════════════════════════════════════
        //
        // ADR-014 §三 的未知键门只挡「拼错的键」。一个**拼对的、白名单内的、
        // 却没人读的**键,丢字段是静默的 —— 门看不见它。
        // 下面两条把那种丢失变成可断言的量。

        /// <summary>
        /// 真源文件当前有 3 个键声明了但未绑定。断言**恰是这 3 个** ——
        /// 少一个(有人补了绑定)或多一个(有人加了新键却没绑)都会红。
        /// <para>⚠️ 本条**不**主张这 3 个键的丢失是可接受的。它只保证:
        /// 丢失的量**不随时间静默变化**。清单本身的缩减归各自实现轮。</para>
        /// <para><b>2026-10-23 从 5 键缩到 3 键</b>:<c>signs</c> 已进 <c>BoundRowKeys</c>
        /// (绑定 + 编码);<c>handle</c> 从真源里**删干净了**(GDD <c>:329</c>「派生而非手填」,
        /// binder 对它的手填值现在是硬失败,不再可能出现在未绑定集里)。</para>
        /// <para>剩下的三个各有归口:<c>causes</c> 是**入边**、真源侧未落地(GDD 校验 11);
        /// <c>treatable_by</c> 是**轴的单一天源**(<c>D-9-K</c>),条目不携带是设计;
        /// <c>zh</c> 是本地化文本,不在 9 的数据面内。</para>
        /// </summary>
        [Test]
        public void test_realSource_unboundKeySetIsExactlyKnown()
        {
            var outp = DiseaseRegistryBaker.BakeFromRepo(RepoRoot());

            string[] expected = { "causes", "treatable_by", "zh" };
            Assert.That(outp.UnboundKeys, Is.EqualTo(expected),
                "未绑定键集与登记不符 —— 要么有人补了绑定(改本断言),要么有人加了新键没绑(补绑定或登记)");
        }

        /// <summary>全部键都被绑定时,<c>UnboundKeys</c> 为空(防「恒定非空」的假判据)。</summary>
        [Test]
        public void test_minimalSource_hasNoUnboundKeys()
        {
            var outp = DiseaseRegistryBaker.BakeFromJson(MinimalJson());
            Assert.That(outp.UnboundKeys, Is.Empty,
                "最小合法源文本的每个键都应被绑定 —— 非空说明 BoundRowKeys 与 BindRow 脱节");
        }

        /// <summary>`_` 前缀注记不计入未绑定(它们按设计就不绑定)。</summary>
        [Test]
        public void test_authorNotes_areNotCountedAsUnbound()
        {
            string withNote = MinimalJson().Replace(
                "\"schema_version\": 2,",
                "\"schema_version\": 2,\n  \"_note\": \"注记\",");
            var outp = DiseaseRegistryBaker.BakeFromJson(withNote);
            Assert.That(outp.UnboundKeys, Is.Empty);
        }
    }
}
