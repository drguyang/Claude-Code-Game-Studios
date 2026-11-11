// 生态区气候基线查询(`TD-processing-09` 裁决乙 · 2026-11-11)。
//
// 权威来源:
//   GDD `world-and-ecozones.md`
//     · :918 「生态区的气候属性集 | 查表 | **6 定义 / 5 消费**」 —— 本类就是那句「查表」的运行期面
//     · `:742` / EC-6-10:`EcozoneOf` 返回 `NONE` 是**合法结果**(真空带 / 未驻留 chunk)
//   GDD `time-and-weather.md`
//     · F-5.4 的 `气候属性(生态区)` 入参
//     · **AC-5-12**:玩家不在任何生态区 ⇒ 取**全局默认**,**非最近区**
//   ADR-015 §三(单一整数格)· ADR-025 §①(本类住边界层,`Sim` 侧零缓存)
//
// ⚠️ **为什么住 `Gameplay.Presentation`**:它同时需要 `Sim` 的 `EcozoneRegistry` 与
//   `Gameplay.Presentation` 的解码产物 `WorldEcozonesDataSet`。`Sim` 够不着 codec
//   (ADR-025 §①),而把多边形判定搬进边界层 = 第二套 `EcozoneOf`(禁)。
//   本类只做「查表 + 兜底」,零几何逻辑 —— 几何仍在 `EcozoneRegistry` 的唯一实现里。
//
// ⚠️ **零缓存 / 零状态**:每次 `Of` 都是现查。缓存归消费侧的 memo(与 24 的 EC-24-05 同款
//   口径:缓存与否不改结果;24 的 `ClinicEnvironmentMemo` 已经在那一侧承担)。

using System;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>
    /// 把「格 → 生态区 → 气候基线」串起来的**唯一**运行期查询面。
    /// <para><b>用法</b>:5 的 `EnvMod.ComputeEnvModDelta` 调它取 `EcozoneBaseline`;
    /// 调用侧**只**传格,不自行判断「玩家是不是在医馆里」。</para>
    /// </summary>
    public sealed class EcozoneClimateBaseline
    {
        private readonly EcozoneRegistry _registry;
        private readonly Fix _globalDefault;

        /// <summary>按 `ecozone_id` 升序排列的 id / 值(与 cooked 行序同源;二分查找)。</summary>
        private readonly int[] _zoneIds;
        private readonly Fix[] _values;

        /// <summary>构造。</summary>
        /// <param name="registry">6 的多边形注册表(`EcozoneOf` 的唯一实现)。</param>
        /// <param name="data">`world_ecozones.cooked` 的解码产物(行序 = id 升序,codec 已校验)。</param>
        /// <exception cref="ArgumentNullException">任一半为 null(接线错误)。</exception>
        /// <exception cref="InvalidOperationException">行序不是严格升序 —— 两份产物不同批 / 手工改过产物。</exception>
        public EcozoneClimateBaseline(EcozoneRegistry registry, WorldEcozonesDataSet data)
        {
            if (data.Zones == null)
                throw new ArgumentException("生态区行集为 null —— 产物损坏", nameof(data));

            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _globalDefault = data.DefaultClimateEnvMod;

            int n = data.Zones.Count;
            _zoneIds = new int[n];
            _values = new Fix[n];
            int prev = -1;
            for (int i = 0; i < n; i++)
            {
                WorldEcozoneRow row = data.Zones[i];
                if (row.EcozoneId <= prev)
                    throw new InvalidOperationException(
                        $"[ecozone] 气候基线表行序非严格升序(id={row.EcozoneId},前行={prev})—— " +
                        "codec 已校验过;走到这里说明数据被手工改过(违反 ADR-022 §二)");
                prev = row.EcozoneId;
                _zoneIds[i] = row.EcozoneId;
                _values[i] = row.ClimateEnvMod;
            }
        }

        /// <summary>全局默认基线(无生态区格取它;AC-5-12)。</summary>
        public Fix GlobalDefault => _globalDefault;

        /// <summary>取某格的生态区气候基线(加法增量;可为负)。</summary>
        /// <param name="cell">求值格(F-5.4 调用点表:炮制 = 医馆**房间格**,不是玩家格)。</param>
        /// <returns>该格的基线;**不在任何生态区 ⇒ 全局默认**(不取最近区 —— AC-5-12 的可测反例)。</returns>
        /// <exception cref="InvalidOperationException">`EcozoneOf` 给出的 id 在多边形表里有、
        /// 在气候基线表里没有 —— 两份产物不同批。**不降级为默认值**:那会把「产物错配」
        /// 伪装成「这里没有生态区」(与 `ClinicEnvProjection` 同一条纪律)。</exception>
        public Fix Of(WorldPos cell)
        {
            EcozoneId id = _registry.EcozoneOf(cell);
            if (!id.IsValid) return _globalDefault;

            int idx = Array.BinarySearch(_zoneIds, id.Value);
            if (idx < 0)
                throw new InvalidOperationException(
                    $"[ecozone] 生态区 {id.Value} 在多边形表里有、在气候基线表里没有 —— " +
                    "两份产物不同批(应同一次烘焙);拒绝降级为全局默认");

            return _values[idx];
        }
    }
}
