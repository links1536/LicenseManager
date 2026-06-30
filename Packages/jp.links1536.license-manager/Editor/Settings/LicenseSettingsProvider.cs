using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace Links.Licenses.Settings
{
	public class LicenseSettingsProvider : SettingsProvider
	{
		const string MenuRoot = "Project/Links/Licenses";
		const string RuntimeLicensePath = MenuRoot + "/Runtime Licenses";

		const string SettingPath = MenuRoot + "/Settings";
		const string UnmanagedAssetsSettingPath = SettingPath + "/Unmanaged Assets";
		const string UnityPackagesSettingPath = SettingPath + "/Unity Packages";
		const string NuGetSettingPath = SettingPath + "/NuGet for Unity";

		Editor m_Editor;

		[SettingsProvider]
		public static SettingsProvider CreateProvider()
			=> new LicenseSettingsProvider(SettingPath, SettingsScope.Project, null);

		[SettingsProviderGroup]
		public static SettingsProvider[] CreateSubProviders()
			=> new SettingsProvider[]
			{
				new LicenseManifestProvider(
					RuntimeLicensePath
				),
				new PackageSettingsProvider(
					UnmanagedAssetsSettingPath,
					"Unmanaged Assets",
					"UPM および NuGet以外で追加されたアセット",
					SourceType.UnmanagedAssets
				),
				new PackageSettingsProvider(
					UnityPackagesSettingPath,
					"Unity Packages",
					"ライセンスファイルがない場合は、ライセンスファイルを割り当ててください",
					SourceType.UnityPackages
				),
				new PackageSettingsProvider(
					NuGetSettingPath,
					"NuGet for Unity",
					"ライセンスファイルがない場合は、ライセンスファイルを割り当ててください",
					SourceType.NuGet
				),
			};

		public LicenseSettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords)
			: base(path, scopes, keywords)
		{
		}

		public override void OnActivate(string searchContext, VisualElement rootElement)
		{
			var instance = LicenseSettings.instance;
			Editor.CreateCachedEditor(instance, typeof(LicenseSettingsEditor), ref m_Editor);
		}

		public override void OnGUI(string searchContext)
		{
			var instance = LicenseSettings.instance;
			if (m_Editor == null || m_Editor.target != instance)
				Editor.CreateCachedEditor(instance, typeof(LicenseSettingsEditor), ref m_Editor);
			using (var check = new EditorGUI.ChangeCheckScope())
			{
				m_Editor.OnInspectorGUI();
				if (check.changed)
				{
					instance.Save();
				}
			}
		}

	}
}
