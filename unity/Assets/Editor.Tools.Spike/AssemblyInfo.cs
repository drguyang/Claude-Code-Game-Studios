using System.Runtime.CompilerServices;

// S12-2(2026-10-15):EditMode 护栏测试须直调 Story009WalkthroughDriver 的
// internal Finish/Check/LegResult 面。先例 = Sim.Codec/AssemblyInfo.cs(同款 IVT,
// ADR-025 §① 载明的「internal + InternalsVisibleTo("Sim.Contracts.Tests")」成例)。
// 注:本程序集 Editor-only(includePlatforms: ["Editor"]),IVT 不触及门 A,
// 亦不影响任何出货构建 —— 测试程序集本身亦 Editor-only。
[assembly: InternalsVisibleTo("Sim.Contracts.Tests")]
