# 7b 纸页成形 + USS 图像层复活(`TD-skeuo-07` 修复轮)

> **日期**: 2026-11-04 · **sprint-24** · commit 基线 `663dca68` + 工作树
> **触发**: 用户指令「按 7b spec §5 补一整套纸页 / 页缘 / 页面的 USS 与九宫格素材。
> 另外 play 启动后 console 看到红色信息」+「水印的问题可以用文生图重新做无水印图,
> 不要将就,以效果为优先级考虑问题」。
> **判据面**: 本轮的每条结论都给了**可跑复算命令**与其实跑输出。

---

## 0. 一句话结论

真正的问题不是「纸页样式没写」,而是**整套拟物 UI 的图像层从来没画出来过**:全库 9 条
`url("guid:…")` 在 Unity 6.3 的 USS 导入器里被判 `Invalid URI scheme`,槽位被丢弃。
修好 URL 之后才暴露出第二层问题 —— 页框是画在**纸面图**里的,而 `.paper` 类在全仓
既当**页 / 卡**用、也当**行**用,于是每一行都套上一圈框。本轮把纸面拆成
「无框底纹(`.paper`)」+「页框(`.paper-page`)」,并给 7b 存档册落实页框 / 页缘 / 页脚页码
与一条**几何判据**。

---

## 1. 证据链

### 1.1 `guid:` URI 恒不解析(根因,可跑)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -o "Skeuo[A-Za-z]*\.uss (line [0-9]*): warning: Invalid URI scheme: 'guid'" unity/Logs/dump3-*.log | sort -u
```

实跑输出(2026-11-04 07:17 的 PlayMode 图形跑,`unity/Logs/dump3-071737.log`):

```text
SkeuoFocusVisible.uss (line 52): warning: Invalid URI scheme: 'guid'
SkeuoInk.uss (line 11): warning: Invalid URI scheme: 'guid'
SkeuoInk.uss (line 34): warning: Invalid URI scheme: 'guid'
SkeuoInk.uss (line 39): warning: Invalid URI scheme: 'guid'
SkeuoPaper.uss (line 41): warning: Invalid URI scheme: 'guid'
SkeuoPaper.uss (line 6): warning: Invalid URI scheme: 'guid'
SkeuoScroll.uss (line 7): warning: Invalid URI scheme: 'guid'
SkeuoSeal.uss (line 7): warning: Invalid URI scheme: 'guid'
```

**8 条**(第 9 处 `url("guid:…")` 在 `SkeuoThemeVariables.uss:166` 的**注释**里,不是活引用)。

### 1.2 渲染树:同一个元素,改 URL 形式前后的 `backgroundImage` 真值

判据 = `resolvedStyle.backgroundImage`(引擎解析后的真值,不是源码字符串)。

**改前**(`unity/Logs/dump3-071737.log`,`[TREE]` 行):

```text
slot-list [paper] ... bg=RGBA(0.957, 0.925, 0.847, 1.000) img=d_console.warnicon borderTop=...(1) slice=(64,64,64,64)
(anon) [focus-visible] ... img=d_console.warnicon ... slice=(2,2,2,2)
```

`d_console.warnicon` 是 Unity 内置的**黄色警告图标** —— 被 `-unity-slice-*` 拉伸铺满后
看起来像一张**镶金边的琥珀色卡面**,极易被误判成美术效果(我本人先后误判过两次:
一次当前只是「水印」,一次当它是「焦点环」)。

**改后**(同一 `[TREE]` 打印,`unity/Logs/probe-*.log`):

```text
slot-list [paper] ... img=sprite:border_paper-final ... slice=(64,64,64,64)
```

### 1.3 视觉证据(1920×1080,渲染到 RenderTexture 后 ReadPixels)

走查件 `Tests/PlayMode/SkeuomorphicUI/modal_toggle_visual_walkthrough.cs` 已把装置从
「截 Game 视图」改为「面板渲到 1920×1080 RT 再读像素」—— Game 视图只有 593×429,
用它判「纸页够不够大 / 行会不会压线」会得出与生产无关的结论(实测踩过)。

| 帧 | 修前(07:05) | 修后(07:31) |
|---|---|---|
| `modal_02_saveslots` | 金框 + 黄带压住首行文字,纸面是**拉伸的警告图标** | 真纸纹 + 三层墨线页框,行在框内,页码在页脚 |
| `modal_01_pause` | 金框 + 黄带 | 纯纸底纹卡面(无逐行框),焦点环 = 细铜框 |

### 1.4 水印:9 张在用纹理逐张目视复核

方法:每张取右下 45% 区域放大拼图后**直接看图**(模型自带读图),不是看脚本自述。

| 纹理 | 修前 | 修后 | md5 |
|---|---|---|---|
| `border_paper-final.png` | 有「日日新/sensenova」 | 无 | `4204c15a4b87d22d03864b924d9532f1` |
| `paper_aged-final.png` | 有 | 无 | `71d8bb287610c986aef54fe567337afd` |
| `border_scroll-final.png` | 有 | 无 | `43326cba88c326a651983aace0b18b80` |
| `seal_surface-final.png` | 有 | 无 | `0c879bda4ff3b21d40f9cd5955830dd5` |
| `ink_light-final.png` | 有 | 无 | `0eab032155cd224c6421d6920bcdd7a2` |
| `ink_wet-final.png` | 有 | 无 | `c66092d5b5ac6ee9d9e4e076e1103fb4` |
| `ink_dry-final.png` | 有 | 无 | `9ca963d1282ac15b0c79c0d725f76cf9` |
| `paper_hemp-final.png` | 有(且当时**全树零引用**) | 无 | `78f42a7fa63f196be139f488a0a2ef24` |
| `Brass/focus_brass_2px-final.png` | **本就无**(原样未动) | — | `b005ba2e7fdd4d389d49b55dbd33a9c1` |

复算:`md5sum unity/Assets/Gameplay.UI/Skeuomorphic/Textures/*.png`

**`border_paper` 的几何(供冻结轮重测时对照)**:三道 **3px** 墨线,外缘在
12 / 24 / 36px(占 12–14 / 24–26 / 36–38),最内缘 **38px**;四角 45° 斜切腿长 20px;
四边带沿拉伸方向 std = 0(九宫格拉伸不会产生接缝)。

---

## 2. 改动清单

| 文件 | 改了什么 | 为什么 |
|---|---|---|
| `SkeuoPaper.uss` / `SkeuoInk.uss` / `SkeuoScroll.uss` / `SkeuoSeal.uss` / `SkeuoFocusVisible.uss` | 8 条 `url("guid:…")` → `url("Textures/…")` | `guid:` scheme 被判非法,恒不解析 |
| `SkeuoPaper.uss` | `.paper` 改用**无框底纹** `paper_hemp-final.png`;新增 `.paper-page`(页框图 + 内缩) | 页框画在纸面图里,而 `.paper` 既当页又当行 ⇒ 每行一个框(暂停壳实测不可用) |
| `SkeuoPaper.uss` | `.save-page` 保持「页是列」,内缩由 `.paper-page` 提供 | 内缩对任何页容器都成立,不该只写在存档册 |
| `SkeuoThemeVariables.uss` | 新增 `--skeuo-paper-frame-inset: 44px`(并订正「`url(guid:)` 仅对 background-image 有效」这句已被证伪的注释,原文不删) | 内缩须 ≥ 最内墨线 38px;44 = 38 + 6 光学余量 |
| `Screens/SaveSlotsScreen.cs` | `_slotHost` 增挂 `paper-page` | 只有页/卡级容器挂页框,行不挂 |
| `Editor.Tools.Gates/TextureBindingGates.cs` | `AssetResolves` 增可选 `ussFilePath`,优先按 **USS 文件所在目录**解析相对路径;`ValidateTextureReferencesResolve` 传入它 | 原实现只按仓库根解析 ⇒ 合法的相对路径被判悬空(门的口径与 Unity 不一致) |
| `Tests/EditMode/.../texture_binding_gate_test.cs` · `ink_wet_dry_test.cs` | 两处断言从锚 `guid:…` 改锚相对路径 | 原断言把「不生效的形态」冻成了判据 |
| 8 张纹理 | 程序化重做,无水印 | 用户指令:不要将就 |
| `Tests/PlayMode/.../save_slots_page_geometry_test.cs`(新) | 5 条几何判据 | `TD-skeuo-07` 解除动作 ③ 要求「至少一条像素级或几何级判据」 |
| `Tests/PlayMode/.../modal_toggle_visual_walkthrough.cs` | 截图装置改 RenderTexture 1920×1080 + 树/解析样式打印 | 593×429 的 Game 视图判形态会误判 |

---

## 3. 判据(全部实跑)

**基线三元组** · 截至 `2026-11-03T23:34Z`(本地 2026-11-04 07:34)· 批次 `sprint-24 纸页轮` · commit `663dca68` + 工作树

| 套件 | 结果 | 对照 |
|---|---|---|
| EditMode | `6811 / 6774 通过 / 0 失败 / 37 跳过`(`unity/Logs/editmode-073151.xml`) | 本轮中途 `6811/6769/5/37` —— 5 红全部转绿,总数与跳过数不变 |
| PlayMode | `331 / 327 通过 / 0 失败 / 2 不结论 / 2 跳过`(`unity/Logs/playmode-073453.xml`) | 上轮 `326/322/0/2/2` —— 总数 +5 = 新几何夹具的 5 条,零回归 |

**新几何夹具判什么**(`save_slots_page_geometry_test.cs`,全部用引擎真算的 `worldBound`):

1. 每一行的**父级**是纸页(挂 `paper` 类)—— 旧形态下父亲是屏根,直接红;
2. 纸页 worldBound **包住**每一行(几何包含,不是父级关系的同义反复);
3. 纸页高 **严格大于**行高之和(防「纸塌成一条细带」复发);
4. 页码在纸页内且位于其**下半部**(页脚,不是左上角);
5. 有下一页时右侧页缘才占位、无上一页时左侧页缘零尺寸。
   附正控:行高 > 0、纸页高 > 0(否则包含判据在退化尺寸上恒真)。

---

## 4. 如实登记的残留

1. **本轮只给存档册的页挂了页框**。暂停壳 / 脉案 / 主菜单 / 库存等「页级」容器
   现在仍是**无框纸卡**(它们没挂 `.paper-page`)。要不要统一上框是设计面决定,
   不属本轮擅自扩面 —— 已登记新债(见债册 `TD-skeuo-09`)。
2. **九宫格冻结轮须重开**。`design/assets/specs/nine-slice-freeze-2026-10-08.md` 逐字:
   「任何一张图**重画 / 替换** ⇒ 本轮作废,须重测重冻」。本轮换了 8 张,
   slice / `.meta` / USS 的**值**未改(border_paper 64 / border_scroll 72 / paper_aged 64 /
   seal 8 / ink_* 0),但「重测重冻」这一流程动作未执行 —— 已登记 `TD-skeuo-10`。
3. **`seal_surface` 的篆书刻字被去掉**了(汉字属「文字」硬约束,且它是容器底、印文会压住界面文本)
   ⇒ 现在是**无字印面**。印文缺失要由设计侧另行裁定。
4. **`ink_wet` 的斜向水痕目视偏「数字化条纹」**,手作感不足。子代理的如实结论:
   程序化路线到此为止,再进一步需要手绘或授权素材。数字判据(湿覆盖率 0.9204 > 干 0.4411)通过。
5. **`-unity-slice-*: 64px` 带 `px` 单位会触发导入器警告** `Expected (<integer>) but found '64px'`
   (全库 16 处),但实测解析后的值仍是 64/2,即**功能正常、只是警告**。改它要同步动两个门
   (正则写死了 `(\d+)px`),本轮不动,登记待办。
6. **生产面板是 `ScaleWithScreenSize` + 参考 1920**,故 480px 的模态宽度在 1080p 下只占屏宽 25%。
   这是既有设计值(`--skeuo-shared-modal-width`),本轮未改。
