﻿using System.IO;
using MonoDevelop.Ide.TypeSystem;

namespace MonoDevelop.D.Parser
{
	public class FoldingParser : IFoldingParser
	{
		public ParsedDocument Parse(string fileName, string content)
		{
			return DParserWrapper.LastParsedMod = DParserWrapper.Instance.Parse(true, fileName, new StringReader(content));
		}
	}
}
