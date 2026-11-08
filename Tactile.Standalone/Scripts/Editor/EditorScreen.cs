using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Tactile.Adapters.Godot;
using Tactile.Core.Elements;
using Tactile.Core.Layout;
using Tactile.Core.Layout.Elements;
using Tactile.Core.Layout.Serialization;
using Tactile.Core.Input;
using Tactile.Core.Input.Keyboard;
using Tactile.Standalone.App;
using Tactile.Standalone.Layouts;
using CoreRect = Tactile.Core.Primitives.Rect;
using CoreVector2 = Tactile.Core.Primitives.Vector2;

namespace Tactile.Standalone.Editor;

/// <summary>
/// 에디터 화면 (<c>Scenes/Editor/Editor.tscn</c>): 위 도구 막대, 왼쪽 요소 목록, 가운데 캔버스(실제 렌더링 미리보기), 오른쪽 속성 편집기, 아래 상태 표시줄.
/// 노드는 씬에 있고, 이 스크립트는 고유 이름(%)으로 찾아 동작만 연결합니다.
/// 미리보기는 실제 키 입력을 받아 그대로 움직입니다.
/// </summary>
public sealed partial class EditorScreen : Control
{
    private readonly ElementRegistry _registry = ElementRegistry.CreateDefault();
    private IReadOnlyList<Type> _creatable = Array.Empty<Type>();
    private LayoutDocument _document = null!;
    private SharedKeyboard _keyboard = null!;

    // Editor.tscn의 노드
    private CanvasView _canvas = null!;
    private Inspector _inspector = null!;
    private ItemList _list = null!;
    private Button _undo = null!, _redo = null!, _duplicate = null!, _delete = null!, _up = null!, _down = null!;
    private Label _status = null!, _permission = null!;
    private Button _openSettings = null!;
    private MenuButton _addMenu = null!;
    private FileDialog _openDialog = null!, _saveDialog = null!;
    private ConfirmationDialog _confirmDiscard = null!;
    private AcceptDialog _alert = null!;
    private Action? _afterConfirm;

    private TactileGodotViewer? _preview;
    private bool _active;
    private bool _previewDirty = true;
    private bool _inspectorDirty = true;
    private bool _listDirty = true;

    public override void _Ready()
    {
        _canvas = GetNode<CanvasView>("%Canvas");
        _inspector = GetNode<Inspector>("%Inspector");
        _list = GetNode<ItemList>("%Elements");
        _undo = GetNode<Button>("%Undo");
        _redo = GetNode<Button>("%Redo");
        _duplicate = GetNode<Button>("%Duplicate");
        _delete = GetNode<Button>("%Delete");
        _up = GetNode<Button>("%MoveUp");
        _down = GetNode<Button>("%MoveDown");
        _status = GetNode<Label>("%Status");
        _permission = GetNode<Label>("%Permission");
        _openSettings = GetNode<Button>("%OpenSettings");
        _addMenu = GetNode<MenuButton>("%AddMenu");
        _openDialog = GetNode<FileDialog>("%OpenDialog");
        _saveDialog = GetNode<FileDialog>("%SaveDialog");
        _confirmDiscard = GetNode<ConfirmationDialog>("%ConfirmDiscard");
        _alert = GetNode<AcceptDialog>("%Alert");

        GetNode<MenuButton>("%FileMenu").GetPopup().IdPressed += OnFileMenu;
        _addMenu.GetPopup().IdPressed += id => AddElement(_creatable[(int)id]);
        _undo.Pressed += () => _document.Undo();
        _redo.Pressed += () => _document.Redo();
        _duplicate.Pressed += DuplicateSelected;
        _delete.Pressed += DeleteSelected;
        _up.Pressed += () => MoveSelected(-1);
        _down.Pressed += () => MoveSelected(1);
        _openSettings.Pressed += () => InputPermissions.OpenKeyboardSettings();
        GetNode<Button>("%Show").Pressed += () => ShowRequested?.Invoke();
        // 목록은 여러 개 선택 가능 (Shift/⌘). 0번은 "레이아웃 설정"
        _list.ItemSelected += index => OnListSelected((int)index);
        _list.MultiSelected += (index, _) => OnListSelected((int)index);
        _inspector.FitCanvasRequested += FitCanvasToElements;
        GetNode<CheckButton>("%Snap").Toggled += on => _canvas.SnapEnabled = on;

        _openDialog.FileSelected += path => ConfirmDiscard(() => Open(path));
        _saveDialog.FileSelected += path => Save(path);

        // ConfirmationDialog는 버튼이 둘(저장/취소)뿐이라 세 번째 버튼만 여기서 붙임
        _confirmDiscard.AddButton("저장 안 함", right: false, action: "discard");
        _confirmDiscard.Confirmed += () =>
        {
            if (Save(_document.Path))
                RunAfterConfirm();
        };
        _confirmDiscard.CustomAction += action =>
        {
            if (action != "discard")
                return;
            _confirmDiscard.Hide();
            RunAfterConfirm();
        };
        _confirmDiscard.Canceled += () => _afterConfirm = null;
    }

    /// <summary>문서와 키보드를 연결합니다 (앱이 _Ready에서 호출).</summary>
    public void Initialize(LayoutDocument document, SharedKeyboard keyboard)
    {
        _document = document;
        _keyboard = keyboard;
        _canvas.Initialize(document);
        _inspector.Initialize(document, keyboard);

        // 추가 메뉴 항목은 등록표에 따라 달라짐 (애드온 요소)
        _creatable = ElementFactory.CreatableTypes(_registry);
        PopupMenu addMenu = _addMenu.GetPopup();
        addMenu.Clear();
        for (int i = 0; i < _creatable.Count; i++)
            addMenu.AddItem(Names.ElementKind(_creatable[i]), i);

        _document.Changed += OnDocumentChanged;
        _keyboard.StatusChanged += _ => UpdatePermission();
        UpdatePermission();
        UpdateStatus();
    }

    /// <summary>"표시" 버튼.</summary>
    public event Action? ShowRequested;

    /// <summary>창 제목이나 저장 상태가 바뀌었을 때.</summary>
    public event Action? TitleChanged;

    /// <summary>에디터 화면을 보여 주고 미리보기를 시작합니다.</summary>
    public void Activate()
    {
        _active = true;
        Visible = true;
        _previewDirty = true;
    }

    /// <summary>에디터 화면을 숨기고 미리보기를 멈춥니다 (표시 창으로 바꿀 때).</summary>
    public void Deactivate()
    {
        _active = false;
        Visible = false;
        ClosePreview();
    }

    public override void _Process(double delta)
    {
        if (!_active)
            return;

        if (_listDirty)
        {
            _listDirty = false;
            RebuildList();
        }

        if (_inspectorDirty)
        {
            _inspectorDirty = false;
            _inspector.Rebuild();
        }

        if (_previewDirty)
        {
            _previewDirty = false;
            RebuildPreview();
        }

        _preview?.Update();
    }

    public override void _ExitTree() => ClosePreview();

    // ---- 문서 변경 반영 ----

    private void OnDocumentChanged(DocumentChange change)
    {
        if ((change & (DocumentChange.Structure | DocumentChange.Selection)) != 0)
            _inspectorDirty = true;
        else if ((change & DocumentChange.Values) != 0)
            _inspector.RefreshValues();

        if ((change & (DocumentChange.Structure | DocumentChange.Values | DocumentChange.Selection)) != 0)
            _listDirty = true;

        if ((change & (DocumentChange.Structure | DocumentChange.Values)) != 0)
        {
            _previewDirty = true;
            _canvas.Fit();
        }

        if ((change & DocumentChange.Selection) != 0)
            _canvas.Redraw();

        UpdateStatus();
    }

    private void RebuildList()
    {
        _list.Clear();
        _list.AddItem("⚙  레이아웃 설정");
        foreach (LayoutElement element in _document.Layout.Elements)
            _list.AddItem("    " + Names.Describe(element));
        SyncListSelection();
        UpdateStatus();
    }

    private void SyncListSelection()
    {
        _list.DeselectAll();
        if (_document.Selection.Count == 0)
            _list.Select(0);
        foreach (int index in _document.Selection)
            _list.Select(index + 1, single: false);
    }

    private void OnListSelected(int clicked)
    {
        if (clicked == 0)
        {
            _document.Select(-1);
            SyncListSelection();
            return;
        }

        var selection = new List<int>();
        foreach (int item in _list.GetSelectedItems())
        {
            if (item > 0)
                selection.Add(item - 1);
        }

        _document.SetSelection(selection);
        if (_list.IsSelected(0))
            _list.Deselect(0);
    }

    private void UpdateStatus()
    {
        KeyViewerLayout layout = _document.Layout;
        _status.Text = $"  {_document.Path}{(_document.IsDirty ? " (저장 안 됨)" : "")}   ·   캔버스 {layout.Canvas.X:0} × {layout.Canvas.Y:0}   ·   {_canvas.Zoom * 100:0}%";
        _undo.Disabled = !_document.CanUndo;
        _redo.Disabled = !_document.CanRedo;
        bool any = _document.Selection.Count > 0;
        bool single = _document.Selected is not null; // 순서 바꾸기는 하나만 선택했을 때
        _duplicate.Disabled = _delete.Disabled = !any;
        _up.Disabled = !single || _document.SelectedIndex <= 0;
        _down.Disabled = !single || _document.SelectedIndex >= layout.Elements.Count - 1;
        TitleChanged?.Invoke();
    }

    private void UpdatePermission()
    {
        _permission.Text = _keyboard.Status switch
        {
            KeyboardStatus.WaitingForPermission => "입력 모니터링 권한이 없어 미리보기가 키 입력에 반응하지 않습니다",
            KeyboardStatus.NotSupported when PlatformKeyboard.IsSupported => "키 입력을 시작하지 못했습니다 (로그를 확인하세요)",
            KeyboardStatus.NotSupported => $"이 운영체제({OS.GetName()})는 키 입력을 아직 지원하지 않습니다",
            _ => "",
        };
        _openSettings.Visible = _keyboard.Status == KeyboardStatus.WaitingForPermission;
    }

    // ---- 미리보기 ----

    private void RebuildPreview()
    {
        ClosePreview();
        try
        {
            _preview = new TactileGodotViewer(_canvas.Stage, _document.Layout, _keyboard, new TactileGodotOptions { Elements = _registry });
            _preview.Root.Scale = Vector2.One; // 확대는 캔버스가 Stage에 적용
            _canvas.ShowMessage(_preview.Viewer.UnsupportedElements.Count > 0
                ? $"그릴 수 없는 요소 {_preview.Viewer.UnsupportedElements.Count}개 (등록되지 않은 종류)는 테두리만 보입니다."
                : null);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            _canvas.ShowMessage($"미리보기를 만들 수 없습니다: {e.Message}");
        }
    }

    private void ClosePreview()
    {
        _preview?.Dispose();
        _preview = null;
    }

    // ---- 편집 동작 ----

    private void AddElement(Type type)
    {
        LayoutElement element = ElementFactory.Create(type, _document.Layout);
        _document.Layout.Elements.Add(element);
        _document.Commit(DocumentChange.Structure);
        _document.Select(_document.Layout.Elements.Count - 1);
    }

    /// <summary>선택한 요소들을 복제해 마지막으로 선택한 요소 바로 뒤에 넣고, 복제본을 선택합니다.</summary>
    private void DuplicateSelected()
    {
        IReadOnlyList<int> selection = _document.Selection;
        if (selection.Count == 0)
            return;

        List<LayoutElement> elements = _document.Layout.Elements;
        int insertAt = selection[selection.Count - 1] + 1;
        var copies = new List<int>();
        foreach (int index in selection.ToArray())
        {
            LayoutElement copy = ElementFactory.Duplicate(elements[index], _document.Layout);
            elements.Insert(insertAt + copies.Count, copy);
            copies.Add(insertAt + copies.Count);
        }

        _document.Select(-1);
        _document.Commit(DocumentChange.Structure);
        _document.SetSelection(copies);
    }

    private void DeleteSelected()
    {
        int[] selection = _document.Selection.ToArray();
        if (selection.Length == 0)
            return;

        for (int i = selection.Length - 1; i >= 0; i--)
            _document.Layout.Elements.RemoveAt(selection[i]);
        _document.Select(-1);
        _document.Commit(DocumentChange.Structure);
        if (selection.Length == 1)
            _document.Select(Math.Min(selection[0], _document.Layout.Elements.Count - 1));
    }

    private void MoveSelected(int direction)
    {
        int index = _document.SelectedIndex;
        int target = index + direction;
        List<LayoutElement> elements = _document.Layout.Elements;
        if (index < 0 || target < 0 || target >= elements.Count)
            return;

        (elements[index], elements[target]) = (elements[target], elements[index]);
        _document.Select(-1);
        _document.Commit(DocumentChange.Structure);
        _document.Select(target);
    }

    private void Nudge(float dx, float dy)
    {
        if (_document.Selection.Count == 0)
            return;
        foreach (LayoutElement element in _document.SelectedElements)
        {
            CoreRect b = element.Bounds;
            element.Bounds = new CoreRect(b.X + dx, b.Y + dy, b.Width, b.Height);
        }

        _document.Commit(DocumentChange.Values, "nudge");
    }

    /// <summary>캔버스를 요소들이 차지하는 영역 + 지금의 왼쪽·위 여백만큼으로 맞춥니다.</summary>
    private void FitCanvasToElements()
    {
        List<LayoutElement> elements = _document.Layout.Elements;
        if (elements.Count == 0)
            return;

        float left = elements.Min(e => e.Bounds.X), top = elements.Min(e => e.Bounds.Y);
        float right = elements.Max(e => e.Bounds.Right), bottom = elements.Max(e => e.Bounds.Bottom);
        float marginX = Math.Max(0, left), marginY = Math.Max(0, top);
        _document.Layout.Canvas = new CoreVector2((float)Math.Ceiling(right + marginX), (float)Math.Ceiling(bottom + marginY));
        _document.Commit(DocumentChange.Values);
    }

    // ---- 파일 ----

    private void OnFileMenu(long id)
    {
        switch (id)
        {
            case 0:
                ConfirmDiscard(() => _document.Replace(new KeyViewerLayout { Canvas = new CoreVector2(600, 400) }, UntitledPath(), saved: false));
                break;
            case 1:
                ConfirmDiscard(() => _document.Replace(DefaultLayouts.FourKeys(), UntitledPath(), saved: false));
                break;
            case 2:
                _openDialog.CurrentDir = Path.GetDirectoryName(_document.Path) ?? "";
                _openDialog.PopupCentered(new Vector2I(800, 500));
                break;
            case 3:
                Save(_document.Path);
                break;
            case 4:
                _saveDialog.CurrentPath = _document.Path;
                _saveDialog.PopupCentered(new Vector2I(800, 500));
                break;
        }
    }

    private string UntitledPath() => Path.Combine(Path.GetDirectoryName(_document.Path) ?? "", "새 레이아웃.json");

    /// <summary>파일을 엽니다. 실패하면 알리고 지금 문서를 그대로 둡니다.</summary>
    public bool Open(string path)
    {
        try
        {
            _document.Replace(LayoutSerializer.LoadFile(path, _registry), path, saved: true);
            LayoutOpened?.Invoke(path);
            return true;
        }
        catch (Exception e) when (e is LayoutFormatException or IOException or UnauthorizedAccessException)
        {
            Alert("레이아웃을 열 수 없습니다", $"{path}\n\n{e.Message}");
            return false;
        }
    }

    /// <summary>파일을 열거나 저장했을 때 (마지막 파일 기억용).</summary>
    public event Action<string>? LayoutOpened;

    public bool Save(string path)
    {
        try
        {
            _document.Save(path);
            LayoutOpened?.Invoke(path);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Alert("저장하지 못했습니다", $"{path}\n\n{e.Message}");
            return false;
        }
    }

    /// <summary>저장하지 않은 변경이 있으면 물어본 뒤 <paramref name="proceed"/>를 실행합니다.</summary>
    public void ConfirmDiscard(Action proceed)
    {
        if (!_document.IsDirty)
        {
            proceed();
            return;
        }

        _afterConfirm = proceed;
        _confirmDiscard.DialogText = $"{Path.GetFileName(_document.Path)}의 변경 내용을 저장할까요?";
        _confirmDiscard.PopupCentered();
    }

    private void RunAfterConfirm()
    {
        Action? proceed = _afterConfirm;
        _afterConfirm = null;
        proceed?.Invoke();
    }

    /// <summary>알림 창.</summary>
    public void Alert(string title, string text)
    {
        _alert.Title = title;
        _alert.DialogText = text;
        _alert.PopupCentered(new Vector2I(520, 0));
    }

    // ---- 단축키 (입력 칸이 쓰지 않은 키만) ----

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!_active || @event is not InputEventKey { Pressed: true } key)
            return;

        bool command = key.IsCommandOrControlPressed();
        float step = key.ShiftPressed ? 10f : 1f;
        bool handled = true;
        switch (key.Keycode)
        {
            case Key.S when command:
                Save(_document.Path);
                break;
            case Key.Z when command && key.ShiftPressed:
                _document.Redo();
                break;
            case Key.Z when command:
                _document.Undo();
                break;
            case Key.A when command:
                _document.SetSelection(Enumerable.Range(0, _document.Layout.Elements.Count));
                break;
            case Key.D when command:
                DuplicateSelected();
                break;
            case Key.Delete or Key.Backspace when !command:
                DeleteSelected();
                break;
            case Key.Left: Nudge(-step, 0); break;
            case Key.Right: Nudge(step, 0); break;
            case Key.Up: Nudge(0, -step); break;
            case Key.Down: Nudge(0, step); break;
            case Key.Escape:
                _document.Select(-1);
                break;
            default:
                handled = false;
                break;
        }

        if (handled)
            GetViewport().SetInputAsHandled();
    }
}
