# Story 006: C1 两层漂移 + C2 可走性同源 + C2' collider 足迹同源 + CI 接线

> **Epic**: 关卡工具(54 · Tooling 层)
> **Status**: Ready
> **Layer**: Tooling
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-28

## Context

**GDD**: `design/gdd/world-and-ecozones.md`(两层世界)· `design/gdd/modular-building.md` 规则八(义务 `O-23-9`)
· `design/gdd/player-controller-and-movement.md`(`AC-1-33②` `slopeLimit` / `stepOffset`)
**Requirement**: 兑现 S20-20 挂标 **#3「C1–C6 七项未实现」**中的 **C1 / C2 / C2'**,
并把整个检查链**接进 CI**

**ADR Governing Implementation**: **ADR-022 §四 C1 / C2 / C2'** · **ADR-015 §一之补**
· **ADR-016 §Validation**(「在关卡工具中存在」)
**ADR Decision Summary**: **C2 可走性同源 = 硬失败**(`|engineParam − Dequantize(bakedInt)| ≤ 1 量子`);
**C2' 模板 collider 足迹同源 = 硬失败**(collider 高度 > `stepOffset` 除非显式声明可走过);
**C1 两层漂移 = 告警**(漂移只影响表现,不退化为构建失败)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: **MEDIUM**
**Engine Notes**: C2 / C2' 须读**引擎侧**真值(`CharacterController` 的 `slopeLimit` /
`stepOffset`;模块 prefab 的 collider)。`CharacterController` 是长期稳定 API(LOW);
**模块 prefab 的 collider 读取**须实测(MEDIUM)。

**Control Manifest Rules (Tooling 层)**:
- Required: C2 / C2' = **硬失败**(`throw`);C1 = **告警**
- Required: 「在关卡工具中存在」是 ADR-015 / ADR-016 两处 Validation 的**执行点**
- Forbidden: **`Debug.Assert` 代替 `throw`**(C2 / C2' 是硬失败)
- Forbidden: **视觉层采样回逻辑判定**(ADR-015 §一:两层纪律)
- Forbidden: **C1 升格为构建失败**(漂移只影响表现,ADR-015 §Risks 同口径)

---

## §病灶 —— 「悬空判据」的确切含义

ADR-022 的立件缘由逐字写着:

> `ADR-015 §Validation:317` 与 `ADR-016 §Validation:442` 的
> 「两层 / 导航格一致性检查**在关卡工具中存在**」这句判据**在架构上悬空** ——
> **没有任何东西被指定为「关卡工具」**。

本 epic 立件后,「关卡工具有了」;但 **C1 / C2 / C2' 三条仍零实现**:

```
"C1 两层漂移(需视觉层数据 —— 本批不产出)",
"C2 可走性同源(需 world_terrain.json —— 本批不产出)",
"C2' 模板 collider 足迹同源(需模块 prefab —— 归 23 素材轮)",
```

⇒ 悬空判据**仍未落地**。S2 让 `world_terrain.json` 存在(C2 的逻辑侧有了),
本 story 才让**比对本身**存在。

**连带后果(实测)**:

| # | 后果 | 依据 |
|---|---|---|
| ① | `AC-1-33②` 无值可比 | 债册 `TD-player-01` 明写该项「归**关卡工具**,系统 1 只是执行者、**无权填值**」 |
| ② | 「两套可走性」风险未受检 | ADR-022 §四 C2' 的理由逐字:玩家不读整数格,**走感全在 collider** ⇒ 两套答案 |
| ③ | 两层漂移无人发现 | C1 是唯一的发现面,且它是**告警**级 —— 现在连告警都没有 |

---

## Acceptance Criteria

- [ ] **AC-1(BLOCKING)** —— **C2 可走性同源 = 硬失败**:
      `|engineParam − Dequantize(bakedInt)| ≤ 1 量子`,对 `slopeLimit` **与** `stepOffset` **各一条**。
      **判据**:① 注入一处超 1 量子的偏差 ⇒ **抛**;② 注入恰 1 量子的偏差 ⇒ **不抛**(边界);
      ③ 注入超 1 量子 +1 单位 ⇒ **抛**。
      ⚠️ **三条缺一不可** —— 只测「超了会抛」时,一个「阈值写得很松」的实现也能通过。
      ⚠️ `slopeLimit` 与 `stepOffset` **各一条夹具** —— 两者是不同的量,不得合并。
- [ ] **AC-2(BLOCKING)** —— **C2' 模板 collider 足迹同源 = 硬失败**:
      collider 高度 > `stepOffset` ⇒ **抛**,**除非**模块显式声明可走过(如地毯)。
      **判据**:① 高 collider 无声明 ⇒ 抛;② 高 collider **有**声明 ⇒ 不抛
      (证明「例外路径可达」,不是「恒抛」);③ 足迹与占用格不一致 ⇒ 抛。
- [ ] **AC-3(BLOCKING)** —— **C1 两层漂移 = 告警**:
      逻辑占用格 ↔ 视觉地形不矛盾。
      **判据**:① 注入漂移 ⇒ **报出告警**;② **不**使构建失败。
      ⚠️ **必须验「报出」** —— 「永远不报」也满足 ②,那是告警类检查的经典假绿。
- [ ] **AC-4(BLOCKING)** —— **CI 接线**:
      整个 C1–C6 检查链可在 **CI 里跑**(不需要打开编辑器 GUI)。
      **判据**:① 一条命令跑完 C1–C6;② 硬失败项以**非零退出码**体现;
      ③ 告警项以**零退出码 + 可读输出**体现。
      ⚠️ **判据须给 `file:line` + 一条能证明它会红的命令**(承
      `coding-standards.md` §「某件有门」的自述纪律)。
- [ ] **AC-5(BLOCKING)** —— **`UnimplementedChecks` 长度 == 0**:
      C1–C6 七项全部实现。**判据**:数组为空 ⇒ 本 epic 的 DoD 达成。
- [ ] **AC-6** —— **两条 Validation 判据落地**:
      `ADR-015 §Validation:317` 与 `ADR-016 §Validation:442` 的「在关卡工具中存在」
      可被指到**具体检查**(C1 / C3)。
      **判据**:两处 ADR 文本各加一条回指注记(指向本 story 的检查实现)。

---

## §Implementation Notes

### 顺序:C2 / C2' 先,C1 后

C2 / C2' 是**硬失败**,它们的判据是**精确的**(≤ 1 量子 / 高度比较);
C1 是**告警**,判据是**模糊的**(「不矛盾」)。
⇒ 先做判据精确的那两条 —— 它们的夹具容易写、判别力容易证。
C1 的「不矛盾」若一开始就写,很容易写成「永远不报」。

### 量化函数必须**已经存在**(S2 定义)

AC-1 的判据用 `Dequantize(bakedInt)`。该函数归 **S2** 定义(逻辑侧存整数)。
若 S2 未定义它,本 story **须先补** —— 但那是**越界**,应回 S2 补。
⚠️ **本 story 不得自行发明第二个量化函数** —— 否则两侧各有一个,「同源」就成了空话。

### C2' 需要模块 prefab —— 而 prefab 归 23 素材轮

`ModuleDefinition` 今日只有五字段(`ModuleId` / `SlotType` / `LocalOccupancy` /
`ValidOrientations` / `Refundable`),**没有 collider 信息**。
⇒ C2' 需要 23 侧提供 collider 足迹。
**两条路**:

| 路 | 做法 | 代价 |
|---|---|---|
| **甲** | 等 23 素材轮产出 prefab,再读 collider | 本 story 被阻塞 |
| **乙** | 本 story 定义「collider 足迹」的**数据形状**(整数),prefab 侧后补 | 可先行;但数据与 prefab 可能漂移 |

⚠️ **本 story 取乙** —— 定义形状 + 检查逻辑,**读 prefab 的那一半登记为未跑**。
若 23 素材轮已产出 prefab,则甲路可跑。**须在完成时如实说明走的是哪条**。

### CI 接线的判据不能只写「能跑」

`coding-standards.md` 的 BLOCKING 规则:「『某件有门』的自述须给 `file:line` +
一条能证明它**真的会红**的命令」。⇒ AC-4 的验收**必须**包含:
① 门在哪(`file:line`);② 一条命令;③ 该命令在**注入缺陷**时的输出。

---

## Out of Scope

- **`slopeLimit` / `stepOffset` 的具体数值** —— **用户数值轮**(本 story 只检「两侧一致」)
- **模块 prefab 本体** —— 归 23 素材轮(本 story 只检「足迹同源」)
- **视觉层地形资产本体** —— 美术 / 关卡设计
- **C3 / C4 / C5 / C6** —— 归 S3 / S5
- **`CharacterController` 的参数应用** —— 归系统 1(债册 `TD-player-01` 已修)
- **量化函数的定义** —— 归 S2(本 story 只用,不定义)

---

## QA Test Cases

- **AC-1**:C2 三条边界,**每个量各一套**。
  - Given: 逻辑侧整数 `slopeLimit` + 引擎侧 float。
  - When: 构造偏差 0 / 1 量子 / 1 量子 + ε。
  - Then: 不抛 / 不抛 / 抛。
  - Negative fixture: 把阈值从 `≤ 1 量子` 改成 `≤ 2 量子` ⇒ 第二条应仍绿、第三条仍红;
    改成 `≤ 0` ⇒ 第一条红。**三条合起来才钉住阈值**。
  - ⚠️ **`slopeLimit` 与 `stepOffset` 各一套** —— 停用其中一条的检查,另一条不受影响。

- **AC-2**:C2' 三条。
  - Given: 一个模块的 collider 高度 + `stepOffset` + 「可走过」声明。
  - When: 三种组合。
  - Then: 抛 / 不抛 / 抛(足迹不一致)。
  - Negative fixture: 若实现「恒抛」⇒ 第二条(有声明)红。

- **AC-3**:C1 告警。
  - Given: 一处逻辑 / 视觉漂移。
  - When: 跑检查。
  - Then: 报出告警,退出码 0。
  - Negative fixture: ① 停用检测 ⇒ 不报 ⇒ 红;② 改成抛 ⇒ 退出码非零 ⇒ 红。

- **AC-4**:CI 门会红。
  - Given: 注入一个 C2 缺陷。
  - When: 跑 CI 命令。
  - Then: 非零退出码 + 报文点名是哪一条。
  - ⚠️ **本条必须贴出实跑输出** —— 「命令存在」不等于「命令会红」。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `unity/Assets/Tests/EditMode/LevelTool/consistency_c1_c2_c2prime_test.cs` — must exist and pass
- **CI 门证据**:一条可跑命令 + 注入缺陷时的实跑输出(落证据件)
- ⚠️ **既有 `locomotion_chain_test.cs` 的 `test_ac133_*` 须保持全绿** ——
  本 story 给 `AC-1-33②` 提供**参数来源**,不得改它的判据

---

## Dependencies

- Depends on: **S2**(`world_terrain` 存在 + 量化函数已定义)· **S3**(生态区存在,C1 的邻域)
- Unlocks: **本 epic 的 DoD**(`UnimplementedChecks` 归零)· `AC-1-33②` 转可判
  · `world-ecozones/story-005` 的 EXTERNAL 前置解除

---

## §诚实边界(本 story 证明不了的事)

1. **C2' 的 prefab 侧若未产出** —— 本 story 只定义形状 + 检查逻辑;
   「prefab 的 collider 真的与占用格一致」**未跑**(须如实登记)。
2. **C1 的「不矛盾」是主观判据** —— 告警级,由人判读;本 story 只保证「漂移会被报出」。
3. **CI 门在真实 CI 环境(而非本机)的行为** —— 本 story 只证明本机命令会红。
4. **`slopeLimit` / `stepOffset` 的数值合理性** —— 用户数值轮。
5. **量化函数的正确性** —— 归 S2;本 story 只用不验。
