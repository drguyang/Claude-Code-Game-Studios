# 真包走查 · S25-4c 教学链是否真的出声出字幕(2026-11-06 19:59–20:10)

| 字段 | 值 |
|---|---|
| **目的** | 回答「教学口述在真包里有没有出声、出字幕」—— S25-4c 只有代码面证据(`AddressablesDataProvider` 分支走不到 EditMode),必须真包确认 |
| **包** | `unity/builds/Linux-x64/`,`Build Finished, Result: Success`(19:59);`-executeMethod …PlayerBuildCli.Build`;源 = `e1c2669e` |
| **驱动** | XTest 键盘注入(python-xlib)+ 根窗口截图;`XO` 焦点经 `get_input_focus` 确认在游戏窗口上 |
| **日志** | `$CLAUDE_JOB_DIR/tmp/player-s254.log`(首轮)/ `player-s254b.log`(重启轮) |

---

## 1. 结论(**先给答案**)

**教学口述一次都没跑出来 —— 既没出声,也没出字幕。**

> **🔁 2026-11-06 21:15 订正(原文不删)**:本节是**第一轮(19:59–20:10)**的结论。
> **第二轮(20:50–21:05)**在补了 `-tutorialdiag` 读数面之后**跑通了一次完整教学**:口述**出声了**、
> 字幕**上屏了**,均有客观判据 —— 见 **§8**。第一轮的「零发火」是**走查手段不足**(盲走没走到病人跟前),
> 不是教学链的问题。⚠️ 但 §8 同时查出一条**真缺陷**(口述声源格在生产里退化成世界原点,音量被削 33 dB)。

直接原因:本次走查**没能完成一次立案**(`CaseOpened`),所以 `observe` 步永不完成 ⇒ 调度器零意图 ⇒ 翻译层零投递。
**这不是「教学链坏了」,是「教学链根本没被叫到」。**

> **🔁 20:25 订正(原文不删,以下是错的,留作复盘)**:初版这里写「原因 = 急救 `Armed` 把 `E` 全拒 +
> 该屏关不掉」。**这两句都已被证伪** —— 候选列表是 **Idle 常态 HUD**(可见恰恰说明**未** Armed,见 §4.1),
> 而 `E` 有绑定。**「`E` 为什么没出立案」至今未定位**,最可能是「半径内没有候选」(§4.2)。

**顺带查实的一条**(与 `E` 无关):「跳过」在**任何一次立案/查体之前**必然是空操作(§4.3) —— 这是设计后果,不是 bug。

## 2. 走查步骤与观测

| # | 动作 | 观测 | 图 |
|---|---|---|---|
| 1 | 启动真包 | 主菜单四行(继续诊治 / 新起一局 / 设置 / 退出游戏);`Down`+`Return` **有效** ⇒ 建新世界 `w3`(`[BootRoot][7a] 切换世界目录: w2 → w3`) | — |
| 2 | 进入世界 | 立刻出现:`【病人】咳…咳咳…`(**患者字幕**,44 的 cue 路径)+ `止血 / 通气 / 跳过`(10 的候选屏) | `s254-w1-…png` |
| 3 | 按 `E`(多次) | **无任何变化**(0.003 灰度差)⇒ 交互被拒 | — |
| 4 | 按 `Return` / 点击 `跳过` | 屏不变、不关 | — |
| 5 | 按 `Escape` | **暂停壳弹出**(继续 / 保存 / 上次保存:1 分钟前 / 存档位 / 调治 / 返回主菜单)⇒ **UI 键盘输入是通的** | `s254-w2-…png` |
| 6 | 关闭暂停壳,四方向各走一步 + 按 `E` | 移动有效(画面变化 mean 9–13),**但始终弹不出脉案** | `s254-w3-…png` |

## 3. 教学链**装配成功**(这一条是真包证据,不是推断)

启动日志里**没有** `[48][tutorial] 教学链未装配 —— 内容表/音频缝/字幕口 至少缺一` 这一行。
该告警是 `BootRoot` 第 4.7 步的**唯一分支出口**(`TutorialSeams == null` 才打),而同一次启动的其它 `Debug.Log` 确实落进了日志文件(如 `[世界装载] …`、`[BootRoot][7a] …`)⇒ **日志通道是通的,缺的这行不是丢失,是不该有**。

⇒ `AddressablesDataProvider.Load<TutorialContentTable>()` 成功、音频缝与字幕口都在、`Attach(actorId)` 执行过。

⚠️ 但「装配成功」≠「发过火」:本次走查中**零发火**,故 `TutorialMediaDriver.Play` **一次都没被调用**(这一点与 §1 是同一句话的两面)。

## 4. `E` 为什么没生效 —— **两轮追查,第一轮的判断被我自己的第二轮证伪**

> **原文(20:10,保留不删)**:本节初版写「进世界即有急危病人 ⇒ `EmergencyChannel` Armed ⇒
> `ModalGate.Accept` 方向① 把 `E` 全拒」。
> **🔁 20:25 订正:这条判断是错的** —— 证伪依据见 §4.1。原文保留,是因为「我为什么会判错」本身有信息量:
> 我把「屏上有急救列表」直接读成了「急救态」,而没有去查这张列表的显隐由谁驱动。

### 4.1 证伪依据:候选列表可见 ⟺ 通道**未** Armed

`CompositionRoot.cs:1757` 把可见性接到 `EmergencyChannelDriver.onIdleGate`;该文件头注(`:29-31`)逐字写着:

> Idle 门(R-9.3):**Arm ⇒ 候选列表隐藏**;End / Abort ⇒ 复现

⇒ 我看到的「进世界就有 止血/通气/跳过」**恰恰证明通道处于 Idle**,不是 Armed。
它是**常态 HUD**(规则六之甲:跳过入口在 Idle 态恒可达),不是「急危弹屏」。
⇒ `IsArmed == false` ⇒ `ModalGate` 方向① 不成立。

顺带排掉另一个可能:`Interact` **有**绑定(`InputSystem_Actions.inputactions`:`Player/Interact` = `<Keyboard>/e`,
急救 = `f`)⇒ 「E 没绑定」也不成立。

### 4.2 那 `E` 到底为什么没反应 —— **未定位**(最可能是「范围内没有候选」)

`InteractionInputDriver` 要求**半径内有候选**才算一次交互。病人确实存在(`【病人】咳…` 说明 13 在场),
但我在 16×16 的图里**盲走**没摸到他。**本次没有可读的位置面来验证这一点** ⇒ 记 `NOT-RUN`,
不记「交互坏了」。

### 4.3 「跳过」为什么没反应 —— 这一条**是确定的**(且与 `E` 的原因无关)

```
「跳过」Submit(Return / 点击两条路径都进 SubmitSkip)
  → BootRoot:1164-1165 接的回调                  ✅ 已接线(不是没接)
  → CasebookCommandRouter.SubmitEmergencySkip()
  → if (_boundPatient.Value < 0) return;         ← ★ 静默返回(零事件零提示)
```

`_boundPatient` 的**唯一写入点** = `CasebookVitalsSource.ChangePatient`(`CasebookCommandRouter.cs:91`),
其**唯一调用方** = `InteractionInputDriver.cs:231` —— 也就是世界里的 `E`(立案 / 查体)。

⇒ **在一次立案/查体之前,「跳过」必然是空操作。** 而这恰恰是玩家**第一次**看到这张列表时的状态。
代码注释把这条路径称为「合法玩家路径」并配「零提示纪律(AC-11-12)」⇒「玩家按了没反应」是**设计后果**,不是 bug。

⚠️ **成立原因与是否该改是两件事**:跳过以「脉案绑定病人」为对象,而该绑定只有立案之后才有。
要不要改对象来源(例如取急救通道自己的目标),是**裁定**,不是修 bug —— 本件不擅自改。

### 4.4 这条为什么一直没被夹具抓到(F1 形态)

`emergency_skip_entry_test` 的判据是 **SAC-023-1(元件形态)/ SAC-023-3(焦点落点形态)** ——
判「入口存在且可获焦」,**零条判「跳过真的产生一次 Attempt」**。
行为面若用假 router 作夹具,会自然绕过 `_boundPatient` 这个真前置 ⇒ 形态绿、行为死。

### 4.5 仍未验证的一小块(如实登记)

**`Return` / 点击究竟有没有到达 `SubmitSkip`(焦点是否在该行)未验。**
对结论无影响:即使到达,也会在 §4.3 的 `_boundPatient` 处静默返回。
⇒ 「跳过在真包里无效」**确定**;「焦点是否掉在别处」是**另一条**待查项。

## 5. 本次**未验证**的项(禁借绿)

| 项 | 状态 |
|---|---|
| 教学口述**出声** | **NOT-RUN**(零发火,没有 cue 被发出去) |
| 教学字幕**上屏** | **NOT-RUN**(同上) |
| `TutorialMediaDriver.Play` 在生产装配下被调用 | **NOT-RUN**(代码路径在,但本次零触发) |
| 急救「跳过」真包可用性 | **NOT-RUN**(见 §4 三可能) |

> ⚠️ 屏幕上那条 `【病人】咳…咳咳…` 是 **44 的病人语声字幕**(`PatientCueDriver` → `SubtitleLineView`),**不是**教学字幕。把它当教学证据 = 借绿。

> **🔁 2026-11-06 21:15 状态更新(原文不删)**:上表四行**全部由 `NOT-RUN` 转为 `RUN`**,判据与原始数据见 **§8**。
> 本表保留第一轮的原状,作为「手段不足会把 RUN 记成 NOT-RUN」的留档 —— 反向借绿同样要防。

## 6. 副作用与还原

| 项 | 结果 |
|---|---|
| 用户存档 `w1` / `w2` / `worlds.index` | 按 `saves-before-s254.tar.gz` 还原;**20/20 SHA-256 逐字节一致** |
| 本轮新建测试世界 `w3` | **移出**存档树 ⇒ `$CLAUDE_JOB_DIR/tmp/s25-4c-testworld-w3-2026-11-06`(可逆) |
| 源码 / 资源 | 走查期间**零改动**(真包构建自 `e1c2669e`) |

## 7. 由本次走查带出的问题(需另裁,不在本件修)

1. ~~**教学第一步在真包里不可达**(急危病人 ⇒ Armed ⇒ `E` 被拒)~~ —— **🔁 20:25 证伪**:那张列表是 Idle 常态 HUD,与 Armed 无关。原文保留以标记判断错误。
2. **「跳过」在真包里不可观测地生效** —— §4.3:**它本来就不该生效**(未立案 ⇒ `_boundPatient = None` ⇒ 静默返回)。真正的问题是**可观测性**:玩家按下去看不出发生了什么(零提示纪律 AC-11-12 的代价)。
3. **走查工具缺一条能力** —— 本次无法读到「玩家格 / 病人格」,只能盲走。
   🔁 **2026-11-06 20:45 已补**:`unity/Assets/Editor.Tools.Spike/SaveCellDumpCli.cs`(Editor-only,不进构建)
   离线读最新世界的存档,打印 `ActorCellEntered`(每 actor 最后一格)/ `DiseaseOnset`(病人+病种+最近锚点)/ `CaseOpened`(立案)。
   **实测(w2,527 事件)**:actor 0(玩家)最后在格 `(4,0,2)` @tick 23445,跨格事件 507 条;病人 0–9 的出现 tick 齐全;立案 1 条(patient 7 @16622)。
   ⚠️ **病人格读不到** —— `DiseaseOnset` 载荷**不含格**(registry 逐字:`onset_tick/disease_id/patient_id/patient_seed/seq`),
   `ActorCellEntered` 的 author = **1 玩家控制器**(病人不动这个流)。病人位置是**呈现态**(13 移动),只在 `PresenceQuery.TryGetCell` 这个**内存面**里。
   ⇒ 「玩家格」有了;**「病人格」要另有手段**(在 Boot 加一条命令行开关驱动的调试打印,属生产代码改动,待裁)。

---

## 8. 第二轮走查(20:50–21:05)—— **口述出声了、字幕上屏了**,并查出一条真缺陷

> **本节是 §1–§7 的后续**。第一轮用 `NOT-RUN` 记的三项(出声 / 上屏 / `Play` 被调用)在本轮**全部转为 RUN**,
> 每条都给了**客观判据**(不是「我看到好像有」)。

### 8.1 本轮补的手段(`-tutorialdiag`)

第一轮之所以零观测,根因是**看不见现场**:玩家格 / 病人格 / 急救态 / 教学链解了几条事实,**一个都读不到**,
只能在 16×16 的图里盲走。本轮新增 `unity/Assets/Gameplay.Boot/WalkthroughDiagnostics.cs`:
只在命令行带 `-tutorialdiag` 时,每 20 tick(1 s)打一行

```
[tutorialdiag] t=12.35 tick=120 actor=0 cell=(0,0,0) 在场=2 #1@(1,0,14) #2@(14,0,1) 最近=#1 距离=14 急救=Idle 教学facts=4 seeded=0
```
> 🔁 2026-11-06 22:10:本节初写时行首没有 `t=`;§10 为了区分「冻结」与「边沿延迟」加了墙钟
> (`Time.realtimeSinceStartup`,秒)。⚠️ 加它是**有代价**的:走查脚本 `diag_walk.py` 原本按
> `[tutorialdiag] tick=` 开头匹配,加了前缀就整条失效(实测:那一轮零读数、零发火)。
> 改格式 = 改解析器,两处必须同批动。

发火瞬间另打一行 `[tutorialdiag] ★发火 tick=N steps=… 口述=N`。
⚠️ **只读**:不写流、不改状态、不碰存档;未带旗标 ⇒ 每 tick 只多一次静态 bool 读。

### 8.2 步骤与观测

| # | 动作 | 观测 |
|---|---|---|
| 1 | 启动 `-tutorialdiag`,`Down`+`Return` 建新世界 | `tick=160 cell=(0,0,0) 在场=2 #1@(1,0,14) #2@(14,0,1) 最近=#1 距离=14 急救=Idle 教学facts=4` |
| 2 | 按读数走(梯度下降) | `w` 四步:`(0,0,0)→(0,0,3)→(0,0,6)→(0,0,9)→(0,0,13)`,距离 `14→11→8→5→1`;**急救态全程 `Idle`** |
| 3 | 按住 `e` 300 ms | 脉案弹出 —— 证明 §4.2 的「`E` 没反应」**不是绑定问题**,是**没走到跟前**(距离 14 时按是空操作) |
| 4 | 按 `Escape` 关脉案 | 世界解冻,**下一 tick 立刻发火**:`★发火 tick=332 steps=observe 口述=1` |
| 5 | 截图 | 屏幕下方出现 **`【师父】喘着气,气色、神态,都在脸上。`** |

**判据 1(发火)**:日志出现 `★发火 tick=332 steps=observe 口述=1`。
**判据 2(字幕)**:截图上屏文本 = **音频事件表**的 `subtitle_text`,**不是** 48 内容表的 `text`。
两表该条文案确实不同(内容表 `先看看这个人。气色、神态,都在脸上。` vs 事件表 `喘着气,气色、神态,都在脸上。`),
屏幕上出现的是后者 ⇒ **`AC-44-15` 的「字幕文本单一真源 = 44 事件表」在真包里成立**(这是实测,不是设计意图)。

### 8.3 判据 3:**出声** —— 波形互相关(不是「听着像」)

真包启动的同时用 `parec` 录 sink monitor(s16le / 44.1 kHz / 立体声),录音 33.17 s;
拿 `unity/Assets/Audio/Narration_TeachObserve_01.wav`(2.39 s,mono)做**归一化互相关**扫全部滞后:

| 滞后 | 归一化互相关 |
|---|---|
| **30.219 s(最佳)** | **0.6032** |
| 20.0 s(负控) | −0.0002 |
| 24.0 s(负控) | −0.0002 |
| 28.0 s(负控) | 0.0168 |

- 最佳滞后 30.219 s **正是按 `Escape` 的那一刻**(脚本时序:`t=30` 按 `Escape`,`t=33` 停录)。
- 负控(同一段录音、同一素材、前后几秒)**互相关 ≈ 0** ⇒ 0.6 不是巧合。
- ⇒ **素材真的被播出来了**,`TutorialMediaDriver.Play → CueRenderSink.Emit → AudioSourceRenderTarget.PlayOneShot`
  整条链在生产装配下跑通。**`口述=1` 只证明 `Emit` 被调用;互相关证明的是「声音到了设备」。**

### 8.4 判据 4(**缺陷**):口述比素材本身低 **33 dB**,根因 = 声源格退化成世界原点

同一段互相关还给出幅度比:**0.0216 = −33.3 dB**。为排除「录音链本身衰减」,做了**控制实验**:
同一条 `Narration_TeachObserve_01.wav` 用 `paplay` 直放到同一 sink、同一 `parec` 录法 ⇒
互相关 **0.9999**、幅度比 **0.8306 = −1.6 dB**(= sink 音量 94% / −1.61 dB,**对得上**)。

⇒ 录音链没有问题,**这 33 dB 是游戏路径吃掉的**。继续量:同一段录音里环境声(11–18 s)对素材是 **−23.6 dB**,
**所有素材都是同一化到 89.1% 峰值的**,所以口径可比。

**根因(代码级,已核)**:`CompositionRoot.cs:2156-2159`

```csharp
// 口述的空间化锚点:P0 没有师父实体 ⇒ 没有声源格,取听者格(相机格)。
// 听者缝未接 ⇒ 原点 —— 只在无声学意义的环境(夹具)里发生。
Func<Int3> tutorialCell = audio.ListenerCell != null
    ? audio.ListenerCell
    : (Func<Int3>)(() => default(Int3));
```

而 `AudioAssemblySeams.ListenerCell`(`AudioAssemblySeams.cs:104`)**全仓只有声明与读取、零赋值点**:

```
$ grep -rn "ListenerCell" unity/Assets --include=*.cs | grep -v "SetListenerCell\|_listenerCell"
Gameplay.Boot/AudioAssemblySeams.cs:104:        public Func<Int3> ListenerCell;
Gameplay.Boot/CompositionRoot.cs:1811:            //    听者格来自 `AudioAssemblySeams.ListenerCell` 缝(相机格,由 BootRoot 注入)。
Gameplay.Boot/CompositionRoot.cs:2157:                Func<Int3> tutorialCell = audio.ListenerCell != null
Gameplay.Boot/CompositionRoot.cs:2158:                    ? audio.ListenerCell
```

- `BootRoot.cs:959` 只调了 `sources.SetListenerCell(...)`(**渲染目标**的读面),**没有**给 **装配缝** `_audioSeams.ListenerCell` 赋值。
- `:1811` 的注释写着「由 BootRoot 注入」—— **那句注入不存在**。这是 F6 形态(自述顶替执行体)的一个新样本。
- 后果:生产里 `audio.ListenerCell == null` ⇒ 声源格 = `default(Int3)` = **(0,0,0)**;
  玩家当时在 **(0,0,13)**,`spatialBlend = 1` + Logarithmic 衰减(min 1 / max 12)⇒ 被拉低约 33 dB。
- ⚠️ 注释自称「只在夹具里发生」,**实测在真包里发生了** —— 这正是实测高于自述的地方。

**这条不是「教学没出声」,是「出声了但音量被空间化吃掉了」** —— 两者对玩家的后果不同,对修法的定位也不同。

### 8.5 时序发现(**行为问题,非缺陷**):教学发火被脉案模态**冻住**,关掉才播

`tick` 从开脉案起**停止增长**(日志停在 980 附近),直到 `Escape` 关掉才恢复,随后**下一 tick 立刻发火**。
原因:`ADR-013 §十-C Amendment D` —— 挡路模态摊开 ⇒ **真停 sim tick**;而教学链挂在 **tick 边沿**(`TutorialChainDriver.OnTickEdge`)。

⇒ 玩家体验上:**立案 → 立刻翻脉案 → 世界冻结 → 师父不出声 → 关掉脉案 → 师父才开口**。
教学想教的「边看病人边听师父说」因此**错位了一步**。这不是教学链的 bug(它的触发面本来就是 tick),而是
**「48 的驱动面 = tick 边沿」与「ADR-013 的模态停 tick」两条已裁设计的接缝**,须另裁是否让模态关闭时也跑一次边沿。

### 8.6 本轮**未验证**的项(禁借绿)

| 项 | 状态 |
|---|---|
| 另外三条口述(`judge` / `close` / `archive`) | **NOT-RUN**(只跑通了 `observe` 一条) |
| 音频 tier / 遮挡对这条 cue 的实际作用 | **NOT-RUN**(本轮没有对照) |
| 修掉 §8.4 后的音量是否回到素材电平 | **NOT-RUN**(未修,不借绿) |

### 8.7 副作用与还原

| 项 | 结果 |
|---|---|
| 用户存档 `w1` / `w2` / `worlds.index` | 按 `saves-before-diag.tar.gz` 还原;**20/20 SHA-256 逐字节一致** |
| 本轮新建的 `w3` / `w4` | 整棵存档树挪到 `saves.testing-<HHMM>`,**未删**(可逆) |
| `probe` 参照录音 | `$CLAUDE_JOB_DIR/tmp/ref.raw`(控制实验)+ `rec.raw`(真包录音),均可复算 |

### 8.8 复算命令(自包含)

```bash
# ① 听者缝零赋值
grep -rn "ListenerCell" unity/Assets --include=*.cs | grep -v "SetListenerCell\|_listenerCell"

# ② 素材与录音的互相关(需先按 §8.3 录一遍)
python3 -I -c "
import numpy as np, wave
w=wave.open('unity/Assets/Audio/Narration_TeachObserve_01.wav','rb')
s=np.frombuffer(w.readframes(w.getnframes()),dtype='<i2').astype(float); s-=s.mean()
a=np.frombuffer(open('rec.raw','rb').read(),dtype='<i2').astype(float).reshape(-1,2)[:,0]
n=np.linalg.norm(s); best=(0,-2)
for lag in range(0,len(a)-len(s)):
    g=a[lag:lag+len(s)]; g=g-g.mean(); c=np.dot(s,g)/(n*np.linalg.norm(g))
    if c>best[1]: best=(lag,c)
print('lag=%.3fs corr=%.4f gain=%.4f'%(best[0]/44100,best[1],np.linalg.norm(a[best[0]:best[0]+len(s)])/n))"

# ③ 控制实验(录音链无衰减)
#    parec … & paplay unity/Assets/Audio/Narration_TeachObserve_01.wav → 期望 gain ≈ 0.83(−1.6 dB = sink 音量)
```

---

## 9. §8.4 的归因**只对了一半** —— 修一轮没修好,再修才修好(2026-11-06 21:20–21:32)

### 9.1 第一轮修法(**无效**,如实留档)

诊断到「`AudioAssemblySeams.ListenerCell` 全仓零赋值点」后,在 `BootRoot` 第 5.2 步补了一句
`_audioSeams.ListenerCell = cameraCell;`,重构建、重跑同一套走查。结果:

| | 互相关 | 增益 |
|---|---|---|
| 修复前 | 0.6009 | 0.0216(−33.3 dB) |
| 「修复」后 | 0.5958 | **0.0218(−33.2 dB)** |

**几乎逐位相同 ⇒ 那次改动没有产生任何可观测效果。** 若就此收工,就会把一次**假修**
当成已修(与 F6「自述顶替执行体」同型:代码改了,行为没变)。

### 9.2 钉死用的仪表(一次运行回答一个问题)

在 `TutorialMediaDriver.PlayNarration` 里加了一行**旗标门控**的日志(默认零开销):

```
[tutorialdiag] 口述投递 cue=15 cell=(0,0,0)
```

- 修第一轮后:**仍然是 `cell=(0,0,0)`** ⇒ 缝填了,但**没人读它**。
- ⇒ 病灶不是「缝没人填」,是**「读缝的时机在填缝之前」**。

### 9.3 真根因:**装配次序**——`Assemble` 早于 `BootRoot` 第 5.2 步

`CompositionRoot.cs:2157`(旧)把三元**在装配那一刻**就求值了:

```csharp
Func<Int3> tutorialCell = audio.ListenerCell != null     // ← 此刻 ListenerCell 恒为 null
    ? audio.ListenerCell                                  //    于是这个 lambda 被固化成
    : (Func<Int3>)(() => default(Int3));                  //    「永远返回 default」的那一支
```

而填缝发生在 `BootRoot` 第 5.2 步 —— **在 `Assemble` 之后**。
⇒ 「判空」与「取值」必须**同时推迟到调用时**,不能一个在装配期、一个在运行期。

**修法**(`CompositionRoot.cs`):

```csharp
Func<Int3> tutorialCell = () =>
{
    Func<Int3> seam = audio.ListenerCell;      // ← 每次投递时才读
    return seam != null ? seam() : default(Int3);
};
```

### 9.4 复测(**修好了**)

| 状态 | 互相关 | 增益 | 对直放控制的相对值 |
|---|---|---|---|
| 修复前 | 0.6009 | 0.0216 | **−31.7 dB** |
| 第一轮(假修) | 0.5958 | 0.0218 | −31.6 dB |
| **延迟读修复后** | **0.9199** | **0.6631** | **−2.0 dB** |

- 互相关从 0.60 抬到 **0.92** —— 声源电平回到正常后,录音里的信噪比也上来了。
- 投递日志变为 **`cell=(0,4,16)`**(相机格);玩家在同一时刻是 `cell=(0,0,13)`
  ⇒ 相机在玩家上方 4 格、后方 3 格,这正是第三人称越肩机位,缝现在**真的被读到了**。
- 余下 −2.0 dB(对直放)可归 Voice 总线路径与格内抖动,不是缺陷。

### 9.5 两处一并订正的**错误自述**(原文不删)

1. `BootRoot.cs:959` 附近原注释与 `CompositionRoot.cs:1811` 的注释都写「听者格由 BootRoot 注入」——
   **在此次修复之前,那句注入不存在**。现已补上,注释与执行体一致。
2. §8.4 曾把 −33 dB **全部**归给「缝未接」。按 §9.4,正确的因果链是两环:
   **①缝没人填 → ②即便填了,装配期的判空也会把它锁死**。只修 ① 无效(§9.1 已实测)。

### 9.6 本轮**仍未验证**

| 项 | 状态 |
|---|---|
| 其余三条口述(`judge` / `close` / `archive`)的出声 | **NOT-RUN** |
| 全量回归是否受本改影响 | **NOT-RUN**(改的是装配期 lambda 形状,尚未跑套件) |

### 9.7 回归(2026-11-06 · 本修复轮)

**基线溯源三元组**:截至时刻 `2026-11-06 21:47:04 CST`(EM)/ `21:48:44 CST`(PM) · 批次:S25-4c 真包走查修复轮 · commit:`1f6490a1` + 工作树(`BootRoot.cs` / `CompositionRoot.cs` / `TutorialMediaDriver.cs` 三改,`WalkthroughDiagnostics.cs` 新增)

| 套件 | 结果 | XML | 对照基线 |
|---|---|---|---|
| EditMode | `total=7059 passed=7022 failed=0 inconclusive=0 skipped=37` | `unity/Logs/edit-diagfix.xml` | 同数(本轮**未新增测试** ⇒ 等式自洽) |
| PlayMode | `total=359 passed=355 failed=0 inconclusive=2 skipped=2` | `unity/Logs/pm-diagfix.xml` | `359/355/0/2/2`(零回归) |

编译错 `grep -c "error CS"` = **0**(两个日志各查一遍)。

⚠️ **复算时的一个坑(如实登记)**:`-runTests` **不能与 `-quit` 同用** —— 那样 Unity 在测试框架启动前就退出,
**不产 XML 且退出码仍为 0**(`Exiting batchmode successfully now!`)。首次就是这么跑的,日志 421 行、零测试痕迹。
判据:XML 不存在 ⇒ 该轮**什么都没跑**,不得按「无失败」计。

---

## 10. 模态关闭补跑一次教学边沿(2026-11-06 22:05–22:20 · 用户裁定)

### 10.1 改了什么

`BootRoot.Update()`:

```csharp
bool worldFrozen = _inputFreeze != null && _inputFreeze.IsBlocked;
bool justThawed = _worldFrozenLastFrame && !worldFrozen;   // 解冻的那一帧
_worldFrozenLastFrame = worldFrozen;
...
// 正常 tick 边沿块之后
if (justThawed && _services?.TutorialChain != null)
    _services.TutorialChain.OnTickEdge(_tickDriver.CurrentTick);
```

**安全前提 = 同 tick 重复调用幂等**。`TutorialChainDriver.OnTickEdge` 的 `tick` 形参
**在函数体里一次都没用**(只按事件游标推进 + 发火闸去重)⇒ 二次调用必然空转。
该前提由新夹具 `test_onTickEdge_sameTickTwice_firesOnce` 钉住
(判别力:去掉发火闸去重 ⇒ `_audio.Emitted.Count` 变 2 ⇒ 转红)。

⚠️ **只对教学链补跑**。其他 tick 边沿驱动(病人出现 / 急救 / 病程 / 时段)**未做**,也不在本条面内 —— 别顺手推广。

### 10.2 真包实测时间线(`-tutorialdiag` 已带墙钟)

```
t=25.38  tick=300  facts=17
t=26.38  tick=320  facts=17
t=29.34  ★解冻补跑 tick=331 facts=17
t=29.34  口述投递 cue=15 cell=(0,4,16)
t=29.34  ★发火 tick=331 steps=observe 口述=1
t=29.75  tick=340  facts=18
```

- **补跑确实触发**,且与发火**同一帧**。
- **只发一次** —— 同一帧/下一帧的正常 tick 边沿没有二次发火(幂等前提在真包里也成立)。
- **冻结确实存在**:`t=26.38` 到 `t=29.34` 共 2.96 s,按 20 Hz 应走 59 tick,实际只走了 **11** tick
  ⇒ 约 **2.4 s 世界是冻着的**。

### 10.3 ⚠️ 归因订正(§8.5 的那句话说过头了)

§8.5 我写「关掉脉案之后师父才开口」并暗示存在可观的**边沿延迟**。按 10.2 的时间线:

- 玩家体感的那段等待(**约 2.4 s**)是**冻结本身的时长**,不是边沿延迟 —— 冻结期间本就不该发火。
- 补跑真正改善的是「**关掉之后还要再等下一个 tick**」那一小段:**≤ 1 tick ≈ 50 ms**。
- 我此前口头说的「延迟数千毫秒」**把冻结时长当成了边沿延迟**,订正。

⇒ 补跑仍然值得做(它把「关掉即开口」变成**确定**行为,不再依赖该帧 tick 是否真的推进),
但它**不是**把 2.4 s 缩短到 0 —— 那 2.4 s 来自 `ADR-013 §十-C` 的冻结裁定,要改得动那条裁定。

### 10.4 判据与回归

**基线溯源三元组**:截至时刻 `2026-11-06 22:16:25 CST`(EM)/ `22:19:41 CST`(PM) · 批次:模态解冻补跑轮 · commit:`905ce971` + 工作树

| 套件 | 结果 | XML | 对照 |
|---|---|---|---|
| EditMode | `total=7060 passed=7023 failed=0 inconclusive=0 skipped=37` | `unity/Logs/em-thaw.xml` | 上轮 `7059/7022` ⇒ **+1/+1 = 新增夹具那一条**(等式自洽) |
| PlayMode | `total=359 passed=355 failed=0 inconclusive=2 skipped=2` | `unity/Logs/pm-thaw.xml` | 零回归 |

编译错 0(两个日志各查一遍)。

**证伪轮(新夹具的判别力)** —— 变异靶点 = `TutorialTrigger.cs:119` 的 `if (_latched[i]) continue;`(发火闸去重):

| 项 | 值 |
|---|---|
| 变异前 SHA-256 | `b10ae752e2642a36496a6c0c5590fac3fafcaadcf30ac7ae27b1121b5a506d3f` |
| 变异后 | `total=11 passed=6 **failed=5**` |
| 转红项 | **`test_onTickEdge_sameTickTwice_firesOnce`** · `test_sameCompletion_doesNotRefireOnLaterTicks` · `test_incremental_newEventFiresOnlyItsOwnStep` · `test_seed_fromLoadedStream_doesNotReplay` · `test_checkpointSeam_whenWired_archiveFiresOnce` |
| 还原后 SHA-256 | `b10ae752…` **逐字节一致** |

⇒ 新夹具**不是恒绿的**;它盯的那个属性(同 tick 二次调用不得重发)对**去重被拿掉**这件事有判别力。

### 10.5 仍然不成立的说法(禁借绿)

| 说法 | 状态 |
|---|---|
| 「补跑把师父开口的等待从 2.4 s 变成 0」 | **假** —— 那 2.4 s 是冻结时长,由 `ADR-013 §十-C` 裁定,本件不动它 |
| 其余三条口述(`judge` / `close` / `archive`)的出声 | **NOT-RUN** |
| 其他 tick 边沿驱动(病人出现 / 急救 / 病程)是否也该补跑 | **未裁、未做** —— 本件只覆盖教学链 |
