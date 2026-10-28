# AC-0 spike —— Unity 6.3 LTS 的 `TerrainData` / `TerrainCollider` 行为实测

**对象**:`production/epics/level-tool/story-002-world-geometry-and-terrain.md` **AC-0(BLOCKING 门)**
**日期**:2026-10-29
**引擎**:Unity 6.3 LTS `6000.3.24f1`(`Application.unityVersion` 实测,非文档引用)
**探针**:`unity/Assets/Editor.Tools.Level/TerrainApiSpike.cs`(batch 入口,无生产消费者)
**结论**:**AC-0 三条全部有本机实测输出;其中两条**推翻了实现前的假设**(见 §五)**

---

## §一 复算命令

```bash
cd unity && ~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity \
  -batchmode -nographics -projectPath "$PWD" -quit \
  -executeMethod DaYiJingCheng.EditorTools.Level.TerrainApiSpike.RunTerrainApiSpike \
  -logFile "$PWD/Logs/terrain-spike.log"
grep '^\[SPIKE-TERRAIN\]' Logs/terrain-spike.log
```

本轮两次运行的退出码均 **0**(`EXIT=0`);`unity/Logs/` 是 `.gitignore:257` 覆盖的暂存区。

---

## §二 ① `TerrainData` 高度的返回精度与坐标系

### 2.1 签名(**先枚举,不猜**)

```
S.GetHeight_overloadCount=1          S.GetHeight_signatures=Single GetHeight(Int32 x, Int32 y)
S.GetInterpolatedHeight_overloadCount=1
S.GetInterpolatedHeight_signatures=Single GetInterpolatedHeight(Single x, Single y)
S.GetSteepness_overloadCount=1       S.GetSteepness_signatures=Single GetSteepness(Single x, Single y)
```

### 2.2 坐标系 —— **两套 API 吃两套坐标**

斜面 `height[z,x] = x/(res-1)`(res = 33),地形尺寸 `16 × 10 × 16` m。决定性取样点在 **x = 0.25**:
归一化读法给 `0.25 × 10 = 2.5 m`,世界米读法给 `0.25/16 × 10 = 0.15625 m`。

```
A2.interpHeight_0p25_0p5=2.50015259
A2.interp_takes_normalized=True
A2.interp_takes_worldMeters=False
```

| API | 入参坐标 | 返回单位 |
|---|---|---|
| `GetHeight(int x, int y)` | **heightmap 网格索引**(`0 … heightmapResolution-1`) | **米** |
| `GetInterpolatedHeight(float x, float y)` | **归一化** `[0,1]` | **米** |
| `GetSteepness(float x, float y)` | **归一化** `[0,1]` | **度** |

索引面实测(同一斜面):

```
A2.heightmapWidth=33   A2.heightmapHeight=33
A2.getHeightIndex_0=0   A2.getHeightIndex_mid=5   A2.getHeightIndex_last=10
A2.getHeightIndex_overlast=no_throw:10
```

⇒ 索引 `0 / 16 / 32` 对应米 `0 / 5 / 10`(= `size.y = 10`);**越界索引被钳到末样本,不抛**。

### 2.3 精度 —— **heightmap 是 16 位量化,不是 float32**

作者侧写归一化 `0.25`,读回米:

```
A3.expected_meters=2.5
A3.read_meters=2.50015259
A3.deviation_meters=0.000152587891
A3.sizeY_over_65535=0.000152590219
A3.deviation_over_quantum=0.999984741
A3.is_16bit_quantized=True
```

⇒ 偏差 = **恰好 1 个 `size.y / 65535`**。高度分辨率 = `size.y / 65535`
(本例 10 m 高地形 ⇒ 1 LSB ≈ **0.153 mm**)。

**1e-6 的高度差不可分辨**(首轮实测,原文保留):

```
A.ulp_probe_a=2.50015259   A.ulp_probe_b=2.50015259   A.ulp_probe_delta=0
A.ulp_probe_distinguishable=False
```

⚠️ 这不是「float32 精度不够」,是 **16 位 storage**:float32 在 `[0,1]` 的 ULP ≈ 6e-8,
远小于 1e-6;真实瓶颈在 heightmap 的存储格式。

---

## §三 ② `TerrainCollider` 与 `TerrainData` 的可走性关系(`slopeLimit` 在哪一侧生效)

### 3.1 声明方 —— 反射逐类型取证

```
B.terrain_has_slopeLimit=False
B.terrainCollider_has_slopeLimit=False
B.terrainData_has_slopeLimit=False
B.characterController_has_slopeLimit=True
B.terrainData_has_getSteepness=True
B.cc_slopeLimit_default=45        B.cc_stepOffset_default=0.3
B.cc_slopeLimit_in_degree_scale=True
```

`TerrainCollider` 的**全部**公开属性(实测枚举,节选其判别性的一条):该列表里**没有** `slopeLimit`
—— 它只有 `terrainData` / `material` / `bounds` / `contactOffset` 等。

### 3.2 单位 —— 45° 斜面实测

```
B.getSteepness_0p5_0p5=45.0017471
B.getSteepness_0p1_0p5=45.0017433
B.steepness_is_degrees_45=True
```

### 3.3 结论

`slopeLimit` **不在 Terrain 侧** —— 它是 `CharacterController` 的属性(默认 45,**单位 = 度**)。
Terrain 只提供**几何**(`TerrainCollider`)与**查询**(`TerrainData.GetSteepness` → 度)。
⇒ 「能不能走上去」这条判定发生在**消费者**(玩家控制器)一侧:
`steepness > slopeLimit ⇒ 不可走`,而不是地形自带一个「可走」标记。

`GetSteepness` 同样吃**归一化**坐标且随位置变化:

```
A2.steepness_norm0p25=17.3550167   A2.steepness_norm0p5=31.99438   A2.steepness_norm1p0=50.9009933
A2.steepness_variesWithPosition=True
A2.steepness_literal4=0            (越界给 0,不是钳到末样本 —— 与 GetHeight 的索引面行为不同)
```

---

## §四 ③ `TerrainData` 能否在**编辑期**被关卡工具读取(不依赖运行期)

```
C.isPlaying=False
C.inmemory_getHeight_mid=5
C.folder_created=True
C.asset_load_ok=True
C.asset_getHeight_mid=5
C.asset_heightmapResolution=33
C.asset_size=16x10x16
C.temp_folder_deleted=True
C.temp_folder_gone_after=True
```

往返路径 = `new TerrainData()` → 写 heightmap → `AssetDatabase.CreateAsset` → `SaveAssets` →
`LoadAssetAtPath<TerrainData>` → **从新引用读出与内存实例相同的值(5)** → `DeleteAsset` 删目录。
⇒ **编辑期可读**,且读的是盘上的资产而不是内存里的那个对象。

⚠️ 临时资产落在 `Assets/TerrainSpikeTemp/`(`AssetDatabase` 的硬要求),探针 `finally` 中删除;
`C.temp_folder_gone_after=True` 是**删除后复查**的实测值,不是「我们调用了删除」。

---

## §五 本轮推翻的两条实现前假设(**这是 spike 的价值所在**)

### ① 首版探针按 `GetHeight(float, float)` 写 ⇒ **6.3 上编译失败**

```
Assets/Editor.Tools.Level/TerrainApiSpike.cs(74,38): error CS1503:
  Argument 1: cannot convert from 'float' to 'int'      (×16 处同类)
```

若按「改成 int 再猜下一个」的路子走,就变成**猜 → 编译 → 再猜**,而 AC-0 要的是**实测**。
⇒ 探针改为**先枚举重载、再按实测签名调用**。这条造出的不只是「拿到了答案」,
还有「答案是怎么拿到的」在证据里可复核。

### ② 「`GetHeight` 返回归一化高度」—— **错**

首轮按归一化解读时,`A.corner11_matchesAuthoredNormalized=False`(作者写 `1.0`,读出 `10`)。
真值是**米**。同轮 `A.interp_equals_normalized_x_sizeY=False` 也是同一条误读的连带产物
(它拿 `5`(米)去比 `0.5 × 10`(米),因为把「归一化」这个量纲错标在了 `GetHeight` 上)。

### ③(附带)**heightmap 是 16 位存储** —— 此前无任何文档或注释提过

见 §2.3。它直接决定 C1(C1 两层漂移,S6)的**可判定容差**:逻辑层的整数量化若细于
`size.y / 65535`,就会在**采样之前**就与地形本身不可比。

---

## §六 对 story-002 实现面的影响(逐条)

| 影响 | 说明 |
|---|---|
| **整数格化路径可行** | 编辑期可读地形(§四),⇒ AC-1 的「每格可走性由工具产出」在工具侧有输入面 |
| **可走性判定不在 Terrain 侧** | AC-1 的 `walkable` 是**工具算出来的整数**,不是地形自带的;判据必须是 `steepness > slopeLimit`(§三) |
| **`slopeLimit` / `stepOffset` 的归属确认** | 二者都是 `CharacterController` 的 float 属性(度 / 米),S2 只导出**整数量化值**,两侧比对归 S6 |
| **量化容差有实测上界** | 地形高度分辨率 1 LSB = `size.y/65535`;C1 的容差不得细于此 |

---

## §七 本件证明不了的事(禁借绿)

1. **没有做动力学仿真** —— 本探针**未**验证「`CharacterController` 在 45° 斜坡上真的滑不滑」。
   那是 PlayMode + `Update` 循环的事。§三 的结论是**接口归属面 + 数值面**的(谁声明 / 单位是什么 /
   随位置怎么变),**不是**动力学行为面的。措辞不得写成「实测了角色爬坡」。
2. **没有真实美术地形** —— 全仓 `grep -rl "TerrainData" unity/Assets` 在 S2 之前**零命中**
   (本节写于探针落盘后:此刻只有本探针自己命中)。§四 证明的是「编辑期能读**任意** `TerrainData` 资产」,
   **不是**「本项目的某份美术地形已可读」——后者尚不存在。
3. **没有测大尺寸地形的性能** —— 只测了 33×33 的 heightmap。转换耗时随分辨率增长的行为未测。
4. **`GetSteepness` 越界返回 0**(`A2.steepness_literal4=0`)的原因未查 —— 只如实记录观测值。
   本 story 的生产路径不会传越界值,故不阻塞;但它与 `GetHeight` 的「钳到末样本」**行为不同**,
   将来若有人写「先钳再查」的工具,这条差异会咬人。
