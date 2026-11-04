# 存档遗留三条结清(转册降档 · 开册首焦点 · 「上次会话未完整保存」)

> **日期**: 2026-11-04 · **sprint-24** · commit 基线 `85de93fa` + 工作树
> **触发**: 用户问「存档 UI 问题外，其他问题有吗？」→ 逐条核对后确认三项遗留 → 指令「继续完成这些遗留」。
> **判据面**: 每条都给可跑命令与实跑输出。

---

## 0. 三条是什么

| # | 来源 | 本轮的处置 |
|---|---|---|
| 1 | `save-slots-7b.md` §5.1 / 可访问性表:「文本缩放 75–150% ⇒ 页容量 **6 行降为 4 行**,溢出转册续页,**不滚动**」 | 实现(`TextScale` / `PageCapacity`) |
| 2 | 同件 §7.1:「开册首焦点 = 当前**高水位行所在页**的首行」 | 实现(`HighWaterPage`) |
| 3 | `AC-7a-17` 后半句:「下次启动检测到标志位 ⇒ **非阻塞提示**「上次会话未完整保存」」—— 42 / 7b 都无 AC(`TD-skeuo-05`) | 补 `AC-42-H1` + 实现 + 接线 |

> **不是新发现的洞**:1 / 2 是 `TD-skeuo-07` 结案时在屏头注里**如实登记为「未做」**的两条;
> 3 是债册里唯一还开着的存档相关 P0。`TD-persist-01…19` 二十条在本次开工前**已全部结案**。

---

## 1. 判据 1:文本缩放降档(`§5.1`)

```csharp
public const int PageSize = 6;          // 100%
public const int PageSizeZoomed = 4;    // ≥150%
public const float ZoomThreshold = 1.5f;
public float TextScale { get; set; } = 1f;   // 数值归 49,42 只消费
public int PageCapacity => TextScale >= ZoomThreshold ? PageSizeZoomed : PageSize;
```

`PageCount` 与 `RenderPage` 一律改读 `PageCapacity`(不再读常量)。

夹具(`Tests/EditMode/SkeuomorphicUI/save_slots_paging_test.cs`):

- `test_zoom100_capacityStaysSix` —— 100% ⇒ 6 行 / 13 行 3 页
- `test_zoom150_capacityDropsToFour` —— 150% ⇒ 4 行 / 13 行 4 页
- `test_zoomBelowThreshold_capacityStaysSix` —— **负控**:149% 仍是 6 行(阈值是 150%,不是「≠1 就降」)

⚠️ **边界说明**:`OQ-SS-2`(用户已裁)把无障碍档位验收排在 **49 / P2**。本条只实现
「屏消费这个输入」,**不新增任何设置 UI**,装配层未注入时保持 100% ⇒ 行为与改动前逐字节一致。

## 2. 判据 2:开册首焦点落高水位页(`§7.1`)

```csharp
private int HighWaterPage()   // 视图按 slot_seq 升序;最后一个 HasSave 即台账高水位
{
    for (int i = _views.Count - 1; i >= 0; i--)
        if (_views[i].HasSave) return i / PageCapacity;
    return 0;                 // 空册 ⇒ 首页
}
```

`SetSlotViews` 用它取代原来的硬编码 `_pageIndex = 0`;`RenderPage` 另加页码夹紧
(缩放变小 ⇒ 旧页码可能越界)。

夹具:

- `test_openBook_startsOnHighWaterPage` —— 13 行 / 前 8 行有档 ⇒ 第 2 页(index 1),
  且 `FirstFocusable` 即该页首行
- `test_openBook_highWaterOnLastPage_whenZoomed` —— 同一份清单在 150% 下页码按**当前容量**重算
- `test_openBook_emptyBook_staysOnFirstPage` —— **正控**:空册仍回首页

> ⚠️ 既有的 `test_paging_resettingViews_returnsToFirstPage` 仍绿:它喂的是**未绑定**视图
> (全 `HasSave == false`)⇒ `HighWaterPage()` 返回 0,行为不变。

## 3. 判据 3:`AC-7a-17` 后半句 → 新 `AC-42-H1`

**AC 补在哪**:`design/gdd/skeuomorphic-ui.md` 新开 **H 组**,`AC-42-H1`(BLOCKING),
四条:① 提示可见且**不替换**主行;② **不抢焦点**(`focusable == false`)、**不参与拾取**
(`pickingMode == Ignore`);③ 标志位为假时**完全不占位**;④ **42 不判断**(零 `System.IO` /
`File.` / `Directory.` / `SaveIncompleteFlag`)。本组计数 43 → **44 条(BLOCKING 39)**。

**形态 = 甲**(主菜单纸面一行),文案 `MainMenuScreen.SessionIncompleteText = "上次会话未完整保存"`
(用 `AC-7a-17` 原文措辞,不另造词)。乙(暂停壳第三行,撞 `OQ-PS-3`)/ 丙(存档册页边注)未采纳。
⚠️ **甲案是实现轮就地裁定,未经用户逐条确认**(承 `ADR-034 §Decision` 口径)。

**接线**:
`BootRoot.cs:1131` → `ShowSceneLevelScreen("MainMenu", hasSave, _sessionWasIncomplete)`
→ `MainMenuScreen.SetSessionIncompleteNotice(...)`。
`_sessionWasIncomplete`(`:457`)= `saveService.IsSessionIncomplete`,7a 侧早已就绪 ⇒ 本条**只补呈现**。

夹具 `main_menu_session_notice_test.cs` **7 条**:默认隐藏 · 置位可见 · 不替换主行 ·
不可聚焦且不拾取 · `FirstFocusable` 不变 · 复位后重新不占位(负控)· 42 侧零文件访问(结构断言)。

---

## 4. 回归(三元组)

**截至** `2026-11-04T01:28:59Z`(本地 09:28)· **批次** `sprint-24 存档遗留轮` · **commit** `85de93fa` + 工作树

| 套件 | 结果 | 对照 |
|---|---|---|
| EditMode | `6824 / 6787 通过 / 0 失败 / 37 跳过` | 上轮 `6811/6774/0/37` ⇒ **+13 = 转册 +6 · 提示 +7**,零失败 |
| PlayMode | `331 / 327 通过 / **0 失败** / 2 不确定 / 2 跳过` | 上轮 `331/325/**2 失败**/2/2` ⇒ 两条转绿,零回归 |
| 定向:转册夹具 | `14 / 14` | 原 8 条 + 新 6 条 |
| 定向:提示夹具 | `7 / 7` | 新文件 |
| AC 编号门 | ✅ 无新增撞号 | `tools/acid/acid_check.py --check` |
| 债册门 | ✅ §五 与索引一致 | `tools/debtcount/debtcount.py --check` |

债务变化:`TD-skeuo-05` **closed**(六十四订)· 未闭 P0 面 **8 → 7** · `skeuomorphic-ui 3 → 2`。

### 4.1 首轮 PlayMode 的两条红与本轮修正(如实登记)

首轮全量 PlayMode 是 `331/325/**2 失败**`。两条**都是本轮的改动打出来的**,不是既有隐患:

**① `test_bootWithMenu_showsSingleRow_andLandsFocus` —— 提示不该常驻**

该夹具按 spec §5「一张纸、一行」数**菜单子树内的文本元素个数**。我把提示做成
「构造期建好、默认 `display:none`」⇒ 常态下也数到 **2 个**,当场红。

**修正:`MainMenuScreen.EnsureNotice()` 改为按需创建** —— 没有东西要报时,纸面上连
元件都不存在;只有在 `SetSessionIncompleteNotice(true)` 时才建。
这比 `display:none` 更贴近 `AC-42-H1` ③「完全不占位」,且**没有削弱任何规格断言**。
观测面随之订正:`SessionIncompleteNotice` 常态返回 `null`
(`test_notice_isHidden_byDefault` 已改为断言 `null`,并写明为什么必须是这个形态)。

**② `test_pageEdges_...` —— 我自己的几何夹具前提过期**

`§7.1` 落地后开册落在**高水位页**;夹具的 10 行**全有档** ⇒ 高水位 = 第 9 行 ⇒
开册即**末页**,而旧断言写死了「开在第一页,必有下一页」。

**修正:不是把夹具绕开新行为,而是把新行为写成判据的一部分** ——
`test_pageEdges_areLaidOut_onlyWhenThereIsAPageThatWay` 先断「开册在末页(index 1)」,
在末页断「右缘零宽 / 左缘占位」,再 `PreviousPage()` 翻到第 1 页断**两个方向互换**。
旧实现开在第 1 页 ⇒ 第一条断言当场红 ⇒ 它同时是「开册页」的负控。

---

## 5. 如实登记的残留(不借绿)

1. **没有 PlayMode 夹具**覆盖「真启动序里 `_sessionWasIncomplete` 真的传到屏」——
   本轮装配断言只在 EditMode 层;BootRoot 启动序仍只有单元级接线证据。
2. **提示形态(甲)待用户追认** —— 若要改 乙 / 丙,改的是 `AC-42-H1` 的 ①③ 观测量,不重开 7a 侧裁决。
3. **文本缩放的数值仍归 49(P2)** —— 本轮没有任何设置 UI,`TextScale` 未注入时恒 100%。
4. 页码数字形态仍是 `"1 / 2"` 纯文本替身(spec 要求本地化素材档)—— **本轮未动**。
