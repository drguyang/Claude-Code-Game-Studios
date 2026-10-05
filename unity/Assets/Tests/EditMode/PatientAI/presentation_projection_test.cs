// patient-ai(13)Story 003 —— 呈现投影:ViewState 优先级 · IPresentPatients 视图 · cue 调度 · Material 通道
//
// 登记落点: tests/unit/patient-ai/presentation_projection_test.cs
// 真身落点: unity/Assets/Tests/EditMode/PatientAI/presentation_projection_test.cs
//
// 权威来源:
//   GDD design/gdd/patient-ai.md §Formulas F-13.5(cue 间隔 / 相位)· F-13.6(ViewState 优先级)
//     · F-13.8(signs[] → MaterialTable)· §Core Rules 十(IPresentPatients 载荷)
//     · AC-13-A4 / A5 · C1 / C2 / C3 / C4 / C5 · D1 / D2 / D3 / D4 / D5 / D6 · F1 / F2 / F3
//   ADR-013 §9 C3(呈现层只渲染;disease_id 不进呈现层,PresentationDtoGuard 递归)
//   ADR-016 §六(13 ↔ 37 单向无环)· ADR-018 §一/§四/§六(44 只触发;无提示音铁律)
//   TR-patient-011/012/018/019/020/023/024
//
// ⚠️ **反空转三件**(承 story-001 / story-002 纪律):
//   ① 负夹具与正测**共用同一台扫描机器**;
//   ② 每条结构断言配影子类型注入 ⇒ 必红且点名;
//   ③ 断言触底到**具体产物 / 字段**。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PatientAI
{
    public class PresentationProjectionTest
    {
        private static readonly CueIntervals Intervals = CueIntervals.Default;

        // ═══════════════════════════════════════════════════════════
        //  AC-13-C1 —— IPresentPatients 载荷递归反射:恰 = {PatientId, WorldPos, 粗状态枚举}
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13c1_presentPatientPayload_isExactlyThreeFields()
        {
            var fields = typeof(PresentPatient)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name).OrderBy(n => n).ToArray();
            CollectionAssert.AreEquivalent(new[] { "Cell", "Id", "State" }, fields,
                "AC-13-C1:`PresentPatient` 载荷恰 = {Id, Cell, State} —— " +
                "无 disease_id / 无 position / 无 trend / 无 signs[]");

            var types = typeof(PresentPatient)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.FieldType).ToArray();
            CollectionAssert.AreEquivalent(
                new[] { typeof(PatientId), typeof(WorldPos), typeof(PresentPatientState) }, types,
                "且字段类型恰 = {PatientId, WorldPos, 粗状态枚举}");
        }

        [Test]
        public void test_ac13c1_presentPatientState_isCoarseEnum_noFloat()
        {
            // 粗状态枚举 = 整数枚举,不是 float 桶 / 不是病种 id
            Assert.IsTrue(typeof(PresentPatientState).IsEnum, "粗状态须是枚举");
            Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(typeof(PresentPatientState)),
                "枚举底层须 int(禁 float 桶 —— TR-patient-010 整数位域纪律)");
            // ⚠️ 四值是契约(与 §States 二 表逐字):Present / AwaitingCare / Collapsed / InTreatment
            var names = Enum.GetNames(typeof(PresentPatientState)).OrderBy(n => n).ToArray();
            CollectionAssert.AreEquivalent(
                new[] { "AwaitingCare", "Collapsed", "InTreatment", "Present" }, names,
                "四值枚举;`Departed`(已离场)不进枚举(视图是快照,离场者直接不在视图里)");
        }

        [Test]
        public void test_ac13c1_guardCatchesDiseaseId_negativeFixture()
        {
            // ⚠️ 影子件:含 `disease_id` 字段 ⇒ `PresentationDtoGuard` 递归扫描须报红且点名。
            //    与正测**共用同一台扫描机器**(反空转三件 ①)。
            var errs = DaYiJingCheng.EditorTools.Gates.PresentationDtoGuard.Scan(typeof(ShadowWithDiseaseId));
            CollectionAssert.IsNotEmpty(errs,
                "影子载荷含 disease_id ⇒ DTO 卫士须报红(AC-13-C1 / AC-37-15 同门)");
            Assert.IsTrue(errs.Any(e => e.ToLowerInvariant().Contains("diseas")),
                "且须点名 disease 族 token:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_ac13c1_guardPassesRealPayload()
        {
            // 真载荷(正测):守卫零报错 —— 证明负夹具的红来自 disease_id 而非机器本身过敏
            var errs = DaYiJingCheng.EditorTools.Gates.PresentationDtoGuard.Scan(typeof(PresentPatient));
            CollectionAssert.IsEmpty(errs,
                "真 `PresentPatient` 载荷不得含疾病族 token:\n" + string.Join("\n", errs));
        }

        /// <summary>影子件:含 `DiseaseId` 字段(供守卫负夹具)。
        /// <para>⚠️ 借住 13 命名空间 —— 守卫按类型名反射,不按命名空间。</para></summary>
        private readonly struct ShadowWithDiseaseId
        {
            public readonly int DiseaseId;
            public readonly int PatientIdValue;
            public ShadowWithDiseaseId(int d, int p) { DiseaseId = d; PatientIdValue = p; }
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-C5 —— ViewState 真值表叉乘 + 优先级锁死
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13c5_viewState_truthTable_overAllCombinations()
        {
            // 全合法组合叉乘:3 BehaviorState × 2 SessionState × 2 SeekingPhase = 12 格
            var behaviors = new[] { BehaviorState.Idle, BehaviorState.Seeking, BehaviorState.Bedridden };
            var sessions = new[] { SessionState.None, SessionState.InTreatment };
            var phases = new[] { SeekingPhase.EnRoute, SeekingPhase.AtClinic };

            int cells = 0;
            foreach (var b in behaviors)
                foreach (var s in sessions)
                    foreach (var ph in phases)
                    {
                        cells++;
                        var got = ViewStateMap.ViewState(b, s, ph);
                        var want = ExpectedViewState(b, s, ph);
                        Assert.AreEqual(want, got,
                            $"逐字一致(F-13.6 / §States 二):({b}, {s}, {ph}) ⇒ {want},得 {got}");
                    }
            Assert.AreEqual(12, cells, "全组合 = 12 格(3×2×2)");
        }

        /// <summary>GDD F-13.6 的**逐字**预期(F-13.6 公式即此顺序)。
        /// <para>⚠️ 本函数是**独立第二实现**(不是调被测代码)—— 正测与被测件若同源,真值表就恒真。</para></summary>
        private static PresentPatientState ExpectedViewState(BehaviorState b, SessionState s, SeekingPhase ph)
        {
            // 优先级 1:会诊中(任一行为态)
            if (s == SessionState.InTreatment) return PresentPatientState.InTreatment;
            // 优先级 2:倒地不起
            if (b == BehaviorState.Bedridden) return PresentPatientState.Collapsed;
            // 优先级 3:已在医馆等待接诊
            if (b == BehaviorState.Seeking && ph == SeekingPhase.AtClinic) return PresentPatientState.AwaitingCare;
            // 优先级 4:在场、可就诊
            return PresentPatientState.Present;
        }

        [Test]
        public void test_ac13c5_priority_inTreatmentBeatsCollapsedAndAwaitingCare()
        {
            // 优先级锁死:`InTreatment` > `Collapsed`
            Assert.AreEqual(PresentPatientState.InTreatment,
                ViewStateMap.ViewState(BehaviorState.Bedridden, SessionState.InTreatment, SeekingPhase.EnRoute),
                "躺床接受查体 ⇒ InTreatment(不是 Collapsed)");
            // `InTreatment` > `AwaitingCare`
            Assert.AreEqual(PresentPatientState.InTreatment,
                ViewStateMap.ViewState(BehaviorState.Seeking, SessionState.InTreatment, SeekingPhase.AtClinic),
                "在医馆就诊中 ⇒ InTreatment(不是 AwaitingCare)");
        }

        [Test]
        public void test_ac13c5_priority_collapsedBeatsAwaitingCare()
        {
            // 优先级锁死:`Collapsed` > `AwaitingCare`(`Bedridden` 时 phase 不参与)
            Assert.AreEqual(PresentPatientState.Collapsed,
                ViewStateMap.ViewState(BehaviorState.Bedridden, SessionState.None, SeekingPhase.AtClinic),
                "倒地不起优先于等待接诊(即使 phase 报 AtClinic)");
        }

        [Test]
        public void test_ac13c5_priorityOrderIsLoadBearing_negativeFixture()
        {
            // ⚠️ 影子件:优先级**写反**(把 Collapsed 放到 InTreatment 之前)⇒ 上一条必红。
            //    与正测共用同一台判据(ExpectedViewState)。
            Assert.AreNotEqual(ExpectedViewState(BehaviorState.Bedridden, SessionState.InTreatment, SeekingPhase.EnRoute),
                               WrongOrderViewState(BehaviorState.Bedridden, SessionState.InTreatment, SeekingPhase.EnRoute),
                "错误优先级(Collapsed 先行)⇒ 放出 Collapsed,与真值 InTreatment 不同 ⇒ 判别力可证");
        }

        /// <summary>影子件:优先级序**故意写反**(供负夹具)。</summary>
        private static PresentPatientState WrongOrderViewState(BehaviorState b, SessionState s, SeekingPhase ph)
        {
            if (b == BehaviorState.Bedridden) return PresentPatientState.Collapsed;      // ← 错序
            if (s == SessionState.InTreatment) return PresentPatientState.InTreatment;
            if (b == BehaviorState.Seeking && ph == SeekingPhase.AtClinic) return PresentPatientState.AwaitingCare;
            return PresentPatientState.Present;
        }

        [Test]
        public void test_ac13c5_jumpCollapse_hasExplanatoryEnums_noIntermediate()
        {
            // Edge Case:跳级(Seeking → Bedridden 无中间帧)的表现可解释性 ——
            // 枚举里**没有**中间态,故跳级 = 直接从 AwaitingCare 落 Collapsed,不制造假过渡态。
            Assert.AreEqual(PresentPatientState.AwaitingCare,
                ViewStateMap.ViewState(BehaviorState.Seeking, SessionState.None, SeekingPhase.AtClinic));
            Assert.AreEqual(PresentPatientState.Collapsed,
                ViewStateMap.ViewState(BehaviorState.Bedridden, SessionState.None, SeekingPhase.AtClinic),
                "同一 phase 下行为态变 Bedridden ⇒ 直接落 Collapsed(无中间枚举值)");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-A5 —— signs[] 只喂 Material,不进 ViewState / Snapshot
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13a5_viewStateSignature_containsNoVitalsDto()
        {
            // 视图投影函数的签名里**不出现 VitalsDto** —— 编译期结构把白名单钉死
            var viewState = typeof(PresentationProjection).GetMethod(nameof(PresentationProjection.ViewState));
            var toPresent = typeof(PresentationProjection).GetMethod(nameof(PresentationProjection.ToPresentPatient));
            CollectionAssert.DoesNotContain(
                viewState.GetParameters().Select(p => p.ParameterType).ToArray(), typeof(VitalsDto),
                "AC-13-A5:ViewState 输入不得含 VitalsDto(signs[] 不得参与状态判定)");
            CollectionAssert.DoesNotContain(
                toPresent.GetParameters().Select(p => p.ParameterType).ToArray(), typeof(VitalsDto),
                "ToPresentPatient 输入不得含 VitalsDto");
        }

        [Test]
        public void test_ac13a5_materialIsOnlyVitalsConsumptionPoint()
        {
            // `Material()` 是 `VitalsDto` 在本文件内的唯一消费点 —— 由输入结构的字段证明
            var inputFields = typeof(PatientProjectionInput)
                .GetFields(BindingFlags.Public | BindingFlags.Instance);
            Assert.IsTrue(inputFields.Any(f => f.FieldType == typeof(VitalsDto)),
                "投影输入**含** VitalsDto(它是投影起点 —— 只喂 Material)");

            // 且 `MaterialTable.Material(in VitalsDto)` 是**直吃** VitalsDto 的那个(白名单最内层)。
            // ⚠️ `in` 参数 ⇒ 形参类型是 **byref**(`VitalsDto&`)⇒ 须与 `MakeByRefType()` 比。
            var direct = typeof(MaterialTable).GetMethod(nameof(MaterialTable.Material));
            Assert.IsNotNull(direct, "MaterialTable.Material(p) 须存在(F-13.8)");
            var directParams = direct.GetParameters().Select(p => p.ParameterType).ToArray();
            Assert.IsTrue(
                directParams.Any(t => t == typeof(VitalsDto) || t == typeof(VitalsDto).MakeByRefType()),
                "MaterialTable.Material 须直接吃 VitalsDto(AC-13-A5 白名单唯一消费点);" +
                "实得:" + string.Join(", ", directParams.Select(t => t.Name)));

            // 且 `PresentationProjection.Material` 走 `PatientProjectionInput`(投影起点)—— 不越权
            var mat = typeof(PresentationProjection).GetMethod(nameof(PresentationProjection.Material));
            Assert.IsNotNull(mat, "PresentationProjection.Material(p) 须存在");
            CollectionAssert.Contains(
                mat.GetParameters().Select(p => p.ParameterType.Name).ToArray(), "PatientProjectionInput&",
                "投影层 Material 走投影输入(in 参数 ⇒ byref 名)");
        }

        [Test]
        public void test_ac13a5_toPresentPatient_outputHasNoVitalsDerivedField()
        {
            // 输出恰 = {Id, Cell, State} —— 上面 C1 已断字段集,此处断**投影结果**不含体征派生量
            var input = new PatientProjectionInput(
                new PatientId(7), BehaviorState.Seeking, SessionState.None, SeekingPhase.AtClinic,
                new WorldPos(3, 0, 4), new VitalsDto(position: 0.9f, trend: 0.1f, signChannelMask: 0xFF, signCount: 9));

            var row = PresentationProjection.ToPresentPatient(input);
            Assert.AreEqual(new PatientId(7), row.Id, "Id 直通");
            Assert.AreEqual(new WorldPos(3, 0, 4), row.Cell, "格直通");
            Assert.AreEqual(PresentPatientState.AwaitingCare, row.State,
                "状态只由 (Behavior, Session, Phase) 定 —— 输入里的高 position / 满 signs[] 不影响它");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-C3 / C4 —— 场外者不投影;13 零生成 / 零删除
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13c3_absentPatientsAreNotProjected()
        {
            // 在场集 = {0,1};id=2 **不在册** ⇒ 不进快照,且**不被调用**
            var source = new FakeProjectionSource();
            var view = new PresentPatientsView(source.Project);

            view.OnPresentEntered(new PatientId(0));
            view.OnPresentEntered(new PatientId(1));
            view.Rebuild();

            Assert.AreEqual(2, view.PresentCount, "在册 2 人");
            Assert.AreEqual(2, view.ProjectionCalls, "⚠️ 投影调用恰 2 次(不在册者零投影)");
            var snap = view.Snapshot();
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, snap.Select(r => r.Id.Value).ToArray(),
                "快照只含在册者");
            Assert.IsFalse(snap.Any(r => r.Id.Value == 2), "场外者不出现在视图");
        }

        [Test]
        public void test_ac13c3_absentNotProjected_isLoadBearing_negativeFixture()
        {
            // ⚠️ 负夹具:影子「全库枚举」实现 —— 与正测**跑同一段断言**,影子须报红。
            //    ⚠️ 原实现断言 `ProjectionCalls > PresentCount`,而影子 `PresentCount => 0`
            //    是硬编码常量 ⇒ `5 > 0` **恒真、零判别力**。改法:**两个实现走同一个被断言的不变量**
            //    ——「投影次数 ≤ 在册数」,影子的在册数与真视图**同源**(都从 `OnPresentEntered` 数),
            //    差别只在影子的 `Rebuild` 无视在册册、按全库枚举 ⇒ 不变量被影子打破。
            var source = new FakeProjectionSource();

            // 真视图:在册 = {0,1},全库 = 5 ⇒ 投影 2 次 ≤ 在册 2 ✓
            var view = new PresentPatientsView(source.Project);
            view.OnPresentEntered(new PatientId(0));
            view.OnPresentEntered(new PatientId(1));
            view.Rebuild();
            Assert.LessOrEqual(view.ProjectionCalls, view.PresentCount,
                "正测前提:真视图投影次数 ≤ 在册数");

            // 影子:同一在册册(2 人),但 Rebuild 按全库(5)枚举 ⇒ 投影 5 次 > 在册 2 ✗
            var shadow = new ShadowProjectAllPatients(source.Project, totalPopulation: 5);
            shadow.OnPresentEntered(new PatientId(0));
            shadow.OnPresentEntered(new PatientId(1));
            shadow.Rebuild();
            Assert.Greater(shadow.ProjectionCalls, shadow.PresentCount,
                "影子按全库枚举 ⇒ 打破「投影次数 ≤ 在册数」同一不变量 ⇒ 正测断言有判别力"
                + $"(影子:调用 {shadow.ProjectionCalls} 次 / 在册 {shadow.PresentCount})");
        }

        [Test]
        public void test_ac13c3_leavingPatient_isImmediatelyGone()
        {
            var source = new FakeProjectionSource();
            var view = new PresentPatientsView(source.Project);
            view.OnPresentEntered(new PatientId(0));
            view.OnPresentEntered(new PatientId(1));
            view.Rebuild();
            Assert.AreEqual(2, view.Snapshot().Count, "前提:两人在册");

            view.OnPresentLeft(new PatientId(0));
            // 离场即刻移出 —— Rebuild 之前也成立(AC-13-C3)
            Assert.AreEqual(1, view.Snapshot().Count, "离场者立即消失(不待 Rebuild)");
            Assert.IsFalse(view.Snapshot().Any(r => r.Id.Value == 0), "id=0 已不在视图");

            view.Rebuild();
            Assert.AreEqual(1, view.ProjectionCalls, "离场者不再被投影");
        }

        [Test]
        public void test_ac13c4_viewNeverSpawnsOrDespawnsPatients()
        {
            // ⚠️ 13 零 spawn / 零 despawn —— 只加行 / 减行(AC-13-C4)。
            //    ⚠️ 原实现只扫**成员名**(`DeclaredOnly`),看不见 `new GameObject()` /
            //    `Object.Destroy(x)` / `Object.Instantiate(p)` —— 这正是「不生成不删除」的
            //    真实违规形态(名字不叫 Spawn 照样生成)。改判 **IL 引用面**:读 `PresentPatientsView`
            //    全方法的 IL,解析每个 `call`/`callvirt` 的目标 → 命中 Unity 实体生 / 灭 API 即红。
            var forbiddenCalls = new[]
            {
                "UnityEngine.GameObject::.ctor",
                "UnityEngine.Object::Destroy",
                "UnityEngine.Object::DestroyImmediate",
                "UnityEngine.Object::Instantiate",
                "UnityEngine.GameObject::Instantiate",
            };
            var hits = ScanTypeForForbiddenCalls(typeof(PresentPatientsView), forbiddenCalls);
            CollectionAssert.IsEmpty(hits,
                "AC-13-C4:视图类型不得调用任何 Unity 实体生成 / 销毁 API"
                + "(13 不生成不删除病人;判据 = IL 引用面,非名字面):\n" + string.Join("\n", hits));
        }

        [Test]
        public void test_ac13c4_ilScan_catchesSpawnCall_negativeFixture()
        {
            // ⚠️ 负夹具:**与正测共用同一台机器**(ScanTypeForForbiddenCalls)。
            //    影子类型在方法体内**真的调用** `new object()` 的等价面 —— 这里用一个必被
            //    命中的已知 API(`System.String::Concat`)模拟「IL 里出现一次 call」的可观测性,
            //    证明扫描器能看见**方法体内**的调用(而非只看见成员名)。
            var hits = ScanTypeForForbiddenCalls(typeof(ShadowWithSpawn),
                new[] { "System.String::Concat" });
            Assert.IsTrue(hits.Any(h => h.Contains("ShadowWithSpawn")),
                "影子 `ShadowWithSpawn.MakeLabel` 体内调 `string.Concat` ⇒ IL 扫描器须报红、且点名声明类型:\n"
                + string.Join("\n", hits));
        }

        [Test]
        public void test_ac13c3_enterIsIdempotent_noDuplicateRows()
        {
            var source = new FakeProjectionSource();
            var view = new PresentPatientsView(source.Project);
            view.OnPresentEntered(new PatientId(4));
            view.OnPresentEntered(new PatientId(4));   // 重复进入
            view.Rebuild();
            Assert.AreEqual(1, view.PresentCount, "重复进入不叠加(同 id 幂等)");
            Assert.AreEqual(1, view.Snapshot().Count, "快照无重行");
        }

        [Test]
        public void test_ac13c3_snapshotOrderIsIdAscending_regardlessOfInsertion()
        {
            // 求值序钉死(承 story-002 同型纪律):插入序打乱 ⇒ 快照序仍升序
            string Run(int[] insertion)
            {
                var source = new FakeProjectionSource();
                var view = new PresentPatientsView(source.Project);
                foreach (int id in insertion) view.OnPresentEntered(new PatientId(id));
                view.Rebuild();
                return string.Join(",", view.Snapshot().Select(r => r.Id.Value));
            }
            Assert.AreEqual("8,17,33,64,128,129", Run(new[] { 8, 17, 33, 64, 128, 129 }));
            Assert.AreEqual("8,17,33,64,128,129", Run(new[] { 129, 33, 17, 8, 128, 64 }),
                "哈希序 ≠ 升序序的键集打乱插入 ⇒ 快照序仍升序(SortedSet 钉死)");
        }

        [Test]
        public void test_ac13b4_resetForLoad_clearsPresentRoster()
        {
            var source = new FakeProjectionSource();
            var view = new PresentPatientsView(source.Project);
            view.OnPresentEntered(new PatientId(0));
            view.Rebuild();
            Assert.AreEqual(1, view.PresentCount, "前提:一人在册");

            view.ResetForLoad();
            Assert.AreEqual(0, view.PresentCount, "重建后在场册清空(由 9 侧重新灌入 —— 13 不自推导)");
            view.Rebuild();
            Assert.AreEqual(0, view.Snapshot().Count, "且快照为空");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-C2 —— 13 不引用 37(单向无环,静态引用断言)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13c2_patientAiSourceDoesNotReferenceCaseSystem()
        {
            // ⚠️ 判据 = **源码面 grep**(GDD `patient-ai.md:1134` AC-13-C2 字面:「grep 断言」)。
            //    ⚠️ 原实现改判**程序集引用面**是错的:37(病例系统)的**程序集当前根本不存在**,
            //    `GetReferencedAssemblies()` 里永远不会有它 ⇒ 断言**恒真、零判别力**(借绿)。
            //    源码面才是可证伪的:一旦有人在 13 目录写下 `using ...Case...`,本测即红。
            var dir = Path.Combine(RepoRoot(), "unity", "Assets", "Gameplay.Presentation", "PatientAI");
            Assert.IsTrue(Directory.Exists(dir), "13 源码目录须存在:" + dir);

            var forbidden = new[] { "CaseSystem", "CaseStream", "DaYiJingCheng.Case",
                                    "ICase", "CaseFile", "CaseOpened", "CaseClosed" };
            var hits = ScanSourceFilesForTokens(dir, "*.cs", forbidden);
            CollectionAssert.IsEmpty(hits,
                "AC-13-C2:13 源码不得引用 37 病例系统(单向无环 —— 37 读 13,13 不引用 37):\n"
                + string.Join("\n", hits));
        }

        [Test]
        public void test_ac13c2_sourceScanCatchesCase_negativeFixture()
        {
            // ⚠️ 负夹具:**与正测共用同一台机器**(ScanSourceFilesForTokens)。
            //    在临时目录造一个含 `using DaYiJingCheng.Case;` 的假源文件 ⇒ 同一扫描器须报红。
            var tmp = Path.Combine(Path.GetTempPath(), "ac13c2_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            try
            {
                File.WriteAllText(Path.Combine(tmp, "ShadowCaseConsumer.cs"),
                    "using DaYiJingCheng.Case;\nnamespace X { class Y { } }\n");

                var hits = ScanSourceFilesForTokens(tmp, "*.cs", new[] { "DaYiJingCheng.Case", "CaseSystem" });
                Assert.IsTrue(hits.Any(h => h.Contains("ShadowCaseConsumer.cs")),
                    "影子源码含 `using DaYiJingCheng.Case` ⇒ 同一扫描器须报红、且须点名文件:\n"
                    + string.Join("\n", hits));
            }
            finally { Directory.Delete(tmp, recursive: true); }
        }

        /// <summary>运行时造一个只含一个空类型的临时程序集(供名字面扫描负夹具)。
        /// <para>⚠️ 用 .NET Standard 的 `AssemblyBuilder`(Mono 下可用);
        /// 若平台不支持 `RunAndCollect`,退回 `Run`(不卸载,但测试进程短命)。</para></summary>
        private static Assembly BuildThrowawayAssemblyWithType(string typeName)
        {
            var an = new System.Reflection.AssemblyName("ShadowFixtures_" + Guid.NewGuid().ToString("N"));
            var ab = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(
                an, System.Reflection.Emit.AssemblyBuilderAccess.Run);
            var mb = ab.DefineDynamicModule("m");
            mb.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class).CreateType();
            return ab;
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-D4 / D6 —— CueInterval 分段常函数 + Intensity 夹取
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13d6_piecewiseConstant_observableThroughDecide()
        {
            // ⚠️ 原实现在**结构面**上循环读同一个 `readonly` `Intervals.For(tier)` 50 次
            //    (`x == x`,恒真),再把三个字面量断言一遍 —— 与「分段常函数」这一命题无关。
            //    ⚠️ 真正可证伪的命题是:**同一个 `position` 值不改变 cue 间隔** —— 即
            //    AC-13-D6 反幻想机械护栏(间隔按档取,不按 position 连续插值)。判据必须落在
            //    **行为面**上:同档、同 id,扫不同 `position`,`Decide` 给出的**发射时刻恒等**。
            var intervals = Intervals;
            var id = new PatientId(7);
            var gate = TerminalFlag.None;   // 未锁存

            int emittedTickAt(float position)
            {
                // 从入场起逐 tick 扫,记录**首次 due** 的 tick ⇒ 该 tick 由 `interval` 决定
                for (int t = 0; t < 200; t++)
                {
                    var d = PatientCueSchedule.Decide(
                        BehaviorState.Idle, gate, t, lastEmitTick: -1, id, SymptomTier.Severe,
                        position, intervals, breathAlive: true, CueKind.Cough, entryTick: 0);
                    if (d.EmitOneShot) return t;
                }
                return -1;
            }

            int baseline = emittedTickAt(0.0f);
            Assert.GreaterOrEqual(baseline, 0, "前提:0.0 档必有首拍");
            // 同档内扫 position:首拍 tick 必须**逐字相同**(间隔不随 position 变)
            foreach (float p in new[] { 0.1f, 0.25f, 0.5f, 0.9f, 1.0f, 3.0f })
                Assert.AreEqual(baseline, emittedTickAt(p),
                    $"position={p} ⇒ 首拍 tick 须与 baseline 恒等(间隔按档取,不按 position 插值,AC-13-D6)");

            // ⚠️ **首拍半边不够**:`Phase` 内部用的是 `intervals.For(tier)`(与调用点无关的
            //    纯函数),故即便调用点把 interval 写成 position 的函数,**首拍也不变** ——
            //    若只测首拍,一个「间隔随 position 变」的实现(违 AC-13-D6 本意)会**漏网**
            //    (2026-10-05 变异实证:MUT-H 注入 `interval + (int)(position*3)` 时首拍半边仍绿)。
            //    ⇒ 必须再测**第二拍**(稳态):发过一拍后,下一拍间隔**同样不随 position 变**。
            int secondIntervalAt(float position)
            {
                int firstDue = baseline;
                for (int t = firstDue + 1; t < firstDue + 400; t++)
                {
                    var d = PatientCueSchedule.Decide(
                        BehaviorState.Idle, gate, t, lastEmitTick: firstDue, id, SymptomTier.Severe,
                        position, intervals, breathAlive: true, CueKind.Cough, entryTick: 0);
                    if (d.EmitOneShot) return t - firstDue;
                }
                return -1;
            }
            int steady = secondIntervalAt(0.0f);
            Assert.AreEqual(intervals.For(SymptomTier.Severe), steady,
                "稳态间隔须恰 = 档值(Severe 档 = 40)");
            foreach (float p in new[] { 0.1f, 0.5f, 0.9f, 1.0f, 3.0f })
                Assert.AreEqual(steady, secondIntervalAt(p),
                    $"position={p} ⇒ **稳态间隔**须与档值恒等(间隔不随 position 插值 —— "
                    + "首拍半边测不到这条,MUT-H 型缺陷只有稳态半边能抓,AC-13-D6)");

            // 反向:换档 ⇒ 间隔真的变(证明上面不是「全恒等」的空断言)
            int mildTierFirst = -1;
            for (int t = 0; t < 400; t++)
            {
                var d = PatientCueSchedule.Decide(
                    BehaviorState.Idle, gate, t, lastEmitTick: -1, id, SymptomTier.Mild,
                    0.5f, intervals, breathAlive: true, CueKind.Cough, entryTick: 0);
                if (d.EmitOneShot) { mildTierFirst = t; break; }
            }
            Assert.AreNotEqual(baseline, mildTierFirst,
                "Mild 档首拍须异于 Severe 档 ⇒ 证明首拍真随档变(上面的恒等有判别力)");
        }

        [Test]
        public void test_ac13d6_cueInterval_forSignature_takesTierNotPosition()
        {
            // 结构性判据:`For` 的形参**恰** = 一个 SymptomTier(**无 position / 无 float**)
            var forM = typeof(CueIntervals).GetMethod(nameof(CueIntervals.For));
            var ps = forM.GetParameters();
            Assert.AreEqual(1, ps.Length, "For 恰一个形参 —— 分档不插值");
            Assert.AreEqual(typeof(SymptomTier), ps[0].ParameterType,
                "形参须是档枚举,**不是** float position(反幻想机械护栏 AC-13-D6)");
        }

        [Test]
        public void test_ac13d6_tierOrdering_isHardConstraint()
        {
            // 档间有序校验(§Tuning 三):0 < Severe < Mild ≤ Recover
            CollectionAssert.IsEmpty(CueIntervals.Validate(Intervals), "合法样例须零错");
            Assert.IsTrue(CueIntervals.Validate(new CueIntervals(90, 40, 120)).Any(e => e.Contains("Severe")),
                "Severe ≥ Mild ⇒ 报错点名 Severe(重档比轻档疏,方向不可辨)");
            Assert.IsTrue(CueIntervals.Validate(new CueIntervals(40, 120, 90)).Any(e => e.Contains("Mild")),
                "Mild > Recover ⇒ 报错点名 Mild(快好了咳得更凶)");
            Assert.IsTrue(CueIntervals.Validate(new CueIntervals(0, 90, 120)).Any(e => e.Contains("Severe")),
                "Severe = 0 ⇒ 报错(除零风险)");
        }

        [Test]
        public void test_ac13d4_intensity_clampsOutOfRange_noWraparound()
        {
            // ⚠️ **AC-13-D4 的核心**:越界 position 不得**静默回绕**。
            //    原稿缺陷:round(1.5 × 255) = 383 ⇒ 塞 byte ⇒ 回绕成 127(重病看起来像轻病)。
            Assert.AreEqual((byte)255, PatientCue.Intensity(1.5f),
                "position = 1.5 ⇒ round(382.5) = 383 ⇒ 夹取 255(**不是**回绕的 127)");
            Assert.AreEqual((byte)0, PatientCue.Intensity(-3f), "position = −3 ⇒ 夹取 0(无负数回绕)");
            Assert.AreEqual((byte)255, PatientCue.Intensity(9f), "position = 9 ⇒ 夹取 255");
            Assert.AreEqual((byte)255, PatientCue.Intensity(float.MaxValue), "极端大 ⇒ 255(无溢出)");
            Assert.AreEqual((byte)0, PatientCue.Intensity(float.MinValue), "极端小 ⇒ 0");
        }

        [Test]
        public void test_ac13d4_intensity_inRange_mapsLinearly()
        {
            Assert.AreEqual((byte)0, PatientCue.Intensity(0f));
            Assert.AreEqual((byte)128, PatientCue.Intensity(0.5f), "round(0.5 × 255) = round(127.5) = 128(远离零)");
            Assert.AreEqual((byte)255, PatientCue.Intensity(1f));
        }

        [Test]
        public void test_ac13d4_intensity_isMonotonicNonDecreasing()
        {
            // ⚠️ 原 `scanAlwaysInByteRange` 是**恒真**的:返回值类型就是 `byte`,
            //    `Assert.GreaterOrEqual(i, (byte)0)` / `LessOrEqual(i, 255)` 被类型系统先证,
            //    与实现无关(删)。改测**有判别力的性质**:强度对 position **单调不减**
            //    —— 若实现误用 `(byte)raw` 回绕,1.5 → 127 < 0.5 的 128 ⇒ 本测即红。
            byte prev = 0;
            for (float p = 0f; p <= 1.01f; p += 0.05f)
            {
                byte cur = PatientCue.Intensity(p);
                Assert.GreaterOrEqual(cur, prev,
                    $"强度须对 position 单调不减:p={p:F2} 得 {cur} < 前值 {prev}(回绕会打破单调)");
                prev = cur;
            }
            Assert.AreEqual((byte)255, prev, "扫到 p ≥ 1 ⇒ 终值 255");
        }

        [Test]
        public void test_ac13d4_wraparound_isDetectable_negativeFixture()
        {
            // ⚠️ 负夹具:影子「直接 (byte)round」实现 ⇒ 1.5 会回绕成 127,与正解 255 不同 ⇒ 判据有判别力。
            byte shadow = unchecked((byte)(float)Math.Round(1.5f * 255f, MidpointRounding.AwayFromZero));
            Assert.AreEqual((byte)127, shadow, "影子实现确会回绕到 127(证正测的 255 断言之必要性)");
            Assert.AreNotEqual(shadow, PatientCue.Intensity(1.5f), "⇒ 正解(255)与影子(127)可辨");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-D5 —— cue 路径零 PRNG;相位稳定 + 去同步
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13d5_cuePath_hasNoPrng_ilReferenceScan()
        {
            // ⚠️ 零 PRNG 是**引用面**判据 —— 走 IL 扫描(与 story-002 E3 同机器),
            //    因 `System.Random` / `UnityEngine.Random` 是 call 目标(外来引用)。
            var forbidden = new[]
            {
                "System.Random::", "System.Random::Next",
                "UnityEngine.Random::", "System.Security.Cryptography",
            };
            var hits = ScanAssemblyForForbiddenRefs(typeof(PatientCue), forbidden);
            CollectionAssert.IsEmpty(hits,
                "AC-13-D5:cue 路径/生产程序集禁 PRNG(随机源):\n" + string.Join("\n", hits));
        }

        [Test]
        public void test_ac13d5_prngScan_catchesShadow_negativeFixture()
        {
            var hits = ScanAssemblyForForbiddenRefs(typeof(ShadowWithRandom), new[] { "System.Random::" });
            CollectionAssert.IsNotEmpty(hits, "影子含 System.Random ⇒ IL 扫描器须报红");
            Assert.IsTrue(hits.Any(h => h.Contains("System.Random")), "且须点名:\n" + string.Join("\n", hits));
        }

        /// <summary>影子件:含真实 `new Random().Next()`(供 PRNG IL 扫描负夹具)。</summary>
        private static class ShadowWithRandom
        {
            public static int Roll() { return new Random(12345).Next(); }
        }

        /// <summary>影子件:含真实 `VisualElement` 引用(供 AC-13-A4 命名空间扫描负夹具)。
        /// <para>⚠️ 借住 13 命名空间 —— 命名空间限定扫描覆盖它。
        /// `VisualElement` 住 `UnityEngine.UIElements`(UnityEngine 模块),
        /// 本测试程序集已引 UnityEngine ⇒ 引用可解析。</para></summary>
        private static class ShadowWithVisualElement
        {
            public static void Touch(UnityEngine.UIElements.VisualElement ve)
            {
                if (ve != null) ve.EnableInClassList("x", true);
            }
        }

        [Test]
        public void test_ac13d5_phase_isStableForSameId()
        {
            // 同 id 重放 ⇒ 相位逐位一致(纯函数,无 PRNG)
            var id = new PatientId(42);
            int first = PatientCue.Phase(id, SymptomTier.Severe, Intervals);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(first, PatientCue.Phase(id, SymptomTier.Severe, Intervals),
                    "同 id 同档 ⇒ 相位恒等(可重建)");
        }

        [Test]
        public void test_ac13d5_phase_inDesyncRange_andWithinInterval()
        {
            // 相位 ∈ [0, interval) —— 且异 id 去同步(分布抽查:不是全同值)
            var phases = new List<int>();
            for (int i = 0; i < 64; i++)
            {
                int ph = PatientCue.Phase(new PatientId(i), SymptomTier.Mild, Intervals);
                Assert.GreaterOrEqual(ph, 0, $"id={i} 相位非负");
                Assert.Less(ph, Intervals.For(SymptomTier.Mild), $"id={i} 相位 < interval");
                phases.Add(ph);
            }
            // 去同步:64 个 id 至少产生 > 1 个不同相位值(否则「错开」是假的)
            Assert.Greater(phases.Distinct().Count(), 1,
                "异 id ⇒ 相位去同步(不是全同值)");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-D2 / D3 —— 死亡 = 呼吸层停止,无一次性播报
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13d3_terminal_endsBreathAndEmitsNoOneShot()
        {
            // ⚠️ **本 story 最承重的一条**(AC-13-D3):终局锁存 ⇒ 呼吸层 `End` + 零一次性 cue。
            //    ⚠️ `Terminal` 是**锁存位**不是 `BehaviorState` —— 遍历三个行为态
            //    证明「锁存压过一切行为态」,而非把 `Down` 当状态用。
            var deceased = new TerminalFlag(true, TerminalCause.Deceased);
            foreach (var behavior in new[] { BehaviorState.Idle, BehaviorState.Seeking, BehaviorState.Bedridden })
            {
                var d = PatientCueSchedule.Decide(
                    behavior, deceased, tick: 100, lastEmitTick: 90,
                    new PatientId(1), SymptomTier.Severe, position: 0.9f,
                    Intervals, breathAlive: true, CueKind.Groan, entryTick: 0);

                Assert.IsFalse(d.EmitOneShot,
                    $"终局(behavior={behavior})⇒ 不发一次性 cue(**无临终一声**)");
                Assert.AreEqual(BreathAction.End, d.Breath,
                    "且呼吸层须 `End`(终局的表现 = 呼吸停止,不是播报)");
                Assert.AreEqual(0, d.PostureTier,
                    "且姿态落**最静止档**(AC-13-D3 的第二半 —— 由字段承载,非空断言)");
            }
        }

        [Test]
        public void test_ac13d3_terminal_latchesRegardlessOfBehaviorOrPosition()
        {
            // ⚠️ **锁存语义**:一旦置位 ⇒ 无论 position 回升多少,呼吸层都不再起(不复活)。
            //    用**痊愈锁存**的场景(position 极低、trend ≤ 0)证明锁存压过 position。
            var recovered = new TerminalFlag(true, TerminalCause.Recovered);
            var d = PatientCueSchedule.Decide(
                BehaviorState.Idle, recovered, tick: 1, lastEmitTick: -1,
                new PatientId(1), SymptomTier.Recover, position: 0.0f,
                Intervals, breathAlive: true, CueKind.Cough, entryTick: 0);
            Assert.AreEqual(BreathAction.End, d.Breath,
                "终局即便 position = 0 ⇒ 呼吸层仍 End(痊愈与死亡皆锁存)");
            Assert.IsFalse(d.EmitOneShot, "且零一次性 cue");
            Assert.AreEqual((byte)0, d.Intensity, "且零强度(无终局播报)");
        }

        [Test]
        public void test_ac13d3_comaKeepsBreathing_terminalDoesNot()
        {
            // ⚠️ **昏迷 vs 死亡只靠呼吸层区分**(AC-13-D3 / TR-019):
            //    昏迷(Bedridden)⇒ 呼吸层**仍在**(弱);终局锁存 ⇒ 呼吸层**消失**。
            Assert.IsTrue(PatientCueSchedule.BreathShouldLive(TerminalFlag.None),
                "未锁存(含昏迷 Bedridden)⇒ 呼吸层仍活");
            Assert.IsFalse(PatientCueSchedule.BreathShouldLive(new TerminalFlag(true, TerminalCause.Deceased)),
                "死亡锁存 ⇒ 呼吸层停");
            Assert.IsFalse(PatientCueSchedule.BreathShouldLive(new TerminalFlag(true, TerminalCause.Recovered)),
                "痊愈锁存 ⇒ 呼吸层亦停(13 不向玩家报告是哪一种)");
        }

        [Test]
        public void test_ac13d3_comaAndDeceased_differOnlyInBreathLayer()
        {
            // 昏迷与死亡在 13 侧的唯一差别 = 呼吸层;姿态差异归 44/25 表现,不靠音效硬报。
            var coma = PatientCueSchedule.Decide(
                BehaviorState.Bedridden, TerminalFlag.None, 10, -1,
                new PatientId(1), SymptomTier.Severe, 0.95f, Intervals, breathAlive: false, CueKind.Groan, entryTick: 0);
            Assert.AreEqual(BreathAction.Begin, coma.Breath, "昏迷 ⇒ 呼吸层起(弱,但不断)");

            var dead = PatientCueSchedule.Decide(
                BehaviorState.Bedridden, new TerminalFlag(true, TerminalCause.Deceased), 10, -1,
                new PatientId(1), SymptomTier.Severe, 0.95f, Intervals, breathAlive: true, CueKind.Groan, entryTick: 0);
            Assert.AreEqual(BreathAction.End, dead.Breath,
                "同一行为态 + 同 position,仅锁存位不同 ⇒ 呼吸层从 Begin 变 End(唯一判据)");
        }

        [Test]
        public void test_ac13d3_breathingBeginsWhenPatientIsAlive()
        {
            // 稳态:活着但呼吸层未起 ⇒ `Begin`;已在跑 ⇒ `None`(不重复起)
            var begin = PatientCueSchedule.Decide(
                BehaviorState.Seeking, TerminalFlag.None, 10, -1, new PatientId(1), SymptomTier.Mild, 0.5f,
                Intervals, breathAlive: false, CueKind.Cough, entryTick: 0);
            Assert.AreEqual(BreathAction.Begin, begin.Breath, "活人 + 呼吸层未起 ⇒ Begin");

            var steady = PatientCueSchedule.Decide(
                BehaviorState.Seeking, TerminalFlag.None, 10, -1, new PatientId(1), SymptomTier.Mild, 0.5f,
                Intervals, breathAlive: true, CueKind.Cough, entryTick: 0);
            Assert.AreEqual(BreathAction.None, steady.Breath, "呼吸层已在跑 ⇒ 不重复 Begin");
        }

        [Test]
        public void test_ac13d2_oneShotCarriesNoStateChangeSemantics()
        {
            // AC-13-D2:一次性 cue **不携带状态变化语义** —— `CueDispatch` 只有
            // {EmitOneShot, Kind, Intensity, Breath},**没有**「状态从 X 变到 Y」字段。
            var fields = typeof(CueDispatch)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name).OrderBy(n => n).ToArray();
            // ⚠️ 2026-10-05:`PostureTier` 是 AC-13-D3 姿态半边的**承载场**(档位,非 from/to 对),
            //    其加入**不**引入状态播报语义 —— 无 `FromState`/`ToState` 这类「从 X 变到 Y」字段。
            CollectionAssert.AreEquivalent(
                new[] { "Breath", "EmitOneShot", "Intensity", "Kind", "PostureTier" }, fields,
                "AC-13-D2:调度决策无 from/to 状态字段(无状态播报语义);PostureTier 是档位非状态对");
        }

        [Test]
        public void test_ac13d3_terminalDispatch_isSilent_noEndSting()
        {
            // 终局这件事**不被播报**:终局调度 = 静默 + End,强度为 0(无临终声)
            var d = PatientCueSchedule.Decide(
                BehaviorState.Idle, new TerminalFlag(true, TerminalCause.Deceased), 5, 0,
                new PatientId(3), SymptomTier.Severe, 1f, Intervals, breathAlive: true, CueKind.Groan, entryTick: 0);
            Assert.IsFalse(d.EmitOneShot, "终局零一次性 cue");
            Assert.AreEqual((byte)0, d.Intensity, "终局强度 = 0(即使 position = 1)");
            Assert.AreEqual(BreathAction.End, d.Breath, "唯一动作 = 呼吸层收尾");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-D1 —— MaterialTable(F-13.8)存在 + signs[] → 材质映射
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13d1_materialTable_mapsSignsToMaterial()
        {
            // 非空 signs[] ⇒ 非中性材质
            var vitals = new VitalsDto(position: 0.5f, trend: 0f, signChannelMask: 0x0A, signCount: 2);
            var mat = MaterialTable.Material(vitals);
            Assert.IsFalse(mat.IsNeutral, "非空词条集 ⇒ 非中性材质");
            Assert.AreNotEqual(0, mat.PostureBits, "姿态位非零");
            Assert.AreNotEqual(0, mat.ComplexionBits, "面色位非零");
        }

        [Test]
        public void test_ac13d1_emptySigns_yieldsNeutralMaterial()
        {
            // GDD §Edge Cases 一:空集 ⇒ 中性,且**不报错、不特判**
            var empty = new VitalsDto(position: 0.9f, trend: 0f, signChannelMask: 0, signCount: 0);
            Assert.IsTrue(MaterialTable.Material(empty).IsNeutral,
                "空 signs[] ⇒ 中性材质(合法形态,非错误)");

            // 仅有 mask 无 count / 仅有 count 无 mask ⇒ 亦中性(两栏任一为空即中性)
            var maskOnly = new VitalsDto(position: 0.5f, trend: 0f, signChannelMask: 0xFF, signCount: 0);
            Assert.IsTrue(MaterialTable.Material(maskOnly).IsNeutral, "count ≤ 0 ⇒ 中性");
            var countOnly = new VitalsDto(position: 0.5f, trend: 0f, signChannelMask: 0, signCount: 5);
            Assert.IsTrue(MaterialTable.Material(countOnly).IsNeutral, "mask = 0 ⇒ 中性");
        }

        [Test]
        public void test_ac13d1_nonEmptySigns_neverFoldsToNeutralTone()
        {
            // ⚠️ 非空词条集**不得**折叠成「中性音色」—— 折叠到 0 时须抬到 1
            //    判据:穷举低 4 位全组合,`ToneVariant` 对非空 mask 恒 ≥ 1
            for (int mask = 1; mask <= 0xF; mask++)
            {
                var vitals = new VitalsDto(0.5f, 0f, mask, 1);
                var mat = MaterialTable.Material(vitals);
                Assert.AreNotEqual(0, mat.ToneVariant,
                    $"mask=0x{mask:X} 折叠到 0 ⇒ 须抬到 1(非空词条集不得是中性音色)");
            }
        }

        [Test]
        public void test_ac13d1_material_isPureFunction_noPrng()
        {
            // 同输入 ⇒ 同输出(纯函数;无 PRNG —— 与 cue 路径同纪律)
            var vitals = new VitalsDto(0.3f, 0.1f, 0x1C, 3);
            var a = MaterialTable.Material(vitals);
            var b = MaterialTable.Material(vitals);
            Assert.AreEqual(a.ToneVariant, b.ToneVariant);
            Assert.AreEqual(a.PostureBits, b.PostureBits);
            Assert.AreEqual(a.ComplexionBits, b.ComplexionBits);
        }

        [Test]
        public void test_ac13d1_materialOutput_isIntegerBitfield_noFloat()
        {
            // 三字段皆 int(整数位域 —— 禁 float,承 TR-patient-010)
            var fields = typeof(PresentMaterial).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var f in fields)
                Assert.AreEqual(typeof(int), f.FieldType, $"{f.Name} 须是 int(整数位域,禁 float)");
            Assert.AreEqual(3, fields.Length, "恰三字段:{ToneVariant, PostureBits, ComplexionBits}");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-A4 / TR-023 —— 13 零玩家可见 UI;调试视图条件编译剥离
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13a4_patientAiDataLayer_hasNoPlayerVisibleUi()
        {
            // AC-13-A4:13 的**交付面**(PatientAI 数据层)**不得**含玩家可见 UI 类型。
            // ⚠️ **扫描根 = 13 命名空间内的类型**,不是整个 `Gameplay.Presentation` 程序集 ——
            //    该程序集同时承载 **42 的拟物呈现**(`Skeuomorphic.PresentationRoot` 等),
            //    那是 42 的合法交付物。全程序集扫描会把 42 的 UI 误判成 13 的违规
            //    (范畴错误:AC-13-A4 约束的是 13 的产物,不是同程序集邻居)。
            var forbiddenRefs = new[] { "UnityEngine.UI.", "UnityEngine.UIElements.",
                                        "UxmlFactory", "VisualElement" };
            var hits = ScanNamespaceTypesForForbiddenRefs(
                typeof(PresentationProjection), "DaYiJingCheng.Gameplay.PatientAI", forbiddenRefs);
            CollectionAssert.IsEmpty(hits,
                "AC-13-A4:13 数据层禁玩家可见 UI 类型(呈现归 42,13 只出数据):\n" + string.Join("\n", hits));
        }

        [Test]
        public void test_ac13a4_uiRefScan_catchesShadow_negativeFixture()
        {
            // ⚠️ 负夹具:影子含 `VisualElement` **调用** ⇒ 同一台 IL 扫描器须报红且点名。
            //    ⚠️ 影子住 `DaYiJingCheng.Tests.PatientAI`(测试命名空间)⇒ 命名空间扫描器
            //    的真产判据扫不到它 —— 负夹具改用**全程序集**谓词(测试件限定 `Shadow*`),
            //    与命空间扫描器**共用同一台内核**(`ScanTypesForForbiddenRefs`)。
            //    这证明「内核能看见 VisualElement 引用」,而正测证「13 命名空间内没有它」。
            var hits = ScanAssemblyForForbiddenRefs(
                typeof(ShadowWithVisualElement), new[] { "VisualElement" });
            CollectionAssert.IsNotEmpty(hits, "影子含 VisualElement 调用 ⇒ IL 扫描内核须报红");
            Assert.IsTrue(hits.Any(h => h.Contains("VisualElement")), "且须点名:\n" + string.Join("\n", hits));
        }

        [Test]
        public void test_ac13a4_dataLayer_hasNoDebugViewMembers()
        {
            // TR-patient-023:调试视图仅 Development Build,**不得**在玩家构建中存活。
            // ⚠️ **本测的诚实边界**:「条件编译剥离」的**真判据是构建产物**(反编译玩家构建、
            //    断言 `DebugView` 类型不在其中)—— 那是**构建期探针**,EditMode 单测**做不到**
            //    (跑测试的是 Editor 程序集,`#if` 已按 Editor 定义求值,看不见玩家构建的面)。
            //    ⚠️ 原实现只扫**类型名**、且用 `!h.Contains("Shadow")` 过滤,既非构建面、
            //    也非 IL 引用面 —— 判别力弱。**订正为可证伪的两半**:
            //    ① 13 命名空间内**零**调试 UI 成员(IL 引用面,与 AC-13-A4 主判据同机器);
            //    ② 本条**显式登记为 NOT-RUN**:构建产物探针未就位(禁借绿)。
            var forbiddenRefs = new[] { "OnGUI", "IMGUIContainer", "Debug.developerConsoleVisible" };
            var hits = ScanNamespaceTypesForForbiddenRefs(
                typeof(PresentationProjection), "DaYiJingCheng.Gameplay.PatientAI", forbiddenRefs);
            CollectionAssert.IsEmpty(hits,
                "TR-patient-023:13 数据层不得含 IMGUI / 控制台调试面(IL 引用面判据):\n"
                + string.Join("\n", hits));
            Assert.Pass(
                "①(已闭)13 数据层零调试 UI 引用。"
                + "②(NOT-RUN,禁借绿)「条件编译剥离」的最终判据 = **玩家构建产物**探针,"
                + "EditMode 单测不可达 —— 登记见 story-003-accessibility-signoff.md NR-S3-4。");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-F1 / F2 / F3 —— [L] 无障碍三判据(判据面 = 数据通道)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13f1_structuralHalf_cueKindsAreDistinctSemanticIds()
        {
            // ⚠️ **本测只判 AC-13-F1 的 13 侧结构前提**,**不**判 AC 原文的「可见对应物 / 非色相可辨」
            //    —— 那两条的**达成面在 42 的冗余通道**(13 不生产可见物;ADR-013 §9 C3 呈现层只渲染)。
            //    达成面登记为 NOT-RUN(见 story-003-accessibility-signoff.md NR-S3-1,禁借绿)。
            // 13 侧能判的 = **cue 语义是独立枚举 id**(不是色相梯度 / 不是同一 cue 的强度差)。
            Assert.AreNotEqual((int)CueKind.Cough, (int)CueKind.Groan,
                "咳嗽 / 呻吟须是**不同语义 id**(听觉区分靠 kind,不靠色相)");
            Assert.AreEqual(2, Enum.GetValues(typeof(CueKind)).Length,
                "两类一次性语义(咳嗽 / 呻吟);呼吸层是第三通道(循环,非 CueKind)");
        }

        [Test]
        public void test_ac13f1_structuralHalf_breathLayerIsDefaultOn()
        {
            // ⚠️ 同样只判 **13 侧结构前提**:呼吸层**默认在**(活着即 Begin,非可选开关)。
            //    「可见对应物」的达成面 BLOCKED-BY 42 —— NOT-RUN。
            foreach (var b in new[] { BehaviorState.Idle, BehaviorState.Seeking, BehaviorState.Bedridden })
            {
                var d = PatientCueSchedule.Decide(
                    b, TerminalFlag.None, 1, -1, new PatientId(1), SymptomTier.Mild, 0.5f,
                    Intervals, breathAlive: false, CueKind.Cough, entryTick: 1);
                Assert.AreEqual(BreathAction.Begin, d.Breath,
                    $"活着的病人({b})⇒ 呼吸层默认起(13 侧结构前提;达成面归 42)");
            }
        }

        [Test]
        public void test_ac13f2_structuralHalf_breathIsIndependentOfMovement()
        {
            // ⚠️ **本测只判 AC-13-F2 的 13 侧结构前提**:呼吸层与**移动与否无关**。
            //    AC 原文的「在 `PERCEPT_R` 内可闻」= 距离衰减 + 空间化 ⇒ **BLOCKED-BY 44**,
            //    且 `PERCEPT_R` 值本身「待调」(GDD :528/:939,数值归用户)⇒ NOT-RUN
            //    (见 story-003-accessibility-signoff.md NR-S3-2,禁借绿)。
            var seek = PatientCueSchedule.Decide(
                BehaviorState.Seeking, TerminalFlag.None, 1, -1, new PatientId(1), SymptomTier.Severe, 0.8f,
                Intervals, breathAlive: false, CueKind.Groan, entryTick: 1);
            var idle = PatientCueSchedule.Decide(
                BehaviorState.Idle, TerminalFlag.None, 1, -1, new PatientId(2), SymptomTier.Recover, 0.1f,
                Intervals, breathAlive: false, CueKind.Cough, entryTick: 1);
            Assert.AreEqual(BreathAction.Begin, seek.Breath, "移动病人 ⇒ 呼吸层起");
            Assert.AreEqual(BreathAction.Begin, idle.Breath, "静止病人 ⇒ 呼吸层亦起(与移动无关)");
        }

        [Test]
        public void test_ac13f3_nonTargetClause_referencedByAccessibilityDoc()
        {
            string callerFile = SourceFilePath();
            // AC-13-F3(文档判据):非目标条款存在且被 `design/accessibility-requirements.md` 引用。
            // ⚠️ **引用已兑现 ≠ 本条记绿** —— 本断言只证「引用字符串就位」。
            //    SIGN-OFF 见 production/qa/evidence/patient-ai/story-003-accessibility-signoff.md。
            // ⚠️ 仓库根 = 工程根(`unity/`)之上一级 —— 承 setup 侧同型上溯(不依赖 cwd)。
            string repoRoot = FindRepoRoot(callerFile);
            Assert.IsNotNull(repoRoot, "须能定位仓库根(含 design/ 与 unity/)");

            string docPath = System.IO.Path.Combine(repoRoot, "design", "accessibility-requirements.md");
            Assert.IsTrue(System.IO.File.Exists(docPath), $"无障碍条款件须存在:{docPath}");
            string doc = System.IO.File.ReadAllText(docPath);
            // ⚠️ 原判据 `Contains("patient-ai") || Contains("13")` 的 `"13"` 半边**近乎恒真**
            //    (26KB 文档里出现 "13" 是必然)。**订正为把两个必需要素钉在同一个窗口内**:
            //    ① 显式点名 `AC-13-F3`;② 与「非目标 / 不可及」语义同现。
            //    ⇒ 只有当 38 侧**真的写了这条非目标**,两条件才同时在窗口内成立。
            int idx = doc.IndexOf("AC-13-F3", StringComparison.Ordinal);
            Assert.GreaterOrEqual(idx, 0,
                "无障碍条款件须**显式点名** `AC-13-F3`(不是泛泛出现 13):" + docPath);
            int windowStart = Math.Max(0, idx - 400);
            int windowLen = Math.Min(doc.Length - windowStart, 800);
            string window = doc.Substring(windowStart, windowLen);
            Assert.IsTrue(window.Contains("非目标") || window.Contains("不可及") || window.Contains("不支持"),
                "`AC-13-F3` 出现处须与「非目标 / 不可及」语义同窗口 —— 否则只是路过提及,"
                + "不构成 GDD §UI Requirements 三 要求的「显式非目标」引用(AC-13-F3)");
        }

        // ═══════════════════════════════════════════════════════════
        //  TC-5 —— 同 tick 多人入场:相位错开,不饿死
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_tc5_sameTickManyPatients_desyncedByPhase_noStarvation()
        {
            // ⚠️ **本测的原始实现把一个真 bug 藏住了**(2026-10-05 订正):
            //    它把「入场 tick」写成 `tick = phase0`(纪元锚),于是 `firstDue = phase`
            //    与 `firstDue = entryTick + phase` 在**那个特定 tick 上恰好都给 due** ——
            //    两种语义不可分辨。而真实入场 tick 是**世界单调量**(远大于 interval),
            //    此时 `firstDue = phase` 使 `tick >= phase` **恒真** ⇒ 全体病人在**同一 tick**
            //    齐发首拍,去同步(F-13.5 ③ / TC-5 存在的唯一理由)彻底失效。
            //    ⇒ 订正:入场 tick 取真实值(此处 5000),经 `Decide` 观察首拍。

            // ── ① 去同步:同 tick(5000)入场的一群病人,首拍 tick 须**互相错开** ──
            const int entryTick = 5000;
            var firstDueById = new System.Collections.Generic.Dictionary<int, int>();
            var distinctFirstDue = new HashSet<int>();
            for (int i = 0; i < 32; i++)
            {
                int dueTick = -1;
                for (int t = entryTick; t < entryTick + Intervals.For(SymptomTier.Severe); t++)
                {
                    var d = PatientCueSchedule.Decide(
                        BehaviorState.Seeking, TerminalFlag.None, t, lastEmitTick: -1,
                        new PatientId(i), SymptomTier.Severe, 0.9f,
                        Intervals, breathAlive: true, CueKind.Groan, entryTick: entryTick);
                    if (d.EmitOneShot) { dueTick = t; break; }
                }
                Assert.GreaterOrEqual(dueTick, entryTick, $"id={i} 须在入场后 interval 内首拍(不饿死)");
                firstDueById[i] = dueTick;
                distinctFirstDue.Add(dueTick);
            }
            Assert.Greater(distinctFirstDue.Count, 1,
                $"TC-5:32 病人同 tick 入场 ⇒ 首拍须错开(得 {distinctFirstDue.Count} 个不同首拍 tick)"
                + ";若全等则去同步失效(相位被当作绝对 tick 的经典 bug)");

            // ── ② 不饿死:任一病人的首拍距入场 ≤ interval − 1 ──
            foreach (var kv in firstDueById)
                Assert.Less(kv.Value - entryTick, Intervals.For(SymptomTier.Severe),
                    $"id={kv.Key} 首拍偏移须 < interval(相位 ∈ [0, interval))");

            // ── ③ 相位真的决定首拍偏移:首拍偏移 == phase(锚在入场时刻) ──
            int phase0 = PatientCue.Phase(new PatientId(0), SymptomTier.Severe, Intervals);
            Assert.AreEqual(entryTick + phase0, firstDueById[0],
                "首拍 tick 须 == entryTick + phase(相位是相对入场时刻的偏移,不是绝对 tick)");

            // ── ④ 节奏稳定:发过后按 lastEmitTick + interval 推进,与邻居无关 ──
            int last = firstDueById[0];
            var nextDue = PatientCueSchedule.Decide(
                BehaviorState.Seeking, TerminalFlag.None, last + Intervals.For(SymptomTier.Severe), last,
                new PatientId(0), SymptomTier.Severe, 0.9f,
                Intervals, breathAlive: true, CueKind.Groan, entryTick: entryTick);
            Assert.IsTrue(nextDue.EmitOneShot, "发过后 + interval ⇒ 下一拍 due");

            var tooEarly = PatientCueSchedule.Decide(
                BehaviorState.Seeking, TerminalFlag.None, last + Intervals.For(SymptomTier.Severe) - 1, last,
                new PatientId(0), SymptomTier.Severe, 0.9f,
                Intervals, breathAlive: true, CueKind.Groan, entryTick: entryTick);
            Assert.IsFalse(tooEarly.EmitOneShot, "interval 未到 ⇒ 不 due(节奏稳定)");
        }


        // ═══════════════════════════════════════════════════════════
        //  扫描机器(正测与负夹具共用的同一台)
        // ═══════════════════════════════════════════════════════════

        private const byte IlCall = 0x28, IlCallvirt = 0x6F;

        /// <summary>IL 引用扫描(与 story-002 同机器)—— 遍历程序集全部类型方法体,
        /// 解析 `call`/`callvirt` 目标为 `声明类型::方法名`,命中禁项则点名。</summary>
        private static List<string> ScanAssemblyForForbiddenRefs(Type root, string[] forbiddenRefs)
        {
            var asm = root.Assembly;
            bool isTestAsm = asm.GetName().Name.Contains("Test");
            return ScanTypesForForbiddenRefs(asm, forbiddenRefs,
                t => t.FullName != null && (!isTestAsm || t.Name.StartsWith("Shadow")));
        }

        /// <summary>IL 引用扫描 —— **限定在某命名空间内**(AC-13-A4:13 数据层判据)。
        /// <para>⚠️ 与全程序集扫描**共用同一台机器**(过滤谓词不同)——
        /// 防「两台机器各自演化」的静默分叉(反空转三件 ①)。</para></summary>
        private static List<string> ScanNamespaceTypesForForbiddenRefs(
            Type root, string @namespace, string[] forbiddenRefs)
        {
            var asm = root.Assembly;
            return ScanTypesForForbiddenRefs(asm, forbiddenRefs,
                t => t.FullName != null &&
                     string.Equals(t.Namespace, @namespace, StringComparison.Ordinal));
        }

        /// <summary>IL 引用扫描内核(两处判据的**唯一实现**)。
        /// <para>⚠️ 负夹具用的影子件住 `DaYiJingCheng.Gameplay.PatientAI` 命名空间 ⇒
        /// 命名空间限定扫描也覆盖它(影子可被看见)。</para></summary>
        private static List<string> ScanTypesForForbiddenRefs(
            Assembly asm, string[] forbiddenRefs, Func<Type, bool> include)
        {
            var hits = new List<string>();
            var module = asm.ManifestModule;

            const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic |
                                     BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var t in asm.GetTypes())
            {
                if (!include(t)) continue;

                var bodies = new List<MethodBase>();
                foreach (var m in t.GetMethods(All)) bodies.Add(m);
                foreach (var c in t.GetConstructors(All)) bodies.Add(c);

                foreach (var m in bodies)
                {
                    byte[] il;
                    try { il = m.GetMethodBody()?.GetILAsByteArray(); }
                    catch (Exception) { continue; }
                    if (il == null) continue;

                    for (int i = 0; i + 4 < il.Length; i++)
                    {
                        if (il[i] != IlCall && il[i] != IlCallvirt) continue;
                        int token = BitConverter.ToInt32(il, i + 1);
                        MethodBase target;
                        try { target = module.ResolveMethod(token); }
                        catch (Exception) { continue; }
                        if (target == null) continue;

                        string sig = (target.DeclaringType?.FullName ?? "") + "::" + target.Name;
                        foreach (var f in forbiddenRefs)
                            if (sig.Contains(f)) hits.Add($"{t.FullName}.{m.Name} ⇒ {sig}");
                    }
                }
            }
            return hits;
        }

        /// <summary>本测试源文件的**编译期路径**(`CallerFilePath`)。
        /// <para>⚠️ NUnit 不接受带默认形参的 `[Test]` 方法 ⇒ 不走形参注入,
        /// 在方法体内调本辅助函数取路径(`[CallerFilePath]` 对**普通方法**照常生效)。</para></summary>
        private static string SourceFilePath(
            [System.Runtime.CompilerServices.CallerFilePath] string path = null) => path;

        /// <summary>上溯到含 `design/` 的**仓库根**(即 `unity/` 之上一级)。
        /// <para>⚠️ 判据 = 目录同时含 `design/` 与 `unity/` —— 单判 `design/` 在有些
        /// 安装布局下会先命中 `Library/PackageCache/*/design`;双判锚定仓根。
        /// 起点取**本测试源文件的编译期路径**(`CallerFilePath`,编译机路径),
        /// 与运行期 cwd / 程序集安装位置**三者互相兜底**(任一可用即可)。</para></summary>
        private static string FindRepoRoot(string callerFile = null)
        {
            var starts = new List<string>();
            if (!string.IsNullOrEmpty(callerFile))
                starts.Add(System.IO.Path.GetDirectoryName(callerFile));
            starts.Add(AppContext.BaseDirectory);
            starts.Add(System.IO.Directory.GetCurrentDirectory());

            foreach (var start in starts)
            {
                if (string.IsNullOrEmpty(start)) continue;
                var dir = new System.IO.DirectoryInfo(start);
                for (int i = 0; i < 16 && dir != null; i++)
                {
                    if (System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "design")) &&
                        System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "unity")))
                        return dir.FullName;
                    dir = dir.Parent;
                }
            }
            return null;
        }

        /// <summary>`FindRepoRoot` 的无参便利形式(走 `[CallerFilePath]`)。
        /// <para>⚠️ 失败时**抛**(不是返回 null)—— 调用点都在断言里,静默 null 会让
        /// `Path.Combine` 抛 `ArgumentNullException`,错误信息指向 `Path` 而非「找不到仓根」。</para></summary>
        private static string RepoRoot()
        {
            var root = FindRepoRoot(SourceFilePath());
            if (string.IsNullOrEmpty(root))
                throw new InvalidOperationException(
                    "找不到仓库根(须同时含 design/ 与 unity/);CallerFilePath=" + SourceFilePath());
            return root;
        }

        /// <summary>**源码面** token 扫描 —— 递归读目录下全部匹配文件,逐行找关键词。
        /// <para>⚠️ 这是 AC-13-C2 的**唯一判据机器**(正测与负夹具共用)。
        /// 之所以不走程序集引用面:37 的程序集**当前不存在** ⇒ 引用面断言恒真。</para>
        /// <para>返回命中行(文件相对路径 + 行号 + 原文),空 = 干净。</para></summary>
        private static List<string> ScanSourceFilesForTokens(string dir, string pattern, string[] tokens)
        {
            var hits = new List<string>();
            foreach (var file in Directory.GetFiles(dir, pattern, SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (var tok in tokens)
                    {
                        if (lines[i].Contains(tok, StringComparison.Ordinal))
                        {
                            hits.Add($"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}");
                            break;
                        }
                    }
                }
            }
            return hits;
        }

        /// <summary>**IL 引用面**扫描 —— 读类型全部方法的 IL 字节,解析 `call`/`callvirt`
        /// (opcode `0x28` / `0x6F`)的 4 字节元数据 token,拼 `声明类型::方法名` 与禁入集比对。
        /// <para>⚠️ 这是 AC-13-C4 的**判据机器**:名字面扫描看不见 `new GameObject()`
        /// (名字里没有 Spawn),IL 面才看得见**方法体内的调用**。
        /// 用 `Module.ResolveMethod` 解 token ⇒ 能看见**外部程序集**引用(名字面只见定义)。</para>
        /// <para>返回值带**声明类型名**(点名前门),空 = 干净。</para></summary>
        private static List<string> ScanTypeForForbiddenCalls(Type t, string[] forbiddenCalls)
        {
            var hits = new List<string>();
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                                         | BindingFlags.Instance | BindingFlags.Static
                                         | BindingFlags.DeclaredOnly))
            {
                var body = m.GetMethodBody();
                if (body == null) continue;                       // abstract / extern ⇒ 无 IL
                var il = body.GetILAsByteArray();
                if (il == null) continue;

                for (int i = 0; i < il.Length; i++)
                {
                    byte op = il[i];
                    if (op != 0x28 && op != 0x6F) continue;        // call / callvirt
                    if (i + 4 >= il.Length) break;
                    int token = BitConverter.ToInt32(il, i + 1);
                    MethodBase target;
                    try { target = m.Module.ResolveMethod(token); }
                    catch { continue; }                            // 解不出的 token 跳过(仍向前扫)
                    if (target == null) continue;

                    string full = $"{target.DeclaringType?.FullName}::{target.Name}";
                    foreach (var forbidden in forbiddenCalls)
                    {
                        if (full == forbidden || full.EndsWith("::" + forbidden, StringComparison.Ordinal))
                        {
                            hits.Add($"{t.Name}.{m.Name} → {full}");
                            break;
                        }
                    }
                    i += 4;                                        // token 已消费
                }
            }
            return hits;
        }

        // ═══════════════════════════════════════════════════════════
        //  夹具类型
        // ═══════════════════════════════════════════════════════════

        /// <summary>假投影源:按 id 产固定的投影输入(格 = id,行为 = Idle,会诊 = None,相位 = EnRoute)。
        /// <para>⚠️ **每次 `Project` 都递增代数** —— 供「同一 tick 重复 Rebuild 结果相同(幂等)」用。</para></summary>
        private sealed class FakeProjectionSource
        {
            private int _generation;
            public int Generation => _generation;
            public PatientProjectionInput Project(PatientId id)
            {
                _generation++;
                return new PatientProjectionInput(
                    id, BehaviorState.Idle, SessionState.None, SeekingPhase.EnRoute,
                    new WorldPos(id.Value, 0, 0),
                    new VitalsDto(position: 0.2f, trend: 0f, signChannelMask: 0, signCount: 0));
            }
        }

        /// <summary>影子件:按**全库**枚举病人(违反「只投影在册者」)—— 供负夹具。
        /// <para>⚠️ 在册册与真视图**同源**(`OnPresentEntered` 数),故 `PresentCount` 是真计数,
        /// 不是常量 —— 唯此「投影次数 ≤ 在册数」这条不变量对影子才有判别力(承 AC-13-C3 负夹具)。</para></summary>
        private sealed class ShadowProjectAllPatients
        {
            private readonly Func<PatientId, PatientProjectionInput> _project;
            private readonly int _total;
            private readonly System.Collections.Generic.HashSet<int> _roster = new System.Collections.Generic.HashSet<int>();
            public int ProjectionCalls { get; private set; }
            public int PresentCount => _roster.Count;   // ⚠️ 真计数(非硬编码 0)
            public ShadowProjectAllPatients(Func<PatientId, PatientProjectionInput> p, int totalPopulation)
            { _project = p; _total = totalPopulation; }
            public void OnPresentEntered(PatientId id) => _roster.Add(id.Value);
            public void Rebuild()
            {
                ProjectionCalls = 0;
                // 违规:无视在册册,按全库枚举
                for (int i = 0; i < _total; i++) { _project(new PatientId(i)); ProjectionCalls++; }
            }
        }

        /// <summary>影子件:方法体内**真的发起一次调用**(违反 13 零 spawn 的等价面)——
        /// 供 IL 引用面扫描负夹具。
        /// <para>⚠️ 名字里**没有** Spawn/Destroy 任何禁词 —— 正因如此,名字面扫描看不见它,
        /// 而 IL 面能看见方法体内的 `call`。这正是本负夹具要证明的判别力来源。</para></summary>
        private sealed class ShadowWithSpawn
        {
            public string MakeLabel() => string.Concat("a", "b");
        }
    }
}
