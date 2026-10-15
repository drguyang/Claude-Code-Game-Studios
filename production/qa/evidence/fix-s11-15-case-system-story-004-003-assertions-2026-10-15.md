# 修复证据原件 — case-system story-004 / story-003 断言形态

**S11 编号**:批 2(承 2026-10-15 范围变更 · A 组 A9–A14 · B 组 B1 / B4 / B6)
**对象**:`case-system` story-004(`TB-1` / `TB-2` / `TB-3` / `CB-3` / `TA-6` / `CB-1`·`TB-4` / ⑥)
· story-003(A-5 / ② B2 / ③ B3)
**日期**:2026-10-15
**触发裁定**:用户 2026-10-15「在 sprint-11 完成这些,不带入下个 sprint」
+ 三项子裁定(「解冻写者」/「裁『不进流』+ 改判据」/「授权改 GDD」)
**性质**:BLOCKING 级判定的闭合件 ⇒ 含 **原判定 → 修复落点 → 验证命令**(可证伪)

---

## 一、原判定(引补评原件)

### 1.1 story-004 —— `review-case-system-story-004-retro-2026-10-15.md`

> **① AC-37-27 定序判别力为零**:实现 `memberSet` 按 `Dictionary` 迭代序拼装(`PatternDetector.cs:104`),
> 而测试夹具的**期望序 ≡ 插入序** ⇒ **删掉 `:121-126` 的 Sort,断言仍绿**(可捕获性判定 = **不可捕获**);
> **② AC-37-09/10 成员身份与冻结内容不可证伪**(只断 `MemberSet.Count` ⇒ 取「后 3 例」的变异同样绿);
> **④ 发射语义无生产载体(借绿)**。

A9–A13 逐项:`TB-1`(期望序 ≡ 插入序)· `TB-2`(只断 Count)· `TB-3`(AC-37-12 真空夹具)·
`CB-3`(**产品代码**:`memberSet` 按 `Dictionary` 迭代序 + 扁平结构)· `TA-6`(全文件输入序 == 期望序)。

### 1.2 story-003 —— `review-case-system-story-003-retro-2026-10-15.md`

- **② 快照半场零行为覆盖**(5 处反射字段存在性检查 = 借绿;**生产侧无快照计算**)
- **③ AC-37-18 夹具不含死亡输入** ⇒ 恒真
- **A14 / A-5** 副作用序列三件中两件在 37 侧无判据(⚠️ 该面 §七·一 已推翻 —— 判据在 8 自己的件里,
  A-5 为**同一命题的另一半**)

---

## 二、修复落点

### 2.1 story-004 —— 测试件 + 一处产品代码

**改动面**:`unity/Assets/Sim/PatternDetector.cs`(**产品代码**,仅 CB-3)+
`unity/Assets/Tests/EditMode/CaseSystem/case_pattern_detection_test.cs`。
**测试数 30 → 34**(+4 新增 · 0 删除 · 4 条重命名)。

#### `TB-1` — 期望序 ≡ 插入序

三处夹具改为**插入序 ≠ 期望序**:夹具前提自检(`salted(2) > salted(1)`,令提取器返 `[2,1]`)
+ AC 后半句「换插入序重跑 ⇒ 事件流逐位相同」(两个流插入序相反)+ 新增一测钉「定序**键**是
`salted_key` 而非 `ordinal`」(夹具 `D ∈ {0,1,5}` ⇒ 期望序 `[5,0,1]` ≠ ordinal 序 `[0,1,5]`)。

> ⚠️ 该新增测是**超出字面清单**的一处补强,理由:原 `TB-1` 修法只能证「有排序 vs 无排序」,
> **不能**证「排序键正确」—— 变异 M6(改按 ordinal 排)在 `D=1/2` 夹具上**零差异**(实测确认)。

#### `TB-2` — 只断 `MemberSet.Count`

三处一律改断**逐位内容 + 次序**:
AC-37-09 用**逐 tick 递增**4 例(令「前 3」`{(100,1,0),(200,2,0),(300,3,0)}` ≠
「后 3」`{(200,2,0),(300,3,0),(400,4,0)}`);
AC-37-10 用**同 tick**(流内发出序 `(3,1,2)` ≠ 全序 `(1,2,3)`)。

#### `TB-3` — AC-37-12 真空夹具

改**恰达阈值数**的 3 例纯伤情案(+「2 真病例 + 3 伤情案」混合夹具)⇒
若伤情案被计入任一病种组,**计数即达标 ⇒ 转红**。

#### `CB-3` — 扁平 `memberSet`(**产品代码**)

**先查后动(消费者调查)**:全仓 grep 确认 `PatternDetectionResult.MemberSet` 的
**唯一消费者 = 本测试件**,**生产侧零消费者**(唯一调用点 `PatternRecognitionWriter` 只读 `.Fired`)。

⇒ **删除** `PatternDetectionResult.MemberSet` 字段(结构收窄为 `{ Fired, FiredDiseases }`)
+ 删除 `Evaluate` 里拼 `memberSet` 的循环(`PatternDetector.cs` 原 `:201-217`)。
该循环**正是**「`foreach (var kvp in candidateSeq)` + `Dictionary` 迭代序」的违规点。

⇒ **CB-3 的两半同批消解**:①(迭代序)随违规循环消失;②(扁平结构)随字段消失。
逐 D 归属从此**只有** `PatternEmission.MemberSet` 一条路径(正是 registry
`payload_schema.member_set` 要求的**逐事件**形状)。

#### `TA-6` — 元规则

文件头(`:23-49`)写入**元规则**:定序类 AC 的夹具必须至少一处「输入序 ≠ 期望序」;
判别力判据固定为「**把对应的实现删掉 / 改错 ⇒ 该测试转红**」;并列出满足该纪律的 7 条用例
与各自可捕获的变异。

> ⚠️ **结构性事实(已写入文件头)**:逐 tick **严格递增**的夹具**不可能**满足该纪律 ——
> 全序键含 `Tick` ⇒ 全序 ≡ 流内序。故所有「逐 tick」用例的次序判别力**只能**由**同 tick** 夹具承担。
> (不满足纪律但确有判别力的用例已逐条注明其判别**轴**,不静默。)

#### `CB-1` / `TB-4` — 发射语义无生产载体

用户裁定「解冻写者」⇒ `Sim/PatternRecognitionWriter.cs` 落地(9 测)。
**详见本批另一件**:`fix-s11-13-case-system-writers-2026-10-15.md`。

#### ⑥ — F-37.1b 注块「先触发歧义」悬垂

**GDD 就地订正**(`case-system.md` §F-37.1b 注块):原写「登记为澄清项,**归 37 实现轮**」
⇒ 更新为 **✅ 2026-10-15 已闭合**。依据:该裁法**确已实现** ——
`PatternDetector.ComputeCandidateSeq` 第 **3b** 步(源码 `:351-358`):
「命中链的病种:候选**只留链成员**(自然病例既不计数也不占名额);未命中链的病种:维持 F-37.1 原算式」
(`ChainEligible.TryChainOf`)。

### 2.2 story-003

#### ② B2 — 快照半场(由写者闭合)

详见 `fix-s11-13-case-system-writers-2026-10-15.md` §2.2(`CaseCloseWriter` 自算 `treated` 窗口证据)。

#### ③ B3 — AC-37-18(**本批就地闭合**)

**(a) GDD 收窄**(`case-system.md` AC-37-18 行内加注,**原文一字保留**):
病人死亡**不进事件流** ⇒ 37 对它**结构性不可见**,原「死亡输入」**不可表达**(该事件不存在)
⇒ 本 AC 的可测内容收窄为 **37 无任何自动结案路径**(结案唯一出口 = 玩家显式动作,GDD §规则六)。

**(b) 夹具判别化**(`case_close_precondition_test.cs:459`):
流内由「只造一条 `CaseOpened`」改为**塞满各式非 `CaseClosed` 事件**
(`DrugTreatmentApplied` / `EmergencyTreatmentApplied` / `CareApplied` / `InjuryOnset` /
`JudgmentRecorded`)+ 断言「案件仍开着」。

**(c) 可捕获性已实证(变异探针,非推断)**:

```bash
F=unity/Assets/Sim/CaseCloseDecider.cs
cp "$F" /tmp/CaseCloseDecider.orig.cs
sed -i 's/else if (e\.Kind == EventKind\.CaseClosed)/else if (e.Kind == EventKind.CaseClosed || e.Kind == EventKind.CareApplied)/' "$F"
# 跑 -testFilter test_ac3718_caseOpen_canClose
cp /tmp/CaseCloseDecider.orig.cs "$F"    # 原子还原
git diff --stat -- "$F"                  # → 空
```

变异体(「**治疗即自动结案**」—— 正是本 AC 要防的缺陷类)
⇒ `total=1 passed=0 failed=1`,**退出码 2**,**被抓住**;探针后已原子还原(`git diff` 为空)。

#### A14 / A-5 — 结案副作用序列

补评 §七·一 已推翻同面的 B4;A-5 是**同一命题的另一半**,逐项复核(全部**亲自打开文件**确认,
非转述)结论一致 —— 三件的判据**均在**,只是不在 37 的证据文件里:

| A-5 的子命题 | 判据所在 | 结论 |
|---|---|---|
| →8 读数清除 | `EditMode/DiagnosisSystem/reading_state_test.cs:409-411` —— `OnCaseClosed` 对 `SignReadState` **全状态遍历** ⇒ `Blank` | ✅ 在 |
| →53 判断冻结 | 同文件 `:396` `FrozenJudgment.Frozen` + `:399` `HandoffTo53` + `:404-406` 「冻结后落笔必拒」竞态断言 | ✅ 在 |
| 「37 自身不产生后果事件」 | `EditMode/CaseSystem/case_secrecy_discipline_test.cs:240-273` `test_case_ac37_20_noConsequenceOwnership`(5 个出站载荷逐个反射扫越权语义 + **空集绿守卫**) | ✅ 在 |

⇒ **A-5 结案,不补测试**(避免与 8 的件重复)。

> ⚠️ **残余(非 A-5)**:37 的结案 → 8 的 `CloseCase` 的**跨系统因果链在 P0 无载体**
> (该接线尚未存在)⇒ 8 侧判据是**纯函数层**判据,**不是**端到端判据。

---

## 三、变异探针矩阵(story-004)

以 **dotnet 行为探针**复刻每条夹具并**直接调生产** `PatternDetector`,与基线逐行 diff:

| 变异 | 内容 | 结果 |
|---|---|---|
| M1 | 停用 `SortBySaltedKey` | **红** ✅ |
| M2 | 成员取 `candidates[count-threshold..]`(后 N 例) | **红** ✅ |
| M3 | 删 `candidates.Sort` | **红** ✅ |
| M4 | 空 `disease_set` 也计入 D0 | **红** ✅ |
| M5 | 删一次性门 | **红** ✅ |
| M6 | 改按 ordinal 排序 | **红** ✅ |

**6/6 全部至少翻红一条断言。**
⚠️ 探针是**独立复刻夹具 + 直接调生产**,**不是** UTF 执行;「34/34 绿」由本轮全量测确认。

---

## 四、验证命令(可复跑 · 可证伪)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
git diff --stat -- unity/Assets/Sim/PatternDetector.cs \
                  unity/Assets/Tests/EditMode/CaseSystem/case_pattern_detection_test.cs \
                  unity/Assets/Tests/EditMode/CaseSystem/case_close_precondition_test.cs \
                  design/gdd/case-system.md
grep -c '\[Test\]' unity/Assets/Tests/EditMode/CaseSystem/case_pattern_detection_test.cs   # → 34

# CB-3:扁平字段已删(PatternEmission.MemberSet 是**逐 D**结构,仍在,正确)
grep -n "class PatternDetectionResult" -A6 unity/Assets/Sim/PatternDetector.cs

# ⑥:GDD 注块已不再悬垂
grep -n "先触发歧义" design/gdd/case-system.md
```

全量门(2026-10-15 实测 · 双信号一致):EditMode `3387/3341/0/46` 退出码 **0** ·
PlayMode `186/186/0` 退出码 **0** · `error CS` = **0 / 0**。

---

## 五、⚠️ 一处越权改动 —— 已按用户裁定回退

**事实**:story-004 轮的实现过程曾自行把 `SortBySaltedKey` 由**偏序改全序**
(主键 `salted_key`,相等时以 **ordinal 次级定序**),并在文档注释中自陈引
「**GDD F-37.1「salted_key 相等……以 ordinal 次级定序」**」。

**经复核,引据不成立**:
- GDD `:828-830` 的裁定原文只写「`PatternRecognized` **按 `salted_key` 的整数升序**依次写入
  (`Seq` 随之 0,1,2…)」;**全文无 ordinal 次级键**;
- 该改动**无夹具可证伪**(作者自陈「需要制造 64 位哈希碰撞」);
- 它**超出本批 20 条清单**。

**处置(用户裁定 2026-10-15「回退这 3 行」)**:已回退
⇒ `PatternDetector.cs` 的 `SortBySaltedKey` 与 `HEAD` **逐字一致**
(`grep "ordinal 次级|a.CompareTo(b)|全序,非偏序"` 全空)。

**回退的实证**:回退前后 EditMode / PlayMode 测数**逐字不变**
⇒ 该改动在**全部夹具上行为不可观测** —— 这正是它「无夹具可证伪」的直接证据。

⚠️ **未处理的相邻 ADVISORY(如实登记,不冒充已决)**:
`SortBySaltedKey` 用 `List.Sort`(**不稳定**排序),主键相等(64 位哈希碰撞,概率极低但非零)时
次序**未定义** ⇒ 理论上与 AC-37-27 后半句「换一个插入序重跑,事件流逐位相同」有张力。
**是否需要次级键是设计决策**,须用户 / ADR 裁 —— 本轮**不自行加**。

---

## 六、未闭合 / 不确定(如实)

1. **A-5 的残余**:37 结案 → 8 `CloseCase` 的跨系统因果链在 P0 无载体(见 §2.2)。
2. **story-004 四条 ADVISORY 未动**(不在本任务清单):`TA-2`(`isFired` 三用例把 `salted_key`
   塞进 `SimEvent.Seq` 自造载体,真载体应是 `PatternRecognizedPayload.SaltedKey` 经 codec 解码)·
   `TA-1`/`TA-5`(AC-37-11 夹具与 AC-37-08 逐字重复;AC-37-14 负断言按**字段名**判)·
   `TA-7`/`TA-8`(AC-37-34 「第 4 例同 tick」边界未覆盖;`PATTERN_THRESHOLD` 两档无构建期门)。
3. **story-003 的留白**:「是否为处置集补一条显式 AC」**仍归用户裁**,本轮未自行加 AC(AC 面归 GDD)。
4. **实现轮未跑 Unity**(按硬约束禁起 batch);全部结论来自 dotnet 编译 + 行为探针。
   本轮全量测由主轮统一执行并确认 0 failure(见 §四)。
5. **并行工作流的树移动**:本批执行期间 `case_identity_routing_test.cs` /
   `case_open_and_judgment_test.cs` 由其他并行轮次改动;本件的两个文件与之**无重叠**。
