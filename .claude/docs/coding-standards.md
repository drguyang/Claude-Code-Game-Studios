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

| **门类名不以 `Gate` / `Gates` 结尾**(如叫 `*Guard` / `*Scanner`) | `GateWiringManifestTest.test_classManifest_chainKindEntriesAreActuallyWired` · `test_entryWiring_matchesManifest_bidirectionally` | 门链解析器 `GateChainSource.GateClassNameSuffixes = {"Gate","Gates"}`:限定的 `X.Method(` **只有 X 以 Gate/Gates 结尾才被认作门接线** ⇒ 叫 `*Guard` 的门即使**真的被 `RunAll` 调用**,也不进接线集,两条判据同时红,报错却是「声明 Chain 但链上看不见它」—— 指向 manifest,不指向命名 | S25-1(2026-11-06):`TutorialSurfaceGuard` 已接进 `RunAll` 仍两红 ⇒ 改名 `TutorialSurfaceGate` 即绿 |

| **方法名含 `Append` / `Write` / `Emit`**(**含私有方法**) | `ClinicPanelRefreshTest.test_ac42d2_noEventSinkAppend_noWriteMethods` | 对 `DaYiJingCheng.Gameplay.UI*` 的**全部类型**取 `BindingFlags.Public\|NonPublic\|Instance\|Static\|DeclaredOnly` 的**方法名**做子串匹配,命中即红(禁 42 写三流);**编译器生成的 lambda 名也会带上**(`<>c.<AppendNarration>b__12_0`) | S25-4(2026-11-06):`TutorialDispatcher.AppendNarration/Demonstrations/Papers` 三个**私有** helper 只往本地列表加条目,仍被三条 + 1 条合成红 ⇒ 改 `Collect*` |

| **引用型成员名含 `clip` 子串**(`ClipKey` / `clipField` …) | `NoHealthbarFeedbackTest.test_rejectionWhitelist_noSilentClipping_noClipFields` | 词表 `{clip, truncate, shrink, crop, overflow}` 按成员名小写子串匹配,**限引用类型**(`string` 算) | S25-4(2026-11-06):`TutorialMediaIntent.ClipKey`(示范片段键,语义正当)命中 ⇒ 改 `SegmentKey`,源数据 `TutorialDemonstrationRow.ClipKey` 不动(它在 `Gameplay.Presentation`,不在扫描面) |

**适用范围(逐行不同)**:
- 含 `rip` 子串成员名 —— 仅 **`Gameplay.UI/Skeuomorphic/**` 的成员名**(该门只扫 42 侧装配);**不是**全仓禁令 —— 值内容、局部变量、注释均不在扫描面内。
- **`Append`/`Write`/`Emit` 与 `clip` 子串两条 —— 扫描面是 `DaYiJingCheng.Gameplay.UI` 起的**整个命名空间前缀**(含 `Gameplay.UI.Tutorial` 这类子装配,不只是 `Skeuomorphic/`)。新增 42 侧类时,**方法名与字段名都要先过这两张词表**;`Gameplay.Presentation` / `Sim*` 不在面内。
- ⚠️ **门名与门词表都别靠猜**:写新类前先 `grep -n "Contains(\"" unity/Assets/Tests/EditMode/SkeuomorphicUI/*.cs` 把在用词表列出来。
- 门类名 `Gate` / `Gates` 后缀 —— **`unity/Assets/Editor.Tools.Gates/**` 的 `public static class` 名**。⚠️ 这一条**不写在任何命名规范里**,却在两条门里执行;新增门类时按它命名,或用 `TestDriven` 档(非 BLOCKING)避开。

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

### 已实测的构建派生组(2026-10-21 立案 · **2026-10-31 复测**)

真构建后 `git status` 会冒哪些**非本批改动**?下表是**实测过的三组**,逐组按上面的三分类定性。

| # | 路径 | 机制(源码依据) | 三分类 | 现行处置 |
|---|---|---|---|---|
| ① | `unity/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` | `RenderPipelineGraphicsSettingsContainer.OnBeforeSerialize()`:构建期 `Clear()` 后 `PerformStripping(m_SettingsList → m_RuntimeSettings)`,而 `m_RuntimeSettings` 是**播放器侧派生快照** | **② 正常派生** | ⚠️ **不进 `.gitignore`** —— 同一文件混着 34 条 `m_Settings`(**作者态**),整文件忽略会吞掉将来的真实编辑(用户 2026-10-21 裁定「只登记,不动」) |
| ② | `unity/Assets/Resources/PerformanceTestRun{Info,Settings}.json`(+ `.meta`) | `TestRunBuilder.OnPreprocessBuild` 建文件,`OnPostprocessBuild → Cleanup()` **自删** | **② 正常派生** | 已进 `.gitignore` 窄 glob —— 但它是**失败路径的兜底**,不是常态 |
| ③ | `unity/Assets/AddressableAssetsData/link.xml`(+ `.meta`) | Addressables 播放器构建期生成的链接器保留清单;从未入库、代码零引用 | **② 正常派生** | 已进 `.gitignore` 窄 glob |

**🔁 2026-10-31 复测的三处订正(原文不删):**

- **成功构建后 `git status` 是干净的。** 本轮真构建(`CostTableProbeBuildCli.Build`,
  `Build Finished, Result: Success` · 26 s)后 `git status --short` **零输出** —— 三组一组都没露头。
  `link.xml` 与它的 `.meta` 确实重新生成了(mtime 是构建时刻),且**被窄 glob 正确忽略**
  (`git status --ignored` 显示为 `!!`)。
- **② 不是「每次真构建都残留」** —— `TestRunBuilder.cs:41-44` 的 `OnPostprocessBuild → Cleanup()`
  会自删,`Cleanup()` 内是 `DeleteFileAndMeta(...)`。⇒ **残留条件是「构建没走到 postprocess」**
  (失败 / 中断)。2026-10-21 之所以观察到它,是因为那一轮跑的正是**刻意报 Failure 的探针轮**。
  ✅ **2026-10-31 失败路径已实测**(`S21-10` 收口后补,F5):探针轮 —— 往 `BuildGate.OnPreprocessBuild`
  插一条无条件 `errs.Add` 并提前 `throw`,`CostTableProbeBuildCli.Build` 跑出
  `Build Finished, Result: Failure`(栈帧 `AssemblyGates.cs:1437`),随后实测:
  - `unity/Assets/Resources/PerformanceTestRunInfo.json`(**3136 B**)与
    `PerformanceTestRunSettings.json`(**23 B**)**确实留在盘上** —— ①的源码推断成立;
  - `.gitignore:93-94` 的窄 glob **命中**这两件;反例验证:`Resources/` 下放一个非匹配文件
    **未被忽略** ⇒ 规则没有过宽(整目录未受影响)。
  - ⚠️ **新发现(原表未记)**:失败构建**还会留下 ① 的派生改动** ——
    `UniversalRenderPipelineGlobalSettings.asset` 的 `m_RuntimeSettings` 由 16 条 rid 变 `[]`,
    与 2026-10-21 那次观察同形。⇒ **「成功构建后 `git status` 干净」不等于「构建后干净」**:
    **失败路径同时留下 ②(资源 json)与 ①(URP 剥离)两类派生**,判读时按 ② 处理(UPR 文件仍不可整文件忽略,见上表 ①)。
- **① 本轮未复现内容差异**:文件 mtime 变了(被构建重写),但内容与 `HEAD` 逐字节一致
  ⇒ 现行入库内容已等于派生形态。

**复算(命令自包含,别凭本表推断):**

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
git status --short                       # 期望空(成功构建后)
git check-ignore -v unity/Assets/AddressableAssetsData/link.xml   # 期望命中 .gitignore
# 反例(必须**不**被忽略):
git check-ignore -q unity/Assets/AddressableAssetsData/AssetGroups/Default.asset || echo 未忽略
git check-ignore -q unity/Assets/Resources/SomeRealAsset.asset     || echo 未忽略
```

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

# 依赖写法 —— 形状 vs 实现(2026-10-31 立)

**为什么单列一节**:story 的 `## Dependencies → Depends on` 会被 `dev-story` Phase 2 与
`story-readiness` **机械读取**:上游 `Status != Complete` ⇒ 本件判 `BLOCKED`。
而作者常常**只想说「我需要它的类型形状」**,却写成「依赖它的实现」——
一旦几件互相这么写,就会构成**闭环死锁**(谁先做都不行)。

## 两种依赖,写法必须分开

| 类型 | 含义 | 写法 | 校验语义 |
|---|---|---|---|
| **形状依赖** | 只需要对方的**载荷 / 接口 / DTO 类型形状**;对方的**实现**在不在、跑不跑都不影响 | `依赖 <X 的载荷/接口类型>(已 Complete)`,**括号里点名类型** | 类型在盘即满足,**不**要求对方 story `Complete` |
| **实现依赖** | 需要对方**运行期真的产生行为**(发事件 / 提供数据 / 落流) | `依赖 X 的实现` | 必须等对方 `Complete` |

## 判定法(一句话)

> **问:如果 X 是个永不执行的空实现,我还能编译、还能让我的夹具通过吗?**
> 能 ⇒ 形状依赖;不能 ⇒ 实现依赖。

## 三条硬要求

1. **写形状依赖时必须点名类型**(如 `CraftPayload.OutputInstanceIds` / `IIdAuthority`)——
   不点名的「依赖 X」在读者眼里一律按**实现依赖**处理。
2. **不得用「依赖 X」的模糊写法省事**。写不准就是没写。
3. **只对新件生效,不追改旧件**。已 `Complete` 的 story 改依赖行 = 改历史;
   若发现旧件写法有问题,**登记为债**,不在原处动刀。

> **立规缘由**:`TD-deps-01`(2026-10-26 立)。`inventory-001` ↔ `processing-003` ↔ `inventory-002`
> 三方把「需要载荷/接口形状」写成「需要对方实现」⇒ 构成闭环,三件**互为阻塞、任一件都开不了**。
> 该债最终由**事实推进**消解(两件做完了),但**写法诱因没消失** —— 本节是那一半的修复。
> 关闭记录见 `docs/tech-debt-register.md` 的 `TD-deps-01 关闭记录`。

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

- **数字须带可跑复算命令(BLOCKING 级 · 2026-10-22 立)**:凡写进文档的
  **分母 / 回归数 / 覆盖率 / 计数 / 件数**类数字,**收口时须附一条可当场跑的命令 + 其实跑输出**。
  禁止照抄规划期的算术,禁止把「上次记的值」当「本次的值」。
  - 命令须**自包含**(读哪个文件、跑什么、预期输出长什么样),不得依赖未写明的上下文。
  - **引用旧件时必须改写来源** —— 写 `引用自 <file> @ <其截至时刻>`,不得重新盖上本次的时间戳。
  - 回归数另须带**三元组**:截至时刻 · 批次 · commit(见 `smoke-check` skill §Baseline provenance)。
  - **立规缘由(2026-10-22 sprint-18 收口)**:`73 → 71` 这个假分母在 sprint 计划 /
    `sprint-status` baseline / QA 签核件**三处存活**,三处都是规划期算术被当成了收口结论。
    只要任何一处跑一遍债册 §五 的脚本,当场就会看到 **74**。**「71 从未在任何情形下成立」。**
    详见 `production/sprints/sprint-18.md` §九.1 与 `retro-sprint-18-2026-10-22.md` §What Went Poorly。

- **「某件有门」的自述须给 `file:line` + 可跑命令(BLOCKING 级 · 2026-10-22 立)**:
  任何「**有门 / 有校验 / 有断言 / 有机械检查**」的自述,须同时给出
  ① 该门所在的 `file:line` ② 一条能证明它**真的会红**的命令。
  **缺一即按 `NOT-RUN` 登记**,不得按「已实现」计入任何判据面。
  - ⚠️ **注释里声称「有断言」不构成门存在** —— 注释不是执行体。
  - ⚠️ **构建绿 ≠ 门跑了** —— 门链无错时零输出,无法区分「门跑了且通过」与「门没被调用」。
    确认门在生效须走**探针轮**(插无条件失败项 ⇒ 要求构建 `Failure`),判据见
    `CLAUDE.md` §构建前门(BuildGate)。
  - **立规缘由(2026-10-22 sprint-18 收口)**:S18-1 的 `AC ⑥`(规则九)靠一句
    「文件末尾有 AST 式断言」撑了**三轮**,实测该断言**从未存在**,全仓 `Editor.Tools.Gates`
    亦无规则九门 ⇒ AC ⑥ 一直是 `NOT-RUN` 却被当成已满足。
    这是本项目抓过的**第五个假绿形态**:不是「测试跑自造断言」,是
    **用一句自述顶替了一个从未存在的门**。

# 假绿形态登记表(2026-10-31 立 · sprint-21 `S21-13` · 承 retro-20 Process Improvement 2)

**用法:写夹具 / 判「这条 AC 算不算已被验证」之前,先来这里扫一遍。**
本表把散在 `tech-debt-register.md` / `retro-*.md` / story Completion Notes / `session-logs/`
里的假绿形态**集中到一处**,并给每一条一条**能当场跑的检出手法**。

> ⚠️ **按名字登记,不按序号** —— 这是本表立出来的第一条结论。
> 旧文档里「第五形态」至少被用过**两次**指两件不同的事
> (`retro-14:210` 指「扫不到被测装配」,`coding-standards §Review Evidence Standards` 指「自述顶替门」),
> 而 `tech-debt-register:3122` 的「前四种」又没有逐条具名。
> ⇒ **序号已不可靠,任何地方引「第 N 形态」都当「没有引用」处理。**

| # | 形态(名字即身份) | 症状 | 检出手法(**可机械跑**) | 已知实例 / 出处 |
|---|---|---|---|---|
| **F1** | **自造断言顶替 AC 语义层** | 夹具绿,但它断言的是**测试自己写的谓词**,不是生产行为;AC 的真实语义无人覆盖 | `grep` 该 AC **字段/符号的生产侧消费方** —— 零消费方 ⇒ 该 AC 未落地 | case-system 001–004 补评轮 |
| **F2** | **测试跑 Fake,生产类零命中** | 夹具用 `Fake*` 全绿,而被测**生产类**根本不存在于源码树 | `grep -rn "<生产类名>" unity/Assets --include=*.cs \| grep -v /Tests/` ⇒ **零命中** | 44 音频 epic 的 story-007/008/009 |
| **F3** | **自指判据(断言引用被测常量)** | 变异常量时**断言两边一起动** ⇒ 对「值本身」零判别力 | 断言里出现被测类的常量/方法名 ⇒ 改字面量;再变异常量,**必须转红** | `TD-clinic-19`(6 处 + 同轮抓出第 7 处) |
| **F4** | **空洞正控 / 无负控** | 正例恒真(空流、空集、`Is.Not.Null` 恒成立);或**正控从未调用被测循环** | ① 正例先加**前置断言**「构造确实改变了被测面」;② 负控必须存在且**独立于正例** | `TD-processing-01`(正例恒真)· `TD-processing-02`(无负控) |
| **F5** | **跳过冒充(扫不到被测装配)** | 症状是**跳过**,不是绿 ⇒ 连红的机会都没有 | 扫 `Assert.Inconclusive` / `[Ignore]` / `Assert.Pass` 的**理由**;对理由里点名的符号 `grep` —— **符号在场 ⇒ 该跳过是误报** | `retro-14:210` · `TD-epic-03`(原诊断错、后推翻留档) |
| **F6** | **自述顶替门(注释当执行体)** | 注释 / README / commit message 声称「有断言 / 有门 / 有校验」,而执行体**从未存在** | ① 要求 `file:line` + **一条能证明它会红的命令**;② 门是否真在链上须走**探针轮**(插无条件失败项 ⇒ 要求构建 `Failure`);**构建绿 ≠ 门跑了** | `S18-1` 规则九 AC ⑥(撑了三轮) |
| **F7** | **借绿(数字形态)** | ① 照抄**陈旧**全量回归数当「含新测的全量」;② 把**还没跑出**的数字先写成既成事实 | 等式判据:`total − 前次 total = 新增测试数`;数字必须带**三元组**(截至时刻 · 批次 · commit) | `sprint-16:266` · sprint-18 的假分母 `71` |
| **F8** | **行为等价掩盖结构分叉** | 断言只看行为输出,而实现**在符号面上已经分叉**(两处各写一份判据),恰好行为一致 ⇒ 全绿 | 证伪轮**改坏其中一处**(而非改坏行为),要求转红;改不动 ⇒ 该处是死代码或第二真源 | `S20-4`(`IsCandidate` 自写 `== None`,8 组合 iff 矩阵全绿) |
| **F9** | **`default(struct)` 绕过 fail-closed** | 构造器守卫生效,但 `readonly struct` 可经 `default(T)` 绕过构造器 ⇒ 「fail-closed 自述」对 `default` 不成立 | 每条 fail-closed 自述**单独对 `default(T)` 验一遍** | `S20-4`(消费侧补 `ValidateQuery` + 拆 M7/M8 各钉一处) |

**与 retro-20 的数目差(如实登记)**:卡面与 `retro-20:213` 写「已知 **8** 形态」。
按出处逐条核后本表是 **9** 条 —— 差额来自 F6 与 F5 在不同文档里**各自**被叫作「第五形态」,
计了其中一个、没计另一个。**这是账目差,不是新发现**:九条的出处都在上表最后一列。

**同族但不算假绿(别混读)**:`tools/falsify/README.md` 的「八种假存活」是**证伪轮自身的失败模式**
(共享 token 借绿 / 变异打错靶 / 拿证伪轮 XML 当基线 …)—— 它防的是「证伪轮没抓住东西」,
本表防的是「夹具/门本身看起来是绿的」。**两者互补,不合并。**

# AC 编号 —— 两个空间不许同名(2026-11-02 立 · 两轮裁定:①零改名 → 同日改判全 17 个改名)

**为什么单列一节**:`AC-{n}-{m}` 这个写法**天然歧义** —— `n` 可以是**系统号**,也可以是 **ADR 号**。
两个空间一旦撞号,`AC-20-15` 同时是「库存的 DTO 无 float」与「相机的 `Downed` 四语义」,
**两侧都是 BLOCKING**,而读者看到「`AC-20-15` 已满足」**无从判断说的是哪条**。
这类记录**可以两边都绿而含义相反**。

## 现行约定(2026-11-02 **两轮裁定** —— 先 ① 零改名,同日**改判为全 17 个改名**)

| 来源 | 写法 | 例 |
|---|---|---|
| **系统 GDD** 的 AC | `AC-{系统号}-{序号}`(**不变**) | `AC-20-15` = 库存的「输出 DTO 无 float」 |
| **ADR 自有**的 AC | **`AC-ADR{ADR号}-{序号}`**(2026-11-02 改名后) | `AC-ADR020-15` = 相机的 `Downed` 四语义 |
| ADR 文件**内部**自陈 | 同上(一律用新名,不留两套写法) | `adr-020:701` 的校验清单 |

**裁定沿革(不删,防误读)**:
1. 先裁 **① 零改名** —— ADR 自有 AC 引用时写 `ADR-0NN AC-NN`,存量不动;
2. **同日改判**为 **全 17 个改名**(`AC-20-01…17` → `AC-ADR020-01…17`),理由: <!-- ac-id-gate: legacy 规则文件示例/沿革 -->
   ① 只改写法解决不了「**裸串本身**有歧义」—— `grep AC-20-15` 仍会同时命中两侧;
   ② 只改 15/16/17 会留下 14 个撞号,门照样红;
   ③ 改装之后**从字符串层面就不可能再歧义**,而写法约定仍可能被人写错。

⇒ **本次改名只动 ADR-020 自有那 17 个**;系统 20 库存 GDD 的 `AC-20-01…25` **一字未动**。 <!-- ac-id-gate: legacy 规则文件示例/沿革 -->
判定依据 = **语义**,不是编号(同一个 `AC-20-10` 在 `BootRoot` 里是「AudioListener 单挂点」,
在 `InventoryContainerScreen` 里是「禁负重条」—— 后者不动)。

## 机械门:`tools/acid/acid_check.py`

**判据**:**同一个 `AC-ID` 不得被两个文件「定义」**。

### 「定义」按**编号空间**分工(S23-37 起)

| ID 形状 | 谁能定义 | 句式 |
|---|---|---|
| `AC-<sys>-<n>` | `design/gdd/`(顶层,不含 `reviews/`) | 表行 `\| **AC-20-01** \|` · 复选框 `- [ ] **AC-20-01**` · **`## Acceptance Criteria` 节内项目符号** `- **AC-1-01(BLOCKING)**` |
| `AC-ADR<adr>-<n>` | `docs/architecture/adr-<adr>-*.md`,且 adr 必须 = 本 ADR 号 | 表行 · 复选框 |

**ADR 里出现 `AC-<sys>-<n>` 一律算引用,不算定义** —— `## Validation Criteria` 列的是
「本 ADR 验证哪些 AC」。实测依据:全库 **0 个 ID 只由 ADR 定义**(两边都有的 9 个全是
ADR-018 复述 44 GDD 的 `AC-44-*`,不是第二定义)。铸号由 **C7 / C10** 兜。

### 记法即判据:**粗体 = 定义形,反引号 = 引用形**

同一个 AC ID,写在**粗体**里是「我定义它」,写在**反引号**里是「我引用它」。这不是排版偏好 ——
**C1 / C10 就是按这个分的**:

```markdown
- [ ] **AC-44-01(BLOCKING)**:…     ← 定义形(在 ADR 里写这个 ⇒ C10 红)
- [ ] `AC-44-01`(BLOCKING):…       ← 引用形(ADR 验证某个 GDD 的 AC 时正确写法)
```

⇒ **ADR 引用系统 AC 一律用反引号**,**行内提及也算** —— `本 ADR 见 **AC-44-08** 一条` 一样红。ADR 自有的 AC 用 `AC-ADR0NN-NN`(粗体或复选框都行,那是它自己的号)。

⚠️ **项目符号句式是 2026-11-02 补的**:系统 **1 / 2 / 44 / 25** 的 GDD 用
`- **AC-1-01(BLOCKING)** ——` 定义 AC,此前 C1 **完全不认**,**72 条 AC 不受保护**。
补的时候必须**限定在 `## Acceptance Criteria` 节内** —— 放开到全文实测多出 16 个「撞号」,
其中 13 个是 review log 与 ADR 复述。

**`## GDD Requirements Addressed` 整节跳过**(那是引用别人的 AC,不是定义 —— 首版原型实测踩到过)。

| # | 判据 | 红在哪 |
|---|---|---|
| C1 | **新撞号**:某 ID 被 ≥2 文件定义且不在基线 | 撞号当场可见 |
| C2 | **基线不得扩大**:基线 ID 不得被第三个文件再定义 | 老问题不许变大 |
| C3 | **基线不得陈旧**:基线 ID 必须**仍**是撞号 | 有人改名后基线会腐烂,迫使同步 |
| C4 | **空集守卫**:扫描到 0 条定义行 ⇒ 红 | 路径写错时 C1 会在空集上恒绿(空洞正控) |
| C5 | **引用面混写**:同一文件里同时出现 `AC-ADR<n>-x` 与**同名**裸 `AC-<n>-x` ⇒ 红 | 改名漏改 / 新写误用的指纹 |
| C6 | **白名单陈旧**:`BARE_MIXED_ALLOW` 里的文件已不再混写 ⇒ 红 | 白名单不许永久掩护一个已消失的混写 |
| C7 | **ADR 用裸名铸 AC**:`adr-<n>-*.md` 里以定义句式出现 `AC-<n>-<m>` ⇒ 红 | **撞号的生产线**。C1 要等系统 GDD 成文才发作,C7 在铸的当时就红 |
| C8 | **裸名误引 ADR 自有 AC**:活文档里裸 `AC-<n>-<m>` 实为 ADR 自有 AC ⇒ 红 | C5 的盲区:文件里一处新名都没有时 C5 **结构性沉默** |
| C9 | **story 级 AC 旧形**:出现 3 位编号的 `AC-0NN-M` ⇒ 红 | 三个编号空间靠**字符串**分开(story 一律 `SAC-0NN-M`) |
| C10 | **ADR 用了系统 AC 的定义形**:ADR 里以**粗体**写 `AC-<sys>-<n>` ⇒ 红(**行内任意位置都算**,且不看 GDD 有没有) | 读者分不出「复述」还是「第二定义」—— 同号异义正是 ADR-020 当年的形态 |

🚨 **C5 的天花板(2026-11-02 实测,别读成「门绿 = 没有陈旧引用」)**:
C5 判的是「**混写**」,不是「**正确**」。一个文件若**一处新名都没有**,只是把 ADR 自有的 AC
全写成裸名 ⇒ C5 **结构性沉默**。实测代价:`.claude/docs/technical-preferences.md` 的 7 处
(ADR-020 日志条目)与 5 个 story/GDD 的「`ADR-020 AC-20-03` 前缀式」共 **8 处**就是这样漏过去的 <!-- ac-id-gate: legacy 规则文件示例/沿革 -->
—— **门一直是绿的**。⇒ 「引用面普查」仍需人工列名单,门只兜「改名做到一半」那一半。
反向佐证:修完 `story-005:39` 后 `:87` 仍是裸名 ⇒ **C5 当场转红**,门在「同文件两种写法并存」时确实有效。

⚠️ **C5 必须按「完整 ID」比,不能只比系统号** —— 一个文件里有 `AC-ADR020-05` 和另一条
无关的 `AC-20-18`(库存的)**不构成混写**,两者指的不是同一条。首版原型按系统号比,
把 17 个文件全判成混写(**实测踩到**,由 C6 的白名单陈旧报错反查出来的)。
⚠️ 编号零填充两侧都要容忍(`AC-20-15` 与 `AC-20-9` 是同一 ID 的两种写法)。

### C8 的两条路径

| 路径 | 判据 | 强度 |
|---|---|---|
| **C8a** | 裸 `AC-<n>-<m>` 在**系统 n 的 GDD 里没有定义**、只作为 ADR-n 的自有 AC 存在 ⇒ 必是 ADR 引用 | **精确**(不看上下文) |
| **C8b** | 系统 n 确实定义了同号,但**同一行**点了 `ADR-<n>` ⇒ 极可能指 ADR | **启发式**,可能误报 |

🚨 **C8a 只认 1–2 位的编号位(`AC-(\d{1,2})-…`)** —— 见下节。

### 三个编号空间:靠**字符串**分开,不靠约定

| 写法 | 归属 | 例 |
|---|---|---|
| `AC-ADR0NN-NN` | **ADR 自有 AC** | `AC-ADR019-06` |
| `AC-<sys>-<n>` | **系统 GDD 的 AC** | `AC-20-03`(库存) |
| `SAC-0NN-M` | **story 级 AC** | `SAC-018-4` |

**这三条不是风格偏好,是 2026-11-02 抓到的两次真实事故的结论**:

- **ADR-020 vs 系统 20**:17 个 ID 两边同义异,`AC-20-15` 两侧都是 BLOCKING ⇒ ADR 侧改名。 <!-- ac-id-gate: legacy 规则文件示例/沿革 -->
- **ADR-019 vs 系统 19**:8 个 ID 潜伏(GDD 未成文)⇒ 同上改名。
- **story 级 AC**:原写 3 位 `AC-0NN-M`(632 处 / 95 文件 / 9 个 story 号)。
  它与 2 位写法**肉眼同形** —— `AC-019-6`(story-019 第 6 条)与 `AC-19-06`(ADR-019 第 6 条) <!-- ac-id-gate: legacy 规则文件示例/沿革 -->
  只差一个 `0` 的位置,而系统 9 / 18 / 19 / 20 / 21 **都存在** ⇒ 全部改名为 `SAC-0NN-M`,
  旧形由 **C9** 一律拒收。

⚠️ **C8 因此必须排除 3 位**:首版把 3 位也当系统/ADR 号,**10 个 audio 文件全部假阳性**
(实测踩到)—— story 级的 `AC-019-6` 被误读成 ADR-019 的 `06`。 <!-- ac-id-gate: legacy 规则文件示例/沿革 -->

### 行级 `legacy` 标记(历史引用的合法出口)

「原文不删」要求记录件保留改名前的写法,它们若落在活文档里就会被 C5 / C8 判成漏改。
处置是**行级标记**而不是**文件级白名单**:

```markdown
… ADR-019 的 `AC-19-01…08` ↔ 系统 19。 <!-- ac-id-gate: legacy 历史引用 -->
```

| 理由 | 说明 |
|---|---|
| 豁免范围 = **那一行** | 不会顺手掩护同文件里将来真漏改的地方 |
| **自清理** | 行删了标记跟着没 → 不存在「白名单腐烂」 |
| **可见** | 每次 `--check` 都把带标记的行列进「提示」,**不是静默豁免** |

⚠️ **只给「记录历史」用**。新写的引用**不许**靠它过关 —— 那会变成「注释顶替门」的反向形态。

```bash
python3 tools/acid/acid_check.py --check           # 违规即退出码 1
python3 tools/acid/acid_check.py --print           # 打印撞号全貌
python3 tools/acid/acid_check.py --mutation-probe  # C1–C8 各自须红(CI 常驻)
```

**基线** = `tools/acid/acid-baseline.json`。**2026-11-02 改名轮后为空** ——
17 个撞号已从 ID 层面消除,不再需要冻结。(改名前的 17 条冻结记录见
`production/qa/evidence/impl-td-docs-01-ac-id-gate-2026-11-02.md` §3。)

⇒ **今日的门是「零容忍」**:任何 ID 被两个文件定义即红,没有白名单。
基线机制保留,**非空即说明又出现了被裁定的例外** —— 那时基线只减不增,
且 C3 会在例外消失后逼你把条目删掉。

**CI**:`.github/workflows/tests.yml` 的 `ac-id-collision` job(`--check` + `--mutation-probe` + 零写盘校验)。

> **立规缘由**:`TD-docs-01`(2026-11-02 立)。ADR-020 的 `AC-20-01…17` 与系统 20 库存 GDD 的 <!-- ac-id-gate: legacy 规则文件示例/沿革 -->
> `AC-20-01…25` **完全重合 17 个 ID**。时间顺序可核:库存侧 2026-10-26 就已在被引用
> (`gate_wiring_manifest_test.cs:98`),而 **ADR-020 Amendment C(2026-11-01)铸新 ID 时没查重**
> ⇒ 不是「历史遗留没办法」,是「新 ID 没人查重」。同一病理的第三例
> (先例:`story-008-death-candidate-sink.md:63` 记 ADR-009 ↔ 系统 9;
> 潜伏:`ADR-019` 的 `AC-19-01…08` ↔ 今日尚无 GDD 的系统 19)。 <!-- ac-id-gate: legacy 规则文件示例/沿革 -->
>
> 🔁 **2026-11-02 更新:这条「潜伏」也已拆除** —— ADR-019 的 8 条自有 AC 全量改名为
> `AC-ADR019-01…08`。⇒ **全库已无 ADR 自有 AC 以裸名存在**,C1 的撞号面从「定义」这一侧被清空。

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
