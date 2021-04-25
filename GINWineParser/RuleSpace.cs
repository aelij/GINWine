using System.Collections.Generic;
using System.Linq;

namespace GINWineParser
{
    public class RuleType
    {
        public static int CFGRules = 0;
        public static int Push1Rules = 1;
        public static int PopRules = 2;
        public static int RuleTypeCount = 3;
    }

    public class RuleSpace
    {
        private readonly List<int>[] _allowedRHSIndices;
        private readonly Dictionary<string, int> _nonTerminalLHS = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _nonTerminalsRHS = new Dictionary<string, int>();


        private readonly Rule[][][] _ruleSpace;

        public RuleSpace(HashSet<string> partsOfSpeechCategories, HashSet<(string rhs1, string rhs2)> bigrams,
            int maxNonTerminals)
        {
            var rhsStore = new List<string>();
            var nonTerminals = new List<string>();

            _ruleSpace = new Rule[RuleType.RuleTypeCount][][];
            _allowedRHSIndices = new List<int>[RuleType.RuleTypeCount];

            for (var i = 0; i < RuleType.RuleTypeCount; i++)
            {
                _ruleSpace[i] = new Rule[maxNonTerminals][];
                _allowedRHSIndices[i] = new List<int>();
            }

            for (var i = 1; i <= maxNonTerminals; i++)
            {
                nonTerminals.Add($"X{i}");
                _nonTerminalLHS[$"X{i}"] = i - 1;
            }

            rhsStore.Add("EMPTY");
            rhsStore = rhsStore.Concat(partsOfSpeechCategories).ToList();
            rhsStore = rhsStore.Concat(nonTerminals).ToList();

            for (var i = 0; i < rhsStore.Count; i++)
                _nonTerminalsRHS[rhsStore[i]] = i;


            var currentCategory = new DerivedCategory("X1", ContextFreeGrammar.StarSymbol);
            var startCategory = new DerivedCategory(ContextFreeGrammar.StartSymbol, ContextFreeGrammar.StarSymbol);
            var length = rhsStore.Count;
            var numberOfPossibleRHS = length * length;

            //allocating the first column of CFG, push and pop rule tables.
            //there are numberOfPossibleRHS - length + 1 unique combinations 
            //(numberOfPossibleRHS includes counting twice POS1*POS1, POS2*POS2 etc)
            _ruleSpace[RuleType.CFGRules][0] = new Rule[numberOfPossibleRHS - length + 1];
            _ruleSpace[RuleType.Push1Rules][0] = new Rule[numberOfPossibleRHS - length + 1];
            _ruleSpace[RuleType.PopRules][0] = new Rule[1];

            //START -> Xi rule in CFG Rule table.
            _ruleSpace[RuleType.CFGRules][0][0] = new Rule(startCategory, new[] { currentCategory });
            _allowedRHSIndices[RuleType.CFGRules].Add(0);

            //START -> Xi rule in Push Rule table. (unused).
            _ruleSpace[RuleType.Push1Rules][0][0] = new Rule(startCategory, new[] { currentCategory });

            //Xi[Xi] -> epsilon Pop in Pop Rule table. 
            var epsilonCat = new DerivedCategory(ContextFreeGrammar.EpsilonSymbol);
            epsilonCat.StackSymbolsCount = -1;
            var currentCatWithCurrenCatStack = new DerivedCategory("X1", "X1");
            _ruleSpace[RuleType.PopRules][0][0] = new Rule(currentCatWithCurrenCatStack, new[] { epsilonCat });

            for (var i = length; i < numberOfPossibleRHS; i++)
            {
                var currentIndex = i - length + 1;
                var rhs1 = rhsStore[i / length];
                if (i % length == 0)
                {
                    DerivedCategory rhs1Cat;
                    //Xi -> RHS in CFG Rules

                    if (partsOfSpeechCategories.Contains(rhs1))
                        rhs1Cat = new DerivedCategory(rhs1);
                    else
                        rhs1Cat = new DerivedCategory(rhs1, ContextFreeGrammar.StarSymbol);

                    _ruleSpace[RuleType.CFGRules][0][currentIndex] = new Rule(currentCategory, new[] { rhs1Cat });

                    //Xi -> RHS in Push Rules (unused)
                    rhs1Cat.StackSymbolsCount = 1;
                    _ruleSpace[RuleType.Push1Rules][0][currentIndex] = new Rule(currentCategory, new[] { rhs1Cat });

                    //unit production rules Xi -> Xj are excluded from CFG Rules
                    //unit production either Xi -> Xj or Xi -> POS are excluded from Push rules.
                    //note:  S -> Xi is allowed (in index 0).
                    if (rhs1Cat.ToString()[0] != 'X')
                        _allowedRHSIndices[RuleType.CFGRules].Add(currentIndex);
                }
                else
                {
                    var rhs2 = rhsStore[i % length];
                    //TODO: assumption spine category is the second rhs. in later stage allow spine to be either rhs.
                    //Xi -> RHS1 RHS2 in CFG Rules
                    var rhs1Cat = new DerivedCategory(rhs1);
                    DerivedCategory rhs2Cat;

                    if (partsOfSpeechCategories.Contains(rhs2))
                        rhs2Cat = new DerivedCategory(rhs2);
                    else
                        rhs2Cat = new DerivedCategory(rhs2, ContextFreeGrammar.StarSymbol);

                    _ruleSpace[RuleType.CFGRules][0][currentIndex] =
                        new Rule(currentCategory, new[] { rhs1Cat, rhs2Cat });

                    //Xi -> RHS1 RHS2[*RHS1] in Push Rules
                    var rhs2CatForPushRule = new DerivedCategory(rhs2, ContextFreeGrammar.StarSymbol + rhs1);
                    rhs2CatForPushRule.StackSymbolsCount = 1;
                    _ruleSpace[RuleType.Push1Rules][0][currentIndex] =
                        new Rule(currentCategory, new[] { rhs1Cat, rhs2CatForPushRule });


                    if (partsOfSpeechCategories.Contains(rhs1) && partsOfSpeechCategories.Contains(rhs2))
                    {
                        if (bigrams.Contains((rhs1, rhs2)))
                            _allowedRHSIndices[RuleType.CFGRules].Add(currentIndex);
                    }
                    else
                    {
                        //Xi -> Xj Xj (Xj non-terminal) are excluded from CFG Rules
                        if (rhs1 != rhs2)
                            _allowedRHSIndices[RuleType.CFGRules].Add(currentIndex);
                    }

                    //allow only movement of nonterminals (Consequence: movement cannot be out of terminals)
                    //also disallow rules such as Xi -> Xj Xj[*Xj] (this is equal to unit production Xi -> Xj)
                    if (rhs1 != rhs2 && !partsOfSpeechCategories.Contains(rhs2) &&
                        !partsOfSpeechCategories.Contains(rhs1))
                        _allowedRHSIndices[RuleType.Push1Rules].Add(currentIndex);
                }
            }

            _allowedRHSIndices[RuleType.PopRules].Add(0);

            for (var i = 1; i < maxNonTerminals; i++)
            {
                var currentLHSNonterminal = $"X{i + 1}";
                currentCategory = new DerivedCategory(currentLHSNonterminal, ContextFreeGrammar.StarSymbol);

                //copy column 0 to column i in CFG Rules, change the current LHS Nonterminal
                _ruleSpace[RuleType.CFGRules][i] = _ruleSpace[RuleType.CFGRules][0]
                    .Select(x => new Rule(currentCategory, x.RightHandSide)).ToArray();
                _ruleSpace[RuleType.CFGRules][i][0] = new Rule(startCategory, new[] { currentCategory });

                //copy column 0 to column i in Push Rules, change the current LHS Nonterminal
                _ruleSpace[RuleType.Push1Rules][i] = _ruleSpace[RuleType.Push1Rules][0]
                    .Select(x => new Rule(currentCategory, x.RightHandSide)).ToArray();
                _ruleSpace[RuleType.Push1Rules][i][0] = new Rule(startCategory, new[] { currentCategory });

                //copy column 0 to column in Pop Rules. change the current nonterminal
                currentCatWithCurrenCatStack = new DerivedCategory(currentLHSNonterminal, currentLHSNonterminal);
                _ruleSpace[RuleType.PopRules][i] = new Rule[1];
                _ruleSpace[RuleType.PopRules][i][0] = new Rule(currentCatWithCurrenCatStack, new[] { epsilonCat });
            }

            var counter = 1;
            for (var k = 0; k < RuleType.RuleTypeCount; k++)
                for (var j = 0; j < _ruleSpace[k].Length; j++)
                    for (var i = 0; i < _ruleSpace[k][j].Length; i++)
                        _ruleSpace[k][j][i].NumberOfGeneratingRule = counter++;

            //for (var j = 0; j < _ruleSpace[0].Length; j++)
            //{
            //    for (var i = 0; i < _ruleSpace[0][j].Length; i++)
            //        _ruleSpace[0][j][i].Number = j * _ruleSpace[0][0].Length + i + 1;
            //}
        }

        public Rule this[RuleCoordinates rc] => _ruleSpace[rc.RuleType][rc.LHSIndex][rc.RHSIndex];

        public int RowsCount(int ruleTypeCount)
        {
            return _ruleSpace[ruleTypeCount].Length;
        }

        public int GetRandomRHSIndex(int ruleType)
        {
            var r = Pseudorandom.NextInt(_allowedRHSIndices[ruleType].Count);
            return _allowedRHSIndices[ruleType][r];
        }

        public int GetRandomLHSIndex()
        {
            var r = Pseudorandom.NextInt(_ruleSpace[0].Length);
            return r; //same LHS indices for all rule type tables.
        }

        public RuleCoordinates FindRule(Rule r)
        {
            var rc = new RuleCoordinates { RuleType = RuleType.CFGRules };

            var lhs = new SyntacticCategory(r.LeftHandSide);

            //if (r.LeftHandSide.Stack.Contains(ContextFreeGrammar.StarSymbol))
            //    if (r.LeftHandSide.Stack.Length > 1)
            //    {
            //        //TODO: in future, implement pop2 stack changing operation.
            //    }

            if (lhs.ToString() == ContextFreeGrammar.StartSymbol)
            {
                //each start rule is of the form S -> Xi and is the first element of the xi column
                rc.LHSIndex = FindLHSIndex(new SyntacticCategory(r.RightHandSide[0]).ToString());
                rc.RHSIndex = 0;
            }
            else
            {
                rc.LHSIndex = FindLHSIndex(new SyntacticCategory(lhs).ToString());

                foreach (var t in r.RightHandSide)
                    if (t.StackSymbolsCount == 1)
                        rc.RuleType = RuleType.Push1Rules;
                    else if (t.StackSymbolsCount == -1)
                        rc.RuleType = RuleType.PopRules;

                rc.RHSIndex = rc.RuleType != RuleType.PopRules
                    ? FindRHSIndex(r.RightHandSide.Select(x => new SyntacticCategory(x).ToString()).ToArray())
                    : 0;
            }

            return rc;
        }

        public int FindLHSIndex(string lhs)
        {
            return _nonTerminalLHS[lhs];
        }

        public int FindRHSIndex(string[] rhs)
        {
            var length = _nonTerminalsRHS.Count;
            var rhsIndexinStore1 = _nonTerminalsRHS[rhs[0]];

            if (rhs.Length == 1)
                return (rhsIndexinStore1 - 1) * length + 1;

            var rhsIndexinStore2 = _nonTerminalsRHS[rhs[1]];
            return (rhsIndexinStore1 - 1) * length + rhsIndexinStore2 + 1;
        }
    }
}