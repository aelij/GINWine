using System.Collections.Generic;

namespace GINWineParser
{
    //This class is used to store the data required for the dictionary of left corner.
    public class LeftCornerInfo
    {

        //the set of nonterminals contained in the transitive left corner of the key.
        public HashSet<DerivedCategory> NonTerminals { get; set; }
    }

    public class LeftCorner
    {
        private Dictionary<DerivedCategory, LeftCornerInfo> _leftCorners;

        private void DFS(DerivedCategory root, DerivedCategory cat, HashSet<DerivedCategory> visited,
            Dictionary<DerivedCategory, List<Rule>> rules)
        {
            visited.Add(cat);
            _leftCorners[root].NonTerminals.Add(cat);

            if (rules.TryGetValue(cat, out var rulesOfCat))
            {
                foreach (var r in rulesOfCat)
                {
                    if (rules.ContainsKey(r.RightHandSide[0]) && !visited.Contains(r.RightHandSide[0]))
                        DFS(root, r.RightHandSide[0], visited, rules);
                }
            }
        }

        public Dictionary<DerivedCategory, LeftCornerInfo> ComputeLeftCorner(
            Dictionary<DerivedCategory, List<Rule>> rules)
        {
            //key - nonterminal, value - see above
            _leftCorners = new Dictionary<DerivedCategory, LeftCornerInfo>();

            var nonTerminals = rules.Keys;

            foreach (var nt in nonTerminals)
            {
                _leftCorners[nt] = new LeftCornerInfo();
                _leftCorners[nt].NonTerminals = new HashSet<DerivedCategory>();
                var visited = new HashSet<DerivedCategory>();
                foreach (var r in rules[nt])
                {
                    if (rules.ContainsKey(r.RightHandSide[0]) && !visited.Contains(r.RightHandSide[0]))
                        DFS(nt, r.RightHandSide[0], visited, rules);
                }
            }

            return _leftCorners;
        }
    }
}