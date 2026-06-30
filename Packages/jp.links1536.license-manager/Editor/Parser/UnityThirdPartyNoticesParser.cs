using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Links.Licenses.Parser
{
	class UnityThirdPartyNoticesParser : IThirdPartyNoticesParser
	{
		Regex m_SupportTypeRegex = new Regex(
			$@"^{Regex.Escape("This package contains third-party software components governed by the license(s) indicated below:")}",
			RegexOptions.Compiled
		);

		static string Separator = "---------";

		Regex m_ComponentRegex = new Regex(
			$@"^(?:#\s\[(?<name>.*)\].*|Component Name:(?:\s*)(?<name>.*))$",
			RegexOptions.Multiline | RegexOptions.Compiled
		);

		Regex m_LicenseTypeRegex = new Regex(
			$@"^License Type:(?: )",
			RegexOptions.Compiled
		);

		public bool IsSupportType(string name, string text)
			=> m_SupportTypeRegex.IsMatch(text);

		public IList<ThirdPartyLicense> Parse(string text)
		{
			var groups = text.Split(Separator, System.StringSplitOptions.None);

			// 最初の一つはまとめなので無視する
			var list = new List<ThirdPartyLicense>();
			foreach (var group in groups.Skip(1))
			{
				var noticeText = group;

				var nameMatches = m_ComponentRegex.Matches(group);
				List<string> componentNameList = new List<string>();
				foreach (Match match in nameMatches)
				{
					if (!match.Success)
						continue;
					string name = match.Groups["name"]?.Value;
					if (string.IsNullOrEmpty(name))
						continue;
					componentNameList.Add(name);
					noticeText = m_ComponentRegex.Replace(group, string.Empty, 1);
				}

				noticeText = ParserUtils.TrimLines(noticeText);

				// 先頭の License Type: を削除する
				noticeText = m_LicenseTypeRegex.Replace(noticeText, string.Empty, 1);
				noticeText = ParserUtils.TrimLines(noticeText);

				if (string.IsNullOrEmpty(noticeText))
					continue;

				list.Add(new ThirdPartyLicense()
				{
					Name = string.Join(", ", componentNameList),
					Text = noticeText,
				});
			}

			return list;
		}
	}
}
