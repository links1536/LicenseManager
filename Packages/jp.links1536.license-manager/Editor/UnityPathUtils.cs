using System.IO;

namespace Links.Licenses
{
	public static class UnityPathUtils
	{
		public static string ToFullPath(string assetPath)
			=> Path.GetFullPath(assetPath);

		public static string ToAssetPath(string path)
		{
			var fullPath = Path.GetFullPath(path);
			var projectRoot = Path.GetFullPath(".");
			var relativePath = Path.GetRelativePath(projectRoot, fullPath);
			return NormalizeAssetPath(relativePath);
		}

		public static string NormalizeAssetPath(string path)
			=> path.Replace(@"\", @"/");

		public static bool IsLocalPackages(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return false;

			var normalizedPath = NormalizeAssetPath(Path.GetFullPath(path));
			var projectRoot = NormalizeAssetPath(Path.GetFullPath("."));
			if (!normalizedPath.StartsWith(projectRoot, System.StringComparison.OrdinalIgnoreCase))
				return false;

			// プロジェクトからの相対パス
			var relativePath = NormalizeAssetPath(Path.GetRelativePath(projectRoot, normalizedPath));

			// Package Manager からインストールされた UPMパッケージか
			bool isLibrary = relativePath.StartsWith("Library/", System.StringComparison.OrdinalIgnoreCase);
			if (isLibrary)
				return false;

			// 展開された UPMパッケージか
			bool isLocalUpm = relativePath.StartsWith("Packages/", System.StringComparison.OrdinalIgnoreCase);
			if (isLocalUpm)
				return true;

			// NuGet のパッケージが置かれる場所
			var isNuGetPackages = relativePath.StartsWith("Assets/Packages", System.StringComparison.OrdinalIgnoreCase);
			if (isNuGetPackages)
				return false;

			return true;
		}

	}
}
