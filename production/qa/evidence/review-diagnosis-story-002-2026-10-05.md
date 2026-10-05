# 评审报告原件 — diagnosis-system story-002(2026-10-05)

> **对象**:`production/epics/diagnosis-system/story-002-sign-lexicon-schema-and-p0-rows.md`
> (体征词条表 schema 与 P0 数据行 · Type: Config-Data · ADR-014/009)
> **协议**:创建并 unity cli 测试 → **双代理评审(单轮)** → 修复 → 复跑绿 → 收口提交推送。
> **评审只做一轮** —— 本文件即该轮记录;修复后**不复评**(承用户裁定),以复跑绿 + MUT 变异证明作结。
> **评审者**:结构侧 / QA 侧两个只读子代理(均未修改任何文件)。
> **原件依据**:`.claude/docs/coding-standards.md` §Review Evidence Standards(BLOCKING 级:无原件不得转 Complete)。

---

## 1. 原判定(单轮双代理,均为 CHANGES REQUIRED · 双侧 **0 BLOCKING**)

### 1.1 结构侧(CHANGES REQUIRED:1 MAJOR + 10 MINOR)

**MAJOR · S-MAJOR 通道「枚举 ↔ 位掩码」两套并存,映射义务零登记 + 数值同形陷阱**
(`DiagnosisSignTable.cs:37-56` vs `Sim.Contracts/VitalsDto.cs:14-21`)
`Diagnosis.SignChannel`(序数 0–5)与 AC-21 位掩码 `Sim.Contracts.SignChannel`(1/2/4/8/16/32)数值**同形不同义**:`Diagnosis.Touch=4` ≡ 掩码 `Posture=4`;`FaceColor=0` ≡ 空掩码;`History=5` 无对应位;8 侧枚举缺掩码第六位 `Wound`。唯一归属声明只在 binder 注释,story-003/004 全文零提及 ⇒ story-003 起任何 `(int)row.Channel` 直接当位用即静默错映,**无构建期断言可抓**。今天无 cast(grep 实证),故是**登记缺口**非活 bug。

**MINOR(10 条)**:
| # | 判定 |
|---|------|
| S-1 | `FixParse` 非 `FormatException` 逃逸聚合(`"1/0"` 抛 `DivideByZeroException`,`catch (FormatException)` 抓不住 ⇒ 丢规则 tag) |
| S-2 | 读方不校验枚举序数与期望 `schema_version`(越界序数静默放行;schema 2 陈旧产物不触发 E-13) |
| S-3 | `count` 未与载荷夹紧即 `new List<>(count)` 预分配(破损产物先巨额分配后 OOM,非 E-13 形态) |
| S-4 | `signs: []` 空表可烘出(空词表静默出货,违背「拒以空集冒充绿」) |
| S-5 | `DiagnosisTuning.SlotBounds` 公开可变数组(消费者可原地改而耦合断言滞后红) |
| S-6 | writer 双 `using` 无 `SignChannel` 别名(日后一写简单名即 CS0104) |
| S-7 | 文件名与类名不一致(无 `DiagnosisSignTable` 类型;story Note 4 点名的类型不存在) |
| S-8 | `BindResult` 公有字段无 XML doc(违 coding-standards) |
| S-9 | `neg_weight` 占位 `"1"` 跨 story 挂账缺失(story-004 消费侧无「禁当终值」登记) |
| S-10 | story 文件 Test Evidence 账目未回填(仍 `NOT STARTED`) |

**结构侧通过面(实证)**:写读镜像 · 校验器唯一调用点 · 门禁合规 · GDD 逐字(20+ 行抽查)· 闭集边界 · 菜单同构 · ConfigVersion 同源 · 前缀零禁则外溢 · 夹具一一对应。

### 1.2 QA 侧(CHANGES REQUIRED:2 MAJOR + 10 MINOR · 「补完 MAJOR 后可转 APPROVED」)

**MAJOR-1 · R-8.2 逐行内容(tier/通道/逐字词)无自动守卫**
阳性 30 行 tier、每通道行数(6/4/7/5/7)、34 行三档词全部未断言 —— 改 sign_rales 的 tier、挪 sign_pallor 通道、打错一个字,31 测依旧全绿。**数据本身是对的**(评审独立核对三方全等),缺的是守卫。

**MAJOR-2 · AC-8-35 的「F-8.1/F-8.3 参数逐位不变」半边无对象也无登记**
F-8.1/F-8.3 参数在代码里不存在(归 story 003/004),金标扫描面不含之 ⇒ 收口勾绿会被读成「F-8 参数也受保护」,实为零覆盖。

**MINOR(10 条)**:
| # | 判定 |
|---|------|
| Q-1 | `Contains("35")` 恒真(校验器消息尾部常驻 `35 = 检验线…` 字面)⇒ 无判别力 |
| Q-2 | addRow「哈希前后相等」同进程恒真且未绑金标;`IsNotNull(cooked)` 近恒真 |
| Q-3 | AC-8-33 P1a 名单 8 值只测 2 值(缺 脉/情志/体质/时序、闻/切) |
| Q-4 | AC-8-32 子句级未覆盖(sign_id 缺失、polarity 非法值、neg_weight 解析失败、空词表) |
| Q-5 | 「恰该条红」为包含式断言,不锁排他(不防未来夹具串味) |
| Q-6 | neg_weight=0 未断言 C-6(missing 断了,zero 没断) |
| Q-7 | 账本 README 缺失 + 头注登记落点悬空(指向不存在的 `tests/unit/diagnosis_system/sign_table_test.cs`) |
| Q-8 | `seed.LastIndexOf(']')` 依赖「词内无 ]」,不证切点是 signs 数组闭括号 |
| Q-9 | `FormatConst` 未覆盖类型回落 `ToString()`(未来带状态 static readonly 会让金标抖) |
| Q-10 | `diagnosis_signs.cooked.bytes` 未生成未入库(item_database 在库 vs interaction 不在库,先例不一致) |

**QA 通过面(实证)**:反空转①②③ · 16 夹具↔测试一一对应零悬空 · AC 矩阵无漏测 · NOT-RUN 诚实性([Ignore] 体真算孤儿)· tripwire 真路径 · 金标实现(FNV-1a/排序/排编译器生成)· 测试规范 · 数据三方全等 · 证据新鲜度(mtime ≤ 测试运行)。

---

## 2. 修复落点(逐条)

### 2.1 结构侧

| 原判定 | 修复落点 |
|--------|----------|
| **S-MAJOR** | ① `DiagnosisSignTable.cs` `SignChannel` 枚举 doc 增「⚠️⚠️ 同名不同物」警示(禁 cast 为掩码位;序数↔位映射表归 story-003);② **story-003 Implementation Note 6** 登记映射表 + 构建期双向断言义务(未立映射前禁消费) |
| S-1 | `DiagnosisSignBinder.ReadNegWeight`:`catch (FormatException)` → `catch (Exception)`(DivideByZero / 超域统一包 `ADR-014·fix-string`,附 S-M1 注释) |
| S-2 | `DiagnosisSignCookedCodec`:新增 `ExpectedSchemaVersion = 1u` 并在 `ValidateHeader` 比对(≠ 即 E-13);`ReadRow` 三处枚举序数界(channel ≤ History / reveal ≤ Inquiry / polarity ≤ Negative,越界 E-13) |
| S-3 | 同文件:新增 `MinRowBytes = 25`,`count × MinRowBytes > 剩余载荷` ⇒ E-13(**先于** `new List<>(count)` 分配) |
| S-4 | `DiagnosisSignBinder.Bind`:`errors.Count == 0` 门内增 `rows.Count == 0` ⇒ `[R-8.1·signs-empty]` 硬失败(空词表 = 以空集冒充绿) |
| S-5 | `DiagnosisTuning`:底层数组收 `private static readonly int[] SlotBoundsStorage`,`SlotBounds` 改 `IReadOnlyList<int>` 只读视图(全部既有用点经参数 `IReadOnlyList<int>` 兼容,grep 实证) |
| S-6 | `DiagnosisSignCookedWriter.cs` 增 `using SignChannel = …Diagnosis.SignChannel;` 别名 + CS0104 关系注释 |
| S-7 | ① `DiagnosisSignTable.cs` 文件头增「文件族命名」注(无同名 C# 类型,六类型清单);② **story Note 4** 点名类型订正为 `DiagnosisSignBinder`(并连带依赖订正,见 2.3) |
| S-8 | `BindResult.Rows` / `.SchemaVersion` 补 XML doc |
| S-9 | **story-004 Implementation Note 7**:占位值预警(禁把 `"1"` 当终值进黄金锚;数值轮回填 + 重签) |
| S-10 | story-002 Test Evidence 回填(见 §4) |

### 2.2 QA 侧

| 原判定 | 修复落点 |
|--------|----------|
| **Q-MAJOR-1** | 新增 `test_r82_contentFrozen_golden` + `BuildContentLines()`:`id\|channel\|tier\|polarity\|reveal\|粗\|中\|细` 34 行 ordinal 排序 FNV-1a,金标 `GoldenContentHash = 3954e294`;漂移 ⇒ `Assert.Fail` 附全部实际行 |
| **Q-MAJOR-2** | 测试头注 NOT-RUN 块增「AC-8-35 的 F-8.1/F-8.3 参数半边 NOT-RUN(story 003/004 落地时扩金标或另立金标)」;story AC-8-35 勾选注释同步写明 |
| Q-1 | `Contains("35")` → `Contains("tier_named=35")`;`Contains("15")` → `Contains("tier_named=15")` |
| Q-2 | addRow 增 `Assert.AreEqual(GoldenConstantsHash, Fnv1aHex(linesBefore))`(绑金标防恒真);legalBaseline 升级为解码 + 2 行 + 首/次行主键 |
| Q-3 | `[TestCase]` 参数化:`test_ac833_p1aChannelValues_dualTagRed`(脉/情志/体质/时序 ×4)+ `test_ac833_p1aRevealValues_dualTagRed`(闻/切 ×2)——连同夹具 舌/望 = 8 值全覆盖 |
| Q-4 | +4 违例夹具:`invalid_sign_id_missing.json` · `invalid_polarity_illegal.json` · `invalid_neg_weight_unparseable.json` · `invalid_empty_signs.json`(各配新测试) |
| Q-5 | 新 helper `BakeFails(json, tags…)`:Throws + **`Assert.AreEqual(1, ex.Errors.Count)` 排他** + 逐 tag 触底;全部负例改经它 |
| Q-6 | negZero 补 `Contains("C-6")`(与 missing 同标) |
| Q-7 | 新建 **`tests/unit/diagnosis_system/README.md`**(真身路径 + AC→测映射 + 21 夹具清单);头注登记落点行改指 README |
| Q-8 | addRow 增切点上下文断言(`']'` 之后 trim 起须 `StartsWith("}")`) |
| Q-9 | `FormatConst` 兜底 `v.ToString()` → `"<" + f.FieldType.FullName + ">"`(状态值不进金标,类型变化仍可证伪) |
| Q-10 | Completion Notes 登记:`diagnosis_signs.cooked.bytes` 不入库(承 interaction_kinds 先例),与 item_database 在库的不一致 **待数据轮统一口径**(登记不擅裁) |

### 2.3 收口时顺手订正的两处 story 文本(实现前对账登记的待办)

1. **依赖订正**:Dependencies / Note 4 原写「复用 disease-simulation story 003 阶段 2 载体」**与仓库实况不符**(disease 侧只有 `Sim/RegistrySchema.cs`,无 JSON/binder/cooked)→ 订正为 `Editor.Tools.Bake` interaction / item 模式(本 story 实际落地路径)。
2. **引用订正**:Note 1 原写「AC-8-F 侧三态可分」→ 正确出处 = **AC-8-21(+ V-8.2 / AC-8-24)**(AC-8-F1…F5 不含三态可分内容)。

### 2.4 修复轮 bootstrap 自查发现(评审外,已修)

P1a reveal 的两个 `[TestCase]` 初值传**裸字符串**(`"闻"`),先撞「reveal_by 须为数组」拿不到 AC-8-33 双 tag —— bootstrap 实测红出后改传数组(`["闻"]`),并在用例 doc 写明教训。

---

## 3. 验证命令(可证伪)

```bash
# ① filter 复跑绿(修复 + 金标钉入后)
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.DiagnosisSystem" \
  --output unity/Logs/s002-run2.xml
# 期望(XML 为准;wrapper exit 0/2 均可):total=67 · passed=66 · failed=0 · skipped=1
# skipped 恰为 test_ac834_reverseOrphan_blockedish([Ignore] NOT-RUN)
# ✅ 实测 2026-10-05:67 / 66 / 0 / 1

# ② MUT-变异证明(校验器唯一调用点承重):临时注释 DiagnosisSignBinder.Bind 内
#    DiagnosisSignTableValidator.Validate(rows); 后复跑同 filter
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.DiagnosisSystem" \
  --output unity/Logs/s002-mut-validate.xml
# 期望:恰 8 条红(校验器路径),绑定层负例与正例全绿 —— 判别力证明
# ✅ 实测恰 8 红:tier35 / tier15 / positiveWithWeight / negativeMissing /
#    negativeZero / duplicateSignId / labRow / historyNotWhitelisted
# 还原注释后复跑 ⇒ 回到 ① 的绿

# ③ 全量 EditMode 复跑绿
unity test unity --mode EditMode --output unity/Logs/s002-full.xml
# 期望:failed=0
# ✅ 实测 2026-10-05:total=2603 · passed=2556 · failed=0 · skipped=46 · inconclusive=1
#    (基线 2561 + 本 story 42 条;skipped 45→46 = +1 反向孤儿 [Ignore])

# ④ 金标引导(收口前已跑,留档):金标置 PENDING ⇒ 3 红并打出实际值
#    constants = b9354110 · content = 3954e294(unity/Logs/s002-bootstrap.xml)
```

**MUT-Validate 的意义**:结构侧 MAJOR/MINOR 反复申明的承重面是「`Validate` 唯一调用点存在」——
注释它 ⇒ **恰**校验器路径 8 条转红、其余全绿,把评审的「删之必转红」断言从推断变为实测。

---

## 4. NOT-RUN / 残余清单(QA 8 项,收口时状态)

| # | 项 | 收口状态 |
|---|----|----------|
| 1 | AC-8-34 **反向孤儿子句**([Ignore],BLOCKED-BY-disease story 006 / TR-diag-013) | **NOT-RUN 维持** —— 头注 + [Ignore] + story AC 注三处显式;附注:「预留」标注机制本身无判据(Ignore 体只断言 empty) |
| 2 | AC-8-34 **正向外键生产接线**(ValidateForwardClosure 仅测试调用,归 disease story 004) | **NOT-RUN 维持** —— tripwire 守接线(9 填 sign_id 即红);机制/红绿路径已验 |
| 3 | AC-8-35 的 **F-8.1/F-8.3 参数半边** | **NOT-RUN 显式登记**(Q-MAJOR-2 修复)—— 头注 + story AC 注;story 003/004 落地时扩金标 |
| 4 | **跨会话/跨平台**烘焙逐位一致 | **NOT-RUN 维持**(同进程双跑已判;承 interaction story-007 口径,测试自注) |
| 5 | AC-8-33「人工核对」半边 | 静态门由闭集 + 种子烘焙间接覆盖;**P1a 名单 8 值负例已补全**(Q-3 修复)⇒ 本项残余收窄为「人工核对」字面(无独立判据,登记) |
| 6 | `diagnosis_signs.cooked.bytes` 产物来源 | **已登记**(Q-10)—— 不入库承 interaction 先例;data-core 预载由「确保组」菜单扫 `*.cooked.bytes`;与 item_database 不一致待数据轮 |
| 7 | 评审报告原件 | **✅ 本文件**(此前不存在 = coding-standards BLOCKING 收口义务) |
| 8 | story 文件自身状态(AC 复选框 / Test Evidence) | **✅ 已回填** —— 7 AC 全勾 + 注释、Test Evidence `[x] PASS` + 四日志路径、Status → Complete、EPIC 行 002 → **Complete** |

另(结构侧 S-9 派生,非 QA 清单):`neg_weight = "1"` 占位 —— **已登记** story-004 Note 7(禁当终值进黄金锚)。

---

## 5. 收口判定

- 双代理单轮评审完成(协议「评审只做一轮」满足);**0 BLOCKING · 3 MAJOR · 20 MINOR** 全部落点(§2)。
- 复跑绿:filter `67/66/0/1` ✅ · MUT 恰 8 红 ✅ · 全量 `2603/2556/0` ✅(日志 = `unity/Logs/s002-*.xml`)。
- NOT-RUN 8 项逐条显式登记(§4),无一借绿。
- **story-002 判 Complete**;本原件 + 四份日志构成证据链。
