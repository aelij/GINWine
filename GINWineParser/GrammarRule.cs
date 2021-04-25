using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GINWineParser
{
    public class RuleReferenceEquals : IEqualityComparer<Rule>
    {
        public bool Equals(Rule x, Rule y)
        {
            return x == y;
        }

        public int GetHashCode(Rule obj)
        {
            return obj.GetHashCode();
        }
    }

    public class RuleValueEquals : IEqualityComparer<Rule>
    {
        public bool Equals(Rule x, Rule y)
        {
            if (x.NumberOfGeneratingRule != y.NumberOfGeneratingRule
                ||
                !x.LeftHandSide.Equals(y.LeftHandSide)
                ||
                x.RightHandSide.Length != y.RightHandSide.Length)
                return false;

            for (var i = 0; i < x.RightHandSide.Length; i++)
                if (!x.RightHandSide[i].Equals(y.RightHandSide[i]))
                    return false;

            return true;
        }

        public int GetHashCode(Rule obj)
        {
            return obj.GetHashCode();
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class Rule : IEquatable<Rule>
    {
        public Rule()
        {
        }

        public Rule(DerivedCategory leftHandSide, DerivedCategory[] rightHandSide)
        {
            LeftHandSide = new DerivedCategory(leftHandSide);
            if (rightHandSide != null)
            {
                var length = rightHandSide.Length;
                RightHandSide = new DerivedCategory[length];
                for (var i = 0; i < length; i++)
                    RightHandSide[i] = new DerivedCategory(rightHandSide[i]);
            }
        }

        public Rule(string leftHandSide, string[] rightHandSide)
        {
            LeftHandSide = new DerivedCategory(leftHandSide);
            if (rightHandSide != null)
            {
                var length = rightHandSide.Length;
                RightHandSide = new DerivedCategory[length];
                for (var i = 0; i < length; i++)
                    RightHandSide[i] = new DerivedCategory(rightHandSide[i]);
            }
        }

        public Rule(Rule otherRule)
        {
            LeftHandSide = new DerivedCategory(otherRule.LeftHandSide);
            if (otherRule.RightHandSide != null)
            {
                var length = otherRule.RightHandSide.Length;
                RightHandSide = new DerivedCategory[length];
                for (var i = 0; i < length; i++)
                    RightHandSide[i] = new DerivedCategory(otherRule.RightHandSide[i]);
            }

            NumberOfGeneratingRule = otherRule.NumberOfGeneratingRule;
        }

        [JsonProperty] public DerivedCategory LeftHandSide { get; set; }

        [JsonProperty] public DerivedCategory[] RightHandSide { get; set; }

        public int NumberOfGeneratingRule { get; set; }

        public bool Equals(Rule other)
        {
            return NumberOfGeneratingRule == other.NumberOfGeneratingRule;
        }

        public override string ToString()
        {
            var p = RightHandSide.Select(x => x.ToString()).ToArray();
            return $"{NumberOfGeneratingRule}. {LeftHandSide} -> {string.Join(" ", p)}";
        }

        public override int GetHashCode()
        {
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            return NumberOfGeneratingRule;
        }

        public bool IsEpsilon()
        {
            return RightHandSide[0].IsEpsilon();
        }
    }
}