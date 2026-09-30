# AI 文生图 Prompt 清单 — 系统 42 拟物 UI 资产

> **用途**：批量生成拟物 UI 所需的贴图/材质资产
> **生成工具**：任意 AI 文生图（Midjourney / Stable Diffusion / DALL-E / SenseNova 等）
> **输出规格**：PNG，512×512 或 1024×1024，透明背景（如支持）

---

## 一、纸纹贴图（4 张）

### 1. 新宣纸
```
A seamless texture of fresh Chinese rice paper (宣纸), white with subtle fiber texture, slight translucency, traditional calligraphy paper, flat lay view, even lighting, no shadows, high detail, 4K
```

### 2. 旧宣纸
```
A seamless texture of aged Chinese rice paper, yellowish-brown tone, visible fiber texture, slight foxing spots and discoloration, antique calligraphy paper, flat lay view, even lighting, high detail, 4K
```

### 3. 焦边纸
```
A seamless texture of burnt-edge Chinese paper, dark brown to black gradient edges, charred paper texture, antique scroll effect, flat lay view, even lighting, high detail, 4K
```

### 4. 粗麻纸
```
A seamless texture of rough hemp paper, beige tone, coarse fiber texture, visible straw particles, traditional Chinese bookbinding paper, flat lay view, even lighting, high detail, 4K
```

---

## 二、墨迹贴图（4 张）

### 5. 湿墨晕染
```
A single Chinese ink wash stroke, wet ink bleeding effect, black ink on white paper, soft edges, water diffusion, traditional calligraphy, isolated on transparent background, high detail, 4K
```

### 6. 干墨飞白
```
A single Chinese ink brush stroke, dry brush effect, black ink with white streaks, flying white (飞白) texture, traditional calligraphy, isolated on transparent background, high detail, 4K
```

### 7. 淡墨渲染
```
A soft Chinese ink wash gradient, light gray to white, subtle ink diffusion, watercolor effect, traditional painting style, isolated on transparent background, high detail, 4K
```

### 8. 浓墨点
```
A single dense Chinese ink dot, solid black, slight edge bleeding, traditional calligraphy seal effect, isolated on transparent background, high detail, 4K
```

---

## 三、印章贴图（2 张）

### 9. 印泥
```
A square Chinese seal stamp impression, red ink paste (印泥), traditional seal script characters, slightly uneven ink distribution, isolated on white background, high detail, 4K
```

### 10. 印面
```
A square Chinese seal stone surface, carved seal script characters, red ink residue in grooves, traditional chop stamp, top-down view, even lighting, high detail, 4K
```

---

## 四、卷轴配件（3 张）

### 11. 木轴
```
A Chinese scroll wooden rod, dark brown wood, smooth polished surface, traditional scroll handle, horizontal orientation, isolated on transparent background, high detail, 4K
```

### 12. 绳结
```
A Chinese knot cord, red silk rope, traditional decorative knot, scroll binding cord, isolated on transparent background, high detail, 4K
```

### 13. 轴头
```
A Chinese scroll end cap, brass or jade material, ornate carved decoration, traditional scroll fitting, isolated on transparent background, high detail, 4K
```

---

## 五、图标雪碧图（1 张）

### 14. UI 图标集
```
A sprite sheet of 16 traditional Chinese medicine UI icons, 4x4 grid, each icon 128x128 pixels, ink brush style, black on white, icons include: pulse diagnosis, herbal medicine, acupuncture needle, scroll book, seal stamp, mortar and pestle, teapot, yin-yang symbol, dragon, phoenix, turtle, crane, mountain, water, fire, wood, clean vector style, high detail, 4K
```

---

## 六、九宫格边框（2 张）

### 15. 纸面九宫格
```
A 9-slice border frame for Chinese paper UI, thin black ink lines, traditional book page border, corner ornaments, seamless edges, isolated on transparent background, high detail, 4K
```

### 16. 卷轴九宫格
```
A 9-slice border frame for Chinese scroll UI, dark brown wood texture edges, brass corner fittings, traditional scroll border, seamless edges, isolated on transparent background, high detail, 4K
```

---

## 七、生成后检查清单

- [ ] 16 张贴图全部生成
- [ ] 分辨率 ≥ 512×512
- [ ] 背景透明或纯白
- [ ] 无水印、无文字标注
- [ ] 风格统一（水墨/拟物）
- [ ] 文件命名与清单一致

---

## 八、存放路径

```
unity/Assets/Gameplay.UI/Skeuomorphic/Textures/
├── paper_xuan.png
├── paper_aged.png
├── paper_burnt_edge.png
├── paper_hemp.png
├── ink_wet.png
├── ink_dry.png
├── ink_light.png
├── ink_dot.png
├── seal_red.png
├── seal_surface.png
├── scroll_rod.png
├── scroll_knot.png
├── scroll_cap.png
├── ui_icons_sprite.png
├── border_paper.png
└── border_scroll.png
```
