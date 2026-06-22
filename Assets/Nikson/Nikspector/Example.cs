#if UNITY_EDITOR
#pragma warning disable
using UnityEngine;
using Nikson;

namespace Nikson
{
    public class Example : MonoBehaviour
    {
        [Tab("Serialize")]
        [BetterHeader("BetterSerializer")]
        [BetterSerializer] int d { get; set; } = 4;
        [BetterSerializer] const int a = 1;
        [BetterSerializer] readonly int b = 2;
        [BetterSerializer] static int c = 3;

        [Tab("Foldouts")]
        [BetterHeader("Foldout")]
        [Foldout("Names")]
        public string name1 = "Nikson";
        public string name2 = "Dave";
        public string name3 = "Ringo";
        [Foldout("Ages")]
        public int age1 = 29;
        public int age2 = 30;
        public int age3 = 28;
        [FoldoutEnd]
        public int age4 = 33;

        [Button] void PrintNothing() => print("Nothing");
        [Button] void PrintEverything() => print("Everything");
    }
}
#endif