using Engine.Editor;

namespace Engine.Editor;

class Program
{
    static void Main(string[] args)
    {
        using var editor = new EditorApplication();
        editor.Run();
    }
}
