namespace AddressablesTools.Catalog;
public class SerializedTypeAsmContainer
{
    public string StandardLibAsm { get; set; }
    public string Hash128Asm { get; set; }
    public string AbroAsm { get; set; }

    public static SerializedTypeAsmContainer ForNet40()
    {
        return new SerializedTypeAsmContainer()
        {
            StandardLibAsm = "mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
            Hash128Asm = "UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
            AbroAsm = "Unity.ResourceManager, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"
        };
    }
}
