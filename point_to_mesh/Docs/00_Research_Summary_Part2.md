# SMR 용접 로봇용 고정밀 메쉬 생성 시스템 - 연구 요약 Part 2

## 📖 문서 정보
- **작성일**: 2026-01-07
- **범위**: 로봇 통신 프로토콜 (FANUC, KUKA, ABB, UR, Doosan, Yaskawa)
- **출처 수**: 50+ 웹사이트 분석

---

## 1. FANUC 로봇 통신

### 1.1 Ethernet/IP
- **타입**: 산업용 표준 프로토콜
- **전송**: TCP/UDP
- **특성**: CIP (Common Industrial Protocol) 기반
- **Unity 통합**: 제한적 (산업용 라이브러리 필요)

### 1.2 Socket Messaging
```
포트: 18735 (기본)
주기: 8ms
형식: 텍스트 기반 명령/응답
```

**명령 예시:**
```
CLRPOS[GP:1]             # 위치 클리어
SETPOS[GP:1,1,2,3,4,5,6] # 관절 위치 설정
GETPOS[GP:1]             # 현재 위치 조회
```

### 1.3 KAREL 프로그래밍
- 로봇 컨트롤러 내 프로그램
- Socket 서버/클라이언트 구현 가능
- 외부 Unity 앱과 통신

---

## 2. KUKA RSI (Robot Sensor Interface)

### 2.1 프로토콜 특성
- **전송**: UDP (실시간)
- **주기**: 4ms (250Hz)
- **형식**: XML
- **포트**: 사용자 정의

### 2.2 XML 데이터 구조

**로봇 → 외부:**
```xml
<Rob Type="KUKA">
  <RIst X="100.0" Y="200.0" Z="300.0" A="0.0" B="90.0" C="0.0"/>
  <RSol X="100.0" Y="200.0" Z="300.0" A="0.0" B="90.0" C="0.0"/>
  <AIPos A1="0.0" A2="-90.0" A3="90.0" A4="0.0" A5="0.0" A6="0.0"/>
  <Delay D="0"/>
  <IPOC>1234567890</IPOC>
</Rob>
```

**외부 → 로봇:**
```xml
<Sen Type="ImFree">
  <RKorr X="0.1" Y="0.0" Z="0.0" A="0.0" B="0.0" C="0.0"/>
  <IPOC>1234567890</IPOC>
</Sen>
```

### 2.3 설정 파일 (RSI Configuration)

```xml
<!-- RSIContext.xml -->
<Configuration>
  <IP_NUMBER>192.168.1.100</IP_NUMBER>
  <PORT>49152</PORT>
  <SENTYPE>ImFree</SENTYPE>
  <ONLPOS>RKORR</ONLPOS>
</Configuration>
```

### 2.4 Unity 통합

```csharp
public class KukaRSI {
    private UdpClient _udp;
    private IPEndPoint _endpoint;
    
    public void SendCorrection(Vector3 correction) {
        string xml = $@"<Sen Type=""ImFree"">
            <RKorr X=""{correction.x}"" Y=""{correction.y}"" Z=""{correction.z}"" 
                   A=""0"" B=""0"" C=""0""/>
            <IPOC>{_ipoc}</IPOC>
        </Sen>";
        byte[] data = Encoding.UTF8.GetBytes(xml);
        _udp.Send(data, data.Length, _endpoint);
    }
}
```

---

## 3. ABB 로봇 통신

### 3.1 Robot Web Services (RWS)
- **전송**: HTTP/HTTPS REST API
- **형식**: JSON/XML
- **포트**: 80 (HTTP), 443 (HTTPS)
- **특성**: 비실시간, 설정/모니터링용

**API 예시:**
```
GET /rw/rapid/symbol/data/RAPID/T_ROB1/Module1/position
POST /rw/rapid/execution?action=start
```

### 3.2 Externally Guided Motion (EGM)

- **전송**: UDP (실시간)
- **주기**: 4ms (250Hz)
- **형식**: Google Protobuf
- **포트**: 사용자 정의 (기본 6510)

**Protobuf 메시지:**
```protobuf
message EgmRobot {
  optional EgmHeader header = 1;
  optional EgmFeedBack feedBack = 2;
  optional EgmPlanned planned = 3;
}

message EgmSensor {
  optional EgmHeader header = 1;
  optional EgmPlanned planned = 2;
}
```

### 3.3 Unity EGM 통합

```csharp
public class AbbEgm {
    private UdpClient _udp;
    
    public void SendTarget(double[] joints) {
        var sensor = new EgmSensor {
            Header = new EgmHeader {
                Seqno = _sequenceNumber++,
                Tm = (uint)_stopwatch.ElapsedMilliseconds
            },
            Planned = new EgmPlanned {
                Joints = new EgmJoints {
                    Joints = { joints }
                }
            }
        };
        
        byte[] data = sensor.ToByteArray();
        _udp.Send(data, data.Length);
    }
}
```

---

## 4. Universal Robots RTDE

### 4.1 Real-Time Data Exchange (RTDE)
- **전송**: TCP
- **주기**: 2ms (500Hz)
- **형식**: Binary (빅엔디안)
- **포트**: 30004

### 4.2 통신 절차

1. **연결**: TCP 소켓 연결
2. **프로토콜 버전**: 버전 협상
3. **레시피 설정**: 입출력 변수 정의
4. **동기화 시작**: 데이터 스트리밍 시작

### 4.3 레시피 구성

**출력 레시피 (로봇 → 외부):**
- `actual_q`: 실제 관절 위치
- `actual_TCP_pose`: 실제 TCP 위치
- `actual_TCP_speed`: TCP 속도
- `robot_mode`: 로봇 상태

**입력 레시피 (외부 → 로봇):**
- `input_double_register_0~47`: 사용자 변수
- `input_int_register_0~47`: 정수 변수
- `input_bit_register_0~127`: 비트 변수

### 4.4 Unity 통합

```csharp
public class UrRtde {
    private TcpClient _tcp;
    private NetworkStream _stream;
    
    public void SetupOutputRecipe(string[] variables) {
        byte[] recipe = BuildRecipePacket(
            RTDE_CONTROL_PACKAGE_SETUP_OUTPUTS,
            variables
        );
        _stream.Write(recipe, 0, recipe.Length);
    }
    
    public double[] ReadActualJoints() {
        byte[] data = ReadPacket();
        return ParseJoints(data);
    }
}
```

### 4.5 URSim (시뮬레이터)
- VirtualBox/Docker 기반
- 실제 RTDE와 동일한 프로토콜
- Unity 개발/테스트에 이상적

---

## 5. Doosan Robotics

### 5.1 통신 옵션

| 방식 | 주기 | 전송 | 용도 |
|------|------|------|------|
| TCP/IP Script | 가변 | TCP | 명령 전송 |
| Real-time | 1ms (1kHz) | UDP | 실시간 제어 |
| DRCF API | 가변 | TCP | C++ SDK |

### 5.2 Real-time 제어 (1kHz)

```cpp
// Doosan Real-time Control
void OnRtControl(LPRT_OUTPUT_DATA_LIST rtData) {
    // 1ms 주기로 호출
    double* q = rtData->actual_joint_position;
    double* tcp = rtData->actual_tcp_position;
    
    // 제어 명령 반환
    rtOutput.target_joint_position[0] = q[0] + delta;
}
```

### 5.3 DRCF (Doosan Robot Control Framework)

```python
# Python API
from DRFC import *

# 관절 이동
movej([0, 0, 90, 0, 90, 0], vel=60, acc=30)

# TCP 이동
movel(posx(400, 0, 300, 0, 180, 0), vel=100, acc=100)

# 실시간 서보
servo_l(delta_pos, time=0.002)
```

### 5.4 Unity 통합 권장
- **개발**: DRCF Emulator + Python API
- **배포**: C++ SDK로 DLL 래핑
- **실시간**: UDP 기반 custom 프로토콜

---

## 6. Yaskawa Motoman

### 6.1 통신 프로토콜

| 프로토콜 | 주기 | 전송 | 특성 |
|----------|------|------|------|
| YMConnect | 14ms (70Hz) | UDP | 모션 커맨드 |
| MotoPlus | 실시간 | VxWorks | 컨트롤러 내장 |
| HSE | 14ms | UDP | High-Speed Ethernet |

### 6.2 YMConnect

```
명령 형식: [Header][Command][Data][Checksum]
응답 형식: [Header][Status][Data][Checksum]
```

**주요 명령:**
- `0x01`: 관절 위치 조회
- `0x02`: TCP 위치 조회
- `0x10`: 관절 이동
- `0x11`: 직선 이동

### 6.3 MotoPlus SDK

VxWorks 기반 실시간 애플리케이션:

```c
void mpTask() {
    MP_CTRL_GRP_SEND_DATA sendData;
    
    while (1) {
        // 실시간 제어
        mpGetPulsePos(&currentPos);
        
        // 타겟 계산
        sendData.grp_no = 0;
        sendData.pulse[0] = targetPulse[0];
        
        mpPutPosData(&sendData);
        mpTaskDelay(1); // 1 tick
    }
}
```

### 6.4 Unity 통합

```csharp
public class YaskawaClient {
    private UdpClient _udp;
    private readonly byte[] _buffer = new byte[512];
    
    public double[] GetJointPosition() {
        byte[] cmd = BuildCommand(0x01, new byte[0]);
        _udp.Send(cmd, cmd.Length);
        
        var result = _udp.Receive(ref _endpoint);
        return ParseJoints(result);
    }
}
```

---

## 7. 제조사별 비교 요약

| 제조사 | 프로토콜 | 주기 | Unity 통합 | 시뮬레이터 |
|--------|----------|------|------------|------------|
| FANUC | Socket | 8ms | ★★☆ | ROBOGUIDE |
| KUKA | RSI | 4ms | ★★★ | WorkVisual |
| ABB | EGM | 4ms | ★★★★ | RobotStudio |
| UR | RTDE | 2ms | ★★★★★ | URSim |
| Doosan | Real-time | 1ms | ★★★★ | DRCF Emulator |
| Yaskawa | MotoPlus | <1ms | ★★★ | - |

### 7.1 Unity 통합 권장 순위

1. **Universal Robots**: RTDE 500Hz, 간편한 통합, URSim 무료
2. **Doosan Robotics**: 1kHz 실시간, ROS 지원 우수, 에뮬레이터 제공
3. **ABB**: RWS + EGM 조합, RobotStudio 강력
4. **KUKA**: RSI XML 기반, 센서 통합에 적합
5. **Yaskawa**: MotoPlus 실시간, 전문 지식 필요
6. **FANUC**: Socket Messaging, 레거시 시스템

---

## 8. 출처 목록 (Part 2)

1. FANUC Socket Messaging Manual
2. KUKA RSI Documentation
3. ABB RWS Developer Guide
4. ABB EGM Application Manual
5. Universal Robots RTDE Guide
6. Doosan Robotics Manual
7. Doosan DRCF API Documentation
8. Yaskawa MotoPlus SDK
9. Yaskawa YMConnect Protocol
10. ROS Industrial - robot_driver packages
11. GitHub - ur_rtde library
12. GitHub - abb_librws
13. GitHub - abb_libegm
14. KUKA Sunrise.OS Manual
15. Industrial Robot Communication Standards

---

*다음 문서: 00_Research_Summary_Part3.md (역기구학 및 용접 경로)*
