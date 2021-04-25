namespace GINWineLearner
{
    public class SentenceParsingResults
    {
        //the sentence being parsed
        public string[] Sentence { get; set; }

        //the number of times the sentence was encountered in the corpus.
        public int Count { get; set; }

        //the length of the sentence (number of words)
        public int Length { get; set; }
    }
}