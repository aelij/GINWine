using System.Collections.Generic;
using System.Linq;

namespace GINWineParser
{
    public class EarleyGenerator : EarleyParser
    {
        public EarleyGenerator(ContextFreeGrammar g, Vocabulary v) : base(g, v, null)
        {
        }

        protected override (EarleyColumn[], int[]) PrepareEarleyTable(string[] text, int maxWords)
        {
            var table = new EarleyColumn[maxWords + 1];

            for (var i = 1; i < table.Length; i++)
                table[i] = new EarleyColumn(i, "generator");

            table[0] = new EarleyColumn(0, "");
            var gammaColumns = Enumerable.Range(1, maxWords).ToArray();
            return (table, gammaColumns);
        }

        protected override HashSet<string> GetPossibleSyntacticCategoriesForToken(string nextScannableTerm)
        {
            return Voc.POSWithPossibleWords.Keys.ToHashSet();
        }
    }
}