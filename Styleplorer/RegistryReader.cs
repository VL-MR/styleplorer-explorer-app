using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Styleplorer
{
    using Microsoft.Win32;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;

    public class RegistryReader
    {
        // Пути к ключам реестра, связанным с контекстным меню проводника
        private static readonly string[] registryPaths = new string[]
        {
            //@"*\shell",
            //@"*\shellex\ContextMenuHandlers",
            @"Directory\shell",
            @"Directory\Background\shell",
            //@"Directory\shellex\ContextMenuHandlers",
            //@"Drive\shell",
            //@"Drive\shellex\ContextMenuHandlers",
            //@"AllFileSystemObjects\shell",
            //@"AllFileSystemObjects\shellex\ContextMenuHandlers",
        };

        private static readonly HashSet<string> systemEntries = new HashSet<string>
        {
            "cmd", "find", "Powershell", "UpdateEncryptionSettings"
        };

        public List<RegistryEntry> CreateMenuItems { get; private set; } = new List<RegistryEntry>();
        public List<RegistryEntry> DirectoryShellEntries { get; private set; } = new List<RegistryEntry>();
        public List<RegistryEntry> DirectoryBackgroundShellEntries { get; private set; } = new List<RegistryEntry>();

        public void ReadCreateMenuItems()
        {
            string classesPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Discardable\PostSetup\ShellNew";
            using (RegistryKey classesKey = Registry.CurrentUser.OpenSubKey(classesPath))
            {
                if (classesKey != null)
                {
                    var classesValues = classesKey.GetValue("Classes") as string[];
                    if (classesValues != null)
                    {
                        //string[] fileTypes = classesValue.Split('\0', StringSplitOptions.RemoveEmptyEntries);
                        foreach (string fileType in classesValues)
                        {
                            ProcessCreateMenuItem(fileType);
                        }
                    }
                }
            }

            // Добавляем пункт "Создать папку"
            //CreateMenuItems.Add(new RegistryEntry
            //{
            //    Name = "Folder",
            //    MenuItemText = "Папку",
            //    Command = "CreateFolder"
            //});
        }

        private void ProcessCreateMenuItem(string fileType)
        {
            string basePath = $@"{fileType}";
            SearchForShellNewFolder(Registry.ClassesRoot, basePath, "");
        }
        private Dictionary<string, HashSet<string>> processedExtensions = new Dictionary<string, HashSet<string>>();
        private void SearchForShellNewFolder(RegistryKey baseKey, string currentPath, string accumulatedPath)
        {
            try
            {
                using (RegistryKey key = baseKey.OpenSubKey(currentPath))
                {
                    if (key == null)
                    {
                        Console.WriteLine($"Unable to open key: {currentPath}");
                        return;
                    }

                    string fullPath = string.IsNullOrEmpty(accumulatedPath) ? currentPath : Path.Combine(accumulatedPath, currentPath);

                    // Check if the current key is a ShellNew key
                    if (currentPath.EndsWith("ShellNew", StringComparison.OrdinalIgnoreCase))
                    {
                        ProcessShellNewKey(key, fullPath);
                    }

                    // Recursively search subkeys
                    foreach (string subKeyName in key.GetSubKeyNames())
                    {
                        SearchForShellNewFolder(key, subKeyName, fullPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accessing registry key {currentPath}: {ex.Message}");
            }
        }

        private void ProcessShellNewKey(RegistryKey key, string fullPath)
        {
            string fileType = fullPath.TrimStart('\\').Split('\\').FirstOrDefault();
            if (string.IsNullOrEmpty(fileType))
            {
                return;
            }

            if (!processedExtensions.ContainsKey(fileType))
            {
                processedExtensions[fileType] = new HashSet<string>();
            }
            var entry = ProcessSubKey2(key, fileType);
            if (entry != null)
            {
                if (!processedExtensions[fileType].Contains(fileType))
                {
                    if (string.IsNullOrEmpty(entry.MenuItemText))
                    {
                        entry.MenuItemText = $"Новый {fileType} файл";
                    }
                    CreateMenuItems.Add(entry);
                    processedExtensions[fileType].Add(fileType);
                }
            }
        }
        public RegistryEntry ProcessSubKey2(RegistryKey key, string fileType)
        {
            var entry = new RegistryEntry
            {
                Name = fileType
            };

            // Process ItemName
            string itemName = key.GetValue("ItemName") as string;
            string itemName2 = key.GetValue("FileName") as string;
            if (!string.IsNullOrEmpty(itemName))
            {
                entry.MenuItemText = ProcessResourceString(itemName);
            }
            else if (!string.IsNullOrEmpty(itemName2))
            {
                entry.MenuItemText = ProcessResourceString(itemName2);
            }

            // Process Command
            string command = key.GetValue("Command") as string;
            if (!string.IsNullOrEmpty(command))
            {
                entry.Command = command;
            }

            // Process IconPath
            string iconPath = key.GetValue("IconPath") as string;
            if (!string.IsNullOrEmpty(iconPath))
            {
                entry.IconPath = iconPath;
            }
            else if (!string.IsNullOrEmpty(itemName))
            {
                string[] parts = itemName.Substring(1).Split(new char[] { ',' }, 2);
                string filePath = Environment.ExpandEnvironmentVariables(parts[0]);
                entry.IconPath = filePath;
            }
            else if (!string.IsNullOrEmpty(itemName2))
            {
                string[] parts = itemName2.Substring(1).Split(new char[] { ',' }, 2);
                string filePath = Environment.ExpandEnvironmentVariables(parts[0]);
                entry.IconPath = filePath;
            }

            // Process MenuText (if ItemName is not set)
            if (string.IsNullOrEmpty(entry.MenuItemText))
            {
                string menuText = key.GetValue("MenuText") as string;
                if (!string.IsNullOrEmpty(menuText))
                {
                    entry.MenuItemText = ProcessResourceString(menuText);
                }
            }

            // If MenuItemText is still empty, use a default
            if (string.IsNullOrEmpty(entry.MenuItemText))
            {
                entry.MenuItemText = $"Новый {fileType.TrimStart('.')} файл";
            }

            return entry;
        }
        private string ProcessResourceString(string resourceString)
        {
            if (string.IsNullOrEmpty(resourceString))
                return resourceString;

            if (resourceString.StartsWith("@"))
            {
                string[] parts = resourceString.Substring(1).Split(new char[] { ',' }, 2);
                string filePath = Environment.ExpandEnvironmentVariables(parts[0]);
                string resourceIdStr = parts[1];
                if (resourceIdStr.StartsWith("-"))
                {
                    resourceIdStr = resourceIdStr.Substring(1);
                }
                if (int.TryParse(resourceIdStr, out int resourceId))
                {
                    return ResourceExtractor.ExtractString(filePath, resourceId) ?? resourceString;
                }
            }
            else if (File.Exists(resourceString))
            {
                return Path.GetFileNameWithoutExtension(resourceString);
            }

            return resourceString;
        }


        public void ReadContextMenuRegistryKeys()
        {
            foreach (string registryPath in registryPaths)
            {
                HashSet<string> processedGUIDs = new HashSet<string>();
                //Console.WriteLine($"\nReading registry path: {registryPath}");

                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(registryPath))
                {
                    if (key == null)
                    {
                        Console.WriteLine($"Failed to open registry path: {registryPath}");
                        continue;
                    }

                    foreach (string subkey in key.GetSubKeyNames())
                    {
                        if (systemEntries.Contains(subkey))
                            continue;

                        if (!processedGUIDs.Contains(subkey))
                        {
                            processedGUIDs.Add(subkey);
                            var entry = ProcessSubKey(key, subkey);
                            if (entry != null)
                            {
                                if (registryPath == @"Directory\shell")
                                {
                                    DirectoryShellEntries.Add(entry);
                                }
                                else if (registryPath == @"Directory\Background\shell")
                                {
                                    DirectoryBackgroundShellEntries.Add(entry);
                                }
                            }
                        }
                    }
                }
            }
        }

        public RegistryEntry ProcessSubKey(RegistryKey key, string subkey)
        {
            using (RegistryKey subKey = key.OpenSubKey(subkey))
            {
                if (subKey == null)
                    return null;

                var entry = new RegistryEntry { Name = subkey };

                foreach (string valueName in subKey.GetValueNames())
                {
                    object value = subKey.GetValue(valueName);
                    if (value != null)
                    {
                        if (valueName.ToLower() == "icon")
                        {
                            entry.IconPath = value.ToString();
                            // Extract icon
                            var icon = ExtractIcon(entry.IconPath);
                            if (icon != null)
                            {
                                entry.Icon = icon;
                            }
                            entry.IconPath = entry.IconPath.Split(',')[0];
                        }
                        else
                        {
                            entry.Values.Add(valueName, value.ToString());
                        }
                    }
                }

                entry.MenuItemText = GetMenuItemText(subKey);

                if (string.IsNullOrEmpty(entry.IconPath))
                {
                    entry.IconPath = GetIconPath(subKey, subkey);
                    // Extract icon if IconPath was found in DefaultIcon
                    if (!string.IsNullOrEmpty(entry.IconPath))
                    {
                        var icon = ExtractIcon(entry.IconPath);
                        if (icon != null)
                        {
                            entry.Icon = icon;
                            entry.IconPath = entry.IconPath.Split(',')[0];
                        }
                    }
                }

                entry.Command = ReadCommand(subKey, subkey);

                entry.CLSIDInfo = ReadCLSIDInfo(subkey);

                return entry;
            }
        }

        private Icon ExtractIcon(string iconPath)
        {
            if (string.IsNullOrEmpty(iconPath))
                return null;

            var parts = iconPath.Split(new[] { ',' }, 2);
            if (parts.Length != 2)
                return null;

            string filePath = parts[0];
            if (!int.TryParse(parts[1].Replace("-", ""), out int index))
                return null;

            // Remove the minus sign if present
            if (index < 0)
                index = -index;

            return IconExtractor.ExtractIconFromFile(filePath, index);
        }
        private string GetMenuItemText(RegistryKey subKey)
        {
            object value = subKey.GetValue("");
            if (value == null)
                return null;

            string valueStr = value.ToString();
            if (valueStr.StartsWith("@"))
            {
                string[] parts = valueStr.Substring(1).Split(new char[] { ',' }, 2);
                if (parts.Length == 2)
                {
                    string dllPath = parts[0];
                    string resourceIdStr = parts[1];

                    if (resourceIdStr.StartsWith("-"))
                    {
                        resourceIdStr = resourceIdStr.Substring(1);
                    }

                    if (int.TryParse(resourceIdStr, out int resourceId))
                    {
                        string resourceStr = LoadResourceString(dllPath, resourceId);
                        if (!string.IsNullOrEmpty(resourceStr))
                        {
                            return resourceStr;
                        }
                        else
                        {
                            Console.WriteLine($"Failed to load string resource ID {resourceId} from {dllPath}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Failed to parse resource ID: {resourceIdStr}");
                    }
                }
                else
                {
                    Console.WriteLine($"Invalid format for valueStr: {valueStr}");
                }
            }
            else if (valueStr.StartsWith("{"))
            {
                using (RegistryKey clsidKey = Registry.ClassesRoot.OpenSubKey(@"CLSID\" + valueStr))
                {
                    if (clsidKey != null)
                    {
                        using (RegistryKey inprocServer32Key = clsidKey.OpenSubKey("InprocServer32"))
                        {
                            if (inprocServer32Key != null)
                            {
                                string dllPath = inprocServer32Key.GetValue("") as string;
                                if (!string.IsNullOrEmpty(dllPath))
                                {
                                    string resourceIdStr = clsidKey.GetValue("ResourceID") as string;
                                    if (!string.IsNullOrEmpty(resourceIdStr) && int.TryParse(resourceIdStr, out int resourceId))
                                    {
                                        string resourceStr = LoadResourceString(dllPath, resourceId);
                                        if (resourceStr != null)
                                        {
                                            return resourceStr;
                                        }
                                    }
                                    else
                                    {
                                        IntPtr hModule = LoadLibraryEx(dllPath, IntPtr.Zero, 0);
                                        if (hModule == IntPtr.Zero)
                                        {
                                            return "Failed to load DLL.";
                                        }

                                        try
                                        {
                                            if (dllPath.Contains("WinRAR"))
                                            {
                                                for (uint block = 0; block < 256; block++)
                                                {
                                                    for (uint i = 0; i < 256; i++)
                                                    {
                                                        uint id = block * 256 + i;
                                                        string resourceString = LoadResourceString(dllPath, (int)id);
                                                        if (!string.IsNullOrEmpty(resourceString))
                                                        {
                                                            Console.WriteLine($"Resource ID {id}: {resourceString}");
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        finally
                                        {
                                            FreeLibrary(hModule);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return valueStr;
        }

        private string GetIconPath(RegistryKey subKey, string subkey)
        {
            using (RegistryKey iconKey = subKey.OpenSubKey("DefaultIcon"))
            {
                object icon = iconKey?.GetValue("");
                return icon?.ToString();
            }
        }

        private string ReadCommand(RegistryKey subKey, string subkey)
        {
            using (RegistryKey commandKey = subKey.OpenSubKey("command"))
            {
                object command = commandKey?.GetValue("");
                if (command != null)
                {
                    return command.ToString();
                }
            }

            using (RegistryKey shellKey = subKey.OpenSubKey("shell"))
            {
                if (shellKey != null)
                {
                    foreach (string commandSubkey in shellKey.GetSubKeyNames())
                    {
                        using (RegistryKey commandKey = shellKey.OpenSubKey(commandSubkey + @"\command"))
                        {
                            object command = commandKey?.GetValue("");
                            if (command != null)
                            {
                                return command.ToString();
                            }
                        }
                    }
                }
            }

            return null;
        }

        private string ReadCLSIDInfo(string subkey)
        {
            using (RegistryKey clsidKey = Registry.ClassesRoot.OpenSubKey(@"CLSID\" + subkey + @"\InProcServer32"))
            {
                object clsidInfo = clsidKey?.GetValue("");
                return clsidInfo?.ToString();
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int LoadString(IntPtr hInstance, uint uID, StringBuilder lpBuffer, int nBufferMax);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FreeLibrary(IntPtr hModule);

        public static string LoadResourceString(string dllPath, int resourceId)
        {
            IntPtr hModule = LoadLibraryEx(dllPath, IntPtr.Zero, 0);
            if (hModule == IntPtr.Zero)
                return null;

            try
            {
                StringBuilder buffer = new StringBuilder(1024);
                if (LoadString(hModule, (uint)resourceId, buffer, buffer.Capacity) > 0)
                {
                    return buffer.ToString();
                }
            }
            finally
            {
                FreeLibrary(hModule);
            }

            return null;
        }
    }
    public class RegistryEntry
    {
        public string Name { get; set; }
        public string MenuItemText { get; set; }
        public string IconPath { get; set; }
        public Icon Icon { get; set; }
        public string Command { get; set; }
        public string CLSIDInfo { get; set; }
        public Dictionary<string, string> Values { get; set; } = new Dictionary<string, string>();
    }

    public class IconExtractor
    {
        [DllImport("Shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

        public static Icon ExtractIconFromFile(string filePath, int index)
        {
            IntPtr hIcon = ExtractIcon(IntPtr.Zero, filePath, index);
            if (hIcon != IntPtr.Zero)
            {
                return Icon.FromHandle(hIcon);
            }
            return null;
        }
    }
    public class ResourceExtractor
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int LoadString(IntPtr hInstance, uint uID, StringBuilder lpBuffer, int nBufferMax);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        public static string ExtractString(string filePath, int resourceId)
        {
            if (string.IsNullOrEmpty(filePath) || resourceId < 0)
                return null;

            IntPtr hModule = LoadLibrary(filePath);
            if (hModule == IntPtr.Zero)
                return null;

            try
            {
                StringBuilder sb = new StringBuilder(1024);
                int length = LoadString(hModule, (uint)Math.Abs(resourceId), sb, sb.Capacity);
                return length > 0 ? sb.ToString() : null;
            }
            finally
            {
                FreeLibrary(hModule);
            }
        }

        public static Icon ExtractIcon(string iconPath)
        {
            if (string.IsNullOrEmpty(iconPath))
                return null;

            var parts = iconPath.Split(new[] { ',' }, 2);
            if (parts.Length != 2)
                return null;

            string filePath = parts[0];
            if (!int.TryParse(parts[1].TrimStart('-'), out int index))
                return null;

            return IconExtractor.ExtractIconFromFile(filePath, Math.Abs(index));
        }
    }
}
