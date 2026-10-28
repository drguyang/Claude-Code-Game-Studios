# Story 001: 关卡源模型泛化 —— 多关卡 + 数据集拆分

> **Epic**: 关卡工具(54 · Tooling 层)
> **Status**: Ready
> **Layer**: Tooling
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-28

## Context

**GDD**: 无 —— 关卡工具是工具不是玩法系统;产出契约 = ADR-022
**Requirement**: 兑现 S20-20 挂标 **#4「关卡工具只服务一个关卡」**
(`production/sprints/sprint-20.md:969`)

**ADR Governing Implementation**: **ADR-022 §二**(唯一作者)· **§三**(导出契约)
**ADR Decision Summary**: 逻辑层全部整数数据**只能由关卡工具编写并导出**;
导出契约 = `world_{geometry,ecozones,poi,resources,buildslots,nav_{chunk},terrain}.json`
→ ADR-014 两阶段烘焙。工具**运行期零存在**、**不进构建**、**门 A 不约束**。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: **LOW**
**Engine Notes**: 本 story **零引擎 API** —— 纯数据模型 + 文件布局重构。
⚠️ 这是本 epic 里**唯一** Engine Risk LOW 的实现 story;S2 / S5 才是 MEDIUM 的那两件。

**Control Manifest Rules (Tooling 层)**:
- Required: 运行期零存在 · 不计入 P0 的 31 项 · 门 A 不约束(ADR-022 §一)
- Required: 逻辑层数据**只能由本工具**编写导出(ADR-022 §二)
- Forbidden: **任何以 seed 生成地形 / 多边形的代码**(ADR-022 §二:210)
- Forbidden: **用 `Debug.Assert` 代替显式 `throw`**(ADR-022 §四:227-228)

---

## §病灶 —— 三条实测,不是推测

### ① 关卡尺寸是**常量**,不是数据

```bash
grep -n 'const int Width\|const int Height\|const int Depth\|LevelId =' \
  unity/Assets/Editor.Tools.Level/P0ClinicLevel.cs
```

实跑(2026-10-28):

```
37:        public const int Width = 16;
40:        public const int Height = 1;
43:        public const int Depth = 16;
46:        public const string LevelId = "p0_clinic";
```

`LevelSource` 虽**已经**有 `(LevelId, Width, Height, Depth, Slots)` 五元组
(它是泛化的),但**唯一的生产者** `P0ClinicLevel` 把四个值写成常量 ⇒
「换一个关卡」需要**改源码**,不是换数据。

### ② 导出器只认**一个**文件名

```bash
grep -n 'public const string FileName\|public static void Export' \
  unity/Assets/Editor.Tools.Level/LevelExporter.cs
```

```
31:        public const string FileName = "world_buildslots.json";
106:        public static void ExportTo(string path, LevelSource src)
117:        public static void ExportToRepo(string repoRoot)
125:        public static void ExportP0ClinicToRepo()
```

`FileName` 是**单一常量**,`ExportToRepo` 硬编码 `P0ClinicLevel.Define()`,
batch 入口 `ExportP0ClinicToRepo` 的名字里就写着 `P0Clinic`。
⇒ **加第二类数据集、加第二个关卡,都没有入口**。

### ③ 数据集类别**零抽象**

ADR-022 §三 要求 **7 类**数据集,实有 **1** 类:

```bash
for n in geometry ecozones poi resources buildslots terrain; do
  printf "world_%-12s JSON=%s cooked=%s\n" "$n" \
    "$([ -f assets/data/world_$n.json ] && echo 有 || echo 无)" \
    "$([ -f unity/Assets/DataCooked/world_$n.cooked.bytes ] && echo 有 || echo 无)"
done
```

实跑:只有 `world_buildslots` 一行是「有/有」,其余五行全「无/无」。

---

## Acceptance Criteria

- [ ] **AC-1(BLOCKING)** —— **`LevelSource` 成为唯一关卡载体,尺寸来自数据**:
      `P0ClinicLevel` 的 `Width` / `Height` / `Depth` **常量删除**(或降为**默认值**且
      被 `LevelSource` 覆盖),`LevelSource` 的尺寸字段是唯一真源。
      **判据**:构造一个 **8×2×8** 的 `LevelSource` 并导出,产物里 `world_size` 恰为
      `[8, 2, 8]` —— **不是** `[16, 1, 16]`。
- [ ] **AC-2(BLOCKING)** —— **至少两个关卡并存**:
      新增第二个关卡定义(尺寸与 `p0_clinic` **不同**),两者各自导出到
      `world_buildslots_{level_id}.json`(或 ADR-022 §三 允许的等价命名)。
      **判据**:两次导出产出的文件**内容不同**(尺寸行 + 槽位行),且**都**过一致性检查。
      ⚠️ **反例**:把 `P0ClinicLevel` 复制一份改个名字**不算** —— 判据要求两关尺寸不同。
- [ ] **AC-3(BLOCKING)** —— **数据集类别成为一等概念**:
      引入 `WorldDataSetKind`(闭集 = ADR-022 §三 的 7 类 + `nav_{chunk}` 的参数化),
      导出器按 kind 分派,**新增一类不需要改导出器主体**。
      **判据**:本 story 结束时 `world_buildslots` 走新路径导出,**逐字节等于**改动前的产物
      (`sha256sum` 对比)—— 证明重构**零行为变化**。
- [ ] **AC-4** —— **`UnimplementedChecks` 的措辞同步**:
      `LevelConsistencyCheck` 里「本批不产出」的措辞须改为「**待 S2–S6 产出**」,
      因为它说的「本批」是 S20-20 那个最小卡,不是本 epic。
      **判据**:该数组**长度仍为 7**(本 story 不实现任何检查),但每条的理由不再指向一个已不存在的「本批」。
- [ ] **AC-5** —— **batch 入口泛化**:`ExportP0ClinicToRepo` 的硬编码被
      `ExportLevelToRepo(levelId)` 取代(或等价),**至少两个关卡各有一条可跑的 batch 命令**。
      **判据**:两条命令实跑各产出一份 JSON。

---

## §Implementation Notes

### 命名冲突须先裁决(本 story 的最大风险)

ADR-022 §三 的导出契约写的是 `world_buildslots.json`(**无** level 后缀)。
引入多关卡后有两种形状:

| 形状 | 文件名 | 代价 |
|---|---|---|
| **甲** | `world_buildslots_{level_id}.json` | 破坏 ADR-022 §三 的字面契约 ⇒ **须 ADR-022 窄修订** |
| **乙** | 一份 JSON 内含多关卡(顶层 `levels: [...]`) | 不破坏契约;但读方 codec 须改(它现在读单关) |

⚠️ **本 story 取「甲」但须先回 ADR-022 追加裁决** —— 按 ADR-029 的先例,
「契约形状变更」不得由 story 自行决定。若用户裁定取乙,本 story 的 AC-2 措辞随之调整
(判据从「两个文件」改为「一份文件里两个 level 块」)。

### 为什么 AC-3 要求「逐字节等于改动前」

这是**重构的可证伪上限**(承 `feedback_refactor_falsifiability_ceiling`):
新写法与旧写法值相等时,夹具分不出对错。唯一可用的哨兵是**字面量哈希** ——
拿改动前产物的 SHA-256 当基准,重构后必须逐字节相同。
⚠️ 若产物字节**必须**变(如加了 `level_id` 字段),则该条**不适用**,
须改为「语义等价」并由**往返夹具**证明,且如实登记「本 AC 的判别力降级」。

### 不要在本 story 实现任何 C1–C6 检查

`UnimplementedChecks` 的长度在本 story 结束时**仍是 7**。
若它变成 6,说明本 story 越界了 —— 检查实现归 S3 / S5 / S6。

---

## Out of Scope

- **`world_geometry` / `world_terrain` 的导出与烘焙** —— 归 **S2**
- **生态区多边形 / C4 / C5** —— 归 **S3**
- **POI / 资源点导出** —— 归 **S4**
- **导航格切片 / C6 / C3** —— 归 **S5**
- **C1 / C2 / C2'** —— 归 **S6**
- **运行期装载面**(`MinimalWorldFactory` 的替换)—— 归 S2 / S4
- **`world_buildslots` 的既有导出语义**(schema 2 / `baked_initial` / `module_id`)—— **不改**

---

## QA Test Cases

- **AC-1**:尺寸来自数据。
  - Given: 一个手造的 8×2×8 `LevelSource`(槽位全在范围内)。
  - When: 导出。
  - Then: 产物 `world_size` == `[8, 2, 8]`。
  - Negative fixture: 若实现仍读 `P0ClinicLevel.Width` ⇒ 产出 `[16,1,16]` ⇒ 红。
  - Edge cases: 非立方(8×2×8 而非 8×8×8)—— 三个轴**各自独立**,不是同一个值。

- **AC-2**:两关卡并存。
  - Given: 关卡 A(16×1×16)+ 关卡 B(尺寸不同,如 8×2×8)。
  - When: 各自导出。
  - Then: 两份文件都存在、内容不同、都过 `CheckOrThrow`。
  - Negative fixture: 若第二关只是 A 的复制品(尺寸相同)⇒ 判据「内容不同」红。

- **AC-3**:重构零行为变化。
  - Given: 改动前 `world_buildslots.json` 的 SHA-256。
  - When: 走新的 kind 分派路径重新导出。
  - Then: SHA-256 **逐字节相同**。
  - Negative fixture: 若新路径漏了任一字段(如 `module_id` 写成省略而非 `null`)⇒ 字节不同 ⇒ 红。
  - ⚠️ **判别力边界**:若产物必须变更,本条改为往返夹具并**如实降级登记**。

- **AC-5**:两条 batch 命令。
  - Given: 两个关卡的 id。
  - When: 各跑一次 batch。
  - Then: 各产出一份 JSON,且两份都能被读方 codec 读入(若 codec 支持)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/LevelTool/level_source_generalization_test.cs` — must exist and pass
- ⚠️ **既有 `world_buildslots_bake_test.cs` / `world_buildslots_codec_validation_test.cs`
  须保持全绿** —— 本 story 是**重构**,不得因它改动既有判据
- ⚠️ **既有产物 `world_buildslots.cooked.bytes` 的 SHA-256 须在改动前后各记一次**
  (AC-3 的判据基础)

---

## Dependencies

- Depends on: **无**(本 epic 的第一件)
- Unlocks: **S2**(数据集拆分骨架是 `world_geometry` / `world_terrain` 导出的前置)·
  **S4** / **S5**(同)

---

## §诚实边界(本 story 证明不了的事)

1. **多关卡 ≠ 多关卡可玩** —— 本 story 只证明「工具能导出第二个关卡」,
   运行期能否装载 / 切换是 S2 之后的事。
2. **ADR-022 §三 的契约形状若被用户否决(取乙案)**,AC-2 的判据须改,
   本 story 的估时不变但验收形态变。
3. **chunk 切片**(`world_nav_{chunk}`)**不在本 story** —— `WorldDataSetKind` 里
   它只是**参数化的一类**,实际切片归 S5。
