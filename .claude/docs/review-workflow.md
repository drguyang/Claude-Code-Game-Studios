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

### 义务(五条)

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
4. **评据不得越权**(2026-10-15 立)**评审只能对其「实际核过」的范围下判定。**
   作**全域否定**表述(「零验证」「全仓零命中」「无处可依」「无任何测试覆盖」)前,
   报告须**同时给出全树搜索命令与结果**;拿不出 ⇒ 必须降级为
   **「在 X 范围内未发现」**并写明 X。

   **为什么立这条(与义务 1–3 的失效模式不同,是第二种)**:
   2026-10-15 case-system 001–004 补评实测 —— 两名代理均**只核了 story §Test Evidence
   声明的那个文件**,却写出「AC-37-30 入流义务**零验证**」「处置半边**全仓零命中**」等
   全域否定,据此产出 5 条 BLOCKING。而**真载体在别处**:
   `EditMode/Boot/judgment_writer_test.cs`(真装配)· `CaseSystem/case_open_writer_test.cs`
   (真组合根 + 真流)· `PlayMode/case_opened_wiring_test.cs`(**真启动序**)·
   `EditMode/DiagnosisSystem/reading_state_test.cs`(→8 读数清除 / →53 冻结)。**五条全被推翻。**

   **本仓的两条结构性诱因**(复审时须先排除):
   ① **声明件 ≠ 全证据面** —— 一个 AC 的载体常横跨「声明的那个件」与**后续 sprint 的接线件**
      (本 epic 的接线件晚于 story 收口 4–7 天,当年收口时确实不存在 ⇒
      **补评口径是「评当下」,必须计入**);
   ② **测试件横跨两棵树,须都查** —— `tests/unit|integration/[system]/` 是**账本约定**树
      (见 `tests/README.md`),`unity/Assets/Tests/{EditMode,PlayMode}/` 是工程落地后的**主体编译树**
      (256+ 件)。**两棵树都可能承载活测试** —— 账本树**并非恒空**:
      `tests/unit/skeuomorphic-ui/` 即持有自带 `.asmdef` 的独立测试工程。
      按**单一**树判「目录不存在 ⇒ 缺失」**会系统性误判**;须两树都查,
      并以被测生产符号广搜(`grep -rln "<符号>" unity/Assets/Tests/ tests/`)。
      ⚠️ 反向亦然:不得反过来说「账本树已作废」—— 那只是把越权换了个方向。

   **操作口径**:立「无证据」判定前,至少跑一次
   `grep -rln "<被测符号>" unity/Assets/Tests/`(全树),并在报告中原样贴出输出。
5. **回刷收口**(2026-10-15 立)**改完一处口径,须对同一关键词做全仓 grep,
   逐条判「动 / 不动」并落纸。**

   **为什么立这条(第三种失效模式,与前四条不同)**:义务 1–3 治的是「没留痕」,
   义务 4 治的是「越权下判」,本条治的是**回刷自己生产新矛盾** ——
   一次回刷若**只改「声明段」而不改「表 / 件」**,就会在**同一个文件里**
   留下一半新口径、一半旧口径,即**又生产一批同文件自相矛盾**。

   **实例(24 小时内现身两次)**:prescription **S11-4** 首轮修掉了四处同文件自相矛盾,
   却因只改 `EPIC.md` 的两处**声明段**、未改 `:88` 的 story 表行 / `:112` 的未闭项表 /
   `story-003:110`,**又生产了三处**;二轮才补齐。
   见 `production/qa/evidence/fix-s11-4-prescription-dod-residuals-2026-10-15.md` §八。

   **操作口径**:
   - 回刷落盘后,对**本次被改动的那个关键词**(如「7a 白名单」/「BLOCKED-BY-7a」)
     跑 `grep -rn "<关键词>" .`(全树)。
   - 把命中**逐条分类**为四类之一:
     **① 本次改** / **② 与新口径一致故不动** / **③ 域外·不同事项故不动** /
     **④ 历史读数故保留**。
   - 分类结果**落纸**(证据原件中一节即可 —— 样板 = 上述 §8.2 那张表)。
   - ⚠️ **域外命中不等于「该动」** —— 但**必须逐条说明为何未动**,不得静默略过
     (静默略过正是义务 4 要治的那种「未核却当已核」)。
   - ⚠️ **计划件 / 历史读数件用「原文保留 + 就地加注」,不抹除历史读数**
     (先例:`e67b33b` 对 `qa-plan-sprint-02/03.md`、`b1f32ca` 对 `qa-plan-sprint-03.md`)。
     **加注 ≠ 改判据** —— 先裁定、后加注,两者是两件事;不得以前者冒充后者,
     也不得以「不改判据」为由拒绝加注。

   ⚠️ **同样刻意不做 hook**:机械 hook 判不出「这个命中该不该动」
   (它需要读懂该行是**声明**还是**表 / 件**,以及是否属域外),
   做了就是**假门** —— 与义务 4 / `/sprint-plan` Phase 1 第 5 步同一条纪律。

### 现存缺口(本义务的前身案例)

| 对象 | 缺口 | 出路 |
|---|---|---|
| `modular-building` | 评审原件缺 | 补做一次评审,落新原件 |
| `world-ecozones` | 评审原件缺 | 同上 |
| `player-controller` | 评审原件缺 | 同上 |

三者另有已闭合的对账件(`reconciliation-*-2026-10-02.md`),
但那些**验证的是「自述的修复是否真在代码里」,不验证「原判定是否完备」** ——
两者不可互相替代。
