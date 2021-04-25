using GINWineParser;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GINWineLearner
{
    public class BestGrammarsQueue
    {
        private List<(BestGrammarsKey, ContextSensitiveGrammar)> list = new List<(BestGrammarsKey, ContextSensitiveGrammar)>();
        private int _currentIndex;
        public int Count => list.Count;

        public (BestGrammarsKey, ContextSensitiveGrammar) FindMin()
        {
            var min = list.FirstOrDefault();
            foreach (var item in list)
            {
                if (item.Item1.CompareTo(min.Item1) < 0)
                    min = item;
            }
            return min;
        }

        public (BestGrammarsKey, ContextSensitiveGrammar) FindMax()
        {
            var max = list.FirstOrDefault();
            foreach (var item in list)
            {
                if (item.Item1.CompareTo(max.Item1) > 0)
                    max = item;
            }

            return max;
        }

        public BestGrammarsKey FindMinKey()
        {
            if (list.Count == 0)
                return new BestGrammarsKey(0, false);
            return FindMin().Item1;
        }

        public void RemoveMin()
        {
            if (list.Count > 0)
            {
                var min = FindMin();
                list.Remove(min);
                _currentIndex--;

            }
        }

        public (BestGrammarsKey, ContextSensitiveGrammar) RemoveMax()
        {
            if (list.Count > 0)
            {
                var max = FindMax();
                list.Remove(max);
                _currentIndex--;
                return max;

            }
            return (new BestGrammarsKey(0, false), null);
        }

        public void Insert((BestGrammarsKey, ContextSensitiveGrammar) item)
        {
            var key = new BestGrammarsKey(item.Item1);
            var grammar = new ContextSensitiveGrammar(item.Item2);
            list.Add((key, grammar));
        }

        public bool ContainsKey(BestGrammarsKey key)
        {
            bool res = false;
            if (list.Count == 0) return false;
            foreach (var item in list)
            {
                if (item.Item1.CompareTo(key) == 0)
                {
                    res = true;
                    break;
                }
            }
            return res;
        }

        public (BestGrammarsKey, ContextSensitiveGrammar) Next()
        {
            if (list.Count == 0)
                throw new Exception("empty best grammars queue");

            if (_currentIndex == list.Count) _currentIndex = 0;
            return list[_currentIndex++]; 
        }



    }
    public class BestGrammarsKey : IComparable<BestGrammarsKey>, IEquatable<BestGrammarsKey>
    {
        public readonly double ObjectiveFunctionValue;
        public readonly bool Feasible;
        private double TOLERANCE = 0.0001;

        public double Key
        {
            get
            {

                var k = Feasible ? ObjectiveFunctionValue + 1.0 : ObjectiveFunctionValue;
                return k;
            }
        }
        public override string ToString()
        {
            return $"objective function value {ObjectiveFunctionValue:0.000} feasible: {Feasible}";
        }
        public BestGrammarsKey(double objectiveVal, bool f)
        {
            ObjectiveFunctionValue = objectiveVal;
            Feasible = f;
        }

        public BestGrammarsKey(BestGrammarsKey other)
        {
            ObjectiveFunctionValue = other.ObjectiveFunctionValue;
            Feasible = other.Feasible;
        }

        public int CompareTo(BestGrammarsKey other)
        {
            return Key.CompareTo(other.Key);
        }

        public bool Equals(BestGrammarsKey other)
        {
            return Math.Abs(Key - other.Key) < TOLERANCE;
        }
    }
}
