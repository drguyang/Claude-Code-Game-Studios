# UX Review Report — 大医精诚：破晓之剂
**Date**: 2026-09-29  
**Tier**: Standard (+ L-1/L-2)  
**Files reviewed**: 8

---

## 1. Summary Verdict Table

| # | Filename | Verdict | Top Issues | Primary Concern |
|---|----------|---------|-----------|-----------------|
| 1 | `hud.md` | **MAJOR REVISION NEEDED** | 7 (4 BLOCKING) | Incomplete structure; contradicts committed spec docs |
| 2 | `casebook-39.md` | **NEEDS REVISION** | 3 (1 BLOCKING) | K=6 vs K=7 focus-order mismatch not back-written to 8 GDD |
| 3 | `save-slots-7b.md` | **APPROVED** *(minor)* | 2 (0 BLOCKING) | OQ-SB-5册间导航未裁; GDD回写义务未执行 |
| 4 | `inventory-container-20.md` | **APPROVED** *(minor)* | 2 (0 BLOCKING) | 药签≤6字schema回写未执行; OQ-IC-6为playtest项 |
| 5 | `settings-shell-42.md` | **NEEDS REVISION** | 4 (1 BLOCKING) | OQ-SS-7恢复默认钮机制未落地; sidecar persistence 缺执行体 |
| 6 | `tutorial-48.md` | **NEEDS REVISION** | 4 (1 BLOCKING) | OQ-TUT-3墨点钮元件未落地; 开屏路由登记仍open |
| 7 | `clinic-panel-24.md` | **APPROVED** *(minor)* | 2 (0 BLOCKING) | BLOCKED-BY-45 (联机AC-42-F5); 秤刻度点位待设计 |
| 8 | `interaction-patterns.md` | **APPROVED** | 1 (0 BLOCKING) | P-01焦点单栈门切换时机(OQ-P1)未解; G-4缺口仍open |

---

## 2. Per-File Detail

### 2.1 `hud.md` — MAJOR REVISION NEEDED

**What's good**: HUD philosophy direction is sound (diegetic, no HP bars, paper+brass). Information inventory table covers the major carriers.

**Issues**:

- **[BLOCKING]** `Status: In Design` — has never been through `/ux-review`; not aligned with the 7 approved per-screen specs.
- **[BLOCKING]** Missing required sections: no Navigation, Entry/Exit, States & Variants, Interaction Map, Data Requirements, Events, Transitions, ACs sections. The checklist cannot be satisfied without them.
- **[BLOCKING]** Contradicts committed specs: §1.2 "诊断态展开时" layout shows a left/right split that contradicts `casebook-39.md` §5.2 (脉案 is a top-level modal, not a HUD sub-panel). §1.1 table lists "怀表(左上) · 天气(右上)" as always-visible HUD elements, but `settings-shell-42.md` §6 explicitly rules out always-visible HUD state displays for time/weather.
- **[BLOCKING]** Contrast spec at 4.5:1 (WCAG AA) — project's committed tier is 7:1 (AB-4); `accessibility-requirements.md` line 87 explicitly overrides WCAG AA for paper-ink text. This spec silently downgrades without justification.
- **[ADVISORY]** §3 "Dynamic Behaviors" includes 「铜器边缘泛红」as a vitals-abnormal indicator — this is a color-only signal for state, directly violating P-06 (no-sting rule, visual side) and `ADR-020 §六`. Should use geometry/sound, not color.
- **[ADVISORY]** 「快捷道具(底栏)」is a new concept not present in any GDD or spec — introducing a hotbar violates the diegetic-inventory pillar; items are carried in 出诊箱, not a hotbar.
- **[ADVISORY]** No gamepad navigation path is described for any element (L1/R1 layer switching mentioned in table but not in any Interaction Map section). K+M primary only — contradicts `technical-preferences.md` mandate that all skeuomorphic UI support gamepad focus navigation.

**Chinese-specific notes**: 4.5:1 vs 7:1 contrast is the most dangerous inconsistency — paper-ink text at 4.5:1 may fail readability on real paper textures; 「铜筹」counters need localization guidance (Chinese numeral ordering vs. Arabic numerals).

**Verdict rationale**: This file cannot be Approved as-is. It needs: (1) full section scaffold, (2) alignment pass against all 7 per-screen specs, (3) contrast standard corrected to 7:1, (4) removal of non-diegetic HUD elements (hotbar, always-visible clock).

---

### 2.2 `casebook-39.md` — NEEDS REVISION

**What's good**: Outstanding ownership boundary table (§1), states table covers all four channel states correctly, interaction map has complete K+M + gamepad dual-column, data requirements cleanly separate 8/39/11/42 ownership.

**Issues**:

- **[BLOCKING]** `OQ-CB-5`甲裁 (confidence = 病名栏 repeated Submit cycle, K=6) directly contradicts 4 locations in `diagnosis-system.md` (规则六, UI-8.2, AC-8-23, AC-8-44) and `skeuomorphic-ui.md` F3 example (K=7). The spec correctly flags this and does not pretend it is resolved — but it remains a genuine blocking inconsistency that will cause implementation divergence. Until 8's GDD is revised, this spec's K=6 cannot be enforced.
- **[ADVISORY]** §5.2 页容量 section is dense and mixes several OQ resolutions inline (OQ-CB-2/3/4) — the cascade makes it hard for a reviewer to verify each OQ is actually closed. Consider splitting into a "裁决议程" sub-table.
- **[ADVISORY]** §11 无障碍色盲行 contains a long parenthetical correcting a prior misattribution of the colorblind palette mechanism to 8 (`diagnosis-system`). The correction is correct, but the parenthetical is 180+ characters — should become a footnote for readability.

**Chinese-specific notes**: 笔迹变体 (3–4 handwriting variants per word × 3 ink concentrations) will be a significant content production cost; spec correctly notes these are素材 not font fallbacks. 「?」 mark localization (full-width vs. half-width question mark) is correctly flagged.

**Verdict rationale**: High quality, most mature spec in the set. The single BLOCKING item is an upstream GDD back-write task, not a spec flaw — the spec is correct. Reviewer should confirm 8's revision is queued.

---

### 2.3 `save-slots-7b.md` — APPROVED *(minor)*

**What's good**: Excellent ownership separation (7a/7b/42), four-state rejection taxonomy is the clearest in the set, interaction map correctly handles grayed-out rows as focusable-with-feedback (AC-7b-06), localization section addresses Chinese numeral ordering (陆/柒/捌).

**Issues**:

- **[ADVISORY]** `OQ-SB-5` (册间 navigation for >6×N slots) remains open — spec acknowledges it but does not provide even a provisional "if triggered" shape. Low risk for P0, but should have a holding statement.
- **[ADVISORY]** §12 localization: 页角注 ≤1 行 constraint says "≈24 字中文 / 扩张语 40% 后仍 ≤1 行 ⇒ 约 17 字" — the math is slightly imprecise (24 × 0.6 ≈ 14 字 for 40% expansion, not 17). Minor but should be corrected before narrative authors use the number.

**Chinese-specific notes**: 册名「杏林诊籍」is a provisional name flagged as content-debt — confirm it doesn't create encoding issues for Addressables. The 汉字序 (Chinese numeral ordering: 陆/柒/捌) is correct for 天干 ordering; slot_seq should use this consistently.

**Verdict rationale**: Approved. The two issues are minor; no blocking items. OQ-SB-5 can be handled at implementation time.

---

### 2.4 `inventory-container-20.md` — APPROVED *(minor)*

**What's good**: Four-tier carry system is monotonic and correctly derived from `CarryRatioDto`. Component inventory correctly establishes the third material tier (木/皮/铜) alongside paper and brass. Snake-placement function is deterministic and documented. Zero-nesting rule for P0 is clear.

**Issues**:

- **[ADVISORY]** §12 药签 ≤6 字 constraint is a self-imposed upper bound that requires a schema validation back-write to `item-database.md` (ADR-014 Phase 2). The spec correctly identifies this as a ripple obligation but it has not been executed — any item with `display_name` >6 characters will silently fail at runtime.
- **[ADVISORY]** `OQ-IC-6` is a valid playtest concern (near-full warning shape vs. mechanism still-accepting-items causing early abandonment). Should be in the playtest plan, not just OQ table.

**Chinese-specific notes**: 「叠角 = 炮制中」uses a corner-fold mark as a processing-state indicator — this is the first cross-screen semantic use of a 记号 and is correctly registered in the notation registry. 「旧字器物以实物堆高表达 qty≥2」 — stacking via physical pile height is a strong diegetic choice; verify art team can produce 3D-looking贴图 at the required resolution.

**Verdict rationale**: Approved. The schema back-write is a known ripple item tracked in §15. No blocking issues.

---

### 2.5 `settings-shell-42.md` — NEEDS REVISION

**What's good**: Correctly identifies itself as a shell not a screen; the no-caching constitution (AC-42-G2) is clearly stated; pluggable page-tab architecture is well-specified; zero-modern-UI commitment is explicit.

**Issues**:

- **[BLOCKING]** `OQ-SS-7` 已裁「页级归源钮」(restore-defaults button) but no mechanism has been implemented: 44 needs to register default values + write-to-mixer + persist-to-sidecar as a three-step atomic action. The spec explicitly disclaims inventing this mechanism ("本件不发明归零件形态") but without it, players who set volume to 0 cannot recover without cycling through all 7 sliders. This is a real UX hole that the spec documents but does not close — and OQ-SS-5 (sidecar persistence) makes it P0-blocking.
- **[ADVISORY]** §5.2 页签带 says "占位牌不渲染" — but P-04 (dual navigation parity) requires that gamepad players can reach all tabs. If placeholder tabs are invisible and unfocusable, gamepad players cannot discover that other pages exist. Consider rendering placeholder tabs at reduced opacity with a "待设" label.
- **[ADVISORY]** `OQ-SS-3` 七总线 grouping question is "已裁 甲" but the spec still leaves the row-count open ("登记 OQ-SS-3,不代拍"). The 7 buses will either be 7 rows or grouped into fewer — this affects the per-page row capacity calculation (§5.2) which feeds into the 150% zoom overflow logic. Should be resolved before layout is finalized.
- **[ADVISORY]** §11 本地性 row says "音量/mono 亦本地(各端各调,联机不分高低)" — this is correct per OQ-SS-5 甲 but conflicts with `ADR-018 §五` "精度统一取主机技能" for stethoscope audio. The spec correctly notes the divergence applies only to volume, but this distinction needs to be explicit in 44's implementation story to prevent accidental unification.

**Chinese-specific notes**: 「独听/双听」brass tabs are 素材字形 — the two-character labels will be carved into the brass control, meaning localization requires creating new brass textures per language (not just font swaps). This is correctly flagged.

**Verdict rationale**: The shell architecture is solid. The restore-defaults gap is the primary concern — it is BLOCKING for the user who misadjusts volume, and the spec's own OQ-SS-5 analysis elevated it from optional to necessary.

---

### 2.6 `tutorial-48.md` — NEEDS REVISION

**What's good**: §0 evidence-gap declaration is an exemplary practice — transparently documenting the "引用却无登记" failure mode before writing the spec. Ownership separation (48=semantics, 42=render, 44=audio) is clean. The shell modal shape (调度壳) is minimal and defensible.

**Issues**:

- **[BLOCKING]** `OQ-TUT-3` (墨点钮 as new component) is unresolved. The spec self-corrected from 黄铜钮 to 墨点钮 mid-authoring but the 墨点钮 has no precedent in the component library — there is no "focusable paper mark/decal" component. Until 42元件轮 creates or confirms this component, the spec's primary interactive element (play/pause) has no implementation path.
- **[ADVISORY]** §0 证据空洞 table correctly identifies that "纸堆翻页走查" appears only in 42's AC-42-F1 naming — 48 GDD has zero mentions of "堆" (stack/pile). However, the resolution (OQ-TUT-1 = 甲 调度壳) is registered but 48 GDD has not been back-written to acknowledge this. The spec notes this but the back-write obligation is still open.
- **[ADVISORY]** §5.2 页上半 = 字幕带 says "末 ≤3 条按 order 排列" — the 150% zoom overflow rule (§11) says "丢末前条" (drop the oldest). This means at 150% zoom, the oldest narration text is silently lost. Should confirm this matches 48's intent (is the oldest narration the least important?).
- **[ADVISORY]** 口述文本 ≤170 字符 is described as a "中文档 约束" — this is not a per-language constraint, it is a specific Chinese-language constraint. If the game ships in other languages, this limit may be too generous (short Japanese) or too tight (German). The spec should clarify whether this is a display-lines constraint (3 lines max at base font size) that happens to equal 170 字 in Chinese, or a hard content limit.

**Chinese-specific notes**: 「师父」speaker label width allowance ("题签带预留 2 倍宽") is correct — a 2-character Chinese word may expand to 6–8 characters in German or Japanese. The 墨点钮 character needs art-direction: a brush-dot that is simultaneously a play icon, a paper texture mark, and a 44×44 focus target.

**Verdict rationale**: The spec is well-reasoned but has a component-level gap (墨点钮) that blocks implementation. The 48 GDD back-write is a known obligation. Not a structural problem.

---

### 2.7 `clinic-panel-24.md` — APPROVED *(minor)*

**What's good**: DTO (`ClinicEnvDto`) is named, located in `Sim.Contracts`, and fully specified with field types. The 秤杆 tilt mapping function has a proven monotonicity requirement and fixed display domain (−1/2 to +1/2). Component inventory establishes the 印 (stamp) as the fifth 记号 shape with clear semantic ownership. Focus-existence declaration (§7) is precise and addresses the "K ≥ 1" rule correctly.

**Issues**:

- **[ADVISORY]** `BLOCKED-BY-45` on AC-42-F5 (next-frame refresh on StructurePlaced) — the single-player path is unavailable (OQ-CP-1 甲), leaving only the multiplayer test. Until 45 is implemented, this AC cannot be executed. Spec correctly registers this but reviewers should note it means 6/24's core "change→read→attribute" fantasy has no automated verification path at present.
- **[ADVISORY]** §5.2 秤杆 倾角映射: DISPLAY_MIN/MAX = [−1/2, +1/2] is now settled (OQ-CP-6b 甲), but the 刻度点位 (tick mark positions on the brass scale) still need design work. This is correctly left to the art/元件轮 but should be in the implementation sprint plan.

**Chinese-specific notes**: 情境词 (context words like 「无菌」「烟通丹房」) are ≤4 字 per spec. Verify CONTEXT_TABLE entries in the cooked data comply — any context name >4 characters will overflow the layout. The 印 seal mark is intentionally not a 朱 red seal (色-only hazard); it uses dark red ink泥 which is permitted by AC-42-F4③.

**Verdict rationale**: Approved. The 45 dependency for AC-42-F5 is a known and correctly registered blocker. The spec is otherwise complete and precise.

---

### 2.8 `interaction-patterns.md` — APPROVED

**What's good**: Catalog is authoritative (ADR-backed), each pattern has the full field set (意图 → 权威源 → 呈现规格 → 键鼠路径 → 手柄路径 → VR 路径 → 反例 → 消费者 → 落点文件). G-4 gap evolution is honestly documented (from "no definition exists" to "three ad-hoc definitions need consolidation").

**Issues**:

- **[ADVISORY]** `OQ-P1` (焦点单栈门切换时机: 42 holds state vs. 3 triggers via action) is unresolved — the pattern describes the mechanism but not who owns the gate-state transition trigger. Low risk but will surface during integration.
- **[ADVISORY]** `G-4` (rejection-feedback diegetic pattern) has three local implementations (inventory 放不回, tutorial 世界内轻顿, clinic-panel 站外拒开) but no consolidated pattern entry. The spec registers this as P-11 candidate pending 42元件轮. Before the closed-set walkthrough executes, P-11 should exist so the walkthrough has a consistent checklist.

**Chinese-specific notes**: None — this is an English-mechanism document; Chinese content appears only in referenced GDD titles.

**Verdict rationale**: Approved. The two open items are known and tracked. G-4 consolidation should be scheduled before the AC-42-F1 walkthrough.

---

## 3. Cross-Cutting Issues

### 3.1 Contrast Standard Inconsistency
`hud.md` specifies 4.5:1 (WCAG AA). All 7 per-screen specs, `accessibility-requirements.md` line 87, and AB-4 specify 7:1. **The HUD spec must be corrected before it can be Approved.** This is the highest-priority fix across all 8 files.

### 3.2 "空白行 = 有格线无字" Discipline
Correctly implemented in: casebook-39.md, inventory-container-20.md, save-slots-7b.md, clinic-panel-24.md. **Missing from**: hud.md (no equivalent section), tutorial-48.md (no equivalent — the tutorial shell has no ruled-line rows, so this may not apply, but it should be explicitly addressed).

### 3.3 AC-42-F1 Walkthrough Readiness
All 7 closed-set member specs are at Draft quality with open OQs flagged. `interaction-patterns.md` G-4 (P-11) should be resolved before walkthrough begins. `hud.md` must reach at least Draft before it can participate.

### 3.4 Localization Schema Back-Writes Still Open
Two specs identify schema back-write obligations that have not been executed:
- `inventory-container-20.md` §12: 药签 ≤6 字 → `item-database.md` `display_name` length validation
- `clinic-panel-24.md` §12: 情境词 ≤4 字 → `clinic-machine.md` CONTEXT_TABLE schema

These are not blocking the specs but are blocking content entry — any item/context with an over-length name will silently pass build and fail at runtime.

---

## 4. Recommended Priority Order

1. **[BLOCKING]** `hud.md` full restructure against approved specs + correct contrast to 7:1
2. **[BLOCKING]** `casebook-39.md`: queue 8 GDD back-write for K=6 focus order
3. **[BLOCKING]** `tutorial-48.md`: resolve OQ-TUT-3 墨点钮 component existence with 42元件轮
4. **[BLOCKING]** `settings-shell-42.md`: close OQ-SS-7 restore-defaults mechanism gap
5. **[ADVISORY]** Execute both schema back-writes (inventory + clinic-panel)
6. **[ADVISORY]** Resolve `interaction-patterns.md` G-4 → P-11 before walkthrough
7. **[ADVISORY]** `hud.md`: add gamepad navigation map and remove non-diegetic elements (hotbar, always-visible HUD)

---

*Report generated by ux-designer Phase 3A review. Files were not modified.*