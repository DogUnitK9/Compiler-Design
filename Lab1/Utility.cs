using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lab
{
    public class Label
    {
        static int ctr = 0;
        public static string make()
        {
            string tmp = $"lbl{ctr}";
            ctr++;
            return tmp;
        }
    }
    public class VarLocation
    {
    }
    public class GlobalLocation : VarLocation
    {
        public string label;
        public GlobalLocation()
        {
            this.label = Label.make();
        }
    }
}
