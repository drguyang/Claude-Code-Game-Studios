# Review Workflow

## 评审层级

1. Code changes require review by the relevant department lead agent
2. Design changes require sign-off from `game-designer` and `creative-director`
3. Architecture changes require sign-off from `technical-director`
4. Cross-domain changes require sign-off from `producer`

---

## 🔴 评审报告原件落盘义务(2026-10-02 立 · **BLOCKING 级**)

**任何产出 BLOCKING 判定的评审,其报告原件必须落
`production/qa/evidence/`。无原件的被评审对象不得转 `Complete`。**

### 为什么立这条

2026-10-02 的对账轮实测发现:`modular-building`(5 BLOCKING)与
`world-ecozones`(4 BLOCKING)的双代理评审**原件从未落盘** ——
仅存 `bb477e6` 的 4 行 commit message 摘要,而该摘要**成了评审的全部留痕**。

三条证据确认这不是「删了」而是「从没写过」:
- `git log --all --diff-filter=D` 对 `review|评审` **零命中**;
- 本仓有归档先例(`design/gdd/reviews/` 下 20+ 份 `*-review-log.md`),
  而这两个 epic **不在其中** ⇒ 是**漏了**既定惯例;
- 当时会话的 transcript 仍在,**其内亦无报告文本**。

**后果(非理论)**:后续只能靠 commit message 的**自述**判断修复是否成立。
对账轮逐条复核后发现**两处自述失实** —— modular B4 实为「部分修」
(bit-packing 仍在)、we B1 实为「未修」(仅 TODO 化)。
**若原件在库,这两处本可在修复当时即被发现。**

### 义务(三条)

1. **落盘**:评审产出 BLOCKING 判定时,报告须以
   `production/qa/evidence/review-<对象>-<YYYY-MM-DD>.md` 落盘,
   内容含 **原判定 → 修复落点 → 验证命令**(可证伪)。
   ⚠️ **commit message 摘要不构成原件** —— 它是**索引**,不是报告。
2. **闸门**:被评审对象的 `Complete` 判定**须引用报告路径**;
   无报告 ⇒ 该对象停在 `In Review`,**不得借绿**
   (与 `coding-standards.md`「不得借绿」同源)。
3. **不可追补**:评审评的是**特定时点**的代码。事后重做评的是**当下**的代码,
   得到的是**新**判定 —— 它**不能**追认为原判定。
   ⇒ 缺原件的对象,其出路是**补做一次评审**(评当下)并落新原件,
   **不是**「补录」;历史判定是否成立**永久不可复核**,须在对象内如实登记。

### 现存缺口(本义务的前身案例)

| 对象 | 缺口 | 出路 |
|---|---|---|
| `modular-building` | 评审原件缺 | 补做一次评审,落新原件 |
| `world-ecozones` | 评审原件缺 | 同上 |
| `player-controller` | 评审原件缺 | 同上 |

三者另有已闭合的对账件(`reconciliation-*-2026-10-02.md`),
但那些**验证的是「自述的修复是否真在代码里」,不验证「原判定是否完备」** ——
两者不可互相替代。
