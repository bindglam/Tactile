using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using Tactile.Core.Input.Keyboard;
using Tactile.Core.Layout;
using Tactile.Core.Layout.Elements;
using Tactile.Core.Layout.Serialization;
using Tactile.Standalone.App;
using CoreColor = Tactile.Core.Primitives.Color;
using CoreRect = Tactile.Core.Primitives.Rect;
using CoreVector2 = Tactile.Core.Primitives.Vector2;

namespace Tactile.Standalone.Editor;

/// <summary>
/// 선택한 요소(또는 레이아웃 설정)의 속성 편집기. 화면 틀은 <c>Editor.tscn</c>의 Inspector 노드에 있습니다.
/// <para>
/// 여러 개를 선택하면: 모두 같은 종류면 모든 속성을 보여 주되 요소마다 다른 값은 "여러 값"으로 비워 두고, 바꾸면 모두에 적용합니다.
/// 종류가 섞여 있으면 묶음 위치만 바꿀 수 있습니다.
/// </para>
/// <para>
/// 편집 칸 목록은 요소 종류마다 다르므로(애드온 요소 포함), 레이아웃 파일과 같은 규칙(공개 get과 public set이 있고
/// <see cref="LayoutIgnoreAttribute"/>가 없는 속성)으로 골라 <c>Scenes/Editor/Fields/</c>의 칸 씬을 인스턴스해 채웁니다.
/// 바꾸면 바로 문서에 반영되고 되돌리기 단계가 생깁니다.
/// </para>
/// </summary>
public sealed partial class Inspector : ScrollContainer
{
    private const string FieldDirectory = "res://Scenes/Editor/Fields/";
    private const float LabelWidth = 128f;
    private const string MixedText = "여러 값";

    // 접은 상태 기억 (요소 종류 + 속성 경로)
    private static readonly HashSet<string> Expanded = new();

    private readonly List<Action> _refreshers = new();
    private readonly NullabilityInfoContext _nullability = new();

    private LayoutDocument _document = null!;
    private SharedKeyboard _keyboard = null!;

    private Label _title = null!, _subtitle = null!, _notice = null!, _summary = null!;
    private VBoxContainer _fields = null!;
    private Control _layoutFooter = null!;

    // "키 눌러서 지정" 중인 칸
    private Action<KeyCode>? _capture;
    private Button? _captureButton;

    /// <summary>레이아웃 설정 화면의 "캔버스를 요소에 맞추기" 버튼.</summary>
    public event Action? FitCanvasRequested;

    public override void _Ready()
    {
        _title = GetNode<Label>("%Title");
        _subtitle = GetNode<Label>("%Subtitle");
        _notice = GetNode<Label>("%Notice");
        _summary = GetNode<Label>("%Summary");
        _fields = GetNode<VBoxContainer>("%Fields");
        _layoutFooter = GetNode<Control>("%LayoutFooter");
        GetNode<Button>("%FitCanvas").Pressed += () => FitCanvasRequested?.Invoke();
    }

    public void Initialize(LayoutDocument document, SharedKeyboard keyboard)
    {
        _document = document;
        _keyboard = keyboard;
        _keyboard.KeyPressed += OnKeyPressed;
    }

    public override void _ExitTree()
    {
        if (_keyboard is not null)
            _keyboard.KeyPressed -= OnKeyPressed;
    }

    /// <summary>편집 칸을 처음부터 다시 만듭니다 (선택이 바뀌었거나 구조가 바뀌었을 때).</summary>
    public void Rebuild()
    {
        CancelCapture();
        _refreshers.Clear();
        foreach (Node child in _fields.GetChildren())
        {
            _fields.RemoveChild(child);
            child.QueueFree();
        }

        List<LayoutElement> selected = _document.SelectedElements.ToList();
        _layoutFooter.Visible = selected.Count == 0;
        _notice.Visible = false;
        if (selected.Count == 0)
            ShowLayoutSettings();
        else if (selected.Count == 1)
            ShowElements(selected);
        else
            ShowMany(selected);
    }

    /// <summary>값만 다시 읽어 칸에 반영합니다 (캔버스에서 드래그했을 때 등). 입력 중인 칸은 건드리지 않습니다.</summary>
    public void RefreshValues()
    {
        foreach (Action refresh in _refreshers)
            refresh();
        if (_document.Selection.Count == 0)
            _summary.Text = Summary();
    }

    // ---- 화면 ----

    private void ShowLayoutSettings()
    {
        _title.Text = "레이아웃";
        _subtitle.Text = "캔버스 크기와 전체 배율";
        _summary.Text = Summary();
        var root = new Accessor(typeof(KeyViewerLayout), new[] { new Target(() => _document.Layout, _ => { }) }, "layout");
        foreach (PropertyInfo property in EditableProperties(typeof(KeyViewerLayout)))
        {
            if (property.Name is nameof(KeyViewerLayout.Version) or nameof(KeyViewerLayout.Elements))
                continue;
            _fields.AddChild(BuildProperty(root.Child(property), 0));
        }
    }

    private string Summary() => $"요소 {_document.Layout.Elements.Count}개 · 캔버스에서 요소를 눌러 선택하세요.";

    /// <summary>요소 하나, 또는 같은 종류 여러 개의 속성.</summary>
    private void ShowElements(List<LayoutElement> elements)
    {
        Type type = elements[0].GetType();
        bool many = elements.Count > 1;
        _title.Text = many ? $"{Names.ElementKind(type)} {elements.Count}개" : Names.ElementKind(type);
        _subtitle.Text = many ? "같은 값만 보여 줍니다 · 바꾸면 모두에 적용" : type.Name;
        if (elements[0] is UnknownElement unknown && !many)
        {
            _notice.Visible = true;
            _notice.Text = $"등록되지 않은 요소 종류(\"{unknown.TypeId}\")입니다. 해당 애드온이 없어서 그릴 수 없고, ID와 위치만 바꿀 수 있습니다. 나머지 내용은 저장할 때 그대로 보존됩니다.";
        }

        if (many)
            _fields.AddChild(BuildGroupPosition());

        var targets = new List<Target>();
        foreach (int index in _document.Selection)
        {
            int i = index;
            targets.Add(new Target(() => _document.Layout.Elements[i], _ => { }));
        }

        var root = new Accessor(type, targets.ToArray(), many ? "multi" : $"e{_document.Selection[0]}");
        foreach (PropertyInfo property in EditableProperties(type))
        {
            if (many && property.Name == nameof(LayoutElement.Id))
                continue; // ID는 겹치면 안 되므로 여러 개를 한 번에 바꾸지 않음
            _fields.AddChild(BuildProperty(root.Child(property), 0));
        }
    }

    /// <summary>여러 개 선택: 같은 종류면 공통 속성, 섞여 있으면 묶음 위치만.</summary>
    private void ShowMany(List<LayoutElement> elements)
    {
        Type type = elements[0].GetType();
        if (type != typeof(UnknownElement) && elements.All(e => e.GetType() == type))
        {
            ShowElements(elements);
            return;
        }

        _title.Text = $"요소 {elements.Count}개";
        _subtitle.Text = string.Join(", ", elements.GroupBy(e => e.GetType()).Select(g => $"{Names.ElementKind(g.Key)} {g.Count()}"));
        _notice.Visible = true;
        _notice.Text = "종류가 다른 요소가 섞여 있어서 위치만 바꿀 수 있습니다. 같은 종류만 고르면 다른 속성도 함께 바꿀 수 있습니다.";
        _fields.AddChild(BuildGroupPosition());
    }

    /// <summary>묶음 위치: 선택한 요소 전체를 감싸는 사각형의 왼쪽 위. 바꾸면 모두 같은 거리만큼 옮깁니다.</summary>
    private Control BuildGroupPosition()
    {
        var row = Field<HBoxContainer>("Row");
        var label = row.GetNode<Label>("Name");
        label.Text = "묶음 위치";
        label.TooltipText = "선택한 요소 전체를 감싸는 사각형의 왼쪽 위";
        label.CustomMinimumSize = new Vector2(LabelWidth, 0);
        var position = Field<HBoxContainer>("Vector2");
        var x = position.GetNode<SpinBox>("X");
        var y = position.GetNode<SpinBox>("Y");
        x.Step = y.Step = 1;
        row.AddChild(position);

        CoreVector2 Group()
        {
            float left = float.MaxValue, top = float.MaxValue;
            foreach (LayoutElement e in _document.SelectedElements)
            {
                left = Math.Min(left, e.Bounds.X);
                top = Math.Min(top, e.Bounds.Y);
            }

            return new CoreVector2(left, top);
        }

        void Read()
        {
            CoreVector2 g = Group();
            if (!x.GetLineEdit().HasFocus()) x.SetValueNoSignal(g.X);
            if (!y.GetLineEdit().HasFocus()) y.SetValueNoSignal(g.Y);
        }

        void MoveTo(float? newX, float? newY)
        {
            CoreVector2 g = Group();
            float dx = newX is { } nx ? nx - g.X : 0, dy = newY is { } ny ? ny - g.Y : 0;
            if (dx == 0 && dy == 0)
                return;
            foreach (LayoutElement e in _document.SelectedElements)
                e.Bounds = new CoreRect(e.Bounds.X + dx, e.Bounds.Y + dy, e.Bounds.Width, e.Bounds.Height);
            _document.Commit(DocumentChange.Values, "group-position");
        }

        Read();
        x.ValueChanged += v => MoveTo((float)v, null);
        y.ValueChanged += v => MoveTo(null, (float)v);
        _refreshers.Add(Read);
        return row;
    }

    private static T Field<T>(string name) where T : Node =>
        GD.Load<PackedScene>($"{FieldDirectory}{name}.tscn").Instantiate<T>();

    /// <summary>속성 하나: 객체면 접을 수 있는 묶음(Section), 목록이면 List, 아니면 "이름 | 값" 한 줄(Row).</summary>
    private Control BuildProperty(Accessor accessor, int depth)
    {
        Type type = Nullable.GetUnderlyingType(accessor.Type) ?? accessor.Type;
        if (IsSection(type) && Nullable.GetUnderlyingType(accessor.Type) is null)
            return BuildSection(accessor, depth);

        if (IsList(type))
        {
            var list = Field<VBoxContainer>("List");
            var name = list.GetNode<Label>("Name");
            name.Text = Names.Property(accessor.Name);
            name.TooltipText = accessor.Name;
            FillList(list, accessor, depth);
            return list;
        }

        var row = Field<HBoxContainer>("Row");
        var label = row.GetNode<Label>("Name");
        label.Text = Names.Property(accessor.Name);
        label.TooltipText = accessor.Name;
        label.CustomMinimumSize = new Vector2(LabelWidth - depth * 12, 0);
        row.AddChild(BuildValue(accessor));
        return row;
    }

    private Control BuildSection(Accessor accessor, int depth)
    {
        string key = $"{_document.SelectedElements.FirstOrDefault()?.GetType().Name ?? "layout"}.{accessor.PathWithoutIndex}";
        bool open = depth == 0 ? !Expanded.Contains("-" + key) : Expanded.Contains(key);

        var section = Field<VBoxContainer>("Section");
        var header = section.GetNode<Button>("Header");
        var indent = section.GetNode<Control>("Indent");
        var body = section.GetNode<VBoxContainer>("Indent/Body");
        string title = Names.Property(accessor.Name);
        header.Text = (open ? "▾ " : "▸ ") + title;
        indent.Visible = open;

        foreach (PropertyInfo property in EditableProperties(accessor.Type))
            body.AddChild(BuildProperty(accessor.Child(property), depth + 1));

        header.Pressed += () =>
        {
            indent.Visible = !indent.Visible;
            header.Text = (indent.Visible ? "▾ " : "▸ ") + title;
            if (depth == 0)
            {
                if (indent.Visible) Expanded.Remove("-" + key); else Expanded.Add("-" + key);
            }
            else
            {
                if (indent.Visible) Expanded.Add(key); else Expanded.Remove(key);
            }
        };
        return section;
    }

    // ---- 값 형식별 편집 칸 ----

    private Control BuildValue(Accessor accessor)
    {
        Type? nullable = Nullable.GetUnderlyingType(accessor.Type);
        if (nullable is not null)
            return BuildOptional(accessor, nullable);

        Type type = accessor.Type;
        if (type == typeof(string)) return BuildText(accessor);
        if (type == typeof(bool)) return BuildToggle(accessor);
        if (type == typeof(float) || type == typeof(double) || type == typeof(int) || type == typeof(long))
            return BuildNumber(accessor);
        if (type.IsEnum) return BuildChoice(accessor);
        if (type == typeof(CoreColor)) return BuildColor(accessor);
        if (type == typeof(CoreVector2)) return BuildVector2(accessor);
        if (type == typeof(CoreRect)) return BuildRect(accessor);
        if (type == typeof(InputBinding)) return BuildBinding(accessor);
        if (IsSection(type)) return BuildInline(accessor);

        var label = Field<Label>("ReadOnly");
        label.Text = accessor.Get()?.ToString() ?? "(없음)";
        return label;
    }

    /// <summary>목록 안의 작은 구조체(손가락 연결 등): 속성들을 이름 없이 한 줄에.</summary>
    private Control BuildInline(Accessor accessor)
    {
        var row = Field<HBoxContainer>("Inline");
        foreach (PropertyInfo property in EditableProperties(accessor.Type))
        {
            Control editor = BuildValue(accessor.Child(property));
            editor.TooltipText = Names.Property(property.Name);
            row.AddChild(editor);
        }

        return row;
    }

    private Control BuildText(Accessor accessor)
    {
        bool allowsNull = accessor.Property is { } p && _nullability.Create(p).WriteState == NullabilityState.Nullable;
        var edit = Field<LineEdit>("Text");
        edit.TextChanged += text => Apply(accessor, allowsNull && text.Length == 0 ? null : text);

        void Read()
        {
            if (edit.HasFocus())
                return;
            bool mixed = accessor.IsMixed;
            edit.Text = mixed ? "" : (string?)accessor.Get() ?? "";
            edit.PlaceholderText = mixed ? MixedText : "";
        }

        Read();
        _refreshers.Add(Read);
        return edit;
    }

    private Control BuildToggle(Accessor accessor)
    {
        var check = Field<CheckBox>("Toggle");
        check.Toggled += on => Apply(accessor, on);

        void Read()
        {
            bool mixed = accessor.IsMixed;
            check.SetPressedNoSignal(!mixed && (bool)accessor.Get()!);
            check.Text = mixed ? MixedText : "";
        }

        Read();
        _refreshers.Add(Read);
        return check;
    }

    private Control BuildNumber(Accessor accessor)
    {
        var spin = Field<SpinBox>("Number");
        spin.Step = StepFor(accessor.Type, accessor.Name);
        BindNumber(spin, () => accessor.IsMixed ? null : Convert.ToDouble(accessor.Get()),
            value => Apply(accessor, Convert.ChangeType(value, accessor.Type)));
        return spin;
    }

    /// <summary>
    /// 숫자 칸 연결. <paramref name="read"/>가 null이면 "여러 값"으로 비워 둡니다.
    /// 비워 둔 칸에 적은 숫자가 내부 값과 우연히 같으면 ValueChanged가 오지 않으므로, 직접 적은 경우는 확정할 때 한 번 더 적용합니다.
    /// </summary>
    private void BindNumber(SpinBox spin, Func<double?> read, Action<double> write)
    {
        LineEdit line = spin.GetLineEdit();
        bool typed = false;

        void Read()
        {
            if (line.HasFocus())
                return;
            double? value = read();
            if (value is { } v)
            {
                spin.SetValueNoSignal(v);
                line.PlaceholderText = "";
            }
            else
            {
                line.Text = "";
                line.PlaceholderText = MixedText;
            }
        }

        void Commit()
        {
            if (!typed)
                return;
            typed = false;
            write(spin.Value);
        }

        Read();
        spin.Ready += Read; // SpinBox는 트리에 들어갈 때 숫자 글자를 다시 쓰므로, 비워 둔 "여러 값"을 그 뒤에 다시 적용
        spin.ValueChanged += value =>
        {
            typed = false;
            write(value);
        };
        line.TextChanged += _ => typed = true;
        line.TextSubmitted += _ => Commit();
        line.FocusExited += () =>
        {
            Commit();
            Read();
        };
        _refreshers.Add(Read);
    }

    private static double StepFor(Type type, string name)
    {
        if (type == typeof(int) || type == typeof(long))
            return 1;
        bool fine = name.EndsWith("Strength", StringComparison.Ordinal) || name.EndsWith("Motion", StringComparison.Ordinal) || name == "Scale";
        return fine ? 0.01 : 0.1;
    }

    private Control BuildChoice(Accessor accessor)
    {
        var option = Field<OptionButton>("Choice");
        Array values = Enum.GetValues(accessor.Type);
        foreach (Enum value in values)
            option.AddItem(Names.EnumValue(value));

        void Read()
        {
            option.Select(accessor.IsMixed ? -1 : Array.IndexOf(values, accessor.Get()));
            option.TooltipText = accessor.IsMixed ? MixedText : "";
        }

        Read();
        option.ItemSelected += index => Apply(accessor, values.GetValue((int)index));
        _refreshers.Add(Read);
        return option;
    }

    private Control BuildColor(Accessor accessor)
    {
        var picker = Field<ColorPickerButton>("Color");
        picker.EditAlpha = true;
        picker.ColorChanged += color => Apply(accessor, new CoreColor(color.R, color.G, color.B, color.A));

        void Read()
        {
            picker.Color = ToGodot((CoreColor)accessor.Get()!);
            picker.Text = accessor.IsMixed ? MixedText : "";
        }

        Read();
        _refreshers.Add(Read);
        return picker;
    }

    /// <summary>구성 요소 하나(<paramref name="component"/>)가 모든 대상에서 같으면 그 값, 아니면 null.</summary>
    private static double? Common<T>(Accessor accessor, Func<T, float> component)
    {
        double? first = null;
        foreach (object? value in accessor.Values())
        {
            double v = component((T)value!);
            if (first is null)
                first = v;
            else if (Math.Abs(first.Value - v) > 0.0001)
                return null;
        }

        return first;
    }

    private Control BuildVector2(Accessor accessor)
    {
        var row = Field<HBoxContainer>("Vector2");
        var x = row.GetNode<SpinBox>("X");
        var y = row.GetNode<SpinBox>("Y");
        if (accessor.Name == nameof(KeyViewerLayout.Canvas))
        {
            x.Prefix = "W";
            y.Prefix = "H";
        }

        // 구성 요소별로 대상마다 따로 바꿈 (여러 개를 골랐을 때 다른 구성 요소는 각자 값 유지)
        BindNumber(x, () => Common<CoreVector2>(accessor, v => v.X),
            value => Update(accessor, old => new CoreVector2((float)value, ((CoreVector2)old!).Y)));
        BindNumber(y, () => Common<CoreVector2>(accessor, v => v.Y),
            value => Update(accessor, old => new CoreVector2(((CoreVector2)old!).X, (float)value)));
        return row;
    }

    private Control BuildRect(Accessor accessor)
    {
        var grid = Field<GridContainer>("Rect");
        BindNumber(grid.GetNode<SpinBox>("X"), () => Common<CoreRect>(accessor, r => r.X),
            v => Update(accessor, old => old is CoreRect r ? new CoreRect((float)v, r.Y, r.Width, r.Height) : old));
        BindNumber(grid.GetNode<SpinBox>("Y"), () => Common<CoreRect>(accessor, r => r.Y),
            v => Update(accessor, old => old is CoreRect r ? new CoreRect(r.X, (float)v, r.Width, r.Height) : old));
        BindNumber(grid.GetNode<SpinBox>("W"), () => Common<CoreRect>(accessor, r => r.Width),
            v => Update(accessor, old => old is CoreRect r ? new CoreRect(r.X, r.Y, Math.Max(1f, (float)v), r.Height) : old));
        BindNumber(grid.GetNode<SpinBox>("H"), () => Common<CoreRect>(accessor, r => r.Height),
            v => Update(accessor, old => old is CoreRect r ? new CoreRect(r.X, r.Y, r.Width, Math.Max(1f, (float)v)) : old));
        return grid;
    }

    /// <summary>입력: 키 이름을 적거나, ⌨ 버튼을 누른 뒤 실제 키를 눌러 지정.</summary>
    private Control BuildBinding(Accessor accessor)
    {
        var row = Field<HBoxContainer>("Binding");
        var edit = row.GetNode<LineEdit>("Edit");
        var capture = row.GetNode<Button>("Capture");
        string captureTooltip = capture.TooltipText;
        bool typed = false;

        void Read()
        {
            if (edit.HasFocus())
                return;
            bool mixed = accessor.IsMixed;
            edit.Text = mixed ? "" : Display((InputBinding)accessor.Get()!);
            edit.PlaceholderText = mixed ? MixedText : "";
        }

        void Submit()
        {
            if (!typed)
                return;
            typed = false;
            if (TryParseKey(edit.Text, out KeyCode key))
            {
                edit.RemoveThemeColorOverride("font_color");
                Apply(accessor, InputBinding.Keyboard(key));
            }
            else
            {
                edit.AddThemeColorOverride("font_color", new Color(1f, 0.45f, 0.45f));
            }
        }

        Read();
        edit.TextChanged += _ => typed = true;
        edit.TextSubmitted += _ => Submit();
        edit.FocusExited += () =>
        {
            Submit();
            Read();
        };
        capture.Pressed += () =>
        {
            if (_captureButton == capture)
            {
                CancelCapture();
                return;
            }

            CancelCapture();
            if (_keyboard.Status != KeyboardStatus.Running)
            {
                capture.TooltipText = "입력 모니터링 권한이 있어야 키를 눌러 지정할 수 있습니다. 키 이름을 직접 적어 주세요.";
                return;
            }

            capture.TooltipText = captureTooltip;
            _captureButton = capture;
            capture.Text = "…";
            _capture = key =>
            {
                Apply(accessor, InputBinding.Keyboard(key));
                Read();
            };
        };
        _refreshers.Add(Read);
        return row;
    }

    private Control BuildOptional(Accessor accessor, Type inner)
    {
        var box = Field<VBoxContainer>("Optional");
        var use = box.GetNode<CheckBox>("Use");
        if (accessor.IsMixed && accessor.Values().Any(v => v is null))
        {
            // 어떤 요소는 쓰고 어떤 요소는 안 씀: 켜면 모두 기본값으로
            use.Text = $"사용 ({MixedText})";
            use.ButtonPressed = false;
        }
        else if (accessor.Get() is not null)
        {
            use.ButtonPressed = true;
            Accessor innerAccessor = accessor.As(inner);
            box.AddChild(IsSection(inner) ? BuildSection(innerAccessor, 1) : BuildValue(innerAccessor));
        }

        use.Toggled += on =>
        {
            accessor.Set(on ? DefaultFor(inner) : null);
            _document.Commit(DocumentChange.Values | DocumentChange.Structure);
        };
        return box;
    }

    private void FillList(VBoxContainer list, Accessor accessor, int depth)
    {
        Type itemType = accessor.Type.IsArray ? accessor.Type.GetElementType()! : accessor.Type.GetGenericArguments()[0];
        var items = list.GetNode<VBoxContainer>("Indent/Box/Items");
        var add = list.GetNode<Button>("Indent/Box/Add");
        var values = (IList?)accessor.Get();
        if (values is null)
        {
            add.Visible = false;
            return;
        }

        if (accessor.IsMixed)
        {
            // 요소마다 목록이 다르면 한꺼번에 고치지 않음
            var label = Field<Label>("ReadOnly");
            label.Text = $"{MixedText} (요소마다 다름 · 하나만 골라서 고치세요)";
            items.AddChild(label);
            add.Visible = false;
            return;
        }

        for (int i = 0; i < values.Count; i++)
        {
            Accessor item = accessor.Item(i, itemType);
            int index = i;
            var row = Field<HBoxContainer>("ListItem");
            Control editor = IsSection(itemType) && EditableProperties(itemType).Length > 3 ? BuildSection(item, depth + 1) : BuildValue(item);
            editor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(editor);
            row.MoveChild(editor, 0); // 지우기 버튼은 오른쪽 끝
            row.GetNode<Button>("Remove").Pressed += () =>
            {
                foreach (object? list in accessor.Values())
                    ((IList)list!).RemoveAt(index);
                _document.Commit(DocumentChange.Values | DocumentChange.Structure);
            };
            items.AddChild(row);
        }

        add.Visible = !accessor.Type.IsArray;
        add.Pressed += () =>
        {
            foreach (object? list in accessor.Values())
                ((IList)list!).Add(DefaultFor(itemType));
            _document.Commit(DocumentChange.Values | DocumentChange.Structure);
        };
    }

    // ---- 공통 ----

    /// <summary>모든 대상에 같은 값을 넣습니다.</summary>
    private void Apply(Accessor accessor, object? value)
    {
        if (!accessor.IsMixed && ValueEquals(accessor.Get(), value))
            return;
        accessor.Set(value);
        _document.Commit(DocumentChange.Values, accessor.Path);
    }

    /// <summary>대상마다 지금 값을 바탕으로 새 값을 넣습니다 (구성 요소 하나만 바꿀 때).</summary>
    private void Update(Accessor accessor, Func<object?, object?> change)
    {
        accessor.Update(change);
        _document.Commit(DocumentChange.Values, accessor.Path);
    }

    private void OnKeyPressed(KeyCode key)
    {
        if (_capture is null)
            return;
        Action<KeyCode> capture = _capture;
        CancelCapture();
        capture(key);
    }

    private void CancelCapture()
    {
        if (_captureButton is not null && IsInstanceValid(_captureButton))
            _captureButton.Text = "⌨";
        _captureButton = null;
        _capture = null;
    }

    private static string Display(InputBinding binding) =>
        binding.Source == InputSource.Keyboard ? ((KeyCode)binding.Code).ToString() : binding.ToString();

    private static bool TryParseKey(string text, out KeyCode key)
    {
        text = text.Trim();
        if (text.StartsWith("Keyboard.", StringComparison.OrdinalIgnoreCase))
            text = text.Substring("Keyboard.".Length);
        key = KeyCode.Unknown;
        return text.Length > 0 && !char.IsDigit(text[0]) && Enum.TryParse(text, ignoreCase: true, out key);
    }

    private static Color ToGodot(CoreColor c) => new(c.R, c.G, c.B, c.A);

    /// <summary>새 값의 기본값. 형식에 정적 <c>Default</c> 속성이 있으면 그것을 씁니다 (RainSettings 등).</summary>
    private static object? DefaultFor(Type type)
    {
        if (type == typeof(string))
            return "";
        PropertyInfo? preset = type.GetProperty("Default", BindingFlags.Public | BindingFlags.Static);
        if (preset is not null && preset.PropertyType == type)
            return preset.GetValue(null);
        return type.IsValueType || type.GetConstructor(Type.EmptyTypes) is not null ? Activator.CreateInstance(type) : null;
    }

    private static bool IsList(Type type) => type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>));

    /// <summary>속성별로 펼쳐서 편집하는 객체 (스타일 등).</summary>
    private static bool IsSection(Type type) =>
        !type.IsPrimitive && !type.IsEnum && type != typeof(string) && type != typeof(CoreColor) && type != typeof(CoreVector2)
        && type != typeof(CoreRect) && type != typeof(InputBinding) && !IsList(type) && type != typeof(decimal)
        && EditableProperties(type).Length > 0;

    private static readonly Dictionary<Type, PropertyInfo[]> PropertyCache = new();

    /// <summary>편집할 속성: 레이아웃 파일 규칙과 같음 (부모 클래스 속성이 먼저).</summary>
    private static PropertyInfo[] EditableProperties(Type type)
    {
        if (PropertyCache.TryGetValue(type, out PropertyInfo[]? cached))
            return cached;

        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetSetMethod(false) is not null && p.GetIndexParameters().Length == 0
                        && !p.IsDefined(typeof(LayoutIgnoreAttribute), true))
            .OrderBy(p => Depth(p.DeclaringType!))
            .ThenBy(p => p.MetadataToken)
            .ToArray();
        PropertyCache[type] = properties;
        return properties;
    }

    private static int Depth(Type type)
    {
        int depth = 0;
        for (Type? t = type.BaseType; t is not null; t = t.BaseType)
            depth++;
        return depth;
    }

    /// <summary>두 값이 같은지. 목록은 항목끼리 비교합니다.</summary>
    private static bool ValueEquals(object? a, object? b)
    {
        if (a is IList la && b is IList lb && a is not string)
        {
            if (la.Count != lb.Count)
                return false;
            for (int i = 0; i < la.Count; i++)
            {
                if (!ValueEquals(la[i], lb[i]))
                    return false;
            }

            return true;
        }

        if (a is not null && b is not null && !a.GetType().IsValueType && a is not string && a.GetType() == b.GetType())
        {
            // 스타일 같은 객체: 편집할 수 있는 속성끼리 비교
            foreach (PropertyInfo property in EditableProperties(a.GetType()))
            {
                if (!ValueEquals(property.GetValue(a), property.GetValue(b)))
                    return false;
            }

            return true;
        }

        return Equals(a, b);
    }

    /// <summary>편집 대상 하나 (선택한 요소 하나 안의 같은 위치).</summary>
    private sealed record Target(Func<object?> Get, Action<object?> Set);

    /// <summary>
    /// 값 하나를 읽고 쓰는 방법. 여러 요소를 골랐으면 대상도 여러 개이고, 읽을 때는 첫 대상, 쓸 때는 모든 대상에 씁니다.
    /// 구조체 안의 값은 바꾼 뒤 부모에 다시 넣어야 하므로(복사본이라) 대상마다 부모 쪽으로 거슬러 올라가며 씁니다.
    /// </summary>
    private sealed class Accessor(Type type, Target[] targets, string path, string name = "", PropertyInfo? property = null)
    {
        public Type Type { get; } = type;

        /// <summary>문서 안의 위치 (되돌리기 합치기 키).</summary>
        public string Path { get; } = path;

        public string Name { get; } = name;

        public PropertyInfo? Property { get; } = property;

        /// <summary>목록 순서를 뺀 경로 (접은 상태 기억용).</summary>
        public string PathWithoutIndex => System.Text.RegularExpressions.Regex.Replace(Path, @"^(e\d+|multi)|\[\d+\]", "");

        /// <summary>대상마다 값이 다른지.</summary>
        public bool IsMixed
        {
            get
            {
                object? first = targets[0].Get();
                for (int i = 1; i < targets.Length; i++)
                {
                    if (!ValueEquals(first, targets[i].Get()))
                        return true;
                }

                return false;
            }
        }

        public object? Get() => targets[0].Get();

        public IEnumerable<object?> Values() => targets.Select(t => t.Get());

        public void Set(object? value)
        {
            foreach (Target target in targets)
                target.Set(value);
        }

        public void Update(Func<object?, object?> change)
        {
            foreach (Target target in targets)
                target.Set(change(target.Get()));
        }

        /// <summary>같은 값을 다른 형식으로 다룸 (Nullable 안쪽 값).</summary>
        public Accessor As(Type other) => new(other, targets, Path, Name, Property);

        public Accessor Child(PropertyInfo child)
        {
            bool parentIsValue = Type.IsValueType || Nullable.GetUnderlyingType(Type) is not null;
            Target[] children = targets.Select(t => new Target(
                () => t.Get() is { } parent ? child.GetValue(parent) : null,
                value =>
                {
                    object? parent = t.Get();
                    if (parent is null)
                        return;
                    child.SetValue(parent, value);
                    if (parentIsValue)
                        t.Set(parent); // 구조체: 바꾼 복사본을 부모에 다시 넣음
                })).ToArray();
            return new Accessor(child.PropertyType, children, $"{Path}.{child.Name}", child.Name, child);
        }

        public Accessor Item(int index, Type itemType)
        {
            Target[] items = targets.Select(t => new Target(
                () => ((IList)t.Get()!)[index],
                value => ((IList)t.Get()!)[index] = value)).ToArray();
            return new Accessor(itemType, items, $"{Path}[{index}]", $"{Name}[{index}]");
        }
    }
}
