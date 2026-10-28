# Story 005: `world_nav_{chunk}` 切片 + C6 切片完整性 + C3 告警

> **Epic**: 关卡工具(54 · Tooling 层)
> **Status**: Ready
> **Layer**: Tooling
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-28

## Context

**GDD**: `design/gdd/world-and-ecozones.md`(`O-6-12` 导航格驻留 · `ADR-015 §四③` chunk 脏块)
**Requirement**: 兑现 S20-20 挂标 **#3「C1–C6 七项未实现」**中的 **C6** 与 **C3**,
以及挂标 **#4** 的 **chunk 半边**

**ADR Governing Implementation**: **ADR-022 §五**(导航格切片者)· **§四 C3 / C6**
· **ADR-015 §四**(chunk 划分)· **ADR-016 §三**(AI 读整数导航格)
**ADR Decision Summary**: **关卡工具是切片者** —— `world_nav_{chunk}.json` 的 `{chunk}` 键
= ADR-015 §四 的**同一** chunk;**C6 保证切片完整**(每格**恰好覆盖一次**)= 硬失败;
**C3 导航格 ↔ NavMesh 非矛盾** = **告警**(漂移只影响表现)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: **MEDIUM**
**Engine Notes**: ⚠️ `com.unity.ai.navigation` 6.3 与 `NavMeshBuilder` 均为 **post-cutoff**
(ADR-022 Engine Risk 明确点名)。C3 须实测 NavMesh 可走性;C6 是**纯整数切片**(LOW)。
**两条的 Engine Risk 不同,不得合并评级。**

**Control Manifest Rules (Tooling 层)**:
- Required: 导航格按 chunk 切片(ADR-022 §五)
- Required: `{chunk}` 键 = ADR-015 §四 的**同一** chunk(不是第二套划分)
- Forbidden: **未驻留 chunk 视为全 `block`**(ADR-015 §四:驻留与否**不改变判定**)
- Forbidden: **C6 降级为告警**(它是硬失败)

---

## §病灶 —— 三条实测

### ① 导航格**连类型都不存在**

```bash
grep -rn "INavLattice\|NavLattice\|navGrid\|NavGrid" unity/Assets --include=*.cs | wc -l
```

实跑:**0**。⇒ 不是「导航格数据没有」,是**承载类型都没有**。
`world-ecozones/EPIC.md` 的 Governing ADRs 表里写「6 拥有 `INavLattice`」,
而全仓**零命中** —— 这是「引用却无登记」的又一实例。

### ② C6 / C3 在 `UnimplementedChecks` 里

```
"C3 导航格↔NavMesh(需 world_nav_*.json —— 本批不产出)",
"C6 切片完整性(需 world_nav_{chunk}.json —— 本批不产出)",
```

### ③ `O-6-12` 的两处补注**已落盘但无执行体**

ADR-022 §五 逐字:「`O-6-12` 的**工具侧**由此闭环」——
但那个「闭环」的**执行体**(C6 检查 + 切片器)**不存在**。
⇒ 闭环是**纸面的**。

---

## Acceptance Criteria

- [ ] **AC-1(BLOCKING)** —— **`INavLattice` 承载类型落地**(或经 ADR 裁定的等价形状):
      须能表达「某格可走 / 不可走」与「未驻留 = 全 block」。
      **判据**:① 类型存在;② **未驻留 chunk 查询回 `block`**(不是 `walkable`)。
      ⚠️ **若裁决为「不立 `INavLattice`,改用别的形状」,须回 ADR 登记** ——
      本 AC 的判据不变(「未驻留 = block」),变的只是载体名。
- [ ] **AC-2(BLOCKING)** —— **工具按 chunk 切片导出**:
      产出 `world_nav_{chunk}.json` 系列,`{chunk}` 键 = **ADR-015 §四 的同一划分**。
      **判据**:① 每个 chunk 一个文件;② 文件名里的 chunk 键与几何侧 chunk 划分
      **用同一个函数算**(不是两处各算一次)。
- [ ] **AC-3(BLOCKING)** —— **C6 切片完整性 = 硬失败**:
      每格**恰好覆盖一次**。两种违反都要拦:
      ① **漏格**(某格不在任何 chunk 里)② **重复格**(某格在两个 chunk 里)。
      **判据**:两条**独立**负向夹具,各 `exactRed`。
      ⚠️ **不得合并** —— 漏格与重复格是不同的失败模式(前者表现为「那格不可走」,
      后者表现为「取哪一份」),一个夹具验两条会让停用任一分支时另一条仍红。
- [ ] **AC-4(BLOCKING)** —— **C3 导航格 ↔ NavMesh 非矛盾 = 告警**:
      逻辑可走与 NavMesh 可走**不矛盾**(不要求逐格相等)。
      **判据**:① 注入一处矛盾(逻辑 walkable / NavMesh 不可达)⇒ **报出告警**;
      ② **不**使构建失败。
      ⚠️ **必须验「报出」,不能只验「不失败」** —— 一个「永远不报」的实现也满足 ②,
      那正是告警类检查的经典假绿。
- [ ] **AC-5(BLOCKING)** —— **烘焙 + 进组**:各 chunk 产物进 `data-core`(或按需加载组)。
      **判据**:组条目数 = 原数 **+ chunk 数**。
      ⚠️ **须核对 ADR-014 §五 的「首次 `Step` 前常驻」不适用于导航格** ——
      ADR-022 §五 已补注「只约束小体量启动前置数据,逻辑导航格除外」。
- [ ] **AC-6** —— **`UnimplementedChecks` 长度 7 → 3**(C3 / C6 移出;
      若 S3 已先行则再减 2)。**判据**:数组长度 == 剩余项数,剩余项逐条点名。

---

## §Implementation Notes

### `INavLattice` 不存在 —— 这是本 story 最大的结构性障碍

`world-ecozones/EPIC.md` 的 Governing ADRs 表逐字写「6 拥有(`INavLattice`)」,
而全仓 `grep` **零命中**。两种可能:

| 可能 | 处置 |
|---|---|
| **甲** 它**本就该存在**,只是从未实现 | 本 story 落地它 —— 但须先确认形状(见下) |
| **乙** 它是 ADR 里的**意向名**,实际形状未定 | **须回 ADR-016 / ADR-022 追加裁决** |

⚠️ **本 story 取甲,但形状须由 ADR-016 §三 的「AI 读粗粒度整数格」反推**:
最小形状 = `bool IsWalkable(WorldPos cell)` + 「未驻留 ⇒ `false`」。
**不发明第二套坐标 / 第二套 chunk 划分**(承 ADR-015 §四:禁 chunk 局部坐标系)。

### chunk 划分的「同一函数」是 C6 的核心

ADR-022 §五 逐字:「`{chunk}` 键 = `ADR-015 §四` 的同一 chunk」。
⇒ **切片的 chunk 函数与几何的 chunk 函数必须同源**。
判据:两处调用**同一个** `ChunkOf(WorldPos)`(或等价),**不是**两处各写一遍公式。
⚠️ 这可以做成**文本级门**:扫「chunk 划分公式」在仓内只出现一次。

### C3 的「矛盾」怎么定义

「不矛盾」不等于「相等」。三种情形:

| 逻辑 | NavMesh | 判定 |
|---|---|---|
| walkable | 可达 | ✅ 一致 |
| block | 不可达 | ✅ 一致 |
| **walkable** | **不可达** | ⚠️ **矛盾** → 告警 |
| block | 可达 | ⚠️ 矛盾(但方向相反 —— AI 会绕开一个玩家能走的地方) |

⚠️ **两个方向都要检**。只检一个方向会让另一半漂移静默。

---

## Out of Scope

- **NavMesh 运行期烘焙** —— 归 27 / 表现层
- **寻路算法本体** —— 归 27(本 story 只出**数据**)
- **C1 / C2 / C2'** —— 归 S6
- **C4 / C5** —— 归 S3
- **chunk 激活权** —— 归 **6**(ADR-023 ⑥;本 story 只出切片数据)
- **`world_geometry` / `world_terrain`** —— 归 S2

---

## QA Test Cases

- **AC-1**:未驻留 = block。
  - Given: 一个未装载的 chunk。
  - When: 查询其中某格。
  - Then: 回 `block`(**不是** `walkable`)。
  - Negative fixture: 若实现「未驻留 ⇒ 默认可走」⇒ 红。
  - ⚠️ 这是**静默失败**类判据 —— 与 `WorldGeometry` 拒绝空白入口同源。

- **AC-3**:C6 两条独立。
  - Given: 一组 chunk 切片。
  - When: 注入漏格 / 注入重复格。
  - Then: 各**恰**触发对应失败(报文可区分)。
  - Negative fixture: 停用「漏格」分支 ⇒ 只漏格那条红;重复格那条**不红**。

- **AC-4**:C3 告警。
  - Given: 一处逻辑 / NavMesh 矛盾。
  - When: 跑检查。
  - Then: **报出告警**且**不抛**。
  - Negative fixture: ① 停用矛盾检测 ⇒ 不报 ⇒ 红;② 把告警改成抛 ⇒ 也不对(告警不该失败)。

- **AC-2**:chunk 函数同源。
  - Given: 几何侧 chunk 划分 + 导航格侧 chunk 划分。
  - When: 对同一格算两次。
  - Then: 同一个 chunk 键。
  - Negative fixture: 两处公式不同(如一处用 `x/16`、一处用 `x>>4` 但边界不同)⇒ 红。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/LevelTool/nav_chunk_slicing_test.cs` — must exist and pass
  (未驻留 block + C6 两条 + C3 告警 + chunk 同源)
- **C3 的 NavMesh 实测证据**:若 `com.unity.ai.navigation` 行为与假设不符,
  须落 `production/qa/evidence/spike-navmesh-6.3-<date>.md`

---

## Dependencies

- Depends on: **S2**(几何 / chunk 划分须先存在 —— C6 的「每格恰好覆盖一次」需要
  「格的全集」,而全集来自几何)
- Unlocks: **27 的寻路实现**(有导航格数据)· `O-6-12` 的**真实**闭环(纸面 → 执行体)

---

## §诚实边界(本 story 证明不了的事)

1. **AI 寻路是否真的走通** —— 本 story 只出数据;寻路归 27。
2. **NavMesh 烘焙质量** —— C3 是**告警**级,漂移只影响表现。
3. **chunk 激活的运行时行为** —— 归 6(ADR-023 ⑥)。
4. **`INavLattice` 的形状若被 ADR 否决** —— AC-1 的载体变,判据不变;
   本 story 的估时**会变**。
5. **C3 的 post-cutoff API 假设** —— 若 spike 推翻,须回 ADR-022 追加裁决。
