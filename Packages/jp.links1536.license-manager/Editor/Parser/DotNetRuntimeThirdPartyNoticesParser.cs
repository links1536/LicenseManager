using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Links.Licenses.Parser
{
	class DotNetRuntimeThirdPartyNoticesParser : IThirdPartyNoticesParser
	{
		const string NewLine = @"(?:\r\n|\n|\r)";
		const string NoticeHeader = @"(?:License notice for|Third party notice for|License for)";
		
		Regex m_SupportTypeRegex = new Regex(
			$@"^\.NET Runtime uses third-party libraries or other resources that may be{NewLine}distributed under licenses different than the \.NET Runtime software\.{NewLine}",
			RegexOptions.Compiled
		);

		// 本文用正規表現
		// 名前とライセンス本文で分離して取得する
		Regex m_Regex = new Regex(
			$@"^{NoticeHeader}\s+(?<name>[\s\S]*?){NewLine}(?:-+){NewLine}(?<text>[\s\S]*?)(?=^{NoticeHeader}\s+[\s\S]*?{NewLine}(?:-+){NewLine}|\z)",
			RegexOptions.Multiline | RegexOptions.Compiled
		);

		public bool IsSupportType(string name, string text)
			=> m_SupportTypeRegex.IsMatch(text);

		public IList<ThirdPartyLicense> Parse(string text)
		{
			var matches = m_Regex.Matches(text);

			var list = new List<ThirdPartyLicense>();
			foreach (Match match in matches)
			{
				var name = ParserUtils.TrimLines(match.Groups["name"].Value);
				var noticeText = ParserUtils.TrimLines(match.Groups["text"].Value);

				if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(noticeText))
					continue;

				list.Add(new ThirdPartyLicense()
				{
					Name = name,
					Text = noticeText,
				});
			}

			return list;
		}
	}
}
