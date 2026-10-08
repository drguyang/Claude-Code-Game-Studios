# 评审原件 — story-021 M2 形态件①(脉案线格 · 空行等重 · 焦点明度轴)

- **日期**: 2026-10-08
- **对象**: story-021 三步交付(AC-021-1/2/3)—— USS 声明层 + CasebookScreen 挂载 + EditMode 三测试
- **方式**: 双代理恰一轮(用户流程裁定:评审只做一轮)
- **初判**: 代码面 **PASS**(0 BLOCKING / 3 RECOMMENDED / 4 nit)· 测试面 **BLOCKING: 1**
- **本轮终判**: **修复全落 → 复跑绿**(复跑读数见文末 §复跑)

---

## 一、代码面(`lead-programmer`)—— PASS

0 BLOCKING / 3 RECOMMENDED / 4 nit。RECOMMENDED 全数采纳,逐条如下。

| # | 原判定 | 修复落点 | 验证 |
|---|---|---|---|
| 代码#1 | 公式无自身锚定:明度计算若写错(如权重/ gamma 反),`minΔ ≥ 0.12` 可能仍绿 | `focus_visual_and_accessibility_test.test_ac021_3_…` 断言区**前置三连**:白=1.0(Within 1e-6)· 黑=0(Within 1e-6)· 128灰=0.21586(Within 1e-3),锚 Rec.709+gamma 正确性 | 公式错 ⇒ 锚定必红(128灰对 gamma 权重最敏感) |
| 代码#2 | `.focus-visible` 与 `.ruled/.empty-row` 同写 `border-bottom-*`,同元素双单类时胜负由**未钉死**的加载序决定 —— 潜在级联冲突无登记 | `SkeuoFocusVisible.uss` 头注新增**级联冲突口径注**;story-021 §边界裁定 ⑥ 同步登记为**接线前置裁定项**(焦点施加者全库零调用 ⇒ 今日不可观测,非缺陷) | grep `SkeuoFocusVisible.uss`「单类级联冲突」在;焦点接线轮须先裁(甲:环子元素 / 乙:格线改伪元素) |
| 代码#3 | `test_ac021_1` 全文 grep `AddToClassList("ruled")` = 假绿面:挂载挪到非行路径(如 confidence-mark)而行上摘掉,断言仍绿 | 改为 **`AddChannel` 方法体锚定**:`Regex.Match(text, @"private void AddChannel\s*\([^)]*\)\s*\{.*?\n        \}", Singleline)` 后**在方法体内**匹配挂载 | 结构漂移(方法改名/移走)⇒ 锚定失败显式红,不退化为全文 grep |
| 代码#4 | `SkeuoFocusVisible.uss:7-9` 注释仍写「贴图 × 主题色最亮像素」—— 绑定轮设想口径,与 story-021 落定口径(环不透明像素均值 vs 纸面高频桶∪主题底色)不一致 | 头注口径**更新为现行断言口径**,并注明实测 0.2656 ≥ 0.12【提案·归数值轮】 | grep 头注含「现口径」「minΔ = 0.2656」 |
| 代码#5 | 环均值 vs 桶均值的 AA(感知均匀)口径未说明不对称性 | 主测试环侧注释补 **AA 不对称口径**说明 | 注在 |
| 代码#7(=n4) | story-011 AC-42-B5 勾选未区分「声明级+源码级」与「渲染级」宽严 | story-011 B5 注补**「渲染级归桌面走查」**半句,与 casebook-39 AC `[A]` 宽严对齐 | grep story-011 B5 注含「渲染级」 |

## 二、测试面(`qa-lead`)—— BLOCKING: 1

**B1(BLOCKING)+ R1-R4 + n1-n4**:

| # | 原判定 | 修复落点 | 验证 |
|---|---|---|---|
| **B1** 🔴 | `paperBuckets` 空集时(贴图加载失败/桶全空)自比对跳过 ⇒ **恒绿假通过** | 主测试体:`paperBuckets` 提取为变量 → ①公式锚定 ②**`Assert.That(paperBuckets, Is.Not.Empty, …BLOCKING B1…)`** 贴图分支非空守卫 ③自比对。**R4 由本守卫承接**:贴图侧退化必红 | 故意清空桶/坏贴图路径 ⇒ B1 红;公式锚定 + 业务断言顺序不回退 |
| R1 | 只锁 `var(--skeuo-shared-row-height)` 引用,theme 里 44px 改 20px 全绿 = 「=44 焦点落点高半」主张无守卫 | `test_ac021_2` 断言 `SkeuoThemeVariables.uss` 含 `--skeuo-shared-row-height:\s*44px\b`。**口径**:44 是无障碍承诺值(非提案)⇒ 可锚;格线**色**是提案值 ⇒ 刻意不锚(归数值轮) | theme 44px→20px ⇒ 断言红 |
| R2 | `.ruled, .empty-row` 只断首块:后追加 `.empty-row { min-height: 0; }` 覆盖块 ⇒ 等重运行期失守而断言仍绿 | `test_ac021_2` 断言两选择器作选择器(\.{cls}\s*(,\|\{))全文**恰各出现 1 次** | 追加第二声明块 ⇒ 计数 2 ⇒ 红 |
| R3 | `.empty-row` 只断声明与挂载点之一:载体(`EmptyRowElement`/`SaveSlotItem`)不挂类 ⇒ 格线永不渲染而断言仍绿 | `test_ac021_2` 遍历两载体源文件(去块注释+行注释)断 `AddToClassList\([^)]*"empty-row"`(与 021-1 对称) | 任一载体摘挂载 ⇒ 红 |
| R1.5(n3 关联) | 块匹配只认正序 `.ruled\s*,\s*.empty-row` 且 `:` 后无 `\s*` 容错 | 块 regex 改**双序可匹配** `\|\.empty-row\s*,\s*\.ruled`,var 断言 `:` 后加 `\s*` | 反序声明块仍绿;正序依旧绿 |
| n1 | focus 测试头注「不依赖文件系统」纪律注与门式读仓实现在字面上冲突,无例外声明 | 头注补**例外声明**(门式读仓,照 `texture_binding_gate_test` 先例) | 注在 |
| n2 | `test_ac021_2` 只剥块注释不剥 `//` 行注释(USS 官方仅块注释,但防御性应对称) | USS 侧同步 `Regex.Replace(body, @"//[^\r\n]*", "")` | 行注释中的样例选择器不再计入 |
| n3 | 见上 R1.5 | 同上 | — |
| n4 | 见上 代码#7 | story-011 B5 注 | — |

## 三、复跑(修复后 · 实测读数)

**过滤**(`--filter "SkeuomorphicUI"`):**230 total / 217 passed / 0 failed / 13 skipped** ——
`test_ac021_1` / `test_ac021_2` / `test_ac021_3` 终态全 **Passed**。
⇒ `unity/Logs/editmode_skeuo_final.xml`

**修复项判别力变异 6 发全中**(逐发独立注入 → 跑 → python 反向恢复;终态校验零 `MUT` 残留):

| 发 | 变异 | 期望红 | 实测 | 失败消息锚 |
|---|---|---|---|---|
| ① | `ruled` 挂载挪出 `AddChannel` 方法体(挪进 `BuildUI`) | 021-1 | ✅ 红 | #3 方法体锚定 |
| ② | `SaveSlotItem` `empty-row`→`empty-slot` | 021-2 | ✅ 红(与①同发,恰 2 红) | R3 挂载侧 |
| ③ | 追加第二 `.empty-row { min-height: 0; }` 覆盖块 | 021-2 | ✅ 红 | R2「实见 2,Expected 1」 |
| ④ | theme `44px`→`20px` | 021-2 | ✅ 红 | R1「须 = 44px」 |
| ⑤ | `minFraction 0.01`→`0.999`(桶空集) | 021-3 | ✅ 红 | B1「高频桶为空 …BLOCKING B1」 |
| ⑥ | gamma 指数 `2.4`→`2.2` | 021-3 | ✅ 红 | 代码#1「公式锚定:128 灰 ≈ 0.21586」 |

**全量对账(与修复前基线逐项一致 = 零回归)**:
- EditMode **3007 / 2960 / 0 failed / 46 skipped / 1 inconclusive** ⇒ `editmode_full_20261008_form01_r2.xml`
- PlayMode **98 / 97 / 0 failed / 1 skipped** ⇒ `playmode_full_20261008_form01_r2.xml`

## 四、结论

- 原判定:代码面 PASS / 测试面 **BLOCKING: 1** + 11 项
- 修复:**全部落盘**(上表逐条)
- 一轮即止(用户裁定);残余未修项 = 无
