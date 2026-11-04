# 实现原件 — ADR-041 存档槽覆盖语义(2026-11-04)

> **对象**:`SaveSlotLedger` / `SaveService` / `SaveSlotProbe` / `SlotProjections` /
> `SaveSlotView` / `SaveSlotItem` / `SaveSlotsScreen` / `ISaveSlotCommandSink` /
> `SaveSlotCommandRouter` / `BootRoot`
> **权威**:`docs/architecture/adr-041-save-slot-overwrite.md`(Accepted 2026-11-04)
> **用户裁定**:「点一下空档就是保存,点一下已有存档会提示读档还是覆盖存档」+ 三个细项全取建议
> (行内两枚墨字 / 不设槽位上限 / `.bak` 读侧可回退)。

---

## 1. 变更面

### 1.1 代码

| 文件 | 变更 |
|---|---|
| `Sim.Codec/SaveSlotLedger.cs` | **删门①**(覆盖 ⇒ 抛);`FallsBackToBak` 拆为 `ProbeCountsBak`(占用态,手动槽 false)与 `ReadFallsBackToBak`(读档回退,恒 true);`OnCheckpointCommitted` 加「**不得触碰 `_locked`**」不变式注 |
| `Sim.Codec/SaveService.cs` | 占用态用 `ProbeCountsBak`;`LoadBytes` 用 `ReadFallsBackToBak`,且「final 缺失 + 只剩 bak」对手动槽仍判**空槽**(与探测同向) |
| `Sim.Codec/SaveSlotProbe.cs` | 占用态改用 `ProbeCountsBak` |
| `Sim.Codec/SlotProjections.cs` | `Writable` 去掉 `¬Locked` 与 `slot_seq ≥ 高水位` 两条子句 ⇒ `Writable ⟺ InNarrativeContext` |
| `Gameplay.UI/Skeuomorphic/SaveSlotView.cs` | 新增 `IsLocked`(尾参,向后兼容) |
| `Gameplay.UI/Skeuomorphic/SaveSlotItem.cs` | 行内二段态:`EnterChoice` / `ExitChoice` / `ActivateOpen` / `ActivateRewrite` + 两枚可焦点墨字 |
| `Gameplay.UI/Skeuomorphic/Screens/SaveSlotsScreen.cs` | `Activate` 三路分流(未绑定 ⇒ 载入 / 空槽 ⇒ 落笔 / 有档 ⇒ 二段态);已锁行「启封」灰置并退出焦点序;重画清二段态 |
| `Gameplay.UI/Skeuomorphic/SaveSlotCommands.cs` | 接口加 `TrySaveAt(uint)` |
| `Gameplay.Boot/SaveSlotCommandRouter.cs` | `TrySave` / `TrySaveAt` 共用一个执行体;新增 `saveFailureSink`(可 null ⇒ 回落) |
| `Gameplay.Boot/BootRoot.cs` | 新增 `LogSaveFailure`(报文 `[7b][落笔]` + 门名);`BuildSaveSlotViews` 填 `isLocked` |

### 1.2 夹具

- **新增** `adr041_slot_overwrite_test`(6 条:覆盖合法 / 槽号 0 仍拒 / 覆写不清锁 /
  `.bak` = 上一版 / 叙事上下文门 / 高水位不倒退)
- **新增** `adr041_save_slot_choices_test`(6 条:空槽落笔 / 二段态零动作 / 重写 / 已锁灰置 /
  重画作废 / 留档仍追加)
- **改写** `manual_slot_bak_scope_test`(核心判据**反转**:手动槽 final 坏 ⇒ 回退上一版;
  `final` 缺失 + 只剩 bak ⇒ 仍空槽;两条谓词必须分叉)
- **改写** `save_slot_contract_test`(覆盖合法 + 闭合回路改断在锁上 + 投影真值表重算)
- **改写** `save_slot_lock_release_test`(跨会话显式覆写 ⇒ 允许且留 `.bak`)
- 四个假 sink 补 `TrySaveAt`

### 1.3 文档

`save-slot-ui.md`(规则二改判 / 边界表 / `AC-7b-08` 改写)· `persistence-service.md`
(`AC-7a-19 (b)` + 二次加注)· `save-slots-7b.md`(交互表存盘行 + 新增覆写行)· `adr-010 §四`(两段改判注)·
`adr-041`(实现轮 + §Validation)。

---

## 2. 判据与回归

| 套件 | 结果 | 三元组 |
|---|---|---|
| EditMode | `6873/6836/0/37` | 截至 `2026-11-04T13:41:27Z` · sprint-24 ADR-041 轮 · `ceac3539` + 工作树 |
| PlayMode | `339/335/0/2/2` | 截至 `2026-11-04T13:42:28Z` · 同批次 |
| 对照基线 | EditMode `6861/6824/0/37` · PlayMode `339/335/0/2/2` | `引用自 production/qa/evidence/impl-main-menu-fixed-rows-2026-11-04.md` @ `13:15:17Z` |

**差值**:EditMode **+12/+12**,恰为新增两件夹具之和(6 + 6);PlayMode 逐位相同 ⇒ **零回归**。

---

## 3. 实跑修正(两处都是我的问题,如实登记)

1. **编译错**:`System.InvalidDataException` 不存在 —— 它是 `System.IO.InvalidDataException`
   (`BootRoot.LogSaveFailure`)。首轮 EditMode 因此零结果(无 XML)。
2. **4 条既有夹具按旧判据转红**:`save_slot_contract_test` 三条(覆盖被拒 / 投影真值表 /
   独立灰置通道)与 `save_slot_lock_release_test` 一条(跨会话覆盖被拒)。
   **处置 = 换判据,不是放宽断言**:
   - 闭合回路仍闭合,断点从「槽号单调」移到「**读档即锁**」(新增环 4);
   - 投影真值表按新公式逐格重算(Writable ⟺ inCtx;Loadable ⟺ ¬locked ∧ inCtx);
   - 跨会话一条改为「显式覆写允许 **且** 旧版留进 `.bak`」。

---

## 4. V-9 补轮(2026-11-04 · 用户指令「补 V-9 的日志捕获夹具」)

**新增** `unity/Assets/Tests/EditMode/Boot/adr041_save_failure_log_test.cs`(4 条)。

- **不走假 sink、不复刻格式**:测试**反射直调生产方法** `BootRoot.LogSaveFailure` /
  `LogLoadFailure`;方法若被改名,`SetUp` 的 `Assert.IsNotNull` 当场点名。
- **判据形态 = 锚定前缀的正则** `^\[7b\]\[落笔\] 槽位 N 落笔被拒\(门:…\)` + `LogAssert.NoUnexpectedReceived()`。
  前缀写错 ⇒ `Expect` 匹配不上 ⇒ 那条日志成为「未预期的错误日志」⇒ 转红
  ⇒ 「**不出现** `[7b][载入]` 措辞」是机械可判的,不靠人读日志。
- 覆盖:叙事上下文门 / 槽号非法 / 数据非法三条门名;载入侧对照(空槽仍 `Log` 级,真失败仍
  `[7b][载入]`)。

**证伪轮(1 变异)**:

| # | 变异 | 结果 |
|---|---|---|
| M1 | `LogSaveFailure` 的前缀 `[7b][落笔]` → `[7b][载入]`(即把 D8 拆开的两条又合回去) | **3 红**:三条落笔侧测试全部报「Unhandled log message: '[Error] [7b][载入] 槽位 N 落笔被拒…'」;**载入侧对照条保持绿** —— 正是应有的判别形态(它不依赖落笔前缀) |

还原 = 备份 `cp` 回 + **SHA-256 逐字节一致**
(`55c3fa76fb30d183e485e0a034e1c7651a2062c2405c0d5279b00110685320e3`,变异前后同值);
未用 `git checkout`。窗口内未编辑任何其它文件。

**回归**:EditMode `6877/6840/0/37` @ `2026-11-04T14:18:57Z` · `18c274d4` + 工作树
(基线 `6873/6836/0/37` ⇒ **+4 = 本件 4 条**);PlayMode 本轮未重跑(改动只在 EditMode 夹具与文档)。

---

## 5. 不得借绿

- **V-9 已转 PASS(2026-11-04 补轮)**,但只钉「措辞与门名」;日志的**级别选择**
  (Error vs Log)只在载入侧那条空槽上有断言,落笔侧四条全是 Error 级 —— 若将来某条失败
  降级成 Log,本件不会红。
- **「覆写后 `.bak` 能救回」只在 EditMode 成立**:没有 PlayMode / 真机走查帧。
- ~~**已锁行的呈现**(「启封」灰置)只有屏级夹具,未做视觉走查。~~
  ✅ **2026-11-04 走查轮已做**(见 §6):四帧实拍,「启封」灰置与退出焦点序均可见。
- `SaveSlotProbe` 对「final 在但坏」仍报 `Corrupt`(不读 bak)⇒ 该行呈**未绑定**,
  按下去走载入并**成功回退**。两处不矛盾(未绑定不声称任何事),但**未做走查确认观感**。

---

## 6. 视觉走查轮(2026-11-04 · 用户指令「存档册图形走查,三帧」+「三个都改,再走查一帧」)

**走查件**:`unity/Assets/Tests/PlayMode/SkeuomorphicUI/adr041_save_slots_walkthrough.cs`
(图形模式跑,`DISPLAY=:0` **不加** `-batchmode`/`-nographics`;batch 下 `WaitForEndOfFrame`
不触发 ⇒ 结构性零写盘)。帧落 `unity/Logs/shots/wb_*.png`(gitignored)。

| 帧 | 形态 | 判 |
|---|---|---|
| `wb_01_empty_slot` | 空槽行 = 格线无字 | ✅ AC-42-B5 |
| `wb_02_armed` | 已有档行 → 行文字**保留** + 两枚墨字同行靠右,焦点在「启封」 | ✅ D5 |
| `wb_03_locked` | 已锁行 → 「启封」灰置且退出焦点序,「重写」带焦点环 | ✅ D5 |
| `wb_04_full_page` | 满页 6 槽 + 留档 + 二段态 —— 页高不变 | ✅ 容量不受二段态影响 |

### 6.1 走查抓到的三处(均已修)

1. **行文字被抹掉**(实质):首版 `EnterChoice` 里 `_mark.style.display = None`
   ⇒ 按下不可逆的「重写」时看不到要盖哪一页。**改**:行文字保留,行容器改单行 flex
   (`SkeuoPaper.uss` 的 `.save-slot-item`),两枚墨字靠右。
2. **二段态三个 class 全仓零 USS 规则**(纪律):复算
   `grep -rn "save-slot-choice" unity/Assets --include=*.uss | wc -l` ⇒ 走查前 **0**。
   灰置只有 `SetEnabled(false)` 的引擎半透明,`save-slot-choice-disabled` 是死类,
   焦点落点无 44 下限声明。**改**:补三条规则,灰置走 `--skeuo-ink-faded`,
   墨字 `min-height/min-width = var(--skeuo-shared-row-height)`(AB-3)。
3. **二段态多吃一行**(容量):纵向堆叠 ⇒ 满页 7 行变 8 行。**改**:① 的同行布局顺带解掉。

### 6.2 修的过程中又抓出一处(本轮新增,如实登记)

补完 ① 后行高仍从 44 涨到 **56**,`[WB][量尺] openLabel=49.0` 定位到:**两枚墨字里的
裸 `new Label(text)` 自带 `.unity-text-element`,而主题那条
`.unity-text-element { font-size: var(--skeuo-font-size-default) }`(28px)是**直接命中
Label 的规则**,优先级高于从父元素**继承**的 `md`** ⇒ 墨字按 28px 渲染(帧里也肉眼可见
比行文字大一圈)。**改**:补后代规则 `.save-slot-choice-glyph .unity-text-element
{ font-size: var(--skeuo-font-size-md) }` ⇒ 墨字回到 15px,行高回落 44。

> **判据**:走查件内 `Assert` 两条 —— 二段态**不得抹掉行文字**(`MarkText == "叁 · 第叁日 夜"`)、
> **不得多占一行**(`|Δ| ≤ 2px`;多一行的量级是 +44)。实测 `44.0 ⇒ 45.0`,残余 1px =
> border 盒模型口径差,已写进断言注释。

### 6.3 回归(三元组)

| 套件 | 结果 | 三元组 |
|---|---|---|
| EditMode | `6877/6840/0/37` | 截至 `2026-11-04T14:53:56Z` · sprint-24 ADR-041 走查修订轮 · `9f7c994c` + 工作树 |
| PlayMode | `340/336/0/2/2` | 截至 `2026-11-04T14:55:03Z` · 同批次 |

- EditMode 与上一轮(`6877/6840/0/37`)逐位相同 ⇒ **零回归**;
- PlayMode `+1` = 走查件本身;
- ⚠️ **首跑 3 红**,全同源:`FsWriteBoundaryGate` R2 —— 走查件含写盘 + `Application.dataPath`
  锚点却无具名声明(**同一形态第七例**)。已补 `Declarations` 条目。
  该文件建好当晚只跑了 PlayMode,EditMode 全量第一次把它带进真树断言时才点名。

### 6.4 仍未结(不借绿)

- ~~「覆写后 `.bak` 能救回」**仍只有 EditMode 证据**,没有 PlayMode / 真机走查帧;~~
  ✅ **2026-11-04 走查轮已做**(见 §7)。
- ~~`SaveSlotProbe` 对「final 在但坏」呈未绑定 + 按下去回退成功,**观感仍未走查**。~~
  ⚠️ **前半句已被 §7 证伪并订正**:探针**不验校验和**,「final 在但正文坏」时报的是
  **`Occupied`**(册子照旧显示有档),只有**头部**坏才报 `Corrupt`。「按下去回退成功」成立。

---

## 7. `.bak` 救回的走查(2026-11-04 · 用户指令「走查一下『覆写后 bak 能救回』」)

**走查件**:`unity/Assets/Tests/PlayMode/SkeuomorphicUI/adr041_bak_rescue_walkthrough.cs`
(真启动序:真 `BootRoot` → 真世界 → 真 `Checkpoint` 两次 → 破坏 final → **按册子上那一行**)。

### 7.1 五条判据与实测

| # | 判据 | 实测 |
|---|---|---|
| ① | 覆写后 `.bak` = 被覆写前那一版 | `v1=20 v2=70 bak=20` ✅ |
| ② | **正文**坏掉后探针报什么 | `Occupied`(**不报 Corrupt**)—— 探针只验头部,不验校验和 |
| ③ | 按行 → 启封 → 载入,读回的是**上一版** | `LastLoadedTick = 20`(v1),当前 tick 也回到 20 ✅ —— 这就是「救回」 |
| ④ | 救回后册子长什么样 | 探针仍 `Occupied`、行文字照旧(救回**不修盘**,GDD 规则十一零 I/O) |
| ⑤ | 把**头部**也打坏(对照组) | 这才报 `Corrupt`、行呈零字(未绑定) |

### 7.2 帧(4 张,`unity/Logs/shots/`)

| 帧 | 内容 |
|---|---|
| `wb_05_after_overwrite` | 覆写后:叁 · 第壹日 昼 |
| `wb_06_after_corrupt` | **正文**打坏后:**与 wb_05 逐像素相同**(玩家看不出) |
| `wb_07_after_rescue` | 救回后:**仍与 wb_05 逐像素相同** |
| `wb_08_after_magic_corrupt` | **头部**打坏后:叁 行变空白(未绑定),这才看得出来 |

**复算**:`sha256sum unity/Logs/shots/wb_05_after_overwrite.png unity/Logs/shots/wb_06_after_corrupt.png unity/Logs/shots/wb_07_after_rescue.png`
⇒ 三行同值(`e3bc4b17…`,1186×858 / 400938 B);`wb_08` 不同(`56b8d098…`)。

### 7.3 结论与登记

- **救回成立**:真启动序里,被覆写过的槽在 final 损坏后仍能读回**上一版**(判据 ③)。
- **两个可见性缺口已立债**:`TD-persist-22`(P0 面 **N** / R4)——
  ① 回退**静默**(`SaveService.cs:641-645` 零日志);② 探针不验校验和 ⇒ 正文坏在册子上不可见。
  **✅ ① 已于同日解掉** —— 见 §7.5。② 仍 open。
- ⚠️ **不借绿**:判据 ③ 只证「读回来的 tick 是 v1」,**不证**读回的世界内容逐项正确(归 7a 既有一致性夹具)。

### 7.4 回归(三元组)

| 套件 | 结果 | 三元组 |
|---|---|---|
| EditMode | `6877/6840/0/37` | 截至 `2026-11-04T15:33:07Z` · sprint-24 `.bak` 走查轮 · `046b6139` + 工作树 |
| PlayMode | `341/337/0/2/2` | 截至 `2026-11-04T15:34:20Z` · 同批次 |

- EditMode 与上一轮(`6877/6840/0/37`)逐位相同 ⇒ **零回归**;
- PlayMode `+1`(= 341/337)= 本件走查;
- 首跑两次全绿,无需修正;本轮只补了 `FsWriteBoundaryGate.Declarations` 一条
  (走查件有写盘 + `Application.dataPath` ⇒ 同一形态第八例,这次是**事先**补的)。


---

## 7.5 回退日志(`TD-persist-22` 日志半边 · 2026-11-04 · 用户指令「加回退日志」)

### 形态:事实归 codec,记录归装配层(门 A 下唯一路径)

`Sim.Codec` 是 `noEngineReferences: true` ⇒ **它不能 `Debug.Log`**。与
`LastSaveQueryError` 同一先例:codec 只**陈述事实**,记录放在能记的那一层。

| 件 | 变更 |
|---|---|
| `Sim.Codec/SaveLoadSource.cs`(新) | 枚举 `SaveLoadSource`(`None` / `Final` / `Bak`)· `SaveLoadFallback`(`None` / `FinalCorrupt` / `FinalMissing`) |
| `SaveService.LoadBytes` | 入口**先清空**两个事实;成功分支各自赋值(失败分支不沿用上一次);回退分支注明「这是一次抢救」 |
| `LoadSequencer.Load` | `LastLoadSource == Bak` ⇒ 一条 **`Debug.Log`**(不是 Warning/Error —— 抢救是设计内行为,但必须看得见),点名「本次读到的是上一版」+「盘上正式的坏文件未修复」 |

**为什么记在 `LoadSequencer`**:载入有**两个入口** —— `BootRoot.RequestLoad`(「继续诊治」)与
`SaveSlotCommandRouter.TryLoad`(存档册「启封」)。唯一共同下游就是它,记一处两者都覆盖。

### 判据

| # | 载体 | 断言 |
|---|---|---|
| ⑥-a | `adr041_slot_overwrite_test`(EditMode,新 3 条) | 干净档 ⇒ `Final` / `None`;**正文坏 ⇒ `Bak` / `FinalCorrupt` 且读回 v1**;两级都坏 ⇒ 抛且事实出口**被清空**(防「拿上一次的回退记日志」) |
| ⑥-b | `adr041_bak_rescue_walkthrough`(PlayMode) | `LogAssert.Expect(LogType.Log, ^\[7a\]\[载入\] 槽位 3 的正式档校验和不通过\(内容被写坏\) ⇒ 已回退 \.bak:.*上一版)` + `LastLoadSource`/`LastLoadFallback` 取值 |

实跑输出:`[BAK] 救回后 LastLoadedTick=20 … 来源=Bak 原因=FinalCorrupt`。

### 回归(三元组)

| 套件 | 结果 | 三元组 |
|---|---|---|
| EditMode | `6880/6843/0/37` | 截至 `2026-11-04T15:45:06Z` · sprint-24 回退日志轮 · `fa2b1738` + 工作树 |
| PlayMode | `341/337/0/2/2` | 截至 `2026-11-04T15:46:17Z` · 同批次 |

- EditMode `+3`(6877 → 6880)= 本件新增三条,其余逐位相同 ⇒ **零回归**;
- PlayMode 计数不变(同一条走查,加了断言)。

### 仍未做(不借绿)

- **② 册子半边仍 open**:正文损坏在册子上**依旧不可见**(探针不验校验和)。要不要画
  「此页已损 / 可启封上一版」、拟物载体是什么 —— 归 **42 + 用户裁定**;
- **不得**顺手改成「读档修盘」:损坏分支零 I/O 是 GDD 规则十一的**禁止**判据。


---

## 8. 按行业惯例纠正 bak 语义(2026-11-05 · 用户指令「按行业惯例,把不合理的地方纠正」)

### 8.1 改的是什么

| 层 | 变更 |
|---|---|
| 判据 | `SaveSlotLedger.ProbeCountsBak` + `ReadFallsBackToBak` → **一条** `BakCountsAsSave(seq) → true` |
| 探测 | `SaveSlotProbe.Probe` 对**全部槽型**看 `.bak`;`SaveSlotSummary` 增 `Source`(`Final`/`Bak`) |
| 载入 | `LoadBytes` 的 `bakCountsAsOccupancy` 改用同一判据 ⇒ `final` 缺失 + `bak` 在 = **可恢复**,不再报空槽 |
| 最近一份 | `TryGetMostRecentSave` 的候选枚举加 `save_*.bak` ⇒ 只剩备份的槽也能进「继续诊治」 |

### 8.2 为什么(一句话)

`WriteAtomic` 的 `Move(final→bak)` 与 `Move(tmp→final)` 之间有窗口;崩在窗口里 ⇒ `final` 没了、
`.bak` 是一份**完整上一版**。分叉口径下探测说「空槽」、读档也抛 ⇒ **那份世界不可达**。
备份的语义就是「主档没了用它」。合并**不会**重造 `TD-persist-19`(那条病是「探测认而读档不认」,
今日三处同源)。

### 8.3 判据

| 载体 | 断言 |
|---|---|
| `manual_slot_bak_scope_test`(改判 4 条) | `final` 缺失 + `bak` 在 ⇒ 探测 `Occupied` / `Source=Bak` / `LoadBytes` 成功且读回 bak 的 tick;「最近一份」认它;主档回位后 `Source` 翻回 `Final`(**来源字段有判别力**) |
| 同上 · 谓词条 | `BakCountsAsSave` 对 5 个槽型全 `true`(唯一判据) |
| `save_slot_probe_test`(改判 1 条) | 手动槽 bak-only ⇒ `Occupied` + `Source=Bak`(原判据是 `Empty`,已按新口径改写) |
| `adr041_bak_rescue_walkthrough`(PlayMode · 新增判据 ⑦ + 帧 `wb_09`) | 删掉主档后册子那一行**还在**(`叁 · 第壹日 昼`),探针 `Occupied` / `Source=Bak` |

**帧对照**:`wb_08`(头部坏)= 该行**空白**;`wb_09`(主档整个删掉、只剩备份)= 该行**在**。
两张图正好把「读不出来」与「只剩备份」分开画出来。

### 8.4 回归(三元组)

| 套件 | 结果 | 三元组 |
|---|---|---|
| EditMode | `6880/6843/0/37` | 截至 `2026-11-04T16:18:05Z` · sprint-24 bak 语义统一轮 · `d5db89d8` + 工作树 |
| PlayMode | `341/337/0/2/2` | 截至 `2026-11-04T16:19:18Z` · 同批次 |

计数与上一轮逐位相同(改的是既有夹具的**判据**,不是新增)——**零回归**。

### 8.5 没做的两件(理由)

1. **写窗口本身没改**(`Move` → `Copy`)。行业写法通常是 copy,但 `WriteAtomic` 同时服务
   退出保存,而退出保存有 1 s 的 join 预算(`TD-persist-14`:join 超时 ≠ 写盘失败)。
   多一次整档拷贝会把这个假阳性面推高。**恢复路径已覆盖**,故不动。
2. **呈现面没改**。`Source` 已在数据面可分,但「要不要告诉玩家这一页来自备份」是 42 的形态问题,
   `AC-7b-07` 禁徽章 / 红字 / toast ⇒ 归 `TD-persist-22` ②。

### 8.6 顺带撞见的另一个缺陷(与本轮无关)

PlayMode 走查的**第二次载入**抛出:

```
[Exception] InvalidOperationException: 体征链 Step 时间倒流:tick=21 < 上次 70
```

`DiseaseVitalsService._lastTick`(`Gameplay.Boot/DiseaseVitalsService.cs:121`)在**读档时不回退**,
全类无重置入口 ⇒ 会话内读一份**更早**的档、关册让世界继续跑,就会每 tick 抛一次,直到时钟追平。
已立 **`TD-sim-05`**(`open` / P0 面 **Y** / R1),解除动作 = 给 `DiseaseVitalsService` 加读档重建入口
(先例 `AnchorCheckpointTrigger.ResetCursor`)+ 接进 `LoadSequencer`(次序须裁)。
⚠️ **走查件已改为不触发它**(本件到「册子上看得见」为止,不再做第二次载入)——
**债不解、走查也不会红**,这是如实登记,不是修掉了。
