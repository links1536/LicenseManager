using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.Pool;

namespace Links.Licenses.Plugins
{
	static class DefineConstraintsHelper
	{
		// テスト・デバッグ用のDefineを除外したい
		static string[] TestDefines = new string[]
		{
			"UNITY_INCLUDE_TESTS",
			"DEBUG",
			"TRACE",
			"UNITY_ASSERTIONS",
			"ENABLE_UNITY_COLLECTIONS_CHECKS",
		};

		// エディタ用のDefineを除外したい
		static string[] EditorDefines = new string[]
		{
			"UNITY_EDITOR",
			"UNITY_EDITOR_64",
			"UNITY_EDITOR_WIN",
			"UNITY_EDITOR_OSX",
			"UNITY_EDITOR_LINUX",
			"ENABLE_EDITOR_GAME_SERVICES",
			"ENABLE_EDITOR_HUB_LICENSE",
			"EDITOR_ONLY_NAVMESH_BUILDER_DEPRECATED",
		};

		public static PooledObject<HashSet<string>> GetActiveScriptCompilationDefines(out HashSet<string> activeScriptCompilationDefinesSet)
		{
			var defines = EditorUserBuildSettings.activeScriptCompilationDefines;
			var pool = HashSetPool<string>.Get(out activeScriptCompilationDefinesSet);
			activeScriptCompilationDefinesSet.UnionWith(defines);
			activeScriptCompilationDefinesSet.ExceptWith(TestDefines);
			activeScriptCompilationDefinesSet.ExceptWith(EditorDefines);
			return pool;
		}

		/// <summary>
		/// アセンブリの Define Constraints (制約定義) を満たしているか
		/// </summary>
		public static bool IsSatisfiedDefineConstraints(HashSet<string> activeDefines, string[] defineConstraints)
			=> UnityEditor.Compilation.CompilationPipeline.IsDefineConstraintsCompatible(activeDefines.ToArray(), defineConstraints);
	}
}
