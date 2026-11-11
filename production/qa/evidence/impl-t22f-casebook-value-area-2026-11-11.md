# T22 批六 —— 脉案值区两条缺陷(`TD-skeuo-18` A / B)证据原件

**日期**:2026-11-11
**批次**:T22 批六(用户「开工 T22 批六」→ 从四条 P0 面候选里选 **甲 · 脉案值区两条缺陷**)
**债**:`TD-skeuo-18`(`open` → 关闭,见 §6)

---

## 0. 两条缺陷的**真正**病因(与债册初版记的不完全一样)

债册 `TD-skeuo-18` 立债时把 A 记成「`RefreshFromSource` 没有『数据变了』这条触发」。
**复勘后更准确的说法**:`SkeuoRuntimeDriver.OnTickEdge()` **一直**在调 `CasebookScreen.ApplyData`,
问题是**那个 tick 边沿在册子摊开时不会发生** —— 脉案是挡路模态,ADR-013 §十-C Amendment D
让世界在模态摊开期间真停 tick(`BootRoot.cs:3199` 的 `if (_tickDriver.CurrentTick > tickBefore)`)。
**这与 2026-11-05 存档册那一处是同一条病**(当时把重探从 `OnTickEdge` 搬到 `OnFrameEdge`)。
判据:`grep -n "_tickDriver.CurrentTick > tickBefore && _uiDriver != null" unity/Assets/Gameplay.Boot/BootRoot.cs`
⇒ 命中 `:3199`(`_uiDriver.OnTickEdge()` 的唯一调用条件)。
⇒ 修法不是「加一个刷新调用」,而是**把重读搬到每帧边沿 + 用脏标记收敛**。

B 的病因也定位到了具体资产 + 具体规则,不是「疑点方向」(见 §2)。

---

## 1. A:查体后不重绘

| 件 | 落点 |
|---|---|
| 脏标记 | `SkeuoRuntimeDriver.MarkCasebookDirty()` + `_casebookDirty` |
| 消费点 | `SkeuoRuntimeDriver.OnFrameEdge()` 开头(先于存档册那段 `return` —— 否则被早退吞掉) |
| 置位点 | `BootRoot` 的 `CasebookCommands.AfterExamined` |

**置位点为什么写成「一个 lambda 两个消费者」而不是两次 `+=`**:
`AfterExamined` 是 `{ get; set; }` **属性**(不是 event),谁后赋值谁覆盖。
原实现是单播(只给 `SeenDisease`),再加一个订阅点若各写各的,后来者会把前者**静默顶掉**。
合成一个 delegate ⇒ 「漏一个」不会静默半坏。

**为什么用脏标记而不是每帧无脑 `ApplyData`**:`ApplyData` 会清掉并重建每个读数格
(`reading-*` 的 DOM),每帧重建 = 每帧 GC + 焦点/悬停抖动。脏标记把它收敛到「数据真的变了」那一帧。

---

## 2. B:值词条渲染成不可读墨块

**定位过程**:先在真包里确认**通道题签**(面色 / 语声 / …)用**同一个 SDF 字体**渲染得清清楚楚
(`unity/Logs/shots/t22c/13-back-to-casebook.png`)⇒ 排除字体。剩下的差异只有一处:
读数格上叠了 `.ink-wet` / `.ink-dry`。

| 实测 | 读数 |
|---|---|
| `ink_wet-final.png` | 1024×1024 · **全不透明**(alpha 恒 255)· 平均亮度 **63** · 中心像素 (67,64,57) |
| `ink_dry-final.png` | 1024×1024 · 全不透明 · 平均亮度 **146** · 中心 (70,67,60) |
| 读数格尺寸 | 约 100×40(`reading-<row>` 包一个 24px 字号的 Label) |
| 文字色 | `--skeuo-ink-fg` #1a1410 / `-wet` #26241f / `-dry` #191714 |

```bash
python3 -c "
from PIL import Image
for f in ['ink_wet-final.png','ink_dry-final.png']:
    im=Image.open('unity/Assets/Gameplay.UI/Skeuomorphic/Textures/'+f).convert('RGBA')
    px=list(im.resize((64,64)).getdata())
    print(f, im.size, 'alpha', min(p[3] for p in px), max(p[3] for p in px),
          'lum', round(sum(0.299*p[0]+0.587*p[1]+0.114*p[2] for p in px)/len(px)))
"
```

⇒ 一张 **1024² 的不透明整幅图**被拉伸进 100×40 的格子 ⇒ 格面 = 墨块;
文字色又同为近黑 ⇒ **字与底同色**,放大三倍也读不出字形。这与实拍症状逐字吻合。

**修法(保留 story-022 的契约,只改载体)**:`.ink-wet` / `.ink-dry` **照挂**(既有 T2.5 三条判据
必须继续绿),但在值区格上加一条复合规则:

```css
.channel-reading.ink-wet, .channel-reading.ink-dry,
.channel-reading-negative.ink-wet, .channel-reading-negative.ink-dry {
    background-image: none;
}
```

只保留 art-bible §4.5 本来的区分 —— **明度轴的颜色差**(湿 #26241f / 干 #191714),
墨渍图不给(它是**大面积书写面**的载体)。复合选择器特异度高于单类 ⇒ 不依赖 stylesheet 加载序。

⚠️ **不动基类**:大面积书写面仍要那张图(AC-022 冻结件 `ink_wet_dry_test.test_ac022_2` 钉着 url)。
⚠️ 若将来要给值区做墨龄的**可见**形态,须换不覆盖文字的载体(注意 `AC-42-F3` 词门禁
「数字角标」/「进度填充」)⇒ 另开 story。

**这条修法顺带把一处无障碍承诺兑现回来**:`accessibility-requirements.md:85` 与
`casebook-39.md:309` 承诺「墨字 on 纸纹 ≥7:1(按**最亮像素**)」,而 S3 轮实测的两个数
(阳性 15.5:1 / 阴性 8.27:1)都是按 `--skeuo-reading-backing` 算的 —— 墨渍图一铺,
真实的底根本不是那个颜色。修后读数格的底**就是**被算过的那一块纸色 ⇒ 承诺与实现重新对齐。

⚠️ **门没有覆盖这件事** —— `skeuo_readability_test.test_s3_1_*` 只读主题变量,看不见运行期
叠加类画的底图(所以它一直绿)。已另立 **`TD-skeuo-22`**(治理债,`P0 面 = N`),
解除动作 = 在门里加「读数格任何状态下不得有底图」的封闭判据。
**本条修的是症状,不是门的盲区** —— 不得据本条为绿称「值区对比已由门守住」。

---

## 3. 真包实测(两条腿一起看)

构建:`PlayerBuildCli.BuildDevelopment` ⇒ `Build Finished, Result: Success.`
运行:`DISPLAY=:0`,1280×800,`-tutorialdiag`;存档根走查前移开、走查后原样还原(本次走查档留 `saves.t22f-run/`)。

| 帧 | 读数 |
|---|---|
| `03-casebook-before.png` | 脉案摊开,**值区全空**(未查 = 空行) |
| `05-after-exam2.png` | 面色行 **5 个词条**:`面色不好 / 面色发红 / 出汗 / 身上有东西 / 面色发黄`;语声仍空 |
| `06-after-exam3.png` | 语声行 **3 个词条**:`说话没力气 / 呻吟 / 说话喘` |

**A 的判据在日志里是硬的**:模态摊开期间 `tick` **停在 249** ——

```text
[tutorialdiag] 模态=Casebook 档位=Casebook tick=249
[tutorialdiag] 查体投影 patient=1 actor=0 disease=2 novelty=First tick=249
[tutorialdiag] 查体投影 patient=1 actor=0 disease=2 novelty=Stale tick=249
```

⇒ 值区是在**世界不推进的 tick 上**出现的。旧实现只挂 `OnTickEdge`,在同一个场景里
**结构上不可能**更新 —— 这就是 A 条的证明,不是「跑了一次看起来对」。

**B 的判据是画面**:词条为深墨字 + 淡纸底 + 细边框,清晰可读(见 `05` / `06` 帧);
overlay 的墨渍图不在。

⚠️ **如实登记一处不确定**:`04-after-exam1.png`(第一次 `Return` 之后)仍是空行。
按 `TD-skeuo-20` 的先例,脉案开屏后第一次 `Return` 很可能是**建立焦点**而非提交
(本轮没有为此单独埋点)⇒ 不能据此说「第一次查体不刷新」。**要钉这条须另做一次
带焦点读数的走查**;A 的结论不依赖它(第 2、3 次查体都当场出了字)。

---

## 4. 证伪轮(四枪;前三枪文件级、第四枪**构建级**)

前三枪:每枪改一处 → 跑对应套件 → 还原 → SHA-256 逐字节比对。

| 枪 | 改法 | 结果 |
|---|---|---|
| **M1** | `SkeuoRuntimeDriver.OnFrameEdge` 里**去掉脏标记条件**(每帧无条件 `ApplyData`) | `failed = 1` = **`test_tdSkeuo18_withoutDirty_frameEdge_doesNotRebuild`**(负控) —— 证明「脏标记收敛」不是靠嘴说的 |
| **M2** | `MarkCasebookDirty()` 变成**空操作** | `failed = 1` = **`test_tdSkeuo18_markDirty_frameEdge_refreshesValueArea`**(正例) |
| **M3** | 删掉 `SkeuoPaper.uss` 的值区墨渍图抑制规则 | `failed = 1` = **`test_tdSkeuo18_readingCells_suppressInkTexture`**(EditMode,285 条全跑) |
| **M4** | **生产接线**:把 `BootRoot` 里 `_uiDriver?.MarkCasebookDirty();` 去掉 → **重新构建** → 同一真包走查 | 查体**发生了**(日志 `查体投影 … tick=245`),而值区**全空**(`unity/Logs/shots/t22f-m4/m4-mut.png`);`tick` 停在 245 ⇒ **没有**任何 tick 边沿兜底 |

**M4 是本轮最重的一枪**:M1–M3 证明的是「代码里的判据有牙」,**M4 证明的是「生产装配真的把牙咬上了」**
—— 单元/集成夹具都碰不到 `BootRoot` 的那一行(没有为它造 boot 级夹具),所以对**接线腿**的证明
只能靠构建级变异。⚠️ **如实登记一处覆盖缺口**:`BootRoot` 的这条接线**今日无自动化夹具**
(要覆盖须起整个 boot 序),本轮以 M4 的构建级变异代替 —— 它是**证据**,但**不是**常驻门。

三文件 SHA-256 逐字节还原(`SkeuoRuntimeDriver.cs` / `SkeuoPaper.uss` / `BootRoot.cs`),
四枪全部打印「还原 成功」/「[restore] 成功」;M4 之后**重建了一次干净包**
(`Build Finished, Result: Success.`),不留变异二进制。

---

## 5. 回归(三元组)

| 套件 | 结果 | 截至时刻(UTC) | 批次 | commit |
|---|---|---|---|---|
| EditMode | `7428 / 7391 / 0 / 0 / 37` | `2026-11-11T01:08:57Z`–`01:11:39Z` | T22 批六 | `d4c10588` + 工作树 |
| PlayMode | `378 / 373 / 0 / 2 / 3` | `2026-11-11T01:11:39Z`–`01:12:37Z` | T22 批六 | `d4c10588` + 工作树 |

基线(T22 批五):EditMode `7427 / 7390 / 0 / 0 / 37` · PlayMode `376 / 371 / 0 / 2 / 3`。
**等式**:`7428 − 7427 = 1` = EditMode 新增 `ink_wet_dry_test.test_tdSkeuo18_*`;
`378 − 376 = 2` = PlayMode 新增两条 `skeuo_runtime_driver_test.test_tdSkeuo18_*`。
skipped / inconclusive 与批五同集合。

---

## 6. 债册

| 动作 | 结果 |
|---|---|
| `TD-skeuo-18` | **`open` → `closed`**(附闭合记录;两半解除判据均有载体) |
| `TD-skeuo-22`(新) | 立债:`P0 面 = N`、R3 —— 对比门只算主题色、看不见叠加类底图(本次缺陷的**门盲区**) |
| `debtcount --check` | exit 0 · 条目 **207** · closed **173** · open **23** · **未闭 P0 面 14 → 13**(skeuomorphic 5 → 4) |

---

## 7. 本批没有做的(不得借绿)

| 项 | 状态 |
|---|---|
| 「第一次 Return 是否建立焦点」 | **NOT-RUN**(见 §3 的保留) |
| 值区墨龄的**可见**形态 | **不做** —— 只保留明度轴颜色差;要做须换不覆盖文字的载体 |
| `AC-42-F1` 整体走查 | 仍 `NOT-RUN`(与 `TD-skeuo-18` 无关,阻塞在 ⑨ 炮制屏 spec) |
| 「查体 ⇒ 判定链」的完整业务验收 | **不在本债面内** —— 本债只问「值区是否显示 / 可读」 |
