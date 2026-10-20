


# Claude Code Game Studios -- Game Studio Agent Architecture

Indie game development managed through 54 coordinated Claude Code subagents.
Each agent owns a specific domain, enforcing separation of concerns and quality.

## Technology Stack

- **Engine**: Unity 6.3 LTS
- **Language**: C#
- **Rendering**: URP (Universal Render Pipeline)
- **Physics**: PhysX (默认 3D 物理);DOTS 侧走 Unity Physics
- **XR**: OpenXR (VR 仅用于急救动作小游戏)
- **Version Control**: Git with trunk-based development
- **Build System**: Unity Build Pipeline (Unity Build Automation for CI)
- **Asset Pipeline**: Unity Asset Import Pipeline + Addressables

> **Note**: Engine-specialist agents exist for Godot, Unity, Unreal, and Babylon.js.
> Use the set matching your engine. `unity-specialist` is the primary for this project;
> see `.claude/docs/technical-preferences.md` for the full routing table.

## Project Structure

@.claude/docs/directory-structure.md

## Engine Version Reference

@docs/engine-reference/unity/VERSION.md

## Technical Preferences

@.claude/docs/technical-preferences.md

## Coordination Rules

@.claude/docs/coordination-rules.md

## Collaboration Protocol

**User-driven collaboration, not autonomous execution.**
Every task follows: **Question -> Options -> Decision -> Draft -> Approval**

- Agents MUST ask "May I write this to [filepath]?" before using Write/Edit tools
- Agents MUST show drafts or summaries before requesting approval
- Multi-file changes require explicit approval for the full changeset
- No commits without user instruction

See `docs/COLLABORATIVE-DESIGN-PRINCIPLE.md` for full protocol and examples.

> **First session?** If the project has no engine configured and no game concept,
> run `/start` to begin the guided onboarding flow.

## Coding Standards

@.claude/docs/coding-standards.md

## Context Management

@.claude/docs/context-management.md

## Unity Debugging (CLI First)

**凡 Unity CLI 能确认/复现/调试的,一律自己跑,不要让用户手动操作编辑器。**
- 已知 bug / 自检 / 测试 → 用 `unity build <project> --target StandaloneLinux64 --executeMethod <类全名>` batch 模式直跑,
  日志落 `unity/Logs/build-*.log`,自行 grep 判决行。
- batch 需独占工程 ⇒ 先 `pgrep` + 查 `unity/Temp/UnityLockfile` 确认编辑器已关;
  跑完提醒用户可重开编辑器。
- 只有「必须图形界面/手动交互才能复现」的问题才请用户操作编辑器。
- **`-projectPath` 必须指向 `unity/`,绝不能用仓库根(2026-10-19 实测踩坑)**:
  仓库根**也有** `ProjectSettings/` + `Packages/`(git 跟踪),看起来像个工程,但**没有 `Assets/`**
  ⇒ 写成 `-projectPath .` 且 cwd 是仓库根时,Unity 会在**仓库根**造出 `Library/` `Logs/` `Temp/`
  和一串 `mono_crash.mem.*.blob`,然后在**没有 Assets 的空工程**上反复初始化、主线程空转不收敛。
  **正确写法**:先 `cd unity`,再 `-projectPath "$PWD"`(或任何指向 `unity/` 的绝对路径)。
  ⚠️ 相对路径(如 `-projectPath .`)会按 cwd 解析,而 cwd 可能被 shell 或工具链改掉 —— **不要依赖它**。

### 构建前门(BuildGate)—— 绿 = 没报错,**不等于**门跑了

**判据差一层**:门链无错时零输出,所以 `Build Finished, Result: Success.` 只证明
`errs.Count == 0`,**无法区分**下面两种情况:

- ① 门真的跑了,扫过 DTO / DLL,没发现问题 ⇒ 绿(**有效**);
- ② 门**根本没被调用**,`errs` 恒为空 ⇒ 绿(**假绿**)。

⇒ **凡要确认「某个 `IPreprocessBuildWithReport` 门/接线真的在生效」,不能只看构建绿,
必须跑一次探针轮**:在门里插一条**无条件** `errs.Add("[PROBE]…")` 并提前 `return`,
重跑同一构建,要求 **`Result: Failure`** 且日志里出现该探针串。

**探针轮的归因三件套(缺一即不算证明)**:

| # | 要素 |
|---|---|
| 1 | 探针串出现在**构建日志**里 |
| 2 | 它的 `Debug.LogError` 栈帧上是目标类的 `OnPreprocessBuild`(给出行号) |
| 3 | 构建**因它失败**:`BuildFailedException` + utp `steps` 在 `Preprocess Player` 即止 |

⚠️ **栈帧给的是 `Debug.LogError` 行,不是接线行本身**。若要用「批次模式下唯一调用点」
论证接线行被执行(例:`RunMenu` 的接线在 batch 下不触发),**须明写这是论证而非观测**,
不要把它写成直接证据。

⚠️ **还原判据 = SHA-256 逐字节**,不是 `grep` 探针串(探针串可以改名,哈希不行)。
探针轮前**声明变异窗口 + 点名被变异文件**,窗口内**不读**该文件(承 `tools/falsify/README.md`
§并发读取禁令)。

> 立此节缘由(2026-10-21 C3 轮):两个 IL 门接进 `BuildGate` 后,源文本级判据只证了
> 「接线在源码里且删掉会红」,**没证构建时真的跑通**。补三轮(干净 = `Success`;
> 两条独立探针各自 = `Failure`)才把这条 NOT-RUN 补掉。
> 同轮的连带教训(git 改动三分类 / `.gitignore` 窄 glob / commit message 订正)见
> `.claude/docs/coding-standards.md` §工作树卫生。
