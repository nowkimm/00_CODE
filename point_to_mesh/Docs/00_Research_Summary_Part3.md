# 연구 요약 Part 3: 역기구학 및 용접 경로 계획

## 개요
본 문서는 6-DOF 용접 로봇의 역기구학(Inverse Kinematics), Jacobian 기반 제어, 그리고 용접 경로 계획 알고리즘에 대한 연구 결과를 요약합니다.

---

## 1. 6-DOF 역기구학 (Inverse Kinematics)

### 1.1 역기구학의 정의
역기구학은 말단장치(End-Effector)의 목표 위치/자세가 주어졌을 때, 이를 달성하기 위한 각 관절의 각도를 계산하는 문제입니다.

**수학적 정의:**
```
Forward Kinematics:  θ → T (관절각도 → 말단위치/자세)
Inverse Kinematics:  T → θ (말단위치/자세 → 관절각도)
```

### 1.2 해석적 해법 (Analytical/Closed-form Solution)

#### 6R 로봇의 Pieper 조건
6개 회전 관절을 가진 로봇에서 닫힌 형태의 해를 구하려면 다음 조건 중 하나를 만족해야 합니다:

1. **구형 손목 (Spherical Wrist):** 마지막 3개 축이 한 점에서 교차
2. **3개 연속 축 평행:** 관절 축 3개가 평행
3. **3개 연속 축 교차:** 관절 축 3개가 한 점에서 교차

대부분의 산업용 용접 로봇(FANUC, KUKA, ABB, UR)은 **구형 손목 구조**를 채택합니다.

#### 분리 해법 (Decoupling Method)
구형 손목 로봇은 위치와 자세 문제를 분리하여 해결할 수 있습니다:

```
1. 손목 중심점 (Wrist Center) 계산
   P_wc = P_ee - d6 * R_ee * [0, 0, 1]^T
   
2. 처음 3개 관절로 손목 위치 결정 (θ1, θ2, θ3)
   → 기하학적 해법 적용

3. 마지막 3개 관절로 자세 결정 (θ4, θ5, θ6)
   → 오일러 각 분해
```

### 1.3 UR 로봇 역기구학 (6R 비구형 손목)

Universal Robots는 구형 손목이 아니지만 특수 구조로 닫힌 해가 존재합니다:

```python
# UR 역기구학 핵심 단계
def ur_inverse_kinematics(T_target, DH_params):
    """
    T_target: 4x4 목표 변환 행렬
    DH_params: [d1, a2, a3, d4, d5, d6] UR 파라미터
    """
    # 1단계: θ1 계산 (2개 해)
    p05 = T_target[:3, 3] - d6 * T_target[:3, 2]
    theta1 = [atan2(p05[1], p05[0]) + acos(d4/hypot(p05[0], p05[1])) - pi/2,
              atan2(p05[1], p05[0]) - acos(d4/hypot(p05[0], p05[1])) - pi/2]
    
    # 2단계: θ5 계산 (2개 해)
    theta5 = [acos((T_target[0,3]*sin(θ1) - T_target[1,3]*cos(θ1) - d4)/d6),
              -acos(...)]
    
    # 3단계: θ6 계산
    theta6 = atan2((-T_target[0,1]*sin(θ1) + T_target[1,1]*cos(θ1))/sin(θ5),
                   (T_target[0,0]*sin(θ1) - T_target[1,0]*cos(θ1))/sin(θ5))
    
    # 4단계: θ3 계산 (2개 해)
    # 5단계: θ2 계산
    # 6단계: θ4 계산
    
    return solutions  # 최대 8개 해
```

### 1.4 다중 해 선택 전략

6-DOF 로봇은 일반적으로 **최대 8개**의 역기구학 해를 가집니다:

| 선택 기준 | 설명 | 용접 적용 |
|-----------|------|-----------|
| 현재 형상 유지 | 현재 관절각에 가장 가까운 해 | ✅ 권장 |
| 관절 한계 회피 | 소프트 한계 내부 해 선택 | ✅ 필수 |
| 특이점 회피 | Manipulability 최대화 | ✅ 권장 |
| 장애물 회피 | 충돌 검사 통과 해 | ⬜ 선택 |

```python
def select_best_solution(solutions, current_joints, joint_limits):
    """최적 역기구학 해 선택"""
    valid = []
    for sol in solutions:
        # 관절 한계 검사
        if not all(limits[i][0] <= sol[i] <= limits[i][1] for i in range(6)):
            continue
        # 연속성 검사 (급격한 변화 방지)
        if max(abs(sol - current_joints)) > np.pi/2:
            continue
        valid.append(sol)
    
    # 현재 형상에 가장 가까운 해 선택
    if valid:
        return min(valid, key=lambda s: np.linalg.norm(s - current_joints))
    return None
```

---

## 2. Screw Theory와 Product of Exponentials (PoE)

### 2.1 Screw Theory 기초

**Screw Axis (스크류 축):**
- 회전축 또는 이동축을 6차원 벡터로 표현
- $\xi = (\omega, v)$ where $\omega$: 회전축, $v$: 선속도

**Twist (운동 스크류):**
```
ξ = [ω]   (회전 관절)
    [v]

- 회전 관절: v = -ω × q (q는 축 위의 점)
- 이동 관절: ω = 0, v = 이동 방향
```

### 2.2 Product of Exponentials 공식

**순기구학 PoE 형태:**
```
T(θ) = e^{[ξ1]θ1} · e^{[ξ2]θ2} · ... · e^{[ξn]θn} · M

여기서:
- [ξi]: 스크류 축의 se(3) 행렬 표현
- θi: i번째 관절 변수
- M: 영위치에서의 말단 변환 행렬
```

### 2.3 Paden-Kahan Sub-problems

역기구학을 기본 하위 문제로 분해하여 해결하는 방법:

**Sub-problem 1: 단일 축 회전**
```
주어진: p, q, ξ
목표: e^{[ξ]θ} · p = q 를 만족하는 θ

해법:
u = p - r, v = q - r  (r은 축 위의 점)
u' = u - ω(ω·u), v' = v - ω(ω·v)
θ = atan2(ω · (u' × v'), u' · v')
```

**Sub-problem 2: 두 축 교차 회전**
```
주어진: p, q, ξ1, ξ2
목표: e^{[ξ1]θ1} · e^{[ξ2]θ2} · p = q

해법:
c = (||p||² - ||q||² + 2·q·r - 2·p·r) / (2·(r-p)·(q-r))
θ1, θ2 를 기하학적으로 해석
```

**Sub-problem 3: 축-거리 문제**
```
주어진: p, δ, ξ
목표: ||e^{[ξ]θ} · p - q|| = δ

해법:
원과 구의 교점 계산
```

### 2.4 PoE 기반 역기구학 장점

| 장점 | DH 방법 대비 |
|------|-------------|
| 기하학적 직관 | 명확한 물리적 해석 |
| 특이점 분석 용이 | Jacobian 유도 간편 |
| 수치 안정성 | 누적 오차 감소 |
| 병렬 축 처리 | α=0° 문제 없음 |

---

## 3. Jacobian 기반 속도 제어

### 3.1 기하학적 Jacobian

말단 속도와 관절 속도의 관계:
```
V_ee = J(θ) · θ̇

여기서:
V_ee = [v]  ∈ R^6 (선속도 + 각속도)
       [ω]
θ̇ ∈ R^n (관절 속도)
J(θ) ∈ R^(6×n) (Jacobian 행렬)
```

### 3.2 Jacobian 계산 (PoE 공식)

```python
def compute_jacobian(joint_angles, screws, M):
    """
    PoE 기반 기하학적 Jacobian 계산
    """
    J = np.zeros((6, len(joint_angles)))
    T = np.eye(4)
    
    for i, (theta, screw) in enumerate(zip(joint_angles, screws)):
        # 현재 관절까지의 변환
        omega, v = screw[:3], screw[3:]
        
        # Adjoint 변환 적용
        if i == 0:
            J[:, i] = screw
        else:
            Ad_T = adjoint_matrix(T)
            J[:, i] = Ad_T @ screw
        
        # 누적 변환 업데이트
        T = T @ exp_twist(screw, theta)
    
    return J
```

### 3.3 역속도 기구학 (Resolved Rate Control)

```python
def resolved_rate_control(J, V_desired, damping=0.01):
    """
    Damped Least Squares (DLS) 방법
    
    θ̇ = J^T (J J^T + λ²I)^{-1} V
    """
    JJT = J @ J.T
    JJT_damped = JJT + damping**2 * np.eye(6)
    theta_dot = J.T @ np.linalg.solve(JJT_damped, V_desired)
    return theta_dot
```

### 3.4 Manipulability (조작성)

특이점으로부터의 거리를 측정하는 지표:

```python
def manipulability(J):
    """Yoshikawa Manipulability Index"""
    return np.sqrt(np.linalg.det(J @ J.T))

def manipulability_ellipsoid(J):
    """속도 타원체 계산"""
    U, S, Vt = np.linalg.svd(J)
    return U, S  # 주축 방향과 크기
```

**용접에서의 Manipulability 활용:**
- 임계값 w_min = 0.01 ~ 0.05 설정
- w < w_min 시 경로 재계획 또는 우회

