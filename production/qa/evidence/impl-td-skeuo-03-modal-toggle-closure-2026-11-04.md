# TD-skeuo-03 实现轮 —— 模态关闭面收敛(`CurrentModal`)+ 壳类栈 + 焦点回进入前载体

**截至时刻**: 2026-11-04 06:35 CST(2026-11-03 22:35 UTC)
**批次**: sprint-24 · 用户裁定「甲:关闭 + 焦点回进入前载体」
**commit**: `e567e5be` + 工作树(本轮提交前)

---

## 1. 病因

`ModalOpener` 契约是**两个逐员布尔**(`IsPauseOpen` / `IsCasebookOpen`),
`ModalInputDriver.OnFrame` 据此三分支:脉案开 ⇒ 关 · 暂停壳开 ⇒ 关 · **其余一律 ⇒ `OpenPause()`**。
⇒ 闭集 9 员里只有 2 员能被同一个 toggle 关掉;另外 7 员(存档位 / 库存容器 / 设置壳 / 教学 /
医馆面板 / 纸近景 / 炮制屏)被**换屏到暂停壳** —— 玩家以为关掉了,实际换了个屏,再按一次又回世界。

三份 Approved spec 的出口都写「同一 action toggle 关闭」:
`inventory-container-20.md:56` · `clinic-panel-24.md:81` · `casebook-39.md:225`。

## 2. 三项裁决(用户 2026-11-04「甲」)

| # | 裁决 |
|---|---|
| ① | toggle 语义 = **关闭 + 焦点回进入前载体**(P-02) |
| ② | 判据收敛成**一个可比较的量** `ModalOpener.CurrentModal : ModalId`;**不逐员分支** |
| ③ | 栈:模态套模态时关回上一层;栈空才回世界并还原焦点 |

## 3. 落点

| 文件 | 改动 |
|---|---|
| `Gameplay.Boot/ModalInputDriver.cs` | `ModalOpener` 两布尔 → `CurrentModal`;`OnFrame` 判据收成「有模态 ⇒ 关,无模态 ⇒ 开暂停壳」 |
| `Gameplay.UI/Skeuomorphic/SkeuoRuntimeDriver.cs` | 增 `_modalStack` / `_entryCarrier`;`OpenModal` 记栈与载体、`CloseModal` 弹栈或还原焦点;`OpenModal` 拆出 `OpenModalCore`(不碰栈,防关-弹无限压栈);新增 `IsShellModal` |
| `Tests/EditMode/Boot/modal_opener_test.cs` | `FakeOpener` 改实现 `CurrentModal` |
| `Tests/EditMode/Boot/modal_toggle_closure_test.cs` | **新增 6 条** |
| `Tests/PlayMode/modal_focus_wiring_test.cs` | **新增 1 条**(P-02,读引擎真源 `panel.focusController.focusedElement`) |
| `docs/architecture/adr-013-…md` | 新增 **§十-F Amendment E** |
| `design/ux/save-slots-7b.md` | 「关闭册」行加实现回写 |

## 4. ⚠️ 一处**真回归**,由 PlayMode 全量抓出(通用栈 → 壳类栈)

首版实现是**通用栈**:任何模态换屏都压栈。`AC-39-07` 的
`test_s39_07_edge_noDuplicateNoSpuriousExplore` 当场转红:

```
从非脉案模态关屏**不得**产生意图   Expected: 4   But was: 5
```

**真因**:`Casebook → Inventory` 被压了栈 ⇒ 关库存时**回到脉案** ⇒ 多一条 camera 意图。
而 `AC-39-07`(Approved)要求「从非脉案模态关屏**零意图**」,`casebook-39.md:253`
逐字「档位回归 Explore」—— **离开脉案是单向的**,不是可返回的一层。

**修法(收窄,不是改判)**:压栈判据改为 `IsShellModal` ——
**壳类**(暂停壳 / 设置壳)会开出下一层,故有「上一层」可回;
**文档类**(册 / 箱 / 面板 / 近景)入口在世界,离开即离开。

⇒ 7b 要的「关册回暂停壳」与 39 要的「零意图」两面同时成立。

## 5. 证伪轮(2 变异,均按**最终代码**重打)

| 轮 | 变异 | 红 |
|---|---|---|
| **M1** | `OnFrame` 判据退回「暂停壳 / 脉案」两分支 | **2** —— `closesEveryMemberOfTheClosedSet` · `toggleOnInventory_closesInsteadOfSwitchingToPause` |
| **M2** | 去掉 `_modalStack.Add(previousModal)` | **1** —— `toggleOnSaveSlots_returnsToPauseShell_notWorld` |

红集互不覆盖(非全红借绿)。还原判据 = SHA-256 逐字节:
M1 `271ca19554915cc10ebac84fafbf0a56c2807f5be35930776cc66d647fa3c1a3` ·
M2 `cfb573feab31b95d6b39996800c37a896b15582330a80b3a23bfcda608e26547`(改前 = 改后)。

⚠️ 第一次跑 M1/M2 时是**通用栈**那版;收窄后代码变了,故**整轮重打** ——
本文件记的是重打后的结果,不是首版。

## 6. 判据的两条设计要点

1. **判据是枚举驱动的**(`Enum.GetValues(typeof(ModalId))`),不是逐员列举 ⇒
   闭集增员时夹具零改动,且新员**自动进判据**;逐员 if 写法下新员不在分支里就会红。
   另配一条空集守卫(`checkedCount >= 8`),防止枚举被改空导致整条恒绿。
2. **「关闭」与「换屏」必须可分** —— 必须断 `OpenCalls == 0`。只断「屏没了」不够:
   换到暂停壳同样会让旧屏消失(首版正是这样骗过所有人),那是假绿。

## 7. 回归

| 套件 | total / passed / failed / inconclusive / skipped | 截至时刻(UTC) | 对照基线 |
|---|---|---|---|
| EditMode | **6811 / 6774 / 0 / 0 / 37** | 2026-11-03 22:28:18 → 22:30:09 | `6805 / 6768 / 0 / 0 / 37` @ 2026-11-03 16:37:18Z · sprint-24 · `e567e5be` |
| PlayMode | **325 / 321 / 0 / 2 / 2** | 2026-11-03 22:26:23 → 22:26:35 | `324 / 320 / 0 / 2 / 2` @ 2026-11-03 16:39:43Z · sprint-24 · `e567e5be` |

**等式**:EditMode `6811 − 6805 = 6` = `modal_toggle_closure_test.cs` 的 6 条;
PlayMode `325 − 324 = 1` = 新增的 P-02 夹具。编译警告 **0**(两套各 0)。
`PlayMode exit=2` 是本仓常态退出码,判据取 XML `failed`(= 0)。

## 8. 逐条核查(不是从总数推的)

| 判据 | 实读结果 |
|---|---|
| `test_productionAssembly_closeModal_returnsFocusToEntryCarrier` | `Passed` |
| `test_s39_07_edge_noDuplicateNoSpuriousExplore`(回归) | `Passed` |
| EditMode 新 6 条 | 逐条 `Passed` |

⚠️ **如实登记一次我自己的检查失误**:首轮核对时我按 `entryCarrier`(小写 e)搜 XML ⇒
**零命中**,差点据此报「测试没跑」。实际测试名是 `..._returnsFocusToEntryCarrier`(大写 E),
**大小写不匹配**。改成列出全部含 `ocus` 的用例后它就在里面。
⇒ 教训与夹具纪律同源:**搜不到 ≠ 不存在**,零命中时先怀疑自己的判据。

## 9. 未做 / 不借绿

- 闭集 3 员(`SettingsShell` / `Tutorial` / `PaperCloseup48`)**生产上开不出来**
  ⇒ 它们的 toggle 只在**契约层**被判据覆盖(枚举驱动那条),**未经真装配** ⇒ 归 `TD-skeuo-04`;
- 焦点回位只在**关闭最外层模态**时生效;换屏(壳 → 子屏)**不**还原载体(设计如此);
- `Cancel` 动作读面**仍未接**(本轮走 toggle 路线,丙案未采纳)。
