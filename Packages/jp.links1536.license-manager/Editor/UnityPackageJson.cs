using System;

namespace Links.Licenses
{
	[Serializable]
	class AuthorInfo
	{
		public string name;
		public string url;
	}

	/// <summary>
	/// パッケージ情報
	/// </summary>
	[Serializable]
	sealed class UnityPackageJson
	{
		public string name;
		public string displayName;
		public string version;
		public string license;
		public string licensesUrl;
		public AuthorInfo author;
	}
}
