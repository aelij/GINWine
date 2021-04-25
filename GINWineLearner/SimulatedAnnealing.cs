using GINWineParser;
using System;
using System.Collections.Generic;


namespace GINWineLearner
{
    public class SimulatedAnnealingParams
    {
        public int NumberOfRestarts { get; set; }
        public int NumberOfNonImprovingIterationsBeforeRestart { get; set; }
        public double InitialTemperature { get; set; }
        public double CoolingFactor { get; set; }
    }


    public class SimulatedAnnealing
    {
        private readonly Learner _learner;
        private readonly GrammarFitnessObjectiveFunction _objectiveFunction;
        private readonly SimulatedAnnealingParams _params;
        private const int NumberOfBestGrammarsTokeep = 20;
        private double TOLERANCE = 0.0001;


        public SimulatedAnnealing(Learner l, SimulatedAnnealingParams parameters,
            GrammarFitnessObjectiveFunction objectiveFunction)
        {
            _learner = l;
            _params = parameters;
            _objectiveFunction = objectiveFunction;
        }

        private (ContextSensitiveGrammar bestGrammar, double bestValue, bool feasible) RunSingleIteration(
            ContextSensitiveGrammar initialGrammar, double initialValue, bool initialFeasible)
        {
            var currentTemp = _params.InitialTemperature;
            var currentValue = initialValue;
            var currentGrammar = initialGrammar;
            var currentFeasible = initialFeasible;
            var finalTemp = 0.3;
            var rejectCounter = 0;
            double newValue = 0;
            bool newFeasible = false;
            // ReSharper disable once TooWideLocalVariableScope
            bool accept;
            double percentageOfConsecutiveRejectionsToGiveUp = 0.1;

            var totalIterations = (int)((Math.Log(finalTemp) - Math.Log(_params.InitialTemperature)) / Math.Log(_params.CoolingFactor));
            var numberOfConsecutiveRejectionsToGiveUp = (int)(percentageOfConsecutiveRejectionsToGiveUp * totalIterations);

            while (currentTemp > finalTemp)
            {
                //previousGrammar = currentGrammar;
                int previousParsedSentencesCount = _learner.ParsedSentences;
                var previousGrammarTreeDic = new Dictionary<int, int>();
                foreach (var kvp in _learner.GrammarTreesDic)
                    previousGrammarTreeDic[kvp.Key] = kvp.Value;
              

                var (mutatedGrammar, acceptReparse) = _learner.GetNeighborAndReparse(currentGrammar);
                if (acceptReparse)
                {
                    currentTemp *= _params.CoolingFactor;
                    (newValue, newFeasible) = _objectiveFunction.Compute(mutatedGrammar);
                    accept = _objectiveFunction.AcceptNewValue(newValue, currentValue, currentTemp);
                }
                else
                    accept = false;

                if (accept)
                {
                    rejectCounter = 0;
                    //Console.WriteLine("accepted");
                    currentValue = newValue;
                    currentGrammar = mutatedGrammar;
                    currentFeasible = newFeasible;

                    _learner.AcceptChanges();
                    if (_objectiveFunction.IsMaximalValue(currentValue))
                        break;
                }
                else
                {
                    rejectCounter++;
                    //Console.WriteLine("rejected");
                    _learner.RejectChanges();
                    _learner.GrammarTreesDic = previousGrammarTreeDic;
                    _learner.ParsedSentences = previousParsedSentencesCount;

                }

                //uncomment the following line ONLY to check that the differential parser works identically to the from-scratch parser.
                //var currentCFHypothesis1 = new ContextFreeGrammar(currentGrammar);
                //var previousHypothesis122 = new ContextFreeGrammar(previousGrammar);
                //var allParses11 = _learner.ParseAllSentencesWithDebuggingAssertion(currentCFHypothesis1, previousHypothesis122, _learner.SentencesParser);

                //after a certain number of consecutive rejections, give up, reheat system.
                if (rejectCounter > numberOfConsecutiveRejectionsToGiveUp) break;
            }

            PruneUnusedRules(currentGrammar);
            //the number of grammar trees can change after pruning unused rules, thus changing the objective function value.
            (currentValue, currentFeasible) = _objectiveFunction.Compute(currentGrammar);

            return (currentGrammar, currentValue, currentFeasible);
        }

        private void PruneUnusedRules(ContextSensitiveGrammar currentGrammar)
        {
            var ruleDistribution = _learner.CollectUsages();
            currentGrammar.PruneUnusedRules(ruleDistribution);
            //after pruning unused rules, parse from scratch in order to remove
            //all resultant unused earley items (i.e, all items using those unused rules
            //that are a part of partial, unsuccessful, derivation)
            _learner.ParseAllSentencesFromScratch(currentGrammar);
        }
        public (ContextSensitiveGrammar bestGrammar, double bestValue, bool feasible) Run(bool isCFGGrammar,
            ContextSensitiveGrammar initiaGrammar = null)
        {
            double currentValue;
            bool feasible;
            ContextSensitiveGrammar currentGrammar;

            if (_learner.Gp == null)
                _learner.Gp = new GrammarPermutations(isCFGGrammar);
            
            var bestGrammars = new BestGrammarsQueue();
            var promiscuousGrammar = _learner.CreatePromiscuousGrammar(isCFGGrammar);
            _learner.ParseAllSentencesFromScratch(promiscuousGrammar);
            (var promiscuousValue, var promiscuousFeasible) = _objectiveFunction.Compute(promiscuousGrammar);
            BestGrammarsKey promiscuousKey = new BestGrammarsKey(promiscuousValue, promiscuousFeasible);
         
            if (initiaGrammar != null)
            {
                currentGrammar = new ContextSensitiveGrammar(initiaGrammar);
                _learner.ParseAllSentencesFromScratch(currentGrammar);
                (currentValue, feasible) = _objectiveFunction.Compute(currentGrammar);
                BestGrammarsKey currentKey = new BestGrammarsKey(currentValue, feasible);
                bestGrammars.Insert((currentKey, currentGrammar));
            }
            else
            {
                currentGrammar = promiscuousGrammar;
                currentValue = promiscuousValue;
                feasible = promiscuousFeasible;
            }


            //LogManager.GetCurrentClassLogger().Info($"BEFORE iterations, objective function value {currentValue} (feasible: {feasible})");

            //if current grammar is already optimal on data, no need to learn anything,
            //return immediately.
            if (feasible && _objectiveFunction.IsMaximalValue(currentValue))
                return (currentGrammar, currentValue, true);

            int noImprovementCounter;

            bool foundMaxSolution = false;
            //int currentIteration = 0;
            for (int i = 0; i < _params.NumberOfRestarts; i++)
            {
                noImprovementCounter = 0;
                var maxKey = new BestGrammarsKey(0.0, false);
                ContextSensitiveGrammar maxGrammar = null;
                while (noImprovementCounter < _params.NumberOfNonImprovingIterationsBeforeRestart)
                {
                    (currentGrammar, currentValue, feasible) = RunSingleIteration(currentGrammar, currentValue, feasible);
                    //LogManager.GetCurrentClassLogger().Info($"iteration {currentIteration++}, objective function value {currentValue} (feasible: {feasible})");
                    var currentKey = new BestGrammarsKey(currentValue, feasible);
                    if (maxKey.Key < currentKey.Key)
                    {
                        maxKey = currentKey;
                        maxGrammar = currentGrammar;
                        noImprovementCounter = 0;
                    }
                    else
                        noImprovementCounter++;

                    if (_objectiveFunction.IsMaximalValue(currentValue))
                    {
                        foundMaxSolution = true;
                        bestGrammars.Insert((currentKey, currentGrammar));
                        if (feasible)
                        {
                            _objectiveFunction.PenaltyCoefficient = 1;
                            break;
                        }
                        else
                        {
                            throw new Exception("maximal value that is infeasible. contradiction by definition");
                            //_objectiveFunction.PenaltyCoefficient += 1;
                        }
                    }
                }

                
                if (!bestGrammars.ContainsKey(maxKey))
                {
                    
                    var smallestBestValue = bestGrammars.FindMinKey();
                    if (smallestBestValue.Key < maxKey.Key)
                        bestGrammars.Insert((maxKey, maxGrammar));
                    else
                        bestGrammars.Insert((promiscuousKey, promiscuousGrammar));
                }
                else
                    bestGrammars.Insert((promiscuousKey,promiscuousGrammar));

                if (bestGrammars.Count > NumberOfBestGrammarsTokeep)
                    bestGrammars.RemoveMin();

                var item = bestGrammars.Next();
                currentGrammar = new ContextSensitiveGrammar(item.Item2);
                currentValue = item.Item1.ObjectiveFunctionValue;
                _objectiveFunction.PenaltyCoefficient = 1;

                //refresh parse forest from scratch, because we moved to an arbitrarily far away point
                //in the hypotheses space.
                _learner.ParseAllSentencesFromScratch(currentGrammar);
                //PruneUnusedRules(currentGrammar);

                if (foundMaxSolution)
                    break;

            }

            var max = bestGrammars.FindMax();
            //the loop can occur if the max is the feasible promiscuous grammar,
            //(having a very low objective function value).
            //every feasible grammar is ranked higher than non-feasible ones (that might have higher objective function value)
            while (max.Item1.ObjectiveFunctionValue <= promiscuousValue + TOLERANCE)
            {
                if (bestGrammars.Count > 1)
                {
                    bestGrammars.RemoveMax();
                    max = bestGrammars.FindMax();
                }
                else
                    break;
            }
            

            currentValue = max.Item1.ObjectiveFunctionValue;
            feasible = max.Item1.Feasible;

            //LogManager.GetCurrentClassLogger().Info($"Value of the objective function of the last key in best grammars: {currentValue} and its feasibility { bestGrammars.Last().Key.feasible} ");
            currentGrammar = new ContextSensitiveGrammar(max.Item2);
            _learner.ParseAllSentencesFromScratch(currentGrammar);

            return (currentGrammar, currentValue, feasible);
        }
    }
}