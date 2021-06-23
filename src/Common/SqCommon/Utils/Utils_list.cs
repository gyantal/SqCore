using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SqCommon
{

    public static partial class Utils
    {

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