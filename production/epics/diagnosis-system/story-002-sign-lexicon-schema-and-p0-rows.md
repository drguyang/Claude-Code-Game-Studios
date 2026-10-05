# Story 002: 体征词条表 schema 与 P0 数据行

> **Epic**: 诊断与体征揭示
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Config-Data
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-05

## Context

**GDD**: `design/gdd/diagnosis-system.md`(R-8.1 词条七字段 schema · R-8.2 P0 冻结清单(已过医学校验)· 规则二/三 读数入口与三级结构 · C-6 `neg_weight` 约束)
**Requirement**: TR-diag-011(D-8-4 `Project(Sign_j) → [0,1]` 归 9,✅ 已办)· TR-diag-014(D-8-9 `SLOT_BOUNDS ⊂ DIAG_TIERS` 双向耦合)· TR-diag-015(D-8-10 登记项)· TR-diag-013(D-8-6 共病体征合并,**no-adr-by-design**,归属件 = 9 规则十 + F5)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-014(作者态 `assets/data/diagnosis_signs.json` → 两阶段烘焙 `*.cooked`)· ADR-009(通道位与体征 id 的真源在 9 的 R1 注册表,8 是外键方)· ADR-026(`DIAG_TIERS` **数值归 30 层、义归 8** —— `tier_named ∈ {1,10,20,50}` 的语义由本表消费)
**ADR Decision Summary**: 词条七字段 = `sign_id` / `display_词×3(三档齐全)` / `channel` / `reveal_by` / `tier_named` / `polarity` / `neg_weight`(仅阴性,构建期断 `>0`);P0 阳性约 30 条(面色6/语声4/姿态7/呼吸5/触感7/病史1)+ 阴性 4 条(`lung_clear`/`abd_soft`/`neck_supple`/`no_organomegaly`,`tier_named=Lv20`);`lab_*` → P1a;病史组 `sign_purulent_stool` 的 `channel=问诊`(通道外第二类证据,不计新造通道)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(schema/校验为数据面;管线复用 disease-simulation story 003 阶段 2 载体)
**Engine Notes**: 中文词串进 JSON 须 UTF-8 且烘焙逐位确定(承 ADR-014 确定性要求);词表是**内容**,增员走 `/design-review` 而非实现期私改。

**Control Manifest Rules (this layer)**:
- Required: 七字段完备性 = **构建期硬失败**(throw 级);外键向 9 的 R1 `signs[]` 闭合;`reveal_by ∈ 五值闭合枚举`;`tier_named ∈ {1}∪SLOT_BOUNDS` 且**不得为 35**
- Forbidden: P1a 手段(望/闻/切)进 `reveal_by`;P1a 通道(舌/脉/情志/体质/时序)进 `channel`;`lab_*` 进 P0 表;阳性条目带 `neg_weight` 非空值
- Guardrail: 「加一条体征不动任何公式或全局常量」= 本表的**结构验收判据**(AC-8-35)—— 若加行需要改常量表,说明有该写进逐条字段的量被硬编码了

---

## Acceptance Criteria

*From GDD `design/gdd/diagnosis-system.md`, scoped to this story:*

- [x] **AC-8-32**[L] BLOCKING:R-8.1 字段完备性构建期校验 —— `channel ∈ 五通道`(恰好五个值,无新造)· `reveal_by ∈ 五值闭合` · `display_词` 三档齐全 · `tier_named ∈ {1,10,20,50}` 不得为 `35` 或中间值 · `polarity` 必填 · `neg_weight` **仅阴性非空且 > 0**(C-6;阳性误填应报警) —— ✅ **2026-10-05 PASS**:`sign_table_test` 21 违例夹具(修复轮 +4:sign_id 缺失 / 极性闭集外 / neg_weight 不可解析 / 空词表)全经 `BakeFails` = 抛 + **恰一条错误**排他 + 规则 tag 触底;**MUT-Validate** 删唯一调用点 ⇒ 校验器路径 8 条恰红(`s002-mut-validate.xml`)
- [x] **AC-8-33**[L] BLOCKING:P0 词表内无 P1a 手段 —— `reveal_by` 不出现望/闻/切;`channel` 不出现舌/脉/情志/体质/时序;`lab_*` 不在 P0 表内(静态守门 + 人工核对) —— ✅ **PASS**:P1a 通道五值全覆盖(夹具 舌 + TestCase 脉/情志/体质/时序)· 手段三值全覆盖(夹具 望 + TestCase 闻/切)· `lab_*` 行红;人工核对 = `test_r82_existence` ⑥ 五通道恰五值
- [x] **AC-8-34**[L] BLOCKING:词表 ↔ 9 外键闭合 —— 9 的 R1 全部 `signs[]` 每项在 R-8.2 找得到;反向无未引用孤儿(有则显式标注「预留」);病史组 `channel=问诊` 不计新造通道。⚠️ **反向孤儿子句依赖 D-8-6(共病体征合并,归 9 规则十 + F5)= TR-diag-013 `no-adr-by-design`** ⇒ 该子句记 **BLOCKED-BY-disease-story-006,NOT-RUN,禁借绿**;正向外键闭合本 story 即判 —— ✅ **正向 PASS**(悬空 id 红 / 子集绿 / tripwire 接线义务;机制 = `ValidateForwardClosure`,负夹具证红路径真实);**反向子句 NOT-RUN**(`test_ac834_reverseOrphan_blockedish` [Ignore] 挂起,方法体诚实计算,去 Ignore 即 34 条孤儿红);病史行恰 1 条(`test_r82_existence` ⑤)
- [x] **AC-8-35**[L] BLOCKING:加一条体征(仅 R-8.1 加一行 + `channel` 已在五通道内)⇒ 全部全局常量表与 F-8.1/F-8.3 参数**逐位不变**(常量表哈希不变);需新填的只有该条逐条字段(「一份内容三处用」的结构前提) —— ✅ **PASS**:金标 `GoldenConstantsHash = b9354110` + 加行前后相等(修复轮起**绑定金标**,防同进程自比较恒真)+ 敏感性反证;**F-8.1/F-8.3 参数半边 NOT-RUN**(现常量面不含 READ_FLOOR / NEG_CONF 曲线参数 —— story 003/004 落地时扩金标或另立金标,测试头注登记,禁借绿)
- [x] **R-8.2 清单存在性**[A]:P0 条目行数与冻结清单一致(阳性 ~30 / 阴性 4,`tier_named=Lv20`);逐条医学身份已在 GDD 冻结轮过校验 —— 本 story **不改医学身份,只承载**(记忆库:医学身份错才改身份层,数值冻结) —— ✅ **PASS**:34 主键逐字 + 30/4 极性 + 阴性全 Lv20 且 raw>0 + koplik 空档 null;**内容金标 `3954e294`**(id|通道|档|极性|揭示法|三档词 逐字冻结 —— 修复轮 QA MAJOR-1 新增)
- [x] **TR-diag-014**[A]:`SLOT_BOUNDS = (10,20,50)` 与 30 的 `DIAG_TIERS(10/20/35/50)` 构建期双向耦合断言 `SLOT_BOUNDS ⊂ DIAG_TIERS`(D-8-9);`35` 在 8 侧仅作「检验线不参与槽划分」的注释存在,不进枚举 —— ✅ **PASS**:现值绿 + 漂移 21 红(双向:单向子集的静默通过口被 ② 句堵死);35 不进 tier 域(`invalid_tier_35` 红)
- [x] **烘焙确定性**[A]:同一源集双跑 `*.cooked` 字节级相等(承 ADR-014/012;中文词表 + 定表数值均入哈希) —— ✅ **同进程 PASS**(`test_bakeDeterminism_doubleRun_byteEqual`);**跨会话逐位一致 NOT-RUN**(承 interaction story-007 同款登记口径,测试头注)

---

## Implementation Notes

*Derived from R-8.1/R-8.2 + ADR-014:*

1. 作者态 `assets/data/diagnosis_signs.json`(主键 `sign_id`);三档 `display_词` 以数组按 slot 序(粗/中/细满)存,空档用显式 `null`/`"—"` 记号(**不得**用空串 —— 空串与「阴性形态」在呈现层会同构,见 **AC-8-21(+ V-8.2 / AC-8-24)**的三态可分纪律)。
   > 📌 **引用订正 2026-10-05**:原写「见 AC-8-F 侧的三态可分纪律」—— GDD 公式级 AC-8-F1…F5 均**不含**三态可分内容;三态可分原文判据 = AC-8-21 / V-8.2 / AC-8-24(实现前 GDD 对账时查出,收口顺手订正)。
   > ⚠️ **「满」档不入数据**:实现按 **3 元素**落(AC-8-32「三档齐全」= 3 槽断言,4 元素即违例);「粗/中/细满」的满档映射归 story 003 读侧(GDD 该处未另裁,读侧承)。
2. `neg_weight` 与 `tier_named` 是判定输入 ⇒ 必须是烘焙整数/Fix 字段(Fix 写字符串),零浮点字面量。
3. 空档向下回退(F-8.2)的**读侧**归 story 003;本 story 只保证数据可表达回退语义。
4. 校验器经**新增 per-schema 绑定** `DiagnosisSignBinder`(阶段 2;载体 = `Editor.Tools.Bake` 的 **interaction / item 模式**)—— `DiagnosisSignTableValidator.Validate` 的**唯一调用点** = `DiagnosisSignBinder.Bind`(删它 ⇒ 违例静默烘出);每条规则号与 GDD AC-8-32/33 括注一一对应。
   > 📌 **依赖订正 2026-10-05**:原写「复用 disease-simulation story 003 的阶段 2 载体」**与仓库实况不符** —— disease 侧只有 `Sim/RegistrySchema.cs` 校验器,无 JSON / 无 binder / 无 cooked;实际可复用载体 = `Editor.Tools.Bake` 链(实现前对账查出,本 story 按 interaction 链落地)。
5. `D-8-2`(GDD 预留登记项)若涉及词条字段增员,本 story 只留 schema 扩展位不实现。

## Out of Scope

- [Story 003]: F-8.1/8.2 求值(读表)
- [Story 004]: F-8.3 把握度(读 `neg_weight`)
- [Story 006]: 词条呈现(墨色/笔迹承载)
- 14 辨证(P1a)与检验(`lab_*`,P1a)
- 共病体征合并的实现(TR-diag-013,归 9)

## QA Test Cases

*Written at story creation(lean mode).*

- **七字段逐负例**: 每条构造「只违该条」反例行(如 `tier_named=35` / 阳性带 `neg_weight` / `channel="舌"`)⇒ 恰该条红,错误信息含规则号(AC-8-32/33)。
- **外键**: 把 9 的某 `signs[]` 项从词表删除 ⇒ 红;孤儿词 ⇒ 报警并须带「预留」标记(AC-8-34 正向半边)。
- **哈希不变**: 加一行合法词条 ⇒ 常量表哈希前后相等(AC-8-35);同时把某系数从表里挪进代码 ⇒ 该测红(结构前提的反证)。
- **SLOT_BOUNDS**: 把 `DIAG_TIERS` 的 20 改成 21 而 `SLOT_BOUNDS` 未改 ⇒ 构建失败(双向耦合)。
- **双跑**: `*.cooked` 字节级相等。

## Test Evidence

**Story Type**: Config-Data
**Required evidence**: `unity/Assets/Tests/EditMode/DiagnosisSystem/sign_table_test.cs` — must exist and pass;AC-8-34 反向孤儿子句 **NOT-RUN(BLOCKED-BY-disease epic story 006 / TR-diag-013)** 须在证据文件头显式写出
**Status**: [x] Created — **PASS 2026-10-05**(42 条,filter namespace `DaYiJingCheng.Tests.DiagnosisSystem` 含边界门共 67 条)
**Logs**: `unity/Logs/s002-bootstrap.xml`(金标引导 PENDING→钉入)· `unity/Logs/s002-run2.xml`(filter **67/66 passed / 0 failed / 1 skipped**)· `unity/Logs/s002-mut-validate.xml`(MUT-Validate 恰 8 红)· `unity/Logs/s002-full.xml`(全量 **2603/2556 passed / 0 failed** / 46 skipped / 1 inconclusive)
**登记落点**: `tests/unit/diagnosis_system/README.md`(真身路径 + AC→测映射 + 21 夹具清单)

---

## Dependencies

- Depends on: ADR-014 两阶段烘焙管线的 `Editor.Tools.Bake` 载体(interaction / item 先例模式;⚠️ 原写「disease-simulation epic story 003 阶段 2 载体」**与仓库实况不符** —— disease 侧只有 `Sim/RegistrySchema.cs`,无 JSON/binder/cooked,2026-10-05 订正)、disease-simulation epic story 004(F2 体征投影产 `signs[]`,提供外键另一端)、skill-system epic(`DIAG_TIERS` 数值层)
- Unlocks: Story 003(门槛曲线消费 `tier_named`)、Story 004(把握度消费 `neg_weight`)、Story 005(词条 id 参与状态机)、Story 006(呈现读 `display_词`)

## Completion Notes

**2026-10-05 收口(协议:创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送;评审只做一轮)。**

### 交付物
- **作者态**:`assets/data/diagnosis_signs.json`(R-8.2 冻结 34 行 = 阳性 30 / 阴性 4)
- **生产**:`DiagnosisSignTable.cs`(`DiagnosisSignTableValidator` 校验器 + 三枚举 + `SignLexemeRow` + `DiagnosisTuning.SlotBounds` **首次成文**)·
  `DiagnosisSignBinder.cs`(**唯一 `Validate` 调用点**,聚合硬失败)· `DiagnosisSignBaker.cs`(仓根种子)·
  `DiagnosisSignCookedWriter.cs` / `DiagnosisSignCookedCodec.cs`(镜像编解码,schema 期望比对 + count 钳制 + 枚举序数界)·
  `DiagnosisSignBinderProbe.cs`(测试薄转发)· `DataBakeMenu.BakeDiagnosisSigns`(菜单调用点)
- **测试**:`sign_table_test.cs`(**42 条**)+ `tests/unit/diagnosis_system/`(**21 夹具** + README 登记)
- **双金标**:`GoldenConstantsHash = b9354110`(AC-8-35)· `GoldenContentHash = 3954e294`(R-8.2 内容逐字)

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **结构侧 CHANGES REQUIRED**(1 MAJOR + 10 MINOR · **0 BLOCKING**)· **QA 侧 CHANGES REQUIRED**(2 MAJOR + 10 MINOR · **0 BLOCKING**)
- **MAJOR 落点**:① `SignChannel` 同名不同物(`Sim.Contracts` 位掩码 vs 本表序数)→ 枚举 doc 警示 + **映射表义务登记进 story-003 Implementation Note 6**(未立映射前禁 cast);
  ② R-8.2 内容金标缺失 → `test_r82_contentFrozen_golden` 补齐;③ F-8.1/F-8.3 参数半边未登记 → 头注 NOT-RUN 显式化
- **MINOR 要点**:binder FixParse `catch (Exception)`(防 `"1/0"` DivideByZero 逃逸聚合)· 空词表硬失败 `[R-8.1·signs-empty]` · codec schema 期望比对 / count 钳制 / 枚举序数界 · `SlotBounds` 收只读视图(防运行期篡改绕过耦合断言)· writer CS0104 别名 · `BakeFails` **恰一条错误排他** · P1a 闭集 TestCase 全值 · +4 违例夹具 · addRow 绑金标 · `FormatConst` 兜底改类型名 · README 真身登记 · story-004 登记 neg_weight `"1"` 占位禁当终值
- **变异证明**:**MUT-Validate**(binder 注释掉 `Validate(rows)`)⇒ 校验器路径 **恰 8 条红**(tier35/tier15/阳性带权/阴性缺权/阴性零权/重复主键/lab/病史白名单),绑定层路径全绿 —— 「唯一调用点」承重可证伪(`unity/Logs/s002-mut-validate.xml`)
- **评审原件**:`production/qa/evidence/review-diagnosis-story-002-2026-10-05.md`

### 收口时顺手订正的两处 story 文本(实现前对账查出,原登记待办)
1. **依赖订正**:Dependencies / Note 4 原写「复用 disease-simulation story 003 阶段 2 载体」**与实况不符**(disease 侧无 JSON/binder/cooked)→ 订正为 `Editor.Tools.Bake` interaction / item 模式(本 story 实际落地路径)
2. **引用订正**:Note 1 原写「AC-8-F 侧三态可分」→ 正确出处 = **AC-8-21(+ V-8.2 / AC-8-24)**(AC-8-F1…F5 不含三态可分)

### NOT-RUN / 未尽(禁借绿,均已在测试头注或本 story 显式登记)
- **AC-8-34 反向孤儿**:BLOCKED-BY-disease story 006 / TR-diag-013,[Ignore] 挂起,方法体诚实
- **AC-8-35 的 F-8.1/F-8.3 参数半边**:story 003/004 落地时扩金标或另立金标
- **跨会话烘焙逐位一致**:只证同进程(承 interaction story-007 口径)
- **9 侧正向外键对拍**:F2 投影仍空(tripwire 守接线;正向闭合本 story 以机制 + 合成引用集判)
- **`diagnosis_signs.cooked.bytes` 不入库**:承 interaction_kinds 先例(菜单烘焙生成);与 item_database 在库的不一致**待数据轮统一口径**(登记,不擅裁)
- **neg_weight `"1"` 为结构占位**(GDD *待裁*)→ 已登记 story-004 Implementation Note 7
