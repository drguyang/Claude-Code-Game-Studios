# Asset Spec: 主菜单

> **Tier**: Full Production
> **Category**: UI Screen
> **Source**: hud.md §主菜单, art-bible §2, game-concept.md
> **Art Bible Ref**: §2 菜单/存档位卷宗动画（卷宗/账本形态）; §1 P2「纸面正文对比承诺 ≥7:1」
> **ADR Ref**: ADR-013 §三（UI Toolkit 主栈）; ADR-013 §十（ModalId.MainMenu 闭集—— 待定）

---

## Visual Description

**主菜单**是游戏的第一个画面。形态为**卷宗/账本**——一本摊开的旧纸册页，墨色标题与按钮排列其上，无现代 UI 元素。

- **纸张**: 与脉案同源的宣纸质感，微黄底色，纤维纹理可见。页边有轻微磨损和泛黄。
- **墨迹**: 标题以**楷书**书写，按钮文字以**行楷**书写。墨色浓淡不一。
- **版式**: 竖排从右至左。页边有红色竖线（界行）。中央偏上位置为主视觉区域。
- **按钮**: 拟物铜扣/铜栓形态（黄铜侧），不是现代圆角矩形。按钮有按下态（铜面下凹 + 墨迹加深）。
- **背景**: 暗色旧木桌面或书案，纸页浮于其上。
- **状态痕迹**: 纸面有轻微折痕、边角磨损、泛黄。空白处有淡墨渍。
- **动画**: 卷宗展开 0.4s ease-out（略长于脉案，体量更大）。按钮 hover = 铜面微光；按钮 press = 铜面下凹 + 纸面微震。

---

## Technical Specs

| Property | Value | Notes |
|----------|-------|-------|
| **Resolution** | 1920×1080 (screen fill) | 铺满全屏，纸页居中偏上 |
| **Format** | PNG (sRGB) | 纸面底 + 墨迹分层 + 铜扣元件 |
| **Material slots** | 3 (paper_base + ink_overlay + brass_overlay) | 铜扣独立层，可按主题变量换色 |
| **Poly count** | N/A (UI element) | UI Toolkit UXML + USS |
| **LOD** | 不适用 | UI 始终全分辨率 |
| **Platform variants** | 无 | 全平台同一套纸面 |

---

## Asset Breakdown

| Asset | Type | Description | Slicing | Priority |
|-------|------|-------------|---------|----------|
| `mainmenu_paper_base.png` | Texture | 纯纸面（纤维/泛黄/折痕/墨渍），不含界行 | 9-slice | P0 |
| `mainmenu_ruling.png` | Texture | 红色界行（竖线） | 横向 repeat | P0 |
| `mainmenu_ink_title.png` | Texture Atlas | 标题墨迹（楷书，含浓淡变体） | — | P0 |
| `mainmenu_ink_buttons.png` | Texture Atlas | 按钮文字（行楷，含 hover/press 变体） | — | P0 |
| `mainmenu_brass_button.png` | Texture | 铜扣/铜栓元件（9-slice，承 art-bible §7.9 黄铜侧） | 9-slice | P0 |
| `mainmenu_wear.png` | Texture | 边角磨损 / 折痕细节层 | 9-slice | P1a |

---

## Interaction States

| State | Visual |
|-------|--------|
| **Default** | 卷宗展开，标题居中，按钮排列下方 |
| **Button Hover** | 铜扣微光（黄铜侧 spec §7.9），纸面无变化 |
| **Button Press** | 铜面下凹 + 纸面微震（0.08s），墨迹加深 |
| **Button Disabled** | 铜扣灰化（降低黄铜侧亮度，非透明度），纸面无变化 |
| **Transition In** | 卷宗展开 0.4s ease-out，墨迹渐显 |
| **Transition Out** | 卷宗合拢 0.3s ease-in |

---

## Color Palette (from Art Bible)

| Element | Color | Role |
|---------|-------|------|
| 纸面底 | `#F5F0E8` (warm rice paper) | 主底 |
| 墨迹浓 | `#1A1714` (near-black ink) | 标题 / 按钮文字 |
| 墨迹淡 | `#4A4640` (faded ink) | 辅助文字 / 界行 |
| 界行红 | `#8B3A3A` (seal red) | 竖线界行 |
| 黄铜亮 | `#C8A84E` (polished brass) | 按钮 hover |
| 黄铜暗 | `#8B7332` (aged brass) | 按钮 default / disabled |
| 纸面泛黄 | `#E8DFC8` (aged paper) | 边缘 / 旧纸感 |

> **⚠️ AB-1 待执行**：上述 hex 值须经 URP linear 工作流校准后方可进入代码常量。
> 执行义务归 technical-artist，见 `design/art/art-bible.md` §8.8。

---

## Layout Specification

### Information Hierarchy

| 层级 | 内容 | 位置 |
|------|------|------|
| 1（首要） | 游戏标题（墨迹楷书，大号） | 卷宗顶部居中 |
| 2 | 按钮组（新游戏 / 继续 / 设置 / 退出） | 卷宗中部偏下，纵向排列 |
| 3（环境） | 纸页边角磨损 + 旧木桌面 | 全屏背景 |

### Layout Zones

```
┌─────────────────────────────────────────┐
│  ┌─────────────────────────────────┐    │
│  │     [墨迹标题 · 楷书大号]        │    │  ← 纸面顶区
│  │                                 │    │
│  │    ┌─────────────────────┐      │    │
│  │    │  ■ 新游戏            │      │    │  ← 铜扣按钮
│  │    │  ■ 继续              │      │    │
│  │    │  ■ 设置              │      │    │
│  │    │  ■ 退出              │      │    │
│  │    └─────────────────────┘      │    │
│  │                                 │    │
│  │  [界行竖线 × N]                 │    │
│  └─────────────────────────────────┘    │
│                                         │
│         旧木桌面（暗色背景）              │
└─────────────────────────────────────────┘
```

### Component Inventory

| 元件 | 类型 | 交互 | USS 主题变量 |
|------|------|------|-------------|
| 纸面底 | 9-slice 背景 | 否 | `--paper-base` |
| 界行 | 横向 repeat | 否 | `--ruling-color` |
| 标题墨迹 | Texture Atlas | 否 | — |
| 按钮文字 | Texture Atlas | 否 | — |
| 铜扣按钮 | 9-slice Button | 是（hover/press/disabled） | `--brass-light` / `--brass-dark` / `--brass-disabled` |
| 边角磨损层 | 9-slice overlay | 否 | `--wear-opacity` |

---

## Accessibility

- 纸面正文对比度 ≥ 7:1（AB-4 已裁定）
- 按钮焦点高亮 = 黄铜边框 2px（承 art-bible §3.3 / §7.4 + ADR-013）
- 手柄焦点导航：按钮纵向排列，焦点遍历序 = 自然阅读序（上→下）
- 零现代 UI 元件（无圆角矩形、无渐变、无阴影）

---

## Open Questions

- [ ] 主菜单是否支持背景音乐（art-bible §2 列了「主菜单卷宗音乐」，但本 spec 只管视觉）
- [ ] 新游戏 / 继续 按钮的禁用条件是什么（无存档 = 继续禁用？）
- [ ] 设置子菜单是否走独立场景还是同一场景的模态切换？

---

## Related Files

- `design/ux/hud.md` — 主菜单 HUD 段
- `design/art/art-bible.md` §2 — 菜单视觉方向
- `design/ux/settings-shell-42.md` — 设置界面（主菜单「设置」按钮的目标）
- `design/ux/save-slots-7b.md` — 存档位界面（主菜单「继续」按钮的目标）
