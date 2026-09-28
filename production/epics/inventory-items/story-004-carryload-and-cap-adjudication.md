# Story 004: 载重裁决 CanCarry、硬不变量与读数契约

> **Epic**: 库存与物品
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 5h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

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

- [ ] **AC-20-08**: `GIVEN` F-20.1,`WHEN` 静态检查,`THEN` 零浮点;`weight` 参与整数乘加(门 A / ADR-006)
- [ ] **AC-20-09**: `GIVEN` 任意操作序列,`WHEN` 单测,`THEN` `∀p: CarryLoad(p) ≤ CARRY_CAP` 恒成立(52 的归一化定义域,F-20.2)
- [ ] **AC-20-13**: `GIVEN` `CARRY_CAP` 未定值,`WHEN` 装载,`THEN` 硬失败(非 null 兜底 —— ADR-014 §五 纪律)
- [ ] **AC-20-18**(二轮重写 BL-26):`GIVEN` `CarryRatioDto = {carry_load, cap}` 整数对且 `W_MIN = min{weight}`,`WHEN` 比对呈现与机制,`THEN` 「新增一律放不进」⇔ `carry_load > cap − W_MIN` 一一对应;20 出谓词、42 出分档,两侧不各自判载重
- [ ] **AC-20-21**(二轮重写 BL-24):`GIVEN` 烘焙期 `CARRY_CAP`,`WHEN` 构建门求值,`THEN` `CARRY_CAP > 0` 且存在某状态 `s`:`CanCarry(s) == false` 且 `s` 从开档经 `DropClaimed` 可达(自指谓词,不需要外部清单);「出诊最小集」出处另登 OQ-20-9,不记本件
- [ ] **AC-20-24**(BL-27 新增):`GIVEN` 20 全部对外读数面,`WHEN` 静态检索,`THEN` 只存在一个容量真值 `CARRY_CAP`,零第二容量常量 / 零「视觉容量」字段 / 零 `RemainingSlack` 类派生字段(slack 由 42 自行 `cap − carry_load`)

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

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(投影累加源),Story 003(折重口径 —— 容器并入 CarryLoad),item-database story-002(int 口径件,Complete)
- Unlocks: Story 005(全部写入路径的裁决谓词),Story 006(四档呈现的整数输入),52 侧归一化(读数消费方)

---

## Completion Notes

*(placeholder — to be filled at story completion)*
