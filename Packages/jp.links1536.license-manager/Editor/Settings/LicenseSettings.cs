using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Links.Licenses.Settings
{
	[FilePath("ProjectSettings/LicenseSettings.asset", FilePathAttribute.Location.ProjectFolder)]
	public class LicenseSettings : ScriptableSingleton<LicenseSettings>
	{
		[Serializable]
		public class Entry
		{
			public string Id;
			public string Name;
			public List<string> CopyrightList;
			public string PackagePath;

			/// <summary>
			/// SPDX
			/// </summary>
			public string LicenseType;

			/// <summary>
			/// LICENSEファイル
			/// </summary>
			public TextAsset LicenseFile;

			/// <summary>
			/// THIRD PARTY NOTICESファイル
			/// </summary>
			public TextAsset ThirdPartyNoticesFile;

			public bool IsLocalPackages;
			public bool IsInRuntime;

			public bool HasSpdxLicense()
				=> !string.IsNullOrEmpty(LicenseType)
				&& CopyrightList != null
				&& !CopyrightList.Any(string.IsNullOrEmpty);

			public bool HasLicenseContent()
				=> !HasSpdxLicense()
				|| LicenseFile != null
				|| ThirdPartyNoticesFile != null;
		}

		// 手動で追加
		[SerializeField] Entry[] m_UnmanagedAssets;

		// UPM経由
		[SerializeField] Entry[] m_UnityPackages;

		// NuGet経由
		[SerializeField] Entry[] m_NuGetPackages;

		// 手動で追加
		public Entry[] UnmanagedAssets
			=> m_UnmanagedAssets
			?? Array.Empty<Entry>();

		// UPM経由
		public Entry[] UnityPackages
			=> m_UnityPackages
			?? Array.Empty<Entry>();

		// NuGet経由
		public Entry[] NuGetPackages
			=> m_NuGetPackages
			?? Array.Empty<Entry>();

		public IEnumerable<Entry> AllPackages
		{
			get
			{
				foreach (var entry in UnmanagedAssets)
					yield return entry;

				foreach (var entry in UnityPackages)
					yield return entry;

				foreach (var entry in NuGetPackages)
					yield return entry;
			}
		}

		public IEnumerable<Entry> LocalPackages
		{
			get
			{
				foreach (var entry in UnmanagedAssets.Where(IsLocalPackage).Where(HasLicense))
					yield return entry;

				foreach (var entry in UnityPackages.Where(IsLocalPackage).Where(HasLicense))
					yield return entry;

				foreach (var entry in NuGetPackages.Where(IsLocalPackage).Where(HasLicense))
					yield return entry;
			}
		}

		public void SetUnityPackageEntries(Entry[] entries)
		{
			m_UnityPackages = entries ?? Array.Empty<Entry>();
		}

		public void SetNuGetEntries(Entry[] entries)
		{
			m_NuGetPackages = entries ?? Array.Empty<Entry>();
		}

		static bool HasLicense(Entry entry)
			=> entry != null
			&& entry.HasLicenseContent();

		static bool IsLocalPackage(Entry entry)
			=> entry != null
			&& entry.IsLocalPackages;

		public void Save()
		{
			Save(true);
		}
	}
}
