# Story 020: M2 形态件解锁链(切图冻结 → 黄铜 2px 出图 → 绑定回填)

> **Epic**: 拟物 UI 框架
> **Status**: **Complete ✅**(2026-10-08:三步全交付 —— ① 冻结件落盘 · ② 黄铜 2px 出图入库 · ③ 绑定回填 + C8 门接棒)
> **Layer**: Foundation
> **Type**: UI / 美术前置
> **Estimate**: ① 待美术实测(零新图)· ② 1.0 人日(外部出图)· ③ 0.5 人日以内(工程机械)
> **Manifest Version**: 2026-10-08
> **Last Updated**: 2026-10-08

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`
**Requirement**: 承 `production/milestones/README.md §三` M2 Exit Criteria「4 项形态件」与「五族切图与 atlas 布局冻结」

**ADR Governing Implementation**: ADR-013(拟物 UI 框架 · 自建 USS 元件库)
**ADR Decision Summary**: 形态件 ①② 压在切图上,切图早错 = 全局返工(牵连全部 USS + atlas)⇒ 冻结先行

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 纯数据/资产接线,不涉 post-cutoff API。Risk = LOW。

**Control Manifest Rules (this layer)**:
- Required: 九宫格 slice 值来自冻结件元数据(AC-42-C8 禁手填);黄铜 2px 为 M2 硬前置
- Forbidden: 自填 `spriteBorder`(第二真源);为解 block 填占位值(`PAGES_MAX` 纪律同族)
- Guardrail: 本 story 不产出 42 项全真 —— 只覆盖 M2 形态件的最小解锁链

### 为什么另立 story-020(与 story-019 的关系,避免双真源)

三步的**实施载体**本就分散:步① = story-019 的分档 **019-f**、步③ = **019-a 余项**(019-e 留的
`spriteBorder` 哨兵)、步② 黄铜 2px 在 `asset-batches/README.md` 记 `G-1b → story-019`。
**story-020 是 M2 视角的汇总执行 story** —— 把散在 019 分档与资产批次里的三步收成
**一个可排期、可收口的 story**,供 M2 Exit Criteria 对账。
> ⚠️ **双真源防线**:019 侧状态**仍归 019**(各分档状态表不搬);020 只**引用**与**验收**。
> 019-f 完成 ⇒ 020 步① 同批勾;**不得**两处各记一份状态后漂移(承 2026-10-07 状态回填教训)。

## Tasks

### 步① 019-f 切图冻结件(实施归 story-019 · 本 story 验收)

- [x] **AC-020-1** ✅ **2026-10-08**: 对**已入库 16 张 `*-final.png`** 实测九宫格 slice 边界,产出**冻结记录文件**
      (零新图;值即 019-e 所留 `spriteBorder` 哨兵的回填源)
      ⇒ **解锁 M2 形态件 ①(脉案线格/空行/明度轴)与 ②(墨乾湿两态)** —— `milestones:89`「①② 的执行前置 = 切图先冻结」
      **交付**:`design/assets/specs/nine-slice-freeze-2026-10-08.md`(`freeze-v1` 机器块 17 行)。
      ⚠️ **口径如实记**:实测为**工程侧可复算掩膜法**(非人工目测、非估计;方法/复算锚见冻结件 §一/§七)——
      「美术实测」原文措辞由该口径兑现;若美术复核发现偏差 ⇒ **重开冻结轮**(§六 已登记)

### 步② 黄铜 2px 最小切片出图(G-1b · 零实物 · 可与步① 并行)

- [x] **AC-020-2** ✅ **2026-10-08**: 焦点高亮**黄铜 2px** 最小切片**出图并入库**(≈1.0 人日,外部交付)
      ⇒ 依据 `milestones:140`(2026-10-04 E 裁:升 **M2 硬前置**)——
      形态件 ① 的明度轴判据跑在「贴图 × 主题色最亮像素」上,**无铜族贴图 ⇒ ① 的验收无载体**
      **交付**:`unity/Assets/Gameplay.UI/Skeuomorphic/Textures/Brass/focus_brass_2px-final.png`
      (64×64 RGBA,2px 环 `#B8863B` = art-bible §4.1 权威值,中心透明)+ meta(`spriteBorder: 2`)+ `Brass.meta`。
      放子目录**刻意** —— 顶层 16 张计数/格式门用 `TopDirectoryOnly`,不受影响

### 步③ 019-a 余项:绑定回填(工程 · Blocked-by 步①)

- [x] **AC-020-3** ✅ **2026-10-08**: `spriteBorder` **零哨兵值由冻结件一次填入**(019-e 只订正格式未填值)+
      USS 绑定核验通过(C7/C8/C10/C11 判据在真值下跑绿)
      ⇒ **禁自填**(story-019 §019-e/019-f 耦合点:`自填 = 第二真源`)
      **交付**:4 张 meta 回填(border_paper 64 · border_scroll **72**(冻结轮查出 68 切右框 1px)·
      paper_aged 64 · seal_surface 8)+ `SkeuoScroll.uss` 68→72 同批改 + **哨兵门退役 →
      `ValidateSpriteBorderMatchesFreeze`**(meta + USS 双侧 = 冻结件,任一单点改 ⇒ 红)
      **绿**:过滤 225/212/0 红 · 变异恰 2 红 · 全量 EditMode 3002/2955/0 红 · 全量 PlayMode 98/97/0 红

## Dependencies

- **Blocked-by**: 019-f 冻结件(步①)· 黄铜 2px 出图(步②,独立)· 019-e 格式订正 ✅(2026-10-05,已闭)
- **Enables**: M2 Exit Criteria「4 项形态件」①② · 「五族切图与 atlas 布局冻结」条 · 「焦点黄铜 2px 最小切片」条
- **不阻塞**: 形态件 ③(急救零数字+可跳过)④(状态反馈通道)—— 零美术依赖,可立即开工

## Acceptance Criteria

- [x] 三步 AC 全勾,且 019 侧状态同批回刷(019-f Complete + 019-a 余项闭)✅ **2026-10-08**
- [x] M2 Exit Criteria 对应三条同轮回勾 ✅ **2026-10-08** —— **分化结果如实记**:
      ① 黄铜 2px 条 → **勾 `[x]`**;② 五族冻结条 → **保持 `[ ]`** 加注(切图冻结 ✅ + 019 接线 c/d/e/f 齐;
      余 **atlas 布局/页数 = 019-b Blocked**,故整条未成立);③ 形态件①② 的**前置**(切图冻结)✅ ——
      形态件**本体交付**仍 `[ ]`(归后续形态件 story,非本 story 范围)
- [x] 零占位值 ✅ **2026-10-08**(16 张 = 可复算掩膜法实测,非估计;铜 2px = 规格书直给 R3,
      非拍脑袋;0 值 = 明示不走九宫格,非占位 —— 三类取值规则见冻结件 §三)

## Definition of Done

- [x] 三步 AC 全闭 + 019 状态同批回刷 ✅ **2026-10-08**(story-019 头部状态 / 三步表 / 拆分表 /
      AC-42-C8 独立条目 / Test Evidence / Completion Notes 六处同批落)
- [x] 测试证据:AC-42-C8/C10/C11 在真 `spriteBorder` 值下复跑绿 ✅ **2026-10-08** ——
      过滤 225/212/0 红(`c8-freeze-round.xml`)· MUT-C8 变异恰 2 红(`c8-mut.xml`,还原复核)·
      全量 EditMode 3002/2955/0 红(`editmode-full-c8-round.xml`,+1 新夹具零回归)·
      全量 PlayMode 98/97/0 红(`playmode-c8-round.xml`,基线逐数一致)
