using System;
using System.Collections.Generic;

namespace ZeroLag
{
    public static class ZeroLagUtils
    {
        public static bool RemoveOne<T>(this List<T> list, Predicate<T> whatToRemove)
        {
            for (int i = 0; i < list.Count; i++)
            {
                bool found = whatToRemove(list[i]);
                if (!found)
                    continue;
                list.RemoveAt(i);
                return true;
            }
            return false;
        }
        public static int InsertSorted<T>(this IList<T> list, Func<T, T, int> predicate, T val)
        {
            var i = list.IndexOf(t => predicate(t, val) >= 0);
            if (i != -1)
            {
                list.Insert(i, val);
                return i;
            }
            else
            {
                list.Add(val);
                return list.Count - 1;
            }
        }
        public static void InsertSorted<T, C>(this List<T> list, T item, Func<T, C> whatToCompare) where C : IComparable
        {
            C itemValue = whatToCompare(item);
            int minInd = 0;
            int maxInd = list.Count;
            while (minInd < maxInd)
            {
                int middleInd = (minInd + maxInd) / 2;
                C middleValue = whatToCompare(list[middleInd]);
                int compare = itemValue.CompareTo(middleValue);
                if (compare < 0)
                    maxInd = middleInd;
                else if (compare > 0)
                    minInd = middleInd + 1;
                else
                {
                    minInd = middleInd;
                    maxInd = middleInd;
                }
            }
            int insertInd = minInd;
            list.Insert(insertInd, item);
        }
        public static int IndexOf<T>(this IEnumerable<T> list, Func<T, bool> predicate)
        {
            int index = 0;
            foreach (var elem in list)
            {
                if (predicate(elem)) return index;
                index++;
            }
            return -1;
        }
        public static void Swap<T>(ref T item1, ref T item2)
        {
            T temp = item1;
            item1 = item2;
            item2 = temp;
        }
    }
}