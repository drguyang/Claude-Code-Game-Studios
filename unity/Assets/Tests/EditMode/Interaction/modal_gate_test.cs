// interaction-system Story 005 —— 模态门与路由边沿(AC-4-09 / AC-4-10 / AC-4-19)
//
// 登记落点: tests/unit/interaction/modal_gate_test.cs
// 真身落点: unity/Assets/Tests/EditMode/Interaction/modal_gate_test.cs
//
// 权威来源:
//   GDD design/gdd/interaction-system.md —— 规则七(模态抑制两方向)/ F-4.2(布尔合取)
//     / 规则八(4 是 MotorSuppressed 合法调用者)/ 规则九(4 无状态)
//   ADR-013 §十 Amendment A(IModalState / ModalId 闭集,42 侧权威声明)
//   ADR-020 §四 对称(抑制的写方在 1)/ ADR-005(输入是意图源)
//
// Story 005 的三条 AC:
//   AC-4-09 [A] 模态门方向②:IModalState.Modal ∈ 闭集 ⇒ 意图**丢弃**(不排队);引用而非复制
//   AC-4-10 [A] 模态门方向①:10 的 Armed 态 ⇒ 意图**压制**;3 侧零状态
//   AC-4-19 [A] per-source lease:4 与 10 同时持有压制,4 先 Release ⇒ 压制仍生效
//
// ⚠️ 断言目标纪律(承 story-004 双代理评审修复轮的「两台机器」教训):
//   本文件**全部断言直接驱动生产** `ModalGate` / `MotorLease`,**不设**测试侧参考实现。
//   负夹具必须与正测**共用同一台机器**(同一个 `ModalGate.Accept` / 同一个 `MotorLease`),
//   否则「夹具红」不蕴含「真断言红」。
//
// ⚠️ AC-4-09 的「引用而非复制」判据形态(结构半边):
//   4 侧代码**不得**出现 `ModalId` 的成员名清单 —— 由本文件顶部的源文本扫描守
//   (Interaction 目录内不得出现 `ModalId.Casebook` 等成员引用;只允许比较「开 / 关」布尔)。
//   ⚠️ 已知限制(同 radius_single_source_test 的启发式登记):源文本扫描是**必要非充分**的守卫
//   —— 成员名可经字符串拼接 / 反射拼出规避;真值单源由「4 侧看不到 ModalId」(程序集方向,
//   见下)共同守。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Gameplay.Presentation.Player;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Interaction
{
    public class ModalGateTest
    {
        // ═══════════════════════════════════════════════════════════
        //  测试替身(只读门的最小实现 —— 与生产共用同一台 Accept)
        // ═══════════════════════════════════════════════════════════

        /// <summary>10 的 `Armed` 只读门替身(真源在 10)。</summary>
        private sealed class FakeArmed : IArmedState
        {
            public bool Armed;
            public bool IsArmed => Armed;
        }

        /// <summary>42 的模态开集只读门替身(真源在 42)。</summary>
        /// <remarks>
        /// ⚠️ 本替身**只暴露一个布尔**,与生产消费面 <see cref="IModalGateState"/> 一致 ——
        /// 它**不**复制 `ModalId` 成员名,亦无「哪一个模态」的读数(结构侧 F-2),
        /// 故它也不能(结构上)成为「复制清单」的温床。</remarks>
        private sealed class FakeModal : IModalGateState
        {
            public bool Open;
            public bool IsOpen => Open;
        }

        /// <summary>计数型发现上报面(证明门开时选择器真被驱动)。
        /// <para>⚠️ 本替身的计数字段是**测试侧**可变状态,不违反 AC-4-13(4 生产侧零记账)——
        /// 生产 `ModalGate` / `InteractionSelector` 仍无可变字段(见 ScanForMutableState)。</para></summary>
        private sealed class CountingReporter : IDiscoveryReporter
        {
            public int TotalRequests;
            public void Request(in DiscoveryRequest request) => TotalRequests++;
        }

        private static InteractionSelector MakeSelector(int r = 3)
            => new InteractionSelector(new CountingReporter(), r);

        /// <summary>注入外部计数器的选择器(供「丢弃而非排队」观测选择器是否真跑到自报步)。</summary>
        private static InteractionSelector MakeSelector(int r, CountingReporter reporter)
            => new InteractionSelector(reporter, r);

        private static Candidate Poi(int x, int y, int z, long id)
            => new Candidate(new WorldPos(x, y, z), InteractableKind.PoiCell, id, StableIdSource.PoiId);

        private static readonly WorldPos PlayerCell = new WorldPos(10, 0, 5);
        private static InteractIntent Pressed() => new InteractIntent(PlayerCell, true, 0);
        private static InteractIntent NotPressed() => new InteractIntent(PlayerCell, false, 0);

        // ═══════════════════════════════════════════════════════════
        //  AC-4-10 —— 模态门方向①:10 的 Armed ⇒ 意图被压制
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac410_armedSuppressesIntent()
        {
            // GIVEN:10 的 Armed 态 = true;42 无模态。
            var armed = new FakeArmed { Armed = true };
            var modal = new FakeModal { Open = false };
            var gate = new ModalGate(armed, modal);

            // WHEN:按交互键。
            bool accepted = gate.Accept(Pressed());

            // THEN:意图被压制(拒收)。
            Assert.IsFalse(accepted, "AC-4-10:10 的 Armed 期 ⇒ 意图被压制(¬Armed 不成立)");
        }

        [Test]
        public void test_ac410_notArmedAndNoModalAcceptsIntent()
        {
            // 反空转对照:两门皆开 ⇒ 通过(证明上条的 false 是 Armed 引起,不是恒 false)。
            var gate = new ModalGate(new FakeArmed { Armed = false }, new FakeModal { Open = false });
            Assert.IsTrue(gate.Accept(Pressed()), "AC-4-10:两门皆开 ⇒ 意图通过(对照,证非恒拒)");
        }

        [Test]
        public void test_ac410_armedSuppressionIsIndependentOfPressState()
        {
            // AC-4-10 的门与「是否按下」正交:门闭时,按下与未按下都拒(压制是在门层,不是在选择层)。
            var gate = new ModalGate(new FakeArmed { Armed = true }, new FakeModal { Open = false });
            Assert.IsFalse(gate.Accept(Pressed()), "AC-4-10:Armed 期按下 ⇒ 拒");
            Assert.IsFalse(gate.Accept(NotPressed()), "AC-4-10:Armed 期未按 ⇒ 亦拒(门层,非选择层)");
        }

        [Test]
        public void test_ac410_armedGateLeavesSelectorUntouched()
        {
            // 「3 侧零状态」的 4 侧形态:门闭 ⇒ 选择器**零出境**(连选都不选)。
            var armed = new FakeArmed { Armed = true };
            var modal = new FakeModal { Open = false };
            var gate = new ModalGate(armed, modal);
            var selector = MakeSelector();
            var candidates = new[] { Poi(10, 0, 5, 1L) };

            var target = gate.Select(selector, Pressed(), candidates);

            Assert.IsFalse(target.HasTarget, "AC-4-10:Armed 期 ⇒ 零目标(门闭,选择器不被驱动)");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-4-09 —— 模态门方向②:42 模态开集 ⇒ 意图被丢弃(不排队)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac409_anyModalOpenDiscardsIntent()
        {
            // GIVEN:42 模态开集,每个成员 ordinal 都试一遍(逐值全覆,非只测一个)。
            // ⚠️ 4 侧看不到 `ModalId` 成员名 ⇒ 这里用 ordinal 值域遍历,而非引用枚举成员。
            var armed = new FakeArmed { Armed = false };
            var modal = new FakeModal { Open = true };

            // ⚠️ 反空转(F-9):下循环的 `Assert.IsFalse` 在 `Open=true` 时**恒真**(不区分
            //   「真检了」与「门被删了」)。故设**正向对照**:`Open=false` ⇒ `Accept` 必真。
            //   ⇒ 任一 `IsOpen` 门被删(或 `FakeModal` 失联)必然使**本对照**红,循环才有承载力。
            modal.Open = false;
            Assert.IsTrue(new ModalGate(armed, modal).Accept(Pressed()),
                "AC-4-09 反空转门:Open=false ⇒ 必通过(证明下方逐值全拒非「恒拒」的假绿)");
            modal.Open = true;

            // ⚠️ F-2/F-3:4 侧契约**无**「哪一个模态」读数 ⇒ 逐值遍历在 4 侧不可观测
            //   (`Accept` 只看 `IsOpen`)。真正的「每成员都拒」由 42 侧适配器保证;4 侧
            //   在此**只须**证「任一 Open ⇒ 拒」+ 对照「Open=false ⇒ 通过」。故此处不再伪遍历。
            var gate = new ModalGate(armed, modal);
            Assert.IsFalse(gate.Accept(Pressed()),
                "AC-4-09:模态摊开(42 闭集任一成员)⇒ 意图丢弃(4 侧只观测布尔,与成员数解耦)");
        }

        [Test]
        public void test_ac409_noModalAcceptsIntent()
        {
            // None(闭合)= 意图通过。
            var gate = new ModalGate(new FakeArmed { Armed = false }, new FakeModal { Open = false });
            Assert.IsTrue(gate.Accept(Pressed()), "AC-4-09:无模态 ⇒ 意图通过");
        }

        [Test]
        public void test_ac409_discardIsNotQueue_secondCallAfterModalClosesSeesNoStaleIntent()
        {
            // 「丢弃而非排队」的**可执行判据**(结构侧 F-1 修复 —— 初稿是空转):
            //   判据 = 模态期按下**不驱使选择器跑到自报步**,且关闭后**不重放**该意图。
            //   ⚠️ 观测面 = 注入选择器的 `CountingReporter`(`InteractionSelector` 是 sealed + 非虚,
            //   不可 spy 其 `Select`)。选择器**真被驱动并选中 POI** ⇒ 必发一次 `Request`
            //   (InteractionSelector.cs:105-108)。⇒ 计数即「选择器真跑了」的**生产侧**证据:
            //   排队实现会在关闸后把缓存 intent 交给选择器(计数 +1),丢弃实现**永不**(计数停留)。
            var armed = new FakeArmed { Armed = false };
            var modal = new FakeModal { Open = true };
            var gate = new ModalGate(armed, modal);
            var reporter = new CountingReporter();
            var selector = MakeSelector(3, reporter);
            var candidates = new[] { Poi(10, 0, 5, 1L) };

            // 模态期:按下 ⇒ 门闭,**选择器零次自报**(丢弃,而非交由选择器再判)。
            var during = gate.Select(selector, Pressed(), candidates);
            Assert.IsFalse(during.HasTarget, "AC-4-09:模态期 ⇒ 零目标(丢弃)");
            Assert.AreEqual(0, reporter.TotalRequests,
                "AC-4-09 丢弃判据:门闭时选择器**零次**跑到自报(排队实现会缓存 intent 待重放)");

            // 模态关闭:提供**未按下**的 intent ⇒ 仍零自报(无「补发上一帧被拒意图」的路径)。
            modal.Open = false;
            var afterClose = gate.Select(selector, NotPressed(), candidates);
            Assert.IsFalse(afterClose.HasTarget, "AC-4-09:关闭后同帧未按下 ⇒ 零目标");
            Assert.AreEqual(0, reporter.TotalRequests,
                "AC-4-09 丢弃判据:关闭后**未按下** ⇒ 选择器仍零自报(证被拒意图**未排队 / 未重放**)");

            // 关闭后**新**按下 ⇒ 选择器自报恰一次(证明门的开闸路径是活的,上两条的零非恒零)。
            var freshPress = gate.Select(selector, Pressed(), candidates);
            Assert.IsTrue(freshPress.HasTarget, "AC-4-09:关闭后**新**按 ⇒ 通过(证非恒拒)");
            Assert.AreEqual(1, reporter.TotalRequests,
                "AC-4-09:关闸后新按 ⇒ 选择器自报**恰一次**(门开路径活;与前两条的零形成对照)");
        }

        [Test]
        public void test_ac409_fourSideDoesNotCopyModalIdMemberList()
        {
            // ⚠️ AC-4-09 的「引用而非复制」判据(结构半边,启发式 —— 见文件头已知限制登记):
            //   4 侧(Interaction 目录)源文本**不得**出现 `ModalId.<成员名>` 形式的复制清单。
            //   判据:扫 `ModalId` 的点访问,签名应为「引用该类型」(如 `ModalId.None` 比较 / 类型形参),
            //   **不是**把 7 个成员名抄进分派分支。
            //
            // ⚠️ **反空转门(2026-10-04 自证轮)**:本扫描器**必须读到 ≥1 个源文件**才具承载力 ——
            //   若源码定位失败(路径解析错 / CI 外环境),`ScanForCopiedModalIdMembers` 会**静默扫零文件**
            //   ⇒ 返回空 ⇒ 本条**假绿**(这正是本仓反复出现的「空转替代真断言」)。故先断言「读到了源」。
            var sources = ReadInteractionSources().ToList();
            Assert.IsNotEmpty(sources,
                "AC-4-09 反空转门:源文本扫描器必须读到 ≥1 个 4 侧源文件,否则本判据是空转的假绿" +
                "(源码定位依赖 cwd=unity/ 或测试目录上溯;两条路径皆不可达时为环境问题,须显式红)");
            Assert.IsTrue(sources.Any(s => s.file.Contains("ModalGate")),
                "AC-4-09 反空转门:扫描集须含生产 `ModalGate.cs`(证扫的是真身,不是无关文件)");

            // ⚠️ F-3 收口:文本清单**须与 `ModalId` 真源同基数**(闭集增员而清单未同步 ⇒ 红)。
            //   这是「文本清单」这个弱形态的**补偿守卫** —— 无它则新增第 8 屏时本扫描器静默失覆盖。
            //   真源以**源文本**读入(测试装配看不到 Gameplay.UI,只能读文本),数 `case`/赋值形态成员。
            var modalIdSrc = ReadModalIdSource();
            Assert.IsNotNull(modalIdSrc,
                "AC-4-09 反空转门(F-3):须能读到 `ModalId.cs` 真源以核对闭集基数" +
                "(读不到 ⇒ 本基数守卫空转,须显式红)");
            var realMemberCount = CountModalIdMembers(modalIdSrc);
            Assert.AreEqual(ClosedSetMembers.Length, realMemberCount,
                $"AC-4-09(F-3):扫描器文本清单({ClosedSetMembers.Length} 员)与 `ModalId` 真源" +
                $"({realMemberCount} 员)不同基数 ⇒ 闭集增员而清单失同步,守卫静默失覆盖。须同步清单与基数。");

            var violations = ScanForCopiedModalIdMembers(sources);
            Assert.IsEmpty(violations,
                "AC-4-09:「引用而非复制」—— 4 侧不得出现 ModalId 成员名清单(闭集新增屏时 4 侧须零改动):\n"
                + string.Join("\n", violations));

            // ⚠️ 更强的形态(AC-4-09 的「4 侧零改动」判据的**充分**半边):4 侧生产**代码**不引用
            //   `ModalId` 类型 —— 它消费的是 Presentation 侧的 `IModalGateState`(一个布尔 + 不透明
            //   ordinal),结构中**看不到**闭集成员 ⇒ 新增第 8 屏时 4 侧**不可能**需要改动。
            //   ⚠️ 只扫**代码**(剥离注释后)—— 散文里提及 `ModalId`(如本文件顶部权威来源注)是**文档**,
            //   不是引用耦合;把注释计入会使本断言沦为「文件名黑名单」式噪声。
            // ⚠️ 逐字到「零出现」(F-8):不只是 `ModalId.<成员名>` —— 4 侧生产代码里
            //   **连 `ModalId` 这个类型名本身都不得出现**(连 `ModalId.None` 这个「合法引用形态」也不许:
            //   4 消费的是 `IModalGateState.IsOpen` 布尔,不接触枚举类型)。这是「4 侧看不到闭集」的**最强**形态。
            var modalIdTypeRefs = sources
                .Where(s => StripComments(s.text).Contains("ModalId"))
                .Select(s => s.file)
                .ToList();
            Assert.IsEmpty(modalIdTypeRefs,
                "AC-4-09:4 侧生产**代码**不得出现 `ModalId` 类型名(消费面是 IModalGateState 的布尔,与 42 闭集完全解耦;" +
                "代码出现即耦合 ⇒ 新增屏时 4 侧须复审)。出现于:\n" + string.Join("\n", modalIdTypeRefs));
        }

        /// <summary>剥离 C# 行注释(<c>//</c>)与块注释(<c>/* */</c>)后的代码文本。
        /// <para>⚠️ 启发式(非真解析器):不处理字符串字面量内的 <c>//</c>。对 4 侧源(无此类形态)足够;
        /// 登记为已知限制(与 <see cref="ScanForCopiedModalIdMembers"/> 同族)。</para></summary>
        private static string StripComments(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            bool inLine = false, inBlock = false;
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';
                if (inLine) { if (c == '\n') { inLine = false; sb.Append(c); } continue; }
                if (inBlock) { if (c == '*' && n == '/') { inBlock = false; i++; } continue; }
                if (c == '/' && n == '/') { inLine = true; i++; continue; }
                if (c == '/' && n == '*') { inBlock = true; i++; continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        [Test]
        public void test_ac409_negativeFixture_copiedMemberListIsRed()
        {
            // 负夹具(与正测**共用同一台机器** —— 同一个 ScanForCopiedModalIdMembers):
            // 造一段**含**复制清单的源文本 ⇒ 扫描器必须点名它(证扫描器非空转)。
            var offending = "if (m.Modal == ModalId.Casebook) { return false; } "
                          + "else if (m.Modal == ModalId.SaveSlots) { return false; }";
            var violations = ScanForCopiedModalIdMembers(new[] { ("FakeFourSide.cs", offending) });
            Assert.IsNotEmpty(violations,
                "AC-4-09 负夹具:把 ModalId 成员名抄进 4 侧分派分支 ⇒ 必须红(实体纪律:引用而非复制)");
            Assert.IsTrue(violations.Any(v => v.Contains("Casebook")),
                "AC-4-09 负夹具:违规须点名被抄的成员(Casebook),证扫描器触底到成员名");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-4-19 —— per-source lease:4 先 Release,10 的位仍置
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac419_fourReleaseLeavesTenSuppressionActive()
        {
            // GIVEN:4 与 10 同时持有移动压制。
            var lease = new MotorLease();
            ModalGate.SetMotorSuppression(lease, true);        // 4 置位(经 4 的调用面)
            lease.Acquire(LeaseSource.Emergency);              // 10 置位
            Assert.IsTrue(lease.IsSuppressed, "前置:两源皆持有 ⇒ 压制生效");

            // WHEN:4 先 Release。
            ModalGate.SetMotorSuppression(lease, false);

            // THEN:压制仍生效(10 的位仍置)。
            Assert.IsTrue(lease.IsSuppressed, "AC-4-19:4 释放后压制仍生效(10 的位仍置)");
            Assert.IsTrue(lease.HasLease(LeaseSource.Emergency), "AC-4-19:10 的位仍在");
            Assert.IsFalse(lease.HasLease(LeaseSource.Self), "AC-4-19:4 的位已清(只碰自己那一位)");
        }

        [Test]
        public void test_ac419_fourReleaseDoesNotTouchCombatBit()
        {
            // 同族:25 的位亦不被 4 的 Release 波及(「三者互不知晓」的位图形态)。
            var lease = new MotorLease();
            lease.Acquire(LeaseSource.Combat);
            ModalGate.SetMotorSuppression(lease, true);
            ModalGate.SetMotorSuppression(lease, false);
            Assert.IsTrue(lease.HasLease(LeaseSource.Combat), "AC-4-19:4 释放不碰 25 的位");
            Assert.IsTrue(lease.IsSuppressed, "AC-4-19:25 的位在 ⇒ 仍压制");
        }

        [Test]
        public void test_ac419_negativeFixture_singleBoolWriterLosesUpdate()
        {
            // 反空转(证伪「单 bool + 互不知晓写者 = 丢失更新」)。
            // ⚠️ 2026-10-04 修复轮(结构侧 F-4):初稿只对**局部变量** `bool singleBool` 断言 ——
            //   那是「自证空转」(断言对象不存在生产语义,删任何生产代码它照样绿)。现改为
            //   **可执行的影子实现** `SingleBoolSuppression`:**真**走一遍 Acquire/Release 序列,
            //   证明单 bool 形态**确实**丢失 10 的压制 —— 与生产 `MotorLease` 的同一序列形成对照。
            var single = new SingleBoolSuppression();   // 影子:旧「单 bool」实现
            single.Acquire(LeaseSource.Emergency);      // 10 置位
            single.Acquire(LeaseSource.Self);           // 4 置位
            Assert.IsTrue(single.IsSuppressed, "前置:单 bool 下两源皆持有");
            single.Release(LeaseSource.Self);           // 4 释放 ⇒ 单 bool 语义下顺手清了 10 的位
            Assert.IsFalse(single.IsSuppressed,
                "AC-4-19 负夹具:单 bool 实现**确实丢失**了 10 的压制(证位图模型是必需的,非风格偏好)");

            // 生产位图模型下**同一序列** ⇒ 压制保留(与影子形成对照,同一断言面)。
            var lease = new MotorLease();
            lease.Acquire(LeaseSource.Emergency);
            ModalGate.SetMotorSuppression(lease, true);
            ModalGate.SetMotorSuppression(lease, false);
            Assert.IsTrue(lease.IsSuppressed, "AC-4-19:生产位图模型下同一序列 ⇒ 压制保留(对照影子)");
            Assert.IsTrue(lease.HasLease(LeaseSource.Emergency), "AC-4-19:10 的位仍在(生产不丢更新)");
        }

        [Test]
        public void test_ac419_fourCallFaceIsAcquireReleaseNotSetSuppressed()
        {
            // AC-4-19 的**调用面**判据:4 侧不得出现 `SetSuppressed(bool)` 形态的 API。
            // 生产面由 `ModalGate.SetMotorSuppression(lease, acquire)` 转发到 per-source 位图;
            // `MotorLease` 本体只有 Acquire/Release/HasLease/IsSuppressed —— 无 `SetSuppressed`。
            var leaseMethods = typeof(MotorLease).GetMethods()
                .Select(m => m.Name).Distinct().ToList();
            Assert.IsFalse(leaseMethods.Any(n => n.Contains("SetSuppressed")),
                "AC-4-19:MotorLease 不得暴露 SetSuppressed(bool)(单 bool 写者形态 = 丢失更新入口);"
                + "实际方法:" + string.Join(", ", leaseMethods));
            Assert.IsTrue(leaseMethods.Contains("Acquire") && leaseMethods.Contains("Release"),
                "AC-4-19:4 的调用面 = Acquire / Release(per-source 位图)");
        }

        // ═══════════════════════════════════════════════════════════
        //  规则九 —— 4 无状态(结构半边,与 story-001 / 004 同式扫描器)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_rule9_modalGateHasNoMutableState()
        {
            // ⚠️ 与 story-001 `ScanForMutableState` / story-004 `ScanMutableState` **同源同式**
            //   (非只读实例字段 + static 非 const;不扫属性)。4 的门是纯函数。
            var violations = ScanForMutableState(typeof(ModalGate));
            Assert.IsEmpty(violations,
                "规则九:ModalGate 须零可变字段(丢弃而非排队 ⇒ 不得缓存被拒意图):\n"
                + string.Join("\n", violations));
        }

        [Test]
        public void test_rule9_negativeFixture_cachedIntentQueueIsRed()
        {
            // 负夹具(与正测共用同一台扫描器):一个带缓存队列的影子类型必须被点名。
            var violations = ScanForMutableState(typeof(ShadowGateWithQueue));
            Assert.IsNotEmpty(violations,
                "规则九负夹具:门内缓存被拒意图的队列 ⇒ 必须红(F-4.2 丢弃而非排队)");
            Assert.IsTrue(violations.Any(v => v.Contains("_queued")),
                "规则九负夹具:违规须点名该队列字段(_queued)");
        }

        // ═══════════════════════════════════════════════════════════
        //  扫描机器(与负夹具共用)
        // ═══════════════════════════════════════════════════════════

        /// <summary>`ModalId` 闭集的**真实屏成员名**(不含 `None`)—— 扫描器与基数守卫共用。
        /// <para>⚠️ 本清单**无法**机械派生:测试装配(`Sim.Contracts.Tests`)**看不见 `Gameplay.UI`**
        /// (实测编译错 CS0234)—— 这恰是 AC-4-09 的病根(4 侧结构上够不着闭集)。
        /// 故守卫只能用「测试侧文本清单」这个**弱**形态;其失同步风险由
        /// <c>CountModalIdMembers</c> **基数守卫**补偿(闭集增员而清单未同步 ⇒ 红)。</para>
        /// <para>⚠️ 这是**测试侧**清单,**不是** 4 侧生产 —— AC-4-09 禁的是 4 侧**生产**复制闭集。</para></summary>
        private static readonly string[] ClosedSetMembers =
        {
            "Casebook", "SaveSlots", "InventoryContainer", "SettingsShell",
            "Tutorial", "ClinicPanel", "PaperCloseup48"
        };

        /// <summary>可变状态扫描:非 readonly 实例字段 + static 非 const 字段(字段级,不扫属性)。
        /// <para>⚠️ 与 story-001 <c>boundary_discipline_test.ScanForMutableState</c> **逐字同式**
        /// (含其两条等价 readonly 分支 —— 保留以维持「逐字」可对拍;story-004 评审 MINOR #7 明载:
        /// 折叠为一条虽行为相同,却使「同式」claim 不再字面成立)。</para></summary>
        private static List<string> ScanForMutableState(Type t)
        {
            var violations = new List<string>();
            const System.Reflection.BindingFlags fb =
                System.Reflection.BindingFlags.DeclaredOnly |
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            for (var cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
            {
                foreach (var f in cur.GetFields(fb))
                {
                    if (f.IsLiteral) continue;                       // const 合法
                    if (f.IsInitOnly && !f.IsStatic) continue;       // readonly 实例字段合法
                    if (f.IsInitOnly && f.IsStatic) continue;        // readonly static 合法(不可变常量)
                    violations.Add($"[规则九] {cur.Name}.{f.Name} —— 可变字段" +
                                   (f.IsStatic ? "(static)" : "(instance)"));
                }
            }
            return violations;
        }

        /// <summary>
        /// 「引用而非复制」扫描(AC-4-09 结构半边):在 4 侧(Interaction 目录)源文本里找
        /// `ModalId.&lt;成员名&gt;` 形态的**复制清单**。
        /// <para>⚠️ 已知限制(启发式,必要非充分):① 只认 `ModalId.&lt;CapName&gt;` 的点访问形态,
        /// 成员名可经字符串拼接 / 反射规避;② 允许 `ModalId.None` 哨兵比较(那是「引用」的合法形态)。
        /// 真值单源由「4 侧程序集看不到 ModalId(方向)」+ 代码审查共同守。</para>
        /// </summary>
        private static List<string> ScanForCopiedModalIdMembers(
            IEnumerable<(string file, string text)> sources = null)
        {
            //   (清单本体 = 类级 `ClosedSetMembers`,与扫描器共用。)
            var closedSetMembers = ClosedSetMembers;
            var violations = new List<string>();
            foreach (var (file, text) in sources ?? ReadInteractionSources())
            {
                foreach (var member in closedSetMembers)
                {
                    if (text.Contains("ModalId." + member) || text.Contains("ModalId . " + member))
                        violations.Add($"[AC-4-09] {file} —— 引用了 ModalId.{member}(闭集成员名复制 ⇒ 新增屏时 4 侧须改动)");
                }
            }
            return violations;
        }

        /// <summary>读取 `ModalId` 真源文本(Gameplay.UI 目录);**读不到返回 null**
        /// (调用方须以「非 null」为反空转门 —— 同族 F-3 基数守卫)。</summary>
        private static string ReadModalIdSource()
        {
            var here = TestContext.CurrentContext.TestDirectory;
            var cwd = Directory.GetCurrentDirectory();
            var rel = Path.Combine("Assets", "Gameplay.UI", "Skeuomorphic", "ModalId.cs");
            var candidates = new[]
            {
                Path.Combine(cwd, rel),
                Path.Combine(cwd, "..", rel),
                Path.Combine(here, "..", "..", "..", rel),
                Path.Combine(here, "..", "..", "..", "..", rel),
            };
            foreach (var p in candidates)
            {
                var full = Path.GetFullPath(p);
                if (File.Exists(full)) return File.ReadAllText(full);
            }
            return null;
        }

        /// <summary>数 `ModalId` 真源里的**具名屏成员数**(剥注释后,`X = n` 形态里 n ≥ 1 的行;
        /// `None = 0` 不计)。⚠️ 启发式文本计数 —— 仅用于 F-3 的**基数**核对,非成员名解析。</summary>
        private static int CountModalIdMembers(string modalIdSrc)
        {
            var code = StripComments(modalIdSrc);
            // 剥掉 enum 声明行与花括号;只数 `Name = 数字` 且数字 ≥ 1 的成员。
            var count = 0;
            foreach (System.Text.RegularExpressions.Match m in
                     System.Text.RegularExpressions.Regex.Matches(code, @"(?m)^\s*([A-Za-z_]\w*)\s*=\s*(\d+)\b"))
            {
                if (m.Groups[1].Value == "None") continue;
                if (int.Parse(m.Groups[2].Value) >= 1) count++;
            }
            return count;
        }

        /// <summary>读取 4 侧源文件(Interaction 生产目录);**找不到目录则返回空** ——
        /// 调用方须以「读到 ≥1 文件」为反空转门(见 <c>test_ac409_fourSideDoesNotCopyModalIdMemberList</c>)。
        /// <para>⚠️ 运行时 <c>TestDirectory</c> = <c>.../unity/Library/ScriptAssemblies</c>(**不是** Assets),
        /// 而 Unity 测试的 <c>cwd</c> = <c>.../unity</c>。⇒ 以 <c>cwd</c> 为主锚,测试目录上溯为辅。</para></summary>
        private static IEnumerable<(string file, string text)> ReadInteractionSources()
        {
            // 主锚:cwd(= unity/ 项目根)→ Assets/Gameplay.Presentation/Interaction。
            // 辅锚:TestDirectory 上溯(不同 runner 布局的兜底)。
            var here = TestContext.CurrentContext.TestDirectory;
            var cwd = Directory.GetCurrentDirectory();
            var rel = Path.Combine("Assets", "Gameplay.Presentation", "Interaction");
            var candidates = new[]
            {
                Path.Combine(cwd, rel),                                  // cwd = unity/
                Path.Combine(cwd, "..", rel),                            // cwd = unity/<sub>
                Path.Combine(here, "..", "..", "..", rel),               // Library/ScriptAssemblies → unity/
                Path.Combine(here, "..", "..", "..", "..", rel),         // 再上一级
            };
            foreach (var dir in candidates)
            {
                var full = Path.GetFullPath(dir);
                if (Directory.Exists(full))
                {
                    foreach (var f in Directory.GetFiles(full, "*.cs", SearchOption.AllDirectories))
                        yield return (Path.GetFileName(f), File.ReadAllText(f));
                    yield break;
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  负夹具影子类型(住测试命名空间,仅反射面)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>影子:门内缓存被拒意图的队列(违反 F-4.2「丢弃而非排队」)。
    /// <para>⚠️ 判据是<b>可变字段</b>(非 readonly / 非 const)—— 故影子用<b>非 readonly</b> 字段,
    /// 与生产 `ModalGate` 的「零非 readonly 实例字段」判据同式。</para></summary>
    internal sealed class ShadowGateWithQueue
    {
        private List<InteractIntent> _queued;   // ← 违规:非 readonly ⇒ 可变字段(缓存被拒意图)
        public int Count => _queued?.Count ?? 0;
    }

    /// <summary>影子:旧「单 bool」压制实现(`OQ-4-13` 裁定前的形态)。
    /// <para>⚠️ 可执行 —— 用于<b>证伪</b>「单 bool + 三个互不知晓的写者」在
    /// AC-4-19 序列下**确实丢失更新**(4 的 Release 顺手清掉 10 的位)。生产已改 per-source 位图
    /// (<see cref="DaYiJingCheng.Gameplay.Presentation.Player.MotorLease"/>);本影子复现旧形态,</para>
    /// <para>与生产在<b>同一 Acquire/Release 序列</b>上形成对照 —— 非「自证空转」(结构侧 F-4)。</para></summary>
    internal sealed class SingleBoolSuppression
    {
        private bool _suppressed;   // ← 单 bool:任一源 Release 即清全态(无 per-source 位)

        public bool IsSuppressed => _suppressed;

        public void Acquire(LeaseSource source) => _suppressed = true;

        public void Release(LeaseSource source) => _suppressed = false;
    }
}
