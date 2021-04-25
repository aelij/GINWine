using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GINWineParser
{
    public class GrammarFileReader
    {

        public static string[][] GetSentencesInWordLengthRange(
            string[][] allData, int minWords, int maxWords)
        {
            var sentences = new List<string[]>();

            foreach (var arr in allData)
            {
                if (arr.Length > maxWords || arr.Length < minWords) continue;
                sentences.Add(arr);
            }

            return sentences.ToArray();
        }

        public static (List<EarleyState> nodeList, List<Rule> grammarRules) GenerateSentenceAccordingToGrammar(
            string filename, string vocabularyFilename, int maxWords)
        {
            var universalVocabulary = Vocabulary.ReadVocabularyFromFile(vocabularyFilename);
            ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys
                .Select(x => new SyntacticCategory(x)).ToHashSet();

            var rules = ReadRulesFromFile(filename);
            var cfGrammar = new ContextFreeGrammar(rules);

            var generator = new EarleyGenerator(cfGrammar, universalVocabulary);

            var statesList = generator.GenerateSentence(null, maxWords);
            return (statesList, rules);
        }

        public static List<EarleyState> ParseSentenceAccordingToGrammar(string filename, string vocabularyFilename,
            string sentence)
        {
            var universalVocabulary = Vocabulary.ReadVocabularyFromFile(vocabularyFilename);
            ContextFreeGrammar.PartsOfSpeech = universalVocabulary.POSWithPossibleWords.Keys
                .Select(x => new SyntacticCategory(x)).ToHashSet();

            var rules = ReadRulesFromFile(filename);
            var cfGrammar = new ContextFreeGrammar(rules);

            var parser = new EarleyParser(cfGrammar, universalVocabulary, sentence.Split());
            var treesDic = new Dictionary<int, HashSet<string>>();
            parser.ParseSentence(treesDic);

            return parser.GetGammaStates();
        }


        private static DerivedCategory CreateDerivedCategory(string s)
        {
            var pattern = new Regex(@"(?<BaseCategory>\w*)(\[(?<Stack>[\w\*]*)\])?");
            var match = pattern.Match(s);
            return new DerivedCategory(match.Groups["BaseCategory"].Value, match.Groups["Stack"].Value);
        }

        public static List<Rule> ReadRulesFromFile(string filename)
        {
            string line;
            var comment = '#';

            var rules = new List<Rule>();
            using (var file = File.OpenText(filename))
            {
                while ((line = file.ReadLine()) != null)
                {
                    
                    if (line[0] == comment) continue;
                    int found = line.IndexOf(". ");
                    if (found >= 0)
                        line = line.Substring(found + 2);
                    var r = CreateRule(line);
                    if (r != null)
                        rules.Add(r);
                }
            }

            return rules;
        }

        private static Rule CreateRule(string s)
        {
            var removeArrow = s.Replace("->", "");

            //string formatted incorrectly. (no "->" found).
            if (s == removeArrow) return null;

            var nonTerminals = removeArrow.Split().Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p))
                .ToArray();

            var leftHandCat = CreateDerivedCategory(nonTerminals[0]);
            var popRule = false;
            //var pushRule = false;
            //SyntacticCategory moveable = null;
            //var key = MoveableOperationsKey.NoOp;

            if (leftHandCat.Stack.Contains(ContextFreeGrammar.StarSymbol))
                if (leftHandCat.Stack.Length > 1)
                    popRule = true;

            if (nonTerminals.Length == 1)
            {
                var epsilonCat = new DerivedCategory(ContextFreeGrammar.EpsilonSymbol) { StackSymbolsCount = -1 };
                //moveable = new SyntacticCategory(leftHandCat);
                return new Rule(leftHandCat, new[] { epsilonCat });

                //key = MoveableOperationsKey.Pop1;
                //return new StackChangingRule(baseRule, key, moveable);
            }

            var rightHandCategories = new DerivedCategory[nonTerminals.Length - 1];
            for (var i = 1; i < nonTerminals.Length; i++)
            {
                rightHandCategories[i - 1] = CreateDerivedCategory(nonTerminals[i]);
                if (rightHandCategories[i - 1].Stack.Contains(ContextFreeGrammar.StarSymbol))
                {
                    if (rightHandCategories[i - 1].Stack.Length > 1)
                    {
                        //pushRule = true;
                        //push rule.
                        //moveable = new SyntacticCategory(rightHandCategories[i - 1].Stack.Substring(1));
                        //key = MoveableOperationsKey.Push1;
                        rightHandCategories[i - 1].StackSymbolsCount = 1;
                        if (popRule)
                            throw new Exception("illegal LIG format: can't push and pop within the same rule");
                    }
                    else
                    {
                        if (popRule)
                            rightHandCategories[i - 1].StackSymbolsCount = -1;
                    }
                }
            }

            return new Rule(leftHandCat, rightHandCategories);
        }
    }
}