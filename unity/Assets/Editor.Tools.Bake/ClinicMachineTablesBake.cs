// Story 001 · clinic-machine —— 24 三表两阶段烘焙的**仓根装载器**。
//
// 权威来源:ADR-014 §三 / §五(两阶段烘焙入口 · ConfigVersion = 源数据集内容哈希派生)
//          · 三个 Binder(唯一构建期校验点)· ClinicMachineCookedWriter(编码面)
//
// ⚠️ 本件刻意**不碰 UnityEngine / Addressables**:纯文件 I/O + 绑定 + 编码
//   (承 DelayTable53Bake / DiagnosisReadFloorBaker 同纪律)⇒ EditMode 测试可直接驱动。
//
// ⚠️ 六文件齐备才做**跨表**断言(它们各自的结构 / 区间门在 Binder 内):
//   · O-24-4:21a 常量 ENV_MOD_MIN < 0(读 item_database_constants.json,**零硬编码**)
//   · base_env ∈ [ENV_MOD_MIN, ENV_MOD_MAX] 域校验(规则三 / F-24-1;ENV 域 = 21a 常量)
//   注意:ENV 域 = [−1/2, +1/2] 是 **21a 的 ENV 域**,**不是** 24 的 W 域([−1/4, +1/4])—— 别串。
//   · 两张词表覆盖:ROOM_NAMES ⊇ 表内全部 room_type · CONTEXT_NAMES ⊇ 表内全部 context_id
//     (2026-10-26 裁定 ①-C / ②-C —— 缺项会让该房间 / 情境**投不出编号** = 静默失效)。
//     ⚠️ 方向是**单向 ⊇**:词表允许有表内暂未用到的条目(为 P1a 预留编号),反向不查。
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

        /// <summary>烘焙输出(五份 cooked 字节 + 绑定结果)。</summary>
        public readonly struct BakeOutput
        {
            /// <summary><c>clinicmachine_context.cooked</c> 字节(含 20 字节头)。</summary>
            public readonly byte[] ContextCooked;

            /// <summary><c>clinicmachine_adj.cooked</c> 字节(含 20 字节头)。</summary>
            public readonly byte[] AdjCooked;

            /// <summary><c>clinicmachine_room.cooked</c> 字节(含 20 字节头)。</summary>
            public readonly byte[] RoomCooked;

            /// <summary><c>clinicmachine_room_names.cooked</c> 字节(含 20 字节头;裁定 ①-C)。</summary>
            public readonly byte[] RoomNamesCooked;

            /// <summary><c>clinicmachine_context_names.cooked</c> 字节(含 20 字节头;裁定 ②-C)。</summary>
            public readonly byte[] ContextNamesCooked;

            /// <summary>源数据集内容哈希派生的 ConfigVersion(u32)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>CONTEXT_TABLE 绑定结果(含表派生 <c>K_CONTEXT_MAX</c>)。</summary>
            public readonly ClinicMachineContextBinder.BindResult Context;

            /// <summary>ADJ_TABLE 绑定结果。</summary>
            public readonly ClinicMachineAdjBinder.BindResult Adj;

            /// <summary>ROOM_TABLE 绑定结果(含缺口哨兵)。</summary>
            public readonly ClinicMachineRoomBinder.BindResult Room;

            /// <summary>房间名词表绑定结果。</summary>
            public readonly ClinicMachineRoomNamesBinder.BindResult RoomNames;

            /// <summary>情境词表绑定结果。</summary>
            public readonly ClinicMachineContextNamesBinder.BindResult ContextNames;

            /// <summary>构造烘焙输出。</summary>
            public BakeOutput(
                byte[] contextCooked, byte[] adjCooked, byte[] roomCooked,
                byte[] roomNamesCooked, byte[] contextNamesCooked, uint configVersion,
                ClinicMachineContextBinder.BindResult context,
                ClinicMachineAdjBinder.BindResult adj,
                ClinicMachineRoomBinder.BindResult room,
                ClinicMachineRoomNamesBinder.BindResult roomNames,
                ClinicMachineContextNamesBinder.BindResult contextNames)
            {
                ContextCooked = contextCooked; AdjCooked = adjCooked; RoomCooked = roomCooked;
                RoomNamesCooked = roomNamesCooked; ContextNamesCooked = contextNamesCooked;
                ConfigVersion = configVersion;
                Context = context; Adj = adj; Room = room;
                RoomNames = roomNames; ContextNames = contextNames;
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
            string roomNamesJson = ReadSource(repoRoot, ClinicMachineRoomNamesBinder.FileName);
            string contextNamesJson = ReadSource(repoRoot, ClinicMachineContextNamesBinder.FileName);
            string constantsJson = ReadSource(repoRoot, ConstantsFileName);

            return Bake(
                contextJson, adjJson, roomJson, roomNamesJson, contextNamesJson, constantsJson,
                furnTypeSource);
        }

        /// <summary>从 JSON 串烘焙(**不落盘**;与 <see cref="BakeFromRepo"/> 同一机器)—— 测试负例入口。
        /// <para>⚠️ 词表两串是**必填** —— 不提供「跳过词表」的重载:那会是一条绕过跨表覆盖门的路径,
        /// 而该门正是词表存在的理由(缺项 ⇒ 编号投不出来 = 静默失效)。</para></summary>
        /// <exception cref="BakeValidationException">任一构建期拒收(聚合)。</exception>
        public static BakeOutput BakeFromJson(
            string contextJson, string adjJson, string roomJson,
            string roomNamesJson, string contextNamesJson, string constantsJson)
            => Bake(
                contextJson, adjJson, roomJson, roomNamesJson, contextNamesJson, constantsJson,
                null);

        /// <summary>从 JSON 串烘焙(可注入闭集来源)。</summary>
        /// <exception cref="BakeValidationException">任一构建期拒收(聚合)。</exception>
        public static BakeOutput BakeFromJson(
            string contextJson, string adjJson, string roomJson,
            string roomNamesJson, string contextNamesJson, string constantsJson,
            IFurnTypeSource furnTypeSource)
            => Bake(
                contextJson, adjJson, roomJson, roomNamesJson, contextNamesJson, constantsJson,
                furnTypeSource);

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

        /// <summary>绑定入口(房间名词表单表)。</summary>
        public static ClinicMachineRoomNamesBinder.BindResult BindRoomNamesForTest(string json)
            => ClinicMachineRoomNamesBinder.Bind(json);

        /// <summary>绑定入口(情境词表单表)。</summary>
        public static ClinicMachineContextNamesBinder.BindResult BindContextNamesForTest(string json)
            => ClinicMachineContextNamesBinder.Bind(json);

        // ════════════════════════════════════════════════════════════════════
        // 内部
        // ════════════════════════════════════════════════════════════════════

        private static BakeOutput Bake(
            string contextJson, string adjJson, string roomJson,
            string roomNamesJson, string contextNamesJson, string constantsJson,
            IFurnTypeSource furnTypeSource)
        {
            // 五表各自的结构 / 区间门在 Binder 内(任一失败即聚合 throw)。
            ClinicMachineContextBinder.BindResult context = ClinicMachineContextBinder.Bind(contextJson);
            ClinicMachineAdjBinder.BindResult adj = ClinicMachineAdjBinder.Bind(adjJson);
            ClinicMachineRoomBinder.BindResult room = ClinicMachineRoomBinder.Bind(roomJson, furnTypeSource);
            ClinicMachineRoomNamesBinder.BindResult roomNames =
                ClinicMachineRoomNamesBinder.Bind(roomNamesJson);
            ClinicMachineContextNamesBinder.BindResult contextNames =
                ClinicMachineContextNamesBinder.Bind(contextNamesJson);

            // 跨表断言(六文件齐备才可做)
            var crossErrors = new List<string>();
            BindConstants(constantsJson, crossErrors, out Fix envMin, out Fix envMax);

            if (crossErrors.Count == 0)
                AssertBaseEnvDomain(context, envMin, envMax, crossErrors);

            // 词表覆盖门(不依赖常量表 —— 与上面两条独立,故不受 crossErrors 早退影响)
            AssertRoomNameCoverage(context, room, roomNames, crossErrors);
            AssertContextNameCoverage(context, contextNames, crossErrors);

            if (crossErrors.Count > 0) throw new BakeValidationException(crossErrors);

            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(ClinicMachineContextBinder.FileName, contextJson),
                new KeyValuePair<string, string>(ClinicMachineAdjBinder.FileName, adjJson),
                new KeyValuePair<string, string>(ClinicMachineRoomBinder.FileName, roomJson),
                new KeyValuePair<string, string>(ClinicMachineRoomNamesBinder.FileName, roomNamesJson),
                new KeyValuePair<string, string>(ClinicMachineContextNamesBinder.FileName, contextNamesJson),
                new KeyValuePair<string, string>(ConstantsFileName, constantsJson),
            });

            return new BakeOutput(
                ClinicMachineCookedWriter.WriteContext(context, configVersion),
                ClinicMachineCookedWriter.WriteAdj(adj, configVersion),
                ClinicMachineCookedWriter.WriteRoom(room, configVersion),
                ClinicMachineCookedWriter.WriteRoomNames(roomNames, configVersion),
                ClinicMachineCookedWriter.WriteContextNames(contextNames, configVersion),
                configVersion, context, adj, room, roomNames, contextNames);
        }

        /// <summary>ROOM_NAMES 覆盖门:表内出现的每个 <c>room_type</c> 都须在词表里(裁定 ①-C)。
        /// <para>⚠️ 方向 = **单向 ⊇**(词表可有表内暂未用到的条目,为 P1a 预留编号)。</para>
        /// <para>⚠️ 取并集 <c>ROOM_TABLE ∪ CONTEXT_TABLE</c> 的 room_type —— 两表共用同一词空间
        /// (<c>ClinicRoomNamesBinder</c> 头注),任一侧缺编号都会在投影时落到 ROOM_NONE。</para></summary>
        private static void AssertRoomNameCoverage(
            ClinicMachineContextBinder.BindResult context,
            ClinicMachineRoomBinder.BindResult room,
            ClinicMachineRoomNamesBinder.BindResult names,
            List<string> errors)
        {
            var known = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < names.Rows.Count; i++) known.Add(names.Rows[i].RoomType);

            // ROOM_TABLE 侧:每个房间定义都须有编号
            for (int i = 0; i < room.Rooms.Count; i++)
            {
                string roomType = room.Rooms[i].RoomType;
                if (known.Contains(roomType)) continue;
                errors.Add(
                    $"{ClinicMachineRoomNamesBinder.FileName}:ROOM_TABLE 的 room_type「{roomType}」" +
                    "在词表里无编号 —— 该房间投影时只能落到 ROOM_NONE(静默错读);" +
                    $"要么在 {ClinicMachineRoomNamesBinder.FileName} 追加一条(新编号,不得复用已删编号)," +
                    "要么把该房间从 ROOM_TABLE 移除");
            }

            // CONTEXT_TABLE 侧:每个情境行所属房间类型也须有编号(两表同词空间)
            for (int i = 0; i < context.Rows.Count; i++)
            {
                string roomType = context.Rows[i].RoomType;
                if (roomType == null || known.Contains(roomType)) continue;
                errors.Add(
                    $"{ClinicMachineRoomNamesBinder.FileName}:CONTEXT_TABLE 的 room_type「{roomType}」" +
                    "在词表里无编号 —— 该情境行的房间名投影不出(静默错读)");
            }
        }

        /// <summary>CONTEXT_NAMES 覆盖门:CONTEXT_TABLE 出现的每个 <c>context_id</c> 都须在词表里(裁定 ②-C)。
        /// <para>⚠️ 方向 = **单向 ⊇**(同 <see cref="AssertRoomNameCoverage"/>)。
        /// 空 <c>context_id</c> 不在本门判据内 —— 那是 Binder 的结构门。</para></summary>
        private static void AssertContextNameCoverage(
            ClinicMachineContextBinder.BindResult context,
            ClinicMachineContextNamesBinder.BindResult names,
            List<string> errors)
        {
            var known = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < names.Rows.Count; i++) known.Add(names.Rows[i].ContextId);

            for (int i = 0; i < context.Rows.Count; i++)
            {
                string contextId = context.Rows[i].ContextId;
                if (contextId == null || known.Contains(contextId)) continue;
                errors.Add(
                    $"{ClinicMachineContextNamesBinder.FileName}:CONTEXT_TABLE 的 context_id「{contextId}」" +
                    "(room_type=\"" + context.Rows[i].RoomType + "\")在词表里无编号 —— " +
                    "该情境投影不出来(静默错读);" +
                    $"要么在 {ClinicMachineContextNamesBinder.FileName} 追加一条(新编号,不得复用已删编号)");
            }
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
