# 模块化建造

> **评审修订记录(2026-09-17,MAJOR REVISION NEEDED → 修订落盘)**
> 首轮 `/design-review`(`full`)裁 **MAJOR REVISION NEEDED**,8 组 BLOCKING(B1–B8)+ 4 项用户裁定,当日全部修订落盘:
> - **B1 假引用**(ai-programmer):`:347/:348` 声称「ADR-016 §三 已写 27 读合成」「`patient-ai.md:477` 已写」—— 实查 ADR-016 **零处** `EffectiveWalkable` / `OccupancyOverlay`;缝一真实源 = 6 的 GDD(`O-6-10`)。⇒ 改 23 侧为单边 ⚠️;修订 ADR-016 §五(补注 `EffectiveWalkable` 口径);13 GDD 回填;27 于其 GDD 登记。
> - **B2 玩家读合成 = 假边**(unity-specialist):玩家走感 = `CharacterController` collide-and-slide(R3/ADR-020),整数格不服务玩家。⇒ F-23-1 / 规则三 / AC-23-03 移除玩家;**EffectiveWalkable 仅 27 / 13**;ADR-022 C2 挂模板 collider 足迹同源钩。
> - **B3 初始占用无真源**(game-designer + unity-specialist):规则三「世界流是 `Overlay` 唯一真源」与 P0 开档既有家具矛盾。⇒ **用户裁定:烘焙初始占用** —— `Overlay(0) := BakedInitial`(派生态,ADR-015 §二 第二类源),`Overlay(t) := BakedInitial ⊕ 事件`;随此把「关卡工具导出初始占用」落 ADR-022 导出契约。
> - **B4 实体占格判定不可建**(game-designer / systems-designer / ai-programmer / unity-specialist):**用户裁定:玩家 + 敌人**;13 病人结构性不可知(ADR-016 §Validation 禁 13 产事件)→ 13 侧避让。判定式 = 量化格 + 宽容半径(ADR-009 §五 同构),禁 Physics 查询。
> - **B5 F-23-2 缺区域检查**(systems-designer):补 `∀cell ∈ OccupiedCells: cell ∈ BuildSlotRegion` + `(0,0) ∈ OccupiedCells_local` 目录校验。
> - **B6 F-23-3 规格缺陷**(systems-designer):`⌈1/R⌉` 的 Q16.16 形式;`⌊⌋` 注为 ADR-006 舍入例外;**P0 R=1**(用户裁定)+ AC-23-08 配 `DEMOLISH_REFUND_RATIO < 1` 仅 P1a。
> - **B7 `StructureRemoved` 无法推导释放格集**(systems-designer):登记**实例表**为第三份派生态;「实例表为准,载荷仅校验」。
> - **B8 AC 载体集群**(qa-lead ×5):AC-23-04 / 06 / 09 / 13 / 14 全部换机制化载体(反射断言 · 可执行四向用例 · golden-vN 刷新政策 · 编译期不可达写者 · SlotType 标记)。
> - **其余用户裁定**:换模块 **P0 删除归 P1a**(白名单去掉 `module_id` 位,`StructureModified` 仅朝向/变体)· 实体占格对玩家+敌人 · P0 重排 R=1 + 成本走拟物轴(BUILD_TIME/停诊)· 初始占用烘焙。



> **Status**: ✅ Approved(2026-09-17 用户裁定接受首轮修订,免二轮验证)
> **Author**: 用户 + systems-designer / unity-specialist / technical-director
> **Last Updated**: 2026-09-17(首轮评审修订 + Approved)
> **Implements Pillar**: 支柱四(时代即关卡 —— 医馆 = 危难的空间载体 · 建造件 = 通关权限的实物)· 支柱一(判断为骨 —— 布局抉择 = 无对错的空间判断题)
> **评审史**:2026-09-17 首轮 `/design-review`(`full`;5 专家并行对抗 + `creative-director` opus 综合)→ **MAJOR REVISION NEEDED**(8 组 BLOCKING + 4 项用户裁定),**当日全部修订落盘**(见 §评审修订记录)。**2026-09-17 用户裁定接受修订并标记 Approved(免二轮验证)—— 系统 23 结案**。
> **权威件**: ADR-015 §五(Accepted, 2026-09-15)—— 模块化网格 · 槽位对齐 `WorldPos` 同一格 · `Structure*` = 世界流事件
> **上游契约**: ADR-009 §三(Structure 三 Kind 骨架 · 载荷归 R-10 → 现归本 GDD)· ADR-022 §三(`world_buildslots.json` 槽位骨架)· 6 的 GDD(缝一 `O-6-10` · 槽位占用 = 23)
> **参考**: `persistence-service.md`(7a,Approved 格式参照)· `world-and-ecozones.md`(6,缝一来源)· `game-concept.md:378-414`(医馆即机器)· `game-concept.md:697`(P0 范围口)
> **P0 范围口**: `game-concept.md:697` —— **固定医馆 + 小改造(允许更换家具/设施位置;「不允许新建」已于 2026-09-20 用户裁定开一次例外 = 帐篷)**;P1a = 自由建造(722)

## Overview

**模块化建造(23)是全案唯一允许玩家改变世界几何的系统,也是「医馆即机器」的机械侧。**

固定世界的手工烘焙逻辑层(ADR-015 §一)是**只读的**;23 在其上叠加一层**玩家可写的槽位占用**:
玩家把模块(家具 / 设施)放进槽位骨架的格子,每一次放置都以 `StructurePlaced / Removed / Modified`
三条世界流事件被**永久记录**(ADR-009 §三骨架,载荷归 R-10 → 现归本 GDD)。世界由此不是一栋
只读的房子 —— 玩家真的改变了它,改变以三流为证、跨会话逐位保留(7a / ADR-010)。

23 拥有且只拥有三样东西:

1. **槽位占用的真相**(该格上立了什么 —— 6 只给骨架,`O-6-10` 明写「槽位占用 = 23」);
2. **`OccupancyOverlay` 的合成**(缝一 —— `EffectiveWalkable(cell) := Nav[cell].walkable ∧ ¬Overlay.blocked(cell)`,
   27 / 13 / 1 读它,不读 `Nav` 原件);
3. **`Structure*` 世界流事件的写入**(载荷:格坐标 + 模块 id + 朝向 + 变体,ADR-015 §五)。

它**不**拥有:静态导航格(6)· 医馆房间邻接判定与 `CONTEXT_TABLE`(24)· 建造件物品
(21a 定义 / 20 持有)· 任何呈现(42)· 存档字节(7a)。

**P0 范围口 = `game-concept.md:697`:固定医馆 + 小改造;「不允许新建」有且仅有**一次已裁例外 = 帐篷**(2026-09-20 用户裁定,见规则六)。**
玩家在 P0 不新建房间、不拆墙、不改槽位骨架 —— 只在一间固定医馆的烘焙槽位骨架内**重排**
家具与设施。这是「位置即逻辑」的最小验证形态:每一次挪动都改变某个数值(感染率 / 成药率 /
恢复),玩家**看得见**。P1a 才开放自由建造(`game-concept.md:722`),本 GDD 的架构为它留口
(模块目录 / 槽位约束是数据,不是代码)。

## Player Fantasy

**「一间从茅草屋长起来的医馆 —— 我摆的每个零件都会运转,而我怎么摆,暴露了我怎么理解医学。」**

三条,层层加深:

1. **我为布局纠结。** 手术室挪过来,术后感染率降了,但床位就少了(`game-concept.md:93`:
   玩家会为布局纠结)。在有限地块里满足一堆互相冲突的邻近约束 = 空间解谜:
   每动一处,代价和收益同时出现,没有白捡的优化。

2. **我摆的零件真的会运转。** 病房紧挨手术室 → 术后感染率上升;药圃背阴 → 减产;
   丹房离水井太远 → 炮制火候难控;传染病区与普通病房相邻 → 交叉感染(`game-concept.md:387-390`)。
   我摆对房间,感染率降、成药率高 —— 医馆像一台机器,我是这台机器的装配者。

3. **我的医馆就是我自己。** 只懂西医的玩家会建一条干净的流水线(诊室 → 药房 → 手术室,
   **把身体当机器修**);开始有整体观的玩家会想病人住的地方会不会影响恢复
   (**把身体当整体养**)(`game-concept.md:408-412`)。你怎么建医馆,就暴露了你怎么理解医学。

**P0 / P1a 分层兑现标注**(评审修订,game-designer #5):幻想三层对应不同周期 ——
**P0 兑现层 1(我为布局纠结)与层 2 的前半(摆的零件真的运转 —— 依赖 24 的最小邻接判定)**;
**层 2 的后半(感染率 / 成药率 / 恢复的完整数值面)与层 3(医馆 = 自我表达)是 P1a 承诺**。
验收措辞不越档:凡声称 P0 兑现的 AC,只以「位置 → 24 可见后果」为判据,不以 P1a 的完整邻接经济为判据。

P0(纯西医切片)的克制:没有自由建造、没有大兴土木 —— 玩家能做的只是**重排**一间屋子里的
家具。但这已经足够让「位置即逻辑」的雏形被亲手验证,也足够让 Expression(支柱三的实物面)
第一次被搭起来:你的布局选择不是装饰,是判断。

## Detailed Rules

### 规则一 —— 槽位骨架与模块目录(两套数据,一层真相)

23 运行期消费**两套烘焙数据**(ADR-022 §三 作者态 → ADR-014 烘成 `*.cooked`,`data-core` 预载):

- **槽位骨架**(`world_buildslots.json`):整数格 + 槽位约束 —— 哪些格可以放什么类型的模块。
  它是**派生态**(ADR-021 的「定义」侧):6 只给格、关卡工具唯一作者、加载期重建、**不进任何事件流**。
  **槽位骨架含初始占用标记**:`BakedInitial`(开档既有家具的占用,评审裁定 B3,规则三)。
- **模块目录**(`build_modules.json`,21a 物品表的 `category = build_part` 扩展,归 21a 拥有):
  `module_id` → 占用格集(相对于锚点的整数格偏移)、槽位类型约束、朝向集合、变体集合。
  **目录校验必须声明 `(0,0) ∈ OccupiedCells_local`**(锚点格本身计入占用,否则出现「锚点可走、结构在其上」,评审 B5)。

**一层真相 = 槽位占用**(模拟态):`slot_occupied(WorldPos) : bool` + `structure_at(WorldPos) : module_id`。
它**只由 23 写入**(规则三),**只经世界流重建**(规则四)。重放时从 `Structure*` 事件逐条重建,
不存额外副本 —— 与 ADR-016 §一 的三源不变量一致(占用是模拟态,但可由事件流纯函数重建)。
**开档基线除外**:`Overlay(0) := BakedInitial`(规则三,评审裁定 B3)。

> **23 不拥有模块目录本身**(数据归 21a / 20),只拥有「哪个实例立在哪个格」的**真相**。
> 类比:`StructurePlaced` 是落格事件,不是物品入库 —— 物品身份仍归 20(`instance_id` 空间,
> 世界流 `Drop*` 系列)。**两者经 `module_id` 关联,不合并。**

### 规则二 —— 放置与合法性判定(先判后写,判定进 sim)

放置 = **意图事件 + 当下判定**(承 ADR-009 §五 拾取判定的同构):玩家发出放置意图,
23 在 sim 侧做**确定性合法性判定**,通过才 Append `StructurePlaced`。

判定五条件(**全部在 sim 整数域,禁 float**;评审 B4/B5 扩为五条 —— 原四条 + 区域 + 实体):

1. **槽位合法**:目标锚点 ∈ 槽位骨架的允许格集 ∧ 该格允许本 `module_id` 的类型;
2. **占用为空**:`slot_occupied` 为假(占用集内每个格都不被占);
3. **占用格全部在骨架区域内**:`∀cell ∈ OccupiedCells(m, anchor): cell ∈ BuildSlotRegion`(评审 B5 ——
   只判锚点会放「穿墙」模块;负偏移的占用格同样受此约束);
4. **骨架未改**:槽位骨架是烘焙数据,玩家不可改(改 = 新建,归 P1a,规则六);
5. **数据可及 + 实体空**:`module_id` ∈ 模块目录 ∧ 玩家库存有对应 `build_part` 条目(从 20 读,20 裁决扣除)
   ∧ **占用格上无实体**(评审裁定 B4 —— 玩家与敌人,见规则二之注)。

任一不满足 ⇒ 拒绝。拒绝**不 Append 任何事件**(静默失败不存在 —— 拒绝本身就是结果,
可感知为「放不下」的呈现,归 42)。

> **判定 5 的「实体」范围(评审裁定 B4)**:sim 判定只对**玩家**(`ActorCellEntered` 跨格世界流事件,ADR-020 §四)
> 与**敌人**(逻辑位姿 sim 原生,ADR-016 §一)生效。**13 病人结构性不可知**(ADR-016 §Validation 禁 13 产事件;
> 13 的格是表现态)⇒ 不查病人。病人被困 / 穿模由 13 侧避让(13 读 `EffectiveWalkable` = 合法只读输入)+ 视觉宽容处理。
> **判定式 = 量化格 + 宽容半径**(与 ADR-009 §五 拾取判定同构),**禁 Physics 查询**(门 A 内无引擎调用,unity-specialist #2)。

> **P0 重排成本(评审裁定)**:**P0 拆除全额返还**(`DEMOLISH_REFUND_RATIO = 1`,规则九)—— 纯重排**净零材料损耗**,
> 代价挪到拟物轴(`BUILD_TIME` / 医馆停诊,规则九)。**P1a 恢复 `R < 1`**(材料经济进 P1a,规则九)。

**P0 小改造的换位**:家具从 A 移到 B ⇒ 两步语义 —— ① `StructureRemoved(A)`;② 判定通过则
`StructurePlaced(B)`。若 B 被占,则须先把 B 移走(递归一步,禁止跨越式交换的「原子 swap」):
**P0 不做原子换位** —— 先拆后放,拆下来的模块进 20 库存(规则九)。这是「判断为先」的
机械侧落实:换位是两次有代价的决定,不是一次无代价的撤销。

> **为什么不做原子 swap**:原子 swap = 免费撤销(把 A、B 互换再换回来不损失任何东西),
> 与 7a 规则十四「没有 30 秒前可回滚」同源 —— 医馆布局的选择必须是**有代价的**。

### 规则三 —— 槽位占用所有权与 `OccupancyOverlay`(缝一,23 的独门义务)

**槽位占用 = 23**(6 的 GDD `O-6-10` / `world-and-ecozones.md:781` 明写)。6 只给骨架,不持有占用;
24 读布局做邻接,不写占用;27 / 13 只读合成结果。

**真源模型(评审裁定 B3)**:世界流是**玩家产生的占用变更**的唯一真源;但**开档基线 = 烘焙初始占用**
(`BakedInitial`,派生态 —— ADR-015 §二 第二类源「版本化烘焙数据」):

```
Overlay(0)      := BakedInitial              // 关卡工具烘焙(ADR-022 导出契约,规则一)
Overlay(t)      := BakedInitial ⊕ Structure* 事件序列   // 重放:基线 ⊕ 变更
```

- 开档家具**不合成 `StructurePlaced` 事件**(无写者、无时点、不涉主机 Append —— creative-director 否决该方案);
- `BakedInitial` 的占用**不进任何事件流、不存档**(世界流无对应事件;快照若存也只是加载加速,ADR-009 §六);
- 初始家具的 `structure_id` 由 `BakedInitial` 定义时分配(进实例表,规则七),玩家对初始家具的**第一次**改动
  = 第一条 `StructureRemoved`(基线对应行),之后世界流照常。

23 在每次占用变更时产出**覆盖层** `OccupancyOverlay: WorldPos → blocked?`:
一个格被模块占用 ⇒ 该格 `blocked = true`(占用的可走性侧含义)。合成规则(F-23-1):

```
EffectiveWalkable(cell) := Nav[cell].walkable ∧ ¬Overlay.blocked(cell)
```

- **`Nav`(静态导航格)归 6**,会话内不变(R-6-13);
- **`Overlay`(占用覆盖)归 23**,随 `Structure*` 事件变更(开档基线 = `BakedInitial`);
- **合成 = 23**(不是 27、不是 6、不是 24)—— 唯一合成点,否则「这个格能不能走」有两个答案;
- **27 / 13 读 `EffectiveWalkable`,不读 `Nav` 原件**(否则 AI 走进玩家盖墙的格 = 「AI 跟不过来」);
  **玩家不读它**(评审裁定 B2 —— 玩家走感 = `CharacterController` collide-and-slide,R3/ADR-020,
  整数格不服务玩家;玩家与建筑碰撞由 42 实例化模板的 collider 承担,见规则八);
- **`Overlay` 是派生态**:不进流、不存档,重放时从 `BakedInitial ⊕ 事件`**逐条重建**(ADR-016 §一 三源不变量)。
  世界流是 `Overlay` 的**变更**唯一真源;快照若存 `Overlay` 也只是加载加速(ADR-009 §六),不是真相。

> **重算对象点名**(承 6 的 `O-6-10` 注):ADR-015 §四③ 的「脏块导航失效」重算的是
> **`Overlay` 的局部**(占用变更的格),**不是** 6 的静态烘焙 `Nav`(会话内不变)。
> **实现建议**(unity-specialist #6):`Nav` / `Overlay` / `EffectiveWalkable` 按 chunk 存 `ulong` 位掩码
> (~64 格/字),A* 高频轮询走 O(1) 字与;重算已即时(规则九),无需懒算。

### 规则四 —— 世界流事件与载荷定稿(23 是 Structure 三 Kind 的写者)

23 是 `StructurePlaced / Removed / Modified` 的**唯一写者**(ADR-009 §三骨架;`StructureModified`
的写者分界见规则十)。全部落**世界流**(`StreamPriority` 世界级,`Patient = PatientId.None`,
不污染 `max(patient_id)` 高水位 —— ADR-007 §四)。主机唯一 Append(ADR-005)。

载荷定稿(**全整数,禁 float** —— ADR-006 边界自动覆盖;评审裁定 B3/B7):

```
StructurePlaced   { structure_id, cell: WorldPos, module_id, orientation, variant }
StructureRemoved  { structure_id, cell: WorldPos, module_id }
StructureModified { structure_id, cell: WorldPos, module_id,
                    new_orientation, new_variant, modified_fields }
```

- `structure_id`:结构实例的稳定身份,**与 `ItemInstanceId` 同模式**(7a 的 `IIdAuthority`,
  计数器 + 高水位可重构,ADR-010 §五)—— 跨系统共用同一 id 空间,折叠谓词不适用于结构行;
- `cell` = **锚点格**(`WorldPos`,整数),模块的其余占用格由 `module_id` 的占用集推导(规则一);
- `orientation` / `variant` = 整数枚举(朝向量化到格对角 / 变体索引);
- `modified_fields`:位集枚举(**朝向 / 变体 —— 白名单,由模块目录声明**)。**评审裁定:去掉「换模块」位**
  —— 单 `module_id` 字段编码不了 old→new 双 id(游戏设计师 #2),且原位换模块 = 绕过
  `DEMOLISH_REFUND_RATIO` 的免费撤销通道(系统设计师 #6)。换模块语义 + 成本模型整体归 **P1a**
  (P1a 时载荷改双 id = 破字节稳定,须同步走 ADR-012 §三 golden-vN 刷新);
  **只记录「物理上变了的」,不记录任何数值结果**(数值是 24 的派生,规则十)。

> **实例表(评审裁定 B7)**:重放时从 Placed / Modified 累积的
> `structure_id → (anchor, module_id, orientation, variant)` 是**第三份派生态**(除 `slot_occupied` /
> `Overlay` 外)—— 登记它。`StructureRemoved` 载荷**无** `orientation` / `variant`,多格模块的释放格集
> 由实例表推导。**实例表为准,载荷仅校验**(存档篡改 / 漂移时以实例表重建,载荷字段不符即校验失败,不静默)。

> **同一实例的视觉网格是表现态**(ADR-015 §五):不进流、不存档。世界流只载结构身份与
> 位置姿态;呈现层经 `structure_id` 对应视觉,重建期由烘焙模板实例化。

### 规则五 —— 与 24 医馆即机器的边界(23 出布局,24 出判定)

| 边 | 23 交付 | 24 消费 | 判定归谁 |
|----|---------|---------|---------|
| 布局查询 | 结构占用视图:`structure_at(cell) → module_id + orientation` | 邻接判定的**输入** | 24 读 23 |
| 邻接判定 | ✖ 不提供 | `CONTEXT_TABLE`(房间格 → 效果乘数) | **24**(承 ADR-015 §五:医馆「红石逻辑」= 整数格邻接判定,非物理 / 非 NavMesh) |

- **23 不读 24 的数值结果**(感染率 / 成药率 / 恢复)—— 布局提供方不看业务后果,单向无环;
- **24 不写占用**(只读布局)—— 规则三的所有权不被动摇;
- **邻接的「房间」语义归 24**:23 只认「模块立在哪些格」,「这些格构成什么房间、有什么属性」
  由 24 从布局 + 房间定义派生;
- **CONTEXT_TABLE 归 24**(系统 1 的 `O-2` 已登记 24 → 1 的反向依赖),**不归 23**。

> **P0 隐性前置(评审,game-designer #5)**:P0 切片验证「位置即逻辑」需**最小 24** —— 哪怕一条
> 邻接规则(如「手术室邻病房 → 术后感染率」)。23 单独成文不阻塞,但此前置未登记 ⇒ 现登记为
> **`O-23-4`**(24 的 GDD 须含 P0 最小邻接;见 Dependencies)。

### 规则六 —— P0 / P1a 范围门(本 GDD 最重的边界)

**P0 = 固定医馆 + 小改造**(`game-concept.md:697`),**唯一例外 = 序章搭帐篷**(见下):

- 医馆的**外壳(墙壁 / 地基 / 房顶)是烘焙槽位骨架的一部分,玩家不可动** —— 拆墙 / 建墙 = 新建,不在 P0;
  - **P0 唯一例外 = 帐篷**:序章的一次新建,落关卡工具在 `world_buildslots.json` 预置的 `tent` 槽(骨架外预置槽,**不破「不改骨架」**);`StructurePlaced` 照常发,载荷形状不变(承 2026-09-20 改判注 `game-concept.md:697-698`);第二/三间棚子仍归 P1a;
- 玩家可做:**重排**家具与设施(规则二)· 拆除家具(规则九)· 放置模块目录内允许的模块;
- 槽位骨架的**可放集合**由关卡工具烘焙(`world_buildslots.json`),P0 只烘焙**一间医馆**的骨架;
- **换位成本走拟物轴(评审裁定)**:P0 拆除全额返还(规则九),代价 = `BUILD_TIME` / 医馆停诊
  (病人等待、照料受损)—— 「布局是判断,不是撤销」由时间轴承担,玩家不被材料账本困住。

**P1a = 自由建造**(`game-concept.md:722`):新建房间 / 拆墙 / 扩骨架 / **换模块**(评审裁定归 P1a,规则十)。

> ⚠️ **2026-09-20 追加裁定(用户,一致性检查轮)**:「**P0 只作为测试切片,也要能新建**」—— 该次新建的**唯一形态 = 帐篷**(序章)。它不是自由建造的一部分,而是**在 P0 内证明「玩家能改变世界几何」这条循环成立**的最小样本;终局形态 = 完整游戏可做《英灵神殿》式世界建设(P1a 自由建造 → 后续),故槽位骨架 / 模块目录 / `Structure*` 三 Kind 的形状**必须在不加系统的前提下承载它**。
**本 GDD 为它留口**:模块目录与槽位约束是**数据不是代码**(规则一),P1a 只是烘焙更大的骨架 + 更宽的目录;
`Structure*` 三 Kind 的载荷不变,世界流的形状不需要改。**任何「改代码才能支持自由建造」的
设计在 P0 就是回归**(AC-23-BL 判据,见 Acceptance Criteria)。

> **P1a 承诺的精确化(评审,unity-specialist #3)**:「自由建造」= **槽位骨架集合的扩展 + 目录加宽**;
> **P0 的外壳(墙)在 P1a 可拆的前提 = 外壳作为 `BakedInitial` 占用参与合成**(规则三)—— 若外壳的可走性
> 被烘进 6 的静态 `Nav`,则 P1a 拆墙需改 `Nav`,overlay 模型做不到 ⇒ **外壳必须走初始占用路径**。
> 本 GDD 承诺:外壳 = `BakedInitial` 的一部分,P1a 拆墙 = 移除基线占用,`Overlay` 模型不变。
> **「任意格自由建造」不在 P1a 承诺内**(那要改 F-23-2①,属 P2);P1a 承诺 = 骨架集合更大,判定式不变。

### 规则七 —— 建造材料(`build_part`)与 20 的结算

- 模块目录挂在 21a 物品表(`category = build_part`,`item-database.md:135`),**归 21a 拥有**;
- **20 裁决库存**:放置的判定条件 ⑤(规则二)读 20,20 在 `StructurePlaced` 被接受后**扣除**
  对应 `build_part`;拆除时**返还**(规则九);
- **23 不拥有物品**:只声明「需要 `module_id` 对应的 `build_part` 条目」,不裁决数量 / 重量 /
  堆叠 —— 那是 20 的领域;
- **结构身份 ≠ 物品身份**:`structure_id`(结构实例)与 `instance_id`(物品实例)是两个空间,
  经 `module_id` 关联;一个 `build_part` 物品被消耗,换来一个结构实例 —— **不合并**;
- **P0 / P1a 的 `build_part` 无实例级语义(评审冻结,game-designer #6)**:`StructurePlaced` 无
  provenance 字段,`build_part` 不携带品质 / 炮制来源等实例级信息。改载荷 = 破 AC-23-09 字节稳定,
  故该冻结回写为对 21a 的约束(义务表 `O-23-5`)。

### 规则八 —— 呈现层边界(42 只渲染,永不持有)

承 ADR-013 §9 C3(42 只渲染 / 永不持有游戏状态)与呈现层三件套纪律(42 / 44 / 51 同构):

- 23 **不接触任何呈现 API**(无 Mesh / 无 Transform / 无 GameObject 实例化);
- 42 呈现结构 = **表现态实例化**:读 `structure_at` 查询 + 烘焙模块模板(ADR-014),
  渲染视觉网格;**42 不得成为 `Structure*` 事件的新写者**(不 Append,只订阅);
- **23 交付的查询接口**(供 42 / 24 / 27 / 13 只读):`OccupancyOverlay` 与 `EffectiveWalkable`(F-23-1);
  **玩家不读 `EffectiveWalkable`**(评审裁定 B2,规则三);
- **OQ-2-3 的裁决点在这里触发**(规则十 / Dependencies 表):23 的 GDD 开写 = 「建造取景档」判据
  到期 —— 详见规则十与 `camera-and-viewpoint.md` OQ-2-3。

> **玩家与建筑的碰撞边界(评审,unity-specialist #1)**:玩家不读整数格,「这里能不能走」对玩家 = 
> **42 实例化的模板 collider**(PhysX)的答案。两套答案(整数 overlay vs 网格 collider)必须一致:
> 模板 collider 足迹须**从同一占用格派生**,collider 高度 > `stepOffset`(除非模块显式声明可走过,
> 如地毯)—— 该一致性挂 **ADR-022 C2** 钩(见 Dependencies / 涟漪)。
> **重放实例化**(unity-specialist #4):重建只重放占用表(数据);Addressables 实例化
> (`InstantiateAsync` / 拆除 `Release`,未释放句柄 = 经典泄漏)与 catch-up **解耦**、可淘汰 / 延迟;
> EC-23-07 的占位模板必须与 `data-core` 同批预载(E-13 硬失败纪律)。

### 规则九 —— 拆除与回收

- 拆除 = 意图事件 + 判定(承规则二):`structure_id` 存在 ∧ 拆除方有权限(联机 P1b)→ Append `StructureRemoved`;
- **返还**:拆除后对应 `build_part` **按返还率**归还 20 库存(旋钮 `DEMOLISH_REFUND_RATIO`,F-23-3);
  **P0 裁定:全额返还(`R = 1`)** —— 纯重排净零材料损耗,代价走拟物轴(下述);
  **P1a 恢复 `R < 1`**(材料经济进 P1a,规则二 / 规则六);
- **P0 拆除的对象**:仅家具 / 设施(可动模块);不可动外壳(规则六)无拆除;
- **拆除时被占格变为可走**(`Overlay.blocked` 清位)—— 但**即刻重算**,不等下一次放置
  (规则三的「占用变更即重算」对拆除同样生效);
- **拆除时模块下有实体**(玩家 / 敌人):**拒绝**(格上有实体不可拆,规则二判定 ⑤ —— 评审裁定 B4,
  实体 = 玩家 + 敌人;13 病人不查,避让);联机(P1b)才需要「逐出 / 悬停」的降级语义 —— 见 Edge Cases;
- **P0 拆除不是免费撤销的窗口 —— 代价走拟物轴**:`BUILD_TIME`(放置耗时)+ **医馆停诊**
  (拆除 / 重排期间该模块对应的照料 / 加成离线,病人等待)。「每动一处,代价和收益同时出现」
  由时间轴承担;材料账本不困住玩家(评审裁定,creative-director 综合)。

> **P0 判据落地**:`DEMOLISH_REFUND_RATIO` 在 P0 恒等于 1 时,AC-23-08 的 `cost ≥ ⌈1/R⌉` 目录校验
> 在 P0 自动满足(P1a `R < 1` 时重新生效)。旋钮表同步标注(见 Tuning Knobs)。

### 规则十 —— `StructureModified` 写者分界与 24 的联动(本 GDD 的承重裁决点)

`StructureModified`(ADR-009 §三:改造 / 医馆即机器状态变更,**23 / 24**)的写者分界,
**在 sim 侧只有一个写者,不允许双写**:

- **物理形态变更(朝向 / 变体)= 23** —— 玩家操作的结构改动,23 判定后 Append;
  **评审裁定:换模块(`module_id` 变更)不在 P0 白名单,归 P1a**(规则四 / 规则六)—— 载荷单
  `module_id` 字段编码不了 old→new,且原位换模块绕过 `DEMOLISH_REFUND_RATIO`;
- **数值结果(感染率 / 成药率 / 恢复)不是事件** —— 24 从布局 + 房间定义**派生**,不进世界流
  (与 ADR-016 §一「效果进流,决策不进流」同构:布局进流,布局的**后果**是 24 的派生);
- **`modified_fields` 只载物理形态**(规则四),数值后果由 24 重算 —— 23 不读、不写、不缓存;
- 因此 **`StructureModified` 的唯一写者 = 23**;24 通过**读布局变更**感知变化,不 Append
  `StructureModified`。这同时结清 ADR-009 骨架里「23 / 24」的双名 —— **写者收敛为 23**,
  24 是布局的读者 + 数值的派生者;
- **改朝向 / 变体的判定(评审,game-designer #7)**:改朝向会变占用格集,须走占用检查
  (`Modifiable(m, anchor, new_orientation) := 新占用集 ∖ 自身旧格 全空 ∧ TypeOK`),否则可压到
  邻格占用。P0 朝向 ⊆ {0°} 使其潜伏,但 AC-23-06 要求判定代码支持四向 —— **此判定现在写**,
  否则 P1a 加旋转 = 改 sim 代码 = 回归。

**OQ-2-3(建造取景档)裁决**:23 的 GDD 开写即触发。判据 = 「取景需求是否真的不同」
(`camera-and-viewpoint.md` OQ-2-3)。**P0 的建造 = 医馆内小改造**,与 `Explore` 档(医馆越肩)
共享取景空间 —— **P0 不新增第四档**;待 43 纸质地图 GDD 开写时再统一裁(采药 / 地图是否需要
各自档)。此裁决登记为本 GDD 对 2 的**义务**(Dependencies 表反向边)。
**评审补注(unity-specialist #5)**:P0 复用 `Explore` 档有两个呈现缺口 —— ① 小医馆内越肩
镜头会被墙 / 货架遮挡(需档内软拉近,非新增档);② 手柄无指针(ADR-011 要求完整手柄),
42 需**幽灵循环选择**放置交互规格(float→格 走单一文档化的 floor 量化器)。两缺口登记为对
42 / 2 的义务(义务表 `O-23-6` / `O-23-7`)。

## Formulas

> 全部整数域,禁 float(ADR-006)。数值均为旋钮(`*待定*`),用户定值。

### F-23-1 —— 有效可走性合成(缝一,唯一合成点)

```
EffectiveWalkable(cell) := Nav[cell].walkable ∧ ¬Overlay.blocked(cell)
```

| 变量 | 定义 | 域 | 拥有者 |
|------|------|-----|--------|
| `cell` | 整数格坐标 | `WorldPos`(ADR-015 §三) | 6(格)· 23(占用) |
| `Nav[cell]` | 静态导航格:该格原生 `walkable / block / cost` | `bool` | **6**(烘焙,会话内不变, R-6-13) |
| `Overlay.blocked(cell)` | 23 的占用覆盖:该格被模块占用 | `bool` | **23**(规则三,`Overlay(0) = BakedInitial`) |
| `EffectiveWalkable(cell)` | 合成结果:AI 可走的真值 | `bool` | **23**(合成),27 / 13 只读 |

**读方**:27 敌人 AI / 13 病人 AI —— 一律读 `EffectiveWalkable`,不读 `Nav` 原件。
**玩家不读**(评审裁定 B2 —— 规则三;玩家走感 = `CharacterController` collide-and-slide)。
**推论**:`EffectiveWalkable = false` 的充要条件 = 原生不可走 ∨ 被占用(与判定 2 的占用定义严格同一 ——
**同一份占用表**,一个写成 `Overlay`,一个写成 `slot_occupied`,不允许两套副本漂移,AC-23-04)。
**合成基线**:`Overlay(0) = BakedInitial`(开档家具,评审裁定 B3),事件流只载其**变更**。

### F-23-2 —— 放置合法性判定(规则二,全量五条件)

```
Placeable(m, anchor) :=
    anchor ∈ BuildSlot(骨架)                          // ① 槽位合法
    ∧ TypeOK(SlotType(anchor), m.module_type)         // ① 类型约束
    ∧ ∀cell ∈ OccupiedCells(m, anchor): ¬slot_occupied(cell)   // ② 占用为空
    ∧ ∀cell ∈ OccupiedCells(m, anchor): cell ∈ BuildSlotRegion // ③ 区域包含(评审 B5)
    ∧ skeleton 未变(骨架 = 烘焙数据,规则六)           // ④ 骨架未改
    ∧ m ∈ ModuleCatalog ∧ Stock(20, m) ≥ 1            // ⑤ 数据可及 + 库存
    ∧ ∀cell ∈ OccupiedCells(m, anchor): ¬EntityOnCell(cell)    // ⑤ 实体空(评审 B4,玩家+敌人)
```

| 变量 | 定义 | 域 |
|------|------|-----|
| `m` | 模块实例候选 | `module_id` |
| `anchor` | 目标锚点格 | `WorldPos` |
| `BuildSlot` | 槽位骨架的允许格集(烘焙) | `set of (WorldPos, SlotType)` |
| `BuildSlotRegion` | 槽位骨架的**区域**(含初始占用,规则三) | `set of WorldPos` |
| `SlotType(anchor)` | 该格的槽位类型约束 | enum(床 / 台 / 柜 / 圃 … 由模块目录声明) |
| `OccupiedCells(m, anchor)` | 模块的占用格集(相对于锚点,由目录的占用集推导) | `set of WorldPos` |
| `slot_occupied(cell)` | 占用真相(规则一) | `bool` |
| `Stock(20, m)` | 20 侧该 `build_part` 的可扣数量 | int ≥ 0 |
| `EntityOnCell(cell)` | 该格有实体(玩家 / 敌人;量化格 + 宽容半径,规则二注) | `bool` |

**输出**:`true` → Append `StructurePlaced`;`false` → 拒绝,不 Append。
**量纲检查**:全部条件为布尔谓词,无数值输出;`OccupiedCells` 由目录数据推得,
**不引入浮点几何**(朝向旋转后的格集由整数旋转矩阵推导,见 F-23-2b)。
**目录校验**:`(0,0) ∈ OccupiedCells_local`(锚点格计入占用,评审 B5);目录的占用格
**全部落在 `BuildSlotRegion` 内**(越界条目构建期拒绝)。

### F-23-2b —— 朝向格集(整数旋转,禁浮点旋转矩阵)

```
OccupiedCells(m, anchor, orientation) :=
    { anchor + R(orientation) · local_offset : local_offset ∈ OccupiedCells_local(m) }
```

`R(orientation)`:朝向 → 格集旋转,**仅四向(0° / 90° / 180° / 270°),整数置换**:
`R(90°)(x, y) = (−y, x)`,在整数格上无浮点中间量。**禁任意角度** —— 模块目录只声明
四向子集(ADR-015 §五「朝向」= 整数枚举,规则四)。

| 变量 | 定义 | 域 |
|------|------|-----|
| `local_offset` | 模块自身的占用格(相对锚点) | `set of (i32, i32)` |
| `R(orientation)` | 四向整数旋转 | 置换 |
| `OccupiedCells` | 世界格上的占用集 | `set of WorldPos` |

**平面归属(评审,systems-designer #2)**:旋转作用于 `WorldPos` 的**水平平面** —— 竖坐标 `y`
(高度格)不参与旋转;`R(90°)(x, y) = (−y, x)` 的 `x / y` 为水平平面两轴(顶层构建均为 `y = 0`,
若医馆有楼层,楼层为 P2,不在本 GDD)。**整数记号与 ADR-015 §三 的 `(i32,i32,i32)` 对齐**:
本公式的 `(x, y)` 是水平面投影,竖直 `z` 恒等。

**P0 约束**:P0 模块目录的朝向集合 ⊆ `{0°}`(固定朝向)—— 简化夹具,不引入旋转语义
(旋转是 P1a 目录扩展,规则六留口);**但判定代码必须支持四向**(AC-23-06),否则 P1a 改代码 = 回归。
**改朝向判定(评审,game-designer #7)**:`Modifiable(m, anchor, new_orientation) :=`
新占用格集 ∖ 自身旧格 全空 ∧ 新朝向 TypeOK —— 现在写,不在 P0 目录生效时潜伏(规则十)。

### F-23-3 —— 拆除返还

```
Refund(m) := ⌊cost(m) × DEMOLISH_REFUND_RATIO⌋   (整数,向下取整)
```

| 变量 | 定义 | 域 | 拥有者 |
|------|------|-----|--------|
| `cost(m)` | 模块 `m` 的建造材料数(目录数据) | int ≥ 1 | 21a |
| `DEMOLISH_REFUND_RATIO` | 返还率 | Q16.16,`0 < R ≤ 1`;**P0 恒 = 1**(评审裁定,规则九);P1a 恢复 `< 1` | 用户(旋钮) |
| `Refund(m)` | 返还 20 的 `build_part` 数 | int ≥ 0 | 20 结算 |

**性质(评审,creative-director 综合)**:P0 `R = 1` ⇒ 重排净零材料损耗,代价走拟物轴(规则九),
玩家不被材料账本困住 —— 「布局是判断不是撤销」由 `BUILD_TIME` / 停诊承担。
P1a `R < 1` ⇒ 换位两次净亏材料(材料经济进 P1a,规则六)。
**舍入例外(评审 B6)**:`⌊⌋` 向 −∞ 截断,**非** ADR-006 的 `ROUND_HALF_AWAY_FROM_ZERO`。
操作数全非负、无 ties ⇒ 此处行为等价、安全 —— 但**须显式注为本式例外**,否则实现者会「纠正」成
`Math.Round`(违反禁浮点)。**Q16.16 求值**:`⌊cost × R⌋ = (cost × rawR) >> 16`,`rawR` = R 的
Q16.16 原始值;`⌈1/R⌉ = ⌈65536/rawR⌉ = (65536 + rawR − 1) / rawR`(整数)。
**边界**:`R → 0` 时 `⌈1/R⌉` 无上限(P1a 若取极小 R 会禁掉一切可返还模块 ⇒ 目录校验构建期失败,
**预期行为,须写**)。`cost = 1` 且 `R < 1` 时 `Refund = 0` —— 消耗性家具由目录 `refundable = false`
显式声明,**不是 bug**(见 Edge Cases);P0 `R = 1` 时 `Refund = cost`,此边界不触发。

## Edge Cases

> 每条 = 明确「发生什么」,不写「优雅处理」。

| # | 情形 | 处理(23 侧) | 判定归属 |
|---|------|-------------|---------|
| **EC-23-01** | 目标锚点已被占(换位时 B 格被占) | **拒绝** `Placeable = false`;玩家须先拆 B(递归一步)。**P0 无原子 swap**(规则二) | 23 |
| **EC-23-02** | `StructureRemoved` 时 `structure_id` 不存在(重复拆除 / 流损坏) | **拒绝**,不 Append。重放时此事件不可达(占用真相由事件流重建,规则三) | 23 |
| **EC-23-03** | 拆除时模块下有实体(玩家 / 敌人) | **拒绝拆除**(格上有实体不可拆,规则九 · 判定 ⑤ —— 评审裁定 B4:**实体 = 玩家 + 敌人;13 病人不查**,避让)。**判定式 = 量化格 + 宽容半径**(ADR-009 §五 同构),**禁 Physics 查询**。联机 P1b 才做「逐出 / 悬停」 | 23(判定)+ 13 / 27(占格) |
| **EC-23-04** | 拆除造成玩家 / AI 站在「刚变不可走」的格上(1 的 `:1538` 下落情形) | 玩家:表现层自然下落(1 的 `player-controller-and-movement.md:1538` 已登记);**sim 侧不补「禁拆玩家所在格」** —— P0 拒绝语义已含「格上有实体不可拆」,故 sim 侧不可达。AI:27 / 13 走 `EffectiveWalkable`,放置 / 拆除令起点格变 blocked ⇒ **下 tick 重规划**(评审,ai-programmer #3 —— 失效谓词 = 路径格 ∩ 新 blocked ≠ ∅ ⇒ dirty ⇒ 重规划;P0 规模全量重规划可负担) | 1(呈现)+ 23(拒绝)+ 27 / 13(重规划) |
| **EC-23-05** | 槽位骨架改版(`ConfigVersion` 变化,ADR-010 §七 / ADR-014 §五) | 已放置结构**保留**(世界流原样);新放置用新骨架。**不迁移结构位置** —— 迁移 = 改写玩家资产,不做。**`BakedInitial` 随骨架版本化**(评审 B3):旧存档重放用旧基线 | 23 + 7a(ConfigVersion 比对) |
| **EC-23-06** | 目标格所在 chunk 未驻留(ADR-015 §四 流式) | 槽位骨架**全部驻留**(`data-core` 预载,ADR-014 §五);结构占用查询不受 chunk 影响。**放置判定不因驻留与否改变** | 23 + 6(chunk 键) |
| **EC-23-07** | `module_id` 不在目录(数据漂移 / 旧存档) | 放置判定 ⑤ 拒绝;重放时该结构行**保留为不透明实例**(无模板可实例化,呈现层显示占位)。**不静默丢弃**(AC-23-BL 关联)。**占用真值未定(评审,ai-programmer #4)—— 显式裁定:不透明实例按其锚点单格计 blocked**(占用集不可推导 ⇒ 退化为单格占,保守安全;同一事件流 + 不同版本目录不会再烘出两个 `Overlay`) | 23(判定)+ 42(占位呈现) |
| **EC-23-08** | `cost = 1` 且 `R < 1` ⇒ `Refund = 0` | **预期行为,不是 bug**:消耗性家具(如一次性台布)由目录 `refundable = false` 显式声明;可返还家具须 `cost ≥ ⌈1/R⌉`(目录校验,AC-23-08)。**P0 `R = 1` 时本边不触发**(规则九) | 21a(目录校验)+ 23 |
| **EC-23-09** | 联机(P1b):两名玩家同时拆 / 放同一格 | 主机唯一判定 + 唯一 Append(ADR-005);**后到的判定重跑**,第一个成功者赢,失败者收到「被占用」拒绝。**P0 无联机,此语义仅登记** | 45(传输)+ 23(判定) |
| **EC-23-10** | `StructureModified` 只改朝向 / 变体,不改模块 | `modified_fields` 只含对应位;23 Append;24 重算邻接。**不发第二条 `StructureRemoved + Placed`**(同一 `structure_id`,连续身份)。**改朝向须过 `Modifiable` 占用检查**(规则十) | 23 |
| **EC-23-11** | 拆除时返还失败(20 库存已满 / 不可扣) | 返还走 20 的标准入库路径;**失败 = 掉落在地上**(世界流 `DropSpawned`,归 20 / 25)。**不静默吞返还**(评审,qa-lead #7 —— `DropSpawned` 发出者 = 20 结算,23 触发;测试文件归属在 AC-23-16 载体写明) | 20(结算)+ 23(触发) |
| **EC-23-12** | 放置被 42 渲染层取消(玩家中途取消放置) | **sim 侧零事件**(意图未提交);42 只是预览。**无「半放置」状态** —— 放置判定要么成功要么拒绝,没有中间态 | 42(预览)+ 23(判定) |
| **EC-23-13** | 开档基线重放(`BakedInitial` 与事件流的关系) | **`Overlay(0) := BakedInitial`**(规则三);玩家对初始家具的**第一次**改动 = 第一条 `StructureRemoved`。`BakedInitial` 不进流、不存档(评审裁定 B3) | 23 + 关卡工具(ADR-022 导出) |

> **规则六的判据落点**:以上任何一条若在 P0 实现时需要「改 sim 侧代码才能支持 P1a 自由建造」,
> 即为回归(AC-23-BL)。

## Dependencies

### 上游(23 依赖谁)

| # | 系统 | 依赖什么 | 判定归谁 | 双向性 |
|---|------|---------|---------|--------|
| **6** | 世界与生态区 | 槽位骨架(烘焙 `world_buildslots.json`)· 静态导航格 `Nav`(合成输入) | 槽位占用 = **23**(`O-6-10` 已登记) | ✅ **双向** —— 6 的下游表已列本边(`world-and-ecozones.md:781`,标注「❌ 单边 —— 23 无 GDD」⇒ 本 GDD 落盘后闭合) |
| **20** | 库存与物品 | `build_part` 库存裁决(判定 ⑤)· 返还结算 | 物品身份 = **20** | ⚠️ **半** —— 20 无 GDD;本 GDD 已列边,20 成文时须反向引用(同 6 的 `O-6-1` 模式)—— ✅ **2026-09-21 复扫闭合**:原「20 无 GDD」前提陈旧(`inventory-and-items.md` 已成稿 Approved),且**反向引用已落**(`inventory-and-items.md:43` / `:624` / `:779` 三处均列 23)⇒ 本边现为**真双向**,「半」这一档**作废**;本行不删除,留作闭合记录 |
| **21a** | 物品与配方数据库 | 模块目录(数据,归 21a)· `build_part` 定义 | 目录校验 = 21a | ⚠️ **半** —— 21a 的 GDD 已有 `category = build_part`(`item-database.md:135`),但未列 23 为消费者 ⇒ **登记 `O-23-1`** |
| **7a** | 持久化服务 | `structure_id` 高水位(`IIdAuthority`,ADR-010 §五)· 世界流序列化 | 折叠谓词不适用于结构行 | ✅ **双向** —— ADR-010 §三 义务 6 / ADR-009 §三 已列 |
| **ADR-015 / 022 / 014** | 几何 / 工具 / 管线 | 模块化网格裁决 · `world_buildslots.json` 作者 · 烘焙 | — | ✅ 架构层已闭环 |

### 下游(被谁依赖)—— 本 GDD 落盘即闭合的反向边

| # | 系统 | 依赖 23 的什么 | 判定归谁 | 双向性(本 GDD 落盘后) |
|---|------|---------------|---------|------------------------|
| **24** | 医馆即机器 | 布局查询 `structure_at`(邻接判定输入) | 邻接 = **24**;写者 = **23**(规则十) | ✅ **双向** —— `systems-index.md:346` 已记 24 → 23;本 GDD 规则五 / 规则十 落反向边 |
| **27** | 敌人 AI | `EffectiveWalkable`(合成结果) | 寻路 = **27** | ⚠️ **单边(评审 B1 降级)** —— ADR-016 §三 仅感知输入(`ActorCellEntered`),**全文零处 `EffectiveWalkable`**(grep 核实);缝一真实源 = 6 的 GDD(`O-6-10`)。ADR-016 §五 已修订补 `EffectiveWalkable` 口径;27 于其 GDD 开写时登记读方 |
| **13** | 病人 AI | `EffectiveWalkable`(合成结果) | 决策 = **13** | ⚠️ **单边(评审 B1 降级)** —— 13 的 GDD 全篇零处 `EffectiveWalkable`(`patient-ai.md:477` 实为 ADR-015「同一格」行);须在 13 下一轮修订**回填**读方(登记 `O-23-8`) |
| **1** | 玩家控制器与移动 | ~~地貌查询同源 + 建造物走感(可走性同源)~~ **评审裁定 B2:玩家不读合成** —— 走感 = `CharacterController` collide-and-slide(R3/ADR-020);玩家与建筑碰撞 = 42 模板 collider(规则八) | 走感 = **1** | ⚠️ **边改向** —— 玩家走感不经 23 的 `EffectiveWalkable`;模板 collider 足迹同源一致性挂 **ADR-022 C2**(登记 `O-23-9`) |
| **42** | 拟物 UI | 建造交互的呈现 + 放置预览 | 渲染 = **42**(不写占用) | ✅ **双向** —— `skeuomorphic-ui.md:666` 要求本 GDD 成文时现出反向引用(规则八);本 GDD 落边 |
| **2** | 摄像机与视角 | **OQ-2-3 的触发**:建造取景档判据到期 | 判据 = 2(本 GDD 规则十已裁决 P0 不新增档) | ✅ **双向** —— 本 GDD 登记对 2 的义务;`camera-and-viewpoint.md` OQ-2-3 标「21/43 的 GDD 开写时」⇒ 本 GDD 落盘 = 触发,登记 `O-23-2` |
| **45** | 网络层(P1b) | `Structure*` 的可靠通道 | 传输 = 45 | ⚠️ **登记** —— 45 无 GDD;本 GDD 规则四已写明主机唯一 Append,P1b 才生效 |

### 本 GDD 登记的义务

| 编号 | 义务 | 归属 | 触发时点 | 状态 |
|------|------|------|---------|------|
| `O-23-1` | 21a 的 GDD 须补「23 为模块目录消费者」的反向引用 | **21a / `item-database.md`** | 21a 下一轮修订(不阻塞本 GDD 落盘) | ✅ **2026-09-17 已落**(规则四 `build_part` 消费者注) |
| `O-23-2` | 2 的 GDD `camera-and-viewpoint.md` OQ-2-3 依本 GDD 规则十的裁决更新(「建造(21)」笔误 → 23;P0 不新增第四档) | **2 / `camera-and-viewpoint.md`** | 本 GDD 落盘后立即 | ✅ **2026-09-17 已落**(OQ-2-3 已裁闭合,收尾口径同步) |
| `O-23-3` | 6 的 GDD `world-and-ecozones.md:781` 的「❌ 单边 —— 23 无 GDD」标注随本 GDD 落盘闭合为双向 | **6 / `world-and-ecozones.md`** | 本 GDD 落盘后立即 | ✅ **2026-09-17 已落**(:781 行改为双向,O-23-3 ✅) |
| `O-23-4` | **24 的 GDD 须含 P0 最小邻接判定**(评审,game-designer #5)—— P0 切片验证「位置即逻辑」的隐性前置 | **24 / `systems-index.md`** | 24 的 GDD 开写时 | ✅ **2026-09-17 已兑现**(clinic-machine.md F-24-1 公共定义 `adj_sum`,整数 4 邻接 + ADJ_TABLE) |
| `O-23-5` | **21a 冻结 `build_part` 无实例级语义**(评审,game-designer #6)—— `StructurePlaced` 无 provenance;改载荷 = 破 AC-23-09 字节稳定 | **21a / `item-database.md`** | 21a 下一轮修订 | ⏳ **待 21a 下一轮** |
| `O-23-6` | **42 补建造交互规格**(评审,unity-specialist #5)—— 手柄幽灵循环选择 + float→格 floor 量化器 | **42 / `skeuomorphic-ui.md`** | 42 下一轮修订 | ⏳ **待 42 下一轮** |
| `O-23-7` | **2 补 `Explore` 档内软拉近**(评审,unity-specialist #5)—— P0 医馆内越肩被墙遮挡 | **2 / `camera-and-viewpoint.md`** | 2 下一轮修订 | ⏳ **待 2 下一轮** |
| `O-23-8` | **13 的 GDD 回填 `EffectiveWalkable` 读方**(评审 B1) | **13 / `patient-ai.md`** | 13 下一轮修订 | ⏳ **待 13 下一轮** |
| `O-23-9` | **ADR-022 C2 挂模板 collider 足迹同源钩**(评审,unity-specialist #1)—— collider 高度 > `stepOffset` 除非可走过 | **ADR-022** | 本轮修订落盘 | ✅ **2026-09-17 已落**(§四 C2 补注) |

## Tuning Knobs

> 全部 `*待定*` —— **数值用户自己调**。每个旋钮标注安全范围 + 影响面 + 归谁。

| 旋钮 | 域 | 安全范围 | 影响面 | 归谁 |
|------|-----|---------|--------|------|
| `DEMOLISH_REFUND_RATIO` | Q16.16 | `0 < R ≤ 1`;**P0 恒 = 1**(评审裁定,规则九 —— 重排净零材料损耗,代价走拟物轴);**P1a 恢复 `< 1`**(材料经济) | 「布局是判断还是撤销」的机械密度;材料经济(20,P1a) | **用户** |
| `BUILD_TIME`(放置耗时) | 表现层 | `0`(瞬发)~ 数秒 | 建造手感 + **P0 重排代价的拟物轴**(评审裁定,规则九):耗时 = 仪式感 + 停诊窗口 | **用户** + 42(呈现) |
| 模块目录的 `cost(m)` | int | ≥ 1;可返还模块须 `cost ≥ ⌈1/R⌉`(EC-23-08;**P0 `R = 1` 时自动满足**) | 每种家具 / 设施的材料成本;返还边界 | **用户** + 21a |
| 模块目录的朝向集合 | enum | P0 ⊆ `{0°}`;判定代码须支持四向(AC-23-06) | P1a 自由建造的扩展宽度 | **用户** + 21a |
| `Overlay` 重算范围 | 局部(占用变更格) | 仅变更格及其邻接(ADR-015 §四③ 脏块) | 重算成本 = 表现层预算 | **23**(结构性,非旋钮) |
| 槽位骨架的 `SlotType` 种类 | enum(床 / 台 / 柜 / 圃…) | 由关卡工具烘焙;P0 只烘焙一间医馆 | 医馆可重排的家具种类;24 的邻接玩法宽度 | **用户** + 关卡工具(ADR-022) |
| `structure_id` 高水位初始值 | u64 | ≥ 1 | 不与其他 id 空间冲突(共用 `IIdAuthority`) | **7a**(结构性) |
| 放置判定重跑间隔 | — | 判定在意图事件当下执行,无间隔 | 联机 P1b 的冲突裁决粒度 | **23**(结构性) |

## Acceptance Criteria

### 判定与合成(EditMode 纯逻辑,[L] = 逻辑)

| # | 级别 | 判据 | 载体 |
|---|------|------|------|
| **AC-23-01** | [L] BLOCKING | **GIVEN** 槽位骨架 + 模块目录,**WHEN** 执行 F-23-2 **五条件**,`THEN` ①锚点 ∈ 允许格集 ∧ 类型匹配 ②占用全空 ③**占用格全在 `BuildSlotRegion` 内** ④骨架未改 ⑤目录 + 库存可及 **∧ 占用格无实体** —— 任一不满足 `Placeable = false`,且**不 Append 任何事件**。⚠️ 条件④在 P0 恒真(无改骨架 API),矩阵以 ①/②/③/⑤ 为主 | `placeable_cases.json`(含负偏移越界用例) |
| **AC-23-02** | [L] BLOCKING | **GIVEN** `StructurePlaced` 序列,**WHEN** 重放,`THEN` 重建的 `slot_occupied` / `structure_at` / **`Overlay`**(评审,ai-programmer #4 扩展)与事件序列**逐位一致**(占用真相 = `BakedInitial ⊕ 事件`纯函数,规则三)。**期望终态表独立钉死**(评审,qa-lead #8 —— 不得由同一重建代码算出,防循环自证) | 重放对拍 + 独立夹具 |
| **AC-23-03** | [L] BLOCKING | **GIVEN** 静态 `Nav` + 23 的 `Overlay`(缝一),**WHEN** 合成 `EffectiveWalkable`,`THEN` = `Nav.walkable ∧ ¬Overlay.blocked`,且 27 / 13 的读接口**只暴露合成结果**(无 `Nav` 原件泄漏);**玩家不读**(评审裁定 B2) | `effective_walkable.json` + 接口断言 |
| **AC-23-04** | [L] BLOCKING | **GIVEN** 同一占用表,**WHEN** 以 `slot_occupied` 与 `Overlay.blocked` 两个访问器读写,`THEN` 两者恒一致。**载体 = 反射断言 `Overlay` 类型无任何自有存储字段**(无 `bool[]` / `Dictionary` 成员;`blocked` 必须是 `slot_occupied` 的纯函数)+ **Place+Remove 混合序列的逐步不变量检查**(评审,qa-lead #1)—— 不允许两套副本漂移 | 反射断言 + 混合序列单测 |
| **AC-23-05** | [L] BLOCKING | **GIVEN** 四向朝向的模块,**WHEN** 应用 F-23-2b 整数旋转,`THEN` 占用格集正确且**零浮点中间量**。**载体升级(评审,qa-lead #9)**:类型级反射断言(字段 / 参数类型 ∈ 整数集,与 `PresentationDtoGuard` AC-37-15 同构);grep 仅辅助 | `rotation_cases.json` + 反射断言 |
| **AC-23-06** | [L] BLOCKING | **GIVEN** 判定代码,`THEN` 支持四向朝向(**即使 P0 目录 ⊆ {0°}**)—— P1a 加旋转**不得改 sim 代码**(规则六判据)。**载体(评审,qa-lead #2)**:`rotation_cases.json` 含**合成目录四向条目**,驱动 Placeable / 重放走 orientation ∈ {90,180,270} 的**可执行用例**;改朝向走 `Modifiable`(规则十) | `rotation_cases.json` 可执行用例 |
| **AC-23-07** | [L] BLOCKING | **GIVEN** `StructureRemoved`(EC-23-02 / EC-23-03 情形),`THEN` 不存在 `structure_id` / **格上有实体(玩家 / 敌人)**时**拒绝且不 Append**;实体判定 = 量化格 + 宽容半径,**零 Physics 调用**(评审 B4 / unity-specialist #2) | `remove_cases.json` + 反射断言 |
| **AC-23-08** | [L] BLOCKING | **GIVEN** 模块目录,`WHEN` 校验,`THEN` 可返还模块满足 `cost ≥ ⌈1/DEMOLISH_REFUND_RATIO⌉`(EC-23-08),否则**构建期拒绝**;**`(0,0) ∈ OccupiedCells_local` ∧ 占用格 ⊆ `BuildSlotRegion`**(评审 B5)。P0 `R = 1` 时本校验自动通过 | 目录校验 |

### 世界流与呈现

| # | 级别 | 判据 | 载体 |
|---|------|------|------|
| **AC-23-09** | [L] BLOCKING | **GIVEN** 任意放置 / 拆除 / 改造序列,**WHEN** 序列化 → 重放,`THEN` 字节流逐位稳定(世界流形状与 7a 契约一致,ADR-010 §一)。**往返定义 = encode → decode → re-encode 字节相等**(评审,qa-lead #3)。**刷新政策显式绑定 ADR-012 §三**(评审,qa-lead #3):golden-vN + 变更日志 + 全平台同签 + 旧版保留回归,禁单平台独签 | 黄金夹具(golden-vN) |
| **AC-23-10** | [L] BLOCKING | **GIVEN** 载荷定稿(规则四),`THEN` 字段 = `structure_id / cell / module_id / orientation / variant`(Removed 无 orientation/variant;Modified 含 `modified_fields`),**全整数、无 float**。**`modified_fields` 白名单 = 朝向 / 变体(无换模块位,评审裁定)** | 载荷单测 |
| **AC-23-11** | [L] BLOCKING | **GIVEN** `StructureModified`,`THEN` `modified_fields` 只载物理形态位(朝向 / 变体),**不载任何数值结果**(规则十:数值 = 24 的派生);改朝向须过 `Modifiable` 占用检查(规则十) | `modify_cases.json` |
| **AC-23-12** | [L] BLOCKING | **GIVEN** 23 的实现程序集,`THEN` **无任何呈现 API 引用**(无 Mesh / Transform / GameObject —— 与 ADR-013 §9 C3 同构;grep + 程序集断言) | 结构断言 |
| **AC-23-13** | [L] BLOCKING | **GIVEN** 42 的渲染层,`THEN` 42 不是 `Structure*` 的写者。**载体(评审,qa-lead #4)**:① **写者守卫** —— Append 对 42 程序集 `internal` 不可见 / 引用集白名单(违规 = 构建失败);② **零事件半** —— 放置预览取消时**零 sim 事件**(EC-23-12),spy-sink PlayMode 集成测试(载体 `preview_cancel_cases` 现补) | 引用集白名单 + spy-sink 集成测试 |

### 边界与范围门

| # | 级别 | 判据 | 载体 |
|---|------|------|------|
| **AC-23-14** | [L] BLOCKING | **GIVEN** P0 医馆,`THEN` 玩家不可动外壳(墙 / 地基 / 房顶)。**载体(评审,qa-lead #5)**:外壳识别经**槽位骨架的 SlotType 标记**(非名字清单);`remove_cases.json` 增「对 shell 格发拆除意图 ⇒ 拒绝且不 Append」用例 | SlotType 标记 + `remove_cases.json` 用例 |
| **AC-23-15** | [L] BLOCKING | **GIVEN** P1a 自由建造设想,`THEN` 只需烘焙更大骨架 + 更宽目录,**sim 代码零改动**(`Structure*` 载荷不变,规则六判据)。**载体(评审,qa-lead #6)**:测试期注入「P1a 形状」夹具(更大骨架 + 含旋转的更宽目录),放置此前未见过的格**必须成功**,全程不改代码 | 注入夹具测试 |
| **AC-23-16** | [L] BLOCKING | **GIVEN** 拆除返还,`THEN` `Refund = ⌊cost × R⌋` 整数向下取整(`(cost × rawR) >> 16`,评审 B6),失败走 `DropSpawned`(EC-23-11),**不静默吞**。**载体(评审,qa-lead #7)**:`refund_cases.json` 含「20 库存满」用例 + spy-sink 断言 `DropSpawned` 存在;`DropSpawned` 发出者 = 20 结算(23 触发),归 20 侧测试文件 | `refund_cases.json` + spy-sink |
| **AC-23-17** | [L] ADVISORY | **GIVEN** 槽位骨架改版(EC-23-05),`THEN` 已放置结构跨版本保留,不迁移位置(迁移 = 改写玩家资产,不做) | 迁移链单测 |
| **AC-23-18** | [L] ADVISORY | **GIVEN** 联机 P1b 设想,`THEN` 冲突裁决语义(主机唯一,EC-23-09)已登记且**不改 P0 判定形状** | 语义登记核对 |

> **BLOCKING 汇总**:AC-23-01…16 为 BLOCKING(17 / 18 ADVISORY)。**门面** = 放置判定 / 合成 /
> 载荷 / 呈现边界 / P0 范围门 —— 全案由「结构身份 vs 物品身份不合并」这条承重纪律兜底(规则七)。
> **AC 载体总纲(评审,qa-lead 共性根因)**:凡「未来不改代码」「不静默」「不会漂移」类否定式断言,
> 一律落**可见性 / 引用白名单(编译期)**或 **spy-sink(运行期)**两类硬守卫 —— 违者构建失败 / 测试红灯,
> 不以 grep / 人工比对为最终判据。

