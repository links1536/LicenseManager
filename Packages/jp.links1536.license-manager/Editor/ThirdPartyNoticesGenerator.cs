using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Links.Licenses.Parser;
using Links.Licenses.Settings;
using UnityEngine;

namespace Links.Licenses
{
	public static class LicenseNoticesGenerator
	{
		internal const string GenerateFileName = "THIRD PARTY NOTICES.md";

		internal const string Header = "# THIRD PARTY NOTICES\n- This project  contains third-party software components governed by the license(s) indicated below";

		public sealed class GenerationResult
		{
			public string OutputDirectory { get; internal set; }
			public int TotalEntryCount { get; internal set; }
			public int MissingEntryCount { get; internal set; }
		}

		static IThirdPartyNoticesParser[] m_ThirdPartyNoticesParsers = new IThirdPartyNoticesParser[]
		{
			new UnityThirdPartyNoticesParser(),
			new DotNetRuntimeThirdPartyNoticesParser(),
			new CargoAboutThirdPartyNoticesParser(),
		};

		public static (List<LicenseManifest.SpdxLicenseEntry> spdxList, List<LicenseManifest.RawLicenseEntry> rawList) CreateLicenseManifest()
		{
			// すべてのOSSを探査
			var list = LicenseSettings.instance.AllPackages;
			return CreateLicenseManifest(list, true);
		}

		public static (List<LicenseManifest.SpdxLicenseEntry> spdxList, List<LicenseManifest.RawLicenseEntry> rawList) CreateLicenseManifest(IEnumerable<LicenseSettings.Entry> list, bool isRuntimeManifest)
		{
			var unityLicenseList = new List<(string Name, string Text)>();
			var spdxList = new List<LicenseManifest.SpdxLicenseEntry>();
			var licenseDict = new Dictionary<string, LicenseManifest.RawLicenseEntry>();
			foreach (var entry in list)
			{
				if (isRuntimeManifest && !entry.IsInRuntime)
					continue;

				// アタッチされたライセンス情報
				bool isSpdx = entry.HasSpdxLicense();
				if (isSpdx)
				{
					var spdxEntry = new LicenseManifest.SpdxLicenseEntry()
					{
						ComponentName = entry.Name,
						SpdxIdentifier = entry.LicenseType,
						CopyrightList = entry.CopyrightList,
					};
					spdxList.Add(spdxEntry);
				}
				else
				{
					if (entry.LicenseFile != null && !string.IsNullOrEmpty(entry.LicenseFile.text))
					{
						if (IsUnityCompanionLicense(entry.LicenseFile.text))
							unityLicenseList.Add((entry.Id, entry.LicenseFile.text));
						else
							AddRaw(licenseDict, entry.Id, entry.LicenseFile.text);
					}
				}

				// パッケージが使用しているOSS
				if (entry.ThirdPartyNoticesFile != null && !string.IsNullOrEmpty(entry.ThirdPartyNoticesFile.text))
				{
					var thirdPartyList = ParseThirdPartyNotices(entry.Name, entry.ThirdPartyNoticesFile.text);
					if (thirdPartyList != null && thirdPartyList.Count > 0)
					{
						foreach (var thirdPartyEntry in thirdPartyList)
						{
							if (thirdPartyEntry == null)
								continue;
							if (IsUnityCompanionLicense(thirdPartyEntry.Text))
								unityLicenseList.Add((thirdPartyEntry.Name, thirdPartyEntry.Text));
							else
								AddRaw(licenseDict, thirdPartyEntry.Name, thirdPartyEntry.Text);
						}
					}
					else
					{
						Debug.LogWarning($"Not support format", entry.ThirdPartyNoticesFile);
					}
				}
			}

			// Unityのライセンスは最後にまとめて
			unityLicenseList.Sort((x, y) => x.Name.CompareTo(y.Name));
			foreach (var unityLicense in unityLicenseList)
				AddRaw(licenseDict, unityLicense.Name, unityLicense.Text);

			var licenseList = licenseDict.Values.ToList();
			//licenseList.Sort((x, y) => x.Name.CompareTo(y.Name));
			return (spdxList, licenseList);
		}

		public static GenerationResult OutputThirdPartyNotices(LicenseSettings settings)
		{
			if (settings == null)
				throw new ArgumentNullException(nameof(settings));

			var outputDirectory = Environment.CurrentDirectory;
			OtuputThirdPartyNotices(settings, outputDirectory);

			return new GenerationResult
			{
				OutputDirectory = outputDirectory,
				TotalEntryCount = settings.LocalPackages.Count(),
				MissingEntryCount = settings.UnityPackages.Count(x => x.LicenseFile == null),
			};
		}

		static void OtuputThirdPartyNotices(LicenseSettings settings, string projectRoot)
		{
			var encoding = new UTF8Encoding(false);

			var localEntries = settings.LocalPackages.ToArray();
			var publicationNoticesPath = Path.Combine(projectRoot, GenerateFileName);

			// 公開対象がある場合のみ、リポジトリ向けの THIRD PARTY NOTICES を出力する
			if (localEntries.Length > 0)
			{
				(var spdx, var licenses) = CreateLicenseManifest(localEntries, false);

				using var stream = File.Open(publicationNoticesPath, FileMode.Create, FileAccess.Write, FileShare.Write);
				using var writer = new StreamWriter(stream, encoding);

				// ヘッダー
				writer.WriteLine("# THIRD PARTY NOTICES");
				writer.WriteLine();
				writer.WriteLine("This project  contains third-party software components governed by the license(s) indicated below");
				writer.WriteLine();

				// 本文を並べていく
				foreach (var license in licenses)
				{
					writer.WriteLine($"----");
					writer.WriteLine(license.LicenseText);
				}

				foreach (var license in spdx)
				{
					writer.WriteLine($"----");
					writer.WriteLine($"## {license.ComponentName}");
					writer.WriteLine($"- SPDX-License: {license.SpdxIdentifier}");
					foreach (var copyright in license.CopyrightList)
						writer.WriteLine($"- Copyright: {copyright}");
				}
			}
			else if (File.Exists(publicationNoticesPath))
			{
				// 0件になった場合は前回生成分を残さない
				File.Delete(publicationNoticesPath);
			}
		}

		static IList<ThirdPartyLicense> ParseThirdPartyNotices(string name, string notices)
		{
			foreach (var parser in m_ThirdPartyNoticesParsers)
			{
				if (!parser.IsSupportType(name, notices))
					continue;
				return parser.Parse(notices);
			}

			return null;
		}

		static void AddRaw(Dictionary<string, LicenseManifest.RawLicenseEntry> dict,　string name, string text)
		{
			text = ParserUtils.TrimLines(text);

			if (!dict.TryGetValue(name, out var entry))
			{
				entry = new LicenseManifest.RawLicenseEntry
				{
					ComponentName = name,
					LicenseText = text,
				};
				dict[name] = entry;
			}
		}

		static bool IsUnityCompanionLicense(string text)
			=> text.Contains("Licensed under the Unity Companion License for Unity-dependent projects", StringComparison.CurrentCultureIgnoreCase);

	}
}
