
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Links.Licenses
{
	public class LicenseManifest : UnityEngine.ScriptableObject
	{
		[Serializable]
		public class SpdxTemplateEntry
		{
			public string SpdxIdentifier;
			public TextAsset Text;
		}

		[Serializable]
		public class SpdxLicenseEntry
		{
			public string ComponentName;
			public string SpdxIdentifier;
			public List<string> CopyrightList;
		}

		[Serializable]
		public class RawLicenseEntry
		{
			public string ComponentName;
			[Multiline(10)]
			public string LicenseText;
		}

		public const string Path = "LicenseManifest";

		public static bool TryGetInstance(out LicenseManifest instance)
		{
			instance = UnityEngine.Resources.Load<LicenseManifest>(Path);
			return instance != null;
		}

		public static LicenseManifest GetOrCreateInstance()
		{
			if (!TryGetInstance(out var instance))
			{
				var parent = "Assets/Resources";
				if (UnityEditor.AssetDatabase.IsValidFolder(parent) == false)
				{
					// Resourcesフォルダが無いことを考慮
					UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
				}
				instance = CreateInstance<LicenseManifest>();

				var assetPath = System.IO.Path.Combine(parent, System.IO.Path.ChangeExtension(Path, ".asset"));
				UnityEditor.AssetDatabase.CreateAsset(instance, assetPath);
			}
			return instance;
		}

		public List<SpdxTemplateEntry> SpdxTemplateList;
		public List<SpdxLicenseEntry> SpdxLicenseList;
		public List<RawLicenseEntry> RawEntryList;
	}
}
