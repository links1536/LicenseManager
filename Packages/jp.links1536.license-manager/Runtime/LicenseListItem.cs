using UnityEngine;
using UnityEngine.Events;

namespace Links.Licenses
{
	public class LicenseListButton : MonoBehaviour
	{
		[SerializeField] UnityEvent<string> m_Text;

		public void Setup(string text)
		{
			if (m_Text != null)
				m_Text.Invoke(text);
		}
	}
}
