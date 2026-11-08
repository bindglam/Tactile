using Tactile.Core.Layout;
using Tactile.Core.Layout.Serialization;
using Tactile.Standalone.Layouts;

namespace Tactile.ADOFAI;

/// <summary>
/// <c>UserData/Tactile/layouts/</c> 폴더의 레이아웃 파일(*.json) 목록.
/// 폴더에 파일이 하나도 없으면 예전 위치의 <c>UserData/Tactile/layout.json</c>을 옮겨 오거나, 기본 레이아웃을 저장합니다.
/// </summary>
internal sealed class LayoutLibrary
{
    private const string DefaultFileName = "default.json";

    private readonly List<string> _files = new();

    public LayoutLibrary(string tactileDirectory)
    {
        LegacyPath = Path.Combine(tactileDirectory, "layout.json");
        Directory = Path.Combine(tactileDirectory, "layouts");
    }

    /// <summary>레이아웃 폴더의 전체 경로.</summary>
    public string Directory { get; }

    /// <summary>예전 버전이 쓰던 레이아웃 파일 경로.</summary>
    private string LegacyPath { get; }

    /// <summary>레이아웃 파일 이름들 (확장자 포함, 이름순).</summary>
    public IReadOnlyList<string> Files => _files;

    /// <summary>폴더를 다시 읽습니다. 파일이 없으면 기본 파일을 만듭니다.</summary>
    /// <exception cref="IOException">폴더를 만들거나 읽지 못한 경우</exception>
    /// <exception cref="UnauthorizedAccessException">폴더 권한이 없는 경우</exception>
    public void Refresh()
    {
        System.IO.Directory.CreateDirectory(Directory);
        if (!ListFiles())
        {
            if (File.Exists(LegacyPath))
                File.Move(LegacyPath, Path.Combine(Directory, "layout.json"));
            else
                LayoutSerializer.SaveFile(DefaultLayouts.FourKeys(), Path.Combine(Directory, DefaultFileName));
            ListFiles();
        }
    }

    /// <summary>목록에 있는 파일을 불러옵니다.</summary>
    /// <exception cref="LayoutFormatException">파일 형식이 잘못된 경우</exception>
    /// <exception cref="IOException">파일을 읽지 못한 경우</exception>
    public KeyViewerLayout Load(string fileName) => LayoutSerializer.LoadFile(GetPath(fileName));

    /// <summary>파일 이름의 전체 경로. 폴더 밖을 가리키지 않도록 이름 부분만 사용합니다.</summary>
    public string GetPath(string fileName) => Path.Combine(Directory, Path.GetFileName(fileName));

    private bool ListFiles()
    {
        _files.Clear();
        foreach (string path in System.IO.Directory.GetFiles(Directory, "*.json"))
            _files.Add(Path.GetFileName(path));
        _files.Sort(StringComparer.OrdinalIgnoreCase);
        return _files.Count > 0;
    }
}
