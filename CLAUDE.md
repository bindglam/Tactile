# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Tactile은 리듬게임 모드·독립 앱에서 쓰는 크로스플랫폼 키 뷰어 라이브러리입니다. 코드 주석과 예외 메시지는 한국어로 씁니다.

## 명령

```bash
dotnet build Tactile.slnx                         # 전체 빌드 (경고 0개 유지)
dotnet build Tactile.Adapters.Godot -p:GodotVersion=4.7.2    # 컴파일용 GodotSharp 버전 지정 (기본 4.4.0)
dotnet build Tactile.Adapters.Unity -p:UnityVersion=2022.3.62 # Unity 참조 버전 지정 (기본 2019.4.41, 2021.2 이상이면 netstandard2.1)

# 얼불춤(ADOFAI) MelonLoader 모드: 빌드하면 게임 폴더의 Mods/(모드)와 UserLibs/(Core·Unity 어댑터)에 복사됨
dotnet build Tactile.ADOFAI                       # 게임 경로가 다르면 -p:GameDir="...", 복사 안 하려면 -p:Deploy=false
# 게임 로그: <게임 폴더>/MelonLoader/Latest.log, 설정: <게임 폴더>/UserData/MelonPreferences.cfg [Tactile]

# Standalone (Godot 4.7.2 .NET 앱) 실행 / 헤드리스 확인
/Applications/Godot.app/Contents/MacOS/Godot --path Tactile.Standalone
/Applications/Godot.app/Contents/MacOS/Godot --headless --path Tactile.Standalone --quit-after 60
```

- Unity 참조 어셈블리는 `NuGet.config`의 BepInEx 피드(`UnityEngine.Modules`)에서 받습니다.
- 테스트 프로젝트는 아직 없습니다. 지금까지는 `Tactile.Core`를 참조하는 임시 콘솔 프로젝트(net10.0)를 만들고, 가짜 `IInputDevice<KeyboardEvent>`로 이벤트를 넣고 `KeyViewer.Update(long timestamp)`에 시각을 직접 주어 검증했습니다. `GC.GetAllocatedBytesForCurrentThread()`로 프레임 루프 할당 0도 확인합니다. 임시 프로젝트(`.tmp-*`)는 끝나면 지웁니다.
- 렌더링 확인은 임시 Godot 프로젝트를 창 모드로 띄워 `GetViewport().GetTexture().GetImage().SavePng(...)`로 캡처합니다 (헤드리스는 렌더링이 안 됨).

## 프로젝트 구성

- **Tactile.Core**: `netstandard2.0`, 외부 의존성 없음 (모드 로더 환경의 DLL 충돌 방지). Unity Mono/IL2CPP, Godot .NET, 일반 .NET에서 모두 로드되어야 하므로 최신 BCL API를 쓰지 말 것. `record`/`init`용 타입은 `Polyfills.cs`에 있습니다.
- **Tactile.Adapters.Godot** (net8.0) / **Tactile.Adapters.Unity**: 엔진 패키지는 컴파일 전용 참조(출력에 복사 안 함).
- **Tactile.Adapters.Unity**: UnityEngine.UI 없이 엔진 모듈만 참조합니다 (BepInEx 피드에 UI 패키지가 없고, 게임마다 UI 버전이 다름). 그래서 `CanvasRenderer`에 메시를 직접 넣고(`MeshView`), 글자는 `Font.GetCharacterInfo`로 직접 배치합니다 (`TextGenerator`/`UIVertex`는 Unity 버전마다 필드 형식이 달라 쓰지 않음). 오버레이 캔버스(`TactileUnityViewer`)에 그리고, macOS 외 플랫폼은 `UnityLegacyKeyboard`(프레임 단위 `Input.GetKey` 폴링)로 대신합니다. 손은 기본적으로 2D 선 메시(`UnityHandsRenderer`)로 그립니다. 선택용 `UnityHands3DRenderer`는 멀리 떨어진 위치·전용 레이어(기본 31)에 캡슐들과 꺼 둔 정사영 카메라를 두고 `RenderTexture`에 직접 `Render()`합니다 (Core 좌표의 z만 반전).
- **Tactile.ADOFAI**: A Dance of Fire and Ice용 MelonLoader 모드 (`netstandard2.1`, Unity 6 Mono). 게임의 `Managed/`와 `MelonLoader/net35/`의 DLL을 `Private=false`로 참조합니다 (macOS는 `.app/Contents/Resources/Data/Managed`). 레이아웃은 Standalone의 `DefaultLayouts.cs`를 링크해서 씁니다. 이 PC의 게임에는 Quartz(UMM 모드를 불러오는 MelonLoader 모드)가 함께 설치되어 있습니다.
- **Tactile.Standalone**: Godot 앱. 어댑터의 `TactileGodotViewer`를 사용하는 얇은 호스트(`Scripts/Viewer/KeyViewerHost.cs`)와 기본 레이아웃(`Scripts/Layouts/DefaultLayouts.cs`).

## 아키텍처

**원칙: Core가 모든 숫자(상태, 애니메이션, 좌표, 색)를 계산하고, 엔진은 받은 값을 그리기만 합니다.**

요소 하나는 네 부분으로 나뉩니다:

1. **레이아웃 데이터** `Layout/Elements/*Element.cs` (`LayoutElement` 상속): 직렬화 대상인 순수 설정. 스타일도 요소 안에 있음 (`KeyStyle`, `BoxStyle`).
2. **로직** `Elements/Builtin/*Behavior.cs` (`ElementBehavior<TElement, TVisual>`): 입력(`OnInput`)과 프레임 갱신(`Update(in FrameContext)`)으로 상태를 계산.
3. **Visual** `Rendering/*Visual.cs`: 렌더러에 넘기는 엔진 무관 값. 매 프레임 새로 만들지 않고 갱신하며, `Revision` 등으로 바뀌었을 때만 다시 그리게 함.
4. **엔진 렌더러** `IElementRenderer<TVisual>` (예: `Tactile.Adapters.Godot/Rendering/Godot*Renderer.cs`).

두 등록표로 연결합니다 (확장성이 이 프로젝트의 핵심 요구사항 — 새 요소 종류는 등록만으로 추가 가능해야 함):

- `ElementRegistry` (Core): 요소 클래스 ↔ 종류 ID(`"tactile:key"` 형식) ↔ Behavior 팩토리. 정확한 타입으로 조회, 중복 등록은 예외.
- `RendererRegistry<TContext>` (엔진별): 요소 클래스 → 렌더러 팩토리. 부모 클래스로 폴백, `Register`는 덮어씀. `Bind(context)`로 `IElementViewProvider`를 만들어 `KeyViewer`에 넘김. Godot 기본값은 `GodotRenderers.CreateDefault()`.

`KeyViewer.Update()` 흐름: 장치에서 이벤트를 읽음 → 전역 눌림 집합으로 중복 Down/Up 제거 → 요소들의 `Bindings`에 포함된 입력만 통계(`InputStatistics`)에 기록 → 모든 Behavior에 `OnInput` → 레이아웃 순서대로 `Update` 후 `Render`. 등록되지 않은 요소는 `UnsupportedElements`에 모입니다.

### 입력 (macOS)

- `KeyboardDevice`는 별도 스레드에서 이벤트를 수집해 큐에 넣고, 이벤트 시각은 `Stopwatch` 타임스탬프입니다. `Start()`는 호출한 스레드에서 `PrepareStart()`를 먼저 실행합니다.
- **TIS/UCKeyTranslate(키 배열 조회)는 반드시 메인 스레드에서만** 호출해야 합니다. 다른 스레드에서 부르면 GUI 앱이 멈춥니다 (`MacOSNative.IsMainThread`로 확인, 아니면 위치 기반 표로 대체).
- 입력 모니터링 권한은 `InputPermissions`(IOKit `IOHIDCheckAccess`/`IOHIDRequestAccess`)로 확인·요청합니다. 권한이 없으면 `PrepareStart()`가 `UnauthorizedAccessException`을 던지고, Standalone은 이를 잡아 권한 안내 화면을 띄웁니다.

### 손 요소 (HandsElement)

ADOFAI Macro 3(github.com/CLiF-1593/ADOFAI_Macro_for_Publish, MIT)의 손 캠(`Hand.cpp`, `HandCam.cpp`)을 옮긴 것입니다. `HandsVisual`이 원작 `Hand::Simulate`(마디 길이·쉬는 각도·`Vector::Rotate`의 vᵀR 회전 순서까지 그대로)로 손 뼈대 선분 28개/손을 계산하고, 원작처럼 정사영으로 화면에 투영해 깊이 순(먼 것부터)으로 정렬하고 깊이에 따라 음영을 준 2D 선(`GetStroke`, 레이아웃 픽셀)까지 만듭니다. 기본 렌더러(`GodotHandsRenderer`, `UnityHandsRenderer`)는 그 선을 양 끝이 둥근 선으로 그리기만 하고, 3D 캡슐로 그리는 `*Hands3DRenderer`는 선택용으로 남아 있습니다 (`GetSegment` 사용). 원작은 2D 투영이라 z 부호가 정해져 있지 않아, 손등 쪽에서 보도록(오른손 엄지가 화면 왼쪽) 원작 +z를 화면 안쪽으로 둡니다. `HandsBehavior`는 원작 애니메이션(손가락 +15°, 손 zoom −10%·updown −10°, `GetDegree` 좌우 회전과 옆 이동)을 `TweenChannel`(시간 기반 Ease 구간)로 재현하되, 좌우 회전 각도에만 `RollStrength`(기본 1 = 원작)를 곱합니다. 원작은 매크로라 미리 아는 미래 입력으로 하는 "준비 동작"은 할 수 없어 모든 동작이 누른 순간부터 시작합니다. 한 손의 손가락이 하나도 눌려 있지 않을 때 누르면 새 타건 묶음이 시작됩니다.

### Godot 어댑터 주의점

- 라이브러리 클래스에서는 `_Draw`를 override할 수 없으므로 `Draw` 시그널 등을 사용합니다.
- 선택용 3D 손(`GodotHands3DRenderer`)은 `SubViewport`(`OwnWorld3D`, 투명 배경, `UpdateMode.Once`)에 캡슐을 두고 `TextureRect`로 표시합니다. 음수 스케일은 조명이 깨지므로 쓰지 않습니다.

## 규칙

- 프레임 루프(`Update`/`Render`)에서 할당하지 않습니다. 문자열·배열은 재사용하고 값이 바뀔 때만 엔진 노드에 반영합니다.
- 애니메이션은 ms 시간 상수를 쓰는 지수 접근(프레임 속도와 무관)과 스냅 임계값으로 처리해서, 멈추면 다시 그리지 않게 합니다.
