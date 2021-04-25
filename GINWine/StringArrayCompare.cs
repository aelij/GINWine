using System.Collections.Generic;
using System.Linq;

namespace GINWine
{
    class StringArrayCompare : IEqualityComparer<string[]>
    {
        public bool Equals(string[] x,  string[] y)
        {
            return x.SequenceEqual(y);
        }

        public int GetHashCode(string[] obj)
        {
            unchecked
            {
                int hash = 17;
                foreach (var element in obj)
                {
                    hash = hash * 31 + element.GetHashCode();
                }
                return hash;
            }
        }
    }
}