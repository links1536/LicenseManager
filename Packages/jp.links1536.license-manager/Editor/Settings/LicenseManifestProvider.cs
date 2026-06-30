using UnityEditor;
using UnityEngine.UIElements;

namespace Links.Licenses.Settings
{
	class LicenseManifestProvider : SettingsProvider
	{
		Editor m_Editor;

		public LicenseManifestProvider(string path)
			: base(path, SettingsScope.Project)
		{
		}

		public override void OnActivate(string searchContext, VisualElement rootElement)
		{
			var instance = LicenseManifest.GetOrCreateInstance();
			Editor.CreateCachedEditor(instance, typeof(LicenseManifestEditor), ref m_Editor);
		}

		public override void OnGUI(string searchContext)
		{
			var instance = LicenseManifest.GetOrCreateInstance();
			if (m_Editor == null || m_Editor.target != instance)
				Editor.CreateCachedEditor(instance, typeof(LicenseManifestEditor), ref m_Editor);

			m_Editor.OnInspectorGUI();
		}
	}
}
