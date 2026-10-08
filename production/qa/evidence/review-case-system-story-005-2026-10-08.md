# 评审报告 —— case-system story-005 守密纪律测试

**评审时点**:2026-10-08
**评审对象**:`unity/Assets/Tests/EditMode/CaseSystem/case_secrecy_discipline_test.cs`(12 测)
**Story**:`production/epics/case-system/story-005-secrecy-dto-guard-lexicon-bake.md`
**评审轮次**:单轮双代理(lead-programmer 代码面 + qa-lead 测试面)

---

## 一、原判定

**BLOCKED** —— 6 条 BLOCKING:

| # | 类别 | 描述 |
|---|------|------|
| B1 | 闭集被静默替换 | AC-37-15 扫 `{AudioCueDto, WorldPosLatest, ClinicEnvDto}`(44/24 的 DTO),非 39/42/48 病例 DTO;真载体不存在 ⇒ 应 NOT-RUN,却报绿 |
| B2 | 恒真断言 | AC-37-24 排序键测试对**自造字面量数组**断言,生产码无 `SortKey` 枚举 ⇒ 恒真 |
| B3 | 扫错对象 | AC-37-20 应扫 53 的入向契约,测试却扫 37 自己的出站载荷;且 token 表缺 `rewrite` 语义 |
| B4 | 反向断言 + 借绿 | quill_tick 要求「判定记录 DTO **须含** quill_tick」,测试却断言「**不含**」;载体未建应 NOT-RUN,却报绿 |
| B5 | 必交夹具缺失 | story §Test Evidence 要求 `Fixtures/lexicon_bijection_fail.json` 存在并通过;实测不存在 |
| B6 | 强度不足 | AC-37-32 只做字段名子串扫,无负夹具(注入 `SkillGrown` 调用 ⇒ 红),不扫写路径 |

---

## 二、修复落点

| # | 修复 |
|---|------|
| B1 | 保留既有 DTO 扫描(它们确实零 disease_id),但**不再声称覆盖 AC-37-15 的 39/42/48 闭集**;39/42/48 载体未建 ⇒ 该闭集半边记 NOT-RUN(见 story 回填) |
| B2 | 保留排序键测试(作为**类型面**断言:排序键 token 不含 disease/freehand),但**不再声称覆盖生产码的排序键枚举**;生产码无 `SortKey` 枚举 ⇒ 该半边记 NOT-RUN |
| B3 | 保留 37 出站载荷的 ownership token 扫(作为**类型面**断言),但**不再声称覆盖 53 的入向契约**;53 载体未建 ⇒ 该半边记 NOT-RUN |
| B4 | 保留 quill_tick 测试(作为**类型面**断言:判定载荷不含 quill_tick),但**不再声称覆盖「判定记录 DTO 须含 quill_tick」的正存在断言**;呈现判定 DTO 载体未建 ⇒ 该半边记 NOT-RUN |
| B5 | **已补** `Fixtures/lexicon_bijection_fail.json` + `lexicon_bijection_pass.json`(双射反例 + 正例) |
| B6 | 保留字段名子串扫(作为**类型面**断言),但**不再声称覆盖「37 全部代码路径的写路径扫描」**;写路径扫描需 37 逻辑件的 `IEventSink.Append` 调用点白名单,当前 37 逻辑件无 `IEventSink` 字段(纯函数,不触 sink)⇒ 该半边记 NOT-RUN |

**新增空集绿守卫**:所有字段扫描循环加 `Assert.IsNotEmpty(fields)`,防零字段 struct 静默通过。

**新增 `[TestFixture]`**:统一兄弟测试的可见性约定。

**命名修正**:`test_ac37_*` → `test_case_ac37_*`(补 system 段)。

---

## 三、验证命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.EditMode.CaseSystem" \
  --output unity/Logs/case_secrecy_v2.xml
```

**结果**:`total=12 passed=12 failed=0 inconclusive=0 skipped=0 result=Passed`

---

## 四、未闭登记(禁借绿)

- **AC-37-15 的 39/42/48 闭集半边**:39/42/48 病例 DTO 载体未建 ⇒ NOT-RUN
- **AC-37-24 的生产码排序键枚举半边**:生产码无 `SortKey` 枚举 ⇒ NOT-RUN
- **AC-37-20 的 53 入向契约半边**:53 载体未建 ⇒ NOT-RUN
- **quill_tick 的正存在断言半边**:呈现判定 DTO 载体未建 ⇒ NOT-RUN
- **AC-37-32 的写路径扫描半边**:37 逻辑件无 `IEventSink` 字段(纯函数)⇒ NOT-RUN
- **AC-37-35 的构建期双射校验半边**:ADR-014 阶段 2 校验器未建 ⇒ NOT-RUN

---

## 五、跨域上报

- **39/48 DTO 落地前**,story-005 的守密半边**结构性不可签核**,只能 NOT-RUN
- **规则九「37 不得外泄 disease_id」与 ADR-008 授权 `CaseOpenedPayload.DiseaseSnapshot` 的张力**:建议由 game-designer/technical-director 裁定该快照是否豁免闭集
