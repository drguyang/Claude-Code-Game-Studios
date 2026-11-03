#!/usr/bin/env python3
"""generate_paper_textures — 拟物 UI 贴图的程序化出图脚本(可复现,2026-11-04)。

为什么程序化出图(不是再一次 AI 文生图):
  2026-09-30 入库的拟物 UI 贴图由 `sensenova-u1.5-fast` 生成,右下角带「日日新 /
  sensenova」生成水印,不能进玩家构建。文生图的水印是**服务端行为**,本地无法承诺
  消除;程序化出图从结构上不可能产生水印 —— 且可逐位复现(重跑脚本即得同一像素)。

本脚本当前产出(与本脚本同目录,文件名固定 —— Unity 靠 GUID 引用,改名会断链):
  · border_paper-final.png   1024×1024 · 米白纸底 + 三道细墨线 + 四角折角斜线(九宫格 64)
  · paper_hemp-final.png     1024×1024 · 无缝麻纤维纸纹(满铺;全树零引用,保留为干净底)
  · paper_aged-final.png     1024×1024 · 旧纸(#e8dcc4 基色 + 茶渍 / 霉点,可平铺;九宫格 64)
  · border_scroll-final.png  1024×1024 · 卷轴边框(深木 + 金线 + 四角回纹;九宫格 72)
  · seal_surface-final.png   1024×1024 · 朱砂印面(无篆字 —— 原图的字与右下角水印一并去掉;九宫格 8)
  · ink_light-final.png      1024×1024 · 淡墨软渐变底(无 slice,拉伸用)
  · ink_wet-final.png        1024×1024 · 湿墨(满幅浓墨 + 洇开纸隙,覆盖率 > 干墨)
  · ink_dry-final.png        1024×1024 · 干墨(斜向飞白笔触 + 纸底,覆盖率 < 湿墨)

色值来源(不新立):
  · 纸底 #f4ecd8 = `SkeuoThemeVariables.uss:20` 的 `--skeuo-paper-bg`
  · 旧纸 #e8dcc4 = 同文件 `--skeuo-paper-bg-aged`
  · 墨色 #2b2416 = 同文件 `--skeuo-paper-rule` 基色;湿墨 #26241f / 干墨 #191714 =
    同文件 `--skeuo-ink-fg-wet` / `--skeuo-ink-fg-dry`
  · 卷轴纸底 #f0e4c8 / 印面 #8b2500 = 同文件 `--skeuo-scroll-bg` / `--skeuo-seal-bg`
  · 画布 1024² = 既有贴图原尺寸(`design/assets/ai-generation-prompts.md` §九)

九宫格几何约束(真源 `design/assets/specs/nine-slice-freeze-2026-10-08.md` 的 freeze-v1):
  · border_paper 64 / border_scroll 72 / paper_aged 64 / seal_surface 8 / ink_* 0。
  · 边框类:最内墨线/金线必须落在 slice 之内(切断 = 缩小时框线被拉细)。
  · 边带必须沿**拉伸方向**逐像素恒定(否则九宫格拉伸会拉出条纹);本脚本用
    `_flatten_bands`(纸类)或「只按到边距离取色」(卷轴)硬保证。
  · 中央区域按自身尺寸做周期噪声(FFT 天然周期)⇒ 中央可无缝平铺。

用法(在本目录运行,依赖 numpy + Pillow):
  python3 generate_paper_textures.py            # 出全部图(幂等;覆盖同目录 png)
  python3 generate_paper_textures.py --check    # 只校验:现有 png 与脚本产出是否像素一致,不写文件
  python3 generate_paper_textures.py --measure  # 出图后打印边带均匀性/接缝/覆盖率自检数字

依赖安装(清华源):
  pip install -i https://pypi.tuna.tsinghua.edu.cn/simple pillow numpy

复现纪律:
  重跑脚本后若 png 像素变化 ⇒ 与入库版漂移。本组图是九宫格切图件,替换/重画会改变
  边缘内容,按冻结件头部纪律「任何一张图重画 / 替换 ⇒ 本轮作废,须重测重冻」。
  本批**只替换图**,`.meta` 的 spriteBorder 与 USS 的 `-unity-slice-*` 均未改。
"""

from __future__ import annotations

import io
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

# ── 画布与切图(来源见模块 docstring)─────────────────────────────────────────
SIZE = 1024          # 1024² = 既有贴图尺寸
SLICE = 64           # border_paper / paper_aged 的九宫格切图值
SLICE_SCROLL = 72    # border_scroll 的九宫格切图值(freeze-v1)
SS = 4               # 超采样倍率:4×4=16 个子样本/输出像素 —— 45° 斜线足够平滑

# ── 色值(权威主题变量,勿手调)──────────────────────────────────────────────
PAPER = (244, 236, 216)       # #f4ecd8 · --skeuo-paper-bg
PAPER_AGED = (232, 220, 196)  # #e8dcc4 · --skeuo-paper-bg-aged
SCROLL_PAPER = (240, 228, 200)  # #f0e4c8 · --skeuo-scroll-bg
INK = (43, 36, 22)            # #2b2416 · --skeuo-paper-rule 基色
INK_WET = (38, 36, 31)        # #26241f · --skeuo-ink-fg-wet
INK_DRY = (25, 23, 20)        # #191714 · --skeuo-ink-fg-dry
SEAL_RED = (139, 37, 0)       # #8b2500 · --skeuo-seal-bg
SCROLL_WOOD = (30, 22, 14)    # 卷轴深木(在 #3a2e1c / #5c4a32 之间的暗端)
SCROLL_GOLD = (198, 160, 84)  # 卷轴金线(与 --skeuo-scroll-border #8c7355 同暖色系、提亮)

# ── 边框三道墨线(几何推导)──────────────────────────────────────────────────
# 原图实测(2026-11-04):旧版白框的暗带落在 22–57px,freeze 文档 §一 记为
# 「墨线主体 22–42、含灰边至 57」。新版改为「纸底到边 + 三道细线」:
#   · 线**外缘**取 12 / 24 / 36 px、线宽 3px;第三道止于 38px,距 64px 切图线还有 25px;
#   · 起点与宽度都取整数像素 ⇒ 直边是硬边、零半覆盖 AA 像素。
#     ⚠️ 这不是排版洁癖:`SAC-021-3` 把纸面贴图按「亮度桶 ≥1% 面积」并入判据,
#     而 50% 覆盖的墨线 AA 边 ≈ 线性亮度 0.27,恰好撞上黄铜焦点环 #B8863B(0.276)
#     ⇒ 全图 2.3% 的半覆盖像素会让那条 BLOCKING 判据转红。硬边把它压到 0。
OUTER_INSETS = (12, 24, 36)
LINE_WIDTH = 3

# 折角斜线:三条线在四角各自以 45° 斜切。
# 约束:最外道外缘 36px + CHAMFER < 64(slice 块边界),否则斜线越出角块、落进可拉伸
# 边带 ⇒ 拉伸时斜线被拉长。取 20 ⇒ 36+20 = 56 < 64,留 8px 余量。
# 内缘斜切腿长再收 2px:使 45° 斜线段垂直厚度 ≈2.8px,与直边 3px 目视等重。
CHAMFER = 20
CHAMFER_INNER_SHRINK = 2

# ── 麻纤维纸参数(视觉标定)──────────────────────────────────────────────────
HEMP_SEED = 20261104
FIBER_COLORS = (
    (206, 187, 153), (193, 169, 132), (178, 151, 112),
    (159, 131, 92), (140, 112, 76), (221, 206, 178),
)
SPECK_COLORS = ((150, 124, 90), (120, 96, 66), (174, 150, 116))
AGED_STAIN = (176, 140, 96)   # 茶渍:偏黄褐,不做成墨渍
AGED_FOXING = (122, 94, 62)   # 霉点


# ══════════════════════════════════════════════════════════════════════
# 公共:可平铺的带限噪声 / 污渍 / 斑点
# ══════════════════════════════════════════════════════════════════════

def _periodic_noise(n: int, rng: np.random.Generator, cutoff: float, amp: float) -> np.ndarray:
    """生成 n×n 的**周期**带限噪声(FFT 频域低通 —— 周期由构造保证)。

    cutoff 为归一化截止频率(1.0 = 奈奎斯特);amp 为目标标准差。
    周期性 ⇒ 该 n×n 场按自身尺寸平铺时左右/上下边界严丝合缝。
    """
    white = rng.normal(0.0, 1.0, (n, n))
    fy = np.fft.fftfreq(n)[:, None]
    fx = np.fft.fftfreq(n)[None, :]
    filt = np.exp(-(fx * fx + fy * fy) / (2.0 * cutoff * cutoff))
    field = np.real(np.fft.ifft2(np.fft.fft2(white) * filt))
    field -= field.mean()
    std = float(field.std())
    if std > 1e-12:
        field *= amp / std
    return field


def _paper_field(n: int, rng: np.random.Generator, amp_low: float, amp_high: float) -> np.ndarray:
    """纸面斑驳 = 低频大块晕染 + 高频细颗粒(两段八度的周期噪声)。"""
    return (_periodic_noise(n, rng, cutoff=0.012, amp=amp_low)
            + _periodic_noise(n, rng, cutoff=0.090, amp=amp_high))


def _periodic_noise_1d(n: int, rng: np.random.Generator, cutoff: float, amp: float) -> np.ndarray:
    """n 点周期一维噪声(用于干墨笔触的方向性条纹)。"""
    white = rng.normal(0.0, 1.0, n)
    f = np.fft.rfftfreq(n)
    filt = np.exp(-(f / cutoff) ** 2)
    field = np.fft.irfft(np.fft.rfft(white) * filt, n)
    field -= field.mean()
    std = float(field.std())
    if std > 1e-12:
        field *= amp / std
    return field


def _sample_1d(field: np.ndarray, coord: np.ndarray) -> np.ndarray:
    """把一维周期场按连续坐标(像素)线性插值采样。"""
    n = field.shape[0]
    idx = np.mod(coord, n)
    i0 = np.floor(idx).astype(np.int64) % n
    i1 = (i0 + 1) % n
    frac = idx - np.floor(idx)
    return field[i0] * (1.0 - frac) + field[i1] * frac


def _blend_blots(rgb: np.ndarray, rng: np.random.Generator, count: int,
                 rmin: float, rmax: float, amin: float, amax: float,
                 color: tuple[int, int, int], power: float = 1.0) -> np.ndarray:
    """周期高斯斑(污渍):叠加后向 color 混合,跨边界不出现断口。"""
    n = rgb.shape[0]
    yy, xx = np.mgrid[0:n, 0:n]
    acc = np.zeros((n, n), dtype=np.float64)
    for _ in range(count):
        cx, cy = rng.uniform(0.0, n, 2)
        r = rng.uniform(rmin, rmax)
        amp = rng.uniform(amin, amax)
        dx = (xx - cx + n / 2.0) % n - n / 2.0
        dy = (yy - cy + n / 2.0) % n - n / 2.0
        acc += amp * np.exp(-((dx * dx + dy * dy) / (r * r)) * power)
    acc = np.clip(acc, 0.0, 1.0)[..., None]
    c = np.asarray(color, dtype=np.float64)
    return rgb * (1.0 - acc) + c * acc


def _blend_specks(rgb: np.ndarray, rng: np.random.Generator, count: int,
                  rmin: float, rmax: float, amin: float, amax: float,
                  color: tuple[int, int, int]) -> np.ndarray:
    """周期细斑点(霉点 / 石孔):小圆点按 3×3 环绕绘制。"""
    n = rgb.shape[0]
    mask = Image.new("L", (n, n), 0)
    d = ImageDraw.Draw(mask)
    for _ in range(count):
        x, y = rng.uniform(0.0, n, 2)
        r = rng.uniform(rmin, rmax)
        a = int(round(rng.uniform(amin, amax) * 255))
        for dy in (-n, 0, n):
            for dx in (-n, 0, n):
                d.ellipse([x + dx - r, y + dy - r, x + dx + r, y + dy + r], fill=a)
    m = np.asarray(mask, dtype=np.float64)[..., None] / 255.0
    c = np.asarray(color, dtype=np.float64)
    return rgb * (1.0 - m) + c * m


# ══════════════════════════════════════════════════════════════════════
# 图一:border_paper-final.png
# ══════════════════════════════════════════════════════════════════════

def _chamfered_rect_points(inset: int, chamfer: int) -> list[tuple[float, float]]:
    """一个「斜切角矩形」的顶点(用于实心多边形填充)。

    逐顶点顺序:上左→上右→右上→右下→下右→下左→左下→左上。
    斜切段连接 (a+c, a) ↔ (a, a+c) 等 —— 三条线各自的斜切段互相平行,
    在角块内形成「折角斜线」。
    """
    a = float(inset)
    b = float(SIZE - inset)
    c = float(chamfer)
    return [
        (a + c, a), (b - c, a),   # 上边
        (b, a + c), (b, b - c),   # 右边
        (b - c, b), (a + c, b),   # 下边
        (a, b - c), (a, a + c),   # 左边
    ]


def _to_supersampled(points: list[tuple[float, float]], max_inset: int) -> list[tuple[float, float]]:
    """把 1024 坐标放大到超采样坐标;修正 PIL 多边形填充的**右/下闭区间**。

    PIL `ImageDraw.polygon` 的扫描线把 x1 / y1 边界也填进去(实测:同一矩形在
    右/下边缘多出 1 个超采样像素)。对 3px 宽的线来说,这会变成「上边 3px 实线、
    下边 0.75/1/1/0.25 的四行」—— 四边不对称,且半覆盖行又把亮度推向 SAC-021-3
    的禁区。故把落在右/下边界的坐标减去 1 个超采样像素,使覆盖恢复成 [min, max)。
    """
    edge = (SIZE - max_inset) * SS
    out: list[tuple[float, float]] = []
    for x, y in points:
        xs, ys = x * SS, y * SS
        if xs == edge:
            xs -= 1.0
        if ys == edge:
            ys -= 1.0
        out.append((xs, ys))
    return out


def _flatten_bands(pixels: np.ndarray, slice_px: int = SLICE) -> np.ndarray:
    """把四条边带压成「沿拉伸方向恒定」,只保留垂直方向的变化。

    九宫格拉伸的是边带:上/下边带沿 x 拉伸,左/右边带沿 y 拉伸。
    只要每行(上/下)或每列(左/右)是常数,拉伸就不会产生条纹。
    这是几何硬保证,不依赖出图时的噪声是否恰好均匀。
    """
    out = pixels
    band = range(slice_px, SIZE - slice_px)
    for y0, y1 in ((0, slice_px), (SIZE - slice_px, SIZE)):
        rows = out[y0:y1, :, :]
        rows[:, band, :] = rows[:, band, :].mean(axis=1, keepdims=True)
    for x0, x1 in ((0, slice_px), (SIZE - slice_px, SIZE)):
        cols = out[:, x0:x1, :]
        cols[band, :, :] = cols[band, :, :].mean(axis=0, keepdims=True)
    return out


def build_border() -> Image.Image:
    """纸底 + 三道斜切角墨线 → RGB 1024²。"""
    rng = np.random.default_rng(HEMP_SEED)

    # 1) 纸底:整幅 #f4ecd8;中央 896² 叠加轻微斑驳(周期 896 ⇒ 中央区可无缝平铺)
    base = np.empty((SIZE, SIZE, 3), dtype=np.float64)
    base[:, :] = PAPER
    inner = SIZE - 2 * SLICE                        # 896
    # 振幅标定:2.0+1.0 目检大块晕染过于显眼(像水渍);1.2+0.7 ⇒ 满幅目视均匀米白。
    mottle = _paper_field(inner, rng, amp_low=1.2, amp_high=0.7)
    base[SLICE:SIZE - SLICE, SLICE:SIZE - SLICE] += mottle[..., None]
    np.clip(base, 0, 255, out=base)

    # 2) 墨线掩膜:超采样画「三道斜切角环」(外多边形填墨、内多边形挖空)。
    #    实心多边形 + 整数像素起点 ⇒ 四条直边是硬边(覆盖恰 0 或 1),
    #    只有 45° 斜切段有 AA —— 斜线段总面积 ≪1%,不构成亮度桶。
    mask = Image.new("L", (SIZE * SS, SIZE * SS), 0)
    draw = ImageDraw.Draw(mask)
    for inset in OUTER_INSETS:
        outer = _to_supersampled(_chamfered_rect_points(inset, CHAMFER), inset)
        inner = _to_supersampled(
            _chamfered_rect_points(inset + LINE_WIDTH, CHAMFER - CHAMFER_INNER_SHRINK),
            inset + LINE_WIDTH)
        draw.polygon(outer, fill=255)
        draw.polygon(inner, fill=0)
    cov = np.asarray(mask.resize((SIZE, SIZE), Image.BOX), dtype=np.float64) / 255.0

    # 3) 合成 + 边带几何硬保证
    ink = np.asarray(INK, dtype=np.float64)[None, None, :]
    out = base * (1.0 - cov[..., None]) + ink * cov[..., None]
    out = _flatten_bands(out)
    return Image.fromarray(np.clip(np.rint(out), 0, 255).astype(np.uint8), "RGB")


# ══════════════════════════════════════════════════════════════════════
# 图二:paper_hemp-final.png
# ══════════════════════════════════════════════════════════════════════

def _fiber_polygon(rng: np.random.Generator, length: float, half_width: float) -> list[tuple[float, float]]:
    """一根两端收尖、略带弯曲的纤维轮廓(1024 坐标系)。"""
    x0 = rng.uniform(0.0, SIZE)
    y0 = rng.uniform(0.0, SIZE)
    ang = rng.uniform(0.0, 2.0 * math.pi)
    bend = rng.uniform(-0.16, 0.16) * length
    steps = 10
    left: list[tuple[float, float]] = []
    right: list[tuple[float, float]] = []
    ca, sa = math.cos(ang), math.sin(ang)
    pa, pb = -sa, ca
    for i in range(steps + 1):
        t = i / steps
        cx = x0 + ca * length * t + pa * bend * math.sin(math.pi * t)
        cy = y0 + sa * length * t + pb * bend * math.sin(math.pi * t)
        hw = max(half_width * (math.sin(math.pi * t) ** 0.35), 0.30)
        left.append((cx + pa * hw, cy + pb * hw))
        right.append((cx - pa * hw, cy - pb * hw))
    return left + right[::-1]


def _wrapped_polygons(poly: list[tuple[float, float]]) -> list[list[tuple[float, float]]]:
    """把单根纤维按 3×3 环绕复制 ⇒ 跨边界时从对边接续 ⇒ 整图严格周期。"""
    out = []
    for dy in (-SIZE, 0, SIZE):
        for dx in (-SIZE, 0, SIZE):
            out.append([((x + dx) * SS, (y + dy) * SS) for x, y in poly])
    return out


def _build_hemp_shapes(rng: np.random.Generator):
    """生成全部纤维/斑点(按透明度升序 ⇒ 深色强势笔画最后落,叠压时不被浅色盖掉)。"""
    shapes: list[tuple[float, tuple[int, int, int], str, float, float]] = []
    # 短纤维 700 根:长度 6–40px,半宽 0.4–0.8px
    for _ in range(700):
        shapes.append((rng.uniform(0.14, 0.52), FIBER_COLORS[rng.integers(0, 4)],
                       "fiber", rng.uniform(6.0, 40.0), rng.uniform(0.40, 0.80)))
    # 中长纤维 150 根:长度 40–110px
    for _ in range(150):
        shapes.append((rng.uniform(0.20, 0.55), FIBER_COLORS[rng.integers(0, 5)],
                       "fiber", rng.uniform(40.0, 110.0), rng.uniform(0.60, 1.10)))
    # 长而淡的底纹纤维 40 根
    for _ in range(40):
        shapes.append((rng.uniform(0.07, 0.18), FIBER_COLORS[rng.integers(0, 6)],
                       "fiber", rng.uniform(120.0, 260.0), rng.uniform(0.5, 0.9)))
    # 深色短纤维 200 根(补足麻纸的深色头)
    for _ in range(200):
        shapes.append((rng.uniform(0.30, 0.60), FIBER_COLORS[3 + rng.integers(0, 2)],
                       "fiber", rng.uniform(8.0, 55.0), rng.uniform(0.35, 0.75)))
    # 细斑点 2200 个:半径 0.5–1.4px
    for _ in range(2200):
        shapes.append((rng.uniform(0.10, 0.48), SPECK_COLORS[rng.integers(0, 3)],
                       "speck", rng.uniform(0.5, 1.4), 0.0))
    shapes.sort(key=lambda s: s[0])
    return shapes


def build_hemp() -> Image.Image:
    """暖米色麻纤维纸 → RGB 1024²,按 1024 周期严格无缝。"""
    rng = np.random.default_rng(HEMP_SEED)
    base = np.empty((SIZE, SIZE, 3), dtype=np.float64)
    base[:, :] = PAPER
    base += _paper_field(SIZE, rng, amp_low=3.2, amp_high=1.4)[..., None]
    np.clip(base, 0, 255, out=base)

    big = Image.fromarray(np.rint(base).astype(np.uint8), "RGB").resize(
        (SIZE * SS, SIZE * SS), Image.BICUBIC)
    base_big = np.asarray(big, dtype=np.float64)

    mask = Image.new("L", (SIZE * SS, SIZE * SS), 0)
    color = Image.new("RGB", (SIZE * SS, SIZE * SS), PAPER)
    dm = ImageDraw.Draw(mask)
    dc = ImageDraw.Draw(color)

    for alpha, rgb, kind, l, w in _build_hemp_shapes(rng):
        if kind == "fiber":
            poly = _fiber_polygon(rng, l, w)
            for pts in _wrapped_polygons(poly):
                dm.polygon(pts, fill=int(round(alpha * 255)))
                dc.polygon(pts, fill=rgb)
        else:
            cx, cy = rng.uniform(0.0, SIZE), rng.uniform(0.0, SIZE)
            r = l
            for dy in (-SIZE, 0, SIZE):
                for dx in (-SIZE, 0, SIZE):
                    x, y = (cx + dx) * SS, (cy + dy) * SS
                    dm.ellipse([x - r * SS, y - r * SS, x + r * SS, y + r * SS],
                               fill=int(round(alpha * 255)))
                    dc.ellipse([x - r * SS, y - r * SS, x + r * SS, y + r * SS], fill=rgb)

    cov = np.asarray(mask, dtype=np.float64)[..., None] / 255.0
    tint = np.asarray(color, dtype=np.float64)
    out_big = base_big * (1.0 - cov) + tint * cov

    out = np.asarray(
        Image.fromarray(np.clip(np.rint(out_big), 0, 255).astype(np.uint8), "RGB")
        .resize((SIZE, SIZE), Image.BOX), dtype=np.float64)
    return Image.fromarray(np.clip(np.rint(out), 0, 255).astype(np.uint8), "RGB")


# ══════════════════════════════════════════════════════════════════════
# 图三:paper_aged-final.png
# ══════════════════════════════════════════════════════════════════════

def build_paper_aged() -> Image.Image:
    """旧纸: #e8dcc4 基色 + 低频黄化 + 茶渍 + 霉点,可平铺;九宫格 64。

    ⚠️ 本图**不做 `_flatten_bands`**。首版做过,结果 64px 处出现一条肉眼可见的
    矩形分界(边带被压平、中心仍有斑驳)。旧纸的角色是「软边污渍满铺」
    (freeze 文档行 2 的 R2 裁定),正确做法是**整幅严格周期、低对比**:
    平铺无缝,九宫格拉伸低对比斑驳也不会拉出条纹。
    """
    rng = np.random.default_rng(HEMP_SEED + 1)

    # 1) 基色 + 低频黄化(整体向暖色漂移,幅度小、不做成脏纸)
    base = np.empty((SIZE, SIZE, 3), dtype=np.float64)
    base[:, :] = PAPER_AGED
    base += _paper_field(SIZE, rng, amp_low=3.0, amp_high=1.2)[..., None]
    tint = np.clip(_periodic_noise(SIZE, rng, 0.020, 1.0) * 0.5 + 0.5, 0.0, 1.0)
    warm = tint[..., None] * 0.20
    base = base * (1.0 - warm) + np.asarray((214, 198, 168), dtype=np.float64) * warm

    # 2) 茶渍(大而淡)+ 霉点(小而深);两者都按周期环绕 ⇒ 平铺无断口
    base = _blend_blots(base, rng, count=18, rmin=40.0, rmax=150.0,
                        amin=0.03, amax=0.10, color=AGED_STAIN)
    base = _blend_specks(base, rng, count=760, rmin=0.6, rmax=2.2,
                         amin=0.10, amax=0.36, color=AGED_FOXING)

    # 3) 细纸纹颗粒
    base += _periodic_noise(SIZE, rng, 0.30, 0.8)[..., None]
    np.clip(base, 0, 255, out=base)
    return Image.fromarray(np.clip(np.rint(base), 0, 255).astype(np.uint8), "RGB")


# ══════════════════════════════════════════════════════════════════════
# 图四:border_scroll-final.png
# ══════════════════════════════════════════════════════════════════════

def _scroll_profile() -> np.ndarray:
    """72px 卷轴边带的「按到边距离」颜色剖面(含三道金线)。

    只按到边距离取色 ⇒ 上/下边带沿 x 恒定、左/右边带沿 y 恒定,
    九宫格拉伸不会产生条纹;剖面里的金线也因此保持直线。
    """
    prof = np.zeros((SLICE_SCROLL, 3), dtype=np.float64)
    for k in range(SLICE_SCROLL):
        t = k / (SLICE_SCROLL - 1)
        prof[k] = (SCROLL_WOOD[0] + 10 * t,
                   SCROLL_WOOD[1] + 7 * t,
                   SCROLL_WOOD[2] + 4 * t)
    # 木纹:几道更暗的平行细线(仍是距离函数 ⇒ 不随拉伸变形)
    for k in (14, 15, 18, 40, 41, 44, 55, 56):
        prof[k] *= 0.78
    # 三道金线:3–6 / 28–31 / 62–66(最内 66 < 72,不会被切图线切断)
    for a, b in ((3, 7), (28, 32), (62, 67)):
        prof[a:b] = SCROLL_GOLD
    return prof


def build_border_scroll() -> Image.Image:
    """深木 + 三道金线 + 四角回纹的卷轴边框;九宫格 72。"""
    rng = np.random.default_rng(HEMP_SEED + 2)
    n = SIZE

    # 1) 中央纸底(周期 880 = 1024-2*72 ⇒ 中央区可无缝平铺)
    img = np.empty((n, n, 3), dtype=np.float64)
    img[:, :] = SCROLL_PAPER
    inner_n = n - 2 * SLICE_SCROLL
    img[SLICE_SCROLL:n - SLICE_SCROLL, SLICE_SCROLL:n - SLICE_SCROLL] += \
        _paper_field(inner_n, rng, amp_low=2.0, amp_high=0.8)[..., None]

    # 2) 四边带:只按「到最近边的距离」取色
    yy, xx = np.mgrid[0:n, 0:n]
    dist = np.minimum(np.minimum(xx, yy), np.minimum(n - 1 - xx, n - 1 - yy))
    prof = _scroll_profile()
    band = dist < SLICE_SCROLL
    img[band] = prof[np.clip(dist[band], 0, SLICE_SCROLL - 1)]
    img = np.clip(img, 0, 255, out=img).astype(np.uint8)

    im = Image.fromarray(img, "RGB")
    d = ImageDraw.Draw(im)

    # 3) 四角构件:64×64 暗块 + 金框 + 回纹(全部落在 72px 角块内,镜像对称)
    meander = [
        (0, 0), (46, 0), (46, 46), (0, 46), (0, 8), (38, 8),
        (38, 38), (8, 38), (8, 16), (30, 16), (30, 30), (16, 30),
        (16, 24), (22, 24),
    ]
    for bx, by in ((4, 4), (n - 68, 4), (4, n - 68), (n - 68, n - 68)):
        mirror_x = bx > n / 2
        mirror_y = by > n / 2
        d.rectangle([bx, by, bx + 63, by + 63], fill=(36, 26, 16))
        d.rectangle([bx + 4, by + 4, bx + 63 - 4, by + 63 - 4],
                    outline=SCROLL_GOLD, width=3)
        pts = []
        for px, py in meander:
            # 块内局部坐标 = 12+px(0..46 ⇒ 12..58);镜像后 = 63 − 局部(块宽 64)
            lx = 12 + px
            ly = 12 + py
            if mirror_x:
                lx = 63 - lx
            if mirror_y:
                ly = 63 - ly
            pts.append((bx + lx, by + ly))
        d.line(pts, fill=SCROLL_GOLD, width=4, joint="curve")

    return im


# ══════════════════════════════════════════════════════════════════════
# 图五:seal_surface-final.png
# ══════════════════════════════════════════════════════════════════════

def build_seal_surface() -> Image.Image:
    """朱砂印面: #8b2500 基色 + 低对比斑驳 + 细石孔(无篆字),可平铺;九宫格 8。

    首版把明暗振幅开到 0.34、暖橙加得过多,目检像铁锈而非印泥;收窄到 0.18,
    高光只提一点点 —— 它只是容器底,印文(奶油色)必须压得住。
    """
    rng = np.random.default_rng(HEMP_SEED + 3)
    n = SIZE

    low = _periodic_noise(n, rng, 0.008, 1.0)
    mid = _periodic_noise(n, rng, 0.050, 1.0)
    t = np.tanh((0.65 * low + 0.35 * mid) * 1.1)   # -1..1,平滑

    base = np.asarray(SEAL_RED, dtype=np.float64)
    lum = 1.0 + 0.10 * t
    rgb = base[None, None, :] * lum[..., None]
    warm = np.clip(t, 0.0, 1.0)
    rgb[..., 0] += warm * 3.0
    rgb[..., 1] += warm * 5.0
    rgb[..., 2] += warm * 3.0

    # 朱砂颗粒 + 石孔(细、低对比 —— 它是容器底,不能抢印文)
    rgb += rng.normal(0.0, 0.9, (n, n, 1))
    rgb = _blend_specks(rgb, rng, 420, 0.5, 1.5, 0.04, 0.14, (70, 18, 0))
    rgb = _blend_specks(rgb, rng, 100, 0.4, 1.0, 0.03, 0.06, (190, 82, 44))
    np.clip(rgb, 0, 255, out=rgb)
    return Image.fromarray(np.rint(rgb).astype(np.uint8), "RGB")


# ══════════════════════════════════════════════════════════════════════
# 图六:ink_light-final.png
# ══════════════════════════════════════════════════════════════════════

def build_ink_light() -> Image.Image:
    """淡墨软渐变底:中心淡墨、边缘化入纸色;无 slice(整幅拉伸,软边不露接缝)。"""
    rng = np.random.default_rng(HEMP_SEED + 4)
    n = SIZE
    yy, xx = np.mgrid[0:n, 0:n]

    # 略偏心的椭圆晕染(手作感;偏心量 <10% 不破坏构图)
    cx, cy = 0.46 * n, 0.52 * n
    dx = (xx - cx) / (0.62 * n)
    dy = (yy - cy) / (0.58 * n)
    r = np.sqrt(dx * dx + dy * dy)
    r *= 1.0 + _periodic_noise(n, rng, 0.020, 0.06)     # 边缘不规则
    density = np.clip(1.0 - r, 0.0, 1.0) ** 1.6
    density *= 1.0 + _periodic_noise(n, rng, 0.120, 0.06)
    density = np.clip(density, 0.0, 0.92)

    # 墨色取「淡墨灰」而非浓墨:`.ink` 承载正文,中心 L 必须留出可读对比
    ink_gray = np.asarray((120, 116, 108), dtype=np.float64)
    paper = np.asarray(PAPER, dtype=np.float64)
    rgb = paper[None, None, :] * (1.0 - density[..., None]) + ink_gray * density[..., None]

    # 极细的墨粒(不做成噪点墙;只在晕染区内可见)
    grain = _periodic_noise(n, rng, 0.25, 1.0) * density
    rgb += grain[..., None] * 0.8
    np.clip(rgb, 0, 255, out=rgb)
    return Image.fromarray(np.rint(rgb).astype(np.uint8), "RGB")


# ══════════════════════════════════════════════════════════════════════
# 图七 / 八:ink_wet-final.png · ink_dry-final.png
# ══════════════════════════════════════════════════════════════════════

def build_ink_wet() -> Image.Image:
    """湿墨:一张水彩式浓墨晕染(中心浓、边缘化入纸),覆盖率 > 干墨。

    ⚠️ 三版迭代的教训:
      1) 高频场进判定 ⇒ 胡椒盐噪点;
      2) 低频场做**硬阈值** ⇒ 墨团碎成迷彩斑块(阈值天然是二值的);
      3) 补一个大纸窗 ⇒ 像「擦除印」。
    正解是**密度场,不是掩膜**:径向衰减 × 低频浓淡,全程 0..1 连续;
    湿墨的语义是「浓淡渐变 + 边缘洇开」,不是「有墨/无墨」的二值图。
    """
    rng = np.random.default_rng(HEMP_SEED + 5)
    n = SIZE
    yy, xx = np.mgrid[0:n, 0:n]

    # 笔势坐标(与干墨同一角度,便于两态对拍)
    ang = math.radians(-33.0)
    ca, sa = math.cos(ang), math.sin(ang)
    u = xx * ca + yy * sa
    v = -xx * sa + yy * ca

    # 1) 径向浓淡:中心 1;r<0.8 保持近乎全浓,r>1.1 化入纸(边缘不规则)
    dx = (xx - 0.50 * n) / (0.62 * n)
    dy = (yy - 0.52 * n) / (0.58 * n)
    r = np.sqrt(dx * dx + dy * dy)
    r *= 1.0 + _periodic_noise(n, rng, 0.012, 0.07)
    density = np.clip((1.14 - r) * 3.2, 0.0, 1.0)

    # 2) 低频浓淡起伏(水彩的沉淀/稀释;幅度小,墨面仍是「浓」)
    density *= 0.97 + 0.03 * _periodic_noise(n, rng, 0.008, 1.0)
    # 水痕:过渡带略深一圈(干后留下的边)
    density = np.clip(density + 0.10 * density * (1.0 - density) * 4.0, 0.0, 1.0)

    # 3) 几道湿笔的纸色拖痕(沿 v 的宽而淡的条,原图的「飞白」语义;不冲淡主墨)
    streak = _sample_1d(_periodic_noise_1d(n, rng, 0.014, 1.0), v)
    density = np.clip(density - np.clip((streak - 0.62) * 0.8, 0.0, 0.22), 0.0, 1.0)

    paper = np.asarray(PAPER, dtype=np.float64)
    ink = np.asarray(INK_WET, dtype=np.float64)
    shade = 1.0 + 0.10 * _periodic_noise(n, rng, 0.020, 1.0)
    ink_field = np.clip(ink[None, None, :] * shade[..., None], 0, 255)
    rgb = paper[None, None, :] * (1.0 - density[..., None]) + ink_field * density[..., None]
    np.clip(rgb, 0, 255, out=rgb)
    return Image.fromarray(np.rint(rgb).astype(np.uint8), "RGB")


def build_ink_dry() -> Image.Image:
    """干墨:一条斜向宽笔触 + 副笔触 + 宽飞白(覆盖率 < 湿墨;不透明 RGB)。

    ⚠️ 前两版的失败形态:① 全场阈值 = 条形码噪声;② 飞白取高频一维场 = 满屏
    细线。干墨的可读形态是**一笔扫过**:宽笔道 + 少量宽飞白 + 不齐的边缘,
    笔道外应保持干净的纸底(不允许残留灰噪)。
    """
    rng = np.random.default_rng(HEMP_SEED + 6)
    n = SIZE
    yy, xx = np.mgrid[0:n, 0:n]

    # 沿 -33° 的笔势:u = 沿笔方向,v = 垂直笔方向
    ang = math.radians(-33.0)
    ca, sa = math.cos(ang), math.sin(ang)
    u = xx * ca + yy * sa
    v = -xx * sa + yy * ca

    # 笔道边界被低频噪声扭曲(不是直线带)
    warp = _periodic_noise(n, rng, 0.010, 1.0) * 0.04 * n
    vc = 0.50 * n

    def band(center: float, half: float, soft: float, strength: float) -> np.ndarray:
        d = np.abs((v - center) + warp)
        return strength / (1.0 + np.exp((d - half) / soft))

    # 主笔道(宽、重)+ 副笔道(窄、轻,错开约 0.66n);带宽收窄 ⇒ 两侧留出干净纸
    body = band(vc, 0.37 * n, 0.018 * n, 1.0)
    body = np.clip(body + band(vc + 0.66 * n, 0.10 * n, 0.012 * n, 0.60), 0.0, 1.0)

    # 笔头笔尾收干:沿 u 的两端变淡
    tip = 1.0 / (1.0 + np.exp((np.abs(u - 0.5 * n) - 0.48 * n) / (0.030 * n)))
    body *= 0.72 + 0.28 * tip
    # 硬边化:纸底必须真的是纸(否则整幅蒙一层灰、像噪点墙)
    body = np.clip((body - 0.45) * 6.0, 0.0, 1.0)

    # 飞白:沿笔势**拉长**的纸隙(是 v 的函数 —— 取低频 ⇒ 少数几条宽白线)
    streaks = _sample_1d(_periodic_noise_1d(n, rng, 0.012, 1.0), v)
    gaps = np.clip((streaks - 0.42) * 1.1, 0.0, 0.70)
    m = body * (1.0 - gaps)

    # 笔毛浓淡:同样沿 v 变化(笔毛是并排的,不该沿 u 断续)
    bristle = _sample_1d(_periodic_noise_1d(n, rng, 0.050, 1.0), v)
    m *= np.clip(0.82 + 0.22 * bristle, 0.55, 1.0)

    # 边缘碎屑:低频扰动边界
    m *= np.clip(0.86 + 0.18 * _periodic_noise(n, rng, 0.055, 1.0), 0.55, 1.0)
    m[m < 0.05] = 0.0

    paper = np.asarray(PAPER, dtype=np.float64)
    ink = np.asarray(INK_DRY, dtype=np.float64)
    shade = 1.0 + 0.06 * _periodic_noise(n, rng, 0.040, 1.0)
    ink_field = np.clip(ink[None, None, :] * shade[..., None], 0, 255)
    rgb = paper[None, None, :] * (1.0 - m[..., None]) + ink_field * m[..., None]

    # 笔道外的零星墨点(飞白溅点)
    rgb = _blend_specks(rgb, rng, count=140, rmin=0.5, rmax=1.3,
                        amin=0.20, amax=0.55, color=INK_DRY)
    np.clip(rgb, 0, 255, out=rgb)
    return Image.fromarray(np.rint(rgb).astype(np.uint8), "RGB")


# ══════════════════════════════════════════════════════════════════════
# 自检数字
# ══════════════════════════════════════════════════════════════════════

def _linear_luminance(rgb: np.ndarray) -> np.ndarray:
    c = rgb.astype(np.float64) / 255.0
    c = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    return 0.2126 * c[..., 0] + 0.7152 * c[..., 1] + 0.0722 * c[..., 2]


def _seam_metrics(a: np.ndarray) -> str:
    seam_lr = np.abs(a[:, 0].astype(float) - a[:, -1].astype(float)).mean()
    seam_tb = np.abs(a[0, :].astype(float) - a[-1, :].astype(float)).mean()
    nb_lr = np.abs(np.diff(a.astype(float), axis=1)).mean()
    nb_tb = np.abs(np.diff(a.astype(float), axis=0)).mean()
    return (f"LR 首末={seam_lr:.3f}(邻列 {nb_lr:.3f}) · "
            f"TB 首末={seam_tb:.3f}(邻行 {nb_tb:.3f})")


def measure_border(img: Image.Image) -> None:
    a = np.asarray(img.convert("L"), dtype=np.float64)
    mid = slice(SLICE, SIZE - SLICE)
    print("[border_paper] 边带沿**拉伸方向** std(应为 0):")
    for name, band, axis in (
        ("上", a[:SLICE, mid], 1), ("下", a[SIZE - SLICE:, mid], 1),
        ("左", a[mid, :SLICE], 0), ("右", a[mid, SIZE - SLICE:], 0),
    ):
        s = band.std(axis=axis)
        print(f"   {name}: max={s.max():.4f} mean={s.mean():.4f} "
              f"(带内 2D std={band.std():.2f},含墨线结构)")
    c = a[SLICE:SIZE - SLICE, SLICE:SIZE - SLICE]
    print(f"[border_paper] 中央 896² 接缝: {_seam_metrics(c)}")
    print(f"   纸底中位数={np.median(c[300:600, 300:600]):.0f} (目标 #f4ecd8≈236)")


def measure_hemp(img: Image.Image) -> None:
    a = np.asarray(img.convert("RGB"))
    g = a.mean(axis=2)
    print(f"[paper_hemp] mean={g.mean():.2f} median={np.median(g):.1f} """
          f"std={g.std():.2f} p5/p95={np.percentile(g, 5):.1f}/{np.percentile(g, 95):.1f}")
    print(f"   无缝: {_seam_metrics(a)}; 中位色={np.median(a.reshape(-1, 3), axis=0)}")


def measure_paper_aged(img: Image.Image) -> None:
    a = np.asarray(img.convert("RGB"))
    g = a.mean(axis=2)
    print(f"[paper_aged] mean={g.mean():.2f} median={np.median(g):.1f} std={g.std():.2f};"
          f" 中位色={np.median(a.reshape(-1, 3), axis=0)} (目标 #e8dcc4=(232,220,196))")
    print(f"   无缝: {_seam_metrics(a)}")
    b = a.astype(np.float64)
    mid = slice(SLICE, SIZE - SLICE)
    for name, band, axis in (("上", b[:SLICE, mid], 1), ("下", b[SIZE - SLICE:, mid], 1),
                             ("左", b[mid, :SLICE], 0), ("右", b[mid, SIZE - SLICE:], 0)):
        s = band.std(axis=axis)
        print(f"   边带{name}沿拉伸方向 std max={s.max():.4f}")


def measure_border_scroll(img: Image.Image) -> None:
    a = np.asarray(img.convert("RGB"))
    g = np.asarray(img.convert("L"))
    mid = slice(SLICE_SCROLL, SIZE - SLICE_SCROLL)
    print("[border_scroll] 边带沿拉伸方向 std(应为 0):")
    for name, band, axis in (
        ("上", g[:SLICE_SCROLL, mid].astype(float), 1),
        ("下", g[SIZE - SLICE_SCROLL:, mid].astype(float), 1),
        ("左", g[mid, :SLICE_SCROLL].astype(float), 0),
        ("右", g[mid, SIZE - SLICE_SCROLL:].astype(float), 0),
    ):
        s = band.std(axis=axis)
        print(f"   {name}: max={s.max():.4f} mean={s.mean():.4f}")
    # 中央区(72..952)不得有任何边框内容:边框/金线的线性亮度远低于纸面
    c = a[mid, mid]
    cl = _linear_luminance(c)
    print(f"[border_scroll] 中央 {SIZE - 2 * SLICE_SCROLL}² 最低 L={cl.min():.3f} "
          f"(纸面≈0.78;出现 <0.5 即边框侵入切图线内)")
    print(f"   中央接缝: {_seam_metrics(c)}; 中位色={np.median(c.reshape(-1, 3), axis=0)}")


def measure_seal(img: Image.Image) -> None:
    a = np.asarray(img.convert("RGB"))
    print(f"[seal_surface] 中位色={np.median(a.reshape(-1, 3), axis=0)} "
          f"(目标 #8b2500=(139,37,0)); 无缝: {_seam_metrics(a)}")


def measure_ink_light(img: Image.Image) -> None:
    a = np.asarray(img.convert("RGB"))
    L = _linear_luminance(a)
    g = a.mean(axis=2)
    print(f"[ink_light] mean={g.mean():.1f} 中心 L={L[SIZE // 2 - 64:SIZE // 2 + 64, SIZE // 2 - 64:SIZE // 2 + 64].mean():.3f} "
          f"角落 L={np.mean([L[:8, :8].mean(), L[:8, -8:].mean(), L[-8:, :8].mean(), L[-8:, -8:].mean()]):.3f}")


def measure_ink(img: Image.Image, name: str) -> None:
    a = np.asarray(img.convert("RGB"))
    L = _linear_luminance(a)
    print(f"[{name}] 墨像素覆盖率(L<0.2)={(L < 0.2).mean():.3f} · "
          f"Lmean={L.mean():.3f} · 尺寸={a.shape[1]}×{a.shape[0]} · "
          f"全不透明={bool((np.asarray(img)[..., 3] == 255).all()) if img.mode == 'RGBA' else True}")


# ══════════════════════════════════════════════════════════════════════
# 入口
# ══════════════════════════════════════════════════════════════════════

def _encode(img: Image.Image) -> bytes:
    buf = io.BytesIO()
    img.save(buf, "PNG")          # 不传 pnginfo ⇒ 不写任何文本块(tEXt/iTXt/zTXt)
    return buf.getvalue()


def main() -> int:
    args = sys.argv[1:]
    check = "--check" in args
    measure = "--measure" in args or check
    here = Path(__file__).resolve().parent

    targets = (
        ("border_paper-final.png", build_border, measure_border),
        ("paper_hemp-final.png", build_hemp, measure_hemp),
        ("paper_aged-final.png", build_paper_aged, measure_paper_aged),
        ("border_scroll-final.png", build_border_scroll, measure_border_scroll),
        ("seal_surface-final.png", build_seal_surface, measure_seal),
        ("ink_light-final.png", build_ink_light, measure_ink_light),
        ("ink_wet-final.png", build_ink_wet, lambda im: measure_ink(im, "ink_wet")),
        ("ink_dry-final.png", build_ink_dry, lambda im: measure_ink(im, "ink_dry")),
    )

    failed = 0
    for name, build, measure_fn in targets:
        path = here / name
        img = build()

        if check:
            if not path.exists():
                print(f"FAIL:{name} 不存在")
                failed += 1
                continue
            have = Image.open(path).convert("RGB")
            same = have.size == img.size and have.tobytes() == img.tobytes()
            raw_same = path.read_bytes() == _encode(img)
            if same:
                tag = "字节级一致" if raw_same else "像素级一致(字节差 = 压缩器参数)"
                print(f"OK:{name} 与脚本产出一致({tag})")
                if measure:
                    measure_fn(img)
            else:
                print(f"FAIL:{name} 与脚本产出**像素不一致** —— 图已漂移,须复核冻结轮")
                failed += 1
        else:
            path.write_bytes(_encode(img))
            print(f"wrote:{path}({path.stat().st_size} bytes, {img.size[0]}×{img.size[1]} {img.mode})")
            if measure_fn:
                measure_fn(img)

    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
