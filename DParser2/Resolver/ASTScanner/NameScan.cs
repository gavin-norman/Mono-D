﻿using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Resolver.TypeResolution;
using D_Parser.Parser;

namespace D_Parser.Resolver.ASTScanner
{
	public class NameScan : AbstractVisitor
	{
		string filterId;
		public List<INode> Matches = new List<INode>();

		NameScan(ResolverContextStack ctxt) : base(ctxt) { }

		public static IEnumerable<INode> SearchMatchesAlongNodeHierarchy(ResolverContextStack ctxt, CodeLocation caret, string name)
		{
			var scan = new NameScan(ctxt) { filterId=name };

			scan.IterateThroughScopeLayers(caret);

			return scan.Matches;
		}

		public override IEnumerable<INode> PrefilterSubnodes(IBlockNode bn)
		{
			return bn.Children[filterId];
		}

		protected override bool HandleItem(INode n)
		{
            if (n != null && n.Name == filterId)
            {
                Matches.Add(n);

                if (Context.Options.HasFlag(ResolutionOptions.StopAfterFirstMatch))
                    return true;
            }

            /*
             * Can't tell if workaround .. or just nice idea:
             * 
             * To still be able to show sub-packages e.g. when std. has been typed,
             * take the first import that begins with std.
             * In HandleNodeMatch, it'll be converted to a module package result then.
             */
            else if (n is IAbstractSyntaxTree)
            {
                var modName = ((IAbstractSyntaxTree)n).ModuleName;
                if (modName.Split('.')[0] == filterId)
                {
                    bool canAdd = true;

                    foreach (var m in Matches)
                        if (m is IAbstractSyntaxTree)
                        {
                            canAdd = false;
                            break;
                        }

                    if (canAdd)
                    {
                        Matches.Add(n);

                        if (Context.Options.HasFlag(ResolutionOptions.StopAfterFirstMatch))
                            return true;
                    }
                }
            }

            return false;
		}

		static int __stack = 0;
		/// <summary>
		/// Scans through the node. Also checks if n is a DClassLike or an other kind of type node and checks their specific child and/or base class nodes.
		/// </summary>
		/// <param name="parseCache">Needed when trying to search base classes</param>
		public static INode[] ScanNodeForIdentifier(IBlockNode curScope, string name, ResolverContextStack ctxt)
		{
			if (__stack > 40)
				return null;

			__stack++;

			var matches = new List<INode>();

			// Watch for anonymous enums
			var children = curScope[string.Empty];

			if(children!=null)
				foreach(var n in children)
					if (n is DEnum)
					{
						var de = (DEnum)n;

						var enumChildren = de[name];

						if (enumChildren != null)
							matches.AddRange(enumChildren);
					}

			// Scan for normal members called 'name'
			if ((children = curScope[name]) != null)
				matches.AddRange(children);

			// If our current Level node is a class-like, also attempt to search in its baseclass!
			if (curScope is DClassLike)
			{
				var dc = (DClassLike)curScope;

				if (dc.ClassType == DTokens.Class)
				{
					var tr=DResolver.ResolveBaseClasses(new ClassType(dc, dc, null), ctxt, true);
					var tit = tr.Base as TemplateIntermediateType;
					if (tit!=null && tit.Definition != dc)
						{
							// Search for items called name in the base class(es)
							var r = ScanNodeForIdentifier(tit.Definition, name, ctxt);

							if (r != null)
								matches.AddRange(r);
						}
				}
				else if (dc.ClassType == DTokens.Interface)
				{
					var tr = DResolver.ResolveBaseClasses(new InterfaceType(dc, dc), ctxt) as InterfaceType;
					if (tr != null && tr.BaseInterfaces != null && tr.BaseInterfaces.Length != 0)
						foreach (var I in tr.BaseInterfaces)
						{
							if (I.Definition == dc)
								break;

							// Search for items called name in the base class(es)
							var r = ScanNodeForIdentifier(I.Definition, name, ctxt);

							if (r != null)
								matches.AddRange(r);
						}
				}
			}

			// Check parameters
			var dn = curScope as DNode;

			if (dn != null)
			{
				var dm = dn as DMethod;
				if (dm != null && dm.Parameters != null && dm.Parameters.Count != 0)
					foreach (var ch in ((DMethod)curScope).Parameters)
						if (name == ch.Name)
							matches.Add(ch);

				// and template parameters
				if (dn.TemplateParameters != null)
					foreach (var ch in dn.TemplateParameters)
						if (name == ch.Name)
							matches.Add(new TemplateParameterNode(ch) { Parent = curScope });
			}

			__stack--;

			return matches.Count > 0 ? matches.ToArray() : null;
		}
	}
}
