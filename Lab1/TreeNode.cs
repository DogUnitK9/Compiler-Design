using System;
using System.Collections.Generic;

namespace lab
{ 
    public class TreeNode
    {
        public string sym;
        public Token token;
        public string production;
        public VarType type = null;
        public Dictionary<string, dynamic> attribs = new Dictionary<string, dynamic>();
        public List<TreeNode> children = new List<TreeNode>();
        private ASM code_ = null;
        public ASM code
        {
            get
            {
                if (this.code_ != null)
                    return this.code_;
                else
                {
                    ASM code = new ASM();
                    foreach (var c in this.children)
                        code = code + c.code;
                    return code;
                }
            }
            set
            {
                this.code_ = value;
            }
        }
        public TreeNode(string sym, Token tok){
            this.sym=sym;
            this.token=tok;
        }
        public TreeNode this[string childSym]
        {
            get
            {
                foreach (var c in this.children)
                {
                    if (c.sym == childSym)
                        return c;
                }
                throw new Exception("No such child");
            }
        }
        public static void assertType(TreeNode toCheck,
                              params VarType[] allowed)
        {
            foreach (var t in allowed)
            {
                if (toCheck.type == t)
                    return;
            }
            error($"Bad type {toCheck.type}", toCheck);
        }

        public static void assertEqualTypes(TreeNode n1, TreeNode n2)
        {
            if (n1.type != n2.type)
                error($"Unequal types: {n1.type} and {n2.type}", n1);
        }
    

        public static void assertEqualTypes(TreeNode n1, VarType t1, VarType t2)
        {
            if (t1 != t2)
                error($"Unequal types: {t1} and {t2}", n1);
        }
        public static void error(string msg, TreeNode node)
        {
            Console.Error.WriteLine($"Compile error: At {getLocation(node)}: {msg}");
            System.Environment.Exit(1);
        }
        public static string getLocation(TreeNode n)
        {
            if (n.token != null)
                return "line " + n.token.line;
            foreach (var c in n.children)
            {
                var tmp = getLocation(c);
                if (tmp != null)
                    return tmp;
            }
            return null;
        }
    }
    
}