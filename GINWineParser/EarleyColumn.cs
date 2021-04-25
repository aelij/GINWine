using System;
using System.Collections.Generic;

namespace GINWineParser
{
    public class EarleyColumn
    {
        internal Queue<DerivedCategory> ActionableNonTerminalsToPredict;
        internal Dictionary<DerivedCategory, HashSet<EarleyState>> Predecessors;
        internal Dictionary<Rule, List<EarleyState>> Predicted;
        internal Dictionary<DerivedCategory, HashSet<EarleyState>> Reductors;
        internal List<EarleyState> StatesAddedInLastReparse = new List<EarleyState>();
        internal HashSet<EarleyState> StatesRemovedInLastReparse = new HashSet<EarleyState>();
        private Dictionary<int, HashSet<string>> _treesDic;
        public Dictionary<int, HashSet<string>> GetTreesDic() {  return _treesDic; }
        public bool _isLastColumn;
        private int _textLength;
        public int CompletedStateCount;

        internal HashSet<DerivedCategory> VisitedCategoriesInUnprediction = new HashSet<DerivedCategory>();
        internal HashSet<DerivedCategory> NonTerminalsCandidatesToUnpredict = new HashSet<DerivedCategory>();

        public void SetLastColumn(int textLength, Dictionary<int, HashSet<string>> treesDic)
        {
            _isLastColumn = true;
            _textLength = textLength;
            _treesDic = treesDic;
        }

        public void RemoveFromTreeDic(EarleyState state)
        {
            if (_isLastColumn)
            {
                lock (_treesDic[_textLength])
                {
                    _treesDic[_textLength].Remove(state.BracketedRepresentation);
                }
            }
        }

        public void AddToTreeDic(EarleyState state)
        {
            if (_isLastColumn)
            {
                lock (_treesDic[_textLength])
                {
                    _treesDic[_textLength].Add(state.BracketedRepresentation);
                }
            }
        }

        public EarleyColumn(int index, string token)
        {
            Index = index;
            Token = token;
            
            //completed agenda is ordered in decreasing order of start indices (see Stolcke 1995 about completion priority queue).
            ActionableCompleteStates = new CompletedStatesHeap();
            ActionableDeletedStates = new DeletedStatesHeap();

            //ActionableNonCompleteStates = new Queue<EarleyState>();
            Predecessors = new Dictionary<DerivedCategory, HashSet<EarleyState>>();
            Reductors = new Dictionary<DerivedCategory, HashSet<EarleyState>>();
            Predicted = new Dictionary<Rule, List<EarleyState>>(new RuleValueEquals());
            GammaStates = new List<EarleyState>();
            OldGammaStates = new List<EarleyState>();
            ActionableNonTerminalsToPredict = new Queue<DerivedCategory>();
        }

        internal CompletedStatesHeap ActionableCompleteStates { get; set; }
        internal DeletedStatesHeap ActionableDeletedStates { get; set; }

        internal Queue<EarleyState> ActionableNonCompleteStates { get; set; }
        public List<EarleyState> GammaStates { get; set; }
        public List<EarleyState> OldGammaStates { get; set; }

        public int Index { get; }
        public string Token { get; set; }

        private void SpontaneousDotShift(EarleyState state, EarleyState completedState, ContextFreeGrammar grammar)
        {
            var newState = new EarleyState(state.Rule, state.DotIndex + 1, state.StartColumn);
            state.Parents.Add(newState);
            completedState.Parents.Add(newState);
            newState.Predecessor = state;
            newState.Reductor = completedState;

            //you need to check for cyclic unit production here.
            //
            //
            //
            //


            completedState.EndColumn.AddState(newState, grammar);
        }

        public void MarkStateDeleted(EarleyState oldState, ContextFreeGrammar grammar)
        {
            StatesRemovedInLastReparse.Add(oldState);

            if (!oldState.IsCompleted)
            {
                var nextTerm = oldState.NextTerm;
                var isPOS = !grammar.StaticRules.ContainsKey(nextTerm);

                if (!isPOS && !VisitedCategoriesInUnprediction.Contains(nextTerm))
                    NonTerminalsCandidatesToUnpredict.Add(nextTerm);
            }
            else
            {
                if (oldState.Rule.LeftHandSide.ToString() == ContextFreeGrammar.GammaRule)
                {
                    oldState.EndColumn.GammaStates.Remove(oldState);
                    OldGammaStates.Add(oldState);
                    oldState.EndColumn.RemoveFromTreeDic(oldState);
                    return;
                }
            }

            foreach (var parent in oldState.Parents)
            {
                if (!parent.Removed)
                {
                    parent.Removed = true;
                    parent.EndColumn.ActionableDeletedStates.Push(parent);
                }
            }
        }

        public void DeleteState(EarleyState oldState)
        {
            
            if (oldState.Predecessor != null)
                if (!oldState.Predecessor.EndColumn.StatesRemovedInLastReparse.Contains(oldState.Predecessor))
                    //need to remove the parent edge between the predecessor to the deleted state
                    oldState.Predecessor.Parents.Remove(oldState);

            if (oldState.Reductor != null)
                if (!oldState.Reductor.EndColumn.StatesRemovedInLastReparse.Contains(oldState.Reductor))
                    //need to remove the parent edge between the reductor to the deleted state
                    oldState.Reductor.Parents.Remove(oldState);


            if (!oldState.IsCompleted)
            {
                var nextTerm = oldState.NextTerm;
                if (Predecessors.TryGetValue(nextTerm, out var predecessors))
                {
                    predecessors.Remove(oldState);
                    if (predecessors.Count == 0)
                        Predecessors.Remove(nextTerm);
                }

                if (oldState.DotIndex == 0)
                {
                    Predicted[oldState.Rule].Remove(oldState);
                    if (Predicted[oldState.Rule].Count == 0)
                        Predicted.Remove(oldState.Rule);

                }
            }
            else
            {
                CompletedStateCount--;

                if (oldState.Rule.LeftHandSide.ToString() == ContextFreeGrammar.GammaRule)
                {
                    var oldgammaStates = oldState.EndColumn.OldGammaStates;
                    oldgammaStates.Remove(oldState);
                    return;
                }

                var reductors = oldState.StartColumn.Reductors[oldState.Rule.LeftHandSide];
                reductors.Remove(oldState);
                if (reductors.Count == 0)
                    oldState.StartColumn.Reductors.Remove(oldState.Rule.LeftHandSide);
            }
        }



        //The responsibility not to add a state that already exists in the column
        //lays with the caller to AddState(). i.e, either predict, scan or complete,
        //or epsilon complete.
        public void AddState(EarleyState newState, ContextFreeGrammar grammar)
        {
            newState.Added = true;
            newState.EndColumn = this;
            StatesAddedInLastReparse.Add(newState);

            if (!newState.IsCompleted)
            {
                var term = newState.NextTerm;
                var isPOS = !grammar.StaticRules.ContainsKey(term);
                bool addTermToPredict = !isPOS;

                if (!Predecessors.TryGetValue(term, out var predecessors))
                {
                    predecessors = new HashSet<EarleyState>();
                    Predecessors.Add(term, predecessors);
                }
                else
                {
                    if (addTermToPredict)
                    {
                        foreach (var predecessor in predecessors)
                        {
                            if (!predecessor.Removed)
                            {
                                addTermToPredict = false;
                                break;
                            }
                        }
                    }
                }
                if (addTermToPredict)
                    ActionableNonTerminalsToPredict.Enqueue(term);

                predecessors.Add(newState);

                if (newState.DotIndex == 0)
                {
                    if (!Predicted.TryGetValue(newState.Rule, out var list))
                    {
                        list = new List<EarleyState>();
                        Predicted[newState.Rule] = list;
                    }
                    list.Add(newState);
                }


                //if grammar is non-lexicalized, we prepare all scannable states in advance.
                //if (isPOS && !Reductors.ContainsKey(term))
                //ActionableNonCompleteStates.Enqueue(newState);

                if (term.ToString() == ContextFreeGrammar.EpsilonSymbol)
                    if (!Reductors.TryGetValue(term, out var reductors1))
                    {
                        var epsilon = new Rule(term.ToString(), new[] { "" });
                        var epsilonState = new EarleyState(epsilon, 1, this);
                        epsilonState.EndColumn = this;
                        reductors1 = new HashSet<EarleyState> { epsilonState };
                        Reductors.Add(term, reductors1);
                    }

                if (Reductors.TryGetValue(term, out var reductors))
                    foreach (var completedState in reductors)
                    {
                        if (!completedState.Removed)
                            SpontaneousDotShift(newState, completedState, grammar);
                    }
            }
            else
            {
                CompletedStateCount++;
                ActionableCompleteStates.Enqueue(newState);
            }
        }

        public void AcceptChanges()
        {
            if (StatesAddedInLastReparse != null)
            {
                foreach (var state in StatesAddedInLastReparse)
                    state.Added = false;
                StatesAddedInLastReparse.Clear();
            }

            if (StatesRemovedInLastReparse != null)
            {
                foreach (var state in StatesRemovedInLastReparse)
                    DeleteState(state);

                StatesRemovedInLastReparse.Clear();
            }

            NonTerminalsCandidatesToUnpredict?.Clear();
            VisitedCategoriesInUnprediction?.Clear();

        }

        public void RejectChanges()
        {
            foreach (var state in StatesRemovedInLastReparse)
                state.Removed = false;

            StatesRemovedInLastReparse.Clear();

            foreach (var state in StatesAddedInLastReparse)
            {

                if (state.IsCompleted)
                {
                    if (state.Rule.LeftHandSide.ToString() == ContextFreeGrammar.GammaRule)
                    {
                        state.EndColumn.GammaStates.Remove(state);
                        state.EndColumn.RemoveFromTreeDic(state);

                    }
                    else
                    {
                        //when the grammar has been rejected due to overflowing maximum completed earley states,
                        //the relevant aborted earley states may not be written to the Reductors dic.
                        if (state.StartColumn.Reductors.ContainsKey(state.Rule.LeftHandSide))
                        {
                            state.StartColumn.Reductors[state.Rule.LeftHandSide].Remove(state);
                            if (state.StartColumn.Reductors[state.Rule.LeftHandSide].Count == 0)
                                state.StartColumn.Reductors.Remove(state.Rule.LeftHandSide);
                        }
                    }
                }
                else
                {
                    var nextTerm = state.NextTerm;
                    state.EndColumn.Predecessors[nextTerm].Remove(state);
                    if (state.EndColumn.Predecessors[nextTerm].Count == 0)
                        state.EndColumn.Predecessors.Remove(nextTerm);

                    if (state.DotIndex == 0)
                    {
                        state.EndColumn.Predicted[state.Rule].Remove(state);
                        if (state.EndColumn.Predicted[state.Rule].Count == 0)
                            state.EndColumn.Predicted.Remove(state.Rule);
                    }

                }

                if (state.Predecessor != null)
                    if (state.Predecessor.Added == false)
                        //need to remove the parent edge between the predecessor to the deleted state
                        state.Predecessor.Parents.Remove(state);

                if (state.Reductor != null)
                    if (state.Reductor.Added == false)
                        //need to remove the parent edge between the reductor to the deleted state
                        state.Reductor.Parents.Remove(state);
            }

            foreach (var state in OldGammaStates)
            {
                GammaStates.Add(state);
            }


            if (_isLastColumn)
            {
                lock (_treesDic[_textLength])
                {
                    foreach (var state in OldGammaStates)
                        _treesDic[_textLength].Add(state.BracketedRepresentation);
                }
            }
            OldGammaStates.Clear();


            VisitedCategoriesInUnprediction?.Clear();
        }

        public void Unpredict(Rule r, ContextFreeGrammar grammar, Dictionary<DerivedCategory, LeftCornerInfo> predictionSet)
        {
            if (Predicted.TryGetValue(r, out var list))
            {
                if (list.Count > 1)
                    throw new Exception("list of predicted should be at this stage 1 item only.");

                foreach (var state in list)
                {
                    if (!state.Removed)
                    {
                        state.Removed = true;
                        state.EndColumn.MarkStateDeleted(state, grammar);
                    }
                }
            }

        }


    }
}