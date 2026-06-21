using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Networking;

namespace Links.Licenses
{
	[CustomEditor(typeof(ThirdPartyNoticesSettings))]
	public class ThirdPartyNoticesSettingsEditor : Editor
	{
		const string PackagesConfigPath = "Assets/packages.config";
		const string NuGetConfigPath = "Assets/NuGet.config";
		const string AuditReportPath = "Library/ThirdPartyNotices/AUDIT_REPORT.txt";
		const string GeneratedUnityPackageLicenseRoot = "Assets/Generated/ThirdPartyLicenses/UnityPackages";
		internal const string PublicationCombinedFileNameForGenerator = "THIRD PARTY NOTICES";
		internal const string PublicationAuditFileNameForGenerator = "AUDIT_REPORT.txt";
		internal const string PublicationResearchFolderPathForGenerator = "Library/ThirdPartyNotices";
		static readonly string[] LicenseFilePrefixes =
		{
			"LICENSE",
			"COPYING",
		};
		static readonly string[] ThirdPartyNoticesFilePrefixes =
		{
			"NOTICE",
			"THIRD-PARTY-NOTICES",
			"THIRDPARTYNOTICES",
		};
		static readonly string[] LicenseFileExtensions =
		{
			string.Empty,
			".txt",
			".md",
			".markdown",
		};
		static readonly Dictionary<string, bool> RepositoryMaterializationCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

		SerializedProperty m_ManualEntriesProperty;

		void OnEnable()
		{
			m_ManualEntriesProperty = serializedObject.FindProperty("m_ManualEntries");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			var settings = (ThirdPartyNoticesSettings)target;
			DrawStatusSummary(settings);
			var exportRequested = GUILayout.Button("監査レポートを出力");
			var generatePublicationRequested = GUILayout.Button("THIRD PARTY NOTICES を生成");

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("管理外パッケージ", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(m_ManualEntriesProperty, true);

			EditorGUILayout.Space();
			DrawSourceSummary("Unity Packages", settings.UnityPackageOSSList, settings.MissingUnityPackageOSSList);
			DrawSourceSummary("NuGet for Unity", settings.NuGetOSSList, settings.MissingNuGetOSSList);

			serializedObject.ApplyModifiedProperties();

			if (exportRequested)
				ExportAuditReport(settings);

			if (generatePublicationRequested)
			{
				var result = ThirdPartyNoticesGenerator.GenerateForProjectPublication(settings, true);
				EditorUtility.DisplayDialog(
					"Third Party Notices",
					$"THIRD PARTY NOTICES を生成しました\n出力先: {result.OutputDirectory}\n\n対象件数: {result.TotalEntryCount}\n不足件数: {result.MissingEntryCount}",
					"閉じる"
				);
			}
		}

		static void DrawSourceSummary(string title, ThirdPartyNoticesSettings.OSSEntry[] entries, ThirdPartyNoticesSettings.MissingOSSEntry[] missingEntries)
		{
			EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
			EditorGUILayout.LabelField("収集済み", entries?.Length.ToString() ?? "0");
			EditorGUILayout.LabelField("不足", missingEntries?.Length.ToString() ?? "0");
			EditorGUILayout.Space();
		}

		static void DrawStatusSummary(ThirdPartyNoticesSettings settings)
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

		public static void RefreshAllEntries(ThirdPartyNoticesSettings settings)
		{
			RepositoryMaterializationCache.Clear();
			RefreshUnityPackageEntries(settings);
			RefreshNuGetEntries(settings);
		}

		public static void RefreshSourceEntries(ThirdPartyNoticesSettings settings, SourceType sourceType)
		{
			RepositoryMaterializationCache.Clear();
			switch (sourceType)
			{
				case SourceType.UnityPackages:
					RefreshUnityPackageEntries(settings);
					break;
				case SourceType.NuGet:
					RefreshNuGetEntries(settings);
					break;
			}
		}

		public static void RefreshUnityPackageEntries(ThirdPartyNoticesSettings settings)
		{
			var gitPackages = GetGitPackageEntries();
			RefreshEntries(
				settings,
				EnumerateUnityPackages(gitPackages),
				settings.UnityPackageOSSList,
				settings.MissingUnityPackageOSSList,
				settings.SetUnityPackageEntries
			);
		}

		public static void RefreshNuGetEntries(ThirdPartyNoticesSettings settings)
		{
			RefreshEntries(
				settings,
				EnumerateNuGetPackages(),
				settings.NuGetOSSList,
				settings.MissingNuGetOSSList,
				settings.SetNuGetEntries
			);
		}

		static void RefreshEntries(
			ThirdPartyNoticesSettings settings,
			IEnumerable<PackageEntry> packages,
			IEnumerable<ThirdPartyNoticesSettings.OSSEntry> existingAssignedEntries,
			IEnumerable<ThirdPartyNoticesSettings.MissingOSSEntry> existingMissingEntries,
			Action<ThirdPartyNoticesSettings.OSSEntry[], ThirdPartyNoticesSettings.MissingOSSEntry[]> applyResult)
		{
			if (settings == null)
				return;

			var collectedEntries = new List<ThirdPartyNoticesSettings.OSSEntry>();
			var missingEntries = new List<ThirdPartyNoticesSettings.MissingOSSEntry>();
			var existingMissingEntriesMap = existingMissingEntries
				.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name))
				.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
			var existingCollectedEntries = existingAssignedEntries
				.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name) && x.HasLicenseContent())
				.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

			foreach (var package in packages)
			{
				existingMissingEntriesMap.TryGetValue(package.DisplayName, out var existingMissingEntry);
				existingCollectedEntries.TryGetValue(package.DisplayName, out var existingCollectedEntry);
				var overrideFiles = new LicenseAssetPair(
					existingMissingEntry?.LicenseFile,
					existingMissingEntry?.ThirdPartyNoticesFile
				);
				if (!TryCollectPackageEntries(package, overrideFiles, existingCollectedEntry, collectedEntries, out var missingReason))
				{
					if (package.IsUnityOwned && string.IsNullOrWhiteSpace(missingReason))
						continue;

					missingEntries.Add(new ThirdPartyNoticesSettings.MissingOSSEntry
					{
						Name = package.DisplayName,
						PackagePath = package.PackageAssetPath,
						Reason = missingReason,
						LicenseFile = overrideFiles.LicenseFile,
						ThirdPartyNoticesFile = overrideFiles.ThirdPartyNoticesFile,
						IsRepositoryMaterialized = package.IsRepositoryMaterialized,
						HasMetadataLicense = !string.IsNullOrWhiteSpace(package.MetadataLicenseText),
					});
				}
			}

			Undo.RecordObject(settings, "Third Party Notices を更新");
			applyResult(
				collectedEntries.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
				missingEntries.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray()
			);
			EditorUtility.SetDirty(settings);
			AssetDatabase.SaveAssets();
		}

		static bool TryCollectPackageEntries(
			PackageEntry package,
			LicenseAssetPair overrideAssets,
			ThirdPartyNoticesSettings.OSSEntry existingCollectedEntry,
			List<ThirdPartyNoticesSettings.OSSEntry> collectedEntries,
			out string missingReason)
		{
			missingReason = null;

			if (!Directory.Exists(package.PackageFullPath))
			{
				missingReason = "パッケージディレクトリが見つかりません";
				return false;
			}

			if (overrideAssets.LicenseFile != null)
			{
				collectedEntries.Add(new ThirdPartyNoticesSettings.OSSEntry
				{
					Name = package.DisplayName,
					LicenseFile = overrideAssets.LicenseFile,
					ThirdPartyNoticesFile = overrideAssets.ThirdPartyNoticesFile,
					LicenseText = null,
					SourcePath = package.PackageAssetPath,
					IsRepositoryMaterialized = package.IsRepositoryMaterialized,
					IsMetadataOnly = false,
				});
				return true;
			}

			if (package.IsUnityOwned)
				return TryCollectUnityOwnedPackageEntries(package, collectedEntries, out missingReason);

			var assets = FindLicenseAssets(package.PackageFullPath, package.PackageAssetPath);
			if (assets.LicenseFile == null)
				assets = assets.WithLicenseFile(FindOrCreateGitHubRootAsset(package, LicenseDocumentType.License));
			if (assets.ThirdPartyNoticesFile == null)
				assets = assets.WithThirdPartyNoticesFile(FindOrCreateGitHubRootAsset(package, LicenseDocumentType.ThirdPartyNotices));

			if (assets.LicenseFile == null && existingCollectedEntry?.LicenseFile != null)
				assets = assets.WithLicenseFile(existingCollectedEntry.LicenseFile);
			if (assets.ThirdPartyNoticesFile == null && existingCollectedEntry?.ThirdPartyNoticesFile != null)
				assets = assets.WithThirdPartyNoticesFile(existingCollectedEntry.ThirdPartyNoticesFile);

			if (assets.LicenseFile != null || assets.ThirdPartyNoticesFile != null)
			{
				collectedEntries.Add(new ThirdPartyNoticesSettings.OSSEntry
				{
					Name = package.DisplayName,
					LicenseFile = assets.LicenseFile,
					ThirdPartyNoticesFile = assets.ThirdPartyNoticesFile,
					LicenseText = null,
					SourcePath = package.PackageAssetPath,
					IsRepositoryMaterialized = package.IsRepositoryMaterialized,
					IsMetadataOnly = false,
				});
				return true;
			}

			if (!string.IsNullOrWhiteSpace(package.MetadataLicenseText))
			{
				if (package.IsRepositoryMaterialized)
				{
					missingReason = "対応するライセンス情報が見つかりません";
					return false;
				}

				collectedEntries.Add(new ThirdPartyNoticesSettings.OSSEntry
				{
					Name = package.DisplayName,
					LicenseFile = null,
					LicenseText = package.MetadataLicenseText,
					SourcePath = package.PackageAssetPath,
					IsRepositoryMaterialized = false,
					IsMetadataOnly = true,
				});
				return true;
			}

			missingReason = "パッケージ直下または GitHub リポジトリ直下に対応する LICENSE が見つかりません";
			return false;
		}

		/// <summary>
		/// Unity公式パッケージ情報
		/// </summary>
		static bool TryCollectUnityOwnedPackageEntries(
			PackageEntry package,
			List<ThirdPartyNoticesSettings.OSSEntry> collectedEntries,
			out string missingReason)
		{
			missingReason = null;
			var explicitAssets = FindLicenseAssets(package.PackageFullPath, package.PackageAssetPath);
			if (explicitAssets.LicenseFile != null)
			{
				collectedEntries.Add(new ThirdPartyNoticesSettings.OSSEntry
				{
					Name = package.DisplayName,
					LicenseFile = explicitAssets.LicenseFile,
					ThirdPartyNoticesFile = explicitAssets.ThirdPartyNoticesFile,
					LicenseText = null,
					SourcePath = package.PackageAssetPath,
					IsRepositoryMaterialized = package.IsRepositoryMaterialized,
					IsMetadataOnly = false,
				});

				return true;
			}

			if (!string.IsNullOrWhiteSpace(package.MetadataLicenseText))
			{
				if (package.IsRepositoryMaterialized)
				{
					missingReason = "対応するライセンス情報が見つかりません";
					return false;
				}

				collectedEntries.Add(new ThirdPartyNoticesSettings.OSSEntry
				{
					Name = package.DisplayName,
					LicenseFile = null,
					ThirdPartyNoticesFile = null,
					LicenseText = package.MetadataLicenseText,
					SourcePath = package.PackageAssetPath,
					IsRepositoryMaterialized = false,
					IsMetadataOnly = true,
				});
				return true;
			}

			return false;
		}

		static IEnumerable<string> Validate(ThirdPartyNoticesSettings settings)
		{
			if (settings == null)
			{
				yield return "設定アセットを読み込めません";
				yield break;
			}

			foreach (var entry in settings.ManualOSSList.Where(x => x != null))
			{
				if (string.IsNullOrWhiteSpace(entry.Name))
					yield return "埋め込みパッケージの名前が空です";

				if (entry.LicenseFile == null && string.IsNullOrWhiteSpace(entry.LicenseText))
					yield return $"埋め込みパッケージ '{entry.Name}' に LICENSE またはライセンステキストが設定されていません";
			}

			foreach (var entry in settings.UnityPackageOSSList.Where(x => x != null))
			{
				if (entry.LicenseFile == null && string.IsNullOrWhiteSpace(entry.LicenseText))
					yield return $"Unity Package '{entry.Name}' の LICENSE がありません";
			}

			foreach (var entry in settings.MissingUnityPackageOSSList.Where(x => x != null))
			{
				if (entry.IsRepositoryMaterialized && entry.LicenseFile == null)
					yield return $"Unity Package '{entry.Name}': {entry.Reason}";
			}

			foreach (var entry in settings.NuGetOSSList.Where(x => x != null))
			{
				if (entry.LicenseFile == null && string.IsNullOrWhiteSpace(entry.LicenseText))
					yield return $"NuGet パッケージ '{entry.Name}' の LICENSE がありません";
			}

			foreach (var entry in settings.MissingNuGetOSSList.Where(x => x != null))
			{
				if (entry.IsRepositoryMaterialized && entry.LicenseFile == null)
					yield return $"NuGet パッケージ '{entry.Name}': {entry.Reason}";
			}
		}

		static void ExportAuditReport(ThirdPartyNoticesSettings settings)
		{
			var report = settings.CreateAuditReport();
			var fullPath = ToFullPath(AuditReportPath);
			Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? "Library");
			File.WriteAllText(fullPath, report);
			EditorUtility.RevealInFinder(fullPath);
		}

		static IEnumerable<PackageEntry> EnumerateNuGetPackages()
		{
			var packagesConfigFullPath = ToFullPath(PackagesConfigPath);
			if (!File.Exists(packagesConfigFullPath))
				yield break;

			var packageRootAssetPath = GetPackageRootAssetPath();
			var packageRootFullPath = ToFullPath(packageRootAssetPath);

			XDocument document;
			try
			{
				document = XDocument.Load(packagesConfigFullPath);
			}
			catch
			{
				yield break;
			}

			foreach (var packageElement in document.Root?.Elements("package") ?? Enumerable.Empty<XElement>())
			{
				var id = packageElement.Attribute("id")?.Value;
				var version = packageElement.Attribute("version")?.Value;
				if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version))
					continue;

				var folderName = $"{id}.{version}";
				var fullPath = Path.GetFullPath(Path.Combine(packageRootFullPath, folderName));
				var assetPath = ToAssetPath(Path.Combine(packageRootAssetPath, folderName));
				yield return new PackageEntry(
					$"{id} {version}",
					id,
					version,
					fullPath,
					assetPath,
					isRepositoryMaterialized: IsRepositoryMaterializedPath(fullPath)
				);
			}
		}

		/// <summary>
		/// UnityPackageManager
		/// </summary>
		static IEnumerable<PackageEntry> EnumerateUnityPackages(IReadOnlyDictionary<string, GitPackageLockEntry> gitPackages)
		{
			var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
			if (packages == null)
				yield break;

			foreach (var package in packages)
			{
				if (package == null)
					continue;

				if (string.IsNullOrWhiteSpace(package.name) || string.IsNullOrWhiteSpace(package.assetPath))
					continue;

				gitPackages.TryGetValue(package.name, out var gitPackage);
				var displayName = string.IsNullOrWhiteSpace(package.version)
					? package.name
					: $"{package.name} {package.version}";
				yield return new PackageEntry(
					displayName,
					package.name,
					package.version,
					package.resolvedPath,
					NormalizeAssetPath(package.assetPath),
					CreateUnityPackageLicenseText(package.resolvedPath),
					gitPackage,
					package.name.StartsWith("com.unity.", StringComparison.OrdinalIgnoreCase),
					IsRepositoryMaterializedPath(package.resolvedPath)
				);
			}
		}

		static IReadOnlyDictionary<string, GitPackageLockEntry> GetGitPackageEntries()
		{
			// Gitパッケージの情報をまとめる
			var result = new Dictionary<string, GitPackageLockEntry>(StringComparer.OrdinalIgnoreCase);
			var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
			foreach(var package in packages)
			{
				if (package.source != PackageSource.Git)
					continue;
				result[package.name] = new GitPackageLockEntry()
				{
					PackageId = package.name,
					Source = package.source.ToString().ToLower(),
					Hash = package.git.hash,

					// パッケージ名@URL形式
					VersionUrl = package.packageId.Substring(package.name.Length + 1)
				};
			}

			return result;
		}

		static TextAsset FindOrCreateGitHubRootAsset(PackageEntry package, LicenseDocumentType documentType)
		{
			if (!TryCreateGitHubRepositoryInfo(package.GitPackage, out var repositoryInfo))
				return null;

			foreach (var candidateFileName in EnumerateLicenseCandidateFileNames(documentType))
			{
				var rawUrl = repositoryInfo.GetRawUrl(candidateFileName);
				if (!TryDownloadText(rawUrl, out var licenseText))
					continue;

				if (string.IsNullOrWhiteSpace(licenseText))
					continue;

				var assetPath = GetGeneratedUnityPackageLicenseAssetPath(package, candidateFileName);
				return UpsertGeneratedLicenseAsset(assetPath, licenseText);
			}

			return null;
		}

		static bool TryCreateGitHubRepositoryInfo(GitPackageLockEntry gitPackage, out GitHubRepositoryInfo repositoryInfo)
		{
			repositoryInfo = default;
			if (gitPackage == null || string.IsNullOrWhiteSpace(gitPackage.VersionUrl) || string.IsNullOrWhiteSpace(gitPackage.Hash))
				return false;

			if (!Uri.TryCreate(gitPackage.VersionUrl, UriKind.Absolute, out var uri))
				return false;

			if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
				return false;

			var segments = uri.AbsolutePath.Trim('/').Split('/');
			if (segments.Length < 2)
				return false;

			var owner = segments[0];
			var repository = segments[1];
			if (repository.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
				repository = repository.Substring(0, repository.Length - 4);

			if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repository))
				return false;

			repositoryInfo = new GitHubRepositoryInfo(owner, repository, gitPackage.Hash);
			return true;
		}

		static IEnumerable<string> EnumerateLicenseCandidateFileNames(LicenseDocumentType documentType)
		{
			var prefixes = documentType == LicenseDocumentType.ThirdPartyNotices
				? ThirdPartyNoticesFilePrefixes
				: LicenseFilePrefixes;
			foreach (var prefix in prefixes)
			{
				foreach (var extension in LicenseFileExtensions)
					yield return prefix + extension;
			}
		}

		static bool TryDownloadText(string url, out string text)
		{
			text = null;
			using (var request = UnityWebRequest.Get(url))
			{
				request.timeout = 15;
				var operation = request.SendWebRequest();
				while (!operation.isDone)
				{
				}

				if (!string.IsNullOrEmpty(request.error))
					return false;

				if (request.responseCode < 200 || request.responseCode >= 300)
					return false;

				text = request.downloadHandler.text;
				return !string.IsNullOrWhiteSpace(text);
			}
		}

		static string GetGeneratedUnityPackageLicenseAssetPath(PackageEntry package, string candidateFileName)
		{
			EnsureAssetFolderExists(GeneratedUnityPackageLicenseRoot);
			var fileName = $"{SanitizeFileName(package.PackageId)}-{SanitizeFileName(package.Version)}-{SanitizeFileName(candidateFileName)}.txt";
			return $"{GeneratedUnityPackageLicenseRoot}/{fileName}";
		}

		static TextAsset UpsertGeneratedLicenseAsset(string assetPath, string content)
		{
			var fullPath = ToFullPath(assetPath);
			var normalizedContent = NormalizeLineEndings(content);
			if (!File.Exists(fullPath) || NormalizeLineEndings(File.ReadAllText(fullPath)) != normalizedContent)
			{
				File.WriteAllText(fullPath, normalizedContent, new UTF8Encoding(false));
				AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
			}

			return AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
		}

		static string NormalizeLineEndings(string text)
			=> (text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");

		static string SanitizeFileName(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return "package";

			var invalidChars = Path.GetInvalidFileNameChars();
			var builder = new StringBuilder(value.Length);
			foreach (var c in value)
			{
				if (invalidChars.Contains(c) || c == ' ' || c == '/')
					builder.Append('_');
				else
					builder.Append(c);
			}

			return builder.ToString();
		}

		static void EnsureAssetFolderExists(string assetFolderPath)
		{
			var parts = assetFolderPath.Split('/');
			var currentPath = parts[0];
			for (var i = 1; i < parts.Length; i++)
			{
				var nextPath = $"{currentPath}/{parts[i]}";
				if (!AssetDatabase.IsValidFolder(nextPath))
					AssetDatabase.CreateFolder(currentPath, parts[i]);

				currentPath = nextPath;
			}
		}

		static string CreateUnityPackageLicenseText(string packageFullPath)
		{
			if (string.IsNullOrWhiteSpace(packageFullPath) || !Directory.Exists(packageFullPath))
				return null;

			var packageJsonPath = Path.Combine(packageFullPath, "package.json");
			if (!File.Exists(packageJsonPath))
				return null;

			try
			{
				var packageJson = JsonUtility.FromJson<UnityPackageJson>(File.ReadAllText(packageJsonPath));
				if (packageJson == null)
					return null;

				var hasLicenseMetadata = !string.IsNullOrWhiteSpace(packageJson.license) || !string.IsNullOrWhiteSpace(packageJson.licensesUrl);
				if (!hasLicenseMetadata)
					return null;

				var builder = new StringBuilder();
				builder.AppendLine("パッケージ ライセンス メタデータ");
				builder.AppendLine();

				if (!string.IsNullOrWhiteSpace(packageJson.displayName))
					builder.AppendLine($"表示名: {packageJson.displayName}");
				if (!string.IsNullOrWhiteSpace(packageJson.name))
					builder.AppendLine($"パッケージ名: {packageJson.name}");
				if (!string.IsNullOrWhiteSpace(packageJson.version))
					builder.AppendLine($"バージョン: {packageJson.version}");
				if (packageJson.author != null && !string.IsNullOrWhiteSpace(packageJson.author.name))
					builder.AppendLine($"作者: {packageJson.author.name}");
				if (packageJson.author != null && !string.IsNullOrWhiteSpace(packageJson.author.url))
					builder.AppendLine($"作者 URL: {packageJson.author.url}");
				if (!string.IsNullOrWhiteSpace(packageJson.license))
					builder.AppendLine($"ライセンス: {packageJson.license}");
				if (!string.IsNullOrWhiteSpace(packageJson.licensesUrl))
					builder.AppendLine($"ライセンス URL: {packageJson.licensesUrl}");

				return builder.ToString().TrimEnd();
			}
			catch
			{
				return null;
			}
		}

		static string GetPackageRootAssetPath()
		{
			var nuGetConfigFullPath = ToFullPath(NuGetConfigPath);
			if (!File.Exists(nuGetConfigFullPath))
				return "Assets/Packages";

			try
			{
				var document = XDocument.Load(nuGetConfigFullPath);
				var repositoryPath = document
					.Descendants("add")
					.FirstOrDefault(x => string.Equals(x.Attribute("key")?.Value, "repositoryPath", StringComparison.OrdinalIgnoreCase))
					?.Attribute("value")?.Value;

				if (string.IsNullOrWhiteSpace(repositoryPath))
					return "Assets/Packages";

				var baseDirectory = Path.GetDirectoryName(NuGetConfigPath) ?? "Assets";
				return ToAssetPath(Path.Combine(baseDirectory, repositoryPath));
			}
			catch
			{
				return "Assets/Packages";
			}
		}

		static LicenseAssetPair FindLicenseAssets(string packageFullPath, string packageAssetPath)
		{
			TextAsset detectedLicenseFile = null;
			TextAsset thirdPartyNoticesFile = null;

			if (string.IsNullOrWhiteSpace(packageFullPath) || !Directory.Exists(packageFullPath))
				return new LicenseAssetPair(detectedLicenseFile, thirdPartyNoticesFile);

			var licenseFiles = Directory
				.GetFiles(packageFullPath, "*", SearchOption.TopDirectoryOnly)
				.Select(path => new FileInfo(path))
				.Where(file => file.Exists && GetLicenseKind(file.Name) != LicenseDocumentType.Unknown)
				.ToArray()
				.OrderBy(file => GetLicensePriority(file.Name))
				.ThenBy(file => file.Name, StringComparer.OrdinalIgnoreCase);

			foreach (var file in licenseFiles)
			{
				var assetPath = $"{packageAssetPath}/{file.Name}";
				var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(NormalizeAssetPath(assetPath));
				if (asset == null)
					continue;

				switch (GetLicenseKind(file.Name))
				{
					case LicenseDocumentType.License:
						detectedLicenseFile ??= asset;
						break;
					case LicenseDocumentType.ThirdPartyNotices:
						thirdPartyNoticesFile ??= asset;
						break;
				}
			}

			return new LicenseAssetPair(detectedLicenseFile, thirdPartyNoticesFile);
		}

		static int GetLicensePriority(string fileName)
		{
			var kind = GetLicenseKind(fileName);
			var prefixes = kind switch
			{
				LicenseDocumentType.License => LicenseFilePrefixes,
				LicenseDocumentType.ThirdPartyNotices => ThirdPartyNoticesFilePrefixes,
				_ => null,
			};
			if (prefixes == null)
				return int.MaxValue;

			var normalizedFileName = NormalizeLicenseFileName(Path.GetFileNameWithoutExtension(fileName) ?? fileName);
			for (var i = 0; i < prefixes.Length; i++)
			{
				if (normalizedFileName.StartsWith(NormalizeLicenseFileName(prefixes[i]), StringComparison.OrdinalIgnoreCase))
					return i;
			}
			return int.MaxValue;
		}

		static LicenseDocumentType GetLicenseKind(string fileName)
		{
			var normalizedFileName = NormalizeLicenseFileName(Path.GetFileNameWithoutExtension(fileName) ?? fileName);
			foreach (var prefix in LicenseFilePrefixes)
			{
				if (normalizedFileName.StartsWith(NormalizeLicenseFileName(prefix), StringComparison.OrdinalIgnoreCase))
					return LicenseDocumentType.License;
			}

			foreach (var prefix in ThirdPartyNoticesFilePrefixes)
			{
				if (normalizedFileName.StartsWith(NormalizeLicenseFileName(prefix), StringComparison.OrdinalIgnoreCase))
					return LicenseDocumentType.ThirdPartyNotices;
			}

			return LicenseDocumentType.Unknown;
		}

		static string NormalizeLicenseFileName(string fileName)
			=> (fileName ?? string.Empty)
				.Replace(" ", string.Empty)
				.Replace("-", string.Empty)
				.Replace("_", string.Empty);

		static string ToFullPath(string assetPath)
			=> Path.GetFullPath(assetPath);

		static string ToAssetPath(string path)
		{
			var fullPath = Path.GetFullPath(path);
			var projectRoot = Path.GetFullPath(".");
			var relativePath = Path.GetRelativePath(projectRoot, fullPath);
			return NormalizeAssetPath(relativePath);
		}

		static string NormalizeAssetPath(string path)
			=> path.Replace('\\', '/');

		static bool IsRepositoryMaterializedPath(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return false;

			var normalizedPath = NormalizeAssetPath(Path.GetFullPath(path));
			var projectRoot = NormalizeAssetPath(Path.GetFullPath("."));
			if (!normalizedPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
				return false;

			var relativePath = NormalizeAssetPath(Path.GetRelativePath(projectRoot, normalizedPath));
			var isRepositoryFolderPath = relativePath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase)
				|| relativePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(relativePath, "Packages", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(relativePath, "Assets", StringComparison.OrdinalIgnoreCase);
			if (!isRepositoryFolderPath)
				return false;

			if (RepositoryMaterializationCache.TryGetValue(relativePath, out var isMaterialized))
				return isMaterialized;

			isMaterialized = !IsGitIgnoredPath(projectRoot, relativePath);
			RepositoryMaterializationCache[relativePath] = isMaterialized;
			return isMaterialized;
		}

		static bool IsGitIgnoredPath(string projectRoot, string relativePath)
		{
			try
			{
				var startInfo = new ProcessStartInfo
				{
					FileName = "git",
					Arguments = $"check-ignore -q -- \"{relativePath.Replace("\"", "\\\"")}\"",
					WorkingDirectory = projectRoot,
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
				};

				using (var process = Process.Start(startInfo))
				{
					if (process == null)
						return false;

					process.WaitForExit(3000);
					if (!process.HasExited)
					{
						process.Kill();
						return false;
					}

					return process.ExitCode == 0;
				}
			}
			catch
			{
				return false;
			}
		}

		readonly struct PackageEntry
		{
			public PackageEntry(
				string displayName,
				string packageId,
				string version,
				string packageFullPath,
				string packageAssetPath,
				string metadataLicenseText = null,
				GitPackageLockEntry gitPackage = null,
				bool isUnityOwned = false,
				bool isRepositoryMaterialized = false
			)
			{
				DisplayName = displayName;
				PackageId = packageId;
				Version = version;
				PackageFullPath = packageFullPath;
				PackageAssetPath = packageAssetPath;
				MetadataLicenseText = metadataLicenseText;
				GitPackage = gitPackage;
				IsUnityOwned = isUnityOwned;
				IsRepositoryMaterialized = isRepositoryMaterialized;
			}

			public string DisplayName { get; }
			public string PackageId { get; }
			public string Version { get; }
			public string PackageFullPath { get; }
			public string PackageAssetPath { get; }
			public string MetadataLicenseText { get; }
			public GitPackageLockEntry GitPackage { get; }
			public bool IsUnityOwned { get; }
			public bool IsRepositoryMaterialized { get; }
		}

		enum LicenseDocumentType
		{
			Unknown,
			License,
			ThirdPartyNotices,
		}

		readonly struct LicenseAssetPair
		{
			public LicenseAssetPair(TextAsset licenseFile, TextAsset thirdPartyNoticesFile)
			{
				LicenseFile = licenseFile;
				ThirdPartyNoticesFile = thirdPartyNoticesFile;
			}

			public TextAsset LicenseFile { get; }
			public TextAsset ThirdPartyNoticesFile { get; }
			public bool HasAny => LicenseFile != null || ThirdPartyNoticesFile != null;

			public LicenseAssetPair WithLicenseFile(TextAsset licenseFile)
				=> new LicenseAssetPair(licenseFile, ThirdPartyNoticesFile);

			public LicenseAssetPair WithThirdPartyNoticesFile(TextAsset thirdPartyNoticesFile)
				=> new LicenseAssetPair(LicenseFile, thirdPartyNoticesFile);
		}

		sealed class GitPackageLockEntry
		{
			public string PackageId;
			public string VersionUrl;
			public string Source;
			public string Hash;
		}

		readonly struct GitHubRepositoryInfo
		{
			public GitHubRepositoryInfo(string owner, string repository, string commitHash)
			{
				Owner = owner;
				Repository = repository;
				CommitHash = commitHash;
			}

			public string Owner { get; }
			public string Repository { get; }
			public string CommitHash { get; }

			public string GetRawUrl(string fileName)
				=> $"https://raw.githubusercontent.com/{Owner}/{Repository}/{CommitHash}/{fileName}";
		}
	}
}
