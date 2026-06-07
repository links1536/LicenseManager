using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Links.Licenses
{
	public static class ThirdPartyNoticesGenerator
	{
		public sealed class GenerationResult
		{
			public string OutputDirectory { get; internal set; }
			public int TotalEntryCount { get; internal set; }
			public int MissingEntryCount { get; internal set; }
		}

		public static GenerationResult GenerateForProjectPublication(bool refreshEntries = true)
		{
			var settings = ThirdPartyNoticesSettingsProvider.GetOrCreateSettings();
			return GenerateForProjectPublication(settings, refreshEntries);
		}

		public static GenerationResult GenerateForProjectPublication(ThirdPartyNoticesSettings settings, bool refreshEntries = true)
		{
			if (settings == null)
				throw new ArgumentNullException(nameof(settings));

			if (refreshEntries)
				ThirdPartyNoticesSettingsEditor.RefreshAllEntries(settings);

			var projectRoot = GetProjectRootPath();
			var researchOutputDirectory = Path.Combine(projectRoot, ThirdPartyNoticesSettingsEditor.PublicationResearchFolderPathForGenerator);
			Directory.CreateDirectory(researchOutputDirectory);
			WritePublicationArtifacts(settings, projectRoot, researchOutputDirectory);

			return new GenerationResult
			{
				OutputDirectory = projectRoot,
				TotalEntryCount = settings.PublicationOSSList.Count(),
				MissingEntryCount = settings.PublicationMissingUnityPackageOSSList.Length + settings.PublicationMissingNuGetOSSList.Length,
			};
		}

		static void WritePublicationArtifacts(ThirdPartyNoticesSettings settings, string projectRoot, string researchOutputDirectory)
		{
			var encoding = new UTF8Encoding(false);

			var publicationEntries = settings.PublicationOSSList.ToArray();
			var publicationNoticesPath = Path.Combine(projectRoot, ThirdPartyNoticesSettingsEditor.PublicationCombinedFileNameForGenerator);
			// 公開対象がある場合のみリポジトリ向けの notices を出力する
			if (publicationEntries.Length > 0)
			{
				File.WriteAllText(
					publicationNoticesPath,
					settings.CreateLicenseText(publicationEntries),
					encoding
				);
			}
			// 0件になった場合は前回生成分を残さない
			else if (File.Exists(publicationNoticesPath))
			{
				File.Delete(publicationNoticesPath);
			}

			File.WriteAllText(
				Path.Combine(researchOutputDirectory, ThirdPartyNoticesSettingsEditor.PublicationAuditFileNameForGenerator),
				settings.CreateAuditReport(),
				encoding
			);
		}

		static string GetProjectRootPath()
			=> Directory.GetParent(Application.dataPath)?.FullName ?? Environment.CurrentDirectory;

	}
}
