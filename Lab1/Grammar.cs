using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System;

namespace lab
{

    public class Grammar
    {
        public List<Terminal> terminals = new List<Terminal>();
        public Dictionary<string, List<Production>> nonterminals = new Dictionary<string, List<Production>>();

        public Grammar(string data)
        {
            //handle line continuation: Any line that begins with
            //at least four spaces is combined with the previous line
            data = data.Replace("\n    ", " ");

            bool foundWhitespace = false;

            //process each line one at a time
            var L = new List<string>();
            foreach (var line in data.Split('\n'))
            {

                //ignore empty line
                if (line.Length == 0)
                    continue;

                //ignore comment line
                if (line.StartsWith("#"))
                    continue;

                //split line into two parts at the double colon
                int i = line.IndexOf("::");
                if (i == -1)
                    throw new Exception("Line is missing ::  ->  " + line);

                //left and right hand sides
                string lhs = line.Substring(0, i).Trim();
                string rhs = line.Substring(i + 2).Trim();

                if (lhs == "S" || lhs.ToUpper() != lhs)
                {
                    if (!this.nonterminals.ContainsKey(lhs))
                        this.nonterminals[lhs] = new List<Production>();
                    foreach (string prodstr in rhs.Split('|'))
                    {
                        string tmp = prodstr.Trim();
                        Production P;
                        if (tmp == "lambda")
                            P = new Production(lhs, new string[0]);
                        else
                            P = new Production(lhs, tmp.Split(" ", StringSplitOptions.RemoveEmptyEntries));
                        this.nonterminals[lhs].Add(P);
                    }
                }
                else
                {
                    this.terminals.Add(new Terminal(lhs, new Regex("\\G(" + rhs + ")")));
                }

                if (lhs == "WHITESPACE")
                    foundWhitespace = true;
            }

            //if we didn't have a WHITESPACE specifier in the grammar,
            //make one now
            if (!foundWhitespace)
            {
                this.terminals.Add(new Terminal("WHITESPACE", new Regex("\\G\\s+")));
            }
        }

        public HashSet<string> computeNullable()
        {
            var nullable = new HashSet<string>();
            bool keeplooping = true;
            while (keeplooping)
            {
                keeplooping = false;
                foreach (string n in this.nonterminals.Keys)
                {
                    if (nullable.Contains(n))
                        continue;
                    foreach (Production p in this.nonterminals[n])
                    {
                        if (p.rhs.All(sym => nullable.Contains(sym)))
                        {
                            nullable.Add(n);
                            keeplooping = true;
                            break;
                        }
                    }
                }
            }
            return nullable;
        }

        public Dictionary<string, HashSet<string>> computeFirst(HashSet<string> nullable)
        {
            var first = new Dictionary<string, HashSet<string>>();
            foreach (Terminal t in this.terminals)
            {
                first[t.sym] = new HashSet<string>();
                first[t.sym].Add(t.sym);
            }
            foreach (string n in this.nonterminals.Keys)
            {
                first[n] = new HashSet<string>();
            }
            bool keeplooping = true;
            while (keeplooping)
            {
                keeplooping = false;
                foreach (string n in this.nonterminals.Keys)
                {
                    foreach (Production p in this.nonterminals[n])
                    {
                        foreach (string sym in p.rhs)
                        {
                            int c1 = first[n].Count;
                            first[n].UnionWith(first[sym]);
                            int c2 = first[n].Count;
                            if (c1 != c2)
                                keeplooping = true;
                            if (!nullable.Contains(sym))
                                break;
                        }
                    }
                }
            }
            return first;
        }


        public Dictionary<string, HashSet<string>> computeFollow(HashSet<string> nullable, Dictionary<string, HashSet<string>> first)
        {
            var follow = new Dictionary<string, HashSet<string>>();
            foreach (string n in this.nonterminals.Keys)
            {
                follow[n] = new HashSet<string>();
            }
            follow["S"].Add("$");
            bool keeplooping = true;
            while (keeplooping)
            {
                keeplooping = false;
                foreach (string n in this.nonterminals.Keys)
                {
                    foreach (Production p in this.nonterminals[n])
                    {
                        for (int i = 0; i < p.rhs.Length; ++i)
                        {
                            string sym1 = p.rhs[i];
                            if (!this.nonterminals.ContainsKey(sym1))
                                continue;
                            int j;
                            for (j = i + 1; j < p.rhs.Length; ++j)
                            {
                                int c1 = follow[sym1].Count;
                                follow[sym1].UnionWith(first[p.rhs[j]]);
                                if (follow[sym1].Count != c1)
                                    keeplooping = true;
                                if (!nullable.Contains(p.rhs[j]))
                                    break;
                            }
                            if (j == p.rhs.Length)
                            {
                                int c1 = follow[sym1].Count;
                                follow[sym1].UnionWith(follow[p.lhs]);
                                if (follow[sym1].Count != c1)
                                    keeplooping = true;
                            }
                        }
                    }
                }
            }
            return follow;
        }
    }

}   //namespace
