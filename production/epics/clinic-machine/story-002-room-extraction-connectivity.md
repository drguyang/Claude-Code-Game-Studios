# Story 002: 房间析出与连通规则

> **Epic**: 医馆即机器
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-10-15  <!-- 原 `2026-10-02` —— 2026-10-21 /dev-story Phase 2 逐件判定取 [A]（升级并按现行规则实施）；原文保留 -->
> **Last Updated**: 2026-10-21
## Context

**GDD**: `design/gdd/clinic-machine.md`(规则一 房间 = 连通 FURN 格集 · EC-24-07 双簇整盘 ROOM_NONE · 外壳格/空地板为非格 · 开顶簇合法)
**Requirement**: TR-clinic-007(房间连通性析出算法与 ROOM_NONE 哨兵 —— 原 gap,本 story 兑现机制层) · TR-clinic-001(纯整数 4 邻接,禁物理/禁连续坐标) · TR-clinic-008(格分类三态判据)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-015(主): 世界几何 —— 单一整数格
**ADR Decision Summary**: `WorldPos = (i32 x, i32 y, i32 z)` 地形/建造槽位/医馆房间格共用同一套格;医馆「红石逻辑」= 整数格邻接判定(非物理/非 NavMesh);房间析出的输入 = 系统 23 的只读 `structure_at(cell) → module_id + orientation`,24 不拥有建造。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯整数集合运算(并查集/BFS on 格),零引擎 API;不依赖 post-cutoff 面。

**Control Manifest Rules (this layer)**:
- Required: 连通判据 `ABS(dx)+ABS(dy)==1`(4 邻接,同层);壳格(BakedInitial)与空地板不参与析出、不计分、不传导连通
- Forbidden: PhysX 触发器 / Collider / NavMesh / 连续坐标参与房间判定;围合(flood-fill 封闭性)检查 —— 开顶簇合法
- Guardrail: ≥2 个不连通家具簇 ⇒ 整盘判 `ROOM_NONE (0,0)`,不做「就近簇」部分生效(EC-24-07)

---

## Acceptance Criteria

*From GDD `design/gdd/clinic-machine.md`, scoped to this story:*

- [x] AC-24-10:断开两簇的 fixture 整盘返回 ROOM_NONE(单簇 L 形 / T 形正常析出)
- [x] 4 邻接判据:对角接触(`|dx|==1 ∧ |dy|==1`)不连通(与 8-邻接反例可区分)
- [x] 外壳格与空地板:不产生房间成员、不阻断两簇判定(壳格内嵌家具时家具照常析出)
- [x] 开顶(无围合)家具簇照样成房间 —— 算法中无封闭性检查(规则一明令)
- [x] 房间标识稳定:同一布局两次求值得到同一 room id(纯函数,承 AC-24-04 的析出侧)
- [x] 输入仅 `structure_at(cell)` 只读视图:24 不调用 23 的写路径、不持建造状态

---

## Implementation Notes

*Derived from ADR-015 Implementation Guidelines:*

1. 输入枚举:对医馆 footprint 内每格调 `structure_at(cell)`(23 只读),得 `module_id + orientation` 的 FURN 格集合 S。
2. 连通分量:4 邻接并查集(或 BFS)对 S 求分量数;`|分量| == 1` ⇒ 该布局成一个房间;`≥ 2` ⇒ 整盘 ROOM_NONE。
3. `ROOM_NONE = (0,0)` 哨兵与真实 room id 空间不交(room id 由布局确定性派生,不含 Unity instance id)。
4. 壳格规则:`BakedInitial`(开档自带)按 GDD 归「非格」—— 从 S 中剔除后不参与连通传导(两个家具簇仅靠壳格相连 ⇒ 仍判 2 簇)。
5. 该纯函数住 `Sim` 程序集(门 A):零 `UnityEngine` 引用;输入类型来自 `Sim.Contracts.WorldPos`。
6. 房间→格 的反向索引(乘子求值按房间取成员)在返回值内一次性构建,不缓存于 24 之外。

> **7. 🔁 2026-10-21 用户裁定「甲案」—— `type(c) ∈ FURN_TYPES` 的代理判据(实施必须遵循)**
>
> **背景(实测)**:GDD F-24 公共定义写「`S` 成员判据 = `type(c) ∈ FURN_TYPES`」,而该闭集
> **全库零定义**;`FURN_TABLE` 数据文件不存在;`IFurnTypeSource` **零生产实现**;
> `build_modules.json`(`module_id` 的目录)**同样不存在**。`TD-clinic-01` 自陈封 001→005 全链入口。
>
> **裁定**:本件成员判据取**代理判据** ——
> **家具格 = 被 `StructurePlaced` 事件占用、且在该只读视图内可达的格**(即 Sim 注册表内被占用的格)。
>
> **四条实施约束**:
> ① **逐字登记代理身份**:实现注释里写明「本判据是 `type(c) ∈ FURN_TYPES` 的**代理**,
>    而非其等价物;代理**更宽**(任何放置件都算家具格)」;
> ② **配缺口哨兵**:须有一条测试使「闭集缺席」**可观测而非静默**(形态承 `clinic-001` 的缺口哨兵先例);
> ③ **闭集落地后须重判**:`O-24-1` 落地时,若真判据与代理**不等价** ⇒ 既有测试须**转红**,
>    届时改判据并如实登记(不得静默改绿);
> ④ **不得**为将来的类型匹配提前造接缝 —— 002 的 6 条 AC **不含**类型匹配;
>    类型匹配(`ROOM_TABLE` 子句求值)是 `S17-2` / 后续的事。
>
> ⚠️ **两条实现陷阱(本轮实测,勿踩)**:
> - `StructureInstanceRegistry.GetInstanceAt(anchor)` **只比对 `Anchor`,不比对 `OccupiedCells`**
>   ⇒ 多格结构的**非锚点格查不到**,`World.GetModuleAt` 对它们返回 `-1`。
>   本件的格集合须遍历**实例的 `OccupiedCells`**,不能逐格调 `GetInstanceAt`。
> - 注册表**无公开枚举器**(只有 `TryGet(id)` / `GetInstanceAt(anchor)` / `Count` / `Clear`)
>   ⇒ 输入视图须由本件**自定义只读接口**注入(承 AC 第 6 条「输入仅只读视图」),测试注入替身。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 001]: 表烘焙与 schema 门(room 谓词子句的解析在表侧)
- [Story 003]: adj_sum / EnvMod / EquipMod 求值(以本 story 的房间析出结果为输入)
- [Story 004]: 交付边界(ClinicEnvDto / 21a 配对)
- [Story 005]: Memoize 缓存与事件失效键
- 系统 23:建造/拆除与 `structure_at` 本身;系统 6:chunk 激活

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(双簇整盘失效)**: EC-24-07 的判据面
  - Given: 两个 4-邻接不连通的家具簇(fixture 布局 A)
  - When: 房间析出
  - Then: 整盘 ROOM_NONE,(0,0);无任何房间条目
  - Edge cases: 三簇同样 ROOM_NONE;两簇+壳格桥 = 仍 2 簇(壳格不传导)
- **AC-2(对角不连通)**: L 形 vs 对角阶梯
  - Given: `(0,0),(1,0),(1,1)` L 形 → 1 簇;`(0,0),(1,1)` 对角 → 2 簇
  - When: 析出
  - Then: 前者成房间;后者 ROOM_NONE
  - Edge cases: 单格家具 = 平凡 1 簇房间
- **AC-3(开顶合法)**: 无围合簇
  - Given: 一排直线家具格(无任何墙/封闭)
  - When: 析出
  - Then: 正常成房间 —— 断言代码路径中不存在围合检查(反射/行为双查)
  - Edge cases: 凹形(有缺口)与凸形结果相同

---

> ### 🔁 QA plan 回填(2026-10-21 · `production/qa/qa-plan-sprint-17-2026-10-21.md`)
>
> 上列规格**原文保留**。本次只补 QA plan 相对上列**新增**的判据(禁把复述当增量);
> 已覆盖的不重复列。**估计测试数 ~12**。
>
> | # | 新增判据 | 依据 |
> |---|---|---|
> | 1 | **判别力来源显式登记**:AC-2 那对 fixture(`(0,0),(1,0),(1,1)` vs `(0,0),(1,1)`)在**8 邻接下会连成 1 簇** ⇒ 它区分的是 4/8 邻接,**不是**「恰取到空集」。测试注释须写明这一点 | 卡面 AC 第 2 条 · F-24 公共定义 |
> | 2 | **壳格内嵌家具 ⇒ 该家具照常析出**(壳格本身仍是「非格」)。上列 AC-1 只测了「壳格桥不传导」,**未测**嵌家具 | 卡面 AC 第 3 条 · EC-24-04 |
> | 3 | **两次求值 `room id` 相同**(稳定性),上列无此测 | 卡面 AC 第 5 条(纯函数) |
> | 4 | **只读断言**:24 零调用系统 23 写路径、不持建造状态(注入只读视图,断言写面不可达) | 卡面 AC 第 6 条 |
> | 5 | **空布局 ⇒ `ROOM_NONE`,不是错误**(开局空医馆,EC-24-01);**家具存在但未匹配任何房间类型 ⇒ 中性**(EC-24-02) | GDD EC-24-01 / 02 |
>
> ⚠️ **上游状态登记**:本件 `Depends on: Story 001`,`clinic-001` 现状 = **`In Review`**(实现已落、42 测、复核原件在册;两条 AC 仍挂 `BLOCKED-BY-O-24-1`)。
> 用户 2026-10-21 裁定「承风险照排」(`/dev-story` 的 `[A]` 分支)。**不为 `clinic-001` 转任何状态。**

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/ClinicMachine/clinic_room_extraction_test.cs` — must exist and pass
**Status**: [x] Created & passing — ~~**22 个测试函数**~~ **23 个测试函数**(`grep -c "\[Test\]"` 与 `grep -c "public void test_"` 同为 23)
> 🔁 **2026-10-21 订正**:本行原为建 story 时的占位 `[ ] Not yet created`,与事实相反(文件已存在且全绿)。
> 原文「21 测」(本文件 §Completion Notes 与 `impl-sprint17-batch1-2026-10-21.md:32`)为**陈旧记账** ——
> `review-sprint17-batch1-2026-10-21.md:211` 记的 **22 测**才与盘上一致。
> 🔁 **2026-10-21 第二次订正(QA 签核轮)**:实测 **23 测**。第 23 条 =
> `test_countComponents_xAtIntMax_noPhantomUnionFromWraparound`(`git diff` 工作树新增件,
> 承载 int64 极值下「不产生幻影并集」的守卫)。⇒ **22 → 23**,原文加删除线保留。
> 溯源:`2026-10-21 16:51 CST` · 批次 `sprint-17 QA 签核轮全量 EditMode` · `e0ad379`+工作树 ·
> `4350/4314/0/36`(集合差 **新增 3 · 消失 0**)。

---

## Dependencies

- Depends on: Story 001(cooked 表可读);系统 23 的 `structure_at` 只读契约(GDD 侧已定,实现若未就绪可用 fixture 桩注入 —— 承「依赖注入而非单例」标准)
- Unlocks: Story 003(乘子求值的房间输入)、Story 005(Memoize 失效键基于布局事件)

---

## Completion Notes
**Completed**: 2026-10-21
**Criteria**: 6/6 passing
**Deviations**:
- ADVISORY — 「外壳格」AC 的判据边界:本视图契约下壳格**不是** `StructurePlaced` 实例,24 在类型层面无从识别「23 违规上报的非空壳格」。本件钉的是「空足迹实例不贡献格、不传导、不入 |S|」。
  反证 M3(注入 instance-as-cell)⇒ 恰 2 条红。提供方半边归系统 23。
- ADVISORY — `FurnTypeClosureEnforced` 仍是编译期常量 `false`;`O-24-1` 落地改判据时须同时处理该哨兵,否则零测试转红。
  另:本批**已修**壳格夹具(原夹具无判别力 —— 评审 A3-3),现以「空足迹实例」表达壳格并与无壳格基线逐项比对。
  <br>🔁 **2026-10-21 稍后已处理(S17-4 · `O-24-1` 兑现)**:闭集来源落地,真判据入口
  `Extract(view, furnTypes)` 已提供且已证。**但本应急转红的两条哨兵测并未转红** —— 默认单参入口
  仍走代理判据(改它会静默改变本 story 已验收的行为,承约束 ③「不得静默改绿」)⇒
  `FurnTypeClosureEnforced` 的真值**就是 `false`**。C11 检测测的判据已按卡面授权**改判并如实登记**
  (新判据 = 痕迹存在时,哨兵 false 仅在 `FurnTraitsProductionReady == true ∧ 真判据重载存在` 时合法),
  见 `production/qa/evidence/impl-s17-4-furniture-catalogue-2026-10-21.md` §七 1。
  ⚠️ **实施约束 ③ 的「既有测试须转红」这一步,实际以「改判据」兑现而非「转红后修改」** —— 形态不同,故在此显式登记。
  **仍未落**:装配层尚未选真判据入口(选哪条是装配层接线)。
**Test Evidence**: Logic — `unity/Assets/Tests/EditMode/ClinicMachine/clinic_room_extraction_test.cs`(~~21 测~~ **23 测**)
  + 证伪 2 例(F1 对角并查 / M3 instance-as-cell),见 `production/qa/evidence/review-sprint17-batch1-2026-10-21.md` §六之一
  <br>🔁 **2026-10-21 订正**:实测 **22 测**(非 21)。原文保留作沿革。
  <br>🔁 **2026-10-21 第二次订正(QA 签核轮)**:实测 **23 测**(非 22)。新增第 23 条 =
  `test_countComponents_xAtIntMax_noPhantomUnionFromWraparound`。**22 → 23**。
**Code Review**: Complete — `production/qa/evidence/review-sprint17-batch1-2026-10-21.md`(APPROVED WITH SUGGESTIONS,0 BLOCKING)
