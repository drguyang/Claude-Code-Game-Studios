// 权威来源:clinic-machine.md O-24-3 补注(**OQ-CP-4 裁定 = 甲,2026-09-22**):
//   24 → 42 的只读源具名 = ClinicEnvDto,查询接口 IClinicEnvQuery.GetEnv(room) → ClinicEnvDto,
//   住 Sim.Contracts,与 VitalsDto 同程序集同浮点出口纪律;
//   字段集 = roomName(词表索引)/ contexts[](情境词表索引,可空集)/ envMod / equipMod
//   (均 Q16.16 raw,envMod **未钳制房间分量**、equipMod **非负**)。**零 disease_id、零 float 字段**。
//
// 转录口径(承 b1b 支 1 裁定批):
// · envMod / equipMod 用 Fix(1-a「Fix raw ⇒ 内存用 Fix」);O-24-4 承重前提 ENV_MOD_MIN < 0
//   由 24 的数据门守,本契约只载值;
// · contexts[] 用 int[](支 4-a:情境词表 size 全库未定 ⇒ 不造 bitmask;本 DTO 非流事件,
//   数组不触支 0 的 header/blob 纪律;「可空集」= 空数组非 null);
// · GetEnv(room) 的 room 类型 registry 未点名(4-c):取 int(24 的房间是整数格连通块导出的
//   房号,ROOM_NONE = 0 是合法输入 → 中性 (0,0),EC-24-01/02)。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>医馆环境读数 DTO(24 派生,42 只渲染不持有 —— O-24-3)。</summary>
    public readonly struct ClinicEnvDto
    {
        /// <summary>房间名字典 ordinal(词表索引;ROOM_NONE 亦占条目)。</summary>
        public readonly int RoomNameId;
        /// <summary>情境词表索引,可空集(空数组 = 「印卸 = 不成立」,tutorial-48 面板语义)。</summary>
        public readonly int[] ContextIds;
        /// <summary>环境乘子,Q16.16(未钳制房间分量;下限 < 0 是承重前提 O-24-4)。</summary>
        public readonly Fix EnvMod;
        /// <summary>装备乘子,Q16.16(非负,只上扬)。</summary>
        public readonly Fix EquipMod;

        public ClinicEnvDto(int roomNameId, int[] contextIds, Fix envMod, Fix equipMod)
        {
            RoomNameId = roomNameId; ContextIds = contextIds; EnvMod = envMod; EquipMod = equipMod;
        }
    }

    /// <summary>24 交付的只读查询接口(OQ-CP-4 甲)。同步可读,无延迟承诺(AC-24-09 已迁 42)。</summary>
    public interface IClinicEnvQuery
    {
        ClinicEnvDto GetEnv(int roomId);
    }
}
