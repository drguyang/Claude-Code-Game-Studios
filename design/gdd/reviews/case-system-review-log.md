# 病例系统 (Case System, #37) — Review Log

## Review — 2026-09-15 — Verdict: MAJOR REVISION NEEDED

Scope signal: **M**
Specialists: systems-designer · network-programmer · game-designer · qa-lead · creative-director(收束)
Blocking items: **12** | Recommended: 若干 | Prior verdict: First review

Summary(creative-director 收束):37 病例系统「记录非实体」+「未结案永久开着」的骨架被肯定,
但 **ADR-005 终态折叠与病例流冲突**(case_id 不可重构、fires-once 破坏)是全局死结,须以
**独立病例流 + 折叠豁免**裁决;**「已处置」纯自述 = 空手连关三例的后门**(支柱四当场作废);
**CaseClosed 载荷单值 vs 共病集合**、**规则九保密措辞**(「client 不可知」为假)均须修订。
12 项 blocking 中,架构类由 **ADR-008**(2026-09-15 落盘,Proposed)承接,其余就地修订。

### Blocking Items → 处置表(2026-09-15)

| # | Blocking 项 | 处置 | 落点 |
|----|------------|------|------|
| 1 | 终态折叠丢流位置 ⇒ case_id 不可重构 / fires-once 重数 3→1 | 病例流独立 + **不折叠**(ADR-008 §六);跨流全序键补 `Patient`(ADR-008 §二) | ADR-008 · case-system.md F-37.2 |
| 2 | `CaseClosed` 载荷单值 vs F-37.3 共病集合 | `disease_set` = **集合(可空)**,快照入载荷 | ADR-008 §三 · F-37.3 |
| 3 | `PatternRecognized` 裸 `disease_id` 进流 | 加盐哈希键 `SplitMix64(WorldSeed, "case-salt")`;**保密措辞收窄**(盐 = 防御纵深非保密层) | ADR-008 §五 · 规则九 |
| 4 | 折叠删处置证据(已处置不可重构) | `treated` 布尔**快照进 `CaseClosed`**;窗口 `[CaseOpened.Tick, CloseTick]` 堵复诊后门 | ADR-008 §四 · 规则六-2 |
| 5 | 判断记录(读数/落笔/改写史)未事件化 | 新增 `JudgmentRecorded` / `JudgmentRevised` 落病例流(不违反 8 铁律②) | ADR-008 §三 · AC-37-05 |
| 6 | 「已处置」纯自述 = 空手连关三例后门 | 可结案 = **病史流 ≥1 处置事件 ∧ 勾选**(勾选 = UI 仪式) | ADR-008 §四 · F-37.2 · AC-37-21 |
| 7 | 阈值语义 `==` vs `≥` | 已为 `≥`(同 tick 批量结案可触发) | F-37.1 性质 3 |
| 8 | `case_id` 单数 Seq vs 三元组(规则二 :137 与 F-37.2 冲突) | 规则二已改三元组;全序键补 `Patient` | 规则二 · F-37.2 |
| 9 | 规则九「client 不可知」为假(病史流明文) | 保密 = **player 不可见**(DTO 静态检查,AC-37-15 唯一落点) | 规则九 · AC-37-15 |
| 10 | 重复结案幂等拒收未机械化 | 判定 = 病例流前缀纯函数,不产生事件 | 规则六-4 · AC-37-03 |
| 11 | 患者可空手连关三例(边界表「不可能」为假) | 同 #6;边界表已改 | F-37.1 边界表 |
| 12 | 成员不互异(同一病人三诊同病种不应触发世界疫情) | `MemberSet` 要求 `patient_id` 互异(创意总监裁定) | F-37.1 性质 5 · AC-37-22 |

### 新增 AC(2026-09-15)

- **AC-37-21**:已勾选但病史流无处置事件(窗口内)⇒ 结案被拒
- **AC-37-22**:同一 `patient_id` 三例同病种全部结案 ⇒ 不触发(成员互异)
- **AC-37-23**:结案后改写史完整可回看(病例流不折叠)

### 未决项(2026-09-15)

- ~~**ADR-008 仍为 Proposed**~~ ✅ **2026-09-15 用户裁定转 Accepted**:依赖它的 AC 已解锁
- **D-37-A 残留**:7a 的病史流折叠谓词 `Folded(p)` 须含「无未结案病例」条件;`max(patient_id)` 扫两流并集
- **D-9-B 神经衰弱史料**:挂起(支柱五,未证实史料不得落盘)
- **图样可辨识度**:三案链 playtest 前未验证(承重问题体例)

## Review — 2026-09-15 — Verdict: PENDING(待新会话重审)

修订完成,本次复核的 12 项 blocking 全部有处置(ADR-008 落盘 + GDD 就地修订 + 3 项新增 AC)。
**待新会话运行 `/design-review design/gdd/case-system.md` 重审。**
