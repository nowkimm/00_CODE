# 연구 요약 Part 4: 특이점 회피 및 용접 경로 계획

## 4. 특이점 분석 및 회피

### 4.1 특이점의 종류

**1. 경계 특이점 (Boundary Singularity)**
- 관절이 물리적 한계에 도달
- 해결: 소프트 한계 설정

**2. 내부 특이점 (Internal Singularity)**
- 손목 특이점 (Wrist Singularity): θ5 ≈ 0°
- 어깨 특이점 (Shoulder Singularity): 손목이 축1 위에 정렬
- 팔꿈치 특이점 (Elbow Singularity): 팔이 완전히 펴짐/접힘

```
특이점 조건: det(J(θ)) = 0
또는 rank(J) < min(m, n)
```

### 4.2 특이점 감지

```python
def detect_singularity(J, threshold=0.001):
    """
    특이점 감지 및 유형 분류
    """
    w = manipulability(J)
    
    # SVD로 상세 분석
    U, S, Vt = np.linalg.svd(J)
    
    result = {
        'is_singular': w < threshold,
        'manipulability': w,
        'singular_values': S,
        'min_sv': S[-1],
        'condition_number': S[0] / S[-1] if S[-1] > 1e-10 else np.inf
    }
    
    # 어떤 방향이 제한되는지 분석
    if result['is_singular']:
        result['constrained_direction'] = U[:, -1]  # 최소 특이값 방향
    
    return result
```

### 4.3 특이점 회피 전략

#### 4.3.1 Damped Least Squares (DLS)

```python
def dls_inverse(J, damping_factor=0.05):
    """
    특이점 근처에서 안정적인 역행렬 계산
    
    J† = J^T (J J^T + λ²I)^{-1}
    """
    m, n = J.shape
    lambda_sq = damping_factor ** 2
    
    if m <= n:
        # 과결정계
        return J.T @ np.linalg.inv(J @ J.T + lambda_sq * np.eye(m))
    else:
        # 과소결정계
        return np.linalg.inv(J.T @ J + lambda_sq * np.eye(n)) @ J.T
```

#### 4.3.2 Adaptive Damping

```python
def adaptive_damping(J, w_threshold=0.01, lambda_max=0.1):
    """
    Manipulability 기반 적응형 댐핑
    """
    w = manipulability(J)
    
    if w >= w_threshold:
        return 0.0  # 특이점에서 멀면 댐핑 없음
    else:
        # 선형 증가
        return lambda_max * (1 - w / w_threshold)
```

#### 4.3.3 Singularity-Robust Task Priority

```python
def task_priority_ik(J_primary, v_primary, J_secondary, v_secondary):
    """
    작업 우선순위 기반 역기구학
    - 1차: 용접 토치 TCP 추종
    - 2차: 특이점 회피
    """
    # 1차 작업 해
    J1_pinv = np.linalg.pinv(J_primary)
    q_dot_1 = J1_pinv @ v_primary
    
    # Null space 투영
    N1 = np.eye(6) - J1_pinv @ J_primary
    
    # 2차 작업 (null space 내에서)
    q_dot_2 = N1 @ np.linalg.pinv(J_secondary @ N1) @ v_secondary
    
    return q_dot_1 + q_dot_2
```

### 4.4 Null Space 최적화

6-DOF 로봇에서 TCP 6-DOF 제약 시 null space가 없지만, 특정 축 자유 시:

```python
def null_space_optimization(J, v_task, objective_gradient):
    """
    Null space에서 2차 목표 최적화
    예: 관절 중심 유지, 장애물 회피
    """
    J_pinv = np.linalg.pinv(J)
    N = np.eye(J.shape[1]) - J_pinv @ J
    
    # 주 작업 + null space 최적화
    q_dot = J_pinv @ v_task + N @ objective_gradient
    return q_dot
```

---

## 5. 용접 경로 계획 알고리즘

### 5.1 경로 표현 방법

#### 5.1.1 이산 경로점 (Discrete Waypoints)

```python
class WeldPath:
    def __init__(self):
        self.waypoints = []  # [(position, orientation, weld_params), ...]
    
    def add_waypoint(self, pos, orient, speed, weave=None):
        self.waypoints.append({
            'position': np.array(pos),
            'orientation': Rotation.from_quat(orient),
            'speed': speed,  # mm/s
            'weave': weave   # 위빙 패턴
        })
```

#### 5.1.2 파라메트릭 경로 (Parametric Path)

```python
def parametric_path(s, control_points):
    """
    B-스플라인 기반 연속 경로
    s: 0~1 경로 파라미터
    """
    from scipy.interpolate import splprep, splev
    
    tck, u = splprep([cp[:, i] for i in range(3)], s=0, k=3)
    position = np.array(splev(s, tck)).T
    
    # 접선 벡터 (경로 방향)
    tangent = np.array(splev(s, tck, der=1)).T
    tangent /= np.linalg.norm(tangent, axis=1, keepdims=True)
    
    return position, tangent
```

### 5.2 메쉬 표면 기반 경로 추출

#### 5.2.1 에지 기반 용접선 추출

```python
def extract_weld_seam_from_mesh(mesh, dihedral_threshold=30):
    """
    메쉬에서 용접 심 자동 추출
    - 날카로운 에지(dihedral angle 큼) = 용접선 후보
    """
    import open3d as o3d
    
    # 삼각형 법선 계산
    mesh.compute_triangle_normals()
    
    weld_edges = []
    edge_to_triangles = mesh.get_non_manifold_edges()
    
    for edge in mesh.get_all_edges():
        # 인접 삼각형의 법선 각도 계산
        tris = get_adjacent_triangles(mesh, edge)
        if len(tris) == 2:
            n1, n2 = mesh.triangle_normals[tris[0]], mesh.triangle_normals[tris[1]]
            angle = np.degrees(np.arccos(np.clip(np.dot(n1, n2), -1, 1)))
            
            if angle > dihedral_threshold:
                weld_edges.append(edge)
    
    # 에지 연결하여 경로 생성
    return connect_edges_to_path(weld_edges)
```

#### 5.2.2 용접 토치 자세 계산

```python
def compute_torch_orientation(path_tangent, surface_normal, 
                               work_angle=15, travel_angle=10):
    """
    용접 토치 자세 계산
    
    work_angle: 작업각 (표면에서의 기울기)
    travel_angle: 진행각 (이동 방향 기울기)
    """
    # 기본 좌표계
    z_torch = -surface_normal  # 토치는 표면을 향함
    x_torch = path_tangent
    y_torch = np.cross(z_torch, x_torch)
    y_torch /= np.linalg.norm(y_torch)
    x_torch = np.cross(y_torch, z_torch)
    
    # 작업각 적용 (y축 중심 회전)
    R_work = Rotation.from_euler('y', work_angle, degrees=True)
    
    # 진행각 적용 (x축 중심 회전)
    R_travel = Rotation.from_euler('x', travel_angle, degrees=True)
    
    R_base = Rotation.from_matrix(np.column_stack([x_torch, y_torch, z_torch]))
    R_final = R_base * R_work * R_travel
    
    return R_final.as_quat()
```

### 5.3 경로 보간 및 시간 파라미터화

#### 5.3.1 선형 보간 (LERP/SLERP)

```python
def interpolate_waypoints(wp1, wp2, num_points):
    """선형 + 구면 보간"""
    positions = np.linspace(wp1['position'], wp2['position'], num_points)
    
    # Quaternion SLERP
    r1 = Rotation.from_quat(wp1['orientation'])
    r2 = Rotation.from_quat(wp2['orientation'])
    slerp = Slerp([0, 1], Rotation.concatenate([r1, r2]))
    orientations = slerp(np.linspace(0, 1, num_points))
    
    return positions, orientations.as_quat()
```

#### 5.3.2 Time-Optimal 경로 파라미터화

```python
def time_optimal_parameterization(path, vel_limits, acc_limits):
    """
    TOPP (Time-Optimal Path Parameterization)
    - 관절 속도/가속도 한계 고려
    """
    from toppra import ParametrizeConstAccel
    
    # 경로를 관절 공간으로 변환
    joint_path = [inverse_kinematics(wp) for wp in path]
    
    # TOPP 실행
    instance = ParametrizeConstAccel(
        ss_path=np.linspace(0, 1, len(joint_path)),
        vs_path=np.array(joint_path),
        vel_limits=vel_limits,
        acc_limits=acc_limits
    )
    
    trajectory = instance.compute_parametrization()
    return trajectory  # (time, position, velocity, acceleration)
```

### 5.4 위빙 패턴 (Weaving)

```python
class WeavePattern:
    """용접 위빙 패턴 생성"""
    
    @staticmethod
    def zigzag(amplitude, frequency, path_length):
        """지그재그 패턴"""
        s = np.linspace(0, path_length, int(path_length * frequency * 2))
        offset = amplitude * np.sin(2 * np.pi * frequency * s / path_length)
        return offset
    
    @staticmethod
    def circular(radius, frequency, path_length):
        """원형 패턴"""
        s = np.linspace(0, path_length, int(path_length * frequency))
        theta = 2 * np.pi * frequency * s / path_length
        offset_x = radius * np.cos(theta)
        offset_y = radius * np.sin(theta)
        return offset_x, offset_y
    
    @staticmethod
    def triangle(amplitude, frequency, path_length):
        """삼각파 패턴"""
        s = np.linspace(0, path_length, int(path_length * frequency * 2))
        from scipy.signal import sawtooth
        offset = amplitude * sawtooth(2 * np.pi * frequency * s / path_length, 0.5)
        return offset
```

---

## 6. Unity 통합 고려사항

### 6.1 실시간 IK 성능

| 방법 | 계산 시간 | Unity 적합성 |
|------|-----------|--------------|
| 해석적 IK | < 0.1ms | ★★★★★ |
| 수치적 IK (Newton-Raphson) | 1-5ms | ★★★☆☆ |
| CCD (Cyclic Coordinate Descent) | 0.5-2ms | ★★★★☆ |
| FABRIK | 0.3-1ms | ★★★★☆ |

### 6.2 권장 구현 전략

```csharp
// Unity에서 역기구학 호출 패턴
public class WeldingRobotController : MonoBehaviour
{
    [DllImport("RobotIK")]
    private static extern int SolveIK(
        float[] targetPose,    // [x,y,z,qx,qy,qz,qw]
        float[] currentJoints, // 현재 관절 각도
        float[] solution,      // 출력: 해
        int maxIterations
    );
    
    private float[] _jointAngles = new float[6];
    private float[] _solution = new float[6];
    
    void UpdateRobotPose(Vector3 targetPos, Quaternion targetRot)
    {
        float[] target = new float[7] {
            targetPos.x, targetPos.y, targetPos.z,
            targetRot.x, targetRot.y, targetRot.z, targetRot.w
        };
        
        int result = SolveIK(target, _jointAngles, _solution, 100);
        
        if (result > 0)
        {
            ApplyJointAngles(_solution);
            Array.Copy(_solution, _jointAngles, 6);
        }
    }
}
```

### 6.3 경로 시각화

```csharp
public class WeldPathVisualizer : MonoBehaviour
{
    public LineRenderer pathRenderer;
    public GameObject waypointPrefab;
    
    public void VisualizePath(WeldPath path)
    {
        // 경로 선 렌더링
        pathRenderer.positionCount = path.waypoints.Count;
        for (int i = 0; i < path.waypoints.Count; i++)
        {
            pathRenderer.SetPosition(i, path.waypoints[i].position);
            
            // 웨이포인트 마커
            var marker = Instantiate(waypointPrefab, 
                path.waypoints[i].position, 
                path.waypoints[i].orientation);
        }
    }
}
```

---

## 7. 참고 자료

1. Murray, R.M. - "A Mathematical Introduction to Robotic Manipulation" (PoE/Screw Theory)
2. Siciliano, B. - "Robotics: Modelling, Planning and Control" (역기구학)
3. Corke, P. - "Robotics, Vision and Control" (MATLAB 구현)
4. UR Script Manual - Universal Robots 역기구학 공식
5. KUKA RSI Documentation - 실시간 센서 인터페이스
6. Open3D Documentation - 메쉬 처리

---
*작성일: 2025-01-07*
