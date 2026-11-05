# `TD-skeuo-19` 兑现轮 —— 存档册翻页的两条输入面(滚轮 / 上下键到页边界)

| 字段 | 值 |
|---|---|
| **日期** | 2026-11-06(凌晨) |
| **用户裁定** | ①「不用想的太复杂,键盘和手柄的上下键移动到本页最上和最下就能进行翻页」<br>②「鼠标的滚轮也能移动焦点上下,也能进行翻页」<br>③(同轮另一条)「留档提示默认初始在 3 号位置,如果有手动存档记录,留档提示默认在最新手动存档记录的下一行」 |
| **三元组** | 截至 `2026-11-06 03:00 CST` · 批次 `TD-skeuo-19 兑现轮` · commit `6aba5507` + 工作树 |

---

## 1. 改了什么

| 面 | 落点 | 内容 |
|---|---|---|
| 42 屏 | `SaveSlotsScreen.HandleEdgeMove` / `IsEdgeMove` | 焦点在**首行再向上** / **末行再向下** ⇒ 翻页;中间行 `return false`,事件继续走官方焦点桥(ADR-013「不自实现焦点算法」不破) |
| 42 屏 | `SaveSlotsScreen.HandleWheel` / `OnWheel` | 滚轮与上下键**同一条竖轴**:一格 = 一行,到边界才翻页。挂 `_book`(不是页缘 —— 滚轮是「在册子上滚」的意图) |
| 42 屏 | `SaveSlotsScreen.StepPage` | 方向触发的翻页落**移动来的那一端**(向下 ⇒ 新页首行;向上 ⇒ 前页末行) |
| 42 屏 | `SaveSlotsScreen.TurnAfterDispatch` | 行边界上的导航事件**不在派发途中翻页**(会拆掉正在收事件的那一行 ⇒ 焦点丢成 −1,PlayMode 实测),改由屏根调度器推到派发之后 |
| 42 屏 | `SaveSlotsScreen.PageCount` | 页数**含「留档」行**;旧算法只数槽位行 ⇒ 槽位行恰好填满一页时新行被塞成第 7 行(页容量 6),纸页硬撑(4 笔手动档即触达,既存缺陷) |
| 装配 | `BootRoot.BuildSaveSlotViews` | **不再单独画一格空手动槽**(与「留档」行同一目标 `NextWritableSeq`)⇒ 「留档」恒落在**下一笔手动档的位置**:无手动档 = 第 3 位,有 = 最新手动档的下一行 |
| 删除 | `SaveBookPageDriver` / `PlayerInputReader.PagePrevPressed/PageNextPressed` / `SkeuoRuntimeDriver.PagePrev/PageNext` | 肩键路线**不做**(用户裁定)—— 方向键与 D-pad 本就是导航移动,不必再加两个动作和一个装配层驱动 |
| 文档 | `design/ux/save-slots-7b.md` §5.1 / §7 表 / §7.1 | 交互表就地改口径(滚轮 + 上下键到页边界;肩键不读),并记「留档」定位规则 |
| 文档 | `adr-041 §D1` | 补注:空手动槽让位给「留档」行 ⇒ 本裁可达面收窄(退出 / 自动两格仍是空槽),权威语义不变 |

## 2. 判据(机器)

| 套件 | 结果 | 等式 / 对照 |
|---|---|---|
| EditMode | `6957 / 6920 / 0 / 0 / 37` | 基线 `6947 / 6910 / 0 / 0 / 37`(`impl-adr042…`)⇒ **+10 / +10** = `save_book_paging_test` 10 条,零回归 |
| PlayMode | `347 / 343 / 0 / 2 / 2` | 基线 `342 / 338 / 0 / 2 / 2` ⇒ **+5 / +5** = `save_book_paging_focus_test` 5 条,零回归 |

- **EditMode** `save_book_paging_test`:滚轮/上下键的**判定逻辑**(页号变不变、消不消费、空册、越界行)。
- **PlayMode** `save_book_paging_focus_test`(真面板 + 生产 `PanelSettings` + 九份生产 USS):
  滚轮一格一行、末行翻页落首行、首行翻回落**末行**(连续竖轴)、`NavigationMoveEvent` 落边界行翻页、单页册不翻。

## 3. 真包走查(XTEST · `unity/builds/Linux-x64/大医精诚.x86_64`)

| # | 帧 | 看到什么 |
|---|---|---|
| 1 | `tdskeuo19-2026-11-06/02-liudang-4th-after-manual-save.png` | 册子摊开:**3 条有档行 + 「留档」在第 4 位** —— 手动档(02:50:03)之后紧跟「留档」,即用户裁定的「最新手动存档记录的下一行」;首行(当前载入的那份)整行灰底(ADR-042 §D6) |
| 2 | `…/01-liudang-3rd-no-manual-save.png` | 另一局:两格空槽 + 「留档」**恰在第 3 位**(无手动档 ⇒ 默认位置)—— 用户裁定的前半句 |
| 3 | `…/03-keynav-focus-on-row.png`(+ `…-3x.png` 放大) | `Down` 在行间移动焦点(键路径照常);行末时刻在 12px 下仍可读 |
| 4 | `…/04-keynav-stuck-at-last-row-BEFORE-FIX.png`(+ `…-3x.png`) | **修复前**的现场:焦点停在末行,再 `Down` 不动 —— §3.1 那条缺陷的取证帧 |

### 3.1 走查抓出的真缺陷(已修,非探针轮)

「单页册(`HasNextPage = false`)上 `Down` 从末行按不动」—— 原因是边界判据只看「是不是首/末行」,
不看**有没有页可翻**:拦下事件 + `StepPage` 返回 false ⇒ 默认焦点动作被 `PreventDefault` 挡掉,
焦点被卡在末行,**走不到它下面的「留档」新行**。修法 = `IsEdgeMove` 以 `HasNextPage` / `HasPreviousPage`
为必要条件(帧 `04-…BEFORE-FIX` 是修复前的现场,`01-…` / `02-…` 的落点是修复后的形态)。

### 3.2 ⚠️ 指针腿:**指针事件确实抵达面板,但 XTEST 瞄不准**(不借绿)

- **已证**:真包内 `_root` 上的 `PointerMoveEvent` / `PointerDownEvent` / `WheelEvent`
  **都收到过真事件**(探针实测:`rootmove (330,393)` … `rootwheel (0,3,0)`,坐标随 warp 线性变化)
  ⇒ 官方桥(`EventSystem` + `InputSystemUIInputModule` + `PanelRaycaster`)是通的,
  「指针输入在真包里彻底不工作」**不成立**。
- **未证**:把指针**稳定停在册子上**再滚一格。实测 XTEST 的 `warp_pointer` 在抬窗后一小段时间里
  只更新 y、x 卡在 15;之后 motion 事件整体不再送达(键盘仍正常)⇒ **瞄不准**,不是功能没做。
  探针已删,证据仅存本节文字(日志在 `/home/gu/.claude/jobs/6001633c/tmp/player-probe2.log`)。
- ⇒ 我这一侧(自动化注入)记 **NOT-RUN**;但**用户随后用真鼠标自己点/滚,当场确认「鼠标滚轮正常」**
  —— 指针腿在**真设备**上成立,§3.3 的两条缺陷也是他在真鼠标下发现的。⇒ 结论:**指针路径可用**,
  不可用的只是我这套 XTEST 瞄准。

## 3.3 用户真鼠标走查抓到的两处缺陷(同日修复)

用户接过真包自己点,报了两条 —— 都**不是**我这轮新引入的判定错,而是「竖轴只数槽位行」的老口径
在**末页只剩「留档」一格**这一形态上露出来的洞:

| # | 现象(用户原话) | 根因 | 修法 |
|---|---|---|---|
| 1 | 「第 7 行留档时,上和鼠标上滚回不到第一页的第六行」 | 边界判据只看槽位行:该页 `_rows.Count == 0` ⇒ 滚轮 `return false`(直接 no-op)、方向键**根本没有监听者** | 竖轴改成「槽位行 + 留档行」:`HandleWheel` 用 `total = slots + writable` 走 `FocusSlot`;新增 `HandleWritableEdgeMove` / `IsWritableEdgeMove` 挂在「留档」行上(本页没有槽位行且还有前一页 ⇒ `StepPage(-1)`) |
| 2 | 「存档第六行后,留档去了第二页,但界面还留在第一页没去第二页」 | 第 6 格恰好填满一页 ⇒ 「留档」被挤到第 2 页;写盘落定后的重画仍按**高水位页**落(§7.1 的开册口径) | `SetSlotViews(views, followWritablePage)`;`SkeuoRuntimeDriver` 的**脏标记重画**传 `true`(跟到「留档」页),**开册仍传 false**(口径不变) |

判据:EditMode `save_book_paging_test` +4 条(留档页向上翻回 / 不越界拦 / 空册 no-op 保持)、
PlayMode `save_book_paging_focus_test` +2 条(留档行向上滚落**前页末行**、脏重画跟页并落焦「留档」)。
回归 EditMode `6961 / 6924 / 0 / 0 / 37` · PlayMode `349 / 345 / 0 / 2 / 2`。

> ⚠️ 顺带修了一处**夹具侧**的连带:`pause_shell_and_frozen_refresh_test` 用反射调
> `SetSaveSlotRows(driver, new object[]{ null })`,签名多一个参数后抛 `TargetParameterCountException`
> ⇒ 补传 `false`(它模拟的是**开册**那次重画)。

## 4. 未做 / 边界

1. **肩键 `PagePrev` / `PageNext` 不读**(用户裁定):输入资产里那两个动作仍在,但**无生产消费者** ——
   这是**裁定**,不是遗留。若日后要接,仍须先改 `save-slots-7b.md` 的交互表。
2. **`PreventDefault` 能否挡住本版默认焦点动作**仍未单独判定 —— 已用「派发后再翻页」把这条依赖
   降级为「不承重」:即便默认动作照常执行,翻页与落焦都发生在它之后。
3. 「留档」定位规则**没有机器判据**(装配层私有方法);证据 = §3 的两帧真包截图。
   要机械化须把 `BuildSaveSlotViews` 抽成可直测的纯函数(未做)。
