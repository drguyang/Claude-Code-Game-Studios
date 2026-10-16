# 证据目录 — 11 处方用药(prescription-medication)

> **本目录由 sprint-13 S13-7(2026-10-16)首次建立** —— 承 story-005 `:59` 声明的证据落点
> (「`production/qa/evidence/prescription-medication/`(目录全仓从未建立,首批建目录带 README;
> 签核位留主创 + 医学从业双栏)」)。

## 用途

存放 **11 处方用药 epic** 的走查 / 签核类证据原件。与 `production/playtests/` 的分工:

- `production/playtests/` —— **走查脚本**(执行前落盘,含步骤与可证伪判据);
- **本目录** —— 走查**执行后的签核原件**(三签表、留证截图索引、判读结论)。

## 现存件

| 文件 | 内容 | 状态 |
|---|---|---|
| `story-005-ac11-12-13-21-signoff.md` | AC-11-12 / AC-11-13 / AC-11-21 走查结论 + **三签页**(主创 / 医学从业 / 音频 lead 各一栏 + 处置问) | **待真人签署**(S13-7 卡面「agent 不可代劳」) |

**走查脚本**(执行入口 + 全部原始证据):`production/playtests/playtest-2026-10-16-s11-ac11-12-13-21-walkthrough.md`

**走查结论摘要**(详见签核件 §0):
| AC | 结果 |
|---|---|
| AC-11-12 | 零数字半 ✅ / 拟物载体可读半 🔴 |
| AC-11-13 | 🔴 缺口(相邻档无形态差异;色盲前置亦不可执行) |
| AC-11-21 | ⚫ 不可判(全案音频链未接通) |

## 签核角色(承 story-005 `:86`)

| AC | 须签核角色 |
|---|---|
| AC-11-12 | 主创(creative-director) |
| AC-11-13 | 主创 + 医学从业 |
| AC-11-21 | 音频 lead(audio-director) |

## 相关登记

- 走查缺口(agent 侧静态勘察已确认 4 条)= `docs/tech-debt-register.md` §「2026-10-16 · 11 处方用药走查缺口」
  (`TD-prescription-01` … `TD-prescription-04`;**01 是根因**)。
- story 归属件 = `production/epics/prescription-medication/story-005-dentch-input-casebook-presentation.md`。
- **跨 epic 事实登记**(2026-10-16 落 · 两条根因各自归属件的 EPIC 内,**只登记事实、处置待签核**):
  - **42 侧** = `production/epics/skeuomorphic-ui/EPIC.md` §范围边界声明 **二**
    (戥子形态元件:义务已登记、42 已认领,但 24 条 story 无一条交付 ⇒ AC-11-12 形态半 / AC-11-13 必红);
  - **44 侧** = `production/epics/audio-system/EPIC.md` §范围边界声明 **三**
    (运行期音频链未接通 ⇒ 全案音频 AC 听测半边不可判;含 AC-11-21 与 `AC-44-09`)。
