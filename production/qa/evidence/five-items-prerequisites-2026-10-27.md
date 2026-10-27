# 五项修复的前置内容清单

**日期**:2026-10-27
**用户指令**:「这五项现在修,给出需要补的前置内容」
**性质**:本件是**开工前的物料清单**,不是修复记录。每项写明 **要补什么 / 谁签 / 代价 / 顺序**。

> ⚠️ **没有一项能在物料不齐时开工**。强行开工的后果不是「做得糙」,是**制造假绿**
> —— 本项目已抓过五次的那类。

---

## 总览:前置依赖图

```
┌─────────────────────────────────────────────────────────────┐
│ 0. 口径裁定(用户,零代码,阻塞 #1 #3)                      │
│    ① R16: P0 单机 ⇒ 建造扣料归唯一玩家                     │
│    ② R18: P0 掉落品级从哪来(恒1 / 源实例 / 新位)          │
└──────────────────┬──────────────────────────────────────────┘
                   │
       ┌───────────┴───────────┐
       ▼                       ▼
┌──────────────┐      ┌────────────────────┐
│ 1. ADR-009   │      │ 3. Story 010       │
│   Amendment  │      │   容器判据载体      │
│   载荷位扩列  │      │   (is_container)   │
└──────┬───────┘      └────────┬───────────┘
       │                       │
       ▼                       ▼
┌──────────────┐      ┌────────────────────┐
│ 2. 实现轮     │      │ 4. FoldWeight 接线 │
│  23/25/27/29 │      └────────────────────┘
└──────────────┘
```

---

## #4 · 构建期 `RunAll` —— ✅ **已完工,无前置**

探针轮 + 干净轮双绿,story-003 已知边界 6 已结案。**本项从清单移除。**

---

## #1 · `IsComplete` 恒 false —— 前置 = **一次口径裁定 + 一次 ADR Amendment**

### 前置 A:用户口径裁定(零代码,必须先做)

这两条**不裁就无法动工**,因为它们是「往载荷里加不加字段」的分叉点:

| 编号 | 要裁的 | 三个选项 | 影响 |
|---|---|---|---|
| **R16** | 建造扣料扣谁的库存 | 甲:裁「P0 单机 ⇒ 恒归唯一玩家」(**不载荷**) | 最省;但联机前须回看 |
| | | 乙:`StructurePlacedPayload` 补 `actor_id` | 破 AC-23-09 字节稳定 ⇒ 23 须 amend |
| **R18** | 非 17 来源的 `quality` 从哪来 | 甲:裁「P0 掉落品级恒 1」(**不载荷**) | 最省;`InstanceWithoutQualitySource` 可解除 |
| | | 乙:`DropSpawnedPayload` 补 `quality: i32` | 4 处构造点 + 11 处 `new` 全改 |
| | | 丙:各源走各自的 `*Applied` 事件侧载 | 复杂,但语义最准 |

> 💡 **我的建议**:R16→甲、R18→甲(P0 范围最小)。理由:两者都是「P0 单机退化」的
> 自然结果,且 GDD 已承认单机是 P0 主形态(`game-concept.md:26`)。但**必须显式裁**,
> 因为「退化是推断不是裁定」正是 R16 登记时的原话。

### 前置 B:ADR-009 Amendment(裁完才写)

若走**乙/丙**案(加字段),须:

1. **ADR-009 开 Amendment**,钉死新字段的类型与语义
2. **`design/registry/entities.yaml` 改 4 处 `payload_schema`**:
   - `StructurePlaced`(现无 actor 位)
   - `StructureRemoved`(现无 refund 位)
   - `DropSpawned`(现无 quality 位)
   - `Craft`(现无产出 `item_key` 位,见 R15)
3. **`WorldPayloads.cs` 改 4 个 struct + 构造函数**(`Assets/Sim.Contracts/Payloads/`)
4. **改 41 处构造调用点**(实测:`StructurePlaced` 6 · `StructureRemoved` 5 · `DropSpawned` 11 · `Craft` 19)
5. **跑 AC-23-09 字节稳定性测试**(`structure_kinds_test.cs:4` 已有,须全绿)

**复算**:
```bash
cd unity
for k in StructurePlacedPayload StructureRemovedPayload DropSpawnedPayload CraftPayload; do
  echo -n "$k : "; grep -rn "new $k(" Assets --include=*.cs | wc -l
done
# 实得:6 / 5 / 11 / 19 ⇒ 合计 41 处
```

### 前置 C:归因口径在 fold 侧的实现

裁完 R16 甲案后,`InventoryFold.cs:412-419` 的**无条件 `gaps.Add`** 要改成
「有 actor 位则按位归因;无位且已裁单机则归唯一玩家」:

```csharp
case EventKind.StructurePlaced:
    // 现在:无条件报 R16 缺口
    // 改后:按裁定口径 —— 甲案则计入唯一玩家,不报缺口
```

**代价**:小(单 case 改写 + 2 条夹具)。

### ⚠️ 关键提醒

**即使 R16 + R18 全裁甲案,R17 仍在**(`StructureRemoved` 返还物既无 `instance_id` 也无 `item_key`,
**无论怎么裁都缺位**,除非改载荷)⇒ **`IsComplete` 仍不会变 true**。

⇒ **#1 的完整修复,必须包含 R17 的乙案(载荷补 `refund_item_key` + `refund_instance_id`)**,
否则做完也只从「3 类缺口」降到「1 类缺口」。

---

## #2 · 「玩家看得到库存」—— 前置 = **R9 回写 + 一次 Engine spike**

### 前置 A:R9 回写(42 侧)—— ✅ **2026-10-27 已完成**

> 🔁 **本节的初版把代价估错了,如实订正**(原文保留在下方删除线内)。
> 逐个复算后发现:**R9 是五子项,不是三件;其中 ③④⑤ 的设计早已裁完**,
> 落在 `design/ux/inventory-container-20.md`(**Approved 2026-09-22**)里 —— 只是从没汇总回 42 主表。

| 子项 | 初版说法 | **实测** |
|---|---|---|
| ① 「20 无 GDD」回刷 | 要做 | ✅ **2026-09-20 就做完了**(42 全文 `grep "20 无 GDD"` **零命中**) |
| ② 载重口径接受 | 要做 | ❌ **真缺口** → ✅ 本轮已改 `skeuomorphic-ui.md:1260` |
| ③ `COLUMNS`/`ROWS_PER_PAGE` | 须 42 轮**设计** | ✅ **已裁 `4`/`3`**(`inventory-container-20.md:98`) |
| ④ 四档单调(初版漏列) | — | ✅ **已裁**(同件 `:142-163`) |
| ⑤ 品级非颜色通道(初版漏列) | — | ✅ **已裁**(同件 `:118-119`) |

**⇒ 本轮只做「登记回写」,没做任何设计裁定。**
已改:R9 行标 ✅ 已回写 · `AC-20-20` 的 `BLOCKED-BY-R9` 全解 · story-003 / story-006 六处标注解除 ·
42 `:1260` 与 `:1059` 就地加注 · 评审日志 BL-27 加注。
⚠️ **`AC-20-20` 仍未勾 = 走查没跑(`NOT-RUN`),不是缺前置。**

~~**①②是纯文档改写,现在就能做。③是设计决策,须 42 轮。**~~

### 前置 B:Engine spike(ADR-013 §6.6 假设 6)—— 🟡 **2026-10-27 自动化腿已跑**

story-006 明文写「原型 spike 为前置」:

> **假设 6 = ⚠️ 半可信** —— UI Toolkit 运行时手柄焦点导航(`NavigationMoveEvent` / `FocusController`)
> 在 Unity 6.3 是否真能按几何位置移动焦点,**未经实测**。

**这是 post-cutoff API 风险**(ADR-013 自己的 Knowledge Risk = HIGH)。
若不 spike 就直接写 story-006,**可能在写完 6h 后发现焦点根本不工作**。

**spike 内容**(约 2h):最小 UXML 面板 + 4 个可聚焦元素 + 手柄方向键 ⇒ 记录
实际焦点移动轨迹,确认按几何位置。

**实测结果(2026-10-27)**:已补**自动化腿**,原件 `production/qa/evidence/spike-assump6-2026-10-27.md`。
- **判据③ ✅ 9/9**(无指针全可达)· **判据② 引擎侧前提 ✅**(同输入两环落点不同 ⇒ 几何序生效,非树序退化)
- **判据① 与判据② 方向语义 = `NOT-RUN`** —— IL 实证 `FocusController.SwitchFocusOnEvent` /
  `FocusNextInDirection` / `EventDispatcher.Dispatch` 全是 `assembly`(internal),
  **进入引擎默认动作阶段的公开入口不存在**
- ⇒ **假设 6 仍不得标「已实测通过」**,人工手柄腿仍欠;但**不是白做** ——
  引擎侧能力(:按几何排序在册且生效)已被证实,ADR-013 的回退路径**无需触发**。

### 前置 C:story-006 拆分(建议)

现 story-006 是 6h 一个包(摆放 + 翻页 + 四档 + 走查)。**建议拆两半**:

- **006a**(不依赖布局):药箱面板骨架 + 四档呈现函数 —— 现在可做
- **006b**(依赖 R9 ③):落位 + 翻页 + 手柄遍历走查

---

## #3 · 五函数零调用点 —— 前置分两类

### 类型 A:`StacksOf` / `Ratio` / `MinWeight` / `IsFullForAnyAddition`

**这些不是缺陷,是消费者未写。** 它们的前置 = **#2 完成**(story-006 会消费 `StacksOf` / `Ratio`)
**+ 42 的 R9**(会消费 `MinWeight` / `IsFullForAnyAddition` 做 AC-20-18 的分档)。

⇒ **无独立前置,跟随 #2 走。**

### 类型 B:`FoldWeight` —— 前置 = **Story 010(容器判据载体)**

这是**唯一有硬前置的**。三件必须先补:

| # | 要补什么 | 现在有没有 | 复算 |
|---|---|---|---|
| 1 | 「这是容器吗」的**判据载体** | ❌ 无 | `grep -rn "is_container\|IsContainer" unity/Assets --include=*.cs` ⇒ 生产侧零命中 |
| 2 | `ItemCategory` 有 `container` 值 | ❌ 无 | 读 `Sim.Contracts` 的 `ItemCategory` 枚举 |
| 3 | 生产路径能构造 `childrenByContainer` | ❌ 无 | `CarryLoad.cs:212` 写死传 `null` |

**为什么这是硬前置**:`FoldWeight(空箱自重, 子件集)` 的入参需要调用方**知道哪些实例是容器、
容器里有哪些子件**。今天这两个信息**在任何地方都不存在** —— 接了也是死路。

⇒ **归 Story 010**,不是 20 的待办。**前置 = Story 010 落地**。

---

## #5 · `AC-18-15` 不勾 —— 前置 = **processing 黄金夹具 + ADR-012 第三格**

### 前置 A:processing 侧建黄金夹具(判据面)

AC-18-15 要的是「三出参逐位相同」,而现有矩阵测的是 item-database 的 19 条哈希。

**要补的**:
1. 给 processing 三出参(`ActualConsumed` / `OutputQuality` / `ActualProduced`)**设计黄金向量**
   —— 参照 `determinism_golden_fixtures_test.cs` 的形态
2. 生成 `golden-processing-v1.txt`(独立金标准,**不由被测代码生成** —— 防自指)
3. 把 processing 夹具加进 `run_matrix.py` 的过滤器

**代价**:中等(须先定「哪些 (recipe, L, 输入集) 组合构成黄金集」)。

**复算**:
```bash
cd unity && grep -rln "golden\|Golden" Assets/Tests/EditMode/Processing/
# 实得:零命中 —— 这就是判据面缺口的直接证据
```

### 前置 B:第三格 Linux-ARM64(环境面)

ADR-012 三格 = Mono / IL2CPP-x64 / **IL2CPP-ARM64(交叉构建)**。

**要补的**:
1. **ARM64 构建目标可用性** —— 本机 `BuildTargetProbeCli` 探针显示无 ARM64 目标
2. **CI runner** —— ADR-012 §二要求「全免费常驻矩阵」,本地跑不算
3. **版本化刷新协议** —— `golden-vN` + 变更日志 + 三平台同签

⇒ **这三样是「CI 基础设施」,不是一次代码改动。**

### 前置 C:两条理由必须**都**满足

按 ADR-012 原文,`AC-18-15` 的判据 = **三格齐 + 常驻 + 版本化刷新协议**。
现状 = 两格 + 非常驻 + 无协议。**补了 processing 夹具(前置 A)也只解一半。**

---

## 开工顺序建议

| 顺序 | 项 | 前置 | 可并行? |
|---|---|---|---|
| **1** | **用户裁 R16 / R18 口径** | 无 | — |
| **2** | **#4**(已完成) | — | — |
| **3** | **#2 前置 A①②**(42 的 R9 文档回刷) | 无 | ✅ 可与 1 并行 |
| **4** | **#2 前置 B**(Engine spike) | 无 | ✅ 可与 1/3 并行 |
| **5** | **#1 前置 B/C**(ADR Amendment + fold 改写) | 1 完成 | 依赖 1 |
| **6** | **#2 story-006a**(面板骨架) | 3 + 4 | 依赖 3/4 |
| **7** | **#3 类型 A** | 6 | 跟随 6 |
| **8** | **#1 R17 乙案**(载荷补 refund 位) | 5 | 依赖 5 |
| **9** | **#3 类型 B** | **Story 010** | 独立轨道 |
| **10** | **#5** | processing 夹具 + CI 基础设施 | 独立轨道 |

---

## 一句话总结

| 项 | 前置性质 | 能不能现在开工 |
|---|---|---|
| #4 | — | ✅ **已完工** |
| #1 | **用户口径裁定** + ADR Amendment + 41 处构造点 | ❌ 卡在裁定 |
| #2 | R9 文档回刷 + **Engine spike** | ⚠️ 部分可开工 |
| #3 | 类型 A 跟随 #2;类型 B 卡 **Story 010** | ❌ |
| #5 | processing 黄金夹具 + **CI 基础设施** | ❌ |

**五项里,唯一「完全无前置」的是已完工的 #4。其余四项各有一到两道硬门,其中三道是用户裁定或跨系统轮次,不是工作量问题。**
