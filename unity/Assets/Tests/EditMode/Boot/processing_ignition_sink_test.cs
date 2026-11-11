// 炮制屏的意图出口(`TD-skeuo-06` · 2026-11-04)。
//
// 权威:
//   · `design/gdd/processing.md` 规则七(点火五步)/ 规则八(发起即落流)
//   · ADR-013 §十-E(`ModalId.Processing` 第九员)· §9 C3(42 只渲染)
//
// 本文件断的是**债册说的那个形态本身**,不是「类存在」:
//   `IProcessingCommandSink` 的生产实现此前**为零** ⇒ `_sink?.Ignite(...)` 恒空转。
//   ⇒ 判据必须落在**流的副作用**上(恰一条 `Craft` / 被拒时**零条**),
//   而不是「方法被调用了」。只断返回值会是又一个自造断言(假绿形态 F1)。
//
// ⚠️ **不**断言「屏上的行被点了一下」—— 那是 42 侧 `processing18_screen_test` 的面;
//    本文件从**接口**入口驱动,判的是装配层实现。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Boot
{
    [TestFixture]
    public sealed class ProcessingIgnitionSinkTest
    {
        private const long Tick = 500L;
        private const int Actor = 7;
        private const int RecipeOrdinal = 3;
        private const int DurationTicks = 20;
        private const int SkillLevel = 30;

        private GameObject _go;
        private SkeuoRuntimeDriver _driver;
        private CountingSink _sink;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private CraftEventWriter _writer;
        private InMemoryBlobPool _pool;
        private CountingIdAuthority _ids;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("processing-ignition-sink-test");
            _driver = _go.AddComponent<SkeuoRuntimeDriver>();
            var awake = typeof(SkeuoRuntimeDriver).GetMethod("Awake",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            Assert.That(awake, Is.Not.Null, "夹具前提:驱动的 Awake 存在(形态变了?)");
            awake.Invoke(_driver, null);

            _sink = new CountingSink();
            _ids = new CountingIdAuthority();
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _writer = new CraftEventWriter(_sink, _encoder, _ids);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        // ── 夹具 ────────────────────────────────────────────────────────────

        private static Recipe MakeRecipe() => new Recipe
        {
            Ordinal = RecipeOrdinal,
            RecipeId = "fixture_recipe_sink",
            Owner = RecipeOwner.Process,
            Inputs = new[]
            {
                new RecipeEntry(new ItemKey("fixture_in_a", ProcessingState.Raw), 2),
                new RecipeEntry(new ItemKey("fixture_in_b", ProcessingState.Raw), 1),
            },
            Outputs = new[] { new RecipeEntry(new ItemKey("fixture_out", ProcessingState.Extracted), 1) },
            DurationTicks = DurationTicks,
            SkillGate = 5,
            MinQuality = 2,
        };

        private static RecipeSettlementConstants MakeConstants()
            => new RecipeSettlementConstants(
                qtyMultMin: FixParse.Parse("1/2"),
                qtyMultMax: FixParse.Parse("5/2"),
                skillModCap: FixParse.Parse("3/10"),
                qualModCap: FixParse.Parse("1/10"),
                equipModCap: FixParse.Parse("1/5"),
                envModMin: FixParse.Parse("-1/2"),
                envModMax: FixParse.Parse("1/2"),
                retainMin: FixParse.Parse("1/2"),
                retainMax: FixParse.Parse("1"),
                effMin: FixParse.Parse("1/2"),
                effMax: FixParse.Parse("1"),
                maxQuality: 5,
                skillCap: 60);

        private DriverProcessingCommandSink NewSink(
            ProcessingMaterials materials,
            System.Func<long, WorldPos?> toolCellOf,
            System.Func<WorldPos, ProcessingEnvMods> envModsOf = null)
            => new DriverProcessingCommandSink(
                _driver,
                () => new RecipeDataSet
                {
                    Recipes = new[] { MakeRecipe() },
                    Settlement = MakeConstants(),
                },
                () => materials,
                (ordinal, line) => 1,
                () => 0L,
                toolCellOf,
                new StubSkillLevels(SkillLevel),
                _sink.Events,
                _decoder,
                new CarryCapacity(1000),
                () => Tick,
                Actor,
                _writer,
                envModsOf);

        private static readonly ProcessingMaterials Materials
            = new ProcessingMaterials(new long[] { 1L, 2L }, new[] { 3, 3 });

        // ── ① 正路:恰一条 Craft 落流 ────────────────────────────────────────

        [Test]
        public void test_ignite_admitted_emitsExactlyOneCraftEvent()
        {
            DriverProcessingCommandSink sink = NewSink(Materials, _ => new WorldPos(2, 0, 5));
            sink.SetTool(42L);

            Assert.That(sink.Ignite(RecipeOrdinal), Is.True,
                "准入 / 单炉 / 容量全过 ⇒ 点火必须成功(否则屏上按配方仍是空转)");

            Assert.That(_sink.Count, Is.EqualTo(1), "一次点火 = **恰一条**事件(零条 = 空转,两条 = 重复落流)");
            Assert.That(_sink.Events[0].Kind, Is.EqualTo(EventKind.Craft), "落流的必须是 `Craft`");
            Assert.That(_ids.ItemMints, Is.GreaterThan(0), "产出实例 id 须由发号器铸造(不是手搓)");
        }

        // ── ② 无器具格:拒发,且**零事件** ──────────────────────────────────

        [Test]
        public void test_ignite_withoutResolvableToolCell_refuses_andEmitsNothing()
        {
            DriverProcessingCommandSink sink = NewSink(Materials, _ => null);
            sink.SetTool(42L);

            Assert.That(sink.Ignite(RecipeOrdinal), Is.False,
                "器具格解析不出 ⇒ 必须拒发(不拿玩家格顶替 —— 起货溢出落点是它)");
            Assert.That(_sink.Count, Is.Zero, "拒发 ⇒ 零事件(不是「先落流再报错」)");
        }

        // ── ③ 未选料:拒发,且零事件 ───────────────────────────────────────

        [Test]
        public void test_ignite_withNoMaterials_refuses_andEmitsNothing()
        {
            DriverProcessingCommandSink sink = NewSink(
                new ProcessingMaterials(new long[0], new int[0]), _ => new WorldPos(2, 0, 5));
            sink.SetTool(42L);

            Assert.That(sink.Ignite(RecipeOrdinal), Is.False, "没投料 ⇒ 不可发起");
            Assert.That(_sink.Count, Is.Zero);
        }

        // ── ④ 两集合不等长 = 编程错误(不是业务拒绝)──────────────────────────

        [Test]
        public void test_ignite_withMisalignedMaterialSets_refuses_andEmitsNothing()
        {
            // 对齐是 `ContainerSelectionStore.SnapshotSelection` 的契约(一趟遍历产出两份);
            // 走到装配层还不等长 ⇒ 上游有第二实现。此时**拒发**比「按较短的截断」安全:
            // 截断会让品级集静默错位(F1 要求逐位对位)。
            DriverProcessingCommandSink sink = NewSink(
                new ProcessingMaterials(new long[] { 1L, 2L }, new[] { 3 }), _ => new WorldPos(2, 0, 5));
            sink.SetTool(42L);

            Assert.That(sink.Ignite(RecipeOrdinal), Is.False);
            Assert.That(_sink.Count, Is.Zero);
        }

        // ── ⑤ Close 真的合屏(不是空操作)────────────────────────────────────

        [Test]
        public void test_close_actuallyClosesTheModal()
        {
            Assert.That(_driver.OpenModal(ModalId.Processing), Is.True, "前提:炮制屏须开得出来");
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.Processing));

            DriverProcessingCommandSink sink = NewSink(Materials, _ => new WorldPos(2, 0, 5));
            sink.Close();

            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.None),
                "Close 必须真的合屏 —— 此前 `_sink?.Close()` 恒空转(TD-skeuo-06)");
        }

        // ── ⑥ 三分量接真值:`TD-processing-09` 结案判据 ⑤ ─────────────────────

        /// <summary>非中性输入**改变产出** —— 债册 `TD-processing-09` 的解除判据 ⑤。</summary>
        /// <remarks><b>判别力</b>:把 sink 的三分量改回硬编码 `Fix.Zero`(旧态)⇒ 本测红;
        /// 把 `envModsOf` 的返回值忽略 ⇒ 本测红。
        /// ⚠️ 本条**只**钉「值有影响力」这一件事 —— 它**不**证明三分量在生产装配里取到了
        /// 真值(那条由 `BootRoot.BuildProcessingEnvMods` 的装配面与 24 / 6 的夹具各自承担)。</remarks>
        [Test]
        public void test_ignite_nonNeutralMods_changeCraftOutputQuantity()
        {
            int neutralQty = IgniteAndReadFirstOutputQty(envModsOf: null);

            int boostedQty = IgniteAndReadFirstOutputQty(envModsOf:
                _ => new ProcessingEnvMods(
                    equipMod: FixParse.Parse("1/5"),      // 24 设备修正上限
                    envModClimate: FixParse.Parse("1/5"), // 5 环境分量
                    envModClinic: FixParse.Parse("1/5"))); // 24 医馆分量

            Assert.That(neutralQty, Is.GreaterThanOrEqualTo(1), "前提:中性也要产出一件以上");
            Assert.That(boostedQty, Is.GreaterThan(neutralQty),
                "三分量若接进 F1 就**必须改变产出**;仍是中性值 ⇒ 缝没接上(旧态)");
        }

        /// <summary>缝收到的格 = **炮制发生格**(F-5.4 调用点表),不是玩家格。</summary>
        [Test]
        public void test_ignite_envModsSeam_receivesToolCell()
        {
            WorldPos toolCell = new WorldPos(2, 0, 5);
            WorldPos? seen = null;
            DriverProcessingCommandSink sink = NewSink(Materials, _ => toolCell,
                cell => { seen = cell; return ProcessingEnvMods.Neutral; });
            sink.SetTool(42L);

            Assert.That(sink.Ignite(RecipeOrdinal), Is.True);

            Assert.That(seen.HasValue, Is.True, "三分量取值缝必须被调用(否则生产上永远拿不到真值)");
            Assert.That(seen.Value.X, Is.EqualTo(toolCell.X), "cell 须是器具格 —— F-5.4 的炮制发生格");
            Assert.That(seen.Value.Z, Is.EqualTo(toolCell.Z));
        }

        /// <summary>跑一次点火,返回落流 `Craft` 的首条产出量。</summary>
        private int IgniteAndReadFirstOutputQty(System.Func<WorldPos, ProcessingEnvMods> envModsOf)
        {
            // 每个用例独立一套流 / 发号器 —— 免得前一条的 Craft 混进后一条的断言。
            _sink = new CountingSink();
            _ids = new CountingIdAuthority();
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _writer = new CraftEventWriter(_sink, _encoder, _ids);

            DriverProcessingCommandSink sink = NewSink(Materials, _ => new WorldPos(2, 0, 5), envModsOf);
            sink.SetTool(42L);

            Assert.That(sink.Ignite(RecipeOrdinal), Is.True);
            Assert.That(_sink.Count, Is.EqualTo(1));

            CraftPayload payload = _decoder.Decode<CraftPayload>(EventKind.Craft, _sink.Events[0].Payload);
            Assert.That(payload.OutputQty, Is.Not.Null.And.Not.Empty);
            return payload.OutputQty[0];
        }

        // ── 夹具替身 ────────────────────────────────────────────────────────

        private sealed class StubSkillLevels : ISkillLevelQuery
        {
            private readonly int _level;
            public StubSkillLevels(int level) => _level = level;
            public int QueryLevel(int actorId, int skillId) => _level;
        }

        private sealed class CountingSink : IEventSink
        {
            private readonly List<SimEvent> _events = new List<SimEvent>();
            public int Count => _events.Count;
            public IReadOnlyList<SimEvent> Events => _events;

            public void Append(in SimEvent e)
            {
                long seq = 0;
                for (int i = _events.Count - 1; i >= 0; i--)
                {
                    if (_events[i].Tick == e.Tick && _events[i].Patient.Value == e.Patient.Value)
                    { seq = _events[i].Seq + 1; break; }
                }
                _events.Add(new SimEvent(e.Tick, e.Patient, e.Seq < 0 ? seq : e.Seq, e.Kind, e.Payload));
            }
        }

        private sealed class CountingIdAuthority : IIdAuthority
        {
            private int _patients;
            private int _items;
            public int ItemMints => _items;

            public PatientId NextPatientId() => new PatientId(_patients++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_items++);
        }
    }
}
