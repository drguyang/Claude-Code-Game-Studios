# 代码评审原件 —— clinic-machine / story-001

**日期**: 2026-10-20
**目标提交**: `fb87e2a`(工作树干净)
**评审对象**: 6 个烘焙工具件 + 1 个测试件(29 `[Test]`)+ 15 个夹具
**方法**: 双专家并行只读静态评审(`unity-specialist` 代码/架构面 · `qa-tester` 可测性面);
两位均**未运行 Unity**、未改任何文件。回归数由编排方另行持有,本件不复核其三元组。

**总判定**: **CHANGES REQUIRED**
—— `unity-specialist`: `APPROVED WITH SUGGESTIONS`(0 BLOCKING / 12 WARNING);
`qa-tester`: `GAPS`(3 🔴 / 11 🟡)。合并后 **4 条 🔴**(qa 的 3 条 + unity 的 D9,编排方复判为阻断)。

> 两位口径差异的原因:unity 从「代码是否合规」看,D9 只是**注释与实现不符**(WARNING);
> qa 从「断言是否有判别力」看,\*\*R-08 使 AC 语义层不可判\*\*。编排方把 D9 提为 🔴,理由是本项目
> 已多次被「引用却无实现 / 自述≠代码在修它」咬过,且它是**唯一会让下一个人信任一句假话**的项。

---

## 一、🔴 阻断项(4 条)

### R-08 —— AC-24-05 的语义层未钉(最重)

| 面 | 内容 |
|---|---|
| 现象 | AC 文本写 `K_speed ∈ (0,1]`(连续区间);实现是**离散档集** `{1, 7/8, 3/4}`(`ClinicMachineSchema.IsAllowedKSpeed:137`,由 `KSpeedLiterals` 经 `FixParse` 派生) |
| 实测 | 全夹具 + 种子 + 内联 JSON 的 `k_speed` 取值 = `1` / `0/1` / `1/1` / `7/8` / `3/4` / `5/4`。**没有任何一个落在 `(0,1)` 且不在档集内**(如 `1/2`) |
| 后果 | 把 `IsAllowedKSpeed` 换成 `0 < raw <= 65536` 的纯区间判据,**29 条测试全绿** ⇒ 档集语义**完全不可判** |
| 定位 | `ContextBinder.cs:140-148` · `Schema.cs:137` · 测试 `test:93-118` |
| 修法 | 补一条夹具/内联用例 `k_speed = "1/2"`(∈(0,1] 但 ∉ 档集)⇒ 必拒。这条**同时**把「区间 vs 档集」的分叉钉死 |

> 这不是"AC 写错了"——`OQ-24-1`(2026-09-25)裁的就是离散档集,实现**正确**;
> 缺的是**让实现与 AC 文本的差异可判**的那条测试。

### R-10a / S27 —— 断言命中跨守卫共享 token(F4 同族,第三例)

`test_adj_wmin_abs_gt_wmax_rejected`(`test:205`)只断言 `"W_MIN"`,而该 token 同时出现在
`AssertOverflowBounds` ②段的**三条**报文里(`Schema.cs:193` `W_MIN raw=… 须 < 0` /
`:199` `W_MAX raw=… 须 > 0` / `:206` `界健全性失败:|W_MIN| > W_MAX`)。
夹具 `invalid_adj_wdomain`(`w_min=-1/2, w_max=1/4`)当前只触发第三条 ⇒ **暂未借绿,但不可判别**。
同时该分支**从未单独变异**过(实现证据 §五.1 自陈)。
**修法**:断言钉 `"界健全性失败"`。

### R-17 —— 七类边界有生产守卫、零测试

| # | 边界 | 守卫落点 | 可达入口 |
|---|---|---|---|
| ① | 空表 `contexts:[]` / `rooms:[]` / `pairs:[]` | Context:104 · Room:135 · Adj:116 | `Bind*ForTest` |
| ② | 空 `clauses` 数组 | Room:168 | `BindRoomForTest` |
| ③ | 重复键(Adj 重复 `(a,b)` / Context 重复 `room_type`) | Adj:172 · Context:164 | `Bind*ForTest` |
| ④ | `n_max ≤ 0` | Schema:165 | 直调 `AssertOverflowBounds` |
| ⑤ | `null` 值 | Schema:358(`ReadRequiredFix` Null 分支) | `Bind*ForTest` |
| ⑥ | `w_min == w_max` | Schema:199(`W_MAX 须 > 0` 之外) | `BindAdjForTest` |
| ⑦ | 超长字符串 | 无守卫(见 D8) | — |

全走现有 **public** 字符串入口,零新增钩子。**有测的只有「单元素表」**(内联 JSON 多为单行,隐式覆盖)。

### D9 —— 伪声明:注释声称有控制字符门,实际不存在

`ClinicMachineAdjBinder.cs:106` 与 `:212` 两处写「类型 id 不含 `\u0001`,**构建期另拒控制字符**」,
`Key(a,b) = a + "\u0001" + b` 的单射性就建立在这句上 —— 但**全件零控制字符校验**
(`grep char.IsControl / 控制` 只命中这两行注释)。
**修法**(择一): ① 实现控制字符门(`a`/`b` 拒 `char.IsControl`);② 删掉伪声明并改写单射依据。

---

## 二、🟡 警告项(合并去重后 16 条)

### 2.1 测试判别力

| ID | 内容 | 落点 |
|---|---|---|
| **R-09** | O-24-5 **接受侧判别力≈0**:`_exact_2pow31_minus_one_accepted` 只断言 `errors.Is.Empty`;`_n_max_boundary_floor_accepted` 只断言 `NMax` 等于夹具字面值(只证解析回读)。**删掉 `AssertOverflowBounds` 调用两条仍绿** | test:157,180 |
| **R-10b** | 断言 `"direction"` —— 同 guard 两条报文(`缺字符串 direction` / `未知方向 direction=`)共用 | test:333 · Room:249,257 |
| **R-10c** | 断言 `"ENV_MOD_MIN"` —— **正是 F4 借绿的那个 token**;本夹具常量表合法故未触发 O-24-4,当前不借绿,但同一 token 被再次使用 | test:250 |
| **R-10d** | 断言 `"须为 JSON 字符串"` —— `ReadRequiredString` 与 `ReadRequiredFix` 共用该子串 | test:418 · Schema:336,366 |
| **R-18** | 多错误聚合**顺序稳定性无测**:只断言 `Count ≥ 2` + 两子串,不钉顺序 | test:422 |
| **R-19** | `_furn_membership_enforced_with_injected_source` 断言的是 `FurnMembershipEnforced` **标志**(Room:102-103 独立算出)⇒ 删掉 `AssertFurnMember` 调用该测仍绿;判别力由姊妹测 `test:352` 兜住 | test:337 · Room:230 |
| **R-21** | 弱接受侧:`test_constants_env_mod_min_negative_accepted` 仅 `DoesNotThrow`;`test_bake_from_repo_seed_tables_accepted` 仅 `Count > 0` | test:213,443 |
| **S23** | **11 条正向接受类未入证伪轮**(实现证据 §五.2 自陈),判别力未经反证 | 见 §四.2 |
| **S24** | 多条正向测只断言 `Count > 0`(R-21 同族)⇒ 语义值全错仍绿 | test:111-118,143-150,286-290,441-454 |
| **S27** | 同 R-10a | — |

### 2.2 生产代码一致性

| ID | 内容 | 落点 |
|---|---|---|
| **D1** | `AssertFurnTableShape` **不拒空表**,而三个姊妹 binder 全有「空表⇒拒」。因 FURN 生产路径挂标(`O-24-1`)故潜伏;**O-24-1 落地前补最省** | Schema:236-298 |
| **D2** | 同一方法**不拒重复 `type`**,三个姊妹 binder 均拒重复键 | 同上 |
| **D4** | `ContextBinder`:`k_speed` 档集外时 `ok` 未置 false,非法行仍入 `rows`。当前**无害**(throw 在 `K_CONTEXT_MAX` 派生之前),但若将来把 throw 降级为告警,非法行会污染派生集 | Context:136-149 |
| **D8** | 字符串字段(`room_type`/`context_id`/`type`/`a`/`b`)只校非空,**无长度上界** | 各 binder |
| **S19** | 三 binder 前言四步(词法→根须对象→`RejectUnknownKeys`→`ReadSchemaVersion`→缺数组早 throw)逐字重复,可抽 `Schema.BeginTable(...)`(不阻塞) | 三 binder |
| **S16** | `ParseClause`(Room:196-277,~82 行)· `AssertFurnTableShape`(~62 行)超 40 行(仓内无明文 40 行规则,仅建议) | — |

### 2.3 实践/承继偏差

| ID | 内容 | 落点 |
|---|---|---|
| **R-13** | 测试专用 `public` 包装 `Bind*ForTest` 进生产装配面(仅转发公开 `Bind`,无碍,但命名把测试意图带入生产 API) | TablesBake:102,106,110 |
| **S25** | 测试读文件系统(`assets/data/` + fixtures),与 `.claude/rules/test-standards.md`「Unit tests must not depend on external state」字面相抵;属本项目既有 EditMode 实践(承 `Audio/settings_exposure_test.cs`) | — |
| **S6** | 承继偏差(非本 story 文件):`JsonStage1Lexer.cs:5-8` 取 `FloatParseHandling=Decimal`,ADR-014 原文钉 `None`(自陈 Newtonsoft 无 None 成员);行为拒收已由阶段2 结构化封住,但字面偏离应登记 | `JsonStage1Lexer.cs` |

---

## 三、🟢 通过项(两位专家一致,关键项)

**ADR-014 合规**
- 两阶段**真分离**:阶段1 = `JsonTextReader` 仅词法 + 自研结构化 DOM;阶段2 = per-schema binder。
  6 件内 `grep JsonConvert|JsonSerializer|JObject|JToken|DeserializeObject` = **NONE**
- `Fix` 字段**强制 JSON 字符串**:`ReadRequiredFix`(Schema:349-385)对非 String token(含 Null)结构性硬拒
- `schema_version`:域检查 **+ 与 `CookedFormat.SchemaVersion` 相等比对**(不止域检查)
- 未知键白名单**单一出处**(Schema:101-134),三 binder 只调 `RejectUnknownKeys`,不自抄
- `ConfigVersion` = FNV-1a 32 over **按 Ordinal 排序**的 (文件名, 内容) 对 ⇒ 与枚举顺序无关

**ADR-006 合规**
- `Fix` 只经 `FixParse.Parse`(默认 `HalfAwayFromZero`,合唯一舍入模式);6 件内 `float/double` = **NONE**
- 唯一乘法 `n_max × tierMax` 有回绕防护(`nMax > long.MaxValue/tierMax`)—— 不让回绕决定判决

**确定性**
- 行序一律 `string.CompareOrdinal` 全序;`seen` 字典只 `ContainsKey/TryGetValue`,**从不迭代出词**
- cooked 头槽写 `bound.SchemaVersion`(非 0);两次同源烘焙逐位一致有断言

**聚合 throw 不漏错(重点核)**
- 三处「缺数组早 throw」均先 `errors.Add` 再 `throw(errors)` ⇒ schema_version / 未知键错一并带出,非首错即停;
  `return null` 只丢行不丢错

**架构**
- `Editor.Tools.Bake.asmdef` = `includePlatforms:["Editor"]` + `autoReferenced:false` ⇒ **不进构建**;
  6 件零 `UnityEngine`;未触任何 sim 写路径

**测试纪律**
- 夹具纪律达标:6 个烘焙件**零写盘 API**(`WriteAllText/Delete/CreateDirectory/StreamWriter` 全空);
  夹具经 `RepoRoot()`(`[CallerFilePath]` 上 5 层)定位,非 cwd 相对
- **零孤儿夹具**:15/15 全被引用
- 钩子充分:全走 public 面,**零反射、零「复制一份逻辑」**
- 无「只断言抛异常不看报文」:所有 `Assert.Throws` 后均跟 `AssertErrorsContain` 或显式 `Errors.Count`
- **未发现「自造断言顶替 AC 语义层」**:断言全落在生产方法的返回/异常上;
  `StubFurnSource` 是**注入的协作对象**,非自造替身

---

## 四、未及核查项(禁借绿)

### 4.1 两位专家共同自陈
1. **未跑任何测试、未运行 Unity** —— 「29 条全绿」与全量 `4041/4005/0/36` 均只引不验;本件不对该三元组背书
2. **`ClinicMachineCookedWriter.cs`(209 行)未逐行读** —— 只从 grep 确认零写盘 API 与「无生产消费者」;
   字节布局 / `ConfigVersion` 写入正确性**未评**
3. **`FixParse.Parse` 本体未读** —— 只核签名与默认 RoundMode;字符串文法(是否接受 `"5/4"`、负号、截断)未核
4. **`assets/data/clinicmachine_*.json` 与 `item_database_constants.json` 内容未自核**(QA 侧读了夹具,unity 侧未读)
5. **读侧镜像 `Gameplay.Presentation.CookedCodec` 未审**(cooked 当前无生产消费者)
6. **IL2CPP 三格 / 跨平台逐位性 NOT RUN**(本机仅 Mono)
7. **`tests/unit/` 账本树是否另载同 AC 活测试未扫**(只审 `unity/Assets/Tests/EditMode/` 树)

### 4.2 明确「未经反证」的测试(11 条,沿用实现证据 §五.2)
`test_cooked_bytes_deterministic_for_same_source` · `test_multiple_errors_aggregated_in_single_throw` ·
`test_bake_from_repo_seed_tables_accepted` · `test_adj_symmetric_pairs_accepted` ·
`test_context_k_speed_boundary_one_accepted` · `test_furn_table_field_spec_accepted` ·
`test_adj_n_max_boundary_floor_accepted` · `test_overflow_bounds_exact_2pow31_minus_one_accepted` ·
`test_constants_env_mod_min_negative_accepted` · `test_adj_wmin_abs_gt_wmax_rejected` ·
`test_room_furn_membership_enforced_with_injected_source`

### 4.3 `AssertOverflowBounds` ②段分支
`|W_MIN| ≤ W_MAX` 分支**从未单独变异**(F3 只停用了 ① 段的 int32 门)⇒ 该分支判别力未经反证。

---

## 五、修复轮建议顺序

| 序 | 项 | 理由 |
|---|---|---|
| 1 | **R-08** | 唯一让 AC **不可判**的项;一条用例即闭环 |
| 2 | **D9** | 伪声明;本项目已知失效模式,成本极低(实现门或删注释) |
| 3 | **R-10a/S27** | F4 同族第三例;一条断言改串 |
| 4 | **R-17** | 七类边界补测,全走现有 public 入口 |
| 5 | **D1/D2** | FURN 形状与三姊妹不一致;**O-24-1 落地前补最省** |
| 6 | **R-09 / R-21 / R-10b/c/d / R-18** | 判别力收紧 |
| 7 | **D4 / D8 / S19 / S16 / R-13 / S25 / S6** | 建议项,可延后 |

修复轮完成后须:① 重跑全量 EditMode(带三元组);② 对**新增/收紧的断言**补做证伪(至少 R-08、R-10a、R-17 各一例);
③ 本件结账表按 **Landed / Proven 双列**填写,`Proven` 列不得写动作描述。

---

## 六、双专家口径差异(留档)

| 项 | unity-specialist | qa-tester | 编排方合并 |
|---|---|---|---|
| D9 | 🟡 WARNING(优先级最高) | 未评(不在其面) | **🔴**(唯一会让人信任一句假话的项) |
| AC-24-05 离散档集 | D13:有意收窄,**符合** Manifest | R-08:**语义层不可判**,🔴 | **🔴**(实现正确,测试缺口) |
| 总判定 | APPROVED WITH SUGGESTIONS | GAPS | **CHANGES REQUIRED** |

---

## 七、修复轮结账(Landed / Proven 双列)

**修复轮**: 2026-10-20 · 前 5 项(R-08 / D9 / R-10a / R-17 / D1+D2)
**新三元组**: `total=4054 passed=4018 failed=0 inconclusive=0 skipped=36` · 截至 `2026-10-20 19:10:23 +0800` ·
批次 `修复轮全量 EditMode` · commit `fb87e2a`+工作树 · 退出码 **0**
**增量核对**: `4054 − 4041 = 13` = `4018 − 4005 = 13` = **本轮新增测试数**(29 → 42)

| ID | Landed | Proven | File(s) | Proven-by |
|---|---|---|---|---|
| **R-08** | ✅ | ✅ | `ClinicMachineSchema.IsAllowedKSpeed` · `ContextBinder` · `tests/unit/clinic_machine/fixtures/invalid_context_k_speed_not_in_set.json` · test | **证伪 G1**:把档集判据换成纯区间 `raw > 0 && raw <= 65536` ⇒ `test_context_k_speed_in_range_but_not_in_set_rejected` **红**。改前该实现变更下 29 条全绿 |
| **D9** | ✅ | ✅ | `ClinicMachineSchema.RejectControlChars`(新)· `ClinicMachineAdjBinder.ParsePair`(建键前调用) | **证伪 G4**:停用控制字符调用 ⇒ `test_adj_type_containing_separator_rejected` **红** |
| **R-10a** | ✅ | ✅ | `clinicmachine_tables_schema_test.cs:205` 断言改 `"界健全性失败"` | **证伪 G2**:停用 `|W_MIN| > W_MAX` 分支 ⇒ `test_adj_wmin_abs_gt_wmax_rejected` **红**。(用改前的 `"W_MIN"` 断言时,该变异体**存活** —— 这就是本条的判别力证据) |
| **R-17** | ✅ | ✅ | 9 条新测(空表 ×3 · 空 clauses · 重复键 ×2 · `n_max ≤ 0` · `w_min == w_max` · `null` 值) | **证伪 G3**(空 `pairs` 门)⇒ `test_adj_empty_pairs_rejected` **红**。⚠️ **其余 8 条未逐条变异**,见 §七之三 |
| **D1** | ✅ | ✅ | `ClinicMachineSchema.AssertFurnTableShape`(空表 ⇒ 拒) | **证伪 G5** ⇒ `test_furn_empty_table_rejected` **红** |
| **D2** | ✅ | ✅ | 同上(重复 `type` ⇒ 拒) | **证伪 G6** ⇒ `test_furn_duplicate_type_rejected` **红** |

### 七之一 · 证伪轮总计
- 首轮 13 个变异体(F1–F13):**13/13 转红**(含 F4 修夹具后、F13 修正靶点后)
- 修复轮 6 个变异体(G1–G6):**6/6 转红**
- 全部「变异 → 过滤跑 → 还原 → SHA-256 校验」;工作树零变异残留

### 七之二 · 修复轮后判定
**🔴 4 → 0**。总判定由 `CHANGES REQUIRED` 改为 **`APPROVED WITH SUGGESTIONS`**。
剩余 🟡 共 **15 条**未修(见 §二):R-09 · R-10b/c/d · R-18 · R-19 · R-21 · S23 · S24 · D4 · D8 ·
S19 · S16 · R-13 · S25 · S6 —— 均为判别力收紧或建议项,不阻断本 story 的「本体交付」。
⚠️ **`story-001` 仍不转 `Complete`**:`AC-24-08` / `AC-24-12` 的闭集半边仍 `BLOCKED-BY-O-24-1`。

### 七之三 · 本轮未及核查项(禁借绿)
1. **R-17 的 9 条新测只抽证 1 例**(G3 空 `pairs`);其余 8 条的判别力**未经反证**。
2. **新测试未覆盖 `assets/data/` 种子的 `k_speed = "1"` vs `"1/1"` 两种写法**在 `FixParse` 下是否同 raw —— 未实跑核对。
3. 修复轮**只跑了 EditMode**;PlayMode / IL2CPP 三格仍 NOT RUN。
4. 剩余 15 条 🟡 **未处理**,其中 **R-09(`AssertOverflowBounds` 接受侧判别力≈0)** 与
   **R-10b/c/d(另三处共用 token)** 与已修项同族,建议下一轮一并收。
