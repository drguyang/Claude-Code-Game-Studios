# Story 003: 堆叠键、容器折重与 P0 质量范围门

> **Epic**: 库存与物品
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 5h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/inventory-and-items.md`(规则三 堆叠 · 规则十 容器 · BL-7③/BL-28 范围门 · OQ-20-4 结清口径 · AC-20-04/12/15)
**Requirement**: TR-inventory-004(堆叠键 = (item_key, quality) 唯一出处 21a;容器自身不堆叠,qty 恒 1;20 不得改写 StackKey,❌ gap —— 无 ADR 承接)· TR-inventory-015(容器折重 = 空箱自重 + 子件逐件,OQ-20-4 已结清;registry 状态待翻)· TR-inventory-012(20 全部输出 DTO 递归反射无 float)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-006(主,次条 D-21-17): `weight` / `stack_max` 是 **int 计数**,移出 `Fix` 解析集 · ADR-014(次): 物品定义与堆叠参数走烘焙管线 · ADR-013(次): 容器界面 = 非网格、无排序(承 42 规则五「不得是背包格」)
**ADR Decision Summary**: 堆叠键唯一出处 = 21a F4(20 只执行,禁本地重定义);不同品级不堆、各占一个 `ItemInstance`;P0 `build_part` 的 quality 恒 1(BL-28 范围债 → P1a);容器 `qty` / `quality` 恒 1;P0 禁嵌套容器(BL-7③,`children` 恒空 —— 任务规则亦钉此条);折重口径已结清 = 空箱自重 + 子件逐件(OQ-20-4);容器摆放序 = `instance_id` 升序确定性函数(「入箱序」废止,BL-22)—— 摆放**函数**归 20,布局(几列几页)归 42(R9)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 堆叠/折重/摆放序均门 A 纯整数逻辑;`PresentationDtoGuard` 递归反射扫描复用 case-system 先例件;零 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: `StackKey = (item_key, quality)` 从 21a 取,20 侧零重定义点;`stack_max`/`weight` 按 int 参与整数运算;容器实例 qty 恒 1(不变量断言);折重 = `自重 + Σ 子件 weight×qty`(整数);摆放序 = 纯 `instance_id` 升序函数(可重放、无第三输入)
- Forbidden: 嵌套容器(P0 `children` 恒空,出现写入 = 构建期拒绝);20 自建堆叠规则或跨品级合并;为呈现新增第二容量真值(AC-20-24 单调契约半边);把排序/自动整理做进机制层(42 规则五禁)
- Guardrail: 布局参数(`COLUMNS` / `ROWS_PER_PAGE`)归 42 未定 ⇒ 本故事只交付「给定 COLUMNS 的落位纯函数」+ BLOCKED-BY-R9 标注(AC-20-20 走查半边在 Story 006)

---

## Acceptance Criteria

*From GDD `design/gdd/inventory-and-items.md`, scoped to this story:*

- [ ] **AC-20-04**: `GIVEN` 堆叠,`WHEN` 检索 `StackKey`,`THEN` 唯一 = `(item_key, quality)`(规则三 · 21a F4)
- [ ] **AC-20-12**: `GIVEN` 容器界面(机制半边),`WHEN` 检索,`THEN` 非网格、零排序/零自动整理(20 侧:摆放序函数不含任何按 weight/品级/时间的重排路径)
- [ ] **AC-20-15**: `GIVEN` 全部 20 的输出 DTO,`WHEN` 递归反射扫描,`THEN` 无 `float` 字段(门 A · 与 AC-44-B1 同型)
- [ ] **P0 范围门(规则三/十,BL-7③ + BL-28)**: 容器实例 `qty`/`quality` 恒 1;`children` 恒空;`build_part` 实例 `quality` 恒 1 —— 三条各立机器断言(不变量破坏 = 构建失败),P1a 解除时以夹具反证扩展点
- [ ] **折重不变量(OQ-20-4 结清口径)**: `Σ(容器 + 子件)` 折重 = 空箱自重 + 子件逐件,无双重计数;整数域求值,先乘后加(与 Story 004 的 `CarryLoad` 共用加法件)

---

## Implementation Notes

*Derived from ADR-006 D-21-17(主)/ ADR-014:*

1. 堆叠:fold 投影(Story 001)之上派生只读视图 `StacksOf(player)`,键取自 21a `StackKey` 定义;合并仅允许同键且 `key != container` 的实例;`stack_max` 是烘焙 int。
2. 容器 = `ItemInstance` 的一种(带 `container_slots` 语义),但 P0 三不变量钉死:qty=1 / quality=1 / children=∅;嵌套写入点在类型上不存在(接口不暴露 children 写)⇒ 用 type-surface 断言代替运行时检查。
3. 折重:承 GDD 结清「空箱自重 + 子件逐件」,即容器自身 weight 行 = 空箱值,子件按各自 `weight×qty` 并入 CarryLoad(与「容器 weight 已含子件」的另一种口径划清,防双重计数 —— TR-inventory-015 的语义即此)。
4. 摆放序纯函数:`col = i mod COLUMNS`,`row = i / COLUMNS`(i = `instance_id` 升序下标);20 出序,42 出 `COLUMNS`/分页 ⇒ 接缝 = `IEnumerable<InstanceRef> PlacementOrder(IReadOnlyList<InstanceRef>)`;「入箱序」措辞废止(BL-22),注释留闭环记录不删原文。
5. AC-20-22(品级不可分视觉):摆放函数不得把两品级栈置于「视觉不可分」位形 —— 本件能自证的半边 = 同 item_key 不同 quality 的两栈在序中**相邻且可区分**(相邻性由序函数性质给出;记号形态归 42,R13 外抛 21a「最小非空」改判请求不在本件)。
6. DTO 反射门:本故事交付 20 全部输出 DTO 的递归扫描绿(承 AC-37-15 递归口径,同一 `PresentationDtoGuard` 件)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001:folds/投影;Story 004:CarryLoad 与容量裁决(折重只给定义件)
- Story 005:消耗转移原子性(堆叠扣减的多栈形状 ✅ OQ-20-10 已于 2026-10-06 裁「相邻多条 N 条」;余 R2 `reason` 值域)
- Story 006:药箱界面、翻页、色盲非颜色通道走查(AC-20-20/23 的呈现半边,BLOCKED-BY-R9)
- skeuomorphic-ui(42)epic:`COLUMNS`/布局、ModalId `InventoryContainer` 模态、器具第三材质档(木/皮/铜)元件
- item-database epic:`StackKey` 与 `weight` schema 本体(21a F4)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-20-04**: 堆叠键唯一性。
  - Given: 合成实例集(同 key 同 q ×3、同 key 异 q ×2、容器 ×2)。
  - When: 建栈。
  - Then: 栈数 = 3(同 key 同 q 合一;异 q 分栈;容器各自独栈);`StackKey` 符号在 20 程序集唯读自 21a(零本地定义)。
  - Edge cases: quality=1 边界;`stack_max` 满 ⇒ 溢出为新栈(禁跨键合并)。
- **P0 三不变量**。
  - Given: 类型面 + 构造器。
  - When: 反射断言。
  - Then: 容器实例 qty/quality 恒 1 且无写入口;`children` 恒空且无 add 路径;`build_part` quality 恒 1(夹具注入异值 ⇒ 装载即拒)。
  - Edge cases: P1a 夹具反证 —— 解除不变量应只需数据/接口扩展不改判定代码(承 building AC-23-15 同型判据的形状)。
- **折重不变量**。
  - Given: 空箱自重 w0、子件 (w1,q1)…(wn,qn)。
  - When: 求折重。
  - Then: `= w0 + Σ wi×qi` 整数逐位;拆出再折回等值(可逆性);无「子件重量既在箱内又在手上」的双计路径。
  - Edge cases: 空箱(Σ=0 ⇒ w0);0 qty 子件栈不可达(fold 不产空栈,承 EC)。
- **摆放序 + AC-20-12**。
  - Given: 箱内容 id 集乱序输入;两品级同种器物。
  - When: 求摆放序/落位。
  - Then: 序 = id 升序(与入箱历史无关,「入箱序」废止的正反两面都测:同一集合乱序喂 ⇒ 同序;不同喂入顺序 ⇒ 同序);无按属性重排路径(扫描摆放函数体:零 sort-by-anything);异品级两栈相邻可分。
  - Edge cases: COLUMNS=1(单列退化);空箱 ⇒ 空序。
- **AC-20-15**: DTO 递归零 float。
  - Given: 20 对外 DTO 全集。
  - When: `PresentationDtoGuard` 递归扫描(含集合泛参/基类)。
  - Then: 零 float;负样例注入必红。
  - Edge cases: `CarryRatioDto` 两字段均 int(Story 004 交付,本扫描覆盖)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/Inventory/stacking_container_rules_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(投影),item-database story-002/004(Complete;`StackKey`/F4 重量件),Story 004 共件(整数加法)
- Unlocks: Story 004(CarryLoad 消费折重口径),Story 006(摆放序 = 药箱落位输入)

---

## Completion Notes

*(placeholder — to be filled at story completion)*
