using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DBPu
{
    internal class DBPu
    {
        public static byte[] bytes;
        public static object InvokeObj;
        public static Type TypeObj;
        public static MethodInfo fileType;
        public static MethodInfo load;
        public static MethodInfo getTypeAss;
        public static Type MainMod;
        public static void OnLoad()
        {
            try
            {
                var AssemblyObj = Type.GetType("System.Reflection.Assembly");
                load = AssemblyObj.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(method => method.Name == "Load").Where(method => method.GetParameters()[0].ParameterType.Name == "Byte[]").First();
                getTypeAss = Type.GetType("System.Reflection.Assembly").GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance).Where(method => method.Name == "GetType" && method.GetParameters().Count() == 1 && method.GetParameters()[0].ParameterType.ToString() == "System.String").First();
                fileType = Type.GetType("System.IO.File").GetMethod("ReadAllBytes", BindingFlags.Public | BindingFlags.Static);
                bytes = (byte[])fileType.Invoke(null, new object[] { Path.Combine(ModAPI.Metadata.MetaLocation, "dragon ball.dll") });
                InvokeObj = load.Invoke(null, new object[] { bytes });
                MainMod = (Type)getTypeAss.Invoke(InvokeObj, new object[] { "DBPU.DBPU" });
                MainMod.GetMethod("OnLoad", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { });
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
        public static void Main()
        {
            MainMod.GetMethod("Main", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { });
        }
    }
}

// Originally uploaded by 'Sequel'. Do not reupload without their explicit permission.
