
using System;
using System.Reflection;
using SHCDESE.Interop;

public class Program
{
    public static void Main()
    {
        Type type = typeof(UnitGoodCosts);
        Console.WriteLine($"Fields of {type.FullName}:");
        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            Console.WriteLine($"  {field.Name}: {field.FieldType.Name}");
        }
        
        Type apiType = typeof(SHCDESE.API.GameUnitManagerAPI);
        Console.WriteLine($"\nMethods of {apiType.FullName} (Set/Get):");
        foreach (MethodInfo method in apiType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            if (method.Name.Contains("Cost") || method.Name.Contains("Speed"))
            {
                Console.WriteLine($"  {method.Name}");
            }
        }
    }
}
