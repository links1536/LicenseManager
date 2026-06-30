using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace Links.Licenses
{
	public class LicenseView : MonoBehaviour
	{
		const string HorizontalLine		= "================================================";
		const string Separator			= "-------------------------";

		[SerializeField] ScrollRect m_ScrollView;
		[SerializeField] LicenseListButton m_ItemBase;

		ObjectPool<LicenseListButton> m_TextPool;
		List<LicenseListButton> m_ActiveList;

		public void Show()
		{
			// List初期化
			if (m_ActiveList == null)
				m_ActiveList = new List<LicenseListButton>();

			// ObjectPool初期化
			var parent = m_ScrollView.content;
			if (m_TextPool == null)
			{
				m_TextPool = new ObjectPool<LicenseListButton>(
					createFunc: () => Instantiate(m_ItemBase, parent),
					actionOnGet: text =>
					{
						text.gameObject.SetActive(true);
						text.transform.SetAsLastSibling();
					},
					actionOnRelease: text => text.gameObject.SetActive(false)
				);
			}

			m_ItemBase.gameObject.SetActive(false);

			// 前回使用分を回収
			foreach (var text in m_ActiveList)
				if (text != null)
					m_TextPool.Release(text);
			m_ActiveList.Clear();

			// テキスト作成
			var spdxList = GetSpdxLicenses();
			if (spdxList != null)
			{
				var dict = new ConcurrentDictionary<string, List<LicenseManifest.SpdxLicenseEntry>>();

				foreach (var entry in spdxList)
				{
					var list = dict.GetOrAdd(entry.SpdxIdentifier, key => new List<LicenseManifest.SpdxLicenseEntry>());
					list.Add(entry);
					list.Sort((x, y) => x.ComponentName.CompareTo(y.ComponentName));
				}

				var spdxTemplate = GetSpdxTemplates();

				foreach (var pair in dict.OrderBy(x => x.Key))
				{
					AddText(HorizontalLine, HorizontalLine);

					foreach (var entry in pair.Value)
					{
						AddText(entry.ComponentName, $"・{entry.ComponentName}");
						foreach (var copyright in entry.CopyrightList)
							AddText($"{entry.ComponentName} - {copyright}", copyright);
					}

					AddText(Separator, Separator);

					// ライセンス本文の表示
					if (spdxTemplate != null
					 && spdxTemplate.TryGetValue(pair.Key, out var template)
					 && template.Text != null
					 && !string.IsNullOrEmpty(template.Text.text))
					{
						AddText("LicenseText", template.Text.text);
					}
				}
			}

			// テキスト作成
			var licenseList = GetRawLicenses();
			if (licenseList != null)
			{
				foreach (var entry in licenseList)
				{
					AddText(HorizontalLine, HorizontalLine);
					AddText(entry.ComponentName, entry.ComponentName);
					AddText("LicenseText", entry.LicenseText);
				}
			}

			// スクロールリセット
			m_ScrollView.verticalNormalizedPosition = 1;
		}

		void AddText(string name, string text)
		{
			var lineObject = m_TextPool.Get();
			lineObject.name = name;
			lineObject.Setup(text);
			m_ActiveList.Add(lineObject);
		}

		Dictionary<string, LicenseManifest.SpdxTemplateEntry>? GetSpdxTemplates()
		{
			if (!LicenseManifest.TryGetInstance(out var instance))
				return default;

			var dict = new Dictionary<string, LicenseManifest.SpdxTemplateEntry>(instance.SpdxTemplateList.Count);
			foreach(var entry in instance.SpdxTemplateList)
				dict[entry.SpdxIdentifier] = entry;
			return dict;
		}

		List<LicenseManifest.SpdxLicenseEntry>? GetSpdxLicenses()
		{
			if (!LicenseManifest.TryGetInstance(out var instance))
				return default;

			return instance.SpdxLicenseList;
		}

		List<LicenseManifest.RawLicenseEntry>? GetRawLicenses()
		{
			if (!LicenseManifest.TryGetInstance(out var instance))
				return default;

			return instance.RawEntryList;
		}

	}
}
