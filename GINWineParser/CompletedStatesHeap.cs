using System.Collections.Generic;

namespace GINWineParser
{
    public class CompletedStatesHeap
    {
        private readonly MaxHeap _indicesHeap = new MaxHeap();
        private readonly Dictionary<int, Queue<EarleyState>> _items = new Dictionary<int, Queue<EarleyState>>();

        public int Count => _indicesHeap.Count;

        public void Enqueue(EarleyState state)
        {
            var index = state.StartColumn.Index;
            if (!_items.TryGetValue(index, out var queue))
            {
                _indicesHeap.Add(index);
                queue = new Queue<EarleyState>();
                _items.Add(index, queue);
            }

            queue.Enqueue(state);
        }

        public void Clear()
        {
            _indicesHeap.Clear();
            _items.Clear();
        }

        public EarleyState Dequeue()
        {
            var index = _indicesHeap.Max;
            var queue = _items[index];

            var state = queue.Dequeue();
            if (queue.Count == 0)
            {
                _items.Remove(index);
                _indicesHeap.PopMax();
            }

            return state;
        }
    }
}