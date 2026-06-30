using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Links.Licenses.Parser
{
	class CargoAboutThirdPartyNoticesParser : IThirdPartyNoticesParser
	{
		const string NewLine = @"(?:\r\n|\n|\r)";

		Regex m_SupportTypeRegex = new Regex(
			$@"^This library depends on following third-party components\.(?:{NewLine}+)Overview of included licen[cs]es:",
			RegexOptions.Compiled
		);

		const string Separator = "--------------------------------------------------------------------------------";

		public bool IsSupportType(string name, string text)
			=> m_SupportTypeRegex.IsMatch(text);

		public IList<ThirdPartyLicense> Parse(string text)
		{
			var groups = text.Split(Separator, System.StringSplitOptions.None);

			// 最初の一つはまとめなので無視する
			var list = new List<ThirdPartyLicense>();
			foreach (var group in groups.Skip(1))
			{
				var noticeText = ParserUtils.TrimLines(group);

				if (string.IsNullOrEmpty(noticeText))
					continue;

				list.Add(new ThirdPartyLicense()
				{
					Text = noticeText,
				});
			}

			return list;
		}
	}
}
