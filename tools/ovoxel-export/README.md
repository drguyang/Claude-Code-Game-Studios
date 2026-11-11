# ovoxel-export — nvdiffrast-free GLB export patch

**Status(2026-11-11):⛔ 已冻结 —— 用户同日裁定「先放弃 Pixal3D」。**
补丁与夹具**留档**,不接入管线、不推进 A800 对拍、不进任何资产流程。
若将来重启 Pixal3D,按下方步骤先跑判据 ②;在那之前本目录是**归档**,不是可用工具。

历史状态(放弃前):补丁与单元测试已写(17 条,16 绿 / 1 条 torch 适配器 NOT-RUN);
dr 对拍与端到端 `to_glb` **从未跑**。`art-bible §8.10.6.11` 的白名单行已改为 **❌ 放弃**。

---

## 为什么需要它

Pixal3D 的代码与权重是 MIT,但**唯一的 GLB 导出路径**依赖 nvdiffrast:

```
inference.py:17          import o_voxel
inference.py:263-269     o_voxel.postprocess.to_glb(...)
o_voxel/postprocess.py:10   import nvdiffrast.torch as dr          ← 模块级,不可选
o_voxel/postprocess.py:230  dr.RasterizeCudaContext()
o_voxel/postprocess.py:237  dr.rasterize(...)
o_voxel/postprocess.py:249  dr.interpolate(...)
```

nvdiffrast 是 NVIDIA 1-Way Commercial License,§3.3 仅限非商用。因此不替换这三处,
产物就不能用于商业管线。

`o_voxel` 的上游是 `microsoft/TRELLIS.2` 的 `o-voxel/`(MIT)。wheel 里的
`postprocess.py` 与上游**逐字节相同**
(sha256 `ef51a1ba0f2748ffb4c265b47d382cee956f23c6a52d0f3587e6d8beccb7e54a`),
所以补丁对象是一份 MIT 源码,不是逆向或二进制 hook。

## 改了什么

只改 `postprocess.py` 的「Texture Baking」段,把 dr 的三处调用换成
`uv_raster.py`(NumPy,无第三方许可依赖):

| dr 调用 | 替换 |
|---|---|
| `dr.RasterizeCudaContext()` | 无上下文对象;`rasterize_uv()` 自带缓冲区 |
| `dr.rasterize(ctx, uvs_rast, faces, resolution)` | `rasterize_uv_torch(out_uvs, out_faces, texture_size)` |
| `dr.interpolate(verts, rast, faces)` | `interpolate_uv_torch(out_vertices, rast, out_faces)` |

输出契约与 dr 一致:

* `rast[..., :2]` = 重心坐标 `(u, v)`,`w = 1-u-v`;
* `rast[..., 3]` = 三角形序号 + 1(0 = 背景);
* 像素中心 `(x+0.5, y+0.5)`,`uv = 0` 映射到第 0 行/列(已对照 dr 的 CUDA kernel
  `rasterize.cu` 的 `xs/xo/ys/yo` 变换确认方向一致);
* 边界值按 dr 的 `__saturatef` + 对角归一化处理。

输入与 dr 的 `pos` 对齐:`cumesh.uv_unwrap` 返回的是**逐顶点** `(V,2)` UV,本实现
按 `faces` 聚合成逐角 `(T,3,2)`;也直接接受逐角数组(测试覆盖两种形态等价)。

**已知语义差(仅在 UV 重叠处可见)**:dr 用深度缓冲;UV 全为 z=0,重叠 texel 的胜负
由实现决定。本补丁取「**三角形序号最大者胜**」,与原代码跨 chunk 的
`torch.where(mask_chunk, rast_chunk, rast)` 同向且**与处理顺序无关**。xatlas 图块不重叠,
差异只可能落在共享图块边界的 1 px 上;对拍夹具会把这一项量化。

## 文件

| 文件 | 作用 |
|---|---|
| `uv_raster.py` | UV 三角形光栅化 + 属性插值(NumPy);torch 适配器懒加载 |
| `postprocess_patched.py` | 上游 MIT 文件的补丁副本(仅 3 处替换;其余逐字节相同) |
| `apply_patch.py` | 安装前校验上游 SHA → 备份 → 复制 → 静态复查;`--revert` 可还原 |
| `test_uv_raster.py` | 13 条 NumPy 单测(含与穷举参考实现的逐像素比对、分块等价、仿射重建、UV 原点朝向、两种 UV 形态等价) |
| `test_apply_patch.py` | 4 条补丁安装/拒绝/还原测试 |
| `compare_with_nvdiffrast.py` | **判据 2 的栅格层**:dr vs 本实现,输出 JSON(需 CUDA) |
| `compare_glb_textures.py` | **判据 2 的贴图层**:同一资产两份 GLB(dr 版 / 补丁版)的几何、UV、PSNR |
| `LICENSE-TRELLIS.2.txt` | 上游 MIT 许可原文 |

## 本机已验证 / 未验证

已验证(可复跑):

```bash
python3 -m unittest tools/ovoxel-export/test_uv_raster.py -v
# Ran 13 tests ... OK (skipped=1)   ← skipped = torch 未安装,torch 适配器 NOT-RUN

python3 -m unittest tools/ovoxel-export/test_apply_patch.py -v
# Ran 4 tests ... OK
```

未验证(**不得读作通过**):

* `torch 适配器` —— 本机无 torch,报显式 skip;
* `compare_with_nvdiffrast.py` —— 本机无 torch/CUDA,退出码 **2 = NOT-RUN**;
* 真 `to_glb` 端到端 —— 需要 o_voxel / cumesh / flex_gemm / Pixal3D 权重,未在本机跑;
* 4096² 真资产纹理 PSNR / 接缝 —— 需 A800 上的 Pixal3D 推理产物。

## 在 A800 上怎么用

```bash
# 1) 按 TRELLIS.2 安装(或至少装好同版本 wheel);不要额外装 nvdiffrast
pip install o_voxel / flex_gemm / cumesh wheel ...

# 2) 校验 + 安装补丁
python3 tools/ovoxel-export/apply_patch.py --check
python3 tools/ovoxel-export/apply_patch.py --apply

# 3) 离屏对拍(判据 2 · 栅格层)
python3 tools/ovoxel-export/compare_with_nvdiffrast.py \
    --resolution 4096 --grid 256 --json production/qa/evidence/ovoxel-dr-compare.json

# 3b) 真资产对拍(判据 2 · 贴图层):同一 image 在上游 env 与补丁 env 各导出一次
python3 tools/ovoxel-export/compare_glb_textures.py \
    --a a-upstream.glb --b b-patched.glb --json production/qa/evidence/ovoxel-glb-compare.json

# 3c) 也可直接拿 ≥3 个真实 GLB 的 UV/faces 做栅格层对拍(满足「≥3 资产」)
python3 tools/ovoxel-export/compare_with_nvdiffrast.py --resolution 4096 \
    --mesh a.glb --mesh b.glb --mesh c.glb --json production/qa/evidence/ovoxel-assets.json

# 4) 确认运行期不 import dr
python3 - <<'PY'
import sys, o_voxel.postprocess
print("nvdiffrast imported:", any(m.startswith("nvdiffrast") for m in sys.modules))
PY
# 期望 False

# 5) 真导出(与 upstream 用法一致)
python3 /path/to/Pixal3D/inference.py --image assets/images/0_img.png --output ./out.glb
```

对拍阈值(脚本内置):覆盖像素上 face-id 不一致率 ≤ **1%**、重心坐标 max |Δ| ≤ **1e-4**、
插值位置 max |Δ| ≤ **1e-4**。任一超标 → 退出码 1。

## 仍然不能用的路径

`app.py`(Gradio 预览)、`pixal3d/renderers/*`、`Trellis2TexturingPipeline` 仍然 import
nvdiffrast;补丁**没有**、也**不应该**改它们。资产导出只走 `inference.py` / `inference_mv.py`
的非预览路径。

## 许可

`postprocess_patched.py` 是 `microsoft/TRELLIS.2`(MIT,Copyright (c) Microsoft Corporation)
的修改副本;修改处见文件头与上方表格。完整 MIT 文本见 `LICENSE-TRELLIS.2.txt`。
`uv_raster.py` / `apply_patch.py` / 测试与本项目其余工具同许可。