# Story 003: `world_ecozones` 导出 + C4 多边形合法性 + C5 边界单一源

> **Epic**: 关卡工具(54 · Tooling 层)
> **Status**: Ready
> **Layer**: Tooling
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-28

## Context

**GDD**: `design/gdd/world-and-ecozones.md`(F-6-3 `EcozoneOf` 整数几何 · `AC-6-19` 多边形退化 · `O-6-11` 边界单一源)
**Requirement**: 兑现 S20-20 挂标 **#3「C1–C6 七项未实现」**中的 **C4** 与 **C5**

**ADR Governing Implementation**: **ADR-022 §三**(导出)· **§四 C4 / C5**(硬失败)
· **ADR-015 §一**(两层世界)· **ADR-021**(POI 定义 = 派生态)
**ADR Decision Summary**: 生态区多边形是**整数顶点**的开集;C4 检查五项退化
(重叠 / 顶点<3 / 自交 / 零面积 / 重合顶点)= **硬失败**;C5 要求**视觉层生态区遮罩由
整数多边形生成**(非手画第二份)= **硬失败**。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: **MEDIUM**(C5 涉视觉层遮罩生成)
**Engine Notes**: C4 是**纯整数几何**(零引擎 API,LOW);C5 须读 / 生成视觉层遮罩
(Texture / Terrain 层,post-cutoff,MEDIUM)。**两条的 Engine Risk 不同,不得合并评级**。

**Control Manifest Rules (Tooling 层)**:
- Required: 逻辑层**只有整数格**(ADR-015 §三);射线法全整数(F-6-3)
- Forbidden: **手画第二份边界**(C5 的判据正是「单一源」)
- Forbidden: **`Debug.Assert` 代替 `throw`**(C4 / C5 都是硬失败)
- Guardrail: 生态区是**开集**(边界点不属任何区),边界 min(id) 仲裁

---

## §病灶 —— C4 / C5 的「未实现」是什么意思

`LevelConsistencyCheck.UnimplementedChecks` 逐字登记(它是**返回值里的数据**,不是注释):

```
"C4 多边形合法性(需 world_ecozones.json —— 本批不产出)",
"C5 边界单一源(需视觉层遮罩 —— 本批不产出)",
```

⇒ 不是「实现得不好」,是**零实现**。而且**数据也不存在**:

```bash
ls assets/data/world_ecozones.json unity/Assets/DataCooked/world_ecozones.cooked.bytes 2>/dev/null \
  || echo "两份都不存在"
```

**连带后果(实测)**:

| # | 后果 | 依据 |
|---|---|---|
| ① | `EcozoneOf` 对任何格恒回 `None` | `CompositionRoot.cs:1163-1165` 自陈「生态区注册表在本装配内**尚未有装载面**」 |
| ② | `AC-23-08` 的生态区半边无对象 | 占用格 ⊆ 生态区?—— 生态区集合为空 |
| ③ | 5 时间天气的 `EcozoneOf(cell)` 输入恒 `None` | `TimeBase.cs:10` 逐字:`天气 = (WorldSeed, block(tick), EcozoneOf(cell))` |
| ④ | 13 病人 AI 的 `KnowsClinic` 恒假 | `CompositionRoot.cs:1172` 自陈「`EcozoneOf` 恒 `None` ⇒ `KnowsClinic` **恒假**,与 story-021 之前的硬编码 `false` **行为等价**」 |

---

## Acceptance Criteria

- [ ] **AC-1(BLOCKING)** —— **`world_ecozones.json` 由工具导出**:
      形状 = `{ ecozone_id, name, vertices: WorldPos[] }`(整数多边形,**开集语义**)。
      **判据**:顶点坐标全部整数(零浮点);`ecozone_id` 唯一。
- [ ] **AC-2(BLOCKING)** —— **C4 五项退化全部实现为硬失败**,各有一条负向夹具:
      ① **重叠**(两多边形内部相交)② **顶点 < 3** ③ **自交**(边相交)
      ④ **零面积** ⑤ **重合顶点**(同一多边形内重复顶点)。
      **判据**:五项各有一条「注入该退化 ⇒ 检查抛」的夹具,**`exactRed` 逐条**。
      ⚠️ **五项不得合并成一条夹具** —— 一个夹具验五项,停用任一分支时其余四项仍红,
      判别面不可分(承 `feedback_falsify_one_guard_per_property`)。
- [ ] **AC-3(BLOCKING)** —— **C5 边界单一源实现**:
      视觉层生态区遮罩**由整数多边形生成**,并检测「存在手画第二份」的情形。
      **判据**:① 遮罩生成路径可复算(同输入两次生成**逐字节相同**);
      ② 注入一份「与多边形不一致的遮罩」⇒ 检查**抛**。
- [ ] **AC-4(BLOCKING)** —— **烘焙 + 进组**:`world_ecozones.cooked` 落
      `unity/Assets/DataCooked/`,进 `data-core` 组。
      **判据**:组条目数 **+1**;catalog 重建后可查。
- [ ] **AC-5(BLOCKING)** —— **运行期装载面接通**:
      `CompositionRoot.cs:1171` 的 `new EcozoneRegistry()`(空表)被**装载路径**替换。
      **判据**:真产物在库时,`EcozoneOf` 对一个**区内格**回**该区 id**(不是 `None`);
      对一个**区外格**回 `None`。**两个方向都要** —— 只测区内会让「恒回 0」逃逸。
- [ ] **AC-6** —— **`UnimplementedChecks` 长度 7 → 5**(C4 / C5 两条移出)。
      **判据**:数组长度 == 5,且剩余五条仍是 C1 / C2 / C2' / C3 / C6。

---

## §Implementation Notes

### C4 的「重叠」判据须先定义清楚

ADR-022 §四 只写「重叠」两个字。三种可能的口径:

| 口径 | 判据 | 后果 |
|---|---|---|
| **甲** | 任意两多边形的**内部**相交 ⇒ 失败 | 严格;但相邻区共享边**不**失败(开集语义) |
| **乙** | 任意两多边形**闭包**相交 ⇒ 失败 | 相邻区共享边**会**失败 ⇒ 与开集语义冲突 |
| **丙** | 只检「重叠面积 > 0」 | 允许边界接触;但需面积计算(整数域可做) |

⚠️ **本 story 取甲**(与 F-6-3 的开集语义一致),**但须在实现里写明口径** ——
否则将来有人按乙实现,C4 会开始误报相邻区。

### C5 的「手画第二份」怎么检测

这是**静默失败类**判据(`world-and-ecozones.md:284-290` 已点名)。两条路:

| 路 | 判据 | 代价 |
|---|---|---|
| **甲** | 遮罩**只能由生成器产出**;生成器是唯一写入面 | 需改视觉层资产管线 |
| **乙** | 遮罩**生成后**与多边形**逐像素比对** | 可检已存在的遮罩;但比对成本高 |

⚠️ **本 story 取甲**(源头唯一),**乙作为兜底**在 CI 里跑一次(若视觉层已有遮罩资产)。
若两者都不可行,须**回 ADR-022 追加裁决** —— 不得把 C5 降级为告警。

### 为什么 AC-5 要求「两个方向都测」

`EcozoneOf` 的实现有三条分支(严格内部命中 / 边界 min(id) 仲裁 / `NONE` 哨兵)。
**只测区内格**时,一个「恒回 0」的实现会通过。必须同时测区外格回 `None`。

---

## Out of Scope

- **C1 / C2 / C2' / C3 / C6** —— 归 S6 / S5
- **POI 定义**(`world_poi`)—— 归 **S4**
- **`EcozoneOf` 的算法本体** —— **已实现**(`EcozoneRegistry.cs:286`),本 story 只接装载面
- **生态区的名称 / 叙事内容** —— 归 `world-ecozones` 的 GDD / 叙事轮
- **视觉层遮罩的美术质量** —— 本 story 只保证「由整数多边形生成」

---

## QA Test Cases

- **AC-2**:C4 五项,**逐项独立**。
  - Given: 一份合法多边形集。
  - When: 分别注入五种退化之一。
  - Then: 各**恰**触发对应失败项(报文点名是哪一项)。
  - Negative fixture: 停用 C4 的任一分支 ⇒ 该项夹具红,**其余四项不红**(判别面可分)。
  - Edge cases: **相邻区共享边不失败**(开集语义);**顶点恰为 3 合法**(< 3 才失败)。

- **AC-3**:C5。
  - Given: 整数多边形。
  - When: 生成遮罩两次。
  - Then: 逐字节相同。
  - Negative fixture: 注入一份与多边形不一致的遮罩 ⇒ 抛。

- **AC-5**:装载面。
  - Given: 真产物。
  - When: 查询区内格 / 区外格。
  - Then: 前者回该区 id,后者回 `None`。
  - Negative fixture: 若仍用空表 ⇒ 两者都回 `None` ⇒ 区内那条红。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/LevelTool/ecozone_polygon_check_test.cs` — must exist and pass
  (C4 五项各一条 + C5 两条 + 装载面两方向)
- ⚠️ **既有 `ecozone_registry_test.cs` 须保持全绿** —— 本 story 只加装载面,不改算法

---

## Dependencies

- Depends on: **S2**(数据集拆分骨架 + 烘焙骨架)
- Unlocks: **S6**(C2' / C5 相邻;C1 的生态区半边)

---

## §诚实边界(本 story 证明不了的事)

1. **生态区数量 / 形状是否合理** —— 本 story 只证明「有多边形且合法」;
   内容是关卡设计的事。
2. **C5 若视觉层遮罩资产尚不存在** —— 甲路可落地但**乙路无法验证**;
   须如实登记「乙路未跑」。
3. **`EcozoneOf` 的跨机器逐位一致性** —— 归 ADR-012 的黄金夹具矩阵,不在本 story。
4. **生态区与天气 / 病人 AI 的行为联动** —— 本 story 只让 `EcozoneOf` **有真值**;
   消费侧的行为是否正确归 5 / 13 的实现轮。
