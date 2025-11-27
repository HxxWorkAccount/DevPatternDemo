using System;
using System.Collections.Generic;
using UnityEngine;
using XLua;
using System.Reflection;
using System.Linq;
using DevPattern.Editor.Build;
using System.Diagnostics;

public static class XLuaConfig
{
    /*====-------------- CS -> Lua --------------====*/

    /* 生成代码（若不生成则只能反射，效率低且特定平台用不了） */
    // - 扩展方法要特别留意一下，要放独立进来
    // - 不会自动添加类的基类
    [LuaCallCSharp]
    public static IEnumerable<Type> luaCallCSharp {
        get { return unityEngineTypesForLua.Concat(customAssembliesTypesForLua); }
    }
    [LuaCallCSharp]
    public static readonly List<Type> s_luaCallCSharpWhiteList = new() {
        /* 系统类型手动注册 */
        // typeof(Type),
        typeof(object),       typeof(Array),          typeof(ValueType),  typeof(List<>),
        typeof(List<int>), typeof(List<string>), typeof(Dictionary<, >), typeof(Nullable<>), typeof(Math),
    };

    /* 只能组织代码被裁剪，但不生成代码（可以通过反射访问），推荐使用 LuaCallCSharp */
    // 该部分不用实现，之后手动添加
    [ReflectionUse]
    public static IEnumerable<Type> reflectionUse {
        get { return new List<Type>(); }
    }

    /*====-------------- Lua -> CS --------------====*/

    /* 这里注册 CS 侧的 delegate/interface */
    // - 将 Lua 函数注册到 CS 侧的 delegate/event 时需要用到
    // - 将 Lua 表注册到 CS 侧的 interface 时需要用到
    [CSharpCallLua]
    public static IEnumerable<Type> csharpCallLua {
        get { return customAssembliesTypesForCS.Concat(unityEngineTypesForCS); }
    }
    [CSharpCallLua]
    public static readonly List<Type> s_csCallLuaWhiteList = new() {
        /* 系统类型手动注册 */
        typeof(Action),  //
        typeof(Action<>),
        typeof(Func<>),
        typeof(System.Collections.IEnumerator),
    };

    /*====-------------- Generation Control --------------====*/

    [BlackList]
    public static List<List<string>> s_memberBlackList = new List<List<string>>() {
        new List<string>() { "System.Xml.XmlNodeList", "ItemOf" },
        new List<string>() { "UnityEngine.WWW", "movie" },
#if UNITY_WEBGL
        new List<string>() { "UnityEngine.WWW", "threadPriority" },
#endif
        new List<string>() { "UnityEngine.Texture2D", "alphaIsTransparency" },
        new List<string>() { "UnityEngine.Security", "GetChainOfTrustValue" },
        new List<string>() { "UnityEngine.CanvasRenderer", "onRequestRebuild" },
        new List<string>() { "UnityEngine.Light", "areaSize" },
        new List<string>() { "UnityEngine.Light", "lightmapBakeType" },
        new List<string>() { "UnityEngine.WWW", "MovieTexture" },
        new List<string>() { "UnityEngine.WWW", "GetMovieTexture" },
        new List<string>() { "UnityEngine.AnimatorOverrideController", "PerformOverrideClipListCleanup" },
#if !UNITY_WEBPLAYER
        new List<string>() { "UnityEngine.Application", "ExternalEval" },
#endif
        new List<string>() { "UnityEngine.GameObject", "networkView" },  //4.6.2 not support
        new List<string>() { "UnityEngine.Component", "networkView" },   //4.6.2 not support
        new List<string>() { "System.IO.FileInfo", "GetAccessControl", "System.Security.AccessControl.AccessControlSections" },
        new List<string>() { "System.IO.FileInfo", "SetAccessControl", "System.Security.AccessControl.FileSecurity" },
        new List<string>() { "System.IO.DirectoryInfo", "GetAccessControl", "System.Security.AccessControl.AccessControlSections" },
        new List<string>() { "System.IO.DirectoryInfo", "SetAccessControl", "System.Security.AccessControl.DirectorySecurity" },
        new List<string>(
        ) { "System.IO.DirectoryInfo", "CreateSubdirectory", "System.String", "System.Security.AccessControl.DirectorySecurity" },
        new List<string>() { "System.IO.DirectoryInfo", "Create", "System.Security.AccessControl.DirectorySecurity" },
        new List<string>() { "UnityEngine.MonoBehaviour", "runInEditMode" },
        new List<string>() { "UnityEngine.UI.Text", "OnRebuildRequested" },
    };

#if UNITY_2018_1_OR_NEWER
    [BlackList]
    public static Func<MemberInfo, bool> MethodFilter = (memberInfo) => {
        if (memberInfo.DeclaringType.IsGenericType && memberInfo.DeclaringType.GetGenericTypeDefinition() == typeof(Dictionary<, >)) {
            if (memberInfo.MemberType == MemberTypes.Constructor) {
                ConstructorInfo constructorInfo                             = memberInfo as ConstructorInfo;
                var                                          parameterInfos = constructorInfo.GetParameters();
                if (parameterInfos.Length > 0) {
                    if (typeof(System.Collections.IEnumerable).IsAssignableFrom(parameterInfos[0].ParameterType)) {
                        return true;
                    }
                }
            } else if (memberInfo.MemberType == MemberTypes.Method) {
                var methodInfo = memberInfo as MethodInfo;
                if (methodInfo.Name == "TryAdd" || methodInfo.Name == "Remove" && methodInfo.GetParameters().Length == 2) {
                    return true;
                }
            }
        }
        return false;
    };
#endif

    /* 指明一个类的部分成员不生成代码（而是通过反射访问） */
    // 相比 ReflectionUse，这个更精准（只针对特定成员）
    // 相比 BlackList，这个不会影响其他成员的代码生成
    [DoNotGen]
    public static Dictionary<Type, List<string>> doNotGen {
        get { return new(); }
    }

    /*====-------------- GC 相关 --------------====*/

    /* 用于值类型或枚举，标记后，XLua 会生成更多代码，以减少访问时的 GC 分配 */
    [GCOptimize]
    public static IEnumerable<Type> gcOptimize {
        // FIXIT == UnityEngine 和自定义程序集的 enum 全部纳入
        get {
            return new List<Type>();
        }
    }

    /* GCOptimize 不会读取非 public 成员，可以用该配置加上 */
    [AdditionalProperties]
    public static Dictionary<Type, List<string>> additionalProperties {
        get { return new(); }
    }

    /*====-------------- Hotfix --------------====*/
    // 需要先启用 HOTFIX_ENABLE 宏（Player Settings 或 Scripting Define Symbols）

    // [Hotfix]
    // static IEnumerable<Type> hotfix {
    //     get {
    //         return customAssembliesTypes;
    //     }
    // }


    //====================== Implementation ======================//


    // 获取当前有效自定义程序集的函数
    public static List<string> activeCustomAssemblies {
        get {
            var          settings = BuildControlWindow.GetWindowInstance().settings;
            List<string> results  = new();
            if (settings == null) {
                UnityEngine.Debug.LogWarning("BuildControlWindow settings is null, return empty activeCustomAssemblys");
                return results;
            }
            results.Add("Assembly-CSharp");
            results.Add("DevPattern");
            if (settings.isClient)
                results.Add("DevPattern.Client");
            if (settings.isServer)
                results.Add("DevPattern.Server");
            if (settings.isDev)
                results.Add("DevPattern.Dev");
            if (settings.isDev && settings.isClient)
                results.Add("DevPattern.Dev.Client");
            if (settings.isDev && settings.isServer)
                results.Add("DevPattern.Dev.Server");
            if (settings.isTest)
                results.Add("DevPattern.Tests");
            if (settings.isTest && settings.isClient)
                results.Add("DevPattern.Tests.Client");
            if (settings.isTest && settings.isServer)
                results.Add("DevPattern.Tests.Server");
            return results;
        }
    }

    public static IEnumerable<Type> unityEngineTypes {
        get {
            List<string> namespaces = new() { "UnityEngine", "UnityEngine.UI" };  // 有效的 Unity 命名空间
            var          unityTypes = (from assembly in AppDomain.CurrentDomain.GetAssemblies()
                                      where !(assembly.ManifestModule is System.Reflection.Emit.ModuleBuilder)
                                      from type in assembly.GetExportedTypes()
                                      where type.Namespace != null && namespaces.Contains(type.Namespace)
                                      select type);
            return unityTypes;
        }
    }
    public static IEnumerable<Type> customAssembliesTypes {
        get {
            var activeAssemblys = activeCustomAssemblies;
            var customTypes     = (from assembly in activeAssemblys.Select(s => Assembly.Load(s))
                                  from type in assembly.GetExportedTypes()
                                  where ValidNamespace(type.Namespace)
                                  select type);
            return customTypes;
        }
    }

    public static IEnumerable<Type> unityEngineTypesForLua {
        get {
            var results = (from type in unityEngineTypes where ValidTypeForLua(type) select type);
            return results;
        }
    }
    public static IEnumerable<Type> customAssembliesTypesForLua {
        get {
            var types = (from type in customAssembliesTypes where ValidTypeForLua(type) select type);
            return ExtractMembers(types, (t) => ValidNamespace(t.Namespace) && ValidTypeForLua(t));
        }
    }

    public static IEnumerable<Type> unityEngineTypesForCS {
        get {
            var types = (from type in unityEngineTypes where ValidTypeForCS(type) select type);
            Func<Type, bool> filter = (t
            )     => ValidNamespace(t.Namespace) && ValidTypeForCS(t) && !DelegateHasEditorRef(t) && !DelegateHasAnonymousType(t);
            types = ExtractMembers(types, filter);
            return ExtractGenericTypes(types, filter);
        }
    }
    public static IEnumerable<Type> customAssembliesTypesForCS {
        get {
            var types = (from type in customAssembliesTypes where ValidTypeForCS(type) select type);
            Func<Type, bool> filter = (t
            )     => ValidNamespace(t.Namespace) && ValidTypeForCS(t) && !DelegateHasEditorRef(t) && !DelegateHasAnonymousType(t);
            types = ExtractMembers(types, filter);
            return ExtractGenericTypes(types, filter);
        }
    }

    /*====-------------- 筛选工具 --------------====*/

    private static bool ValidNamespace(string ns) {
        if (ns == null)
            return true;
        if (ns.StartsWith("XLua"))
            return false;
        if (ns.Contains("Editor"))
            return false;
        if (ns.Contains("Lua"))
            return false;
        if (ns.StartsWith("System"))
            return true;
        if (ns.StartsWith("UnityEngine"))
            return true;
        if (ns.StartsWith("DevPattern"))
            return true;
        return false;
    }

    /* LuaCallCSharp 用到的类型 */
    private static bool ValidTypeForLua(Type type) {
        if (type.BaseType == typeof(MulticastDelegate))
            return false;
        if (type.IsInterface)
            return false;
        if (type.IsEnum)
            return false;
        return !IsExcludedForLua(type);
    }

    private static bool IsExcludedForLua(Type type) {
        var fullName = type.FullName;
        if (string.IsNullOrEmpty(fullName))
            return false;
        for (int i = 0; i < s_excludeLuaCallCSharp.Count; i++) {
            if (fullName.Contains(s_excludeLuaCallCSharp[i])) {
                return true;
            }
        }
        return false;
    }

    /* CSharpCallLua 用到的类型 */
    private static bool ValidTypeForCS(Type type) {
        if (type.BaseType == typeof(MulticastDelegate))
            return !IsExcludedForCS(type);
        if (type.IsInterface)
            return !IsExcludedForCS(type);
        if (type.IsEnum)
            return !IsExcludedForCS(type);
        return false;
    }

    static bool IsExcludedForCS(Type type) {
        var fullName = type.FullName;
        if (string.IsNullOrEmpty(fullName))
            return false;
        for (int i = 0; i < s_excludeCSharpCallLua.Count; i++) {
            if (fullName.Contains(s_excludeCSharpCallLua[i])) {
                return true;
            }
        }
        return false;
    }

    /* 如果类型与 UnityEditor 代码相关，则剔除 */

    static bool HasEditorRef(Type type) {
        if (type.Namespace != null && (type.Namespace == "UnityEditor" || type.Namespace.StartsWith("UnityEditor."))) {
            return true;
        }
        if (type.IsNested) {
            return HasEditorRef(type.DeclaringType);
        }
        if (type.IsByRef || type.IsArray) {
            return HasEditorRef(type.GetElementType());
        }
        if (type.IsGenericType) {
            foreach (var typeArg in type.GetGenericArguments()) {
                if (typeArg.IsGenericParameter) {
                    //skip unsigned type parameter
                    continue;
                }
                if (HasEditorRef(typeArg)) {
                    return true;
                }
            }
        }
        return false;
    }
    static bool DelegateHasEditorRef(Type delegateType) {
        if (HasEditorRef(delegateType))
            return true;
        var method = delegateType.GetMethod("Invoke");
        if (method == null) {
            return false;
        }
        if (HasEditorRef(method.ReturnType))
            return true;
        return method.GetParameters().Any(pinfo => HasEditorRef(pinfo.ParameterType));
    }

    /* 排除匿名类型 */

    static bool IsAnonymousType(Type type) {
        if (type == null)
            return false;
        return Attribute.IsDefined(type, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false) && type.IsGenericType &&
               type.Name.Contains("AnonymousType") && (type.Name.StartsWith("<>") || type.Name.StartsWith("VB$")) &&
               (type.Attributes & TypeAttributes.NotPublic) == TypeAttributes.NotPublic;
    }
    static bool DelegateHasAnonymousType(Type delegateType) {
        if (IsAnonymousType(delegateType))
            return true;
        var method = delegateType.GetMethod("Invoke");
        if (method == null)
            return false;
        if (IsAnonymousType(method.ReturnType))
            return true;
        foreach (var p in method.GetParameters()) {
            if (IsAnonymousType(p.ParameterType))
                return true;
        }
        return false;
    }

    /* 提取成员 */

    private static IEnumerable<Type> ExtractMembers(IEnumerable<Type> types, Func<Type, bool> filter) {
        HashSet<Type> uniqueTypes = new();
        HashSet<Type> tempTypes   = new();
        foreach (var type in types) {
            if (uniqueTypes.Contains(type) || !filter(type))
                continue;
            bool validMembers = true;
            tempTypes.Clear();
            tempTypes.Add(type);
            // 遍历 public 成员的，包括：函数的返回值类型和参数类型、属性和字段的类型，将其加入
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var field in type.GetFields(flags)) {
                if (!filter(field.FieldType)) {
                    validMembers = false;
                    break;
                }
                if (uniqueTypes.Contains(field.FieldType))
                    continue;
                if (!field.FieldType.IsGenericTypeParameter)
                    tempTypes.Add(field.FieldType);
            }
            foreach (var prop in type.GetProperties(flags)) {
                if (!filter(prop.PropertyType)) {
                    validMembers = false;
                    break;
                }
                if (uniqueTypes.Contains(prop.PropertyType))
                    continue;
                if (!prop.PropertyType.IsGenericTypeParameter)
                    tempTypes.Add(prop.PropertyType);
            }
            foreach (var method in type.GetMethods(flags)) {
                if (method.IsGenericMethodDefinition) {
                    validMembers = false;
                    break;
                }
                if (!filter(method.ReturnType)) {
                    validMembers = false;
                    break;
                }
                if (uniqueTypes.Contains(method.ReturnType))
                    continue;
                if (!method.ReturnType.IsGenericParameter)
                    tempTypes.Add(method.ReturnType);
                foreach (var param in method.GetParameters()) {
                    var paramType = param.ParameterType;
                    if (paramType.IsByRef)
                        paramType = paramType.GetElementType();
                    if (!filter(paramType)) {
                        validMembers = false;
                        break;
                    }
                    if (uniqueTypes.Contains(paramType))
                        continue;
                    if (!paramType.IsGenericParameter)
                        tempTypes.Add(paramType);
                }
            }
            if (validMembers) {
                foreach (var t in tempTypes)
                    uniqueTypes.Add(t);
            }
        }
        return uniqueTypes;
    }

    /* 泛型处理相关 */

    private static IEnumerable<Type> ExtractGenericTypes(IEnumerable<Type> types, Func<Type, bool> filter) {
        HashSet<Type> uniqueTypes = new();
        foreach (var type in types) {
            foreach (var t in ExtractGenericTypesHelper(type, filter, uniqueTypes))
                uniqueTypes.Add(t);
        }
        return uniqueTypes;
    }
    private static IEnumerable<Type> ExtractGenericTypesHelper(Type type, Func<Type, bool> filter, HashSet<Type> addedTypes) {
        if (type == null || type.IsGenericTypeDefinition || type.IsGenericParameter)
            yield break;
        if (type.IsByRef || type.IsArray)
            type = type.GetElementType();
        if (addedTypes.Contains(type) || !filter(type))
            yield break;
        yield return type;
        if (type.IsGenericType) {
            foreach (var arg in type.GetGenericArguments()) {
                foreach (var t in ExtractGenericTypesHelper(arg, filter, addedTypes)) {
                    yield return t;
                }
            }
        }
    }


    //====================== Hardcode ======================//

    static readonly List<string> s_excludeLuaCallCSharp = new List<string> {
        "HideInInspector",
        "ExecuteInEditMode",
        "AddComponentMenu",
        "ContextMenu",
        "RequireComponent",
        "DisallowMultipleComponent",
        "SerializeField",
        "AssemblyIsEditorAssembly",
        "Attribute",
        "Types",
        "UnitySurrogateSelector",
        "TrackedReference",
        "TypeInferenceRules",
        "FFTWindow",
        "RPC",
        "Network",
        "MasterServer",
        "BitStream",
        "HostData",
        "ConnectionTesterStatus",
        "GUI",
        "EventType",
        "EventModifiers",
        "FontStyle",
        "TextAlignment",
        "TextEditor",
        "TextEditorDblClickSnapping",
        "TextGenerator",
        "TextClipping",
        "Gizmos",
        "ADBannerView",
        "ADInterstitialAd",
        "Android",
        "Tizen",
        "jvalue",
        "iPhone",
        "iOS",
        "Windows",
        "CalendarIdentifier",
        "CalendarUnit",
        "CalendarUnit",
        "ClusterInput",
        "FullScreenMovieControlMode",
        "FullScreenMovieScalingMode",
        "Handheld",
        "LocalNotification",
        "NotificationServices",
        "RemoteNotificationType",
        "RemoteNotification",
        "SamsungTV",
        "TextureCompressionQuality",
        "TouchScreenKeyboardType",
        "TouchScreenKeyboard",
        "MovieTexture",
        "UnityEngineInternal",
        "Terrain",
        "Tree",
        "SplatPrototype",
        "DetailPrototype",
        "DetailRenderMode",
        "MeshSubsetCombineUtility",
        "AOT",
        "Social",
        "Enumerator",
        "SendMouseEvents",
        "Cursor",
        "Flash",
        "ActionScript",
        "OnRequestRebuild",
        "Ping",
        "ShaderVariantCollection",
        "SimpleJson.Reflection",
        "CoroutineTween",
        "GraphicRebuildTracker",
        "Advertisements",
        "UnityEditor",
        "WSA",
        "EventProvider",
        "Apple",
        "ClusterInput",
        "Motion",
        "UnityEngine.UI.ReflectionMethodsCache",
        "NativeLeakDetection",
        "NativeLeakDetectionMode",
        "WWWAudioExtensions",
        "UnityEngine.Experimental",

        "System.Void",

        // 报错太多，从自动自动生成代码里屏蔽了，如果有需要再手动添加吧
        "UnityEngine.AnimatorControllerParameter",
        "UnityEngine.DrivenRectTransformTracker",
        "UnityEngine.Light",
        "UnityEngine.Texture",
        "UnityEngine.MeshRenderer",
        "UnityEngine.ParticleSystem",
        "UnityEngine.TextureMipmapLimitGroups",
        "UnityEngine.UI.Graphic",
        "UnityEngine.UI.DefaultControls",
        "UnityEngine.LightingSettings",
        "UnityEngine.AudioSettings",
        "UnityEngine.AudioSource",
        "UnityEngine.Caching",
        "UnityEngine.Input",
        "UnityEngine.Material",
        "UnityEngine.QualitySettings",
        "UnityEngine.LightProbeGroup",
        "RuleTile",
        "CodeGeneratedRegistry",
    };

    private static readonly List<string> s_excludeCSharpCallLua = new List<string> {
        "UnityEngine.CanvasRenderer.OnRequestRebuild",
        "UnityEngine.Application.MemoryUsageChangedCallback",
    };
}
