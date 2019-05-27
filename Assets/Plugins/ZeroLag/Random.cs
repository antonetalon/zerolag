using System;
using System.Collections.Generic;
using System.Linq;
using FixMath.NET;
using ServerEngine;

namespace ZeroLag
{
    [GenBattleData, GenSerialization, GenTask(GenTaskFlags.JsonSerialization)]
    public partial class Random
    {
        const long c = 2147483647;
        const long a = 6364136223846793005;
        private long seed;
        public Random(long seed) {
            this.seed = seed;
        }

        public void SetSeed(long seed)
        {
            this.seed = seed;
        }
        
        private long RotateLeft(long x, Byte n)
        {
            return ((x << n) | (x >> (64 - n)));
        }

        public long GetSeed() => seed;
        /// <summary>
        /// range = [min;max]
        /// </summary>
        /// <param name="min">including</param>
        /// <param name="max">including</param>
        /// <returns></returns>
        public int RangeInt(int min, int max)
        {
            int rand = Math.Abs((int)Next());
            rand = rand % (max - min + 1) + min;
            return rand;
        }

        /// <summary>
        /// range = [min;max]
        /// </summary>
        /// <param name="min">including</param>
        /// <param name="max">including</param>
        /// <returns></returns>
        public long RangeLong(long min, long max)
        {
            var rand = Math.Abs(Next());
            rand = rand % (max - min + 1) + min;
            return rand;
        }

        public T RandomElement<T>(T[] array)
        {
            return array[RangeInt(0, array.Length - 1)];
        }

        public T RandomElement<T>(IList<T> list)
        {
            return list[RangeInt(0, list.Count - 1)];
        }

        public Fix64 RangeFrac(Fix64 min, Fix64 max)
        {
            Fix64 rand;
            if (min == max)
                rand = min;
            else
            {
                rand = new Fix64();
                rand.RawValue = Next();
                rand = Fix64.Abs(rand);
                rand = rand % (max - min) + min;
            }
            //ServerEngine.Debug.Log($"RangeFrac({min}, {max}) = {rand}");
            return rand;
        }

        public BEPUutilities.Vector2 randomVector2(Fix64 minRange, Fix64 maxRange)
        {
            Fix64 signX = randBool(0.5m) ? 1 : -1;
            Fix64 signY = randBool(0.5m) ? 1 : -1;

            Fix64 range = RangeFrac(minRange, maxRange);
            
            BEPUutilities.Vector2 result = new BEPUutilities.Vector2(RangeFrac(0.0001m, 1m) * signX, RangeFrac(0.0001m, 1m) * signY);
            Fix64 scale = result.Length() / range;

            result /= scale;
            
            return result;
        }

        public bool Chance(Fix64 chance)
        {
            return chance > RangeFrac(0m, 1m);
        }
        
        public bool randBool(Fix64 probability)
        {
            return RangeFrac(0, 1) < probability;
        }
        public long Next()
        {
            long next = a * RotateLeft(seed, 17) + c;
            seed = next;
            return next;
        }

        public T Element<T>(Array list) 
        {
            if (list.Length == 0) return default(T);
            if (list.Length == 1) return (T)list.GetValue(0);
            return (T)list.GetValue(RangeInt(0, list.Length - 1));
        }
        public T Element<T>(IEnumerable<T> list) 
        {
            return Element(list.ToList());
        }
        public T Element<T>(List<T> list) 
        {
            if (list.Count == 0) return default(T);
            if (list.Count == 1) return list[0];
            return list[RangeInt(0, list.Count - 1)];
        }
        
        public int[] RandomIndexes(int max, int count)
        {
            if (max <= count)
            {
                var r = new int[max];
                for (int i = 0; i < max; i++)
                {
                    r[i] = i;
                }
                return r;
            }
            var result = new int[count];
            var range = Enumerable.Range(0, max).ToList();
            for (int i = 0; i < count; ++i)
            {
                int randIndex = RangeInt(0, max - i - 1);
                int rand = range[randIndex];
                result[i] = rand;
                range[randIndex] = range[max - i - 1];
            }
            return result;
        }

        public T[] RandomElements<T>(IList<T> list, int count)
        {
            int max = list.Count;
            
            if (max <= count)
            {
                var r = new T[max];
                for (int i = 0; i < max; i++)
                {
                    r[i] = list[i];
                }
                return r;
            }
            var result = new T[count];
            var range = Enumerable.Range(0, max).ToList();
            for (int i = 0; i < count; ++i)
            {
                int randIndex = RangeInt(0, max - i - 1);
                int rand = range[randIndex];
                result[i] = list[rand];
                range[randIndex] = range[max - i - 1];
            }
            return result;
        }

        public int fromWeights(IList<Fix64> weights)
        {
            Fix64 sum = 0;
            int i;
            for (i = 0; i < weights.Count; i++)
                sum += weights[i];
            Fix64 val = RangeFrac(0, sum);
            i = 0;
            while (i<weights.Count)
            {
                val -= weights[i];
                if (val <= 0)
                    return i;
                i++;
            }

            return 0;
            // changed to 0 since if it is problem with fix64 precision its better to return smth valid
            // return -1;
        }
    }
}