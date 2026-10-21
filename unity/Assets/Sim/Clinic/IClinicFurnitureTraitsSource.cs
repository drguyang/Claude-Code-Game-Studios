// 权威来源:GDD design/gdd/clinic-machine.md 公共定义 `:155-172`(S 成员判据 · type(c) / tier(c) / e_env(type))
//          · ADR-015 §一(逻辑层整数格)· ADR-005(确定性模拟,纯函数无隐藏状态)
//
// ⚠️ **本接口补的是 F-24-1 / F-24-2 的"每格属性"输入面**:
//   `IPlacedStructureReadView`(23)只报**占用格**,不报 `type(c)` / `tier(c)` / `e_env(type)` ——
//   后三者按 GDD 来自 **FURN_TABLE**,而 FURN_TABLE **全库不存在**(`O-24-1` / `TD-clinic-01`:
//   21a 家具目录的 `type` / `tier` / `e_env` 三列已双侧登记、零实现)。
//   ⇒ 本接口是那条缺口的**注入缝**:求值器只依赖抽象,生产实现者**当前不存在**
//     (`FurnTraitsProductionReady == false` 哨兵),测试注入夹具源。
//
// ⚠️ 三值分类是刻意的(禁二值 bool):
//   `Furniture` / `NonFurniture` / `Unknown` 三分让「这格不是家具」与「我不知道这格是什么」
//   在**类型上**可分。若用 bool,源未就绪时会把每格都答成「不是家具」⇒ `env_score` 静默退化成
//   `base_env`(本项目最忌的静默失效形态)。`Unknown` ⇒ 求值器**硬失败**。

using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Clinic
{
    /// <summary>一格家具的 F-24-1 / F-24-2 属性(`type(c)` / `tier(c)` / `e_env(type)`)。</summary>
    public readonly struct ClinicFurnitureTraits
    {
        /// <summary>家具类型 id(`type(c)`;GDD 要求 ∈ `FURN_TYPES` 闭集)。</summary>
        public readonly string Type;

        /// <summary>器具档位 `tier(c)` ∈ [0, TIER_MAX];0 = 无档,不进 F-24-2 的和。</summary>
        public readonly int Tier;

        /// <summary>单件环境贡献 `e_env(type)`,可负(F-24-1)。</summary>
        public readonly Fix EEnv;

        /// <summary>构造一份属性。</summary>
        public ClinicFurnitureTraits(string type, int tier, Fix eEnv)
        {
            Type = type; Tier = tier; EEnv = eEnv;
        }
    }

    /// <summary>格属性查询的三值结果(见文件头「三值分类是刻意的」)。</summary>
    public enum ClinicCellLookup : byte
    {
        /// <summary>该格是家具格,属性有效。</summary>
        Furniture = 0,

        /// <summary>该格确定**不是**家具格(外壳 / 空地板 / 纯装饰件)—— 合法跳过。</summary>
        NonFurniture = 1,

        /// <summary>**查不到**(源未就绪 / 该格不在目录里)—— 求值器硬失败,禁静默跳过。</summary>
        Unknown = 2,
    }

    /// <summary>
    /// 系统 24 的**格属性来源**(F-24-1 / F-24-2 的 `type(c)` / `tier(c)` / `e_env`)。
    /// <para>⚠️ 生产实现者**当前不存在** —— 依赖 `O-24-1`(FURN_TABLE:21a 家具目录扩 `type`/`tier`/`e_env`),
    /// 见 <see cref="ClinicModifierEvaluator.FurnTraitsProductionReady"/> 缺口哨兵。</para>
    /// <para>实现方契约:<see cref="Lookup"/> 对**每个** `S` 成员格都必须给出
    /// <see cref="ClinicCellLookup.Furniture"/> 或 <see cref="ClinicCellLookup.NonFurniture"/> ——
    /// `Unknown` 会让求值硬失败(它是「源不完整」的信号,不是「这格无所谓」)。</para>
    /// </summary>
    public interface IClinicFurnitureTraitsSource
    {
        /// <summary>查一格的家具属性。</summary>
        /// <param name="cell">世界格(整数格,ADR-015 §三)。</param>
        /// <param name="traits">仅当返回 <see cref="ClinicCellLookup.Furniture"/> 时有效。</param>
        ClinicCellLookup Lookup(WorldPos cell, out ClinicFurnitureTraits traits);
    }
}
