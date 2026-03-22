using System.Reflection;
using System.Runtime.Loader;

namespace Jellyfin.Plugin.Oscars.Infrastructure;

/// <summary>
/// Optionally registers the Oscar badge script with the JavaScript Injector plugin.
/// </summary>
public static class JavaScriptInjectorRegistration
{
    private const string InjectorAssemblyName = "Jellyfin.Plugin.JavaScriptInjector";
    private const string PluginInterfaceTypeName = "Jellyfin.Plugin.JavaScriptInjector.PluginInterface";
    private const string RegisterScriptMethodName = "RegisterScript";
    private const string UnregisterScriptMethodName = "UnregisterScript";
    private const string OscarBadgeScriptRelativePath = "wwwroot/scripts/oscarDetailBadge.js";

    public static void TryRegisterOscarBadgeScript(string assemblyFilePath, Guid pluginId, string pluginName, Version pluginVersion)
    {
        try
        {
            var injectorAssembly = AssemblyLoadContext.All
                .SelectMany(loadContext => loadContext.Assemblies)
                .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, InjectorAssemblyName, StringComparison.Ordinal));

            if (injectorAssembly is null)
            {
                Log("JavaScript Injector plugin not detected. Keeping manual injection fallback.");
                return;
            }

            Log("JavaScript Injector plugin detected. Attempting Oscar badge script registration.");
            var pluginInterfaceType = injectorAssembly.GetType(PluginInterfaceTypeName);
            if (pluginInterfaceType is null)
            {
                Log("JavaScript Injector registration failed because PluginInterface was not found.");
                return;
            }

            var registerScriptMethod = pluginInterfaceType.GetMethod(RegisterScriptMethodName, BindingFlags.Public | BindingFlags.Static);
            if (registerScriptMethod is null)
            {
                Log("JavaScript Injector registration failed because RegisterScript was not found.");
                return;
            }

            var scriptDirectory = Path.GetDirectoryName(assemblyFilePath);
            if (string.IsNullOrWhiteSpace(scriptDirectory))
            {
                Log("JavaScript Injector registration failed because the plugin directory could not be determined.");
                return;
            }

            var scriptPath = Path.Combine(scriptDirectory, OscarBadgeScriptRelativePath);
            if (!File.Exists(scriptPath))
            {
                Log($"JavaScript Injector registration failed because the Oscar badge script was not found at {scriptPath}.");
                return;
            }

            var scriptId = $"{pluginId}-oscar-detail-badge";
            pluginInterfaceType
                .GetMethod(UnregisterScriptMethodName, BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, [scriptId]);

            var registrationPayload = CreateRegistrationPayload(
                injectorAssembly,
                scriptId,
                File.ReadAllText(scriptPath),
                pluginId,
                pluginName,
                pluginVersion);
            if (registrationPayload is null)
            {
                Log("JavaScript Injector registration failed because a JObject payload could not be created.");
                return;
            }

            var registrationResult = registerScriptMethod.Invoke(null, [registrationPayload]);
            if (registrationResult is bool success && success)
            {
                Log("Successfully registered Oscar badge script with JavaScript Injector.");
                return;
            }

            Log("JavaScript Injector registration failed. Keeping manual injection fallback.");
        }
        catch (Exception ex)
        {
            Log($"JavaScript Injector registration failed with an exception: {ex.Message}");
        }
    }

    private static void Log(string message)
    {
        Console.WriteLine($"[Jellyfin Oscars] {message}");
    }

    private static object? CreateRegistrationPayload(Assembly injectorAssembly, string scriptId, string scriptContents, Guid pluginId, string pluginName, Version pluginVersion)
    {
        var newtonsoftAssembly = injectorAssembly.GetReferencedAssemblies()
            .Select(Assembly.Load)
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "Newtonsoft.Json", StringComparison.Ordinal));
        if (newtonsoftAssembly is null)
        {
            return null;
        }

        var jObjectType = newtonsoftAssembly.GetType("Newtonsoft.Json.Linq.JObject");
        var jTokenType = newtonsoftAssembly.GetType("Newtonsoft.Json.Linq.JToken");
        var jValueType = newtonsoftAssembly.GetType("Newtonsoft.Json.Linq.JValue");
        var addMethod = jObjectType?.GetMethod("Add", [typeof(string), jTokenType!]);
        if (jObjectType is null || jTokenType is null || jValueType is null || addMethod is null)
        {
            return null;
        }

        var payload = Activator.CreateInstance(jObjectType);
        if (payload is null)
        {
            return null;
        }

        AddValue(payload, addMethod, jValueType, "id", scriptId);
        AddValue(payload, addMethod, jValueType, "name", "Oscar Detail Badge");
        AddValue(payload, addMethod, jValueType, "script", scriptContents);
        AddValue(payload, addMethod, jValueType, "enabled", true);
        AddValue(payload, addMethod, jValueType, "requiresAuthentication", false);
        AddValue(payload, addMethod, jValueType, "pluginId", pluginId.ToString());
        AddValue(payload, addMethod, jValueType, "pluginName", pluginName);
        AddValue(payload, addMethod, jValueType, "pluginVersion", pluginVersion.ToString());
        return payload;
    }

    private static void AddValue(object payload, MethodInfo addMethod, Type jValueType, string propertyName, object value)
    {
        var token = Activator.CreateInstance(jValueType, value);
        addMethod.Invoke(payload, [propertyName, token!]);
    }
}
