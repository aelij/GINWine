using System.Collections.Generic;

namespace GINWineParser
{
    public class DeletedStatesHeap
    {
        private readonly MaxHeap _indicesHeap = new MaxHeap();
        //the node's value of the heap is stack and not queue - purely from performance consideration
        //stack is slightly faster than queue (we could implement it as queue like CompletedStatesHEap if we wanted,
        //because deletion uses lazy evaulation, so either queue or stack order guarantees visiting
        //all items to be deleted).
        private readonly Dictionary<int, Stack<EarleyState>> _items = new Dictionary<int, Stack<EarleyState>>();

        public int Count => _indicesHeap.Count;

        public void Push(EarleyState state)
        {
            var index = state.StartColumn.Index;
            if (!_items.TryGetValue(index, out var queue))
            {
                _indicesHeap.Add(index);
                queue = new Stack<EarleyState>();
                _items.Add(index, queue);
            }

            queue.Push(state);
        }

        public EarleyState Pop()
        {
            var index = _indicesHeap.Max;
            var queue = _items[index];

            var state = queue.Pop();
            if (queue.Count == 0)
            {
                _items.Remove(index);
                _indicesHeap.PopMax();
            }

            return state;
        }
    }
}