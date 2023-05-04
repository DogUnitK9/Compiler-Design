using System.Collections.Generic;
using System;

namespace lab{


public class SLRException : Exception{
    public SLRException(string reason): base(reason) {}
}

class HashSetKeyThing : IEqualityComparer<HashSet<NFAState>> {

    public bool Equals(HashSet<NFAState> h1, HashSet<NFAState> h2){
        return h1.SetEquals(h2);
    }
    public int GetHashCode(HashSet<NFAState> h){
        //only requirement: If h1 equals h2,
        //hashCode(h1) == hashCode(h2)
        int total=0;
        foreach(NFAState q in h)
            total = leftRotate(total) ^ q.GetHashCode();
        return total;
    }
    int leftRotate(int x){
        int topBit = x>>31;
        x <<= 1;
        x |= topBit;
        return x;
    }
}

public class SLR1Parser{

    public List<NFAState> nfa = new List<NFAState>();
    public List<DFAState> dfa = new List<DFAState>();

    private readonly Grammar G;
    private readonly Dictionary<string,HashSet<string>> follow;
    
    private void verbose(params Object[] obj)
    {
        return;
/*
        bool flag=false;
        foreach(var ob in obj){
            if(flag)
                System.Console.Error.Write(" ");
            flag=true;
            System.Console.Error.Write(""+ob);
        }
	System.Console.Error.WriteLine("");
*/
    }
    public SLR1Parser(Grammar g){
        this.G=g;
        var nullable = G.computeNullable();
        var first = G.computeFirst(nullable);
        this.follow = G.computeFollow(nullable, first);
        this.follow["S'"] = new HashSet<string>();
        this.follow["S'"].Add("$");

        var allItems = new List<LR0Item>();
        foreach(var lst in g.nonterminals.Values){
            foreach(Production p in lst){
                for(int j=0;j<=p.rhs.Length;++j){
                    var I = new LR0Item(p,j);
                    allItems.Add(I);
                }
            }
        }
        
        var P = new Production( "S'", new string[]{"S"} );
        nfa.Add( new NFAState( new LR0Item( P, 0 ), 0 ) );
        nfa.Add( new NFAState( new LR0Item( P, 1 ), 1 ) );

        foreach( var I in allItems ){
            NFAState q = new NFAState(I, nfa.Count);
            nfa.Add( q );
        }

        var stateMap = new Dictionary<LR0Item,NFAState>();
        foreach( var q in nfa ){
            stateMap[q.item] = q;
        }

        foreach( NFAState q in nfa){
            LR0Item I = q.item;
            if( I.dpos == I.production.rhs.Length )
                continue;
            string sym = I.production.rhs[ I.dpos ];
            LR0Item I2 = new LR0Item( I.production, I.dpos+1 );
            NFAState q2 = stateMap[I2];
            q.addTransition(sym,q2);

            if( g.nonterminals.ContainsKey(sym)){
                foreach(Production P2 in g.nonterminals[sym]){
                    LR0Item I3 = new LR0Item( P2, 0 );
                    NFAState q3 = stateMap[I3];
                    q.addTransition("",q3);
                }
            }
        }

        foreach( NFAState q in nfa ){
            q.computeClosure( );
        }

        var dfaMap = new Dictionary<HashSet<NFAState>, DFAState >(
            new HashSetKeyThing()
        );

        var toDo = new Stack<DFAState>();

        dfa.Add( new DFAState( nfa[0].closure, 0 ) );
        toDo.Push( dfa[0] );
        dfaMap[dfa[0].label] = dfa[0];

        while(toDo.Count > 0 ){
            DFAState dq = toDo.Pop();

            //transitions, organized by symbol
            var T = new Dictionary<string,HashSet<NFAState>>();
            foreach( NFAState nq in dq.label ){
                foreach( string sym in nq.transitions.Keys ){
                    if( sym != "" ){
                        foreach(NFAState nq2 in nq.transitions[sym]){
                            if( !T.ContainsKey(sym) )
                                T[sym]=new HashSet<NFAState>();
                            T[sym].UnionWith(nq2.closure);
                        }
                    }
                }
            }
            foreach( string sym in T.Keys ){
                HashSet<NFAState> lbl = T[sym];
                if( !dfaMap.ContainsKey(lbl) ){
                    DFAState dq2 = new DFAState(lbl, dfa.Count);
                    dfa.Add(dq2);
                    toDo.Push(dq2);
                    dfaMap[lbl]=dq2;
                }
                dq.addTransition(sym,dfaMap[lbl]);
            }
        }

    }

    public TreeNode parse(Tokenizer T){
        var nodestack = new Stack<TreeNode>();
        var statestack = new Stack<DFAState>();
        nodestack.Push(null);
        statestack.Push(dfa[0]);
        
        while(true){
            string t = T.peek();
            verbose($"Examining token {t} at {T.line},{T.column}");
            DFAState q = statestack.Peek();
            if( q.transitions.ContainsKey(t)){
                //shift
                Token tok = T.next();
                verbose($"Shift {tok}");
                nodestack.Push(new TreeNode(tok.sym,tok));
                statestack.Push(q.transitions[t]);
            } else {
                bool ok=false;
                foreach( NFAState nq in q.label ){
                    LR0Item I = nq.item;
                    if( I.dpos == I.production.rhs.Length ){
                        if( this.follow[I.production.lhs].Contains(t)){
                            //reduce
                            verbose($"Reduce {I.production}");
                            //Special case
                            if( I.production.lhs == "S'"){
                                return nodestack.Pop();
                            }
                            
                            ok=true;
                            var n = new TreeNode(I.production.lhs,null);
                            for(int i=0;i<I.production.rhs.Length;++i){
                                n.children.Insert(0,nodestack.Pop());
                                statestack.Pop();
                            }
                            q = statestack.Peek();
                            statestack.Push(q.transitions[I.production.lhs]);
                            nodestack.Push(n);
                            ok=true;
                            break;
                        }
                    }
                } 
                if ( !ok ){
                    throw new SLRException(
                            $"Cannot shift or reduce at line {T.line}, column {T.column}"
                    );
                }
            }
        }
    } 

    private void replaceNode(TreeNode orig, TreeNode replacement )
    {
        orig.sym = replacement.sym;
        orig.token = replacement.token;
        orig.children = replacement.children;
    }

    public void exprCleanup(TreeNode node)
    {
        foreach(var t in node.children){
            exprCleanup(t);
        }
        if(node.sym.IndexOf("expr") != -1 && node.children.Count == 3 ){
            var left = node.children[0];
            var op = node.children[1];
            var right = node.children[2];
            op.children = new List<TreeNode>{left,right};
            replaceNode( node , op );
        } else if(node.sym.IndexOf("expr") != -1 && node.children.Count == 2 ){
            var op = node.children[0];
            var val = node.children[1];
            op.children = new List<TreeNode>{val};
            replaceNode( node, op );
        } else if( node.sym.IndexOf("expr") != -1 && node.children.Count == 1 ){
            replaceNode( node, node.children[0] );
        } else if( node.sym == "factor" &&  node.children[0].sym == "LP" ){
            replaceNode(node, node.children[1]);
        } else if (node.sym == "factor" && node.children.Count == 1){
            replaceNode( node, node.children[0]);
        }
    }
}

}
