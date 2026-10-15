# 修复证据 —— S12-2:Story009WalkthroughDriver `passed` 汇总 bug

- **对象**:`unity/Assets/Editor.Tools.Spike/Story009WalkthroughDriver.cs`(Editor-only,不进构建)
- **日期**:2026-10-15 · **Sprint**:12 · **执行者**:Claude
- **基线三元组(S12-0 规则的第一次适用)**:截至 **2026-10-15 07:54 UTC** · 批次 **sprint-12 · S12-2 全量门** · commit **`a5ef7c5`**
- **前置**:S12-0 伪基线订正已生效 —— 本件不用 `3366/3320/0/46`(伪终值),真基线 = `3388/3342/0/46`

---

## 一、原判定的实际情况(如实:原判定未记录现象)

承接来源 `production/session-state/active.md:3851`:

> 前置 = 修 `Story009WalkthroughDriver.cs:406` 的
> `r.passed = r.checks.All(c => c.StartsWith("✅"))` 汇总 bug(**实测仍在**)。

⚠️ **该条只指了行号,未记录「错的现象是什么」**。本件先构造三个可证伪假设逐一核验:

| 假设 | 检验 | 结论 |
|---|---|---|
| A. 空腿假绿(`All` 对空集恒真) | 每个 `Finish` 前是否 ≥1 条 `Check`(含 `:185`/`:269` 两条 early-exit) | **不可达** —— 两处 early-exit 前都有 `Check`,故 `checks` 非空 |
| B. 前缀碰撞(`"✅x"` 蒙混) | `Check` 只产 `✅`/`🔴` 两前缀 | **不可能** |
| C. 多腿 JSON 不自洽 | `:133` `_results.Clear()` + `finally:162` 单次写盘;单腿入口同路径 | **自洽** |

⇒ 三假设均未命中 ⇒ **先写夹具再定 bug**,不让"原判定"替代现场。

---

## 二、夹具实测抓到的真 bug

**夹具**:`unity/Assets/Tests/EditMode/Spike/story009_walkthrough_driver_summary_test.cs`(6 条,新建)
**装配接线**:`unity/Assets/Editor.Tools.Spike/AssemblyInfo.cs`(新增,IVT 先例 = `Sim.Codec/AssemblyInfo.cs`)
+ `unity/Assets/Tests/EditMode/EditMode.asmdef` 增 `"Editor.Tools.Spike"` 引用。

**第一跑即转红**(`Logs/s12-2-summary2.xml`,5 测 3 过 2 红):注入**一条 `🔴` 失败断言**,
`Finish` 后 `passed == true`。诊断实测(`Logs/s12-2-diag.xml`):

```
DIAG before: checks=1 passed=False | after: checks=1 passed=True | checks[0]=🔴 x
```

⇒ **真 bug 坐实**:`"🔴 x".StartsWith("✅")` 在 Mono 下返回 **true**
⇒ 一条失败的断言让整腿被判「全绿」。

**根因**:`StartsWith(string)` 无 `StringComparison` 重载 ⇒ culture-sensitive 比较,
在 Mono 下对**非 BMP emoji 前缀**(`✅` = U+2705 · `🔴` = U+1F534)处理不可靠。
字节级已核两个 emoji 均为合法 UTF-8(`E2 9C 85` / `F0 9F 94 B4`)⇒ **不是编码问题,是比较语义问题**。

**危害面(为何 BLOCKING)**:Driver 是 S12-10 人工走查的汇总源。若一条断言失败却报「全绿」,
走查结论即**假绿** —— 且 JSON 落盘的 `passed` 字段同源,下游读 JSON 的也一样被骗。

---

## 三、修复(真值完全不经字符串)

`LegResult` 增 `OkCount`/`FailCount` 两个整数;`Check` 逐条累计;`Finish` 只读整数:

```csharp
int ok = r.OkCount, bad = r.FailCount;
if (ok + bad == 0) { r.checks.Add("🔴 本腿零断言执行(空腿)——结构缺陷,非通过"); r.passed = false; }
else               { r.passed = bad == 0; }
```

`checks`(emoji 前缀串)**仅作人读展示**,不再承载判定语义。
`Check` 内原有的 `if (!ok) r.passed = false;` **保留**(逐条求真,与收腿求真同向加固)。
空腿 fail-loud 闸一并落入(原 `All` 对空集恒真的理论缺陷,当前文件内不可达,但**一旦可达立即报红**)。

---

## 四、判据(可证伪)

| 平台 | 退出码 | XML(三元组:2026-10-15 07:54 UTC · S12-2 全量门 · `a5ef7c5`) |
|---|---|---|
| 本 fixture | **0** | `total=6 passed=6 failed=0`(`Logs/s12-2-v3.xml`) |
| 全量 EditMode | **0** | `total=3394 passed=3348 failed=0 inconclusive=0 skipped=46`(`Logs/s12-2-full.xml`) |
| 全量 PlayMode | **0** | `total=186 passed=186 failed=0`(`Logs/s12-2-full-pm.xml`) |
| `error CS` | — | **0 / 0** |

**零回归核算**:基线 `3388/3342/0/46` → `3394/3348/0/46` ⇒ **+6 / +6**,恰为本 fixture 新增 6 条,**零删除零失败**。
S12-0 订正后的真基线首次被用作对照。

### 反证(回退即转红 · S12-4 新规的第一次适用)

把 `Finish` 判据改回 `r.checks.All(c => c.StartsWith("✅"))`:

```
REVERT_EXIT=2   total=6 passed=3 failed=3     (Logs/s12-2-revert.xml)
```

⇒ 夹具**确能**抓到该 bug(3 条红:1 绿 1 红 / 全红 / emoji 专钉三条)。
反证后**立即还原**,`diff` 与修复版 **IDENTICAL**;并核实无"刻意回退"字样残留(前 2 处 `StartsWith` 命中
均在注释与文档说明中,非代码判据)。

---

## 五、附带改动(超出原判定的,如实登记)

| # | 改动 | 理由 |
|---|---|---|
| 1 | 新建 `Editor.Tools.Spike/AssemblyInfo.cs`(IVT) | 夹具须打 `internal` 的 `Check`/`Finish`/`LegResult`;先例 = `Sim.Codec/AssemblyInfo.cs`,ADR-025 §① 成例。Editor-only,不涉门 A、不进构建 |
| 2 | `EditMode.asmdef` 增 `"Editor.Tools.Spike"` 引用 | 同上 |
| 3 | 测试方法命名改 `test_[scenario]_[expected]` | 首稿误用 PascalCase;按 `.claude/rules/test-standards.md` 与 CaseSystem 全族惯例订正 |
| 4 | `Finish` 的空腿 fail-loud 闸 | 假设 A 虽不可达,但"一旦可达即报红"是零成本防线 |

**产品码变更范围**:仅 `Story009WalkthroughDriver.cs`(判据改整数累计 + 计数器)与上述装配面;
**无任何 sim / 构建 / 场景改动**。

---

## 六、未闭合 / 交接

- **`WriteResults` 的 JSON 仍含 `passed` 字段** —— 现由整数判据供给,**真值已正确**;
  但 JSON 无人消费侧的回归(下游若曾按旧假绿读,需知悉),无代码动作。
- **S12-10 前置已满足**(汇总可信),人工走查可启动。
- ⚠️ **本件的教训已沉淀为 S12-4 议题**:「fail-loud 判据不得用文化敏感字符串比较」——
  建议扩入 control-manifest(emoji / 前缀 / `Equals` 默认比较 一律显式 `StringComparison.Ordinal`)
  —— **sprint-12 的 S12-3 承载**(Sim 引用集前置检查同批建闸)。
