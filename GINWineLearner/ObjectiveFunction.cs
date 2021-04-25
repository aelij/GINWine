using GINWineParser;
using System;
using System.Collections.Generic;

namespace GINWineLearner
{

    public class GrammarFitnessObjectiveFunction
    {
        public const double RoundingErrorTolerance = 0.0001;

        //currently PenaltyCoefficient (augemented lagrangian) is not used.
        public int PenaltyCoefficient { get; set; }
        public double NoiseTolerance { get; set; }

        private static readonly double[] Uniform =
        {
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1
        };

        private static double _maxVal;
        private readonly Learner _learner;

        public GrammarFitnessObjectiveFunction(Learner l, double noiseTolerance)
        {
            PenaltyCoefficient = 1;
            NoiseTolerance = noiseTolerance;
            _learner = l;
            _maxVal = 1.0;
        }

        public Learner GetLearner() => _learner;
        public void SetMaximalValue(double val) => _maxVal = val;

        public bool AcceptNewValue(double newValue, double oldValue, double temperature)
        {
            if (newValue > oldValue) return true; //any positive improvement - accept.

            //neval =< oldValue (our objective function is to maximize value)
            //degradation - accept with a probability proportional to the delta and the iteration
            //bigger delta (bigger degradation) => lower probability.
            //bigger temperature => higher probability
            var exponent = 100 * (Math.Log(newValue) - Math.Log(oldValue)) / temperature;
            var prob = Math.Exp(exponent);
            var randomThrow = Pseudorandom.NextDouble();

            return randomThrow < prob;
        }

        public bool IsMaximalValue(double val)
        {
            if (val >= _maxVal) return true;
            return Math.Abs(val - _maxVal) < RoundingErrorTolerance;
        }
        private Dictionary<int, int> ComputeDataTrees(Dictionary<int, HashSet<string>> treesDic)
        {

            var dataTreesPerLength = new Dictionary<int, int>();
            int sum = 0;
            foreach (var length in treesDic.Keys)
            {
                dataTreesPerLength[length] = treesDic[length].Count;
                sum+= treesDic[length].Count; 
            }
            if (sum == 0) dataTreesPerLength = null;

            return dataTreesPerLength;
        }

        public (double val, bool feasible) Compute(ContextSensitiveGrammar currentHypothesis)
        {
            var probabilityMassOfLength = Uniform;
            double unparsedSentencesRatio, unexplainedSentenceRatio = 0;
            if (currentHypothesis == null) return (0, false);

            //checking in ReparseWith Addition/ ReparseWithDeletion / ParseFromScratch
            //if (currentCFHypothesis.ContainsCyclicUnitProduction())
            //    throw new Exception(
            //        "Cyclic Unit Production Encountered at unexpected place, in preparation to remove the check for cyclic");

            double prob = 0;

            var allParses = _learner.Parses;
            Dictionary<int, int> grammarTreesPerLength = _learner.GrammarTreesDic;
            Dictionary<int, int> dataTreesPerLength;
            dataTreesPerLength = ComputeDataTrees(_learner.TreesDic);
       

            if (dataTreesPerLength != null)
            {

                    prob = 1;
                    double totalProbabilityOfGrammarTrees = 0;
                    foreach (var length in grammarTreesPerLength.Keys)
                        totalProbabilityOfGrammarTrees += probabilityMassOfLength[length];

                    foreach (var length in grammarTreesPerLength.Keys)
                    {
                        dataTreesPerLength.TryGetValue(length, out var dataTreesInLength);
                        var allGrammarTreesInLength = grammarTreesPerLength[length];

                        var diff = allGrammarTreesInLength - dataTreesInLength;
                        if (diff > 0)
                            prob -= diff / (double)allGrammarTreesInLength * probabilityMassOfLength[length] /
                                    totalProbabilityOfGrammarTrees;

                    }

                    if (prob > 1)
                    {
                        return (0, false);
                        //the case where probabilityOfInputGivenGrammar > 1 arises when
                        //totalTreesCountofData > totalTreesCountofGrammar, which can happen because totalTreesCountofGrammar
                        //is computed only up to a certain depth of the tree.
                        //so it's possible that the input data is parsed in a tree whose depth exceeds the depth we have allowed above.

                        //assumption: we will reject grammars with data parsed too deep.
                        //discuss: what is the upper bound of tree depth as a function of the number of words in the sentence?
                        //right now: it is depth = maxWords+3. change?
                    
                }

                unparsedSentencesRatio = 1.0 - (_learner.ParsedSentences / (double)allParses.Length);

                unexplainedSentenceRatio = unparsedSentencesRatio - NoiseTolerance > RoundingErrorTolerance ? unparsedSentencesRatio - NoiseTolerance : 0;

                prob *= 1 - unexplainedSentenceRatio;// * PenaltyCoefficient;
                if (prob < 0) prob = 0;
            }

            return (prob, unexplainedSentenceRatio == 0);
        }
    }
}