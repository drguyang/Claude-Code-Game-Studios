// S20-12 · inventory story-004 —— 载重裁决 `CarryLoad` / `CanCarry` / `CarryRatioDto`
//
// 权威来源:
//   design/gdd/inventory-and-items.md
//     · 规则四 F-20.1(:435-450 载重整数求和)· F-20.2(:464 容量判定 + :474 硬不变量)
//     · 边例 :623-624(恰等界合法 / cap 未定值硬失败)
//     · §Tuning Knobs :705-720(CARRY_CAP 安全区间 + 满档口径)
//     · AC-20-08 / 09 / 13 / 18 / 21 / 24
//   ADR-006 §一 D-21-17(weight / cap = int 计数,移出 Fix 解析集)· §二(整数域求值)
//   ADR-014 §五(装载期硬失败纪律,e-13 同款)· ADR-025 §①(`Sim` 引用集 = {BCL, Sim.Contracts})
//   story production/epics/inventory-items/story-004-carryload-and-cap-adjudication.md
//
// ⚠️ **AC 记分(禁把「本体跑通」读成「六条全绿」)** —— 逐条与判据载体对齐:
//     ✅ AC-20-08 · 零浮点:源码面(剥注释扫 float/double/decimal + 转换指令)+ IL 面
//                    (Cecil 扫 `Sim` 装配里四个载重类型的 `conv.r4/r8` / `ldc.r4/r8`)。
//     ✅ AC-20-09 · 硬不变量:写入路径**后置断言**(CanCarry 通过 ⇒ 后态仍满足)+
//                    **反向夹具**(绕过 CanCarry 的写入 ⇒ 后置断言必红)。
//     ✅ AC-20-13 · 装载期校验**负路径**:缺键 / 0 / 负 / 越 int / 非 int token 各一条具名错误
//                    (2026-10-26 用户裁定「改测装载期校验的负路径」—— AC 语义不变,
//                     载体从「cap 未定值」改成「五种未定值形态」)。
//     ✅ AC-20-18 · 满档等价:`IsFullForAnyAddition ⟺ load > cap − W_MIN`,扫描线四点 +
//                    **禁 `== cap` 简化的反证**(W_MIN=1 与 W_MIN>1 两种表)。
//     ✅ AC-20-21 · 存在性门:自指谓词(构造可达满状态)+ 空表 / 全非法重量 ⇒ 门红。
//     ✅ AC-20-24 · 单源零派生:反射扫 `CarryRatioDto` 恰两字段 + 类型面(无 float / 无派生容器)
//                    + 生产代码零容量常量。
//
// ⚠️ **三个必须读清的近邻风险**:
//   ① 「`CanCarry` 谓词对」≠「Story 005 的原子装卸已实现」—— 本件只给谓词真值 +
//      后置断言**模板**,真正的 Apply 入口归 Story 005。
//   ② 「`CarryRatioDto` 零派生字段」≠「42 的四档呈现已实现」—— 分档与单调性归 42(R9 ④)。
//   ③ 「`CARRY_CAP = 60` 在数据表里」≠「运行期装配读到它」—— `IDataProvider` 消费方
//      尚未接(归装配轮);本件的门直读作者态 JSON,fail-closed。
//
// ⚠️ 零随机 / 零时间依赖 / 零写盘;全部 id / 数值写死。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DaYiJingCheng.Tests.EditMode.Inventory
{
    /// <summary>inventory story-004 的 EditMode 夹具(六条 AC 全覆盖)。</summary>
    [TestFixture]
    public class CarryLoadAdjudicationTest
    {
        // ══════════════════════════════════════════════════════════════════
        // 共享夹具
        // ══════════════════════════════════════════════════════════════════

        /// <summary>ordinal → weight 的测试物品表(夹具值,非 GDD 数值)。</summary>
        private static readonly Dictionary<int, int> UnitWeights = new Dictionary<int, int>
        {
            [0] = 2,   // willow_bark
            [1] = 1,   // foxglove
            [2] = 5,   // iron_scalpel(重)
            [3] = 3,   // cinchona_bark
            [4] = 7,   // 最重(用于最坏情况扫描)
        };

        private static int WeightOf(int ordinal)
            => UnitWeights.TryGetValue(ordinal, out int w) ? w : 0;   // 未知 ⇒ 0 ⇒ 被测件 fail-loud

        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;

        [SetUp]
        public void Setup()
        {
            var pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(pool);
            _decoder = new PayloadDecoder(pool);
        }

        // ── 事件构造(与 S20-10 测试同款；真编码器 + 真池 + 真解码器，零 mock)──

        private SimEvent Ev<T>(EventKind kind, T payload, long tick, long seq = 0L) where T : struct
            => new SimEvent(tick, PatientId.None, seq, kind, _encoder.Encode(kind, payload));

        private SimEvent Spawn(long id, int itemKey, int qty, long tick, long seq = 0L)
            => Ev(EventKind.DropSpawned,
                  new DropSpawnedPayload(id, new WorldPos(1, 2, 3), itemKey, qty), tick, seq);

        private SimEvent Harvest(long id, int nodeId, int qty, int outQuality, long tick, long seq = 0L)
            => Ev(EventKind.ResourceHarvested,
                  new ResourceHarvestedPayload(id, nodeId, 1, qty, outQuality), tick, seq);

        private SimEvent Claim(long id, int claimer, long tick, long seq = 0L)
            => Ev(EventKind.DropClaimed, new DropClaimedPayload(id, claimer, tick), tick, seq);

        /// <summary>造一个 owner 名下的投影:每个 (itemKey, qty) 一件,id 从 1 递增。</summary>
        private InventoryProjection ProjectionFor(int owner, params (int itemKey, int qty)[] stacks)
        {
            var events = new List<SimEvent>();
            long tick = 1;
            for (int i = 0; i < stacks.Length; i++)
            {
                long id = i + 1;
                events.Add(Spawn(id, stacks[i].itemKey, stacks[i].qty, tick, seq: 0));
                events.Add(Harvest(id, nodeId: 100 + i, qty: stacks[i].qty, outQuality: 1, tick, seq: 1));
                events.Add(Claim(id, owner, tick + 1, seq: 0));
                tick += 2;
            }
            return InventoryFold.Fold(events, _decoder).Projection;
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-20-08 · 零浮点(源码面 + IL 面双判)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>载重路径的四个生产类型所在文件 —— 扫描面的单一出处。</summary>
        private static readonly string[] CarryLoadSourceFiles =
        {
            "Assets/Sim/ItemDatabase/CarryLoad.cs",
            "Assets/Sim/ItemDatabase/ContainerWeightFold.cs",
        };

        [Test]
        public void test_ac2008_sourceSurface_hasNoFloatingPointTypes()
        {
            string root = RepoRoot();
            var hits = new List<string>();
            foreach (string rel in CarryLoadSourceFiles)
            {
                string path = Path.Combine(root, "unity", rel);
                Assert.That(File.Exists(path), Is.True, $"扫描面文件不存在:{rel}(判据载体被搬?)");

                string code = StripComments(File.ReadAllText(path));
                foreach (string bad in new[] { "float", "double", "decimal" })
                {
                    // 词边界匹配,但排除 `ToFloat` 这类标识符里的子串。
                    foreach (System.Text.RegularExpressions.Match m in
                             System.Text.RegularExpressions.Regex.Matches(
                                 code, $@"(?<![A-Za-z0-9_]){bad}(?![A-Za-z0-9_])"))
                        hits.Add($"{rel}:{LineOf(code, m.Index)} 命中 `{bad}`");
                }
            }
            Assert.That(hits, Is.Empty,
                "载重路径出现浮点类型 —— AC-20-08 / 门 A:\n  " + string.Join("\n  ", hits));
        }

        [Test]
        public void test_ac2008_sourceSurface_hasNoFloatLiterals()
        {
            string root = RepoRoot();
            var hits = new List<string>();
            var lit = new System.Text.RegularExpressions.Regex(
                @"(?<![A-Za-z0-9_.])([0-9]+\.[0-9]+|[0-9]+[eE][-+]?[0-9]+)[fFdDmM]?");
            foreach (string rel in CarryLoadSourceFiles)
            {
                string code = StripComments(File.ReadAllText(Path.Combine(root, "unity", rel)));
                foreach (System.Text.RegularExpressions.Match m in lit.Matches(code))
                {
                    // 排除 XML doc 里的 `<c>1.0</c>` 之类已被注释剥离;此处只剩真字面量。
                    // ⚠️ 版本号形如 `2026-10-26` 不含小数点,不会命中。
                    hits.Add($"{rel}:{LineOf(code, m.Index)} 浮点字面量 `{m.Value}`");
                }
            }
            Assert.That(hits, Is.Empty,
                "载重路径出现浮点字面量(ADR-006 §二:整数域求值):\n  " + string.Join("\n  ", hits));
        }

        [Test]
        public void test_ac2008_ilSurface_hasNoFloatOpcodes()
        {
            string dll = AssemblyGates.ScriptAssemblyPath("Sim");
            Assert.That(File.Exists(dll), Is.True, $"Sim 装配不存在:{dll}");

            var targets = new HashSet<string>(StringComparer.Ordinal)
            {
                "DaYiJingCheng.Sim.CarryLoadSolver",
                "DaYiJingCheng.Sim.CarryRatioDto",
                "DaYiJingCheng.Sim.CarryCapacity",
                "DaYiJingCheng.Sim.ContainerWeightFold",
            };

            var hits = new List<string>();
            int scannedMethods = 0;

            using (ModuleDefinition mod = ModuleDefinition.ReadModule(dll))
            {
                foreach (TypeDefinition t in mod.GetTypes())
                {
                    if (!targets.Contains(t.FullName)) continue;
                    foreach (MethodDefinition m in t.Methods)
                    {
                        if (!m.HasBody) continue;
                        scannedMethods++;
                        foreach (Instruction ins in m.Body.Instructions)
                            if (IsFloatOpcode(ins.OpCode))
                                hits.Add($"{t.FullName}.{m.Name}: {ins.OpCode} @ IL_{ins.Offset:X4}");
                    }
                }
            }

            Assert.That(scannedMethods, Is.GreaterThan(0),
                "IL 扫描扫到 **零个** 方法体 —— 靶类型没找着(装配名/类型名漂移?),拒以空集冒充绿");
            Assert.That(hits, Is.Empty,
                "载重路径 IL 面出现浮点指令(反射看不见的常量折叠 / 库返回值落栈):\n  "
                + string.Join("\n  ", hits));
        }

        [Test]
        public void test_ac2008_projectionInput_weightsAreIntNotFix()
        {
            // 签名面:`Func<int,int>` 而非 `Func<int,Fix>` —— 编译器直接判,
            // 且 Fix 刻意无 implicit operator int ⇒ 静态类型对不上就编不过。
            System.Reflection.MethodInfo sum = typeof(CarryLoadSolver).GetMethod(
                nameof(CarryLoadSolver.Sum), new[]
                {
                    typeof(InventoryProjection),
                    typeof(Func<int, int>),
                    typeof(int),
                });
            Assert.That(sum, Is.Not.Null,
                "`CarryLoadSolver.Sum` 的 weight 查询须为 `Func<int,int>`(int 计数口径,D-21-17);" +
                "若改成 `Func<int,Fix>` 则「weight 移出 Fix 解析集」被破坏");

            System.Reflection.ParameterInfo[] ps = sum.GetParameters();
            Assert.That(ps[1].ParameterType, Is.EqualTo(typeof(Func<int, int>)),
                "第 2 参须为 `Func<int,int>`");
        }

        [Test]
        public void test_ac2008_carryCapacity_rejectsFixTypedCap()
        {
            // Fix 口径误用 ⇒ 具名硬失败(不是静默截断成 int)。
            Assert.Throws<CarryCapUndefinedException>(
                () => { var _ = new CarryCapacity(Fix.One); },
                "`new CarryCapacity(Fix)` 必须抛 —— 容量是 int 计数口径(D-21-17)");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-20-09 · 硬不变量(写入路径后置断言 + 反向夹具)
        //   2026-10-26 用户裁定「测写入路径后置断言(归纳闭合)」
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Story 005 的 Apply 入口在本件**尚不存在** —— 故用本模板模拟写入路径。
        /// <para>⚠️ 它**不是**生产代码,是「后置断言」这一判据形态的载体:
        /// 本件交付的是**断言谓词与归纳闭合关系**,不是 Apply 实现。</para>
        /// </summary>
        private static readonly Func<long, int, int, CarryCapacity, bool> ApplyGate =
            (load, unitWeight, qty, cap) => CarryLoadSolver.CanCarry(load, unitWeight, qty, cap);

        [Test]
        public void test_ac2009_writePathPostcondition_holdsAcrossArbitrarySequence()
        {
            const int capValue = 13;
            var capacity = new CarryCapacity(capValue);

            // 固定伪随机序列(自实现 SplitMix 式 LCG —— 不用 Unity Random,零引擎依赖)。
            uint state = 20261026u;
            var weights = new[] { 1, 2, 3, 5, 7 };

            long load = 0L;
            int accepted = 0, rejected = 0;

            for (int step = 0; step < 1000; step++)
            {
                state = unchecked(state * 1664525u + 1013904223u);
                int unit = weights[(int)(state % (uint)weights.Length)];
                int qty = 1 + (int)((state >> 8) % 4u);

                bool ok = ApplyGate(load, unit, qty, capacity);
                if (ok)
                {
                    // ── 后置断言(归纳闭合):通过 ⇒ 后态仍然满足硬不变量 ──
                    load = checked(load + ContainerWeightFold.InstanceWeight(unit, qty));
                    Assert.That(load, Is.LessThanOrEqualTo(capValue),
                        $"第 {step} 步:谓词通过后载重 {load} 越过 cap {capValue} —— " +
                        "CanCarry 与求和件口径不一致(AC-20-09 硬不变量)");
                    accepted++;
                }
                else
                {
                    // ── 拒绝 ⇒ 载重**不动**(原子性的一半;另一半归 Story 005)──
                    long before = load;
                    rejected++;
                    Assert.That(load, Is.EqualTo(before), "拒绝路径不得改载重");
                }
            }

            Assert.That(accepted, Is.GreaterThan(0), "1000 步里一件都没装上 —— 序列构造失效(假绿)");
            Assert.That(rejected, Is.GreaterThan(0), "1000 步里一次都没被拒 —— 满载不可达(假绿)");
        }

        [Test]
        public void test_ac2009_writePathBypassingCanCarry_breaksPostcondition()
        {
            // ── 反向夹具:绕过 CanCarry 的写入 ⇒ 后置断言必红 ──
            // 本夹具**故意**模拟「有人跳过裁决直接加」,断言后态违反不变量。
            // 它证明上一条夹具的判别力:若把 ApplyGate 换成「无条件 true」,上面那条会红。
            const int capValue = 13;
            const int unit = 7;
            const int qty = 2;                    // 7×2 = 14 > 13

            var capacity = new CarryCapacity(capValue);
            long load = 0L;

            // 正确路径:拒
            Assert.That(ApplyGate(load, unit, qty, capacity), Is.False,
                "7×2=14 应超过 cap 13 —— 谓词须拒");

            // 绕过路径(反面对照):直接加 ⇒ 后态违反不变量
            long bypassed = checked(load + ContainerWeightFold.InstanceWeight(unit, qty));
            Assert.That(bypassed, Is.GreaterThan(capValue),
                "绕过 CanCarry 直接累加后,载重**必须**越过 cap —— " +
                "否则「后置断言」这条夹具对变异毫无判别力(假绿)");
        }

        [Test]
        public void test_ac2009_exactCapBoundary_isAccepted()
        {
            // GDD :623「CarryLoad 恰等 CARRY_CAP」⇒ **允许**(`≤` 非 `<`)。
            var capacity = new CarryCapacity(10);
            Assert.That(CarryLoadSolver.CanCarry(4L, 3, candidateQty: 2, capacity), Is.True,
                "4 + 3×2 = 10 == cap 10 ⇒ 允许(等号侧合法,`≤` 不是 `<`)");

            long load = 10L;
            Assert.That(load, Is.LessThanOrEqualTo(capacity.Cap),
                "恰等界仍满足硬不变量");
        }

        [Test]
        public void test_ac2009_oneOverCap_isRejected()
        {
            var capacity = new CarryCapacity(10);
            Assert.That(CarryLoadSolver.CanCarry(4L, 3, candidateQty: 2 + 1, capacity), Is.False,
                "4 + 3×3 = 13 > 10 ⇒ 拒");
            Assert.That(CarryLoadSolver.CanCarry(9L, 2, candidateQty: 1, capacity), Is.False,
                "9 + 2 = 11 > 10 ⇒ 拒(哪怕只超 1)");
        }

        [Test]
        public void test_ac2009_negativeLoad_isRejectedNotSilentlyClamped()
        {
            var capacity = new CarryCapacity(10);
            Assert.Throws<InvalidOperationException>(
                () => CarryLoadSolver.CanCarry(-1L, 1, 1, capacity),
                "负载重不是「合法的小值」—— 它是投影/求和出错的信号,须硬失败而非静默当 0");
        }

        [Test]
        public void test_ac2009_emptyInventory_loadIsZero()
        {
            InventoryProjection empty = ProjectionFor(owner: 7);   // 零栈
            Assert.That(CarryLoadSolver.Sum(empty, WeightOf, 7), Is.EqualTo(0L));
            Assert.That(CarryLoadSolver.Sum(empty, WeightOf, 7), Is.LessThanOrEqualTo(60),
                "空库存满足硬不变量(60 = GDD 现行 cap;此处只作上界参照,不作判据值)");
        }

        [Test]
        public void test_ac2009_carryLoadSumsWeightTimesQty()
        {
            // (key 0, w=2) × 3 + (key 2, w=5) × 1 = 6 + 5 = 11
            InventoryProjection p = ProjectionFor(owner: 7, (0, 3), (2, 1));
            Assert.That(CarryLoadSolver.Sum(p, WeightOf, 7), Is.EqualTo(11L),
                "F-20.1:Σ(weight × qty),先乘后加");
        }

        [Test]
        public void test_ac2009_otherOwnersInstancesAreNotCounted()
        {
            var events = new List<SimEvent>
            {
                Spawn(1, 2, 2, 1, seq: 0), Harvest(1, 1, 2, 1, 1, seq: 1), Claim(1, 7, 2),   // 玩家 7:5×2=10
                Spawn(2, 3, 1, 3, seq: 0), Harvest(2, 2, 1, 1, 3, seq: 1), Claim(2, 8, 4),   // 玩家 8:3×1=3
            };
            InventoryProjection p = InventoryFold.Fold(events, _decoder).Projection;

            Assert.That(CarryLoadSolver.Sum(p, WeightOf, 7), Is.EqualTo(10L), "只算玩家 7 自己的");
            Assert.That(CarryLoadSolver.Sum(p, WeightOf, 8), Is.EqualTo(3L), "只算玩家 8 自己的");
        }

        [Test]
        public void test_ac2009_unknownOrdinal_failsLoudNotZero()
        {
            // 物品表缺条目 ⇒ 重量无从得知 ⇒ 硬失败(不得静默当 0 ⇒ 玩家可无限带)。
            InventoryProjection p = ProjectionFor(owner: 7, (99, 1));   // ordinal 99 不在表里
            var ex = Assert.Throws<ItemWeightUndefinedException>(
                () => CarryLoadSolver.Sum(p, WeightOf, 7),
                "unknown ordinal 须抛 ItemWeightUndefinedException,不得静默当 0(AC-20-13 邻域)");
            Assert.That(ex.Message, Does.Contain("99"), "错误信息须含实际 ordinal");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-20-13 · 装载期校验的负路径(五种未定值形态 + Fix 口径)
        //   2026-10-26 用户裁定「改测『装载期校验』的负路径」
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac2013_missingKey_throwsWithNamedError()
        {
            string json = "{ \"schema_version\": 1, \"_note\": \"缺 carry_cap\" }";
            BakeValidationException ex = Assert.Throws<BakeValidationException>(
                () => InventoryConstantsBinder.Bind(json));
            Assert.That(ex.Errors.Any(e => e.Contains("缺必填键") && e.Contains("carry_cap")), Is.True,
                "缺键须报具名错误:\n" + string.Join("\n", ex.Errors));
        }

        [Test]
        public void test_ac2013_zeroCap_throwsAndNamesTheValue()
        {
            BakeValidationException ex = Assert.Throws<BakeValidationException>(
                () => InventoryConstantsBinder.Bind(Json(0)));
            Assert.That(ex.Errors.Any(e => e.Contains("carry_cap = 0")), Is.True,
                "cap = 0 须报出实际值 0(与负值分开报):\n" + string.Join("\n", ex.Errors));
        }

        [Test]
        public void test_ac2013_negativeCap_throwsAndNamesTheValue()
        {
            BakeValidationException ex = Assert.Throws<BakeValidationException>(
                () => InventoryConstantsBinder.Bind(Json(-5)));
            Assert.That(ex.Errors.Any(e => e.Contains("carry_cap = -5")), Is.True,
                "cap < 0 须报出实际值 -5:\n" + string.Join("\n", ex.Errors));
        }

        [Test]
        public void test_ac2013_capBeyondIntRange_throws()
        {
            string json = "{ \"schema_version\": 1, \"_note\": \"越界\", \"carry_cap\": 4294967296 }";
            BakeValidationException ex = Assert.Throws<BakeValidationException>(
                () => InventoryConstantsBinder.Bind(json));
            Assert.That(ex.Errors.Any(e => e.Contains("越出 int 域")), Is.True,
                "越 int 域须报越界(不得截断):\n" + string.Join("\n", ex.Errors));
        }

        [Test]
        public void test_ac2013_capAsJsonString_throws()
        {
            string json = "{ \"schema_version\": 1, \"_note\": \"字符串\", \"carry_cap\": \"60\" }";
            BakeValidationException ex = Assert.Throws<BakeValidationException>(
                () => InventoryConstantsBinder.Bind(json));
            Assert.That(ex.Errors.Any(e => e.Contains("须为 JSON") && e.Contains("整数")), Is.True,
                "字符串形态须拒:\n" + string.Join("\n", ex.Errors));
        }

        [Test]
        public void test_ac2013_capAsFixLiteral_throwsAndNamesD2117()
        {
            // cap 是 int 计数口径,**不是** Fix —— 写 "3/4" 这类定点字面量须拒。
            string json = "{ \"schema_version\": 1, \"_note\": \"Fix 口径\", \"carry_cap\": \"3/4\" }";
            BakeValidationException ex = Assert.Throws<BakeValidationException>(
                () => InventoryConstantsBinder.Bind(json));
            Assert.That(ex.Errors.Any(e => e.Contains("Fix") || e.Contains("3/4")), Is.True,
                "Fix 口径须被点名(D-21-17):\n" + string.Join("\n", ex.Errors));
        }

        [Test]
        public void test_ac2013_capAsFloat_throws()
        {
            string json = "{ \"schema_version\": 1, \"_note\": \"浮点\", \"carry_cap\": 60.0 }";
            BakeValidationException ex = Assert.Throws<BakeValidationException>(
                () => InventoryConstantsBinder.Bind(json));
            Assert.That(ex.Errors.Any(e => e.Contains("浮点")), Is.True,
                "浮点 cap 须拒(恰等界比较不可判):\n" + string.Join("\n", ex.Errors));
        }

        [Test]
        public void test_ac2013_emptyState_constructedByConstruction()
        {
            // 类型面:「未定」在 `CarryCapacity` 上不可表达 —— `null` 与 `default` 都被拒。
            //
            // ⚠️ **必须按消息区分分支,不能只断异常类型**(2026-10-26 证伪轮 M5 抓出的假绿):
            //    首版三条都只写 `Assert.Throws<CarryCapUndefinedException>`,而 `null` 分支一旦
            //    被改成「兜底成 0」,代码会**落到下游 `cap.Value <= 0` 分支并抛出同一个异常类型**
            //    ⇒ 断言照绿。变异实测 54/54 全绿逃逸,证明该夹具当时对「未注入 ≠ 填了 0」零判别力。
            //    「未注入」(装配漏接线)与「填了 0」(数据填错)是**两种不同的故障**,日志必须可分辨
            //    —— 这也是生产代码把两条消息分开写的理由,夹具须把该理由钉住。
            var nullEx = Assert.Throws<CarryCapUndefinedException>(
                () => { var _ = new CarryCapacity(null); }, "null(未注入)须抛");
            Assert.That(nullEx.Message, Does.Contain("未注入"),
                "null 分支须走『未注入』消息,不得兜底成 0 后混入非正值分支(AC-20-13)");

            var zeroEx = Assert.Throws<CarryCapUndefinedException>(
                () => { var _ = new CarryCapacity(0); }, "0 须抛");
            Assert.That(zeroEx.Message, Does.Contain("非正"),
                "0 分支须走『非正』消息");
            Assert.That(zeroEx.Message, Does.Contain("= 0"),
                "0 分支须报出实际值(与负值可分辨)");

            var negEx = Assert.Throws<CarryCapUndefinedException>(
                () => { var _ = new CarryCapacity(-1); }, "负值须抛");
            Assert.That(negEx.Message, Does.Contain("= -1"),
                "负值分支须报出实际值(与 0 可分辨)");

            // 合法值不抛。
            var ok = new CarryCapacity(60);
            Assert.That(ok.Cap, Is.EqualTo(60));
        }

        [Test]
        public void test_ac2013_productionCode_hasNoDefaultCapacityConstant()
        {
            // 反射扫 Sim 装配里全部类型,拒绝 `const int ...Cap... = <正数>` 形态的默认容量。
            // ⚠️ 判据 = 「常量名含 Cap 且值为正」—— 与 AC-20-24 的「零第二容量常量」同源。
            var violations = new List<string>();
            foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                string name = asm.GetName().Name;
                if (name != "Sim" && name != "Sim.Contracts") continue;

                foreach (Type t in asm.GetTypes())
                {
                    if (t.IsNested) continue;
                    foreach (System.Reflection.FieldInfo f in t.GetFields(
                                 System.Reflection.BindingFlags.Public |
                                 System.Reflection.BindingFlags.NonPublic |
                                 System.Reflection.BindingFlags.Static |
                                 System.Reflection.BindingFlags.DeclaredOnly))
                    {
                        if (!f.IsLiteral) continue;
                        // ⚠️ 只打「容量真值」的两种名形 —— **不是**任何含 "Cap" 的常量。
                        //    实测误伤:`Sim.Foraging.ForagingConstants.CapTableLength = 61`
                        //    是 17 的档位数上界(合法常量),与 20 的容量无关。
                        //    判据须对合法输入类不误判,否则门恒红 = 等于没有门。
                        if (!f.Name.Contains("CarryCap") && !f.Name.Contains("Capacity")) continue;
                        if (f.FieldType != typeof(int)) continue;
                        int v = (int)f.GetRawConstantValue();
                        if (v > 0)
                            violations.Add($"{t.FullName}.{f.Name} = {v}");
                    }
                }
            }
            Assert.That(violations, Is.Empty,
                "生产代码里出现默认容量常量(AC-20-13 禁 null 兜底 / 默认容量;AC-20-24 零第二容量常量):\n  "
                + string.Join("\n  ", violations));
        }

        [Test]
        public void test_ac2013_realDataSource_bindsToPositiveCap()
        {
            // 真实作者态数据件须可绑定且 cap > 0(否则构建门无输入)。
            InventoryConstantsBinder.BindResult r =
                InventoryConstantsBinder.BindFromRepo(RepoRoot());
            Assert.That(r.CarryCap, Is.GreaterThan(0),
                "assets/data/inventory_constants.json 的 carry_cap 须为正(AC-20-21 判据 A 的输入)");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-20-18 · 满档等价(`load > cap − W_MIN`,禁简化成 `== cap`)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac2018_wMinGreaterThanOne_boundaryIsCapMinusWMin()
        {
            const int cap = 20, wMin = 2;
            var capacity = new CarryCapacity(cap);

            // 扫描线四点:cap−W_MIN−1 / cap−W_MIN / cap−W_MIN+1 / cap
            Assert.That(CarryLoadSolver.IsFullForAnyAddition(cap - wMin - 1, capacity, wMin), Is.False,
                $"{cap - wMin - 1} ⇒ 未满(还剩 {wMin} 余量,恰能再放一件)");
            Assert.That(CarryLoadSolver.IsFullForAnyAddition(cap - wMin, capacity, wMin), Is.False,
                $"{cap - wMin} ⇒ **仍未满** —— 这正是 `== cap` 简化会谎报的那一段");
            Assert.That(CarryLoadSolver.IsFullForAnyAddition(cap - wMin + 1, capacity, wMin), Is.True,
                $"{cap - wMin + 1} ⇒ 已满(余量 {wMin - 1} < W_MIN)");
            Assert.That(CarryLoadSolver.IsFullForAnyAddition(cap, capacity, wMin), Is.True,
                $"{cap} ⇒ 已满");
        }

        [Test]
        public void test_ac2018_fullPredicate_agreesWithCanCarryForEveryItem()
        {
            // 「一律放不进」⇔ 对**表内每一件** CanCarry 都为 false。
            // ⚠️ 这条是 AC-20-18 的**语义定义**:谓词与机制必须一一对应。
            const int cap = 20;
            int[] weights = { 2, 3, 5, 7 };    // W_MIN = 2
            int wMin = weights.Min();
            var capacity = new CarryCapacity(cap);

            for (long load = 0; load <= cap + 1; load++)
            {
                bool anyFits = weights.Any(w => CarryLoadSolver.CanCarry(load, w, 1, capacity));
                bool predicate = CarryLoadSolver.IsFullForAnyAddition(load, capacity, wMin);

                Assert.That(predicate, Is.EqualTo(!anyFits),
                    $"load = {load}:IsFullForAnyAddition = {predicate} 但「有件能放」= {anyFits} " +
                    "—— 谓词与机制分叉(AC-20-18 的一一对应)");
            }
        }

        [Test]
        public void test_ac2018_wMinEqualsOne_degradesToCapMinusOne()
        {
            // W_MIN = 1 时两式同:满 ⇔ load > cap − 1 ⇔ load ≥ cap ⇔ load == cap(域内)。
            const int cap = 10;
            var capacity = new CarryCapacity(cap);

            Assert.That(CarryLoadSolver.IsFullForAnyAddition(cap - 1, capacity, wMin: 1), Is.False);
            Assert.That(CarryLoadSolver.IsFullForAnyAddition(cap, capacity, wMin: 1), Is.True);
        }

        [Test]
        public void test_ac2018_minWeight_computedFromTableNotHardcoded()
        {
            Assert.That(CarryLoadSolver.MinWeight(new[] { 3, 1, 5 }), Is.EqualTo(1));
            Assert.That(CarryLoadSolver.MinWeight(new[] { 4, 2, 9 }), Is.EqualTo(2));
        }

        [Test]
        public void test_ac2018_minWeight_emptyTable_throws()
        {
            Assert.Throws<InvalidOperationException>(
                () => CarryLoadSolver.MinWeight(Array.Empty<int>()),
                "空表 ⇒ W_MIN 无定义,不得返回哨兵值冒充");
        }

        [Test]
        public void test_ac2018_minWeight_illegalWeight_throws()
        {
            Assert.Throws<InvalidOperationException>(
                () => CarryLoadSolver.MinWeight(new[] { 2, 0 }),
                "weight < 1 是域错误(AC-21a-15),不得静默跳过");
        }

        [Test]
        public void test_ac2018_minWeightNull_throws()
        {
            Assert.Throws<ArgumentNullException>(() => CarryLoadSolver.MinWeight(null));
        }

        [Test]
        public void test_ac2018_invalidWMin_throwsRatherThanGuessing()
        {
            var capacity = new CarryCapacity(10);
            Assert.Throws<InvalidOperationException>(
                () => CarryLoadSolver.IsFullForAnyAddition(5L, capacity, wMin: 0),
                "wMin = 0 是域错误(weight ∈ int>0 ⇒ min ≥ 1)");
        }

        [Test]
        public void test_ac2018_ratioDto_carriesExactlyLoadAndCap()
        {
            var capacity = new CarryCapacity(20);
            CarryRatioDto dto = CarryLoadSolver.Ratio(7L, capacity);

            Assert.That(dto.CarryLoad, Is.EqualTo(7L));
            Assert.That(dto.Cap, Is.EqualTo(20));
        }

        [Test]
        public void test_ac2018_ratioNegativeLoad_throws()
        {
            var capacity = new CarryCapacity(20);
            Assert.Throws<InvalidOperationException>(() => CarryLoadSolver.Ratio(-1L, capacity));
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-20-21 · 存在性门(自指谓词)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac2021_realData_gateIsGreen()
        {
            List<string> errs = CarryCapExistenceGate.RunAll(RepoRoot());
            Assert.That(errs, Is.Empty,
                "AC-20-21 门在真实数据下须绿:\n  " + string.Join("\n  ", errs));
        }

        [Test]
        public void test_ac2021_emptyItemTable_isRed()
        {
            List<string> errs = CarryCapExistenceGate.Evaluate(60, Array.Empty<int>());
            Assert.That(errs, Is.Not.Empty, "空物品表 ⇒ 装满不可达 ⇒ 门须红");
            Assert.That(errs.Any(e => e.Contains("AC-20-21/B")), Is.True,
                "须点名判据 B(物品表为空):\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_ac2021_allIllegalWeights_isRed()
        {
            List<string> errs = CarryCapExistenceGate.Evaluate(60, new[] { 0, -1, 0 });
            Assert.That(errs, Is.Not.Empty, "全非法重量 ⇒ 加不出重量 ⇒ 门须红");
            Assert.That(errs.Any(e => e.Contains("AC-20-21/C")), Is.True,
                "须点名判据 C(无 weight ≥ 1 的条目 / 域错误):\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_ac2021_nonPositiveCap_isRed()
        {
            List<string> errs = CarryCapExistenceGate.Evaluate(0, new[] { 1, 2 });
            Assert.That(errs.Any(e => e.Contains("AC-20-21/A")), Is.True,
                "cap ≤ 0 ⇒ 门须红并点名判据 A:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_ac2021_singleUnitWeightTable_reachesFullState()
        {
            // cap 10 / 表内只有 w=1 ⇒ 领 10 件后 load == cap,第 11 件装不进 ⇒ 可达。
            List<string> errs = CarryCapExistenceGate.Evaluate(10, new[] { 1 });
            Assert.That(errs, Is.Empty,
                "w=1 的表在 cap=10 下必然装满 —— 门须绿:\n  " + string.Join("\n  ", errs));
        }

        [Test]
        public void test_ac2021_heaviestSingleItemExceedingCap_isImmediatelyFull()
        {
            // cap 3,表里只有 w=5 ⇒ 第一件就装不下 ⇒ 开档状态本身就是满。
            List<string> errs = CarryCapExistenceGate.Evaluate(3, new[] { 5 });
            Assert.That(errs, Is.Empty, "首件即超 cap ⇒ 存在性成立:\n  " + string.Join("\n  ", errs));
        }

        [Test]
        public void test_ac2021_adapter_failsClosedOnMalformedInput()
        {
            // 取数适配器 fail-closed:取不出 ⇒ 红,不静默跳过。
            Assert.That(CarryCapExistenceGate.TryReadCarryCap("{ }", out _, out string e1), Is.False);
            Assert.That(e1, Is.Not.Null.And.Not.Empty);

            Assert.That(CarryCapExistenceGate.TryReadCarryCap(
                "{ \"carry_cap\": \"60\" }", out _, out _), Is.False,
                "字符串形态取不出 ⇒ fail-closed");

            Assert.That(CarryCapExistenceGate.TryReadCarryCap(
                "{ \"carry_cap\": 60.0 }", out _, out _), Is.False,
                "浮点形态取不出 ⇒ fail-closed");

            Assert.That(CarryCapExistenceGate.TryReadCarryCap(
                "{ \"carry_cap\": 60 }", out int cap, out _), Is.True);
            Assert.That(cap, Is.EqualTo(60));
        }

        [Test]
        public void test_ac2021_itemWeightAdapter_countsMustMatchOrdinals()
        {
            // 完备性哨兵:weight 条数须等于 ordinal 条数(某条 weight 非整数 ⇒ 少一条 ⇒ 拒)。
            string good = "{ \"items\": [ { \"ordinal\": 0, \"weight\": 2 }, { \"ordinal\": 1, \"weight\": 1 } ] }";
            Assert.That(CarryCapExistenceGate.TryReadItemWeights(good, out List<int> w, out _), Is.True);
            Assert.That(w, Is.EqualTo(new[] { 2, 1 }));

            string bad = "{ \"items\": [ { \"ordinal\": 0, \"weight\": 2 }, { \"ordinal\": 1, \"weight\": \"1\" } ] }";
            Assert.That(CarryCapExistenceGate.TryReadItemWeights(bad, out _, out string err), Is.False,
                "weight 非整数形态 ⇒ 条数不符 ⇒ fail-closed");
            Assert.That(err, Does.Contain("≠"));
        }

        [Test]
        public void test_ac2021_gateIsWiredIntoAssemblyGatesRunAll()
        {
            // 「门在链上」的**结构性**判据:源码里真有该调用行。
            // ⚠️ 这不证明门在构建期执行 —— 执行须走探针轮(见 story §Completion Notes)。
            string src = File.ReadAllText(
                Path.Combine(RepoRoot(), "unity/Assets/Editor.Tools.Gates/AssemblyGates.cs"));
            Assert.That(src, Does.Contain("CarryCapExistenceGate.RunAll"),
                "门未接进 AssemblyGates.RunAll ⇒ 零强制点(BLOCKING 判据不得挂 TestDriven)");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-20-24 · 单源与零派生字段
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac2024_ratioDto_hasExactlyTwoFields()
        {
            System.Reflection.FieldInfo[] fields = typeof(CarryRatioDto).GetFields(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.DeclaredOnly);

            Assert.That(fields.Length, Is.EqualTo(2),
                "`CarryRatioDto` 须恰两字段(carry_load, cap);实得:"
                + string.Join(", ", fields.Select(f => f.Name)));

            var names = new HashSet<string>(fields.Select(f => f.Name), StringComparer.Ordinal);
            Assert.That(names.SetEquals(new[] { "CarryLoad", "Cap" }), Is.True,
                "字段名须恰为 CarryLoad / Cap;实得:" + string.Join(", ", names));
        }

        [Test]
        public void test_ac2024_ratioDto_hasNoDerivedOrDecimalMember()
        {
            // 禁 `RemainingSlack` / 百分比 / 视觉容量 类成员,也禁浮点类型。
            var banned = new[] { "slack", "remain", "percent", "ratio", "visual", "capacity" };
            var violations = new List<string>();

            foreach (System.Reflection.MemberInfo m in typeof(CarryRatioDto).GetMembers(
                         System.Reflection.BindingFlags.Public |
                         System.Reflection.BindingFlags.Instance |
                         System.Reflection.BindingFlags.DeclaredOnly))
            {
                // 排除构造器 / ToString 等由 object 继承的成员(DeclaredOnly 已滤掉大部分)。
                if (m is System.Reflection.MethodInfo mi && mi.IsSpecialName) continue;

                string lower = m.Name.ToLowerInvariant();
                foreach (string b in banned)
                    if (lower.Contains(b))
                        violations.Add($"成员 `{m.Name}` 命中禁用词 `{b}`");

                if (m is System.Reflection.FieldInfo f &&
                    (f.FieldType == typeof(float) || f.FieldType == typeof(double) ||
                     f.FieldType == typeof(decimal)))
                    violations.Add($"字段 `{f.Name}` 类型为 {f.FieldType.Name}(禁浮点)");
            }

            Assert.That(violations, Is.Empty,
                "AC-20-24:20 侧零派生字段(slack 由 42 自行 `cap − carry_load` 相减):\n  "
                + string.Join("\n  ", violations));
        }

        [Test]
        public void test_ac2024_ratioDto_hasNoRemainingSlackField()
        {
            // 显式点名 —— 这是 GDD 唯一点名禁止的字段(BL-27)。
            Assert.That(typeof(CarryRatioDto).GetField("RemainingSlack"), Is.Null,
                "`RemainingSlack` 是 AC-20-24 明令禁止的字段(第二容量真值)");
            Assert.That(typeof(CarryRatioDto).GetProperty("RemainingSlack"), Is.Null);
        }

        [Test]
        public void test_ac2024_capacityTruth_definedInDataFileNotInCode()
        {
            // `CARRY_CAP` 的真源 = 数据件;代码里零字面量常量(上一条已反射扫常量,此处扫源面)。
            string src = File.ReadAllText(
                Path.Combine(RepoRoot(), "unity/Assets/Sim/ItemDatabase/CarryLoad.cs"));
            string code = StripComments(src);

            Assert.That(System.Text.RegularExpressions.Regex.IsMatch(
                    code, @"const\s+int\s+\w*Cap\w*\s*="), Is.False,
                "`CarryLoad.cs` 里出现 `const int *Cap* = …` —— 那是第二容量真值(AC-20-24)");
        }

        [Test]
        public void test_ac2024_dataFile_isTheSingleCapacitySource()
        {
            // 数据件里确实有 carry_cap,且只有一份 `carry_cap` 键落在 assets/data 下。
            string dataDir = Path.Combine(RepoRoot(), "assets", "data");
            string[] hits = Directory.GetFiles(dataDir, "*.json", SearchOption.TopDirectoryOnly)
                .Where(f => File.ReadAllText(f).Contains("\"carry_cap\""))
                .Select(Path.GetFileName)
                .ToArray();

            Assert.That(hits, Is.EqualTo(new[] { "inventory_constants.json" }),
                "`carry_cap` 须只出现在 inventory_constants.json;实得:["
                + string.Join(", ", hits) + "]");
        }

        // ══════════════════════════════════════════════════════════════════
        // 相邻面(容器子件表两路 —— 2026-10-26 用户裁定「双路都有夹具」)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_containerPath_singleLayer_countsEachProjectionRow()
        {
            // ① 单层求和:容器 + 子件**各有自己的投影行** ⇒ 天然就是「空箱自重 + 子件逐件」。
            //    容器 key 2 (w=5) + 子件 key 1 (w=1)×3 = 5 + 3 = 8
            InventoryProjection p = ProjectionFor(owner: 7, (2, 1), (1, 3));
            CarryLoadBreakdown b = CarryLoadSolver.SumDetailed(p, WeightOf, 7, null);

            Assert.That(b.Total, Is.EqualTo(8L), "5 + 1×3 = 8");
            Assert.That(b.ChildWeightAdded, Is.EqualTo(0L), "单层路径不并入子件表");
            Assert.That(b.DoubleCountedChildWeight, Is.EqualTo(0L));
            Assert.That(b.OrphanChildCount, Is.EqualTo(0));
            Assert.That(b.IsClean, Is.True);
        }

        [Test]
        public void test_containerPath_explicitChildTable_addsUnownedChildren()
        {
            // ② 显式子件表:子件**未归属**(在地上)⇒ 并入本玩家载重是正确的。
            //    容器行 id=1 (key 2, w=5);子件行 id=2 (key 1, w=1)×3,未 claim ⇒ NoOwner
            var events = new List<SimEvent>
            {
                Spawn(1, 2, 1, 1, seq: 0), Harvest(1, 1, 1, 1, 1, seq: 1), Claim(1, 7, 2),
                Spawn(2, 1, 3, 3, seq: 0), Harvest(2, 2, 3, 1, 3, seq: 1),   // 未 claim
            };
            InventoryProjection p = InventoryFold.Fold(events, _decoder).Projection;

            var children = new Dictionary<long, IReadOnlyList<long>>
            {
                [1L] = new long[] { 2L },
            };
            CarryLoadBreakdown b = CarryLoadSolver.SumDetailed(p, WeightOf, 7, children);

            Assert.That(b.Total, Is.EqualTo(8L), "空箱 5 + 子件 1×3 = 8");
            Assert.That(b.ChildWeightAdded, Is.EqualTo(3L), "子件表并入 3");
            Assert.That(b.DoubleCountedChildWeight, Is.EqualTo(0L), "子件未归属 ⇒ 不双计");
            Assert.That(b.IsClean, Is.True);
        }

        [Test]
        public void test_containerPath_doubleCountIsReportedNotSilentlySummed()
        {
            // 容器 id=1 + 子件 id=2**都已被本玩家 claim** ⇒ 子件已在单层求和里计过;
            // 再经子件表并入 = 双计。件须**显式报出**,不得静默相加。
            var events = new List<SimEvent>
            {
                Spawn(1, 2, 1, 1, seq: 0), Harvest(1, 1, 1, 1, 1, seq: 1), Claim(1, 7, 2),
                Spawn(2, 1, 3, 3, seq: 0), Harvest(2, 2, 3, 1, 3, seq: 1), Claim(2, 7, 4),
            };
            InventoryProjection p = InventoryFold.Fold(events, _decoder).Projection;

            var children = new Dictionary<long, IReadOnlyList<long>> { [1L] = new long[] { 2L } };
            CarryLoadBreakdown b = CarryLoadSolver.SumDetailed(p, WeightOf, 7, children);

            Assert.That(b.Total, Is.EqualTo(8L), "总量仍是 5 + 3 = 8(不把子件加第二遍)");
            Assert.That(b.DoubleCountedChildWeight, Is.EqualTo(3L),
                "子件已被本玩家持有 ⇒ 其 3 点重量须记入双计");
            Assert.That(b.IsClean, Is.False, "IsClean 须为 false —— 让误用可见");
        }

        [Test]
        public void test_containerPath_doubleCountAcrossOwners_alsoReported()
        {
            // 子件归**别人**:它已计入那个人的载重,并入本玩家同样构成全局双计。
            var events = new List<SimEvent>
            {
                Spawn(1, 2, 1, 1, seq: 0), Harvest(1, 1, 1, 1, 1, seq: 1), Claim(1, 7, 2),
                Spawn(2, 1, 3, 3, seq: 0), Harvest(2, 2, 3, 1, 3, seq: 1), Claim(2, 8, 4),
            };
            InventoryProjection p = InventoryFold.Fold(events, _decoder).Projection;

            var children = new Dictionary<long, IReadOnlyList<long>> { [1L] = new long[] { 2L } };
            CarryLoadBreakdown b = CarryLoadSolver.SumDetailed(p, WeightOf, 7, children);

            Assert.That(b.Total, Is.EqualTo(5L), "只算玩家 7 自己的容器 5");
            Assert.That(b.DoubleCountedChildWeight, Is.EqualTo(3L),
                "子件归玩家 8 ⇒ 并入玩家 7 同样双计");
        }

        [Test]
        public void test_containerPath_orphanChild_isCountedNotDropped()
        {
            // 子件 id 在投影里查不到 ⇒ 重量无从得知 ⇒ 记 orphan,不得静默忽略。
            InventoryProjection p = ProjectionFor(owner: 7, (2, 1));   // 只有容器 id=1
            var children = new Dictionary<long, IReadOnlyList<long>> { [1L] = new long[] { 999L } };
            CarryLoadBreakdown b = CarryLoadSolver.SumDetailed(p, WeightOf, 7, children);

            Assert.That(b.Total, Is.EqualTo(5L), "缺载体不得当 0 加(总量保持不变)");
            Assert.That(b.OrphanChildCount, Is.EqualTo(1), "孤儿子件须计数");
            Assert.That(b.IsClean, Is.False);
        }

        [Test]
        public void test_containerPath_nullChildTable_isSingleLayerPath()
        {
            InventoryProjection p = ProjectionFor(owner: 7, (0, 2));
            CarryLoadBreakdown b = CarryLoadSolver.SumDetailed(p, WeightOf, 7, null);
            Assert.That(b.Total, Is.EqualTo(CarryLoadSolver.Sum(p, WeightOf, 7)),
                "`Sum` 须等价于 `SumDetailed(…, null).Total`");
        }

        // ══════════════════════════════════════════════════════════════════
        // 辅助
        // ══════════════════════════════════════════════════════════════════

        private static string Json(int cap)
            => $"{{ \"schema_version\": 1, \"_note\": \"夹具\", \"carry_cap\": {cap} }}";

        /// <summary>工程根(<c>unity/</c>)= <c>Application.dataPath</c> 的上级。</summary>
        private static string RepoRoot()
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        /// <summary>剥除 <c>//</c> 与 <c>/* */</c> 注释(字符串字面量内容保留)。</summary>
        private static string StripComments(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            bool inLine = false, inBlock = false, inStr = false, inChar = false, esc = false;

            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';

                if (inLine) { if (c == '\n') { inLine = false; sb.Append(c); } continue; }
                if (inBlock) { if (c == '*' && n == '/') { inBlock = false; i++; } continue; }
                if (inStr)
                {
                    sb.Append(c);
                    if (esc) { esc = false; continue; }
                    if (c == '\\') { esc = true; continue; }
                    if (c == '"') inStr = false;
                    continue;
                }
                if (inChar)
                {
                    sb.Append(c);
                    if (esc) { esc = false; continue; }
                    if (c == '\\') { esc = true; continue; }
                    if (c == '\'') inChar = false;
                    continue;
                }

                if (c == '/' && n == '/') { inLine = true; i++; continue; }
                if (c == '/' && n == '*') { inBlock = true; i++; continue; }
                if (c == '"') { inStr = true; sb.Append(c); continue; }
                if (c == '\'') { inChar = true; sb.Append(c); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>字符偏移 → 行号(1 起;诊断用)。</summary>
        private static int LineOf(string text, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < text.Length; i++)
                if (text[i] == '\n') line++;
            return line;
        }

        /// <summary>是否浮点相关 IL 指令(与 `InventoryDtoFloatGate` 的判据面同源)。</summary>
        private static bool IsFloatOpcode(OpCode op)
            => op.Code == Code.Conv_R4 || op.Code == Code.Conv_R8 || op.Code == Code.Conv_R_Un
            || op.Code == Code.Ldc_R4 || op.Code == Code.Ldc_R8
            || op.Code == Code.Ldind_R4 || op.Code == Code.Ldind_R8
            || op.Code == Code.Stind_R4 || op.Code == Code.Stind_R8;
    }
}
