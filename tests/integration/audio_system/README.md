# integration/audio_system/

按系统分目录的集成测试账本路径(coding-standards §Testing Evidence by Story Type)。
**Unity 只编译 `unity/Assets/` 树** —— 本目录是**账本(登记)侧**,不是编译落点。

## Story 001(装配边界与 DTO 护栏 —— AC-44-B1 / B2 / B3 / B4)—— 落点说明

故事头登记的证据路径为
`tests/integration/audio_system/assembly_boundary_test.cs`,但该路径在仓库根,
**Unity 不编译 `unity/Assets/` 之外的代码** ⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/Audio/assembly_boundary_test.cs`**

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/Audio/assembly_boundary_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(name = `Sim.Contracts.Tests`) |
| 被测门(b5) | `unity/Assets/Editor.Tools.Gates/AssemblyGates.cs`(`RunAudioBoundary` / `CheckAudioAssemblyIl` / `CheckPresentationReferenceSet` / `CheckAudioSourceText`) |
| 被测守卫 | `unity/Assets/Editor.Tools.Gates/PresentationDtoGuard.cs`(`Scan` / `AssertNoDiseaseId`) |
| 运行方式 | `unity test unity --mode EditMode --filter AssemblyBoundary` |

> 承 `tests/integration/item_database/README.md` 同一先例:账本路径登记 + 真身落
> `unity/Assets/Tests/` 树,两处以本 README 互链。

## AC → 测试函数映射

| AC | 测试函数(`AssemblyBoundaryTest` 内) | 性质 |
|---|---|---|
| **AC-44-B1 ①**(类型级 IL 扫描) | `test_ilScan_negativeFixtureInTestAssembly_flagged`(端到端负例:对测试装配产物跑同一谓词)· `test_fullGate_currentTree_zeroErrors`(生产产物正例) | BLOCKING |
| **AC-44-B1 ①b**(源文本层:using / nameof / 反射字符串) | `test_sourceScan_forbiddenUsingAndTypeName_reportsRed` · `test_sourceScan_commentMention_notFlagged` · `test_sourceScan_reflectionString_reportsRed` | BLOCKING |
| **AC-44-B1 ②**(引用集 = 登记集 ∪ 引擎白名单 ∪ 基线) | `test_referenceSet_newDrift_reportsRedWithThreeSegments` · `test_referenceSet_baselineSim_warnsNotRed` · `test_referenceSet_simCodecBlacklist_reportsRed` · `test_referenceSet_engineAndBcl_pass` | BLOCKING |
| **AC-44-B1 ③**(DOTS 黑名单) | `test_referenceSet_dotsFamily_reportsRedUnconditionally` | BLOCKING |
| **AC-44-B1 三段总门** | `test_fullGate_currentTree_zeroErrors` | BLOCKING |
| **AC-44-B2**(守卫递归扫描 `AudioCueDto`) | `test_dtoGuard_audioCueDto_noDiseaseField`(正)· `test_dtoGuard_nestedPrivateField_reportsRed`(负①嵌套+私有)· `test_dtoGuard_listElementField_reportsRed`(负②泛型元素)· `test_dtoGuard_baseClassPrivateField_reportsRed`(负③基类私有)· `test_dtoGuard_assertThrows_onViolation` | — |
| **AC-44-B3**(入口白名单 / 公开接口仅 `IAudioCueSink`) | `test_entryPoints_production44_noUnknownEntry`(正)· `test_entryPoints_vitalsDtoParam_reportsRed`(负)· `test_entryPoints_simSignature_reportsRed`(负)· `test_entryPoints_wrongInterface_reportsRed`(负)· `test_audioCueSink_onlyIntoNoStateOut`(契约只入不出) | — |
| **AC-44-B4**(字段类型白名单) | `test_fieldTypes_production44_withinWhitelist`(正,44 空集平凡成立)· `test_fieldTypes_simStateAndEngineNonAudioField_reportsRed`(负) | — |

## 负例夹具落位(不污染生产扫描面)

- **B1 端到端**:文件末命名空间块 `DaYiJingCheng.Gameplay.Presentation.Audio` 下的
  `IlScanNegativeFixture`(住**测试装配** `Sim.Contracts.Tests`)—— 零新增 asmdef
  (b3 Manifest 封闭性会拒),真编译产物真 IL;对 `Library/ScriptAssemblies/Sim.Contracts.Tests.dll`
  跑与生产同一谓词 `CheckAudioAssemblyIl`。
- **B1 源文本**:合成文本喂 `CheckAudioSourceText`(免把生产源写坏)。
- **B2**:测试类私有夹具(不在 44 前缀下)。**B3/B4**:显式传入类型列表(纯函数面)。
