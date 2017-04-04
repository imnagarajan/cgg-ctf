using System.Collections.Generic;
using System.Linq;

using CGGCTF;

namespace CGGCTF.Extensions
{
    public static class IEnumerableExtensions
    {
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> list)
        {
            var li = list.ToList();
            for (int i = li.Count-1; i >= 0; --i) {
                int j = CTFUtils.Random(i+1);
                var temp = li[i];
                li[i] = li[j];
                li[j] = temp;
            }

            return li;
        }
    }
}
