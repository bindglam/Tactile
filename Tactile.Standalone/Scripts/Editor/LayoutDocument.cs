using System;
using System.Collections.Generic;
using Tactile.Core.Layout;
using Tactile.Core.Layout.Elements;
using Tactile.Core.Layout.Serialization;

namespace Tactile.Standalone.Editor;

/// <summary>무엇이 바뀌었는지. 화면은 이것을 보고 필요한 만큼만 다시 그립니다.</summary>
[Flags]
public enum DocumentChange
{
    None = 0,

    /// <summary>요소 값 (위치, 스타일 등).</summary>
    Values = 1,

    /// <summary>요소 목록 (추가, 삭제, 순서) 또는 레이아웃 전체 교체.</summary>
    Structure = 2,

    /// <summary>선택.</summary>
    Selection = 4,

    /// <summary>파일 경로나 저장 여부.</summary>
    File = 8,
}

/// <summary>
/// 편집 중인 레이아웃 하나: 선택(여러 개 가능), 파일 경로, 저장 여부, 되돌리기/다시 실행.
/// <para>
/// 되돌리기는 바뀔 때마다 레이아웃을 직렬화한 스냅샷을 쌓는 방식입니다 (레이아웃이 작아서 충분히 빠름).
/// 같은 값을 연속으로 바꾸는 경우(숫자 끌기, 요소 드래그)는 <c>mergeKey</c>로 한 단계로 합칩니다.
/// </para>
/// </summary>
public sealed class LayoutDocument
{
    private const int MaxUndo = 200;
    private static readonly TimeSpan MergeWindow = TimeSpan.FromSeconds(1.5);

    private readonly List<int> _selection = new();
    private readonly List<string> _undo = new();
    private readonly List<string> _redo = new();
    private string _current;
    private string _saved;
    private string? _lastMergeKey;
    private DateTime _lastMergeTime;

    public LayoutDocument(KeyViewerLayout layout, string path)
    {
        Layout = layout;
        Path = path;
        _current = _saved = LayoutSerializer.Save(layout);
    }

    /// <summary>바뀐 것을 알립니다.</summary>
    public event Action<DocumentChange>? Changed;

    public KeyViewerLayout Layout { get; private set; }

    /// <summary>저장할 파일 경로 (절대 경로).</summary>
    public string Path { get; private set; }

    /// <summary>선택한 요소들의 순서 (오름차순). 비어 있으면 레이아웃 설정.</summary>
    public IReadOnlyList<int> Selection => _selection;

    /// <summary>요소를 하나만 선택했을 때 그 순서, 아니면 -1.</summary>
    public int SelectedIndex => _selection.Count == 1 ? _selection[0] : -1;

    /// <summary>요소를 하나만 선택했을 때 그 요소, 아니면 null.</summary>
    public LayoutElement? Selected => SelectedIndex >= 0 ? Layout.Elements[SelectedIndex] : null;

    /// <summary>선택한 요소들 (목록 순서).</summary>
    public IEnumerable<LayoutElement> SelectedElements
    {
        get
        {
            foreach (int index in _selection)
                yield return Layout.Elements[index];
        }
    }

    public bool IsSelected(int index) => _selection.BinarySearch(index) >= 0;

    /// <summary>저장한 뒤 바뀐 것이 있는지.</summary>
    public bool IsDirty => _current != _saved;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>요소 하나만 선택합니다. 범위 밖(-1 등)이면 선택을 지웁니다 (레이아웃 설정).</summary>
    public void Select(int index) => SetSelection(index >= 0 ? new[] { index } : Array.Empty<int>());

    /// <summary>선택을 바꿉니다. 범위 밖이나 중복은 무시합니다.</summary>
    public void SetSelection(IEnumerable<int> indices)
    {
        var next = new SortedSet<int>();
        foreach (int index in indices)
        {
            if (index >= 0 && index < Layout.Elements.Count)
                next.Add(index);
        }

        if (next.Count == _selection.Count && next.SetEquals(_selection))
            return;
        _selection.Clear();
        _selection.AddRange(next);
        Changed?.Invoke(DocumentChange.Selection);
    }

    /// <summary>요소를 선택에 넣거나 뺍니다 (Shift/⌘ 클릭).</summary>
    public void ToggleSelection(int index)
    {
        var next = new List<int>(_selection);
        if (!next.Remove(index))
            next.Add(index);
        SetSelection(next);
    }

    /// <summary>
    /// 레이아웃을 직접 바꾼 뒤 호출합니다. 되돌리기 단계를 하나 만듭니다.
    /// </summary>
    /// <param name="change">무엇이 바뀌었는지</param>
    /// <param name="mergeKey">같은 키로 연달아 바꾸면 한 단계로 합침 (null이면 합치지 않음)</param>
    public void Commit(DocumentChange change, string? mergeKey = null)
    {
        string snapshot = LayoutSerializer.Save(Layout);
        if (snapshot == _current)
        {
            if (change != DocumentChange.None)
                Changed?.Invoke(change); // 값은 같아도 화면 갱신은 필요할 수 있음 (드래그 중 등)
            return;
        }

        DateTime now = DateTime.UtcNow;
        bool merge = mergeKey is not null && mergeKey == _lastMergeKey && now - _lastMergeTime < MergeWindow && _undo.Count > 0;
        if (!merge)
        {
            _undo.Add(_current);
            if (_undo.Count > MaxUndo)
                _undo.RemoveAt(0);
        }

        _redo.Clear();
        _current = snapshot;
        _lastMergeKey = mergeKey;
        _lastMergeTime = now;
        Changed?.Invoke(change | DocumentChange.File);
    }

    /// <summary>다음 변경을 이전 변경과 합치지 않게 합니다 (드래그를 놓았을 때 등).</summary>
    public void EndMerge() => _lastMergeKey = null;

    public void Undo() => Restore(_undo, _redo);

    public void Redo() => Restore(_redo, _undo);

    /// <summary>레이아웃 전체를 바꿉니다 (새로 만들기, 열기). 되돌리기 기록은 지웁니다.</summary>
    public void Replace(KeyViewerLayout layout, string path, bool saved)
    {
        Layout = layout;
        Path = path;
        _current = LayoutSerializer.Save(layout);
        _saved = saved ? _current : "";
        _undo.Clear();
        _redo.Clear();
        _lastMergeKey = null;
        _selection.Clear();
        Changed?.Invoke(DocumentChange.Structure | DocumentChange.Selection | DocumentChange.File);
    }

    /// <summary>파일로 저장합니다.</summary>
    /// <exception cref="System.IO.IOException">저장 실패</exception>
    public void Save(string? path = null)
    {
        path ??= Path;
        LayoutSerializer.SaveFile(Layout, path);
        Path = path;
        _saved = _current = LayoutSerializer.Save(Layout);
        Changed?.Invoke(DocumentChange.File);
    }

    private void Restore(List<string> from, List<string> to)
    {
        if (from.Count == 0)
            return;

        to.Add(_current);
        _current = from[from.Count - 1];
        from.RemoveAt(from.Count - 1);
        _lastMergeKey = null;

        Layout = LayoutSerializer.Load(_current);
        _selection.RemoveAll(index => index >= Layout.Elements.Count);
        Changed?.Invoke(DocumentChange.Structure | DocumentChange.Selection | DocumentChange.File);
    }
}
