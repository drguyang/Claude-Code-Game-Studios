// 权威来源:
//   GDD `design/gdd/clinic-machine.md` :340「字段集 = roomName(词表索引)/ contexts[](情境词表索引)」
//   `Sim.Contracts/ClinicEnvDto.cs` :9 / :20(「支 4-a:情境词表 size 全库未定」——
//      **本文件即该 size 的答案**:不是 bitmask,是显式词表的稳定 int 编号)
//   **2026-10-26 用户裁定 ①-C / ②-C / ③-B**:两张显式词表文件;`ROOM_NONE = 0`;真实房间从 1 起。
//   ADR-014 §二/§五(作者态 JSON → 构建期烘焙 `*.cooked` → 运行期只读产物)
//   写方镜像:`Editor.Tools.Bake/ClinicMachineCookedWriter.Write{RoomNames,ContextNames}`
//   读方镜像:`Gameplay.Presentation/ClinicMachineCookedCodec.Read{RoomNames,ContextNames}`
//
// ⚠️ 住 Sim 程序集 `DaYiJingCheng.Sim.Clinic`(门 A / b2:引用集 = BCL + Sim.Contracts,零引擎)。
//
// ⚠️ **编号 ≠ 行序** —— 这是本文件存在的全部理由:
//   `ROOM_TABLE` / `CONTEXT_TABLE` 的行序由烘焙的 ordinal 排序决定,是**产物序**。
//   若用行序当 DTO 编号,新增一个 room_type 会让其后所有编号**移位** ⇒ 旧存档里的
//   `ClinicEnvDto.RoomNameId` 指向错误的房间名(静默错读,无异常、无日志)。
//   词表把编号**冻结在作者态**,新增只能追加。
//
// ⚠️ **0 不是「另一个 id」**:`ClinicRoomNameTable.NoneId = 0` 是 `ROOM_NONE` 的哨兵
//   (与 `ClinicRoomId.None = (0,0)` 对齐)。词表里**没有** id = 0 的条目 —— 构造器拒收,
//   烘焙侧 `ReadRequiredPositiveInt` 亦天然拒收(单机制,不靠另设 if)。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Sim.Clinic
{
    /// <summary>词表一行(键 + 稳定编号)。</summary>
    public readonly struct ClinicNameEntry
    {
        /// <summary>词(room_type 或 context_id,与对应表同一词空间)。</summary>
        public readonly string Name;

        /// <summary>稳定编号(≥ 1;0 保留给 `ROOM_NONE`)。</summary>
        public readonly int Id;

        /// <summary>构造一行。</summary>
        public ClinicNameEntry(string name, int id) { Name = name; Id = id; }
    }

    /// <summary>词表的双向查找核(两张词表共用的唯一实现)。
    /// <para>⚠️ 本类是 <c>internal</c> —— 对外只暴露 <see cref="ClinicRoomNameTable"/> /
    /// <see cref="ClinicContextNameTable"/> 两个具名类型,免得「room 的编号喂给 context 表」
    /// 这类串用成为可编译的。</para>
    /// </summary>
    internal sealed class ClinicNameTableCore
    {
        private readonly Dictionary<string, int> _idByName;
        private readonly Dictionary<int, string> _nameById;
        private readonly string _label;

        /// <summary>条目数。</summary>
        public int Count => _idByName.Count;

        /// <summary>构造并校验。</summary>
        /// <param name="entries">条目集(构造期拷贝)。</param>
        /// <param name="label">报文用的表名(如 <c>ROOM_NAMES</c>)。</param>
        /// <exception cref="ArgumentException">① 条目为空(空词表让全部投影落到 None = 静默失效)
        /// ② 键为空 / 重复 ③ id &lt; 1(0 保留给 None)④ id 重复(两词争同一编号 ⇒ 投影二义)。</exception>
        public ClinicNameTableCore(IReadOnlyList<ClinicNameEntry> entries, string label)
        {
            _label = label;

            if (entries == null || entries.Count == 0)
                throw new ArgumentException(
                    $"{label} 条目为 0 —— 空词表让全部投影落到 None = 静默失效");

            _idByName = new Dictionary<string, int>(entries.Count, StringComparer.Ordinal);
            _nameById = new Dictionary<int, string>(entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                ClinicNameEntry e = entries[i];

                if (string.IsNullOrEmpty(e.Name))
                    throw new ArgumentException($"{label} 第 {i} 条 Name 为空 —— 拒收");

                // 0 保留给 None:词表里出现 0 会让「无房间」与「某个真房间」不可区分。
                if (e.Id < 1)
                    throw new ArgumentException(
                        $"{label} 条目「{e.Name}」id={e.Id} < 1 —— " +
                        "0 保留给 None 哨兵(裁定 ③-B),真实条目须 ≥ 1");

                if (_idByName.ContainsKey(e.Name))
                    throw new ArgumentException(
                        $"{label} Name「{e.Name}」重复 —— 一词一编号");

                if (_nameById.ContainsKey(e.Id))
                    throw new ArgumentException(
                        $"{label} id={e.Id} 重复(「{e.Name}」与「{_nameById[e.Id]}」争同一编号)—— " +
                        "投影会二义");

                _idByName.Add(e.Name, e.Id);
                _nameById.Add(e.Id, e.Name);
            }
        }

        /// <summary>词 → 编号。</summary>
        /// <returns>命中 ⇒ true;否则 false(不抛 —— 未知词是调用方的正常分支)。</returns>
        public bool TryGetId(string name, out int id)
        {
            if (name != null && _idByName.TryGetValue(name, out id)) return true;
            id = 0;
            return false;
        }

        /// <summary>编号 → 词。</summary>
        /// <returns>命中 ⇒ true;否则 false(<b>0 恒 false</b> —— 它不是词表条目)。</returns>
        public bool TryGetName(int id, out string name)
            => _nameById.TryGetValue(id, out name);
    }

    /// <summary>房间名词表(<c>room_type</c> ↔ <c>RoomNameId</c>;裁定 ①-C + ③-B)。
    /// <para>⚠️ 本表**不含** <see cref="NoneId"/> 条目 —— 0 是哨兵,不是「另一个房间」。</para>
    /// </summary>
    public sealed class ClinicRoomNameTable
    {
        /// <summary>`ROOM_NONE` 的编号(裁定 ③-B:0;真实房间从 1 起)。</summary>
        public const int NoneId = 0;

        private readonly ClinicNameTableCore _core;

        /// <summary>条目数(不含 <see cref="NoneId"/>)。</summary>
        public int Count => _core.Count;

        /// <summary>构造并校验。</summary>
        /// <exception cref="ArgumentException">空表 / 键为空或重复 / id &lt; 1 或重复。</exception>
        public ClinicRoomNameTable(IReadOnlyList<ClinicNameEntry> entries)
            => _core = new ClinicNameTableCore(entries, "ROOM_NAMES");

        /// <summary>room_type → RoomNameId。</summary>
        public bool TryGetId(string roomType, out int roomNameId)
            => _core.TryGetId(roomType, out roomNameId);

        /// <summary>RoomNameId → room_type。</summary>
        public bool TryGetRoomType(int roomNameId, out string roomType)
            => _core.TryGetName(roomNameId, out roomType);
    }

    /// <summary>情境词表(<c>context_id</c> ↔ <c>ContextNameId</c>;裁定 ②-C)。
    /// <para>⚠️ **空集语义不在本表**:`ClinicEnvDto.ContextIds` 为空数组表示「印卸 = 不成立」
    /// (tutorial-48 面板),**不是**本表里的某个 id —— 本表不设「空」条目。</para>
    /// </summary>
    public sealed class ClinicContextNameTable
    {
        private readonly ClinicNameTableCore _core;

        /// <summary>条目数。</summary>
        public int Count => _core.Count;

        /// <summary>构造并校验。</summary>
        /// <exception cref="ArgumentException">空表 / 键为空或重复 / id &lt; 1 或重复。</exception>
        public ClinicContextNameTable(IReadOnlyList<ClinicNameEntry> entries)
            => _core = new ClinicNameTableCore(entries, "CONTEXT_NAMES");

        /// <summary>context_id → ContextNameId。</summary>
        public bool TryGetId(string contextId, out int contextNameId)
            => _core.TryGetId(contextId, out contextNameId);

        /// <summary>ContextNameId → context_id。</summary>
        public bool TryGetContextId(int contextNameId, out string contextId)
            => _core.TryGetName(contextNameId, out contextId);
    }
}
