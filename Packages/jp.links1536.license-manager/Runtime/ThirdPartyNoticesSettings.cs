using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;

namespace Links.Licenses
{
	[CreateAssetMenu(fileName = "ThirdPartyNoticesSettings", menuName = "Scriptable Objects/ThirdPartyNoticesSettings")]
	public class ThirdPartyNoticesSettings : ScriptableObject
	{
		// OSS
		[Serializable]
		public class OSSEntry
		{
			public string Name;

			[FormerlySerializedAs("LicenseFile")]
			public TextAsset LicenseFile;

			public TextAsset ThirdPartyNoticesFile;

			[TextArea(3, 20)]
			public string LicenseText;

			public string SourcePath;

			public bool IsRepositoryMaterialized;

			public bool IsMetadataOnly;

			public bool HasLicenseContent()
				=> LicenseFile != null
				|| ThirdPartyNoticesFile != null
				|| !string.IsNullOrWhiteSpace(LicenseText);

			public string GetDisplayText()
			{
				var builder = new StringBuilder();

				builder.AppendLine("============================================================");
				builder.AppendLine(Name);
				builder.AppendLine("============================================================");
				builder.AppendLine();

				if (LicenseFile != null && !string.IsNullOrWhiteSpace(LicenseFile.text))
				{
					builder.AppendLine(LicenseFile.text);
					builder.AppendLine();
				}

				if (ThirdPartyNoticesFile != null && !string.IsNullOrWhiteSpace(ThirdPartyNoticesFile.text))
				{
					builder.AppendLine(ThirdPartyNoticesFile.text);
					builder.AppendLine();
				}

				if (LicenseFile == null && ThirdPartyNoticesFile == null && !string.IsNullOrWhiteSpace(LicenseText) )
				{
					builder.AppendLine(LicenseText);
					builder.AppendLine();
				}

				return builder.ToString();
			}
		}

		// ライセンスファイルが見つからなかったOSS
		[Serializable]
		public class MissingOSSEntry
		{
			public string Name;

			public string PackagePath;

			public string Reason;

			[FormerlySerializedAs("LicenseFile")]
			public TextAsset LicenseFile;

			public TextAsset ThirdPartyNoticesFile;

			public bool IsRepositoryMaterialized;

			public bool HasMetadataLicense;
		}

		public const string Path = "ThirdPartyLicenses";
		static ThirdPartyNoticesSettings m_Instance;

		public static bool TryGetInstance(out ThirdPartyNoticesSettings instance)
		{
			if (m_Instance == null)
				m_Instance = Resources.Load<ThirdPartyNoticesSettings>(Path);
			instance = m_Instance;
			return instance != null;
		}

		// 手動で追加
		[SerializeField] OSSEntry[] m_ManualEntries;

		// UPM経由
		[SerializeField] OSSEntry[] m_UnityPackageEntries;
		[SerializeField] MissingOSSEntry[] m_MissingUnityPackageEntries;

		// NuGet経由
		[SerializeField] OSSEntry[] m_NuGetEntries;
		[SerializeField] MissingOSSEntry[] m_MissingNuGetEntries;

		// 手動で追加
		public OSSEntry[] ManualOSSList
			=> m_ManualEntries ?? Array.Empty<OSSEntry>();

		// UPM経由
		public OSSEntry[] UnityPackageOSSList
			=> m_UnityPackageEntries ?? Array.Empty<OSSEntry>();
		public MissingOSSEntry[] MissingUnityPackageOSSList
			=> m_MissingUnityPackageEntries ?? Array.Empty<MissingOSSEntry>();

		// NuGet経由
		public OSSEntry[] NuGetOSSList
			=> m_NuGetEntries ?? Array.Empty<OSSEntry>();
		public MissingOSSEntry[] MissingNuGetOSSList
			=> m_MissingNuGetEntries ?? Array.Empty<MissingOSSEntry>();

		public IEnumerable<OSSEntry> OSSList
		{
			get
			{
				foreach (var entry in ManualOSSList)
					yield return entry;

				foreach (var entry in UnityPackageOSSList)
					yield return entry;

				foreach (var entry in NuGetOSSList)
					yield return entry;
			}
		}

		public string CreateLicenseText()
		{
			return CreateLicenseText(OSSList);
		}

		public string CreatePublicationLicenseText()
		{
			return CreateLicenseText(PublicationOSSList);
		}

		// 任意のOSS一覧からライセンス本文を組み立てる
		public string CreateLicenseText(IEnumerable<OSSEntry> entries)
		{
			return CreateLicenseTextInternal(entries);
		}

		string CreateLicenseTextInternal(IEnumerable<OSSEntry> entries)
		{
			var builder = new StringBuilder();

			builder.AppendLine("THIRD PARTY NOTICES");
			builder.AppendLine();
			builder.AppendLine("This product includes software developed by third parties.");
			builder.AppendLine();

			var ossEntries = entries.Where(x => x != null).ToArray();
			if (ossEntries.Length == 0)
				return builder.ToString();

			foreach (var oss in ossEntries)
			{
				if (oss == null)
					continue;

				builder.AppendLine(oss.GetDisplayText());
			}

			return builder.ToString();
		}

		public string CreateAuditReport()
		{
			var builder = new StringBuilder();

			builder.AppendLine("サードパーティ ライセンス監査");
			builder.AppendLine();

			builder.AppendLine($"管理外パッケージ数: {ManualOSSList.Length}");
			builder.AppendLine($"Unity Packages数: {UnityPackageOSSList.Length}");
			builder.AppendLine($"NuGet for Unity数: {NuGetOSSList.Length}");
			builder.AppendLine($"公開対象パッケージ数: {PublicationOSSList.Count()}");

			builder.AppendLine($"Unity Packages 不足数: {MissingUnityPackageOSSList.Length}");
			builder.AppendLine($"NuGet for Unity 不足数: {MissingNuGetOSSList.Length}");
			builder.AppendLine($"公開対象の Unity Packages 不足数: {PublicationMissingUnityPackageOSSList.Length}");
			builder.AppendLine($"公開対象の NuGet for Unity 不足数: {PublicationMissingNuGetOSSList.Length}");

			builder.AppendLine();

			AppendEntrySection(builder, "管理外パッケージ", ManualOSSList);
			AppendEntrySection(builder, "Unity Packages", UnityPackageOSSList);
			AppendEntrySection(builder, "NuGet for Unity", NuGetOSSList);
			AppendMissingSection(builder, "不足している Unity Packages", MissingUnityPackageOSSList);
			AppendMissingSection(builder, "不足している NuGet for Unity", MissingNuGetOSSList);
			AppendEntrySection(builder, "リポジトリ公開対象エントリ", PublicationOSSList.ToArray());
			AppendMissingSection(builder, "リポジトリ公開対象の不足 Unity Packages", PublicationMissingUnityPackageOSSList);
			AppendMissingSection(builder, "リポジトリ公開対象の不足 NuGet for Unity", PublicationMissingNuGetOSSList);

			return builder.ToString();
		}

		public IEnumerable<OSSEntry> PublicationOSSList
		{
			get
			{
				foreach (var entry in ManualOSSList.Where(IsPublicationEligibleEntry))
					yield return entry;

				foreach (var entry in UnityPackageOSSList.Where(IsPublicationEligibleEntry))
					yield return entry;

				foreach (var entry in NuGetOSSList.Where(IsPublicationEligibleEntry))
					yield return entry;
			}
		}

		public MissingOSSEntry[] PublicationMissingUnityPackageOSSList
			=> MissingUnityPackageOSSList.Where(IsPublicationRelevantMissingEntry).ToArray();

		public MissingOSSEntry[] PublicationMissingNuGetOSSList
			=> MissingNuGetOSSList.Where(IsPublicationRelevantMissingEntry).ToArray();

		public void SetUnityPackageEntries(OSSEntry[] entries, MissingOSSEntry[] missingEntries)
		{
			m_UnityPackageEntries = entries ?? Array.Empty<OSSEntry>();
			m_MissingUnityPackageEntries = missingEntries ?? Array.Empty<MissingOSSEntry>();
		}

		public void SetNuGetEntries(OSSEntry[] entries, MissingOSSEntry[] missingEntries)
		{
			m_NuGetEntries = entries ?? Array.Empty<OSSEntry>();
			m_MissingNuGetEntries = missingEntries ?? Array.Empty<MissingOSSEntry>();
		}

		static void AppendEntrySection(StringBuilder builder, string title, OSSEntry[] entries)
		{
			builder.AppendLine(title);
			builder.AppendLine("------------------------------------------------------------");

			if (entries == null || entries.Length == 0)
			{
				builder.AppendLine("なし");
				builder.AppendLine();
				return;
			}

			foreach (var entry in entries)
			{
				if (entry == null)
					continue;

				builder.Append("- ");
				builder.Append(entry.Name);
				builder.Append(" : ");
				if (entry.LicenseFile != null && entry.ThirdPartyNoticesFile != null)
					builder.AppendLine($"{entry.LicenseFile.name}, {entry.ThirdPartyNoticesFile.name}");
				else if (entry.LicenseFile != null)
					builder.AppendLine(entry.LicenseFile.name);
				else if (entry.ThirdPartyNoticesFile != null)
					builder.AppendLine(entry.ThirdPartyNoticesFile.name);
				else if (!string.IsNullOrWhiteSpace(entry.LicenseText))
					builder.AppendLine("パッケージ情報");
				else
					builder.AppendLine("ライセンスファイル未設定");
			}

			builder.AppendLine();
		}

		static void AppendMissingSection(StringBuilder builder, string title, MissingOSSEntry[] entries)
		{
			builder.AppendLine(title);
			builder.AppendLine("------------------------------------------------------------");

			if (entries == null || entries.Length == 0)
			{
				builder.AppendLine("なし");
				builder.AppendLine();
				return;
			}

			foreach (var entry in entries)
			{
				if (entry == null)
					continue;

				builder.Append("- ");
				builder.Append(entry.Name);
				builder.Append(" : ");
				builder.Append(entry.Reason);

				if (!string.IsNullOrWhiteSpace(entry.PackagePath))
				{
					builder.Append(" (");
					builder.Append(entry.PackagePath);
					builder.Append(')');
				}

				builder.AppendLine();
			}

			builder.AppendLine();
		}

		static bool IsPublicationEligibleEntry(OSSEntry entry)
		{
			if (entry == null)
				return false;

			var hasLicenseContent = entry.HasLicenseContent();
			if (!hasLicenseContent)
				return false;

			if (string.IsNullOrWhiteSpace(entry.SourcePath))
				return true;

			return entry.IsRepositoryMaterialized && !entry.IsMetadataOnly;
		}

		static bool IsPublicationRelevantMissingEntry(MissingOSSEntry entry)
			=> entry != null && entry.IsRepositoryMaterialized;
	}
}
