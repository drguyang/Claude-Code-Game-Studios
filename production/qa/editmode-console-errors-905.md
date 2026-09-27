# EditMode Console 错误级(红色)原文摘录 — 15 条

- **来源 XML**: `unity/Logs/s005-final2.xml` — testcasecount=**905**, passed=**905**, **failed=0**, skipped=0
- **级别判定**: 文案含 Error 级关键词(须 > 0 / 须 ≥ 0 / 须 ∈ / 非法 / 失败 / 越出 / 拒绝),对应 `Debug.LogError`
- **所有归属测试结果**: `Passed`
- **生成时间**: 2026-09-27

> 说明:`unity/Logs/` 按 .gitignore 设计不入库;本报告落 `production/qa/` 作持久记录。

---

## 逐条

### 1
```
[InspirePhaseMapping] 完整呼吸周期 T 须 > 0 —— 返回 0
```
归属测试:`test_adventitiousWindow_nonPositivePeriod_invalid`
文件:`unity/Assets/Gameplay.Presentation/Audio/InspirePhaseMapping.cs`

### 2
```
[InspirePhaseMapping] 完整呼吸周期 T 须 > 0 —— 返回 0
```
归属测试:`test_adventitiousWindow_nonPositivePeriod_invalid`(同 1,第二次触发)
文件:同上

### 3
```
[BreathPhaseGate] 相位分母非法(clip 未就绪 / timeSamples < 0)—— 返回 0(周期原点)
```
归属测试:`test_cyclePhase_invalidDenominator_logsSafeZero`
文件:`unity/Assets/Gameplay.Presentation/Audio/BreathPhaseGate.cs`

### 4
```
[BreathPhaseGate] 相位分母非法(clip 未就绪 / timeSamples < 0)—— 返回 0(周期原点)
```
归属测试:`test_cyclePhase_invalidDenominator_logsSafeZero`(同 3,第二次触发)
文件:同上

### 5
```
[FilterRamp] ramp 时长须 > 0(硬下界 = AudioTuning.RampSeconds)—— 按 0 处理(直接到位,不产生斜坡)
```
归属测试:`test_filterRamp_beginNonPositiveDuration_logsAndMarksInactive`
文件:`unity/Assets/Gameplay.Presentation/Audio/FilterRamp.cs`

### 6
```
[FilterRamp] 频率须 > 0 Hz —— 返回 0 轴(安全值)
```
归属测试:`test_filterRamp_nonPositiveHz_logsSafeZero`
文件:同上

### 7
```
[FilterRamp] 频率须 > 0 Hz —— 返回 0 轴(安全值)
```
归属测试:`test_filterRamp_nonPositiveHz_logsSafeZero`(同 6,第二次触发)
文件:同上

### 8
```
[BreathPhaseGate] INSPIRE_FRACTION 须 ∈ (0,1) —— 本周期不触发(GDD §Tuning Knobs 安全范围)
```
归属测试:`test_shouldFire_invalidInspireFraction_returnsFalse`
文件:`unity/Assets/Gameplay.Presentation/Audio/BreathPhaseGate.cs`

### 9
```
[MixerAssetGenerator] [exposed·YAML] exposed 名「tier_passband_center_hz」查不到同名组的 m_Volume 哈希 —— 拒绝写假 guid(合成条序路径 2026-09-26 退役;若为未来 filter 参数,先裁定资产拓扑)
```
归属测试:`test_normalizeExposedGuids_unknownExposedName_throws`
文件:`unity/Assets/Editor.Tools.Gates/MixerAssetGenerator.cs`

### 10
```
绑重 sidecar 失配:载荷备份失败(The file '/tmp/dayj_hash_mismatch_1b72f98aa2004c968999426780ffc5f/bindings.overrides.json.bak-001' already exists.)—— 跳过备份,仍载入默认,不中断。
```
归属测试:`test_hashMismatch_backupDirUnwritable_skipsBackupStillDefaults_noCrash`
文件:`unity/Assets/Gameplay.Input/BindingsStore.cs`

### 11
```
绑重 sidecar 失配:头部备份失败(The file '/tmp/dayj_hash_mismatch_1b72f98aa2004c968999426780ffc5f/bindings.schema.txt.bak-001' already exists.)—— 跳过备份,仍载入默认,不中断。
```
归属测试:`test_hashMismatch_backupDirUnwritable_skipsBackupStillDefaults_noCrash`
文件:同上

### 12
```
绑重 sidecar 写入失败(只记日志,写点不换;内存态 overrides 继续生效,下次启动回退默认):Access to the path '/tmp/dayj_hash_mismatch_7e9f18819bff478ba6457a8c613a0286/bindings.schema.txt' is denied.
```
归属测试:`test_hashMismatch_headerWriteFailure_payloadAlone_mismatchNextLoad_backedUp`
文件:同上

### 13
```
绑重 sidecar 失配:头部备份失败(The file '/tmp/dayj_hash_mismatch_6d6fac3817c740f185dcc08ac1b6ca69/bindings.schema.txt.bak-001' already exists.)—— 跳过备份,仍载入默认,不中断。
```
归属测试:`test_hashMismatch_partialBackup_scopeLogNamesOnlyMovedSide`
文件:同上

### 14
```
绑重 sidecar 写入失败(只记日志,写点不换;内存态 overrides 继续生效,下次启动回退默认):Access to the path '/tmp/dayj_overrides_sidecar_644e2ef0a06b49b5b4ee20bc97043339/bindings.schema.txt' is denied.
```
归属测试:`test_overridesSidecar_headerWriteFailure_payloadAlone_mismatchNextLoad`
文件:同上

### 15
```
绑重 sidecar 写入失败(只记日志,写点不换;内存态 overrides 继续生效,下次启动回退默认):Access to the path '/tmp/dayj_overrides_sidecar_2428e03a0b8d42ed9363fac4509af90e/bindings.overrides.json' is denied.
```
归属测试:`test_overridesSidecar_save_writeFailure_logsOnlyKeepsWritePoints`
文件:同上

---

## 按族归类

| 族 | 条数 | 归属文件 | 触发原因 |
|---|---:|---|---|
| A 音频纯函数防线 | 8 | `InspirePhaseMapping.cs` ×2 · `BreathPhaseGate.cs` ×3 · `FilterRamp.cs` ×3 | 测试故意传非法值,防线按设计降级并记录 |
| B 生成器拒绝写假 guid | 1 | `MixerAssetGenerator.cs` | 测试注入未知暴露名,门拒绝静默写假值 |
| C input-system 绑重降级 | 6 | `BindingsStore.cs` | 测试构造备份失败/写入拒绝路径,实现降级不中断 |

## 性质判定

**15 条全部是负例测试的真实输出,不是缺陷。**

- **族 A**(8 条):防线在响 —— 这正是测试要证明的事。删掉等于让「配置非法时静默返回 0」与「算错了」无法区分。
- **族 B**(1 条):门在拒绝 —— 防止未来 filter 参数时静默写出无效 guid。
- **族 C**(6 条):实现在降级 —— 备份失败/权限拒绝时不中断,退回默认绑定。

**没有任何一条建议删除。** Console 出现红色是负例测试的正常副产物;判据是测试结果(905/905 Passed),不是 Console 颜色。

---

## 验证基线

- 过滤 `SnrAnalysis|TierFilterCarrier` → 32/32 Passed
- 全量 EditMode → **905/905 Passed** · 0 失败 · 0 跳过
