// 关卡工具 —— 槽位骨架的**确定性导出器**(ADR-022 §三)。
//
// 权威来源:
//   ADR-022 §三(`assets/data/world_buildslots.json`,整数格 / 枚举一律整数,`Fix` 写字符串 —— 本表无 Fix)
//   §②(导出前跑一致性检查;失败 = 显式 throw,不降级为告警)
//   ADR-014 §二(作者态源文件是烘焙的**唯一**输入;运行期零 JSON 解析器)
//
// ⚠️ **确定性**:字段序固定 · 槽位按 `(x,y,z)` 升序 · 缩进固定 · LF 行尾 · 零本地时间 /
//   零随机 / 零字典迭代序 ⇒ 同一 `LevelSource` 两次导出**逐字节相同**。
//
// ⚠️ **本文件是 `assets/data/world_buildslots.json` 的唯一作者** —— 手写该 JSON 违反
//    ADR-022 §二(逻辑层「只能由关卡工具编写并导出」)。唯一合法产法:
//      batch: `Unity -batchmode -projectPath unity -executeMethod
//              DaYiJingCheng.EditorTools.Level.LevelExporter.ExportP0ClinicToRepo`
//      或 `LevelExporter.ExportToRepo()`(测试 / 菜单同调用)。

using System;
using System.Collections.Generic;
using System.Globalization;   // InvariantCulture —— 跨区域性逐字节确定性(A-18)
using System.IO;
using System.Text;
using DaYiJingCheng.Sim.World;   // SlotType(唯一真源)
using UnityEngine;   // Application.dataPath(本程序集为编辑期工具,不进玩家构建)

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>槽位骨架的确定性 JSON 导出器。</summary>
    public static class LevelExporter
    {
        /// <summary>作者态文件名(ADR-022 §三的导出契约)。</summary>
        public const string FileName = "world_buildslots.json";

        /// <summary>本表的 schema_version(单一出处 = 本常量;读方 codec 的期望值与它同值)。</summary>
        /// <remarks>⚠️ <b>2026-10-28(S20-20 挂标 #2):1 → 2</b> —— 行对象新增 `module_id`
        /// (开档既有家具的模块 id)。旧 schema 1 的产物**无**该字段 ⇒ 读方必须拒
        /// (`WorldBuildSlotsCookedCodec.ExpectedSchemaVersion` 同值 2)。
        /// ⚠️ 三处**必须同时**改:本常量 / `WorldBuildSlotsCookedCodec.ExpectedSchemaVersion` /
        /// `WorldBuildSlotsBinder.RowKeys` —— 前两者由绑定器的 `ReadSchemaVersion` 交叉校验
        /// (它以 codec 常量为期望值),故本常量若漏改,真种子烘焙**当场**报「schemaVersion ≠ 期望」。</remarks>
        public const uint SchemaVersion = 2u;

        // ⚠️ **此处曾有 `SlotTypeNames` / `DeriveSlotTypeNames()`**(名字表 + 名字→序数派生)。
        //    2026-10-28 代码评审 **A-21** 后删除,两半各有归处 ——
        //      · **导出侧的名字**不再需要表:`TypeName()` 直接 `Enum.GetName`(见下);
        //      · **绑定侧的名字→序数**唯一出处 = `Editor.Tools.Bake.SlotTypeVocabulary`
        //        (它派生自同一个 `Sim.World.SlotType`,并带 `[1,30]` 位域守卫)。
        //    删除理由 = 它是**零消费方的死 API**(全仓 grep 零命中),留着等于给「再来一份手写表」
        //    留下榜样。若将来导出侧真的需要闭集文案,请复用 `SlotTypeVocabulary`,
        //    **不要在本文件重建第二份**。

        /// <summary>把关卡源序列化成确定性 JSON 文本(不落盘)。</summary>
        /// <remarks>⚠️ 导出前**先跑一致性检查**(ADR-022 §②):硬失败 ⇒ `InvalidOperationException`。</remarks>
        public static string ExportBuildSlots(LevelSource src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            LevelConsistencyCheck.CheckOrThrow(src);

            // 全序 = (x, y, z) 升序。声明序不承载语义(重排 = 零信息)。
            var ordered = new List<SlotSpec>(src.Slots);
            ordered.Sort((a, b) =>
            {
                int cmp = a.X.CompareTo(b.X);
                if (cmp != 0) return cmp;
                cmp = a.Y.CompareTo(b.Y);
                if (cmp != 0) return cmp;
                return a.Z.CompareTo(b.Z);
            });

            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"schema_version\": ").Append(Num((int)SchemaVersion)).Append(",\n");
            sb.Append("  \"level_id\": ").Append(Quote(src.LevelId)).Append(",\n");
            sb.Append("  \"world_size\": [")
              .Append(Num(src.Width)).Append(", ")
              .Append(Num(src.Height)).Append(", ")
              .Append(Num(src.Depth)).Append("],\n");
            sb.Append("  \"slots\": [\n");

            for (int i = 0; i < ordered.Count; i++)
            {
                SlotSpec s = ordered[i];
                sb.Append("    {\"cell\": [")
                  .Append(Num(s.X)).Append(", ").Append(Num(s.Y)).Append(", ").Append(Num(s.Z))
                  .Append("], \"slot_type\": ").Append(Quote(TypeName(s.Type)))
                  .Append(", \"baked_initial\": ").Append(s.BakedInitial ? "true" : "false");
                // ⚠️ `module_id` **恒出现**(schema 2):`baked_initial = false` 的行写 `null`,
                //    而不是省略字段 —— 「省略」与「写了 null」在读者眼里是两件事
                //    (前者像作者遗漏,后者是明确声明「本格无既有家具」),与
                //    `baked_initial` 必填的既有口径同源。
                sb.Append(", \"module_id\": ");
                if (s.BakedInitial)
                    sb.Append(Num(s.ModuleId));
                else
                    sb.Append("null");
                sb.Append('}')
                  .Append(i + 1 < ordered.Count ? ",\n" : "\n");
            }

            sb.Append("  ]\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>写盘到任意路径(调用方须保证路径在允许面内 —— 测试只给它 `Path.GetTempPath()`)。</summary>
        public static void ExportTo(string path, LevelSource src)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("路径为空", nameof(path));
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            // UTF-8 **无 BOM**(BOM 会让字节流随编辑器差异漂移,破坏 ConfigVersion 的可复算性)。
            File.WriteAllText(path, ExportBuildSlots(src), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        /// <summary>导出 P0 医馆到 `<repoRoot>/assets/data/world_buildslots.json`。</summary>
        /// <exception cref="InvalidOperationException">一致性检查硬失败。</exception>
        public static void ExportToRepo(string repoRoot)
        {
            if (string.IsNullOrEmpty(repoRoot)) throw new ArgumentNullException(nameof(repoRoot));
            ExportTo(Path.Combine(repoRoot, "assets", "data", FileName), P0ClinicLevel.Define());
        }

        /// <summary>batch 入口:导出 P0 医馆到**本工程所在仓库**。</summary>
        /// <remarks>⚠️ `Application.dataPath` 仅用于推导仓库根(`unity/Assets` 的上两级)。</remarks>
        public static void ExportP0ClinicToRepo()
        {
            string repoRoot = RepositoryRoot();
            ExportToRepo(repoRoot);
            Debug.Log($"[关卡工具] 已导出 {FileName} → {Path.Combine(repoRoot, "assets", "data", FileName)}");
        }

        /// <summary>仓库根(`unity/Assets` 的上两级)。</summary>
        public static string RepositoryRoot()
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        /// <summary>枚举 → 导出名;`None` 与未定义值一律拒(导出合法值域之外的词是不可读数据)。</summary>
        /// <remarks>⚠️ 名字直接取自 CLR 枚举(`Enum.GetName`),**不查手写表** —— 表与枚举一旦分家,
        /// 「作者写了 `Decor`、导出成 `Bed`」这种错位是静默的(2026-10-28 评审 A-21)。</remarks>
        private static string TypeName(SlotType type)
        {
            if (type != SlotType.None && Enum.IsDefined(typeof(SlotType), type))
                return Enum.GetName(typeof(SlotType), type);
            throw new InvalidOperationException(
                $"[关卡工具] slot_type = {(int)type} 无可读导出名(None / 未定义值不得导出)");
        }

        /// <summary>整数 → **区域性无关**的文本(A-18:导出字节须跨 locale 逐字节相同)。</summary>
        /// <remarks>⚠️ `StringBuilder.Append(int)` 走 `int.ToString()`(当前区域性);
        /// CI 的 `InvariantCulture` 与开发机的某区域性可能产出不同字节 ⇒ `ConfigVersion`
        /// (内容哈希)不同 ⇒ 存档头比对报「配置版本不符」,**错误指向存档而非导出器**。</remarks>
        private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>JSON 字符串转义(本表只可能出现字母数字与下划线,但仍按最小集转义)。</summary>
        private static string Quote(string s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
