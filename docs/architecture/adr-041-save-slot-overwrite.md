# ADR-041 — 存档槽的覆盖语义(空槽即写 · 覆写不清锁 · 行内二段确认)

| Field | Value |
|-------|-------|
| **Status** | **Accepted**(2026-11-04 · 起草于同日用户裁定「**我觉得我刚刚的更合理**」→ 草案呈报 → 用户「**写吧,三个都按你建议的来**」= 三项细项裁定 + 写入授权,故转 Accepted。⚠️ **D2 / D4 / D9 / D10 的细项由本轮就地拟定,未经用户逐条确认** —— 同 ADR-034 / 037 / 038 / 039 / 040 口径) |
| **Date** | 2026-11-04 |
| **Supersedes** | 无(就地修订 GDD 三处判据与 ADR-010 §四 的一条加注,**不整件推翻任何 ADR**) |
| **Related** | ADR-010 §四(损坏恢复 / bak 回退)· ADR-013 §9 C3(42 只渲染)· ADR-040 D6(删世界,二次确认先例)· GDD `save-slot-ui.md` 规则二 / 规则三 / 规则四 / `AC-7b-02` / `AC-7b-07` / `AC-7b-08` / `AC-7b-10` · GDD `persistence-service.md` 规则十四 / `AC-7a-19` / `OQ-7a-8`(已裁)· UX `save-slots-7b.md` §7 交互表 / §6 状态表 |

---

## Context

### 1. 「只进不退」的定价**在实现里并不存在**

`save-slot-ui.md:22` 把「槽位只进不退」写成反 save-scum 的玩家可见侧,其定价论证在
`save-slot-ui.md:34-37`:

> 存 A → 坏判断 → 读 A(A 被锁)→ 此刻仍无未决动作 ⇒ 存 A+1(合法,只进不退)→ 再坏 → 读 A+1
> …… **每页 = 一次免费重试**

这段推理成立的前提是**页是稀缺的**。实测三条,前提不成立:

| 事实 | 出处 |
|---|---|
| 手动槽**总数无上限** ——「超出即续页」 | `design/ux/save-slots-7b.md:108` |
| 保留策略已裁 = **手动池永不自动裁剪** | `save-slot-ui.md` 边界表 + `OQ-7a-8`(2026-10-06 已裁) |
| 手动存本身已被**叙事上下文门**管住(只能在脉案落笔 / 病例结案 / 出诊启动之后) | `SaveSlotLedger.cs:155-158` |

⇒ 玩家想重试,写 A+1、A+2、A+3 即可,**一分代价不付**。该约束今日的实际效果只有一个:
**一个槽永远不能复用**。这是纯摩擦,而它被称为「反 scum 的承重墙」。

真正在守反 scum 的是另外两条,**都不依赖**只进不退:

- **读档即锁**(`SaveSlotLedger.cs:181-195`,`OnLoaded` 置锁 / `OnNarrativeAnchor` 清锁);
- **载入安全上下文门**(`save-slot-ui.md:116-127`,未决医疗动作中不可读)。

覆盖**不会**破这两条 —— 条件是守住 D3 的不变式(覆写不清锁)。

### 2. 空槽行是**死胡同**

7b 交互表(`save-slots-7b.md:228`)写的落笔条件是「**空行** / 末行后 Submit」,
而实现只做了「末行后」那半:

```csharp
// SaveSlotsScreen.cs:376-380
public void Activate(int rowIndex)
{
    if (rowIndex < 0 || rowIndex >= _slots.Count) return;
    _sink?.TryLoad(_slots[rowIndex]);      // ← 空槽也走载入
}
```

空槽按下去 = 一次必然失败的载入,`BootRoot.LogLoadFailure`(`:2355-2360`)打一行普通日志
`[7b][载入] 槽位 N 是空的(从未存过)—— 正常态`,**界面零变化**。
玩家看到的是「这行按了没用」。

### 3. 三处判据把「不能覆盖」写死了

| 件 | 位置 | 原文要点 |
|---|---|---|
| 7b GDD 规则二 | `save-slot-ui.md:22` / `:111` | 覆盖既有槽位 ⇒ **拒绝**,7b 呈现灰置 + 页角小字 |
| 7a GDD 规则十四 | `persistence-service.md:723-729` `AC-7a-19 (b)` | 覆盖既有槽位 ⇒ **被 7a 拒绝**(`slot_seq` 单调校验) |
| 7b GDD 走查判据 | `save-slot-ui.md:408` `AC-7b-08` | 「**不能覆盖**在幻想层讲得通 —— 两页保住,不可改」 |
| 实现 | `SaveSlotLedger.cs:147-153` | 门①:`seq >= 3 && seq < next` ⇒ 抛 `InvalidDataException` |

外加一条**隐含依赖**:ADR-010 §四 2026-11-03 的加注「玩家槽无 bak ⇒ 损坏走 Corrupt 报损、
不静默兜底」,其前提是「手动槽一生只写一次」(`SaveService.cs:532-533`:正因如此
`save_N.bak` **永不出现**)。开启覆盖后这个前提消失,该加注必须同批重新裁。

### 4. 不裁的代价

空槽死胡同已在实际走查中出现;而「想复用一页」是玩家最自然的操作 ——
不裁则玩家只能不断续页,册子无限变厚,`OQ-7a-8` 的「永不自动裁剪」会把账单全部留给玩家。

---

## Decision

### D1 — 空槽行 Submit = 落笔(**写该行自己的 `slot_seq`**)

> 用户裁定(2026-11-04):「**点一下空挡就是保存**」。

- 屏侧:`SaveSlotsScreen` 的空槽行与末页「留档」行**同一出口**,都发落笔意图;
  区别只在**目标槽**:空槽行带自己的 `slot_seq`,「留档」带台账的 `NextWritableSeq`。
- 执行面仍是**唯一写入口** `SaveService.Checkpoint(SaveSlot)`
  (`save-slot-ui.md:101-102`:7b 不得调用 `Checkpoint` 之外的写入 API)。
- 7b **不判**「这槽能不能写」(`AC-7b-02` 不变);被拒 ⇒ 页角注(拟物,零弹窗)。

### D2 — 覆盖既有手动槽 = **允许**

> 用户裁定(2026-11-04):「**点一下已有存档会提示读档还是覆盖存档**」。

- 删除 `SaveSlotLedger.EnsureWritable` 的**门①**(`SaveSlotLedger.cs:147-153`);
- **保留**门⓪(槽号 0 非法)、门(c)(叙事上下文)、以及 `SaveService.Checkpoint` 侧的
  fail-closed 三件(`RequireNarrativeContext` / `RequireCompose` / `RequireWriteScheduler`)
  与「目录枚举失败 ⇒ 拒绝一切写入」(`SaveService.cs:778-784`);
- 保留槽 1 / 2 的语义**不变**(固定身份、原地覆盖,玩家手动写路径不指向它们);
- `_nextWritableSeq` **语义收窄**为「『留档』的默认目标」,**不再是写入门**;
- **存档格式零变化** ⇒ `SaveVersion` 不动、**无迁移链**(见 D10)。

### D3 — **覆写不清锁**(本裁的承载不变式)

```csharp
// SaveSlotLedger.cs:181-195 —— 本裁一字不改
public void OnLoaded(SaveSlot slot)  { _locked.Add(slot.SlotSeq); }
public void OnNarrativeAnchor()      { _locked.Clear(); }
```

- `OnCheckpointCommitted`(`:166-175`)只推高水位,**不得**触碰 `_locked`;
- `Checkpoint` 路径**不得**新增任何 `_locked.Remove` / `Clear` 调用;
- ⇒ 「读 A → 覆写 A → 再读 A」在同一叙事窗口内仍被 `IsLocked` 挡住。
- ⚠️ **这是本裁唯一的新失效面**:若有人「顺手」在写成功时解锁,反 scum 立刻变成零成本循环。
  由 V-3 专项夹具钉住(覆写后 `IsLocked == true`)。

### D4 — 覆写的旧版留 `.bak`,且**读侧允许回退**

`WriteAtomic` 本就是「final 存在则先 final → bak」(`SaveService.cs:563-570`),
⇒ 开启覆盖后 `save_N.bak` **第一次真的会出现**(该路径此前是死代码,见 `:532-533`)。

两条**必须分开**的谓词(今日合为 `FallsBackToBak`,`SaveSlotLedger.cs:68`):

| 谓词 | 用途 | 手动槽取值 |
|---|---|---|
| `ProbeCountsBak(seq)` | 槽位**探测** / 占用态(`SaveSlotProbe` / `TryGetMostRecentSave` / `BuildSaveSlotViews`) | **false** —— 有档 = `final` 存在 |
| `ReadFallsBackToBak(seq)` | 读档时 `final` 校验坏是否回退 | **true** —— 校验和过才用 |

- 为何必须拆:**`TD-persist-19` 那条病**(「菜单说有档 ⇒ 按下去说空槽」)源于探测把
  `.bak` 算成占用;拆分后探测仍只看 `final`,该病不复发,而回退面获得真正的备份;
- 保留槽 1 / 2 两谓词均为 **true**(不变);
- 读侧回退仍须**过校验和**(与保留槽同一路径,`SaveService.cs:621-626`)。

### D5 — 42 侧形态:**行内二段态,零系统弹窗**

> 用户裁定(2026-11-04):「**提示读档还是覆盖**」→ 采纳,但**用拟物行内态承载**。

| 行型 | 第一次 Submit | 第二次 Submit |
|---|---|---|
| **空槽行** | 立即落笔(D1) | — |
| **已有档行** | 该行「翻起一角」,在行内出示两枚墨字:**启封** / **重写**;焦点默认落 **启封** | 落在哪一枚就执行哪一个:`启封` → `TryLoad(seq)`;`重写` → `TrySaveAt(seq)` |
| **已锁槽行**(读档即锁) | 同上,但 `启封` 灰置(不可提交),只留 `重写` | 同上 |
| 移焦 / 取消 | 两枚墨字合回,行复原 |

- **不违反 `AC-7b-07`**(`save-slot-ui.md:407`):它禁的是弹窗 / toast / 进度条 /
  「存档成功」类文本;**行内态不是弹窗**,且 42 已有同构先例 —— 主菜单「删除世界」
  的行内删除态 + 两次确认(`MainMenuScreen`,2026-11-04 走查通过);
- **默认焦点在安全侧**(启封):误按一次不会毁档;
- ⚠️ 「重写」仍然是**不可逆**动作 —— 二段确认是唯一防线,不得省。

### D6 — 槽位**仍不设硬上限**

- 与 `OQ-7a-8`(2026-10-06 已裁:手动池永不自动裁剪)一致,本件不改该裁定;
- 覆盖给了玩家「复用旧页」的出口,反而不逼册子膨胀;
- **不**做「设上限以重建稀缺」—— 那会惩罚长期玩家,而反 scum 已由叙事门 + 载入安全门承担(见 Context §1)。

### D7 — 7b 仍**零本地判定**(`AC-7b-02` / `AC-7b-10` 不变)

- 屏只转达**意图**(`TryLoad(seq)` / `TrySaveAt(seq)`),不判断「能否覆盖 / 能否载入」;
- 能否写 = `Checkpoint` 内四道门的返回值;7b 只把它画成灰置 / 页角注;
- 「空槽 / 已有档 / 已锁」的**呈现**依据是 7a 的 `SaveSlotView` 投影,不是 42 自己算的。

### D8 — 落笔失败日志与载入失败**拆开**

- 今日两者共用一个 sink(`BootRoot.cs:1017` 传的是 `LogLoadFailure`)⇒ 落笔被拒会打出
  `[7b][载入] 槽位 N 载入失败`,文字与行为不符;
- 本裁:新增 `LogSaveFailure`,`[7b][落笔] 槽位 N 落笔被拒:<门名>`;
- 门名须点名(叙事上下文 / 目录枚举失败 / 组版失败 / 写队列未注入),不得只打异常全文。

### D9 — 叙事上下文门**不变**(`AC-7a-19 (c)` 保留)

- 覆盖**不**开新窗口:手动存仍只许在「无未决医疗动作」上下文;
- `EnsureWritable` 的门(c)与 `Checkpoint` 的 `RequireNarrativeContext` 原样保留。

### D10 — **存档格式不变**

- 覆盖只改**写入目标的选择**与**旧档去向**,不改字节布局 ⇒ `SaveVersion` 保持 5、
  **不新增迁移脚本**、`ConfigVersion` / 时间参数段照旧(ADR-037 / ADR-038 面不动)。

### Architecture

```
42 存档册(SaveSlotsScreen)                       装配层                      7a
┌──────────────────────────┐          ┌────────────────────────┐   ┌──────────────────────────┐
│ 空槽行  ── Submit ────────┼─ TrySaveAt(seq) ──▶│ SaveSlotCommandRouter  │──▶│ SaveService.Checkpoint   │
│ 已有档行 ─ 1st ─▶ 两枚墨字 │                    │  TrySaveAt = Checkpoint │   │  ├ 门:叙事上下文(fail-closed)│
│          └ 2nd:          │                    │  TryLoad  = Sequencer   │   │  ├ 组版 → 原子写(final→bak)│
│             启封 ────────┼─ TryLoad(seq) ────▶│                        │   │  └ 写成功 → OnCheckpointCommitted│
│             重写 ────────┼─ TrySaveAt(seq) ──▶│  失败 → LogSaveFailure  │   │      (只推高水位,不碰 _locked)│
│ 末页「留档」─────────────┼─ TrySave() ───────▶│        → Checkpoint(next)│  └──────────────────────────┘
│ 页角注(拟物,零弹窗)◀───┴────────────────────┘
└──────────────────────────┘
                    ▲ 7b 零本地判定:写不写得进由 7a 返回值决定(AC-7b-02)
```

### Key Interfaces

```csharp
// Gameplay.UI/Skeuomorphic/SaveSlotCommands.cs —— 接口新增一个成员(D1/D2 的出口)
public interface ISaveSlotCommandSink
{
    bool TryLoad(uint slotSeq);
    bool TrySave();                              // 留档:写台账 NextWritableSeq(不变)
    bool TrySaveAt(uint slotSeq);                // 🔁 本裁:写指定槽(空槽落笔 / 覆写)
}

// SaveSlotLedger —— 门① 删除,其余不动
public void EnsureWritable(SaveSlot slot);       // 门⓪ + 门(c) 保留;门①(只进不退)删除
public bool IsLocked(SaveSlot slot);             // 原样 —— 覆写不清锁(D3)

// bak 谓词拆分(今日合为 FallsBackToBak)
public static bool ProbeCountsBak(uint seq);     // 手动槽 false / 保留槽 true
public static bool ReadFallsBackToBak(uint seq); // 手动槽 true  / 保留槽 true
```

### Implementation Guidelines

1. **先拆谓词,再开门**:`FallsBackToBak` 的两处消费面(探测 / 读档)必须先分家,
   否则「开了覆盖但探测仍按旧口径」会产出新的「说有档 / 说空槽」分叉。
2. **门① 删除后 `_nextWritableSeq` 的语义收窄**:它只剩「留档的默认目标」与
   「册子显示区间右端」两个消费者(`BootRoot.BuildSaveSlotViews:2242-2252`);
   **不得**把「能否写入」重新挂回它。
3. **不得顺手解锁**:`OnCheckpointCommitted` 保持逐字不变(D3)。
4. **7b 不新增判定**:`SaveSlotsScreen` 只按 `SaveSlotView` 的呈现字段决定画什么,
   提交时只转发意图(D7)。
5. **二次确认的状态机住在屏内**(与主菜单删除态同构):进入态 / 返回态 / 移焦合回,
   三处都必须有夹具。
6. **失败路径不得静默**:落笔被拒一律经 `LogSaveFailure` 点名门名(D8)。

---

## Consequences

### Positive

- **空槽死胡同消失** —— 玩家点空页就存,不再撞一个必然失败的载入;
- **槽位可复用** —— 册子不再单调变厚,`OQ-7a-8` 的「永不自动裁剪」不再把账单全给玩家;
- **反 scum 的承重结构被如实识别** —— 从「一条自称在定价的约束」收敛为两条真实的门
  (叙事上下文 + 载入安全门 + 读档即锁),后续设计不必再为一个不存在的定价辩护;
- **备份第一次真的可用**:`.bak` 从「写了永不读」变成「损坏时可回退」。

### Negative

- **覆盖不可逆** —— 玩家可能毁掉唯一回滚点;靠 D5 二段确认(默认焦点在安全侧)+ D4 `.bak` 缓解,**不能消除**;
- **新增一条必须守的不变式**(D3 覆写不清锁)—— 它是本裁唯一的新失效面,靠夹具 + 注释钉;
- **`AC-7b-08` 的走查判据作废** —— 「不能覆盖在幻想层讲得通」要改写为「旧页可重写,但按下两次才落」,
  文案归 narrative 内容轮(`OQ-7b-2` 的同一归属);
- **一处历史加注失效** —— ADR-010 §四 2026-11-03 的「玩家槽无 bak」前提被撤销。

### Neutral

- 保留槽 1 / 2 的语义、`SaveVersion`、迁移链、`ConfigVersion` 段、世界目录(ADR-040)**全部不动**;
- 「留档」行保留,语义从「唯一落笔入口」降为「追加一页的快捷方式」。

---

## Alternatives

### Alternative 1 — 保持只进不退,只补「空槽即写」

- **Description**:保留门①,只把空槽行的死胡同补上(上一轮的建议方案)。
- **Pros**:改动面最小;不触碰 ADR-010 §四 的 bak 口径。
- **Cons**:玩家仍不能复用一页;而 `OQ-7a-8` 的「永不自动裁剪」+ 无上限槽位会让册子无限变厚。
- **Rejection Reason**:其**定价理由在实现里不成立**(Context §1 —— 页是免费且无限的),
  该约束只剩摩擦。用户裁定取覆盖。

### Alternative 2 — 覆盖用系统弹窗(Unity modal / UIDocument 覆盖层)

- **Description**:点已有档弹一个「读档 / 覆盖」对话框。
- **Pros**:语义最直白;实现最省事。
- **Cons**:直接违反 `AC-7b-07`(零弹窗 / toast)与 7b 的幻想纪律(`save-slot-ui.md:86-88`
  「7b 是一本册子,不是设置菜单」);手柄焦点还要多一层栈。
- **Rejection Reason**:行内二段态(D5)能给出同一语义,且不违约束。

### Alternative 3 — 「覆盖 = 先删旧页,再写新页」

- **Description**:把覆盖拆成两个显式动作 —— 删除该槽,然后再存。
- **Pros**:每个动作都可解释,「删除」还能复用主菜单删除态的形状。
- **Cons**:两次确认变三次;玩家看不到「删除」与「覆盖」的差别;删除还要另立「删除后编号是否复用」的裁定。
- **Rejection Reason**:语义等价于覆盖,却多一道门与一套裁定。

### Alternative 4 — 给槽位设上限,以重建「页的稀缺性」

- **Description**:册子最多 N 页,写满即拒;用稀缺把「每页 = 一次免费重试」的定价做实。
- **Pros**:让 GDD 原论证**变成真的**。
- **Cons**:与 `OQ-7a-8`(手动池永不自动裁剪)**直接冲突**;惩罚长期玩家;还需要裁剪 / 淘汰策略;
  而反 scum 已由叙事上下文门 + 载入安全门承担。
- **Rejection Reason**:为一条并非承重的定价,引入一套新的容量与裁剪裁定。

---

## Risks

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| **R1 覆写毁掉唯一回滚点**(不可逆) | 中 | 高(玩家丢 40 小时进度) | D5 二段确认 + 默认焦点在「启封」;D4 `.bak` 留底;页角注写明「重写会盖掉这一页」 |
| **R2 覆写绕过读档即锁** | 低(须有人"顺手"解锁) | 高(反 scum 归零) | D3 钉死;V-3 专项夹具;`OnCheckpointCommitted` 逐字锁死 |
| **R3 `.bak` 口径改动复发「说有档 / 说空槽」** | 中 | 中(玩家点空槽) | D4 两谓词拆分;探测只看 `final`;V-5 夹具 |
| **R4 `AC-7b-08` 走查判据作废** | 高(必然) | 低 | 改写为「旧页可重写,但按下两次才落」;文案归 narrative 内容轮 |
| **R5 二次确认态与焦点序打架**(手柄无指针) | 中 | 中(卡死一步) | 两枚墨字均可焦点;移焦即合回;PlayMode 走查帧(与主菜单删除态同款) |
| **R6 旧存档 `.bak` 残留被新口径读回** | 低 | 低 | 读侧仍过校验和;`ProbeCountsBak` 恒 false ⇒ 不影响「有没有档」的判断 |

---

## Migration Plan

**本件不实现**。实现按下列次序,每步独立可回归:

1. **谓词拆分**(`SaveSlotLedger`):`FallsBackToBak` → `ProbeCountsBak` / `ReadFallsBackToBak`,
   并逐处改消费面(`SaveSlotProbe` / `SaveService.TryGetMostRecentSave` / `LoadBytes`)—— **先做,后开门**;
2. **删门①**(`SaveSlotLedger.EnsureWritable:147-153`),保留门⓪ / 门(c);
3. **新增 `TrySaveAt(uint)`**(`ISaveSlotCommandSink` + `SaveSlotCommandRouter`),
   失败面拆出 `LogSaveFailure`(D8);
4. **屏侧**(`SaveSlotsScreen`):空槽行 → 落笔;已有档行 → 行内两枚墨字 + 默认焦点「启封」;
5. **文档回写**:`save-slot-ui.md`(规则二 / 规则三 / 边界表 / `AC-7b-08`)、
   `persistence-service.md`(`AC-7a-19 (b)`)、`save-slots-7b.md`(§6 状态表 / §7 交互表)、
   `adr-010` §四加注;
6. **夹具**:见 §Validation;
7. **债册**:本件解除的登记项与新增项各记一笔。

**Rollback plan**:回滚 = 恢复门① + 屏侧退回「留档」单入口 + `TrySaveAt` 保留但不接线。
存档字节**零变化** ⇒ 回滚不产出不可读的档(这是本裁「格式不动」的直接红利)。

---

## Validation

⚠️ **V-1…V-9 全部未跑 —— 禁借绿**。Accepted 表示方向已裁,不表示任何判据已通过。

| # | 判据 | 载体(待建) | 状态 |
|---|---|---|---|
| V-1 | 空槽行 Submit ⇒ 落笔到**该行自己的 `slot_seq`**,盘上出现 `save_{seq}.bin` | EditMode(`save_slots_screen` 屏级 + `save_slot_save_wiring_test` 路由级) | **NOT-RUN** |
| V-2 | 已有档行**第一次** Submit **零写盘**(前后目录 SHA-256 逐档相同),第二次才落 | EditMode(副作用判据,不是「看返回 true」) | **NOT-RUN** |
| V-3 | **覆写后 `IsLocked(slot)` 仍为 true**(D3) | EditMode(`SaveSlotLedger` 直测 + 端到端一次) | **NOT-RUN** |
| V-4 | 覆写生成 `save_{seq}.bak`,内容 = **上一版字节**(逐字节) | EditMode(临时目录) | **NOT-RUN** |
| V-5 | `final` 校验坏 ⇒ 读侧回退 `.bak` 成功;**同时** `ProbeCountsBak(seq) == false`(探测仍报「有档」而非「空槽」) | EditMode(两条独立断言,不得合并) | **NOT-RUN** |
| V-6 | 非叙事上下文 ⇒ 覆写被 7a 拒;7b 只画页角注 | EditMode(现成门夹具扩一条覆写路径) | **NOT-RUN** |
| V-7 | 「留档」仍写 `NextWritableSeq`(追加语义未变) | 既有 `save_slot_save_wiring_test` 保持绿 | **NOT-RUN** |
| V-8 | 42 **零本地判定**:屏不含「能否覆盖 / 能否载入」的判定分支(`AC-7b-02` / `AC-7b-10` 不变) | 静态复核 + `AC-42` 侧的既有反射门 | **NOT-RUN** |
| V-9 | 落笔失败日志**不出现** `[7b][载入]` 措辞,且点名门名 | EditMode(日志捕获) | **NOT-RUN** |

**必须同时跑的反面**(禁借绿):
- 删门①后,**非手动槽**(0 / 1 / 2)的写路径行为**不变**(保留槽仍原地覆盖);
- `SaveVersion` 仍为 **5**,无新迁移脚本(`grep` 迁移链注册表)。

---

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS(6000.3.24f1) |
| **Domain** | Core / UI(存档 I/O + UI Toolkit 行内态) |
| **Knowledge Risk** | **LOW** |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`;本件不触任何引擎 API |
| **Post-Cutoff APIs Used** | **None** |
| **Verification Required** | None —— 纯 BCL 文件操作 + UI Toolkit 已有元件的状态切换(与主菜单删除态同构) |

**LOW**。零 post-cutoff API;`Sim` / `Sim.Contracts` 引用集一字不改(门 A / b2 / ADR-025 §① 全部继续成立);
`SaveHeader` / `SaveCodec` / 迁移链**零改动**。

---

## GDD Requirements Addressed

| AC / 规则 | 本 ADR 的关系 |
|---|---|
| `save-slot-ui.md` 规则二(槽位只进不退) | **就地修订**:从一个「写入门」降级为「追加语义 + 页数单调」;覆盖不再被拒 |
| `save-slot-ui.md` 规则三(读档即锁) | **不变**,并由 D3 显式加固(覆写不清锁) |
| `save-slot-ui.md` 规则四(叙事事件上下文) | **不变**(`AC-7a-19 (c)` 原样) |
| `save-slot-ui.md` 边界表「玩家尝试覆盖既有槽位」 | **改写**:从「7a 拒绝 + 灰置」改为「行内二次确认后允许」 |
| `AC-7b-02` / `AC-7b-10`(7b 零本地判定) | **不变** —— 意图选择不是判定,写不写得进仍由 7a 返回值决定 |
| `AC-7b-07`(零弹窗 / toast) | **不变** —— 行内二段态不是弹窗(D5 已论证) |
| `AC-7b-08`(不能覆盖在幻想层讲得通) | **作废并改写** 为「旧页可重写,但须两次落定」;文案归 narrative 内容轮 |
| `persistence-service.md` 规则十四 / `AC-7a-19 (b)` | **就地修订**:(b) 由「覆盖被拒」改为「覆盖允许,但不清锁」 |
| `persistence-service.md` `OQ-7a-8`(手动池永不裁剪) | **不变** —— D6 与之对齐 |
| `save-slots-7b.md` §6 状态表「空册」/ §7 交互表「存盘(落笔)」 | **回写**:空行落笔落地;新增「覆写」行与二段态 |
| ADR-010 §四(损坏恢复) | **就地加注**:玩家槽的 `.bak` 由「永不产生」变为「覆写时产生且读侧可回退」 |

---

## Related

- **ADR-010** —— 存档布局与唯一写入口;§四 的一条加注被本件就地修订
- **ADR-013 §9 C3** —— 42 只渲染,永不持有游戏状态(7b 零本地判定的上位依据)
- **ADR-040 D6** —— 「删除世界」的行内二次确认形态,是本件 D5 的直接先例
- **代码落点(待实现)**:`unity/Assets/Sim.Codec/SaveSlotLedger.cs` ·
  `unity/Assets/Sim.Codec/SaveService.cs` · `unity/Assets/Gameplay.Boot/SaveSlotCommandRouter.cs` ·
  `unity/Assets/Gameplay.Boot/BootRoot.cs` · `unity/Assets/Gameplay.UI/Skeuomorphic/SaveSlotCommands.cs` ·
  `unity/Assets/Gameplay.UI/Skeuomorphic/Screens/SaveSlotsScreen.cs`
