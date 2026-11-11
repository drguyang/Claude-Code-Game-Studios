# T22 批十 —— 真包走查带出的两条缺陷(混音电平 + 咳嗽节奏)

- **日期**:2026-11-11
- **批次**:T22 批十(范围裁定 = 乙「真包打完一炉」的走查连带)
- **commit 基线**:`622554f5` + 工作树(本文件所记全部判据跑在该工作树上)
- **产物**:`unity/builds/Linux-x64/大医精诚.x86_64`(2026-11-11 13:44:56 CST 重建)

---

## 1. 范围与路线

用户要求「你来操作跑,我来看」。真包走查路线(键鼠,无手柄):

1. 启动 → 主菜单(4 行:继续诊治 / 新起一局 / 设置 / 退出游戏)
2. `Return` 进「继续诊治」→ `World` 装载(16×1×16,196/256 可走,POI 3,资源点 4)
3. 走到 4 个资源点采药 → 20 屏(容器)选中料 → 炮制屏点火 → 等起货
4. 采集 / 炮制 / 起货 / 病人咳嗽 / 呼吸层全程听

**发现两条缺陷**,均在走查中直接由用户听感提出,不是代码审查产物:

| # | 用户原话 | 缺陷 |
|---|---|---|
| ① | 「游戏里没声音」→「赶紧修复声音太小的问题」 | 整局输出比原声低 **−27 dB**(空间化近场半径 1 m vs 第三人称听者 5.5 m) |
| ② | 「2背景一直有的咳嗽修复一下」 | 病人重档每 **2 s** 咳一次(数据 40 tick) |

---

## 2. 缺陷 ①:整局音量 −27 dB

### 2.1 先排除管线(证明「放出来了但听不见」)

`-tutorialdiag` 诊断(装配时 + 发射时):

```text
[audiodiag] preload=True seams=ok rows=20 clipsLoaded=20 missing=0 buses=ok listenerVol=1 pause=False rate=48000
[audiodiag] 起货 cue 解析 resolved=True ordinal=19
[audiodiag] oneshot cue=19 clip=sfx_process_complete vol=1.000 playing=True blend=1.00 min=1.00 max=12.00 roll=Logarithmic group=SFX pos=(14.15, 0.00, 14.15) listener=(15.35, 3.41, 19.68) listenerVol=1 pause=False
[audiodiag] 起货 cue 发射 tick=714 ordinal=19 clip=sfx_process_complete cam=(15.35, 3.41, 19.68) 累计=1
```

⇒ seam / 表行 / clip / bus / listener / pause / sample-rate 全正常,`playing=True`。
⇒ **不是管线断**,是电平过低。

### 2.2 实测(可复跑)

录信宿 monitor(`sink 12` = 1080P USB Camera 单声道,用户确认该输出正确):

```bash
S=alsa_output.usb-DC474C00_P090101_SN0002_1080P_USB_Camera_DC474C00_P090101_SN0002-02.mono-fallback.monitor
parec -d "$S" --rate=48000 --channels=1 --format=s16le --file-format=raw > rec.raw
# 另开:paplay 直放同一素材作对照
python3 -c "import numpy as np;a=np.fromfile('rec.raw',dtype='<i2');print(int(abs(a).max()))"
```

| 对象 | 峰值 | 相对素材峰值 29204 |
|---|---|---|
| 原声文件 `unity/Assets/Audio/vo_patient_cough_damp_m2_1.wav` | **29204** | 0 dB(基准) |
| 修前:游戏输出(monitor) | **1335** | **−26.8 dB** |
| 修后:游戏输出(monitor,本文件对应构建,**已落盘** `t22j-measure.raw`) | **8734** | **−10.5 dB** |
| 修后 RMS | 1984 | −23.4 dBFS |

⇒ 增益 **+16.3 dB**;修前 −27 dB 在任何合理听音量下都是「听不见」。
(修后首次试听那次实测峰值 7156,与本次 8734 的差来自不同病人 / 不同 cue 实例,方向一致。
⚠️ **修前 / 首次试听两次录制未落盘**(会话内即时分析);表格末行的 8734 有落盘原件
`t22j-measure.raw`,命令即上方两条。)

### 2.3 根因与修法

- **根因**:`AudioListener` 单挂点 = 第三人称主相机(ADR-020 §七),离地面声源恒
  ≈ `sqrt(4.33² + 3.41²) ≈ 5.5 m`;而事件表 `min_distance = 1` + `Logarithmic` 滚降
  ⇒ 每条 cue 都被压掉 14–17 dB,叠加素材 / mixer 差异合计约 27 dB。
- **修法**:`AudioTuning.NearFieldFloor = 6f`;`AudioSourcePool.CreateSource` 与
  `ApplySpatialDistance` 把 `minDistance` **下限**抬到该值(表列更大者不降)。
  **不改听者挂点** —— 那是 ADR-020 §七 已裁口径。

### 2.4 判据与证伪

- 判据:`spatialization_wiring_test.test_td028_poolPinsSpatialTriple_atCreationPoint`
  断言 `src.minDistance == 6f`(**字面量**,不引用 `AudioTuning.NearFieldFloor` ——
  防自指判据 F3)。
- 证伪 M1:`NearFieldFloor 6f → 1f` ⇒ **1 红**(正是该条)。
  还原:`AudioTuning.cs` SHA-256 `5dc9d986…` 与备份逐字节一致。

---

## 3. 缺陷 ②:咳嗽每 2 s 一次

- **数据**:`assets/data/ai_patient.json` 的 `cue_intervals`
  `severe/mild/recover = 40/90/120` → **`120/240/360`**(6 / 12 / 18 s)。
- **重烘**:`unity/Assets/DataCooked/ai_patient.cooked.bytes`,`ConfigVersion = 0x0487E33C`。
- **实测(本构建)**:

| 病人 | 档位 | 发射 tick | 间隔 |
|---|---|---|---|
| #1(m1)| recover | 160 / 520 / 880 / 1240 | **360 / 360 / 360**(18 s) |
| #2(m2)| severe | 160 / 280 / 400 / 520 / 640 / 760 / 880 / 1000 / 1120 | **120 × 8**(6 s) |

⇒ 两档都按数据生效,不再是 2 s。

- **判据重写(旧判据的前提已失效)**:
  原先 `test_realSeedTable_cueIntervalsMatchLegacyConstants` 断言真种子 ==
  `CueIntervals.Default`。用户 2026-11-11 调值后该等式**不再成立**,故:
  - `test_realSeedTable_zeroErrors` 改为断言**结构约束**:`Validate` 空 ·
    `Severe > 0` · `Severe < Mild` · `Mild ≤ Recover` · `SchemaVersion == 1`;
  - 旧等式条改名 `…_retired`,只留 `Severe != Mild` 反向哨兵,注释写明等式为何退役。
- 证伪 M2:源 `severe 120 → 240`(与 mild 相等)⇒ **10 红**:
  `test_realSeedTable_zeroErrors` · `…_retired` · `…_behaviorBandsAreFixedPointQuantized` ·
  `test_behaviorBands_quantizationWithinOneQ1616Step` · `test_cooked*` 7 条(源 ↔ cooked 同步)。
  还原:`ai_patient.json` SHA-256 `b4e3b926…` 与备份逐字节一致;cooked 未在变异面内(未重烘)。

---

## 4. 回归

| 套件 | 结果 | 时刻(UTC) | 基线对照 |
|---|---|---|---|
| EditMode 全量 | `7443 / 7406 / 0 / 0 / 37` | 05:36:19Z–05:38:26Z | 批九实跑 `7443 / 7406` ⇒ **逐项相同**(本批**无新增 `[Test]`**;两条为就地重写 + 一条改名) |
| PlayMode 全量 | `380 / 375 / 0 / 2 / 3` | 05:39:19Z–05:39:41Z | 逐项相同 |

批次 = T22 批十 · commit `622554f5` + 工作树。**证伪轮在回归之后**,且还原 SHA-256
逐字节一致 ⇒ 回归所测状态即本批终态。
定向套件(Audio 四件)`199 / 199 / 0` @ 02:57:08Z–02:57:42Z。

---

## 5. 构建

```text
Build Finished, Result: Success.
[player-build] Result: Succeeded;错误数 = 0;耗时 = 00:00:18.73;输出 = unity/builds/Linux-x64/大医精诚.x86_64;大小 = 123684320 B
```

- 日志:`unity/Logs/player-build-20261111T054417Z.log`
- 数据新鲜度:`data-core` bundle 写入 13:44:56 > cooked 重烘 13:35:35
  ⇒ **出货构建含新咳嗽间隔**;本文件 §3 的 tick 间隔即在该构建上实测。

---

## 6. 不得借绿 / 残留

- **声学判据仍 `NOT-RUN`**:`AC-44-03 [L]` 等听测没有做。本次只证「用户实机听后说正常」
  + 「monitor 电平差从 −26.8 dB 收到 −10.5 dB」,**不等于**声学判据签核。
- **远端病人在 `max_distance = 12 m` 外仍完全无声** —— 这是空间化的设计面;但「同一间屋里
  在另一头说话听不见」是否可接受,归数值轮。
- `NearFieldFloor = 6` 是**种子值**(≈ 相机后置偏移量级),数值归用户。
- 咳嗽间隔 `120/240/360` 同样是**种子值**;本次只消除「2 s 一次」的重复感,不声称最终节奏。
- 走查本身创建了 `w4…w10` 若干测试世界目录(主菜单「新起一局」误触 / 反复启动);
  它们是本机存档,不属仓库产物。
- **本批不改** ADR-020 §七(听者挂点)、不改 `spatialization` 表列语义;`TD-audio-110` 已登记为
  `closed`(债册 2026-11-11 行)。