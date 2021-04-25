using System;
using System.Collections.Generic;
using System.Linq;

namespace GINWineParser
{
    public class RuleCoordinates : IEquatable<RuleCoordinates>
    {
        public RuleCoordinates()
        {
        }

        public RuleCoordinates(RuleCoordinates other)
        {
            LHSIndex = other.LHSIndex;
            RHSIndex = other.RHSIndex;
            RuleType = other.RuleType;
        }

        public int RuleType { get; set; }

        //the LHS index in the Rule Space (index 0 = LHS "X1", index 1 = LHS "X2", etc)
        public int LHSIndex { get; set; }

        //the RHS index in the Rule Space ( same RHS index for different LHS indices means they share the same RHS)
        public int RHSIndex { get; set; }

        public bool Equals(RuleCoordinates other)
        {
            return RuleType == other.RuleType && LHSIndex == other.LHSIndex && RHSIndex == other.RHSIndex;
        }

        public override int GetHashCode()
        {
            int hashCode;
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            hashCode = RuleType;
            unchecked
            {
                // ReSharper disable once NonReadonlyMemberInGetHashCode
                hashCode = (hashCode * 397) ^ LHSIndex;
                // ReSharper disable once NonReadonlyMemberInGetHashCode
                hashCode = (hashCode * 397) ^ RHSIndex;
                return hashCode;
            }
        }
    }

    public class ContextSensitiveGrammar
    {
        //Rule space is a 3D array,
        //zero index is the rule type: 0 = CFG rule table, 1 = Push LIG rule table, 2 = Pop rule table.
        //first index is LHS, the column in the relevant rule table ([0] = "X1", [1] = "X2", [2] = "X3" etc)
        //second index is RHS, the row in the relevant rule table such that [0][i] = [1][i] = [2][i] etc. 
        //(note, RHS index 0 is a special case, see RuleSpace class).
        public static RuleSpace RuleSpace;
        public readonly Dictionary<int, int> MoveableReferences = new Dictionary<int, int>();
        public readonly List<RuleCoordinates> StackConstantRules = new List<RuleCoordinates>();
        public readonly List<RuleCoordinates> StackPush1Rules = new List<RuleCoordinates>();

        public ContextSensitiveGrammar()
        {
        }

        public ContextSensitiveGrammar(List<Rule> grammarRules)
        {
            foreach (var rule in grammarRules)
            {
                var rc = RuleSpace.FindRule(rule);
                if (rc.RuleType == RuleType.CFGRules)
                {
                    StackConstantRules.Add(rc);
                }
                else if (rc.RuleType == RuleType.Push1Rules)
                {
                    StackPush1Rules.Add(rc);
                    AddCorrespondingPopRule(rc);
                }
            }
        }

        public ContextSensitiveGrammar(ContextSensitiveGrammar otherGrammar)
        {
            StackConstantRules = otherGrammar.StackConstantRules.Select(x => new RuleCoordinates(x)).ToList();
            StackPush1Rules = otherGrammar.StackPush1Rules.Select(x => new RuleCoordinates(x)).ToList();
            MoveableReferences = otherGrammar.MoveableReferences.ToDictionary(x => x.Key, x => x.Value);
        }

        public override string ToString()
        {
            var s1 = "Stack Constant Rules:\r\n" +
                     string.Join("\r\n", StackConstantRules.Select(x => RuleSpace[x].ToString()));
            var s2 = "Stack Changing Rules:\r\n" +
                     string.Join("\r\n", StackPush1Rules.Select(x => RuleSpace[x].ToString()));
            return s1 + "\r\n" + s2;
        }

        public void PruneUnusedRules(Dictionary<int, int> usagesDic)
        {
            try
            {
                foreach (var rule in StackConstantRules)
                    if (RuleSpace[rule] == null)
                    {
                        Console.WriteLine(
                            $"Rule lhs {rule.LHSIndex}, rule rhs {rule.RHSIndex}, rule space type: {rule.RuleType} ");
                        throw new Exception();
                    }

                if (StackConstantRules == null)
                {
                    Console.WriteLine("Stack Constant Rules is null");
                    throw new Exception();
                }

                if (usagesDic == null)
                {
                    Console.WriteLine("usages dic is null");
                    throw new Exception();
                }

                if (RuleSpace == null)
                {
                    Console.WriteLine("rule space is null");
                    throw new Exception();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }


            var unusedConstantRules =
                StackConstantRules.Where(x => !usagesDic.ContainsKey(RuleSpace[x].NumberOfGeneratingRule)).ToArray();

            foreach (var unusedRule in unusedConstantRules.ToList())
                StackConstantRules.Remove(unusedRule);

            var unusedChangingRules =
                StackPush1Rules.Where(x => !usagesDic.ContainsKey(RuleSpace[x].NumberOfGeneratingRule)).ToArray();

            foreach (var unusedRule in unusedChangingRules.ToList())
                StackPush1Rules.Remove(unusedRule);
        }

        public void AddCorrespondingPopRule(RuleCoordinates rc)
        {
            var pushRule = RuleSpace[rc];

            //assumption: moveable is first RHS, to be relaxed.
            var moveable = pushRule.RightHandSide[0].ToString();
            var lhsIndex = RuleSpace.FindLHSIndex(moveable);
            if (!MoveableReferences.ContainsKey(lhsIndex))
                MoveableReferences[lhsIndex] = 0;

            MoveableReferences[lhsIndex]++;
        }


        public void DeleteCorrespondingPopRule(RuleCoordinates rc)
        {
            var pushRule = RuleSpace[rc];
            //assumption: moveable is first RHS, to be relaxed.
            var moveable = pushRule.RightHandSide[0].ToString();
            var lhsIndex = RuleSpace.FindLHSIndex(moveable);
            if (!MoveableReferences.ContainsKey(lhsIndex))
                throw new Exception("missing key");

            MoveableReferences[lhsIndex]--;
            if (MoveableReferences[lhsIndex] < 0)
                throw new Exception("wrong value of moveable references");
        }
    }
}