using System.Reflection;
using System.Runtime.Loader;
using Jellyfin.Plugin.Oscars.Models;

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

    public static FrontendBadgeIntegrationStatus TryRegisterOscarBadgeScript(string assemblyFilePath, Guid pluginId, string pluginName, Version pluginVersion)
    {
        try
        {
            var injectorAssembly = AssemblyLoadContext.All
                .SelectMany(loadContext => loadContext.Assemblies)
                .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, InjectorAssemblyName, StringComparison.Ordinal));

            if (injectorAssembly is null)
            {
                const string message = "Inactive: JavaScript Injector plugin not installed. Install it to enable Oscars badges in Jellyfin Web.";
                LogWarning(message);
                return new FrontendBadgeIntegrationStatus
                {
                    State = FrontendBadgeIntegrationState.MissingDependency,
                    Message = message
                };
            }

            LogInfo("JavaScript Injector plugin detected. Attempting Oscar badge script registration.");
            var pluginInterfaceType = injectorAssembly.GetType(PluginInterfaceTypeName);
            if (pluginInterfaceType is null)
            {
                return CreateFailureStatus("Inactive: JavaScript Injector plugin was detected, but its PluginInterface type was not found.");
            }

            var registerScriptMethod = pluginInterfaceType.GetMethod(RegisterScriptMethodName, BindingFlags.Public | BindingFlags.Static);
            if (registerScriptMethod is null)
            {
                return CreateFailureStatus("Inactive: JavaScript Injector plugin was detected, but its RegisterScript API was not found.");
            }

            var scriptDirectory = Path.GetDirectoryName(assemblyFilePath);
            if (string.IsNullOrWhiteSpace(scriptDirectory))
            {
                return CreateFailureStatus("Inactive: Oscar badge registration failed because the plugin directory could not be determined.");
            }

            var scriptPath = Path.Combine(scriptDirectory, OscarBadgeScriptRelativePath);
            if (!File.Exists(scriptPath))
            {
                return CreateFailureStatus($"Inactive: Oscar badge registration failed because the badge script was not found at {scriptPath}.");
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
                return CreateFailureStatus("Inactive: Oscar badge registration failed because the JavaScript Injector payload could not be created.");
            }

            var registrationResult = registerScriptMethod.Invoke(null, [registrationPayload]);
            if (registrationResult is bool success && success)
            {
                const string message = "Active: JavaScript Injector detected and Oscars badge script registered.";
                LogInfo(message);
                return new FrontendBadgeIntegrationStatus
                {
                    State = FrontendBadgeIntegrationState.Active,
                    Message = message
                };
            }

            return CreateFailureStatus("Inactive: JavaScript Injector was detected, but Oscar badge script registration did not succeed.");
        }
        catch (Exception ex)
        {
            return CreateFailureStatus($"Inactive: Oscar badge registration failed with an exception: {ex.Message}");
        }
    }

    private static FrontendBadgeIntegrationStatus CreateFailureStatus(string message)
    {
        LogWarning(message);
        return new FrontendBadgeIntegrationStatus
        {
            State = FrontendBadgeIntegrationState.RegistrationFailed,
            Message = message
        };
    }

    private static void LogInfo(string message)
    {
        Console.WriteLine($"[Jellyfin Oscars] INFO: {message}");
    }

    private static void LogWarning(string message)
    {
        Console.WriteLine($"[Jellyfin Oscars] WARN: {message}");
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
