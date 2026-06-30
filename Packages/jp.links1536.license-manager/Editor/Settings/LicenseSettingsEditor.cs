using System.Collections.Generic;
using System.Linq;
using System.Text;
using Links.Licenses.Plugins;
using UnityEditor;
using UnityEngine;

namespace Links.Licenses.Settings
{
	[CustomEditor(typeof(LicenseSettings))]
	public class LicenseSettingsEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			var settings = (LicenseSettings)target;
			DrawStatusSummary(settings);

			if (GUILayout.Button("更新"))
			{
				Undo.RecordObject(settings, $"パッケージ情報更新 (All)");
				RefreshAllEntries(settings);
				settings.Save();
			}
			if (GUILayout.Button("リポジトリ用ライセンス情報を生成"))
			{
				var result = LicenseNoticesGenerator.OutputThirdPartyNotices(settings);
				EditorUtility.DisplayDialog(
					"Third Party Notices",
					$"THIRD PARTY NOTICES を生成しました\n出力先: {result.OutputDirectory}\n\n対象件数: {result.TotalEntryCount}\n不足件数: {result.MissingEntryCount}",
					"閉じる"
				);
			}
			if (GUILayout.Button("プレイヤー用ライセンス情報を出力"))
			{
				(var spdx, var licenseList) = LicenseNoticesGenerator.CreateLicenseManifest();

				var licenseManifest = LicenseManifest.GetOrCreateInstance();
				Undo.RecordObject(licenseManifest, "ライセンス情報更新");

				licenseManifest.SpdxLicenseList = spdx;
				licenseManifest.RawEntryList = licenseList;
				EditorUtility.SetDirty(licenseManifest);
				AssetDatabase.SaveAssetIfDirty(licenseManifest);

				var outputPath = AssetDatabase.GetAssetPath(licenseManifest);

				//EditorUtility.DisplayDialog(
				//	"プレイヤー用ライセンス情報出力",
				//	$"プレイヤー用ライセンス情報を生成しました\n出力先: {outputPath}\n\n対象件数: {licenseList.Count}",
				//	"閉じる"
				//);
			}

			EditorGUILayout.Space();

			EditorGUILayout.Space();
			DrawSourceSummary("管理外パッケージ", settings.UnmanagedAssets);
			DrawSourceSummary("Unity Packages", settings.UnityPackages);
			DrawSourceSummary("NuGet for Unity", settings.NuGetPackages);

			serializedObject.ApplyModifiedProperties();

		}

		static void DrawSourceSummary(string title, LicenseSettings.Entry[] entries)
		{
			EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
			EditorGUILayout.LabelField("収集済み", entries?.Length.ToString() ?? "0");
			EditorGUILayout.Space();
		}

		static void DrawStatusSummary(LicenseSettings settings)
		{
			var issues = Validate(settings).ToArray();
			if (issues.Length == 0)
			{
				EditorGUILayout.HelpBox("各パッケージのライセンス設定に不整合はありません", MessageType.Info);
				return;
			}

			var builder = new StringBuilder();
			builder.AppendLine("公開前に確認が必要です:");
			foreach (var issue in issues)
				builder.AppendLine($"- {issue}");

			EditorGUILayout.HelpBox(builder.ToString(), MessageType.Warning);
		}

		public static void RefreshAllEntries(LicenseSettings settings)
		{
			RefreshSourceEntries(settings, SourceType.UnityPackages);
			RefreshSourceEntries(settings, SourceType.NuGet);
		}

		public static void RefreshSourceEntries(LicenseSettings settings, SourceType sourceType)
		{
			switch (sourceType)
			{
				case SourceType.UnityPackages:
					var upmPackages = LicenseFinder.GetUPMPackages();
					settings.SetUnityPackageEntries(ConvertToLicenseEntry(upmPackages).ToArray());
					break;
				case SourceType.NuGet:
					var nugetPackage = LicenseFinder.GetNuGetPackages();
					settings.SetNuGetEntries(ConvertToLicenseEntry(nugetPackage).ToArray());
					break;
				default:
					break;
			}
		}

		static List<LicenseSettings.Entry> ConvertToLicenseEntry(List<PackageLicenseData> packageList)
		{
			var list = new List<LicenseSettings.Entry>();
			foreach (var item in packageList)
			{
				var copyrightList = new List<string>();
				if (!string.IsNullOrEmpty(item.Copyright))
					copyrightList.Add(item.Copyright);

				var entry = new LicenseSettings.Entry()
				{
					Id = item.Id,
					Name = item.Name,
					CopyrightList = copyrightList,
					PackagePath = item.AssetPath,
					LicenseType = item.LicenseType,
					LicenseFile = item.LicenseFile,
					ThirdPartyNoticesFile = item.ThirdPartyNoticesFile,
					IsLocalPackages = UnityPathUtils.IsLocalPackages(item.AssetPath),
					IsInRuntime = AssemblyDefinitionHelper.IsInRuntime(item.AssetPath) || PluginHelper.IsInRuntime(item.AssetPath),
				};
				list.Add(entry);
			}
			return list;
		}

		static IEnumerable<string> Validate(LicenseSettings settings)
		{
			if (settings == null)
			{
				yield return "設定アセットを読み込めません";
				yield break;
			}

			foreach (var entry in settings.UnmanagedAssets.Where(x => x != null))
			{
				if (string.IsNullOrWhiteSpace(entry.Name))
					yield return "埋め込みパッケージの名前が空です";

				if (!entry.HasSpdxLicense() && entry.LicenseFile == null)
					yield return $"埋め込みパッケージ '{entry.Name}' に LICENSE が設定されていません";
			}

			foreach (var entry in settings.UnityPackages.Where(x => x != null))
			{
				if (!entry.HasSpdxLicense() && entry.LicenseFile == null)
					yield return $"Unity Package '{entry.Name}' の LICENSE がありません";
			}

			foreach (var entry in settings.NuGetPackages.Where(x => x != null))
			{
				if (!entry.HasSpdxLicense() && entry.LicenseFile == null)
					yield return $"NuGet パッケージ '{entry.Name}' の LICENSE がありません";
			}
		}

	}
}
