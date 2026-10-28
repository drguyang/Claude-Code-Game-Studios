// 关卡工具 —— **关卡登记簿**(`level_id → LevelSource` 的唯一入口)。
//
// 权威来源:
//   ADR-022 §二(逻辑层整数数据的唯一作者)· §三(导出契约)
//   `production/epics/level-tool/story-001-level-source-generalization.md` AC-2 / AC-5
//
// ⚠️ 本文件存在的**唯一理由**:在它之前,「关卡 → 声明式源」的映射**不存在** ——
//    `LevelExporter.ExportToRepo` 直接 `P0ClinicLevel.Define()`,`ExportP0ClinicToRepo`
//    的名字里就写着 `P0Clinic`。⇒ 「加第二个关卡」没有任何入口(不是「配置化程度低」,
//    是**入口不存在**)。
//
// ⚠️ **基准关卡 = `CanonicalLevelId`** 是**命名形状的一部分**,不是装饰:
//    `WorldDataSets.FileNameFor` 用它决定「无后缀(基准)还是 `_{level_id}` 后缀(其他)」。
//    改动它 = 改动 `assets/data/world_*.json` 的文件名 = 运行期装载面(`DataCookedDisk` /
//    Addressables 组条目)跟着动 —— **不得**为了「加一个关卡」顺手改它。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>关卡登记簿(闭集;新增关卡 = <see cref="LevelIds"/> 加一行 + <see cref="Define"/> 加一支)。</summary>
    public static class LevelCatalog
    {
        /// <summary>
        /// 基准关卡 id —— **它的数据集文件名不带 `_{level_id}` 后缀**(ADR-022 §三 的契约字面)。
        /// <para>单一出处 = <see cref="P0ClinicLevel.LevelId"/>;此处只做**命名语义**的别名,
        /// 不另起一个字面量。</para>
        /// </summary>
        public const string CanonicalLevelId = P0ClinicLevel.LevelId;

        /// <summary>全部已登记关卡 id(声明序;基准关卡在第一位)。</summary>
        public static readonly IReadOnlyList<string> LevelIds = new[]
        {
            P0ClinicLevel.LevelId,
            ScaffoldSecondLevel.LevelId,
        };

        /// <summary>按 id 产出声明式关卡源。</summary>
        /// <exception cref="ArgumentException">id 不在登记簿内(报文列出全部已知 id)。</exception>
        public static LevelSource Define(string levelId)
        {
            if (string.IsNullOrEmpty(levelId))
                throw new ArgumentException("level_id 不得为空", nameof(levelId));

            switch (levelId)
            {
                case P0ClinicLevel.LevelId:      return P0ClinicLevel.Define();
                case ScaffoldSecondLevel.LevelId: return ScaffoldSecondLevel.Define();
                default:
                    throw new ArgumentException(
                        $"[关卡工具] 未知关卡 id = '{levelId}';已登记 = [{string.Join(", ", LevelIds)}]",
                        nameof(levelId));
            }
        }

        // ⚠️ **此处曾有一个 `TryDefine(levelId, out LevelSource)`**(非抛形的试探入口),实现轮内删除。
        //    理由与 `LevelExporter.FileName` 的删除同源(2026-10-28 评审 A-21):它是**零消费方的死 API**
        //    (`grep -rn 'TryDefine' unity/Assets` 只命中定义本身)。
        //    「S2 将来可能要用」不是留下的理由 —— 那是**猜测的**消费方;真到那时加一个方法是一行的事,
        //    而留一个没人调用、也没夹具钉住的入口,等于给「再加一个试探入口」立榜样。
        //    ⚠️ 本类**不允许**出现「抛形 + 非抛形」两个**未约定**的入口并存:
        //    未知 id 是**作者的错**(关卡 id 来自本类的闭集),就该显式 `throw`(承 ADR-022 §② 硬失败口径)。
    }
}
