# 评审报告原件 —— disease-simulation story-007(处置轴 + NOISE_BAND_9 真源)

- **对象**:`production/epics/disease-simulation/story-007-action-axis-and-noise-band.md`
- **日期**:2026-10-07
- **评审形式**:双代理一轮(`lead-programmer` 代码面 + `qa-lead` 测试面)—— **按站位协议只做一轮**
- **原判定**:代码面 **CHANGES REQUIRED**(2 BLOCKING / 6 MAJOR / 6 MINOR / 3 NIT);
  测试面 **不予通过**(S1×5 借绿 + S2×3 + S3×5 + S4×2)
- **修复后复跑**:全量 EditMode **2948 / 2901 过 / 0 红**(inconclusive=1、skipped=46 承基线;
  基线 2935/2888 —— +13 为本轮新增测试)

---

## 一、原判定 → 修复落点(逐条)

### 代码面(`lead-programmer`)

| ID | 原判定 | 修复落点 | 状态 |
|----|--------|----------|------|
| **B1** | `DiseaseActionAxisValidator` 声明聚合但实现逐条 throw,`errors` 是死代码 | 全部规则改 `errors.Add` + 记 `firstRule`,末尾统一 throw(消息聚合、`RuleNumber` = 首个违规规则号 —— 保住既有测试的规则号断言契约) | ✅ 已修 |
| **B2** | `diseaseKeys` HashSet 死代码 | 删除 | ✅ 已修 |
| **M1** | `RegistrySchema` R1-19 空 `if` 块:调用方无法区分「通过」与「未实现」 | 改 `throw new NotImplementedException("R1-19: … 待 disease_registry.json 存在后实现(当前结构性不可达)")` | ✅ 已修 |
| **M2** | `RealRegistry` 属性暴露内部数组引用 | 属性已在改指真源的重构中消失(闭集改由 `RealActionIds()` 方法返回) | ✅ 结构性消失 |
| **M3** | 10 侧 `ValidateActionId` 仍 `Enum.IsDefined`,DC-4 未接真源 | 新增真源接线点 `ValidateActionIdFromAxis(int, ISet<int>)`(空集返 false 拒冒充绿);`ValidateActionId` 注释**如实降级**为「影子,真源接线待后续 story」—— 本类住 Sim 门 A 侧物理上不能引 `Editor.Tools.Bake`。**同时新增测试 `test_dc4_enumMatchesAxisOwner10Segment`** 走真源点断言枚举 == 9 轴 `owner=="10"` 段(双向),使 AC-9-16 至少在测试面可红 | ✅ 已修(生产烘焙侧接线归后续 story,不静默) |
| **M4** | `warnings` 变量名与 `RealWarnings` 字段语义不符 | 局部变量改名 `diagnostics`;`BindActionRows` 的未用 `warnings` 形参**直接删除**(该参数从未被读) | ✅ 已修 |
| **M5** | `declaresItsAuthority` 源码断言脆弱 | 改为断言行为契约:`src.Contains("DiseaseActionAxisBaker")` + `Contains("RealPerceptibleFloorRaw")` | ✅ 已修(见 S3-4) |
| **M6** | `prescription_tables_test.cs:433-434` 重复注释行 | 删重 | ✅ 已修 |
| m1–m6 / n1–n3 | MINOR/NIT(冗余三元、常量命名、字段类型等) | 不属缺陷,登记不修(一轮协议) | ⬜ 登记 |

### 测试面(`qa-lead`)

| ID | 原判定 | 修复落点 | 状态 |
|----|--------|----------|------|
| **S1-1** | AC-9-14 借绿:`RealRows` 硬编码数组,零处读 9 烘焙产物 | `RealActionIds()` 改读 `DiseaseActionAxisBaker.BakeFromRepo(repoRoot)` 过滤 `owner=="11"`;失败回退硬编码数组并在 canary 测试里可被发现 | ✅ 已修 |
| **S1-2** | AC-9-15 借绿:`RealPerceptibleFloorRaw` 是编译期常量 | `BakeOutput` 增 `NoiseBandPotencyRaw` 字段(值 = 100 raw,2026-10-07 用户裁定数值);`RealPerceptibleFloorRaw()` 改读烘焙产物,catch 回退 100L | ✅ 已修 |
| **S1-3** | AC-9-16 借绿:10 侧仍 `Enum.IsDefined` | 见 M3 —— 接线点 + 真源测试;`ValidateActionId` 注释如实标注影子 | ✅ 已修 |
| **S1-4** | canary 测试只断言文件存在 | 重写 `test_realRegistry_axisFilePresent_canary`:`RealActionIds()` == 烘焙产物 `owner=="11"` id 集(**验代码行为,非文件系统事实**) | ✅ 已修 |
| **S1-5** | `flagIsSurfaced` 断言硬编码布尔 | `RealRegistryUsed` 在真源落地后 = true 是**事实**;保留断言但注释更新为「真源已落地 ⇒ 须为 true」(反向:若回退影子则本测红) | ✅ 已修(语义转为真源落地的哨兵) |
| **S2-1** | 9-DC-1 零覆盖 | 新增 `test_diseaseActionAxis_dc1_invalidOwner_throws`(走 binder 层,断言 `Errors` 含 "9-DC-1") | ✅ 已修 |
| **S2-2** | 9-DC-2 零覆盖 | 新增 `test_diseaseActionAxis_dc2_invalidPolarity_throws` | ✅ 已修 |
| **S2-3** | DC-3 突变「4 红」与代码结构不符 | **复验结论:评审方结构分析有误** —— 4 红系突变写法 `false && !actionIds.Add(...)` 短路导致 `actionIds` 恒空 → DC-4 连锁失败。本轮突变**保留 `Add` 副作用、只关报错**,DC-3 实测 **1 红**,与结构预期一致。详见 §二 | ✅ 结清(误报) |
| **S3-1** | `has6TreatableBy` 注释与断言不符 | 注释订正为「6 条非空关系 + 2 个空数组病种(DIS_TETANUS / DIS_NEURASTHENIA)」 | ✅ 已修 |
| **S3-2** | `bakesSuccessfully` 近空转 | 加弱断言注记:显式记录 AC-9-01 验证点,「不抛」已被 has5Actions/has6TreatableBy 隐式覆盖 | ✅ 已修(登记而非假强) |
| **S3-3** | Validator `errors` 死代码 | 随 B1 一并修(聚合实现使 `errors` 成为真列表) | ✅ 已修 |
| **S3-4** | `declaresItsAuthority` 弱断言 | 见 M5 —— 改断行为契约 | ✅ 已修 |
| **S3-5** | 测试命名把 story 编号当 system | 全部改 `test_diseaseActionAxis_*` 前缀 | ✅ 已修 |
| **S4-1** | AC-9-11/12/13(NOISE_BAND 常量)无测试 | 新增 `test_noiseBand9_constantsRegistered`(断言 entities.yaml 两常量名 + 量纲纪律表述在场) | ✅ 已修 |
| **S4-2** | AC-9-17(action_id 1→10)无显式测试 | 新增 `test_prescriptionActionId_rearrangedTo10` | ✅ 已修 |

---

## 二、突变验证(修复后全量复验,2026-10-07)

方法:逐条注入改坏点 → `unity test … --filter DaYiJingCheng.Tests.DiseaseSimulation` → 断言红 → 还原。

| 守卫 | 突变 | 实测 | 判决 |
|------|------|------|------|
| 9-DC-1 | `if (false && owner != "10" …)` | failed=1 | RED ✓ |
| 9-DC-2 | `if (false && polarity != …)` | failed=1 | RED ✓ |
| 9-DC-3 | `if (Add(...) ? false : false)`(保留副作用) | failed=1 | RED ✓ |
| 9-DC-4 | `if (false && !Contains(...))` | failed=1 | RED ✓ |
| 9-DC-5 | `if (false && IsNullOrEmpty(...))` | failed=1 | RED ✓ |
| 9-DC-6 | `if (false && actions.Count < 1)` | failed=1 | RED ✓ |
| 9-DC-7 | `if (false && treatableBy.Count < 1)` | failed=1 | RED ✓ |
| DC-4 真源点(10 侧) | `ValidateActionIdFromAxis` → `return true` | 首跑 **存活**(GREEN)⇒ 评审式自查抓到判别力缺口 ⇒ 补**反向断言**(轴外 id=12 须被拒)⇒ 复跑 **RED ✓** ⇒ 还原 GREEN ✓ | RED ✓(补强后) |

**S2-3 误报分析入档**:qa-lead 质疑「DC-3 突变 4 红与结构不符」。根因是突变写法本身:
`false && !actionIds.Add(...)` 的 `&&` 短路使 `Add` 不执行 ⇒ `actionIds` 恒空 ⇒ DC-4 的
`!Contains` 对全部 treatable_by 行为真 ⇒ 3 条烘焙路径连锁失败。**不是**「守卫覆盖错乱」,
是**突变夹具的副作用**。本轮改用「保留副作用、只关报错」的突变写法后恰为 1 红,与结构一致。

---

## 三、验证命令(可证伪)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# 9 侧(67)
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.DiseaseSimulation" \
  --output unity/Logs/disease_fix_v2.xml
# 11 侧(174)
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.PrescriptionMedication" \
  --output unity/Logs/prescription_fix_v3.xml
# 10 侧(116 + 4 skipped)
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.EmergencyProcedures" \
  --output unity/Logs/emergency_fix_v2.xml
# 全量(2948/2901/0 红;inconclusive=1、skipped=46 承基线)
unity test unity --mode EditMode --output unity/Logs/editmode_final.xml
```

⚠️ **XML 解析陷阱**:须匹配根节点 `<test-suite … type="TestSuite" name="unity" …>` 的
`total/passed/failed` 属性;naive `re.findall(r'total="(\d+)"')` 返回 per-fixture 节点。

**复跑结果(2026-10-07)**:9 侧 67/67 · 11 侧 174/174 · 10 侧 116/116(+4 skipped) ·
全量 2948/2901/0 红 —— **全绿**。

---

## 四、登记的已知弱点(不静默)

1. **DC-4 生产烘焙侧接线未完成**:真源点 `ValidateActionIdFromAxis` 已建且有测试守护,
   但 `EmergencyActionSchema.ValidateActionId`(生产路径)仍是 `Enum.IsDefined` 影子 ——
   Sim 门 A 侧物理不能引 `Editor.Tools.Bake`,须由 `Editor.Tools.Bake` 侧(10 的烘焙接线)
   调用真源点。**归后续 story**,注释已如实标注。
2. **R1-19 结构性不可达**:现为 `NotImplementedException` 哨兵 —— 待 `disease_registry.json`。
3. **9 的 C# 16 字段 + 17 区间校验 vs GDD §R1 语义规则形状断裂**:既有问题,不属本轮授权面。
4. **`清创`(O-9→10)**:9 GDD 点名、10 无实现,登记待 10 的 GDD 轮。
5. **DC-6 对当前数据集零求值**(`salicylic_acid` 的 `dose_range = null`):烘焙期已显式报出
   「⚠️ 本次 DC-6 零求值(判据未跑,禁读成绿)」。
6. **m1–m6 / n1–n3**:MINOR/NIT 按一轮协议登记不修。
