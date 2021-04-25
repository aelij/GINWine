using GINWineParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GINWineLearner
{
    public class Learner
    {
        private readonly int _maxWordsInSentence;
        private readonly int _minWordsInSentence;

        private readonly HashSet<string> _posInText;
        public readonly EarleyParser[] SentencesParser;
        private readonly Vocabulary _voc;

        // ReSharper disable once NotAccessedField.Local
        public GrammarPermutations Gp;
        public GrammarTreeCountsCalculator GrammarTreesCalculator;
        public Dictionary<int, HashSet<string>> TreesDic { get; set; }
        public int ParsedSentences { get; set; }
        public Dictionary<int, int> GrammarTreesDic { get; set; }
        public Learner(string[][] sentences, int minWordsInSentence, int maxWordsInSentence,
            Vocabulary dataVocabulary)
        {
            _voc = dataVocabulary;
            _posInText = dataVocabulary.POSWithPossibleWords.Keys.ToHashSet();
            _maxWordsInSentence = maxWordsInSentence;
            _minWordsInSentence = minWordsInSentence;

            var dict = sentences.GroupBy(x => string.Join(" ", x)).ToDictionary(g => g.Key, g => g.Count());

            Parses = new SentenceParsingResults[dict.Count];
            var arrayOfDesiredVals = dict.Select(x => (x.Key, x.Value)).ToArray();

            GrammarTreesCalculator =
                new GrammarTreeCountsCalculator(_posInText, _maxWordsInSentence);
            SentencesParser = new EarleyParser[Parses.Length];

            for (var i = 0; i < Parses.Length; i++)
            {
                Parses[i] = new SentenceParsingResults();
                Parses[i].Sentence = arrayOfDesiredVals[i].Key.Split();
                Parses[i].Count = arrayOfDesiredVals[i].Value;
                Parses[i].Length = Parses[i].Sentence.Length;
            }
        }

        public SentenceParsingResults[] Parses { get; }

        ////We create the "promiscuous grammar" as initial grammar.
        public ContextSensitiveGrammar CreatePromiscuousGrammar(bool isCFGGrammar)
        {
            var rules = new List<Rule>();
            foreach (var pos in _posInText)
            {
                //rules.Add(new Rule("X1", new[] { "X1", pos }));
                rules.Add(new Rule("X1", new[] { pos, "X1" }));
                rules.Add(new Rule("X1", new[] { pos }));
            }

            rules.Add(new Rule(ContextFreeGrammar.StartSymbol, new[] { "X1" }));

            var originalGrammar = new ContextSensitiveGrammar(rules);
            return originalGrammar;
        }


        //the difference between ParseAllSentencesFromScratch and ParseAllSentence is that the
        //in the former we keep using the _sentencesParser parsers.
        public void ParseAllSentencesFromScratch(ContextSensitiveGrammar currentHypothesis)
        {
            var currentCFGHypothesis = new ContextFreeGrammar(currentHypothesis);

            if (currentCFGHypothesis.ContainsCyclicUnitProduction())
                throw new Exception("initial grammar should not contain cyclic unit productions");

            for (var i = 0; i < SentencesParser.Length; i++)
                SentencesParser[i] =
                    new EarleyParser(currentCFGHypothesis, _voc, Parses[i].Sentence,
                        false); //parser does not check for cyclic unit productions

            InitTreesDictionary();
            bool[] parsed = new bool[SentencesParser.Length];
            Parallel.Invoke(
                () =>
                {
                    Parallel.For(0, SentencesParser.Length,
                        (i) => { parsed[i] = SentencesParser[i].ParseSentence(TreesDic); });
                },
                () => { GrammarTreesDic = GetGrammarTrees(currentCFGHypothesis); }
            );

            RecalculateParsedSentences(parsed);

            AcceptChanges();
        }

        private void InitTreesDictionary()
        {
            TreesDic = new Dictionary<int, HashSet<string>>();
            for (int i = _minWordsInSentence; i <= _maxWordsInSentence; i++)
                TreesDic[i] = new HashSet<string>();
        }


        public void SetOriginalGrammarBeforePermutation()
        {
            for (var i = 0; i < Parses.Length; i++)
                SentencesParser[i].OldGrammar = SentencesParser[i].Grammar;
        }


        public bool ReparseWithAddition(ContextSensitiveGrammar currentHypothesis, int numberOfGeneratingRule)
        {
            var currentCFGHypothesis = new ContextFreeGrammar(currentHypothesis);

            if (currentCFGHypothesis.ContainsCyclicUnitProduction())
            {
                //Console.WriteLine("ContainsCyclicUnitProduction in ReparseWithAddition ");
                return false;
            }

            var rs = new List<Rule>();
            foreach (var ruleList in currentCFGHypothesis.StaticRules.Values)
            {
                foreach (var rule in ruleList)
                {
                    if (rule.NumberOfGeneratingRule == numberOfGeneratingRule)
                        rs.Add((rule));
                }
            }

            if (rs.Count == 0)
            {
                //Console.WriteLine($"added ");

                //rs.Count == 0 when the new rule is unreachable from the existing set of rules.
                //that means that the parser earley items are exactly the same as before.
                //we can return immediately with no change.
                return true;
            }

            int acceptedSum = 0;
            int[] accepted = new int[SentencesParser.Length];
            bool[] parsed = new bool[SentencesParser.Length];
            Parallel.Invoke(
                () =>
                {
                    Parallel.For(0, SentencesParser.Length,
                        (i) =>
                        {
                            (accepted[i], parsed[i]) = SentencesParser[i].ReParseSentenceWithRuleAddition(currentCFGHypothesis, rs);
                        });
                },
                () => { GrammarTreesDic = GetGrammarTrees(currentCFGHypothesis); }
            );
            for (int i = 0; i < SentencesParser.Length; i++)
                acceptedSum += accepted[i];

            RecalculateParsedSentences(parsed);

            if (acceptedSum != 0) return false; // a parse was rejected.

            return true;
        }

        public bool ReparseWithDeletion(ContextSensitiveGrammar currentHypothesis, int numberOfGeneratingRule)
        {
            var currentCFGHypothesis = new ContextFreeGrammar(currentHypothesis);

            if (currentCFGHypothesis.ContainsCyclicUnitProduction())
            {
                //Console.WriteLine("ContainsCyclicUnitProduction in ReparseWithDeletion ");
                return false;
            }

            var rulesExceptDeletedRule =
                new Dictionary<DerivedCategory, List<Rule>>();

            var deletedRule = new List<Rule>();
            foreach (var kvp in SentencesParser[0].Grammar.StaticRules)
            {
                rulesExceptDeletedRule[kvp.Key] = new List<Rule>();

                foreach (var r in kvp.Value)
                {
                    if (r.NumberOfGeneratingRule == numberOfGeneratingRule)
                        deletedRule.Add(r);
                    else
                        rulesExceptDeletedRule[kvp.Key].Add(r);
                }
            }

            var leftCorner = new LeftCorner();
            var predictionSet = leftCorner.ComputeLeftCorner(rulesExceptDeletedRule);
            bool[] parsed = new bool[SentencesParser.Length];

            Parallel.Invoke(
                () =>
                {


                    Parallel.For(0, SentencesParser.Length,
                        (i) =>
                        {
                            parsed[i] =
                            SentencesParser[i].ReParseSentenceWithRuleDeletion(currentCFGHypothesis, deletedRule, predictionSet);
                        });
                },
                () => { GrammarTreesDic = GetGrammarTrees(currentCFGHypothesis); }
            );

            RecalculateParsedSentences(parsed);

            return true;
        }

        private void RecalculateParsedSentences(bool[] parsed)
        {
            ParsedSentences = 0;
            for (int i = 0; i < SentencesParser.Length; i++)
                if (parsed[i]) ParsedSentences++;
        }

        public SentenceParsingResults[] ParseAllSentencesWithDebuggingAssertionForTreesDic(ContextFreeGrammar currentHypothesis, ContextFreeGrammar previousHypothesis,
            EarleyParser[] diffparsers = null)
        {
            var sentencesWithCounts = new SentenceParsingResults[Parses.Length];

            for (var i = 0; i < Parses.Length; i++)
            {
                sentencesWithCounts[i] = new SentenceParsingResults();
                sentencesWithCounts[i].Sentence = Parses[i].Sentence;
                sentencesWithCounts[i].Count = Parses[i].Count;
                sentencesWithCounts[i].Length = Parses[i].Length;
            }

            var parsers = new EarleyParser[Parses.Length];
            for (var i = 0; i < parsers.Length; i++)
                parsers[i] =
                    new EarleyParser(currentHypothesis, _voc, Parses[i].Sentence,
                        false); //parser does not check for cyclic unit production, you have guaranteed it before (see Objective function).

            var tempTreeDic = new Dictionary<int, HashSet<string>>();
            for (int i = _minWordsInSentence; i <= _maxWordsInSentence; i++)
                tempTreeDic[i] = new HashSet<string>();


            Parallel.For(0, SentencesParser.Length,
                       (i) => parsers[i].ParseSentence(tempTreeDic));



            if (diffparsers != null)
                for (var i = 0; i < diffparsers.Length; i++)
                {
                    var actual = diffparsers[i].ToString();
                    var expected = parsers[i].ToString();
                    if (actual != expected)
                    {
                        var grammar = parsers[i].Grammar.ToString();
                        NLog.LogManager.GetCurrentClassLogger().Info($"Actual:\r\n {actual}");
                        NLog.LogManager.GetCurrentClassLogger().Info($"Expected:\r\n {expected}");
                        NLog.LogManager.GetCurrentClassLogger().Info($"Grammar in parser: {grammar}");
                        //NLog.LogManager.GetCurrentClassLogger().Info($"Grammar in currentHypothesis: {currentHypothesis}");
                        //NLog.LogManager.GetCurrentClassLogger().Info($"Grammar in currentHypothesis: {previousHypothesis}");

                        throw new Exception("actual parse differs from expected parse");
                    }
                }


            return sentencesWithCounts;
        }


        public SentenceParsingResults[] ParseAllSentencesWithDebuggingAssertion(ContextFreeGrammar currentHypothesis, ContextFreeGrammar previousHypothesis,
            EarleyParser[] diffparsers = null)
        {
            var sentencesWithCounts = new SentenceParsingResults[Parses.Length];

            for (var i = 0; i < Parses.Length; i++)
            {
                sentencesWithCounts[i] = new SentenceParsingResults();
                sentencesWithCounts[i].Sentence = Parses[i].Sentence;
                sentencesWithCounts[i].Count = Parses[i].Count;
                sentencesWithCounts[i].Length = Parses[i].Length;
            }

            var parsers = new EarleyParser[Parses.Length];
            for (var i = 0; i < parsers.Length; i++)
                parsers[i] =
                    new EarleyParser(currentHypothesis, _voc, Parses[i].Sentence,
                        false); //parser does not check for cyclic unit production, you have guaranteed it before (see Objective function).

            var tempTreeDic = new Dictionary<int, HashSet<string>>();
            for (int i = _minWordsInSentence; i <= _maxWordsInSentence; i++)
                tempTreeDic[i] = new HashSet<string>();

            //Parallel.ForEach(sentencesWithCounts,
            //    (sentenceItem, loopState, i) =>
            //    {
            //        parsers[i].ParseSentence(tempTreeDic);
            //    });

            for (int i = 0; i < sentencesWithCounts.Length; i++)
            {
                parsers[i].ParseSentence(tempTreeDic);
            }


            if (diffparsers != null)
                for (var i = 0; i < diffparsers.Length; i++)
                {
                    var actual = diffparsers[i].ToString();
                    var expected = parsers[i].ToString();
                    if (actual != expected)
                    {
                        var grammar = parsers[i].Grammar.ToString();
                        NLog.LogManager.GetCurrentClassLogger().Info($"Actual: {actual}");
                        NLog.LogManager.GetCurrentClassLogger().Info($"Expected: {expected}");
                        NLog.LogManager.GetCurrentClassLogger().Info($"Grammar in parser: {grammar}");
                        NLog.LogManager.GetCurrentClassLogger().Info($"Grammar in currentHypothesis: {currentHypothesis}");
                        NLog.LogManager.GetCurrentClassLogger().Info($"Grammar in currentHypothesis: {previousHypothesis}");

                        throw new Exception("actual parse differs from expected parse");
                    }
                }


            return sentencesWithCounts;
        }
        
        public Dictionary<int, int> GetGrammarTrees(ContextFreeGrammar hypothesis)
        {
            var res = GrammarTreesCalculator.Recalculate(hypothesis);
            var grammarTreesPerLength = new Dictionary<int, int>();
            for (var i = 0; i < res.Length; i++)
                if (i <= _maxWordsInSentence && i >= _minWordsInSentence && res[i] > 0)
                    grammarTreesPerLength[i] = res[i];

            return grammarTreesPerLength;
        }


        public Dictionary<int, int> CollectUsages()
        {
            var usagesDic = new Dictionary<int, int>();

            if (Parses != null)
            {
                for (int i = 0; i < Parses.Length; i++)
                {
                    foreach (var gammaState in SentencesParser[i].GetGammaStates())
                        CollectRuleUsages(gammaState, usagesDic, Parses[i].Count);
                }

                return usagesDic;
            }

            Console.WriteLine("returning usages dic null. meaning that all parses are zero.");
            return null;
        }

        private static void CollectRuleUsages(EarleyState state, Dictionary<int, int> ruleCounts, int sentenceCount)
        {
            if (state.Predecessor != null)
                CollectRuleUsages(state.Predecessor, ruleCounts, sentenceCount);

            if (state.Reductor != null)
                CollectRuleUsages(state.Reductor, ruleCounts, sentenceCount);

            var ruleNumber = state.Rule.NumberOfGeneratingRule;
            if (ruleNumber != 0) //SCAN_RULE_NUMBER = 0.
            {
                if (!ruleCounts.ContainsKey(ruleNumber)) ruleCounts[ruleNumber] = 0;
                ruleCounts[ruleNumber] += sentenceCount;
                //add +1 to the count of the rule, multiplied by the number of times the sentence appears in the text (sentenceCount).
            }
        }

        internal (ContextSensitiveGrammar mutatedGrammar, bool acceptReparse) GetNeighborAndReparse(
            ContextSensitiveGrammar currentHypothesis)
        {
            //choose mutation function in random (weighted according to weights file)
            var m = GrammarPermutations.GetWeightedRandomMutation();
            var newGrammar = new ContextSensitiveGrammar(currentHypothesis);

            //mutate the grammar.
            SetOriginalGrammarBeforePermutation();
            bool acceptReparse = m(newGrammar, this);
            return (newGrammar, acceptReparse);
        }


        public void AcceptChanges()
        {
             Parallel.For(0, SentencesParser.Length,
                 (i) => SentencesParser[i].AcceptChanges());
        }

        public void RejectChanges()
        {
            Parallel.For(0, SentencesParser.Length,
                (i) => SentencesParser[i].RejectChanges());
        }
    }
}