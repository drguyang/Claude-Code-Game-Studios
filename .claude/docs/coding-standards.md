# Coding Standards

- All game code must include doc comments on public APIs
- Every system must have a corresponding architecture decision record in `docs/architecture/`
- Gameplay values must be data-driven (external config), never hardcoded
- All public methods must be unit-testable (dependency injection over singletons)
- Commits must reference the relevant design document or task ID
- **Commit messages**: Use Conventional Commits format — `feat:`, `fix:`, `chore:`, `docs:`, `test:`, `refactor:`. Reference the story or task ID in the body (e.g., `Story: EPIC-001-S02`).
- **Verification-driven development**: Write tests first when adding gameplay systems.
  For UI changes, verify with screenshots. Compare expected output to actual output
  before marking work complete. Every implementation should have a way to prove it works.

# Naming — 机械门避让表(2026-10-18 立)

**为什么单列一节**:项目里有一批**机械门**是按**标识符字面**扫的,不看你写注释里的意图。
撞上去的失败信息**指向门的词表、不指向你的命名**,排查成本高且极易被误读成「门坏了」。
下表是**已实测命中**的避让项 —— 命名前先扫一眼。

| 避让 | 触发门 | 判据(实测) | 出处 |
|---|---|---|---|
| **`strip` / `grip` / `trip`** 等**含 `rip` 子串**的成员名 | `NoHealthbarFeedbackTest.test_markRegistry_noPaperTear_noTearFields` | 破纸词表 `{tear, rip, break, split, fracture}` 按**成员名小写子串**匹配,**限引用类型**(`f.FieldType.IsClass \|\| IsInterface`),`BindingFlags.DeclaredOnly` | story-025 实现轮(2026-10-18):`const string DetentStripClass` + `private readonly VisualElement _detentStrip` **双双命中** ⇒ 改 `band` |

**适用范围**:仅 **`Gameplay.UI/Skeuomorphic/**` 的成员名**(该门只扫 42 侧装配)。
**不是**全仓禁令 —— 值内容、局部变量、注释均不在扫描面内。

**同族提醒(未穷举)**:`AC-42-F3` 词门扫 `Skeuomorphic/**/*.cs` 的**全文(含注释)**,
避让 `未查标签` / `占位符` / `计数` / `分组` / `排序` / `检索控件` / `同源提示` / `数字角标` / `进度填充`。
⇒ **写 42 侧注释时也要避开这些词**,否则注释本身就会让门转红。

# 工作树卫生 — 「这条改动是什么」三分类(2026-10-21 立)

**为什么单列一节**:跑完工具后 `git status` 冒出看不懂的改动时,**有三种可能**,
而它们要求**完全不同的处置**。混淆的代价是双向的 —— 把「正常派生」当损坏会做无用功
(还会污染 commit message),把「损坏」当噪音会漏掉真问题。

| 类 | 判据 | 处置 |
|---|---|---|
| **① 损坏 / 真改动** | 改动落在**作者态**语义上(源文件、作者编辑的设置、须入库的资产) | **查清再动**;不确定时**先报,不要自裁** |
| **② 正常派生** | 改动由工具的**设计内行为**产生,重跑工具会再现 | 生成物 ⇒ 进 `.gitignore`;**混在作者态文件里的** ⇒ 见下 |
| **③ 我的误读** | 机制没查就下了结论 | 收回结论,**先查源码再定词** |

> ⚠️ **本节的立规缘由(实测,2026-10-21 C3 轮)**:真构建后
> `UniversalRenderPipelineGlobalSettings.asset` 的 `m_RuntimeSettings` 从 16 条 rid 变 `[]`。
> 我**直接按 ① 报了**,用了「污染」这个断定词,还多做了一次还原。查
> `com.unity.render-pipelines.core` 源码后:那是 `OnBeforeSerialize()` 在
> `BuildPipeline.isBuildingPlayer` 时 `Clear()` + `PerformStripping()` 的**正常派生**
> (`m_RuntimeSettings` 是播放器侧快照,编辑器侧 getter 读的是 `m_SettingsList`)。
> ⇒ **类别判错 + 措辞过重 + 多做一步**,三样都发生了。详见 `TD-build-01`。

### 派生 vs 作者的判定法(**下结论前必做**)

1. **重跑工具会不会再现?** 会 ⇒ 倾向 ②(派生物)。不会 ⇒ 倾向 ①。
2. **改动是否落在编辑器侧真正被读的字段上?** 读源码里那个字段的 getter 有没有
   `#if UNITY_EDITOR` 分叉 —— **别猜,打开看**。
3. **文件里是否还混着作者态内容?** 若是,那它是「混合文件」,不能整文件忽略(见下)。

### `.gitignore` —— 窄 glob,不整目录

**优先逐类文件窄 glob,而不是整目录忽略。** 整目录忽略的失败模式是**静默**:
将来真有该入库的资产落进那个目录,**它会自动消失,而且看不见**。
窄 glob 的失败模式是**可见的**:新生成物会以未跟踪形态冒出来,由人判断去留。

| 反例(禁) | 正例 | 理由 |
|---|---|---|
| `unity/Assets/Resources/` | `unity/Assets/Resources/PerformanceTestRun*.json*` | `Assets/Resources/` 是 **Unity 特殊目录**(`Resources.Load` 只认它);整目录忽略会吞掉将来真该入库的资产 |
| `unity/Assets/AddressableAssetsData/` | `unity/Assets/AddressableAssetsData/link.xml` | 该目录**混着作者态**(组定义须入库)与生成物(`link.xml`)。2026-10-17 曾因整目录忽略致全新 clone 上音频组缺失(`TD-audio-01`) |

**混合文件 = 不能忽略**:若同一文件里既有作者态又有派生态(如 URP GlobalSettings 的
`m_Settings` + `m_RuntimeSettings`),`.gitignore` **整文件**会连作者的编辑一起吞掉。
这类只能**保持现状**(每次工具后人工判读),或另裁机制。

**新增 `.gitignore` 规则时的两条必做**:
- 逐条 `git check-ignore -v <path>` 验证**命中**;
- **同时验证反例未被误伤**(挑 2–3 个「必须保持不忽略」的邻近路径),否则规则可能过宽。

### commit message 里的定性错了怎么办

**未推送 + 零引用 + 工作树干净 ⇒ 可以订正 message**,用 `commit-tree` 重建
(不要 `rebase` / 不要 `checkout`):

```bash
NEW=$(git commit-tree <old>^{tree} -p <old>^ -F new-msg.txt)   # tree 原样复用
git update-ref refs/heads/<branch> "$NEW" "$(git rev-parse HEAD)"  # 第三参数 = 旧值,防误覆盖
```

**判据 = 改写前后 `tree` 哈希逐位相同** —— 它证明「只动了 message,没碰任何文件内容」。
（若 `tree` 变了,说明混进了内容改动,立即停下重做。）

> ⚠️ 订正后**旧哈希会从历史里消失**。若别处已引用它(issue / 文档 / 对方 commit message),
> **不要 amend**,改为在新 commit 里登记订正(承「原文不删」纪律)。
> 改写前必查三件:`git branch -r --contains <sha>` 为空 · 全仓 `grep` 旧哈希零命中 · `git status` 干净。

# Review Evidence Standards

- **评审报告原件落盘(BLOCKING 级 · 2026-10-02 立)**:任何产出 **BLOCKING 判定**的评审,
  其报告原件须落 `production/qa/evidence/review-<对象>-<YYYY-MM-DD>.md`,
  含 **原判定 → 修复落点 → 验证命令**(可证伪)。**无原件 ⇒ 该对象不得转 `Complete`**。
  - ⚠️ **commit message 摘要不构成原件** —— 它是索引,不是报告。
  - ⚠️ **对账件(`reconciliation-*.md`)不可替代原件** —— 对账件验证的是
    「自述的修复是否真在代码里」,**不验证「原判定是否完备」**。
  - ⚠️ **评审不可事后追补** —— 事后重做评的是**当下**的代码,得到的是**新**判定,
    **不追认**原判定。缺原件的对象,出路是**补做一次评审**(评当下)并落新原件。
  - 完整义务与现存缺口见 `.claude/docs/review-workflow.md`;
    收口闸门见 `.claude/skills/story-done/SKILL.md` §评审报告原件要求。

# Design Document Standards

- All design docs use Markdown
- Each mechanic has a dedicated document in `design/gdd/`
- Documents must include these 8 required sections:
  1. **Overview** -- one-paragraph summary
  2. **Player Fantasy** -- intended feeling and experience
  3. **Detailed Rules** -- unambiguous mechanics
  4. **Formulas** -- all math defined with variables
  5. **Edge Cases** -- unusual situations handled
  6. **Dependencies** -- other systems listed
  7. **Tuning Knobs** -- configurable values identified
  8. **Acceptance Criteria** -- testable success conditions
- Balance values must link to their source formula or rationale

# Testing Standards

## Test Evidence by Story Type

All stories must have appropriate test evidence before they can be marked Done:

| Story Type | Required Evidence | Location | Gate Level |
|---|---|---|---|
| **Logic** (formulas, AI, state machines) | Automated unit test — must pass | `unity/Assets/Tests/EditMode/[System]/` | BLOCKING |
| **Integration** (multi-system) | Integration test OR documented playtest | `unity/Assets/Tests/PlayMode/`(或同树 `EditMode/[System]/` 真装配件) | BLOCKING |
| **Visual/Feel** (animation, VFX, feel) | Screenshot + lead sign-off | `production/qa/evidence/` | ADVISORY |
| **UI** (menus, HUD, screens) | Manual walkthrough doc OR interaction test | `production/qa/evidence/` | ADVISORY |
| **Config/Data** (balance tuning) | Smoke check pass | `production/qa/smoke-[date].md` | ADVISORY |

> ⚠️ **路径写明实际落地的那棵树,且允许一条 AC 多载体(2026-10-15 立)** ——
> 测试件横跨**两棵树**:`tests/unit|integration/[system]/` 是**账本约定**(见 `tests/README.md`),
> `unity/Assets/Tests/{EditMode,PlayMode}/` 是工程落地后的**主体编译树**;
> **两棵都可能承载活测试**(账本树非恒空:`tests/unit/skeuomorphic-ui/` 自带 `.asmdef`)。
> ⇒ ① 上表 Location 写**实际落地路径**,另一棵可并列注明;
> ② **一个 AC 的载体允许多条**(story §Test Evidence 的 `AC → 载体映射` 表须列全)——
> 后续 sprint 补的真装配件 / 真启动序件同样是该 AC 的证据;
> ③ 判「无证据」前须两树都扫(`grep -rln "<符号>" unity/Assets/Tests/ tests/`)并贴出输出,
> 否则只能写「在 X 范围内未发现」。详见 `.claude/docs/review-workflow.md` 义务 4「评据不得越权」。

## Automated Test Rules

- **Naming**: `[system]_[feature]_test.[ext]` for files; `test_[scenario]_[expected]` for functions
- **Determinism**: Tests must produce the same result every run — no random seeds, no time-dependent assertions
- **Isolation**: Each test sets up and tears down its own state; tests must not depend on execution order
- **No hardcoded data**: Test fixtures use constant files or factory functions, not inline magic numbers
  (exception: boundary value tests where the exact number IS the point)
- **Independence**: Unit tests do not call external APIs, databases, or file I/O — use dependency injection

## What NOT to Automate

- Visual fidelity (shader output, VFX appearance, animation curves)
- "Feel" qualities (input responsiveness, perceived weight, timing)
- Platform-specific rendering (test on target hardware, not headlessly)
- Full gameplay sessions (covered by playtesting, not automation)

## CI/CD Rules

- Automated test suite runs on every push to main and every PR
- No merge if tests fail — tests are a blocking gate in CI
- Never disable or skip failing tests to make CI pass — fix the underlying issue
- Engine-specific CI commands:
  - **Godot**: `godot --headless --script tests/gdunit4_runner.gd`
  - **Unity**: `game-ci/unity-test-runner@v4` (GitHub Actions)
  - **Unreal**: headless runner with `-nullrhi` flag
