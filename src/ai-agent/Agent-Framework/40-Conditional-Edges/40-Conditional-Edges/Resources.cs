using System;
using System.Collections.Generic;
using System.Text;


public static class Resources
{
    private const string ResourceFolder = "Resources";

    public static string Read(string fileName) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, ResourceFolder, fileName));
}

