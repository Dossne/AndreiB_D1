using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class AndroidBuildUtility
{
    private const string OutputPath = "Builds/Android/SnakeMaze.apk";

    public static void BuildAndroidApk()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath)!);

        var buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = new[]
            {
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/SampleScene.unity"
            },
            locationPathName = OutputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException($"Android build failed: {report.summary.result}");
        }
    }
}
