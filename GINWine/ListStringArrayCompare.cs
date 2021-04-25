using System.Collections.Generic;
using System.Linq;
// ReSharper disable All

namespace GINWine
{
    class ListStringArrayCompare : IEqualityComparer<List<string[]>>
    {
        public bool Equals(List<string[]> x, List<string[]> y)
        {
            if (x.Count != y.Count) return false;

            var comparer = new StringArrayCompare();

            return (x.Intersect(y, comparer).Count() == x.Count);
        }

        private static int GetInnerHashCode(string[] obj)
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
        public int GetHashCode(List<string[]> obj)
        {
            unchecked
            {
                int hash = 17;
                foreach (var element in obj)
                {
                    hash = hash * 31 + GetInnerHashCode(element);
                }
                return hash;
            }
        }
    }
}