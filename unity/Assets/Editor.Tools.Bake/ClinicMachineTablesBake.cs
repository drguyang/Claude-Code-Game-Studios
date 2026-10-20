// Story 001 · clinic-machine —— 24 三表两阶段烘焙的**仓根装载器**。
//
// 权威来源:ADR-014 §三 / §五(两阶段烘焙入口 · ConfigVersion = 源数据集内容哈希派生)
//          · 三个 Binder(唯一构建期校验点)· ClinicMachineCookedWriter(编码面)
//
// ⚠️ 本件刻意**不碰 UnityEngine / Addressables**:纯文件 I/O + 绑定 + 编码
//   (承 DelayTable53Bake / DiagnosisReadFloorBaker 同纪律)⇒ EditMode 测试可直接驱动。
//
// ⚠️ 四文件齐备才做**跨表**断言(它们各自的结构 / 区间门在 Binder 内):
//   · O-24-4:21a 常量 ENV_MOD_MIN < 0(读 item_database_constants.json,**零硬编码**)
//   · base_env ∈ [ENV_MOD_MIN, ENV_MOD_MAX] 域校验(规则三 / F-24-1;ENV 域 = 21a 常量)
//   注意:ENV 域 = [−1/2, +1/2] 是 **21a 的 ENV 域**,**不是** 24 的 W 域([−1/4, +1/4])—— 别串。
//
// ⚠️ 缺口哨兵:生产路径无家具类型闭集来源(O-24-1 未兑现)⇒ 传 null,
//   `BindResult.FurnMembershipEnforced == false`(缺席可观测)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>24 三表的烘焙装载器(绑定 + cooked 字节 + 跨表断言)。</summary>
    public static class ClinicMachineTablesBake
    {
        /// <summary>源文件所在目录(相对仓根;承既有先例 <c>assets/data/</c>)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>21a 常量文件(只读;O-24-4 / ENV 域的唯一来源)。</summary>
        public const string ConstantsFileName = "item_database_constants.json";

        /// <summary>烘焙输出(三份 cooked 字节 + 绑定结果)。</summary>
        public readonly struct BakeOutput
        {
            /// <summary><c>clinicmachine_context.cooked</c> 字节(含 20 字节头)。</summary>
            public readonly byte[] ContextCooked;

            /// <summary><c>clinicmachine_adj.cooked</c> 字节(含 20 字节头)。</summary>
            public readonly byte[] AdjCooked;

            /// <summary><c>clinicmachine_room.cooked</c> 字节(含 20 字节头)。</summary>
            public readonly byte[] RoomCooked;

            /// <summary>源数据集内容哈希派生的 ConfigVersion(u32)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>CONTEXT_TABLE 绑定结果(含表派生 <c>K_CONTEXT_MAX</c>)。</summary>
            public readonly ClinicMachineContextBinder.BindResult Context;

            /// <summary>ADJ_TABLE 绑定结果。</summary>
            public readonly ClinicMachineAdjBinder.BindResult Adj;

            /// <summary>ROOM_TABLE 绑定结果(含缺口哨兵)。</summary>
            public readonly ClinicMachineRoomBinder.BindResult Room;

            /// <summary>构造烘焙输出。</summary>
            public BakeOutput(
                byte[] contextCooked, byte[] adjCooked, byte[] roomCooked, uint configVersion,
                ClinicMachineContextBinder.BindResult context,
                ClinicMachineAdjBinder.BindResult adj,
                ClinicMachineRoomBinder.BindResult room)
            {
                ContextCooked = contextCooked; AdjCooked = adjCooked; RoomCooked = roomCooked;
                ConfigVersion = configVersion; Context = context; Adj = adj; Room = room;
            }
        }

        /// <summary>从仓根烘焙(生产路径:无家具类型闭集来源 ⇒ 缺口哨兵 false)。</summary>
        /// <exception cref="BakeValidationException">任一构建期拒收(聚合)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot)
            => BakeFromRepo(repoRoot, null);

        /// <summary>从仓根烘焙(可注入家具类型闭集来源 —— 仅供 O-24-1 落地后的生产接线 / 测试)。</summary>
        /// <exception cref="BakeValidationException">任一构建期拒收(聚合)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot, IFurnTypeSource furnTypeSource)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string contextJson = ReadSource(repoRoot, ClinicMachineContextBinder.FileName);
            string adjJson = ReadSource(repoRoot, ClinicMachineAdjBinder.FileName);
            string roomJson = ReadSource(repoRoot, ClinicMachineRoomBinder.FileName);
            string constantsJson = ReadSource(repoRoot, ConstantsFileName);

            return Bake(contextJson, adjJson, roomJson, constantsJson, furnTypeSource);
        }

        /// <summary>从 JSON 串烘焙(**不落盘**;与 <see cref="BakeFromRepo"/> 同一机器)—— 测试负例入口。</summary>
        /// <exception cref="BakeValidationException">任一构建期拒收(聚合)。</exception>
        public static BakeOutput BakeFromJson(
            string contextJson, string adjJson, string roomJson, string constantsJson)
            => Bake(contextJson, adjJson, roomJson, constantsJson, null);

        /// <summary>从 JSON 串烘焙(可注入闭集来源)。</summary>
        /// <exception cref="BakeValidationException">任一构建期拒收(聚合)。</exception>
        public static BakeOutput BakeFromJson(
            string contextJson, string adjJson, string roomJson, string constantsJson,
            IFurnTypeSource furnTypeSource)
            => Bake(contextJson, adjJson, roomJson, constantsJson, furnTypeSource);

        /// <summary>绑定入口(测试负例直接喂单表 JSON 串 —— 不落盘)。</summary>
        public static ClinicMachineContextBinder.BindResult BindContextForTest(string json)
            => ClinicMachineContextBinder.Bind(json);

        /// <summary>绑定入口(ADJ 单表)。</summary>
        public static ClinicMachineAdjBinder.BindResult BindAdjForTest(string json)
            => ClinicMachineAdjBinder.Bind(json);

        /// <summary>绑定入口(ROOM 单表;可注入闭集来源)。</summary>
        public static ClinicMachineRoomBinder.BindResult BindRoomForTest(
            string json, IFurnTypeSource furnTypeSource)
            => ClinicMachineRoomBinder.Bind(json, furnTypeSource);

        // ════════════════════════════════════════════════════════════════════
        // 内部
        // ════════════════════════════════════════════════════════════════════

        private static BakeOutput Bake(
            string contextJson, string adjJson, string roomJson, string constantsJson,
            IFurnTypeSource furnTypeSource)
        {
            // 三表各自的结构 / 区间门在 Binder 内(任一失败即聚合 throw)。
            ClinicMachineContextBinder.BindResult context = ClinicMachineContextBinder.Bind(contextJson);
            ClinicMachineAdjBinder.BindResult adj = ClinicMachineAdjBinder.Bind(adjJson);
            ClinicMachineRoomBinder.BindResult room = ClinicMachineRoomBinder.Bind(roomJson, furnTypeSource);

            // 跨表断言(四文件齐备才可做)
            var crossErrors = new List<string>();
            BindConstants(constantsJson, crossErrors, out Fix envMin, out Fix envMax);

            if (crossErrors.Count == 0)
                AssertBaseEnvDomain(context, envMin, envMax, crossErrors);

            if (crossErrors.Count > 0) throw new BakeValidationException(crossErrors);

            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(ClinicMachineContextBinder.FileName, contextJson),
                new KeyValuePair<string, string>(ClinicMachineAdjBinder.FileName, adjJson),
                new KeyValuePair<string, string>(ClinicMachineRoomBinder.FileName, roomJson),
                new KeyValuePair<string, string>(ConstantsFileName, constantsJson),
            });

            return new BakeOutput(
                ClinicMachineCookedWriter.WriteContext(context, configVersion),
                ClinicMachineCookedWriter.WriteAdj(adj, configVersion),
                ClinicMachineCookedWriter.WriteRoom(room, configVersion),
                configVersion, context, adj, room);
        }

        /// <summary>读 21a 常量表的 ENV 域 + O-24-4 断言(只读;不校验 21a 的其余字段)。</summary>
        private static void BindConstants(
            string json, List<string> errors, out Fix envMin, out Fix envMax)
        {
            envMin = Fix.Zero;
            envMax = Fix.Zero;

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
            {
                errors.Add($"{ConstantsFileName} 阶段1 词法失败:{lexErr}");
                return;
            }
            if (root.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{ConstantsFileName} 根须为对象(实得 {root.Kind})");
                return;
            }

            bool hasMin = ClinicMachineSchema.ReadRequiredFix(
                root, "env_mod_min", "根", ConstantsFileName, errors, out envMin);
            bool hasMax = ClinicMachineSchema.ReadRequiredFix(
                root, "env_mod_max", "根", ConstantsFileName, errors, out envMax);

            if (hasMin)
                ClinicMachineSchema.AssertEnvModMinNegative(envMin, errors);   // O-24-4

            if (hasMin && hasMax && envMin.Raw > envMax.Raw)
                errors.Add(
                    $"{ConstantsFileName}:ENV_MOD_MIN raw={envMin.Raw} > ENV_MOD_MAX raw={envMax.Raw} " +
                    "—— 21a F1 的钳制区间倒置(21a 侧缺陷,24 只读即报)");
        }

        /// <summary>base_env ∈ [ENV_MOD_MIN, ENV_MOD_MAX](规则三 / F-24-1;ENV 域 = 21a 常量)。</summary>
        private static void AssertBaseEnvDomain(
            ClinicMachineContextBinder.BindResult context, Fix envMin, Fix envMax, List<string> errors)
        {
            for (int i = 0; i < context.Rows.Count; i++)
            {
                ClinicMachineContextBinder.ContextRow row = context.Rows[i];
                if (row.BaseEnv.Raw < envMin.Raw || row.BaseEnv.Raw > envMax.Raw)
                {
                    errors.Add(
                        $"{ClinicMachineContextBinder.FileName}:contexts[{i}]" +
                        $"(room_type=\"{row.RoomType}\") base_env raw={row.BaseEnv.Raw} " +
                        $"∉ [ENV_MOD_MIN, ENV_MOD_MAX] raw=[{envMin.Raw}, {envMax.Raw}] " +
                        "—— 规则三 / F-24-1:base_env 的域 = 21a 的 ENV 域(不是 24 的 W 域)");
                }
            }
        }

        private static string ReadSource(string repoRoot, string fileName)
        {
            string path = Path.Combine(repoRoot, DataDirName, fileName);
            if (!File.Exists(path))
                throw new BakeValidationException(new List<string> { $"源文件不存在:{path}" });
            return File.ReadAllText(path);
        }
    }
}
