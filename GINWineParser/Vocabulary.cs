using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GINWineParser
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Vocabulary
    {
        public Vocabulary()
        {
            WordWithPossiblePOS = new Dictionary<string, HashSet<string>>();
            POSWithPossibleWords = new Dictionary<string, HashSet<string>>();
        }

        // key = word, value = possible POS
        public Dictionary<string, HashSet<string>> WordWithPossiblePOS { get; set; }

        // key = POS, value = words having the same POS.
        [JsonProperty] public Dictionary<string, HashSet<string>> POSWithPossibleWords { get; set; }

        public HashSet<string> this[string word]
        {
            get
            {
                if (WordWithPossiblePOS.ContainsKey(word))
                    return WordWithPossiblePOS[word];
                return null;
            }
        }

        public static Vocabulary ReadVocabularyFromFile(string jsonFileName)
        {
            Vocabulary voc;

            //deserialize JSON directly from a file
            using (var file = File.OpenText(jsonFileName))
            {
                var serializer = new JsonSerializer();
                voc = (Vocabulary)serializer.Deserialize(file, typeof(Vocabulary));
            }

            voc.PopulateDependentJsonPropertys();
            return voc;
        }

        public bool ContainsWord(string word)
        {
            return WordWithPossiblePOS.ContainsKey(word);
        }

        public bool ContainsPOS(string pos)
        {
            return POSWithPossibleWords.ContainsKey(pos);
        }

        public List<string[]> BuildVocabularyFromPOSTaggedInput(List<string[]> sentences, List<string[]> poses)
        {
            Dictionary<string, List<string>> posToWordsMap = new Dictionary<string, List<string>>();
            List<string> posCategories = new List<string> { "ADJ", "ADP", "DET", "NOUN", "PRON", "VERB", "AUX", "PROPN" };
            foreach (var pos in posCategories)
                posToWordsMap[pos] = new List<string>();

            //unused categories for now:
            List<string> posCategoryUnused = new List<string> { "SYM", "PUNCT", "INTJ", "NUM", "CCONJ", "SCONJ", "ADV", "X", "PART" };
            List<string[]> sentencesWithUsedCategories = new List<string[]>();

            for (int i = 0; i < sentences.Count; i++)
            {
                bool foundUnusedCat = false;
                for (int j = 0; j < sentences[i].Length; j++)
                {
                    if (posCategoryUnused.Contains(poses[i][j]))
                    {
                        foundUnusedCat = true;
                        break;
                    }
                    else if (posCategories.Contains(poses[i][j]))
                    {
                        var w = sentences[i][j];
                        var isThisThatThoseThese = (w == "this" || w == "that" || w == "those" || w == "these");

                        if (isThisThatThoseThese && j < sentences[i].Length -1  && poses[i][j+1] == "AUX")
                        {
                            //fix, automatic taggers always ascribe DET part to this/that/these/those - WRONG.
                            poses[i][j] = "PRON"; //if they are followed by a verb, they are pronouns.
                        }
                            
                        posToWordsMap[poses[i][j]].Add(sentences[i][j]);
                    }
                    else
                        throw new Exception($"unrecognized POS: {poses[i][j]} ");
                }

                if (!foundUnusedCat)
                    sentencesWithUsedCategories.Add(sentences[i]);
            }


            foreach (var pos in posCategories)
                AddWordsToPOSCategory(pos, posToWordsMap[pos].ToArray());

            //int count = 0;
            //foreach (var word in WordWithPossiblePOS.Keys)
            //{
            //    if (WordWithPossiblePOS[word].Count > 1)
            //    {
            //        Console.WriteLine($"word {word} has the following POS: { string.Join(" ", WordWithPossiblePOS[word])}");
            //        count++;
            //    }
            //}

            return sentencesWithUsedCategories;
        }

        public void AddWordsToPOSCategory(string posCat, string[] words)
        {
            foreach (var word in words)
            {
                if (!WordWithPossiblePOS.ContainsKey(word))
                    WordWithPossiblePOS[word] = new HashSet<string>();
                WordWithPossiblePOS[word].Add(posCat);
            }

            if (!POSWithPossibleWords.ContainsKey(posCat))
                POSWithPossibleWords[posCat] = new HashSet<string>();

            foreach (var word in words)
                POSWithPossibleWords[posCat].Add(word);
        }

        public static (List<string[]>, List<string[]>) ReadChildesFile(string filename)
        {
            var sentences = new List<string[]>();
            var poses = new List<string[]>();

            string line;
            using var file = File.OpenText(filename);

            while ((line = file.ReadLine()) != null)
            {
                var row = line.Split(',');
                sentences.Add(row[0].Split(' ', StringSplitOptions.RemoveEmptyEntries));
                poses.Add(row[1].Split(' ', StringSplitOptions.RemoveEmptyEntries));
            }

            return (sentences, poses);
        }

        //the function initializes WordWithPossiblePOS field after POSWithPossibleWords has been read from a json file.
        private void PopulateDependentJsonPropertys()
        {
            foreach (var kvp in POSWithPossibleWords)
            {
                var words = kvp.Value;
                foreach (var word in words)
                {
                    if (!WordWithPossiblePOS.ContainsKey(word))
                        WordWithPossiblePOS[word] = new HashSet<string>();
                    WordWithPossiblePOS[word].Add(kvp.Key);
                }
            }
        }
    }
}