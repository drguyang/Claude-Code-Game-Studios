# Story 002: 体征词条表 schema 与 P0 数据行

> **Epic**: 诊断与体征揭示
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Config-Data
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

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

- [ ] **AC-8-32**[L] BLOCKING:R-8.1 字段完备性构建期校验 —— `channel ∈ 五通道`(恰好五个值,无新造)· `reveal_by ∈ 五值闭合` · `display_词` 三档齐全 · `tier_named ∈ {1,10,20,50}` 不得为 `35` 或中间值 · `polarity` 必填 · `neg_weight` **仅阴性非空且 > 0**(C-6;阳性误填应报警)
- [ ] **AC-8-33**[L] BLOCKING:P0 词表内无 P1a 手段 —— `reveal_by` 不出现望/闻/切;`channel` 不出现舌/脉/情志/体质/时序;`lab_*` 不在 P0 表内(静态守门 + 人工核对)
- [ ] **AC-8-34**[L] BLOCKING:词表 ↔ 9 外键闭合 —— 9 的 R1 全部 `signs[]` 每项在 R-8.2 找得到;反向无未引用孤儿(有则显式标注「预留」);病史组 `channel=问诊` 不计新造通道。⚠️ **反向孤儿子句依赖 D-8-6(共病体征合并,归 9 规则十 + F5)= TR-diag-013 `no-adr-by-design`** ⇒ 该子句记 **BLOCKED-BY-disease-story-006,NOT-RUN,禁借绿**;正向外键闭合本 story 即判
- [ ] **AC-8-35**[L] BLOCKING:加一条体征(仅 R-8.1 加一行 + `channel` 已在五通道内)⇒ 全部全局常量表与 F-8.1/F-8.3 参数**逐位不变**(常量表哈希不变);需新填的只有该条逐条字段(「一份内容三处用」的结构前提)
- [ ] **R-8.2 清单存在性**[A]:P0 条目行数与冻结清单一致(阳性 ~30 / 阴性 4,`tier_named=Lv20`);逐条医学身份已在 GDD 冻结轮过校验 —— 本 story **不改医学身份,只承载**(记忆库:医学身份错才改身份层,数值冻结)
- [ ] **TR-diag-014**[A]:`SLOT_BOUNDS = (10,20,50)` 与 30 的 `DIAG_TIERS(10/20/35/50)` 构建期双向耦合断言 `SLOT_BOUNDS ⊂ DIAG_TIERS`(D-8-9);`35` 在 8 侧仅作「检验线不参与槽划分」的注释存在,不进枚举
- [ ] **烘焙确定性**[A]:同一源集双跑 `*.cooked` 字节级相等(承 ADR-014/012;中文词表 + 定表数值均入哈希)

---

## Implementation Notes

*Derived from R-8.1/R-8.2 + ADR-014:*

1. 作者态 `assets/data/diagnosis_signs.json`(主键 `sign_id`);三档 `display_词` 以数组按 slot 序(粗/中/细满)存,空档用显式 `null`/`"—"` 记号(**不得**用空串 —— 空串与「阴性形态」在呈现层会同构,见 AC-8-F 侧的三态可分纪律)。
2. `neg_weight` 与 `tier_named` 是判定输入 ⇒ 必须是烘焙整数/Fix 字段(Fix 写字符串),零浮点字面量。
3. 空档向下回退(F-8.2)的**读侧**归 story 003;本 story 只保证数据可表达回退语义。
4. 校验器复用 disease-simulation story 003 的阶段 2 载体(新增 per-schema 绑定 `DiagnosisSignTable`);每条规则号与 GDD AC-8-32/33 括注一一对应。
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
**Status**: [ ] Created — NOT STARTED

---

## Dependencies

- Depends on: disease-simulation epic story 003(烘焙阶段 2 管线)、story 004(F2 体征投影产 `signs[]`,提供外键另一端)、skill-system epic(`DIAG_TIERS` 数值层)
- Unlocks: Story 003(门槛曲线消费 `tier_named`)、Story 004(把握度消费 `neg_weight`)、Story 005(词条 id 参与状态机)、Story 006(呈现读 `display_词`)

## Completion Notes
