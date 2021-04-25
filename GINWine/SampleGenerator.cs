using GINWineParser;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GINWine
{
    class SampleGenerator
    {

        static int[] GetMinimumNumberOfSamples()
        {
            int[] bounds =
{
                0,
                200,
                200,
                200,
                200,
                200,
                200,
                200,
                200,
                200,
                200,
                200,
                200,
            };

            return bounds;
        }


        static private double F(float x, float mean, float var)
        {
            return (Math.Exp(-(x - mean) * (x - mean) / (2 * var)));
        }

        static int[] PreparePowerLawDistribution(int numberOfStates, int startVal, int minTreesToHear)
        {
            int[] powerlaw = new int[numberOfStates];
            if (startVal < 1)
                startVal = 1;

            for (int i = 0; i < numberOfStates; i++)
            {
                powerlaw[i] = startVal / (i + 1);   //if  i > startVal, powerlaw[i] = 0.
                //also: always hear the first minTreesToHear trees in non-zero probability.
                if (i < minTreesToHear && powerlaw[i] == 0)
                    powerlaw[i] = 1;
            }
            return powerlaw;
        }

        static int[] PrepareNormalDistribution(int numberOfStates)
        {
            int[] normal = new int[numberOfStates];

            float mean = numberOfStates / 2.0f;
            float stddev = numberOfStates / 5.0f;
            float var = stddev * stddev;

            for (int i = 0; i < numberOfStates; i++)
                normal[i] = (int)(100 * F(i, mean, var));

            return normal;
        }
        static int[] PrepareUniformDistribution(int numberOfStates)
        {
            int[] uniform = new int[numberOfStates];
            for (int i = 0; i < numberOfStates; i++)
                uniform[i] = 1;

            return uniform;
        }

        static (string[][] sentences, Vocabulary textVocabulary) DrawSamples(List<EarleyState>[] allGrammarStates, int[] bounds, HashSet<string> pos, Vocabulary universalVocabulary, DistributionType distType)
        {
            var sentences = new List<string[]>();
            var textVocabulary = new Vocabulary();
            int[] distribution;
            for (int i = 1; i < allGrammarStates.Length; i++)
            {
                if (allGrammarStates[i].Count > 0)
                {
                    if (distType == DistributionType.Uniform)
                        distribution = PrepareUniformDistribution(allGrammarStates[i].Count);
                    else if (distType == DistributionType.Normal)
                        distribution = PrepareNormalDistribution(allGrammarStates[i].Count);
                    else if (distType == DistributionType.PowerLaw)
                    {
                        //
                        double percentOfObservedStatesForPowerLaw = 1.0;
                        int minimumNumberOfTreesToHear = 5;
                        distribution = PreparePowerLawDistribution(allGrammarStates[i].Count, (int)(allGrammarStates[i].Count * percentOfObservedStatesForPowerLaw), minimumNumberOfTreesToHear);
                    }
                    else
                    {
                        throw new Exception("unrecognized distribution Type");
                    }

                    (var sentencesOfLength, var posCategories) = DrawSampleFromStatesWithLengthK(allGrammarStates[i], distribution, bounds[i], pos, universalVocabulary);
                    for (int j = 0; j < sentencesOfLength.Length; j++)
                        sentences.Add(sentencesOfLength[j]);

                    foreach (var category in posCategories)
                    {
                        if (!textVocabulary.ContainsPOS(category))
                        {
                            textVocabulary.AddWordsToPOSCategory(category,
                                universalVocabulary.POSWithPossibleWords[category].ToArray());
                        }
                    }

                    //AddNoise(sentences, textVocabulary, i, sentencesOfLength);
                }
            }

            return (sentences.ToArray(), textVocabulary);
        }

/*
        private static void AddNoise(List<string[]> sentences, Vocabulary textVocabulary, int i, string[][] sentencesOfLength, double noiseSentencesRatio = 0.1)
        {
            //add noise
            var allWords = textVocabulary.WordWithPossiblePOS.Keys.ToArray();
            
            int noiseSentencesCount = (int)(sentencesOfLength.Length * noiseSentencesRatio);
            for (int j = 0; j < noiseSentencesCount; j++)
            {
                var sentence = new string[i];
                for (int k = 0; k < i; k++)
                {
                    var wordIndex = Pseudorandom.NextInt(textVocabulary.WordWithPossiblePOS.Keys.Count);
                    sentence[k] = allWords[wordIndex];
                }
                sentences.Add(sentence);
            }
        }
*/

        static (string[][] sentences, HashSet<string> posCategories) DrawSampleFromStatesWithLengthK(List<EarleyState> states, int[] distributionOverStates, int numberOfSamples, HashSet<string> pos, Vocabulary universalVocabulary)
        {
            var rand = new Random();
            var posCategories = new HashSet<string>();
            var sentences = new string[numberOfSamples][];

            for (int j = 0; j < numberOfSamples; j++)
            {
                int k = DrawStateIndexAccordingToDistribution(distributionOverStates);
                var selectedState = states[k];
                var nonterminalSentence = selectedState.GetNonTerminalStringUnderNode(pos);
                var arr = nonterminalSentence.Split();
                var sentence = new string[arr.Length];
                for (var i = 0; i < sentence.Length; i++)
                {
                    var posCat = arr[i];
                    posCategories.Add(posCat);
                    //you can improve here - do not repeatedly call toARRAY.
                    var possibleWords = universalVocabulary.POSWithPossibleWords[posCat].ToArray();
                    sentence[i] = possibleWords[rand.Next(possibleWords.Length)];

                }
                sentences[j] = sentence;
            }
            return (sentences, posCategories);
        }

        private static int DrawStateIndexAccordingToDistribution(int[] distributionOverStates)
        {
            int totalWeight = 0;
            for (int i = 0; i < distributionOverStates.Length; i++)
                totalWeight += distributionOverStates[i];

            var r = Pseudorandom.NextInt(totalWeight);

            var sum = 0;
            for (int i = 0; i < distributionOverStates.Length; i++)
            {
                if (sum + distributionOverStates[i] > r)
                    return i;
                sum += distributionOverStates[i];
            }

            throw new Exception("should never arrive here!");

        }

        public static (string[][] data, Vocabulary dataVocabulary) PrepareDataFromTargetGrammar(
            List<Rule> grammarRules, Vocabulary universalVocabulary, int maxWords, DistributionType distType)
        {
            var pos = universalVocabulary.POSWithPossibleWords.Keys.ToHashSet();

            var generator = new EarleyGenerator(new ContextFreeGrammar(grammarRules), universalVocabulary);
            var statesList = generator.GenerateSentence(null, maxWords);

            var allGrammarStates = new List<EarleyState>[maxWords + 1];
            for (int i = 0; i < maxWords + 1; i++)
                allGrammarStates[i] = new List<EarleyState>();

            foreach (var state in statesList)
                allGrammarStates[state.EndColumn.Index].Add(state);

            var bounds = GetMinimumNumberOfSamples();

            return DrawSamples(allGrammarStates, bounds, pos, universalVocabulary, distType);
        }
    }
}
