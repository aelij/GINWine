using System;
using System.Collections.Generic;

namespace GINWineParser
{
    public class MaxHeap
    {
        public readonly List<int> Elements = new List<int>();

        public int Count => Elements.Count;

        public int Max => Elements[0];

        public void Add(int item)
        {
            Elements.Add(item);
            HeapifyUp(Elements.Count - 1);
        }

        public void Clear()
        {
            Elements.Clear();
        }

        public int PopMax()
        {
            if (Elements.Count > 0)
            {
                var item = Elements[0];
                Elements[0] = Elements[Elements.Count - 1];
                Elements.RemoveAt(Elements.Count - 1);

                HeapifyDown(0);
                return item;
            }

            throw new InvalidOperationException("no element in heap");
        }

        private void HeapifyUp(int index)
        {
            var parent = index <= 0 ? -1 : (index - 1) / 2;

            if (parent >= 0 && Elements[index] > Elements[parent])
            {
                var temp = Elements[index];
                Elements[index] = Elements[parent];
                Elements[parent] = temp;

                HeapifyUp(parent);
            }
        }

        private void HeapifyDown(int index)
        {
            var largest = index;

            var left = 2 * index + 1;
            var right = 2 * index + 2;

            if (left < Count && Elements[left] > Elements[index])
                largest = left;

            if (right < Count && Elements[right] > Elements[largest])
                largest = right;

            if (largest != index)
            {
                var temp = Elements[index];
                Elements[index] = Elements[largest];
                Elements[largest] = temp;

                HeapifyDown(largest);
            }
        }
    }
}