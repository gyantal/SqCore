using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SqCommon
{
    public static partial class Utils
    {
        // There is no Foreach of IEnumerable, only for List/Array.
        // But Calling ToList() followed by ForEach() involves iterating through the original collection twice. And extra malloc. This avoids building a List()
        // https://stackoverflow.com/questions/1509442/linq-style-for-each
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            foreach (T item in source)
            {
                action(item);
            }
        }

        // https://stackoverflow.com/questions/2575592/moving-a-member-of-a-list-to-the-front-of-the-list/2576736
        public static void MoveItemAtIndexToFront<T>(this List<T> list, int index)
        {
            T item = list[index];
            for (int i = index; i > 0; i--)
                list[i] = list[i - 1];
            list[0] = item;
        }
    }
}