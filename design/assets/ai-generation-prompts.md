# AI 文生图 Prompt 清单 — 系统 42 拟物 UI 资产

> **用途**：批量生成 42 拟物 UI 所需的贴图/材质资产
> **生成工具**：任意 AI 文生图（Midjourney / Stable Diffusion / DALL-E / SenseNova 等）
> **输出规格**：PNG，512×512 或 1024×1024，透明背景（如支持）
> **存放路径**：`unity/Assets/Gameplay.UI/Skeuomorphic/Textures/`

---

## 一、纸纹贴图（4 张）

### 1. 新宣纸
```
A seamless texture of fresh Chinese rice paper (宣纸), white with subtle fiber texture, slight translucency, traditional calligraphy paper, flat lay, even lighting, no shadows, high detail, 4K
```

### 2. 旧宣纸
```
A seamless texture of aged Chinese rice paper, yellowish-brown tone, visible fiber texture, slight foxing spots and discoloration, antique calligraphy paper, flat lay, even lighting, high detail, 4K
```

### 3. 焦边纸
```
A seamless texture of burnt-edge Chinese paper, dark brown to black gradient edges, charred paper texture, antique scroll effect, flat lay, even lighting, high detail, 4K
```

### 4. 粗麻纸
```
A seamless texture of rough hemp paper, beige tone, coarse fiber texture, visible straw particles, traditional Chinese bookbinding paper, flat lay, even lighting, high detail, 4K
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

## 七、字体文件（2 个）

### 17. 中文字体
```
Download from: https://github.com/googlefonts/noto-cjk/releases
File: NotoSerifCJKsc-Regular.otf
License: SIL Open Font License 1.1
```

### 18. 英文字体
```
Download from: https://fonts.google.com/specimen/Noto+Serif
File: NotoSerif-Regular.ttf
License: SIL Open Font License 1.1
```

---

## 八、Unity 导入设置

### 贴图导入设置
1. 选中所有贴图 → Inspector
2. **Texture Type**: Sprite (2D and UI)
3. **Sprite Mode**: Single
4. **Pixels Per Unit**: 100
5. **Filter Mode**: Bilinear
6. **Compression**: None（保持高质量）
7. **Max Texture Size**: 1024 或 2048
8. **Apply**

### 字体导入设置
1. 选中字体文件 → Inspector
2. **Font Names**: Noto Serif CJK SC, Noto Serif
3. **Rendering Mode**: Smooth
4. **Character**: Dynamic
5. **Include Font Data**: ✅ 勾选
6. **Apply**

---

## 九、验证清单

- [ ] 16 张贴图全部生成并导入 Unity
- [ ] 2 个字体文件全部下载并导入 Unity
- [ ] 所有贴图 Texture Type = Sprite (2D and UI)
- [ ] 所有字体 Rendering Mode = Smooth
- [ ] 运行全量 EditMode 测试无回归
- [ ] 确认 USS 文件中的 `background-image` 引用正确

---

## 十、注意事项

1. **AI 生成物的版权**：中国司法实践倾向保护有独创性投入的 AI 生成物，保留 prompt + 迭代记录作为证据
2. **字体许可证**：Noto 系列字体使用 SIL OFL 1.1 许可证，可免费商用
3. **贴图分辨率**：512×512 足够 UI 使用，1024×1024 更清晰但文件更大
4. **九宫格切片**：Unity 的 Sprite Editor 支持 9-slice 切片，需在 Inspector 中设置 Border 值

---

**生成完成后**：将贴图放入 `unity/Assets/Gameplay.UI/Skeuomorphic/Textures/`，字体放入 `unity/Assets/Gameplay.UI/Skeuomorphic/Fonts/`，然后运行测试验证。
