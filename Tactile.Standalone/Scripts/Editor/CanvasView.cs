using System;
using System.Collections.Generic;
using Godot;
using Tactile.Core.Layout;
using Tactile.Core.Layout.Elements;
using CoreRect = Tactile.Core.Primitives.Rect;

namespace Tactile.Standalone.Editor;

/// <summary>
/// 가운데 캔버스: 키뷰어 미리보기를 영역에 맞춰 축소·확대해 보여 주고, 그 위에서 요소를 선택·이동·크기 조절합니다.
/// <list type="bullet">
/// <item>클릭: 선택 · Shift/⌘ 클릭: 선택에 넣기/빼기 · 빈 곳에서 끌기: 사각형 안의 요소들 선택</item>
/// <item>선택한 요소를 끌면 함께 이동, 하나만 선택했을 때 손잡이를 끌면 크기 조절 (Shift: 10px 격자)</item>
/// <item>스냅: 다른 요소와 캔버스의 가장자리·중앙에 맞추고 분홍 안내선을 보여 줌 (Alt: 잠시 끄기)</item>
/// <item>선택한 요소(여러 개면 묶음)의 중앙점을 + 표시와 좌표로 보여 줌</item>
/// </list>
/// 노드 구성은 <c>Editor.tscn</c>의 Canvas: 미리보기(Stage)는 키뷰어가 그리고, 선택 표시(Selection)는 그 위에 이 스크립트가 그립니다.
/// </summary>
public sealed partial class CanvasView : Control
{
    private const float Padding = 32f;
    private const float HandleSize = 9f;
    private const float MaxZoom = 3f;
    private const float SnapDistance = 6f; // 화면 px
    private const float CenterMark = 7f;
    private const float DragThreshold = 3f; // 화면 px. 이보다 적게 움직이면 클릭

    private static readonly Color CanvasBorder = new(1f, 1f, 1f, 0.35f);
    private static readonly Color Outline = new(1f, 1f, 1f, 0.18f);
    private static readonly Color Hover = new(0.5f, 0.83f, 1f, 0.6f);
    private static readonly Color Selected = new(0.5f, 0.83f, 1f, 1f);
    private static readonly Color GroupBox = new(0.5f, 0.83f, 1f, 0.55f);
    private static readonly Color MarqueeFill = new(0.5f, 0.83f, 1f, 0.12f);
    private static readonly Color Guide = new(1f, 0.35f, 0.75f, 0.95f);
    private static readonly Color CanvasCenter = new(1f, 1f, 1f, 0.22f);
    private static readonly Color CheckerA = new(0.16f, 0.17f, 0.2f);
    private static readonly Color CheckerB = new(0.2f, 0.21f, 0.24f);

    private LayoutDocument _document = null!;
    private Control _selection = null!;
    private Label _message = null!;

    private float _zoom = 1f;
    private Vector2 _origin;
    private int _hover = -1;

    // 드래그 상태
    private DragMode _drag;
    private int _handle;
    private Vector2 _dragStart;
    private bool _dragMoved;
    private CoreRect _startGroup;
    private readonly List<int> _dragIndices = new();
    private readonly List<CoreRect> _dragStartBounds = new();
    private readonly List<int> _marqueeBase = new();
    private readonly List<int> _marqueeHits = new();
    private Rect2 _marquee;
    private int _clickedSelected = -1; // 이미 선택된 요소를 끌지 않고 클릭만 하면 그것만 선택

    // 스냅: 드래그를 시작할 때 움직이지 않는 요소·캔버스의 가장자리와 중앙을 모아 둠 (레이아웃 좌표)
    private readonly List<float> _targetsX = new();
    private readonly List<float> _targetsY = new();
    private float? _guideX, _guideY;

    private enum DragMode
    {
        None,
        Move,
        Resize,
        Marquee,
    }

    /// <summary>키뷰어 미리보기를 붙일 노드. 레이아웃 좌표 그대로 그리면 되고, 확대·위치는 이 노드에 적용됩니다.</summary>
    public Control Stage { get; private set; } = null!;

    /// <summary>다른 요소·캔버스의 가장자리와 중앙에 맞출지. 끌 때 Alt를 누르고 있으면 잠시 꺼집니다.</summary>
    public bool SnapEnabled { get; set; } = true;

    /// <summary>현재 확대 배율 (레이아웃 1px → 화면 px).</summary>
    public float Zoom => _zoom;

    public override void _Ready()
    {
        Stage = GetNode<Control>("%Stage");
        _selection = GetNode<Control>("%Selection");
        _message = GetNode<Label>("%Message");
        _selection.Draw += DrawSelection;
        Resized += Fit;
    }

    public void Initialize(LayoutDocument document)
    {
        _document = document;
        Fit();
    }

    /// <summary>미리보기 위에 띄울 오류 (null이면 숨김).</summary>
    public void ShowMessage(string? message)
    {
        _message.Text = message ?? "";
        _message.Visible = message is not null;
    }

    /// <summary>레이아웃이나 크기가 바뀌었을 때 확대·위치를 다시 맞추고 다시 그립니다.</summary>
    public void Fit()
    {
        if (_document is null) // Initialize 전
            return;
        var canvas = new Vector2(Math.Max(1f, _document.Layout.Canvas.X), Math.Max(1f, _document.Layout.Canvas.Y));
        Vector2 available = Size - new Vector2(Padding * 2, Padding * 2);
        _zoom = Math.Max(0.05f, Math.Min(MaxZoom, Math.Min(available.X / canvas.X, available.Y / canvas.Y)));
        _origin = ((Size - canvas * _zoom) / 2f).Floor();

        Stage.Position = _origin;
        Stage.Scale = new Vector2(_zoom, _zoom);
        QueueRedraw();
        _selection.QueueRedraw();
    }

    /// <summary>선택 표시만 다시 그립니다.</summary>
    public void Redraw() => _selection.QueueRedraw();

    // ---- 그리기 ----

    public override void _Draw()
    {
        if (_document is null) // Initialize 전
            return;

        // 투명한 부분이 보이도록 체크무늬 배경
        Vector2 canvas = new(_document.Layout.Canvas.X, _document.Layout.Canvas.Y);
        var area = new Rect2(_origin, canvas * _zoom);
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.11f, 0.12f, 0.14f));
        const float cell = 12f;
        for (float y = 0; y < area.Size.Y; y += cell)
        {
            for (float x = 0; x < area.Size.X; x += cell)
            {
                bool odd = ((int)(x / cell) + (int)(y / cell)) % 2 == 1;
                var size = new Vector2(Math.Min(cell, area.Size.X - x), Math.Min(cell, area.Size.Y - y));
                DrawRect(new Rect2(area.Position + new Vector2(x, y), size), odd ? CheckerA : CheckerB);
            }
        }
    }

    private void DrawSelection()
    {
        if (_document is null) // Initialize 전
            return;
        KeyViewerLayout layout = _document.Layout;
        Vector2 canvas = new(layout.Canvas.X, layout.Canvas.Y);
        _selection.DrawRect(new Rect2(_origin, canvas * _zoom), CanvasBorder, filled: false, width: 1f);
        DrawCross(_origin + canvas * _zoom / 2f, CenterMark * 1.5f, CanvasCenter, 1f);

        for (int i = 0; i < layout.Elements.Count; i++)
        {
            bool selected = _document.IsSelected(i);
            Color color = selected ? Selected : i == _hover ? Hover : Outline;
            _selection.DrawRect(ToScreen(layout.Elements[i].Bounds), color, filled: false, width: selected ? 2f : 1f);
        }

        Font font = GetThemeDefaultFont();
        if (_document.Selected is { } single)
        {
            Rect2 r = ToScreen(single.Bounds);
            for (int h = 0; h < 8; h++)
            {
                Vector2 p = HandlePosition(r, h);
                var handle = new Rect2(p - new Vector2(HandleSize, HandleSize) / 2f, new Vector2(HandleSize, HandleSize));
                _selection.DrawRect(handle, new Color(0.1f, 0.12f, 0.15f));
                _selection.DrawRect(handle, Selected, filled: false, width: 1.5f);
            }

            DrawCenter(single.Bounds, font, "");
        }
        else if (_document.Selection.Count > 1)
        {
            // 묶음: 전체를 감싸는 사각형과 그 중앙
            CoreRect group = GroupBounds();
            _selection.DrawRect(ToScreen(group).Grow(3f), GroupBox, filled: false, width: 1f);
            DrawCenter(group, font, $"{_document.Selection.Count}개   ");
        }

        if (_drag == DragMode.Marquee && _dragMoved)
        {
            _selection.DrawRect(_marquee, MarqueeFill);
            _selection.DrawRect(_marquee, Selected, filled: false, width: 1f);
        }

        // 스냅 안내선 (캔버스 전체를 가로지름)
        if (_drag is DragMode.Move or DragMode.Resize)
        {
            if (_guideX is { } gx)
            {
                float x = _origin.X + gx * _zoom;
                _selection.DrawLine(new Vector2(x, _origin.Y - 8), new Vector2(x, _origin.Y + canvas.Y * _zoom + 8), Guide, 1f);
            }

            if (_guideY is { } gy)
            {
                float y = _origin.Y + gy * _zoom;
                _selection.DrawLine(new Vector2(_origin.X - 8, y), new Vector2(_origin.X + canvas.X * _zoom + 8, y), Guide, 1f);
            }
        }
    }

    /// <summary>중앙점 + 표시와 "크기 · 중앙 좌표" 글자.</summary>
    private void DrawCenter(CoreRect bounds, Font font, string prefix)
    {
        Rect2 r = ToScreen(bounds);
        Vector2 center = r.GetCenter();
        DrawCross(center, CenterMark, Selected, 1.5f);
        _selection.DrawCircle(center, 2.5f, Selected);
        string text = $"{prefix}{bounds.Width:0.#} × {bounds.Height:0.#}   중앙 {bounds.X + bounds.Width / 2f:0.#}, {bounds.Y + bounds.Height / 2f:0.#}";
        _selection.DrawString(font, new Vector2(r.Position.X, r.End.Y + 18), text, HorizontalAlignment.Left, -1, 12, Selected);
    }

    private void DrawCross(Vector2 center, float size, Color color, float width)
    {
        _selection.DrawLine(center - new Vector2(size, 0), center + new Vector2(size, 0), color, width);
        _selection.DrawLine(center - new Vector2(0, size), center + new Vector2(0, size), color, width);
    }

    // ---- 마우스 ----

    public override void _GuiInput(InputEvent @event)
    {
        if (_document is null) // Initialize 전
            return;
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                if (button.Pressed)
                    BeginDrag(button.Position, button.ShiftPressed || button.IsCommandOrControlPressed());
                else
                    EndDrag();
                AcceptEvent();
                break;

            case InputEventMouseMotion motion:
                if (_drag != DragMode.None)
                    ContinueDrag(motion.Position, motion.ShiftPressed, SnapEnabled && !motion.AltPressed);
                else
                    UpdateHover(motion.Position);
                break;
        }
    }

    private void BeginDrag(Vector2 position, bool additive)
    {
        GrabFocus();
        _dragStart = position;
        _dragMoved = false;
        _clickedSelected = -1;
        _document.EndMerge();

        // 하나만 선택했을 때 손잡이가 먼저
        if (!additive && _document.Selected is { } single)
        {
            Rect2 r = ToScreen(single.Bounds);
            for (int h = 0; h < 8; h++)
            {
                if (HandlePosition(r, h).DistanceTo(position) <= HandleSize)
                {
                    _handle = h;
                    StartMoveOrResize(DragMode.Resize);
                    return;
                }
            }
        }

        int hit = HitTest(position);
        if (hit < 0)
        {
            // 빈 곳: 사각형 선택. Shift/⌘면 지금 선택에 더함
            _drag = DragMode.Marquee;
            _marqueeBase.Clear();
            if (additive)
                _marqueeBase.AddRange(_document.Selection);
            else
                _document.Select(-1);
            return;
        }

        if (additive)
        {
            _document.ToggleSelection(hit);
            return;
        }

        if (_document.IsSelected(hit))
            _clickedSelected = hit; // 묶음을 끌 수도 있으므로 놓을 때까지 선택 유지
        else
            _document.Select(hit);
        StartMoveOrResize(DragMode.Move);
    }

    private void StartMoveOrResize(DragMode mode)
    {
        _drag = mode;
        _dragIndices.Clear();
        _dragStartBounds.Clear();
        foreach (int index in _document.Selection)
        {
            _dragIndices.Add(index);
            _dragStartBounds.Add(_document.Layout.Elements[index].Bounds);
        }

        _startGroup = GroupBounds();
        CollectSnapTargets();
    }

    private void ContinueDrag(Vector2 position, bool grid10, bool snapToObjects)
    {
        if (!_dragMoved && position.DistanceTo(_dragStart) < DragThreshold)
            return;
        _dragMoved = true;

        if (_drag == DragMode.Marquee)
        {
            UpdateMarquee(position);
            return;
        }

        if (_dragIndices.Count == 0)
            return;

        Vector2 delta = (position - _dragStart) / _zoom;
        float grid = grid10 ? 10f : 1f;
        float Grid(float v) => (float)Math.Round(v / grid) * grid;
        _guideX = _guideY = null;

        // 한 축의 값 하나(가장자리)를 맞춤: 요소·캔버스에 가까우면 거기로, 아니면 격자로
        float SnapEdge(float value, List<float> targets, ref float? guideLine)
        {
            if (snapToObjects && FindSnap(targets, value, 0, out float g) is { } snapped)
            {
                guideLine = g;
                return snapped;
            }

            return Grid(value);
        }

        List<LayoutElement> elements = _document.Layout.Elements;
        if (_drag == DragMode.Move)
        {
            // 묶음 전체의 가장자리·중앙을 기준으로 맞춘 뒤 같은 거리만큼 모두 옮김
            float w = _startGroup.Width, h = _startGroup.Height;
            float x = _startGroup.X + delta.X, y = _startGroup.Y + delta.Y;
            if (snapToObjects && FindSnap(_targetsX, x, w, out float gx) is { } sx)
            {
                x = sx;
                _guideX = gx;
            }
            else
            {
                x = Grid(x);
            }

            if (snapToObjects && FindSnap(_targetsY, y, h, out float gy) is { } sy)
            {
                y = sy;
                _guideY = gy;
            }
            else
            {
                y = Grid(y);
            }

            float dx = x - _startGroup.X, dy = y - _startGroup.Y;
            for (int i = 0; i < _dragIndices.Count; i++)
            {
                CoreRect b = _dragStartBounds[i];
                elements[_dragIndices[i]].Bounds = new CoreRect(b.X + dx, b.Y + dy, b.Width, b.Height);
            }
        }
        else
        {
            // 손잡이: 0 왼위, 1 위, 2 오위, 3 오른쪽, 4 오아래, 5 아래, 6 왼아래, 7 왼쪽. 움직이는 가장자리만 맞춤
            CoreRect start = _dragStartBounds[0];
            float left = start.X, top = start.Y, right = start.Right, bottom = start.Bottom;
            if (_handle is 0 or 6 or 7) left = Math.Min(SnapEdge(left + delta.X, _targetsX, ref _guideX), right - 1);
            if (_handle is 2 or 3 or 4) right = Math.Max(SnapEdge(right + delta.X, _targetsX, ref _guideX), left + 1);
            if (_handle is 0 or 1 or 2) top = Math.Min(SnapEdge(top + delta.Y, _targetsY, ref _guideY), bottom - 1);
            if (_handle is 4 or 5 or 6) bottom = Math.Max(SnapEdge(bottom + delta.Y, _targetsY, ref _guideY), top + 1);
            elements[_dragIndices[0]].Bounds = new CoreRect(left, top, right - left, bottom - top);
        }

        _document.Commit(DocumentChange.Values, "canvas-drag");
        _selection.QueueRedraw();
    }

    private void UpdateMarquee(Vector2 position)
    {
        _marquee = new Rect2(_dragStart, Vector2.Zero).Expand(position);
        _marqueeHits.Clear();
        _marqueeHits.AddRange(_marqueeBase);
        List<LayoutElement> elements = _document.Layout.Elements;
        for (int i = 0; i < elements.Count; i++)
        {
            if (_marquee.Intersects(ToScreen(elements[i].Bounds)))
                _marqueeHits.Add(i);
        }

        _document.SetSelection(_marqueeHits);
        _selection.QueueRedraw();
    }

    private void EndDrag()
    {
        if (_drag == DragMode.None)
            return;

        // 묶음 안의 요소를 끌지 않고 클릭만 했으면 그것만 선택
        if (!_dragMoved && _clickedSelected >= 0)
            _document.Select(_clickedSelected);

        _drag = DragMode.None;
        _guideX = _guideY = null;
        _clickedSelected = -1;
        _selection.QueueRedraw();
        _document.EndMerge();
    }

    private void UpdateHover(Vector2 position)
    {
        int hover = HitTest(position);
        bool onHandle = false;
        if (_document.Selected is { } single)
        {
            Rect2 r = ToScreen(single.Bounds);
            for (int h = 0; h < 8 && !onHandle; h++)
            {
                if (HandlePosition(r, h).DistanceTo(position) <= HandleSize)
                {
                    onHandle = true;
                    MouseDefaultCursorShape = h is 0 or 4 ? CursorShape.Fdiagsize : h is 2 or 6 ? CursorShape.Bdiagsize
                        : h is 1 or 5 ? CursorShape.Vsize : CursorShape.Hsize;
                }
            }
        }

        if (!onHandle)
            MouseDefaultCursorShape = hover >= 0 ? CursorShape.Move : CursorShape.Arrow;

        if (hover != _hover)
        {
            _hover = hover;
            _selection.QueueRedraw();
        }
    }

    // ---- 스냅 ----

    private void CollectSnapTargets()
    {
        _targetsX.Clear();
        _targetsY.Clear();
        KeyViewerLayout layout = _document.Layout;
        _targetsX.Add(0);
        _targetsX.Add(layout.Canvas.X / 2f);
        _targetsX.Add(layout.Canvas.X);
        _targetsY.Add(0);
        _targetsY.Add(layout.Canvas.Y / 2f);
        _targetsY.Add(layout.Canvas.Y);
        for (int i = 0; i < layout.Elements.Count; i++)
        {
            if (_document.IsSelected(i))
                continue;
            CoreRect b = layout.Elements[i].Bounds;
            _targetsX.Add(b.X);
            _targetsX.Add(b.X + b.Width / 2f);
            _targetsX.Add(b.Right);
            _targetsY.Add(b.Y);
            _targetsY.Add(b.Y + b.Height / 2f);
            _targetsY.Add(b.Bottom);
        }
    }

    /// <summary>
    /// 길이 <paramref name="size"/>인 구간을 <paramref name="start"/>에 둘 때, 시작·중앙·끝 중 하나가 목표(가장자리·중앙)에
    /// 화면에서 <see cref="SnapDistance"/>px 안으로 가까우면 맞춘 시작 위치를 돌려줍니다. 없으면 null. (크기 조절은 size 0)
    /// </summary>
    private float? FindSnap(List<float> targets, float start, float size, out float guide)
    {
        float threshold = SnapDistance / _zoom;
        float best = float.MaxValue;
        float? result = null;
        guide = 0;
        foreach (float target in targets)
        {
            for (int k = 0; k < 3; k++)
            {
                float offset = size * k / 2f; // 0: 시작, 1: 중앙, 2: 끝
                float distance = Math.Abs(target - (start + offset));
                if (distance <= threshold && distance < best)
                {
                    best = distance;
                    result = target - offset;
                    guide = target;
                }
            }
        }

        return result;
    }

    // ---- 도우미 ----

    /// <summary>선택한 요소들을 모두 감싸는 사각형 (레이아웃 좌표).</summary>
    private CoreRect GroupBounds()
    {
        float left = float.MaxValue, top = float.MaxValue, right = float.MinValue, bottom = float.MinValue;
        foreach (LayoutElement element in _document.SelectedElements)
        {
            CoreRect b = element.Bounds;
            left = Math.Min(left, b.X);
            top = Math.Min(top, b.Y);
            right = Math.Max(right, b.Right);
            bottom = Math.Max(bottom, b.Bottom);
        }

        return left > right ? default : new CoreRect(left, top, right - left, bottom - top);
    }

    /// <summary>위에 그려진(목록 뒤쪽) 요소부터 찾습니다.</summary>
    private int HitTest(Vector2 position)
    {
        var elements = _document.Layout.Elements;
        for (int i = elements.Count - 1; i >= 0; i--)
        {
            if (ToScreen(elements[i].Bounds).HasPoint(position))
                return i;
        }

        return -1;
    }

    private Rect2 ToScreen(CoreRect bounds) =>
        new(_origin + new Vector2(bounds.X, bounds.Y) * _zoom, new Vector2(bounds.Width, bounds.Height) * _zoom);

    private static Vector2 HandlePosition(Rect2 r, int handle) => handle switch
    {
        0 => r.Position,
        1 => new Vector2(r.GetCenter().X, r.Position.Y),
        2 => new Vector2(r.End.X, r.Position.Y),
        3 => new Vector2(r.End.X, r.GetCenter().Y),
        4 => r.End,
        5 => new Vector2(r.GetCenter().X, r.End.Y),
        6 => new Vector2(r.Position.X, r.End.Y),
        _ => new Vector2(r.Position.X, r.GetCenter().Y),
    };
}
