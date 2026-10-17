# 重评原件 — medcons story-003 复核(S13-6 新判据首次实测)

**日期**: 2026-10-17
**对象**: `unity/Assets/Sim/Consequences/ConsequenceEmitter.cs` · `DelayTable53.cs` ·
`unity/Assets/Tests/EditMode/MedConsequences/medcons_delayed_emit_test.cs` · `Sim/Sim.asmdef`
**原评审**: `review-medcons-story-003-2026-10-16.md`(Verdict CHANGES REQUIRED)
**本轮目的(两项)**:
1. 核**原判据的修复是否真在代码里**(A 半边);
2. **实测 S13-6 的收窄后判据**(TD-s13-01 的机制修复)—— 这是该判据的**首次真实重评**。

**方式**: 静态核对 + `grep` / `awk`,**未跑 Unity**(工程可能被占用,且本轮判据不含执行面)。
**草稿(增量落盘产物,本轮判据的直接证据)**:
- `tmp-review-medcons-s003-recheck-a-2026-10-17.md` —— **11 行**
- `tmp-review-medcons-s003-recheck-b-2026-10-17.md` —— **45 行**

> ⚠️ **两份草稿已在合并后按 `.claude/skills/code-review/SKILL.md:180` 的纪律删除**
> (「草稿合格后由编排方合并为原件,草稿随后删除 —— 不留两份并行的分析段」)。
> 删除前实测行数如上,内容已逐条并入本件 §二 / §三。
> **代价如实登记**:「草稿在轮次耗尽前非空」这一判据的**原始物证**不再独立存在,
> 只剩本件的记录 —— 后续若要复核该判据,须在下一轮重评时**先留存副本再合并**。

---

## 一、S13-6 判据实测结果(本轮最重要的产出)

**收窄后的判据**(`.claude/skills/code-review/SKILL.md` §Phase 7b 判据节):
> 每位专家的**草稿文件在轮次耗尽前已非空**,且覆盖其判定清单**过半**。

| 专家 | 草稿行数 | 清单覆盖 | 是否撞轮次上限 | **新判据** | **旧判据**(限内交付报告) |
|---|---|---|---|---|---|
| A · 原判据核账 | **11** | **3/3 = 100%** | 否(12 次工具调用内) | ✅ **PASS** | ✅ PASS |
| B · 新缺陷 + 硬约束 | **45** | **4/4 = 100%** | **是** —— 撞 **10 轮**上限,**零报告**交回 | ✅ **PASS** | 🔴 **RED** |

**结论(可复算)**:
```
wc -l production/qa/evidence/tmp-review-medcons-s003-recheck-a-2026-10-17.md   ⇒ 11
wc -l production/qa/evidence/tmp-review-medcons-s003-recheck-b-2026-10-17.md   ⇒ 45
两文件内结论行数(以 `|` 起首的表行)均 ≥ 应核条数的一半
```
**⇒ 新判据 PASS,旧判据仍 RED。** 这正是 S13-6 要抓的情形:B 侧**又一次**撞限零报告,
但**草稿在盘上、4 项全有结论**,编排方读草稿即可补成报告 —— 机制按设计工作。

⚠️ **不得**因此声称「S13-6 已结清」:
- 判据的**有效性**本轮只测到 **1 次**(n=1);B 侧的「零报告」也说明 **agent 侧自我监控仍未修好**,
  修好的是**交付物不丢**这一条。
- `TD-s13-01` 的**残留**仍 open(见债册)。

---

## 二、A 半边 — 原判据核账(三条 BLOCKING)

| ID | 判定 | 修复落点 | 验证命令 / 核对记录 |
|---|---|---|---|
| **S-B1** | **已修(部分强支撑)** | 文档 `ConsequenceEmitter.cs:132-137`(`eligible` 的调用方义务 precondition,逐字含「须含全部 `EmitTick ≤ nowTick`」「不得按 lookback 裁剪」);夹具 `medcons_delayed_emit_test.cs:1070` | `grep -n "调用方义务(precondition,S-B1)" ConsequenceEmitter.cs` ⇒ `:132`;夹具存在性已核 |
| **S-B2** | **已修(部分强支撑)** | 预检 `ConsequenceEmitter.cs:278-296`(`stream[Count-1].Tick` 作流尾,`pending[0].EmitTick < 流尾` ⇒ 具名 `InvalidOperationException`);夹具 `medcons_delayed_emit_test.cs:1279` | 源码已逐字复核(见 §四 复核项 1) |
| **T-B1** | **部分修** | 新判别夹具 `medcons_delayed_emit_test.cs:485`(输入序打乱 ⇒ 载荷区 FNV 相等);写方 `DelayTableCookedWriter.cs:48` `rowList.Sort` 在盘上 | **旧假绿夹具 `:468` `test_ac2_bake_cookedBytes_deterministic_sameSourceTwice` 仍在盘上未删** ⇒ 「假绿消除」只达成一半 |

### A 半边指出的两处「原件结账表口径偏宽」

1. **S-B1**:原件的 `Proven ✅` 依据是「删 S-B1 前置条件块 ⇒ 夹具红」——
   而该夹具的 ⑤ 是**源码文本 `StringAssert.Contains`** ⇒ **删注释即红**,对**行为**零判别力。
   原判定要求的正向判据(裁剪 eligible 后仍断言后果数 == 2)**未被采纳**;
   修复走的是「声明前置条件」而非「补守卫」⇒ S-B1 实质仍是**调用方纪律**。
2. **S-B2**:原判定要求的夹具是「补发 ⇒ 后果数 == 2(恢复性)」;
   现夹具断言「具名失败、不死于裸 BCD 码」。**预检使失败可诊断,但「半提交后不可恢复」的病灶仍在。**
   原件把该项标 `✅ 已证` 未披露该语义差。

> 两处**不构成新 BLOCKING**(修复方向本身合理,且原判定已登记「跨域归属归 9 / 7a 轮」),
> 但**原件 §六 的 Proven 列口径须订正** —— 见 §五 处置。

---

## 三、B 半边 — 新缺陷 + 硬约束

| 项 | 结论 | 证据 |
|---|---|---|
| 门 A(`Sim` 引用集) | ✅ **通过** | `Sim.asmdef`:`"references": ["Sim.Contracts"]` + `"noEngineReferences": true` ⇒ 恰 = {BCL, Sim.Contracts} |
| 方法长度 ≤ 40 | ⚠️ **FAIL ×1**(B 报 2 条,其中 1 条**经复核为误报**) | 真:`ConsequenceEmitter.cs` `BuildDueRows` **185-235 = 51 行**(本行复核成立);误报:`test_ac2_bake_floatLiteralAndUnknownKey_hardFail` 实测 **400-411 = 12 行**,B 侧 awk 花括号配对被字符串字面量里的 `{` 带偏 |
| 自包含夹具恒真嫌疑 | ⚠️ **2 条** | ① `test_sb1_truncatedEligible_*`(:1070):断言的是「前提被违反时的可观测后果」,对生产码分支**零判别力**;② `test_ac6_bandCountSingleSource*`(:908):`BuildEligible` 是**测试侧 helper**,生产装配面无任何代码被此测约束 |
| 新缺陷 | **3 条(S3 ×3)** | 见下 |

### 新缺陷(均 S3,静态可判,未构造可执行反例)

- **D-1 · 流尾预检与写入目标可能不是同一条流**:`stream` 与 `_eventSink` 是两个独立注入物,
  构造器不校验一致性(`ConsequenceEmitter.cs:105-108`),注释自承「stream 即本发射器的写入目标视图」
  (`:280-282`)。若装配层传只读快照而 sink 指向活流,S-B2 预检**静默失效**。
- **D-2 · 部分成功后抛异常不回填 `emittedCount`**:逐条 Append 中途抛(去重键冲突 / CAP / 单调断言)
  ⇒ 已在流中的条目**不被计入**返回码;「调用方不得 catch 重试」的约束**在类型面上不可表达**。
- **D-3 · 同一 CaseClosed 以不同 outcome 重复喂入不去重**:去重键含 `outcome`
  (`:217` `seen.Add((emitTick, patient, outcome, close.Tick, close.Seq))`)⇒ 上游污染时同一病例可发两条后果,
  违 GDD `:323`「每病例至多一条」/ 卡面 Guardrail。现有 `test_edge_twoClosesWithinDelayWindow_eachEmits_notMerged`
  (:758)覆盖「两案不合并」,**未覆盖「一案重复喂不同 outcome」**。

---

## 四、编排方复核(B 侧断言逐条复算,防照抄)

| # | B 的断言 | 复核命令 | 结果 |
|---|---|---|---|
| 1 | S-B2 预检存在且抛具名异常 | 读 `ConsequenceEmitter.cs:276-296` | ✅ **成立** |
| 2 | 去重键含 `outcome` | `sed -n 217p` | ✅ **成立**(D-3 前提为真) |
| 3 | 旧假绿夹具仍在盘上 | `grep -n test_ac2_bake_cookedBytes_deterministic_sameSourceTwice` ⇒ `:468` | ✅ **成立** |
| 4 | 方法超 40 行 ×2 | 逐方法读终止行 | ⚠️ **1 真 1 假** —— `BuildDueRows` 51 行成立;`test_ac2_bake_floatLiteralAndUnknownKey_hardFail` 实为 12 行 |
| 5 | `Sim.asmdef` 引用集 | 读文件 | ✅ **成立** |

---

## 五、处置

| 项 | 处置 | 归处 |
|---|---|---|
| S-B1 / S-B2 的 `Proven ✅` 口径偏宽 | **原件 §六 加订正注**(不改原行) | 本件 §二 |
| T-B1 旧假绿夹具未删 | **登记债**,不在此轮删(删测会改计数,须单独批) | `TD-medcons-16` |
| 方法长度 `BuildDueRows` 51 行 | **登记债**(无 ≤40 门 ⇒ 无夹具) | `TD-medcons-17` |
| D-1 / D-2 / D-3 | **登记债**(S3,静态可判,未构造反例) | `TD-medcons-18` |
| 自包含夹具 2 条 | **登记债**(判别力弱,非假绿恒真) | `TD-medcons-19` |
| S13-6 新判据 | **本轮 PASS(n=1)**,有效性仍待更多样本 | `TD-s13-01` 补记 |

---

## 六、未及核查项(禁借绿)

- **未跑任何测试**:`MedConsequences` 47 个 `[Test]` / 全量回归数字**均未复跑**;
  三条「反证 ⇒ 转红」记录**未复做**,只读原件自述。
- **D-1 / D-2 / D-3 未构造可执行反例** —— 三条都是静态可判,但「可判 ≠ 已判」。
- **b2 / 门 A 的门实现段未读**(B 侧只读了 `Sim.asmdef`),「门对 `Consequences` 子目录无特判」未确认。
- **S-B2 的「流尾 = `_lastAppendedTick` 等价」前提**未在所有流视图(非主机 / 空流 / 三流混合)下验证。
