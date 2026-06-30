using System.IO;
using System.Xml.Linq;

namespace Links.Licenses.Packages.NuGet
{
	class NuSpec
	{
		public string Id { get; private set; }
		public string Version { get; private set; }
		public string Authors { get; private set; }
		public string Description { get; private set; }
		public string ProjectUrl { get; private set; }
		public string Readme { get; private set; }
		public string Copyright { get; private set; }
		public string LicenseType { get; private set; }
		public string LicenseUrl { get; private set; }
		public string RepositoryType { get; private set; }
		public string RepositoryUrl { get; private set; }
		public string RepositoryCommit { get; private set; }

		public static bool TryLoad(string nuspecPath, out NuSpec nuSpec)
		{
			nuSpec = null;

			if (string.IsNullOrWhiteSpace(nuspecPath) || !File.Exists(nuspecPath))
				return false;

			try
			{
				var document = XDocument.Load(nuspecPath);
				var root = document.Root;
				if (root == null)
					return false;

				var ns = root.Name.Namespace;
				var metadata = root.Element(ns + "metadata");
				if (metadata == null)
					return false;

				var licenseElement = metadata.Element(ns + "license");
				var repositoryElement = metadata.Element(ns + "repository");

				// SPDX Identifier
				bool isLicenseExpression = ReadAttribute(licenseElement, "type") == "expression";
				var licenseType = isLicenseExpression
					? ReadElementValue(licenseElement)
					: nuspecPath;

				nuSpec = new NuSpec
				{
					// パッケージ情報
					Id = ReadValue(metadata, ns + "id"),
					Version = ReadValue(metadata, ns + "version"),
					Authors = ReadValue(metadata, ns + "authors"),
					Description = ReadValue(metadata, ns + "description"),
					Readme = ReadValue(metadata, ns + "readme"),

					// プロジェクトの情報
					ProjectUrl = ReadValue(metadata, ns + "projectUrl"),
					Copyright = ReadValue(metadata, ns + "copyright"),

					// ライセンス情報
					LicenseType = licenseType,
					LicenseUrl = ReadValue(metadata, ns + "licenseUrl"),

					// プロジェクトのリポジトリ情報
					RepositoryType = ReadAttribute(repositoryElement, "type"),
					RepositoryUrl = ReadAttribute(repositoryElement, "url"),
					RepositoryCommit = ReadAttribute(repositoryElement, "commit"),
				};

				return true;
			}
			catch
			{
				return false;
			}
		}

		static string ReadValue(XElement parent, XName name)
				=> ReadElementValue(parent?.Element(name));

		static string ReadElementValue(XElement element)
		{
			var value = Parser.ParserUtils.TrimLines(element?.Value);
			return string.IsNullOrWhiteSpace(value) ? null : value;
		}

		static string ReadAttribute(XElement element, XName name)
		{
			var value = Parser.ParserUtils.TrimLines(element?.Attribute(name)?.Value);
			return string.IsNullOrWhiteSpace(value) ? null : value;
		}
	}
}
