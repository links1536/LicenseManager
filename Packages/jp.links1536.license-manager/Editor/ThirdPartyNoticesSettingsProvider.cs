using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Links.Licenses
{
	public class ThirdPartyNoticesSettingsProvider : SettingsProvider
	{
		const string SettingPath = "Project/Links/Licenses/Third Party Notices";
		const string UnityPackagesSettingPath = SettingPath + "/Unity Packages";
		const string NuGetSettingPath = SettingPath + "/NuGet for Unity";

		Editor m_Editor;

		[SettingsProvider]
		public static SettingsProvider CreateProvider()
			=> new ThirdPartyNoticesSettingsProvider(SettingPath, SettingsScope.Project, null);

		[SettingsProviderGroup]
		public static SettingsProvider[] CreateSubProviders()
			=> new SettingsProvider[]
			{
				new PackageSettingsProvider(
					UnityPackagesSettingPath,
					"Unity Packages",
					"Unity Package Manager で解決されたパッケージをプロジェクトの依存関係から収集します\nパッケージ直下に対応するライセンスファイルがない場合は、ライセンスファイルを割り当ててください",
					SourceType.UnityPackages
				),
				new PackageSettingsProvider(
					NuGetSettingPath,
					"NuGet for Unity",
					"NuGet パッケージを Assets/packages.config と NuGet リポジトリパスから収集します\n展開先のパッケージ直下に対応するライセンスファイルがない場合は、ライセンスファイルを割り当ててください",
					SourceType.NuGet
				),
			};

		public ThirdPartyNoticesSettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords)
			: base(path, scopes, keywords)
		{
		}

		public override void OnActivate(string searchContext, VisualElement rootElement)
		{
			var instance = GetOrCreateSettings();
			ThirdPartyNoticesSettingsEditor.RefreshAllEntries(instance);
			Editor.CreateCachedEditor(instance, typeof(ThirdPartyNoticesSettingsEditor), ref m_Editor);
		}

		public override void OnGUI(string searchContext)
		{
			var instance = GetOrCreateSettings();
			if (m_Editor == null || m_Editor.target != instance)
				Editor.CreateCachedEditor(instance, typeof(ThirdPartyNoticesSettingsEditor), ref m_Editor);

			m_Editor.OnInspectorGUI();
		}

		internal static ThirdPartyNoticesSettings GetOrCreateSettings()
		{
			if (ThirdPartyNoticesSettings.TryGetInstance(out var instance))
				return instance;

			CreateSettings();
			AssetDatabase.SaveAssets();
			Resources.UnloadUnusedAssets();
			ThirdPartyNoticesSettings.TryGetInstance(out instance);
			return instance;
		}

		static void CreateSettings()
		{
			var config = ScriptableObject.CreateInstance<ThirdPartyNoticesSettings>();
			var parent = "Assets/Resources";
			if (!AssetDatabase.IsValidFolder(parent))
				AssetDatabase.CreateFolder("Assets", "Resources");

			var assetPath = Path.Combine(parent, Path.ChangeExtension(ThirdPartyNoticesSettings.Path, ".asset"));
			AssetDatabase.CreateAsset(config, assetPath);
		}
	}
}
