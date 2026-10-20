# P0 剩余面静态 Backlog — 依赖序 · 阻塞面 · 不可闭合清单

**建立日期**:2026-10-20(承 sprint-15 收口)
**性质**:**静态层**。它回答「还剩什么 / 谁挡着谁 / 哪些永远绿不了」,
**不回答**「下个 sprint 做哪几件」「几天能做完」。滚动层的件选择归每个 sprint 收口时的 `/sprint-plan`。
**规划依据**:`production/CAPACITY-MODEL.md`(复杂度 S/M/L + 并发 ≤ 3 + 判别力门槛;**明禁按天外推**)

---

## 0. 本件的判读边界(先读)

1. **本件不是排期。** 表内的 `Estimate` 列是 story 文件头部**原样抄录的字段**,仅供量级参照。
   `production/CAPACITY-MODEL.md` 明文:`它不预测「几天做完」,只约束「一批别塞太多」`。
   本件**不合计人日、不给 sprint 数、不给 ETA**。
2. **本件的分母只到 P0。** P1a / P1b 面(VR、45 联机层、无障碍走查、目标硬件适配)不在本件内。
3. **「阻塞」分三类,不可混读**:
   - **EXTERNAL** —— 依赖项目外部件(数值轮由用户裁定 / 外部美术 / CI 凭证),**agent 侧无法解除**;
   - **BLOCKED-BY-\*** —— 依赖某个**代码或裁决**落地,agent 侧可推进但判据不得记绿;
   - **STALE** —— story 里写的阻塞**已被后来的裁决解除**,只是卡面没刷(本件 §五 逐条列)。
4. **凡本件给出「实测」的格子,均附可复算命令**;凡引自其它文件的,标 `引用自`。
   **没有第三种**:不存在「我记得是」的数。

**溯源三元组**:截至时刻 `2026-10-20`(session 时点)· 批次 `sprint-15 收口后 · 静态勘察`
· commit `4937275`(工作树 clean)

---

## 1. 分母台账(实测订正)

### 1.1 story 级

| 量 | 实测值 | 复算命令 |
|---|---|---|
| P0 story 总数 | **226** | `ls production/epics/*/story-*.md \| wc -l` |
| `Complete` | **182**(80.5%) | 见下方脚本 |
| `Ready` | **42** | 同上 |
| 非 Complete / 非 Ready | **2** | 同上 |

> 复算脚本(读每份 story 的 `> **Status**:` 行;⚠️ 该行**带引用块前缀 `>`**,用
> `^\s*>?\s*\*{0,2}Status\*{0,2}\s*[:：]` 才能命中 —— 漏掉 `>` 会把 226 条全判成「Other」):
> ```bash
> python3 - <<'EOF'
> import re,glob
> for f in sorted(glob.glob('production/epics/*/story-*.md')):
>     t=open(f,encoding='utf-8',errors='replace').read()
>     m=re.search(r'^\s*>?\s*\*{0,2}Status\*{0,2}\s*[:：]\s*(.+)$',t,re.M)
>     print(f, '|', re.sub(r'[\*\[\]]','',m.group(1)).strip() if m else '(none)')
> EOF
> ```

**两件「非 Complete / 非 Ready」逐条**:

| 件 | Status 原文 | 性质 |
|---|---|---|
| `skeuomorphic-ui/story-019` | `部分完成 —— 019-c/d/e/f Complete · 019-b 冻结轮落定 ✅(门待接线)` | **部分完成**,不是未开工。`019-b` 的判据已实测倒推冻结,剩的是**接线**。 |
| `world-ecozones/story-005` | `Pending — [L] 桌面走查 + 工具 CI` | **待人工/CI**,非代码面 |

### 1.2 epic 级 —— ⚠️ 里程碑文件与本件不一致,以本件为准

| 来源 | 全 Complete epic 数 | 状态 |
|---|---|---|
| `production/milestones/README.md:49` | 22 / 31 | ❌ **陈旧**(三元组 `2026-10-15 · sprint-12 收口 · a5ef7c5`) |
| **本件实测(按 story 派生)** | **18 / 31** | ✅ 以本件为准 |

> ⚠️ **此处同时订正我自己的口头说法**:sprint-15 收口时的对话里我说过「19/31」,
> **那是错的**。实测逐 epic 归并(epic 全 Complete ⟺ 其全部 story 均 `Complete`)得 **18**。
> 差额 = `emergency-procedures`(8/9)· `medical-consequences`(3/4)· `patient-ai`(4/5)·
> `skeuomorphic-ui`(24/25)· `world-ecozones`(5/6)—— 这五户**不是**全 Complete。
> 22 与 18 的差另有 4/31 来自里程碑文件的旧快照。

**⚠️ 但 EPIC.md 的「X/X story 已实现」不可直接采信** —— 实测其**分母自身就偏小**:

| epic | EPIC.md 声称 | 实际 story 文件数 | 差 |
|---|---|---|---|
| `emergency-procedures` | `Complete ✅(7/7 story 已实现)` | **9** | 分母少 2 |
| `patient-ai` | `✅ 4/4 story 全闭` | **5** | 分母少 1 |
| `skeuomorphic-ui` | `Complete ✅(18/18 stories)` | **25** | 分母少 7 |
| `world-ecozones` | `Complete ✅(6/6 story 已实现)` | **6**(实 `5 Complete + 1 Pending`) | 分子多 1 |

⇒ **判 epic 完成度只认 story `Status:` 的逐条归并,不认 EPIC.md 的分数**。
上列四户 + `medical-consequences` 的 EPIC.md 分数刷账,登记为本件发现
(未改,改 EPIC.md 不属本件职权)。

### 1.3 分母的构成

**42 件 Ready + 2 件部分/待办**,分布如下(Estimate 为 story 头部原值,量级参照):

| epic | Ready 件数 | 链性质 | Estimate 合计(原值) |
|---|---|---|---|
| `casebook` | 6 | 内部**单链** 001→002→003→004→005/006 | 25 h |
| `clinic-machine` | 5 | 内部**单链** 001→002→003→004→005 | 27 h |
| `death-respawn` | 6 | 内部**单链** 001→{002,003,004}→005→006 | 30 h |
| `foraging` | 5 | 内部**单链** 001→002→{003,004}→005 | 28 h |
| `inventory-items` | 6 | 内部**单链** 001→{002,003,004}→005→006 | 33 h |
| `processing` | 5 | 内部**单链** 001→002→004→003→005 | 29 h |
| `tutorial-onboarding` | 5 | 内部**单链** 001→002→003→{004,005} | 21 h |
| `medical-consequences` | 1 | 尾件 | 5 h |
| `patient-ai` | 1 | 尾件(外部美术) | 24 h |
| `emergency-procedures` | 1 | 尾件(外部美术) | 待估 |
| `save-slot-ui` | 1 | **无 EPIC.md** 的孤儿 epic | 2 h |
| **合计** | **42** | | **224 h**(41 件有值) |

> ⚠️ **224 h 不是排期**:它是把 41 个 story 头部的 `Estimate` 字段加起来的算术和。
> 依 CAPACITY-MODEL,该值**不折算人日、不折算 sprint 数**。列出它只为给出量级,
> 且提醒一件事:**`patient-ai/story-005` 一件就占 24 h(3.0 人日),是单件里最重的**。

---

## 2. 八条依赖链(逐件:依赖 · 阻塞 · 可闭否)

图例:**可闭** = 该件的全部 AC 有无外部阻断;`⚠️` = 有半件/部分 AC 永远绿不了。

### 2.1 `casebook`(6 件)— 链最短、外部面最小

| 件 | 依赖 | 阻塞 | 可闭 |
|---|---|---|---|
| 001 分册态契约与存档段 | ADR-010 codec(既有)· `IIdAuthority` 机制 A(既有) | `player_id` 跨机稳定性 → **EXTERNAL·BLOCKED-BY-45**(P0 单机不阻塞) | ✅ 可闭 |
| 002 病例列表排序纯函数 | 001 | 无 | ✅ 可闭 |
| 003 分册键与改史投影 | 001 · 002 | AC-39-04③ id 跨机 **BLOCKED-BY-45**(单机夹具模拟迁移可先行) | ✅ 可闭 |
| 004 反幻想能力守卫 | 002 · 003 | 无 | ✅ 可闭 |
| 005 页布局与焦点导航 | 002 · 003 · 004 · 42 元件库(**已 Complete,直连**) | 无 | ✅ 可闭 |
| 006 相机模式意图与落笔锚点 | 001 · 003 · 005 · 2 的 `ICameraRig`(既有)· 7a 锚点接口 | 联机落笔形态 **BLOCKED-BY-45**(接口形状可落,运行面不绿) | ⚠️ 半闭 |

> **判读**:001→005 五件**无硬外部门**,是全 P0 剩余面里最干净的一条链。

### 2.2 `clinic-machine`(5 件)— 第二干净

| 件 | 依赖 | 阻塞 | 可闭 |
|---|---|---|---|
| 001 烘焙表 schema 门 | ADR-014 两阶段工具链(**已可运行**) | 无 | ✅ 可闭 |
| 002 房间切分与连通性 | 001 · 系统 23 `structure_at`(未就绪可 fixture 桩) | 无硬门 | ✅ 可闭 |
| 003 修饰符求值 | 001 · 002 | 无 | ✅ 可闭 |
| 004 交付边界 `ClinicEnvDto` | 003 · 21a F1 clamp · 系统 1 K 消费(桩先行) | 联调前 **BLOCKED-BY**,不记绿 | ⚠️ 半闭 |
| 005 纯性重放与记忆化 | 002 · 003 · 004 | AC 跨平台对拍 **EXTERNAL·BLOCKED-BY-ADR-012** | ⚠️ 半闭 |

### 2.3 `death-respawn`(6 件)— 有一处**硬设计前置缺失**

| 件 | 依赖 | 阻塞 | 可闭 |
|---|---|---|---|
| 001 `PlayerDied` 结算 | epic 入口 · 9 的 `DeathCandidate`(⚠️ **未落地,桩先行**)· kindgen(既有) | AC-29-01 全链 **BLOCKED-BY-9** | ⚠️ 半闭 |
| 002 掉落清单与流回收 | 001 · 20 库存读 + `IIdAuthority`(既有)· 4 拾取意图(桩) | AC-29-02/17 **BLOCKED-BY-9** | ⚠️ 半闭 |
| 003 技能等级下降(int 截断) | 001 · 30 story-005(**已 Complete**)· 7a 折叠面 | AC-29-05 **BLOCKED-BY-9**;<br>AC-29-16 见 §五 **STALE** | ⚠️ 半闭 |
| 004 死亡冷却(流派生) | 001 | AC-29-08/10 **BLOCKED-BY-9** | ⚠️ 半闭 |
| 005 复活床格传送命令 | 001 · 004 · 1 的命令消费(桩) | **24 床格「选定规则」未落地** → **BLOCKED-BY-24** | ⚠️ 半闭 |
| 006 呈现白名单 | 005 | 🔴 **`/ux-design` 全屏过渡 spec = 硬前置,实测该 spec 不存在**(`design/ux/` 十份无一份是死亡过渡) | 🔴 **硬阻** |

> ⚠️ `design/ux/` 现有十份:`casebook-39` `clinic-panel-24` `hud` `index` `interaction-patterns`
> `inventory-container-20` `paper-closeup-48` `save-slots-7b` `settings-shell-42` `tutorial-48`。
> **无死亡/复活过渡 spec** ⇒ `story-006` 需**先做设计件**(`/ux-design`),不能直接实现。

### 2.4 `foraging`(5 件)— 外部面最宽(跨平台 AC)

| 件 | 依赖 | 阻塞 | 可闭 |
|---|---|---|---|
| 001 品种闭集与装载校验 | item-db 001/002(**Complete**)· 关卡工具(编辑期,非运行时阻塞) | 🔴 **`21a` 的 `item_key` 行未落地** —— 实测 `item_database_items.json` 首条键为 `base_id`,**无 `item_key` 字段** ⇒ 该子用例 `Ignore` 是**真阻塞**;`OQ-17-7` 未裁(按驻留最小实现,不构成阻塞) | ⚠️ 一件 AC 挂起 |
| 002 品质掷骰管线 | 001 · 30 的 `QueryLevel`(**Complete**)· `SplitMix64`/`Fix`(**Complete**) | AC-17-02 跨平台 **EXTERNAL·BLOCKED-BY-ADR-012**(Mono 半边可证) | ⚠️ 半闭 |
| 003 三事件与身份铸造 | 002 · 30 `EmitGrowth`/`SkillGrown`(**Complete**)· `entities.yaml` 三支(零 kindgen 变更) | 无**外部**门;但见 §三 **环 A** | ✅ 可闭 |
| 004 节点余量前缀函数 | 001 · 002 | AC-17-13 **BLOCKED-BY-OQ-17-2**(`GATHER_MUL_CAP` 值未裁 ⇒ 数值轮) | ⚠️ 形状可闭 / 数值不可 |
| 005 动作原子性与失败收敛 | 003 · 004 · `inventory-items/003` · 3/4(既有 Complete 件) | AC-17-07b/13b **EXTERNAL(42 呈现未交付)** + OQ-17-2 | ⚠️ 半闭 |

### 2.5 `inventory-items`(6 件)— **全 P0 剩余面阻塞最重的一条链**

| 件 | 依赖 | 阻塞 | 可闭 |
|---|---|---|---|
| 001 `InventoryOf` 折叠重建 | item-db 001/002(**Complete**)· `foraging/003` · `processing/003` | 🔴 **AC-20-03 BLOCKED-BY-R2/R3/R10/R11/OQ-20-5**(五件);AC-20-16 **BLOCKED-BY-OQ-20-5/R2** | ⚠️ 主体挂起 |
| 002 id 权威与高水位 | 001 · item-db story-008 类先例(**Complete**)· ADR-025 | **R3 未落地**(高水位基线缺 `BakedInitial`)⇒ 预摆物撞号用例挂 BLOCKED-BY-R3 | ⚠️ 半闭 |
| 003 堆叠与容器折重 | 001 · 004 · item-db 002/004(**Complete**) | `COLUMNS`/`ROWS_PER_PAGE` 归 **42 未定** ⇒ **BLOCKED-BY-R9** | ⚠️ 半闭 |
| 004 载重与容量裁决 | 001 · 003 · item-db 002(**Complete**) | 🔴 **`CARRY_CAP` 值未裁**(`entities.yaml:1780` `value: "待定"`;`OQ-20-2`)⇒ **AC-20-09 的硬不变量无值可验**,且**装载期须硬失败** | 🔴 **数值轮** |
| 005 原子性/消耗/恢复/无确认 | 001 · 003 · 004 · `processing/003,004` · `modular-building 002/005`(**Complete**) | `DropDespawned.qty`(R2)未定稿 ⇒ 子用例 `Ignore` | ⚠️ 半闭 |
| 006 药箱 UI 走查 | 003 · 004 · 005 · 42 元件库(**Complete**) | **AC-20-20 BLOCKED-BY-R9**(页可见集 + 二维几何焦点稳定性);<br>ADR-013 假设 6 / S5 spike(**桌面侧**) | 🔴 多层挂起 |

> **本链的判读**:六件里**没有一件可判全绿**,唯一「数值轮」外的阻塞源是 **R2/R3/R9** 三条
> 前置债 + `CARRY_CAP` 一个数值。⇒ 本链**不适合作为下一个 sprint 的主线**,
> 除非先单独清偿 R2/R3 之一。

### 2.6 `processing`(5 件)— 与 inventory 形成**真实依赖环**(见 §三)

| 件 | 依赖 | 阻塞 | 可闭 |
|---|---|---|---|
| 001 配方子集与可发起谓词 | item-db 002/003(**Complete**)· 30 story-001(**Complete**)· `inventory-items/001` | ✅ **AC-18-18 的判据面已清(2026-10-20 二轮)**:`RecipeOwner` 枚举 + `Recipe.Owner` 属性 + 烘焙读取面**均已存在**;数据表经补口后 `owner` 分布 = `{process:2, craft:1, build:1}` ⇒ 三子集**均非空**,三段判据(非空 / 并 / 交)**两段皆可判且已证伪判别力**。⚠️ **本 story 主体仍未开工**(`unity/Assets/Sim/Processing/` 目录不存在)—— 已落地的只有 AC-18-18 一项的判据与夹具 | ✅ 判据面可闭;主体待开工 |
| 002 F1 调用契约与修饰输入 | 001 · item-db 003(**Complete**)· item-db 011(**Complete**)· 24 侧只读接口(桩) | AC-18-10b **EXTERNAL·BLOCKED-BY-42**(面板未交付) | ⚠️ 半闭 |
| 003 `Craft` 载荷/点火/落流 | 001 · 002 · 004 · `inventory-items/002` | AC-18-19 **EXTERNAL·BLOCKED-BY-21a(O-18-R3)**(守恒式两处不同形,真修归 21a) | ⚠️ 半闭 |
| 004 炉火时序与无过程状态 | 003 · 001 · `inventory-items/001,002` | AC-18-15 跨平台 **EXTERNAL·BLOCKED-BY-ADR-012** | ⚠️ 半闭 |
| 005 完成调度/溢出/音频 | 004 · 003 · `inventory-items/001,004` · 44(**Complete,直连**) | 走查三条 **EXTERNAL(42)**;`OQ-18-9`(器具状态渲染形态)未裁 ⇒ 只断 18 侧两输出 | ⚠️ 半闭 |

### 2.7 `tutorial-onboarding`(5 件)— 尾部一件硬阻

| 件 | 依赖 | 阻塞 | 可闭 |
|---|---|---|---|
| 001 内容 schema 与烘焙校验 | ADR-014 管线 · `entities.yaml` · 9/44/13 词表 `.cooked`(缺表报「未就绪」不崩) | 无 | ✅ 可闭 |
| 002 只读投影与装配守卫 | 001 · 三流只读快照(既有) | 联机各玩家投影独立 → **BLOCKED-BY-45**(双 id 夹具可先行) | ⚠️ 半闭 |
| 003 锚点完成触发引擎 | 002 · 001 | 同上 **BLOCKED-BY-45** | ⚠️ 半闭 |
| 004 三媒体调度与纸近景模态 | 003 · 001 · 42 近景模态(`ModalId.PaperCloseup48` **已注册**)· 44 字幕管线 | AC-48-09 **BLOCKED-BY-45**;字幕 AC-44-15 的 **48 口述半边 NOT-RUN**(缺口哨兵在) | ⚠️ 半闭 |
| 005 零门槛/零键位提示守卫与走查 | 003 · 004 · 39 story-004 | 🔴 **`OQ-C4`(三媒体节奏 spec)未补** ⇒ 走查 `BLOCKED-BY-SPEC`。实测:`uk/tutorial-48.md:273` 与 `paper-closeup-48.md:300` 均明写「**不代 48 拍时序**,归 48 实现轮」⇒ spec 确实不存在 | 🔴 **硬阻** |

### 2.8 六个尾件(不成链)

| 件 | Type | 依赖 / 阻塞 | 可闭 |
|---|---|---|---|
| `medical-consequences/story-004` | Integration | `story-003`(世界流事件在档)· 13/42/44 呈现通道(并行 epic)· 37 三案链夹具 | ⚠️ 半闭(数值走查用相对次序) |
| `patient-ai/story-005` | Visual/Feel | 🔴 **外部美术**(真人病人模型) | 🔴 **不可由 agent 闭** |
| `emergency-procedures/story-008` | Visual/Feel | 🔴 **外部美术**(处置动作资产);素材缺失时 story-015/016 为 Not-Run 面 | 🔴 **不可由 agent 闭** |
| `world-ecozones/story-005` | Visual-Feel | 🔴 **桌面走查 + 关卡工具 CI 产物**;AC-6-25/26 **EXTERNAL** | 🔴 **不可由 agent 闭** |
| `skeuomorphic-ui/story-019` | UI | `019-b` 判据已冻结,**门待接线** | ⚠️ 部分可闭 |
| `save-slot-ui/story-001` | UI | 无列出依赖;`design/ux/save-slots-7b.md` **在场** | ✅ 可闭 |

> `save-slot-ui` **缺 `EPIC.md`**(实测:31 个 epic 目录里唯一无该文件的)。
> 该 epic 只有 1 件 story(2 h),不立 EPIC 可能是有意为之,但 M3 的分母按目录计 ⇒ 如实登记。

---

## 3. 真实依赖环 —— 🔴 必须用契约桩破环

**findings:存在一条 strict cycle。** 三份 story 的 `Depends on` 互相咬合:

```
inventory-items/story-001  ──►  processing/story-003   ("两支上游事件的产出形状")
        ▲                                │
        │                                ▼
processing/story-001  ◄──────── processing/story-003  ("Story 001(准入)")
        │
        └──► inventory-items/story-001  ("InventoryOf 投影,取实例集")
```

逐边原文(可复算:`grep -o "Depends on:[^|]*" <file>`):

- `inventory-items/story-001` → `… foraging Story 003 / processing Story 003(两支上游事件的产出形状)`
- `processing/story-003` → `… Story 001(准入),Story 002(F1 出参)…`
- `processing/story-001` → `… inventory-items Story 001(InventoryOf 投影,取实例集)`

**为什么这不致命**:三条边依赖的**都不是行为**,是**数据形状**(载荷 struct / 投影签名)。
⇒ 破环手段 = **契约先行**:
1. 先落 `Craft` / `ResourceHarvested` / `DropSpawned` 的**载荷 struct** 与 `InventoryOf` 的**签名**(纯 `Sim.Contracts`);
2. `inventory-items/001` 用**合成流**验证 fold 本体(不依赖 `processing/003` 的实现);
3. `processing/001` 用 `InventoryOf` 的**桩**取实例集;
4. 两侧都落地后,在 `inventory-items/005` **合并联测**。

**⇒ 排期含义**:这三件**不能严格串行**,必须**同批**或**跨批成对**开。
若按「一件一 sprint」串行排,会在第 1 件就卡死。

**另有一组交叉但不构成环的咬合**(同样须同批考虑):
`foraging/005` → `inventory-items/003` → `inventory-items/001` → `foraging/003`(→ `foraging/002` → `001`)。
`foraging/001` 不反向依赖 inventory ⇒ 无环,但**`inventory-items/001` 必须等 `foraging/003` 落地**。

---

## 4. 外部阻塞清单(五类)—— 这五类解除前,相关 AC 永远绿不了

| # | 外部阻塞 | 实测证据 | 卡住的 AC(点名) | 解除者 |
|---|---|---|---|---|
| **E-1** | **数值轮未冻结**(用户裁定) | `assets/data/` 下**无 `disease_registry.json`**;`CompositionRoot.cs:609-625` 用**占位 plateau 曲线**;`entities.yaml:1780` 的 `CARRY_CAP` = `待定` | `AC-20-09`(硬不变量无值可验)· `AC-17-13`(`GATHER_MUL_CAP`)· `AC-44-02/03/04/08/17` 听测半边 · `TD-listen-01` 的 **L-3 / L-4** | **用户** |
| **E-2** | **ADR-012 三格黄金矩阵不完整** | `tests/golden/` 仅 `medcons/` + `telemetry/`;**CI 的 Linux-x64-IL2CPP job 从未跑过**(未配 `UNITY_CLIENT_ID/SECRET`,job 注释自称「从未跑过」);**Linux-ARM64 格在本 pinned 工具链下物理不可建**(46 个 `BuildTarget` 中零个含 arm) | `AC-17-02` · `AC-18-15` · `clinic-machine/005` 跨平台对拍 | **CI 凭证 + 一次 ARM64 格的用户重裁** |
| **E-3** | **45 联机层无 GDD(P1b)** | 全库 `BLOCKED-BY-45` 命中 **36 处 / 16 个文件**(`grep -ro "BLOCKED-BY-45" production/epics/ \| wc -l`);其中 3 处在 `EPIC.md`(audio / prescription / tutorial) | `AC-39-04③` · `AC-48-09` · tutorial 002/003 的联机半边 · `casebook/006` 运行面 | P1b 轮 |
| **E-4** | **外部美术 / 桌面走查** | `patient-ai/story-005`(病人真模型)· `emergency-procedures/story-008`(动作资产)· `world-ecozones/story-005`(桌面 + 工具 CI) | 三件尾 story 本体 | **外部 / 用户桌面** |
| **E-5** | **42 呈现侧未交付的载体** | `processing` AC-18-10b(面板)· `foraging` AC-17-07b/13b(三类失败反馈) | 4 条 [L] 走查 | 42 + 对应 epic |

**⚠️ E-2 的一条独立结论**:`ADR-012 §Validation` 的 **Linux-ARM64 行不是「未跑」,是「跑不了」**
—— 这需要**用户重新裁定该格**(改为「不要求」或「换工具链」),不能靠排期解决。

---

## 5. 陈旧阻塞 / 需订正项(STALE)—— 卡面写了但事实已变

| # | 位置 | 卡面原文 | 实测状态 | 建议处置 |
|---|---|---|---|---|
| **S-1** | `death-respawn/story-003` AC-29-16 | `BLOCKED-BY-OQ-7a-9(折叠豁免待三方裁)` | ❌ **已裁**。`ADR-026 §⑦` 明写「**结清 `OQ-7a-9`**:终态折叠不得抹掉 `SkillGrown`」(案 1)。该 story 自己在 `:49` 也写了「ADR-026 案 1 已裁」 | ✅ **已订正(2026-10-20)**。6 处 `BLOCKED-BY-OQ-7a-9` 全部改为「**BLOCKED-BY-7a 折叠实现**」;原措辞以删除线 + 订正块留痕,未删原文。**阻塞性质变更**:「裁决待定」→「实现待落」。实测 `Folded(p)` 生产谓词全库零命中 |
| **S-2** | `processing/story-001` AC-18-18 | `EXTERNAL · BLOCKED-BY-21a(O-18-R1)—— 字段有 schema、无实现` | ⚠️ **性质已变**。实测:`Sim.Contracts/ItemDatabase/RecipeOwner.cs` 有枚举 · `Recipe.cs:41` 有 `Owner` 属性 · `CookedCodec.cs:181` 烘焙期读取。**但有三个真问题**:① `Sim/` 侧**零消费方**(`grep '\.Owner\b'` 零命中);② 数据表**只有 2 条配方、`owner` 全为 `process`** ⇒ `craft`/`build` 子集为空;③ 「并 = 全表、两两交 = ∅」在空子集下**平凡成立** = **空集借绿** | ✅ **已订正且已补口(2026-10-20)**。AC 改为**三段判据**(非空前置 / 并 / 交),并拆「判据机制可证」与「真表覆盖度」两段记分;① 的解释也订正了 —— `Sim/Processing/` 目录**尚不存在**,该 story 未开工,「零消费方」是预期,不是缺陷;**② 亦已消解** —— 真表补 `craft`/`build` 占位配方后 `owner` 分布 = `{process:2, craft:1, build:1}`(见下行补口记录) |
| **S-3** | `foraging/story-001` | `Ignore("BLOCKED-BY: 21a item_key 行未落地")` | ✅ **阻塞真实**。实测 `item_database_items.json` 的 4 条记录首键为 `base_id`,**无 `item_key` 字段**(`'item_key' in item` = False) | **保持**,真阻塞 |
| **S-4** | `production/milestones/README.md:49` | `22/31` epic 全 Complete | ❌ **陈旧**(三元组 `2026-10-15`)。实测 **18/31** | 里程碑复评轮订正(非本件职权) |
| **S-5** | 四个 epic 的 `EPIC.md` 分数 | `emergency-procedures 7/7` · `patient-ai 4/4` · `skeuomorphic-ui 18/18` · `world-ecozones 6/6` | ❌ **分母偏小**(实际 story 数 9 / 5 / 25 / 6) | 各 epic 复评轮刷账 |
| **S-6** | `save-slot-ui` | —— | ⚠️ **缺 `EPIC.md`**(31 目录中唯一) | 若有意(单件 epic),在 M3 分母口径里注明 |

> **S-2 是本件最重要的一条**:它不是「文档陈旧」,是**一条会恒绿的 AC**。
> 空集子集让「并 = 全表」在零配方时天然成立 —— 这正是本项目反复出现的
> 「自造断言顶替 AC 语义层」的同一形态。
> ✅ **已于 2026-10-20 订正并补口**(见上行 S-2 处置);开 `processing` 链前该判据已在卡面上堵住。
>
> **S-2 结清记录(2026-10-20 · 同日二轮)**:
> - **判据订正**已落卡面(三段判据 + 非空前置),并补两段记分(**禁合并成一条绿**)。
> - **真表补口**已执行:新增 `craft_wooden_splint`(`craft`)与 `assemble_wooden_post`(`build`)
>   两条**占位配方** ⇒ `owner` 分布 = `{process:2, craft:1, build:1}`。
>   ⚠️ **数值为夹具值,非平衡值** —— 真配方内容仍归**数值轮 / 21a 内容批**,本补口不追认内容侧。
> - **判别力已证伪**:删 craft / 删 build / craft 改 `process` ⇒ 真表夹具**三次全转红**
>   (基线自检绿 → 变异红,数据 `sha256` 逐字节还原)。
> - **回归零损伤**:全量 EditMode `3951/3915/0/36`,较前次基线 `3945/3909/0/36` 恰 `+6/+6/0/0`。
> - **补口途中的一次改道**:首版 craft 产出写成 `drug` 类,会踩红 11 的 `AC-11-20`
>   (药类必须在 `materia_lexicon.json` 有功效词 = **11 的内容**)⇒ 改为 `tool` 类,切断耦合。
>   详 `tests/integration/processing/README.md`。
> - ⇒ **S-2 从本件 §五 的「待处置」转入「已结清」**;`processing/001` 的判据面不再有阻塞项
>   (其**主体实现**仍未开工,`Sim/Processing/` 目录不存在 —— 那是排期问题,不是阻塞问题)。

---

## 6. 现在就能开工的件(无硬外部门)

**判据**:该件的全部上游依赖已在盘上(或只需接口桩),且 AC 无 EXTERNAL 阻断。

| 件 | 链位置 | 为什么可开 |
|---|---|---|
| `casebook/001` | 链首 | 只依赖既有的 ADR-010 codec 与 `IIdAuthority` |
| `clinic-machine/001` | 链首 | 只依赖已可运行的 ADR-014 两阶段工具链 |
| `tutorial-onboarding/001` | 链首 | schema + 烘焙校验,缺表报「未就绪」不崩 |
| `save-slot-ui/001` | 孤儿 | 无列出依赖,`design/ux/save-slots-7b.md` 在场 |
| `foraging/001` | 链首 | 上游全 Complete;**但一个子用例因 `item_key` 缺失须挂 `Ignore`**(S-3) |
| `death-respawn/001` | 链首 | 9 的 `DeathCandidate` 未落地,**桩先行**;AC-29-01 全链不记绿 |
| `inventory-items/001` + `processing/001` + `processing/003` | **环 A** | **必须同批开**(承 §三);`processing/001` 的 AC-18-18 **非空前置 + 真表补口已于 2026-10-20 完成**(S-2 结清),开工时判据直接可用,无需再补 |

**⇒ 三个干净的链首**:`casebook/001` · `clinic-machine/001` · `tutorial-onboarding/001`
(加 `save-slot-ui/001` 一件 2 h 的小件)。

**⇒ 一条必须打包的**:`inventory-items/001` + `processing/001` + `processing/003`(环)。

---

## 7. 本件明确**不**覆盖什么

- **不做件选择**:下一个 sprint 做哪几件 = 收口时的 `/sprint-plan`,本件只给约束。
- **不给 ETA / 人日 / sprint 数**(承 CAPACITY-MODEL)。
- **不动任何 story 的 `Status:`** —— 本件是只读勘察产物。
- **不改 EPIC.md 分数、不改里程碑文件**(S-4 / S-5 只登记,处置归各自复评轮)。
- **不裁 `OQ-*`**:未裁项的裁定权归用户(逐条已标解除者)。
- **不含 P1a / P1b 面**(VR · 45 联机层 · 无障碍走查 · 目标硬件)。
- **不含 `TD-listen-01` 的执行**:L-1…L-5 的解除条件已在该债条,本件 §4 E-1 只做引用。

---

## 8. 三条供 `/sprint-plan` 直接复用的硬约束

1. **每批 2–4 件,L 件单独跑**(CAPACITY-MODEL)。
2. **开工前必须重扫该 epic 的 `EPIC.md` + story `Status:`**(`/sprint-plan` Phase 1 硬前置)——
   实测已有 S-1 / S-2 两处「卡面与事实不符」,不复扫就会照旧阻塞或照旧借绿。
3. **`inventory-items/001` + `processing/001/003` 三件不可拆批**(环 A)。
