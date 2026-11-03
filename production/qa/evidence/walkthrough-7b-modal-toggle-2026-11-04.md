# 视觉走查 —— 模态开合链(`TD-skeuo-03` 结案轮)

**截至时刻**: 2026-11-04 06:45 CST
**批次**: sprint-24 · 用户质问「你是视觉模型,你之前有编写脚本去打开编辑器直接看修改后的内容」
**commit**: `787396b0` + 工作树

---

## 1. 为什么补这一轮

前面三轮我都只读了 XML 的 `failed=0`。那证明**判据**对了,证明不了**画面**对。
用户点出这一点之后,我按仓里既有的图形模式先例
(`Tests/PlayMode/camera_downed_visual_walkthrough.cs` + `tools/` 里的 graphical 跑法)补了走查。

**跑法**:去掉 `-batchmode` / `-nographics`,走真实 X 显示(`DISPLAY=:0`),
`-testFilter ModalToggleVisualWalkthrough`,截图落 `Logs/shots/`,再复制进本目录。

## 2. 五帧与它们的哈希

| 帧 | 文件 | 耗时结论 |
|---|---|---|
| ⓪ 世界(载体获焦) | `…-00_world.png` | 基线 |
| ① 暂停壳 | `…-01_pause.png` | — |
| ② 存档册 | `…-02_saveslots.png` | — |
| ③ 关册 ⇒ 回暂停壳 | `…-03_backtopause.png` | **与①逐字节相同** |
| ④ 再关 ⇒ 回世界 | `…-04_world_restored.png` | **与⓪逐字节相同** |

```
aef1d462201d…  00_world  == 04_world_restored
95d811425509…  01_pause  == 03_backtopause
9b6f4625c339…  02_saveslots(独立)
```

**这是本轮最硬的一条视觉证据**:「关册回暂停壳」不是「看起来像」——
**像素完全相同**;「关到底回世界」同样逐字节回到出发状态。
栈语义(壳类压栈、弹栈回上一层、栈空还原焦点)在画面上成立。

## 3. 看图发现的三件事(两件是装置问题,一件是**产品问题**)

### ① 首轮全黑 —— 装置问题(裸 `PanelSettings`)
裸 `CreateInstance<PanelSettings>()` 没有 theme、没有样式 ⇒ 面板什么都不画,
五帧**哈希完全相同**、全黑。
**修法**:改载生产资产 `Assets/Gameplay.UI/Skeuomorphic/PanelSettings.asset` +
九份 `Skeuo*.uss`(同 `casebook_reading_inline_test` 的 `#if UNITY_EDITOR` 守卫写法)。

### ② 重影 —— 装置问题(场景里没有相机)
修好样式后,`02` / `03` 上同时出现「暂停卡片 + 存档册横线」的重影,
而夹具断言 `CurrentModal == Pause` 是**绿的**。差点据此报模态切换泄漏。
**真因**:场景里没有相机 ⇒ 每帧没人清屏 ⇒ UI Toolkit 的绘制**逐帧累积**在 backbuffer 上。
**排除法**:容器泄漏那条已被既有绿夹具
`test_switchingModal_leavesExactlyOneModalContainer` 否掉。
**修法**:加一台 `cullingMask = 0` 的纯色清屏相机;重影消失,哈希关系立刻变干净。

### ③ 存档册**没有纸页** —— **产品问题,不是装置**

`02` 里存档册是一列**带横线的行**,没有纸页、没有页缘、页码 `1 / 2` 贴在左上角极小、
焦点环被拉成横贯全宽的黄色带。逐条查证:

| 查什么 | 实测 |
|---|---|
| `SaveSlotsScreen` 建树方式 | **代码建树**(`BuildUI`),不用 UXML |
| `SaveSlots7b.uxml` 的 C# 引用 | **零**(`grep -rn "SaveSlots7b" --include=*.cs` 无命中) |
| `save-slot-page` / `save-slot-edge` 的 USS 规则 | **全仓零命中** |
| `slot-list` 的 USS 规则 | 零(UXML 里写的 `class="scroll"` 也**没有规则**) |
| `.save-slot-item` | 有:`border-bottom` + `min-height` ⇒ 就是图上那一列横线 |

⇒ **存档册今天的视觉形态 = 一列带横线的文本**,拟物纸页 / 页缘 / 页码**从来没有样式载体**。
这不是本轮分页改坏的(它此前也是这个形态),而是 7b 视觉层一直没落。

⚠️ **连带的判读限制**:图上行字极暗(深墨落在深色世界背景上)——
没有纸页底色,「行字可读性」这一条**在真机上也站不住**,不只是在装置里。

## 4. 本轮不修,登记为 `TD-skeuo-07`

修它不是「补几行 USS」这么简单:要么把 `SaveSlotsScreen` 接回它自己的 UXML
(所有屏都是代码建树,接 UXML = 改这一屏的建树范式),要么按 7b spec §5 补一整套
纸页 / 页缘 / 页码的 USS 与九宫格素材。两条都属**设计面**,须先裁定载体形态。

## 5. 复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# ① UXML 零引用
grep -rn "SaveSlots7b" unity/Assets --include=*.cs | wc -l          # 期望 0
# ② 三个 class 无样式
grep -rn "save-slot-page\|save-slot-edge" --include=*.uss unity/Assets | wc -l   # 期望 0
# ③ 只有行样式
grep -rn "save-slot-item" --include=*.uss unity/Assets               # 期望恰 1 处(SkeuoPaper.uss)
# ④ 帧间哈希关系
cd unity/Logs/shots && sha256sum modal_*.png
```
