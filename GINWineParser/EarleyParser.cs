using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GINWineParser
{
    public class EarleyParser
    {
        private readonly bool _checkForCyclicUnitProductions;
        private int[] _finalColumns;
        public ContextFreeGrammar Grammar;
        public ContextFreeGrammar OldGrammar;
        private EarleyColumn[] _table;
        private string[] _text;
        protected Vocabulary Voc;
        private const int MaximumCompletedStatesInColumn = 10000;


        public EarleyParser(ContextFreeGrammar g, Vocabulary v, string[] text, bool checkUnitProductionCycles = true)
        {
            Voc = v;
            Grammar = g;
            _checkForCyclicUnitProductions = checkUnitProductionCycles;
            _text = text;
        }

        private void Predict(EarleyColumn col, List<Rule> ruleList)
        {
            foreach (var rule in ruleList)
            {
                var newState = new EarleyState(rule, 0, col);
                col.AddState(newState, Grammar);
            }
        }


        protected void Scan(EarleyColumn startColumn, EarleyColumn nextCol, EarleyState state, DerivedCategory term,
            string token)
        {
            if (!startColumn.Reductors.TryGetValue(term, out var stateList))
            {
                var scannedStateRule = new Rule(term.ToString(), new[] { token });
                var scannedState = new EarleyState(scannedStateRule, 1, startColumn);
                scannedState.EndColumn = nextCol;
                stateList = new HashSet<EarleyState> { scannedState };
                startColumn.Reductors.Add(term, stateList);
            }


            //var reductor = stateList[0];
            var newState = new EarleyState(state.Rule, state.DotIndex + 1, state.StartColumn);
            state.Parents.Add(newState);
            newState.Predecessor = state;
            nextCol.AddState(newState, Grammar);
        }

        private void Complete(EarleyColumn col, EarleyState reductorState)
        {
            if (reductorState.Rule.LeftHandSide.ToString() == ContextFreeGrammar.GammaRule)
            {
                var sb = new StringBuilder();
                reductorState.Reductor.CreateBracketedRepresentation(sb, Grammar);
                reductorState.BracketedRepresentation = sb.ToString();
                col.GammaStates.Add(reductorState);
                col.AddToTreeDic(reductorState);


                return;
            }

            var startColumn = reductorState.StartColumn;
            var completedSyntacticCategory = reductorState.Rule.LeftHandSide;
            var predecessorStates = startColumn.Predecessors[completedSyntacticCategory];

            if (!startColumn.Reductors.TryGetValue(reductorState.Rule.LeftHandSide, out var reductorList))
            {
                reductorList = new HashSet<EarleyState>();
                startColumn.Reductors.Add(reductorState.Rule.LeftHandSide, reductorList);
            }

            reductorList.Add(reductorState);

            foreach (var predecessor in predecessorStates)
            {
                if (predecessor.Removed)
                    continue;

                var newState = new EarleyState(predecessor.Rule, predecessor.DotIndex + 1, predecessor.StartColumn);
                predecessor.Parents.Add(newState);
                reductorState.Parents.Add(newState);
                newState.Predecessor = predecessor;
                newState.Reductor = reductorState;

                if (_checkForCyclicUnitProductions)
                    if (IsNewStatePartOfUnitProductionCycle(reductorState, newState, startColumn, predecessor))
                        continue;

                col.AddState(newState, Grammar);
            }
        }

        private static bool IsNewStatePartOfUnitProductionCycle(EarleyState reductorState, EarleyState newState,
            EarleyColumn startColumn, EarleyState predecessor)
        {
            //check if the new state completed and is a parent of past reductor for its LHS,
            //if so - you arrived at a unit production cycle.
            var foundCycle = false;

            if (newState.IsCompleted)
                if (startColumn.Reductors.TryGetValue(newState.Rule.LeftHandSide, out var reductors))
                    foreach (var reductor in reductors)
                        if (newState.Rule.Equals(reductor.Rule) && newState.StartColumn == reductor.StartColumn)
                        {
                            var parents = reductor.GetTransitiveClosureOfParents();
                            foreach (var parent in parents)
                                //found a unit production cycle.
                                if (newState == parent)
                                    foundCycle = true;
                        }

            if (foundCycle)
            {
                //undo the parent ties,  and do not insert the new state
                reductorState.Parents.Remove(newState);
                predecessor.Parents.Remove(newState);
                return true;
            }

            return false;
        }

        //read the current gamma states from what's stored in the Earley Table.
        public List<EarleyState> GetGammaStates()
        {
            if (_finalColumns.Length == 1)
                return _table[_finalColumns[0]].GammaStates;

            var gammaStates = new List<EarleyState>();
            foreach (var index in _finalColumns)
                gammaStates.AddRange(_table[index].GammaStates);

            return gammaStates;
        }

        public (int, bool) ReParseSentenceWithRuleAddition(ContextFreeGrammar g, List<Rule> rs)
        {
            Grammar = g;
            bool rejected = false;

            foreach (var col in _table)
            {
                for (var i = 0; i < rs.Count; i++)
                {
                    //seed the new rule in the column
                    //think about categories if this would be context sensitive grammar.
                    if (col.Predecessors.TryGetValue(rs[i].LeftHandSide, out var predecessorsWithKey))
                    {
                        foreach (var predecessor in predecessorsWithKey)
                        {
                            if (!predecessor.Removed)
                            {
                                if (!col.ActionableNonTerminalsToPredict.Contains(rs[i].LeftHandSide))
                                {
                                    var newState = new EarleyState(rs[i], 0, col);
                                    col.AddState(newState, Grammar);
                                }

                                break;
                            }
                        }
                    }
                }


                var exhaustedCompletion = false;
                while (!exhaustedCompletion)
                {
                    //1. complete
                    TraverseCompletedStates(col);

                    //2. predict after complete:
                    TraversePredictableStates(col);
                    exhaustedCompletion = col.ActionableCompleteStates.Count == 0;
                }

                if (col.CompletedStateCount > MaximumCompletedStatesInColumn)
                {
                    rejected = true;
                    break;
                }

                //3. scan after predict -- not necessary if the grammar is non lexicalized,
                //i.e if terminals are not mentioned in the grammar rules.
                //we then can prepare all scanned states in advance (PrepareScannedStates)
                //if you uncomment the following line make sure to uncomment the 
                //ActionableNonCompleteStates enqueuing in Column.AddState()
                //TraverseScannableStates(_table, col);

            }
            bool hasParse = _table[_table.Length - 1].GammaStates.Count > 0;


            if (rejected)
            {
                foreach (var col in _table)
                {
                    col.ActionableCompleteStates.Clear();
                    col.ActionableNonTerminalsToPredict.Clear();
                }

                return (1, hasParse); //parse rejected.

            }
            return (0, hasParse); //all good.
        }

        public bool ReParseSentenceWithRuleDeletion(ContextFreeGrammar g, List<Rule> rs,
            Dictionary<DerivedCategory, LeftCornerInfo> predictionSet)
        {
            foreach (var col in _table)
            {
                foreach (var rule in rs)
                    col.Unpredict(rule, Grammar, predictionSet);


                var exhausted = false;
                while (!exhausted)
                {
                    TraverseStatesToDelete(col);

                    TraversePredictedStatesToDelete(col, predictionSet);

                    //unprediction can lead to completed /uncompleted parents in the same column
                    //if there is a nullable production, same as in the regular
                    exhausted = col.ActionableDeletedStates.Count == 0;
                }
            }

            Grammar = g;
            bool hasParse = _table[_table.Length - 1].GammaStates.Count > 0;
            return hasParse;

        }

        private void TraversePredictedStatesToDelete(EarleyColumn col,
            Dictionary<DerivedCategory, LeftCornerInfo> predictionSet)
        {

            //var visitedDeletedNonterminals = new HashSet<DerivedCategory>();
            while (col.NonTerminalsCandidatesToUnpredict.Count > 0)
            {
                //1. Choose the topmost (root) nonterminal to consider for unprediction
                //based on left corner relations graph.
                //if there is no topmost nonterminal, i.e. a loop, return all nonterminals in the loop.
                var (topmostNonTerminal, nonTerminalsToConsider) = ComputeRootNonTerminalsToUnpredict(col, predictionSet);

                //check the nonterminals to consider if any of them has undeleted predecessor
                bool foundUndeletedPredecessor = FindUndeletedPredecessor(col, predictionSet, nonTerminalsToConsider);

                var transitiveLeftCornerNonTerminals = predictionSet[topmostNonTerminal].NonTerminals;

                if (foundUndeletedPredecessor)
                {
                    col.NonTerminalsCandidatesToUnpredict.Remove(topmostNonTerminal);
                    col.VisitedCategoriesInUnprediction.Add(topmostNonTerminal);

                    //remove from candidate and all its transitive left corner
                    //from non terminals to unprediction candidates
                    foreach (var nt in transitiveLeftCornerNonTerminals)
                    {
                        col.NonTerminalsCandidatesToUnpredict.Remove(nt);
                        col.VisitedCategoriesInUnprediction.Add(nt);
                    }
                }
                else
                {
                    foreach (var nt in nonTerminalsToConsider)
                    {
                        //visitedDeletedNonterminals.Add(nt);
                        col.NonTerminalsCandidatesToUnpredict.Remove(nt);
                        col.VisitedCategoriesInUnprediction.Add(nt);
                    }

                    //insert all transitive left corner nonterminals to check if they need unprediction too.
                    //avoid examining nonterminals that we already verified whether they should be predicted or not.
                    foreach (var nt in transitiveLeftCornerNonTerminals)
                    {
                        if (!col.VisitedCategoriesInUnprediction.Contains(nt))
                        {
                            col.NonTerminalsCandidatesToUnpredict.Add(nt);
                            col.VisitedCategoriesInUnprediction.Add(nt);
                        }
                    }

                    foreach (var nt in nonTerminalsToConsider)
                    {
                        //unpredict the relevant nonterminal(s):
                        if (Grammar.StaticRules.TryGetValue(nt, out var ruleList))
                            foreach (var rule in ruleList)
                                col.Unpredict(rule, Grammar, predictionSet);
                    }

                }
            }

            //clear visited categories since they may be revisited in next loop 
            //from prediction to completion.
            col.VisitedCategoriesInUnprediction.Clear();
        }

        private static bool FindUndeletedPredecessor(EarleyColumn col, Dictionary<DerivedCategory, LeftCornerInfo> predictionSet, List<DerivedCategory> nonTerminalsToConsider)
        {
            bool foundNonDeletedPredecessor = false;
            foreach (var nonTerminalToConsider in nonTerminalsToConsider)
            {
                if (col.Predecessors.TryGetValue(nonTerminalToConsider, out var predecessors))
                {
                    foreach (var predecessor in predecessors)
                    {
                        if (!predecessor.Removed)
                        {
                            //when have we found a predecessor state which will not be deleted by removing all rules with nonTerminalToConsider as LHS?
                            //either when the predecessor is not predicted itself,
                            //or if it is predicted and also not in the prediction set of rules of the transitive left corner of nonTerminalToConsider.
                            if (predecessor.DotIndex != 0 ||

                                !predictionSet[nonTerminalToConsider].NonTerminals.Contains(predecessor.Rule.LeftHandSide))
                            {
                                foundNonDeletedPredecessor = true;
                                break;
                            }
                        }
                    }

                    if (foundNonDeletedPredecessor)
                        break;
                }
                else
                {
                    throw new Exception("FindUndeletedPredecessor: show me a nonterminal without predecessors.");
                }
            }

            return foundNonDeletedPredecessor;
        }

        private static (DerivedCategory topmost, List<DerivedCategory> nonTerminalsToConsider) ComputeRootNonTerminalsToUnpredict(EarleyColumn col, Dictionary<DerivedCategory, LeftCornerInfo> predictionSet)
        {
            var nonTerminalsToConsider = new List<DerivedCategory>();

            var topmostCandidate = col.NonTerminalsCandidatesToUnpredict.First();
            foreach (var nonterminal in col.NonTerminalsCandidatesToUnpredict)
            {
                if (predictionSet[nonterminal].NonTerminals.Contains(topmostCandidate))
                    topmostCandidate = nonterminal;
            }
            nonTerminalsToConsider.Add(topmostCandidate);

            foreach (var nonterminal in predictionSet[topmostCandidate].NonTerminals)
            {
                //for every non terminal in the left corner that contains the topmost candidate,
                //then it is in a closed loop with the topmost candidate and must be considered.
                if (predictionSet[nonterminal].NonTerminals.Contains(topmostCandidate) && !nonterminal.Equals(topmostCandidate))
                    nonTerminalsToConsider.Add(nonterminal);
            }


            return (topmostCandidate, nonTerminalsToConsider);
        }

        private void TraverseStatesToDelete(EarleyColumn col)
        {
            while (col.ActionableDeletedStates.Count > 0)
            {
                var state = col.ActionableDeletedStates.Pop();
                state.EndColumn.MarkStateDeleted(state, Grammar);
            }
        }

        public List<EarleyState> GenerateSentence(string[] text, int maxWords = 0)
        {
            _text = text;
            (_table, _finalColumns) = PrepareEarleyTable(text, maxWords);
            PrepareScannedStates();

            //assumption: GenerateAllStaticRulesFromDynamicRules has been called before parsing
            //and added the GammaRule
            var startRule = Grammar.StaticRules[new DerivedCategory(ContextFreeGrammar.GammaRule)][0];

            var startState = new EarleyState(startRule, 0, _table[0]);
            _table[0].AddState(startState, Grammar);

                foreach (var col in _table)
                {
                    var exhaustedCompletion = false;
                    while (!exhaustedCompletion)
                    {
                        //1. complete
                        TraverseCompletedStates(col);

                        //2. predict after complete:
                        TraversePredictableStates(col);

                        //prediction of epsilon transitions can lead to completed states.
                        //hence we might need to complete those states.
                        exhaustedCompletion = col.ActionableCompleteStates.Count == 0;
                    }

                    //3. scan after predict -- not necessary if the grammar is non lexicalized,
                    //i.e if terminals are not mentioned in the grammar rules.
                    //we then can prepare all scanned states in advance (PrepareScannedStates)
                    //if you uncomment the following line make sure to uncomment the 
                    //ActionableNonCompleteStates enqueuing in Column.AddState()
                    //var anyScanned = TraverseScannableStates(_table, col);

                    //if (!anyCompleted && !anyPredicted /*&& !anyScanned*/) break;
                }
            

            return GetGammaStates();
        }

        public bool ParseSentence(Dictionary<int, HashSet<string>> treesDic, int maxWords = 0)
        {
            (_table, _finalColumns) = PrepareEarleyTable(_text, maxWords);
            _table[_table.Length - 1].SetLastColumn(_text.Length, treesDic);
            PrepareScannedStates();

            //assumption: GenerateAllStaticRulesFromDynamicRules has been called before parsing
            //and added the GammaRule
            var startRule = Grammar.StaticRules[new DerivedCategory(ContextFreeGrammar.GammaRule)][0];

            var startState = new EarleyState(startRule, 0, _table[0]);
            _table[0].AddState(startState, Grammar);

                foreach (var col in _table)
                {
                    var exhaustedCompletion = false;
                    while (!exhaustedCompletion)
                    {
                        //1. complete
                        TraverseCompletedStates(col);

                        //2. predict after complete:
                        TraversePredictableStates(col);

                        //prediction of epsilon transitions can lead to completed states.
                        //hence we might need to complete those states.
                        exhaustedCompletion = col.ActionableCompleteStates.Count == 0;
                    }

                    //3. scan after predict -- not necessary if the grammar is non lexicalized,
                    //i.e if terminals are not mentioned in the grammar rules.
                    //we then can prepare all scanned states in advance (PrepareScannedStates)
                    //if you uncomment the following line make sure to uncomment the 
                    //ActionableNonCompleteStates enqueuing in Column.AddState()
                    //var anyScanned = TraverseScannableStates(_table, col);

                    //if (!anyCompleted && !anyPredicted /*&& !anyScanned*/) break;
                }


            bool hasParse = _table[_table.Length - 1].GammaStates.Count > 0;
            return hasParse;
        }

        protected virtual (EarleyColumn[], int[]) PrepareEarleyTable(string[] text, int maxWord)
        {
            var table = new EarleyColumn[text.Length + 1];

            for (var i = 1; i < table.Length; i++)
                table[i] = new EarleyColumn(i, text[i - 1]);

            table[0] = new EarleyColumn(0, "");
            return (table, new[] { table.Length - 1 });
        }

        protected virtual HashSet<string> GetPossibleSyntacticCategoriesForToken(string nextScannableTerm)
        {
            return Voc[nextScannableTerm];
        }

        private void PrepareScannedStates()
        {
            for (int i = 0; i < _table.Length - 1; i++)
            {
                var nextScannableTerm = _table[i + 1].Token;
                var possibleNonTerminals = GetPossibleSyntacticCategoriesForToken(nextScannableTerm);

                foreach (var nonTerminal in possibleNonTerminals)
                {
                    var currentCategory = new DerivedCategory(nonTerminal);

                    var scannedStateRule = new Rule(nonTerminal, new[] { _table[i + 1].Token });
                    var scannedState = new EarleyState(scannedStateRule, 1, _table[i]);
                    scannedState.EndColumn = _table[i + 1];
                    _table[i].Reductors[currentCategory] = new HashSet<EarleyState> { scannedState };
                }
            }
        }

/*
        private bool TraverseScannableStates(EarleyColumn[] table, EarleyColumn col)
        {
            var anyScanned = col.ActionableNonCompleteStates.Count > 0;
            if (col.Index + 1 >= table.Length)
            {
                col.ActionableNonCompleteStates.Clear();
                return false;
            }

            while (col.ActionableNonCompleteStates.Count > 0)
            {
                var stateToScan = col.ActionableNonCompleteStates.Dequeue();

                var nextScannableTerm = table[col.Index + 1].Token;
                var possibleSyntacticCategories = GetPossibleSyntacticCategoriesForToken(nextScannableTerm);

                foreach (var item in possibleSyntacticCategories)
                {
                    var currentCategory = new DerivedCategory(item);
                    if (stateToScan.NextTerm.Equals(currentCategory))
                        Scan(table[col.Index], table[col.Index + 1], stateToScan, currentCategory, nextScannableTerm);
                }
            }

            return anyScanned;
        }
*/

        private void TraversePredictableStates(EarleyColumn col)
        {
            while (col.ActionableNonTerminalsToPredict.Count > 0)
            {
                var nextTerm = col.ActionableNonTerminalsToPredict.Dequeue();
                var ruleList = Grammar.StaticRules[nextTerm];
                Predict(col, ruleList);
            }
        }

        private void TraverseCompletedStates(EarleyColumn col)
        {
            while (col.ActionableCompleteStates.Count > 0)
            {
                var state = col.ActionableCompleteStates.Dequeue();
                Complete(col, state);
            }
        }

        public string EarleyTableToString()
        {
            var sb = new StringBuilder();
            var cc = new CategoryCompare();

            foreach (var col in _table)
            {
                sb.AppendLine($"col {col.Index}");

                sb.AppendLine("Predecessors:");
                var keys = col.Predecessors.Keys.ToArray();
                Array.Sort(keys, cc);

                foreach (var key in keys)
                {
                    sb.AppendLine($"key {key}");
                    var values = new List<string>();
                    foreach (var state in col.Predecessors[key])
                        values.Add(state.ToString());
                    values.Sort();

                    foreach (var value in values)
                        sb.AppendLine(value);
                }

                sb.AppendLine("Predicted:");
                var keys1 = col.Predicted.Keys.Select(x => (x.ToString(), x));
                var ordered = keys1.OrderBy(x => x.Item1);
                foreach (var stringAndRule in ordered)
                    sb.AppendLine($"{stringAndRule.Item1}");
                //List<string> values = new List<string>();
                //values.Add(col.Predicted[key].ToString());
                //values.Sort();

                //foreach (var value in values)
                //    sb.AppendLine(value);
                sb.AppendLine("Reductors:");

                var keys2 = col.Reductors.Keys.ToArray();
                Array.Sort(keys2, cc);
                foreach (var key in keys2)
                {
                    //do not write POS keys into string
                    //reason: for comparison purposes (debugging) between 
                    //from-scratch earley parser and differential earley parser.
                    //the differential parser contains also reductor items for
                    //POS although the column might not be parsed at all.
                    //the from-scratch parser will not contain those reductor items
                    //but this is OK, we don't care about these items.
                    if (!Grammar.StaticRules.ContainsKey(key)) continue;

                    sb.AppendLine($"key {key}");
                    var values = new List<string>();

                    foreach (var state in col.Reductors[key])
                        values.Add(state.ToString());
                    values.Sort();

                    foreach (var value in values)
                        sb.AppendLine(value);
                }
            }

            return sb.ToString();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            var cc = new CategoryCompare();

            foreach (var col in _table)
            {
                if (!col._isLastColumn) continue;

                var dic = col.GetTreesDic();
                foreach (var kvp in dic)
                {
                    sb.AppendLine($"length {kvp.Key} sentence count: {kvp.Value.Count}");
                }
            }

            return sb.ToString();
        }

        public void AcceptChanges()
        {
            foreach (var col in _table)
                col.AcceptChanges();


            foreach (var col in _table)
                col.OldGammaStates?.Clear();


            OldGrammar = null;
        }

        public void RejectChanges()
        {

            foreach (var col in _table)
                col.RejectChanges();

            foreach (var col in _table)
                col.AcceptChanges();

            Grammar = OldGrammar;
            OldGrammar = null;

        }

        //Suggest RHS for a rule that would complete currently unparsed sequence.
        //not working for LIG - only for CFG.
        public string[] SuggestRHSForCompletion()
        {
            //1. find completion that is closest to the end of the table
            int furthestCompletedColumn = -1;

            //we go back from penultimate column.
            for (int i = _table.Length - 2; i >= 0; i--)
            {
                if (_table[i].Reductors.Count > 0)
                {
                    foreach (var reductor in _table[i].Reductors)
                    {
                        var isPOS = !Grammar.StaticRules.ContainsKey(reductor.Key);
                        if (isPOS) continue;

                        foreach (var item in reductor.Value)
                        {
                            if (item.EndColumn.Index > furthestCompletedColumn)
                                furthestCompletedColumn = item.EndColumn.Index;
                        }
                    }
                }
            }

            if (furthestCompletedColumn < 0 || furthestCompletedColumn == _table.Length - 1) return null;

            //2. randomly choose a reductor with the same end column
            List<EarleyState> candidates = new List<EarleyState>();

            for (int i = _table.Length - 2; i >= 0; i--)
            {
                if (_table[i].Reductors.Count > 0)
                {
                    foreach (var reductor in _table[i].Reductors)
                    {
                        var isPOS = !Grammar.StaticRules.ContainsKey(reductor.Key);
                        if (isPOS) continue;

                        foreach (var item in reductor.Value)
                        {
                            if (item.EndColumn.Index == furthestCompletedColumn &&
                                item.Rule.LeftHandSide.ToString() != ContextFreeGrammar.StartSymbol)
                                candidates.Add(item);
                        }
                    }
                }
            }
            var furthestUnparsedToken = _table[furthestCompletedColumn + 1].Token;
            var possibleNonTerminals = GetPossibleSyntacticCategoriesForToken(furthestUnparsedToken);

            var r = Pseudorandom.NextInt(candidates.Count);
            return new[] { candidates[r].Rule.LeftHandSide.ToString(), possibleNonTerminals.First() };
        }

        public class CategoryCompare : IComparer<DerivedCategory>
        {
            public int Compare(DerivedCategory x, DerivedCategory y)
            {
                return String.CompareOrdinal(x.ToString(), y.ToString());
            }
        }
    }
}