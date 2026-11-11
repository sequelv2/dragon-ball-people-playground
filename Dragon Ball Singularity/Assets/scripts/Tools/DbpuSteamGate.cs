using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace DBPU
{
    public sealed class DbpuSteamItem
    {
        readonly object _raw;

        internal DbpuSteamItem(object raw)
        {
            _raw = raw ?? throw new ArgumentNullException("raw");
        }

        internal object Raw { get { return _raw; } }

        public string Title
        {
            get { return DbpuSteamGate.GetStringProp(_raw, "Title"); }
        }

        public string Description
        {
            get { return DbpuSteamGate.GetStringProp(_raw, "Description"); }
        }

        public ulong Id
        {
            get { return DbpuSteamGate.GetUlongValueMember(_raw, "Id"); }
        }

        public string OwnerName
        {
            get
            {
                try
                {
                    object owner = DbpuSteamGate.GetMemberValue(_raw, "Owner");
                    if (owner == null) return "Unknown";
                    string name = DbpuSteamGate.GetStringProp(owner, "Name");
                    return string.IsNullOrEmpty(name) ? "Unknown" : name;
                }
                catch
                {
                    return "Unknown";
                }
            }
        }

        public string[] Tags
        {
            get
            {
                object tags = DbpuSteamGate.GetMemberValue(_raw, "Tags");
                return tags as string[] ?? new string[0];
            }
        }

        public bool IsSubscribed
        {
            get { return DbpuSteamGate.GetBoolMember(_raw, "IsSubscribed"); }
        }

        public bool IsInstalled
        {
            get { return DbpuSteamGate.GetBoolMember(_raw, "IsInstalled"); }
        }

        public bool NeedsUpdate
        {
            get { return DbpuSteamGate.GetBoolMember(_raw, "NeedsUpdate"); }
        }

        public string Directory
        {
            get { return DbpuSteamGate.GetStringProp(_raw, "Directory"); }
        }

        public string PreviewImageUrl
        {
            get { return DbpuSteamGate.GetStringProp(_raw, "PreviewImageUrl"); }
        }

        public int VotesUp
        {
            get { return (int)DbpuSteamGate.GetUIntMember(_raw, "VotesUp"); }
        }

        public ulong NumSubscriptions
        {
            get { return DbpuSteamGate.GetUlongMember(_raw, "NumSubscriptions"); }
        }

        public string Url
        {
            get { return DbpuSteamGate.GetStringProp(_raw, "Url"); }
        }

        public DbpuSteamEditor Edit()
        {
            object editor = DbpuSteamGate.CallInstance(_raw, "Edit");
            return new DbpuSteamEditor(editor);
        }

        public object StartSubscribe()
        {
            return DbpuSteamGate.CallInstance(_raw, "Subscribe");
        }

        public object StartDownload(object progress, int milisecondsUpdateDelay)
        {
            return DbpuSteamGate.CallInstance(
                _raw,
                "DownloadAsync",
                new[] { typeof(Action<float>), typeof(int), typeof(System.Threading.CancellationToken) },
                new object[] { progress, milisecondsUpdateDelay, System.Threading.CancellationToken.None });
        }
    }

    public sealed class DbpuSteamEditor
    {
        readonly object _raw;

        internal DbpuSteamEditor(object raw)
        {
            _raw = raw ?? throw new ArgumentNullException("raw");
        }

        public DbpuSteamEditor WithTitle(string title)
        {
            return new DbpuSteamEditor(DbpuSteamGate.CallInstance(_raw, "WithTitle", title));
        }

        public DbpuSteamEditor WithDescription(string description)
        {
            return new DbpuSteamEditor(DbpuSteamGate.CallInstance(_raw, "WithDescription", description));
        }

        public DbpuSteamEditor WithPreviewFile(string path)
        {
            return new DbpuSteamEditor(DbpuSteamGate.CallInstance(_raw, "WithPreviewFile", path));
        }

        public DbpuSteamEditor WithContent(string folder)
        {
            return new DbpuSteamEditor(DbpuSteamGate.CallInstance(_raw, "WithContent", new[] { typeof(string) }, new object[] { folder }));
        }

        public DbpuSteamEditor WithTag(string tag)
        {
            return new DbpuSteamEditor(DbpuSteamGate.CallInstance(_raw, "WithTag", tag));
        }

        public DbpuSteamEditor WithPublicVisibility()
        {
            return new DbpuSteamEditor(DbpuSteamGate.CallInstance(_raw, "WithPublicVisibility"));
        }

        public object StartSubmit()
        {
            return DbpuSteamGate.CallInstance(
                _raw,
                "SubmitAsync",
                new Type[] { typeof(IProgress<float>), typeof(Action<ulong>) },
                new object[] { null, null });
        }
    }

    public sealed class DbpuSteamPublishResult
    {
        readonly object _raw;

        internal DbpuSteamPublishResult(object raw)
        {
            _raw = raw ?? throw new ArgumentNullException("raw");
        }

        public bool Ok
        {
            get
            {
                try
                {
                    object success = DbpuSteamGate.GetMemberValue(_raw, "Success");
                    if (success is bool)
                        return (bool)success;
                }
                catch { }

                try
                {
                    object result = DbpuSteamGate.GetMemberValue(_raw, "Result");
                    if (result == null) return false;
                    object ok = Enum.Parse(result.GetType(), "OK");
                    return ((Enum)result).HasFlag((Enum)ok);
                }
                catch
                {
                    return false;
                }
            }
        }

        public string ResultText
        {
            get
            {
                object result = DbpuSteamGate.GetMemberValue(_raw, "Result");
                return result != null ? Convert.ToString(result) : "";
            }
        }

        public ulong FileId
        {
            get { return DbpuSteamGate.GetUlongValueMember(_raw, "FileId"); }
        }

        public bool NeedsWorkshopAgreement
        {
            get { return DbpuSteamGate.GetBoolMember(_raw, "NeedsWorkshopAgreement"); }
        }
    }

    public sealed class DbpuSteamQuery
    {
        readonly object _raw;

        internal DbpuSteamQuery(object raw)
        {
            _raw = raw ?? throw new ArgumentNullException("raw");
        }

        public DbpuSteamQuery WithTag(string tag)
        {
            return new DbpuSteamQuery(DbpuSteamGate.CallInstance(_raw, "WithTag", tag));
        }

        public DbpuSteamQuery WhereSearchText(string searchText)
        {
            return new DbpuSteamQuery(DbpuSteamGate.CallInstance(_raw, "WhereSearchText", searchText));
        }

        public DbpuSteamQuery WithLongDescription(bool value)
        {
            return new DbpuSteamQuery(DbpuSteamGate.CallInstance(_raw, "WithLongDescription", value));
        }

        public object StartGetPage(int page)
        {
            return DbpuSteamGate.CallInstance(_raw, "GetPageAsync", page);
        }
    }

    public sealed class DbpuSteamResultPage
    {
        readonly object _raw;

        internal DbpuSteamResultPage(object raw)
        {
            _raw = raw ?? throw new ArgumentNullException("raw");
        }

        public int TotalCount
        {
            get { return Convert.ToInt32(DbpuSteamGate.GetMemberValue(_raw, "TotalCount") ?? 0); }
        }

        public IEnumerable<DbpuSteamItem> Entries
        {
            get
            {
                object entries = DbpuSteamGate.GetMemberValue(_raw, "Entries");
                IEnumerable enumerable = entries as IEnumerable;
                if (enumerable == null)
                    yield break;

                foreach (object entry in enumerable)
                {
                    if (entry != null)
                        yield return new DbpuSteamItem(entry);
                }
            }
        }
    }

    public static class DbpuSteamGate
    {
        const string AsmFileName = "Facepunch.Steamworks.Win64.dll";
        const string AsmSimpleName = "Facepunch.Steamworks.Win64";

        static readonly object Sync = new object();
        static Assembly _asm;
        static Type _steamClient;
        static Type _steamUtils;
        static Type _steamFriends;
        static Type _item;
        static Type _editor;
        static Type _query;
        static Type _publishedFileId;
        static bool _tried;
        static Exception _loadError;

        public static bool TryEnsure(out string error)
        {
            error = null;
            if (_asm != null) return true;
            lock (Sync)
            {
                if (_asm != null) return true;
                if (_tried)
                {
                    error = _loadError != null ? _loadError.Message : "Steamworks assembly not loaded.";
                    return false;
                }
                _tried = true;
                try
                {
                    Load();
                    return true;
                }
                catch (Exception ex)
                {
                    _loadError = ex;
                    error = ex.Message;
                    return false;
                }
            }
        }

        static void Ensure()
        {
            string error;
            if (!TryEnsure(out error))
                throw new InvalidOperationException(error ?? "Steamworks unavailable.");
        }

        static void Load()
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (string.Equals(a.GetName().Name, AsmSimpleName, StringComparison.OrdinalIgnoreCase))
                    {
                        _asm = a;
                        break;
                    }
                }
                catch { }
            }

            if (_asm == null)
            {
                string path = Path.Combine(Application.dataPath, "Managed", AsmFileName);
                if (!File.Exists(path))
                    throw new FileNotFoundException("Steamworks DLL not found at " + path);
                _asm = Assembly.LoadFrom(path);
            }

            _steamClient = _asm.GetType("Steamworks.SteamClient", true);
            _steamUtils = _asm.GetType("Steamworks.SteamUtils", true);
            _steamFriends = _asm.GetType("Steamworks.SteamFriends", true);
            _item = _asm.GetType("Steamworks.Ugc.Item", true);
            _editor = _asm.GetType("Steamworks.Ugc.Editor", true);
            _query = _asm.GetType("Steamworks.Ugc.Query", true);
            _publishedFileId = _asm.GetType("Steamworks.Data.PublishedFileId", true);
        }

        static object GetStaticProp(Type type, string name)
        {
            PropertyInfo prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
            if (prop == null)
                throw new MissingMemberException(type.FullName, name);
            return prop.GetValue(null, null);
        }

        public static bool IsOverlayEnabled
        {
            get
            {
                string error;
                if (!TryEnsure(out error)) return false;
                return Convert.ToBoolean(GetStaticProp(_steamUtils, "IsOverlayEnabled"));
            }
        }

        public static void OpenWebOverlay(string url, bool modal)
        {
            Ensure();
            MethodInfo method = _steamFriends.GetMethod("OpenWebOverlay", BindingFlags.Public | BindingFlags.Static);
            method.Invoke(null, new object[] { url, modal });
        }

        public static bool IsValid
        {
            get
            {
                string error;
                if (!TryEnsure(out error)) return false;
                return Convert.ToBoolean(GetStaticProp(_steamClient, "IsValid"));
            }
        }

        public static bool IsLoggedOn
        {
            get
            {
                string error;
                if (!TryEnsure(out error)) return false;
                return Convert.ToBoolean(GetStaticProp(_steamClient, "IsLoggedOn"));
            }
        }

        public static string Name
        {
            get
            {
                string error;
                if (!TryEnsure(out error) || !IsValid) return "Unknown";
                object name = GetStaticProp(_steamClient, "Name");
                return name != null ? Convert.ToString(name) : "Unknown";
            }
        }

        public static uint AppId
        {
            get
            {
                Ensure();
                object appId = GetStaticProp(_steamClient, "AppId");
                return GetUIntValue(appId);
            }
        }

        public static object SteamId
        {
            get
            {
                Ensure();
                return GetStaticProp(_steamClient, "SteamId");
            }
        }

        static object MakePublishedFileId(ulong id)
        {
            object pfid = Activator.CreateInstance(_publishedFileId);
            FieldInfo valueField = _publishedFileId.GetField("Value", BindingFlags.Public | BindingFlags.Instance);
            if (valueField == null)
                throw new MissingFieldException(_publishedFileId.FullName, "Value");
            valueField.SetValue(pfid, id);
            return pfid;
        }

        public static object StartGetItem(ulong id)
        {
            Ensure();
            MethodInfo method = null;
            foreach (MethodInfo mi in _item.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (mi.Name != "GetAsync") continue;
                ParameterInfo[] ps = mi.GetParameters();
                if (ps.Length >= 1 && ps[0].ParameterType == _publishedFileId)
                {
                    method = mi;
                    break;
                }
            }
            if (method == null)
                throw new MissingMethodException("Steamworks.Ugc.Item.GetAsync");

            ParameterInfo[] parameters = method.GetParameters();
            object[] args = new object[parameters.Length];
            args[0] = MakePublishedFileId(id);
            for (int i = 1; i < parameters.Length; i++)
            {
                if (parameters[i].IsOptional)
                    args[i] = parameters[i].DefaultValue;
                else if (parameters[i].ParameterType == typeof(int))
                    args[i] = 1800;
                else
                    args[i] = null;
            }

            return method.Invoke(null, args);
        }

        public static DbpuSteamEditor NewCommunityFile
        {
            get
            {
                Ensure();
                return new DbpuSteamEditor(GetStaticProp(_editor, "NewCommunityFile"));
            }
        }

        public static DbpuSteamQuery QueryAll
        {
            get
            {
                Ensure();
                return new DbpuSteamQuery(GetStaticProp(_query, "All"));
            }
        }

        public static bool IsTaskCompleted(object taskObj)
        {
            if (taskObj == null) return true;
            Task task = taskObj as Task;
            if (task != null) return task.IsCompleted;
            try
            {
                PropertyInfo prop = taskObj.GetType().GetProperty("IsCompleted", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                    return Convert.ToBoolean(prop.GetValue(taskObj, null));
            }
            catch { }
            return true;
        }

        public static bool IsTaskFaulted(object taskObj)
        {
            if (taskObj == null) return false;
            Task task = taskObj as Task;
            if (task != null) return task.IsFaulted;
            try
            {
                PropertyInfo prop = taskObj.GetType().GetProperty("IsFaulted", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                    return Convert.ToBoolean(prop.GetValue(taskObj, null));
            }
            catch { }
            return false;
        }

        public static Exception GetTaskException(object taskObj)
        {
            Task task = taskObj as Task;
            if (task != null)
            {
                if (task.Exception == null) return null;
                return task.Exception.GetBaseException();
            }
            try
            {
                PropertyInfo prop = taskObj.GetType().GetProperty("Exception", BindingFlags.Public | BindingFlags.Instance);
                object ex = prop != null ? prop.GetValue(taskObj, null) : null;
                AggregateException agg = ex as AggregateException;
                if (agg != null) return agg.GetBaseException();
                return ex as Exception;
            }
            catch
            {
                return null;
            }
        }

        public static object ReadTaskResult(object taskObj)
        {
            if (taskObj == null) return null;

            Task task = taskObj as Task;
            if (task != null)
            {
                if (!task.IsCompleted)
                    throw new InvalidOperationException("Task is not completed.");
                if (task.IsFaulted)
                    throw task.Exception != null ? task.Exception.GetBaseException() : new Exception("Task faulted.");
                if (task.IsCanceled)
                    throw new TaskCanceledException();
            }
            else if (!IsTaskCompleted(taskObj))
            {
                throw new InvalidOperationException("Task is not completed.");
            }
            else if (IsTaskFaulted(taskObj))
            {
                Exception error = GetTaskException(taskObj);
                throw error ?? new Exception("Task faulted.");
            }

            Type type = taskObj.GetType();
            if (!type.IsGenericType)
                return null;

            PropertyInfo resultProp = type.GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
            if (resultProp == null) return null;

            MethodInfo getter = resultProp.GetGetMethod();
            if (getter == null) return null;

            object raw;
            try
            {
                raw = getter.Invoke(taskObj, null);
            }
            catch (TypeLoadException tle)
            {
                throw new InvalidOperationException(
                    "Steam workshop result type failed to load. Restart PPG through Steam and try again.",
                    tle);
            }
            catch (TargetInvocationException tie)
            {
                Exception inner = tie.InnerException ?? tie;
                if (inner is TypeLoadException)
                {
                    throw new InvalidOperationException(
                        "Steam workshop result type failed to load. Restart PPG through Steam and try again.",
                        inner);
                }
                throw inner;
            }

            return UnwrapNullable(raw);
        }

        public static DbpuSteamResultPage ReadResultPage(object taskObj)
        {
            object result = ReadTaskResult(taskObj);
            if (result == null) return null;
            return new DbpuSteamResultPage(result);
        }

        public static DbpuSteamItem ReadItem(object taskObj)
        {
            object result = ReadTaskResult(taskObj);
            if (result == null) return null;
            return new DbpuSteamItem(result);
        }

        public static DbpuSteamPublishResult ReadPublishResult(object taskObj)
        {
            object result = ReadTaskResult(taskObj);
            if (result == null) return null;
            return new DbpuSteamPublishResult(result);
        }

        public static bool ReadBoolResult(object taskObj)
        {
            object result = ReadTaskResult(taskObj);
            return result != null && Convert.ToBoolean(result);
        }

        internal static object GetMemberValue(object target, string name)
        {
            if (target == null) return null;
            Type type = target.GetType();
            PropertyInfo prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
                return prop.GetValue(target, null);
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
                return field.GetValue(target);
            throw new MissingMemberException(type.FullName, name);
        }

        internal static string GetStringProp(object target, string name)
        {
            object value = GetMemberValue(target, name);
            return value != null ? Convert.ToString(value) : null;
        }

        internal static bool GetBoolMember(object target, string name)
        {
            object value = GetMemberValue(target, name);
            return value != null && Convert.ToBoolean(value);
        }

        internal static uint GetUIntMember(object target, string name)
        {
            object value = GetMemberValue(target, name);
            return value == null ? 0u : Convert.ToUInt32(value);
        }

        internal static ulong GetUlongMember(object target, string name)
        {
            object value = GetMemberValue(target, name);
            return value == null ? 0UL : Convert.ToUInt64(value);
        }

        internal static ulong GetUlongValueMember(object target, string name)
        {
            object value = GetMemberValue(target, name);
            return GetUlongValue(value);
        }

        static uint GetUIntValue(object value)
        {
            if (value == null) return 0u;
            if (value is uint) return (uint)value;
            if (value is int) return (uint)(int)value;
            if (value is ulong) return (uint)(ulong)value;
            object nested = GetMemberValue(value, "Value");
            return nested == null ? Convert.ToUInt32(value) : Convert.ToUInt32(nested);
        }

        static ulong GetUlongValue(object value)
        {
            if (value == null) return 0UL;
            if (value is ulong) return (ulong)value;
            if (value is long) return (ulong)(long)value;
            if (value is uint) return (ulong)(uint)value;
            if (value is int) return (ulong)(int)value;
            try
            {
                object nested = GetMemberValue(value, "Value");
                if (nested != null)
                    return Convert.ToUInt64(nested);
            }
            catch { }
            return Convert.ToUInt64(value);
        }

        internal static object CallInstance(object target, string methodName)
        {
            return CallInstance(target, methodName, Type.EmptyTypes, new object[0]);
        }

        internal static object CallInstance(object target, string methodName, object arg0)
        {
            if (target == null) throw new ArgumentNullException("target");
            Type type = target.GetType();
            MethodInfo method = null;
            foreach (MethodInfo mi in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (mi.Name != methodName) continue;
                ParameterInfo[] ps = mi.GetParameters();
                if (ps.Length != 1) continue;
                if (arg0 == null || ps[0].ParameterType.IsInstanceOfType(arg0) || IsAssignableValue(ps[0].ParameterType, arg0))
                {
                    method = mi;
                    break;
                }
            }
            if (method == null)
                throw new MissingMethodException(type.FullName, methodName);
            return method.Invoke(target, new object[] { arg0 });
        }

        internal static object CallInstance(object target, string methodName, Type[] parameterTypes, object[] args)
        {
            if (target == null) throw new ArgumentNullException("target");
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, parameterTypes, null);
            if (method == null)
            {
                foreach (MethodInfo mi in target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (mi.Name != methodName) continue;
                    ParameterInfo[] ps = mi.GetParameters();
                    if (ps.Length != args.Length) continue;
                    method = mi;
                    break;
                }
            }
            if (method == null)
                throw new MissingMethodException(target.GetType().FullName, methodName);
            return method.Invoke(target, args);
        }

        static bool IsAssignableValue(Type parameterType, object arg)
        {
            if (arg == null) return !parameterType.IsValueType;
            Type argType = arg.GetType();
            if (parameterType.IsAssignableFrom(argType)) return true;
            if (parameterType == typeof(int) && (arg is int || arg is short || arg is byte)) return true;
            if (parameterType == typeof(bool) && arg is bool) return true;
            if (parameterType == typeof(string) && arg is string) return true;
            return false;
        }

        static object UnwrapNullable(object value)
        {
            if (value == null) return null;
            Type type = value.GetType();
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Nullable<>))
                return value;
            PropertyInfo hasValue = type.GetProperty("HasValue");
            if (hasValue != null && !(bool)hasValue.GetValue(value, null))
                return null;
            PropertyInfo prop = type.GetProperty("Value");
            return prop != null ? prop.GetValue(value, null) : value;
        }
    }
}
