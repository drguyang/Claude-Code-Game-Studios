# Story 004: 载重裁决 CanCarry、硬不变量与读数契约

> **Epic**: 库存与物品
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 5h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-26

## Context

**GDD**: `design/gdd/inventory-and-items.md`(规则四 F-20.1 / F-20.2 · 边例 CARRY_CAP 未定值 · BL-26/27 重写行 · UI-20.3 读数半边)
**Requirement**: TR-inventory-005(CarryLoad = Σ weight×qty,整数先乘后加,weight 是 int)· TR-inventory-006(CanCarry ⟺ CarryLoad + InstanceWeight ≤ CARRY_CAP + 硬不变量 ∀p CarryLoad ≤ CARRY_CAP,❌ gap —— CARRY_CAP 值未裁 ⇒ 不变量无值可验)· TR-inventory-013(CARRY_CAP 未定值 ⇒ 装载期硬失败,禁 null 兜底)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-006(主): weight int 口径(D-21-17)+ 整数域求值 · ADR-014(次): 装载期硬失败纪律(E-13 同型)+ 烘焙期常量 · ADR-013(次): 42 只渲染不持状态 ⇒ `CarryRatioDto` 是 20 唯一的载重读数出口
**ADR Decision Summary**: `CarryLoad(p) = Σ weight×qty`(int64,先乘后加);`CanCarry ⟺ CarryLoad + InstanceWeight ≤ CARRY_CAP`;**硬不变量** `∀p: CarryLoad(p) ≤ CARRY_CAP` 必须恒成立(52 的 `clamp(CarryLoad/CARRY_CAP, 0, 1)` 归一化定义域依赖它,否则爆界);`CARRY_CAP` **单源归 20**(BL-6①,52 侧 R5 义务);值未裁(OQ-20-2)⇒ 未定时装载硬失败;满档 ⇔ `carry_load > cap − W_MIN`(**禁简化 `== cap`**,W_MIN = 烘焙期 `min weight`);BL-27 裁定「声明单调契约,不新增形状」⇒ 20 **零** `RemainingSlack` 类派生字段,`slack` 由 42 自算;AC-20-21 存在性门 = 自指谓词(`CARRY_CAP > 0 ∧ ∃可达 s: CanCarry == false`)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯整数比较与求和,门 A;构建期存在性门(AC-20-21)为烘焙期纯计算,零引擎 API;跨平台整数溢出定义性(ADR-005 Amendment G hi/lo 纪律)仅在乘积超 int64 前域适用,溢出界由 AC-20-10 烘焙常量保证。

**Control Manifest Rules (this layer)**:
- Required: `CARRY_CAP` 单一出处(20 烘焙常量,52 经读数消费,零第二容量真值);W_MIN 烘焙期可算常量(`min{weight}`,weight ∈ int>0 ⇒ W_MIN ≥ 1);`CarryRatioDto = {carry_load:int, cap:int}` 全整数;装载路径对未定值 ⇒ 具名硬失败
- Forbidden: `CarryLoad` 经 float/double 求和或除法(52 侧的归一化除法降级义务 = R5,归 52);20 出 `RemainingSlack` / 「视觉容量」类字段(BL-27);把满档判据写成 `carry_load == cap`(BL-26 明令禁止的简化);存在性门外求「出诊最小集」(BL-24 已判全案无定义 ⇒ 自指谓词替代)
- Guardrail: `CARRY_CAP` 值归用户数值轮 ⇒ 本故事用**合成临界 cap** 测形状与不变量,真实 cap 落地后同测试体翻真值(AC-18-25 型「形状义务不豁免」纪律);OQ-20-7(CarryLoad vs DrugLoad 语义归 52)不影响本条,只读数不判语义

---

## Acceptance Criteria

*From GDD `design/gdd/inventory-and-items.md`, scoped to this story:*

- [x] **AC-20-08**: `GIVEN` F-20.1,`WHEN` 静态检查,`THEN` 零浮点;`weight` 参与整数乘加(门 A / ADR-006)
- [x] **AC-20-09**: `GIVEN` 任意操作序列,`WHEN` 单测,`THEN` `∀p: CarryLoad(p) ≤ CARRY_CAP` 恒成立(52 的归一化定义域,F-20.2)
- [x] **AC-20-13**: `GIVEN` `CARRY_CAP` 未定值,`WHEN` 装载,`THEN` 硬失败(非 null 兜底 —— ADR-014 §五 纪律)
- [x] **AC-20-18**(二轮重写 BL-26):`GIVEN` `CarryRatioDto = {carry_load, cap}` 整数对且 `W_MIN = min{weight}`,`WHEN` 比对呈现与机制,`THEN` 「新增一律放不进」⇔ `carry_load > cap − W_MIN` 一一对应;20 出谓词、42 出分档,两侧不各自判载重
- [x] **AC-20-21**(二轮重写 BL-24):`GIVEN` 烘焙期 `CARRY_CAP`,`WHEN` 构建门求值,`THEN` `CARRY_CAP > 0` 且存在某状态 `s`:`CanCarry(s) == false` 且 `s` 从开档经 `DropClaimed` 可达(自指谓词,不需要外部清单);「出诊最小集」出处另登 OQ-20-9,不记本件
- [x] **AC-20-24**(BL-27 新增):`GIVEN` 20 全部对外读数面,`WHEN` 静态检索,`THEN` 只存在一个容量真值 `CARRY_CAP`,零第二容量常量 / 零「视觉容量」字段 / 零 `RemainingSlack` 类派生字段(slack 由 42 自行 `cap − carry_load`)

> ⚠️ **AC-20-21 勾选的确切含义**(禁把它读成「取舍存在性已全面验证」):本件兑现的是
> **判据本体 + 构建门接线 + 四条判据(A/B/C/D)各一例夹具**。该门受 GDD `:705-712` 自陈的
> **弱门性质**限制 —— 领取次数无上界 ⇒ 对任何有限 cap 几乎恒绿,**抓不到「cap 大到能带齐一切」**;
> 后者须等 OQ-20-9 的具名 `ESSENTIAL_KIT` 才立得了真门。详见 §Completion Notes 的「四条限制」。

---

## Implementation Notes

*Derived from ADR-006 §一(主)/ ADR-014 §五:*

1. `CarryLoad(p)` 从 Story 001 投影累加:`for each stack: sum += (long)weight × qty`(先乘后加,int64 中间量);溢出界预检承 AC-20-10 烘焙常量(TR-inventory-010 partial ⇒ 本件做运行期防御断言半边 + 值班方外抛登记)。
2. `CanCarry(p, candidateInstance)` 纯谓词;所有写入路径(Story 005 的原子装卸)只经由它,零「容量差不多」的第二实现;硬不变量在 Apply 入口后置断言(前态满足 + 谓词通过 ⇒ 后态仍满足,归纳闭合)。
3. `W_MIN` = 烘焙期常量(min over `ItemDef.weight`),与 `CARRY_CAP` 同批装载;满档谓词 `IsFullForAnyAddition = carry_load > cap − W_MIN`(减法防负,整数)。
4. `CarryRatioDto` 仅两 int 字段;**不**提供 slack / 百分比 / 分档枚举(BL-27:单调契约以「声明 + 42 自算」实现,20 侧零形状膨胀)。
5. 装载路径:`CARRY_CAP` 缺省/0/未烘 ⇒ 具名异常终止该表(镜像 foraging AC-17-08b 硬失败族,同一异常风格)。
6. 存在性门(AC-20-21)= 构建期跑一遍「开档可达状态空间」搜索(合成物品集下可判;真实集待内容轮 ⇒ 门本体先接,结果登记):搜索以「能装满」为目标,证明非空洞;若真实数据下永不可满,构建告警(取舍不存在 = 设计缺陷,非本件 bug)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 005:拒绝语义的信令侧与原子回滚(本件只给谓词真值)
- Story 006:四档分档函数与满溢感呈现(R9 的 42 半边)
- 52 随机事件:`clamp(CarryLoad/CARRY_CAP)` 归一化与 float 除法降级(R5)
- random-events / clinic-machine:cap 的数值(OQ-20-2 归用户数值轮)
- 7a:容量常量的存档头联动(ConfigVersion,既有件)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-20-08**: 零浮点静态门。
  - Given: 20 的载重路径源码。
  - When: Roslyn 类型签名 + 字面量扫描。
  - Then: weight 乘加全整数;`/` 不出现在载重路径(比值不属 20)。
  - Edge cases: `long` 溢出哨兵夹具(临界 qty×weight 逼近 2^31/2^63);负 weight 非法输入 ⇒ 装载拒(weight ∈ int>0 前提)。
- **AC-20-09**: 不变量随机序列。
  - Given: 固定种子伪随机操作序列(拾取/放下/消耗/炮制产出入库,1000 步),合成 cap。
  - When: 每步后断言。
  - Then: `∀p: CarryLoad ≤ cap` 恒真;任何绕过 `CanCarry` 的写入注入必红。
  - Edge cases: cap=1 极小、W_MIN=2(临界「一律放不进」带);恰等界(= cap 合法)。
- **AC-20-13**: 未定值硬失败。
  - Given: 注入缺 `CARRY_CAP` / cap=0 / 非 int 字符串三夹具。
  - When: 装载。
  - Then: 各抛具名异常;无默认值路径存在(反射扫默认常量 = 零)。
  - Edge cases: cap 为 Fix 字面量("3/4")⇒ 拒(cap 是 int 计数口径,承 D-21-17 邻域纪律)。
- **AC-20-18**: W_MIN 满档等价。
  - Given: 扫描线 `carry_load` ∈ {cap−W_MIN−1, cap−W_MIN, cap−W_MIN+1, cap};W_MIN>1 的表。
  - When: 逐位比对 `CanCarry(任意候选)` 全 false 区间与谓词。
  - Then: 等价关系在 `> cap − W_MIN` 精确成立,且在 `== cap−W_MIN` 处**仍可放**(证明未简化成 ==cap)。
  - Edge cases: W_MIN=1 时退化为 `> cap−1`,两式同(等号侧仍正确)。
- **AC-20-21**: 存在性门。
  - Given: 烘焙期 cap + 合成物品表;可满的状态经 `DropClaimed` 序列可达。
  - When: 构建门求值。
  - Then: cap>0 且 ∃不可 CanCarry 可达状态(门真绿);注入永不可满的表 ⇒ 门红(设计缺陷信号)。
  - Edge cases: 空物品表 ⇒ 门红(空洞);真实表未冻结 ⇒ 合成表驱动,AC 记形状绿、内容待数值轮(与 Story 003 合成夹具纪律同)。
- **AC-20-24**: 单源与零派生字段。
  - Given: 20 对外符号面。
  - When: 检索容量常量与 DTO 字段名/类型。
  - Then: `CARRY_CAP` 唯一定义点;零 slack/百分比/视觉容量字段;负样例(本地加 `RemainingSlack`)必红。
  - Edge cases: 42 自算的减法在 42 程序集,不计违例(判据 = 20 侧字段存在性)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/Inventory/carry_load_adjudication_test.cs` — must exist and pass

**Status**: [x] Created and passing — **54/54 passed / 0 failed / 0 skipped**
（`unity/Logs/s20-12-c.xml`，2026-10-26 实测）

### 夹具分布（54 条，零 `[Ignore]`）

| AC | 条数 | 覆盖要点 |
|---|---:|---|
| AC-20-08 | 5 | 源码面剥注释扫 float/double/decimal + 转换指令 · Cecil IL 面扫 `conv.r4/r8` · `Func<int,int>` 签名 · `CarryCapacity(Fix)` 恒拒 |
| AC-20-09 | 9 | 1000 步后置断言（归纳闭合）· **反向夹具**（绕过 `CanCarry` ⇒ 必红）· 恰等界接受/超一件拒绝 · 负负载重 · 空库存 · weight×qty · 他主排除 · 未知 ordinal 硬失败 |
| AC-20-13 | 10 | 缺键 / 0 / 负 / 越 int / 字符串 / Fix 字面量 / 浮点 各一条**具名错误**· ctor 三态 · 生产代码零默认容量常量 |
| AC-20-18 | 10 | W_MIN>1 四点扫描 · 全表扫描（`load ∈ [0, cap+1]` × 每件）· W_MIN=1 退化 · `MinWeight` 三态 · DTO 恰两字段 |
| AC-20-21 | 9 | 真数据绿 · 空表/全非法重量/非正 cap 各红并点名判据 · 单件重量表可达 · 首件即超 cap · 取数适配器 fail-closed · 门已接线 |
| AC-20-24 | 5 | 恰两字段 · 无派生/小数字段（禁词 slack/remain/percent/ratio/visual/capacity）· 无 `RemainingSlack` · `CarryLoad.cs` 零 `const int *Cap*` · `carry_cap` 仅在数据文件 |
| 容器两路 | 6 | 单层 · 显式子件表并入未归属子件 · 双计上报（同主）· 双计上报（**跨主**）· 孤儿计入 · null 表 ≡ 单层 |

### Falsifiability Gate(S12-4) —— 命中形态与反证记录

**命中形态**:② 共存互斥 · ③ 定义域收窄 · ⑤ 阈值边界

| # | 变异 | 靶点 | 反证:停用/改坏什么 ⇒ 哪条红 |
|---|---|---|---|
| M1 | `CanCarry` 的 `≤` 改 `<` | `CarryLoad.cs:316` | 恰等界不再合法 ⇒ `exactCapBoundary` + `fullPredicate` 红 |
| M2 | 满档简化成 `== cap`（BL-26 明令禁） | `CarryLoad.cs:343` | W_MIN>1 时 `cap−1` 误报未满 ⇒ 2 条红 |
| M3 | 去掉 `− wMin` | `CarryLoad.cs:343` | 减数丢失 ⇒ 3 条红 |
| M4 | 停用负载重守卫 | `CarryLoad.cs:311` | 负值静默参与比较 ⇒ 1 条红 |
| M5 | null 兜底成 0 | `CarryLoad.cs:162` | 「未注入」与「填了 0」不可分 ⇒ 1 条红（**曾有假绿，已修**） |
| M6 | 放行非正 cap | `CarryLoad.cs:166` | 值域分支 ⇒ 1 条红 |
| M7 | 放行字符串 token | `Binder.cs:174` | `"60"` / `"3/4"` 都过 ⇒ 2 条红 |
| M8 | 放行浮点 token | `Binder.cs:174` | 恰等界不可判 ⇒ 1 条红 |
| M9 | 放行零容量 | `Binder.cs:204` | 1 条红 |
| M10 | 放行负容量 | `Binder.cs:210` | 1 条红 |
| M11 | 缺载体当 0 | `CarryLoad.cs:288` | 未知 ordinal 可无限带 ⇒ 1 条红 |
| M12 | 双计静默求和 | `CarryLoad.cs:269` | 2 条红（本主 / 他主两向） |
| M13 | 孤儿静默丢弃 | `CarryLoad.cs:261` | 1 条红 |
| M14 | 停用门判据 A | `Gate.cs:121` | cap≤0 不再红 ⇒ 1 条红 |
| M15 | 停用门判据 B | `Gate.cs:129` | 空表不再点名 B ⇒ 1 条红 |
| M16 | 门判据 D **取反** | `Gate.cs:168` | 首件即满的场景假绿 ⇒ 1 条红 |
| M17 | 门判据 D **找到了也不返回** | `Gate.cs:169` | 3 条红（**M16 暴露的逃逸面由此补上**） |

**Gate 结果**: **PASS** —— 第 5 轮 **17/17** 全部恰如预期；SHA-256 还原逐字节校验通过；受保护路径（`AddressableAssetsData` / `Audio` / `DataCooked`）指纹未变。

**spec**: `tools/falsify/specs/s20-12-carryload-adjudication.json`

### 五轮演进（不掩盖）

| 轮 | 结果 | 原因与处置 |
|---|---|---|
| 1 | 10/13 | **M5 真逃逸**（54/54 全绿）—— 夹具只断异常类型，null 兜底成 0 后落到下游 `cap.Value <= 0` 仍抛同型异常 ⇒ 断言照绿。→ 修夹具（按消息区分三分支）。另 M1/M3 多红（正确），补 `expectRed` |
| 2 | **13/13** | M5 修好后转红（红 1 条，正是它） |
| 3 | 15/16 | 补 `CarryCapExistenceGate` 的 3 条变异后：**M16 的 `singleUnitWeightTable` 逃逸** —— 该夹具 cap=10/w=1 首步可装，取反后立即返回绿 ⇒ 对「是否真装满」零判别力。→ 补 M17 打「提前返回」属性 |
| 4 | 16/17 | M17 多红 `realData_gateIsGreen`（正确：真数据 cap=60/wMax=2，正常第 30 步返回，变异后一路到 `stepBound=62` 才报 D），补 `expectRed` |
| 5 | **17/17** | 全绿 |

> **五轮里只有两次是真正的假绿**（M5 / M16），其余「多红」都是同一被变异函数被**另一批夹具独立覆盖**（加强），是我在 `expectRed` 里漏列 —— 每次均写明变红原因，未以「加强」二字含糊带过。

---

## Dependencies

- Depends on: Story 001(投影累加源),Story 003(折重口径 —— 容器并入 CarryLoad),item-database story-002(int 口径件,Complete)
- Unlocks: Story 005(全部写入路径的裁决谓词),Story 006(四档呈现的整数输入),52 侧归一化(读数消费方)

---

## Completion Notes

**Completed**: 2026-10-26
**Criteria**: **6/6 全部记绿**（AC-20-08 · AC-20-09 · AC-20-13 · AC-20-18 · AC-20-21 · AC-20-24）
· 但 AC-20-21 的记绿**有确切边界**，见下「四条限制」

### 交付面

| 文件 | 性质 |
|---|---|
| `unity/Assets/Sim/ItemDatabase/CarryLoad.cs` | 新建 · 387 行 · 门 A 纯 C#（`CarryCapacity` / `CarryRatioDto` / `CarryLoadSolver` / 两个具名异常） |
| `unity/Assets/Editor.Tools.Bake/InventoryConstantsBinder.cs` | 新建 · ADR-014 阶段 2 绑定层（AC-20-13 的构建期执行体） |
| `unity/Assets/Editor.Tools.Gates/CarryCapExistenceGate.cs` | 新建 · AC-20-21 存在性门（判据本体为纯函数 + fail-closed 取数适配器） |
| `assets/data/inventory_constants.json` | 新建 · 作者态数据件（**只有 `carry_cap` 一个键**） |
| `unity/Assets/Tests/EditMode/Inventory/carry_load_adjudication_test.cs` | 新建 · **54 条夹具** · 零 `[Ignore]` |
| `tools/falsify/specs/s20-12-carryload-adjudication.json` | 新建 · **17 条变异** spec |
| `unity/Assets/Editor.Tools.Gates/AssemblyGates.cs` | 修改 · +1 行接线 + 注释 |
| `unity/Assets/Tests/EditMode/Sim/gate_wiring_manifest_test.cs` | 修改 · 两处台账登记 |
| `docs/architecture/tr-registry.yaml` | 修改 · `TR-inventory-006` gap→covered · `TR-inventory-010` requirement 订正 |

### ⛔ 四条限制（必须与「6/6 记绿」同读，否则是虚报）

**① AC-20-21 是弱门 —— GDD 自己承认的。** `design/gdd/inventory-and-items.md:705-712` 明写：
「存在某状态使 `CanCarry == false`」在**领取次数无上界**的前提下，对任何有限 cap 都成立。
本门真正能抓的只有三类**结构性**空洞（cap ≤ 0 / 空物品表 / 全表 weight < 1）；
**「cap 大到能带齐一切」那种程度性缺陷抓不到** —— 须等 GDD 登记的 **OQ-20-9**（具名 `ESSENTIAL_KIT`）
才立得了真门。该限制已在**四处**登记：门文件头 · 门自己的失败消息 · `gate_wiring_manifest_test.cs`
的 `ClassVerdict.Note` · `AssemblyGates.cs` 调用点注释。

**② 构建期执行 = `NOT-RUN`。** `CarryCapExistenceGate` 在 `AssemblyGates.RunAll` 里的调用
**只有源码级证据**（`AssemblyGates.cs` 的调用行 + `gate_wiring_manifest_test.cs` 的台账登记）。
**未跑探针轮** —— 按 `CLAUDE.md` §BuildGate「构建绿 ≠ 门跑了」，本门在构建期的执行**不记绿**。
门生效的确认须另跑探针轮（门内插无条件 `errs.Add("[PROBE]…")` ⇒ 要求构建 `Failure`）。

**③ `test_ac2021_singleUnitWeightTable_reachesFullState` 判别面受限。** 由 M16 实测证明：
该夹具（cap=10 / w=1）在首步即可装，门取反后会**提前返回绿** ⇒ 它只证明「门不误报红」，
**不证明**「门真的领满了 10 件」。后者由 M17 + `heaviestSingleItem` 承担。

**④ AC-20-08 的 IL 面只在 Mono 后端。** 夹具用 Cecil 扫 `Sim` 装配的 `conv.r4/r8`；
**IL2CPP 生成物与 Mono IL 不同**，跨平台逐位须走 ADR-012 的三格矩阵，**不在本件判据内**。

### 与 GDD 的 ADVISORY 偏差（1 项）

| # | GDD 写 | 实现 | 性质 |
|---|---|---|---|
| 1 | `CarryRatioDto = { carry_load: int, cap: int }`（`inventory-and-items.md:302`） | `public readonly long CarryLoad;`（int64） | **ADVISORY**。GDD `:452-459` 已把 `CarryLoad` 类型**升为 int64**（与 `AC-21a-64` 对齐），但 `:302` 的 DTO 行未同步更新。实现取了**较新**的口径（int64），**未改 GDD** —— 建议后续把 `:302` 的 `int` 订正为 `int64`，或明确 DTO 出口做窄化（当前**未窄化**，`long` 直出）。 |

### 对下游的契约承诺（Story 005 / 006 / 52 消费方须知）

| 承诺 | 内容 | 消费方须注意 |
|---|---|---|
| **E-1** | `CarryLoadSolver.CanCarry(long currentLoad, int unitWeight, int qty, CarryCapacity)` —— **纯谓词**，不写状态 | Story 005 的全部写入路径**只经由它**；禁第二实现 |
| **E-2** | 恰等界合法（`≤` 非 `<`）：`load + w == cap` **允许** | 005 的拒绝语义须以此为准，`== cap` 不得判拒 |
| **E-3** | `IsFullForAnyAddition(load, cap, wMin) ⟺ load > cap − wMin` | **禁简化成 `== cap`**（BL-26）；W_MIN 由消费方自算或取 `MinWeight` |
| **E-4** | `CarryRatioDto` **恰两字段**（`CarryLoad` / `Cap`），**零派生字段** | 42 自行算 `slack = Cap − CarryLoad`；20 不出 slack / 百分比 / 档位 |
| **E-5** | `CarryCapacity` 构造对 null / ≤0 **恒抛**，无默认值路径 | 005 的装配点须显式注入；**不得** catch 后兜底 |
| **E-6** | 容器子件表是**调用方可选口径**（P0 无进出容器事件载体） | 若走 `SumDetailed`，双计与孤儿**会显式上报**（`DoubleCountedChildWeight` / `OrphanChildCount`），不会静默求和 |

### 未落地项（本件明确不做）

| # | 项 | 归属 |
|---|---|---|
| 1 | 运行期 `IDataProvider` 装配（读 `carry_cap`）+ `*.cooked` 产物 | **装配轮**（届时须登记进 `cooked_drift_all_artifacts_test` 的 `BakedArtifacts`） |
| 2 | 原子装卸的全部写入路径 | **Story 005** |
| 3 | 四档呈现 / 分档阈值 / 单调性 | **Story 006 / 42**（R9 ④） |
| 4 | OQ-20-9 具名 `ESSENTIAL_KIT`（真存在性门） | **用户 / 37 / 11** |
| 5 | 构建期探针轮（证明门真被执行） | 下次真机构建轮 |

### 回归数字（带可跑复算命令）

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
head -2 unity/Logs/s20-12-full.xml | grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"'
# 实测:total="5354" passed="5319" failed="0" inconclusive="0" skipped="35"

head -2 unity/Logs/f3-full.xml | grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"'
# 上批基线(2026-10-26 14:36, commit 089006b 之前):5213 / 5178 / 0 / 35
```

**增量分解**（`+141` 中本件只占 `+54`）：

| 测试类 | 条数 | 归属 |
|---|---:|---|
| `CarryLoadAdjudicationTest` | **54** | **本件 S20-12** |
| `StackingContainerRulesTest` | 33 | S20-11 inventory-003（`089006b`，14:36 时未提交） |
| `InventoryFoldTest` | 28 | S20-10 inventory-001（`ebe532e`） |
| `OrdinalBaselineGateTest` | 17 | 物品 ordinal 硬门（`80eeab2`） |
| `BuildModulesBakeTest` | 5 | 模块表材料外键（`40d9cfa`） |
| `HarvestOrchestratorTest` | 4 | foraging 补测（`8bfb861` 之后） |
| **合计** | **141** | |

> 基线说明：`f3-full.xml` 测于 **14:36**，当时 `8bfb861`（foraging，15:07）尚未提交，
> 而 `089006b`（20:03）/ `ebe532e`（18:20）/ `40d9cfa`（16:53）/ `80eeab2`（16:53）四笔
> 都在其**之后** ⇒ 该基线不含这四个 story 的夹具。**任何把 `+141` 全算作本件产出的说法都是借绿。**
