# 评审原件 — diagnosis-system story-003(F-8.1 可读地板与 F-8.2 精度档槽)

> **对象**:`production/epics/diagnosis-system/story-003-read-floor-and-precision-slots.md`
> **日期**:2026-10-05 · **轮次**:单轮(承「评审只做一轮」)
> **评审方**:结构侧 `unity-specialist` + QA 侧 `qa-lead`(均只读)
> **原判定 → 修复落点 → 验证命令** 三段式(可证伪)

---

## 一、原判定(双代理,只读)

| 侧 | 判定 | BLOCKING | MAJOR | MINOR |
|---|---|---|---|---|
| 结构侧 | CHANGES REQUIRED | 0 | 1 | 5 |
| QA 侧 | CHANGES REQUIRED | 0 | 1 | 6 |

**两条 MAJOR 同源**(AC-8-35 扫描面表述 over-claim + README 账本未补);**零 BLOCKING、零运行期正确性缺陷**。
两侧独立复核均确认:9 条 AC 各有真断言 · 反空转纪律(BakeFails 排他 + 非空集自证 + 内容金标)到位 ·
三处 MUT 证明承重面可证伪 · 4 条 NOT-RUN 诚实无借绿 · 10 夹具零悬空 · Note 6 通道映射接生产路径。

### 结构侧 CHANGES REQUIRED(0 B / 1 MAJOR / 5 MINOR)

- **MAJOR-1** AC-8-35 金标扫描面「覆盖」声明与同族文件自相矛盾(`read_floor_slots_test.cs:550` 写「F-8.1 参数半边从 NOT-RUN 转 covered」,而 `sign_table_test.cs:38-40` 同 AC 写 NOT-RUN)—— 实测扫描面不含曲线系数(住 `assets/data/*.json`,仅 ConfigVersion 覆盖)⇒ over-claim,同一扫描面两文件对同 AC 断言相反 = 禁借绿点名要防的失败模式。
- **MINOR-1** `DiagnosisReadFloorCookedCodec.cs:30-31` `FixedHeadBytes = 36` + 注释「4 个 i64 + 1 个 i32 = 36」双错;实为 `skill_cap i32 + 3× i64 + count i32 = 32`(行为无害:E-13 文本打印错误阈值)。
- **MINOR-2** `sign_table_test.cs:38-40` / README 未记录 story-003 已扩枚举/映射常量面并重钉。
- **MINOR-3** `tests/unit/diagnosis_system/README.md` 缺 Story 003 段(证据台账缺口)。
- **MINOR-4** GDD §F-8.2 回退散文**示例**方向与自身规则相悖(规则「向下」,示例 `sign_rales` 粗→中「向上」;且 `sign_rales` 粗档实有词 ⇒ 示例永不触发)—— **GDD 散文缺陷,非实现缺陷**。
- **MINOR-5** `DiagnosisReadFloorTable.cs:25` `using DaYiJingCheng.Sim.Contracts;` 未使用(死 import)。

### QA 侧 CHANGES REQUIRED(0 B / 1 MAJOR / 6 MINOR)

- **M-1**(= 结构 MINOR-3)README 未补 story-003 段(标称夹具 21 实存 31;无 AC→测映射;未登记 NOT-RUN 面)。
- **m-1** `read_floor_slots_test.cs:368-370` `CollectionAssert.DoesNotContain(states, (SignReadState)99)` 按定义恒真(`Enum.GetValues` 永不返未声明值);且未断「有读数」。
- **m-2** `read_floor_slots_test.cs:424-427` `ReadFloor(s)==ReadFloor(s)` 同 table 同参纯函数必等,非「定表命中」判据。
- **m-3** G-1 反射扫描按精确方法名扫单类型(名字变体/依赖类型逃逸;真守卫在边界门 [D-G1])。
- **m-4** AC-8-7 浮点边界的**代理**判据(反射断参型 int),非 AC 字面用例(测试自陈偏离,有判别力,非恒真)。
- **m-5** AC-8-46「词变粗」子句未证(四档配三档词 ⇒ 满→细同词,数据形状下不可观测)。
- **m-6** story-003 Test Evidence 勾选与 Status 未回填。

---

## 二、修复落点(BLOCKING 判定的修复,逐条;MINOR 一并收口)

| 项 | 落点 | 处置 |
|---|---|---|
| **MAJOR-1** | `read_floor_slots_test.cs`(AC-8-35 段头注 + 测试消息)、`DiagnosisGoldenScan.cs`(金标 doc)、`sign_table_test.cs`(头注)、README(金标纪律段)| 四处统一为**准确边界**:「扫描面 = 前缀 ns **代码常量** + DIAG_TIERS;story-003 已扩**枚举/映射常量**面并重钉;F-8.1/F-8.3 **曲线参数**(`assets/data/*.json` 数据,由 ConfigVersion 覆盖)**仍 NOT-RUN**」 |
| **MINOR-1** | `DiagnosisReadFloorCookedCodec.cs:30-31` | `FixedHeadBytes` 36 → **32**,注释订正为「skill_cap i32 4 + 3× i64 24 + 表长 i32 4 = 32」 |
| **MINOR-2** | `sign_table_test.cs:38-40` 头注 | 登记 story-003 已扩枚举/映射常量面、重钉 `b9354110`→`5bba361c`→`f75a8170`;曲线参数半边仍 NOT-RUN |
| **MINOR-3 / M-1** | `tests/unit/diagnosis_system/README.md` | 追加 **Story 003 段**:AC→测映射(17 行)+ 10 夹具清单 + NOT-RUN 登记(6 条)+ Note 6 义务;订正「21 个」→「31 个(21 + 10)」 |
| **MINOR-4** | `DiagnosisReadFloorTable.cs` `DisplayWord` doc | 加 **GDD 散文勘误**注(示例方向反 + 永不触发,实现以规则向下为准,示例待 GDD 轮)—— **不改 GDD 权威件** |
| **MINOR-5** | `DiagnosisReadFloorTable.cs:25` | 删死 import(`IReadOnlyList` 走全限定名) |
| **m-1** | `read_floor_slots_test.cs` `test_ac89` | 去恒真 `DoesNotContain(...,99)`;改断**枚举值域**(恰 5 成员 + 零 Unavailable/Locked/Disabled/Greyed)+ 五手段**各有真读数**(`DisplayWord` 非 null + State ∈ 字母表) |
| **m-2** | `read_floor_slots_test.cs` `test_g1` | 去同参自等循环;改断**表项 = 存储值**(`FloorAt(s)==ReadFloor(s)`,跨档区分,非退化) |
| **m-3** | `read_floor_slots_test.cs` `test_g1` | 幂名清单扩至 {Pow,Power,Exp,Exp2,Sqrt,Cbrt,Log,Log2,Log10};注释明写「真守卫在边界门,本反射为廉价前置」 |
| **m-4** | `read_floor_slots_test.cs` `test_ac87` 注释 | 明写「代理判据(非 AC 字面);AC 字面用例待 `Precision`(9 侧)落地补」 |
| **m-5** | `read_floor_slots_test.cs`(头注 NOT-RUN + `test_ac846` 注释) | 登记「词变粗」NOT-RUN(四档配三档词,数据形状不可观测;词面粗化归 story 006) |
| **m-6** | story-003 文件 | 收口轮回填(Status / AC 勾选 / Test Evidence) |

### 设计决定登记(收口)

1. **空白档回退方向 = 严格向下**(GDD §F-8.2 规则字面;koplik 粗/中为空 ⇒ 粗/中档读不出,细档起出词)。GDD 散文**示例**(`sign_rales` 粗→中)与之相悖且永不触发 —— 登记为 GDD 散文勘误(待设计轮),实现以规则为准。
2. **金标重钉 `5bba361c` → `f75a8170`**(有意识):源于 MINOR-1 的代码常量修正(`FixedHeadBytes` 36→32,属前缀 ns 代码常量故入扫描面)。**非顺手重钉** —— 由结构侧 MINOR-1 判定驱动,已登记重钉理由。
3. **`diagnosis_read_floor.cooked.bytes` 不入库**(承 story-002 / interaction_kinds 先例):烘焙产物由 `DataBakeMenu` 生成;仅提交代码与作者态种子。

---

## 三、验证命令(可证伪)

```bash
# filter 绿(94 total / 93 passed / 0 failed / 1 skipped)
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.DiagnosisSystem" \
  --output unity/Logs/s003-fix3.xml

# 全量绿(2630 total / 2583 passed / 0 failed / 46 skipped / 1 inconclusive)
unity test unity --mode EditMode --output unity/Logs/s003-fixfull.xml

# MUT-D(DisplayWord 回退下界 i>=0 → i>=1:Skill=0 无词)⇒ 恰 3 红
#   test_ac85_wordPresent_readableIndependent / test_ac88_fallback_neverGoesUp /
#   test_ac89_allRevealMethods_availableAtMinSkill  ← 证 m-1 增强判据承重(旧 DoesNotThrow 版不红)
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.DiagnosisSystem" \
  --output unity/Logs/s003-mutD.xml

# MUT-E(FloorAt 返回 0f 错值)⇒ 恰 1 红
#   test_g1_tableLookup_deterministicAndSelfEvident  ← 证 m-2 增强判据承重(旧自等版不红)
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.DiagnosisSystem" \
  --output unity/Logs/s003-mutE.xml
```

**MUT XML 逐名核对**:`s003-mutD.xml`(failed=3)· `s003-mutE.xml`(failed=1)—— 与上列红名一致;两处均还原后复绿(`s003-fix3.xml`)。

---

## 四、未闭登记(NOT-RUN,禁借绿 —— 覆盖缺口,非安全洞)

- AC-8-F3 的 `≥ Project(σ)` 联动子句(`Project(` 在 `unity/Assets/**.cs` 零命中,归 disease story 004)
- AC-8-9 的 EmitGrowth 实际门控调用(归 story 005)
- AC-8-46 的「词变粗」子句(四档配三档词不可观测;词面粗化归 story 006)+ 病名持久化半边(归 37/story 005)
- 跨会话/跨平台烘焙逐位一致(本 story 只证同进程;跨平台归 AC-8-F5 / story 004 矩阵)
- AC-8-35 的 F-8.1/F-8.3 **曲线参数**半边(参数住 `assets/data/*.json`,由 ConfigVersion 覆盖)
- AC-8-7 的 **AC 字面**浮点边界用例(只判代理;待 `Precision` 落地)
- 边界门 [D-G1]/[D-TREF] 等**未执行**(结构侧静态核对规则,未跑 IL/源扫描)

---

**结论**:0 BLOCKING / 2 MAJOR / 11 MINOR 全部收口,全为文本/文档 + 判据强度,**零运行期改动**;
复跑绿 + 两处新 MUT 证明增强判据承重 ⇒ 判据强度**较原版提升**。转 **APPROVED**。
