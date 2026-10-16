# 新测试自检前置(sprint-13 S13-5 · 复盘行动项 4)

## 为什么有这件

sprint-12 返工期把三类自检**散落**成一次性 `python -c` 片段,未固化为可复用步骤 ——
下一个 story 又要重造。本目录把它收成**单一入口 + 明确的「何时必跑」**。

出处:
- `production/qa/evidence/review-medcons-story-002-2026-10-15.md:375` —— X-2「`EncodeRowKey`
  换乘子时丢一个配对 `)`,CS1026 整条编译链红 ⇒ **括号改配平须编译期才可见**」
- `production/qa/evidence/fix-s11-12-case-system-story-001-b2-b4-2026-10-15.md:253` ——
  静态自检四项(`[Test]` 计数 / 花括号配平 / 无悬空引用 / `using` 未动)
- `production/qa/qa-plan-sprint-13-2026-10-16.md:172` —— S13-5 判据:对
  `medcons_outcome_judge_test.cs` 跑 ⇒ 结论与返工期实测四项一致

## 用法

```bash
python3 tools/test-precheck/precheck.py <文件...>       # 逐个自检(.cs / .json)
python3 tools/test-precheck/precheck.py --dir <目录>     # 递归扫 *.cs / *.json
python3 tools/test-precheck/precheck.py --json           # 机读输出(CI 用)
python3 tools/test-precheck/precheck.py --dangling <文件>  # 额外跑悬空引用粗筛(默认关)
```

**退出码**:0 = 全过;1 = 有 FAIL(CI 可直接用)。

## 何时必跑

| 场景 | 为什么 |
|---|---|
| **① 手改 / 新增测试件后、跑 batch 之前** | 括号失配会让**整条编译链红**(X-2 实测)。一次 batch 编译 ~6 s + 域重载;前置检查 0.1 s 就能挡掉。 |
| **② 手改 JSON 数据表后、烘焙之前** | JSON 词法失败在阶段 1 才报;前置可当场定位到行列。 |
| **③ 断言消息与断言值不自洽时** | 返工期「断言自相矛盾」实测过(fix-s11-14 类);一条消息写「应为 1」而断言 `AreEqual(0, …)` = 测试自欺。 |

## 自检项

| # | 项 | 级别 | 判据 |
|---|---|---|---|
| 1 | **括号配平** `{ } ( ) [ ]` | **FAIL** | C# 字符串 / 逐字串 / 字符 / 注释感知 —— 字面量里的括号不算(返工期若用朴素 `count` 会误报) |
| 2 | **JSON 合法性** | **FAIL** | `.json` 文件;失败报行列 |
| 3 | **断言自相矛盾** | **FAIL** | 单条 `Assert.AreEqual(期望, …)` 语句内,消息**明确写出**的期望数字(「应为 N」/「恰 N 条」)与期望值不符 |
| 4 | `[Test]` 计数 | 报告 | 非判据,供报告引用 |
| 5 | 悬空引用 | 告警 | **默认关**(`--dangling`)—— 命名空间末段会被当类型名,假报多,仅人工排查用 |

## ⚠️ 判据刻意保守(2026-10-16 首版实测收紧)

首版 [3] 按「消息里出现任何 `= N` / `档 N`」判 ⇒ 对
`medcons_outcome_judge_test.cs` 误报 3 条、对本 story 新件误报 1 条,**全是假红**
(消息里的 `= 0` 指代的是**别的量**,如「漏发 = 0」,不是期望值)。
**假红比漏报更糟** —— 会让人整体忽略该检查。故收紧为:只认**明确措辞** + 限定在
**单条断言语句**内(括号配平截断,不跨到相邻断言)。

## 证伪轮记录(S13-5 · 改→跑→还原)

| 注入的错误 | 期望 | 实测 |
|---|---|---|
| ① 删一个配对 `)` | FAIL | `[1] 括号配平:第 141 行 '}' 与第 134 行的 '(' 不匹配` ✅ |
| ② JSON 删一个 `}` | FAIL | `[2] JSON:第 8 行第 1 列:Expecting ',' delimiter` ✅ |
| ③ `AreEqual(0, 1, "漏发应为 1 条")` | FAIL | `[3] 断言自相矛盾:第 3 行:断言期望值 0,而消息明确写出 [1]` ✅ |
| 误报侧:4 件合法件 | 全 OK | `FAIL 0 · WARN 0` ✅ |
| 误报侧:全 MedConsequences 目录批扫 | 全 OK | `FAIL 0 · WARN 0 · 文件 3` ✅ |

## 卡面判据兑现(S13-5)

> 判据 = 对 `medcons_outcome_judge_test.cs` 跑出与返工期一致的结论

实测:`[OK] medcons_outcome_judge_test.cs [Test]=41`,`FAIL 0 · WARN 0` ——
`[Test]` = 41(与文件实况一致)· 括号配平(168/168 花括号、938/938 圆括号)· JSON 合法。
**与返工期四项静态自检结论一致**(`fix-s11-12-...md:253` 的 `[Test]`=9 是另一件;本件的 41
为本件实况)。
