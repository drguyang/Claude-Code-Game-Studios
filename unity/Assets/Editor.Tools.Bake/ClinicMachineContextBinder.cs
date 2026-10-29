// Story 001 · clinic-machine —— CONTEXT_TABLE(assets/data/clinicmachine_context.json)的阶段 2 绑定器。
//
// 权威来源:
//   ADR-014 §三(阶段 2:已知键白名单 · FixParse · schema_version · 聚合 throw)· §四(Fix 字段必须字符串)
//   GDD design/gdd/clinic-machine.md 规则三 / F-24-3:
//     · 每项 K_speed ∈ (0,1](AC-24-05;失败通道 = 显式 throw,承 ADR-022 §三③,非 Debug.Assert)
//     · K_CONTEXT_MAX = max(K_speed) 由表派生(禁裸常数;交付系统 1 的 F-1-8 / O-2)
//   OQ-24-1(2026-09-25):K_speed **档集 = {1, 7/8, 3/4}**
//     卡面 deliverable 2:档集外一律走**同一条**规则判 schema 错(单机制,不另设分支)。
//     值域 (0,1] 由档集蕴含 ⇒ 不再单设「(0,1] 区间」分支(两套判据 = 两处真源)。
//
// ⚠️ 本件是 CONTEXT_TABLE 的**唯一构建期校验点**;K_CONTEXT_MAX 的派生也在本件(单一出处)。
// ⚠️ base_env 的 [ENV_MOD_MIN, ENV_MOD_MAX] 域校验**不在此处** —— 该域常量归 21a
//   (item_database_constants.json),由 ClinicMachineTablesBake 在四文件齐备时断言(见该类)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>clinicmachine_context.json</c> 的阶段 2 绑定器(AC-24-05 + K_CONTEXT_MAX 派生)。</summary>
    public static class ClinicMachineContextBinder
    {
        /// <summary>源文件名(单一出处;烘焙装载器 / 菜单 / 测试共用)。</summary>
        public const string FileName = "clinicmachine_context.json";

        /// <summary>一行房情境。</summary>
        public readonly struct ContextRow
        {
            /// <summary>房间类型 id(词表索引;与 ROOM_TABLE 的 <c>room_type</c> 对齐)。</summary>
            public readonly string RoomType;

            /// <summary>情境 id(如「手术室 · 无菌」)。</summary>
            public readonly string ContextId;

            /// <summary>房间格情境乘数(∈ 档集 {1, 7/8, 3/4})。</summary>
            public readonly Fix KSpeed;

            /// <summary>房间基础环境(∈ [ENV_MOD_MIN, ENV_MOD_MAX];域校验在 TablesBake)。</summary>
            public readonly Fix BaseEnv;

            /// <summary>构造一行(测试 / 物化用)。</summary>
            public ContextRow(string roomType, string contextId, Fix kSpeed, Fix baseEnv)
            {
                RoomType = roomType; ContextId = contextId; KSpeed = kSpeed; BaseEnv = baseEnv;
            }
        }

        /// <summary>绑定结果。</summary>
        public readonly struct BindResult
        {
            /// <summary>行集(声明序;编码时另行全序排序)。</summary>
            public readonly IReadOnlyList<ContextRow> Rows;

            /// <summary>表派生导出值 = max(K_speed)(F-24-3;交付系统 1)。</summary>
            public readonly Fix KContextMax;

            /// <summary>源 schema_version(写方头槽 offset 8 的唯一真值来源)。</summary>
            public readonly uint SchemaVersion;

            /// <summary>构造绑定结果。</summary>
            public BindResult(IReadOnlyList<ContextRow> rows, Fix kContextMax, uint schemaVersion)
            {
                Rows = rows; KContextMax = kContextMax; SchemaVersion = schemaVersion;
            }
        }

        /// <summary>绑定 + AC-24-05 构建期拒收(任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>)。</summary>
        /// <exception cref="BakeValidationException">词法失败 / 任一校验失败(聚合后一次 throw)。</exception>
        public static BindResult Bind(string json)
        {
            var errors = new List<string>();

            JsonNode root = ClinicMachineSchema.BeginTable(
                json, ClinicMachineSchema.ContextRootKeys, FileName, errors, out uint schemaVersion);

            // ⚠️ 早 throw 豁免点(同 DelayTableBinder):无 contexts 数组 ⇒ 行层校验结构上不可达。
            if (!root.TryGet("contexts", out JsonNode rowsNode) || rowsNode.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{FileName}:缺 contexts 数组");
                throw new BakeValidationException(errors);
            }

            var rows = new List<ContextRow>();
            var seenRoomTypes = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < rowsNode.Items.Count; i++)
            {
                ContextRow? row = ParseRow(rowsNode.Items[i], i, seenRoomTypes, errors);
                if (row.HasValue) rows.Add(row.Value);
            }

            if (rows.Count == 0 && errors.Count == 0)
                errors.Add($"{FileName}:contexts 为空 —— 空表无法派生 K_CONTEXT_MAX(F-24-3),拒");

            if (errors.Count > 0) throw new BakeValidationException(errors);

            Fix kContextMax = rows[0].KSpeed;
            for (int i = 1; i < rows.Count; i++)
                if (rows[i].KSpeed.Raw > kContextMax.Raw) kContextMax = rows[i].KSpeed;

            return new BindResult(rows, kContextMax, schemaVersion);
        }

        /// <summary>逐行解析(白名单 + K_speed 档集;重复 room_type 拒)。</summary>
        private static ContextRow? ParseRow(
            JsonNode row, int i, HashSet<string> seenRoomTypes, List<string> errors)
        {
            string where = $"contexts[{i}]";
            if (!ClinicMachineSchema.BeginRow(
                    row, where, ClinicMachineSchema.ContextRowKeys, FileName, errors))
                return null;

            bool ok = ClinicMachineSchema.ReadRequiredString(
                row, "room_type", where, FileName, errors, out string roomType);
            ok &= ClinicMachineSchema.ReadRequiredString(
                row, "context_id", where, FileName, errors, out string contextId);

            Fix kSpeed = Fix.Zero;
            if (!ReadKSpeed(row, where, roomType, errors, out kSpeed)) ok = false;

            Fix baseEnv = Fix.Zero;
            if (ClinicMachineSchema.ReadRequiredFix(
                    row, "base_env", where, FileName, errors, out Fix parsedBaseEnv))
                baseEnv = parsedBaseEnv;
            else
                ok = false;

            if (!ok) return null;

            if (!seenRoomTypes.Add(roomType))
            {
                errors.Add($"{FileName}:{where} room_type「{roomType}」重复 —— 每房间类型恰一情境行");
                return null;
            }

            return new ContextRow(roomType, contextId, kSpeed, baseEnv);
        }

        /// <summary>`k_speed` 读取 + AC-24-05 档集白名单(档集外一律 schema 错,单机制无第二分支)。</summary>
        /// <remarks>🔁 <b>2026-10-29 拆分</b>(债册 `TD-clinic-22` / S16 普查残项)。报文原文逐字未变;
        /// <b>`ok = false` 的位置未动</b> —— 它仍只在该分支内被置假(承 `TD-clinic-11` / D4 的加固口径)。</remarks>
        private static bool ReadKSpeed(
            JsonNode row, string where, string roomType, List<string> errors, out Fix kSpeed)
        {
            kSpeed = Fix.Zero;
            if (!ClinicMachineSchema.ReadRequiredFix(
                    row, "k_speed", where, FileName, errors, out Fix parsedKSpeed))
                return false;

            kSpeed = parsedKSpeed;
            // AC-24-05:档集白名单单机制 —— 档集外(含 0 / 负 / 5/4 / 1/2)同一条规则。
            if (!ClinicMachineSchema.IsAllowedKSpeed(kSpeed.Raw))
            {
                // 🔁 2026-10-29(债册 TD-clinic-11 / 评审 D4):此前只 `errors.Add`,`ok` 未置 false
                //    ⇒ **非法行仍被塞进 `rows`**。当前无害(throw 发生在 `K_CONTEXT_MAX` 派生之前),
                //    但一旦将来把这条 throw 降级为告警,非法行就会污染派生集 ⇒ 派生值静默漂移。
                //    加固后不产生**今日**可观测差异(仍在同一处 throw),只堵住那条降级路径。
                //    ⚠️ 拆到本方法后,「置假」由**返回 false** 表达(调用方 `ok = false`),语义等价。
                errors.Add(
                    $"{FileName}:{where}(room_type=\"{roomType ?? "?"}\") k_speed raw={kSpeed.Raw} " +
                    $"∉ 档集 {{{ClinicMachineSchema.KSpeedLiteralsText()}}} —— AC-24-05 / F-24-3 / OQ-24-1:" +
                    "值域 (0,1] 由档集蕴含,档集外一律 schema 错(非「给它一个值」);" +
                    $"违例值 = \"{(row.TryGet("k_speed", out JsonNode kn) ? kn.Str : "?")}\"");
                return false;
            }

            return true;
        }

    }
}
