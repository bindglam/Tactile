#!/usr/bin/env python3
"""Tactile 배포용 빌드: 프로젝트를 Release로 빌드해 build/ 폴더에 zip으로 만듭니다.

사용법: scripts/build.py <대상>... [-v 버전]

버전은 Directory.Build.props의 TactileVersion을 씁니다. -v는 이번 빌드에서만 바꿉니다 (DLL, 모드, 앱, zip 이름 모두).

대상:
  core                 Tactile.Core (라이브러리)
  unity                Tactile.Adapters.Unity + Core
  godot                Tactile.Adapters.Godot + Core
  adofai               얼불춤 MelonLoader 모드 (Mods/, UserLibs/ 구조 그대로)
  standalone-macos     독립 앱 macOS (.app)
  standalone-windows   독립 앱 Windows (.exe)
  standalone           내보내기 프리셋이 있는 독립 앱 전부
  all                  위 전부 (독립 앱은 프리셋이 있는 것만)

환경 변수:
  VERSION   -v와 같음
  GODOT     Godot 실행 파일 (기본: PATH의 godot, 없으면 /Applications/Godot.app)
  GAME_DIR  얼불춤 게임 폴더 (모드 빌드에 게임 DLL이 필요, 기본은 csproj의 Steam 경로)

예: scripts/build.py adofai standalone-macos -v 0.2.0
"""

from __future__ import annotations

import argparse
import os
import re
import shutil
import stat
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "build"
PRESETS = ROOT / "Tactile.Standalone" / "export_presets.cfg"
PROPS = ROOT / "Directory.Build.props"

# 독립 앱: 대상 → (Godot 프리셋 이름, 내보낼 파일 이름, zip 이름의 플랫폼)
STANDALONE = {
    "standalone-macos": ("macOS", "Tactile.app", "macOS"),
    "standalone-windows": ("Windows", "Tactile.exe", "Windows"),
}

# 라이브러리: 대상 → (프로젝트, zip 이름)
LIBRARIES = {
    "core": ("Tactile.Core", "Tactile-Core"),
    "unity": ("Tactile.Adapters.Unity", "Tactile-Unity"),
    "godot": ("Tactile.Adapters.Godot", "Tactile-Godot"),
}

ADOFAI_README = """\
Tactile - A Dance of Fire and Ice 모드

설치: MelonLoader가 설치된 게임 폴더에 이 압축의 Mods, UserLibs 폴더를 그대로 풀어 넣으세요.
설정: <게임 폴더>/UserData/MelonPreferences.cfg 의 [Tactile]
레이아웃: <게임 폴더>/UserData/Tactile/layout.json (처음 실행하면 기본값으로 만들어짐)
"""

COLOR = sys.stdout.isatty() and os.name != "nt"


class BuildError(Exception):
    pass


def info(message: str) -> None:
    print(f"\033[1;36m▶ {message}\033[0m" if COLOR else f"> {message}", flush=True)


def ok(message: str) -> None:
    print(f"\033[1;32m✔ {message}\033[0m" if COLOR else f"OK {message}", flush=True)


def error(message: str) -> None:
    print(f"\033[1;31m✘ {message}\033[0m" if COLOR else f"ERROR {message}", file=sys.stderr, flush=True)


# ---- 공통 ----

def solution_version() -> str:
    """Directory.Build.props의 TactileVersion (솔루션 전체 버전)."""
    match = re.search(r"<TactileVersion[^>]*>([^<]+)</TactileVersion>", PROPS.read_text(encoding="utf-8"))
    if not match:
        raise BuildError(f"{PROPS.name}에서 TactileVersion을 찾을 수 없습니다")
    return match.group(1).strip()


def dotnet_build(project: str, output: Path, version: str, *extra: str) -> None:
    command = ["dotnet", "build", str(ROOT / project), "-c", "Release", "-o", str(output),
               "-nologo", "-v", "quiet", "-clp:NoSummary", f"-p:TactileVersion={version}", *extra]
    if subprocess.run(command).returncode != 0:
        raise BuildError(f"{project} 빌드 실패")


def add_notices(folder: Path) -> None:
    """라이선스 파일 동봉 (MIT 고지 조건)."""
    shutil.copy2(ROOT / "LICENSE", folder / "LICENSE.txt")
    shutil.copy2(ROOT / "THIRD_PARTY_NOTICES.md", folder / "THIRD_PARTY_NOTICES.md")


def make_zip(source: Path, name: str) -> Path:
    """폴더 내용을 zip으로. macOS 앱이 깨지지 않도록 심볼릭 링크와 실행 권한을 보존합니다."""
    OUT.mkdir(parents=True, exist_ok=True)
    target = OUT / f"{name}.zip"
    temp = target.with_suffix(".zip.tmp")

    with zipfile.ZipFile(temp, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path in sorted(source.rglob("*")):
            relative = path.relative_to(source).as_posix()
            if path.name == ".DS_Store":
                continue

            if path.is_symlink():
                entry = zipfile.ZipInfo(relative)
                entry.create_system = 3  # Unix
                entry.external_attr = (stat.S_IFLNK | 0o755) << 16
                archive.writestr(entry, os.readlink(path))
            elif path.is_dir():
                entry = zipfile.ZipInfo(relative + "/")
                entry.create_system = 3
                entry.external_attr = (stat.S_IFDIR | 0o755) << 16 | 0x10
                archive.writestr(entry, b"")
            else:
                archive.write(path, relative)

    temp.replace(target)  # 다 쓴 뒤에 바꿔치기 (실패해도 원래 zip 유지)
    size = target.stat().st_size
    ok(f"{target.name} ({size / 1024 / 1024:.1f}MB)" if size >= 1024 * 1024 else f"{target.name} ({size // 1024}KB)")
    return target


def find_godot() -> str:
    if os.environ.get("GODOT"):
        return os.environ["GODOT"]
    found = shutil.which("godot")
    if found:
        return found
    mac = "/Applications/Godot.app/Contents/MacOS/Godot"
    if os.access(mac, os.X_OK):
        return mac
    raise BuildError("Godot을 찾을 수 없습니다. GODOT 환경 변수로 실행 파일을 지정하세요.")


def available_presets() -> set[str]:
    if not PRESETS.exists():
        return set()
    return set(re.findall(r'^name="([^"]*)"', PRESETS.read_text(encoding="utf-8"), re.MULTILINE))


# ---- 대상별 ----

def build_library(project: str, name: str, version: str, stage: Path) -> None:
    info(f"{name} 빌드")
    dotnet_build(project, stage / "bin", version)
    package = stage / "pkg"
    package.mkdir()
    # 엔진 패키지는 컴파일 전용이라 출력에 없음. Tactile DLL만 담음
    for dll in sorted((stage / "bin").glob("Tactile.*.dll")):
        shutil.copy2(dll, package)
    add_notices(package)
    make_zip(package, f"{name}-{version}")


def build_adofai(version: str, stage: Path) -> None:
    info("얼불춤 모드 빌드")
    extra = ["-p:Deploy=false"]
    if os.environ.get("GAME_DIR"):
        extra.append(f"-p:GameDir={os.environ['GAME_DIR']}")
    binary = stage / "bin"
    dotnet_build("Tactile.ADOFAI", binary, version, *extra)

    # 게임 폴더에 그대로 풀면 되는 구조
    package = stage / "pkg"
    (package / "Mods").mkdir(parents=True)
    (package / "UserLibs").mkdir()
    shutil.copy2(binary / "Tactile.ADOFAI.dll", package / "Mods")
    shutil.copy2(binary / "Tactile.Core.dll", package / "UserLibs")
    shutil.copy2(binary / "Tactile.Adapters.Unity.dll", package / "UserLibs")
    add_notices(package)
    (package / "README.txt").write_text(ADOFAI_README, encoding="utf-8")
    make_zip(package, f"Tactile-ADOFAI-{version}")


def build_standalone(target: str, version: str, stage: Path) -> None:
    preset, file_name, platform = STANDALONE[target]
    if preset not in available_presets():
        raise BuildError(f'Tactile.Standalone/export_presets.cfg에 "{preset}" 내보내기 프리셋이 없습니다. '
                         "Godot 에디터의 프로젝트 → 내보내기에서 추가하세요.")

    godot = find_godot()
    info(f"독립 앱 {preset} 내보내기")
    package = stage / "pkg"
    package.mkdir()
    output = package / file_name

    # 앱 버전은 내보내는 동안만 프리셋에 넣고 원래대로 되돌림 (프리셋 파일에는 버전을 저장해 두지 않음).
    # DLL 버전은 Godot이 C#을 빌드할 때 환경 변수 TactileVersion으로 전달
    original = PRESETS.read_bytes()
    PRESETS.write_text(with_preset_version(original.decode("utf-8"), version), encoding="utf-8")
    try:
        result = subprocess.run(
            [godot, "--headless", "--path", str(ROOT / "Tactile.Standalone"), "--export-release", preset, str(output)],
            capture_output=True, text=True, errors="replace", env={**os.environ, "TactileVersion": version})
    finally:
        PRESETS.write_bytes(original)
    log = result.stdout + result.stderr
    # Godot은 내보내기가 실패해도 종료 코드가 0일 수 있어 결과물과 로그로 확인
    if result.returncode != 0 or "Project export for preset" in log and "failed" in log or not output.exists():
        lines = [line for line in log.splitlines() if "ERROR" in line or "템플릿" in line or "template" in line]
        print("\n".join(lines[:10] or log.splitlines()[-20:]), file=sys.stderr)
        raise BuildError(f"{preset} 내보내기 실패 (내보내기 템플릿이 설치되어 있는지 확인하세요)")

    add_notices(package)
    make_zip(package, f"Tactile-{platform}-{version}")


# 프리셋의 앱 버전 칸 (macOS Info.plist, Windows 파일 정보). 숫자 버전만 받으므로 "-beta" 같은 꼬리는 뗌
PRESET_VERSION_KEYS = ("application/short_version", "application/version",
                       "application/file_version", "application/product_version")


def with_preset_version(text: str, version: str) -> str:
    numeric = re.match(r"\d+(\.\d+){0,3}", version)
    value = numeric.group(0) if numeric else "0.0.0"
    for key in PRESET_VERSION_KEYS:
        text = re.sub(rf'^{re.escape(key)}=.*$', f'{key}="{value}"', text, flags=re.MULTILINE)
    return text


# ---- 인자 처리 ----

def expand(targets: list[str]) -> list[str]:
    presets = available_presets()
    standalone = [t for t, (preset, _, _) in STANDALONE.items() if preset in presets]

    result: list[str] = []
    for target in (t.lower() for t in targets):
        if target == "all":
            result += [*LIBRARIES, "adofai", *standalone]
        elif target == "standalone":
            if not standalone:
                raise BuildError("내보내기 프리셋이 하나도 없습니다 (Tactile.Standalone/export_presets.cfg)")
            result += standalone
        elif target in LIBRARIES or target == "adofai" or target in STANDALONE:
            result.append(target)
        else:
            raise BuildError(f"알 수 없는 대상: {target} (scripts/build.py --help 참고)")

    return list(dict.fromkeys(result))  # 중복 제거, 순서 유지


def main() -> int:
    parser = argparse.ArgumentParser(
        prog="scripts/build.py", description=__doc__.split("\n\n")[0],
        epilog="\n\n".join(__doc__.split("\n\n")[2:]), formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("targets", nargs="+", metavar="대상", help="빌드할 대상 (아래 목록)")
    parser.add_argument("-v", "--version", default=os.environ.get("VERSION"),
                        help="이번 빌드에만 쓸 버전 (기본: Directory.Build.props의 TactileVersion)")
    args = parser.parse_args()

    try:
        if not args.version:
            args.version = solution_version()
        info(f"버전 {args.version}")
        for target in expand(args.targets):
            with tempfile.TemporaryDirectory(prefix="tactile-build.") as temp:
                stage = Path(temp)
                if target in LIBRARIES:
                    build_library(*LIBRARIES[target], args.version, stage)
                elif target == "adofai":
                    build_adofai(args.version, stage)
                else:
                    build_standalone(target, args.version, stage)
    except BuildError as e:
        error(str(e))
        return 1
    except KeyboardInterrupt:
        error("중단했습니다")
        return 130

    ok(f"완료 → {OUT}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
