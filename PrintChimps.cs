
using System;
using SHCDESE.Interop;

public class Program
{
    public static void Main()
    {
        foreach (eChimps chimp in Enum.GetValues(typeof(eChimps)))
        {
            Console.WriteLine($"{(int)chimp}: {chimp}");
        }
    }
}
