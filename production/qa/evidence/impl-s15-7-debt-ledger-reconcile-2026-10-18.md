# S15-7 实现原件 —— 债册刷账批(sprint-15 收口)

**日期**:2026-10-18
**对象**:`docs/tech-debt-register.md`(8 项刷账 + 新专节)· `production/epics/audio-system/story-016-*.md`(E5 reconcile 注)
· `production/qa/evidence/impl-s18-runtime-audio-assembly-2026-10-18.md`(误记订正)
· `production/epics/audio-system/story-021-*.md` + `production/sprints/sprint-15.md`(「全仓」口径收窄)
**类型**:Config/Data(纯账面;零代码改动)
**判定口径**:每笔刷账须先**只读对码**得出结论,再改声明;「声明与代码不一致」时**以代码为准**。

---

## 一、原判定(sprint-15 S15-7 卡面逐字)

| # | 卡面要求 |
|---|---|
| A | **台账与代码逐条对码**;「账面滞后」同型已出现三例 ⇒ **只改声明不改代码 = 不合格** |
| B | 三项具体内容:**TD-audio-103 阻塞源改口** + **story-016 reconcile** + **本 sprint 各件结案注** |

---

## 二、对码方法(可复算)

1. 派只读勘察 agent 对 10 个编号命题逐条 grep + 行号实测(返回结论 + `file:line` 证据);
2. **逐条自验**:对其给出的每条 `file:line` 用 `sed`/`grep` 复核(本批复核实测值见 §四);
3. 只对**声明**做 Edit;凡代码侧与声明不符,**一律不改代码**(本批零代码改动,符合 S15-7 的 Config/Data 性质);
4. 判定分三态:**声明错/代码对**(改声明)· **声明对**(不动)· **声明半真**(改措辞,不改判定)。

---

## 三、逐项结论(8 项刷账 + 1 项复核通过)

| # | 项 | 原声明 | 实测 | 处置 |
|---|---|---|---|---|
| 1 | **TD-audio-103** 阻塞源 | 「变体素材未落地」 | ❌ **过期** —— `unity/Assets/Audio/` 下 `vo_patient_cough_damp_{m,f}{1,2,3}_{1,2}.wav` **12/12 在场**,`assets/data/audio_events.json` `Voice_Cough_Damp.assets[]` 逐条引用(15 行 / 38 assets,缺失 0) | 债行加「✅ 阻塞源改口」注:**真阻塞 = 被试 ≥2 人**;触发条件表「变体素材落地后」行同步改口(消解) |
| 2 | **story-016 AC-44-E5** | 卡面 `:83/:166/:190` 三处「dr_guyang 签字 [x]」 | ❌ **相抵** —— 签核载体 `docs/audio/statereport-walkthrough-2026-10-17.md:32` 实为 **`[ ] 通过`**,其 `:34` 自注逐字「**未勾选 = E5 未通过**」;`443a413` 的 message 写「签字」而内容仍是 `[ ]` | 专节,见 §五 |
| 3 | **TD-audio-030** 结案注 | 选择器 = `VoiceVariantLib.SelectVariant` + `SelectSlot` | ❌ **名实不符** —— 生产是 **`SelectVariantCube`**(`AudioClipLibrary.cs:182`);`SelectVariant`(`:197`)/`SelectSlot`(`:266`)仍存在但**生产零调用**;且只有 `variant_axis` 已声明的行走选择器 | 就地加订正块,**不改判定**(结案成立);连带订正 identity/Intensity 轴归属(见 §六) |
| 4 | **TD-audio-032 ③** | 未刷 `BuildAudioSeams` 行号(债行 `:504-550`/≈47 行) | 现行 `BootRoot.cs:513-559`,方法体 **45 行**(仍 >40) | 结案注刷行号 + 明写「③ 的该半边仍 open」 |
| 5 | **TD-audio-032 ④** | 「稳态每拍零分配」 | ⚠️ **半真** —— 数组分配省了;`PruneAbsent` 每拍仍 `_presentScratch.Clear()` + 全量 `foreach` 拷进集(**O(N) 拷贝仍在**) | 措辞订正为「零**分配**,拷贝仍在」,**不改判定** |
| 6 | **TD-audio-031** | 引 `:479/:541/:520-546/:709-738` | 四处**全漂移** —— 现行 `:488`/`:542`/`:513-559`/`:725-757`;**两半均未修**(OnDestroy 无置 null;两条抛路径前无 `Dispose`) | 债行刷行号 + 明写「S15-7 复核:两半均未修」 |
| 7 | **TD-audio-033** / `story-021:43` / `sprint-15:46` | 「**全仓**不得再出现 `knowsClinic: false`」 | ⚠️ 生产树成立、**「全仓」不成立** —— `Tests/EditMode/PatientAI/behavior_map_test.cs:61` 有一处(**夹具必需**);源码门口径本就是「`Tests/` 之外的生产树」(`knowsclinic_seam_test.cs:245`) | 三处措辞同步收窄为「**生产树内**」,**不改判定** |
| 8 | **`assets/audio/`** 路径 | 债册触发条件表仍以该路径叙述 | ❌ 全仓无此目录(只有 `assets/audio-staging/` + `unity/Assets/Audio/`) | **本批不改**(不在 AC 面内),登记为待订正 |

**复核通过、无需改动的项(声明与代码一致)**:TD-audio-028 四项结案判据逐条成立 ·
TD-audio-029 四 API + `OneShotReclaimPump` + tick 边沿调用全部在场 · TD-audio-032 ②(全 long)· ③ `OnTickEdge` 各段
9/24/23/12 行 + 夹具自校准 · ④ 快照三处失效 + `TrackedPatientCount` · TD-audio-035 三项代偿常量仍在 ·
`TD-audio-034/035` 已登记且 **036–039 零命中**(无游离债号)。

---

## 四、复核实测值(第三方可重跑)

```bash
# ① 12 个变体在场
ls unity/Assets/Audio/vo_patient_cough_damp_*.wav | wc -l            # 12
# ② 生产选择器是 Cube,不是 SelectVariant
grep -n "SelectVariant" unity/Assets/Gameplay.Presentation/Audio/AudioClipLibrary.cs
#    :182  int variant = VoiceVariantLib.SelectVariantCube(cue, tier, identity, size);
grep -rn "SelectVariant\b\|SelectSlot" unity/Assets --include=*.cs | grep -v Tests   # 零生产调用
# ③ BuildAudioSeams 行界
awk 'NR>=513 && NR<=559' unity/Assets/Gameplay.Boot/BootRoot.cs | wc -l              # 47(含签名/收尾)
#    方法体 { → } = :515-559 ⇒ 45 行
# ④ 031 现行行号
grep -n "AudioSettingsRegistry.Provider =\|PlayerBusVolumeInitializer.MixerResolver =\|void OnDestroy" \
  unity/Assets/Gameplay.Boot/BootRoot.cs            # :488 / :542 / :725
# ⑤ 测试侧 knowsClinic 字面量(生产侧零)
grep -rn "knowsClinic: false" unity/Assets --include=*.cs
#   → Tests/EditMode/PatientAI/behavior_map_test.cs:61(夹具必需)+ 两处生产注释
# ⑥ E5 签核格与 predicates
sed -n '30,34p' docs/audio/statereport-walkthrough-2026-10-17.md
python3 -c "import json;d=json.load(open('docs/audio/statereport-walkthrough-predicates.json'));\
print(d['reviewer'], d['date'], len(d['verdicts']), all(v['status_words'] is None for v in d['verdicts']))"
#   → dr_guyang 2026-10-17 15 True
# ⑦ 10-18 副本与 10-17 逐字节相同(且已入库)
md5sum docs/audio/statereport-walkthrough-2026-10-1{7,8}.md docs/audio/statereport-walkthrough-manifest-2026-10-1{7,8}.json
git log --oneline --diff-filter=A -- docs/audio/statereport-walkthrough-2026-10-18.md   # 910aa79
```

---

## 五、story-016 `AC-44-E5` reconcile(本批正题)

**结论:人耳判定内容真实存在且可复核,但签核载体与卡面声明相抵 —— 按该文件自订判据,E5 不得判通过。**

**事实固定(可复算)**:

| 半 | 状态 | 证据 |
|---|---|---|
| 机器半 | ✅ 齐 | `statereport-walkthrough-predicates.json`:15 条 verdicts,`reviewer: dr_guyang`,`date: 2026-10-17`,**全部 `status_words: null`**,`not_reporting` 逐条非空 |
| 记录半 | ✅ 有判定 | `statereport-walkthrough-2026-10-17.md` 表 15 行判定列全为「✅ 不是状态播报」 |
| 签核半 | ❌ **空** | 同一文件 `:32` = `[ ] 通过`;`:34` 自注逐字「未勾选 = E5 未通过」 |

⇒ 判据自相矛盾地指向两个结论。**按该文件自订的规则(不是外部强加的),E5 维持未通过。**

**处置(三件事,均不代签/不追认/不追改)**:

1. story-016 卡面三处「签字 [x]」旁加 reconcile 注(内容真实 / 签核未勾 / E5 **不得**据本件判通过);
   `AC-44-E5` 的 ✅ **暂回退为「内容齐 / 签核缺」**,出路 = 用户补勾或明确裁定「内容即判据」;
2. **回退范围仅 E5 这一个签核格** —— 不波及 E1–E4(构建期门,与签核无关),**不改 story-016 的 `Status: Complete`**
   (10-17 的裁定,承「评审/裁定不可事后追补」);
3. 顺带固定一处**上游账面滞后**:`impl-s18-*:330-332` 写「10-18 副本已删除」—— 实测**未删除**,两份与 10-17 原件
   **逐字节相同**(md5 `1ad6ba93…` / `152ea3b0…`),并已随 S15-5(`910aa79`)**入库**。成因 = probe 的
   `RecordFileName()`/`ManifestFileName()` 按**当天日期**命名 + `WriteOnce` 当天不覆盖 ⇒ 跨天重跑**必然**产出同内容副本。
   就地订正该句,副本去留**交回用户裁**。

---

## 六、连带订正(030 的轴归属,不改任何判定)

`SelectVariantCube(cue, tier, identity, size)` 的 `identity` 在生产 = **`AudioCueDto.Source`**
(`AudioClipLibrary.cs:192-194`:`ResolveCue` → `Source >= 0 ? Source : Cue`;咳嗽链
`PatientCueEmit.cs:52` `source: patientId`;呼吸链 `PatientCueDriver.cs:454` 同)⇒ **患者身份轴已接线**;
而 GDD 定义的 **`Intensity` 桶轴仍未接**(`AudioCueDto.Intensity` 在音频选择路径零消费)—— 归 **`TD-audio-034` ①**,
不在 TD-audio-030 结案面内,本批只在结案注里标明归属,不扩张结案范围。

---

## 六·五、用户裁定落地(2026-10-18 · 两项全落 · 附证伪轮)

**裁定原文**:「E5 选 A,副本选 A,顺手做 C 的治法」。

### ① E5 = A(维持未通过)

签核格 `[ ] 通过` 与文件自订判据「未勾选 = E5 未通过」**一致** ⇒ **E5 就是 ✗**,不存在「账面造假」,
只存在「半份证据被当成整份」。

**本批新测出的一层(比「缺一个勾」更强)**:谓词文件 15 条 `not_reporting` **与 probe 预填的建议稿逐字相同** ——
`_instructions` ④ 自陈「这是**默认判定**,不是你听过的结论」,而 `ReplayPredicatesFromText` 对 `not_reporting`
**只校验非空**(`statereport_walkthrough_probe.cs:697` 一条 `IsNullOrWhiteSpace`),**没有任何机制要求它必须被改过**。
⇒ `status_words: null` 既可能是「听完确实没有」也可能是「没人填」;
记录表 15 行判定列的「✅ 不是状态播报」是机器从 null 推出的,**不是人写的**。

**为何否 C(改判据口径)**:那会让账绿而把「预填稿即判定」固化,且是改规则迁就证据。
**出路(裁定 A 的兑现形态)**:走查人真听一遍 → 把 15 条 `not_reporting` **改写成自己的话** →
`status_words` 保持 null → 重跑 probe 生成新记录 ⇒ 人半才有「人听过」的物证。**现有判据就够,不立新机制。**

### ② 副本 = A(删)+ C(治法)

**失效形态实测两处**(治法前):
- manifest 走 `WriteOnce`(当天已存在则不覆盖)+ **按当天日期命名** ⇒ 跨天重跑**必然**产出同内容副本
  (10-18 两份与 10-17 原件 md5 全同,且已随 `910aa79` 入库);
- record 走**无条件** `File.WriteAllText` ⇒ 同天第二次重跑**静默改写**已签核记录(比副本更重:销毁证据)。

**治法**:文件名「当天日期」→「**内容哈希**」;`WriteOnce` 判据「当天已存在」→「**内容相同**」;
且**异内容一律拒写**(`Debug.LogError` + 不覆盖)⇒ 两个失效形态同时拆掉。
副作用如实登记:文件名不再含日期,日期信息由正文承载(与「不用位置编码时间」同型纪律)。

**判据三条**(`StateReportWalkthroughProbe` **16/16 绿**,`unity/Logs/s157-e5-probe.xml`):

| 夹具 | 钉什么 |
|---|---|
| `fileName_isContentAddressed_notDateAddressed` | 同内容两次命名同一路径(副本根因)+ 异内容不同路径 |
| `contentHash_isDeterministic_andHex16` | 确定性 + 异内容异值 + 16 位小写 hex |
| `writeOnce_sameContent_isNotRewritten_differentContent_isRefused` | 同内容不重写 + **异内容拒写**(不覆盖已签核件) |

**证伪轮 G1–G3(全部转红,还原 SHA 一致 = `5501eb6c…`)**:

| # | 改坏 | 转红 |
|---|---|---|
| G1 | 文件名回到日期戳 | `fileName_isContentAddressed_notDateAddressed` |
| G2 | `WriteOnce` 改回无条件覆盖 | `writeOnce_sameContent_isNotRewritten_differentContent_isRefused` |
| G3 | 哈希改成常数 | `contentHash_isDeterministic_andHex16` + `fileName_isContentAddressed_notDateAddressed`(2 红) |

⚠️ **治法侧如实登记:判据首轮就是红的** —— 首版实现只靠「内容哈希命名」保证异内容换路径,
`WriteOnce` 本身**仍会覆盖**;是断言把覆盖打红后才补上拒写分支。**命名纪律 ≠ 断言兜底。**
(第二轮又撞一次 UTF 的 Unhandled log:`Debug.LogError` 被当失败日志 ⇒ 用 `LogAssert.Expect` 声明为设计内告警。)

---

### 治法跑一轮的产物如实登记(4 份 → 3 份)

治法后的全量回归顺带跑了 probe ⇒ `docs/audio/` 一度产出 **4 份**新文件,逐个判定后清理:

| 文件 | 判定 | 处置 |
|---|---|---|
| `…walkthrough-0000000000000000.md` | 证伪期 **G3 变异态**(常数哈希)的产物 | **删**(污染残留) |
| `…manifest-0000000000000000.json` | 同上 | **删** |
| `…walkthrough-bf2fe107d8e9cbff.md` | 与在库 10-17 record **逐字节相同**(md5 `1ad6ba93…`)⇒ 治法**正确地复用了同一路径** | **删**(内容与在库件重复) |
| `…manifest-c48baa7e9553230e.json` | 与在库 10-17 manifest **内容不同**(仅 `event_table_config_version` 511471757 → 2044814530,因 `4d64747` story-020 重裁改了事件表)⇒ 治法**正确地换了路径、没覆盖旧件** | **删**,但**新事实登记在案**:ConfigVersion 已变 ⇒ 10-17 那份 manifest 是**旧表快照**;待 E5 人半补做时同批重生成 |
| `…manifest-2026-10-18.json`(untracked 副本) | 与 `c48baa…` 同内容不同名(治法前的旧命名遗留) | **删** |

⇒ **`docs/audio/` 回到三件**(10-17 record + 10-17 manifest + predicates)。
**这一轮反过来验证了治法判据是对的**:同内容复用路径(record)、异内容换路径且不覆盖(manifest)——
**两个不变式都在真实数据上成立过一次**,不是只在夹具里成立。

---

## 七、回归

**本批主体无代码改动 ⇒ 前半不跑测试**;但**用户裁定的治法轮改了 probe 代码 + 加 3 条夹具** ⇒ 后半**跑了**。

| 项 | 值 | 出处 |
|---|---|---|
| 本夹具单跑(治法后基线) | **16 / 16 / 0** | `unity/Logs/s157-e5-probe.xml` |
| 证伪轮 G1–G3 | **全转红**(还原 SHA `5501eb6c…` 一致) | §六·五表 |
| 全量 EditMode(本件后) | **3797 / 3751 / 1 / 45** | `unity/Logs/s157-full-editmode.xml` |
| 全量 EditMode(基线 S15-5) | `3794 / 3748 / 1 / 45` | `unity/Logs/s155-full-editmode.xml` |

**等式对账**:`3797 − 3794 = 3` = 本批新增夹具数;`passed 3751 − 3748 = 3`;`failed` / `skipped` 不变。

**唯一红**:`MovementFeedTest.test_move_afterFreeFall_horizontalDisplacementBoundedByConfig` —— 已知间歇
(`TD-player-01`),**不用于抵扣计数**,归系统 1 实现轮。

> ⚠️ **过程中一次读数污染(如实登记)**:证伪脚本最后一次运行停留在 **G3 变异态**,其 `test-results.xml`
> 被我直接 `cp` 成 fixture 日志 ⇒ 首份 `s157-e5-probe.xml` 记的是 **16/14/2**(变异态,2 红为预期)。
> 发现后重跑基线覆盖为 **16/16/0**。**教训**:证伪轮的产物**不得**直接当基线日志用 ——
> 变异态与基线态必须分文件(同族于 S15-5 的「Skipped 被读成红」,同一病灶的第三次:
> **把过程态当结果态**)。

---

## 八、如实登记(禁借绿 · 本批未做 / 未裁的部分)

| 项 | 状态 | 说明 |
|---|---|---|
| **E5 签核格的补勾** | ✅ **已裁 = A(维持未通过)** | 用户裁定「E5 选 A」。⇒ **不补勾**;补勾只能让账面变绿,`not_reporting` 与预填稿逐字相同这一事实不因签字改变(第三次「✅ 早于事实」的规避) |
| **10-18 两份副本的去留** | ✅ **已裁 = A(删)+ C(治法)** | 副本 `git rm` 已删;probe 根因已治(内容哈希命名 + 内容判等 + 异内容拒写) |
| **`assets/audio/` → `unity/Assets/Audio/` 路径订正** | **未做** | 不在 S15-7 AC 面内;归 `OQ-44-2` 同族(需 GDD/ADR-018 §五 补注,承 ADR-015 修订 ADR-009 先例) |
| **TD-audio-031 两半的修复** | **未做** | 本批只刷行号 + 明写未修;触发条件不变 |
| **TD-audio-032 ① / `BuildAudioSeams` 拆分 / `isPlaying` PlayMode 半边** | **仍 open** | 各自触发条件不变 |
| **TD-audio-101/102/103 听测** | **仍 open** | 103 的阻塞源已改口(被试 ≥2 人),**未解除** |
| **TD-audio-034 ①(`Intensity` 桶轴)** | **仍 open** | 本批只登记归属 |
