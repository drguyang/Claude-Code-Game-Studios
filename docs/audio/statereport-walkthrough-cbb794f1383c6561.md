# AC-44-E5 无提示音铁律 · 手跑走查记录(2026-10-21)

> 机器半:`StateReportWalkthroughProbe.walkthrough_allCues_manifestPlaybackAndOrdering`(全 19 cue 真播)。
> 人半:走查人 `dr_guyang` 逐条耳朵判定(谓词文件重跑通过)。

**判定依据**:`design/gdd/audio-system.md:66-78` 规则二黑名单四禁(sting / jingle 宣告状态变化 · ducking 报状态 · 素材切换报状态 · 音频表达该由读数得出的结论)。

| # | cue | whitelist_category | 时长 | 字幕 | 参考解读 | 听到的状态结论 | 判定 |
|---|---|---|---|---|---|---|---|
| 0 | `Voice_Cough_Damp` | ContinuousPhysiology | 0.911 s | `咳…咳咳…` | physiology:Voice_Cough_Damp: whitelist_category = 「ContinuousPhysiology」∈ 白名单;替代读法 = 有(manifest §四);症状的连续变化本身即病人状态,不是「宣告」状态变化 | 无 | ✅ 不是状态播报 |
| 1 | `BreathLayer_Base_Calm` | ContinuousPhysiology | 1.95 s | — | physiology:BreathLayer_Base_Calm: whitelist_category = 「ContinuousPhysiology」∈ 白名单;替代读法 = 有(manifest §四);症状的连续变化本身即病人状态,不是「宣告」状态变化 | 无 | ✅ 不是状态播报 |
| 2 | `BreathLayer_Adventitious_Fine` | ContinuousPhysiology | 1.95 s | — | physiology:BreathLayer_Adventitious_Fine: whitelist_category = 「ContinuousPhysiology」∈ 白名单;替代读法 = 有(manifest §四);症状的连续变化本身即病人状态,不是「宣告」状态变化 | 无 | ✅ 不是状态播报 |
| 3 | `WorldBreath_Baseline` | ContinuousPhysiology | 1.95 s | — | physiology:WorldBreath_Baseline: whitelist_category = 「ContinuousPhysiology」∈ 白名单;替代读法 = 有(manifest §四);症状的连续变化本身即病人状态,不是「宣告」状态变化 | 无 | ✅ 不是状态播报 |
| 4 | `Music_Encounter_Layer` | MusicLayer | 23.316 s | — | music_layer:Music_Encounter_Layer: whitelist_category = 「MusicLayer」∈ 白名单;替代读法 = 有(manifest §四);乐层算在 E5 射程内(G1–G3 是判定口径非豁免)—— 听不出切换的那一刻才算过,听得出「哪一段新乐句 / 哪一拍告诉你遭遇开始了」即违规(按 G2 去标注盲测口径回炉) | 无 | ✅ 不是状态播报 |
| 5 | `World_Threat` | WorldPerceptible | 2.88 s | — | player_action:World_Threat: whitelist_category = 「WorldPerceptible」∈ 白名单;替代读法 = 有(manifest §四);该 cue 不承载字幕 —— 玩家凭声音描述动作,无结论被读出 | 无 | ✅ 不是状态播报 |
| 6 | `World_Opportunity` | WorldPerceptible | 2.224 s | — | player_action:World_Opportunity: whitelist_category = 「WorldPerceptible」∈ 白名单;替代读法 = 有(manifest §四);该 cue 不承载字幕 —— 玩家凭声音描述动作,无结论被读出 | 无 | ✅ 不是状态播报 |
| 7 | `World_Reaction` | WorldPerceptible | 0.559 s | — | player_action:World_Reaction: whitelist_category = 「WorldPerceptible」∈ 白名单;替代读法 = 有(manifest §四);该 cue 不承载字幕 —— 玩家凭声音描述动作,无结论被读出 | 无 | ✅ 不是状态播报 |
| 8 | `World_Disaster` | WorldPerceptible | 3.496 s | — | player_action:World_Disaster: whitelist_category = 「WorldPerceptible」∈ 白名单;替代读法 = 有(manifest §四);该 cue 不承载字幕 —— 玩家凭声音描述动作,无结论被读出 | 无 | ✅ 不是状态播报 |
| 9 | `Pulse_Contact` | PlayerExamAction | 0.651 s | — | player_action:Pulse_Contact: whitelist_category = 「PlayerExamAction」∈ 白名单;替代读法 = 有(manifest §四);该 cue 不承载字幕 —— 玩家凭声音描述动作,无结论被读出 | 无 | ✅ 不是状态播报 |
| 10 | `Steth_Contact` | PlayerExamAction | 0.858 s | — | player_action:Steth_Contact: whitelist_category = 「PlayerExamAction」∈ 白名单;替代读法 = 有(manifest §四);该 cue 不承载字幕 —— 玩家凭声音描述动作,无结论被读出 | 无 | ✅ 不是状态播报 |
| 11 | `Page_Turn` | PlayerExamAction | 1.319 s | — | player_action:Page_Turn: whitelist_category = 「PlayerExamAction」∈ 白名单;替代读法 = 有(manifest §四);该 cue 不承载字幕 —— 玩家凭声音描述动作,无结论被读出 | 无 | ✅ 不是状态播报 |
| 12 | `Footstep_Base` | PlayerAction | 2.306 s | — | player_action:Footstep_Base: whitelist_category = 「PlayerAction」∈ 白名单;替代读法 = 有(manifest §四);该 cue 不承载字幕 —— 玩家凭声音描述动作,无结论被读出 | 无 | ✅ 不是状态播报 |
| 13 | `Cabinet_Prop` | PlayerAction | 0.963 s | — | player_action:Cabinet_Prop: whitelist_category = 「PlayerAction」∈ 白名单;替代读法 = 有(manifest §四);该 cue 不承载字幕 —— 玩家凭声音描述动作,无结论被读出 | 无 | ✅ 不是状态播报 |
| 14 | `Clinic_Room_Tone` | WorldPerceptible | 3.52 s | — | player_action:Clinic_Room_Tone: whitelist_category = 「WorldPerceptible」∈ 白名单;替代读法 = 有(manifest §四);该 cue 不承载字幕 —— 玩家凭声音描述动作,无结论被读出 | 无 | ✅ 不是状态播报 |
| 15 | `Narration_TeachObserve_01` | WorldPerceptible | 2.388 s | `喘着气,气色、神态,都在脸上。` | world_ambience:Narration_TeachObserve_01: whitelist_category = 「WorldPerceptible」∈ 白名单;替代读法 = 有(manifest §四);该 cue 承载字幕「喘着气,气色、神态,都在脸上。」—— 声音先于文字,音形为可闻事实 | 无 | ✅ 不是状态播报 |
| 16 | `Narration_TeachJudge_01` | WorldPerceptible | 3.716 s | `哼了一声,看明白了,就翻本子记一笔。` | world_ambience:Narration_TeachJudge_01: whitelist_category = 「WorldPerceptible」∈ 白名单;替代读法 = 有(manifest §四);该 cue 承载字幕「哼了一声,看明白了,就翻本子记一笔。」—— 声音先于文字,音形为可闻事实 | 无 | ✅ 不是状态播报 |
| 17 | `Narration_TeachClose_01` | WorldPerceptible | 4.878 s | `叹口气,处置妥当,一个病例才算了结。` | world_ambience:Narration_TeachClose_01: whitelist_category = 「WorldPerceptible」∈ 白名单;替代读法 = 有(manifest §四);该 cue 承载字幕「叹口气,处置妥当,一个病例才算了结。」—— 声音先于文字,音形为可闻事实 | 无 | ✅ 不是状态播报 |
| 18 | `Narration_TeachArchive_01` | WorldPerceptible | 3.312 s | `哼着小曲,要紧的,都写进册子。` | world_ambience:Narration_TeachArchive_01: whitelist_category = 「WorldPerceptible」∈ 白名单;替代读法 = 有(manifest §四);该 cue 承载字幕「哼着小曲,要紧的,都写进册子。」—— 声音先于文字,音形为可闻事实 | 无 | ✅ 不是状态播报 |

**总计**:机器半全 19 cue 均在谓词文件内 · 人半 19 条判定。

## 签字

| 角色 | 姓名 | 日期 | 判定 |
|---|---|---|---|
| 音频 / QA(人耳走查) | dr_guyang | 2026-10-21 | [ ] 通过 |

> ⚠️ **未勾选 = E5 未通过**。机器半全绿只说明「清单齐、都出了声、文字没先于声音」;「声音本身不是在报状态」只能由此表的耳朵判定给出。
