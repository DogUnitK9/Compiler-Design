using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lab
{
    public class Scope
    {
		public Dictionary<string, VarInfo> vars = new Dictionary<string, VarInfo>();
		public Scope()
		{
			this.vars = new Dictionary<string, VarInfo>();
		}
		public void declareVariable(string name, VarInfo info, TreeNode node)
		{
			if (this.vars.ContainsKey(name))
				TreeNode.error($"Duplicate variable declaration: {name} at {info.decl.line}; " +
				$"previously declared at {this.vars[name].decl.line}", node);
			this.vars[name] = info;
		}
	}
}
