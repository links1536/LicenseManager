using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Links.Licenses.Packages.NuGet;
using Links.Licenses.Packages.UnityPackageManager;
using Links.Licenses.Plugins;
using UnityEditor;
using UnityEngine;

namespace Links.Licenses
{
	using UnityPackageInfo = UnityEditor.PackageManager.PackageInfo;

	[DebuggerDisplay("{Id}:{Name}")]
	class PackageLicenseData
	{
		public string Id;
		public string Name;
		public string Auther;
		public string AssetPath;
		public string ResolvedPath;

		public string Copyright;
		public string LicenseType;

		public TextAsset LicenseFile;
		public TextAsset ThirdPartyNoticesFile;
	}

	class LicenseFinder
	{
		struct AutoClearProgressBar : IDisposable
		{
			public void Dispose()
			{
				EditorUtility.ClearProgressBar();
			}
		}

		class AssetEditScope : IDisposable
		{
			public AssetEditScope()
			{
				AssetDatabase.StartAssetEditing();
			}
			public void Dispose()
			{
				AssetDatabase.StopAssetEditing();
			}
		}

		static string[] LicenseFileNames = new string[]
		{
			"LICENSE",
			"COPYING",
		};

		static string[] ThirdPartyNoticesFileNames = new string[]
		{
			"THIRD-PARTY-NOTICES",
			"THIRD PARTY NOTICES",
			"THIRD_PARTY_NOTICES",
			"THIRDPARTYNOTICES",
			"NOTICE",
		};

		static readonly string[] LicenseFileExtensions =
		{
			string.Empty,
			".txt",
			".md",
			".markdown",
		};

		static void CollectUPMPackages(UnityPackageInfo packageInfo, Dictionary<string, UnityPackageInfo> packageDict, Dictionary<string, UnityPackageInfo> activePackageDict)
		{
			if (activePackageDict.ContainsKey(packageInfo.name))
				return;

			if (!IsInRuntime(packageInfo))
				return;

			activePackageDict[packageInfo.name] = packageInfo;

			foreach (var dependency in packageInfo.dependencies)
			{
				if (packageDict.TryGetValue(dependency.name, out var dependencyPackageInfo))
				{
					CollectUPMPackages(dependencyPackageInfo, packageDict, activePackageDict);
				}
			}
		}

		static bool IsInRuntime(UnityPackageInfo packageInfo)
			=> AssemblyDefinitionHelper.IsInRuntime(packageInfo.assetPath)
			|| PluginHelper.IsInRuntime(packageInfo.assetPath);

		public static List<PackageLicenseData> GetUPMPackages()
		{
			using var clearProgressBar = new AutoClearProgressBar();

			var packages = UnityPackageInfo.GetAllRegisteredPackages();
			var packageDict = new Dictionary<string, UnityPackageInfo>();
			var activePackageDict = new Dictionary<string, UnityPackageInfo>();
			foreach (var package in packages)
				packageDict[package.name] = package;

			foreach(var package in packages)
			{
				if (string.IsNullOrWhiteSpace(package.name) || string.IsNullOrWhiteSpace(package.assetPath))
					continue;

				if (!package.isDirectDependency)
					continue;
				CollectUPMPackages(package, packageDict, activePackageDict);
			}

			int progress = 0;
			var list = new List<PackageLicenseData>(packages.Length);
			foreach (var package in activePackageDict.Values)
			{
				if (package == null)
					continue;

				progress++;
				EditorUtility.DisplayProgressBar("Unity Package Manager情報を更新", $"{package.displayName} ({package.name})", (float)progress / packages.Length);

				string packageJsonPath = Path.Combine(package.resolvedPath, "package.json");
				string json = File.ReadAllText(packageJsonPath);
				var additionalData = JsonUtility.FromJson<AdditionalUnityPackageInfo>(json);

				var packageLicenseData = new PackageLicenseData()
				{
					Id = package.name,
					Name = package.displayName,
					Auther = package.author?.name,
					AssetPath = package.assetPath,
					ResolvedPath = package.resolvedPath,
					Copyright = package.author?.name,

					LicenseType = additionalData.License,
					LicenseFile = FindTextAsset(package.assetPath, LicenseFileNames),
					ThirdPartyNoticesFile = FindTextAsset(package.assetPath, ThirdPartyNoticesFileNames),
				};

				bool isUnityPackage = package.name.StartsWith("com.unity.", StringComparison.OrdinalIgnoreCase);
				if (isUnityPackage)
				{
					if (string.IsNullOrEmpty(packageLicenseData.LicenseType)
					 && packageLicenseData.LicenseFile == null
					 && packageLicenseData.ThirdPartyNoticesFile == null)
						continue;
				}

				list.Add(packageLicenseData);
			}

			return list;
		}

		public static List<PackageLicenseData> GetNuGetPackages()
		{
			using var clearProgressBar = new AutoClearProgressBar();

			var list = new List<PackageLicenseData>();
			var nuspecPaths = Directory.GetFiles("Assets/Packages", "*.nuspec", SearchOption.AllDirectories);
			for (int i = 0; i < nuspecPaths.Length; i++)
			{
				string nuspecPath = nuspecPaths[i];
				if (!NuSpec.TryLoad(nuspecPath, out var nuSpec))
					continue;
				var directory = Path.GetDirectoryName(nuspecPath);
				directory = UnityPathUtils.NormalizeAssetPath(directory);

				EditorUtility.DisplayProgressBar("NuGet for Unity情報を更新", $"{nuSpec.Id}", (float)i / nuspecPaths.Length);

				var packageLicenseData = new PackageLicenseData()
				{
					Id = nuSpec.Id,
					Name = nuSpec.Id,
					Auther = nuSpec.Authors,
					AssetPath = directory,
					ResolvedPath = directory,

					Copyright = nuSpec.Copyright,

					LicenseType = nuSpec.LicenseType,
					LicenseFile = FindTextAsset(directory, LicenseFileNames),
					ThirdPartyNoticesFile = FindTextAsset(directory, ThirdPartyNoticesFileNames),
				};
				list.Add(packageLicenseData);

			}

			return list;
		}


		static TextAsset FindTextAsset(string directory, string[] fileNames)
		{
			foreach (var fileNameBase in fileNames)
			{
				foreach (var extension in LicenseFileExtensions)
				{
					string fileName = Path.ChangeExtension(fileNameBase, extension);
					string assetPath = Path.Combine(directory, fileName);
					assetPath = UnityPathUtils.NormalizeAssetPath(assetPath);
					if (!AssetDatabase.AssetPathExists(assetPath))
						continue;

					var file = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
					if (file == null || string.IsNullOrEmpty(file.text))
						continue;

					return file;
				}
			}

			return null;
		}

	}
}
