# Sprint 26 — 日期不设(用户 2026-11-07 裁定「忽视起止日期」)

> **前置**:`sprint-25` 已收口(2026-11-07,Closed with carryover;其「收口后追加件」六节与
> S25-5 结转件补做均已落盘)。归档 yaml = `production/sprint-status-sprint25-archive.yaml`
> (SHA-256 前 16 位 `8af99691c31f55bf`,覆盖前快照,8 个 story 条目)。
> **QA 计划**:开工后立即跑 `/qa-plan sprint`(承 sprint-25 的用户裁定)。
> **容量口径**:本文件**不设结束日期** ⇒ 进度一律按 `production/CAPACITY-MODEL.md` §三 的
> 「记录三元 + 约束三条」评估,**不用燃尽率、不用「件/日」**(该单位已整条废止)。

---

## ⚠️ Phase 1.5 复核(2026-11-07 · 开工前必做)

路线图的 sprint-26 行写于 **2026-10-20**,取件前逐条复核后**三处须订正**:

| # | 路线图原写 | 实测 | 处置 |
|---|---|---|---|
| 1 | 件集 = `casebook-005` · `casebook-006` · `inventory-006` · `death-respawn-006` | **2 / 4 已 `Complete`**:`casebook-006`(相机模式意图,`Complete ✅ 2026-11-02`)· `death-respawn-006`(呈现白名单,`Complete`);仍 `Ready` 的只有 `casebook-005` 与 `inventory-006` | 件集按**现况重取**,不按原行 |
| 2 | `casebook-005` 排 sprint-26 | 其 `## Dependencies` 逐字写「Depends on: Story 002 · **003** · **004**」,而 003 / 004 被路线图排在 **sprint-27** ⇒ **依赖倒置**:sprint-26 起手第一件事就卡在自己的前置上 | **把 001 → 003 → 004 提到本 sprint**,005 才有开工前提 |
| 3 | `A2-1`(019-b atlas 预算门)指定落 sprint-26 | 容量被脉案链占满 | **结转 sprint-27**,在 §Carryover 内具名,不静默吞掉 |

> 🔁 逐件实测(`grep -m1 "Status\*\*:" production/epics/casebook/story-*.md`):
> `001` / `003` / `004` / `005` / `007` = `Ready`;`002` = `Complete`;`006` = `Complete ✅ 2026-11-02`。
> `inventory-items/story-006` = `Ready`;`death-respawn/story-006` = `Complete`。

### 为什么是「整条链」而不是「挑几件」

`casebook-001 → 003 → 004 → 005` 是一条**单向前置链**:

- `001` 解锁 `003`(分册正文投影需要段 schema 就位)
- `003` 解锁 `005`(「旧」态 / 空白页渲染读同一投影)
- `004` 解锁 `005`(呈现层在「做不到」约束内设计,不再返工)
- `005` 解锁 `007`(落笔提交与相机联动的宿主页面)

⇒ 只做 005 而不做 003/004 = **拿没有数据源的页面糊一层 UI**;只做 001 而不做 003 = 段写进去了但没人读。
**链内任一件落单,整条的判据面都不可判。**

---

## Sprint Goal

**把 39 脉案的数据链从「契约未定」推到「页可摊开」** —— 分册态有段、痕有投影、能力面有守卫,
然后 005 的页布局才具备开工前提。

## Capacity(按 `production/CAPACITY-MODEL.md` §三)

| 元 | 值 |
|---|---|
| 计划件 | **5**(Must 3 · Should 2 · Nice 0) |
| 复杂度分布 | **M 4** · **L 1**(`casebook-005` = UI + HIGH engine risk + 焦点桥) |
| 并发上限 P | **3** —— 实际并行度更低:001→003 串行,004 可与 003 并行,005 等 003+004;`inventory-006` 独立可并行 |
| 追加额度(§3.3) | 散件 **1** + 链条 **1** |
| 判别力门槛 D | 每件须有「改坏生产 ⇒ 夹具转红」的可证伪判据,否则**不计 done** |

---

## Tasks

### Must Have

| ID | 任务 | Type | 复杂度 | 掀债 | 掀 story | 链 | 验收判据 |
|---|---|---|---|---|---|---|---|
| **S26-1** | `casebook-001` 分册态数据契约与存档段 | Logic | M | 0 | 1(003) | 脉案链 | 分册态段按 ADR-010 §一 布局落槽(义务 13);**禁 `float`**、`Fix` 走自定义编码器 + EditMode 探针;`player_id` 经 `IIdAuthority`,45 铸造契约残**记 `NOT-RUN` 不记绿** |
| **S26-2** | `casebook-003` 分册键与改写痕读时投影 | Logic | M | 0 | 1(005) | 脉案链 | 痕 = **读时投影、零独立存储**(AC-39-13:痕 ∉ 可序列化类型、∉ `CaseClosed` 载荷);`J(c,p)` = 该作者在病例 c 上的最后一笔判断;分册键跨 join/leave 稳定不重号 |
| **S26-3** | `casebook-004` 反幻想三条「绝不」的能力面守卫 | Logic | M | 0 | 1(005) | 脉案链 | 每条「绝不」**各有负夹具**(故意破戒 ⇒ 必红);守卫是**能力面**不是提示面 —— 「做不到」不得用「没显示」冒充 |

### Should Have

| ID | 任务 | Type | 复杂度 | 掀债 | 掀 story | 链 | 验收判据 |
|---|---|---|---|---|---|---|---|
| **S26-4** | `casebook-005` 脉案页布局与无指针焦点导航 | UI | **L** | 0 | 1(007) | 脉案链 | 焦点顺序铁律(面色→语声→姿态→呼吸→触感→病名→置信度)**永不重排**;空行**可聚焦**、反馈只有压痕声 / 行高亮、**零提示文字**;P0 无 disabled 按钮;`AC-39-11` / `AC-39-12` 的 `[L]` 走查未跑即 **`NOT-RUN`** |
| **S26-5** | `inventory-006` 药箱容器摆放、翻页遍历与满溢四档呈现 | UI | M | 0 | 0 | 库存链 | 满溢四档呈现;`AC-42-F1` 的库存屏项;`R9` 回写已解(2026-10-27),`BLOCKED-BY` 注保留不删 |

### Nice to Have

无 —— 承 §3.2「L 件单独占批」,不凑件数。

---

## Carryover from Previous Sprint

| 任务 | 原因 | 备注 |
|---|---|---|
| **`A2-1`** `019-b` atlas 预算门接线 | 路线图**指定**落 sprint-26,但容量被脉案链占满 | **被动让位**,结转 sprint-27。⚠️ 这不是「评估后不做」,是「排位冲突」 |
| **sprint-25 的四条 `[L]` 走查**(`AC-48-05` / `AC-48-06` / `AC-48-07` / `AC-48-15`) | 前件未变:纸屏零生产开口 + 资产未出 | 仍 `NOT-RUN`;解锁件 = `TD-skeuo-04`(纸近景开屏点) |
| **15 条未闭 P0 面债** | 本轮一件不碰 | `TD-skeuo-04`(解锁 48 走查)排 sprint-27 的首选 |

---

## Risks

| 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|
| **链式串行的头节点失败**(001 卡住 ⇒ 003/004/005 全卡) | 中 | **高** | 001 是纯 Foundation Logic、零外部依赖;卡住即**停手登记**,不改设计把链拆短 |
| **`casebook-005` HIGH engine risk**(UI Toolkit 焦点桥 / world-space) | 中 | 高 | 抄 `TD-skeuo-20` 的修法(按 id 还原焦点、不悬空);不自实现焦点算法(ADR-013) |
| 三件 Logic 可能**掀新债**(源文本已存在、行为未接的形态) | 中 | 中 | 收口时逐条进 `tech-debt-register`,**不在实现轮现场消化**;若掀出 P0 面债则本 sprint 的「未闭 P0 面」不降 |
| 回归数与旧基线混批 | 低 | 中 | 收口必须带三元组,且用等式判据 `total − 前次 = 新增测试数` |
| CI 仍零运行(`TD-ci-03`,**P2**) | 确定 | 低 | 本 sprint **不得在任何地方声称「CI 绿」** |

## Dependencies on External Factors

- **`AC-39-11` / `AC-39-12` 的 `[L]` 走查** = 需实机 + 手柄;无手柄则如实 `NOT-RUN`,不代签
- **45 联机面**(分册跨机私有 / `judge` 铸造契约残)= `BLOCKED-BY-45`(**P1b**),单机夹具**不替代**该 closure
- **`casebook-007`**(落笔写流 + 7a checkpoint 锚点)依赖本 sprint 的 001/003/005 + 7a 锚点接口(当前不存在)⇒ 不在本 sprint

---

## Definition of Done for this Sprint

- [ ] Must 3 件交付(`NOT-RUN` / `BLOCKED-BY` 半随如实标,**不借绿**)
- [ ] QA 计划存在(`production/qa/qa-plan-sprint-26-*.md`)
- [ ] 每件至少一条「改坏生产 ⇒ 夹具转红」的证伪轮,还原 **SHA-256 逐字节**(禁 `git checkout`)
- [ ] 回归三元组(截至时刻 · 批次 · commit)写入收口节,且带等式判据
- [ ] `python3 tools/debtcount/debtcount.py --check` exit 0
- [ ] `python3 tools/acid/acid_check.py --check` exit 0
- [ ] 收口时**不得**把「未闭 P0 面 15」说成已降,除非有可跑的 `--print` 输出

## 复算(自包含,只读)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# 件面:五件卡面的 Status
grep -m1 "Status\*\*:" production/epics/casebook/story-00{1,3,4,5}*.md
grep -m1 "Status\*\*:" production/epics/inventory-items/story-006*.md

# 债面(开工基线)
python3 tools/debtcount/debtcount.py --print
# 2026-11-07 规划时实测:条目 200 | closed 168 · partial 9 · open 22 · done 1 | 未闭 P0 面 = 15

# 路线图原行(复核靶)
grep -n "sprint-26" production/milestones/p0-remaining-roadmap-2026-10-20.md | head -3
```

---
