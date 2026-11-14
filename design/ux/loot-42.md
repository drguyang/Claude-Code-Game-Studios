# 拾取面板(42 · `ModalId.Loot`)

| 字段 | 值 |
|---|---|
| **Status** | **Draft**(2026-11-14 与代码同批落盘,**未经 `/ux-review`**) |
| **Author** | 装配轮(承用户 2026-11-14 口述形态) |
| **Platform Target** | PC(Keyboard/Mouse + Gamepad);VR 不在本屏面内 |
| **Accessibility Tier** | Standard(`design/accessibility-requirements.md`) |
| **Governing ADR** | `ADR-013 §十-H Amendment G`(闭集第十一员)· `ADR-009 §七`(拾取三段式)· `ADR-013 §9 C3`(42 只渲染) |
| **Code** | `unity/Assets/Gameplay.UI/Skeuomorphic/Screens/LootScreen.cs` · `unity/Assets/Gameplay.Boot/DriverLootActionSink.cs` |

> ⚠️ **本件是本轮新写的 spec,未经 `/ux-review`** ⇒ `AC-42-F1` 的 ⑪ 员记 **`NOT-RUN`**,
> 按该 AC 的纪律**阻塞签核**。成文 ≠ 评审 ≠ 走查。

---

## 1. Purpose & Player Need

**玩家视角的需求**:杀死 / 采集之后地上躺着几件东西,玩家需要**一眼看清有哪些、再决定拿哪件**,
而不是盲按 E 一件一件试。

**为什么不是「一按即拿一件」**(改前形态):地上三件就要按三次;更要紧的是**看不见地上有什么** ——
拾取退化成「按一下,看背包多了什么」。

## 2. Player Context on Arrival

玩家刚做完一件让它掉东西的事(采集完成 / 击败敌人 / 拆除结构),人还站在落点旁边。
**此刻的心理状态**:想知道「掉了什么」,并且很可能想**全拿走**。

## 3. Navigation Position

世界 → (按 E 选中一件掉落) → **本屏**(`ModalId.Loot`)。它**不是**从任何其他模态进入的,
故在模态栈里是**文档类**(离开即离开,不压栈)—— 与 §十-E 的同一判据。

## 4. Entry & Exit Points

| 方向 | 入口 / 出口 | 条件 |
|---|---|---|
| **进入** | 玩家按 E,4 的选择器选中 `InteractableKind.Drop` | 拾取半径内至少有一件地面实例 |
| **不进入** | 地上没有可拾行 | **不开空屏**(与「空箱不开空页」同纪律) |
| **退出(自动)** | 拿完之后列表为空 | 合屏(零行的拾取屏 = 死界面) |
| **退出(手动)** | Escape / 暂停键 | 走 `SkeuoRuntimeDriver.CloseModal`(既有契约) |

## 5. Layout Specification

```
┌─ 拾取 ─────────────────────────┐   ← 页级 `.paper-page`(带页框)
│ 门板 ×1                        │   ← 行(`.ruled`,可获焦 / 可点)
│ 止血草 ×3                      │
│ 干柳树皮 ×2                    │
│                                │
│ 全部拾取                       │   ← 末行(可获焦;空集时不可获焦)
└────────────────────────────────┘
```

| 元件 | 内容 | 来源 |
|---|---|---|
| 标题 | 「拾取」 | 42 常量 |
| 行 | `显示名`(数量 > 1 时 `显示名 ×N`) | **装配层传入**(屏不查物品表) |
| 空态 | 「地上没有可拾的物品」 | 42 常量 |
| 末行 | 「全部拾取」 | 42 常量 |

**行序**:由装配层给(`InventoryProjection.InstancesOf` 的 `instance_id` 升序),**本屏不再定序**。
两处定序 = 第二真源。

## 6. States & Variants

| 状态 | 触发 | 表现 |
|---|---|---|
| **有物** | 行数 ≥ 1 | 行 + 末行;焦点落第一行(或按 E 选中的那一件) |
| **空** | 行数 = 0 | 一行空态注;末行不可获焦、不可点 |
| **收缩** | 玩家拿走一件 | 该行消失,其余行保序;焦点原位回焦(按实例 id 找回) |
| **合屏** | 拿完 | 模态关闭(由装配层出口执行) |

⚠️ **没有加载态 / 错误态**:数据是内存投影的纯读,无异步、无失败面。
若装配层拿不到投影(未装配)⇒ 按 E **不开屏**(与「没有掉落」同形,诚实降级)。

## 7. Interaction Map

| 输入 | 行为 |
|---|---|
| 方向键 / 左摇杆 | 焦点在行之间移动(官方焦点桥,不自实现算法) |
| Submit(A / Enter) | 对**当前获焦行** = 拿那一件;对末行 = 全部拾取 |
| 指针左键 | 点行 = 拿那一件;点末行 = 全部拾取 |
| Escape / 暂停键 | 合屏 |

**焦点序**:第一行 → … → 末行 → (环绕,由焦点门与 `tabIndex` 决定)。

## 8. Data Requirements

| 数据 | 来源系统 | 所有者 | 更新时机 |
|---|---|---|---|
| 地面实例 id + 物品序号 + 数量 | 20(`InventoryProjection` 读世界流) | **20** | 每次开屏 / 每次拿取后重取 |
| 物品显示名 | 21(物品种子表 `DisplayName`) | **21** | 装配层惰性查表一次 |
| 拾取半径 | 20(`DropClaimOrchestrator.Tolerance`) | **20** | 构造期常量 |

⚠️ **42 不持任何一条**:行集是**开屏时喂入的呈现缓存**,投影一变由装配层重喂。

## 9. Events Fired

| 玩家动作 | 报给谁 | 是否进流 |
|---|---|---|
| 点某一行 | `ILootActionSink.Claim(instanceId)` | **不直接进流**;装配层经 20 的编排面写 `DropClaimed` |
| 全部拾取 | `ILootActionSink.ClaimAll()` | 同上(逐件写 `DropClaimed`) |
| 合屏 | `SkeuoRuntimeDriver`(模态关合) | **不进流**(模态开合永不落为流事件) |

## 10. Transitions & Animations

- 开屏 / 合屏:沿用其余模态的既有过渡(`SkeuoRuntimeDriver`)。
- 行消失:无动画(拟物纸面不做逐行淡出 —— 与 §10 其它屏一致)。
- **不新增**任何镜头效果(ADR-020 §六:镜头效果不得用于报状态)。

## 11. Accessibility Requirements

- 焦点可见:`FocusVisibleStyle`(既有)。
- 行高与字号满足 Standard 档下限;不做颜色单通道编码。
- 屏上不呈现数字角标 / 进度填充(AC-42-F3 墨侧禁令)。

## 12. Localization Considerations

- 行文字由装配层给(物品显示名 + `×N`),本地化随 21 / 50。
- 屏自身常量:「拾取」/「全部拾取」/「地上没有可拾的物品」—— 须进字符串表(P2)。

## 13. Acceptance Criteria

| # | 判据 | 载体 |
|---|---|---|
| **AC-L1** | 喂 n 行 ⇒ 屏上恰 n 行,行文字**原样**(屏不拼文案) | `loot_screen_test.test_present_buildsOneRowPerItem_verbatimLabels` |
| **AC-L2** | 空集 ⇒ 空态一行,「全部拾取」不可获焦,激活零意图 | `loot_screen_test.test_present_empty_showsEmptyState_andDisablesTakeAll` |
| **AC-L3** | 点第 i 行 ⇒ 只报**那一件**的实例 id | `loot_screen_test.test_activateRow_reportsThatInstanceOnly` |
| **AC-L4** | 「全部拾取」⇒ 报一次 `ClaimAll` | `loot_screen_test.test_activateTakeAll_reportsOnce` |
| **AC-L5** | 逐件 ⇒ 恰一条 `DropClaimed`;批量 ⇒ 每件一条 | `driver_loot_action_sink_test` 3 条 |
| **AC-L6** | 拿完 ⇒ 合屏 | `driver_loot_action_sink_test.test_claimAll_claimsEveryGroundRow_thenCloses` |
| **AC-L7** | 列表与结算**同一道半径门**(列出来的一定点得动) | ⚠️ **NOT-RUN** —— 需要「半径内有 / 外各一件」的装配夹具 |
| **AC-L8** | 手柄走查:开屏 → 逐行 → 批量 → 合屏,目视无按键提示浮层 | ⚠️ **NOT-RUN**(无真包走查件) |
| **AC-L9** | UI 事件绑定本身(`NavigationSubmitEvent` / `ClickEvent`)真的触发 | ⚠️ **NOT-RUN**(EditMode 无 `PanelSettings`,`SendEvent` 不派发) |

## 14. Open Questions

| # | 问题 | 现状 |
|---|---|---|
| **OQ-L1** | 行序是否应改成「按物品类别聚类」? | 现口径 = `instance_id` 升序(与投影同序)。**未裁** |
| **OQ-L2** | 拾取后是否播一行「已放入」反馈? | **不做**(无提示音铁律的同源;且会变成 toast) |
| **OQ-L3** | 背包满时怎么表现? | **未裁** —— 今日 `TryClaimInstance` 不查载重,满包仍可拿;归 20 的容量轮 |
