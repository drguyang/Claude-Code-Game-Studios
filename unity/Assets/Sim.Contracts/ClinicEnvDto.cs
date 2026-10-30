// 权威来源:clinic-machine.md O-24-3 补注(**OQ-CP-4 裁定 = 甲,2026-09-22**):
//   24 → 42 的只读源具名 = ClinicEnvDto,住 Sim.Contracts,与 VitalsDto 同程序集同浮点出口纪律;
//   字段集 = roomName(词表索引)/ contexts[](情境词表索引,可空集)/ envMod / equipMod
//   (均 Q16.16 raw,envMod **未钳制房间分量**、equipMod **非负**)。**零 disease_id、零 float 字段**。
//
// 🔁 **2026-10-30 窄修订(`TD-clinic-20` 结案 · 用户裁定丙案)**:**删掉 `IClinicEnvQuery`**。
//   原契约登记的是「查询接口 `IClinicEnvQuery.GetEnv(room)` → `ClinicEnvDto`」,实测两件事:
//   ① 该接口**零实装、零调用点**(`grep -rn ': IClinicEnvQuery' unity/Assets/ --include=*.cs`
//      ⇒ 0;`.GetEnv(` 生产侧 ⇒ 0)—— 它从出生起就没有生产物;
//   ② 它的签名**装不下真实的房间标识**:`GetEnv(int roomId)` 取单个 `int`,而
//      `Sim.Clinic.ClinicRoomId` 是 **64 位**(`High` / `Low` 两个 `int`,由布局规范排序格集
//      哈希派生)—— 32 位必然碰撞,碰撞会让两间不同房间取到同一 env。
//      ⚠️ 原文 `:17-18` 那句「24 的房间是整数格连通块导出的房号」**与实现不符**(见 `ClinicRoomId.cs:11-19`)。
//   ⇒ 42 的真实读面是 `ClinicEnvironmentMemo.GetDto()`(缓存 `ClinicEnvDto`,由
//   `ClinicEnvProjection.ToDto` 单点投影)。**承认它就是 ADR-025 §① 的契约通道**,
//   删掉那个从未存在过的接口 —— 留着一个没有生产物、且签名错误的契约,比没有契约更坏
//   (它会让下一个人以为「通道已定义,只差接线」)。
//   ⚠️ 残留:ADR-025 §① 的正文仍写着 `IClinicEnvQuery`,须同批窄修订(见债册 `TD-clinic-20` 结案段)。
//
// 转录口径(承 b1b 支 1 裁定批):
// · envMod / equipMod 用 Fix(1-a「Fix raw ⇒ 内存用 Fix」);O-24-4 承重前提 ENV_MOD_MIN < 0
//   由 24 的数据门守,本契约只载值;
// · contexts[] 用 int[](支 4-a:情境词表 size 全库未定 ⇒ 不造 bitmask;本 DTO 非流事件,
//   数组不触支 0 的 header/blob 纪律;「可空集」= 空数组非 null);
//   ✅ **2026-10-26 支 4-a 结案**(用户裁定 ①-C / ②-C / ③-B):size 由两张显式词表承载 ——
//   `assets/data/clinicmachine_{room,context}_names.json` → `*.cooked` → `Sim.Clinic.ClinicRoomNameTable`
//   / `ClinicContextNameTable`;**编号冻结在作者态,新增只能追加**(禁用产物行序当编号,那会让旧存档
//   编号移位)。`ROOM_NONE = 0` 是哨兵,真实条目从 1 起。**仍不造 bitmask**(数组形态不变)。
//   投影单点 = `ClinicEnvProjection.ToDto`;查不到编号 ⇒ throw,不降级为 None。详见 GDD `:340` 补注。
// · ~~GetEnv(room) 的 room 类型 registry 未点名(4-c):取 int(24 的房间是整数格连通块导出的
//   房号,ROOM_NONE = 0 是合法输入 → 中性 (0,0),EC-24-01/02)。~~
//   🔁 **2026-10-30 作废**(随 `IClinicEnvQuery` 一起删除,见上方窄修订):该注描述的是一个
//   **不存在的量** —— 真实房间标识是 `ClinicRoomId`(64 位布局哈希),不是「整数格连通块导出的房号」。
//   哨兵也不是「`ROOM_NONE = 0` 单个 int」,是 `ClinicRoomId.None = (0, 0)`。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>医馆环境读数 DTO(24 派生,42 只渲染不持有 —— O-24-3)。</summary>
    public readonly struct ClinicEnvDto
    {
        /// <summary>房间名词表索引;`ROOM_NONE = 0` 为**哨兵,不占词表条目**(真实房间从 1 起)。
        /// <para>⚠️ 2026-10-26 订正:原文本「ROOM_NONE 亦占条目」与实现相反 ——
        /// `ClinicRoomNameTable` 构造器对 `id &lt; 1` 直接拒,词表里**没有** 0 号条目
        /// (见 `ClinicNameTables.cs:78,113`)。承裁定 ③-B。</para></summary>
        public readonly int RoomNameId;
        /// <summary>情境词表索引,可空集(空数组 = 「印卸 = 不成立」,tutorial-48 面板语义)。
        /// <para>⚠️ **调用方不得修改返回的数组** —— `ClinicEnvironmentMemo.GetDto()` 命中时返回
        /// 缓存内的同一实例(数组是引用类型,`readonly` 只禁重绑不禁元素写入);
        /// 写入会静默污染缓存,违反 EC-24-05「缓存前后逐位一致」。
        /// 详见 `ClinicEnvironmentMemo.GetDto` 的 `<returns>`。</para></summary>
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

    // 🔁 **2026-10-30 删除 `IClinicEnvQuery`(`TD-clinic-20` 结案 · 用户裁定丙案)**。
    //   原文(留档):
    //     /// <summary>24 交付的只读查询接口(OQ-CP-4 甲)。同步可读,无延迟承诺(AC-24-09 已迁 42)。</summary>
    //     public interface IClinicEnvQuery { ClinicEnvDto GetEnv(int roomId); }
    //
    //   **删它的两条理由(均实测,不是偏好)**:
    //   ① **零生产物** —— 全仓零实装、零调用点。它不是「待接线」,是从未被接过的空契约。
    //   ② **签名装不下真实模型** —— `GetEnv(int)` 收单个 `int`,而房间标识是
    //      `Sim.Clinic.ClinicRoomId` 的 **64 位**(`High`+`Low`)。任何 int 映射都必然碰撞。
    //      且 `ClinicRoomId` 住 `Sim`,`Sim.Contracts` 的引用集**恰 = BCL**(ADR-025 §① / b2 门)
    //      ⇒ 契约面**物理上引不到**那个类型。改签名 = 击穿已裁的程序集边。
    //
    //   **真实通道 = `ClinicEnvironmentMemo.GetDto()`**(42 侧唯一读面,缓存 `ClinicEnvDto`,
    //   由 `ClinicEnvProjection.ToDto` 单点投影)。`AC-24-09` 的「同步可读、无延迟承诺」由它兑现。
    //   ⚠️ 若要恢复接口形态,须先裁 `ClinicRoomId` 的程序集归属 —— 见债册 `TD-clinic-20` 结案段的甲乙两案。
}
