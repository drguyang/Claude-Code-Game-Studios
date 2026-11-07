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

---

## 11. 其余三条口述:**四条全部出声**(2026-11-06 23:20–23:35)

### 11.1 先说手动走查为什么没走通(如实留档)

试图在真包里手动走「查体 → 病名栏 → 病名册 → 指认落笔」来触发 `judge`。摸清了操作面:

| 键 | 作用 |
|---|---|
| `Tab` | 焦点前进(`Down` 不动焦点) |
| `Right` | 从左侧行跨到右栏(病名 / ? / 折)—— 但**从靠下的行走不出来** |
| `Enter`(五通道行) | 行级查体,行内出墨迹(截图可见 面色 5 笔 / 语声 3 / 姿态 4 / 呼吸 2 / 触感 5) |
| `Enter`(病名栏) | 摊开病名册(条目 = **已见病名**,所以必须先查体,否则打开是空册 —— 这一点骗过我一次) |
| `Enter`(后案) | 翻到「方笺续页」—— **我两次误走到这里,把戥子档位行当成了病名册** |

`教学facts` 从 18 涨到 25,证明屏内动作**确实在写事件**。但焦点送进病名册条目那一步没稳定复现,
`JudgmentRecorded` 没写出来 ⇒ **judge 步没有发火**。⇒ 手动走查**失败**,不是系统坏的证据。

### 11.2 改用能出判据的手段:`-tutorialnarrcheck`

不再跟 UI 纠缠,改为让**真包**用**生产解析面**把 48 的全部口述依次播一遍:

- 新旗标 `-tutorialnarrcheck`(与 `-tutorialdiag` 独立),`BootRoot` 在教学链装配后起协程,
  逐条调 `TutorialMediaDriver.Play` —— 与教学链发火时**同一条**翻译层路径。
- 条间 5.5 s(须大于最长素材 Close 4.9 s,否则录音里会叠)。
- ⚠️ **它不替教学链发火**:发火仍只由事件流驱动。本件只回答「cue 解析得出、播得响吗」。
- 为此在 `TutorialChainSeams` 加了三个 **`internal`** 访问器(同程序集,不扩公共面)。

日志逐条落盘(少一条即某步抛异常):

```
[tutorialdiag] ▶口述复查 1/4 cue=Narration_TeachObserve_01
[tutorialdiag] ▶口述复查 2/4 cue=Narration_TeachJudge_01
[tutorialdiag] ▶口述复查 3/4 cue=Narration_TeachClose_01
[tutorialdiag] ▶口述复查 4/4 cue=Narration_TeachArchive_01
[tutorialdiag] ▶口述复查完毕
```

> ⚠️ `PlayNarration` 在「cue 解析不到」或「事件表行没有 `subtitle_text`」时**直接抛** ⇒
> 四条都跑完 = 四个 cue 全部解析出 ordinal、四个 `subtitle_text` 全部非空。

### 11.3 出声判据(录音 + 互相关)

`parec` 录 sink monitor(51.14 s),四段素材各自对录音做归一化互相关:

| cue | 互相关 | 起点 | 增益 |
|---|---|---|---|
| Observe | 0.8848 | 14.22 s | 0.6177(−4.2 dB) |
| **Judge** | **0.9978** | 19.73 s | 0.6173(−4.2 dB) |
| **Close** | **0.9911** | 25.21 s | 0.6074(−4.3 dB) |
| **Archive** | **0.9798** | 30.69 s | 0.6072(−4.3 dB) |

- 起点间隔 ≈ 5.5 s,与设定的条间间隔一致 ⇒ 不是巧合命中。
- 四条增益几乎相同(0.607–0.618)⇒ 四条走的是同一条总线路径、同一电平。
- 余下 −4.2 dB(对素材)与 §9.4 的 −3.6 dB 同量级 ⇒ 同一条总线损耗,不是某条 cue 特有的。

### 11.4 这条证明了什么、**没**证明什么(禁借绿)

| 命题 | 状态 |
|---|---|
| 四个 cue 在**真包**里解析得出(ordinal ≥ 0) | **已证** |
| 四行的 `subtitle_text` 非空(否则抛) | **已证** |
| 四个 clip 载入并**出声到设备** | **已证**(互相关 0.88–0.998) |
| `judge` / `close` 的**游戏内触发**能在真包走通 | **未证** —— 手动走查没走到,不是走不通的证据 |
| `archive` 的游戏内触发 | **结构上不可达**(`checkpointWritten: null`,7a 无「已存过档」读面;夹具 `test_checkpointSeam_whenNull_archiveNeverCompletes` 钉着) |

⇒ 可以说「**四条口述都能出声**」;**不能**说「教学六步在真包里都能教到」——
后者仍卡在 `judge` 的 UI 走查与 `archive` 的 7a 缺口。

### 11.5 回归

**基线溯源三元组**:截至时刻 `2026-11-06 23:33 CST`(EM)/ `23:36 CST`(PM) · 批次:口述复查轮 · commit:`f4292aae` + 工作树

| 套件 | 结果 | XML |
|---|---|---|
| EditMode | `total=7060 passed=7023 failed=0 inconclusive=0 skipped=37` | `unity/Logs/em-narr.xml` |
| PlayMode | `total=359 passed=355 failed=0 inconclusive=2 skipped=2` | `unity/Logs/pm-narr.xml` |

编译错 0。本轮**未新增测试**(只加旗标与内部访问器)⇒ 总数与上轮同值,等式自洽。

---

## 12. 病名册接不上 Tab 的根因与修法(2026-11-06 23:45–23:59)

### 12.1 根因:三个代码事实叠在一起

| # | 事实 | 位置 |
|---|---|---|
| ① | 病名栏 Submit 只做「开册 + 重建条目」,**不移动焦点** | `CasebookScreen.cs:393-408`(`_bookOpen = true; RefreshLexiconBook(); return;`) |
| ② | 册子容器由 `anchor.parent.Add(_lexiconBook)` **append 到 `right-column` 末尾** | `:627` |
| ③ | 「折」(`fold-edge`) 在 `BuildUI` 里早已加入同一栏,**且 `focusable = true`** | `:165`(对比 `:154` 的 confidence 不可聚焦) |

⇒ `right-column` 树序 = **病名 → 折 → 册子条目 1 → 条目 2 …**
⇒ UI Toolkit 无显式 `tabIndex` 时**树序即 Tab 序** ⇒ 从病名栏按一次 `Tab` 落在**「折」**上,
再按一次才进册子;而「折」的 Submit 是 `SubmitFold`(右页折回、盖住判断区)
⇒ 玩家看到的是「界面变了」,不会想到「我刚刚跳过了册子」。

⚠️ **我自己的走查要打折**:那几轮在病名栏之后按了很多键(Tab×7 / Down×2 / Up×5…),
**具体哪一次把我带到「方笺续页」的不能指定**。§12.1 三条是代码事实(可复算);
「我撞上了哪一关」是推断,不写进判据。

### 12.2 为什么既有夹具全绿而没抓到

`casebook_lexicon_book_test` 的 ⑦b 是这么写的:

```csharp
DispatchSubmit(entries[1]);   // 内部先 target.Focus()，再发 NavigationSubmitEvent
```

**它先 `Focus()` 把焦点强设到条目上,再提交** —— 证明的是「条目收到 Submit 会指认」,
**跳过了「焦点能不能走到条目」这一问**。六条语义判据 + 一条派发判据全绿,而这条路径实际不好走。
⇒ 与 `.claude/docs/coding-standards.md` 假绿表 **F1(自造断言顶替 AC 语义层)** 同型:
AC 要的是「玩家能指认病名」,夹具证的是「条目收到事件会指认」。

### 12.3 修法(用户裁定「按 A 修」)

`CasebookScreen` 增 `FocusFirstLexiconEntry()`,在 `SubmitDiseaseNameBar()` **开册那一条路径**上调用:

```csharp
_bookOpen = true;
RefreshLexiconBook();
FocusFirstLexiconEntry();   // ← 新增
return;
```

- 无条目 / 无面板 ⇒ **空操作**(屏单元直调语义方法的既有夹具形态不受影响)。
- **只在开册处调用,不进 `RefreshLexiconBook`** —— 册子因数据刷新重建时不得抢焦点。
- 不动树序、不动视觉布局(那是 B 案,会让条目上移到「折」之上)。

### 12.4 新夹具(补的正是缺失的那一层)

`casebook_lexicon_book_test.test_openBook_movesFocusToFirstEntry`
—— **只按玩家真实动作序**:落焦病名栏 → 真派发 Submit,**不给任何条目 `Focus()`**;
断言 `panel.focusController.focusedElement == 第一条条目`。

**证伪轮**:去掉 `FocusFirstLexiconEntry()` 的调用 —

| 项 | 值 |
|---|---|
| 变异前 SHA-256 | `ebc501a65f9db2deb79a7c797481f83743d9219c85d77f55502d5ff9a98e395f` |
| 变异后 | `total=12 passed=11 **failed=1**` |
| 转红项 | **`test_openBook_movesFocusToFirstEntry`**(恰好一条,不多不少) |
| 还原后 SHA-256 | `ebc501a6…` **逐字节一致** |

### 12.5 回归

**基线溯源三元组**:截至时刻 `2026-11-06 23:57:25 CST`(EM)/ `23:58:44 CST`(PM) · 批次:病名册落焦轮 · commit:`95698054` + 工作树

| 套件 | 结果 | 对照 |
|---|---|---|
| EditMode | `total=7060 passed=7023 failed=0 inconclusive=0 skipped=37` | 与上轮同值(本轮未加 EditMode 测试) |
| PlayMode | `total=360 passed=356 failed=0 inconclusive=2 skipped=2` | 上轮 `359/355` ⇒ **+1 = 新夹具那一条**(等式自洽) |

编译错 0。

### 12.6 ⚠️ 仍未验证(禁借绿)

| 项 | 状态 |
|---|---|
| **真包里 judge 步能否因此走通** | **NOT-RUN** —— 修的是「焦点送不进册子」,夹具已证、证伪已过;**但没有重跑真包走查**。「修好了」≠「走通了」 |
| `close` 步 | 同上,更在其后 |
| 手柄路径(`AC-48-24` / `AC-18-24`) | **NOT-RUN** —— 本轮只碰键盘/导航语义,未在手柄上复验 |

---

## 13. 重跑真包攻 judge(2026-11-06 23:45 – 2026-11-07 00:35)—— **仍未触发**,但挖出三条更硬的缺陷

### 13.1 先补仪表:焦点读数(否则一直是盲按)

前几轮失败根因之一是**看不见焦点在哪**。而周期读数(`tick` 整除 20 打一行)挂在**tick 边沿**上,
模态摊开时 **tick 停** ⇒ 冻结期间**一行都没有**,恰好是最需要看见的时候。

⇒ `WalkthroughDiagnostics.ReportFocusIfChanged()`:每帧读 `panel.focusController.focusedElement`,
**仅在变化时**打 `[tutorialdiag] 焦点=<name|匿名;userData|(无焦点)>`。

**实测焦点轨迹(一次完整走查)**:

```
焦点=(无焦点)              ← 脉案刚打开
焦点=channel-complexion    ← 按 Down
焦点=channel-voice         ← Tab
焦点=channel-posture       ← Tab
焦点=channel-breathing     ← Tab
焦点=channel-palpation     ← Tab(触感)
焦点=(无焦点)              ← ★ 按 Right
```

### 13.2 三条实测缺陷(都是代码事实,有轨迹)

| # | 现象 | 判据 |
|---|---|---|
| **D-1** | **脉案开屏时没有任何元素持焦** | 轨迹第一行恒为 `(无焦点)`;此时 `Submit` 被引擎**静默丢弃**(引擎语义:导航提交落在 `focusedElement` 上,无焦点则丢弃 —— 既有夹具 ⑦c 已钉住该语义) |
| **D-2** | **从下方行按 `Right` 会把焦点整个丢掉** | 从 `channel-palpation` 按 `Right` ⇒ `焦点=(无焦点)`。右栏只有 病名/？/折,都靠在顶部 ⇒ 从第 5 行往右**没有候选**,引擎不是"不动"而是**失焦**;且**无自动恢复** ⇒ 之后所有 Submit 全被丢弃。**这正是"按了没反应"的机械原因** |
| **D-3** | Tab 序实测与树序一致,多按会落到翻页键 | `complexion → voice → posture → breathing → palpation → inquiry → disease-name → fold-edge → page-edge-prev → …`。从「触感」到「病名」**只需 2 次 Tab**;我此前按 4 次,多出的两下落在 `fold-edge` 与 `page-edge-prev`(前案 = **翻页**)—— 这就是屡次"翻到方笺续页"的真凶 |

D-1 / D-2 都是**可玩性缺陷**:键盘/手柄玩家进脉案先要吃一次"按键无效",再从下方行往右会直接失能。

### 13.3 judge 为什么**仍然**没触发:病名册是空的

修好东西后按正确序列走(2 次 Tab 到病名 → Submit),焦点轨迹停在 `channel-disease-name`,
**没有出现任何条目焦点** ⇒ `RefreshLexiconBook` 建了**零条目**。

而查体**确实写入了**:关掉脉案再打开,五行都出现墨迹(面色 5 笔 / 语声 3 / 姿态 4 / 呼吸 2 / 触感 5)。
⇒ 卡点不在查体,在**「已见病名」为空**:

```csharp
// CasebookVitalsSource.cs:139-147
SeenLexiconIds => _viewerActorId < 0 ? 空
                : _lexiconBook.PointableLexiconIds(_seenDisease.SeenDiseaseIds(_viewerActorId));
```

三环中**哪一环返回空未定**:`_viewerActorId`(BootRoot:881 `SetViewer` 已调)·
`SeenDiseaseIds`(`OnExamined` 写在 `BootRoot` 的 `AfterExamined` 缝上)·
`PointableLexiconIds`(词表里可指认的 id)。

**没查下去**,是因为再往下要逐个读三环的生产值 —— 这不是"再走一次"能回答的,需要加读数。

### 13.4 本轮**未**达成的与未验证的(禁借绿)

| 项 | 状态 |
|---|---|
| `judge` 步真包触发 | **仍未触发**(卡在 §13.3) |
| `close` 步 | 仍未触发 |
| 手柄路径 | **NOT-RUN** |
| 三条口述的**游戏内**触发 | 仍未验(§11 证的是 cue → 声音,**不是**触发) |

### 13.5 下一步(若要继续)

给 `CasebookVitalsSource.SeenLexiconIds` 的三环各加一行 `-tutorialdiag` 读数
(`viewer` / `SeenDiseaseIds.Count` / `PointableLexiconIds.Count`),一次真包即可定位是哪一环空。
在此之前,任何"judge 走通了"的说法都不成立。

### 13.6 最终树回归(含焦点读数)

**基线溯源三元组**:截至时刻 `2026-11-07 00:45:34 CST`(EM)/ `00:46:59 CST`(PM) · 批次:病名册落焦 + 焦点读数轮 · commit:`95698054` + 工作树

| 套件 | 结果 | 对照 |
|---|---|---|
| EditMode | `total=7060 passed=7023 failed=0 inconclusive=0 skipped=37` | 与上一轮同值(本轮未加 EditMode 测试) |
| PlayMode | `total=360 passed=356 failed=0 inconclusive=2 skipped=2` | 较 `359/355` **+1 = §12.4 新夹具那一条** |

编译错 0(两个日志各查一遍)。

⚠️ **为什么要重跑**:§12.5 那轮(EM `7060/7023` · PM `360/356`)跑在**加焦点读数之前**,
其数字不覆盖**当前**工作树。按「禁借绿」,提交前必须在最终树上重跑,而不是沿用上一轮的数。

---

## 14. 三环读数落地:空的是**第三环**(2026-11-07)

§13.5 说的那次真包跑完了。**答案:ring 3(`pointable`)空,而它空的两个原因都不是代码。**

### 14.1 读数(真包,`-tutorialdiag`,同一进程一次走查)

```
[tutorialdiag] 病名册环 t=10.40 viewer=0 seen=0 pointable=0 词表行=3 novelty=0      ← 进世界
[tutorialdiag] 查体投影 patient=1 actor=0 disease=2 novelty=First tick=311           ← 查面色
[tutorialdiag] 病名册环 t=28.51 viewer=0 seen=1 pointable=0 词表行=3 novelty=1
[tutorialdiag] 查体投影 patient=1 actor=0 disease=2 novelty=Stale tick=311   ×4      ← 又查 4 行
[tutorialdiag] ★发火 tick=311 steps=observe 口述=1                                   ← 只有 observe 发火
```

逐环判决:

| 环 | 读数 | 结论 |
|---|---|---|
| ① `viewer` | `0` | **不空** —— `SetViewer` 生效 |
| ② `seen` | `1`(`novelty` 由 `0` → `1`) | **不空** —— `OnExamined` 真写过一条 `SkillGrown` |
| ③ `pointable` | `0` | **空的是这一环** |
| 附:`词表行` | `3` | 词表**装载成功**(不是管线没接) |

### 14.2 第三环为什么空 —— 两个彼此独立的错配,任一单独成立都足以清零

**错配 A:词表外键指向一个不存在的病种 id。**

```
assets/data/case_judgment_lexicon.json
  {lexicon_id:1, text:"伤寒", disease_ids:[1]}   ← id 1
  {lexicon_id:2, text:"湿温", disease_ids:[]}    ← 孤儿
  {lexicon_id:3, text:"温毒", disease_ids:[]}    ← 孤儿
assets/data/disease_registry.json 的 id 段 = 2..17(17 条),**没有 id 1**
```

⇒ 「伤寒」这一条反查命中不了任何真实病种(顺带记:按 `disease_names.json`,`伤寒` 是
`DIS_TYPHOID`,注册表 id = **10**,不是 1)。

⚠️ **烘焙期没有任何跨表校验**:`JudgmentLexiconBinder.ReadDiseaseIds` 只查
「数组 / 元素是整数」(见 `:136-158`),不比对注册表 ⇒ 悬空外键**静默烘进去**。
时间线可核:词表烘于 `6223ca13`(2026-10-12),当时生产只有 fallback 注册表
(`DiseaseId = 1`);**2026-10-22 B1 接真表(id 从 2 起)后,这个 1 就悬空了**,而没人报错。

**错配 B:真包里出现的病人,病种恒为 id 2(`INJ_STAB` 刺伤),词表里没有「刺伤」这个词条。**

```
[tutorialdiag] 查体投影 patient=1 actor=0 disease=2 ...
```
生产 `CompositionRoot` 的 `PatientAppearedDriver` 取 `registryIndex: 0`
⇒ 注册表第 0 条 = id **2** = `INJ_STAB`。而病名册三词条是 伤寒 / 湿温 / 温毒 —— **没有一个对应伤情**。

⇒ 即使把错配 A 修成 `disease_ids:[10]`,本轮这个病人(`disease=2`)照样反查不出条目。
**两个都要动,或者换病人,或者给伤情补词条 —— 这是内容/设计裁定,不是改一行代码。**

### 14.3 本轮验到了什么(**只**这一条)

- 三环里**只有第三环空**,已由真包读数直接观测,不是推断。
- 查体 → `SkillGrown` → `NoveltyTracker` 这条写路径在真包里**是通的**(`novelty 0→1`)。
- `judge` 之所以不触发,根因**不在教学链、不在焦点、不在查体**,在**词表外键与病人病种**。

### 14.4 本轮**未**做(禁借绿)

| 项 | 状态 |
|---|---|
| `judge` 步真包触发 | **仍未触发**(且按 §14.2,不修数据就不可能触发) |
| 修错配 A(`disease_ids:[1]` → `[10]`) | **未做** —— 改的是作者态数据,且改完仍不够(见错配 B) |
| 修错配 B(给词表补伤情 / 换病人病种) | **未做** —— 内容 / 设计裁定 |
| 加跨表门(词表 `disease_ids` ⊆ 注册表 id) | **未做** —— 若要做,它是防复发的那一半 |

### 14.5 本轮的仪表改动(与 §14.1 读数同源)

| 文件 | 改了什么 |
|---|---|
| `WalkthroughDiagnostics.cs` | 新增 `ReportLexiconRingsIfChanged`(每帧、仅变化时打)· `ReportExamined` |
| `CasebookVitalsSource.cs` | 抽出 `ComputeLexiconRings` 供生产与读数**共用同一次求值** + `GetLexiconRingCounts`(`internal`) |
| `SeenDiseaseProjector.cs` | `OnExamined` 落地瞬间打一行(区分「没走到投影」与「走到了但抛了」) |
| `BootRoot.cs` | 每帧调读数(与焦点读数同理由:脉案摊开时 tick 停,周期读数一行都没有) |

构建:`unity/Logs/build-ring.log:8750` `Build Finished, Result: Success.` · 错误数 0 · 22.9 s。

**存档**:走查前备份 `saves/` 20 个文件 → 走查后 `rsync -a --delete` 还原 → 逐文件 SHA-256
与备份**20/20 一致**(走查期间产生过 `saves/w3/save_auto.bin`,已随还原消失)。

---

## 15. `judge` 走通(2026-11-07)—— 修 A + 伤寒病人

### 15.1 改了什么

**① 修错配 A(作者态数据 + 重烘)**

```
assets/data/case_judgment_lexicon.json
  { "lexicon_id": 1, "text": "伤寒", "disease_ids": [1] }   →  [10]
```

理由:`伤寒` = `DIS_TYPHOID` = 注册表 id **10**(`disease_registry.json` id 段 = 2..17,无 1)。

烘焙:`unity/Logs/bake-lexicon.log:397`
`case_judgment_lexicon.cooked.bytes = 73 B, 3 行, ConfigVersion = 0x74C8A843`
产物逐字节核对:`...e4 bc a4 e5 af 92 01 0a...` —— 病种数组长度 `01`、值 `0a`(= 10)。

**② 换伤寒病人 —— 用现成的启动期缝,不改代码**

`DYJC_AUDITION_REGISTRY_INDEX=8`(`BootRoot.ResolvePatientRegistryIndexOverride`)⇒
`PatientAppearedDriver` 取注册表第 8 条 = id **10** = `DIS_TYPHOID`。
真包日志自证:`[BootRoot] 病人病种索引覆盖 = 8(DYJC_AUDITION_REGISTRY_INDEX;听测期专用)`。

**③ 重建 player**(`unity/Logs/build-ring2.log:8619` `Build Finished, Result: Success.`,错误 0,18.1 s)。
⚠️ **必须重建**:词表产物走 Addressables bundle(`<build>/…_Data/StreamingAssets/aa/`),只重烘不重建 ⇒ 包里仍是旧字节。

### 15.2 真包读数(一次走查,新起一局)

```
[tutorialdiag] 查体投影 patient=1 actor=0 disease=10 novelty=First tick=331      ← 伤寒病人
[tutorialdiag] 病名册环 t=29.58 viewer=0 seen=1 pointable=1 词表行=3 novelty=1   ← 三环全非空
[tutorialdiag] 焦点=channel-disease-name
[tutorialdiag] 焦点=<匿名;1>          ← 开册后焦点落在词条(lexicon_id=1)
[tutorialdiag] 焦点=(无焦点)
[tutorialdiag] ★解冻补跑 t=43.96 tick=331 facts=17
[tutorialdiag] 口述投递 cue=15 cell=(0,4,16)
[tutorialdiag] 口述投递 cue=16 cell=(0,4,16)
[tutorialdiag] ★发火 tick=331 steps=observe,judge 口述=2
```

截图 `jd1.png`:病名列出现「**伤寒**」条目行且**带焦点框**(其上是 `?` 空栏,其下是「折」)。
⇒ §14 的 `pointable=0` 已转 `1`,病名册**有条目了**。

**★发火 `steps=observe,judge 口述=2`** —— `judge` 步真包触发,并投递了 2 条口述
(`observe` 与 `judge` 各一)。

### 15.3 ⚠️ 这条结论的边界(禁借绿,逐条写清)

| 边界 | 说明 |
|---|---|
| **发火发生在「解冻补跑」路径上** | 整个开册/查体/指认期间世界冻结(`tick` 恒 331),两条步**同一趟解冻补跑**一起发火。**没有**单独观测到 `judge` 在「普通 tick 边沿」发火 —— 该路径与解冻补跑共用同一个 `OnTickEdge`,但本轮的观测只覆盖后者 |
| **伤寒病人来自启动期旋钮** | `DYJC_AUDITION_REGISTRY_INDEX=8` 是**听测期专用**缝。**出厂默认(`registryIndex: 0` = `INJ_STAB` 刺伤)仍然一本词条都翻不到** —— 这个缺口没被本轮填上。🔁 **2026-11-07 后续轮已填**(见 §17):词表补第 4 行「刺伤」,默认病人即可走通 |
| **词表仍只有 1 条真关联** | 3 行里 2 行是孤儿(`湿温` / `温毒` 的 `disease_ids` 仍为 `[]`);GDD 说的 37 条词表**没有**成文 |
| **`close` 步** | 仍未触发 |
| **手柄路径** | 仍 `NOT-RUN` |
| **口述听感** | 只证到「投递了 2 条 cue」(`口述投递`),**没有**本轮录音互相关;出声判据见 §11 |

### 15.4 连带修的一处测试弱化(不修就是 F4 空洞正控)

`case_judgment_lexicon.json` 改 `[1]→[10]` 后,`lexicon_book_source_test` 里两条
**端到端**判据会**静默变弱**:它们用 `SpawnNext(diseaseId: 1)`(EditMode fallback 注册表的
占位 id),而病种 1 在真词表里没有关联行 ⇒ 两侧都空 ⇒
`foreach` **零次迭代**(⑥)、`空 == 空`(⑦)—— 都恒真。

处置(改成**可证伪的事实断言**,而不是删掉):
- ⑥ 显式写死「病种 1 ⇒ 反查为空(当下正确行为,非装载失败)」+ 补一条真表判据
  「已见 10 ⇒ 词条 1 入册」(外键再悬空即红);
- ⑦ 补一条「病种 1 ⇒ DTO 面为空」,让 `空 == 空` 的弱相等至少被一条硬断言钉住。

**PlayMode 一侧更严重:三条链上判据直接变红(它们的前提本来就是那条错数据)。**

| 夹具 | 改前 | 改后 |
|---|---|---|
| `casebook_point_identify_chain_test` | `DiseaseId = 1`(注释写着「内联注册表病种 1(**词表反查有 id**)」) | `10` + 传**真注册表** |
| `e2e_full_loop_test` | 同上 | `10` + 真注册表 + `diagnosisQueryLevelOverride: 20` |

⚠️ `e2e_full_loop_test` 换真病种后还连带暴露**两处夹具写死**:
1. `AdvancePastIncubation` 原推**固定 60 tick**(按 fallback 曲线 潜伏 20 / τ=40 算的),
   而真表 伤寒是 潜伏 **1200** / τ_rise **3600** ⇒ 连潜伏都过不去。
   ⇒ 改为**按位置推进**(推到 `position ≥ 0.60`,上限 20000 tick),不再写死曲线参数。
2. 诊断等级:定表实测 `READ_FLOOR(0) = 1.0` 而 伤寒 归一峰值只有 **0.667**
   ⇒ 等级 0 下**任何体征都读不出**;但等级 60 又触发
   `[SignChannelBinder] 档位 slot 越界(词表长度 3)`(因为 `SLOT_BOUNDS = {10,20,50}`
   ⇒ 等级 ≥ 50 时档序 = 3,而词条行只有三档词)。
   ⇒ 取 **20**:`READ_FLOOR(20) = 0.5833 < 0.667` 读得出,且档序 = 2 合法。
   (这两个数都是从烘焙产物 `diagnosis_read_floor.cooked.bytes` 直接解出来的,不是估的。)

### 15.5 复算命令

```bash
# 词表制品里的外键(应为 01 0a = 长度 1、值 10)
xxd -s 0x20 -l 16 unity/Assets/DataCooked/case_judgment_lexicon.cooked.bytes
# 期望:e4bc a4e5 af92 010a …(「伤寒」UTF-8 + 数组长度 01 + 病种 id 0a)
# 烘焙行(应为 73 B / 3 行 / ConfigVersion 0x74C8A843)
grep "烘焙完成" unity/Logs/bake-lexicon.log
# 构建行
grep "Build Finished" unity/Logs/build-ring2.log
```

### 15.6 回归(最终树)

**基线溯源三元组**:截至时刻 `2026-11-07 00:19:58Z`(EM)/ `00:32:18Z`(PM,2026-11-07)·
批次:修 A + 伤寒病人轮 · commit `33901b5d` + 工作树

| 套件 | 结果 | 对照 |
|---|---|---|
| EditMode | `total=7060 passed=7023 failed=0 inconclusive=0 skipped=37` | 与 §14 基线**同值**(本轮未增删用例,只改钉值与强度) |
| PlayMode | `total=360 passed=356 failed=0 inconclusive=2 skipped=2` | 与 §14 基线**同值** |

**天然证伪(不是模拟的)**:把词表退回旧值(`[1]`)时,同一批夹具当场红
—— EditMode **1 红**(`test_bake_roundtrip_threeRows_withOrphan`,`Expected 1 / But was 10`)
· PlayMode **3 红**(两条链上判据 + e2e 的「病名册非空」前置)。⇒ 这两处判据**真的钉着这条数据**,
不是恒绿。

**存档**:走查前备份 → 走查后 `rsync -a --delete` 还原 → **20/20 逐字节一致**。

---

## 16. 跨表外键门(2026-11-07 · 用户裁定「跨表门也加上」)

### 16.1 门在哪

| 项 | 位置 |
|---|---|
| 门类 | `unity/Assets/Editor.Tools.Gates/LexiconRegistryCrossRefGate.cs` |
| 判据本体(纯函数) | `:90` `RunAll(IReadOnlyList<JudgmentLexemeRow>, IReadOnlyList<DiseaseRegistryEntry>)` |
| 构建期入口 | `:54` `RunAll()` —— 读两份烘焙产物 |
| **接进链** | `AssemblyGates.cs:160` `errs.AddRange(LexiconRegistryCrossRefGate.RunAll());`(`RunAll` = 菜单 / 域重载 / 构建前门三处共用) |
| 接线声明 | `gate_wiring_manifest_test.cs` 的 `EntryWiringManifest["RunAll"]` + `ClassConsumerManifest`(均**手写**,承 C-1 的「禁从源生成」) |
| 判据夹具 | `unity/Assets/Tests/EditMode/DataPipeline/lexicon_registry_cross_ref_gate_test.cs`(**6 条**) |

### 16.2 判据(两条 + 两条守卫)

- **C1 悬空外键**:词表每行 `disease_ids` 逐值必须 ∈ 病种注册表 id 集;缺 ⇒ 红,报错串**点名** `lexicon_id` / 词条 / 下标 / 那个 id。
- **C2 只查一向**:注册表里有病种没有对应词条**合法**(孤儿条目是设计内形态 —— `AC-37-35` 的双射候选拒**依赖**它),不得反向报红。
- **守卫 G1/G2**:词表 0 行 / 注册表 0 行 ⇒ 红(空集上 C1 恒真 = 空洞正控)。

**读的是烘焙产物**(`DataCookedDisk.LoadJudgmentLexicon` / `LoadDiseaseRegistry`,与运行期同一 codec、同一份字节;
为此在 `DataCookedDisk` 开了同源读路径 —— 原唯一读路径在 `Gameplay.Boot`,而门所在的装配不引用它)。
⚠️ **如实登记盲区**:改了源 JSON 忘了重烘时,本门看的是旧产物 ⇒ 可能绿;该形态由
`cooked_drift_all_artifacts_test` 兜。**两者互补,本门的绿不得读成「源数据也一致」。**

**同批结清一处自陈 NOT-RUN**:`JudgmentLexiconTable.cs` 的 `JudgmentLexiconValidator` 文档原写
「与真实注册表的全量交叉检查归烘焙落地轮(**登记 NOT-RUN**)」—— 注册表已于 2026-10-22 落地,
该前置消失;本门即其实行体(原文不删,已就地加注)。

### 16.3 证伪轮 —— 门**真的会红**(不是模拟,在于链上观测)

**变异窗口**:`case_judgment_lexicon.cooked.bytes` 第 `0x27` 字节 `0A → 01`(= 把 disease_id 10 改回当初那个悬空 1);
窗口内**未读**该文件。跑全量 EditMode,结果 `7066 / 7023 / **6 红**`:

| 红件 | 归因 |
|---|---|
| `lexicon_registry_cross_ref_gate_test.test_realArtifacts_zeroErrors` | **靶心** —— 门读盘上字节,当场红,报错串含 `disease_ids[0]=1` |
| `assembly_gate_runall` 的 `test_assemblyGates_runAll_zeroErrors_b3_b2_b6_b7` | **证明门在链上**(不只是夹具能过) |
| `test_adr031_v4_b5GateStillPassesOnRealTree` | 另一个走 `RunAll` 的判据,同因 |
| `drift[case_judgment_lexicon.cooked.bytes]` | 源 ↔ 产物漂移门(预期内:变异只改产物) |
| `lexicon_book_source_test` 两条 | 悬空 id 一回来,`[1]` 就又"命中"了 ⇒ §15.4 新加的两条断言转红 |

**还原判据 = SHA-256 逐字节**:变异前 `a83af2bc…d40222`,还原后同值(`diff` 零差异)。
⇒ 这一刻的工作树与跑出 `7066/7029` 的那棵树**逐字节相同**。

### 16.4 回归(含新门)

**基线溯源三元组**:截至时刻 `2026-11-07 01:56:37Z`(EM)/ `02:01:27Z`(PM)· 批次:跨表门外键轮 · commit `33901b5d` + 工作树

| 套件 | 结果 | 对照 |
|---|---|---|
| EditMode | `total=7066 passed=7029 failed=0 inconclusive=0 skipped=37` | 较 §15.6 `7060/7023` **+6** —— 等式:7066 − 7060 = 6 = 新夹具条数 |
| PlayMode | `total=360 passed=356 failed=0 inconclusive=2 skipped=2` | 与 §15.6 **同值**,零回归 |

⚠️ **一处读数坑(如实记)**:PlayMode 首读时 `-f` 判据在 XML **刚创建、尚未写完**时就放行,
读到 `341/337/0` 这种截断值。判据应是「文件存在 **且** 含 `<test-run ... total=` 闭合」,
或读前先确认 mtime 稳定 —— 否则会把「读到半截」当成「少了 19 条测试」。

---

## 17. 诊室默认病人 = 刺伤(2026-11-07 · 用户裁定「让诊室默认来一个刺伤病人」)

### 17.1 先纠一处前提:**默认本来就是刺伤**

`CompositionRoot` 的 `PatientAppearedDriver` 取 `registryIndex: 0` ⇒ 注册表第 0 条 =
id **2** = `INJ_STAB` **刺伤**。§15/§16 那两轮的 `disease=2` 读数就是它。
⇒ 「让默认来一个刺伤病人」要动的**不是病人**,是**词表**:当时四个词条里没有伤情,
默认病人因此一本词条都翻不到(§15.3 的第二条边界)。

### 17.2 改了什么

```
assets/data/case_judgment_lexicon.json
  + { "lexicon_id": 4, "text": "刺伤", "disease_ids": [2] }
```

烘焙:`unity/Logs/bake-lexicon2.log:428`
`case_judgment_lexicon.cooked.bytes = 92 B, 4 行, ConfigVersion = 0x0A2BD752`(旧:73 B / 3 行 / `0x74C8A843`)。
重建 player:`unity/Logs/build-stab.log:8756` `Build Finished, Result: Success.` · 错误 0 · 30.7 s。

⚠️ **`ConfigVersion` 变了** ⇒ 旧存档在新包上比对会走 ADR-010 §七 的**非致命**分支(告警不阻断)。
本轮走查用「新起一局」,不涉旧档。

### 17.3 真包读数 —— **不带任何 `DYJC_*` 旋钮**

```
[tutorialdiag] 病名册环 t=10.45 viewer=0 seen=0 pointable=0 词表行=4 novelty=0     ← 进世界
[tutorialdiag] 查体投影 patient=1 actor=0 disease=2 novelty=First tick=332          ← 默认病人 = 刺伤
[tutorialdiag] 病名册环 t=29.57 viewer=0 seen=1 pointable=1 词表行=4 novelty=1
[tutorialdiag] 焦点=channel-disease-name
[tutorialdiag] 焦点=<匿名;4>          ← 开册焦点落在 lexicon_id=4「刺伤」
[tutorialdiag] ★发火 tick=332 steps=observe,judge 口述=2
```

截图 `jg1.png`:病名列出现「**刺伤**」条目行且带焦点框。
⇒ **出厂路径**(默认病人 + 默认词表 + 零启动参数)从头走到 `judge` 发火。

### 17.4 夹具

`judgment_lexicon_bake_test` 增 4 行断言(`Rows.Count ≥ 4` / `LexiconId = 4` / `Text = "刺伤"` /
`DiseaseIds = [2]`)—— 钉的是**源数据**,行被删或外键被改即红。
既有「病种 1 ⇒ 反查为空」两条断言不受影响(仍无词条关联 1)。

### 17.5 回归(最终树)

**基线溯源三元组**:截至时刻 `2026-11-07 02:17:39Z`(EM)/ `02:18:46Z`(PM)· 批次:默认刺伤病人轮 · commit `f35ae13d` + 工作树

| 套件 | 结果 | 对照 |
|---|---|---|
| EditMode | `total=7066 passed=7029 failed=0 inconclusive=0 skipped=37` | 与 §16.4 **同值**(未增删用例,只在既有夹具里加断言) |
| PlayMode | `total=360 passed=356 failed=0 inconclusive=2 skipped=2` | 与 §16.4 **同值**,零回归 |

### 17.6 仍然不成立的说法(禁借绿)

| 项 | 状态 |
|---|---|
| 发火走的是**解冻补跑**路径 | 仍然成立(§15.3 第一条边界没变) |
| 词表 4 行里只有 2 行有外键 | `湿温` / `温毒` 仍是孤儿;GDD 的 37 条仍未成文 |
| `close` 步 | 仍未触发 |
| 手柄路径 | 仍 `NOT-RUN` |
| 口述听感 | 只证到投递 2 条 cue,**没有**录音互相关 |

---

## 18. 手柄路径验证(2026-11-07 · 用户「验手柄路径,用之前的虚拟手柄」)

### 18.1 用什么验

复用项目**既有的虚拟手柄**形态(`InputSystem.AddDevice<Gamepad>()` + `QueueStateEvent`,
先例 = `U1Assump6GamepadChainTest` / `pause_focus_navigation_test`),走**生产**的整条链:

```
虚拟 Gamepad → InputSystemUIInputModule.Process → PanelEventHandler.OnMove/OnSubmit
  → Panel.SendEvent(NavigationMove / NavigationSubmit) → FocusController → 42 屏回调
```

新夹具:`unity/Assets/Tests/PlayMode/SkeuomorphicUI/casebook_gamepad_navigation_test.cs`

**为什么不能用既有那条链上夹具代替**:`casebook_point_identify_chain_test` 的
`Submit(el)` 是 `el.Focus()` + `el.SendEvent(NavigationSubmitEvent)` —— **绕过了输入层**,
「手柄的 Navigation 动作有没有接到这条路上」完全不在判据面内。

### 18.2 判据(一条测到底)

① 方向键逐行移动,**每次按键恰好一次**焦点移动(无双触发);
② 从面色按 Down 能**逐行走到**问诊(不是「先 Focus 上去再看能不能用」);
③ 横走到病名行;
④ 手柄 Submit ⇒ 病名册摊开且焦点落**条目**;再 Submit ⇒ 病例流出现 `JudgmentRecorded`,
   载荷 `lexiconId` = 词表显式 id。

结果:**通过**。轨迹 `channel-complexion→voice→posture→breathing→palpation→inquiry`(Down 五次)
→ Up 五次回列首 → Right → `channel-disease-name` → Submit → 焦点 `<unnamed>`(条目)→ Submit → 落笔。

### 18.3 这一轮挖出的三件事(前两件是环境/几何事实,第三件是**产品缺陷**)

**F-1 环境(夹具,不是产品)**:`-nographics` 下 42 的行**自动高度恒为 0**
(`worldBound = 1356×0`,带不带图形一样)⇒ 几何导航无高差、`Down` 找不到邻居。
夹具给七行**显式行高**(既有两条手柄导航夹具同样用显式尺寸),并在断言里加了
「行高为 0 ⇒ **夹具红**」的闸 —— 不得把环境限制读成产品缺陷。

**F-2 几何(真实形状,不是缺陷)**:**右栏只有两行**(病名 / 折),左栏六行
⇒ 只有与右栏竖直波段重叠的左栏行(面色 / 语声)能右走成功;
**从问诊(第 6 行)按 Right 无候选、焦点不动**(实测)。
⇒ 手柄玩家的真实路径 = **在列首右走一次**,而不是「一路 Down 再右」
(Tab 的树序 ≠ 手柄的几何序 —— 这两条路径结论不同,不能互推)。

**F-3 产品缺陷(已修)**:在 `NavigationSubmitEvent` **派发过程中**直接
`FocusFirstLexiconEntry()` 落焦,焦点会被随后的输入更新**清掉**。
证据(探针 + 复现):`Focus()` 确实执行了(条目 `focusable=True / enabledInHierarchy=True /
panel=True`,且 `Focus()` 前后都在),但**两帧后 `focusedElement` 恒为 `null`**;
而同样一次 `Focus()` 在之后任意一帧调用都能立住。
**为什么此前没被发现**:键鼠侧夹具是直接 `SendEvent`,**不走 EventSystem** ⇒ 只见过「成功」那一面。
**修法**:`CasebookScreen.FocusFirstLexiconEntry` 改为 `schedule.Execute(...).ExecuteLater(0)`
(下一个面板更新落焦),带「条目可能已被重建 / 摘除」的守卫。
连带:`casebook_lexicon_book_test.test_openBook_movesFocusToFirstEntry` 补 `yield return null`
(它原本同步读焦点)。

### 18.4 改动后真包复核(生产代码变了,必须重启包验证)

重建 player(`Logs/build-padfix.log` `Build Finished, Result: Success.`,错误 0,24.9 s)后,
按 §17 的**同一路径**(默认刺伤病人、零启动旋钮)重走一遍:
`焦点=channel-disease-name → 焦点=<匿名;4> → ★发火 tick=331 steps=observe,judge 口述=2`
⇒ 推迟一帧的改动**没有**破坏键鼠路径。

### 18.5 回归(最终树)

**基线溯源三元组**:截至时刻 `2026-11-07 03:07:28Z`(EM)/ `03:08:58Z`(PM)· 批次:手柄路径轮 · commit `9e07c156` + 工作树

| 套件 | 结果 | 对照 |
|---|---|---|
| EditMode | `total=7066 passed=7029 failed=0 inconclusive=0 skipped=37` | 与 §17.5 **同值** |
| PlayMode | `total=361 passed=357 failed=0 inconclusive=2 skipped=2` | 较 §17.5 `360/356` **+1** —— 等式:361 − 360 = 1 = 新手柄夹具那一条 |

### 18.6 仍然不成立的说法(禁借绿)

| 项 | 状态 |
|---|---|
| 真实手柄硬件 | **不在面内**(轴死区 / 平台映射 / 驱动差异) |
| 焦点高亮**看得见看不见** | **不在面内**(`-nographics`) |
| 「移动跟不跟手」主观判断 | **不在面内** |
| 覆盖范围 | 只覆盖**脉案 → 病名册 → 指认**这一段;**不含**世界空间的走近 / 就诊 / 急救档位 —— 🔁 **2026-11-07 补:世界空间那三条已由 §19 覆盖**(真启动序 + 虚拟手柄;仍不含真实手柄硬件) |
| `close` 步 | 仍未触发(键鼠与手柄皆然) |

---

## 19. 世界空间手柄路径(2026-11-07 · 用户「验世界空间的手柄路径(走近、就诊、急救档位)」)

### 19.1 用什么验(以及为什么不是真包)

新夹具 `unity/Assets/Tests/PlayMode/world_gamepad_path_test.cs`:**真 `BootRoot` 启动序 +
虚拟手柄,零键盘注入**。三条判据都走生产帧泵,不手调驱动:

```
BootRoot.Update → MovementFeed.PumpFrame → _interactionDriver.OnFrame()
               → _cameraRig.Tick(档位机结算)
```

**为什么本轮没有真包**:§18 的手柄验证用的是**面板侧**虚拟设备(进程内 `AddDevice<Gamepad>`);
而真包是独立进程,手柄注入只能经 `/dev/uinput` —— 本机实测 `crw------- root root`、
当前用户**不在 `input` 组**(`id -nG` 无 `input`)⇒ 造不出虚拟手柄。PlayMode 真启动序
是这条判据在当前环境下能拿到的**最强形态**(它跑的是与真包同一套装配与帧泵)。

**与既有覆盖的分工**(逐条对过,不是重复):
`movement_virtual_key_test` 手动装配、无启动序、不读世界流;`casebook_entry_wiring_test`
**先把玩家传送**到病人身边且键盘优先;`emergency_camera_mode_intent_wiring_test`
**直接调 `driver.Arm`**、玩家按键不在链上;`casebook_gamepad_navigation_test` 手柄只到 42 面板。

### 19.2 三条判据与结果

| # | 判据 | 结果 |
|---|---|---|
| ① 走近 | 摇杆 `(0,+1)` 推 4 m ⇒ `transform.z` 前进 **且** `LastCommittedCell.Z` 变号 **且** 世界流新增 `ActorCellEntered`(载荷格 z > 起点) | **PASS** |
| ② 就诊 | 沿**同一条走出来的路**走到 `d∞ ≤ 2`(病人 `(1,0,14)`,玩家沿 `x=0` 走到 `z≈12–14`),**只按手柄 North** ⇒ 恰 1 条 `CaseOpened` + `CurrentModal == Casebook` | **PASS** |
| ③ 急救档位 | West 按下沿 ⇒ Armed ⇒ 相机 `Treatment`;按住 180 帧(3 s,> `AbortIdleTicks` 50 tick)**仍 Armed**;松开 ⇒ Idle ⇒ `Explore` | **PASS**(**先红后绿,见 19.3**) |

### 19.3 挖出的产品缺陷(已修):急救看门狗基准在接线形态下**永不推进**

**症状**:真启动序 + 手柄按住 West,相机进 `Treatment` 后约 2.5 s 被中止回 `Explore`,
零事件 —— 玩家按住 3 秒等于什么都没发生。

**判别证据(夹具先做判别步,把两因分开)**:按住 180 帧后实测
**采样计数 `0 → 149`(样本在进)、`IsArmed = False`** ⇒ 不是「输入没进来」,是
**看门狗基准没被采样推进**。

**根因**:`EmergencyDirectReadChannel.OnAfterUpdate` 把读数喂给**通道自身**
(`EmergencyDirectReadChannel.Feed`),**不经过** `EmergencyChannelDriver.Feed` ——
而 `_lastActiveTick` 只在后者里写 ⇒ 基准停在 `Arm` 那一刻 ⇒ 50 tick 后 `AbortAction`
(零事件,规则六之甲)。

**为什么此前没被发现**:既有两条夹具都走**测试缝** `FeedForTest`(那条缝经过本类,会推基准);
L2 相机夹具直接 `Arm` → `EndAction`,不在「按住 > 2.5 s」的形态里。⇒ 同一失败形态:
**夹具走一条缝、生产走另一条缝**,行为断言在夹具面恒真。

**修法(两处,最小面)**:
1. `EmergencyDirectReadChannel` 新增 `event Action Sampled`,在 `Feed` **成功之后**触发
   (「样本真的被接收」,不是「回调跑了一次」);
2. `EmergencyChannelDriver.Attach()` 订阅、`Detach()` 退订,回调里把 `_lastActiveTick`
   推到当前 tick —— 与既有 `Feed` 路径**同一时刻语义**。

> 🔁 **中途改过一次修法(原文不删)**:第一版按 `_channel.SampleCount` 在 tick 边沿比对。
> 它把「样本」归到**下一个** tick 边沿 ⇒ 既有夹具
> `emergency_channel_wiring_test.test_armedIdleBeyondAbortTicks_tickEdgeAborts_zeroEvents`
> 转红(49 tick 仍 Armed,50 tick 才因基准被推到 1 而未达阈值)。实战跑出来 ⇒ 改为事件订阅,
> 语义与 `Feed` 同刻,既有夹具不需要改。

### 19.4 证伪轮(四条变异,**每条各转红**,逐字节还原)

| 变异 | 靶 | 判据 | 结果 | 还原(SHA-256) |
|---|---|---|---|---|
| **M1** `MovementFeed.ProcessMovementFrame` 改喂 `Vector3.zero` | ① | `test_wg_01` | `Failed(total=1,p=0,f=1)` | `c7f7194e…` 一致 |
| **M2** `BootRoot.Update` 删 `_interactionDriver?.OnFrame()` | ② | `test_wg_02` | `Failed` | `2937cb84…` 一致 |
| **M3** 看门狗基准**不随采样推进**(还原到修前形态) | ③ | `test_wg_03` | `Failed` | `875457e8…` 一致 |
| **M3′** `Attach()` **不订阅** `Sampled`(针对最终修法) | ③ | `test_wg_03` | `Failed(total=1,p=0,f=1)` | `c0d4da1b…` 一致 |

**M3 打的是被废弃的第一版修法**;最终修法由 **M3′** 单独打 —— 一手一枪,不共用。
还原全部「备份 `cp` 回 + SHA-256 比对」,无 `git checkout`;窗口内未编辑被变异文件与测试件。

**如实登记两处无判别力的面**:
- `Detach()` 的**退订行**无可观察差异(回调是幂等赋值,重复订阅不改变结果)⇒ 无变异可打;
- `Attach` 的调用次序(先挂通道再订阅)不改变结果,未单独打。

### 19.5 回归(最终树)

**基线溯源三元组**:截至时刻 `2026-11-07 03:51:21Z`(EM)/ `03:53:56Z`(PM)·
批次:世界空间手柄路径轮 · commit `c44f9135` + 工作树

| 套件 | 结果 | 对照(§18.5) | 等式 |
|---|---|---|---|
| EditMode | `total=7066 passed=7029 failed=0 inconclusive=0 skipped=37` | `7066/7029/0/0/37` | 同值 |
| PlayMode | `total=364 passed=360 failed=0 inconclusive=2 skipped=2` | `361/357/0/2/2` | **364 − 361 = 3 = 新增三条**(`test_wg_01/02/03`) |

复算(命令自包含):

```bash
cd unity
~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testFilter "world_gamepad_path_test" \
  -testResults "$PWD/Logs/playmode-worldgamepad.xml" -logFile "$PWD/Logs/playmode-worldgamepad.log"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('Logs/playmode-worldgamepad.xml').getroot();print(r.get('result'),r.get('total'),r.get('passed'),r.get('failed'))"
# 期望: Passed 3 3 0
```

⚠️ **旧的 `Logs/editmode-final.xml` / `playmode-final.xml` 是 2026-11-04 的残留** ——
本轮已覆盖,判读前先看 `start-time`(本轮 = `2026-11-07 03:51Z/03:53Z`)。

### 19.6 仍然不成立的说法(禁借绿)

| 项 | 状态 |
|---|---|
| 真实手柄硬件 | **不在面内**(虚拟 Gamepad 不覆盖驱动 / 死区 / 平台映射 / 真实摇杆漂移) |
| 真包里的手柄 | **不可达**(`/dev/uinput` 仅 root,当前用户不在 `input` 组) |
| 「跟不跟手」「走起来顺不顺」 | **不在面内**(主观判断;且 `-nographics`) |
| 松开后聚合出的两个急救事件 | `test_wg_03` **不断言** `EmergencyAttempt` / `EmergencyTreatmentApplied`(那是 `emergency_attempt_wiring_test` 的面);本件只判**档位**与 **Armed 存续** |
| 世界空间形态 | 接线期形态:单玩家、病人至多 2 个(`PatientWiringPositions`)、外圈 `0/15` 不可走 —— 不是设计形态 |
| 键鼠真包的世界空间路径 | 仍是 §13 / §17 的**非系统**走查,未做成判据 |

---

## 19.7 真包手柄可达(B 路 · 2026-11-07 · 用户「按B进行」)

### 为什么此前不可达(环境实测,不是能力问题)

| 事实 | 实测值 | 后果 |
|---|---|---|
| `/dev/uinput` | `crw------- root root`(0600) | **造不出**虚拟手柄 |
| `/dev/input/event*` | `crw-rw---- root:input`(0660)、无 ACL | 游戏以 `gu` 运行 ⇒ **读不到任何手柄**(真手柄也一样) |
| 键盘为什么一直正常 | Unity 在图形会话下**走 X11**、不经 evdev | 键鼠走查与手柄**不是同一条路** —— 所以手柄从来没人验过 |

### 一次性授权(用户已执行)

```bash
sudo bash tools/input/install_uinput_permissions.sh
```

只改两条 udev 规则,**按用户组**授权(不是 0666),可撤销:
`/dev/uinput` → `root:gu 0660`;`/dev/input/event*` → `root:gu 0660`。
脚本自检输出:`✅ /dev/uinput 可写` · `✅ /dev/input/event0 可读`。

### 工具面(`tools/input/`)

| 文件 | 作用 |
|---|---|
| `vpad.py` | uinput 虚拟手柄(造设备 + 推摇杆/面键),创建即打印 `node=/dev/input/eventN` |
| `verify_world_gamepad.py` | 起 player(`-tutorialdiag -batchmode -nographics`)→ **边读读数边推输入**(自适应)→ 判 PASS/FAIL |
| `world_gamepad_trace.json` | 静态时间线(手动复现用;自适应脚本不依赖) |
| `install_uinput_permissions.sh` | 上面那条 sudo |
| `README.md` | 用法 + 五个坑 |

### 实测踩到的五个坑(每条都有失败证据,全部已修)

1. **摇杆 Y 轴反向** —— evdev 的 `ABS_Y` 最大值朝下,Unity 按原值映射。
   证据:第一轮 `stick=(0,+1)` 把玩家从 `cell.z=0` 推到 **−5** 并掉出地面(坠落重置回 `(0,0,0)`)。
   修:`vpad.set_stick` 内部对 `y` **取负**;修后同一输入 `cell.z` 单调 `0→4→9→14`。
2. **`West` / `North` 在 evdev 与 Unity 之间是反的** —— evdev `BTN_NORTH`(0x133) = Unity
   **`buttonWest`**;evdev `BTN_WEST`(0x134) = Unity **`buttonNorth`**(evdev 按 SNES 方位命名,
   Unity 按 Xbox 物理方位)。证据:探针按 0x133 ⇒ 读数 `手柄=…:W` 且 `急救=Armed`、`档位=Treatment`;
   按 0x134 ⇒ 读数 `手柄=…:N`。修:表按 **Unity 语义**命名。
3. **窗口失焦 ⇒ 设备停用 + 游戏停更** —— `ResetAndDisableNonBackgroundDevices` +
   `runInBackground=false`。证据:读数列从 `手柄=Gamepad#3:` 变 `手柄=无`,此后按键全部静默失效
   (实测正好吞掉「就诊」那一按;日志也同时断流)。修:验证器默认 `-batchmode -nographics`
   (无窗口即无失焦),并保留窗口模式下的**周期重置焦**。
4. **模态摊开 ⇒ tick 停**(ADR-013 §十-C)⇒ **周期读数不再出现**。
   证据:第一版把「就诊」判成 `FAIL(模态=Casebook 样本 0)`,而脉案**已经开了** —— 只是冻结后
   一行都没打。修:`WalkthroughDiagnostics.ReportModalTierIfChanged` **每帧、变化才打**
   (与既有焦点读数同因同法)。
5. **世界是预载、sim 停在主菜单** —— 必须先按 **South** 确认首行才进世界;否则 `[tutorialdiag]`
   一行都没有,看起来像「设备没被识别」。修:验证器先按 South 再等首行读数。

### 最终判据(真包,一轮,自适应驱动)

```
PASS ① 走近    cell.z 区间 [0, 14],前进 14 格(要求 ≥ 3);走完距离 1
PASS ③ 急救档位 Armed 样本 3 · Treatment 样本 4 · Treatment 之后回 Explore 样本 6
PASS ② 就诊    模态=Casebook 样本 1
```

⚠️ ③ 同时是 §19.3 那个修复的**真包证据**:按住 3.5 s(> `AbortIdleTicks` 2.5 s)全程 `Armed`,
未被看门狗误杀 —— 修前这一轮会停在 `Treatment` 后约 2.5 s 掉回 `Explore`。

### 本轮改动面

- 生产代码:**只有走查仪表**(`WalkthroughDiagnostics` 增 `模态/档位` 周期列、`路由` 列、
  `模态/档位` 每帧变化行;`BootRoot` 三处只读调用)。**零行为改动**,不带 `-tutorialdiag` 时整段不进。
- 工具:新增 `tools/input/`(五件)。
- 系统:一条可撤销的 udev 规则(仓库外,见上)。

### 边界(禁借绿)

| 项 | 状态 |
|---|---|
| 真实手柄硬件 | **不在面内**(虚拟设备发标准轴值;固件 / 死区 / 厂商映射差异验不到) |
| 画面 / 布局 / 焦点可见性 | **不在面内**(`-batchmode -nographics`) |
| 「跟不跟手」主观判断 | **不在面内** |
| CI | 需要 `/dev/uinput` 权限 ⇒ 普通 runner 上**跑不了**,只能在本机(有 udev 规则时)跑 |

### 回归(最终树)

**基线溯源三元组**:截至时刻 `2026-11-07 05:11:36Z`(EM)/ `05:14:13Z`(PM)· 批次:真包手柄 B 路 ·
commit `2249d1ff` + 工作树

| 套件 | 结果 | 对照(§19.5) |
|---|---|---|
| EditMode | `total=7066 passed=7029 failed=0 skipped=37` | 同值 |
| PlayMode | `total=364 passed=360 failed=0 inconclusive=2 skipped=2` | 同值 |

⚠️ **本轮未新增自动化夹具** —— 两处生产改动是**走查仪表**(只在 `-tutorialdiag` 下进),
其判据是上面那轮真包。如实登记:这几行读数**无夹具覆盖**,改动若失效只会在真包侧暴露。

---

## 19.8 真包键鼠世界空间路径(2026-11-07 · 用户「验一下键鼠的真包世界空间路径」)

### 注入层与手柄那侧**不是同一条路**

| | 手柄(§19.7) | 键鼠(本节) |
|---|---|---|
| 注入 | uinput 虚拟设备(`/dev/uinput`) | **X11 XTest**(`tools/input/xkeys.py`) |
| 需要窗口/焦点 | 不需要(设备级) | **需要**(XTest 只发焦点窗口) |
| 能否 `-batchmode -nographics` | 能 | **不能**(无 X 连接 ⇒ 无处可发) |
| 失焦后果 | 设备被停用 + 游戏停更 | 按键打到别的窗口 |

**为什么键鼠不用 uinput**:实测 —— 给 `/dev/input/event*` 授权**之前**,游戏就能读键盘;
而当时那些节点是 `root:input 0660`、无 ACL ⇒ Unity 在图形会话下**键盘走 X11、不经 evdev**。

### 工具

`tools/input/verify_world_kbm.py`(复用 `verify_world_gamepad.parse/judge`,同一套判据)+
`tools/input/xkeys.py`(焦点 / 按住 / 点按)。时间线与手柄轮一致:**急救(F 3.5 s)→ 走近(W)→ 就诊(E)**。

新增读数:本轮给 `[tutorialdiag]` 加了 `键=`(W/A/S/D/E/F/Esc 的按住位),
用来区分「键没进 Input System」与「进了但没被消费」—— 与 `手柄=` 同用途。

### 结果(真包,一轮)

```
[kbm] 按住 F 期间 读数 键=F
[kbm] 走完后 距离=1 cell=(0, 0, 14) 键=W

PASS ① 走近    cell.z 区间 [0, 14],前进 14 格
PASS ③ 急救档位 Armed 样本 3 · Treatment 样本 4 · 回 Explore 样本 6
PASS ② 就诊    模态=Casebook 样本 1
```

### 判据不是空转(变异 K1/K2)

| 变异 | 结果 |
|---|---|
| **K1** 去掉「按住 W」 | ① `FAIL`(cell.z 区间 [0,0],前进 0 格)—— ② 同时 FAIL |
| **K2** 去掉「按 E」 | ② `FAIL`(模态=Casebook 样本 0) |
| 两变异同批 | ③ 仍 `PASS`(`键=F`、Armed/Treatment/Explore 全在)**⇒ 单红 = 独立靶** |

还原 = 备份 `cp` 回 + SHA-256 逐字节(`c041595a…` 一致,`grep FALSIFY` = 0)。

### 边界(禁借绿)

| 项 | 状态 |
|---|---|
| 鼠标 | 世界空间 P0 **无指针动作**(交互是键位)⇒ 鼠标面不在本判据内;它在 42 UI 走查里 |
| 画面 / 布局 / 焦点可见性 | **不在面内**(判据只读 `[tutorialdiag]`) |
| 「跟不跟手」 | **不在面内** |
| 运行环境 | 需要**有 X 会话的桌面**(XTest 前提),不能进 batch CI |

### 回归(最终树)

**基线溯源三元组**:截至时刻 `2026-11-07 05:23:42Z`(EM)/ `05:26:23Z`(PM)· 批次:真包键鼠轮 ·
commit `2f1a2c98` + 工作树

| 套件 | 结果 | 对照(§19.7) |
|---|---|---|
| EditMode | `total=7066 passed=7029 failed=0 skipped=37` | 同值 |
| PlayMode | `total=364 passed=360 failed=0 inconclusive=2 skipped=2` | 同值 |

⚠️ 本轮生产改动同样**只是走查仪表**(`[tutorialdiag]` 增 `键=` 列),无夹具覆盖 —— 同 §19.7 口径。

### 证据日志

- 干净一轮:`/tmp/dyjc-kbm-world-pass.log`(PASS ×3)
- 变异轮(已还原,仅留档作反证):`/tmp/dyjc-kbm-world.log`

⚠️ 记录一处**过程失误**:清扫时发现第一轮 PASS 的日志被变异轮**同名覆盖** ——
之后改用 `--log` 分开命名。上面「结果」一节的三条判据来自重新跑的干净一轮(同工具、同版本)。
