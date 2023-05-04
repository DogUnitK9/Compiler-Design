using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lab
{
    public class Walker
    {

        public delegate void WalkFunc(TreeNode n);
        public Dictionary<string, WalkFunc> preCallbacks = new Dictionary<string, WalkFunc>();
        public Dictionary<string, Dictionary<string, WalkFunc>> inCallbacks = new Dictionary<string, Dictionary<string, WalkFunc>>();
        public Dictionary<string, WalkFunc> postCallbacks = new Dictionary<string, WalkFunc>();
        public static void walkPre(TreeNode n, WalkFunc pre)
        {
            pre(n);
            foreach (var c in n.children)
                walkPre(c, pre);
        }
        public static void walkPost(TreeNode n, WalkFunc post)
        {
            foreach (var c in n.children)
                walkPost(c, post);
            post(n);
        }
        public static void walkIn(TreeNode n, string sym, WalkFunc func)
        {
            foreach (var c in n.children)
            {
                if (c.sym == sym)
                    func(n);
                walkIn(n, sym, func);
            }
        }
        public void registerIn(string production, string sym, WalkFunc f)
        {
            production = normalize(production);
            if (!this.inCallbacks.ContainsKey(production))
                this.inCallbacks[production] = new Dictionary<string, WalkFunc>();
            if (this.inCallbacks[production].ContainsKey(sym))
                throw new Exception();
            this.inCallbacks[production][sym] = f;
        }

        static string normalize(string tmp)
        {
            int idx = tmp.IndexOf("::");
            if (idx == -1)
                throw new Exception("No '::': " + tmp);
            string lhs = tmp.Substring(0, idx).Trim();
            var rhs = tmp.Substring(idx + 2).Trim().Split(' ',
                            StringSplitOptions.RemoveEmptyEntries);
            return lhs + " :: " + string.Join(" ", rhs);
        }

        public void registerIn((string production, string sym)[] patterns,
                                 WalkFunc f)
        {
            foreach ((string production, string sym) pattern in patterns)
            {
                string production = pattern.production;
                string sym = pattern.sym;
                this.registerIn(production, sym, f);
            }
        }
        private static void registerHelper(string[] productions, WalkFunc f, Dictionary<string, WalkFunc> D)
        {
            foreach (string p in productions)
            {
                var pattern = normalize(p);
                if (D.ContainsKey(pattern))
                    throw new Exception();
                D[pattern] = f;
            }
        }
        public void registerPre(string production, WalkFunc f)
        {
            registerPre(new[] { production }, f);
        }

        public void registerPre(string[] productions, WalkFunc f)
        {
            registerHelper(productions, f, this.preCallbacks);
        }

        public void registerPost(string production, WalkFunc f)
        {
            registerPost(new[] { production }, f);
        }

        public void registerPost(string[] productions, WalkFunc f)
        {
            registerHelper(productions, f, this.postCallbacks);
        }
        //Limitation: Inorder callbacks can't distinguish between
        //two children with identical symbols.
        public void walk(TreeNode n)
        {
            //see if there are any preorder callbacks for this node
            if (this.preCallbacks.ContainsKey(n.production))
                this.preCallbacks[n.production](n);

            //walk children and do inorder walks as well
            Dictionary<string, WalkFunc>? D = null;
            if (this.inCallbacks.ContainsKey(n.production))
                D = this.inCallbacks[n.production];
            foreach (var c in n.children)
            {
                //if next child matches dictionary key, do callback.
                if (D != null && D.ContainsKey(c.sym))
                {
                    D[c.sym](n);
                }
                walk(c);
            }

            //handle any postorder callbacks
            if (this.postCallbacks.ContainsKey(n.production))
                this.postCallbacks[n.production](n);
        }

    }
}
