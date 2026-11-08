# Third-Party Notices

Tactile은 아래 오픈소스 소프트웨어의 일부를 포함하거나 바탕으로 합니다.
각 소프트웨어의 라이선스 조건에 따라 저작권 고지와 라이선스 전문을 함께 싣습니다.

---

## ADOFAI Macro 3

- **저장소:** https://github.com/CLiF-1593/ADOFAI_Macro_for_Publish
- **저작권자:** FFLiK
- **라이선스:** MIT
- **사용한 부분:** `ADOFAI_Macro_3/Hand.cpp`, `Hand.h`, `HandCam.cpp`, `HandCam.h`의 손 뼈대 계산과 손 캠 애니메이션
- **Tactile에서의 위치:**
  - `Tactile.Core/Rendering/HandsVisual.cs`: 손 뼈대 계산(`Hand::Simulate`)과 굵은 선 그리기
  - `Tactile.Core/Elements/Builtin/HandsBehavior.cs`: 손 캠 애니메이션
- **변경 사항:** C#으로 옮김. 실시간 입력에 맞게 미리 손가락을 드는 준비 동작을 빼고 모든 동작이 누른 순간부터 시작하도록 바꿈. 화면 투영, 깊이 음영, 좌우 기울임 배율(`RollStrength`)을 더함.

```
MIT License

Copyright (c) 2025 FFLiK

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
