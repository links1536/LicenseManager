namespace Links.Licenses.Parser
{
	static class ParserUtils
	{
		static char[] Lines = new char[] { '\r', '\n' };

		public static string TrimLines(string text)
			=> !string.IsNullOrWhiteSpace(text)
			? text.TrimStart(Lines).TrimEnd(Lines)
			: text;
	}
}
