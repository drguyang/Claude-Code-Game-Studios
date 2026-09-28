# Story 006: 拆除判定、返还舍入例外与 P0 范围门

> **Epic**: 模块化建造
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 5h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/modular-building.md`(规则六 P0 范围 · 规则九 拆除返还 · F-23-3 · EC-23-02/03/08/11 · AC-23-07/14/16/18)
**Requirement**: TR-building-009(Refund 截断例外未回写 ADR-006 守恒律 ⚠️ partial —— 回写轮义务:把「`⌊cost×R⌋` 向 −∞ 截断 = ADR-006 `ROUND_HALF_AWAY_FROM_ZERO` 的显式例外注」落 ADR-006 正文;本故事交付机制与注的权威措辞,不自宣 registry 结案)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-006(主,§单一舍入模式): 全案唯一舍入 = `ROUND_HALF_AWAY_FROM_ZERO`,**本式是显式登记例外** —— `Refund = ⌊cost × R⌋` 向 −∞ 截断(评审 B6:操作数全非负、无 ties ⇒ 行为等价安全,**但须显式注为例外**,否则实现者会「纠正」成 `Math.Round` 违反禁浮点)· ADR-015(次,§三): 单一整数格(拆除释放格集 = 整数格)· ADR-009(次,Amendment J): `StructureRemoved` 三支之一;返还失败走**世界流 `DropSpawned`**(既有 Kind,发出者 = 20 结算、23 触发,EC-23-11)· ADR-010(次): Removed 载荷无 orientation/variant ⇒ 释放格集从实例表回查
**ADR Decision Summary**: 拆除判定:不存在 `structure_id` / **格上有实体(玩家/敌人)** ⇒ 拒绝且不 Append(实体判定 = 量化格 + 宽容半径,**零 Physics**,与 Story 002 条件⑤ 同件复用);返还 `Refund(m) := ⌊cost(m) × DEMOLISH_REFUND_RATIO⌋`,Q16.16 整数求值 `= (cost × rawR) >> 16`,**P0 R = 1**(用户裁定,全额返还,纯重排净零材料损耗;代价走拟物轴 `BUILD_TIME`/停诊,归 2/24 侧);返还入库走 20 标准路径,**失败 = 掉落地上(`DropSpawned`),不静默吞**;P0 范围门:仅家具/设施可拆,**外壳(墙/地基/房顶)不可动** —— 外壳识别经骨架 `SlotType` 标记(非名字清单);唯一新建例外 = 序章帐篷(Story 001 数据承载);**无原子 swap**(先拆后放,EC-23-02 一步不可达即拒);禁嵌套容器(承任务规则/20 BL-7③,本件侧 = build_part 无容器语义路径)。AC-23-18:联机冲突裁决 = 主机唯一(EC-23-09),P1b 语义登记不改 P0 判定形状。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 判定与舍入全在门 A 整数层;`>> 16` 移位语义须对齐 ADR-005 负值右移纪律(操作数非负 ⇒ 无算术/逻辑移位分歧,测试仍钉边界);与 20 的入库交接为进程内接口(Sim 同侧),零引擎调用。

**Control Manifest Rules (this layer)**:
- Required: 舍入例外**显式注**(代码注释 + 本故事文档 = 回写轮的权威措辞来源);失败具名原因枚举(NotFound / EntityOnCell / ShellSlot / NotRefundable 路径分叉);返还走 20 标准入库 API(23 不发明入库逻辑,只触发);`R=1` 时 `Refund = cost` 恒等(P0 全绿基线)
- Forbidden: `Math.Round` / float 中间量 / 借用 `Fix` 舍入件做「纠正」;名字字符串匹配识别外壳(必须 SlotType 标记);拆除即新建的 swap 路径(无原子 swap 已裁);返还失败静默吞(丢材料);嵌套容器进 build_part 路径
- Guardrail: `⌈1/R⌉` 目录校验与 `⌊cost×R⌋` 求值成对测同一 rawR 集(Story 001 校验 ↔ 本件求值,两式共用整数式定义,禁两处各抄);P1a `R<1` 用例全走合成夹具(真实 `R` 值归用户数值轮,本件不拍板)

---

## Acceptance Criteria

*From GDD `design/gdd/modular-building.md`, scoped to this story:*

- [ ] **AC-23-07**: `GIVEN` `StructureRemoved`(EC-23-02/03 情形),`THEN` 不存在 `structure_id` / **格上有实体(玩家/敌人)** 时**拒绝且不 Append**;实体判定 = 量化格 + 宽容半径,**零 Physics 调用**(反射/IL 扫描复用 Story 002 执法门);P0 无原子 swap ⇒ 意图链「拆 A 放 B」一步不可达即整体拒(不部分执行)
- [ ] **AC-23-14**: `GIVEN` P0 医馆,`THEN` 玩家不可动外壳(墙/地基/房顶)—— 外壳识别经**骨架 `SlotType` 标记**(非名字清单);`remove_cases.json` 含「对 shell 格发拆除意图 ⇒ 拒绝且不 Append」用例;外壳作为 `BakedInitial` 占用参与合成(P1a 拆墙 = 移除基线占用,本件只证 P0 拒)
- [ ] **AC-23-16**: `GIVEN` 拆除返还,`THEN` `Refund = ⌊cost × R⌋` 整数向下取整(`(cost × rawR) >> 16`),失败走 `DropSpawned`(EC-23-11)**不静默吞**;载体 = `refund_cases.json` 含「20 库存满」用例 + spy-sink 断言 `DropSpawned` 存在;⚠️ **`DropSpawned` 发出者 = 20 结算(23 触发),该半边测试归 20 侧文件**(inventory-items Story 005 交接对偶)
- [ ] **AC-23-18** [ADVISORY]: `GIVEN` 联机 P1b 设想,`THEN` 冲突裁决语义(主机唯一执行 Remove/返还,EC-23-09)已登记且**不改 P0 判定形状** —— 语义登记核对(文档断言:P0 判定函数无「客户端预判」参数位)
- [ ] **舍入例外注(TR-building-009 交付面)**: `GIVEN` F-23-3 求值代码,`THEN` 向 −∞ 截断以**显式注释登记为 ADR-006 舍入模式的例外**(操作数全非负、无 ties ⇒ 等价安全);EC-23-08:`cost=1 ∧ R<1 ⇒ Refund=0` 为预期行为(消耗性家具经目录 `refundable=false` 声明,P0 R=1 不触发)。**registry 改 covered 归回写轮,本故事不自宣**

---

## Implementation Notes

*Derived from F-23-3(主)/ 规则六/九:*

1. 拆除谓词链:`Found(structure_id) ∧ ¬Shell(SlotTypeOf(anchor)) ∧ ¬EntityOnCells(释放格集) ∧ 单步可达(无 swap)` —— 逐条具名失败原因;释放格集 = 实例表回查当前朝向(交叉 Story 005 边案例);通过 ⇒ `Append(StructureRemoved{id, cell, module_id})`(Story 003 写权)→ Overlay 清位(Story 004)→ 返还结算。
2. 返还求值:`refund = (cost × rawR) >> 16`(int×int→中间量注意 64 位宽,cost ≤ 目录上限 ⇒ 无溢出域,注在夹具边界用例);**P0 `rawR = 65536` ⇒ refund = cost 恒等测试**;`refundable = false` 目录位 ⇒ refund 路径整体跳过(掉物语义归 20/25 既有,不新立)。
3. 入库交接:调 20 的标准入库 API(整数件数,`build_part` item_key)⇒ 满则 20 侧走掉落(`DropSpawned`,发出者 20);23 侧断言 = 「触发点存在且必达」(spy-sink 两侧对齐:23 记账不含「吞掉」分支)。
4. 外壳识别:`SlotType` 枚举位(Story 001 数据)判定,禁字符串;帐篷例外槽 = `tent` SlotType,放置走正常三支(Story 002 判定① 允许格集已含),拆除仍按模块属性(帐篷 = 可拆家具类,P0 序章后不留永久规则洞)。
5. AC-23-18 登记形:判定函数签名审查(无 client-side 预判参数)+ 文档链接(EC-23-09 主机唯一,承 ADR-005/011 Amendment B 同构);**不改形状不预实现联机**。
6. 舍入例外注释模板措辞(回写轮直接取用):「`⌊cost×R⌋` 向 −∞ 截断,为 ADR-006 §单一舍入模式的显式例外:操作数全非负、无 ties 场景,与 ROUND_HALF_AWAY_FROM_ZERO 行为等价;禁实现者改回 Math.Round」。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002:放置五条件(拆除是其对偶,实体判件复用)
- Story 003:Removed 载荷/写权/实例表(本件是其调用方之一)
- Story 004:Overlay 清位与即刻重算(EC-23-04 呈现侧自然下落归 1)
- Story 005:旋转(释放格集按当前朝向的函数在其侧)
- inventory-items epic(Story 001/005):入库结算与 `DropSpawned` 发出本体
- item-database(21a):目录 `refundable`/`cost` schema 值
- 2/24:`BUILD_TIME` 与停诊的拟物代价承担(规则九裁定「代价走拟物轴」,非本件)
- 45/ADR-001:联机冲突裁决实现(P1b;本件仅 AC-23-18 语义登记)
- ADR-006 正文回写:TR-building-009 partial 的结案动作(回写轮)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-23-07**: 拆除拒绝矩阵。
  - Given: `remove_cases.json`:id 不存在 / 玩家格上 / 敌人格上 / 拆 A 放 B 而 B 位被占(swap 不可达)/ 正例。
  - When: 执行拆除意图。
  - Then: 各拒例 false + 原因具名 + spy-sink 零事件;swap 例不部分执行(流中无半程状态);正例恰一条 Removed。
  - Edge cases: 实体在释放格集旋转后的新增格(交叉 Story 005 朝向);宽容半径边界取等;13 病人在格上 ⇒ 不拒(与放置同口径,反证夹具)。
- **AC-23-14**: 外壳门。
  - Given: shell SlotType 格(墙/地基/房顶各一)+ 名字伪装夹具(模块名含「墙」但 SlotType 非 shell)。
  - When: 拆除意图。
  - Then: shell 三例拒且不 Append;名字匹配**不生效**(只有 SlotType 标记被读);外壳占用参与 Overlay 基线断言(承 BakedInitial)。
  - Edge cases: 帐篷槽拆除(可拆,非 shell);P1a 夹具中 shell 标记移除 ⇒ 同码可拆(零代码回归位)。
- **AC-23-16 / 舍入例外**: 返还求值与失败。
  - Given: `refund_cases.json`:P0 `rawR=65536` 全表 / 合成 `rawR∈{32768, 1, 65535}` / `cost ∈ {1, 3, 1000}` / 「20 库存满」夹具 / `refundable=false` 表行。
  - When: 拆除结算。
  - Then: `(cost×rawR)>>16` 逐值 = 独立期望表(含 R=1 恒等、R<1 向下截断、cost=1∧R<1⇒0 预期不报错);满箱例 ⇒ spy-sink 见 `DropSpawned`(23 触发点必达,发出者断言归 20 侧文件);`refundable=false` ⇒ 零返还零掉落(路径整体跳过)。
  - Edge cases: `Math.Round` 注入(负样例必红);移位与 `⌈1/R⌉` 式共用 rawR 集一致性;64 位中间量边界(大 cost 合成值)。
- **AC-23-18** [ADVISORY]: 形状不变性。
  - Given: 拆除/返还判定函数签名与参数集。
  - When: 语义登记核对(代码审查清单 + 反射签名断言)。
  - Then: 无客户端预判/墙钟/表现态参数位;EC-23-09 主机唯一口径在文档链接可溯。
  - Edge cases: 未勾走查 ≠ 阻塞本件其余 BLOCKING 项。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/Building/demolish_refund_and_scope_gates_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(SlotType/cost/refundable 数据),Story 002(实体判件复用),Story 003(Removed 写权),Story 004(清位),Story 005(当前朝向格集),inventory-items Story 001/005(入库与 DropSpawned 交接)
- Unlocks: 序章帐篷闭环的拆除侧(48/tutorial 联动验证);TR-building-009 回写轮(交权威措辞);数值轮 `R<1` 翻真值路径

---

## Completion Notes

*(placeholder — to be filled at story completion)*
