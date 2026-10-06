# 评审报告:prescription-medication story-001

**评审对象**:处方表与本草词表 —— 双表 polarity 硬门
**评审日期**:2026-10-06
**评审人**:结构侧 unity-specialist + QA 侧 qa-lead

---

## 评审结论

**双侧均 NOT APPROVED** — 存在 6 项 BLOCKING 问题(结构 3 + QA 3)。

---

## BLOCKING 问题

### 结构侧

#### B-1. 绑定器不读 `item_database_items.json` — DC-1 / DC-5(覆盖) / DC-7(上界) / AC-11-20 在生产路径上未强制

**文件**: `unity/Assets/Editor.Tools.Bake/PrescriptionActionsBinder.cs`

**问题**: `Bind` 方法只接收 `actionsJson` 和 `lexiconJson` 两个字符串,从未读取 `item_database_items.json`。以下 DC 在 GDD 中标注为「构建期硬失败」,但在生产绑定路径上**完全不校验**:

- **DC-1**: `item_key` 必须是 21a `ItemDef` 闭集的子集 — 绑定器不检查 `item_key` 是否存在于 `item_database_items.json`
- **DC-5(覆盖方向)**: 每味 `category=drug` 的药必须在词表中有一条 — 绑定器只检查词表内部重复,不检查覆盖
- **DC-7(上界)**: `DOSE_BASE <= 65536 * dose_range.hi` — 绑定器只检查 `> 0`,注释说「当前数据集 dose_range = null ⇒ 无 hi 约束」,但这是数据依赖的静默跳过,不是结构性排除
- **AC-11-20**: 词表覆盖所有药 — 绑定器不检查

**修复**: `Bind` 签名加 `itemsJson` 参数,绑定阶段执行跨文件校验。

#### B-2. AC-11-02(零重定义)在绑定器中未实现

**文件**: `unity/Assets/Editor.Tools.Bake/PrescriptionActionsBinder.cs`

**问题**: 注释写着「此校验在烘焙期扫描源文本」,但**下方没有任何代码执行这个扫描**。实际上 AC-11-02 被 `RejectUnknownKeys` 隐式满足(白名单不含 `drug_potency` / `half_life` / `axis_offset` 等字段)。

**修复**: 删除误导注释,改为说明「AC-11-02 由 RejectUnknownKeys 隐式满足」。

#### B-3. 测试用 regex 解析 JSON 驱动断言,而非驱动生产绑定器

**文件**: `unity/Assets/Tests/EditMode/PrescriptionMedication/prescription_tables_test.cs`

**问题**: 测试用 `Regex` 从原始 JSON 文本中提取字段,然后断言提取的值满足 DC。这**完全绕过了生产代码路径**。测试绿不等于生产代码正确。

**修复**: 测试应调用 `PrescriptionActionsBinderProbe.Bind(actionsJson, lexiconJson, itemsJson)`,然后断言返回的 `BindResult` 满足 DC。

### QA 侧

#### B-1. `test_bakeDeterminism_sameInputSameOutput` 是恒真测试

**文件**: `prescription_tables_test.cs:174-187`

**问题**: 该测试只做了两次 `File.ReadAllText` 然后断言两次读取结果相等。这证明的是「文件不会在自己读取自己时变化」,不是「烘焙器对同一输入产出相同字节」。

**修复**: 调用 `PrescriptionActionsBaker.BakeFromRepo(repoRoot)` 两次,比较两次 `BakeOutput.Cooked` 字节数组相等。

#### B-2. `test_dc7_doseBaseWithinUpperBound` 在当前数据集上是空集真空真

**文件**: `prescription_tables_test.cs:116-133`

**问题**: 测试遍历 `ExtractDrugProfiles` 返回的列表,对每个 `doseRange != null` 的药执行上界断言。但当前数据集 `salicylic_acid` 的 `dose_range = null`,循环体内 `continue` 跳过,**零条断言执行**。

**修复**: 在测试内联构造一个带非 null `dose_range` 的夹具(不依赖当前数据集)。

#### B-3. 四条负夹具全部是正向测试的复制品

**文件**: `prescription_tables_test.cs:191-252`

**问题**: 四条负夹具全部读取**真实数据集**并断言数据合法。它们与对应的正向测试逻辑完全重复。真正的负夹具应该**构造违反条件的数据**,喂给校验器,断言校验器**拒绝**。

**修复**: 每条负夹具构造一个最小的违反条件 JSON 字符串,调用 `Bind`,断言抛出 `BakeValidationException` 且 `Errors` 包含对应 DC 编号。

---

## 修复验证

### 修复后测试结果

- filter: `unity/Logs/prescription_tables_fix5.xml` = **12 / 12 passed / 0 failed**
- 全量: `prescription_tables_full.xml` = **2796 / 2749 passed / 0 failed / 46 skipped / 1 inconclusive**

### 修复要点

1. **结构 B-1**: `Bind` 签名加 `itemsJson` 参数;绑定阶段解析 `item_database_items.json` 并执行 DC-1/DC-5(覆盖)/DC-7(上界)/AC-11-20 校验
2. **结构 B-2**: 删除误导注释,改为说明「AC-11-02 由 RejectUnknownKeys 隐式满足」
3. **结构 B-3**: 全部测试改为调用 `PrescriptionActionsBinderProbe.Bind` 驱动生产代码路径
4. **QA B-1**: 烘焙确定性测试改为调用 `BakeFromRepo` 两次比较字节
5. **QA B-2**: DC-7 上界测试内联构造带非 null dose_range 的夹具
6. **QA B-3**: 四条负夹具构造违反条件数据喂给 Binder 断言拒绝

---

## NOT-RUN 声明(禁借绿)

- **DC-2**: action_id 闭集 = 处置注册表全值,取决于 OQ-11-2(枚举定值待数值/内容轮)
- **DC-6**: 依赖 9 侧 NOISE_BAND_9 常量(BL-2, O-11→9)
- **AC-11-07**: 双表 polarity 交叉硬门,依赖 9 侧 disease_registry.json(9 侧未建)
