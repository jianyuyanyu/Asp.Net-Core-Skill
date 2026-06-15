using System;
using System.Collections.Generic;
using System.Text;

namespace _41_Switch_Case
{
    /// <summary>
    /// 加载资源文件的工具类
    /// </summary>
    internal static class Resources
    {
        private const string ResourceFolder = "Resources";

        public static string Read(string fileName) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, ResourceFolder, fileName));
    }
}
