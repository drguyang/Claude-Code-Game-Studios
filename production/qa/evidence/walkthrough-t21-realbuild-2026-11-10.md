# T21 —— 真包走查(Development 构建)证据原件

**日期**:2026-11-10
**批次**:T21(用户指令「开工 T21 真包走查」;本批补做受用户「先做玩家格走查辅助」推动)
**构建**:`unity/builds/Linux-x64/大医精诚.x86_64`,Development,**2026-11-10 17:25** 产出
  (`PlayerBuildCli.BuildDevelopment`,`Build Finished, Result: Success`,206 MB,47 s)
**运行**:`DISPLAY=:0`,窗口 1280×800,存档根 = `~/.config/unity3d/DaYiJingCheng/大医精诚`
  (**走查前把原 `saves/` 移开,走查后原样还原**;三轮走查存档分别留在
  `saves.t21-walkthrough/` · `saves.t21-close-run/` · `saves.t21-reject-run/`)
**驱动**:X11 XTEST(`python-xlib`),截图 = `XGetImage` + PIL

> **本件分两批写**:前一批(17:30–17:52,即上一份报告的所据)**只有负例与 UI 屏**;
> 本批开了两次进程 —— **p2**(18:17–18:32,走通阳性主链并提交结案)、
> **p3**(18:36–18:42,独立复跑:立案后**不勾就提交**,取拒绝路径),
> 两次都带 `-tutorialdiag`。下面 §1.1 的条目在上一批里被记为**未验证**,
> 此处**不删原判**,逐条标明改判理由与证据。

---

## 1. 已验证(截图 + 文件系统双据)

| # | 判据 | 证据 |
|---|---|---|
| 1 | 主菜单渲染 4 行(开始诊治 / 新起一局 / 设置 / 退出游戏),`Enter` 进入世界 | `01-boot.png` → `02-after-enter.png` |
| 2 | 世界载入;急救候选列表常驻(止血 / 通气 / **跳过**);病历字幕「【病人】咳 咳咳」 | `02` / `05-keys.png` |
| 3 | `Escape` → **暂停壳 6 行**:继续 / 保存 / 上次保存: N分钟前 / 存档位 / 调治 / 返回主菜单 | `07-esc.png` · 本批 p2 轮 `27-pause-run2.png`(「上次保存: **2 分钟前**」) |
| 4 | **保存真的落盘**:键盘 `Down`+`Enter` ⇒ `save_auto.bin` 覆写(见 §6 复算) | `08-after-save.png` |
| 5 | **存档位屏(7b)**:卡片「第 0 日 晚上」+ 槽「留档」+ **真实落笔时刻 `2026-11-10 17:38:24`** + 页码 `1 / 1` | `08-after-save.png`(`ADR-042` 的 `SavedAtUtcTicks` 在真包成立) |
| 6 | **7b 屏上按 `Escape` 回到暂停壳**(ADR-013 记录的粗糙边) | `09-esc-from-7b.png` |
| 7 | **`M` → 手册近景**:「望诊四要 / 望神、望色、望形、望态。/ 第 1 页 / 共 7 页」 | `17-M-manual.png`(T18 甲 的开屏入口在真包可用) |
| 8 | **`C` 在无开案时不开空页**(`AC-CC-01` 的真包负例) | `18-C-close.png`(世界原样,零模态) |
| 9 | 玩家移动 `W = +z` 成立:格 `(0,0,-1)` → `(0,0,17)` | 存档格导出前后两读 |
| 10 | 52 掷骰→降临→病人真发生:`EventArrived 2002 @ tick 6000`,病人 3(病种 7)锚点 `(1,0,14)` | `Logs/celldump-t21b.log` |
| 11 | **退出保存路径真的跑**:进程终止后出现 `save_exit.bin` 与 `session_incomplete.flag` | `ls saves/w1`(18:32 与 18:42 两轮各一次) |
| 12 | 全程 **0 条 `Exception:`**(`grep -ac` = 0,两轮日志各一次) | `p2.log` / `p3.log` |

### 1.1 本批补做 —— 此前记为「未验证」的阳性主链

| # | 判据 | 证据 |
|---|---|---|
| 13 | **玩家能精确落到交互半径**:`W` 3.2 s + `D` 0.35 s ⇒ 格 `(0,0,14)`,`距离=1`(病人 #1 在 `(1,0,14)`) | `p2.log` / `p3.log` 的 `[tutorialdiag] … cell=(0,0,14) … 最近=#1 距离=1` |
| 14 | **`E` → 立案 → 脉案**:屏上开出五通道页(面色 / 语声 / 姿态 / 呼吸 / 触感 + 问诊 / 前案 / 后案,病名「?」) | `20-E-casebook.png` · `24-E-casebook-run2.png` |
| 15 | **立案真的落流**:`CaseOpened: patient 1 · tick 1162`(p2 轮) / `tick 1229`(p3 轮) | `Logs/celldump-t21b.log`(p2 档) / `-t21e.log`(p3 档) |
| 16 | **`F` → 急救/处置视角**:镜头推近到病人,候选列表随位 | `21-F-treatment.png`;教学链 `★发火 tick=1202 steps=treat` |
| 17 | **`C` → 结案页摊开**:纸页标题「结案」,行「已处置」/「结案」,焦点落墨勾行 | `19-C-caseclose.png` · `25-C-caseclose-run2.png` |
| 18 | **墨勾可落**:`Return` 于墨勾行 ⇒ 行内出现「✓」 | `22-ink-checked.png` |
| 19 | **提交后合页**:`Down` + `Return` ⇒ 回到世界(模态 `None`,档位 `Explore`) | `23-after-submit.png` + `p2.log` 尾部读数 |
| 20 | **`CaseClosed` 真的落盘**:`CaseClosed: patient 1 · case (1162,1,0) · treated=True · tick 1933` | `Logs/celldump-t21d.log`(`CELLDUMP_SLOT=3` 读手动 3 号槽) |
| 21 | **教学 `close` 步发火**:`★发火 tick=1933 steps=close 口述=1`(与 #20 同 tick) | `p2.log` |
| 22 | **拒绝路径(淡墨行)**:未勾就提交 ⇒ 页**不关**、下方出现「未勾「已处置」」、**零写入** | `26-reject-ink.png` + `Logs/celldump-t21e.log`(`CaseOpened 1 条 · **CaseClosed 0 条**`) |
| 23 | **设置壳(调治)打开**:纸页标题「调治」+ 两个空行位(43 §5.2 的内容归 44 / narrative,`TD-skeuo-11` 内容半边仍 open ⇒ **形态如此,不是缺陷**) | `28-settings-shell.png` |

**#20 与 #21 是同一事实的两条独立腿**,刻意都留:
- #20 是**落盘字节**(离线读 `save_3.bin`,走 `SaveService.Load` 的正常路径);
- #21 是**运行期投影**(`TutorialStreamFeed` → `TutorialSnapshotBuilder` → `TutorialProjection`,
  `close` 步的 `predicateKind` 逐字是 `CaseClosed`,见 `assets/data/48_tutorial_content.json:45`)。
两条腿都指 `CaseClosed`,但**任一条单独都不够**:#21 单独说不清是否进了档,#20 单独说不清
「提交那一刻是否真的走了写者」(写入后立刻合页也可能是别处写的)。

**#22 是 #19 的负控**:同一个屏、同一个提交行,只有墨勾态不同 ⇒ 一条写、一条不写。

### 1.2 截图为什么不随仓走

按 **2026-11-06 用户指令**(`.gitignore:344-350`):证据**正文**入库,截图**只留本地**
(理由:截图是过程留痕,判定价值在正文的判据与复算命令里;且每轮几十张 PNG 会让仓库体积
与 diff 噪声线性膨胀,而这些图**不可复现地绑定当时的构包**)。

⇒ 本文所有 `NN-*.png` **都是本地文件**,全在 `unity/Logs/shots/t21/`(`.gitignore:257` 忽略)。
其中 `22b-ink-crop.png` / `26b-reject-crop.png` 是墨勾行与拒因行的 3 倍裁切版(小字原图看不清)。
想复现观感只能按 §6 重跑一次走查,**不是拉仓就能看到**。

---

## 2. 仍未验证(并把原因写死,禁借绿)

1. **手柄全程未测**(本机无手柄):`AC-42-F1` 的「手柄单机走查」一条都没执行,
   `AC-CC-08`(手柄走完开屏 → 勾 → 提交 → 合页)也因此仍是 `NOT-RUN`。
2. **`AC-CC-03`(墨勾可擦除)只在逻辑腿验过**:真包里落了勾(#18)但**没再落一次看它消失**;
   EditMode 夹具 `test_inkRow_submit_flipsInkState` 覆盖了擦除,真包未复现。
3. **视觉判读有限**:世界画面几乎是一整片灰/绿平面(无道具、无病人轮廓),截图对「场景是否对」
   几乎不提供信息;本轮的视觉判据只覆盖 **UI 屏**。
4. **`case-close-42.md` 仍未经 `/ux-review`** ⇒ `AC-42-F1` ⑩ 按纪律维持 `NOT-RUN`
   (本轮的截图能支持「屏长得像 spec」,但**不能**替代评审)。
5. **`F` 键的相机档位语义未逐档验**:只看到「推近到病人」这一形态,没验退出档位是否回 `Explore`
   (读数尾部 `档位=Explore` 支持,但那是按 `C` 之后)。

### 2.1 观感与 spec 的两处出入(留给 `/ux-review`,本轮**不改**)

| # | spec 写的 | 真包看到的 | 判断 |
|---|---|---|---|
| A | `case-close-42.md` §4 布局图:墨勾与标签**同一行**(`□ 已处置`) | 墨勾位在**标签下一行**,左对齐的小字形「✓」 | 逻辑上成立(`AC-CC-03` 的「勾 / 擦」可判),但**形态与 spec 图不一致** ⇒ 要么改 spec 图,要么改屏,归 UX 轮 |
| B | §5「一行**淡墨**字」 | 拒因行「未勾「已处置」」在截图里与正文**几乎同墨色**,小字号下分不出「淡」 | 同上 —— 纸面浅色底上这行不显眼,「淡」到什么程度需 `/ux-review` 定 |

⚠️ A / B 都是**观感项**,不是缺陷判定:本轮的判据是「屏上有这个东西且能被真实输入驱动」,两条都满足。

---

## 3. 走查辅助:本轮之前就已存在,上一批却没启用

上一批把「看不到玩家格」写成了**缺工具**(见旧版 §4 ①)。本批复核源码后,
该工具 **2026-11-06 就已落地**,上一批是我**没带旗标**:

| 工具 | 位置 | 作用 | 上一批为何没用上 |
|---|---|---|---|
| **`-tutorialdiag`** | `Gameplay.Boot/WalkthroughDiagnostics.cs`(`Flag = "-tutorialdiag"`) | 每 tick 边沿一行现场读数:`cell=(x,y,z)` · 在场病人与各自格 · `最近=#k 距离=d` · `模态=` · `档位=` · `路由=` · `教学facts=`;教学步发火时另打 `★发火 … steps=` | 我按缺省参数启动,**没读这个类的文件头** |
| **`SaveCellDumpCli`** | `Editor.Tools.Spike/SaveCellDumpCli.cs`(Editor-only,不进构建) | 离线读落盘存档:玩家格 / 病人锚点 / `DiseaseOnset` / 立案 | 上一批用了,但它**只打立案不打结案**,也**写死自动槽** |

**本批对 `SaveCellDumpCli` 的两处补强**(本批暴露的缺口,均已在文件头注明缘由):

1. **补打 `CaseClosed` / `CaseReferred`** —— 结案页提交后,原工具看不出事件是否落了,
   只能绕道读 `★发火 steps=close`(见 §1.1 #20/#21 的关系)。现在「一对事件成对打印」。
2. **允许 `CELLDUMP_SLOT` 指定槽号** —— 原工具写死 `AutoSaveSlot`;走查里手动保存落的是
   `save_3.bin`,于是本批第一次导出读的是 5 分钟前的自动档(`事件总数 = 2`,不是 30)。
   现 `CELLDUMP_SLOT=1/0/N` 分别读自动 / 退出 / 手动槽,缺省不变。

⚠️ 这是**工具**的改动,不是产品代码;两支都不进构建(`Editor.Tools.Spike` 的 `includePlatforms = ["Editor"]`)。

---

## 4. 走查方法学:`按下鼠标 ≠ 激活那一行`

| 现象 | 复现 | 归因 |
|---|---|---|
| 点「调治」行 → 暂停壳**关掉** | `10-settings.png`(行高亮) → `11-after-enter-settings.png`(壳关闭) | XTEST 指针确实到面板,`Submit` 却作用在**逻辑焦点行**(仍是「继续」)⇒ **点击只改视觉高亮、不改焦点** |
| 点「保存」位置却开出**存档位** | 点击 `(904,576)`,结果进 7b | 与上同族:点击的命中行与焦点行不一致(记忆 `project_xtest_pointer_aim_limit` 的同一现象) |

⇒ **走查应以键盘导航为主**(`Down` / `Return` 两轮全部可靠:进世界、保存、开结案页、提交、开设置壳都靠它)。
⚠️ 这是**走查手段**的结论,不是产品缺陷的判定 —— 真人用鼠标时是否同样错位,**未验**,
不得据此写 bug。

### 4.1 两条本轮新踩到的坑(下一轮别重犯)

| 坑 | 症状 | 正确做法 |
|---|---|---|
| **`pgrep -x 大医精诚.x86_64` 恒不命中** | 用它判「进程是否还在」永远得到「已退出」 | Linux 的 `comm` 被截到 **15 字节**,中文名截成 `大医精诚.x8`。用 `pgrep -af "大医精诚.x86_64"` 并排除 `pgrep` 自身 |
| **模态摊开时诊断读数**停下** | 每 1 s 一行的 `[tutorialdiag]` 突然不打了,像是进程死了 | 挡路模态摊开 ⇒ **真停 sim tick**(ADR-013 §十-C Amendment D)⇒ tick 边沿读数自然停。此时只能看**截图**判断模态开没开 |

---

## 5. 环境卫生

- 玩家原有存档在走查前**整体移开**(`saves → saves.bak-t21b`),三轮走查结束后**原样移回**
  (`saves/` 现为 `w1` + `w2` + `worlds.index`,mtime 2026-11-06/08,与走查前一致);
  三轮走查产生的存档分别留在 `saves.t21-walkthrough/` · `saves.t21-close-run/` · `saves.t21-reject-run/`(不删,留作证据)。
- 上一批进程以 `SIGTERM` 结束(**非**走 UI 退出),因此 `session_incomplete.flag` 被写下 ——
  这是 AC-7a-17 的标记,如实记录,不当异常。本批 p3 走的是 UI 的「退出游戏」
  (`[BootRoot][主菜单] 玩家选择退出游戏`),同样留下 `session_incomplete.flag`
  ⇒ **该标记不区分两种退出方式**,是既有行为,不是本批引入。
- ⚠️ `pkill -f "大医精诚.x86_64"` 会**命中包装 shell 自身**(记忆 `feedback_monitor_pgrep_selfmatch`);
  上一批实际被自己的 pkill 打断过一次(exit 144)。本批改用 `kill -TERM <精确 pid>`。

---

## 6. 复算

```bash
# 构建产物时间
ls -l unity/builds/Linux-x64/大医精诚.x86_64

# 三轮走查存档
ls -l ~/.config/unity3d/DaYiJingCheng/大医精诚/saves.t21-{walkthrough,close-run,reject-run}/w1

# 用户存档已还原(期望 w1 / w2 / worlds.index,且 mtime 不是今天)
ls -l ~/.config/unity3d/DaYiJingCheng/大医精诚/saves/

# 零异常(两份日志各期望 0)
grep -ac "Exception:" /home/gu/.claude/jobs/6001633c/tmp/t21/p2.log
grep -ac "Exception:" /home/gu/.claude/jobs/6001633c/tmp/t21/p3.log

# 教学三步发火(期望三行:observe / treat / close)
grep -a "★发火" /home/gu/.claude/jobs/6001633c/tmp/t21/p2.log

# 结案真的落盘(期望 CaseClosed 1 条 · treated=True · tick 1933)
grep -a "\[CellDump\]" unity/Logs/celldump-t21d.log

# 拒绝路径零写入(期望 CaseClosed 0 条)
grep -a "\[CellDump\]" unity/Logs/celldump-t21e.log
```

> ⚠️ 两读数需要 `CELLDUMP_SLOT=3`,且**对应的世界目录必须还在 `saves/` 下** ——
> 上面两条命令是在「把 `saves.t21-close-run` / `saves.t21-reject-run` 临时换名成 `saves`」
> 的窗口里跑的。事后复算时须重做这一步(见 §5 的目录清单)。

---

## 7. 对 `AC` 面的影响(不借绿)

| AC | 本轮之后 | 依据 |
|---|---|---|
| `AC-CC-01` 无开案不开空页 | 真包**负例**成立 | `18-C-close.png` |
| `AC-CC-02` 有开案 ⇒ 开页 + 焦点墨勾行 | 真包**成立** | `19` / `25` |
| `AC-CC-03` 墨勾勾 / 擦 | 仅逻辑腿 | §2 ② |
| `AC-CC-04` 未勾提交 ⇒ 不关页 + 淡墨行 + 零写入 | 真包**成立** | `26` + `t21e.log` |
| `AC-CC-05` 勾 + 有处置 ⇒ `CaseClosed` 恰一条 + 合页 | 真包**成立** | `20` + `21` |
| `AC-CC-06` 取消 ⇒ 合页零写入 | 仅逻辑腿(真包里按过 `Escape` 合页,但没同时验零写入) | §2 |
| `AC-CC-07` 屏上无数量 / 进度 / 病种 | **目视**通过(纸页只有三行文字);禁词扫描是夹具面,未在本轮重跑 | `19` / `25` / `26` |
| `AC-CC-08` 手柄全程 | `NOT-RUN` | §2 ① |
| `AC-42-F1` ⑩ | `NOT-RUN` 不变 | `case-close-42.md` 未评审 |

> ⚠️ 上表是**真包走查**这一路的补充,不是「AC 已签核」——
> `case-close-42.md` 的 AC 表仍全 `[ ]`,签核归 `/ux-review` + `/story-done`。

---

## 8. 回归(三元组)

本批的**源码改动只有一件**:`unity/Assets/Editor.Tools.Spike/SaveCellDumpCli.cs`(Editor-only,不进构建)。
仍按惯例跑全量两套,证明「至少没有把既有夹具弄坏」。

| 套件 | 结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode(首跑) | `7392 / 7355 / 0 / 0 / 37` | `2026-11-10T10:50:30Z`–`10:52:34Z` | T21 走查补轮 | `34e1da7c` + 工作树 |
| PlayMode | `376 / 371 / 0 / 2 / 3` | `2026-11-10T10:53:51Z`–`10:54:13Z` | T21 走查补轮 | `34e1da7c` + 工作树 |
| **EditMode(终态复跑)** | `7392 / 7355 / 0 / 0 / 37` | `2026-11-10T10:59:20Z`–`11:01:25Z` | T21 走查补轮 | `34e1da7c` + 工作树(终态) |

> **为什么 EditMode 跑了两遍**:首跑之后我改了一处 —— `ResolveSlot` 的下界由 `seq >= 0`
> 改成 `seq >= SaveSlotLedger.ExitSlotSeq`,因为复核发现槽号语义是
> **`1` = 退出档 / `2` = 自动槽 / `≥3` = 手动槽**(`SaveSlotLedger.cs:37/40/49`),
> 而我第一版注释把它写成了「0 = 退出档」。改的是**工具**(Editor-only),
> 但既然动了树就重跑一次,让上面的数字对应**最终**状态 —— 两次结果相同。

基线(T20 第二批):EditMode `7392 / 7355` · PlayMode `376 / 371`。
**等式**:两套**零变化** —— 本批没有新增 `[Test]`,也没有既有夹具转红/转绿。
⚠️ 这**不构成**对 `SaveCellDumpCli` 改动的判据:它是走查工具,零**测试**引用
(`grep -rl "SaveCellDumpCli" unity/Assets --include=*.cs` 只命中自身与一处**注释**引用 ——
`WalkthroughDiagnostics.cs:6` 的散文,不是调用)。
它的判据是 §1.1 的 #20 与 #22 —— **两份导出各自打出了对方打不出的那一行**。

```bash
# 证明「零测试引用」(期望两行:自身 + WalkthroughDiagnostics.cs:6 的注释)
grep -rn "SaveCellDumpCli" unity/Assets --include=*.cs
```
