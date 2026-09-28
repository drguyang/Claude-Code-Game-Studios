using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class RunEditModeTests
{
    [MenuItem("Tools/Run EditMode Tests")]
    public static void RunTests()
    {
        var testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
        var filter = new Filter()
        {
            testMode = TestMode.EditMode,
            assemblyNames = new[] { "SkeuomorphicUI.Tests" }
        };
        testRunnerApi.Execute(new ExecutionSettings(filter));
        Debug.Log("EditMode tests triggered for SkeuomorphicUI.Tests");
    }
}
