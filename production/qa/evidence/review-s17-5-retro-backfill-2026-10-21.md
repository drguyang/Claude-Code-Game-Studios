# 代码评审原件 — S17-5(retro-14 行动项 5 回写)

**日期**: 2026-10-21
**对象**: S17-5「retro-14 行动项 5(`TD-player-01` 归口)回写」
**Story 类型**: 文档回写(零代码改动)
**review-mode**: `lean`(承 `production/review-mode.txt`)
**评审方式**: 单路(`lead-programmer` 视角)+ 逐条声称实测核对

---

## 〇、为什么这件需要单独评审

sprint-17 的另外三件都有评审原件:

| Story | 原件 |
|---|---|
| S17-1 / S17-3 / S17-4 | `review-sprint17-batch1-2026-10-21.md` |
| S17-2 | `review-s17-2-clinic003-2026-10-21.md` |
| S17-6 | `review-s17-6-room-matcher-2026-10-21.md` |
| **S17-5** | **无** ← 本件补的就是这一格 |

DoD 第 10 条要求「代码评审完成」,四件 Must Have + S17-5 共五件,缺一件即不完整。

---

## 一、评审对象的特殊性

S17-5 **零代码改动**。`git show --stat 7e92c34` 实测:

```
production/retrospectives/retro-sprint-14-2026-10-18.md |  6 +++---
production/session-state/active.md                      | 14 ++++++++++++++
production/sprint-status.yaml                           |  6 +++---
```

⇒ 本件的「代码质量」面为空。**评审对象是另一件事**:台账回写的
**声称是否与原文实存一致** —— 即「我写下的结论,是否真的在文件里」。

这是文档类回写的特有失效模式:**声称与实存漂移**(写「原文不删」但删了 /
写「两处同值」但行号已变 / 写「已结」但子条仍开)。评审按此设判据。

---

## 二、逐条声称核对(可复算)

### 声称 1:「原文关键词逐词保留」—— **✅ 成立**

commit message 与 yaml note 均声称 `不抵扣` / `要么修` / `flaky registry` 逐词保留。

```bash
grep -o '不抵扣'          production/retrospectives/retro-sprint-14-2026-10-18.md | wc -l   # 4
grep -o '要么修'          production/retrospectives/retro-sprint-14-2026-10-18.md | wc -l   # 1
grep -o 'flaky registry'  production/retrospectives/retro-sprint-14-2026-10-18.md | wc -l   # 1
```

三个词全部在。`不抵扣` 4 处 ≥ commit message 声称的 2 处(多出的是回写时新加注里的引用),
**原文一字未删**这条成立。

### 声称 2:「债册侧同值可 grep」—— **✅ 成立,但行号已漂移**

```bash
grep -n '| `TD-player-01` | `player-controller` | closed | **Y** | R1 |' docs/tech-debt-register.md
# 301:| `TD-player-01` | `player-controller` | closed | **Y** | R1 |
```

值同、可 grep。⚠️ **commit message 写的行号是 `:281`,yaml note 写的是 `:209`,实测是 `:301`** ——
三处行号互不相同且都不对。**这不影响判据**(判据是「值同值可 grep」,不是「行号对」),
但登记为一处 **MINOR**:行号会随文件增长漂移,把它写死进 commit message 是**易腐的**做法;
后续回写应写「可 grep 的字符串」而非行号。

### 声称 3:「retro 全文四处 TD-player-01(:22/:119/:147/:213)」—— **✅ 成立**

```bash
grep -n 'TD-player-01' production/retrospectives/retro-sprint-14-2026-10-18.md
# 22 / 119 / 147 / 213 —— 四处,与声称一致
```

### 声称 4:「:119 / :147 / :213 三处已加注,`:22` 不动(本就带结论)」—— **✅ 成立**

`grep 'TD-player-01' | grep -c '✅'` = **3**,即三处带 ✅ 加注,`:22` 无。

`:22` 实测原文:
> `| 回归(EditMode) | — | 3682 → 3709 = +27(story-018 新增测数,零回归) | 首跑 1 条间歇红(TD-player-01),复跑绿 |`

**判定**:这是 **retro-14 时点的回归数据行**(历史事实),不是当前状态行。
历史行陈述「当时的首跑红、复跑绿」是真话,不加注是**对的** ——
给历史数据行加「现已修复」的注,反而会污染时点记录。

### 声称 5:「未兑现的一半如实保留 —— `AC-1-33②` 仍开放」—— **✅ 成立**

`retro:147` 加注含:
> `② (slopeLimit/stepOffset) **仍开放**(卡 O-9),不得读作整条已闭`

`yaml note` 同款。这是本件**最值得肯定的一处** —— 结案时主动登记「哪一半没结」,
而不是把整条 `TD-player-01` 读成已闭。

### 声称 6:「回归 `3938/3901/1/36` → `3940/3904/0/36`」—— **✅ 成立**

```bash
# 逐 XML 扫 total=3940 的
unity/Logs/td01-full.xml                      total=3940 passed=3904 failed=0 skipped=36  ✅
unity/Logs/fix-listener-full-2026-10-20.xml   total=3940 passed=3904 failed=0 skipped=36
```

数字逐位相符。

### 声称 7:「证据原件 = `impl-td-player-01-2026-10-20.md`」—— **✅ 成立**

文件在册(8070 字节,2026-10-20 07:17)。

---

## 三、发现

### M-1(MINOR · 已登记不改)行号写死进 commit message 会腐

commit message 写「债册 `:281`」、yaml note 写「债册 `:209`」、实测 `:301`。
三处互不相同。**判据本身不受影响**(值同值可 grep),但行号是易腐引用。

**处置**:不改历史 commit message(承「原文不删」)。本件登记该形态,
供后续回写时改用「可 grep 的字符串」而非行号。

### M-2(MINOR · 观察)`:22` 的「复跑绿」措辞有时态歧义

`:22` 写「首跑 1 条间歇红(`TD-player-01`),复跑绿」—— 作为 retro-14 的时点记录是正确的,
但今天的读者可能读成「现在仍是间歇性红、复跑就好」。

**判定**:不加注是**对的**(历史数据行不该被后见之明污染),但这是一个**可预见**的误读点。
**处置**:不改。若将来有人据此误判,回看本条即可。

### 无 BLOCKING / 无 WARNING

---

## 四、结账表(Phase 8b 双列)

| ID | 判定 | Landed(已落) | Proven(已证) | Proven-by(可复算判据) |
|---|---|---|---|---|
| 声称 1 原文不删 | PASS | ✅ | ✅ | `grep -o` 三词计数 = 4 / 1 / 1 |
| 声称 2 债册同值 | PASS | ✅ | ✅ | `grep -n 'TD-player-01.*closed.*\*\*Y\*\*.*R1'` ⇒ `:301` |
| 声称 3 四处存在 | PASS | ✅ | ✅ | `grep -n 'TD-player-01' retro` ⇒ 22/119/147/213 |
| 声称 4 三处加注 | PASS | ✅ | ✅ | `grep 'TD-player-01' \| grep -c '✅'` = 3 |
| 声称 5 未兑现半边 | PASS | ✅ | ✅ | `sed -n '147p'` 含「仍开放(卡 O-9)」 |
| 声称 6 回归数字 | PASS | ✅ | ✅ | `td01-full.xml` total=3940 passed=3904 failed=0 skipped=36 |
| 声称 7 证据原件 | PASS | ✅ | ✅ | `ls production/qa/evidence/impl-td-player-01-2026-10-20.md` |
| M-1 行号易腐 | MINOR | ✅ 登记 | — | 本条即记录 |
| M-2 时态歧义 | MINOR | ✅ 登记 | — | 本条即记录 |

---

## 五、未覆盖项(如实登记)

- **本件零代码**,故 code-review 技能的 Phase 4(标准合规)/ Phase 5(SOLID)/
  Phase 6(游戏特定关注)/ Phase 7(引擎专家)全部 **N/A**,不是跳过。
- **单路评审**,非三路并行(`review-mode = lean`)。
  S17-1/3/4 那件用了三路(`lead-programmer` / `qa-tester` / `unity-specialist`),
  S17-5 是纯文档,单路足够 —— 但**这是判断,不是豁免**,登记于此供后续质疑。
- **未追认 S17-5 实现轮的任何判定** —— 本件本来就没有实现轮,只有回写轮。

---

## 六、Verdict

**APPROVED WITH SUGGESTIONS**(2 条 MINOR,均登记不改)。

S17-5 的回写质量**高于卡面要求**:卡面 note 只说「只差刷 :213 行动项行」,
实测作者发现全文四处、并识别出「只刷 :213 会让同一文件自相矛盾」这一结构性问题,
把 `:119` / `:147` 一并加注。这是**正确超出范围**的处置。

**DoD 第 10 条就此兑现。**
