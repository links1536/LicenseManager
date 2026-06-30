using UnityEditor;

namespace Links.Licenses.Settings
{
	[CustomEditor(typeof(LicenseManifest))]
	public class LicenseManifestEditor : Editor
	{
		public override void OnInspectorGUI()
			=> base.OnInspectorGUI();
	}
}
