using System;
using System.Collections.Generic;
using System.Text;

namespace GINWineParser
{
    public class EarleyState : IEquatable<EarleyState>
    {
        public string BracketedRepresentation;
        public HashSet<EarleyState> Parents = new HashSet<EarleyState>();

        public EarleyState(Rule r, int dotIndex, EarleyColumn c)
        {
            Rule = r;
            DotIndex = dotIndex;
            StartColumn = c;
            EndColumn = null;
        }

        public bool Added { get; set; }
        public bool Removed { get; set; }

        public Rule Rule { get; }
        public EarleyColumn StartColumn { get; }
        public EarleyColumn EndColumn { get; set; }
        public int DotIndex { get; }
        public EarleyState Predecessor { get; set; }
        public EarleyState Reductor { get; set; }
        public bool IsCompleted => DotIndex >= Rule.RightHandSide.Length;

        public DerivedCategory NextTerm => IsCompleted ? null : Rule.RightHandSide[DotIndex];

        public bool Equals(EarleyState other)
        {
            return this == other;
        }

        public void CreateBracketedRepresentation(StringBuilder sb, ContextFreeGrammar g)
        {
            if (IsCompleted) sb.Append($"({Rule.LeftHandSide} ");

            //predecessor
            if (DotIndex > 1)
            {
                Predecessor.CreateBracketedRepresentation(sb, g);
                sb.Append(" ");
            }

            //reductor
            if (g.StaticRules.ContainsKey(Rule.RightHandSide[DotIndex - 1]))
                Reductor.CreateBracketedRepresentation(sb, g);
            else
                sb.Append($"{Rule.RightHandSide[DotIndex - 1]}");

            if (IsCompleted) sb.Append(")");
        }

        public List<EarleyState> GetTransitiveClosureOfParents()
        {
            var l = new List<EarleyState>();

            foreach (var parent in Parents)
            {
                l.Add(parent);
                var grandParents = parent.GetTransitiveClosureOfParents();
                l.AddRange(grandParents);
            }

            return l;
        }

        private static string RuleWithDotNotation(Rule rule, int dotIndex)
        {
            //this function is called very heavily, so the following code
            //is optimized to return exactly the possible 5 cases.
            if (rule.RightHandSide.Length == 1)
            {
                if (dotIndex == 0)
                    return $"{rule.LeftHandSide} -> $ {rule.RightHandSide[0]}";

                return $"{rule.LeftHandSide} -> {rule.RightHandSide[0]} $";
            }

            //length  = 2
            if (dotIndex == 0)
                return $"{rule.LeftHandSide} -> $ {rule.RightHandSide[0]} {rule.RightHandSide[1]}";
            if (dotIndex == 1)
                return $"{rule.LeftHandSide} -> {rule.RightHandSide[0]} $ {rule.RightHandSide[1]}";

            return $"{rule.LeftHandSide} -> {rule.RightHandSide[0]} {rule.RightHandSide[1]} $";
        }

        public override string ToString()
        {
            var endColumnIndex = "None";
            if (EndColumn != null)
                endColumnIndex = EndColumn.Index.ToString();
            return string.Format("{0} [{1}-{2}]", RuleWithDotNotation(Rule, DotIndex),
                StartColumn.Index, endColumnIndex);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + Rule.GetHashCode();
                hash = hash * 23 + DotIndex;
                hash = hash * 23 + StartColumn.Index;
                return hash;
            }
        }

        public string GetNonTerminalStringUnderNode(HashSet<string> pos)
        {
            var leaves = new List<string>();
            GetNonTerminalStringUnderNode(leaves, pos);
            return string.Join(" ", leaves);
        }

        private void GetNonTerminalStringUnderNode(List<string> leavesList, HashSet<string> pos)
        {
            if (!IsCompleted)
            {
                var nextTerm = NextTerm.ToString();
                if (pos.Contains(nextTerm))
                    leavesList.Insert(0, nextTerm);
            }

            Reductor?.GetNonTerminalStringUnderNode(leavesList, pos);
            Predecessor?.GetNonTerminalStringUnderNode(leavesList, pos);
        }
    }
}