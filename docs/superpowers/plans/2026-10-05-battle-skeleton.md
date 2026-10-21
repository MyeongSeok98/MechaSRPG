# Battle Skeleton Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** 기본 Primitive를 사용하는 클릭 선택·그리드 이동·기동 회피·방향별 엄폐 테스트 Scene을 만든다.

**Architecture:** GridCell을 공간 데이터의 기준으로 삼고 BFS로 이동 범위와 경로를 구한다. 이동, 회피, 엄폐, 입력, 표현을 작은 컴포넌트로 분리한다. BattleSceneController가 초기화 순서를 조정한다.

**Tech Stack:** Unity 6000.5.5f1, C#, 기존 URP, 기존 Input System, 기존 Unity Test Framework, IMGUI.

**Spec:** [승인된 설계](../specs/2026-10-05-battle-skeleton-design.md)

## Global Constraints

- 기본 폭 10, 높이 10, 셀 크기 2m; MovementRange 기본값은 5.
- North는 월드 +Z, East는 +X, South는 -Z, West는 -X이다.
- 이동은 상하좌우만 허용한다. 한 칸 이동 비용은 1이며 대각선 이동은 없다.
- 이동 회피: 0칸=0, 1~2칸=5, 3~4칸=10, 5칸 이상=15.
- Half/Full 보너스를 각각 기본 15/30으로 관리한다.
- 외부 에셋이나 새 패키지를 추가하지 않는다. Cube와 Capsule을 사용한다.
- 기본 SampleScene을 보존하고 별도 테스트 Scene을 만든다.
- 과도한 인터페이스, 추상 클래스, 이벤트 버스, DI는 사용하지 않는다.
- 공격, 데미지, HP, 부위 파괴, 탄약, 무기, 파일럿 스탯·관계, 적 AI,
  턴 순서, AP, 부스트, 열 관리, 기체 커스터마이징, Save/Load, Mission System은 구현하지 않는다.
- 자유 배치 경계 벽, 동시 이동, 런타임 엄폐 파괴와 이동은 범위 밖이다.
- 프로젝트 루트: C:/Users/myong/Documents/Unity/MechaXCOM.
- 현재 Git 저장소가 없다. 새 저장소를 임의로 만들지 않고 커밋 단계는 생략한다.

## Review Focus

1. 필수 참조 누락과 잘못된 시작 셀: NullReference 없이 해당 기능을 중단하고 진단한다. Task 5에서 검사한다.
2. 이동 표시용 Collider의 클릭 차단: 시각 오브젝트는 클릭 판정에 참여하지 않는다. Task 6에서 검사한다.
3. 엄폐 Cube 위치의 작은 오차: 셀 중심 포함 기준으로 인접 셀을 잘못 막지 않는다. Task 3에서 검사한다.
4. 선택 유닛 제거와 이동 컴포넌트 비활성화: 점유·선택·이동 상태를 정리한다. Tasks 4, 6에서 검사한다.
5. Scene 생성 메뉴 재실행: 미저장 Scene을 무단 폐기하거나 기존 테스트 Scene을 무단 덮어쓰지 않는다. Task 7에서 검사한다.

## 파일 지도

아래 경로는 프로젝트 루트 기준이다. C# namespace는 MechaXCOM,
Editor 코드는 MechaXCOM.Editor, 테스트는 MechaXCOM.Tests를 사용한다.
Unity가 생성한 .meta를 결과물에 포함한다.

- Assets/_Game/Scripts/MechaXCOM.Runtime.asmdef: 기존 Unity.InputSystem 참조.
- Assets/_Game/Scripts/Grid/{GridCell,GridManager,GridObstacle,Pathfinder}.cs
- Assets/_Game/Scripts/Unit/{BattleUnit,UnitMovement,UnitMobility}.cs
- Assets/_Game/Scripts/Cover/{CoverTypes,CoverObject,CoverSystem}.cs
- Assets/_Game/Scripts/Input/PlayerInputController.cs
- Assets/_Game/Scripts/Presentation/{GridVisualizer,BattleDebugUI}.cs
- Assets/_Game/Scripts/Setup/BattleSceneController.cs
- Assets/_Game/Editor/MechaXCOM.Editor.asmdef: Runtime 참조, Editor 전용.
- Assets/_Game/Editor/BattleTestSceneBuilder.cs
- Assets/_Game/Tests/EditMode/MechaXCOM.EditModeTests.asmdef: Runtime 참조, TestAssemblies, Editor 전용.
- Assets/_Game/Tests/EditMode/{GridTests,PathfinderTests,CoverTests,MobilityTests,InitializationTests,SceneBuilderTests}.cs
- Assets/_Game/Tests/PlayMode/MechaXCOM.PlayModeTests.asmdef: Runtime 참조, TestAssemblies.
- Assets/_Game/Tests/PlayMode/{MovementTests,InputPresentationTests}.cs
- Assets/_Game/Scenes/BattleTest.unity; Assets/_Game/Materials/*.mat
- Assets/_Game/Prefabs/{BattleUnit,HalfCover,FullCover,GridObstacle}.prefab
- docs/battle-skeleton-guide.ko.md: 구성·사용·검증 설명.
- Artifacts/: 테스트 XML과 실행 로그. Assets 폴더 밖에 보관한다.

SceneBuilderTests가 Editor 코드를 사용하므로 EditMode asmdef는 Runtime과
MechaXCOM.Editor를 참조한다. Runtime asmdef는 Editor·테스트를 참조하지 않는다.

## 검증 실행 방법

설치 폴더를 확인한 Unity 실행 경로는 다음과 같다.
C:/Program Files/Unity/Hub/Editor/6000.5.5f1/Editor/Unity.exe.
실행 전 파일 존재와 프로젝트 사용 여부를 확인한다.
동일 프로젝트가 Editor에 열려 있으면 두 번째 Unity 프로세스로 열지 않고
열린 Editor의 Test Runner를 사용한다. 실행 수단이 없으면 실행하지 않은
검증을 명시하고 통과했다고 보고하지 않는다.

Editor가 닫혀 있을 때 프로젝트 루트에서 Artifacts 폴더를 만든 뒤 실행한다.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.5.5f1/Editor/Unity.exe' -batchmode -nographics -projectPath 'C:/Users/myong/Documents/Unity/MechaXCOM' -runTests -testPlatform EditMode -testResults 'Artifacts/editmode.xml' -logFile 'Artifacts/editmode.log'
& 'C:/Program Files/Unity/Hub/Editor/6000.5.5f1/Editor/Unity.exe' -batchmode -nographics -projectPath 'C:/Users/myong/Documents/Unity/MechaXCOM' -runTests -testPlatform PlayMode -testResults 'Artifacts/playmode.xml' -logFile 'Artifacts/playmode.log'
```

개별 작업 검증은 해당 명령에 `-testFilter MechaXCOM.Tests.GridTests`처럼 fixture 이름을 추가한다.
XML에서 대상 테스트 Passed, Failed=0을 확인하고 로그의 컴파일 오류와 예외를 확인한다.
실패 테스트 단계에서는 의도한 미구현 실패와 실행 환경 장애를 구분한다.
화면과 실제 마우스 조작은 -nographics 결과만으로 검증했다고 하지 않는다.

### Task 1: 셀 데이터·좌표 변환·점유

**Files:** 생성: GridCell.cs, GridManager.cs, CoverTypes.cs, BattleUnit.cs,
Runtime/EditMode asmdef, GridTests.cs. 각 경로는 파일 지도를 따른다.
BattleUnit은 우선 이동 범위·시작 위치·현재 위치를 정의하고 Task 4에서 이동 연결을 추가한다.

**Interfaces:**
- CoverDirection { North, East, South, West }; CoverType { None, Half, Full }.
- GridCell: Position:Vector2Int, WorldPosition:Vector3, Walkable:bool,
  OccupyingUnit:BattleUnit, Occupied:bool;
  GetCover(CoverDirection):CoverType, SetCover(CoverDirection,CoverType):void, ClearCover():void.
- GridManager: Configure(int width,int height,float cellSize):void, Generate():void,
  TryGetCell(Vector2Int,out GridCell):bool, TryWorldToGrid(Vector3,out Vector2Int):bool,
  GetWorldPosition(Vector2Int):Vector3, GetCells():IEnumerable<GridCell>,
  SetWalkable(Vector2Int,bool):void, TryPlaceUnit(BattleUnit,Vector2Int):bool,
  TryMoveUnit(BattleUnit,Vector2Int):bool, ReleaseUnit(BattleUnit):void;
  Width:int, Height:int, CellSize:float.
- BattleUnit: MovementRange:int, StartGridPosition:Vector2Int,
  CurrentGridPosition:Vector2Int; Configure(Vector2Int,int movementRange):void.

- [x] GridTests를 작성한다. fixture가 GameObject에 GridManager와 BattleUnit을 추가하고 종료 시 제거한다.
  `FirstAndLastCellCenters`: (0,0)→(1,0,1), (9,9)→(19,0,19).
  `WorldBounds`: x=-0.01과 x=20은 false, x=19.99는 셀 9.
  `OriginOffset`: 원점 (10,0,-4)의 셀 (0,0) 중심은 (11,0,-3).
  `OccupiedCannotBeOverwritten`: 다른 유닛의 같은 셀 등록은 false.
  `MoveTransfersOccupancy`: 이전 셀은 비고 새 셀은 해당 유닛을 점유자로 가진다.
  `BlockedPlacementFails`: Walkable=false인 셀에 배치하면 false.
- [x] GridTests를 실행해 미구현된 타입·메서드로 실패하는지 확인한다.
- [x] Interfaces를 구현한다. Try 계열은 맵 밖에서 false, Occupied는 점유 참조에서 계산한다.
  width/height는 최소 1, cellSize는 최소 0.1로 제한한다. 원점은 남서쪽 외곽 모서리다.
- [x] GridTests를 다시 실행해 전부 Passed인지 확인한다.

### Task 2: BFS 이동 범위와 경로

**Files:** 생성: Pathfinder.cs, PathfinderTests.cs.
**Consumes:** Task 1의 셀 조회·점유·MovementRange.
**Produces:** Pathfinder.GetReachableCells(GridManager,BattleUnit):List<GridCell>;
FindPath(GridManager,BattleUnit,Vector2Int destination):List<GridCell>.
잘못된 요청·도달 불가·시작 셀 요청은 빈 목록. 경로는 시작 셀을 제외한다.

- [x] `EmptyGridRange`: (0,0), range=5의 도달 셀은 20개이며 (5,0)을 포함하고 (6,0)을 제외한다.
  `ObstacleDetour`: 시작 (1,1), 장애물 (2,1), 목적지 (3,1)의 경로 길이는 4.
  `DetourExceedsRange`: range=3이면 같은 목적지 경로는 빈 목록.
  `OccupiedBlocksCorridor`: 3×1 맵에서 다른 유닛이 (1,0)을 점유하면 (2,0)에 도달 불가.
  `NoDiagonalAndNoStart`: 연속 경로 셀의 맨해튼 거리는 1, 시작 셀은 이동 범위에서 제외.
  `SurroundedAndOutside`: 포위된 유닛의 이동 범위와 맵 밖 목적지의 경로는 빈 목록.
- [x] PathfinderTests를 실행해 의도한 실패를 확인한다.
- [x] Queue, 거리와 이전 셀 사전을 사용하는 BFS를 구현한다.
  자신의 시작 셀만 점유 차단에서 제외하고 경로 길이에 MovementRange를 적용한다.
- [x] PathfinderTests를 실행해 전부 Passed인지 확인한다.

### Task 3: 장애물 등록과 방향별 엄폐

**Files:** 생성: GridObstacle.cs, CoverObject.cs, CoverSystem.cs, CoverTests.cs.
**Consumes:** Task 1의 셀·좌표·CoverType.
**Produces:** GridObstacle.Collider:BoxCollider;
CoverObject.Type:CoverType, Collider:BoxCollider, Configure(CoverType):void;
GridManager.RegisterObstacle(BoxCollider):List<GridCell>;
CoverSystem.Build(GridManager,IEnumerable<CoverObject>):void,
GetCoverBonus(GridCell,CoverDirection):int.

- [x] Cube를 GameObject.CreatePrimitive로 만드는 fixture와 테스트를 작성한다.
  `FullNorth`: (3,4)의 Full은 (3,3).North=Full, 보너스 30. 나머지 3방향은 None.
  `HalfFourDirections`: 엄폐 인접 4셀은 엄폐를 바라보는 방향에 Half, 보너스 15.
  `OrdinaryObstacleHasNoCover`: 이동은 막지만 4방향 None.
  `LargeCubeOuterCells`: 2셀 폭 Cube는 2셀을 막고 바깥쪽 걸을 수 있는 셀에 엄폐 제공.
  `OverlapPrefersFull`: Half/Full 등록 순서를 바꿔도 Full.
  `SmallPlacementOffset`: 2m Cube를 중심에서 0.05m 옮겨도 1셀만 막는다.
  `EmptyFootprintWarns`: 셀 중심을 포함하지 않는 Cube는 등록 0개, 이름을 담은 경고.
- [x] CoverTests를 실행해 의도한 실패를 확인한다.
- [x] BoxCollider.bounds의 XZ에 셀 중심이 포함되면 등록한다.
  회전된 Cube는 경고하고 등록하지 않는다. Full을 Half로 덮어쓰지 않는다.
  막힌 셀에 엄폐를 부여할 필요는 없고 맵 밖 이웃은 무시한다.
- [x] CoverTests를 실행해 전부 Passed인지 확인한다.

### Task 4: 이동과 기동 회피

**Files:** 생성: UnitMobility.cs, UnitMovement.cs, MobilityTests.cs, MovementTests.cs,
PlayMode asmdef. 수정: BattleUnit.cs.
**Consumes:** Tasks 1–2의 GridManager, Pathfinder.
**Produces:** UnitMobility.MovedCells:int, MovementEvasion:int;
ResetMovement():void, RecordCellMoved():void;
UnitMovement.Initialize(GridManager,BattleUnit):void, TryMove(Vector2Int):bool, IsMoving:bool;
BattleUnit.Movement:UnitMovement, Mobility:UnitMobility, MovementEvasion:int,
Initialize(GridManager):bool, IsInitialized:bool.

- [x] MobilityTests를 작성한다. TestCase(0,0),(1,5),(2,5),(3,10),(4,10),(5,15),(6,15)에서
  RecordCellMoved를 지정 횟수 호출하고 거리·회피를 비교한다. ResetMovement 후 둘 다 0.
- [x] MobilityTests를 실행해 의도한 실패를 확인한다.
- [x] UnitMobility를 구현한다. 구간 보너스 5/10/15는 SerializeField로 노출한다.
- [x] MobilityTests를 실행해 전부 Passed인지 확인한다.
- [x] MovementTests를 작성한다. `MovesAlongPathWithoutTeleport`: 시작 직후 목적지에 없고 완료 후 중심 도착.
  `BlocksSecondOrder`: 이동 중 요청 false.
  `ArrivalUpdatesOccupancyAndEvasion`: 4칸 이동 후 Moved=4, Evasion=10, 최종 셀 점유.
  `NextMoveResetsDistance`: 다음 1칸 이동 후 Moved=1, Evasion=5.
  `BlockedNextCellStopsAtLastArrival`, `DisableDuringMoveRestoresLastCell`,
  `DestroyedUnitReleasesCell`도 확인한다. 모든 대기에는 유한한 시간 제한을 둔다.
- [x] MovementTests를 실행해 의도한 실패를 확인한다.
- [x] BattleUnit과 UnitMovement를 구현한다. 기본 속도 6m/s, Coroutine의 MoveTowards로 이동한다.
  셀 도착 시만 점유·거리를 갱신한다. 중단·비활성화 시 마지막 점유 셀로 돌아가 IsMoving을 해제한다.
  유닛 제거 시 ReleaseUnit. 무효 이동 요청은 이전 회피를 변경하지 않는다.
- [x] MovementTests를 실행해 전부 Passed인지 확인한다.

### Task 5: 초기화와 진단

**Files:** 생성: BattleSceneController.cs, InitializationTests.cs.
**Consumes:** Tasks 1–4의 생성·등록·초기화.
**Produces:** BattleSceneController.InitializeBattle():bool;
SerializeField 참조: grid, coverSystem, units, obstacles, covers,
inputController, visualizer, debugUI. 마지막 3개 연결은 Task 6에서 추가한다.

- [x] `InitRegistersBeforeUnits`: 시작 셀에 장애물이 있는 유닛은 IsInitialized=false와 경고.
  `DuplicateStartWarns`: 같은 시작 셀의 두 번째 유닛은 미등록.
  `MissingGridDoesNotThrow`: grid=null은 false와 설명 경고, 예외 없음.
  `ValidFixtureNoWarnings`: 유효한 구성은 true와 예상 밖 로그 없음.
  의도한 경고는 LogAssert.Expect로 내용까지 확인한다.
- [x] InitializationTests를 실행해 의도한 실패를 확인한다.
- [x] Generate→장애물 등록→CoverSystem.Build→유닛 등록 순으로 구현한다.
  Controller.Start에서 실행하고 유닛 Awake에서 그리드에 등록하지 않는다.
  동일 Controller의 초기화 재호출은 상태를 다시 만들지 않는다.
- [x] InitializationTests를 실행해 전부 Passed인지 확인한다.

### Task 6: 클릭 입력과 화면 표시

**Files:** 생성: PlayerInputController.cs, GridVisualizer.cs, BattleDebugUI.cs,
InputPresentationTests.cs. 수정: BattleSceneController.cs.
**Consumes:** Tasks 1–5의 범위·이동 상태·엄폐·초기화.
**Produces:** PlayerInputController.Initialize(Camera,GridManager,GridVisualizer,BattleDebugUI):void,
SelectedUnit:BattleUnit, SelectUnit(BattleUnit):bool, TryMoveSelected(Vector2Int):bool;
GridVisualizer.Initialize(GridManager):void, ShowReachableCells(List<GridCell>):void,
ClearReachableCells():void, SetSelectedUnit(BattleUnit):void;
BattleDebugUI.Initialize(GridManager,CoverSystem):void, SetSelectedUnit(BattleUnit):void.

- [x] `SelectionShowsRange`: 유효한 유닛 선택은 Pathfinder 결과를 표시.
  `MovingPreventsSelection`: 이동 중 다른 유닛 선택 false.
  `DestroyedSelectionClears`: 제거 후 SelectedUnit=null, 범위·선택 표시 제거.
  `VisualsDoNotInterceptClicks`: 생성한 범위·선택 표시에 Collider 없음.
  `InvalidCellIgnored`: 맵 밖·blocked 요청 false, 이동 거리 변화 없음.
- [x] InputPresentationTests를 실행해 의도한 실패를 확인한다.
- [x] Mouse.current의 null을 확인하고 Camera Raycast로 BattleUnit을 우선 선택한다.
  유닛 외 클릭은 그리드 높이 Plane과 교차한 위치를 셀로 변환한다.
  선·범위·선택 표시용 Cube의 Collider는 생성 시 제거한다.
  이동 완료를 감지해 범위를 다시 계산한다. 표현 클래스에는 입력·게임 규칙을 넣지 않는다.
- [x] OnGUI로 좌표, Moved, Movement Evasion, 4방향 종류·보너스를 별도로 표시한다.
  UI 영역 위 마우스 클릭은 월드 입력에서 제외한다.
- [x] Controller에 입력·표시 초기화를 연결하고 InputPresentationTests의 Passed를 확인한다.

### Task 7: 테스트 Scene·Prefab·배치 메뉴

**Files:** 생성: Editor asmdef, BattleTestSceneBuilder.cs, SceneBuilderTests.cs,
BattleTest.unity, Materials, Prefabs.
**Consumes:** Tasks 1–6의 컴포넌트.
**Produces:** BattleTestSceneBuilder.BuildDefaultScene():void;
BuildSceneAtPath(string scenePath,string assetFolder):bool;
MenuItem: MechaXCOM/Create Battle Test Scene.

- [x] 임시 Assets 폴더에 생성하는 SceneBuilderTests를 작성한다.
  `DefaultSceneHasExpectedObjects`: 그리드 10×10/2m, Capsule 1개, Half 2개,
  Full 2개, 장애물 3개와 필수 참조를 확인한다.
  `ExistingOutputNotOverwritten`: 같은 경로 재생성 false, 원본 유지.
  `GeneratedPrefabsHaveComponents`: 4개 Prefab의 필요한 컴포넌트와 Collider 확인.
  종료 시 테스트가 만든 폴더만 AssetDatabase.DeleteAsset으로 제거한다.
- [x] SceneBuilderTests를 실행해 의도한 실패를 확인한다.
- [x] Builder를 구현한다. 미저장 Scene은 Editor의 저장 확인을 거치며 취소하면 중단한다.
  메뉴의 기존 BattleTest 덮어쓰기는 명시적 다이얼로그로 확인한다.
  BuildSceneAtPath는 기존 출력 경로를 거부한다. 메뉴에서 승인된 경우에만 기존 출력을 교체한다.
  시작 (1,1), Half (3,2)/(6,5), Full (3,4)/(7,7), 장애물 (4,2)/(4,3)/(4,4)를 배치한다.
  Capsule 중심 Y=1, Cube 바닥 Y=0, Half 높이 1/Full 높이 2, XZ 폭 2m.
  재사용 Prefab과 URP Material을 저장하고 모든 Scene 참조를 설정한다.
  고정 Orthographic 쿼터뷰 Camera로 전체 맵과 UI를 확인할 수 있게 구성한다.
- [x] SceneBuilderTests를 실행해 전부 Passed인지 확인한다.
- [x] 본 프로젝트에 BattleTest Scene을 생성한다. 기존 SampleScene과 Build Settings는 유지한다.

### Task 8: 통합 검증과 사용 설명

**Files:** 생성: docs/battle-skeleton-guide.ko.md, Artifacts 검증 기록.
**Consumes:** Tasks 1–7. 새 게임 기능은 추가하지 않는다.

- [x] EditMode 전체를 실행해 Passed·Failed=0과 컴파일 오류 없음을 기록한다.
- [x] PlayMode 전체를 실행해 Passed·Failed=0과 컴파일 오류 없음을 기록한다.
- [ ] BattleTest에서 실제 마우스로 선택하고 1/2/3/4/5칸 이동해 회피 5/5/10/10/15를 확인한다.
  이동이 자연스럽고 장애물 셀을 통과하지 않는지 확인한다.
- [ ] 엄폐물의 네 방향에 이동해 해당 방향의 종류와 보너스 15/30을 확인한다.
  (3,3)에서는 North=Full, South=Half가 동시에 표시되어야 한다.
- [ ] 두 번째 Capsule을 다른 시작 셀에 임시 배치해 점유 차단을 확인하고 기본 1개 구성으로 되돌린다.
  이동 중 재클릭, UI 클릭, 맵 밖 클릭을 확인한다.
- [ ] Console 오류·경고 0개와 Scene 저장·재열기 후 참조 유지를 확인한다.
- [x] guide에 생성·수정 파일, 클래스 책임, 연결 흐름, 수동 Scene 구성,
  GameObject별 컴포넌트·Inspector 참조, 테스트 방법, 회피 계산·엄폐 판정,
  향후 연결 메서드, 제외 기능, 실행한 검증과 미실행 검증을 기록한다.
- [x] 설계의 완료 조건 11개를 검증 기록과 대조한다. 미달 항목은 해결하거나 실행 장애를 명시한다.

## 자체 검토와 실행 인계

- 그리드·점유는 Task 1, 경로는 Task 2, 엄폐는 Task 3, 이동·회피는 Task 4가 담당한다.
- 초기화·방어는 Task 5, 조작·표시는 Task 6, Scene은 Task 7, 최종 검증·설명은 Task 8이 담당한다.
- 타입과 메서드 이름을 작업 사이에 일치시켰다. Review Focus 5개를 해당 작업에 배정했다.
- 문서 작성 단계에서는 제품 코드나 테스트를 작성·실행하지 않았다.
- 실행 방식은 사용자 계획 검토 후 선택한다. 같은 채팅에서 주 에이전트가 순서대로
  구현하는 방식을 추천한다. 작업 사이 참조가 밀접해 맥락을 유지하기 쉽다.
- 하위 에이전트 방식 선택 시 작업별 구현·독립 검토를 수행한다.
  직접 구현 방식도 마지막에 한 번 독립 검토를 수행하며 관련 실행 스킬을 따른다.

실행 결과: Tasks 1~7 및 자동 검증 완료. Task 8의 직접 OS 마우스·UI 시각 검사는 미실행이며, 저장된 Scene의 합성 마우스 이벤트와 카메라 렌더 검사로 기능을 검증했다. 자세한 증거는 Artifacts/progress.md와 docs/battle-skeleton-guide.ko.md를 참조한다.
