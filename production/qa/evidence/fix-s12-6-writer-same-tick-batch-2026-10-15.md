# 夹具补齐证据 —— S12-6:写者侧「同 tick 批量跨阈值」

- **对象**:`unity/Assets/Tests/EditMode/CaseSystem/pattern_recognition_writer_test.cs`(扩 7 条)
- **日期**:2026-10-15 · **Sprint**:12 · **执行者**:Claude
- **基线三元组(S12-0 规则)**:截至 **2026-10-15 09:58 UTC** · 批次 **sprint-12 · S12-6 全量门** · commit **`a5ef7c5`**
- **前置基线**:S12-2 收口后的 `3394 / 3348 / 0 / 46`

---

## 一、缺口来源(具名登记,非本件自述)

`production/qa/evidence/fix-s11-17-case-system-advisories-2026-10-15.md` §六②-5 原文:

> 写者 `anchor_case = MemberSet[末位]` 在「同 tick 批量跨阈值」下**未证伪** ——
> **检测器侧**由 TA-7 新夹具覆盖该维;**写者侧仍无**同 tick 批量夹具 ⇒ 如实登记为残余。

同件 §170 残余表:

> | 2 | **写者侧「同 tick 批量跨阈值」夹具** | **仍开放** | 检测器侧已由 TA-7 新夹具覆盖;写者 9 测仍均逐 tick 递增。低置信、非阻塞 |

**复核确证**:既有 9 测的 `AppendClosed(ps[i], 1…6, …)` 全部**逐 tick 递增**
(见 `test_patternRecognized_realBag_writesOneEvent_roundTrip` 等),同 tick 批量这一维在写者侧零覆盖。

**分层边界(防重复劳动)**:TA-7 的 `test_ac3734_sameTickFourthCase_membersAreFirstThreeInFullOrder`
验的是**检测器判定**;本件验的是**写者发射**(真装配 + 真流 + 真编码器)。两者不得互相顶替。

---

## 二、新增 7 条夹具(判据真源 = GDD `case-system.md:1143` AC-37-29)

| # | 测名 | 判据 |
|---|---|---|
| 1 | `test_patternRecognized_sameTickBatchOverThreshold_writesExactlyOne` | 同 tick 5 例 ⇒ 恰 1 条;anchor = 全序第 3 例 |
| 2 | `test_patternRecognized_sameTickBatchRepeatCall_firesOnce` | 同 tick 重复调用 ⇒ 零新发射(幂等分支) |
| 3 | `test_patternRecognized_sameTickBatchMoreCases_noResend` | 同 tick 后续病例 ⇒ 永不重发;member_set 冻结 |
| 4 | `test_patternRecognized_sameTickExactlyThreshold_writesOne` | 同 tick 恰 1 例不发射 / 恰 3 例发射(边界下沿) |
| 5 | `test_patternRecognized_sameTickMultiDisease_writesOneEach` | 同 tick 两 D ⇒ 各一条,按 salted_key 升序 |
| 6 | `test_patternRecognized_chainQuotaSameTickRandomCases_notConsumed` | 链外同 D 病例不消耗链名额(AC-37-29 定义域收窄) |
| 7 | `test_patternRecognitionWriter_thresholdTwo_allowed` | 阈值 = 2 放行(守卫边界上沿) |

### 判别力设计(避免 TB-1 型假绿)

- **测 1 / 4 / 5 / 6**:插入序**刻意逆全序**(如测 1 插入 `[3,5,1,4,2]`,全序 = Patient 升序 `[1,2,3,4,5]`)。
  若夹具插入序 ≡ 期望序,则成员/锚点断言对「有无排序」零判别力 —— 这正是 TB-1 的假绿形态。
- **测 6**:链外病例 `patient_id` 刻意取**更小值**(1 / 2 < 链成员 10/11/12)⇒ 若收窄失效,它们会抢占全序前三。
- **铺流纪律**:同 tick 各测**不经 `SpawnPatients`**(故不 Append `DiseaseOnset`),直接用 `PatientId` 字面量。
  理由:`SpawnNext` 按 `NextPatientId()` 单调发号 ⇒ **无法产出逆全序的插入序**(判别力会归零)。
  全部 `CaseClosed` 用同一 tick ⇒ 流非降不变量平凡满足。此例外已写入文件头 ⚠️ 段。

---

## 三、判据(可证伪)

| 平台 | 退出码 | XML(三元组:2026-10-15 09:58 UTC · S12-6 全量门 · `a5ef7c5`) |
|---|---|---|
| 全量 EditMode(首轮) | **0** | `total=3401 passed=3355 failed=0`(`Logs/s12-6-editmode.xml`) |
| 全量 EditMode(三次反证还原后**终轮**) | **0** | `total=3401 passed=3355 failed=0`(`Logs/s12-6-final.xml`)—— 与首轮**逐位一致** |

> **终轮的意义**:三次反证均在同一命令内原子还原,但「还原是否真的落盘」不能只靠 `diff` 自述 ——
> 终轮全量重跑与首轮逐位一致,是「盘上确为修复态、反证未留痕」的**端到端证据**。

**零回归核算**:基线 `3394/3348/0/46` → `3401/3355/0/46` ⇒ **+7 / +7**,恰为本件新增 7 条,
**零删除零失败**。

**产品码改动 = 零**(`git status --porcelain unity/Assets/Sim/` 无输出)。

---

## 四、证伪轮(S12-4 新规适用 · 三处独立反证)

按 `.claude/skills/story-done/SKILL.md` 的还原纪律:每处反证「**改→跑→还原**」在**同一命令内原子完成**
(带 `trap ... EXIT` 兜底),跑完即 `diff` 校验字节相同 + `grep` 校验探针零残留。

| # | 变异(反证对象) | 结果 | 转红条目(含本件新增) |
|---|---|---|---|
| 1 | 停用 `NextEmissions` 的 `IsFired` 一次性门 | `EXIT=2` · `3401/3349/6` | `…_sameTickBatchMoreCases_noResend` ✱ · `…_sameTickBatchRepeatCall_firesOnce` ✱ + 4 条既有 |
| 2 | 停用 `ComputeCandidateSeq` 的 `candidates.Sort` | `EXIT=2` · `3401/3351/4` | `…_sameTickBatchOverThreshold_writesExactlyOne` ✱ + 3 条既有(检测器侧) |
| 3 | 阈值守卫 `< 2` → `< 3` | `EXIT=2` · `3401/3354/1` | `…_thresholdTwo_allowed` ✱ |

✱ = 本件新增的 7 条之一。

⇒ **三处判据均确能抓到对应变异**,非纸面断言。三次反证后 `RESTORED_IDENTICAL` 均为真、
`REVERT_PROBE` 全仓零残留。

### 分层边界的两处实测证据

- 反证 1(一次性门)令**检测器侧**的 `test_ac3709_…` / `test_ac3729_firesOnce_neverRefires` **同时**转红
  ⇒ 该门是两层共享的,写者侧新增的两条与之同向 —— 但写者侧验的是**真流里的发射计数**,检测器侧验的是**返回值**。
- 反证 2(排序)令**检测器侧**三条转红、**写者侧一条**转红 ⇒ 排序对两层都有影响,
  而写者侧的 `…_writesExactlyOne` 是通过**载荷 round-trip 后的 `AnchorCase`** 断言的(端到端)。

---

## 五、未闭合 / 交接

- **`PatternRecognized` 生产触发点仍未接线**(意图源 = 结案 / 触发尚不存在)——
  承 `PatternRecognitionWriter.cs:41` 的既有登记,归 37 实现轮的接线轮。本件不改变该状态。
- **AC-37-29 的两层闭合判据**:检测器侧(TA-7, S11-9)+ 写者侧(本件)= **两者都绿**。
  该 AC 的证据面自此**两层齐备**;`story-004` 的 §六②-5 残余可销项(见下方回刷)。
- 阈值守卫的**上沿**格由本件补齐(下沿三格 0/-1/1 由 `test_patternRecognitionWriter_thresholdBelowTwo_throws` 覆盖)
  ⇒ S11-17 教训「阈值守卫差一格即假绿」的两侧均有夹具。
