using GrammarLearnerGUI.Models;
using GINWineParser;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Text;

namespace GrammarLearnerGUI.Controllers
{
    [Produces("application/json")]
    [Route("api/Parser")]
    public class ParserController : Controller
    {

        private SyntacticTree ExtractTreeFromEarleyState(EarleyState state, ContextFreeGrammar g)
        {
            SyntacticTree tree = null, subtree1 = null, subtree2 = null;


            //predecessor
            if (state.Predecessor != null)
                subtree1 = ExtractTreeFromEarleyState(state.Predecessor, g);

            //reductor
            if (state.Reductor != null)
                subtree2 = ExtractTreeFromEarleyState(state.Reductor, g);


            if (state.IsCompleted)
            {
                tree = new SyntacticTree();
                tree.Name = state.Rule.LeftHandSide.ToString();
                tree.Children = new List<SyntacticTree>();

                if (subtree1 != null || subtree2 != null)
                {
                    if (subtree1 != null)
                        tree.Children.Add(subtree1);
                    if (subtree2 != null)
                        tree.Children.Add(subtree2);
                }
                else
                {
                    var terminal = state.Rule.RightHandSide[0].ToString();
                    tree.Name = tree.Name + "(" + terminal + ")";
                }


                return tree;

            }
            return subtree2;



        }
        [HttpPost]
        [Route("ParseSentence/{text}")]
        //public int[] ParseSentence([FromBody] DataMatrix<float> mat)
        public SyntacticTree ParseSentence(string text)
        {
            var res = new List<SyntacticTree>();
            var v = Vocabulary.ReadVocabularyFromFile(@"..\GINWine\Input Data\Vocabularies\ChildesGUIVocabulary.json");
            var rules = GrammarFileReader.ReadRulesFromFile(@"..\GINWine\Input Data\Context Free Grammars\TargetGrammar.txt");
            var g = new ContextFreeGrammar(rules);

            var parser = new EarleyParser(g, v, text.Split(), true);
            var treeDic = new Dictionary<int, HashSet<string>>();
            for (int i = 1; i <= 10; i++)
                treeDic[i] = new HashSet<string>();

            parser.ParseSentence(treeDic);

            var gStates = parser.GetGammaStates();

            foreach (var state in gStates)
                res.Add(ExtractTreeFromEarleyState(state, g));

            return res[0];
        }

    }
    
}
