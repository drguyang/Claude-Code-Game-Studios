# S22-7 · `TD-persist-13` 结案(`ParseBody` / `WriteExitSave` ≤ 40 代码行)· 执行原件

| 字段 | 值 |
|---|---|
| **卡** | sprint-22 `S22-7`(= `TD-persist-13` 的解除动作) |
| **对象** | `SaveService.ParseBody`(74 代码行)与 `WriteExitSave`(债记 61,实测 **6**) |
| **执行轮** | 2026-11-01 · 用户指令「做 S22-7」 |
| **判据载体** | `EditMode/PersistenceService/method_line_budget_test.cs`(8 条)<br>`EditMode/PersistenceService/save_slot_contract_test.cs`(+1 条,尾随字节) |

---

## 1. 开工实测:两处结论,**一处是假的**

```bash
# 用与夹具同款的度量器逐个量(代码行 = 非空、非 // 起始)
ParseBody       → 74
WriteExitSave   → 6      ← 债记「61」
```

- **`ParseBody` 真的超** —— 74 代码行,按 `── 段 ──` 注释拆。
- **`WriteExitSave` 从来就不超** —— 它是一行表达体,委托给 `WriteAtomic`。
  债记的「61」是**度量器假数**:`MethodBody` 只做花括号配平,签名行没有 `{`,
  它就一路吞到**后一个有花括号的方法**(`RaiseIncompleteFlag` / `Load`)。

> 这不是「债写错了」,是**度量器失效没有人验**:拿一个会报假大数的尺子量了几个方法,
> 尺子本身没被钉过。本卡同批把尺子修好并给它加了自测。

---

## 2. 拆分(`SaveService.cs`)

| 方法 | 代码行 | 内容 |
|---|---|---|
| `ParseBody` | **17** | 头部长度门 → 配置版本段 → 事件段 → 折叠段 → 载荷段 |
| `ParseConfigVersionSegment` | **10** | ADR-037 段 |
| `ParseFoldSegment` | **20** | 折叠段(count 前缀 + 行字节,整体交出) |
| `ParsePayloadSegment` | **25** | 载荷段头 + count 复验 + 循环 + **末段读尽** |
| `ReadPayloadEntry` | **21** | 单条 + 四条损坏校验 |

**分区不变量**:校验顺序、拒收报文、游标推进次序**逐字照搬**,未改一处语义。

---

## 3. 度量器修正(`method_line_budget_test.cs`)

- 新增 `IndexOfExpressionBodyEnd`:先扫到 `{` 或 `;`;先见 `=>` ⇒ 表达体,body 到该语句的 `;`。
- 自测 `test_measurer_expressionBody_stopsAtSemicolon`(字面量样本:3 代码行 + 不得吞 `Next()`)。

---

## 4. 判据(4 条新增)

| 判据 | 钉什么 |
|---|---|
| `test_td13_parseBodyAndHelpers_withinBudget` | 5 个方法全部 ≤ 40 |
| `test_td13_writeExitSave_expressionBody_notSwallowedByMeasurer` | **度量器不得越过表达体吞下一个方法** + ≤10 行 |
| `test_measurer_expressionBody_stopsAtSemicolon` | 度量器自测(3 行,字面量) |
| `test_td13_trailingBytesAfterPayloadSegment_areRejected` | 载荷段后尾随字节必拒(见 §5.2) |

---

## 5. 证伪(4 轮;其中两轮**先红后改**)

| 变异 | 结果 |
|---|---|
| **M1 第一版**:内联 `ReadPayloadEntry` 回 `ParsePayloadSegment` | ❌ **绿** —— 量出 38,**仍在 40 内**。**这不是假绿,是变异没跨过界**(如实登记,重做) |
| **M1′(第二版)**:把 `ParseBody` 整套撤回成拆分前单体(74 行) | ✅ `test_td13_parseBodyAndHelpers_withinBudget` 红 |
| **M2**:停用表达体识别(`end = -1`) | ✅ `writeExitSave` 判据 + 自测 **2 红** |
| **M3**:删掉 `EnsureFullyConsumed()` | ❌ **首次 271 条零红** ⇒ 见 §5.2 |
| **M3′(补夹具后)**:同上 | ✅ `test_td13_trailingBytesAfterPayloadSegment_areRejected` **恰 1 红** |

### 5.1 为什么 M1 第一版不算假绿

假绿的定义是「夹具对变异**没有判别力**」。这里夹具量得对(38 行确实 ≤40),
是变异**没把被测属性破坏掉**。区别很重要:前者要修夹具,后者要改变异。
本项目 `falsify` 的八种「假存活」里正有「变异打错靶」一条。

### 5.2 M3 暴露的真缺口(本卡顺带补上)

`EnsureFullyConsumed()`(载荷段是末段 ⇒ 尾随字节无处可去)在拆分中**逐字保留**,
但删掉它时**全 PersistenceService 面 271 条一条都没红** ⇒ 该守卫**此前零载体**。
- 已有的是**段级**判据(`fold_row_codec_test` 的「未读尽」),不是**整档级**。
- 补:`save_slot_contract_test.test_td13_trailingBytesAfterPayloadSegment_areRejected` ——
  合法档(未签名)+ 3 字节尾随 ⇒ **重签**(不重签会先撞校验和门)⇒ 写盘 ⇒ 读档必抛「尾随 … 未被消费」。

---

## 6. 回归与账面(三元组)

**截至 `2026-11-01 09:47:01 CST` · 批次 `S22-7` · commit `ec7893b2` + 未提交工作树**

| 平台 | 本次 | 前次(`09:13`) | 差 | 等式 |
|---|---|---|---|---|
| EditMode | `6341 / 6304 / 0 / 37` | `6337 / 6300 / 0 / 37` | **+4** | `6341 − 6337 = 4` = 新增 4 条判据 ✅ |
| PlayMode | `273 / 269 / 0 / 2 / 2` | 同 | 0 | 零变化 ✅ |

两轮 `error CS` = **0**。

### 6.1 账面

- `TD-persist-13`:**open → closed**;`debtcount.py --check` exit **0**(`closed 126 → 127` · `open 14 → 13`)
- **行为面零回归**:`ParseBody` 的 271 条 PersistenceService 判据全绿,M3 之外无一条转红
- 拆分前后**校验顺序 / 拒收报文 / 游标推进次序**未变(逐字照搬,见 §2)

### 6.2 如实登记的两处

1. **M1 第一版是弱变异**(38 行仍 ≤40):不是假绿,是变异没跨过界 —— 已重做为「撤回整个拆分」。
2. **`WriteExitSave` 的「61」是度量器假数**,真值 6。本卡修了尺子并给它加了自测;
   但**债册里那个 61 仍留在原文里**(按「原文不删」纪律),关闭记录已在同条下方逐字订正。
