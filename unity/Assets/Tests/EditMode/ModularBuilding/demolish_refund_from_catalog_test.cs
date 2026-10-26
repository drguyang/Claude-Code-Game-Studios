// `TD-building-01` 解除动作 · 读侧的 **(b) `Cost` 从目录读** 载体(2026-10-25 · S20-2a)。
//
// 权威来源:
//   `docs/tech-debt-register.md` §`TD-building-01`「解除动作 · 读侧」逐字:
//     「`ModuleDefinition` 补 `Cost` + `DemolishChecker` 从目录读」
//   §`TD-itemdb-01` 实测 ⑧:`cost` 在 `Sim/World` 全树的唯一形态曾是**形参**
//     (`CalculateRefund(int cost, …)`),由调用方喂、**不从目录读**,且生产树调用点 = 零。
//   GDD `design/gdd/modular-building.md` F-23-3(拆除返还)· AC-23-08(目录校验 `cost ≥ ⌈1/R⌉`)
//   ADR-006(整数域 · `⌊cost × R⌋` 向下取整)
//
// ⚠️ **判据形态 = 字面量神谕**:期望值写成手算整数(如 `cost=4`、`R=1.0` ⇒ `4`),
//   **不得**写成 `RefundCalculator.CalculateRefund(def.Cost, r)` ——
//   那等于拿被测件当期望值(自指断言 = 假绿形态,本项目已抓过多次)。
//
// ⚠️ **本文件不重复测公式本身**:`⌊cost × R⌋` 的边界在 `refund_calculator_test.cs`
//   (纯公式核,静态式)。本文件只钉「**cost 从哪来**」与「**目录缺行时怎么办**」。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    /// <summary>
    /// (b) `DemolishChecker` 的返还读路径:**cost 从 `IModuleCatalog` 读**,缺行 fail-closed。
    /// </summary>
    [TestFixture]
    public class DemolishRefundFromCatalogTest
    {
        /// <summary>Q16.16 的 1.0(P0 `DEMOLISH_REFUND_RATIO`)。</summary>
        private const int RatioOne = 65536;

        /// <summary>Q16.16 的 0.5。</summary>
        private const int RatioHalf = 32768;

        /// <summary>目录里的模块 A:cost = 4(字面量神谕的基准)。</summary>
        private const int ModuleCheap = 11;

        /// <summary>目录里的模块 B:cost = 9(与 A 不同 ⇒ 证明返还随目录行变,不是常量)。</summary>
        private const int ModuleDear = 12;

        /// <summary>**不在**任何目录里的模块 id(fail-closed 腿)。</summary>
        private const int ModuleUnknown = 999;

        private StructureInstanceRegistry _registry;
        private TestModuleCatalog _catalog;
        private DemolishChecker _checker;

        [SetUp]
        public void Setup()
        {
            _registry = new StructureInstanceRegistry();
            _catalog = new TestModuleCatalog();
            _catalog.RegisterModule(MakeModule(ModuleCheap, cost: 4));
            _catalog.RegisterModule(MakeModule(ModuleDear, cost: 9));

            _checker = new DemolishChecker(
                _registry, new EmptyOccupancyQuery(), new AlwaysEmptyPresence(), _catalog);
        }

        private static ModuleDefinition MakeModule(int moduleId, int cost) =>
            new ModuleDefinition(
                moduleId: moduleId,
                slotType: SlotType.Decor,
                localOccupancy: new List<WorldPos> { new WorldPos(0, 0, 0) },
                validOrientations: new[] { 0 },
                cost: cost,
                buildItemKey: 0,
                refundable: 1);

        // ══════════ 1. 正常路径:返还 = 目录 cost × ratio(字面量神谕)══════════

        [Test]
        public void test_calculateRefund_ratioOne_returnsCatalogCost()
        {
            // Arrange: 结构指向 cost = 4 的模块
            long sid = _registry.Register(new WorldPos(0, 0, 0), ModuleCheap, 0, 0);

            // Act
            int refund = _checker.CalculateRefund(sid, RatioOne);

            // Assert: 字面量 4 —— 若 cost 不由目录读(或读到默认 0),此处必不为 4
            Assert.AreEqual(4, refund,
                "🔴 (b):R=1.0 时返还须 == 目录行 cost(4);0 ⇒ cost 未从目录读;"
                + "别的数 ⇒ 读错了行 / 公式用错");
        }

        [Test]
        public void test_calculateRefund_ratioHalf_floorsCatalogCost()
        {
            // Arrange: cost = 9, R = 0.5 ⇒ ⌊4.5⌋ = 4
            long sid = _registry.Register(new WorldPos(0, 0, 0), ModuleDear, 0, 0);

            // Act
            int refund = _checker.CalculateRefund(sid, RatioHalf);

            // Assert
            Assert.AreEqual(4, refund,
                "🔴 (b):⌊9 × 0.5⌋ = 4(向下取整);5 ⇒ 用了四舍五入 / 向上取整");
        }

        [Test]
        public void test_calculateRefund_twoModules_differByCatalogRow()
        {
            // Arrange: 同一 checker、同一 ratio,两个不同 cost 的结构
            long cheap = _registry.Register(new WorldPos(0, 0, 0), ModuleCheap, 0, 0);
            long dear = _registry.Register(new WorldPos(1, 0, 0), ModuleDear, 0, 0);

            // Act
            int cheapRefund = _checker.CalculateRefund(cheap, RatioOne);
            int dearRefund = _checker.CalculateRefund(dear, RatioOne);

            // Assert: 差 = 5(= 9 − 4)。若 cost 是硬编码常量或形参,两值会相等 ⇒ 红
            Assert.AreEqual(4, cheapRefund);
            Assert.AreEqual(9, dearRefund);
            Assert.AreNotEqual(cheapRefund, dearRefund,
                "🔴 (b):不同目录行须给不同返还 —— 相等 ⇒ cost 不随目录行走");
        }

        // ══════════ 2. fail-closed:结构不存在 ⇒ 0 ══════════

        [Test]
        public void test_calculateRefund_unknownStructure_returnsZero()
        {
            // Act: 从未注册的 structure_id
            int refund = _checker.CalculateRefund(424242L, RatioOne);

            // Assert
            Assert.AreEqual(0, refund, "结构不存在 ⇒ 返还 0(不得抛、不得发幻影返还)");
        }

        // ══════════ 3. fail-closed:模块不在目录 ⇒ 0 ══════════

        [Test]
        public void test_calculateRefund_moduleNotInCatalog_returnsZero()
        {
            // Arrange: 结构指向一个**目录里没有**的 module id
            //   (构造路径:直调 registry.Register —— `StructureWriter.Place` 对未知 id 是 fail-closed 抛,
            //    但注册表本身允许;生产里该形态来自**目录改版后残留的旧结构**,是真实可达状态)
            long sid = _registry.Register(new WorldPos(0, 0, 0), ModuleUnknown, 0, 0);

            // Act
            int refund = _checker.CalculateRefund(sid, RatioOne);

            // Assert
            Assert.AreEqual(0, refund,
                "🔴 (b):模块不在目录 ⇒ 返还 0(fail-closed)。无目录行 = 无成本真源,"
                + "不得拿「公式值」冒充(去掉 `!defOpt.HasValue` 守卫后本行会抛 / 给非零)");
        }

        [Test]
        public void test_calculateRefund_emptyCatalog_returnsZeroNotThrow()
        {
            // Arrange: 空目录 + 有结构 —— 与上测不同:目录**整个**为空(生产里 = 烘焙缺失)
            var emptyCatalog = new TestModuleCatalog();
            var checker = new DemolishChecker(
                _registry, new EmptyOccupancyQuery(), new AlwaysEmptyPresence(), emptyCatalog);
            long sid = _registry.Register(new WorldPos(0, 0, 0), ModuleCheap, 0, 0);

            // Act / Assert: 不抛,返还 0
            Assert.DoesNotThrow(() => checker.CalculateRefund(sid, RatioOne),
                "空目录 ⇒ 须 fail-closed 返回 0,不得 NullReference / InvalidOperation");
            Assert.AreEqual(0, checker.CalculateRefund(sid, RatioOne));
        }

        // ══════════ 4. `ValidateRefundable(moduleId, ratio)` —— 目录读 + fail-closed ══════════

        [Test]
        public void test_validateRefundable_ratioOne_passesForCatalogRows()
        {
            // R = 1.0 ⇒ ⌈1/1⌉ = 1,cost ≥ 1 的两行都通过
            Assert.IsTrue(_checker.ValidateRefundable(ModuleCheap, RatioOne), "cost=4 ≥ 1 ⇒ 通过");
            Assert.IsTrue(_checker.ValidateRefundable(ModuleDear, RatioOne), "cost=9 ≥ 1 ⇒ 通过");
        }

        [Test]
        public void test_validateRefundable_ratioHalf_thresholdFromCatalogCost()
        {
            // R = 0.5 ⇒ ⌈1/0.5⌉ = 2;两行 cost 都 ≥ 2 ⇒ 都通过。
            //   判别力:阈值若被写死成别的数(如 5),cost=4 会转 false ⇒ 红。
            Assert.IsTrue(_checker.ValidateRefundable(ModuleCheap, RatioOne),
                "前置:R=1 下 cost=4 通过");

            // 反向腿:目录里放一行 cost=1,R=0.5 ⇒ 1 < 2 ⇒ false(证明用的是目录 cost)
            var lowCatalog = new TestModuleCatalog();
            lowCatalog.RegisterModule(MakeModule(ModuleCheap, cost: 1));
            var lowChecker = new DemolishChecker(
                _registry, new EmptyOccupancyQuery(), new AlwaysEmptyPresence(), lowCatalog);

            Assert.IsFalse(lowChecker.ValidateRefundable(ModuleCheap, RatioHalf),
                "🔴 (b):cost=1 在 R=0.5 下不满足 ⌈1/R⌉=2 ⇒ false;true ⇒ 未用目录 cost 比较");
            Assert.IsTrue(lowChecker.ValidateRefundable(ModuleCheap, RatioOne),
                "同一行 R=1 下通过(证明 false 来自阈值,不是恒定 false)");
        }

        [Test]
        public void test_validateRefundable_moduleNotInCatalog_returnsFalse()
        {
            Assert.IsFalse(_checker.ValidateRefundable(ModuleUnknown, RatioOne),
                "🔴 (b):模块不在目录 ⇒ false(无成本真源即不可判通过)");
        }

        [Test]
        public void test_validateRefundable_emptyCatalog_returnsFalse()
        {
            var emptyCatalog = new TestModuleCatalog();
            var checker = new DemolishChecker(
                _registry, new EmptyOccupancyQuery(), new AlwaysEmptyPresence(), emptyCatalog);

            Assert.IsFalse(checker.ValidateRefundable(ModuleCheap, RatioOne),
                "空目录 ⇒ 任何模块都不可判通过(fail-closed)");
        }

        // ══════════ 5. 生产目录:真烘焙产物的 cost 真的被读到 ══════════

        /// <summary>
        /// `build_modules.cooked` 的行:module 1(`Bed`)= cost **4**;module 2(`surgery_table`)= cost **8**。
        /// <para>期望值是 `assets/data/build_modules.json` 的**字面量**,不是从被测件回读 ——
        /// 若作者态改值,本测红 ⇒ 提醒同步(与 `clinic_room_matching_e2e_test` 的 id 门同款)。</para>
        /// </summary>
        [Test]
        public void test_calculateRefund_realBakedCatalog_usesAuthoredCost()
        {
            // Arrange
            var realCatalog = new BuildModulesCatalog(DataCookedDisk.LoadBuildModules());
            var checker = new DemolishChecker(
                _registry, new EmptyOccupancyQuery(), new AlwaysEmptyPresence(), realCatalog);
            long bed = _registry.Register(new WorldPos(0, 0, 0), moduleId: 1, 0, 0);
            long table = _registry.Register(new WorldPos(1, 0, 0), moduleId: 2, 0, 0);

            // Act
            int bedRefund = checker.CalculateRefund(bed, RatioOne);
            int tableRefund = checker.CalculateRefund(table, RatioOne);

            // Assert: 字面量 4 / 8 —— 来自 `assets/data/build_modules.json`
            Assert.AreEqual(4, bedRefund, "module 1(Bed)的作者态 cost = 4");
            Assert.AreEqual(8, tableRefund, "module 2(surgery_table)的作者态 cost = 8");
        }

        [Test]
        public void test_calculateRefund_realBakedCatalog_shellIsNotRefundable()
        {
            // module 8 = Shell,refundable = false,cost = 1 —— 返还公式本身不看 refundable
            //   (那是 `Check` / `ValidateRefundable` 的职责),本测只钉「目录读到的是该行 cost」
            var realCatalog = new BuildModulesCatalog(DataCookedDisk.LoadBuildModules());
            var checker = new DemolishChecker(
                _registry, new EmptyOccupancyQuery(), new AlwaysEmptyPresence(), realCatalog);
            long shell = _registry.Register(new WorldPos(0, 0, 0), moduleId: 8, 0, 0);

            Assert.AreEqual(1, checker.CalculateRefund(shell, RatioOne),
                "module 8 的 cost = 1(字面量);0 ⇒ 未读到目录行 / 误把 refundable 当 cost");
        }
    }
}
