# S22-8 · smoke `#13` 勘察 + 两案(帐篷承接者)

| 字段 | 值 |
|---|---|
| **卡** | sprint-22 `S22-8` |
| **对象** | `tests/smoke/critical_paths.md` #13「建造:模块对齐整数格;**帐篷(P0 唯一新建例外)可放置**且不改世界流骨架」 |
| **执行轮** | 2026-11-01 · 用户指令「做 S22-8」 |
| **交付形态** | **只勘察 + 出两案,不实现**(卡面硬约束) |

---

## 1. 裁定原文(不是「没人裁」,是「裁了没人接」)

`design/gdd/game-concept.md:697-698`(M0 第 4 条):

> 「一间固定医馆 + 小改造 —— 允许更换家具/设施位置…**不允许新建**
> *(⚠️ 2026-09-20 用户裁定改判:**P0 允许「帐篷」这一次新建** = 剧情开场…裁定原话要义:
> **P0 只是测试切片,也必须能新建** —— 帐篷即该能力在 P0 的最小证明…)*」

**裁定是完整的**,而且落到了 GDD 正文:`design/gdd/modular-building.md:246-257` 规则六已写死实现形态:

> 「P0 唯一例外 = 帐篷:序章的一次新建,**落关卡工具在 `world_buildslots.json` 预置的 `tent` 槽**
> (骨架外预置槽,**不破「不改骨架」**);`StructurePlaced` **照常发,载荷形状不变**;第二/三间棚子仍归 P1a。」

⇒ 缺的不是裁决,是**承接者**:没有一张 story 认领「把 `tent` 槽与 `tent` 模块做出来」。

---

## 2. 现状实测(逐条可跑)

```bash
grep -rniE "\btent\b" unity/Assets --include=*.cs | wc -l                 # 0
grep -rn "帐篷" unity/Assets --include=*.cs | wc -l                        # 0
grep -c "tent" assets/data/world_buildslots.json assets/data/build_modules.json   # 0 / 0
sed -n '17,26p' unity/Assets/Sim/World/BuildSlotCatalog.cs                 # SlotType 闭集 6 员,无 Tent
```

| 面 | 现状 |
|---|---|
| 代码 | **零** `tent`。唯一相关类型 `SlotType` 是**闭集六员**(`None/Bed/Table/Cabinet/Plot/Decor/Shell`) |
| 关卡数据 | `world_buildslots.json` **13 个槽全是医馆内**(Shell ×4 / Decor 等),**无 `tent` 槽** |
| 模块目录 | `build_modules.json` **9 个模块,无帐篷**;`module_type` 与 `SlotType` 同一闭集 |
| 23 的卡面 | `story-001…007` 全是**医馆改造 + 骨架/载荷/拆除**面;**无一张**认领帐篷 |
| 放置引擎 | `Placeable` 五条件(`story-002`)与写权(`story-003`)已在,**放置机制本身不缺** |

⇒ 帐篷不是「机制没做」,是「**数据 + 一个类型位**没做」。放置五条件、`StructurePlaced` 写权、
叠加重建(`story-004`)都已就绪 —— 帐篷只是它们的一个新输入。

---

## 3. 两案

### 甲案(推荐)—— 补一条 content story(`modular-building/story-008-tent`)

| 项 | 内容 |
|---|---|
| **动了什么** | ① `BuildSlotCatalog.SlotType` 加 `Tent = 7`(闭集扩员,**同批**改 `TypeOK` 的允许对与目录校验);② `build_modules.json` 加一条 `module_type: Tent`(占用 1 格、`cost` 归数值轮);③ `world_buildslots.json` 由**关卡工具**产出新条目:`tent` 槽,位于**骨架外**(医馆外围空地),`baked_initial = false`;④ 一条 EditMode 夹具:载入目录 → `Placeable(tent 槽, tent 模块)` 为真 → 放置 ⇒ **恰一条 `StructurePlaced`**,且**骨架三 Kind(`StructurePlaced/Removed/Modified`)之外零新事件**;⑤ 一条反例:非 `tent` 槽放 `tent` 模块 ⇒ 拒(五条件的 ① 支)。 |
| **判据怎么判** | smoke `#13` 一分为二:**「模块对齐整数格」已有载体**(`BuildSlotCatalog` 用 `WorldPos` 整数格);「帐篷可放置」挂新夹具 ⇒ `#13` 转 `PASS(2 载/全绿)`,`无名 5 → 4`。 |
| **与裁定/规则的关系** | **完全兑现**,零改判:规则六已写明「预置 `tent` 槽 + `StructurePlaced` 载荷不变」,本案只是把它做出来。 |
| **风险** | 中低。唯一实质风险是 `SlotType` 扩员会牵动 `TypeOK` 与目录校验的对齐 —— 须同批加「`Tent` 槽只接受 `Tent` 模块」的判据,否则扩员等于开了一个任意对。 |
| **代价** | 一张 `S`~`M` 卡(数据 + 枚举 + 2 条夹具)。 |

### 乙案 —— 登记为已知缺口(P0 不做帐篷)

| 项 | 内容 |
|---|---|
| **动了什么** | 不改代码。改的是**账**与**裁定**:① smoke `#13` 拆成「整数格对齐(有载体)」+「帐篷(marked NOT-RUN)」;② `game-concept.md:697` 的裁定注加**改判记录**(P0 不做新建,整条推 P1a);③ 23 的 EPIC 补一条「已知缺口」行。 |
| **判据怎么判** | `#13` 只能记 `PARTIAL`(一半有载体、一半 NOT-RUN)。⚠️ 按本项目纪律,**这半不得读成通过** —— 等于 `#13` 永远留在 INCOMPLETE 面里。 |
| **与裁定/规则的关系** | **正面推翻 2026-09-20 用户裁定**。裁定明写「P0 只是测试切片,**也必须能新建** —— 帐篷即该能力在 P0 的最小证明」⇒ 乙案拿掉的正是这条裁定的**宾语**。而且它同时抽掉 23 的「方向承重前提」(《英灵神殿》式世界建设从 P0 起就有锚点)。 |
| **风险** | 高,且是**裁定级**风险:改判须用户重新裁;`modular-building.md` 规则六的整段实现口径随之失效。 |
| **代价** | 零代码,但零收益:缺口不会消失,只是被记成「已知」。 |

---

## 4. 本轮不做的部分(按卡面)

- **不实现**任何一案的落地:本轮**未改** `SlotType`、`build_modules.json`、`world_buildslots.json`,
  也**未改** `critical_paths.json` 的 `#13`。
- 甲案若被采纳,落地 = 一张新 story + 数据 + 两条夹具;乙案若被采纳,落地 = 一次改判 + 账本拆分。

---

## 5. 建议

**建议取甲案。** 理由不是「裁定写了就得做」,而是 §2 的实测表明**成本已经很低**:
放置五条件、写权、叠加重建三件都在,帐篷只是它们的第 10 个输入 + 一个类型位;
而乙案要推翻的是一条**专门为「P0 也能新建」做的**裁定,并且会让 23 的方向锚点消失。

---

## 6. 复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
sed -n '697,698p' design/gdd/game-concept.md          # 裁定原文
sed -n '246,257p' design/gdd/modular-building.md       # 规则六的实现形态
grep -rniE "\btent\b" unity/Assets --include=*.cs | wc -l   # 0(代码零实现)
grep -c "tent" assets/data/world_buildslots.json assets/data/build_modules.json  # 0 / 0
sed -n '17,26p' unity/Assets/Sim/World/BuildSlotCatalog.cs   # 闭集六员,无 Tent
ls production/epics/modular-building/                   # story-001…007,无帐篷承接者
```
