# 九宫格冻结件 round-2 重冻(`TD-skeuo-10` 结案)

> **日期**: 2026-11-04 · **sprint-24** · commit 基线 `f369cd6f` + 工作树
> **触发**: `design/assets/specs/nine-slice-freeze-2026-10-08.md` 逐字
> 「任何一张图**重画 / 替换** ⇒ 本轮作废,须重测重冻」。为去水印重画了 8 张在用纹理。

---

## 0. 结论

**8 张重测,只有 1 个冻结值变更,且变更原因不是测量而是接线。**

| | |
|---|---|
| 唯一值变更 | `paper_hemp-final.png` **0 → 64**(它被 `.paper` 接上作无框底纹,按规则 R2 取现行渲染切片) |
| 其余 7 张 | 重测后**维持原值** —— 这本身是证据:重画没把任何框的几何推出原切片 |
| 新增产物 | `tools/slice-measure/measure_slices.py`(原冻结件 §七 写「见会话日志」= 方法不可复算,此处补齐) |

---

## 1. 实测(可复跑)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 tools/slice-measure/measure_slices.py --check
```

实跑输出(节选,**框**类两行是本轮唯一需要看的两行):

```text
border_paper-final.png              236    39    39    39    39     40    64  R1  ✓
border_scroll-final.png             227    72    72    72    72     72    72  R1  ✓
paper_hemp-final.png                235  1024  1024  1024  1024   1024    64  R2/R3  (实测不适用)

比对完成:全部通过
```

完整 8 张的 T/B/L/R 与 round-1 对照见冻结件 **§八 round-2 记录**。

判读要点:`--check` **按冻结件 §二 表的「形态」列分流** —— 只有**框**类走 R1
(`冻结值 ≥ ceil8(四边最大延展)`),满铺 / 件 / 表 / 规格出图走 R2·R3(实测值不作冻结依据)。
⚠️ 首版 `--check` 用「延展 > 0 且 < 半边长」猜框类,把 `focus_brass`(R3)与 `ink_dot`(R2 件)
误判成框并报了两条假红 —— 分类必须读冻结件的形态列,不能靠启发式。

---

## 2. 同批改动

| 项 | 改动 |
|---|---|
| `freeze-v1` 机器块第 4 行 | `paper_hemp-final.png\|0\|-` → `paper_hemp-final.png\|64\|SkeuoPaper.uss` |
| `paper_hemp-final.png.meta` | `spriteBorder` `{0,0,0,0}` → `{64,64,64,64}` |
| 冻结件 §二 表第 4 行 | 实测值更新 + 取值依据备注 |
| 冻结件 §七 | 复算脚本改为指向仓内 `tools/slice-measure/measure_slices.py`;round-1 的期望值标注为「旧图的」 |
| 冻结件 §八 | 新增 round-2 记录(逐张 round-1 / round-2 对照表) |

---

## 3. 判据:门真的读了新登记吗(变异探针)

**绿 ≠ 门跑了** —— 27/27 全绿也可能是门根本没扫到第 4 行。故做一次探针:

```bash
# 把 paper_hemp 的 spriteBorder 改回 0(与冻结值 64 不符),重跑同一门
sha256(变异前) = 8d5d9f8b7a3193c5be9c681c77c6bf07de5cad658c518661cd45a7c83c06df95
```

**实得**:`texture_binding_gate_test` `27 / 26 通过 / 1 失败` ——
红的正是 `test_ac42c8_sprite_border_and_uss_match_freeze_record`,报文:

```text
[C8] `paper_hemp-final.png.meta` spriteBorder = {0, 0, 0, 0},冻结件 = 64
     —— 值只能源自冻结件(AC-42-C8 禁手填)。
```

⇒ 门**确实**读到了第 4 行。还原后 `sha256` 与变异前**逐字节相同**(未用 `git checkout`)。

- 常规回归:`texture_binding_gate_test` **27/27 通过**(`unity/Logs/c8-*.xml`)。

---

## 4. 如实登记的残留(不借绿)

1. `.meta` 的 `spriteMode` / `textureType` / `alphaIsTransparency` 本轮**未复验** —— 只动了 `spriteBorder`。
2. E1 格式门仍只覆盖**顶层面 16 张**;`Brass/` · `Casebook/` 三个子目录件不在其扫描面(承冻结件 §六)。
3. 冻结件抬头仍写「round-1 · 2026-10-08」—— 按「原文不删」保留为历史陈述;
   **当前有效值以 §二 表与 `freeze-v1` 块为准**。
