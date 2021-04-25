using System.Collections.Generic;

namespace GINWineParser
{
    public class SubTreeCountsCache
    {
        public Dictionary<DerivedCategory, int[][]> CategoriesCache;
        public Dictionary<Rule, int[][]> RuleCache;

        public SubTreeCountsCache(ContextFreeGrammar g, int depth)
        {
            CategoriesCache = new Dictionary<DerivedCategory, int[][]>();
            foreach (var lhs in g.StaticRulesGeneratedForCategory)
            {
                CategoriesCache[lhs] = new int[depth][];

                for (var i = 0; i < depth; i++)
                    CategoriesCache[lhs][i] = new int[depth + 1];
                //the last location is a flag that signifies that the cache cell is used.
            }

            //RuleCache = new Dictionary<Rule, int[][]>(new GeneratingRuleComparer());
            RuleCache = new Dictionary<Rule, int[][]>(new RuleReferenceEquals());
            foreach (var ruleList in g.StaticRules.Values)
            {
                foreach (var rule in ruleList)
                {

                    RuleCache[rule] = new int[depth][];
                    for (var i = 0; i < depth; i++)
                        RuleCache[rule][i] = new int[depth + 1];
                    //the last location is a flag that signifies that the cache cell is used.
                }
            }
        }
    }
}