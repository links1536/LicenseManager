using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Links.Licenses.Settings
{
	sealed class PackageSettingsEditor : Editor
	{
		struct CollectData
		{
			public string Name;
			public bool IsInRuntime;
			public SerializedProperty Property;
		}

		string m_Title;
		string m_Description;
		SourceType m_SourceType;
		SerializedProperty m_EntriesProperty;
		
		public static string GetPropertyString(SerializedProperty property, string propertyName)
		{
			using var element = property.FindPropertyRelative(propertyName);
			return element?.stringValue;
		}

		public static bool? GetPropertyBool(SerializedProperty property, string propertyName)
		{
			using var element = property.FindPropertyRelative(propertyName);
			return element?.boolValue;
		}

		public static UnityEngine.Object GetPropertyObjectReference(SerializedProperty property, string propertyName)
		{
			using var element = property.FindPropertyRelative(propertyName);
			return element?.objectReferenceValue;
		}

		public static string PropertyIndex(SerializedProperty property, string propertyName, int index)
		{
			using var element = property.FindPropertyRelative(propertyName);
			return PropertyIndex(element?.stringValue, index);
		}

		public static string PropertyIndex(string stringValue, int index)
			=> string.IsNullOrWhiteSpace(stringValue)
			? $"要素 {index}"
			: stringValue;

		public void Configure(string title, string description, SourceType sourceType)
		{
			m_Title = title;
			m_Description = description;
			m_SourceType = sourceType;
			BindProperties();
		}

		void OnEnable()
			=> BindProperties();

		void BindProperties()
		{
			if (serializedObject == null)
				return;

			switch (m_SourceType)
			{
				case SourceType.UnmanagedAssets:
					m_EntriesProperty = serializedObject.FindProperty("m_UnmanagedAssets");
					break;
				case SourceType.UnityPackages:
					m_EntriesProperty = serializedObject.FindProperty("m_UnityPackages");
					break;
				case SourceType.NuGet:
					m_EntriesProperty = serializedObject.FindProperty("m_NuGetPackages");
					break;
			}
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			var settings = (LicenseSettings)target;
			EditorGUILayout.HelpBox($"{m_Title}\n{m_Description}", MessageType.None);

			if (m_SourceType == SourceType.UnmanagedAssets)
				EditorGUILayout.PropertyField(m_EntriesProperty);
			else
				OnInspectorPackageGUI(settings);

			serializedObject.ApplyModifiedProperties();
		}

		void OnInspectorPackageGUI(LicenseSettings settings)
		{
			serializedObject.Update();

			// パッケージ情報読み込みボタン
			if (GUILayout.Button($"{m_Title} を再読み込み"))
			{
				serializedObject.ApplyModifiedProperties();
				Undo.RecordObject(settings, $"パッケージ情報更新 ({m_SourceType})");
				LicenseSettingsEditor.RefreshSourceEntries(settings, m_SourceType);
				settings.Save();
				serializedObject.Update();
			}

			// パッケージの種類情報
			EditorGUILayout.Space();
			EditorGUILayout.LabelField(m_Title, EditorStyles.boldLabel);

			var validEntryList = CollectEntries(m_EntriesProperty, false);
			var invalidEntryList = CollectEntries(m_EntriesProperty, true);

			// パッケージを列挙
			DrawEntries(validEntryList, false);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField($"{m_Title} の不足項目", EditorStyles.boldLabel);

			// ライセンス情報の不足したパッケージを列挙
			DrawEntries(invalidEntryList, true);
		}

		List<CollectData> CollectEntries(SerializedProperty entriesProperty, bool findMissing)
		{
			// パッケージ情報をまとめる
			var list = new List<CollectData>();
			for (var i = 0; i < entriesProperty.arraySize; i++)
			{
				var element = entriesProperty.GetArrayElementAtIndex(i);

				
				bool isMissingLicense = string.IsNullOrEmpty(GetPropertyString(element, nameof(LicenseSettings.Entry.LicenseType)))
					&& GetPropertyObjectReference(element, nameof(LicenseSettings.Entry.LicenseFile)) == null;
				if (isMissingLicense != findMissing)
					continue;

				list.Add(new CollectData()
				{
					Name = GetPropertyString(element, nameof(LicenseSettings.Entry.Id))
						?? GetPropertyString(element, nameof(LicenseSettings.Entry.Name)),
					IsInRuntime = GetPropertyBool(element, nameof(LicenseSettings.Entry.IsInRuntime)) ?? false,
					Property = element,
				});
			}

			list.Sort((x, y) => x.Name.CompareTo(y.Name));

			return list;
		}

		void DrawEntries(List<CollectData> entryList, bool drawMissing)
		{
			if (entryList == null)
				return;

			using (new EditorGUI.DisabledScope(true))
			{
				EditorGUILayout.IntField("件数", entryList.Count);
			}

			if (entryList.Count == 0)
			{
				EditorGUILayout.HelpBox("パッケージが見つかりませんでした", MessageType.Info);
				return;
			}

			// パッケージ一覧を表示
			using (new EditorGUI.IndentLevelScope())
			{
				for (var i = 0; i < entryList.Count; i++)
				{
					string name = entryList[i].Name;
					if (!entryList[i].IsInRuntime)
						name += " (Ignore Runtime)";
					DrawEntry(name, entryList[i].Property, i);
				}
			}
		}

		void DrawEntry(string name, SerializedProperty element, int index)
		{
			element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, name, true);
			if (!element.isExpanded)
				return;

			var childProperty =
			(
				Id: element.FindPropertyRelative(nameof(LicenseSettings.Entry.Id)),
				Name: element.FindPropertyRelative(nameof(LicenseSettings.Entry.Name)),
				CopyrightList: element.FindPropertyRelative(nameof(LicenseSettings.Entry.CopyrightList)),
				PackagePath: element.FindPropertyRelative(nameof(LicenseSettings.Entry.PackagePath)),
				LicenseType: element.FindPropertyRelative(nameof(LicenseSettings.Entry.LicenseType)),
				LicenseFile: element.FindPropertyRelative(nameof(LicenseSettings.Entry.LicenseFile)),
				ThirdPartyNoticesFile: element.FindPropertyRelative(nameof(LicenseSettings.Entry.ThirdPartyNoticesFile)),
				LocalPackages: element.FindPropertyRelative(nameof(LicenseSettings.Entry.IsLocalPackages)),
				IsInRuntime: element.FindPropertyRelative(nameof(LicenseSettings.Entry.IsInRuntime))
			);

			// パッケージに埋め込まれたライセンスなら編集不可にする
			string packagePath = childProperty.PackagePath.stringValue.TrimEnd('/') + "/";
			string licenseType = childProperty.LicenseType?.stringValue;
			string licenseFilePath = childProperty.LicenseFile != null && childProperty.LicenseFile.objectReferenceValue != null
				 ? AssetDatabase.GetAssetPath(childProperty.LicenseFile.objectReferenceValue)
				 : string.Empty;
			string noticesFilePath = childProperty.ThirdPartyNoticesFile != null && childProperty.ThirdPartyNoticesFile.objectReferenceValue != null
				 ? AssetDatabase.GetAssetPath(childProperty.ThirdPartyNoticesFile.objectReferenceValue)
				 : string.Empty;

			// パッケージに埋め込まれていたら編集不可にする
			bool isEmbeddLicense = !string.IsNullOrEmpty(licenseType) || licenseFilePath.StartsWith(packagePath);
			bool isEmbeddNotices = noticesFilePath.StartsWith(packagePath);

			using (new EditorGUI.IndentLevelScope())
			{
				using (new EditorGUI.DisabledScope(true))
				{
					EditorGUILayout.PropertyField(childProperty.Id, true);
					EditorGUILayout.PropertyField(childProperty.Name, true);
					EditorGUILayout.PropertyField(childProperty.CopyrightList, true);
					EditorGUILayout.PropertyField(childProperty.LicenseType, true);
				}

				using (new EditorGUI.DisabledScope(isEmbeddLicense))
					EditorGUILayout.PropertyField(childProperty.LicenseFile, true);
				using (new EditorGUI.DisabledScope(isEmbeddNotices))
					EditorGUILayout.PropertyField(childProperty.ThirdPartyNoticesFile, true);

				using (new EditorGUI.DisabledScope(true))
				{
					EditorGUILayout.PropertyField(childProperty.LocalPackages, true);
					EditorGUILayout.PropertyField(childProperty.IsInRuntime, true);
				}
			}
		}
	}
}
