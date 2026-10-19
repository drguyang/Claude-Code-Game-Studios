# 证据原件 — sprint-13 治理件 S13-4 / S13-5 / S13-6 / S13-8

**日期**:2026-10-16
**对象**:sprint-13 Should-Have + Nice-to-Have 治理件(复盘行动项 2 / 4 / 3 + 债册)
**类型**:治理(工具 / 纪律固化 / 登记)—— 判据 = 可证伪动作,非「写进文档」

---

## S13-4 · asmdef 引用面判读固化(复盘行动项 2 · High)

### 原判定(病因)
sprint-12 X-1:判 H 项只读 asmdef **名称列表**、漏看 GUID 形引用 ⇒ 按名补一条
`"Editor.Tools.Gates"` 触发 `Assembly has duplicate references` **编译红**
(出处 `retro-sprint-12-2026-10-16.md:78-79 / :89`)。

### 修复落点
| 文件 | 改动 |
|---|---|
| `unity/Assets/Editor.Tools.Gates/AssemblyGates.cs` | 新增 `FindDuplicateResolvedReferences`(GUID → 装配名解析**后**判重)+ `CheckReferenceDuplicates`(`[b3-refdup]`);接入 `RunAll`(:115 后) |
| `unity/Assets/Tests/EditMode/Sim/assembly_gate_refdup_test.cs` | 新增(7 测试) |

### 验证命令 + 实测
```
Unity -batchmode -runTests -testPlatform EditMode -testFilter "AssemblyGateRefDupTest"
```
**结果**:`total 7 passed 7 failed 0`(`unity/Logs/s13-4-refdup.xml`)。

### 可证伪判据(证伪轮 · 改→跑→还原同一 Bash 调用)
| 注入 | 期望 | 实测 |
|---|---|---|
| 停用 `FindDuplicateResolvedReferences` 的判重(恒返回空) | 漏报侧转红 | **2 红**:`test_refdup_nameFormPlusGuidForm_sameAssembly_reported` · `test_refdup_trueDuplicateSameForm_reported` ✅ |
| md5 还原校验 | 一致 | `4bc24571c6beacecad7a8daeea8418e7` → 一致 ✅ |

**误报侧**(不得把单次声明判成两次):`guidFormOnly` / `nameFormOnly` / `twoDistinctAssemblies` 三条绿 ✅
**真树**:0 重复引用(扫描 17 个 asmdef)。

---

## S13-5 · 新测试自检前置固化为可复用脚本(复盘行动项 4)

### 原判定(病因)
sprint-12 返工期三类自检**散落**成一次性 `python -c` 片段,未固化
(`review-medcons-story-002-2026-10-15.md:375` X-2 · `fix-s11-12-...md:253`)。

### 修复落点
| 文件 | 说明 |
|---|---|
| `tools/test-precheck/precheck.py` | 单一入口:括号配平(C# 感知)/ JSON 合法性 / 断言自相矛盾 / `[Test]` 计数 / 悬空引用(默认关) |
| `tools/test-precheck/README.md` | 「何时必跑」+ 判据 + 证伪轮记录 |

### 卡面判据兑现
> 判据 = 对 `medcons_outcome_judge_test.cs` 跑 ⇒ 结论与返工期实测四项一致

```
python3 tools/test-precheck/precheck.py unity/Assets/Tests/EditMode/MedConsequences/medcons_outcome_judge_test.cs
→ [OK] [Test]=41  ·  FAIL 0 · WARN 0  (退出码 0)
```
括号配平实测:花括号 168/168 · 圆括号 938/938 ✅

### 可证伪判据(三类错误各注入一个 ⇒ 须转红)
| 注入 | 实测 |
|---|---|
| ① 删一个配对 `)` | `[1] 括号配平:第 141 行 '}' 与第 134 行的 '(' 不匹配` ✅ |
| ② JSON 删一个 `}` | `[2] JSON:第 8 行第 1 列:Expecting ',' delimiter` ✅ |
| ③ `AreEqual(0, 1, "漏发应为 1 条")` | `[3] 断言自相矛盾:第 3 行:断言期望值 0,而消息明确写出 [1]` ✅ |
| 误报侧:4 件合法件 + 全 MedConsequences 目录批扫 | `FAIL 0 · WARN 0` ✅ |

### ⚠️ 首版误报如实登记
首版 [3] 按「消息里出现任何 `= N` / `档 N`」判 ⇒ **误报 4 条假红**
(消息里的 `= 0` 指代别的量,如「漏发 = 0」)。**假红比漏报更糟** ⇒
收紧为「只认明确措辞 + 单条断言语句内」。此项为**自查发现的缺陷**,非原判定。

---

## S13-6 · 重评专家 prompt 拆小(复盘行动项 3)

### 原判定(病因)
sprint-12 重评轮两位专家(unity-specialist / qa-tester)分别撞 **20 / 10 轮上限**未出报告,
须 `SendMessage` 续跑才交付(`retro-sprint-12-2026-10-16.md:80-81 / :91`)。

### 修复落点
`.claude/skills/code-review/SKILL.md` 新增 **§Phase 7b:Agent Briefing Discipline** ——
① 首轮「先出报告再补查」预声明模板(≤12 次工具调用硬预算 + 「未及核查项」节);
② 重评轮拆两 agent(「原判据核账」/「新缺陷 + 硬约束」,并行)。

### ⚠️ 卡面判据:前瞻性,当时不可自证
> 判据 = 下一次重评两位专家均在轮次上限内交付报告

本判据只能在**下一次真实重评**时验证。**本轮不得声称已通过**(禁借绿)。
本节的**存在性**可证伪(改本节 ⇒ skill 行为变),但**有效性**待下次重评实测。

### 🔴 2026-10-17 核对补记:**首次实战实测 = 红**
下一次重评即 story-003 评审,`review-medcons-story-003-2026-10-16.md:20-22` 记录:
`tools-programmer` 撞 **20 轮**上限零报告、`qa-tester` 撞 **10 轮**上限零报告,靠「续派 + 只要报告」才交回
⇒ **卡面判据未兑现**(拆小 / 预声明**不足以**保证限内交付)。
⚠️ 本条为核对时追记,**不追认**原判定;sprint-13 收口记录中的对应勾已撤回(改记「收口存疑」)。
**残留归处**:`docs/tech-debt-register.md` `TD-s13-01`。

---

## S13-8 · 9 条 Minor 入册 + 重评件 §五 收口

### 修复落点
| 文件 | 改动 |
|---|---|
| `docs/tech-debt-register.md` | **新建** —— 登记 TD-medcons-01…15 |
| `production/qa/evidence/review-medcons-story-002-rework-2026-10-16.md` §五 | 加收口回指(原文不删) |

### 判据兑现
> 判据 = 逐条带出处(重评件 §五 条目号),**9 条全在**

- TD-medcons-01…09 = story-002 重评 §五 9 条,**逐条带出处与触发条件** ✅
- 另并入 story-003 评审 6 条(TD-medcons-10…13 B-W1..W4 + 14/15 W-5/W-4)。
- ⚠️ 卡面写「9 条」,**实登 15 条** —— 超出部分如实登记,**不冒称**。

---

## 全量回归(sprint-13 收口)

| 轮次 | Passed / Failed / Skipped | 证据 |
|---|---|---|
| story-003 收口后 | 3508 / 0 / 46 | `unity/Logs/s003-final-full.xml` |
| **sprint-13 治理件后** | **3515 / 0 / 46** | `unity/Logs/s13-full.xml` |

**增量**:+7(S13-4 新增 7 测试)⇒ **+7 / +7,零回归**。

## 禁借绿登记
- **IL2CPP 三格(ADR-012 矩阵)本 sprint 仍 NOT-RUN** —— 本机 Mono 侧 + 负夹具。
- **S13-6 判据前瞻性,不可自证**。
- **S13-7 需真人三签**,agent 不可代劳 —— 仍 backlog。
