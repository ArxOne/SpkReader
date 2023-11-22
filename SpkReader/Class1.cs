namespace SpkReader;
public class Class1
{
    public string Version { get; set; }
    public string Name { get; set; }

    public string Description { get; set; }

    public Class1(string version, string name)
    {
        Version = version;
        Name = name;
    }
}