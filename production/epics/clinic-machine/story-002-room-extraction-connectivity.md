# Story 002: 房间析出与连通规则

> **Epic**: 医馆即机器
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28
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

- [ ] AC-24-10:断开两簇的 fixture 整盘返回 ROOM_NONE(单簇 L 形 / T 形正常析出)
- [ ] 4 邻接判据:对角接触(`|dx|==1 ∧ |dy|==1`)不连通(与 8-邻接反例可区分)
- [ ] 外壳格与空地板:不产生房间成员、不阻断两簇判定(壳格内嵌家具时家具照常析出)
- [ ] 开顶(无围合)家具簇照样成房间 —— 算法中无封闭性检查(规则一明令)
- [ ] 房间标识稳定:同一布局两次求值得到同一 room id(纯函数,承 AC-24-04 的析出侧)
- [ ] 输入仅 `structure_at(cell)` 只读视图:24 不调用 23 的写路径、不持建造状态

---

## Implementation Notes

*Derived from ADR-015 Implementation Guidelines:*

1. 输入枚举:对医馆 footprint 内每格调 `structure_at(cell)`(23 只读),得 `module_id + orientation` 的 FURN 格集合 S。
2. 连通分量:4 邻接并查集(或 BFS)对 S 求分量数;`|分量| == 1` ⇒ 该布局成一个房间;`≥ 2` ⇒ 整盘 ROOM_NONE。
3. `ROOM_NONE = (0,0)` 哨兵与真实 room id 空间不交(room id 由布局确定性派生,不含 Unity instance id)。
4. 壳格规则:`BakedInitial`(开档自带)按 GDD 归「非格」—— 从 S 中剔除后不参与连通传导(两个家具簇仅靠壳格相连 ⇒ 仍判 2 簇)。
5. 该纯函数住 `Sim` 程序集(门 A):零 `UnityEngine` 引用;输入类型来自 `Sim.Contracts.WorldPos`。
6. 房间→格 的反向索引(乘子求值按房间取成员)在返回值内一次性构建,不缓存于 24 之外。

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

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/ClinicMachine/clinic_room_extraction_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(cooked 表可读);系统 23 的 `structure_at` 只读契约(GDD 侧已定,实现若未就绪可用 fixture 桩注入 —— 承「依赖注入而非单例」标准)
- Unlocks: Story 003(乘子求值的房间输入)、Story 005(Memoize 失效键基于布局事件)

---

## Completion Notes

*(留空 — story 关闭时回填)*
