# Asset Spec: 敌人(P0 · 兵痞 / 野狗)

> **Tier**: Vertical Slice Critical
> **Category**: Character / Enemy(2026-09-29 用户裁定:敌人独立成类)
> **Source**: `enemy-ai.md`(27)· `art-bible.md` §3.1 / §5.1 / §7.6 / §10 · `prologue-beat-script.md`(PA-04 / PA-05)
> **Art Bible Ref**: §3.1「敌人 —— 人 / 兽」形状立法(AB-9 已裁)· §5.1 墨铜分轨 · §7.6 敌人读数条 · §10 性能预算
> **ADR Ref**: ADR-013 §9 C3(呈现层只渲染)· ADR-016 §二(表现态不可反推逻辑)· ADR-018(`AudioCueDto` 同构)
> **Status**: **占位资产已落**(primitive 组合体,2026-11-14「完整可见」轮)· 真资产 = **未开**

---

## Visual Description

P0 两个原型,分属 art-bible §3.1 的**两套立法**(AB-9 裁定:兽不受人形禁令约束)。

### 兵痞(人形敌人 · `Thug`)

- **剪影**:前倾楔形 + **锐角衣物破度**,但与病人共用「**人形底盘**」——
  可读成「**同一种人的两种处境**」(`art-bible.md:183`)。
- **威胁感来源 = 失序**(抽动节奏 / 窃步 / 褴褛),**不来自**巨物化或怪物化。
  **禁**把「人」设计成可憎(支柱二「医者不杀」的形状立法)。
- **铜侧**:缴获的劣质器械 —— **锈多、刻度不清、缺件**;
  铜**只出现在「他们带了什么来」处**,身体本身仍是墨侧(`art-bible.md:384` / `:417`)。
- **面孔**:无个性面孔 —— 敌人的脸**不被设计成可被记住的**(`:439`)。
- **材质归属**:墨(褴褛衣料)+ 铜(零星器械);皮肤 = PBR non-metal + high roughness。
- **P0 占位形态**(已落):粗布色躯干 + 肤色头 + 深色靴,无武器挂件;
  武器/器械挂点归真资产轮。

### 野狗(野兽 · `WildDog`)

- **剪影**:**不套用**人形底盘与「失序」立法(AB-9)——
  可读 = **动物性的饥饿与领地**;剪影**允许被读成威胁**,与匪徒不同判(`:184`)。
- **材质归属**:墨(皮毛 / 肌肉)+ 铜(牙 / 爪 / 甲);**无拟人表情**(`:441`)。
- **巨物化**:门对兽**不设禁**(AB-9)—— 但 P0 的野狗是**普通体型**,不是巨兽;
  巨化形态保留给危难本身(支柱四建筑尺度形)。
- **P0 占位形态**(已落):毛色胶囊躯干 + 球头 + 尾 + 四腿,无獠牙/甲片挂件;
  细节归真资产轮。

> **两原型的共同硬约束**:归零 = **「不动了」不是「死了」** ——
> **禁**死亡动画 / 尸体淡出 / 实体销毁 / 血液飞溅的量级升级(`enemy-ai.md:1269-1273`);
> **必须**倒地姿态 + **呼吸层仍在**。野狗倒地后额外显示**可取材墨色轮廓微光**
> (2026-11-14 裁定,见下 §Interaction States)。

---

## Technical Specs

| Property | 兵痞(人形) | 野狗(兽) | Notes |
|----------|------------|----------|-------|
| **Poly count** | 25,000 tris(LOD0)/ 15,000–25,000(LOD1) | 50,000 tris(LOD0)/ 30,000–50,000(LOD1) | `art-bible.md:966-967` |
| **Texture set** | 3(albedo + normal + ORM) | 4(albedo + normal + ORM + emissive) | 人形 emissive 走 albedo 编码;兽的 emissive = 眼睛 / 甲壳(`:1003-1004`) |
| **Material slots** | 4(皮肤 / 衣料 / 铜侧器械 / 特效覆盖) | 4(皮毛 / 肌肉 / 铜侧牙爪 / 特效覆盖) | `art-bible.md:791` |
| **Rig** | Humanoid(与病人共用底盘) | Quadruped | — |
| **Animations** | idle / walk / run / alert / attack / down / breath | idle / walk / run / alert / bite / down / breath | 六态可读的最小集;**呼吸层在倒地态仍须播** |
| **LOD** | LOD0(近景)/ LOD1(中景) | 同 | 开放世界摄像机距离 |
| **Collider** | **无**(敌人视觉不吃物理) | **无** | 与 `CapsulePatientVisualFactory` 同纪律 |
| **Platform variants** | 无 | 无 | 全平台同一套 |

### P0 占位资产(现状,已落)

| 项 | 值 |
|---|---|
| 形态 | primitive 组合体(`ComposedEnemyVisualFactory`),人形 / 兽两套 |
| 材质 | 共享 `Resources/PatientPlaceholderMaterial.mat`(中性灰 + `MaterialPropertyBlock._BaseColor` 逐件覆盖) |
| 轮廓 | 同 shader 的**运行时材质**,`_Cull = Front`(倒置外壳)⇒ 只露背面 = 墨色轮廓 |
| 颜色 | 衣料 `#5C574D` · 皮肤 `#9E8066` · 靴 `#333029` · 毛 `#4F4538` · 墨 `#292623`(零语义,只区分部件) |
| 兑换点 | 真资产到来时在 `CompositionRoot` 换掉 `new ComposedEnemyVisualFactory()` **一行**;spawner / 位姿桥 / 读数条 / 夹具一字不动 |

> ⚠️ **TD-build-03 纪律**:材质必须经**资产引用**进包 —— **禁** `Shader.Find` 现找(否则 player 构建剥离)。
> 占位材质缺失 ⇒ **构造期抛**(不留品红、不静默降级);缺 `_Cull` 属性同样构造期抛。

---

## Asset Breakdown

| Asset | Type | Description | Priority |
|-------|------|-------------|----------|
| `enemy_thug_body.png` | Texture | 兵痞 albedo(褴褛衣料 + 皮肤),2048² | P0(真资产) |
| `enemy_thug_normals.png` | Texture | 法线(衣料破度 / 褶皱) | P0(真资产) |
| `enemy_thug_orm.png` | Texture | ORM(衣料粗糙度主导) | P0(真资产) |
| `enemy_thug_rig.fbx` | Mesh + Rig | 人形骨架(与病人共用底盘) | P0(真资产) |
| `enemy_dog_body.png` | Texture | 野狗 albedo(皮毛 + 肌肉) | P0(真资产) |
| `enemy_dog_normals.png` | Texture | 法线(皮毛束 / 肌理) | P0(真资产) |
| `enemy_dog_orm.png` | Texture | ORM(毛 / 牙爪分轨) | P0(真资产) |
| `enemy_dog_emissive.png` | Texture | 自发光(眼睛 / 甲壳) | P0(真资产) |
| `enemy_dog_rig.fbx` | Mesh + Rig | 四足骨架 | P0(真资产) |
| `enemy_down.anim` | Animation | 倒地(两套:人形侧卧 / 野兽侧翻) | P0 |
| `enemy_breath.anim` | Animation | 呼吸起伏(仅驱动姿态层;倒地仍播) | P0 |
| `enemy_salvage_ink.mat` | Material | 可取材墨色轮廓(倒置外壳,运行期可建) | P0 |

> **SFX 不在本卡**:敌人受伤 / 昏迷音归 44(`entity-inventory.md` SFX #7),按 `AudioCueDto` 走。

---

## Material / Dual-Track(墨铜分轨)

| 原型 | 墨侧(主体) | 铜侧(器械 / 武器) | 读法 |
|---|---|---|---|
| 兵痞 | 褴褛衣料 | 缴获的劣质器械(锈多、刻度不清、缺件) | 「同一种人的两种处境」;铜只在「他们带了什么来」处 |
| 野狗 | 皮毛 / 肌肉 | 牙 / 爪 / 甲 | 动物性的饥饿与领地;**无拟人表情** |

> **禁敌我色区分**:「我们 / 他们」的颜色编码本身违支柱二(`art-bible.md:361`)。
> 敌我可辨靠**形状 + 体态 + 运动节奏**,不靠颜色。

---

## Interaction States(六态 + 倒地)

> 态归 27(六态机,`enemy-ai.md` §States 一);本表只写**呈现层必须让玩家读到的**。
> 判据:态**不得只靠颜色**传达 —— 每态**同时**有姿态 / 动作差异。

| 态 | 玩家读到的 | 呈现形态(全身) | 读数条(42) |
|---|---|---|---|
| `Patrol` | 在走它的路 | 沿导航格巡游,不朝你 | 不显示(未接战) |
| `Alert` | 停下了,转头了 | 面向你,不动 | 浮出(命中 / 瞄准时) |
| `Chase` | 直线朝你来 | 直线追击,**格间插值** | 显示 |
| `Flank` | 不直线来,绕了 | 侧向路径 | 显示 |
| `Engage` | 贴身了,出手 | 出手动作(`bite` / `attack`) | 显示 |
| `Disengage` | 退了,背对你走开 | 背向走开 | 显示,随后淡出 |
| `Down`(非态) | **不动了** | 躺倒 + **呼吸层仍在**;野狗额外**墨色轮廓微光** | 压暗但仍可见(归零 ≠ 已清除) |

**可取材微光(2026-11-14 裁定,`enemy-ai.md:1286-1294`)**

- **触发** = `is_down ∧ down_class = Salvageable` —— P0 只有**野狗**;兵痞(`Healable`)只躺倒 + 呼吸。
- **形态** = 倒地野兽外围一圈极淡的**墨色**轮廓,低频**呼吸**(与身体呼吸同频,2.2 Hz)。
- **禁**:颜色单独承载信息 · 数字角标 · 进度填充 · 悬停交互提示。
- **不改变**「归零 ≠ 死亡」的其余硬约束(死亡动画 / 尸体淡出 / 实体销毁 / 血液升级仍然禁)。

> **2026-11-14 追加裁定**:人形敌人也可「掉落拾取」,但口径 = **独立的掉落物系统**
> (物品实例 + 掉落表 + 拾取交互),**不是**本微光;登记 `OQ-27-8` /
> `production/registration-debt-register.md` §F(`27-掉落-1`),本轮不实现。

---

## Readout Bar(V-10 · 黄铜侧,归 42)

- 形态 = **錾刻弧 + 指针**(机械表式),**禁止填充条**(`art-bible.md:582`);
  P0 实现 = **九格离散蚀刻槽**(`EnemyVitalsBillboard.TickSlots = 9`)。
- 材质 = 黄铜底 + 铜锈渐变(使用痕迹)+ 蚀刻刻度线;哑光面片。
- 挂载 = 敌人身上**世界空间** UGUI Canvas,随敌 transform 运动;**恒定屏幕尺寸**。
- 档位文案(27 的伤情词汇,42 只映射不比较阈值):
  `轻伤` / `中伤` / `重伤` / `危殆` / `昏迷` / `复苏` / `旧伤` / `殁` / `无恙`。
- 出现条件:命中或瞄准时出现,脱离战斗数秒后淡出(`:584`)。

---

## Accessibility

- **状态不靠颜色单通道**:六态每态有姿态 / 动作差异(上表),色盲玩家可读。
- **「可脱离」不依赖视觉提示**:玩家不需要「看到脱战按钮」,他只需要走开(`enemy-ai.md:1362-1363`)。
- **可取材微光**是**附加**信息,不是唯一通道:倒地姿态 + 呼吸层本身已表达「不动了」;
  微光只是「可取材」的提示,缺失不影响「它倒了」的判读。
- ⚠️ 无障碍需求评审尚未成文(`OQ-27-6`,与 13 的 `OQ-13-4` 同一缺口)。

---

## AI Generation Prompt (for reference)

```
Two enemy types for a late Qing / early Republican era (circa 1910s) Chinese medical RPG.
Style: ink-wash silhouette language, dual track of ink (body) and brass (equipment).

1. Thug (humanoid enemy): forward-leaning wedge silhouette, sharp angular clothing tears, but
   shares the same "human chassis" as patients — readable as "the same person in a different plight".
   Threat comes from disorder (twitchy rhythm, furtive steps, ragged clothing), NOT from
   gigantification or monstrosity. No personalized face — the enemy's face is not designed to be
   remembered. Brass side: looted, shoddy equipment (heavy rust, unclear markings, missing parts).
   Ink: ragged cloth. Skin: high roughness, non-metal.

2. Wild dog (beast): NOT built on the human chassis; readable as animal hunger and territory.
   Ink: fur and muscle. Brass: teeth, claws, shell. No anthropomorphic expression.
   Normal size (no gigantification in P0).

Constraints: NO enemy-vs-player color coding. NO fantasy elements. NO firearms for humanoids.
Death is never shown: downed = lying prone with breathing, not a corpse.
Resolution: 2048x2048 albedo/normal/ORM (beast also emissive for eyes/shell).
Lighting: warm ambient, soft shadows, three-quarter view.
```

### Generation Record(§8.10.2 · 独创性留痕)

| 字段 | 值 |
|---|---|
| `prompt` | 见上方代码块(prompt 本体) |
| `model` | `TBD` — 待补生成时的模型与版本 |
| `iterations` | `TBD` — 待补迭代轮次与每轮改动要点 |
| `seed` | `TBD` — 待补随机种子(可复现) |
| `human_edits` | `none` — 若有后期人工修改须逐项记录 |

> **§8.10.2 落地**:本表是发行时 Steam AI 申报清单的数据源。
> `TBD` 字段须在该资产**首次生成时回填**;参考图本身为本地产物不入库,
> 规格真源 = 本 spec。**P0 占位为程序化 primitive 组合体,不涉 AI 生成。**

---

## Open Questions

- [ ] **`OQ-ENEMY-ASSET-1`** 真资产是否与病人共用同一套 Humanoid 骨架?
      共用 = 动画可复用(成本低),但敌人姿态(前倾楔形 / 窃步)需额外动画层;
      独立 = 自由但动画全量重做。**未裁** —— 归真资产轮。
- [ ] **`OQ-ENEMY-ASSET-2`** 野狗的「巨物化」在 P0 是否需要变体?
      AB-9 只裁了「门不设禁」,没有要求 P0 出巨兽。**现口径:P0 只有普通体型。**
- [ ] **`OQ-ENEMY-ASSET-3`** 兵痞的武器是否为独立可换挂件?
      现占位无挂件;若 25 的武器线进 P0 则需要挂点规格。**未裁**。

---

## Related Files

- `design/gdd/enemy-ai.md` — 27 敌人 AI(六态机 / 视觉契约 / 读数条触发)
- `design/gdd/skeuomorphic-ui.md` — V-10 敌人读数条(黄铜侧)
- `design/art/art-bible.md` §3.1 / §5.1 / §7.6 / §10 — 形状立法 / 墨铜分轨 / 读数条 / 性能预算
- `design/levels/prologue-beat-script.md` — PA-04(兵痞 1)/ PA-05(野狗 2)遭遇编排
- `design/ux/enemy-42.md` — 敌人呈现 UX spec(世界空间)
- `unity/Assets/Gameplay.Presentation/Enemies/ComposedEnemyVisualFactory.cs` — P0 占位实现
