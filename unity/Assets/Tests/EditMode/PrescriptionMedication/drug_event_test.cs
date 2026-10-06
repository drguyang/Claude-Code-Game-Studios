// 权威来源:GDD `design/gdd/prescription-and-medication.md`
//   规则一(病名不给 11 双判据)· 规则三(具名 Kind 与载荷七项)· 规则五(意图即效果)
//   规则十一(与 10 的共享面收窄)
//   AC-11-01 / AC-11-03 / AC-11-22 / AC-11-10 / AC-10-06b 成对 / 恒 Applied / 主机权威
//   ADR-009 Amendment I(写者 = 11)· ADR-024(author=11)· ADR-025 §①(装配清单)
//   ADR-005(主机唯一 Append)· ADR-006(全序键)· ADR-029 §③(唯一载荷构造路径)
//
// 测试对象 = **生产件**:
//   `Sim/Prescription/PrescribeFlow.cs`(唯一写者)
//   `Editor.Tools.Gates/PrescriptionWriterGates.cs`(IL/metadata 门 —— 真判据)
//   `Sim.Contracts/Payloads/HistoryPayloads.cs`(两个成对载荷 struct)
//
// ══════════════════════════════════════════════════════════════════════════════
// NOT-RUN 声明(禁借绿 —— 五处,逐条列名 + 阻塞源)
// ══════════════════════════════════════════════════════════════════════════════
// 1. **AC-11-15 三格矩阵子句** —— BLOCKED-BY-ADR-012(CI 黄金夹具矩阵未激活)。
//    本文件只证 **Mono 侧同进程**逐位自洽;跨进程 / IL2CPP / ARM64 格 **未跑**。
// 2. **AC-11-15 跨进程半边** —— 本文件的重放断言全部在**同一进程**内
//    (`Prescribe` 两次调用逐字段比对)。真跨进程重放 = ADR-012 矩阵的一部分,未跑。
// 3. **AC-10-06b 的「漂移 ⇒ 构建失败」执行体** —— 本文件交付的是**判据**(metadata 比对
//    + 阳性对照),**不是**阶段 2 烘焙器里的执行点。执行体归 ADR-014 阶段 2 / 载体
//    复用 disease story 003(卡 §Implementation Notes 4 逐字如此)。本文件只证
//    「判据可跑且能打出漂移」,**不证**「烘焙器会在漂移时 throw」。
// 4. **AC-11-22 的 7a 序列化白名单本体** —— 本文件证 **codec 两支齐**(`Encode` / `Decode`
//    在 `PayloadCodec.History.cs` 各有具名实现 + `PayloadEncoder` 分派表有该 case),
//    但 7a 持久化的**存档白名单表**(ADR-010 侧)未落地 ⇒ 那半边 BLOCKED-BY-7a。
// 5. **AC-11-10 的「双路径对拍」子句** —— 卡 §AC 写「11 恒 Applied 路径与 10 的 Judge 路径
//    **不共享代码**」。本文件证 **11 侧零该三符号**(真 IL 扫描),**不证**「对拍」——
//    对拍需要两条路径都产载荷并逐位比较,而 10 的路径产出的是 **9 项载荷**(含 method/cause),
//    与 11 的 7 项**形状不同**,「逐位对拍」在形状层就不成立(能对的只有公共 7 项)。
//    ⇒ 对拍降级为 **AC-10-06b 的逐位字段比较**(已交付),卡内「对拍测试」措辞**未逐字兑现**。
// ══════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Prescription;
using NUnit.Framework;
using Gates = DaYiJingCheng.EditorTools.Gates.PrescriptionWriterGates;
using AsmGates = DaYiJingCheng.EditorTools.Gates.AssemblyGates;

namespace DaYiJingCheng.Tests.PrescriptionMedication
{
    /// <summary>
    /// `DrugTreatmentApplied` 的构造 / 零病名 / 写者独占 / 算法独占 / 载荷成对(AC-11-01 ·
    /// AC-11-03 · AC-11-22 · AC-11-10 · AC-10-06b)。
    /// <para>⚠️ 本类**不**断言「门恒过」—— 那种断言是空转。每条判据都配**阳性对照**:
    /// 同一谓词在**另一真实编译产物**上必须打出非空结果。</para>
    /// </summary>
    [TestFixture]
    public class DrugEventTest
    {
        // ── 夹具(与 prescribe_flow_test.cs 同形;同名 fake 在本命名空间内已存在,
        //    故此处**复用**而非重定义 —— 见文件末的复用说明)────────────────────

        private const int DoseBase = 65536;                       // 1.0 Q16.16
        private static readonly ItemKey Drug = new ItemKey("salicylic_acid", ProcessingState.Raw);

        private SpySink _sink;
        private CapturingEncoder _encoder;
        private StubConversion _conversion;
        private StubStore _store;
        private StubPresence _presence;
        private StubSkills _skills;
        private PrescribePorts _ports;

        [SetUp]
        public void SetUp()
        {
            _sink = new SpySink();
            _encoder = new CapturingEncoder();
            _conversion = new StubConversion();
            _store = new StubStore();
            _presence = new StubPresence();
            _skills = new StubSkills();
            _ports = new PrescribePorts(_conversion, _store, _presence, _skills, DoseBase);
        }

        private static DrugProfile MakeProfile(long potencyRaw = 65536L, long halfLifeRaw = 131072L,
                                               string[] indications = null)
            => new DrugProfile
            {
                DoseRange = new DoseRange(1, 5),
                DrugPotency = new Fix(potencyRaw),
                HalfLife = new Fix(halfLifeRaw),
                AxisOffsetByQuality = new[] { new Fix(0L), new Fix(-16384L) },
                Indications = indications,
            };

        private static PrescribeRequest Request(DrugProfile profile, int dose = 2, int quality = 1,
                                                bool isHost = true, int actorId = 7,
                                                int patientId = 3)
            => new PrescribeRequest(Drug,
                                    new PrescriptionEntry(actionId: 1,
                                                          polarity: PrescriptionPolarity.Symptomatic,
                                                          unlockLevel: 0),
                                    actorId: actorId, patientId: new PatientId(patientId),
                                    selectedDose: dose, profile: profile,
                                    selectedQuality: quality, isHost: isHost);

        private PrescribeOutcome Run(in PrescribeRequest req, long tick = 100L)
            => PrescribeFlow.Prescribe(req, tick, _ports, _sink, _encoder);

        private static string RepoRoot => ComputeRepoRoot();

        private static string ComputeRepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        private static string SimDll => AsmGates.ScriptAssemblyPath(Gates.SimAssemblyName);
        private static string ContractsDll => AsmGates.ScriptAssemblyPath("Sim.Contracts");

        // ══════════════════════════════════════════════════════════════════
        // AC-11-01② 零病名 —— 行为断言(端到端)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_drugEvent_sameVitalsDifferentDisease_payloadFieldwiseEqual()
        {
            // AC-11-01②:**行为判据** —— 体征相同、病种不同的两病人,同药同剂
            // ⇒ 产出的载荷**逐字段相等**。这是 8↔11 零数据流的端到端证明。
            //
            // ⚠️ 病种差异如何表达?—— 11 的**全部**输入面里,**只有 `PatientId` 能承载
            //    「不同病人」**。`DrugProfile.Indications` 是 21a 的字段、挂在**入参类型上**,
            //    正是「病名可能溜进 11」的唯一缝隙 ⇒ 夹具刻意给两个 profile **不同的
            //    indications 数组**,断言载荷仍逐字段相等。若 11 将来读了 indications,
            //    本测**必红** —— 这就是它相对反射判据的独立价值。
            var profileWithIndications = MakeProfile(indications: new[] { "cold", "fever" });
            var profileWithOther = MakeProfile(indications: new[] { "plague" });

            Run(Request(profileWithIndications, patientId: 3));
            Run(Request(profileWithOther, patientId: 4));

            Assert.AreEqual(2, _encoder.Payloads.Count, "两病人各一条载荷");
            var a = (DrugTreatmentAppliedPayload)_encoder.Payloads[0];
            var b = (DrugTreatmentAppliedPayload)_encoder.Payloads[1];

            // 逐字段相等(七项全比 —— 不用 struct 默认 Equals,后者对 Fix 的语义不透明)
            Assert.AreEqual(a.Tick, b.Tick, "Tick");
            Assert.AreEqual(a.TreatmentId, b.TreatmentId, "TreatmentId(处置 id)");
            Assert.AreEqual(a.ActorId, b.ActorId, "ActorId(施予者)");
            Assert.AreEqual(a.Polarity, b.Polarity, "Polarity");
            Assert.AreEqual(a.DrugPotency.Raw, b.DrugPotency.Raw, "DrugPotency(逐位)");
            Assert.AreEqual(a.HalfLife, b.HalfLife, "HalfLife");
            Assert.AreEqual(a.Seq, b.Seq, "Seq");

            // 反向:两病人**确实不同** —— 否则上面的「相等」平凡成立(恒真断言守卫)
            Assert.AreEqual(3, _sink.Events[0].Patient.Value);
            Assert.AreEqual(4, _sink.Events[1].Patient.Value);
        }

        [Test]
        public void test_drugEvent_diseaseBearingProfileField_isNotRead_positiveControl()
        {
            // ⚠️ 上一条测的**阳性对照**:证明「改 indications 会改载荷」这条路**是可观测的**,
            //    即 `Indications` 不是被编译器优化掉的死字段。
            //    做法 = 直接读它(在**测试侧**,不是 11 侧),断言两个 fixture 确实不同。
            //    若 21a 将来把 Indications 改名 / 删掉,本测红 ⇒ 上一条的「缝隙」前提失效须复核。
            var p1 = MakeProfile(indications: new[] { "cold", "fever" });
            var p2 = MakeProfile(indications: new[] { "plague" });

            Assert.AreNotEqual(p1.Indications.Length, p2.Indications.Length,
                "两 fixture 的 indications 必须真不同 —— 否则 AC-11-01② 的「病种不同」前提为空");
            Assert.IsNotNull(typeof(DrugProfile).GetProperty("Indications"),
                "DrugProfile.Indications 须存在 —— 它是「病名可能溜进 11」的那道缝");
        }

        [Test]
        public void test_drugEvent_patientIdIsTheOnlyDiseaseAdjacentInput()
        {
            // AC-11-01① 的**反射半边**:11 的公开输入/输出类型集里不得出现病名面**类型**。
            // 递归扫(含泛型实参 / 数组元素),不是只看形参名。
            var reachable = new List<string>();
            foreach (var m in typeof(PrescribeFlow).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                CollectTypeNames(m.ReturnType, reachable);
                foreach (var p in m.GetParameters()) CollectTypeNames(p.ParameterType, reachable);
            }
            foreach (var t in new[] { typeof(PrescribeRequest), typeof(PrescribeOutcome),
                                      typeof(PrescriptionEntry), typeof(DrugProfile) })
                CollectTypeNames(t, reachable);

            // ⚠️ **判据面的诚实切分**(不是遗漏,是反射的能力边界):
            //    反射在**类型可达性**层面只能排除「专门的病名**类型**」——
            //    `DiseaseId` / `DiseaseIdSet` / `TierNamed` / `Severity` / `DiagnosisResult`。
            //    它**排除不了** `Indications` / `Contraindications` 这两个**属性名**:
            //    它们是 21a `DrugProfile`(11 的**入参类型**)上的属性,
            //    而反射的类型可达性**不下钻到成员名**。下一测即证这一点。
            //    ⇒ 能真正排除它们的只有 IL 判据(test_drugEvent_diseaseNameIl_*)。
            var typeLevelForbidden = Gates.DiseaseNameForbidden
                .Where(n => n != "Indications" && n != "Contraindications")
                .ToArray();

            foreach (var bad in typeLevelForbidden)
                CollectionAssert.DoesNotContain(reachable, bad,
                    $"11 的公开签名面触及病名类型 `{bad}`(TR-prescription-001)");

            Assert.Greater(reachable.Count, 0, "反射面为空 ⇒ 断言恒真");
            Assert.IsNotEmpty(typeLevelForbidden, "类型级禁名集为空 ⇒ 断言恒真");
        }

        [Test]
        public void test_drugEvent_reflectionCannotSeeIndications_whichIsWhyIlGateExists()
        {
            // ⚠️ 本测是**判据边界的存在性证明**,不是「11 干净」的证明。
            //    它断言:① `DrugProfile` 上**确实**挂着 `Indications` / `Contraindications`
            //    —— 即「病名溜进 11」这道缝真实存在;② 反射的**类型可达性**面
            //    (上一测的那个集)**看不见**它们(不下钻成员名)。
            //    ⇒ 这两条合起来 = 「反射判据不足以覆盖 AC-11-01①,必须补 IL 判据」的形式证明。
            //    若 21a 将来删掉这两个属性,本测红 ⇒ 须复核 AC-11-01 的威胁模型是否仍成立。
            Assert.IsNotNull(typeof(DrugProfile).GetProperty("Indications"),
                "DrugProfile.Indications 须存在 —— 它是「病名可能溜进 11」的那道缝");
            Assert.IsNotNull(typeof(DrugProfile).GetProperty("Contraindications"),
                "DrugProfile.Contraindications 须存在 —— 同上");

            var reachable = new List<string>();
            CollectTypeNames(typeof(DrugProfile), reachable);
            CollectionAssert.DoesNotContain(reachable, "Indications",
                "反射的**类型**可达性面不应含成员名 —— 若开始含,说明本测的前提变了");
        }

        /// <summary>递归收集类型名(含数组元素 / 泛型实参)。</summary>
        private static void CollectTypeNames(Type t, List<string> into)
        {
            if (t == null) return;
            into.Add(t.Name);
            if (t.HasElementType) CollectTypeNames(t.GetElementType(), into);
            if (t.IsGenericType)
                foreach (var a in t.GetGenericArguments()) CollectTypeNames(a, into);
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-11-01① IL 半边 —— 真扫描 + 阳性对照
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_drugEvent_diseaseNameIl_simPrescriptionNamespaceIsClean()
        {
            var errs = Gates.CheckDiseaseNameIl(
                SimDll, Gates.PrescriptionNamespacePrefix, Gates.DiseaseNameForbidden,
                out int types, out int methods);

            Assert.IsEmpty(errs,
                "11 的实现面不得触及病名符号(TR-prescription-001):\n" + string.Join("\n", errs));
            Assert.Greater(types, 0, "扫描类型数为 0 ⇒ 判据空转");
            Assert.Greater(methods, 0, "扫描方法数为 0 ⇒ 判据空转");
        }

        [Test]
        public void test_drugEvent_diseaseNameIl_predicateIsNotVacuous()
        {
            // ⚠️ **阳性对照(本文件最要紧的一条)**:同一谓词、**同一命名空间**、
            //    换成**确实存在**的禁名 —— 必须命中。若它也零命中,说明谓词是恒空扫描器,
            //    那么上一条的绿毫无意义(这是本工程的高发失效模式,承 AC-10-02 的
            //    「断言恒真、零扫描」教训)。
            //
            // 做法:11 的命名空间里确有 `DoseCalculator` 这个类型,拿它当禁名 ⇒ 必命中。
            var probe = Gates.CheckDiseaseNameIl(
                SimDll, Gates.PrescriptionNamespacePrefix, new[] { "DoseCalculator" },
                out int types, out int methods);

            Assert.IsNotEmpty(probe,
                "同一命名空间下拿真实存在的类型名当禁名竟零命中 —— 谓词空转," +
                "上一条的绿不作数(拒以恒空谓词冒充判据)");
            Assert.Greater(types, 0, "扫描类型数为 0 ⇒ 谓词没读到产物");
            Assert.Greater(methods, 0);
            Assert.IsTrue(probe.Any(e => e.Contains("DoseCalculator")),
                "命中内容须点名 DoseCalculator:" + string.Join(" | ", probe));
        }

        [Test]
        public void test_drugEvent_diseaseNameIl_catchesPropertyReadOfIndications()
        {
            // ⚠️ **威胁模型的直接证伪(2026-10-06 结构侧评审 B-1 重写)**。
            //
            // 原版拿 `Indications` 扫 **`Sim.Contracts`**(属性的**声明处**)⇒ 命中,
            // 于是判「谓词有能力抓」。**但它测错了对象**:威胁在**调用处**
            // (`11` 读 `profile.Indications`),而声明处的命中来自 getter 体内的
            // `ldfld <Indications>k__BackingField` —— 调用点的 IL **不含** backing field。
            // 原版因此给出**假信心**(实测:`IsBannedName("get_Indications")` 曾恒 False)。
            //
            // 现在改测**真威胁形态**:同一个谓词,打在**一个真的调用了属性访问器的命名空间**上。
            // 该命名空间 = `Editor.Tools.Bake`(`CookedWriter.cs:156` 读 `drug.Indications`)
            // —— 它**不在** 11 的禁区内(烘焙层合法),但它是**真调用点**,
            // 故能证明「谓词抓得住属性读取」这条能力本身。
            var on11 = Gates.CheckDiseaseNameIl(
                SimDll, Gates.PrescriptionNamespacePrefix, new[] { "Indications" },
                out _, out _);
            Assert.IsEmpty(on11, "11 侧零读 Indications(TR-prescription-001 的正题):\n"
                                 + string.Join("\n", on11));

            // 阳性对照:真调用点所在的命名空间 ⇒ 必命中。
            var onCaller = Gates.CheckDiseaseNameIl(
                AsmGates.ScriptAssemblyPath("Editor.Tools.Bake"),
                "DaYiJingCheng.EditorTools.Bake", new[] { "Indications" },
                out int cTypes, out int cMethods);
            Assert.IsNotEmpty(onCaller,
                "真调用了 `get_Indications` 的命名空间竟零命中 —— 谓词对**属性访问器**恒假阴性," +
                "上一条的绿不作数(B-1 的正是这条缝)");
            Assert.IsTrue(onCaller.Any(e => e.Contains("get_Indications")),
                "命中须点名访问器 `get_Indications`(证词边界展开生效):"
                + string.Join(" | ", onCaller));
            Assert.Greater(cTypes, 0);
            Assert.Greater(cMethods, 0);
        }

        [Test]
        public void test_drugEvent_isBannedName_expandsAccessorPrefixes()
        {
            // B-1 的**单元级**判据(不依赖任何产物):访问器名必须命中,
            // 且前缀展开**只剥一次**、不误伤无关名。
            var banned = new HashSet<string>(new[] { "Indications", "TierNamed" });

            Assert.IsTrue(Gates.IsBannedName("Indications", banned), "精确名须命中");
            Assert.IsTrue(Gates.IsBannedName("get_Indications", banned), "读访问器须命中(B-1 的缝)");
            Assert.IsTrue(Gates.IsBannedName("set_Indications", banned), "写访问器须命中");
            Assert.IsTrue(Gates.IsBannedName("get_TierNamed", banned), "读访问器(第二例)须命中");

            // 不误伤面:前缀展开只剥一次,且非访问器名不受影响。
            Assert.IsFalse(Gates.IsBannedName("get_get_Indications", banned),
                "只剥一次 —— 非编译器产物不展开");
            Assert.IsFalse(Gates.IsBannedName("MyIndicationsHolder", banned),
                "词边界须拦住非独立词");
            Assert.IsFalse(Gates.IsBannedName("DiagnosisIndicationsX", banned),
                "右边界须拦住非独立词");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-11-22 写者独占 —— 真 IL 扫描 + 双向
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_drugEvent_writerExclusivity_productionCtorSitesAreWhitelisted()
        {
            var errs = Gates.CheckDrugPayloadCtorSites(out int scanned, out var hits);

            Assert.IsEmpty(errs,
                "载荷构造点须 ⊆ 白名单(AC-11-22 / TR-prescription-018):\n" +
                string.Join("\n", errs));
            Assert.Greater(scanned, 0, "扫描装配数为 0 ⇒ 判据空转");

            // 下界断言(测试侧独立复核,不信门自己的自检):
            // 白名单的**每一条**都必须真出现在命中集里 —— 否则白名单是死的。
            foreach (var a in Gates.ProductionCtorSites)
                CollectionAssert.Contains(hits, a,
                    $"白名单条目 {a.Assembly}/{a.Type} 零命中 —— 白名单陈旧(类型改名 / 装配未编译)");
        }

        [Test]
        public void test_drugEvent_writerExclusivity_testAssembliesAreIsolatedButPresent()
        {
            // 测试装配内的构造点**单列**(不进白名单判定),但**必须存在** ——
            // 它是本谓词的天然阳性对照面:若连测试侧都扫不到构造点,
            // 说明谓词根本没在扫 IL(而非「无人违例」)。
            var errs = Gates.CheckDrugPayloadCtorSites(out _, out var hits);
            Assert.IsEmpty(errs);

            var testHits = hits.Where(h => Gates.IsTestAssembly(h.Assembly)).ToList();
            Assert.IsNotEmpty(testHits,
                "测试装配内零构造点 ⇒ 谓词的 IL 扫描面失效(空转),AC-11-22 的绿不作数");
        }

        [Test]
        public void test_drugEvent_writerExclusivity_whitelistCannotBeSilentlyGutted()
        {
            // ⚠️ **下界判据的证伪**(AC-11-22 的「白名单被架空」失效模式):
            //    只断言「无人违例」(上界)是不够的 —— 把白名单写成空集、或让扫描面
            //    塌缩到零装配,上界都平凡成立。故门**必须**同时断言白名单每条真命中。
            //    做法 = 白名单给一条**扫描面内不可能存在**的条目 ⇒ 必红且点名「零命中」。
            string simDll = SimDll;
            if (!File.Exists(simDll))
                Assert.Inconclusive("Sim.dll 未编译 —— 本测不可用(不借绿)");

            var gutted = Gates.CheckDrugPayloadCtorSites(
                new[] { simDll },
                new[] { ("Sim", "DaYiJingCheng.Sim.Prescription.NoSuchWriterType") },
                out int scanned, out var hits);

            Assert.Greater(scanned, 0, "扫描面须真的打开了一个装配");
            Assert.IsNotEmpty(gutted,
                "白名单条目在扫描面内零命中却零报错 —— 下界判据失效(白名单可被静默架空)");
            Assert.IsTrue(gutted.Any(e => e.Contains("零命中")),
                "报错须点名「零命中」:" + string.Join(" | ", gutted));

            // 且该调用**同时**报了「Sim 不在白名单」—— 证明上界判据也在跑(两界互不遮蔽)
            Assert.IsTrue(gutted.Any(e => e.Contains("写者独占")),
                "上界判据须同批报出 Sim 的构造点不在白名单:" + string.Join(" | ", gutted));
        }

        [Test]
        public void test_drugEvent_writerExclusivity_missingArtifactIsRed()
        {
            // 产物缺失 / 扫描面为空 ⇒ 必红(拒以空集冒充绿)—— 与 EmergencyIntegerGates 同纪律。
            var noArtifacts = Gates.CheckDrugPayloadCtorSites(
                Array.Empty<string>(), Gates.ProductionCtorSites, out int scanned, out var hits);
            Assert.IsNotEmpty(noArtifacts, "扫描面为空须报红");
            Assert.AreEqual(0, scanned);
            Assert.IsEmpty(hits);
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-11-10 算法独占 —— 真 IL 扫描 + 阳性对照 + 引用集
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_drugEvent_algorithmExclusivity_simPrescriptionIsClean()
        {
            var errs = Gates.CheckAlgorithmExclusivity(
                SimDll, Gates.PrescriptionNamespacePrefix, Gates.AlgorithmExclusiveForbidden,
                out int types, out int methods);

            Assert.IsEmpty(errs,
                "11 不得定义/引用 SkillMul / ResultMul / JudgeResult(AC-11-10):\n" +
                string.Join("\n", errs));
            Assert.Greater(types, 0, "扫描类型数为 0 ⇒ 判据空转");
            Assert.Greater(methods, 0, "扫描方法数为 0 ⇒ 判据空转");
        }

        [Test]
        public void test_drugEvent_algorithmExclusivity_tenSideIsThePositiveControl()
        {
            // ⚠️ **阳性对照**:同谓词打到 10 的命名空间 —— 那里**确有**这三个符号。
            //    若 10 侧也零命中,则上一条的绿是空转的绿。
            foreach (var name in Gates.AlgorithmExclusiveForbidden)
            {
                var errs = Gates.CheckAlgorithmExclusivity(
                    SimDll, Gates.EmergencyNamespacePrefix, new[] { name }, out _, out _);

                Assert.IsNotEmpty(errs,
                    $"10 侧命名空间竟不含「{name}」—— 阳性对照面失效," +
                    "AC-11-10 的判据无从证伪(该符号或被改名,或谓词空转)");
            }
        }

        [Test]
        public void test_drugEvent_simReferenceFace_zeroEngineAndOnlyContracts()
        {
            var errs = Gates.CheckSimReferenceFace(
                SimDll, Gates.ProjectAssemblyNames(), Gates.SimAllowedProjectReferences,
                out var engineRefs);

            Assert.IsEmpty(errs,
                "`Sim` 引用面须零引擎 + 工程内引用 ⊆ {Sim.Contracts}(ADR-025 §① / 门 A):\n" +
                string.Join("\n", errs));
            Assert.IsEmpty(engineRefs, "引擎/未登记引用须为空");

            // ⚠️ **阳性对照(2026-10-06 结构侧评审 M-1 补)**:上两条断言的是**空集**,
            //    若谓词被掏空(永远返回空)则平凡通过。故须对一个**确实引用引擎**的产物
            //    跑同一谓词 ⇒ 必非空。取 `Gameplay.Presentation`(它必然引用 UnityEngine)。
            var enginePositive = Gates.CheckSimReferenceFace(
                AsmGates.ScriptAssemblyPath("Gameplay.Presentation"),
                Gates.ProjectAssemblyNames(), Gates.SimAllowedProjectReferences,
                out var posEngineRefs);
            Assert.IsNotEmpty(posEngineRefs,
                "对一个**确实引用引擎**的产物跑同谓词竟零引擎命中 —— " +
                "谓词空转,上两条的绿不作数(拒以恒空谓词冒充判据)");
            Assert.IsTrue(posEngineRefs.Any(r => r.StartsWith("UnityEngine", StringComparison.Ordinal)),
                "对照面须真含 UnityEngine:" + string.Join(" | ", posEngineRefs));
        }

        [Test]
        public void test_drugEvent_sourceFace_noMultiplierOrJudgeSymbols()
        {
            // 源码面(与 IL 面互补):剥离注释后,11 目录下零这三个符号。
            // ⚠️ IL 面已覆盖**编译产物**;源码面额外覆盖「被注释掉的死代码」之外的情形
            //    —— 两者判据面等价但**证据不同**,留双份是因为 IL 面依赖产物新鲜度。
            string dir = Path.Combine(RepoRoot, "unity", "Assets", "Sim", "Prescription");
            Assert.IsTrue(Directory.Exists(dir), $"目录不存在: {dir}");

            var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
            // ⚠️ 2026-10-06 QA 侧评审 NIT-2:文件集为空时下面的循环平凡为真 ⇒ 补空集守卫。
            Assert.IsNotEmpty(files, "源码面文件集为空 ⇒ 断言恒真(目录漂移 / 扫描面塌缩)");

            foreach (var f in files)
            {
                string src = StripComments(File.ReadAllText(f));
                foreach (var name in Gates.AlgorithmExclusiveForbidden)
                    Assert.IsFalse(src.Contains(name),
                        $"{Path.GetFileName(f)} 含「{name}」(AC-11-10 算法独占)");
            }
        }

        private static string StripComments(string src)
        {
            src = System.Text.RegularExpressions.Regex.Replace(src, @"/\*.*?\*/", " ",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            src = System.Text.RegularExpressions.Regex.Replace(src, @"//[^\n]*", " ");
            return System.Text.RegularExpressions.Regex.Replace(src, "\"(@?)(\\\\.|[^\"\\\\])*\"", "\"\"");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-10-06b 载荷成对 —— 逐位 + 阳性对照
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_drugEvent_payloadPairing_fieldwiseIdenticalExceptTenSideOnly()
        {
            var errs = Gates.CheckPayloadPairing(ContractsDll, Gates.TenSideOnlyFields,
                                                 out int compared);

            Assert.IsEmpty(errs,
                "11/10 载荷除 method/cause 外须逐位同名同类型同序数(AC-10-06b):\n" +
                string.Join("\n", errs));
            Assert.AreEqual(7, compared,
                "逐位比较的字段数须 = 7(11 的七项)—— 少于 7 说明有一侧字段被删/改名而比较静默缩面");
        }

        [Test]
        public void test_drugEvent_payloadPairing_predicateDetectsDrift()
        {
            // ⚠️ **阳性对照**:把 10 侧的豁免集改成**错的一项**(拿掉 `Cause` 而保留 `Method`),
            //    那么 10 侧剩 8 项 vs 11 侧 7 项 ⇒ **字段数不等**必须被报出。
            //    这证明谓词真在比较,不是恒过。
            var errs = Gates.CheckPayloadPairing(ContractsDll, new[] { "Method" }, out int compared);

            Assert.IsNotEmpty(errs,
                "少豁免一项(10 侧多出 `Cause`)竟零报错 —— 成对判据失效(恒过)");
            Assert.IsTrue(errs.Any(e => e.Contains("字段数不等") || e.Contains("漂移")),
                "报错须点名字段数不等或漂移:" + string.Join(" | ", errs));
            Assert.Greater(compared, 0);
        }

        [Test]
        public void test_drugEvent_payloadPairing_absentArtifactIsRed()
        {
            // 产物缺失 ⇒ 必红(拒以「查不到 = 没问题」冒充绿)—— 承 EmergencyIntegerGates 同款纪律。
            var errs = Gates.CheckPayloadPairing("/tmp/no-such-contracts-011.dll",
                                                 Gates.TenSideOnlyFields, out int compared);
            Assert.IsNotEmpty(errs, "产物缺失须报红");
            Assert.AreEqual(0, compared);
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-11-03 载荷七项齐备(写入期)+ 7a 白名单两支(codec 面)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_drugEvent_payloadSevenItems_allCarryRealValues()
        {
            // 七项齐备的**行为**判据:每一项都须从真实输入推出**可独立复算**的值,
            // 不是默认值占位。任一项为默认(0 / null)即红。
            _store.PeekQuality = 1;
            Run(Request(MakeProfile(potencyRaw: 65536L * DoseBase, halfLifeRaw: 131072L),
                        dose: 1, actorId: 7),
                tick: 321L);

            Assert.AreEqual(1, _encoder.Payloads.Count);
            var p = (DrugTreatmentAppliedPayload)_encoder.Payloads[0];

            Assert.AreEqual(321L, p.Tick, "① tick");
            Assert.AreEqual(1, p.TreatmentId, "② 处置 id = action_id");
            Assert.AreEqual(7, p.ActorId, "③ 施予者 = 玩家 id");
            Assert.AreEqual((int)PrescriptionPolarity.Symptomatic, p.Polarity, "④ polarity");
            Assert.AreEqual(65536L, p.DrugPotency.Raw, "⑤ drug_potency = 1.0 Q16.16(可独立复算)");
            Assert.AreEqual(131072L, p.HalfLife, "⑥ half_life(quality=1 ⇒ offsets[0]=0)");
            Assert.AreEqual(0L, p.Seq, "⑦ Seq = 占位(主机 Append 发号 —— 见 NOT-RUN 注)");

            // 七项**都非零**才叫「齐备」—— 除 Seq 外(它按契约就是占位 0)
            Assert.AreNotEqual(0L, p.Tick, "tick 不得为 0(默认值占位)");
            Assert.AreNotEqual(0, p.TreatmentId);
            Assert.AreNotEqual(0, p.ActorId);
            Assert.AreNotEqual(0L, p.DrugPotency.Raw);
            Assert.AreNotEqual(0L, p.HalfLife);
        }

        [Test]
        public void test_drugEvent_codecHasBothBranchesForThisKind()
        {
            // 7a 白名单**两支齐**(TR-prescription-018 的可执行半边):
            // ① 编码支 —— `PayloadCodec.Encode(DrugTreatmentAppliedPayload)` 存在且往返无损;
            // ② 解码支 —— `PayloadCodec.Decode<DrugTreatmentAppliedPayload>` 还原逐位相同;
            // ③ 分派支 —— `PayloadEncoder` 的 kind→重载表里有该 case(经**行为**证:
            //    用真 encoder 编码,断言不抛 NotSupportedException / InvalidOperationException)。
            var p = new DrugTreatmentAppliedPayload(210L, 5, 999, 1, new Fix(49152L), 60L, 78L);

            byte[] bytes = PayloadCodec.Encode(p);
            Assert.IsNotEmpty(bytes, "编码支须产出非空字节");

            var back = PayloadCodec.Decode<DrugTreatmentAppliedPayload>(
                EventKind.DrugTreatmentApplied, bytes);
            Assert.AreEqual(p.Tick, back.Tick);
            Assert.AreEqual(p.TreatmentId, back.TreatmentId);
            Assert.AreEqual(p.ActorId, back.ActorId);
            Assert.AreEqual(p.Polarity, back.Polarity);
            Assert.AreEqual(p.DrugPotency.Raw, back.DrugPotency.Raw);
            Assert.AreEqual(p.HalfLife, back.HalfLife);
            Assert.AreEqual(p.Seq, back.Seq);

            // ③ 分派支:真 encoder 走一遍(池 = 内存桩)
            var sink = new MemoryBlobSink();
            var encoder = new PayloadEncoder(sink);
            Assert.DoesNotThrow(() => encoder.Encode(EventKind.DrugTreatmentApplied, p),
                "PayloadEncoder 分派表须含本 Kind(ADR-029 §① 完备性)");
            Assert.Greater(sink.Stored.Count, 0, "编码须真入池");
        }

        [Test]
        public void test_drugEvent_codecMissingField_isRejected()
        {
            // 缺任一项 ⇒ 解码期被拒(AC-11-03 的「缺任一项写入期被拒」——
            // 写入期在 11 侧由 struct ctor 强制七参,解码期由 mask 校验强制)。
            // 判据 = 手工构造**缺末项(Seq,tag 7)**的字节流 ⇒ 必抛 InvalidDataException。
            var p = new DrugTreatmentAppliedPayload(1L, 1, 1, 0, new Fix(1L), 1L, 1L);
            byte[] full = PayloadCodec.Encode(p);

            // 尾字段 = tag(1B) + int64(8B) = 9B ⇒ 砍掉尾部 9 字节即「缺 Seq」
            byte[] missing = new byte[full.Length - 9];
            Array.Copy(full, missing, missing.Length);

            Assert.Throws<InvalidDataException>(
                () => PayloadCodec.Decode<DrugTreatmentAppliedPayload>(
                          EventKind.DrugTreatmentApplied, missing),
                "缺字段须被 mask 校验拒绝(RequireCompleteMask)");
        }

        /// <summary>内存 blob 池写面桩(只为驱动 `PayloadEncoder` 的分派路径)。</summary>
        private sealed class MemoryBlobSink : IBlobSink
        {
            public readonly List<byte[]> Stored = new List<byte[]>();
            public int Store(ReadOnlySpan<byte> blob)
            {
                Stored.Add(blob.ToArray());
                return Stored.Count;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 恒 Applied 无分支 + 主机权威
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_drugEvent_noHandThresholdBranch_anyInputStillApplied()
        {
            // 恒 Applied:零技艺玩家 + 极小剂量 + 整剂路径(dose_range 空)⇒ 仍恰一条事件。
            // 不存在 AppliedWeak / Missed 概念 —— 由 IL 面独立证(10 侧有、11 侧无)。
            _skills.Level = 0;

            var profile = MakeProfile();
            profile.DoseRange = null;                 // 整剂路径
            Run(Request(profile, dose: 1));

            Assert.AreEqual(1, _sink.Events.Count, "恒 Applied:任何输入恰一条事件");
            Assert.AreEqual(EventKind.DrugTreatmentApplied, _encoder.Kinds[0]);

            // 源码面:11 目录内零 AppliedWeak / Missed
            string dir = Path.Combine(RepoRoot, "unity", "Assets", "Sim", "Prescription");
            var srcFiles = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
            Assert.IsNotEmpty(srcFiles, "源码面文件集为空 ⇒ 断言恒真(扫描面塌缩)");
            foreach (var f in srcFiles)
            {
                string src = StripComments(File.ReadAllText(f));
                Assert.IsFalse(src.Contains("AppliedWeak"), $"{Path.GetFileName(f)} 含 AppliedWeak");
                Assert.IsFalse(src.Contains("Missed"), $"{Path.GetFileName(f)} 含 Missed");
            }
        }

        [Test]
        public void test_drugEvent_clientContext_zeroAppendAndZeroConsume()
        {
            // 主机权威(AC 卡「主机权威」):客户端上下文 ⇒ 零 Append;编排照跑(意图即效果)。
            var outcome = Run(Request(MakeProfile(), isHost: false));

            Assert.IsTrue(outcome.Applied, "编排照跑(意图即效果,规则五)");
            Assert.AreEqual(0, _sink.Events.Count, "客户端零 Append(ADR-005 主机唯一)");
            Assert.IsNull(outcome.TreatmentEvent, "客户端零事件回执");
            Assert.AreEqual(0, _store.ConsumeCalls, "客户端零本地扣减");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-11-15 重放半边(同进程 —— 见文件头 NOT-RUN 注 1/2)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_drugEvent_sameInputs_bitwiseIdenticalReplay()
        {
            // ⚠️ **同进程**重放(跨进程半边 NOT-RUN —— 归 ADR-012 矩阵)。
            // 输入集**刻意不含技能等级**:两次跑注入**不同**的技能等级,断言载荷逐位相同。
            var profile = MakeProfile(potencyRaw: 123456L, halfLifeRaw: 98765L);

            _skills.Level = 0;
            Run(Request(profile, dose: 3, quality: 2), tick: 777L);
            _skills.Level = 99;                       // ← 唯一变量:技能等级
            Run(Request(profile, dose: 3, quality: 2), tick: 777L);

            var a = (DrugTreatmentAppliedPayload)_encoder.Payloads[0];
            var b = (DrugTreatmentAppliedPayload)_encoder.Payloads[1];

            Assert.AreEqual(a.Tick, b.Tick);
            Assert.AreEqual(a.TreatmentId, b.TreatmentId);
            Assert.AreEqual(a.ActorId, b.ActorId);
            Assert.AreEqual(a.Polarity, b.Polarity);
            Assert.AreEqual(a.DrugPotency.Raw, b.DrugPotency.Raw, "技能等级不得改药效(TR-prescription-017)");
            Assert.AreEqual(a.HalfLife, b.HalfLife, "技能等级不得改半衰期");
            Assert.AreEqual(a.Seq, b.Seq);
        }

        [Test]
        public void test_drugEvent_skillLevelChangesOnlyGrowthNotPayload()
        {
            // 上一条的**阳性对照**:技能等级**确实**在起作用(经成长出口),
            // 否则「技能等级不影响载荷」平凡成立(恒真守卫)。
            var profile = MakeProfile();
            profile.DoseRange = null;                 // 整剂 ⇒ GateHit 恒真(可观测成长)

            _skills.Level = 0;
            Run(Request(profile));
            int emitAtZero = _skills.EmitCalls;

            _skills.Level = 99;
            Run(Request(profile));
            int emitAtNinetyNine = _skills.EmitCalls;

            Assert.AreEqual(1, emitAtZero, "零技艺仍发成长(恒 Applied ⇒ 恒有成长入口)");
            Assert.AreEqual(2, emitAtNinetyNine, "第二次仍发 —— 证明技能桩真的被读到了(非死代码)");
        }
    }
}
