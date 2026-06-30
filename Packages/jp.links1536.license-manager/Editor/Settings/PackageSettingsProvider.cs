using UnityEditor;
using UnityEngine.UIElements;

namespace Links.Licenses.Settings
{
	class PackageSettingsProvider : SettingsProvider
	{
		readonly string m_Title;
		readonly string m_Description;
		readonly SourceType m_SourceType;
		Editor m_Editor;

		public PackageSettingsProvider(string path, string title, string description, SourceType sourceType)
			: base(path, SettingsScope.Project)
		{
			m_Title = title;
			m_Description = description;
			m_SourceType = sourceType;
			label = title;
		}

		public override void OnActivate(string searchContext, VisualElement rootElement)
		{
			var instance = LicenseSettings.instance;
			Editor.CreateCachedEditor(instance, typeof(PackageSettingsEditor), ref m_Editor);
			if (m_Editor is PackageSettingsEditor sourceEditor)
				sourceEditor.Configure(m_Title, m_Description, m_SourceType);
		}

		public override void OnGUI(string searchContext)
		{
			var instance = LicenseSettings.instance;
			if (m_Editor == null || m_Editor.target != instance)
				Editor.CreateCachedEditor(instance, typeof(PackageSettingsEditor), ref m_Editor);

			if (m_Editor is PackageSettingsEditor sourceEditor)
				sourceEditor.Configure(m_Title, m_Description, m_SourceType);

			m_Editor.OnInspectorGUI();
		}
	}
}
