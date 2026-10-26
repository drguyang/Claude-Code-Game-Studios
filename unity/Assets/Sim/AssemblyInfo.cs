// Sim 程序集 —— 仅供夹具使用的内部面可见性声明。
//
// 为什么需要本文件:
//   `ClinicEnvironmentEvaluator.CollectContextIdsFromRows` 是 **internal** ——
//   它的存在理由就是「让夹具绕过 `ClinicContextTable` 构造期的 room_type 唯一性约束,
//   直接喂多行同 roomType 的行集」,使 P0 下不可达的排序属性可证伪
//   (2026-10-26 用户裁定 B 路;F7 存活分析见 story-005 证据件 §四)。
//   若把它改 public,生产面就凭空多出一个「可喂任意行集」的入口 —— 那不是本意。
//
// 承既有先例:`Sim.Codec/AssemblyInfo.cs` 对 `FixCodec` 用的是同一条声明
//   (`[assembly: InternalsVisibleTo("Sim.Contracts.Tests")]`),
//   ADR-025 §① 亦把 `Sim.Codec` 的 internal 编码器 + IVT 写进装配清单。
//
// ⚠️ 门 A 不受影响:本文件零 `using`,不引任何引擎程序集;`Sim.asmdef` 的
//   `references: ["Sim.Contracts"]` + `noEngineReferences: true` 一字未改。

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Sim.Contracts.Tests")]
