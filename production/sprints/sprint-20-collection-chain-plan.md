# 计划 —— 采集链路解锁链（8 件 story · 2026-10-26 立）

> 🔁 **2026-10-27 改名 + 结案(用户「6 裁决」)**
>
> **原名**:`sprint-21-candidate-collection-chain-plan.md`
> **现名**:`sprint-20-collection-chain-plan.md`
>
> **改名的依据是事实,不是偏好** —— 链的 8 件**全部在 sprint-20 内落地**
> (`S20-8` / `S20-13` / `S20-14` / `S20-15` 即本链的四件;`S20-4` = `processing/001`;
> `S20-9`…`S20-12` / `S20-16` 为链上已完成的件)。原名写 `sprint-21-candidate` 有两处失真:
> ① 它**从来不是候选** —— 用户 2026-10-26 裁定「甲:按链路顺序做到底」即已采纳;
> ② **归属是 sprint-20**,不是 sprint-21。
> ⇒ 保留原名会让下轮 `grep sprint-21` 找不到链的载体,同时让 `grep sprint-20` 漏掉它。
>
> **本件的现状 = 已完成的执行计划,不是待办**:
> 8 件全部 `done`(复算见 §七),`AC-17-01` 已解除挂起。本件此后**只作历史与追溯用**,
> 不再需要更新进度。
>
> ⚠️ **§二 的 Est 合计 51h 与「约 8 件 / 52h」的口径差**保留原样不加改 ——
> 那是规划期的算术,与本件现在的用途(历史记录)无关;实测工时无从复算,不补造数字。

> **立此件缘由**:`foraging-006`(采集侧 novelty 接线)的真实前置 = 20 的三个 `Drop*` 写者,
> 而 20 侧写者熔在 `inventory-items/story-005` 的「统一写入事务模板」里,该件又有 5 个未开工前置。
> ⇒ 「修 novelty 静默失效」这件事实际是一条 **8 件、约 52h** 的依赖链。
> 用户 2026-10-26 裁定:走 **甲**(按链路顺序做到底),**先出计划再开工**。

---

## 一、链路与状态(2026-10-26 实测)

```
item-database/001 ✅ Complete          item-database/002 ✅ Complete
modular-building/002 ✅ Complete       modular-building/005 ✅ Complete
        │
        ├── processing/001 准入谓词         Ready  ← **= sprint-20 的 S20-4**
        ├── processing/002 F1 调用契约      Ready
        │        ↓
        ├── processing/004 单炉空闲判定     Ready  ← 与 003 互锁，**先做此件的空闲判定半**
        ├── processing/003 Craft 落流       Ready  ← 载荷形状用本卡契约先行
        │
        ├── inventory/001 InventoryOf 投影  Ready  ← 前置已 Complete，可随时开工
        ├── inventory/003 堆叠折重          Ready  ← ← 001
        ├── inventory/004 CanCarry          Ready  ← ← 001 + 003
        ├── inventory/005 统一事务模板       Ready  ← ← 001+003+004 + processing-003/004 + building-002/005
        │        ↑ **Drop* 三支写者在此落地**
        └── foraging/006 novelty + AC-17-01 Ready  ← ← inventory-005
```

**关键事实**:`inventory-001/003/004` 的前置(`item-database-001/002`)**已 Complete**,
⇒ 这三件**不等 processing**,可与 processing 批次并行。

---

## 二、批次与工作量

| 批 | # | Story | Type | AC | Est | 前置 | 可否并行 |
|---|---|---|---|---|---|---|---|
| **1** | 1 | `processing/001` 配方子集 + 可采纳谓词 | Logic | 4 | 5h | 无(已齐) | ✅ 与 2、4 并行 |
| | 2 | `processing/002` F1 调用契约 + 模组输入 | Logic | 7 | 6h | 无(已齐) | ✅ |
| **2** | 3 | `processing/004` 单炉空闲判定(**只做空闲半**) | Logic | 5 | 6h | 1、2 | ✅ 与 4/5/6 并行 |
| **3** | 4 | `inventory/001` InventoryOf 投影重建 | Logic | 4 | 6h | 已 Complete | ✅ |
| | 5 | `inventory/003` 堆叠 / 容器 / 折重 | Logic | 3 | 5h | #4 | ← #4 |
| | 6 | `inventory/004` CanCarry / 载重裁决 | Logic | 6 | 5h | #4、#5 | ← #5 |
| **4** | 7 | `processing/003` Craft 落流 + 点火事务 | Integration | 4 | 6h | #1、#2、#3 | ← #3 |
| **5** | 8 | `inventory/005` 统一事务模板 + **Drop\* 写者** | Integration | 6 | 6h | #4、#5、#6、#7 | ← 全部 |
| **6** | 9 | `foraging/006` novelty 接线 + AC-17-01 解除挂起 | Integration | 8 | 6h | #8 | ← #8 |
| | | **合计** | | **53** | **51h** | | |

> ⚠️ **表内 9 行,但其中 `processing/003` 是链路上的过路件** —— 用户裁定的范围是
> 「先实现 20 写者,再接 17 动作链」,**`processing/003` 非做不可**(`inventory-005` 的
> 事务模板要消费 `Craft` 的扣料语义),故列入。若将来认为可绕过,再裁。

**约 51h ≈ 6.4 人日**(按 8h/日)。

---

## 三、互锁破解(processing-003 ↔ 004)

**互锁原文**(003 `§Dependencies`):
> `Story 004`(单炉空闲判定 —— 点火事务第一步;**两者互为时序前后但测试夹具可先行桩**)

**用户裁定(2026-10-26)**:**先做 `processing-004` 的空闲判定半**。

**具体破法**:
1. 从 `processing-003` 卡里取**已写死的 `Craft` 落流载荷契约**(不新裁,只抄既有)
2. `processing-004` 按该契约做「单炉空闲判定」—— 它只需要**读** `Craft` 的形状,不需要**写**它
3. `processing-003` 开工时,004 的那半已在盘上 ⇒ 无阻塞

⚠️ **不得**在 004 里改动 `Craft` 载荷形状 —— 那是 003 的裁决面。若 004 发现契约不够用,
**停下来问**,不得自行扩字段。

---

## 四、每件的收口标准(不得裁剪)

每件均须走完整流程(**不得跳过任何一步**):

| 步骤 | 判据 |
|---|---|
| 1 实现 | 代码 + 夹具,门 A 合规 |
| 2 夹具级跑绿 | 三元组 total/passed/failed/skipped + XML 路径 |
| 3 **证伪轮(S12-4)** | 每条命中形态各一条变异,**必须转红**;还原判据 = SHA-256 逐字节 |
| 4 `/code-review` | 报告原件落 `production/qa/evidence/review-<对象>-<日期>.md` |
| 5 修复轮 | BLOCKING 全修 + 各自变异反证 |
| 6 全量回归 | 相对基线增量 == 新增夹具数,零回归 |
| 7 `/story-done` 收口 | Status → Complete + Test Evidence 三元组 + Completion Notes |

⚠️ **禁借绿三条**(承 `coding-standards.md`):
① 构建绿 ≠ 门跑了(确认门在效须探针轮)
② 「有门」自述须给 `file:line` + 可跑命令,缺一按 `NOT-RUN`
③ 数字须带可跑复算命令,禁照抄规划期算术

---

## 五、已知风险

| 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|
| **AC 挂 EXTERNAL / BLOCKED-BY 扩散** | 高 | 多件可能有 AC 依赖 42/45/数值轮 | 每件开工前先跑可达性表;不可达的**如实不勾**,禁凑绿 |
| `processing-003/004` 互锁比预期深 | 中 | 004 的空闲判定可能需要 003 的写侧 | 撞上就停,回来裁 |
| `inventory-005` 事务模板范围再扩 | 中 | 已含五种操作(拾取/放下/消耗/建造扣/返还) | 若发现第六种,停 |
| ~~**本链不在 sprint-20 计划内**(除 S20-4)~~ | ~~高~~ | ~~sprint-status 无对应行~~ | ✅ **2026-10-26 已消解** —— 8 件已入 `production/sprint-status.yaml` 作 **S20-8…S20-15**(S20-4 即 processing-001,不重复计),另 S20-16 补记已完成的 foraging-003。⚠️ 遗留:本文件名仍写 `sprint-21`,与实际归属(sprint-20)不符,待裁是否改名 |
| 长链中前件 AC 记录错误会向后传导 | 中 | `inventory-001` 的 fold 口径错 ⇒ 003/004/005 全错 | 每件收口时把「本件对下游的契约承诺」写进 Completion Notes |

---

## 六、执行顺序(实际开工序)

```
批1: processing-001 (S20-4, 已在 sprint)  ┐
     processing-002                      ├─ 可并行
批2: processing-004(空闲判定半)          ┘
     └→ processing-003(Craft 落流)
批3: inventory-001 → 003 → 004           (可与批1/2 并行,前置已齐)
批4: inventory-005  ← Drop* 写者在此落地
批5: foraging-006   ← novelty 全绿 + AC-17-01 解除
```

---

## 七、完成判据

| # | 判据 |
|---|---|
| 1 | 8 件全部 `Status: Complete`(含 processing-003 共 9 件) |
| 2 | `foraging-006` 的 AC-17-14 系列(7 条)**全绿** —— novelty 静默失效修掉 |
| 3 | `foraging-006` 的 AC-17-01 **勾绿** —— 三条事件真的落流 |
| 4 | `foraging-003` 的反向夹具 `test_ac1701_resourceHarvestedIsWritten_butDropEventsAreNot` **按预期转红并更新** |
| 5 | `WriterExistenceGate.cs:152-154` 的三条豁免登记**移除**(写者已存在) |

⚠️ **判据 5 由机械门自动强制,不靠人记** —— `WriterExistenceGate.cs:242-248` 有
**判据①「已写者不得豁免(豁免 = 红)」**:

```csharp
foreach (var (kind, reason) in ExemptKinds)
    if (written.Contains(kind))
        errs.Add($"[b7] Kind「{kind}」已在扫描面内有生产写者,却仍挂在豁免表 —— ...");
```

⇒ 写者落地后若忘撤表,门**当场红**。这比「收口时人查一遍」可靠 —— 项目抓过的
「用一句自述顶替一个从未存在的门」那种假绿,在这条判据上不成立。

⚠️ **但有一个时序坑**(承 `CareApplied` 2026-10-22 的既有注记):
**写者与撤表必须同批提交** —— 先撤表后写码 ⇒ 判据「写者缺失」红;先写码后撤表 ⇒ 判据① 红。
中间态一定是红的,这是设计而非缺陷。

---

## 八、立此件的元信息

- **立件人**:编排会话(用户裁定 甲 + 先出计划)
- **用户裁定记录**:2026-10-26 —— ① 走甲(按链路做到底)· ② 先出计划再开工 · ③ 先做 processing-004 空闲判定半
- **本件不改动任何 story 卡内容** —— 纯计划件
- **2026-10-27 结案裁定**(用户「6 裁决」):① **改名**为 `sprint-20-collection-chain-plan.md`
  (依据:8 件全在 sprint-20 落地,原名 `sprint-21-candidate` 的「候选」与「归属」双失真);
  ② **不归档删除** —— 保留为历史执行记录(其中 §三 互锁破解 / §四 收口标准 /
  §七 撤表时序坑均有长期参考价值)。

## 九、结案状态(2026-10-27 实测)

链上 8 件**全部 `done`**:

| 件 | sprint-20 id | 状态 |
|---|---|---|
| `processing/001` 配方子集 + Admissible | `S20-4` | done |
| `processing/002` F1 调用契约 | `S20-8` | done |
| `processing/004` 单炉空闲判定 | `S20-9` | done |
| `inventory/001` InventoryOf 投影 | `S20-10` | done |
| `inventory/003` 堆叠 / 容器 / 折重 | `S20-11` | done |
| `inventory/004` CanCarry / 载重 | `S20-12` | done |
| `processing/003` Craft 落流 | `S20-13` | done |
| `inventory/005` 统一事务模板 + Drop\* 写者 | `S20-14` | done |
| `foraging/006` novelty 接线 + AC-17-01 | `S20-15` | done |

（表内 9 行 = §二 的 9 个 story;「8 件」的原口径不含 `processing/001`,因它本就在 sprint-20 计划内,
不重复计。）

**复算**:
```bash
python3 -c "import yaml,collections;d=yaml.safe_load(open('production/sprint-status.yaml'));print(len(d['stories']),collections.Counter(s['status'] for s in d['stories']))"
# 实跑:17 Counter({'done': 17})
```

⚠️ **本链的完成不解除 `AC-18-15`**(processing 的跨平台逐位重放)—— 那条挂的是 ADR-012
矩阵面,归 `TD-ci-01`(EXTERNAL)。**链做完 ≠ AC-18-15 能勾。**
