using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lab
{
    public class SymbolTable
    {
		public List<Scope> scopes;
		public SymbolTable()
		{
			this.scopes = new List<Scope>();
			this.scopes.Add(new Scope());   //global
		}
		public void addScope()
		{
			this.scopes.Add(new Scope());
		}
		public void removeScope()
		{
			this.scopes.RemoveAt(this.scopes.Count-1);
		}
		public void declareVariable(string name, VarInfo info, TreeNode node)
		{
			this.scopes[^1].declareVariable(name, info, node);
		}
		public void ICE()
		{
			throw new Exception();
		}
		public VarInfo lookup(TreeNode n)
		{
			if (n.sym != "ID")
				ICE();
			string name = n.token.lexeme;
			for (int i = this.scopes.Count - 1; i >= 0; i--)
			{
				if (this.scopes[i].vars.ContainsKey(name))
					return this.scopes[i].vars[name];
			}
			//Some error function you've defined somewhere...
			TreeNode.error($"No such variable {name} at line {n.token.line}, column {n.token.col}", n);
			return null;
		}

	}
}
