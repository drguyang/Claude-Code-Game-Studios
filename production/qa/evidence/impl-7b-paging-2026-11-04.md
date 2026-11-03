# 7b 存档册「转册」实现轮 + 孤儿清理

**截至时刻**: 2026-11-04 00:20 CST(2026-11-03 16:20 UTC)
**批次**: sprint-24 · 用户指令「把 7b 的册页分页做了。孤儿清理掉」
**commit**: `93bf989c` + 工作树(本轮提交前)

---

## 1. 做了什么

| 规格条目 | 出处 | 实现 |
|---|---|---|
| 每页 6 行,超出转册续页 | `save-slots-7b.md` §5.1(OQ-SB-2) | `SaveSlotsScreen.PageSize = 6`;`RenderPage()` 只画 `[_pageIndex*6, min(+6, n))` 这一段窗口 |
| 鱼尾页码 | §5.1 | `save-slot-page`(`Label`),文本 `"a / b"` |
| 页缘热区(指针点 / 手柄 Submit) | §5.2「零按钮元件」 | `save-slot-prev` / `save-slot-next`,两条输入路径都汇到 `PreviousPage()` / `NextPage()` |
| 不可翻时不出现 | §5.2 | `display = None` **且** `focusable = false`(见 §5) |
| 翻页后焦点重置为该页首行 | §7.1 | `FocusFirstRowOfPage()` |
| 「留档」新行只在末页 | §5.2(新行 = 末行之后) | `if (!HasNextPage) BuildWritableRow(...)` |

**行号是页内的**:`Activate(rowIndex)` / `RowAt` 都是页内面;`_slots` 同步只装本页的槽号。
不换算 ⇒ 按第二页会读到第一页的档 —— 这是最坏的静默错读,夹具 `test_paging_activateIsPageLocal` 正打它。

## 2. 生产可达性(不是空转)

按用户机器当前盘面:

```
手动槽 [3,4,5,6,7,8] ⇒ next = 9 ⇒ 册上行 = [1,2] + [3..8] = 8 行 ⇒ 2 页
```

⇒ 分页在生产上**今天就会触发**,不是等将来槽位多了才用的死路径。

## 3. 回归

| 套件 | total / passed / failed / inconclusive / skipped | 截至时刻(UTC) | 对照基线 |
|---|---|---|---|
| EditMode | **6797 / 6760 / 0 / 0 / 37** | 2026-11-03 16:14:59 → 16:16:50 | `6789 / 6752 / 0 / 0 / 37` @ 2026-11-03 15:57:47Z · sprint-24 · `93bf989c` |
| PlayMode | **324 / 320 / 0 / 2 / 2** | 2026-11-03 16:17:29 → 16:17:41 | `324 / 320 / 0 / 2 / 2` @ 2026-11-03 15:52:50Z · sprint-24 · `93bf989c` |

**等式**:`6797 − 6789 = 8` = 新增 `[Test]` 数(`save_slots_paging_test.cs` 共 8 条)。
编译警告 **0**。`PlayMode exit=2` 是本仓常态退出码,判据取 XML 的 `failed` 计数(= 0)。

## 4. 证伪轮(1 变异,命中)

| 变异 | 内容 | 结果 |
|---|---|---|
| **M1** | `UpdateChrome` 删去 `_prevEdge.focusable = …` / `_nextEdge.focusable = …` 两行 | **1 红 / 8**(`test_paging_edgesOnlyShownWhenTurnable`) |

**还原判据**:SHA-256 逐字节 ——
`before = be5d3e620b8a1b353483201cf62debb6278f1dae2578ab79e67dbcfd92176ed1` ·
`after = be5d3e620b8a1b353483201cf62debb6278f1dae2578ab79e67dbcfd92176ed1` · `MUT-` 残留 **0**。

## 5. 本轮自己抓到并修掉的一处**未验证断言**(如实登记)

首版 `UpdateChrome` 的注释写:

> 「不可翻时页缘**不占位**(`display=None`),因此**也自然退出焦点遍历**。」

**后半句没有证据。** 查 Unity 6000.3.24f1 的 `UnityEngine.UIElementsModule.xml`:
`Focusable.canGrabFocus` 的文档只有一句「Whether the element can be focused」,
`focusable` 是「Whether an element can potentially receive focus」——
**没有任何一处说 `display` 会影响焦点可达性**。这是推断,不是事实。

**修法**:不删注释了事,而是把断言变成**由构造保证** —— `UpdateChrome` 显式写
`_prevEdge.focusable = HasPreviousPage; _nextEdge.focusable = HasNextPage;`,
夹具同时断言 `display` 与 `focusable` 两面。上面 M1 就是打这条。

⚠️ 这属于本仓反复登记的 **F6(自述顶替门)** 的近亲形态:**注释里的一句因果,被当成实现性质**。
差一点就带着它提交了。

## 6. 孤儿清理(**是搬走,不是删除**)

六个文件 `save_0/1/2.bin` + `save_0/1/2.bak` 被移到:

```
~/.config/unity3d/DefaultCompany/unity/saves/_orphans-2026-11-04/
```

**为什么判定为孤儿**:`TD-persist-17` 之后槽 1 = `save_exit.bin`、槽 2 = `save_auto.bin`,
手动槽从 3 起 ⇒ 这些文件名**没有任何代码路径会读或写**。

**复算命令**:
```bash
D="$HOME/.config/unity3d/DefaultCompany/unity/saves"
ls "$D" | sort                      # 期望:只剩 save_3..8 / save_exit.bin / 两个 flag
ls "$D/_orphans-2026-11-04" | sort  # 期望:六件全在
grep -n "EnumerateFiles(_basePath" unity/Assets/Sim.Codec/SaveService.cs
# 期望:2 处,都是两参重载 ⇒ 非递归 ⇒ 子目录里的文件不被扫到
```

⚠️ **诚实补充**:它们即使**留在原地**也不影响高水位 —— `TryGetHighestPlayerSlotOnDisk`
有 `if (seq < MinManualSlotSeq) continue;`,槽 0/1/2 本来就被跳过。
⇒ 这次搬走是**卫生**(盘面干净、尸检不误导),**不是**修正确性。两者不要混读。

⚠️ **可逆**:文件在,随时搬回去。

## 7. 未做(禁借绿)

| # | 规格条目 | 状态 |
|---|---|---|
| ① | §5.1「文本缩放 150% ⇒ 页容量降为 4 行」 | **NOT-RUN** —— 页容量今日是常数 |
| ② | §7.1「开册首焦点 = 当前高水位行**所在页**的首行」 | **NOT-RUN** —— 开册一律停首页(装配方仍只取 `FirstFocusable`) |
| ③ | 页码的**数字形态**(本地化素材档) | 今日是 `"a / b"` 纯文本替身 |
| ④ | AC-7b 走查整体 | 未做 —— 本轮只落了册页结构,行内文案 / 元数据仍缺 |

## 8. 勘察中发现的**另一件事** → 已立 `TD-persist-19`(本轮不动代码)

盘上有 `save_3.bak` / `save_4.bak` / `save_5.bak`(mtime 2026-11-03 08:22,早于「只进不退」生效)。
它们**不是**孤儿:`SaveService.LoadBytes`(`:599-606`)仍对**任何**槽无条件回退 `save_{n}.bak`,
而 `TD-persist-16` 收窄的只是**写侧**(玩家槽不再产生 `bak`)。⇒ 读侧支持一个写侧产不出来的东西,
且用户盘上已有 `bak` 让这条路径**今天就可触发**:`save_3.bin` 一旦校验坏,会**静默装回更旧的 3 号档**
而不是按 `AC-7a-15` 报损。

**本轮不动它**,理由:① 属 7a 的 bak 口径,不属 7b 分页;② 改 `LoadBytes` 会翻转一条既有绿夹具
(`test_checksumFallback_readsBakWhenFinalCorrupted`,它用 `SaveBytes` 绕过只进不退门造出生产不可达的
`bak` 再断言回退成功)的语义;③ 盘上那三件 `bak` 的处置也须一并裁。

⇒ 已登记为 **`TD-persist-19`**(`open` · P0 面 **Y**),债册五十八订。
**这是本轮唯一的 P0 面变化**:未闭 P0 面 `9 → 10`,epic 分布 `2/31 → 3/31`。
