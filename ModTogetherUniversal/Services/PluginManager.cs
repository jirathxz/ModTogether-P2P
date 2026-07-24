using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ModTogether.API;

namespace ModTogetherUniversal.Services
{
    public class PluginManager
    {
        private static PluginManager? _instance;
        public static PluginManager Instance => _instance ??= new PluginManager();

        public List<IModPlugin> LoadedPlugins { get; private set; } = new();
        public Action<string>? OnLog { get; set; }
        private Dictionary<string, Assembly> _loadedAssemblies = new();

        public PluginManager()
        {
            // Pre-populate with all assemblies currently in AppDomain (such as ModTogether.API)
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var asmName = asm.GetName().Name;
                if (!string.IsNullOrEmpty(asmName)) _loadedAssemblies[asmName] = asm;
                if (!string.IsNullOrEmpty(asm.FullName)) _loadedAssemblies[asm.FullName] = asm;
            }

            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var name = new AssemblyName(args.Name).Name;
                if (name != null && _loadedAssemblies.TryGetValue(name, out var asm))
                    return asm;
                if (_loadedAssemblies.TryGetValue(args.Name, out var asmFull))
                    return asmFull;

                var existing = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase));
                if (existing != null) return existing;

                return null;
            };

            System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (context, args) =>
            {
                if (args.Name != null && _loadedAssemblies.TryGetValue(args.Name, out var asm))
                    return asm;
                var simpleName = args.Name;
                if (simpleName != null && _loadedAssemblies.TryGetValue(simpleName, out var asmSimple))
                    return asmSimple;

                if (simpleName != null)
                {
                    var existing = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => string.Equals(a.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase));
                    if (existing != null) return existing;
                }

                return null;
            };

            App.Settings.OnSettingsChanged += () => 
            {
                foreach (var ext in LoadedPlugins)
                {
                    ext.SetLanguage(App.Settings.Current.Language);
                }
            };
        }

        public string GetPluginsPath()
        {
            string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string pluginsPath = Path.Combine(docsPath, "ModTogether", "Plugins");
            Directory.CreateDirectory(pluginsPath);
            return pluginsPath;
        }

        public void LoadPlugins()
        {
            LoadedPlugins.Clear();
            string userPluginsPath = GetPluginsPath();
            string basePath = Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;
            string localPluginsPath = Path.Combine(basePath, "Plugins");
            string localExtPath = Path.Combine(basePath, "Extensions");

            var dlls = new List<string>();

            // 1. Documents/ModTogether/Plugins
            if (Directory.Exists(userPluginsPath))
            {
                dlls.AddRange(Directory.GetFiles(userPluginsPath, "*.dll", SearchOption.AllDirectories));
            }

            // 2. Local Plugins folder
            if (Directory.Exists(localPluginsPath))
            {
                foreach (var file in Directory.GetFiles(localPluginsPath, "*.dll", SearchOption.AllDirectories))
                {
                    if (!dlls.Contains(file)) dlls.Add(file);
                }
            }

            // 3. Local Extensions folder
            if (Directory.Exists(localExtPath))
            {
                foreach (var file in Directory.GetFiles(localExtPath, "*.dll", SearchOption.AllDirectories))
                {
                    if (!dlls.Contains(file)) dlls.Add(file);
                }
            }

            // 4. Root directory (useful for local debugging in VS)
            foreach (var file in Directory.GetFiles(basePath, "ModTogether.Extensions.*.dll"))
            {
                if (!dlls.Contains(file)) dlls.Add(file);
            }

            OnLog?.Invoke($"[DEBUG] basePath: {basePath}, userPluginsPath: {userPluginsPath}, dlls found: {dlls.Count}");

            var assembliesToProcess = new List<Assembly>();

            foreach (var file in dlls)
            {
                if (Path.GetFileName(file).Equals("ModTogether.API.dll", StringComparison.OrdinalIgnoreCase)) continue;
                string simpleName = Path.GetFileNameWithoutExtension(file);

                Assembly? assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase));

                if (assembly == null)
                {
                    try
                    {
                        byte[] rawAssembly = System.IO.File.ReadAllBytes(file);
                        assembly = Assembly.Load(rawAssembly);
                        OnLog?.Invoke($"[DEBUG] Loaded {simpleName} via Assembly.Load(byte[])");
                        
                        // Register for WPF URI Resolution
                        if (!string.IsNullOrEmpty(assembly.FullName) && !_loadedAssemblies.ContainsKey(assembly.FullName))
                        {
                            _loadedAssemblies[assembly.FullName] = assembly;
                            if (assembly.GetName().Name != null)
                                _loadedAssemblies[assembly.GetName().Name!] = assembly;
                        }
                    }
                    catch (Exception ex)
                    {
                        OnLog?.Invoke($"Failed to load assembly from {file}: {ex.Message}");
                    }
                }
                else
                {
                    OnLog?.Invoke($"[DEBUG] Found {simpleName} in AppDomain already");
                }

                if (assembly != null && !assembliesToProcess.Contains(assembly))
                {
                    assembliesToProcess.Add(assembly);
                }
            }

            // Also include any assemblies already loaded into AppDomain that match extension naming
            foreach (var loadedAsm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var asmName = loadedAsm.GetName().Name;
                if (asmName != null && asmName.StartsWith("ModTogether.Extensions.", StringComparison.OrdinalIgnoreCase))
                {
                    if (!assembliesToProcess.Contains(loadedAsm))
                    {
                        assembliesToProcess.Add(loadedAsm);
                    }
                }
            }

            foreach (var assembly in assembliesToProcess)
            {
                try
                {
                    var asmName = assembly.GetName().Name;
                    if (!string.IsNullOrEmpty(asmName))
                    {
                        _loadedAssemblies[asmName] = assembly;
                    }
                    if (!string.IsNullOrEmpty(assembly.FullName))
                    {
                        _loadedAssemblies[assembly.FullName] = assembly;
                    }

                    var typesLog = new System.Text.StringBuilder();
                    typesLog.AppendLine($"Loaded assembly: {assembly.FullName}");
                    
                    var extensionTypes = assembly.GetTypes()
                        .Where(t => 
                        {
                            bool assignable = typeof(IModPlugin).IsAssignableFrom(t) || t.GetInterfaces().Any(i => i.FullName == typeof(IModPlugin).FullName);
                            typesLog.AppendLine($"Type: {t.FullName}, IsIModPlugin: {assignable}, IsInterface: {t.IsInterface}, IsAbstract: {t.IsAbstract}");
                            return assignable && !t.IsInterface && !t.IsAbstract;
                        }).ToList();

                    string logPath = Path.Combine(basePath, "plugin_types_debug.log");
                    System.IO.File.WriteAllText(logPath, typesLog.ToString());

                    foreach (var type in extensionTypes)
                    {
                        if (LoadedPlugins.Any(e => e.GetType() == type)) continue;

                        try
                        {
                            object? instance = Activator.CreateInstance(type);
                            if (instance != null)
                            {
                                IModPlugin extension = instance as IModPlugin ?? new PluginProxy(instance);
                                extension.SetLanguage(App.Settings.Current.Language);
                                LoadedPlugins.Add(extension);
                                OnLog?.Invoke($"Loaded Extension: {extension.Name} v{extension.Version}");
                            }
                        }
                        catch (Exception createEx)
                        {
                            OnLog?.Invoke($"Failed to create instance of {type.FullName}: {createEx.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    string msg = $"Failed to process extension assembly {assembly.FullName}: {ex.GetType().Name} - {ex.Message}";
                    if (ex is ReflectionTypeLoadException rtle)
                    {
                        foreach (var le in rtle.LoaderExceptions)
                        {
                            if (le != null) 
                            {
                                msg += $"\nLoaderException: {le.GetType().Name} - {le.Message}\n{le.StackTrace}";
                                if (le is TypeLoadException tle) msg += $"\nTypeName: {tle.TypeName}";
                            }
                        }
                    }
                    else
                    {
                        msg += $"\n{ex.StackTrace}";
                    }
                    OnLog?.Invoke(msg);
                    string errPath = Path.Combine(basePath, "plugin_load_error.log");
                    System.IO.File.WriteAllText(errPath, msg);
                }
            }
        }

        public bool IsPluginForGame(IModPlugin ext, string gameDir)
        {
            if (string.IsNullOrEmpty(gameDir)) return false;
            
            if (ext.TargetGame == "Monster Hunter: World")
            {
                return File.Exists(Path.Combine(gameDir, "MonsterHunterWorld.exe"));
            }

            return false;
        }
    }

    public class PluginProxy : ModTogether.API.IModPlugin
    {
        private readonly object _instance;
        private readonly Type _type;

        public PluginProxy(object instance)
        {
            _instance = instance;
            _type = instance.GetType();
        }

        public string Name => (string)_type.GetProperty("Name")?.GetValue(_instance)! ?? "Unknown Proxy";
        public string TargetGame => (string)_type.GetProperty("TargetGame")?.GetValue(_instance)! ?? "";
        public string Version => (string)_type.GetProperty("Version")?.GetValue(_instance)! ?? "1.0.0";
        public string Description => (string)_type.GetProperty("Description")?.GetValue(_instance)! ?? "";
        public string Author => (string)_type.GetProperty("Author")?.GetValue(_instance)! ?? "";
        public string NavigationIcon => (string)_type.GetProperty("NavigationIcon")?.GetValue(_instance)! ?? "Box24";

        public void Initialize(string gameDirectory) => _type.GetMethod("Initialize")?.Invoke(_instance, new object[] { gameDirectory });
        public void SetLanguage(string language) => _type.GetMethod("SetLanguage")?.Invoke(_instance, new object[] { language });
        public System.Windows.Controls.Page CreatePage() => (System.Windows.Controls.Page)_type.GetMethod("CreatePage")?.Invoke(_instance, null)!;
    }
}

