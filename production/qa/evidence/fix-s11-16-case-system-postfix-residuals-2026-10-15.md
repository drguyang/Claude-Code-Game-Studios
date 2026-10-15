# 修复证据原件 — case-system postfix 复评残余(批 5)

**S11 编号**:批 5(承 2026-10-15 用户裁定「**本轮跑一次新评审(当下状态)**」所触发的复评残余)
**对象**:`case-system` story-001(**B-1 测试半边**)· story-003(**AC-37-18 另两条恒真测试**)
**日期**:2026-10-15
**触发**:四件 story 的 postfix 复评回件 —— 001 **门不通过**(残余 1 条)· 002 / 003 / 004 **门通过**
(003 另抓一条 ADVISORY)
**性质**:BLOCKING 级判定的闭合件 ⇒ 含 **原判定 → 修复落点 → 验证命令**(可证伪)

---

## 一、原判定(逐字引复评原件)

### 1.1 story-001 —— `review-case-system-story-001-postfix-2026-10-15.md`

> **B-1(AC-37-06 判据算术上不可证伪)→ PARTIALLY-CLOSED**(本轮唯一残余)
> **未做**:retro 的修复原文是「重述判据 **并改打生产载体**」。story-001 测试
> `test_highWaterMark_sentinelMinusOne_noPollution`(`:261-285`)**逐字仍是**测试内
> `Where(...).Select(...).Max()`,`ReconstructPatientHighWater` 在 story-001 面内**仍零调用**
> (唯一真实调用点 = `ItemDatabase/id_authority.cs:168`,归 21a)。
> ⇒ story-001 的 Test Evidence 文件里,**仍有一条 GDD 自认「非证据」的断言被当作判据**。
> **通过条件(复评开出)**:「补上「测试改打 `ReconstructProductionHighWater` + 断『只升不降』」
> 即可转**通过** —— 同型范例已在 `id_authority.cs:163 test_ac21a63_patientReconstruct_ignoresNoneSentinel` 现成。」

**同件另抓**:GDD `case-system.md:1121` ② 称本轮可测半边「**已由 story-001 的测试载体承接**」——
**与代码不符**(写该句时测试尚未改)。

### 1.2 story-003 —— `review-case-system-story-003-postfix-2026-10-15.md`

> **【ADVISORY · 新发现】AC-37-18 的另两条测试仍恒真,且未登记为残余** ——
> `test_ac3718_emptyDiseaseName_canClose`(`:493-501`)与
> `test_ac3718_pureInjuryCase_emptyDiseaseSet_canClose`(`:504-515`)**只调 `Decide` 传字面量**,
> 与 happy path 逐字重复;「病名为空」根本不在夹具里,`emptyDiseaseSet` 从未进入 `Decide`。
> ⇒ **③ 实为 PARTIALLY-CLOSED**。正解应与死亡半边同构:记 **NOT-RUN**(病名是 UI 概念,
> 37 Sim 层结构性不可表达)。**story `:212-254` 的「③ AC-37-18 全部已清」表述过宽**。

---

## 二、修复落点

### 2.1 story-001 B-1 测试半边

**文件**:`unity/Assets/Tests/EditMode/CaseSystem/case_identity_routing_test.cs`
(**单方法改写,1 换 1,测数不变**)。

**改前**(逐字):`events.Where(e => !e.Patient.IsNone).Select(e => e.Patient.Value).Max()`
—— 生产函数零调用,断言在**任何**实现下为真。

**改后**:三处断言,**全部打生产** `IdAuthority.ReconstructPatientHighWater`:

| # | 断言 | 可捕获的变异 |
|---|---|---|
| ① | 真 id 累加 ⇒ `NextPatientId() == 6` | 删 `if (id + 1 > candidate) candidate = id + 1;` ⇒ next 停在 0 ⇒ **红** |
| ② | 全哨兵流 ⇒ `NextPatientId() == 2`(只升不降) | **复合**删 `if (id < 0) continue;` **与** `if (candidate > _nextPatient)` 两守卫 ⇒ candidate 回退到 `-1+1 = 0` ⇒ next 发 0 = **重号** ⇒ **红** |
| ③ | `ReconstructPatientHighWater(new[]{ int.MaxValue })` ⇒ `OverflowException` | 删该 throw ⇒ **红** |

> ⚠️ **两处不可证伪面已逐字写进测试 docstring,不冒充判别力**:
> - 「哨兵不污染 max」**单独**在算术上不可证伪 —— `-1` 是下界,含它与否 `max` 相同;
> - **单独**删 `id < 0 continue`(保留只升不降)**不会红** —— 两守卫单看**各自冗余**,
>   判别力只存在于**复合**变异上。

> 🔴 **本件自身的一处误述 —— 由第三次窄审实测推翻,已就地订正(方向朝上 = 虚报判别力)**:
> §2.1 初稿写「② 全哨兵不回退:**复合**删 `id<0 continue` 与 `if (candidate > _nextPatient)`
> 两守卫 ⇒ candidate 回退 0 ⇒ **红**」。**窄审矩阵 M4 实测:该复合变异下 ② 仍绿。**
> **根因**(窄审给出,本件复核认同):「只升不降」的真正来源是 `candidate` 的**初值**
> `= _nextPatient`(`IdAuthority.cs:93`)与 `if (id + 1 > candidate)` 的**取 max 形式**(`:99`),
> **不是**末尾 `:101` 那道守卫 —— 后者**行为冗余**;`candidate ≥ 0` 恒成立
> ⇒ 同删 `:96` 与 `:101` 后 `candidate` 仍 ≥ 初值 ⇒ ② 仍绿。**只有整函数重写才会红。**
> ⇒ **② 是结构性保留,无单行捕获面**;本稿**单行可捕获的只有 ① 与 ③ 两处,不是三处**。
> 订正已落三处:测试 docstring · `story-001` B-1(b) 块 · `story-001` 收口实证表。
>
> ✅ **窄审的另一条建议已采纳(零成本纯增益)**:① 的输入序由 `[1, 5, -1, 3]` 改为
> **`[1, -1, 5, 3]`**(哨兵前置)⇒ 「`id < 0 continue` 改 `break`」这一**真实缺陷类**
> (截断扫描)由**不可捕获**变为**可捕获**(candidate 停在 2 ⇒ 红);正确实现下仍绿。
> ⚠️ 该建议**不**推翻「哨兵不污染 max 不可证伪」的自陈 —— 补的是**另一条面**。

### 2.2 story-003 AC-37-18 另两条恒真测试

**删除**(`unity/Assets/Tests/EditMode/CaseSystem/case_close_precondition_test.cs`):
两条**均删**,原位留**注块**逐字说明删因与真覆盖落点。

理由(三条,逐条可核):
1. 两条都只调 `CaseCloseDecider.Decide(isOpen:true, isClosed:false, hasTreatmentInWindow:true,
   isChecked:true)` —— 与 happy path **逐字相同**;`Decide` 的形参里**根本没有 `disease_set`**
   ⇒ 该维度在决策层**结构性不可表达** ⇒ 断言**恒真**;
2. `emptyDiseaseSet` 构造后**从未传给任何东西**(只自断 `.Bits == 0UL`);
3. 「病名」是**词表 / UI 概念**(37 Sim 层无病名,只有 `disease_set` 位集)⇒ 该半边语义载体
   **不在本层** ⇒ 登记 **NOT-RUN**。

**真覆盖迁至**(`unity/Assets/Tests/EditMode/CaseSystem/case_close_writer_test.cs`,**新增 1 测**):
`test_ac3718_pureInjuryCase_emptyDiseaseSet_closesWithEmptySnapshot` —— 真装配
(真流 + 真写者 + 真 blob 池),把**空 `disease_set` 真正喂进**生产 `CaseCloseWriter.TryClose`,
再经 `PayloadCodec.TryGetPayload` **回读**快照断言 `Bits == 0UL`。
可捕获:① `TryClose` 加「空 disease_set 拒收」守卫 ⇒ 结果码红;② codec 对空位集编码错 ⇒ 回读红。

> ⚠️ **本测自身曾红一次(如实登记)**:首稿误用 `EventKind.InjuryOnset` 当窗口内处置事件 ——
> 实测该 Kind **不在** `TreatmentKinds` 权威集(`CaseCloseDecider.cs:103-108`,只有
> `EmergencyTreatmentApplied` / `DrugTreatmentApplied` / `CareApplied`)⇒ 结案被
> `RejectNoTreatment` 拒掉、**本测自己红**(全量 `failed=1`,唯一失败即它)。
> 已改为 `CareApplied` 并复跑转绿。**该红是一次有价值的实证**:它顺带证实了处置集的**真源边界**。

---

## 三、登记面订正(与本批同批,防「原件变强了、登记没跟上」)

| 文件 | 原表述 | 订正 |
|---|---|---|
| story-001 `:92` | 「13/13 绿」 | **9 测**(B-4 删 4 条 kindgen 复刻后的陈旧值) |
| story-001 `:99` | 「AC-37-30/06 断言形态错误」 | **story 自定判据**(⚠️ **无 GDD AC 号**)—— 原记是笔误 |
| story-001 B-1 块 | 只写设计侧半边 | 补 **(b) 测试侧**;并留痕「GDD 承接声明**先于代码写出**」 |
| story-003 `:214` | 「③ AC-37-18 **全部已清**」 | **PARTIALLY-CLOSED**(见 §2.2) |
| story-004 `:116` | 「30/30 绿」 | **34**(同 story `:195` 早写「30 → 34」,**同文件内自相矛盾**) |
| story-004 测试件 `:165` | 「写者未落」 | 写者已于 S11-13 落地;本测仍用夹具是为**隔离单测**,真装配载体 = `pattern_recognition_writer_test.cs` |

> ⚠️ **GDD `:1121` ② 未改**(如实说明):该句原文「已由 story-001 的测试载体承接」,
> 在**本批补上代码之后才成立**。本批**不删该句、不粉饰** —— 只在 story-001 留痕其
> 「先写声明、后补代码」的时序。若日后有人读到该句,应连同该留痕一起读。

---

## 四、验证命令(可复跑 · 可证伪)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# B-1:生产载体已真调用(非自造 Max)
grep -n "ReconstructPatientHighWater" unity/Assets/Tests/EditMode/CaseSystem/case_identity_routing_test.cs
grep -c "\.Select(e => e.Patient.Value)" unity/Assets/Tests/EditMode/CaseSystem/case_identity_routing_test.cs  # → 1(仍作入参派生,不再自造 max)

# AC-37-18:两条恒真已删,真覆盖已迁
grep -c "test_ac3718_emptyDiseaseName_canClose\|test_ac3718_pureInjuryCase_emptyDiseaseSet_canClose" \
  unity/Assets/Tests/EditMode/CaseSystem/case_close_precondition_test.cs            # → 0
grep -c "test_ac3718_pureInjuryCase_emptyDiseaseSet_closesWithEmptySnapshot" \
  unity/Assets/Tests/EditMode/CaseSystem/case_close_writer_test.cs                  # → 1

# 处置集真源边界(本批实测到的那个坑)
sed -n '103,108p' unity/Assets/Sim/CaseCloseDecider.cs
```

### 全量门(2026-10-15 实测 · 双信号一致)

| 平台 | 退出码 | XML | 对照(S11-12 后) |
|---|---|---|---|
| EditMode | **0** | `total=3386 passed=3340 failed=0 inconclusive=0 skipped=46` | `3387/3341/0/46` ⇒ **−1** |
| PlayMode | **0** | `total=186 passed=186 failed=0 inconclusive=0 skipped=0` | 逐位一致 |

`error CS` 计数 = **0 / 0**。

> ⚠️ **`−1` 的构成已核**:`case_close_precondition_test` **−2**(删两条恒真)+
> `case_close_writer_test` **+1**(增一条真装配)= 净 **−1**;
> `case_identity_routing_test` 为 **1 换 1**(计数不变)。
> **未靠删测变绿** —— 删的 2 条是**恒真**测试(零判别力),增的 1 条是**真装配**判别测试。

> ⚠️ **一次中间红已如实登记**:本批 EditMode 首次跑 **退出码 2 / `failed=1`**,
> 唯一失败 = 本批新增的那条测试(原因见 §2.2 注)。**修正后复跑转绿**;
> 该红**不是**既有测试的回归 —— 其余 3339 条同期全绿。

> ⚠️ **PlayMode 的运行时点**:PlayMode 于本批**改完 `CareApplied` 之后**编译运行;
> 其后仅剩**注释级**改动(`case_pattern_detection_test.cs:165`)与 story / evidence 文本
> ⇒ PlayMode 结果对当前树有效。

---

## 五、未闭合 / 不确定(如实)

1. ~~**story-001 的 B-1 闭合本身**已交**第三次窄口径评审**~~ ⇒ **已回件,门判定 = 通过**
   (原件 `review-case-system-story-001-b1-postfix-2026-10-15.md`;判定边界 = **只覆盖 B-1 测试半边**)。
   ⚠️ **但窄审同时抓到本件(§2.1)一处「自述 ≠ 代码」并已订正** —— 见 §2.1 末注。
2. **复评登记的其余 ADVISORY 未动**(均非门阻塞):story-001 的 CI job 未在真 runner 跑过 ·
   变异矩阵无常驻执行体;story-003 的 `:110` 恒真断言 · `IsInWindow` 悬垂 · `EmergencyAttempt`
   排除无显式 AC 守卫;story-004 的 `TA-1`/`TA-2`/`TA-5`/`TA-7`/`TA-8` 五条。
3. **两处待裁项仍归用户**:① `SortBySaltedKey` 是否需次级键(不稳定排序 vs AC-37-27 后半句);
   ② 是否为「处置集 == 权威枚举」补显式 AC。**本批未自行裁、未自行加 AC**。
4. **CI 侧**:`.github/workflows/tests.yml` 的 `kindgen-assertions` job 仍未在真 runner 上跑过
   (离线无法核),「失败即红」是**设计判据**非**已观测行为**。
