using System.Collections.Generic;
using System.Diagnostics;

namespace Links.Licenses.Parser
{
	[DebuggerDisplay("{LicenseText}")]
	public class ThirdPartyLicense
	{
		public string Name;
		public string Text;
	}

	interface IThirdPartyNoticesParser
	{
		public bool IsSupportType(string name, string text);

		public IList<ThirdPartyLicense> Parse(string text);
	}
}
