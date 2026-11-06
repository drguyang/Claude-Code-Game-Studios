# P0 空间解 —— 2 病人两角 · 灶台第三角 · 真包验证

| 字段 | 值 |
|---|---|
| **日期** | 2026-11-06 10:16–10:30 CST |
| **用户裁定** | 「p0 的空间太小是主要问题,这个阶段让病人至多同时出现 2 个,位置分居 2 个角落,灶台占另一个角落,其他先不改看看能不能测试成功」 |
| **出包** | `unity/builds/Linux-x64/大医精诚.x86_64`(`Build Finished, Result: Success`,两轮:含探针 / 含地面修复) |
| **世界** | 测试局 = 本轮「新起一局」新建的 `w3`(**验证后已删除,索引已还原**);w1 / w2 逐字节还原 |
| **判据** | 一次性探针 `[PROBE-4]`(打印玩家格 / 半径内候选 / 选中者),**验证后已从源码移除**(见 §5) |
| **截图** | `production/qa/evidence/walkthrough-2026-11-06-space-fix/01..07*.png` |

---

## 1. 先说结论(三条,按重要性排序)

1. **上一轮的归因是错的**。走查记录的「病人优先级压住灶台」**不成立**:实测玩家从未离开 `(0,0,0)`,
   而在旧代码里**所有病人都登记在 `(0,0,0)`** ⇒ 病人 `d∞ = 0`,按 F-4.1 的**键 ①(格距)** 就赢了,
   `KindPriority`(键 ②)**根本没有参与**。
2. **玩家走不动的真因是地面**,不是交互:World 的 `Ground` 是 Unity 内置 **Plane(10×10 米)**、
   位于原点,而世界格是 **16×16** ⇒ `x,z ≳ 5` 之外**脚下没有地板**。实测探针给出
   `player=0,-6,-9` / `player=-9,-7,0`(自由落体 + 滑行)。
3. **修完地面 + 空间解之后,炮制屏真的开得出来**:玩家 `(15,0,15)`,`E` ⇒
   `Utensil#9@14,0,14 d=1 => Utensil#9`(见 §3 逐字)。模态开时 `E` 被挡(F-4.2),`Escape` 关掉后 `E` 再记一条。

**但炮制屏是空壳**:开出来的是一张**窄纸带**,无标题 / 无候选行(§4)。内容面仍 `NOT-RUN`。

---

## 2. 本轮改了什么(六处 + 两个新文件)

| # | 落点 | 内容 |
|---|---|---|
| 1 | `Gameplay.Boot/PatientWiringPositions.cs`(**新**) | 接线期病人格单一出处:`(1,0,14)` / `(14,0,1)` 两个内角;`Count = 2` |
| 2 | `Gameplay.Boot/PatientAppearedDriver.cs` | 上限 3 → **2**;登记格由字面量 `(0,0,0)` 改为 `PatientWiringPositions.CellFor(_spawnedCount)` |
| 3 | `Gameplay.Boot/CompositionRoot.cs` | 52 池路径的两处 `(0,0,0)` 锚点 → 同一张表;病人**视觉锚点**由 `RingPatientAnchorSource`(环玩家)→ `PresenceCellAnchorSource`(读在场格) |
| 4 | `Gameplay.Presentation/Patients/PresenceCellAnchorSource.cs`(**新**) | 视觉锚点 = 登记簿的整数格 ⇒ **看得到的 = 选得中的** |
| 5 | `Editor.Tools.Level/P0ClinicLevel.cs` | 灶台槽 `(10,0,5)` → **`(14,0,14)`**(第三个内角);重导 + 重烘 |
| 6 | `Scenes/World.unity` | `Ground` 的 Transform:`pos (0,0,0) scale (1,1,1)` → **`pos (7.5,0,7.5) scale (1.8,1,1.8)`**(18×18,覆盖 `x,z ∈ [-1.5,16.5]`) |

**重导 / 重烘链**(逐条实跑,均 exit 0、零 `error CS`):

```bash
cd unity
unity -batchmode -quit -projectPath "$PWD" \
  -executeMethod DaYiJingCheng.EditorTools.Level.LevelExporter.ExportP0ClinicToRepo
unity -batchmode -quit -projectPath "$PWD" \
  -executeMethod DaYiJingCheng.EditorTools.Bake.DataBakeMenu.BakeWorldBuildSlots
unity -batchmode -quit -projectPath "$PWD" \
  -executeMethod DaYiJingCheng.EditorTools.Bake.DataBakeMenu.BuildAddressablesContent
unity -batchmode -quit -projectPath "$PWD" \
  -executeMethod DaYiJingCheng.EditorTools.Bake.PlayerBuildCli.Build
```

> ⚠️ **`m_BuildAddressablesWithPlayerBuild: 0`** ⇒ 打包**不会**顺带重建内容 —— 上面第三条是必需的,
> 漏了则改的是 JSON / cooked、跑的还是旧 bundle。

导出产物核验(**只有一件变了**):

```bash
python3 -c "import json;d=json.load(open('assets/data/world_buildslots.json'));print([s for s in d['slots'] if s['slot_type']=='Utensil'])"
# ⇒ [{'cell': [14, 0, 14], 'slot_type': 'Utensil', 'baked_initial': True, 'module_id': 12}]
# 烘后:world_buildslots.cooked.bytes = 468 B · ConfigVersion = 0xD5CCD18E
```

---

## 3. 实测(探针逐字)

`[PROBE-4]` 在 `InteractionPressRouter.OnPress` 里打印:玩家格 · 半径内**全部**候选(种类 / 格 / `d∞`)· 选中者。

**(甲)修复前 —— 玩家卡在世界角,病人也在同一格**

```
[PROBE-4] player=0,0,0 inRange=4 | ForageSpot#0@2,0,2 d=2 | BuildSlot#17@1,0,1 d=1 | BuildSlot#18@2,0,1 d=2 | BuildSlot#19@3,0,1 d=3 => BuildSlot#17
```

**(乙)修复前 —— 按方向键之后,玩家在下坠**

```
[PROBE-4] player=0,-6,-9 inRange=0 => None
[PROBE-4] player=-9,-7,0 inRange=0 => None
```

⇒ 这解释了**全部**历史观测:玩家一直停在原点附近,而旧代码把**所有病人**登记在 `(0,0,0)`,
所以 `E` 总选中病人(`d∞ = 0`)、`脉案` 总打开;**与 `KindPriority` 无关**。
同时「离开病人区按 E 什么都不开」= 那一刻在场集为空(病人到期退场)。

**(丙)空间解生效 —— 走到灶台角**

```
[PROBE-4] player=8,0,0 inRange=3 | ForageSpot#2@10,0,2 d=2 | BuildSlot#21@5,0,1 d=3 | Door#6@5,0,1 d=3 => ForageSpot#2
[PROBE-4] player=15,0,15 inRange=4 | ForageSpot#3@12,0,12 d=3 | BuildSlot#204@12,0,12 d=3 | BuildSlot#238@14,0,14 d=1 | Utensil#9@14,0,14 d=1 => Utensil#9
```

- **`Utensil#9` 在候选集里**,且 `d = 1` 时选中它 ⇒ `_enterProcessing` 被调用;
- 同格还有 `BuildSlot#238`(`d = 1`),但 `KindPriority[Utensil]=2 < [BuildSlot]=5` ⇒ **键 ② 在这里真的起作用了**
  (这是本轮唯一一次键 ② 参与裁决的观测);
- 病人 `#` 一个都不在半径内(两角都离得很远)。

**(丁)模态确实打开(用「E 被挡」作判据)**

| 动作 | `PROBE-4` 行数 |
|---|---|
| 开屏后按 `E` | 4 → **4**(不新增 ⇒ 模态门 F-4.2 拦下) |
| `Escape` 关屏后再按 `E` | 4 → **5**(新增,且选中仍是 `Utensil#9`) |

⇒ 「炮制屏开了」不靠画面判断,而靠**门的行为**判断。

---

## 4. 开出来的屏是**空壳**(如实登记)

`07-processing.png` / `07c-strip.png`:屏是一张**窄纸带**(约 400×40 px),无标题、无候选行、无读数。
与「病人分居两角」无关 —— 更可能的原因:新局没有材料 / 品级集为空 ⇒ 准入表为空。
⇒ **本轮只证明「入口可达」,不证明「内容可用」**:

| 项 | 状态 |
|---|---|
| 灶台可被选中 · 炮制屏打开 · 模态门生效 | **PASS**(§3) |
| 屏内候选行(选料)/ 点火 / 规则八 | **NOT-RUN** |
| `AC-18-21` 端到端人工腿 | **仍 NOT-RUN**(屏内无内容) |
| `AC-42-F1` ⑨ | **仍 NOT-RUN**(`design/ux/processing-18.md` 仍未成文;空壳与该缺口同向) |

---

## 5. 副作用处置

| 项 | 结果 |
|---|---|
| 第一次会话(载入 w2)重写了 `w2/save_auto.bin` / `save_exit.bin`(旋转出 `.bak`) | **由 `saves-before-probe4-1018.tar` 逐字节还原** —— `diff` 结果 `w1/w2 全部逐字节一致` |
| 「新起一局」创建的测试世界 `w3` | **已删除** |
| `worlds.index` | 由备份还原,SHA-256 `61edf427…2887df1` 两侧一致 |
| `[PROBE-4]` 探针 | **已从源码删除**;`grep -c PROBE-4 InteractionPressRouter.cs` = **0**,且 git 显示该文件**未修改**(= 逐字节回到 HEAD) |
| 受影响的自动化夹具 | `PatientAppearedWiringTest` **1/1 PASS**(`unity/Logs/probe4-playmode.xml`) |

> ⚠️ 备份件在 `/home/gu/.claude/jobs/6001633c/tmp/`,会话结束即清 —— 需要长期留档请另存。

---

## 6. 对既有记录的订正(**原文不删**)

1. `walkthrough-2026-11-06-crafting.md` 的「13 次 `E` 零命中 ⇒ 病人优先级压住」:
   **归因作废**。真因 = ① 所有病人与玩家同在 `(0,0,0)`(键 ① 决定);② 玩家走不动是**地面只有 10×10**。
   该件的「方法」一节里「按 tick 取末条 = 当前格」也不可靠 —— 玩家在坠落时 `y` 会变负,而我只看了 `x/z`。
2. `smoke-2026-11-06.md` §7.1 第 3、4 条与 §7.2 的结论一并按本条订正。
3. `TD-building-05` 的灶台格 `(10,0,5)` → **`(14,0,14)`**;并新增一条**地面覆盖**债(§7)。

---

## 7. 新发现(已登记)

**P0 地面只有 10×10,而世界格是 16×16** —— 越过 `x,z ≈ 5` 即无地板,玩家自由落体。
这是「任何空间走查都不可进行」的**结构性原因**,与「结构无视觉」(`TD-building-05`)是两件事:
前者让**移动**不可用,后者让**目标**不可见。已登记 **`TD-building-06`**。

---

## 8. 炮制链全链跑步(2026-11-06 11:42–11:55)—— **链通了**

用户指令:「跑一遍:采集 → 容器选料 → 灶台」。

| # | 动作 | 观测(探针逐字 / 截图) |
|---|---|---|
| 1 | 主菜单「新起一局」 | `[PROBE-4] player=0,0,0 inRange=4 … => BuildSlot#17` |
| 2 | 走到 `(2,0,2)`,`E` | `ForageSpot#0@2,0,2 d=0 … => ForageSpot#0`(起表;采集是持续交互) |
| 3 | **原地不动**等 8 s | 不动 = 不打断(`Advance` 的打断条件是「玩家格变了」) |
| 4 | 走到 `(5,0,5)`,`E` | `Container#1@5,0,5 d=0 … => Container#1`;屏上出现 **「器物#0 品1 叠2」** ⇒ **采集真的进了背包(叠 2)** — `09-container.png` |
| 5 | `Down` → `Return` | 行出现金色边框 — `10f-row-after.png` |
| 6 | 走到 `(14,0,14)` 旁,`E` | `Utensil#9@14,0,14 d=1 … => Utensil#9` |
| 7 | 截屏 | 炮制屏 **两行**:`dry_willow_bark` / `extract_salicylic` — `11-processing-with-material.png` |

**⇒ 上一轮的「空壳」原因被坐实**:零行 = 没有选料(`NoConsumedInstances`),不是屏坏。
选料之后同一条链在同一台机器上出两行。

**为什么只有 2 行(不是 4)**:配方表 4 条里 `craft_wooden_splint`(owner=`craft`)与
`assemble_wooden_post`(owner=`build`)不属炮制;灶台屏只列 `process` 两类。
**行文案 = `recipe_id` 键名**(`dry_willow_bark`),不是「麻黄汤」式显示名 —— 已知粗糙边(21a 显示名轮)。

### 8.1 本轮新抓到的缺陷:容器屏开屏**无初始焦点**(已立 `TD-skeuo-20`)

- **实测**:开屏后**第一次 `Return` 无任何视觉变化**(裁切放大 `10c-row.png`:行无金色边框);
  **先 `Down` 再 `Return`** 才出现边框(`10f-row-after.png`),随后灶台屏才出现两行。
- **机理**:`InventoryContainerScreen` 既无 `FirstFocusable`,也无 `Focus()` 调用
  (全仓 `FirstFocusable` 只出现在 `MainMenuScreen` / `PauseScreen` / `CasebookScreen` / `SaveSlotsScreen`)。
- **后果**:键盘 / 手柄玩家会以为屏坏了;而且它与「零行」叠加时,「空壳」和「没操作对」不可区分。

### 8.2 副作用处置(同 §5 口径)

| 项 | 结果 |
|---|---|
| 走查前备份 | `saves-before-chain-1142.tar` |
| `w1` / `w2` | `diff` ⇒ **一致**(本轮全程走 `w3`) |
| 测试世界 `w3` | **已删除**;`worlds.index` 还原,SHA-256 `61edf427…2887df1` 两侧一致 |
| 探针 | **已从源码删除**;`grep -c PROBE-4 InteractionPressRouter.cs` = 0,该文件 git 显示未修改 |
