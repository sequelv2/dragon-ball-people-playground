using System.IO;
using System.Reflection;
using UnityEngine;
namespace WorldBreakerLoader
{
    public class Mod
    {
        static Assembly runtime;
        public static void OnLoad()
        {
            runtime = Assembly.Load(File.ReadAllBytes(Path.Combine(ModAPI.Metadata.MetaLocation, "DragonballWorldBreaker.dll")));
        }
        public static void Main()
        {
            if (runtime == null) OnLoad();
            runtime.GetType("DragonballWorldBreaker.Entry").GetMethod("Main", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        }
    }
}
