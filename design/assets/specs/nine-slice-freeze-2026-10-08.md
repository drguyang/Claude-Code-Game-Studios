# 九宫格切图冻结件(五族 · 第一轮)

> **冻结轮次**: round-1 · **2026-10-08**
> **对应**: `story-019` 分档 **019-f**(AC-42-C8)· `story-020` 步①(AC-020-1)
> **依据**: AC-42-C8「slice 值须来自切图冻结件的元数据,**不得手填**」·
> `production/qa/evidence/art-assets-required-for-019-2026-10-05.md` §二①(冻结件规格)
> **范围**: 已入库 16 张 `*-final.png`(五族:纸/墨/卷轴/印章/图标)+ 铜族新出图 1 张 = **17 条**
> + **2026-10-08 同日绑定轮补登记 1 条**(第 18 行 `Casebook/…`,递归覆盖检查抓出的冻结轮盲区漏项;
> 未接线 ⇒ 0 值登记态,非新实测)⇒ 现 **18 条**,`freeze-v1` 块与门已同步。
>
> 🔴 **本件是 `spriteBorder` 与 `-unity-slice-*` 的唯一真源。**
> 改任何冻结值 = **重开冻结轮**(改本件 → 同批改 `.meta` / USS → 复跑 C8 门),
> **禁止单点改**(meta 或 USS 单独改 = 第二真源 = AC-42-C8 违例)。
> 任何一张图**重画 / 替换** ⇒ 本轮作废,须重测重冻。

---

## 一、测量方法(可复算)

程序化实测,非目测。流程:

1. 灰度化;若带 alpha 先合成白底;
2. **背景** = 中心 1/3 区域的中位数;
3. **掩膜** = `|pixel − bg| > 12`;
4. **边带限制**:测上/下边只统计**中 1/3 列**的行计数(≥3 个掩膜像素算有内容),
   测左/右边只统计**中 1/3 行**的列计数 —— 不做边带裁剪会把框的对侧边每行都点亮,四边恒为全图(首轮即踩);
5. **连续内容带**:从边向内取第一段连续带,带内容许 ≤40 行空隙(吸收墨线间隙);
6. **四边延展** = 该带长度。

**锚校验**:`border_paper` 的 USS 注释自称「三道墨线实测 22–57px」;
本法实测墨线主体行段 `22–42`(阈值 12)、含灰边至 `~57`(阈值放宽)—— 两口径差异为**阈值敏感**,
冻结值取 `max(现值, ceil8(两口径))` 覆盖二者(见 §三 R1)。

## 二、逐张冻结表

> 形态:**框** = 有真实九宫格边框(中心空)· **满铺** = 全幅纹理(软边/渐变,无硬边界)·
> **件** = 独立器物贴图 · **表** = 图标表。
> 「冻结 slice」为**四边统一值**(本项目全部 USS 落点均为四边同值)。
> **0 = 明示不走九宫格**(非占位值 —— 承 `PAGES_MAX` 禁占位纪律,此处 0 是登记语义不是空值)。

| # | 文件 | 族 | 形态 | 实测 T/B/L/R | 冻结 slice | USS 落点 | 备注 |
|---|------|----|------|-------------|-----------|----------|------|
| 1 | `border_paper-final.png` | 纸 | **框** | 40 / 39 / 43 / 43 | **64** | `SkeuoPaper.uss` | R1:ceil8(43)=48,现值 64 已过接缝验证(USS 注释甲-B 口径)⇒ `max(64,48)=64` 维持,两口径(43/57)均覆盖 |
| 2 | `paper_aged-final.png` | 纸 | 满铺(软边污渍) | 24 / 314 / 274 / 171 | **64** | `SkeuoPaper.uss`(随 `.paper` 类) | R2:污渍为渐变非硬边界;实际渲染切片 = `.paper` 的 64 ⇒ 冻结 64 |
| 3 | `paper_xuan-final.png` | 纸 | 满铺 | 39 / 762 / 226 / 123 | **0** | — | 未接线(逐变体映射归 019 余项);接线时**重开冻结轮** |
| 4 | `paper_hemp-final.png` | 纸 | 满铺 | 1024 / 1012 / 1024 / 1024 | **0** | — | 同上 |
| 5 | `paper_burnt_edge-final.png` | 纸 | 满铺 | 221 / 506 / 1024 / 1024 | **0** | — | 同上 |
| 6 | `border_scroll-final.png` | 卷轴 | **框** | **66 / 64 / 68 / 69** | **72** | `SkeuoScroll.uss` | 🔴 **本轮查出的现值缺陷**:右框延展 69 > 现值 68 ⇒ 现值切掉右框 1px;R1:ceil8(69)=72 ⇒ 冻结 72,USS 同批 68→72(见 §五) |
| 7 | `scroll_cap-final.png` | 卷轴 | 件 | 1024 ×4 | **0** | — | 未接线;归铜族登记表(2026-10-05 裁定) |
| 8 | `scroll_rod-final.png` | 卷轴 | 件 | 623 / 359 / 1024 / 1024 | **0** | — | 未接线 |
| 9 | `scroll_knot-final.png` | 卷轴 | 件 | 1024 ×4 | **0** | — | 未接线;与 `casebook_paper_stitch` 合并裁定归 casebook 链 |
| 10 | `ink_light-final.png` | 墨 | 渐变底 | 1024 ×4 | **0** | `SkeuoInk.uss` | R2:`SkeuoInk.uss` **明示不加 `-unity-slice-*`**(软渐变底非框,承该文件注释)⇒ 冻结 0,门验「USS 无 slice 行」 |
| 11 | `ink_wet-final.png` | 墨 | 件 | 212 / 53 / 158 / 274 | **0** | — | 未接线 |
| 12 | `ink_dry-final.png` | 墨 | 件 | 1024 ×4 | **0** | — | 未接线 |
| 13 | `ink_dot-final.png` | 墨 | 件 | 116 / 94 / 95 / 89 | **0** | — | 未接线 |
| 14 | `seal_surface-final.png` | 印章 | 满铺(石面) | 1024 ×4 | **8** | `SkeuoSeal.uss` | R2:印面石材质无框边界(中心非空,掩膜法不适用)⇒ 冻结 = 现行渲染切片 8 |
| 15 | `seal_red-final.png` | 印章 | 件 | 1024 ×4 | **0** | — | 未接线 |
| 16 | `ui_icons_sprite-final.png` | 图标 | 表(2048) | 2034 / 2035 / 2039 / 2039 | **0** | — | 2026-10-05 裁定**不走元件库**,交图标系统 ⇒ 不参与元件库九宫格 |
| 17 | `Brass/focus_brass_2px-final.png` | **铜** | 规格出图 | n/a(非实测) | **2** | `SkeuoFocusVisible.uss` | **本冻结轮唯一新图**;值 = art-assets-required §二③ 规格「2px 视觉厚度」,**非边界实测**(规则 R3)。✅ **绑定已落 2026-10-08(同日绑定轮)**:`.focus-visible` 以 `background-image` + slice 2 承载环图,实色 border 宽退役(0)、压痕 box-shadow 移除 —— 单槽风险(带纹理元素获焦纸纹被顶)已**显式登记**于 `SkeuoFocusVisible.uss` 注释与下方 §六,非静默绑 |
| 18 | `Casebook/casebook_paper_base-final.png` | 纸(脉案底) | 满铺底 | n/a(未接线未实测) | **0** | — | ⚠️ **2026-10-08 绑定轮递归覆盖检查抓出的冻结轮漏登记**(原扫 `TopDirectoryOnly` 盲区;2026-10-05 入库 casebook P0 四层)。**未接线**(全库零引用)⇒ 0 = 登记态,同未接线族;**接线时必须重开冻结轮实测**。三项格式手验合规(spriteMode 1 / textureType 8 / alphaIsTransparency 1)但 E1 门顶层面不扫它(见 §六) |

## 三、冻结值取值规则

- **R1 · 框图**:`冻结值 = max(ceil8(实测四边延展最大值), 现行已验证 USS 值)`
  —— 向上取 8 的倍数;且**不小于现值**,防「更准的测量反而缩小已过接缝/渲染验证的 slice」。
- **R2 · 非框(满铺/件/表)**:无硬边界,掩膜延展**不作冻结依据**;
  冻结值 = **现行渲染切片值**(已接线者)或 **0 = 明示不走九宫格**(未接线/裁定不参与者)。
- **R3 · 规格出图(铜族 2px)**:值 = 规格书直接给定,不走实测。

## 四、机器可读块(门唯一解析源)

C8 门(`TextureBindingGates.ValidateSpriteBorderMatchesFreeze`)只解析本块;
格式:`文件(相对 Textures/)|冻结值|USS 文件名 或 -`。

```freeze-v1
border_paper-final.png|64|SkeuoPaper.uss
paper_aged-final.png|64|SkeuoPaper.uss
paper_xuan-final.png|0|-
paper_hemp-final.png|0|-
paper_burnt_edge-final.png|0|-
border_scroll-final.png|72|SkeuoScroll.uss
scroll_cap-final.png|0|-
scroll_rod-final.png|0|-
scroll_knot-final.png|0|-
ink_light-final.png|0|SkeuoInk.uss
ink_wet-final.png|0|-
ink_dry-final.png|0|-
ink_dot-final.png|0|-
seal_surface-final.png|8|SkeuoSeal.uss
seal_red-final.png|0|-
ui_icons_sprite-final.png|0|-
Brass/focus_brass_2px-final.png|2|SkeuoFocusVisible.uss
Casebook/casebook_paper_base-final.png|0|-
```

## 五、本轮发现(冻结轮的存在理由实例)

**`border_scroll` 现值 68 是错的**:右框内容自 `x=955` 起 ⇒ 右延展 = 69 > 68
⇒ 68 会把右框最外 1 列划进拉伸区(缩放时框线被拉细)。实测同时给出
上 66 / 下 64 / 左 68 —— 四边取 max=69,ceil8 ⇒ **72**。
同批改:`SkeuoScroll.uss` 68→72 · `border_scroll` `.meta` `spriteBorder` → 72。
> 这正是「切图早错 = 全局返工」要抓的形态 —— 现值 68 自 019-d 接图起从未被验过。

## 六、局限登记(禁借绿)

- **paper 双口径**:阈值 12 得墨线至 y≈42,注释口径至 57;冻结 64 两者皆覆盖,非二选一。
- **paper 底部稀疏斑点**:`y=941–942 / 971–973` 有 cnt≤24 的零星暗点,落在 64 拉伸区内
  —— 视觉为纸面飞尘级,登记不修;若目视走查判可见,重开冻结轮。
- **铜族 2px 为规格值非实测**(R3),其「明度轴判据」的载体是**文件本身**(读最亮像素),
  与切图边界无关。
- **`spriteSheet.sprites[].border` 仍为 0**:UI Toolkit 九宫格只读 USS,
  不读该字段;日后若走 uGUI Sliced,须 Sprite Editor 重刷(登记,不阻塞)。
- **未接线 11 张的 0 值**是登记态:日后接线**必须重开冻结轮**补测,不得直接抄现值。
- **单槽纸底风险(2026-10-08 绑定轮登记)**:`.focus-visible` 的 `background-image` 是**整槽替换** ——
  焦点目标若自身带纸纹,获焦瞬间纸纹被环图顶掉(环中心透明救不了被顶掉的纹理层)。
  现状运行期施加者不存在(`FocusVisibleStyle.ClassName` 零调用)⇒ 今日零可见风险;
  铁律与出路(乙案环子元素)见 `SkeuoFocusVisible.uss` 注释 —— **不得静默接受纸纹闪烁**。
- **C8 覆盖检查面 = `Textures/` 递归**(2026-10-08 同批修):原 `TopDirectoryOnly` 不含子目录
  ⇒ 第 17 行若被误删,门**不会**报漏冻(登记盲区)。已改递归,子目录新图漏登记即红。
  **改面当场抓出冻结轮自身的一笔漏登记** = 第 18 行 `Casebook/casebook_paper_base-final.png`
  (2026-10-05 入库、2048²、零引用未接线)—— 冻结轮当时只扫顶层 16 张,它落在视野外;
  已按「未接线 ⇒ 0 值登记态」补登记(见 §二 行 18)。
  ⚠️ **E1 格式门仍为顶层 16 张面**(不递归):Brass 的三项格式已手验合规
  (`spriteMode 1 / textureType 8 / alphaIsTransparency 1`)但**门不覆盖** —— 扩面须连动
  `test_ac42e1_covers_all_sixteen`(断言恰 16)与五族计数叙述,归后续轮(登记,不阻塞)。

## 七、复算脚本

见会话日志(2026-10-08);要点即 §一 六步,零依赖(numpy + PIL)。
复算期望:第 1 行 `40/39/43/43`、第 6 行 `66/64/68/69` —— 对不上即说明方法或图变了。

**出图脚本(第 17 行铜环,2026-10-08 绑定轮补存)**:
`unity/Assets/Gameplay.UI/Skeuomorphic/Textures/Brass/generate_focus_brass_2px.py`
—— 与图同目录;`--check` 校验复现(实测**字节级一致**,235 bytes)。依赖 Pillow(清华源安装)。
