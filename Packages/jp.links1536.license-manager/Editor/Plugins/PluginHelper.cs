using System.Linq;
using UnityEditor;

namespace Links.Licenses.Plugins
{
	static class PluginHelper
	{
		public static BuildTarget[] BuildTargets = new BuildTarget[]
		{
			BuildTarget.StandaloneWindows,
			BuildTarget.StandaloneWindows64,
			BuildTarget.Android,
			BuildTarget.iOS,
		};

		public static bool IsInRuntime(string packageDirectory)
		{
			using var pool = DefineConstraintsHelper.GetActiveScriptCompilationDefines(out var activeScriptCompilationDefinesSet);

			var files = AssetDatabase.FindAssets("t:defaultasset", new string[] { packageDirectory })
				.Select(AssetDatabase.GUIDToAssetPath)
				.Where(x => !AssetDatabase.IsValidFolder(x))
				.Where(x => !UnityEditorInternal.InternalEditorUtility.IsInEditorFolder(x))
				.ToArray();
			foreach (var filePath in files)
			{
				// Resourcesフォルダはアプリに強制的に埋め込まれる
				if (filePath.Contains("/Resources/"))
					return true;

				if (PluginImporter.GetAtPath(filePath) is not PluginImporter importer)
					continue;

				if (!DefineConstraintsHelper.IsSatisfiedDefineConstraints(activeScriptCompilationDefinesSet, importer.DefineConstraints))
					continue;

				// AnyPlatformか
				if (importer.GetCompatibleWithAnyPlatform())
				{
					// 除外されているか
					foreach (var buildTarget in BuildTargets)
						if (!importer.GetExcludeFromAnyPlatform(buildTarget))
							return true;
				}
				else
				{
					// 含まれているか
					foreach (var buildTarget in BuildTargets)
						if (importer.GetCompatibleWithPlatform(buildTarget))
							return true;
				}
			}
			return false;
		}
	}
}
