// M2 接线轮阶段 1 装配轮 · CompositionRoot 的 EditMode 直测。
//
// 覆盖两条:
//   ① 生产装配真接线 —— 袋中编码器 → IEventSink.Append → 真实 EventStream:Seq 已发号、
//      字节真进 InMemoryBlobPool(ADR-029 乙案:编码 + 入池一体),并可按 Kind 解码回读;
//   ② fail-loud —— 任一依赖为 null ⇒ 具名 ArgumentNullException(不返回半装配袋)。
//
// 判据纪律:确定性、隔离(每测自建装配),不依赖文件 / 网络(Coding Standards)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary><see cref="CompositionRoot"/> / <see cref="CompositionRootServices"/> 单元测试。</summary>
    public class CompositionRootTest
    {
        /// <summary>
        /// 52 出诊启动接线轮(2026-10-25):生产装配必须发导演壳 + 医馆格占位源,
        /// 且驱动持有的源 == 服务袋实例(同源义务的结构性钉 —— 三处消费必须同实例,
        /// IClinicCellSource 接口头注禁令;另两个消费点出现时,断言面在此扩列)。
        /// </summary>
        [Test]
        public void test_assemble_randomEventsDirector_clinicCells_sameInstance()
        {
            CompositionRootServices bag = CompositionRoot.Assemble();

            Assert.NotNull(bag.RandomEventsDirector, "52 导演主循环壳(接线义务①)");
            Assert.NotNull(bag.ClinicCells, "医馆格判据源占位(裁定②)");
            Assert.AreSame(bag.ClinicCells, bag.RandomEventsDirector.ClinicCells,
                "同源单点发放:驱动消费的实例 == 服务袋实例(另立实例 = 同一 tick 两答)");
            Assert.IsFalse(bag.RandomEventsDirector.IsPlayerBound,
                "装配刚完成时未绑定玩家(id 在装配后才铸造)—— 合法状态,非装配失败");
            Assert.AreEqual(0, ((ClinicCellSource)bag.ClinicCells).ConfiguredCount,
                "生产占位 = 空集(裁定②;23/24 接线轮喂真源)");
        }

        [Test]
        public void test_assemble_wiresRealEventStream_roundTrip()
        {
            // Arrange + Act:全自动装配
            CompositionRootServices bag = CompositionRoot.Assemble();

            // Assert:袋内各件齐备(缺一 = 半装配,fail-loud 已保证不返回这种袋)
            Assert.NotNull(bag);
            Assert.NotNull(bag.Stream, "EventStream 实装");
            Assert.NotNull(bag.EventSink, "IEventSink 抽象面");
            Assert.AreSame(bag.Stream, bag.EventSink, "EventSink 即 Stream(同一实装)");
            Assert.NotNull(bag.IdAuthority, "机制 A 发号权威");
            Assert.NotNull(bag.BlobPool, "不可变 blob 池");
            Assert.NotNull(bag.Encoder, "第七抽象点 IPayloadEncoder");
            Assert.NotNull(bag.Presence, "在场登记簿(IPresenceQuery 生产实装)");
            Assert.NotNull(bag.PatientSpawner, "9 的 DiseaseOnset 写者");

            // Act:编码 + 入池(ADR-029 乙案 —— 接口直接出 PayloadRef)
            // (测试面 T3:patientId 刻意取**非默认值** 5 —— 0 恰为 int 默认值,
            //  编码器漏写该字段时 round-trip 仍会「碰巧」相等,判据失真。)
            var payload = new DiseaseOnsetPayload(
                onsetTick: 7, diseaseId: 3, patientId: 5, patientSeed: 123456789L, seq: 0);
            PayloadRef reference = bag.Encoder.Encode(EventKind.DiseaseOnset, payload);

            // Assert:载荷非空(Length = 字节段长度)
            Assert.Greater(reference.Length, 0, "编码即入池,PayloadRef 须带非零长度");

            // Act:经抽象写面进流(Seq = -1 未发号哨兵,由主机 EventStream 发号)
            bag.EventSink.Append(new SimEvent(7, new PatientId(5), -1,
                                              EventKind.DiseaseOnset, reference));

            // Assert:事件真在流内,Seq 已发号 ≥ 0
            Assert.AreEqual(1, bag.Stream.Count, "事件进入真实 EventStream");
            SimEvent stored = bag.Stream.Events[0];
            Assert.AreEqual(EventKind.DiseaseOnset, stored.Kind);
            Assert.GreaterOrEqual(stored.Seq, 0L, "Seq 由 EventStream 发号(O-1 哨兵已消费)");
            Assert.AreEqual(reference.BlobId, stored.Payload.BlobId, "载荷引用随事件头进入流");

            // Assert:字节真进池(不是手搓假引用)
            Assert.IsTrue(bag.BlobPool.TryGetBlob(reference.BlobId, out var blob),
                "BlobId 须能在池中命中");
            Assert.Greater(blob.Length, 0, "池内字节非空");

            // Assert:按 Kind 解码回读 —— 编码 → 池 → 事件头 → 解码全链路闭合
            Assert.IsTrue(PayloadCodec.TryGetPayload(in stored, bag.BlobPool, out DiseaseOnsetPayload decoded),
                "TryGetPayload 解码成功");
            Assert.AreEqual(7, decoded.OnsetTick);
            Assert.AreEqual(3, decoded.DiseaseId);
            Assert.AreEqual(5, decoded.PatientId, "非默认 patientId 须逐位 round-trip(T3)");
            Assert.AreEqual(123456789L, decoded.PatientSeed);
            // 载荷 Seq 字段:生产写者 PatientSpawner.SpawnNext 填**占位 0**
            // (载荷内 Seq ≠ 流 header Seq —— 后者由主机 Append 时发号,O-1 哨兵 -1 消费);
            // 本断言 = 载荷 Seq 字段 round-trip 完好(与 Spawner 同款占位约定)。
            Assert.AreEqual(0, decoded.Seq, "载荷 Seq 字段须 round-trip(占位 0,流 Seq 另发号)");
        }

        [Test]
        public void test_assemble_patientSpawner_writesDiseaseOnsetToBagStream()
        {
            // 测试面 T4:代码面 #5 —— 袋内 PatientSpawner 真接线(9 的 DiseaseOnset 写者,ADR-030 §③),
            // 而非「构造出来但没人调」的摆设。
            // Arrange:全自动装配(计数器从 0 起)
            CompositionRootServices bag = CompositionRoot.Assemble();

            // Act:第一次创建病人(SpawnNext(diseaseId, tick))
            PatientId first = bag.PatientSpawner.SpawnNext(diseaseId: 3, tick: 7);

            // Assert ①:流尾事件 Kind == DiseaseOnset(写入真的落到袋内 EventStream)
            Assert.AreEqual(1, bag.Stream.Count, "首次 SpawnNext 须写入 1 条");
            SimEvent tail = bag.Stream.Events[bag.Stream.Count - 1];
            Assert.AreEqual(EventKind.DiseaseOnset, tail.Kind, "尾事件须为 DiseaseOnset");
            Assert.AreEqual(first, tail.Patient, "事件头 patient_id = Spawner 返回值");

            // Assert ②:header Seq 已发号(≥ 0,非 -1 未发号哨兵)
            Assert.GreaterOrEqual(tail.Seq, 0L, "Seq 由 EventStream 发号(O-1 哨兵已消费)");

            // Assert ③:经生产 PayloadCodec 解码成功且 DiseaseId 匹配
            Assert.IsTrue(PayloadCodec.TryGetPayload(in tail, bag.BlobPool, out DiseaseOnsetPayload decoded),
                "载荷字节须真入池并可解码(ADR-029 乙案:编码 + 入池一体)");
            Assert.IsTrue(bag.BlobPool.TryGetBlob(tail.Payload.BlobId, out var bytes),
                "BlobId 须在池内命中(载荷非空的前提)");
            Assert.Greater(bytes.Length, 0, "解码所得字节非空");
            Assert.AreEqual(3, decoded.DiseaseId, "DiseaseId 须与 SpawnNext 入参一致");
            Assert.AreEqual(first.Value, decoded.PatientId, "载荷 patient_id 与事件头一致");

            // Assert ④:再创建一个 —— patient_id 递增恰 1(机制 A 计数器;不断绝对值,
            // 只断差值,免疫「计数器从几起」的口径漂移)
            PatientId second = bag.PatientSpawner.SpawnNext(diseaseId: 3, tick: 7);
            Assert.AreEqual(1, second.Value - first.Value, "第二次 SpawnNext 的 patient_id 须递增 1");
            Assert.AreEqual(2, bag.Stream.Count, "两次创建 = 流内两条(同 tick 不同 patient_id,不去重)");
            Assert.AreEqual(EventKind.DiseaseOnset, bag.Stream.Events[bag.Stream.Count - 1].Kind);
        }

        [Test]
        public void test_assemble_wiresCasebookSource_toVitalsQuery()
        {
            // sprint-05 T2.1 + G1.4:42 侧只读数源须在装配袋内,
            // 且 revealed 真源 = 查体分册快照(乙案派生态;非 F2 tripwire)。
            CompositionRootServices bag = CompositionRoot.Assemble();

            Assert.NotNull(bag.CasebookSource, "42 侧只读数源须在袋内(T2.1 接线判据)");
            Assert.NotNull(bag.ExaminationLedger, "查体分册须在袋内(G1.4)");
            Assert.NotNull(bag.ExamineDriver, "查体入口驱动须在袋内(G1.4)");

            // Act:spawn 一个真病人(diseaseId=1 = fallback 注册表内)+ 查体
            PatientId patient = bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            bag.VitalsService.OnTickEdge(0);   // τ=0 < Curve.Incubation=20(潜伏)⇒ position=0;
                                               // 本测主旨是「未查体 ⇒ 空行」,与 τ 面无关

            // 未查体 ⇒ 空行(ActiveSignCount 0 / RevealedRows 空 —— 禁假数据)
            bag.CasebookSource.ChangePatient(patient);
            Assert.AreEqual(0, bag.CasebookSource.ActiveSignCount, "未查体 ⇒ 0(空行)");
            Assert.AreEqual(0, bag.CasebookSource.RevealedRows.Count, "未查体 ⇒ 空集");

            // 未建档病人 ⇒ 门退化为 0(42 侧转空行,不把 fail-loud 泄进 UI 帧)
            bag.CasebookSource.ChangePatient(new PatientId(9999));
            Assert.AreEqual(0, bag.CasebookSource.ActiveSignCount,
                "未建档病人 ⇒ 空行(门不抛 —— 异常不得以 UI 帧崩掉呈现层)");

            // fail-loud 仍在门面侧保留(直调 IVitalsQuery 未建档 ⇒ 具名异常)
            Assert.Throws<PatientCourseNotFoundException>(
                () => { var _ = bag.VitalsService.GetVitals(new PatientId(9999)); },
                "未建档病人直调 query 须 fail-loud —— 全零 DTO 与「真无症状」不可区分");
        }

        [Test]
        public void test_assemble_failLoud_missingDeps()
        {
            // Act + Assert:发号权威缺失 ⇒ 具名 ArgumentNullException,不吞不降级
            var exId = Assert.Throws<ArgumentNullException>(
                () => CompositionRoot.Assemble(null, new PresenceRegistry(), new InMemoryBlobPool(), 0UL));
            Assert.AreEqual("idAuthority", exId.ParamName, "异常须带具名参数");

            // Act + Assert:在场登记簿缺失 ⇒ 具名
            var exPresence = Assert.Throws<ArgumentNullException>(
                () => CompositionRoot.Assemble(new IdAuthority(), null, new InMemoryBlobPool(), 0UL));
            Assert.AreEqual("presence", exPresence.ParamName, "异常须带具名参数");

            // Act + Assert:blob 池缺失 ⇒ 具名
            var exPool = Assert.Throws<ArgumentNullException>(
                () => CompositionRoot.Assemble(new IdAuthority(), new PresenceRegistry(), null, 0UL));
            Assert.AreEqual("blobPool", exPool.ParamName, "异常须带具名参数");

            // Act + Assert:三个引用依赖全缺 ⇒ 仍是最先校验的那一个具名(不返回半装配袋)
            var exAll = Assert.Throws<ArgumentNullException>(
                () => CompositionRoot.Assemble(null, null, null, 0UL));
            Assert.AreEqual("idAuthority", exAll.ParamName);
        }

        // ══════════════════════════════════════════════════════════════════
        // W5(2026-10-25 代码评审必修)—— 叙事锚点观察钩子的真判据
        // ══════════════════════════════════════════════════════════════════
        //
        // 病因:`CompositionRoot.cs` 原传 `narrativeContext: null` ⇒ `Checkpoint` /
        //   `SaveOnExit` / `Load` 生产期**一律抛**,存档/读档在生产路径上完全不可用。
        // 用户 2026-10-25 裁定:用已有流事件驱动(不新开 Kind、不动 8/37 的写者)。
        //
        // ⚠️ 判据纪律:本组断言**不得**只验「装配不抛」—— 那是自指判据(把当前实现
        //   写进断言)。必须喂**具体的合成流**,正反两向各断言,且反例是**真正会误判的
        //   那条**:非锚点事件(如 `InjuryOnset`)不得放行 —— 否则 save-scum 通道默开。

        private static SimEvent Ev(EventKind kind, long tick = 1L)
            => new SimEvent(tick, PatientId.None, -1, kind, new PayloadRef(0, 0, 0));

        [Test]
        public void test_narrativeAnchor_emptyStream_isFalse()
        {
            // 空流 ⇒ false(初次启动无叙事事件 —— 规则六「定时兜底」那一半的边界)
            Assert.IsFalse(CompositionRoot.HasNarrativeAnchorEvent(new List<SimEvent>()),
                "空流不得放行(恒真会让 save-scum 通道在生产路径默开)");
        }

        [Test]
        public void test_narrativeAnchor_nonAnchorEventsOnly_isFalse()
        {
            // ★ 反向判据的核心:流里有事件 ≠ 有叙事锚点。
            // 若实现写成 `stream.Events.Count > 0`(我首版的形态),本条**必红** ——
            // 它正是「把任何一条病史事件都当锚点」的 save-scum 通道。
            var events = new List<SimEvent>
            {
                Ev(EventKind.InjuryOnset),
                Ev(EventKind.DiseaseOnset),
                Ev(EventKind.EventRolled),
                Ev(EventKind.ThreatDeferred),
                Ev(EventKind.CareApplied),
            };
            Assert.IsFalse(CompositionRoot.HasNarrativeAnchorEvent(events),
                "非锚点事件集不得放行 —— 「流非空即真」会把每条 InjuryOnset 都当锚点");
        }

        [Test]
        public void test_narrativeAnchor_caseClosed_isTrue()
        {
            // 正向:病例结案(37)= GDD 三锚点之一,有真写者(Sim/CaseCloseWriter.cs)
            Assert.IsTrue(CompositionRoot.HasNarrativeAnchorEvent(
                    new List<SimEvent> { Ev(EventKind.CaseClosed) }),
                "CaseClosed 须命中锚点(GDD 规则六「病例结案(37)」)");
        }

        [Test]
        public void test_narrativeAnchor_patternRecognized_isTrue()
        {
            // 正向:`PatternRecognized` 是**保守补集**(见 HasNarrativeAnchorEvent 的 doc:
            //   它比「脉案落笔」宽,但比「流非空即真」窄得多)
            Assert.IsTrue(CompositionRoot.HasNarrativeAnchorEvent(
                    new List<SimEvent> { Ev(EventKind.PatternRecognized) }),
                "PatternRecognized 须命中锚点(脉案落笔无专属 Kind,取其保守补集)");
        }

        [Test]
        public void test_narrativeAnchor_anchorAfterNonAnchors_isTrue()
        {
            // 次序无关:锚点埋在非锚点事件之间/之后均须命中(实现若只查首尾即红)
            var events = new List<SimEvent>
            {
                Ev(EventKind.InjuryOnset), Ev(EventKind.CaseOpened),
                Ev(EventKind.CaseClosed),                       // ← 锚点在中段
                Ev(EventKind.EventRolled), Ev(EventKind.ThreatDeferred),
            };
            Assert.IsTrue(CompositionRoot.HasNarrativeAnchorEvent(events),
                "锚点位于中段须命中(禁只查首条/末条)");
        }

        [Test]
        public void test_narrativeAnchor_nullStream_throws()
        {
            // fail-loud:null 流不得静默返 false(那会把「配置错误」伪装成「无锚点」)
            var ex = Assert.Throws<ArgumentNullException>(
                () => CompositionRoot.HasNarrativeAnchorEvent(null));
            Assert.AreEqual("events", ex.ParamName);
        }

        [Test]
        public void test_assemble_savePath_narrativeContextStaysFailClosed()
        {
            // 🔁 **2026-10-25 生产接线轮(用户裁定「生产接线今日不为零,现在修」)——
            //    本条从 ④ 的 fail-closed 判据翻转为「已接真源」判据**。
            //
            // 演变轨迹(原文不删,留作闭环记录):
            //   ① 原版:`test_assemble_savePath_...` 断言 `HasNarrativeContextSource == false`
            //      —— 「今日刻意未接」。
            //   ② 评审轮按用户前一裁定翻转为 `IsTrue`(断言已注入 `HasNarrativeAnchorEvent`)。
            //   ③ 字面兑现后暴露**语义违背**:GDD 规则十四要的是**瞬时**量,而流里「曾出现过锚点」
            //      是**累积**量 ⇒ 首次锚点后恒 true ⇒ `AC-7a-19 (c)` 的安全门恒开(fail-open)。
            //   ④ **前版**:接线回退 `null`,断言随之翻回 `false`,并行为验证「Checkpoint 必抛」。
            //   ⑤ **本版**:用户裁定接线当日接通 —— 瞬时源 = `NoPendingMedicalActionQuery`
            //      (急救通道 Armed 态取反;**瞬时内存量,不是流内累积钩子**,③ 的形态不回归);
            //      断言翻转回「已注入 + 已 Bind」,并钉「同一实例」与「兜底在袋」。
            string dir = Path.Combine(Path.GetTempPath(), "s19_w5_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                CompositionRootServices bag = CompositionRoot.Assemble(saveBasePath: dir);

                Assert.NotNull(bag.SaveService, "带 saveBasePath ⇒ SaveService 须在袋内");

                // ① 台账:瞬时源**已**注入且**已 Bind**(装配根在急救段 Bind,返回前完成)
                Assert.IsTrue(bag.SaveService.Ledger.HasNarrativeContextSource,
                    "生产接线轮:瞬时源已接通(⑤),不得再回到 null fail-closed");
                Assert.NotNull(bag.NarrativeContext, "袋须暴露瞬时源(装配单例)");
                Assert.IsTrue(bag.NarrativeContext.IsBound,
                    "Assemble 返回时 Bind 已完成(急救段晚于 SaveService 段,但先于 return)");

                // ② 行为:已注入 ⇒ narrativeContext 门**已过** —— 本装配未传 provider,
                //    失败点前移到数据派生(E-13,`ResolveGlobalConfigVersion` 的既有硬失败)。
                //    断言钉的是「门换了」:异常必须**不再**是 narrativeContext 拒绝,
                //    而是 compose 闭包内的 ConfigVersion 派生失败(E-13 的正确行为)。
                //    ⚠️ 生产(BootRoot)传 DataCorePreloader.Provider ⇒ compose 正常,不抛 E-13。
                var ex = Assert.Throws<InvalidOperationException>(
                    () => bag.SaveService.Checkpoint(new SaveSlot(0)),
                    "无 provider 的装配里 Checkpoint 会走到 compose ⇒ E-13(门已过,失败点在数据派生)");
                StringAssert.Contains("ConfigVersion", ex.Message);
                StringAssert.DoesNotContain("narrativeContext", ex.Message,
                    "失败若仍是 narrativeContext 拒绝 = 瞬时源没接上(⑤ 的判据被破坏)");

                // ③ 兜底在袋(AC-7a-20 定时半边的装配产出)
                Assert.NotNull(bag.CheckpointFallback,
                    "带 saveBasePath ⇒ 定时兜底须与 SaveService 同批装配");

                // ⑤ 锚点触发器在袋(AC-7a-20 事件触发半边 · TD-persist-04 动作① 实现轮):
                //    与兜底同源装配;瞬时源同一实例(触发器预问的门 = 台账门 = 袋暴露,一个量)。
                Assert.NotNull(bag.AnchorCheckpointTrigger,
                    "带 saveBasePath ⇒ 锚点触发器须与兜底同批装配(事件触发半边)");
                var triggerCtx = typeof(AnchorCheckpointTrigger)
                    .GetField("_context", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(bag.AnchorCheckpointTrigger);
                Assert.AreSame(bag.NarrativeContext, triggerCtx,
                    "触发器的瞬时源与袋暴露的须是同一实例(规则十四「一个量」;裁定③ 复用带门入口的门 = 同一量)");

                // ④ **「一个量」机械钉**:SaveService 台账的瞬时源、兜底的瞬时源、袋暴露的
                //    瞬时源**是同一个对象**(装配期单变量;拆实例 = 读档门与兜底门分叉)。
                var ledgerCtx = typeof(SaveSlotLedger)
                    .GetField("_narrativeContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(bag.SaveService.Ledger);
                var fallbackCtx = typeof(CheckpointFallback)
                    .GetField("_context", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(bag.CheckpointFallback);
                Assert.AreSame(bag.NarrativeContext, ledgerCtx,
                    "台账的瞬时源与袋暴露的须是同一实例(规则十四「一个量」)");
                Assert.AreSame(bag.NarrativeContext, fallbackCtx,
                    "兜底的瞬时源与袋暴露的须是同一实例(规则十四「一个量」)");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
        }

        [Test]
        public void test_assemble_noSavePath_fallbackAbsent_contextStillBound()
        {
            // `saveBasePath == null` ⇒ SaveService / CheckpointFallback 均 null(既有合法状态),
            // 但瞬时源**照常构造 + Bind**(它有 7b 呈现侧复用面,不随存档目录存在与否而消失)。
            CompositionRootServices bag = CompositionRoot.Assemble();

            Assert.Null(bag.SaveService, "未指定存档目录 ⇒ SaveService 为 null(既有合法状态)");
            Assert.Null(bag.CheckpointFallback, "SaveService 为 null ⇒ 兜底同 null(同源装配)");
            Assert.Null(bag.AnchorCheckpointTrigger,
                "SaveService 为 null ⇒ 锚点触发器同 null(同源装配;事件触发半边无存档入口可调)");
            Assert.Null(bag.LoadSequencer,
                "SaveService 为 null ⇒ 读档序列器同 null(依赖三件存档侧组件,TD-persist-04 义务②)");
            Assert.NotNull(bag.NarrativeContext, "瞬时源不随存档目录消失(7b 复用面)");
            Assert.IsTrue(bag.NarrativeContext.IsBound, "Bind 照常完成");
        }

        /// <summary>
        /// Load 接线轮(TD-persist-04 义务② · 2026-10-25 三裁全取推荐):
        /// 带 saveBasePath ⇒ 袋内发读档序列器,且其锚点触发器 == 服务袋实例
        /// (同源钉 —— 序列器消费的必须与 BootRoot 边沿 Drain 的是同一观察器,
        /// 另立实例 = ResetCursor 归零的不是生产游标,义务② 判据空转)。
        /// </summary>
        [Test]
        public void test_assemble_loadSequencer_sameTriggerInstance()
        {
            string dir = Path.Combine(Path.GetTempPath(), "s19_loadseq_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                CompositionRootServices bag = CompositionRoot.Assemble(saveBasePath: dir);

                Assert.NotNull(bag.LoadSequencer, "带 saveBasePath ⇒ 读档序列器须在袋内(义务② 接线)");
                Assert.AreSame(bag.AnchorCheckpointTrigger, bag.LoadSequencer.AnchorTrigger,
                    "同源钉:序列器复位的触发器 == 服务袋实例(另立实例 = 义务② 空转)");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
        }
    }
}
