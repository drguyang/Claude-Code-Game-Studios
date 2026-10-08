#!/usr/bin/env python3
"""generate_focus_brass_2px — 黄铜 2px 焦点环图出图脚本(可复现,2026-10-08)。

图形态:64×64 RGBA · 外缘 2px 环 = #B8863B 不透明 · 中心 60×60 全透明。
       (九宫格 slice=2 时四角与环带原样保留、中心拉伸 —— 元素自身纸底经透明中心透出。)

规格真源(三个参数全由规格钉死,故为程序化出图,冻结件登记为 **R3 规格出图**):
  · 环厚 2px = `production/qa/evidence/art-assets-required-for-019-2026-10-05.md` §二③
    「2px 视觉厚度」(M2 硬前置,`milestones:139`)
  · 色值 #B8863B = `design/art-bible.md` §4.1 黄铜权威值(184,134,59)
  · 冻结登记 = `design/assets/specs/nine-slice-freeze-2026-10-08.md` §二 行 17 +
    §四 `freeze-v1`:`Brass/focus_brass_2px-final.png|2|SkeuoFocusVisible.uss`

用法(在本目录运行):
  python3 generate_focus_brass_2px.py           # 出图(幂等;覆盖同目录 png)
  python3 generate_focus_brass_2px.py --check   # 只校验:现有 png 与脚本产出是否一致,不写文件

依赖:python3 + Pillow。
  安装(清华源):pip install -i https://pypi.tuna.tsinghua.edu.cn/simple pillow

复现纪律:重跑脚本后若 png 字节变化 ⇒ 与入库版漂移,须复跑 C8/C11 门
(`EditMode` 过滤 `DaYiJingCheng.Tests.Unit.SkeuomorphicUI`)并同批提交。
图**重画/替换** ⇒ 冻结轮作废须重测(承冻结件头部「任何一张图重画 ⇒ 本轮作废」)。
"""

import sys
from pathlib import Path

from PIL import Image, ImageDraw

SIZE = 64            # 画布(px)
RING = 2              # 环厚(px)= 冻结值 / 规格「2px 视觉厚度」
COLOR = (184, 134, 59, 255)  # #B8863B · art-bible §4.1
CLEAR = (0, 0, 0, 0)
OUT = Path(__file__).resolve().parent / "focus_brass_2px-final.png"


def build() -> Image.Image:
    """构造环图:外矩形全填铜色 → 内矩形掏透明(四角随外矩形 = 铜色)。"""
    img = Image.new("RGBA", (SIZE, SIZE), CLEAR)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, SIZE - 1, SIZE - 1], fill=COLOR)
    d.rectangle(
        [RING, RING, SIZE - 1 - RING, SIZE - 1 - RING],
        fill=CLEAR,
    )
    return img


def main() -> int:
    check = "--check" in sys.argv[1:]
    built = build()

    if check:
        if not OUT.exists():
            print(f"FAIL:{OUT} 不存在")
            return 1
        have = Image.open(OUT).convert("RGBA")
        if have.tobytes() == built.tobytes():  # tobytes = 原始像素字节(RGBA 逐点等价)
            raw_have, raw_built = OUT.read_bytes(), _encode(built)
            tag = "字节级一致" if raw_have == raw_built else "像素级一致(字节差 = 压缩器参数,可接受)"
            print(f"OK:{OUT.name} 与脚本产出一致({tag})")
            return 0
        print(f"FAIL:{OUT.name} 与脚本产出**像素不一致** —— 图已漂移,须复核冻结轮")
        return 1

    built.save(OUT, "PNG")
    print(f"wrote:{OUT}({OUT.stat().st_size} bytes)")
    return 0


def _encode(img: Image.Image) -> bytes:
    import io
    buf = io.BytesIO()
    img.save(buf, "PNG")
    return buf.getvalue()


if __name__ == "__main__":
    sys.exit(main())
