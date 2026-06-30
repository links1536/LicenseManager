using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Links.Licenses.Plugins
{
	static class AssemblyDefinitionHelper
	{
		/// <summary>
		/// Assembly Definition
		/// Unityが標準で提供していないけど、中身としてはJsonなので再現する
		/// </summary>
		[System.Serializable]
		class CustomScriptAssemblyData
		{
			public string name;
			public string[] references;
			public string[] includePlatforms;
			public string[] excludePlatforms;

			public string[] defineConstraints;
			public string[] optionalUnityReferences;

			public bool IsTestAssemblies
				=> optionalUnityReferences != null
				&& optionalUnityReferences.Contains("TestAssemblies");

			public bool IsEditorOnly
				=> includePlatforms != null
				&& includePlatforms.Length == 1
				&& includePlatforms[0].Equals("Editor");
		}

		public static bool IsInRuntime(string packageDirectory)
		{
			using var pool = DefineConstraintsHelper.GetActiveScriptCompilationDefines(out var activeScriptCompilationDefinesSet);

			var files = AssetDatabase.FindAssets("t:assemblydefinitionasset", new string[] { packageDirectory })
				.Select(AssetDatabase.GUIDToAssetPath)
				.Where(x => !AssetDatabase.IsValidFolder(x))
				.Where(x => !UnityEditorInternal.InternalEditorUtility.IsInEditorFolder(x))
				.ToArray();
			foreach (var filePath in files)
			{
				// Assembly Definition Asset はファイルの中身がJsonになっている
				var assemblyDefinition = AssetDatabase.LoadAssetAtPath<UnityEditorInternal.AssemblyDefinitionAsset>(filePath);
				if (assemblyDefinition == null)
					continue;
				var assemblyData = JsonUtility.FromJson<CustomScriptAssemblyData>(assemblyDefinition.text);
				if (!DefineConstraintsHelper.IsSatisfiedDefineConstraints(activeScriptCompilationDefinesSet, assemblyData.defineConstraints))
					continue;
				if (assemblyData.IsTestAssemblies)
					continue;
				if (assemblyData.IsEditorOnly)
					continue;
				return true;
			}
			return false;
		}
	}
}
