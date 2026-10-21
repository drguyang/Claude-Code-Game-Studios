// 权威来源:ADR-014 §二/§五(运行期读烘焙产物)· CookedFormat.cs(布局契约)
//          写方严格镜像:`Editor.Tools.Bake/ClinicMachineCookedWriter.cs`
//          表构造 / 结构性拒收的**权威出处**:`DaYiJingCheng.Sim.Clinic.{ClinicContextTable,
//          ClinicAdjTable,ClinicRoomTable}`(构造期拒收的真实判据住在那里)。
//
// ⚠️ 本件**复做 4 类字节级校验**(2026-10-21 订正,评审 D6):
//    ① 空表 / 空条目 / 空合取拒收(三个 Read* 各自的 `须 ≥ 1` 守卫)
//    ② 重复键拒收(room_type 去重 · 有向邻接键去重 —— 后者用 `\u0001` 分隔,防串键)
//    ③ 长度耗尽校验(`CookedReadUtil.AssertFullyConsumed`,CONTEXT / ROOM 各有一次)
//    ④ 表构造拒收 → 包成 `InvalidDataException`(三个 Read* 各自的 `表构造被拒`)
//    这些**不是**「重做表构造校验」,而是**解码期**的纵深防御:字节流可能被截断 /
//    被旧版本写入 / 被篡改,而这些形态**进不到**构造器(类型系统看不到字节)。
//    与构造器重复的部分(域端点 / 对称性 / n_max)**只在构造器里做一次** —— 那才是
//    「单一出处」,本件不复制它们的判据。
//
// ⚠️ 载荷布局(全部 little-endian;字符串 = i32 字节长 + UTF-8,长度 -1 = null):
//    CONTEXT: i64 k_context_max_raw; i32 row_count;
//             row_count × { string room_type; string context_id; i64 k_speed_raw; i64 base_env_raw }
//    ADJ    : i64 w_min_raw; i64 w_max_raw; i64 n_max; i32 pair_count;
//             pair_count × { string a; string b; i64 w_raw }
//    ROOM   : i32 room_count;
//             room_count × { string room_type; i32 clause_count;
//                            clause_count × { u8 kind; string type; i32 min; string neighbor_type; u8 direction } }
//
// ⚠️ 行序 = 写方的全序排序结果(声明序不承载语义);读方**不依赖**读入序做任何计算。
//
// ⚠️ 任何字段缺失 / 魔数不符 / 长度不符 / 构造拒收 ⇒ 显式 throw(启动期硬失败,E-13 口径)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Clinic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>24 医馆三表(<c>clinicmachine_{context,adj,room}.cooked.bytes</c>)的运行期解码器。
    /// <para>产物解码后直接物化为 Sim 侧表类型(构造期拒收在此被包成启动期硬失败)。</para>
    /// </summary>
    public static class ClinicMachineCookedCodec
    {
        /// <summary>读 CONTEXT_TABLE。</summary>
        /// <exception cref="InvalidDataException">头部损坏 / 长度不符 / 行数 &lt; 1 / 表构造拒收
        /// (空 room_type · 重复 · K_speed 域外 · K_CONTEXT_MAX 与重算值不符)—— 启动期硬失败。</exception>
        public static ClinicContextDataSet ReadContext(byte[] bytes)
        {
            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, "clinicmachine_context", out uint configVersion))
            {
                long kContextMaxRaw = r.ReadInt64();
                int rowCount = r.ReadInt32();
                if (rowCount < 1)
                    throw new InvalidDataException(
                        $"[cooked] clinicmachine_context 行数 = {rowCount} —— 须 ≥ 1" +
                        "(空表 ⇒ 全部房间退回默认情境 = 静默失效;E-13)");

                var rows = new ClinicContextRow[rowCount];
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < rowCount; i++)
                {
                    string roomType = CookedReadUtil.ReadString32(r, $"clinicmachine_context rows[{i}].room_type");
                    string contextId = CookedReadUtil.ReadString32(r, $"clinicmachine_context rows[{i}].context_id");
                    long kSpeedRaw = r.ReadInt64();
                    long baseEnvRaw = r.ReadInt64();

                    if (roomType != null && !seen.Add(roomType))
                        throw new InvalidDataException(
                            $"[cooked] clinicmachine_context room_type「{roomType}」重复 —— 产物损坏(E-13)");

                    rows[i] = new ClinicContextRow(roomType, contextId, new Fix(kSpeedRaw), new Fix(baseEnvRaw));
                }

                CookedReadUtil.AssertFullyConsumed(r, "clinicmachine_context");

                try
                {
                    var table = new ClinicContextTable(rows, new Fix(kContextMaxRaw));
                    return new ClinicContextDataSet(table, configVersion);
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidDataException(
                        $"[cooked] clinicmachine_context 表构造被拒:{ex.Message}", ex);
                }
            }
        }

        /// <summary>读 ADJ_TABLE。</summary>
        /// <exception cref="InvalidDataException">头部损坏 / 长度不符 / 条目为空 / W 域端点非法 /
        /// 对称性缺失(AC-24-06)—— 启动期硬失败。</exception>
        public static ClinicAdjDataSet ReadAdj(byte[] bytes)
        {
            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, "clinicmachine_adj", out uint configVersion))
            {
                long wMinRaw = r.ReadInt64();
                long wMaxRaw = r.ReadInt64();
                long nMax = r.ReadInt64();
                int pairCount = r.ReadInt32();
                if (pairCount < 1)
                    throw new InvalidDataException(
                        $"[cooked] clinicmachine_adj 条目 = {pairCount} —— 须 ≥ 1" +
                        "(空表 ⇒ adj_sum 恒 0 = 整表静默失效;E-13)");

                var pairs = new ClinicAdjPair[pairCount];
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < pairCount; i++)
                {
                    string a = CookedReadUtil.ReadString32(r, $"clinicmachine_adj pairs[{i}].a");
                    string b = CookedReadUtil.ReadString32(r, $"clinicmachine_adj pairs[{i}].b");
                    long wRaw = r.ReadInt64();

                    // 有向键去重(重复键 ⇒「取哪一行」成为隐式规则)— 与烘焙侧同一判据。
                    string key = a + "\u0001" + b;
                    if (!seen.Add(key))
                        throw new InvalidDataException(
                            $"[cooked] clinicmachine_adj 重复有向键 (\"{a}\",\"{b}\") —— 产物损坏(E-13)");

                    pairs[i] = new ClinicAdjPair(a, b, new Fix(wRaw));
                }

                CookedReadUtil.AssertFullyConsumed(r, "clinicmachine_adj");

                try
                {
                    var table = new ClinicAdjTable(pairs, nMax, new Fix(wMinRaw), new Fix(wMaxRaw));
                    return new ClinicAdjDataSet(table, configVersion);
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidDataException(
                        $"[cooked] clinicmachine_adj 表构造被拒:{ex.Message}", ex);
                }
            }
        }

        /// <summary>读 ROOM_TABLE。</summary>
        /// <exception cref="InvalidDataException">头部损坏 / 长度不符 / 房间为空 / 子句字段非法
        /// (闭集外 kind / direction · 空合取)—— 启动期硬失败。</exception>
        public static ClinicRoomDataSet ReadRoom(byte[] bytes)
        {
            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, "clinicmachine_room", out uint configVersion))
            {
                int roomCount = r.ReadInt32();
                if (roomCount < 1)
                    throw new InvalidDataException(
                        $"[cooked] clinicmachine_room 房间类型 = {roomCount} —— 须 ≥ 1" +
                        "(空表 ⇒ 全部布局恒 ROOM_NONE = 静默失效;E-13)");

                var rooms = new ClinicRoomDef[roomCount];
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < roomCount; i++)
                {
                    string roomType = CookedReadUtil.ReadString32(r, $"clinicmachine_room rooms[{i}].room_type");
                    if (roomType != null && !seen.Add(roomType))
                        throw new InvalidDataException(
                            $"[cooked] clinicmachine_room room_type「{roomType}」重复 —— 产物损坏(E-13)");

                    int clauseCount = r.ReadInt32();
                    if (clauseCount < 1)
                        throw new InvalidDataException(
                            $"[cooked] clinicmachine_room「{roomType}」clauses = {clauseCount} —— 须 ≥ 1" +
                            "(空合取 = 恒匹配,整馆恒判为该类型;E-13)");

                    var clauses = new ClinicRoomClause[clauseCount];
                    for (int c = 0; c < clauseCount; c++)
                    {
                        string where = $"clinicmachine_room rooms[{i}].clauses[{c}]";
                        var kind = (ClinicRoomClauseKind)r.ReadByte();
                        string type = CookedReadUtil.ReadString32(r, where + ".type");
                        int min = r.ReadInt32();
                        string neighborType = CookedReadUtil.ReadString32(r, where + ".neighbor_type");
                        var direction = (ClinicRoomClauseDirection)r.ReadByte();

                        clauses[c] = new ClinicRoomClause(kind, type, neighborType, direction, min);
                    }

                    rooms[i] = new ClinicRoomDef(roomType, Array.AsReadOnly(clauses));
                }

                CookedReadUtil.AssertFullyConsumed(r, "clinicmachine_room");

                try
                {
                    var table = new ClinicRoomTable(rooms);
                    return new ClinicRoomDataSet(table, configVersion);
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidDataException(
                        $"[cooked] clinicmachine_room 表构造被拒:{ex.Message}", ex);
                }
            }
        }
    }

    /// <summary>CONTEXT_TABLE 烘焙产物的解码形。</summary>
    public struct ClinicContextDataSet
    {
        /// <summary>表(可直接交给 <see cref="ClinicContextTable.TryGetBaseEnv"/> 等查询)。</summary>
        public ClinicContextTable Table;

        /// <summary>烘焙时从源 JSON 文本派生的内容版本号(u32)。</summary>
        public uint ConfigVersion;

        /// <summary>构造解码形。</summary>
        public ClinicContextDataSet(ClinicContextTable table, uint configVersion)
        {
            Table = table; ConfigVersion = configVersion;
        }
    }

    /// <summary>ADJ_TABLE 烘焙产物的解码形。</summary>
    public struct ClinicAdjDataSet
    {
        /// <summary>表(可直接交给 <see cref="ClinicAdjTable.TryGetWeight"/> 查询)。</summary>
        public ClinicAdjTable Table;

        /// <summary>烘焙时从源 JSON 文本派生的内容版本号(u32)。</summary>
        public uint ConfigVersion;

        /// <summary>构造解码形。</summary>
        public ClinicAdjDataSet(ClinicAdjTable table, uint configVersion)
        {
            Table = table; ConfigVersion = configVersion;
        }
    }

    /// <summary>ROOM_TABLE 烘焙产物的解码形。</summary>
    public struct ClinicRoomDataSet
    {
        /// <summary>表(可直接交给 <see cref="ClinicRoomTable.TryGetRoom"/> 查询)。</summary>
        public ClinicRoomTable Table;

        /// <summary>烘焙时从源 JSON 文本派生的内容版本号(u32)。</summary>
        public uint ConfigVersion;

        /// <summary>构造解码形。</summary>
        public ClinicRoomDataSet(ClinicRoomTable table, uint configVersion)
        {
            Table = table; ConfigVersion = configVersion;
        }
    }
}
