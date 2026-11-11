// 6 的生态区气候基线查询(`TD-processing-09` 裁决乙 · 2026-11-11)。
//
// 权威来源:
//   GDD `world-and-ecozones.md:918`「生态区的气候属性集 | 查表 | 6 定义 / 5 消费」
//   GDD `world-and-ecozones.md:742` / EC-6-10:`EcozoneOf` 返回 `None` 是**合法结果**
//   GDD `time-and-weather.md` **AC-5-12**:玩家不在任何生态区 ⇒ 取**全局默认**,
//     **非最近区**(可测反例 = 两区中点格)
//   ADR-015 §三(单一整数格)· ADR-025 §①(本类住边界层)
//
// ⚠️ 本文件**不读**仓库内文件:数据集与多边形全部在夹具内构造 ——
//    要钉的是「查表 / 兜底 / 错配即抛」三条**逻辑**,不是产物内容
//    (产物内容由 `cooked_drift_all_artifacts_test` 与烘焙门守)。

using System;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.WorldEcozones
{
    public sealed class EcozoneClimateBaselineTest
    {
        private static readonly WorldPos[] ZoneZeroQuad =
        {
            new WorldPos(1, 0, 1), new WorldPos(5, 0, 1), new WorldPos(5, 0, 5), new WorldPos(1, 0, 5),
        };

        private static readonly WorldPos[] ZoneOneQuad =
        {
            new WorldPos(10, 0, 1), new WorldPos(14, 0, 1), new WorldPos(14, 0, 5), new WorldPos(10, 0, 5),
        };

        private static WorldEcozonesDataSet Data(Fix globalDefault, params WorldEcozoneRow[] rows)
            => new WorldEcozonesDataSet(Array.AsReadOnly(rows), schemaVersion: 2u,
                configVersion: 0u, defaultClimateEnvMod: globalDefault);

        private static WorldEcozoneRow Row(int id, WorldPos[] verts, string fixLiteral)
            => new WorldEcozoneRow(id, verts, DaYiJingCheng.Sim.Contracts.FixParse.Parse(fixLiteral));

        private static EcozoneRegistry Registry(WorldEcozonesDataSet data)
        {
            var reg = new EcozoneRegistry();
            for (int i = 0; i < data.Zones.Count; i++)
                reg.Register(new EcozonePolygon(new EcozoneId(data.Zones[i].EcozoneId), data.Zones[i].Vertices));
            return reg;
        }

        // ── ① 区内 ⇒ 取该区的值 ─────────────────────────────────────────────

        [Test]
        public void test_climate_ofCellInsideZone_returnsThatZonesValue()
        {
            WorldEcozonesDataSet data = Data(FixParse.Parse("-1/10"),
                Row(0, ZoneZeroQuad, "-1/5"),
                Row(1, ZoneOneQuad, "3/10"));
            var query = new EcozoneClimateBaseline(Registry(data), data);

            Assert.That(query.Of(new WorldPos(3, 0, 3)).Raw, Is.EqualTo(FixParse.Parse("-1/5").Raw),
                "医馆前厅格的基线须是 zone 0 的值(负值合法,零钳制)");
            Assert.That(query.Of(new WorldPos(12, 0, 3)).Raw, Is.EqualTo(FixParse.Parse("3/10").Raw),
                "药圃格的基线须是 zone 1 的值");
        }

        // ── ② 区外 ⇒ 取全局默认(**非最近区**)────────────────────────────────

        [Test]
        public void test_climate_ofCellOutsideEveryZone_returnsGlobalDefault_notNearest()
        {
            // 两区中点格 (7,3):到 zone 0 与 zone 1 的几何距离相等。
            // 「取最近区」在这里**无唯一解**,且无论取哪边都不等于全局默认 ——
            // 这就是 AC-5-12 的可测反例。
            Fix globalDefault = FixParse.Parse("-1/10");
            WorldEcozonesDataSet data = Data(globalDefault,
                Row(0, ZoneZeroQuad, "-1/5"),
                Row(1, ZoneOneQuad, "3/10"));
            var query = new EcozoneClimateBaseline(Registry(data), data);

            Fix atGap = query.Of(new WorldPos(7, 0, 3));

            Assert.That(atGap.Raw, Is.EqualTo(globalDefault.Raw),
                "真空带须取全局默认(AC-5-12:非最近区)");
            Assert.That(atGap.Raw, Is.Not.EqualTo(FixParse.Parse("-1/5").Raw));
            Assert.That(atGap.Raw, Is.Not.EqualTo(FixParse.Parse("3/10").Raw));
        }

        [Test]
        public void test_climate_globalDefault_isExposedVerbatim()
        {
            Fix globalDefault = FixParse.Parse("-7/100");
            WorldEcozonesDataSet data = Data(globalDefault, Row(0, ZoneZeroQuad, "0"));

            Assert.That(new EcozoneClimateBaseline(Registry(data), data).GlobalDefault.Raw,
                Is.EqualTo(globalDefault.Raw));
        }

        // ── ③ 两份产物不同批 ⇒ 抛(不降级为默认)──────────────────────────────

        [Test]
        public void test_climate_zoneInPolygonTableButMissingHere_throws()
        {
            // 多边形表 = 两个区(同一次装载)……但气候表只带 zone 0。
            WorldEcozonesDataSet withTwo = Data(Fix.Zero,
                Row(0, ZoneZeroQuad, "0"), Row(1, ZoneOneQuad, "0"));
            WorldEcozonesDataSet withOne = Data(Fix.Zero, Row(0, ZoneZeroQuad, "0"));

            var query = new EcozoneClimateBaseline(Registry(withTwo), withOne);

            var ex = Assert.Throws<InvalidOperationException>(() => query.Of(new WorldPos(12, 0, 3)));
            StringAssert.Contains("不同批", ex.Message,
                "错配须**抛**;静默返回全局默认会把「产物错配」显示成「这里没有生态区」");
        }

        // ── ④ 行序不是严格升序 ⇒ 构造期即抛(产物被手工改过)──────────────────

        [Test]
        public void test_ctor_rowsNotStrictlyAscending_throws()
        {
            WorldEcozonesDataSet bad = Data(Fix.Zero,
                Row(1, ZoneOneQuad, "0"), Row(0, ZoneZeroQuad, "0"));

            var ex = Assert.Throws<InvalidOperationException>(() => new EcozoneClimateBaseline(Registry(bad), bad));
            StringAssert.Contains("升序", ex.Message);
        }
    }
}
