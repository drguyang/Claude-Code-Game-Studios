# 实现证据 —— `TD-audio-027`:AC② 求交面收严至「玩家可设电平完整落点集」

- **日期**:2026-10-29
- **批次**:音频债批 C · `TD-audio-027`(全条 `open` → **`closed`**)
- **commit**:`8b25a049` + 工作树(见 §五 三元组)
- **对象**:`unity/Assets/Editor.Tools.Gates/MixerTopologyGates.cs` ·
  `unity/Assets/Audio/DaYiJingCheng.mixer` ·
  `unity/Assets/Tests/EditMode/Audio/{mixer_topology_test,breath_layers_test}.cs` ·
  `tests/integration/audio_system/fixtures/invalid_snapshot_captures_root_group.yaml`

---

## 一、原判定(债册原文,节选)

> `ValidateSnapshotExposedDisjoint` 把捕获键经各组 `m_Volume` 哈希反查成**组名**,再与
> `doc.Exposed`(参数名集)求交。`Master` 组的**原始电平**不在该参数名集里(它是根组内部电平,
> 非 exposed)⇒ 任何快照捕获 Master 原始电平均**判绿通过**。
> 实测触发样:`Default` 快照的 `m_FloatValues` 由 `{}` 变为 `{4b26ff…: 0}`。
> 失效形态是**将来式**:若日后把 Master 原始电平调成全局 headroom(如 −3 dB),
> `Default` 快照会在每次切入时**静默打回 0**。

**复核(本轮)**:判定成立,且**资产里已带着该形态**。

---

## 二、修复落点

| # | 落点 | 内容 |
|---|---|---|
| R-1 | `ValidateSnapshotExposedDisjoint` | 求交对象:exposed 参数名集 → **玩家可设电平完整落点集** = exposed ∪ **根组 Master** |
| R-2 | 同上 | 根组经 `AudioMixerController.MasterGroupId` 解析;**解析不到 ⇒ `NOT-RUN 守卫`**(拒空集冒充绿) |
| R-3 | `DaYiJingCheng.mixer` | `Default` 快照删掉 `4b26ff…: 0`(根组 Master 电平捕获),恢复 `m_FloatValues: {}` |
| R-4 | 新夹具 + 2 测 | `invalid_snapshot_captures_root_group.yaml` · 必红测 + **反向守卫**测 |
| R-5 | `BreathLayersTest` | 两条依赖「资产带 bug」的变异改为**自注入**(见 §四) |

### R-3 的来历(这是本条债的**活证据**)

```bash
git log -S "4b26ff2484ad0dd3dbba5a2756453a2e: 0" --oneline -- unity/Assets/Audio/DaYiJingCheng.mixer
# 60580ad6 feat(audio): story-018 运行期音频装配 —— A-1…A-7 七处缺口全补 + 实机听测结案
```

该行由 **story-018 听测提交**带入,与债册记的「听测期在 Mixer 窗口选中 `Default` 快照后动了
Master 电位器」逐字吻合 —— **编辑器误操作残留,非设计意图**。
它的意义在于:**门全程判绿,而资产里早已带着该形态** —— 这就是「求交面漏了根组」的可观测后果。

---

## 三、证伪轮(S12-4 形态)

驱动脚本:`/home/gu/.claude/jobs/181888b1/tmp/falsify027.py`

| 变异 | 打靶 | 期望 | 实测 |
|---|---|---|---|
| M1 | 停用收严(根组移出玩家可设集) | `test_snapshotCapture_rootGroupRawVolume_reportsRed` **转红** | **命中**(报文 `Expected: not <empty> But was: <empty>`) |
| M2 | 过度收严(全部组电平都当玩家可设) | `test_snapshotCapture_busGroupRawVolume_stillAllowed` **转红** | **命中**(连带 4 条合法夹具测) |

**2/2 成功;2/2 `RESTORE-OK`(`MixerTopologyGates.cs` SHA-256 回到 `fe76952b…`)。**

**M2 是反向守卫的判别力所在**:若把收严写成「任何组电平都不许捕获」,合法夹具
(`Paused` 捕获 7 条 `duck_*`)会整片误红 —— M2 证明根组特判确实在起区分作用,不是摆设。

---

## 四、本轮踩的两个坑(如实登记)

### (a) 我改资产击穿了两条既有夹具 —— **全量回归抓到的,不是自查发现的**

`BreathLayersTest` 的两条(`test_silentCaptureOnGroup_masterAtMinus74_red` /
`…_aboveLegacyFloor_zeroFromLegacyGate`)这样造变异:

```csharp
string sabotaged = yaml.Replace($"{masterVolumeHash}: 0", $"{masterVolumeHash}: -74.14725");
```

**前提是生产资产里带着那行 bug**。我修好资产后,替换找不到目标 ⇒ 变异不生效 ⇒ 两条红。
第二条的报文「变异未生效 —— Master 的捕获行没被替换到」正是**夹具自带的守卫** ——
说明夹具写得好,错的是它把「生产代码有缺陷」当成了前提。

**修法**:新立 `InjectDefaultSnapshotCapture(yaml, hash, db)`,**自己注入**该行,
两种形态都处理(`m_FloatValues: {}` 空捕获 / 已有捕获块)。
**夹具不该以「生产代码有缺陷」为前提。**

> **教训**:改资产 / 改门这类「收紧」动作,必须先跑全量看有没有夹具依赖**旧形态**。
> 这一次若跳过全量直接提交,两条红会留到下一轮才被发现。

### (b) 证伪判据方向写反 —— M1 最初被我报成「假存活」

我最初把 M1 的期望写成 `ABSENT`(期望该测试**从红列表消失**),而正确判据是
**停用守卫 ⇒ 该测试转红**。看报文 `Expected: not <empty> But was: <empty>` 才定案。

**这是本轮第二次同类失误**(前一次 = `TD-audio-032` 的「变异太弱,没越过阈值」)。
共同点:**证伪判据的方向 / 强度没先自检就写进脚本**。

---

## 五、回归三元组(S12-0 口径)

| 套件 | 截至时刻 | 批次 | commit | 结果 | 对照 |
|---|---|---|---|---|---|
| EditMode(修复前) | 2026-10-29 14:04 CST | 全量 | `8b25a049` + 工作树 | `5875 / 5836 / **2 failed** / 0 / 37` | **2 条真红**(§四 a) |
| EditMode(修复后) | 2026-10-29 14:12 CST | 全量 | `8b25a049` + 工作树 | **`5875 / 5838 / 0 / 0 / 37`**(exit 0) | `5873 + 2 = 5875` |
| PlayMode | 2026-10-29 14:04 CST | 全量 | `8b25a049` + 工作树 | **`238 / 234 / 0 / 2 / 2`**(exit 2) | 与基线**逐位相同** |

**EditMode 等式**:`5873`(上轮 `TD-audio-031` 的全量)`+ 2`(本轮 `mixer_topology_test` 新增两条)
`= 5875`。`passed` 由 `5836 → 5838` 同步 `+2`(修复前那 2 条红正是新增的两条 + 两条既有夹具红,
净效果见上表)。

**PlayMode 两条 Inconclusive 是既有具名占位**(`…_notRun`);exit 2 与基线同形,不写成绿也不写成红。

---

## 六、本轮**不**证

| 项 | 状态 | 理由 |
|---|---|---|
| 「Master 电平被调成 −3 dB 后,切快照真会打回 0」 | **NOT-RUN** | 需要真 PlayMode 跑混音器 DSP;EditMode 下 `SetFloat` 对任何名字都返回 false(`MixerRegistry` H-B 实测) |
| 其它快照(StethoscopeFocus 等)无根组捕获 | ✅ 已核 | 实测五快照的捕获键全部反查为 `duck_*` 组;只有 `Default` 曾有根组行(已删) |
