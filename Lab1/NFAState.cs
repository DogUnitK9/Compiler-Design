using System.Collections.Generic;


namespace lab{

public class NFAState{
    public LR0Item item;
    public int index;
    public Dictionary<string,List<NFAState> > transitions = new Dictionary<string,List<NFAState>>();
    public HashSet<NFAState> closure = new HashSet<NFAState>();
    public NFAState(LR0Item item, int index){
        this.item = item;
        this.index = index;
    }
    public void addTransition( string sym, NFAState q ){
        if( !this.transitions.ContainsKey(sym) )
            this.transitions[sym] = new List<NFAState>();
        this.transitions[sym].Add(q);
    }
    public override int GetHashCode(){
        return item.GetHashCode();
    }
    public override bool Equals(object o){
        if(o==null || !(o is NFAState))
            return false;
        var q = (o as NFAState);
        return this.item.Equals(q.item);
    }
    
    public void computeClosure(){
        computeClosure(this,this.closure);
    }
    private void computeClosure( NFAState q,
             HashSet<NFAState> closure)
    {
        closure.Add(q);
        if( q.transitions.ContainsKey("") ){
            foreach( NFAState q2 in q.transitions[""] ){
                if( !closure.Contains(q2) )
                    computeClosure( q2,closure );
            }
        }
    }
}

}