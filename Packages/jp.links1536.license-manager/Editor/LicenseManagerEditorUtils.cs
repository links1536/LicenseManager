using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;

namespace Links.Licenses
{
	static class LicenseManagerEditorUtils
	{
		public static string PropertyIndex(SerializedProperty property, string propertyName, int index)
		{
			using var element = property.FindPropertyRelative(propertyName);
			return PropertyIndex(element?.stringValue, index);
		}

		public static string PropertyIndex(string stringValue, int index)
			=> string.IsNullOrWhiteSpace(stringValue)
			? $"要素 {index}"
			: stringValue;
	}
}
