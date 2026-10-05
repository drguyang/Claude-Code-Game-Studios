# 评审原件 — diagnosis-system Story 004(F-8.3 阴性把握度与不泄漏不变量)

**日期**:2026-10-06 · **对象**:`confidence_leak_test.cs`(39 条)+ 生产六件 + 夹具 14 个
**方式**:单轮双代理评审(承「评审只做一轮」)· **结论**:两位评审均 **无 BLOCKING**

| 评审员 | 面 | 判定 |
|---|---|---|
| A | 正确性与 AC 覆盖(七问逐条) | 无 BLOCKING · 1 MAJOR · 5 MINOR · 4 NIT |
| B | 结构 / 契约 / 门禁一致性 | **APPROVED WITH SUGGESTIONS** · 无 BLOCKING · 1 MAJOR · 3 MINOR · 4 NIT |

> ⚠️ 两位评审均**未实跑 Unity**(无法验证金标哈希与 IL 门真实行为)—— 相关结论在原报告中
> 已自行标注「未核验」;修复后由本会话**实跑**补齐(见 §验证命令)。

---

## 原判定 → 修复落点

| # | 原判定(评审) | 级别 | 修复落点 | 状态 |
|---|---|---|---|---|
| 1 | `test_ac812_negGamma_doesNotMovePositiveFamily` 的承重循环用**阳性极性行**断言 `State ∈ {Positive, UnreadableNegative}` —— 该式对阳性行**恒真**(`Evaluate` 对阳性永不返回 `Negative`),故「仅改 NEG_GAMMA ⇒ 阳性族不变」**无证据** | **MAJOR** | `confidence_leak_test.cs` 该测重写:改证 ①阳性烘焙接缝实参表**无** `DiagnosisNegativeConfidenceTable` 入口 ②阳性族**确实**响应自己的 `READ_GAMMA`(可证伪);删除恒真循环 | ✅ |
| 2 | `Q16One = 65536f` 在 `Table` 与 `Evaluator` **各持一份**(重复魔法数 + D-FIX 谓词绕过面) | **MAJOR**(B) | `DiagnosisNegativeConfidenceTable` 消重:求值器副本删除,唯一换算出口 `public static float RawToFloat(long)`;新增 `test_q16one_matchesFixCanonical` 逐位锚到 `Fix.ToFloat()` / `Fix.FractionalBits` | ✅ |
| 3 | `DiagnosisGoldenScan` 文档称「四个新常量」而实为 **5 行**(`Q16One` ×2) | MINOR | 文档改为「四类常量」(消重后确为 4 行);重钉历史登记 `a7bfec31` → `72db379c` | ✅ |
| 4 | README AC-8-12 行未登记本版新增的方向测 + 未登记「字面形不可表达」的口径替代 | MINOR | README 该行重写,补两条测名 + **口径替代说明** | ✅ |
| 5 | 运行期 / 编辑期镜像(codec ↔ writer)**无机械约束** | MINOR | **登记技术债**(本轮不改):两处手写同序,归后续轮抽共享 `CookedCursor` | 📝 登记 |
| 6 | `ReadSchemaVersion` 只判 u32 域、**不钉版本** ⇒ 源写 `2` 时烘焙成功、**运行期装载**才抛 E-13 | MINOR(A) | Binder 增加 `!= ExpectedSchemaVersion` 判据(与读方**同源**);新增夹具 `neg_conf_schema_version_red.json` + `test_bind_schemaVersionMismatch_red` | ✅ |
| 7 | `test_ac814_noAggregateConfidenceType` 扫描面**无非空守卫**;且只扫字段不扫类型名 | MINOR(A) | 加 `scannedTypes > 0` 守卫;扫描面扩至类型名 / 属性名(**类型名只查聚合 token** —— 裸 `confidence` 是阴性族自己的名词,首次扩面即因此自伤并已订正) | ✅ |
| 8 | `test_ac818_confidenceArgTable_lacksSignValue` 用 `GetMethod("CurveAt") ?? …` —— 左侧恒 `null`(求值器**没有** `CurveAt`),日后加重载会静默逃过 | MINOR(A) | 去 `??` 回退:显式断言求值器**无** `CurveAt` + 定表的 `CurveAt` 只吃 `int` | ✅ |
| 9 | `CurveAt` 对 `null`/空曲线**静默返回 `0f`** ⇒ `C_neg ≡ 0` 使每条阴性「永不构成排除」,把 C-3 死内容报警**伪装成正常值** | NIT(A) | 改**硬失败**(`InvalidOperationException` 带 `[E-13]` 上下文);新增 `test_curveat_unloadedTable_throws` | ✅ |
| 10 | `ReadF32` 不拒 NaN/Inf(「合法长度 + 非法值」窄缝) | NIT(A) | **登记为已知非目标威胁面**:定表值来自有限 `Fix.ToFloat()`,`ReadF32` 消费的是自家 writer 产物;跨端篡改不在威胁模型内 | 📝 登记 |
| 11 | `test_ac810` 的 `lStar > table.SkillCap` 分支**不可达**(`ExclusionLevel` 只返回哨兵或 `[0,SkillCap]`) | NIT(A) | 删不可达分支,改 `Assert.LessOrEqual` 直判 C-3 字面 | ✅ |
| 12 | `test_ac814_multiEvidence_notMerged` 的「调低第三条」比对**按构造恒等**(三条是彼此独立入参) | NIT(A) | 保留演示并**明写其局限**;补真正承重的接口形状断言(求值器**无**吃多行的聚合入口) | ✅ |
| 13 | `test_confidence_clampedToUnitRange` / 端点断言**硬编码合成占位值**,数值轮落定后必红 | NIT(A) | **维持登记**(承 Note 7):合成值即本轮判形状的前提,数值轮后重签 | 📝 登记 |
| 14 | codec 不校验 `skillCap == SKILL_CAP`(60) | NIT(B) | **登记**:姊妹件 `DiagnosisReadFloorCookedCodec` 同形,若采纳须**两处同改** —— 单改一侧反成不对称 | 📝 登记 |

---

## 变异证明(MUT,修复前完成)

| MUT | 改动 | 结果 |
|---|---|---|
| A | 断 `W_j` 权重路径 | **7 红**(负夹具全抓) |
| B | 去把握度 clamp | 首轮 **0 红**(覆盖洞)→ 补 `test_confidence_clampedToUnitRange` 后 **恰 1 红** |
| C | 去 `ConstitutesExclusion` 极性门 | 首轮 **0 红**(种子兜底 `1/2` 使阈值掩盖门)→ 补夹具 `neg_conf_high_fallback.json` + `test_ac815_positivePolarityGate_isLoadBearing` 后 **恰 1 红** |
| D | 去 `ExclusionLevel` 极性门 | **恰 1 红** |

源已 md5 校验还原并复跑绿。

---

## 验证命令(可证伪)

```bash
unity test unity --mode EditMode \
  --filter "DaYiJingCheng.Tests.DiagnosisSystem.ConfidenceLeakTest" \
  --output unity/Logs/story004-fix2.xml          # 39/39 passed, failed=0

unity test unity --mode EditMode \
  --output unity/Logs/story004-fix3-full.xml     # 2669 total / 2622 passed / 0 failed / 46 skipped / 1 inconclusive
```

> ⚠️ CLI 包装器在**绿**时亦打印「测试失败：Unity 进程以代码 2 退出」—— 判决以 XML `failed=0` 为准。

---

## 未闭登记(禁借绿)

- **AC-8-F5 跨平台三格矩阵**(ADR-012)未实跑 —— 本 story 只证 **Mono 侧自洽**。
- **AC-8-16 跨进程半边** —— EditMode 无法起独立进程,只证同进程 N ≥ 10⁴ 逐位相同。
- **AC-8-13 端到端** —— 依赖 9 侧 `Project(Sign_j)` 未落地(归 disease-simulation story 004)。
- **AC-8-35 曲线参数半边** —— 参数住 `assets/data/*.json`,由 `ConfigVersion` 覆盖。
- **`DiagnosisReadFloorBinder.ReadSchemaVersion` 同形缺口** —— 归 story-003 后续轮。
