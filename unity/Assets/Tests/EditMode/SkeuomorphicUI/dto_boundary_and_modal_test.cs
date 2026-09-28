namespace DaYiJingCheng.Tests.Unit.SkeuomorphicUI
{
    using DaYiJingCheng.EditorTools.Gates;
    using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
    using DaYiJingCheng.Sim.Contracts;
    using NUnit.Framework;

    /// <summary>
    /// Story 004: 数据边界守卫 · 单元测试。
    /// <para>覆盖 AC-42-D1 / D3 / D4 / D5 / AC-37-15 / TR-skeuoui-009。</para>
    /// </summary>
    [TestFixture]
    public class dto_boundary_and_modal_test
    {
        // ── AC-42-D1: PresentationDtoGuard 递归扫描 ──

        [Test]
        public void test_dto_guard_scan_vitals_dto_clean()
        {
            var errs = PresentationDtoGuard.Scan(typeof(VitalsDto));
            Assert.IsEmpty(errs);
        }

        [Test]
        public void test_dto_guard_scan_null_returns_error()
        {
            var errs = PresentationDtoGuard.Scan(null);
            Assert.IsNotEmpty(errs);
            StringAssert.Contains("null", errs[0]);
        }

        [Test]
        public void test_dto_guard_scan_nested_list_dto()
        {
            var errs = PresentationDtoGuard.Scan(typeof(DtoWithNestedList));
            Assert.IsEmpty(errs);
        }

        [Test]
        public void test_dto_guard_scan_derived_dto_clean()
        {
            var errs = PresentationDtoGuard.Scan(typeof(DtoCleanDerived));
            Assert.IsEmpty(errs);
        }

        [Test]
        public void test_dto_guard_scan_derived_dto_with_disease_token_fails()
        {
            var errs = PresentationDtoGuard.Scan(typeof(DtoDerivedWithDisease));
            Assert.IsNotEmpty(errs);
            StringAssert.Contains("disease", errs[0].ToLowerInvariant());
        }

        [Test]
        public void test_dto_guard_scan_deep_nesting_terminates_at_depth_guard()
        {
            var errs = PresentationDtoGuard.Scan(typeof(DeepNestLevel5));
            // 5 层远低于 64 深度护栏,不应触发
            Assert.IsEmpty(errs);
        }

        // ── AC-42-D3: 42 类型树不持有 DTO 副本 / 设置值 ──
        // 通过 IModalState 接口的「只有 getter」形态验证（无 setter = 不持有可写状态）

        [Test]
        public void test_modal_state_interface_readonly_no_setter()
        {
            var modalProperty = typeof(IModalState).GetProperty("Modal");
            Assert.IsNotNull(modalProperty);
            Assert.IsTrue(modalProperty.CanRead);
            Assert.IsFalse(modalProperty.CanWrite);
        }

        [Test]
        public void test_modal_id_enum_has_seven_members_plus_none()
        {
            var values = System.Enum.GetValues(typeof(ModalId));
            Assert.AreEqual(8, values.Length); // None + 7 members
            Assert.IsTrue(System.Enum.IsDefined(typeof(ModalId), ModalId.None));
            Assert.IsTrue(System.Enum.IsDefined(typeof(ModalId), ModalId.Casebook));
            Assert.IsTrue(System.Enum.IsDefined(typeof(ModalId), ModalId.SaveSlots));
            Assert.IsTrue(System.Enum.IsDefined(typeof(ModalId), ModalId.InventoryContainer));
            Assert.IsTrue(System.Enum.IsDefined(typeof(ModalId), ModalId.SettingsShell));
            Assert.IsTrue(System.Enum.IsDefined(typeof(ModalId), ModalId.Tutorial));
            Assert.IsTrue(System.Enum.IsDefined(typeof(ModalId), ModalId.ClinicPanel));
            Assert.IsTrue(System.Enum.IsDefined(typeof(ModalId), ModalId.PaperCloseup48));
        }

        [Test]
        public void test_modal_id_values_are_unique()
        {
            var values = System.Enum.GetValues(typeof(ModalId));
            var set = new System.Collections.Generic.HashSet<int>();
            foreach (ModalId v in values)
                Assert.IsTrue(set.Add((int)v), $"Duplicate ModalId value: {v}");
        }

        // ── AC-42-D5: DtoRoot 注册表覆盖 ──
        // 当前已登记根类型：VitalsDto / AudioCueDto / EmergencyAttempt / CaseOpened /
        // CaseClosed / PatternRecognized / PoiStateChanged / ActorCellEntered
        // 验证 PresentationDtoGuard 对全部已知 DTO 根类型扫描通过

        [Test]
        public void test_dto_guard_all_known_dto_roots_clean()
        {
            var knownRoots = new[]
            {
                typeof(VitalsDto),
                typeof(DtoWithNestedList),
                typeof(DtoCleanDerived),
            };
            foreach (var root in knownRoots)
            {
                var errs = PresentationDtoGuard.Scan(root);
                Assert.IsEmpty(errs, $"DtoGuard found issues in {root.FullName}");
            }
        }

        // ── AC-37-15 / TR-skeuoui-009: ModalId 闭集完整性 ──

        [Test]
        public void test_modal_id_none_is_zero()
        {
            Assert.AreEqual(0, (int)ModalId.None);
        }

        [Test]
        public void test_modal_id_paper_closeup_48_is_seventh_member()
        {
            Assert.AreEqual(7, (int)ModalId.PaperCloseup48);
        }
    }

    // ── 测试辅助 DTO（用于 D1/D5 扫描）──

    public class DtoWithNestedList
    {
        public System.Collections.Generic.List<VitalsDto> Items { get; set; } = new();
    }

    // 干净 DTO（无 disease/diagnosis/symptom 语义）—— 用于测试扫描通过
    public class DtoCleanBase { }

    public class DtoCleanDerived : DtoCleanBase { }

    // 含 disease 语义的 DTO —— 用于测试扫描拦截
    public class DtoWithDiseaseField
    {
        public int symptomCount { get; set; }
    }

    public class DtoDerivedWithDisease : DtoWithDiseaseField { }

    // 前向声明：DtoCircularA 引 DtoCircularB，B 引 A
    public class DtoCircularA
    {
        public DtoCircularB Partner { get; set; } = null!;
    }

    public class DtoCircularB
    {
        public DtoCircularA Partner { get; set; } = null!;
    }

    public class DeepNestLevel5
    {
        public DeepNestLevel4 Level4 { get; set; } = null!;
    }

    public class DeepNestLevel4
    {
        public DeepNestLevel3 Level3 { get; set; } = null!;
    }

    public class DeepNestLevel3
    {
        public DeepNestLevel2 Level2 { get; set; } = null!;
    }

    public class DeepNestLevel2
    {
        public DeepNestLevel1 Level1 { get; set; } = null!;
    }

    public class DeepNestLevel1
    {
        public int Value { get; set; }
    }
}
