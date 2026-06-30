namespace Links.Licenses.Packages.UnityPackageManager
{
	/// <summary>
	/// サードパーティだとSPDX用にlicenseというキーが存在することがある
	/// </summary>
	internal class AdditionalUnityPackageInfo
	{
		[UnityEngine.SerializeField] string license;

		public string License
			=> license;
	}
}
