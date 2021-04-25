using System.Collections.Generic;

namespace GINWineParser
{
    public class GrammarTreeCountsCalculator
    {
        private readonly HashSet<string> _pos;

        private readonly int _treeDepth;
        private SubTreeCountsCache _cache;
        public ContextFreeGrammar G;

        public GrammarTreeCountsCalculator(HashSet<string> pos, int max)
        {
            _pos = pos;
            _treeDepth = max + 3;
        }

        public int[] NumberOfParseTreesPerWords()
        {
            return ParseTreesForCategoryInDepth(new DerivedCategory(ContextFreeGrammar.StartSymbol), _treeDepth - 1);
        }

        //in a given depth treedepth, we have array of arr[wc] = tc;
        private int[] ParseTreesForRuleInDepth(Rule r, int treeDepth)
        {
            var ruleCounts = _cache.RuleCache[r][treeDepth];

            //check if in rules cache.
            var storedCountsOfRules = _cache.RuleCache[r];
            var indexInRuleCacheArr = treeDepth;
            var cachedRuleCounts = storedCountsOfRules[indexInRuleCacheArr];

            while (IsEmpty(cachedRuleCounts) && ++indexInRuleCacheArr < _treeDepth)
                cachedRuleCounts = storedCountsOfRules[indexInRuleCacheArr];

            if (!IsEmpty(cachedRuleCounts)) return cachedRuleCounts;

            //from now on, not found cached rule information
            var rhs = r.RightHandSide;

            if (treeDepth > 0)
            {
                if (rhs.Length == 2)
                {
                    var catCounts1 = ParseTreesForCategoryInDepth(rhs[0], treeDepth - 1);
                    var catCounts2 = ParseTreesForCategoryInDepth(rhs[1], treeDepth - 1);

                    UpdateCounts(ruleCounts, catCounts1, catCounts2);
                }
                else
                {
                    var catCounts1 = ParseTreesForCategoryInDepth(rhs[0], treeDepth - 1);
                    UpdateCounts(ruleCounts, catCounts1);
                }
            }

            ruleCounts[_treeDepth] = 1;
            return ruleCounts;
        }

        private void UpdateCounts(int[] res, int[] fromCat1, int[] fromCat2)
        {
            for (var i = 0; i < _treeDepth; i++)
            {
                if (fromCat1[i] <= 0) continue;
                for (var j = 0; j < _treeDepth; j++)
                {
                    if (fromCat2[j] <= 0) continue;

                    var wc = i + j;
                    var tc = fromCat1[i] * fromCat2[j];

                    if (wc < _treeDepth)
                        res[wc] += tc;
                }
            }
        }

        private bool IsEmpty(int[] res)
        {
            return res[_treeDepth] == 0;
        }

        private int[] ParseTreesForCategoryInDepth(DerivedCategory cat, int treeDepth)
        {
            var categoryCounts = _cache.CategoriesCache[cat][treeDepth];

            //check if in categories cache.
            var storedCountsOfCat = _cache.CategoriesCache[cat];
            var indexInCatCacheArr = treeDepth;
            var cachedCategoryCounts = storedCountsOfCat[indexInCatCacheArr];

            while (IsEmpty(cachedCategoryCounts) && ++indexInCatCacheArr < _treeDepth)
                cachedCategoryCounts = storedCountsOfCat[indexInCatCacheArr];

            if (!IsEmpty(cachedCategoryCounts)) return cachedCategoryCounts;

            //from now on, not found cached category information.
            if (G.StaticRules.ContainsKey(cat))
            {
                var ruleList = G.StaticRules[cat];
                foreach (var rule in ruleList)
                {
                    var ruleCounts = ParseTreesForRuleInDepth(rule, treeDepth);
                    UpdateCounts(categoryCounts, ruleCounts);
                }
            }
            else if (_pos.Contains(cat.ToString()))
            {
                CountTerminal(categoryCounts);
            }
            else if (cat.ToString() == ContextFreeGrammar.EpsilonSymbol)
            {
                CountEpsilon(categoryCounts);
            }

            categoryCounts[_treeDepth] = 1;
            return categoryCounts;
        }

        private static void CountTerminal(int[] res)
        {
            res[1] += 1;
        }

        private static void CountEpsilon(int[] res)
        {
            res[0] += 1;
        }

        private void UpdateCounts(int[] res, int[] fromRHS)
        {
            for (var i = 0; i < _treeDepth; i++)
                res[i] += fromRHS[i];
        }

        public int[] Recalculate(ContextFreeGrammar grammar)
        {
            _cache = new SubTreeCountsCache(grammar, _treeDepth);
            G = grammar;

            return NumberOfParseTreesPerWords();
        }
    }
}