# story-020 · 语声变体选择接进运行期 —— 实现证据原件

> **对象**:`production/epics/audio-system/story-020-voice-variant-selection-wired.md`
> **AC**:`AC-020-1` … `AC-020-4`(4 条;前三 [A],第四条账面)
> **日期**:2026-10-18
> **来源判定**:`docs/tech-debt-register.md` **`TD-audio-030`** —— `AudioClipLibrary` 对多素材行
> **恒取 `row.Assets[0]`**,`VoiceVariantLib`(分桶 / `SelectVariant` / 烘焙门)**生产零调用**。
> **本件性质**:实现轮证据(**交付面 → AC 载体 → 证伪轮 → 回归对账 → 如实登记缺口**)。
> **⚠️ 状态口径**:story-020 的 [A] 三条**全绿且有反证**;但 AC-44-02 的**人耳半边**
> (「听起来确实在变」)归 `TD-audio-102`,本件**不**宣称它已证。

---

## §一 交付面

| # | 交付 | 文件 | 关法 |
|---|---|---|---|
| **AC-020-1** | 多素材行**逐条装**(此前恒装 `assets[0]`) | `Gameplay.Presentation/Audio/AudioClipLibrary.cs` | `Preload` 内层循环遍历 `row.Assets[]`;`_clips` 改存 `AudioClip[]`(位置 = 变体索引);行内**至少一条**装成才计该行,缺的素材留 `null` 位 |
| **AC-020-1** | 选择入口 = 真生产选择器 | 同上 · `Resolve(int cue, int tableCueId, int tier, int identity)` | 行内素材 > 1 时:把该行投影成**单行** `VoiceVariantLib.VoiceVariantTable` → `SelectVariant`(CueId × Tier × 桶)得**槽起点** → `SelectSlot`(槽起点, N, 身份)得**槽内位置** → `assets[slot]` |
| **AC-020-1** | 槽宽 / 槽内位置 / 身份哈希(纯函数) | `Gameplay.Presentation/Audio/VoiceVariantLib.cs` | 新增 `BucketBand(int total)` = `max(1, total/3)`、`IdentityScalar(int identity, int range)`(整数乘 + xorshift 混合 + 取模)、`SelectSlot(int start, int total, int identity)`;**无墙钟 / 无 `UnityEngine.Random` / 无静态可变态** |
| **AC-020-2** | 身份进选择键(生产链路) | `AudioClipLibrary.ResolveCue(in AudioCueDto, int tier)` + `AudioSourceRenderTarget` + `CompositionRoot` | `ResolveCue` 取 `cueDto.Source` 作身份(环境音 `-1` ⇒ 取 cue 自身);`AudioSourceRenderTarget` 增 `Func<AudioCueDto, AudioClip>` 解析缝(与既有 `Func<int, AudioClip>` **并存不替换** —— 62 处既有 EditMode 调用零改动);组合根改传整条 cue |
| 测试 | 新夹具 9 测 | `unity/Assets/Tests/EditMode/Audio/voice_selection_wiring_test.cs`(新) | 断言宾语 = 真 `AudioClipLibrary` + 真 `VoiceVariantLib` + **真种子** `assets/data/audio_events.json`(经 `AudioEventTableBake` 真烘焙) |

### 为什么「变体索引 → 素材」的映射落在 `AudioClipLibrary`

事件表 `assets[]` **没有索引列**(`AudioEventTableGates.RowKeys` 白名单无此字段)⇒ 映射只能由
**能看见文件名的一方**建立。装载面是唯一持有 bare 名的地方;`VoiceVariantLib` 保持纯整数域
(它的 `VoiceVariantRow` 里装的就是索引)。两侧分工:**库选槽与槽内位置,装载面把位置翻成素材**。

### 为什么 `BucketBand` 的槽宽是「形状」不是「数值」

`12 → 4/4/4`、`4 → 1/1/2` 只决定「一行素材如何铺进三桶」,**不触碰**分桶阈值
(`BucketLow` / `BucketHigh`,归用户数值轮)与变体量。改它会让某些变体不可达 ⇒ 由 AC-020-3 钉住。

---

## §二 AC ↔ 测试载体映射

| AC | 载体(测试名) | 判据 |
|---|---|---|
| **AC-020-1** | `VoiceSelectionWiringTest.test_ac020_1_multiAssetCue_usesRealSelector_notAssetsZero` | 逐 (tier 0–2) × (身份 0–11) 断言 `Resolve` 结果 = **直接调用生产选择器**复算的素材名;并断言「选中 `assets[0]` 的次数 > 0」以外的形态**确实发生**(否则等于没换) |
| | `test_ac020_1_selectorBinding_pinnedOnProductionClass` | ① 反射钉住 `Resolve(int,int,int,int)` 存在;② **扫生产程序集 IL**,断言至少一个方法对 `SelectVariant` / `SelectSlot` 有真 `call` —— 堵「测试单边绿、生产零调用」 |
| | `test_ac020_1_singleAssetAndInvalidInputs_behaveUnchanged` | 单素材行任意身份恒该条;负 / 越界 ordinal ⇒ `null`;`SelectVariant` 对空表 ⇒ `-1`(不静默取 0) |
| **AC-020-2** | `test_ac020_2_sameIdentity_isDeterministic` | 同 (cue, tier, identity) 两次调用同名 |
| | `test_ac020_2_distinctPatients_doNotAlwaysShareVariant` | 每个 tier 下 8 名患者**必须**出现 ≥ 2 个不同变体 |
| | `test_ac020_2_distribution_coversSlot` | 8 名患者选出的变体全 ∈ 行内,且 ≥ 2 条 |
| | `test_ac020_2_identityScalar_spreadsAcrossRange` | 64 个身份在 `[0,256)` 上 > 32 个不同值;`range=0 ⇒ 0` 不抛;**三桶全部可达**(否则 AC-020-3 结构性不可达) |
| | `test_ac020_2_cueSource_reachesSelector_notJustCueOrdinal` | 真池 + 真渲染目标 + 整条 cue 解析:两名患者(`Source=101/202`)的 `AudioSource.clip.name` = 按各自 `Source` 算出的变体 |
| **AC-020-3** | `test_ac020_3_allTwelveVoVariants_reachable` | 真种子咳嗽行 = 12 条 `vo_patient_cough_damp_*`(**从表行取清单,不内联**);身份 × tier 全域枚举后 **12 条逐条可达** |
| **AC-020-4** | `docs/tech-debt-register.md` `TD-audio-030` 结案注 · `docs/architecture/tr-registry.yaml` `TR-diag-022` note | 原判定 → 修复落点 → 验证命令三件齐;**明写**人耳半边未证 |

---

## §三 证伪轮(逐条「停用什么 ⇒ 哪条红」)

> 纪律:每例**改 → 跑 → 还原在同一命令内**(`/tmp/fz.py`),还原后 **SHA-256 逐字节校验**
> (`restored sha ok`)。过滤器固定 `DaYiJingCheng.Tests.Unit.Audio.VoiceSelectionWiringTest`。

| # | 停用的守卫 | 期望变红 | 实测 |
|---|---|---|---|
| **F1** | `Resolve` 内的槽内位置:`SelectSlot(...)` → 常量 `0`(即「恒第 0 条」) | AC-020-1 主测 · AC-020-2 两条分布 · AC-020-3 | **红 ×4** ✅ |
| **F2** | `SelectVariant` 结果被丢弃(`0 * SelectVariant(...)` ⇒ 恒弱桶槽) | AC-020-1 主测 · AC-020-3 | **红 ×2** ✅ |
| **F3** | `SelectSlot` 里身份不进哈希(`identity ^ (start*40503)` → `start*40503`) | AC-020-3 | **红 ×1** ✅ |
| **F4** | `BucketBand` 槽宽 `total/3` → `total/6` | AC-020-3 | **红 ×1** ✅ |
| **F5** | `Resolve` 多素材分支整段短路(`if (true) return first;`) | AC-020-1 主测 · 绑定测 · AC-020-2 两条 · AC-020-3 | **红 ×5** ✅ |
| **F6** | 注入缝装来的 clip 也塞进 `_handles`(即走 `Addressables.Release`) | 全部 7 条(TearDown 判 Unhandled log) | **红 ×7** ✅(证明释放缝是承重件) |
| **F7** | `ResolveCue` 的身份来源删掉(`cueDto.Source >= 0 ? ... : ...` → `cueDto.Cue`) | 接线测 | **红 ×1** ✅ |
| **F8** | `Resolve` 的槽内位置只在前 6 条里取(`Math.Min(6, loaded.Length)`) | AC-020-1 主测 · AC-020-3 | **红 ×2** ✅ |

**一处如实登记的失败尝试(不算证伪成功)**:首次的「F8-a」是把 `AudioSourceRenderTarget.ClipOf`
里两条解析缝的**优先级对调**(`_clipOf` 先于 `_cueClipOf`)—— 结果 **exit 0 全绿**。
根因:本测试只传 `_cueClipOf`,优先级对调对它**没有观测差**;真正承重的是 `ResolveCue` 的
身份来源 ⇒ 改写为 **F7**(停用 `cueDto.Source`)后才转红。**对调优先级这一步没有判别力**,
记录在此以免后人把「改了却没红」误当夹具坏。

**未发现新的假绿**;F1–F8 每一例都红在**预期的那条**上(报文点名具体患者 / 具体变体)。

---

## §四 回归

| 项 | 值 | 文件 |
|---|---|---|
| 基线(S19 终跑,story-019) | `3748 / 3703 / 0 / 45`(EditMode) | `unity/Logs/s19-full-editmode.xml` |
| 音频全域(本件中途) | `492 / 492 / 0 / 0`(`--filter DaYiJingCheng.Tests.Unit.Audio`) | 无原件(stdout) |
| **本件全量(最终)** | **`3757 / 3712 / 0 / 45`**,exit 0 | `unity/Logs/s20-full-editmode.xml` |
| 等式判据 | `total 3757 − 3748 = +9`;`passed 3712 − 3703 = +9` | 新增 `[Test]` 逐名数 = **9**(`voice_selection_wiring_test.cs`) |

> ✅ 本次**等式严丝合缝**(+9 = 新增 9 条),且基线原件在位(`s19-full-editmode.xml`)⇒
> S19 遗留的「+16 vs +15 差 1【待核】」**本轮未消**(那是 S19 的账,须逐名 diff S19 与 S18 两轮),
> **不得**用本轮的 +9 去冲抵它。

**音频侧零回归**:`492/492`(含 `VoiceVariantsTest` 21 · `RuntimeAssemblyTest` 36 ·
`AssemblyBoundaryTest` 24 · `EventTableGateTest` 69 等)—— 既有夹具对 `AudioClipLibrary`
的调用面(`Resolve(int)` / `BusOf` / `LoadedCount` / `MissingCues`)语义未变。

---

## §五 如实登记:未证 / 未跑

| # | 项 | 状态 | 说明 |
|---|---|---|---|
| 1 | **AC-44-02 人耳半边**(「不同病人听起来确实不同」) | **NOT-RUN** | 归 `TD-audio-102`(AC-44-03 的 `[L]`);本件只让**被听的对象真的在变**,不判听感。⚠️ 不得据本件推定 AC-44-02 已落地 |
| 2 | 真 `Addressables` 装载 12 条变体 | **未在本件验证** | EditMode 无 catalog(`runtime_assembly_test` 的 NOT-RUN 哨兵仍写着 `LoadedCount == 0`);PlayMode 半边 `audio_wiring_test::test_ac018_2_clipsPreloaded_noMissingCues` 只断言**行数**相等 —— 现在一行装 12 条素材,**该测的 `LoadedCount` 语义仍是「行数」**(已就地改注释),但「12 条全装成」这一条**没有夹具**。⇒ 登记为缺口,归下一次 PlayMode 轮 |
| 3 | 事件表 `assets[]` **重排** = 静默换声音 | **未加门** | `variant index i ↔ assets[i]`,重排作者态行即改变「哪个声音」。当前缓解 = `ConfigVersion` 内容哈希(ADR-014 §五);**没有**专门的构建期门。登记为缺口,归 D6/D9 门族下一次扩充 |
| 4 | `Tier` 在生产恒 0 | 已按设计 | `TierSource` 闭集只有 `Local`(契约层钉死,裁定 D-A)⇒ `Resolve` 的 `tier` 形参在 P0 恒 0;测试逐 tier 覆盖是**为将来**,不是当下行为 |
| 5 | IL2CPP 三格(ADR-012 矩阵) | **NOT-RUN** | 本机仅 Mono <br>⚠️ **2026-10-21 订正:本句「本机仅 Mono」为假** —— ADR-012 §Validation 2026-10-17 已逐格实测:Linux-x64-Mono ✅ 09-24(3/3)· **Linux-x64-IL2CPP ✅ 09-24**(StandaloneLinux64 player 3/3,与独立 Python 金标准 + Mono 三方 19/19 逐位相同)· Linux-ARM64-IL2CPP ❌ **目标不存在**(pinned 工具链 46 个 BuildTarget 无任何 arm 目标;用户裁定长期未达)· CI 常驻 ⚠️ job 已建但从未跑(缺 `UNITY_CLIENT_ID`/`UNITY_CLIENT_SECRET`)。⇒ **矩阵级,非本件个体判据**。原文保留不删。 |
| 6 | epic 44 的「16 条未全验证」计数 | **未重算** | 承 story-020 §Tasks AC-020-4 的明示边界 |

---

## §六 顺手修正(同批,非新裁决)

- `AudioClipLibrary.LoadedCount` 的语义在文档上写明 = **行数**(此前注释写「clip 数」,
  一行多素材后会误导);
- `AudioClipLibrary.Preload` 新增 `loader` / `internalRelease` 两个**可选**注入缝(默认 null ⇒ 生产
  路径逐字不变),使选择面可在 EditMode 用假素材直驱 —— 缝内装来的 clip **不**经
  `Addressables.Release`(那会打 `[Error]`,Unity Test Framework 判 Unhandled log)。
