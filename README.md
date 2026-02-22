# protocol (Self-Study: Framing / Parsing / Checksum / Serial)

이 레포는 “완성형 프로젝트”라기보다, 현업에서 자주 쓰는 통신 기본기인  
Protocol framing / parsing / checksum / UART(Serial) 송수신을 혼자 연습해본 기록입니다.

- 핵심: 바이트/문자 스트림을 규격(frame) 으로 만들고, 파싱(parse) 해서 유효성(checksum)까지 검증하는 흐름

---

## 1) What I practiced (요약)

- ASCII Frame 설계(Framing): Start char + ID + CMD + ADDR (+DATA) + CHECKSUM + LF
- Parsing & Validation: 길이/문법/체크섬 검증 후 구조체로 변환
- Master/Slave 시나리오: Master가 요청 프레임 생성 → Slave가 파싱 후 응답 프레임 생성
- UART(Serial) 실습: Python(pyserial) / C#(SerialPort)로 예제 프레임 송수신

---

## 2) Frame Format (ASCII Protocol)

### Start char
- Request: `$`
- Response: `:`

### Fields (HEX ASCII)
- `ID` : 2 chars (예: `01`)
- `CMD`: `W`(Write) 또는 `R`(Read)
- `ADDR`: 2 chars (예: `10`)
- `DATA`: 2 chars (Write request 또는 Read response에서만 포함)
- `CHECKSUM`: 2 chars (ASCII 합 기반)
- End: `\n` (LF)

### DATA 포함 규칙 (코드 기준)
- `$ + W`  : DATA 포함 (Write 요청)
- `$ + R`  : DATA 없음 (Read 요청)
- `: + R`  : DATA 포함 (Read 응답)
- `: + W`  : DATA 없음 (Write ACK)

(위 규칙으로 길이도 달라짐: DATA 포함 시 총 길이 11, 미포함 시 총 길이 9)  
※ 코드에서 조건/길이 검증으로 구현됨.

---

## 3) Checksum (Validation)

- 체크섬은 Start char 다음부터(= ID부터) 지정 길이만큼의 ASCII 문자 합(sum) 입니다.
- DATA가 있으면 7 bytes, 없으면 5 bytes를 합산합니다.
- 수신 시 동일 방식으로 계산한 값과 프레임에 포함된 CHECKSUM이 같아야 유효합니다.

---

## 4) Examples (from code)

- Write request (Master → Slave):
  - `$01W10AAEB\n`
- Read request (Master → Slave):
  - `$01R10??\n`  (DATA 없음, 코드 예제에서는 `$01R10...` 형태)
- Write ACK (Slave → Master):
  - `:01W100C\n`
- Read response (Slave → Master):
  - `:01R10AAEB\n` (DATA 포함)

※ 실제 예제 프레임 문자열은 코드에 포함되어 있습니다.

---

## 5) Repository Files

- `main.c` : C로 구현한 Frame build/parse + Master/Slave 테스트 코드
- `main.cs` : 동일 로직을 C#으로 구현한 버전
- `serial.py` : pyserial로 포트 자동 탐색 후 예제 프레임 송신
- `serial.cs` : C# SerialPort로 예제 프레임 송신 및 수신 이벤트 처리
- `protocol.py` : (레포에 존재) Python 버전 프로토콜 구현 파일

---

## 6) How to run (간단 실행)

### C (console)
- `main.c`는 printf 기반으로 프레임 생성/파싱/시나리오 테스트가 포함되어 있습니다.

### C# (console)
- `main.cs`는 동일한 프로토콜 로직을 C#으로 테스트합니다.

### Serial (Python)
- `serial.py`는 연결된 Serial port 목록을 찾고 첫 번째 포트를 선택해 예제 프레임을 송신합니다.
- 환경: `pip install pyserial`

### Serial (C#)
- `serial.cs`는 SerialPort로 예제 프레임 송신 + 수신 이벤트(DataReceived) 출력을 포함합니다.

---

## 7) Notes (Self-study 성격)

- 이 레포는 “프로덕션용 통신 스택”이 아니라,  
  현업 빈출 개념(Framing / Parsing / Checksum / Logging / Serial I/O) 을 손으로 구현해보는 목적입니다.


