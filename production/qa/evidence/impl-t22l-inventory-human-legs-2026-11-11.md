# T22 批十二 —— 药箱人工腿(`inventory-006`)证据原件

**日期**:2026-11-11
**批次**:T22 批十二(用户「开工 T22 批十二」;范围 = **甲 · 药箱人工腿(S26-5)**)
**引擎**:Unity 6.3 LTS(6000.3.24f1)
**构建**:`DaYiJingCheng.EditorTools.Bake.PlayerBuildCli.Build` ⇒ `Build Finished, Result: Success.`
(27.4 s;产物 `unity/builds/Linux-x64/大医精诚.x86_64`)
**运行**:`DISPLAY=:0`,1280×800,`-tutorialdiag`;存档 = 开发机现有世界(继续诊治)。
**HEAD**:`323871d4` + 工作树(本批两文件改动)

---

## 1. 范围与结果一句话

用户选定「药箱人工腿」= 真包走查 `AC-20-10` / `AC-20-11` / `AC-20-20` / `AC-20-23`。
本批查出**两条真缺陷**,均已修并补 PlayMode 夹具:
① 药箱**翻页后焦点悬空**(第 2 页方向键不可达);
② 药箱四档的**沿口 / 半盖从未上屏**(清树循环把非格子件摘掉,`display:flex` 画在树外元素上)。
其余三条 AC 的人工腿**按实况登记**:满档见 §5;音效见 §6;两品级见 §7(受阻,`NOT-RUN`)。

---

## 2. 走查路径(可复跑)

| 步 | 操作 | 观测 |
|---|---|---|
| 1 | 启动 → 主菜单 `Return`(继续诊治) | 出生格 `(0,0)` |
| 2 | 闭环走位到容器柜 `(5,0,5)`(`-tutorialdiag` 的 `cell=` 逐格纠偏) | 到位 |
| 3 | 按 `E` | `模态=InventoryContainer`;开屏焦点 = 首格 `container-item-2` |
| 4 | 采集:按住 `E` ≥ 1.35 s(`duration_ticks = 20` ⇒ 1 s) | 每次成功产出一叠(叠 2) |
| 5 | 回箱、按 `E`、方向键遍历、末行 `Down` 到 `page-next`、`Return` 翻页 | 见 §3 |

> ⚠️ **「按住 E ≥ 1 s」不是可选**:首轮用 `tap('e')` 采不到任何东西,`ResourceHarvested` 零增长 ——
> 交互的表头写的是持续窗口(`AC-17-08`),而 `HarvestInteractionDriver` 只在**窗口满**时结算。

---

## 3. 缺陷:翻页后焦点悬空(`TD-skeuo-23`)

**修复前(真包)** —— `t22l-probe2.log`:

```
[tutorialdiag] 焦点=container-item-11
[tutorialdiag] 焦点=container-item-12
[tutorialdiag] 焦点=container-item-13
[tutorialdiag] 焦点=page-next
[tutorialdiag] 焦点=(无焦点)      ← 按 Return 翻到第 2 页之后
```

第 2 页当时有 2 件器物,方向键**无处可走** —— `AC-20-20` 的「可遍历」在这一页断开。

**根因**:`TurnPage` 只做 `_page = target; RebuildPage();`。`RebuildPage` 把旧格从树上摘掉,
焦点随元素脱离 panel 而丢失;新格**没有任何 `Focus()`**。

**修法**(`InventoryContainerScreen.cs`):重建后把焦点**推迟一帧**落到新页首格:

```csharp
VisualElement resume = _cells[0];
resume.schedule.Execute(() =>
{
    if (resume.panel != null && resume.focusable) resume.Focus();
}).ExecuteLater(0);
```

推迟是必需的:本方法由铜扣的 `NavigationSubmitEvent` 派发中调用,同帧直接 `Focus()`
会被随后的输入更新清掉(与 `FocusFirstLexiconEntry` 同因)。

**修复后(真包,同一存档、同一路径)** —— `t22l-verify.log`:

```
[trail] container-item-3 → … → container-item-12 → container-item-13 → page-next → AFTER:container-item-14
[down1] container-item-15
[down2] page-prev
```

**同内容 + 同按键序列 ⇒ 同焦点目标**(第二遍从 `page-prev` 回第 1 页重走):

```
[back-to-page1] container-item-2
[trail2] container-item-3 → … → container-item-13 → page-next → AFTER:container-item-14
```

两次 trail 逐项相同。

### 3.2 缺陷二:四档的沿口 / 半盖从未上屏(`TD-skeuo-24`)

**真包实测(修复前)**:药箱从空到满,`ApplyBandGeometry` 的格间隙在变,但近满的 2 px 沿口
`(71,51,33)` 与满档的 10 px 半盖 `(51,36,23)` **一次也没有上屏**(`70-fast*.png` 像素扫描零命中)。

**根因**:`RebuildPage` 的清树循环只豁免 `item-grid-paper`;`_rim` / `_lid` 是构造期加进
`_gridHost` 的**非格子件** ⇒ `Bind` 的首次 `RebuildPage` 把它们从树上摘掉。之后
`ApplyBandGeometry` 设 `display: flex` 打在**树外元素**上 —— 属性为真,屏上为零。
既有夹具只断言 `style.display` / `IsRimVisible` 属性,**查不到「仍在树上」**(F1/F8 形态)。

**修法**:清树循环同时豁免 `box-rim` / `box-lid`;新增 PlayMode 夹具断言 `panel != null`。

**修后真包复核**:重建玩家包、载入同一存档(load = 60)⇒ `80-band-open.png` 的 y 396–400
实测 **277 px × 5 px 的 `(51,36,23)`**(半盖;UI 缩放把 10 px 压成 5 px),紧邻 y 402
出现沿口混色 `(123,88,41)` ⇒ 几何通道真的上屏。

---

## 4. 判据

### 4.1 新增 PlayMode 夹具(2 条)

`unity/Assets/Tests/PlayMode/SkeuomorphicUI/inventory_container_content_test.cs`:
`test_ac2020_pageTurn_movesFocusToNewPageFirstCell` —— 15 件(instance 100..114),第 1 页 12 格,
翻页后断言 `focusController.focusedElement` 是第 2 页首格 `container-item-112`。

`test_ac2010_bandGeometry_rimAndLidStayAttachedAcrossRebuild` —— NearFull(load 55/60)断言沿口
**在树上**(`panel != null`)且可见;Full(load 60 + 谓词 true)断言半盖同理。

- 定向跑(修复后):`t22l-focus2.xml` ⇒ `total=9 passed=9 failed=0 inconclusive=0 skipped=0`。
- **证伪 M1**(删掉 `TurnPage` 里的推迟落焦整块)⇒ `total=9 passed=8 failed=1`,
  红的恰是 `test_ac2020_pageTurn_movesFocusToNewPageFirstCell`(其余 8 绿)。
- **证伪 M2**(撤掉 `RebuildPage` 对 `box-rim` / `box-lid` 的豁免)⇒ `total=9 passed=8 failed=1`,
  红的恰是 `test_ac2010_bandGeometry_rimAndLidStayAttachedAcrossRebuild`(其余 8 绿)。
- 还原判据 = `sha256sum -c`:两轮均还原到 `63139b4540867068eb3f983d925cb3a63e056d2332863810c6b64397f5c9c78a`,成功。

### 4.2 全量回归(三元组)

| 套件 | 结果 | 窗口(UTC) | 批次 / commit |
|---|---|---|---|
| EditMode | `7453/7416/0/0/37` | `2026-11-11T08:38:06Z`–`08:41:08Z` | T22 批十二 / `323871d4` + 工作树 |
| PlayMode | `388/383/0/2/3` | `2026-11-11T08:41:08Z`–`08:42:12Z` | 同上 |

等式:`PlayMode 388 − 386 = 2`(两条新增夹具);`EditMode 7453 − 7453 = 0`(本批无 EditMode 新增)。

---

## 5. 满溢四档(AC-20-10 的人工腿)

四档实现 = `BandOf` + `ApplyBandGeometry`(全走几何:格间隙 6/3/0/−3 px;近满出「沿口」2 px;满档出「半盖」10 px)。

| 档 | 观测(修后真包) | 截图 |
|---|---|---|
| 空 | 新起一局(tick 706)开箱:**只有空槽横条**,无器物 | `t23/12-empty-box.png` |
| 半满 | 2 轮采集后:器物铺开、格间隙 3 px;**无沿口** | `t23/13-cycle2.png` |
| 近满 | 4 轮后:格间隙 0 px,页首出现 **2 px 沿口** —— 像素实测 y 388–389 连续两行、每行 277 px、色 `(71,51,33)` | `t23/13-cycle4.png` |
| 满 | 6 轮后:页首出现 **10 px 半盖**(UI 缩放后 5 px)—— 像素实测 y 396–400 连续五行、每行 283 px、色 `(51,36,23)`;另一份 load = 60 的存档同样可见(`80-band-open.png`/`81-band-lastpage.png`) | `t23/13-cycle6.png` · `t22l/80-band-open.png` |

⇒ **四档在修后同一会话内全部拍到**(新世界 `w10`:空 → 采集 2/4/6 轮),沿口与半盖均由
**像素色值与连续行数**判定,不是靠目测文字描述。修复前那批近满帧(无沿口)只能证明缺陷②,
**不作为**本表的证据。

**禁数字 / 禁负重条**:屏内无任何数字载重、无百分比、无进度条 —— 呈现只有 `Band` 派生的几何
(`grep -n "Band" InventoryContainerScreen.cs` 的消费面 = `ApplyBandGeometry`;零 `Label` 写载重)。

**⚠️ 不得借绿**:`AC-20-10` 是 `[L]`(主创签核)。本批只提供**截图与过程**,**不代替签核**。

---

## 6. AC-20-11 · 库存音效零状态播报(人工听测半边)

**机器侧证据**:

- `assets/data/audio_events.json` 共 **21** 行,按名字/触发器筛 `item|inventory|pick|box|container|drop`
  ⇒ **零命中**(`python3` 判决见下)。
- `InventoryContainerScreen` / `InventoryContainerDto` 生产码里**零** `IAudioCueSink` / `AudioCueDto` 引用。
- 四档切换、翻页、开合箱**不产生任何 cue** —— 满档不会响「警告」。

**仍不勾**:人工听测(尤其「无拾取 jingle / 无满箱警告」的听感)未做;本条 `[L]`,须人耳 + 签核。

---

## 7. AC-20-23 · 两品级同种器物的非颜色通道

**本批不可达,记 `NOT-RUN`**:

- 真包 ~40+ 次采集,**全部 `品1`**(截图页 1/页 2 每行都是 `·品1·`)。
- 根因是**机制上限**,不是运气:`foraging_constants.json` 的 `cap_table[L] = 1 + min(L,20)/20`
  (整数除法)⇒ **L < 20 时 `QualityCap = 1`**,`GatherQualityRoll.OutQuality` 把掷骰裁到 1;
  而 `gather_profile.quality_distribution` 虽是 `[1,1,1,1,1]`,被 `QualityCap` 截断后不可见。
- 解除判据:采集技能等级 **≥ 20**(或用户裁定改 `cap_table` 公式 / 数值轮)。

因此「形状 / 药签文字承载品级差」的**屏上判据**本批没有对象可看;`AC-20-23` 维持 `NOT-RUN`,
**不得**用「夹具里有两品级」借绿。

---

## 8. 走查的连带发现(非库存域,已登记)

**`TD-foraging-05`(open)**:`world_resources` 的 4 个资源点两两同 `resource_id`(1/1/2/2),
而 `HarvestWorldQuery.cs:85` 用 `nodeId = row.ResourceId` ⇒ 同种两格**共享余量池**。
实测:先采 `(2,0,2)` 再立刻采 `(5,0,9)`,第二次几乎总被余量门拒。
关卡工具注释明写「资源 id 是种类」(`ResourceSource.cs:109`),GDD 却把 `node_id` 定义为节点 —— 两侧语义不同。
该发现直接压低了本批的采集速率(每 60 s 窗口最多 2 次成功采集),并解释了「满档要走很多轮」。

---

## 9. 本批改动

| 文件 | 改动 |
|---|---|
| `unity/Assets/Gameplay.UI/Skeuomorphic/Screens/InventoryContainerScreen.cs` | ① `TurnPage` 重建后推迟一帧落焦到新页首格;② `RebuildPage` 清树时豁免 `box-rim` / `box-lid` |
| `unity/Assets/Tests/PlayMode/SkeuomorphicUI/inventory_container_content_test.cs` | 新增 `test_ac2020_pageTurn_movesFocusToNewPageFirstCell` · `test_ac2010_bandGeometry_rimAndLidStayAttachedAcrossRebuild` |
| `docs/tech-debt-register.md` | 新增 `TD-skeuo-23`(closed,N)· `TD-skeuo-24`(closed,Y)· `TD-foraging-05`(open,N) |

---

## 10. 复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# 1) 真包焦点序列(修复前 / 后)
grep -a "焦点=" /home/gu/.claude/jobs/6001633c/tmp/t22l-probe2.log | tail -5
grep -a "\[trail\]\|\[trail2\]\|AFTER" /home/gu/.claude/jobs/6001633c/tmp/t22l-verify.log
# 2) 定向夹具 / 证伪
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/t22l-focus2.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'))"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/falsify-t22l-M1b.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'))"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/falsify-t22l-M2.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'))"
# 3) 全量
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/t22l2-EditMode-full.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'),r.get('inconclusive'),r.get('skipped'))"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/t22l2-PlayMode-full.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'),r.get('inconclusive'),r.get('skipped'))"
# 4) 库存域零音效面
python3 -c "
import json
rows=json.load(open('assets/data/audio_events.json',encoding='utf-8'))['rows']
print(len(rows), [r['cue'] for r in rows if any(k in str(r.get('cue','')).lower() for k in ('item','inventory','pick','box','container','drop'))])"
grep -rn "IAudioCueSink\|AudioCueDto" unity/Assets/Gameplay.UI/Skeuomorphic/Screens/InventoryContainerScreen.cs || echo 零引用
# 5) 品级上限(AC-20-23 的阻塞物)
python3 -c "
import json;d=json.load(open('assets/data/foraging_constants.json',encoding='utf-8'));print(d['cap_table'][:22])"
```