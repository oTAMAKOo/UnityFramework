﻿
using System;﻿
using System.Collections.Generic;
using System.Linq;

namespace Extensions
{
    public static class RandomUtility
    {
        private static Random random = new Random();

        public static int Seed { get; private set; }

        static RandomUtility()
        {
            SetRandomSeed();
        }

        public static void SetRandomSeed(int? seed = null)
        {
            Seed = seed.HasValue ? seed.Value : Environment.TickCount;
            
            lock (random)
            {
                random = new Random(Seed);
            }
        }

        private static int Range(int min, int max)
        {
            lock (random)
            {
                return random.Next(min, max);
            }
        }

        private static double Range(double min, double max)
        {
            lock (random)
            {
                return min + (random.NextDouble() * (max - min));
            }
        }

        //-----------------------------------------------
        // Random Type : Int.
        //-----------------------------------------------

        public static int RandomInt()
        {
            return Range(int.MinValue, int.MaxValue);
        }

        public static int RandomInRange(int min, int max)
        {
            return Range(min, max + 1);
        }

        /// <summary> 0-max%を入力してヒットしたかを判定.</summary>
        public static bool IsPercentageHit(int percentage, int max)
        {
            return percentage != 0 && RandomInRange(1, max) <= percentage;
        }

        /// <summary> 0-max%を入力してヒットしたかを判定.</summary>
        public static bool IsPercentageHit(float percentage, float max)
        {
            return percentage != 0f && RandomInRange(1f, max) <= percentage;
        }

        //-----------------------------------------------
        // Random Type : Float.
        //-----------------------------------------------

        public static float RandomFloat()
        {
            return (float)Range(float.MinValue, float.MaxValue);
        }

        public static float RandomInRange(float min, float max)
        {
            return (float)Range(min, max);
        }

        //-----------------------------------------------
        // Random Type : Double.
        //-----------------------------------------------

        public static double RandomDouble()
        {
            return Range(double.MinValue, double.MaxValue);
        }

        public static double RandomInRange(double min, double max)
        {
            return Range(min, max);
        }

        //-----------------------------------------------
        // Random Type : Bool.
        //-----------------------------------------------

        public static bool RandomBool()
        {
            return (RandomInt() % 2) == 0;
        }

        //-----------------------------------------------
        // Random Type : Weight.
        //-----------------------------------------------

        /// <summary> 重みを考慮したランダム抽選 </summary>
        public static int GetRandomIndexByWeight(int[] weightTable)
        {
            var totalWeight = weightTable.Sum();
            var value = RandomInRange(1, totalWeight);
            var retIndex = -1;

            for (var i = 0; i < weightTable.Length; ++i)
            {
                if (weightTable[i] >= value)
                {
                    retIndex = i;
                    break;
                }
                value -= weightTable[i];
            }

            return retIndex;
        }

        /// <summary> 重みを考慮したランダム抽選 </summary>
        public static T GetRandomByWeight<T>(int[] weightTable, T[] valueTable)
        {
            if (weightTable.Length != valueTable.Length)
            {
                throw new ArgumentException();
            }

            var index = GetRandomIndexByWeight(weightTable);

            return valueTable[index];
        }
    }
}
