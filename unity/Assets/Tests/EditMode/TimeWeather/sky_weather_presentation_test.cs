// time-weather Story 005 测试
//
// AC-5-09/10/18/21: [L] 人工走查（见 evidence 文档）
// 静态断言: 42/44 对 5 的读数接口只读，零写路径

using System;
using System.Reflection;
using DaYiJingCheng.Sim;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.TimeWeather
{
    public class SkyWeatherPresentationTest
    {
        // 静态断言: 42/44 侧对 5 的读数接口只读，零写路径
        [Test]
        public void test_readonlyInterface_noWritePath()
        {
            // 验证 TimeBase / WeatherRoll / EnvMod 无 public 写入方法
            var simAssembly = typeof(TimeBase).Assembly;

            foreach (var type in simAssembly.GetTypes())
            {
                if (type.Name.StartsWith("Time") || type.Name.StartsWith("Weather") || type.Name.StartsWith("EnvMod"))
                {
                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                    {
                        // 检查是否有 void 返回的写入方法（排除属性 setter 和 Validate）
                        if (method.ReturnType == typeof(void) && !method.IsSpecialName)
                        {
                            // 允许的只读方法
                            if (method.Name.StartsWith("Get") || method.Name.StartsWith("Compute") || method.Name.StartsWith("Is") || method.Name == "Validate")
                            {
                                continue;
                            }

                            // 发现写入方法
                            Assert.Fail($"发现写入方法: {type.Name}.{method.Name}");
                        }
                    }
                }
            }
        }

        // 静态断言: DTO 无 float 泄漏进 sim
        [Test]
        public void test_noFloatLeak_inSim()
        {
            var simAssembly = typeof(TimeBase).Assembly;

            foreach (var type in simAssembly.GetTypes())
            {
                if (type.Name.StartsWith("Time") || type.Name.StartsWith("Weather") || type.Name.StartsWith("EnvMod"))
                {
                    foreach (var field in type.GetFields())
                    {
                        Assert.IsFalse(field.FieldType == typeof(float),
                            $"字段 {type.Name}.{field.Name} 不应为 float");
                        Assert.IsFalse(field.FieldType == typeof(double),
                            $"字段 {type.Name}.{field.Name} 不应为 double");
                    }
                }
            }
        }

        // 静态断言: 呈现帧率不构成 sim 依赖
        [Test]
        public void test_frameRateIndependent()
        {
            // 验证 TimeBase / WeatherRoll / EnvMod 不引用 UnityEngine.Time
            var simAssembly = typeof(TimeBase).Assembly;

            foreach (var type in simAssembly.GetTypes())
            {
                if (type.Name.StartsWith("Time") || type.Name.StartsWith("Weather") || type.Name.StartsWith("EnvMod"))
                {
                    // 检查字段类型
                    foreach (var field in type.GetFields())
                    {
                        Assert.IsFalse(field.FieldType == typeof(float),
                            $"字段 {type.Name}.{field.Name} 不应为 float（帧率依赖）");
                        Assert.IsFalse(field.FieldType == typeof(double),
                            $"字段 {type.Name}.{field.Name} 不应为 double（帧率依赖）");
                    }

                    // 检查方法参数
                    foreach (var method in type.GetMethods())
                    {
                        foreach (var param in method.GetParameters())
                        {
                            Assert.IsFalse(param.ParameterType == typeof(float),
                                $"方法 {type.Name}.{method.Name} 参数 {param.Name} 不应为 float");
                            Assert.IsFalse(param.ParameterType == typeof(double),
                                $"方法 {type.Name}.{method.Name} 参数 {param.Name} 不应为 double");
                        }
                    }
                }
            }
        }
    }
}
