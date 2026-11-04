# 视觉走查(图形模式七帧)—— 存档 / 设置 / 炮制 / 主菜单

> **日期**: 2026-11-04 · **sprint-24** · commit 基线 `20af6fbf` + 工作树
> **触发**: 用户指令「把刚才完成的两项和存档有关的内容写脚本去实际看看效果」。
> **载体**: `unity/Assets/Tests/PlayMode/SkeuomorphicUI/save_and_settings_visual_walkthrough.cs`
> (PlayMode · **必须图形模式跑**,`-batchmode` 下 `WaitForEndOfFrame` 不触发 ⇒ 零截图)。
> **产物**: `unity/Logs/shots/sv_01_pause.png` … `sv_07_processing.png`(1920×1080,gitignored 暂存区)。
> **判读方式**: 逐帧读图(模型原生读图),不看断言 —— 断言只保证「链跑通了」,图才回答「长什么样」。

---

## 1. 实跑命令

```bash
DISPLAY=:0 ~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity \
  -projectPath "$(cd unity && pwd -P)" -runTests -testPlatform PlayMode \
  -testFilter "DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI.SaveAndSettingsVisualWalkthrough" \
  -testResults "$(pwd)/unity/Logs/playmode-visual.xml" \
  -logFile "$(pwd)/unity/Logs/playmode-visual.log"
# 实跑:total=1 passed=1 failed=0;七帧全部落盘(每帧 ~70–235 KB)
```

---

## 2. 七帧逐条判读

| 帧 | 内容 | 判读 |
|---|---|---|
| `sv_01_pause` | 暂停壳 | ✅ 三行「继续 / 存档位 / **调治**」同纸、纸纹与格线可见、首行带黄铜焦点环。**本轮新增的第三行真的在画面上** |
| `sv_02_settings` | 设置壳 | ⛔ **两条横贯全屏的空白纸条,零文字**(见 §3.1) |
| `sv_03_saveslots_last` | 存档册 第 3/3 页 | ✅ 纸页有双层页框、行在页内、页码「3 / 3」在页脚、「留档」行只在末页 |
| `sv_04_saveslots_first` | 存档册 第 1/3 页 | ✅ 六行(壹…陆)、行序正确、首行焦点环 |
| `sv_05_saveslots_zoom150` | 150% 容量降档 | ✅ 第 1/4 页、**四行**(壹…肆)⇒ §5.1 的「6 行降 4 行」在画面上成立 |
| `sv_06_mainmenu_notice` | 主菜单 + 未完整保存提示 | ✅ 纸面居中、「继续诊治」一行带焦点环、下方灰字「上次会话未完整保存」**加行而不替换主行** |
| `sv_07_processing` | 炮制屏三行候选 | ⚠️ 三行在纸面上可读,但文案是**原始键名** `herb_soup` / `herb_pill` / `herb_powder`(已登记的粗糙边,21a 无显示名字段);纸面贴顶(见 §3.2) |

---

## 3. 看图才看出来的问题(两笔已登记)

### 3.1 `TD-skeuo-11` · 设置壳是**空壳**

`SettingsScreen.BuildUI()` 只塞两个空 `SkeuoElement.Paper`,无 Label、无列宽、无容器 class
⇒ 屏上是两条 1920px 宽的空白纸条。`story-014` 标 `Complete`、10 条夹具全绿,
但那些夹具判的是「两个 paper 元素存在」—— **结构断言,不是可见性断言**。
本轮让该屏**可达**之后,这个问题才第一次出现在画面上。

### 3.2 `TD-skeuo-12` · 所有模态屏**贴顶**

暂停壳 / 存档册 / 炮制屏 / 设置壳的 `worldBound` 全部起于 **y = 0**;
而场景级主菜单是居中的(`.menu-host{justify-content:center}`)。同一 UI 体系两套纵向定位。
不是顺手改 CSS 的事:模态容器由驱动建,改它影响全部 9 员的落点与既有几何夹具。

---

## 4. 同轮修掉的判据面缺陷(不计条目)

**两个视觉走查件的「清屏相机」都没有指向 RenderTexture。**
它们建相机的注释写着「没有相机 ⇒ 逐帧累积」,但相机只清了 Game 视图,RT 从头到尾没人清
⇒ UI 绘制照样累积。首轮本走查据此截出过:

- `sv_02`:**暂停壳的行 + 设置壳的纸带同时可见**(像是「模态叠着不关」);
- `sv_05`:**两张册页卡上下叠在一起**(像是「换页不重建」)。

两条都**不是产品缺陷**,是截图装置的假象 —— 逻辑断言同时是绿的。
**修法**:`camera.targetTexture = _target`(相机与面板同一目标,每帧先清再画)。
修后同一序列的画面干净,文件体积从 ~225 KB 降到 ~70–130 KB(少了叠影)。

⚠️ **这条的教训与 `TD-skeuo-07` 同族**:「判据齐全 ≠ 画面成立」,而**看图装置本身也会骗人** ——
截图装置的缺陷会伪装成产品缺陷,并且与「产品缺陷」在日志上长得一模一样。

---

## 4.1 三处实跑修正(首轮全量套件的红,全在本轮新件上)

| 红 | 原因 | 修法 |
|---|---|---|
| `test_realTestTree_zeroRed` 等 3 条 | `FsWriteBoundaryGate` **R2**:新走查件同时含写盘调用与 `Application.dataPath` 锚点,须具名声明 | 在 `FsWriteBoundaryGate.Declarations` 补一条(写目标 `unity/Logs/shots/sv_*.png`,batch 零写盘) |
| PlayMode `RenderTexture.Create failed` ×2 | 相机指向 RT 在 `-batchmode -nographics` 下真去渲 ⇒ 报错并使整条变红 | `camera.targetTexture` 只在 `!Application.isBatchMode` 时挂 |
| (上一轮)走查中断 | `DumpTree` 在存档册子树上抛 NRE(诊断代码把走查搞红) | 诊断面全部 try/catch —— 它只负责多一条线索 |

**终态三元组**:截至 `2026-11-04T03:05:54Z` · sprint-24 视觉走查轮 · `20af6fbf` + 工作树
· EditMode `6831 / 6794 / 0 / 37` · PlayMode `332 / 328 / 0 / 2 / 2`(较上轮 **+1** = 本走查件)。

---

## 5. 如实登记(不借绿)

1. **七帧是夹具构造态,不是真启动序** —— 槽位视图、配方表、准入查询都是夹具喂的;
   生产链路(`BootRoot` 整段)仍无 PlayMode 判据。
2. **`TextScale` 只改容量、不改字号** —— 150% 那张图里字号与 100% 完全相同;
   字号的实际缩放归 49(P2),42 只消费 `TextScale` 这个输入。
3. **`sv_07` 的配方名是键名**,不是中文方名(21a 无 `DisplayName`)。
4. 设置壳的内容缺口与模态贴顶**已登记为债**,本轮未修。
