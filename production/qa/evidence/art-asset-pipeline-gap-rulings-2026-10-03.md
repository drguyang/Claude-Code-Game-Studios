# 资产断链接口 · 裁定清单

> **Date**: 2026-10-03
> **性质**: 待裁清单,**不是裁定**。落盘后待用户逐条批复。
> **执笔**: 承三份审计(设计层审计代理 + 流程层审计代理 + 本轮实测)
> **一句话**: 设计层只写机制、资产层只写画什么,中间「**谁在何时把哪件资产做出来接进哪段机制**」
> 三段皆空。
> ⚠️ **本件不主张任何 ALREADY-DECIDED** —— 五条待裁项全部 open,建议列仅供参考。

---

## 断链总图(实测)

| 层 | 登记处 | 数量 | 与下一层的引用边 |
|---|---|---|---|
| 登记 | `design/gdd/*.md` | 37 份,**35 份无资产清单** | → assets/ **= 0** |
| 登记 | `design/assets/entity-inventory.md` | 42 VS + **62** FP = **104** | 被 `production/` 引用 **2 处**(均 2026-10-03 本轮所写) |
| 登记 | `design/assets/specs/*.md` | 19 份,**14 份 Source 引 `hud.md`** | 被 story 引用 **= 0** |
| 生产 | `production/epics/` | 31 epic,**0 个做资产生产** | — |
| 接入 | `unity/Assets/**/Textures/` | 16 PNG,**GUID 在全库引用 = 0** | 承接件仅 `skeuomorphic-ui/story-019`(2026-10-03 立) |

**关键实测(逐条可复算)**:

1. `grep -rn "design/assets\|assets/specs\|entity-inventory" design/gdd/*.md` → **0 命中**
2. `design/gdd/` 中 42 项 VS Critical 的**逐项 literal 名**,有 **7 项零命中**:
   诊脉台 / 手术灯 / 针具 / 呼吸波形 / 舌象色卡 / 体征异常脉冲 / 墨迹落笔
3. `grep -rn "specs/" production/epics/*/story-*.md` → **0 命中**
4. `ls -d production/epics/*art* production/epics/*asset*` → **0 命中**
5. `design/art/art-bible.md` 最高章 = **§9**(:1181),**无 §10 / §11**
6. `design/assets/specs/` 实际 **19 份**;`Tier: Vertical Slice Critical` = 14,`Full Production` = 5
7. `assets/audio/` **零文件**,但 `assets/data/audio_events.json` 引用了具体 `.wav`(如
   `vo_patient_cough_damp_m1.wav`)—— **被引 .wav 均不存在**

> ### ⚠️ 制度上早已要求的接口 —— 却从未执行
>
> `design/art/art-bible.md` §8.11.2 **D1 · 入库** 原文:
> > 「任何灰盒资产进入 `unity/Assets/` ⇒ 须在**该系统 epic 的 story 件内显式登记**(见 8.11.4 表格),
> > 不留『看起来像忘了做』的空白」
>
> ⇒ **制度早已规定「资产挂在消费它的系统 epic 的 story 里」**,但实测 **story 内零资产登记**。
> 本件 R-2 的建议即「落实该条」,**不是新裁**。

---

## 待裁项

### R-1 · 谁拥有「资产需求」这个位置?

> ### ✅ **已落地(2026-10-03)—— 取建议乙**
>
> `art-bible §8.11.6` 已写入引用边格式;**31/31 系统 GDD** 的 `## Visual/Audio Requirements`
> 节尾各加一条引用边:21 清单型 · 1 转引型(`item-database` 定义方)· 9 无资产声明。
> **不新立第二真源** —— `entity-inventory.md` 仍是唯一登记处。


**实测**:`design/gdd/` 里 35/37 无资产清单;唯 1 份有(`combat-and-weapon-lines.md:1806` §8.2
「资产规格(数量级 —— 供 producer 排期)」),唯 1 份**显式弃权**(`world-and-ecozones.md:1029`
「角色 / 建筑的具体美术资产清单 → 关卡与美术的交付物,6 只提供逻辑层骨架」)。

| 选项 | 含义 | 代价 |
|---|---|---|
| **甲** | 系统 GDD 增设 `§Visual/Audio Requirements` **强制节**(29 份已有此节,但非强制) | 35 份 GDD 须回填;`coding-standards.md` 的 8 必需节 → 9 节 |
| **乙** | 维持现状,资产需求**唯一登记处** = `entity-inventory.md`,但**补 GDD → inventory 的引用边** | 引用边要写 37 处;「需求在别处」仍反直觉 |
| **丙** | 不裁归属,只要求每个 GDD 写一行「本系统资产见 `entity-inventory` #N」 | 最轻;但仍未回答「谁**定义**需求」 |

**建议:乙**。理由:`entity-inventory.md` 已存在且严谨(104 项、分类清楚),另起一套会造第二真源。
补引用边是**把已有的两座岛连起来**,不是新建。丙是乙的弱化版,若嫌 37 处引用太重可取丙。

---

### R-2 · M4 的「42 项全交付」由谁的 story 承接?

**实测**:31 个 epic 里做资产生产的 = **0**;`entity-inventory` 在 `production/` 仅被引用 2 处(均本轮)。

| 选项 | 含义 | 代价 |
|---|---|---|
| **甲** | 新立 epic `art-production` | 与「不加系统 / 不加 #54」取向冲突;需新 ADR |
| **乙** | 42 项**逐项挂到消费它的系统 epic** 的 story 里(承 §8.11.2 D1 原意) | 42 项要分清归属;部分项(VFX / SFX)跨系统 |
| **丙** | M4 只管里程碑判据,不建 story 承接(= **现状**) | 无法回答「谁在做」—— 即当前困境 |

**建议:乙**。理由:§8.11.2 D1 **本来就要求**这样做,只是从未执行。它**不需要新 epic、不需要新 ADR**,
是**把已写的制度落实**。甲案的代价被高估过一次(见 `ADR-022` 立 Tooling 层的先例 —— 那一次确实
新立了,但理由是「引用却无登记」;**本件的情况不同:`登记处已存在,只是没连边`**)。

> ### ✅ **已裁(2026-10-03)—— 用户裁定:按类型分别归属**
>
> **不取甲乙丙任一单选项**,改**按资产类型分别归属**:
> - 角色 / 环境 / 道具 / 物品 / 界面 / HUD = 挂**消费它的系统 epic**
> - **SFX / Ambient** = 集中挂 `audio-system`(**急救专用音例外** → `emergency-procedures`)
> - **VFX** = 挂**触发系统**
>
> 42 项全部分完(**11 个 epic**,无遗漏无重叠),逐项归属见
> `production/qa/evidence/r2-asset-ownership-ruling-2026-10-03.md`。
> ⚠️ **本件只定归属,不定排期** —— 承接 story 的编号与排期归 `producer`(`art-bible §8.11.3`)。
> ⚠️ **甲案(新立 `art-production` epic)仍未被采纳** —— 归属拆到 11 个既有 epic,不新增 epic。

---

### R-3 · 19 份 spec 谁认领?

> ### ✅ **已落地(2026-10-03)—— 取建议甲**
>
> **19/19 spec 全部具名认领**(写进对应 GDD 的引用边)。
> ⚠️ **原判「4 份无系统 GDD」已订正 → 0 份**:补引用边后,
> `ink-splash`→`skeuomorphic-ui` · `main-menu`→`save-slot-ui` ·
> `protagonist`→`player-controller` · `vitals-pulse`→`patient-ai` 四份各有归属。
> 「无主」是**引用边缺失**造成的观测假象,不是真缺口。


**实测**:引用 `specs/` 的 story = **0**;19 份中 14 份 Source 引 `hud.md`(UX 层);
剥掉 UX / art-bible / concept / ADR 后**无任何系统 GDD** 的有 4 份
(`ink-splash` / `main-menu` / `protagonist` / `vitals-pulse`)。

> ⚠️ **本轮两处实测口径不一致,登记不隐藏**:流程层审计判为 **7 份**,本轮实测判为 **4 份**。
> 根因 = **「`game-concept.md` / `art-bible.md` 算不算系统设计文档」这一边界本身模糊**
> —— 而这模糊**正是问题所在**,不是谁数错。**采用 4 份口径**(只认 `design/gdd/` 下系统件);
> 若取宽口径(容 meta 文档)则为 7 份。

| 选项 | 含义 |
|---|---|
| **甲** | 按 R-2 乙案,spec 随其**消费系统**挂进 story |
| **乙** | spec 视为美术内部交付物,不进 story 体系 |
| **丙** | 暂不裁,等 M4 启动时再说 |

**建议:甲**,与 R-2 同批。**若 R-2 取乙,R-3 自动成立** —— 不必单裁。

---

### R-4 · 那 7 项 GDD 零命中的资产怎么办?

> ### ✅ **已由 R-1 顺带闭合(2026-10-03)**
>
> 7 项**全部命中**,且落在语义正确的系统:诊脉台/呼吸波形/舌象色卡 → `diagnosis-system`;
> 手术灯 → `emergency-procedures`;针具 → `emergency-procedures`;体征异常脉冲 → `patient-ai`;
> 墨迹落笔 → `casebook`。**引用边是直接成因** —— 无需逐项回填。
> ⚠️ ~~**残留(另议,不在本件面内)**:针具是否该留在 VS Critical(针灸属 MVP 明确不做的内容)。~~
> ✅ **已裁(2026-10-03)—— 针具留在 VS Critical**。
> 用户裁定:针具**保留**在 42 项内。⇒ 原「针灸属 MVP 不做内容、故其存在存疑」的疑虑**不成立**
> —— 针具在 VS 面内**有其位置**,归属 = `emergency-procedures`(R-2 已定)。
> ⚠️ 本裁**只结针具的去留**,不改 `game-concept.md:719` 的 MVP 排除清单(中药与针灸仍在排除项内)
> —— 即「MVP 不做针灸**系统**,但针具**作为器械资产**在 VS Critical 内」,两者不矛盾。
> 若后续确需重开,MVP 排除清单是**入口**(须用户裁定),不在资产面自行改判。


**实测**(逐个复算,7 项在 `design/gdd/` 全部 = 0 命中):

| # | 资产 | 是否有 spec | 备注 |
|---|---|---|---|
| 1 | 诊脉台 | ✅ `pulse-dial.md` | `diagnosis-system.md` 有「听诊器」6 处、「听诊 / 触诊」多处,但**从无「诊脉台」字样** |
| 2 | 手术灯 | ✅ `surgical-lamp.md` | `emergency-procedures.md` **零命中** |
| 3 | 针具(针灸) | ✗ | `emergency-procedures.md` **零命中**(该 GDD 只写「止血包扎」「CPR」) |
| 4 | 呼吸波形(纸带) | ✅ `breath-waveform.md` | 仅 `hud.md` / `entity-inventory` |
| 5 | 舌象色卡 | ✅ `tongue-color-card.md` | 「舌象」命中 `hud.md`,但非 GDD |
| 6 | 体征异常脉冲 | ✅ `vitals-pulse.md` | Source 仅 `hud.md §Dynamic Behaviors` |
| 7 | 墨迹落笔 | ✅ `ink-splash.md` | 仅 `art-bible` / `entity-inventory` |

**这条不是归属问题,是缺口**:4 项明明有 spec,却没有任何系统 GDD 提到它们。

| 选项 | 含义 |
|---|---|
| **甲** | 逐项回填到对应 GDD(诊脉台 → `diagnosis-system.md`;针具 → **需先确认归谁**) |
| **乙** | 接受「资产存在但机制文档不提」,即承认 GDD 不必穷举资产 |
| **丙** | 只补**有 spec 的 5 项**,其余 2 项(针具 / 呼吸波形)另议 |

**建议:丙 → 甲 分批**。理由:**有 spec 说明它已被认真设计过**,只是没回填 GDD,属**纯勘误**;
余项要先确认归谁 —— ~~**针具尤其存疑**~~(emergency 从无针具 / 针灸;针灸属 MVP 明确不做的中医内容,
见 `game-concept.md:719`,故它在 VS Critical 里的存在**本身可能需要重新审视**)。
> ✅ **该存疑已结(2026-10-03 用户裁定)**:针具**留在 VS Critical**,归属 `emergency-procedures`。
> 「MVP 不做针灸**系统**」与「针具**作为器械资产**在 VS 内」两者不矛盾 —— 详见上文 R-4 落盘段。

---

### R-5 · story-019 之外,还有别的「已建未接」吗?

> ### ✅ **已落地(2026-10-03)—— 取建议甲**
>
> 新立 `production/epics/audio-system/story-015-audio-asset-binding.md`(Ready ⬜),
> AC 含「删任一被引 `.wav` ⇒ **构建期硬失败**」(真素材复验,非夹具)。
> `audio-system/EPIC.md` + `epics/index.md` 已同步 §范围边界声明。
> ⚠️ **实测收窄**:story-010 的 AC-44-D6 **已要求**过「素材缺失⇒门拒绝」,
> 其 11 测**用的是夹具** —— 缺口是「真素材未验」,不是「门未建」。


**实测**:`assets/audio/` **零文件**,但 `assets/data/audio_events.json` 引用了具体 `.wav`
—— **被引 .wav 均不存在**。即音频侧存在**同型断链**(引用存在、资产不存在)。

| 选项 | 含义 |
|---|---|
| **甲** | 同 `story-019` 模式,立音频接入 story(含「引用不存在的 `.wav` ⇒ 构建期硬失败」) |
| **乙** | 归 44 的 GDD 轮处理 |
| **丙** | 登记不动 |

**建议:甲**,且 **AC 里必须含「构建期硬失败」** —— 因为贴图那边的教训正是:
**静默缺资产让「未接入」不可见**(16 张图 GUID 引用 = 0 却无任何告警)。

---

## 不需要裁的(纯事实登记,无需批复)

- 19 份 spec 的 Source 锚点以 `hud.md`(UX 层)为主而非系统 GDD —— 这是**事实**,不是错误。
- `art-bible` 最高 §9,**无 §10 / §11** —— **不是缺章**(§9 禁令与对标为设计收尾)。
- `entity-inventory.md` 的 42 + 62 = **104** —— 与本轮各层计数一致。
  ⚠️ **2026-10-03 收口订正**:原文记「61 / 103」,系 `entity-inventory.md` 的**三次订正未落到表体**
  (划删行仍被计入、HUD 侧该减未减)。实测复算 = **FP 62 / Total 104**;表体与各引用处已同步。

---

## 与既有裁定的关系

- **不重开** `production/milestones/README.md` §六 的 3 条开放问题
  (M2 最小真资产 / M1 两项是否硬前置 / 三处口径合流)—— 那是**里程碑面**,本件是**接口面**。
- **承** `art-bible §8.11.2 D1` —— 本件 R-2 建议即「**落实该条**」,非新裁。
- **不改**任何 ADR —— 全部待裁项均为**流程 / 归属**,不触架构。
- **不新增系统** —— 五条建议项**均不立新系统 / 新 epic**(R-2 甲案是唯一例外,且**未推荐**)。

---

## 批复格式建议

逐条回 `R-1=乙` / `R-2=乙` 这种即可;或对某条给自定义口径。
未批条目**维持现状**,本件不自行推进。
