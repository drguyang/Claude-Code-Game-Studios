// 2026-11-02 · `TD-persist-12` 收口 —— **读档后**物品 / 受伤实体 id 高水位必须按新流重构。
//
// 债(改前):`ItemInstanceHighWaterCollector.CollectAndReconstruct` 与
//   `IdAuthority.ReconstructPatientHighWater` 的**生产调用点 = 0**
//   (`grep -rn "CollectAndReconstruct" unity/Assets --include=*.cs | grep -v /Tests/`
//   只命中定义文件自身)。⇒ 读档后两个计数器都停在**装配那一刻**的值
//   ⇒ 存档里更大的 id 会被后续铸造复用 ⇒ fold 里两件物品静默合并
//   (ADR-010 §五登记的原风险)/ 新病人复用已折叠病人的 id
//   (ADR-006 Amendment B 第二十六批)。
//
// 判据(逐条可证伪;**全部走生产装配根 + 生产 `LoadSequencer.afterRestore`**):
//   ① **物品**:存档里的 `DropSpawned.instance_id` 大于装配期计数器 ⇒ 读档后
//      `NextItemInstanceId()` 必须**越过**它(不越过 = 读档后立刻撞号);
//   ② **基线仍计入**:存档流里**零** `instance_id` ⇒ 读档后高水位仍须越过
//      `world_containers` 的基线容器 id(AC-20-03 的「扫描域 = 三流 ∪ 基线」形状);
//   ③ **受伤实体**:存档里病人的 `patient_id` 大于装配期计数器 ⇒ 读档后
//      `NextPatientId()` 必须越过它(玩家 actorId 在启动序第 4 步已铸号,同一空间);
//   ④ **折叠行参与**(③ 的加强):被折叠病人的 id 已从流里抹除、只活在折叠行 ——
//      高水位仍须越过它,否则新病人复用已折叠 id。
//
// ⚠️ **本件刻意不复用 `SaveService` 的往返夹具**:那些件只验「事件条数恢复」,
//   不验**发号器状态** —— 而发号器状态恰恰是本债的唯一观测面。
// ⚠️ **存档字节用 `SaveComposer` 直接组版**(与 `save_slot_load_wiring_test` 同款):
//   组版是 7a 的生产入口,不经它就无法控制「存档里到底有什么 id」。
//
// 写盘边界:全部落 `Path.GetTempPath()`(FsWriteBoundaryGate 允许形态;零仓库锚点)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>读档后的 id 高水位重构(`TD-persist-12`)。</summary>
    [TestFixture]
    public sealed class LoadHighWaterWiringTest
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        private static string CookedDir => Path.Combine(RepoRoot, "unity", "Assets", "DataCooked");

        private string _testDir;
        private CompositionRootServices _bag;
        private IdAuthority _authority;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "dyjq_highwater_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);

            // 真烘焙产物 + 真装配根 ⇒ `LoadSequencer` 的 `afterRestore` 是**生产那一份**。
            AddressablesDataProvider provider = MakeProvider(address =>
            {
                string path = Path.Combine(CookedDir, address);
                Assert.That(File.Exists(path), Is.True,
                    $"盘上产物缺失:{path} —— 重烘:「大医精诚/数据管线」全量烘焙");
                return File.ReadAllBytes(path);
            });

            _bag = CompositionRoot.Assemble(dataProvider: provider, saveBasePath: _testDir);
            Assert.That(_bag.LoadSequencer, Is.Not.Null,
                "前置:给了 saveBasePath ⇒ 生产装配须构造 LoadSequencer(否则本件无被测面)");
            Assert.That(_bag.IdAuthority, Is.InstanceOf<IdAuthority>(),
                "前置:本件需要具 `IdAuthority` 形的发号器(高水位重构是它的具体能力)");
            _authority = (IdAuthority)_bag.IdAuthority;
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_testDir))
            {
                try { Directory.Delete(_testDir, true); } catch { }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // ① 物品:存档 id 越过装配期计数器 ⇒ 读档后必须越过
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 读档后 `NextItemInstanceId()` 必须**大于**存档里的最大 `instance_id`。
        /// <para><b>判别力</b>:把 `afterRestore` 里那次 `CollectAndReconstruct` 删掉 ⇒
        /// 计数器停在装配期值(基线容器 id + 1)⇒ 本测红。</para>
        /// <para>⚠️ 存档 id 刻意取 **10000** —— 远大于 `world_containers` 的基线 id,
        /// 使「越过」只能来自**三流那一半**,不会因基线 id 恰好更大而蒙对。</para>
        /// </summary>
        [Test]
        public void test_persist12_itemHighWater_reconstructedFromLoadedStream()
        {
            const long SaveInstanceId = 10000L;
            WriteSlot(3u, enc => new[] { DropOf(enc, SaveInstanceId, tick: 7L) }, tick: 7L);

            long before = _authority.NextItemInstanceId().Value;
            Assert.That(before, Is.LessThanOrEqualTo(SaveInstanceId),
                $"前置:装配期计数器({before})须小于存档 id({SaveInstanceId})—— " +
                "否则「越过」可能是装配期就已达成的,本测对重构零判别力");

            _bag.LoadSequencer.Load(new SaveSlot(3));

            long after = _authority.NextItemInstanceId().Value;
            Assert.That(after, Is.GreaterThan(SaveInstanceId),
                $"读档后下一号({after})必须越过存档里的最大 instance_id({SaveInstanceId})—— " +
                "不越过 ⇒ 读档后第一件掉落撞号 ⇒ fold 里两件物品静默合并(ADR-010 §五)");
        }

        /// <summary>
        /// **每次读档都重算**(不是「首次读档抬一次就完事」)。
        /// <para>⚠️ **本条是原 ②「基线半边参与」的可判替代** —— 原 ② 的两条断言在
        /// 2026-11-02 证伪轮实测为**假存活**:
        /// ① 「越过基线 id」不可判(`IdAuthority` 只升不降 + 装配期容器段已抬到
        ///    `baselineMax + 1` ⇒ 读档路径传不传基线结果相同,M2 变异后 4 条全绿);
        /// ② 「`afterFirst < SecondInstanceId`」是**空洞正控**(两个不同装配,恒真)。
        /// ⇒ 两条均已删除,只保留这一条能判的。</para>
        /// <para><b>判别力</b>:删掉 `afterRestore` 里的物品重构 ⇒ 第二次读档后停在装配期值 ⇒ 本测红
        /// (与 ① 同被 M1 覆盖 —— 如实登记为**超集**,不宣称独占)。</para>
        /// </summary>
        [Test]
        public void test_persist12_itemHighWater_recomputedOnEachLoad()
        {
            WriteSlot(3u, enc => new[] { OnsetOf(enc, patientId: 3, tick: 7L) }, tick: 7L);
            _bag.LoadSequencer.Load(new SaveSlot(3));

            // 第二份:流里有**远大于**第一份的 instance_id ⇒ 高水位必须再抬一次。
            // ⚠️ **必须换一个新装配**(新池):同一装配的池里已有第一份档的 blob(BlobId 0),
            //    第二份档的 DropSpawned 也落在 BlobId 0 ⇒ 同号**异内容** ⇒ `InMemoryBlobPool`
            //    按 ADR-006 G-2 拒绝装载(实测撞到,报文逐字「已装载且内容不同」)。
            //    这不是缺陷,是池的不可变语义 —— 夹具按它设计。
            const long SecondInstanceId = 50000L;
            CompositionRootServices bag2 = NewBag();
            IdAuthority authority2 = (IdAuthority)bag2.IdAuthority;

            Assert.That(authority2.NextItemInstanceId().Value, Is.LessThanOrEqualTo(SecondInstanceId),
                "前置:新装配的计数器须小于第二份档的 id");

            WriteSlot(4u, enc => new[] { DropOf(enc, SecondInstanceId, tick: 9L) }, tick: 9L);
            bag2.LoadSequencer.Load(new SaveSlot(4));

            long afterSecondLoad = authority2.NextItemInstanceId().Value;
            Assert.That(afterSecondLoad, Is.GreaterThan(SecondInstanceId),
                $"第二次读档后高水位({afterSecondLoad})须越过新档里的 id({SecondInstanceId})—— " +
                "停在装配期值 ⇒ 读档路径没按新流重算");
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 受伤实体:存档病人 id 越过装配期计数器 ⇒ 读档后必须越过
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 读档后 `NextPatientId()` 必须**大于**存档里的最大 `patient_id`。
        /// <para><b>判别力</b>:删掉 `afterRestore` 里的 `ReconstructPatientHighWater` ⇒
        /// 计数器停在装配期值(0)⇒ 本测红。</para>
        /// </summary>
        [Test]
        public void test_persist12_patientHighWater_reconstructedFromLoadedStream()
        {
            const int SavePatientId = 500;
            WriteSlot(3u, enc => new[] { OnsetOf(enc, SavePatientId, tick: 7L) }, tick: 7L);

            int before = _authority.NextPatientId().Value;
            Assert.That(before, Is.LessThanOrEqualTo(SavePatientId),
                $"前置:装配期病人计数器({before})须小于存档 patient_id({SavePatientId})");

            _bag.LoadSequencer.Load(new SaveSlot(3));

            int after = _authority.NextPatientId().Value;
            Assert.That(after, Is.GreaterThan(SavePatientId),
                $"读档后下一病人号({after})必须越过存档里的最大 patient_id({SavePatientId})—— " +
                "不越过 ⇒ 新病人复用已用 id(ADR-006 Amendment B 第二十六批,同一空间)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 折叠行参与(③ 的加强):被折叠病人的 id 只活在 FoldRow 里
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 折叠病人的 id **已从流里抹除**,高水位仍须越过它。
        /// <para>构造:组版时**折掉** 500 号(谓词 = 只折它)⇒ 存档折叠段带 `PatientId = 500`,
        /// 事件段只剩 12 号 ⇒ 若高水位只扫流,就停在 13 ⇒ 复用 500。</para>
        /// <para><b>判别力</b>:把 `afterRestore` 里那段「补扫 `stream.FoldedRows`」删掉 ⇒ 本测红。</para>
        /// <para>⚠️ 折叠谓词只在**组版**侧起作用(读档侧的折叠行来自文件,不由谓词重算)——
        /// 故本测注入的谓词不影响被测面,只用来**造出**带折叠行的存档。</para>
        /// </summary>
        [Test]
        public void test_persist12_patientHighWater_includesFoldedRows()
        {
            const int FoldedPatientId = 500;
            const int SurvivorPatientId = 12;

            // ⚠️ **折叠行只在有终态时产出**(`HistoryFolder.BuildRows` 逐 `outcomes` 建行)——
            //    只给 onset 会被折掉**事件**却不产**折叠行** ⇒ 折叠行里没有那个 id,
            //    判据退化成 ③(首轮实测即此:「病人 500 的事件须已被折叠抹除」为真,
            //    但 `FoldedRows.Count` 为 0)。⇒ 补一条终态(`Died = 2`)。
            WriteSlot(3u,
                enc => new[]
                {
                    OnsetOf(enc, FoldedPatientId, tick: 7L),
                    // ⚠️ `outcome` 是 **`PatientOutcome` 枚举**(Recovered=1 / Died=2),
                    //    **不是** `InjuryState.Deceased = 8` —— 两个枚举,首轮实测拿错了
                    //    (解码期即拒:「outcome=8 不是合法终态」,ADR-032 ②)。
                    OutcomeOf(enc, FoldedPatientId, outcome: 2, tick: 8L),
                    OnsetOf(enc, SurvivorPatientId, tick: 7L),
                },
                tick: 8L, foldOnly: FoldedPatientId);

            int before = _authority.NextPatientId().Value;
            Assert.That(before, Is.LessThanOrEqualTo(FoldedPatientId),
                $"前置:装配期病人计数器({before})须小于被折叠的 id({FoldedPatientId})");

            _bag.LoadSequencer.Load(new SaveSlot(3));

            // 前置:折叠确实发生了 —— 流里不再有 500 号(否则本测与 ③ 同形,零新增判别力)。
            bool stillInStream = false;
            foreach (SimEvent e in _bag.Stream.Events)
                if (e.Patient.Value == FoldedPatientId) { stillInStream = true; break; }
            Assert.IsFalse(stillInStream,
                $"前置:病人 {FoldedPatientId} 的事件须已被折叠抹除(否则本测对「折叠行参与」零判别力)");
            Assert.That(_bag.Stream.FoldedRows.Count, Is.GreaterThan(0),
                "前置:折叠段须真的带回了折叠行");

            int after = _authority.NextPatientId().Value;
            Assert.That(after, Is.GreaterThan(FoldedPatientId),
                $"读档后下一病人号({after})必须越过**被折叠**病人的 id({FoldedPatientId})—— " +
                "折叠行是它唯一的残留,漏扫 ⇒ 新病人复用已折叠 id");
        }

        // ══════════════════════════════════════════════════════════════════
        // 辅助:写档 / 取产物
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 组版并落盘(带校验和)。载荷在**本档自己的池**里编码 ——
        /// `SaveComposer.Compose` 写的是 `PayloadRef`,字节由 `blobPool` 供给。
        /// <para>⚠️ **编码必须用 `Compose` 的同一个池**(故 `build` 拿到 `encoder` 而不是
        /// 调用方自己造池):两个池 ⇒ `PayloadRef` 指向别处 ⇒ 读档时 `Collect` 解码抛
        /// 「查无此 BlobId」(而那正是 ADR-034 消灭的形态)。</para>
        /// </summary>
        /// <param name="slotSeq">槽位号。</param>
        /// <param name="build">用给定的 encoder 造事件(调用方在此决定写哪几个 id)。</param>
        /// <param name="tick">档头 tick。</param>
        /// <param name="foldOnly">只折该病人(<c>-1</c> ⇒ 谁都不折)——
        /// 非负时造出**带折叠行**的档,事件段里该病人的事件被抹除。</param>
        private void WriteSlot(uint slotSeq, Func<PayloadEncoder, IReadOnlyList<SimEvent>> build,
                               long tick, int foldOnly = -1)
        {
            var pool = new InMemoryBlobPool();
            var encoder = new PayloadEncoder(pool);
            var decoder = new PayloadDecoder(pool);

            IReadOnlyList<SimEvent> events = build(encoder);

            IFoldedPredicate folded = foldOnly >= 0
                ? (IFoldedPredicate)new FoldOnly(foldOnly)
                : new FoldNone();

            byte[] unsigned = SaveComposer.Compose(
                events, folded, decoder, new SaveCodec(),
                worldSeed: 1UL, configVersion: 9U, tick: tick,
                timeParams: new SaveTimeParams(36000L, 27000L, 9000L, 252000L, 5L, 6000L, 100L), blobPool: pool);
            File.WriteAllBytes(Path.Combine(_testDir, $"save_{slotSeq}.bin"),
                               SaveService.AddChecksum(unsigned));
        }

        /// <summary>编一条 `DiseaseOnset`(病人 id 半边的观测面)。</summary>
        /// <summary>再造一份同形装配(独立池 / 独立发号器)—— 用于「同一用例里读两份档」。</summary>
        private CompositionRootServices NewBag()
        {
            AddressablesDataProvider provider = MakeProvider(address =>
            {
                string path = Path.Combine(CookedDir, address);
                Assert.That(File.Exists(path), Is.True, $"盘上产物缺失:{path}");
                return File.ReadAllBytes(path);
            });
            return CompositionRoot.Assemble(dataProvider: provider, saveBasePath: _testDir);
        }

        private static SimEvent OnsetOf(PayloadEncoder encoder, int patientId, long tick)
            => new SimEvent(tick, new PatientId(patientId), -1, EventKind.DiseaseOnset,
                encoder.Encode(EventKind.DiseaseOnset,
                    new DiseaseOnsetPayload(tick, 3, patientId, 7L, 0L)));

        /// <summary>编一条 `PatientOutcomeChanged`(折叠行的必要条件 —— 无终态则无折叠行)。</summary>
        private static SimEvent OutcomeOf(PayloadEncoder encoder, int patientId, int outcome, long tick)
            => new SimEvent(tick, new PatientId(patientId), -1, EventKind.PatientOutcomeChanged,
                encoder.Encode(EventKind.PatientOutcomeChanged,
                    new PatientOutcomeChangedPayload(patientId, 3, outcome, 0L)));

        /// <summary>编一条 `DropSpawned`(物品 id 半边的观测面)。</summary>
        private static SimEvent DropOf(PayloadEncoder encoder, long instanceId, long tick)
            => new SimEvent(tick, PatientId.None, -1, EventKind.DropSpawned,
                encoder.Encode(EventKind.DropSpawned,
                    new DropSpawnedPayload(instanceId, new WorldPos(1, 0, 1), 8, 1)));

        /// <summary>经内部构造缝造 provider(internal ⇒ 反射;先例 `data_core_config_version_wiring_test`)。</summary>
        private static AddressablesDataProvider MakeProvider(Func<string, byte[]> byteSource)
        {
            ConstructorInfo ctor = typeof(AddressablesDataProvider).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(Func<string, byte[]>) },
                modifiers: null);
            Assert.That(ctor, Is.Not.Null,
                "`AddressablesDataProvider(Func<string, byte[]>)` 内部构造不见了");
            return (AddressablesDataProvider)ctor.Invoke(new object[] { byteSource });
        }

        private sealed class FoldNone : IFoldedPredicate
        {
            public bool Folded(int patientId) => false;
        }

        private sealed class FoldOnly : IFoldedPredicate
        {
            private readonly int _only;
            public FoldOnly(int only) => _only = only;
            public bool Folded(int patientId) => patientId == _only;
        }
    }
}
