using System.Collections.Generic;

namespace lab{

public class DFAState {
    public int index;           //unique number for each state
    public HashSet<NFAState> label;
    public Dictionary<string,DFAState> transitions =
                    new Dictionary<string,DFAState>();

    public DFAState(HashSet<NFAState> lbl, int index){
        this.label=lbl;
        this.index=index;
    }
    public void addTransition(string sym, DFAState q){
        if( this.transitions.ContainsKey(sym) )
            throw new System.Exception("Duplicate transition");
        this.transitions[sym] = q;
    }
}

}