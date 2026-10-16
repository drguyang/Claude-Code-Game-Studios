# S14-4 实现证据 —— story-012(乐层护栏与性能/VR 切面)· 2026-10-16

> **证据性质**:实现轮原件(`.claude/docs/coding-standards.md` §Review Evidence Standards ——
> 原判定 → 修复落点 → 验证命令,可证伪)。
> **对象**:`production/epics/audio-system/story-012-music-guardrails-perf-vr.md`
> **上游判定**:`production/qa/evidence/review-audio-epic-fake-green-2026-10-16.md` §2.6
> (012 = 12 测中 🟢 真 1 · 🟡 半真 6 · 🔴 假 5)
> **sprint**:`production/sprints/sprint-14.md` S14-4(Should Have · S 件)
> **用户裁定(2026-10-16,AskUserQuestion ×3)**:① `MUSIC_XFADE_MIN_MS` 种子值 = **2000 ms** ·
> ② AC-44-E2 后半「测量点注册表」= **44 侧生产闭集 + 反空转守卫** ·
> ③ 三条 🟡 半真(E1 扫描面 / G1–G3 锚点 / AC-44-18③ 跨文件)= **本轮一并强化**。

---

## §1 原判定 → 修复落点

| # | 原判定(评审原件 §2.6) | 严重度 | 修复落点 | 验证命令 |
|---|---|---|---|---|
| 1 | AC-44-18① 三条测打**自写两行谓词** `MusicXfadeGuard.IsXfadeValid` + 硬编码 `2000`/`1999` | 🔴 | `AudioEventTableGates.ValidateXfade`(**构建期真门**,原唯一具名空位)+ `AudioTuning.MusicXfadeMinMs` | 反证 ①②③④ + **真种子端到端** |
| 2 | AC-44-E2 两条测打**自写** `DspBudgetGuard` + 双端硬编码 `2.5f`/`1.8f` | 🔴 | `AudioBudgetProbe`(生产分位 + 预算判定)+ `AudioTuning.AudioBudgetMs` | 反证 ⑤⑥ |
| 3 | AC-44-E2「测量点注册表」的断言宾语 = **GDD 散文**,注册表零落点 | 🟡 半真 | `AudioMeasurementPoints`(44 侧生产闭集 + **反空转守卫**) | 反证 ⑦⑧⑨ |
| 4 | AC-44-E1 P0-VR 扫描面只有**公开方法签名** | 🟡 半真 | 扫描面扩三面(方法 / **字段** / **属性**)+ 装配引用集 + **负向夹具自证** | 反证 ⑩⑩b |
| 5 | G1–G3 护栏用裸 `Contains("G1")`(**无锚点**,反向句可蒙混) | 🟡 半真 | 锚定**表格行首列** + **锚定判据自证测** | 反证 ⑪ |
| 6 | AC-44-18③ 的 25 侧引用**未跨文件断言**(原 NICE 登记) | 🟡 半真 | `test_musicXfadeMinMs_ownerIs44_withCrossFileReference`(44 GDD owner 声明 + 25 侧同名引用) | 该测自身即判据 |

## §2 交付面

| 类别 | 文件 | 内容 |
|---|---|---|
| 新(生产) | `unity/Assets/Gameplay.Presentation/Audio/AudioMeasurementPoints.cs` | `Ac1008Points` 闭集 · `EmitSymbol`(`nameof` 编译期绑定)· `Validate`(null / 空集 / 含 Emit 三态,**错误文本各自不同**) |
| 新(生产) | `unity/Assets/Gameplay.Presentation/Audio/AudioBudgetProbe.cs` | `Percentile`(最近秩法,与 `InputLatencyProbe` 同口径)· `IsP95WithinBudget`(**负样本哨兵不得冒充通过**) |
| 改(生产) | `unity/Assets/Gameplay.Presentation/Audio/AudioTuning.cs` | `MusicXfadeMinMs = 2000` · `AudioBudgetMs = 2.5` |
| 改(**构建期真门**) | `unity/Assets/Editor.Tools.Gates/AudioEventTableGates.cs` | `ValidateXfade` 补下界比较(原 XML doc 逐字写「**不做**」)+ `using Gameplay.Presentation.Audio` + 文件头范围锁就地订正 |
| 改(测试) | `unity/Assets/Tests/EditMode/Audio/music_guardrails_test.cs` | 12 → **22 测**;两个自写谓词类**全数删除**;三条半真强化;新增两条**判据自证测** |

**关键设计**:下界常量**单一出处** = `AudioTuning.MusicXfadeMinMs` —— 门**引用**它,禁在门内另抄
数值(先例 = `MixerTopologyGates` 引 `MixerRegistry`;纪律 = ADR-024 §⑥)。
`Editor.Tools.Gates.asmdef` 已引用 `Gameplay.Presentation`,零装配变更。

## §3 证伪轮 —— 11 例全红(停用生产码守卫 ⇒ 断言转红)

**纪律**:每例**改 → 跑 → 还原**在**同一命令内**完成(`/tmp/fz144.py`):
① 先把干净文件复制到 `/tmp/s144src-{tag}.bak`;② 改生产码;③ 跑 Unity EditMode(音频过滤);
④ `finally` 块还原;⑤ 还原后**逐字节校验**。

| 例 | 停用的守卫(生产码) | 转红断言 | 红数 |
|---|---|---|---|
| ① lowerbound | 门内 `if (music && xfade > 0 && xfade < AudioTuning.MusicXfadeMinMs)` → `if (false)` | `musicXfade_belowMin_rejected` · `musicXfade_belowMin_diagnosticCarriesTuningValue` | 2 |
| ② domain | 同上,下界改 `xfade < 0`(恒不命中) | 同 ① 两条 | 2 |
| ③ musiconly | 去掉 `music &&` 定义域收窄(下界套到所有行) | `nonMusicRow_belowMin_notRejected` | 1 |
| ④ diagvalue | 诊断文本的真常量值 → 硬编码 `0` | `musicXfade_belowMin_diagnosticCarriesTuningValue` | 1 |
| ⑤ budget | `AudioBudgetProbe.IsP95WithinBudget` → `true` | `dspBudget_p95Exceeds_rejected` · `dspBudget_noSamples_notWithinBudget` | 2 |
| ⑥ percentile | `int idx = ceil(...) - 1;` → `int idx = 0;` | `dspBudget_percentile_nearestRank` | 1 |
| ⑦ antivacuous | `if (points.Count == 0)` → `if (false)` | `measurementRegistry_emptySet_isRed` | 1 |
| ⑧ nullguard | `if (points == null)` → `if (false)` | `measurementRegistry_null_isRed` | 1 |
| ⑨ emitdetect | `if (string.Equals(points[i], EmitSymbol, ...))` → `if (false)` | `measurementRegistry_emitInjected_isRed` | 1 |
| ⑩ vrfield | 扫描面去掉**字段**分支 | `vrAudio_scanSurface_catchesVrMembers` | 1 |
| ⑪ anchor | 锚定判据退化为裸 `Contains(guardrailTitle)` | `guardrailAnchor_rejectsBareMention` | 1 |

**原始输出**:见 `unity/Logs/s144-{tag}.xml` 逐例(命令与 tag 同上表)。

### §3.1 ⚠️ 两处**弱判别面** —— 由证伪轮当场暴露,已修(如实登记)

**(a) 例 ⑩ 首轮得 0 红** —— 我扩展的 E1 三个面(字段 / 属性 / 装配引用集)在当前 44 生产代码下
**零命中** ⇒ 删掉它们主测照样绿 = **不可证伪的弱判别面**(不是假绿,但判据强度无法被检验)。
**修法**:抽 `ScanVrViolations` 共用方法 + 新增**负向夹具自证测**
`test_vrAudio_scanSurface_catchesVrMembers`(注入 `VrLeakFixture`:VR 字段 / 属性 / 返回类型)
⇒ 重跑例 ⑩b 转红 1 条。

**(b) 🔴 判据自证测当场抓到我自己写的锚定缺陷** —— 新增 `test_guardrailAnchor_rejectsBareMention`
首版**转红**,报 `非行首列不得通过(锚点 = 行首): Expected False But was True`。
**根因**:首版 `HasGuardrailRow` = `text.Contains("| **" + title + "**")` ——
`| 说明 | **G1 禁帧对齐** |`(G1 在**中间列**)同样以 `| **` 开头 ⇒ 通过。
**修法**:锚点改**真行首**(`StartsWith` 或 `"\n" + anchor`)⇒ 22 测全绿,例 ⑪ 转红 1 条。

> **登记理由**:这正是证伪轮的价值 —— 它抓的不是生产码,是**我自己的判据**。
> 与 S14-3 的假绿夹具同型,**第二次在同一 sprint 内由证伪轮抓到撰写者自己的缺陷**。

## §4 真种子端到端(sprint-14 计划点名的判据 ③)

sprint-14 计划逐字要求:**「构建期真门:改种子 `xfade_ms` 至 1999 ⇒ 红」**。

```
[真种子 1999] failed=2 total=334 还原OK
   RED: test_musicXfade_aboveMin_accepted
   RED: test_realSeedAudioEvents_zeroErrors
```

- `test_realSeedAudioEvents_zeroErrors` 转红 ⇒ **真表过全门**这条判据在真数据上生效
  (`assets/data/audio_events.json` 的乐层行被门拒)。
- 还原后逐字节校验通过(`"xfade_ms": 4000` 计数 = 1)。

## §5 回归终值

**`3552 / 3506 / 0 / 46`**(`unity/Logs/s14-4-final.xml`,2026-10-16)

- 基线(S14-3)= `3542 / 3496 / 0 / 46`
- ⇒ **+10 / +10 / 0 / 46 不变,零回归**(`music_guardrails_test` 12 → **22** 测)
- `Audio` 过滤套件 = **334 / 333 / 0 / 1 skipped**

**验证命令**:
```bash
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/s14-4-final.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
# ⇒ 3552 3506 0 46
```

## §6 门保持

| 门 | 判据 | 结果 |
|---|---|---|
| b5 | 含 `AudioCueDto`/`IAudioCueSink`/`AudioCueHandle`/`TierSource` token 的文件须声明 44 命名空间 | ✓ 2 新文件全在 `DaYiJingCheng.Gameplay.Presentation.Audio`(且 `AudioMeasurementPoints` **真的引用** `IAudioCueSink`) |
| b3 | 44 公开签名里的项目类型须白名单 | ✓ 新增公开面仅 `IReadOnlyList<string>` / `double` / `bool`(BCL) |
| b4 | 字段类型白名单 | ✓ `AudioMeasurementPoints` 字段 = `IReadOnlyList<string>` + `string`;`AudioBudgetProbe` **零字段** |
| ADR-029 §③ | `Sim/` 内 `new PayloadRef(` | ✓ 真代码零命中(本件不涉) |

**新增门(本件)**:`AudioEventTableGates.ValidateXfade` 的**下界比较**已进 `Validate` 聚合链
(`:279`)⇒ 构建期硬失败(经 Story 010 烘焙管线 throw),AC-44-18① 逐字「构建期数值比较,超界 = 失败」。

## §7 NOT-RUN 如实登记(禁借绿)

- **AC-44-E2 的实测半边**(60 s 窗 `Audio.Process` / DSP 回调 P95 实测值)—— **NOT-RUN**:
  须真 GPU + 真音频回调(EditMode 无 DSP 图)。本件只交付**判据 + 分位算法**;
  实测归 `production/qa/evidence/`(待真 player 跑)。
- **AC-44-E1 的 P1b 交付切面实测** —— **NOT-RUN**(P1b,ADR-018 §七)。
- **AC-44-11 VR 听测** —— **BLOCKED-BY-P1b**(GDD 已标记;本件只保 AC 存在与挂点正确,禁勾)。
- **IL2CPP 三格**(ADR-012 矩阵)—— **NOT-RUN**:本机仅 Mono;CI 真 runner 未配。

## §8 登记项(本批不做)

- **story-012 转 `Complete`** —— 归 **S14-7**(6 条复评批,`sprint-14.md` DoD 逐字要求)。
- **`AudioMeasurementPoints` 归属收口** —— 本类承载的是 **44 侧可静态核对的那一半**;
  `AC-10-08` 是**系统 10** 的 AC,10 侧真注册表落地时**本类应改为读它**
  (同 `MixerRegistry` ← `MixerTopologyGates` 的「引用禁另抄」纪律)。已在文件头「归属诚实声明」登记。
- **素材接入 / 烘焙管线**(story-010)—— `assets[]` 存在性谓词仍 NOT-RUN(归 S14-6)。
