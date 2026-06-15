using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Links.Licenses
{
	sealed class PackageSettingsEditor : Editor
	{
		string m_Title;
		string m_Description;
		SourceType m_SourceType;
		SerializedProperty m_EntriesProperty;
		SerializedProperty m_MissingEntriesProperty;

		readonly Dictionary<string, Vector2> m_LicenseTextScrollPositions = new Dictionary<string, Vector2>();

		static string GetCollectedEntryLabel(SerializedProperty element, int index)
			=> LicenseManagerEditorUtils.PropertyIndex(element, "Name", index);

		static string GetCollectedEntryGroupLabel(string label)
		{
			var separatorIndex = label.IndexOf(" / ", StringComparison.Ordinal);
			return separatorIndex >= 0
				? label.Substring(0, separatorIndex)
				: label;
		}

		static string GetCollectedEntryChildLabel(string label)
		{
			var separatorIndex = label.IndexOf(" / ", StringComparison.Ordinal);
			return separatorIndex >= 0
				? label.Substring(separatorIndex + 3)
				: label;
		}

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
				case SourceType.UnityPackages:
					m_EntriesProperty = serializedObject.FindProperty("m_UnityPackageEntries");
					m_MissingEntriesProperty = serializedObject.FindProperty("m_MissingUnityPackageEntries");
					break;
				case SourceType.NuGet:
					m_EntriesProperty = serializedObject.FindProperty("m_NuGetEntries");
					m_MissingEntriesProperty = serializedObject.FindProperty("m_MissingNuGetEntries");
					break;
			}
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			var settings = (ThirdPartyNoticesSettings)target;
			EditorGUILayout.HelpBox($"{m_Title}\n{m_Description}", MessageType.None);

			// パッケージ情報読み込みボタン
			if (GUILayout.Button($"{m_Title} を再読み込み"))
			{
				serializedObject.ApplyModifiedProperties();
				EditorUtility.SetDirty(settings);
				AssetDatabase.SaveAssets();
				ThirdPartyNoticesSettingsEditor.RefreshSourceEntries(settings, m_SourceType);
				serializedObject.Update();
			}

			// パッケージの種類情報
			EditorGUILayout.Space();
			EditorGUILayout.LabelField(m_Title, EditorStyles.boldLabel);

			// パッケージを列挙
			DrawCollectedEntries(m_EntriesProperty);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField($"{m_Title} の不足項目", EditorStyles.boldLabel);

			// ライセンス情報の不足したパッケージを列挙
			DrawMissingEntriesEditor(m_MissingEntriesProperty);

			serializedObject.ApplyModifiedProperties();
		}

		void DrawCollectedEntries(SerializedProperty entriesProperty)
		{
			if (entriesProperty == null)
				return;

			using (new EditorGUI.DisabledScope(true))
			{
				EditorGUILayout.IntField("件数", entriesProperty.arraySize);
			}

			if (entriesProperty.arraySize == 0)
			{
				EditorGUILayout.HelpBox("パッケージが見つかりませんでした", MessageType.Info);
				return;
			}

			// パッケージ情報をまとめる
			var groupStartIndices = new List<int>();
			var groupLabels = new List<string>();
			var groupExpanded = new List<bool>();
			for (var i = 0; i < entriesProperty.arraySize; i++)
			{
				var element = entriesProperty.GetArrayElementAtIndex(i);
				var fullLabel = LicenseManagerEditorUtils.PropertyIndex(element, "Name", i);
				var groupLabel = GetCollectedEntryGroupLabel(fullLabel);
				if (groupLabels.Count == 0 || !string.Equals(groupLabels[groupLabels.Count - 1], groupLabel, StringComparison.Ordinal))
				{
					groupStartIndices.Add(i);
					groupLabels.Add(groupLabel);
					groupExpanded.Add(element.isExpanded);
				}
			}

			// パッケージ一覧を表示
			using (new EditorGUI.IndentLevelScope())
			{
				for (var groupIndex = 0; groupIndex < groupLabels.Count; groupIndex++)
				{
					int nextIndex = groupIndex + 1;
					var startIndex = groupStartIndices[groupIndex];
					var endIndex = nextIndex < groupStartIndices.Count
						? groupStartIndices[nextIndex]
						: entriesProperty.arraySize;
					var firstElement = entriesProperty.GetArrayElementAtIndex(startIndex);
					var isSingleEntryGroup = (endIndex - startIndex) == 1;
					var firstEntryLabel = GetCollectedEntryLabel(firstElement, startIndex);
					if (isSingleEntryGroup && string.Equals(groupLabels[groupIndex], firstEntryLabel, StringComparison.Ordinal))
					{
						DrawCollectedEntry(firstElement, startIndex, firstEntryLabel);
						continue;
					}

					var expanded = EditorGUILayout.Foldout(groupExpanded[groupIndex], groupLabels[groupIndex], true);
					groupExpanded[groupIndex] = expanded;
					firstElement.isExpanded = expanded;
					if (!expanded)
						continue;

					using (new EditorGUI.IndentLevelScope())
					{
						for (var i = startIndex; i < endIndex; i++)
						{
							var element = entriesProperty.GetArrayElementAtIndex(i);
							DrawCollectedEntry(element, i, GetCollectedEntryChildLabel(GetCollectedEntryLabel(element, i)));
						}
					}
				}
			}
		}

		void DrawCollectedEntry(SerializedProperty element, int index, string label)
		{
			var nameProperty = element.FindPropertyRelative("Name");
			var licenseFileProperty = element.FindPropertyRelative("LicenseFile");
			var thirdPartyNoticesFileProperty = element.FindPropertyRelative("ThirdPartyNoticesFile");
			var licenseTextProperty = element.FindPropertyRelative("LicenseText");
			var fullLabel = GetCollectedEntryLabel(element, index);

			element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, label, true);
			if (!element.isExpanded)
				return;

			using (new EditorGUI.IndentLevelScope())
			{
				using (new EditorGUI.DisabledScope(true))
				{
					EditorGUILayout.PropertyField(nameProperty);
					EditorGUILayout.PropertyField(licenseFileProperty, new GUIContent("LICENSE"));
					EditorGUILayout.PropertyField(thirdPartyNoticesFileProperty, new GUIContent("THIRD PARTY NOTICES"));
				}

				var licenseText = licenseTextProperty?.stringValue;
				if (!string.IsNullOrWhiteSpace(licenseText))
				{
					EditorGUILayout.LabelField("ライセンステキスト");
					DrawScrollablePreview(fullLabel, licenseText);
				}
			}
		}

		void DrawScrollablePreview(string key, string text)
		{
			if (!m_LicenseTextScrollPositions.TryGetValue(key, out var scrollPosition))
				scrollPosition = Vector2.zero;

			using (var scrollView = new EditorGUILayout.ScrollViewScope(scrollPosition, GUILayout.Height(140f)))
			{
				using (new EditorGUI.DisabledScope(true))
					EditorGUILayout.TextArea(text, GUILayout.ExpandHeight(true));
				m_LicenseTextScrollPositions[key] = scrollView.scrollPosition;
			}
		}

		/// <summary>
		/// ライセンス情報の不足しているパッケージを表示
		/// </summary>
		static void DrawMissingEntriesEditor(SerializedProperty missingEntriesProperty)
		{
			if (missingEntriesProperty == null)
				return;

			using (new EditorGUI.DisabledScope(true))
			{
				EditorGUILayout.IntField("件数", missingEntriesProperty.arraySize);
			}

			if (missingEntriesProperty.arraySize == 0)
			{
				EditorGUILayout.HelpBox("ライセンス情報の不足しているパッケージは見つかりませんでした", MessageType.Info);
				return;
			}

			using (new EditorGUI.IndentLevelScope())
			{
				for (var i = 0; i < missingEntriesProperty.arraySize; i++)
				{
					var element = missingEntriesProperty.GetArrayElementAtIndex(i);
					var nameProperty = element.FindPropertyRelative("Name");
					var packagePathProperty = element.FindPropertyRelative("PackagePath");
					var reasonProperty = element.FindPropertyRelative("Reason");
					var licenseFileProperty = element.FindPropertyRelative("LicenseFile");
					var thirdPartyNoticesFileProperty = element.FindPropertyRelative("ThirdPartyNoticesFile");
					var label = LicenseManagerEditorUtils.PropertyIndex(nameProperty?.stringValue, i);

					element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, label, true);
					if (!element.isExpanded)
						continue;

					using (new EditorGUI.IndentLevelScope())
					{
						using (new EditorGUI.DisabledScope(true))
						{
							EditorGUILayout.PropertyField(nameProperty);
							EditorGUILayout.PropertyField(packagePathProperty);
							EditorGUILayout.PropertyField(reasonProperty);
						}

						// ライセンスの上書き用の項目のみ編集可能
						EditorGUILayout.PropertyField(licenseFileProperty, new GUIContent("LICENSE"));
						EditorGUILayout.PropertyField(thirdPartyNoticesFileProperty, new GUIContent("THIRD PARTY NOTICES"));
					}
				}
			}
		}
	}
}
