// W / TREND_EPS 从烘焙产物流到 DiseaseVitalsService 的**端到端**判据
// (2026-10-22 B1 收口轮的诚实边界补件)。
//
// ── 为什么需要本文件 ──────────────────────────────────────────────────────────
// B1 接真后,「W/eps 值真的流到 vitals」只有**间接**证据:
//   · `MultiTableProvider` 夹具缺 `DiseaseRegistryDataSet` ⇒ 装配当场抛
//     ⇒ 那只证「装配向 provider **要了**这张表」;
//   · `DiseaseVitalsService` 构造从 4 参变 6 参 ⇒ 那只是签名。
// 两者都**不证值本身**。本文件补的正是这一跳:让同一个病种在
// 「表带 W/eps」与「表不带 W/eps」两种装配下判出**不同的终态**。
//
// ── 判据形态(为什么必须是「两路对照」) ──────────────────────────────────────
// 单看「provider 路径能判出 Recovered」是**不充分**的 —— 痊愈支成立也可能是
// 别的条件凑巧满足。故每条判据都成对:
//
//   | 路径 | 装配 | W / eps 来源 | 期望 |
//   |---|---|---|---|
//   | 甲 | `Assemble(dataProvider: p)` | 烘焙产物 | 能判出 Recovered |
//   | 乙 | `Assemble(registry: rows)` | **无**(caller 传表刻意不混真值) | 永不 Recovered |
//
// 乙路是甲的**对照**,不是「另一种合法配置」—— 它存在的唯一意义是证明
// 「甲能判出来自产物里的 W/eps,不是来自别处」。缺了乙,甲单独绿说明不了任何事。
//
// ── 覆盖不到(如实登记,禁借绿) ──────────────────────────────────────────────
// ① 本文件跑的是 **EditMode + 盘上/夹具产物**,不经 Addressables catalog
//    ⇒ 它证的是「codec → DataSet → CompositionRoot → DiseaseVitalsService」,
//    **不证** `catalog.bin` 新鲜度(`m_BuildAddressablesWithPlayerBuild = 0`,
//    player 腿须重跑构建菜单;该缺口另行登记)。
// ② `W` / `TREND_EPS` 的**数值本身**仍归数值轮(**数值轮未裁**,非 `OQ-9-1`)—— 本文件用的是
//    判据用的合成值(见下方常量),不是设计值,不得被读成「数值已定」。
//    ⚠️ 2026-10-23 订正:原文把「`W` 数值未裁」与「`OQ-9-1` 未裁」混为一谈。`OQ-9-1`
//    (潜伏期起算点)**已于 2026-10-23 裁为乙案**(起算点 = `onset + incubation`,
//    落 `ProgressionEvaluator.ComputeLowPositionHeldTicks`,判据
//    `low_position_held_incubation_boundary_test.cs`);仍未裁的**只有 `W` 的数值**。
// ③ 本文件**不**证「多病聚合 / 照护通道 / 处置通道」的接线 —— 那是
//    `f4_wiring_test` / `care_lever_and_aggregator_test` 的面。
//
// 权威来源:
//   ADR-014 §二/§五(两阶段烘焙 · 运行期读烘焙产物)· §四(Fix 字段在 JSON 里写**字符串**)
//   ADR-006 §四(`Fix` 在 `.cooked` 里 = raw long,零 `FixParse`)
//   GDD disease-simulation.md:1113-1120(痊愈支六条件)· :1743-1744(W / TREND_EPS)
//   `F4OutcomeEvaluator.RecoveryWindowInput.IsConfigured`(`W == 0` ⇒ 两支不求值)
//   `RegistrySchemaValidator.ValidateTrendWindow`(R1-36:`W ≥ 0` ∧ `W` 偶数 ∧ `eps ≥ 0`)

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    /// <summary>
    /// <c>W</c> / <c>TREND_EPS</c> 的端到端流经判据:烘焙产物 → <see cref="DiseaseRegistryDataSet"/>
    /// → <see cref="CompositionRoot.Assemble"/> → <see cref="DiseaseVitalsService"/> → F4 终态。
    /// </summary>
    public class WTrendEpsVitalsEndToEndTest
    {
        // ══════════════════════════════════════════════════════════════════════
        // 判据用的合成量(**不是设计值** —— 见文件头 ②)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>合成病种 id(须 ∈ [1,64],R1-33)。</summary>
        private const int DiseaseId = 41;

        /// <summary>
        /// `TREND_EPS` 的「大死区」取值 —— 单位是 **Q16.16 raw**,故 = <c>8.0</c>。
        /// <para>选值依据:合成曲线的**最大上升斜率** ≈ <c>A_peak / τ_rise / R_rise</c>
        /// ≈ <c>2.32 / tick</c>(raw ≈ 152k)⇒ 取 8.0(raw 524288)给出 ≈ 3.4 倍余量,
        /// 保证上升段**任意** tick 的 trend 都被死区吃掉。取小了会让判据退化成
        /// 「碰巧某几 tick 被吃掉」的脆弱形。</para>
        /// </summary>
        private const long WideEpsRaw = 8L * 65536L;   // = 524288 raw = 8.0

        /// <summary>驱动上界 —— 须覆盖「衰减到低位 + 攒满 W」的最晚时刻(W=100 时为 210)。</summary>
        private const long DriveUntil = 400;

        // ══════════════════════════════════════════════════════════════════════
        // 盘上产物(其余 data-core 表;**本文件自己不写盘**)
        // ══════════════════════════════════════════════════════════════════════

        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        private static string CookedDir => Path.Combine(RepoRoot, "unity", "Assets", "DataCooked");

        private static byte[] ReadCommitted(string fileName)
        {
            string path = Path.Combine(CookedDir, fileName);
            Assert.That(File.Exists(path), Is.True, $"盘上产物缺失:{path}");
            return File.ReadAllBytes(path);
        }

        /// <summary>⚠️ 静态注入缝跨测试残留 ⇒ 每测前后复原(承 `reader_wiring_gate_test` 同款)。
        /// <para>本文件**不注册** <c>InteractionKindDataSet</c>(它走惰性 Provider,装配不即时 Load)
        /// ⇒ 若不清,后续任何 `PriorityOf` 调用会撞进本测试已释放的 provider 闭包。</para></summary>
        [SetUp]
        [TearDown]
        public void ResetInjectionSeam() => KindPriorityTable.Provider = null;

        // ══════════════════════════════════════════════════════════════════════
        // 合成病种 —— 自限型,先升后衰
        // ══════════════════════════════════════════════════════════════════════
        //
        // 形状(τ = t − onset):
        //   τ < 100  : Base = A_peak × (1 − e^(−τ/50)) / R_rise   (R_rise = 1 − e^(−2) ≈ 0.865)
        //   τ ≥ 100  : Base = A_peak × e^(−(τ−100)/5)             (self_limit ⇒ 指数衰减)
        // ⇒ position = Base / SCALE 在 τ≈6 越过 RECOVER_THRESHOLD(≈0.12),
        //   在 τ≈111 重新跌破 ⇒ 「连续低位」只在**两端**成立。
        //   这正是本判据要的形态:窗口 W 是否配置,决定这两段能否攒够 held ≥ W。
        //
        // ⚠️ 所有 `Fix` 字段在 JSON 里写**字符串**(ADR-014 §四硬规则 1)。
        // ⚠️ `trend_eps` 同样是 `Fix` ⇒ 字符串,且值以 **raw/65536** 形式给出,
        //    使 `FixParse.FromRatio(epsRaw, 65536)` 逐位还原成 `epsRaw`(无舍入)。
        private const string SyntheticJsonTemplate = @"{
  ""schema_version"": 1,
  ""trend_window_ticks"": __W__,
  ""trend_eps"": ""__EPS__"",
  ""diseases"": [
    {
      ""id"": 41,
      ""key"": ""DIS_E2E_W_EPS"",
      ""kind"": ""disease"",
      ""severity"": 3,
      ""lethal"": false,
      ""scale"": ""100"",
      ""trauma_half_life"": 60,
      ""natural_progress"": {
        ""incubation"": 0,
        ""a_peak"": ""100"",
        ""tau_rise"": ""50"",
        ""tau_peak"": 100,
        ""tau_fall"": ""5"",
        ""plateau"": false,
        ""self_limit"": true,
        ""boundary_mode"": ""monotone""
      }
    }
  ]
}";

        /// <summary>按给定的 <c>W</c> / <c>TREND_EPS</c>(raw)造一份合成源文本。</summary>
        /// <param name="w">趋势差分窗口(tick;须为偶数且 ≥ 0 —— R1-36)。</param>
        /// <param name="epsRaw">趋势死区(Q16.16 raw;写成 <c>raw/65536</c> 以逐位还原)。</param>
        private static string Json(long w, long epsRaw)
            => SyntheticJsonTemplate
                .Replace("__W__", w.ToString())
                .Replace("__EPS__", epsRaw.ToString() + "/65536");

        /// <summary>烘一份合成表 ⇒ 真 cooked 字节 ⇒ 经**同一**读码器解回数据集。</summary>
        /// <remarks>
        /// ⚠️ 刻意走 <see cref="DiseaseRegistryBaker.BakeFromJson"/> 而**不是**直接调
        /// <c>DiseaseRegistryCookedWriter.Write</c> —— 前者经过 Binder(读
        /// <c>trend_window_ticks</c> / <c>trend_eps</c> 两个作者态键 + R1-36 值域门),
        /// 后者只是编码。跳过 Binder 就等于跳过「作者态键名对不对」这一跳。
        /// <para>⚠️ <c>Materialize</c> 是写/读两侧布局分叉的**唯一自动兜现**
        /// (写方 `Editor.Tools.Bake`、读方 `Gameplay.Presentation`,两份手写布局)。</para>
        /// </remarks>
        private static DiseaseRegistryDataSet Bake(long w, long epsRaw)
        {
            DiseaseRegistryBaker.BakeOutput output = DiseaseRegistryBaker.BakeFromJson(Json(w, epsRaw));
            Assert.That(output.TrendWindowTicks, Is.EqualTo(w),
                "Binder 未把 trend_window_ticks 读进 BakeOutput ⇒ 后续全部判据无意义");
            Assert.That(output.TrendEpsRaw, Is.EqualTo(epsRaw),
                "Binder 未把 trend_eps 读进 BakeOutput(注意 JSON 里是字符串 raw/65536)");

            DiseaseRegistryDataSet viaMaterialize = DiseaseRegistryBaker.Materialize(output);
            Assert.That(viaMaterialize.TrendWindowTicks, Is.EqualTo(w), "Materialize 往返丢了 W");
            Assert.That(viaMaterialize.TrendEpsRaw, Is.EqualTo(epsRaw), "Materialize 往返丢了 TREND_EPS");
            return viaMaterialize;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 装配 —— 两条路
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 甲路:<c>dataProvider</c> 给真表 ⇒ <c>AssembleCore</c> 走 ② 级(真注册表 + 真 W/eps)。
        /// </summary>
        /// <remarks>
        /// 夹具里的**其余**六份 data-core 产物都是盘上真件(零合成):
        /// <c>AssembleCore</c> 在 <c>dataProvider != null</c> 时对它们做**即时** Load,
        /// 缺任一份装配当场抛。它们与本判据无关,只为让装配面完备。
        /// <para>⚠️ 不注册的只有 <c>BuildModules</c> / <c>ClinicFurniture</c> / <c>ClinicRoom</c> ——
        /// 它们仅在 <c>structures != null</c> 时 Load,本装配不传 <c>structures</c>。</para>
        /// </remarks>
        private static CompositionRootServices AssembleViaProvider(DiseaseRegistryDataSet registry)
            => CompositionRoot.Assemble(
                registry: null,
                dataProvider: new MultiTableProvider()
                    .Add(registry)
                    .Add(DiseaseActionAxisCookedCodec.Read(
                        ReadCommitted("disease_action_axis.cooked.bytes")))
                    .Add(PrescriptionActionsCookedCodec.Read(
                        ReadCommitted("prescription_actions.cooked.bytes")))
                    // 交互表:装配只装**惰性** Provider 闭包(`KindPriorityTable.Provider`),
                    // 装配期不 Load。仍注册它 —— 装配若将来改成即时 Load,这里不会静默变噪声。
                    .Add(InteractionKindCookedCodec.Read(
                        ReadCommitted("interaction_kinds.cooked.bytes")))
                    .Add(DiagnosisCookedDisk.LoadSigns())
                    .Add(DiagnosisCookedDisk.LoadReadFloor())
                    .Add(DiagnosisCookedDisk.LoadJudgmentLexicon()));

        /// <summary>
        /// 乙路:caller 直传行集 ⇒ <c>AssembleCore</c> 走 ① 级,**W / eps 恒 0**
        /// (该级刻意不混入真值 —— 见 <c>CompositionRoot</c> 三级优先处的注释)。
        /// <para>这是甲路的对照,不是另一种合法配置。</para>
        /// </summary>
        private static CompositionRootServices AssembleViaCallerTable(DiseaseRegistryDataSet registry)
            => CompositionRoot.Assemble(registry: registry.Rows);

        // ══════════════════════════════════════════════════════════════════════
        // 驱动 —— 找出「首次判出某终态」的 tick
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>装配 + 建档 + 逐 tick 驱动,返回首次判出 <paramref name="target"/> 的 tick。</summary>
        /// <returns>首次命中的 tick;<c>-1</c> = 驱动到 <see cref="DriveUntil"/> 仍未命中。</returns>
        private static long FirstTickWithOutcome(CompositionRootServices bag, InjuryState target)
        {
            DiseaseVitalsService svc = bag.VitalsService;
            PatientId patient = bag.PatientSpawner.SpawnNext(DiseaseId, tick: 0);

            for (long tick = 0; tick <= DriveUntil; tick++)
            {
                svc.OnTickEdge(tick);
                if (svc.GetOutcome(patient) == target) return tick;
            }
            return -1;
        }

        // ══════════════════════════════════════════════════════════════════════
        // ① 核心:W 真的从产物流到 vitals
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// <b>B1 缺口的直接闭合判据</b>:同一个病种、同一条曲线,只差「W 从哪来」,
        /// 终态必须不同 —— 甲路能判出痊愈,乙路永远不能。
        /// </summary>
        /// <remarks>
        /// <b>判别力(三向)</b>:
        /// <list type="bullet">
        /// <item>把 <c>CompositionRoot</c> 的 ② 级改回「不读 <c>TrendWindowTicks</c>」
        /// ⇒ 甲路 W = 0 ⇒ 与乙路同绿(都判不出)⇒ 本测红。</item>
        /// <item>把 <c>DiseaseRegistryCookedWriter</c> 的 W 字段写死 0(或读方不解析)
        /// ⇒ 同上,本测红。</item>
        /// <item>把 <c>F4OutcomeEvaluator</c> 的 <c>IsConfigured</c> 改成恒 true
        /// ⇒ 乙路也会判出痊愈 ⇒ 本测红。</item>
        /// </list>
        /// <para>⚠️ 乙路「永不 Recovered」不是靠 W=0 单独保证的:W=0 只关掉两支,
        /// 真正让它**不会**误判的是 <c>IsConfigured</c> 这个门。两者都要在,本测才立得住。</para>
        /// </remarks>
        [Test]
        public void test_endToEnd_WFromCookedData_makesRecoveryReachable()
        {
            const long W = 100;
            DiseaseRegistryDataSet registry = Bake(w: W, epsRaw: 0);

            long viaProvider = FirstTickWithOutcome(AssembleViaProvider(registry), InjuryState.Recovered);
            long viaCallerTable = FirstTickWithOutcome(
                AssembleViaCallerTable(registry), InjuryState.Recovered);

            Assert.That(viaProvider, Is.GreaterThanOrEqualTo(0),
                $"甲路(W={W} 来自烘焙产物)应能判出痊愈 —— 实际驱动到 tick {DriveUntil} 仍未判出。\n" +
                "0 命中 = W 没从产物流到 DiseaseVitalsService(条件 ⑥ `held >= W` 恒不成立)");

            Assert.That(viaCallerTable, Is.EqualTo(-1),
                $"乙路(caller 传表,刻意不混真 W/eps)不得判出痊愈 —— 实际在 tick {viaCallerTable} 就判出了。\n" +
                "判出 = 要么 CompositionRoot 的 ① 级混入了真值(违三级优先纪律)," +
                "要么 F4 的 IsConfigured 门失效(两支在 W=0 下被求值)");

            // 判据的时序含义:痊愈必须晚于「衰减到低位 + 攒满 W」——
            // 早于 τ_peak 就判出 ⇒ 条件 ⑤(非上升期门)或 ⑥ 有漏。
            Assert.That(viaProvider, Is.GreaterThan(100),
                $"痊愈不应早于 τ_peak(=100)—— 实际 tick {viaProvider}。" +
                "过早判出 = 条件 ⑤ / ⑥ 之一失效(上升期不该判愈)");
        }

        /// <summary>
        /// <c>W = 0</c>(未配置哨兵)经产物走一遍 ⇒ 与乙路同结果:两支不求值。
        /// <para>这条钉的是「管线通 ≠ 功能可用」:**同一份管道**,W 填 0 时功能就是关的。
        /// 它同时是上一条的阴性对照 —— 若把上一条的红归因于「装配路径差异」,本测会指出
        /// 真正的变量是 W 的值本身。</para>
        /// </summary>
        [Test]
        public void test_endToEnd_WZero_fromCookedData_neverRecovers()
        {
            DiseaseRegistryDataSet registry = Bake(w: 0, epsRaw: 0);
            Assert.That(registry.TrendWindowTicks, Is.EqualTo(0), "W=0 须原样读回(不得被填默认值)");

            long viaProvider = FirstTickWithOutcome(AssembleViaProvider(registry), InjuryState.Recovered);

            Assert.That(viaProvider, Is.EqualTo(-1),
                "W=0(未配置)⇒ RecoveryWindowInput.IsConfigured == false ⇒ 痊愈支 / 迁延支**都不求值**," +
                $"不得判出痊愈 —— 实际在 tick {viaProvider} 判出。" +
                "判出 = IsConfigured 门被绕过(它要求 WindowTicks > 0)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ② TREND_EPS 真的从产物流到 vitals
        // ══════════════════════════════════════════════════════════════════════
        //
        // 难点:TREND_EPS 只影响两处 ——
        //   (a) `ComputeTrendBackward` 的死区(`|trend| < eps ⇒ 归零`);
        //   (b) 痊愈支条件 ⑤ `rising = trend > eps`。
        // 衰减段的 trend 恒为负 ⇒ eps 怎么取都过 ⑤;故**只能用上升段**做判据。
        //
        // 上升段判据形状(W 取小值 4,使「连续低位」在病程最初几 tick 就能攒满):
        //   · eps = 0  ⇒ 上升段 trend > 0 ⇒ ⑤ 判「仍在上升」∧ τ ≤ τ_peak ⇒ **不判愈**
        //     ⇒ 一路拖到衰减段末尾(τ≈114)才可能痊愈;
        //   · eps = 8.0(> 最大上升斜率 2.32)⇒ 上升段 trend 被死区归零 ⇒ ⑤ 放行
        //     ⇒ 只要 ⑥ 满足(τ=3 时 held 已达 W=4)就**当场判愈**。
        //
        // ⇒ 判据 = 两者的「首次 Recovered tick」必须显著不同。这是**差值判据**,
        //    不依赖我对定点 exp 的逐位预估(那正是本文件刻意避免的脆弱面)。

        /// <summary>
        /// 只差 <c>TREND_EPS</c> 的两份产物 ⇒ 首次判愈的时刻必须显著不同。
        /// </summary>
        /// <remarks>
        /// <b>判别力</b>:把 <c>CompositionRoot</c> ② 级的 <c>trendEpsRaw</c> 灌成 0(或漏灌)
        /// ⇒ 两份产物行为相同 ⇒ 本测红。把 <c>DiseaseRegistryCookedCodec</c> 的 eps 解析
        /// 去掉(恒 0)⇒ 同上。
        /// </remarks>
        [Test]
        public void test_endToEnd_trendEpsFromCookedData_shiftsRecoveryOnset()
        {
            const long W = 4;   // 小窗口:上升段最初几 tick 即可攒满 held

            DiseaseRegistryDataSet tight = Bake(w: W, epsRaw: 0);          // 死区关:trend 原样生效
            DiseaseRegistryDataSet wide = Bake(w: W, epsRaw: WideEpsRaw);   // 死区大:上升段 trend 归零

            Assert.That(tight.TrendEpsRaw, Is.EqualTo(0), "tight 表 eps 应为 0");
            Assert.That(wide.TrendEpsRaw, Is.EqualTo(WideEpsRaw), "wide 表 eps 应原样读回");

            long tightFirst = FirstTickWithOutcome(AssembleViaProvider(tight), InjuryState.Recovered);
            long wideFirst = FirstTickWithOutcome(AssembleViaProvider(wide), InjuryState.Recovered);

            Assert.That(wideFirst, Is.GreaterThanOrEqualTo(0),
                "大死区表应在上升段就判出痊愈(⑤ 被死区放行)—— " +
                $"实际 {DriveUntil} tick 内未判出。\n" +
                "0 命中 = eps 没从产物流到 vitals,或死区本身没生效");

            Assert.That(tightFirst, Is.EqualTo(-1).Or.GreaterThan(wideFirst),
                "小死区表(trend 未被归零)不得在 wide 之前判出痊愈 —— " +
                $"tight={tightFirst}, wide={wideFirst}。两者相同 = TREND_EPS 没被消费");

            Assert.That(tightFirst == -1 || tightFirst - wideFirst >= 10,
                $"两表的首次判愈时刻应显著不同(差 ≥ 10 tick)—— 实际 tight={tightFirst}, wide={wideFirst}。" +
                "差距过小 = 差异来自舍入而非死区");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ③ 装配面:产物确实携带 W/eps(结构判据,补行为判据的归因盲区)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 产物**确实带着**非零 W/eps,且行集 id 与建档 id 一致 —— 这是上两条行为判据的
        /// **前提**。它单独不足以证明接线,但缺了它,行为判据的归因就不唯一
        /// (「甲路绿」可能被解释成别的差异)。
        /// </summary>
        [Test]
        public void test_endToEnd_tableCarriesWindow_andRowIdMatches()
        {
            DiseaseRegistryDataSet registry = Bake(w: 100, epsRaw: WideEpsRaw);

            Assert.That(registry.TrendWindowTicks, Is.EqualTo(100), "产物须携带 W");
            Assert.That(registry.TrendEpsRaw, Is.EqualTo(WideEpsRaw), "产物须携带 eps");
            Assert.That(registry.Rows, Is.Not.Empty, "产物须携带行集(否则装配无病种可查)");
            Assert.That(registry.Rows.Count, Is.EqualTo(1), "本夹具恰一条病种");
            Assert.That(registry.Rows[0].DiseaseId, Is.EqualTo(DiseaseId),
                "行集 id 须与建档 id 一致 —— 不一致则 Step 的「病种不在注册表内」会 fail-loud");
        }

        // ══════════════════════════════════════════════════════════════════════
        // 夹具
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 按类型分派的 <see cref="IDataProvider"/> 夹具。
        /// <para>⚠️ 缺表 ⇒ **抛**,不返回 default —— 「装配问到了没准备的表」本身就是接线错的
        /// 信号,静默返回 <c>default</c> 会把它变成下游的空表崩溃(与本文件判据无关的噪声)。</para>
        /// </summary>
        private sealed class MultiTableProvider : IDataProvider
        {
            private readonly Dictionary<Type, object> _byType = new Dictionary<Type, object>();

            public MultiTableProvider Add<TDataSet>(in TDataSet dataSet) where TDataSet : struct
            {
                _byType[typeof(TDataSet)] = dataSet;
                return this;
            }

            public TDataSet Load<TDataSet>() where TDataSet : struct
            {
                if (_byType.TryGetValue(typeof(TDataSet), out object boxed))
                    return (TDataSet)boxed;
                throw new InvalidOperationException(
                    $"MultiTableProvider 无 {typeof(TDataSet).Name} —— 本测不该问到它(真问 = 接线错)");
            }
        }
    }
}
